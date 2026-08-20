# Perf baselines — combat hot path

Plan + budgets: [`../../runbook/perf-probe-plan.md`](../../runbook/perf-probe-plan.md).
Raw windows: `_baseline-<scenario>.json` beside this file.

## Hardware truth (2026-08-20)

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
