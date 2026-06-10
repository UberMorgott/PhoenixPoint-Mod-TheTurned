using System;
using System.Reflection;
using Base.UI;
using HarmonyLib;
using PhoenixPoint.Geoscape.View.ViewControllers;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Phase-4: backfills the mutoid popup's null prefab-template fields so row/cell overflow can
    /// instantiate. <c>SpecializedAbilityTrackPopupElement.Init</c> calls
    /// <c>UIUtil.EnsureActiveComponentsInContainer</c> twice — rows (SpecContainer, template
    /// <c>AbilityRowPrefab</c>, count = AvailablePandoranSpecialzations.Count, decompile :107) and
    /// cells (row.transform, template <c>AbilityPrefab</c>, count = maxLevel, :112). Instantiate
    /// fires only when count exceeds the pre-placed children (UIUtil.cs:64-82); vanilla never
    /// overflows, but our 6 fed rows do, and the shipped MutoidSkillContainer leaves the template
    /// fields null → ArgumentException (Instantiate null).
    /// Prefix on Init: if a template is null, assign it from an existing pre-placed child — exactly
    /// what EnsureActiveComponentsInContainer's reuse branch treats those children as anyway; cell
    /// handler wiring stays safe because Init Delegate.Remove's before Combine (:115-120) and Unity
    /// does not serialize the Action fields on clones. Fires only when a template is actually null
    /// (covers any pandoran popup that overflows due to OUR fed rows); logs the null-state once.
    /// Graceful-disable on unresolved target (DevUnlockPatch pattern).
    /// </summary>
    internal static class PopupPrefabPatch
    {
        private static bool _applied;
        private static bool _stateLogged;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(SpecializedAbilityTrackPopupElement),
                nameof(SpecializedAbilityTrackPopupElement.Init));
            if (target == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Popup prefab patch: Init unresolved — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(PopupPrefabPatch), nameof(InitPrefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Popup prefab-template patch applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] Popup prefab patch failed: " + e);
            }
        }

        public static void InitPrefix(SpecializedAbilityTrackPopupElement __instance)
        {
            try
            {
                if (!_stateLogged)
                {
                    TheTurnedMain.LogInfo("[TheTurned] Popup templates: "
                        + $"AbilityRowPrefab={__instance.AbilityRowPrefab != null}, "
                        + $"AbilityPrefab={__instance.AbilityPrefab != null}.");
                    _stateLogged = true;
                }
                if (__instance.AbilityRowPrefab == null && __instance.SpecContainer != null)
                {
                    StaticHorizontalLayoutGroup row =
                        FirstChildComponent<StaticHorizontalLayoutGroup>(__instance.SpecContainer);
                    if (row != null)
                    {
                        __instance.AbilityRowPrefab = row;
                        TheTurnedMain.LogInfo("[TheTurned] AbilityRowPrefab was null — assigned from a pre-placed SpecContainer row.");
                    }
                    else
                    {
                        TheTurnedMain.LogWarn("[TheTurned] AbilityRowPrefab null and no pre-placed row found — row overflow would throw.");
                    }
                }
                if (__instance.AbilityPrefab == null && __instance.SpecContainer != null)
                {
                    AbilityTrackSkillEntryElement cell = null;
                    foreach (Transform row in __instance.SpecContainer)
                    {
                        cell = FirstChildComponent<AbilityTrackSkillEntryElement>(row);
                        if (cell != null)
                        {
                            break;
                        }
                    }
                    if (cell != null)
                    {
                        __instance.AbilityPrefab = cell;
                        TheTurnedMain.LogInfo("[TheTurned] AbilityPrefab was null — assigned from a pre-placed row cell.");
                    }
                    else
                    {
                        TheTurnedMain.LogWarn("[TheTurned] AbilityPrefab null and no pre-placed cell found — cell overflow would throw.");
                    }
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] Popup prefab backfill failed: " + e.Message);
            }
        }

        /// <summary>First direct child carrying T — same child scan EnsureActiveComponentsInContainer
        /// uses to collect reusable elements (UIUtil.cs:55-63).</summary>
        private static T FirstChildComponent<T>(Transform container) where T : Component
        {
            foreach (Transform child in container)
            {
                T component = child.GetComponent<T>();
                if (component != null)
                {
                    return component;
                }
            }
            return null;
        }
    }
}
