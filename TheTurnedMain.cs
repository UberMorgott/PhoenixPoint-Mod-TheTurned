using Base.Levels;
using HarmonyLib;
using PhoenixPoint.Modding;
using UnityEngine;

namespace TheTurned
{
    /// <summary>
    /// Main mod class (one per assembly). Mirrors OfficerMain lifecycle.
    /// On enable it attaches the input-poller MonoBehaviour to the live mod GameObject
    /// (ModGO), which lives for the whole mod lifetime and fires the Arthron recruit
    /// only while a geoscape is active.
    /// </summary>
    public class TheTurnedMain : ModMain
    {
        public static TheTurnedMain Main { get; private set; }

        /// Mirror Officer: unsafe to disable (no clean revert of runtime grants).
        public override bool CanSafelyDisable => false;

        public override void OnModEnabled()
        {
            Main = this;
            Logger.LogInfo("[TheTurned] Enabling — Ctrl+Shift+T on the geoscape recruits one Arthron.");

            // Create the dedicated Arthron class (ClassTag + marker tag + SpecializationDef) BEFORE
            // any geoscape exists, so FactionCharacterGenerator.Start() caches our spec in its
            // SpecializationsDefs list (FactionCharacterGenerator.cs:105). Idempotent on re-enable.
            ArthronClass.EnsureCreated();

            // Apply the marker-scoped CheckIsHuman Postfix (classifies the recruit as a soldier).
            // Idempotent: guarded inside Apply so a re-enable does not double-patch.
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
            // No def-repo work for the MVP — we reference the live vanilla Arthron def at
            // recruit time, not a clone. Stub kept for a future cloned/customized def
            // (idempotent re-apply on the "Home" geoscape, see research note §5).
        }

        public override void OnLevelEnd(Level level)
        {
            // Stub for future idempotent re-apply (e.g. when we ship a cloned TacCharacterDef).
        }
    }
}
