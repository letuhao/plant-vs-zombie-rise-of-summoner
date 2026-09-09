# Module: `derived-theme-packs`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Depends on:** [../gui-lego/spec-theme-packs.md](../gui-lego/spec-theme-packs.md)  
**Authoring:** `docs/design/gui-lego/themes/packs/`

---

## Objective

Author and register theme packs for **action-category** and **cook-tab** kinds so variant/primary
rails get real paint (ideal taxonomy). Stop fold hardcoding statusId→L2b and glyph maps — use
status-catalog `category` / `hudToken` / `icon`.

## Deliverables

| Pack kind | Examples |
|---|---|
| `action-category` | attack, defense, support, movement, status |
| `cook-tab` | elements, status, resources, other |
| Contribution bucket (optional) | aptitude, equip, tree, … → pack ids |

## Success criteria

- [ ] Pack JSON + FE registry entries exist.
- [ ] Fold `variantTheme` returns themeRef for OTHER/action chips.
- [ ] Status glyph/category from catalog — fold maps deleted.

## Commands

```powershell
Test-Path docs/design/gui-lego/themes/packs
rg -n "statusIdToL2b|statusGlyph" web/fusion-rpg-web/src/features/gui-lego
```

## Boundaries

- **Ask first:** new theme kind beyond gui-lego ideal taxonomy.
