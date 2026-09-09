# Module: `derived-recipe-wire`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Amends:** `derived-console` recipe · `DerivedTab` · bindSurface · [../actor-sheet/spec-derived-tab.md](../actor-sheet/spec-derived-tab.md)  
**Queue:** menu-refactor P0 Harden → Done

---

## Objective

Land Waves 1–3 into the live Derived surface: thin host, closed bus, revision on sheet invalidate,
no god TSX regression.

## Bus (closed)

Keep: `derived.search.set` | `showUnchanged.set` | `tab.set` | `variant.set` | `channel.select` | `retry`.  
Realtime: host SignalR → invalidate `["actorSheet", id]` → re-fold → `revision` bump. Pieces never fetch.

## Success criteria

- [ ] All Wave 1–2 module success criteria green on live recipe path.
- [ ] Contract tests: cook IA; inspect-split landmark; no player-band jargon.
- [ ] Queue P0 marked Harden cleared → Done with link to this program.
- [ ] Prefer sheet-only fetch when projection complete; lean `/derived` Pending-honest.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run DerivedTab DerivedCombatConsole.contract foldDerivedSurfaceVm
```

## Boundaries

- **Always:** amend existing recipe/VM.
- **Never:** revive `ui/actor/derived/*` god console.
