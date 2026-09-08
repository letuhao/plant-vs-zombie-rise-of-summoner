# Piece: `tool-search`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/tool-search.html](../../design/gui-lego/pieces/tool-search.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Search query field for channel filter.

## Structure

| | |
|---|---|
| Landmark / root | `input.search` |
| Slots | _none_ |
| CSS `>` parents | surface-shell tools slot |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"tool-search"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `query` | `string` | yes | Current query |
| `placeholder` | `string` | yes | Localized placeholder |



## Theme slots

- Pack kind(s): `neutral`
- Reads: border/focus sun
- Vfx keys: none

## Data flow

- **Bind:** `vm.search`
- **Bus out:** `derived.search.set`

## Focus (GG-19)

yes — tab stop; often first stop in tools

## Motion (GG-31/32)

none

## Empty / error

Always mountable with empty query

## Sample payloads

```json
{
  "piece": "tool-search",
  "instanceId": "tool:search",
  "phase": "ready",
  "query": "",
  "placeholder": "Search channels"
}
```
