using Base.Core;
using Base.Entities;
using HarmonyLib;
using PhoenixPoint.Common.Core;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// CRITICAL mission-deploy hang fix (shared, marker-scoped).
    ///
    /// Root cause: when a turned recruit enters tactical play, the actor's
    /// <see cref="TacticalActorRandomTags"/> lifecycle listener
    /// (<c>OnActorEnteredPlay</c>, decompile TacticalActorRandomTags.cs:17-34) rolls one random tag from
    /// each <c>PossibleTagsList</c> group and calls <c>GameTags.Add(tag)</c> — which defaults to
    /// <c>GameTagAddMode.ErrorOnExistingExclusive</c> (GameTagsList.cs:67-128). Our recruit is generated
    /// down the HUMAN <c>CharacterGenerator.GenerateUnit</c> path (the CheckIsHuman flip), so it already
    /// carries a baked <see cref="PhoenixPoint.Common.Entities.GameTagsTypes.VoiceProfileTagDef"/> — a
    /// <c>[MutuallyExclusiveGameTag]</c>. Rolling a second VoiceProfileTag therefore throws
    /// InvalidOperationException, the <c>TacticalLevelController.OnLevelStart</c> coroutine dies, and the
    /// loading screen hangs at ~80% forever.
    ///
    /// Fix (surgical, marker-scoped, hardened): a Prefix that, ONLY for a marked recruit (the shared
    /// <see cref="Tags.RecruitMarkerTag"/> survives the geo→tactical actor via
    /// <c>TacticalActorBase.GameTags.MergeRange(TacticalActorBaseDef.GameTags)</c>, decompile
    /// TacticalActorBase.cs:586), re-implements the roll mutual-exclusion-safe: it SKIPS adding a rolled
    /// tag whose runtime <see cref="Type"/> already exists on the actor (keeping the human-baked voice/etc.,
    /// dropping only the duplicate). Non-recruits fall through to the untouched vanilla method, so no other
    /// actor — human or alien — is affected, and the shared Crabman def is never mutated.
    /// </summary>
    internal static class RandomTagsExclusiveGuard
    {
        internal const string PatchId = "Morgott.TheTurned.RandomTagsExclusiveGuard";
        private static bool _applied;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(TacticalActorRandomTags),
                    nameof(TacticalActorRandomTags.OnActorEnteredPlay));
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] RandomTagsExclusiveGuard: OnActorEnteredPlay unresolved — hang guard disabled.");
                    return;
                }
                var prefix = AccessTools.Method(typeof(RandomTagsExclusiveGuard), nameof(OnActorEnteredPlay_Prefix));
                harmony.Patch(target, prefix: new HarmonyMethod(prefix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] RandomTagsExclusiveGuard: OnActorEnteredPlay Prefix applied (recruit-scoped voice-tag hang fix).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RandomTagsExclusiveGuard apply failed: " + e);
            }
        }

        /// <summary>
        /// Returns <c>false</c> (skip vanilla) ONLY for a marked recruit, replacing the roll with a
        /// mutual-exclusion-safe version. Returns <c>true</c> (run vanilla) for everyone else.
        /// </summary>
        private static bool OnActorEnteredPlay_Prefix(TacticalActorRandomTags __instance, ActorComponent actor)
        {
            try
            {
                GameTagDef marker = Tags.RecruitMarkerTag;
                TacticalActorBase tab = actor as TacticalActorBase;
                // Not our recruit (marker not created yet, non-recruit actor) → let the vanilla method run.
                if (marker == null || tab == null || tab.GameTags == null || !tab.GameTags.Contains(marker))
                {
                    return true;
                }
                // Mirror the vanilla early-out (TacticalActorRandomTags.cs:20-23).
                if (tab.HasEnteredPlay)
                {
                    return false;
                }
                var def = __instance.TacticalActorRandomTagsDef;
                if (def == null || def.PossibleTagsList == null)
                {
                    return false;
                }
                var random = GameUtl.Game().GetComponent<SharedData>()?.Random;
                if (random == null)
                {
                    return false;
                }
                foreach (var possibleTags in def.PossibleTagsList)
                {
                    // ListWrapper is a struct; PossibleTags is its (nullable) backing list field.
                    if (possibleTags.PossibleTags == null || possibleTags.PossibleTags.Count == 0)
                    {
                        continue;
                    }
                    // Vanilla quirk preserved: Next(Count - 1) never selects the last element. With a single
                    // element Count-1==0 and System.Random.Next(0) returns 0 (no throw) — pick index 0 directly
                    // to keep that behavior explicit and RNG-implementation-independent.
                    int count = possibleTags.PossibleTags.Count;
                    int index = count <= 1 ? 0 : random.Next(count - 1);
                    GameTagDef rolled = possibleTags.PossibleTags[index];
                    if (rolled == null)
                    {
                        continue;
                    }
                    // Skip the add when a tag of the SAME runtime type is already present (e.g. the
                    // human-baked VoiceProfileTagDef). PossibleTagsList groups are pick-one-of-a-kind, so a
                    // pre-baked tag of that type already satisfies the group; keep it, drop the duplicate.
                    Type rolledType = rolled.GetType();
                    if (tab.GameTags.Any(t => t != null && t.GetType() == rolledType))
                    {
                        continue;
                    }
                    tab.GameTags.Add(rolled);
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] RandomTagsExclusiveGuard Prefix threw (recruit roll skipped safely): " + e);
            }
            return false;
        }
    }
}
