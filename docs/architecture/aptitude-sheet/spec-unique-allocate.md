# Spec: `unique-allocate`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** Mode A · **D1** free-build · **D4** write contract · **A5** Done gate  
**Code anchors:** `AptitudeEndpoints.cs` · `RpgStore.Aptitudes.cs` · `PointBudget` · `UniqueCreatureAllocation` · `UniqueActorDto.Level`

---

## Objective

Give players a real UniqueCreature allocate surface: **GET** persisted shares + budget honesty and
**POST** save by `instanceId`, so ActorSheet Mode A is not forced onto the commander pool.

Success: a UniqueActor with level ≥ 2 can spend UniqueCreature points within budget; overspend returns
409; empty allocation remains legal (zero unique Hub contribution until spend — D1).

---

## Tech stack

- Server: ASP.NET Minimal APIs (`FusionRpg.Server`)
- Store: `FusionRpg.Data` `SaveAllocation` / `LoadAllocation(AllocationScope.UniqueCreature, instanceId)`
- FE bus: React Query hooks mirroring `useAptitudes` / `useSaveAptitudes`

---

## Commands

```powershell
# After implement
curl -s http://127.0.0.1:5088/api/aptitudes/unique/<instanceId>
curl -s -X POST http://127.0.0.1:5088/api/aptitudes/unique/allocate `
  -H "Content-Type: application/json" `
  -d '{ "instanceId": "<id>", "shares": { "might": 1 } }'
dotnet test tests/FusionRpg.Server.Tests --filter UniqueAptitude
dotnet test tests/FusionRpg.Data.Tests --filter Allocation
```

---

## Project structure

| Path | Duty |
|---|---|
| `src/FusionRpg.Server/AptitudeEndpoints.cs` | Add unique GET/POST; keep commander routes |
| `src/FusionRpg.Contracts/` (if needed) | DTO types for unique state |
| `web/.../lib/bus/queries.ts` + `mutations.ts` | `useUniqueAptitudes` / `useSaveUniqueAptitudes` |
| `web/.../lib/bus/types.ts` | Unique aptitude state type |
| `tests/FusionRpg.Server.Tests/` | Budget / 404 / 409 / isolation |

---

## Routes

| Method | Route | Duty |
|---|---|---|
| GET | `/api/aptitudes/unique/{instanceId}` | Project UniqueCreature state for specimen |
| POST | `/api/aptitudes/unique/allocate` | Body `{ instanceId, shares }`; save UniqueCreature |

**Ownership:** specimen must exist (`GetUniqueActor`); player ownership check matches other actor APIs.

### GET response (minimum)

| Field | Type | Notes |
|---|---|---|
| `instanceId` | string | |
| `playerId` | long | owner |
| `shares` | `Record<string, long>` | **Persisted** UniqueCreature only — not baseline fill (D1) |
| `budget` | long | `PointBudget.PointsFor(UniqueCreature, UniqueCreatureSourceFromLevel(level), tuning)` |
| `spent` | long | scope total |
| `leftover` | long | `budget - spent` (≥ 0 when within budget) |
| `specimenLevel` | long | from UniqueActor |
| `theta` | long | optional honesty for chip; specimen power index if already used elsewhere |

Do **not** return EffectiveUnique / plan baseline as `shares`. Optional separate `commanderContribution`
read-only map may be added for A5b (spec `host-role-gate` / inspect) — not required inside this module’s
minimum GET if host loads commander GET separately.

### POST behavior

1. Parse `shares` → `AptitudeAllocation` with `AllocationScope.UniqueCreature`.
2. `PointBudget.CheckScope(UniqueCreature, allocation, sourceFromLevel, tuning)`.
3. If over budget → **409** `{ reason: "aptitudes.overbudget", spent, budget }` — never clamp.
4. `SaveAllocation(UniqueCreature, instanceId, allocation)`.
5. Broadcast via `aptitudes-live-bus` (`scope: "unique"`, `instanceId`, `playerId`).
6. Return fresh GET projection.

Unknown aptitude id → 400. Missing actor → 404.

---

## Code style

Mirror commander allocate in `AptitudeEndpoints.cs` (~32–58): Aggregate `AptitudeAllocation.Single`,
check budget, save, broadcast. Magnitudes `long`; widen before multiply inside PointBudget (already).

---

## Testing strategy

| Level | Cases |
|---|---|
| Server/Data | Empty shares OK; spend within budget; overspend 409; unknown id 400; wrong/missing instance 404 |
| Isolation | POST unique must not write Commander or CreatureType rows |
| FE (later host) | Hook query key `uniqueAptitudes(instanceId)` |

---

## Boundaries

- **Always:** Use existing `PointBudget` / store; 409 never clamp; raw shares only (D1).
- **Ask first:** Changing Hub to auto-apply UniqueCreature baseline; new HTTP beyond these routes.
- **Never:** Infer allocation from `typeId`/species; reopen Aspect; write CreatureType from this route.

---

## Success criteria

- [ ] GET/POST unique live; curl proves budget + leftover.
- [ ] Hub `LoadAllocation(UniqueCreature, id)` matches POST after save.
- [ ] FE hooks exist and are unused by commander Mode C path.
- [ ] Broadcast uses live-bus shape (depends on `aptitudes-live-bus`).

## Open questions

None — commander contribution readout placement is host/inspect (A5b).
