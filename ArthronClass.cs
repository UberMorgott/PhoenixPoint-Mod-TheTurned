using Base.Core;
using Base.Defs;
using Base.Entities.Abilities;
using Base.UI;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities.Abilities;
using System;
using System.Linq;
using UnityEngine;

namespace TheTurned
{
    /// <summary>
    /// Builds (idempotently) a dedicated Phoenix-side class for the recruited Arthron so the
    /// generator (<see cref="PhoenixPoint.Geoscape.Core.FactionCharacterGenerator.GenerateUnit"/>)
    /// attaches a real runtime Progression to it. A non-null Progression =>
    /// <see cref="PhoenixPoint.Geoscape.Entities.GeoCharacter.LevelProgression"/> non-null, which
    /// fixes the edit-screen NRE (UIStateEditSoldier.cs:601, ShowHumanProgression :499).
    ///
    /// All shapes mirror the OfficerClass mod (refs\Officer-src): ClassTagDef + marker GameTagDef
    /// (Tags.cs), SpecializationDef cloned from Sniper (Specialisation.cs), ClassProficiencyAbilityDef
    /// cloned from Sniper (ClassProficiency.cs), AbilityTrackDef with 7 AbilityTrackSlots, registered
    /// into SharedData.SharedGameTags.Specializations (Tags.cs:84-89). Vanilla GUIDs (Sniper spec /
    /// proficiency) are the same base-game defs the Officer mod relies on.
    /// </summary>
    internal static class ArthronClass
    {
        // Vanilla base-game GUIDs (same as OfficerClass relies on).
        private const string SniperSpecGuid = "8b8510fe-f1cb-53b4-3a85-3a306c94e31f";        // SniperSpecializationDef
        private const string SniperProficiencyGuid = "54328f21-e01a-4364-0aa7-4507affd2ccf"; // Sniper_ClassProficiency_AbilityDef

        // Our invented stable GUIDs (idempotent: GetDef returns the existing def on re-enable).
        private const string ClassTagGuid = "b2d4f6a8-1c3e-4a5b-8d7f-2e9c0a1b3d5e";
        private const string MarkerTagGuid = "c3e5a7b9-2d4f-5b6c-9e8a-3f0d1b2c4e6f";
        private const string SpecGuid = "d4f6b8ca-3e5a-6c7d-af9b-4a1e2c3d5f70";
        private const string TrackGuid = "e5a7c9db-4f6b-7d8e-ba0c-5b2f3d4e6081";
        private const string ProficiencyGuid = "f6b8da0c-5a7c-8e9f-cb1d-6c3a4e5f7192";
        private const string ProficiencyProgGuid = "a7c9eb1d-6b8d-9fa0-dc2e-7d4b5f608203";
        private const string SpecVedGuid = "b8da0c2e-7c9e-a0b1-ed3f-8e5c60719314";
        private const string ProficiencyVedGuid = "c9eb1d3f-8da0-b1c2-fe40-9f6d71820425";

        // Public so the recruiter + Harmony patch can reference them.
        internal const string ClassTagName = "TheTurned_Arthron_ClassTagDef";
        internal const string MarkerTagName = "TheTurned_RecruitTag";
        internal const string SpecName = "TheTurned_ArthronSpecializationDef";

        internal static ClassTagDef ArthronClassTag { get; private set; }
        internal static GameTagDef RecruitMarkerTag { get; private set; }
        internal static SpecializationDef ArthronSpec { get; private set; }

        private static ModLogger Log => TheTurnedMain.Main?.Logger;
        private static DefRepository Repo => GameUtl.GameComponent<DefRepository>();

