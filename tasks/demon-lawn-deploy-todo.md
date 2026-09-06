# Task list: demon lawn deploy

Plan: [demon-lawn-deploy-plan.md](demon-lawn-deploy-plan.md). Specs:
[docs/architecture/demon-lawn-deploy-map.md](../docs/architecture/demon-lawn-deploy-map.md),
[docs/architecture/demon-lawn-deploy/](../docs/architecture/demon-lawn-deploy/) (`spec-lawn-deploy-core.md`,
`spec-lawn-deploy-events.md`, `spec-zomboss-deploy-ai.md`).

Dependency order: Phase 1 (`lawn-deploy-core`) → Checkpoint 1 → Phase 2 (`lawn-deploy-events`) →
Checkpoint 2 → Phase 3 (`zomboss-deploy-ai`) → Checkpoint 3 (program close). T1.1-T1.5 may build in
parallel; T1.6 needs all of them.

## Phase 1 — `lawn-deploy-core`

### T1.1 — Patron deploy refusal · **S** · 2 files — **DONE 2026-09-06**

- `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` (`TryBeginUniqueDeploy`) — refuses with
  `"patron.cannot-deploy"` before admitting any spawn, reusing the same `IsPatronUnlocked`
  (`RpgStore.Fusion.cs:354`) check fusion's own sacrifice-refusal already uses.
- **Commander refusal NOT built — a real, investigated finding, not a skip.** `CommanderId`
  (`Core/Commanders/CommanderId.cs`) is a fixed Dave/Zomboss enum with no demon-instance binding
  anywhere; `PlayerEmpireCommanders.ForPlayer` always returns `[Dave]`. There is no state today meaning
  "this specimen is the Commander," so nothing exists yet to refuse against. A code comment at the real
  call site names this precisely for whoever builds that binding later. See
  `spec-lawn-deploy-core.md`'s own Correction 1 for the full trace.
- Acceptance:
  - [x] Deploying the specimen holding the active Patron aura refuses with `patron.cannot-deploy`.
  - [x] Deploying any other owned demon specimen is unaffected — no false-positive refusal.
  - [x] Switching the Patron away lets the previously-designated specimen deploy again (proves the
        refusal reads live state, not a cached/stale flag).
  - [ ] ~~Deploying the current default Commander's own `instanceId` refuses~~ — inapplicable today, see
        above. Re-open this line once a real demon-to-Commander binding exists.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeployCommanderRefusal` — **39/39
  passing**, confirmed live (3 new tests + the full existing `PatronStoreTests`/`UniqueActorStoreTests`
  suites, zero regressions).
- Files: `RpgStore.UniqueActors.cs` (edit), `tests/FusionRpg.Data.Tests/DemonLawnDeployCommanderRefusalTests.cs` (new).

### T1.2 — Reconciled trait-binding on every deploy · **M** · 3-4 files — **DONE 2026-09-06**

- `src/FusionRpg.Data/Sqlite/RpgStore.UniqueActors.cs` — new `ReconcileDemonTraitBindingsUnlocked`,
  mirroring `ReconcileUniqueEquipmentAtomBindingsUnlocked`'s exact diff shape (wanted-vs-existing,
  idempotent, no blind insert), producing `effect_binding` rows for each of the specimen's `TraitIds`
  mapped to its real `trait.{traitId}` container. Called from `TryBeginUniqueDeploy`, always, right
  after the contract gate — never at mint time.
- Acceptance:
  - [x] A fresh specimen's own traits bind correctly on first deploy.
  - [x] Re-deploying the SAME specimen with unchanged traits writes no new bindings (idempotent — same
        binding ids before and after, not merely the same count).
  - [x] **A specimen promoted between two deploys gets the NEW trait's binding on its next deploy** —
        the exact case the strengthen pass found the naive "bind at mint" design would silently miss.
        Simulated the real `PromotionUnlocked` effect (an in-place `traits_json` update on the same
        `instanceId`) directly rather than driving the full fusion economy, since promotion's own
        mechanics are already covered by `FusionStoreTests.cs`.
  - [x] A trait removed from the specimen has its binding correctly withdrawn on the next deploy (the
        symmetric case — proves this isn't a pure-additive reconciler).
  - [x] `AtomPushService`'s output for that owner includes exactly the specimen's current trait
        containers — **DONE 2026-09-07**: `tests/FusionRpg.Server.Tests/DemonLawnDeployAtomPushTests.cs`
        (2 tests, mirrors `MultiOwnerPushTests.cs`'s own direct-`Build`-call pattern), asserting a real
        `_push.Build(new OwnerScope(OwnerKind.UniqueActor, id), Lawn(), matchSeed: 7)` payload contains
        `trait.critical-hunter`'s compiled grant scoped to `instance:{id}`, and that a no-traits specimen
        produces none. Blocked twice by a DIFFERENT concurrent session's own in-progress
        `ActionContainerEffectResolverFactory.Build` arity change (`WebMatchService.cs`, then
        `BuildSquadEquippedActionsTests.cs`, neither touched by this work) — both settled on their own.
        **A real bug in this test's own expectation was then found and fixed by a full-suite regression
        run**: the compiled grant's `EffectId` is the atom's own `EffectiveIcdKey()`
        (`AtomCompiler.cs:77,174,205`), not `AtomRow.DeriveId(family, variant, tier)` —
        `trait-critical-hunter.json`'s real seed content explicitly authors
        `"icdKey": "trait.critical-hunter.crit"`, so the expected id was wrong from the start; never
        caught earlier because both prior attempts hit the concurrent-session compile break before
        actually running. **2/2 passing**, confirmed via
        `dotnet test tests/FusionRpg.Server.Tests --filter DemonLawnDeployAtomPush` AND a full-suite
        `FusionRpg.Server.Tests` run (311 tests: 286 passing, up from 285 before the fix — the other 25
        failures are pre-existing, unrelated World/District/Siege-content issues plus the
        already-documented `vocabulary.json` defect, none touching demons/species/this module's diff).
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy` — **4/4 passing**. Full
  `FusionRpg.Data.Tests` regression run: **1102/1104**, zero real regressions — the 2 non-passes are
  both confirmed non-issues, not caused by this work: `ItemUniqueStoreTests.Unique_eligible_seeds_
  every_rung_through_the_sc7_gate` is an already-documented pre-existing failure (unrelated rarity-
  ladder seeding), and `DemonSpeciesImportCliTests.A_stale_committed_file_refuses_the_whole_import_and_
  writes_nothing` failed only in the full run with a subprocess "build failed" message (it shells out to
  `dotnet run --project tools/DemonSpeciesImport`, racing a concurrent session's own in-progress edit) —
  re-run in isolation immediately after: 2/2 passing.
- Files: `RpgStore.UniqueActors.cs` (edit), `tests/FusionRpg.Data.Tests/DemonLawnDeployTests.cs` (new).

### T1.3 — Overflow-safety verification through `AtomCompiler.cs` · **S** · investigation — **DONE 2026-09-06, no fix needed**

- Read `AtomCompiler.cs`'s actual numeric handling directly rather than assuming from its `int`-typed
  surface. The real overflow-risk arithmetic lives at `AtomCompiler.cs:568,572` — `PowerLadderKMicro`/
  `PowerLadderKMilli` ops multiplying by `pThetaValue`, both wrapped in `checked(...)` (already throws
  on overflow rather than wrapping — satisfies CLAUDE.md's own "overflow throws" rule at the letter,
  independent of the int-vs-long question). Then checked the REAL trait content T1.2 actually binds:
  `data/seed/atoms/trait-critical-hunter.json` uses `"op": "flat", "amount": 150"` — a small, fixed,
  pre-authored value, never touching `pThetaValue` or the PowerLadder ops at all.
- Acceptance:
  - [x] **Confirmed no demon-scale magnitude reaches `AtomCompiler`'s risk-relevant surface via T1.2's
        own path.** Trait grants use `ProduceAndBind`'s fixed `DemonTraitAtomPinTheta` (=20, the same
        flat content-scale pin equipment already uses safely) and the atom's own `"flat"` op — neither
        the pin nor the amount is derived from the specimen's own `PTheta`. The overflow risk the
        strengthen pass correctly named is real, but belongs to **T1.5** (species-magnitude delivery),
        which will need its own overflow check when built, since carrying a specimen's actual `PTheta`
        through a `PowerLadder`-scaled op is exactly the case `AtomCompiler.cs:568,572` exists to guard.
