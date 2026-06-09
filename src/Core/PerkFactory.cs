using Base.Defs;
using Base.Entities.Abilities;
using Base.Entities.Statuses;
using Base.UI;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using System.Collections.Generic;

namespace TheTurned.Core
{
    /// <summary>
    /// Builds self-contained, dependency-free perks for any turned monster's ability track. The core
    /// primitive is <see cref="BuildStatPassive"/>: a custom <see cref="PassiveModifierAbilityDef"/> whose
    /// <c>StatModifications</c> array is authored directly (no reliance on a cloned template's payload), so
    /// it ALWAYS produces a non-null ability — satisfying the "every track slot must be non-null" rule even
    /// when the live game lacks any particular vanilla/TFTV def.
    ///
    /// Grounded facts (decompiled source):
    ///  - PassiveModifierAbilityDef : TacticalAbilityDef; fields StatModifications (ItemStatModification[]),
    ///    DamageKeywordPairs, ItemTagStatModifications.
    ///  - ItemStatModification { StatModificationTarget TargetStat; StatModificationType Modification; float Value }.
    ///  - StatModificationTarget enum: Endurance=0 (raises MaxHP), Armour=8, Health=4, BonusAttackDamage=0x200, ...
    /// </summary>
    internal static class PerkFactory
    {
        // A known-good vanilla PassiveModifierAbilityDef to clone for the base TacticalAbilityDef
        // scaffolding (Officer uses this same "Devoted_AbilityDef"). Resolved by GUID with robust fallbacks.
        private const string DevotedPassiveGuid = "52dde58b-5782-f804-38fe-78e7353941b2";
        // Sniper proficiency supplies a known-good progression + VED template (already used by the class).
        private const string SniperProficiencyGuid = "54328f21-e01a-4364-0aa7-4507affd2ccf";

        /// <summary>
        /// Build (idempotently) a passive perk that applies the given stat modifications. Returns a non-null
        /// <see cref="PassiveModifierAbilityDef"/> in all cases (blank fallback if no template resolves).
        /// </summary>
        internal static PassiveModifierAbilityDef BuildStatPassive(
            DefRepository repo,
            string abilityGuid,
            string abilityName,
            string progGuid,
            string vedGuid,
            string nameLocKey,
            string descLocKey,
            string iconFileName,
            int skillPointCost,
            int mutagenCost,
            IList<ItemStatModification> statMods)
        {
            if (repo == null)
            {
                return null;
            }
            if (repo.GetDef(abilityGuid) is PassiveModifierAbilityDef existing)
            {
                return existing;
            }

            PassiveModifierAbilityDef template = ResolvePassiveTemplate(repo);
            PassiveModifierAbilityDef perk = template != null
                ? repo.CreateDef<PassiveModifierAbilityDef>(abilityGuid, template)
                : repo.CreateDef<PassiveModifierAbilityDef>(abilityGuid);
            if (perk == null)
            {
                return null;
            }

            perk.name = abilityName;
            perk.StatModifications = statMods != null ? System.Linq.Enumerable.ToArray(statMods) : new ItemStatModification[0];
            // Don't carry the template's extra damage keywords / item-tag mods.
            perk.DamageKeywordPairs = new PhoenixPoint.Tactical.Entities.DamageKeywords.DamageKeywordPair[0];
            perk.ItemTagStatModifications = new EquipmentItemTagStatModification[0];
            perk.CharacterProgressionData = BuildProgression(repo, progGuid, abilityName, skillPointCost, mutagenCost);
            perk.ViewElementDef = BuildVed(repo, vedGuid, abilityName, nameLocKey, descLocKey, iconFileName);
            return perk;
        }

        /// <summary>
        /// Build (idempotently) a PERSONAL-TRACK marker perk: a <see cref="PassiveModifierAbilityDef"/> with
        /// no stat modifications, whose progression carries the shared <c>PersonalProgressionTag</c> so the
        /// game treats it as a legitimate personal/rolled ability (renders in the personal row and is a valid
        /// PerkOracle swap target). Used for the rolled weapon-arm markers (Feature 2). The arm geometry is
        /// driven separately via <c>SetItems</c>; this def only carries identity (loc + icon).
        /// </summary>
        internal static PassiveModifierAbilityDef BuildMarker(
            DefRepository repo,
            string abilityGuid,
            string abilityName,
            string progGuid,
            string vedGuid,
            string nameLocKey,
            string descLocKey,
            string iconFileName)
        {
            PassiveModifierAbilityDef marker = BuildStatPassive(repo, abilityGuid, abilityName, progGuid, vedGuid,
                nameLocKey, descLocKey, iconFileName, skillPointCost: 0, mutagenCost: 0, statMods: null);
            // Tag the progression as a personal-track ability (same filter the generator's pool uses).
            if (marker?.CharacterProgressionData != null)
            {
                GameTagDef personalTag = ResolvePersonalProgressionTag();
                if (personalTag != null)
                {
                    marker.CharacterProgressionData.PersonalTrackTags = new[] { personalTag };
                }
            }
            return marker;
        }

