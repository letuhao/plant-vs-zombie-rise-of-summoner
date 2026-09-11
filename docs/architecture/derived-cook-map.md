# Capability map: derived-cook

**Status:** Phase 0 **approved by owner mandate** 2026-09-10 (full cook harden; width = architecture).  
**Program id:** `derived-cook`  
**Ideal:** [derived-cook-ideal.md](derived-cook-ideal.md)  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md)  
**Design SSOT:** [../design/spec-derived-stat-sheet.md](../design/spec-derived-stat-sheet.md) · [../design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md)  
**Existing contracts (amend, do not fork):** [gui-lego/spec-derived-surface-vm.md](gui-lego/spec-derived-surface-vm.md) · [actor-sheet/spec-derived-tab.md](actor-sheet/spec-derived-tab.md)  
**Shared paint:** [gui-lego/spec-element-paint-ssot.md](gui-lego/spec-element-paint-ssot.md) (gui-lego; Condition Wave 1 first ship)  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md) P0 → **Harden** (was Done; reopen until cook truth lands)  
**Sibling programs:** [condition-glance-map.md](condition-glance-map.md) · [shield-sheet-map.md](shield-sheet-map.md)  
**Plans (after specs):** `tasks/derived-cook-plan.md` · `tasks/derived-cook-todo.md`  
**DESIGN-GATE:** UI + Player menus · Stats / derived sheet design docs · ActorHub · tunables for caps

**Owner mandate:** fix Derived cook bugs **fully** — BE projection + cook IA + six states + paint SSOT
wire + player copy. Cheap FE-only / hardcoded CAP / mute `data-el` passes are **out of acceptance**.

---

## Owner decisions (locked 2026-09-10 strengthen)

| Id | Decision |
|---|---|
| **D1** | Status cook L2b: rail = **Omni + `statusCategoryVariants`**; dense families use `expand: status-category` |
| **D2** | Wire `renderState` **authoritative**; FE may recompute only for parity goldens |
| **D3** | OTHER **Shared** emitted by BE cook DTO; delete FE invent |
| **D4** | Show-unchanged hides **`default` only**; `no-producer` stays visible with fiction label |
| **D5** | Fold accepts injectable `themeRegistry` (amend fold to match surface-vm) |
| **D6** | Sheet channel `Value` stays **`double` for now** (overflow/exempt note); shield HP stays `long` |
| **D7** | `derived-statrow-gauge` **deferred** — not Wave 1–2 Done gate |
| **S1–S3** | Shared with condition-glance / shield-sheet (layers on sheet; live bag) |

---

## What this program is

Harden the shipped Derived Lego cook so it matches the design SSOT: registry-truth six states and
caps, cook-owned IA (including Status L2b + OTHER Shared), one element paint path, fiction-only
player band.

---

## Ownership splits

| Concern | Owner | Must not |
|---|---|---|
| Sheet channel wire (state/cap/unit/default) | `derived-sheet-projection` | FE heuristics as SSOT |
| Cook variants / Shared / status expand | `derived-cook-ia` | FE invent chips |
| Cap numbers | `derived-cap-ssot` + tuning | `KNOWN_CAPS` literals |
| Six-state join | `derived-render-states` | Third classification |
| Fold / VM | amend `spec-derived-surface-vm` | Second fold |
| Element paint | shared `element-paint-ssot` + `derived-element-paint-wire` | CSS `data-el` twins |
| Theme packs for cook tabs / action-category | `derived-theme-packs` | Private L2b maps |
| Player copy | `derived-player-copy` | channelId / GG-49 on band |

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `derived-sheet-projection` | BE: extend sheet channel DTO with renderState/cap/unitClass/default; UniqueDemon honesty | ActorHub / sheet compose | [derived-cook/spec-derived-sheet-projection.md](derived-cook/spec-derived-sheet-projection.md) |
| `derived-cook-ia` | BE+catalog: Status L2b reachability; OTHER Shared first-class; drop FE variant hardcodes | cook catalog | [derived-cook/spec-derived-cook-ia.md](derived-cook/spec-derived-cook-ia.md) |
| `derived-cap-ssot` | Caps from registry/policy on wire; delete FE `KNOWN_CAPS` | projection, tunables | [derived-cook/spec-derived-cap-ssot.md](derived-cook/spec-derived-cap-ssot.md) |
| `derived-render-states` | Pure cook×sheet → six states only; goldens | projection, cook-ia | [derived-cook/spec-derived-render-states.md](derived-cook/spec-derived-render-states.md) |
| `derived-fold-harden` | Align fold to VM contract; LadderIndex; themeRegistry; no private paint | render-states, paint wire | [derived-cook/spec-derived-fold-harden.md](derived-cook/spec-derived-fold-harden.md) |
| `derived-element-paint-wire` | Chip/CSS consume `resolveElementPaint`; remove data-el overrides | element-paint-ssot | [derived-cook/spec-derived-element-paint-wire.md](derived-cook/spec-derived-element-paint-wire.md) |
| `derived-theme-packs` | action-category + cook-tab packs; catalog glyphs for status | theme packs | [derived-cook/spec-derived-theme-packs.md](derived-cook/spec-derived-theme-packs.md) |
| `derived-player-copy` | Compose/unit/cap/state fiction; ban engine jargon on band | catalogs/locale | [derived-cook/spec-derived-player-copy.md](derived-cook/spec-derived-player-copy.md) |
| `derived-statrow-gauge` | Spark/pips from catalog gauge — **D7 deferred** (not Done gate) | fold | [derived-cook/spec-derived-statrow-gauge.md](derived-cook/spec-derived-statrow-gauge.md) |
| `derived-volume-guard` | Stress fixture ~channel count + 500 | fold | [derived-cook/spec-derived-volume-guard.md](derived-cook/spec-derived-volume-guard.md) |
| `derived-recipe-wire` | Land amend recipe/bind/host; queue P0 Harden → Done | all above | [derived-cook/spec-derived-recipe-wire.md](derived-cook/spec-derived-recipe-wire.md) |

