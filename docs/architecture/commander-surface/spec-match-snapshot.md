# Spec: `match-snapshot`

**Module id:** `match-snapshot` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `default-persistence` · **Blocks:** `lawn-hud-chip`
**Soft-deps:** `/api/loadout`, `/api/aura-runtime`, `commander-lawn-bridge` (aura-skill), `aura-delivery-path` (delivery)
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Hook point:** `MatchHost.Apply` when `isStart`, **after** `GameHooks.MatchKey` assign and **before**
   `TryEffect("NotifyMatchStart", …)` (`MatchHost.cs:144-154`) — insert snapshot freeze between lines 153
   and 154. `_runtime.Apply(kind)` has already run; auto-end-before-start path (`MatchHost.cs:115-122`)
   must clear any prior holder.
2. **Clear on end:** `board.end`, `match.result`, and the auto-end-before-start path (`115-122`) all call
   `EndMatch` on the holder.
3. **v1 transport: session cache at start** — freeze from injector session cache populated by prior REST
   refresh (patron pattern), **not** `await` HTTP on Unity main thread inside `MatchHost.Apply`. Poll may
   run on background thread before Apply or reuse last successful refresh if fresh enough.
4. **`MatchCommanderSnapshot` is new** — no symbol exists in `src/` today.
5. **Snapshot includes aptitude allocation** for the leading commander — bridge reads holder during
   `InMatch`, not live `CommanderAllocationSource` / `AptitudesUpdated`.
6. **Mid-run loadout/aptitude saves** may persist on server but **lawn delivery and HUD read snapshot**
   until match ends.

---

## Objective

At each lawn `board.start`, freeze who led this match, which aura/loadout state applies, and the commander's
aptitude allocation for **this** match. Lawn HUD, observe fold, and aura delivery consume the snapshot, not
live server state mid-wave.

**Success:** Automated test: set default Dave + active Might on server → synthetic `board.start` → snapshot
contains Dave + `"Might"` + allocation copy → change default on server before `board.end` → HUD/observe
unchanged for current match → next `board.start` picks up new default.

---

## ⛔ Program acceptance share

Automated test: after `BeginMatch`, change default (and allocation) on server → assert `Current` snapshot
unchanged until `EndMatch`; next `BeginMatch` reflects new values. Mid-match freeze test is mandatory for
this module to be marked done.

---

## Commands

```powershell
$env:FUSIONRPG_GAME_DIR = "<game folder>"
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~MatchCommanderSnapshot
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
```

Live check (owner-run): web changes default mid-match; lawn HUD stays on leader at wave start.

---

## Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Commanders/MatchCommanderSnapshot.cs` | **new** — immutable snapshot record |
| `src/FusionRpg.Core/Commanders/MatchCommanderSnapshotHolder.cs` | **new** — match-scoped holder, clear on end |
| `src/FusionRpg.Injector/Match/MatchHost.cs` | edit — freeze snapshot before `NotifyMatchStart` |
| `src/FusionRpg.Injector/Commanders/MatchCommanderSnapshotSource.cs` | **new** — read session cache, build snapshot |
| `src/FusionRpg.Injector/RpgClient.cs` | edit — background refresh into session cache (no Apply await) |
| `src/FusionRpg.Injector/Debug/DebugRuntime.cs` | edit — extend `Snapshot()` with `match.commander` fold |
| `tests/FusionRpg.Core.Tests/Commanders/MatchCommanderSnapshotTests.cs` | **new** |

Optional combined server endpoint deferred — two GETs + allocation GET suffice v1.

---

## Design

### Snapshot shape

```csharp
public sealed record MatchCommanderSnapshot(
    string LeadingCommanderId,           // stable id, e.g. commander:dave
    string LeadingCommanderDisplayName,    // e.g. Crazy Dave
    string? ActiveAuraId,                  // catalog id, e.g. Might
    string? ActiveAuraDisplayName,
    AptitudeAllocation Allocation,         // copy at freeze — bridge reads this during InMatch
    long AllocationRevision,               // from store revision at freeze
    long SnapshotRevision);                // opaque, for logging
```

Holder API:

```csharp
void BeginMatch(MatchCommanderSnapshot snapshot);  // board.start (before NotifyMatchStart)
void EndMatch();                                    // board.end / match.result / auto-end
MatchCommanderSnapshot? Current { get; }            // null outside match
```

Clear on:

- `isEnd` path after `NotifyMatchEnd` (`MatchHost.cs:138-142`)
- Auto-end-before-start (`MatchHost.cs:115-122`)
- `match.result` via `IsMatchEnd` (`MatchHost.cs:168-170`)

### Freeze at board.start (ordering)

