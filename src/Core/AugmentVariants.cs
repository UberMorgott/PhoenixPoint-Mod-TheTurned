using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2 build-time prep for the Arthron augment screen. Makes every Crabman head / left-arm /
    /// right-arm variant <c>BodyPart</c> def eligible to appear as a CARD in the vanilla Bionics screen:
    ///   1. Tag it with <c>SharedGameTags.BionicalTag</c> (the <c>UIModuleBionics.InitPossibleMutations</c>
    ///      filter requires this — [G UIModuleBionics.cs:341-343]).
    ///   2. Zero its manufacture cost (V1 = FREE — [G UIModuleBionics.cs:254 Wallet.Take], [G
    ///      UIModuleMutationSection.cs:459 mutagen affordability]).
    /// Tagging is SAFE for non-recruit humans: a variant only becomes a card if its RequiredSlotBind matches
    /// a section's SlotForMutation — on a human screen the sections key to Human slots, not the Crabman slots
    /// our parts bind to, so they never populate a human section ([G UIModuleBionics.cs:349 ContainsKey]).
    ///
    /// Also resolves + caches the three Crabman augment <see cref="ItemSlotDef"/>s the sections retarget to
    /// (Head / LeftArm / RightArm) — runtime-dumped names, resolved by name (bundle-only GUIDs).
    /// </summary>
    internal static class AugmentVariants
    {
        // Runtime-dumped slot def names (verified in-game 2026-06-10, build 2369149).
        internal const string HeadSlotName = "Crabman_Head_SlotDef";
        internal const string LeftArmSlotName = "Crabman_LeftArm_SlotDef";
        internal const string RightArmSlotName = "Crabman_RightArm_SlotDef";

        private static bool _prepared;
        private static GameTagDef _bionicalTag;
        private static FieldInfo _manufacturePriceField;

        internal static ItemSlotDef HeadSlot { get; private set; }
        internal static ItemSlotDef LeftArmSlot { get; private set; }
        internal static ItemSlotDef RightArmSlot { get; private set; }

        /// <summary>All variant bodypart defs (head + both arms), deduped — the card pool + the apply catalog.</summary>
        internal static IEnumerable<TacticalItemDef> AllVariantBodyparts =>
            CrabmanParts.HeadSets.Concat(CrabmanParts.LeftArmSets).Concat(CrabmanParts.RightArmSets)
                .Select(s => s.BodyPart).Where(b => b != null).Distinct();

        internal static bool Ready => _prepared && HeadSlot != null && LeftArmSlot != null && RightArmSlot != null;

        /// <summary>
        /// Idempotent. Tags every variant bodypart Bionical + zeroes its cost, and resolves the 3 Crabman
        /// augment slot defs. Call from <c>BuildAllClasses</c> AFTER <see cref="CrabmanParts.Build"/>.
        /// </summary>
        internal static void Prepare(DefRepository repo)
        {
            if (_prepared || repo == null || !CrabmanParts.HasSets)
            {
                return;
            }

            _bionicalTag = GameUtl.GameComponent<SharedData>()?.SharedGameTags?.BionicalTag;
            if (_bionicalTag == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentVariants: BionicalTag null — variants cannot become cards.");
                return;
            }

            HeadSlot = DefUtils.ResolveByName<ItemSlotDef>(repo, HeadSlotName);
            LeftArmSlot = DefUtils.ResolveByName<ItemSlotDef>(repo, LeftArmSlotName);
            RightArmSlot = DefUtils.ResolveByName<ItemSlotDef>(repo, RightArmSlotName);
            if (HeadSlot == null || LeftArmSlot == null || RightArmSlot == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] AugmentVariants: Crabman slot def(s) missing "
                    + $"(Head={HeadSlot != null} LeftArm={LeftArmSlot != null} RightArm={RightArmSlot != null}) "
                    + "— augment sections cannot retarget.");
                return;
            }

            int tagged = 0, missingVed = 0;
            foreach (TacticalItemDef bp in AllVariantBodyparts)
            {
                if (bp.Tags == null)
                {
                    continue;
                }
                if (!bp.Tags.Contains(_bionicalTag))
                {
                    bp.Tags.Add(_bionicalTag);
                    tagged++;
                }
                if (bp.ViewElementDef == null)
                {
                    missingVed++;
                    TheTurnedMain.LogWarn($"[TheTurned] AugmentVariants: '{bp.name}' has NULL ViewElementDef — it will NOT render as a card.");
                }
                ZeroCost(bp);
            }

            _prepared = true;
            TheTurnedMain.LogInfo($"[TheTurned] AugmentVariants prepared: {AllVariantBodyparts.Count()} variant bodyparts "
                + $"({tagged} newly Bionical-tagged, {missingVed} missing VED), slots Head='{HeadSlot.name}' "
                + $"LeftArm='{LeftArmSlot.name}' RightArm='{RightArmSlot.name}'.");
        }

        /// <summary>Zero the manufacture-cost fields + invalidate the cached <c>_manufacturePrice</c> so V1 swaps are FREE.</summary>
        private static void ZeroCost(ItemDef def)
        {
            def.ManufactureTech = 0f;
            def.ManufactureMaterials = 0f;
            def.ManufactureMutagen = 0f;
            def.ManufactureLivingCrystals = 0f;
            def.ManufactureOricalcum = 0f;
            def.ManufactureProteanMutane = 0f;
            try
            {
                if (_manufacturePriceField == null)
                {
                    _manufacturePriceField = typeof(ItemDef).GetField("_manufacturePrice",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                }
                _manufacturePriceField?.SetValue(def, null); // force lazy recompute -> zero ResourcePack
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn($"[TheTurned] AugmentVariants: could not reset _manufacturePrice on '{def.name}': {e.Message}");
            }
        }

        /// <summary>Map a section's CURRENT (native) human slot name to the Crabman slot it should retarget to.</summary>
        internal static ItemSlotDef MapHumanSlotToCrabman(string humanSlotName)
        {
            if (string.IsNullOrEmpty(humanSlotName))
            {
                return null;
            }
            if (humanSlotName.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0) return HeadSlot;
            if (humanSlotName.IndexOf("Torso", StringComparison.OrdinalIgnoreCase) >= 0
                || humanSlotName.IndexOf("Body", StringComparison.OrdinalIgnoreCase) >= 0) return LeftArmSlot;
            if (humanSlotName.IndexOf("Legs", StringComparison.OrdinalIgnoreCase) >= 0
                || humanSlotName.IndexOf("Leg", StringComparison.OrdinalIgnoreCase) >= 0) return RightArmSlot;
            return null;
        }
    }
}
