# TheTurned — Arthron Stat Spec (LOCKED, single source of truth)

> **Scope: the EXAMPLE monster.** TheTurned is a generic recruit framework; these numbers are
> Arthron-specific DATA, not framework rules. A future monster ships its own spec — the `Core`
> machinery (cells, marker gating, stat-passive plumbing) is identical.
> User-approved, wiki-derived stat rework. Numbers are LOCKED — do not re-derive; implement exactly.
> Date: 2026-06-13. Tier mapping: **L1-L2 = Basic, L3 = Alpha, L4 = Alpha+evolve (limbs flip
> normal->evolved), L5 = Prime.** Cells gate one-per-level (cell N at level N,
> `CellRowPurchasePatch` adjusted-level gate).

## HP formula (grounded)

- `MaxHP = Toughness + Endurance x EnduranceToHealthMultiplier`.
  - `EnduranceToHealthMultiplier = 10` [G `TacticalActorBaseDef.cs:23`] (default; on SHARED base def — NOT touched).
  - Arthron `Toughness = 120` (on SHARED base def — NOT touched).
  - `TacCharacterData.BonusStats.Endurance = Strength` [G `TacCharacterData.cs:61-63`] — the per-def
    authoring field **Strength** seeds the runtime **Endurance** stat. `CharacterStats.cs:303`:
    `Health = Toughness + Endurance x healthMultiplier`.
- Cell stat passives add `StatModificationTarget.Endurance` (+10 MaxHP each) on top of the base, stacking
  additively across all learned (lower) cells.

## OVERALL actor stats — locked curve + back-solve

| Level | HP  | Will | Move | Endurance(total)=(HP-120)/10 |
|------|-----|------|------|------|
| L1   | 150 | 12   | 18   | 3  |
| L2   | 220 | 16   | 20   | 10 |
| L3   | 300 | 21   | 22   | 18 |
| L4   | 370 | 26   | 23   | 25 |
| L5   | 440 | 30   | 25   | 32 |

Implementation (base + per-cell deltas):

| Source | dEnd | dWill | dSpeed | -> End / Will / Speed | -> HP |
|---|---|---|---|---|---|
| L1 base (`ArthronMonster.ApplyStatOverrides`: Str3/Will12/Speed18) | 3 | 12 | 18 | 3 / 12 / 18 | 150 |
| Cell2 ARMOR1 (`Cell2Stats`)        | +7 | +4 | +2 | 10 / 16 / 20 | 220 |
| Cell3 STAT_ALPHA (`AlphaStats`)    | +8 | +5 | +2 | 18 / 21 / 22 | 300 |
| Cell4 ARMOR2_EVOLVE (`Cell4Stats`) | +7 | +5 | +1 | 25 / 26 / 23 | 370 |
| Cell5 STAT_PRIME (`PrimeStats`)    | +7 | +4 | +2 | 32 / 30 / 25 | 440 |

- **NOTE:** base Strength=3 is forced by locked L1=150 HP + fixed Toughness 120 + x10. Strength also seeds
  carry-weight -> low carry capacity by design.
- The old actor-wide `BonusAttackDamage` on Alpha/Prime is **DROPPED**; limb damage is defined by the
  weapon defs (Pincer 65->95, etc.), not a flat actor bonus.

## PER-PART HP(Armor) — targets

| Part | L1 | L2 | L3 | L4 | L5 |
|---|---|---|---|---|---|
| Head        | 40(10) | 50(10) | 60(20) | 70(20) | 70(20) |
| Torso       | 90(10) | 100(20)| 120(20)| 120(30)| 120(30)|
| Arm hitbox  | 50(0)  | 60(10) | 60(20) | 70(20) | 70(20) |
| Leg (each)  | 70(0)  | 90(10) | 100(20)| 120(30)| 150(30)|

