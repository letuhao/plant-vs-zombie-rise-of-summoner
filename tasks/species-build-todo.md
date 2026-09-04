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

- [ ] **T2.1** Scope key and compose-at-read · **M** · `m5`
  - Acceptance:
    - [ ] `scope_key` = `player:{playerId}:species:{speciesId}`, encoded in **one place** beside the
          Commander encoding
    - [ ] **A species with a level and no override row resolves to the plan's shares × its budget — not
          to zero.** The test that catches the silent-zero risk
    - [ ] Per-player isolation: two players, same species, same level, one overriding → different results
    - [ ] Baseline is **computed, never persisted**
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude`; `dotnet test tests\FusionRpg.Data.Tests --filter Allocation`
  - Files: `SpeciesAllocation.cs`, `RpgStore.Aptitudes.cs`, tests

- [ ] **T2.2** Override, budget enforcement, endpoints · **M** · `m5`
  - Acceptance:
    - [ ] Override is **whole-vector**; deleting the row returns exactly the baseline, **free**
    - [ ] Overspend refused, **scope-locally** — a large Commander budget does not fund it
    - [ ] Scopes sum before share (an actor with both reads the sum)
    - [ ] No cap on the allocation (PS-8); overflow throws
    - [ ] ⛔ **`AptitudesUpdated` broadcasts to BOTH groups** on a species save, not just `WebGroup`.
          A WebGroup-only send is a defect this repo has already shipped once and found by live probe
          (2026-08-30): it left the injector's cached allocation **stale until the next reconnect**.
          Without this, a respec would not take effect on the lawn until a match edge
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `.\scripts\guard-dal.ps1`
  - Files: `AptitudeEndpoints.cs`, `RpgStore.Aptitudes.cs`, tests

- [ ] **T2.3** The seam guard · **XS** · `m5`
  - Acceptance:
    - [ ] A guard test asserts **no production consumer of species allocation calls `LoadAllocation`
          directly** — composition only happens behind the one named effective-allocation entry point
  - Verify: `dotnet test tests\FusionRpg.Guard.Tests`
  - Files: `tests/FusionRpg.Guard.Tests/SpeciesAllocationSeamTests.cs`

### ✅ Checkpoint 2 — allocation is real, and still invisible
- [ ] Core + Data + Server + Guard green
- [ ] **Zero goldens** — holds *only* because the budget is zero at level 1. If one moved, T0.4 broke
- [ ] Nothing player-visible yet, by design

---

## Phase 3 — both read paths · `m6 allocation-transport`, `m10 battle-allocation`

**They land together.** Shipping the lawn without battle is the incoherence module 10 exists to prevent.

- [ ] **T3.1** Server payload gains `species` — additively · **S** · `m6`
  - Acceptance:
    - [ ] `shares` is **kept, not renamed** — `RpgClient` hard-requires it; a rename silently stops the
          injector applying every allocation
    - [ ] `species` added beside the existing `{theta, budget, spent, withinBudget, shares}`
    - [ ] Only species the player has actually levelled are sent
    - [ ] The commander half is **byte-unchanged** for a player with no species allocations
  - Verify: `dotnet test tests\FusionRpg.Server.Tests --filter Aptitude`
  - Files: `AptitudeDtos.cs`, `AptitudeEndpoints.cs`, tests

- [ ] **T3.2** Core `SpeciesAllocationSource` · **M** · `m6`
  - Acceptance:
    - [ ] `ctx → allocation` behind an **injected lookup**, mirroring `SpecimenOwnershipOracle`'s shape —
          fully provable in Core with a fake resolver, no game required
    - [ ] `polevaulterzombie`/`wallnut` resolve differently (side stays in the key) — a named test
    - [ ] **An un-configured index reports, never returns a silent zero** — the 222-point defect's shape
    - [ ] Commander and species points **merge into one `AptitudeAllocation`**, resolved once
    - [ ] No I/O on the Hot path — a guard test
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Aptitude`
  - Files: `SpeciesAllocationSource.cs`, tests

- [ ] **T3.3** Injector cache and refresh · **M** · `m6`
  - Acceptance:
    - [ ] Cache refreshed on **exactly the existing cadence** — `StartAsync`, reconnect,
          `AptitudesUpdated`, match edges. No new lifecycle, no polling
    - [ ] Never awaits the server on the Hot path
    - [ ] **One test per refresh path** for the cache-update logic that *is* Core-testable — a stale
          cache after an `AptitudesUpdated` push is a failure, and it is the shape T2.2's broadcast exists
          to prevent
    - [ ] ⚠️ The injector-side write is unverifiable offline — **verified by direct read plus T3.6**,
          this repo's established precedent for injector-only edits
  - Verify: `.\scripts\guard-secondary-no-unity.ps1`; `.\scripts\deploy-play.ps1 -NoServer`
  - Files: `RpgClient.cs`, `CheatState.cs`

