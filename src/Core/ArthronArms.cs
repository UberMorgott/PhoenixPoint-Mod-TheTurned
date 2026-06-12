using Base.Defs;
using PhoenixPoint.Common.Entities.Addons;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities.Abilities;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace TheTurned.Core
{
    /// <summary>
    /// Physical arm/head equipment for the recruited Arthron (Crabman). Parts are bodypart items living in
    /// <c>GeoCharacter.ArmourItems</c>; right vs left is identified by the def-name token
    /// (<c>Crabman_RightHand</c> / <c>Crabman_LeftHand</c>) — NOT array index.
    ///
    /// Phase-4: <see cref="ApplyChosenSets"/> is the SINGLE SOURCE OF TRUTH. It re-derives the desired
    /// matched SETs (arm/head bodypart + hand weapon — C3: always swapped together) from the Phase-4
    /// marker abilities currently learned (mutoid-popup progression / PerkOracle swaps) and applies them
    /// with ONE <c>GeoCharacter.SetItems</c> call; <see cref="ArmFollowHook"/> re-runs it on every
    /// <c>OnAbilityAdded</c>. <see cref="ResetMismatchedArms"/> is the §9 one-shot ADDITIVE migration for
    /// pre-e403584 saves (hand weapon without its matched arm bodypart → default SET; otherwise C4: touch
    /// nothing). Legacy Phase-3 recruit-time roll machinery is kept ONLY for old-save def compat, [Obsolete].
    ///
    /// Grounded API: arms = <c>TacCharacterDef.Data.BodypartItems[]</c> → descriptor <c>ArmorItems</c>
    /// → live <c>GeoCharacter._armourItems</c> (persisted <c>BodypartItemsData</c>); live swap via
    /// <c>SetItems(armour:..)</c> (re-derives abilities + stats). <c>WeaponDef : EquipmentDef :
    /// TacticalItemDef : ItemDef</c>, so a <c>WeaponDef</c> is a valid armour/bodypart item + GeoItem def.
    /// </summary>
    internal static class ArthronArms
    {
        internal const string RightHandToken = "Crabman_RightHand";
        internal const string LeftHandToken = "Crabman_LeftHand";
        internal const string RightArmToken = "Crabman_RightArm";
        internal const string LeftArmToken = "Crabman_LeftArm";

        /// <summary>One arm option: the bodypart weapon def + its marker ability + which side it occupies.</summary>
        internal sealed class ArmOption
        {
            internal WeaponDef Weapon;
            internal PassiveModifierAbilityDef Marker;
            internal bool IsRight;
        }

        // Discovered options per side (rebuilt idempotently by BuildOptions).
        private static readonly List<ArmOption> _right = new List<ArmOption>();
        private static readonly List<ArmOption> _left = new List<ArmOption>();
        // marker ability def Guid -> arm option (for the live re-derive).
        private static readonly Dictionary<string, ArmOption> _byMarkerGuid = new Dictionary<string, ArmOption>();
        private static bool _logged;
        private static bool _built;

        private static ModLogger Log => TheTurnedMain.Main?.Logger;

        internal static bool HasOptions => _right.Count > 0 || _left.Count > 0;

        /// <summary>
        /// Idempotently enumerate the Arthron arm <c>WeaponDef</c>s, log the full list ONCE (fills the
        /// research OPEN items: base claw/shield/plain-gun names), and build one marker ability per option.
        /// Safe to call on every re-enable / geoscape reload.
        /// </summary>
        internal static void BuildOptions(DefRepository repo)
        {
            if (repo == null)
            {
                return;
            }

            List<WeaponDef> arms = repo.GetAllDefs<WeaponDef>()
                .Where(d => d != null && d.name != null
                    && (d.name.Contains(RightHandToken) || d.name.Contains(LeftHandToken)))
                .OrderBy(d => d.name, StringComparer.Ordinal)
                .ToList();

            if (!_logged)
            {
                Log?.LogInfo($"[TheTurned] Arthron arm WeaponDef enumeration ({arms.Count} found):");
                foreach (WeaponDef w in arms)
                {
                    Log?.LogInfo($"[TheTurned]   arm '{w.name}' guid='{w.Guid}' -> {(IsRight(w.name) ? "RIGHT" : "LEFT")} / {Classify(w.name)}");
                }
                _logged = true;
            }

            _right.Clear();
            _left.Clear();
            _byMarkerGuid.Clear();

            foreach (WeaponDef w in arms)
            {
                bool isRight = IsRight(w.name);
                ArmOption opt = new ArmOption
                {
                    Weapon = w,
                    IsRight = isRight,
                    Marker = BuildMarkerFor(repo, w, isRight)
                };
                if (opt.Marker == null)
                {
                    continue;
                }
                _byMarkerGuid[opt.Marker.Guid] = opt;
                (isRight ? _right : _left).Add(opt);
            }

            _built = true;
            Log?.LogInfo($"[TheTurned] Arthron arm options built: right={_right.Count}, left={_left.Count}.");
        }

        /// <summary>
        /// Roll one right + one left option at random and return their marker abilities. Returns nulls for a
        /// side whose pool is empty (caller skips that side gracefully, keeping the default arm).
        /// </summary>
        internal static void Roll(out ArmOption right, out ArmOption left)
        {
            right = _right.Count > 0 ? _right[UnityEngine.Random.Range(0, _right.Count)] : null;
            left = _left.Count > 0 ? _left[UnityEngine.Random.Range(0, _left.Count)] : null;
        }

        /// <summary>True if the def Guid belongs to one of our arm-marker abilities.</summary>
        internal static bool IsArmMarker(TacticalAbilityDef ability)
        {
            return ability != null && ability.Guid != null && _byMarkerGuid.ContainsKey(ability.Guid);
        }

        /// <summary>
        /// Re-derive BOTH arms from the arm-markers currently in <paramref name="geoChar"/>'s ability set and
        /// apply them via <c>SetItems</c>. Idempotent and safe to call repeatedly: only writes when an arm
        /// actually changes. If a side has no marker present (e.g. swapped out for a non-arm perk), that
        /// side's current physical arm is left untouched (a unit always has two arms).
        /// </summary>
        [Obsolete("hand-only swap; use ApplyChosenSets")]
        internal static void ApplyRolledArms(GeoCharacter geoChar)
        {
            if (geoChar?.Progression == null || !_built)
            {
                return;
            }
            try
            {
                WeaponDef desiredRight = null;
                WeaponDef desiredLeft = null;
                // Scan BOTH the personal-track slots (where a rolled/swapped marker always lives, even before
                // it is "learned" on level-up) AND the learned ability set (PerkOracle AddAbility path).
                foreach (TacticalAbilityDef ability in EnumerateLearnedAbilities(geoChar.Progression))
                {
                    if (ability?.Guid != null && _byMarkerGuid.TryGetValue(ability.Guid, out ArmOption opt) && opt.Weapon != null)
                    {
                        if (opt.IsRight)
                        {
                            desiredRight = opt.Weapon;
                        }
                        else
                        {
                            desiredLeft = opt.Weapon;
                        }
                    }
                }

                if (desiredRight == null && desiredLeft == null)
                {
                    return; // no arm markers -> nothing to enforce
                }

                List<GeoItem> current = geoChar.ArmourItems.ToList();
                WeaponDef curRight = current.Select(i => i.ItemDef as WeaponDef)
                    .FirstOrDefault(d => d != null && d.name != null && d.name.Contains(RightHandToken));
                WeaponDef curLeft = current.Select(i => i.ItemDef as WeaponDef)
                    .FirstOrDefault(d => d != null && d.name != null && d.name.Contains(LeftHandToken));

                bool changeRight = desiredRight != null && desiredRight != curRight;
                bool changeLeft = desiredLeft != null && desiredLeft != curLeft;
                if (!changeRight && !changeLeft)
                {
                    return; // already correct -> avoid a redundant UpdateStats
                }

                List<GeoItem> newList = new List<GeoItem>();
                foreach (GeoItem item in current)
                {
                    WeaponDef wd = item.ItemDef as WeaponDef;
                    bool isRightArm = wd?.name != null && wd.name.Contains(RightHandToken);
                    bool isLeftArm = wd?.name != null && wd.name.Contains(LeftHandToken);
                    if (changeRight && isRightArm)
                    {
                        continue; // drop old right arm
                    }
                    if (changeLeft && isLeftArm)
                    {
                        continue; // drop old left arm
                    }
                    newList.Add(item);
                }
                if (changeRight)
                {
                    newList.Add(new GeoItem(desiredRight));
                }
                if (changeLeft)
                {
                    newList.Add(new GeoItem(desiredLeft));
                }

                geoChar.SetItems(armour: newList);
                Log?.LogInfo($"[TheTurned] Arthron arms applied for '{geoChar.GetName()}': "
                    + $"right='{(desiredRight ?? curRight)?.name}', left='{(desiredLeft ?? curLeft)?.name}'.");
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] ApplyRolledArms failed: {e.Message}");
            }
        }

        /// <summary>Re-derive desired matched SETs (right arm / left arm / head) from learned Phase-4 marker abilities
        /// and apply via ONE SetItems call. Single source of truth = learned markers. C3: bodypart+hand together.</summary>
        internal static void ApplyChosenSets(GeoCharacter geoChar)
        {
            try
            {
                if (geoChar?.Progression == null || !CrabmanParts.HasSets)
                {
                    return;
                }
                MatchedSet wantRight = null, wantLeft = null, wantHead = null;
                WeaponDef clawOverride = null; // claw row: cloned claw weapon replaces the default right set's hand
                foreach (TacticalAbilityDef ability in EnumerateLearnedAbilities(geoChar.Progression))
                {
                    if (ability == null)
                    {
                        continue;
                    }
                    if (Phase4Markers.TryGetArmSet(ability, out MatchedSet set))
                    {
                        if (set.IsRight) { wantRight = set; } else { wantLeft = set; }
                    }
                    else if (Phase4Markers.TryGetHeadSet(ability, out MatchedSet head))
                    {
                        wantHead = head;
                    }
                    else if (Phase4Markers.TryGetClawWeapon(ability, out WeaponDef claw))
                    {
                        clawOverride = claw;
                    }
                }
                if (clawOverride != null && wantRight == null)
                {
                    wantRight = CrabmanParts.DefaultRight;
                }

                // WEAPON EVOLUTION (generic): upgrade the player's CHOSEN set to its elite variant when an
                // evolve cell is learned. Scope is the MAXIMUM learned tier (L5 AllWeapons implies L4
                // LeftWeapon). Composed HERE in the same derive that owns the arms, so it survives the
                // per-frame refresh + the cell-armor prefix and stays idempotent (re-derived each pass).
                // Honour player choice: evolve ONLY the chosen set; null/already-elite/no-mapping -> unchanged.
                // The token map + scope-per-cell are monster DATA (EvolutionMarkers, filled by ArthronEvolution).
                EvolveScope scope = EvolutionMarkers.HighestLearnedScope(geoChar.Progression);
                if (scope >= EvolveScope.LeftWeapon)
                {
                    wantLeft = EvolveSet(wantLeft, CrabmanParts.LeftArmSets);
                }
                if (scope >= EvolveScope.AllWeapons)
                {
                    // Right arm: skip when a claw clone owns the right hand (the clone IS the chosen weapon).
                    if (clawOverride == null)
                    {
                        wantRight = EvolveSet(wantRight, CrabmanParts.RightArmSets);
                    }
                    wantHead = EvolveSet(wantHead, CrabmanParts.HeadSets);
                }

                var newList = new List<GeoItem>(geoChar.ArmourItems);
                bool changed = false;
                changed |= SwapSet(newList, RightHandToken, RightArmToken, wantRight,
                                   (clawOverride != null && wantRight == CrabmanParts.DefaultRight) ? clawOverride : null);
                changed |= SwapSet(newList, LeftHandToken, LeftArmToken, wantLeft, null);
                changed |= SwapSet(newList, "Crabman_Head", "Crabman_Head", wantHead, null);

                // BUG3: EVOLVE EQUIPPED GEAR. Gear equipped through the augment/Bionics DNA screen lands in
                // geoChar.ArmourItems WITHOUT learning any marker ability, so the marker-derived wants above
                // never see it -> it would stay base at L4/L5. Walk the equipped Crabman arm/hand/head items and,
                // for each within the current evolve scope, upgrade its base token to the registered elite
                // MatchedSet (same token map + side lists EvolveSet uses). Idempotent: already-elite tokens have
                // no upgrade (TryGetEliteToken=false) and SwapSet skips when the elite is already present.
                changed |= EvolveEquippedGear(newList, scope, clawOverride);

                if (changed)
                {
                    geoChar.SetItems(armour: newList);
                    Log?.LogInfo($"[TheTurned] ApplyChosenSets for '{geoChar.GetName()}': "
                        + $"R='{wantRight?.Token}' L='{wantLeft?.Token}' H='{wantHead?.Token}' claw='{clawOverride?.name}'");
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning("[TheTurned] ApplyChosenSets failed: " + e);
            }
        }

        /// <summary>GENERIC evolve step: replace <paramref name="chosen"/> with the elite-variant MatchedSet
        /// from the SAME side list when an upgrade is registered (EvolutionMarkers DATA). Returns the chosen
        /// set unchanged when it is null, already elite, has no registered upgrade, or the elite MatchedSet is
        /// absent from the enumeration. Idempotent + honours player choice (only the chosen set is touched).</summary>
        private static MatchedSet EvolveSet(MatchedSet chosen, IEnumerable<MatchedSet> sideSets)
        {
            if (chosen == null || !EvolutionMarkers.TryGetEliteToken(chosen.Token, out string eliteToken))
            {
                return chosen;
            }
            MatchedSet elite = sideSets?.FirstOrDefault(s => CrabmanParts.TokenExact(s.Token, eliteToken));
            if (elite == null)
            {
                Log?.LogWarning($"[TheTurned] evolve: elite set '{eliteToken}' for chosen '{chosen.Token}' "
                    + "not found in enumeration — left unevolved.");
                return chosen;
            }
            Log?.LogInfo($"[TheTurned] evolve: '{chosen.Token}' -> '{elite.Token}'.");
            return elite;
        }

        /// <summary>BUG3: evolve gear that is EQUIPPED (in <paramref name="items"/>) but learned no marker, so the
        /// marker-derived <see cref="ApplyChosenSets"/> wants never covered it. For each equipped Crabman
        /// arm/hand/head item whose side is within <paramref name="scope"/>, map its base variant token to the
        /// registered elite MatchedSet and swap the bodypart+hand. Idempotent (already-elite tokens have no
        /// upgrade; SwapSet skips when the elite is already present). Returns true if anything changed.
        /// Scope: L4/LeftWeapon -> left only; L5/AllWeapons -> left + right + head.</summary>
        private static bool EvolveEquippedGear(List<GeoItem> items, EvolveScope scope, WeaponDef clawOverride)
        {
            if (scope == EvolveScope.None || items == null)
            {
                return false;
            }
            bool changed = false;
            // Snapshot the equipped tokens per side FIRST (the loop below mutates `items` via SwapSet, so we
            // must not iterate it live). Prefer the BODYPART token; fall back to the hand token.
            string leftToken = ExtractSideToken(items, LeftArmToken, LeftHandToken);
            string rightToken = ExtractSideToken(items, RightArmToken, RightHandToken);

            if (scope >= EvolveScope.LeftWeapon)
            {
                changed |= EvolveEquippedSide(items, leftToken, CrabmanParts.LeftArmSets, LeftHandToken, LeftArmToken);
            }
            if (scope >= EvolveScope.AllWeapons)
            {
                // Mirror the marker-derived right-arm guard in ApplyChosenSets: when a claw clone owns the right
                // hand (the clone IS the player's chosen weapon, and clones keep the Crabman_RightHand token),
                // SKIP the right side — evolving would SwapSet the elite set's default hand over the player's
                // claw, clobbering their choice. The HEAD is NEVER auto-evolved here: it is chosen manually in
                // the DNA/augment screen and auto-evolve must never clobber it (was causing manual-pick revert
                // + per-frame head flicker). Only the right + left arm/shield/launcher sides evolve.
                if (clawOverride == null)
                {
                    changed |= EvolveEquippedSide(items, rightToken, CrabmanParts.RightArmSets, RightHandToken, RightArmToken);
                }
            }
            return changed;
        }

        /// <summary>BUG3: find the variant token of the equipped item on one side. Scans <paramref name="items"/>
        /// for a def whose name embeds <paramref name="bodyPrefix"/> (preferred) or <paramref name="handPrefix"/>,
        /// strips any leading "TheTurned_" clone prefix by matching from the embedded "Crabman_*" substring, and
        /// returns the variant token via <see cref="CrabmanParts.VariantToken"/>. Null when nothing equipped.</summary>
        private static string ExtractSideToken(List<GeoItem> items, string bodyPrefix, string handPrefix)
        {
            string body = TokenFromItems(items, bodyPrefix, requireNonWeapon: !string.Equals(bodyPrefix, handPrefix));
            if (!string.IsNullOrEmpty(body))
            {
                return body;
            }
            return TokenFromItems(items, handPrefix, requireNonWeapon: false);
        }

        private static string TokenFromItems(List<GeoItem> items, string prefix, bool requireNonWeapon)
        {
            foreach (GeoItem item in items)
            {
                string name = item?.ItemDef?.name;
                if (name == null)
                {
                    continue;
                }
                if (requireNonWeapon && item.ItemDef is WeaponDef)
                {
                    continue;
                }
                int idx = name.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (idx < 0)
                {
                    continue;
                }
                // Slice from the embedded "Crabman_*" prefix so VariantToken's Substring(prefix.Length) is valid
                // even when the def carries a leading "TheTurned_" (authored clone) prefix.
                string token = CrabmanParts.VariantToken(name.Substring(idx), prefix);
                if (!string.IsNullOrEmpty(token))
                {
                    return token;
                }
            }
            return null;
        }

        /// <summary>BUG3: if <paramref name="baseToken"/> has a registered elite upgrade present in
        /// <paramref name="sideSets"/>, swap that elite MatchedSet (bodypart+hand) into <paramref name="items"/>.</summary>
        private static bool EvolveEquippedSide(List<GeoItem> items, string baseToken,
            IEnumerable<MatchedSet> sideSets, string handToken, string bodyToken)
        {
            if (string.IsNullOrEmpty(baseToken)
                || !EvolutionMarkers.TryGetEliteToken(baseToken, out string eliteToken))
            {
                return false;
            }
            MatchedSet elite = sideSets?.FirstOrDefault(s => CrabmanParts.TokenExact(s.Token, eliteToken));
            if (elite == null)
            {
                Log?.LogWarning($"[TheTurned] evolve-equipped: elite set '{eliteToken}' for equipped "
                    + $"'{baseToken}' not found in enumeration — left unevolved.");
                return false;
            }
            if (SwapSet(items, handToken, bodyToken, elite, null))
            {
                Log?.LogInfo($"[TheTurned] evolve-equipped: '{baseToken}' -> '{elite.Token}'.");
                return true;
            }
            return false;
        }


        /// <summary>
        /// Force a freshly-recruited Crabman to the NAKED BASE loadout (the weakest/earliest Arthron). The
        /// base def the game ships (`Crabby_AlienMutationVariationDef`) carries spitter head + shield + elite
        /// legs, so the naked loadout must be CONSTRUCTED. Keeps the chassis Humanoid head + right Pincer
        /// (+ its SubAddon hand) + torso; corrects the other three slots:
        ///   - HEAD: drop the spitter head WEAPON (the Humanoid head bodypart is on the chassis) -> no spit.
        ///   - LEFT arm: replace whatever is there with the plain `Crabman_LeftArm_BodyPartDef` (real part).
        ///   - LEGS: replace elite/armoured legs with the unarmored base `Crabman_Legs_Agile_ItemDef`.
        /// All defs resolved by exact NAME (bundle GUIDs unknown). Missing def -> warn + leave that slot.
        /// </summary>
        internal static void ApplyNakedBase(GeoCharacter geoChar)
        {
            try
            {
                if (geoChar?.ArmourItems == null)
                {
                    return;
                }
                DefRepository repo = DefUtils.Repo;
                // Plain head BODYPART (no spit). The Crabby chassis occupies the head slot with the SPITTER
                // WEAPON itself (Crabman_Head_Spitter_WeaponDef as the BodypartItems[0] head occupant) — there
                // is NO separate Humanoid head bodypart in its items (proven by the BEFORE dump below + the
                // "items 5 -> 4" headcount: removing the head weapon left the head slot EMPTY -> headless).
                // So the naked base must explicitly ADD the plain Humanoid head bodypart, not merely strip spit.
                TacticalItemDef plainHead = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Head_Humanoid_BodyPartDef");
                TacticalItemDef plainLeft = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_LeftArm_BodyPartDef");
                TacticalItemDef agileLegs = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Legs_Agile_ItemDef");

                var list = new List<GeoItem>(geoChar.ArmourItems);
                int before = list.Count;

                // EVIDENCE (one-shot per session): dump the chassis's FULL head-slot picture BEFORE we touch it,
                // so a single Player.log pass proves exactly what occupies the head slot and what we change.
                LogBodypartDumpOnce("naked base BEFORE", geoChar, list);

                // HEAD: clear the whole head slot — any head WEAPON (the spitter) AND any head BODYPART — then
                // add exactly the plain Humanoid head bodypart. Slot-clear-then-add (mirrors LEFT/LEGS) makes
                // the result identical regardless of how the chassis structured its head, and is idempotent.
                list.RemoveAll(i => i?.ItemDef?.name != null
                    && i.ItemDef.name.IndexOf("Crabman_Head", StringComparison.OrdinalIgnoreCase) >= 0);
                if (plainHead != null)
                {
                    list.Add(new GeoItem(plainHead));
                }
                else
                {
                    Log?.LogWarning("[TheTurned] naked base: 'Crabman_Head_Humanoid_BodyPartDef' not found — "
                        + "recruit may spawn HEADLESS.");
                }

                // LEFT arm: remove the existing left bodypart + any left hand weapon, add the plain arm.
                list.RemoveAll(i => i?.ItemDef?.name != null
                    && (i.ItemDef.name.IndexOf(LeftHandToken, StringComparison.OrdinalIgnoreCase) >= 0
                     || (i.ItemDef.name.IndexOf(LeftArmToken, StringComparison.OrdinalIgnoreCase) >= 0 && !(i.ItemDef is WeaponDef))));
                if (plainLeft != null)
                {
                    list.Add(new GeoItem(plainLeft));
                }
                else
                {
                    Log?.LogWarning("[TheTurned] naked base: 'Crabman_LeftArm_BodyPartDef' not found — left arm left as-is.");
                }

                // LEGS: remove any leg item, add the unarmored Agile legs.
                list.RemoveAll(i => i?.ItemDef?.name != null
                    && i.ItemDef.name.IndexOf("Crabman_Legs", StringComparison.OrdinalIgnoreCase) >= 0);
                if (agileLegs != null)
                {
                    list.Add(new GeoItem(agileLegs));
                }
                else
                {
                    Log?.LogWarning("[TheTurned] naked base: 'Crabman_Legs_Agile_ItemDef' not found — legs left as-is.");
                }

                geoChar.SetItems(armour: list);
                LogBodypartDumpOnce("naked base AFTER", geoChar, list);
                Log?.LogInfo($"[TheTurned] naked base applied for '{geoChar.GetName()}' "
                    + $"(items {before} -> {list.Count}; set plain Humanoid head (no spit), plain left arm, agile legs).");
            }
            catch (Exception e)
            {
                Log?.LogWarning("[TheTurned] ApplyNakedBase failed: " + e);
            }
        }

        private static bool _bodypartDumpLogged;

        /// <summary>One-shot-per-session diagnostic: dump each item in <paramref name="list"/> with its name +
        /// whether it is a WeaponDef, so the head-slot occupant (bodypart vs weapon) is unambiguous in the log.</summary>
        private static void LogBodypartDumpOnce(string tag, GeoCharacter geoChar, List<GeoItem> list)
        {
            if (_bodypartDumpLogged && tag.IndexOf("BEFORE", StringComparison.Ordinal) >= 0)
            {
                return; // gate only on BEFORE so a matched BEFORE/AFTER pair always prints together
            }
            if (list == null)
            {
                return;
            }
            string dump = string.Join(", ", list.Select(i =>
            {
                ItemDef d = i?.ItemDef;
                if (d == null) { return "<null>"; }
                return d.name + (d is WeaponDef ? " [WeaponDef]" : " [BodyPart]");
            }));
            Log?.LogInfo($"[TheTurned] {tag} for '{geoChar?.GetName()}' items({list.Count})=[{dump}]");
            if (tag.IndexOf("AFTER", StringComparison.Ordinal) >= 0)
            {
                _bodypartDumpLogged = true; // close the one-shot only after the AFTER line prints
            }
        }

        // §9 migration: characters already checked this session (one-shot; ScanAndSubscribe re-runs on every
        // geoscape load, the cleanup must not).
        private static readonly HashSet<GeoCharacter> _migrationChecked = new HashSet<GeoCharacter>();

        /// <summary>§9 arm-roll cleanup: pre-e403584 saves may carry a hand weapon without its matched arm bodypart.
        /// If a side has a Crabman hand weapon whose matched arm bodypart is absent AND no Phase-4 arm/claw marker is
        /// learned for that side, reset that side to the default SET (one SetItems). Additive otherwise (C4).</summary>
        internal static void ResetMismatchedArms(GeoCharacter geoChar)
        {
            if (!CrabmanParts.HasSets || geoChar?.Progression == null || !_migrationChecked.Add(geoChar))
            {
                return;
            }
            try
            {
                // Sides owned by a learned Phase-4 marker are skipped: ApplyChosenSets is the source of
                // truth there (a claw marker owns the RIGHT side — its clone replaces the default hand).
                bool rightOwned = false, leftOwned = false;
                foreach (TacticalAbilityDef ability in EnumerateLearnedAbilities(geoChar.Progression))
                {
                    if (Phase4Markers.TryGetArmSet(ability, out MatchedSet set))
                    {
                        if (set.IsRight) { rightOwned = true; } else { leftOwned = true; }
                    }
                    else if (Phase4Markers.TryGetClawWeapon(ability, out _))
                    {
                        rightOwned = true;
                    }
                }

                var items = new List<GeoItem>(geoChar.ArmourItems);
                bool changed = false;
                if (!rightOwned)
                {
                    changed |= ResetSideIfMismatched(items, RightHandToken, RightArmToken, CrabmanParts.DefaultRight, geoChar);
                }
                if (!leftOwned)
                {
                    changed |= ResetSideIfMismatched(items, LeftHandToken, LeftArmToken, CrabmanParts.DefaultLeft, geoChar);
                }
                if (changed)
                {
                    geoChar.SetItems(armour: items);
                }
            }
            catch (Exception e)
            {
                Log?.LogWarning($"[TheTurned] ResetMismatchedArms failed for '{geoChar.GetName()}': {e}");
            }
        }

        /// <summary>One side of the §9 cleanup: hand weapon present but its variant-matched arm bodypart absent
        /// -> swap the side to <paramref name="defaultSet"/> (mutates <paramref name="items"/> only; the caller
        /// commits with ONE SetItems). Returns true when the side was reset.</summary>
        private static bool ResetSideIfMismatched(List<GeoItem> items, string handToken, string armToken,
            MatchedSet defaultSet, GeoCharacter geoChar)
        {
            if (defaultSet == null)
            {
                return false;
            }
            WeaponDef hand = items.Select(i => i?.ItemDef as WeaponDef)
                .FirstOrDefault(d => d?.name != null && d.name.IndexOf(handToken, StringComparison.OrdinalIgnoreCase) >= 0);
            if (hand == null)
            {
                return false; // no hand weapon on that side -> nothing to migrate (C4: additive)
            }
            // Variant token of the hand (substring-safe: Task-8 clones carry a TheTurned_ name prefix).
            string handVariant = CrabmanParts.VariantToken(
                hand.name.Substring(hand.name.IndexOf(handToken, StringComparison.OrdinalIgnoreCase)), handToken);
            bool armPresent = items.Any(i =>
            {
                var d = i?.ItemDef;
                if (d?.name == null || d is WeaponDef)
                {
                    return false;
                }
                int at = d.name.IndexOf(armToken, StringComparison.OrdinalIgnoreCase);
                if (at < 0)
                {
                    return false;
                }
                string armVariant = CrabmanParts.VariantToken(d.name.Substring(at), armToken);
                return CrabmanParts.TokenExact(armVariant, handVariant) || CrabmanParts.TokenFuzzy(armVariant, handVariant);
            });
            if (armPresent)
            {
                return false; // hand+arm consistent -> leave the roll alone (C4: additive)
            }
            bool changed = SwapSet(items, handToken, armToken, defaultSet, null);
            if (changed)
            {
                Log?.LogWarning($"[TheTurned] §9 migration: '{geoChar.GetName()}' hand '{hand.name}' had no matched "
                    + $"'{armToken}' bodypart — side reset to default set '{defaultSet.Token}'.");
            }
            return changed;
        }

        /// <summary>
        /// V1 Phase-2 apply path: when an augment-screen card (a Crabman arm/head BODYPART) is equipped, the
        /// native screen places the bodypart ALONE — its matched hand weapon is NOT brought along (a single
        /// card = one GeoItem, [G UIModuleBionics.cs:196]). Enforce the matched SET (C3) by removing that
        /// side's stale hand+arm and adding the desired bodypart + its hand together. Returns the rebuilt
        /// item list when a change was needed, else null (no-op). Caller commits with ONE SetItems.
        /// </summary>
        internal static List<GeoItem> EnforceSetForBodypart(GeoCharacter geoChar, ItemDef appliedBodypart)
        {
            if (geoChar == null || appliedBodypart == null || !CrabmanParts.HasSets)
            {
                return null;
            }

            // === GENERIC PER-SLOT DEDUP (root-cause fix for the per-frame ArmorContainer churn loop) ===
            // The native augment swap evicts a prior occupant ONLY when the engine recognises it as the same
            // slot. It fails to do so in two authored cases: (a) a head occupant is a WeaponDef placed into the
            // head bodypart slot (Spitter <-> Evolved_Spitter), and (b) an arm slot ends up with two bodyparts
            // (e.g. left-arm Shield AND the acid-grenade "bazooka") after toggling variants in the DNA screen.
            // With two occupants of ONE slot, the native ArmorContainer (UIInventoryList.GetFirstAvailableSlot)
            // cannot place both and destroys+recreates one every frame forever (~60fps churn).
            //
            // Robust GENERAL fix, for ANY side/slot (head, left arm, right arm, torso, legs, carapace): key
            // occupants by the APPLIED item's OWN RequiredSlotBinds slot Guids and remove every OTHER occupant
            // whose slot Guids intersect -> at most ONE occupant per slot remains (the just-applied one).
            // SAFETY: a bodypart's HAND weapon is a SubAddon occupying a DIFFERENT RequiredSlot (different Guid)
            // than the arm [G UIModuleBionics.cs:421 resolves them as separate slot binds], so its slot Guids do
            // NOT intersect the arm's -> the hand is NEVER evicted. Items with no RequiredSlotBinds are never
            // matched (and an applied item with no slot binds skips dedup entirely) -> nothing is stripped
            // unsafely. For a normal swap (already exactly one occupant) this is a NO-OP -> native left alone.
            List<GeoItem> working = null;
            var appliedSlotGuids = new HashSet<string>(SlotGuids(appliedBodypart));
            if (appliedSlotGuids.Count > 0)
            {
                var deduped = new List<GeoItem>(geoChar.ArmourItems);
                int removed = deduped.RemoveAll(i => i?.ItemDef != null
                    && i.ItemDef.Guid != appliedBodypart.Guid
                    && SlotGuids(i.ItemDef).Any(g => appliedSlotGuids.Contains(g)));
                // Guard: ensure the just-applied bodypart is present (native click placed it; re-add if a
                // refresh dropped it) so the manual pick always persists after dedup.
                bool readded = false;
                if (!deduped.Any(i => i?.ItemDef != null && i.ItemDef.Guid == appliedBodypart.Guid))
                {
                    deduped.Add(new GeoItem(appliedBodypart));
                    readded = true;
                }
                // Airtight collapse: if the SAME applied def somehow occupies the slot more than once (two
                // GeoItems of the same Guid), keep exactly ONE — otherwise the native ArmorContainer still sees
                // two occupants of one slot and the per-frame churn persists. Keep the first, drop the rest.
                bool seenApplied = false;
                removed += deduped.RemoveAll(i =>
                {
                    if (i?.ItemDef == null || i.ItemDef.Guid != appliedBodypart.Guid)
                    {
                        return false;
                    }
                    if (seenApplied)
                    {
                        return true; // a duplicate copy of the applied def -> drop
                    }
                    seenApplied = true;
                    return false; // keep the first applied occupant
                });
                if (removed > 0 || readded)
                {
                    Log?.LogInfo($"[TheTurned] augment apply (slot dedup): kept '{appliedBodypart.name}', removed "
                        + $"{removed} stale occupant(s) in slot(s) [{string.Join(", ", appliedSlotGuids)}] "
                        + $"for '{geoChar.GetName()}'.");
                    working = deduped;
                }
            }

            // Locate the matched SET whose bodypart IS the applied card, and the side tokens to swap on.
            MatchedSet set = null;
            string handToken = null;
            string bodyToken = null;
            IEnumerable<MatchedSet> sideSets = null;
            EvolveScope sideScope = EvolveScope.None; // scope at/above which THIS side evolves to elite
            if ((set = CrabmanParts.RightArmSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = RightHandToken; bodyToken = RightArmToken;
                sideSets = CrabmanParts.RightArmSets; sideScope = EvolveScope.AllWeapons; // right evolves at L5
            }
            else if ((set = CrabmanParts.LeftArmSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = LeftHandToken; bodyToken = LeftArmToken;
                sideSets = CrabmanParts.LeftArmSets; sideScope = EvolveScope.LeftWeapon; // left evolves at L4
            }
            else if ((set = CrabmanParts.HeadSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = "Crabman_Head"; bodyToken = "Crabman_Head";
                sideSets = CrabmanParts.HeadSets; sideScope = EvolveScope.AllWeapons; // head evolves at L5
            }
            else
            {
                // Not one of our variant bodyparts (torso/legs/carapace/etc.): the generic slot-dedup above is
                // the only enforcement -> return its result (deduped list if it removed a stale same-slot
                // occupant, else null to leave the native swap alone).
                return working;
            }

            // The HEAD is picked MANUALLY in the DNA/augment screen and must be applied VERBATIM — auto-evolve
            // must NEVER touch it (was reverting the manual pick). The generic slot-dedup above already left a
            // single head occupant (head WeaponDef clones carry Crabman_Head_SlotDef RequiredSlotBinds), so the
            // head needs nothing further here; the sideSets!=HeadSets guard below skips evolve for it.

            // BUG4b: PERK-AWARE apply. Re-derive base-vs-elite from the LEARNED CELLS first (same scope logic
            // ApplyChosenSets uses), THEN apply — instead of mutating off whatever stale base part the native
            // click placed. If the learned evolve scope covers THIS side, swap the placed BASE set for its elite
            // variant (full bodypart+hand swap via the shared SwapSet). Makes re-entry idempotent + perk-correct
            // (EvolveSet returns the set unchanged when already elite / no upgrade -> no-op for non-evolvable
            // cards). Dovetails with BUG3's evolve-equipped pass (shared EvolveSet + SwapSet helpers). Runs on
            // the deduped working list when dedup fired, so evolve composes with the per-slot cleanup.
            EvolveScope learnedScope = EvolutionMarkers.HighestLearnedScope(geoChar.Progression);
            if (sideSets != CrabmanParts.HeadSets && learnedScope >= sideScope)
            {
                MatchedSet evolved = EvolveSet(set, sideSets);
                if (evolved != null && evolved != set)
                {
                    var evolveItems = working ?? new List<GeoItem>(geoChar.ArmourItems);
                    if (SwapSet(evolveItems, handToken, bodyToken, evolved, null))
                    {
                        Log?.LogInfo($"[TheTurned] augment apply (perk-aware): evolved applied '{set.Token}' "
                            + $"-> '{evolved.Token}' for '{geoChar.GetName()}' (learned scope {learnedScope}).");
                        return evolveItems;
                    }
                }
            }

            // BUG-B ROOT CAUSE: the native click already placed the bodypart correctly. Native Crabman arm
            // bodyparts carry their hand weapon as a SubAddon (verified dump:
            // Crabman_RightArm_Gun_BodyPartDef.SubAddons = [Crabman_RightHand_Gun_WeaponDef]); SubAddons
            // "are attached and detached together with the main addon ALWAYS" [G AddonDef.cs:74]. So the
            // engine brings the hand from the bodypart itself. Force-adding a SECOND flat hand GeoItem
            // conflicts with the SubAddon-attached one and the arm was being DROPPED. Therefore: only add a
            // matched hand when the applied bodypart does NOT already declare it as a SubAddon (true for our
            // AUTHORED head clones — clones of the Humanoid head, which has no spitter SubAddon). For native
            // arm/shield/base-head cards this is a NO-OP and the native+SubAddon path is left untouched.
            if (set.Hand == null || BodypartCarriesHandSubaddon(appliedBodypart, set.Hand))
            {
                return working; // no flat hand to add -> return the deduped list (or null) untouched
            }
            // Authored clone (e.g. spitter head): the hand is not a SubAddon, so add it as a flat item,
            // first removing any stale hand on this side. Do NOT touch the bodypart (native placed it).
            var items = working ?? new List<GeoItem>(geoChar.ArmourItems);
            int before = items.Count;
            items.RemoveAll(i => i?.ItemDef is WeaponDef && i.ItemDef.name != null
                && i.ItemDef.name.IndexOf(handToken, StringComparison.OrdinalIgnoreCase) >= 0);
            bool alreadyHasHand = items.Any(i => i?.ItemDef != null && i.ItemDef.Guid == set.Hand.Guid);
            if (!alreadyHasHand)
            {
                items.Add(new GeoItem(set.Hand));
            }
            if (items.Count == before && alreadyHasHand)
            {
                return working; // nothing changed by the hand path -> return the deduped list (or null)
            }
            Log?.LogInfo($"[TheTurned] augment apply: added matched hand '{set.Hand.name}' for authored "
                + $"bodypart '{appliedBodypart.name}' (set '{set.Token}') on '{geoChar.GetName()}'.");
            return items;
        }

        /// <summary>
        /// Slot keys for an item: the <c>Guid</c> of each <see cref="AddonSlotDef"/> the item's OWN
        /// <see cref="AddonDef.RequiredSlotBinds"/> target. Two items are "same slot" iff these sets intersect.
        /// Deliberately does NOT fold in SubAddon slot binds — a bodypart's hand SubAddon lives in a DIFFERENT
        /// RequiredSlot, so keying on the item's own binds guarantees per-slot dedup never evicts the hand.
        /// </summary>
        private static IEnumerable<string> SlotGuids(ItemDef def)
        {
            return (def?.RequiredSlotBinds ?? Array.Empty<AddonDef.RequiredSlotBind>())
                .Where(b => b.RequiredSlot != null)
                .Select(b => b.RequiredSlot.Guid);
        }

        /// <summary>True if the bodypart declares <paramref name="hand"/> among its SubAddons (so the engine
        /// auto-attaches it and we must NOT add a conflicting flat copy).</summary>
        private static bool BodypartCarriesHandSubaddon(ItemDef bodypart, WeaponDef hand)
        {
            AddonDef.SubaddonBind[] subs = (bodypart as TacticalItemDef)?.SubAddons;
            if (subs == null || hand == null)
            {
                return false;
            }
            foreach (AddonDef.SubaddonBind sb in subs)
            {
                if (sb.SubAddon != null && sb.SubAddon.name == hand.name)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>Replace BOTH items of a side with the desired set. desired == null -> side untouched (never strip unchosen).</summary>
        internal static bool SwapSet(List<GeoItem> items, string handToken, string bodyToken, MatchedSet desired, WeaponDef handOverride)
        {
            if (desired == null)
            {
                return false;
            }
            WeaponDef hand = handOverride ?? desired.Hand;
            bool already = items.Any(i => i?.ItemDef != null && i.ItemDef.Guid == desired.BodyPart.Guid)
                        && (hand == null || items.Any(i => i?.ItemDef != null && i.ItemDef.Guid == hand.Guid));
            if (already)
            {
                return false;
            }
            // CONTRACT: cloned hand weapons MUST keep the side token (e.g. TheTurned_Crabman_RightHand_*)
            // in their def name or this removal will miss them (Task-8 claw clones rely on this).
            items.RemoveAll(i => i?.ItemDef?.name != null &&
                (i.ItemDef.name.IndexOf(handToken, StringComparison.OrdinalIgnoreCase) >= 0
              || (i.ItemDef.name.IndexOf(bodyToken, StringComparison.OrdinalIgnoreCase) >= 0 && !(i.ItemDef is WeaponDef))));
            items.Add(new GeoItem(desired.BodyPart));
            // BUG-B GUARD (mirrors EnforceSetForBodypart): native Crabman elite/base arm bodyparts carry their
            // hand weapon as an auto-attached SubAddon. Adding a SECOND flat GeoItem(hand) conflicts with the
            // SubAddon-attached one and the engine DROPS the whole arm. Only add the flat hand when the bodypart
            // does NOT already declare it as a SubAddon (true for authored clones whose hand is not a SubAddon).
            if (hand != null && !BodypartCarriesHandSubaddon(desired.BodyPart, hand))
            {
                items.Add(new GeoItem(hand));
            }
            return true;
        }


        /// <summary>Yield every ability present in the personal track slots + the learned set. Feeds the
        /// Phase-4 marker lookups (arm SET + head SET + claw markers) and the legacy arm-marker scan.</summary>
        internal static IEnumerable<TacticalAbilityDef> EnumerateLearnedAbilities(PhoenixPoint.Common.Entities.Characters.CharacterProgression prog)
        {
            var personal = prog.PersonalAbilityTrack;
            if (personal?.AbilitiesByLevel != null)
            {
                foreach (var slot in personal.AbilitiesByLevel)
                {
                    if (slot?.Ability != null)
                    {
                        yield return slot.Ability;
                    }
                }
            }
            foreach (var a in prog.Abilities)
            {
                if (a != null)
                {
                    yield return a;
                }
            }
        }

        // --- helpers ----------------------------------------------------------------------------
        private static bool IsRight(string name)
        {
            return name != null && name.Contains(RightHandToken);
        }

        /// <summary>Coarse classification used only for logging + loc-key derivation.</summary>
        private static string Classify(string name)
        {
            if (name == null)
            {
                return "Unknown";
            }
            if (name.IndexOf("Grenade", StringComparison.OrdinalIgnoreCase) >= 0) return "Grenade";
            if (name.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0) return "Shield";
            if (name.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) >= 0) return "Gun";
            return IsRight(name) ? "Claw" : "LeftArm";
        }

        private static PassiveModifierAbilityDef BuildMarkerFor(DefRepository repo, WeaponDef weapon, bool isRight)
        {
            // Deterministic, idempotent GUIDs derived from the arm def name.
            string baseName = weapon.name;
            string abilityName = "TheTurned_Arthron_Arm_" + Sanitize(baseName) + "_AbilityDef";
            string abilityGuid = DeriveGuid(baseName + "|ability");
            string progGuid = DeriveGuid(baseName + "|prog");
            string vedGuid = DeriveGuid(baseName + "|ved");

            // Loc keys: side-generic per-option (e.g. ARTHRON_ARM_<CLASS>_NAME). Multiple defs may share a
            // class (gun/grenade/claw/shield); that's fine — they reuse the same loc string.
            string locClass = Classify(baseName).ToUpperInvariant();
            string nameLocKey = "ARTHRON_ARM_" + locClass + "_NAME";
            string descLocKey = "ARTHRON_ARM_" + locClass + "_DESC";

            // Icon: per-side file (the generated textures exist as Arthron_ArmRight/Arthron_ArmLeft). Loader
            // no-ops if missing (keeps cloned icon). Right arm = claw/gun variants, left arm = shield/grenade.
            string iconFile = isRight ? "Arthron_ArmRight.png" : "Arthron_ArmLeft.png";

            return PerkFactory.BuildMarker(repo, abilityGuid, abilityName, progGuid, vedGuid,
                nameLocKey, descLocKey, iconFile);
        }

        private static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s))
            {
                return "Unknown";
            }
            StringBuilder sb = new StringBuilder(s.Length);
            foreach (char c in s)
            {
                sb.Append(char.IsLetterOrDigit(c) ? c : '_');
            }
            return sb.ToString();
        }

        /// <summary>Deterministic GUID from a seed string (stable across reloads -> idempotent defs).</summary>
        private static string DeriveGuid(string seed)
        {
            using (MD5 md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(Encoding.UTF8.GetBytes("TheTurned.ArthronArm:" + seed));
                return new Guid(hash).ToString();
            }
        }
    }
}
