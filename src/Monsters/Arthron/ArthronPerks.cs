using Base.Defs;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using System;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// The Arthron's real ability track: a "heavy bruiser Pandoran" that grows chitin armour and crushes
    /// with its claws. Slot 0 is the class proficiency (identity + can also wield human sidearms); slots
    /// 1-6 are thematic perks built as self-contained <see cref="PassiveModifierAbilityDef"/>s (no external
    /// def dependency), so every slot ALWAYS resolves to a non-null ability.
    ///
    /// Stat lever recap (grounded): StatModificationTarget.Endurance raises MaxHP via
    /// MaxHP = Toughness + Endurance * EnduranceToHealthMultiplier(10); Armour adds flat per-bodypart
    /// absorption; BonusAttackDamage scales standard-damage attacks (incl. the innate claw).
    /// </summary>
    internal static class ArthronPerks
    {
        // Human sidearm weapon tags so the recruited Arthron can also carry a pistol/PDW.
        // Resolved BY NAME at runtime (robustness rule), with Officer's known GUIDs as fallback.
        private const string HandgunTagName = "HandgunItem_TagDef";
        private const string HandgunTagGuid = "7a8a0a76-deb6-c004-3b5b-712eae0ad4a5";
        private const string PdwTagName = "PDWItem_TagDef";
        private const string PdwTagGuid = "87b91929-c816-97d4-4877-20b00fdf37b3";

        // Optional vanilla/TFTV abilities to prefer; fall back to custom passives if absent (no hard dependency).

        // ACID SPIT: deliberately left EMPTY -> always uses the custom passive fallback. The real acid
        // abilities are all ShootAbilityDefs bound to a creature-specific weapon/bodypart, e.g.
        // "Siren_SpitAcid_AbilityDef" (TFTVDefsWithConfigDependency.cs:1187, paired with
        // "Siren_Torso_AcidSpitter_WeaponDef" :1185) and "GooSpit_ShootAbilityDef"
        // (TFTVArtOfCrab.cs:833). VERDICT: NOT safe as a personal-track perk -- ShootAbility.Weapon is
        // base.Equipment as Weapon (ShootAbility.cs:23) and the whole ability dereferences it
        // (targeting/Shoot/GetWeaponDisabledState -> NoSuitableEquipment when Weapon==null,
        // ShootAbility.cs:126). A recruited Arthron has no acid-spitter weapon, so the ability would be
        // permanently disabled / null-deref. Keep the +BonusAttackDamage "Acid Glands" passive instead.
        private static readonly string[] AcidSpitCandidates = new string[0];

        // REGENERATION: verified self-contained passive. "Regeneration_Torso_Passive_AbilityDef" is an
        // ApplyStatusAbilityDef (ApplyStatusAbilityDef : TacticalAbilityDef, ApplyStatusAbilityDef.cs:12)
        // that applies the "Regeneration_Torso_Constant_StatusDef" HealthChangeStatusDef when added to a
        // unit. TFTV grants it exactly this way -- AddAbility(regeneration, actor) with no weapon needed
        // (TFTVHumanEnemies.cs:1719; def also runtime-present per VariousAdjustments.cs:101). VERDICT:
        // safe to drop into a personal track. Null-guarded fallback (+Endurance) remains for non-TFTV.
        private static readonly string[] RegenerationCandidates =
        {
            "Regeneration_Torso_Passive_AbilityDef"
        };

        // --- stable invented GUIDs (idempotent across reloads) ----------------------------------
        // Slot 1 Natural Armour
        private const string NaturalArmourGuid = "11a1b2c3-0001-4a11-9001-aa0102030401";
        private const string NaturalArmourProgGuid = "11a1b2c3-0002-4a11-9001-aa0102030402";
        private const string NaturalArmourVedGuid = "11a1b2c3-0003-4a11-9001-aa0102030403";
        // Slot 2 Acid Glands (fallback passive)
        private const string AcidSpitGuid = "22a1b2c3-0001-4a22-9002-aa0102030401";
        private const string AcidSpitProgGuid = "22a1b2c3-0002-4a22-9002-aa0102030402";
        private const string AcidSpitVedGuid = "22a1b2c3-0003-4a22-9002-aa0102030403";
        // Slot 3 Chitin Plating
        private const string ChitinPlatingGuid = "33a1b2c3-0001-4a33-9003-aa0102030401";
        private const string ChitinPlatingProgGuid = "33a1b2c3-0002-4a33-9003-aa0102030402";
        private const string ChitinPlatingVedGuid = "33a1b2c3-0003-4a33-9003-aa0102030403";
        // Slot 4 Crushing Claw
        private const string CrushingClawGuid = "44a1b2c3-0001-4a44-9004-aa0102030401";
        private const string CrushingClawProgGuid = "44a1b2c3-0002-4a44-9004-aa0102030402";
        private const string CrushingClawVedGuid = "44a1b2c3-0003-4a44-9004-aa0102030403";
        // Slot 5 Hardened Hide (fallback passive)
        private const string RegenerationGuid = "55a1b2c3-0001-4a55-9005-aa0102030401";
        private const string RegenerationProgGuid = "55a1b2c3-0002-4a55-9005-aa0102030402";
        private const string RegenerationVedGuid = "55a1b2c3-0003-4a55-9005-aa0102030403";
        // Slot 6 Apex Carapace (capstone)
        private const string ApexCarapaceGuid = "66a1b2c3-0001-4a66-9006-aa0102030401";
        private const string ApexCarapaceProgGuid = "66a1b2c3-0002-4a66-9006-aa0102030402";
        private const string ApexCarapaceVedGuid = "66a1b2c3-0003-4a66-9006-aa0102030403";

        // --- chosen numeric values --------------------------------------------------------------
        internal const float NaturalArmour_Armour = 10f;
        internal const float AcidSpit_BonusDamage = 15f;
        internal const float ChitinPlating_Armour = 15f;
        internal const float ChitinPlating_Endurance = 5f;   // +50 max HP
        internal const float CrushingClaw_BonusDamage = 20f;
        internal const float Regeneration_Endurance = 8f;    // +80 max HP
        internal const float ApexCarapace_Armour = 20f;
        internal const float ApexCarapace_Endurance = 10f;   // +100 max HP
        internal const float ApexCarapace_BonusDamage = 10f;

        /// <summary>
        /// Build the 7 real ability-track slots. Slot 0 = the supplied class proficiency (enriched with
        /// human sidearm tags); slots 1-6 = the thematic perks. Every slot's Ability is non-null.
        /// </summary>
        internal static AbilityTrackSlot[] BuildTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            EnrichProficiency(repo, proficiency);

            AbilityTrackSlot[] slots = new AbilityTrackSlot[7];
            slots[0] = new AbilityTrackSlot { Ability = proficiency, RequiresPrevAbility = false };
            slots[1] = Slot(NaturalArmour(repo));
            slots[2] = Slot(AcidSpit(repo));
            slots[3] = Slot(ChitinPlating(repo));
            slots[4] = Slot(CrushingClaw(repo));
            slots[5] = Slot(Regeneration(repo));
            slots[6] = Slot(ApexCarapace(repo));
            return slots;
        }

        private static AbilityTrackSlot Slot(TacticalAbilityDef ability)
        {
            return new AbilityTrackSlot { Ability = ability, RequiresPrevAbility = false };
        }

        /// <summary>Merge human Handgun + PDW tags onto the proficiency so the Arthron can wield a sidearm.</summary>
        private static void EnrichProficiency(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            if (proficiency?.ClassTags == null || repo == null)
            {
                return;
            }
            MergeWeaponTag(proficiency.ClassTags, DefUtils.ResolveByName<ItemTypeTagDef>(repo, HandgunTagName, HandgunTagGuid));
            MergeWeaponTag(proficiency.ClassTags, DefUtils.ResolveByName<ItemTypeTagDef>(repo, PdwTagName, PdwTagGuid));
        }

        private static void MergeWeaponTag(GameTagsList tags, GameTagDef tag)
        {
            if (tag == null || tags.Contains(tag))
            {
                return;
            }
            try
            {
                tags.Merge(tag);
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogWarning($"[TheTurned] Could not add weapon tag '{tag.name}': {e.Message}");
            }
        }

        // --- Slot 1: Natural Armour (+Armour) ---------------------------------------------------
        private static PassiveModifierAbilityDef NaturalArmour(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                NaturalArmourGuid, "TheTurned_Arthron_NaturalArmour_AbilityDef",
                NaturalArmourProgGuid, NaturalArmourVedGuid,
                "ARTHRON_NATURALARMOUR_NAME", "ARTHRON_NATURALARMOUR_DESC", "Arthron_NaturalArmour.png",
                skillPointCost: 10, mutagenCost: 10,
                new[] { PerkFactory.Add(StatModificationTarget.Armour, NaturalArmour_Armour) });
        }

        // --- Slot 2: Acid Spit (prefer vanilla active; else "Acid Glands" passive) --------------
        private static TacticalAbilityDef AcidSpit(DefRepository repo)
        {
            foreach (string name in AcidSpitCandidates)
            {
                TacticalAbilityDef vanilla = DefUtils.ResolveByName<TacticalAbilityDef>(repo, name);
                if (vanilla != null)
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Arthron slot 2 using vanilla acid ability '{name}'.");
                    return vanilla;
                }
            }
            // Fallback: self-contained "Acid Glands" passive (+bonus attack damage).
            return PerkFactory.BuildStatPassive(repo,
                AcidSpitGuid, "TheTurned_Arthron_AcidGlands_AbilityDef",
                AcidSpitProgGuid, AcidSpitVedGuid,
                "ARTHRON_ACIDSPIT_NAME", "ARTHRON_ACIDSPIT_DESC", "Arthron_AcidGlands.png",
                skillPointCost: 15, mutagenCost: 15,
                new[] { PerkFactory.Add(StatModificationTarget.BonusAttackDamage, AcidSpit_BonusDamage) });
        }

        // --- Slot 3: Chitin Plating (signature 'grows armour': +Armour +Endurance) --------------
        private static PassiveModifierAbilityDef ChitinPlating(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                ChitinPlatingGuid, "TheTurned_Arthron_ChitinPlating_AbilityDef",
                ChitinPlatingProgGuid, ChitinPlatingVedGuid,
                "ARTHRON_CHITINPLATING_NAME", "ARTHRON_CHITINPLATING_DESC", "Arthron_ChitinPlating.png",
                skillPointCost: 20, mutagenCost: 15,
                new[]
                {
                    PerkFactory.Add(StatModificationTarget.Armour, ChitinPlating_Armour),
                    PerkFactory.Add(StatModificationTarget.Endurance, ChitinPlating_Endurance)
                });
        }

        // --- Slot 4: Crushing Claw (melee/attack damage) ----------------------------------------
        private static PassiveModifierAbilityDef CrushingClaw(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                CrushingClawGuid, "TheTurned_Arthron_CrushingClaw_AbilityDef",
                CrushingClawProgGuid, CrushingClawVedGuid,
                "ARTHRON_CRUSHINGCLAW_NAME", "ARTHRON_CRUSHINGCLAW_DESC", "Arthron_CrushingClaw.png",
                skillPointCost: 20, mutagenCost: 20,
                new[] { PerkFactory.Add(StatModificationTarget.BonusAttackDamage, CrushingClaw_BonusDamage) });
        }

        // --- Slot 5: Regeneration (prefer vanilla; else "Hardened Hide" +Endurance passive) -----
        private static TacticalAbilityDef Regeneration(DefRepository repo)
        {
            foreach (string name in RegenerationCandidates)
            {
                TacticalAbilityDef vanilla = DefUtils.ResolveByName<TacticalAbilityDef>(repo, name);
                if (vanilla != null)
                {
                    TheTurnedMain.Main?.Logger?.LogInfo($"[TheTurned] Arthron slot 5 using vanilla regen ability '{name}'.");
                    return vanilla;
                }
            }
            return PerkFactory.BuildStatPassive(repo,
                RegenerationGuid, "TheTurned_Arthron_HardenedHide_AbilityDef",
                RegenerationProgGuid, RegenerationVedGuid,
                "ARTHRON_REGENERATION_NAME", "ARTHRON_REGENERATION_DESC", "Arthron_HardenedHide.png",
                skillPointCost: 25, mutagenCost: 15,
                new[] { PerkFactory.Add(StatModificationTarget.Endurance, Regeneration_Endurance) });
        }

        // --- Slot 6: Apex Carapace (capstone: strongest) ----------------------------------------
        private static PassiveModifierAbilityDef ApexCarapace(DefRepository repo)
        {
            return PerkFactory.BuildStatPassive(repo,
                ApexCarapaceGuid, "TheTurned_Arthron_ApexCarapace_AbilityDef",
                ApexCarapaceProgGuid, ApexCarapaceVedGuid,
                "ARTHRON_APEXCARAPACE_NAME", "ARTHRON_APEXCARAPACE_DESC", "Arthron_ApexCarapace.png",
                skillPointCost: 30, mutagenCost: 30,
                new[]
                {
                    PerkFactory.Add(StatModificationTarget.Armour, ApexCarapace_Armour),
                    PerkFactory.Add(StatModificationTarget.Endurance, ApexCarapace_Endurance),
                    PerkFactory.Add(StatModificationTarget.BonusAttackDamage, ApexCarapace_BonusDamage)
                });
        }
    }
}
