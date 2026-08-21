# Spec: turn-engine (wave 1)

**Status:** Draft — pending owner review. Module id `turn-engine` in the [world map program](../world-map-program.md). Depends on `world-model`.

## Objective

The map's clock and its single source of truth: a pure, deterministic `step` that advances the whole world one turn from the commands every commander submitted, behind a barrier that waits for all of them.

Success looks like: twenty turns of scripted commands produce a world whose state hash matches a golden, replay from `(seed, template, command log)` reproduces it byte-identically, and nothing in the engine can tell whether a human, an AI, or a test wrote a command.

## Design

### The model

```
state(turn N+1) = TurnEngine.Step( state(turn N), commands(N), seed )
```

`commands(N)` is the union of every commander's submissions for turn N — human, Zomboss, clans, rivals. `Step` is pure: no I/O, no clock, no ambient state, no knowledge of who issued what.

### The barrier

`ITurnBarrier.ShouldFire(world, committed)` decides when a turn resolves. **v1 ships exactly one implementation: `WaitForAllCommitted`.** The interface exists because a deadline-carrying policy is what would make this real-time and a wall-clock-period policy is what would make it idle — and `Step` must never learn which one is installed. That is a design invariant, not a feature promise: no other policy is built in this module.

### Commands

Typed records in Core, plain data, versioned:

```
WorldCommand: commanderId, commandId (unique per commander per turn), kind, payload
```

v1 kinds are deliberately thin — `stand-fast` plus whatever `world-movement` adds. Validation is two-stage: **admission** (well-formed, references exist, the commander owns the subject) at submit time, and **legality** (still possible given the state at Reveal) inside `Step`. An illegal-at-reveal command is dropped into the turn report with a reason rather than throwing — one commander's stale order must never abort a turn.

### Phase order (locked; changing it bumps `RulesetVersion`)

```
Commit → Reveal → Movement resolution (discrete-event) →
Sieges → Construction & Production → Growth → Pressure → Events/Calendar → Snapshot
```

- **No command may read another commander's commands for the same turn.** Orders seal at Commit and reveal together — this is what makes simultaneity fair and the AI honest.
- Phases after movement are single ordered passes over the graph.

#### Movement resolution is discrete-event, not fixed sub-steps

There is **no `StepsPerTurn` constant.** Movement resolves by processing the ordered set of *interesting moments* inside the turn:

- **Event time** is an integer fraction of the turn, 0–1000 per-mille. Every event carries `(timeMilli, entityId, kind)`.
- **Seed events** at Reveal: each moving entity's arrival time on its current lane, computed from movement points and lane cost.
- **Derived events**: lane crossings (two entities meeting — solved arithmetically, not sampled), zone-of-control entry, arrival at a sector.
- **Process in order** of `(timeMilli, entityId)` — the entity id is the tie-break, never dictionary order. Processing an event may enqueue later events; it may never enqueue an earlier one (a monotonicity assert makes that a test, not a hope).

Why this over sampling the turn N times: a crossing point is *exact* rather than quantised to the nearest sub-step, there is no arbitrary constant baked into `RulesetVersion` to defend or re-tune later, and the work is proportional to what actually happens rather than to a fixed sampling rate. A quiet turn costs almost nothing.

### Simultaneity rulings (locked with the phase order)

| Situation | Ruling |
|---|---|
| Two forces enter the same empty sector at the same event time | meeting engagement — neither is the defender |
| Two forces cross on one lane | they meet at the arithmetically exact crossing time and fight on the lane |
| A force attacks one that is leaving | zone of control halts the mover at contact, so leaving must have been ordered before contact |
| Two claims on one subject | higher initiative wins; the loser keeps its movement and is blocked |
| Mutual destruction | permitted |
| Any remaining tie | stable sort by entity id — never dictionary order |

### Determinism rules

Integer or fixed-point only in game-affecting branches · stable ordering by entity id everywhere, never dictionary enumeration · seeded per-system RNG streams derived as `(worldSeed, turn, streamName)` using the existing `SeededRng`, never `System.Random` · **no wall-clock read anywhere inside `Step`** · every turn stamped `(engineVersion, rulesetVersion, seed)`.

### Outputs

`TurnReport` — the ordered typed event list for presentation, replay, and the "what happened while I was away" screen: commands accepted and dropped (with reasons), movements, contacts, battle requests and their outcomes, production, growth, pressure changes, calendar rolls.

`StateHash` — a stable hash over the canonical serialization of the whole world after the turn. This is the drift detector: goldens compare hashes, and a cross-version mismatch is a conscious re-bless, exactly as battle goldens work today.

### The calendar

A turn is a day; day 7 of each week and day 1 of each month fire boundary effects in the Events phase (weekly recruit pulses, special weeks, special and plague months, era-event rolls). v1 implements the **boundary hooks and their rolls**; the economic effects land with `sector-development`.

### Combat, in wave 1

