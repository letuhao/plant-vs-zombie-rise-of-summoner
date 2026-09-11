# Piece: `meta-sentences`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/meta-sentences.html](../../design/gui-lego/pieces/meta-sentences.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Compose / unit / join explanatory lines under the hero.

## Structure

| | |
|---|---|
| Landmark / root | `.sentence` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane meta |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"meta-sentences"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `lines` | `string[]` | yes | Ordered sentences |



## Theme slots

- Pack kind(s): `neutral`
- Reads: muted
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.meta`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Empty array ok

## Sample payloads

```json
{
  "piece": "meta-sentences",
  "instanceId": "inspect:meta",
  "phase": "ready",
  "lines": [
    "Compose: Hub derived",
    "Unit: whole",
    "Join: channelId"
  ]
}
```
