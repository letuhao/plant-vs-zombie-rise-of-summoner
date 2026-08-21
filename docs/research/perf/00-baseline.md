# Perf baselines — combat hot path

Plan + budgets: [`../../runbook/perf-probe-plan.md`](../../runbook/perf-probe-plan.md).
Raw windows: `_baseline-<scenario>.json` beside this file.

## Hardware truth (2026-08-20)

> **Superseded for the default configuration (noted 2026-08-22).** These numbers were taken
> **uncapped**, before the frame cap shipped. `InjectorLoop.ApplyFpsCap` now sets
> `Application.targetFrameRate = 60` by default at startup, and uncapping requires an explicit
> `FUSIONRPG_FPS_CAP=0`. With the cap on, the frame is **16.6 ms** and the plan's original
> **≤ 1.0 ms/frame** injector target applies. The section below remains correct for the
> uncapped case — read it as "what happens if you remove the cap", not as the current budget.

The game runs **~240 fps** uncapped on this machine (idle lawn: fpsAvg 239.6, frameMax 8.7ms).
Frame budget is therefore **4.16 ms**, not 16.6 — tighter than the plan assumed. Injector target
revised: ≤ 0.5 ms/frame avg.

## b2-live-x2 — live level, x2 speed, light-to-mid board (2026-08-20, 18 windows / 90s)

| summary | fpsAvg | frameMax | allocKb/5s | gen2 |
|---|---|---|---|---|
| b2-live-x2 | 187.0 | **233.6 ms** | 7042 | 0 |

| section | calls/s | totalMs/5s | avgUs | maxMs |
|---|---|---|---|---|
| loop.tick | 187.0 | 46.8 | 51 | 8.6 |
| **board.capture** | **21.9** | **1042.6** | **9554** | 15.8 |
| stats.resolve | 9.9 | 1.3 | 25 | 0.08 |
| hub.resolveDerived | 1.0 | 0.1 | 20 | 0.04 |
| **effect.onCapture** | 64.5 | **1163.4** | 4146 | 22.8 |
| effect.tickDots | 187.0 | 2.3 | 2.5 | 0.6 |
| **takeDamage.prefix** | 9.0 | **547.3** | **12324** | 22.9 |
| grants.scan | 9.6 | 0.1 | 2.1 | 0.1 |

Emits over 90s: combat=446, damage=1918, debug=892, other=2549 (~64 events/s).

### Reading

- **`board.capture` is the lag.** 9.55 ms per scan (2.3 frames at 240fps) with only ~9–30 zombies;
  ~208 ms of every second (≈21% of wall time) goes to `FindObjectsOfType` scans. Sections nest
  (`takeDamage.prefix` ⊃ `effect.onCapture` ⊃ `board.capture`), so the unique damage-path cost is
  ≈25–30% of wall time. This scales with entity count — a 200-entity board multiplies the per-scan
  cost, which matches "lags harder every wave".
- The 233 ms frame spikes line up with damage bursts (multiple TakeDamage in one frame → several
  9.5–23 ms hooks back-to-back).
- **Not implicated:** `stats.resolve` (25 µs), `grants.scan` (2 µs), gen2 GC (0 collections),
  transport queue (no drops). Plan items O4/O5/O7 are demoted; O1/O2 (kill the scans) and O3
  (fewer events → fewer capture triggers) are the whole game.

### Fix order (revised by data)

1. **O1** per-frame `BoardSnapshot` cache + invalidation on spawn/die hooks — collapses the
   3–5 scans per hit to ≤1 per frame.
2. **O2** incremental ptr→entity map from existing Start/InitHealth/Die hooks — removes scans
   entirely; required because even 1 scan/frame × 9.5 ms busts the 4.16 ms budget in heavy combat.
3. **O3** telemetry gating (~10 debug events/s in normal play today; each `Emit` re-enters
   MatchHost.Apply + OnCapture).

Re-measure the same scenario after each step; keep this file append-only per run.

## b2-live-x2-o1 — same play pattern, after O1 per-frame snapshot cache (2026-08-20, 18 windows)

| summary | fpsAvg | frameMax | allocKb/5s | gen2 |
|---|---|---|---|---|
| baseline | 187.0 | **17,600 ms** (wave freeze) / 233 ms (steady) | 7042 | 0 |
| o1 | 186.8 | **185.8 ms** | 6651 | 0 |

