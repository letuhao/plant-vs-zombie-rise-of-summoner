# Piece: `surface-foot`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/surface-foot.html](../../design/gui-lego/pieces/surface-foot.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Footer: hidden-count and deferred notes.

## Structure

| | |
|---|---|
| Landmark / root | `footer.foot` |
| Slots | _none_ |
| CSS `>` parents | direct child of surface-shell foot slot |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"surface-foot"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `hiddenCount` | `number` | yes | Channels hidden by filter |
| `note` | `string` | yes | Player-facing sentence |



## Theme slots

- Pack kind(s): `neutral`
- Reads: muted text
- Vfx keys: none

## Data flow

- **Bind:** `vm.foot`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Omit foot or empty note when nothing hidden

## Sample payloads

```json
{
  "piece": "surface-foot",
  "instanceId": "foot:derived",
  "phase": "ready",
  "hiddenCount": 4,
  "note": "4 default channels hidden"
}
```
