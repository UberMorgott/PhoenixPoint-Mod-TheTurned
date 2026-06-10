using HarmonyLib;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Events.Eventus;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using System.Collections.Generic;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2 unlock bypass — make our Crabman variant cards appear UNLOCKED/buyable for the recruit
    /// WITHOUT polluting the persisted <c>faction.UnlockedAugmentations</c> set (it serializes — [G
    /// GeoFaction.cs:371/592] — so a permanent <c>.Add</c> would leave orphan defs in the save / in
    /// <c>UnlockedBionics</c> after the mod is removed).
    ///
    /// The native section gates a card on <c>_context.GetFaction().UnlockedAugmentations.Contains(mutation)</c>
    /// in four places: render (<c>RefreshContainerSlots</c> [G UIModuleMutationSection.cs:365] — locked sprite
    /// when absent), the apply gate (<c>CanApplyAugumentation</c> [G :167]), and the two hover gates
    /// [G :151/159]. We TEMPORARILY add our recruit variants to the set in a Prefix and REMOVE them in a
    /// Finalizer, scoped to each method's synchronous execution — so <c>Contains</c> returns true while the
    /// method runs but the set is restored before control returns. No save can interleave a synchronous call,
    /// so the persisted set is NEVER serialized with our defs (clean save, clean uninstall).
    ///
    /// Gated on the section's current character being a Phase4 recruit — humans never touch the set.
    /// </summary>
    internal static class BionicsUnlockBypass
    {
        internal const string PatchId = "Morgott.TheTurned.BionicsUnlockBypass";
        private static bool _applied;

        private static readonly FieldInfo ContextField =
            AccessTools.Field(typeof(UIModuleMutationSection), "_context");

        // Re-entrancy-safe scope state (single-threaded UI). The patched methods NEST (RefreshContainerSlots
        // [G :389] calls CanApplyAugumentation; SelectMutation calls CanApplyAugumentation/CanAffordMutation),
        // so the OUTERMOST scope owns the inject/remove — nested calls only bump the depth and must NOT clear
        // the pending removal. _depth==0 at a Prefix means this is the outermost scope.
        [ThreadStatic] private static int _depth;
        [ThreadStatic] private static List<TacticalItemDef> _injected;
        [ThreadStatic] private static HashSet<ItemDef> _injectedInto;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo prefix = AccessTools.Method(typeof(BionicsUnlockBypass), nameof(Bypass_Prefix));
                MethodInfo finalizer = AccessTools.Method(typeof(BionicsUnlockBypass), nameof(Bypass_Finalizer));
                foreach (string name in new[] { "RefreshContainerSlots", "CanApplyAugumentation", "OnMutationHover", "OnMutationHoverOut" })
                {
                    MethodInfo target = AccessTools.Method(typeof(UIModuleMutationSection), name);
                    if (target == null)
                    {
                        TheTurnedMain.LogWarn($"[TheTurned] BionicsUnlockBypass: UIModuleMutationSection.{name} not found — skipped.");
                        continue;
                    }
                    harmony.Patch(target, prefix: new HarmonyMethod(prefix), finalizer: new HarmonyMethod(finalizer));
                }
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] BionicsUnlockBypass: scoped unlock applied (no persisted-set mutation).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsUnlockBypass apply failed: " + e);
            }
        }

        private static void Bypass_Prefix(UIModuleMutationSection __instance)
        {
            int enteringDepth = _depth++;
            if (enteringDepth != 0)
            {
                return; // nested call: variants already injected by the outer scope — do nothing, do not clear.
            }
            // Outermost scope: start clean and (maybe) inject.
            _injected = null;
            _injectedInto = null;
            try
            {
                var context = ContextField?.GetValue(__instance) as ItemContext;
                if (context == null)
                {
                    return;
                }
                // Only for the recruit: read the section's selected actor (the character being augmented).
                if (!(context.GetCommonActor() is GeoCharacter ch) || !Phase4.IsPhase4Recruit(ch))
                {
                    return;
                }
                GeoFaction faction = context.GetFaction();
                HashSet<ItemDef> set = faction?.UnlockedAugmentations;
                if (set == null || !AugmentVariants.Ready)
                {
                    return;
                }
                List<TacticalItemDef> added = null;
                foreach (TacticalItemDef bp in AugmentVariants.AllVariantBodyparts)
                {
                    if (set.Add(bp)) // true only if it was NOT already present (so we remove exactly what we added)
                    {
                        (added ?? (added = new List<TacticalItemDef>())).Add(bp);
                    }
                }
                _injected = added;
                _injectedInto = set;
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsUnlockBypass Prefix threw: " + e);
            }
        }

        // Runs after the method (and any exception). Only the OUTERMOST scope (depth back to 0) removes the
        // variants it injected, restoring the persisted set. Returning null leaves any real exception to
        // propagate unchanged.
        private static Exception Bypass_Finalizer()
        {
            int remaining = --_depth;
            if (remaining < 0)
            {
                _depth = 0; // defensive: never go negative on an unexpected Finalizer-without-Prefix
                return null;
            }
            if (remaining != 0)
            {
                return null; // nested call returning — leave the outer scope's pending removal intact.
            }
            try
            {
                if (_injected != null && _injectedInto != null)
                {
                    foreach (TacticalItemDef bp in _injected)
                    {
                        _injectedInto.Remove(bp);
                    }
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsUnlockBypass Finalizer threw: " + e);
            }
            finally
            {
                _injected = null;
                _injectedInto = null;
            }
            return null;
        }
    }
}
