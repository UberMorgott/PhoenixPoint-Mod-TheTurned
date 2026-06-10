using Base.Defs;
using Base.Entities.Abilities;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Addons;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace TheTurned.Core
{
    /// <summary>
    /// ONE-SHOT runtime diagnostics for the Arthron augment screen (V1 Phase 1). Fires the first time a
    /// UIModuleBionics screen Awakes while Phase 4 is on. Dumps the prefab/asset-only unknowns that Phase 2
    /// (custom sections) depends on — real Crabman ItemSlotDef names, the bionics section-container shape,
    /// and one arm bodypart's SubAddons wiring. REMOVE this file once Phase 2 has consumed the dump.
    /// </summary>
    internal static class AugmentDiagnosticsDump
    {
        internal const string PatchId = "Morgott.TheTurned.AugmentDiagnosticsDump";
        private static bool _applied;
        private static bool _dumped;

        internal static void Apply(Harmony harmony)
        {
            if (_applied || harmony == null)
            {
                return;
            }
            try
            {
                var target = AccessTools.Method(typeof(UIModuleBionics), "Awake");
                var postfix = AccessTools.Method(typeof(AugmentDiagnosticsDump), nameof(Bionics_Awake_Postfix));
                harmony.Patch(target, postfix: new HarmonyMethod(postfix));
                _applied = true;
                TheTurnedMain.LogInfo("[TheTurned] AugmentDiagnosticsDump: UIModuleBionics.Awake Postfix applied.");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentDiagnosticsDump apply failed: " + e);
            }
        }

        private static void Bionics_Awake_Postfix(UIModuleBionics __instance)
        {
            if (_dumped || !Phase4.Enabled)
            {
                return;
            }
            _dumped = true;
            try
            {
                DumpDefs();
                DumpSectionContainer(__instance);
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] AugmentDiagnosticsDump threw: " + e);
            }
        }

        private static void DumpDefs()
        {
            DefRepository repo = DefUtils.Repo;
            if (repo == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] DUMP: DefRepository null — skipping def dump.");
                return;
            }

            // (a) Legs + Torso defs (names + type + bound ItemSlotDefs) — Phase 1.1 / Torso roadmap.
            var legsTorso = repo.GetAllDefs<ItemDef>()
                .Where(d => d?.name != null
                    && (d.name.IndexOf("Crabman_Legs", StringComparison.OrdinalIgnoreCase) >= 0
                     || d.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            TheTurnedMain.LogInfo($"[TheTurned] DUMP legs/torso ItemDefs ({legsTorso.Count}):");
            foreach (var d in legsTorso)
            {
                TheTurnedMain.LogInfo($"  '{d.name}' type={d.GetType().Name} slots=[{SlotBindsToString(d)}]");
            }

            // (b) Head / left-arm / right-arm BODYPART (non-weapon) defs + the ItemSlotDef each binds to.
            var bodyparts = repo.GetAllDefs<TacticalItemDef>()
                .Where(d => d?.name != null && !(d is WeaponDef))
                .Where(d => d.name.IndexOf("Crabman_Head", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_LeftArm", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            TheTurnedMain.LogInfo($"[TheTurned] DUMP head/arm bodyparts ({bodyparts.Count}):");
            foreach (var d in bodyparts)
            {
                TheTurnedMain.LogInfo($"  '{d.name}' ved={(d.ViewElementDef != null ? "yes" : "NULL")} slots=[{SlotBindsToString(d)}]");
            }

            // (d) One sample arm bodypart's SubAddons wiring (decides Phase-2 hand-pairing route 3.4 a vs b).
            var sample = bodyparts.FirstOrDefault(d => d.name.IndexOf("Crabman_RightArm_Gun", StringComparison.OrdinalIgnoreCase) >= 0)
                      ?? bodyparts.FirstOrDefault(d => d.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0);
            if (sample != null)
            {
                AddonDef.SubaddonBind[] subs = sample.SubAddons;
                TheTurnedMain.LogInfo($"[TheTurned] DUMP SubAddons of '{sample.name}' ({(subs == null ? "null" : subs.Length.ToString())}):");
                if (subs != null)
                {
                    foreach (var sb in subs)
                    {
                        TheTurnedMain.LogInfo($"    subaddon='{sb.SubAddon?.name ?? "<null>"}'");
                    }
                }
            }

            // (e) Phase-B card-text harvest: for every BASE-TIER augment card (bodypart + matched hand),
            //     dump the vanilla ViewElementDef loc keys + every granted AbilityDef name + ActionPointCost.
            //     The user runs one in-game pass (open recruit -> DNA -> Bionics) and pastes this back so we
            //     can surface the real vanilla ability descriptions (Return Fire, Deploy Shield, Spit, ...).
            DumpCardAbilities();
        }

        /// <summary>Phase-B: dump loc keys + granted abilities (name + AP cost) per base-tier card.</summary>
        private static void DumpCardAbilities()
        {
            TheTurnedMain.LogInfo("[TheTurned] DUMP PHASE-B card ability/loc harvest (base-tier cards):");
            foreach (MatchedSet set in CrabmanParts.BaseTier(CrabmanParts.HeadSets)
                .Concat(CrabmanParts.BaseTier(CrabmanParts.LeftArmSets))
                .Concat(CrabmanParts.BaseTier(CrabmanParts.RightArmSets)))
            {
                TacticalItemDef bp = set?.BodyPart;
                if (bp == null)
                {
                    continue;
                }
                TheTurnedMain.LogInfo($"  CARD token='{set.Token}' bodypart='{bp.name}' hand='{set.Hand?.name ?? "<none>"}' "
                    + $"armor={bp.Armor} hp={bp.HitPoints}");
                DumpItemTextAndAbilities("    bodypart", bp);
                if (set.Hand != null)
                {
                    DumpItemTextAndAbilities("    hand", set.Hand);
                }
            }
        }

        private static void DumpItemTextAndAbilities(string label, ItemDef item)
        {
            var ved = item.ViewElementDef;
            string nameKey = ved?.DisplayName1?.LocalizationKey ?? "<null>";
            string descKey = ved?.Description?.LocalizationKey ?? "<null>";
            TheTurnedMain.LogInfo($"{label} '{item.name}' nameKey='{nameKey}' descKey='{descKey}'");
            AbilityDef[] abilities = item.Abilities;
            if (abilities == null || abilities.Length == 0)
            {
                TheTurnedMain.LogInfo($"{label}   abilities: <none>");
                return;
            }
            foreach (AbilityDef a in abilities)
            {
                if (a == null)
                {
                    continue;
                }
                TacticalAbilityDef tad = a as TacticalAbilityDef;
                string apCost = tad != null ? tad.ActionPointCost.ToString("0.###") : "n/a";
                ViewElementDef aved = tad?.ViewElementDef;
                string aNameKey = aved?.DisplayName1?.LocalizationKey ?? "<null>";
                string aDescKey = aved?.Description?.LocalizationKey ?? "<null>";
                TheTurnedMain.LogInfo($"{label}   ability '{a.name}' type={a.GetType().Name} ap={apCost} "
                    + $"nameKey='{aNameKey}' descKey='{aDescKey}'");
            }
        }

        private static string SlotBindsToString(ItemDef d)
        {
            AddonDef.RequiredSlotBind[] binds = d.RequiredSlotBinds;
            if (binds == null || binds.Length == 0)
            {
                return "<none>";
            }
            return string.Join(", ", binds.Select(b =>
            {
                var slot = b.RequiredSlot;
                string slotName = (slot as ItemSlotDef)?.SlotName ?? "<not-ItemSlotDef>";
                return $"{slot?.name ?? "<null>"}({slotName})";
            }));
        }

        private static void DumpSectionContainer(UIModuleBionics module)
        {
            if (module == null)
            {
                return;
            }
            var sections = module.GetComponentsInChildren<UIModuleMutationSection>(true);
            TheTurnedMain.LogInfo($"[TheTurned] DUMP bionics UIModuleMutationSection count={sections.Length}:");
            foreach (var s in sections)
            {
                var slot = s.SlotForMutation;
                string slotName = (slot as ItemSlotDef)?.SlotName ?? "<null/non-ItemSlot>";
                TheTurnedMain.LogInfo($"    section '{s.name}' slotForMutation='{slot?.name ?? "<null>"}'({slotName})");
            }

            Transform parent = sections.Length > 0 ? sections[0].transform.parent : module.transform;
            bool hasScroll = parent != null && parent.GetComponentInParent<ScrollRect>() != null;
            var vlg = parent != null ? parent.GetComponent<VerticalLayoutGroup>() : null;
            TheTurnedMain.LogInfo($"[TheTurned] DUMP section container '{(parent != null ? parent.name : "<null>")}' "
                + $"childCount={(parent != null ? parent.childCount : -1)} "
                + $"hasScrollRectAncestor={hasScroll} hasVerticalLayoutGroup={(vlg != null)}");
        }
    }
}
