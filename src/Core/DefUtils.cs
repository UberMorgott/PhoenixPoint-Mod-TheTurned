using Base.Core;
using Base.Defs;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;
using System.Reflection;

namespace TheTurned.Core
{
    /// <summary>
    /// Low-level, monster-agnostic helpers over <see cref="DefRepository"/>: get-or-create / clone,
    /// per-instance GameTag appending, the class-tag cache reset, borrowing a human LevelProgression,
    /// and registering a spec in SharedData. Generalized from the original Arthron-specific code.
    /// </summary>
    internal static class DefUtils
    {
        /// <summary>The live def repository handle (mirrors Officer: GameUtl.GameComponent).</summary>
        internal static DefRepository Repo => GameUtl.GameComponent<DefRepository>();

        /// <summary>
        /// Idempotent get-or-create. If a def with <paramref name="guid"/> already exists it is returned;
        /// otherwise it is created — cloned from <paramref name="templateGuid"/> when supplied, else blank.
        /// </summary>
        internal static T GetOrCreate<T>(DefRepository repo, string guid, string templateGuid = null) where T : BaseDef
        {
            if (repo == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }
            if (repo.GetDef(guid) is T existing)
            {
                return existing;
            }
            if (!string.IsNullOrEmpty(templateGuid))
            {
                BaseDef template = repo.GetDef(templateGuid);
                if (template == null)
                {
                    return null;
                }
                return repo.CreateDef<T>(guid, template);
            }
            return repo.CreateDef<T>(guid);
        }

        /// <summary>
        /// Robustly resolve a def by exact <c>name</c> at runtime (the def-name string, not the GUID).
        /// Used for vanilla/TFTV defs whose GUID we don't hardcode. Optionally falls back to a known GUID.
        /// Returns null if neither resolves (callers must null-guard).
        /// </summary>
        internal static T ResolveByName<T>(DefRepository repo, string defName, string fallbackGuid = null) where T : BaseDef
        {
            if (repo == null)
            {
                return null;
            }
            if (!string.IsNullOrEmpty(defName))
            {
                T byName = repo.GetAllDefs<T>().FirstOrDefault(d => d != null && d.name == defName);
                if (byName != null)
                {
                    return byName;
                }
            }
            if (!string.IsNullOrEmpty(fallbackGuid) && repo.GetDef(fallbackGuid) is T byGuid)
            {
                return byGuid;
            }
            return null;
        }

        /// <summary>
        /// Find any def of type T to use as a clone template (last-resort robustness when a specific
        /// template can't be resolved). Returns null only if the repo has none.
        /// </summary>
        internal static T AnyTemplate<T>(DefRepository repo) where T : BaseDef
        {
            return repo?.GetAllDefs<T>().FirstOrDefault(d => d != null);
        }

        /// <summary>Clone an existing template def into a new GUID.</summary>
        internal static T CloneDef<T>(DefRepository repo, string guid, BaseDef template) where T : BaseDef
        {
            if (repo == null || template == null || string.IsNullOrEmpty(guid))
            {
                return null;
            }
            if (repo.GetDef(guid) is T existing)
            {
                return existing;
            }
            return repo.CreateDef<T>(guid, template);
        }

        /// <summary>
        /// Appends a <see cref="GameTagDef"/> to the def's per-instance <c>Data.GameTags</c> array
        /// (mirrors the game's own pattern: data.GameTags = data.GameTags.Append(t).Distinct().ToArray()).
        /// Idempotent. Does NOT touch the shared TacticalActorBaseDef.GameTags component.
        /// </summary>
        internal static void AppendDataGameTag(TacCharacterDef def, GameTagDef tag)
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
        /// Clears TacCharacterDef's cached <c>_classTags</c> list (NonSerialized, private; populated on
        /// first ClassTags access) so a freshly-appended ClassTag is seen.
        /// </summary>
        internal static void ResetClassTagsCache(TacCharacterDef def)
        {
            if (def == null)
            {
                return;
            }
            try
            {
                FieldInfo f = typeof(TacCharacterDef).GetField("_classTags",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                f?.SetValue(def, null);
            }
            catch (Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogWarning($"[TheTurned] Could not reset _classTags cache: {e.Message}");
            }
        }

        /// <summary>
        /// Borrow a valid <see cref="LevelProgressionDef"/> from a human soldier template (Pandorans
        /// have a null Data.LevelProgression). Deterministic (ordered by name), null-checked, no hardcoded
        /// GUID. Returns null if none found.
        /// </summary>
        internal static LevelProgressionDef BorrowHumanLevelProgression(DefRepository repo)
        {
            if (repo == null)
            {
                return null;
            }
            return repo.GetAllDefs<TacCharacterDef>()
                .Where(d => d != null && d.Data?.LevelProgression?.Def != null)
                .OrderBy(d => d.name, StringComparer.Ordinal)
                .Select(d => d.Data.LevelProgression.Def)
                .FirstOrDefault();
        }

        /// <summary>
        /// Registers a spec into SharedData so the generator's cached SpecializationsDefs list discovers
        /// it. Idempotent (Contains-guarded).
        /// </summary>
        internal static void RegisterSpecInSharedData(SpecializationDef spec)
        {
            if (spec == null)
            {
                return;
            }
            SharedData shared = GameUtl.GameComponent<SharedData>();
            if (shared?.SharedGameTags == null)
            {
                TheTurnedMain.Main?.Logger?.LogWarning(
                    "[TheTurned] SharedGameTags unavailable — spec not registered (generator may still discover it via repo).");
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
