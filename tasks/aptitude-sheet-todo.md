# aptitude-sheet — todo

**Plan:** [aptitude-sheet-plan.md](aptitude-sheet-plan.md)  
**Map:** [docs/architecture/aptitude-sheet-map.md](../docs/architecture/aptitude-sheet-map.md)  
**Specs:** [docs/architecture/aptitude-sheet/](../docs/architecture/aptitude-sheet/)  
**Coverage:** 13/13 modules tasked; Accept locks G1–G13 closed (see plan Spec coverage)

---

## Phase 0 — Truth + UniqueDemon write (Wave 0)

- [x] **AS-0.1** Stale-doc amend (D5) — `stale-doc-amend`
  - Accept: actor-sheet aptitudes-tab, class-system allocation-surface, guide aptitudes, menu queue P4 claim aptitude-sheet; no active commander-only-v1 claims
  - Accept (G1): `aptitude-sheet-ideal.md` hand-off points at map + `docs/architecture/aptitude-sheet/`
  - Verify: `rg -n "commander-scope|commander scope only|UniqueDemon.*out of scope" docs/` (struck/superseded OK)
  - Files: docs listed in `spec-stale-doc-amend.md`
  - Deps: None
  - Scope: S

- [x] **AS-0.2** UniqueDemon GET/POST + FE hooks — `unique-allocate`
  - Accept: `GET /api/aptitudes/unique/{instanceId}` returns persisted shares + budget/leftover (no EffectiveUnique); `POST .../unique/allocate` saves UniqueDemon; overspend 409; empty legal; ownership checks
  - Accept: FE `useUniqueAptitudes` / `useSaveUniqueAptitudes` (names flexible)
  - Accept (G2): after POST, `LoadAllocation(UniqueDemon, instanceId)` equals saved shares; unique hooks unused by Mode C commander path; shares/budget/leftover are `long` (no float magnitudes)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter UniqueAptitude`; curl GET/POST
  - Files: `AptitudeEndpoints.cs`, RpgStore aptitudes, `web/.../lib/bus/*`, Server.Tests
  - Deps: None (parallel with 0.1)
  - Scope: M

- [x] **AS-0.3** Scoped `AptitudesUpdated` + FE species type honesty — `aptitudes-live-bus`
  - Accept: broadcasts include `scope` + key (`instanceId` / `speciesId`); FE invalidates matching keys; legacy `{ playerId }` fallback; `AptitudesState` includes nested commander GET `species` (S10)
  - Accept: unique allocate / commander allocate / species respec all emit scoped payload
  - Accept (G3): shared broadcast helper is the sole emitter for commander / species / unique / **preset activate** (contract Ready for AS-3.2)
  - Verify: Server.Tests AptitudesUpdated; hub-provider invalidate tests
  - Files: `AptitudeEndpoints.cs`, `SpeciesBuildEndpoints.cs`, `hub-provider.tsx`, `types.ts`
  - Deps: AS-0.2 (unique route exists to broadcast)
  - Scope: M

- [x] **AS-0.4** Catalog `icon` field — `catalog-icons`
  - Accept: every primary aptitude has `icon` in catalog; loader accepts; missing icon does not crash tile
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter AptitudeCatalog`
  - Files: `data/tuning/aptitude-catalog.v{n}.json`, catalog loader
  - Deps: None
  - Scope: S

- [x] **AS-0.5** Posture theme packs + `vfx.select` — `posture-theme-packs`
  - Accept: three posture packs registered; non-null `vfx.select`; tiles/bands can bind `themeRef`
  - Accept (G5): `bucket.aptitude` remains Derived-bucket only (not posture chrome)
  - Verify: theme registry load / pack smoke
  - Files: `docs/design/gui-lego/themes/packs/posture-*.json` (or project pack path), registry
  - Deps: None
  - Scope: S

### Checkpoint 0

- [x] Unique GET/POST curl green; empty UniqueDemon legal
- [x] Scoped SignalR on unique allocate; FE species typing present
- [x] D5 `rg` clean; icons + posture packs load
- [x] Review Phase 0 before lawn wire

---

## Phase 1 — Lawn + presentation contracts (Wave 1)

- [ ] **AS-1.1** Injector Bound UniqueDemon apply — `unique-lawn-wire`
  - Accept: Bound Hot resolve = commander + UniqueDemon(instanceId); generals stay commander+species; fetch via **unique GET only** (S4); empty unique legal
  - Accept (G6): Bound unique sharing a species id with a general still resolves `commander+UniqueDemon`, never empire species shares
  - Verify: Injector/Core unit; live probe after allocate+AptitudesUpdated; `.\scripts\guard-secondary-no-unity.ps1`
  - Files: `RpgClient.cs`, `CheatState.cs`, Hot resolve path
  - Deps: AS-0.2, AS-0.3
  - Scope: M

- [x] **AS-1.2** Piece HTML drafts — `aptitude-pieces` (drafts)
  - Accept: drafts for scope-chip, leftover-gauge, allocate-decision-strip, preset-entry, posture-band, aptitude-tile, aptitude-inspect, species-build-chrome, aptitudes-layout, preset-distribution-chart (donut)
  - Accept: leftover in-band; Cancel fiction; no ConfirmDialog in Mode B chrome sketch (S2/S8)
  - Verify: files under `docs/design/gui-lego/pieces/`; owner visual skim at Checkpoint 1 (not a start-block)
  - Deps: AS-0.4, AS-0.5 (icons/packs inform drafts)
  - Scope: M

- [x] **AS-1.3** Piece React factories — `aptitude-pieces` (factories)
  - Accept: pure presenters; leftover-gauge promotes LeftoverBar; donut uses recharts; bus intents only
  - Accept (G4): `aptitude-tile` glyph comes from catalog `icon` join (lucide/CatalogIcon path)
  - Verify: `npm test -- --run aptitude` (landmarks)
  - Files: `web/.../pieces/` or gui-lego factories
  - Deps: AS-1.2
  - Scope: M

- [x] **AS-1.4** Fold + `aptitudes-console` recipe — `aptitudes-surface-vm`
  - Accept: modes A/B/C fixtures; slices leftover, decision, autoAssign, presetEntry, bands, inspect; closed bus; Activate not on `aptitude.confirm`
  - Accept (G7): inspect payload includes `fedFamilies[].displayName` when catalog edges exist (A3); pieces render those names
  - Verify: fold unit goldens Mode A/B/C
  - Files: fold cook, `docs/design/gui-lego/recipes/aptitudes-console.json`
  - Deps: AS-1.3, AS-0.3
  - Scope: M

### Checkpoint 1

- [ ] Bound unique lawn reflects UniqueDemon after allocate+reload
- [x] Fold fixtures show leftover + decision in-band
- [x] Piece landmark tests green; HTML drafts present
- [x] Review Phase 1 before hosts

---

## Phase 2 — Hosts (Wave 2)

- [x] **AS-2.1** ActorSheet role gate Mode A/C — `host-role-gate`
  - Accept: creature → UniqueDemon draft/Confirm; commander → commander; thin RecipeMount; shell mirror Confirm/**Cancel** (S8); no DemonType write from sheet
  - Accept (G8): scope-chip title/fiction matches Mode A vs C
  - Accept: Activate path = `POST /api/aptitude-presets/activate` only when presets wired (Done gated on AS-3.5)
  - Verify: `npm test -- --run AptitudesTab ActorPanel`
  - Files: `AptitudesTab.tsx`, `ActorPanel.tsx`, AptitudesPage collapse if in scope
  - Deps: AS-1.4, AS-0.2
  - Scope: M

- [x] **AS-2.2** Species Mode B host collapse — `species-host`
  - Accept: AptitudesLayer mounts shared console Mode B; SpeciesBuildPanel not product SSOT; price on strip; **ConfirmDialog retired** (S2); free first-override/revert via Confirm; free allocate stays retired
  - Accept (G9): lawn generals still EffectiveSpeciesAllocation (unchanged BE — regression assert or documented path)
  - Accept: Activate path = activate API only when presets wired (Done gated on AS-3.5)
  - Verify: `npm test -- --run AptitudesLayer species-build`; no respec-confirm dialog in tree
  - Files: `AptitudesLayer.tsx`, `SpeciesBuildPanel.tsx`, `useSpeciesBuild.ts`
  - Deps: AS-1.4
  - Scope: M

### Checkpoint 2

- [x] UniqueActor no longer posts commander allocate
- [x] Mode B priced Confirm without dialog; Mode C still works
- [x] Review Phase 2 before presets

---

## Phase 3 — Presets + auto-assign (Wave 3)

- [x] **AS-3.1** Preset store + CRUD + favour GET + materialize — `aptitude-preset-api` (core)
  - Accept: player-scoped library (item-loadout discipline); Save sum ‰==1000 (E5); D13 materialize leftover legal (E2); soft max default **32** (E8); favour GET `sharesPermille` (S1); empty `{}` when no plan
  - Accept (G10): materialize refuses when `lo > hi` with named reason; library survives process restart
  - Verify: Data.Tests + Server.Tests AptitudePreset; `.\scripts\guard-dal.ps1`
  - Files: `RpgStore` new tables, endpoints, tuning keys, Core materialize helper
  - Deps: AS-0.3 (SignalR shape for later activate)
  - Scope: M

- [x] **AS-3.2** Transactional Activate endpoint — `aptitude-preset-api` (activate)
  - Accept: `POST /api/aptitude-presets/activate` sets active + Mode A unique / Mode C commander / Mode B **existing** priced respec in one txn; no half-active; scoped broadcast via AS-0.3 helper; no level-up autorespec (E6)
  - Verify: Server.Tests activate success/failure rollback; Mode B price path reused
  - Files: preset endpoints, species-build respec call-in
  - Deps: AS-3.1, AS-0.2, AS-0.3
  - Scope: M

- [x] **AS-3.3** Auto-assign draft rules — `aptitude-auto-assign`
  - Accept: Even / posture / active-preset / species-favour; draft only; favour via GET; empty favour refuse + Even (S7); `(long)budget *` math (S6); Mode C no favour
  - Accept (G11): `active-preset` fill uses shared D13 materialize (leftover legal, no redistribute)
  - Verify: Core and/or FE unit tests AutoAssign
  - Files: fill helper, bus `aptitude.autoAssign` wire in fold/host
  - Deps: AS-3.1, AS-1.4
  - Scope: S

- [x] **AS-3.4** Preset nested console — `aptitude-preset-console`
  - Accept: gallery/editor/donut; New seed favour permille or Even; Apply-to-draft; Activate → activate API only; depth ≤3; Save blocked unless sum 1000
  - Accept (G12): editor exposes dual abs + ‰ constraints per row (D13), not ‰-only
  - Verify: `npm test -- --run aptitude-preset`; HTML surface draft present
  - Files: recipe `aptitude-preset-console`, fold, pieces mount
  - Deps: AS-3.1, AS-3.2, AS-1.3
  - Scope: M

- [x] **AS-3.5** Wire presets + auto-assign into hosts
  - Accept (G13a): `preset.open` works on Modes A/B/C
  - Accept (G13b): Activate txn works on Modes A/B/C via activate API only
  - Accept (G13c): Mode B shows price before Activate
  - Accept: auto-assign dirties draft only (Confirm commits)
  - Verify: AptitudesTab / AptitudesLayer integration tests; manual Confirm vs Activate
  - Files: host-role-gate, species-host thin handlers
  - Deps: AS-2.1, AS-2.2, AS-3.2, AS-3.3, AS-3.4
  - Scope: M

### Checkpoint 3 — program Done

- [x] Map success criteria checklist all met (or explicitly deferred items only A6/E6 keep-aligned) — FE A/B/C + presets proven; AS-1.1 Bound UniqueDemon lawn wire remains injector
- [x] Guards: DAL green (this stream); secondary-no-unity N/A for FE-only
- [ ] Live: Bound unique after Activate/allocate shows UniqueDemon
- [ ] Menu queue P4 Aptitudes evidence noted on map/queue

---

## Follow-ups (non-blocking)

- [ ] Owner retune soft max presets / default abs max in balance pass
- [ ] Exact lucide keys per aptitude (content polish)
- [ ] Lawn species glance door (Wave 2 ideal)
- [ ] posture-balance piece (A6)
- [ ] Keep-aligned rematerialize on level-up (E6 deferred)
