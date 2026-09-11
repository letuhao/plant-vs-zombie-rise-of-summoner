# Piece: `cap-note`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/cap-note.html](../../design/gui-lego/pieces/cap-note.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Cap sentence only when a cap exists; otherwise explicit none or omit.

## Structure

| | |
|---|---|
| Landmark / root | `.cap-note` |
| Slots | _none_ |
| CSS `>` parents | inspect-pane cap |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"cap-note"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `capKind` | `"none" | "soft" | "hard" | string` | yes | Never invent a cap |
| `text` | `string` | yes | Player sentence |



## Theme slots

- Pack kind(s): `neutral`
- Reads: sun accent for note
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect.cap`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

Omit piece when fold sets absent

## Sample payloads

```json
{
  "piece": "cap-note",
  "instanceId": "inspect:cap",
  "phase": "ready",
  "capKind": "none",
  "text": "No cap on this channel."
}
```
