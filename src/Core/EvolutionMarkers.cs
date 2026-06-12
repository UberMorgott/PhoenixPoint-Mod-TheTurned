using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>
    /// How far a learned cell evolves the recruit's CHOSEN weapons. Ordinal-comparable: a higher tier
    /// implies every lower tier (AllWeapons implies LeftWeapon). The recruit's effective scope is the
    /// MAXIMUM scope among all learned evolve markers (<see cref="EvolutionMarkers.HighestLearnedScope"/>).
    /// </summary>
    internal enum EvolveScope
    {
        /// <summary>No evolution.</summary>
        None = 0,
        /// <summary>Evolve only the LEFT-arm weapon set.</summary>
        LeftWeapon = 1,
        /// <summary>Evolve left arm + right arm + head spit.</summary>
        AllWeapons = 2,
    }

    /// <summary>
    /// GENERIC (monster-agnostic) weapon-evolution registry. Mirrors <see cref="CellArmorMarkers"/> /
    /// <see cref="Phase4Markers"/>: a per-monster DATA class populates it during its row-build (no Core
    /// changes when a new monster is added). Two pieces of DATA live here, both supplied by the monster:
    ///   1. marker GUID -> <see cref="EvolveScope"/>: which learned cell unlocks which evolution tier.
    ///   2. normalToken -> eliteToken: how each chosen weapon variant upgrades to its elite variant.
    /// The generic APPLY (<see cref="ArthronArms.ApplyChosenSets"/>) reads this registry to compose the
    /// elite variant ON TOP of the player's chosen set — honouring the choice, idempotent, no literal
    /// monster def names in Core logic.
    /// </summary>
    internal static class EvolutionMarkers
    {
        // marker ability GUID -> the evolve scope that learning it grants.
        private static readonly Dictionary<string, EvolveScope> _byMarkerGuid = new Dictionary<string, EvolveScope>();
        // normal variant token -> elite variant token (case-insensitive). Populated by the monster DATA.
        private static readonly Dictionary<string, string> _tokenMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>DATA hook: bind a cell marker to the evolve tier it grants (idempotent upsert).</summary>
        internal static void Register(TacticalAbilityDef marker, EvolveScope scope)
        {
            if (marker?.Guid != null && scope != EvolveScope.None)
            {
                _byMarkerGuid[marker.Guid] = scope;
            }
        }

        /// <summary>DATA hook: declare one normal->elite token upgrade (idempotent upsert). Tokens are the
        /// variant tokens used by the monster's part enumeration (e.g. CrabmanParts.MatchedSet.Token).</summary>
        internal static void RegisterTokenUpgrade(string normalToken, string eliteToken)
        {
            if (!string.IsNullOrEmpty(normalToken) && !string.IsNullOrEmpty(eliteToken))
            {
                _tokenMap[normalToken] = eliteToken;
            }
        }

        internal static bool HasAny => _byMarkerGuid.Count > 0;

        /// <summary>The MAXIMUM evolve scope among the recruit's learned evolve markers (None if none).
        /// Higher tier wins (L5/AllWeapons over L4/LeftWeapon).</summary>
        internal static EvolveScope HighestLearnedScope(CharacterProgression prog)
        {
            EvolveScope max = EvolveScope.None;
            if (prog == null || _byMarkerGuid.Count == 0)
            {
                return max;
            }
            foreach (TacticalAbilityDef ability in ArthronArms.EnumerateLearnedAbilities(prog))
            {
                if (ability?.Guid != null
                    && _byMarkerGuid.TryGetValue(ability.Guid, out EvolveScope s)
                    && s > max)
                {
                    max = s;
                }
            }
            return max;
        }

        /// <summary>Map a normal variant token to its elite token. Returns false (eliteToken=null) when the
        /// token has no registered upgrade OR is already an elite VALUE (idempotent — no double-evolve).</summary>
        internal static bool TryGetEliteToken(string normalToken, out string eliteToken)
        {
            eliteToken = null;
            if (string.IsNullOrEmpty(normalToken))
            {
                return false;
            }
            // Already-elite guard: if the token is a registered upgrade TARGET, it is already elite.
            foreach (string elite in _tokenMap.Values)
            {
                if (string.Equals(elite, normalToken, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return _tokenMap.TryGetValue(normalToken, out eliteToken) && !string.IsNullOrEmpty(eliteToken);
        }
    }
}
