using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Abstract base implementing the shared boilerplate / sane defaults of <see cref="ITurnedMonster"/>.
    /// Concrete monsters override the monster-specific bits (template resolution, stats, ability track,
    /// the GUIDs, and the display metadata). Def names are derived from <see cref="Id"/> to match the
    /// project's naming convention.
    /// </summary>
    internal abstract class TurnedMonsterBase : ITurnedMonster
    {
        public abstract string Id { get; }
        public abstract KeyCode RecruitKey { get; }

        public abstract TacCharacterDef ResolveTemplate(DefRepository repo);
        public abstract AbilityTrackSlot[] BuildAbilityTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency);
        public abstract void ApplyStatOverrides(TacCharacterDef clone);

        // Stable GUIDs — concrete monsters MUST supply their own (idempotency across reloads).
        public abstract string CloneGuid { get; }
        public abstract string SpecGuid { get; }
        public abstract string TrackGuid { get; }
        public abstract string ProficiencyGuid { get; }
        public abstract string ProficiencyProgGuid { get; }
        public abstract string ClassTagGuid { get; }
        public abstract string SpecVedGuid { get; }
        public abstract string ProficiencyVedGuid { get; }

        // Derived display / def-name defaults (override if a monster needs a custom name).
        public virtual string ClassTagName => $"TheTurned_{Id}_ClassTagDef";
        public virtual string SpecName => $"TheTurned_{Id}SpecializationDef";
        public virtual string SpecDisplayName => Id;
        public virtual string SpecDescription => $"A turned Pandoran {Id}.";
        public virtual string IconFileName => null;

        // --- Phase 3: second spec row — defaults no-op (monsters without a 2nd tree) -------------
        public virtual bool HasSecondarySpec => false;

        public virtual string SecondarySpecName => $"TheTurned_{Id}Secondary_SpecializationDef";
        public virtual string SecondarySpecDisplayName => Id + " (2)";
        public virtual string SecondarySpecDescription => $"A turned Pandoran {Id} (second tree).";
        public virtual string SecondaryIconFileName => null;

        public virtual string SecondaryClassTagName => $"TheTurned_{Id}Secondary_ClassTagDef";
        public virtual string SecondaryClassTagGuid => null;
        public virtual string SecondarySpecGuid => null;
        public virtual string SecondaryTrackGuid => null;
        public virtual string SecondaryProficiencyGuid => null;
        public virtual string SecondaryProficiencyProgGuid => null;
        public virtual string SecondarySpecVedGuid => null;
        public virtual string SecondaryProficiencyVedGuid => null;

        public virtual AbilityTrackSlot[] BuildSecondaryAbilityTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            // Default: just the proficiency in slot 0 (overridden by monsters with a real 2nd tree).
            return new[] { new AbilityTrackSlot { Ability = proficiency, RequiresPrevAbility = false } };
        }

        // --- Phase 3: rolled arms — defaults no-op -----------------------------------------------
        public virtual bool HasRolledArms => false;

        public virtual void BuildArmOptions(DefRepository repo)
        {
            // No-op by default.
        }
    }
}