| section | calls/s | totalMs/5s | avgUs | vs baseline |
|---|---|---|---|---|
| board.capture | 21.6 | 1003.9 | 9334 | unchanged |
| effect.onCapture | 112.9 | 1236.7 | 2268 | avg halved (cache hits), more events this run |
| takeDamage.prefix | 10.2 | 616.4 | 12562 | unchanged |
| entity.apply | — | ~0 | — | negligible (new section) |

### Reading

- **Wave freezes eliminated** — burst frames now scan once instead of hundreds of times
  (frameMax 17.6 s → 186 ms).
- **Steady-state scanning unchanged** (~21 scans/s × 9.3 ms ≈ 20% of wall time): damage events
  land on ~21 distinct frames/s and each frame's first event pays a full scan. A per-frame cache
  cannot go lower — the scan itself must go. Confirms O2 as designed.

## O2 (2026-08-21): hook-fed InjectorEntityRegistry

`InjectorBoardSnapshot` now builds from a registry maintained by the existing Start/InitHealth/
Die hooks (same pattern as Fx.AnchorResolver); a full `FindObjectsOfType` scan runs only on the
registry's throttled resync (every 1024 frames) as a safety net for units the hooks never saw.
Expected: `board.capture` avg drops from ~9300 µs to interop-read cost (~a few hundred µs),
`takeDamage.prefix` follows. New `match.apply` section instruments the remaining uninstrumented
per-event cost. Note: dying zombies now leave the snapshot at `Die` (previously visible until
destroy) — targeting a dying zombie was skipped downstream anyway.

## b2-live-x2-o2 — after O2 registry (2026-08-21, 13 windows + live observation)

| section | baseline | O1 | O2 |
|---|---|---|---|
| board.capture avgUs | 9554 | 9334 | **336** |
| board.capture totalMs/5s | 1043 | 1004 | **0.9–150** |
| effect.onCapture avgUs | 4146 | 2268 | **325** |
| takeDamage.prefix avgUs | 12324 | 12562 | **7337** (rate 10/s → 1.9/s) |
| wave freeze | 3–17.6 s | none | none |

Live combat observation (board growing 20p/10z → 31p/23z, x2 speed): fps 127–222, frameMax
25–92 ms, no freezes, alloc 14–22 MB/5s, gen2 0. The headline 18.4 s "frameMax" in the o2
summary is an alt-tab/level-load artifact — unscaledDeltaTime counts focus-loss pauses as one
giant frame; no in-combat window exceeded 92 ms.

## v2-stress — event pipeline v2 live (2026-08-21, 24 windows / 120s, 60fps cap, drain on)

| section | o3 (before v2) | v2-stress | change |
|---|---|---|---|
| takeDamage.prefix avgUs | 4588 | **26** | 176× |
| takeDamage.prefix avgUs (vs v1 baseline) | 12324 | **26** | **467×** |
| effect.onCapture avgUs | 480 | **37** | 13× |
| board.capture avgUs | 25 | 57 | — (already solved) |
| drain.tick | — | 120 µs avg, 12 ms/5s (0.25%) | new |
| emits per 120s | ~21k | **8.6k** (damage/combat ≈ 0) | |
| gen2 | 0 | 0 | |

**v2 pipeline sections combined ≈ 1.8% of wall time — spec criterion 1 met for the event
pipeline.** Coalescing visible: 116 captures/s in → 38 pipeline executions/s (≈3:1).

**New top offender exposed:** `loop.tick` avg 9.2 ms/frame (53% of wall) with only ~50 ms/5s
in instrumented subsections — the cost is in *uninstrumented per-frame loop work*: prime
suspects `CheatActions.AutoCollectTick`/`TickContinuous` (per-frame `FindObjectsOfType` when
toggles are on — audit finding, never fixed) and the new `VfxDirector.Tick`. frameMax 2.6 s
is the known alt-tab/level-load artifact; real spikes were ≤85 ms.

## Stress scaling curve (2026-08-21 ~02:15, stress-fill API, waves frozen, 60fps cap)

| tier | board | fps | frameMax | pipeline share | gen2 / drops | verdict |
|---|---|---|---|---|---|---|
| 300z | 40p/302z | 59.8 | 39 ms | 3.67% | 0 / 0 | **PASS** |
| 600z | 50p/600z | 55.8 | 251 ms | ~8% (11.85% w/ double-count) | 0 / 0 | over bar — causes identified |
| 1000z (normal speed, plants ×50HP/×10ATK) | 64p/1006z | — | — | — | — | game playable ("lag but playable"); **server process crashed mid-run** — capture lost |

