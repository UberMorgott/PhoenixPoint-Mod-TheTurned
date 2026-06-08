# The Turned

> Recruit a Pandoran **Arthron** into your Phoenix Point roster as a playable soldier, with a single hotkey. A developer MVP for Phoenix Point, standalone or alongside TFTV.

The Turned takes the game's own enemy **Arthron** (internal codename *Crabman*) and grants a copy of it to your Phoenix faction at runtime. The recruited Arthron is classified as a real **soldier** — it shows up in the personnel list and its edit/progression screen opens without errors. This is an early dev build: the recruit is triggered by a debug hotkey and the unit's ability track is a placeholder.

## Features

- **Hotkey recruit.** Press **Ctrl+Shift+T** on the geoscape to add one Arthron to your roster. Idempotent — re-pressing reuses the same cloned definition, no duplicates.
- **Arthron as a soldier class.** The recruited Arthron is classified as a playable soldier (not a vehicle), filed under the Soldiers list with its own class and a 7-slot ability track.
- **Edit screen works.** Opening the recruited Arthron's edit/progression screen no longer crashes — it has a real runtime progression.
- **TFTV-safe.** The Arthron keeps its alien tag, so TFTV's BetterClasses personal-spec logic stays gated off — no popup, no regression. Works standalone or with TFTV installed.

## Roadmap

Done:

- [x] Hotkey recruit of a Pandoran Arthron into the Phoenix roster
- [x] Arthron classified as a playable **soldier** (dedicated class + real progression)
- [x] Edit/progression screen opens without crashing

Planned:

- [ ] Real, Arthron-themed perks in the 7-slot ability track (currently placeholder vanilla abilities)
- [ ] Custom icons and proper localization
- [ ] A second recruitable unit (the "jellyfish-head" / Siren-class Pandoran)
- [ ] Real spawn conditions instead of a debug hotkey (mission reward / capture-and-turn / research gate)
- [ ] More recruitable Pandoran units

## Requirements

- **Phoenix Point** (base game) with the official mod system.
- Tested alongside **Terror From The Void (TFTV)** — compatible, no dependency.
- Harmony is bundled with the mod system; no separate install needed.

## Installation

Manual install:

1. Copy the `TheTurned` folder into your Phoenix Point `Mods` folder. For a Steam install this is usually `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. The final path should be `Phoenix Point\Mods\TheTurned\meta.json`.
2. Launch Phoenix Point and enable **The Turned** in the in-game mod manager.
3. On the geoscape, press **Ctrl+Shift+T** to recruit an Arthron.

## Building from source

Requires the .NET SDK and a Phoenix Point install (the project references the game's managed assemblies).

```powershell
# build the mod assembly in Release
$env:DOTNET_ROLL_FORWARD="LatestMajor"; dotnet build TheTurned.csproj -c Release
```

The build outputs to `Dist\`. To deploy, copy the contents of `Dist\` plus `meta.json` and the `Assets\` folder into `Phoenix Point\Mods\TheTurned\`.

## License

The Turned © 2026 Morgott. Licensed under [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): free to use and modify for non-commercial purposes with attribution.

## Credits

- Built by **Morgott**.
- Built with [Claude Code](https://claude.com/claude-code).
- Compatible with, but not dependent on, the **TFTV** overhaul by Voland163 and contributors.
- Phoenix Point © Snapshot Games.

---

## Русский

> Наймите пандоранского **Артрона** в ваш ростер Phoenix Point как играбельного бойца одной горячей клавишей. Девелоперский MVP для Phoenix Point, автономно или вместе с TFTV.

The Turned берёт игрового врага — **Артрона** (внутренний кодовый код *Crabman*) — и выдаёт его копию вашей фракции Phoenix во время игры. Нанятый Артрон классифицируется как настоящий **боец**: он появляется в списке персонала, а его экран редактирования/прогрессии открывается без ошибок. Это ранняя дев-сборка: найм запускается отладочной горячей клавишей, а ветка способностей юнита — заглушка.

### Возможности

- **Найм по горячей клавише.** Нажмите **Ctrl+Shift+T** на геоскейпе, чтобы добавить одного Артрона в ростер. Идемпотентно — повторное нажатие переиспользует тот же клонированный дефайн, без дубликатов.
- **Артрон как класс бойца.** Нанятый Артрон классифицируется как играбельный боец (а не техника), попадает в список бойцов со своим классом и веткой способностей на 7 слотов.
- **Экран редактирования работает.** Открытие экрана редактирования/прогрессии нанятого Артрона больше не вызывает краш — у него есть настоящая рантайм-прогрессия.
- **Безопасно для TFTV.** Артрон сохраняет свой тег чужого, поэтому логика персональных специализаций BetterClasses в TFTV остаётся выключенной — никакого попапа, никаких регрессий. Работает автономно или с установленным TFTV.

### Дорожная карта

Готово:

- [x] Найм пандоранского Артрона в ростер Phoenix по горячей клавише
- [x] Артрон классифицируется как играбельный **боец** (свой класс + настоящая прогрессия)
- [x] Экран редактирования/прогрессии открывается без краша

В планах:

- [ ] Настоящие тематические перки Артрона в ветке на 7 слотов (сейчас — заглушка из ванильных способностей)
- [ ] Кастомные иконки и полноценная локализация
- [ ] Второй наёмный юнит («голова-медуза» / класс Сирены)
- [ ] Настоящие условия появления вместо отладочной горячей клавиши (награда за миссию / захват-и-обращение / гейт по исследованию)
- [ ] Больше наёмных пандоранских юнитов

### Требования

- **Phoenix Point** (базовая игра) с официальной системой модов.
- Протестировано вместе с **Terror From The Void (TFTV)** — совместимо, без зависимости.
- Harmony поставляется с системой модов; отдельная установка не нужна.

### Установка

Ручная установка:

1. Скопируйте папку `TheTurned` в каталог `Mods` игры Phoenix Point. Для установки через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. Итоговый путь: `Phoenix Point\Mods\TheTurned\meta.json`.
2. Запустите Phoenix Point и включите **The Turned** во внутриигровом менеджере модов.
3. На геоскейпе нажмите **Ctrl+Shift+T**, чтобы нанять Артрона.

### Сборка из исходников

Требуется .NET SDK и установленная Phoenix Point (проект ссылается на управляемые сборки игры).

```powershell
# собрать сборку мода в Release
$env:DOTNET_ROLL_FORWARD="LatestMajor"; dotnet build TheTurned.csproj -c Release
```

Сборка выводится в `Dist\`. Для развёртывания скопируйте содержимое `Dist\` плюс `meta.json` и папку `Assets\` в `Phoenix Point\Mods\TheTurned\`.

### Лицензия

The Turned © 2026 Morgott. Лицензия [CC BY-NC 4.0](https://creativecommons.org/licenses/by-nc/4.0/): свободно использовать и изменять в некоммерческих целях с указанием авторства.

### Благодарности

- Создано **Morgott**.
- Сделано с [Claude Code](https://claude.com/claude-code).
- Совместимо, но не зависит от оверхола **TFTV** от Voland163 и контрибьюторов.
- Phoenix Point © Snapshot Games.
