using Base.Defs;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// TEMPORARY M1 render-proof dev action (REMOVE in M7). On Ctrl+Shift+Y it cycles the FIRST marked
    /// recruit through two armor loadouts via GeoCharacter.SetItems(armour:):
    ///   variant A = Crabman_Legs_Armoured_ItemDef + Crabman_Carapace_BodyPartDef
    ///   variant B = Crabman_Legs_EliteArmoured_ItemDef + Crabman_EliteTorso_BodyPartDef + Crabman_EliteCarapace_BodyPartDef
    /// GOAL: confirm the carapace back plate + armored legs + elite torso RENDER on the base Crabby chassis
    /// (preview + tactical). Read-only against the def DB; only mutates the recruit's _armourItems.
    /// </summary>
    internal static class CellDevDump
    {
        private static int _variant; // toggles A(0) / B(1)

        internal static void CycleArmorOnFirstRecruit(GeoLevelController geo)
        {
            if (geo?.PhoenixFaction == null || !Phase4.Enabled)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellDevDump: no geoscape/PhoenixFaction or Phase4 off.");
                return;
            }
            GeoCharacter recruit = geo.PhoenixFaction.Characters?.FirstOrDefault(Phase4.IsPhase4Recruit);
            if (recruit?.ArmourItems == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellDevDump: no marked recruit with ArmourItems found.");
                return;
            }
            ApplyVariant(recruit);
        }

        private static void ApplyVariant(GeoCharacter recruit)
        {
            DefRepository repo = DefUtils.Repo;
            string[] names = (_variant == 0)
                ? new[] { "Crabman_Legs_Armoured_ItemDef", "Crabman_Carapace_BodyPartDef" }
                : new[] { "Crabman_Legs_EliteArmoured_ItemDef", "Crabman_EliteTorso_BodyPartDef", "Crabman_EliteCarapace_BodyPartDef" };

            var add = new List<TacticalItemDef>();
            foreach (string n in names)
            {
                var def = DefUtils.ResolveByName<TacticalItemDef>(repo, n);
                if (def == null) { TheTurnedMain.LogWarn($"[TheTurned] CellDevDump: '{n}' NOT RESOLVED (bundle def missing)."); }
                else { add.Add(def); }
            }

            // Reuse the SHARED, M1-proven loadout recipe (slot-clear-then-add) so the dev action and the real
            // cell-armor apply path stay in lock-step (DRY). One SetItems(armour:) commits it.
            List<GeoItem> list = CellArmorApply.BuildArmorList(recruit, add);
            recruit.SetItems(armour: list);
            TheTurnedMain.LogInfo($"[TheTurned] CellDevDump variant {(_variant == 0 ? "A" : "B")} applied "
                + $"({add.Count}/{names.Length} resolved): [{string.Join(", ", add.Select(d => d.name))}] "
                + $"-> recruit now {list.Count} items.");
            _variant = 1 - _variant;
        }
    }
}
