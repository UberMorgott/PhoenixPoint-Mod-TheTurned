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
        // marker ability GUID -> the character level at which that cell unlocks. A learned/leaked marker only
        // counts toward the evolve scope once the recruit's level reaches this (prevents pre-unlock evolution
        // from a template/auto-learn leak; see HighestLearnedScope). 0 = no level gate.
        private static readonly Dictionary<string, int> _levelByMarkerGuid = new Dictionary<string, int>();
        // normal variant token -> elite variant token (case-insensitive). Populated by the monster DATA.
        private static readonly Dictionary<string, string> _tokenMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>DATA hook: bind a cell marker to the evolve tier it grants AND the character level at which
        /// that cell unlocks (idempotent upsert). <paramref name="unlockLevel"/> gates the marker in
        /// <see cref="HighestLearnedScope"/> so no elite weapon appears before the recruit reaches that level,
        /// even if the marker leaks into the enumeration before being purchased (0 = no level gate).</summary>
        internal static void Register(TacticalAbilityDef marker, EvolveScope scope, int unlockLevel = 0)
        {
            if (marker?.Guid != null && scope != EvolveScope.None)
            {
                _byMarkerGuid[marker.Guid] = scope;
                _levelByMarkerGuid[marker.Guid] = unlockLevel;
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

        /// <summary>BUG2: true when the ability is one of our registered cell/evolution markers. Used by the
        /// click-suppression to recognize a mod cell even if its slot's AbilityTrack.Source reads unexpectedly.</summary>
        internal static bool IsCellMarker(TacticalAbilityDef ability)
        {
            return ability?.Guid != null && _byMarkerGuid.ContainsKey(ability.Guid);
        }

        /// <summary>The MAXIMUM evolve scope among the recruit's learned evolve markers (None if none).
        /// Higher tier wins (L5/AllWeapons over L4/LeftWeapon).</summary>
        internal static EvolveScope HighestLearnedScope(CharacterProgression prog)
        {
            EvolveScope max = EvolveScope.None;
            if (prog == null || _byMarkerGuid.Count == 0)
            {
                return max;
            }
            // LEVEL GUARD (Fix 2): a cell/evolve marker only counts toward the scope once the recruit's CURRENT
            // level has reached that cell's unlock level. EnumerateLearnedAbilities can surface a marker (template
            // slot or generation auto-learn of an available secondary slot) before it is actually unlocked, which
            // would evolve weapons pre-L4. Gating here (not in EnumerateLearnedAbilities) leaves arm-set/claw
            // selection in ApplyChosenSets untouched — those use the separate ArmOption/Phase4Markers registries.
            int charLevel = prog.LevelProgression?.Level ?? 0;
            foreach (TacticalAbilityDef ability in ArthronArms.EnumerateLearnedAbilities(prog))
            {
                if (ability?.Guid != null
                    && _byMarkerGuid.TryGetValue(ability.Guid, out EvolveScope s)
                    && s > max)
                {
                    int unlock = _levelByMarkerGuid.TryGetValue(ability.Guid, out int lvl) ? lvl : 0;
                    if (charLevel >= unlock)
                    {
                        max = s;
                    }
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
