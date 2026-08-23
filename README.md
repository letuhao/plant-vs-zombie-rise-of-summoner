<div align="center">

<img src="docs/assets/banner.svg" alt="Rise of Summoner — a persistent RPG built on your PvZ Fusion runs. Command demons. Hold the lawn. Take back the multiverse." width="100%">

<br>

[![License: AGPL v3+](https://img.shields.io/badge/license-AGPL--3.0--or--later-3d6b45?style=for-the-badge)](LICENSE) [![CI](https://img.shields.io/github/actions/workflow/status/letuhao/plant-vs-zombie-rise-of-summoner/ci.yml?branch=main&style=for-the-badge&label=build)](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/actions/workflows/ci.yml) [![Platform](https://img.shields.io/badge/platform-Windows-2a231b?style=for-the-badge)](#play-it) [![Status](https://img.shields.io/badge/status-pre--release-e0b44b?style=for-the-badge)](#play-it)

**[What is this?](#turn-your-fusion-runs-into-a-persistent-rpg)** · **[Features](#what-you-get)** · **[Play it](#play-it)** · **[Roadmap](#roadmap)** · **[Under the hood](#under-the-hood)** · **[Docs](docs/README.md)**

</div>

---

## Turn your Fusion runs into a persistent RPG

**Play the lawn. Build your roster. Summon demons. Discover fusions. Conquer the rift.**

Rise of Summoner takes everything you do in a Plants vs. Zombies Fusion run and gives it a life beyond the level. Plants, zombies, kills, placements and fusions become persistent progress, feeding a roster, an almanac, an economy, and a growing war across the multiverse.

The lawn is where you fight. The world is what you build afterward.

> **The loop:** hold the lawn → harvest souls → summon and bind demons → fuse them into something stronger → march a legion across the rift map → do it again on ground that fights back.

---

## One run becomes a world

A normal lawn run ends when the wave ends. Here it leaves something behind.

Every plant you place, every zombie you meet, every kill you land and every fusion you discover becomes part of a game that keeps running after the level does.

<table>
<tr>
<th width="50%">🌱 &nbsp;Play the lawn</th>
<th width="50%">👹 &nbsp;Play the RPG</th>
</tr>
<tr>
<td valign="top">

Keep playing Fusion and let Rise of Summoner follow along.

Kills become souls. Plants and zombies feed progression. Encounters fill your almanac. Specimens gain levels off the work they actually did.

Bound demons deploy back into the lawn and fight for you.

</td>
<td valign="top">

Away from the lawn, your world keeps moving.

Summon at the altar. Manage contracts and loyalty. Fuse specimens into stronger forms. Send squads on expeditions. Develop territory and push your legion across the rift.

Advance the whole war without starting a run.

</td>
</tr>
</table>

Then go back to the lawn with everything you have built. **Same roster, same souls, same world** — whichever side you are playing from.

---

## What you get

### ⚔️ A combat system your roster grows into

Six elements — **fire, ice, air, earth, light, dark** — plus `omni`, arranged in a ring where every matchup is a real number rather than a flavour label. Light and dark counter each other and nothing else.

Around them: layered shields, critical hits, two-phase resistance rolls, internal cooldowns that stop interactions chaining into soup, and statuses that actually resolve.

One resolver produces the damage number, and both sides use it — the live lawn and the server's own battle engine run the same math, with goldens keeping them honest. The roster you build is the roster that performs, wherever you take it.

### 👹 Demons are persistent individuals

A demon is not another copy of a unit. Every one is a **specimen** carrying its own id, level, gear, traits, element, rarity, variant and history. Duplicates are not dust — they have a job.

**Summon.** Spend the souls you earned to pull new demons into the roster. Gacha is one path, never the only one.

**Bind.** Assign demons to contract slots and keep them loyal. Loyalty decays under daily upkeep, and a demon you neglect long enough will refuse to deploy.

**Fuse.** Merge specimens into star merges that preserve identity, inherit traits, and open recipes you find by experimenting rather than by reading a list.

**Patron.** Name one demon your patron and let its element colour your entire army.

### 🗺️ Build a legion across the rift

The world is a network of sectors joined by rift lanes. Legions march between them, claim ground, raise economy and defenses, and fight for control.

Ground is not neutral. Every sector carries a **loam** rating — fertile land supports an expanding empire, barren land drains one, and territory you neglect gets taken back by the Fracture.

**Dr. Zomboss runs his own war.** He is a real commander with his own evaluation tables and his own fog, and no access to your state. He decides for himself; twenty turns of him doing it sit in the test suite.

Find his fortress and take it before your homeworld falls.

### 📈 Progress that survives the run

Nothing you earn disappears with the level. Persistent progression covers the player, every plant type, every zombie type, and every specimen in your roster — levels, gear and traits included — with an append-only ledger behind it, so every change stays explicit and recoverable.

### 📖 An almanac that builds itself

See something in the game and the overlay captures it: portrait, pedia text, cost, type information. What you meet during play turns into a browsable set of dossiers, and the fusion recipes you uncover get catalogued beside them.

You end up with a record of your own playthrough instead of a wiki somebody else wrote.

### 🖥️ Your control room

A browser command center running on your own machine. No account, no cloud, no external service, and nothing to install — no Node, no npm.

| | | | |
|---|---|---|---|
| **Lawn** — live 12×5 mirror | **Roster** — specimens + gear | **Demons** — codex + altar | **Fusion** — the lab |
| **Expeditions** — dispatch/collect | **World** — the rift map | **Progression** — almanac dossiers | **Status** — connection state |
| **Cheats** — the sandbox page | **Stats** — derived sheets | **Activity** — recorded play facts | **Runs** — match history |
| **Types** · **Recipes** · **Log** | **Storage** — archive + purge | **Sim** — play with no game | **Icon / Almanac dump** |

Twenty screens, all of it at `http://127.0.0.1:5088`.

### 🎛️ A sandbox for building and testing

Trying a build, tuning a fight, or just messing around? The sandbox hands you direct control over the systems Rise of Summoner exposes. It sits deliberately outside the normal loop and is off by default.

Every control means exactly one thing: empty is unset, Clear clears, **Reset all** resets. No mystery values left behind by a build from three versions ago.

<!-- SCREENSHOT SLOT — drop real captures here once you have them, e.g.
     <p align="center">
       <img src="docs/assets/shot-lawn.png"   alt="Live lawn mirror" width="49%">
       <img src="docs/assets/shot-roster.png" alt="Demon roster"     width="49%">
     </p>
     Shot list: lawn mirror mid-run · roster with gear · demon codex · rift map · almanac dossier -->

---

## Play it

> ### 🚧 The first packaged build is not cut yet
>
> The stack runs today — it just runs from source. The one-click zip lands with the first tagged release.
> **[⭐ Star or watch the repo](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner)** and GitHub will tell you the moment it does.

### When it ships, playing looks like this

1. Download `FusionRpg-win-x64.zip` from [Releases](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/releases).
2. Unzip anywhere → double-click **`FusionRpg.Launcher.exe`**.
3. **Browse** to your legal game folder → install **one** loader (BepInEx 6 IL2CPP *or* MelonLoader — never both) → **Play**.

The launcher starts the server, copies the plugin, starts the game, and opens the UI. You do not install Node, npm, a .NET SDK, or the Desktop Runtime.

Your Fusion install stays untouched throughout — no binary patched, no game file written, and uninstalling is deleting a folder. Builds are unsigned hobby builds, so read the **Trust & security** panel on first run — [the player runbook](docs/runbook/players.md) explains exactly what your antivirus is likely to say and why.

### Running it from source, right now

```powershell
# Windows · .NET 8 (+ .NET 6 for the injector) · Node 20+
$env:FUSIONRPG_GAME_DIR = "<your game folder — the one with PlantsVsZombiesRH.exe>"

dotnet test tests\FusionRpg.Core.Tests     # prove the domain
.\scripts\deploy-play.ps1                  # guards → injector → server → game → browser
```

Want the RPG without the game? Run `.\scripts\deploy-play.ps1 -NoGame` and open `http://127.0.0.1:5088`.

Full setup: [docs/contributing/dev-setup.md](docs/contributing/dev-setup.md) · [docs/runbook/local-dev.md](docs/runbook/local-dev.md)

---

## Roadmap

**✅ Shipped and green**

`live capture + stat overlay` · `cheats control room` · `six elements + ring matchups` · `statuses, shields, crit, resistances` · `atomic effect system` · `per-type XP, levels + ledger` · `almanac capture + dossiers` · `specimen roster, gear + XP` · `demon core, souls, summoning` · `contracts + loyalty` · `demon fusion` · `patron demon (sim)` · `expeditions — the first standalone loop` · `Phaser lawn mirror` · `rift map waves 1–2` · `Zomboss AI commander` · `loam economy` · `hot / media / cold persistence`

**🔨 In the forge**

Interactive turn-based web battles · the action layer joining effects to the turn kernel · sector development and the map→battle handoff · in-run demon capture · an in-game button that opens the control room without alt-tabbing · **the first tagged release**

**🗺️ Charted, not started**

The full three-layer world (rift graph → sector board → lane board) · hero recruitment into the commander slot already waiting for it · world events · fog-of-war depth

Nothing on this list is a promise with a date attached. It is one person's build order, and it moves.

---

## Under the hood

<details>
<summary><b>The architecture in one paragraph</b> — click to expand</summary>

<br>

A WPF **Launcher** starts a legal PVZ Fusion install with a Harmony **Injector** inside it and an independent **Server** (SQLite + REST + SignalR) beside it. A React **Web** control room, served from the server's own `wwwroot`, observes everything and issues commands. The injector never talks to the browser — both talk to the server on `127.0.0.1:5088`.

```mermaid
flowchart LR
  L["Launcher (WPF)"] -->|start/stop| S["Server (ASP.NET)"]
  L -->|start/stop| G["PlantsVsZombiesRH.exe"]
  G -->|Harmony hooks| I["Injector (in-process)"]
  I <-->|REST + SignalR| S
  W["Web control room"] <-->|REST + SignalR| S
  S --> DB[("hot · media · cold SQLite")]
  C["Core — Unity-free domain"] -.-> I
  C -.-> S
```

| Module | Path | Role |
|---|---|---|
| **Launcher** | `src/FusionRpg.Launcher` | Player entry — loader install, port pick, start game + server, self-update |
| **Injector** | `src/FusionRpg.Injector` (+ BepInEx / MelonLoader hosts) | Harmony hooks, capture, guarded apply |
| **Core** | `src/FusionRpg.Core` | Stats, actor hub, statuses, elements, combat, effects, match runtime — **no Unity** |
| **Data** | `src/FusionRpg.Data` | The only place SQL exists |
| **Server** | `src/FusionRpg.Server` | REST, SignalR, ingest, battle engine, static SPA — **no SQL** |
| **Contracts** | `src/FusionRpg.Contracts` | Shared DTOs across every boundary |
| **CheatCore** | `src/FusionRpg.CheatCore` | Cheat schema, identity/strip rules, codec |
| **Web** | `web/fusion-rpg-web` | Vite + React + Phaser control room |

</details>

<details>
<summary><b>The one rule everything hangs off</b></summary>

<br>

**Unity is the source of truth for physics, vanilla combat, entity lifetime, and current HP.** The overlay only ever does two things:

1. **Projects** Unity outward through Harmony capture — events → server → SQLite → browser.
2. **Mutates** Unity through a tiny set of guarded paths and nothing else: `EntityStatWriter` for stats, the CC executor for statuses, the effect Funnel for HP deltas, `pvz.*` intents for spawns.

Two state machines, no shared state, only messages. That is why 120 fps survives an RPG stapled to it, and why a bug in the overlay cannot corrupt your game.

Four scripts enforce it, on every CI run and every local deploy:

```powershell
.\scripts\guard-single-writer.ps1       # combat writes only via EntityStatWriter
.\scripts\guard-secondary-no-unity.ps1  # gameplay plugins stay Unity-free
.\scripts\guard-funnel-delta.ps1        # HP deltas only through the Funnel
.\scripts\guard-dal.ps1                 # SQL only inside FusionRpg.Data
```

</details>

<details>
<summary><b>Is it actually tested?</b></summary>

<br>

| | |
|---|---|
| **3,158** `[Fact]` / `[Theory]` tests | across 10 C# suites |
| **323** web tests | Vitest + Testing Library |
| **545** C# source files · **160** web TS files | |
| **4** architectural guards | run in CI *and* on every local deploy |
| Golden files | damage math, element matchups, battle resolution, world turns |
| Mutation testing | `scripts/mutate.ps1` — a covered line asserted by nothing is worth nothing |

Deterministic by design: battles resolve from recorded seeds, the world steps behind a command barrier, and replays are byte-comparable. That is not a testing convenience — it is what lets a save survive a version bump.

</details>

<details>
<summary><b>Safety, fair play, and what this thing is not</b></summary>

<br>

**Does it modify my game?** No. It never downloads, patches, or writes to the game binary. It installs a loader you choose, into a folder you point at, from that loader's official GitHub release. Uninstalling is deleting the plugin folder.

**Do I need a legal copy?** Yes. Game content is not part of this project and is not covered by its license.

**Is my save safe?** The RPG keeps its own databases next to the server executable. Updating the overlay preserves them and never touches PvZ's saves.

**Is this a cheat menu?** There is a sandbox page, because you cannot build a stat overlay without being able to set stats. It is single-player, local-only, and off by default — leave it alone and Fusion plays exactly as shipped.

**Multiplayer?** No. Localhost only. No auth, no telemetry, no network traffic beyond your own machine.

**Which game version?** Built and proven against PVZ Fusion 3.8.1; the MelonLoader host tracks 3.9.

</details>

---

## Read the docs

The docs are the design record, not an afterthought — architecture decisions, subsystem sources of truth, research notes, and the reasoning trails behind the things that got cut.

| Start here | For |
|---|---|
| [docs/README.md](docs/README.md) | The whole map |
| [architecture/software-architecture.md](docs/architecture/software-architecture.md) | The system on one page — modules, hot path, invariants, FSMs |
| [architecture/data-architecture.md](docs/architecture/data-architecture.md) | Every store and table, who owns what, hot → cold lifecycle |
| [architecture/decisions.md](docs/architecture/decisions.md) | The locked choices, and why |
| [docs/DESIGN-GATE.md](docs/DESIGN-GATE.md) | Read this before proposing anything |

---

## Contributing

Issues, ideas, and playtest reports are all welcome — especially playtest reports, because the lawn does things no offline test can see.

Start at [CONTRIBUTING.md](CONTRIBUTING.md), then [dev-setup.md](docs/contributing/dev-setup.md). PRs want a short test plan. Anything that locks behavior in goes through [decisions.md](docs/architecture/decisions.md) first. Be decent to each other: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md) · [SECURITY.md](SECURITY.md) · [SUPPORT.md](SUPPORT.md).

---

<div align="center">

**Rise of Summoner** is the player-facing name. `FusionRpg` is the internal prefix on every assembly, env var, and release zip — same project, two names.

Licensed under [AGPL-3.0-or-later](LICENSE) · Windows only · Bring your own legal copy of the game

Built by fans of Fusion, for fans of Fusion. All credit for the lawn itself belongs to the people who made it.

<sub>Plants vs. Zombies is a trademark of Electronic Arts Inc. This is an unofficial, non-commercial fan project, not affiliated with or endorsed by PopCap or Electronic Arts.</sub>

</div>
