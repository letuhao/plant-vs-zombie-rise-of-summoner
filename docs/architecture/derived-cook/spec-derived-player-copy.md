# Module: `derived-player-copy`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Design:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §4–5.2a

---

## Objective

Player-band strings are fiction/locale — compose sentences, unit sentences, state labels, CAP copy.
Ban engine vocabulary on the Derived surface.

## Bans (player band)

| Ban | Replace with |
|---|---|
| ``Join: ${channelId}`` | Fiction title / reading only |
| `Sources (GG-49)` | Fiction “Sources” / catalog title |
| Raw `no-producer` / `stub` tags as sole chrome | Fiction state sentences from design §3 |
| FE-only `COMPOSE_SENTENCE` as permanent SSOT | Catalog or locale keys (may seed from current sentences) |

## Success criteria

- [ ] Contract/e2e: no `GG-49`, no raw channelId in inspect hero meta.
- [ ] State tags use fiction labels.
- [ ] Compose/unit lines still correct for FlatReplace (strongest wins).

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run DerivedCombatConsole.contract
rg -n "GG-49|Join:" web/fusion-rpg-web/src
```

## Boundaries

- **Never:** show `definitions.md` or Intent vocabulary.
