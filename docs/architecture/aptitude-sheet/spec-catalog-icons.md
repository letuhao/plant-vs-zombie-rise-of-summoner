# Spec: `catalog-icons`

**Program:** `aptitude-sheet` · **Map:** [../aptitude-sheet-map.md](../aptitude-sheet-map.md)  
**Ideal:** catalog `icon` field · buy-before-build lucide  
**Tunables SSOT:** runtime catalog sibling — [../tunables-ssot.md](../tunables-ssot.md) T7/T8

---

## Objective

Every aptitude row in `aptitude-catalog.v{n}.json` carries an `icon` glyph key consumable by
`aptitude-tile` via existing CatalogIcon / lucide path — no private SVG registry.

---

## Commands

```powershell
# Schema / catalog load tests after bump
dotnet test tests/FusionRpg.Core.Tests --filter AptitudeCatalog
npm test -- --run aptitude
```

---

## Project structure

| Path | Duty |
|---|---|
| `data/tuning/aptitude-catalog.v{n}.json` | Add `icon` per aptitude; bump version if loader requires |
| Catalog loader / contracts | Accept `icon` string |
| FE catalog consume | Tile reads `icon` |

---

## Contract

| Field | Type | Notes |
|---|---|---|
| `icon` | string | Lucide / catalog glyph id already used elsewhere in FE |

Missing icon in content → tile shows empty glyph slot or named fallback **fiction**, never crash.
Exact lucide key per aptitude is content — implementer picks coherent set; owner may retune.

---

## Boundaries

- **Always:** Icons in catalog, not hardcoded in TSX maps as SSOT.
- **Never:** Hand-rolled SVG icon pack as primary path.

---

## Success criteria

- [ ] All twelve aptitudes have non-empty `icon` in shipped catalog.
- [ ] Tile factory renders glyph from catalog join.
