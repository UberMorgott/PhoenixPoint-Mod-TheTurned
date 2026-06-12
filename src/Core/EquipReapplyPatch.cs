using HarmonyLib;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Geoscape.View.ViewStates;
using System;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// ROOT-CAUSE fix for the edit-soldier screen reverting our applied crab equipment. On the edit-soldier
    /// screen, <c>UIStateEditSoldier.UpdateSoldierEquipment(GeoCharacter)</c> [G UIStateEditSoldier.cs:490] runs
    /// every refresh and re-pushes the UI's stale human ArmorList back onto the GeoCharacter via
    /// <c>soldier.SetItems(_soldierEquipModule.ArmorList.UnfilteredItems.OfType&lt;GeoItem&gt;(), ..)</c> [G :492].
    /// That ArmorList never carries our crab cell bodyparts, so the engine's per-frame re-push wins and reverts
    /// the cell armor / augment arms the mod wrote directly onto the model. (The augment screen,
    /// <see cref="UIModuleBionics"/>, has no such reverter — that path works.)
    ///
    /// Earlier attempts re-applied the crab loadout AFTER the engine re-push (Postfix). That created a per-frame
    /// battle: the Postfix SetItems(armour:) → UpdateStats → StatChanged → RequestRefreshCharacterData →
    /// _uiRefreshNeeded → UpdateState → UpdateSoldierEquipment again, an infinite loop (lag + flashing).
    ///
    /// This PREFIX kills the revert AT THE SOURCE instead. For a Phase-4 recruit it replicates the original's
    /// ready/inventory commit + preferred-loadout call but passes <c>armour = null</c> to SetItems, so
    /// <c>_armourItems</c> is never cleared and UpdateStats is never triggered [G GeoCharacter.cs:706 / :748] —
    /// the crab cell armor the one-time ApplyChosenSets / ApplyLearnedArmor path wrote stays untouched and
    /// persists. Then it returns false to skip the original (which would re-push the human ArmorList). Humans /
    /// non-recruits run the original verbatim (return true), so vanilla equipping is unaffected.
    /// </summary>
    internal static class EquipReapplyPatch
    {
        internal const string PatchId = "Morgott.TheTurned.EquipReapply";
        private static bool _applied;

        // Private get-only property on UIStateEditSoldier [G UIStateEditSoldier.cs:64]:
        //   private UIModuleSoldierEquip _soldierEquipModule => base._geoscapeModules.SoldierEquipModule;
        // Read it off __instance via reflection (Traverse) rather than re-deriving the base module chain.
        private const string EquipModuleMember = "_soldierEquipModule";

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                // private void UpdateSoldierEquipment(GeoCharacter soldier) [G UIStateEditSoldier.cs:490].
                MethodInfo target = AccessTools.Method(typeof(UIStateEditSoldier), "UpdateSoldierEquipment");
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] EquipReapplyPatch: UpdateSoldierEquipment unresolved — patch disabled.");
                    return;
                }
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(EquipReapplyPatch), nameof(Prefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] EquipReapplyPatch: UpdateSoldierEquipment Prefix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] EquipReapplyPatch apply failed: " + e);
            }
        }

        /// <summary>
        /// For a Phase-4 recruit, commit ready + inventory exactly like the original UpdateSoldierEquipment but
        /// pass armour=null so the crab armour is NOT reverted (and UpdateStats not triggered), then skip the
        /// original. For everyone else, run the original verbatim. On any error, fail safe to the original.
        /// Runs once per refresh — keep it silent on the happy path (no per-frame log spam).
        /// </summary>
        public static bool Prefix(GeoCharacter soldier, UIStateEditSoldier __instance)
        {
            if (soldier == null || !Phase4.Enabled || !Phase4.IsPhase4Recruit(soldier))
            {
                return true; // humans / non-recruits: original verbatim
            }
            try
            {
                // _soldierEquipModule [G UIStateEditSoldier.cs:64] — private get-only property off __instance.
                UIModuleSoldierEquip equipModule = Traverse.Create(__instance).Property(EquipModuleMember).GetValue<UIModuleSoldierEquip>();
                if (equipModule == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] EquipReapply prefix: _soldierEquipModule null — running original.");
                    return true;
                }
                // Mirror the original [G UIStateEditSoldier.cs:492] EXCEPT armour: pass null so SetItems skips
                // _armourItems.Clear()+UpdateStats [G GeoCharacter.cs:706/748] and the crab loadout persists.
                var ready = equipModule.ReadyList.UnfilteredItems.OfType<GeoItem>();      // [G UIModuleSoldierEquip.cs:43]
                var inv = equipModule.InventoryList.UnfilteredItems.OfType<GeoItem>();    // [G UIModuleSoldierEquip.cs:40]
                soldier.SetItems(armour: null, equipment: ready, inventory: inv);         // [G GeoCharacter.cs:703]
                // Replicate the original's preferred-loadout call [G UIStateEditSoldier.cs:493-496].
                if (soldier.Faction is GeoPhoenixFaction faction)
                {
                    faction.UpdatePreferredLoadout(soldier);                              // [G GeoPhoenixFaction.cs:1233]
                }
                return false; // skip the original (which would re-push the stale human ArmorList and revert armour)
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] EquipReapply prefix error: " + e);
                return true; // fail safe to the original
            }
        }
    }
}
