# Module: `shield-recipe-wire`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Recipe:** `docs/design/gui-lego/recipes/shield-console.json` (author) → FE copy  
**Host:** thin `ShieldTab`

---

## Objective

Assemble Shield tab as recipe + fold + bind + RecipeMount. Closed bus; revision on sheet/shields
invalidate. Coordinate Hot with condition-glance `sheet-hot-projection`.

## Bus (closed, proposed)

`shield.layer.select` | `shield.retry`  
Host invalidates on SignalR live-state → re-fold.

## Success criteria

- [ ] Recipe JSON + assembled HTML draft exist for owner gate.
- [ ] Thin host only — no chrome-only Empty layer TSX as product.
- [ ] Live Hot: bar + inspect + omni; glance summary still works on Condition.
- [ ] Landmark tests for stack bar.

## Commands

```powershell
Test-Path docs/design/gui-lego/recipes/shield-console.json
cd web\fusion-rpg-web; npm test -- --run ShieldTab
```

## Boundaries

- **Always:** GUI Lego path.
- **Never:** fork PanelShell; piece-level SignalR.
