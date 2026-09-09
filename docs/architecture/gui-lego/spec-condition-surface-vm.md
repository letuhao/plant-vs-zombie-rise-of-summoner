# Module: condition-surface-vm — fold contract

**Program:** `gui-lego`  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md)  
**Map:** [../gui-lego-map.md](../gui-lego-map.md)  
**Composition:** [spec-composition.md](spec-composition.md)  
**Sheet SSOT:** [../actor-sheet/spec-condition-tab.md](../actor-sheet/spec-condition-tab.md),
[../actor-sheet/spec-actor-sheet-shell.md](../actor-sheet/spec-actor-sheet-shell.md),
[../../design/13-actor-sheet.html](../../design/13-actor-sheet.html)

---

## 1. Intent

Pure fold from actor sheet data model → `ConditionSurfaceVm` so views only render payloads.
Condition is a **glance** surface (`cond-hero` + `stand-row` + progression gauge), not a
`split-inspect` console.

**Name:** `foldConditionSurfaceVm(input) -> ConditionSurfaceVm`

Implementation language (later): pure TypeScript under FE. **No React.**

---

## 2. Inputs (`ConditionSurfaceVmInput`)

| Field | Source | Notes |
|---|---|---|
| `identity` | `/sheet` | name/species/side/phase/level/elements for shell summarize sibling |
| `condition` | `/sheet` | pools, shield summary, live statuses, xp, xpToNext, standing if known |
| `resources` | actor-surface catalog | roster, labels, icons, colors |
| `statuses` | actor-surface catalog | hudToken/color join for live glyphs |
| `locale` | FE i18n | formatter selection |
| `ui` | local intent | selectedPoolId only |
| `availability` | query status | ready / loading / error |
| `revision` | host monotonic | payload freshness |

Do **not** pass React nodes or DOM refs into the fold.

---

## 3. Outputs (`ConditionSurfaceVm`)

| Slice | Binds to pieces | Contents |
|---|---|---|
| `progression` | `progression-gauge` | level, xp, xpToNext, fillPct/pending |
| `hero` | `cond-hero` | shell for radial + meter list |
| `radial` | `pool-radial` | hp/shield ring payloads |
| `meters` | `pool-meter*` | six resource rows, selected, pending/value text |
| `standingRadar` | `standing-radar` or `phase-pending` | five axes if known |
| `standingBars` | `standing-bars` or `phase-pending` | five bars if known |
| `statusStrip` | `status-glyph-strip` or `phase-empty` | live instances only |
| `phasePayload` | `phase-loading/error` | whole-surface overlay when query not ready |
| `selectedPoolId` | — | selection SSOT |
| `revision` | — | monotonic bump each fold |

Shell identity is a sibling consumer of the same `/sheet` source, not a child of this VM.

---

## 4. Fold rules

1. Build progression first from `level`, `xp`, `xpToNext`.
2. Always emit one meter per catalog resource row; never infer the roster from a FE union.
3. Join live pool values by `resourceId`; if absent, emit pending/value-missing payload.
4. Build HP radial from the `hp` pool plus shield summary if known.
5. Join live status instances against the status catalog; render only applied instances, never the catalog roster.
6. Use standing payload only when a real five-axis producer exists; `channelSummary` is **not** a substitute.
7. Surface-level loading/error prefers `phase-*` payloads over fake zeroes.

Pieces **must not** re-join pool/status/identity data.

---

## 5. Pending and empty

| Condition | Payload behavior |
|---|---|
| `/sheet` loading | top-level `phase-loading` |
| `/sheet` error | top-level `phase-error`; bus `condition.retry` |
| `xpToNext` missing | progression bar + text marked pending; show current `xp` truthfully |
| pool current/max missing | meter slot still renders from catalog with pending copy |
| shield summary missing | omit outer ring or mark radial pending; never invent shield |
| live statuses empty | `phase-empty` or honest empty strip copy |
| standing absent | `phase-pending` on radar/bars |

---

## 6. Bus catalog

Closed surface bus:

- `condition.pool.select`
- `condition.status.open`
- `condition.retry`

No search/filter bus for Condition v1.

---

## 7. Test plan (implementation phase)

- Catalog fixture always yields six pool meters, including `poise`.
- Plant `hunger` label resolves to `Sun`.
- Progression shows real `xp` and pending `xpToNext` honestly.
- No `split-inspect` landmark at root.
- Standing stays pending until producer exists.
- Live status strip renders only joined live instances.
