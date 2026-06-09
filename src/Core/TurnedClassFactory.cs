using Base.Defs;
using Base.Entities.Abilities;
using Base.UI;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;

namespace TheTurned.Core
{
    /// <summary>
    /// Generic builder that constructs (idempotently) a dedicated Phoenix-side class for any
    /// <see cref="ITurnedMonster"/> so the generator attaches a real runtime Progression to the
    /// recruited unit (a non-null Progression fixes the edit-screen NRE).
    ///
    /// All shapes mirror the OfficerClass mod: per-monster <see cref="ClassTagDef"/> + the shared marker
    /// (<see cref="Tags"/>), a <see cref="SpecializationDef"/> cloned from the vanilla Sniper spec, a
    /// <see cref="ClassProficiencyAbilityDef"/> cloned from the Sniper proficiency, and an
    /// <see cref="AbilityTrackDef"/> whose 7 slots come from the monster's BuildAbilityTrack. The spec is
    /// registered into SharedData.SharedGameTags.Specializations.
    /// </summary>
    internal static class TurnedClassFactory
    {
        // Vanilla base-game GUIDs shared by all monsters (same defs the OfficerClass mod relies on).
        private const string SniperSpecGuid = "8b8510fe-f1cb-53b4-3a85-3a306c94e31f";        // SniperSpecializationDef
        private const string SniperProficiencyGuid = "54328f21-e01a-4364-0aa7-4507affd2ccf"; // Sniper_ClassProficiency_AbilityDef

        private static ModLogger Log => TheTurnedMain.Main?.Logger;

        /// <summary>
        /// Idempotently create the monster's class tag, register the shared marker, and build + register
        /// its specialization. Safe to call on every re-enable / geoscape reload. Returns true on success.
        /// </summary>
        internal static bool EnsureClass(DefRepository repo, ITurnedMonster monster)
        {
            if (repo == null || monster == null)
            {
                Log?.LogError("[TheTurned] EnsureClass called with null repo/monster.");
                return false;
            }
            try
            {
                ClassTagDef classTag = Tags.EnsureClassTag(repo, monster);
                GameTagDef marker = Tags.EnsureMarker(repo);
                SpecializationDef spec = GetOrCreateSpec(repo, monster, classTag);

                if (classTag == null || marker == null || spec == null)
                {
                    Log?.LogWarning($"[TheTurned] Class creation incomplete for '{monster.Id}' — tag/spec missing.");
                    return false;
                }

                DefUtils.RegisterSpecInSharedData(spec);
                Log?.LogInfo($"[TheTurned] Class ready for '{monster.Id}': classTag='{classTag.name}', "
                    + $"marker='{marker.name}', spec='{spec.name}'.");
                return true;
            }
            catch (Exception e)
            {
                Log?.LogError($"[TheTurned] Class creation failed for '{monster.Id}': {e}");
                return false;
            }
        }

        /// <summary>
        /// Idempotently create the monster's SECOND specialization (Carapace Gunner for Arthron): its own
        /// <see cref="ClassTagDef"/>, a Sniper-cloned <see cref="SpecializationDef"/> with a 7-slot gunner
        /// track + proficiency, registered into SharedData. No-op (returns true) for monsters without a 2nd
        /// tree. The unit gets this tree automatically because the recruiter appends the secondary class tag
        /// to the clone's Data.GameTags (the generator maps a 2nd spec-tagged tag to SecondarySpecDef).
        /// </summary>
        internal static bool EnsureSecondaryClass(DefRepository repo, ITurnedMonster monster)
        {
            if (repo == null || monster == null || !monster.HasSecondarySpec)
            {
                return true;
            }
            try
            {
                ClassTagDef classTag = Tags.EnsureSecondaryClassTag(repo, monster);
                SpecializationDef spec = GetOrCreateSecondarySpec(repo, monster, classTag);
                if (classTag == null || spec == null)
                {
                    Log?.LogWarning($"[TheTurned] Secondary class creation incomplete for '{monster.Id}'.");
                    return false;
                }
                DefUtils.RegisterSpecInSharedData(spec);
                Log?.LogInfo($"[TheTurned] Secondary class ready for '{monster.Id}': classTag='{classTag.name}', spec='{spec.name}'.");
                return true;
            }
            catch (Exception e)
            {
                Log?.LogError($"[TheTurned] Secondary class creation failed for '{monster.Id}': {e}");
                return false;
            }
        }

