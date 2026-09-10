# Capability map: condition-glance

**Status:** Phase 0 **approved** 2026-09-10 (owner). Module specs authorized.  
**Program id:** `condition-glance`  
**Ideal:** [condition-glance-ideal.md](condition-glance-ideal.md)  
**Procedure:** [idea-ui-phase.md](idea-ui-phase.md)  
**Parent kit:** [gui-lego-map.md](gui-lego-map.md) · [gui-lego-authoring.md](gui-lego-authoring.md)  
**Existing fold:** [gui-lego/spec-condition-surface-vm.md](gui-lego/spec-condition-surface-vm.md) — **amend**, do not fork  
**Queue:** [gui-lego/menu-refactor-queue.md](gui-lego/menu-refactor-queue.md) P1 **Done** (Waves C–D landed)  
**Plans:** `tasks/condition-glance-plan.md` · `tasks/condition-glance-todo.md` (after specs; `/plan`)  
**Sibling programs (owner 2026-09-10 — three programs, three plans):**  
[derived-cook-map.md](derived-cook-map.md) · [shield-sheet-map.md](shield-sheet-map.md)  
**DESIGN-GATE:** UI + Player menus · Element / Status / Resource SSOTs for BE projection

**Owner mandate:** this is a **full** architecture-correct program (BE + shared element paint +
chart/realtime-capable pieces). Cheap FE-only / mute-chip / stub-title passes are **out of
acceptance**. Prior Condition sessions that shipped layout-without-modules are treated as defects,
not baselines to polish.

**Hot shield:** this program owns `sheet-hot-projection` (`shieldSummary` + `liveStatuses` + live bag).  
**Shield tab layers** (`sheet.shieldLayers`, **S1**) are filled in the **same** `ProjectSheet` call by sibling
`shield-stack-projection` — do not fork compose. Wave 1 Hot fixture proof is shared with `shield-sheet`.

**Ideal supersession:** Where [condition-glance-ideal.md](condition-glance-ideal.md) catalog or
composition still mentioned `phase-empty` for zero shield/status, “recharts only if kit fails,” or
deferred Hot projection, **owner locks Q2–Q6 on this map win**. Species missing is **fiction copy
under `actor-identity`** (keep the block) — not a separate `species-empty` module and not omit-whole-identity.

---

## Owner decisions (locked 2026-09-10)

| Id | Decision |
|---|---|
| **B1** | Map approved |
| **Q1** | Shared piece specs → `gui-lego/spec-*`; surface modules → `condition-glance/spec-*` |
| **Q2** | **Centralize element paint** — one SSOT file/group (`element-paint-ssot`) for all element UI (badge, HUD later, Derived rails). Maximum reuse; no per-surface color twins |
| **Q3** | **Omit when empty** — 0 shield / 0 live statuses → **do not mount** indicator pieces. No empty chrome that overwhelms the glance |
| **Q4** | **Use chart library** (`recharts`, locked tech-stack) for Standing radar / gauge animation paths; code-split; do not hand-roll a third grammar |
| **Q5** | Pieces must support **realtime data** via surface bus + host SignalR→fold refresh; charts animate on payload revision (not static mock fills) |
| **Q6** | **BE in scope** — serious Hot projection of shield + live statuses onto `/sheet` when session is Hot; cold remains honest omit. Not FE fixtures |
| **Q7** | **Amend** `spec-condition-surface-vm` / recipe / bindSurface — implement architecture correctly; no parallel god-TSX or second VM |
| **Q8** | Write **all** module specs now; implement later in Wave build order |
| **Q9** | **Three programs / three plans:** `condition-glance` · `derived-cook` (full harden) · `shield-sheet` (tab in; shared Hot) |
| **S1** | Shield layers ride **`sheet.shieldLayers`** beside `shieldSummary` (reject player `GET …/shields` as tab SSOT) |
| **S2** | Summary `elementId` = **front drain-order** layer (else null); optional `stacks` = layer count |
| **S3** | Hot path: Server **`ActorLiveState` bag** from Injector (prefer extend match/dump ingest; **ask before new HTTP**). `ProjectSheet` reads bag — never FE fixtures |

### Overturned prior ideal defaults

