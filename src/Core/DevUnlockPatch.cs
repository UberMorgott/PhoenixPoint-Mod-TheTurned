using System;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Progression;
using PhoenixPoint.Geoscape.View.ViewModules;

namespace TheTurned.Core
{
    /// <summary>
    /// DEV-only (<see cref="Phase4.DevUnlockAllLevels"/>): make EVERY Phase-4 recruit progression
    /// cell clickable AND buyable at level 1. Three grounded seams (decompile):
    ///  1. <c>UICharacterProgressionUtl.GetAbilityAdjustedLevel</c> — sole feeder of
    ///     <c>MutoidAbilityTrackContainerElement.IsAbilityLocked</c> (via GetSlotLevel :31-34; only
    ///     call site engine-wide). Postfix → 1 unlocks the main-track cells (clicking an unlocked
    ///     cell opens the popup, OnTrackSlotPointerClicked :99-106 gates on !LockedSkill).
    ///  2. <c>SpecializedAbilityTrackPopupElement.Init</c> — the popup lock is a LOCAL
    ///     <c>level &lt; num3</c> (Init :146) read once from character.LevelProgression.Level (:74).
    ///     Prefix/Finalizer temporarily raise <c>LevelProgression.Experience</c> (public field;
    ///     Level => Def.GetLevel(Experience)) to max-level XP so the ENGINE computes every flag
    ///     (learned/buyable/wallet) itself, then restore the exact value. Finalizer (not Postfix)
    ///     so the restore runs even if Init throws.
    ///  3. <c>UIModuleCharacterProgression.OnTrackSlotPointerClicked</c> — purchase gate
    ///     <c>Level &gt;= button.AbilityLevel</c> (:1045); same temporary-XP elevation.
    ///     button.AbilityLevel must stay REAL: BuyAbility places the bought ability via
    ///     <c>GetAbilitySlotForLevel(_boughtAbilityLevel)</c> (:428), so rewriting it would corrupt
    ///     track placement — hence elevation, not an AbilityLevel rewrite.
    /// All seams gate on <see cref="Phase4.IsPhase4Recruit"/>; vanilla/TFTV characters untouched.
    /// Graceful-disable on any unresolved target (same pattern as <see cref="PandoranProgressionGate"/>).
    /// </summary>
    internal static class DevUnlockPatch
    {
        private static bool _applied;
        private static readonly FieldInfo ModuleCharacterField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_character");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled || !Phase4.DevUnlockAllLevels)
            {
                return;
            }
            MethodInfo adjustedLevel = AccessTools.Method(typeof(UICharacterProgressionUtl),
                nameof(UICharacterProgressionUtl.GetAbilityAdjustedLevel));
            MethodInfo popupInit = AccessTools.Method(typeof(SpecializedAbilityTrackPopupElement),
                nameof(SpecializedAbilityTrackPopupElement.Init));
            MethodInfo slotClicked = AccessTools.Method(typeof(UIModuleCharacterProgression),
                "OnTrackSlotPointerClicked");
            if (adjustedLevel == null || popupInit == null || slotClicked == null || ModuleCharacterField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] DEV unlock: target(s) unresolved (adjLevel="
                    + $"{adjustedLevel != null} popupInit={popupInit != null} clicked={slotClicked != null} "
                    + $"charField={ModuleCharacterField != null}) — dev unlock disabled.");
                return;
            }
            try
            {
                harmony.Patch(adjustedLevel,
                    postfix: new HarmonyMethod(typeof(DevUnlockPatch), nameof(AdjustedLevelPostfix)));
                harmony.Patch(popupInit,
                    prefix: new HarmonyMethod(typeof(DevUnlockPatch), nameof(PopupInitPrefix)),
                    finalizer: new HarmonyMethod(typeof(DevUnlockPatch), nameof(RestoreXpFinalizer)));
                harmony.Patch(slotClicked,
                    prefix: new HarmonyMethod(typeof(DevUnlockPatch), nameof(SlotClickedPrefix)),
                    finalizer: new HarmonyMethod(typeof(DevUnlockPatch), nameof(RestoreXpFinalizer)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] DEV unlock-all-levels active");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] DEV unlock patch failed: " + e);
            }
        }

        /// <summary>Saved XP for one temporarily-elevated call (passed prefix→finalizer via __state).</summary>
        public sealed class XpElevation
        {
            public LevelProgression Progression;
            public int SavedExperience;
        }

        /// <summary>Elevate the character's XP to max level for the duration of one engine call;
        /// returns null (no-op) for anyone but our Phase-4 recruits.</summary>
        private static XpElevation Elevate(GeoCharacter character)
        {
            if (!Phase4.IsPhase4Recruit(character))
            {
                return null;
            }
            LevelProgression lp = character.LevelProgression; // == Progression.LevelProgression (GeoCharacter.cs:239)
            if (lp?.Def == null)
            {
                return null;
            }
            XpElevation state = new XpElevation { Progression = lp, SavedExperience = lp.Experience };
            lp.Experience = lp.Def.GetTotalXpForLevel(lp.Def.MaxLevel);
            return state;
        }

        // Seam 1: main-track cell lock. Param name `character` binds the original's first argument.
        public static void AdjustedLevelPostfix(GeoCharacter character, ref int __result)
        {
            if (Phase4.IsPhase4Recruit(character))
            {
                __result = 1;
            }
        }

        // Seam 2: popup cells (Init reads character.LevelProgression.Level once at :74).
        public static void PopupInitPrefix(GeoCharacter character, ref XpElevation __state)
        {
            __state = Elevate(character);
        }

        // Seam 3: purchase gate (reads _character.Progression.LevelProgression.Level at :1045).
        public static void SlotClickedPrefix(UIModuleCharacterProgression __instance, ref XpElevation __state)
        {
            __state = Elevate(ModuleCharacterField.GetValue(__instance) as GeoCharacter);
        }

        // Shared restore; finalizers receive prefix injections incl. __state and run even on throw.
        public static void RestoreXpFinalizer(XpElevation __state)
        {
            if (__state?.Progression != null)
            {
                __state.Progression.Experience = __state.SavedExperience;
            }
        }
    }
}
