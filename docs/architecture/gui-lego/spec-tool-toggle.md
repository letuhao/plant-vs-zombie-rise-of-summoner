# Piece: `tool-toggle`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/tool-toggle.html](../../design/gui-lego/pieces/tool-toggle.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Boolean tool — show unchanged channels.

## Structure

| | |
|---|---|
| Landmark / root | `label.toggle` |
| Slots | _none_ |
| CSS `>` parents | surface-shell tools slot |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"tool-toggle"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `value` | `boolean` | yes | On/off |
| `label` | `string` | yes | Adjacent label |



## Theme slots

- Pack kind(s): `neutral`
- Reads: knob uses lawn-hot when on
- Vfx keys: none

## Data flow

- **Bind:** `vm.showUnchanged`
- **Bus out:** `derived.showUnchanged.set`

## Focus (GG-19)

yes

## Motion (GG-31/32)

knob 160ms; prefers-reduced-motion → instant

## Empty / error

n/a

## Sample payloads

```json
{
  "piece": "tool-toggle",
  "instanceId": "tool:showUnchanged",
  "phase": "ready",
  "value": false,
  "label": "Show unchanged"
}
```
