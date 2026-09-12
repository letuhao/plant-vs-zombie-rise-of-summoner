# Tasks: `data-test-substrate`

Plan: [data-test-substrate-plan.md](data-test-substrate-plan.md) · Map: [../docs/architecture/data-test-substrate-map.md](../docs/architecture/data-test-substrate-map.md)
Module 1 spec: [../docs/architecture/data-test-substrate/spec-memory-storage-plan.md](../docs/architecture/data-test-substrate/spec-memory-storage-plan.md)

> **Sizing note (deliberate, stated).** The migration tasks (T11–T18) are batches of **~8** files, not
> the skill's ~5. Each is the **same mechanical edit** (`DataTestStore.Create()` replaces the temp-dir
> ctor; `Dispose` replaces the swallowed delete), the pattern is proven by the pilot (T9) at 5 files
> before any batch runs, and `guard-test-substrate.ps1` fails the batch if any file is left leaking.
> The smaller batch is *safer per task*; the larger batch is *how the work actually gets done* without
> 26 near-identical tasks. Both constraints are met: no task changes behaviour, only substrate.

> **The gate already shipped** (`7183a59e`). Every batch **must remove the baseline line** for each
> file it fixes; a stale line fails the gate (`testing-standard.md` R4). A batch that adds a line is a
> review event.

## Phase 0 — Module specs ✅ COMPLETE (2026-09-12)

> All six module specs are written under `docs/architecture/data-test-substrate/`. Every module id in
> the map now traces to a spec, so the gated workflow's per-module Specify is satisfied before any
> build phase.

- [x] **Task T0a: spec-test-store-helper.md** — the helper's contract (factories, dispose semantics, failure-is-failure).
- [x] **Task T0b: spec-store-test-migration.md** — the migration contract (the recipe, the read-only conversion, the baseline shrink, the file-bound exclusion set).
- [x] **Task T0c: spec-disk-write-probe.md** — the probe contract (static gate shipped; the runtime leak alarm + 0-file assertion + delta-not-count rule).
- [x] **Task T0d: spec-archive-target.md** — the archive-target abstraction contract (create/open/list/exists/delete over file|memory; the four writers + purge).
- [x] **Task T0e: spec-substrate-standard.md** — the standard's contract (R1–R5 shipped; the `data-architecture.md` amendment + the no-read-only-memory rule).

## Phase 1 — Foundation: an in-memory store (module `memory-storage-plan`)

- [x] **Task T1: ADR row — the storage plan seam** ✅ 2026-09-12
  - Description: added the `decisions.md` row "`RpgStore` storage plan (2026-09-12)" beside the SQLite / DAL-single-gate rows; it locks file-default + memory-as-test-substrate, the three public doors, independent instances (no singleton), the URI-throw, the read-only limitation, and links the spec + standard.
  - Acceptance met: `decisions.md` carries the row; no existing locked row contradicts it (the SQLite + DAL rows are untouched and the new row restates the DAL boundary as unchanged).
  - Verified: row read at `decisions.md:84`.
  - Files: `docs/architecture/decisions.md`. Scope: XS.
  - Dependencies: none — spec approved 2026-09-12.

- [x] **Task T2: `SqliteConnectionFactory` memory-URI branch** ✅ 2026-09-12
  - Description: `MemoryUri(name)` builds `file:{name}?mode=memory&cache=shared`; `IsMemoryUri` detects it; `Open` routes a memory URI to `OpenMemory` (no `Path.GetFullPath`/`CreateDirectory`/forced `ReadWriteCreate`), which omits WAL (no-op on memory) and **throws** on `readOnly: true`. The file branch is byte-identical.
  - Acceptance met: a memory URI opens and round-trips SQL through two connections; a file path still creates its file/dir exactly as before; read-only+memory throws `InvalidOperationException` naming memory.
  - Verified: `MemoryStoragePlanTests` 5/5; full Data suite 1,264/1,264; Server suite 404/404; `guard-dal.ps1` + `guard-test-substrate.ps1` green.
  - Files: `src/FusionRpg.Data/Sqlite/SqliteConnectionFactory.cs`, `tests/FusionRpg.Data.Tests/MemoryStoragePlanTests.cs`. Scope: S.
  - Dependencies: T1.

