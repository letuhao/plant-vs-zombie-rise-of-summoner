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
| [architecture/resource-hub-ssot.md](architecture/resource-hub-ssot.md) | **Resource SSOT** — five actor resources in one shared set (`hp` `stamina` `hunger` `spirit` `qi`), faction differences are display labels only; scope/polarity/accrual registry, the two-suns rule, exhaustion-as-status, lazy regen |
| [architecture/resource-hub-ideal.md](architecture/resource-hub-ideal.md) | **Superseded — reasoning trail only.** Was the ideal capture — the five resources (`hp` `stamina` `hunger` `spirit` `qi`) with per-faction display labels, the exhaustion-debuff mechanic, lazy regen, and the scope/class/polarity registry shape |
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
| [architecture/effect-atom-map.md](architecture/effect-atom-map.md) | **Capability map** — atom effects as the Secondary SSOT: 20 modules in 5 waves, dependency order, checkpoints, four cross-program hazards. **complete** — all 21 rows built, Checkpoints A–E reached, no golden re-blessed |
| [architecture/effect-atom/atom-catalog-ssot.md](architecture/effect-atom/atom-catalog-ssot.md) | **SSOT effect list** — the closed vocabulary: 5 attach points, 12 kinds, 7 triggers, 8 primary + 99 derived channels, 21 statuses (13 functional), refused-with-cause list, and the 7 silent failures that become rejections |
| [architecture/effect-atom/definitions.md](architecture/effect-atom/definitions.md) | **Definitions** — the ~40 values the module specs referenced and never defined: units, tolerances, id grammars, NULL semantics, orderings, hash algorithm, the 30 rejection codes. Wins over any spec until that spec is rewritten |
| [architecture/effect-atom/atom-family-library.md](architecture/effect-atom/atom-family-library.md) | **Family library** — ~55 authored affix families over the 12 kinds, element families generated not authored, side asymmetry (plant/zombie/demon), and the fire-rate channel gap |
| [architecture/action-map.md](architecture/action-map.md) | **Capability map** — the action layer joining atoms (what) to the turn kernel (when): 9 modules, targeting, the four resource pools, selection. Blocked on the atom map's Checkpoint B; no build authorized |
| [architecture/action/spec-action-model.md](architecture/action/spec-action-model.md) | **Spec A1** — the action data structure: `rpg_action` / `rpg_action_cost` / `rpg_action_effect_scope`, the membership rule, dataflow, and the six-case corpus. Awaiting approval; no build authorized |
| [architecture/action/spec-targeting.md](architecture/action/spec-targeting.md) | **Spec A2** — typed closed targeting contract compiling to the shipped `TargetResolver`; caster-relative `Relation` (one action serves both factions) and Chebyshev range that passes with no board |
| [architecture/action/spec-usability-conditions.md](architecture/action/spec-usability-conditions.md) | **Spec A4** — the five ordered usability gates with typed refusals; reuses `E3`'s predicate compiler and asks it for two resource leaves |
| [architecture/action/spec-basic-attack-adoption.md](architecture/action/spec-basic-attack-adoption.md) | **Spec A5** — the seam proof: the shipped basic attack as a declared action, eight goldens byte-identical; seven hazard fixtures and the `SourceOrder`/`OrdinalPtr` finding |
| [architecture/action/spec-action-costs.md](architecture/action/spec-action-costs.md) | **Spec A3** — the five resources, lazy regen, exhaustion-as-status with hysteresis, atomic cost rollback, run lifetime and rest |
| [architecture/action/spec-action-catalog.md](architecture/action/spec-action-catalog.md) | **Spec A6** — load, compile, cache. Server-side only: actions are battle-mode, so the injector never sees one and there is no push |
| [architecture/action/spec-action-selection.md](architecture/action/spec-action-selection.md) | **Spec A7** — the stub AI and the game's first AI layer: pursue nearest, act to kill, read through `IBattleView` so deferred fog is a swap |
| [architecture/action/spec-defence-actions.md](architecture/action/spec-defence-actions.md) | **Spec A8** — block/guard/brace as actions; stance vs reaction, separate `WReact` pool, bounded nesting, `WReact=0` byte-identical |
| [architecture/action/spec-movement-actions.md](architecture/action/spec-movement-actions.md) | **Spec A9** — movement as an ordinary action: slot-free, priced in ticks, `move.range` distinct from `turn.speed` |
| [architecture/action/spec-battle-board.md](architecture/action/spec-battle-board.md) | **Spec A10** — the grid: seeded per-encounter dimensions, one actor per cell, Chebyshev, builds a `BoardSnapshot` so targeting transfers unchanged |
| [architecture/match-runtime.md](architecture/match-runtime.md) | MatchRuntime FSM + MatchState (W1–W5 shipped; bullets/hypno deferred) |
| [architecture/unique-actor-runtime.md](architecture/unique-actor-runtime.md) | UniqueActor FSM — durable specimens (W4–W5 + W8 equip/XP/roster shipped) |
| [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) | Dual authority: Hot / Cold / Intent loops (design lock) |
| [architecture/p0-hot-path-hardening.md](architecture/p0-hot-path-hardening.md) | P0 closed (W0); lawn observe on `#/lawn` (W12 deferred) |
| [architecture/lawn-projector.md](architecture/lawn-projector.md) | Phaser 4 FE lawn projection (**W6–W7 shipped**; 12×5 canvas) |
| [architecture/fe-game-foundation.md](architecture/fe-game-foundation.md) | Dual-Plane Lawn Projector runtime SSOT (DPLP; **W6–W7 shipped**) |
| [design/README.md](design/README.md) | **The GUI design** — the Entity–Representation Matrix method, the seven HTML plates covering every player surface, and the token/kit source the app theme is generated from |
| [design/tech-stack.md](design/tech-stack.md) | **FE stack + gap register** — what the design demands that the current stack cannot do, the measured per-library bundle cost, the i18n choice (Lingui, English-first) and the 12 open build gaps |
| [design/information-architecture.md](design/information-architecture.md) | **The GUI map** — 4 stages, 8 layers, the band assignment, the verb table, the reachability exceptions, the unlock ladder, and where all 20 current routes go |
| [architecture/game-gui-principles.md](architecture/game-gui-principles.md) | **Binding business rules for every player surface** — one stage + layer stack (GG-1), band model, vocabulary/contrast/reach rules, player vs developer trees, enforcement checks, and the 2026-08-22 compliance baseline |
| [architecture/implementation-roadmap.md](architecture/implementation-roadmap.md) | Master W0–W14 checklist — **W0–W11 shipped**, W12 triage deferred, W13–W14 → implement plan |
| [architecture/unique-entity-effects.md](architecture/unique-entity-effects.md) | Lawn unique power path + apply scope (bind → `entity:{ptr}`) |
| [architecture/standalone-rpg-map.md](architecture/standalone-rpg-map.md) | Standalone-first program: web-playable RPG core, PvZ as extension — capability map + invariants |
| [architecture/standalone/](architecture/standalone/spec-standalone-charter.md) | Module specs: standalone-charter, match-source-core (wave 1), expeditions (the first playable web loop) |
| [architecture/standalone/audit-2026-08-21.md](architecture/standalone/audit-2026-08-21.md) | Structured multi-perspective review: findings, debates, adjudications behind the spec hardening |
| [architecture/rpg-mechanism-audit-2026-08-21.md](architecture/rpg-mechanism-audit-2026-08-21.md) | Code-verified audit of the RPG as built: catalogs, loops, findings, doc↔code drift, world/map readiness |
| [architecture/world-graph-ideal.md](architecture/world-graph-ideal.md) | **Ideal capture (not a spec)** — PvZ multiverse as a living strategy map: sector graph → sector board (construction) → lane board (tower defense); legions, bases, Dave's homeworld |
| [architecture/empire-economy-ideal.md](architecture/empire-economy-ideal.md) | **Superseded — reasoning trail only.** Ideal capture — what a territory produces and where it lands: the two-tier stock/treasury seam that keeps world replay deterministic, the `resource`/`shard` naming collisions, and the soul-mine question |
| [architecture/empire-economy-ssot.md](architecture/empire-economy-ssot.md) | **Economy SSOT** — what holds: three stocks, anchoring and component pooling, the progression loop (you keep who you are, you lose where you were), the reward layer and the soul conduit, and why bounded worlds dissolve the 500-hour problem. **Supersedes the ideal** |
| [architecture/economy-principles.md](architecture/economy-principles.md) | **Economy principles** — the tests any currency must pass before it exists: faucet/sink balance, territorial income needs territorial upkeep, complements-not-substitutes as the resource-count test, convertibility, payback period, land/labour/capital, and what they already decide |
| [architecture/loam-map.md](architecture/loam-map.md) | **Capability map** — loam and the Fracture: nine audit findings (grind breaks one-time-cost mechanics; completion is the enemy capital, not conquest), the cut list, nine modules and the build order with its four hard-won ordering hazards |
| [architecture/loam/spec-loam-model.md](architecture/loam/spec-loam-model.md) | **Spec (draft)** — loam and Fracture intensity as state: two fields on `WorldSector`, the rootbed slot, validation, persistence, and the fog rule (intensity is terrain, stock is live) |
| [architecture/loam/spec-loam-calc.md](architecture/loam/spec-loam-calc.md) | **Spec (draft)** — the five pure calculators (production, upkeep, balance, fade, habitability) wired to nothing, plus the economy harness that measures the numbers instead of guessing them |
| [architecture/loam/spec-loam-turn.md](architecture/loam/spec-loam-turn.md) | **Spec (draft)** — the turn wakes up: `Production` yields, `Pressure` charges upkeep and fades ground to `Lost`, `RulesetVersion` 4; the fade *is* the settlement enforcement, so a claim on barren ground is allowed and warned, never refused |
| [architecture/loam/spec-loam-maps.md](architecture/loam/spec-loam-maps.md) | **Spec (draft)** — `two-hearths`, the gate map built to exercise scarcity, barren corridors and a chaos gradient, with its teaching properties asserted by tests; the size ladder as a catalog with big tiers gated on `world-generator` |
| [architecture/loam/spec-loam-ai-survival.md](architecture/loam/spec-loam-ai-survival.md) | **Spec (draft)** — one rule (`Abandon`: do not keep what you cannot sustain) plus `UpkeepHandicapMilli`, a **declared and reported** balance lever; a silent handicap could not survive replay |
| [architecture/loam/spec-loam-fe.md](architecture/loam/spec-loam-fe.md) | **Spec (draft)** — pre-gate, because the owner cannot judge what they cannot see: light-in-the-dark overlay, the loam gauge, per-sector net flow on the wire, and the four ground states rendered distinctly |
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
| [web/spec.md](web/spec.md) | **v2** — Vite React UI as stages + layers: shell, band runtime, entity ladders, i18n, bundle budget, developer tree |
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


