# Piece: `channel-row`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Row  
**Draft:** [../../design/gui-lego/pieces/channel-row.html](../../design/gui-lego/pieces/channel-row.html)  
**Shared types:** [payload-types.md](payload-types.md) · **Composition:** [spec-composition.md](spec-composition.md)

## Role

One derived channel row — six render states on `state`.

## Structure

| | |
|---|---|
| Landmark / root | `button.row` |
| Slots | _none_ |
| CSS `>` parents | family-block rows |

**Ban:** illicit wrappers between a `>` parent and its declared child.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"channel-row"` | yes | Registry id |
| `instanceId` | `string` | yes | Stable mount / testid |
| `phase` | `Phase` | yes | See payload-types |
| `channelId` | `string` | yes | Registry channel id |
| `title` | `string` | yes | Catalog displayName |
| `reading` | `string` | yes | Short reading line |
| `valueRaw` | `number or string` | when ready | MagnitudeDisplay.valueRaw |
| `valueText` | `string` | when ready | Formatted by fold |
| `formatterId` | `string` | when ready | GG-46 formatter |
| `state` | `DerivedRenderState` | yes | Six states only |
| `selected` | `boolean` | yes | From selectedChannelId |
| `themeRef` | `ThemeRef` | no | From expand axis |
| `glyphRef` | `GlyphRef` | no | CatalogIcon / hudToken |

`state` ∈ `active|default|capped|stub|no-producer|unregistered` — do not invent a seventh.

## Theme slots

- Pack kind(s): `themeRef`
- Reads: `--piece-accent`, paint for glyph tint
- Vfx keys: `select`

## Data flow

- **Bind:** `families[].rows[]`
- **Bus out:** `derived.channel.select`

## Focus (GG-19)

yes

## Motion (GG-31/32)

vfx.select; reduced-motion instant

## Empty / error

phase-pending for Pending fields — never invent magnitude

## Sample payloads

```json
{
  "piece": "channel-row",
  "instanceId": "row:combat.power.fire",
  "phase": "ready",
  "channelId": "combat.power.fire",
  "title": "Power",
  "reading": "Fire power",
  "valueRaw": 2847,
  "valueText": "2,847",
  "formatterId": "whole",
  "state": "active",
  "selected": true,
  "themeRef": {
    "kind": "element",
    "id": "fire"
  },
  "glyphRef": {
    "catalogIcon": "flame"
  }
}
```

```json
{
  "piece": "channel-row",
  "instanceId": "row:combat.power.ice",
  "phase": "ready",
  "channelId": "combat.power.ice",
  "title": "Power",
  "reading": "Ice power",
  "valueRaw": 1920,
  "valueText": "1,920",
  "formatterId": "whole",
  "state": "default",
  "selected": false,
  "themeRef": {
    "kind": "element",
    "id": "ice"
  },
  "glyphRef": {
    "catalogIcon": "snowflake"
  }
}
```
