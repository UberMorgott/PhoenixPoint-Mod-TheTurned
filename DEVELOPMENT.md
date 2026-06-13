# The Turned — Development Notes

Self-contained dev reference so this repo alone bootstraps a new session. The *fuller* grounded research (with decompile `file:line` citations for every claim) lives in the monorepo at `docs\research\theturned-arthron-recruit.md`; this file is the distilled, repo-local version.

`[G]` = base-game decompile, `[T]` = TFTV reference source, `[O]` = Officer mod (the `ModMain` template this mod mirrors).

---

## What the mod is

**The Turned is a generic, monster-agnostic FRAMEWORK** for recruiting **any** turned Pandoran monster into the Phoenix roster as a real, playable **soldier** — with its own recruit chain, soldier-style evolution cells, a part/augment swap system, and a single shared recruit-marker that gates every mod behavior. The framework is the product; a concrete monster is just data plugged into it.

**Arthron (codename *Crabman*) = the FIRST / reference monster.** Everything Arthron-specific (its perk tables, stat numbers, bodypart defs) is one worked example living under `src\Monsters\Arthron\`; the `Core` layer knows nothing about it. Adding a second monster (e.g. Triton, Siren) is a new folder under `src\Monsters\` + one line in `MonsterRegistry.RegisterDefaults()` — **no `Core` changes**.

At runtime the framework grants a clone of the chosen enemy def to the Phoenix faction, classified as a playable **soldier** with a balanced stat block and a real evolution track. Triggered today by **Ctrl+Shift+T** on the geoscape (dev hotkey). Hard-depends on TFTV (`Dependencies: [ "phoenixrising.tftv" ]`); the Phase-4 runtime guard (`Phase4.Init`/`Phase4.Enabled`) probes the resolved dependency and falls back to the Phase-2/3 fixed track when TFTV is absent. Target framework **.NET Framework 4.7.2**; mod ID `Morgott.TheTurned`, v0.3.2.

## Universal recruit-marker gating (the core mechanism)

The framework's single load-bearing mechanism is **one shared `GameTag`** — `"TheTurned_RecruitTag"` (`Tags.RecruitMarkerTag`, `src\Core\Tags.cs`) — stamped on **every** turned recruit, regardless of which monster it is. All mod behavior (soldier classification, evolution cells, augment screen, the today's-fixes UI guards) gates on that one marker. There is no per-monster branching in the gate.

- **Geoscape side** — `Phase4.IsPhase4Recruit(GeoCharacter)`: true iff `template.Data.GameTags.Contains(marker)` **and** `Phase4.Enabled`. Used by every geo-side patch (`AugmentButtonVisibilityPatch`, `BionicsApplyPatch`, `BionicsSectionPatch`, `RecruitFatiguePatch`, …).
- **Tactical side** — on the live actor: `TacticalActorBase.GameTags.Contains(marker)`. Used by tactical-time patches (e.g. `RandomTagsExclusiveGuard`) where there is no `GeoCharacter`.
- **Why one marker:** soldier classification (`CheckIsHuman` Postfix) and every guard read the same tag, so a new monster inherits all framework behavior for free the moment its clone carries the marker — never the shared/enemy def, which is left untouched so wild monsters are unaffected.

## Architecture (Core + per-monster) — the universal framework

The codebase is a **generic `Core`** layer plus **per-monster** definitions under `src\Monsters\`. The Core knows nothing about any specific monster; each monster supplies its own data through one interface. This split IS the framework: `Core` does all the heavy lifting once, monsters add data only.

### `src\Core\` (generic, monster-agnostic)

- **`ITurnedMonster.cs`** — the contract every monster implements: `Id`, `RecruitKey`, `ResolveTemplate(repo)` (find the source enemy def), class/spec display metadata, `BuildAbilityTrack(repo, proficiency)` → 7 slots, `ApplyStatOverrides(clone)`, and the stable GUIDs (clone, class tag, spec, track, proficiency, plus the progression/VED GUIDs).
- **`TurnedMonsterBase.cs`** — abstract base implementing the boilerplate of `ITurnedMonster`; monsters subclass it and override only what's monster-specific.
- **`MonsterRegistry.cs`** — the explicit registry. `RegisterDefaults()` clears and registers each monster (currently one line: `Register(new ArthronMonster())`). Core iterates `MonsterRegistry.All` to build classes and map hotkeys.
- **`DefUtils.cs`** — def-system helpers: `GetOrCreate`/`Clone` (idempotent `CreateDef` guards), `AppendDataGameTag`, `ResetClassTagsCache` (reflection null of `_classTags`), `BorrowHumanLevelProgression`, `RegisterSpecInSharedData` (Contains-guarded append into `SharedData.SharedGameTags.Specializations`), `ResolveByName<T>`, `AnyTemplate<T>`.
- **`Tags.cs`** — the **one** shared marker `GameTag` `"TheTurned_RecruitTag"` (`Tags.RecruitMarkerTag`) stamped on every recruited monster. It is the universal gate for ALL mod behavior (soldier classification, evolution cells, augment screen, UI guards), read geo-side via `Phase4.IsPhase4Recruit` and tactical-side via `TacticalActorBase.GameTags.Contains(marker)`. See "Universal recruit-marker gating".
- **`TurnedClassFactory.cs`** — generic `EnsureClass`: clones the vanilla **Sniper** spec / proficiency / ViewElementDefs, builds the monster's ability track, and registers the spec.
- **`TurnedRecruiter.cs`** — generic `RecruitMonster`: resolve template → clone + borrow LevelProgression → append the class tag + the shared marker → reset the class-tags cache → `ApplyStatOverrides` → `GenerateUnit` → `SpawnAsCharacter` → `GeoFactionReward` grant chain.
- **`RecruitHotkey.cs`** — `MonoBehaviour` on `ModGO`; iterates the registry and fires the matching monster's recruit on **Ctrl+Shift+<key>** (legacy `UnityEngine.Input` poll; `UnityEngine.InputLegacyModule.dll` referenced from the live game Managed folder).
- **`HumanClassificationPatch.cs`** — the shared Harmony **Postfix on `TacCharacterDef.CheckIsHuman`**, keyed on the **one** marker tag (`__result = true` only when the def's `Data.GameTags` contains it). `IsHuman => CheckIsHuman()`, so one Postfix covers all read paths for all monsters.
- **`PerkFactory.cs`** — generic, dependency-free perk builder: `BuildStatPassive(...)` creates a self-contained `PassiveModifierAbilityDef`; `Add(target, value)` builds an `ItemStatModification`.
- **`Icons.cs`** — sprite loader for perk PNGs under `Assets\Textures\` (graceful no-op if a PNG is absent — falls back to the Sniper icon).
- **`Localization.cs`** — CSV loader for `Assets\Localization\TheTurned.csv`.
- **`ModMain.cs`** — `TheTurnedMain : ModMain`. `OnModEnabled`: register monsters + build all classes + apply the `CheckIsHuman` patch once + attach the hotkey + load the loc CSV. `OnLevelEnd` re-applies the classes on the `"Home"` level for persistence. `CanSafelyDisable => false`. Class creation MUST happen in `OnModEnabled` (before any geoscape) so `FactionCharacterGenerator.Start()` caches the specs.

### `src\Monsters\Arthron\` — the EXAMPLE monster (Arthron-specific data)

> This folder is the first / reference monster. It is pure DATA for the `Core` framework above — a template for any future monster, not part of the engine.

- **`ArthronMonster.cs`** — `ITurnedMonster` impl (via `TurnedMonsterBase`): resolves a basic Crabman variant (filter by `Crabman_ClassTagDef`, prefer non-Elite/non-Ultra, ordinal name order), carries the preserved stable GUIDs, applies the stat overrides, and delegates the track to `ArthronPerks`.
- **`ArthronPerks.cs`** — the 7-slot track + the per-perk builders and their numeric consts (see the perk table below).

## The grounded recipe (why it works)

### Recruit chain `[G]`
`geo.CharacterGenerator.GenerateUnit(PhoenixFaction, template)` → `GeoUnitDescriptor.SpawnAsCharacter()` → `new GeoFactionReward { Units += geoChar }.Apply(PhoenixFaction, site, null)` → `GiveUnits` → `GeoPhoenixFaction.AddRecruit`. `AddRecruit` is gated **only by container space** (`MaxCharacterSpace`), there is **no species gate** — a non-human can be added if there's room. Site = `PhoenixFaction.Bases.First().Site`.

### Soldier classification trick `[G]`
- `IsHuman` is **GameTag-driven**: `CheckIsHuman() => TacticalActorBaseDef.GameTags.Contains(HumanTag)`. That tag lives on the **shared** `TacticalActorBaseDef` component — mutating it would flip *every* Arthron, so we do NOT touch it.
- `GeoPhoenixFaction.Soldiers = Characters.Where(IsHuman)`; `GroundVehicles = Where(!IsHuman)`. The edit UI routes `IsHuman ? ShowHumanProgression : ShowVehicleProgression`, and `ShowHumanProgression` derefs `Progression.SkillPoints` / `LevelProgression.HasNewLevel` — which is why a non-human with a null progression crashed the edit screen.
- **Fix (two parts):** (a) give the clone a dedicated `ClassTagDef` whose `SpecializationDef` is registered, so `GenerateUnit` attaches a real runtime `Progression` → `GeoCharacter.LevelProgression` is non-null → edit screen no longer NREs; (b) a **marker-scoped** `CheckIsHuman` Postfix so ONLY this def is classified as a soldier.
- Append BOTH the class tag and the marker tag to the clone's **per-def `Data.GameTags`** (`.Append().Distinct().ToArray()`), then reset the cached `_classTags` field (reflection null) so the new ClassTag is seen.

### TFTV AlienTag rule `[T]`
Keep the clone's **AlienTag** → `IsAlien` stays true → TFTV `PersonalSpecModification` Prefix/Postfix stay gated off (gated on `!IsAlien`). This is what suppresses the BetterClasses popup. Do not strip AlienTag.

### null-`LevelProgression` fix `[G]`/`[T]`
Pandoran defs have a **null** `Data.LevelProgression`. TFTV's `GenerateUnit` prefix derefs `template.Data.LevelProgression.ShouldGeneratePersonalAbilities` → NRE → caught → popup. Fix: clone the Arthron def and set `clone.Data.LevelProgression = new LevelProgression(borrowed)`, where `borrowed` is the first valid `LevelProgressionDef` from any human soldier template (resolved at runtime by name, no hardcoded GUID).

### Tactical safety `[G]`
`find_referencing_symbols(CheckIsHuman)` shows all callers in the **Geoscape** namespace only; a pattern search over the entire `PhoenixPoint.Tactical` namespace for `CheckIsHuman`/`.IsHuman` returns **ZERO** hits. So the marker-scoped Postfix has no tactical-side effect.

### Stat formula & rebalance `[G]`
`ApplyStatOverrides(clone)` mutates **only** `clone.Data.Strength / .Will / .Speed` (per-def). MaxHP follows the shared game formula:

```
MaxHP = TacticalActorBaseDef.Toughness + Strength × EnduranceToHealthMultiplier(10)
```

- Arthron `Toughness = 120` (derived from observed Phase-1 raw values: Strength 100 → 1120 HP).
- Chosen consts: **Strength 20, Will 18, Speed 12** (in `ArthronMonster.cs`). So base recruit MaxHP = `120 + 20×10 = 320` (down from 1120) — a "heavy bruiser but fair" tanky alien soldier, not god-tier.
- Strength also governs carry weight / endurance; Will 18 = solid WP pool; Speed 12 = slightly below nimble humans (heavy unit).
- **`Toughness` and `EnduranceToHealthMultiplier` are on the SHARED `TacticalActorBaseDef`** — mutating them would flip *all* enemy Arthrons, so they are NOT touched. The lever is the clone's per-def `Strength/Will/Speed` only.

### Perk model `[G]`
Each custom perk is a self-contained `PassiveModifierAbilityDef` built by `PerkFactory.BuildStatPassive`:

- `PassiveModifierAbilityDef.StatModifications : ItemStatModification[]`; each `ItemStatModification` has `TargetStat` / `Modification` / `Value`.
- `StatModificationTarget` members used (with their enum values): `Endurance = 0`, `Armour = 8`, `BonusAttackDamage = 0x200`; `StatModificationType.Add`.
- `Endurance` raises MaxHP via the formula above (×10 per point): +5 Endurance = +50 MaxHP, etc.
- Slot 0 = a `ClassProficiencyAbilityDef` whose `ClassTags : GameTagsList` is `Merge`d (idempotent) with `HandgunItem_TagDef` + `PDWItem_TagDef` so the Arthron may also wield a human sidearm.
- Slots 2 & 5 first try a vanilla active by name (`ResolveByName`); only if unresolved do they fall back to a custom passive — so the track always has 7 non-null slots regardless of the loaded def DB.
- `CreateDef` throws on a duplicate GUID, so every builder is wrapped in a get-or-create guard for idempotency across reloads/saves.

## Build & deploy

```powershell
# build (uses the .NET SDK against net472 reference assemblies; no VS targeting pack needed)
cd TheTurned
$env:DOTNET_ROLL_FORWARD="LatestMajor"; dotnet build TheTurned.csproj -c Release
```

- Output → `Dist\TheTurned.dll` (+ `.pdb`, + `meta.json` copied).
- Deploy: copy `Dist\*` + `meta.json` + `Assets\` to `D:\Steam\steamapps\common\Phoenix Point\Mods\TheTurned\` (PowerShell: `copy Dist\* <Mods>\TheTurned\`).
- Build references the Officer ModSDK (`..\refs\Officer-src\ModSDK`: `0Harmony.dll`, `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`) and `UnityEngine.InputLegacyModule.dll` from the live game Managed folder.

## Arthron perk table — EXAMPLE monster (consts in `ArthronPerks.cs`)

> Arthron-scoped data. A different monster would ship its own table; the framework above is identical.

| Slot | Name | Stat mods |
| --- | --- | --- |
| 0 | Arthron Instincts (proficiency) | + Handgun/PDW item tags on the class proficiency |
| 1 | Natural Armour | Armour +10 |
| 2 | Acid Glands / Acid Spit | vanilla acid active by name, else BonusAttackDamage +15 |
| 3 | Chitin Plating | Armour +15, Endurance +5 (+50 MaxHP) |
| 4 | Crushing Claw | BonusAttackDamage +20 |
| 5 | Hardened Hide / Regeneration | vanilla regen active by name, else Endurance +8 (+80 MaxHP) |
| 6 | Apex Carapace (capstone) | Armour +20, Endurance +10 (+100 MaxHP), BonusAttackDamage +10 |

Progression costs scale 10→30 SkillPoints and 10→30 Mutagen toward the capstone. Fully leveled, the Endurance perks stack on the 320 base → roughly ~550 MaxHP plus heavy armour.

## How to add a new monster (zero-boilerplate)

The Core is monster-agnostic; adding a recruitable Pandoran needs **no Core changes** — a new folder plus one registry line.

1. **Create** `src\Monsters\<Name>\<Name>Monster.cs` and subclass `TurnedMonsterBase`.
2. **Pick stable, unique GUIDs** for the clone, class tag, spec, track, proficiency, the proficiency-progression, and the two ViewElementDef (VED) GUIDs. Invent fresh GUIDs — never reuse another monster's (a duplicate GUID makes `CreateDef` throw).
3. **Implement the monster-specific overrides:**
   - `Id`, `RecruitKey` (its `Ctrl+Shift+<key>`).
   - `ResolveTemplate(repo)` — locate the source enemy `TacCharacterDef` (filter `GetAllDefs<TacCharacterDef>()` by the monster's class tag, prefer a basic variant, order by name for determinism).
   - `ApplyStatOverrides(clone)` — set `clone.Data.Strength/Will/Speed` for the desired MaxHP (`Toughness + Strength×10`). Mutate the clone only, never the shared base def.
   - `BuildAbilityTrack(repo, proficiency)` — return 7 `AbilityTrackSlot`s. Slot 0 = the supplied proficiency; slots 1–6 = `PerkFactory.BuildStatPassive(...)` perks (and/or vanilla actives via `DefUtils.ResolveByName`). Keep a custom-passive fallback so every slot is non-null.
   - Display metadata + the loc keys (add rows to `Assets\Localization\TheTurned.csv`); optionally drop perk PNGs into `Assets\Textures\`.
4. **Register it:** add one line to `MonsterRegistry.RegisterDefaults()` — `Register(new <Name>Monster());`.
5. **Build & deploy.** Core builds the class, wires the hotkey, and the shared `CheckIsHuman` patch + the single `"TheTurned_RecruitTag"` marker handle soldier classification automatically.

## Planned: data-only monsters (per-monster JSON config)

Today a new monster still needs a small C# data class (the `ITurnedMonster` impl). The **planned** universalization path is an **external per-monster JSON config** so new monsters can be added by **data alone** — no compile. A draft `src\Core\MonsterConfig.cs` exists but is currently **DEFERRED**: untracked, unwired, and excluded from the build. Treat this as a roadmap item, not a shipped feature.

## 2026-06-13 — mission-deploy hang + edit-screen UI cleanup

All fixes below are **recruit-scoped** via the shared marker (`Tags.RecruitMarkerTag` / `Phase4.IsPhase4Recruit` on the geo side, `TacticalActorBase.GameTags.Contains(marker)` on the tactical side). None mutate the shared Crabman / enemy def, so they are framework-universal (any future monster carrying the marker inherits them).

### Voice-tag hang fix (tactical) `[G]`

- **Symptom:** mission deploy hangs at ~80% on the loading screen.
- **Cause:** the recruit is generated down the human `CharacterGenerator` path → it carries a baked, **mutually-exclusive** `VoiceProfileTagDef`. The engine's `TacticalActorRandomTags.OnActorEnteredPlay` (decompile `TacticalActorRandomTags.cs:17-34`) then rolls a SECOND voice tag, and `GameTagsList.AddImpl` (`GameTagsList.cs:105-128`, `ErrorOnExistingExclusive`) throws `InvalidOperationException` → the `TacticalLevelController.OnLevelStart` coroutine dies → load stalls.
- **Fix:** `src\Core\RandomTagsExclusiveGuard.cs` — a recruit-scoped Harmony **Prefix** that re-rolls in a mutual-exclusion-safe way (skips adding a rolled tag whose runtime `Type` already exists on the actor), gated on the tactical-actor marker tag. Marker-scoped, hardens against ANY pre-baked exclusive tag, and never touches the shared Crabman def.

### Edit-screen UI removals (geoscape, 4 items)

Root cause for all four: `HumanClassificationPatch` forces `CheckIsHuman == true`, so the recruit gets the **full human edit screen** plus the **Fatigue** mechanic — neither of which fits a turned monster. Each removal is recruit-gated.

1. **Stamina / Fatigue mechanic + widget** — `src\Core\RecruitFatiguePatch.cs` Prefix-skips `GeoCharacter.AddFaitgue` (vanilla typo) for the recruit → `Fatigue` stays null → `UIModuleCharacterProgression.cs:574/593` auto-hides `StaminaSlider` / `StaminaStatText`.
2. **Hide-helmet controls** (would pop the crab head out) — `src\Core\RecruitHelmetTogglePatch.cs` hides BOTH the native `UIModuleSoldierCustomization.HideHelmetToggle` (`:26`; via `OnNewCharacter` `:74` Postfix) AND the TFTV custom `Loadouts.HelmetToggle` (TFTV `Loadouts.cs:33`; via `UIModuleActorCycle.SetContextButtonsBasedOnType` Postfix ordered `HarmonyAfter("phoenixrising.tftv")`, reflection).
3. **Strip-all loadout button** (`EditUnitButtonsController.ToggleLoadoutButton`, TFTV-added, TFTV `Loadouts.cs:279`) — hidden for the recruit by extending `src\Core\AugmentButtonVisibilityPatch.cs` (its `SetContextButtonVisibility` postfix), reflection. Hiding it also removes a **crash** (strip empties armour → crab bodyparts deleted → NRE).
4. **Save-loadout button** (`EditUnitButtonsController.SaveLoadoutButton`, TFTV `Loadouts.cs:280`) — hidden in the same `AugmentButtonVisibilityPatch` postfix, reflection.

## Known issues / next steps

- **Acid Spit & Regeneration fallback:** slots 2 & 5 may use the custom-passive fallback if the vanilla def names (`Siren_SpitAcid_AbilityDef`, `Regeneration_AbilityDef`, …) don't resolve in the live def DB. Confirm the real names from a live def-dump.
- **Perk icons are placeholders:** the `Icons.cs` loader is wired but no per-perk art ships yet (Sniper icon shown as fallback).
- **In-game verification pending:** the final Phase-2 build could not be live-tested from the build host (needs a game restart). Follow the README "In-game test steps".
- **Cosmetic equip UI (residual):** mutate/bionics/equip geoscape screens treat the Arthron as human-eligible and render incompatible non-human bodyparts. No crash; needs a bodypart-slot guard or custom equip view later.
- **Spawn conditions:** the recruit is still a debug hotkey; a real trigger (mission reward / capture-and-turn / research gate) is future scope.

## See also

- Monorepo research note (full citations): `docs\research\theturned-arthron-recruit.md` — recruit chain (§2), hotkey (§3), null-`LevelProgression` fix (§6), soldier-classification trick (§7), gotchas (§8), and **Phase 2 — refactor + perks + rebalance (§9)**.
- Design spec: `docs\superpowers\specs\2026-06-09-theturned-arthron-recruit-design.md`.
