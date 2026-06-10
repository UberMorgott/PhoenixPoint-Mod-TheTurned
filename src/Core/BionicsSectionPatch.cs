using HarmonyLib;
using I2.Loc;
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
        // Per-section header Localize + its saved native term (so the human header is restored on cycle).
        private static readonly Dictionary<UIModuleMutationSection, Localize> _headerLoc =
            new Dictionary<UIModuleMutationSection, Localize>();
        private static readonly Dictionary<UIModuleMutationSection, string> _originalHeaderTerm =
            new Dictionary<UIModuleMutationSection, string>();

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
                    AugmentVariants.EnsureFree(); // re-assert FREE cost each open (defensive vs reload rebuilds)
                    retargetChanged = RetargetToCrabman(sections);
                    FixRightArmPreviewSprite(sections); // right-arm row's blank empty-slot art -> torso silhouette
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
                // Relabel the section header to our Голова / Левая рука / Правая рука (recruit only).
                SetSectionHeader(section, AugmentVariants.HeaderKeyForSlot(crab));
            }
            return changed;
        }


        /// <summary>
        /// The empty-slot preview silhouette per row = the section's own <c>PreviewSprite</c> (shown by
        /// <c>SetPreviewMutation(null)</c> → <c>PreviewImage.sprite = PreviewSprite</c>,
        /// [G UIModuleMutationSection.cs:415-422]). The RIGHT-arm row was retargeted from the native LEGS
        /// section, whose PreviewSprite is blank/white; the LEFT-arm row (retargeted from the Torso/Body
        /// section) has the torso silhouette. Copy the LEFT section's PreviewSprite onto the RIGHT section so
        /// both arm rows show the torso silhouette. Reuses the existing sprite — no new asset. Idempotent.
        /// </summary>
        private static void FixRightArmPreviewSprite(UIModuleMutationSection[] sections)
        {
            try
            {
                UIModuleMutationSection left = sections.FirstOrDefault(s =>
                    s != null && s.SlotForMutation != null && s.SlotForMutation == AugmentVariants.LeftArmSlot);
                UIModuleMutationSection right = sections.FirstOrDefault(s =>
                    s != null && s.SlotForMutation != null && s.SlotForMutation == AugmentVariants.RightArmSlot);
                if (left == null || right == null || left.PreviewSprite == null)
                {
                    return;
                }
                if (right.PreviewSprite == left.PreviewSprite)
                {
                    return; // already done
                }
                right.PreviewSprite = left.PreviewSprite;
                // If the right row is currently showing its (blank) empty-slot placeholder, refresh it live.
                if (right.PreviewImage != null && right.MutationUsed == null)
                {
                    right.PreviewImage.sprite = right.PreviewSprite;
                }
                TheTurnedMain.LogInfo("[TheTurned] augment: right-arm row empty-slot preview set to the left-arm torso silhouette.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] FixRightArmPreviewSprite threw: " + e);
            }
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
                RestoreSectionHeader(section); // put the native header term back for humans
            }
            return changed;
        }

        /// <summary>
        /// Find the section's header <see cref="Localize"/> (lazily, once) and set it to our loc key. The
        /// header is a static prefab Text/Localize child NOT among the script's preview-panel Localize fields
        /// (MutationNameLoc / MutationDescriptionLoc / MutationLockedLoc) — pick the first other Localize in
        /// the subtree. The native term is saved so humans are restored on cycle.
        /// </summary>
        private static void SetSectionHeader(UIModuleMutationSection section, string headerKey)
        {
            if (section == null || string.IsNullOrEmpty(headerKey))
            {
                return;
            }
            Localize header = ResolveHeaderLoc(section);
            if (header == null)
            {
                return;
            }
            if (!_originalHeaderTerm.ContainsKey(section))
            {
                _originalHeaderTerm[section] = header.Term; // remember the native term, once
            }
            if (header.Term != headerKey)
            {
                header.SetTerm(headerKey);
                TheTurnedMain.LogInfo($"[TheTurned] augment section '{section.name}' header -> '{headerKey}'.");
            }
        }

        private static void RestoreSectionHeader(UIModuleMutationSection section)
        {
            if (section == null || !_headerLoc.TryGetValue(section, out Localize header) || header == null)
            {
                return;
            }
            if (_originalHeaderTerm.TryGetValue(section, out string native) && !string.IsNullOrEmpty(native)
                && header.Term != native)
            {
                header.SetTerm(native);
            }
        }

        /// <summary>The header Localize for a section = the first Localize in its subtree that is NOT one of the
        /// known preview-panel Localize fields. Cached per section.</summary>
        private static Localize ResolveHeaderLoc(UIModuleMutationSection section)
        {
            if (_headerLoc.TryGetValue(section, out Localize cached))
            {
                return cached;
            }
            var exclude = new HashSet<Localize>();
            if (section.MutationNameLoc != null) exclude.Add(section.MutationNameLoc);
            if (section.MutationDescriptionLoc != null) exclude.Add(section.MutationDescriptionLoc);
            if (section.MutationLockedLoc != null) exclude.Add(section.MutationLockedLoc);
            Localize header = section.GetComponentsInChildren<Localize>(true)
                .FirstOrDefault(l => l != null && !exclude.Contains(l));
            _headerLoc[section] = header; // cache (may be null — then we just skip header relabel)
            if (header == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] BionicsSectionPatch: no header Localize found on section '{section.name}' — header not relabeled.");
            }
            return header;
        }
    }
}