Realize via the def equipped at that level (real loadout defs' `ItemDef.HitPoints`/`Armor`).

> **CONFLICT (per-part not yet implemented — see "Open conflicts" below).** The cell ladder reuses ONE
> def across multiple levels (torso L1-3 = base `Crabman_Torso_BodyPartDef`; legs L4=L5 =
> `Crabman_Legs_EliteArmoured_ItemDef`; carapace L2=L3 = `Crabman_Carapace_BodyPartDef`), and head/arm
> hitboxes are player-CHOSEN via the augment screen (not level-scaled). A single def can hold only ONE
> HP/Armor pair, and these are the REAL shared enemy defs (editing them flips all enemy Arthrons).
> Per-level per-part values therefore cannot be expressed without cloning per level + accepting enemy
> spillover. HELD for user decision.

## LIMB evolved values (constant per limb; flip at L4)

`normal -> evolved`: Pincer 50(20)->100(30) dmg65->95 | Spitter 50(0)->70(20) | MG 80(20)->100(20) |
ViralMG 80(20)->100(20) | Shield 100(30)->150(50) | GL 80(0)->100(0) | AcidGL 50(0)->100(0).

- **EXCEPTIONS — do NOT transform at L4** (already separate already-evolved cards): Pincer (claw) and
  Spitter-head. Leave their evolve behaviour as-is.

## ARMORED HEAD (tank identity, no spit/attack)

- Basic/until-L4: **HP 100, Armor 40**. At Prime/L5 context: HP 120, Armor 50.
- IMPLEMENTED: `CrabmanParts.cs` Armored-card clone overridden to **HP 100 / Armor 40** AFTER
  `NormalizeBodypartStats` (not re-normalized down). Clone def -> real enemy head untouched.
- **NOTE:** one clone def = one stat pair; the L5 120/50 bump cannot be expressed on the single static
  clone -> shipped at the until-L4 value.

## ADAPTIVE EVOLVED LABELS (held)

After L4 (evolved scope learned), changed parts should read "Evolved X" with changed stats in the tooltip.
HELD — the clone VED is GLOBAL (shared by the card), and `HighestLearnedScope` is per-recruit; a global
rename would mislabel for every recruit. Needs per-recruit label design. Pincer/Spitter already read as
evolved (keep).

## Tooltips

- Body-part hover: native `UIItemTooltip.SetTacItemStats` / `UIMutationTooltip.ShowStats` render
  `ItemDef.HitPoints`/`Armor` + `BodyPartAspectDef` automatically (no UI code).
- Cell skill hover (L1-5): explicit interpolated `_DESC` loc text in `Assets/Localization/TheTurned.csv`
  (`ARTHRON_CELL_*_DESC`) — surfaces +HP/+Will/Speed-> / limbs->evolved the vanilla way. EN + RU populated
  (the mod's two filled languages).

## Implementation status (2026-06-13)

- DONE: base stats (`ApplyStatOverrides`), per-cell HP/Will/Move deltas (`ArthronEvolution` + wired cell2/4
  in `ArthronCellRow`), armored-head HP100/Armor40, cell `_DESC` loc.
- HELD (open conflicts, user decision): per-part per-level HP/Armor (shared-def + player-chosen +
  enemy-spillover); armored-head L5 variant; leg L5 150(30) distinct def; adaptive evolved labels.

## Open conflicts (need user decision)

1. **Per-part per-level HP/Armor.** Options: (a) clone every loadout def per level (large churn; risks the
   torso/carapace dedup + augment swap reference-equality the fixed bugs depend on); (b) edit the REAL
   shared defs to ONE per-tier value (flips enemy Arthrons too); (c) accept tier-granular (base/elite)
   per-part values instead of per-level. Recommend (c) for parts + (a) only for legs L4/L5 if the L5 bump
   is wanted.
2. **Armored-head L5 (HP120/Armor50)** and **leg L5 (150/30)** distinct-def variants — same granularity
   limit; need a per-level clone or accept the until-L4 value.
3. **Adaptive evolved labels** — per-recruit vs global VED.
