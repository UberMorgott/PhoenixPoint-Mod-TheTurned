using Base.Defs;
using PhoenixPoint.Common.Entities.GameTags;
using PhoenixPoint.Common.Entities.GameTagsTypes;
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
            BuildAuthoredArmVariants(repo);
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
            // The head CARDS are authored clones (each owns a private VED so RebindNames never mutates a real
            // enemy head VED). Per the augment principle "the recruit SPAWNS the base part, the menu shows only
            // SWAP options", the BASE head is NOT a card — the recruit spawns the real Humanoid head and the
            // head menu offers only:
            //   1. SPITTER = clone of the Humanoid head + the native Spitter weapon (poison spit; same skull).
            //   2. ARMORED = clone of the real ELITEHUMANOID head (its OWN distinct armored SkinData — the
            //      real carapace model TFTV assigns to the UltraShielder), NOT a Humanoid+Armor clone (that
            //      reuses the base SkinData and renders identical — appearance = SkinData, Armor is a stat).
            // (Umbra head is INFEASIBLE — Umbra is a single-torso blob on its own rig with no head bodypart
            // that binds the Crabman head slot; a possible tinted clone is under separate investigation.)
            // The card pool filter (AugmentVariants.AllVariantBodyparts) keeps only "TheTurned_Crabman_Head_*"
            // for the head slot. Real defs resolved by exact name (bundle GUIDs unknown); the recruit still
            // equips the REAL chassis Humanoid head via ApplyNakedBase — unaffected by these card clones.
            TacticalItemDef baseHumanoid = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Head_Humanoid_BodyPartDef")
                ?? DefaultHead?.BodyPart
                ?? NonElite(HeadSets).FirstOrDefault(s => s.Hand == null)?.BodyPart;
            if (baseHumanoid == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: base Humanoid head bodypart unresolved — skipped");
                return;
            }
            TacticalItemDef eliteHumanoid = DefUtils.ResolveByName<TacticalItemDef>(repo, "Crabman_Head_EliteHumanoid_BodyPartDef");
            // Native non-Elite spitter head weapon (poison spit).
            WeaponDef spitterWeapon = NonElite(HeadSets)
                .Where(s => s.Hand != null && s.Hand.name.IndexOf("Spitter", StringComparison.OrdinalIgnoreCase) >= 0)
                .Select(s => s.Hand)
                .FirstOrDefault();

            // NOTE: no BASE head card — the recruit spawns the real Humanoid head; the menu shows only swaps.

            // -- SPITTER card (clone Humanoid + spitter weapon) -------------------------------------
            if (spitterWeapon != null)
            {
                TacticalItemDef spitterBody = CloneHeadBodypart(repo,
                    baseHumanoid, "head:authored:Spitter", "TheTurned_Crabman_Head_Spitter_BodyPartDef", armorBonus: 0f);
                if (spitterBody != null)
                {
                    HeadSets.Add(new MatchedSet { BodyPart = spitterBody, Hand = spitterWeapon, IsRight = false, Token = "Spitter" });
                }
            }
            else
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: native spitter head weapon unresolved — Spitter card skipped");
            }

            // -- ARMORED card (clone of the REAL EliteHumanoid head -> distinct armored model) -------
            TacticalItemDef armoredSource = eliteHumanoid ?? baseHumanoid;
            if (eliteHumanoid == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored heads: 'Crabman_Head_EliteHumanoid_BodyPartDef' not found — "
                    + "Armored card falls back to a Humanoid clone (will LOOK like base; appearance = SkinData).");
            }
            TacticalItemDef armoredBody = CloneHeadBodypart(repo,
                armoredSource, "head:authored:Armored", "TheTurned_Crabman_Head_Armored_BodyPartDef", armorBonus: ArmoredHeadArmorBonus);
            if (armoredBody != null)
            {
                HeadSets.Add(new MatchedSet { BodyPart = armoredBody, Hand = null, IsRight = false, Token = "Armored" });
            }

            // -- UMBRA head card — EliteHumanoid clone (armored model), renders NORMALLY (tint BACKED OUT).
            // The blue mesh tint via CustomizationSecondaryColorTagDef_2 is a DEAD END: the engine's addon
            // rebuild (AddonsCharacterBuilder.RebuildCharacter -> Addon.MergeTagsWithManager) throws
            // "InvalidOperationException: tag CustomizationSecondaryColorTagDef_0 mutually exclusive with
            // CustomizationSecondaryColorTagDef_2", aborting RebuildCharacter -> headless recruit + blank
            // Umbra cards [Player.log 2026-06-10]. So do NOT add the tag here; the Umbra head renders the
            // plain armored model (like Armored). Blue will be re-applied next round via a non-tag method
            // (per-renderer MaterialPropertyBlock / whole-actor color FX) once the shader param is confirmed.
            TacticalItemDef umbraHead = CloneHeadBodypart(repo,
                armoredSource, "head:authored:Umbra", "TheTurned_Crabman_Head_Umbra_BodyPartDef", armorBonus: 0f);
            if (umbraHead != null)
            {
                HeadSets.Add(new MatchedSet { BodyPart = umbraHead, Hand = null, IsRight = false, Token = "Umbra" });
            }

            _authoredHeadsBuilt = true;
        }


        private static bool _authoredArmsBuilt;

        /// <summary>
        /// Author the UMBRA CLAW right-arm card (PROBE): a clone of the base Pincer arm bodypart (keeps its
        /// SubAddon Pincer hand → the matched hand auto-attaches, BUG-B-safe) with a BLUE mesh tint + own VED.
        /// Added to RightArmSets with the Pincer hand so EnforceSetForBodypart resolves it; the right card
        /// filter (token != "Pincer") admits the "Umbra" token → right cards = {MG, Viral, Umbra}. The base
        /// Pincer + recruit default are untouched.
        /// </summary>
        private static void BuildAuthoredArmVariants(DefRepository repo)
        {
            if (_authoredArmsBuilt || repo == null)
            {
                return;
            }
            // Base Pincer set (bodypart + its hand weapon) = the right default.
            MatchedSet pincer = DefaultRight
                ?? NonElite(RightArmSets).FirstOrDefault(s => s.Token != null
                    && s.Token.IndexOf("Pincer", StringComparison.OrdinalIgnoreCase) >= 0);
            if (pincer?.BodyPart == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] authored arms: base Pincer arm unresolved — Umbra claw skipped");
                return;
            }
            TacticalItemDef umbraArm = CloneHeadBodypart(repo,
                pincer.BodyPart, "arm:authored:Umbra", "TheTurned_Crabman_RightArm_Umbra_BodyPartDef", armorBonus: 0f);
            if (umbraArm != null)
            {
                // Tint BACKED OUT (same mutually-exclusive-tag crash as the Umbra head) — renders the plain
                // Pincer model for now; blue re-applied next round via a non-tag method.
                // Hand = the Pincer weapon; the clone keeps Pincer's SubAddon so EnforceSetForBodypart skips
                // the flat-hand add (BodypartCarriesHandSubaddon == true) and the native+SubAddon path attaches it.
                RightArmSets.Add(new MatchedSet { BodyPart = umbraArm, Hand = pincer.Hand, IsRight = true, Token = "Umbra" });
            }
            _authoredArmsBuilt = true;
        }

        /// <summary>Extra armor added to the authored armored head bodypart (vanilla base head armor = 10).</summary>
        private const float ArmoredHeadArmorBonus = 10f;

        /// <summary>Idempotent clone of a head bodypart def with an optional armor bonus (ItemDef.Armor [G]).</summary>
        // Generic bodypart clone (head OR arm): deep-clones the source TacticalItemDef (keeps SkinData +
        // SubAddons by reference), renames it, optional Armor bump, and gives it its OWN cloned VED.
        private static TacticalItemDef CloneHeadBodypart(DefRepository repo, TacticalItemDef source,
            string guidSeed, string cloneName, float armorBonus)
        {
            string guid = Phase4.DeriveGuid(guidSeed).ToString();
            if (repo.GetDef(guid) is TacticalItemDef existing)
            {
                return existing;
            }
            TacticalItemDef clone = repo.CreateDef<TacticalItemDef>(guid, source);
            if (clone == null)
            {
                TheTurnedMain.LogWarn($"[TheTurned] authored heads: clone failed for '{cloneName}'");
                return null;
            }
            clone.name = cloneName;
            // SkinData (the 3D model/prefab) is intentionally NOT touched here — CreateDef copies the
            // source's SkinData by reference, so an EliteHumanoid clone keeps EliteHumanoid's armored prefab
            // (runtimeKey a9a243ad…, confirmed distinct from base Humanoid 122e5b8b…) and renders armored.
            if (armorBonus > 0f)
            {
                clone.Armor = source.Armor + armorBonus;
            }
            // CRITICAL: give the clone its OWN ViewElementDef. Unity's CreateDef deep-copies the ItemDef but
            // copies the ViewElementDef as a shared REFERENCE — so the base Humanoid head + both authored
            // clones would share ONE VED, and AugmentVariants.RebindNames (called per card) would have the
            // LAST write win for all three (verified: all heads showed "Armored Head"), and would corrupt
            // the real enemy Humanoid head's VED. Clone the VED (non-generic CreateDef preserves its runtime
            // type) so each authored head owns its name/description independently.
            ViewElementDef srcVed = source.ViewElementDef;
            if (srcVed != null)
            {
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
                    TheTurnedMain.LogWarn($"[TheTurned] authored heads: VED clone failed for '{cloneName}' — "
                        + "card may share the base head's name.");
                }
            }
            return clone;
        }


        /// <summary>DEFERRED / NOT CALLED (kept for reference). The CustomizationColorTag approach is a DEAD
        /// END: adding CustomizationSecondaryColorTagDef_2 makes AddonsCharacterBuilder.RebuildCharacter throw
        /// "tag _2 mutually exclusive with _0" and abort, blanking the model. Re-apply blue next round via a
        /// non-tag method (per-renderer MaterialPropertyBlock / whole-actor color FX).
        /// Blue-mesh-tint PROBE: enable per-item color customization on a clone and add the vanilla
        /// BLUE customization color tag, so <c>Item.RefreshTags</c> [G Item.cs:128-150] writes the palette
        /// blue into the bodypart shader via <c>HighlightControllerComponent.CustomizeColor(tag.ShaderParamName,
        /// color)</c>. `AlwaysCustomizeColor=true` satisfies the `flag2` gate. Blue tag =
        /// `CustomizationSecondaryColorTagDef_2` (the secondary BLUE, confirmed [T TFTVDefsInjectedOnlyOnce.cs:3282]).
        /// Works ONLY if the Crabman bodypart shader exposes that color param — UNKNOWN, hence the probe +
        /// the shader diagnostics. Reuses the existing vanilla tag; mutates only the CLONE (its Tags are
        /// deep-copied by CreateDef, same as AugmentVariants.Prepare's BionicalTag add). Null-guarded.</summary>
        private static void TintBlue(DefRepository repo, TacticalItemDef clone)
        {
            if (clone == null)
            {
                return;
            }
            clone.AlwaysCustomizeColor = true;
            GameTagDef blue = DefUtils.ResolveByName<GameTagDef>(repo, "CustomizationSecondaryColorTagDef_2");
            if (blue == null)
            {
                TheTurnedMain.LogWarn("[TheTurned] umbra tint: 'CustomizationSecondaryColorTagDef_2' (blue) not found — "
                    + $"'{clone.name}' AlwaysCustomizeColor set but no blue tag added.");
                return;
            }
            if (clone.Tags != null && !clone.Tags.Contains(blue))
            {
                clone.Tags.Add(blue);
            }
            string shaderParam = (blue as CustomizationColorTagDef)?.ShaderParamName ?? "<not-a-ColorTag>";
            TheTurnedMain.LogInfo($"[TheTurned] umbra tint: '{clone.name}' AlwaysCustomizeColor=true, "
                + $"added blue tag '{blue.name}' (ShaderParamName='{shaderParam}').");
        }
    }
}