        private static SpecializationDef GetOrCreateSecondarySpec(DefRepository repo, ITurnedMonster monster, ClassTagDef classTag)
        {
            if (repo.GetDef(monster.SecondarySpecGuid) is SpecializationDef existing)
            {
                return existing;
            }
            SpecializationDef sniperSpec = repo.GetDef(SniperSpecGuid) as SpecializationDef;
            if (sniperSpec == null)
            {
                Log?.LogError($"[TheTurned] Sniper spec '{SniperSpecGuid}' not found — cannot clone 2nd spec for '{monster.Id}'.");
                return null;
            }
            SpecializationDef spec = repo.CreateDef<SpecializationDef>(monster.SecondarySpecGuid, sniperSpec);
            if (spec == null)
            {
                return null;
            }
            spec.name = monster.SecondarySpecName;
            spec.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + monster.SecondarySpecName;
            spec.AchievementID = "";
            spec.ClassTag = classTag;
            spec.AbilityTrack = GetOrCreateSecondaryTrack(repo, monster);
            spec.IsEliteUnit = false;
            spec.ViewElementDef = GetOrCreateSecondarySpecVed(repo, monster, sniperSpec.ViewElementDef);
            if (spec.ClassFilterText != null)
            {
                spec.ClassFilterText.LocalizationKey = monster.SecondarySpecDisplayName;
            }
            return spec;
        }

        private static AbilityTrackDef GetOrCreateSecondaryTrack(DefRepository repo, ITurnedMonster monster)
        {
            if (repo.GetDef(monster.SecondaryTrackGuid) is AbilityTrackDef existing)
            {
                return existing;
            }
            AbilityTrackDef track = repo.CreateDef<AbilityTrackDef>(monster.SecondaryTrackGuid);
            if (track == null)
            {
                return null;
            }
            track.name = "E_AbilityTrack [" + monster.SecondarySpecName + "]";
            track.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + monster.SecondarySpecName;
            ClassProficiencyAbilityDef proficiency = GetOrCreateSecondaryProficiency(repo, monster);
            track.AbilitiesByLevel = monster.BuildSecondaryAbilityTrack(repo, proficiency);
            return track;
        }

        private static ClassProficiencyAbilityDef GetOrCreateSecondaryProficiency(DefRepository repo, ITurnedMonster monster)
        {
            if (repo.GetDef(monster.SecondaryProficiencyGuid) is ClassProficiencyAbilityDef existing)
            {
                return existing;
            }
            ClassProficiencyAbilityDef sniperProf = repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef;
            if (sniperProf == null)
            {
                Log?.LogError($"[TheTurned] Sniper proficiency '{SniperProficiencyGuid}' not found for '{monster.Id}' 2nd tree.");
                return null;
            }
            ClassProficiencyAbilityDef prof = repo.CreateDef<ClassProficiencyAbilityDef>(monster.SecondaryProficiencyGuid, sniperProf);
            if (prof == null)
            {
                return null;
            }
            prof.name = $"TheTurned_{monster.Id}_GunnerProficiency_AbilityDef";
            prof.CharacterProgressionData = repo.CreateDef<AbilityCharacterProgressionDef>(
                monster.SecondaryProficiencyProgGuid, sniperProf.CharacterProgressionData);
            if (prof.CharacterProgressionData != null)
            {
                prof.CharacterProgressionData.name = "E_CharacterProgressionData [" + prof.name + "]";
            }
            prof.ViewElementDef = GetOrCreateSecondaryProficiencyVed(repo, monster, sniperProf.ViewElementDef);
            ClassTagDef classTag = Tags.GetSecondaryClassTag(repo, monster);
            prof.ClassTags = new GameTagsList(new GameTagDef[] { classTag });
            prof.AbilityDefs = new AbilityDef[0];
            return prof;
        }