- [x] **Task T3: `RpgStoreOptions` + three doors + keepers + `Dispose`** ✅ 2026-09-12
  - Description: public `RpgStoreOptions` record (`DataDir`, `InMemory`, `HotName`, `MediaName`) with `For`/`Resolve` (Resolve throws on a `file:`/`mode=memory` URI under the file plan); one private path resolution; the string ctor became `(string dataDir, bool inMemory = false)` so existing calls bind unchanged; public `RpgStore(RpgStoreOptions)`; static `RpgStore.InMemory()`; `IDisposable` releasing the two keepers (file plan no-op); memory stores get unique DB names per instance (no singleton).
  - Acceptance met: all three doors build an init-able store; production `new RpgStore(dir)` path computation unchanged; `Dispose` releases keepers; `inMemory:true` ignores and never creates `dataDir`.
  - Verified: gate subagent PASS — focused 9/9, full Data 1273/1273, both guards green, scope clean; keeper release proven by reflection probe.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.cs`, `src/FusionRpg.Data/Sqlite/RpgStoreOptions.cs`, `tests/FusionRpg.Data.Tests/RpgStoreStoragePlanTests.cs`. Scope: M.
  - Dependencies: T2.

- [x] **Task T4: `Init()` skip + archive throw + `Reset()` in memory** ✅ 2026-09-12
  - Description: `Init()` skips the four file-only steps under the memory plan; `ArchiveDir` and **every** archive entry point (`PromoteClosedRunCapture`, `CompactAfterRunClosed`, `TrimHotTailsNow`, `TrimSoulLedgerTails`, `DeleteArchives`, `PurgeClosedRunCapture`, `DeleteClosedRuns`) throw a dedicated **`StorePlanException`** before any early-return or filesystem work, so none can silently resolve a cwd-relative path; `Reset()` skips the filesystem archive branch and re-`Init()`s.
  - Acceptance met: memory `Init()` creates no file; every archive entry point throws `StorePlanException` (an `InvalidOperationException` naming the memory plan); `Reset()` leaves an empty usable store with no file; the file plan's archive still creates/reads (22/22 archive/purge/soul tests).
  - Verified: gate subagent PASS — focused 11/11, full Data 1275/1275, both guards green, scope clean.
  - Files: `src/FusionRpg.Data/Sqlite/{RpgStore,StorePlanException}.cs`, `RpgStore.Compaction.cs`, `RpgStore.Storage.cs`, `tests/FusionRpg.Data.Tests/RpgStoreStoragePlanTests.cs`. Scope: M.
  - Dependencies: T3.

- [x] **Task T5: Module-1 proof tests (the module's Definition of Done)** ✅ 2026-09-12
  - Description: completed the spec's Testing-strategy table — added the two missing rows: **`Memory_schema_is_identical_to_the_file_store`** (asserts the `sqlite_master` DDL row-set relationship, not a count) and **`Keeper_survives_a_global_ClearAllPools`**. Every other row already had a covering test from T2–T4.
  - Acceptance met: every spec testing row maps to an executed passing test; `RpgStoreSmokeTests`/`RpgStoreDalSmokeTests` pass **unmodified** (git-clean, green).
  - Verified: gate subagent PASS — spec-row mapping complete, focused 18/18, full Data 1277/1277, both guards green, no leaked parity dir.
  - Files: `tests/FusionRpg.Data.Tests/RpgStoreStoragePlanTests.cs`. Scope: M.
  - Dependencies: T4.
  - **Module 1 (`memory-storage-plan`) is complete** — Checkpoint 1 reached; production path unchanged.

### Checkpoint 1 — Foundation
- [ ] Data suite green; production smoke tests untouched; `guard-dal` + `guard-test-substrate` green; owner reviews the seam before migration starts.

## Phase 2 — The test helper (module `test-store-helper`)

- [x] **Task T6: `DataTestStore` core** ✅ 2026-09-12
  - Description: `DataTestStore` wraps `RpgStore` with two factories — `Create()` (in-memory, `Init()`ed, no temp dir) and `CreateFileBacked()` (unique dir under the test output root, initialized) — and implements `IDisposable`/`IAsyncDisposable`. Its file-plan dispose clears SQLite pools before `Directory.Delete` and has **no catch**.
  - Acceptance met: both factories return an init-able store; `Create()` creates no temp dir and no file; `CreateFileBacked()` uses a unique dir; dispose leaks nothing (gate-verified 0 surviving `teststore-*` dirs); no gate baseline entry.
  - Verified: gate subagent PASS — focused 5/5, full Data 1282/1282, both guards green, scope clean.
  - Files: `tests/FusionRpg.Data.Tests/DataTestStore.cs`, `tests/FusionRpg.Data.Tests/DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T5.
  - **Known gap → T7:** the "failed delete throws" runtime behavior is not yet proven by a test (static inspection only). T7 owns it.

