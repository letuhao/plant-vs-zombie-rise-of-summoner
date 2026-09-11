# Derived cook console — the ideal

**Status:** idea phase locked into `/spec` 2026-09-10 (owner: full architecture fix, not paint-only).  
**Program id:** `derived-cook`  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md)  
**Surface:** ActorSheet **Derived** tab — recipe `derived-console` (not Condition glance).  
**Parent kit:** [gui-lego-ideal.md](gui-lego-ideal.md) · cook SSOT [design/spec-derived-stat-sheet.md](../design/spec-derived-stat-sheet.md)  
**Sibling programs:** [condition-glance-map.md](condition-glance-map.md) · [shield-sheet-map.md](shield-sheet-map.md)  
**Plans (later):** `tasks/derived-cook-plan.md` · `tasks/derived-cook-todo.md`

---

## Out loud (load-bearing)

1. Derived lives in the **RPG layer** — Hub channels, cook catalog, six render states. Never blocked by Unity fields.
2. Stage + layers — Derived is a band-2 tab body, not a route.
3. Recipe + pure fold + closed bus — **amend** existing fold/pieces; no second god console.
4. Theme packs + **`element-paint-ssot`** own paint — never `data-el` CSS twins or private bucket hex maps.
5. Buy before build — gauges already use paint hex; keep recharts/kit where locked.
6. Each cook bug is a **module** (BE projection, cook IA, six states, paint wire, player copy).
7. No engine vocabulary on the player band — no `channelId` join strings, no `GG-49` titles, no raw `no-producer` tags as product copy.

---

## What this program is

P0 Derived Lego **shipped structurally** (thin host → fold → bind → RecipeMount) but is **built, defective**
against the design SSOT: incomplete six states, FE-hardcoded CAP/status variants, element paint dual-path,
FE-invented OTHER `shared` chip, LadderIndex mis-format, player-band jargon.

This program **hardens the cook end-to-end** (BE sheet/cook projection + catalog IA + fold + paint SSOT
wire + player copy). Cheap CSS / mute-chip / FE-only CAP tables are **out of acceptance**.

**Not this program:** Core channel registration (`derived-stats` — already built). Pipeline OverlayAdd
audit D1/D2 (separate Core fix unless owner pulls in). Shield **tab** (`shield-sheet`). Condition glance
(`condition-glance`).

---

## Inventory (2026-09-10)

### Built

| Item | Evidence |
|---|---|
| Recipe + RecipeMount + closed bus | `DerivedTab.tsx`, `derived-console.json` |
| Cook expand × join fold | `foldDerivedSurfaceVm.ts`, `derivedCook.ts` |
| OTHER Shared vs Attack split (no duplicate expand:none under Attack) | fold filter + tests |
| Six-state *names* in FE | `DerivedRenderState` union |

### Built, defective

| Id | Defect | Cite |
|---|---|---|
| D1 | `unregistered` unreachable; absent live always `no-producer` | `derivedCook.ts` resolve |
| D2 | CAP literals on FE (`KNOWN_CAPS` 0.95) | `derivedCook.ts` |
| D3 | Dense `status.*.dot\|cc\|contagion` not cookable (status-id expand only) | cook + catalog |
| D4 | FE hardcode `STATUS_CATEGORY_VARIANTS` / `ACTION_CATEGORY_VARIANTS` | `derivedCook.ts` |
| D5 | OTHER `shared` invented on FE, not in BE cook | fold + `DerivedSurfaceCook` |
| D6 | Element paint via `data-el` CSS vars fighting theme packs | `derivedConsole.css`, chip |
| D7 | Private contribution bucket paint maps | fold `PAINT_BUCKET`, cook `BUCKET_COLORS` |
| D8 | Status→L2b + glyph maps hardcoded in fold | fold helpers |
| D9 | Player-band `channelId` / `GG-49` / raw state tags | fold + domain pieces |
| D10 | `LadderIndex` falls through to gameUnits | `formatDerivedMagnitude.ts` |
| D11 | No action-category / cook-tab theme packs | theme registry |
| D12 | Compose/unit sentences FE constants | `COMPOSE_SENTENCE` |
| D15 | `NO_PRODUCER_HINT` branch dead (same as else) | `derivedCook.ts` |

### Wiring gaps

| Id | Gap |
|---|---|
| W1 | Sheet channel DTO lacks `renderState` / `cap` / `unitClass` / defaults |
| W4 | `element-paint-ssot` specified; Derived not migrated |
| W5–W6 | Cap + producer/stub flags not on wire |
| W9 | Dual `/sheet` + `/derived` fetch until sheet-complete |

