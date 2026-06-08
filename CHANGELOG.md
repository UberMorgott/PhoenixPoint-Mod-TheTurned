# Changelog

All notable changes to **The Turned** are documented here. Format loosely follows [Keep a Changelog](https://keepachangelog.com/); this project uses [Semantic Versioning](https://semver.org/).

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
