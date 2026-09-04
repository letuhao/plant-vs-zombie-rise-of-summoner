# Spec: interactive-turns + decision-trace (T6 + T10)

Module ids `interactive-turns` (T6) and `decision-trace` (T10) in the
[battle timeline map](../battle-timeline-map.md). **They ship together and this is one spec**, because
the map is explicit about why: *"an interactive battle without a persisted decision trace is precisely
the hole where a boot sweep silently overwrites a player's win."*

**Written 2026-09-04** to satisfy B20/B21's *"spec first"* gate. Depends on T5 (shipped) and T8
(shipped, B19). T11 `live-sessions` builds on both and is specced separately.

## What the owner already decided

From `decisions.md`, **Battle engine open questions (2026-09-04)**:

> **Interactive battles ship as true live SignalR sessions** — input dwell, AFK timeout, reconnect.
> **This makes T10 `decision-trace` mandatory, not optional**: with real input, `(setup, seed)` stops
> being a complete description of a battle.

So the cheaper "pre-declared intent + client-side playback" path is **out**, deliberately, and
determinism becomes `(setup, seed, trace)`.

## Objective

Let a human occupy the `Ready` dwell an actor already has, and make the resulting battle **as
reproducible as an auto-resolved one**. Those are one problem: input is only safe if it is recorded.

## Design

### 1. The seam already exists — this module fills it, it does not widen it

`IIntentSource.TryDeclare(actorKey, nowTick)` is already documented as *"the AI-policy seam the
auto-resolved modes need, **and the player-input seam an interactive mode needs**"*. `StubIntentSource`
is the AI implementation. T6 adds a second implementation; it does **not** change the interface.

That matters for scope: the kernel's `Ready → Committed` gating, slot contention, and the `Passed`
outcome for "no legal action" are all built and tested. An interactive turn is a *slower* `TryDeclare`,
not a new state machine.

### 2. The dwell, and why a timeout is a decision

Defaults inherited from Chaos `combat-core/08` (`battle-turn-ideal.md` §10a), all **tunable**:
`input_window_ms` 1500, `afk_timeout_ms` 5000, `round_time_ms`.

⛔ **A timeout is recorded as a decision at a tick, never evaluated against a wall clock.** This is the
sharpest determinism trap in the program: if a replay re-measured elapsed time it would take a
different branch on a slower machine. The trace stores *"actor A defaulted at tick T"*, and replay
reads that, exactly as it reads an explicit choice. `SimulationClock` cannot read a wall clock at all
(`spec-virtual-time-core.md` non-negotiables), so the session layer — not the kernel — owns the
countdown and converts it into a tick-stamped decision.

### 3. The trace: appended as the battle progresses, not at the end

**Storage:** a `decisions_json` column on `rpg_web_match_log`. That table already carries
`setup_json`, `seed`, the three version stamps and `environment_stamp` — the trace is the fourth
member of the determinism tuple and belongs beside them, not in a new table.

**Appended per decision.** A trace written only at completion is worthless for the failure it exists
to cover: a disconnect mid-battle leaves a row that *looks* auto-resolvable.

**Shape.** One entry per seated intent: `(tick, actorKey, actionId, targetKey, source)` where `source`
is `player` or `timeout`. Ordered by `(tick, seq)`, the same total order the queue uses — never by
arrival time.

### 4. The sweep must refuse, not heal — and the mechanism is already built

⭐ **`rpg_web_match_log.sweep_refused` already exists**, and `WebMatchService.cs:132` already documents
its semantics: *"A refusal is TERMINAL, not a skip: the row is marked so it leaves the unresolved
[set]"*. The map's own wording for T10 — *"reusing the platform-stamp refusal path"* — points at
exactly this column.

So the rule: **an interactive match whose trace is incomplete is marked refused and abandoned. It is
never re-resolved.** Re-resolving it would substitute AI decisions for a player's and silently
overwrite a real result, which is the precise hole this module exists to close.

### 5. Expeditions are barred from interactive profiles by assertion

Not by convention. An expedition resolves server-side with nobody watching, so an interactive profile
there could only ever time out every turn — a slow way to produce a worse auto-resolve. The bar is an
assertion at resolve time, so it fails loudly rather than degrading.

### 6. Determinism becomes `(setup, seed, trace)`

The existing replay guard already refuses cross-stamp re-resolution. This adds one term: a completed
trace replays byte-identically, and a **missing or partial** trace is not a replay input at all — it is
the refusal condition of §4.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Interactive"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Trace"
dotnet test tests\FusionRpg.Data.Tests
```

## Structure

```
src/FusionRpg.Core/Battle/Timeline/InteractiveIntentSource.cs   (the dwell; an IIntentSource)
src/FusionRpg.Core/Battle/Timeline/DecisionTrace.cs             (append + replay)
src/FusionRpg.Data/Sqlite/RpgStore.WebMatches.cs                (decisions_json, ALTER-style)
src/FusionRpg.Server/WebMatchService.cs                         (refusal on incomplete trace)
tests/FusionRpg.Core.Tests/Battle/Timeline/                     (dwell, timeout-as-decision, replay)
tests/FusionRpg.Data.Tests/                                     (column round-trip, refusal)
```

## Testing strategy

1. **An AFK timeout produces an identical battle on replay** — the map's own named acceptance and the
   sharpest trap here. Prove it with a replay that *would* diverge if the timeout were re-measured.
2. **The boot sweep refuses and marks `Abandoned`** for an interactive match with an incomplete trace,
   and **never heals it**. Assert the row leaves the unresolved set and is not re-resolved on a second
   sweep.
3. **Expeditions are barred** from interactive profiles by assertion — a test that tries it and gets a
   throw, not a degraded battle.
4. **A completed trace replays byte-identically**, and re-running the same trace twice is idempotent.
5. **Zero-interactive is byte-identical**: an auto-resolved battle writes no trace and is unchanged, so
   this module lands without moving a golden — the same invariant every module in this program has
   held to.
6. **Falsifier**: delete the append-per-decision behaviour (write the trace only at the end) and the
   disconnect test must go red.

## Boundaries

- **Always:** record a timeout as a tick-stamped decision; append per decision; refuse an incomplete
  trace terminally.
- **Ask first:** the three timing defaults becoming anything other than tunables; any change to
  `IIntentSource`'s signature.
- **Never:** let the kernel read a wall clock; re-resolve an interactive match from `(setup, seed)`
  alone; heal a partial trace by filling it with AI decisions.

## Success criteria

1. A human can occupy the `Ready` dwell and the battle replays byte-identically from its trace.
2. An AFK timeout is a recorded decision, not a re-measured duration. 3. An incomplete trace is
refused terminally and never re-resolved. 4. Expeditions cannot select an interactive profile.
5. Auto-resolved battles are byte-identical — no golden moves.

## Open, and owner's

- **Which profile carries the interactive dwell.** `hybrid-atb` is where T15 is taking production, but
  T15 is itself blocked (see `battle-timeline-todo.md` B34's finding: `BattleEngine.Resolve` reads no
  profile field today). Until battle resolution routes through the per-actor turn FSM, an interactive
  profile has nothing to attach its dwell to — **so this module cannot ship ahead of that wiring**, and
  saying so here is cheaper than discovering it at build time.
