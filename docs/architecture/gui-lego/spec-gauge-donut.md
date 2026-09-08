# Piece: `gauge-donut`

**Program:** `gui-lego` · **Kind:** gauge · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/gauge-donut.html](../../design/gui-lego/pieces/gauge-donut.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Contribution share donut — SVG strokes use theme **paint** hex, never var() in fill/stroke.

## Structure

| | |
|---|---|
| Landmark / root | `.share-donut` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane gauges |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"gauge-donut"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `segments` | `{ id, label, sharePm, paintKey }[]` | yes | sharePm is per-mille; paintKey accent|accentMuted |
| `themeRef` | `ThemeRef` | yes | Resolves paint |



## Theme slots

- Pack kind(s): `themeRef`
- Reads: `paint.accent`, `paint.accentMuted`
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.donut`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Hide when no contributions

## Sample payloads

```json
{
  "piece": "gauge-donut",
  "instanceId": "inspect:donut",
  "phase": "ready",
  "themeRef": {
    "kind": "element",
    "id": "fire"
  },
  "segments": [
    {
      "id": "base",
      "label": "Base",
      "sharePm": 620,
      "paintKey": "accent"
    },
    {
      "id": "gear",
      "label": "Gear",
      "sharePm": 380,
      "paintKey": "accentMuted"
    }
  ]
}
```

```json
{
  "piece": "gauge-donut",
  "instanceId": "inspect:donut",
  "phase": "ready",
  "themeRef": {
    "kind": "element",
    "id": "ice"
  },
  "segments": [
    {
      "id": "base",
      "label": "Base",
      "sharePm": 700,
      "paintKey": "accent"
    },
    {
      "id": "gear",
      "label": "Gear",
      "sharePm": 300,
      "paintKey": "accentMuted"
    }
  ]
}
```