        /// <summary>Resolve the shared <c>PersonalProgressionTag</c> (the personal-pool filter tag).</summary>
        private static GameTagDef ResolvePersonalProgressionTag()
        {
            SharedData shared = Base.Core.GameUtl.GameComponent<SharedData>();
            return shared?.SharedGameTags?.PersonalProgressionTag;
        }

        /// <summary>Convenience: a single Add stat modification.</summary>
        internal static ItemStatModification Add(StatModificationTarget target, float value)
        {
            return new ItemStatModification
            {
                TargetStat = target,
                Modification = StatModificationType.Add,
                Value = value
            };
        }

        private static PassiveModifierAbilityDef ResolvePassiveTemplate(DefRepository repo)
        {
            if (repo.GetDef(DevotedPassiveGuid) is PassiveModifierAbilityDef devoted)
            {
                return devoted;
            }
            // Robust fallback: any PassiveModifierAbilityDef in the repo.
            return DefUtils.AnyTemplate<PassiveModifierAbilityDef>(repo);
        }

        private static AbilityCharacterProgressionDef BuildProgression(
            DefRepository repo, string progGuid, string abilityName, int skillPointCost, int mutagenCost)
        {
            if (repo.GetDef(progGuid) is AbilityCharacterProgressionDef existing)
            {
                return existing;
            }
            // Clone the Sniper proficiency's progression as a known-good base, then set costs.
            AbilityCharacterProgressionDef template =
                (repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef)?.CharacterProgressionData;
            AbilityCharacterProgressionDef prog = template != null
                ? repo.CreateDef<AbilityCharacterProgressionDef>(progGuid, template)
                : repo.CreateDef<AbilityCharacterProgressionDef>(progGuid);
            if (prog == null)
            {
                return null;
            }
            prog.name = "E_CharacterProgressionData [" + abilityName + "]";
            prog.RequiredStrength = 0;
            prog.RequiredWill = 0;
            prog.RequiredSpeed = 0;
            prog.SkillPointCost = skillPointCost;
            prog.MutagenCost = mutagenCost;
            prog.PersonalTrackTags = new PhoenixPoint.Common.Entities.GameTags.GameTagDef[0];
            return prog;
        }

        /// <summary>
        /// Build (idempotently) a VED for a Phase-4 popup ROW (SpecializationDef.ViewElementDef): same
        /// clone+loc flow as <see cref="BuildVed"/> but with the SPEC icon slot (Icons.TrySetSpecIcon).
        /// </summary>
        internal static TacticalAbilityViewElementDef BuildRowVed(
            DefRepository repo, string vedGuid, string vedName, string nameLocKey, string descLocKey, string iconFileName)
        {
            if (repo.GetDef(vedGuid) is TacticalAbilityViewElementDef existing)
            {
                return existing;
            }
            TacticalAbilityViewElementDef template =
                (repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef)?.ViewElementDef;
            TacticalAbilityViewElementDef ved = template != null
                ? repo.CreateDef<TacticalAbilityViewElementDef>(vedGuid, template)
                : repo.CreateDef<TacticalAbilityViewElementDef>(vedGuid);
            if (ved == null)
            {
                return null;
            }
            ved.name = "E_ViewElement [" + vedName + "]";
            ved.Name = vedName;
            ved.DisplayName1 = new LocalizedTextBind(nameLocKey);
            ved.Description = new LocalizedTextBind(descLocKey);
            Icons.TrySetSpecIcon(ved, iconFileName);
            return ved;
        }

        private static TacticalAbilityViewElementDef BuildVed(
            DefRepository repo, string vedGuid, string abilityName, string nameLocKey, string descLocKey, string iconFileName)
        {
            if (repo.GetDef(vedGuid) is TacticalAbilityViewElementDef existing)
            {
                return existing;
            }
            // Use the Sniper proficiency VED as a known-good template (same approach the class uses).
            TacticalAbilityViewElementDef template =
                (repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef)?.ViewElementDef;
            TacticalAbilityViewElementDef ved = template != null
                ? repo.CreateDef<TacticalAbilityViewElementDef>(vedGuid, template)
                : repo.CreateDef<TacticalAbilityViewElementDef>(vedGuid);
            if (ved == null)
            {
                return null;
            }
            ved.name = "E_ViewElement [" + abilityName + "]";
            ved.Name = abilityName;
            ved.DisplayName1 = new LocalizedTextBind(nameLocKey);
            ved.Description = new LocalizedTextBind(descLocKey);
            Icons.TrySetAbilityIcon(ved, iconFileName);
            return ved;
        }
    }
}
