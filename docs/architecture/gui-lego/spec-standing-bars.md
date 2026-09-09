# Piece: `standing-bars`

**Program:** `gui-lego` · **Kind:** gauge  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/standing-bars.html` — **must author before factory done**

---

## Role

Precision path for Standing: label + track + **absolute int** per axis.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"standing-bars"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `axes` | `{ id, label, value, valueText, fillPct, paint }[]` | yes | paint hex |
| `revision` | `number` | yes | |

## Success criteria

- [ ] Draft HTML exists.
- [ ] Large ints (e.g. 523999) don’t force page horizontal scroll (layout owns column).
- [ ] fillPct relative; valueText absolute `long`-backed ints.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/standing-bars.html
cd web\fusion-rpg-web; npm test -- --run standing-bars
```

## Boundaries

- **Never:** use radar alone for absolute comparison.
