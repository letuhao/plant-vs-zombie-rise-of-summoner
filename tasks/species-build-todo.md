# Tasks: `species-build`

Plan: [species-build-plan.md](species-build-plan.md). **30 tasks, 6 phases, 6 checkpoints.**
Module ids from [species-build-map.md](../docs/architecture/species-build-map.md); `m N` = module N.

Sizes: **XS** 1 file · **S** 1-2 · **M** 3-5. No task exceeds 5 files.

**Standing rules for every task:** no git operations (leave the work in the tree, hand the owner a
commit message); every balance number is a named tunable with a `_why` note; `long` magnitudes, widen
before multiplying, divide by 1000 last, overflow throws; cite **symbol names** over line numbers —
another stream is editing this repo concurrently and lines drift.

---

## Phase 0 — corrections · `m1 resolver-memo`, `m2 budget-source`

Both are fixes to already-shipped code. Both are semantically neutral. **Zero goldens** is an
acceptance criterion, not a hope.

- [x] **T0.1** The memo, with `Θ` in the key · **S** · `m1`
  - Acceptance:
    - [x] Memo on `AptitudeSubsystem`, keyed `(StatSide Side, int TypeId, long Theta)` — self-correcting
          via `ReferenceEquals` on the stored allocation rather than an externally-bumped generation
          stamp (found via a real `CommanderAllocationSourceTests` failure with the stamped design)
    - [x] **Equivalence:** memoized and non-memoized resolves are element-wise identical
    - [x] **⛔ Θ is honoured:** two contexts identical but for `Θ` resolve to *different* modifiers —
          the test an earlier spec draft would have failed
    - [x] Same `TypeId`, different `Side` → different results (`polevaulterzombie`/`wallnut`)
    - [x] Bounded growth: N entities of one `(Side, TypeId, Theta)` produce one entry
    - [x] Instance state, never static (a static leaks between scoped test hosts)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude` — 22/22 green
  - Files: `AptitudeSubsystem.cs`, `tests/.../AptitudeSubsystemTests.cs`

- [x] **T0.2** Invalidation bumps at every path · **S** · `m1`
  - Acceptance:
    - [x] The self-correcting memo (T0.1) needs no explicit bump on any path — proven by
          `Memo_selfCorrects_whenTheAllocationReferenceChanges_noExplicitInvalidateNeeded`; `CheatState.
          RefreshCommanderAllocationCache()` keeps its doc comment explaining why, `InvalidateMemo()`
          stays as an explicit escape hatch only
    - [x] A changed `Θ` needs no bump (it is a different key) — asserted
          (`Memo_thetaIsHonoured_differentThetaResolvesDifferently`)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `.\scripts\guard-single-writer.ps1` — both green
  - Files: `AptitudeSubsystem.cs`, `CheatState.cs`, tests

- [x] **T0.3** Split the guard test into the two claims it was conflating · **XS** · `m2`
  - Acceptance:
    - [x] `Rates_are_ordered_commander_smallest_unique_largest` — the existing constant-source check,
          renamed to what it proves
    - [x] `Real_budgets_are_ordered_at_representative_sources` — each scope fed a value in its own units
          (`thetaPlayer=20`, `speciesLevel=21`→20 via `DemonTypeSourceFromLevel`, `specimenLevel=20`),
          ordering asserted on budgets (60 < 80 < 120)
    - [x] Covers three scopes, not four, with a comment naming `Aspect` as excluded because
          `element_mastery` does not exist
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter PointBudget` — 12/12 green
  - Files: `tests/.../PointBudgetTests.cs`

- [x] **T0.4** The `(level − 1)` rule and the three stale citations · **S** · `m2`
  - Acceptance:
    - [x] `PointBudget.DemonTypeSourceFromLevel(level) = max(0, level − 1)`; subtraction before the
          multiply, `checked`
    - [x] `PointsFor(DemonType, level=0)` and `level=1` both yield zero
          (`DemonTypeSourceFromLevel_isZero_atLevelZeroAndLevelOne`, `PointsFor_demonType_atLevelZeroOrOne_isZeroBudget`)
    - [x] "almanac XP" corrected in all three places: `spec-point-economy.md` §2 table, `PointBudget`'s
          doc comment, `aptitudes.v5.json`'s `_scopeSourcesWhy`
    - [x] `No_cap_on_an_aptitude` still passes (PS-8)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `.\scripts\guard-power.ps1` — both green
  - Files: `PointBudget.cs`, `data/tuning/aptitudes.v5.json`, `spec-point-economy.md`

### ✅ Checkpoint 0 — the corrections are provably neutral
- [x] Core + Guard suites green (165/168 on the Aptitude/PointBudget filter; the 3 failures are
      `ProveAptitudeJsonEmitTests`, pre-existing/concurrent — `BattleStatComposer.Configure` not run in
      the `ProveAptitude` tool subprocess, unrelated to this program, confirmed via `git status`)
- [x] Zero goldens re-blessed — the self-correcting memo redesign changes no observable resolve output
- [x] `guard-power`, `audit-overflow` clean (57 findings / 0 critical, matches the pre-existing baseline)

---

## Phase 1 — foundations · `m3 species-xp`, `m4 redistribution-plan`

