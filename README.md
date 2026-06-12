# The Turned

> A modular, monster-agnostic framework for recruiting **turned Pandorans** into your Phoenix Point roster as playable soldiers. Ships one monster today — the **Arthron** (internal codename *Crabman*) — with a soldier-style "cell progression" evolution track. A developer/test build for Phoenix Point, built on **TFTV**. Version **0.3.0**.

The Turned is, first and foremost, an **engine**. A generic `Core` layer does all the heavy lifting — clone an enemy def at runtime, classify it as a real **soldier**, give it a working progression — while each monster lives in its own folder under `src\Monsters\` and supplies only its monster-specific data through one interface (`ITurnedMonster`). Adding a new recruitable Pandoran is a **new folder + one line** in `MonsterRegistry.RegisterDefaults()`; no `Core` changes.

Today the framework registers **exactly one** monster: the **Arthron** (Crabman). The recruited Arthron is a true **soldier** — it appears in the personnel list, its edit/progression screen opens without errors, and it has a working soldier-style evolution track. This is a **developer/test build**: recruit and progression are driven by developer hotkeys, and perk/cell icons are placeholders (art pending).

## Features

- **Modular recruit framework.** A monster-agnostic `Core` (clone → soldier-classify → real progression → faction-reward grant) plus per-monster defs. New monsters need no `Core` changes.
- **Arthron as a soldier class.** The recruited Arthron is classified as a playable soldier (not a vehicle), filed under the Soldiers list with its own class.
- **Cell-progression evolution.** A soldier-style evolution track replaces the old static perk tree: a 5-cell top evolution row that unlocks by character level (earned via mission XP) and is purchased with SkillPoints, with prerequisites and respec.
- **Visible armor evolution.** Armor cells swap **real Crabman bodypart defs** (legs / torso shell / carapace back-plate) for a visible "armored" read on the model.
- **Tunable stat growth.** Stats grow via tunable `PassiveModifierAbilityDef` passives (Endurance → MaxHP, plus Willpower / Speed / bonus damage).
- **Balanced base.** Strength 20 / Will 18 / Speed 12, ~320 base MaxHP (down from the ~1120 of a wild Arthron) via the game formula `MaxHP = Toughness + Strength × EnduranceToHealthMultiplier`. Tanky but fair.
- **Edit screen works.** Opening the recruited Arthron's edit/progression screen no longer crashes — it has a real runtime progression.
- **TFTV-safe.** The Arthron keeps its alien tag, so TFTV's BetterClasses personal-spec logic stays gated off — no popup, no regression.

## Cell progression (Arthron)

The progression panel's **top evolution row** has **5 cells**. The row progresses soldier-style: cell *N* unlocks at **character level *N*** (earned by mission XP), then the player clicks the cell and **pays SkillPoints** to apply it. Respec is supported.

| # | Cell | Unlock | Cost | Prereq | Effect |
| --- | --- | --- | --- | --- | --- |
| 1 | **Mutations** | Level 1 | Free | none | Navigation cell — opens the existing augment / Bionics screen. |
| 2 | **First armor layer** | Level 2 | SkillPoints | none | First visible armor: armored legs + carapace back-plate (real Crabman bodypart defs). |
| 3 | **Stats Basic→Alpha** | Level 3 | SkillPoints | none | Stat passive: +Endurance (→ +MaxHP), +Willpower, +Speed, + small bonus damage (tunable). |
| 4 | **Max armor + weapons evolve** | Level 4 | SkillPoints | cell 2 | Full visible armor: elite legs + elite torso shell + elite carapace plate. **All equipped weapons evolve to their elite form (model + stats)** — left arm, right arm, and head. |
| 5 | **Stats Alpha→Prime** | Level 5 | SkillPoints | cell 3 | A second, cumulative stat passive on top of Alpha (tunable). |

The legs progress as a visual ladder (light → light-armored → heavy → heavy-armored) and exactly one cell unlocks per character level. The head augment is **manual-only** (not auto-evolved). This evolution track is **in-game verified** (no per-frame model flicker).

A **bottom Mutagen row** is designed but **deferred** (not in this build). Exact stat deltas for Alpha / Prime are tunable defaults, to be set in-game.

## Dev / test hotkeys

This build drives recruit and progression through **developer hotkeys**. All require **Ctrl+Shift** held, on the geoscape:

- **Ctrl+Shift+T** — recruit / spawn the Arthron onto the Phoenix faction.
- **Ctrl+Shift+U** — dev level-up: +1 character level to the first recruit (drives the real level-gated cell unlocks).
- **Ctrl+Shift+Y** — dev: cycle the recruit through armor loadouts A / B (render proof).

These are **temporary** developer hotkeys. Real spawn / unlock conditions (mission reward / capture-and-turn / research gate) are future scope. `DevUnlockAllLevels` is **OFF**, so the level gate is genuinely testable.

## Roadmap

Done:

- [x] Modular `Core` + per-monster framework (zero-boilerplate new monsters)
- [x] Hotkey recruit of a Pandoran Arthron into the Phoenix roster
- [x] Arthron classified as a playable **soldier** (dedicated class + real progression)
- [x] Edit/progression screen opens without crashing
- [x] Soldier-style **cell-progression** / evolution track + augment screen + visible armor swap
- [x] Stat rebalance (~320 base HP; Str 20 / Will 18 / Speed 12)

Planned:

- [ ] More recruitable monsters on the framework (e.g. a Siren-class Pandoran)
- [ ] Real (non-hotkey) spawn / unlock conditions (mission reward / capture-and-turn / research gate)
- [ ] The bottom **Mutagen** evolution row (designed, deferred)
- [ ] Final perk / cell icons and art (placeholders today; loader is wired)

## Requirements

- **Phoenix Point** (base game) with the official mod system.
- **Terror From The Void (TFTV)** — **required**. `meta.json` declares a hard dependency on `phoenixrising.tftv`, and TFTV is the supported configuration. (Phase-4 features fall back to a fixed track if TFTV is absent, but that is not the supported path.)
- Harmony is bundled with the mod system; no separate install needed.

## Installation

Manual install:

1. Copy the `TheTurned` folder into your Phoenix Point `Mods` folder. For a Steam install this is usually `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. The final path should be `Phoenix Point\Mods\TheTurned\meta.json`. The folder must contain `TheTurned.dll`, `TheTurned.pdb`, `meta.json`, and the `Assets\` folder.
2. Make sure **TFTV** is installed and enabled (it is a hard dependency).
3. Launch Phoenix Point and enable **The Turned** in the in-game mod manager.
4. On the geoscape, press **Ctrl+Shift+T** to recruit an Arthron.

## In-game test steps

After a build, verify the mod end-to-end:

1. **Close** Phoenix Point if it is running.
2. **Deploy:** copy `TheTurned.dll`, `TheTurned.pdb`, `meta.json`, and the `Assets\` folder into `…\Phoenix Point\Mods\TheTurned\`.
3. **Launch** Phoenix Point with **TFTV enabled** and enable **The Turned**.
4. **Load** a geoscape save that has at least one Phoenix base with roster space.
5. Press **Ctrl+Shift+T** to recruit the Arthron.
6. Open the **Personnel / soldier edit** screen and confirm:
   - The Arthron appears in the **Soldiers** list (not vehicles).
   - The top evolution row shows the **5 cells**.
   - Base health is roughly **~320 HP**.
   - **No TFTV BetterClasses popup** appears.
7. Press **Ctrl+Shift+U** a few times to level the recruit, and confirm cells unlock one per character level (cell *N* at level *N*).
8. Click an unlocked cell, pay SkillPoints, and confirm the effect applies — armor cells swap visible bodyparts, stat cells raise MaxHP / stats. Use **Ctrl+Shift+Y** to verify the armor render swap.

## Building from source

Requires the .NET SDK and a Phoenix Point install (the project references the game's managed assemblies). Target framework **.NET Framework 4.7.2**.

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
- Built on the **TFTV** overhaul by Voland163 and contributors (hard dependency).
- Phoenix Point © Snapshot Games.

---

## Русский

> Модульный, не привязанный к конкретному монстру фреймворк для найма **обращённых пандоранцев** в ваш ростер Phoenix Point как играбельных бойцов. Сегодня поставляется один монстр — **Артрон** (внутренний кодовый код *Crabman*) — с прогрессией бойца в стиле «клеточной эволюции». Девелоперская/тестовая сборка для Phoenix Point, построенная на **TFTV**. Версия **0.3.0**.

The Turned — это прежде всего **движок**. Универсальный слой `Core` делает всю основную работу — клонирует деф врага во время игры, классифицирует его как настоящего **бойца**, выдаёт ему рабочую прогрессию — тогда как каждый монстр живёт в своей папке под `src\Monsters\` и поставляет лишь свои специфичные данные через один интерфейс (`ITurnedMonster`). Добавить нового наёмного пандоранца — это **новая папка + одна строка** в `MonsterRegistry.RegisterDefaults()`; без изменений `Core`.

Сегодня фреймворк регистрирует **ровно один** монстр: **Артрон** (Crabman). Нанятый Артрон — настоящий **боец**: появляется в списке персонала, экран редактирования/прогрессии открывается без ошибок, и у него есть рабочая прогрессия в стиле бойца. Это **девелоперская/тестовая сборка**: найм и прогрессия запускаются девелоперскими горячими клавишами, а иконки перков/клеток — заглушки (арт в разработке).

### Возможности

- **Модульный фреймворк найма.** Не привязанный к монстру `Core` (клон → классификация как боец → настоящая прогрессия → выдача через награду фракции) плюс дефы по монстрам. Новые монстры не требуют изменений `Core`.
- **Артрон как класс бойца.** Нанятый Артрон классифицируется как играбельный боец (а не техника), попадает в список бойцов со своим классом.
- **Клеточная эволюция-прогрессия.** Прогрессия в стиле бойца заменяет старое статичное древо перков: верхний ряд эволюции из 5 клеток, открывающихся по уровню персонажа (зарабатывается опытом за миссии) и покупаемых за очки навыка, с предусловиями и переспеком.
- **Видимая эволюция брони.** Клетки брони подменяют **настоящие дефы частей тела Crabman** (ноги / панцирь торса / задняя пластина-карапакс) ради видимого «бронированного» облика модели.
- **Настраиваемый рост характеристик.** Характеристики растут через настраиваемые пассивки `PassiveModifierAbilityDef` (Выносливость → MaxHP, плюс Воля / Скорость / бонусный урон).
- **Сбалансированная база.** Сила 20 / Воля 18 / Скорость 12, ~320 базового MaxHP (против ~1120 у дикого Артрона) по игровой формуле `MaxHP = Toughness + Strength × EnduranceToHealthMultiplier`. Живучий, но честный.
- **Экран редактирования работает.** Открытие экрана редактирования/прогрессии нанятого Артрона больше не вызывает краш — у него есть настоящая рантайм-прогрессия.
- **Безопасно для TFTV.** Артрон сохраняет свой тег чужого, поэтому логика персональных специализаций BetterClasses в TFTV остаётся выключенной — никакого попапа, никаких регрессий.

### Клеточная прогрессия (Артрон)

Верхний ряд эволюции на панели прогрессии содержит **5 клеток**. Ряд прогрессирует в стиле бойца: клетка *N* открывается на **уровне персонажа *N*** (зарабатывается опытом за миссии), затем игрок кликает по клетке и **платит очки навыка**, чтобы её применить. Переспек поддерживается.

| # | Клетка | Открытие | Стоимость | Предусловие | Эффект |
| --- | --- | --- | --- | --- | --- |
| 1 | **Мутации** | Ур. 1 | Бесплатно | нет | Навигационная клетка — открывает существующий экран аугментаций / Bionics. |
| 2 | **Первый слой брони** | Ур. 2 | Очки навыка | нет | Первая видимая броня: бронированные ноги + задняя пластина-карапакс (настоящие дефы частей тела Crabman). |
| 3 | **Характеристики Basic→Alpha** | Ур. 3 | Очки навыка | нет | Пассивка характеристик: +Выносливость (→ +MaxHP), +Воля, +Скорость, + небольшой бонусный урон (настраиваемо). |
| 4 | **Максимальная броня + эволюция оружия** | Ур. 4 | Очки навыка | клетка 2 | Полная видимая броня: элитные ноги + элитный панцирь торса + элитная пластина-карапакс. **Всё надетое оружие эволюционирует в элитную форму (модель + характеристики)** — левая рука, правая рука и голова. |
| 5 | **Характеристики Alpha→Prime** | Ур. 5 | Очки навыка | клетка 3 | Вторая, кумулятивная пассивка характеристик поверх Alpha (настраиваемо). |

Ноги прогрессируют как визуальная лесенка (лёгкие → легко-бронированные → тяжёлые → тяжело-бронированные), и ровно одна клетка открывается за уровень персонажа. Аугментация головы — **только вручную** (не авто-эволюция). Этот ряд эволюции **проверен в игре** (без покадрового мерцания модели).

**Нижний ряд Мутагена** спроектирован, но **отложен** (нет в этой сборке). Точные дельты характеристик для Alpha / Prime — настраиваемые значения по умолчанию, задаются в игре.

### Дев / тест горячие клавиши

Эта сборка управляет наймом и прогрессией через **девелоперские горячие клавиши**. Все требуют удержания **Ctrl+Shift**, на геоскейпе:

- **Ctrl+Shift+T** — нанять / заспавнить Артрона во фракцию Phoenix.
- **Ctrl+Shift+U** — дев-левелап: +1 уровень персонажа первому рекруту (запускает настоящие открытия клеток по уровню).
- **Ctrl+Shift+Y** — дев: прокрутить рекрута по лоадаутам брони A / B (пруф рендера).

Это **временные** девелоперские горячие клавиши. Настоящие условия появления / открытия (награда за миссию / захват-и-обращение / гейт по исследованию) — будущая работа. `DevUnlockAllLevels` **ВЫКЛЮЧЕН**, поэтому гейт по уровню действительно тестируемый.

### Дорожная карта

Готово:

- [x] Модульный фреймворк `Core` + по-монстрам (новый монстр без шаблонного кода)
- [x] Найм пандоранского Артрона в ростер Phoenix по горячей клавише
- [x] Артрон классифицируется как играбельный **боец** (свой класс + настоящая прогрессия)
- [x] Экран редактирования/прогрессии открывается без краша
- [x] Прогрессия в стиле бойца — **клеточная эволюция** + экран аугментаций + видимая смена брони
- [x] Ребаланс характеристик (~320 базового HP; Сила 20 / Воля 18 / Скорость 12)

В планах:

- [ ] Больше наёмных монстров на фреймворке (напр. класс Сирены)
- [ ] Настоящие (не по горячей клавише) условия появления / открытия (награда за миссию / захват-и-обращение / гейт по исследованию)
- [ ] Нижний ряд эволюции **Мутаген** (спроектирован, отложен)
- [ ] Финальные иконки перков / клеток и арт (сейчас заглушки; загрузчик подключён)

### Требования

- **Phoenix Point** (базовая игра) с официальной системой модов.
- **Terror From The Void (TFTV)** — **обязателен**. `meta.json` объявляет жёсткую зависимость от `phoenixrising.tftv`, и TFTV — поддерживаемая конфигурация. (Функции Phase-4 откатываются на фиксированную ветку, если TFTV отсутствует, но это не поддерживаемый путь.)
- Harmony поставляется с системой модов; отдельная установка не нужна.

### Установка

Ручная установка:

1. Скопируйте папку `TheTurned` в каталог `Mods` игры Phoenix Point. Для установки через Steam это обычно `…\steamapps\common\Phoenix Point\Mods\TheTurned\`. Итоговый путь: `Phoenix Point\Mods\TheTurned\meta.json`. Папка должна содержать `TheTurned.dll`, `TheTurned.pdb`, `meta.json` и папку `Assets\`.
2. Убедитесь, что **TFTV** установлен и включён (это жёсткая зависимость).
3. Запустите Phoenix Point и включите **The Turned** во внутриигровом менеджере модов.
4. На геоскейпе нажмите **Ctrl+Shift+T**, чтобы нанять Артрона.

### Шаги внутриигровой проверки

После сборки проверьте мод от начала до конца:

1. **Закройте** Phoenix Point, если запущен.
2. **Разверните:** скопируйте `TheTurned.dll`, `TheTurned.pdb`, `meta.json` и папку `Assets\` в `…\Phoenix Point\Mods\TheTurned\`.
3. **Запустите** Phoenix Point с **включённым TFTV** и включите **The Turned**.
4. **Загрузите** сейв геоскейпа с хотя бы одной базой Phoenix со свободным местом в ростере.
5. Нажмите **Ctrl+Shift+T**, чтобы нанять Артрона.
6. Откройте экран **персонала / редактирования бойца** и убедитесь:
   - Артрон в списке **бойцов** (не техники).
   - Верхний ряд эволюции показывает **5 клеток**.
   - Базовое здоровье примерно **~320 HP**.
   - **Попап TFTV BetterClasses не появляется**.
7. Нажмите **Ctrl+Shift+U** несколько раз, чтобы повысить уровень рекрута, и убедитесь, что клетки открываются по одной на уровень персонажа (клетка *N* на уровне *N*).
8. Кликните по открытой клетке, заплатите очки навыка и убедитесь, что эффект применяется — клетки брони подменяют видимые части тела, клетки характеристик повышают MaxHP / характеристики. Используйте **Ctrl+Shift+Y** для проверки смены рендера брони.

### Сборка из исходников

Требуется .NET SDK и установленная Phoenix Point (проект ссылается на управляемые сборки игры). Целевой фреймворк **.NET Framework 4.7.2**.

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
- Построено на оверхоле **TFTV** от Voland163 и контрибьюторов (жёсткая зависимость).
- Phoenix Point © Snapshot Games.