        private static ViewElementDef GetOrCreateSecondarySpecVed(DefRepository repo, ITurnedMonster monster, ViewElementDef template)
        {
            if (repo.GetDef(monster.SecondarySpecVedGuid) is ViewElementDef existing)
            {
                return existing;
            }
            ViewElementDef ved = repo.CreateDef<ViewElementDef>(monster.SecondarySpecVedGuid, template);
            if (ved != null)
            {
                ved.name = "E_ViewElement [" + monster.SecondarySpecName + "]";
                ved.Name = monster.SecondarySpecDisplayName;
                ved.DisplayName1 = new LocalizedTextBind("ARTHRON_GUNNER_PROFICIENCY_NAME");
                ved.DisplayName2 = new LocalizedTextBind(monster.SecondarySpecDisplayName, true);
                ved.Description = new LocalizedTextBind("ARTHRON_GUNNER_PROFICIENCY_DESC");
                Icons.TrySetSpecIcon(ved, monster.SecondaryIconFileName);
            }
            return ved;
        }

        private static TacticalAbilityViewElementDef GetOrCreateSecondaryProficiencyVed(DefRepository repo, ITurnedMonster monster, TacticalAbilityViewElementDef template)
        {
            if (repo.GetDef(monster.SecondaryProficiencyVedGuid) is TacticalAbilityViewElementDef existing)
            {
                return existing;
            }
            TacticalAbilityViewElementDef ved = repo.CreateDef<TacticalAbilityViewElementDef>(monster.SecondaryProficiencyVedGuid, template);
            if (ved != null)
            {
                ved.name = $"E_ViewElement [TheTurned_{monster.Id}_GunnerProficiency_AbilityDef]";
                ved.Name = monster.SecondarySpecDisplayName + "Proficiency";
                ved.DisplayName1 = new LocalizedTextBind("ARTHRON_GUNNER_PROFICIENCY_NAME");
                ved.DisplayName2 = new LocalizedTextBind(monster.SecondarySpecDisplayName, true);
                ved.Description = new LocalizedTextBind("ARTHRON_GUNNER_PROFICIENCY_DESC");
                Icons.TrySetAbilityIcon(ved, monster.SecondaryIconFileName);
            }
            return ved;
        }

        private static SpecializationDef GetOrCreateSpec(DefRepository repo, ITurnedMonster monster, ClassTagDef classTag)
        {
            if (repo.GetDef(monster.SpecGuid) is SpecializationDef existing)
            {
                return existing;
            }

            SpecializationDef sniperSpec = repo.GetDef(SniperSpecGuid) as SpecializationDef;
            if (sniperSpec == null)
            {
                Log?.LogError($"[TheTurned] Sniper spec '{SniperSpecGuid}' not found — cannot clone spec for '{monster.Id}'.");
                return null;
            }

            SpecializationDef spec = repo.CreateDef<SpecializationDef>(monster.SpecGuid, sniperSpec);
            if (spec == null)
            {
                return null;
            }
            spec.name = monster.SpecName;
            spec.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + monster.SpecName;
            spec.AchievementID = "";
            spec.ClassTag = classTag;
            spec.AbilityTrack = GetOrCreateTrack(repo, monster);
            spec.IsEliteUnit = false;
            spec.ViewElementDef = GetOrCreateSpecVed(repo, monster, sniperSpec.ViewElementDef);
            if (spec.ClassFilterText != null)
            {
                spec.ClassFilterText.LocalizationKey = monster.SpecDisplayName;
            }
            return spec;
        }

        private static AbilityTrackDef GetOrCreateTrack(DefRepository repo, ITurnedMonster monster)
        {
            if (repo.GetDef(monster.TrackGuid) is AbilityTrackDef existing)
            {
                return existing;
            }
            AbilityTrackDef track = repo.CreateDef<AbilityTrackDef>(monster.TrackGuid);
            if (track == null)
            {
                return null;
            }
            track.name = "E_AbilityTrack [" + monster.SpecName + "]";
            track.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + monster.SpecName;

            ClassProficiencyAbilityDef proficiency = GetOrCreateProficiency(repo, monster);
            track.AbilitiesByLevel = monster.BuildAbilityTrack(repo, proficiency);
            return track;
        }

