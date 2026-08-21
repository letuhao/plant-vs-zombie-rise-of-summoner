# Plan: Demon RPG + standalone-first — remaining waves (planned 2026-08-21, after P1–P4 landed)

Specs: [demon-system-map](../docs/architecture/demon-system-map.md) · [standalone-rpg-map](../docs/architecture/standalone-rpg-map.md) · module specs in `docs/architecture/{demons,standalone}/` · rationale: [audit-2026-08-21](../docs/architecture/standalone/audit-2026-08-21.md).
Tasks: [demon-standalone-todo.md](demon-standalone-todo.md). Execution: full-auto (owner); git hands-off; commit drafts at checkpoints.

## Landed so far (baseline for this plan)

P1 charter · P2 element extension (56 channels, light/dark) · P3 demon-core (generated 24-species catalog, atomic mint, codex) · P4 soul-economy (earn v2 in-transaction, spends) · P5 foundations (`SeededRng`, XP-dedupe bug fix). Suites: Core 872 / Data 83 / Guard 38 / E2E 103+1-foreign-failure.

## Dependency graph (what actually blocks what)

```
DONE: demon-core ─┬─ soul-economy ─┬─► WAVE A  summoning (V1 internal gate)
                  │                │      needs: catalogs + mint + spend + SeededRng — ALL DONE
DONE: SeededRng ──┴────────────────┘      needs NOTHING from the pipeline waves
DONE: element-ext ─► (typing consumed everywhere)

WAVE B  pipeline adaptations ──► WAVE C  BattleEngine + WebMatchService ──► WAVE D  expeditions
   (runs.game, pollution guards,      (pure engine → subsystems → goldens      (spec gate, then
    gating, retention — each lands     → service + log + boot sweep)            dispatch/collect + FE;
    with its own regression test)                                               also needs WAVE A demons)
```

**Planning decision — reorder P6 before P5-middle:** summoning's dependencies are entirely shipped; nothing in it touches ingest or the game column. Building it first delivers the declared V1 internal gate earliest, and gives the audit's riskiest module (match-source) an uninterrupted stretch afterward. Expeditions needs both (demons to send + battles to resolve), so it stays last. The combined-roadmap doc order is honored in spirit (dependency order), and this file is the execution SSOT.

## Vertical slicing rule applied

Every task below is one complete path (model → store → API → test, or hook → guard → regression), never a horizontal layer. No task touches more than ~5 files. Each wave ends in a checkpoint whose criteria come from the module spec's success list.

## Risks and mitigations

