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
    /// The native screen has EXACTLY three sections (Human Head/Torso/Legs), keyed by
    /// <c>section.SlotForMutation</c> [G UIModuleMutationSection.cs:68] inside
    /// <c>UIModuleBionics.InitPossibleMutations</c> [G :328-360]. That method re-keys the sections and
    /// re-populates each section's <c>PossibleMutations</c> from every Bionical-tagged ItemDef whose
    /// RequiredSlotBind matches the section's slot. We PREFIX it so, for the recruit, the three sections are
    /// retargeted to the Crabman augment slots BEFORE population runs (Option A — repurpose, do NOT inject:
    /// the section container is non-scrollable / capped at 3). The section's <c>SlotForMutation</c> field is
    /// shared prefab state, so we SAVE each original and RESTORE it whenever a non-recruit opens, keeping
    /// humans untouched. We also add every variant to the faction's <c>UnlockedAugmentations</c> so none are
    /// locked, and (Postfix) log the card count per section.
    /// </summary>
    internal static class BionicsSectionPatch
    {
        internal const string PatchId = "Morgott.TheTurned.BionicsSection";
        private static bool _applied;

        private static readonly FieldInfo ActorCycleField =
            AccessTools.Field(typeof(UIModuleBionics), "_actorCycleModule");

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
                MethodInfo target = AccessTools.Method(typeof(UIModuleBionics), "InitPossibleMutations");
                MethodInfo prefix = AccessTools.Method(typeof(BionicsSectionPatch), nameof(InitPossibleMutations_Prefix));
                MethodInfo postfix = AccessTools.Method(typeof(BionicsSectionPatch), nameof(InitPossibleMutations_Postfix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix), postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] BionicsSectionPatch: UIModuleBionics.InitPossibleMutations Prefix+Postfix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch apply failed: " + e);
            }
        }

        private static GeoCharacter CurrentCharacter(UIModuleBionics module)
        {
            try
            {
                var actorCycle = ActorCycleField?.GetValue(module) as UIModuleActorCycle;
                return actorCycle != null ? actorCycle.CurrentCharacter : null;
            }
            catch
            {
                return null;
            }
        }

        private static void InitPossibleMutations_Prefix(UIModuleBionics __instance)
        {
            try
            {
                UIModuleMutationSection[] sections = __instance.GetComponentsInChildren<UIModuleMutationSection>(true);
                bool isRecruit = Phase4.IsPhase4Recruit(CurrentCharacter(__instance));

                if (!isRecruit)
                {
                    RestoreNativeSlots(sections);
                    return;
                }
                if (!AugmentVariants.Ready)
                {
                    TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch: recruit screen opened but AugmentVariants not ready — sections NOT retargeted.");
                    return;
                }

                int retargeted = 0;
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
                    if (crab != null)
                    {
                        section.SlotForMutation = crab;
                        retargeted++;
                        TheTurnedMain.LogInfo($"[TheTurned] augment section '{section.name}' retargeted "
                            + $"'{nativeSlot?.name}'({nativeSlot?.SlotName}) -> '{crab.name}'({crab.SlotName}).");
                    }
                }

                UnlockVariants(__instance);
                TheTurnedMain.LogInfo($"[TheTurned] BionicsSectionPatch: {retargeted}/{sections.Length} sections retargeted for recruit.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSectionPatch Prefix threw: " + e);
            }
        }

        private static void InitPossibleMutations_Postfix(UIModuleBionics __instance)
        {
            try
            {
                if (!Phase4.IsPhase4Recruit(CurrentCharacter(__instance)))
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

        /// <summary>Add every Crabman variant bodypart to the faction's UnlockedAugmentations so no card is locked.</summary>
        private static void UnlockVariants(UIModuleBionics module)
        {
            var faction = module.Context?.ViewerFaction;
            if (faction?.UnlockedAugmentations == null)
            {
                return;
            }
            int added = 0;
            foreach (TacticalItemDef bp in AugmentVariants.AllVariantBodyparts)
            {
                if (faction.UnlockedAugmentations.Add(bp))
                {
                    added++;
                }
            }
            if (added > 0)
            {
                TheTurnedMain.LogInfo($"[TheTurned] BionicsSectionPatch: unlocked {added} Crabman variants for the faction.");
            }
        }

        /// <summary>Restore any section we previously retargeted back to its native human slot (humans untouched).</summary>
        private static void RestoreNativeSlots(UIModuleMutationSection[] sections)
        {
            if (_originalSlot.Count == 0)
            {
                return;
            }
            foreach (UIModuleMutationSection section in sections)
            {
                if (section != null && _originalSlot.TryGetValue(section, out ItemSlotDef native) && native != null
                    && section.SlotForMutation != native)
                {
                    section.SlotForMutation = native;
                }
            }
        }
    }
}
