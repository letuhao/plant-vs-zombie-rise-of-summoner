# Spec: `first-open-signal`

**Program:** `rift-gate` · **Map:** [../rift-gate-map.md](../rift-gate-map.md) — **Audit correction 2**
**Ideal:** [../rift-gate-ideal.md](../rift-gate-ideal.md) — decisions 1, 5
**Touches:** `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs` (a durable row), an onboarding endpoint,
the web FE's first-load write, and the injector's consume arm
**Depends on:** — (independent; `entry-landing` depends on this)

---

## Objective

Record a **durable once-per-player fact — "the FE has been opened"** — plus the gating that makes it
*actionable*: the **server** knowing the injector is connected. **Trigger only** — the capture
mechanism itself (text sweep, portrait traversal, manifest, progress UI) is a **separate program**
(decision 1). This module writes the fact and exposes the capture trigger condition; it builds no
capture.

**Three separate facts (map Audit 2), corrected from the first draft:**

1. **"Auto-create the player if needed" is already built.** `SeedPlayerIfEmpty` inserts
   `(1,'Player 1')` on any empty DB and sets `current_player_id=1` (`RpgStore.cs:3865-3878`), called
   from schema unlock (`:149`). A player row **always** exists before the FE can read anything. This is
   a **built** finding, not work. `entry-landing` must not "create a player".
2. **The FE cannot detect it is in the overlay** without a marker — which `overlay-hide` now provides
   (decision 16). Crucially, the first-open fact is **not** keyed on the tombstone or the marker: it is
   keyed on **the FE being opened**, so it is also correct for the launcher's **"Open RPG UI"** button
   (`MainWindow.xaml.cs:471`, which opens the same URL in a normal browser) or a plain browser visit.
   That is a feature, not a loophole (map Audit 2; `standalone-first`).
3. **The injector cannot observe the launcher's F10** in launcher mode — the code says so itself
   (*"The launcher's F10 never reaches the injector"*, `OverlaySwitch.cs:136-139`). So an injector-side
   "the player opened the FE" trigger is **blind to the primary open path** in the default host mode.
   The fact is therefore written **FE/server-side** on first load.

**The corrected shape:** the fact is a **durable row** (mirroring `OnboardingStoryRow`, not a session
flag), keyed on `player_id` (the idempotency key), and the **actionability gate is the server knowing
the injector is connected** — the exact condition the server already tracks. Capture needs the injector
running and in-game anyway (it reads live Almanac dictionaries), so "injector connected" is the honest
condition, not a proxy.

**Success:** the first FE load records the fact once per player, durably; a second load is a no-op; the
fact is *actionable* only when the injector is connected; the FE is never blocked by any of it.

**ASSUMPTIONS I'M MAKING:**

