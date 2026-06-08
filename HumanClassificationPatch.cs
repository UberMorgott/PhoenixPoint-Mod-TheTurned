using HarmonyLib;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;

namespace TheTurned
{
    /// <summary>
    /// Classifies the recruited Arthron as a "human" (= soldier) ONLY when its def carries our
    /// unique marker GameTag (<see cref="ArthronClass.MarkerTagName"/>). This routes the unit into
    /// GeoPhoenixFaction.Soldiers (Characters.Where(IsHuman), GeoPhoenixFaction.cs:189) instead of
    /// GroundVehicles, and sends the edit screen down ShowHumanProgression
    /// (UIModuleCharacterProgression.cs:485) which (together with the real Progression from our
    /// SpecializationDef) no longer NREs.
    ///
    /// Grounding for safety: find_referencing_symbols on CheckIsHuman/IsHuman shows ALL callers live
    /// in the Geoscape namespace (GeoUnitDescriptor.UnitTypeDescriptor ctor, UnitDisplayData,
    /// GeoscapeView roster/customize/mutate/bionics state routing, GeoPhoenixFaction.Soldiers/
    /// GroundVehicles, UIModuleCharacterProgression). A pattern search for CheckIsHuman/.IsHuman over
    /// the entire PhoenixPoint.Tactical namespace returns ZERO hits — so making CheckIsHuman return
    /// true for our marked def has NO tactical-side effect. The scope (marker tag only) means no
    /// other unit, human or alien, is affected.
    ///
    /// Patches CheckIsHuman (not the IsHuman property getter): IsHuman => CheckIsHuman(), so a single
    /// Postfix covers every read path.
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
                TheTurnedMain.Main?.Logger?.LogInfo("[TheTurned] Harmony: CheckIsHuman Postfix applied (marker-scoped).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError($"[TheTurned] Harmony patch failed: {e}");
            }
        }

        // Postfix: if the def carries our marker tag, classify it as human (soldier).
        private static void CheckIsHuman_Postfix(TacCharacterDef __instance, ref bool __result)
        {
            if (__result)
            {
                return; // already human — leave untouched
            }
            GameTagDef marker = ArthronClass.RecruitMarkerTag;
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
