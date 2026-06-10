using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewControllers.Progression;
using PhoenixPoint.Geoscape.View.ViewControllers.Roster;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// CHUNK B blocker fix — make the TOP (SecondaryClass) mutoid cells INTERACTIVE and show a non-Mutagen
    /// tooltip. Root cause (decompile):
    ///   * MutoidAbilityTrackContainerElement.SetAbilitySlot [G :64-68]: for a slot WITH an Ability it calls
    ///     element.SetSkill(..., isBuyable:false). AbilityTrackSkillEntryElement.OnPointerClick [G :153-160]
    ///     only dispatches TrackSlotPointerClick when IsBuyableSkill==true -> the click is DEAD before it ever
    ///     reaches OnTrackSlotPointerClicked (and our CellRowPurchasePatch Prefix). => "not clickable".
    ///   * UIModuleCharacterProgression.OnTrackSlotPointerEnter [G :996-998]: with _hasPandoranProgression
    ///     (TRUE for our mutoid recruit) it shows the tooltip with useMutagens:true. => "costs Mutagen".
    ///
    /// Fixes (Phase-4 recruit + SecondaryClass-source scoped):
    ///   (A) Postfix SetAbilitySlot -> re-set our cells as BUYABLE (and unlocked when level-met / always in
    ///       DevUnlockAllLevels) so OnPointerClick dispatches and the click reaches CellRowPurchasePatch.
    ///   (B) Prefix OnTrackSlotPointerEnter -> show the SP tooltip (useMutagens:false; cost 0 in dev) and skip
    ///       the Mutagen path for our top cells.
    /// Release behavior intact: locked cells (release, char level &lt; adjusted cell level) stay isBuyable:false
    /// (not clickable) and show the SP cost.
    /// </summary>
    internal static class CellRowInteractivityPatch
    {
        internal const string PatchId = "Morgott.TheTurned.CellRowInteractivity";
        private static bool _applied;

        private static readonly FieldInfo ContainerCharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        private static readonly FieldInfo ModuleCharacterField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_character");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo setSlot = AccessTools.Method(typeof(MutoidAbilityTrackContainerElement), "SetAbilitySlot",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource), typeof(AbilityTrackSlot), typeof(AbilityTrackSkillEntryElement) });
            MethodInfo enter = AccessTools.Method(typeof(UIModuleCharacterProgression), "OnTrackSlotPointerEnter",
                new[] { typeof(AbilityTrackSlot), typeof(bool) });
            if (setSlot == null || enter == null || ContainerCharacterField == null || ModuleCharacterField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellRowInteractivityPatch: target(s)/field(s) unresolved "
                    + $"(setSlot={setSlot != null} enter={enter != null} contChar={ContainerCharacterField != null} "
                    + $"modChar={ModuleCharacterField != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(setSlot, postfix: new HarmonyMethod(typeof(CellRowInteractivityPatch), nameof(SetAbilitySlot_Postfix)));
                harmony.Patch(enter, prefix: new HarmonyMethod(typeof(CellRowInteractivityPatch), nameof(OnTrackSlotPointerEnter_Prefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] CellRowInteractivityPatch applied (top-row buyable + SP tooltip).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] CellRowInteractivityPatch failed: " + e);
            }
        }

        // (A) After the mutoid container renders a cell, make OUR top-row (SecondaryClass) ability cells buyable
        // so OnPointerClick dispatches. Locked iff (release AND char level < adjusted cell level); dev = never.
        public static void SetAbilitySlot_Postfix(MutoidAbilityTrackContainerElement __instance,
            AbilityTrackSource abilitySource, AbilityTrackSlot slot, AbilityTrackSkillEntryElement element)
        {
            try
            {
                if (abilitySource != AbilityTrackSource.SecondaryClass || slot?.Ability == null || element == null)
                {
                    return;
                }
                GeoCharacter character = ContainerCharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character))
                {
                    return;
                }
                // Already learned -> leave the engine's KnownSkill render (not re-buyable).
                if (character.Progression.Abilities.Contains(slot.Ability))
                {
                    return;
                }
                bool locked = !Phase4.DevUnlockAllLevels
                    && character.Progression.LevelProgression.Level
                       < UICharacterProgressionUtl.GetAbilityAdjustedLevel(character, slot, skipDualSpec: true);
                // isAvailable/isBuyable = !locked: buyable cells dispatch OnPointerClick -> our purchase Prefix.
                element.SetSkill(abilitySource, slot, slot.Ability.ViewElementDef.SmallIcon,
                    isLocked: locked, isAvailable: !locked, isBuyable: !locked);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellRowInteractivity SetAbilitySlot_Postfix threw: " + e);
            }
        }

        // (B) Replace the hover tooltip for our top-row cells: SP cost (useMutagens:false), 0 in dev. Skip the
        // native Mutagen tooltip path. Return false only when we handled it.
        public static bool OnTrackSlotPointerEnter_Prefix(UIModuleCharacterProgression __instance,
            AbilityTrackSlot slot, bool isDualClassSlot)
        {
            try
            {
                if (slot?.Ability == null || slot.AbilityTrack == null
                    || slot.AbilityTrack.Source != AbilityTrackSource.SecondaryClass)
                {
                    return true; // not our top row — vanilla tooltip
                }
                GeoCharacter character = ModuleCharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character) || __instance.AbilityToolTipObject == null)
                {
                    return true;
                }
                int cost = Phase4.DevUnlockAllLevels ? 0 : character.Progression.GetAbilitySlotCost(slot);
                ViewElementDef view = slot.Ability.ViewElementDef;
                __instance.AbilityToolTipObject.Show(slot, view, useMutagens: false, cost);
                return false; // handled — skip the native (Mutagen) tooltip
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellRowInteractivity OnTrackSlotPointerEnter_Prefix threw: " + e);
                return true;
            }
        }
    }
}
