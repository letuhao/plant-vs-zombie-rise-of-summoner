# Module: condition-surface-vm — fold contract

**Program:** `gui-lego`  
**Ideal:** [../gui-lego-ideal.md](../gui-lego-ideal.md) · [../condition-glance-ideal.md](../condition-glance-ideal.md)  
**Map:** [../gui-lego-map.md](../gui-lego-map.md) · [../condition-glance-map.md](../condition-glance-map.md)  
**Amend in place:** [../condition-glance/spec-recipe-wire.md](../condition-glance/spec-recipe-wire.md) — do not fork a second VM.  
**Composition:** [spec-composition.md](spec-composition.md)  
**Sheet SSOT:** [../actor-sheet/spec-condition-tab.md](../actor-sheet/spec-condition-tab.md),
[../actor-sheet/spec-actor-sheet-shell.md](../actor-sheet/spec-actor-sheet-shell.md),
[../../design/13-actor-sheet.html](../../design/13-actor-sheet.html)  
**Owner locks:** Q3 omit-empty · Q5 revision/realtime · Q7 amend this file body

---

## 1. Intent

Pure fold from actor sheet data model → `ConditionSurfaceVm` so views only render payloads.
Condition is a **glance** surface on a **2×2 CSS grid**
(`progression-gauge` \| `actor-identity` / `cond-hero` \| `stand-row`), not a
`split-inspect` console. Live status nests under `stand-row` **only when** there are live
instances — never a fifth `main` sibling, never empty chrome.

**Name:** `foldConditionSurfaceVm(input) -> ConditionSurfaceVm`

Implementation: pure TypeScript under FE. **No React** inside the fold.

---

## 2. Inputs (`ConditionSurfaceVmInput`)

| Field | Source | Notes |
|---|---|---|
| `identity` | `/sheet` | species/phase/elements for Condition `actor-identity` (rail takes name/Lv/role only) |
| `condition` | `/sheet` | pools, shield summary, live statuses, xp, xpToNext, **standing** (`PowerVector`) |
| `resources` | actor-surface catalog | roster, labels, icons, colors |
| `statuses` | actor-surface catalog | hudToken/color join for live glyphs |
| `locale` | FE i18n | formatter selection |
| `ui` | local intent | selectedPoolId only |
| `availability` | query status | ready / loading / error |
| `revision` | host monotonic | payload freshness — charts animate on bump |

Do **not** pass React nodes or DOM refs into the fold.

---

## 3. Outputs (`ConditionSurfaceVm`)

| Slice | Binds to pieces | Grid / nest | Contents |
|---|---|---|---|
| `main[0]` | `progression-gauge` | area `prog` | level, xp, xpToNext, fillPct/pending, `revision`, themeRef when pack applies |
| `main[1]` | `actor-identity` | area `identity` | speciesName / speciesMessage; slots → **`phase-badge`**, **`element-badge*`** (no mute chips; no Name/Lv/role) |
| `main[2]` | `cond-hero` | area `hero` | `pool-radial` + meters; slot **`shield` → `shield-status` only when mountable** |
| `main[3]` | `stand-row` | area `stand` | radar + bars; nested status strip **only when items.length > 0** |
| `standingRadar` / `bars` | under stand-row | — | absolute axis ints; fillPct = relative to max; `revision` |
| `statusStrip` | under stand-row `live` | nested | **omit payload entirely** when no live statuses |
| `shield` | under cond-hero | nested | **omit** when `shieldSummary` null or current/max ≤ 0 |
| `phasePayload` | `phase-loading/error` | overlay | host retry when query fails |
| `revision` | — | — | monotonic bump each fold |

Every chart/gauge payload carries `revision`. Themed pieces carry `themeRef` / `shieldThemeRef` for [spec-theme-bind.md](spec-theme-bind.md).

Shell summarize is a sibling consumer of `/sheet` for name/level/role only — not a child of this VM.

---

## 4. Fold rules

1. Emit `main` in grid order: progression → identity → hero → stand (placement is CSS `grid-area`).
2. Always emit one meter per catalog resource row; never infer the roster from a FE union.
3. Join live pool values by `resourceId`; if absent, emit pending/value-missing payload (**no `0%` fill**).
4. Build HP radial from the `hp` pool; attach shield ring + `shieldThemeRef` **only** when shield is mountable.
5. Join live status instances against the status catalog; nest under stand-row **only if** length > 0.
6. Use `/sheet.standing` when present (incl. Zero vector); `channelSummary` is **not** a substitute.
   Bar/radar fillPct = relative normalize to max axis; labels keep absolute ints.
7. Identity: emit `phase-badge` when phase label present; emit `element-badge` per element id with
   `themeRef: { kind: "element", id }`; never mute `<span className="chip">`.
8. Species missing: set fiction `speciesMessage` (see fiction-copy); **keep** identity block mounted.
9. Surface-level loading/error prefers host retry / field pending over fake zeroes.
10. Pass through fold `icon` for meters (CatalogIcon at factory).

Pieces **must not** re-join pool/status/identity data.

---

## 5. Pending and empty (Q3 omit)

**Progressive enrichment:** Condition always has `ActorView`, so the host keeps
`availability: "ready"` while `/sheet` loads — never full-surface-swap to `phase-loading`.
ActorView fills progression level/xp and catalog pool slots; `/sheet` enriches identity,
`xpToNext`, and live instances when it arrives.

| Condition | Payload behavior |
|---|---|
| `/sheet` loading | glance stays `ready`; pending field copy where sheet-only data is missing |
| `/sheet` error | glance stays mounted; compact host retry strip → bus `condition.retry` → `sheet.refetch()` |
| `xpToNext` missing | progression bar + text marked pending; show current `xp` truthfully |
| pool current/max missing | meter slot still renders from catalog with pending copy; **no `0%` fill** |
| shield null or HP ≤ 0 | **omit** `shield-status` payload and radial shield ring — **no** empty chrome / `phase-empty` |
| `sheet == null` | status strip **omitted**; field pending elsewhere as needed |
| `sheet` present, `liveStatuses: []` | **omit** status strip entirely — **no** “No live effects applied” prose |
| standing absent | `phase-pending` on radar/bars; **no fabricated axis `value: 0`** |
| species missing | identity stays; fiction `speciesMessage` only |

---

## 6. Bus catalog (closed)

| Event | Payload | Sink |
|---|---|---|
| `condition.pool.select` | `{ poolId: string }` | VM selection / meter emphasis |
| `condition.status.open` | `{ statusId: string }` | Host opens Status tab (glyph activate) |
| `condition.retry` | `{}` | Host `sheet.refetch()` |

No search/filter bus for Condition v1. Expanding this list is a reviewed map/ideal change.

---

## 7. Test plan (implementation phase)

- [ ] Catalog fixture always yields six pool meters, including `poise`.
- [ ] Plant `hunger` label resolves to `Sun`.
- [ ] Progression shows real `xp` and pending `xpToNext` honestly.
- [ ] No `split-inspect` landmark at root.
- [ ] Standing ready when `/sheet.standing` present; pending when sheet null.
- [ ] `liveStatuses: []` → **no** status-strip node in bind tree.
- [ ] `shieldSummary: null` → **no** shield-status payload.
- [ ] Identity emits `element-badge` / `phase-badge` payloads with themeRefs — no mute chip fields.
- [ ] Meter payloads include `icon` + `themeRef`; chart payloads include `revision`.
- [ ] Forbidden strings absent: `definitions.md`, `No live effects applied`, `tap for Status`.