| Was | Now |
|---|---|
| Theme vfx + kit depth first; recharts only if kit fails | **recharts in scope** for animated gauges/radar; theme packs still own paint hex |
| Cold empty = empty piece chrome | **Omit mount** when zero/null |
| `/sheet` BE done / FE-only program | **BE Hot projection in program** |
| `element-badge` may wrap chip/`.el` ad hoc | **element-paint-ssot** first; badge consumes it |

---

## What this program is

Finish ActorSheet **Condition glance** end-to-end: Hub/sheet projection (including Hot live),
centralized element paint, recipe + fold + pieces with chart animation and realtime refresh —
architecture-correct GUI Lego, not a CSS patch.

---

## Ownership splits (binding)

| Concern | Owner | Must not |
|---|---|---|
| Element paint SSOT (css/paint/vfx tokens) | `element-paint-ssot` + theme packs | Private color maps in Condition / HUD / Derived |
| Shared pieces | `gui-lego/spec-*` | Mute twin chips |
| Condition surface layout / recipe wire | `condition-glance/spec-*` | God TSX |
| Hot `/sheet` liveStatuses + shield **summary** | `sheet-hot-projection` (this program) | Fabricate on cold UniqueActor; fork a second ProjectSheet |
| Hot shield **layers** (tab) | Sibling `shield-sheet` / `shield-stack-projection` | Use HUD AggregateByElement as tab SSOT |
| Fold / bindSurface | Amend existing gui-lego contracts | Second VM SSOT |
| Rail name / Lv + **role-badge** | ActorPanel summarize mounts `role-badge` | Mute `Lv · role` plain text; Condition does not duplicate role |

---

## Modules

| Module id | Responsibility | Depends on | Spec |
|---|---|---|---|
| `element-paint-ssot` | Centralized element color/CSS/vfx SSOT for all FE consumers | theme packs, element catalog | [gui-lego/spec-element-paint-ssot.md](gui-lego/spec-element-paint-ssot.md) |
| `theme-bind` | Resolve `themeRef` + `shieldThemeRef`; factories consume `themeResolved`/`vfxClass` | theme packs, `element-paint-ssot` | [gui-lego/spec-theme-bind.md](gui-lego/spec-theme-bind.md) |
| `fiction-copy` | Fiction-only titles; no `.md` / author notes in fold or drafts | — | [condition-glance/spec-fiction-copy.md](condition-glance/spec-fiction-copy.md) |
| `sheet-hot-projection` | BE: Hot session projects `liveStatuses` + `shieldSummary` onto `/sheet`; cold omit. Layers → sibling `shield-stack-projection` | resource/status/element Hub, match runtime; **shield-sheet** | [condition-glance/spec-sheet-hot-projection.md](condition-glance/spec-sheet-hot-projection.md) |
| `element-badge` | Shared elemental identity piece; consumes paint SSOT | `element-paint-ssot`, `theme-bind` | [gui-lego/spec-element-badge.md](gui-lego/spec-element-badge.md) |
| `phase-badge` | Fiction phase chip | theme packs (side) | [gui-lego/spec-phase-badge.md](gui-lego/spec-phase-badge.md) |
| `role-badge` | Fiction **role** Chip on ActorPanel rail — replaces mute `Lv · role` text | theme packs (side), `/sheet.roleLabel` | [gui-lego/spec-role-badge.md](gui-lego/spec-role-badge.md) |
| `shield-status` | Shield glance under `cond-hero`; **omit when null/0** | `theme-bind`, `sheet-hot-projection` | [gui-lego/spec-shield-status.md](gui-lego/spec-shield-status.md) |
| `progression-gauge` | XP/level gauge; recharts/kit animated fill; realtime revision | `theme-bind` | [gui-lego/spec-progression-gauge.md](gui-lego/spec-progression-gauge.md) |
| `pool-meter` | Resource track + CatalogIcon + theme/vfx; animated | `theme-bind` | [gui-lego/spec-pool-meter.md](gui-lego/spec-pool-meter.md) |
| `pool-radial` | HP radial (+ secondary shield ring when shield present); chart/animated; fixed box | `theme-bind` | [gui-lego/spec-pool-radial.md](gui-lego/spec-pool-radial.md) |
| `standing-radar` | Five-axis radar via **recharts**; tips on values; paint hex | — | [gui-lego/spec-standing-radar.md](gui-lego/spec-standing-radar.md) |
| `standing-bars` | Absolute Standing ints (precision path) | — | [gui-lego/spec-standing-bars.md](gui-lego/spec-standing-bars.md) |
| `status-glyph-strip` | Live glyphs via `StatusGlyph`; **omit entire strip when 0** | StatusGlyph, `sheet-hot-projection` | [gui-lego/spec-status-glyph-strip.md](gui-lego/spec-status-glyph-strip.md) |
| `actor-identity` | Species + phase-badge + element-badge*; omit mute twins | badges, `fiction-copy` | [gui-lego/spec-actor-identity.md](gui-lego/spec-actor-identity.md) |
| `cond-hero` | Radial + meters + optional shield-status slot (conditional mount) | pool-*, shield-status | [gui-lego/spec-cond-hero.md](gui-lego/spec-cond-hero.md) |
| `stand-row` | Radar + bars + optional status strip | standing-*, status strip | [gui-lego/spec-stand-row.md](gui-lego/spec-stand-row.md) |
| `condition-layout` | 2×2 landmark CSS; stand col ≥ chart; no surplus scroll | hosts | [condition-glance/spec-condition-layout.md](condition-glance/spec-condition-layout.md) |
| `recipe-wire` | Recipe + fold + register + bus/realtime revision; amend surface-vm | all above | [condition-glance/spec-recipe-wire.md](condition-glance/spec-recipe-wire.md) |

