using Base.Core;
using Base.Defs;
using Base.UI;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.UI;
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
    ///   3. Rebind its ViewElementDef DisplayName/Description to mod loc keys (the bundle VEDs resolve to
    ///      "NEEDS TEXT" — no shipped term), so cards read proper names. Keys derive from the variant token.
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

        // Section-header loc keys (set per retargeted section for the recruit; see BionicsSectionPatch).
        internal const string HeadHeaderKey = "TURNED_AUGMENT_HEAD";
        internal const string LeftArmHeaderKey = "TURNED_AUGMENT_LEFTARM";
        internal const string RightArmHeaderKey = "TURNED_AUGMENT_RIGHTARM";

        private static bool _prepared;
        private static GameTagDef _bionicalTag;
        private static FieldInfo _manufacturePriceField;

        internal static ItemSlotDef HeadSlot { get; private set; }
        internal static ItemSlotDef LeftArmSlot { get; private set; }
        internal static ItemSlotDef RightArmSlot { get; private set; }

        /// <summary>BASE-TIER variant bodypart defs (head + both arms), deduped — the card pool + the apply
        /// catalog. Elite/Ultra/evolution variants are excluded (deferred to the separate perk system); see
        /// <see cref="CrabmanParts.BaseTier"/>. Head pool = Humanoid(base) + Spitter + Armored (the last two
        /// are authored clone bodyparts, see CrabmanParts.BuildAuthoredHeadVariants).</summary>
        internal static IEnumerable<TacticalItemDef> AllVariantBodyparts =>
            // HEAD cards = ONLY the authored clones (TheTurned_Crabman_Head_*): Base / Spitter / Armored,
            // each with its OWN VED. This excludes the real Crabman_Head_Humanoid / EliteHumanoid / native-
            // spitter-set bodyparts from the card pool, so (a) base is listed exactly once and (b)
            // RebindNames NEVER mutates a live enemy head ViewElementDef. The real heads stay equippable
            // (recruit equips the real chassis Humanoid via ApplyNakedBase) — just not as cards.
            CrabmanParts.BaseTier(CrabmanParts.HeadSets)
                .Where(s => s.BodyPart != null
                    && s.BodyPart.name.StartsWith("TheTurned_Crabman_Head_", StringComparison.Ordinal))
                // The plain left arm (Crabman_LeftArm_BodyPartDef, empty token) is the recruit's equipped
                // DEFAULT — it must NOT appear as a selectable card. Left cards = {Shield, Grenade,
                // Acid_Grenade}. Exclude the bare arm here (it stays equippable, just not a card).
                .Concat(CrabmanParts.BaseTier(CrabmanParts.LeftArmSets)
                    .Where(s => !string.IsNullOrEmpty(s.Token)))
                .Concat(CrabmanParts.BaseTier(CrabmanParts.RightArmSets))
                .Select(s => s.BodyPart).Where(b => b != null).Distinct();

        internal static bool Ready => _prepared && HeadSlot != null && LeftArmSlot != null && RightArmSlot != null;

        /// <summary>
        /// Idempotent. Tags every variant bodypart Bionical, zeroes its cost, rebinds its VED name/desc, and
        /// resolves the 3 Crabman augment slot defs. Call from <c>BuildAllClasses</c> AFTER
        /// <see cref="CrabmanParts.Build"/>.
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
            foreach (TacticalItemDef bp in AllVariantBodyparts) // deduped: one rebind per unique card def
            {
                if (bp?.Tags == null)
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
                else
                {
                    RebindNames(bp);
                }
                ZeroCost(bp);
            }

            _prepared = true;
            TheTurnedMain.LogInfo($"[TheTurned] AugmentVariants prepared: {AllVariantBodyparts.Count()} variant bodyparts "
                + $"({tagged} newly Bionical-tagged, {missingVed} missing VED), slots Head='{HeadSlot.name}' "
                + $"LeftArm='{LeftArmSlot.name}' RightArm='{RightArmSlot.name}'.");
            foreach (TacticalItemDef bp in AllVariantBodyparts)
            {
                TheTurnedMain.LogInfo($"[TheTurned] augment card '{bp.name}' -> locKey '{LocKeyForBodypart(bp)}'.");
            }
        }

        /// <summary>
        /// Re-assert FREE cost on every variant (defensive — call each time the recruit screen opens, since a
        /// reload/other-mod pass can rebuild a def's economy). Cheap + idempotent; logs any still-nonzero def.
        /// </summary>
        internal static void EnsureFree()
        {
            if (!Ready)
            {
                return;
            }
            foreach (TacticalItemDef bp in AllVariantBodyparts)
            {
                ZeroCost(bp);
                ResourcePack price = bp.ManufacturePrice;
                if (price != null && price.Values.Any(r => r.Value != 0f))
                {
                    TheTurnedMain.LogWarn($"[TheTurned] AugmentVariants: '{bp.name}' STILL shows non-zero cost after zeroing "
                        + $"[{string.Join(",", price.Values.Where(r => r.Value != 0f).Select(r => $"{r.Type}:{r.Value}"))}].");
                }
            }
        }

        /// <summary>Rebind the bundle VED's display name + description to a mod loc key derived from the
        /// bodypart def NAME (one stable key per card). The bundle VEDs resolve to "NEEDS TEXT".</summary>
        private static void RebindNames(TacticalItemDef bp)
        {
            ViewElementDef ved = bp.ViewElementDef;
            string nameKey = LocKeyForBodypart(bp);
            string descKey = nameKey + "_DESC";
            ved.DisplayName1 = new LocalizedTextBind(nameKey);
            ved.DisplayName2 = new LocalizedTextBind(nameKey);
            ved.Description = new LocalizedTextBind(descKey);
        }

        /// <summary>Stable loc key from the bodypart def name, e.g. Crabman_RightArm_Viral_Gun_BodyPartDef
        /// -> TURNED_AUGMENT_RIGHT_VIRAL_GUN; Crabman_Head_Humanoid_BodyPartDef -> TURNED_AUGMENT_HEAD_HUMANOID.</summary>
        internal static string LocKeyForBodypart(ItemDef bp)
        {
            string n = bp?.name ?? "";
            string side, token;
            if (n.IndexOf("RightArm", StringComparison.OrdinalIgnoreCase) >= 0)
            { side = "RIGHT"; token = CrabmanParts.VariantToken(Cut(n, "Crabman_RightArm"), "Crabman_RightArm"); }
            else if (n.IndexOf("LeftArm", StringComparison.OrdinalIgnoreCase) >= 0)
            { side = "LEFT"; token = CrabmanParts.VariantToken(Cut(n, "Crabman_LeftArm"), "Crabman_LeftArm"); }
            else if (n.IndexOf("Head", StringComparison.OrdinalIgnoreCase) >= 0)
            { side = "HEAD"; token = CrabmanParts.VariantToken(Cut(n, "Crabman_Head"), "Crabman_Head"); }
            else
            { side = "PART"; token = n; }
            token = new string((token ?? "").ToUpperInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '_').ToArray()).Trim('_');
            if (string.IsNullOrEmpty(token)) token = "BASE";
            return $"TURNED_AUGMENT_{side}_{token}";
        }

        /// <summary>Substring from the first occurrence of <paramref name="anchor"/> (so VariantToken's prefix-strip is correct).</summary>
        private static string Cut(string name, string anchor)
        {
            int at = name.IndexOf(anchor, StringComparison.OrdinalIgnoreCase);
            return at >= 0 ? name.Substring(at) : name;
        }

        /// <summary>Zero the manufacture-cost fields + invalidate the cached <c>_manufacturePrice</c> so V1 swaps are FREE.</summary>
        private static void ZeroCost(ItemDef def)
        {
            if (def == null)
            {
                return;
            }
            def.ManufactureTech = 0f;
            def.ManufactureMaterials = 0f;
            def.ManufactureMutagen = 0f;
            def.ManufactureLivingCrystals = 0f;
            def.ManufactureOricalcum = 0f;
            def.ManufactureProteanMutane = 0f;
            def.ManufacturePointsCost = 0f;
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

        /// <summary>The recruit section-header loc key for a Crabman slot (Head/LeftArm/RightArm), or null.</summary>
        internal static string HeaderKeyForSlot(ItemSlotDef slot)
        {
            if (slot == null)
            {
                return null;
            }
            if (slot == HeadSlot) return HeadHeaderKey;
            if (slot == LeftArmSlot) return LeftArmHeaderKey;
            if (slot == RightArmSlot) return RightArmHeaderKey;
            return null;
        }
    }
}
