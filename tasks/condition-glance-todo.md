# condition-glance — todo

**Plan:** [condition-glance-plan.md](condition-glance-plan.md)  
**Map:** [docs/architecture/condition-glance-map.md](../docs/architecture/condition-glance-map.md)

## Phase A — Wave 1

- [x] **CG-A1** Implement `resolveElementPaint` + wire theme packs (`element-paint-ssot`)
  - Accept: single import path; dark not mute-only; unit test per catalog element id
  - Accept (paint SSOT): redirect or delete private paint table in `actorHudDisplayTokens.ts` (must-migrate); HUD consumers call `resolveElementPaint` — or explicit follow-up checkbox if HUD lands after A1
  - Verify: `npm test -- --run elementPaint` (or equivalent); `rg` no private HUD paint SSOT table after migrate
  - Files: `web/.../features/gui-lego/themes/`, packs sync; HUD tokens redirect
  - Cross-link: derived-cook **DC-6** uses this same path — no twin
- [x] **CG-A2** Amend `bindSurface` / factories for `themeResolved` + `shieldThemeRef` (`theme-bind`)
  - Accept: themed pieces consume resolved paint hex for SVG
  - Deps: CG-A1
- [x] **CG-A3** Fiction-only titles in fold + drafts (`fiction-copy`)
  - Accept: grep no `definitions.md` / author notes in Condition payloads
- [x] **CG-A4** Server `IActorLiveStateStore` + ProjectSheet reads bag for `liveStatuses` + `shieldSummary` (**S2**/S3)
  - Accept: cold empty; Hot from bag; `elementId` front drain-order; optional `stacks`
  - Default transport: extend dump ingest **or** internal POST (reversible)
  - **Named fixture (owner):** `ActorSheetHotLiveStateTests` — asserts summary + statuses; SS-A2 also asserts `shieldLayers` on same fixture
  - Coordinate: same compose adds `shieldLayers` per [shield-sheet-todo.md](shield-sheet-todo.md) SS-A1/A2
  - Verify: Server.Tests cold + Hot fixture; curl
- [x] **CG-A4b** Injector pushes live statuses + shields into the live bag (RPG runtime → transport → store)
  - Accept: after Injector Hot session, Server bag non-empty **without FE fixtures**; GetShields + live statuses represented
  - Deps: CG-A4 store exists
  - Verify: Server bag read in Hot fixture / curl after Injector push
- [x] **CG-A5** FE host invalidate `["actorSheet", id]` on preferred `ActorLiveStateChanged` (or documented interim)
  - Accept: re-fold bumps `revision`; pieces never fetch
  - Deps: CG-A4
- [x] **CG-A5b** Server emits `ActorLiveStateChanged` when live bag updates (Condition owns emit; Shield rides same invalidate)
  - Accept: hub method/event name documented; test or script assert on emit with store write
  - Deps: CG-A4
  - Verify: hub/test assert — not a hard gate on dump path

### Checkpoint A
- [x] Cold/Hot sheet curl documented — [docs/runbook/actor-sheet-hot-live-state.md](../docs/runbook/actor-sheet-hot-live-state.md)
- [x] Paint + theme-bind unit green
- [x] `ActorSheetHotLiveStateTests` green (summary + layers via SS-A2)
- [x] Injector fill + SignalR emit proven or script-asserted

## Phase B — Wave 2

- [x] **CG-B1** Author HTML drafts: `element-badge`, `phase-badge`, `role-badge`, `shield-status`
- [x] **CG-B2** Factories + register pieces; mount shield-status only when summary mountable (Q3)
- [x] **CG-B3** `ActorSummarize` mounts `role-badge` — no mute `Lv · role` sole presentation
  - Verify: `npm test -- --run ActorSummarize role-badge`

### Checkpoint B
- [x] Rail role themed; Condition omit empty shield/status

## Phase C — Wave 3

- [x] **CG-C1** Drafts + factories: `progression-gauge`, `pool-meter`, `pool-radial` (secondary shield ring)
  - Accept (Q5): gauges/meters **animate or visibly update on `revision` bump** — not static mock fills
- [x] **CG-C2** `standing-radar` via **recharts** (code-split) + `standing-bars` + `status-glyph-strip` (omit when 0)
  - Accept (Q5): radar/bars update on host `revision` invalidate — not static mock fills
- [x] **CG-C3** Hosts: `actor-identity`, `cond-hero`, `stand-row`

### Checkpoint C
- [x] Landmark tests; no surplus horizontal scroll fixture
- [x] Revision bump refreshes gauges/radar (host invalidate path)

## Phase D — Wave 4

- [x] **CG-D1** `condition-layout` CSS landmarks (2×2; stand col ≥ chart)
- [x] **CG-D2** `recipe-wire` — amend fold/surface-vm; recipe shield slot; closed bus; queue P1 Done evidence
  - Accept: closed bus catalog tested; FE recipe stays in sync with [condition-console.json](../docs/design/gui-lego/recipes/condition-console.json) shield → `shield-status` slot
  - Verify: ConditionTab RecipeMount-only; contract tests; bus catalog test

### Checkpoint D — program Done
- [x] Map success criteria all met
- [x] Menu queue P1 → Done
- [x] Sibling Hot parity with shield-sheet proven (`ActorSheetHotLiveStateTests`)

## Follow-ups (non-blocking)

- [ ] Owner may merge internal live-state POST into dump ingest later
- [x] HUD adoption of paint SSOT — `actorHudDisplayTokens` redirects to `resolveElementPaint` (CG-A1)
- [x] Ideal note: [condition-glance-ideal.md](../docs/architecture/condition-glance-ideal.md) verified ~279 lines on disk — no restore needed
