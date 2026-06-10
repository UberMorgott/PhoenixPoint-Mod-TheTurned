using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;

namespace TheTurned.Core
{
    /// <summary>
    /// Phase-4: pads the progression screen's PERSONAL skill-element pool to MaxLevel before the
    /// engine renders it. The MutoidSkillContainer prefab ships with fewer than MaxLevel pre-placed
    /// <c>AbilityTrackSkillEntryElement</c> children under <c>PersonalTrackContainer</c>
    /// (<c>_personalTraitSkillElements</c> is filled once at Awake→AddSkillElement from prefab
    /// children, decompile AbilityTrackContainerElement.cs:197-208). Vanilla never indexes that pool:
    /// the Personal track is routed there only when the character has BOTH pandoran progression AND a
    /// non-null SecondarySpecDef (RefreshAbilityTracks :158-167) — a combination only our recruits
    /// produce — and then SetAbilityTrack reads <c>trackElements[b]</c> for b &lt; MaxLevel
    /// (:220-230) → ArgumentOutOfRangeException on the short pool.
    /// Prefix on <see cref="AbilityTrackContainerElement.RefreshAbilityTracks"/>, gated on
    /// <see cref="Phase4.IsPhase4Recruit"/> + the exact crash route + pool actually short, so vanilla
    /// paths are untouched. New elements are clones of an existing pool element wired exactly like
    /// AddSkillElement does. Graceful-disable on unresolved targets (DevUnlockPatch pattern).
    /// </summary>
    internal static class PersonalPoolPatch
    {
        private static bool _applied;

        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        private static readonly FieldInfo PersonalElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_personalTraitSkillElements");
        private static readonly FieldInfo SecondaryElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_secondaryClassSkillElements");
        private static readonly FieldInfo PrimaryElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_primaryClassSkillElements");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(AbilityTrackContainerElement),
                nameof(AbilityTrackContainerElement.RefreshAbilityTracks));
            if (target == null || CharacterField == null || PersonalElementsField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Personal pool pad: target(s) unresolved "
                    + $"(refresh={target != null} charField={CharacterField != null} "
                    + $"poolField={PersonalElementsField != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(PersonalPoolPatch), nameof(RefreshPrefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Personal element pool pad patch applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] Personal pool pad patch failed: " + e);
            }
        }

        public static void RefreshPrefix(AbilityTrackContainerElement __instance)
        {
            try
            {
                GeoCharacter character = CharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character) || __instance.PersonalTrackContainer == null)
                {
                    return;
                }
                // The personal pool is only indexed on the Personal + SecondarySpecDef != null route
                // (RefreshAbilityTracks :158-167); without a second spec it renders into the secondary pool.
                if (character.Progression?.SecondarySpecDef == null)
                {
                    return;
                }
                int maxLevel = character.LevelProgression?.Def?.MaxLevel ?? 0;
                List<AbilityTrackSkillEntryElement> pool =
                    PersonalElementsField.GetValue(__instance) as List<AbilityTrackSkillEntryElement>;
                if (pool == null || maxLevel <= 0 || pool.Count >= maxLevel)
                {
                    return; // idempotent: already padded (or nothing to do)
                }
                AbilityTrackSkillEntryElement template = (pool.Count > 0 ? pool[pool.Count - 1] : null)
                    ?? FirstOf(SecondaryElementsField, __instance)
                    ?? FirstOf(PrimaryElementsField, __instance);
                if (template == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] Personal pool pad: no template element in any pool — skipped.");
                    return;
                }
                int before = pool.Count;
                while (pool.Count < maxLevel)
                {
                    AbilityTrackSkillEntryElement clone = UnityEngine.Object.Instantiate(
                        template, __instance.PersonalTrackContainer.transform);
                    // Mirror AddSkillElement wiring (:202-206). Unity does NOT serialize the Action
                    // fields, so the clone starts with null handlers — no double-wiring inherited.
                    Wire(clone, __instance);
                    pool.Add(clone);
                }
                TheTurnedMain.LogInfo($"[TheTurned] Personal element pool padded {before}→{maxLevel} "
                    + $"for '{character.GetName()}'.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] Personal pool pad failed: " + e.Message);
            }
        }

        private static AbilityTrackSkillEntryElement FirstOf(FieldInfo poolField, AbilityTrackContainerElement instance)
        {
            List<AbilityTrackSkillEntryElement> list =
                poolField?.GetValue(instance) as List<AbilityTrackSkillEntryElement>;
            return list != null && list.Count > 0 ? list[0] : null;
        }

        /// <summary>Same three handler hookups AddSkillElement performs. The handlers are protected
        /// virtual; CreateDelegate with a target binds the most-derived override (the Mutoid one).</summary>
        private static void Wire(AbilityTrackSkillEntryElement element, AbilityTrackContainerElement owner)
        {
            element.TrackSlotPointerClick +=
                (Action<AbilityTrackSkillEntryElement, AbilityTrackSource, AbilityTrackSlot, bool>)
                AccessTools.Method(typeof(AbilityTrackContainerElement), "OnTrackSlotPointerClicked")
                    .CreateDelegate(typeof(Action<AbilityTrackSkillEntryElement, AbilityTrackSource, AbilityTrackSlot, bool>), owner);
            element.TrackSlotPointerEnter += (Action<AbilityTrackSlot, bool>)
                AccessTools.Method(typeof(AbilityTrackContainerElement), "OnTrackSlotPointerEnter")
                    .CreateDelegate(typeof(Action<AbilityTrackSlot, bool>), owner);
            element.TrackSlotPointerExit += (Action)
                AccessTools.Method(typeof(AbilityTrackContainerElement), "OnTrackSlotPointerExit")
                    .CreateDelegate(typeof(Action), owner);
        }
    }
}
