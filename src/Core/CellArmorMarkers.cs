using PhoenixPoint.Tactical.Entities.Abilities;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>Marker ability GUID -> ordered list of armor ItemDef NAMES the cell equips (legs / torso /
    /// carapace only — never head/arm, which the augment screen owns, spec §5.5). CellArmorApply re-derives
    /// the equipped armor from which markers are learned (single source of truth, mirrors Phase4Markers).</summary>
    internal static class CellArmorMarkers
    {
        /// <summary>One cell's armor payload: the ordered ItemDef NAMES + the cell ORDER (cell index/level)
        /// used to pick the HIGHEST learned loadout (monotonic by cell order, NOT by buy order or name).</summary>
        internal struct Entry
        {
            internal string[] Names;
            internal int Order;
        }

        private static readonly Dictionary<string, Entry> _byMarkerGuid = new Dictionary<string, Entry>();

        internal static void Register(TacticalAbilityDef marker, string[] armorDefNames, int order)
        {
            if (marker != null && armorDefNames != null) { _byMarkerGuid[marker.Guid] = new Entry { Names = armorDefNames, Order = order }; }
        }

        internal static bool TryGet(TacticalAbilityDef a, out string[] names, out int order)
        {
            names = null;
            order = 0;
            if (a != null && _byMarkerGuid.TryGetValue(a.Guid, out Entry e))
            {
                names = e.Names;
                order = e.Order;
                return true;
            }
            return false;
        }

        internal static bool HasAny => _byMarkerGuid.Count > 0;
    }
}
