# Rise of Summoner

> **Plants vs. Zombies Fusion** — reimagined as a summoner-led lawn defense RPG.

We retired the generic **Fusion RPG** name. It described the mod accurately but read like every other stat-overlay project. **Rise of Summoner** names the core loop we are building next: command summoned allies, grow power across runs, and hold the line while the horde advances. The four-module stack below is live today; the summoner fantasy lands on top of it.

| Module | Role |
|---|---|
| **Launcher** | WPF player entry — install loader, pick port, start game + server |
| **Injector** | BepInEx / MelonLoader Harmony hooks inside PVZ Fusion |
| **Server** | Independent RPG backend (SQLite, REST, SignalR) |
| **Web** | Browser control room — lawn, cheats, stats, progression, storage |

[![License: AGPL v3+](https://img.shields.io/badge/License-AGPL%20v3%2B-blue.svg)](LICENSE)
[![CI](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/actions/workflows/ci.yml/badge.svg)](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/actions/workflows/ci.yml)
[![Release](https://img.shields.io/github/v/release/letuhao/plant-vs-zombie-rise-of-summoner?include_prereleases)](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/releases)

**License:** [AGPL-3.0-or-later](LICENSE). Windows-only. You need a **legal** PVZ Fusion install — FusionRpg never downloads or patches the game binary.

## Understand the system in 15 minutes

| # | Read | You will learn |
|---|---|---|
| 1 | [docs/architecture/software-architecture.md](docs/architecture/software-architecture.md) | The whole system on one page: modules, hot path, invariants, control loops, FSMs, protocol, build |
| 2 | [docs/architecture/data-architecture.md](docs/architecture/data-architecture.md) | Every database/table, who is source of truth, hot → cold lifecycle, DAL boundary |
| 3 | [docs/architecture/overview.md](docs/architecture/overview.md) | The original four-module v1 scope and boundaries |
| 4 | [docs/architecture/data-flow.md](docs/architecture/data-flow.md) | Sequence diagrams: web → game stat change, game → web event capture |
| 5 | [docs/README.md](docs/README.md) | Full documentation map (research, per-subsystem SSOT docs, protocol, runbooks) |

Then per subsystem (deep dives): [stat-system](docs/architecture/stat-system.md) · [actor-hub](docs/architecture/actor-hub-ssot.md) · [status](docs/architecture/status-ssot.md) · [element-hub](docs/architecture/element-hub-ssot.md) · [combat-damage](docs/architecture/combat-damage-ssot.md) · [effect-system](docs/architecture/effect-system.md) · [effect-funnel](docs/architecture/effect-funnel.md) · [match-runtime](docs/architecture/match-runtime.md) · [unique-actor](docs/architecture/unique-actor-runtime.md) · [pvz-middle-layer](docs/architecture/pvz-middle-layer.md) · [rpg-progression](docs/architecture/rpg-progression.md) · [lawn-projector](docs/architecture/lawn-projector.md) · [decisions](docs/architecture/decisions.md)

## Players

1. Download `FusionRpg-win-x64.zip` from [Releases](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/releases).
2. Unzip → double-click `FusionRpg.Launcher.exe`.
3. **Browse** to your game folder → **Install BepInEx** if needed → **Play**.

Full steps: [docs/runbook/players.md](docs/runbook/players.md). Support: [SUPPORT.md](SUPPORT.md).

## Developers

Clone, install .NET 8 (+ .NET 6 for Injector) and Node 20+, then see [docs/contributing/dev-setup.md](docs/contributing/dev-setup.md). Architecture map: [docs/contributing/architecture-map.md](docs/contributing/architecture-map.md).

```powershell
$env:FUSIONRPG_GAME_DIR = "<your game folder with BepInEx\core and interop>"
dotnet test tests\FusionRpg.Launcher.Tests
.\scripts\publish-player.ps1
```

## Contributing

Read [CONTRIBUTING.md](CONTRIBUTING.md), [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md), and [SECURITY.md](SECURITY.md). PRs need a short test plan; architecture locks go through [docs/architecture/decisions.md](docs/architecture/decisions.md) first.

---

**Status:** full play-scene capture is in. Combat HP/ATK come from **per-spawn dumps** (`spawn_stats`), not the type catalog. Global percent/flat scale still works for DEF; loot/XP RPG loop is later.

**Overlay waves (W0–W11 shipped):** MatchRuntime live FSM + UniqueActor Cold Data/Server FSM + UniqueBindings + lawn Dual-Plane Projector on `#/lawn` (12×5 canvas, Split/Large/Stack, monitor + Intent) + roster FE with equip + specimen XP (`#/roster`) + Secondary content kit + dual-host profile Bridges and DropIntoGame matrix + Secondary no-Unity guard + LimHealth Bend + alt-damage inventory. **W12 P2–P3 seeds deferred** (product pick later). **Build next:** Playwright lawn e2e; full gear shop polish remains out. Hard outs still held: ActiveBound mid-run equip, bullet/hypno folds. See [match-runtime.md](docs/architecture/match-runtime.md), [unique-actor-runtime.md](docs/architecture/unique-actor-runtime.md), [lawn-projector.md](docs/architecture/lawn-projector.md).

**Cheats:** the web UI is the **single source of truth** (absence = unset). There is no in-game cheat menu/GUI. Empty fields and Clear remove entries; identity defaults (`1` / `0` / `-1`) are never applied. Document `revision` bumps on each store write; injector applies present-only scales, absolutes, and board config.

**PvzStats:** player-bound modifier foundation (`pvz_stat_*` tables) between future RPG features and `EntityApply`. Web `#/pvz-stats` shows the derived sheet (Y0=0 monitor) and drill-down by source. Not the same as future RPG progression stats.

**PvzActivity / PvzIntent:** typed play facts + rollups (`pvz_activity_*`) for progression substrate, plus `pvz.*` write commands (e.g. `pvz.spawn.extra` → `ExtraSpawnFired`, capture `source=extra`). Web `#/pvz-activity`. Capture projects Match/Kill/Place; Intent is idempotent on `correlationId`. See [docs/architecture/pvz-middle-layer.md](docs/architecture/pvz-middle-layer.md).

**RpgProgression:** first RPG feature — per-save actor XP/levels (`rpg_actor_progression` / `rpg_xp_ledger`) for player, plant types, and zombie types. Driven from Activity; arithmetic curve; demotion debt for future power. Kill XP reserved for power-scale (×1 stub). Web `#/rpg-progression` Almanac dossier (Overview charts, Plants/Zombies dossiers, Ledger). Actor dossiers promote almanac **portrait** (`image` layer), **name**, **info**, zombie **introduce**, and plant **cost** when dumps exist. See [docs/architecture/rpg-progression.md](docs/architecture/rpg-progression.md).

**Foundation Effects (sealed):** `FoundationContractVersion = 2` — Core `EffectBag` plans FA1–FA10 IntentPlans; Secondary/FE enqueue through **Funnel** (never Unity apply / `Bag.Grant` / `TakeDamage`). FA10 `ApplyResourceDelta` is Writer **Add** HP only. Debug prove: `POST /api/debug/effect/enqueue-delta`. IMGUI floaters (`SYS-DAMAGE-FX`, default on) and BoardAction cherry use injector [lawn-coords.md](docs/injector/lawn-coords.md) (Mouse box cell center, not entity feet). Gate: `scripts/guard-funnel-delta.ps1`. Offline kit: `SimEffectHost` + `POST /api/sim/effect/*`. LIVE L1–L14 lawn PASS. See [effect-system.md](docs/architecture/effect-system.md), [effect-funnel.md](docs/architecture/effect-funnel.md), [effect-testing.md](docs/architecture/effect-testing.md).

**Persistence (DAL cutover live):** all SQL lives in `FusionRpg.Data` (`RpgStore*`). Hot DB `rpg-hot.sqlite` + media `rpg-media.sqlite` + cold `archive/*`. Post-run compaction (KeepLastN=50, Activity/XP tails 10k/5k). User-driven clear on web `#/storage` (`/api/storage/*`) — **no auto archive GC**. Gate: `scripts/guard-dal.ps1` (Guard.Tests + `deploy-play.ps1`). See [docs/database/ledger-snapshot.md](docs/database/ledger-snapshot.md) and [persistence-implement-checklist.md](docs/database/persistence-implement-checklist.md).

**Almanac dumps:** selecting cards in the in-game almanac captures type icons (`type_icon_layers` / composed `type_icons`) and pedia text (`type_almanac_dump`). Review pages: `#/icon-dump`, `#/almanac-dump`. Portrait promote uses layer `image`; text promote lands on progression actor DTOs and fills the `types` catalog without letting later English spawn overwrite Chinese titles.

**Players** double-click `FusionRpg.Launcher.exe`. Vite is compiled into static files inside the server’s `wwwroot` folder. Nobody installs Node, npm, a .NET SDK, or the Desktop Runtime. See [docs/runbook/players.md](docs/runbook/players.md) and [docs/launcher/spec.md](docs/launcher/spec.md).

## Read the docs first

Start at **[docs/README.md](docs/README.md)** (map). Then architecture → protocol → module specs → [runbook](docs/runbook/local-dev.md).

Game research for this pack is under [docs/research/](docs/research/sources.md).

## What is captured

| Layer | Role |
|---|---|
| `spawn_stats` + event dump JSON | **SSOT for combat** — HP/ATK/armor at each capture (`initHealth`, `start`, `reinforce`, …) |
| `types` | Catalog / baseline — `typeName` / `displayName` fill-if-empty from spawn; almanac dump may PreferIncoming Chinese titles |
| `type_icons` / `type_icon_layers` | Portrait + raw almanac card layers as PNG BLOBs in SQLite (served via `/api/icons/...`) |
| `type_almanac_dump` | Captured pedia fields (`name` / `info` / `cost` / `introduce` / UI TMP) for promote + review |
| `runs` | Match lifecycle, board snapshot, modifiers from `Board.config` |
| `recipes` | Fusion parents from `PlantMixTreeManager.ChildToParents` |
| `cheats` (settings JSON) | **SSOT for live cheats** — versioned `entries[]` (+ optional `mods`); web writes, injector applies |
| `pvz_stat_*` | **SSOT for player attrs** — modifiers + sheet cache (PvzStats) |
| `pvz_activity_*` | **SSOT for play facts** — append-only facts + rollup cache (PvzActivity) |
| `archive/*` + `archive_catalog` | Cold capture / Activity / XP segments (written before hot delete); user purge via `#/storage` |

External HP hacks (e.g. 100× zombies) show up in spawn dumps when the game applies them (often via reinforce / set-health paths). They may **not** appear in `board.modifiers`. Future RPG code must read dumps, never `types.hp_base`.

## Modules

| Module | Path | Role |
|---|---|---|
| Launcher | `src/FusionRpg.Launcher` | WPF installer + process dashboard (player entry) |
| Injector | `src/FusionRpg.Injector` (+ `.BepInEx` / `.MelonLoader` hosts) | Harmony hooks + cheat apply behind RpgHost |
| CheatCore | `src/FusionRpg.CheatCore` | Schema, identity/strip rules, `ModDocument` codec |
| Core | `src/FusionRpg.Core` | StatSystem + StatMath + SimEngine + Activity kinds + EffectBag / SimEffectHost (no Unity) |
| Data | `src/FusionRpg.Data` | Sole SQLite DAL (`RpgStore*`, cold archive, compaction, Storage purge) |
| Server | `src/FusionRpg.Server` | REST + SignalR + ingest + CompactionWorker + `/api/storage/*` + `/api/debug/*` + `/api/sim/effect/*` (no SQL) |
| Contracts | `src/FusionRpg.Contracts` | Shared DTOs / `ModDocument` / Activity / Effect DTOs |
| Web | `web/fusion-rpg-web` | Vite + React control room (Lawn, Cheats, PvzStats, PvzActivity, Progression, Storage, Icon/Almanac dump) |
| Tests | `tests/` | CheatCore + Core + Data + Guard (DAL / funnel / lawn-coords) + Launcher + server e2e (no PVZRH for Secondary Effects) |

Injector never talks to the browser. Both talk to the server (`http://127.0.0.1:5088`). Commands also land in an HTTP inbox (`GET /api/cheats/commands/pending`) when SignalR delivery fails.

**Play this pack:** `.\scripts\deploy-play.ps1` (runs writer/DAL/Secondary/funnel guards, builds injector, publishes server to `dist/FusionRpg.Server`, launches the game). Melon 3.9: `$env:FUSIONRPG_ML_GAMEDIR` + `-LoaderHost MelonLoader`. Use `-RebuildUi` / `-RestartServer` when the SPA or API changes. Session DB: `dist/FusionRpg.Server/data/rpg-hot.sqlite` + `rpg-media.sqlite` + `archive/` (next to the server exe; gitignored). Icons and almanac text live as BLOBs in the media file. Lawn UI: `http://127.0.0.1:5088/#/lawn` (12×5 projector; zombies use Unity `Column` so spawn lanes 10–11 are not stacked on plant col 9).

If cheats look wrong after an upgrade, open the web **Cheats** page and use **Reset all** once to clear polluted identity rows.

## Local game (not in git)

Point `FUSIONRPG_GAME_DIR` at your install (must contain `PlantsVsZombiesRH.exe`, `BepInEx\core\`, `BepInEx\interop\`). Example layout:

```
<your game folder>\
  BepInEx\core\
  BepInEx\interop\
  BepInEx\plugins\FusionRpg\   ← built injector
  PlantsVsZombiesRH.exe
```