**Not modules of this program (siblings, now in scope elsewhere):**  
Derived cook → [derived-cook-map.md](derived-cook-map.md); Shield **tab** → [shield-sheet-map.md](shield-sheet-map.md).  
Glance still mounts `shield-status` from summary. **`role-badge` is in** — see module table.

---

## Build order

```text
Wave 1 — SSOT + seams + BE + copy
  element-paint-ssot → theme-bind
  sheet-hot-projection + sibling shield-stack-projection  (same Hot fixture)
  fiction-copy                  (parallel)

Wave 2 — shared pieces
  element-badge · phase-badge · role-badge · shield-status

Wave 3 — glance pieces + hosts
  progression-gauge · pool-meter · pool-radial
  standing-radar · standing-bars · status-glyph-strip
  actor-identity · cond-hero · stand-row
  ActorSummarize rail mounts role-badge (with Wave 2)

Wave 4 — surface
  condition-layout → recipe-wire
```

---

## Success criteria (program-level)

- Element colors come from **one** paint SSOT; dark is never mute text.
- Rail **role-badge** is a themed Chip — never mute `Lv · role` plain text alone.
- Hot sheet shows real shield + live statuses when present; cold omits those modules.
- Standing radar uses **recharts**; gauges animate on revision; SignalR updates re-fold without piece fetch.
- Fiction titles only; no `definitions.md`.
- No surplus horizontal scroll; layout landmarks tested.
- Architecture: recipe + fold + bus only; amend surface-vm; no god TSX.
- Queue P1 Done only when Waves 1–4 + BE Hot proof + rail role-badge land.

---

## Coverage-gap register (strengthen 2026-09-10)

| Id | Gap | Disposition |
|---|---|---|
| CG-C1 | Hot layers vs glance summary parity | **Closed** — **S1** same ProjectSheet; hot-projection + stack-projection |
| CG-C2 | Summary `elementId` / stacks policy | **Closed** — **S2** |
| CG-C3 | Injector→Server Hot path unnamed | **Closed in spec** as **S3** live bag (ask before new HTTP) |
| CG-C4 | Recipe missing shield slot | **Closed** — `condition-console.json` + recipe-wire |
| CG-C5 | SignalR event name | **Closed in spec** — preferred `ActorLiveStateChanged` (implement ask-first) |
| CG-C6 | Shield tab Empty wells | **Deferred to** `shield-sheet` (not Condition wiring) |

---

## Explicitly out

| Out | Why |
|---|---|
| Cheap FE-only paint without BE Hot path | Owner Q6 |
| Mute element chips / private color tables | Q2 |
| Mute plain-text role on rail | Defect — use `role-badge` (now **in** scope) |
| Empty shield/status chrome when count is 0 | Q3 |
| Hand-rolled third gauge grammar when recharts fits | Q4 / buy-before-build |
| Piece-level fetch / SignalR inside a piece | GUI Lego |
| Implementing Derived cook inside this plan | Sibling `derived-cook` (own plan) |
| Implementing Shield **tab** stack UI here | Sibling `shield-sheet` (own plan); shared Hot summary + **S1** layers |
| `SPEC.md` / bare `tasks/plan.md` | Parallel programs |
