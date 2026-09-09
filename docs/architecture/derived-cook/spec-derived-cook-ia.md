# Module: `derived-cook-ia`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Entrypoints:** `DerivedSurfaceCook` · `data/tuning` derived-surface / family catalog · FE expand helpers

---

## Objective

Cook information architecture is **owned by BE + catalog**, not FE constants. Close Status L2b
(category) reachability and first-class OTHER Shared variant.

## Requirements

1. **Status dense categories** (`dot` / `cc` / `contagion` and omni) must be selectable so
   `status.resist.dot` (etc.) join into the rail — design CAP story.
2. **OTHER Shared** chip for `expand:"none"` families is emitted by cook DTO (or catalog), not
   `OTHER_SHARED_VARIANT_ID` invented only in FE.
3. Delete production dependence on FE `STATUS_CATEGORY_VARIANTS` / `ACTION_CATEGORY_VARIANTS`
   hardcodes — expand from cook variants / catalogs.
4. Preserve Guard: cook primary tabs remain Elements / Status / Resources / Other — never sheetGroups
   as primary.

## Success criteria

- [ ] BE cook documents Status variant set including L2b categories **or** dual expand mode accepted in catalog.
- [ ] OTHER Shared appears in cook JSON; FE only selects it.
- [ ] Grep: no production use of hardcoded action/status variant arrays in expand path (tests may keep fixtures).
- [ ] Regression: expand:none families do **not** duplicate under Attack/Defense chips.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/catalogs/derived-surface | ConvertFrom-Json
cd web\fusion-rpg-web; npm test -- --run foldDerivedSurfaceVm
```

## Boundaries

- **Always:** one cook SSOT.
- **Ask first:** renaming cook primary tabs; adding a fifth primary tab.
- **Never:** FE-only IA chips that disagree with cook JSON.
