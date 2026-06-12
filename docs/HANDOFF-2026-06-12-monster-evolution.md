# The Turned — Monster Evolution System: Design + Handoff (2026-06-12)

> **Supersedes** the older cell-progression plan for the **progression semantics**:
> `..\..\docs\superpowers\specs\2026-06-10-cell-progression-design.md` and
> `..\..\docs\superpowers\plans\2026-06-10-cell-progression-v1.md`. Where those docs
> and this one disagree on progression (cells, tracks, evolution), THIS doc wins.

---

## 1. CURRENT VERIFIED STATE (done + in-game OK unless noted)

- Recruit = turned Arthron as a Phoenix soldier (`Ctrl+Shift+T` dev hotkey). Hard dep **TFTV**.
- Augment/Bionics screen (DNA button) for body-part swaps: **head + left arm + right arm**. Arms swap cleanly (gold standard).
- **CELL-ARMOR render saga RESOLVED** (in-game OK): armor (legs/torso/carapace) persists.
  - ROOT CAUSE: `UIStateEditSoldier.UpdateSoldierEquipment:492` re-pushing the stale human `ArmorList` into `ArmourItems` every refresh.
  - FIX: Harmony Prefix `EquipReapplyPatch` on `UpdateSoldierEquipment` — for Phase4 recruits, re-push only ready+inventory, pass `armour=null` (never revert crab armour; avoids `UpdateStats`→`_uiRefreshNeeded` infinite loop).
- **SLOT-OWNERSHIP MODEL (locked)**: CELL-ARMOR owns **torso + carapace + legs** (always present); AUGMENT owns **head + arms** (free swap). Disjoint slots → independent async swap.
- **HEAD spit = WEAPON-SLOT-OCCUPANT** (in-game OK):
  - The spit weapon itself occupies `Crabman_Head_SlotDef` (skull from chassis, organ mesh = `WeaponDef.SkinData`).
  - Spitter / Evolved-Spitter cards = clone of the spit `WeaponDef` via `CrabmanParts.CloneHeadWeapon` (`Hand=null`).
  - Armored card = `EliteHumanoid` bodypart clone.
  - All head cards `Hand==null` → `EnforceSetForBodypart` returns null → no flat-hand 2nd `SetItems` → no lag/flicker.
  - **Minor:** user reports flicker still appears under some unspecified conditions — TODO pin.
- **Duplicate-torso de-dup** in `CellArmorApply.BuildArmorList` (torso provides head+arm slots; 2 torsos broke native `CanSwapItem` → head no-react + arm-duplicate). Fixed.
- **BUG1**: cell-1 is buyable (was instant-nav); DNA button gated on cell-1-learned via `EditUnitButtonsController.RefreshContextButtonVisibility()`.

---

## 2. BUILT BUT NOT YET TESTED (uncommitted working tree) — VERIFY/DEPLOY FIRST

