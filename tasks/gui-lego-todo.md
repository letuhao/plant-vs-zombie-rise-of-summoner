# gui-lego — todo

**Plan:** [gui-lego-plan.md](gui-lego-plan.md)

## Wave A — initial design pack

- [x] Ideal + map
- [x] Composition + VM + theme-packs specs
- [x] Recipe + piece HTML drafts
- [x] Initial theme demos + tasks

## Wave B — harden into binding standard (2026-09-09)

- [x] DESIGN-GATE / decisions / authoring / specs / surfaces / themes / queue

## Wave 0 — shared FE runtime (P0)

- [x] types + themeRegistry + pieceRegistry + recipeRegistry
- [x] bindSurface (`$bindArray`, instanceIdTemplate, lifecycleOverlays)
- [x] RecipeMount + createSurfaceBus
- [x] Fixture tests (array, overlay, `>` DOM)

## Wave C — Derived consumer

- [x] C1 foldDerivedSurfaceVm + cook under features/gui-lego
- [x] C2 Derived pieces + CatalogIcon + paint gauges
- [x] C3 switch DerivedTab; delete ui/actor/derived/*
- [x] C4 contract + e2e SSOT retarget + queue P0 done

## Wave D — P0 audit fixes + coverage

- [x] formatDerivedMagnitude total|delta; fold totals unsigned
- [x] Recipe phasePayload binds; empty dock phase-empty + count
- [x] bind validate before overwrite; DerivedTab refetch/OR/contrib
- [x] fold/bind/RecipeMount/piece/DerivedTab tests green

## Wave E — Derived chrome trim + chip presentation

- [x] Show unchanged `role=switch`; tools on primary cook rail
- [x] Drop identity / `.console-hd`; empty copy without debug label
- [x] Variant accent + Lucide glyphs + VFX; resource theme packs
- [x] Contract / DerivedTab / fold / design SSOT synced

## Queue (after P0 React)

- [ ] P1 Condition
- [ ] P2 Creatures filter chrome
- [ ] P3 Relics / Commanders
- [ ] P4 other rail layers + remaining Actor tabs
