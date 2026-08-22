# Documentation map — Rise of Summoner

Player-facing name: **Rise of Summoner**. Internal binaries and modules keep the **FusionRpg** prefix.

## ⛔ Before you design anything

**[DESIGN-GATE.md](DESIGN-GATE.md) is binding for every contributor, human or automated.** It carries
the topic index (*"about to touch X → you must have read Y"*), the load-bearing invariants, the
evidence rules, and the log of misconceptions that have cost real time. Read it before writing any
spec, plan, proposal, or ADR — and read the documents its §1 row names for your subsystem.

## Start here

| Audience | Start |
|---|---|
| **Before any design work** | **[DESIGN-GATE.md](DESIGN-GATE.md)** — mandatory reading gate + topic index |
| **Anyone new** | [architecture/software-architecture.md](architecture/software-architecture.md) (whole system, one page) · [architecture/data-architecture.md](architecture/data-architecture.md) (all data, one page) |
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
| [research/level-entry.md](research/level-entry.md) | How levels open (`UIMgr.EnterGame`); gated probe; mid-match lab vs custom run |
| [research/arpg-effects/00-index.md](research/arpg-effects/00-index.md) | ARPG effects inspiration (attrs, stacking, procs, ailments, hit/convert) — not product ADR |
| [research/effect-runtime/00-index.md](research/effect-runtime/00-index.md) | Own-game inject/capture for Effects (hit/status/spawn capability matrix + proofs) |
| [research/effect-runtime/06-chaos-combat-element-adaptation.md](research/effect-runtime/06-chaos-combat-element-adaptation.md) | Chaos combat/element borrow vs Fusion overlay damage adaptation |
| [research/status-core-chaos-mapping.md](research/status-core-chaos-mapping.md) | Chaos status-core resistance borrow → Fusion layers (reference only) |
| [research/actor-core-chaos-mapping.md](research/actor-core-chaos-mapping.md) | Chaos level/realm/power_scale → Fusion progression.power (reference only) |
| [research/architecture-stress/00-index.md](research/architecture-stress/00-index.md) | Red-team: situations + break matrix vs dual-authority locks (research only) |
| [research/mod-loaders.md](research/mod-loaders.md) | BepInEx vs MelonLoader on this machine; host choice (not Effect depth) |

## 2. Architecture (product design)

