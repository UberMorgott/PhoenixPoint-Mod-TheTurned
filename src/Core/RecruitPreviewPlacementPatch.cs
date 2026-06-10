using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.DataObjects;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// PREVIEW-MODEL OFFSET FIX. The Arthron recruit shares the vanilla Crabman
    /// <c>TacticalActorViewDef.ViewElementDef</c> (TurnedRecruiter shares the ComponentSetDef). TFTV patches
    /// that VED's <c>BuilderViewParamDef</c> but only corrects Z, leaving an enemy-tuned X — so on the hire /
    /// character / augment screens the recruit's preview model is shifted LEFT.
    ///
    /// All three screens position the preview in <c>UIModuleActorCycle.OnCharacterRebuilded()</c>
    /// [G UIModuleActorCycle.cs:448-487]. At :476-487 it branches on the displayed character's
    /// <c>ClassViewElementDef.BuilderViewParamDef</c>: when null it applies the module's serialized
    /// <c>DefaultBuilderViewParams</c> (the centered default soldiers use); else it reads the def's
    /// per-class params. Because our recruit's shared VED carries a (TFTV-patched, X-offset) param, it takes
    /// the else-branch and is mis-centered.
    ///
    /// FIX: a marker-scoped Postfix on <c>OnCharacterRebuilded</c>. When the displayed character is a
    /// TheTurned recruit (detected via the SAME marker tag as <see cref="HumanClassificationPatch"/> /
    /// <see cref="RecruitAnimActionsPatch"/>, read off the resolved <see cref="TacCharacterDef"/>'s
    /// <c>Data.GameTags</c>), FORCE the null-branch placement: copy the module's <c>DefaultBuilderViewParams</c>
    /// onto <c>_charRoot.localPosition/localScale</c> + <c>_platform.localScale</c>. This reproduces the
    /// default-soldier placement EXACTLY and touches nothing for any other unit (the shared enemy VED is left
    /// alone, so enemy/crab previews are unaffected).
    ///
    /// Marker note: the marker lives on <c>TacCharacterDef.Data.GameTags</c> (not
    /// <c>TacticalActorBaseDef.GameTags</c>), and <c>GeoCharacter.GameTags</c> is reinit'd from the base def
    /// only [G GeoCharacter.cs:451], so <c>UnitDisplayData.GameTags</c> is NOT a reliable marker source.
    /// Hence we resolve the <c>TacCharacterDef</c> from <c>UnitDisplayData.BaseObject</c>
    /// (TacCharacterDef | GeoCharacter.TemplateDef | GeoUnitDescriptor.UnitType.TemplateDef) and read its
    /// <c>Data.GameTags</c> directly — the same authoritative source the other recruit patches use.
    /// </summary>
    internal static class RecruitPreviewPlacementPatch
    {
        internal const string PatchId = "Morgott.TheTurned.RecruitPreviewPlacement";
        private static bool _applied;
        private static bool _diagLogged;

        // Private fields on UIModuleActorCycle [G UIModuleActorCycle.cs:110,134,136,144].
        private static readonly FieldInfo DisplayedCharacterField =
            AccessTools.Field(typeof(UIModuleActorCycle), "_displayedCharacter");
        private static readonly FieldInfo CharRootField =
            AccessTools.Field(typeof(UIModuleActorCycle), "_charRoot");
        private static readonly FieldInfo PlatformField =
            AccessTools.Field(typeof(UIModuleActorCycle), "_platform");
        private static readonly FieldInfo DefaultParamsField =
            AccessTools.Field(typeof(UIModuleActorCycle), "DefaultBuilderViewParams");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo target = AccessTools.Method(typeof(UIModuleActorCycle), "OnCharacterRebuilded");
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitPreviewPlacementPatch: UIModuleActorCycle.OnCharacterRebuilded not found — patch skipped.");
                    return;
                }
                MethodInfo postfix = AccessTools.Method(typeof(RecruitPreviewPlacementPatch), nameof(OnCharacterRebuilded_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] RecruitPreviewPlacementPatch: OnCharacterRebuilded Postfix applied (marker-scoped centered placement).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitPreviewPlacementPatch apply failed: " + e);
            }
        }

        // Postfix: runs AFTER the vanilla branch has set the (possibly off-center) placement. For a marked
        // recruit ONLY, overwrite with the module's DefaultBuilderViewParams (the centered default-soldier
        // placement). Any non-recruit unit is left exactly as vanilla set it.
        private static void OnCharacterRebuilded_Postfix(UIModuleActorCycle __instance)
        {
            try
            {
                if (__instance == null
                    || DisplayedCharacterField == null || CharRootField == null
                    || PlatformField == null || DefaultParamsField == null)
                {
                    return;
                }

                GameTagDef marker = Tags.RecruitMarkerTag;
                if (marker == null)
                {
                    return;
                }

                if (!(DisplayedCharacterField.GetValue(__instance) is UnitDisplayData displayed))
                {
                    return;
                }

                TacCharacterDef template = ResolveTemplate(displayed);
                if (template?.Data?.GameTags == null || !template.Data.GameTags.Contains(marker))
                {
                    return; // not our recruit — leave vanilla placement untouched
                }

                if (!(DefaultParamsField.GetValue(__instance) is PhoenixPoint.Common.UI.CharacterBuilderViewParametersDef defaults)
                    || defaults == null)
                {
                    return;
                }

                var charRoot = CharRootField.GetValue(__instance) as UnityEngine.Transform;
                var platform = PlatformField.GetValue(__instance) as UnityEngine.Transform;
                if (charRoot == null || platform == null)
                {
                    return;
                }

                charRoot.localPosition = defaults.ObjectWorldPosition;
                charRoot.localScale = defaults.ObjectScale;
                platform.localScale = defaults.PlatformScale;

                if (!_diagLogged)
                {
                    _diagLogged = true;
                    TheTurnedMain.LogInfo("[TheTurned] RecruitPreviewPlacement: forced default placement for '"
                        + (template.name ?? "<null>") + "' pos=" + defaults.ObjectWorldPosition
                        + " scale=" + defaults.ObjectScale + " platform=" + defaults.PlatformScale + ".");
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitPreviewPlacementPatch Postfix threw: " + e);
            }
        }

        // Resolve the source TacCharacterDef from UnitDisplayData.BaseObject. The three UnitDisplayData ctors
        // [G UnitDisplayData.cs:39-84] set BaseObject to a GeoCharacter, a GeoUnitDescriptor, or the
        // TacCharacterDef template directly — cover all three.
        private static TacCharacterDef ResolveTemplate(UnitDisplayData displayed)
        {
            object baseObj = displayed?.BaseObject;
            switch (baseObj)
            {
                case TacCharacterDef def:
                    return def;
                case GeoCharacter character:
                    return character.TemplateDef;
                case GeoUnitDescriptor descriptor:
                    return descriptor.UnitType?.TemplateDef;
                default:
                    return null;
            }
        }
    }
}
