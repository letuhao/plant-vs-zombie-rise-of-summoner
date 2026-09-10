# Piece: `shield-stack-bar`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Card / gauge  
**Map:** [../shield-sheet-map.md](../shield-sheet-map.md)  
**Draft:** `docs/design/gui-lego/pieces/shield-stack-bar.html` — **must author before factory done**  
**Design SSOT:** [../../design/spec-shield-and-elements.md](../../design/spec-shield-and-elements.md) §3.1  
**Depends on:** theme-bind, element-paint-ssot, `shield-stack-projection`  
**Locks:** **S1** · **D8** regen optional

---

## Role

**One** segmented bar for up to three shield layers in **drain order** (left→right = deplete order).
Primary Shield tab stack chrome. **Bans** three permanent “Empty layer” radials as product.

## Structure

| | |
|---|---|
| Landmark / root | `.shield-stack-bar` |
| Segments | `.shield-segment` × N (0–3) |
| Empty slots | dashed wells only when Hot and N &lt; 3 |
| Pending | lifecycle piece — not fake empties |

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"shield-stack-bar"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | ready / pending / … |
| `segments` | array | yes | see segment fields |
| `emptySlots` | `0..3` | yes | Hot only |
| `themeRef` | | per segment | |

### Segment

| Field | Notes |
|---|---|
| `shieldId` | |
| `elementId` / themeRef | typed paint; untyped hatch |
| `current` / `max` | long; width ∝ max; fill ∝ ratio |
| `currentText` / `maxText` | fold-formatted |
| `priorityLabel` | aura / skill / innate fiction |
| `regenText` | optional — only if DTO has regen (**D8**) |
| `broken` | empty fill, slot remains |

## Success criteria

- [ ] Draft HTML exists.
- [ ] Order matches GetShields drain order.
- [ ] Element paint from paint SSOT.
- [ ] Pending vs Hot-empty vs N segments distinguished — no three Empty layer labels as “done.”

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
