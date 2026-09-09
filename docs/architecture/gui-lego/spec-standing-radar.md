# Piece: `standing-radar`

**Program:** `gui-lego` · **Kind:** gauge  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/standing-radar.html` — **must author before factory done**  
**Library:** **recharts** RadarChart (Q4) — code-split  
**Layout:** [../condition-glance/spec-condition-layout.md](../condition-glance/spec-condition-layout.md)

---

## Role

Five-axis Standing glance. Absolute ints on `standing-bars`. Radar uses **recharts**; vertex
markers on scaled values; paint hex (no `fill="var(--x)"` alone).

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"standing-radar"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `axes` | `{ id, label, value, fillPct, paint }[]` | yes | five; paint = hex from fold (constants → packs later) |
| `revision` | `number` | yes | |

## Success criteria

- [ ] Draft HTML exists.
- [ ] recharts mount; tip markers on scaled radius (not stuck at full radius) — golden.
- [ ] Container width cooperates with condition-layout (≥ chart).
- [ ] Lazy import — not sanctum entry.
- [ ] Paint source documented in fold (hex literals OK until theme packs for Standing).

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/standing-radar.html
cd web\fusion-rpg-web; npm test -- --run standing-radar
```

## Boundaries

- **Never:** hand-SVG as long-term SSOT after this module; bars remain precision path.