        /// <summary>
        /// Idempotently creates the class tag, marker tag and specialization, and registers the
        /// spec into SharedData so the generator's cached SpecializationsDefs list (built in
        /// FactionCharacterGenerator.Start, before any geoscape) discovers it. Call once from
        /// OnModEnabled. Returns true if the class is available.
        /// </summary>
        internal static bool EnsureCreated()
        {
            try
            {
                DefRepository repo = Repo;
                if (repo == null)
                {
                    Log?.LogError("[TheTurned] DefRepository unavailable — Arthron class not created.");
                    return false;
                }

                ArthronClassTag = GetOrCreateClassTag(repo);
                RecruitMarkerTag = GetOrCreateMarkerTag(repo);
                ArthronSpec = GetOrCreateSpec(repo);

                if (ArthronClassTag == null || RecruitMarkerTag == null || ArthronSpec == null)
                {
                    Log?.LogWarning("[TheTurned] Arthron class creation incomplete — tag/spec missing.");
                    return false;
                }

                RegisterSpecInSharedData(ArthronSpec);
                Log?.LogInfo($"[TheTurned] Arthron class ready: classTag='{ArthronClassTag.name}', "
                    + $"marker='{RecruitMarkerTag.name}', spec='{ArthronSpec.name}'.");
                return true;
            }
            catch (Exception e)
            {
                Log?.LogError($"[TheTurned] Arthron class creation failed: {e}");
                return false;
            }
        }

        private static ClassTagDef GetOrCreateClassTag(DefRepository repo)
        {
            if (repo.GetDef(ClassTagGuid) is ClassTagDef existing)
            {
                return existing;
            }
            ClassTagDef tag = repo.CreateDef<ClassTagDef>(ClassTagGuid);
            if (tag != null)
            {
                tag.name = ClassTagName;
                tag.ResourcePath = "Defs/GameTags/Classes/" + ClassTagName;
            }
            return tag;
        }

        private static GameTagDef GetOrCreateMarkerTag(DefRepository repo)
        {
            if (repo.GetDef(MarkerTagGuid) is GameTagDef existing)
            {
                return existing;
            }
            GameTagDef tag = repo.CreateDef<GameTagDef>(MarkerTagGuid);
            if (tag != null)
            {
                tag.name = MarkerTagName;
                tag.ResourcePath = "Defs/GameTags/" + MarkerTagName;
            }
            return tag;
        }

        private static SpecializationDef GetOrCreateSpec(DefRepository repo)
        {
            if (repo.GetDef(SpecGuid) is SpecializationDef existing)
            {
                return existing;
            }

            SpecializationDef sniperSpec = repo.GetDef(SniperSpecGuid) as SpecializationDef;
            if (sniperSpec == null)
            {
                Log?.LogError($"[TheTurned] Sniper spec '{SniperSpecGuid}' not found — cannot clone Arthron spec.");
                return null;
            }

            SpecializationDef spec = repo.CreateDef<SpecializationDef>(SpecGuid, sniperSpec);
            if (spec == null)
            {
                return null;
            }
            spec.name = SpecName;
            spec.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + SpecName;
            spec.AchievementID = "";
            spec.ClassTag = ArthronClassTag;
            spec.AbilityTrack = GetOrCreateTrack(repo, sniperSpec);
            spec.IsEliteUnit = false;
            spec.ViewElementDef = GetOrCreateSpecVed(repo, sniperSpec.ViewElementDef);
            if (spec.ClassFilterText != null)
            {
                spec.ClassFilterText.LocalizationKey = "TheTurned Arthron";
            }
            return spec;
        }

        /// <summary>
        /// 7-slot AbilityTrack. Slot 0 = our cloned class proficiency (carries the new class tag);
        /// slots 1-6 = the Sniper track's own abilities (valid, already-localized vanilla defs) so
        /// CharacterProgression construction never hits a null ability.
        /// </summary>
        private static AbilityTrackDef GetOrCreateTrack(DefRepository repo, SpecializationDef sniperSpec)
        {
            if (repo.GetDef(TrackGuid) is AbilityTrackDef existing)
            {
                return existing;
            }
            AbilityTrackDef track = repo.CreateDef<AbilityTrackDef>(TrackGuid);
            if (track == null)
            {
                return null;
            }
            track.name = "E_AbilityTrack [" + SpecName + "]";
            track.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + SpecName;

            ClassProficiencyAbilityDef proficiency = GetOrCreateProficiency(repo);

            // Reuse the Sniper track's vanilla abilities for slots 1-6 (valid + localized).
            AbilityTrackSlot[] sniperSlots = sniperSpec.AbilityTrack?.AbilitiesByLevel;
            AbilityTrackSlot[] slots = new AbilityTrackSlot[7];
            slots[0] = new AbilityTrackSlot { Ability = proficiency, RequiresPrevAbility = false };
            for (int i = 1; i < 7; i++)
            {
                TacticalAbilityDef vanilla = null;
                if (sniperSlots != null && i < sniperSlots.Length)
                {
                    // Skip the Sniper's own proficiency slot (slot 0); pick the next non-proficiency.
                    AbilityTrackSlot src = sniperSlots[i];
                    if (src != null && !(src.Ability is ClassProficiencyAbilityDef))
                    {
                        vanilla = src.Ability;
                    }
                }
                slots[i] = new AbilityTrackSlot { Ability = vanilla, RequiresPrevAbility = false };
            }
            track.AbilitiesByLevel = slots;
            return track;
        }