- **5-LEVEL EVOLUTION feature** implemented + **builds clean**, but **NOT yet deployed/tested in-game**. `DiagArmInsteadOfArmor=false`.
- The last **DEPLOYED** dll was the head-weapon-occupant fix — so the new session **MUST rebuild + redeploy** (copy `Dist\*` to `D:\Steam\steamapps\common\Phoenix Point\Mods\TheTurned\` per `DEVELOPMENT.md`) and **test the evolution feature in-game BEFORE adding more**.
- **Generic engine pieces added:** `Core\EvolutionMarkers.cs` (monster-agnostic registry):
  - `EvolveScope { None, LeftWeapon, AllWeapons }`; marker→scope + normalToken→eliteToken maps.
  - `HighestLearnedScope(prog)`, `TryGetEliteToken`.
- **Arthron data:** `Monsters\Arthron\ArthronEvolution.cs` (7 normal→elite token pairs + tunable Alpha/Prime stat constants).
- **Evolve apply point:** `ArthronArms.ApplyChosenSets` — after deriving the player's chosen sets, replaces each chosen set's token with its elite token per `HighestLearnedScope`:
  - `LeftWeapon` → left only; `AllWeapons` → left + right + head.
  - Honors player choice, composes idempotently in the same `SetItems` derive.
  - Default never-chosen Pincer right arm won't auto-evolve (no learned marker) — **acceptable**.
- **New 5-cell ladder** (`ArthronCellRow.cs`):
  - **L1** NAV (unlock augment)
  - **L2** `[Legs_Armoured + Carapace]`
  - **L3** `[Legs_EliteAgile + Carapace]` + **Alpha stats** (overrides L2 legs — intended visual dip)
  - **L4** `[Legs_EliteArmoured + EliteTorso + EliteCarapace]` + `EvolveScope.LeftWeapon`
  - **L5** (inherits L4 armor) + **Prime stats** + `EvolveScope.AllWeapons`
  - Costs **20 / 20 / 20 / 25 / 25**.
- **Stat constants** (tunable, `ArthronEvolution`):
  - **Alpha** = Endurance+5, Speed+1, Willpower+2, BonusAttackDamage+10.
  - **Prime** = Endurance+10, Speed+2, Willpower+4, BonusAttackDamage+25, Armour+10 (stacks on Alpha).

---

## 3. APPROVED DESIGN — still to BUILD

Two **independent** progression tracks:

- **TRACK 1 — Personal power** (character XP/level), the 5 cells above. Keep XP as-is for now (may artificially lengthen later — **deferred**).
- **TRACK 2 — Mutation AVAILABILITY** gated by **GLOBAL Pandoran evolution** (auto, player can't influence). Gates **WHICH augment swap CARDS are offered**, in **WAVES** (slots open over the campaign):
  - head available early → **RIGHT arm opens mid** → **LEFT arm opens later**.
  - Paced by `GeoAlienFaction.EvolutionProgress` so it spreads across the whole campaign regardless of recruit XP.
- **CARD SIMPLIFICATION (approved):** each augment card = a **FUNCTION/role**:
  - Right: Claw / MG / Viral; Left: Shield / Grenade / Acid; Head: Spit / none.
  - **REMOVE** the separate "evolved-look but base-stat" duplicate cards. Elite look+stats come **ONLY** from Track-1 personal evolution (L4/L5).
  - Net: **EP = which roles available; XP = how strong/evolved.**
- **STAT CLEANUP (approved):** set part stats to the real wiki values (table §6). Base-tier parts = real base stats; evolution (L4/L5) uses the **RAW native Elite defs** (real elite stats), **NOT** normalized clones.

---

## 4. GROUNDED MECHANISM — Pandoran evolution gate

- **Field:** `GeoAlienFaction.EvolutionProgress` (int, 0 → ~4700 end-game, save-persisted, monotonic, `+EvolutionProgressPerDay` daily via `UpdateFactionDaily`).
  - TFTV per-day **35 / 40 / 50 / 70** by difficulty. Game/TFTV already buckets **~470 EP = 1 "evolution level"** (ODI).
  - `decompiled GeoAlienFaction.cs:116 / 90 / 265 / 803 / 1068`.
- **Access** (geoscape-time, mod already uses this pattern):
  - `GameUtl.CurrentLevel().GetComponent<GeoLevelController>().AlienFaction.EvolutionProgress` (`GeoLevelController.cs:223`). **Guard `AlienFaction` null.**
- **Card listing:** `UIModuleBionics.InitPossibleMutations` (`UIModuleBionics.cs:328-360`) rebuilds `section.PossibleMutations` LIVE on each open via `BionicsSectionPatch.OnNewCharacter` (`BionicsSectionPatch.cs:103`).
  - **FILTER POINT** = prune `section.PossibleMutations` in `BionicsSectionPatch` postfix (`~:120-128`) by per-card `RequiredEvolution` vs current EP.
- **Generic boundary:** Core reads EP + prunes (no monster names). Per-monster DATA = add `int RequiredEvolution` to `CrabmanParts.MatchedSet` (`CrabmanParts.cs:14-20`), set per card in the Arthron `PairSide` / `PairHeads` (`CrabmanParts.cs:142, 167`).
- **Proposed EP thresholds** (TUNABLE, 470-bucket): head EP **0** (start) · right arm EP **~940** · left arm EP **~1880** · (late/advanced EP **~2820+** if added).
- **Fallback monotonic signals** if needed: `GeoLevelController.ElaspedTime` (days), Phoenix `Research.Completed` count.

---

## 5. GENERIC ARCHITECTURE (must keep — new monsters plug in with ZERO Core edits)

- **Core** (`src\Core\*`) = generic engine reading per-monster DATA:
  - `CellArmorApply` / `CellArmorMarkers` (armor loadouts)
  - `EvolutionMarkers` (evolve)
  - `Phase4RowCells` / `PerkFactory` (cells + stats)
  - `BionicsApplyPatch` / `BionicsSectionPatch` / `AugmentVariants` (augment screen)
  - `EquipReapplyPatch` (armor persist)
  - `CellRowPurchasePatch` / `AugmentButtonVisibilityPatch`
- **Per-monster DATA** (`src\Monsters\<X>\*`) declares: cell specs, leg/carapace/torso def names, normal→elite token map, EP thresholds per card, base stats.
  - **Arthron** = `Monsters\Arthron\{ArthronMonster, ArthronCellRow, ArthronEvolution, ...}` + `CrabmanParts` / `WeaponVariants`.
- A **new monster** (e.g. Triton) = new folder + 1 line in `MonsterRegistry`, **NO Core change**.

---

## 6. AUTHORITATIVE STAT TABLE (wiki vanilla [W]; map to def names)

### Base chassis — `Crabby_AlienMutationVariationDef` (Arthron Worker)
| Stat | Value |
|---|---|
| HP | 110 |
| Will | 9 |
| Move/Speed | 20 |
| Perception | 25 |
| Accuracy | 0 |
| Stealth | 0 |

**Body parts** — `HP(Armor)`:
| Part | HP(Armor) |
|---|---|
| Head | 40(10) |
| LeftArm | 50(0) |
| Torso | 90(10) |
| RightArm | 50(0) |
| Legs (each) | 60(0) |

> `MaxHP = Toughness + Endurance×EnduranceToHealthMultiplier`; `Endurance=Strength` at init (Toughness/mult are bundle-only RDN).

### HEAD
| Def | HP(Armor) | Dmg | Pierce | Poison | Burst | Range | AP |
|---|---|---|---|---|---|---|---|
| Spitter (base) | 50 | 10 | 20 | 60 | 1 | 5 | 2 (Armor 0) |
| EliteSpitter | 70 | 15 | 30 | 80 | — | 5 | 2 (Armor 20) |
| Humanoid head | 40(10) | — | — | — | — | — | — |
| EliteHumanoid | RDN | — | — | — | — | — | — |

> **No fire head def exists.**

### RIGHT ARM
| Def | HP(Armor) | Type | Dmg | Shred | AP | Range | Burst | Ammo | Extra |
|---|---|---|---|---|---|---|---|---|---|
| Pincer | 50(0) | melee | 65 | 1 | 1 | 1 | — | — | +Armour20 (Strike) |
| ElitePincer | 100 | melee | 95 | — | — | — | — | — | +Armour30 |
| Gun (MG) | 80(20) | MG | 35 | — | 2 | 16 | 6 | 36 | +innate ReturnFire |
| EliteGun | 100 | MG | 45 | 3 | — | 16 | 6 | 36 | — |
| Viral_Gun | 80 | gun | 25 | 1 | — | — | 6 | 36 | Viral1, Range14 |
| Viral_EliteGun | 100 | gun | 30 | — | — | 17 | 10 | 60 | Viral1 |

### LEFT ARM
| Def | HP(Armor) | Dmg/Effect | Shred | AP | Blast | AoE | Range | Ammo | Extra |
|---|---|---|---|---|---|---|---|---|---|
| LeftArm (plain) | 50(0) | — | — | — | — | — | — | — | — |
| Shield (bodypart) | 100 | Deploy Shield | — | 0 | — | — | — | — | frontal Armor 30 |
| EliteShield | 150 | — | — | — | — | — | — | — | Armor 50 |
| Grenade GL | 80 | — | 3 | 2 | 50 | 2.5 | 11 (vanilla; TFTV→15) | 3 | — |
| EliteGrenade | 100 | — | — | — | 60 | — | — | — | — |
| Acid_Grenade | 50 | Acid10 | — | — | 10 | — | 11/15 | 3 | — |
| Acid_EliteGrenade | 100 | Acid20 | — | — | 20 | — | — | — | — |

> **NOTE:** catalog mislabel "shield armour 150" is actually the **HP(150)**; real armor = **30/50**.

### LEGS
- Agile / EliteAgile = jump 1 floor (**Agile Legs** ability); Armoured / EliteArmoured = no jump, armored.
- Per-strain leg `HP(Armor)` examples: base **60(0)** → evolved **90(20)**; Shieldbearer/Tyrant up to **100(10)** → **150(30)**.
- Exact per-leg-def Armor/HP/Speed/Weight = **RDN** (runtime-dump needed).

### TORSO / CARAPACE
| Def | HP(Armor) |
|---|---|
| Torso | 90(10) |
| EliteTorso | RDN |
| Carapace (`Crabman_Carapace`) | 120(20) |
| EliteCarapace | 180(40) [wiki explicit] |

### Abilities
- **Poison Spit** (spitter head)
- **Return Fire** (all gun arms)
- **Deploy Shield** (shield arm, AP0)
- **Agile Legs** jump

### STILL-MISSING (runtime-dump-needed)
- EliteHumanoid head HP/Armor
- ElitePincer pierce/shred
- per-leg-def Armor/HP/Speed/Weight (all 4)
- EliteTorso HP/Armor
- Carapace runtimeKey / render confirm
- chassis Toughness / EnduranceToHealthMultiplier / Strength

> **Dump method:** `repo.GetAllDefs` filtered `name.Contains("Crabman_")`, log `HitPoints / Armor / Weight / DamagePayload / Range / AP / ProjectilesPerShot / ChargesMax / Abilities`.

---

## 7. NEXT-STEPS (implementation order)

1. **Rebuild + redeploy** current working tree; **TEST the 5-level evolution feature in-game** (legs per level, carapace render, weapon→Elite at L4/L5, Alpha/Prime stats). Report results.
2. Implement **TRACK 2 EP-gated card availability**: add `MatchedSet.RequiredEvolution` (Arthron data), Core prune in `BionicsSectionPatch` by EP. Thresholds head **0** / right **940** / left **1880** (tunable).
3. **Simplify augment cards to functions** (drop evolved-look base-stat duplicates).
4. **Apply real stats (§6)**: base parts real base stats; evolution uses raw native Elite defs. Add a one-shot runtime def-dump hotkey to confirm STILL-MISSING bundle values, then finalize.
5. **(Deferred)** lengthen XP for pacing; pin the residual head flicker; cleanup dev hotkeys/diagnostics; commit + push.

---

## 8. READ FIRST (research docs)

Monorepo research root: `..\..\docs\research\`

- [`crabman-bodypart-catalog.md`](..\..\docs\research\crabman-bodypart-catalog.md)
- [`augment-mutation-screen-reuse.md`](..\..\docs\research\augment-mutation-screen-reuse.md)
- [`arthron-arm-weapons.md`](..\..\docs\research\arthron-arm-weapons.md)
- [`arthron-compendium.md`](..\..\docs\research\arthron-compendium.md)
- [`theturned-phase4-implementation.md`](..\..\docs\research\theturned-phase4-implementation.md)
- [`arthron-augment-dead-ends.md`](..\..\docs\research\arthron-augment-dead-ends.md)
- [`source-provenance.md`](..\..\docs\research\source-provenance.md)

Superpowers (older — progression semantics **SUPERSEDED** by this doc):
- [`..\..\docs\superpowers\specs\2026-06-10-cell-progression-design.md`](..\..\docs\superpowers\specs\2026-06-10-cell-progression-design.md)
- [`..\..\docs\superpowers\plans\2026-06-10-cell-progression-v1.md`](..\..\docs\superpowers\plans\2026-06-10-cell-progression-v1.md)

Mod working rules:
- `TheTurned\DEVELOPMENT.md` (build + deploy)
- `CLAUDE.md` (LEAD orchestrator, delegate, Serena / Context7 / Sequential-Thinking)
