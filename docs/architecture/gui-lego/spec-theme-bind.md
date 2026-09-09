# Module: `theme-bind`

**Program:** `gui-lego` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Amends:** `bindSurface.ts`, [payload-types.md](payload-types.md), Condition factories  
**Depends on:** [spec-theme-packs.md](spec-theme-packs.md), [spec-element-paint-ssot.md](spec-element-paint-ssot.md)

---

## Objective

`bindSurface` resolves **all** theme refs on a payload — including `shieldThemeRef` and nested
array children — and piece factories **consume** `themeResolved` / `vfxClass` / paint hex. No
fire-default shield ring; no unused folded `themeRef`.

## Success criteria

- [ ] `bindSurface` resolves `themeRef` and `shieldThemeRef` (and nested array children) → `themeResolved` / `shieldThemeResolved`.
- [ ] Shared helper `vfxClass(themeResolved)` returns pack `vfx.select` class or null.
- [ ] `poolMeterFactory` / `poolRadialFactory` / `element-badge` apply css, paint.accent, and vfxClass when present.
- [ ] Shield ring color from `shieldThemeResolved.paint.accent` (or omit ring when no shield).
- [ ] Contract test: `shieldThemeRef: { kind: "element", id: "ice" }` → ice paint, not `--el-fire`.

## Nested resolve

`bindSurface` walks recipe-bound payloads recursively:

- Object fields `themeRef` / `shieldThemeRef`
- Array children (e.g. `meters[]`, `elements[]`, `axes[]` if themed)

```ts
themeRef?: ThemeRef;
shieldThemeRef?: ThemeRef;
themeResolved?: ThemeResolved;
shieldThemeResolved?: ThemeResolved;
// factory:
className={cx(root, vfxClass(payload.themeResolved))}
style={themeStyle(payload.themeResolved)}
```

Pieces **never** call `resolveTheme` themselves.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run bindSurface
npm test -- --run theme
```

## Testing

- Unit: bindSurface golden for themeRef + shieldThemeRef + meters[] themeRef.
- Factory unit: meter root has CSS vars; vfx class when `vfx.select` non-null.

## Boundaries

- **Always:** amend existing bindSurface; keep single bind path.
- **Never:** piece-local `resolveTheme`; hard-coded fire fallback when themeRef was provided.

## ActorHub

N/A — FE bind seam.
