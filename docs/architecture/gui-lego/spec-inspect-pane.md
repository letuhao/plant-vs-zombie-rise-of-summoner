# Piece: `inspect-pane`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Panel-slice  
**Draft:** [../../design/gui-lego/pieces/inspect-pane.html](../../design/gui-lego/pieces/inspect-pane.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

Assembles hero, meta, cap, gauges, sources for the selected channel.

## Structure

| | |
|---|---|
| Landmark / root | `aside.inspect` |
| Slots | `hero` · `meta` · `cap` · `gauges` · `sources` |
| CSS `>` parents | `.inspect-split > .inspect` |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"inspect-pane"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `channelId` | `string` | when ready | Selected channel |
| `themeRef` | `ThemeRef` | no | Matches row theme |



## Theme slots

- Pack kind(s): `themeRef`
- Reads: accent on hero/gauges
- Vfx keys: none

## Data flow

- **Bind:** `vm.inspect`
- **Bus out:** _none_

## Focus (GG-19)

contains focusables

## Motion (GG-31/32)

none

## Empty / error

phase-empty when no selection; phase-pending for Pending

## Sample payloads

```json
{
  "piece": "inspect-pane",
  "instanceId": "inspect:derived",
  "phase": "ready",
  "channelId": "combat.power.fire",
  "themeRef": {
    "kind": "element",
    "id": "fire"
  }
}
```

```json
{
  "piece": "inspect-pane",
  "instanceId": "inspect:derived",
  "phase": "ready",
  "channelId": "combat.power.ice",
  "themeRef": {
    "kind": "element",
    "id": "ice"
  }
}
```
