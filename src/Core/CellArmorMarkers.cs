using PhoenixPoint.Tactical.Entities.Abilities;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>Marker ability GUID -> ordered list of armor ItemDef NAMES the cell equips (legs / torso /
    /// carapace only — never head/arm, which the augment screen owns, spec §5.5). CellArmorApply re-derives
    /// the equipped armor from which markers are learned (single source of truth, mirrors Phase4Markers).</summary>
    internal static class CellArmorMarkers
    {
        private static readonly Dictionary<string, string[]> _byMarkerGuid = new Dictionary<string, string[]>();

        internal static void Register(TacticalAbilityDef marker, string[] armorDefNames)
        {
            if (marker != null && armorDefNames != null) { _byMarkerGuid[marker.Guid] = armorDefNames; }
        }

        internal static bool TryGet(TacticalAbilityDef a, out string[] names)
        {
            names = null;
            return a != null && _byMarkerGuid.TryGetValue(a.Guid, out names);
        }

        internal static bool HasAny => _byMarkerGuid.Count > 0;
    }
}
