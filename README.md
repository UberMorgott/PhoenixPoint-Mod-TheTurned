# The Turned

> Recruit a Pandoran **Arthron** into your Phoenix Point roster as a playable soldier, with a single hotkey. A developer build for Phoenix Point, standalone or alongside TFTV. Version **0.2.0**.

The Turned takes the game's own enemy **Arthron** (internal codename *Crabman*) and grants a copy of it to your Phoenix faction at runtime. The recruited Arthron is classified as a real **soldier** — it shows up in the personnel list and its edit/progression screen opens without errors — and it now has a real, Arthron-themed perk tree and balanced stats. This is still a dev build: the recruit is triggered by a debug hotkey.

## Features

- **Hotkey recruit.** Press **Ctrl+Shift+T** on the geoscape to add one Arthron to your roster. Idempotent — re-pressing reuses the same cloned definition, no duplicates.
- **Arthron as a soldier class.** The recruited Arthron is classified as a playable soldier (not a vehicle), filed under the Soldiers list with its own class and a 7-slot ability track.
- **Real Arthron perks.** The 7-slot track is now a themed "heavy bruiser" tree (chitin armour, crushing claws, acid) instead of placeholder Sniper abilities — see the table below.
- **Balanced stats.** Strength 20 / Will 18 / Speed 12, ~320 base MaxHP (down from the ~1120 of a wild Arthron). Tanky but fair; HP and armour grow through the perk tree to roughly ~550 MaxHP plus heavy armour when fully leveled.
- **Edit screen works.** Opening the recruited Arthron's edit/progression screen no longer crashes — it has a real runtime progression.
- **TFTV-safe.** The Arthron keeps its alien tag, so TFTV's BetterClasses personal-spec logic stays gated off — no popup, no regression. Works standalone or with TFTV installed.

## Arthron class & perk tree

| Slot | Name | Effect |
| --- | --- | --- |
| 0 | Arthron Instincts (proficiency) | Class identity; can also wield human Handguns and PDWs (claws/viral-gun are innate). |
| 1 | Natural Armour | +10 Armour |
| 2 | Acid Glands / Acid Spit | Vanilla acid ability when available; else +15 bonus attack damage |
| 3 | Chitin Plating | +15 Armour, +5 Endurance (+50 MaxHP) |
| 4 | Crushing Claw | +20 bonus attack damage |
| 5 | Hardened Hide / Regeneration | Vanilla regen ability when available; else +8 Endurance (+80 MaxHP) |
| 6 | Apex Carapace (capstone) | +20 Armour, +10 Endurance (+100 MaxHP), +10 bonus attack damage |

Progression costs scale 10→30 SkillPoints and 10→30 Mutagen toward the capstone.

## Roadmap

Done:

- [x] Hotkey recruit of a Pandoran Arthron into the Phoenix roster
- [x] Arthron classified as a playable **soldier** (dedicated class + real progression)
- [x] Edit/progression screen opens without crashing
- [x] Real, Arthron-themed 7-slot perk tree
- [x] Stat rebalance (~320 base HP; Str 20 / Will 18 / Speed 12)
- [x] Reusable Core + per-monster framework (zero-boilerplate new monsters)

Planned:

- [ ] Final perk icons (placeholders today; loader is wired)
- [ ] Confirm the vanilla Acid Spit / Regeneration def names from a live def-dump
- [ ] A second recruitable unit (e.g. the "jellyfish-head" / Siren-class Pandoran) using the new framework
- [ ] Real spawn conditions instead of a debug hotkey (mission reward / capture-and-turn / research gate)

## Requirements

- **Phoenix Point** (base game) with the official mod system.
- Tested alongside **Terror From The Void (TFTV)** — compatible, no dependency.
- Harmony is bundled with the mod system; no separate install needed.

## Installation

Manual install:

1. Copy the `TheTurned` folder into your Phoenix Point `Mods` folder. For a Steam install this is usually `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. The final path should be `Phoenix Point\Mods\TheTurned\meta.json`. The folder must contain `TheTurned.dll`, `TheTurned.pdb`, `meta.json`, and the `Assets\` folder.
2. Launch Phoenix Point and enable **The Turned** in the in-game mod manager.
3. On the geoscape, press **Ctrl+Shift+T** to recruit an Arthron.

## In-game test steps

After a build, verify the mod end-to-end:

1. **Close** Phoenix Point if it is running.
2. **Deploy:** copy `TheTurned.dll`, `TheTurned.pdb`, `meta.json`, and the `Assets\` folder into `…\Phoenix Point\Mods\TheTurned\`.
3. **Launch** Phoenix Point (with TFTV enabled, for the full path) and enable **The Turned**.
4. **Load** a geoscape save that has at least one Phoenix base with roster space.
5. Press **Ctrl+Shift+T**.
6. Open the **Personnel / soldier edit** screen.
7. **Confirm:**
   - The Arthron appears in the **Soldiers** list (not vehicles).
   - It shows the **7 named perks** (Arthron Instincts, Natural Armour, Acid Glands/Spit, Chitin Plating, Crushing Claw, Hardened Hide/Regeneration, Apex Carapace).
   - Base health is roughly **~320 HP**.
   - **No TFTV BetterClasses popup** appears.
8. **Level up** the Arthron and confirm armour / MaxHP grow as the chitin and Endurance perks are unlocked.

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

> Наймите пандоранского **Артрона** в ваш ростер Phoenix Point как играбельного бойца одной горячей клавишей. Девелоперская сборка для Phoenix Point, автономно или вместе с TFTV. Версия **0.2.0**.

The Turned берёт игрового врага — **Артрона** (внутренний кодовый код *Crabman*) — и выдаёт его копию вашей фракции Phoenix во время игры. Нанятый Артрон классифицируется как настоящий **боец** (появляется в списке персонала, экран редактирования/прогрессии открывается без ошибок) и теперь имеет настоящее тематическое древо перков и сбалансированные характеристики. Это всё ещё дев-сборка: найм запускается отладочной горячей клавишей.

### Возможности

- **Найм по горячей клавише.** Нажмите **Ctrl+Shift+T** на геоскейпе, чтобы добавить одного Артрона в ростер. Идемпотентно — повторное нажатие переиспользует тот же клонированный дефайн, без дубликатов.
- **Артрон как класс бойца.** Нанятый Артрон классифицируется как играбельный боец (а не техника), попадает в список бойцов со своим классом и веткой способностей на 7 слотов.
- **Настоящие перки Артрона.** Ветка на 7 слотов теперь — тематическое древо «тяжёлого бойца» (хитиновая броня, дробящие клешни, кислота) вместо заглушек из способностей Снайпера — см. таблицу ниже.
- **Сбалансированные характеристики.** Сила 20 / Воля 18 / Скорость 12, ~320 базового MaxHP (против ~1120 у дикого Артрона). Живучий, но честный; HP и броня растут по древу перков примерно до ~550 MaxHP плюс тяжёлая броня при полной прокачке.
- **Экран редактирования работает.** Открытие экрана редактирования/прогрессии нанятого Артрона больше не вызывает краш — у него есть настоящая рантайм-прогрессия.
- **Безопасно для TFTV.** Артрон сохраняет свой тег чужого, поэтому логика персональных специализаций BetterClasses в TFTV остаётся выключенной — никакого попапа, никаких регрессий. Работает автономно или с установленным TFTV.

### Класс Артрона и древо перков

| Слот | Название | Эффект |
| --- | --- | --- |
| 0 | Инстинкты Артрона (проф.) | Идентичность класса; может также носить человеческие пистолеты и PDW (клешни/виро-пушка — врождённые). |
| 1 | Природная броня | +10 брони |
| 2 | Кислотные железы / Плевок кислотой | Ванильная кислотная способность при наличии; иначе +15 к бонусному урону |
| 3 | Хитиновые пластины | +15 брони, +5 выносливости (+50 MaxHP) |
| 4 | Дробящая клешня | +20 к бонусному урону атаки |
| 5 | Закалённая шкура / Регенерация | Ванильная регенерация при наличии; иначе +8 выносливости (+80 MaxHP) |
| 6 | Высший панцирь (капстоун) | +20 брони, +10 выносливости (+100 MaxHP), +10 к бонусному урону |

Стоимость прокачки растёт 10→30 очков навыка и 10→30 мутагена к капстоуну.

### Дорожная карта

Готово:

- [x] Найм пандоранского Артрона в ростер Phoenix по горячей клавише
- [x] Артрон классифицируется как играбельный **боец** (свой класс + настоящая прогрессия)
- [x] Экран редактирования/прогрессии открывается без краша
- [x] Настоящее тематическое древо перков на 7 слотов
- [x] Ребаланс характеристик (~320 базового HP; Сила 20 / Воля 18 / Скорость 12)
- [x] Переиспользуемый фреймворк Core + по-монстрам (новый монстр без шаблонного кода)

В планах:

- [ ] Финальные иконки перков (сейчас заглушки; загрузчик подключён)
- [ ] Подтвердить ванильные имена дефов Плевка кислотой / Регенерации из живого дампа дефов
- [ ] Второй наёмный юнит (напр. «голова-медуза» / класс Сирены) на новом фреймворке
- [ ] Настоящие условия появления вместо отладочной горячей клавиши (награда за миссию / захват-и-обращение / гейт по исследованию)

### Требования

- **Phoenix Point** (базовая игра) с официальной системой модов.
- Протестировано вместе с **Terror From The Void (TFTV)** — совместимо, без зависимости.
- Harmony поставляется с системой модов; отдельная установка не нужна.

### Установка

Ручная установка:

1. Скопируйте папку `TheTurned` в каталог `Mods` игры Phoenix Point. Для установки через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. Итоговый путь: `Phoenix Point\Mods\TheTurned\meta.json`. Папка должна содержать `TheTurned.dll`, `TheTurned.pdb`, `meta.json` и папку `Assets\`.
2. Запустите Phoenix Point и включите **The Turned** во внутриигровом менеджере модов.
3. На геоскейпе нажмите **Ctrl+Shift+T**, чтобы нанять Артрона.

### Шаги внутриигровой проверки

После сборки проверьте мод от начала до конца:

1. **Закройте** Phoenix Point, если запущен.
2. **Разверните:** скопируйте `TheTurned.dll`, `TheTurned.pdb`, `meta.json` и папку `Assets\` в `…\Phoenix Point\Mods\TheTurned\`.
3. **Запустите** Phoenix Point (с включённым TFTV, для полного пути) и включите **The Turned**.
4. **Загрузите** сейв геоскейпа с хотя бы одной базой Phoenix со свободным местом в ростере.
5. Нажмите **Ctrl+Shift+T**.
6. Откройте экран **персонала / редактирования бойца**.
7. **Убедитесь:**
   - Артрон в списке **бойцов** (не техники).
   - Видны **7 названных перков** (Инстинкты Артрона, Природная броня, Кислотные железы/Плевок, Хитиновые пластины, Дробящая клешня, Закалённая шкура/Регенерация, Высший панцирь).
   - Базовое здоровье примерно **~320 HP**.
   - **Попап TFTV BetterClasses не появляется**.
8. **Прокачайте** Артрона и убедитесь, что броня / MaxHP растут по мере открытия хитиновых перков и перков выносливости.

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