### Real gaps

| Id | Gap |
|---|---|
| G1 | Registry-truth six-state machine |
| G2 | Status L2b category occupancy on cook |
| G3 | Optional StatRow spark/pips (design §5.2b) — **D7 deferred** |
| G5 | Single resolve path for element paint |
| G6 | Volume fixture ~ Guard 7 (current §1 scale + 500 stress) — `derived-volume-guard` |
| G8–G10 | Theme packs + catalog copy + BE Shared variant |

**Inventory note:** W2/W3/W7/W8 and D13/D14 were audit-internal ids; not separate modules. W2 UniqueDemon →
`derived-sheet-projection`; W3 unattributed → `derived-player-copy`; W7 themeRegistry → **D5** /
`derived-fold-harden`; W8 Pending fields → fold + recipe-wire.

### Owner locks (strengthen 2026-09-10)

**D1–D7** + shared **S1–S3** — see [derived-cook-map.md](derived-cook-map.md). No open A/B Options.

---

## Closed module catalog

| Module id | Kind | Spec home |
|---|---|---|
| `derived-sheet-projection` | BE | `derived-cook/` |
| `derived-cook-ia` | BE + catalog | `derived-cook/` |
| `derived-cap-ssot` | BE + FE delete hardcode | `derived-cook/` |
| `derived-render-states` | pure join | `derived-cook/` |
| `derived-fold-harden` | amend fold/VM | `derived-cook/` + amend `gui-lego/spec-derived-surface-vm` |
| `derived-element-paint-wire` | consume shared paint | `derived-cook/` → `gui-lego/spec-element-paint-ssot` |
| `derived-theme-packs` | packs | `derived-cook/` + `gui-lego` packs |
| `derived-player-copy` | fiction / locale | `derived-cook/` |
| `derived-statrow-gauge` | optional piece polish | `derived-cook/` |
| `derived-volume-guard` | tests/guards | `derived-cook/` |
| `derived-recipe-wire` | surface land | `derived-cook/` |

Shared **not owned here:** `element-paint-ssot` (gui-lego; condition-glance Wave 1 first ship — Derived wires same SSOT).

---

## Bug → module map

| Defect | Modules |
|---|---|
| D1 D15 G1 | `derived-render-states` + `derived-sheet-projection` |
| D2 W5 | `derived-cap-ssot` |
| D3 D4 G2 | `derived-cook-ia` |
| D5 G10 | `derived-cook-ia` |
| D6 D7 W4 G5 | `derived-element-paint-wire` (+ shared paint SSOT) |
| D8 D11 G8 | `derived-theme-packs` |
| D9 D12 | `derived-player-copy` |
| D10 | `derived-fold-harden` |
| W1 W6 W9 | `derived-sheet-projection` |
| G3 | `derived-statrow-gauge` |
| G6 | `derived-volume-guard` |
| Land | `derived-recipe-wire` |

---

## Correct shapes / bans

| Ban | Correct |
|---|---|
| FE `KNOWN_CAPS` / regex producer hints as product truth | Wire from registry/policy |
| Private element / bucket color tables | `element-paint-ssot` + theme packs |
| Invent cook IA chips on FE | Cook DTO / catalog owns variants |
| Third classification beyond six states | Six states only |
| God TSX under `ui/actor/derived/*` | Recipe + fold only |
| Piece-level fetch | Host invalidate → re-fold |
| Claiming Derived “done” while CAP/states FE-fake | Program success criteria |

---

## Prior art (genre)

Dense combat grids (PoE/Last Epoch) separate **matrix occupancy** from **inspect detail**; CAP markers
exist so players stop stacking dead resist. Failure modes this program prevents: mute grey for typed
columns, inventing 0 for missing channels, hiding CAP so players buy nothing.

---

## Deferred (explicit)

| Out | Why |
|---|---|
| Core `Overlay` replace-vs-add (pipeline audit D1/D2) | Core/injector; ask to pull |
| Condition glance layout | `condition-glance` |
| Shield tab stack UI | `shield-sheet` |
| HUD element-badge adoption | After paint SSOT + Condition |

---

## The real question

Not “does Derived exist?” — it does. The question is whether the cook **tells the truth** the design
SSOT already wrote (six states, registry CAP, cook-owned IA, one paint path). This program answers yes.
