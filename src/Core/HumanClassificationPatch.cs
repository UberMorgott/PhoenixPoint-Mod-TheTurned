using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// SHARED across all turned monsters: classifies a recruited unit as "human" (= soldier) ONLY when
    /// its def carries the single shared marker GameTag (<see cref="Tags.MarkerTagName"/>). This routes
    /// the unit into GeoPhoenixFaction.Soldiers (instead of GroundVehicles) and sends the edit screen down
    /// ShowHumanProgression, which — together with the real Progression from the monster's
    /// SpecializationDef — no longer NREs.
    ///
    /// Safety (grounded): all CheckIsHuman/IsHuman callers live in the Geoscape namespace; a search over
    /// PhoenixPoint.Tactical returns ZERO hits, so flipping CheckIsHuman for a marked def has no
    /// tactical-side effect. The scope (marker tag only) means no other unit, human or alien, is affected.
    /// Patches CheckIsHuman (IsHuman => CheckIsHuman()), so a single Postfix covers every read path.
    /// </summary>
    internal static class HumanClassificationPatch
    {
        internal const string PatchId = "Morgott.TheTurned.HumanClassification";
        private static bool _applied;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(TacCharacterDef), nameof(TacCharacterDef.CheckIsHuman));
                var postfix = AccessTools.Method(typeof(HumanClassificationPatch), nameof(CheckIsHuman_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.Main?.Logger?.LogInfo("[TheTurned] Harmony: CheckIsHuman Postfix applied (shared marker-scoped).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError($"[TheTurned] Harmony patch failed: {e}");
            }
        }

        // Postfix: if the def carries the shared marker tag, classify it as human (soldier).
        private static void CheckIsHuman_Postfix(TacCharacterDef __instance, ref bool __result)
        {
            if (__result)
            {
                return; // already human — leave untouched
            }
            GameTagDef marker = Tags.RecruitMarkerTag;
            if (marker == null || __instance?.Data?.GameTags == null)
            {
                return;
            }
            if (__instance.Data.GameTags.Contains(marker))
            {
                __result = true;
            }
        }
    }
}
