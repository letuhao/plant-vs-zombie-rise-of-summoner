# Module: `recipe-wire`

**Program:** `condition-glance` · **Map:** [../condition-glance-map.md](../condition-glance-map.md)  
**Amends:** [../gui-lego/spec-condition-surface-vm.md](../gui-lego/spec-condition-surface-vm.md),  
`docs/design/gui-lego/recipes/condition-console.json`, `foldConditionSurfaceVm`, piece register,
`bindSurface` ([spec-theme-bind.md](../gui-lego/spec-theme-bind.md))  
**Owner mandate:** Q7 — amend architecture correctly; no fourth god-TSX refactor

---

## Objective

Wire the approved recipe + fold + registries so Condition mounts the full module set: badges,
conditional shield/status, theme-bind, charts with `revision`, Hot sheet fields. **Single** VM SSOT
amended in place — never a parallel ConditionTab that bypasses RecipeMount.

---

## Recipe changes

| Change | Detail |
|---|---|
| `actor-identity` slots | `phase` → `phase-badge`; `elements` → `element-badge*` |
| `cond-hero` slots | add `shield` → `shield-status` (conditional bind) |
| `stand-row` live | `standing-bars`; `status-glyph-strip` only when items.length > 0 |
| Overlays | keep loading/error/pending; **no** `phase-empty` for 0 shield/status (omit instead) |

Composition must match [condition-glance-ideal.md](../condition-glance-ideal.md) tree (post-Q3).

---

## Fold changes (`foldConditionSurfaceVm`)

Parity with [spec-condition-surface-vm.md](../gui-lego/spec-condition-surface-vm.md):

- Badge payloads + `themeRef` from elementTyping / phase.
- `shield-status` only when mountable; else omit.
- Status strip only when `liveStatuses.length > 0`.
- Fiction titles ([spec-fiction-copy.md](spec-fiction-copy.md)).
- `revision` on chart/gauge payloads.
- Meter `icon` + `themeRef` always when catalog provides.
- Standing + resourcePools Hub path unchanged.

---

## Closed bus catalog (parity with surface-vm §6)

| Event | Payload | Sink |
|---|---|---|
| `condition.pool.select` | `{ poolId: string }` | VM / meter emphasis |
| `condition.status.open` | `{ statusId: string }` | Host opens Status tab |
| `condition.retry` | `{}` | Host `sheet.refetch()` |

Expanding this list requires map/ideal review.

---

## Realtime bus (Q5)

| Layer | Duty |
|---|---|
| Host | SignalR / query invalidation of `["actorSheet", id]` ([sheet-hot-projection](spec-sheet-hot-projection.md)) |
| Fold | Pure; bumps `revision` |
| Pieces | Render payload only — **no** fetch, **no** SignalR |
| Surface bus | Closed catalog above |

---

## Bind

Requires `theme-bind` shipped: `shieldThemeRef` resolved; nested meters/elements resolved.

---

## Success criteria

- [ ] SPA Condition uses RecipeMount only for glance body (no mute chip twin path left).
- [ ] Surface-vm omit rules landed in fold tests (no “No live effects applied”).
- [ ] Nested drafts authored before factory “done”: `pool-radial`, `standing-radar`, `standing-bars`, `status-glyph-strip`, `element-badge`, `phase-badge`, `shield-status`.
- [ ] CatalogIcon rendered whenever meter `icon` set.
- [ ] `vfx.select` class attached when pack defines it (element + resource consumers).
- [ ] Hot curl + SPA: shield + statuses appear when present; cold omits.
- [ ] Element badges use paint SSOT.
- [ ] Standing radar is recharts; animates on revision.
- [ ] `spec-condition-surface-vm.md` body matches fold (already amended — keep parity).
- [ ] Landmark / fold unit tests green.
- [ ] Recipe slot tree: identity→badges; hero→shield; stand→conditional strip.

---

## Testing

- Fold goldens: omit rules, fiction titles, themeRefs, revision, icon.
- Register: all new piece ids factory-registered.
- E2E: live actor Condition screenshot gate (real numbers + themed badges).

## Boundaries

- **Always:** recipe + fold + bus; amend surface-vm.
- **Never:** ConditionTab hand-built glance DOM; FE fixtures as Hot data; cheap CSS-only “done.”
- **Ask first:** expanding closed bus event list.

## Commands

```powershell
cd web\fusion-rpg-web
npm test -- --run foldConditionSurfaceVm
npm test -- --run ConditionTab
npm test -- --run gui-lego
# After BE Hot lands:
curl -s http://127.0.0.1:5088/api/actors/<id>/sheet
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~AuraDerivedEndpoints
```

## Project structure

```text
docs/design/gui-lego/recipes/condition-console.json
docs/design/gui-lego/pieces/*.html
web/.../foldConditionSurfaceVm.ts
web/.../bindSurface.ts
web/.../pieces/condition.tsx + new factories
web/.../features/gui-lego/themes/             # element-paint-ssot
src/FusionRpg.Server/UniqueActorHubCompose.cs # sheet-hot-projection
```

## ActorHub

Projection consumes Hub/runtime (`sheet-hot-projection`). No private FE magnitude fold.
