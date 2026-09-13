# Spec: `lawn-action-bridge`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `basic-attack-seed`

> **Added 2026-09-13 after an adversarial audit.** Two other specs assumed this capability existed.
> It does not, and it is the single most likely day-one stop.

---

## Objective

**The injector cannot see the action stack at all.**

```
grep -r "FusionRpg.Core.Actions" src/FusionRpg.Injector/   →  0 files
grep -r "CostLedger"             src/FusionRpg.Injector/   →  0 hits
```

`basic-attack-grant` needs a compiled `act.attack` row to bind. `basic-attack-cost` needs a
`CostLedger` to call. Neither is reachable from `FusionRpg.Injector` today, and no other spec creates
the path.

Success: the injector can obtain the compiled basic-attack row **and** its cost rows, on the lawn, at
hit time, with no Server round trip — and the boundary guards still pass.

## Tech stack

`FusionRpg.Injector` (new reference + a cache), `FusionRpg.Core.Actions` (consumed, unchanged).
Delivery mirrors the existing session-cache pattern, not a new transport.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCatalog"
.\scripts\guard-secondary-no-unity.ps1
.\scripts\guard-dal.ps1
```

## The constraint that shapes everything

**No Server round trip on the hit path.** `overlay-control-loops.md:87` bans a Server FSM between
`combat.hit` and FA* apply; Hot rule 3 (`:150`) is *"**Never await** SignalR, HTTP, or SQLite for the
roll or apply."*

So the compiled row and its costs must be **resident in injector RAM before the first hit**, hydrated
on a cold edge, exactly like every other injector cache:

| Existing precedent | Hydrated at |
|---|---|
| `RefreshCommanderAllocationAsync` / `RefreshUniqueAptitudesAsync` | session start, reconnect, `AptitudesUpdated` |
| `TreeBoundAtomsCache` | session start, reconnect, `PassiveTreeUpdated` |

**Follow that shape, and enumerate the full trigger set** — DESIGN-GATE §2.16 requires it, and this
repo has shipped that bug class four times. The trigger set here is at minimum: session start,
reconnect, and any action-catalog change. **If the cache is keyed by anything whose key set moves,
the edge that moves it is a required trigger** — that is precisely the defect `unique-lawn-wire`
shipped.

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Injector/FusionRpg.Injector.csproj` | The reference (check it does not drag Unity types into a Unity-free assembly) |
| `src/FusionRpg.Injector/Effects/` (new cache) | Holds the compiled row + cost rows |
| `src/FusionRpg.Injector/RpgClient.cs` | Hydration on the existing cold edges |

## Code style

Hydrate wholesale, never incrementally — same contract as `ApplySpeciesAllocations` /
`ApplyUniqueAllocations`, so a stale entry cannot survive a refresh.

The injector consumes a **compiled** row; it does not compile, parse or import one. Seeding, parsing
and composing stay on the Server/Core side (`basic-attack-seed`), and SQL stays inside
`FusionRpg.Data` — `guard-dal.ps1` enforces that and this module must not widen it.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | The compiled row round-trips through whatever DTO carries it, cost rows included |
| Injector | The cache is populated before the first hit is processed; an empty cache degrades to "no RPG contribution", never a throw |
| Injector | Every trigger in the enumerated set repopulates it (§2.16) |
| Guard | `guard-secondary-no-unity.ps1` and `guard-dal.ps1` green — the new reference must not breach either |
| Negative | No HTTP/SQLite call occurs on the hit path; assert by source scan or by a test that fails if one is introduced |

## Boundaries

- **Always:** hydrate on cold edges; keep the hit path allocation-free of I/O.
- **Always:** enumerate the cache's full trigger set and test each (§2.16).
- **Ask first:** exposing more of `Core.Actions` to the injector than the basic attack needs. A broad
  reference is a standing invitation to run the whole action runtime on the lawn, which is *not* what
  this program scoped.
- **Never:** await the Server on the hit path; compile or import actions injector-side; add SQL
  outside `FusionRpg.Data`.

## Success criteria

- [ ] The injector holds the compiled `act.attack` row **and** its `stamina` cost row at match start.
- [ ] No HTTP, SignalR or SQLite call on the hit path, proven by a test that fails if one appears.
- [ ] The cache's trigger set is enumerated in this spec and each trigger has a test.
- [ ] An empty/unhydrated cache produces **no RPG contribution and no exception** — the same
      fail-closed posture as a null shooter in `lawn-hit-attribution`.
- [ ] `guard-secondary-no-unity.ps1` and `guard-dal.ps1` green.