- [ ] **T3.4** Battle setup reads species · **S** · `m10`
  - Acceptance:
    - [ ] `AptitudeChannelMods` takes the species; commander read **hoisted out** of the per-actor loop
    - [ ] **The coherence test:** an actor whose species has an allocation resolves *different* mods than
          one whose species has none
    - [ ] **Merged ≠ concatenated** — asserted explicitly, so a future refactor into two resolves fails
          loudly rather than silently changing every battle
    - [ ] Two species in one squad resolve differently (the species read stays per-actor)
    - [ ] **Inertness preserved:** a player with no allocation in either scope still resolves to empty —
          the existing `AptitudeChannelModsTests` assertion, unchanged and still passing
    - [ ] **The commander-read hoist is behaviour-neutral** — a squad's mods are identical before and after
    - [ ] **Every battle and expedition golden byte-identical.** If one moves, the level-1-zero rule broke
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `dotnet test tests\FusionRpg.Core.Tests --filter Battle`
  - Files: `WebMatchService.cs`, `tests/.../AptitudeChannelModsTests.cs`

- [ ] **T3.5** The two diagnostic paths · **S** · `m10`
  - Acceptance:
    - [ ] Battle report `aptitude.snapshot` includes the species contribution — a report missing the term
          that decided the battle is worse than no report
    - [ ] The derived-stat inspection endpoint agrees with what the lawn applies
    - [ ] Provenance no longer hard-codes `"scope" = "commander"`
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`
  - Files: `WebMatchService.cs`, `AuraDerivedEndpoints.cs`, tests

- [ ] **T3.6** ⚠️ **Owner-run live lawn check** · `m6`
  - Acceptance:
    - [ ] A plant whose species has a real allocation shows changed stats on a live lawn
    - [ ] Clearing the allocation returns it to baseline
    - [ ] No frame-time regression versus before this program (T0.1's memo is why)
  - Verify: `.\scripts\deploy-play.ps1 -NoServer`, then the live check
  - Files: none — this is a proof, not a change

### ✅ Checkpoint 3 — the feature is real everywhere it should be
- [ ] All C# suites green; four boundary guards green
- [ ] **Zero goldens**
- [ ] Lawn **and** battle both honour a species allocation; both diagnostics agree with the game
- [ ] Owner live check passed

---

## Phase 4 — economy and AI · `m7 species-respec`, `m8 zomboss-adaptive`

- [ ] **T4.1** Respec price and the Soul resource · **S** · `m7`
  - Acceptance:
    - [ ] `RespecResource` gains `Soul`; `PriceOf` gains a **count** argument, never a level
    - [ ] `price(count) = base + base × count × escalationPermille / 1000` — **linear, not geometric**
          (geometric against a flat faucet is how a price becomes a ceiling)
    - [ ] `RespecPolicy` carries no bare literal
    - [ ] ⚠️ **`species-build.v1.json` is shared with `m4`** — T1.5 created it with the band and lean keys.
          **Add the three respec keys beside them; do not rewrite the file.** Its loader and host wiring
          already exist from T1.5, so this task adds keys, not plumbing
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Respec`
  - Files: `RespecPolicy.cs`, `data/tuning/species-build.v1.json`, tests

- [ ] **T4.2** Counter, decay, atomic spend · **M** · `m7`
  - Acceptance:
    - [ ] `rpg_species_respec(player_id, species_id, count, last_respec_utc)` as a **partial `RpgStore`
          slice** sharing the one connection/lock/`EnsureHotSchema`/`Reset()` pipeline
    - [ ] Decay day-quantised in UTC, applied **on read** — no timer, no background job; count floors at
          zero and carries a comment naming it a **bounded counter**, exempt from PS-8
    - [ ] **Spend + counter + override in one transaction** — a simulated failure between them leaves
          *neither* applied
    - [ ] Uses **the ledger path the shipped sinks use** — `TrySpendSouls` has zero production callers
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter Respec`; `.\scripts\guard-dal.ps1`
  - Files: `RpgStore.SpeciesRespec.cs`, tests

- [ ] **T4.3** The respec endpoint · **S** · `m7`
  - Acceptance:
    - [ ] Its **own** feature endpoint and reason — spends are never a generic endpoint
    - [ ] **First override free; revert free**; subsequent changes escalate then decay — all asserted
    - [ ] Replayed correlation id returns the original result **without spending again**; a refusal
          writes no state
    - [ ] Insufficient balance → `409 souls.insufficient`, no counter increment
    - [ ] **Never refused for being a respec** (PS-8)
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`
  - Files: `SpeciesBuildEndpoints.cs`, tests

