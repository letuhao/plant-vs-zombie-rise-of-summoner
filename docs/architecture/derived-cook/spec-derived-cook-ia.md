# Module: `derived-cook-ia`

**Program:** `derived-cook` · **Map:** [../derived-cook-map.md](../derived-cook-map.md)  
**Locks:** **D1** Status L2b · **D3** OTHER Shared from BE  
**Entrypoints:** `DerivedSurfaceCook` · `data/tuning/derived-stat-catalog.v{n}.json` · FE expand helpers

---

## Objective

Cook information architecture is **owned by BE + catalog**, not FE constants. Close Status L2b
(category) reachability and first-class OTHER Shared variant.

## Locked IA (**D1**)

| Rail | Variants |
|---|---|
| Status | **Omni + `statusCategoryVariants`** (dot / cc / contagion — from catalog) |
| Dense families needing CAP story (`status.resist`, etc.) | Catalog `expand: "status-category"` so join yields `status.resist.dot` |

Status-id expand remains valid for per-status open families; do not drop it wholesale — use the
expand mode that matches the family.

Today catalog loads `statusCategoryVariants` but cook emits Omni + status-ids only
(`DerivedSurfaceCook` CookStatusVariants) — **this module fixes that**.

## Locked Shared (**D3**)

BE cook DTO emits OTHER **Shared** as a first-class variant (e.g. on `other.variants` or sibling to
`actionCategoryVariants`). FE **selects** it; does not invent `OTHER_SHARED_VARIANT_ID`.

## Catalog / cook amend fields

| Field | Owner |
|---|---|
| `expand: status-category` on dense status families | catalog |
| Status rail variants include L2b | cook |
| OTHER Shared variant id + label | cook DTO |
| Action-category variants | cook (already) — FE must consume, not hardcode arrays |

## Guard

Cook primary tabs remain Elements / Status / Resources / Other — never sheetGroups as primary.

## Success criteria

- [ ] Dense `status.resist.dot|cc|contagion` join into Status rail.
- [ ] OTHER Shared appears in cook JSON; FE only selects.
- [ ] Grep: no production FE `STATUS_CATEGORY_VARIANTS` / `ACTION_CATEGORY_VARIANTS` expand arrays.
- [ ] Regression: expand:none families do **not** duplicate under Attack/Defense chips.

## Commands

```powershell
curl -s http://127.0.0.1:5088/api/catalogs/derived-surface | ConvertFrom-Json
cd web\fusion-rpg-web; npm test -- --run foldDerivedSurfaceVm
rg -n "STATUS_CATEGORY_VARIANTS|OTHER_SHARED_VARIANT_ID" web/fusion-rpg-web/src
```

## Sample cook fragment

```json
{
  "tabId": "status",
  "variants": [
    { "id": "omni", "label": "Omni" },
    { "id": "dot", "label": "Damage over time" },
    { "id": "cc", "label": "Crowd control" },
    { "id": "contagion", "label": "Contagion" }
  ]
}
```

## Boundaries

- **Always:** one cook SSOT.
- **Ask first:** renaming cook primary tabs; fifth primary tab.
- **Never:** FE-only IA chips that disagree with cook JSON; open A/B expand models after this lock.
