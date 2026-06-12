using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>Marker ability GUID -> payload registry. Markers are passive abilities placed in row cells;
    /// <see cref="ArthronArms.ApplyChosenSets"/> re-derives equipment from which markers are learned
    /// (single source of truth).</summary>
    internal static class Phase4Markers
    {
        private static readonly Dictionary<string, MatchedSet> _armSets = new Dictionary<string, MatchedSet>();
        private static readonly Dictionary<string, MatchedSet> _headSets = new Dictionary<string, MatchedSet>();
        private static readonly Dictionary<string, WeaponDef> _clawWeapons = new Dictionary<string, WeaponDef>();

        // Upserts: idempotent across BuildAllClasses re-runs (defs are get-or-create, registry is in-memory).
        internal static void RegisterArmSet(TacticalAbilityDef marker, MatchedSet set)
        {
            if (marker != null && set != null) { _armSets[marker.Guid] = set; }
        }

        internal static void RegisterHeadSet(TacticalAbilityDef marker, MatchedSet set)
        {
            if (marker != null && set != null) { _headSets[marker.Guid] = set; }
        }

        internal static void RegisterClawWeapon(TacticalAbilityDef marker, WeaponDef claw)
        {
            if (marker != null && claw != null) { _clawWeapons[marker.Guid] = claw; }
        }

        internal static bool TryGetArmSet(TacticalAbilityDef a, out MatchedSet s)
        {
            s = null;
            return a != null && _armSets.TryGetValue(a.Guid, out s);
        }

        internal static bool TryGetHeadSet(TacticalAbilityDef a, out MatchedSet s)
        {
            s = null;
            return a != null && _headSets.TryGetValue(a.Guid, out s);
        }

        internal static bool TryGetClawWeapon(TacticalAbilityDef a, out WeaponDef w)
        {
            w = null;
            return a != null && _clawWeapons.TryGetValue(a.Guid, out w);
        }

        /// <summary>BUG2: true when the ability is one of our registered arm/head/claw cell markers.</summary>
        internal static bool IsCellMarker(TacticalAbilityDef a)
        {
            return a?.Guid != null
                && (_armSets.ContainsKey(a.Guid) || _headSets.ContainsKey(a.Guid) || _clawWeapons.ContainsKey(a.Guid));
        }
    }
}