1. **Durability is a row, not a session flag** (map: "a row, not a session flag, mirroring
   `OnboardingStoryRow`, `RpgStore.Onboarding.cs:25`"). The natural home is the existing
   `rpg_onboarding_story`-family pattern or a sibling once-per-player row; the spec proposes a
   dedicated flag row keyed `(player_id)` with a `revision`, matching the onboarding ledger's
   `revision` discipline (`ListOnboardingStories` returns `Revision`, `:118`).
2. **Idempotency key is `player_id`.** `INSERT OR IGNORE` with the player id as the key is the
   precedent (`EnsureOnboardingStoryRowUnlocked`'s `INSERT OR IGNORE`, `:44-58`;
   `TryEarnOnboardingCheckpoint`'s "primary key is the durable idempotency gate", `:270`).
3. **Actionability is read, not pushed.** The server already publishes injector connectivity:
   `HealthDto.InjectorConnected` (`Dtos.cs:45`) is derived from a 5s heartbeat window
   (`RpgStore.InjectorConnected`, `:1059-1060`) and `LiveInjector` (`:1062-1063`). The gate reads this;
   the FE never has to guess.
4. **Failure never blocks the FE.** An unactionable fact **stays pending** until an injector connects
   (map Audit 2's own failure clause). The write is best-effort from the FE's perspective; a failed
   write is retried on the next load, never surfaced as a blocking error.
5. **Stable refusal-reason vocabulary** matching the existing `onboarding.story-*` style
   (`onboarding.story-unknown`, `onboarding.story-version-unsupported`, `onboarding.story-outcome-unknown`,
   `onboarding.story-conflict`, `RpgStore.Onboarding.cs` `AcknowledgeOnboardingStory`).

→ Correct me now or implementation proceeds with these.

## Tech stack

- `FusionRpg.Data`: one durable flag row + read/write methods, following the existing onboarding
  ledger (SQLite, `INSERT OR IGNORE`, `revision`).
- `FusionRpg.Server`: one endpoint (`GET` read + `POST` record) on the existing onboarding group
  (`OnboardingEndpoints.MapOnboarding`, `/api/onboarding`, `:14-44`).
- `FusionRpg.Injector`: the **consume** arm — a read of the trigger condition at the existing 0.25s
  command/health cadence (`InjectorLoop.cs:126-131`), no new poll.
- Web: a one-shot first-load write via the existing bus (`sendJson`, `rest.ts`).

## Commands

```powershell
# Data: the durable row + idempotency
dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~Onboarding"

# Server: the endpoints
dotnet test tests/FusionRpg.Server.Tests

# Web: the first-load write (one-shot)
cd web/fusion-rpg-web; npm test -- --run firstOpen; npm run build
```

### Boundary selection (never the whole suite)

```powershell
.\scripts\verify-change.ps1 -Paths `
  src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs,`
  src/FusionRpg.Server/OnboardingEndpoints.cs,`
  src/FusionRpg.Injector/Host/InjectorLoop.cs `
  -Session rift-gate-spec-20260915-c41a
```

`src/FusionRpg.Data/**` selects the data boundary with the `dal` + `test-substrate` guards
(`verification-boundaries.v1.json:24`); the substrate rule applies in full — an in-memory store test,
disk only when the disk is under test, and a failed temp-delete is a failure, never `catch { }`.

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Data/Sqlite/RpgStore.Onboarding.cs` | The durable fact: `RecordFirstOpen(playerId)` (`INSERT OR IGNORE`, idempotent) and `GetFirstOpen(playerId)`, plus the actionability read reusing `InjectorConnected` (`RpgStore.cs:1059-1060`). Row shape mirrors `OnboardingStoryRow` (`:25`) |
| `src/FusionRpg.Server/OnboardingEndpoints.cs` | Two routes on the existing group: `POST /api/onboarding/{playerId}/first-open` (record, idempotent) and `GET .../first-open` (read the fact **and** its `actionable` flag). A stable reason vocabulary |
| `src/FusionRpg.Injector/Host/InjectorLoop.cs` | **NOT CHANGED — see "Implemented scope" below.** The spec's original "consume arm" would be a trigger with no consumer: this program builds the trigger fact, the capture program builds the capture. Recorded rather than stubbed. |
| `web/fusion-rpg-web/src/lib/bus/firstOpen.ts` (new) | One-shot first-load write via the existing bus; fired once, never blocking, retried next load on failure |
| `web/fusion-rpg-web/src/app/App.tsx` | Mount the one-shot write beside the existing `ActorSurfaceCatalogBootstrap` (`:12,21-24`) |

## Code Style

Follow the onboarding ledger's exact idioms: `INSERT OR IGNORE` for idempotency, a `revision` column,
and a refusal-reason string rather than an exception for a benign repeat.

```csharp
/// <summary>
/// Durable once-per-player "the FE has been opened" fact. Trigger only — the capture mechanism is a
/// separate program. Keyed on player_id: a row, not a session flag, so it survives restart and is
/// correct however the player reached the FE (tombstone, F10, Open RPG UI, or a plain browser visit).
/// </summary>
public bool RecordFirstOpen(long playerId)
{
    lock (_gate)
    {
        using var db = OpenUnlocked();
        if (GetPlayerUnlocked(db, playerId) is null) return false;
        using var cmd = db.CreateCommand();
        // The primary key is the durable idempotency gate — a second open is a no-op, not a conflict.
        cmd.CommandText = """
            INSERT OR IGNORE INTO rpg_first_open(player_id, opened_utc, revision)
            VALUES($p, $utc, 1);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$utc", DateTime.UtcNow.ToString("o"));
        return cmd.ExecuteNonQuery() > 0;
    }
}
```

## Testing Strategy

- **Idempotency is the contract.** Record twice → one row, the second is a no-op; the read is stable.
  The test asserts a **contract** (row exists exactly once, revision semantics), never a population
  count.
- **No derived count is asserted.** The module deals in a per-player boolean fact, so there is no
  roster/species/manifest count anywhere. Actionability tests assert the boolean gate, not a number.
- **Actionability gate.** With the heartbeat stale (`InjectorConnected` false, `RpgStore.cs:1059-1060`)
  the fact reads `actionable: false`; on a fresh heartbeat (`Heartbeat(SourceInjector)`, `:1065-1072`)
  it reads `true`. This is the honest condition, tested directly, not via a proxy.
- **Failure never blocks.** A refused/failed record leaves the FE usable; a pending fact stays pending
  and becomes actionable on the next connect — tested by recording with no heartbeat, then
  heartbeating and re-reading.
- **Substrate.** `RpgStore` tests run in memory (`RpgStore(string dataDir, bool inMemory)`,
  `RpgStoreOptions`); the disk is not under test here. Test-substrate guard green.
- **Live (owner terminal):** first load records the fact; reload does not duplicate; with the game
  closed the fact reads unactionable; with the injector connected it reads actionable. **No capture is
  claimed** — this module only proves the trigger condition, per `live-probe-standard.md`
  (a response body is not proof of a capture that this program does not build).

## Boundaries

- **Always:** a durable row (never a session flag); `player_id` as the idempotency key; the
  actionability read is the server's own `InjectorConnected`; best-effort write that never blocks the FE.
- **Ask first:** renaming or moving the fact's storage; adding a second "opened" signal beside it;
  changing the actionability condition away from injector-connectivity.
- **Never:** build the capture mechanism (separate program, decision 1); key the fact on the tombstone
  or the embed marker (it is keyed on the FE being opened); block the FE on the write; assert a
  population count; treat a response body as proof of capture.
- **ActorHub gate: N/A.** No actor combat/derived/AppliedCombat magnitude is produced or consumed; no
  fold is invented.

## Tunables

| Number | Home | Why |
|---|---|---|
| **First-open capture pacing** | **structural per-frame cap, hardcoded with a comment** — **not** a `data/tuning` number | The ideal's own rule (`rift-gate-ideal.md` Tunables table): precedent is the drain budget `Math.Clamp(frameSec*0.10, 0.0002, 0.002)`, explicitly *hardcoded and not configurable* because changing it changes whether the drain fits a frame, never how the game feels (`event-pipeline-v2-ssot.md:68-76`). Capture pacing is the same shape |
| A bounded card-traversal count (if any lands here) | structural limit, hardcoded with a comment | `ssot-power-scale.md` §11 exempts per-frame caps and bounded ratios **only when they say so** |
| No magnitude, placement, or progression number | — | This module introduces none |

Note: the **capture mechanism is out of scope**, so its pacing lands in the capture program. This
spec records the class rule so the capture program inherits it and does not invent a tuning key.

## Success Criteria

- [ ] The first FE load records a **durable** once-per-player fact; a second load is a no-op
      (`INSERT OR IGNORE`, `player_id` key), proven in memory.
- [ ] The fact is keyed on the FE being opened — correct for the tombstone, F10, launcher **Open RPG
      UI**, and a plain browser visit alike.
- [ ] Actionability is the server's own `InjectorConnected` (`RpgStore.cs:1059-1060`); a pending fact
      becomes actionable when an injector connects.
- [ ] The FE is never blocked: a failed write is retried next load, never a blocking error.
- [ ] A stable refusal-reason vocabulary in the `onboarding.story-*` style.
- [ ] The injector consume arm rides the **existing** cadence; no new poll.
- [ ] Capture is **not** built here; no capture claim is made; ActorHub gate N/A stated.
- [ ] Capture pacing, when it lands, is a structural per-frame cap with a comment — never a tuning key.
- [ ] No derived population count asserted anywhere; substrate is in-memory.

## Open Questions

### Implemented scope (recorded at build time, 2026-09-15)

The spec's project-structure table originally listed an injector **consume arm** in `InjectorLoop`.
It was **deliberately not implemented**, and this is the honest reason rather than an omission:

- Decision 1 makes the capture a **separate program**. This module's deliverable is the *trigger fact*
  plus the *actionability* gate on it.
- The actionability condition is **the server knowing the injector is connected** —
  `RpgStore.InjectorConnected`, the existing 5-second heartbeat window. The injector's side of that
  condition is **already shipped**: the injector heartbeats today, and the server exposes the read.
  No new injector code is required to make the fact actionable.
- An injector-side "read the fact and fire" arm with **no capture to fire** would be a trigger wired
  to nothing — unreachable surface, which this repo forbids ("a wire protocol with unreachable verbs
  is untested surface"). The capture program adds that arm together with the capture it drives.

So the implemented surface is: the durable row, the two endpoints, the actionability read, and the
FE's one-shot write. Everything needed to *consume* the fact is present; nothing pretends to consume it.

> **Decided (owner, 2026-09-15)** — the storage shape below was a proposed default and is now
> **confirmed**: **a dedicated row keyed `(player_id)`** (simplest idempotency key, no join), mirroring
> `OnboardingStoryRow` (`RpgStore.Onboarding.cs:25`). Not a column on an existing per-player row. Settled;
> behaviour is unchanged from the proposal.

1. **Whether the "capture is complete" signal belongs to this program or the capture program.** The map
   is explicit that the **manifest** is the capture program's (`rift-gate-map.md:126`, gap
   "Capture completeness / manifest"). This module records only the *trigger fact*; the completion
   contract is not ours. Stated here so a downstream reader does not assume a completion signal exists.
   This is a scope boundary, not an open question.
