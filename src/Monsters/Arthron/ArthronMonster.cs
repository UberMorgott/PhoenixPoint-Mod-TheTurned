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
            // No stat overrides — clone keeps pure vanilla Arthron stats (Str 100 -> ~1120 HP).
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
            // Resolve the Arthron class tag ONCE (created by Tags.EnsureClassTag in BuildAllClasses).
            ITurnedMonster arthron = MonsterRegistry.All.FirstOrDefault(m => m != null && m.Id == "Arthron");
            ClassTagDef classTag = arthron != null ? Tags.GetClassTag(repo, arthron) : null;

            // ROW A — Bruiser: 5 design cells, padded to RowLength by the factory.
            SpecRowFactory.GetOrCreateRow(repo, "Bruiser", "ARTHRON_ROW_BRUISER", "Arthron_Spec.png",
                classTag, ArthronPerks.BuildRowCells(repo), fillerIcon: "Arthron_Spec.png");
            // ROW B — Gunner: 6 design cells, padded to RowLength by the factory.
            SpecRowFactory.GetOrCreateRow(repo, "Gunner", "ARTHRON_ROW_GUNNER", "ArthronGunner_Spec.png",
                classTag, ArthronGunnerPerks.BuildRowCells(repo), fillerIcon: "ArthronGunner_Spec.png");
            // ROW C — Arms: matched-SET marker cells (CrabmanParts.Build runs first — ModMain order).
            // GATED on resolved sets: building early would bake 8 fillers into the track def PERMANENTLY
            // (GetOrCreateRowTrack early-returns the existing def and never refreshes). Deferred rows
            // appear on the first successful pass + the next FeedRows.
            if (CrabmanParts.HasSets)
            {
                SpecRowFactory.GetOrCreateRow(repo, "Arms", "ARTHRON_ROW_ARMS", "Arthron_ArmRight.png",
                    classTag, ArthronArmsRow.BuildRowCells(repo), fillerIcon: "Arthron_Spec.png");
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] arms row deferred — Crabman sets not resolved yet");
            }
            // ROW E — Claw Strikes: status-on-hit claw clones. Same gating rationale as Arms: building
            // before the base claw resolves would bake a filler-only track PERMANENTLY.
            if (CrabmanParts.DefaultRight?.Hand != null)
            {
                SpecRowFactory.GetOrCreateRow(repo, "Claw", "ARTHRON_ROW_CLAW", "Arthron_CrushingClaw.png",
                    classTag, ArthronClawPerks.BuildRowCells(repo), fillerIcon: "Arthron_Spec.png");
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] claw row deferred — base claw weapon not resolved yet");
            }
            // ROW D — Head/Spray: native spitter set + keyword-clone variants. Gated on the SPITTER set
            // specifically (not just HeadSets.Count) — same permanent-filler-bake rationale.
            if (ArthronHeadPerks.FindSpitterSet() != null)
            {
                SpecRowFactory.GetOrCreateRow(repo, "Head", "ARTHRON_ROW_HEAD", "ArthronSpray_Acid.png",
                    classTag, ArthronHeadPerks.BuildRowCells(repo), fillerIcon: "Arthron_Spec.png");
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] head row deferred — Crabman spitter set not resolved yet");
            }
            // ROW F — Survival: immunities + limb-restore capstone. NOT gated like Arms/Claw/Head: its
            // cells resolve from repo-global StatusDefs / ability defs (not bundle-timing-sensitive
            // Crabman item defs), and each cell degrades individually (warn + PadRow filler).
            // Runs every pass on purpose — refreshes the LimbRestoreMarker static read by LimbRestoreHook.
            var survivalCells = ArthronSurvivalPerks.BuildRowCells(repo);
            SpecRowFactory.GetOrCreateRow(repo, "Survival", "ARTHRON_ROW_SURVIVAL", "ArthronSurvival_Regen.png",
                classTag, survivalCells, fillerIcon: "Arthron_Spec.png");

            // ROW — Cell progression (top yellow 5-cell track). NOT gated on CrabmanParts.HasSets: the
            // armor cells resolve their leg/torso/carapace defs lazily at apply time (CellArmorApply),
            // and the stat/nav cells are repo-global. Runs every pass (idempotent get-or-create).
            SpecRowFactory.GetOrCreateRow(repo, "Cell", "ARTHRON_ROW_CELL", "Arthron_Spec.png",
                classTag, ArthronCellRow.BuildRowCells(repo), fillerIcon: "Arthron_Spec.png");
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
