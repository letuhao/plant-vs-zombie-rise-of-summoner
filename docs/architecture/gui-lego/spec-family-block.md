# Piece: `family-block`

**Program:** `gui-lego` · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/family-block.html](../../design/gui-lego/pieces/family-block.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Section header + rows. Header is owned inline (not a separate piece).

## Structure

| | |
|---|---|
| Landmark / root | `.family-block (.family-hd + rows)` |
| Slots | `rows` (channel-row[]) — header is props, not a child piece |
| CSS `>` parents | family-list blocks |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"family-block"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `familyId` | `string` | yes | Stable family key |
| `title` | `string` | yes | Section title |
| `rowCount` | `number` | yes | Visible rows in block |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.families[]`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Skip empty families in fold

## Sample payloads

```json
{
  "piece": "family-block",
  "instanceId": "family:power",
  "phase": "ready",
  "familyId": "power",
  "title": "Power",
  "rowCount": 3
}
```
