# Piece: `family-list`

**Program:** `gui-lego` · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/family-list.html](../../design/gui-lego/pieces/family-list.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Ordered list of family-block children.

## Structure

| | |
|---|---|
| Landmark / root | `.list-pane` |
| Slots | `blocks` (family-block[]) |
| CSS `>` parents | scroll-region content |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"family-list"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `count` | `number` | yes | Visible family count |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.families`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

When zero families, parent shows phase-empty

## Sample payloads

```json
{
  "piece": "family-list",
  "instanceId": "families:derived",
  "phase": "ready",
  "count": 2
}
```
