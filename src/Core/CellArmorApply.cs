using Base.Defs;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>
    /// Re-derives the recruit's Torso / Carapace / Legs armor from its LEARNED cell-armor markers. Called
    /// alongside ArthronArms.ApplyChosenSets from ArmFollowHook (on subscribe + every OnAbilityAdded + every
    /// geoscape load) — the same additive, re-derive-from-learned-set persistence the arm swap uses.
    /// Cumulative-by-tier: cell 4 (Elite) wins over cell 2 (basic) because its loadout is applied if learned;
    /// when only cell 2 is learned, the basic loadout is applied. Both clear the same slots first.
    ///
    /// The loadout list-building (slot-clear-then-add, one SetItems) is shared with the M1 dev action
    /// (<see cref="CellDevDump"/>) via <see cref="BuildArmorList"/> — single source of the SetItems recipe
    /// proven in M1 (legs always; torso only when an elite torso is added; carapace always).
    /// </summary>
    internal static class CellArmorApply
    {
        // DIAGNOSTIC — revert to false: routes the BASIC armor cell (cell 2) to a KNOWN-RENDERING ARM SET
        // (the Gun/MG right arm, proven to render on the augment screen via SetItems(armour:)) INSTEAD of the
        // armored legs/carapace, to test whether the cell-progression refresh path itself works or whether the
        // bug is specific to leg/carapace bodypart mounts. Cell 4 (elite) is left untouched. One-line revert.
        public const bool DiagArmInsteadOfArmor = false;

        // PROVEN-rendering right-arm set for the probe. The Gun ("MG") arm bodypart carries its hand weapon as a
        // SubAddon (verified dump: Crabman_RightArm_Gun_BodyPartDef.SubAddons = [Crabman_RightHand_Gun_WeaponDef],
        // augment-mutation-screen-reuse.md §5(d)); swapping the arm bodypart via SetItems(armour:) auto-attaches
        // the gun hand and renders a clearly visible claw->machine-gun change vs the recruit's DEFAULT Pincer
        // right arm (AugmentVariants.cs: "Right cards = {MG, Viral MG}"). We add ONLY the arm bodypart — the hand
        // rides as its SubAddon exactly like the augment path; listing the hand WeaponDef explicitly too would
        // double-occupy the Crabman_RightHand slot and AddonsCharacterBuilder.SetupAddons(reportErrors:false)
        // would cull both -> blank. Keep the Crabman_RightArm token so BuildArmorList strips the default Pincer.
        private static readonly string[] DiagArmSet =
            { "Crabman_RightArm_Gun_BodyPartDef" };

        internal static void ApplyLearnedArmor(GeoCharacter geoChar)
        {
            try
            {
                if (geoChar?.Progression == null || geoChar.ArmourItems == null || !CellArmorMarkers.HasAny)
                {
                    return;
                }
                // Pick the HIGHEST learned loadout by CELL ORDER (cell index/level), NOT buy order or name —
                // so each level shows its proper legs and the ladder is monotonic (cell 4 > cell 3 > cell 2,
                // incl. cell 3's deliberately UNARMORED EliteAgile dip ranking below cell 4 purely by order).
                string[] chosen = null;
                int chosenOrder = int.MinValue;
                foreach (TacticalAbilityDef ability in ArthronArms.EnumerateLearnedAbilities(geoChar.Progression))
                {
                    if (CellArmorMarkers.TryGet(ability, out string[] names, out int order) && names != null && order > chosenOrder)
                    {
                        chosen = names; // highest learned cell order wins
                        chosenOrder = order;
                    }
                }
                if (chosen == null)
                {
                    return;
                }
                // DIAGNOSTIC PROBE B (DiagArmInsteadOfArmor): when the HIGHEST-tier learned loadout is the BASIC
                // armor cell (cell 2 — no "Elite" entry; cell 4 carries Elite legs/torso/carapace), substitute its
                // legs/carapace def-name list with the proven-rendering Gun arm set so we can tell whether the
                // cell-progression refresh path renders at all. Cell 4 falls through unchanged (it has Elite names).
                if (DiagArmInsteadOfArmor
                    && !chosen.Any(n => n != null && n.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    chosen = DiagArmSet;
                    TheTurnedMain.LogInfo($"[TheTurned] DIAG: cell2 applying ARM set [{string.Join(", ", chosen)}]");
                }
                DefRepository repo = DefUtils.Repo;
                var defs = new List<TacticalItemDef>();
                foreach (string n in chosen)
                {
                    var d = DefUtils.ResolveByName<TacticalItemDef>(repo, n);
                    if (d == null) { TheTurnedMain.LogWarn($"[TheTurned] CellArmorApply: '{n}' unresolved — skipped."); }
                    else { defs.Add(d); }
                }
                if (defs.Count == 0)
                {
                    return;
                }
                // Already-applied guard: every chosen def already present -> no SetItems (avoids churn).
                var current = new List<GeoItem>(geoChar.ArmourItems);
                bool already = defs.All(d => current.Any(i => i?.ItemDef != null && i.ItemDef.Guid == d.Guid));
                if (already)
                {
                    return;
                }
                List<GeoItem> list = BuildArmorList(geoChar, defs);
                geoChar.SetItems(armour: list);
                TheTurnedMain.LogInfo($"[TheTurned] CellArmorApply for '{geoChar.GetName()}': [{string.Join(", ", defs.Select(d => d.name))}].");
            }
            catch (Exception e)
            {
                TheTurnedMain.LogWarn("[TheTurned] CellArmorApply failed: " + e);
            }
        }

        /// <summary>
        /// SHARED loadout recipe (M1-proven): clone the recruit's current armour, drop the occupants of the
        /// slots this loadout owns (legs always; torso only when an elite torso is added; carapace always),
        /// then add the chosen bodyparts. Returns the new list to commit with ONE SetItems(armour:). Mirrors
        /// the slot-clear-then-add discipline of ArthronArms.SwapSet. Mutates nothing on the character.
        /// </summary>
        internal static List<GeoItem> BuildArmorList(GeoCharacter geoChar, IList<TacticalItemDef> defs)
        {
            var list = new List<GeoItem>(geoChar.ArmourItems);
            // TORSO TIER (reuses the existing EliteTorso-token detection; ALSO treats an Elite carapace as elite
            // tier because cell 4 ships EliteCarapace alongside its EliteTorso). The TORSO bodypart PROVIDES the
            // head + both arm slots + the Carapace slot [research/crabman-bodypart-catalog.md §5/§5b], so the final
            // list must hold EXACTLY ONE torso of the correct tier — two torsos corrupt the slot-provider mapping
            // and break the native augment swap (CommonCharacterUtils.CanSwapItem). The two torso def-name tokens
            // are the ONLY torso tokens [catalog §5]: Crabman_Torso (basic) and Crabman_EliteTorso (elite); note
            // "Crabman_EliteTorso" does NOT contain the "Crabman_Torso" substring, so both must be matched.
            bool addsEliteTorso = defs.Any(d => d.name != null
                && d.name.IndexOf("EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0);
            bool addsEliteCarapace = defs.Any(d => d.name != null
                && d.name.IndexOf("Crabman_EliteCarapace", StringComparison.OrdinalIgnoreCase) >= 0);
            bool addsBasicCarapace = defs.Any(d => d.name != null
                && d.name.IndexOf("Crabman_Carapace", StringComparison.OrdinalIgnoreCase) >= 0);
            bool wantsElite = addsEliteTorso || addsEliteCarapace;
            bool wantsBasic = !wantsElite && addsBasicCarapace;
            // The single correct torso tier this loadout needs (null => no carapace => keep recruit's own torso).
            string requiredTorsoName = wantsElite ? "Crabman_EliteTorso_BodyPartDef"
                                     : wantsBasic ? "Crabman_Torso_BodyPartDef"
                                     : null;

            // DIAGNOSTIC PROBE B: when the loadout carries a RIGHT ARM (only the DiagArmSet does — normal cell
            // loadouts are legs/torso/carapace), strip the recruit's existing right arm + hand first so the Gun
            // arm replaces the default Pincer (two right arms would conflict). Mirrors ArthronArms.SwapSet's
            // hand+arm removal contract. No-op for every real armor cell (none carry a Crabman_RightArm/RightHand).
            bool addsRightArm = defs.Any(d => d.name != null
                && (d.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0
                 || d.name.IndexOf("Crabman_RightHand", StringComparison.OrdinalIgnoreCase) >= 0));
            if (addsRightArm)
            {
                list.RemoveAll(i => i?.ItemDef?.name != null &&
                    (i.ItemDef.name.IndexOf("Crabman_RightHand", StringComparison.OrdinalIgnoreCase) >= 0
                  || (i.ItemDef.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0
                                 && !(i.ItemDef is WeaponDef))));
            }
            list.RemoveAll(i => i?.ItemDef?.name != null &&
                (i.ItemDef.name.IndexOf("Crabman_Legs", StringComparison.OrdinalIgnoreCase) >= 0
              || i.ItemDef.name.IndexOf("Crabman_Carapace", StringComparison.OrdinalIgnoreCase) >= 0
              || i.ItemDef.name.IndexOf("Crabman_EliteCarapace", StringComparison.OrdinalIgnoreCase) >= 0));
            // TORSO DE-DUP: when this loadout owns a carapace tier, UNCONDITIONALLY strip every torso of ANY tier,
            // then add back EXACTLY the one required-tier torso (resolved by name = the REAL def, never a clone, so
            // the augment swap's reference-equality holds). When the loadout owns NO carapace, leave the recruit's
            // own torso in place (added below as a no-op host) and only collapse any accidental duplicate.
            if (requiredTorsoName != null)
            {
                list.RemoveAll(i => i?.ItemDef?.name != null &&
                    (i.ItemDef.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0
                  || i.ItemDef.name.IndexOf("Crabman_EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0));
            }
            foreach (var d in defs) { list.Add(new GeoItem(d)); }

            // CARAPACE HOST-SLOT GUARD: the Carapace bodypart occupies Crabman_Carapace_SlotDef, which is PROVIDED
            // BY a torso bodypart [research/crabman-bodypart-catalog.md §5/§5b: Crabman_Torso_BodyPartDef PROVIDES
            // the Carapace slot]. A naked recruit may lack that torso, so AddonsCharacterBuilder (reportErrors:false)
            // silently culls the carapace and it never renders. The strip above already removed the wrong-tier torso
            // when a carapace tier is owned; EnsureCarapaceTorsoHost now adds the SINGLE required-tier torso back
            // (basic Carapace => Crabman_Torso_BodyPartDef, elite EliteCarapace => Crabman_EliteTorso_BodyPartDef),
            // and only when NO torso of any kind is present. Resolve+add via BuildArmorList's existing path; skip if
            // unresolved. (Legs handling untouched.)
            EnsureCarapaceTorsoHost(list, "Crabman_EliteCarapace", "Crabman_EliteTorso_BodyPartDef");
            EnsureCarapaceTorsoHost(list, "Crabman_Carapace", "Crabman_Torso_BodyPartDef");

            // POST-CONDITION (regression catch): the end state must be at most ONE torso. Light warn only on n>1.
            int torsoCount = list.Count(i => i?.ItemDef?.name != null &&
                (i.ItemDef.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0
              || i.ItemDef.name.IndexOf("Crabman_EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0));
            if (torsoCount > 1)
            {
                TheTurnedMain.LogWarn($"[TheTurned] BuildArmorList WARN: {torsoCount} torsos");
            }
            return list;
        }

        /// <summary>
        /// If <paramref name="carapaceMarker"/> is present in the list and its host <paramref name="torsoName"/>
        /// is not, resolve the torso by name (same resolve+add path BuildArmorList uses) and add it so the
        /// engine keeps the carapace's provided slot. No-op if the carapace is absent, the host is already
        /// present, or the torso def fails to resolve (logged, mirroring the existing unresolved-log).
        /// </summary>
        private static void EnsureCarapaceTorsoHost(List<GeoItem> list, string carapaceMarker, string torsoName)
        {
            bool hasCarapace = list.Any(i => i?.ItemDef?.name != null
                && i.ItemDef.name.IndexOf(carapaceMarker, StringComparison.OrdinalIgnoreCase) >= 0);
            if (!hasCarapace)
            {
                return;
            }
            // Add the host torso ONLY when NO torso of ANY tier is already present (check BOTH torso tokens, not
            // just the exact name) — otherwise a wrong-tier torso left in the list would get a SECOND torso mounted.
            // BuildArmorList's strip-then-add already normalized the tier, so when a torso IS present it is the
            // correct one; we must not add another.
            bool hasAnyTorso = list.Any(i => i?.ItemDef?.name != null
                && (i.ItemDef.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0
                 || i.ItemDef.name.IndexOf("Crabman_EliteTorso", StringComparison.OrdinalIgnoreCase) >= 0));
            if (hasAnyTorso)
            {
                return;
            }
            var torso = DefUtils.ResolveByName<TacticalItemDef>(DefUtils.Repo, torsoName);
            if (torso == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] CellArmorApply: carapace host '{torsoName}' unresolved — skipped.");
                return;
            }
            list.Add(new GeoItem(torso));
        }
    }
}
