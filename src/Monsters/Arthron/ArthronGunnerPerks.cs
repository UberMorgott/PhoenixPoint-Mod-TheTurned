using Base.Defs;
using Base.Entities.Abilities;
using Base.UI;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// The Arthron's SECOND specialization track: "Carapace Gunner" — a ranged/assault tree that
    /// complements the heavy-bruiser primary tree and synergises with a rolled machine-gun right arm.
    /// Slot 0 is the secondary class proficiency (identity); slots 1-6 are self-contained
    /// <see cref="PassiveModifierAbilityDef"/>s (no external def dependency) except slot 4 (Return Fire),
    /// which prefers the vanilla overwatch-on-being-shot active and falls back to a passive. Every slot
    /// ALWAYS resolves to a non-null ability (the track invariant).
    ///
    /// Stat targets (grounded, StatModificationTarget.cs): Accuracy=0x800, BonusAttackRange=0x400,
    /// Perception=0x20, Willpower=2, ActionPoints=0x4000, Armour=8.
    /// </summary>
    internal static class ArthronGunnerPerks
    {
        // Return Fire: prefer the vanilla overwatch-on-being-shot active; null-guarded fallback to passive.
        private static readonly string[] ReturnFireCandidates =
        {
            "ReturnFire_AbilityDef"
        };

        // --- stable invented GUIDs (idempotent across reloads) ----------------------------------
        // Slot 1 Steady Aim
        private const string SteadyAimGuid = "71b1c2d3-0001-4b71-9101-bb0102030401";
        private const string SteadyAimProgGuid = "71b1c2d3-0002-4b71-9101-bb0102030402";
        private const string SteadyAimVedGuid = "71b1c2d3-0003-4b71-9101-bb0102030403";
        // Slot 2 Suppression Plates
        private const string SuppressPlatesGuid = "72b1c2d3-0001-4b72-9102-bb0102030401";
        private const string SuppressPlatesProgGuid = "72b1c2d3-0002-4b72-9102-bb0102030402";
        private const string SuppressPlatesVedGuid = "72b1c2d3-0003-4b72-9102-bb0102030403";
        // Slot 3 Long Barrel
        private const string LongBarrelGuid = "73b1c2d3-0001-4b73-9103-bb0102030401";
        private const string LongBarrelProgGuid = "73b1c2d3-0002-4b73-9103-bb0102030402";
        private const string LongBarrelVedGuid = "73b1c2d3-0003-4b73-9103-bb0102030403";
        // Slot 4 Return Fire (passive fallback)
        private const string ReturnFireGuid = "74b1c2d3-0001-4b74-9104-bb0102030401";
        private const string ReturnFireProgGuid = "74b1c2d3-0002-4b74-9104-bb0102030402";
        private const string ReturnFireVedGuid = "74b1c2d3-0003-4b74-9104-bb0102030403";
        // Slot 4 Return Fire (vanilla clone + localized VED)
        private const string ReturnFireCloneGuid = "74b1c2d3-0004-4b74-9104-bb0102030404";
        private const string ReturnFireCloneProgGuid = "74b1c2d3-0005-4b74-9104-bb0102030405";
        private const string ReturnFireCloneVedGuid = "74b1c2d3-0006-4b74-9104-bb0102030406";
        // Slot 5 Spotter Eyes
        private const string SpotterGuid = "75b1c2d3-0001-4b75-9105-bb0102030401";
        private const string SpotterProgGuid = "75b1c2d3-0002-4b75-9105-bb0102030402";
        private const string SpotterVedGuid = "75b1c2d3-0003-4b75-9105-bb0102030403";
        // Slot 6 Overwatch Carapace (capstone)
        private const string OverwatchCarapaceGuid = "76b1c2d3-0001-4b76-9106-bb0102030401";
        private const string OverwatchCarapaceProgGuid = "76b1c2d3-0002-4b76-9106-bb0102030402";
        private const string OverwatchCarapaceVedGuid = "76b1c2d3-0003-4b76-9106-bb0102030403";

        // --- chosen numeric values --------------------------------------------------------------
        internal const float SteadyAim_Accuracy = 15f;
        internal const float SuppressPlates_Armour = 8f;
        internal const float SuppressPlates_Willpower = 4f;
        internal const float LongBarrel_AttackRange = 8f;
        internal const float ReturnFire_Accuracy = 10f;        // passive fallback only
        internal const float Spotter_Perception = 6f;
        internal const float Spotter_Accuracy = 5f;
        internal const float OverwatchCarapace_Accuracy = 15f;
        internal const float OverwatchCarapace_AttackRange = 6f;
        internal const float OverwatchCarapace_ActionPoints = 1f;

        /// <summary>
        /// Build the 7 secondary ability-track slots. Slot 0 = the supplied gunner proficiency; slots 1-6 =
        /// the gunner perks. Every slot's Ability is non-null.
        /// </summary>
        internal static AbilityTrackSlot[] BuildTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            EnrichProficiency(repo, proficiency);

            AbilityTrackSlot[] slots = new AbilityTrackSlot[7];
            slots[0] = new AbilityTrackSlot { Ability = proficiency, RequiresPrevAbility = false };
            slots[1] = Slot(SteadyAim(repo));
            slots[2] = Slot(SuppressPlates(repo));
            slots[3] = Slot(LongBarrel(repo));
            slots[4] = Slot(ReturnFire(repo));
            slots[5] = Slot(SpotterEyes(repo));
            slots[6] = Slot(OverwatchCarapace(repo));
            return slots;
        }

        /// <summary>
        /// Phase-4 popup ROW B "Gunner" cells: the existing gunner perks WITHOUT the slot-0 proficiency
        /// (rows are pure perk lists). 6 design cells; SpecRowFactory pads to RowLength with fillers.
        /// Builders are get-or-create, so double building with the fixed track is safe.
        /// </summary>
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            return new[]
            {
                Slot(SteadyAim(repo)),
                Slot(SuppressPlates(repo)),
                Slot(LongBarrel(repo)),
                Slot(ReturnFire(repo)),
                Slot(SpotterEyes(repo)),
                Slot(OverwatchCarapace(repo))
            };
        }

        private static AbilityTrackSlot Slot(TacticalAbilityDef ability)
        {
            return new AbilityTrackSlot { Ability = ability, RequiresPrevAbility = false };
        }

        // The gunner proficiency is purely cosmetic identity for the 2nd row; the primary proficiency
        // already grants the human-sidearm tags, so no extra weapon tags are required here. Kept as a
        // hook in case a monster wants assault/MG tags later.
        private static void EnrichProficiency(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
        }

        // --- Slot 1: Steady Aim (+Accuracy) -----------------------------------------------------
        private static PassiveModifierAbilityDef SteadyAim(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                SteadyAimGuid, "TheTurned_Arthron_SteadyAim_AbilityDef",
                SteadyAimProgGuid, SteadyAimVedGuid,
                "ARTHRON_STEADYAIM_NAME", "ARTHRON_STEADYAIM_DESC", "ArthronGunner_SteadyAim.png",
                skillPointCost: 10, mutagenCost: 10,
                new[] { PerkFactory.Add(StatModificationTarget.Accuracy, SteadyAim_Accuracy) });
        }

        // --- Slot 2: Suppression Plates (+Armour +Willpower) ------------------------------------
        private static PassiveModifierAbilityDef SuppressPlates(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                SuppressPlatesGuid, "TheTurned_Arthron_SuppressPlates_AbilityDef",
                SuppressPlatesProgGuid, SuppressPlatesVedGuid,
                "ARTHRON_SUPPRESSPLATES_NAME", "ARTHRON_SUPPRESSPLATES_DESC", "ArthronGunner_SuppressPlates.png",
                skillPointCost: 15, mutagenCost: 15,
                new[]
                {
                    PerkFactory.Add(StatModificationTarget.Armour, SuppressPlates_Armour),
                    PerkFactory.Add(StatModificationTarget.Willpower, SuppressPlates_Willpower)
                });
        }

        // --- Slot 3: Long Barrel (+BonusAttackRange) --------------------------------------------
        private static PassiveModifierAbilityDef LongBarrel(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                LongBarrelGuid, "TheTurned_Arthron_LongBarrel_AbilityDef",
                LongBarrelProgGuid, LongBarrelVedGuid,
                "ARTHRON_LONGBARREL_NAME", "ARTHRON_LONGBARREL_DESC", "ArthronGunner_LongBarrel.png",
                skillPointCost: 15, mutagenCost: 15,
                new[] { PerkFactory.Add(StatModificationTarget.BonusAttackRange, LongBarrel_AttackRange) });
        }

        // --- Slot 4: Return Fire (prefer vanilla active; else +Accuracy passive) ----------------
        private static TacticalAbilityDef ReturnFire(DefRepository repo)
        {
            foreach (string name in ReturnFireCandidates)
            {
                TacticalAbilityDef vanilla = DefUtils.ResolveByName<TacticalAbilityDef>(repo, name);
                if (vanilla == null)
                {
                    continue;
                }
                TacticalAbilityDef clone = CloneReturnFire(repo, vanilla);
                if (clone != null)
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Arthron gunner slot 4 using vanilla ability '{name}' (own clone, localized VED).");
                    return clone;
                }
            }
            return PerkFactory.BuildStatPassive(repo,
                ReturnFireGuid, "TheTurned_Arthron_ReturnFire_AbilityDef",
                ReturnFireProgGuid, ReturnFireVedGuid,
                "ARTHRON_RETURNFIRE_NAME", "ARTHRON_RETURNFIRE_DESC", "ArthronGunner_ReturnFire.png",
                skillPointCost: 20, mutagenCost: 20,
                new[] { PerkFactory.Add(StatModificationTarget.Accuracy, ReturnFire_Accuracy) });
        }

        /// <summary>
        /// Own clone of the vanilla Return Fire def for the popup row. The vanilla def used directly
        /// renders "NEEDS TEXT" (its VED loc keys don't resolve in the mutoid popup context) and its
        /// CharacterProgressionData is SHARED (cost 8; PersonalTrackTags feed the human personal-track
        /// random pool) — mutating either would leak to human soldiers. So: non-generic CreateDef keeps
        /// the runtime type (ReturnFireAbilityDef — CreateDef&lt;T&gt; would flatten to TacticalAbilityDef
        /// and lose the return-fire behavior, DefRepository.cs:254-283); own prog data (20/20 row scale,
        /// empty PersonalTrackTags so the clone never enters human rolls); own VED cloned from the
        /// VANILLA one (keeps the real icon) rebound to our CSV keys. Idempotent; null on create
        /// failure → caller falls back to the passive.
        /// </summary>
        private static TacticalAbilityDef CloneReturnFire(DefRepository repo, TacticalAbilityDef vanilla)
        {
            TacticalAbilityDef clone = repo.GetDef(ReturnFireCloneGuid) as TacticalAbilityDef
                ?? repo.CreateDef(ReturnFireCloneGuid, vanilla) as TacticalAbilityDef;
            if (clone == null)
            {
                return null;
            }
            clone.name = "TheTurned_Arthron_ReturnFireClone_AbilityDef";
            // Popup null-derefs a null prog data (cost read) — never ship the cell without one.
            AbilityCharacterProgressionDef prog = PerkFactory.BuildProgression(repo,
                ReturnFireCloneProgGuid, clone.name, skillPointCost: 20, mutagenCost: 20);
            if (prog == null)
            {
                return null;
            }
            clone.CharacterProgressionData = prog;
            TacticalAbilityViewElementDef ved = repo.GetDef(ReturnFireCloneVedGuid) as TacticalAbilityViewElementDef;
            if (ved == null && vanilla.ViewElementDef != null)
            {
                ved = repo.CreateDef(ReturnFireCloneVedGuid, vanilla.ViewElementDef) as TacticalAbilityViewElementDef;
            }
            if (ved != null)
            {
                ved.name = "E_ViewElement [" + clone.name + "]";
                ved.DisplayName1 = new LocalizedTextBind("ARTHRON_RETURNFIRE_NAME");
                ved.Description = new LocalizedTextBind("ARTHRON_RETURNFIRE_DESC");
                clone.ViewElementDef = ved; // icon inherited from the vanilla VED clone
            }
            return clone;
        }

        // --- Slot 5: Spotter Eyes (+Perception +Accuracy) ---------------------------------------
        private static PassiveModifierAbilityDef SpotterEyes(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                SpotterGuid, "TheTurned_Arthron_SpotterEyes_AbilityDef",
                SpotterProgGuid, SpotterVedGuid,
                "ARTHRON_SPOTTER_NAME", "ARTHRON_SPOTTER_DESC", "ArthronGunner_Spotter.png",
                skillPointCost: 25, mutagenCost: 20,
                new[]
                {
                    PerkFactory.Add(StatModificationTarget.Perception, Spotter_Perception),
                    PerkFactory.Add(StatModificationTarget.Accuracy, Spotter_Accuracy)
                });
        }

        // --- Slot 6: Overwatch Carapace (capstone: +Accuracy +Range +ActionPoints) --------------
        private static PassiveModifierAbilityDef OverwatchCarapace(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                OverwatchCarapaceGuid, "TheTurned_Arthron_OverwatchCarapace_AbilityDef",
                OverwatchCarapaceProgGuid, OverwatchCarapaceVedGuid,
                "ARTHRON_OVERWATCHCARAPACE_NAME", "ARTHRON_OVERWATCHCARAPACE_DESC", "ArthronGunner_OverwatchCarapace.png",
                skillPointCost: 30, mutagenCost: 25,
                new[]
                {
                    PerkFactory.Add(StatModificationTarget.Accuracy, OverwatchCarapace_Accuracy),
                    PerkFactory.Add(StatModificationTarget.BonusAttackRange, OverwatchCarapace_AttackRange),
                    PerkFactory.Add(StatModificationTarget.ActionPoints, OverwatchCarapace_ActionPoints)
                });
        }
    }
}
