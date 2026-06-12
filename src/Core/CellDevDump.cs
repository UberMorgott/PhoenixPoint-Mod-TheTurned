using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

        /// <summary>
        /// TEMP dev action (Ctrl+Shift+U): add ONE level to the first Phase-4 recruit, exercising the REAL
        /// auto-unlock-by-level mechanic. Grants the XP gap to the next level threshold via
        /// LevelProgression.AddExperience [G LevelProgression.cs:78], which recomputes Level and fires
        /// CharacterProgression.OnLevelUp -> SkillPoints += SkillpointsPerLevel [G CharacterProgression.cs:120-122].
        /// One press = +1 level; no-op at MaxLevel. Works regardless of DevUnlockAllLevels (it only adds levels).
        /// Refreshes the progression panel if open so the newly-unlocked cell shows. REMOVE with the other dev
        /// keys at cleanup.
        /// </summary>
        internal static void LevelUpFirstRecruit(GeoLevelController geo)
        {
            if (geo?.PhoenixFaction == null || !Phase4.Enabled)
            {
                TheTurnedMain.LogWarn("[TheTurned] DevLevelUp: no geoscape/PhoenixFaction or Phase4 off.");
                return;
            }
            GeoCharacter recruit = geo.PhoenixFaction.Characters?.FirstOrDefault(Phase4.IsPhase4Recruit);
            LevelProgression lp = recruit?.Progression?.LevelProgression;
            if (lp == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] DevLevelUp: no marked recruit with a LevelProgression found.");
                return;
            }
            if (lp.IsMaxLevel)
            {
                TheTurnedMain.LogInfo($"[TheTurned] DevLevelUp: '{recruit.GetName()}' already at MaxLevel ({lp.Def.MaxLevel}).");
                return;
            }

            // XP gap to the NEXT level's cumulative threshold (TotalNextLevelExperience) [G LevelProgression.cs:33].
            int gap = lp.TotalNextLevelExperience - lp.Experience;
            if (gap < 1)
            {
                gap = 1;
            }
            int beforeLevel = lp.Level;
            recruit.Progression.LevelProgression.AddExperience(gap); // -> OnLevelUp -> SkillPoints granted
            TheTurnedMain.LogInfo($"[TheTurned] DevLevelUp: '{recruit.GetName()}' now level {lp.Level} "
                + $"(was {beforeLevel}, XP={lp.Experience}, SP={recruit.Progression.SkillPoints}).");

            // Refresh the open progression panel so the newly-unlocked cell visibly unlocks.
            UIModuleCharacterProgression module = UnityEngine.Object.FindObjectOfType<UIModuleCharacterProgression>();
            if (module != null && recruit.Faction is GeoPhoenixFaction phx)
            {
                module.SetCharacterProgression(phx, recruit);
            }
        }

        // BUG1 — TEMP DEV LEG-MESH IDENTIFIER (Ctrl+Shift+L). Gated behind DevLegCycle so it is trivial to
        // remove. All four leg defs are distinct meshes; visually they are hard to tell apart, so this cycles
        // the FIRST recruit's equipped legs through them in a fixed order and logs the current def name. Reuses
        // the shared CellArmorApply.BuildArmorList path (strips Crabman_Legs, re-adds the chosen leg) exactly
        // like the Ctrl+Shift+Y armor cycle, so the equip route is identical to the real cell-armor apply.
        internal const bool DevLegCycle = true;
        private static int _legIndex;
        private static readonly string[] LegDefNames =
        {
            "Crabman_Legs_Agile_ItemDef",        // L1 light plain
            "Crabman_Legs_EliteAgile_ItemDef",   // L2 light + armor
            "Crabman_Legs_Armoured_ItemDef",     // L3 heavy plain
            "Crabman_Legs_EliteArmoured_ItemDef",// L4 heavy + armor
        };

        internal static void CycleLegsOnFirstRecruit(GeoLevelController geo)
        {
            if (geo?.PhoenixFaction == null || !Phase4.Enabled)
            {
                TheTurnedMain.LogWarn("[TheTurned] LEGCYCLE: no geoscape/PhoenixFaction or Phase4 off.");
                return;
            }
            GeoCharacter recruit = geo.PhoenixFaction.Characters?.FirstOrDefault(Phase4.IsPhase4Recruit);
            if (recruit?.ArmourItems == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] LEGCYCLE: no marked recruit with ArmourItems found.");
                return;
            }
            string name = LegDefNames[_legIndex];
            DefRepository repo = DefUtils.Repo;
            var leg = DefUtils.ResolveByName<TacticalItemDef>(repo, name);
            if (leg == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned][LEGCYCLE] '{name}' NOT RESOLVED (bundle def missing).");
                return;
            }
            List<GeoItem> list = CellArmorApply.BuildArmorList(recruit, new List<TacticalItemDef> { leg });
            recruit.SetItems(armour: list);
            TheTurnedMain.LogInfo($"[TheTurned][LEGCYCLE] now={name}");
            _legIndex = (_legIndex + 1) % LegDefNames.Length;
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
