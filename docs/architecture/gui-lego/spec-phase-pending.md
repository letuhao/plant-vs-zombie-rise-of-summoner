# Piece: `phase-pending`

**Program:** `gui-lego` · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-pending.html](../../design/gui-lego/pieces/phase-pending.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Lifecycle frame — A field is still Pending — reason visible.

## Structure

| | |
|---|---|
| Landmark / root | `.phase / .phase-pending` |
| Slots | _none_ |
| CSS `>` parents | overlay on surface or pane |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"phase-pending"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `message` | `string` | yes |  |
| `field` | `string` | no | Which field |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.phase or field.phase`
- **Bus out:** _none_

## Focus (GG-19)

retry control when phase-error

## Motion (GG-31/32)

none

## Empty / error

n/a — this piece IS the empty/error frame

## Sample payloads

```json
{
  "piece": "phase-pending",
  "instanceId": "demo:phase-pending",
  "phase": "pending",
  "message": "A field is still Pending \u2014 reason visible.",
  "field": "value"
}
```
