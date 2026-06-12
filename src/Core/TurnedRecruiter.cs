using Base.Core;
using Base.Defs;
using Base.Levels;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Core;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Generic recruit chain for any <see cref="ITurnedMonster"/>. Resolves the monster's source enemy
    /// TacCharacterDef, clones it into a progression-bearing def (so the generator attaches a real runtime
    /// Progression and the edit screen no longer NREs), appends the monster class tag + the ONE shared
    /// marker tag, resets the class-tag cache, applies the monster's balanced stat overrides, then runs
    /// the native generate/spawn/reward chain to grant the unit to the Phoenix faction.
    ///
    /// All game API signatures verified against the decompiled source.
    /// </summary>
    internal static class TurnedRecruiter
    {
        private static ModLogger Log => TheTurnedMain.Main?.Logger;

        public static void RecruitMonster(ITurnedMonster monster)
        {
            if (monster == null)
            {
                return;
            }
            try
            {
                // 1. Geoscape guard: bail outside the geoscape (e.g. tactical) or if faction missing.
                Level level = GameUtl.CurrentLevel();
                GeoLevelController geo = level?.GetComponent<GeoLevelController>();
                if (geo == null || geo.PhoenixFaction == null)
                {
                    Log?.LogInfo($"[TheTurned] Hotkey ignored ({monster.Id}) — not on an active geoscape.");
                    return;
                }

                // 2. Resolve the source enemy template.
                TacCharacterDef vanillaTemplate = monster.ResolveTemplate(DefUtils.Repo);
                if (vanillaTemplate == null)
                {
                    Log?.LogError($"[TheTurned] No source TacCharacterDef found for '{monster.Id}' — recruit aborted.");
                    return;
                }
                Log?.LogInfo($"[TheTurned] Resolved '{monster.Id}' template name='{vanillaTemplate.name}' guid='{vanillaTemplate.Guid}'.");

                // 2b. Use a cloned def with a non-null Data.LevelProgression (Pandorans have null, which
                //     makes TFTV's BetterClasses GenerateUnit Prefix NRE + popup). Applies stat overrides
                //     + tags on first creation. Idempotent across hotkey presses / reloads.
                TacCharacterDef template = GetOrCreateProgressedClone(monster, vanillaTemplate) ?? vanillaTemplate;

                // 3. Build a descriptor from the live template (bodyparts/equipment/inventory copied).
                GeoUnitDescriptor descriptor = geo.CharacterGenerator.GenerateUnit(geo.PhoenixFaction, template);

                // 4. Spawn the GeoCharacter (registers in the level's unit list).
                GeoCharacter geoChar = descriptor.SpawnAsCharacter();
                if (geoChar == null)
                {
                    Log?.LogError($"[TheTurned] SpawnAsCharacter returned null for '{monster.Id}' — recruit aborted.");
                    return;
                }

                // 4b. Phase 3: ensure the second spec wired (auto via tags; fallback explicit).
                WireSecondarySpecIfMissing(monster, geoChar);
                if (Phase4.Enabled)
                {
                    // C1: popup-only progression. Keep slot 0 (class proficiency = identity), blank the rest.
                    geoChar.Progression?.ClearAbilityTrack(AbilityTrackSource.Personal, keepFirstAbility: true);
                    // CHUNK A: host the 5 evolution cells in the SecondaryClass in-panel track (TOP mutoid row)
                    // BEFORE the generic reshape, so the cells (already RowLength+1 with spacer) are skipped by
                    // ReshapeRuntimeTracks (Length>=RowLength+1) and the OTHER runtime tracks still reshape.
                    // Arthron-only this chunk (the sole recruit with authored cells).
                    if (monster.Id == "Arthron")
                    {
                        SpecRowFactory.HostCellsInSecondaryTrack(geoChar,
                            Monsters.Arthron.ArthronCellRow.BuildRowCells(DefUtils.Repo));
                    }
                    // Runtime tracks (Personal born 7 slots, spec tracks = CloneSlots copies) — the
                    // mutoid container indexes slots[maxLevel], so reshape each to maxLevel+1.
                    SpecRowFactory.ReshapeRuntimeTracks(geoChar);
                    ArmFollowHook.Subscribe(geoChar);   // matched-SET re-derive on every future level-up
                }

                // 4c. V1 NAKED BASE loadout. The base def the game ships (Crabby_AlienMutationVariationDef)
                //     carries spitter head + shield + elite-agile legs, so the weakest/earliest "naked"
                //     Arthron the user wants must be CONSTRUCTED: keep chassis Humanoid head + right Pincer
                //     (+SubAddon hand) + torso; drop the spit head weapon; left -> plain Crabman_LeftArm_
                //     BodyPartDef; legs -> unarmored Crabman_Legs_Agile_ItemDef. Crabman-only (HasSets).
                if (ArthronArms.HasOptions)
                {
                    ArthronArms.ApplyNakedBase(geoChar);
                }
                // Log the resulting BodypartItems once so the loadout is verifiable from Player.log.
                if (Log != null)
                {
                    string parts = string.Join(", ", geoChar.ArmourItems
                        .Select(i => i?.ItemDef?.name).Where(n => n != null));
                    Log.LogInfo($"[TheTurned] recruit base loadout for '{geoChar.GetName()}' "
                        + $"(template '{template.name}'): armour=[{parts}]");
                }

                // 5. Route into the Phoenix roster via the native grant path (GiveUnits -> AddRecruit).
                GeoSite recruitSite = geo.PhoenixFaction.Bases?.FirstOrDefault()?.Site;
                GeoFactionReward reward = new GeoFactionReward
                {
                    Reason = "TheTurned",
                    SourceFaction = geo.AlienFaction
                };
                reward.Units.Add(geoChar);
                reward.Apply(geo.PhoenixFaction, recruitSite, null);

                int count = geo.PhoenixFaction.Characters?.Count() ?? -1;
                Log?.LogInfo($"[TheTurned] Recruited '{monster.Id}' ('{template.name}') into the Phoenix roster "
                    + $"(site='{(recruitSite != null ? recruitSite.name : "null")}', faction Characters count={count}).");
            }
            catch (Exception e)
            {
                Log?.LogError($"[TheTurned] Recruit failed for '{monster.Id}': {e}");
            }
        }

        /// <summary>
        /// Returns a cloned TacCharacterDef with a non-null Data.LevelProgression, the monster class tag +
        /// shared marker appended, the class-tag cache reset, and stat overrides applied. Idempotent:
        /// re-uses the clone if it already exists. Returns null if no human LevelProgressionDef can be
        /// borrowed (caller falls back to the vanilla def, preserving prior behaviour).
        /// </summary>
        private static TacCharacterDef GetOrCreateProgressedClone(ITurnedMonster monster, TacCharacterDef vanilla)
        {
            DefRepository repo = DefUtils.Repo;
            if (repo == null)
            {
                return null;
            }

            if (repo.GetDef(monster.CloneGuid) is TacCharacterDef existing)
            {
                return existing;
            }

            LevelProgressionDef borrowed = DefUtils.BorrowHumanLevelProgression(repo);
            if (borrowed == null)
            {
                Log?.LogWarning($"[TheTurned] No human LevelProgressionDef to borrow for '{monster.Id}' — "
                    + "recruiting vanilla def (BetterClasses popup may appear).");
                return null;
            }

            TacCharacterDef clone = repo.CreateDef<TacCharacterDef>(monster.CloneGuid, vanilla);
            if (clone == null)
            {
                Log?.LogWarning($"[TheTurned] CreateDef returned null for the '{monster.Id}' clone — recruiting vanilla def.");
                return null;
            }

            // Non-null progression so the BetterClasses Prefix's ShouldGeneratePersonalAbilities read is safe.
            // REV-2 (M-PROBE step 2, formalized in M-LAYOUT.1): assign a cloned LevelProgressionDef (LevelXPTable
            // trimmed) so the mutoid ability-track container renders the intended column count. Inline here for
            // the probe; Level5Progression.cs formalizes it in M-LAYOUT.
            //
            // CELL-5 ROOT-CAUSE FIX (raw-level off-by-one): the SecondaryClass runtime track is reshaped to 8
            // slots with a spacer at idx 3 (forced: the mutoid GetAbilitySlotForLevel ALWAYS skips index
            // generatorSecondSpec-1 = 3). The 5 real cells therefore occupy slot indices 0,1,2,4,5, so
            // AbilityTrack.GetAbilitySlot​ -> GetAbilityLevel (RAW = index+1) yields cell1..cell5 RAW levels
            // 1,2,3,5,6. The native availability rule (CharacterProgression.CanLearnAbility ->
            // AbilityTrack.IsAbilitySlotAvailable: RAW level <= character level) then needs char-level >= RAW.
            // With MaxLevel 5, cell4 (RAW 5) just fits at level 5, but cell5 (RAW 6) is NEVER satisfiable
            // (max reachable level 5 < 6) -> cell5 is the unique cell the engine treats as permanently
            // unlearnable -> "clicking cell 5 does nothing". cells 1-4 are unaffected (RAW 1,2,3,5 all <= 5).
            // FIX: trim to 6 levels (MaxLevel 6) so cell5's RAW level (6) fits within MaxLevel exactly like
            // cell4's RAW (5) does — cell5 now obeys the IDENTICAL availability rule as cells 1-4. The mutoid
            // skip@idx3 still applies; with maxLevel 6 the render maps L1..L5 -> slots 0,1,2,4,5 = cells 1..5
            // and L6 -> slot 6 (empty), which the RowRenderClampPatch (5-element pool) clamps off, so no
            // phantom 6th column is shown. SecondSpecializationLevel=4 aligns the adjusted-level gate: our recruit
            // carries the MONSTER's own ClassTag (NOT the shared MutoidClassTag), so GetAbilityAdjustedLevel takes
            // the non-mutoid branch and reads THIS def's SecondSpecializationLevel. With =4 the adjusted shift
            // (RAW>=4 ? RAW-1 : RAW) maps cells {1,2,3,4,5}->RAW{1,2,3,5,6}->adjusted{1,2,3,4,5}: exactly one cell
            // per level, cell5 buyable at L5. (=0 mis-shifted ALL cells to RAW-1 -> {0,1,2,4,5} -> cells 1&2 both
            // buyable at creation.) The spacer geometry stays driven by the GENERATOR's SecondSpecializationLevel.
            LevelProgressionDef levelProg = borrowed;
            if (borrowed.LevelXPTable != null && borrowed.LevelXPTable.Length >= 6)
            {
                LevelProgressionDef clone6 = DefUtils.CloneDef<LevelProgressionDef>(repo,
                    Phase4.DeriveGuid("levelprog6:" + monster.Id).ToString(), borrowed);
                if (clone6 != null)
                {
                    clone6.name = "TheTurned_" + monster.Id + "_LevelProgression6";
                    clone6.LevelXPTable = borrowed.LevelXPTable.Take(6).ToArray();
                    clone6.SecondSpecializationLevel = 4;
                    levelProg = clone6;
                    Log?.LogInfo($"[TheTurned] 2-row layout: assigned 6-level LevelProgressionDef '{clone6.name}' "
                        + $"(MaxLevel={clone6.MaxLevel}) to '{monster.Id}' — cell5 RAW level 6 now within MaxLevel.");
                }
            }
            clone.Data.LevelProgression = new LevelProgression(levelProg);

            // Per-def Data.GameTags only (shared HumanTag/AlienTag untouched -> IsAlien stays true).
            ClassTagDef classTag = Tags.GetClassTag(repo, monster);
            GameTagDef marker = Tags.EnsureMarker(repo);
            DefUtils.AppendDataGameTag(clone, classTag);
            DefUtils.AppendDataGameTag(clone, marker);
            // Phase 3: append the SECONDARY class tag too. The generator maps the first spec-tagged tag to
            // the primary spec and any subsequent spec-tagged tag to SecondarySpecDef (FactionCharacter
            // Generator.GenerateUnit), so the 2nd "Carapace Gunner" tree auto-wires on spawn.
            if (monster.HasSecondarySpec)
            {
                ClassTagDef secondaryTag = Tags.GetSecondaryClassTag(repo, monster);
                if (secondaryTag != null)
                {
                    DefUtils.AppendDataGameTag(clone, secondaryTag);
                }
            }
            DefUtils.ResetClassTagsCache(clone);

            // Balance the cloned template (Strength/Will/Speed -> derived HP/WP/AP).
            monster.ApplyStatOverrides(clone);

            Log?.LogInfo($"[TheTurned] Cloned '{monster.Id}' '{vanilla.name}' -> '{clone.name}' (guid='{clone.Guid}'), "
                + $"LevelProgression from '{borrowed.name}', classTag='{classTag?.name}', marker='{marker?.name}', "
                + $"stats Str={clone.Data.Strength} Will={clone.Data.Will} Speed={clone.Data.Speed}.");
            return clone;
        }

        /// <summary>
        /// Phase 3 — Feature 2 recruit-time roll. Picks one right + one left arm option and bakes the result
        /// into the descriptor BEFORE spawn:
        ///  - <c>descriptor.ArmorItems</c>: swap the existing right/left arm bodypart WeaponDefs for the rolled ones.
        ///  - <c>descriptor.Progression.PersonalAbilities</c>: keep at most 2 vanilla rolled perks (the human
        ///    pool) + inject the two arm-marker abilities → exactly 4 personal slots (2 markers + 2 vanilla),
        ///    matching the user spec. Markers are ordinary personal-track abilities so PerkOracle can swap them.
        /// No-op for monsters without rolled arms or when no arm options were discovered.
        /// </summary>
        [Obsolete("Phase-4: popup progression replaced rolls")]
        private static void RollArmsIntoDescriptor(ITurnedMonster monster, GeoUnitDescriptor descriptor)
        {
            if (monster == null || !monster.HasRolledArms || descriptor == null || !ArthronArms.HasOptions)
            {
                return;
            }
            try
            {
                ArthronArms.Roll(out ArthronArms.ArmOption right, out ArthronArms.ArmOption left);

                // 1) Arm bodypart items on the descriptor (WeaponDef : TacticalItemDef).
                if (descriptor.ArmorItems != null)
                {
                    if (right?.Weapon != null)
                    {
                        ReplaceArm(descriptor.ArmorItems, ArthronArms.RightHandToken, right.Weapon);
                    }
                    if (left?.Weapon != null)
                    {
                        ReplaceArm(descriptor.ArmorItems, ArthronArms.LeftHandToken, left.Weapon);
                    }
                }

                // 2) Personal-track abilities: at most 2 vanilla + the 2 arm markers.
                GeoUnitDescriptor.ProgressionDescriptor prog = descriptor.Progression;
                if (prog?.PersonalAbilities != null)
                {
                    List<PassiveModifierAbilityDef> markers = new List<PassiveModifierAbilityDef>();
                    if (right?.Marker != null) markers.Add(right.Marker);
                    if (left?.Marker != null) markers.Add(left.Marker);
                    InjectMarkersKeepTwoVanilla(prog.PersonalAbilities, markers);
                }

                Log?.LogInfo($"[TheTurned] Rolled arms for '{monster.Id}': "
                    + $"right='{right?.Weapon?.name ?? "(none)"}', left='{left?.Weapon?.name ?? "(none)"}'.");
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] RollArmsIntoDescriptor failed: {e.Message}");
            }
        }

        /// <summary>Remove the existing arm bodypart matching <paramref name="token"/> and add the rolled one.</summary>
        [Obsolete("Phase-4: popup progression replaced rolls")]
        private static void ReplaceArm(List<TacticalItemDef> armour, string token, WeaponDef newArm)
        {
            armour.RemoveAll(d => d != null && d.name != null && d.name.Contains(token));
            armour.Add(newArm);
        }

        /// <summary>
        /// Mutate the generator's personal-ability dictionary so it ends with at most 2 vanilla entries plus
        /// the supplied marker abilities, each placed at a distinct free level key. The personal track length
        /// equals the level-progression MaxLevel, so there are always enough free keys.
        /// </summary>
        [Obsolete("Phase-4: popup progression replaced rolls")]
        private static void InjectMarkersKeepTwoVanilla(Dictionary<int, TacticalAbilityDef> personal, List<PassiveModifierAbilityDef> markers)
        {
            const int keepVanilla = 2;
            // Trim vanilla rolls down to the first `keepVanilla` (by level key order).
            List<int> vanillaKeys = personal.Keys.OrderBy(k => k).ToList();
            for (int i = keepVanilla; i < vanillaKeys.Count; i++)
            {
                personal.Remove(vanillaKeys[i]);
            }

            // Place each marker at the lowest free non-negative key.
            int nextKey = 0;
            foreach (PassiveModifierAbilityDef marker in markers)
            {
                if (marker == null)
                {
                    continue;
                }
                while (personal.ContainsKey(nextKey))
                {
                    nextKey++;
                }
                personal[nextKey] = marker;
                nextKey++;
            }
        }

        /// <summary>
        /// Phase 3 — Feature 1 safety net. The second spec normally auto-wires (the clone carries two
        /// spec-mapped class tags → generator sets SecondarySpecDef → GenerateProgression calls
        /// AddSecondaryClass). If a save / generator path didn't, add it explicitly (guarded: AddSecondaryClass
        /// throws if a secondary is already set).
        /// </summary>
        private static void WireSecondarySpecIfMissing(ITurnedMonster monster, GeoCharacter geoChar)
        {
            if (monster == null || !monster.HasSecondarySpec || geoChar?.Progression == null)
            {
                return;
            }
            try
            {
                if (geoChar.Progression.SecondarySpecDef != null)
                {
                    Log?.LogInfo($"[TheTurned] '{monster.Id}' secondary spec auto-wired ('{geoChar.Progression.SecondarySpecDef.name}').");
                    return;
                }
                DefRepository repo = DefUtils.Repo;
                SpecializationDef secondary = repo?.GetDef(monster.SecondarySpecGuid) as SpecializationDef;
                if (secondary != null && secondary != geoChar.Progression.MainSpecDef)
                {
                    geoChar.Progression.AddSecondaryClass(secondary);
                    Log?.LogInfo($"[TheTurned] '{monster.Id}' secondary spec wired via explicit fallback ('{secondary.name}').");
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] WireSecondarySpecIfMissing failed: {e.Message}");
            }
        }
    }
}
