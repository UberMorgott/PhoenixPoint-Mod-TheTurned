using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.View.ViewModules;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Progression;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Linq;
using System.Reflection;
using TheTurned.Monsters.Arthron;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// CHUNK B — per-row currency + cell-1 nav for the recruit's 2-row mutoid panel. Prefix on
    /// <c>MutoidAbilityTrackContainerElement.OnTrackSlotPointerClicked</c> (decompile :99-106, which
    /// unconditionally opens the Mutagen popup). For a Phase-4 recruit, the TOP row (Source==SecondaryClass =
    /// our 5 evolution cells, hosted by SpecRowFactory.HostCellsInSecondaryTrack) is intercepted:
    ///   - cell 1 (NAV marker) -> open the augment/Bionics screen (EditUnitButtonsController.GoToBionicsScreen,
    ///     the same entry the DNA button uses, AugmentButtonPatch.cs:154); no purchase.
    ///   - cells 2-5 -> SOLDIER-STYLE in-panel SkillPoints purchase (NOT Mutagen): level-gated by the adjusted
    ///     level (cell N at level N, UICharacterProgressionUtl.GetAbilityAdjustedLevel skips the dual-spec
    ///     spacer, decompile :10-25), afforded by SkillPoints (+ faction skillpoints), deducted with the native
    ///     non-pandoran ConsumeAbilityCost idiom (UIModuleCharacterProgression.cs:452-458), learned via
    ///     AddAbility (fires OnAbilityAdded -> CellArmorApply applies cells 2/4 armor, M2), then the panel is
    ///     refreshed (CommitStatChanges + SetAbilityTracks + RefreshStatPanel, mirroring BuyAbility :422-437).
    /// The BOTTOM purple Personal row falls through to the original (Mutagen popup) untouched.
    ///
    /// We cannot reuse the module's own BuyAbility(): it branches on _hasPandoranProgression (TRUE for our
    /// mutoid-container recruit) and would take the Mutagen path. So we replicate the non-pandoran branch here.
    /// Module-instance + private fields obtained via FindObjectOfType + AccessTools (the panel is open while the
    /// click fires). All names verified against the decompile (see member comments).
    /// </summary>
    internal static class CellRowPurchasePatch
    {
        internal const string PatchId = "Morgott.TheTurned.CellRowPurchase";
        private static bool _applied;

        // Container's owning character [G AbilityTrackContainerElement.cs:57] (same reflect as RowRenderClampPatch).
        private static readonly FieldInfo ContainerCharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        // Module private state [G UIModuleCharacterProgression.cs:230,232,252].
        private static readonly FieldInfo CurrentSkillPointsField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_currentSkillPoints");
        private static readonly FieldInfo CurrentFactionPointsField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_currentFactionPoints");
        // Private refresh method [G :845]; RefreshStatPanel is public [G :614]; CommitStatChanges public [G :384].
        private static readonly MethodInfo SetAbilityTracksMethod =
            AccessTools.Method(typeof(UIModuleCharacterProgression), "SetAbilityTracks");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(MutoidAbilityTrackContainerElement), "OnTrackSlotPointerClicked");
            if (target == null || ContainerCharacterField == null || CurrentSkillPointsField == null
                || CurrentFactionPointsField == null || SetAbilityTracksMethod == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellRowPurchasePatch: target/fields unresolved "
                    + $"(target={target != null} charField={ContainerCharacterField != null} "
                    + $"sp={CurrentSkillPointsField != null} fp={CurrentFactionPointsField != null} "
                    + $"setTracks={SetAbilityTracksMethod != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target, prefix: new HarmonyMethod(typeof(CellRowPurchasePatch), nameof(OnTrackSlotPointerClicked_Prefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] CellRowPurchasePatch applied (top-row SP purchase + cell-1 nav).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] CellRowPurchasePatch failed: " + e);
            }
        }

        // Return false = skip the original (no Mutagen popup). Return true = vanilla (Personal -> popup).
        // Arg names bind the original (button, source, slot, isDualClassSlot).
        public static bool OnTrackSlotPointerClicked_Prefix(MutoidAbilityTrackContainerElement __instance,
            AbilityTrackSkillEntryElement button, AbilityTrackSource source, AbilityTrackSlot slot, bool isDualClassSlot)
        {
            try
            {
                GeoCharacter character = ContainerCharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character) || slot == null)
                {
                    return true; // not our recruit — vanilla
                }
                // Only intercept the TOP row (the 5 cells hosted in the SecondaryClass track). Bottom purple
                // Personal row keeps the native Mutagen-popup behavior.
                if (slot.AbilityTrack == null || slot.AbilityTrack.Source != AbilityTrackSource.SecondaryClass)
                {
                    return true;
                }
                if (button != null && button.LockedSkill)
                {
                    return false; // locked cell — swallow (no popup), matches vanilla "do nothing when locked"
                }
                TacticalAbilityDef ability = slot.Ability;
                if (ability == null)
                {
                    return false; // empty cell — nothing to buy / no popup
                }

                // CELL 1 — NAV: open the augment/Bionics screen, no purchase.
                if (ability.name == ArthronCellRow.NavAbilityName)
                {
                    OpenBionicsScreen();
                    return false;
                }

                // Already learned -> no re-buy. (Re-clicking a learned cell is a no-op even in DEV mode;
                // recruit a fresh Arthron to retest a cell from scratch.)
                if (character.Progression.Abilities.Contains(ability))
                {
                    return false;
                }

                UIModuleCharacterProgression module = UnityEngine.Object.FindObjectOfType<UIModuleCharacterProgression>();
                if (module == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] CellBuy: UIModuleCharacterProgression not found — purchase aborted.");
                    return false;
                }

                // DEV-TESTING: when DevUnlockAllLevels is on, every top cell is unlocked AND FREE — skip the
                // level gate, the affordance check, and the SkillPoint deduction; just learn it (AddAbility
                // still fires OnAbilityAdded so armor/stat effects apply) and refresh the panel. Lets the user
                // click each cell to preview its model effect without spending SP.
                if (Phase4.DevUnlockAllLevels)
                {
                    character.Progression.AddAbility(ability);
                    RefreshPanel(module);
                    TheTurnedMain.LogInfo($"[TheTurned] CellBuy(DEV-FREE): learned '{ability.name}' (no cost) on '{character.GetName()}'.");
                    return false;
                }

                // REAL purchase path (DevUnlockAllLevels == false). DevUnlockAllLevels is a compile-time const,
                // so while it is true this block const-folds to unreachable; the pragma keeps the build clean
                // and the code reactivates verbatim when the const is flipped to false (the real Chunk-B path).
