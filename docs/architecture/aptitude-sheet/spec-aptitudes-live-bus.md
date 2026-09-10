# Spec: `aptitudes-live-bus`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D4** SignalR scope+key  
**Code anchors:** `AptitudeEndpoints.BroadcastBestEffort` · `SpeciesBuildEndpoints` broadcast ·
`hub-provider.tsx` `onAptitudesUpdated`

---

## Objective

One SignalR payload shape for all aptitude mutations so FE and Injector can invalidate / reload the
correct scope — ending the thin `{ playerId }` lie. Also own FE type honesty for commander GET’s
nested **`species`** map (**S10**) so Mode C / Pacts consumers are not blind to server projection.

---

## Tech stack

- Server: `IHubContext<RpgHub>` → WebGroup + InjectorGroup (existing pattern)
- FE: React Query invalidate in `hub-provider.tsx`
- Injector: existing `AptitudesUpdated` → `aptitudes.allocation.reload` (widen in `unique-lawn-wire`)

---

## Commands

```powershell
# Manual: allocate unique / respec species / allocate commander; watch SignalR payload in FE logs
dotnet test tests/FusionRpg.Server.Tests --filter AptitudesUpdated
```

---

## Project structure

| Path | Duty |
|---|---|
| Shared broadcast helper (Server) | Single method used by commander, species respec, unique allocate, preset activate |
| `web/.../hub-provider.tsx` | Invalidate by scope |
| `web/.../lib/bus/queryKeys.ts` (or equiv) | `uniqueAptitudes(instanceId)` key |
| FE `AptitudesState` (types) | Include nested `species` map from commander GET (**S10**) — match server `ProjectState` |

---

## Payload contract

```json
{
  "playerId": 1,
  "scope": "unique",
  "instanceId": "ua_…",
  "speciesId": null
}
```

| `scope` | Required keys | FE invalidate |
|---|---|---|
| `commander` | `playerId` | `aptitudes(playerId)` |
| `species` | `playerId`, `speciesId` | `speciesAptitudes(playerId, speciesId)` (+ list if any) |
| `unique` | `playerId`, `instanceId` | `uniqueAptitudes(instanceId)`; optionally actor sheet |

Unknown/missing `scope`: treat as **legacy** — invalidate commander + all species aptitude queries
(backward compatible with old injectors during rollout). Prefer always sending `scope` from new code.

Injector may ignore scope until `unique-lawn-wire` lands; then reload must refresh unique cache too.

### FE type honesty (S10)

Server commander GET still projects nested effective `species` map. FE `AptitudesState` must include
that field (typed) so Mode C / Pacts invalidate and display stay honest. This module owns the type +
invalidate wiring; it does **not** invent a second species write path.

---

## Code style

One private/static `BroadcastAptitudesUpdated(hub, dto)` — do not triple-copy try/catch blocks.

---

## Testing strategy

- Unit/integration: each write path emits correct scope+key.
- FE: mock hub message invalidates only the matching query key (plus legacy fallback).

---

## Boundaries

- **Always:** Both WebGroup and InjectorGroup; best-effort catch.
- **Ask first:** Renaming the hub event method.
- **Never:** Drop `playerId`; send allocation shares on the wire (refetch is SSOT).

---

## Success criteria

- [ ] Commander, species respec, unique allocate, and preset activate all emit scoped payload.
- [ ] FE unique query invalidates on unique allocate without requiring full page reload.
- [ ] FE `AptitudesState` includes nested `species` from commander GET (S10).
- [ ] Legacy `{ playerId }` alone still safe during transition.