        private static ClassProficiencyAbilityDef GetOrCreateProficiency(DefRepository repo)
        {
            if (repo.GetDef(ProficiencyGuid) is ClassProficiencyAbilityDef existing)
            {
                return existing;
            }
            ClassProficiencyAbilityDef sniperProf = repo.GetDef(SniperProficiencyGuid) as ClassProficiencyAbilityDef;
            if (sniperProf == null)
            {
                Log?.LogError($"[TheTurned] Sniper proficiency '{SniperProficiencyGuid}' not found.");
                return null;
            }

            ClassProficiencyAbilityDef prof = repo.CreateDef<ClassProficiencyAbilityDef>(ProficiencyGuid, sniperProf);
            if (prof == null)
            {
                return null;
            }
            prof.name = "TheTurned_Arthron_ClassProficiency_AbilityDef";
            prof.CharacterProgressionData = repo.CreateDef<AbilityCharacterProgressionDef>(
                ProficiencyProgGuid, sniperProf.CharacterProgressionData);
            if (prof.CharacterProgressionData != null)
            {
                prof.CharacterProgressionData.name = "E_CharacterProgressionData [" + prof.name + "]";
            }
            prof.ViewElementDef = GetOrCreateProficiencyVed(repo, sniperProf.ViewElementDef);
            prof.ClassTags = new GameTagsList(new GameTagDef[] { ArthronClassTag });
            prof.AbilityDefs = new AbilityDef[0];
            return prof;
        }

        private static ViewElementDef GetOrCreateSpecVed(DefRepository repo, ViewElementDef template)
        {
            if (repo.GetDef(SpecVedGuid) is ViewElementDef existing)
            {
                return existing;
            }
            ViewElementDef ved = repo.CreateDef<ViewElementDef>(SpecVedGuid, template);
            if (ved != null)
            {
                ved.name = "E_ViewElement [" + SpecName + "]";
                ved.Name = "Arthron";
                ved.DisplayName1 = new LocalizedTextBind("Arthron", true);
                ved.DisplayName2 = new LocalizedTextBind("Recruited Arthron", true);
                ved.Description = new LocalizedTextBind("A turned Pandoran Arthron.", true);
            }
            return ved;
        }

        private static TacticalAbilityViewElementDef GetOrCreateProficiencyVed(DefRepository repo, TacticalAbilityViewElementDef template)
        {
            if (repo.GetDef(ProficiencyVedGuid) is TacticalAbilityViewElementDef existing)
            {
                return existing;
            }
            TacticalAbilityViewElementDef ved = repo.CreateDef<TacticalAbilityViewElementDef>(ProficiencyVedGuid, template);
            if (ved != null)
            {
                ved.name = "E_ViewElement [TheTurned_Arthron_ClassProficiency_AbilityDef]";
                ved.Name = "ArthronProficiency";
                ved.DisplayName1 = new LocalizedTextBind("Arthron Proficiency", true);
                ved.DisplayName2 = new LocalizedTextBind("Arthron", true);
                ved.Description = new LocalizedTextBind("Pandoran Arthron proficiency.", true);
            }
            return ved;
        }

        private static void RegisterSpecInSharedData(SpecializationDef spec)
        {
            SharedData shared = GameUtl.GameComponent<SharedData>();
            if (shared?.SharedGameTags == null)
            {
                Log?.LogWarning("[TheTurned] SharedGameTags unavailable — spec not registered (generator may still discover it via repo).");
                return;
            }
            SpecializationDef[] specs = shared.SharedGameTags.Specializations ?? new SpecializationDef[0];
            if (!specs.Contains(spec))
            {
                shared.SharedGameTags.Specializations = specs.Append(spec).ToArray();
            }
        }
    }
}