#pragma warning disable CS0162 // Unreachable code (const-folded DevUnlockAllLevels free-mode switch — intentional)
                // LEVEL GATE: adjusted level (skips the dual-spec spacer) == "cell N at level N".
                int requiredLevel = UICharacterProgressionUtl.GetAbilityAdjustedLevel(character, slot, skipDualSpec: true);
                int charLevel = character.Progression.LevelProgression.Level;
                if (charLevel < requiredLevel)
                {
                    TheTurnedMain.LogInfo($"[TheTurned] CellBuy: '{ability.name}' locked (needs level {requiredLevel}, char level {charLevel}).");
                    return false;
                }

                // AFFORD: SkillPoints (+ faction skillpoints), native non-pandoran rule (CanAffordSkill :1075).
                int cost = character.Progression.GetAbilitySlotCost(slot);   // = Ability.CharacterProgressionData.SkillPointCost [G :296-298]
                int sp = (int)CurrentSkillPointsField.GetValue(module);
                int fp = (int)CurrentFactionPointsField.GetValue(module);
                if (sp + fp < cost)
                {
                    TheTurnedMain.LogInfo($"[TheTurned] CellBuy: '{ability.name}' not affordable (cost {cost}, have SP {sp} + faction {fp}).");
                    return false;
                }

                // DEDUCT: native ConsumeAbilityCost non-pandoran branch (UIModuleCharacterProgression.cs:452-458).
                sp -= cost;
                if (sp < 0)
                {
                    fp -= -sp;
                    sp = 0;
                }
                CurrentSkillPointsField.SetValue(module, sp);
                CurrentFactionPointsField.SetValue(module, fp);

                // LEARN: AddAbility fires OnAbilityAdded -> CellArmorApply (cells 2/4 armor). Mirrors BuyAbility's
                // non-pandoran LearnAbility (:433) but skips its raw-level IsAbilitySlotAvailable LogError (we
                // already gated on the ADJUSTED level; raw level = adjusted+1 for post-spacer cells).
                character.Progression.AddAbility(ability);

                // COMMIT + REFRESH: flush SP to progression/faction, rebuild the panel (BuyAbility :422,436,437).
                module.CommitStatChanges();
                RefreshPanel(module);

                TheTurnedMain.LogInfo($"[TheTurned] CellBuy: learned '{ability.name}' for {cost} SP (now SP {sp} + faction {fp}) on '{character.GetName()}'.");
                return false;
#pragma warning restore CS0162
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellRowPurchasePatch prefix threw (falling back to vanilla): " + e);
                return true;
            }
        }

        // Rebuild the progression panel after a learn (mirrors BuyAbility :436-437): public RefreshStatPanel
        // + the private SetAbilityTracks (reflected) so the cell flips to "learned" and counters update.
        private static void RefreshPanel(UIModuleCharacterProgression module)
        {
            module.RefreshStatPanel();
            SetAbilityTracksMethod.Invoke(module, null);
        }

        // Open the augment/Bionics screen via the same public entry the DNA button uses
        // (EditUnitButtonsController.GoToBionicsScreen, AugmentButtonPatch.cs:154). The controller is active on
        // the recruit's edit screen while the progression panel is shown.
        private static void OpenBionicsScreen()
        {
            EditUnitButtonsController controller = UnityEngine.Object.FindObjectOfType<EditUnitButtonsController>();
            if (controller == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellNav: EditUnitButtonsController not found — cannot open Bionics screen.");
                return;
            }
            TheTurnedMain.LogInfo("[TheTurned] CellNav: cell-1 clicked — opening Bionics/augment screen.");
            controller.GoToBionicsScreen();
        }
    }
}