---

## Build order

```text
Wave 1 — truth on the wire
  derived-sheet-projection · derived-cook-ia · derived-cap-ssot
  (parallel: element-paint-ssot from condition-glance / gui-lego if not landed)

Wave 2 — join + paint
  derived-render-states → derived-fold-harden
  derived-element-paint-wire · derived-theme-packs · derived-player-copy

Wave 3 — surface land + guards
  derived-recipe-wire · derived-volume-guard
  derived-statrow-gauge (D7 deferred — optional after Wave 2)
```

---

## Success criteria (program)

- Six render states match design §3 with registry/wire truth — `unregistered` reachable; default ≠ no-producer.
- CAP painted only from wire/registry; zero `KNOWN_CAPS` in FE.
- Status dense category channels cookable (**D1**); OTHER Shared from BE cook (**D3**).
- Show-unchanged = **D4**; wire `renderState` = **D2**; themeRegistry inject = **D5**.
- Element chips use `element-paint-ssot` only — no `data-el` CSS paint SSOT.
- No player-band `channelId` / `GG-49` / raw state enums as product copy.
- `LadderIndex` formats correctly; amend surface-vm; no god TSX.
- Queue P0 Harden cleared when Waves 1–3 land (**without** requiring D7 gauge).

---

## Coverage-gap register (strengthen 2026-09-10)

| Id | Gap | Disposition |
|---|---|---|
| CG-D1 | Six-state / unregistered unreachable | **Closed in spec** — `derived-render-states` + **D2** |
| CG-D2 | FE `KNOWN_CAPS` / immune=1 | **Closed in spec** — `derived-cap-ssot` |
| CG-D3 | Status L2b not cookable | **Closed** — **D1** |
| CG-D4 | OTHER Shared FE invent | **Closed** — **D3** |
| CG-D5 | Show-unchanged hides no-producer | **Closed** — **D4** |
| CG-D6 | themeRegistry singleton drift | **Closed** — **D5** |
| CG-D7 | LadderIndex miss | **Closed in spec** — fold-harden / surface-vm |
| CG-D8 | Player-band GG-49 / channelId | **Closed in spec** — `derived-player-copy` |
| CG-D9 | Unattributed contributions | **Closed in spec** — owned by `derived-player-copy` |
| CG-D10 | Volume Guard 7 / G6 undefined | **Closed** — `derived-volume-guard` |
| CG-D11 | Dual fetch sheet+derived | **Closed in spec** — recipe-wire fetch matrix |
| CG-D12 | StatRow spark/pips | **Deferred D7** — not Done gate |
| CG-D13 | Channel Value → long | **Deferred** — **D6** keep double; separate Core ticket |
| CG-D14 | Design §3 turn.* stale | **Closed** — design errata 2026-09-10 |

---

## Explicitly out

| Out | Why |
|---|---|
| Core OverlayAdd / aura idempotence (pipeline D1/D2) | Ask to pull; not silent scope |
| Condition glance / Shield tab | Sibling programs |
| New private power curve | Power SSOT |
| Claiming paint-only fix as done | Owner mandate |
| Bare `tasks/plan.md` | Prefixed pair only |
