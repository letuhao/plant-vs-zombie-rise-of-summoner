# Piece: `element-badge`

**Program:** `gui-lego` · **Kind:** entity · **ERM rung:** Chip  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/element-badge.html` — **must author before factory done**  
**Depends on:** [spec-element-paint-ssot.md](spec-element-paint-ssot.md), [spec-theme-bind.md](spec-theme-bind.md)  
**Shared types:** [payload-types.md](payload-types.md)

---

## Role

Shared elemental identity badge (glyph + label + paint + optional pulse). First consumer:
Condition `actor-identity`. Later: Elements tab, HUD. Consumes **element-paint-ssot**.

## Structure

| | |
|---|---|
| Landmark / root | `span.element-badge` or `button.element-badge` |
| Slots | _none_ |
| CSS | paint SSOT vars + `data-el` |

**Ban:** plain muted `.chip` with element id text.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"element-badge"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `elementId` | `string` | yes | Catalog id |
| `label` | `string` | yes | Fiction name |
| `themeRef` | `ThemeRef` | yes | `{ kind: "element", id }` |
| `themeResolved` | `ThemeResolved` | bind | |
| `glyphRef` | `GlyphRef` | no | |
| `selectable` | `boolean` | no | Default false on Condition |

## Success criteria

- [ ] Draft HTML exists at path above.
- [ ] Dark/fire show non-muted accent from paint SSOT + `vfx.select` when pack defines.
- [ ] Condition identity mounts badges — zero mute chip twins.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/element-badge.html
cd web\fusion-rpg-web; npm test -- --run element-badge
```

## Empty

No elements → mount **zero** badges.

## Testing

Snapshot + contrast; reduced-motion skips pulse.

## Boundaries

- **Always:** paint SSOT.  
- **Never:** Condition-only hex.

## Sample payload

```json
{
  "piece": "element-badge",
  "instanceId": "condition:el:dark",
  "phase": "ready",
  "elementId": "dark",
  "label": "Dark",
  "themeRef": { "kind": "element", "id": "dark" }
}
```
