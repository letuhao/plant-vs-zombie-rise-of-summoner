# Spec: `condition-tab`

**Module id:** `condition-tab` · **Program:** [actor-sheet-map.md](../actor-sheet-map.md) ·
**Depends on:** `actor-sheet-shell` · **Sibling program:** [condition-glance-map.md](../condition-glance-map.md)  
**Status:** Amended 2026-09-10 — aligns with condition-glance owner locks (Q3/Q6/Q7).

---

## Assumptions

1. Resource **ids and labels** come from `resource-catalog` (includes `poise`). FE must not use the
   five-string `ResourceId` union as a roster.
2. Cold UniqueActor `/sheet` always projects six `resourcePools` (Hub Max via
   `ResourceBaselineSubsystem` + Current from persisted/at-rest). FE pending only when
   `sheet` is null / missing pools — not as the normal cold state.
3. Standing five-axis vector is **owned by this tab**. `/sheet.standing` projects `PowerVector`
   (`ActorPowerCache.Compose` from durable equip + tree atoms). Do not invent values and do not
   reuse `channelSummary` as fake Standing. Absent field → pending; present (incl. Zero) → ready.
4. Shell rail shows name / Lv / role only. Condition owns **progression gauge**, **species
   identity** (`actor-identity`), vitality (`cond-hero`), and Standing (`stand-row`).
5. Layout is a **2×2 CSS grid** on `.condition-console` (`prog|identity` / `hero|stand`), not a
   single-column stack. `status-glyph-strip` nests under `stand-row` → `live` **only when**
   live statuses exist (Q3 omit).
6. Glance body is **recipe + fold + pieces** ([spec-recipe-wire.md](../condition-glance/spec-recipe-wire.md),
   [spec-condition-surface-vm.md](../gui-lego/spec-condition-surface-vm.md)). `ConditionTab.tsx` is a
   **thin host** (sheet query + RecipeMount) — not a god TSX.
7. Hot live statuses + shield on `/sheet` are **in program** via
   [spec-sheet-hot-projection.md](../condition-glance/spec-sheet-hot-projection.md) (Q6).

---

## Objective

First-paint **Condition** glance on a 2×2 grid: progression \| species identity; vitality \|
Standing (live glyphs nested when present). HP radial + meters from `/sheet.resourcePools`;
Standing from `/sheet.standing`; cold omits status/shield mounts; Hot projects real live fields.

**Success:** Opening Condition never prints a dotted channel id; every catalog resource has a meter
slot; plant `hunger` label resolves to **Sun** from catalog; no five-string `ResourceId` roster;
wide panel uses horizontal space; element badges use paint SSOT; no mute chips; no empty status/shield chrome.

### `/sheet` current-state contract

| Field | Cold UniqueActor | Hot (Injector session) |
|---|---|---|
| `standing` | Always present — `PowerVector` (Zero OK) | same |
| `resourcePools[6]` | Always present — Max from Hub; Current persisted or at-rest = Max | same + live current when available |
| `liveStatuses` | `[]` → FE **omits** strip | projected from runtime when present |
| `shieldSummary` | `null` → FE **omits** shield-status + ring | projected when shield up |

---

## Tech Stack / Commands / Structure

GUI Lego pieces + theme packs + `recharts` (lazy) + `lucide-react` / CatalogIcon / StatusGlyph.
Host:

```text
web/.../ui/actor/ConditionTab.tsx          # thin host only
web/.../features/gui-lego/foldConditionSurfaceVm.ts
web/.../ui/gui-lego/recipes/condition-console.json
```

```powershell
cd web\fusion-rpg-web
npm test -- --run ConditionTab
npm test -- --run foldConditionSurfaceVm
```

---

## Design

- **Glance grammar:** 2×2 — `progression-gauge` \| `actor-identity` / `cond-hero` \| `stand-row`.
- Identity: `phase-badge` + `element-badge*` (paint SSOT); species line / `speciesMessage` fiction;
  never role badge; never mute `.chip`.
- Vitality: meters + radial; `shield-status` under `cond-hero` when mountable.
- Standing: recharts radar + bars; fiction title (fiction-copy).
- Status strip: compose `StatusGlyph`; mount only when `liveStatuses.length > 0`; bus
  `condition.status.open` → Status tab.
- Pool select: bus `condition.pool.select` — local emphasis only.

**Sun bank (`pvz.*`) never appears** — only actor `hunger`.

Piece contracts: see [condition-glance-map.md](../condition-glance-map.md) module table.

---

## Tunables

None new. Colors/icons from catalogs + theme packs. Shield policy owns max stacks if DTO widens.

---

## Testing / Boundaries / Success

- Unit: six meters from fixture catalog including `poise`; Sun label for plant hunger.
- Unit: `liveStatuses: []` / `shieldSummary: null` → no strip / shield payloads.
- Unit: sheet with standing + pools lights bars/meters; badges carry themeRefs.
- Always: catalog iteration; landmark grid areas; RecipeMount path only for glance body.
- Never: hardcoded five resources; lawn sun bank; species chips on rail; god ConditionTab DOM;
  empty “No live effects” chrome; FE fixtures as Hot data.
- Success: matches recipe + piece grammar; Hot curl shows live fields when session Hot.

---

## Open Questions

None for Standing / pools / Hot ownership — Hot projection is `sheet-hot-projection` in
`condition-glance`. Shield **tab** rewrite remains a separate queue row.
