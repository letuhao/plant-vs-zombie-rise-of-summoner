# Spec: compiled-push (E19)

**Status: PARTIAL 2026-08-22** — contract, codec, server half, and the injector receiver are built. **The hub wiring is not** — see "What is left" below.

Module **E19** in the [atom effect map](../effect-atom-map.md). Depends on **E7**, **E8**, **E15** (it delivers the runner entries E15 consumes).

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

> **Added by the audit.** E7 attributed delivery to "E8/E-push work" — a module that appeared in no map, no spec, and had no owner. Without this, the compiler's only consumer is its own tests and nothing the atom layer produces ever reaches a running game.

## Objective

Deliver E7's compiled output from the server to the injector, and hold the guarantee that makes the whole Cold/Hot split legal: **the injector never holds content rows.**

## Design (locked on approval)

### What travels

Two payloads, both already-compiled — never atoms, never containers, never curves.

| Payload | Shape | Consumer |
|---|---|---|
| Compiled grants | `EffectGrantDto[]` — the shape `EffectBag` already accepts | `EffectBag` via the Funnel |
| Runner entries | `RunnerEntry[]` — E7 owns the contract | `AtomRunner` (E15) |

The injector receives **resolved** content: value specs already interned, curve ids already int-indexed, predicates already compiled to E13's form. It cannot author, validate, or re-resolve anything, which is exactly why it needs no content tables.

### The channel is the one that already exists

This extends today's `effects.grants.apply`-on-Hello path rather than inventing a second one. That path already: pushes on cold start **and** on SignalR reconnect, upserts by required `grantId` with no `ClearAll`, and is cleared on `board.start` / `board.end` alongside the injector's `ClearMatch`.

E19 adds the runner-entry half and the revision negotiation. It does **not** add a transport.

### Revision negotiation

```text
injector Hello  → { contentHash, catalogRevision }   (what it currently holds; empty on cold start)
server response → { catalogRevision, grants[], runnerEntries[] }  — full set, never a delta
```

**Full set, not a delta.** Deltas need ordering guarantees a reconnect cannot provide, and the payload is small — compiled output for one match, not a catalog. If the injector's `catalogRevision` already matches, the server sends an empty apply and the injector keeps what it has.

`contentHash` travels so a mismatch is **visible in telemetry** rather than silently tolerated: an injector running against content the server has since edited is a diagnosable state, not a mystery.

### What it must not do

- **Never sit between an event and its apply.** The push is **Cold**; per-hit rolls and dispatch stay local. If the server is unreachable mid-match, the runner keeps dispatching from what it holds ([overlay-control-loops.md](../overlay-control-loops.md)).
- **Never ship content rows.** If the injector ever needs an atom row to decide something, the compile/run split has leaked and that is a design bug, not a transport gap.
- **Never require a push to start a match.** A match with no pushed bindings runs with none — that is a normal state, not an error.

### Match lifecycle

Compiled output is **match-scoped**, like today's grant session. `board.end` clears it. `entity:{ptr}` bindings are session-scoped by definition (definitions §6), so nothing durable survives a match, and a reconnect mid-match re-pushes the full set.

### [built] What building it settled

**A compiled predicate can travel.** `FlatPredicate`'s ops are already all ints — leaf id, subject,
value, jump targets — so the wire carries the compiled form itself and the injector rebuilds an
evaluator without a status catalog, an element roster, or a content row. An interned status bit
travels as a bit; the name is gone before the payload is serialised.

**One codec, both ends** (`AtomPushCodec`). A hand-written decoder on the far side is how a dropped
limit becomes an effect that silently never caps — E7 already dropped those keys once.

**The runner binding id is `(binding, atom)`, not the binding alone.** A container with two runner
atoms needs two ICD clocks and two caps; a shared id would merge them *and* tie the `(priority,
bindingId)` sort, making evaluation order depend on how rows happened to arrive.

**Defs travel with the entries.** A runner dispatch names its atom id as an `effectId` and
`EffectBag.Grant` throws on an unknown one (found building E15) — so `AtomPushDto.Defs` carries both
paths' defs.

