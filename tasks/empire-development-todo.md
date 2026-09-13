# Task list: empire-development

Companion checklist to `tasks/empire-development-plan.md` — see that file for full reasoning,
acceptance criteria, verification commands, and file lists per task. Check items here as they land.

## Phase 0: External prerequisite — `deployment-hierarchy`'s `corpse-cache` trio

*(Skip entirely if `deployment-hierarchy`'s own plan has already built this — verify with
`grep -rn "rpg_corpse_cache" src/FusionRpg.Data` first.)*

- [ ] 0.1 `corpse-cache` — schema + death/wipe move (core mechanism now; anti-fraud phase-gate guarantee
  BLOCKED on an item-program ask, tracked not blocking)
- [ ] 0.2 `cache-decay-void` — clock, amended for durable world-map places (never voids `world_sector`/`world_lane`)
- [ ] 0.3 `cache-field-access` — reachability, amended for `world_sector`/`world_lane`
- [ ] 0.3b `cache-field-access` — claim into legion cargo (world-map claim write; closes a real SPEC gap
  this session's audit found — `cargo-fate` never owned this verb; fixed at the spec level in
  `spec-cache-field-access.md` §2a, needs Task 1.1)

### Checkpoint: Phase 0 complete
- [ ] Combined test run green
- [ ] `guard-dal.ps1` / `guard-actor-hub.ps1` green
- [ ] Reviewed with human (only blocks Phase 2's `cargo-fate` task, not the rest of Phase 1/2 — note
  0.3b itself also needs Task 1.1 from Phase 1)

## Phase 1: Wave 1 — four independent modules (parallelizable)

- [ ] 1.1 `legion-cargo` — slot+weight cargo overlay, all-members capacity
- [ ] 1.2a `sector-storage` — `StructureKind.ItemStorage` + capacity axis
- [ ] 1.2b `sector-storage` — table + capture hook (no-op capture transfer)
- [ ] 1.3a `relic-item-kind` — KindSpec + enum + mint arm
- [ ] 1.3b `relic-item-kind` — seedsmith `droptablegen` gains `relic` entryKind + append operation
- [ ] 1.3c `relic-item-kind` — content authoring (regenerate, never hand-edit)
- [ ] 1.4 `wonder-structure` — vocabulary + `StructureDef` facet + `Validate`

### Checkpoint: Phase 1 complete
- [ ] All four modules' tests pass independently
- [ ] `guard-dal.ps1` / `guard-actor-hub.ps1` / `guard-single-writer.ps1` green
- [ ] `audit-overflow.py` / `audit-magic-numbers.py` clean
- [ ] Reviewed with human

## Phase 2: Wave 2 — four modules

- [ ] 2.1 `cargo-transfer` — deposit/withdraw/legion-to-legion, corrected weight resolution
- [ ] 2.2 `cargo-fate` — legion-death cargo cache (sector + lane cases) — **needs Task 1.1 + Phase 0**
  (corrected: not Task 2.1 — the map's own wave-2-after-wave-1 framing was coarser than the actual code
  dependency; `cargo-fate` never calls `cargo-transfer`)
- [ ] 2.3a `wonder-effect-empire` — faction-input plumbing + SUM
- [ ] 2.3b `wonder-effect-empire` — SQL persistence + 3 secondary call sites
- [ ] 2.3c `wonder-effect-empire` — upkeep term (owner-requested addition)
- [ ] 2.4 `wonder-build-flow` Core-side — fields, cap scan, admission check, **rubble/ironwork wiring fix**

### Checkpoint: Phase 2 complete
- [ ] All Phase 2 tests pass
- [ ] `guard-dal.ps1` / `guard-actor-hub.ps1` green
- [ ] Manual/integration check: legion cargo load→transfer→death-cache works end-to-end
- [ ] Manual/integration check: Sector-scope and Empire-scope Wonder both affect real loam yield
- [ ] Reviewed with human

## Phase 3: Final integration

- [ ] 3.1 `wonder-build-flow` Data-side — relic reachability pre-check + spend, same-transaction

### Checkpoint: Phase 3 complete — full program integration
- [ ] End-to-end: mint → carry/deposit → spend → build → observe production + upkeep effect
- [ ] Full suite green: Core.Tests, Data.Tests, Guard.Tests
- [ ] All boundary guards green
- [ ] `audit-overflow.py` / `audit-magic-numbers.py` clean
- [ ] Ready for owner review / live deploy-play smoke test

## Deferred, named future work (not a task — carried for a future notification-consumer session)

- [ ] `spec-sector-storage.md` — sector storage reachability flip on capture, silent to the player
- [ ] `spec-cargo-fate.md` — legion-death cargo cache, silent to the player
- [ ] `spec-wonder-build-flow.md` — Wonder completion + `wonder.cap-reached` refusal, silent to the player

## Open, non-blocking content/balance decisions

- [ ] Relic drop weight/rate per `SourceKind` table (Task 1.3c) — ship a low default, flag for balance pass
- [ ] `EmpireWonderUpkeepRateMilli` real value (Task 2.3c) — ship provisional `50`, flag for balance pass
