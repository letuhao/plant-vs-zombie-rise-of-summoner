# shield-sheet — todo



**Plan:** [shield-sheet-plan.md](shield-sheet-plan.md)  

**Map:** [docs/architecture/shield-sheet-map.md](../docs/architecture/shield-sheet-map.md)



## Phase A — Wave 1 (Hot layers)



- [x] **SS-A0** Confirm Injector→live-bag fill includes shield instances (coordinate **CG-A4b**; do not fork transport)

  - Accept: Hot bag carries GetShields-shaped data usable by ProjectSheet

  - Deps: CG-A4 / CG-A4b (or land together)

- [x] **SS-A1** Add `ActorShieldLayerDto` + `ActorSheetDto.ShieldLayers`; FE `aura.ts` twin

  - Accept: fields per stack-projection spec; magnitudes `long`

- [x] **SS-A2** ProjectSheet fills `shieldLayers` from live bag / `ShieldRuntime.GetShields` (same call as summary)

  - Accept: drain order; cold empty; parity `sum(layers)==Totals==summary`

  - Deps: CG-A4 live bag (or land together); SS-A0

  - Verify: assert layers on shared fixture **`ActorSheetHotLiveStateTests`** (owned by condition-glance CG-A4)

- [x] **SS-A3** Document curl cold/Hot proof for layers + summary
  - [docs/runbook/actor-sheet-hot-live-state.md](../docs/runbook/actor-sheet-hot-live-state.md)



### Checkpoint A

- [x] Hot sheet layers+summary agree; cold empty

- [x] `ActorSheetHotLiveStateTests` asserts layers + summary parity



## Phase B — Wave 2 (pieces)



- [x] **SS-B1** Author `docs/design/gui-lego/pieces/shield-stack-bar.html` + `shield-layer-inspect.html`

- [x] **SS-B2** Factories for stack-bar + layer-inspect; element-paint-ssot (`resolveElementPaint` / CG-A1); pending ≠ three Empty wells

  - Accept: drain-order segments; **broken layer keeps empty slot**; priority fiction labels (aura/skill/innate); **no three permanent Empty wells**

- [x] **SS-B3** Omni-rows join helper (cook shield omni allow-list) — Pending when missing



### Checkpoint B

- [x] Draft-exists gates pass; order = drain order

- [x] Broken empty-slot + priority labels; no permanent Empty wells



## Phase C — Wave 3 (surface)



- [x] **SS-C1** `foldShieldSurfaceVm` — pending / hot-empty / 1–3 segments; closed bus events

  - Accept unit matrix: **Pending** vs **Hot-empty** vs **N-segments** (mirror surface-vm spec)

- [x] **SS-C2** Author `shield-console.json` (+ assembled HTML preferred); register recipe

  - Accept recipe slots: `shield-stack-bar` + `shield-layer-inspect` + **omni region**; mount omni from **SS-B3**

- [x] **SS-C3** Replace CatalogTabs Empty-layer `ShieldTab` with thin RecipeMount host

  - Accept: no permanent “Empty layer” product chrome; Ward string absent

  - Verify: `npm test -- --run ShieldTab foldShield`



### Checkpoint C — program Done

- [x] Map success criteria met

- [x] Pending / Hot-empty / N-segments matrix green

- [x] Recipe mounts stack-bar + layer-inspect + omni

- [x] Queue P1b Done evidence

- [x] Glance `shield-status` still omit-correct (condition-glance)

- [x] Shared fixture name: `ActorSheetHotLiveStateTests` (CG-A4 / SS-A2)



## Deferred (D8 — not Done gate)



- [ ] **SS-D8a** Segment regenText when runtime exposes rate

- [ ] **SS-D8b** Cascade lines / apply-outcome inspect (P3)



## Follow-ups (non-blocking)



- [ ] Debug-only GET /shields if needed — never tab SSOT

