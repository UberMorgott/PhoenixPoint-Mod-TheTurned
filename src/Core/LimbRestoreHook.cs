using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using System;
using System.Linq;
using TheTurned.Monsters.Arthron;

namespace TheTurned.Core
{
    /// <summary>
    /// Phase-4 survival capstone: Postfix on <c>GeoCharacter.ApllyTacticalResult</c> (ENGINE TYPO
    /// verbatim, GeoCharacter.cs:754 — the method that persists post-mission bodypart health via
    /// <c>_bodypartHealth = new List(result.BodypartsHealth)</c> + AggregateBodyPartHealth). After it
    /// runs, a Phase-4 recruit that learned the LIMBS marker gets every bodypart restored
    /// (<c>GeoCharacter.RestoreBodyPart</c>, GeoCharacter.cs:1312 — clears damage + re-aggregates).
    /// </summary>
    internal static class LimbRestoreHook
    {
        private static bool _applied;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled) return;
            var target = AccessTools.Method(typeof(GeoCharacter), nameof(GeoCharacter.ApllyTacticalResult));
            if (target == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] limb restore hook: GeoCharacter.ApllyTacticalResult not resolved — hook disabled.");
                return;
            }
            try
            {
                harmony.Patch(target, postfix: new HarmonyMethod(typeof(LimbRestoreHook), nameof(Postfix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] limb restore hook applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError($"[TheTurned] limb restore hook patch failed: {e}");
            }
        }

        public static void Postfix(GeoCharacter __instance)
        {
            try
            {
                if (__instance == null || !Phase4.IsPhase4Recruit(__instance))
                {
                    return;
                }
                var marker = ArthronSurvivalPerks.LimbRestoreMarker;
                if (marker == null || __instance.Progression == null)
                {
                    return;
                }
                bool learned = ArthronArms.EnumerateLearnedAbilities(__instance.Progression)
                    .Any(a => a != null && a.Guid == marker.Guid);
                if (!learned)
                {
                    return;
                }
                // ToList: RestoreBodyPart only mutates _bodypartHealth, but never enumerate a live
                // engine collection while calling back into its owner.
                foreach (GeoItem item in __instance.ArmourItems.ToList())
                {
                    __instance.RestoreBodyPart(item);
                }
                TheTurnedMain.LogInfo($"[TheTurned] limb restore: bodyparts auto-restored post-mission for '{__instance.DisplayName}'.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] limb restore postfix failed: " + e.Message);
            }
        }
    }
}
