using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using System.Collections.Generic;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Cell": the 5-cell top track of the recruit's cell-progression. Cell order
    /// (2026-06-11 redefined ladder + weapon EVOLUTION):
    ///   1 NAV            — unlock the augment screen (buyable; reveals the DNA button). No payload.
    ///   2 ARMOR1         — armor [Armoured legs + Carapace]. cost 20.
    ///   3 LEGS_STAT      — armor [EliteAgile legs (bigger UNARMORED) + Carapace] + Alpha stat passive. cost 20.
    ///                      Higher cell index overrides cell 2's Armoured legs (intended visual dip).
    ///   4 ARMOR2_EVOLVE  — armor [EliteArmoured legs + EliteCarapace + EliteTorso] + EVOLVE left weapon. cost 25.
    ///   5 PRIME_EVOLVE   — NO armor (inherits cell 4) + Prime stat passive (stacks on Alpha) + EVOLVE all
    ///                      weapons (left arm + right arm + head spit). cost 25.
    /// Armor cells register their loadout in CellArmorMarkers (CellArmorApply re-derives, HIGHEST learned
    /// cell wins). Stat cells carry the Alpha/Prime StatModifications. Evolve cells register an
    /// <see cref="EvolveScope"/> in EvolutionMarkers; ArthronArms.ApplyChosenSets composes the elite variant
    /// of the player's CHOSEN weapon on top. Stat numbers + token map live in <see cref="ArthronEvolution"/>.
    /// </summary>
    internal static class ArthronCellRow
    {
        /// <summary>def-name of the cell-1 NAV marker ability — purchasing it (soldier-style SP buy, like cells
        /// 2-5) is the augment-unlock marker: AugmentButtonVisibilityPatch shows the DNA button only once a
        /// recruit's Progression.Abilities contains this def. Carries no armor payload (register:null).</summary>
        internal const string NavAbilityName = "TheTurned_Arthron_Cell_NAV_AbilityDef";

        // Spec §3: top row = SkillPoints + level-gate, NOT Mutagen. We still set mutagenCost==skillPointCost
        // through Phase4RowCells.AddMarkerCell (it passes the value to BOTH costs), because the popup reads
        // MutagenCost for display; the soldier-style SP/level gate is applied by M5 routing.
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();

            // Arthron DATA: declare the normal->elite weapon-token upgrades the evolve cells consume.
            // Idempotent; safe on every (re)build. Generic apply lives in ArthronArms.ApplyChosenSets.
            ArthronEvolution.RegisterTokenMap();

            // Cell 1 — NAV: a NORMAL buyable cell (lowest-level, SP cost like cells 2-5). No armor payload
            // (register:null) — purchasing it is the augment-unlock marker that reveals the DNA button.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:NAV",
                NavAbilityName, "ARTHRON_CELL_NAV",
                "Arthron_NaturalArmour.png", mutagenCost: 20, extraStats: null, register: null);

            // Cell 2 — ARMOR1: Armoured legs + Carapace back-plate. Marker registers the loadout.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:ARMOR1",
                "TheTurned_Arthron_Cell_ARMOR1_AbilityDef", "ARTHRON_CELL_ARMOR1",
                "Arthron_NaturalArmour.png", mutagenCost: 20, extraStats: null,
                register: m => CellArmorMarkers.Register(m,
                    new[] { "Crabman_Legs_Armoured_ItemDef", "Crabman_Carapace_BodyPartDef" }, order: 2));

            // Cell 3 — LEGS_STAT: bigger UNARMORED EliteAgile legs (keep the small Carapace) + Alpha stat
            // passive. The higher cell index makes its loadout override cell 2's Armoured legs (intended
            // visual dip). Marker is BOTH an armor marker (CellArmorMarkers) AND a stat passive (AlphaStats).
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:STAT_ALPHA",
                "TheTurned_Arthron_Cell_STAT_ALPHA_AbilityDef", "ARTHRON_CELL_STAT_ALPHA",
                "Arthron_ChitinPlating.png", mutagenCost: 20, extraStats: ArthronEvolution.AlphaStats(),
                register: m => CellArmorMarkers.Register(m,
                    new[] { "Crabman_Legs_EliteAgile_ItemDef", "Crabman_Carapace_BodyPartDef" }, order: 3));

            // Cell 4 — ARMOR2_EVOLVE: EliteArmoured legs + EliteTorso + EliteCarapace, AND evolve the player's
            // chosen LEFT weapon (shield/launcher) to its elite variant. Prereq cell 2.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:ARMOR2",
                "TheTurned_Arthron_Cell_ARMOR2_AbilityDef", "ARTHRON_CELL_ARMOR2",
                "Arthron_ApexCarapace.png", mutagenCost: 25, extraStats: null,
                register: m =>
                {
                    CellArmorMarkers.Register(m,
                        new[] { "Crabman_Legs_EliteArmoured_ItemDef", "Crabman_EliteTorso_BodyPartDef", "Crabman_EliteCarapace_BodyPartDef" }, order: 4);
                    EvolutionMarkers.Register(m, EvolveScope.LeftWeapon);
                });

            // Cell 5 — PRIME_EVOLVE: NO armor loadout (inherits cell 4's EliteArmoured/EliteCarapace) + Prime
            // stat passive (stacks additively on Alpha) + evolve ALL weapons (left arm + right arm + head
            // spit) to their elite variants. Prereq cell 3.
            Phase4RowCells.AddMarkerCell(repo, cells, "cell:STAT_PRIME",
                "TheTurned_Arthron_Cell_STAT_PRIME_AbilityDef", "ARTHRON_CELL_STAT_PRIME",
                "Arthron_HardenedHide.png", mutagenCost: 25, extraStats: ArthronEvolution.PrimeStats(),
                register: m => EvolutionMarkers.Register(m, EvolveScope.AllWeapons));

            return cells.ToArray();
        }
    }
}