- [x] **Task T7: Leak-proof file cleanup (the R3 rule, mechanized)** ✅ 2026-09-12
  - Description: T6 already implemented the throwing delete; this task **proves** it at runtime. Added two tests to `DataTestStoreTests`: `A_file_store_with_a_pooled_connection_still_deletes_after_ClearAllPools` (a pooled connection holds the file handle; the helper still deletes) and `A_failed_file_cleanup_throws_rather_than_being_swallowed` (a `FileShare.None` blocker file makes `Directory.Delete` throw `IOException`, which the helper lets surface).
  - Acceptance met: a held-open file store still deletes after `ClearAllPools`; a simulated failure surfaces as an exception. The failure test is **not vacuous** — reintroducing a `catch { }` makes it fail (proven by temporarily doing so, then reverting).
  - Verified: gate subagent PASS — focused 7/7 stable across 3 runs, full Data 1284/1284, both guards green, `DataTestStore.cs` byte-identical to HEAD, no leaked dir.
  - Files: `tests/FusionRpg.Data.Tests/DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T6.

- [x] **Task T8: Helper proof tests** ✅ 2026-09-12
  - Description: each helper criterion now has its own DAMP test — memory store no-file (`Create_creates_no_temp_directory`, `Create_returns_an_initialized_memory_store_that_round_trips_sql`), file store deletes cleanly (`A_file_store_dispose_deletes_its_directory_cleanly`, added here), failure-not-swallowed (`A_failed_file_cleanup_throws_rather_than_being_swallowed`), and no `guard-test-substrate` baseline entry (`Memory_helper_adds_no_substrate_gate_entry`).
  - Acceptance met: all helper assertions pass; the baseline has no `DataTestStore` line.
  - Verified: gate re-run PASS after the concurrent-worktree regression was cleared — focused 8/8, full Data 1286/1286, both guards green, no leaked dir.
  - Files: `tests/FusionRpg.Data.Tests/DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T7.
  - **Process note:** this test was swept into the concurrent `59ff9941` ("add stale files") commit by another stream before it could get its own increment; content is in HEAD and green, but the per-task rollback boundary for T8 is not clean.

### Checkpoint 2 — Helper ✅
- [ ] Helper green; gate still green; owner reviews the helper API before the migration.

## Phase 3 — Pilot: prove the pattern at minimum blast radius

