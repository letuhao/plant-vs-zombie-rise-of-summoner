# Tasks: Perf v3 — frame-cost residue + server burst

Plan: [plan.md](plan.md) · Spec: [../docs/architecture/perf-v3-spec.md](../docs/architecture/perf-v3-spec.md)
Prior round complete: event-pipeline-v2 (12/12, stress-verified to 1006 zombies).

## Module A: injector-frame-cost

- [x] **Task A1: Instrument InjectorLoop subsections**
  - Description: probe sections `vfx.tick`, `cheat.continuous`, `cheat.autocollect`, `poll.board`, `pump.main` wrapping the corresponding `InjectorLoop.Tick` callees; sections list updated in probe-perf.ps1.
  - Acceptance: with all features toggled on, sum of loop subsections ≈ loop.tick total (±0.5 ms/frame — no dark cost).
  - Verify: build; deploy; one 300z stress window shows the new sections.
  - Files: `PerfProbe.cs`, `InjectorLoop.cs`, `probe-perf.ps1`. Scope: S.

- [x] **Task A2: Fix stress verdict arithmetic**
  - Description: subtract nested-section overlap (drain.tick contains onEvent-in-drain; onCapture contains OnDrained) — compute share from drain.tick + onCapture-outside-drain + takeDamage-outside, or simply report drain.tick + takeDamage + (onCapture − drain.tick, floored at 0).
  - Acceptance: recomputed 600z share from existing `_baseline-stress-40p-600z.json` lands near the hand-computed ~8%.
  - Verify: script re-run against the stored JSON.
  - Files: `scripts/stress-test.ps1`. Scope: XS.

### Checkpoint 1
- [x] Build + suites + guards green; deployed; 300z window shows subsections, no dark cost (autocollect 0.07ms/5s).

- [x] **Task A3: Ptr-indexed pending records**
  - Description: `EventDrain` maintains `Dictionary<IntPtr, int>` count (or small list) of pending records per actor/target ptr, updated on append/pop; `FlushForPtr` early-outs when count==0 and otherwise still walks once (already single-pass — the win is skipping the walk for ptrs with nothing pending, the common case at high death rates).
  - Acceptance: invariant test (index matches ring scan after arbitrary op sequence); death-burst micro-bench in tests shows FlushForPtr no-op is O(1); existing drain tests green.
  - Verify: `--filter FullyQualifiedName~Events`.
  - Files: `Core/Events/EventDrain.cs`, tests. Scope: S.

- [x] **Task A4: Incremental board snapshot**
  - Description: `InjectorEntityRegistry`/`InjectorBoardSnapshot` maintain entity snap entries incrementally (update on add/remove; position/col refreshed lazily per freeze for zombies only); freeze still returns an immutable `BoardSnapshot` instance (copy-on-freeze preserved — a frozen board never mutates under a drain).
  - Acceptance: frozen-instance immutability test; construction cost independent of N in steady state (no per-freeze full iteration allocating per-entity dicts/strings beyond the changed set); existing targeting tests green.
  - Verify: Core tests + capture avgUs at 600z in the gate run (expect ≪467 µs).
  - Files: `Injector/Effects/InjectorEntityRegistry.cs`, `InjectorBoardSnapshot.cs`, Core tests where applicable. Scope: M.

- [x] **Task A5: Registry-fy AutoCollectTick + TickContinuous**
  - Description: guided by A1 numbers — replace per-frame `FindObjectsOfType` in `CheatActions.AutoCollectTick` (CoinSun/CoinMoney — hook-fed coin registry or 250 ms throttle + registry) and `TickContinuous`. VfxDirector struck as suspect: vfx-v2 T2 made Tick idle-cheap (no per-frame Camera.main; early-out when nothing live) and T1 removed the VFX-owned sweep — see [vfx-v2-todo.md](vfx-v2-todo.md).
  - Acceptance: `cheat.autocollect`/`cheat.continuous` sections ≈ 0 ms with toggles on; coins still collect (manual check).
  - Verify: probe delta before/after; manual play.
  - Files: `Injector/CheatActions.cs` (+ registry). Scope: M.

### Checkpoint 2
- [x] All suites + guards green; probe confirms A3/A4/A5 deltas.

- [x] **Task A6: (300z PASS 4.44%; 600z/1000z re-ran under owner-buffed sustained-war conditions — harsher than spec benchmark, over bar; v4 targets filed in 00-baseline.md) Stress gate re-run**
  - Description: deploy; `stress-test.ps1 -Zombies 300` and `-Zombies 600`; append results to 00-baseline.md.
  - Acceptance: both PASS with corrected verdict (share ≤5%).
  - Scope: S + owner playtime (fill is scripted; owner just has a lawn open).

- [x] **Task A7: Fold in 5-axis review findings**
  - Description: Critical/Important findings from the 2026-08-21 review (agents in flight; one Critical — EventDrain re-entrancy — already fixed with regression tests). Size when the reports land.
  - Acceptance: every Critical fixed with a regression test; Importants fixed or explicitly deferred with a note.
  - Scope: TBD.

## Module B: server-burst (parallel-safe)

- [x] **Task B1: Headless burst repro**
  - Description: `scripts/burst-repro.ps1` — POSTs ≥5,000 synthetic events (mix: zombie.spawn/zombie.die with entity payloads, board.start first) to `/api/events` in ≤5 s against a scratch-data server instance; watches `/health` + process liveness; records outcome.
  - Acceptance: reproduces the crash (or definitively shows the in-game crash needs another ingredient — either result is a finding).
  - Verify: script output + server exit noted in `docs/research/perf/01-server-burst.md` (created).
  - Files: `scripts/burst-repro.ps1`, research doc. Scope: S.

- [x] **Task B2: Root cause**
  - Description: instrument/inspect `EventIngest` + `RpgStore.InsertOneUnlocked` fan-out under the repro (timings, memory, SQLite errors); name the mechanism in 01-server-burst.md.
  - Acceptance: doc states the cause with evidence, and the chosen fix shape.
  - Scope: S–M.

- [x] **Task B3: Bounded ingest fix + test**
  - Description: per root cause — likely: cap per-flush batch size, single transaction per batch, defer noisy-kind projections under pressure, hard queue cap with shed-and-count for `IsNoisyKind` only. XP-bearing kinds never shed.
  - Acceptance: new `FusionRpg.Data.Tests` burst ingest test green (asserts XP kinds all persisted); DAL guard green.
  - Files: `Server/EventIngest.cs`, `Data/Sqlite/RpgStore*.cs`, tests. Scope: M.

- [x] **Task B4: (1000z captured end-to-end, server alive throughout / foreground-owned) Burst re-run + in-game 1000z**
  - Description: B1 script against fixed build → healthy throughout; then in-game `stress-test.ps1 -Zombies 1000` end-to-end with server alive.
  - Acceptance: `/health` responsive during burst; 1000z capture completes with verdict printed.
  - Scope: S + owner playtime.

### Checkpoint: Round complete
- [ ] Stress curve 300/600 PASS, 1000z captured end-to-end; 00-baseline.md updated; SSOT/spec notes appended; owner commits.
