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
    /// mutoid choose-on-levelup UI. Fallback if the transpiler breaks on a game patch (documented,
    /// not shipped): Postfix on SetCharacterProgression: set _hasPandoranProgression via
    /// reflection, then reflection-invoke private SetAbilityTracks().</summary>
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
            if (target == null || CharacterField == null || GateField == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] Pandoran progression gate: target method or fields not resolved — gate disabled.");
                return;
            }
            try
            {
                harmony.Patch(target, transpiler: new HarmonyMethod(typeof(PandoranProgressionGate), nameof(Transpiler)));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] Pandoran progression gate transpiler applied.");
            }
            catch (System.Exception e)
            {
                TheTurnedMain.Main?.Logger?.LogError($"[TheTurned] Pandoran progression gate patch failed: {e}");
            }
        }

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int injected = 0;
            foreach (var ci in instructions)
            {
                // Stack discipline: before `stfld` the stack is [this, value]; pushing `ldarg.0`
                // gives [this, value, this]; `call OrGate(bool, UIModuleCharacterProgression)` pops
                // (value, this) in declaration order (this = top of stack), pushes the OR'd bool
                // -> [this, bool'] -> `stfld` consumes. OrGate must be public static (call target
                // from the patched method).
                if (ci.opcode == OpCodes.Stfld && Equals(ci.operand, GateField))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(PandoranProgressionGate), nameof(OrGate)));
                    injected++;
                }
                yield return ci;
            }
            if (injected == 0)
                TheTurnedMain.LogWarn("[TheTurned] Pandoran progression gate: target stfld not found — gate disabled (see PandoranProgressionGate class doc for the Postfix fallback)");
            else if (injected > 1)
                TheTurnedMain.LogWarn($"[TheTurned] Pandoran progression gate: {injected} assignments of _hasPandoranProgression patched — game code drifted from single-assignment shape.");
        }

        public static bool OrGate(bool original, UIModuleCharacterProgression module)
        {
            if (original) return true;
            // REV-2 (M-PROBE/M-LAYOUT): on the 2-row layout, DON'T OR our recruit -> _hasPandoranProgression
            // stays false -> the HUMAN ability-track container draws (2 rows), not the mutoid popup container.
            // (TwoRowCellLayout is a compile-time const, so the mutoid-path code below is intentionally
            //  unreachable while it is true; it reactivates verbatim when the const is flipped to false.)
#pragma warning disable CS0162 // Unreachable code (const-folded TwoRowCellLayout switch — intentional, revertible)
            if (Phase4.TwoRowCellLayout) return original;
            var character = (GeoCharacter)CharacterField.GetValue(module);   // assigned just before the gate line
            return Phase4.IsPhase4Recruit(character);
#pragma warning restore CS0162
        }
    }
}
