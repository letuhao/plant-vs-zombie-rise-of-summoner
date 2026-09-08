# Piece: `phase-error`

**Program:** `gui-lego` · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-error.html](../../design/gui-lego/pieces/phase-error.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Lifecycle frame — Sheet/cook unavailable.

## Structure

| | |
|---|---|
| Landmark / root | `.phase / .phase-error` |
| Slots | _none_ |
| CSS `>` parents | overlay on surface or pane |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"phase-error"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `message` | `string` | yes |  |
| `retryLabel` | `string` | yes | Button label |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.phase or field.phase`
- **Bus out:** `derived.retry`

## Focus (GG-19)

retry control when phase-error

## Motion (GG-31/32)

none

## Empty / error

n/a — this piece IS the empty/error frame

## Sample payloads

```json
{
  "piece": "phase-error",
  "instanceId": "demo:phase-error",
  "phase": "error",
  "message": "Sheet/cook unavailable.",
  "retryLabel": "Retry"
}
```
