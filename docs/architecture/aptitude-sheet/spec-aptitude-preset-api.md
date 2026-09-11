# Spec: `aptitude-preset-api`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal locks:** **D9** · **D13** · **D14** · **E2** · **E5** · **E6** · **E8** · **A13–A19** · **S1/S3/S5/S7**  
**Prior art:** `RpgStore` item loadouts (`rpg_item_loadout` / `ListLoadouts` / `SaveLoadout`) —
named player-scoped library discipline. **Not** aura `LoadoutEndpoints` (GET/POST replace only).  
**Favour SSOT (Built, server-only):** `SpeciesBuildPlanCatalog.SharesFor` · `_species-build-plan.json`

---

## Objective

Persist player **aptitude build presets** (twelve-aptitude share templates + D13 constraints),
activation per allocate binding, **materialize(budget)** with leftover-legal clamps, and
**transactional Activate**. Expose species favour as **target permille** for New/Auto-assign seed —
never as Hub UniqueDemon baseline (**E3**).

---

## Tech stack

- Server: endpoints under `/api/aptitude-presets` (one namespace)
- Data: Sqlite in `FusionRpg.Data` only (DAL guard)
- SignalR: scoped `AptitudesUpdated` on CRUD / active / Activate allocate
- Magnitudes: **`long`**; widen before multiply; `/1000` last; overflow throws
- Mode B Activate: call into **existing** species-build priced respec — do not fork economy

---

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests --filter AptitudePreset
dotnet test tests/FusionRpg.Server.Tests --filter AptitudePreset
.\scripts\guard-dal.ps1
```

---

## Project structure

| Path | Duty |
|---|---|
| `FusionRpg.Data` store methods | Library rows + active bindings (mirror item-loadout table discipline) |
| Server endpoints | CRUD, active, materialize, favour GET, **activate** |
| Core materialize helper | D13 + E2 leftover legal (shareable with auto-assign) |
| Tuning | Soft max presets / default row abs max → `data/tuning/aptitudes.v{n}.json` (or aptitude-presets) |

---

## Data model (sketch)

| Entity | Keys / fields |
|---|---|
| **Preset** | `presetId`, `playerId`, `name`, `kind` (`player` \| `systemCopy`), rows[] |
| **Row** | `aptitudeId`, `targetPermille` (0..1000), optional `minAbs`/`maxAbs`/`minPermille`/`maxPermille` |
| **Active** | `(playerId, scope, scopeKey) → presetId` |

**Save validation (E5):** sum of `targetPermille` across twelve primaries **== 1000** or 400 with named
reason. Abs fields optional; not a second sum SSOT.

**Soft cap (E8):** max presets per player — tunable soft; refuse create with named reason (not a
grind ceiling on PointBudget).

---

## Materialize (D13 + E2)

```
for each aptitude:
  lo = max(minAbs?, floor(budget * minPermille? / 1000))
  hi = min(maxAbs?, floor(budget * maxPermille? / 1000))
  if lo > hi → refuse (named reason)
  share = clamp(floor(budget * targetPermille / 1000), lo, hi)  // long math — see Code style
leftover = budget - sum(shares)   // LEGAL — do not redistribute
```

Unset abs or ‰ axis → ignore that axis. POST allocate/respec still rejects overspend (sum > budget);
sum < budget is fine (leftover).

---

## API sketch

| Verb | Duty |
|---|---|
| `GET /api/aptitude-presets/{playerId}` | Library list |
| `POST /api/aptitude-presets` | Create (copy-on-edit from favour/system OK) |
| `PUT /api/aptitude-presets/{presetId}` | Update (sum-1000) |
| `DELETE /api/aptitude-presets/{presetId}` | Delete; clear active if pointed here |
| `GET/PUT .../active?scope=&scopeKey=` | Active preset for binding |
| `POST .../materialize` | Body: presetId + budget → shares + leftover |
| `GET .../favour/{speciesId}` | **S1:** `{ sharesPermille: Record<aptitudeId, long> }` — target permille from catalog. Planned species: values sum **1000**. No plan: empty object `{}` (not an error). Read-only. |
| `POST .../activate` | **S3 (locked):** transactional Activate — see below |

### Favour contract (S1 / S7)

- Unit is **permille**, never points.
- Server maps `SpeciesBuildPlanCatalog.SharesFor` → response; FE **must not** call catalog or reuse
  species GET `baseline` points as template ‰.
- Empty `{}` → clients **refuse** favour seed / `species-favour` auto-assign with named reason;
  offer Even (S7).

### Activate (S3 — locked)

```
POST /api/aptitude-presets/activate
body: { playerId, presetId, scope, scopeKey, /* budget context as needed */ }
```

**One server transaction:**

1. Materialize preset to binding budget (D13/E2).
2. Set active `(scope, scopeKey) → presetId`.
3. Commit allocation:
   - Mode A (`unique` / `instanceId`) → UniqueDemon allocate
   - Mode C (`commander`) → commander allocate
   - Mode B (`species` / `speciesId`) → **existing** priced `species-build/respec` path (same
     pricing/correlation rules — do not fork)

On any failure: no half-active (active unchanged if allocate/respec fails; allocate unchanged if
active write fails). Emit scoped `AptitudesUpdated`.

Hosts / preset-console call **only** this endpoint for Activate — no client sequence of
set-active then separate allocate.

**Level-up (E6):** no server job rematerializes active preset into allocation on level change.

---

## Code style

```csharp
checked
{
    long scaled = (long)budget * row.TargetPermille / 1000L;
}
```

No `float` magnitudes. No silent clamp when `lo > hi`.

---

## Testing strategy

- Save rejects permille sum ≠ 1000.
- Materialize: leftover ≥ 0; conflicting lo>hi → error; no redistribute into other aptitudes.
- Favour GET permille matches catalog for a known species; empty species → `{}`.
- Activate: active + allocate succeed together; failure leaves neither half-applied.
- Mode B Activate invokes respec pricing (not free allocate).
- Soft max presets: create past soft cap → named refusal.
- DAL: no SQL outside Data project.

---

## Boundaries

- **Always:** `long`; leftover legal; favour read-only permille; transactional Activate; DAL boundary.
- **Ask first:** Auto rematerialize-on-level-up (deferred E6); Vision loadout fields.
- **Never:** FE-localStorage as SSOT; mutate `_species-build-plan.json`; EffectiveUnique Hub fill;
  hard progression ceiling on budget via abs max; split Activate client sequence; cite
  `LoadoutEndpoints` as named-library CRUD.

---

## Success Criteria

- [ ] CRUD + active binding persist across restart.
- [ ] Materialize matches D13/E2; Save enforces E5.
- [ ] Favour endpoint returns permille (S1); empty handled (S7).
- [ ] `POST .../activate` is sole Activate path (S3).
- [ ] No level-up autorespec (E6).
- [ ] Soft caps tunable (E8).

## Open Questions

None for Wave 1 — A18/A19 / S1–S3 locked. Exact soft-cap numbers live in tuning at implement time.
