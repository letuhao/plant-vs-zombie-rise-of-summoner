# Module: `derived-render-states`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Design:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §3  
**Depends on:** `derived-sheet-projection`, `derived-cook-ia`

---

## Objective

Pure function: cook expansion × sheet channels → **exactly six** render states. No third
classification. `unregistered` must be reachable; `default` ≠ `no-producer`.

## State machine (normative)

| State | When |
|---|---|
| `active` | present, non-default meaningful value, has contributions or live write |
| `default` | registered, sitting at default, nothing interesting writing |
| `capped` | at registry cap |
| `stub` | placeholder curve (e.g. progression.power/realm until real) |
| `no-producer` | registered, nothing in game can write yet |
| `unregistered` | id in expand/code path but registry rejects |

Absent from snapshot for an **open-prefix** expand may be `default` or `no-producer` per registry
producer metadata — **never** collapse all absences to one state.

## Success criteria

- [ ] Unit goldens for each of six states with fixtures.
- [ ] Dead `NO_PRODUCER_HINT` same-as-else branch gone.
- [ ] Prefer consuming wire `renderState` when present; FE recompute only for parity tests.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run derivedCook foldDerivedSurfaceVm
```

## Boundaries

- **Never:** invent a seventh state; show blank for default.
