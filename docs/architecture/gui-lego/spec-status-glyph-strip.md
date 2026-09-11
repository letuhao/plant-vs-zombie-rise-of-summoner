# Piece: `status-glyph-strip`

**Program:** `gui-lego` · **Kind:** Chip strip  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** `docs/design/gui-lego/pieces/status-glyph-strip.html` — **must author before factory done**  
**Depends on:** `StatusGlyph`, [../condition-glance/spec-sheet-hot-projection.md](../condition-glance/spec-sheet-hot-projection.md),
[../gui-lego/spec-condition-surface-vm.md](spec-condition-surface-vm.md) bus

---

## Role

Live status glyphs on Condition glance. Composes shared **`StatusGlyph`**.

## Omit rules (Q3)

If `items.length === 0` → **do not mount** (no title, no “no effects” paragraph).

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"status-glyph-strip"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `title` | `string` | yes when mounted | Fiction ([fiction-copy](../condition-glance/spec-fiction-copy.md)) |
| `items` | `{ id, hudToken, color, … }[]` | yes | Catalog join |
| `revision` | `number` | yes | |

## Bus (locked)

Glyph activate → emit **`condition.status.open`** `{ statusId }` → host opens Status tab.
Display-only without bus is **rejected**.

## Success criteria

- [ ] Draft HTML exists.
- [ ] Factory imports `StatusGlyph` — no homemade button twin.
- [ ] Cold / empty: no strip DOM.
- [ ] Bus event wired.

## Commands

```powershell
Test-Path docs/design/gui-lego/pieces/status-glyph-strip.html
cd web\fusion-rpg-web; npm test -- --run status-glyph-strip
rg -n "StatusGlyph" web/fusion-rpg-web/src/ui/gui-lego/pieces
```

## Boundaries

- **Never:** empty chrome; developer “tap for Status tab” title; reinvent glyphs.
