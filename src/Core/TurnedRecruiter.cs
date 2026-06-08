using Base.Core;
using Base.Defs;
using Base.Levels;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
using PhoenixPoint.Geoscape.Core;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.Levels;
using PhoenixPoint.Geoscape.Levels.Factions;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities;
using System;
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
            clone.Data.LevelProgression = new LevelProgression(borrowed);

            // Per-def Data.GameTags only (shared HumanTag/AlienTag untouched -> IsAlien stays true).
            ClassTagDef classTag = Tags.GetClassTag(repo, monster);
            GameTagDef marker = Tags.EnsureMarker(repo);
            DefUtils.AppendDataGameTag(clone, classTag);
            DefUtils.AppendDataGameTag(clone, marker);
            DefUtils.ResetClassTagsCache(clone);

            // Balance the cloned template (Strength/Will/Speed -> derived HP/WP/AP).
            monster.ApplyStatOverrides(clone);

            Log?.LogInfo($"[TheTurned] Cloned '{monster.Id}' '{vanilla.name}' -> '{clone.name}' (guid='{clone.Guid}'), "
                + $"LevelProgression from '{borrowed.name}', classTag='{classTag?.name}', marker='{marker?.name}', "
                + $"stats Str={clone.Data.Strength} Will={clone.Data.Will} Speed={clone.Data.Speed}.");
            return clone;
        }
    }
}
