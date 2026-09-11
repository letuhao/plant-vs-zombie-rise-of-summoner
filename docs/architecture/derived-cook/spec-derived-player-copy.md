# Module: `derived-player-copy`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Design:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §4–5.2a · §5.3 unattributed  
**Owns:** compose/unit/state fiction · contribution fiction / unattributed rows

---

## Objective

Player-band strings are fiction/locale. Ban engine vocabulary on the Derived surface.

## String inventory (owned)

| Slot | Ban | Replace |
|---|---|---|
| Inspect join meta | ``Join: ${channelId}`` | Fiction title / reading only |
| Sources header | `Sources (GG-49)` | Fiction “Sources” / catalog title |
| State tags | raw `no-producer` / `stub` / `active` | Fiction sentences from design §3 |
| Compose / unit | FE-only permanent constants | Catalog or locale keys (seed from current `COMPOSE_SENTENCE` / `UNIT_SENTENCE`) |
| Unattributed contrib | Silent fold into total | Fiction producer label or explicit `unattributed` row (§5.3) |

## Home for copy

| Copy class | Preferred home |
|---|---|
| Family displayName / reading | `derived-stat-catalog.v{n}.json` |
| Compose / unit sentences | locale keys or catalog `composeSentence` / `unitSentence` fields |
| State fiction | locale keys keyed by renderState |

## Success criteria

- [ ] Contract/e2e: no `GG-49`, no raw channelId in inspect hero meta.
- [ ] State tags use fiction labels.
- [ ] FlatReplace still explains strongest-wins.
- [ ] Unattributed contributions visible as such.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run DerivedCombatConsole.contract
rg -n "GG-49|Join:" web/fusion-rpg-web/src
```

## Boundaries

- **Never:** show `definitions.md` or Intent vocabulary.
