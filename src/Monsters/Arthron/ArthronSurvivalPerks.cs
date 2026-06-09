using Base.Defs;
using Base.Entities.Statuses;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Statuses;
using System;
using System.Collections.Generic;
using System.Linq;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Survival": cells 1-3 are OWN <see cref="StatusImmunityAbilityDef"/>s wired to
    /// runtime-resolved StatusDefs (panic / poison / mind control); cell 4 borrows TFTV's mutoid daze
    /// immunity; cell 5 borrows the engine's fire-immunity ability (the EXACT def instance
    /// FireStatus.OnApply checks — see <see cref="AddFireCell"/>); cell 6 is a pure marker read by
    /// <see cref="LimbRestoreHook"/> (post-mission limb auto-restore). Any cell failing to resolve is
    /// warned + skipped (SpecRowFactory.PadRow fills).
    ///
    /// Verified status-def instance names (TFTV real source — runtime-checked DefCache.GetDef calls):
    ///  - "Panic_StatusDef"                 VariousAdjustmentsMain.cs:105
    ///  - "Poison_DamageOverTimeStatusDef"  VariousAdjustments.cs:194 (+ ArtOfCrab, FactionPerks, Drills)
    ///  - "MindControl_StatusDef"           BerserkerSkills.cs:115, TFTVNJQuestline.cs:691
    /// Later candidates per cell are fallback hypotheses only; a wholesale miss dumps every StatusDef
    /// containing the cell's token (once) so the real name lands in Player.log.
    /// </summary>
    internal static class ArthronSurvivalPerks
    {
        /// <summary>Capstone marker (cell 6) read by <see cref="LimbRestoreHook"/>; null until
        /// BuildRowCells runs (or when the marker build failed).</summary>
        internal static TacticalAbilityDef LimbRestoreMarker;

        /// <summary>Tokens already dumped to the log on a wholesale candidate miss (once per session).</summary>
        private static readonly HashSet<string> _dumpedTokens = new HashSet<string>();

        /// <summary>Design cells in row order. Cells degrade individually (no row-level gate needed:
        /// StatusDefs are def-repo global, not bundle-timing-sensitive like the Crabman item defs).</summary>
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();
            // Cells 1-3 — own StatusImmunityAbilityDefs on runtime-resolved statuses.
            AddImmunityCell(repo, cells, "PANIC", "Panic",
                new[] { "Panic_StatusDef", "Panicked_StatusDef" },
                20, "ARTHRON_SURVIVAL_PANIC", "ArthronSurvival_Panic.png");
            AddImmunityCell(repo, cells, "POISON", "Poison",
                new[] { "Poison_DamageOverTimeStatusDef", "Poisoned_DamageOverTimeStatusDef" },
                20, "ARTHRON_SURVIVAL_POISON", "ArthronSurvival_Poison.png");
            AddImmunityCell(repo, cells, "MC", "MindControl",
                new[] { "MindControl_StatusDef", "MindControlled_StatusDef" },
                30, "ARTHRON_SURVIVAL_MC", "ArthronSurvival_MC.png");
            // Cell 4 — borrow TFTV's mutoid daze immunity (ApplyStatusAbilityDef; a clone applies the
            // same immunity status, so cloning stays functional — unlike fire below).
            AddBorrowedDazeCell(repo, cells, 25);
            // Cell 5 — the engine fire-immunity ability (original instance only — see comment inside).
            AddFireCell(repo, cells, 30);
            // Cell 6 — pure capstone marker, no registry payload; LimbRestoreHook reads it by Guid.
            LimbRestoreMarker = Phase4RowCells.AddMarkerCell(repo, cells, "survival:LIMBS",
                "TheTurned_Arthron_Survival_LIMBS_AbilityDef", "ARTHRON_SURVIVAL_LIMBS",
                "ArthronSurvival_Regen.png", 30, extraStats: null, register: null);
            return cells.ToArray();
        }

        /// <summary>Get-or-create an own StatusImmunityAbilityDef for one resolved status; resolution or
        /// create failure → warn + skip (PadRow fills).</summary>
        private static void AddImmunityCell(DefRepository repo, List<AbilityTrackSlot> cells, string key,
            string dumpToken, string[] statusCandidates, int mutagenCost, string baseLocKey, string icon)
        {
            StatusDef status = ResolveStatus(repo, key, dumpToken, statusCandidates);
            if (status == null)
            {
                return; // ResolveStatus already warned (+ dumped token matches once)
            }
            string guid = Phase4.DeriveGuid("survival:" + key).ToString();
            var ability = repo.GetDef(guid) as StatusImmunityAbilityDef;
            if (ability == null)
            {
                // Clone any existing StatusImmunityAbilityDef as scaffolding (vanilla ships several,
                // e.g. StunStatusImmunity_AbilityDef); blank CreateDef as last resort.
                StatusImmunityAbilityDef template = DefUtils.AnyTemplate<StatusImmunityAbilityDef>(repo);
                ability = template != null
                    ? repo.CreateDef<StatusImmunityAbilityDef>(guid, template)
                    : repo.CreateDef<StatusImmunityAbilityDef>(guid);
                if (ability == null)
                {
                    TheTurnedMain.LogWarn($"[TheTurned] survival row: immunity ability create failed for '{key}' — cell skipped");
                    return;
                }
                ability.name = $"TheTurned_Arthron_Survival_{key}_AbilityDef";
            }
            ability.StatusDef = status;
            // Same invariant the FIRE cell enforces: the popup NREs on null prog data (cost read).
            var prog = PerkFactory.BuildProgression(repo,
                Phase4.DeriveGuid("survival:" + key + "|prog").ToString(),
                ability.name, mutagenCost, mutagenCost);
            if (prog == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] survival row: prog data build failed for '{key}' — cell skipped");
                return;
            }
            ability.CharacterProgressionData = prog;
            ability.ViewElementDef = PerkFactory.BuildVed(repo,
                Phase4.DeriveGuid("survival:" + key + "|ved").ToString(),
                ability.name, baseLocKey + "_NAME", baseLocKey + "_DESC", icon);
            cells.Add(new AbilityTrackSlot { Ability = ability, RequiresPrevAbility = false });
        }

        /// <summary>First candidate hit wins; wholesale miss → warn + one-shot dump of every StatusDef
        /// containing <paramref name="dumpToken"/> (the designed real-name discovery mechanism).</summary>
        private static StatusDef ResolveStatus(DefRepository repo, string key, string dumpToken, string[] candidates)
        {
            List<StatusDef> all = repo.GetAllDefs<StatusDef>().Where(d => d != null).ToList();
            foreach (string name in candidates)
            {
                StatusDef hit = all.FirstOrDefault(d => d.name == name);
                if (hit != null)
                {
                    return hit;
                }
            }
            TheTurnedMain.LogWarn($"[TheTurned] survival row: no StatusDef candidate hit for '{key}' " +
                $"(tried: {string.Join(", ", candidates)}) — cell skipped");
            if (_dumpedTokens.Add(dumpToken))
            {
                foreach (StatusDef d in all.Where(d => d.name.IndexOf(dumpToken, StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    TheTurnedMain.LogInfo($"[TheTurned]   StatusDef containing '{dumpToken}': '{d.name}'");
                }
            }
            return null;
        }

        /// <summary>
        /// Cell 4 — borrow TFTV's <c>MutoidDazeImmunity_AbilityDef</c> (resolved by name; it is an
        /// ApplyStatusAbilityDef per TFTV's FixMutoidDazeImmunity). The def is SHARED with real mutoids:
        /// when its prog data already carries our designed cost we use the original untouched; otherwise
        /// we never mutate it — we CLONE (non-generic CreateDef keeps the runtime def type, and the clone
        /// applies the same immunity status, so it stays functional) and give the clone its own prog
        /// data. The clone keeps the borrowed VED (native "Daze Immunity" name/icon) so the cell renders
        /// identically on both paths.
        /// </summary>
        private static void AddBorrowedDazeCell(DefRepository repo, List<AbilityTrackSlot> cells, int mutagenCost)
        {
            TacticalAbilityDef borrowed = DefUtils.ResolveByName<TacticalAbilityDef>(repo, "MutoidDazeImmunity_AbilityDef");
            if (borrowed == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] survival row: 'MutoidDazeImmunity_AbilityDef' not found — DAZE cell skipped");
                return;
            }
            if (borrowed.CharacterProgressionData != null
                && borrowed.CharacterProgressionData.MutagenCost == mutagenCost)
            {
                cells.Add(new AbilityTrackSlot { Ability = borrowed, RequiresPrevAbility = false });
                return;
            }
            string guid = Phase4.DeriveGuid("survival:DAZE").ToString();
            var clone = repo.GetDef(guid) as TacticalAbilityDef
                ?? repo.CreateDef(guid, borrowed) as TacticalAbilityDef;
            if (clone == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] survival row: daze immunity clone failed — DAZE cell skipped");
                return;
            }
            clone.name = "TheTurned_Arthron_Survival_DAZE_AbilityDef";
            // Same invariant the FIRE cell enforces: the popup NREs on null prog data (cost read).
            var prog = PerkFactory.BuildProgression(repo,
                Phase4.DeriveGuid("survival:DAZE|prog").ToString(),
                clone.name, mutagenCost, mutagenCost);
            if (prog == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] survival row: prog data build failed for 'DAZE' — cell skipped");
                return;
            }
            clone.CharacterProgressionData = prog;
            cells.Add(new AbilityTrackSlot { Ability = clone, RequiresPrevAbility = false });
        }

        /// <summary>
        /// Cell 5 — fire immunity. DECISION: FireStatus.OnApply self-unapplies only when the actor has
        /// the EXACT ability instance referenced by FireStatusDef.FireImmunityAbilityDef
        /// (FireStatus.cs:50) — a clone would render in the popup but grant NOTHING. Therefore the
        /// ORIGINAL goes in the cell, never a clone:
        ///  - prog data null → attach our own (ADDITIVE: actors holding the ability never read it, only
        ///    the level-up popup does, so real mutoids/pandorans are unaffected);
        ///  - prog data present with a different MutagenCost → use as-is and log the displayed cost
        ///    (shared def — mutating the cost would leak into every other holder's UI).
        /// </summary>
        private static void AddFireCell(DefRepository repo, List<AbilityTrackSlot> cells, int mutagenCost)
        {
            FireStatusDef fireStatus = repo.GetAllDefs<FireStatusDef>()
                .FirstOrDefault(d => d != null && d.FireImmunityAbilityDef != null);
            TacticalAbilityDef fireImmunity = fireStatus?.FireImmunityAbilityDef;
            if (fireImmunity == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] survival row: no FireStatusDef with FireImmunityAbilityDef — FIRE cell skipped");
                return;
            }
            if (fireImmunity.CharacterProgressionData == null)
            {
                fireImmunity.CharacterProgressionData = PerkFactory.BuildProgression(repo,
                    Phase4.DeriveGuid("survival:FIRE|prog").ToString(),
                    fireImmunity.name, mutagenCost, mutagenCost);
                if (fireImmunity.CharacterProgressionData == null)
                {
                    // Popup null-derefs a null prog data (cost read) — never ship the cell without one.
                    TheTurnedMain.LogWarn("[TheTurned] survival row: fire immunity prog data build failed — FIRE cell skipped");
                    return;
                }
            }
            else if (fireImmunity.CharacterProgressionData.MutagenCost != mutagenCost)
            {
                TheTurnedMain.LogInfo("[TheTurned] survival row: fire immunity displayed cost " +
                    $"{fireImmunity.CharacterProgressionData.MutagenCost} != designed {mutagenCost} — accepted (shared def, exact-instance check forbids cloning)");
            }
            cells.Add(new AbilityTrackSlot { Ability = fireImmunity, RequiresPrevAbility = false });
        }
    }
}