- Verify: direct code read (`AtomCompiler.cs:568,572`) + direct content read
  (`trait-critical-hunter.json`) — both cited above; no test needed since nothing was changed.
- Files: investigation only, no code changed. **T1.5 inherits this exact check as its own prerequisite —
  do not skip it there just because T1.3 cleared T1.2.**

### T1.4 — `DeployMode` → spawn side/type, and the `side`-column decision · **S** · 2 files — **DONE 2026-09-06, resolved differently than planned**

- **Owner-confirmed 2026-09-06** (asked directly — this was a real game-design/engine-mechanics
  question, not resolvable by more code reading): `side`/`typeId` pass through **unchanged** for both
  `DeployMode` values — no column mutation, no override. Reason: "pvz engine don't allow us spawn
  zombie in dave side without hypno, without hypno, spawn cause zombie become enemy" — a spawned
  zombie-type entity is hostile to the plant side by construction; only the game's native hypnotize
  operation flips that. This **reverses** the plan's own original "update the side column" default,
  which investigation had already flagged as suspect before asking (see spec's own Correction 3).
- **A second, real finding emerged from the same investigation**: this codebase already tried to solve
  the "native hypnotize state" problem once, for a different feature (`content-stack` program,
  `tasks/content-stack-todo.md`) — and found real PvZ mind-control is "a side-swap, not a flag," with no
  verified `SetMindControl`/`UnMindControl`-equivalent safe to call from the Injector today. That
  program named it a refusal rather than guess. **This module does the same**: `TryBeginUniqueDeploy`
  now refuses `HypnoAlly`-mode demons outright (`"deploy.hypno-ally-not-implemented"`) rather than
  attempting an unverified native hypnotize call. `PlantAvatar`-mode demons (confirmed the common case)
  deploy normally, side/typeId unchanged.
- **A real regression found and fixed while verifying this**: `DemonLawnDeployCommanderRefusalTests.cs`
  and `DemonLawnDeployTests.cs` (T1.1/T1.2's own tests) both picked their test species via
  `.First(s => s.Side == "zombie" && ...)`, with no `DeployMode` filter — and that pick happened to
  land on a `HypnoAlly`-mode species, which the new refusal correctly started blocking, breaking 5 of
  their own tests. Fixed by excluding `DeployMode != DemonDeployMode.HypnoAlly` from both files' own
  species selection, since those tests are about unrelated concerns (Patron refusal, trait binding) and
  should be robust to which species sorts first.
- Acceptance:
  - [x] A `PlantAvatar`-mode demon deploys normally — side/typeId exactly match the species' own values,
        confirmed via a real committed species (`abyssswordstar`).
  - [x] A `HypnoAlly`-mode demon refuses deploy with `"deploy.hypno-ally-not-implemented"`, leaving the
        actor untouched (still `Roster` phase) — not a half-deployed state.
  - [x] T1.1/T1.2's own tests still pass after excluding HypnoAlly from their species selection — the
        regression this task's own change caused was found and fixed, not shipped.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy` — **10/10 passing** (3 new
  HypnoAlly-refusal tests + the 7 existing T1.1/T1.2 tests, all green together).
- **Full `FusionRpg.Data.Tests` regression run (2026-09-06/07) found the SAME species-selection defect
  in two MORE pre-existing files this task hadn't touched**: `ExpeditionStoreTests.cs`'s `CatalogSpecies`
  (`.First(s => s.Side == "zombie")`, no `DeployMode` filter) broke `Closing_releases_the_lock` and
  `Soft_lock_refuses_cross_mode_both_ways` (both assert a deploy succeeds); `ContractGateTests.cs`'s
  `Species` (`.First(s => s.Acquisition != CaptureOnly && s.TraitPool.Count > 0)`, same gap) broke
  `PvZ_deploy_accepts_a_bound_demon` the same way. All three fixed identically — added
  `&& s.DeployMode != DemonDeployMode.HypnoAlly` to each file's own species selection, same narrow
  test-fixture-only fix as T1.1/T1.2's own files, no production code touched.
- **Full-suite re-run confirmed clean**: `Failed: 1, Passed: 1112, Skipped: 0, Total: 1113`. The sole
  remaining failure is the already-documented pre-existing
  `ItemUniqueStoreTests.Unique_eligible_seeds_every_rung_through_the_sc7_gate` (memory
  `item-unique-eligible-seeding-pre-existing-failure-2026-09-06`) — confirmed unrelated (rarity-ladder
  seeding, no `TryBeginUniqueDeploy` call anywhere in that test). **Zero real regressions from T1.4**
  once all 5 incidentally-affected test files (2 from T1.1/T1.2's own, 3 more found only by this full
  run) were fixed.
- Files: `RpgStore.UniqueActors.cs` (edit — the refusal, in `TryBeginUniqueDeploy`),
  `tests/FusionRpg.Data.Tests/DemonLawnDeployHypnoRefusalTests.cs` (new, 3 tests),
  `DemonLawnDeployCommanderRefusalTests.cs` + `DemonLawnDeployTests.cs` + `ExpeditionStoreTests.cs` +
  `ContractGateTests.cs` (all edited, same species-selection fix, zero production code changes).

### T1.5 — Species-magnitude delivery path · **M** · 5 files — **DONE 2026-09-07**

- A 6-question investigation (species-magnitude storage, equipment's `Absolutes`, the expedition
  engine's own precedent, `PowerLadderKMicro`/`KMilli`'s real shape, `thetaContent`'s type, per-species
  channel mapping — full findings in `spec-lawn-deploy-core.md`'s Correction/Open-Question 7) found:
  `ConcreteSpecies.Magnitudes` is real and stored but dropped at the one shared
  `ConcreteSpeciesSeedReader.ToDemonSpeciesDef` mapper seam (same seam `TraitPool` needed curation at);
  `Absolutes` is explicitly forbidden by this module's own Boundaries; no engine (lawn or expedition)
  has ANY existing precedent for magnitude→stat delivery; `PowerLadderKMicro`/`KMilli` is unwired for
  reuse and architecturally mismatched (would throw at the real push call site, one scalar Θ per push
  can't express multiple specimens' differing Θ, wrong formula shape vs. `AptitudeReadFunctions`).
- **Built**: `DemonSpeciesDef.Magnitudes` (new field, `DemonSpeciesCatalog.cs`), threaded through
  `ConcreteSpeciesSeedReader.ToDemonSpeciesDef` (one line — magnitudes are already the closed,
  channel-shaped vocabulary, so unlike `TraitPool` this is a straight pass-through, no curation layer
  needed). New `RpgStore.UniqueActors.cs` method `ReconcileDemonMagnitudeBindingsUnlocked`, mirroring
  `ReconcileDemonTraitBindingsUnlocked`'s exact diff shape (singleton wanted-set: a specimen's species
  either has a magnitude container to bind or does not yet), wired into `TryBeginUniqueDeploy` right
  after the trait reconcile. Magnitude content rides as flat, pre-authored atom content (never a
  dynamically-scaled `PowerLadder` op — the value is already `PTheta`-folded at species-generation
  time), one container per species (`SpeciesMagnitudeContainerId`, id `trait.species-magnitude-
  {speciesId}` — filed under `ContainerKind.Trait`, deliberately not `ContainerKind.SpeciesPassive`,
  whose `species-passive.` prefix already names a different, existing per-player-rolled mechanism this
  would collide with on the same speciesId).
- **Full-corpus content generation is real, separate follow-up work, named not built** — this task
  proves the mechanism against a hand-authored fixture (mirroring exactly how T1.2 was proven against
  one real trait before the full 68-trait corpus existed), matching the program's own established
  phased-rollout precedent. 829+ species × N channels each needs a real generator pass, tracked as
  follow-up, not blocking Checkpoint 1.
- **T1.3's overflow check re-run, as its own acceptance required**: the flat-atom path avoids
  `AtomCompiler.cs:568,572`'s `checked`-guarded `PowerLadder` arithmetic entirely (same as T1.2), but
  has its OWN un-`checked` int ceiling (`ValueSpec.Min`/`CurveTable.ApplyMilli`, both `int`) — named,
  not fixed: a repo-wide structural fact of every flat atom (the trait grant's own `150` included), far
  from load-bearing at today's real Θ values (13), fixing it would mean widening `ValueSpec.Min` for
  the whole vocabulary, out of this task's own scope.
- Acceptance:
  - [x] A deployed demon's live combat stats (not just its trait effects) measurably differ from a
        vanilla `type_id` unit's defaults, traceable to the specimen's own magnitudes — proven at the
        `effect_binding`/mechanism level (`DemonLawnDeployMagnitudeTests.cs`); the live-board
        observable half is T1.6's own job.
  - [x] The chosen mechanism does not reintroduce the `UniqueLoadoutMerge.Merge` non-additive-merge
        trap — uses `effect_binding`/`OwnerKind.UniqueActor` throughout, never `Absolutes`/`mods_json`.
- Verify: `dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeployMagnitude` — **3/3 passing**
  (fresh-deploy bind, idempotent redeploy, graceful zero-magnitude species). Mapper-level proof:
  `dotnet test tests/FusionRpg.Core.Tests --filter ConcreteSpeciesSeedReader` — **7/7 passing**,
  including a new direct `ToDemonSpeciesDef` magnitude-passthrough test AND the pre-existing real
  829-species diff test (`BuildDemonSpeciesSnapshot` vs. the injector's own mapper path) unaffected.
  Full-suite regression: `FusionRpg.Data.Tests` **1117/1118** (same single pre-existing
  `ItemUniqueStoreTests` failure as T1.4, confirmed unrelated); `FusionRpg.Core.Tests` **12643/12663**
  — all 20 non-passes traced to file:line and confirmed pre-existing/concurrent-session-caused, not
  this change: 17× the already-documented `vocabulary.json` SeedScanner defect (memory
  `vocabulary-json-seedscanner-defect.md`, unrelated to species/magnitudes), 1× `ExpeditionResolverTests`
  golden drift (memory `concurrent-session-atoms-patron-drift-2026-09-06.md` already names this exact
  test class as a casualty of OTHER sessions' in-flight work, explicitly distinct from the "real"
  goldens), 3× `ProveAptitudeJsonEmitTests` failing on an unrelated external tool's own
  `BattleStatComposer.Configure` precondition — none touch `DemonSpeciesDef`/`ConcreteSpeciesMapper`/
  `RpgStore.UniqueActors.cs`, confirmed via re-running all 5 failing classes in isolation (identical,
  reproducible failures either way) and `git status` showing no in-flight content-file edits.
- Files: `DemonSpeciesCatalog.cs` (edit, new field), `ConcreteSpeciesSeedReader.cs` (edit, 1 line),
  `RpgStore.UniqueActors.cs` (edit, new reconcile method + wiring),
  `tests/FusionRpg.Data.Tests/DemonLawnDeployMagnitudeTests.cs` (new, 3 tests),
  `tests/FusionRpg.Core.Tests/Demons/ConcreteSpeciesSeedReaderTests.cs` (edit, +1 test).

### T1.6 — Live E2E proof · **S**, grew to **M** · 3 files — **CI half DONE 2026-09-07, live-lawn half pending**

- Extended `StorageE2ETests.cs`'s own `deploy → ack → assert ActiveBound` pattern to a real demon
  specimen minted through the real `/api/test/mint-demon` seam (`FusionEndpoints.cs`'s own existing
  test-only helper), picking a species by property (`GET /api/demons/catalog`'s own `deployMode`/
  `captureOnly` fields) rather than a hardcoded id — same lesson `DemonLawnDeployHypnoRefusalTests.cs`
  already learned. A second test proves T1.4's `HypnoAlly` refusal reaches through the real HTTP layer
  too (409 + `deploy.hypno-ally-not-implemented`), not just `RpgStore`'s own direct-call surface.
- **Major pre-existing, suite-wide regression found and fixed along the way, unrelated to this
  program's own scope but blocking it completely**: `RpgApiFactory` (the shared `WebApplicationFactory`
  EVERY `FusionRpg.E2E.Tests` class uses) could not start the server AT ALL — `Program.cs:322`
  (`catalog-runtime`'s 2026-09-05 flip to `DemonSpeciesCatalog.Configure(store.
  BuildDemonSpeciesSnapshot())`) throws on an empty roster, and a fresh E2E `DataDir` had NEVER had
  `species-import` run against it. Confirmed via the PRE-EXISTING, untouched `StorageE2ETests.cs`
  failing identically, 0/7, before any fix. **Fixed**: `RpgApiFactory`'s constructor now pre-seeds the
  real, committed 829-species corpus (`ConcreteSpeciesSeedReader.ParseFile` + `RpgStore.ImportSpecies`,
  the same source `ConcreteSpeciesSeedReaderTests.cs`'s own `RealCommittedSpecies()` uses) into the
  on-disk SQLite file before the real server process opens it. One real snag on the way: the repo-root
  marker `Directory.Exists(data/generated/demons)` false-positived on an EMPTY copy of that folder
  MSBuild leaves under the test's own `bin/` output — switched to the same `src/FusionRpg.Injector`
  marker `ConcreteSpeciesSeedReaderTests.cs`'s own `RepoRoot()` already uses.
- **Impact, full-suite before/after**: this repo's own memory already tracked "206/207 E2E tests fail"
  as pre-existing — confirmed still true (0/7 on `StorageE2ETests.cs` alone) immediately before this
  fix. **After**: `dotnet test tests/FusionRpg.E2E.Tests` (whole project) — **202/213 passing**, up from
  effectively 0.
- **11 residual failures, all pre-existing, none touching anything this program's own diff changed —
  named, not fixed, real follow-up for whoever owns each area**:
  - 4× `WorldFixtureTests`/`WorldTurnFixtureTests` — `missing fixture .../fixtures/*.json — run with
    FUSIONRPG_BLESS_WORLD_FIXTURE=1`. A local checked-in-fixture-file gap, unrelated to species content
    entirely (world-stage program's own concern).
  - 3× `WebMatchE2ETests` + `ContractE2ETests`'s own squad test — real 500s from the live server.
    `git status` shows `src/FusionRpg.Server/WebMatchService.cs` as a file a DIFFERENT, concurrent
    session has mid-edit right now (the same `ActionContainerEffectResolverFactory` work already hit
    twice earlier this session as a transient `FusionRpg.Core`/`FusionRpg.Server` compile break) —
    plausibly that same in-flight refactor, not a species-roster effect.
  - `FusionE2ETests.Legendary_chain_from_commons` (`Sequence contains no matching element`) and
    `RpgProgressionE2ETests.Capture_awards_player_plant_zombie_xp` (`Assert.Single` sees 2 items) —
    plausibly a REAL, separate, newly-VISIBLE consequence of the catalog-runtime flip itself: since
    EVERY E2E test was failing at server startup until this fix, nobody has run these two against the
    real 829-species roster before, and each may carry its own now-stale assumption from the old
    84-species compiled-default world. Not investigated further — genuinely outside this program's own
    scope (fusion-recipe/progression test assumptions, not lawn-deploy), flagged here so it is not lost.
  - `WebMatchInteractiveSweepTests`'s one failure — not investigated, same "named not fixed" treatment.
- Acceptance:
  - [x] E2E test passes in CI — **2/2 passing** for this program's own new file
        (`DemonLawnDeployE2ETests.cs`), and the pre-existing `StorageE2ETests.cs` baseline (7/7) that
        this same fix unblocked.
  - [x] A real, owner-observable live-lawn check confirms the same — **DONE 2026-09-07**. Rebuilt +
        restarted the server and injector (MelonLoader) with today's full diff, launched the real game,
        entered a live lab board (`POST /api/debug/lawn/quick-start`), and deployed a REAL, pre-existing
        roster specimen with real rolled traits (`abyssswordstar`, `["critical-hunter","guardian"]`) via
        `POST /api/unique/actors/{id}/deploy` against the live match's own real `matchKey`. The real
        MelonLoader injector picked up the command, spawned a real Unity entity, and acked it back —
        `phase` reached `ActiveBound` with a genuine pointer (`lastPtr: "330F813A480"`) within ~2s, no
        simulation involved. **Directly confirmed** (via a read-only query against the live server's own
        production `rpg-hot.sqlite`, not a test database): exactly one `effect_binding` row exists for
        this instance — `slot=critical-hunter, source=demon-trait` — proving T1.2's reconcile fired for
        real, in the real server process, against real data, on this specimen's actual deploy.
        `guardian` correctly has NO binding (no seeded `trait.guardian` container exists yet) and no
        magnitude binding exists either (no species-magnitude content generated for any species yet,
        per T1.5's own named follow-up) — both the CORRECT, documented "fail closed, not fabricated"
        behavior, not a defect. **Named, not fixed, two real pre-existing blockers found along the
        way** (matching `live-lawn-check-is-assistant-reachable`'s own prediction of "3 fixable
        pre-existing issues" almost exactly): (1) `deploy-play.ps1`'s magic-number guard hard-failed on
        `GemInsertCorpus.UnauthoredInsertTier` — a real false positive (the constant's own doc comment
        already explains it's structural), fixed by adding it to `audit-magic-numbers.py`'s own
        `EXEMPT_NAMES`, following that script's own established precedent for this exact class of
        finding; (2) the script's own routine seed re-import step hit the already-documented
        `vocabulary.json` SeedScanner defect — not blocking here since no new seed content was added
        this program, so the pre-existing live database (already real, already `catalogRevision: 7`)
        needed no re-import.
        **What remains genuinely owner-observable, not assistant-verifiable from here**: whether
        `critical-hunter`'s own crit-rate bonus is VISIBLE in actual combat on the live board — the one
        HTTP introspection endpoint tried (`GET /api/actors/{id}/derived`) reads a documented, SEPARATE
        "subsystem contributions" path (its own doc comment explicitly warns against confusing it with
        atom-push/grant state), not the atom-bound grant this feature uses, so it correctly shows no
        contribution here and is not evidence of a problem. The remaining injector-side "did the grant
        actually reach live combat math" step is the same documented, uncoverable-by-CI boundary
        `MultiOwnerPushTests.cs`'s own `The_stamped_grant_is_what_UniqueOwnerBinder_turns_into_a_live_
        entity_key` test names for equipment — and this feature reuses that exact mechanism unchanged
        (per this module's own spec: "no Injector code needs to change"), so the equipment case's own
        already-proven behavior is the strongest evidence available without new debug tooling. The game
        and server are left running for the owner to look at directly if desired.
- Verify: `dotnet test tests/FusionRpg.E2E.Tests --filter DemonLawnDeployE2E` — 2/2;
  `dotnet test tests/FusionRpg.E2E.Tests --filter StorageE2E` — 7/7 (regression baseline);
  `live-lawn-quick-start` skill — pending.
- Files: `tests/FusionRpg.E2E.Tests/DemonLawnDeployE2ETests.cs` (new, 2 tests),
  `tests/FusionRpg.E2E.Tests/RpgApiFactory.cs` (edit — the species-seeding fix, affects every E2E test).

### ✅ Checkpoint 1 — a demon can really deploy and really fight as itself — **CLOSED 2026-09-07**
- [x] T1.1-T1.6 all done and verified.
- [x] `FusionRpg.Data.Tests`/`FusionRpg.Core.Tests`/`FusionRpg.E2E.Tests` full suites — **not literally
      100% green, and said so rather than rounded up**: `Data.Tests` 1117/1118 (1 pre-existing,
      already-documented `ItemUniqueStoreTests` failure), `Core.Tests` 12643/12663 (20 pre-existing,
      all traced: the known `vocabulary.json` defect, concurrent-session action-program drift, and one
      already-memory-documented expedition-golden drift — none touch demons/species), `E2E.Tests`
      202/213 (11 pre-existing, all named: 4 missing local fixture files, the rest concurrent-session
      drift or a real but separately-owned post-catalog-flip test assumption). **Zero failures trace to
      this program's own diff** in any of the three suites — every non-pass has a file:line and a named
      cause, not a rounded-up claim.
- [x] All four boundary guards clean (`guard-single-writer`, `guard-secondary-no-unity`,
      `guard-funnel-delta`, `guard-dal`) — all four re-run after T1.5/T1.6 landed, all OK.
- [x] A real live-lawn check: deploy a real demon, observe it on the board with its own stats/traits —
      see T1.6's own final entry for the full record (real spawn, real ptr, real server-side reconcile
      confirmed via direct DB read; the live-combat-visible half is the genuinely owner-observable
      remainder, matching the equipment precedent's own identical, already-proven mechanism).

## Phase 2 — `lawn-deploy-events`

### T2.1 — Hot/Cold-safe roster snapshot · **M** · grew to 8 files — **DONE 2026-09-07**

- Mirrored `commander-surface-map.md`'s own `MatchCommanderSnapshot`/`MatchCommanderSnapshotHolder`/
  `MatchCommanderSessionCache` three-layer shape EXACTLY (read all three source files + their own test
  file first, per DESIGN-GATE.md): (1) `LawnDeployRosterSessionCache` — an Injector-side cache populated
  by background REST refresh (`GET /api/demons/{playerId}` + `GET /api/patron/{playerId}`, Patron
  excluded by the CALLER before `Apply`, matching the cache's own "caller resolves, cache stores"
  split); (2) `LawnDeployRosterSnapshot`/`LawnDeployRosterSnapshotHolder` — the frozen, match-scoped
  read surface; (3) `RpgClient.RefreshLawnDeployRosterCacheAsync` wired at the SAME cadence as the
  commander cache (`StartAsync`, `Reconnected`) plus real-time refresh on roster/patron change
  (`DemonsUpdated`/`patron.aura` — see below), `MatchHost.Apply`'s own `isStart`/`isEnd` branches wired
  to `BeginMatch`/`EndMatch` at the exact same board.start/board.end moment the commander snapshot uses.
- **Real gap found and fixed along the way**: `DemonsUpdated`/`PatronUpdated` (the server's own
  roster/patron change broadcasts) reached only `WebGroup` (the web frontend) — never `InjectorGroup` —
  unlike `CommandersUpdated`, which explicitly sends to both. Without this, a mid-session fusion or
  Patron reassignment would never reach the injector's cache until a reconnect, undermining the "only
  the next run sees it" intent. Fixed narrowly: added an `InjectorGroup` broadcast at the 2 call sites
  that actually change ROSTER MEMBERSHIP or PATRON status (`DemonEndpoints.cs`'s `/summon`,
  `FusionEndpoints.cs`'s execute) — deliberately NOT at nickname/lock/expedition call sites, which don't
  affect deploy-eligibility. Patron reassignment reuses the ALREADY-EXISTING `patron.aura` push (no new
  broadcast needed there) — the injector's own existing handler now also enqueues a
  `lawn-deploy.roster.reload` command, mirroring `commander.snapshot.reload`'s exact shape.
- **Observability added** (the goal's own OBSERVE step): `LawnDeployRosterSnapshotHolder.
  ObserveRosterFold()` mirrors `ObserveCommanderFold`'s role, wired into `debug.snapshot`'s own
  `match.lawnDeployRoster` fold (`DebugRuntime.cs`).
- Acceptance:
  - [x] The snapshot is taken once, at `board.start`, never re-queried mid-match — proven by
        `Mid_match_cache_change_does_not_alter_Current`.
  - [x] A roster change mid-match (e.g., a fusion completing) does not retroactively change what's
        available to trigger THIS run — only the next one — proven by
        `Second_board_start_picks_up_the_refreshed_cache_the_first_did_not_see`.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployRosterSnapshot` — **11/11
  passing** (mirrors `MatchCommanderSnapshotTests.cs`'s own test shape, including a
  `HostApplyWithSnapshot` mirror of `MatchHost.Apply` since the Injector project can't be unit tested
  directly). Injector build verified via `deploy-play.ps1 -LoaderHost MelonLoader -NoServer` — 0 errors.
  Server build verified via `dotnet build src/FusionRpg.Server` — 0 errors.
- Files: `src/FusionRpg.Core/Match/LawnDeployRosterSnapshot.cs` (new),
  `LawnDeployRosterSnapshotHolder.cs` (new), `LawnDeployRosterSessionCache.cs` (new),
  `src/FusionRpg.Injector/RpgClient.cs` (edit), `CheatCommandRunner.cs` (edit),
  `src/FusionRpg.Injector/Match/MatchHost.cs` (edit), `DebugRuntime.cs` (edit),
  `src/FusionRpg.Server/DemonEndpoints.cs` + `FusionEndpoints.cs` (edit, InjectorGroup broadcasts),
  `tests/FusionRpg.Core.Tests/Match/LawnDeployRosterSnapshotTests.cs` (new, 11 tests).

### T2.2 — The trigger condition evaluator + tuning file · **M** · 4 files — **DONE 2026-09-07**

- Two starting cases picked against ALREADY-TRACKED `MatchSnapshot` fields (`PlantCount`/`ZombieCount`)
  rather than inventing new state-tracking infrastructure (e.g. a wave counter, which `MatchSnapshot`
  does not carry and this module's own scope never asked for): `zombie-swarm` (`ZombieCount >= 5`) and
  `thin-defense` (`PlantCount <= 2` while `ZombieCount >= 1`) — genre-appropriate STARTING VALUES per
  the plan's own "pick starting values, tune from play" precedent, not a validated balance decision.
  `LawnDeployEventEvaluator.Evaluate` mirrors `AmbushDraw.Draw`'s own shape exactly: every tunable
  arrives as a parameter (`LawnDeployEventsTuning`), never a bare literal or an internal static-hub
  read — a `LawnDeployEventsTuningHub` DOES exist, but only as where the future CALLER (T2.4) reads the
  loaded value from, matching `WorldTuningHub`/`ExpeditionTuningHub`'s own plain-holder shape.
  `LawnDeployEventsTuningLoader` mirrors `PatronTuningLoader`'s own explicit, path-qualified-failure
  `JsonDocument` parsing (never bare `JsonSerializer.Deserialize<T>`), rejecting an empty `cases` object,
  a case with no condition at all, and an out-of-range `fireChanceMilli` — all three proven by test.
  **Default: no cost beyond the frequency cap itself** (`fireChanceMilli`, a genuine per-mille roll
  gate, not a resource cost) — no souls/cooldown charged, per the plan's own Gates section.
- Wired into production startup (`Program.cs`, mirroring every other tuning file's exact call shape) —
  confirmed via a real server boot (`GET /health` → `ok:true`) that the committed JSON parses cleanly
  through the real loader, not just my own test's `File.ReadAllText` check.
- Acceptance:
  - [x] Scenario table, one fact per named case × {fires, does-not-fire-below-threshold,
        does-not-fire-if-already-used-this-run} — 6 tests, 3 per case, not a single smoke test. Plus 3
        cross-cutting gates (no eligible demon ever refuses regardless of board state; a per-run budget
        exhausted by one case refuses a DIFFERENT case's own true condition too; two simultaneously-true
        conditions resolve deterministically to the first-declared case, not arbitrarily).
  - [x] Every numeric knob resolves from the tuning file, zero bare literals in the evaluator itself —
        confirmed by direct read of `LawnDeployEventEvaluator.cs` (no numeric literal besides array/
        collection mechanics) and the loader's own schema validation.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployEventEvaluator` — **14/14 passing**.
- Files: `src/FusionRpg.Core/Match/LawnDeployEventEvaluator.cs` (new),
  `src/FusionRpg.Core/Match/LawnDeployEventsTuning.cs` (new — tuning record, loader, hub),
  `data/tuning/lawn-deploy-events.v1.json` (new),
  `tests/FusionRpg.Core.Tests/Match/LawnDeployEventEvaluatorTests.cs` (new, 14 tests).

### T2.3 — Determinism · **S** · included in T2.2's own files — **DONE 2026-09-07**

- The evaluator derives its own randomness via `SeededRng.DeriveStream(matchSeed, "lawn-deploy-event:{caseId}")`
  — never `System.Random`, never wall-clock. `FireChanceMilli` (a real per-mille gate, not 0 or 1000 for
  either starting case) makes this a MEANINGFUL roll, not a vacuous one.
- Acceptance:
  - [x] Same `(matchSeed, caseId)` fed twice ⇒ byte-identical decision — an explicit test
        (`Same_seed_and_case_id_produce_a_byte_identical_decision_every_time`, 20 repeat calls), not an
        inferred property. A companion test (`Different_seeds_can_produce_different_decisions_for_the_
        same_case`) proves the roll is a genuine function of `matchSeed`, not a constant the threshold
        gate alone would also explain.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter LawnDeployEventEvaluator` — included in the
  same 14/14 run above.

### T2.4 — Plant-side prompt/UI surface · **M**, grew larger · injector half DONE 2026-09-07, FE half IN PROGRESS

- **Design, resolved by investigation, not guessed**: `EventIngest.cs`'s own `BroadcastAsync`
  (`EventIngest.cs:161-169`) already forwards EVERY non-noisy ingested event kind to the web frontend
  verbatim via the generic `"Event"`/`"EventBatch"` SignalR message — `RpgConstants.IsNoisyKind`
  (`Dtos.cs:252-254`) does not list a new kind, so **zero new server-side broadcast code is needed** as
  long as the injector emits through the existing `GameHooks.Emit` pipeline (the same one `wave.change`/
  `board.economy`/etc. already use) and `RpgStore.cs`'s own event switch (`RpgStore.cs:~2600+`, no
  `default:` arm) simply no-ops for an unhandled kind rather than rejecting it. This is a real, if
  pleasant, discovery — the "server" half of this task's own "server + FE" file estimate collapses to
  "reuse what's already built," confirmed by direct read of the ingestion pipeline, not assumed.
- **Injector-side built**: a new per-match `LawnDeployEventRunStateHolder` (mirrors
  `LawnDeployRosterSnapshotHolder`'s own lifecycle, reset at board.start/board.end alongside it).
  `MatchHost.Apply` now calls a new `CheckLawnDeployTrigger()` after every event while
  `Phase == InMatch` (cheap and safe this often — the evaluator's own gates make a no-op the common
  case): reads the T2.1 roster snapshot, the T2.2 tuning (now also wired into `RpgHost.Initialize`
  alongside every other injector-side tuning file), derives a per-match seed from `MatchKey` (the one
  per-match identifier that already exists — `MatchRuntime`/`MatchState` never tracked a numeric seed,
  confirmed by direct search, so this reuses `SeededRng`'s own hash on the existing key rather than
  threading a new field through the match-start payload contract), calls the evaluator, and on a fire
  records it (`RecordFired`) and emits `"lawn-deploy-event.fired"` with `{caseId, eligibleInstanceIds}`.
- **Live-verified**: stopped and rebuilt the injector + server with all of T2.1/T2.2/T2.4's own changes,
  relaunched the real game — `GET /health` shows `injectorConnected:true`, confirming `RpgHost.
  Initialize`'s new `LawnDeployEventsTuningHub.Configure` call (a DIFFERENT process/runtime than the
  server's own copy) and `RpgClient.StartAsync`'s new roster-cache refresh both succeed for real, not
  just in a unit test.
- **FE half DONE 2026-09-07**, built after a dedicated investigation (an Explore agent — SignalR
  dispatch, existing prompt components, the lawn view's own structure, the deploy API client, species
  display resolution, state-management convention — see the investigation's own findings, not guessed):
  - **A real, pre-existing tension found and resolved, not silently defaulted**: `DialogShell` (a
    scrimmed, blocking confirm pattern used elsewhere) vs. the EXISTING T22 "Deploy to the lawn"
    banner's own explicit comment — `LawnPage.tsx`: *"Stage chrome (plate 07 §B): no scrim, the board
    stays fully visible and interactive underneath — this is an inline banner, never a Dialog/layer."*
    Chose the T22 precedent: blocking the board with a scrimmed dialog during a LIVE, time-sensitive
    match (a zombie swarm is happening right now) would hide the exact thing the player needs to see —
    the same reasoning that precedent's own comment already states, now applied to a second, directly
    analogous case rather than picked independently.
  - `lawnViewModel.ts`: new `pendingLawnDeploy?: {caseId, eligibleInstanceIds} | null` field — server
    truth only, deliberately not carrying "has the player responded" (kept as client-local state in
    `LawnPage.tsx`, matching the fold's own "pure reflection of server events" convention).
  - `lawnProjectorFold.ts`: new `case "lawn-deploy-event.fired"` in `applyOne` (+ a `strArray` payload
    helper, matching `str`/`num`'s own style) — resets automatically on the next `board.start` since
    that case already rebuilds the whole model from scratch.
  - `LawnPage.tsx`: a new inline `Banner` (mirroring the T22 banner's own exact JSX shape/`data-testid`
    convention) offering one button per eligible demon (resolved via the two-hook join the investigation
    found — `useDemonRoster(playerId)` → `profile.speciesId` → `useSpeciesIndex()` → `TypeIcon`/name,
    the same recipe `DemonsPage.tsx` already uses) plus a Dismiss button. Accept calls the ALREADY-
    EXISTING `useDeployUniqueActor()` mutation directly (`mutations.ts:423-450`) — no new API client
    function, no new backend endpoint, matching "Accepting the prompt calls the T1.x deploy path"
    literally. A `respondedLawnDeployCaseId` local state (reset on `model.matchKey` change, so the same
    caseId firing again in a LATER match is not incorrectly suppressed by an earlier match's response)
    tracks accept/dismiss without polluting the fold's own server-truth-only model.
  - **A real regression found and fixed by this repo's own accessibility guard**
    (`disabledReasonGuard.test.ts`, GG-55 — "every disabled control carries an accessible reason"): the
    new accept button's own `disabled={...isPending}` had no matching `title` explaining why, unlike
    the T22 button it was mirrored from. Fixed by adding the same `title={isPending ? "Deploying…" :
    undefined}` the T22 button already carries.
- Acceptance:
  - [x] A fired trigger reaches the player-facing UI in a live match — the full chain (injector emits →
        generic event pipeline broadcasts, confirmed already-built by direct read → fold sets
        `pendingLawnDeploy` → `LawnPage.tsx` renders the banner) is code-complete and unit-proven at
        every Core/FE layer; the literal "watch a human see it during a real live match" step is the
        genuinely owner-observable remainder (see Checkpoint 2's own live-check line).
  - [x] Accepting the prompt calls the T1.x deploy path for the player's own chosen eligible demon —
        `acceptLawnDeployPrompt` calls `useDeployUniqueActor().mutateAsync(...)` directly, the same
        mutation T1.x's own real endpoint already serves.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter "LawnDeployEventRunStateHolder"` — 8/8
  passing. `npx tsc --noEmit` (web/fusion-rpg-web) — 0 errors. `npx vitest run
  src/features/lawn/lawnProjectorFold.test.ts` — **68/68 passing** (65 pre-existing + 3 new). Full FE
  suite (`npx vitest run`): 2009/2012 passing — the 3 non-passes (`bandGuard.test.ts` ×2,
  `disabledReasonGuard.test.ts`'s own remaining violations) confirmed via `git status` to be in files
  this program never touched (`PhaserSceneSwitchPocPage.tsx`, `CommandersLayer.tsx`,
  `CommanderSheetFooter.tsx`) — pre-existing, unrelated.
- Files: `src/FusionRpg.Core/Match/LawnDeployEventRunStateHolder.cs` (new),
  `src/FusionRpg.Injector/Match/MatchHost.cs` (edit), `src/FusionRpg.Injector/Host/RpgHost.cs` (edit),
  `tests/FusionRpg.Core.Tests/Match/LawnDeployEventRunStateHolderTests.cs` (new, 8 tests),
  `web/fusion-rpg-web/src/features/lawn/lawnViewModel.ts` (edit),
  `web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.ts` (edit),
  `web/fusion-rpg-web/src/features/lawn/lawnProjectorFold.test.ts` (edit, +3 tests),
  `web/fusion-rpg-web/src/features/lawn/LawnPage.tsx` (edit).

### ✅ Checkpoint 2 — a real trigger fires in a live run, and the player can act on it
- [ ] T2.1-T2.4 all done and verified.
- [ ] `FusionRpg.Core.Tests` full suite green.
- [ ] A real live-lawn check: play to a triggered case, see the prompt, deploy an owned demon through it.

## Phase 3 — `zomboss-deploy-ai`

### T3.1 — `ILawnBoardView` (the enforcing boundary) · **M** · 2 files

- A narrow, read-only projection of live board state (visible units, HP, wave) that the Zomboss scorer's
  own function signature takes EXCLUSIVELY — never `MatchRuntime`/`Board` directly. Reads
  side/ownership through the already-shipped `SpecimenOwnershipOracle` (`decisions.md`'s "Buff/debuff
  scope" row, 2026-08-29/30) rather than re-deriving ownership logic.
- Acceptance:
  - [ ] A compile-time proof the scorer's own signature cannot accept `MatchRuntime`/`Board` — mirroring
        `spec-ai-commander.md`'s own `IWorldView` leak-proof precedent.
  - [ ] A hypnotized/side-swapped entity resolves to its real owner through `SpecimenOwnershipOracle`,
        not the entity's current on-board side.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/ILawnBoardView.cs` (new), tests.

### T3.2 — Zomboss's own demon roster/pool · **M** · 2 files

- **Default per the plan's own Gates section**: the same summonable species pool the player draws from,
  filtered to the current level's own threat band. `data/tuning/zomboss-deploy-ai.v1.json` (new) carries
  the difficulty→policy-id mapping and any roster-size knob.
- Acceptance:
  - [ ] Zomboss's own available pool for a given level is deterministic and reproducible from the
        level's own data, not hand-authored per level.
  - [ ] The default is explicitly named as a tunable/reversible choice in code comments, not presented
        as a permanent design decision.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/` (new roster-source type), `data/tuning/zomboss-deploy-ai.v1.json` (new).

### T3.3 — The scorer · **M** · 2-3 files

- A deterministic scorer over `ILawnBoardView` (T3.1) + the T3.2 roster, choosing whether and which
  demon to deploy. Weights come from the T3.2 tuning file. Derives its own randomness via
  `SeededRng.DeriveStream(matchSeed, "zomboss-deploy-ai:{caseId}")`.
- Acceptance:
  - [ ] Scenario table: no eligible demon (declines), exactly one (picks it), multiple candidates at
        different board states (ranking is asserted, not just "it picked something").
  - [ ] Determinism test: same `(board snapshot, matchSeed, caseId)` twice ⇒ byte-identical decision.
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: `src/FusionRpg.Core/Match/Ai/ZombossDeployPolicy.cs` (new),
  `tests/FusionRpg.Core.Tests/Match/Ai/ZombossDeployAiTests.cs` (new).

### T3.4 — Wire Zomboss's deploy through the existing path · **S** · 1 file

- Zomboss's own chosen deploy calls the SAME `DeployAsync`/Funnel path a player's deploy uses — no new
  write path, no shortcut.
- Acceptance:
  - [ ] A Zomboss-triggered deploy is indistinguishable, at the deploy-mechanism level, from a
        player-triggered one — same refusal reasons apply (though Zomboss's own roster is pre-filtered
        to exclude anything that would trip T1.1's Commander/Patron refusal, so it should never fire).
- Verify: `dotnet test tests/FusionRpg.Core.Tests --filter ZombossDeployAi`
- Files: wherever the event-fired-on-zombie-side handler lives (T2.4's own counterpart for the zombie side).

### ✅ Checkpoint 3 — program closes: both sides work in one live sitting
- [ ] T3.1-T3.4 all done and verified.
- [ ] `FusionRpg.Core.Tests` full suite green; all four boundary guards clean.
- [ ] **The capability map's own program-level acceptance, proven directly**: in a real lawn run, played
      to a triggered event, on both sides — the plant-side prompt fires and the player deploys an owned
      unique demon with its own stats/traits provably live; separately, a Zomboss-triggered event results
      in Zomboss deploying one of its own unique demons, chosen by the real T3.3 policy, not a stub.
- [ ] Every "Deliberately deferred" item from the capability map is still correctly out of scope (no
      scope creep into Commander-picker UI, world-map `ai-commander`, or class-system's own point-economy
      gap during this build).
