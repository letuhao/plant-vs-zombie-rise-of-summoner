# Piece: `source-list`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Row list  
**Draft:** [../../design/gui-lego/pieces/source-list.html](../../design/gui-lego/pieces/source-list.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

GG-49 attribution sources for the selected channel.

## Structure

| | |
|---|---|
| Landmark / root | `ul.sources` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane sources |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"source-list"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `items` | `{ sourceId, label, valueText }[]` | yes | sourceId is GG-49 grammar |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.sources`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Empty list ok

## Sample payloads

```json
{
  "piece": "source-list",
  "instanceId": "inspect:sources",
  "phase": "ready",
  "items": [
    {
      "sourceId": "gear:weapon",
      "label": "Weapon",
      "valueText": "+1,083"
    },
    {
      "sourceId": "base",
      "label": "Base",
      "valueText": "+1,764"
    }
  ]
}
```
