# Piece: `chip`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Chip  
**Draft:** [../../design/gui-lego/pieces/chip.html](../../design/gui-lego/pieces/chip.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

One selectable rail option (cook tab or variant).

## Structure

| | |
|---|---|
| Landmark / root | `button.chip` |
| Slots | _none_ |
| CSS `>` parents | rail chips slot |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"chip"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `id` | `string` | yes | Tab or variant id |
| `label` | `string` | yes | Player label |
| `selected` | `boolean` | yes | From surface selection SSOT |
| `count` | `number` | no | Optional badge count |
| `themeRef` | `ThemeRef` | no | Required for themed variants |



## Theme slots

- Pack kind(s): `themeRef` or neutral
- Reads: `--piece-accent`, `--piece-rail-edge`, `--piece-select-glow`
- Vfx keys: `select` when themed

## Data flow

- **Bind:** `rail.chips[]`
- **Bus out:** parent emits tab/variant set

## Focus (GG-19)

yes

## Motion (GG-31/32)

vfx.select when pack provides; reduced-motion instant

## Empty / error

n/a

## Sample payloads

```json
{
  "piece": "chip",
  "instanceId": "chip:variant:fire",
  "phase": "ready",
  "id": "fire",
  "label": "Fire",
  "selected": true,
  "count": 12,
  "themeRef": {
    "kind": "element",
    "id": "fire"
  }
}
```

```json
{
  "piece": "chip",
  "instanceId": "chip:variant:ice",
  "phase": "ready",
  "id": "ice",
  "label": "Ice",
  "selected": false,
  "count": 9,
  "themeRef": {
    "kind": "element",
    "id": "ice"
  }
}
```
