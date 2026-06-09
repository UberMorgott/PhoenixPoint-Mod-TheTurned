using Base.Defs;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Tactical.Entities.Weapons;
using System.Collections.Generic;
using TheTurned.Core;

namespace TheTurned.Monsters.Arthron
{
    /// <summary>
    /// Phase-4 popup ROW "Claw Strikes": each cell is a passive MARKER ability bound (via
    /// <see cref="Phase4Markers.RegisterClawWeapon"/>) to a CLONE of the default right-arm claw
    /// weapon carrying one extra status damage keyword (poison / shock / viral).
    /// <see cref="ArthronArms.ApplyChosenSets"/> swaps the right hand to the clone while keeping the
    /// default claw bodypart (clawOverride path). Clone names keep the side token
    /// (TheTurned_Crabman_RightHand_*) per the SwapSet removal contract.
    /// </summary>
    internal static class ArthronClawPerks
    {
        private static bool _baseKeywordsLogged;

        /// <summary>Design cells in row order. Empty when the base claw is unresolved (row deferred by caller).</summary>
        internal static AbilityTrackSlot[] BuildRowCells(DefRepository repo)
        {
            var cells = new List<AbilityTrackSlot>();
            WeaponDef baseClaw = CrabmanParts.DefaultRight?.Hand;
            if (baseClaw == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] claw row: base claw weapon unresolved — row will be fillers");
                return cells.ToArray();
            }
            LogBaseClawKeywordsOnce(baseClaw);
            AddClawCell(repo, cells, baseClaw, "POISON", "Poisonous_DamageKeywordDataDef",
                WeaponVariants.PoisonOnHitValue, 20,
                "ARTHRON_CLAW_POISON", "ArthronClaw_Poison.png");
            AddClawCell(repo, cells, baseClaw, "STUN", "Shock_DamageKeywordDataDef", 120f, 20,
                "ARTHRON_CLAW_STUN", "ArthronClaw_Stun.png");
            AddClawCell(repo, cells, baseClaw, "VIRAL", "RawViral_DamageKeywordDataDef", 4f, 25,
                "ARTHRON_CLAW_VIRAL", "ArthronClaw_Viral.png");
            return cells.ToArray();
        }

        /// <summary>Build one claw-clone marker cell; clone or marker failure → warn + skip (PadRow fills).</summary>
        private static void AddClawCell(DefRepository repo, List<AbilityTrackSlot> cells, WeaponDef baseClaw,
            string key, string keywordDefName, float keywordValue, int mutagenCost, string baseLocKey, string icon)
        {
            // CONTRACT (ArthronArms.SwapSet): clone name MUST keep the RightHand side token.
            WeaponDef clawClone = WeaponVariants.GetOrCreateWeaponVariant(repo, baseClaw,
                $"claw:{key}|weapon", $"TheTurned_Crabman_RightHand_Claw_{key}_WeaponDef",
                keywordDefName, keywordValue);
            if (clawClone == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] claw row: clone for '{key}' unavailable — cell skipped");
                return;
            }
            Phase4RowCells.AddMarkerCell(repo, cells, "claw:" + key,
                $"TheTurned_Arthron_Claw_{key}_AbilityDef", baseLocKey, icon, mutagenCost,
                extraStats: null, register: m => Phase4Markers.RegisterClawWeapon(m, clawClone));
        }

        /// <summary>One-shot dump of the base claw's existing keywords — in-game calibration aid for the
        /// added Values (40 poison / 120 shock / 4 viral).</summary>
        private static void LogBaseClawKeywordsOnce(WeaponDef baseClaw)
        {
            if (_baseKeywordsLogged)
            {
                return;
            }
            _baseKeywordsLogged = true;
            var pairs = baseClaw.DamagePayload?.DamageKeywords;
            TheTurnedMain.LogInfo($"[TheTurned] base claw '{baseClaw.name}' keywords ({pairs?.Count ?? 0}):");
            if (pairs == null)
            {
                return;
            }
            foreach (var p in pairs)
            {
                TheTurnedMain.LogInfo($"  keyword '{p?.DamageKeywordDef?.name}' value={p?.Value}");
            }
        }
    }
}
