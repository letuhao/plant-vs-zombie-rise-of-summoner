# Spec: `unique-lawn-wire`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D3** Done gate · bug **0c** · **A9** · **S4**  
**Code anchors:** `RpgClient.RefreshCommanderAllocationAsync` (~401–441) · `CheatState.SpeciesAllocation` /
`ApplyCommanderAllocation` / `ApplySpeciesAllocations` · Bound unique match bindings

---

## Objective

When a UniqueActor is **Bound** on the lawn, Injector Hot aptitude resolve must include that
specimen’s **UniqueCreature** allocation (summed with commander), not commander+species only —
**matching** [`UniqueActorHubCompose`](../../../src/FusionRpg.Server/UniqueActorHubCompose.cs)
(`commander + UniqueCreature(instanceId)`).

Success: allocate UniqueCreature in UI → SignalR reload → Bound unique on lawn reflects new points.
Server Hub alone is **not** acceptance.

### SSOT / FSM defect (locked, 2026-09-12)

Lawn Hot and UniqueActor sheet/web share **one** ActorHub vocabulary and the same omni /
`combat.*` consumers. Divergent aptitude input for the same Bound specimen
(`commander+species` on lawn vs `commander+UniqueCreature` on sheet) is an **ActorHub sole Hot
compose / FSM defect** — out of order, not a deferrable FE-only gap. See
[combat-power-number-ideal.md](../combat-power-number-ideal.md) and `decisions.md`
(ActorHub sole Hot compose gate; Creature progression source and spawn ownership).

---

## Tech stack

- Injector: `FusionRpg.Injector` CheatState / RpgClient / match unique bindings
- Core: `AptitudeAllocation` operator+ ; isolation — Bound unique must **not** merge CreatureType
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
| `RpgClient.cs` | On every trigger in the cadence below: for each Bound `instanceId`, `GET /api/aptitudes/unique/{instanceId}` |
| `CheatState.cs` | Cache `instanceId → UniqueCreature allocation`; resolve path for Bound entities |
| `MatchHost.cs` | **Bind edge**: a specimen entering `Bound` re-triggers the fetch (see cadence below) |
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
| Bound UniqueActor | `commander + Load/cache UniqueCreature(instanceId)` |
| Empire general | `commander + EffectiveSpecies` (unchanged) |
| Commander aura host | Commander only (unchanged) |

Empty UniqueCreature cache entry = empty allocation (D1) — still legal.

---

### Refresh cadence — the FULL trigger set (amended 2026-09-13 after a live failure)

**This cache is keyed by Bound `instanceId`, so its KEY SET changes when a specimen binds — not only
when an allocation changes.** That is what makes it different from the commander cache it otherwise
mirrors: a commander allocation is global and its key set never moves, so "refresh on allocate" is a
complete trigger set there and an **incomplete** one here.

| # | Trigger | Why it is required |
|---|---|---|
| 1 | Session start (`RpgClient.StartAsync`) | Initial hydrate |
| 2 | SignalR reconnect | A change during the disconnected window is otherwise lost (same reason the commander cache re-syncs here) |
| 3 | `aptitudes.allocation.reload` / `AptitudesUpdated` | The allocation itself changed |
| 4 | **A specimen enters `Bound`** (`MatchHost`'s `ConsumeLastBound` edge) | **The KEY SET changed.** A specimen allocated *before* it was deployed is absent from every earlier fetch, so without this edge its allocation never loads at all |

Trigger 4 was missing from the first draft of this spec, which named only 1–3 — and
`aptitude-sheet-map.md`'s own module row said "on reload/**bind**" all along. The implementation
followed the spec faithfully, so the gap shipped.

**Live failure this cadence exists because of (2026-09-13).** A real specimen was minted, levelled,
allocated 30 `Might` through `POST /api/aptitudes/unique/allocate` (persisted: budget 882, spent 30),
then deployed to a live board (`phase: ActiveBound`, real ptr). Result on the lawn: `bonusAtk 0`,
`bonusAtkContribs ""` — the allocation never reached the entity, because the only fetch that could
have loaded it ran while the specimen was still in `Roster` phase. Re-allocating *after* it was Bound
fired trigger 3 and the same specimen immediately resolved `bonusAtk 1330`,
`bonusAtkContribs "aptitude.Might:Flat:1330"`, written live to Unity. **The compose chain was never
broken — only the cache-population edge was.** Order-dependence was the whole defect: deploy→allocate
worked, allocate→deploy silently produced an unbuffed actor.

---

## Code style

Mirror `ApplySpeciesAllocations` dictionary cache shape for uniques. Refresh on **every** trigger in
the cadence table above — mirroring the commander cache's *shape* is correct, mirroring its *trigger
set* is not (that cache is not keyed by a set that moves).

---

## Testing strategy

| Level | Cases |
|---|---|
| Core/Injector unit | Bound ctx resolves commander+unique; general still species; unique with same typeId never gets species |
| Fetch | Reload issues unique GET per Bound id — never relies on commander GET `uniques` |
| **Cadence (required — this is the level that was missing)** | **Each of the 4 triggers above independently populates the cache.** Above all, the ordering case the live failure hit: *allocate while the specimen is in `Roster`, THEN bind* must end with the allocation resolved. A cache still empty after a bind edge is a **failure**, not a timing tolerance. Mirrors `species-build`'s own `spec-allocation-transport.md` cadence test ("a stale cache after an `AptitudesUpdated` push is a failure"), which this module copied the cache shape from but not the test |
| Live / probe | Per `live-probe-standard.md`: allocate → deploy (in that order) → read the live entity back and assert the bonus actually reached Unity. A persisted-state read alone does **not** close this |

---

## Boundaries

- **Always:** Source isolation (decisions 2026-09-08); Secondary Unity-free; unique GET fetch (S4).
- **Ask first:** Any HTTP channel beyond unique-allocate GET/POST.
- **Never:** typeId→UniqueCreature inference; apply UniqueCreature baseline automatically;
  extend commander GET with `uniques` map.

---

## Success criteria

- [ ] After unique allocate + AptitudesUpdated, Bound unique Hot path includes UniqueCreature shares.
- [ ] **Order-independent: allocating BEFORE the specimen is deployed resolves just as well as
      allocating after.** Both orderings end with the shares on the live entity — stated explicitly
      because the original criterion named only the allocate-while-Bound ordering, and the other
      ordering is what failed live on 2026-09-13.
- [ ] **All 4 cadence triggers covered by a test**, including the bind edge (see Testing strategy).
- [ ] Fetch path is unique GET only (S4).
- [ ] General lawn creatures unchanged (species path).
- [ ] Regression: unique with same species id as a general does not inherit empire allocation.
- [ ] Live-probe evidence recorded per `live-probe-standard.md` — the live-engine read, not just the
      persisted read-back.
