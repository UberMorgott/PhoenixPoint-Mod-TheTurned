using HarmonyLib;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewControllers;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities;
using System;
using System.Reflection;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// TEMPORARY M-PROBE diagnostic (REMOVE in M-FINAL). One-shot Postfix on
    /// UIModuleCharacterProgression.SetCharacterProgression [G UIModuleCharacterProgression.cs:480], gated on
    /// Phase4.IsPhase4Recruit. De-risks the REV-2 2-row layout BEFORE mass authoring by dumping every value
    /// the rewrite assumes (read-only):
    ///   - TemplateDef.IsHuman + runtime CheckIsHuman() (HumanClassificationPatch flips these; :485 routes to
    ///     ShowHumanProgression when IsHuman);
    ///   - reflected _hasPandoranProgression (must be FALSE for the human container path);
    ///   - ActiveAbilityTrackContainer type name (expect human AbilityTrackContainer, not MutoidSkillContainer);
    ///   - that container's PrimaryClassContainer / SecondaryClassContainer / PersonalTrackContainer non-null +
    ///     transform.childCount (entry-element pools SetAbilityTrack indexes — must each be >=5);
    ///   - LevelController.CharacterLevelRoot.transform.childCount (numbered-tab pool — must be >=5);
    ///   - LevelProgression.Def.MaxLevel (expect 5) + each runtime AbilityTrack Source + AbilitiesByLevel.Length.
    /// All private fields read via AccessTools.Field (same pattern as PandoranProgressionGate / DevUnlockPatch);
    /// container/LevelController/row-containers are PUBLIC fields on the respective classes.
    /// </summary>
    internal static class CellProbe
    {
        internal const string PatchId = "Morgott.TheTurned.CellProbe";
        private static bool _applied;
        private static bool _dumped;

        private static readonly FieldInfo HasPandoranField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_hasPandoranProgression");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(UIModuleCharacterProgression),
                    nameof(UIModuleCharacterProgression.SetCharacterProgression));
                if (target == null)
                {
                    TheTurnedMain.LogWarn("[TheTurned] CellProbe: SetCharacterProgression not found — probe skipped.");
                    return;
                }
                var postfix = AccessTools.Method(typeof(CellProbe), nameof(SetCharacterProgression_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] CellProbe: SetCharacterProgression Postfix applied (one-shot 2-row de-risk dump).");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellProbe apply failed: " + e);
            }
        }

        // Runs AFTER SetCharacterProgression has set _hasPandoranProgression, taken the IsHuman branch, and (on
        // the human path) run SetAbilityTracks -> SetActiveAbilityTrackContainer, so ActiveAbilityTrackContainer
        // is populated. `character` is the method's 2nd arg (Harmony binds by name).
        private static void SetCharacterProgression_Postfix(UIModuleCharacterProgression __instance, GeoCharacter character)
        {
            if (_dumped || __instance == null || !Phase4.IsPhase4Recruit(character))
            {
                return;
            }
            _dumped = true;
            try
            {
                TacCharacterDef tmpl = character.TemplateDef;
                bool isHuman = tmpl != null && tmpl.IsHuman;            // :485 routes on this (IsHuman => CheckIsHuman())
                bool checkIsHuman = tmpl != null && tmpl.CheckIsHuman();
                string pandoran = HasPandoranField != null
                    ? HasPandoranField.GetValue(__instance)?.ToString() ?? "<null>"
                    : "<field-unresolved>";
                TheTurnedMain.LogInfo($"[TheTurned] PROBE char='{character.GetName()}' tmpl='{tmpl?.name ?? "<null>"}' "
                    + $"IsHuman={isHuman} CheckIsHuman()={checkIsHuman} _hasPandoranProgression={pandoran}");

                AbilityTrackContainerElement active = __instance.ActiveAbilityTrackContainer;
                string mutoidName = __instance.MutoidSkillContainer != null ? __instance.MutoidSkillContainer.GetType().Name : "<null>";
                string humanName = __instance.AbilityTrackContainer != null ? __instance.AbilityTrackContainer.GetType().Name : "<null>";
                TheTurnedMain.LogInfo($"[TheTurned] PROBE ActiveAbilityTrackContainer='{(active != null ? active.GetType().Name : "<null>")}' "
                    + $"(human AbilityTrackContainer='{humanName}', MutoidSkillContainer='{mutoidName}')");

                if (active != null)
                {
                    TheTurnedMain.LogInfo($"[TheTurned] PROBE rows: Primary={DescribeChild(active.PrimaryClassContainer)} "
                        + $"Secondary={DescribeChild(active.SecondaryClassContainer)} "
                        + $"Personal={DescribeChild(active.PersonalTrackContainer)}");

                    GameObject levelRoot = active.LevelController != null ? active.LevelController.CharacterLevelRoot : null;
                    TheTurnedMain.LogInfo($"[TheTurned] PROBE LevelController={(active.LevelController != null ? "yes" : "NULL")} "
                        + $"CharacterLevelRoot={DescribeChild(levelRoot)} (numbered-tab pool, must be >=5)");
                }
                else
                {
                    TheTurnedMain.LogWarn("[TheTurned] PROBE ActiveAbilityTrackContainer is NULL — human path may not have initialized the container.");
                }

                CharacterProgression prog = character.Progression;
                LevelProgressionDef lpDef = prog?.LevelProgression?.Def;
                TheTurnedMain.LogInfo($"[TheTurned] PROBE LevelProgression.Def='{lpDef?.name ?? "<null>"}' MaxLevel={(lpDef != null ? lpDef.MaxLevel.ToString() : "<null>")} "
                    + $"Level={(prog?.LevelProgression != null ? prog.LevelProgression.Level.ToString() : "<null>")}");

                if (prog?.AbilityTracks != null)
                {
                    TheTurnedMain.LogInfo($"[TheTurned] PROBE runtime AbilityTracks ({prog.AbilityTracks.Count}):");
                    foreach (AbilityTrack t in prog.AbilityTracks)
                    {
                        int len = t?.AbilitiesByLevel != null ? t.AbilitiesByLevel.Length : -1;
                        TheTurnedMain.LogInfo($"    track Source={(t != null ? t.Source.ToString() : "<null>")} AbilitiesByLevel.Length={len}");
                    }
                }
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellProbe dump threw: " + e);
            }
        }

        private static string DescribeChild(GameObject go)
        {
            if (go == null)
            {
                return "<null>";
            }
            return $"'{go.name}'(childCount={go.transform.childCount})";
        }
    }
}
