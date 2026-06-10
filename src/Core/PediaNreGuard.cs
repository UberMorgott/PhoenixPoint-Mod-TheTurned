using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Levels;
using System;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2 crash guard. Applying a Crabman augment moves the displaced part(s) to faction storage
    /// (<c>UIModuleBionics.OnAugmentApplied</c> -> <c>ItemStorage.AddItem</c> [G :220]); that fires
    /// <c>GeoPhoenixpedia.OnStorageItemAdded -> AddEntryFromDef -> AddItemEntry</c> [G GeoPhoenixpedia.cs:171,
    /// 139, 219]. <c>AddItemEntry</c> dereferences <c>item.RelatedItemDef.ViewElementDef.Description</c> and
    /// <c>GetDisplayName()</c> [G :244-245, ItemDef.cs:156] WITHOUT a null-VED guard — a Crabman hand/bodypart
    /// def with a null ViewElementDef NREs there (no pedia/VED data for bundle-only crab items), crashing the
    /// apply. We Finalize <c>AddItemEntry</c> and SWALLOW the exception ONLY when the offending item is a
    /// Crabman def (name token); every other item path still throws normally (no broad suppression).
    /// </summary>
    internal static class PediaNreGuard
    {
        internal const string PatchId = "Morgott.TheTurned.PediaNreGuard";
        private static bool _applied;
        private static bool _warnedOnce;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo target = AccessTools.Method(typeof(GeoPhoenixpedia), "AddItemEntry");
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] PediaNreGuard: GeoPhoenixpedia.AddItemEntry not found — guard NOT applied.");
                    return;
                }
                MethodInfo finalizer = AccessTools.Method(typeof(PediaNreGuard), nameof(AddItemEntry_Finalizer));
                harmony.Patch(target, finalizer: new HarmonyMethod(finalizer));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] PediaNreGuard: GeoPhoenixpedia.AddItemEntry Finalizer applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] PediaNreGuard apply failed: " + e);
            }
        }

        /// <summary>
        /// Finalizer: return null to SUPPRESS the exception, or return the Exception to RE-THROW it (Harmony).
        /// We suppress ONLY when <paramref name="__exception"/> is non-null AND the item is a Crabman def.
        /// <paramref name="__result"/> is set false so <c>AddEntryFromDef</c> treats the entry as not-added.
        /// </summary>
        private static Exception AddItemEntry_Finalizer(Exception __exception, ItemDef item, ref bool __result)
        {
            if (__exception == null)
            {
                return null; // no error -> nothing to do
            }
            string name = item?.name;
            if (name != null && name.IndexOf("Crabman", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                __result = false; // pedia entry not added; safe for our swap-only crab parts
                if (!_warnedOnce)
                {
                    _warnedOnce = true;
                    TheTurnedMain.LogInfo($"[TheTurned] pedia-NRE guarded for '{name}' "
                        + $"(no VED/pedia data on bundle-only Crabman item): {__exception.GetType().Name}.");
                }
                return null; // swallow (Harmony: returning null from a finalizer clears the exception)
            }
            return __exception; // not ours -> re-throw unchanged
        }
    }
}
