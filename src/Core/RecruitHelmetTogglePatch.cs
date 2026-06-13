using HarmonyLib;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace TheTurned.Core
{
    /// <summary>
    /// Hides BOTH "hide-helmet" controls for a turned recruit (the one that pops the Arthron head out of
    /// its carapace):
    ///
    ///  1. Native <see cref="UIModuleSoldierCustomization.HideHelmetToggle"/> (Toggle, decompile
    ///     UIModuleSoldierCustomization.cs:26) — hidden in a Postfix of <c>OnNewCharacter</c> (cs:74),
    ///     gated on <see cref="Phase4.IsPhase4Recruit"/>.
    ///
    ///  2. TFTV's custom helmet button <c>TFTVUI.Personnel.Loadouts.HelmetToggle</c> (a static
    ///     <c>PhoenixGeneralButton</c>; TFTV Loadouts.cs:33). TFTV re-shows it from its OWN Postfix on
    ///     <c>UIModuleActorCycle.SetContextButtonsBasedOnType</c> (Loadouts.cs:241-255 →
    ///     ShowAndHideHelmetButton :196), which runs AFTER the inner SetContextButtonVisibility pass. So we
    ///     re-hide it from a Postfix on the SAME method, ordered <c>HarmonyAfter("phoenixrising.tftv")</c>
    ///     so our hide is the last word. Accessed entirely via reflection (TFTV internal) and fully
    ///     null-safe → no-op when TFTV is absent.
    /// </summary>
    internal static class RecruitHelmetTogglePatch
    {
        internal const string PatchId = "Morgott.TheTurned.RecruitHelmetToggle";
        private const string TftvHarmonyId = "phoenixrising.tftv";
        private static bool _applied;

        // Resolved once, lazily: TFTV Loadouts.HelmetToggle static field (PhoenixGeneralButton).
        private static bool _tftvProbed;
        private static FieldInfo _tftvHelmetToggleField;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var onNewChar = AccessTools.Method(typeof(UIModuleSoldierCustomization),
                    nameof(UIModuleSoldierCustomization.OnNewCharacter));
                var setContext = AccessTools.Method(typeof(UIModuleActorCycle),
                    nameof(UIModuleActorCycle.SetContextButtonsBasedOnType));

                if (onNewChar != null)
                {
                    harmony.Patch(onNewChar,
                        postfix: new HarmonyMethod(typeof(RecruitHelmetTogglePatch), nameof(OnNewCharacter_Postfix)));
                }
                else
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch: OnNewCharacter unresolved — native helmet toggle not hidden.");
                }

                if (setContext != null)
                {
                    var postfix = new HarmonyMethod(typeof(RecruitHelmetTogglePatch), nameof(SetContextButtonsBasedOnType_Postfix))
                    {
                        after = new[] { TftvHarmonyId }   // run AFTER TFTV's own helmet-show postfix
                    };
                    harmony.Patch(setContext, postfix: postfix);
                }
                else
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch: SetContextButtonsBasedOnType unresolved — TFTV helmet button not hidden.");
                }

                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] RecruitHelmetTogglePatch applied (native + TFTV helmet toggles recruit-hidden).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch apply failed: " + e);
            }
        }

        // (1) Native Toggle — hide its own GameObject for a recruit (auto-restores for humans: postfix
        // re-runs on every character switch, and we only act when IsPhase4Recruit is true).
        private static void OnNewCharacter_Postfix(UIModuleSoldierCustomization __instance, GeoCharacter newCharacter)
        {
            try
            {
                if (__instance == null || __instance.HideHelmetToggle == null)
                {
                    return;
                }
                bool isRecruit = Phase4.IsPhase4Recruit(newCharacter);
                // Only force-hide for the recruit; leave the native show/enable logic intact for humans.
                if (isRecruit)
                {
                    __instance.HideHelmetToggle.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch OnNewCharacter_Postfix threw: " + e);
            }
        }

        // (2) TFTV button — re-hide for a recruit AFTER TFTV's show-postfix ran.
        private static void SetContextButtonsBasedOnType_Postfix(UIModuleActorCycle __instance)
        {
            try
            {
                if (__instance == null || !Phase4.IsPhase4Recruit(__instance.CurrentCharacter))
                {
                    return;
                }
                var helmetButton = GetTftvHelmetToggle();
                HideButtonWrapper(helmetButton);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch SetContextButtonsBasedOnType_Postfix threw: " + e);
            }
        }

        /// <summary>Reflectively read the TFTV static <c>Loadouts.HelmetToggle</c>; null when TFTV absent.</summary>
        private static object GetTftvHelmetToggle()
        {
            if (!_tftvProbed)
            {
                _tftvProbed = true;
                try
                {
                    Type loadouts = AccessTools.TypeByName("TFTV.Loadouts")
                        ?? AccessTools.TypeByName("TFTVUI.Personnel.Loadouts");
                    if (loadouts != null)
                    {
                        _tftvHelmetToggleField = AccessTools.Field(loadouts, "HelmetToggle");
                    }
                }
                catch (Exception e)
                {
                    TheTurnedMain.LogWarn("[TheTurned] RecruitHelmetTogglePatch: TFTV Loadouts.HelmetToggle probe failed: " + e.Message);
                }
            }
            return _tftvHelmetToggleField?.GetValue(null);
        }

        /// <summary>Hide a button by toggling its parent wrapper GameObject (mirror of the vanilla
        /// SetCircularButtonVisibility / TFTV SetButtonVisibility convention). Reflection-tolerant: accepts
        /// any object exposing a <c>transform</c>.</summary>
        private static void HideButtonWrapper(object button)
        {
            if (button == null)
            {
                return;
            }
            Component component = button as Component;
            if (component == null)
            {
                return;
            }
            Transform parent = component.transform != null ? component.transform.parent : null;
            if (parent != null)
            {
                parent.gameObject.SetActive(false);
            }
        }
    }
}
