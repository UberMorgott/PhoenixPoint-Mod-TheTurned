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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace TheTurned
{
    /// <summary>
    /// Grounded recruit chain (research note §2). Resolves a basic vanilla Arthron
    /// (codename Crabman) TacCharacterDef at runtime, builds a perk-less GeoUnitDescriptor,
    /// spawns it as a GeoCharacter and grants it to the Phoenix faction via GeoFactionReward.
    /// All game API signatures verified against the decompiled source.
    /// </summary>
    internal static class ArthronRecruiter
    {
        private const string CrabmanClassTagName = "Crabman_ClassTagDef";

        // Stable invented GUID for our cloned, progression-bearing Arthron def.
        // Idempotent: GetDef(this) on repeat hotkey presses returns the existing clone.
        private const string ProgressedArthronGuid = "TheTurned_ProgressedArthron_5f3a1c20-7b41-4e9d-9c2a-1d6e8f0a2b34";

        private static ModLogger Log => TheTurnedMain.Main?.Logger;

        public static void RecruitOne()
        {
            try
            {
                // 1. Geoscape guard: bail outside the geoscape (e.g. tactical) or if faction missing.
                Level level = GameUtl.CurrentLevel();
                GeoLevelController geo = level?.GetComponent<GeoLevelController>();
                if (geo == null || geo.PhoenixFaction == null)
                {
                    Log?.LogInfo("[TheTurned] Hotkey ignored — not on an active geoscape.");
                    return;
                }

                // 2. Resolve a basic Arthron TacCharacterDef by the Crabman class tag.
                TacCharacterDef vanillaTemplate = ResolveBasicArthron();
                if (vanillaTemplate == null)
                {
                    Log?.LogError("[TheTurned] No Arthron (Crabman) TacCharacterDef found — recruit aborted.");
                    return;
                }
                Log?.LogInfo($"[TheTurned] Resolved Arthron template name='{vanillaTemplate.name}' guid='{vanillaTemplate.Guid}'.");

                // 2b. Use a cloned Arthron def whose Data.LevelProgression is non-null. Pandoran
                //     TacCharacterDefs have a null Data.LevelProgression, which makes TFTV's
                //     BetterClasses GenerateUnit Prefix deref null (template.Data.LevelProgression
                //     .ShouldGeneratePersonalAbilities) -> caught NRE -> popup. Borrowing a valid
                //     LevelProgressionDef from a human soldier and recruiting the clone removes the
                //     popup. (The clone has no Phoenix spec for the Crabman tag, so the vanilla
                //     GenerateUnit never builds a runtime Progression — behaviour is unchanged.)
                TacCharacterDef template = GetOrCreateProgressedArthron(vanillaTemplate) ?? vanillaTemplate;

                // 3. Build a descriptor from the live template. For a Crabman class tag there is
                //    no matching Phoenix SpecializationDef, so Progression stays null => exact
                //    enemy clone with no perks/levels (= MVP). Bodyparts/equipment/inventory copied.
                GeoUnitDescriptor descriptor = geo.CharacterGenerator.GenerateUnit(geo.PhoenixFaction, template);

                // 4. Spawn the GeoCharacter (registers in the level's unit list).
                GeoCharacter geoChar = descriptor.SpawnAsCharacter();
                if (geoChar == null)
                {
                    Log?.LogError("[TheTurned] SpawnAsCharacter returned null — recruit aborted.");
                    return;
                }

                // 5. Route into the Phoenix roster via the native grant path (GiveUnits -> AddRecruit).
                //    Pass a Phoenix-owned base site as the container; if full, AddRecruit opens the
                //    deploy-asset UI (acceptable for MVP).
                // PhoenixFaction.Bases is IEnumerable<GeoPhoenixBase>; its .Site is the GeoSite
                // (an IGeoCharacterContainer) used as the recruit container.
                GeoSite recruitSite = geo.PhoenixFaction.Bases?.FirstOrDefault()?.Site;

                GeoFactionReward reward = new GeoFactionReward
                {
                    Reason = "TheTurned",
                    SourceFaction = geo.AlienFaction
                };
                reward.Units.Add(geoChar);
                reward.Apply(geo.PhoenixFaction, recruitSite, null);

                int count = geo.PhoenixFaction.Characters?.Count() ?? -1;
                Log?.LogInfo($"[TheTurned] Recruited Arthron '{template.name}' into the Phoenix roster "
                    + $"(site='{(recruitSite != null ? recruitSite.name : "null")}', faction Characters count={count}).");
            }
            catch (Exception e)
            {
                Log?.LogError($"[TheTurned] Recruit failed: {e}");
            }
        }

        /// <summary>
        /// Pick a deterministic basic Arthron variant: filter all TacCharacterDefs by the
        /// Crabman class tag, prefer non-Elite / non-Ultra variants (claw+shield base loadout),
        /// and order by name so the choice is stable across runs.
        /// </summary>
        private static TacCharacterDef ResolveBasicArthron()
        {
            DefRepository repo = GameUtl.GameComponent<DefRepository>();
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

            // Prefer a basic (non-elite/non-ultra) variant; fall back to any Crabman if none qualify.
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
        /// Returns a cloned Arthron <see cref="TacCharacterDef"/> with a non-null
        /// Data.LevelProgression (so TFTV's BetterClasses GenerateUnit Prefix does not NRE and
        /// spawn its popup). Idempotent: re-uses the clone if it already exists in the repo.
        /// Returns null if no valid LevelProgressionDef can be borrowed (caller then falls back
        /// to recruiting the vanilla def, preserving today's behaviour).
        /// </summary>
        private static TacCharacterDef GetOrCreateProgressedArthron(TacCharacterDef vanilla)
        {
            DefRepository repo = GameUtl.GameComponent<DefRepository>();
            if (repo == null)
            {
                return null;
            }

            // Idempotency: reuse a previously created clone (same game session).
            if (repo.GetDef(ProgressedArthronGuid) is TacCharacterDef existing)
            {
                return existing;
            }

            // Borrow a valid LevelProgressionDef from a human soldier template. Pandorans have a
            // null Data.LevelProgression; human Phoenix soldiers carry a real one with a non-null
            // Def. Pick deterministically (by name) and null-check — no hardcoded GUID.
            LevelProgressionDef borrowed = repo.GetAllDefs<TacCharacterDef>()
                .Where(d => d != null && d.Data?.LevelProgression?.Def != null)
                .OrderBy(d => d.name, StringComparer.Ordinal)
                .Select(d => d.Data.LevelProgression.Def)
                .FirstOrDefault();

            if (borrowed == null)
            {
                Log?.LogWarning("[TheTurned] No human LevelProgressionDef found to borrow — "
                    + "recruiting vanilla Arthron def (BetterClasses popup may appear).");
                return null;
            }

            TacCharacterDef clone = repo.CreateDef<TacCharacterDef>(ProgressedArthronGuid, vanilla);
            if (clone == null)
            {
                Log?.LogWarning("[TheTurned] CreateDef returned null for the Arthron clone — "
                    + "recruiting vanilla Arthron def.");
                return null;
            }

            // CreateDef deep-copies the ScriptableObject (incl. Data); set the clone's progression
            // only. The borrowed Def gives a non-null .Def, satisfying the Prefix's
            // ShouldGeneratePersonalAbilities (=> Def.GeneratePersonalAbilities) read.
            clone.Data.LevelProgression = new LevelProgression(borrowed);

            // Append our dedicated ClassTag (so GenerateUnit attaches our SpecializationDef =>
            // non-null runtime Progression => GeoCharacter.LevelProgression non-null => edit screen
            // no longer NREs) and the unique marker tag (so the Harmony CheckIsHuman Postfix
            // classifies ONLY this def as a soldier). Both go on the per-def Data.GameTags array
            // (GetGameTags concats TacticalActorBaseDef.GameTags + Data.GameTags,
            // TacCharacterDef.cs:215); the shared HumanTag/AlienTag are NOT mutated, so IsAlien
            // stays true and TFTV's PersonalSpecModification keeps skipping. AlienTag is retained.
            AppendDataGameTag(clone, ArthronClass.ArthronClassTag);
            AppendDataGameTag(clone, ArthronClass.RecruitMarkerTag);

            // _classTags is a cached List<ClassTagDef> populated on first ClassTags access
            // (TacCharacterDef.cs:142). Reset it so our newly-appended ClassTag is picked up.
            ResetClassTagsCache(clone);

            Log?.LogInfo($"[TheTurned] Cloned Arthron '{vanilla.name}' -> '{clone.name}' "
                + $"(guid='{clone.Guid}'), LevelProgression set from '{borrowed.name}', "
                + $"tags appended: classTag='{ArthronClass.ArthronClassTag?.name}', "
                + $"marker='{ArthronClass.RecruitMarkerTag?.name}'.");
            return clone;
        }

        /// <summary>
        /// Appends a GameTagDef to the def's per-instance Data.GameTags array (mirrors the game's own
        /// pattern, TacCharacterDef.cs:291: data.GameTags = data.GameTags.Append(t).Distinct().ToArray()).
        /// Idempotent (Distinct). Does NOT touch the shared TacticalActorBaseDef.GameTags component.
        /// </summary>
        private static void AppendDataGameTag(TacCharacterDef def, GameTagDef tag)
        {
            if (def?.Data == null || tag == null)
            {
                return;
            }
            GameTagDef[] current = def.Data.GameTags ?? new GameTagDef[0];
            if (current.Contains(tag))
            {
                return;
            }
            def.Data.GameTags = current.Append(tag).Distinct().ToArray();
        }

        /// <summary>
        /// Clears TacCharacterDef's cached _classTags list (NonSerialized, private; populated on the
        /// first ClassTags access, TacCharacterDef.cs:142) so a freshly-appended ClassTag is seen.
        /// </summary>
        private static void ResetClassTagsCache(TacCharacterDef def)
        {
            try
            {
                FieldInfo f = typeof(TacCharacterDef).GetField("_classTags",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                f?.SetValue(def, null);
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] Could not reset _classTags cache: {e.Message}");
            }
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
