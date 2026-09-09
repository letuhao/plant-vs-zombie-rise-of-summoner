# Piece: `pool-meter`

**Program:** `gui-lego` · **Kind:** gauge  
**Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Draft:** [../../design/gui-lego/pieces/pool-meter.html](../../design/gui-lego/pieces/pool-meter.html)  
**Depends on:** [spec-theme-bind.md](spec-theme-bind.md)

---

## Role

One resource pool track (label, **CatalogIcon**, current/max, animated fill). Consumes resource
`themeResolved` + `vfxClass`.

## Fields

| Field | Type | Required | Notes |
|---|---|---|---|
| `piece` | `"pool-meter"` | yes | |
| `instanceId` | `string` | yes | |
| `phase` | `Phase` | yes | |
| `poolId` | `string` | yes | |
| `label` | `string` | yes | |
| `current` / `max` | `long` wire | when ready | |
| `fillPct` | `number` | yes | |
| `valueText` | `string` | yes | |
| `icon` | `string` | no | **Must render CatalogIcon when set** |
| `themeRef` / `themeResolved` | | yes | resource pack |
| `selected` | `boolean` | no | |
| `revision` | `number` | yes | |

## Success criteria

- [ ] CatalogIcon always visible when `icon` is set (ideal wiring gap closed).
- [ ] themeResolved accent + `vfx.select` when pack defines.
- [ ] No pending text when Hub max > 0.
- [ ] Animates on `revision`.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run pool-meter
```

## Boundaries

- **Never:** ignore folded themeRef/icon; private `[data-pool]` colors that fight theme packs (packs win).
