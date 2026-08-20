# Event Pipeline v2 — record-then-drain SSOT

Status: **DRAFT — audit in progress** (2026-08-21). Companion perf data:
[`../research/perf/00-baseline.md`](../research/perf/00-baseline.md). Needs a `decisions.md`
row before implementation locks behavior.

## 1. Why v1 cannot ship

v1 runs the full pipeline synchronously inside every Harmony hook on the game's main thread:

```
game hook → dict payload → MatchHost.Apply → OnCapture → TryMap → dedupe
          → Bag.OnEvent → grant match → proc → actions → sink writes → Enqueue
```

Measured (b2-live-x2 late waves, 57–71 plants / 32–48 zombies): ~1,250 events/s,
`effect.onEvent` = 3.6–3.9 s per 5 s window (**~75% of wall time**), fps 13–24. Event rate is
unbounded — it scales with `zombies × plants × fire rate × game speed`. Some modes run 10× the
zombie count → 10,000+ events/s. Per-event cost has a floor (~0.7 ms), so no per-event
optimization survives that multiplication. A realtime tick is a hard budget; v1 has no budget
at all.

## 2. Design goals (hard requirements)

| # | Goal |
|---|---|
| G1 | Per-frame mod cost is **bounded by construction** — a drain budget, independent of event rate |
| G2 | Hook-side cost is O(1), allocation-free, and near-zero when no feature needs the event (trigger mask) |
| G3 | Effect/gameplay semantics preserved (proc counters, ICD, dedupe, chains) or changed only by explicit decision |
| G4 | Telemetry is opt-in; debug sessions may bypass budgets for fidelity |
| G5 | Weak-PC first: worst case degrades to *delayed effects*, never to frame drops |

## 3. v2 architecture

### 3.1 Record (inside hooks — the only work on the game's call stack)

- A fixed-capacity **ring buffer of typed structs** (`GameEventRec`: kind enum, actor/target
  ptr as `IntPtr`, int amount, int typeId, short flags, int frame). No dictionaries, no
  strings, no allocation.
- A **trigger mask** (bitset of event kinds anything currently consumes: live grant triggers ∪
  match-FSM needs ∪ telemetry-on ∪ debug session). Rebuilt only on grant change / session
  toggle / stats change. Hook fast path: `if ((mask & kindBit) == 0) return;`
- Work that the game needs immediately stays synchronous (audit will enumerate; known:
  TakeDamage `ref damage` stat scaling).

### 3.2 Coalesce (at drain entry)

- Same-target damage records within one drain window merge: summed amount, hit count carried
  (`MergedCount`), first/last actor kept. Proc semantics per §4 (audit decides counter
  handling).
- High-rate spawn records (`bullet.init`) collapse to counts unless an OnSpawn grant or debug
  session needs instances.

### 3.3 Drain (once per frame, budgeted)

- Runs from `InjectorLoop.Tick`, FIFO, under a time budget (default ~1.5 ms, configurable).
- Budget exhausted → remaining records carry to next frame (bounded backlog; overflow policy:
  coalesce harder, then drop droppable kinds with a counter, never drop death/board lifecycle).
- Dict payloads are built **only here**, and only for consumers that need them (transport batch,
  debug session). MatchHost/effects consume the typed record directly where possible.
- Events generated *during* draining (chains, counter bursts) append to the buffer and respect
  ChainDepth exactly as today; the drain processes them in the same or next frame within budget.

### 3.4 What stays as-is

- RpgClient transport (already batched/async/capped).
- StatusRuntime 100 ms DoT grid (already coalesced), fed board state by the drain.
- PerfProbe — v2 adds `drain.tick` and buffer depth/drop counters as permanent regression
  tripwires.

## 4. Producer audit findings (2026-08-21)

### 4.1 The waste profile

- `MatchHost.Apply` folds only 7 kinds; the effect adapter maps only 11. **Every other kind
  (~80) pays the full Emit fan-out to be dropped by both consumers.** The trigger mask (§3.1)
  eliminates this class entirely.
- **Nested emit fan-out**: one `combat.hit` with live grants synchronously produces 3–6 nested
  Emits (`debug.effect.plan`, `debug.effect.fired` per action — ungated, `pvz.status.apply`,
  status-resisted), each re-entering the full pipeline. v2: sink-side telemetry becomes
  drain-side records, gated by session.