| File | Contents |
|---|---|
| [architecture/software-architecture.md](architecture/software-architecture.md) | **Start here** — whole-system map: modules, hot path, invariants, loops, FSMs, protocol, build |
| [architecture/data-architecture.md](architecture/data-architecture.md) | **Start here (data)** — physical stores, table inventory, SSOT map, lifecycle, DAL boundary |
| [architecture/overview.md](architecture/overview.md) | Four modules (Launcher + Injector + Server + Web), v1 scope |
| [architecture/stat-system.md](architecture/stat-system.md) | Modifier bag, compose, EntityApply / single writer |
| [architecture/actor-hub-ssot.md](architecture/actor-hub-ssot.md) | **Derived-stat SSOT** — derived snapshot, progression.power, dynamic ApplyScale; 99 registered channels (84 combat **shipped**, not reserved); `resource.*` proposed in §3.G |
| [architecture/resource-hub-ideal.md](architecture/resource-hub-ideal.md) | **Ideal capture (not a spec)** — the five resources (`hp` `stamina` `hunger` `spirit` `qi`) with per-faction display labels, the exhaustion-debuff mechanic, lazy regen, and the scope/class/polarity registry shape |
| [architecture/status-ssot.md](architecture/status-ssot.md) | StatusRuntime actor instances, ICD, resistance, contagion catalog — **shipped** |
| [architecture/element-hub-ssot.md](architecture/element-hub-ssot.md) | Element typing, ring-cycle matchup matrix (§8.5), combat derived channels — **design locked** |
| [architecture/combat-element-implement-plan.md](architecture/combat-element-implement-plan.md) | Overlay combat + Element Hub implement checklist (C0–C4; matrix golden tests) |
| [architecture/actor-hub-status-implement-plan.md](architecture/actor-hub-status-implement-plan.md) | Actor Hub + StatusRuntime implement checklist (S0–S7 **shipped**) |
| [architecture/pvz-stats.md](architecture/pvz-stats.md) | Player-bound PvzStats layer (≠ future RPG stats) |
| [architecture/pvz-middle-layer.md](architecture/pvz-middle-layer.md) | Stats + Activity + Intent constitution |
| [architecture/pvz-activity.md](architecture/pvz-activity.md) | Typed play facts + rollups |
| [architecture/pvz-intent.md](architecture/pvz-intent.md) | `pvz.*` game write commands |
| [architecture/rpg-progression.md](architecture/rpg-progression.md) | Per-save actor XP / levels (first RPG feature) |
| [architecture/effect-system.md](architecture/effect-system.md) | Foundation Effects (sealed v1) — bag, FA*, Secondary law |
| [architecture/effect-data.md](architecture/effect-data.md) | Effect / grant / overlay JSON shapes |
| [architecture/effect-runtime.md](architecture/effect-runtime.md) | Injector apply path + capture → FT* |
| [architecture/effect-funnel.md](architecture/effect-funnel.md) | Funnel + Guard: Secondary enqueue → merge → FA10 Writer Add (`guard-funnel-delta.ps1` shipped) |
| [architecture/combat-damage-ssot.md](architecture/combat-damage-ssot.md) | RPG overlay damage layer: derived combat + element math → signed HP delta — **partially shipped** (resolver/Funnel); overlay CombatMath **deferred** |
| [architecture/effect-testing.md](architecture/effect-testing.md) | Offline SimEffectHost / scenarios vs LIVE L1–L14 |
| [architecture/effect-atom-ideal.md](architecture/effect-atom-ideal.md) | **Ideal capture (not a spec)** — atom effects as the smallest unit, skills/traits/items as containers, values + power in SQLite; roll policy (fixed / on-instantiate / on-apply) and power as a category vector |
| [architecture/effect-adoption-audit-2026-08-22.md](architecture/effect-adoption-audit-2026-08-22.md) | **Adoption tracker** — the 11 sites that own effect-shaped logic, the runtime consumer matrix, what "follows the effect SSOT" means, and a per-stream status table |
| [architecture/effect-atom-map.md](architecture/effect-atom-map.md) | **Capability map** — atom effects as the Secondary SSOT: 14 modules in 5 waves, dependency order, checkpoints, four cross-program hazards. Awaiting approval; no build authorized |
| [architecture/effect-atom/atom-catalog-ssot.md](architecture/effect-atom/atom-catalog-ssot.md) | **SSOT effect list** — the closed vocabulary: 5 attach points, 12 kinds, 7 triggers, 8 primary + 99 derived channels, 21 statuses (13 functional), refused-with-cause list, and the 7 silent failures that become rejections |
| [architecture/effect-atom/definitions.md](architecture/effect-atom/definitions.md) | **Definitions** — the ~40 values the module specs referenced and never defined: units, tolerances, id grammars, NULL semantics, orderings, hash algorithm, the 30 rejection codes. Wins over any spec until that spec is rewritten |
| [architecture/effect-atom/atom-family-library.md](architecture/effect-atom/atom-family-library.md) | **Family library** — ~55 authored affix families over the 12 kinds, element families generated not authored, side asymmetry (plant/zombie/demon), and the fire-rate channel gap |
| [architecture/action-map.md](architecture/action-map.md) | **Capability map** — the action layer joining atoms (what) to the turn kernel (when): 9 modules, targeting, the four resource pools, selection. Blocked on the atom map's Checkpoint B; no build authorized |
| [architecture/match-runtime.md](architecture/match-runtime.md) | MatchRuntime FSM + MatchState (W1–W5 shipped; bullets/hypno deferred) |
| [architecture/unique-actor-runtime.md](architecture/unique-actor-runtime.md) | UniqueActor FSM — durable specimens (W4–W5 + W8 equip/XP/roster shipped) |
| [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) | Dual authority: Hot / Cold / Intent loops (design lock) |
| [architecture/p0-hot-path-hardening.md](architecture/p0-hot-path-hardening.md) | P0 closed (W0); lawn observe on `#/lawn` (W12 deferred) |
| [architecture/lawn-projector.md](architecture/lawn-projector.md) | Phaser 4 FE lawn projection (**W6–W7 shipped**; 12×5 canvas) |
| [architecture/fe-game-foundation.md](architecture/fe-game-foundation.md) | Dual-Plane Lawn Projector runtime SSOT (DPLP; **W6–W7 shipped**) |
| [architecture/implementation-roadmap.md](architecture/implementation-roadmap.md) | Master W0–W14 checklist — **W0–W11 shipped**, W12 triage deferred, W13–W14 → implement plan |
| [architecture/unique-entity-effects.md](architecture/unique-entity-effects.md) | Lawn unique power path + apply scope (bind → `entity:{ptr}`) |
| [architecture/standalone-rpg-map.md](architecture/standalone-rpg-map.md) | Standalone-first program: web-playable RPG core, PvZ as extension — capability map + invariants |
| [architecture/standalone/](architecture/standalone/spec-standalone-charter.md) | Module specs: standalone-charter, match-source-core (wave 1), expeditions (the first playable web loop) |
| [architecture/standalone/audit-2026-08-21.md](architecture/standalone/audit-2026-08-21.md) | Structured multi-perspective review: findings, debates, adjudications behind the spec hardening |
| [architecture/rpg-mechanism-audit-2026-08-21.md](architecture/rpg-mechanism-audit-2026-08-21.md) | Code-verified audit of the RPG as built: catalogs, loops, findings, doc↔code drift, world/map readiness |
| [architecture/world-graph-ideal.md](architecture/world-graph-ideal.md) | **Ideal capture (not a spec)** — PvZ multiverse as a living strategy map: sector graph → sector board (construction) → lane board (tower defense); legions, bases, Dave's homeworld |
| [architecture/world-map-program.md](architecture/world-map-program.md) | World map program: capability map, module build order, wave-1 checkpoint (**specs pending owner review**) |
| [architecture/world/](architecture/world/spec-world-model.md) | Wave-1 module specs: world-model (storage + catalogs), turn-engine (the SSOT clock), world-movement (march, claim, supply, ZOC) |
| [architecture/battle-turn-ideal.md](architecture/battle-turn-ideal.md) | **Ideal capture (not a spec)** — one virtual-time battle state machine for every mode: PvZ realtime, synchronous turn-based, hybrid. Virtual clock + scheduler + per-actor FSM + mode profiles; why it precedes enrichment |
| [architecture/battle-timeline-map.md](architecture/battle-timeline-map.md) | Battle timeline program: capability map, module ids T1–T8, dependency graph, build order; the kernel that combat action management is built on |
| [architecture\battle\](architecture\battle\spec-virtual-time-core.md) | Module specs: virtual-time-core, turn-fsm, readiness-model, mode-profiles, kernel-adoption (T1–T5) |
| [architecture/battle/audit-2026-08-21.md](architecture/battle/audit-2026-08-21.md) | Structured review of the timeline specs: four lenses, debates, adjudications, and the amendments they forced |
| [architecture/demon-system-map.md](architecture/demon-system-map.md) | Demon gameplay program: capability map, vision→stack mapping, module build order |
| [architecture/demons/](architecture/demons/spec-element-extension.md) | Module specs: element-extension, demon-core, soul-economy, demon-summoning (V1) |
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
| [launcher/overlay-spec.md](launcher/overlay-spec.md) | Game ⇄ web overlay contract (WebView2 + F10 hotkey + in-game button over a local pipe) — behavior, transport, two-wave host plan, live checklist |
| [research/2026-08-22-overlay-injector-host.md](research/2026-08-22-overlay-injector-host.md) | Wave-2 spike: can the overlay view live in the game process — WebView2 without WPF, apartment constraint, open in-game questions |
| [injector/spec.md](injector/spec.md) | BepInEx plugin (current host) |
| [injector/lawn-coords.md](injector/lawn-coords.md) | Unity Mouse box = injector lawn XY (cherry + floaters) |
| [injector/dual-host-roadmap.md](injector/dual-host-roadmap.md) | BepInEx + MelonLoader dual-artifact port (not dual-load) |
| [server/spec.md](server/spec.md) | ASP.NET + SQLite + SignalR |
| [web/spec.md](web/spec.md) | Vite React UI + Phaser `#/lawn` (12×5 projector) |
| [contributing/dev-setup.md](contributing/dev-setup.md) | SDK/Node, `FUSIONRPG_GAME_DIR`, publish + release |
| [contributing/live-test-maintain.md](contributing/live-test-maintain.md) | Enrich/maintain LIVE Python harness + SSOT honesty |
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
| [runbook/live-test-ssot.md](runbook/live-test-ssot.md) | LIVE SSOT: API map, scenario matrix, coverage honesty, Python CLI |
| [runbook/debug-pipeline.md](runbook/debug-pipeline.md) | `/api/debug/*` + `/api/sim/effect/*` controllable Effect tests |
| [runbook/debug-live-checklist.md](runbook/debug-live-checklist.md) | Ordered LIVE prove checklist (F1–F23, P1 verdict, sign-off) |
| [research/effect-runtime/_checklist-effect-foundation-live.json](research/effect-runtime/_checklist-effect-foundation-live.json) | Foundation L1–L14 seal status (offline + lawn) |


