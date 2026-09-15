# aptitude-sheet — todo

**Plan:** [aptitude-sheet-plan.md](aptitude-sheet-plan.md)  
**Map:** [docs/architecture/aptitude-sheet-map.md](../docs/architecture/aptitude-sheet-map.md)  
**Specs:** [docs/architecture/aptitude-sheet/](../docs/architecture/aptitude-sheet/)  
**Coverage:** 13/13 modules tasked; Accept locks G1–G13 closed (see plan Spec coverage)

---

## Phase 0 — Truth + UniqueCreature write (Wave 0)

- [x] **AS-0.1** Stale-doc amend (D5) — `stale-doc-amend`
  - Accept: actor-sheet aptitudes-tab, class-system allocation-surface, guide aptitudes, menu queue P4 claim aptitude-sheet; no active commander-only-v1 claims
  - Accept (G1): `aptitude-sheet-ideal.md` hand-off points at map + `docs/architecture/aptitude-sheet/`
  - Verify: `rg -n "commander-scope|commander scope only|UniqueCreature.*out of scope" docs/` (struck/superseded OK)
  - Files: docs listed in `spec-stale-doc-amend.md`
  - Deps: None
  - Scope: S

- [x] **AS-0.2** UniqueCreature GET/POST + FE hooks — `unique-allocate`
  - Accept: `GET /api/aptitudes/unique/{instanceId}` returns persisted shares + budget/leftover (no EffectiveUnique); `POST .../unique/allocate` saves UniqueCreature; overspend 409; empty legal; ownership checks
  - Accept: FE `useUniqueAptitudes` / `useSaveUniqueAptitudes` (names flexible)
  - Accept (G2): after POST, `LoadAllocation(UniqueCreature, instanceId)` equals saved shares; unique hooks unused by Mode C commander path; shares/budget/leftover are `long` *(reworded 2026-09-15 per owner ruling: floating-point allowed)*
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

- [x] Unique GET/POST curl green; empty UniqueCreature legal
- [x] Scoped SignalR on unique allocate; FE species typing present
- [x] D5 `rg` clean; icons + posture packs load
- [x] Review Phase 0 before lawn wire

---

## Phase 1 — Lawn + presentation contracts (Wave 1)

- [x] **AS-1.1** Injector Bound UniqueCreature apply — `unique-lawn-wire`
  - Accept: Bound Hot resolve = commander + UniqueCreature(instanceId); generals stay commander+species; fetch via **unique GET only** (S4); empty unique legal
  - Accept (G6): Bound unique sharing a species id with a general still resolves `commander+UniqueCreature`, never empire species shares
  - Verify: Core unit green (`SpeciesAllocationSourceTests` — 4 new Bound-priority cases, 11/11 total). Injector/live probe **not run in this session** — `FusionRpg.Injector` needs a game-dir interop build this sandbox has no `FUSIONRPG_GAME_DIR`/MelonLoader install for; `.\scripts\guard-secondary-no-unity.ps1` not run for the same reason. Owner: build + deploy-play + live Bound-allocate probe still owed before calling the Injector half proven, not just written.
  - Files: `SpeciesAllocationSource.cs` (Core: optional Bound-priority branch, backward-compatible — sole production caller is `CheatState.cs`), `RpgClient.cs` (new `RefreshUniqueAptitudesAsync`: enumerates Bound instanceIds off `MatchHost.Runtime.ToSnapshot().Bindings`, one `GET /api/aptitudes/unique/{instanceId}` per id, wholesale-replaces the cache; wired at StartAsync/Reconnected/`aptitudes.allocation.reload`), `CheatState.cs` (`_uniqueAllocations` cache + `ApplyUniqueAllocations` + `ResolveBoundInstanceId` via the SAME `MatchHost` ptr→binding index `UniqueBoundLoadout` already uses), `CheatCommandRunner.cs` (reload wiring)
  - Deps: AS-0.2, AS-0.3
  - **Cross-linked (2026-09-13):** built to unblock `actor-hub-and-combat-power-solid-fixing`'s T12 (`lawn-aptitude-parity`) and T19 bullet 3 (`prove-hub-combat`), per owner instruction not to leave a real cross-program block deferred. Both revisited same session — see their own entries for what's now closed vs. what still needs the live probe.
  - **⚠ LIVE PROBE RUN 2026-09-13 — found an incomplete trigger set; see AS-1.1b.** The live probe this
    entry owed was finally run against a real game+server. Result: the resolve logic is **correct**
    (a Bound specimen with a cache entry resolves `bonusAtk 1330` / `bonusAtkContribs
    "aptitude.Might:Flat:1330"`, written through to the live Unity field). But the fetch cadence is
    incomplete: `RefreshUniqueAptitudesAsync` runs only at StartAsync / Reconnected /
    `aptitudes.allocation.reload`, and the cache is keyed by **currently Bound** instanceIds — so a
    specimen allocated *before* it is deployed is in no fetch's key set and its allocation never
    loads. `allocate → deploy` silently produced an unbuffed actor; `deploy → allocate` worked. Spec
    gap, not an implementation slip: `spec-unique-lawn-wire.md` named only those three triggers while
    `aptitude-sheet-map.md`'s module row said "on reload/**bind**". Spec amended 2026-09-13 with the
    full 4-trigger cadence table + a required cadence test.
  - Scope: M