- Per-frame background waste independent of events: `PollBoard` builds `LiveBoard()` dumps
  (~20 interop reads with delegate allocs) on near-every sun tick (`board.economy`);
  `LevelName()` reads 3 UI texts every frame; `Plant.LimHealth` prefix runs per-plant-per-frame.
  `VfxDirector` emits `debug.fx.shown`/`.skipped` per cue, ungated.
- `bullet.init`/`bullet.place`/`combat.hitland` are the per-bullet tier; `zombie.status`
  (freeze/cold/poison hooks) fire per snow-pea impact.

### 4.2 CANNOT-DEFER list (hook-side work that stays synchronous)

1. `TakeDamage` prefixes: `ref damage` mutation (god-mode zeroing + `StatMath.ScaleIncoming`);
   `before` value exists only in the prefix.
2. `Plant.LimHealth` prefix (skips original, writes health under gate).
3. `CreatePlant.MixEvent` before/after row census (also: uses 2× `FindObjectsOfType` per
   fusion — separate fix: use the entity registry).
4. `SpawnAdmit.TryAdmit` and unique-pending gates (return values gate the spawn).
5. `NoteZombieDead` dedupe (`DeadZombies`), registry add/remove + snapshot invalidate,
   `CheatState.Select`, `Board`/`MatchKey` assignment, `Applied` gate.
6. `EntityBaseline` capture (must read pre-modification values).
7. **Entity field snapshots**: die/damage payload fields (`type`, `row`,
   `transform.position`) must be read at record time — a deferred `Il2CppObjectBase` read is
   use-after-free. Ring-buffer records carry values, never object refs.
8. **Tick assignment at record time** — `DealtIdentity` correlates dealt→taken within 8 ticks;
   drain-time ticks would collapse a frame into one tick and invert the dedupe.
9. **Ordering:** strict FIFO. `board.start` before entity events; `*.die` processed before
   `ForgetEntity`/grant-withdraw (v2 defers `ForgetEntity` into the same drain slot as the
   death record, or snapshots grants with the record).

### 4.3 Semantic risk: board state at drain time

`FreezeBoard` today captures board state per event; a single end-of-frame drain gives every
event the frame's *final* board (targeting semantics shift for events preceding a same-frame
spawn/death). Mitigation: the frame-cached snapshot already invalidates on spawn/death, so the
drain re-captures at each lifecycle record boundary within the FIFO — events between two
lifecycle records share one snapshot, which matches v1 within-frame behavior closely enough;
`SelectedPtr` is snapshotted per record (single field in the struct).

### 4.4 Re-entrancy contract for the recorder

Known reentrant chains (all single-thread, currently survive via reentrant locks):
Emit-catch → `CheatState.Error` → Emit; `MatchHost.Apply` under Gate → `ClearAll` →
`ReapplyAllLiving` → Emit → Apply; sink → `DebugRuntime.Emit` → OnCapture (2+ deep);
`SpawnCatalog.Note` emits while holding its own gate. The v2 recorder must be append-only and
lock-free (single-writer main thread), so recording from inside a drain is always safe;
drain-generated records process in the same or next frame under the budget.

Threading: **all producers are main-thread** (verified); SignalR/thread-pool paths already use
record-then-drain (`CheatCommandRunner`, catalog pump) — the pattern v2 generalizes. One
pre-existing race to fix independently: `CheatState.PvzStatsMods` is replaced from a pool
thread while `TakeDamage` prefixes read it.

## 4b. Pipeline audit findings (2026-08-21)

### 4b.1 Hard invariants

- **Lifecycle barriers**: `board.start`/`board.end`/`match.result` own `ClearAll` and rewrite
  `MatchKey`. They stay synchronous (or force drain-to-empty first). `MatchKey` is stamped
  into the record at record time.
- **Death ordering**: `OnDeath` must see `entity:` grants — death records drain before
  `ForgetEntity` (v2 defers `ForgetEntity` into the same drain slot). Grant withdraw has
  stat-writer side effects (`OnRemoved` → `ModifyStat remove` → reapply), not just bookkeeping.
