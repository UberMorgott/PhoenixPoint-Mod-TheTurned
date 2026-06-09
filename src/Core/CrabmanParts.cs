using Base.Defs;
using PhoenixPoint.Tactical.Entities.Equipments;
using PhoenixPoint.Tactical.Entities.Weapons;
using System;
using System.Collections.Generic;
using System.Linq;

namespace TheTurned.Core
{
    /// <summary>A matched bodypart+hand pair. C3: ALWAYS swapped together (the 22k addon-error lesson).</summary>
    internal sealed class MatchedSet
    {
        public TacticalItemDef BodyPart;   // arm or head bodypart item
        public WeaponDef Hand;             // hand/head weapon, may be null for bodypart-only head variants
        public bool IsRight;
        public string Token;               // variant token, e.g. "Gun", "Grenade", "Pincer", "Shield", "Spitter"
    }

    /// <summary>Runtime enumeration + token pairing of Crabman_* defs (bundle-only names — never hardcode).</summary>
    internal static class CrabmanParts
    {
        private static bool _built;
        private static bool _logged;
        private static bool _attempted;
        internal static readonly List<MatchedSet> RightArmSets = new List<MatchedSet>();
        internal static readonly List<MatchedSet> LeftArmSets = new List<MatchedSet>();
        internal static readonly List<MatchedSet> HeadSets = new List<MatchedSet>();
        internal static MatchedSet DefaultRight;   // pincer/claw set
        internal static MatchedSet DefaultLeft;    // shield set
        internal static MatchedSet DefaultHead;    // normal carapace head

        internal static bool HasSets => RightArmSets.Count > 0 || LeftArmSets.Count > 0;