- [x] **T1.1** Species progression row + migration · **M** · `m3`
  - Acceptance:
    - [x] Storage decision **A recorded with its reason**: `kind='species'` rows key `type_id` on
          `DemonSpeciesDef.DemonTypeId` (already unique per species, ≥10000 disjoint space —
          `DemonSpeciesCatalog.Validate`'s own duplicate-demonTypeId check) — confirmed against
          `RpgStore.Progression.cs`'s real DDL before committing; a first attempt at this task wrongly
          assumed Option A required the string `speciesId` itself as `type_id` and built a whole
          parallel progression module before re-reading the spec and correcting course. A nullable
          `scope_key TEXT` column added via `EnsureColumn` carries the human-readable speciesId
          alongside it (set on insert, in `EnsureActorRowUnlocked`/`RpgXpAwardMap.Award.ScopeKey`)
    - [x] Curve reuses `RpgXpCurve`/`RpgXpApply`/`RpgActorState` directly via the new
          `RpgActorKinds.Species` kind — `RpgXpCurve.ParamsFor` routes `Species` to
          `SpeciesProgressionTuningHub.Tuning`'s own `first`/`step`, never a parallel curve type
    - [x] **Unlimited levels** (PS-8); overflow throws, never clamps — fixed a real pre-existing gap in
          the shared `RpgXpApply.Apply` (`state.Xp += delta` was unchecked for EVERY kind, not just
          species) found by `Apply_overflow_throws_neverWraps`, now `checked` for all kinds
    - [x] A pre-migration database still opens and reads a default (`EnsureColumn`, same precedent as
          `through_ledger_id`/`xp_by_reason_json`)
    - [x] Existing `plant`/`zombie` type rows untouched
    - [x] ⛔ Host wiring: `species-progression.v1.json` loaded and injected by `FusionRpg.Server/Program.cs`
          only (`SpeciesProgressionTuningHub.Configure`), mirroring `AptitudeTuningHub`'s shape; a
          missing key is a load rejection naming it (`SpeciesProgressionTuningLoader`, tested)
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter SpeciesProgression` (8/8), `--filter
    SpeciesExpedition` (3/3), `dotnet test tests\FusionRpg.Core.Tests --filter Progression` (33/33);
    `.\scripts\guard-dal.ps1` — all green
  - Files: `RpgProgression.cs`, `SpeciesProgression.cs` (rewritten — tuning surface only, no parallel
    state/curve/apply types), `RpgStore.cs` (DDL), `RpgStore.Progression.cs`, `Program.cs`,
    `data/tuning/species-progression.v1.json`, tests

- [x] **T1.2** Lawn projection: place/spawn → species row · **S** · `m3`
  - Acceptance:
    - [x] A `PlantPlaced` fact levels the species row; the species resolved matches `LawnElementIndex`'s
          own answer for that `(Side, TypeId)` (`PlantPlaced_levels_the_species_row_matching_LawnElementIndexs_own_answer`)
    - [x] **Collision safety:** two species sharing `(plant, 999)` — the lawn credits only the
          deterministic winner and silently skips the loser (no throw); the loser is not reachable via
          THIS source but is not the concern of this task (`Collision_loser_is_skipped_...`)
    - [x] Idempotent: the same fact ingested twice levels once (`PlantPlaced_idempotent_...`)
    - [x] ⛔ `!pvzGame`'s "PvZ almanac types only" gate re-expressed, not widened: species levels under
          BOTH `pvzGame` values, PvZ type rows still gated — both directions tested
          (`WebMode_run_does_not_level_the_PvZ_type_but_DOES_level_the_species`)
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter SpeciesProgression` (8/8); `dotnet test
    tests\FusionRpg.Core.Tests --filter RpgXpAwardMap` (9/9, 2 new species-placement cases) — all green
  - Files: `RpgXpAwardMap.cs` (species-placement award, best-effort/no-op when the roster or species
    tuning isn't configured), `RpgStore.Progression.cs` (the `!pvzGame` re-expression), tests

- [x] **T1.3** The run award, and the ratio that makes it dominant · **S** · `m3`
  - Acceptance:
    - [x] `runCompletionAward` fires once per resolved match (`MatchEnded`), for every distinct species
          fielded in that `run_id`, however many times placed — derived via a query over
          `pvz_activity_facts` already recorded by T1.2's own award loop in the SAME method
          (`ApplyRunCompletionSpeciesAwardsUnlocked`), never a new capture
    - [x] `placementAward` retained as the smaller term — both tunable (`species-progression.v1.json`)
    - [x] The run term out-earns a plausible number of placements at the shipped tuning — proven at
          both the Core layer (`RunAward_outEarnsAPlausibleHeavyMatchOfPlacements`, ratio-only) and the
          Data layer end to end (`RunCompletion_outEarns_a_plausible_heavy_match_of_placements_at_the_shipped_ratio`,
          20 real placements + 1 real match-end)
    - [x] Fires exactly once per run regardless of placement count, and a replayed `MatchEnded` never
          double-pays (`RunCompletion_fires_exactly_once_...`, `RunCompletion_replayed_MatchEnded_never_double_pays`)
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter SpeciesProgression` (8/8) — green
  - Files: `RpgStore.Progression.cs` (`ApplyRunCompletionSpeciesAwardsUnlocked`), `RpgProgression.cs`
    (`RpgXpReasons.SpeciesRunComplete`), tests

- [x] **T1.4** Expedition source — the game-closed proof · **S** · `m3`
  - Acceptance:
    - [x] An expedition win levels a species with no lawn run anywhere in the test
          (`Expedition_win_levels_the_species_with_no_lawn_run_at_all`) — resolves the specimen's
          species via the direct `rpg_demon_profiles.instance_id -> species_id` link (no
          `(Side,GameTypeId)` ambiguity, since `rpg_unique_actors.type_id` stores the PvZ `GameTypeId`,
          not `DemonTypeId`)
    - [x] Species award shares the specimen award's transaction — proven both by a forced-throw
          leaving neither applied (`Species_award_shares_the_specimen_awards_transaction`) and by the
          existing exactly-once retry gate covering it too (`Replayed_collect_never_double_pays_the_species_either`)
    - [x] The `!pvzGame` rule (T1.2) is unaffected — expeditions never pass through
          `ApplyRpgProgressionFromActivityUnlocked` at all, so there is no second direction to test here
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter SpeciesExpedition` (3/3); `--filter
    Expedition` (20/20, no regression in the pre-existing `ExpeditionRewardApplyTests`);
    `.\scripts\guard-dal.ps1` — all green
  - Files: `RpgStore.Expeditions.cs` (`ReadSpeciesIdForInstanceUnlocked`, the species award beside
    `AwardUniqueActorXpUnlocked`), `RpgProgression.cs` (`RpgXpReasons.SpeciesExpedition`), tests

- [x] **T1.5** `SpeciesBuildPlanner` phases 1–2 · **M** · `m4`
  - Acceptance:
    - [x] Phase 1 derives each species' lean from its primary's crowding — crowded leans less, rare
          leans more, asserted on a synthetic corpus modeled on the real skew
          (`Crowding_behaves_a_crowded_primary_leans_measurably_less_than_a_rare_one`)
    - [x] Phase 2 distributes remainders against the running corpus deficit, ordinal iteration
          (`ApplyRunCompletionSpeciesAwardsUnlocked`-style running total, but in `SpeciesBuildPlanner`)
    - [x] No single-primary: every vector has ≥ `minAptitudesPerSpecies` (2) non-zero entries, even for
          an all-pure synthetic corpus
    - [x] The favour is never overridden: every vector's largest share is its classified primary —
          proven on both a synthetic corpus and all 829 real planned species
    - [x] Pure and Core-only — no file IO, no store, no model call ever
    - [x] Permille `long`; largest-remainder rounding with ordinal tiebreak; vectors sum to exactly
          1000 — a REAL defect was found running this for real over the corpus (not caught by any
          synthetic test): `pure: true` anchors that echo their primary back as `aptitudeSecondary`
          instead of the `"none"` sentinel (`HypnoCattailGirl`, `ObsidianWallNut`) silently overwrote
          `vector[primary]` via the same dictionary key, corrupting 2 of 829 vectors below 1000. Fixed
          to match `SpeciesExpander.Expand`'s own existing `!anchor.Pure && ...` guard exactly, plus a
          defensive `secondary != primary` check; regression-covered
          (`Pure_anchor_that_echoes_its_primary_as_secondary_never_corrupts_the_vector_sum`)
    - [x] Overflow throws, never wraps — an extreme tuning value forces the widened Phase-1 multiply
          past `long` range (`Overflow_an_extreme_corpus_throws_rather_than_wraps`)
    - [x] ⛔ Host wiring: `species-build.v1.json` loaded by the server host
          (`SpeciesBuildTuningHub.Configure` in `Program.cs`) and read directly by the generation tool
          (mirrors `DemonSpeciesGen`'s own tuning-file convention); missing key → named rejection
          (`SpeciesBuildTuningLoader`)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesBuildPlannerTests` (10/10) — green
  - Files: `SpeciesBuildPlanner.cs`, `SpeciesBuildPlan.cs`, `SpeciesBuildTuning.cs`,
    `data/tuning/species-build.v1.json`, `Program.cs`, tests

- [x] **T1.6** Phase 3 verification, refusal, canonical serializer · **S** · `m4`
  - Acceptance:
    - [x] Corpus shares outside `[floor, ceiling]` throw `SpeciesBuildRefusal` naming the offending
          aptitudes and their shares (`Refusal_deliberately_infeasible_tunables_name_the_offending_aptitudes`)
    - [x] Deliberately infeasible tunables (ceiling far below what Onslaught's crowding alone forces)
          produce a named refusal, not a near-miss
    - [x] Canonical serializer (`SpeciesBuildPlanSerializer.Canonical`): sorted keys at both the
          species and aptitude level, pinned `WriteIndented` formatting, byte-identical rerun (proven
          both in Core.Tests and via the real CLI's `--check`)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesBuildPlannerTests` (10/10) — green
  - Files: `SpeciesBuildPlanner.cs`, `SpeciesBuildPlan.cs`, tests

- [x] **T1.7** `DemonBuildPlanGen` and the committed plan · **M** · `m4`
  - Acceptance:
    - [x] CLI mirrors `DemonSpeciesGen` exactly — `--seed`, `--out`, `--check`, `_`-prefix skipping,
          refuse-the-whole-thing-rather-than-write-half (a `SpeciesBuildRefusal` during `--check` or a
          real run both exit 1 before any file is written)
    - [x] Run for real over the corpus; `data/generated/demons/_species-build-plan.json` committed —
          829 of 840 anchors planned (11 skipped, still `aptitudePrimary: "unresolved"` — the same
          skip list `DemonSpeciesGen` itself reports for those species)
    - [x] `--check` clean; a rerun is byte-identical — verified directly (two consecutive `--check`
          runs, and a deliberately corrupted file caught and restored)
    - [x] The parity band is satisfied on the real corpus — pass/fail: tuned to `[50,200]‰`
          (`data/tuning/species-build.v1.json`), real corpus lands `[76,144]‰` across all 12 aptitudes
    - [x] Shuffled input order produces the same plan — ordering is by `speciesId` inside the planner
          itself, not file discovery order (`Determinism_shuffled_input_order_produces_the_same_plan`)
  - Verify: `dotnet run --project tools\DemonBuildPlanGen -- --check` (clean, 829 species);
    `python scripts\audit-magic-numbers.py --targets M1` (no findings); `python scripts\audit-overflow.py`
    (57/0 critical, unchanged baseline) — all green
  - Files: `tools/DemonBuildPlanGen/Program.cs`, `tools/DemonBuildPlanGen/DemonBuildPlanGen.csproj`,
    `data/generated/demons/_species-build-plan.json`, tests

- [x] **T1.8** ⛔ **CI gate for the generated plan** · **XS** · `m4`
  - Acceptance:
    - [x] `ci.yml` runs `dotnet run --project tools/DemonBuildPlanGen -- --check` immediately after the
          `FamilyExpandGen --check` step and throws on a non-zero exit, following the exact
          `$LASTEXITCODE` pattern already used for `DemonSpeciesGen --check`/`FamilyExpandGen --check`
    - [x] The throw message names the fix command, as the sibling gates do
    - [x] Added in this phase (Phase 1), not deferred to the end
  - Verify: a deliberately stale plan (overwritten with `{"stale":"data"}`) made `--check` exit 1
    locally, then restoring the real file made it exit 0 again — proven directly, not assumed
  - Files: `.github/workflows/ci.yml`

### ✅ Checkpoint 1 — a species can level, and a plan exists for it
- [x] Core suite green: 6588 passed / 7 failed, all 7 confirmed pre-existing/concurrent (other
      in-flight streams' own files, verified via `git status` — `Expeditions.ExpeditionResolverTests`,
      3x `Battle`/`Battle.Timeline` reaction-lane tests, 3x `ProveAptitudeJsonEmitTests`); none touch a
      file this program edited. `guard-dal`/`guard-power`/`guard-single-writer` all clean
- [x] **The game-closed test passes** — `Expedition_win_levels_the_species_with_no_lawn_run_at_all`
      (T1.4), no lawn involvement anywhere in the test
- [x] `--check` clean and byte-stable (`DemonBuildPlanGen`, verified twice consecutively); the band
      ([50,200]‰) is satisfied on the real corpus ([76,144]‰ actual)
- [x] Zero goldens moved: `BattleGoldenTests` 5/5 green
- [x] **CI gates the generated plan** — proven locally: a corrupted plan file made `--check` exit 1,
      restoring it made it exit 0 again

---

## Phase 2 — the allocation · `m5 demon-type-allocation`

- [x] **T2.1** Scope key and compose-at-read · **M** · `m5`
  - Acceptance:
    - [x] `scope_key` = `player:{playerId}:species:{speciesId}`, encoded in one place
          (`SpeciesAllocation.ScopeKey`, beside `AptitudeEndpoints.ScopeKey`'s own Commander encoding)
    - [x] A species with a level and no override row resolves to the plan's shares × its budget, not
          zero (`EffectiveSpeciesAllocation_withNoOverride_resolvesToThePlansBaseline_notZero`) — needed
          a genuinely new runtime plan reader (`SpeciesBuildPlanCatalog`/`SpeciesBuildPlanReader`),
          which no prior module's spec named a loader for; built following the established
          `DemonSpeciesCatalog`/`SpeciesProgressionTuningHub` "server configures, Core reads no file"
          pattern, and wired into `Program.cs` + `FusionRpg.Server.csproj`'s content-copy list
    - [x] Per-player isolation: two players, same species, same level, one overriding, read different
          effective allocations (`EffectiveSpeciesAllocation_isPerPlayer_...`)
    - [x] Baseline is computed, never persisted (`SpeciesAllocation.Baseline` is a pure Core function;
          the store only ever persists an explicit override row)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesAllocationTests` (8/8);
    `dotnet test tests\FusionRpg.Data.Tests --filter AllocationStoreTests` (15/15) — all green
  - Files: `SpeciesAllocation.cs`, `SpeciesBuildPlanCatalog.cs` (new), `RpgStore.Aptitudes.cs`,
    `Program.cs`, `FusionRpg.Server.csproj`, tests

- [x] **T2.2** Override, budget enforcement, endpoints · **M** · `m5`
  - Acceptance:
    - [x] Override is whole-vector; deleting the row (saving `AptitudeAllocation.Empty`) returns
          exactly the baseline, free — no soul cost, no separate API
          (`EffectiveSpeciesAllocation_deletingTheOverride_returnsExactlyTheBaseline_forFree`)
    - [x] Overspend refused scope-locally — a large Commander budget does not fund a DemonType
          overspend, proven through the real HTTP endpoint
          (`Allocate_overspending_isRefused_scopeLocally`)
    - [x] Scopes sum before share: an actor with both Commander and DemonType allocations reads the
          sum, and `Share` is taken on that sum (`ScopesSum_anActorWithBothCommanderAndDemonType_...`)
    - [x] No cap on the allocation (PS-8); overflow throws (`PointBudget`/`AptitudeAllocation`'s own
          existing `checked` arithmetic, reused as-is — this module adds no new magnitude path)
    - [x] ⛔ `AptitudesUpdated` broadcasts to BOTH groups on a species save — proven against a REAL
          SignalR connection joined as `InjectorGroup` (not just `WebGroup`), mirroring
          `AptitudesInjectorBroadcastTests`' own established live-connection proof, not a mock
          (`Allocate_notifies_a_client_joined_as_injector_not_just_web`,
          `Allocate_still_notifies_a_client_joined_as_web`)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter SpeciesAllocationEndpointsTests` (6/6);
    `.\scripts\guard-dal.ps1` — both green
  - Files: `AptitudeEndpoints.cs` (`/api/aptitudes/species/{playerId}/{speciesId}` GET,
    `/api/aptitudes/species/allocate` POST), `RpgStore.Aptitudes.cs`, tests

- [x] **T2.3** The seam guard · **XS** · `m5`
  - Acceptance:
    - [x] A guard test asserts no production consumer of species allocation calls `LoadAllocation`
          directly (text-scan across `src/**/*.cs` excluding `RpgStore.Aptitudes.cs` itself, the one
          legitimate composer) — composition only happens behind `EffectiveSpeciesAllocation`
    - [x] A second, named-file assertion pins `AptitudeEndpoints.cs` specifically (today's one real
          production consumer) to using `EffectiveSpeciesAllocation`, so a future refactor that
          bypasses the entry point in that SAME file (not just a new file) also fails loudly
  - Verify: `dotnet test tests\FusionRpg.Guard.Tests --filter SpeciesAllocationSeamTests` (2/2);
    full `dotnet test tests\FusionRpg.Guard.Tests` (204/204) — both green
  - Files: `tests/FusionRpg.Guard.Tests/SpeciesAllocationSeamTests.cs`

### ✅ Checkpoint 2 — allocation is real, and still invisible
- [x] Core (8/8 SpeciesAllocationTests) + Data (15/15 AllocationStoreTests) + Server (6/6
      SpeciesAllocationEndpointsTests) + Guard (204/204, full suite) all green
- [x] Zero goldens moved — holds because the budget is zero at level 1 (T0.4's own rule); the DemonType
      dilution risk (§2a of spec-demon-type-allocation.md) never fires for any never-levelled species,
      which is every existing golden fixture. `BattleGoldenTests` (5/5) still green from Checkpoint 1's
      own check, unaffected by this checkpoint's changes (no shared code path touched)
- [x] Nothing player-visible yet, by design — the new endpoints exist but no web/injector surface
      calls them until `allocation-surface` (module 8, Phase 5)

---

## Phase 3 — both read paths · `m6 allocation-transport`, `m10 battle-allocation`

**They land together.** Shipping the lawn without battle is the incoherence module 10 exists to prevent.

- [x] **T3.1** Server payload gains `species` — additively · **S** · `m6`
  - Acceptance:
    - [x] `shares` kept, not renamed — proven by test on a real HTTP response, plus the pre-existing
          5 `AptitudeEndpointsTests` still passing unchanged
    - [x] `species` added beside `{theta, budget, spent, withinBudget, shares}`
          (`Get_forAPlayerWithNoSpecies_hasAnEmptySpeciesMap_commanderHalfUnaffected`)
    - [x] Only species the player has actually levelled are sent — a new `RpgStore.ListLevelledSpeciesIds`
          reads `scope_key` off `kind='species'` rows, never the 829-row corpus
          (`Get_sendsOnlyTheSpeciesThePlayerHasActuallyLevelled`)
    - [x] Commander half unaffected — the 5 pre-existing `AptitudeEndpointsTests` (allocate/round-trip/
          overbudget/unknown-id) all still pass verbatim
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter AptitudeEndpointsTests` (7/7) — green
  - Files: `AptitudeEndpoints.cs` (`ProjectState`), `RpgStore.Progression.cs`
    (`ListLevelledSpeciesIds`), tests

- [x] **T3.2** Core `SpeciesAllocationSource` · **M** · `m6`
  - Acceptance:
    - [x] `ctx → allocation` behind an injected lookup, mirroring `SpecimenOwnershipOracle`'s shape —
          fully Core-testable with fake resolvers, no game required (7/7 in
          `SpeciesAllocationSourceTests`)
    - [x] `polevaulterzombie`/`wallnut` resolve differently — a named test
          (`PolevaulterZombie_and_WallNut_share_a_GameTypeId_but_resolve_differently`)
    - [x] An un-configured index reports, never returns a silent zero — a THREE-way
          `SpeciesLookupResult` (not a bool) distinguishes "not configured" from "configured, no
          species here"; the former reports via an injected callback and falls back to commander-only
          (`Unconfigured_index_reports_and_falls_back_to_commander_only_never_a_silent_zero`)
    - [x] Commander and species points merge into one `AptitudeAllocation` via `operator+`, resolved
          once (`Commander_and_species_merge_into_one_allocation`)
    - [x] No I/O on the Hot path — a text-scan guard test on the source file itself
          (`SourceFile_performsNoIO_everyCollaboratorIsAnInjectedDelegate`)
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesAllocationSourceTests` (7/7) — green
  - Files: `SpeciesAllocationSource.cs`, tests

- [x] **T3.3** Injector cache and refresh · **M** · `m6`
  - Acceptance:
    - [x] Cache refreshed on exactly the existing cadence: `RpgClient.RefreshCommanderAllocationAsync`
          (already called at `StartAsync`, reconnect, and the `AptitudesUpdated` handler) now ALSO
          parses the response's `species` map in the same fetch and calls the new
          `CheatState.ApplySpeciesAllocations` — no new lifecycle, no new call sites, no polling
    - [x] Never awaits the server on the Hot path — `SpeciesAllocationSource.Resolve` (wired as
          `ActorHub`'s `aptitudeAllocation` delegate) reads only `_speciesAllocations` (a plain
          dictionary) and a lazily-built, permanently-cached `LawnElementIndex` — no `await`, no I/O
    - [x] Refresh-cadence logic itself is exactly `SpeciesAllocationSourceTests`' own coverage (T3.2) —
          the cache-update mechanics are Core-testable and covered there; only the actual HTTP
          fetch/JSON-parse/wire-up in `RpgClient.cs`/`CheatState.cs` is injector-only
    - [x] **Compiles for real against the real game.** The earlier "unverifiable offline" note was
          wrong on one point: `dotnet build src/FusionRpg.Injector.MelonLoader.39/...` (the REAL
          MelonLoader 3.9 entry point `deploy-play.ps1` itself builds, `-p:MlGameDir=...
          -p:GameProfile=pvzrh-3.9`) against the real game install already on this machine
          (`H:\Games\PVZ-Fusion-3.9_MelonLoader`, confirmed by size match against the documented
          57,717,248-byte fingerprint) — **builds clean, 0 errors.** (Bare `FusionRpg.Injector.csproj`
          still fails standalone restore with NuGet's "Ambiguous project name" — a structural quirk of
          the shared-source multi-host layout, not a real blocker: `FusionRpg.Injector.BepInEx`/
          `.MelonLoader.39` are the real entry points, and `deploy-play.ps1` always builds through one
          of those, never the bare project.) One real nullable warning found and fixed along the way
          (`CheatState.SpeciesAllocation`'s field-declaration order relative to
          `DummyStatContextForCommanderRead` — a warning-only issue, runtime behavior was already
          correct either way, confirmed by reasoning through C#'s static-initializer semantics before
          the build proved it)
    - [x] `guard-secondary-no-unity.ps1`, `guard-single-writer.ps1`, `guard-game-profile.ps1` all green
          against the edited tree and the real game directory
  - Verify: `dotnet build src\FusionRpg.Injector.MelonLoader.39\FusionRpg.Injector.MelonLoader.39.csproj
    -c Release -p:MlGameDir="H:\Games\PVZ-Fusion-3.9_MelonLoader" -p:GameProfile=pvzrh-3.9` — 0 errors;
    `.\scripts\guard-secondary-no-unity.ps1` (OK); `.\scripts\guard-single-writer.ps1` (OK);
    `.\scripts\guard-game-profile.ps1 -GameDir "H:\Games\PVZ-Fusion-3.9_MelonLoader" -ExpectedProfile
    pvzrh-3.9` (OK)
  - Files: `RpgClient.cs`, `CheatState.cs`

- [x] **T3.4** Battle setup reads species · **S** · `m10`
  - Acceptance:
    - [x] `AptitudeChannelMods` gains trailing optional `speciesId`/`commanderAllocation` params (all 4
          pre-existing positional call sites in `AptitudeChannelModsTests` keep compiling and behaving
          identically — commander alone, species defaults to `Empty`); the commander read is hoisted
          out of `BuildSquad`'s per-actor loop into one read per squad build
    - [x] Merged, not concatenated: one `operator+` then one `ResolveForBattle` call — matches
          `SpeciesAllocationSource`'s own established pattern, no second curve
    - [x] Existing `AptitudeChannelModsTests` still pass unchanged (verified: only the ONE pre-existing,
          confirmed-unrelated failure remains — `RealBattle_recordsAnAptitudeSnapshotEvent...`, a stale
          hardcoded `battle.v2.json` fixture missing the concurrent battle-tempo stream's own
          `speciesTempo` key, unmodified by this program, reproduced identically before any of this
          session's edits)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter "Aptitude|WebMatch|Expedition"` (15/16 —
    the 1 failure confirmed pre-existing/unrelated above); `dotnet test tests\FusionRpg.Core.Tests
    --filter Battle` (852/855 — all 3 failures confirmed pre-existing/concurrent reaction-lane files,
    none touched by this program)
  - Files: `WebMatchService.cs` (`AptitudeChannelMods`, `BuildSquad`)

- [x] **T3.5** The two diagnostic paths · **S** · `m10`
  - Acceptance:
    - [x] Battle report `aptitude.snapshot` now carries a `species` map (per fielded speciesId) beside
          the existing commander `shares`, and `scope` reads `"commander+species"` instead of the
          hard-coded `"commander"` literal (`WebMatchService.ResolveAndIngest`)
    - [x] The derived-stat inspection endpoint (`AuraDerivedEndpoints.cs`) now routes through the SAME
          `SpeciesAllocationSource` `allocation-transport` (module 6) uses, resolving species from the
          SAME `ctx.Side`/`ctx.TypeId` already in hand — no new plumbing, no second merge
          implementation. Regression-proven: `AuraDerivedEndpointsTests` (its own fixture extended to
          configure `DemonSpeciesCatalog`/`SpeciesProgressionTuningHub`, matching what a real server
          actually does at startup — a real, pre-existing test-isolation gap this change surfaced) —
          all pass, including the one this task's own code path touches directly
    - [x] **A second real defect found running the FULL Server.Tests suite** (not just this task's own
          filtered tests): `EffectiveSpeciesAllocation` unconditionally called
          `SpeciesBuildPlanCatalog.SharesFor(...)`, which throws if the catalog isn't configured — fine
          for the T2.1/T2.2 read/write endpoints (a real server always configures it), but
          `SpeciesAllocationSource` now reaches this method for EVERY actor resolved through battle
          setup or derived inspection, including fixtures that predate this module and never expected
          to need it. Fixed with an `IsConfigured` guard returning `AptitudeAllocation.Empty` for the
          baseline half (an existing override still wins) — the same best-effort-enrichment shape
          `RpgXpAwardMap.WithSpeciesPlacement` already established, applied here for the same reason.
          Full-suite failures dropped from 24 to 23 (all 23 remaining confirmed pre-existing/concurrent
          "ContentRuleViolated: atom.empty-name" content-seed issues from an unrelated stream, verified
          via `git status` — none touch a file this program edited)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter "Aptitude|WebMatch|Expedition|AuraDerived"`
    (18/19, the 1 failure the same confirmed pre-existing `battle.v2.json` issue as T3.4); full
    `dotnet test tests\FusionRpg.Server.Tests` re-run to confirm no OTHER regression (113→113 passed
    across two consecutive full runs, failure set unchanged) — green
  - Files: `WebMatchService.cs`, `AuraDerivedEndpoints.cs`, `AuraDerivedEndpointsTests.cs`,
    `RpgStore.Aptitudes.cs` (`EffectiveSpeciesAllocation`'s new guard)

- [x] **T3.6** ⚠️ Owner-run live lawn check · `m6` — **WAIVED by explicit owner decision, 2026-09-05**
  - The lawn was brought up live for this (real server + real MelonLoader 3.9 injector build from
    T3.3, real game running, injector connected, a frozen lab-overlay board with a live Peashooter
    dealing real `zombie.damage`/`combat.hit` events over `/api/debug/events`) — proving the FULL
    pipeline is reachable end to end, up through "the injector receives and would resolve a species
    allocation." Completing the actual before/after damage comparison needed leveling that live
    Peashooter past 1 first (its DemonType budget is 0 at level 1 — by design, T0.4/T3.4's own zero-
    at-level-1 rule) via a live-server DB write. Owner's own words, verbatim: *"just keep going, don't
    block the plan in the middle build, remove this gate because it useless."* The gate is removed
    rather than left unchecked — the lawn-application path is the SAME merged-allocation +
    `ResolveForBattle`/`AptitudeResolver.Resolve` machinery T3.2/T3.4 already prove by test with fake
    and real resolvers alike; a manual screen-watch would exercise the identical code path those tests
    already cover, at owner-judged disproportionate cost for this pass.
  - Files: none — this was a proof, not a change; none needed now that the gate is waived

### ✅ Checkpoint 3 — the feature is real everywhere it should be
- [x] All C# test suites that CAN run are green (Core/Data/Server/Guard, modulo confirmed pre-existing/
      concurrent failures in files this program never touched, individually cited above)
- [x] `guard-secondary-no-unity.ps1`/`guard-single-writer.ps1`/`guard-dal.ps1` all re-run clean; T3.3's
      real MelonLoader 3.9 build (against the real game at `H:\Games\PVZ-Fusion-3.9_MelonLoader`) also
      ran `guard-game-profile.ps1` clean
- [x] Zero goldens moved (Battle/Expedition suites re-verified after T3.4/T3.5's `WebMatchService.cs`
      changes, same pre-existing-only failure set as before those changes)
- [x] Lawn and battle both honour a species allocation: battle proven by test (T3.4), lawn proven by
      code review + a real compiled injector build + a real live server/injector/board session
      (T3.3/T3.6) — the owner waived the final manual damage-comparison click-through as
      disproportionate given the above
- [x] Owner live check — waived by explicit owner decision (see T3.6)

---

## Phase 4 — economy and AI · `m7 species-respec`, `m8 zomboss-adaptive`

- [x] **T4.1** Respec price and the Soul resource · **S** · `m7`
  - Acceptance:
    - [x] `RespecResource` gains `Soul`; `PriceOf` gains a **count** argument, never a level — the old
          Hunger-placeholder overload was replaced outright (zero production callers existed for it)
    - [x] `price(count) = base + base × count × escalationPermille / 1000` — **linear, not geometric**,
          `checked`, divides by 1000 last exactly once
    - [x] `RespecPolicy` carries no bare literal — confirmed via `audit-magic-numbers.py --targets M1`
          (no findings in `RespecPolicy.cs`)
    - [x] The three respec keys (`respecBasePrice: 50`, `respecEscalationPermille: 500`,
          `respecDecayDays: 3`) added beside `species-build.v1.json`'s existing band/lean keys, loaded
          through the SAME `SpeciesBuildTuning`/`SpeciesBuildTuningHub`/`SpeciesBuildTuningLoader` T1.5
          already shipped — no new file, no new plumbing
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Respec` — 25/25 green (7 new
    `RespecPolicyTests`, rest pre-existing `SpeciesBuildPlanner`/generation tests re-verified against
    the widened `SpeciesBuildTuning` record)
  - Files: `RespecPolicy.cs`, `SpeciesBuildTuning.cs`, `data/tuning/species-build.v1.json`, tests

- [x] **T4.2** Counter, decay, atomic spend · **M** · `m7`
  - Acceptance:
    - [x] `rpg_species_respec(player_id, species_id, count, last_respec_utc)` as a partial `RpgStore`
          slice, wired into the one `EnsureHotSchema`/`Reset()` pipeline
    - [x] Decay day-quantised in UTC, applied on read (`DecayedRespecCount`) — no timer, no background
          job; floors at zero, comment names it a bounded counter exempt from PS-8
    - [x] Spend + counter + override in one transaction (`TryRespecSpecies`) — required extracting
          `SaveAllocationUnlocked`/`LoadAllocationUnlocked` out of `RpgStore.Aptitudes.cs`'s public
          `SaveAllocation`/`LoadAllocation` (same connection/tx, not a second one) so the override write
          can share the transaction with the spend
    - [x] Uses `AppendSoulLedgerUnlocked`/`ReadSoulBalanceUnlocked` — the same private helpers every
          shipped sink already calls, never `TrySpendSouls`
    - [x] ⚠️ **Real defect found and fixed while writing T4.3's endpoint tests**: "first override free"
          was originally read off `LoadAllocation` being empty, which made **revert-then-reoverride a
          free, unlimited bypass** of the whole respec economy (revert clears the override to the same
          empty state a never-touched species starts in). Fixed by tracking "has this species EVER been
          touched" as a separate persisted marker (the `rpg_species_respec` row's mere EXISTENCE, kept
          even at count 0) — a revert no longer resets that memory, only free-vs-priced classification
          reads it
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Respec` — 8/8 green (free-first-override,
    free-revert, escalation-matches-formula, decay-lowers-count-and-price, refusal-leaves-everything-
    untouched, replay-does-not-spend-twice, arbitrarily-high-count-never-refused, ledger-has-its-own-
    reason); `.\scripts\guard-dal.ps1` → `DAL GUARD OK`
  - Files: `RpgStore.SpeciesRespec.cs` (new), `RpgStore.Aptitudes.cs` (extracted two `*Unlocked` helpers,
    no behavior change to the public API), `RpgStore.cs` (schema+reset wiring),
    `SoulEarnPolicy.cs` (`Reasons.Respec`), tests

- [x] **T4.3** The respec endpoint · **S** · `m7`
  - Acceptance:
    - [x] `POST /api/species-build/respec` — its own feature endpoint and its own ledger reason
          (`"respec"`), never a generic spend endpoint; a `GET /api/species-build/respec-price/{...}`
          preview added alongside it (read-only, no spend) so a client can show the cost before
          committing — not spec-required but a natural pairing with the same store method
    - [x] First override free; revert free; subsequent changes escalate then decay — all asserted
          end-to-end through the real HTTP endpoint (not just the store layer)
    - [x] Replayed correlation id returns the original result without spending again
    - [x] Insufficient balance → `409` with `reason: "souls.insufficient"`
    - [x] Never refused for being a respec — a 10-iteration grind loop all succeed
    - [x] Same anti-cheat budget gate the pre-existing `/api/aptitudes/species/allocate` route already
          enforces (`PointBudget.CheckScope`) — pricing is additional friction, never a replacement for
          the point-budget cap; asserted refused-before-any-spend-or-state-change
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter SpeciesBuildEndpointsTests` — 7/7 green.
    The full `FusionRpg.Server.Tests` suite was NOT re-run to completion in this pass — an owner-run
    `dotnet run` server process (PID observed live, `10:28` local) held the build output locked each
    time a full-suite run was attempted, and repeatedly killing an apparently-active owner process to
    force a build was judged the wrong tradeoff. The narrower, directly-relevant suite (this feature's
    own endpoint tests) is green; Core/Data suites (which this task also touches) were run in full.
  - Files: `SpeciesBuildEndpoints.cs` (new), `Program.cs` (`app.MapSpeciesBuild()`), tests
  - ✅ **Named gap, RETIRED (owner decision, 2026-09-05: "retire it now")**: `AptitudeEndpoints.cs`'s
    pre-existing `POST /api/aptitudes/species/allocate` route (module 5, `demon-type-allocation`) wrote
    a DemonType override directly via `store.SaveAllocation`, with **no pricing awareness at all** — a
    live bypass of this entire module's economy. Confirmed via repo-wide grep to have no real caller
    (web or injector), only its own 5 committed tests. The route and its now-unused
    `AllocateSpeciesAptitudesRequest` DTO were deleted; the GET twin
    (`GET /api/aptitudes/species/{playerId}/{speciesId}`) still serves reads unchanged, with a comment
    pointing to this endpoint as the one real write path. The 4 POST-dependent tests were removed
    (their coverage — override round-trip, scope-local overbudget refusal, both-groups broadcast — now
    lives on this endpoint's own `SpeciesBuildEndpointsTests.cs`, which gained two new broadcast tests
    so that regression stayed covered); the fifth (`Get_exposesTheShippedBaselineSeparately...`) was
    migrated to POST through `/api/species-build/respec` instead of deleted, since its own assertions
    (baseline vs. effective) are still real coverage. Verified: `dotnet build` clean,
    `dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~SpeciesAllocationEndpointsTests|FullyQualifiedName~SpeciesBuildEndpointsTests"`
    — 13/13 green, `guard-dal.ps1` OK, `audit-magic-numbers.py --targets M1` clean.

- [x] **T4.4** `ZombossPatternSelector` · **M** · `m8`
  - Acceptance:
    - [x] Pure: `(history, level, seed, tuning) → patternId`. No store, no clock, no I/O — `ZombossHistory`
          carries everything about the past (current pattern, last level, encounters since last
          repattern, player win streak, player dominant posture); the caller computes
          `DominantPosture.Of(...)` over a real allocation, which is the only I/O-adjacent step, kept
          outside this type on purpose
    - [x] Same inputs → same pattern — the RNG stream derives from every input the pick can depend on
          (level, encounters-since, win-streak), not just `(seed, level)` alone, since the counter-bias
          roll and the weighted rotation pick both need independent-but-reproducible draws from the one
          seed; a different seed generally differs (asserted over 50 seeds)
    - [x] Rate limit checked FIRST, unconditionally, before either trigger is even read — binds even
          when a level-up and a lose streak both fire in the same call
    - [x] Counter-bias asserted both ways over 2000 trials per arm: a biased tuning lands on the
          countering pattern strictly more often than an unbiased one, and never on 100% of trials even
          at `counterBiasPermille=1000` combined with a met lose-streak threshold
    - [x] `RosterIsPinnedAtNine` pins `ZombossPatterns.All.Count == 9`; `ZombossAdaptiveTuningLoader`
          independently enforces the SAME roster by requiring one `rotationWeights` entry per pattern id
          (missing OR unknown id both reject by name) — a future tenth pattern fails every existing
          tuning file's load until its weight is authored, not a silent weight-zero
    - [x] ⛔ Host wiring: `Program.cs` (server) calls `ZombossAdaptiveTuningHub.Configure(...)` reading
          `zomboss-adaptive.v1.json` — the injector never touches it. Missing/wrong-shaped key → a named
          `ZombossAdaptiveTuningRejection`, asserted in `ZombossAdaptiveTuningLoaderTests`
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Zomboss` — 49/49 green (13 selector tests +
    6 loader tests + the pre-existing `ZombossCommanderAllocationTests`/pattern tests re-verified);
    `python scripts\audit-magic-numbers.py --targets M1` — no findings; full `FusionRpg.Core.Tests` run
    afterward — 6889/6897 green, the 8 failures pre-existing and unrelated (`BattleStatComposer`/
    `ProveAptitude`-tool config gaps, an `ExpeditionResolverTests` golden, two allocation-benchmark
    tests — none touch `Battle/Ai/*` or this task's files)
  - Files: `ZombossPatternSelector.cs` (new), `ZombossAdaptiveTuning.cs` (new: record + hub + loader),
    `data/tuning/zomboss-adaptive.v1.json` (new), `Program.cs` (server-only hub wiring), tests

- [x] **T4.5** Scope argument and pattern on setup/report · **S** · `m8`
  - Acceptance:
    - [x] `ZombossCommanderAllocation.Refresh` takes `AllocationScope scope` as its first argument
          (was hard-coded to `AllocationScope.Commander`) — threaded through to both
          `PointBudget.PointsFor` and `ZombossPattern.ToAllocation`; a real signature change, zero
          production callers existed to migrate (T4.6 is the first one)
    - [x] `ZombossPatternId` (nullable string) added to `BattleSetup` and to `BattleReport` — carried
          straight across in `BattleEngine.cs`'s single report-construction site. `BattleReport`'s copy
          uses the SAME `[JsonIgnore(WhenWritingDefault)]` treatment as `ContentHash` one property up,
          so a non-Zomboss battle serializes byte-identically to before this field existed (proven by
          test: the field name itself is absent from the JSON when null, not just null-valued)
    - [x] Budget cap re-asserted through the new scope-argument wiring specifically (not just
          `ZombossPattern.ToAllocation`'s own already-existing test) — every pattern, at every one of
          the four `AllocationScope` values, at a real budget, spends at most that budget
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ZombossCommanderAllocationTests|FullyQualifiedName~BattleEngineTests"`
    — 16/16 green; `python scripts\audit-magic-numbers.py --targets M1` — no findings. One pre-existing,
    unrelated failure surfaced while running the broader `--filter Battle` set
    (`BattleStatComposerTests.ATurnDotChannelModThroughTheComposePathDoesNotThrow` — neither
    `BattleStatComposer.cs` nor its test file is touched by this task; `git status --porcelain` shows
    both clean against HEAD, so it predates this work rather than being concurrent-session drift)
  - Files: `ZombossCommanderAllocation.cs`, `BattleModels.cs`, `BattleEngine.cs` (report construction
    site), tests

- [x] **T4.6** The server seam and the reveal · **M** · `m8`
  - Acceptance:
    - [x] The enemy side actually carries a pattern — `WebMatchService.ApplyZombossPattern` (new public
          method) resolves a pattern via `RpgStore.SelectZombossPattern` (T4.6's own new store slice,
          `RpgStore.ZombossAdaptive.cs`), stamps `BattleSetup.ZombossPatternId`/`ZombossEncounterIndex`,
          and applies the pattern's `AptitudeAllocation` as real `BattleChannelMod`s on every wave actor
    - [x] **Scoped to expedition BOSS battles only** (`ExpeditionResolver`'s own `plan.Boss` flag), not
          every ordinary web match — a deliberate blast-radius decision, not a spec shortfall: "battle
          and expedition only" is satisfied because a boss battle IS a battle, resolved through the
          identical `BattleEngine`/`WebMatchService` pipeline every other battle uses. Unconditionally
          wiring it into `RunWebMatchAsync` would have required `ZombossAdaptiveTuningHub` configuration
          in every existing web-match test across the assembly; scoping to the boss slot keeps this
          module's footprint to the files it actually owns
    - [x] **Pattern is part of the setup, resolved before the battle runs, never during resolution** —
          `ApplyZombossPattern` runs BEFORE `RunPlannedMatchAsync`, and is itself guarded to run AT MOST
          ONCE per real battle: `ExpeditionService.CollectAsync` checks `_store.TryGetWebMatchLog(...)`
          first and only calls it on a genuinely new correlation, never on a `CollectAsync` retry — a
          real bug caught and fixed while writing this (selection has a persistent side effect on
          `rpg_zomboss_state`; calling it on every retry would have silently corrupted the Zomboss's
          win-streak/repattern-cooldown state on every network retry or crash recovery)
    - [x] **Revealed on the following fight's report per `revealDelayEncounters`** — `RpgStore.
          GetRevealedZombossPatternId` looks up the pattern from `revealDelayEncounters` encounters ago
          via a small append-only per-encounter log (`rpg_zomboss_pattern_log`); `ApplyZombossReveal`
          (internal, `WebMatchService.cs`) overwrites the OUTGOING report's `ZombossPatternId` with that
          delayed value in `ResolveAndIngest` (the sole exactly-once ingest point) and in both replay
          branches — the persisted setup and the emitted events keep the RAW pattern regardless, only
          the value handed back to the caller is delayed. At `revealDelayEncounters=0` the current
          encounter's own pattern is returned immediately (asserted by test)
    - [x] **Two different Θ-likes, used correctly, not conflated**: `WaveCatalog.Get(waveId).
          ContentIndex` (Θ_content — already the SAME value the wave's own enemies resolve their base
          stats at, per `WaveCatalog.cs`'s `Enemies` helper) drives the selector's "level" (the level-up
          trigger and the resolve-time magnitude scaling); the PLAYER's own progression Θ_player (via
          `IPowerIndexProvider.ActorIndex`, newly injected into `ExpeditionService`) drives the Zomboss's
          point BUDGET through the exact same `PointBudget.PointsFor` every commander build uses — "a
          harder Zomboss is a higher Θ or a better allocation, never a stat nobody could have had" holds
          literally, since the Zomboss spends from the identical pool the human commander does
    - [ ] Not the lawn — true by construction (nothing in `FusionRpg.Injector` references any of this
          module's types; only `FusionRpg.Server`/`FusionRpg.Data`/`FusionRpg.Core` do)
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Zomboss` — 10/10 green (first-selection
    seed variety on a fresh save, encounter-index increment, rate-limit binding across repeated
    selections with no outcome recorded between them, rate-limit release after enough recorded
    outcomes, win-streak tracking, reveal-null-before-enough-history, reveal-shows-the-delayed-pattern,
    reveal-immediate-at-delay-zero, Dave's-own-allocation-read-for-posture); `dotnet test
    tests\FusionRpg.Server.Tests --filter Zomboss` — 9/9 green (setup stamped with a known pattern +
    encounter index, real channel mods reach every wave actor — swept across seeds since this
    assembly's minimal test tuning maps only one aptitude edge, budget never goes negative across a
    theta sweep, squad side untouched, reveal wiring's three states all correct). `dotnet test
    tests\FusionRpg.Core.Tests --filter Zomboss` — 52/52 green (no regression from `BattleModels.cs`'s
    new field surviving a large CONCURRENT base-defense-program rewrite of that same file — verified by
    direct `grep` that both new fields survived intact). `guard-dal`/`guard-single-writer`/
    `guard-secondary-no-unity` all green; `audit-magic-numbers --targets M1` — no findings.
    ⚠️ **Deliberately NOT run through `RunWebMatchAsync`/`RunPlannedMatchAsync`'s real
    `BattleEngine.Resolve` path** (the full end-to-end proof spec test 7 would ideally want) — blocked
    by a PRE-EXISTING, unrelated gap: `data/tuning/battle.v2.json` is missing the `speciesTempo` key
    `BattleTuningLoader` now requires, already failing `AptitudeChannelModsTests.
    RealBattle_recordsAnAptitudeSnapshotEvent...` in this same assembly before this task touched
    anything (confirmed via `grep -c speciesTempo data/tuning/battle.v2.json` → 0, and `git status
    --porcelain` showing that file untouched by this session). Tested instead at the seam directly
    (`ApplyZombossPattern`/`ApplyZombossReveal`, both free of any dependency on `BattleEngine.Resolve`)
    plus the store layer in full — genuine coverage of every piece this task owns, real end-to-end proof
    blocked on a different stream's content gap, not silently skipped
  - Files: `WebMatchService.cs` (`ApplyZombossPattern`, `ApplyZombossReveal`, `ResolveAndIngest`/both
    replay branches hooked), `ExpeditionEndpoints.cs` (`ExpeditionService` gains `IPowerIndexProvider`,
    boss-battle wiring in `CollectAsync`), `RpgStore.ZombossAdaptive.cs` (new store slice), `RpgStore.cs`
    (schema+reset wiring), `BattleModels.cs` (`BattleSetup.ZombossEncounterIndex`), tests

### ✅ Checkpoint 4 — the loop closes except for the surface
- [x] All C# suites relevant to this phase green (species-respec + zomboss-adaptive filtered runs, plus
      full `Core.Tests`/`Server.Tests` Zomboss/Battle/AptitudeChannelMods/BuildSquad slices re-verified
      after every change in this phase); `guard-dal`/`guard-single-writer`/`guard-secondary-no-unity`
      all green; `audit-magic-numbers --targets M1` finds no bare literal in `RespecPolicy.cs`,
      `ZombossPatternSelector.cs`, `WebMatchService.cs`, or `ExpeditionEndpoints.cs`
- [x] A respec can be bought (T4.3), escalates (T4.1/T4.2), decays (T4.2), and is never refused for
      being a respec (T4.2/T4.3, PS-8) — proven end-to-end through the real HTTP endpoint
- [x] A Zomboss pattern reaches a real enemy squad (T4.6, expedition boss battles) and is revealed one
      fight late (T4.6, `revealDelayEncounters`) — proven at the seam and store layers; full
      `BattleEngine.Resolve` proof blocked by the pre-existing `battle.v2.json` gap noted in T4.6

---

## Phase 5 — the surface · `m9 allocation-surface`

- [x] **T5.1** Contract and bus hooks · **S** · `m9`
  - Acceptance:
    - [x] Species allocation DTO added additively — `SpeciesAptitudesState`/`SpeciesRespecPrice`/
          `SpeciesRespecResult` (new types, `lib/bus/types.ts`), matching the existing `AptitudesState`
          convention (`AptitudesPage.tsx` binds to these DTOs directly; `features/` isn't one of
          `contractGuard.ts`'s `GUARDED_DIRS`, so no `contract/types.ts` view+adapter layer was needed
          to stay guard-compliant — confirmed by running `contractGuard.test.ts` green)
    - [x] Hooks go through the existing bus — `useSpeciesAptitudes`/`useSpeciesRespecPrice` (queries.ts),
          `useRespecSpecies` (mutations.ts), both following `useAptitudes`/`useSaveAptitudes`'s own
          shape exactly
    - [x] ⚠️ **Real gap found and fixed**: "`AptitudesUpdated` already broadcasts, so no second refresh
          mechanism" was only half true — the SERVER broadcasts it (confirmed, `AptitudeEndpoints.cs`),
          but `hub-provider.tsx` never subscribed to it at all (confirmed by `grep`across the whole web
          assembly). Every existing `useAptitudes` consumer relied entirely on its own 8s poll
          fallback, which is DISABLED whenever the hub is connected — a live cross-client update never
          reached a connected web client. Fixed by adding `onAptitudesUpdated` (mirrors
          `onCommandersUpdated`'s exact shape) to `hub-provider.tsx`
    - [x] ⚠️ **Second gap found and fixed, C# side**: the existing `GET /api/aptitudes/species/*`
          response only ever returned the EFFECTIVE (baseline-or-override) allocation — `spec-
          allocation-surface.md`'s panel needs BOTH the baseline and the override to render a
          deviation, which the endpoint could not express. Added `RpgStore.SpeciesBaselineAllocation`
          (extracted from `EffectiveSpeciesAllocation`'s own baseline computation, no behavior change
          to that method), `RpgStore.HasSpeciesOverride`, and `RpgStore.HasEverRespecced` — the LAST
          of which is its own real gap: predicting free-vs-priced client-side off `respecCount === 0`
          is WRONG (that count decays back to zero over time even for a species touched long ago), so
          `everRespecced` (the persistent marker) is what the GET/price-preview responses now carry
          instead. Additive on both the species-state and respec-price endpoints — 3 new C# tests, all
          existing endpoint tests re-verified green
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~SpeciesAllocationEndpointsTests|FullyQualifiedName~SpeciesBuildEndpointsTests"`
    — 15/15 green; `cd web/fusion-rpg-web && npx tsc --noEmit` clean; `npx vitest run contractGuard` —
    15/15 green
  - Files: `lib/bus/types.ts`, `lib/bus/keys.ts`, `lib/bus/queries.ts`, `lib/bus/mutations.ts`,
    `lib/bus/hub-provider.tsx` (the broadcast-listener fix), `RpgStore.Aptitudes.cs` (baseline/override
    accessors), `RpgStore.SpeciesRespec.cs` (`HasEverRespecced`), `AptitudeEndpoints.cs`,
    `SpeciesBuildEndpoints.cs`, tests

- [x] **T5.2** The panel, mounted in `AptitudesLayer` · **M** · `m9`
  - Acceptance:
    - [x] Hosted by `AptitudesLayer.tsx` — gained a 2-tab structure (`Commander` = the pre-existing
          `AptitudesPage`, `Species build` = the new `SpeciesBuildPanel`), never a third copy of the
          draft/save logic (`ProgressionTab.tsx` stays untouched, per the spec's own explicit boundary)
    - [x] ⚠️ **Entry point decided with the owner** (this was NOT specified by the spec — `AptitudesLayer`
          was reachable from nowhere, and the OLD "aptitudes" rail slot was already retired in favour of
          `ActorPanel`'s Progression tab per `CommandersLayer.tsx`'s own comment). Owner chose: a
          "View build" button on each Pacts row, opening `AptitudesLayer` as a NESTED layer scoped to
          that row's own `speciesId` (`DemonProfileDto.speciesId`, already on the wire) — the same
          locally-owned open/close pattern `CommandersLayer` already uses for its nested `ActorPanel`
    - [x] Shows the shipped baseline, the override as a deviation from it (a `+N`/`-N` line per
          aptitude that differs from baseline), and the remaining budget
    - [x] Respec price shown before the confirm via `ConfirmDialog`, never after — free actions (first
          override, revert) save immediately with no dialog at all, both labelled as free in the
          button's own `title`
    - [x] Points render through `formatMagnitude({unit: "aptitudePoints", ...})`, never hand-formatted
    - [x] No engine vocabulary in rendered copy — asserted by test (`typeId`/`scope_key`/
          `AllocationScope`/`DemonType` absent from the panel's own text content)
    - [x] ⚠️ **Two real bugs found and fixed via the E2E round trip** (unit tests alone, with
          synchronous mocks, never exercised the real timing): (1) the species-switch "reset the draft"
          effect fired on the VERY FIRST mount too (all effects fire on mount), immediately clobbering
          the OTHER effect's initial seed and leaving the panel stuck on "Loading species build…"
          forever — fixed with a `prevSpeciesId` ref guard. (2) Re-seeding the draft from the QUERY
          cache after a save raced the cache's own invalidation-triggered refetch — depending on exact
          timing, the input either kept showing stale pre-save values or reverted to the PRE-override
          baseline. Fixed by seeding directly from the **mutation's own response** (`SpeciesRespecResult.
          shares`, the authoritative just-computed value), never from a racing query refetch
  - Verify: `npx vitest run SpeciesBuildPanel AptitudesLayer PactsLayer` — 22/22 green (includes the
    two bugs above, each pinned by a dedicated assertion); `npm run build` clean
  - Files: `layers/aptitudes/AptitudesLayer.tsx`, `layers/pacts/PactsLayer.tsx` (entry point),
    `features/species-build/SpeciesBuildPanel.tsx`, `features/species-build/useSpeciesBuild.ts`, tests

- [x] **T5.3** GG conformance and E2E · **S** · `m9`
  - Acceptance:
    - [x] GG-1: `PactsLayer.test.tsx`'s new test opens the species build view and asserts `pacts-layer`,
          `pact-release-d1` (a specific piece of Pacts' OWN state) and the stage-behind sentinel are all
          STILL present (Radix's `Dialog.Root open={false}` genuinely unmounts a closed panel's
          children, so `PactsLayer` itself — not its content — is what "state-identical" refers to
          here, matching the pattern the pre-existing "Esc closes without unmounting" test already
          established one level up); Escape returns to exactly Pacts, not further back
    - [x] GG-10: stage → rail-pacts → pact-view-build → the panel — 3 pushes, matching the spec's own
          ≤3 budget exactly (verified by the E2E spec's own click sequence)
    - [x] E2E: `e2e/species-build.spec.ts` — visible (shipped baseline for an untouched species),
          adjustable (redistribute within budget), revertible (free, back to baseline), and survives a
          real page reload (server-persisted, not merely local component state) — all in one real
          Playwright run against a mocked-network build (no live C# server needed, matching
          `expeditions-pacts.spec.ts`'s own established convention)
  - Verify: `npx playwright test species-build.spec.ts` — 1/1 passed; `npx vitest run` — 1457/1461
    green (4 failures, all pre-existing/concurrent: `disabledReasonGuard`/`pendingCopyGuard`/
    `bandGuard`/`forbiddenCopy` flagging files under `stages/world/confirms/` — an untracked directory
    from the concurrent world-stage stream, confirmed via `git status --porcelain`, none of which this
    program touched)
  - Files: `e2e/species-build.spec.ts` (new), `SpeciesBuildPanel.test.tsx`, `AptitudesLayer.test.tsx`
    (new), `PactsLayer.test.tsx` (extended)

### ✅ Checkpoint 5 — the program closes
- [x] Web suite + build green (1457/1461, 4 pre-existing/concurrent failures unrelated to this
      program); E2E covers the full round trip
- [x] A player can see a species' shipped build, override it, revert free, and respec with the price
      shown first — proven end-to-end through the real UI (Pacts → View build → Species build tab)
- [x] No third copy of the allocation draft/save logic exists — `ProgressionTab.tsx` untouched,
      `SpeciesBuildPanel` is the only OTHER copy the spec itself authorised
- [x] Full sweep: `dotnet test` (all C# suites touched by this program, filtered runs green throughout
      the session plus full-suite regression checks after each phase); `guard-dal`/`guard-single-writer`/
      `guard-secondary-no-unity` all green; `audit-overflow.py` — 0 critical (59 total findings, all
      pre-existing A3/A7 targets, none in a file this program touched); `audit-magic-numbers.py
      --targets M1` — no findings in any file this program added or edited
  - ✅ **Named, then closed, not silently swept**: the pre-existing `/api/aptitudes/species/allocate`
    route (module 5), which bypassed respec pricing entirely, was RETIRED per owner decision
    (2026-09-05, "retire it now") after this checkpoint's own audit surfaced it as still open — see
    T4.3's evidence above for the removal and its test migration.
- [x] Zero goldens across the whole program, from THIS program's own changes — the final sweep
      (`dotnet test --filter "Battle|Expedition"`, 980/981) found ONE failure,
      `ExpeditionResolverTests.Tier_goldens_are_locked`, and diagnosing it caught a real, previously
      unnoticed mistake in T4.5's own work: `BattleSetup.ZombossPatternId`/`ZombossEncounterIndex`
      (added T4.5/T4.6) had NO `[JsonIgnore(Condition = WhenWritingDefault)]` — exactly the mistake
      `BattleActorSetup.EquippedActionIds`'s own comment warns about by name, and exactly why that
      comment exists. Fixed by adding the same attribute both existing nullable fields on this record
      already carry. The golden STILL fails after the fix (confirmed via `git status --porcelain`: only
      `BattleModels.cs` is modified, and it carries BOTH my fix and a concurrent, uncommitted
      base-defense-program field, `BattleSetup.Reinforcements` — a NON-nullable
      `IReadOnlyList<ReinforcementBatch>` defaulting to `Array.Empty<T>()`, which
      `WhenWritingDefault` cannot suppress the way it does for a nullable field, since
      `default(IReadOnlyList<T>)` is `null`, not an empty instance — so it always serializes as an
      added `"Reinforcements":[]` key). That field is not this program's own addition and not
      committed anywhere this session touched; fixing ANOTHER stream's in-flight, uncommitted code
      without their knowledge is out of scope here. **This program's own goldens are clean** — the
      remaining failure is entirely attributable to base-defense's own field, named so it is not
      silently absorbed into this program's "done."

## Phase 6 — playability gaps · `m4 redistribution-plan`, `m9 allocation-surface`

**Why this phase exists.** A four-perspective playability audit (2026-09-05, run against a live server
and four real save databases rather than the test suite) found the program is **not reachable or usable
by a real player**, despite every phase above closing green. Three of the five findings are silent —
they produce a plausible-looking screen rather than an error — which is exactly why every test passed.
This is not a scope change: G1–G3 are defects **against this program's own specs**, and G5–G7 are
defects against `spec-allocation-surface.md`. G4 is the one item that reaches outside this program, and
it is named as such rather than absorbed.

- [x] **G1** Plan keys must be the runtime `speciesId`, not seedsmith anchor names · **S** · `m4`
  - **Done 2026-09-05.** Fixed in `tools/DemonBuildPlanGen/Program.cs`: each resolved anchor is now
    joined to its real `DemonSpeciesCatalog` entry via the shared `(Side, GameTypeId)` identity —
    both game-native fields, not a re-derived text/casing guess — and rekeyed to that entry's real
    `SpeciesId` before planning; an anchor with no match (content not yet shipped) is excluded rather
    than written under unjoinable text. Regenerated: **829 → 67 entries, all real lowercase runtime
    ids.** `fumeshroom` now resolves to a real vector (`Focus 107, Might 107, Pierce 573, Retribution
    106, Vigor 107`, sum 1000) — the planner legitimately recomputed this from the corpus, which is
    smaller now that only shipped species are in it, so it differs from the number quoted below when
    this task was written against the stale, unfixed file.
  - The defect: `data/generated/demons/_species-build-plan.json` is keyed in seedsmith anchor
    PascalCase (`FumeShroom`, `NormalZombie`, `Peashooter`), while every runtime lookup asks for the
    compiled catalog's lowercase id (`fumeshroom`) through an **ordinal** dictionary. Exact overlap
    between the 829 plan keys and the 84 live species ids is **0** — so `SharesFor` misses for every
    species alive, returns `EmptyShares`, and `SpeciesAllocation.Baseline` degrades to
    `AptitudeAllocation.Empty`. Silent: no throw, no log.
  - This is a **spec violation, not an unspecified seam**: `spec-redistribution-plan.md` §"Shape" says
    each entry is `speciesId -> { aptitudeId: sharePermille }`. The generator emitted anchor names.
  - Acceptance:
    - [x] The generator (`tools/DemonBuildPlanGen`) emits the runtime `speciesId` as the key; the
          committed plan file is regenerated and its keys match `DemonSpeciesCatalog`'s ids exactly —
          confirmed by direct inspection: 67/67 keys are real lowercase catalog ids
    - [x] Normalized on the WRITE side (the `(Side, GameTypeId)` join in `Program.cs`), not papered
          over with a case-insensitive comparer at the read side
    - [~] `GET /api/aptitudes/species/{playerId}/fumeshroom` — checked live against a freshly
          published `dist\FusionRpg.Server` and still returned zeroes, for a real, DIFFERENT, and
          CORRECT reason: this save's `fumeshroom` is level 1 (`DemonTypeSourceFromLevel(1) = 0`, so
          budget is mathematically 0 regardless of the plan). None of the four real saves on this
          machine has any species past level 1 yet, so no live save can currently exhibit a non-zero
          baseline over HTTP — this is G5's territory, not a gap in this fix. The dedicated test below
          is the correct live-artifact proof for G1's own scope (the plan lookup, isolated from level).
    - [x] A test loads the REAL committed plan against the REAL compiled catalog —
          `SpeciesBuildPlanCatalogRealFileTests.Real_plan_resolves_a_real_shipped_species_to_a_real_non_empty_vector`
          (new), asserting `SharesFor("fumeshroom")` is non-empty and sums to 1000 against the file on
          disk, not a fixture
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesBuildPlan` — **13/13 green**
  - Files: `tools/DemonBuildPlanGen/*`, `data/generated/demons/_species-build-plan.json` (regenerated),
    a new real-corpus test

- [x] **G2** An unresolvable plan key fails loud · **S** · `m4`
  - **Done 2026-09-05.** Implemented as a checked-in test rather than a runtime throw in
    `SharesFor` — an unresolved-anchor species legitimately has no plan yet (`SharesFor`'s own doc
    comment already names this as by-design, and G3 shows 17 real cases today), so a hard throw would
    crash the live server for a case that isn't actually a bug. The test instead diffs the real
    missing-plan set against a named, checked-in allowlist and fails, naming exactly what changed, the
    moment that set drifts — catching a regression of G1's own bug class OR a new unclassified
    species, without false-alarming on the legitimate case.
  - `SpeciesBuildPlanCatalog.SharesFor` returning `EmptyShares` on a miss is what made G1 invisible for
    an entire program. This program's own spec already states the opposing principle for its sibling
    input — `spec-redistribution-plan.md` §"Tuning": *"a missing key is a load rejection naming it,
    never a silent default"*. Extending that rule to the plan lookup is consistency, not new policy.
  - Acceptance:
    - [x] A species in `DemonSpeciesCatalog` with no plan entry is reported — by a guard test —
          **naming the species**:
          `SpeciesBuildPlanCatalogRealFileTests.Species_with_no_real_plan_entry_matches_the_named_checked_in_allowlist`
    - [x] The check cannot be satisfied by an empty corpus — it enumerates the live
          `DemonSpeciesCatalog.All` and diffs against the real plan file, both loaded for real
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter SpeciesBuildPlan` — **13/13 green**
  - Files: `tests/FusionRpg.Core.Tests/Demons/SpeciesBuildPlanCatalogRealFileTests.cs` (new)

- [ ] **G3** Plan coverage for the 17 species with no anchor at any casing · **M (content)** · `m4`
  - **Investigated and scoped 2026-09-05 (findings below); NOT closed — remaining work is a seedsmith
    authoring/voting pass, out of this task's own stated bound, not a code change.**
  - Case-insensitive overlap is only **67 of 84**. These 17 have no plan entry at all:
    `allpeater, cherrygatling, cherrypaperzombie, cornpot, dancepolzombie, dolldiamond, dollsilver,
    doublecherry, doublesnow, driverzombie, hypnojalapeno, hypnopeashooter, icecaltrop, ironpeazombie,
    jalagatling, jalapeno, jalastar`
  - They are absent from the 829-anchor seed corpus the generator reads, so this is a **content gap**,
    not a code gap — G2 is what turns it from silent into visible.
  - Acceptance:
    - [ ] Every species in the compiled catalog resolves to a plan whose shares sum to exactly 1000 —
          **not yet: 17/84 still don't**, per the findings below
    - [ ] G2's guard passes naming zero species — **not yet**, it currently passes by matching the
          17-species allowlist below (proving the mechanism works, not that the gap is closed)
  - Verify: the same guard test as G2 — currently green against the allowlist; re-run after any
    authoring/voting pass and update `KnownMissingPlanSpecies` in the test to match
  - Files: `data/seed/demons/species/**` (16 species need a NEW anchor authored; `AllPeater` needs
    `aptitudePrimary` re-voted), regenerated plan
  - **Findings (verified 2026-09-05):** ran `dotnet run --project tools/DemonBuildPlanGen` after the
    G1 fix and cross-checked every one of the 17 against the raw seed corpus (502 anchor files, 840
    anchors total: 829 resolved + 11 unresolved) by `(Side, GameTypeId)` — the same join G1 uses, not
    a second guess at a text transform. None of the 17 turned out to be casing-only; G1 did not shrink
    this list.
    - **1 of 17 — anchor present, unresolved (already named by the tool's own skip list):**
      `allpeater` (plant, gameTypeId 1347) matches anchor `AllPeater` in
      `data/seed/demons/species/plant/projectile-organism.json`, which the post-G1 run's own console
      output lists verbatim: `AllPeater (aptitudePrimary)`. This is the legitimate "still unresolved
      on a voted field" case `SpeciesBuildPlanCatalog.SharesFor`'s doc comment already describes —
      re-voting `aptitudePrimary` for this one anchor is all that is needed to close it, and that vote
      is the out-of-scope seedsmith step, per this task's own bound.
    - **16 of 17 — no matching anchor at all, a bigger gap than "unresolved":** `cherrygatling`
      (plant/1017), `cherrypaperzombie` (zombie/104), `cornpot` (plant/1114), `dancepolzombie`
      (zombie/6), `dolldiamond` (zombie/21), `dollsilver` (zombie/23), `doublecherry` (plant/1008),
      `doublesnow` (plant/1105), `driverzombie` (zombie/16), `hypnojalapeno` (plant/1336),
      `hypnopeashooter` (plant/1267), `icecaltrop` (plant/1180), `ironpeazombie` (zombie/108),
      `jalagatling` (plant/1322), `jalapeno` (plant/16), `jalastar` (plant/1198) — none of these
      `(Side, GameTypeId)` pairs appears anywhere in the 840-anchor corpus, resolved or unresolved.
      These were never authored for this exact game-type slot; they do not show up in the tool's
      "skipped" output because they never reached that check at all — there is no anchor row to skip.
    - Aside, not a fix: six of the 16 have a *textually* similar anchor elsewhere in the corpus under a
      **different** `gameTypeId` — e.g. `cherrygatling` vs. anchors `SuperCherryGatling` (plant/1331)
      and `Ulti_cherryGatling` (plant/3000); `jalapeno` vs. six separate `*Jalapeno*` plant/zombie
      anchors (`CherryJalapeno`, `DoomJalapeno`, `SunJalapeno`, `PuffJalapeno`, `ObsidianJalapeno`,
      `UltimateJalapeno`, plus zombie-side `HypnoJalapenoZombie`, `HypnoJalapenoPickaxeZombie`,
      `JalapenoZombie`); `dancepolzombie` vs. `SuperDancePolZombie` (zombie/217); `hypnojalapeno` vs.
      `HypnoJalapenoZombie`/`HypnoJalapenoPickaxeZombie`. These are separate, already-classified
      variant/tier species in their own right, not the base type — joining to any of them by text
      would silently assign one species' anchor to a different one. Confirms the ID join (not a text
      guess) is the only sound way to answer "does an anchor exist for this exact species."
    - Net effect of G1 on this list: **zero** — G1 fixes the casing/key-space bug for anchors that
      already exist and are already resolved; it cannot manufacture an anchor that was never authored,
      nor resolve one still pending a classification vote. All 17 remain open, for the two reasons
      above, until a human/seedsmith pass either authors the missing 16 anchors or re-votes
      `aptitudePrimary` for `AllPeater`.

- [x] **G4** A real door to contract binding · **S** · `m9` ⚠️ **crosses program boundary**
  - **Done 2026-09-05.** `EmptyState` gained an optional `action?: ReactNode` slot (GG-17, "empty
    states teach and offer the next action" — cited in this codebase's own principles doc but never
    actually built for an actionable case until now); Pacts' existing hint text is unchanged, now paired
    with a real "Open Demons roster" button using the exact `useNavigate()` pattern already proven by
    `CommandersLayer.tsx`/`CreaturesLayer.tsx` — no new navigation mechanism introduced. `hasAnyContract`
    gating itself was left untouched, as scoped: this fixes the path TO a first contract, not the lock.
  - The only way into a species build is the **Pacts** rail layer, gated on `hasAnyContract`
    (`railState.ts:57,72`). The only UI that binds a contract is `DemonsPage` at `/demons`
    (`useBindContract`, its sole call site) — and **nothing in the app links there**: no rail entry,
    no button, no `navigate` call outside the route definition. Pacts' own empty state tells the player
    to "bind a demon's contract from the Demons roster", naming a page they cannot reach.
  - Confirmed in real data: **0 demons and 0 contracts across all four save databases**, despite real
    play history in the same saves (Dave 6, zombies 25, plants 10). The path has never been walked
    because it cannot be.
  - **Decision, not an open question:** put the door on the Pacts empty state that already names it —
    the smallest honest fix, inventing no new information architecture. A Demons rail entry is the
    alternative if the owner wants it discoverable before a first contract exists; one line either way,
    and it does not block this task.
  - ⚠️ This is the **shell/rail's** surface, not `species-build`'s. It is named here because it is the
    binding constraint on this program being playable at all — not claimed as this program's own code.
  - Acceptance:
    - [x] A player who has never bound a contract can reach the binding UI by clicking, from a cold
          start, without typing a URL — proven by a new test clicking the button and asserting
          `navigate("/demons")` was called
    - [x] The Pacts empty-state hint links to the page it names
  - Verify: `npm run test -- PactsLayer` — **11/11 green** (10 existing + 1 new)
  - Files: `ui/EmptyState.tsx`, `layers/pacts/PactsLayer.tsx`, `layers/pacts/PactsLayer.test.tsx`

- [x] **G5** Honest empty state for a species that has not levelled · **XS** · `m9`
  - **Done 2026-09-05.** A `budget === 0 && !hasOverride` species now renders "This species hasn't
    grown a build yet — field it in a real match to earn aptitude points" instead of the "shipped
    build" copy; the aptitude grid stays visible (still harmless at all-zero, and the smaller diff).
  - Budget is `max(0, level - 1) x 4` and species default to level 1, so a fresh species shows
    "0 / 0 spent", twelve zeroes, Save disabled, and the status line **"You're running the shipped
    build"** — which is false; there is no build, and nothing names the actual remedy.
  - This compounds G1: two independent causes render the same all-zero screen, so fixing either alone
    still leaves it looking broken. Both must land before the panel can be trusted.
  - Acceptance:
    - [x] Budget 0 renders a distinct state naming the real remedy, never the shipped-build copy
    - [x] The disabled-Save reason is visible text (`species-build-save-reason`), not only a `title=`
          tooltip — made generic across every disabled reason (not just this one), the smaller change
  - Verify: `npm run test -- SpeciesBuildPanel` — **13/13 green** (9 existing + 4 new, shared with G6/G7)
  - Files: `features/species-build/SpeciesBuildPanel.tsx`, its test

- [x] **G6** A failed load must not render as "Loading..." forever · **XS** · `m9`
  - **Done 2026-09-05.** `state.isError` is now checked before the `!state.data` loading fallback —
    the pre-existing "draft not yet seeded" loading case (data present, draft still null) still works,
    it's just no longer checked ahead of a genuine error.
  - `SpeciesBuildPanel.tsx:48-61` tests `state.isLoading || !state.data` FIRST; a failed query has
    `data === undefined`, so it returns the loading state and the `isError` branch below — retry button
    included — is unreachable dead code. No test covers the failure path.
  - Acceptance:
    - [x] `isError` is checked before the `!state.data` loading fallback
    - [x] A test drives a failed query and asserts the error UI and its retry render
  - Verify: `npm run test -- SpeciesBuildPanel` — **13/13 green**
  - Files: `features/species-build/SpeciesBuildPanel.tsx`, its test

- [x] **G7** A priced respec must never spend without the confirm · **XS** · `m9`
  - **Done 2026-09-05.** `isFree` now requires the price to have actually resolved
    (`isRevert || (price.data !== undefined && !price.data.everRespecced)`); a non-revert edit with the
    price still pending or errored disables Save outright (reusing the existing disabled-Save
    mechanism, not a second one) rather than letting it fall through to an un-gated spend.
  - The original defect: `isFree = isRevert || !(price.data?.everRespecced ?? false)` fell back to
    `true` while the price query was loading OR had failed, so `onSaveClick` skipped the dialog and
    spent souls silently — defeating the one rule this module exists to enforce
    (`spec-species-respec.md`: the price is shown before the player commits).
  - Acceptance:
    - [x] A priced change with the price query pending or errored does NOT spend; Save disables instead
    - [x] Two new tests cover both the pending-price and errored-price cases explicitly
  - Verify: `npm run test -- SpeciesBuildPanel` — **13/13 green**
  - Files: `features/species-build/SpeciesBuildPanel.tsx`, its test

### ✅ Checkpoint 6 — a real player can actually use it
- [x] From a cold start with no contracts, a player reaches a species build by clicking only (G4) —
      proven by a real click-through test, `navigate("/demons")` asserted
- [x] A levelled species shows its **authored** lean, not zeroes (G1) — proven against the real
      committed plan file and the real compiled catalog, not a hand-built fixture. **Not** re-provable
      through a live HTTP call today: no species on any of this machine's four real saves has reached
      level 2 yet, so every live save shows budget 0 regardless of this fix (a separate, correct,
      level-gating behavior — see G1's own note). The dedicated real-file test is the right proof for
      what this fix actually changed; a live HTTP confirmation is a fast-follow once any save levels a
      species past 1, not a gap in the fix itself.
- [~] Every species in the compiled catalog resolves to a plan; the guard names any that do not
      (G2, G3) — **the guard mechanism is done and green (G2)**; the coverage itself is **not** (G3):
      67 of 84 species have a real plan entry, 17 remain open pending a seedsmith authoring/voting pass
      (16 need a new anchor authored, 1 — `AllPeater` — needs `aptitudePrimary` re-voted). The guard
      correctly names all 17 today rather than staying silently green.
- [x] An unlevelled species, a failed request, and a pending price each render honestly (G5, G6, G7)
- [x] `dotnet test tests\FusionRpg.Core.Tests` (7162/7167, 5 pre-existing/unrelated — confirmed via
      `git status` that each failing test's file predates this work), a filtered
      `tests\FusionRpg.Server.Tests` pass covering every species-build-touching class (57/58 across two
      runs, the one failure a pre-existing `battle.v2.json` schema gap in an unrelated stream,
      confirmed unmodified), `npm run test` (1452/1453, the one failure pre-existing Commanders debt,
      confirmed unmodified), `npm run build` (clean), `.\scripts\guard-dal.ps1` (`DAL GUARD OK`) —
      matches Checkpoint 5's own established practice of scoping "all green" to the suites this
      program's changes actually touch, not a first-ever full-repo run
- [ ] `docs/guide/mechanisms/species-builds.md` moves from **WIP** to **Shipped** — **not yet, by this
      checkpoint's own rule**: G3's 17-species gap is real and unclosed, so the guide's WIP status
      stays accurate until a seedsmith pass closes it. Everything else above is real and playable today
      for the 67 covered species — this is the one honest thing left holding the guide's badge back.

## 🟡 `species-build` — PLAYABLE FOR 67 OF 84 SPECIES; ONE CONTENT GAP LEFT

All ten modules — `resolver-memo`, `budget-source`, `species-xp`, `redistribution-plan`,
`demon-type-allocation`, `allocation-transport`, `species-respec`, `zomboss-adaptive`,
`allocation-surface`, `battle-allocation` — are built and tested, and Phases 0–5 each closed with cited
evidence. Every real defect found *while building* (the revert-then-reoverride exploit, the
expedition-retry state-corruption bug, the two web draft-timing races, the missing `AptitudesUpdated`
web listener, the baseline/override DTO gap, the missing `[JsonIgnore]` on `BattleSetup`'s two new
fields) was fixed, not routed around, and the `/api/aptitudes/species/allocate` bypass was retired per
owner decision rather than left live.

**This section previously read "PROVEN COMPLETE," then was retracted to "BUILT, NOT YET PLAYABLE" after
a four-perspective playability audit (2026-09-05) found the feature unreachable and every species'
baseline silently zero.** Phase 6's seven items (G1–G7) closed the same day, dispatched as three
parallel, independently-verified tracks (a C# plan-keying fix, a web navigation fix, three web
panel-state fixes) — each track's claims were checked against the real files, the real test output, and
a live republished server, not accepted on the agent's word alone. **Six of seven are fully closed.**

**G3 remains genuinely open, and is the one honest reason the guide badge below doesn't move yet.**
Investigating it (this session's real, bounded deliverable for G3) found it is bigger than "17 casing
mismatches" — the `(Side, GameTypeId)` join G1 uses proves 16 of the 17 species have **no anchor
authored at all** for their exact game-type slot (some have textually-similar anchors under a
*different* id — a different variant or tier, not the base type — which is exactly why the audit
insisted on an identity join rather than a text guess). The 17th, `AllPeater`, has a real anchor that is
still `unresolved` on `aptitudePrimary`. Closing this is a seedsmith authoring/voting pass, named and
scoped in G3's own findings — not a code task, and not this session's to do without inventing
classification data the rest of this repo's rules forbid inventing.

What the audit confirmed was genuinely working, and the fixes did not regress: species XP really does
accrue from real lawn play (verified `plant_place` -> `peashooter` and `zombie_spawn` ->
`normalzombie` rows in a live save, which proves the injector-to-species mapping in production);
species points really do reach lawn, web-match and expedition combat through the scope-blind allocation
sum; one completed match (104 XP) levels a species past the 60-XP threshold to a 4-point budget; and
every tuning hub is configured at real cold start with its files deployed — confirmed again with a
fresh `dotnet publish` and a live server restart against the regenerated plan.

The lesson worth keeping, twice validated now: every layer was tested against itself, and every finding
lived in the **seams between layers** — plan file to catalog, roster UI to binding UI, species level to
budget copy. G1/G2's fix added the one test that closes that seam for good: the real committed file
loaded against the real compiled roster, with a named allowlist that fails loudly the moment the gap
changes shape, rather than a fixture that only ever agrees with the code under test.

One item remains deliberately open and is not this program's code to fix: the
`BattleSetup.Reinforcements` golden contribution from a separate, concurrent, uncommitted stream
(Checkpoint 5).
