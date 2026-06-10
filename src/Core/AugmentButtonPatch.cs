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
    /// Adds a custom "DNA" button to the soldier edit screen by cloning the BIONICS button's WRAPPER (the
    /// per-button circular-slot object = BionicsButton.transform.parent) into the SAME cluster container, so
    /// the DNA button inherits the correct circular-slot layout and sits next to the АУГМЕНТ/Bionics button.
    /// (Earlier we cloned EditButton bare into the controller root — no wrapper → wrong position.) The button
    /// opens the vanilla Bionics screen for the recruit (Phase-1 placeholder entry — the recruit passes the
    /// ToBionicsState CheckIsHuman gate via HumanClassificationPatch). Created once per controller; shown only
    /// for the marked recruit by AugmentButtonVisibilityPatch. Icon = a temporary placeholder until DNA art ships.
    /// </summary>
    internal static class AugmentButtonPatch
    {
        internal const string PatchId = "Morgott.TheTurned.AugmentButton";
        private static bool _applied;

        // Temporary placeholder art under Assets\Textures\. No DNA png ships yet → reuse an existing mod
        // icon; the Icons loader no-ops (keeps the cloned icon) if the file is absent.
        // TODO replace with DNA art (user generates PP icons via ComfyUI separately).
        private const string DnaIconFile = "Arthron_NaturalArmour.png";

        // Per-controller guard: the cloned WRAPPER GameObject is named with this marker so a re-Awake (or a
        // second Postfix) does not stack duplicate buttons, and the visibility patch can find it.
        private const string DnaButtonName = "TheTurned_DnaButtonWrapper";

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

        /// <summary>
        /// Find the DNA WRAPPER GameObject previously created on this controller (or null). Searches the whole
        /// controller subtree by marker name so it does not assume a fixed cluster path.
        /// </summary>
        internal static GameObject FindDnaWrapper(EditUnitButtonsController controller)
        {
            if (controller == null)
            {
                return null;
            }
            foreach (Transform t in controller.GetComponentsInChildren<Transform>(true))
            {
                if (t != null && t.name == DnaButtonName)
                {
                    return t.gameObject;
                }
            }
            return null;
        }

        private static void Awake_Postfix(EditUnitButtonsController __instance)
        {
            try
            {
                if (__instance == null)
                {
                    return;
                }

                // ── Diagnostic: log the live hierarchy + layout of the three context buttons so placement can
                //    be verified and tuned precisely. (button → wrapper(parent) → cluster(wrapper.parent),
                //    plus anchoredPosition + sibling index for button and wrapper.)
                LogButtonHierarchy("EditButton", __instance.EditButton);
                LogButtonHierarchy("MutationButton", __instance.MutationButton);
                LogButtonHierarchy("BionicsButton", __instance.BionicsButton);

                if (__instance.BionicsButton == null || FindDnaWrapper(__instance) != null)
                {
                    return;
                }

                Transform bionicsWrapper = __instance.BionicsButton.transform.parent;
                if (bionicsWrapper == null || bionicsWrapper.parent == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] DNA button: BionicsButton has no wrapper/cluster — cannot place.");
                    return;
                }
                Transform cluster = bionicsWrapper.parent;

                // Clone the bionics WRAPPER into the same cluster → inherits the circular-slot layout. Place it
                // immediately after the bionics slot in sibling order (a layout group, if any, slots it next to
                // АУГМЕНТ; absolute layout will be tuned from the diagnostic on the next pass).
                GameObject dnaWrapper = UnityEngine.Object.Instantiate(bionicsWrapper.gameObject, cluster);
                dnaWrapper.name = DnaButtonName;
                dnaWrapper.transform.SetSiblingIndex(bionicsWrapper.GetSiblingIndex() + 1);

                PhoenixGeneralButton dna = dnaWrapper.GetComponentInChildren<PhoenixGeneralButton>(true);
                if (dna == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] DNA button: cloned wrapper has no PhoenixGeneralButton — aborting.");
                    UnityEngine.Object.Destroy(dnaWrapper);
                    return;
                }

                // Swap the child "UI_Icon" sprite (stock-Unity descendant walk — GetChildren() is a TFTV
                // extension, not stock). No-op if the placeholder file is absent (keeps the cloned icon).
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
                    TheTurnedMain.LogInfo("[TheTurned] DNA button: placeholder icon absent — using cloned icon (replace with DNA art).");
                }

                // Tooltip. UITooltipText lives in the global namespace (no using needed); TipText is public.
                // The clone may carry the bionics tooltip — reuse it if present, else add one.
                UITooltipText tip = dna.gameObject.GetComponent<UITooltipText>() ?? dna.gameObject.AddComponent<UITooltipText>();
                tip.TipText = "DNA";

                // Rebind click → open the Bionics screen (Phase-1 placeholder entry). Clear first: the clone may
                // carry the bionics button's PointerClicked (GoToBionicsScreen) — reset to avoid a double-invoke,
                // then bind our own logged handler. GoToBionicsScreen() is PUBLIC and uses the controller's own
                // _context / _parentModule internally, so no reflection is needed.
                dna.PointerClicked = null;
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

                TheTurnedMain.LogInfo($"[TheTurned] DNA button created (wrapper clone of '{bionicsWrapper.name}') in cluster '{cluster.name}' at siblingIndex {dnaWrapper.transform.GetSiblingIndex()}.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentButtonPatch Awake_Postfix threw: " + e);
            }
        }

        // One diagnostic line per context button: name, wrapper(parent) name, cluster(wrapper.parent) name,
        // button + wrapper anchoredPosition, and wrapper sibling index. Lets us read the real slot layout.
        private static void LogButtonHierarchy(string label, PhoenixGeneralButton button)
        {
            if (button == null)
            {
                TheTurnedMain.LogInfo($"[TheTurned] HIER {label}: <null>");
                return;
            }
            Transform t = button.transform;
            Transform wrapper = t.parent;
            Transform cluster = wrapper != null ? wrapper.parent : null;
            Vector2 btnPos = (t as RectTransform)?.anchoredPosition ?? Vector2.zero;
            Vector2 wrapPos = (wrapper as RectTransform)?.anchoredPosition ?? Vector2.zero;
            int wrapSibling = wrapper != null ? wrapper.GetSiblingIndex() : -1;
            TheTurnedMain.LogInfo(
                $"[TheTurned] HIER {label}: button='{button.name}' anchoredPos={btnPos} | "
                + $"wrapper='{(wrapper != null ? wrapper.name : "<null>")}' wrapperAnchoredPos={wrapPos} wrapperSibling={wrapSibling} | "
                + $"cluster='{(cluster != null ? cluster.name : "<null>")}'");
        }
    }
}
