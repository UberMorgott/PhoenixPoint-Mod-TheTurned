using Base.Defs;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Builds the Phase-4 themed SpecializationDef ROWS shown in the native mutoid level-up popup
    /// (<c>SpecializedAbilityTrackPopupElement</c>) and feeds them into
    /// <c>GeoPhoenixFaction.AvailablePandoranSpecialzations</c> (EXACT engine spelling — missing "i",
    /// GeoPhoenixFaction.cs:228).
    ///
    /// Popup contract (decompile, SpecializedAbilityTrackPopupElement.Init:68-165):
    ///  - each ROW's track is read [0..maxLevel) → rows need ≥maxLevel cells with non-null Ability
    ///    (cost reads ability.CharacterProgressionData.MutagenCost; null prog data LogErrors) — shortfall
    ///    is padded with cheap unique filler passives (<see cref="PadRow"/>);
    ///  - the character's PERSONAL track gets a hardcoded RemoveAt(3) then is read [0..maxLevel) →
    ///    the personal track must be maxLevel+1 slots with index 3 a spacer (<see cref="ReshapeWithSpacer"/>);
    ///  - rows must NOT use VehicleClassTag (popup SingleOrDefault-filters it) and set
    ///    IsUsedForProficiency=false (no ClassProficiencyAbilityDef → suppress the engine warn).
    /// </summary>
    internal static class SpecRowFactory
    {
        /// <summary>Expected MaxLevel of the borrowed human LevelProgressionDef (verified via log at feed time).</summary>
        internal const int RowLength = 7;

        private static readonly List<SpecializationDef> _rows = new List<SpecializationDef>();

        /// <summary>All rows built so far (fed into the faction list by <see cref="FeedRows"/>).</summary>
        internal static IReadOnlyList<SpecializationDef> Rows => _rows;

        /// <summary>
        /// Idempotently build one popup row: a Sniper-cloned <see cref="SpecializationDef"/> with NO
        /// proficiency (pure perk list) and a <paramref name="cells"/>-driven track padded to
        /// <see cref="RowLength"/> non-null abilities.
        /// </summary>
        internal static SpecializationDef GetOrCreateRow(DefRepository repo, string rowKey,
            string nameLocKey, string iconFile, AbilityTrackSlot[] cells)
        {
            if (repo == null || string.IsNullOrEmpty(rowKey))
            {
                return null;
            }
            string specGuid = Phase4.DeriveGuid("row:" + rowKey).ToString();
            if (repo.GetDef(specGuid) is SpecializationDef existing)
            {
                if (!_rows.Contains(existing))
                {
                    _rows.Add(existing);
                }
                return existing;
            }

            SpecializationDef sniperSpec = repo.GetDef(TurnedClassFactory.SniperSpecGuid) as SpecializationDef;
            if (sniperSpec == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] Sniper spec not found — cannot clone row '{rowKey}'.");
                return null;
            }
            SpecializationDef spec = repo.CreateDef<SpecializationDef>(specGuid, sniperSpec);
            if (spec == null)
            {
                return null;
            }
            spec.name = $"TheTurned_ArthronRow_{rowKey}_SpecializationDef";
            spec.ResourcePath = "Defs/Common/TacUnitClasses/SpecializationDef/" + spec.name;
            spec.AchievementID = "";
            spec.IsEliteUnit = false;
            // Rows carry no ClassProficiencyAbilityDef — suppress GetSpecProficiency's engine warn.
            spec.IsUsedForProficiency = false;
            spec.NotSecondClassSpecialization = true;
            // NEVER VehicleClassTag (the popup filters it with SingleOrDefault — two would throw).
            spec.ClassTag = ResolveArthronClassTag(repo);
            if (spec.ClassTag == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] Arthron class tag unresolved for row '{rowKey}' — row keeps null ClassTag.");
            }
            if (spec.ClassFilterText != null)
            {
                spec.ClassFilterText.LocalizationKey = nameLocKey;
            }
            spec.AbilityTrack = GetOrCreateRowTrack(repo, rowKey, cells);
            spec.ViewElementDef = PerkFactory.BuildRowVed(repo,
                Phase4.DeriveGuid("rowved:" + rowKey).ToString(),
                spec.name + "_Ved", nameLocKey, nameLocKey + "_DESC", iconFile);

            _rows.Add(spec);
            return spec;
        }

        private static AbilityTrackDef GetOrCreateRowTrack(DefRepository repo, string rowKey, AbilityTrackSlot[] cells)
        {
            string trackGuid = Phase4.DeriveGuid("rowtrack:" + rowKey).ToString();
            if (repo.GetDef(trackGuid) is AbilityTrackDef existing)
            {
                return existing;
            }
            AbilityTrackDef track = repo.CreateDef<AbilityTrackDef>(trackGuid);
            if (track == null)
            {
                return null;
            }
            track.name = $"TheTurned_ArthronRow_{rowKey}_AbilityTrackDef";
            track.ResourcePath = "Defs/Common/TacUnitClasses/AbilityTrackDef/" + track.name;
            track.AbilitiesByLevel = PadRow(repo, rowKey, cells, RowLength);
            return track;
        }

        /// <summary>
        /// Rows must have exactly <paramref name="length"/> non-null-Ability cells (the popup reads
        /// [0..maxLevel) and dereferences each ability's progression data). Design cells are kept; any
        /// shortfall / null cell becomes a unique cheap filler passive (+2 Willpower, cost 10/10).
        /// </summary>
        internal static AbilityTrackSlot[] PadRow(DefRepository repo, string rowKey, AbilityTrackSlot[] cells, int length)
        {
            AbilityTrackSlot[] result = new AbilityTrackSlot[length];
            int cellCount = cells != null ? cells.Length : 0;
            for (int i = 0; i < length; i++)
            {
                if (i < cellCount && cells[i]?.Ability != null)
                {
                    result[i] = cells[i];
                    continue;
                }
                string seed = $"filler:{rowKey}:{i}";
                var filler = PerkFactory.BuildStatPassive(repo,
                    Phase4.DeriveGuid(seed).ToString(),
                    $"TheTurned_ArthronRow_{rowKey}_Filler{i}_AbilityDef",
                    Phase4.DeriveGuid(seed + "|prog").ToString(),
                    Phase4.DeriveGuid(seed + "|ved").ToString(),
                    "ARTHRON_ROW_FILLER_NAME", "ARTHRON_ROW_FILLER_DESC", "Arthron_Spec.png",
                    skillPointCost: 10, mutagenCost: 10,
                    new[] { PerkFactory.Add(StatModificationTarget.Willpower, 2f) });
                result[i] = new AbilityTrackSlot { Ability = filler, RequiresPrevAbility = false };
            }
            return result;
        }

        /// <summary>
        /// Personal-track reshape for the popup quirk: copy <paramref name="source"/> into
        /// <paramref name="totalLength"/> slots leaving <paramref name="spacerIndex"/> an empty slot
        /// (the popup's hardcoded RemoveAt(3) eats it). Idempotent: already-long-enough arrays pass through.
        /// </summary>
        internal static AbilityTrackSlot[] ReshapeWithSpacer(AbilityTrackSlot[] source, int totalLength, int spacerIndex)
        {
            if (source == null)
            {
                return null;
            }
            if (source.Length >= totalLength)
            {
                return source;
            }
            AbilityTrackSlot[] result = new AbilityTrackSlot[totalLength];
            int src = 0;
            for (int i = 0; i < totalLength; i++)
            {
                if (i == spacerIndex)
                {
                    result[i] = new AbilityTrackSlot();
                    continue;
                }
                result[i] = src < source.Length ? source[src++] : new AbilityTrackSlot();
            }
            return result;
        }

        /// <summary>
        /// Feed all built rows into <c>GeoPhoenixFaction.AvailablePandoranSpecialzations</c>.
        /// Idempotent (Contains-guarded). Also logs a marked recruit's MaxLevel (RowLength sanity check).
        /// </summary>
        internal static void FeedRows(GeoLevelController geo)
        {
            if (!Phase4.Enabled || geo?.PhoenixFaction == null)
            {
                return;
            }
            List<SpecializationDef> list = geo.PhoenixFaction.AvailablePandoranSpecialzations;
            foreach (SpecializationDef row in _rows)
            {
                if (!list.Contains(row))
                {
                    list.Add(row);
                }
            }
            TheTurnedMain.LogInfo($"[TheTurned] FeedRows: {_rows.Count} rows fed, pandoran specs now {list.Count}. "
                + DescribeRecruitMaxLevel(geo));
        }

        /// <summary>RowLength sanity probe: log the first marked recruit's LevelProgression.Def.MaxLevel.</summary>
        private static string DescribeRecruitMaxLevel(GeoLevelController geo)
        {
            GeoCharacter recruit = geo.PhoenixFaction.Characters?.FirstOrDefault(Phase4.IsPhase4Recruit);
            if (recruit == null)
            {
                return "No marked recruit present (maxLevel unverified).";
            }
            int? maxLevel = recruit.LevelProgression?.Def?.MaxLevel;
            if (maxLevel == null)
            {
                return "Marked recruit has null LevelProgression (maxLevel unverified).";
            }
            return maxLevel.Value == RowLength
                ? $"Recruit MaxLevel={maxLevel.Value} matches RowLength."
                : $"WARNING: recruit MaxLevel={maxLevel.Value} != RowLength={RowLength} — rows may render short/overflow.";
        }

        /// <summary>The Arthron's class tag (created by Tags.EnsureClassTag during BuildAllClasses).</summary>
        private static ClassTagDef ResolveArthronClassTag(DefRepository repo)
        {
            ITurnedMonster arthron = MonsterRegistry.All.FirstOrDefault(m => m != null && m.Id == "Arthron");
            return arthron != null ? Tags.GetClassTag(repo, arthron) : null;
        }
    }
}