- [x] **Task T9: Migrate 5 representative store tests to memory** ✅ 2026-09-12
  - Description: migrated `ActionStoreTests`, `AffixStoreTests`, `AtomStoreTests`, `CurveStoreTests`, `ElementStoreTests` from `new RpgStore(tempDir)` + `Init()` + swallowed delete to `DataTestStore.Create()` / `_testStore.Dispose()`. `ActionStoreTests`' 2 `readOnly: true` opens on `_store.HotPath` became plain opens (a shared-cache memory DB cannot be opened read-only). The 5 baseline lines were removed (216→211).
  - Acceptance met: all 5 pass **in memory** (68/68) with **no assertion and no constructor call changed**; zero temp dirs created; baseline shrank exactly 5; gate green.
  - Verified: gate subagent PASS — per-file diff confirms only ctor/Dispose/read-open changed and no `SeedAtoms()`/seed call was dropped (Action 6/6 seeds, 51/51 asserts; Affix 2/2, 24/24; Atom 39/39; Curve 18/18; Element 25/25); full Data 1286/1286; both guards green.
  - **Pattern proven.** The migration recipe works at minimum blast radius — the pilot is the gate for the remaining batches.
  - Files: the 5 test files + `scripts/test-substrate-baseline.txt`. Scope: M.
  - Dependencies: T8.

- [x] **Task T10: Pilot checkpoint — pattern proven or reverted** ✅ 2026-09-12
  - Description: confirmed at T9's gate — **zero assertion changes**, every constructor seed call preserved, baseline shrank by exactly **5** lines (216→211), zero temp dirs created, duration ~5s for the 68 pilot tests, full Data 1286/1286.
  - Acceptance met: no mis-classification found; the recipe is proven and the pattern gates the remaining batches.
  - Verified: gate subagent PASS (per-file diff + HEAD-vs-worktree token counts).
  - Files: none (verification). Scope: XS.
  - Dependencies: T9.

### Checkpoint 3 — Pilot ✅
- [x] ⭐ Zero assertion drift across 5 files; pattern proven before the bulk migration.

## Phase 4 — The migration (module `store-test-migration`)

> **The migration recipe (every batch task does exactly this):**
> 1. `new RpgStore(<temp dir>)` (+ `Directory.CreateDirectory`) → `DataTestStore.Create()`.
> 2. `Dispose`'s `try { Directory.Delete(...) } catch { }` → the helper's dispose (delete the whole
>    temp-dir block).
> 3. **`SqliteConnectionFactory.Open(_store.HotPath, readOnly: true)` → plain open**
>    (`SqliteConnectionFactory.Open(_store.HotPath)`) — a shared-cache memory DB cannot be opened
>    read-only. **Keep** read-only if the file is file-bound (only `ColdArchiveCompactionTests` is).
>    The 9 memory-bound sites and the batch that owns each:
>    `AllocationStoreTests` (T11) · `ChannelPolicyStoreTests` (T12) · `GateCounterStoreTests` (T15) ·
>    `TreeStateVolumeTests` (T17) · `ItemGrantStoreTests` (T18b) · `DomainImportTests`,
>    `DomainProgressStoreTests` (T18c) · `ActionStoreTests` ×2 (T9, the pilot).
> 4. Delete each fixed file's line from `scripts/test-substrate-baseline.txt` (the ratchet only shrinks).
> 5. Run the batch's `dotnet test` filter + `guard-test-substrate.ps1`; the gate must pass with the
>    baseline **smaller**, and no assertion outside steps 1–3 may change.
>
> **Exclude `CreatureSpeciesImportCliTests.cs`** (owned by `cold-process-test-build-e5b1`).

- [x] **Task T11: Data.Tests root batch A** (8: ActionCatalogBuilder, ActionCatalogStore, AffixImportPath, AllocationStore, AlmanacSeedEnrichment, AlmanacSeed, AptitudePreset, AtomInstance) — **read-only: `AllocationStoreTests`** ✅ 2026-09-12
  - Migrated all 8 to `DataTestStore.Create()` / helper dispose; `AllocationStoreTests`' read-only open became a plain open; `AptitudePresetStoreTests`' reopen uses the new `_testStore.Reopen()`; `ActionCatalogStoreTests`' local `NewStore()` helper is now helper-backed.
  - **The batch surfaced two shapes the pilot did not cover, so `DataTestStore` gained `Reopen()`** (address the same named memory DBs — the memory equivalent of a process restart), proven by `Reopen_sees_what_the_previous_store_committed` (non-vacuous). This preserves the "fresh store sees committed data" assertion in memory instead of mis-classifying those files as file-bound.
  - A real defect was caught by running the batch rather than trusting the edit: the read-only conversion for `AllocationStoreTests` had not applied (the replace ran before that file was migrated), and `Schema_storesInputsOnly_noResolvedChannelValueColumn` failed until fixed.
  - Verified: gate PASS — focused 93/93, no assertion/seed dropped (per-file token counts), baseline 211→203, `Reopen()` proven, full Data 1287/1287, both guards green.
  - Deps: T10. Scope: M.
