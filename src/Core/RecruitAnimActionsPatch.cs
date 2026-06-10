using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Geoscape.View.DataObjects;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Animations;
using System;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// FROZEN-IDLE FIX. The mod's <see cref="HumanClassificationPatch"/> makes the marked Arthron recruit
    /// report <c>CheckIsHuman()==true</c> (so it routes into the soldier roster / edit screen). A side effect:
    /// the <see cref="UnitDisplayData"/> ctor [G UnitDisplayData.cs:70-73] then assigns the HUMAN
    /// <c>sharedData.SoldierEditAnimActions</c> to <c>AnimActionDef</c> instead of the crab's native
    /// anim-actions, so the human <see cref="TacActorAnimActionsDef"/> gets bound onto the crab rig +
    /// crab RuntimeAnimatorController. <c>ResetCharacterAnimation</c> then does <c>Animator.Play(0,-1,0)</c>
    /// [G CommonCharacterUtils.cs:66-72] but the human anim-actions has no valid default/idle for the crab
    /// controller → the model freezes. The crab rig + controller are already correct; ONLY the AnimActionDef
    /// is wrong.
    ///
    /// FIX: Postfix the <c>UnitDisplayData(TacCharacterDef, SharedData)</c> ctor; when the template carries
    /// the recruit marker tag (the SAME predicate as <see cref="HumanClassificationPatch"/>), overwrite
    /// <c>__instance.AnimActionDef = template.GetAnimActionDef()</c> (the crab's native
    /// <c>TacActorAnimActionsDef</c> = the ComponentSet's anim-actions [G TacCharacterDef.cs:187]). This
    /// reverses ONLY the human-anim assignment for our marked unit; every other unit / classification path is
    /// untouched.
    /// </summary>
    internal static class RecruitAnimActionsPatch
    {
        internal const string PatchId = "Morgott.TheTurned.RecruitAnimActions";
        private static bool _applied;
        private static bool _diagLogged;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                ConstructorInfo target = AccessTools.Constructor(typeof(UnitDisplayData),
                    new[] { typeof(TacCharacterDef), typeof(SharedData) });
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitAnimActionsPatch: UnitDisplayData(TacCharacterDef, SharedData) ctor not found — patch skipped.");
                    return;
                }
                MethodInfo postfix = AccessTools.Method(typeof(RecruitAnimActionsPatch), nameof(Ctor_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] RecruitAnimActionsPatch: UnitDisplayData ctor Postfix applied (marker-scoped crab anim-actions).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitAnimActionsPatch apply failed: " + e);
            }
        }

        // Postfix: runs AFTER the ctor. `template` is the ctor's first arg (Harmony binds by name).
        private static void Ctor_Postfix(UnitDisplayData __instance, TacCharacterDef template)
        {
            try
            {
                GameTagDef marker = Tags.RecruitMarkerTag;
                if (__instance == null || marker == null || template?.Data?.GameTags == null)
                {
                    return;
                }
                if (!template.Data.GameTags.Contains(marker))
                {
                    return; // not our recruit — leave the native (human or alien) anim-actions untouched
                }

                TacActorAnimActionsDef crabAnim = template.GetAnimActionDef();
                TacActorAnimActionsDef wasHuman = __instance.AnimActionDef;
                if (crabAnim != null)
                {
                    __instance.AnimActionDef = crabAnim; // crab native anim-actions onto the crab rig/controller
                }

                if (!_diagLogged)
                {
                    _diagLogged = true;
                    // What was wrongly assigned (the human soldier set) vs the crab native set we restore, plus
                    // the crab set's BaseAnimActions + AnimActions count (proves the crab set is non-null and
                    // populated). NOTE: the LIVE rig RuntimeAnimatorController + its state names are NOT reachable
                    // from this ctor (the rig is built later by the display module), and AnimationClip.name would
                    // pull in UnityEngine.AnimationModule (not referenced) — so this diagnostic confirms the
                    // def-level fix; if the model is still frozen after this, the next round must dump the live
                    // controller's state[0] at DisplaySoldier time to confirm Play(0,-1,0) lands on the idle.
                    TheTurnedMain.LogInfo("[TheTurned] RecruitAnimActions DIAG for '" + (template.name ?? "<null>") + "': "
                        + "was(human)='" + (wasHuman?.name ?? "<null>") + "' -> crab='" + (crabAnim?.name ?? "<null>") + "' "
                        + "crab.BaseAnimActions='" + (crabAnim?.BaseAnimActions?.name ?? "<null>") + "' "
                        + "crab.AnimActions=" + (crabAnim?.AnimActions?.Length.ToString() ?? "<null>") + ".");
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitAnimActionsPatch Ctor_Postfix threw: " + e);
            }
        }
    }
}
