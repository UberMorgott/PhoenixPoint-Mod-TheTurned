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
    ///    the personal track must be maxLevel+1 slots with index <see cref="SpacerIndex"/> a spacer
    ///    (<see cref="ReshapeWithSpacer"/>);
    ///  - rows must NOT use VehicleClassTag (popup SingleOrDefault-filters it) and set
    ///    IsUsedForProficiency=false (no ClassProficiencyAbilityDef → suppress the engine warn).
    /// </summary>
    internal static class SpecRowFactory
    {
        /// <summary>Expected MaxLevel of the borrowed human LevelProgressionDef (verified via log at feed time).</summary>
        internal const int RowLength = 7;

        /// <summary>Index the popup's hardcoded personal-track RemoveAt(3) eats (Init:124).</summary>
        internal const int SpacerIndex = 3;

        private static readonly List<SpecializationDef> _rows = new List<SpecializationDef>();
        private static bool _sniperSpecErrorLogged;

        /// <summary>All rows built so far (fed into the faction list by <see cref="FeedRows"/>).</summary>
        internal static IReadOnlyList<SpecializationDef> Rows => _rows;

        /// <summary>
        /// Idempotently build one popup row: a Sniper-cloned <see cref="SpecializationDef"/> with NO
        /// proficiency (pure perk list) and a <paramref name="cells"/>-driven track padded to
        /// <see cref="RowLength"/> non-null abilities. <paramref name="baseLocKey"/> derives the
        /// name/description keys (+"_NAME" / +"_DESC"). <paramref name="classTag"/> is the owning
        /// monster's class tag — NEVER VehicleClassTag (the popup filters it with SingleOrDefault,
        /// two would throw).
        /// </summary>
        internal static SpecializationDef GetOrCreateRow(DefRepository repo, string rowKey,
            string baseLocKey, string iconFile, ClassTagDef classTag, AbilityTrackSlot[] cells,
            string fillerIcon,
            string fillerNameLocKey = "ARTHRON_ROW_FILLER_NAME",
            string fillerDescLocKey = "ARTHRON_ROW_FILLER_DESC")
        {
            if (repo == null || string.IsNullOrEmpty(rowKey))
            {
                return null;
            }
            string specGuid = Phase4.DeriveGuid("row:" + rowKey).ToString();
            if (repo.GetDef(specGuid) is SpecializationDef existing)
            {
                // Same popup-NRE guard as below: never register a row with a missing track/VED.
                if (existing.AbilityTrack != null && existing.ViewElementDef != null && !_rows.Contains(existing))
                {
                    _rows.Add(existing);
                }
                return existing;
            }

            SpecializationDef sniperSpec = repo.GetDef(TurnedClassFactory.SniperSpecGuid) as SpecializationDef;
            if (sniperSpec == null)
            {
                if (!_sniperSpecErrorLogged)
                {
                    _sniperSpecErrorLogged = true;
                    TheTurnedMain.Main?.Logger?.LogError(
                        $"[TheTurned] Sniper spec '{TurnedClassFactory.SniperSpecGuid}' not found — cannot clone popup rows (first miss: '{rowKey}').");
                }
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
            spec.ClassTag = classTag;
            if (spec.ClassTag == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] Null class tag passed for row '{rowKey}' — row keeps null ClassTag.");
            }
            if (spec.ClassFilterText != null)
            {
                spec.ClassFilterText.LocalizationKey = baseLocKey + "_NAME";
            }

            AbilityTrackDef track = GetOrCreateRowTrack(repo, rowKey, cells, fillerIcon, fillerNameLocKey, fillerDescLocKey);
            var ved = PerkFactory.BuildRowVed(repo,
                Phase4.DeriveGuid("rowved:" + rowKey).ToString(),
                spec.name + "_Ved", baseLocKey + "_NAME", baseLocKey + "_DESC", iconFile);
            if (track == null || ved == null)
            {
                // Popup NRE guard: a row without a track or VED would null-deref in Init — don't register it.
                TheTurnedMain.Main?.Logger?.LogError(
                    $"[TheTurned] Row '{rowKey}' track/VED build failed (track={(track == null ? "null" : "ok")}, ved={(ved == null ? "null" : "ok")}) — row NOT registered.");
                return null;
            }
            spec.AbilityTrack = track;
            spec.ViewElementDef = ved;

            _rows.Add(spec);
            return spec;
        }

        private static AbilityTrackDef GetOrCreateRowTrack(DefRepository repo, string rowKey,
            AbilityTrackSlot[] cells, string fillerIcon, string fillerNameLocKey, string fillerDescLocKey)
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
            // Rows stay RowLength (7), NOT maxLevel+1: only tracks in character.Progression.AbilityTracks
            // pass through MutoidAbilityTrackContainerElement.GetAbilitySlotForLevel (slots[maxLevel],
            // needs 8). Row tracks render solely in SpecializedAbilityTrackPopupElement.Init:122, which
            // reads AbilitiesByLevel.Where(Ability != null) indexed [0..maxLevel) — 7 non-null cells.
            track.AbilitiesByLevel = PadRow(repo, rowKey, cells, RowLength, fillerIcon, fillerNameLocKey, fillerDescLocKey);
            return track;
        }

        /// <summary>
        /// Rows must have exactly <paramref name="length"/> non-null-Ability cells (the popup reads
        /// [0..maxLevel) and dereferences each ability's progression data). Design cells are kept; any
        /// shortfall / null cell becomes a unique cheap filler passive (+2 Willpower, cost 10/10).
        /// </summary>
        internal static AbilityTrackSlot[] PadRow(DefRepository repo, string rowKey, AbilityTrackSlot[] cells,
            int length, string fillerIcon, string fillerNameLocKey, string fillerDescLocKey)
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
                    fillerNameLocKey, fillerDescLocKey, fillerIcon,
                    skillPointCost: 10, mutagenCost: 10,
                    new[] { PerkFactory.Add(StatModificationTarget.Willpower, 2f) });
                result[i] = new AbilityTrackSlot { Ability = filler, RequiresPrevAbility = false };
            }
            return result;
        }

        /// <summary>
        /// Personal-track reshape for the popup quirk: copy <paramref name="source"/> into
        /// <paramref name="totalLength"/> slots leaving <paramref name="spacerIndex"/> an empty slot
        /// (the popup's hardcoded RemoveAt(3) eats it). Idempotent: already-long-enough arrays pass
        /// through. NOTE: the pass-through assumes such a source was already spacered; a NATURAL 8-slot
        /// source would lose its index-3 slot to the popup's RemoveAt(3).
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
        /// Runtime counterpart of <see cref="ReshapeWithSpacer"/>: EVERY runtime <c>AbilityTrack</c>
        /// INSTANCE the character carries goes through MutoidAbilityTrackContainerElement.
        /// GetAbilitySlotForLevel, which indexes slots[maxLevel] (decompile :78-97) — needs
        /// maxLevel+1. The Personal track is born MaxLevel (7) slots (CharacterProgression ctor:106);
        /// the Primary/SecondaryClass tracks are CloneSlots COPIES of the def tracks SERIALIZED into
        /// the save, so the def-level reshape (TurnedClassFactory) never reaches pre-fix clones.
        /// Reshapes each short track in place and re-wires the slot→track back-refs the AbilityTrack
        /// ctor would normally set (LearnAbility reads slot.AbilityTrack). Idempotent; returns true
        /// when at least one track was actually resized.
        /// </summary>
        internal static bool ReshapeRuntimeTracks(GeoCharacter geoChar)
        {
            IReadOnlyList<AbilityTrack> tracks = geoChar?.Progression?.AbilityTracks;
            if (tracks == null)
            {
                return false;
            }
            bool resized = false;
            foreach (AbilityTrack track in tracks)
            {
                if (track?.AbilitiesByLevel == null || track.AbilitiesByLevel.Length >= RowLength + 1)
                {
                    continue;
                }
                int oldLength = track.AbilitiesByLevel.Length;
                track.AbilitiesByLevel = ReshapeWithSpacer(track.AbilitiesByLevel,
                    totalLength: RowLength + 1, spacerIndex: SpacerIndex);
                foreach (AbilityTrackSlot slot in track.AbilitiesByLevel)
                {
                    slot.SetAbilityTrack(track);
                }
                TheTurnedMain.LogInfo($"[TheTurned] {track.Source} track reshaped {oldLength}→{RowLength + 1} for '{geoChar.GetName()}'.");
                resized = true;
            }
            return resized;
        }

        /// <summary>
        /// CHUNK A: host the recruit's 5 evolution <paramref name="cells"/> in its SecondaryClass in-panel track
        /// (the TOP visible mutoid row — RefreshAbilityTracks routes Source==SecondaryClass to the secondary
        /// pool, AbilityTrackContainerElement.cs:154-156). Reshapes the 5 cells to RowLength+1 (8) with the
        /// index-<see cref="SpacerIndex"/> (3) spacer — EXACTLY like <see cref="ReshapeRuntimeTracks"/> — so the
        /// mutoid container's GetAbilitySlotForLevel (skips slot where i+1==SecondSpecializationLevel==4 → idx 3)
        /// maps cell-index i to visible level i+1: levels 1-3 → cells 1-3 (idx 0-2), level 4 → cell 4 (idx 4),
        /// level 5 → cell 5 (idx 5). Each slot's back-pointer is set via SetAbilityTrack (GetAbilityLevel needs
        /// it). Idempotent: re-running rewrites the same cells. Returns true when the track was found + written.
        /// </summary>
        internal static bool HostCellsInSecondaryTrack(GeoCharacter geoChar, AbilityTrackSlot[] cells)
        {
            if (geoChar?.Progression?.AbilityTracks == null || cells == null || cells.Length == 0)
            {
                return false;
            }
            AbilityTrack secondary = geoChar.Progression.AbilityTracks
                .FirstOrDefault(t => t != null && t.Source == AbilityTrackSource.SecondaryClass);
            if (secondary == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] HostCells: no SecondaryClass runtime track for '{geoChar.GetName()}' — cells not hosted.");
                return false;
            }
            secondary.AbilitiesByLevel = ReshapeWithSpacer(cells, totalLength: RowLength + 1, spacerIndex: SpacerIndex);
            foreach (AbilityTrackSlot slot in secondary.AbilitiesByLevel)
            {
                slot.SetAbilityTrack(secondary);
            }
            TheTurnedMain.LogInfo($"[TheTurned] HostCells: {cells.Length} evolution cells hosted in the SecondaryClass "
                + $"track ({secondary.AbilitiesByLevel.Length} slots, spacer@{SpacerIndex}) for '{geoChar.GetName()}'.");
            return true;
        }

        /// <summary>
        /// Feed all built rows into <c>GeoPhoenixFaction.AvailablePandoranSpecialzations</c>.
        /// Idempotent (Contains-guarded); silent when nothing new was added (no log, no recruit scan).
        /// </summary>
        internal static void FeedRows(GeoLevelController geo)
        {
            if (!Phase4.Enabled || geo?.PhoenixFaction == null)
            {
                return;
            }
            List<SpecializationDef> list = geo.PhoenixFaction.AvailablePandoranSpecialzations;
            int added = 0;
            foreach (SpecializationDef row in _rows)
            {
                if (!list.Contains(row))
                {
                    list.Add(row);
                    added++;
                }
            }
            if (added == 0)
            {
                return;
            }
            TheTurnedMain.LogInfo($"[TheTurned] FeedRows: {added} rows fed, pandoran specs now {list.Count}.");
            ProbeRecruitMaxLevel(geo);
        }

        /// <summary>
        /// RowLength sanity probe: log the first marked recruit's LevelProgression.Def.MaxLevel.
        /// A mismatch is ALWAYS surfaced as a WARNING (rows would render short/overflow).
        /// </summary>
        private static void ProbeRecruitMaxLevel(GeoLevelController geo)
        {
            GeoCharacter recruit = geo.PhoenixFaction.Characters?.FirstOrDefault(Phase4.IsPhase4Recruit);
            int? maxLevel = recruit?.LevelProgression?.Def?.MaxLevel;
            if (maxLevel == null)
            {
                TheTurnedMain.LogInfo("[TheTurned] FeedRows: no marked recruit with LevelProgression present (maxLevel unverified).");
            }
            else if (maxLevel.Value != RowLength)
            {
                TheTurnedMain.LogWarn($"[TheTurned] FeedRows: recruit MaxLevel={maxLevel.Value} != RowLength={RowLength} — rows may render short/overflow.");
            }
            else
            {
                TheTurnedMain.LogInfo($"[TheTurned] FeedRows: recruit MaxLevel={maxLevel.Value} matches RowLength.");
            }
        }
    }
}
