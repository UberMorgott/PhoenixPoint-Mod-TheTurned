using Base.Defs;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Arms": each cell is a passive MARKER ability bound (via
    /// <see cref="Phase4Markers"/>) to a matched arm SET discovered at runtime by
    /// <see cref="CrabmanParts"/>. Buying a cell learns the marker; ApplyChosenSets re-derives the
    /// equipped bodypart+hand pair from the learned markers. Unresolved sets (bundle defs missing)
    /// skip their cell with a warn — SpecRowFactory.PadRow fills the hole with cheap fillers.
    /// </summary>
    internal static class ArthronArmsRow
    {
        /// <summary>Design cells in row order. Safe with empty CrabmanParts (all cells skipped).</summary>
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();
            AddSetCell(repo, cells, FindRight("Gun", exclude: "Viral"), "GUN", "ARTHRON_ARM_GUN",
                "Arthron_ArmRight.png", 20,
                new[] { PerkFactory.Add(StatModificationTarget.BonusAttackRange, 5f) });
            AddSetCell(repo, cells, FindLeft("Grenade", exclude: "Acid|Elite"), "GRENADE", "ARTHRON_ARM_GRENADE",
                "Arthron_ArmLeft.png", 20, null);
            AddSetCell(repo, cells, FindRight("Viral_Gun"), "VIRALGUN", "ARTHRON_ARM_VIRALGUN",
                "Arthron_ArmRight.png", 25, null);
            AddSetCell(repo, cells, FindLeft("EliteGrenade", exclude: "Acid"), "ELITEGRENADE", "ARTHRON_ARM_ELITEGRENADE",
                "Arthron_ArmLeft.png", 25, null);
            AddSetCell(repo, cells, FindLeft("Acid_Grenade"), "ACIDGRENADE", "ARTHRON_ARM_ACIDGRENADE",
                "Arthron_ArmLeft.png", 25, null);
            return cells.ToArray();
        }

        /// <summary>Build one marker cell; <paramref name="set"/> == null → warn + skip (PadRow fills).</summary>
        private static void AddSetCell(DefRepository repo, List<AbilityTrackSlot> cells, MatchedSet set,
            string key, string baseLocKey, string icon, int mutagenCost, ItemStatModification[] extraStats)
        {
            if (set == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] arms row: SET '{key}' unresolved — cell skipped");
                return;
            }
            Phase4RowCells.AddMarkerCell(repo, cells, "armset:" + key,
                $"TheTurned_Arthron_ArmSet_{key}_AbilityDef", baseLocKey, icon, mutagenCost,
                extraStats, m => Phase4Markers.RegisterArmSet(m, set));
        }

        private static MatchedSet FindRight(string token, string exclude = null)
            => Find(CrabmanParts.RightArmSets, token, exclude);

        private static MatchedSet FindLeft(string token, string exclude = null)
            => Find(CrabmanParts.LeftArmSets, token, exclude);

        // Substring match against runtime-discovered variant tokens (vocabulary visible in the
        // CrabmanParts log dump); exclude is a pipe-separated OR-list of forbidden substrings.
        private static MatchedSet Find(IEnumerable<MatchedSet> sets, string token, string exclude)
            => sets.FirstOrDefault(s => s.Token.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0
                && (exclude == null || !exclude.Split('|').Any(x => s.Token.IndexOf(x, StringComparison.OrdinalIgnoreCase) >= 0)));
    }
}
