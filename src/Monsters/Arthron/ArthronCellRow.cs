using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using System.Collections.Generic;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Cell": the 5-cell top track of the recruit's cell-progression (spec
    /// 2026-06-10-cell-progression-design.md). Cell order:
    ///   1 Mutations (nav -> Bionics, free)       [M4]
    ///   2 First armor (Armoured legs + Carapace)  [M2]
    ///   3 Stats Basic->Alpha (cumulative passive) [M3]
    ///   4 Max armor (EliteArmoured+EliteTorso+EliteCarapace) [M2]  (prereq cell 2)
    ///   5 Stats Alpha->Prime (cumulative passive) [M3]  (prereq cell 3)
    /// Each armor cell is a marker registered in CellArmorMarkers; CellArmorApply re-derives the loadout.
    /// </summary>
    internal static class ArthronCellRow
    {
        // Spec §3: top row = SkillPoints + level-gate, NOT Mutagen. We still set mutagenCost==skillPointCost
        // through Phase4RowCells.AddMarkerCell (it passes the value to BOTH costs), because the popup reads
        // MutagenCost for display; the soldier-style SP/level gate is applied by M5 routing.
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();

            // Cell 1 — NAV placeholder (free marker; M4 rebinds the click to GoToBionicsScreen).
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:NAV",
                "TheTurned_Arthron_Cell_NAV_AbilityDef", "ARTHRON_CELL_NAV",
                "Arthron_NaturalArmour.png", mutagenCost: 0, extraStats: null, register: null);

            // Cell 2 — FIRST ARMOR: Armoured legs + Carapace back-plate. Marker registers the loadout.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:ARMOR1",
                "TheTurned_Arthron_Cell_ARMOR1_AbilityDef", "ARTHRON_CELL_ARMOR1",
                "Arthron_NaturalArmour.png", mutagenCost: 20, extraStats: null,
                register: m => CellArmorMarkers.Register(m,
                    new[] { "Crabman_Legs_Armoured_ItemDef", "Crabman_Carapace_BodyPartDef" }));

            // Cell 3 — STAT Basic->Alpha PLACEHOLDER (M3 authors the real passive). Cheap marker for now.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:STAT_ALPHA",
                "TheTurned_Arthron_Cell_STAT_ALPHA_AbilityDef", "ARTHRON_CELL_STAT_ALPHA",
                "Arthron_ChitinPlating.png", mutagenCost: 20, extraStats: null, register: null);

            // Cell 4 — MAX ARMOR: EliteArmoured legs + EliteTorso + EliteCarapace. Prereq cell 2 (M6).
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:ARMOR2",
                "TheTurned_Arthron_Cell_ARMOR2_AbilityDef", "ARTHRON_CELL_ARMOR2",
                "Arthron_ApexCarapace.png", mutagenCost: 25, extraStats: null,
                register: m => CellArmorMarkers.Register(m,
                    new[] { "Crabman_Legs_EliteArmoured_ItemDef", "Crabman_EliteTorso_BodyPartDef", "Crabman_EliteCarapace_BodyPartDef" }));

            // Cell 5 — STAT Alpha->Prime PLACEHOLDER (M3 authors the real passive). Prereq cell 3 (M6).
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:STAT_PRIME",
                "TheTurned_Arthron_Cell_STAT_PRIME_AbilityDef", "ARTHRON_CELL_STAT_PRIME",
                "Arthron_HardenedHide.png", mutagenCost: 25, extraStats: null, register: null);

            return cells.ToArray();
        }
    }
}
