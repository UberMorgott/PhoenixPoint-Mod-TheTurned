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

        // --- Phase 3: second spec row "Carapace Gunner" -----------------------------------------
        // REV-2 (M-PROBE step 3 / M-LAYOUT): on the 2-row layout, suppress the secondary spec so the human
        // container shows only Primary + Personal tracks (= 2 rows). Reverts when TwoRowCellLayout is false.
        public override bool HasSecondarySpec => !Phase4.TwoRowCellLayout;
        public override string SecondarySpecName => "TheTurned_ArthronGunner_SpecializationDef";
        public override string SecondaryClassTagName => "TheTurned_ArthronGunner_ClassTagDef";
        public override string SecondarySpecDisplayName => "Carapace Gunner";
        public override string SecondarySpecDescription => "A turned Arthron drilled as a ranged carapace gunner.";
        public override string SecondaryIconFileName => "ArthronGunner_Spec.png";

        public override string SecondaryClassTagGuid => "c1e3a5b7-2d4f-6b8c-ae90-3f1d2b4c6e80";
        public override string SecondarySpecGuid => "d2f4b6c8-3e5a-7c9d-bf01-4a2e3c5d7f91";
        public override string SecondaryTrackGuid => "e3a5c7d9-4f6b-8d0e-c012-5b3f4d6e8002";
        public override string SecondaryProficiencyGuid => "f4b6d8ea-5a7c-9e1f-d123-6c4a5e7f9013";
        public override string SecondaryProficiencyProgGuid => "a5c7e9fb-6b8d-af20-e234-7d5b6f801124";
        public override string SecondarySpecVedGuid => "b6d8fa0c-7c9e-b031-f345-8e6c70912235";
        public override string SecondaryProficiencyVedGuid => "c7e90b1d-8da0-c142-0456-9f7d81023346";

        // --- Phase 3: rolled weapon arm slots ---------------------------------------------------
        public override bool HasRolledArms => true;

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
            // TRUE BASE chassis = `Crabby_AlienMutationVariationDef` (the base-game Arthron's real internal
            // name, guid cf460fd0-…, VERIFIED in Player.log 2026-06-10). NOT "alphabetically-first non-Elite"
            // (that picked Crabman3_AdvancedCharger — '3' < '_' in ordinal — armored legs/role loadout), and
            // NOT "Crabman_AlienMutationVariationDef" (that def DOES NOT EXIST). Crabby ships spitter head +
            // shield + elite legs, so the NAKED base is constructed post-spawn (ArthronArms.ApplyNakedBase);
            // Crabby is the right chassis/model. Resolve by exact name; fall back only if absent.
            TacCharacterDef chosen = crabmen.FirstOrDefault(d => d.name == "Crabby_AlienMutationVariationDef");
            if (chosen != null)
            {
                return chosen;
            }
            TheTurnedMain.LogWarn("[TheTurned] ResolveTemplate: 'Crabby_AlienMutationVariationDef' not found — "
                + "falling back to first non-high-tier Crabman (loadout may differ from true base).");
            chosen = crabmen
                .Where(d => !IsHighTier(d.name)
                    && d.name.IndexOf("SpawningPool", StringComparison.OrdinalIgnoreCase) < 0)
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
            // LOCKED STAT SPEC (docs/STAT-SPEC.md). The recruit's L1 baseline is set on the clone's per-def
            // authoring fields; per-level growth (L2-L5) is added by the evolution cells' Endurance/Willpower/
            // Speed stat passives (ArthronEvolution.Cell*Stats), one cell per level (cell N gated to level N).
            //
            // HP formula (grounded): MaxHP = Toughness + Endurance x EnduranceToHealthMultiplier.
            //   EnduranceToHealthMultiplier = 10 [G TacticalActorBaseDef.cs:23]; Arthron Toughness = 120
            //   (both on the SHARED base def -> NOT touched). TacCharacterData.BonusStats.Endurance = Strength
            //   [G TacCharacterData.cs:61-63], so the per-def "Strength" field seeds the runtime Endurance stat.
            //   L1: 120 + 3 x 10 = 150 HP. (Cells then drive 220/300/370/440 at L2/L3/L4/L5.)
            //
            // NOTE (reported): base Strength=3 is forced by the locked L1=150 HP + fixed Toughness 120 + x10.
            //   Strength also seeds carry-weight (EnduranceToCarryWeightMultiplier) -> low carry capacity by design.
            if (clone?.Data == null)
            {
                return;
            }
            clone.Data.Strength = 3;   // -> Endurance 3 -> 150 MaxHP at L1
            clone.Data.Will = 12;
            clone.Data.Speed = 18;
        }

        /// <summary>
        /// Second tree "Carapace Gunner": slot 0 = gunner proficiency; slots 1-6 = ranged/assault perks.
        /// See <see cref="ArthronGunnerPerks"/>.
        /// </summary>
        public override AbilityTrackSlot[] BuildSecondaryAbilityTrack(DefRepository repo, ClassProficiencyAbilityDef proficiency)
        {
            return ArthronGunnerPerks.BuildTrack(repo, proficiency);
        }

        /// <summary>Discover the Arthron arm WeaponDefs + build their marker abilities (Feature 2).</summary>
        public override void BuildArmOptions(DefRepository repo)
        {
            ArthronArms.BuildOptions(repo);
        }

        /// <summary>
        /// Phase-4: register the Arthron's popup ROWS (reuse rows first — Bruiser + Gunner from the
        /// existing Phase-2/3 perk builders). Idempotent; called from ModMain.BuildAllClasses when
        /// Phase4.Enabled.
        /// </summary>
        internal static void BuildPhase4Rows(DefRepository repo)
        {
            // CHUNK A — COLLAPSE TO 2 IN-PANEL ROWS. The recruit's visible mutoid panel draws from
            // Progression.AbilityTracks (SecondaryClass=top 5 evolution cells via
            // SpecRowFactory.HostCellsInSecondaryTrack, Personal=bottom purple) — a DIFFERENT data source
            // than the themed POPUP rows fed here into AvailablePandoranSpecialzations. So we STOP feeding the
            // themed popup rows (no SpecRowFactory.GetOrCreateRow calls) to leave exactly 2 rows; the in-panel
            // rows are untouched (investigation E1).
            //
            // We STILL call each *.BuildRowCells builder for its REGISTRATION SIDE EFFECTS (marker payloads
            // other systems depend on): ArthronCellRow → CellArmorMarkers (cells 2/4 armor, M2),
            // ArthronArmsRow → Phase4Markers arm SETs (ArthronArms.ApplyChosenSets), ArthronSurvivalPerks →
            // LimbRestoreMarker (LimbRestoreHook). Bruiser/Gunner/Claw/Head builders have no external-static
            // side effect (pure perk lists), so they're not needed for CHUNK A and are dropped from the feed.
            // The results are discarded (not fed to the popup). Gated builders keep their resolve guards.

            // Cell armor markers (cells 2/4) — also called at recruit/load time, idempotent here.
            ArthronCellRow.BuildRowCells(repo);

            // Arm-SET markers — required by ArthronArms.ApplyChosenSets / PerkOracle swaps.
            if (CrabmanParts.HasSets)
            {
                ArthronArmsRow.BuildRowCells(repo);
            }

            // Survival markers — refreshes the LimbRestoreMarker static read by LimbRestoreHook.
            ArthronSurvivalPerks.BuildRowCells(repo);
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
