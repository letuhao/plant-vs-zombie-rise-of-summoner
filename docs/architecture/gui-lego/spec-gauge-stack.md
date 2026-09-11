# Piece: `gauge-stack`

**Program:** `gui-lego` · **Kind:** gauge · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/gauge-stack.html](../../design/gui-lego/pieces/gauge-stack.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Contribution stack bars — width from sharePm; color from theme css.

## Structure

| | |
|---|---|
| Landmark / root | `.stack / .stack-row` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane gauges |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"gauge-stack"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `rows` | `{ id, label, valueText, sharePm }[]` | yes |  |
| `themeRef` | `ThemeRef` | yes |  |



## Theme slots

- Pack kind(s): `themeRef`
- Reads: `--piece-accent` on bar fill
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.stack`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Hide when empty

## Sample payloads

```json
{
  "piece": "gauge-stack",
  "instanceId": "inspect:stack",
  "phase": "ready",
  "themeRef": {
    "kind": "element",
    "id": "fire"
  },
  "rows": [
    {
      "id": "base",
      "label": "Base",
      "valueText": "+1,764",
      "sharePm": 620
    },
    {
      "id": "gear",
      "label": "Gear",
      "valueText": "+1,083",
      "sharePm": 380
    }
  ]
}
```

```json
{
  "piece": "gauge-stack",
  "instanceId": "inspect:stack",
  "phase": "ready",
  "themeRef": {
    "kind": "element",
    "id": "ice"
  },
  "rows": [
    {
      "id": "base",
      "label": "Base",
      "valueText": "+1,344",
      "sharePm": 700
    },
    {
      "id": "gear",
      "label": "Gear",
      "valueText": "+576",
      "sharePm": 300
    }
  ]
}
```
