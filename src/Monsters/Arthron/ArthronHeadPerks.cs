using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Head/Spray": cell 1 is the NATIVE spitter head SET (acid spray as shipped);
    /// cells 2-3 keep the spitter bodypart but swap the head weapon for a CLONE carrying one extra
    /// status keyword (poison / goo). Markers map to MatchedSets via
    /// <see cref="Phase4Markers.RegisterHeadSet"/>; <see cref="ArthronArms.ApplyChosenSets"/> swaps
    /// bodypart+weapon together (C3). Clone names keep the Crabman_Head token per the SwapSet
    /// removal contract. A FIRE spray cell is deliberately dropped (design OPEN-2).
    /// </summary>
    internal static class ArthronHeadPerks
    {
        /// <summary>The native spitter head SET (bodypart + spitter weapon); null until bundle defs resolve.
        /// Used by both the row builder and the registration gate in ArthronMonster.BuildPhase4Rows.</summary>
        internal static MatchedSet FindSpitterSet()
            => CrabmanParts.HeadSets.FirstOrDefault(s => s.Hand != null
                && s.Hand.name.IndexOf("Spitter", StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Design cells in row order. Empty when the spitter set is unresolved (row deferred by caller).</summary>
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();
            MatchedSet spitterSet = FindSpitterSet();
            if (spitterSet == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] head row: spitter SET unresolved — row will be fillers");
                return cells.ToArray();
            }
            // Cell 1 — ACID: the native spitter set as-is.
            AddHeadCell(repo, cells, spitterSet, "ACID", 20, "ARTHRON_SPRAY_ACID", "ArthronSpray_Acid.png");
            // Cell 2 — POISON: spitter weapon clone + poison keyword.
            AddHeadCell(repo, cells, BuildVariantSet(repo, spitterSet, "POISON",
                    "Poisonous_DamageKeywordDataDef", WeaponVariants.PoisonOnHitValue),
                "POISON", 25, "ARTHRON_SPRAY_POISON", "ArthronSpray_Poison.png");
            // Cell 3 — GOO: spitter weapon clone + goo keyword (exact instance name verified in TFTV
            // real source: Goo_DamageKeywordEffectorDef, TFTVDefsInjectedOnlyOnce.cs:6841).
            AddHeadCell(repo, cells, BuildVariantSet(repo, spitterSet, "GOO",
                    "Goo_DamageKeywordEffectorDef", 5f),
                "GOO", 25, "ARTHRON_SPRAY_GOO", "ArthronSpray_Goo.png");
            return cells.ToArray();
        }

        /// <summary>Spitter set with the head weapon swapped for a keyword-carrying clone; null on clone failure.</summary>
        private static MatchedSet BuildVariantSet(DefRepository repo, MatchedSet spitterSet,
            string key, string keywordDefName, float keywordValue)
        {
            // CONTRACT (ArthronArms.SwapSet): clone name MUST keep the Crabman_Head token.
            WeaponDef clone = WeaponVariants.GetOrCreateWeaponVariant(repo, spitterSet.Hand,
                $"spray:{key}|weapon", $"TheTurned_Crabman_Head_Spitter_{key}_WeaponDef",
                keywordDefName, keywordValue);
            if (clone == null)
            {
                return null; // helper already warned
            }
            return new MatchedSet
            {
                BodyPart = spitterSet.BodyPart,
                Hand = clone,
                IsRight = false,
                Token = "Spitter_" + key
            };
        }

        /// <summary>Build one head-SET marker cell; set == null (clone failed) → warn + skip (PadRow fills).</summary>
        private static void AddHeadCell(DefRepository repo, List<AbilityTrackSlot> cells, MatchedSet set,
            string key, int mutagenCost, string baseLocKey, string icon)
        {
            if (set == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] head row: SET '{key}' unavailable — cell skipped");
                return;
            }
            Phase4RowCells.AddMarkerCell(repo, cells, "spray:" + key,
                $"TheTurned_Arthron_Spray_{key}_AbilityDef", baseLocKey, icon, mutagenCost,
                extraStats: null, register: m => Phase4Markers.RegisterHeadSet(m, set));
        }
    }
}
