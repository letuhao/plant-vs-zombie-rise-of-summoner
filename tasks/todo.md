# Tasks: Event Pipeline v2 — Phase 2

Plan: [plan.md](plan.md) · Spec: [../docs/architecture/event-pipeline-v2-spec.md](../docs/architecture/event-pipeline-v2-spec.md)
Test command: `dotnet test tests\FusionRpg.Core.Tests` (focused: `--filter FullyQualifiedName~Events`)
Build: `dotnet build src\FusionRpg.Injector.BepInEx\FusionRpg.Injector.BepInEx.csproj -c Release -p:GameDir=$env:FUSIONRPG_GAME_DIR -p:OutputPath=artifacts\perf-build\`

## Phase 1: Foundation (pure Core, offline)

- [x] **Task 1: GameEventRec struct + kind enum + interning**
  - Description: `GameEventKind` enum for the hot tier, readonly `GameEventRec` struct (values only, per SSOT §4b.6), and small intern tables (matchKey, grantId → int) with clear-on-match-end.
  - Acceptance: struct has no reference-type fields except via intern indices; intern round-trips; kind enum covers combat.hit, plant.damage, zombie.damage, bullet.init, status hooks, chain-synthetic.
  - Verify: new `Events/GameEventRecTests` green; all existing Core tests green.
  - Dependencies: none. Files: `src/FusionRpg.Core/Events/GameEventRec.cs`, `tests/.../Events/GameEventRecTests.cs`. Scope: S.

- [x] **Task 2: Counter/stack math takes HitCount**
  - Description: `StatusRuntime.RecordCounterHit` accepts `hits` (default 1), advances by N, fires one burst per threshold crossing; `EffectProcPolicy` `max_stacks` consumes per hit.
  - Acceptance: `hits=1` byte-identical to today (existing CombatCounterTests untouched and green); N=3 on a 5-threshold fires at cumulative 5 not 15; crossing twice in one call fires one burst.
  - Verify: new tests + existing 733 green.
  - Dependencies: none. Files: `src/FusionRpg.Core/Status/StatusRuntime.cs`, `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs`, new tests. Scope: S.

- [x] **Task 3: Chance proc closed-form merge**
  - Description: `EffectProcPolicy.TryPass` chance gate accepts hit count n: single roll vs `1−(1−p)^n` (owner decision #2). n=1 identical to today.
  - Acceptance: n=1 path unchanged (existing tests green); distribution test: for p=0.2, n=5, pass rate ≈ 0.672 over 10k seeded trials (±2%).
  - Verify: new `Events/ProcMergeMathTests` green.
  - Dependencies: none. Files: `src/FusionRpg.Core/Effects/EffectProcAndOwner.cs`, tests. Scope: S.

### Checkpoint 1
- [x] All new Phase-1 tests green; all 733 existing Core tests green untouched; guards green.

## Phase 2: Ring + coalescer (pure Core)

- [x] **Task 4: GameEventRing**
  - Description: fixed-capacity (4096) single-writer ring with drain cursor; overflow drops droppable kinds with a counter, never drops non-droppable (backpressure: overwrite oldest droppable, else grow-once diagnostic mode).
  - Acceptance: FIFO order preserved; append during drain is safe (records land after current drain cursor); overflow counters visible.
  - Verify: `Events/GameEventRingTests` incl. append-during-drain; all suites green.
  - Dependencies: T1. Files: `src/FusionRpg.Core/Events/GameEventRing.cs`, tests. Scope: S.

- [x] **Task 5: EventCoalescer**
  - Description: merge window over pending records: key `(Kind, MatchKey, Side, ActorPtr, TargetPtr, TypeId, TargetTypeId)`; sums Amount, accumulates HitCount; never merges ChainDepth>0, SourceGrantId set, or paired-taken records across their dealt partner.
  - Acceptance: SSOT §4b.2 exclusions all enforced; per-target FIFO survives; PairId adjacency preserved.
  - Verify: `Events/EventCoalescerTests`; funnel 1e9-cap extreme-amount test.
  - Dependencies: T1, T4. Files: `src/FusionRpg.Core/Events/EventCoalescer.cs`, tests. Scope: M.

### Checkpoint 2
- [x] Ring + coalescer tests green; all suites green.

## Phase 3: Drain (pure Core)

- [x] **Task 6: EventDrain core — budgeted FIFO**
  - Description: processes coalesced records into `EffectEventDto` → `Bag.OnEvent`; injectable time source; budget parameter (caller computes 10% of frame); carry-over queue.
  - Acceptance: budget exhaustion carries remainder in order; a grant's actions never split; zero-record drain is allocation-free.
  - Verify: `Events/EventDrainTests` (test groups 1, 4 from spec).
  - Dependencies: T1–T5. Files: `src/FusionRpg.Core/Events/EventDrain.cs`, tests. Scope: M.

- [x] **Task 7: Barriers, chain records, session bypass**
  - Description: ptr-flush API (drain all pending records for a ptr, called by death hooks before ForgetEntity); drain-generated records inherit ChainDepth+1 (limit clamped 1..8, default 6, mechanism hard-coded); generation cap ≤3/frame with overflow to next frame; session mode = no budget, no coalescing.
  - Acceptance: death-flush ordering test (OnDeath sees grants); depth-6 chain terminates; generation-4 records processed next frame; session mode preserves v1 event-for-event behavior.
  - Verify: `Events/EventDrainBarrierTests` (test groups 1, 5, 6).
  - Dependencies: T6. Files: `EventDrain.cs`, tests. Scope: M.

- [x] **Task 8: Cost classes**
  - Description: classify expensive records (match-scoped ModifyStat, all-target packets, ClearStatus-bearing plans) — max 1 expensive record per frame, own carry queue.
  - Acceptance: two expensive records in one frame → second drains next frame; cheap records unaffected behind an expensive one (no head-of-line block beyond budget).
  - Verify: `Events/EventDrainCostClassTests`.
  - Dependencies: T6. Files: `EventDrain.cs`, tests. Scope: S.

### Checkpoint 3
- [x] Spec test groups 1–6 all green; 733 + new tests green; guards green.

## Phase 4: Injector wiring

- [x] **Task 9: Record sites** *(landed flag-off — `EventDrainHost.Enabled=false` keeps legacy behavior byte-identical until T10 wires the drain tick; zombie.status StatusHook wiring deferred to backlog, web chips keep legacy emits)*
  - Description: hot-kind hook sites (TakeDamage ×2, AttackPlant, status hooks, bullet.init-when-consumed) append `GameEventRec` instead of calling `OnCapture`; PairId stamped for dealt/taken of one physical hit; payload dicts for transport deferred (built at drain only if kind emits).
  - Acceptance: hooks allocate nothing on the record path; `ref damage` scaling unchanged; non-hot kinds untouched.
  - Verify: build vs game dir; guards; Core tests.
  - Dependencies: T7. Files: `GameHooks.cs`, `GameCaptureHooks.cs`, `src/FusionRpg.Injector/Effects/EventDrainHost.cs` (new). Scope: M.

- [x] **Task 10: Drain host + death flush + transport**
  - Description: drain runs in `InjectorLoop.Tick` before `TickDots` sharing one board freeze; death/board-end hooks call ptr-flush/match-flush; drained records that still need transport build payloads there and Enqueue.
  - Acceptance: DoT damage and drained damage merge into one FA10 per target per frame; death ordering preserved live; server still receives every kind it consumed before.
  - Verify: build + guards + all suites; manual: web log shows hot kinds while a session is active.
  - Dependencies: T9. Files: `EventDrainHost.cs`, `InjectorLoop.cs`, `EffectRuntime.cs`. Scope: M.

- [x] **Task 11: FPS cap default + probe counters**
  - Description: `FUSIONRPG_FPS_CAP` unset → 60 (owner decision #3), `0` → uncapped; PerfProbe `drain.tick` section + ring depth/dropped/carryover counters in the 5s window.
  - Acceptance: default launch caps at 60; probe window shows drain stats.
  - Verify: build; probe smoke via `/api/perf/recent`.
  - Dependencies: T10. Files: `InjectorLoop.cs`, `PerfProbe.cs`, `PerfReporter.cs`, `probe-perf.ps1`. Scope: S.

### Checkpoint 4
- [x] Injector builds vs game dir; 4 guards green; all 5 test suites green.

## Phase 5: Live verification

- [ ] **Task 12: Stress verification + baseline record**
  - Description: deploy; owner plays heaviest board at max speed with grants active; capture `v2-stress` probe scenario; re-run LIVE checklist F-rows inside a session; append results to `docs/research/perf/00-baseline.md`.
  - Acceptance: spec Success Criteria 1–3 and 5–6 met, or findings drive an iterate loop.
  - Verify: probe summary table + checklist rows.
  - Dependencies: T11 + owner playtime. Scope: S (code) + live session.

### Checkpoint: Complete
- [ ] All spec success criteria met; SSOT updated with as-built notes; owner writes decisions.md row; commit message drafted for owner.
