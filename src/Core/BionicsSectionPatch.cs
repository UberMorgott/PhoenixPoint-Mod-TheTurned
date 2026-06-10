using HarmonyLib;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2: repurpose the vanilla Bionics screen's three <see cref="UIModuleMutationSection"/> children
    /// to show OUR Crabman Head / Left arm / Right arm variants for the marked recruit.
    ///
    /// The recruit passes <c>CheckIsHuman</c> (forced by HumanClassificationPatch), so it sits in the SAME
    /// bionics character-cycle list as humans. Arrow-cycling recruit&lt;-&gt;human mid-screen calls
    /// <c>UIStateBionics.CharacterChangedHandler</c> [G UIStateBionics.cs:113-117] -&gt;
    /// <c>UIModuleBionics.OnNewCharacter</c> [G UIModuleBionics.cs:136], which re-contexts the sections but
    /// does NOT rebuild their card lists (<c>PossibleMutations</c> is only built by the private
    /// <c>InitPossibleMutations</c> [G :328-360], called once from <c>Init</c>). So we hook
    /// <b>OnNewCharacter</b> (fires on the initial open AND on every cycle): Prefix retargets/restores the
    /// three sections' <c>SlotForMutation</c> for the new character and re-runs <c>InitPossibleMutations</c>
    /// (mirroring the <c>Init</c> sequence: populate THEN OnNewCharacter), so BOTH recruit (Crabman cards)
    /// and human (native cards) render correctly every switch. <c>SlotForMutation</c> is shared prefab state,
    /// so each native slot is saved once and restored for humans.
    ///
    /// Unlocked-state is handled WITHOUT mutating the persisted <c>faction.UnlockedAugmentations</c> set —
    /// see <see cref="BionicsUnlockBypass"/>.
    /// </summary>
    internal static class BionicsSectionPatch
    {
        internal const string PatchId = "Morgott.TheTurned.BionicsSection";
        private static bool _applied;

        private static readonly MethodInfo InitPossibleMutationsMethod =
            AccessTools.Method(typeof(UIModuleBionics), "InitPossibleMutations");

        // Per-section saved native slot (so we can restore for humans). Keyed by the section instance.
        private static readonly Dictionary<UIModuleMutationSection, ItemSlotDef> _originalSlot =
            new Dictionary<UIModuleMutationSection, ItemSlotDef>();

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo target = AccessTools.Method(typeof(UIModuleBionics), "OnNewCharacter");
                MethodInfo prefix = AccessTools.Method(typeof(BionicsSectionPatch), nameof(OnNewCharacter_Prefix));
                MethodInfo postfix = AccessTools.Method(typeof(BionicsSectionPatch), nameof(OnNewCharacter_Postfix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] BionicsSectionPatch: UIModuleBionics.OnNewCharacter Prefix+Postfix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch apply failed: " + e);
            }
        }

        // Retarget/restore the sections for the INCOMING character, then rebuild the card lists, BEFORE the
        // native OnNewCharacter body re-contexts the (now correct) sections. Mirrors Init's populate->context.
        private static void OnNewCharacter_Prefix(UIModuleBionics __instance, GeoCharacter newCharacter)
        {
            try
            {
                UIModuleMutationSection[] sections = __instance.GetComponentsInChildren<UIModuleMutationSection>(true);
                bool isRecruit = Phase4.IsPhase4Recruit(newCharacter);

                bool retargetChanged;
                if (isRecruit)
                {
                    if (!AugmentVariants.Ready)
                    {
                        TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch: recruit selected but AugmentVariants not ready — sections NOT retargeted.");
                        return;
                    }
                    retargetChanged = RetargetToCrabman(sections);
                }
                else
                {
                    retargetChanged = RestoreNativeSlots(sections);
                }

                // Rebuild PossibleMutations + cards for the new character's (correct) section slots. Only when
                // a retarget/restore actually changed a slot — otherwise the native populate from Init/prior
                // cycle is already correct (avoids redundant container rebuilds for human->human cycles).
                if (retargetChanged)
                {
                    InitPossibleMutationsMethod?.Invoke(__instance, null);
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch Prefix threw: " + e);
            }
        }

        private static void OnNewCharacter_Postfix(UIModuleBionics __instance, GeoCharacter newCharacter)
        {
            try
            {
                if (!Phase4.IsPhase4Recruit(newCharacter))
                {
                    return;
                }
                foreach (UIModuleMutationSection section in __instance.GetComponentsInChildren<UIModuleMutationSection>(true))
                {
                    if (section == null || section.PossibleMutations == null)
                    {
                        continue;
                    }
                    TheTurnedMain.LogInfo($"[TheTurned] augment section '{section.name}' "
                        + $"slot='{section.SlotForMutation?.name}' cards={section.PossibleMutations.Count} "
                        + $"[{string.Join(", ", section.PossibleMutations.Select(m => m.name))}]");
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch Postfix threw: " + e);
            }
        }

        /// <summary>Retarget the 3 sections to the Crabman augment slots (saving each native slot once). Returns true if any changed.</summary>
        private static bool RetargetToCrabman(UIModuleMutationSection[] sections)
        {
            bool changed = false;
            foreach (UIModuleMutationSection section in sections)
            {
                if (section == null)
                {
                    continue;
                }
                if (!_originalSlot.ContainsKey(section))
                {
                    _originalSlot[section] = section.SlotForMutation; // remember the native slot, once
                }
                ItemSlotDef nativeSlot = _originalSlot[section];
                ItemSlotDef crab = AugmentVariants.MapHumanSlotToCrabman(nativeSlot?.SlotName);
                if (crab != null && section.SlotForMutation != crab)
                {
                    section.SlotForMutation = crab;
                    changed = true;
                    TheTurnedMain.LogInfo($"[TheTurned] augment section '{section.name}' retargeted "
                        + $"'{nativeSlot?.name}'({nativeSlot?.SlotName}) -> '{crab.name}'({crab.SlotName}).");
                }
            }
            return changed;
        }

        /// <summary>Restore any section we previously retargeted back to its native human slot. Returns true if any changed.</summary>
        private static bool RestoreNativeSlots(UIModuleMutationSection[] sections)
        {
            if (_originalSlot.Count == 0)
            {
                return false;
            }
            bool changed = false;
            foreach (UIModuleMutationSection section in sections)
            {
                if (section != null && _originalSlot.TryGetValue(section, out ItemSlotDef native) && native != null
                    && section.SlotForMutation != native)
                {
                    section.SlotForMutation = native;
                    changed = true;
                    TheTurnedMain.LogInfo($"[TheTurned] augment section '{section.name}' restored to native '{native.name}'({native.SlotName}).");
                }
            }
            return changed;
        }
    }
}
