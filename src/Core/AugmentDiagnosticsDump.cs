using Base.Defs;
using Base.Entities.Abilities;
using HarmonyLib;
using PhoenixPoint.Common.Entities.Addons;
using PhoenixPoint.Common.Entities.Characters;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
using PhoenixPoint.Geoscape.View.ViewModules;
using PhoenixPoint.Tactical.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Linq;
using System.Reflection;
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

            // (f) APPEARANCE harvest: for every Crabman head/arm/leg/torso part the screen can touch (incl.
            //     our authored head clones), dump the visual-identity fields so a single in-game pass reveals
            //     which parts SHARE a model (look identical) vs are visually distinct. Feeds the body-part
            //     catalog (docs/research/crabman-bodypart-catalog.md).
            DumpAppearance(repo);

            // (g) BACK-PLATE hunt #1: SubAddons riding on the TORSO + the 4 LEG defs (the mesh that holds
            //     the visible back plate is most likely a sub-addon of one of these). Read-only.
            DumpTorsoLegSubAddons(repo);

            // (h) BACK-PLATE hunt #2: per-strain loadouts — which torso/leg defs each Crabman strain tier
            //     equips (Data.BodypartItems + ComponentSetDef CharacterBodyStateDef.BodyPartsDefs). Read-only.
            DumpPerStrainLoadouts(repo);

            // (i) BACK-PLATE hunt #3: EVERY Crabman-prefixed TacticalItemDef (no narrow name filter) + slot
            //     bind — catches a back/carapace/plate def hiding under a non-obvious name. Read-only.
            DumpAllCrabmanItemDefs(repo);
        }

        /// <summary>(g) Torso + 4 leg defs: recursively dump each one's SubAddons wiring — name, SkinData type +
        /// NormalPrefab/runtimeKey (via the shared <see cref="DumpAssetReferenceKeys"/> reflector), WeakAddon flag,
        /// RequiredSlotBinds, ProvidedSlots. Reveals any back-plate addon attached to the torso/legs.</summary>
        private static void DumpTorsoLegSubAddons(DefRepository repo)
        {
            string[] names =
            {
                "Crabman_Torso_BodyPartDef",
                "Crabman_Legs_Agile_ItemDef",
                "Crabman_Legs_Armoured_ItemDef",
                "Crabman_Legs_EliteAgile_ItemDef",
                "Crabman_Legs_EliteArmoured_ItemDef",
            };
            TheTurnedMain.LogInfo($"[TheTurned] DUMP torso/leg SubAddons ({names.Length} target defs):");
            foreach (string n in names)
            {
                AddonDef root = DefUtils.ResolveByName<TacticalItemDef>(repo, n);
                if (root == null)
                {
                    TheTurnedMain.LogInfo($"  TARGET '{n}' = <not resolved>");
                    continue;
                }
                TheTurnedMain.LogInfo($"  TARGET '{root.name}' type={root.GetType().Name}");
                DumpAddonRecursive("    ", root, 0);
            }
        }

        /// <summary>Recursively log an addon + its SubAddons (the back-plate is most likely a leaf sub-addon).</summary>
        private static void DumpAddonRecursive(string indent, AddonDef addon, int depth)
        {
            if (addon == null || depth > 6)
            {
                return;
            }
            string skinType = addon.SkinData != null ? addon.SkinData.GetType().Name : "<null>";
            string skinName = addon.SkinData != null ? addon.SkinData.name : "<null>";
            TheTurnedMain.LogInfo($"{indent}addon='{addon.name}' weak={addon.WeakAddon} skinType={skinType} skin='{skinName}' "
                + $"required=[{RequiredBindsToString(addon)}] provided=[{ProvidedSlotsToString(addon)}]");
            // SkinData asset-reference RuntimeKeys (NormalPrefab/mesh identity) — same accessor as the arm dump.
            DumpAssetReferenceKeys($"{indent}  skin", addon.SkinData);

            AddonDef.SubaddonBind[] subs = addon.SubAddons;
            if (subs == null || subs.Length == 0)
            {
                return;
            }
            TheTurnedMain.LogInfo($"{indent}  subAddons ({subs.Length}):");
            foreach (AddonDef.SubaddonBind sb in subs)
            {
                AddonDef child = sb.SubAddon;
                TheTurnedMain.LogInfo($"{indent}    sub='{child?.name ?? "<null>"}' attachPoint='{sb.AttachmentPointName ?? "<null>"}'");
                DumpAddonRecursive(indent + "      ", child, depth + 1);
            }
        }

        /// <summary>(h) For every Crabman-named TacCharacterDef (strain tiers): dump its Data.BodypartItems names
        /// AND its ComponentSetDef CharacterBodyStateDef.BodyPartsDefs names — shows which torso/leg each strain
        /// equips and whether evolved strains inject a back-plate the base lacks.</summary>
        private static void DumpPerStrainLoadouts(DefRepository repo)
        {
            var strains = repo.GetAllDefs<TacCharacterDef>()
                .Where(d => d?.name != null && d.name.IndexOf("Crabman", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            TheTurnedMain.LogInfo($"[TheTurned] DUMP per-strain loadouts: Crabman TacCharacterDefs ({strains.Count}):");
            foreach (TacCharacterDef cd in strains)
            {
                ItemDef[] bodyparts = cd.Data?.BodypartItems;
                string bpItems = bodyparts == null || bodyparts.Length == 0
                    ? "<none>"
                    : string.Join(", ", bodyparts.Select(i => "'" + (i?.name ?? "<null>") + "'"));
                CharacterBodyStateDef bodyState = cd.ComponentSetDef?.GetComponentDef<CharacterBodyStateDef>();
                ItemDef[] stateParts = bodyState?.BodyPartsDefs;
                string statePartsStr = stateParts == null || stateParts.Length == 0
                    ? "<none>"
                    : string.Join(", ", stateParts.Select(i => "'" + (i?.name ?? "<null>") + "'"));
                TheTurnedMain.LogInfo($"  STRAIN '{cd.name}'");
                TheTurnedMain.LogInfo($"    Data.BodypartItems=[{bpItems}]");
                TheTurnedMain.LogInfo($"    BodyState.BodyPartsDefs=[{statePartsStr}]");
            }
        }

        /// <summary>(i) Every TacticalItemDef whose name starts with "Crabman" (drop the narrow head/arm/leg/torso
        /// filter) + its slot bind — catches any back/carapace/plate def under a non-obvious name.</summary>
        private static void DumpAllCrabmanItemDefs(DefRepository repo)
        {
            var all = repo.GetAllDefs<TacticalItemDef>()
                .Where(d => d?.name != null && d.name.StartsWith("Crabman", StringComparison.Ordinal))
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            TheTurnedMain.LogInfo($"[TheTurned] DUMP all Crabman* TacticalItemDefs ({all.Count}):");
            foreach (TacticalItemDef d in all)
            {
                TheTurnedMain.LogInfo($"  '{d.name}' type={d.GetType().Name} slots=[{SlotBindsToString(d)}]");
            }
        }

        private static string RequiredBindsToString(AddonDef d)
        {
            AddonDef.RequiredSlotBind[] binds = d.RequiredSlotBinds;
            if (binds == null || binds.Length == 0)
            {
                return "<none>";
            }
            return string.Join(", ", binds.Select(b => b.RequiredSlot?.name ?? "<null>"));
        }

        private static string ProvidedSlotsToString(AddonDef d)
        {
            AddonDef.ProvidedSlotBind[] binds = d.ProvidedSlots;
            if (binds == null || binds.Length == 0)
            {
                return "<none>";
            }
            return string.Join(", ", binds.Select(b => (b.ProvidedSlot?.name ?? "<null>") + "@" + (b.AttachmentPointName ?? "<null>")));
        }

        /// <summary>Per Crabman part: VED display/icons, BodyPartAspectDef, SkinData type, and the RuntimeKey
        /// of every AssetReference (prefab/mesh) field — the cross-part model fingerprint.</summary>
        private static void DumpAppearance(DefRepository repo)
        {
            var parts = repo.GetAllDefs<TacticalItemDef>()
                .Where(d => d?.name != null
                    && (d.name.IndexOf("Crabman_Head", StringComparison.OrdinalIgnoreCase) >= 0
                     || d.name.IndexOf("Crabman_LeftArm", StringComparison.OrdinalIgnoreCase) >= 0
                     || d.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0
                     || d.name.IndexOf("Crabman_Legs", StringComparison.OrdinalIgnoreCase) >= 0
                     || d.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            TheTurnedMain.LogInfo($"[TheTurned] DUMP APPEARANCE Crabman parts ({parts.Count}):");
            foreach (TacticalItemDef d in parts)
            {
                string tier = d.name.IndexOf("Ultra", StringComparison.OrdinalIgnoreCase) >= 0 ? "Ultra"
                    : d.name.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0 ? "Elite" : "Base";
                ViewElementDef ved = d.ViewElementDef;
                string disp = ved?.DisplayName1?.LocalizationKey ?? "<null>";
                string small = ved?.SmallIcon != null ? ved.SmallIcon.name : "<null>";
                string large = ved?.LargeIcon != null ? ved.LargeIcon.name : "<null>";
                string aspect = d.BodyPartAspectDef != null ? d.BodyPartAspectDef.name : "<null>";
                string skinType = d.SkinData != null ? d.SkinData.GetType().Name : "<null>";
                string skinName = d.SkinData != null ? d.SkinData.name : "<null>";
                TheTurnedMain.LogInfo($"  PART '{d.name}' tier={tier} vedName='{(ved != null ? ved.name : "<null>")}' "
                    + $"disp='{disp}' small='{small}' large='{large}' aspect='{aspect}' skinType={skinType} skin='{skinName}'");
                // Model fingerprint: RuntimeKey of every AssetReference field on the SkinData def
                // (two parts with the same RuntimeKey render the SAME mesh/prefab).
                DumpAssetReferenceKeys("    skin", d.SkinData);
            }
        }

        /// <summary>Reflect over an object's fields; for every AssetReference-like field (UnityEngine
        /// Addressables, detected by type name to avoid a compile-time assembly reference), log its
        /// RuntimeKey (the addressable address — the model/prefab identity, comparable across parts).</summary>
        private static void DumpAssetReferenceKeys(string label, object def)
        {
            if (def == null)
            {
                return;
            }
            foreach (FieldInfo f in def.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                object val = null;
                try { val = f.GetValue(def); } catch { continue; }
                if (val == null)
                {
                    continue;
                }
                Type vt = val.GetType();
                // AssetReferenceGameObject / AssetReference etc. — match by type name (no assembly ref).
                if (vt.Name.IndexOf("AssetReference", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }
                object key = null;
                try
                {
                    PropertyInfo rk = vt.GetProperty("RuntimeKey", BindingFlags.Instance | BindingFlags.Public);
                    key = rk != null ? rk.GetValue(val) : null;
                }
                catch { }
                TheTurnedMain.LogInfo($"{label} assetRef field='{f.Name}' type={vt.Name} runtimeKey='{key ?? "<null>"}'");
            }
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

            // Shield deploy ability is NOT on Crabman_LeftArm_Shield_BodyPartDef.Abilities[] (verified <none>)
            // — it is granted at runtime / lives as a separate def. Enumerate every DeployShieldAbilityDef in
            // the repo so we can identify the one tied to the Crabman shield (name + AP + loc keys). OPEN item.
            DefRepository repo = DefUtils.Repo;
            if (repo != null)
            {
                var deploys = repo.GetAllDefs<DeployShieldAbilityDef>().Where(d => d != null).ToList();
                TheTurnedMain.LogInfo($"[TheTurned] DUMP PHASE-B DeployShieldAbilityDef instances ({deploys.Count}):");
                foreach (DeployShieldAbilityDef d in deploys)
                {
                    var ved = d.ViewElementDef;
                    TheTurnedMain.LogInfo($"    '{d.name}' ap={d.ActionPointCost:0.###} "
                        + $"nameKey='{ved?.DisplayName1?.LocalizationKey ?? "<null>"}' "
                        + $"descKey='{ved?.Description?.LocalizationKey ?? "<null>"}'");
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
