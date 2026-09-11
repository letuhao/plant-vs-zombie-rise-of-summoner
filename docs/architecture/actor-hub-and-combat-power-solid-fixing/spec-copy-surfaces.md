# Spec: `copy-surfaces`

**Program:** `actor-hub-and-combat-power-solid-fixing` · **Map:** [../actor-hub-and-combat-power-solid-fixing-map.md](../actor-hub-and-combat-power-solid-fixing-map.md)  
**Ideal:** HF-copy · D3 combat-power label = O+S+C · vocabulary three “power” words  
**Wave:** 2 (depends on `standing-compose`)  
**Code anchors:** `ActorStandingDto` five axes · `foldConditionSurfaceVm.ts` · Condition tab · any UI string “combat power” / omni glance as power

---

## Objective

Player-facing string **“combat power”** (and equivalents) means **Offense + Survivability + Control** from Standing only — not Utility, not Economy, not Θ, not specimen level, not a single `combat.power.omni` glance.

Condition tab keeps the **five-axis** Standing vector for inspect. Label sum is a derived display, not a sixth stored axis.

Success: one shared FE (or DTO helper) computes `combatPowerLabel = Offense + Survivability + Control`; grep-clean of omni-as-power product copy on actor sheet Condition / standing surfaces.

---

## Tech stack

- FE: Condition surface VM / ActorPanel standing widgets
- Optional Server: precomputed `combatPower` on standing DTO — **ask first**; default FE sum of three longs from existing DTO
- Magnitudes: display may format `long`; no float combat power

---

## Commands

```powershell
cd web/fusion-rpg-web
npm test -- --run foldConditionSurfaceVm
npm test -- --run ActorPanel
```

---

## Project structure

| Path | Duty |
|---|---|
| Condition / standing fold | Expose `combatPower` = O+S+C; keep five bars/radar |
| Copy / i18n strings | “combat power” → that sum only |
| Docs / aptitude-sheet checklist | HF-copy Done |
| Audit grep | Fail if sheet copy equates omni attack alone to combat power |

---

## Code style

- Prefer one pure helper `sumCombatPowerLabel(standing)` used by all surfaces.
- Do not geomean Standing (`PowerScalar` stays item-card).

---

## Testing strategy

| Level | Cases |
|---|---|
| Unit | Label equals O+S+C; U/E ignored |
| Unit | Vector still exposes five axes |
| Contract | No `PowerScalar` on UniqueActor Standing path |

---

## Boundaries

- **Always:** O+S+C label; five-axis vector retained; long.
- **Ask first:** Persisting combatPower on API; renaming player fiction.
- **Never:** Include Utility/Economy in the label; Θ or level as combat power; omni glance as the product number.

---

## Success criteria

- [ ] Shared O+S+C helper wired on Condition / standing UI.
- [ ] Utility/Economy visible on vector, excluded from “combat power” string.
- [ ] No sheet copy treats `combat.power.omni` alone as combat power.