**`BindResolution` now carries the atom rows it already loaded.** Resolving loads every one of them
and used to discard them; the push would otherwise have re-queried per binding, reopening the N+1
that method was rewritten to close.

**E1's `capPerMatch` "not available yet" guard is lifted.** It refused the param at load, so the
counter E15 shipped was unauthorable by the content it exists for. A guard that outlives its reason
is a silently dead feature.

**The seed is derived from the match key with a named hash (FNV-1a), not `String.GetHashCode`,**
which is randomised per process and would make "same match key, same rolls" false on every restart.
If seeds ever need to be unpredictable to a player this becomes a stored column; the wire field
already carries whatever is chosen.

**The receiver installs defs and bindings, never grants.** The command runner's existing grant loop
keeps that job: it resolves `entity:selected`, normalises the owner key, and refuses an `instance:`
owner on the Hot path. A receiver that applied grants itself would either duplicate that work or skip
it, and skipping it is silent.

**The legacy payload still works unchanged.** `effects.grants.apply` carrying only `grants[]` takes
exactly the path it always did; the atom half runs only when `runnerBindings`, `defs` or `upToDate`
is present, so an older server needs no coordination.

**The runner is fed before `Bag.OnEvent`** on both injector event paths, for the flush-ordering reason
recorded in [spec-atom-runner.md](spec-atom-runner.md).

### What is left

- **Hub wiring.** `RpgHub.PushGrantSnapshotAsync` pushes a *session* grant snapshot, with no player
  attached to the connection. Which `OwnerScope` a Hello push resolves for is an open question, not
  something to guess — the answer decides whether the push is per-player or per-session.
- **Receiver tests.** `AtomPushReceiver` has a caller on every path (install, per-event, match start,
  `board.end`) but **no test**: the injector has no test project and its statics reach Unity-side
  helpers. The codec and the trigger index it hands off to are covered in Core; the ~40 lines of glue
  between them are not, and that should be said rather than counted as covered.

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~CompiledPush"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Push"
```

## Structure

```
src/FusionRpg.Contracts/AtomPushDtos.cs                  BUILT — payload shapes
src/FusionRpg.Core/Effects/Atoms/AtomPushCodec.cs        BUILT — encode/decode, both ends
src/FusionRpg.Server/AtomPushService.cs                  BUILT — resolve, compile, negotiate, seed
tests/FusionRpg.Server.Tests/                            BUILT — new project + solution entry
tests/FusionRpg.Server.Tests/CompiledPushTests.cs        BUILT — 15 tests
tests/FusionRpg.Core.Tests/Atoms/PushContractTests.cs    BUILT — 25 tests
src/FusionRpg.Injector/Effects/AtomPushReceiver.cs       BUILT — install defs + bindings, per-event
                                                                 dispatch, match start, board.end
src/FusionRpg.Injector/Effects/EffectRuntime.cs          BUILT — drives the runner before the bag
src/FusionRpg.Injector/CheatCommandRunner.cs             BUILT — atom half of effects.grants.apply
src/FusionRpg.Server/RpgHub.cs                           TODO  — extend the Hello push (owner scope open)
```

## Testing strategy

| Case | Expect |
|---|---|
| Cold start Hello | full set delivered; injector holds grants + runner entries, **zero content rows** |
| Reconnect mid-match | full set re-delivered; no duplicates — upsert by id |
| Injector revision already current | empty apply; injector keeps what it holds |
| `contentHash` mismatch | delivered anyway, **mismatch recorded in telemetry** |
| Server unreachable mid-match | runner keeps dispatching; **no await on the hot path** |
| `board.end` | compiled output cleared alongside the grant session |
| Match with no bindings | starts normally — not an error |
| Payload inspection | contains no atom, container, or curve rows — architecture test |
| `guard-secondary-no-unity.ps1`, `guard-funnel-delta.ps1` | pass unchanged |

## Boundaries

**Always:** push the full set; keep the push Cold; clear on `board.end`; keep the injector free of content rows.

**Ask first:** adding a second transport; making any part of the push synchronous with a match start.

**Never:** await the push on the hot path; ship a delta; ship content rows; make a missing push an error; let the server sit between an event and its apply.
