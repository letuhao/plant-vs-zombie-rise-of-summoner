# Spec: `standing-compose`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-standing · D2 synthetics + filter · E9 `ActorPowerCache.Compose(AtomRow[])`  
**Wave:** 2 (depends on `combat-membership`; Hub-only after fuse)  
**Code anchors:** `UniqueActorHubCompose.ProjectStanding` (~237–255) — equip + tree only today · `AptitudeSubsystem` Hub writers · `ActorPowerCache.Compose` · `SyntheticStatDerived` tree bridge

---

## Objective

UniqueActor **Standing** must price **all Hub combat writers** that membership includes — not equip+tree atoms alone. Aptitude (and any other non-atom Hub combat contributors) become **synthetic `stat.derived` `AtomRow`s**, filtered by `combat-membership`, then `ActorPowerCache.Compose`.

**Rejected:** naive “price the Hub Derived snapshot” (imports Θ via `progression.power`).

Success: high aptitude into dodge/cooldown raises Standing O+S+C; Θ alone does not; Condition tab vector stays five-axis; player combat-power label owned by `copy-surfaces`.

---

## Tech stack

- Server: `UniqueActorHubCompose.ProjectStanding`
- Core: `ActorPowerCache`, atom row shape used by tree bridge today
- Contribute/consume: Hub Derived totals → synthetics (read Hub, do not private-fold aptitude math twice)

---

## Commands

```powershell
dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~Standing|UniqueActorHub|ProjectStanding"
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorPower|CombatPowerMembership"
```

---

## Project structure

| Path | Duty |
|---|---|
| `UniqueActorHubCompose.ProjectStanding` | Build atom list: equip + tree + **synthetics from Hub combat writers** |
| Synthetic builder | For each membership channel where Hub total has non-atom contribution (or all Hub combat deltas not already in atom list) → `stat.derived` rows |
| Dedup | Do not double-count equip/tree already present as atoms |
| DTO | Keep `ActorStandingDto` five axes; no geomean |

### Algorithm (locked shape)

1. Resolve Hub Derived for the UniqueActor (same compose as sheet Derived).
2. Collect existing equip + tree `AtomRow`s (today’s path).
3. For membership channels: compute residual (Hub total − already priced by those atoms) **or** emit synthetics for aptitude SourceIds / non-atom writers — choose one approach in implementation, document in PR; must not double-count.
4. Filter every atom through `CombatPowerMembership.Includes(channel)`.
5. `ActorPowerCache.Compose(filteredAtoms)` → Standing DTO.

Magnitudes remain `long` through Compose.

---

## Code style

- Reuse tree’s `SyntheticStatDerived` pattern; do not invent a second Standing composer type.
- No FE private fold of combat power.

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit/Server | Aptitude-only grant on combat channel → Standing Offense/Survivability/Control rises |
| Unit | Hub with only Θ / progression → Standing unchanged by Θ |
| Unit | Equip+tree still counted once |
| Unit | Cooldown membership channel from Hub writer raises Standing |
| Contract | Still `Compose(AtomRow[])` only — no snapshot overload |

---

## Boundaries

- **Always:** Synthetics + membership; Hub-only numbers; long; no Θ in Standing.
- **Ask first:** Changing E9 Compose API.
- **Never:** Second Standing composer; FE matrix; price full Hub snapshot.

---

## Success criteria

- [ ] `ProjectStanding` includes aptitude (and other Hub combat writers) via synthetics + filter.
- [ ] Double-count proven absent for equip/tree.
- [ ] Θ / `progression.*` do not raise Standing.
- [ ] Five-axis DTO unchanged; label work deferred to `copy-surfaces`.