- [x] **Task T12: Data.Tests root batch B** (8: AtomRowWiring, BindResolution, ChannelPolicyStore, ContainerStore, ContentBoot, ContentHashStore, ContractGate, ContractOps) — **read-only: `ChannelPolicyStoreTests`** ✅ 2026-09-12
  - Migrated all 8; `ChannelPolicyStoreTests`' read-only open → plain open; `ContentHashStoreTests`' local `NewStore()` → helper-backed (`List<DataTestStore>`), so its 25 call sites each get an independent in-memory store.
  - Verified: gate PASS — focused 103/103, zero assertions dropped (18/37/18/41/9/42/28/45 all unchanged), every constructor seed preserved (`Seed()`, `SeedAtoms()`, `AwardSouls(...)`), baseline 203→195 (−8), no old-prefix temp dirs, both guards green, full Data 1287/1287.
  - Deps: T10. Scope: M.
- [x] **Task T13: Data.Tests root batch C** (8: ContractRegression, ContractSettle, ContractStore, CreatureLawnDeployCommanderRefusal, CreatureLawnDeployHypnoRefusal, CreatureLawnDeployMagnitude, CreatureLawnDeploy, CreatureStore) ✅ 2026-09-12
  - Migrated all 8. **`CreatureLawnDeployTests` had a third shape the pilot didn't cover:** its `SimulatePromotionTraitChange` opened the raw hot file by path (`new SqliteConnection($"Data Source={Path.Combine(_dir, "rpg-hot.sqlite")}")`) — now `SqliteConnectionFactory.Open(_store.HotPath)`, which is memory-safe. No other file touched a raw path.
  - Verified: gate PASS on all substantive criteria — focused 42/42, zero assertions dropped (41/25/20/9/10/8/12/33 unchanged), seeds preserved (`AwardSouls` ×3, `ImportRealTraitSeed()`), baseline 195→187 (−8), no batch-C temp dirs, both guards green, full Data 1287/1287.
  - Deps: T10. Scope: M.
