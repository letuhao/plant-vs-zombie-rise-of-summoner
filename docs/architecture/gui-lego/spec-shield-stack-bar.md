# Piece: `shield-stack-bar`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Card / gauge  
**Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Draft:** `docs/design/gui-lego/pieces/shield-stack-bar.html` — **must author before factory done**  
**Design SSOT:** [../../design/spec-shield-and-elements.md](../../design/spec-shield-and-elements.md) §3.1  
**Depends on:** theme-bind, element-paint-ssot, `shield-stack-projection`

---

## Role

**One** segmented bar for up to three shield layers in **drain order** (left→right = deplete order).
Primary Shield tab stack chrome — supersedes three equal “Empty layer” radials as SSOT.

## Structure

| | |
|---|---|
| Landmark / root | `.shield-stack-bar` |
| Segments | `.shield-segment` × N (0–3) |
| Empty slots | dashed wells only when Hot and N &lt; 3 |
| Pending | lifecycle piece — not fake empties |

## Segment fields

| Field | Notes |
|---|---|
| `elementId` / themeRef | typed paint; untyped hatch |
| `current` / `max` | long; width ∝ max; fill ∝ current/max |
| `priorityLabel` | aura / skill / innate fiction |
| `regenText` | optional rate |
| `broken` | empty fill but slot remains |

## Success criteria

- [ ] Draft HTML exists.
- [ ] Order matches GetShields drain order.
- [ ] Element paint from paint SSOT; not mute grey.
- [ ] Pending vs Hot-empty distinguished.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/shield-stack-bar.html
cd web\fusion-rpg-web; npm test -- --run shield-stack
```

## Sample payload

```json
{
  "piece": "shield-stack-bar",
  "instanceId": "shield:stack",
  "phase": "ready",
  "segments": [
    {
      "shieldId": "aura-1",
      "elementId": "ice",
      "current": 40,
      "max": 40,
      "priorityLabel": "Aura",
      "themeRef": { "kind": "element", "id": "ice" }
    }
  ],
  "emptySlots": 2
}
```
