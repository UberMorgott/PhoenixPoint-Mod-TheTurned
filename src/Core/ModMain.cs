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
    /// On every level unload it re-applies the classes (Officer-style, idempotent) for persistence, since
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

        // One-shot diagnostic flag (see OnLevelStart).
        private static bool _mutoidMaxLevelLogged;

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

            // Frozen-idle fix: the CheckIsHuman flip makes UnitDisplayData assign the HUMAN anim-actions to
            // the crab rig; this marker-scoped UnitDisplayData ctor Postfix restores the crab's native
            // anim-actions for the recruit only. Idempotent (guarded inside Apply).
            RecruitAnimActionsPatch.Apply((Harmony)HarmonyInstance);

            // V1 Phase-1 augment screen: DNA button + marker-scoped show/hide + one-shot diagnostics dump.
            // All idempotent (guarded inside each Apply). The dump is Phase4-gated and self-removes after one fire.
            AugmentButtonPatch.Apply((Harmony)HarmonyInstance);
            AugmentButtonVisibilityPatch.Apply((Harmony)HarmonyInstance);
            AugmentDiagnosticsDump.Apply((Harmony)HarmonyInstance);

            // V1 Phase-2 augment screen: retarget the 3 Bionics sections to Crabman Head/LeftArm/RightArm on
            // every character switch (symmetric recruit<->human), scope-unlock variants WITHOUT polluting the
            // persisted faction set, enforce matched bodypart+hand SETs on apply, and guard the
            // GeoPhoenixpedia NRE when a crab item is moved to storage. All idempotent + recruit-scoped.
            BionicsSectionPatch.Apply((Harmony)HarmonyInstance);
            BionicsUnlockBypass.Apply((Harmony)HarmonyInstance);
            BionicsSlotInitGuard.Apply((Harmony)HarmonyInstance);
            BionicsApplyPatch.Apply((Harmony)HarmonyInstance);
            PediaNreGuard.Apply((Harmony)HarmonyInstance);

            // Phase-4: OR our recruits into the mutoid progression gate (no-op when TFTV absent).
            PandoranProgressionGate.Apply((Harmony)HarmonyInstance);

            // Phase-4: post-mission limb auto-restore for the survival capstone (no-op when TFTV absent).
            LimbRestoreHook.Apply((Harmony)HarmonyInstance);

            // Phase-4: clamp the Personal + Secondary row render to the prefab pool size (5) on the
            // mutoid progression screen (hiding the rows blanked the screen — they ARE the visible UI).
            RowRenderClampPatch.Apply((Harmony)HarmonyInstance);

            // Phase-4: backfill the mutoid popup's null row/cell prefab templates (overflow = our fed rows).
            PopupPrefabPatch.Apply((Harmony)HarmonyInstance);

            // DEV: unlock all progression levels for Phase-4 recruits (no-op unless DevUnlockAllLevels).
            DevUnlockPatch.Apply((Harmony)HarmonyInstance);

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
            // Phase 4: arm-follow hook re-enabled — swaps are now matched bodypart+hand SETs (C3), which
            // fixes the old hand-only-swap 22k addon-attach errors. Gated inside on Phase4.Enabled + HasSets.
            // Geoscape detection = GeoLevelController component presence, NOT level.name: the level
            // literally named "Home" is the MAIN MENU, so the old name gate never matched the real
            // geoscape (FeedRows/ScanAndSubscribe never ran — zero 'FeedRows:' lines in 41MB of logs).
            GeoLevelController geo = level != null ? level.GetComponent<GeoLevelController>() : null;
            if (geo != null)
            {
                // Diagnostic (once): mutoid progression MaxLevel — documents the 5-wide-pool/7-level
                // mismatch behind RowRenderClampPatch (FactionCharacterGenerator.MutoidLevelProgression).
                // In-game 2026-06-10 this printed <null>: the field isn't wired on this generator
                // instance — vanilla mutoid level count isn't recoverable here; our recruits use the
                // borrowed HUMAN LevelProgressionDef (MaxLevel 7) regardless.
                if (!_mutoidMaxLevelLogged && Phase4.Enabled)
                {
                    _mutoidMaxLevelLogged = true;
                    var mutoidLp = geo.CharacterGenerator != null ? geo.CharacterGenerator.MutoidLevelProgression : null;
                    Logger.LogInfo("[TheTurned] MutoidLevelProgression MaxLevel = "
                        + (mutoidLp != null ? mutoidLp.MaxLevel.ToString() : "<null>"));
                }
                ArmFollowHook.ScanAndSubscribe(geo);
                // Phase-4: feed the popup spec ROWS into the faction list (no-op when TFTV absent).
                SpecRowFactory.FeedRows(geo);
                // Log only when the arm subscribe actually ran (same gate ScanAndSubscribe puts on
                // its arm path), so V1 log reading isn't confused when Phase 4 is disabled.
                if (Phase4.Enabled && CrabmanParts.HasSets)
                {
                    Logger.LogInfo("[TheTurned] Arm-follow hook scanned/subscribed on geoscape start.");
                }
            }
        }

        public override void OnLevelEnd(Level level)
        {
            // Re-apply classes on EVERY level unload, Officer-style, so they survive other mods
            // re-injecting defs on the next geoscape load. Unconditional on purpose: BuildAllClasses
            // is idempotent get-or-create (cheap GetDef hits once defs exist), and the old "Home"
            // name gate actually fired on MAIN-MENU unload (accidental but correct pre-geoscape
            // timing, incl. after save-load) — unconditional keeps that AND covers geoscape/tactical
            // unloads without needing a second GetComponent probe.
            BuildAllClasses();
            Logger.LogInfo($"[TheTurned] Classes re-applied on level unload ('{level?.name}').");
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
            // Phase 4: enumerate + pair Crabman matched bodypart/hand SETs (idempotent, logs once),
            // then build the popup spec ROWS (Bruiser + Gunner reuse rows).
            if (Phase4.Enabled)
            {
                CrabmanParts.Build(repo);
                Monsters.Arthron.ArthronMonster.BuildPhase4Rows(repo);
                // V1 Phase-2 augment screen: make Crabman head/arm variant bodyparts eligible as Bionics
                // cards (BionicalTag + zero cost) and resolve the Crabman augment slot defs. Idempotent.
                AugmentVariants.Prepare(repo);
            }
        }
    }
}
