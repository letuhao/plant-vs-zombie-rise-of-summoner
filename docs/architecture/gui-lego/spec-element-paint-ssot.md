# Module: `element-paint-ssot`

**Program:** `gui-lego` (shared) · **Consumers:** `condition-glance` (first ship) · `derived-cook` · `shield-sheet`  
**Map:** [../condition-glance-map.md](../condition-glance-map.md) · [../derived-cook-map.md](../derived-cook-map.md) · [../shield-sheet-map.md](../shield-sheet-map.md)  
**Depends on:** [spec-theme-packs.md](spec-theme-packs.md), `data/tuning/element-catalog.v{n}.json`  
**Ideal lock:** Q2 — centralize element UI colors for maximum reuse

---

## Objective

One **element paint SSOT** for every player UI that shows an element (Condition `element-badge`,
Derived rails, Elements tab, HUD later). Catalog names + theme pack `css` / `paint` / `vfx` resolve
through this module — never a private hex table in a tab or HUD file.

## Success criteria

- [ ] Single FE import path exports resolved paint + CSS vars + vfx class ids for every catalog element id.
- [ ] Condition `element-badge`, Derived `chip` (element variants), and future HUD **all** import this SSOT.
- [ ] Grep finds **zero** private element color maps in those consumers after wire.
- [ ] `actorHudDisplayTokens.ts` private element table deleted or redirected to this SSOT.
- [ ] Pack JSON under `docs/design/gui-lego/themes/packs/element-*.json` remains authoring SSOT; FE copy stays in sync.

### Consumer path list (must migrate)

| Consumer | Path |
|---|---|
| Condition identity | `web/.../pieces/condition.tsx` actor-identity / element-badge |
| Derived chip | `web/.../pieces/chrome.tsx` chipFactory — **`derived-element-paint-wire`** |
| Shield segments | `shield-stack-bar` / `shield-status` |
| HUD tokens | `web/.../actorHudDisplayTokens.ts` (redirect) |
| Resolve module | e.g. `web/.../features/gui-lego/themes/elementPaint.ts` |

## Structure

| Artifact | Role |
|---|---|
| Theme pack JSON | Authoring: css vars, paint hex, vfx.select/idle |
| Element catalog | Display name, glyph/hudToken, id |
| FE `resolveElementPaint(id)` | Runtime resolve |

## Code style (contract)

```ts
resolveElementPaint(elementId: string): {
  themeId: string;
  css: Record<string, string>;
  paint: { accent: string; accentMuted: string; onAccent: string };
  vfx: { select: string | null; idle: string | null };
  dataEl: string;
  label: string;
  glyphRef: GlyphRef;
}
```

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run elementPaint
rg -n "el-fire|#e7733f|elementColor|ELEMENT_COLOR" web/fusion-rpg-web/src/ui/actor web/fusion-rpg-web/src/ui/gui-lego/pieces/condition.tsx
```

## Testing

- Unit: every catalog id resolves; unknown → neutral fallback (documented).
- Guard/grep: no private element hex maps in Condition + Derived chip paths after migration.

## Boundaries

- **Always:** theme packs + catalog own colors; FE only resolves.
- **Ask first:** adding a second element theme system.
- **Never:** invent Condition-only colors; leave competing HUD private table after ship.

## Tunables

`data/tuning/element-catalog.v{n}.json` + `themes/packs/element-*.json`.

## ActorHub

N/A — presentation resolve only.