`IBattleResolver` is a port. v1 installs a **placeholder** — a deterministic comparison of composed strength with a seeded margin — purely so movement and contact are testable end to end. It is explicitly not the game's combat: `combat-handoff` swaps in the real seam (`BattleRequest` out, `OutcomeRecord` in) and the placeholder is deleted, not kept as a fallback.

### Persistence and the turn transaction

| Table | Key | Columns |
|---|---|---|
| `rpg_world_commands` | `(world_id, turn, commander_id, command_id)` | `seq`, `kind`, `payload_json`, `submitted_utc` |
| `rpg_world_turn_log` | `(world_id, turn)` | `state_hash`, `engine_version`, `ruleset_version`, `seed`, `committed_utc`, `report_json` **(nullable — hot tail only)** |

**Reports are re-derivable, so most of them are not stored.** The engine is deterministic, so `(seed, template, command log)` reproduces any turn's report exactly. The log therefore always keeps the cheap columns — hash and version stamps, which are what detect drift — and keeps `report_json` only for a **hot tail of the last N turns** (proposed 50). Asking for an older report replays from the last stored state or from turn zero and regenerates it, stamped with the versions it was produced under; a version mismatch returns the hash and refuses to fabricate a report the current engine would not produce. This mirrors the ledger watermark pattern already in the codebase, and keeps a thousand-turn campaign cheap.

**One turn is one transaction:** the barrier fires → load state → `Step` → write mutated world rows, append the turn log, bump `current_turn` and the world revision → commit. A replayed end-turn for an already-committed turn returns the stored report and changes nothing (the summon/fusion idempotency discipline).

Command submission is separately idempotent on `(world_id, turn, commander_id, command_id)`.

### Server

`POST /api/world/{id}/commands` (submit or replace a commander's orders for the current turn) · `POST /api/world/{id}/commit` (mark a commander committed; the barrier fires when all are) · `GET /api/world/{id}/turn/{n}` (report) · SIM-only `POST /api/test/world/{id}/run-turns` (drive N turns from a scripted log). SignalR `WorldTurnAdvanced` after commit.

Stub commanders (Zomboss, clans) auto-commit `stand-fast` in this module; real policies are `ai-commander`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # step purity, phase order, simultaneity rulings, hashing
dotnet test tests\FusionRpg.Data.Tests    # one-transaction turns, command + commit idempotency
dotnet test tests\FusionRpg.E2E.Tests     # SIM: 20-turn scripted run + replay
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/World/Turn/     → TurnEngine.cs, WorldCommand.cs, ITurnBarrier.cs,
                                     WaitForAllCommitted.cs, TurnPhases.cs, TurnReport.cs,
                                     StateHasher.cs, IBattleResolver.cs, PlaceholderResolver.cs
src/FusionRpg.Data/Sqlite/         → RpgStore.WorldTurns.cs (commands, turn log, the turn transaction)
src/FusionRpg.Server/              → WorldTurnEndpoints.cs
tests/FusionRpg.Core.Tests/World/  → phase-order goldens, simultaneity cases, determinism, hash stability
tests/FusionRpg.E2E.Tests/         → the wave-1 checkpoint
```

## Code style

`TurnEngine` mirrors `BattleEngine` exactly in discipline: pure, injected seed, catalog validation, integer per-mille, no logging side effects — the report *is* the log. Phase functions are small and individually testable; the engine never reaches into the store.

## Testing strategy

- **Purity and determinism:** same `(state, commands, seed)` twice ⇒ identical state and hash; a guard test asserts no `DateTime.Now`/`UtcNow`/`Environment.TickCount` symbol appears under `Core/World/Turn/`.
- **Phase order:** golden report for a fixed scenario; reordering any phase changes the hash (proving the order is load-bearing).
- **Simultaneity:** one test per ruling in the table, including mutual destruction and the tie-break.
- **Barrier:** a turn does not fire until every commander commits; a late commander's submission is accepted until it does; a replayed commit is a no-op.
- **Transactions:** forced failure mid-turn leaves zero rows and does not advance `current_turn`.
- **The wave-1 checkpoint:** 20 scripted turns ⇒ golden hash, byte-identical replay, exactly one turn-log row per turn.

## Boundaries

- **Always:** `Step` is pure; one turn is one transaction; every RNG draw comes from a named derived stream; every dropped command is reported with a reason.
- **Ask first:** changing the phase order or the event-ordering rule (both bump `RulesetVersion`); adding a second barrier policy; new command kinds beyond the movement module's; changing the report hot-tail depth.
- **Never:** wall-clock reads inside `Step`; `System.Random`; dictionary-order iteration over game state; letting a commander read another's pending orders; throwing out of a turn because one command went stale.

## Success criteria

1. The wave-1 checkpoint passes (golden hash + byte-identical replay + one transaction per turn). 2. Every simultaneity ruling has a passing test. 3. The no-wall-clock guard test is green. 4. Command and commit idempotency proven. 5. All existing suites and guards stay green.

## Open questions

The report hot-tail depth (proposed 50) is a guess until a real campaign exists to measure replay cost against. Whether re-derivation should replay from turn zero or from periodic state snapshots is the same question one layer down — snapshots are easy to add later and pointless to add now.
