using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Geoscape.View.ViewModules;

namespace TheTurned.Core
{
    /// <summary>OR our marker into `_hasPandoranProgression` at its single assignment in
    /// UIModuleCharacterProgression.SetCharacterProgression. Routes Phase-4 recruits into the
    /// mutoid choose-on-levelup UI. Fallback if transpiler breaks on a game patch (documented,
    /// not shipped): Postfix set the field + re-invoke private SetAbilityTracks().</summary>
    internal static class PandoranProgressionGate
    {
        private static bool _applied;
        private static readonly FieldInfo CharacterField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_character");
        private static readonly FieldInfo GateField =
            AccessTools.Field(typeof(UIModuleCharacterProgression), "_hasPandoranProgression");

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null || !Phase4.Enabled) return;
            var target = AccessTools.Method(typeof(UIModuleCharacterProgression),
                nameof(UIModuleCharacterProgression.SetCharacterProgression));
            harmony.Patch(target, transpiler: new HarmonyMethod(typeof(PandoranProgressionGate), nameof(Transpiler)));
            _applied = true;
            TheTurnedMain.LogInfo("[TheTurned] Pandoran progression gate transpiler applied.");
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool injected = false;
            foreach (var ci in instructions)
            {
                // Stack discipline: before `stfld` the stack is [this, value]; pushing `ldarg.0`
                // gives [this, value, this]; `call OrGate(bool, UIModuleCharacterProgression)` pops
                // (value, this) in declaration order (this = top of stack), pushes the OR'd bool
                // -> [this, bool'] -> `stfld` consumes. OrGate must be public static (call target
                // from the patched method).
                if (!injected && ci.opcode == OpCodes.Stfld && Equals(ci.operand, GateField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(PandoranProgressionGate), nameof(OrGate)));
                    injected = true;
                }
                yield return ci;
            }
            if (!injected)
                TheTurnedMain.LogWarn("[TheTurned] gate transpiler: stfld _hasPandoranProgression NOT found — gate inactive (see fallback note).");
        }

        public static bool OrGate(bool original, UIModuleCharacterProgression module)
        {
            if (original) return true;
            var character = (GeoCharacter)CharacterField.GetValue(module);   // assigned just before the gate line
            return Phase4.IsPhase4Recruit(character);
        }
    }
}
