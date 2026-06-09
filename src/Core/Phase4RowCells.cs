using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>
    /// Shared tail for Phase-4 popup row MARKER cells (Arms / Claw / Head / Survival rows): build the
    /// passive via <see cref="PerkFactory.BuildStatPassive"/>, register an optional payload, append the
    /// track slot. One responsibility — the per-row precondition checks (set/clone resolution) stay in
    /// the row builders.
    /// </summary>
    internal static class Phase4RowCells
    {
        /// <summary>
        /// Build one marker cell idempotently (seeds <paramref name="guidSeed"/> / +"|prog" / +"|ved",
        /// sp == mutagen, loc keys <paramref name="baseLocKey"/>+"_NAME"/"_DESC") and append it to
        /// <paramref name="cells"/>. Build failure → warn + return null (cell skipped, PadRow fills).
        /// <paramref name="register"/> may be null for pure markers without a registry payload.
        /// Returns the built (or pre-existing) marker def.
        /// </summary>
        internal static TacticalAbilityDef AddMarkerCell(DefRepository repo, List<AbilityTrackSlot> cells,
            string guidSeed, string abilityName, string baseLocKey, string icon, int mutagenCost,
            ItemStatModification[] extraStats, Action<TacticalAbilityDef> register)
        {
            var marker = PerkFactory.BuildStatPassive(repo,
                Phase4.DeriveGuid(guidSeed).ToString(),
                abilityName,
                Phase4.DeriveGuid(guidSeed + "|prog").ToString(),
                Phase4.DeriveGuid(guidSeed + "|ved").ToString(),
                baseLocKey + "_NAME", baseLocKey + "_DESC", icon,
                skillPointCost: mutagenCost, mutagenCost: mutagenCost,
                extraStats);
            if (marker == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] row cell '{guidSeed}': marker build failed — cell skipped");
                return null;
            }
            // Register AFTER BuildStatPassive on purpose: same-session BuildAllClasses re-runs hit its
            // get-or-create EXISTING path, and the registry upsert must still run on every pass.
            register?.Invoke(marker);
            cells.Add(new AbilityTrackSlot { Ability = marker, RequiresPrevAbility = false });
            return marker;
        }
    }
}