| Risk | Mitigation |
|---|---|
| Summon transaction misses a post-crash state (audit F3's ghost) | A2's forced-failure test aborts mid-sequence and asserts zero rows in all five tables |
| Pipeline adaptations regress live PvZ ingest | Wave B lands one adaptation per task, each with a pvzrh-unchanged regression; Checkpoint B runs the full E2E suite |
| BattleEngine goldens lock wrong semantics | C1 locks clock/order *before* subsystems arrive (C2); goldens only in C3 after subsystem tests pass |
| Parallel streams (event-pipeline-v2, VFX) collide | This program touches none of `Core/Events/`, `Fx/`; ingest edits in Wave B are surgical and listed per-file up front |
| FE work stalls the gate | A4 is the only FE task in Wave A; checkpoint A allows API-proven V1 with FE following |

## Checkpoints

- **Checkpoint A (= V1 internal gate):** SIM demo loop offline: seed Souls → ×10 pull → roster/codex/balance/pity counters exact → replay adds nothing → nickname/lock persist. All suites + guards green. **✅ PASSED 2026-08-21.**
- **Checkpoint B:** a synthetic `webrpg-1` batch through ingest leaves every pvzrh surface byte-identical (types, metrics, XP, grant session, retention); full E2E suite green. **✅ PASSED 2026-08-21.**
- **Checkpoint C (= match-source success criteria):** 3 golden battles deterministic; SIM e2e web match produces run+facts+XP+Souls with zero injector; replay and concurrent-PvZ tests green. **✅ PASSED 2026-08-21.**
- **Checkpoint D (= announced ship gate):** expedition dispatch→collect loop playable in FE against SIM; specimens soft-locked while deployed; rewards land through the one economy. **✅ PASSED 2026-08-21.**

## Refinement 2026-08-21 — C2→D detail (post-review; all wave decisions locked)

**C2 trait architecture (the one design fork, resolved at plan time):** the 14 traits split by mechanism —
- *Funnel-routed* (stat modifiers + HP mutations, honoring the spec's battle-local `EffectFunnel` mandate): berserker (low-HP ATK ramp), regenerator (heal pulse), soul-eater (on-kill heal), critical-hunter (crit-rate mod), guardian (adjacent damage share), swift (initiative mod), immortal (one death-refusal charge).
- *Engine-native behaviors* — **not expressible as FA opcodes** (FA was designed for board operations, not battle AI): coward (retreat below 25 % HP → leaves the battle alive), bloodthirsty (targets lowest-HP opponent), loyal (guards an adjacent ally, sharing damage), greedy (loot/Soul multiplier in the report), genius (specimen-XP multiplier), void-touched/chaos-marked (rare essence procs: small typed damage riders). These live in a `TraitBattleCatalog` read directly by the engine; recorded as engine semantics outside the FA vocabulary *by design*, so no guard is weakened. Contracts later layers obedience on top of the same keys.

**C2→C3 rule:** goldens bless only after ALL of C2 (any trait/status change invalidates them). Rounding canon (integer per-mille) is locked in the spec.

**Wave D shape:** the chain+events resolver is a *pure Core function* — `(tier, squad, seed) → [battle setups] + [event ticks] + rewards manifest` — so determinism/goldens cover expeditions end-to-end. The server resolves **lazily at collect** (the recorded seed seals the outcome, so lazy ≡ eager; recall before due resolves only elapsed ticks). Timers are `due_utc` rows; a SIM-only force-due hook makes them testable. Soft-lock = expedition membership rows consulted by both expedition dispatch and UniqueActor deploy (Cold-plane flag per the audit — no FSM change).

## Wave F — demon-fusion (planned 2026-08-21; spec: demons/spec-demon-fusion.md, all eight owner locks)

**Dependency graph:** F1 (StarPolicy+costs, pure) and F2 (RecipeCatalog, pure) are independent roots; F3 (FusionRoller) stands alone; F4 (schema + Retired filtering) gates the store work; F5 (star-merge transaction) needs F1+F4; F6 (recipe/promotion modes) needs F2+F3+F5; F7 (endpoints) needs F5+F6; F8 (squad star mods) needs only F4 and may land any time after it; F9 (FE) needs F7+F8; F10 closes.

**Key risks:** (1) "battle goldens untouched" is a claim F8 must PROVE — its verify step runs the golden suite explicitly; stars enter as ordinary ChannelMods in setups, never engine changes. (2) Retired leakage — consumption must vanish from roster/squad/dispatch surfaces but deploy/dispatch already require phase Roster, so only the roster query filters; a sweep test checks every surface. (3) Recipe-graph validation runs at startup (species catalog regeneration could orphan recipes) — eager-warm in Program.cs like the species/wave catalogs. (4) Cost numbers are spec-initial; any tuning mid-build is ask-first (balance boundary).

**Checkpoint F (= fusion success criteria):** merge/promote/recipe loops playable in SIM+FE; recipe catalog deterministic+validated; legendary reachable purely via fusion (E2E chain); forced-failure atomicity proven; stars swing web battles; battle goldens byte-identical; all suites + guards green; commit draft handed to owner.

## Wave P — patron-demon (planned 2026-08-21; spec: demons/spec-patron-demon.md; INJECTOR scope)

**Dependency graph:** PT1 (PatronPolicy, pure) roots everything; PT2 (Data: designation + fusion guard + earn hook) needs PT1; PT3 (Server endpoints + push) needs PT2; PT4 (injector grant plugin + overlay read) needs PT3 and is the **risk slice** — it starts with a read-only investigation of how match-owner grant overlays reach the derived compose (equipment mods are the precedent; if match-scope needs a small compose extension, that lands inside PT4 with its own regression test). PT5 (FE) needs PT3; PT6 (SIM e2e + guards) closes the offline half; PT7 is the LIVE owner gate.

**Earn-bonus cap semantics (locked at plan time):** the 50-cap is a SOUL cap. `PatronPolicy` owns the whole kill-earn shape when a patron is set: +1/kill plus +1 on every 10th earning kill, bonus withheld once total match kill-souls would pass 50 — the audited `KillEarn` path is untouched when no patron is set. Unit goldens pin the boundary (kills 44–50).

**Injector delivery:** server pushes the computed aura over the existing Command channel on `PatronUpdated` and on injector connect; the injector caches it and a grant-only `PatronPlugin` (Secondary discipline) enqueues the `patron.aura` modifier at `NotifyMatchStart`. Withdrawal rides the normal match-end session teardown. Zero per-hit work; the secondary-no-unity guard must stay green.

**Checkpoint P (two halves):** SIM half = spec criteria 1/2/3/5 green in CI (set/switch pricing, aura grant in session, bonus souls exact, fusion can't eat the patron, guards green) — full-auto reaches here. LIVE half = the owner's five-point checklist (spec §Testing) after a `deploy-play -NoServer`; the wave is DONE only when the owner signs it off. Commit draft handed at the SIM half.

### Task detail (mirrors the todo)

| Task | Slice | Verify |
|---|---|---|
| C2a | ActorHub-composed battle stats: per-actor derived snapshots (level + trait stat mods → 56-channel combat reads), hit/dodge/crit via `crit` RNG stream | crit/dodge swing fixed battles; channel reads match CombatDerivedReader |
| C2b | Battle-local `EffectFunnel` + `BattleEffectSink`: HP mutations merge/cap/apply to battle state | regen heals across rounds; opposite-sign sums net; caps hold |
| C2c | Battle-local `StatusRuntime` (catalog bootstrap, ResistanceEvaluator over derived profiles, clock = round × 1000 ms) | DoT kills through a round; CC skips a turn; resistance blocks an apply |
| C2d | `TraitBattleCatalog` — all 14 defs + behavior hooks (retreat, targeting, guard, multipliers, essence riders) | 14-row table test + behavior scenario tests (coward survives a wipe, loyal redirects damage…) |
| C3a | `BattleReportEmitter` → lean event vocabulary, `web:{matchKey}:{n}` ids | emitted list validates against the lean profile |
| C3b | 3 golden battles + N-seed hash suite (review I6) | hashes committed; CI diff = conscious version bump |
| C4a | Data: `rpg_web_match_log` + dedicated explicit-player single-transaction web insert + boot-sweep query | crash-window test: log without run row re-ingests on boot |
| C4b | Server: `WebMatchService` + SIM trigger + FE log/lawn feed filter by game (deferred from B4) | SIM e2e: run+facts+XP+Souls, replay adds nothing |
| C4c | Concurrency e2e: web match during live PvZ leaves grant session + ActiveBound untouched (extends the B3 test through the real service) | e2e green |
| D1 | spec-expeditions.md from the locked anchors (present per spec gate) | doc review |
| D2 | Data: `rpg_expeditions` (state Dispatched/Collected/Recalled, tier, squad, seed, due_utc) + `rpg_demon_materials` + soft-lock membership checks in deploy/dispatch | dispatch locks specimens; PvZ deploy of a locked specimen refuses; and vice versa |
| D3 | Core: `ExpeditionResolver` (pure chain+events: battles via WaveCatalog tier scaling, event ticks via `loot` stream, rewards manifest incl. wild-join rolls + materials) | resolver determinism + tick pro-rating goldens |
| D4 | Server: dispatch/collect/recall endpoints — collect runs resolver → battles through `WebMatchService` → specimen XP + wild-join mints (origin `expedition`) + materials + Souls, correlation-idempotent | SIM e2e full loop with force-due hook |
| D5 | Soul-ledger tail-trim + archive (the P4 deferral lands; XP-ledger pattern) | trim/rebuild test per spec success criterion 4 |
| D6 | FE: expeditions UI (dispatch from Active roster, tier pick, slot gating, live timers, collect reveal battle-by-battle + events, materials shelf) | Vitest for tick/pro-rate display logic |
| D7 | Checkpoint D e2e sweep + docs sync (README status, doc map) | all suites + guards green; commit draft |
| F1 | `StarPolicy` + `FusionCostTable` (pure): caps 3/4/5/5, sacrifice count n+1, +30‰/star, cost rows | policy table tests; cost lookups reject unknown bands |
| F2 | `DemonRecipeCatalog`: deterministic build over species catalog (one recipe per summonable rare+, band-below inputs, element relation), startup Validate | determinism (two builds identical); coverage + band/element properties; capture-only excluded |
| F3 | `FusionRoller` (pure): result traits (pick-one validated ∈ combined pool, rest seeded 1/2/2/3), variant roll; streams `fusion:traits`/`fusion:variant` | fixed-seed goldens; pick-one rejection |
| F4 | Data: `star`/`promoted` EnsureColumn, `rpg_demon_lineage`, `rpg_fusion_log`, `rpg_fusion_discovery`; roster filters Retired | schema smoke; Retired specimen absent from roster/BuildSquad/dispatch, lineage survives |
| F5 | Data: `ExecuteFusion` star-merge mode — ONE transaction (replay → validate → Souls+materials spend → consume → star++ → lineage → log) | forced mid-failure ⇒ zero rows; replay returns stored; locked/expedition/retired sacrifices refuse |
| F6 | Data: recipe mode (consume all, mint origin `fusion`, discovery + Souls bonus dedupe `recipe:{id}`) + promotion mode (max-star gate, slots grow, traits kept) | discovery pays once; promotion resets stars + keeps traits; recipe with undiscovered inputs still executes (discovery IS the experiment) |
| F7 | Server: `POST /api/fusion/preview` + `/execute` (corr ≤64, server seed, hub pushes) + `GET /{playerId}/recipes` (silhouette projection, no seeds on the wire) | E2E: merge, promote, discover, replay-adds-nothing; mismatched replay 400 |
| F8 | `WebMatchService.BuildSquad` star channel mods (+30‰ of level stats, flat ints) | squad setup carries mods; battle GOLDENS re-run byte-identical; starred squad beats mirror statistically |
| F9 | FE: `lib/bus/fusion.ts` + `#/fusion` lab (base slot, sacrifice tray with greyed locked/expedition, recipe silhouettes, cost have/need, pick-one selector, reveal) + star pips on roster cards | Vitest: cost math, pip render, silhouette gating |
| F10 | Checkpoint F sweep + docs sync (map status, README, spec header) + E2E legendary chain | all suites + guards green; commit draft |
