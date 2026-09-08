# Piece: `rail-primary`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/rail-primary.html](../../design/gui-lego/pieces/rail-primary.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Cook primary tablist: Elements · Status · Resources · Other.

## Structure

| | |
|---|---|
| Landmark / root | `nav.cat-bar` |
| Slots | `chips` (chip[]) |
| CSS `>` parents | surface-shell railPrimary |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"rail-primary"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `selectedId` | `string` | yes | Active cook tab id |
| `chips` | `ChipPayload[]` | yes | Built by fold; rendered as chip pieces |



## Theme slots

- Pack kind(s): `cook-tab` / `neutral`
- Reads: selected chip accent
- Vfx keys: none

## Data flow

- **Bind:** `vm.primaryRail`
- **Bus out:** `derived.tab.set` (via chip)

## Focus (GG-19)

roving tabindex within tablist

## Motion (GG-31/32)

none

## Empty / error

phase-error if cook missing

## Sample payloads

```json
{
  "piece": "rail-primary",
  "instanceId": "rail:primary",
  "phase": "ready",
  "selectedId": "elements",
  "chips": [
    {
      "id": "elements",
      "label": "Elements",
      "selected": true
    }
  ]
}
```
