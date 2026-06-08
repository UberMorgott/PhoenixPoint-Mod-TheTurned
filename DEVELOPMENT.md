# The Turned — Development Notes

Self-contained dev reference so this repo alone bootstraps a new session. The *fuller* grounded research (with decompile `file:line` citations for every claim) lives in the monorepo at `docs\research\theturned-arthron-recruit.md`; this file is the distilled, repo-local version.

`[G]` = base-game decompile, `[T]` = TFTV reference source, `[O]` = Officer mod (the `ModMain` template this mod mirrors).

---

## What the mod does

Grants a copy of the game's enemy **Arthron** (internal codename *Crabman*) to the Phoenix faction at runtime, classified as a playable **soldier**. Triggered by **Ctrl+Shift+T** on the geoscape. Standalone (`Dependencies: []`), TFTV-safe. Target framework **.NET Framework 4.7.2**; mod ID `Morgott.TheTurned`, v0.1.0.

## Architecture (5 source files)

- **`TheTurnedMain.cs`** — `ModMain` subclass (one per assembly), mirrors OfficerMain lifecycle. `OnModEnabled` runs three things in order: (1) `ArthronClass.EnsureCreated()`, (2) `HumanClassificationPatch.Apply(HarmonyInstance)`, (3) attach `RecruitHotkey` to `ModGO`. `CanSafelyDisable => false` (runtime grants can't be cleanly reverted). Class creation and the patch MUST happen in `OnModEnabled` (mod/game load) — before any geoscape — so `FactionCharacterGenerator.Start()` caches our spec.
- **`RecruitHotkey.cs`** — `MonoBehaviour` attached to `ModGO`; polls legacy `UnityEngine.Input` in `Update()`. Fires `ArthronRecruiter.RecruitOne()` on Ctrl+Shift+T. (PP is Unity 2019.4.x with the legacy input module active; `UnityEngine.InputLegacyModule.dll` is referenced from the live game Managed folder, not the Officer ModSDK.)
- **`ArthronRecruiter.cs`** — the recruit chain. Geoscape guard → resolve a basic Arthron `TacCharacterDef` by the `Crabman_ClassTagDef` tag (prefer non-Elite/non-Ultra, deterministic by name) → clone it (`GetOrCreateProgressedArthron`) → `GenerateUnit` → `SpawnAsCharacter` → grant via `GeoFactionReward.Apply` into a Phoenix base site. Idempotent: the clone uses a fixed invented GUID, so repeat presses reuse it.
- **`ArthronClass.cs`** — builds (idempotently) the dedicated class: a `ClassTagDef`, a marker `GameTagDef`, and a `SpecializationDef` cloned from the vanilla **Sniper** spec, carrying a 7-slot `AbilityTrackDef`. Slot 0 = a `ClassProficiencyAbilityDef` cloned from the Sniper proficiency (carries the new class tag); slots 1–6 = the Sniper track's own vanilla abilities (valid + localized placeholders). Registered into `SharedData.SharedGameTags.Specializations` so the generator discovers it. NOT granted to the faction, so it doesn't pollute normal soldier class-selection.
- **`HumanClassificationPatch.cs`** — a Harmony **Postfix on `TacCharacterDef.CheckIsHuman`**, scoped to the marker tag: `__result = true` only when the def's `Data.GameTags` contains our marker. `IsHuman => CheckIsHuman()`, so one Postfix covers every read path.

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

## Build & deploy

```powershell
# build (uses the .NET SDK against net472 reference assemblies; no VS targeting pack needed)
$env:DOTNET_ROLL_FORWARD="LatestMajor"; dotnet build TheTurned.csproj -c Release
```

- Output → `Dist\TheTurned.dll` (+ `.pdb`, + `meta.json` copied).
- Deploy: copy `Dist\*` + `meta.json` + `Assets\` to `D:\Steam\steamapps\common\Phoenix Point\Mods\TheTurned\`.
- Build references the Officer ModSDK (`..\refs\Officer-src\ModSDK`: `0Harmony.dll`, `Assembly-CSharp.dll`, `UnityEngine.CoreModule.dll`) and `UnityEngine.InputLegacyModule.dll` from the live game Managed folder.

## Known issues / Phase-2 TODO

- **Ability track placeholders:** slots 1–6 reuse vanilla Sniper abilities. Replace with real Arthron-themed perks.
- **Icons + localization:** ViewElementDefs currently use literal `LocalizedTextBind(text, doNotLocalize:true)`. Move to a real loc CSV + custom icons under `Assets\Textures\`.
- **Second unit:** the "jellyfish-head" (Siren-class) Pandoran, via the same recipe with the Siren class tag.
- **Spawn conditions:** replace the debug hotkey with a real trigger (mission reward / capture-and-turn / research gate).
- **Cosmetic equip UI (residual):** mutate/bionics/equip geoscape screens now treat the Arthron as human-eligible and render incompatible non-human bodyparts. No crash; needs a bodypart-slot guard or custom equip view later.

## See also

- Monorepo research note (full citations): `docs\research\theturned-arthron-recruit.md` — recruit chain (§2), hotkey (§3), null-`LevelProgression` fix (§6), soldier-classification trick (§7), gotchas + Phase-2 (§8).
- Design spec: `docs\superpowers\specs\2026-06-09-theturned-arthron-recruit-design.md`.
