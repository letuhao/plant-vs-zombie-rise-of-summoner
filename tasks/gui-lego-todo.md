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

## Queue (after P0 React)

- [ ] P1 Condition
- [ ] P2 Creatures filter chrome
- [ ] P3 Relics / Commanders
- [ ] P4 other rail layers + remaining Actor tabs
