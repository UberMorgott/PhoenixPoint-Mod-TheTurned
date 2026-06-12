# Changelog

All notable changes to **The Turned** are documented here. Format loosely follows [Keep a Changelog](https://keepachangelog.com/); this project uses [Semantic Versioning](https://semver.org/).

## [0.3.1] - 2026-06-13

Phase 4 bug-fix round: the 5-level monster-evolution track is now in-game verified. All fixes built 0-warn, deployed, and live-tested.

### Fixed

- **Legs evolution ladder order.** `ArthronCellRow` reorders the cell→leg tokens to the visual ladder light → light-armored → heavy → heavy-armored (`order2 = EliteAgile`, `order3 = Armoured`).
- **Cell gating — exactly 1 cell per level.** `CellRowPurchasePatch` gates with `SecondSpecializationLevel = 4` + `MaxLevel = 6`, yielding exactly one cell per character level; cell 5 becomes buyable at L5.
- **L5 cell + augment NRE.** Dropped the `Armour` stat-mod from Prime stats — an unresolvable passive `Armour` caused an "Armour not found" NRE on `UpdateStats`.
- **Right-arm drop.** `SwapSet` now skips the flat-hand add when the bodypart already carries its hand as a `SubAddon`, so the right arm no longer drops.
- **Pre-L4 elite guard.** `EvolutionMarkers` adds a level-guard (`charLevel >= unlockLevel`) so no elite parts appear before L4; the head is manual-only and excluded from auto-evolve.
- **All weapons evolve at L4.** Cell-4 evolve scope widened `LeftWeapon` → `AllWeapons` (model + stats) in `ArthronArms`, so left + right + head all upgrade at L4.
- **Head flicker / generic slot de-dup.** New per-slot dedup in `EnforceSetForBodypart` keys by the `RequiredSlotBinds` slot `Guid` → one occupant per slot across all sides, killing the per-frame `ArmorContainer` churn that caused head flicker.
- **Viral gun** evolution resolved.

### Notes

- Dev-only diagnostics added this round: `CellDevDump`, and `Ctrl+Shift+L` leg-cycle (`DevLegCycle` in `RecruitHotkey`) — to be removed at cleanup.

## [0.2.0] - 2026-06-09

Phase 2: framework refactor, real Arthron perks, and a stat rebalance.

### Changed

- **Refactored to a reusable framework.** The monolithic, Arthron-specific code was split into a generic **`Core`** layer plus **per-monster** definitions under `src\Monsters\`. Adding a new recruitable Pandoran is now a new folder implementing `ITurnedMonster` plus one line in `MonsterRegistry.RegisterDefaults()` — no `Core` changes. See `DEVELOPMENT.md`.
- **Stat rebalance — "heavy bruiser, but fair."** The recruited Arthron's base stats are now explicit overrides on the clone (the shared enemy def is untouched, so wild Arthrons are unaffected):
  - **Strength = 20**, **Will = 18**, **Speed = 12**.
  - Base **MaxHP ≈ 320** (down from ~1120), via the game formula `MaxHP = Toughness(120) + Strength(20) × EnduranceToHealthMultiplier(10)`. Tanky alien soldier, not god-tier; HP/armour grow further through perks.

### Added

- **Real, Arthron-themed 7-slot ability track** (replaces the placeholder Sniper abilities):
  - **Slot 0 — Arthron Instincts (class proficiency):** class identity; proficiency extended with `HandgunItem_TagDef` + `PDWItem_TagDef` so the Arthron can also wield a human sidearm (its claws/viral-gun are innate bodypart weapons).
  - **Slot 1 — Natural Armour:** +10 Armour.
  - **Slot 2 — Acid Glands / Acid Spit:** prefers a vanilla acid ranged ability (resolved by name, present under TFTV); falls back to a +15 bonus-attack-damage passive if unresolved.
  - **Slot 3 — Chitin Plating (signature):** +15 Armour, +5 Endurance (+50 MaxHP).
  - **Slot 4 — Crushing Claw:** +20 bonus attack damage (boosts the innate claw melee).
  - **Slot 5 — Hardened Hide / Regeneration:** prefers a vanilla regeneration ability by name; falls back to a +8 Endurance (+80 MaxHP) passive.
  - **Slot 6 — Apex Carapace (capstone):** +20 Armour, +10 Endurance (+100 MaxHP), +10 bonus attack damage.
  - Progression costs scale 10→30 SkillPoints and 10→30 Mutagen toward the capstone. Fully leveled, the Endurance perks stack on the 320 base to roughly ~550 MaxHP plus heavy armour.
- **Localization scaffold.** Perk and class names/descriptions load from `Assets\Localization\TheTurned.csv` (English populated; other columns ready).
- **Icon loader** wired for per-perk sprites under `Assets\Textures\` (graceful no-op when a PNG is absent — the Sniper icon is shown as a fallback).

### Known issues

- Acid Spit and Regeneration may use their custom-passive fallback if the vanilla def names don't resolve in the live def DB (they should resolve when TFTV is loaded).
- Perk icons are placeholders (loader is wired, art pending).
- In-game verification of the final build is pending a game restart (could not be live-tested from the build host).

## [0.1.0] - 2026-06-09

First playable developer build (Phase 1).

### Added

- **Hotkey recruit.** Press **Ctrl+Shift+T** on the geoscape to recruit one Pandoran Arthron (codename *Crabman*) into the Phoenix roster. The recruit chain resolves a basic Arthron def at runtime, clones it, and grants it via the game's native faction-reward path. Idempotent — re-pressing reuses the same clone.
- **Arthron as a soldier.** A dedicated class (class tag + marker tag + 7-slot `SpecializationDef`) makes the generator attach a real progression, so the recruited Arthron is filed under **Soldiers** (not vehicles).

### Fixed

- **TFTV BetterClasses popup.** The recruited Arthron's cloned def now carries a borrowed, non-null `LevelProgression`, so TFTV's `GenerateUnit` prefix no longer dereferences null and spawns its error popup.
- **Edit-screen crash.** With a real runtime progression and human (soldier) classification, opening the Arthron's edit/progression screen no longer throws a null-reference exception.

### Known issues

- Ability track slots 1–6 are placeholder vanilla (Sniper) abilities, not Arthron-themed perks.
- No custom icons or localization yet.
- Mutate/bionics/equip screens render incompatible non-human bodyparts (cosmetic; no crash).
