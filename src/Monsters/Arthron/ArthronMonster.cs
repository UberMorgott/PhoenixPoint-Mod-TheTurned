using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Collections.Generic;
using System.Linq;
using TheTurned.Core;
using UnityEngine;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// The Arthron (codename "Crabman") turned monster. Supplies the Arthron-specific data the generic
    /// Core needs: the source enemy template, the (preserved) stable GUIDs/names, the balanced stat
    /// overrides, and the (currently placeholder) ability track.
    ///
    /// All GUIDs/names are carried over verbatim from the original monolithic ArthronClass /
    /// ArthronRecruiter so existing defs stay idempotent across reloads and saves.
    /// </summary>
    internal sealed class ArthronMonster : TurnedMonsterBase
    {
        private const string CrabmanClassTagName = "Crabman_ClassTagDef";

        public override string Id => "Arthron";
        public override KeyCode RecruitKey => KeyCode.T;

        // Display metadata (preserved from the original ViewElementDef text).
        public override string SpecDisplayName => "Arthron";
        public override string SpecDescription => "A turned Pandoran Arthron.";

        // Class/spec icon (256x256 RGBA PNG deployed to Assets\Textures).
        public override string IconFileName => "Arthron_Spec.png";

        // --- preserved stable GUIDs -------------------------------------------------------------
        // Cloned, progression-bearing Arthron def (was ArthronRecruiter.ProgressedArthronGuid).
        public override string CloneGuid => "TheTurned_ProgressedArthron_5f3a1c20-7b41-4e9d-9c2a-1d6e8f0a2b34";
        public override string ClassTagGuid => "b2d4f6a8-1c3e-4a5b-8d7f-2e9c0a1b3d5e";
        public override string SpecGuid => "d4f6b8ca-3e5a-6c7d-af9b-4a1e2c3d5f70";
        public override string TrackGuid => "e5a7c9db-4f6b-7d8e-ba0c-5b2f3d4e6081";
        public override string ProficiencyGuid => "f6b8da0c-5a7c-8e9f-cb1d-6c3a4e5f7192";
        public override string ProficiencyProgGuid => "a7c9eb1d-6b8d-9fa0-dc2e-7d4b5f608203";
        public override string SpecVedGuid => "b8da0c2e-7c9e-a0b1-ed3f-8e5c60719314";
        public override string ProficiencyVedGuid => "c9eb1d3f-8da0-b1c2-fe40-9f6d71820425";

        /// <summary>
        /// Resolve a basic Arthron variant: filter TacCharacterDefs by the Crabman class tag, prefer
        /// non-Elite/non-Ultra variants, order by name for a stable choice. (Same logic the original
        /// ArthronRecruiter.ResolveBasicArthron used, so the same template is cloned.)
        /// </summary>
        public override TacCharacterDef ResolveTemplate(DefRepository repo)
        {
            if (repo == null)
            {
                return null;
            }
            List<TacCharacterDef> crabmen = repo.GetAllDefs<TacCharacterDef>()
                .Where(d => d != null && HasCrabmanTag(d))
                .ToList();
            if (crabmen.Count == 0)
            {
                return null;
            }
            TacCharacterDef chosen = crabmen
                .Where(d => !IsHighTier(d.name))
                .OrderBy(d => d.name, StringComparer.Ordinal)
                .FirstOrDefault();
            if (chosen == null)
            {
                chosen = crabmen.OrderBy(d => d.name, StringComparer.Ordinal).First();
            }
            return chosen;
        }

        /// <summary>
        /// Real Arthron ability track (heavy bruiser): slot 0 = class proficiency; slots 1-6 = thematic
        /// perks (Natural Armour, Acid Glands, Chitin Plating, Crushing Claw, Hardened Hide, Apex Carapace).
        /// All slots resolve to non-null abilities. See <see cref="ArthronPerks"/>.
        /// </summary>
        public override AbilityTrackSlot[] BuildAbilityTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            return ArthronPerks.BuildTrack(repo, proficiency);
        }

        public override void ApplyStatOverrides(TacCharacterDef clone)
        {
            // No stat overrides — clone keeps pure vanilla Arthron stats (Str 100 -> ~1120 HP).
        }

        private static bool HasCrabmanTag(TacCharacterDef def)
        {
            IEnumerable<ClassTagDef> tags = def.ClassTags;
            if (tags == null)
            {
                return false;
            }
            foreach (ClassTagDef tag in tags)
            {
                if (tag != null && tag.name == CrabmanClassTagName)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsHighTier(string defName)
        {
            if (string.IsNullOrEmpty(defName))
            {
                return false;
            }
            return defName.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0
                || defName.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
