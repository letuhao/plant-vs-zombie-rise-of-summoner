# Piece: `rail-variant`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/rail-variant.html](../../design/gui-lego/pieces/rail-variant.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Variant tablist under the cook tab (elements, status categories, …).

## Structure

| | |
|---|---|
| Landmark / root | `nav.cat-bar.variant-bar` |
| Slots | `chips` (chip[]) |
| CSS `>` parents | surface-shell railVariant |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"rail-variant"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `selectedId` | `string` | yes | Active variant id |
| `chips` | `ChipPayload[]` | yes | Each chip carries themeRef |



## Theme slots

- Pack kind(s): inferred per chip
- Reads: chip theme packs
- Vfx keys: none

## Data flow

- **Bind:** `vm.variantRail`
- **Bus out:** `derived.variant.set`

## Focus (GG-19)

roving tabindex

## Motion (GG-31/32)

none

## Empty / error

Hide rail when cook tab has no variants

## Sample payloads

```json
{
  "piece": "rail-variant",
  "instanceId": "rail:variant",
  "phase": "ready",
  "selectedId": "fire",
  "chips": [
    {
      "id": "fire",
      "label": "Fire",
      "selected": true,
      "themeRef": {
        "kind": "element",
        "id": "fire"
      }
    }
  ]
}
```
