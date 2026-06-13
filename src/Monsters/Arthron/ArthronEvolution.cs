using PhoenixPoint.Common.Entities;
using PhoenixPoint.Tactical.Entities.Equipments;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// ARTHRON-SPECIFIC weapon-evolution + cell-stat DATA. The generic engine (<see cref="EvolutionMarkers"/>
    /// + <see cref="ArthronArms.ApplyChosenSets"/>) is monster-agnostic; this class declares everything that
    /// is Arthron/Crabman-specific:
    ///   - the normal->elite VARIANT TOKEN map (the chosen weapon upgrades to its real-elite-stat variant),
    ///   - the tunable Alpha / Prime stat bonuses (named constants — easy to tweak, documented below).
    ///
    /// A new monster (e.g. Triton) plugs in by adding its OWN &lt;X&gt;Evolution data class that calls
    /// <see cref="EvolutionMarkers.RegisterTokenUpgrade"/> + supplies its cell stat arrays — ZERO Core edits.
    ///
    /// Tokens here are the <see cref="MatchedSet.Token"/> values produced by CrabmanParts.VariantToken
    /// (def-name minus the side prefix + the type suffix), NOT raw def names — so the elite MatchedSet is
    /// found by Token equality inside the same side list (RightArmSets / LeftArmSets / HeadSets).
    /// </summary>
    internal static class ArthronEvolution
    {
        // ---- normal -> elite VARIANT TOKEN pairs (REAL native elite defs = real elite stats) -----------
        // Right arm: Pincer->ElitePincer, Gun->EliteGun, Viral_Gun->Viral_EliteGun (same mesh, stats only).
        // Left arm:  Shield->EliteShield, Grenade->EliteGrenade, Acid_Grenade->Acid_EliteGrenade.
        // Head spit: Spitter->EliteSpitter.
        // Each elite token's MatchedSet already exists in CrabmanParts (raw enumeration includes Elite sets).
        private static bool _registered;

        internal static void RegisterTokenMap()
        {
            if (_registered)
            {
                return;
            }
            // Right-arm weapons.
            EvolutionMarkers.RegisterTokenUpgrade("Pincer", "ElitePincer");
            EvolutionMarkers.RegisterTokenUpgrade("Gun", "EliteGun");
            EvolutionMarkers.RegisterTokenUpgrade("Viral_Gun", "Viral_EliteGun");
            // Left-arm weapons.
            EvolutionMarkers.RegisterTokenUpgrade("Shield", "EliteShield");
            EvolutionMarkers.RegisterTokenUpgrade("Grenade", "EliteGrenade");
            EvolutionMarkers.RegisterTokenUpgrade("Acid_Grenade", "Acid_EliteGrenade");
            // Head spit organ.
            EvolutionMarkers.RegisterTokenUpgrade("Spitter", "EliteSpitter");
            _registered = true;
        }

        // ---- PER-LEVEL cell stat bonuses (LOCKED SPEC, docs/STAT-SPEC.md) -------------------------------
        // Cells are SEPARATE PassiveModifierAbilityDefs, one per level (cell N gated to level N). Higher cells
        // require lower cells, so the engine applies EVERY learned cell's stat array -> the deltas STACK
        // ADDITIVELY on the L1 base set in ArthronMonster.ApplyStatOverrides (Str/Will/Speed 3/12/18).
        // Endurance raises MaxHP x10 (MaxHP = Toughness 120 + Endurance * 10).
        //
        // Target curve (cumulative): HP 150/220/300/370/440 | Will 12/16/21/26/30 | Move 18/20/22/23/25.
        // Endurance totals (=(HP-120)/10): 3/10/18/25/32. Deltas below back-solve that curve from the L1 base.
        //   L1 base      End  3  Will 12  Speed 18   (ApplyStatOverrides)
        //   Cell2 (L2)  +End  7 +Will  4 +Speed  2 -> End10 Will16 Speed20 -> 220 HP
        //   Cell3 (L3)  +End  8 +Will  5 +Speed  2 -> End18 Will21 Speed22 -> 300 HP   (Alpha)
        //   Cell4 (L4)  +End  7 +Will  5 +Speed  1 -> End25 Will26 Speed23 -> 370 HP
        //   Cell5 (L5)  +End  7 +Will  4 +Speed  2 -> End32 Will30 Speed25 -> 440 HP   (Prime)
        // Per-spec the actor-wide BonusAttackDamage that the old Alpha/Prime carried is DROPPED — limb damage
        // is now defined by the equipped/evolved weapon defs (Pincer 65->95, etc.), not a flat actor bonus.

        // Cell 2 (ARMOR1).
        internal const float Cell2_Endurance = 7f;
        internal const float Cell2_Willpower = 4f;
        internal const float Cell2_Speed = 2f;

        // Cell 3 (LEGS_STAT / Alpha).
        internal const float Alpha_Endurance = 8f;
        internal const float Alpha_Willpower = 5f;
        internal const float Alpha_Speed = 2f;

        // Cell 4 (ARMOR2_EVOLVE).
        internal const float Cell4_Endurance = 7f;
        internal const float Cell4_Willpower = 5f;
        internal const float Cell4_Speed = 1f;

        // Cell 5 (PRIME_EVOLVE / Prime).
        internal const float Prime_Endurance = 7f;
        internal const float Prime_Willpower = 4f;
        internal const float Prime_Speed = 2f;

        /// <summary>Cell-2 stat passive contents (per-level growth L1->L2).</summary>
        internal static ItemStatModification[] Cell2Stats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Cell2_Endurance),
            PerkFactory.Add(StatModificationTarget.Willpower, Cell2_Willpower),
            PerkFactory.Add(StatModificationTarget.Speed, Cell2_Speed),
        };

        /// <summary>Cell-3 Alpha stat passive contents (per-level growth L2->L3).</summary>
        internal static ItemStatModification[] AlphaStats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Alpha_Endurance),
            PerkFactory.Add(StatModificationTarget.Willpower, Alpha_Willpower),
            PerkFactory.Add(StatModificationTarget.Speed, Alpha_Speed),
        };

        /// <summary>Cell-4 stat passive contents (per-level growth L3->L4).</summary>
        internal static ItemStatModification[] Cell4Stats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Cell4_Endurance),
            PerkFactory.Add(StatModificationTarget.Willpower, Cell4_Willpower),
            PerkFactory.Add(StatModificationTarget.Speed, Cell4_Speed),
        };

        /// <summary>Cell-5 Prime stat passive contents (per-level growth L4->L5; stacks on all lower cells).</summary>
        internal static ItemStatModification[] PrimeStats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Prime_Endurance),
            PerkFactory.Add(StatModificationTarget.Willpower, Prime_Willpower),
            PerkFactory.Add(StatModificationTarget.Speed, Prime_Speed),
        };
    }
}
