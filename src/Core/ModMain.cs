using Base.Levels;
using HarmonyLib;
using PhoenixPoint.Modding;
using TheTurned.Core;
using UnityEngine;

namespace TheTurned
{
    /// <summary>
    /// Main mod class (one per assembly). Mirrors OfficerMain lifecycle. The type name is kept as
    /// <c>TheTurnedMain</c> so the loader's ModMain-subclass discovery and existing references resolve.
    ///
    /// On enable it: registers all monsters (<see cref="MonsterRegistry"/>), builds each monster's
    /// class/spec generically via <see cref="TurnedClassFactory"/>, applies the shared CheckIsHuman
    /// Harmony patch ONCE, and attaches the input-poller MonoBehaviour to the live mod GameObject (ModGO).
    /// On every geoscape ("Home") unload it re-applies the classes (Officer-style) for persistence, since
    /// other mods (e.g. TFTV) re-inject defs on geoscape load.
    /// </summary>
    public class TheTurnedMain : ModMain
    {
        public static TheTurnedMain Main { get; private set; }

        /// Mirror Officer: unsafe to disable (no clean revert of runtime grants).
        public override bool CanSafelyDisable => false;

        public override void OnModEnabled()
        {
            Main = this;
            Logger.LogInfo("[TheTurned] Enabling — Ctrl+Shift+<key> on the geoscape recruits a turned monster.");

            MonsterRegistry.RegisterDefaults();

            // Import localization terms (class + perk names/descriptions) from the shipped CSV.
            Localization.AddLocalizationFromCSV("TheTurned.csv");

            // Build each monster's class (ClassTag + shared marker + SpecializationDef) BEFORE any
            // geoscape exists, so FactionCharacterGenerator.Start() caches our specs. Idempotent.
            BuildAllClasses();

            // Apply the shared marker-scoped CheckIsHuman Postfix. Idempotent (guarded inside Apply).
            HumanClassificationPatch.Apply((Harmony)HarmonyInstance);

            // Attach the hotkey poller to the mod's live GameObject (persists for mod lifetime).
            GameObject go = ModGO;
            if (go != null)
            {
                if (go.GetComponent<RecruitHotkey>() == null)
                {
                    go.AddComponent<RecruitHotkey>();
                }
                Logger.LogInfo("[TheTurned] RecruitHotkey component attached to ModGO.");
            }
            else
            {
                Logger.LogError("[TheTurned] ModGO was null in OnModEnabled — hotkey not attached.");
            }
        }

        public override void OnModDisabled()
        {
            // ModGO (and the attached RecruitHotkey) is destroyed by the game after this call.
            Main = null;
        }

        public override void OnLevelStart(Level level)
        {
            // No work needed on level start; classes are (re)built on enable + on geoscape unload.
        }

        public override void OnLevelEnd(Level level)
        {
            // Re-apply classes when leaving the geoscape ("Home"), Officer-style, so they survive
            // other mods re-injecting defs on the next geoscape load. Idempotent (GetDef-guarded).
            if (level != null && level.name != null && level.name.Contains("Home"))
            {
                BuildAllClasses();
                Logger.LogInfo("[TheTurned] Classes re-applied on geoscape unload (Home).");
            }
        }

        private void BuildAllClasses()
        {
            var repo = DefUtils.Repo;
            foreach (ITurnedMonster monster in MonsterRegistry.All)
            {
                TurnedClassFactory.EnsureClass(repo, monster);
            }
        }
    }
}
