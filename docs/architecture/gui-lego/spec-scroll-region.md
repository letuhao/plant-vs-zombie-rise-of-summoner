# Piece: `scroll-region`

**Program:** `gui-lego` · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/scroll-region.html](../../design/gui-lego/pieces/scroll-region.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Declared overflow host for dock or inspect content.

## Structure

| | |
|---|---|
| Landmark / root | `.scroll-region` |
| Slots | `content` |
| CSS `>` parents | split-inspect dock or inspect |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"scroll-region"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `phase` | `Phase` | yes |  |



## Theme slots

- Pack kind(s): `neutral`
- Reads: none
- Vfx keys: none

## Data flow

- **Bind:** `vm.dockScroll | vm.inspectScroll`
- **Bus out:** _none_

## Focus (GG-19)

contains focusables

## Motion (GG-31/32)

none

## Empty / error

content slot may be phase-empty

## Sample payloads

```json
{
  "piece": "scroll-region",
  "instanceId": "scroll:dock",
  "phase": "ready"
}
```
