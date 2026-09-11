# Module: `derived-recipe-wire`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Amends:** `derived-console` recipe · `DerivedTab` · bindSurface · [../actor-sheet/spec-derived-tab.md](../actor-sheet/spec-derived-tab.md)  
**Queue:** menu-refactor P0 Harden → Done  
**Draft:** `docs/design/gui-lego/surfaces/derived-console.html` (exists — amend if paint/copy change)

---

## Objective

Land Waves 1–2 into the live Derived surface: thin host, closed bus, revision on sheet invalidate,
no god TSX regression. **D7 gauge not required** for Done.

## Fetch matrix

| Phase | Host fetch |
|---|---|
| Until projection complete | `/sheet` + lean `/derived` OK; lean Pending-honest |
| After `derived-sheet-projection` lands | Prefer **sheet-only**; drop lean when metadata complete |

## Bus (closed)

`derived.search.set` | `showUnchanged.set` | `tab.set` | `variant.set` | `channel.select` | `retry`

Realtime: host SignalR / live invalidate → `invalidateQueries(["actorSheet", id])` → re-fold →
`revision` bump. Pieces never fetch.

## Success criteria

- [ ] Wave 1–2 module success criteria green on live recipe path.
- [ ] Contract: cook IA; inspect-split landmark; no player-band jargon.
- [ ] Fetch matrix followed; sheet-only when ready.
- [ ] Queue P0 Harden → Done **without** requiring D7.
- [ ] Draft surface still matches landmarks (or amended intentionally).

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run DerivedTab DerivedCombatConsole.contract foldDerivedSurfaceVm
Test-Path docs/design/gui-lego/surfaces/derived-console.html
```

## Boundaries

- **Always:** amend existing recipe/VM.
- **Never:** revive `ui/actor/derived/*` god console.
