# Documentation map — Rise of Summoner

Player-facing name: **Rise of Summoner**. Internal binaries and modules keep the **FusionRpg** prefix.

## Start here

| Audience | Start |
|---|---|
| **Players** | [runbook/players.md](runbook/players.md) · [SUPPORT.md](../SUPPORT.md) · [Releases](https://github.com/letuhao/plant-vs-zombie-rise-of-summoner/releases) |
| **Contributors** | [../CONTRIBUTING.md](../CONTRIBUTING.md) · [contributing/dev-setup.md](contributing/dev-setup.md) · [contributing/architecture-map.md](contributing/architecture-map.md) |

Then architecture → protocol → module specs → [local-dev runbook](runbook/local-dev.md).

Read in this order for deep work.

## 1. Research (how the game works)

Observation only. No product design here. Paths under `H:\Games\...` in research notes are **author machine examples**, not required installs.

| File | Contents |
|---|---|
| [research/sources.md](research/sources.md) | Repos, licenses, local interop paths |
| [research/harmony-hook-map.md](research/harmony-hook-map.md) | Harmony candidates / in-use patches |
| [research/game-types-381.md](research/game-types-381.md) | Types dumped from this 3.8.1 pack |
| [research/stat-fields.md](research/stat-fields.md) | HP / ATK / speed fields |
| [research/modifiable-gameplay.md](research/modifiable-gameplay.md) | What can change (WRITE / CAPTURE / OUT) before RPG design |
| [research/cheat-menu-coverage.md](research/cheat-menu-coverage.md) | Proof-suite cheat definition + full menu coverage checklist |
| [research/events-lifecycle.md](research/events-lifecycle.md) | Board / spawn / death |
| [research/simple-spawner.md](research/simple-spawner.md) | Simple Spawner vs our capture |
| [research/open-questions.md](research/open-questions.md) | Runtime risks still untested |
| [research/arpg-effects/00-index.md](research/arpg-effects/00-index.md) | ARPG effects inspiration (attrs, stacking, procs, ailments, hit/convert) — not product ADR |
| [research/effect-runtime/00-index.md](research/effect-runtime/00-index.md) | Own-game inject/capture for Effects (hit/status/spawn capability matrix + proofs) |
| [research/architecture-stress/00-index.md](research/architecture-stress/00-index.md) | Red-team: situations + break matrix vs dual-authority locks (research only) |
| [research/mod-loaders.md](research/mod-loaders.md) | BepInEx vs MelonLoader on this machine; host choice (not Effect depth) |

## 2. Architecture (product design)

| File | Contents |
|---|---|
| [architecture/overview.md](architecture/overview.md) | Four modules (Launcher + Injector + Server + Web), v1 scope |
| [architecture/stat-system.md](architecture/stat-system.md) | Modifier bag, compose, EntityApply / single writer |
| [architecture/pvz-stats.md](architecture/pvz-stats.md) | Player-bound PvzStats layer (≠ future RPG stats) |
| [architecture/pvz-middle-layer.md](architecture/pvz-middle-layer.md) | Stats + Activity + Intent constitution |
| [architecture/pvz-activity.md](architecture/pvz-activity.md) | Typed play facts + rollups |
| [architecture/pvz-intent.md](architecture/pvz-intent.md) | `pvz.*` game write commands |
| [architecture/rpg-progression.md](architecture/rpg-progression.md) | Per-save actor XP / levels (first RPG feature) |
| [architecture/effect-system.md](architecture/effect-system.md) | Foundation Effects (sealed v1) — bag, FA*, Secondary law |
| [architecture/effect-data.md](architecture/effect-data.md) | Effect / grant / overlay JSON shapes |
| [architecture/effect-runtime.md](architecture/effect-runtime.md) | Injector apply path + capture → FT* |
| [architecture/effect-funnel.md](architecture/effect-funnel.md) | Funnel + Guard: Secondary enqueue → merge → FA10 Writer Add (`guard-funnel-delta.ps1` shipped) |
| [architecture/combat-damage-ssot.md](architecture/combat-damage-ssot.md) | DamagePacket target + delivery SSOT (signed HP delta; heal included) — partially shipped |
| [architecture/effect-testing.md](architecture/effect-testing.md) | Offline SimEffectHost / scenarios vs LIVE L1–L14 |
| [architecture/match-runtime.md](architecture/match-runtime.md) | MatchRuntime FSM + MatchState (W1–W5 shipped; bullets/hypno deferred) |
| [architecture/unique-actor-runtime.md](architecture/unique-actor-runtime.md) | UniqueActor FSM — durable specimens (W4–W5 + W8 equip/XP/roster shipped) |
| [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) | Dual authority: Hot / Cold / Intent loops (design lock) |
| [architecture/p0-hot-path-hardening.md](architecture/p0-hot-path-hardening.md) | P0 closed (W0); lawn observe on `#/lawn` (W12 deferred) |
| [architecture/lawn-projector.md](architecture/lawn-projector.md) | Phaser 4 FE lawn projection (**W6–W7 shipped**; 12×5 canvas) |
| [architecture/fe-game-foundation.md](architecture/fe-game-foundation.md) | Dual-Plane Lawn Projector runtime SSOT (DPLP; **W6–W7 shipped**) |
| [architecture/implementation-roadmap.md](architecture/implementation-roadmap.md) | Master W0–W12 checklist — **W0–W11 shipped**, W12 triage deferred |
| [architecture/unique-entity-effects.md](architecture/unique-entity-effects.md) | Lawn unique power path + apply scope (bind → `entity:{ptr}`) |
| [architecture/decisions.md](architecture/decisions.md) | Locked choices |
| [architecture/data-flow.md](architecture/data-flow.md) | Game → injector → server → web |

## 3. Protocol and database

| File | Contents |
|---|---|
| [protocol/rest.md](protocol/rest.md) | HTTP routes and JSON |
| [protocol/signalr.md](protocol/signalr.md) | Hub, groups, methods |
| [protocol/events.md](protocol/events.md) | Event envelope and kinds |
| [database/data-model.md](database/data-model.md) | Players, FKs, invariants |
| [database/schema.md](database/schema.md) | SQLite tables (live) |
| [database/ledger-snapshot.md](database/ledger-snapshot.md) | Live: watermarks + cold archive/trim + user Storage clear |
| [database/persistence-refactor-blast-radius.md](database/persistence-refactor-blast-radius.md) | Cutover complete (A–E + W12); historical abort/restore record |
| [database/persistence-implement-checklist.md](database/persistence-implement-checklist.md) | W0–W12 implement checklist (A–E + Storage) |

## 4. Module specs

| File | Contents |
|---|---|
| [launcher/spec.md](launcher/spec.md) | WPF player entry: loader install, FusionRpg update, port pick, process dashboard |
| [injector/spec.md](injector/spec.md) | BepInEx plugin (current host) |
| [injector/lawn-coords.md](injector/lawn-coords.md) | Unity Mouse box = injector lawn XY (cherry + floaters) |
| [injector/dual-host-roadmap.md](injector/dual-host-roadmap.md) | BepInEx + MelonLoader dual-artifact port (not dual-load) |
| [server/spec.md](server/spec.md) | ASP.NET + SQLite + SignalR |
| [web/spec.md](web/spec.md) | Vite React UI + Phaser `#/lawn` (12×5 projector) |
| [contributing/dev-setup.md](contributing/dev-setup.md) | SDK/Node, `FUSIONRPG_GAME_DIR`, publish + release |
| [contributing/architecture-map.md](contributing/architecture-map.md) | Where code/SQL/Unity writes belong |

## 5. Testing

| File | Contents |
|---|---|
| [testing/foundation.md](testing/foundation.md) | In-CI matrix vs in-game-only |
| [testing/probes.md](testing/probes.md) | SIM-only reset / snapshot / `test.probe` |
| [testing/player-pack-smoke.md](testing/player-pack-smoke.md) | Offline/online smoke for `dist/FusionRpg` (not SIM HTTP) |
| [testing/web.md](testing/web.md) | Vitest coverage + Playwright e2e for the SPA |

## 6. Runbook

| File | Contents |
|---|---|
| [runbook/players.md](runbook/players.md) | Non-tech install: Launcher EXE, trust/AV note, no Node/SDK/Desktop Runtime |
| [runbook/release-prove.md](runbook/release-prove.md) | Tag → GitHub Release → real Browse/Install/Play prove |
| [runbook/local-dev.md](runbook/local-dev.md) | Developers: Vite, `dotnet`, publish zip |
| [runbook/simulator.md](runbook/simulator.md) | Fake injector tab, `FUSIONRPG_SIM=1`, `dotnet test` |
| [runbook/debug-pipeline.md](runbook/debug-pipeline.md) | `/api/debug/*` + `/api/sim/effect/*` controllable Effect tests |
| [runbook/debug-live-checklist.md](runbook/debug-live-checklist.md) | Ordered LIVE prove checklist (F1–F23, P1 verdict, sign-off) |
| [research/effect-runtime/_checklist-effect-foundation-live.json](research/effect-runtime/_checklist-effect-foundation-live.json) | Foundation L1–L14 seal status (offline + lawn) |
