using Base.Defs;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Geoscape.Entities;
using PhoenixPoint.Modding;
using PhoenixPoint.Tactical.Entities.Abilities;
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

                var newList = new List<GeoItem>(geoChar.ArmourItems);
                bool changed = false;
                changed |= SwapSet(newList, RightHandToken, RightArmToken, wantRight,
                                   (clawOverride != null && wantRight == CrabmanParts.DefaultRight) ? clawOverride : null);
                changed |= SwapSet(newList, LeftHandToken, LeftArmToken, wantLeft, null);
                changed |= SwapSet(newList, "Crabman_Head", "Crabman_Head", wantHead, null);
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
            // Locate the matched SET whose bodypart IS the applied card, and the side tokens to swap on.
            MatchedSet set = null;
            string handToken = null, bodyToken = null;
            if ((set = CrabmanParts.RightArmSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = RightHandToken; bodyToken = RightArmToken;
            }
            else if ((set = CrabmanParts.LeftArmSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = LeftHandToken; bodyToken = LeftArmToken;
            }
            else if ((set = CrabmanParts.HeadSets.FirstOrDefault(s => s.BodyPart != null && s.BodyPart.Guid == appliedBodypart.Guid)) != null)
            {
                handToken = "Crabman_Head"; bodyToken = "Crabman_Head";
            }
            else
            {
                return null; // not one of our variant bodyparts -> leave the native swap alone
            }

            var items = new List<GeoItem>(geoChar.ArmourItems);
            bool changed = SwapSet(items, handToken, bodyToken, set, null);
            if (!changed)
            {
                return null; // bodypart+hand already correct
            }
            Log?.LogInfo($"[TheTurned] augment apply: enforced SET '{set.Token}' "
                + $"(bodypart='{set.BodyPart?.name}' hand='{set.Hand?.name ?? "<none>"}') for '{geoChar.GetName()}'.");
            return items;
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
            if (hand != null)
            {
                items.Add(new GeoItem(hand));
            }
            return true;
        }


        /// <summary>
        /// Remove a whole arm side (hand weapon + its bodypart) from an item list, leaving the slot EMPTY.
        /// Used to strip the recruit's vanilla left-arm Shield so it spawns bare (Gunner/Scourge strains
        /// have an empty left arm natively, so an empty side is a valid no-orphan state). Mirrors the
        /// removal half of <see cref="SwapSet"/> (token match on the hand weapon + the non-weapon bodypart).
        /// Returns true if anything was removed.
        /// </summary>
        internal static bool StripSide(List<GeoItem> items, string handToken, string bodyToken)
        {
            if (items == null)
            {
                return false;
            }
            int before = items.Count;
            items.RemoveAll(i => i?.ItemDef?.name != null &&
                (i.ItemDef.name.IndexOf(handToken, StringComparison.OrdinalIgnoreCase) >= 0
              || (i.ItemDef.name.IndexOf(bodyToken, StringComparison.OrdinalIgnoreCase) >= 0 && !(i.ItemDef is WeaponDef))));
            return items.Count != before;
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
