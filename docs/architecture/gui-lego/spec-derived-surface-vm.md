# Module: derived-surface-vm — fold contract

**Program:** `gui-lego`  
**Harden program:** [../derived-cook-map.md](../derived-cook-map.md) (amend this contract — do not fork)  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md)  
**Map:** [../gui-lego-map.md](../gui-lego-map.md)  
**Composition:** [spec-composition.md](spec-composition.md)  
**Cook / sheet SSOT:** [../actor-sheet/spec-derived-tab.md](../actor-sheet/spec-derived-tab.md),
[../../design/spec-derived-stat-sheet.md](../../design/spec-derived-stat-sheet.md),
[../../design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)

---

## 1. Intent

Pure fold from Model → `DerivedSurfaceVm` so Views only render payloads. Formats magnitudes
(GG-46), maps Pending to lifecycle pieces, attaches `themeRef`, never invents channel values.

**Name:** `foldDerivedSurfaceVm(input) → DerivedSurfaceVm`

Implementation language (later): pure TypeScript under FE (or shared pure package). **No React.**

---

## 2. Inputs (`DerivedSurfaceVmInput`)

| Field | Source | Notes |
|---|---|---|
| `identity` | Actor sheet / roster | displayName, level, side |
| `cook` | `GET /api/catalogs/derived-surface` | tabs, variants, families |
| `channels` | `/sheet` derived (preferred) or lean `/derived` | by `channelId` |
| `locale` | FE i18n | formatter selection |
| `ui` | Local intent state | tabId, variantId, query, showUnchanged, selectedChannelId |
| `themeRegistry` | Theme packs | handle or lookup fn |
| `availability` | Query status | ready / loading / error |

Do **not** pass React nodes or DOM refs into the fold.

---

## 3. Outputs (`DerivedSurfaceVm`)

| Slice | Binds to pieces | Contents |
|---|---|---|
| `identity` | `identity-hd` | who, meta lines, `themeRef` (side) |
| `search` | `tool-search` | `query` |
| `showUnchanged` | `tool-toggle` | `value`, `label` |
| `primaryRail` | `rail-primary` + chips | cook tabs, selected |
| `variantRail` | `rail-variant` + chips | variants for tab, selected, themeRefs |
| `families` | `family-list` / `family-block` / `channel-row` | filtered rows |
| `inspect` | `inspect-pane` or lifecycle | hero, meta, cap, gauges, sources |
| `foot` | `surface-foot` | hiddenCount, deferred notes |
| `phase` | surface-level | `ready\|loading\|empty\|error` |
| `selectedChannelId` | — | selection SSOT |
| `selectedInstanceId` | — | e.g. `row:{channelId}` |
| `revision` | — | monotonic bump each fold |

When `availability` is loading/error, prefer top-level `phase-*` payloads over inventing empty
families.

---

## 4. Filtering and join (owned by fold, not pieces)

Order (align with existing cook helpers; do not fork a second cook SSOT):

1. Resolve primary tab + variant from `ui` + cook.  
2. Expand cook families → candidate `channelId`s.  
3. Join sheet channels; compute **six render states**.  
4. Apply search query (title/reading/id).  
5. Apply `showUnchanged` (hide default/stub per product rules).  
6. Build row payloads with `instanceId = row:{channelId}`.  
7. Build inspect from `selectedChannelId` (fallback: first visible row).  
8. Resolve `themeRef` per row/chip from expand mode (element / status-category / …).  
9. Format magnitudes → `valueText` + keep `valueRaw` (`long` / string decimal as sheet provides).

Pieces **must not** re-join cook to sheet.

---

## 5. Pending and empty

| Condition | Payload behavior |
|---|---|
| Sheet/cook query loading | `phase: loading` → `phase-loading` piece |
| Query error | `phase: error` + message → `phase-error`; bus `derived.retry` |
| Ready, zero visible rows | `phase: empty` → `phase-empty` in dock |
| Channel field Pending | row/inspect field uses `phase-pending`; **no invented magnitude** |
| Cap absent | omit `cap-note` or `capKind: none` — never invent CAP |

---

## 6. Magnitude formatting (GG-46)

Fold owns formatter selection from UnitClass / channel metadata
([spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md)).

Payload always carries:

```json
{
  "valueRaw": 2847,
  "valueText": "2,847",
  "unitLabel": null,
  "formatterId": "whole"
}
```

Views render `valueText` for display; tools may show `valueRaw` in debug only.

---

## 7. Theme resolution step

Either inside the fold or a pure `bindThemes(vm, registry)` pass immediately after:

1. Read `themeRef: { kind, id }` on payload.  
2. Lookup pack; on miss use `neutral`.  
3. Attach `themeResolved: { themeId, css, paint, vfx }` for the view binder.

Views apply CSS variables / paint hex; they do not look up packs by element name strings.

---

## 8. Bus → fold loop

Host (later React) listens to the closed bus catalog, updates `ui`, re-runs fold, remounts payloads.
Pieces never call the fold.

---

## 9. Test plan (implementation phase)

- Golden fixtures: cook + sheet JSON → VM snapshot (revision ignored or normalized).  
- Pending channel → no numeric invention.  
- Theme miss → neutral.  
- Selection change → only `selected` flags and inspect slice change.

No golden tests required in the design-only wave.
