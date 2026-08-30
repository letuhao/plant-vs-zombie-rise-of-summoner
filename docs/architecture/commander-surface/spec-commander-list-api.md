# Spec: `commander-list-api`

**Module id:** `commander-list-api` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** `default-persistence` · **Blocks:** `commanders-layer`, `sanctum-readout`
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **Roster source today:** `CommanderIds.All` = Dave, Zomboss (`CommanderId.cs:39`) — **FE list filters
   to player empire** (Dave only until empire program adds leaders).
2. **Default flag** reads `default-persistence` — `isDefault` on exactly one row when N>1; N=1 Dave always
   default implicitly.
3. **Active aura summary** = **loadout ∩ AuraRuntime** for each commander — not loadout alone. Cite
   `AuraRuntimeEndpoints.cs:39-63` (`RuntimeFor` reads loadout fresh; `activeAuraIds` from session
   runtime). Today Dave-only via `/api/loadout` + `/api/aura-runtime`.
4. **Aura runtime enable must run before list shows active** — equipping in loadout without enable yields
   `activeAuraId: null` until `POST /api/aura-runtime/.../enable` succeeds.
5. **Location + legion fields are stubs** — **`null` v1** (closed — no `"—"` drift).
6. **DTOs live in `FusionRpg.Contracts`** — web adapts via `contract/adapt.ts`, never binds REST types
   (`contract/types.ts` header rule).

---

## Objective

Give the Commanders layer and Sanctum readout a single list endpoint: player-empire commanders with default
badge, display name, active aura label, and map/legion stub fields.

**Success:** `GET /api/commanders/1` returns one Dave row with `isDefault: true`, `activeAuraId`/`Name`
from loadout ∩ runtime when Might is equipped and enabled; Zomboss never appears.

---

## ⛔ Program acceptance share

Integration test: `GET /api/commanders/{playerId}` — Zomboss absent from `commanders[]`; when loadout
contains `"Might"` and aura runtime has it active, row shows `activeAuraId: "Might"`,
`activeAuraName: "Might"`. This module is not done until that test is green.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~CommanderList
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~CommanderId
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~AuraContentCatalog
```

---

## Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Contracts/CommanderDtos.cs` | **new** — `CommanderListRowDto`, `CommanderListResponse` |
| `src/FusionRpg.Server/CommanderEndpoints.cs` | **new** — list + default routes (single file) |
| `src/FusionRpg.Core/Commanders/PlayerEmpireCommanders.cs` | **new** — filter `CommanderIds.All` → empire list |
| `web/fusion-rpg-web/src/contract/types.ts` | edit — `CommanderListRow` pending/known |
| `web/fusion-rpg-web/src/contract/adapt.ts` | edit — adapter when endpoint exists |
| `tests/FusionRpg.Server.Tests/CommanderListEndpointsTests.cs` | **new** |

---

## Design

### `GET /api/commanders/{playerId:long}`

Response:

```json
{
  "defaultLawnCommanderId": "commander:dave",
  "commanders": [
    {
      "id": "commander:dave",
      "displayName": "Crazy Dave",
      "isDefault": true,
      "activeAuraId": "Might",
      "activeAuraName": "Might",
      "locationStub": null,
      "legionStub": null
    }
  ]
}
```

| Field | Rule |
|---|---|
| `id` | `CommanderIds.ToStableId` |
| `displayName` | Fixed map v1: Dave → `"Crazy Dave"` |
| `isDefault` | `id === defaultLawnCommanderId` from persistence |
| `activeAuraId` | First (or only) aura in **equipped loadout ∩ `AuraRuntime.ActiveAuraIds`**; catalog ids from `AuraContentCatalog` (`"Might"`, `"Fortitude"`, … — not prefixed) |
| `activeAuraName` | Same string as id v1 (catalog display = id) |
| `locationStub` / `legionStub` | **`null` v1** |

### Active aura algorithm

For each empire commander (Dave today):

1. `equipped = store.GetLoadout(DaveScope(playerId))` filtered with `AuraContentCatalog.IsKnown`.
2. `runtime = AuraRuntimeEndpoints.RuntimeFor(playerId, store)` (or equivalent server helper).
3. `active = equipped.Where(id => runtime.ActiveAuraIds.Contains(id)).ToList()`.
4. If `active.Count == 0` → `activeAuraId: null`, `activeAuraName: null`.
5. If `active.Count >= 1` → use first active id (max one active per tuning; order deterministic).

**Dependency:** aura-skill T18c runtime must be enabled for the aura to appear active — list-api does not
infer active from loadout alone.

### Empire filter

Server-side allowlist — today `[CommanderId.Dave]` only. Zomboss excluded from player API entirely (ideal
§4). World/AI uses separate surfaces.

### POST default

Reuse `default-persistence` POST `/api/commanders/default` in the same `CommanderEndpoints.cs` group.

Optional SignalR broadcast deferred (ideal §4 v1 poll-only).

---

## Code style

- **Endpoint file:** one `CommanderEndpoints.cs` with `MapGroup("/api/commanders")`; list handler calls
  `PlayerEmpireCommanders` + persistence get + aura helper.
- **Contracts:** record DTOs in `FusionRpg.Contracts`; include `activeAuraId` on row DTO.
- **Web:** extend `CommanderListRow` in `types.ts` with `activeAuraId: string | null`; adapt in
  `adapt.ts` mirroring `CreaturesLayer` list patterns.
- **Tests:** server integration with in-memory store + runtime enable; never use fictional aura ids like
  `"aura.sun_blessing"`.

---

## Testing strategy

| Test | Assert |
|---|---|
| List known player | One Dave row, `isDefault: true` |
| Unknown player | `404` |
| Zomboss absent | Never in `commanders[]` |
| Loadout only, not enabled | `activeAuraId: null` |
| Loadout + runtime enable Might | `activeAuraId: "Might"`, `activeAuraName: "Might"` |
| After POST default | `isDefault` moves when second commander added (fixture test with mock roster) |
| Stubs | `locationStub` and `legionStub` are `null` |

---

## Boundaries

- **Always:** filter Zomboss; contracts in `FusionRpg.Contracts`; adapt in web layer; active = loadout ∩
  runtime; real catalog ids only
- **Ask first:** adding Penny to empire allowlist (content decision)
- **Never:** expose `unique_actors`; return full loadout body in list row; require list call before play;
  infer active aura from loadout without runtime

---

## Success criteria

- [ ] Server tests pass for list shape, empire filter, and aura algorithm
- [ ] Contract types compile in web with `activeAuraId`
- [ ] ⛔ share: Zomboss absent + Might active fields integration test green
- [ ] Sanctum readout and Commanders layer can consume one GET (integration in their modules)

---

## Open questions

- **Display names for future commanders** — catalog table vs hardcoded map until empire program ships.
  *(Stub fields closed: `null` v1.)*
