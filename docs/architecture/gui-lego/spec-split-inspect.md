# Piece: `split-inspect`

**Program:** `gui-lego` · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/split-inspect.html](../../design/gui-lego/pieces/split-inspect.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Dock | inspect layout. Fragility: direct children only.

## Structure

| | |
|---|---|
| Landmark / root | `.inspect-split` |
| Slots | `dock` · `inspect` |
| CSS `>` parents | `.console > .inspect-split`; `.inspect-split > .dock`; `.inspect-split > .inspect` |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"split-inspect"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `phase` | `Phase` | yes | Usually ready when surface ready |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm`
- **Bus out:** _none_

## Focus (GG-19)

no (children hold focus)

## Motion (GG-31/32)

none

## Empty / error

Dock/inspect may each show phase-empty

## Sample payloads

```json
{
  "piece": "split-inspect",
  "instanceId": "split:derived",
  "phase": "ready"
}
```
