# Module: `derived-fold-harden`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Amends:** [../gui-lego/spec-derived-surface-vm.md](../gui-lego/spec-derived-surface-vm.md)  
**Locks:** **D2** · **D3** · **D4** · **D5**  
**Code:** `foldDerivedSurfaceVm.ts`, `formatDerivedMagnitude.ts`, `derivedCook.ts`

---

## Objective

Align the fold with the VM contract and kill defective joins: private paint maps, LadderIndex miss,
themeRegistry inject, Shared invent, Show-unchanged policy.

## Entrypoints

| File | Change |
|---|---|
| `foldDerivedSurfaceVm.ts` | Input delta; wire states/caps; D4 filter; remove PAINT_BUCKET; catalog glyphs |
| `formatDerivedMagnitude.ts` | Add `LadderIndex` → correct formatter |
| `derivedCook.ts` | Consume cook variants; delete KNOWN_CAPS / variant hardcodes (owned with sibling modules) |

## Input / output delta vs shipped

| Shipped | Target |
|---|---|
| Singleton `resolveTheme` | Inject `themeRegistry` (**D5**) |
| FE Shared prepend | BE Shared only (**D3**) |
| FE CAP / state heuristics | Wire fields (**D2**) + render-states |
| Hide default+no-producer | Hide default only (**D4**) |
| LadderIndex → gameUnits | Ledger map |
| `Join: ${channelId}` / `Sources (GG-49)` | Fiction via `derived-player-copy` |
| `statusIdToL2b` / `statusGlyph` hardcode | Catalog `category` / `hudToken` / `icon` |

## Success criteria

- [ ] Fold signature matches amended surface-vm (themeRegistry required or documented inject).
- [ ] LadderIndex golden test green.
- [ ] No private hex tables for elements/buckets in fold after paint wire.
- [ ] OTHER Shared not invented when absent from cook.
- [ ] Contract: no GG-49 / Join channelId on player band.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run foldDerivedSurfaceVm formatDerivedMagnitude
rg -n "PAINT_BUCKET|GG-49|KNOWN_CAPS|statusIdToL2b" web/fusion-rpg-web/src/features/gui-lego
```

## Testing

- Parity: FE recompute vs wire `renderState` goldens only.
- Regression: expand:none not under Attack chips.

## Boundaries

- **Always:** pure fold; no React; no fetch.
- **Never:** second cook join inside pieces.
