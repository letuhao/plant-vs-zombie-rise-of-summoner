# Piece: `identity-hd`

**Program:** `gui-lego` · **Kind:** chrome · **ERM rung:** —  
**Draft:** [../../design/gui-lego/pieces/identity-hd.html](../../design/gui-lego/pieces/identity-hd.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Who / level / side / cook meta strip. Dual with ActorPanel header is an embed delta.

## Structure

| | |
|---|---|
| Landmark / root | `.identity (.who / .meta)` |
| Slots | _none_ |
| CSS `>` parents | surface-shell identity slot |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"identity-hd"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `who` | `string` | yes | Display name |
| `meta` | `string` | yes | Lv · side · cook path |
| `themeRef` | `ThemeRef` | no | Usually side |



## Theme slots

- Pack kind(s): `side` or `neutral`
- Reads: optional side tint
- Vfx keys: none

## Data flow

- **Bind:** `vm.identity`
- **Bus out:** _none_

## Focus (GG-19)

no

## Motion (GG-31/32)

none

## Empty / error

phase-pending if identity unknown

## Sample payloads

```json
{
  "piece": "identity-hd",
  "instanceId": "identity:derived",
  "phase": "ready",
  "who": "Emberling",
  "meta": "Lv 12 \u00b7 Plant \u00b7 Elements / Fire",
  "themeRef": {
    "kind": "side",
    "id": "plant"
  }
}
```
