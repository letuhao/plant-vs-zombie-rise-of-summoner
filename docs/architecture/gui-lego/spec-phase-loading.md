# Piece: `phase-loading`

**Program:** `gui-lego` · **Kind:** lifecycle · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/phase-loading.html](../../design/gui-lego/pieces/phase-loading.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Lifecycle frame — Surface or pane loading.

## Structure

| | |
|---|---|
| Landmark / root | `.phase / .phase-loading` |
| Slots | _none_ |
| CSS `>` parents | overlay on surface or pane |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"phase-loading"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `message` | `string` | yes | Player copy |



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
  "piece": "phase-loading",
  "instanceId": "demo:phase-loading",
  "phase": "loading",
  "message": "Surface or pane loading."
}
```
