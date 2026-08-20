# Spec: Perf v3 round — frame-cost residue + server burst stability

Status: draft — awaiting owner review. Evidence base:
[`../research/perf/00-baseline.md`](../research/perf/00-baseline.md) (stress scaling curve,
"v3 targets" list). Prior locked design: [`event-pipeline-v2-ssot.md`](event-pipeline-v2-ssot.md)
— v3 changes nothing in that contract.

## Capability map (Phase 0, approved shape)

| Module | Responsibility | Depends on |
|---|---|---|
| `injector-frame-cost` | in-game frame cost residue after v2 | — |
| `server-burst` | server survives mass spawn/death event floods | — |

Build order: injector-frame-cost → (server-burst parallel-safe).

## Module: injector-frame-cost

### Objective
After v2, the event pipeline is ~3.7% of frame time at 300 zombies but ~8% at 600, and one
session showed 9.2 ms/frame of *uninstrumented* loop work. v3 closes the gap: 600-zombie tier
under the 5% bar, loop residue named and its top offender fixed, so the stress curve stays
flat as the summoner features stack on top.

### Acceptance criteria
1. `stress-test.ps1 -Zombies 600` verdict PASS (share ≤5%, corrected arithmetic).
2. Every `InjectorLoop.Tick` callee ≥0.1 ms/frame appears in a probe section (no dark cost).
3. `AutoCollectTick`/`TickContinuous` do zero `FindObjectsOfType` (registry-fed).
4. Death flush is O(pending-for-ptr), not O(ring): ptr-indexed pending records.
5. Board snapshot construction cost independent of entity count in steady state
   (incremental maintenance; full rebuild only at registry resync).
6. All existing tests green; guards green; LIVE F-rows unaffected.

### Work items (from measured findings)
| # | Item | Evidence |
|---|---|---|
| F1 | Probe sections: `vfx.tick`, `cheat.continuous`, `cheat.autocollect`, `poll.board`, `pump.main` | loop.tick 9.2ms dark (baseline §v2-stress) |
| F2 | Registry-fy AutoCollectTick (CoinSun/CoinMoney via hook-fed registry or throttle) + TickContinuous | audit: per-frame scans |
| F3 | Ptr-index pending drain records (Dictionary<IntPtr, small list> alongside ring) | 600z: flush churn outside budget |
| F4 | Incremental `BoardSnapshot` (mutate-on-add/remove, copy-on-freeze semantics preserved) | capture 57→467µs scaling |
| F5 | stress-test.ps1: subtract nested-section overlap in verdict | double-count noted |
| F6 | Fold in Critical/Important findings from the 2026-08-21 five-axis review | review in flight |

### Commands / structure / style / testing
Same as [`event-pipeline-v2-spec.md`](event-pipeline-v2-spec.md) — same test suites, guards,
deploy, probe scripts; new code lives beside the v2 files it refines
(`Core/Events`, `Injector/Effects`, `Injector/Host`, `Core/Combat/BoardSnapshot`).
Offline-first: F3/F4 get Core unit tests before any deploy; F2 verified by probe delta.

### Boundaries
Same three tiers as the v2 spec. Additionally **never**: change v2 SSOT semantics (coalescing
key, pair suppression, chain depth, budget contract) — v3 is implementation-only.

## Module: server-burst

### Objective
The server process died during the 1000-zombie stress fill (≈2,000 entity/event rows in
seconds) while the game ran on unaffected. Players will hit this via effect-driven mass
spawns. The server must degrade (defer, batch, shed noisy rows) — never die.

### Acceptance criteria
1. Headless repro exists first: a script POSTs a synthetic burst (≥5,000 events incl.
   spawn/die with entity rows in ≤5 s) — reproduces the crash on the current build.
2. Root cause named in `docs/research/perf/01-server-burst.md` (suspects: SQLite insert
   pressure in `EventIngest`/`RpgStore.InsertOneUnlocked` fan-out, OOM, unbounded batch).
3. Post-fix: same burst → server stays healthy (`/health` responds throughout), ingest lag
   allowed, no data-loss for XP-bearing kinds, noisy kinds may shed with a counter.
4. `FusionRpg.Data.Tests` + a new burst ingest test green.

### Boundaries
- Always: SQL stays inside `FusionRpg.Data` (DAL guard); XP-bearing events never shed.
- Ask first: schema changes; changing the `events` table retention/compaction behavior.
- Never: fix by silently dropping lifecycle/XP kinds; move ingest off SQLite without a
  decisions.md row.

## Success criteria (round complete)
Stress curve re-run at 300/600/1000: 300 and 600 PASS; 1000 tier captures end-to-end with the
server alive; results appended to 00-baseline.md. Owner commits.
