using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Weapons;

namespace TheTurned.Core
{
    // TODO Phase4 Task 7: real registry (marker ability def Guid -> matched set / claw weapon).
    /// <summary>TEMPORARY STUB — maps Phase-4 marker abilities to their chosen matched sets. Always misses
    /// until the real registry lands, so <see cref="ArthronArms.ApplyChosenSets"/> is a safe no-op.</summary>
    internal static class Phase4Markers
    {
        internal static bool TryGetArmSet(TacticalAbilityDef a, out MatchedSet s) { s = null; return false; }
        internal static bool TryGetHeadSet(TacticalAbilityDef a, out MatchedSet s) { s = null; return false; }
        internal static bool TryGetClawWeapon(TacticalAbilityDef a, out WeaponDef w) { w = null; return false; }
    }
}
