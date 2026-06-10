using Base.Defs;
using PhoenixPoint.Common.Entities.Addons;
using PhoenixPoint.Common.Entities.Items;
using PhoenixPoint.Common.UI;
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
            // V1 augment screen base-tier head cards: the native head SETs only expose the Humanoid
            // bodypart (base + spitter SHARE Crabman_Head_Humanoid_BodyPartDef), so a bodypart-keyed card
            // grid cannot show Spitter and an armored head as DISTINCT cards. Author two clone bodyparts
            // (distinct GUIDs) so each becomes its own card and the apply path (EnforceSetForBodypart,
            // keyed by bodypart GUID) resolves the correct matched hand / armor. Additive: appended AFTER
            // PickDefaults so the recruit DEFAULT head stays the real base Humanoid.
            BuildAuthoredHeadVariants(repo);
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


        // ---- V1 augment screen: base-tier card pool + authored head variants ----------------------

        private static bool _authoredHeadsBuilt;

        /// <summary>
        /// The base-tier card pool for the augment screen: each slot's NON-Elite SETs (Elite/Ultra
        /// evolution variants are deferred to the separate perk system). Right = Pincer/Gun/Viral_Gun,
        /// Left = Shield/Grenade/Acid_Grenade, Head = Humanoid(base) + Spitter(authored) + Armored(authored).
        /// </summary>
        internal static IEnumerable<MatchedSet> BaseTier(IEnumerable<MatchedSet> sets) => NonElite(sets);

        /// <summary>
        /// Author two extra HEAD bodypart cards as clones of the base Humanoid head (distinct GUIDs so each
        /// becomes its own bodypart-keyed card + apply target):
        ///   - Spitter: clone bodypart paired with the native Crabman_Head_Spitter_WeaponDef (acid/poison
        ///     spit). The native base + spitter sets SHARE Crabman_Head_Humanoid_BodyPartDef, which the
        ///     bodypart-keyed card grid cannot disambiguate — the clone gives Spitter its own card.
        ///   - Armored: clone bodypart with +Armor, no hand (a tankier base head; perks add more later).
        /// Idempotent (deterministic MD5 GUIDs + GetDef guard). Appended to HeadSets AFTER PickDefaults so
        /// the recruit default head stays the real base Humanoid.
        /// </summary>
        private static void BuildAuthoredHeadVariants(DefRepository repo)
        {
            if (_authoredHeadsBuilt || repo == null)
            {
                return;
            }
            // AUGMENT-SCREEN PRINCIPLE: every card chooses a limb MODEL only. ALL augment parts run on BASE
            // (ordinary) stats — no stat advantage. Where we use an EVOLVED (Elite) MODEL for a distinct look,
            // we NORMALIZE its numeric stats down to the corresponding base part (looks evolved, hits base).
            // Stat ranks + true ordinary->evolved upgrades are a SEPARATE future perk system, not this screen.
            //
            // The head CARDS are authored clones (each owns a private VED so RebindNames never mutates a real
            // enemy head VED). The recruit SPAWNS the real base Humanoid head; the head menu offers only swaps:
            //   1. SPITTER       = clone of the base Humanoid head + the ordinary Spitter weapon (poison spit;
            //                      same skull). Base stats already (clone of base).
            //   2. ARMORED       = clone of the real EliteHumanoid head (its OWN distinct armored carapace
            //                      SkinData, runtimeKey a9a243ad… ≠ base 122e5b8b…), stats NORMALIZED to base
            //                      Humanoid (Armor/HP). No spit.
            //   3. EVOLVED-SPITTER = clone of the EliteHumanoid head (armored skull MODEL) + a clone of the
            //                      EliteSpitter weapon (evolved spit-organ MODEL) whose stats are normalized
            //                      to the ordinary Spitter weapon. Distinct look = armored skull + evolved organ.
            // (Umbra head INFEASIBLE — separate investigation.) CRITICAL headless-safe rule: clones add NO
            // CustomizationColorTagDef / AlwaysCustomizeColor / mutually-exclusive GameTag (the prior headless
            // crash). Clone = name + own VED + SkinData inherited from the Elite model + base-normalized stats.
            TacticalItemDef baseHumanoid = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Head_Humanoid_BodyPartDef")
                ?? DefaultHead?.BodyPart
                ?? NonElite(HeadSets).FirstOrDefault(s => s.Hand == null)?.BodyPart;
            if (baseHumanoid == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: base Humanoid head bodypart unresolved — skipped");
                return;
            }
            TacticalItemDef eliteHumanoid = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Head_EliteHumanoid_BodyPartDef");
            // Ordinary (non-Elite) spitter head weapon (poison spit).
            WeaponDef spitterWeapon = NonElite(HeadSets)
                .Where(s => s.Hand != null && s.Hand.name.IndexOf("Spitter", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(s => s.Hand)
                .FirstOrDefault();
            WeaponDef eliteSpitterWeapon = DefUtils.ResolveByName<WeaponDef>(repo, "Crabman_Head_EliteSpitter_WeaponDef");

            // -- SPITTER card (clone base Humanoid + ordinary spitter weapon) -----------------------
            if (spitterWeapon != null)
            {
                TacticalItemDef spitterBody = CloneHeadBodypart(repo,
                    baseHumanoid, baseHumanoid, "head:authored:Spitter", "TheTurned_Crabman_Head_Spitter_BodyPartDef");
                if (spitterBody != null)
                {
                    HeadSets.Add(new MatchedSet { BodyPart = spitterBody, Hand = spitterWeapon, IsRight = false, Token = "Spitter" });
                }
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: ordinary spitter head weapon unresolved — Spitter card skipped");
            }

            // -- ARMORED card (clone EliteHumanoid MODEL, stats normalized to base Humanoid) ---------
            TacticalItemDef armoredSource = eliteHumanoid ?? baseHumanoid;
            if (eliteHumanoid == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: 'Crabman_Head_EliteHumanoid_BodyPartDef' not found — "
                    + "Armored card falls back to a Humanoid clone (will LOOK like base; appearance = SkinData).");
            }
            TacticalItemDef armoredBody = CloneHeadBodypart(repo,
                armoredSource, baseHumanoid, "head:authored:Armored", "TheTurned_Crabman_Head_Armored_BodyPartDef");
            if (armoredBody != null)
            {
                HeadSets.Add(new MatchedSet { BodyPart = armoredBody, Hand = null, IsRight = false, Token = "Armored" });
            }

            // -- EVOLVED-SPITTER card (EliteHumanoid skull MODEL + normalized EliteSpitter organ MODEL) --
            if (eliteHumanoid != null && eliteSpitterWeapon != null && spitterWeapon != null)
            {
                TacticalItemDef evolvedSpitterBody = CloneHeadBodypart(repo,
                    eliteHumanoid, baseHumanoid, "head:authored:EvolvedSpitter",
                    "TheTurned_Crabman_Head_Evolved_Spitter_BodyPartDef");
                WeaponDef normalizedEliteSpit = WeaponVariants.GetOrCreateNormalizedWeapon(repo,
                    eliteSpitterWeapon, spitterWeapon, "head:authored:EvolvedSpitter|weapon",
                    "TheTurned_Crabman_Head_Evolved_Spitter_WeaponDef");
                if (evolvedSpitterBody != null && normalizedEliteSpit != null)
                {
                    HeadSets.Add(new MatchedSet { BodyPart = evolvedSpitterBody, Hand = normalizedEliteSpit, IsRight = false, Token = "Evolved_Spitter" });
                }
                else
                {
                    TheTurnedMain.LogWarn("[TheTurned] authored heads: Evolved-Spitter clone/weapon unavailable — card skipped");
                }
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: EliteHumanoid or EliteSpitter weapon unresolved — Evolved-Spitter card skipped");
            }

            BuildAuthoredArmVariants(repo);

            _authoredHeadsBuilt = true;
        }

        /// <summary>
        /// Author the EVOLVED-CLAW right-arm card: a clone of the ElitePincer arm bodypart (its distinct
        /// evolved-claw MODEL = SkinData, runtimeKey 269bc82d… ≠ base 3f099b87…) whose stats are NORMALIZED
        /// to the base Pincer arm, paired with a clone of the ElitePincer HAND weapon whose damage/stats are
        /// normalized to the base Pincer hand. The clone arm's SubAddons are REWRITTEN to attach the
        /// normalized hand clone (preserving the original attachment point), so the engine auto-attaches the
        /// evolved-claw hand-with-base-damage exactly like a native arm (BodypartCarriesHandSubaddon -> the
        /// augment apply path treats it as native+SubAddon, no flat-hand conflict). LOOKS evolved, hits base.
        /// Token "Evolved_Claw" (no "Pincer" substring) so the AllVariantBodyparts right filter keeps it as a
        /// card while still excluding the base Pincer (recruit default). Idempotent (MD5-GUID guards).
        /// </summary>
        private static void BuildAuthoredArmVariants(DefRepository repo)
        {
            TacticalItemDef baseArm = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_RightArm_Pincer_BodyPartDef");
            TacticalItemDef eliteArm = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_RightArm_ElitePincer_BodyPartDef");
            WeaponDef baseHand = DefUtils.ResolveByName<WeaponDef>(repo, "Crabman_RightHand_Pincer_WeaponDef");
            WeaponDef eliteHand = DefUtils.ResolveByName<WeaponDef>(repo, "Crabman_RightHand_ElitePincer_WeaponDef");
            if (eliteArm == null || baseArm == null || eliteHand == null || baseHand == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] authored arm: Evolved-Claw defs unresolved "
                    + $"(eliteArm={eliteArm != null} baseArm={baseArm != null} eliteHand={eliteHand != null} baseHand={baseHand != null}) — card skipped");
                return;
            }
            // Normalized evolved-claw HAND (evolved mesh, base damage). Name keeps the RightHand side token
            // (ArthronArms.SwapSet removal contract).
            WeaponDef normalizedHand = WeaponVariants.GetOrCreateNormalizedWeapon(repo,
                eliteHand, baseHand, "arm:authored:EvolvedClaw|hand",
                "TheTurned_Crabman_RightHand_Evolved_Claw_WeaponDef");
            if (normalizedHand == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored arm: Evolved-Claw hand clone failed — card skipped");
                return;
            }
            // Normalized evolved-claw ARM bodypart (evolved skull/arm SkinData, base Armor/HP/aspect).
            TacticalItemDef armBody = CloneArmBodypart(repo, eliteArm, baseArm, normalizedHand,
                "arm:authored:EvolvedClaw", "TheTurned_Crabman_RightArm_Evolved_Claw_BodyPartDef");
            if (armBody == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored arm: Evolved-Claw arm clone failed — card skipped");
                return;
            }
            // Reference-guard: don't double-add across idempotent re-runs.
            if (!RightArmSets.Any(s => s.BodyPart != null && s.BodyPart.Guid == armBody.Guid))
            {
                RightArmSets.Add(new MatchedSet { BodyPart = armBody, Hand = normalizedHand, IsRight = true, Token = "Evolved_Claw" });
            }
        }

        /// <summary>Idempotent clone of a head bodypart def: keep the model source's SkinData, normalize stats to base.</summary>
        private static TacticalItemDef CloneHeadBodypart(DefRepository repo, TacticalItemDef modelSource,
            TacticalItemDef baseStats, string guidSeed, string cloneName)
        {
            string guid = Phase4.DeriveGuid(guidSeed).ToString();
            if (repo.GetDef(guid) is TacticalItemDef existing)
            {
                return existing;
            }
            TacticalItemDef clone = repo.CreateDef<TacticalItemDef>(guid, modelSource);
            if (clone == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] authored heads: clone failed for '{cloneName}'");
                return null;
            }
            clone.name = cloneName;
            // SkinData (the 3D model/prefab) is intentionally NOT touched — CreateDef copies the model
            // source's SkinData by reference, so an EliteHumanoid clone keeps EliteHumanoid's armored prefab
            // (runtimeKey a9a243ad…, distinct from base Humanoid 122e5b8b…) and renders armored.
            // Stats NORMALIZED to base (augment principle: evolved look, ordinary mechanics).
            NormalizeBodypartStats(clone, baseStats);
            GiveOwnVed(repo, clone, modelSource, guidSeed, cloneName);
            return clone;
        }

        /// <summary>Copy the ordinary stat fields FROM <paramref name="baseStats"/> onto <paramref name="clone"/>
        /// (the evolved-MODEL clone), so it is mechanically identical to base while keeping the evolved SkinData.
        /// Fields per crabman-bodypart-catalog §0: ItemDef.Armor/HitPoints/Weight + TacticalItemDef.BodyPartAspectDef.</summary>
        private static void NormalizeBodypartStats(TacticalItemDef clone, TacticalItemDef baseStats)
        {
            if (clone == null || baseStats == null)
            {
                return;
            }
            clone.Armor = baseStats.Armor;
            clone.HitPoints = baseStats.HitPoints;
            clone.Weight = baseStats.Weight;
            clone.BodyPartAspectDef = baseStats.BodyPartAspectDef;
        }

        /// <summary>Give <paramref name="clone"/> its OWN cloned ViewElementDef (Unity's CreateDef copies the
        /// VED as a shared REFERENCE, so every clone + the real enemy part would otherwise share one VED and
        /// RebindNames' last-write would win for all + corrupt the live enemy VED). Headless-safe: only the
        /// VED name is set — no color tag / customization flag is touched.</summary>
        private static void GiveOwnVed(DefRepository repo, ItemDef clone, ItemDef modelSource, string guidSeed, string cloneName)
        {
            ViewElementDef srcVed = modelSource?.ViewElementDef;
            if (srcVed == null)
            {
                return;
            }
            string vedGuid = Phase4.DeriveGuid(guidSeed + "|ved").ToString();
            ViewElementDef newVed = repo.GetDef(vedGuid) as ViewElementDef
                ?? repo.CreateDef(vedGuid, srcVed) as ViewElementDef;
            if (newVed != null)
            {
                newVed.name = "E_ViewElement [" + cloneName + "]";
                clone.ViewElementDef = newVed;
            }
            else
            {
                TheTurnedMain.LogWarn($"[TheTurned] authored clone: VED clone failed for '{cloneName}' — "
                    + "card may share the source part's name.");
            }
        }

        /// <summary>Idempotent clone of an arm bodypart: keep the evolved-model SkinData, normalize stats to
        /// <paramref name="baseStats"/>, give it its own VED, and REWRITE its SubAddons so the matched hand
        /// auto-attaches the supplied <paramref name="hand"/> clone (preserving the original attachment point).
        /// Headless-safe: no color/customization tag added.</summary>
        private static TacticalItemDef CloneArmBodypart(DefRepository repo, TacticalItemDef modelSource,
            TacticalItemDef baseStats, WeaponDef hand, string guidSeed, string cloneName)
        {
            string guid = Phase4.DeriveGuid(guidSeed).ToString();
            if (repo.GetDef(guid) is TacticalItemDef existing)
            {
                return existing;
            }
            TacticalItemDef clone = repo.CreateDef<TacticalItemDef>(guid, modelSource);
            if (clone == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] authored arm: clone failed for '{cloneName}'");
                return null;
            }
            clone.name = cloneName;
            NormalizeBodypartStats(clone, baseStats);
            GiveOwnVed(repo, clone, modelSource, guidSeed, cloneName);
            // Rewrite the SubAddon hand -> the normalized hand clone (keep attachment point of the first
            // hand SubAddon). The native ElitePincer arm carries its Elite hand as a SubAddon; pointing it at
            // our normalized clone makes the engine auto-attach the evolved-claw mesh with base damage, and
            // makes ArthronArms.BodypartCarriesHandSubaddon true (apply path = native+SubAddon, no flat-hand
            // conflict). If the source had no SubAddons, add one.
            if (hand != null)
            {
                AddonDef.SubaddonBind[] subs = clone.SubAddons;
                string attach = (subs != null && subs.Length > 0) ? subs[0].AttachmentPointName : null;
                clone.SubAddons = new[]
                {
                    new AddonDef.SubaddonBind { SubAddon = hand, AttachmentPointName = attach }
                };
            }
            return clone;
        }
    }
}