- [x] **AS-1.1b** Bind-edge refresh for the unique allocation cache — `unique-lawn-wire` (fix)
  - **Why:** AS-1.1's cadence omits the one trigger where the cache's KEY SET moves. Confirmed live
    2026-09-13 (see AS-1.1 above and `DESIGN-GATE.md` §4). Third instance of this bug class in this
    codebase — now also codified as `DESIGN-GATE.md` §2.16.
  - [x] Accept: a specimen entering `Bound` triggers the refresh — `MatchHost.ConsumeLastBound`'s edge
    now also calls the new `RpgClient.TriggerBoundAptitudeRefresh()`, fire-and-forget, right alongside
    the existing `UniqueBoundLoadout.TryApply` call (`e4e2548`).
  - [x] Accept (**the regression that matters**): **order-independent**, proven live 2026-09-14 —
    `POST /api/aptitudes/unique/allocate` (Might 141) on a Roster specimen, THEN
    `POST /api/unique/actors/{id}/deploy`. Live read via `debug.board-stats`:
    `attack:2721, attackDamage:2721` (ptr `2887A74BB40`, instanceId `5dd73a05c09a4bc6afcebbf1acd5e847`,
    level 148) — vanilla baseline is `attack:1`. This is the EXACT `allocate → deploy` order that
    produced `bonusAtk 0` / `attack 1` in the 2026-09-13 incident this task exists because of.
  - [x] Accept: all 4 cadence triggers covered —
    `tests/FusionRpg.Injector.Tests/UniqueAptitudeRefreshCadenceTests.cs` (7/7): source-scan for
    triggers 1-3 (StartAsync, SignalR reconnect, `aptitudes.allocation.reload`) and a behavioral test
    for trigger 4 (the new bind edge, fire-and-forget, never awaited on the hot path).
  - [x] Accept: no redundant refetch storm — `RpgClient.TriggerBoundAptitudeRefresh` coalesces
    concurrent binds into at most one extra round trip after an in-flight fetch completes (since
    `RefreshUniqueAptitudesAsync` always reads the CURRENT Bound set live, never a snapshot taken at
    trigger time); proven by a real-HTTP behavioral test (10 rapid triggers → far fewer than 10 actual
    fetch runs).
  - [x] Verify: `dotnet test tests/FusionRpg.Injector.Tests --filter "FullyQualifiedName~UniqueAptitudeRefreshCadenceTests"`
    (7/7) + full `tests/FusionRpg.Injector.Tests` (38/38, no regressions) + `guard-actor-hub.ps1` +
    `guard-secondary-no-unity.ps1` (both green); then the live probe above (persisted-state read alone
    was explicitly NOT accepted as closing this, per the spec's own standard).
  - Files: `src/FusionRpg.Injector/Match/MatchHost.cs` (hook the bind edge),
    `src/FusionRpg.Injector/RpgClient.cs` (`TriggerBoundAptitudeRefresh` + coalescing state machine),
    `tests/FusionRpg.Injector.Tests/UniqueAptitudeRefreshCadenceTests.cs`
  - Deps: AS-1.1
  - Scope: S

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

- [x] Bound unique lawn reflects UniqueCreature after allocate+reload — **both orders now proven
      live.** `deploy → allocate` (2026-09-13): `bonusAtk 1330`, `bonusMaxHp 1110` from
      Vigor+Fortitude, board read `attack 223 hp 1410 maxHp 1410`. `allocate → deploy` (2026-09-14,
      after AS-1.1b): Might 141 allocated on a Roster specimen, then deployed — board read
      `attack 2721 attackDamage 2721` (vanilla baseline `attack 1`) on the live Unity entity. Both
      orders pass — this box now ticks for real
- [x] Fold fixtures show leftover + decision in-band
- [x] Piece landmark tests green; HTML drafts present
- [x] Review Phase 1 before hosts

---

## Phase 2 — Hosts (Wave 2)

- [x] **AS-2.1** ActorSheet role gate Mode A/C — `host-role-gate`
  - Accept: creature → UniqueCreature draft/Confirm; commander → commander; thin RecipeMount; shell mirror Confirm/**Cancel** (S8); no CreatureType write from sheet
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

- [x] Map success criteria checklist all met (or explicitly deferred items only A6/E6 keep-aligned) — FE A/B/C + presets proven; AS-1.1 Injector wire written 2026-09-13, live probe still owed (see AS-1.1)
- [x] Guards: DAL green (this stream); secondary-no-unity N/A for FE-only
- [x] Live: Bound unique after Activate/allocate shows UniqueCreature — **stale, closed**: this
      checkbox predates AS-1.1's 2026-09-13 live probe and AS-1.1b's 2026-09-14 order-independent
      fix, both already `[x]` above. AS-1.1b's own live evidence IS this exact criterion: `allocate
      → deploy` on a real Roster specimen reads back `attack:2721` (vanilla baseline `attack:1`) via
      `debug.board-stats` — a Bound unique correctly showing its UniqueCreature-sourced aptitude
      bonus after Activate/allocate, in the harder of the two trigger orders. Left unticked by an
      oversight when AS-1.1b closed; corrected here 2026-09-15.
- [x] Menu queue P4 Aptitudes evidence noted on map/queue — **closed 2026-09-15**:
      `docs/architecture/gui-lego/menu-refactor-queue.md`'s P4 row updated from a bare "claimed" note
      to "**Done**" (matching the P0/P1/P1b rows' own convention) with the real evidence citation (FE
      A/B/C + presets proven, AS-1.1/AS-1.1b Injector wire live-proven 2026-09-14).

---

## Follow-ups (non-blocking)

- [ ] Owner retune soft max presets / default abs max in balance pass
- [ ] Exact lucide keys per aptitude (content polish)
- [ ] Lawn species glance door (Wave 2 ideal)
- [ ] posture-balance piece (A6)
- [ ] Keep-aligned rematerialize on level-up (E6 deferred)
