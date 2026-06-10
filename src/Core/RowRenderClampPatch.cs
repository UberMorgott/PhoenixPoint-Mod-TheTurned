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
    /// Phase-4: CLAMP the Personal and SecondaryClass row render to the prefab element-pool size for
    /// our recruits. The MutoidSkillContainer prefab's <c>PersonalTrackContainer</c> and
    /// <c>SecondaryClassContainer</c> hold only 5 pre-placed <c>AbilityTrackSkillEntryElement</c>
    /// children BY DESIGN (the class icon occupies the cells 6-7 space), while the original
    /// <c>SetAbilityTrack</c> indexes <c>trackElements[b]</c> for every ability slot (decompile
    /// AbilityTrackContainerElement.cs:220-230) → ArgumentOutOfRange on our 7-level tracks.
    /// History: pool padding reverted (4ef566b — deferred-Awake NRE + drew over the class icon);
    /// row HIDING reverted (d6465b3 follow-up — in this prefab the visible rows ARE the secondary
    /// teal + personal purple pools; hiding both blanked the screen). Clamp keeps the rows visible:
    /// teal shows the first 5 of 7 Gunner cells (clickable; the POPUP covers all levels), purple
    /// shows slot-0 proficiency + empties, the class icon stays intact (no children appended).
    /// Seam: Prefix on the base <c>SetAbilityTrack(AbilityTrack, AbilityTrackSource, List)</c> —
    /// no subclass overrides it; sole caller is RefreshAbilityTracks (:148-167). Gate = Phase-4
    /// recruit + pool actually SHORT, for ANY (source, pool) combination — this also covers the
    /// latent route C (Personal → SECONDARY pool when SecondarySpecDef==null, :160-163:
    /// TurnedMonsterBase.HasSecondarySpec defaults false, so a future single-spec monster hits it);
    /// a big-enough pool (e.g. the ≥7 primary) returns true → vanilla path; vanilla characters are
    /// excluded by IsPhase4Recruit alone. The clamped reimplementation mirrors the original loop
    /// exactly, calling the protected virtual GetAbilitySlots/SetAbilitySlot via instance-bound
    /// delegates (virtual dispatch hits the MutoidAbilityTrackContainerElement overrides).
    /// Containers are NOT hidden — vanilla's own SetActive(true) (:136-147) stays in charge.
    /// Graceful-disable on unresolved targets (DevUnlockPatch pattern).
    /// </summary>
    internal static class RowRenderClampPatch
    {
        private static bool _applied;
        // Once-per-character-per-route log; keyed on GeoTacUnitId string (stable across re-opens; a
        // GeoCharacter reference key would pin dead instances and re-log on save reload).
        private static readonly HashSet<string> _loggedFor = new HashSet<string>();

        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        private static readonly FieldInfo PersonalElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_personalTraitSkillElements");
        private static readonly FieldInfo SecondaryElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_secondaryClassSkillElements");
        // Protected virtual originals (decompile :253-263 / :232-251), invoked via instance-bound
        // delegates so virtual dispatch reaches the Mutoid overrides (GetAbilitySlotForLevel/SetAbilitySlot).
        private static readonly MethodInfo GetAbilitySlotsMethod =
            AccessTools.Method(typeof(AbilityTrackContainerElement), "GetAbilitySlots",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource) });
        private static readonly MethodInfo SetAbilitySlotMethod =
            AccessTools.Method(typeof(AbilityTrackContainerElement), "SetAbilitySlot",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource), typeof(AbilityTrackSlot), typeof(AbilityTrackSkillEntryElement) });

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(AbilityTrackContainerElement), "SetAbilityTrack",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource), typeof(List<AbilityTrackSkillEntryElement>) });
            if (target == null || CharacterField == null || PersonalElementsField == null
                || SecondaryElementsField == null || GetAbilitySlotsMethod == null || SetAbilitySlotMethod == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Row render clamp: target(s) unresolved "
                    + $"(setTrack={target != null} charField={CharacterField != null} "
                    + $"personalField={PersonalElementsField != null} secondaryField={SecondaryElementsField != null} "
                    + $"getSlots={GetAbilitySlotsMethod != null} setSlot={SetAbilitySlotMethod != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(RowRenderClampPatch), nameof(SetAbilityTrackPrefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Row render-clamp patch applied (Personal + Secondary).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] Row render-clamp patch failed: " + e);
            }
        }

        // Param names bind the original's arguments (track, abilitySource, trackElements).
        public static bool SetAbilityTrackPrefix(AbilityTrackContainerElement __instance,
            AbilityTrack track, AbilityTrackSource abilitySource, List<AbilityTrackSkillEntryElement> trackElements)
        {
            try
            {
                GeoCharacter character = CharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character) || trackElements == null)
                {
                    return true;
                }
                // Original body (:220-230): slots = GetAbilitySlots(track, source); for b < slots.Count
                // → SetAbilitySlot(track, source, slots[b], trackElements[b]).
                var getSlots = (Func<AbilityTrack, AbilityTrackSource, List<AbilityTrackSlot>>)
                    GetAbilitySlotsMethod.CreateDelegate(
                        typeof(Func<AbilityTrack, AbilityTrackSource, List<AbilityTrackSlot>>), __instance);
                List<AbilityTrackSlot> slots = getSlots(track, abilitySource);
                if (slots == null || trackElements.Count >= slots.Count)
                {
                    return true; // pool fits — vanilla path untouched
                }
                var setSlot = (Action<AbilityTrack, AbilityTrackSource, AbilityTrackSlot, AbilityTrackSkillEntryElement>)
                    SetAbilitySlotMethod.CreateDelegate(
                        typeof(Action<AbilityTrack, AbilityTrackSource, AbilityTrackSlot, AbilityTrackSkillEntryElement>), __instance);
                int clamped = trackElements.Count;
                for (int b = 0; b < clamped; b++)
                {
                    if (slots[b] == null)
                    {
                        continue; // defensive: SetAbilitySlot derefs slot.Ability
                    }
                    setSlot(track, abilitySource, slots[b], trackElements[b]);
                }
                // Pool identity only labels the log (route C renders Personal into the secondary pool).
                string poolName = ReferenceEquals(trackElements, PersonalElementsField.GetValue(__instance)) ? "personal"
                    : ReferenceEquals(trackElements, SecondaryElementsField.GetValue(__instance)) ? "secondary"
                    : "primary";
                if (_loggedFor.Add($"{character.Id}:{abilitySource}:{poolName}"))
                {
                    TheTurnedMain.LogInfo($"[TheTurned] {abilitySource} row ({poolName} pool) clamped "
                        + $"{slots.Count}→{clamped} for '{character.GetName()}'.");
                }
                return false; // original would index trackElements[b] past the pool
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] Row render clamp failed (rendering normally): " + e.Message);
                return true;
            }
        }
    }
}
