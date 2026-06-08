using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Contract every "turned" Pandoran implements so <see cref="TheTurned.Core"/> can do all the
    /// generic recruit + class-building work exactly once, with no monster-specific branching.
    ///
    /// Adding a new monster = drop a folder under src/Monsters implementing this interface (usually by
    /// deriving <see cref="TurnedMonsterBase"/>) and add one registration line in
    /// <see cref="MonsterRegistry.RegisterDefaults"/>.
    ///
    /// Hotkey convention: the recruiter fires on <c>Ctrl+Shift+<see cref="RecruitKey"/></c> while on an
    /// active geoscape (see <see cref="RecruitHotkey"/>).
    /// </summary>
    internal interface ITurnedMonster
    {
        /// <summary>Stable short identifier, e.g. "Arthron". Used to derive def names.</summary>
        string Id { get; }

        /// <summary>Base key for the recruit hotkey; combined with Ctrl+Shift by the poller.</summary>
        KeyCode RecruitKey { get; }

        /// <summary>Resolve the source enemy <see cref="TacCharacterDef"/> to clone for recruiting.</summary>
        TacCharacterDef ResolveTemplate(DefRepository repo);

        // --- class-tag + specialization metadata ------------------------------------------------
        /// <summary>Name of the per-monster <see cref="ClassTagDef"/>, e.g. "TheTurned_Arthron_ClassTagDef".</summary>
        string ClassTagName { get; }

        /// <summary>Name of the per-monster <see cref="SpecializationDef"/>.</summary>
        string SpecName { get; }

        /// <summary>Human-facing class display name (drives the spec's ViewElement DisplayName).</summary>
        string SpecDisplayName { get; }

        /// <summary>Human-facing class description (drives the spec's ViewElement Description).</summary>
        string SpecDescription { get; }

        /// <summary>Sprite file under Assets\Textures (optional; null/empty = keep cloned icon).</summary>
        string IconFileName { get; }

        // --- ability track ----------------------------------------------------------------------
        /// <summary>
        /// Build the 7 ability-track slots. Slot 0 MUST be the supplied <paramref name="proficiency"/>
        /// (carries the new class tag). The remaining slots are monster-defined (perks come later).
        /// </summary>
        AbilityTrackSlot[] BuildAbilityTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency);

        // --- balance ----------------------------------------------------------------------------
        /// <summary>Apply balanced stat overrides onto the freshly-cloned template (clone.Data).</summary>
        void ApplyStatOverrides(TacCharacterDef clone);

        // --- stable GUIDs (idempotent across reloads) -------------------------------------------
        string CloneGuid { get; }
        string SpecGuid { get; }
        string TrackGuid { get; }
        string ProficiencyGuid { get; }
        string ProficiencyProgGuid { get; }
        string ClassTagGuid { get; }
        string SpecVedGuid { get; }
        string ProficiencyVedGuid { get; }
    }
}
