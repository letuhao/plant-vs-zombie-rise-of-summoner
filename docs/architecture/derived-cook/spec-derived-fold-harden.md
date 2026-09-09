# Module: `derived-fold-harden`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Amends:** [../gui-lego/spec-derived-surface-vm.md](../gui-lego/spec-derived-surface-vm.md)  
**Code:** `foldDerivedSurfaceVm.ts`, `formatDerivedMagnitude.ts`

---

## Objective

Align the fold with the VM contract and kill remaining defective joins: private paint maps,
LadderIndex miss, themeRegistry input, identity/foot/phase rules.

## Requirements

1. Accept `themeRegistry` (or pure resolve inject) per surface-vm §2/§7 — stop undocumented singleton-only path **or** document singleton as intentional in amended VM.
2. Format `LadderIndex` via `formatMagnitude` / UnitClass ledger — not gameUnits fallthrough.
3. Remove private `PAINT_BUCKET` / CSS-var bucket maps once theme packs cover contribution buckets (or map buckets → pack ids).
4. Status category / glyph from **catalog** fields after `derived-theme-packs` — delete fold hardcode maps.
5. Keep OTHER Shared vs action-category split (no duplicate expand:none under Attack).

## Success criteria

- [ ] `spec-derived-surface-vm` amended to match shipped fold inputs/outputs.
- [ ] LadderIndex golden test.
- [ ] No private hex tables for elements/buckets in fold after paint wire.

## Commands

```powershell
cd web\fusion-rpg-web; npm test -- --run foldDerivedSurfaceVm formatDerivedMagnitude
```

## Boundaries

- **Always:** pure fold; no React; no fetch.
- **Never:** second cook join inside pieces.
