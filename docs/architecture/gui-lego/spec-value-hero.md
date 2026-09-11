# Piece: `value-hero`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Token→display  
**Draft:** [../../design/gui-lego/pieces/value-hero.html](../../design/gui-lego/pieces/value-hero.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Big magnitude for inspect — displays valueText from VM.

## Structure

| | |
|---|---|
| Landmark / root | `.big (+ .reading)` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane hero |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"value-hero"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `title` | `string` | yes | Channel title |
| `reading` | `string` | yes | Reading line |
| `valueRaw` | `number or string` | when ready |  |
| `valueText` | `string` | when ready |  |
| `formatterId` | `string` | when ready |  |
| `themeRef` | `ThemeRef` | no |  |



## Theme slots

- Pack kind(s): `themeRef`
- Reads: `--piece-accent` on .big
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.hero`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

phase-pending — do not show 0 as invented

## Sample payloads

```json
{
  "piece": "value-hero",
  "instanceId": "inspect:hero",
  "phase": "ready",
  "title": "Power",
  "reading": "Fire power",
  "valueRaw": 2847,
  "valueText": "2,847",
  "formatterId": "whole",
  "themeRef": {
    "kind": "element",
    "id": "fire"
  }
}
```

```json
{
  "piece": "value-hero",
  "instanceId": "inspect:hero",
  "phase": "ready",
  "title": "Power",
  "reading": "Ice power",
  "valueRaw": 1920,
  "valueText": "1,920",
  "formatterId": "whole",
  "themeRef": {
    "kind": "element",
    "id": "ice"
  }
}
```