600z findings: (a) per-death `FlushForPtr` pops/re-appends the whole ring — O(ring) per death,
runs outside the drain budget (`effect.onCapture` 379ms/5s > `drain.tick` 222ms/5s);
(b) board snapshot rebuild cost scales with entities (57µs → 467µs at 640); (c) remaining
`effect.onEvent` ~957µs/record is largely real target-resolution work at density.
1000z finding: **server died under the spawn/death event burst** (~2000+ entity rows +
events in seconds) — injector unaffected, game playable; server stability under burst is now
a v3 item. Verdict-formula note: drain.tick contains onEvent-in-drain; summing both
double-counts — fix stress-test.ps1 arithmetic.

## v3 gate curve (2026-08-21 ~03:10 — NOTE: tiers 2–3 ran under owner-buffed sustained-war
conditions, far harsher than the v2 benchmark: plants ATK×5/HP×100/DEF×30, zombies HP×60)

| tier | fps | share (corrected) | gen2/drops | verdict |
|---|---|---|---|---|
| v3-300z | 59.5 | **4.44%** | 0/0 | **PASS** — A-module acceptance met |
| v3-600z (war) | 50.7 | 19.8% | 0/0 | over bar under war conditions |
| v3-1000z (war) | 7.2 | 22.5% | 1/0 | over bar; **server alive end-to-end (B4 ✓)**; fps floor largely base-game sim of 1002 buffed entities |

Loop decomposition worked perfectly: `cheat.autocollect` 0.07 ms/5s (was ~9 ms/frame),
`vfx.tick` 0.12, `pump.main` 0.22, zero dark cost. No ring drops at any tier.

### v4 — IMPLEMENTED 2026-08-21 (~03:25), same session

All four items below shipped and offline-verified (875 Core tests): death-flush per-frame
allowance with shed counter (`droppedDeathBudget`), `maxRecordUs` + expensive threshold
0.5→0.35, `combat.dispatch`/`funnel.flush` probe decomposition, `maxLatencyFrames` in the
perf window. **Pending live validation: one war-tier re-run on the v4 build** (deploy +
`stress-test.ps1 -Zombies 600` under buffed sustained-war conditions) — expected: death-flush
cost inside budget, `onCaptureOutside` collapses, latency measured directly.

### v5 — persistent-carry saturation fix (2026-08-21 ~04:00)

Storage split into ring (new records) + persistent coalesced carry with cursor (never
re-coalesced). Validated live (`v5-600z-war`, conditions escalated AGAIN to 2,123 damage
events/s — 30× the original war): carry churn 2.07M → 508k *and the number now means true
backlog (~300 records)*; drain progresses every frame instead of re-coalescing; first ring
drops appeared (123,858 — designed shed under input≫output, counted). fps 18.3 dominated by
base-game sim of ~118 attacks/frame; our share 11.8%.

**New finding fixed same hour:** `stats.resolve` ran per TakeDamage (2,123/s — v1 leftover
defense scaling, top allocator at density). Now cached by (DocumentRevision, PvzStatsRevision)
per side — hook cost drops to a comparison. Deployed with the next restart.

**Where the ceiling genuinely is now:** single mega-record effect execution (~4.4 ms atomic —
splitting or capping merged HitCount is the remaining knob if ever needed) and the base game
itself. At ≤900 events/s (v4 conditions) the pipeline held without drops.

### FINAL — resolve-cache build (2026-08-21 ~04:11, `final-600z-war`, 2,979 events/s)

First run where pipeline share FELL while load rose: **8.59%** at 2,979 events/s (vs 14.0% at
2,123). `stats.resolve` 2,123/s → 0.47/s; TakeDamage hook 12.5 µs → **1.46 µs**; gen2 back to
0. War-protocol trajectory across the night: share 19.8→15.1→14.0→8.6% while event rate rose
70→906→2,123→2,979/s — ~150× cheaper per event end to end. fps 12 at the final tier is the
base game simulating ~246 attacks/frame; drops (158k) are the designed shed in that regime.
Remaining optional knob: mega-record execution splitting (~4.5 ms atomic) — only relevant
beyond the base game's own renderable density. **Perf campaign closed.**

