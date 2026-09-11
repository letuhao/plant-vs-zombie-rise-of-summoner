# Module: derived-surface-vm — fold contract

**Program:** `gui-lego`  
**Harden program:** [../derived-cook-map.md](../derived-cook-map.md) (amend this contract — do not fork)  
**Locks:** **D2** · **D4** · **D5** themeRegistry inject · LadderIndex via UnitClass  
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

**No React.** No fetch. No private paint maps.

---

## 2. Inputs (`DerivedSurfaceVmInput`) — target after harden (**D5**)

| Field | Source | Notes |
|---|---|---|
| `identity` | Actor sheet / roster | displayName, level, side (side theme only if recipe uses identity-hd) |
| `cook` / `cookTabs` | `GET /api/catalogs/derived-surface` | Alias OK during migration; **document one name in code** |
| `channels` / `sheetChannels` | `/sheet` derived preferred | lean `leanChannels` only when sheet incomplete |
| `locale` | FE i18n | formatter selection |
| `ui` | Local intent | tabId, variantId, query, showUnchanged, selectedChannelId |
| `themeRegistry` | Theme packs | **inject** lookup fn/handle — do not rely on undocumented singleton alone |
| `availability` | Query status | ready / loading / error |

Do **not** pass React nodes or DOM refs into the fold.

### Shipped → target delta

| Shipped (defective/drift) | Target |
|---|---|
| No `themeRegistry` input; `resolveTheme` singleton | Injectable registry (**D5**) |
| `cookTabs` / `sheetChannels` names | Keep aliases or rename once — amend both names here |
| FE invents OTHER Shared | Consume BE Shared (**D3**) |
| Join invents CAP / states | Consume wire `cap` / `renderState` (**D2**) |
| Show-unchanged hides default+no-producer | Hide **default only** (**D4**) |
| LadderIndex → gameUnits | Format via UnitClass ledger |

---

## 3. Outputs (`DerivedSurfaceVm`)

| Slice | Binds to pieces | Contents |
|---|---|---|
| `identity` | `identity-hd` (optional) | who, meta; recipe may omit (rail owns identity) |
| `search` | `tool-search` | `query` |
| `showUnchanged` | `tool-toggle` | `value`, `label` |
| `primaryRail` | `rail-primary` + chips | cook tabs, selected |
| `variantRail` | `rail-variant` + chips | variants, themeRefs |
| `families` | `family-list` / `family-block` / `channel-row` | filtered rows + six states |
| `inspect` | `inspect-pane` or lifecycle | hero, fiction meta, cap, gauges, sources |
| `foot` | `surface-foot` | hiddenCount (defaults hidden) |
| `phase` | surface-level | `ready\|loading\|empty\|error` |
| `selectedChannelId` | — | selection SSOT |
| `selectedInstanceId` | — | e.g. `row:{channelId}` |
| `revision` | — | monotonic bump each fold |

---

## 4. Filtering and join

1. Resolve primary tab + variant from `ui` + cook.  
2. Expand cook families → candidate `channelId`s (cook variants only — no FE hardcode arrays).  
3. Join sheet channels; prefer wire **`renderState`** (**D2**).  
4. Apply search query (title/reading — not player-band raw id as primary).  
5. Apply `showUnchanged`: hide **`default` only** (**D4**).  
6. Build row payloads with `instanceId = row:{channelId}`.  
7. Build inspect from selection (fiction copy — see `derived-player-copy`).  
8. Resolve `themeRef` via **themeRegistry**.  
9. Format magnitudes — **LadderIndex** included via UnitClass map.

Pieces **must not** re-join cook to sheet.

---

## 5. Pending and empty

| Condition | Payload behavior |
|---|---|
| Sheet/cook query loading | `phase: loading` → `phase-loading` |
| Query error | `phase: error` → `phase-error`; bus `derived.retry` |
| Ready, zero visible rows | `phase: empty` → `phase-empty` |
| Channel field Pending | `phase-pending`; **no invented magnitude** |
| Cap absent / null | omit CAP chrome — never invent |

---

## 6. Magnitude formatting (GG-46)

Fold owns formatter from `unitClass` on wire/catalog.
`LadderIndex` must map explicitly — never fall through to `gameUnits`.

```json
{
  "valueRaw": 2847,
  "valueText": "2,847",
  "unitLabel": null,
  "formatterId": "whole"
}
```

---

## 7. Theme resolution (**D5**)

1. Read `themeRef: { kind, id }` on payload.  
2. Lookup via injected `themeRegistry`; miss → `neutral`.  
3. Attach `themeResolved: { themeId, css, paint, vfx }`.  
4. Element paint via `element-paint-ssot` / packs — no `data-el` CSS SSOT.

---

## 8. Bus → fold loop

Host updates `ui` from closed bus, re-runs fold, remounts. Pieces never call the fold.
Invalidate `["actorSheet", id]` → re-fold → `revision` bump.

---

## 9. Test plan

- Golden: cook + sheet JSON → VM (six states, Shared from BE, CAP from wire).  
- LadderIndex golden.  
- Show-unchanged hides default only.  
- Theme miss → neutral.  
- No `GG-49` / `Join: channelId` in inspect (player-copy).

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run foldDerivedSurfaceVm formatDerivedMagnitude DerivedCombatConsole.contract
```
