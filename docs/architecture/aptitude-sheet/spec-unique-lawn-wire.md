# Spec: `unique-lawn-wire`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D3** Done gate · bug **0c** · **A9** · **S4**  
**Code anchors:** `RpgClient.RefreshCommanderAllocationAsync` (~401–441) · `CheatState.SpeciesAllocation` /
`ApplyCommanderAllocation` / `ApplySpeciesAllocations` · Bound unique match bindings

---

## Objective

When a UniqueActor is **Bound** on the lawn, Injector Hot aptitude resolve must include that
specimen’s **UniqueDemon** allocation (summed with commander), not commander+species only —
**matching** [`UniqueActorHubCompose`](../../../src/FusionRpg.Server/UniqueActorHubCompose.cs)
(`commander + UniqueDemon(instanceId)`).

Success: allocate UniqueDemon in UI → SignalR reload → Bound unique on lawn reflects new points.
Server Hub alone is **not** acceptance.

### SSOT / FSM defect (locked, 2026-09-12)

Lawn Hot and UniqueActor sheet/web share **one** ActorHub vocabulary and the same omni /
`combat.*` consumers. Divergent aptitude input for the same Bound specimen
(`commander+species` on lawn vs `commander+UniqueDemon` on sheet) is an **ActorHub sole Hot
compose / FSM defect** — out of order, not a deferrable FE-only gap. See
[combat-power-number-ideal.md](../combat-power-number-ideal.md) and `decisions.md`
(ActorHub sole Hot compose gate; Demon progression source and spawn ownership).

---

## Tech stack

- Injector: `FusionRpg.Injector` CheatState / RpgClient / match unique bindings
- Core: `AptitudeAllocation` operator+ ; isolation — Bound unique must **not** merge DemonType
- HTTP: **`GET /api/aptitudes/unique/{instanceId}`** from `unique-allocate` (S4)

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter SpeciesAllocation
# Live (owner): deploy-play → Bound unique → POST unique allocate → observe lawn stats / probe
.\scripts\guard-secondary-no-unity.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| `RpgClient.cs` | On reload: for each Bound `instanceId`, `GET /api/aptitudes/unique/{instanceId}` |
| `CheatState.cs` | Cache `instanceId → UniqueDemon allocation`; resolve path for Bound entities |
| Aptitude Hot resolve | Bound unique: `commander + unique(instanceId)`; general: keep `commander + species` |

### Fetch strategy (S4 — locked)

Injector Bound reload uses **`GET /api/aptitudes/unique/{instanceId}`** per Bound id and caches in
CheatState.

**Do not** extend `GET /api/aptitudes/{playerId}` with a `uniques` map — commander payload already
hard-requires a literal `"shares"` shape for Injector; widening that channel is out of scope.

Ask before inventing a third HTTP channel. Do not pull empire species into unique resolve.

### Resolve isolation

| Entity | Allocation |
|---|---|
| Bound UniqueActor | `commander + Load/cache UniqueDemon(instanceId)` |
| Empire general | `commander + EffectiveSpecies` (unchanged) |
| Commander aura host | Commander only (unchanged) |

Empty UniqueDemon cache entry = empty allocation (D1) — still legal.

---

## Code style

Mirror `ApplySpeciesAllocations` dictionary cache shape for uniques; refresh on same
`aptitudes.allocation.reload` command (and scoped `AptitudesUpdated` when live-bus lands).

---

## Testing strategy

| Level | Cases |
|---|---|
| Core/Injector unit | Bound ctx resolves commander+unique; general still species; unique with same typeId never gets species |
| Fetch | Reload issues unique GET per Bound id — never relies on commander GET `uniques` |
| Live / probe | Document curl + lawn check in runbook note when implemented |

---

## Boundaries

- **Always:** Source isolation (decisions 2026-09-08); Secondary Unity-free; unique GET fetch (S4).
- **Ask first:** Any HTTP channel beyond unique-allocate GET/POST.
- **Never:** typeId→UniqueDemon inference; apply UniqueDemon baseline automatically;
  extend commander GET with `uniques` map.

---

## Success criteria

- [ ] After unique allocate + AptitudesUpdated, Bound unique Hot path includes UniqueDemon shares.
- [ ] Fetch path is unique GET only (S4).
- [ ] General lawn demons unchanged (species path).
- [ ] Regression: unique with same species id as a general does not inherit empire allocation.
