using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using System;

namespace TheTurned.Core
{
    /// <summary>
    /// Removes the Stamina/Fatigue mechanic (and therefore its UI widget) for a turned recruit.
    ///
    /// The mod's <see cref="HumanClassificationPatch"/> classifies the recruit as human, so the generator
    /// path calls <c>GeoCharacter.AddFaitgue(...)</c> (vanilla typo spelling; decompile GeoCharacter.cs:507,
    /// invoked at GeoCharacter.cs:930) and the recruit ends up with a non-null <c>Fatigue</c>. The
    /// progression screen then shows the StaminaSlider/StaminaStatText (UIModuleCharacterProgression.cs:574)
    /// — fatigue is meaningless for a Pandoran soldier.
    ///
    /// Fix (marker-scoped Prefix): for a recruit, SKIP <c>AddFaitgue</c> entirely. <c>_fatigue</c> stays
    /// null → the <c>Fatigue</c> getter returns null → the progression module takes its else branch
    /// (UIModuleCharacterProgression.cs:593) and hides the slider + stat text. This removes the MECHANIC,
    /// not just the widget. Non-recruits run the vanilla method untouched.
    /// </summary>
    internal static class RecruitFatiguePatch
    {
        internal const string PatchId = "Morgott.TheTurned.RecruitFatigue";
        private static bool _applied;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(GeoCharacter), nameof(GeoCharacter.AddFaitgue));
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitFatiguePatch: AddFaitgue unresolved — fatigue removal disabled.");
                    return;
                }
                var prefix = AccessTools.Method(typeof(RecruitFatiguePatch), nameof(AddFaitgue_Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] RecruitFatiguePatch: AddFaitgue Prefix applied (recruit fatigue removed).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitFatiguePatch apply failed: " + e);
            }
        }

        // Skip fatigue wiring for a marked recruit (return false). CharacterFatigue param is ignored.
        private static bool AddFaitgue_Prefix(GeoCharacter __instance, CharacterFatigue fatigue)
        {
            try
            {
                return !Phase4.IsPhase4Recruit(__instance);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitFatiguePatch Prefix threw (running vanilla AddFaitgue): " + e);
                return true;
            }
        }
    }
}
