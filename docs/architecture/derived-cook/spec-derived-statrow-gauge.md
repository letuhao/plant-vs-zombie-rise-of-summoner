# Module: `derived-statrow-gauge`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Lock:** **D7 — deferred** — not Wave 1–2 Done gate  
**Design:** [../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md) §5.2b  
**Depends on:** Waves 1–2 green (IA + states + paint)

---

## Status: DEFERRED (D7)

Optional occupancy chrome after cook truth lands. Skipping this module does **not** block P0 Harden Done.

## Objective (when scheduled)

StatRow sparks / element pips from catalog `gauge` policy.

## Fields (when built)

| Field | Type | Notes |
|---|---|---|
| `gauge` | catalog enum | spark policy |
| `paint` | hex | from packs |
| `values` | number[] | spark series |

## Draft

`docs/design/gui-lego/pieces/` — author spark/pip piece before factory if scheduled.

## Success criteria (when scheduled)

- [ ] Catalog `gauge` drives chrome when present.
- [ ] Uses paint hex from packs; buy sparkline lib if needed (locked tech-stack).
- [ ] Uncapped GameUnits never paint CAP on spark.

## Commands

```powershell
# Only when D7 scheduled
cd web\fusion-rpg-web; npm test -- --run channel-row
```

## Boundaries

- **Ask first:** inventing a new gauge grammar outside kit/recharts/sparkline.
- **Never:** treat missing gauge as blocking cook truth.