- [ ] **T4.4** `ZombossPatternSelector` · **M** · `m8`
  - Acceptance:
    - [ ] Pure: `(history, level, seed, tuning) → patternId`. No store, no clock, no I/O
    - [ ] Same inputs → same pattern; the pick is a function of `(seed, level)`, never a live roll
    - [ ] **Rate limit binds:** no second re-pattern within the cooldown even when both triggers fire
    - [ ] **Counter-bias is a weight, not a guarantee** — over many seeds the countering pattern is more
          likely *and is not always chosen*. Both halves asserted; the second keeps it out of the Mario
          Kart failure mode
    - [ ] Roster pinned at nine so a self-cancelling tenth cannot be added quietly
    - [ ] ⛔ **Host wiring.** `zomboss-adaptive.v1.json` gets a loader injected by the **server host only**
          — the Zomboss exists on battle and expedition surfaces, never the lawn, so wiring it into the
          injector would be dead weight. Missing key → named rejection
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Zomboss`
  - Files: `ZombossPatternSelector.cs`, `data/tuning/zomboss-adaptive.v1.json`, tests

- [ ] **T4.5** Scope argument and pattern on setup/report · **S** · `m8`
  - Acceptance:
    - [ ] `ZombossCommanderAllocation` takes the scope as an **argument** — it hard-codes Commander today,
          and a Zomboss pattern is a named allocation, not a player's commander build
    - [ ] Pattern id on `BattleSetup` and on the report
    - [ ] Budget cap holds for every pattern at every budget — the anti-cheat property, re-asserted here
          because this is what makes it reachable
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter Battle`
  - Files: `ZombossCommanderAllocation.cs`, `BattleModels.cs`, tests

- [ ] **T4.6** The server seam and the reveal · **M** · `m8`
  - Acceptance:
    - [ ] The enemy side actually carries a pattern — without this, "a real production caller" is
          unreachable
    - [ ] **Pattern is part of the setup**, resolved before the battle runs, never rolled during
          resolution: the same `(setup, seed)` resolves identically twice
    - [ ] Revealed on the **following** fight's report per `revealDelayEncounters`; at delay 0, immediately
    - [ ] Battle and expedition only — **not the lawn**, and the acceptance does not ask for it
  - Verify: `dotnet test tests\FusionRpg.Server.Tests`; `dotnet test tests\FusionRpg.Core.Tests`
  - Files: `WebMatchService.cs`, `ExpeditionEndpoints.cs`, tests

### ✅ Checkpoint 4 — the loop closes except for the surface
- [ ] All C# suites green; `guard-dal` green; `audit-magic-numbers` finds no bare literal in either Policy
- [ ] A respec can be bought, escalates, decays, and is never refused
- [ ] A Zomboss pattern reaches a real enemy squad and is revealed one fight late

---

## Phase 5 — the surface · `m9 allocation-surface`

- [ ] **T5.1** Contract and bus hooks · **S** · `m9`
  - Acceptance:
    - [ ] Species allocation DTO added **additively**; a narrowing or rename would be a version bump and
          is not done here
    - [ ] Hooks go through the existing bus — TanStack Query + the one SignalR hub; features call
          `useX()` only. `AptitudesUpdated` already broadcasts, so no second refresh mechanism
  - Verify: `cd web/fusion-rpg-web && npm run test`
  - Files: `contract/types.ts`, `lib/bus/queries.ts`, `lib/bus/mutations.ts`

- [ ] **T5.2** The panel, mounted in `AptitudesLayer` · **M** · `m9`
  - Acceptance:
    - [ ] Hosted by **`AptitudesLayer.tsx`** (owner, 2026-09-05) — imported by nothing today, so no
          migration and no third copy of the draft/save logic
    - [ ] Shows the shipped baseline, the override **as a deviation from it**, and the remaining budget
    - [ ] **Respec price shown before the confirm, never after**; first override and revert labelled free
    - [ ] Points render through the `aptitudePoints` unit class — and **never as a speculative preview**,
          which that class's rule forbids
    - [ ] No engine vocabulary in any rendered string
  - Verify: `npm run test -- SpeciesBuild`; `npm run build`
  - Files: `layers/aptitudes/AptitudesLayer.tsx`, `features/species-build/SpeciesBuildPanel.tsx`, `useSpeciesBuild.ts`

- [ ] **T5.3** GG conformance and E2E · **S** · `m9`
  - Acceptance:
    - [ ] **GG-1:** opening the layer from a stage leaves the stage mounted, its state identical **by
          reference**, with no refetch — the assertion GG-1 names as its own test
    - [ ] **GG-10:** the override action is ≤3 pushes from a stage
    - [ ] E2E: a species' build is visible, adjustable, revertible, and survives a reload
  - Verify: `npm run test`; `npx playwright test`
  - Files: tests only

### ✅ Checkpoint 5 — the program closes
- [ ] Web suite + build green; E2E covers the round trip
- [ ] A player can see a species' shipped build, override it, revert free, and respec with the price
      shown first
- [ ] **No third copy** of the allocation draft/save logic exists
- [ ] Full sweep: all C# suites, four boundary guards, `audit-overflow`, `audit-magic-numbers`
- [ ] **Zero goldens across the whole program**, or each move triaged and explained before re-blessing
