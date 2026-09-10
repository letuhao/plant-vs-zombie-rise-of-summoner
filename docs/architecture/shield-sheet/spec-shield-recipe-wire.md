# Module: `shield-recipe-wire`

**Program:** `shield-sheet` · **Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Recipe:** `docs/design/gui-lego/recipes/shield-console.json` — **must author before Done**  
**Surface draft:** `docs/design/gui-lego/surfaces/shield-console.html` — author for owner gate  
**Host:** thin `ShieldTab`  
**Locks:** **S1** sheet layers · shared invalidate with Condition

---

## Objective

Assemble Shield tab as recipe + fold + bind + RecipeMount. Closed bus; revision on sheet invalidate
(same `["actorSheet", id]` as Condition when layers ride `/sheet`).

## Bus (closed)

| Event | Effect |
|---|---|
| `shield.layer.select` | selectedShieldId |
| `shield.retry` | refetch / invalidate sheet |

Host: SignalR `ActorLiveStateChanged` (preferred) → invalidate actorSheet → re-fold.

## Draft-exists gates

- [ ] `docs/design/gui-lego/recipes/shield-console.json`
- [ ] `docs/design/gui-lego/pieces/shield-stack-bar.html`
- [ ] `docs/design/gui-lego/pieces/shield-layer-inspect.html`
- [ ] Assembled surface HTML (optional but preferred for owner gate)

## Success criteria

- [ ] Recipe + drafts exist; thin host only.
- [ ] Live Hot: bar + inspect + omni; glance summary still works on Condition.
- [ ] No three permanent Empty layer wells as product chrome.
- [ ] Landmark tests for stack bar.

## Commands

```powershell
Test-Path docs/design/gui-lego/recipes/shield-console.json
Test-Path docs/design/gui-lego/pieces/shield-stack-bar.html
cd web\fusion-rpg-web; npm test -- --run ShieldTab
```

## Boundaries

- **Always:** GUI Lego path.
- **Never:** fork PanelShell; piece-level SignalR.
