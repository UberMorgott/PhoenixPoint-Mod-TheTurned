using HarmonyLib;
using PhoenixPoint.Common.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewModules;
using System;
using System.Reflection;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// V1 Phase-2 crash guard for the augment cards. When a 6-card Crabman arm section is shown the screen
    /// activates <c>Container6</c> (unused by vanilla human bionics, ≤3 → Container3) and immediately calls
    /// <c>UIModuleMutationsContainer.InitView</c> -&gt; <c>UIModuleMutationsSlot.InitView</c>. On a freshly
    /// activated slot the private <c>_button</c> field (set in the slot's <c>Awake</c> via
    /// <c>GetComponent&lt;PhoenixGeneralButton&gt;()</c>) can still be null, so <c>InitView</c> NREs at
    /// <c>_button.SetEnabled(true)</c> (verified: real-DLL IL_0046-004D of
    /// <c>UIModuleMutationsSlot.InitView</c> = <c>ldfld _button; ldc.i4.1; callvirt SetEnabled</c>). TFTV's
    /// <c>InitPossibleMutations</c> replacement wraps the whole rebuild in a try/catch that logs (firing the
    /// "error in Terror from the Void" popup) and rethrows — so this vanilla NRE surfaces as a TFTV popup.
    ///
    /// FIX (data-faithful, vanilla-safe): Prefix <c>UIModuleMutationsSlot.InitView</c> — if <c>_button</c> is
    /// null, populate it exactly as <c>Awake</c> would (<c>GetComponent&lt;PhoenixGeneralButton&gt;()</c>).
    /// If the slot genuinely has no button, skip the body (return false) so nothing throws. Only ever ACTS
    /// when <c>_button</c> was null (the bug state) — a healthy slot is untouched, humans unaffected.
    /// </summary>
    internal static class BionicsSlotInitGuard
    {
        internal const string PatchId = "Morgott.TheTurned.BionicsSlotInitGuard";
        private static bool _applied;
        private static bool _warnedOnce;

        private static readonly FieldInfo ButtonField =
            AccessTools.Field(typeof(UIModuleMutationsSlot), "_button");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                MethodInfo target = AccessTools.Method(typeof(UIModuleMutationsSlot), "InitView");
                MethodInfo prefix = AccessTools.Method(typeof(BionicsSlotInitGuard), nameof(InitView_Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] BionicsSlotInitGuard: UIModuleMutationsSlot.InitView Prefix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSlotInitGuard apply failed: " + e);
            }
        }

        // Return false to SKIP the original (Harmony) — used only when the slot has no button at all.
        private static bool InitView_Prefix(UIModuleMutationsSlot __instance)
        {
            try
            {
                if (__instance == null || ButtonField == null)
                {
                    return true;
                }
                if (ButtonField.GetValue(__instance) != null)
                {
                    return true; // healthy slot — run the original unchanged
                }
                // _button null: Awake hasn't run yet. Populate it exactly as Awake does (same GameObject).
                var button = __instance.GetComponent<PhoenixGeneralButton>();
                if (button != null)
                {
                    ButtonField.SetValue(__instance, button);
                    if (!_warnedOnce)
                    {
                        _warnedOnce = true;
                        TheTurnedMain.LogInfo("[TheTurned] BionicsSlotInitGuard: populated a null slot _button before InitView (Awake-not-yet-run).");
                    }
                    return true; // now safe — run the original
                }
                // No button component at all — skip the body so vanilla InitView cannot NRE.
                TheTurnedMain.LogWarn("[TheTurned] BionicsSlotInitGuard: slot has no PhoenixGeneralButton — skipping InitView to avoid NRE.");
                return false;
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] BionicsSlotInitGuard Prefix threw: " + e);
                return true;
            }
        }
    }
}
