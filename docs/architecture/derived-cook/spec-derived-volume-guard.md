# Module: `derived-volume-guard`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Design Guard 7:** volume fixture (~current channel count / 500 stress)

---

## Objective

Prove fold + RecipeMount remain correct and usable under full cook expansion — no silent drops,
no O(n²) UI freezes in unit/contract scope.

## Success criteria

- [ ] Fixture expands to current registered/cook scale without throw.
- [ ] Stress ~500 synthetic rows: fold completes; selection/inspect still defined.
- [ ] Documented runtime budget note (not a hard FPS claim without probe).

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run foldDerivedSurfaceVm
```

## Boundaries

- **Never:** drop omni to “help perf.”
