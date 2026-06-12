using HarmonyLib;
using PhoenixPoint.Common.View.ViewControllers;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using System;
using System.Linq;
using System.Reflection;
using TheTurned.Monsters.Arthron;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Marker-scopes the augment buttons. Postfix on EditUnitButtonsController.SetContextButtonVisibility
    /// (which ends by re-running SetEditUnitButtonsBasedOnType, so postfixing it runs AFTER the native
    /// visibility pass — our re-hide is not overwritten). For a marked recruit: HIDE MutationButton +
    /// BionicsButton, SHOW the DNA button. For everyone else: HIDE the DNA button (humans keep native
    /// buttons). Read each refresh, so switching to a human soldier restores the native buttons.
    /// </summary>
    internal static class AugmentButtonVisibilityPatch
    {
        internal const string PatchId = "Morgott.TheTurned.AugmentButtonVisibility";
        private static bool _applied;

        // EditUnitButtonsController._parentModule (UIModuleActorCycle) is private; read it once via reflection.
        private static readonly FieldInfo ParentModuleField =
            AccessTools.Field(typeof(EditUnitButtonsController), "_parentModule");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(EditUnitButtonsController),
                    nameof(EditUnitButtonsController.SetContextButtonVisibility));
                var postfix = AccessTools.Method(typeof(AugmentButtonVisibilityPatch), nameof(SetContextButtonVisibility_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] AugmentButtonVisibilityPatch: SetContextButtonVisibility Postfix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentButtonVisibilityPatch apply failed: " + e);
            }
        }

        private static void SetContextButtonVisibility_Postfix(EditUnitButtonsController __instance)
        {
            try
            {
                if (__instance == null || ParentModuleField == null)
                {
                    return;
                }
                var parentModule = ParentModuleField.GetValue(__instance) as UIModuleActorCycle;
                GeoCharacter current = parentModule != null ? parentModule.CurrentCharacter : null;
                bool isRecruit = Phase4.IsPhase4Recruit(current);

                if (isRecruit)
                {
                    // Re-hide both native augment buttons. These sit in their own circular wrappers, so the
                    // private SetCircularButtonVisibility body (parent.gameObject.SetActive) is correct here.
                    SetNativeButtonVisible(__instance.MutationButton, false);
                    SetNativeButtonVisible(__instance.BionicsButton, false);
                }
                // Show DNA only for a recruit who has ALREADY bought cell 1 (the NAV marker) — that purchase is
                // what unlocks the augment tree, so the DNA button must not appear before it. Match the NAV def
                // by name in the character's learned abilities (same lookup the cell buy-path uses). Hide it for
                // everyone else (humans untouched beyond their own button). The DNA button lives in its OWN
                // cloned wrapper (clone of the bionics wrapper) inside the augment cluster. Toggle THAT wrapper —
                // it is our object, so this never touches a shared container; consistent with the native
                // parent-toggle. AddAbility re-runs RefreshPanel -> panel rebuild -> this postfix, so the button
                // appears the instant cell 1 is bought (no screen re-entry).
                bool cell1Learned = isRecruit && current.Progression != null
                    && current.Progression.Abilities != null
                    && current.Progression.Abilities.Any(a => a != null && a.name == ArthronCellRow.NavAbilityName);
                GameObject dnaWrapper = AugmentButtonPatch.FindDnaWrapper(__instance);
                if (dnaWrapper != null)
                {
                    dnaWrapper.SetActive(cell1Learned);
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentButtonVisibilityPatch Postfix threw: " + e);
            }
        }

        // NATIVE buttons only: mirror of the private EditUnitButtonsController.SetCircularButtonVisibility
        // body (:397-401) — toggle the button's PARENT GameObject (its real circular wrapper). Safe because
        // each native button has its own wrapper. ResetButtonAnimations omitted (cosmetic; the native pass
        // already ran it and we only force-hide/show).
        private static void SetNativeButtonVisible(PhoenixGeneralButton button, bool isVisible)
        {
            if (button == null)
            {
                return;
            }
            Transform parent = button.transform.parent;
            if (parent != null)
            {
                parent.gameObject.SetActive(isVisible);
            }
        }
    }
}
