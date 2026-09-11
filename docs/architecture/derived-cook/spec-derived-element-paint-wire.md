# Module: `derived-element-paint-wire`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** [../gui-lego/spec-element-paint-ssot.md](../gui-lego/spec-element-paint-ssot.md)  
**Wave gate:** May land parallel with Condition Wave 1 paint SSOT — **must consume same module**, never fork colors.  
**Code:** `chrome.tsx` chip, `derivedConsole.css`

---

## Objective

Derived element chips and gauge accents consume **`resolveElementPaint` / themeResolved** only.
Remove `data-el` → `var(--el-*)` as a competing paint SSOT.

## Fields (chip payload)

| Field | Type | Notes |
|---|---|---|
| `themeRef` | `{ kind: "element", id }` | required for element variants |
| `themeResolved` | paint + css + vfx | from theme-bind / paint SSOT |
| `label` | string | fiction |

## CSS delete list

| Path | Remove |
|---|---|
| `derivedConsole.css` | `data-el` → `--el-fire` etc. chip/pip color rules |
| `chrome.tsx` | setting `data-el` as paint SSOT (structural attrs OK if unused for color) |
| fold | `PAINT_BUCKET` private hex (after bucket packs or neutral) |

## Success criteria

- [ ] Chip factory uses element-paint-ssot / themeResolved (dark not mute grey).
- [ ] Grep clean for private element color maps in Derived path.
- [ ] Shares same SSOT as Condition `element-badge`.
- [ ] Assembled `derived-console.html` accents match packs (draft amend if needed).

## Commands

```powershell
rg -n "data-el|--el-fire|BUCKET_COLORS" web/fusion-rpg-web/src/ui/gui-lego web/fusion-rpg-web/src/features/gui-lego
cd web\fusion-rpg-web; npm test -- --run DerivedTab
```

## Boundaries

- **Never:** fork a Derived-only color table.
