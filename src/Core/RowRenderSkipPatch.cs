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
    /// Phase-4: SKIP rendering the Personal AND SecondaryClass track rows on the progression screen
    /// for our recruits. The MutoidSkillContainer prefab's <c>PersonalTrackContainer</c> and
    /// <c>SecondaryClassContainer</c> hold only 5 pre-placed <c>AbilityTrackSkillEntryElement</c>
    /// children BY DESIGN (the class icon occupies the cells 6-7 space), while <c>SetAbilityTrack</c>
    /// indexes <c>trackElements[b]</c> for b &lt; MaxLevel (decompile AbilityTrackContainerElement.cs
    /// :220-230) → ArgumentOutOfRange. RefreshAbilityTracks route map (IL): 0x00a8 Primary→primary
    /// pool (≥7, safe), 0x00cf SecondaryClass→secondary pool (5-wide, crashed), 0x010b Personal with
    /// SecondarySpecDef==null→secondary pool (vanilla mutoids, never our recruits), 0x0130
    /// Personal→personal pool (5-wide, skipped). Padding the pools at runtime was reverted (4ef566b):
    /// clones instantiated in the inactive hierarchy get deferred Awake → null _animator NRE, and
    /// they draw over the class icon. Our recruits don't need these rows — the mutoid POPUP is their
    /// progression surface (it reads the tracks directly).
    /// Seam: Prefix on the base <c>SetAbilityTrack(AbilityTrack, AbilityTrackSource, List)</c> —
    /// no subclass overrides it; sole caller is RefreshAbilityTracks (:148-167). Gate = Phase-4
    /// recruit + per-route ReferenceEquals pool discriminator, so the vanilla SecondarySpecDef==null
    /// route stays untouched even though IsPhase4Recruit already excludes vanilla. Each skip also
    /// hides its container (RefreshAbilityTracks unconditionally SetActive(true)'d all three at
    /// :136-147; it re-activates them on every refresh, so vanilla characters on the same instance
    /// recover automatically). Graceful-disable on unresolved targets (DevUnlockPatch pattern).
    /// </summary>
    internal static class RowRenderSkipPatch
    {
        private static bool _applied;
        // Once-per-character-per-route log; keyed on GeoTacUnitId (stable across re-opens; a
        // GeoCharacter reference key would pin dead instances and re-log on save reload).
        private static readonly HashSet<string> _loggedFor = new HashSet<string>();

        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_character");
        private static readonly FieldInfo PersonalElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_personalTraitSkillElements");
        private static readonly FieldInfo SecondaryElementsField =
            AccessTools.Field(typeof(AbilityTrackContainerElement), "_secondaryClassSkillElements");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            MethodInfo target = AccessTools.Method(typeof(AbilityTrackContainerElement), "SetAbilityTrack",
                new[] { typeof(AbilityTrack), typeof(AbilityTrackSource), typeof(List<AbilityTrackSkillEntryElement>) });
            if (target == null || CharacterField == null || PersonalElementsField == null || SecondaryElementsField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Row render skip: target(s) unresolved "
                    + $"(setTrack={target != null} charField={CharacterField != null} "
                    + $"personalField={PersonalElementsField != null} secondaryField={SecondaryElementsField != null}) — patch disabled.");
                return;
            }
            try
            {
                harmony.Patch(target,
                    prefix: new HarmonyMethod(typeof(RowRenderSkipPatch), nameof(SetAbilityTrackPrefix)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Row render-skip patch applied (Personal + Secondary).");
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError("[TheTurned] Row render-skip patch failed: " + e);
            }
        }

        // Param names bind the original's arguments (abilitySource, trackElements).
        public static bool SetAbilityTrackPrefix(AbilityTrackContainerElement __instance,
            AbilityTrackSource abilitySource, List<AbilityTrackSkillEntryElement> trackElements)
        {
            try
            {
                if (abilitySource != AbilityTrackSource.Personal && abilitySource != AbilityTrackSource.SecondaryClass)
                {
                    return true;
                }
                GeoCharacter character = CharacterField.GetValue(__instance) as GeoCharacter;
                if (!Phase4.IsPhase4Recruit(character))
                {
                    return true;
                }
                // Per-route pool discriminators: Personal must target the PERSONAL pool (:164-167) —
                // the SecondarySpecDef==null re-route into the SECONDARY pool (:160-163) is vanilla
                // mutoid territory; SecondaryClass must target the SECONDARY pool (:154-156).
                bool personalRoute = abilitySource == AbilityTrackSource.Personal
                    && ReferenceEquals(trackElements, PersonalElementsField.GetValue(__instance));
                bool secondaryRoute = abilitySource == AbilityTrackSource.SecondaryClass
                    && ReferenceEquals(trackElements, SecondaryElementsField.GetValue(__instance));
                if (!personalRoute && !secondaryRoute)
                {
                    return true;
                }
                // RefreshAbilityTracks force-activated the container (:136-147); hide it so the
                // 5 stale pre-placed cells don't render under/over the class icon.
                var container = personalRoute ? __instance.PersonalTrackContainer : __instance.SecondaryClassContainer;
                if (container != null)
                {
                    container.SetActive(false);
                }
                string routeName = personalRoute ? "Personal" : "Secondary";
                if (_loggedFor.Add($"{character.Id}:{routeName}"))
                {
                    TheTurnedMain.LogInfo($"[TheTurned] {routeName} row render skipped for '{character.GetName()}'.");
                }
                return false; // skip the original render (would index trackElements[b] past the pool)
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] Row render skip failed (rendering normally): " + e.Message);
                return true;
            }
        }
    }
}