- [x] **Task T14: Data.Tests root batch D** (8: EligibilityAxisMigration, ExpeditionRewardApply, ExpeditionStore, FusionInheritancePicks, FusionStore, GateCounterSeed, GetMaxEventId, InstanceProducerStore) ✅ 2026-09-12
  - Migrated all 8. **This batch found a real mis-classification** (the pilot's whole purpose): class A was defined by *what a test asserts*, but a memory-bound test can reach a file **through the store's API** — the archive entry points (`TrimSoulLedgerTails` etc.) throw `StorePlanException` on memory (T4). `ExpeditionRewardApplyTests.Soul_trim_...` (1 of its 6 tests) now uses its own `CreateFileBacked()`; the class stays memory.
  - Two helper capabilities were added and proven: `Reopen()` (T11) and **`CreateWithPreInitHot(...)`** — seeds a legacy schema in memory before `Init()`, so the pre-module migration test runs **without a file** instead of being declared file-bound. `EligibilityAxisMigrationTests` uses it, keeping the legacy DDL and all 8 assertions.
  - `GetMaxEventIdTests`' `static NewStore()` (which had **no cleanup at all**) is now helper-backed with disposal.
  - Verified: gate PASS — focused 63/63, zero assertions dropped (30/33/32/30/102/33/6/24 unchanged), seeds preserved, baseline 187→179 (−8), no batch-D temp dirs, both guards green, full Data 1288/1288.
  - **Latent, must be handled in their batches:** `SoulLedgerTrimTests` and `WebGameIsolationTests` call archive entry points and will throw unless they use `CreateFileBacked()` (flagged in T15/T18). The spec's census is corrected accordingly (`CompactionWorkerTests` is a false positive — a `FakeHotCompactor`, not a store test).
  - Deps: T10. Scope: M.
  - **Excludes** the pilot's `CurveStoreTests`/`ElementStoreTests` (already migrated by T9).
- [x] **Task T15: Data.Tests root batch E** (8: GateCounterStore, LoadoutStore, LoamPersistence, OnboardingCheckpointStore, OnboardingProjection, PassiveTreeState, SoulLedgerTrim, SoulStore) — **read-only: `GateCounterStoreTests`** ✅ 2026-09-12
  - Migrated all 8. `LoadoutStoreTests`' reopen → `_testStore.Reopen()`; the two `Onboarding*` files' **field-initializer** `_dir` shape handled; `GateCounterStoreTests`' read-only open → plain open.
  - **`SoulLedgerTrimTests` is Tier-3 file-bound** (as the T14 finding predicted): its subject IS the filesystem archive, so it uses `CreateFileBacked()` and its 3 archive assertions now target `_testStore.DataDir!`, keeping the exact same globs (`souls-a1-*.sqlite` Single, `souls-*.sqlite` zero). The gate independently confirmed those assertions are non-vacuous (they would fail if trim wrote no slice).
  - Verified: gate PASS — focused 59/59, zero assertions dropped (17/25/9/39/8/21/10/44), all seed calls preserved, baseline 179→171 (−8), no batch-E temp dirs, both guards green, full Data 1288/1288.
  - Deps: T10. Scope: M.
  - **Excludes** `PassiveTreeImportRunnerTests`, `SeedImportRunnerTests`, `CreatureSpeciesImportCliTests` (separate subprocess/fixture cases — T16).
- [x] **Task T16: Data.Tests root batch F — subprocess/fixture store tests** (PassiveTreeImportRunner, SeedImportRunner, PatronStore, PlayerMaterialise, PowerCoefficientImport, PowerStore, RunPoolStore, ShardRungsMigration — 8) ✅ 2026-09-12
  - Migrated all 8. **Two of them split substrate by subject:** `PassiveTreeImportRunnerTests` and `SeedImportRunnerTests` keep a **real corpus fixture dir** (the generated seed/tree corpus is what the runner under test reads) while their **store** is memory. Their per-test fixture cleanups — **13 swallowed deletes** — became throwing deletes (R3). `PlayerMaterialiseTests`' raw `new SqliteConnection($"Data Source={Path.Combine(_dir, ...)}")` → `SqliteConnectionFactory.Open(_store.HotPath)`; `RunPoolStoreTests`' reopen → `Reopen()`.
  - Verified: gate PASS — focused 70/70, zero assertions/methods/seeds dropped, baseline 171→163 (−8), both guards green, **two consecutive full-suite runs 1288/1288**. A single earlier `Failed: 1` did not reproduce across two clean runs and named no test; no shared state exists (each store is its own named memory DB), so it is treated as transient and left for the T19b runtime alarm to catch if it recurs.
  - **Excludes `CreatureSpeciesImportCliTests.cs`** (owned by `cold-process-test-build-20260912-e5b1`) — untouched.
  - Deps: T10. Scope: M.
- [ ] **Task T17: Data.Tests root batch G** (8: SpeciesExpedition, SpeciesImportStore, SpeciesProgression, SpeciesRespec, SpecimenMaterialisedRoll, StructureInstanceStore, SummonStore, TreeStateVolume) — **read-only: `TreeStateVolumeTests`** — Deps: T10. Scope: M.
- [ ] **Task T18: Data.Tests root batch H** (8: UniqueActorStore, UniqueEquipmentAtomBinding, WardenContract, WatermarkSmoke, WebGameIsolation, WebMatchStore, WorldAiAcceptance, WorldAiCommit) — Deps: T10. Scope: M.
  - The remainder (World*/Xp/Zomboss store tests, `Sqlite/**`, `Items/**`, `Delve/**`, `Actions/**`, remaining `PassiveTree/**`) is sub-divided in T18a–T18c so no task is XL.

- [ ] **Task T18a: root remainder + Sqlite/** (WorldCommand*, WorldStore, WorldTurnCommit, WorldTwentyTurnCheckpoint, WorldWaveOneAcceptance, WorldSeedStore, WorldGraphDiff, WorldCommandTurnGuard + `Sqlite/PlayerCommanderStoreTests` ≈ 9) — Deps: T10. Scope: M.
- [ ] **Task T18b: Items/** (23 files → 3 sub-batches of ~8) — **read-only: `ItemGrantStoreTests`** — Deps: T10. Scope: M ×3.
- [ ] **Task T18c: Delve/** (13) + Actions/** (5) + remaining PassiveTree/** (2) → 3 sub-batches of ~7 — **read-only: `DomainImportTests`, `DomainProgressStoreTests`** — Deps: T10. Scope: M ×3.
- [ ] **Task T18d: Server.Tests (54 A-tier)** — each boots a `WebApplication`/store directly; migrate construction + teardown in 7 sub-batches of ~8 — Deps: T10. Scope: M ×7 (never one XL task).
- [ ] **Task T18e: E2E (4 A-tier) + Core.Tests (4 store sites)** — Deps: T10. Scope: S.
- [ ] **Task T18f: the 5 file-bound classes to `CreateFileBacked()`** (`LegacyMonoMigratorTests`, `RpgStoreDalSmokeTests`, `RpgStoreSmokeTests`, `ColdArchiveCompactionTests`, `StoragePurgeTests`) — fix the leak **without** changing their file/WAL assertions — Deps: T10. Scope: M.

- [ ] **Task T19: Migration checkpoint — the whole suite is leak-proof**
  - Description: every migrated file's baseline line is gone; the file-bound 5 use the leak-proof helper; no test in `tests/**` creates a temp store; runtime duration + temp-dir delta reported.
  - Acceptance: `guard-test-substrate.ps1` green with a **smaller** baseline than `7183a59e`; a full Data+Server+E2E+Core run leaves **0** `rpg-*.sqlite` files outside the file-bound tmp dirs.
  - Verify: `guard-test-substrate.ps1` + full suites + a temp-root count before/after.
  - Files: `scripts/test-substrate-baseline.txt`. Scope: M.
  - Dependencies: T11–T18f.

- [ ] **Task T19b: runtime leak alarm — module `disk-write-probe`'s second half**
  - Description: the shipped gate (`guard-test-substrate.ps1`) is **static only**. Add `scripts/test-substrate-leak-alarm.ps1`: snapshot the test temp root (count + the `fusionrpg-*` dirs) **before** a full test run, run it, snapshot **after**, and **fail** if any `fusionrpg-*` dir survives or any `rpg-*.sqlite` was created outside the file-bound tmp dirs. Assert the **delta** (`before == after` / `0 new`) — never a population count (`validation-ssot.md`). Wire into CI after the test step (not `deploy-play.ps1`, which does not run the suite).
  - Acceptance: on a clean post-migration tree the alarm passes; planting a store test that leaks a temp dir makes it **fail**; planted-`rpg-*.sqlite` makes it fail.
  - Verify: run the alarm around a focused `dotnet test`, then with a planted leaking test (must fail), then remove it (must pass); `guard-test-substrate.ps1` unchanged.
  - Files: `scripts/test-substrate-leak-alarm.ps1`, `.github/workflows/ci.yml` (shared with `cold-process-test-build-e5b1` — coordinate). Scope: M.
  - Dependencies: T19 (the suite must be leak-proof before an alarm can pass).

### Checkpoint 4 — Migration
- [ ] ⭐ Baseline strictly smaller; no new violation; owner reviews the delta before the archive tail.

## Phase 5 — Archive target (module `archive-target`, optional)

- [ ] **Task T20a: archive-target abstraction + file impl** — Deps: T5. Scope: M.
  - Description: the `IArchiveStore`-shaped abstraction (create/open/list/exists/delete) plus the file-backed implementation; no behavior change yet.
- [ ] **Task T20b: thread the abstraction through the 4 writers + purge; add the memory target** — Deps: T20a. Scope: M.
  - Description: replace the direct `Directory.CreateDirectory(ArchiveDir)`/`Path.Combine`/`Open(absPath)` at `Compaction.cs:140,276,484,623` and the resolver at `:711` + purge (`Storage.cs:258-288`) with the abstraction; add the memory target; the memory store's archive entry points stop throwing once its target is memory-capable.
  - Acceptance: archive slices created and read on a memory target; file plan byte-identical.
  - Verify: Data tests + `guard-test-substrate.ps1`.
  - Files: `RpgStore.Compaction.cs`, `RpgStore.Storage.cs`, the abstraction.

- [ ] **Task T21: Archive tests to memory** (`ColdArchiveCompactionTests`, `StoragePurgeTests`) — Deps: T20. Scope: M.
- [ ] **Task T22: Archive checkpoint** — slices prove on memory; the file-bound set drops from 5 to 3 (legacy + 2 smoke). Deps: T21. Scope: XS.

### Checkpoint 5 — Archive
- [ ] Archive tests on memory; the remaining file-bound set is legacy + the 2 WAL/file smoke tests.

## Phase 6 — The standard binds (module `substrate-standard`)

- [ ] **Task T23: `data-architecture.md` amendment** — doc content ✅ written 2026-09-12; final reconcile after build
  - Description: added §1's **Storage plan (2026-09-12)** subsection (file vs memory, the three doors, independent instances, the URI-throw, the read-only limitation, the four skipped `Init()` steps) and §6's **the storage plan does not move the DAL boundary** paragraph (both plans keep SQL in Data; `IDisposable` added).
  - Acceptance: the doc describes the shipped shape; no stale claim. **The docs now describe the *specified* shape; the box closes when the built code matches it** (`Substrate-standard` is the last module, so this reconciles against the shipped implementation at the final gate).
  - Verify: doc read-through + `decisions.md` consistency; re-read after T22 to confirm every sentence matches the shipped code.
  - Files: `docs/architecture/data-architecture.md`. Scope: XS.
  - Dependencies: T19b, T22.

- [ ] **Task T24: Final gate**
  - Description: full CI-equivalent run, all guards, `dotnet test` for every touched project; report duration and temp-dir delta; confirm the gate is green with the final (smallest) baseline.
  - Acceptance: all guards + suites green; 0 store temp files; baseline reflects only genuinely file-bound or not-yet-migrated files, each with a reason.
  - Verify: `deploy-play.ps1` guard chain (without a live deploy) + per-project `dotnet test`.
  - Files: none (verification). Scope: M.
  - Dependencies: T23.

### Checkpoint 6 — Complete
- [ ] ⭐ All acceptance criteria met; leak impossible to reintroduce silently; owner reviews for merge.

---

## Parallelization summary

- **Parallel-safe after T10:** T11–T18c (disjoint Data.Tests folders).
- **Sequential:** T1→T5 (one seam), T6→T8 (one helper), T20→T21 (one abstraction).
- **Coordinate:** T18d (Server, boots `WebApplication`), T18e (E2E shares `ci.yml`), T18a excludes another session's file.

## Gate check (per skill "Gates vs. checkpoints")

No pre-work gate blocks a phase. T1 is a doc edit inside this program; every other task's dependency is
in-program work. The only external coordination (another session's file, shared `ci.yml`) is handled by
**exclusion + owner coordination**, not by halting — and the archive tail (T20–T22) is a reversible
default (defer to its own program) if it outgrows its cut point.
