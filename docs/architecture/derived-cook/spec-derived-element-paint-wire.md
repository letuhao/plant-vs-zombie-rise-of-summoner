# Module: `derived-element-paint-wire`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** [../gui-lego/spec-element-paint-ssot.md](../gui-lego/spec-element-paint-ssot.md)  
**Code:** `chrome.tsx` chip, `derivedConsole.css`

---

## Objective

Derived element chips and gauge accents consume **`resolveElementPaint` / themeResolved** only.
Remove `data-el` → `var(--el-*)` as a competing paint SSOT.

## Success criteria

- [ ] Chip factory sets theme/paint from element-paint-ssot (no mute grey for dark).
- [ ] `derivedConsole.css` element color rules via data-el deleted or reduced to structural only.
- [ ] Grep clean for private element color maps in Derived path.
- [ ] Shares same SSOT as Condition `element-badge` (condition-glance Wave 1).

## Commands

```powershell
rg -n "data-el|--el-fire|BUCKET_COLORS" web/fusion-rpg-web/src/ui/gui-lego web/fusion-rpg-web/src/features/gui-lego
cd web\fusion-rpg-web; npm test -- --run DerivedTab
```

## Boundaries

- **Never:** fork a Derived-only color table.
