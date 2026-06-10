using Base.Defs;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Re-derives the recruit's Torso / Carapace / Legs armor from its LEARNED cell-armor markers. Called
    /// alongside ArthronArms.ApplyChosenSets from ArmFollowHook (on subscribe + every OnAbilityAdded + every
    /// geoscape load) — the same additive, re-derive-from-learned-set persistence the arm swap uses.
    /// Cumulative-by-tier: cell 4 (Elite) wins over cell 2 (basic) because its loadout is applied if learned;
    /// when only cell 2 is learned, the basic loadout is applied. Both clear the same slots first.
    ///
    /// The loadout list-building (slot-clear-then-add, one SetItems) is shared with the M1 dev action
    /// (<see cref="CellDevDump"/>) via <see cref="BuildArmorList"/> — single source of the SetItems recipe
    /// proven in M1 (legs always; torso only when an elite torso is added; carapace always).
    /// </summary>
    internal static class CellArmorApply
    {
        internal static void ApplyLearnedArmor(GeoCharacter geoChar)
        {
            try
            {
                if (geoChar?.Progression == null || geoChar.ArmourItems == null || !CellArmorMarkers.HasAny)
                {
                    return;
                }
                // Pick the HIGHEST-tier learned loadout (last non-null wins; cells authored basic-then-elite).
                string[] chosen = null;
                foreach (TacticalAbilityDef ability in ArthronArms.EnumerateLearnedAbilities(geoChar.Progression))
                {
                    if (CellArmorMarkers.TryGet(ability, out string[] names) && names != null)
                    {
                        chosen = names; // later (elite) marker overrides earlier (basic) when both learned
                    }
                }
                if (chosen == null)
                {
                    return;
                }
                DefRepository repo = DefUtils.Repo;
                var defs = new List<TacticalItemDef>();
                foreach (string n in chosen)
                {
                    var d = DefUtils.ResolveByName<TacticalItemDef>(repo, n);
                    if (d == null) { TheTurnedMain.LogWarn($"[TheTurned] CellArmorApply: '{n}' unresolved — skipped."); }
                    else { defs.Add(d); }
                }
                if (defs.Count == 0)
                {
                    return;
                }
                // Already-applied guard: every chosen def already present -> no SetItems (avoids churn).
                var current = new List<GeoItem>(geoChar.ArmourItems);
                bool already = defs.All(d => current.Any(i => i?.ItemDef != null && i.ItemDef.Guid == d.Guid));
                if (already)
                {
                    return;
                }
                List<GeoItem> list = BuildArmorList(geoChar, defs);
                geoChar.SetItems(armour: list);
                TheTurnedMain.LogInfo($"[TheTurned] CellArmorApply for '{geoChar.GetName()}': [{string.Join(", ", defs.Select(d => d.name))}].");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellArmorApply failed: " + e);
            }
        }

        /// <summary>
        /// SHARED loadout recipe (M1-proven): clone the recruit's current armour, drop the occupants of the
        /// slots this loadout owns (legs always; torso only when an elite torso is added; carapace always),
        /// then add the chosen bodyparts. Returns the new list to commit with ONE SetItems(armour:). Mirrors
        /// the slot-clear-then-add discipline of ArthronArms.SwapSet. Mutates nothing on the character.
        /// </summary>
        internal static List<GeoItem> BuildArmorList(GeoCharacter geoChar, IList<TacticalItemDef> defs)
        {
            var list = new List<GeoItem>(geoChar.ArmourItems);
            bool addsEliteTorso = defs.Any(d => d.name != null
                && d.name.IndexOf("EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0);
            list.RemoveAll(i => i?.ItemDef?.name != null &&
                (i.ItemDef.name.IndexOf("Crabman_Legs", StringComparison.OrdinalIgnoreCase) >= 0
              || i.ItemDef.name.IndexOf("Crabman_Carapace", StringComparison.OrdinalIgnoreCase) >= 0
              || i.ItemDef.name.IndexOf("Crabman_EliteCarapace", StringComparison.OrdinalIgnoreCase) >= 0
              || (addsEliteTorso && i.ItemDef.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0
                                 && i.ItemDef.name.IndexOf("EliteTorso", StringComparison.OrdinalIgnoreCase) < 0)
              || (addsEliteTorso && i.ItemDef.name.IndexOf("Crabman_EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0)));
            foreach (var d in defs) { list.Add(new GeoItem(d)); }
            return list;
        }
    }
}