- **Dealt→taken pairing**: today an event-count heuristic (`DealtIdentity`, "<8 ticks" =
  <8 *events*, rate-dependent and fragile). v2 replaces it with a causal `PairId` stamped by
  the hook that emits both records for one physical hit. Per-target FIFO preserved.
- **Grant order** (priority desc) and **action atomicity** (Seq order, stop-on-failure; a
  grant's actions never split across a budget boundary).
- **`SourceGrantId` self-proc guard and `ChainDepth`** must ride in the record. Chain records
  (`ChainDepth > 0` or `SourceGrantId != null`) are **never coalesced**.
- **Funnel semantics** unchanged: one FA10 per (target, channel) per flush; nested enqueues
  drain in the same flush as extra FA10s, not merged. Watch the 1e9 merged-amount cap under
  wider windows.
- **Trigger-mask coupling**: `HasAnyGrant || Funnel.HasPending` gate must include pending
  funnel work; mask flips mid-frame apply from the next record onward.

### 4b.2 Coalescing is a balance decision, not just a perf one

`ev.Damage` is written but never read (summing is free today), BUT coalescing N hits into 1:
- **EveryHits counters** advance 1 instead of N → `RecordCounterHit` takes a `hits` count;
  one burst per threshold crossing.
- **`max_stacks`** consumes 1 stack for N hits → stack accounting takes count.
- **`chance` procs** roll once instead of N → roll per `HitCount` (or `1-(1-p)^n`).
- Overlay hit/crit RNG same issue (rare today). FX floaters merge harder (accepted, visual).
Coalescing key: `(Kind, MatchKey, Side, ActorPtr, TargetPtr, TypeId, TargetTypeId)`, only at
`ChainDepth == 0 && SourceGrantId == null`. Never coalesce die/board/match records.

### 4b.3 Tick redefinition

`Tick` today = global event counter (rate-dependent). v2 record carries `Frame`
(`Time.frameCount`) + `Seq` (monotonic ordinal). Wall clock stays for ICD/status/DoT (player-
perceived durations; DoTs use *unscaled* time today — game speed does not accelerate them —
keep as-is, documented). `EffectEventDedupe` is **dead code in LIVE** (window=1 → always
passes) — delete it, before a tick redefinition silently activates it as an accidental
coalescer.

### 4b.4 Re-entrancy: the ring buffer fixes the real bug

Today action execution re-enters the pipeline through live game hooks (spawn→hook→OnEvent
recursion), corrupting outer-window telemetry, bypassing dedupe, and restarting `ChainDepth`
at 0 (chains bounded only by ICD). v2: hooks record; drain-generated records process in the
same drain under the budget with a **generation cap (≤3/frame, overflow to next frame)** and
inherit `ChainDepth = parent+1`. This tightens runaway spawn-on-death/cherry-chain builds to
the depth limit — an intentional, documented behavior change.

### 4b.5 Budgeting needs cost classes

Single events can exceed the whole budget: match-scoped `ModifyStat` → `ReapplyLivingForOwner`
(full board reapply), `ClearStatus` (unconditional scene scan — pre-v2 fix), all-target
packets (O(n²) element resolve), match-edge `ClearAll` (unbounded, **never budgeted** — runs
at lifecycle barriers). Drain classifies records: cheap ones drain freely; expensive classes
capped at 1/frame with their own carry-over queue.

`TickDots` runs inside the drain, sharing one board freeze (DoT damage then merges with
recorded damage into one FA10 per target per frame); it is never budget-skipped (it owns
status expiry).

### 4b.6 Record struct (locked)

`Kind, Frame, Seq, ActorPtr, TargetPtr(IntPtr), TypeId, TargetTypeId, Side, Amount(summed),
HitCount, ChainDepth, SourceGrantId, MatchKey(interned), KillerPtr, PairId` — values only,
never IL2CPP object references.

### 4b.7 Pre-v2 cleanups surfaced (independent, do first)

1. `ClearStatus` scans scene even with known targetPtr (InjectorEffectActionSink.cs:262).
2. `Status.AllInstances()` allocates per frame for an emptiness test (EffectRuntime.TickDots).
3. Delete inert `EffectEventDedupe`.
4. `debug.effect.fired` builds a dict per action unconditionally.
5. `ResolveElementTypesFromHub` linear board scan per ptr (O(n²) multi-target).
6. `StatusRuntime.WithdrawEntity` leaks `_counterHits` entries.

## 4c. Downstream consumer audit findings (2026-08-21)

### 4c.1 What consumers actually need

- **MatchHost** folds 9 kinds, all identity/transition — it never counts. Per-ptr ordering
  (spawn before die) and `board.start`-first are the only hard rules; cross-ptr reordering is
  free. Damage/late events: zero impact (it never sees them). One same-frame consumer:
  `UniqueBoundLoadout` stat write on the Pending→Bound transition. `SpawnAdmit` caps read live
  counts — a drain backlog makes caps soft by the backlog size (degradation, acceptable).
- **Server**: `zombie.die`/`plant.place`/`zombie.spawn`/`mower.start` each award XP per
  instance keyed by ptr — **never coalesce/drop**. `entity.stats`, `wave.change`,
  `board.economy`, `board.snapshot`, `level.name` are last-writer-wins — **coalesce freely**.
  `combat.hit`/`combat.hitland`/`*.damage`/`bullet.place`/`item.drop`/`pet.xp` have **no
  server projection at all** (pure log rows).
- **Web**: three hub messages, already batched; noisy kinds already stripped; `lastHit` is
  last-writer-wins → `combat.hit` sampleable for UI; economy deltas (`sun.*`, `money.*`,
  `points.*`) need every instance; `debug.board-stats`/`debug.snapshot` are the resync path
  that heals any dropped membership event.
- **matchKey must be stamped at record time** (MatchHost nulls `GameHooks.MatchKey` on end;
  drain-time stamping orphans every post-end event from its run server-side).

### 4c.2 Safe to stop emitting in normal play (trigger-mask/session gated)

1. `bullet.init` (~100/s) — MatchHost TODO/no-op; server metric redundant *and* clobbered by
   the `BumpBullet` heartbeat absolute-write (two writers, same metric — pre-existing bug);
   web strips it. Emit only when an OnSpawn grant is live or a debug session is active; keep
   `BumpBullet()` always.
2. `plant.damage`/`zombie.damage` — no server projection, web-stripped. Emit when LogDamage ∥
   session ∥ **an OnDamageTaken grant is live** (the last clause fixes a latent bug: with
   telemetry now default-off, melee OnDamageTaken effects would silently never fire).
3. `bullet.place`, `item.drop`, `pet.xp` — no consumer anywhere; session-gate.
4. All `debug.*` outside sessions.

### 4c.3 Never coalesce / drop / reorder

`board.start`, `board.end`, `match.result`, `plant.spawn`, `zombie.spawn`, `plant.die`,
`zombie.die`, `plant.place`, `mower.place/start/die`, `pvz.spawn.extra.ack` — XP ledger and
membership are per-instance, per-ptr.

## 4d. Status

Audit complete (3 sweeps, 2026-08-21). Phase 1 (trigger mask, emission gating, pre-v2
cleanups) implemented same day. Phase 2 (ring buffer + budgeted drain + coalescing) is
specified above and awaits a `decisions.md` row.

## 5. Phasing

| Phase | Content | Risk |
|---|---|---|
| P1 | Trigger mask + typed hot-path records + payload laziness | low — no semantic change |
| P2 | Ring buffer + frame-budgeted drain + input coalescing | medium — needs §4 resolved, ordering tests |
| P3 | Stress verification: synthetic 10× zombie level, max speed; budgets hold or design iterates | — |

Acceptance: mod share ≤ 5% of frame time at 10× zombie density and max game speed; zero
multi-second frames attributable to the mod; effects land ≤ 3 frames late worst case.

## 6. Explicitly rejected

- **Patching GameAssembly.dll** — forbidden by repo policy (AGENTS.md hard boundary) and
  unnecessary: measured cost is in mod code, not hook mechanism.
- **Rust/gRPC server rewrite** — transport measured at <1% and off the frame path.
- Fps caps as a *requirement* — remains a comfort option (`FUSIONRPG_FPS_CAP`).
