# Module: `derived-volume-guard`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Ideal gap:** **G6**  
**Design Guard 7:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §7

---

## Objective

Prove fold + RecipeMount remain correct under full cook expansion — no silent drops, no O(n²) freezes
in unit/contract scope.

## Fixture shape (G6)

| Fixture | Scale |
|---|---|
| `volume:current` | Current §1 scale — ~269 registered + open-prefix expand (~385 when status sparse dims expand) |
| `volume:stress500` | 500 synthetic channel rows |

Pin counts to design Guard 7 / §1 — do not hand-recompute a third number.

## Success criteria

- [ ] `volume:current` expands without throw; omni not dropped.
- [ ] `volume:stress500`: fold completes; selection/inspect defined.
- [ ] Documented note: not a live FPS claim without probe.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run foldDerivedSurfaceVm
```

## Boundaries

- **Never:** drop omni to “help perf.”
