using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2 apply path: enforce the matched bodypart+hand SET (C3) when an augment-screen card is
    /// equipped for the recruit. A Bionics card places exactly ONE GeoItem (the arm/head bodypart) — its
    /// matched HAND weapon is NOT brought along [G UIModuleBionics.cs:196]. We Postfix both stages:
    ///   - <c>OnAugmentClicked</c> (preview, [G :174]) — after the native SetItems, swap in the matched
    ///     hand so the 3D model previews the full arm, and patch <c>CharacterCurrentItems</c> so the commit
    ///     persists the hand.
    ///   - <c>OnAugmentApplied</c> (commit, [G :213]) — belt-and-braces re-enforce on the live character.
    /// The live 3D model is re-rendered via the screen's own <c>_actorCycleModule.DisplaySoldier(..,
    /// resetAnimation:false)</c> [G UIModuleActorCycle.cs:609].
    /// </summary>
    internal static class BionicsApplyPatch
    {
        internal const string PatchId = "Morgott.TheTurned.BionicsApply";
        private static bool _applied;

        private static readonly FieldInfo ActorCycleField =
            AccessTools.Field(typeof(UIModuleBionics), "_actorCycleModule");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo clicked = AccessTools.Method(typeof(UIModuleBionics), "OnAugmentClicked");
                MethodInfo applied = AccessTools.Method(typeof(UIModuleBionics), "OnAugmentApplied");
                MethodInfo clickedPost = AccessTools.Method(typeof(BionicsApplyPatch), nameof(OnAugmentClicked_Postfix));
                MethodInfo appliedPost = AccessTools.Method(typeof(BionicsApplyPatch), nameof(OnAugmentApplied_Postfix));
                harmony.Patch(clicked, postfix: new HarmonyMethod(clickedPost));
                harmony.Patch(applied, postfix: new HarmonyMethod(appliedPost));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] BionicsApplyPatch: OnAugmentClicked + OnAugmentApplied Postfixes applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsApplyPatch apply failed: " + e);
            }
        }

        // Preview: slotClicked.Mutation is the bodypart ItemDef.
        private static void OnAugmentClicked_Postfix(UIModuleBionics __instance, UIModuleMutationsSlot slotClicked)
        {
            try
            {
                if (slotClicked == null || !Phase4.IsPhase4Recruit(__instance.CurrentCharacter))
                {
                    return;
                }
                EnforceAndRefresh(__instance, slotClicked.Mutation, patchCurrentItems: true);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsApplyPatch OnAugmentClicked_Postfix threw: " + e);
            }
        }

        // Commit: augment is the bodypart ItemDef that was applied.
        private static void OnAugmentApplied_Postfix(UIModuleBionics __instance, ItemDef augment)
        {
            try
            {
                if (augment == null || !Phase4.IsPhase4Recruit(__instance.CurrentCharacter))
                {
                    return;
                }
                EnforceAndRefresh(__instance, augment, patchCurrentItems: false);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsApplyPatch OnAugmentApplied_Postfix threw: " + e);
            }
        }

        private static void EnforceAndRefresh(UIModuleBionics module, ItemDef bodypart, bool patchCurrentItems)
        {
            GeoCharacter ch = module.CurrentCharacter;

            // BUG-B diagnostics: dump the live armour list so a runtime pass shows exactly what the native
            // swap left and what (if anything) we add. Cheap; one line per apply.
            TheTurnedMain.LogInfo($"[TheTurned] augment apply DIAG: card='{bodypart.name}' "
                + $"armour-before-enforce=[{ArmourNames(ch)}]");

            List<GeoItem> enforced = ArthronArms.EnforceSetForBodypart(ch, bodypart);
            if (enforced == null)
            {
                // Native swap + the bodypart's own SubAddons handle this card (native arm/shield/base-head).
                // Still refresh the model so the hand weapon renders.
                RefreshModel(module, ch);
                TheTurnedMain.LogInfo($"[TheTurned] augment apply: '{bodypart.name}' handled by native+SubAddon path "
                    + $"(no flat-hand add). armour=[{ArmourNames(ch)}]");
                return;
            }
            ch.SetItems(armour: enforced);

            // Keep the screen's preview buffer in sync so the COMMIT persists the hand alongside the bodypart.
            if (patchCurrentItems && module.CharacterCurrentItems != null)
            {
                module.CharacterCurrentItems.Clear();
                module.CharacterCurrentItems.AddRange(ch.ArmourItems);
            }

            RefreshModel(module, ch);
            TheTurnedMain.LogInfo($"[TheTurned] augment apply: enforced authored hand for '{bodypart.name}' "
                + $"on '{ch.GetName()}'. armour=[{ArmourNames(ch)}]");
        }

        private static string ArmourNames(GeoCharacter ch)
        {
            if (ch?.ArmourItems == null)
            {
                return "<null>";
            }
            return string.Join(", ", ch.ArmourItems.Select(i => i?.ItemDef?.name).Where(n => n != null));
        }

        /// <summary>Re-render the live 3D model via the screen's own actor-cycle module (private field).</summary>
        private static void RefreshModel(UIModuleBionics module, GeoCharacter ch)
        {
            try
            {
                var actorCycle = ActorCycleField?.GetValue(module) as UIModuleActorCycle;
                actorCycle?.DisplaySoldier(ch, resetAnimation: false, addWeapon: true);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsApplyPatch: model refresh failed: " + e.Message);
            }
        }
    }
}
