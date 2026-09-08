# Piece: `surface-shell`

**Program:** `gui-lego` · **Kind:** layout · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/surface-shell.html](../../design/gui-lego/pieces/surface-shell.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Outer frame and slot host for the Derived console (identity, tools, rails, main, foot).

## Structure

| | |
|---|---|
| Landmark / root | `.console / .console-mini (draft)` |
| Slots | `identity` · `tools` · `railPrimary` · `railVariant` · `main` · `foot` |
| CSS `>` parents | `.console > .inspect-split` (via main → split-inspect); no wrapper under `.console` |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"surface-shell"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `phase` | `Phase` | yes | Surface-level readiness |



## Theme slots

- Pack kind(s): `neutral`
- Reads: chrome tokens only
- Vfx keys: none

## Data flow

- **Bind:** `vm`
- **Bus out:** _none_

## Focus (GG-19)

Initial focus may move into `tools` (search) per host policy

## Motion (GG-31/32)

none

## Empty / error

When `phase` is loading/error, host overlays `phase-*` instead of inventing children

## Sample payloads

```json
{
  "piece": "surface-shell",
  "instanceId": "shell:derived",
  "phase": "ready"
}
```
