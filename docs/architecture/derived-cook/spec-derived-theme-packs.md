# Module: `derived-theme-packs`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** [../gui-lego/spec-theme-packs.md](../gui-lego/spec-theme-packs.md)  
**Authoring:** `docs/design/gui-lego/themes/packs/`

---

## Objective

Author and register theme packs for **action-category** and **cook-tab** kinds. Stop fold hardcoding
statusId→L2b and glyph maps — use status-catalog `category` / `hudToken` / `icon`.

## Pack paths (deliverables)

| Kind | Pack files (author under packs/) | FE registry |
|---|---|---|
| `action-category` | `action-category-attack.json` … movement, status, support, defense | `themeRegistry.ts` |
| `cook-tab` | `cook-tab-elements.json` … status, resources, other | same |
| Contribution bucket (optional) | `bucket-aptitude.json` … | optional Wave 2+ |

Schema: same as existing packs — `css` + `paint` hex + optional `vfx`.

## Catalog fields replacing fold maps

| Need | Source |
|---|---|
| Status L2b category | status-catalog `category` / cook statusCategoryVariants |
| Glyph | `hudToken` / `icon` — never fold RGB hash |

## Success criteria

- [ ] Pack JSON + FE registry entries exist for action-category + cook-tab.
- [ ] Fold `variantTheme` returns themeRef for OTHER/action chips.
- [ ] Fold `statusIdToL2b` / `statusGlyph` maps deleted.

## Commands

```powershell
Test-Path docs/design/gui-lego/themes/packs
rg -n "statusIdToL2b|statusGlyph" web/fusion-rpg-web/src/features/gui-lego
```

## Sample themeRef

```json
{ "kind": "action-category", "id": "attack" }
```

## Boundaries

- **Ask first:** new theme kind beyond gui-lego ideal taxonomy.
