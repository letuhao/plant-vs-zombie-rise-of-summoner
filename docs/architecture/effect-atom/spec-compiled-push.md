# Spec: compiled-push (E19)

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

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~CompiledPush"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Push"
```

## Structure

```
src/FusionRpg.Server/AtomPushService.cs                  (new — compile on demand, respond to Hello)
src/FusionRpg.Contracts/AtomPushDtos.cs                  (new — payload shapes)
src/FusionRpg.Injector/Effects/AtomPushReceiver.cs       (new — apply grants + runner entries,
                                                          hand entries to AtomRunner, clear on board.end)
src/FusionRpg.Injector/CheatCommandRunner.cs             (extend `effects.grants.apply`)
tests/FusionRpg.Server.Tests/                            (new PROJECT — does not exist yet; csproj + solution entry)
tests/FusionRpg.Server.Tests/                            (new PROJECT — does not exist yet; csproj + solution entry)
tests/FusionRpg.Server.Tests/CompiledPushTests.cs
tests/FusionRpg.Core.Tests/Atoms/PushContractTests.cs
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
