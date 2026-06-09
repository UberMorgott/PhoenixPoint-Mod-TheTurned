using Base.Levels;
using HarmonyLib;
using PhoenixPoint.Geoscape.Levels;
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

        /// <summary>Static log helpers (null-safe before/after enable) for code outside this class.</summary>
        internal static void LogInfo(string message) => Main?.Logger?.LogInfo(message);
        internal static void LogWarn(string message) => Main?.Logger?.LogWarning(message);

        public override void OnModEnabled()
        {
            Main = this;
            Phase4.Init(this);
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
            // Arm-follow hook DISABLED along with the arm roll (rolled hand WeaponDefs lacked compatible arm
            // BodyPartDefs → 22k addon-attach errors). Nothing to keep in sync until arms return.
            // Phase 4: arm choice will be reimplemented via mutoid-style progression (see design doc).
            // if (level != null && level.name != null && level.name.Contains("Home"))
            // {
            //     GeoLevelController geo = level.GetComponent<GeoLevelController>();
            //     if (geo != null)
            //     {
            //         ArmFollowHook.ScanAndSubscribe(geo);
            //         Logger.LogInfo("[TheTurned] Arm-follow hook scanned/subscribed on geoscape start.");
            //     }
            // }
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
                // Phase 3: build the optional second spec row + the rolled arm-option marker defs.
                TurnedClassFactory.EnsureSecondaryClass(repo, monster);
                if (monster.HasRolledArms)
                {
                    monster.BuildArmOptions(repo);
                }
            }
        }
    }
}