        private static ClassProficiencyAbilityDef GetOrCreateProficiency(DefRepository repo, ITurnedMonster monster)
        {
            if (repo.GetDef(monster.ProficiencyGuid) is ClassProficiencyAbilityDef existing)
            {
                return existing;
            }
            ClassProficiencyAbilityDef sniperProf = repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef;
            if (sniperProf == null)
            {
                Log?.LogError($"[TheTurned] Sniper proficiency '{SniperProficiencyGuid}' not found for '{monster.Id}'.");
                return null;
            }

            ClassProficiencyAbilityDef prof = repo.CreateDef<ClassProficiencyAbilityDef>(monster.ProficiencyGuid, sniperProf);
            if (prof == null)
            {
                return null;
            }
            prof.name = $"TheTurned_{monster.Id}_ClassProficiency_AbilityDef";
            prof.CharacterProgressionData = repo.CreateDef<AbilityCharacterProgressionDef>(
                monster.ProficiencyProgGuid, sniperProf.CharacterProgressionData);
            if (prof.CharacterProgressionData != null)
            {
                prof.CharacterProgressionData.name = "E_CharacterProgressionData [" + prof.name + "]";
            }
            prof.ViewElementDef = GetOrCreateProficiencyVed(repo, monster, sniperProf.ViewElementDef);
            ClassTagDef classTag = Tags.GetClassTag(repo, monster);
            prof.ClassTags = new GameTagsList(new GameTagDef[] { classTag });
            prof.AbilityDefs = new AbilityDef[0];
            return prof;
        }

        private static ViewElementDef GetOrCreateSpecVed(DefRepository repo, ITurnedMonster monster, ViewElementDef template)
        {
            if (repo.GetDef(monster.SpecVedGuid) is ViewElementDef existing)
            {
                return existing;
            }
            ViewElementDef ved = repo.CreateDef<ViewElementDef>(monster.SpecVedGuid, template);
            if (ved != null)
            {
                ved.name = "E_ViewElement [" + monster.SpecName + "]";
                ved.Name = monster.SpecDisplayName;
                ved.DisplayName1 = new LocalizedTextBind(monster.SpecDisplayName, true);
                ved.DisplayName2 = new LocalizedTextBind(monster.SpecDisplayName, true);
                ved.Description = new LocalizedTextBind(monster.SpecDescription, true);
                Icons.TrySetSpecIcon(ved, monster.IconFileName);
            }
            return ved;
        }

        private static TacticalAbilityViewElementDef GetOrCreateProficiencyVed(DefRepository repo, ITurnedMonster monster, TacticalAbilityViewElementDef template)
        {
            if (repo.GetDef(monster.ProficiencyVedGuid) is TacticalAbilityViewElementDef existing)
            {
                return existing;
            }
            TacticalAbilityViewElementDef ved = repo.CreateDef<TacticalAbilityViewElementDef>(monster.ProficiencyVedGuid, template);
            if (ved != null)
            {
                ved.name = $"E_ViewElement [TheTurned_{monster.Id}_ClassProficiency_AbilityDef]";
                ved.Name = monster.SpecDisplayName + "Proficiency";
                // Localized name/description via CSV keys (e.g. ARTHRON_PROFICIENCY_NAME/_DESC).
                string nameLocKey = monster.Id.ToUpperInvariant() + "_PROFICIENCY_NAME";
                string descLocKey = monster.Id.ToUpperInvariant() + "_PROFICIENCY_DESC";
                ved.DisplayName1 = new LocalizedTextBind(nameLocKey);
                ved.DisplayName2 = new LocalizedTextBind(monster.SpecDisplayName, true);
                ved.Description = new LocalizedTextBind(descLocKey);
                Icons.TrySetAbilityIcon(ved, monster.IconFileName);
            }
            return ved;
        }
    }
}