### v4 targets (named by the war-tier data — original list)

1. **Death-flush inside the budget**: FlushForPtr processed 832 ms/5s outside drain.tick at
   1000z kill rates — batch death flushes per frame or process them as priority records in
   the next drain instead of synchronously in the hook.
2. **Per-record budget overshoot**: budget is checked between records; a ~1–1.5 ms effect
   execution blows through the 2 ms clamp. Sub-record accounting or tighter expensive-class
   thresholds.
3. **Per-record cost decomposition**: instrument inside OnDrained (target resolve vs funnel
   vs status) — 1 ms/merged-record is the real ceiling under sustained war.
4. Carried backlog telemetry: 38.8k (600z war) / 1.85M (1000z war) carries — add
   effect-latency-frames to the probe window so delay is measured, not inferred.

### Next iteration (v3 targets)

1. Instrument `InjectorLoop` subsections (`vfx.tick`, `cheat.continuous`, `cheat.autocollect`,
   `poll.board`) — find the 9 ms.
2. Registry-fy `AutoCollectTick` (coin scans per frame) and `TickContinuous`.
3. Spec criterion 2 formally near-miss (≈5 ms per 5s per 100 events/s vs the 2 ms letter) —
   intent met at 0.7% wall; revisit the number or the pipeline after the loop work lands.
4. **Death-flush batching** (600z finding a): index pending records by ptr, or mark-dead and
   let the next drain handle them first — removes the O(ring)-per-death churn outside budget.
5. **Incremental board snapshot** (600z finding b): maintain the snapshot in place on
   registry add/remove instead of rebuild-on-invalidate; cost stops scaling with entities.
6. **Server burst stability** (1000z finding): server process died under a ~2000-row
   spawn/death event burst — reproduce headless (POST synthetic events), find the crash
   (likely SQLite insert pressure or OOM in EventIngest), add backpressure. Injector is
   already immune (queue + drop policy).
7. stress-test.ps1 verdict arithmetic double-counts nested sections (drain contains onEvent);
   subtract the overlap.

### Remaining (next iteration)

1. **Capture count went up** (~60–113/s vs 21/s) — cheap now (~0.34 ms), ~3% of wall time, but
   something invalidates or captures near per-frame; find the caller, consider append-in-place
   instead of full invalidate on spawn.
2. `takeDamage.prefix` still ~7.3 ms per hit at low rate — inner cost is no longer capture;
   profile candidates: `TryStampDamageFrom` interop casts, double Emit per hit, `stats.resolve`
   (avg rose to 850 µs — was 25 µs; investigate).
3. 25–92 ms frame spikes remain under load — separate base-game share (B8/B9 controls) before
   attributing.

## b2-live-x2-o3 — after event-pipeline round (2026-08-21, 18 windows, fps capped 120)

fpsAvg 112 (at cap), frameMax 87.7 ms, gen2 0. `board.capture` avg **24.8 µs** (was 9,554 —
385× cheaper), totals 0.1% of wall time. Remaining cost: `effect.onEvent` 71.8/s × 1.76 ms
(≈14%) — decomposition showed it's the effect-action execution path, dominated by per-action
scene scans in `InjectorEffectActionSink` and always-on telemetry.

Root causes found and fixed in the next round (o4, deployed 2026-08-21):

1. **`StatsConfig.LogDamage` defaulted to `true`** — every fresh session (and every future
   player) paid per-hit `*.damage` + forced `combat.hit` emission unless a server pull said
   otherwise. Default flipped to false; telemetry is now opt-in.
2. `InjectorEffectActionSink` resolved FA10/status targets via `FindObjectsOfType` per action —
   now O(1) `InjectorEntityRegistry.FindZombie/FindPlant` with scan fallback on registry miss.
3. `debug.combat.packet` / `debug.combat.overlay` traces emitted per hit in normal play — now
   gated behind `DebugRuntime.SessionActive` (LIVE prove packs must run inside a session, which
   the checklist already does).
4. Grant store sorted + allocated per call at ~1000 calls/s — sorted view now cached, rebuilt
   only on grant mutation; effect actions pre-sorted at catalog load.

Measurement pending: b2-live-x2-o4 (also the first run that can honestly answer the fps-cap
question — remaining drops at that point are the base game's own cost).