1. `_runtime.Apply("board.start")` — match entered `InMatch`, `MatchKey` assigned.
2. `TryEffect("ClearAll", …)` if applicable (`MatchHost.cs:125-128`).
3. **`MatchCommanderSnapshotSource.BuildFromSessionCache()`** — no await in Apply.
4. `MatchCommanderSnapshotHolder.BeginMatch(snapshot)`.
5. `ConfigureCaps(SpawnAdmit.Config)` (`MatchHost.cs:146`) — unchanged.
6. `GameHooks.MatchKey = key` (`MatchHost.cs:149-152`).
7. `NotifyMatchStart` (`MatchHost.cs:154`) — aura delivery runs after snapshot exists.

### Session cache population (v1)

Mirror patron session refresh:

1. Background or pre-match thread: `GET /api/commanders/{playerId}/default`, list row or loadout +
   `/api/aura-runtime`, `GET` allocation for leading commander's scope key.
2. Write into injector session cache (`MatchCommanderSnapshotSource`).
3. `MatchHost.Apply` reads cache synchronously; stale/missing → degradation below.

**Does not:** re-poll mid-match; apply aptitude math (bridge); issue aura grants (delivery path).

### Poll failure degradation (closed)

| Condition | Behavior |
|---|---|
| Cache miss / poll failed | Dave + null aura + empty allocation + **log warning** |
| Partial data | Prefer whatever fields succeeded; never abort match |
| Invalid commander id in cache | Treat as miss → Dave fallback |

Match **always** starts (ideal §2.1).

### Integration with aura-skill

| Program module | Relationship |
|---|---|
| `commander-lawn-bridge` | Reads **allocation from holder** during `InMatch` when present — see cross-link in [spec-commander-lawn-bridge.md](../aura-skill/spec-commander-lawn-bridge.md) |
| `aura-delivery-path` | Consumes active aura **from snapshot** when granting at match start |
| Live aptitude cache | **Ignored for lawn during `InMatch`** — `AptitudesUpdated` must not mutate holder |

Cross-link in implement PR; do not duplicate W1/W2 or R4 in this spec.

### FE observe deliverable (owned here)

Extend `DebugRuntime.Snapshot()` / `debug.snapshot` emit (`CheatCommandRunner.cs:294-295`) with:

```json
{
  "match": {
    "commander": {
      "leadingCommanderId": "commander:dave",
      "leadingCommanderDisplayName": "Crazy Dave",
      "activeAuraId": "Might",
      "activeAuraDisplayName": "Might"
    }
  }
}
```

Server forwards via existing debug event poll (`DebugEndpoints.cs:69-74`). Web `lawn-hud-chip` folds this
into `LawnViewModel` — no separate lawn REST poll each frame.

---

## Code style

- **Core:** immutable `record`; holder static or match-scoped singleton cleared on end — mirror patron
  match freeze naming.
- **Injector:** snapshot build in dedicated `MatchCommanderSnapshotSource.cs`; `MatchHost` calls one line
  before `NotifyMatchStart`; no HTTP in Apply.
- **Tests:** Core holder tests without Unity; injector test mocks session cache.

---

## Testing strategy

| Level | Test |
|---|---|
| Core unit | Holder begin/end/clear; snapshot immutability; auto-end clears |
| Core unit | Allocation copy equals source at freeze; revision captured |
| Injector unit | Mock session cache → snapshot built on synthetic `MatchHost.Apply("board.start")` |
| Injector unit | Ordering: snapshot before `NotifyMatchStart` (spy) |
| Integration | Default change between two board.starts reflected |
| Integration | **Mid-match default + allocation change does not alter `Current`** |
| Integration | `match.result` and auto-end-before-start clear holder |
| Observe | `debug.snapshot` includes `match.commander` when in match |

---

## Boundaries

- **Always:** freeze before `NotifyMatchStart`; clear on all end paths; poll once v1; allocation in
  snapshot; bridge immutability during `InMatch`
- **Ask first:** combined snapshot endpoint vs cache refresh timing
- **Never:** live re-read mid-wave for HUD; `await` HTTP in `MatchHost.Apply`; duplicate aura delivery;
  block `board.start` on poll failure

---

## Success criteria

- [ ] Snapshot populated on every `board.start` in tests
- [ ] `EndMatch` clears holder on `board.end`, `match.result`, and auto-end path
- [ ] Allocation + revision copied at freeze
- [ ] `debug.snapshot` exposes `match.commander` for lawn HUD fold
- [ ] ⛔ share: mid-match default change does not alter `Current` until next start
- [ ] Degradation: poll failure → Dave + log; match starts

---

## Open questions

None — poll failure = Dave + log (closed); combined endpoint optional (defer); observe path owned by this
module via `debug.snapshot`.