        internal static void Build(DefRepository repo)
        {
            if (_built || repo == null)
            {
                return;
            }
            var hands = repo.GetAllDefs<WeaponDef>()
                .Where(d => d?.name != null)
                .Where(d => d.name.IndexOf("Crabman_RightHand", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_LeftHand", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_Head", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();
            var bodyparts = repo.GetAllDefs<TacticalItemDef>()
                .Where(d => d?.name != null && !(d is WeaponDef))
                .Where(d => d.name.IndexOf("Crabman_RightArm", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_LeftArm", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_Head", StringComparison.OrdinalIgnoreCase) >= 0
                         || d.name.IndexOf("Crabman_Torso", StringComparison.OrdinalIgnoreCase) >= 0)
                .OrderBy(d => d.name, StringComparer.Ordinal).ToList();

            if (!_logged)
            {
                _logged = true;
                TheTurnedMain.LogInfo($"[TheTurned] Crabman hand/head WeaponDefs ({hands.Count}):");
                foreach (var d in hands) TheTurnedMain.LogInfo($"  weapon '{d.name}' guid='{d.Guid}'");
                TheTurnedMain.LogInfo($"[TheTurned] Crabman bodypart TacticalItemDefs ({bodyparts.Count}):");
                foreach (var d in bodyparts) TheTurnedMain.LogInfo($"  bodypart '{d.name}' guid='{d.Guid}'");
            }

            PairSide(hands, bodyparts, "Crabman_RightHand", "Crabman_RightArm", RightArmSets, isRight: true);
            PairSide(hands, bodyparts, "Crabman_LeftHand", "Crabman_LeftArm", LeftArmSets, isRight: false);
            PairHeads(hands, bodyparts);
            PickDefaults();
            bool firstAttempt = !_attempted;
            _attempted = true;
            _built = HasSets;
            // Summary only on the successful (HasSets) attempt or the very first one — the failed-retry
            // path (Build re-called on every geoscape return until defs appear) must not spam the log.
            if (_built || firstAttempt)
            {
                TheTurnedMain.LogInfo($"[TheTurned] CrabmanParts: right={RightArmSets.Count} left={LeftArmSets.Count} head={HeadSets.Count} " +
                    $"defaults R='{DefaultRight?.BodyPart?.name}/{DefaultRight?.Hand?.name}' L='{DefaultLeft?.BodyPart?.name}/{DefaultLeft?.Hand?.name}' H='{DefaultHead?.BodyPart?.name}'");
            }
        }

        private static void PairSide(List<WeaponDef> hands, List<TacticalItemDef> bodyparts,
            string handPrefix, string armPrefix, List<MatchedSet> into, bool isRight)
        {
            into.Clear();
            var candidates = bodyparts
                .Where(b => b.name.StartsWith(armPrefix, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var hand in hands.Where(h => h.name.StartsWith(handPrefix, StringComparison.OrdinalIgnoreCase)))
            {
                string token = VariantToken(hand.name, handPrefix);
                // Pass 1: exact token match; pass 2: substring fallback ONLY if no exact hit
                // (prevents 'Viral_Gun' hand pairing to the plain 'Gun' arm when an exact arm exists).
                var arm = candidates.FirstOrDefault(b => TokenExact(VariantToken(b.name, armPrefix), token))
                       ?? candidates.FirstOrDefault(b => TokenFuzzy(VariantToken(b.name, armPrefix), token));
                if (arm == null)
                {
                    TheTurnedMain.LogWarn($"[TheTurned] no matched {armPrefix} bodypart for hand '{hand.name}' (token '{token}') — set skipped (C3)");
                    continue;
                }
                into.Add(new MatchedSet { BodyPart = arm, Hand = hand, IsRight = isRight, Token = token });
            }
            // Bodypart-only sets (mirror PairHeads): arm bodyparts NO hand weapon paired to. Runtime
            // dump shows the LEFT side has 4 grenade hand weapons ONLY — the shield is a pure bodypart
            // (Crabman_LeftArm_Shield/EliteShield_BodyPartDef, no shield WeaponDef), so hand-driven
            // pairing alone can never produce the Shield set. Reference-guard prevents duplicating a
            // bodypart already covered by a hand-paired set (incl. fuzzy-matched ones).
            foreach (var bp in candidates)
            {
                if (into.Any(s => s.BodyPart == bp))
                {
                    continue;
                }
                into.Add(new MatchedSet { BodyPart = bp, Hand = null, IsRight = isRight, Token = VariantToken(bp.name, armPrefix) });
            }
        }

        private static void PairHeads(List<WeaponDef> hands, List<TacticalItemDef> bodyparts)
        {
            HeadSets.Clear();
            var headWeapons = hands
                .Where(h => h.name.StartsWith("Crabman_Head", StringComparison.OrdinalIgnoreCase)).ToList();
            var headBodyparts = bodyparts
                .Where(b => b.name.StartsWith("Crabman_Head", StringComparison.OrdinalIgnoreCase)).ToList();
            var pairedWeapons = new HashSet<WeaponDef>();
            foreach (var bp in headBodyparts)
            {
                string token = VariantToken(bp.name, "Crabman_Head");
                // Same two-pass policy as PairSide: exact first, substring fallback only when no exact hit.
                var weapon = headWeapons.FirstOrDefault(h => TokenExact(VariantToken(h.name, "Crabman_Head"), token))
                          ?? headWeapons.FirstOrDefault(h => TokenFuzzy(VariantToken(h.name, "Crabman_Head"), token));
                if (weapon != null)
                {
                    pairedWeapons.Add(weapon);
                }
                HeadSets.Add(new MatchedSet { BodyPart = bp, Hand = weapon, IsRight = false, Token = token });
            }
            // Head WEAPONS with no token-matched bodypart. Runtime dump: head weapons are
            // Crabman_Head_Spitter/EliteSpitter_WeaponDef, head bodyparts are ONLY
            // Crabman_Head_Humanoid/EliteHumanoid_BodyPartDef (TFTV strings.json:4311-4312 confirms
            // no other head bodyparts) — "Spitter" vs "Humanoid" fails exact AND fuzzy, so the
            // spitter SETs were never created (head row stayed deferred). Grounding: a head weapon
            // attaches into a head bodypart's addon slots (C3 matched-SET rule), and Humanoid/Elite-
            // Humanoid are the only carriers → pair by Elite-ness (Elite weapon ↔ Elite bodypart).
            // Non-Elite weapons first so ArthronHeadPerks.FindSpitterSet (first Hand containing
            // "Spitter") resolves the BASIC spitter, consistent with the non-Elite defaults policy.
            foreach (var weapon in headWeapons
                .Where(w => !pairedWeapons.Contains(w))
                .OrderBy(w => VariantToken(w.name, "Crabman_Head").IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 0))
            {
                string token = VariantToken(weapon.name, "Crabman_Head");
                bool elite = token.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0;
                var bp = headBodyparts.FirstOrDefault(b =>
                        (VariantToken(b.name, "Crabman_Head").IndexOf("Elite", StringComparison.OrdinalIgnoreCase) >= 0) == elite)
                      ?? headBodyparts.FirstOrDefault();
                if (bp == null)
                {
                    TheTurnedMain.LogWarn($"[TheTurned] no head bodypart for head weapon '{weapon.name}' — set skipped (C3)");
                    continue;
                }
                HeadSets.Add(new MatchedSet { BodyPart = bp, Hand = weapon, IsRight = false, Token = token });
            }
        }

        // "Crabman_RightHand_Viral_Gun_WeaponDef" + prefix "Crabman_RightHand" -> "Viral_Gun";
        // variant-less "Crabman_Head_BodyPartDef" -> "" (suffix stripped from the RAW remainder FIRST,
        // then '_' trimmed — trimming first would leave token "BodyPartDef").
        internal static string VariantToken(string defName, string prefix)
        {
            string s = defName.Substring(prefix.Length);
            foreach (var suffix in new[] { "_WeaponDef", "_BodyPartDef", "_ItemDef", "_TacticalItemDef" })
                if (s.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) { s = s.Substring(0, s.Length - suffix.Length); break; }
            return s.Trim('_');
        }

        internal static bool TokenExact(string a, string b)
            => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

        // Substring fallback. Empty tokens NEVER fuzzy-match (empty matches only exact-empty via
        // TokenExact) — "".IndexOf / IndexOf("") == 0 would otherwise match everything.
        internal static bool TokenFuzzy(string a, string b)
            => !string.IsNullOrEmpty(a) && !string.IsNullOrEmpty(b)
            && (a.IndexOf(b, StringComparison.OrdinalIgnoreCase) >= 0
             || b.IndexOf(a, StringComparison.OrdinalIgnoreCase) >= 0);

        /// <summary>Sets whose token does NOT carry "Elite" — ordinal sort otherwise puts Elite
        /// variants first (e.g. EliteGrenade &lt; Grenade), skewing every FirstOrDefault default.</summary>
        private static IEnumerable<MatchedSet> NonElite(IEnumerable<MatchedSet> sets)
            => sets.Where(s => string.IsNullOrEmpty(s.Token)
                            || s.Token.IndexOf("Elite", StringComparison.OrdinalIgnoreCase) < 0);

        private static void PickDefaults()
        {
            // Each default prefers the non-Elite variant first, then falls back to the full list.
            DefaultRight = NonElite(RightArmSets).FirstOrDefault(s => s.Token.IndexOf("Pincer", StringComparison.OrdinalIgnoreCase) >= 0
                                                                   || s.Token.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? NonElite(RightArmSets).FirstOrDefault(s => s.Token.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) < 0)
                        ?? RightArmSets.FirstOrDefault(s => s.Token.IndexOf("Pincer", StringComparison.OrdinalIgnoreCase) >= 0
                                                         || s.Token.IndexOf("Claw", StringComparison.OrdinalIgnoreCase) >= 0)
                        ?? RightArmSets.FirstOrDefault(s => s.Token.IndexOf("Gun", StringComparison.OrdinalIgnoreCase) < 0)
                        ?? RightArmSets.FirstOrDefault();
            DefaultLeft = NonElite(LeftArmSets).FirstOrDefault(s => s.Token.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? LeftArmSets.FirstOrDefault(s => s.Token.IndexOf("Shield", StringComparison.OrdinalIgnoreCase) >= 0)
                       ?? LeftArmSets.FirstOrDefault();
            // Default head = the bare non-Elite head (bodypart-only set, Hand == null): the only head
            // bodyparts are Humanoid/EliteHumanoid; the spitter sets carry a head WEAPON and are
            // popup-row choices, not the recruit default.
            DefaultHead = NonElite(HeadSets).FirstOrDefault(s => s.Hand == null)
                       ?? HeadSets.FirstOrDefault(s => s.Hand == null)
                       ?? HeadSets.FirstOrDefault();
            if (DefaultRight == null || DefaultLeft == null || DefaultHead == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] CrabmanParts default(s) missing: "
                    + $"R={(DefaultRight != null)} L={(DefaultLeft != null)} H={(DefaultHead != null)}");
            }
        }
    }
}
