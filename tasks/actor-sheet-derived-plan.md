# Actor-sheet derived surface — plan (runtime cook)

**Program:** `actor-sheet-derived` · **Index:** [docs/architecture/actor-sheet-map.md](../docs/architecture/actor-sheet-map.md) ·
**Binding:** DESIGN-GATE Stats + ActorSheet · [spec-derived-stat-sheet.md](../docs/design/spec-derived-stat-sheet.md) ·
[actor-hub-ssot.md](../docs/architecture/actor-hub-ssot.md) §8.1 · [spec-derived-tab.md](../docs/architecture/actor-sheet/spec-derived-tab.md).

**Status:** Runtime cook **shipped**. FE Derived rewrite is a **named follow-on** (out of this plan).

---

## Goal

Ship a non-flattened, tabbed derived-surface payload so clients can layout Elements / Status /
Resources / Other without inventing a third UnitClass taxonomy or flattening 269 channels into the
catalog response. Live magnitudes stay on `GET /api/actors/{id}/sheet` by `channelId`.

---

## Shipped (this plan)

| Piece | Path |
|---|---|
| Catalog v2 | `data/tuning/derived-stat-catalog.v2.json` — families + `expand` / `sheetGroup` / locale maps |
| Loader + reject rules | `DerivedStatSurfaceCatalogLoader` (schema 2 only; leaf-as-family rejected) |
| Host inject | `Program.cs` → `ActorSurfaceCatalogHub.ConfigureAll` (derived v2 + element/resource/status/sheet/aptitude) |
| Cook | `DerivedSurfaceCook.Build(lang, side)` |
| HTTP | `GET /api/catalogs/derived-surface?lang=&side=` |
| Contracts | `DerivedSurfaceDto` (+ Tab / Variant / Category / Family) |
| Parity | Expand ⊆ registry; **269 pin unchanged** |

### Closed expand rules

| Tab | Expand | Variants |
|---|---|---|
| `elements` | `element` | element-catalog (omni `presentationOnly`) |
| `status` | `status-id` | **Omni + 24** status-catalog chips (status-rail; replaces L2b category rail) |
| `resources` | `resource` | resource-catalog (6); `?side=` flips hunger/qi labels |
| `other` | `none` (+ skill `action-category`) | empty tab variants; `actionCategoryVariants` for skills |

Join: `{family}` or `{family}.{variantId}` → look up on `/sheet`. Missing → six render states (FE).

---

## Gaps (honest inventory)

| Gap | Disposition |
|---|---|
| v1 leaf status “families” (`status.resist.dot`) | **Fixed** by v2 |
| Catalog under-covered 28 combat / resources / other | **Fixed** by v2 content |
| No tabbed cook payload | **Fixed** — this endpoint |
| Program only Configure derived v1 | **Fixed** — ConfigureAll |
| Sparse open-prefix status on sheet (`status.resist.burn`) | **Follow-on** (live join only) |
| FE still wrong layout / `/derived` only | **Follow-on** (out of scope) |
| Full `GET /api/catalogs/actor-surface` fan-in never shipped | **Follow-on** — can embed cook as `derivedSurface` |
| §6.1 unattributed producers | Hub follow-on |
| Spec docs saying “268” | Errata to **269** when touching those files |
| Plate 13 / InspectSplit Derived rewrite | **Named follow-on** — `phase2-fe` |

---

## Acceptance (runtime)

- [x] `derived-stat-catalog.v2.json` loads; v1 not required at boot
- [x] `GET /api/catalogs/derived-surface?lang=en` → four tabs, cooked English
- [x] Missing lang keys fall back to `en`
- [x] `?side=zombie` flips hunger/qi labels
- [x] No leaf-as-family; six status families × four category variants
- [x] 28 element families; expand parity green; **269** unchanged
- [x] Prefixed plan/todo + gap table
- [x] No FE code required for this gate

---

## Verification

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~ActorSurfaceCatalogTests"
dotnet test tests/FusionRpg.Server.Tests --filter "FullyQualifiedName~DerivedSurface"
# manual: GET /api/catalogs/derived-surface?lang=en
```

Suggested commits: `Add derived-stat-catalog v2 with tabbed expand kinds` then
`Ship GET /api/catalogs/derived-surface cook`.
