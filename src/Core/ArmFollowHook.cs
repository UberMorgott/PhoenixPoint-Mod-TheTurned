using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Modding;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Keeps a recruited Arthron's physical arms in sync with its rolled/swapped arm-marker abilities.
    /// PerkOracle's swap removes the old marker silently (no event) then <c>AddAbility(newMarker)</c> which
    /// fires <c>CharacterProgression.OnAbilityAdded</c>; by the time our handler runs the marker set is
    /// already correct, so we FULLY re-derive both arms from the current ability set (never trust a per-add
    /// delta or rely on a removal event). The subscription is scoped to OUR Arthrons (narrower + reversible
    /// vs a global Harmony patch on the non-public removal path).
    /// </summary>
    internal static class ArmFollowHook
    {
        private static ModLogger Log => TheTurnedMain.Main?.Logger;

        // Track which progressions we've already subscribed (avoid double-subscribe on re-scan).
        private static readonly HashSet<CharacterProgression> _subscribed = new HashSet<CharacterProgression>();

        /// <summary>
        /// Scan the Phoenix roster for our recruited Arthrons, subscribe each to <c>OnAbilityAdded</c>, and
        /// re-derive its arms once. Idempotent — safe to call on recruit and on every geoscape load.
        /// </summary>
        internal static void ScanAndSubscribe(GeoLevelController geo)
        {
            if (geo?.PhoenixFaction == null || !Phase4.Enabled || !CrabmanParts.HasSets)
            {
                return;
            }
            try
            {
                foreach (GeoCharacter geoChar in geo.PhoenixFaction.Characters.ToList())
                {
                    if (IsTurnedArthron(geoChar))
                    {
                        Subscribe(geoChar);
                    }
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] ArmFollowHook scan failed: {e.Message}");
            }
        }

        /// <summary>Subscribe a single GeoCharacter's progression + apply arms immediately.</summary>
        internal static void Subscribe(GeoCharacter geoChar)
        {
            CharacterProgression prog = geoChar?.Progression;
            if (prog == null)
            {
                return;
            }
            if (_subscribed.Add(prog))
            {
                // Capture the GeoCharacter; re-derive on any ability add (arm-marker or not — guarded inside).
                prog.OnAbilityAdded += _ => ArthronArms.ApplyChosenSets(geoChar);
            }
            ArthronArms.ApplyChosenSets(geoChar);
        }

        private static bool IsTurnedArthron(GeoCharacter geoChar)
        {
            var marker = Tags.RecruitMarkerTag;
            var tags = geoChar?.TemplateDef?.Data?.GameTags;
            return marker != null && tags != null && tags.Contains(marker);
        }
    }
}
