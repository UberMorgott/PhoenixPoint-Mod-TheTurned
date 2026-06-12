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

        // ---- TUNABLE cell stat bonuses -----------------------------------------------------------------
        // Both cells are SEPARATE PassiveModifierAbilityDefs; learning cell 5 (prereq cell 3) keeps cell 3
        // learned too, so the engine applies BOTH stat arrays -> Prime STACKS ADDITIVELY on Alpha.
        // (Endurance raises MaxHP at x10 via MaxHP = Toughness + Endurance * EnduranceToHealthMultiplier.)

        // Alpha (Cell 3, LEGS_STAT): a solid mid-tier bump.
        internal const float Alpha_Endurance = 5f;            // +50 MaxHP
        internal const float Alpha_Speed = 1f;
        internal const float Alpha_Willpower = 2f;
        internal const float Alpha_BonusAttackDamage = 10f;

        // Prime (Cell 5, PRIME_EVOLVE): peak bonus, applied ON TOP of Alpha (totals = Alpha + Prime).
        internal const float Prime_Endurance = 10f;           // +100 MaxHP (on top of Alpha's +50)
        internal const float Prime_Speed = 2f;
        internal const float Prime_Willpower = 4f;
        internal const float Prime_BonusAttackDamage = 25f;
        internal const float Prime_Armour = 10f;

        /// <summary>Cell-3 Alpha stat passive contents (fixed bonus, no level-scaling).</summary>
        internal static ItemStatModification[] AlphaStats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Alpha_Endurance),
            PerkFactory.Add(StatModificationTarget.Speed, Alpha_Speed),
            PerkFactory.Add(StatModificationTarget.Willpower, Alpha_Willpower),
            PerkFactory.Add(StatModificationTarget.BonusAttackDamage, Alpha_BonusAttackDamage),
        };

        /// <summary>Cell-5 Prime stat passive contents (fixed bonus; stacks additively on Alpha).</summary>
        internal static ItemStatModification[] PrimeStats() => new[]
        {
            PerkFactory.Add(StatModificationTarget.Endurance, Prime_Endurance),
            PerkFactory.Add(StatModificationTarget.Speed, Prime_Speed),
            PerkFactory.Add(StatModificationTarget.Willpower, Prime_Willpower),
            PerkFactory.Add(StatModificationTarget.BonusAttackDamage, Prime_BonusAttackDamage),
            PerkFactory.Add(StatModificationTarget.Armour, Prime_Armour),
        };
    }
}
