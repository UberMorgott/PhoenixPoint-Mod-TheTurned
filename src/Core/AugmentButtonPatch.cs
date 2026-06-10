using HarmonyLib;
using PhoenixPoint.Common.View.ViewControllers;
using PhoenixPoint.Common.View.ViewModules;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TheTurned.Core
{
    /// <summary>
    /// Adds a custom "DNA" button to the soldier edit screen by cloning EditButton (production-proven TFTV
    /// recipe, Loadouts.cs:309-367). The button opens the vanilla Bionics screen for the recruit (Phase-1
    /// placeholder entry — the recruit passes the ToBionicsState CheckIsHuman gate via
    /// HumanClassificationPatch). Created once per controller; shown only for the marked recruit by
    /// AugmentButtonVisibilityPatch. Icon = a temporary placeholder until DNA art ships.
    /// </summary>
    internal static class AugmentButtonPatch
    {
        internal const string PatchId = "Morgott.TheTurned.AugmentButton";
        private static bool _applied;

        // Temporary placeholder art under Assets\Textures\. No DNA png ships yet → reuse an existing mod
        // icon; the Icons loader no-ops (keeps the cloned EditButton icon) if the file is absent.
        // TODO replace with DNA art (user generates PP icons via ComfyUI separately).
        private const string DnaIconFile = "Arthron_NaturalArmour.png";

        // Per-controller guard: store the created button on the controller's GameObject via a marker name
        // so a re-Awake (or a second Postfix) does not stack duplicate buttons.
        private const string DnaButtonName = "TheTurned_DnaButton";

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(EditUnitButtonsController), nameof(EditUnitButtonsController.Awake));
                var postfix = AccessTools.Method(typeof(AugmentButtonPatch), nameof(Awake_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] AugmentButtonPatch: EditUnitButtonsController.Awake Postfix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentButtonPatch apply failed: " + e);
            }
        }

        /// <summary>Find the DNA button previously created on this controller (or null).</summary>
        internal static PhoenixGeneralButton FindDnaButton(EditUnitButtonsController controller)
        {
            if (controller == null)
            {
                return null;
            }
            Transform existing = controller.transform.Find(DnaButtonName);
            return existing != null ? existing.GetComponent<PhoenixGeneralButton>() : null;
        }

        private static void Awake_Postfix(EditUnitButtonsController __instance)
        {
            try
            {
                if (__instance == null || __instance.EditButton == null || FindDnaButton(__instance) != null)
                {
                    return;
                }

                PhoenixGeneralButton dna = UnityEngine.Object.Instantiate(__instance.EditButton, __instance.transform);
                dna.gameObject.name = DnaButtonName;

                // Reposition relative to EditButton (offset to the left, matching the TFTV recipe pattern).
                Resolution res = Screen.currentResolution;
                float fx = res.width / 1920f;
                float fy = res.height / 1080f;
                dna.transform.position += new Vector3(-50f * fx, -35f * fy, 0f);

                // Tooltip. UITooltipText lives in the global namespace (no using needed); TipText is public.
                dna.gameObject.AddComponent<UITooltipText>().TipText = "DNA";

                // Swap the child "UI_Icon" sprite (stock-Unity descendant walk — GetChildren() is a TFTV
                // extension, not stock). No-op if the placeholder file is absent (keeps the EditButton icon).
                Sprite sprite = Icons.CreateSpriteFromImageFile(DnaIconFile);
                if (sprite != null)
                {
                    Image icon = dna.GetComponentsInChildren<Image>(true)
                        .FirstOrDefault(img => img != null && img.gameObject.name == "UI_Icon");
                    if (icon != null)
                    {
                        icon.sprite = sprite;
                    }
                    else
                    {
                        TheTurnedMain.LogWarn("[TheTurned] DNA button: 'UI_Icon' Image child not found — icon not swapped.");
                    }
                }
                else
                {
                    TheTurnedMain.LogInfo("[TheTurned] DNA button: placeholder icon absent — using cloned EditButton icon (replace with DNA art).");
                }

                // Bind click → open the Bionics screen for the current character (Phase-1 placeholder entry).
                // GoToBionicsScreen() is PUBLIC on EditUnitButtonsController and uses the controller's own
                // _context / _parentModule internally, so no reflection is needed here.
                dna.PointerClicked += () =>
                {
                    try
                    {
                        TheTurnedMain.LogInfo("[TheTurned] DNA button clicked — opening Bionics screen.");
                        __instance.GoToBionicsScreen();
                    }
                    catch (Exception ce)
                    {
                        TheTurnedMain.LogWarn("[TheTurned] DNA button click failed: " + ce);
                    }
                };

                TheTurnedMain.LogInfo("[TheTurned] DNA button created on EditUnitButtonsController.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentButtonPatch Awake_Postfix threw: " + e);
            }
        }
    }
}
