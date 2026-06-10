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
    /// Phase-4: SKIP rendering the Personal track row on the progression screen for our recruits.
    /// The MutoidSkillContainer prefab's <c>PersonalTrackContainer</c> holds only 5 pre-placed
    /// <c>AbilityTrackSkillEntryElement</c> children BY DESIGN (the class icon occupies the cells
    /// 6-7 space), while <c>SetAbilityTrack</c> indexes <c>trackElements[b]</c> for b &lt; MaxLevel
    /// (decompile AbilityTrackContainerElement.cs:220-230) → ArgumentOutOfRange. Padding the pool at
    /// runtime is the wrong altitude: clones Instantiate'd while the edit-screen hierarchy is still
    /// inactive get a deferred Awake, so their <c>_animator</c> cache (assigned ONLY in
    /// AbilityTrackSkillEntryElement.Awake) is null when the same synchronous pass renders them →
    /// NRE; and last-sibling clones draw over the class icon. Our recruits don't need the row at
    /// all — the mutoid POPUP is their progression surface (it reads the Personal track directly).
    /// Seam: Prefix on the base <c>SetAbilityTrack(AbilityTrack, AbilityTrackSource, List)</c> —
    /// no subclass overrides it; reached only from RefreshAbilityTracks (:148-167). Gate =
    /// Phase-4 recruit + Personal source + trackElements IS the personal pool (reference compare),
    /// so the vanilla SecondarySpecDef==null route (Personal rendered into the SECONDARY pool,
    /// vanilla mutoids) is untouched. The skip also hides PersonalTrackContainer (RefreshAbilityTracks
    /// unconditionally SetActive(true)'d it at :144-147) so the 5 stale pre-placed cells don't show.
    /// Graceful-disable on unresolved targets (DevUnlockPatch pattern).
    /// </summary>
    internal static class PersonalRenderSkipPatch
    {
        private static bool _applied;
        private static readonly HashSet<GeoCharacter> _loggedFor = new HashSet<GeoCharacter>();

        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        private static readonly FieldInfo PersonalElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_personalTraitSkillElements");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(AbilityTrackContainerElement), "SetAbilityTrack",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource), typeof(List<AbilityTrackSkillEntryElement>) });
            if (target == null || CharacterField == null || PersonalElementsField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Personal render skip: target(s) unresolved "
                    + $"(setTrack={target != null} charField={CharacterField != null} "
                    + $"poolField={PersonalElementsField != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(PersonalRenderSkipPatch), nameof(SetAbilityTrackPrefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Personal row render-skip patch applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] Personal render-skip patch failed: " + e);
            }
        }

        // Param names bind the original's arguments (abilitySource, trackElements).
        public static bool SetAbilityTrackPrefix(AbilityTrackContainerElement __instance,
            AbilityTrackSource abilitySource, List<AbilityTrackSkillEntryElement> trackElements)
        {
            try
            {
                if (abilitySource != AbilityTrackSource.Personal)
                {
                    return true;
                }
                GeoCharacter character = CharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character))
                {
                    return true;
                }
                // Only the personal-CONTAINER route (:164-167). When SecondarySpecDef == null the
                // engine renders the Personal track into the SECONDARY pool (:160-163) — leave that.
                if (!ReferenceEquals(trackElements, PersonalElementsField.GetValue(__instance)))
                {
                    return true;
                }
                // RefreshAbilityTracks force-activated the container (:144-147); hide it so the
                // 5 stale pre-placed cells don't render under/over the class icon.
                if (__instance.PersonalTrackContainer != null)
                {
                    __instance.PersonalTrackContainer.SetActive(false);
                }
                if (_loggedFor.Add(character))
                {
                    TheTurnedMain.LogInfo($"[TheTurned] Personal row render skipped for '{character.GetName()}'.");
                }
                return false; // skip the original render (would index trackElements[b] past the pool)
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] Personal render skip failed (rendering normally): " + e.Message);
                return true;
            }
        }
    }
}
