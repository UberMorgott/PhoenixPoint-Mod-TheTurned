using Base.Defs;
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
    /// Rolled weapon-arm slots for the recruited Arthron (Crabman). Arms are bodypart <c>WeaponDef</c>s
    /// living in <c>GeoCharacter.ArmourItems</c>; right vs left is identified by the def-name substring
    /// (<c>Crabman_RightHand</c> / <c>Crabman_LeftHand</c>) — NOT array index.
    ///
    /// Two personal-track slots are rolled at recruit: a RIGHT arm (claw / gun + elemental gun variants)
    /// and a LEFT arm (shield / grenade + elemental grenade variants). Each option is represented by a
    /// marker <see cref="PassiveModifierAbilityDef"/> placed in the personal track (so it renders and is a
    /// valid PerkOracle swap target). The actual arm is DERIVED from whichever arm-markers are currently in
    /// the unit's ability set via <see cref="ApplyRolledArms"/> (the single source of truth), applied with
    /// <c>GeoCharacter.SetItems</c>. This makes PerkOracle swaps follow automatically (the
    /// <see cref="ArmFollowHook"/> re-derives on <c>OnAbilityAdded</c>).
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
                foreach (TacticalAbilityDef ability in EnumerateArmMarkers(geoChar.Progression))
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

        /// <summary>Yield every arm-marker ability present in the personal track slots + the learned set.</summary>
        private static IEnumerable<TacticalAbilityDef> EnumerateArmMarkers(PhoenixPoint.Common.Entities.Characters.CharacterProgression prog)
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
