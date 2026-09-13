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
- [x] **Task T0f: spec-test-profiles.md** (added 2026-09-12 by owner decision) — the profile contract: two categories (`DiskSemantics`, `Heavy`), four profiles (default/full/gate/nightly), the verified filter semantics, and the rule that the guards run only on `full`.

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
- [x] **Task T17: Data.Tests root batch G** (8: SpeciesExpedition, SpeciesImportStore, SpeciesProgression, SpeciesRespec, SpecimenMaterialisedRoll, StructureInstanceStore, SummonStore, TreeStateVolume) — **read-only: `TreeStateVolumeTests`** ✅ 2026-09-12
  - Migrated all 8; `TreeStateVolumeTests`' read-only open → plain open. Constructor tuning/seed calls preserved (`SpeciesBuildTuningHub.Configure`, `AwardSouls`).
  - Verified: gate PASS — focused 54/54, zero assertions/methods/seeds dropped, no `new RpgStore(`/`GetTempPath`/`catch {`/`readOnly:` remains in any of the 8, baseline 163→155 (−8), both guards green, full Data 1288/1288, no new old-prefix temp dirs.
  - Deps: T10. Scope: M.
- [x] **Task T18: Data.Tests root batch H** (8: UniqueActorStore, UniqueEquipmentAtomBinding, WardenContract, WatermarkSmoke, WebGameIsolation, WebMatchStore, WorldAiAcceptance, WorldAiCommit) ✅ 2026-09-12
  - Migrated all 8; ctor seeds preserved (`ImportRealSeedTree()`, `AwardSouls`, `CreateWorld`). Two specials: `WebGameIsolationTests.Closed_web_runs_are_exempt_from_capture_archiving` uses `CreateFileBacked()` (it calls the archive entry point `CompactAfterRunClosed`, whose subject is "no capture files appear") while the class stays memory; `WebMatchStoreTests.Old_databases_gain_the_stamp_column...` uses `CreateWithPreInitHot(...)` in memory (legacy DDL seeded before `Init`), keeping every assertion and dropping its temp dir + raw `SqliteConnection`.
  - **The gate caught a real defect in my refactor:** the new `PlayMatch(store, …)` overload duplicated the helper and **omitted the `board.start` event** the original emitted. Fixed by making the original delegate to the store-taking overload (one helper, `board.start` intact, one `InsertEvents`); assert/method counts unchanged (13/5), verified after the fix.
  - Verified: gate PASS — focused 90/90, zero assertions/methods dropped, baseline 155→147 (−8), no batch-H temp dirs, both guards green, full Data 1288/1288. **Archive census now clean** — no memory-bound store file remains that would throw; remaining callers are the file-bound classes (`ColdArchiveCompactionTests`, `StoragePurgeTests`), the already-converted (`SoulLedgerTrimTests`, `ExpeditionRewardApplyTests`, `WebGameIsolationTests`), the intentional contract test (`RpgStoreStoragePlanTests`), and the `CompactionWorkerTests` false positive (a `FakeHotCompactor`, not a store test).
  - Deps: T10. Scope: M.
  - The remainder (World*/Xp/Zomboss store tests, `Sqlite/**`, `Items/**`, `Delve/**`, `Actions/**`, remaining `PassiveTree/**`) is sub-divided in T18a–T18c so no task is XL.

- [x] **Task T18a: root remainder + Sqlite/** (15: AtomImport, TreeRespecStore, WorldCommandRoundTripProperty, WorldCommandStore, WorldCommandTurnGuard, WorldGraphDiff, WorldSeedStore, WorldStore, WorldTurnCommit, WorldTwentyTurnCheckpoint, WorldWaveOneAcceptance, XpLedgerRunScope, ZombossAdaptiveStore, ZombossDeployStore, `Sqlite/PlayerCommanderStore`) ✅ 2026-09-12
  - Migrated all 15. `AtomImportTests` splits substrate (store memory; its `A_seed_tree_on_disk_imports_end_to_end` fixture dir stays real, delete no longer swallows). `Sqlite/PlayerCommanderStoreTests`' 3 static helpers + inline open now use `SqliteConnectionFactory.Open(...)`, so they read the memory `HotPath` instead of a raw `Data Source=path`.
  - Verified: gate PASS on **all substantive criteria** — focused 135/135, zero assertions/methods dropped (65/23, 61/15, 25/9, … unchanged), baseline 147→132 (−15), both guards green, no batch temp dirs, and `AtomImportTests`' fixture dir is created **and deleted** (no leak).
  - **Flake identified and explained (not this task):** a full-suite run intermittently fails `CreatureSpeciesImportCliTests.A_stale_committed_file_refuses_the_whole_import_and_writes_nothing` — a file **byte-identical to HEAD**, owned by the now-**merged** session `cold-process-test-build-20260912-e5b1`. It passes 2/2 in isolation (7s) and fails only under full-suite load: its `WaitForExit(60_000)` cap (`CreatureSpeciesImportCliTests.cs:65`) is exceeded while this machine runs 4 concurrent agent streams (CPU ~79%). This is the intermittency seen at T16 and T18. Environmental, not a migration defect.
  - Deps: T10. Scope: M.
- [x] **Task T18b: Items/** (23 files) ✅ 2026-09-12
  - Migrated all 23 Items store tests to `DataTestStore.Create()` / helper dispose. Specials: the three reopens (`AssignmentStoreTests`, `InstanceOpTests`, `RelicRowMigrationTests`) use `_testStore.Reopen()`; raw `new SqliteConnection($"Data Source={Path.Combine(_dir,…)}")` in `CharmCarryStoreTests` (×2), `DropTableStoreTests`, `ItemSetStoreTests`, `RunDraughtStoreTests` and `RelicRowMigrationTests` became `SqliteConnectionFactory.Open(_store.HotPath)`; `ItemGrantStoreTests`' read-only open became a plain open; `ItemCardStoreTests` had a **vestigial** temp dir (created, never used) removed with all 4 assertions kept.
  - **This task found and fixed a real defect in the shipped static gate.** `Strip-Comments` stripped block comments *before* line comments with a regex, so a `/**` inside prose — e.g. the path literal `data/seed/items/charms/**` in a doc comment — opened a **phantom block comment** whose match ran to the next real `*/` ~160 lines below, deleting the ctor and `Dispose` from the scanned text. **15 test files contain such prose**, so the gate silently passed them; 6 real violations had been invisible since the gate shipped (and 2 of the 23 Items files, `CharmCarryStoreTests`/`ItemSetStoreTests`, were never baselined at all). Fixed with a **single left-to-right scanner** that consumes `//`, `/* */` and string literals in one pass. Gate's own tests 4/4; independently re-proven by planting a `data/x/**`+`catch{}` probe the old scanner was blind to.
  - Verified: gate PASS — 23 files, zero assertions/methods/seeds dropped, focused Items **238/238**, full Data **1288/1288**, `guard-dal` + `guard-test-substrate` green, no probe/temp residue. Baseline **132→117** (132 − 21 migrated Items lines + 6 newly-detected real violations; `ItemWorkbench`'s line also widened from `temp-store` to `swallowed-delete, temp-store`).
  - Files: `tests/FusionRpg.Data.Tests/Items/**` (23), `scripts/guard-test-substrate.ps1`, `scripts/test-substrate-baseline.txt`. Scope: M ×3.
  - Deps: T10.
- [x] **Task T18c: Delve/ + Actions/ + PassiveTree/** (21 files) ✅ 2026-09-12
  - Migrated the 20 fenced files, plus **`Delve/Domains/DomainRealPipelineTests.cs`** — a genuine un-migrated store test the **broken scanner had made invisible** (never baselined), found because the fixed gate now reports it.
  - Specials: `DomainImportTests`/`DomainProgressStoreTests` read-only opens → plain; `TreeCatalogMigrationTests`' two raw `Data Source=` helpers → `SqliteConnectionFactory.Open(_store.HotPath)`; `WebMatchDecisionsTests`' legacy-schema test → `CreateWithPreInitHot` (memory); `ActionUnlockGrantWiringTests`' ctor no longer writes disk.
  - Verified: build clean; focused Delve/Actions/PassiveTree **255/255**; per-file `Assert.`/method counts identical HEAD→work across all 21; zero leftovers.
  - Files: 21 under Delve/Actions/PassiveTree + `scripts/test-substrate-baseline.txt`. Scope: M ×3.
  - Deps: T10.

- [x] **Seam fix (uncovered by the T18d worker, 2026-09-12): `DataTestStore` was unreachable from Server/E2E tests** ✅
  - **A real gap in this program's own spec:** `spec-test-store-helper.md` assumed the helper was "reachable by the other test projects that construct the store", but no task provisioned that seam. `DataTestStore` lives only in `tests/FusionRpg.Data.Tests`, and the Server/E2E csprojs have no reference to it — so every Server test would fail `CS0103: The name 'DataTestStore' does not exist`.
  - Verified empirically by a probe (fails before, builds after), not assumed.
  - Fix: a `<Compile Include="..\FusionRpg.Data.Tests\DataTestStore.cs" Link="TestSupport\DataTestStore.cs" />` source link in the Server csproj — the repo's **existing** cross-test-project seam (`FusionRpg.Core.Tests.csproj` already links Injector sources this way). No new project, no new dependency. E2E needs the same link at T18e.
  - Files: `tests/FusionRpg.Server.Tests/FusionRpg.Server.Tests.csproj`. Scope: XS.
- [x] **Task T18d: Server.Tests (54 store files)** ✅ 2026-09-12
  - Migrated all 54 Server store tests to `DataTestStore.Create()` / helper dispose. 14 use `SqliteConnectionFactory.Open(_store.HotPath)` for their raw reads; `ContentBootStartupWiringTests` uses `Reopen()` and its static helper now takes an `RpgStore` instead of building one; `ZombossAdaptiveSeamTests`' per-iteration sweep store is now an independent `DataTestStore.Create()` **and is disposed each iteration** (it previously leaked one store per loop); `ReforgeWorldEndpointTests`' multi-host bundle runs entirely in memory.
  - **`BaseTypeSocketMaxCorpusTests` is deliberately NOT migrated** — it is class C (no `new RpgStore(`; it writes a real fixture tree for `BaseTypeSocketMaxCorpus.Load`), so it keeps its baseline line and needs a separate fixture-leak ruling.
  - Verified: gate PASS — 54 files with **zero assertion/method drift**, focused + full Server **404/404 twice**, `guard-dal` + `guard-test-substrate` green, baseline **96→42**, leak alarm 0 survivors.
  - **The gate found a real false positive in the T19b alarm**, fixed here: on a shared machine a *concurrent* process creates/removes its own `fusionrpg-*` dirs inside the alarm's window (measured: 108 "survivors" that all belonged to a concurrent Data.Tests run). Added `-IsolateTemp`, which gives the wrapped run its own private temp root; verified it still **detects** a planted leak (exit 1, naming the dir) and passes clean (exit 0).
  - Files: 54 under `tests/FusionRpg.Server.Tests/`, `scripts/test-substrate-baseline.txt`, `scripts/test-substrate-leak-alarm.ps1`. Scope: M ×7.
  - Deps: T10, seam fix.
- [x] **Task T18e: E2E (3) + Core.Tests (11)** ✅ 2026-09-12
  - Migrated the 3 E2E files and the 11 Core.Tests lines. `ChannelPolicyE2ETests`/`ContentBootE2ETests` use a memory throwaway store (their throwaway-ness is the point); **`RpgApiFactory` stays file-bound** — `WebApplicationFactory<Program>` boots the real server, which reads `FUSIONRPG_DATA` (`Program.cs:327`) — but its dispose is now leak-proof (`ClearAllPools` before delete, no swallow). `CorpusDumpTests` keeps a real `_dump` output tree (its subject) with the store in memory; the 7 class-C Core files had their fixture deletes un-swallowed (R3).
  - **The gate caught two real defects in this task's own work, both fixed:**
    1. `ConcreteSpeciesSeedReaderTests` used `CreateFileBacked()` where **memory suffices** — the identical `ImportSpecies → BuildCreatureSpeciesSnapshot` pipeline runs on `DataTestStore.Create()` in `RealCorpusFixture`, so the file store was needless disk churn (R1). Moved to memory.
    2. §7 of the standard pinned literal per-project counts (Guard 10 / Launcher 8) which were wrong on arrival (both are 9) — **exactly the population-count anti-pattern this repo bans**. §7 now describes groups by membership, no counts.
  - Also added the helper source link to `FusionRpg.Core.Tests.csproj` and `FusionRpg.E2E.Tests.csproj` (the same seam pattern).
  - Verified: gate PASS round 2 — Core **13,364/13,364**, Data **1,288/1,288**, E2E 212/219 (7 proven pre-existing: 5 checked-in fixture JSONs absent from the tree + 2 unrelated DTO/catalog failures, all store-free, reproduced on a pristine HEAD clone), 14 files zero drift, baseline **42→29**, both guards green.
  - Files: 11 Core + 3 E2E + 2 csprojs + baseline + standard. Scope: S→M.
  - Deps: T10, seam fix.
- [x] **Task T18f: the 5 file-bound classes to the leak-proof file helper** ✅ 2026-09-12
  - `ColdArchiveCompactionTests`, `RpgStoreDalSmokeTests`, `RpgStoreSmokeTests`, `StoragePurgeTests` now build their store via **`DataTestStore.CreateFileBacked()`** (they keep real files — the disk IS their subject — but their disposal is the helper's, which clears pools and throws on a failed delete). `LegacyMonoMigratorTests` builds a hand-made legacy `rpg.sqlite` per test, so it keeps its own dirs with `ClearAllPools()` + a **throwing** delete in each `finally`.
  - **The gate's runtime alarm caught a real live leak in this task:** `RpgStoreSmokeTests.Init_recreates_media_when_hot_exists_and_media_missing` had a **second**, per-test swallowed delete that leaked `fusionrpg-nomedia-*` on every run — the exact 65.5 GB mechanism. Fixed with `ClearAllPools()` + a throwing delete. Both alarm runs now report **0 survivors**.
  - **The gate also caught two false claims in §7 of the standard** (it said all six use `CreateFileBacked()` — false for `LegacyMonoMigratorTests` and the other session's `CreatureSpeciesImportCliTests` — and understated `RpgStoreSmokeTests`). §7 corrected to name exactly which classes use the helper, which keep a `temp-store` line and why, with no pinned count.
  - Verified: gate PASS round 2 — focused 35/35, Data **1,288/1,288**, baseline **29→26**, both guards green, leak alarm 0 survivors twice, 5 files zero drift and no memory move (every disk assertion intact).
  - Files: 5 test files + `scripts/test-substrate-baseline.txt` + `docs/contributing/testing-standard.md`. Scope: M.
  - Deps: T10.

### Checkpoint 4 — Migration ✅
- [x] ⭐ Baseline **216 → 26**; every store test that *can* be memory writes nothing; the 6 genuinely file-bound classes use the leak-proof file helper; the runtime alarm reports 0 survivors. No new violation possible: the ratchet only shrinks and the gate has been live since before the build.

- [x] **Task T19: Migration checkpoint — the whole suite is leak-proof** ✅ 2026-09-12
  - Description/acceptance met: every migrated file's baseline line is gone (216 → **26**); the file-bound classes use the leak-proof file helper; `guard-test-substrate.ps1` is green with a strictly smaller baseline than `7183a59e`; and the **runtime alarm around Data + Server + Core reports 0 survivors** (the strongest form of "no `rpg-*.sqlite` left outside the file-bound dirs").
  - Verified: alarm `-IsolateTemp` around Data (default profile) + Server + Core → exit 0, no survivors; baseline arithmetic 216 → 26; both guards green.
  - Files: `scripts/test-substrate-baseline.txt`. Scope: M.
  - Dependencies: T11–T18f.

- [x] **Task T19b: runtime leak alarm — module `disk-write-probe`'s second half** ✅ 2026-09-12 (commit `9408d67a`)
  - Description: the shipped gate (`guard-test-substrate.ps1`) is **static only**. Added `scripts/test-substrate-leak-alarm.ps1`: snapshot the OS temp root's `fusionrpg-*` dirs and the test-output `rpg-*.sqlite` files, run a supplied `-Run` block, snapshot after, and **fail** if any temp dir survived or any sqlite was left. It compares **membership (set diff), never a count** (`validation-ssot.md`), and names each survivor. Wired into `ci.yml` after the test step, with an `ANCHOR(T28)` comment.
  - Acceptance met: pass case exits 0; a planted leaking test exits 1 naming the exact dir; a planted sqlite leaker exits 1 naming the path. The static gate and its baseline are byte-identical.
  - Verified: gatekeeper re-ran all three cases independently (including the fail case naming `fusionrpg-gateleak-<guid>`), cleaned up the probes, and confirmed `guard-test-substrate.ps1` still exits 0.
  - Files: `scripts/test-substrate-leak-alarm.ps1`, `.github/workflows/ci.yml`. Scope: M.
  - Dependencies: T19.
  - Files: `scripts/test-substrate-leak-alarm.ps1`, `.github/workflows/ci.yml` (shared with `cold-process-test-build-e5b1` — coordinate). Scope: M.
  - Dependencies: T19 (the suite must be leak-proof before an alarm can pass).

### Checkpoint 4 — Migration
- [x] ⭐ Baseline strictly smaller (216 → 26); no new violation; the runtime alarm reports 0 survivors.

## Phase 5 — Archive target (module `archive-target`) — ⛔ CUT BY OWNER 2026-09-13

**The module is deliberately not built.** When the owner chose to keep it (2026-09-12) its premise was
true; by 2026-09-13 every part of that premise had changed, so the owner cut it. The reasoning is
recorded here because "cut" must be a decision with evidence, not a silent omission.

| The decision's premise (2026-09-12) | The measured state (2026-09-13) |
|---|---|
| The two archive classes wrote disk on **every dev run** | **`DiskSemantics`-tagged** (T26) → excluded from the default profile; they run only at `full`/nightly/gate |
| Their cleanup **swallowed** failures | **`DataTestStore.CreateFileBacked()`** (T18f) → leak-proof: clears pools, a failed delete throws |
| They ran in the local dev loop | The default profile (`test-fast.ps1`) excludes them; the alarm around it reports **0 survivors** |

So the refactor's entire remaining benefit is *"two already-excluded, already-leak-proof classes run in
RAM instead of ephemeral CI disk"* — no SSD saving, no dev-loop saving. Against that: **793 lines of
production archive data-movement code** would change — 4 writers, `ResolveArchiveAbsPath` (`:708`), the
purge path, a path-escape security guard, **15 `File.Delete` sites**, and **15 file-system assertions**
across the two tests. That is real risk to a data-movement path for no remaining benefit. The plan
itself already classed Phase 5 as **separable/cuttable**; this is that cut, taken on evidence.

**Consequence, stated plainly:** the file-bound set stays at **six** classes (the five store ones plus
`CreatureSpeciesImportCliTests`), not three. `ColdArchiveCompactionTests` and `StoragePurgeTests` keep
real files and the `DiskSemantics` trait, permanently, and remain leak-proof through the file helper.
A future program can still build `IArchiveTarget` — [spec-archive-target.md](../docs/architecture/data-test-substrate/spec-archive-target.md)
is written and remains the contract; it simply is not needed for this program's goal.

- [x] **Task T20a: archive-target abstraction + file impl** — ⛔ cut (not built)
- [x] **Task T20b: thread the abstraction through the 4 writers + purge; add the memory target** — ⛔ cut
- [x] **Task T21: Archive tests to memory** — ⛔ cut; the two classes stay `DiskSemantics` + file-bound
- [x] **Task T22: Archive checkpoint** — ⛔ cut; the file-bound set is documented above as six, not three

### Checkpoint 5 — Archive
- [x] ⛔ Cut by owner 2026-09-13: archive stays file-bound and `DiskSemantics`; the leak-proof file helper already removes the only defect it had (leaked, swallowed cleanup).

## Phase 6 — The standard binds (module `substrate-standard`)

- [x] **Task T23: `data-architecture.md` amendment** ✅ 2026-09-12 — reconciled against the shipped code
  - Description: §1 carries the **Storage plan (2026-09-12)** subsection (file vs memory, the three doors, independent instances, the URI-throw, the read-only limitation, the four skipped `Init()` steps) and §6 the **the storage plan does not move the DAL boundary** paragraph.
  - Acceptance met: every claim now matches the built code, verified line-by-line — three doors at `RpgStore.cs:67` (`(string dataDir, bool inMemory = false)`), `:73` (`RpgStoreOptions`), `:105` (`InMemory()`); `ArchiveUnavailable`/`RequireFileArchive` at `:37,44,52,55`; `IDisposable`. No stale claim.
  - Verified: doc read-through + `decisions.md` consistency (the storage-plan row at `decisions.md:84` agrees).
  - Files: `docs/architecture/data-architecture.md`. Scope: XS.

- [x] **Task T24: Final gate** ✅ 2026-09-12
  - Description/acceptance met: every CI test project + the full guard chain green; the baseline is at its smallest and reflects only genuinely file-bound or not-yet-owned files, each with a reason in `testing-standard.md` §7.
  - **CI-equivalent result (11 projects):** Core **13,364/13,364** · Data **1,288/1,288** · Server **404/404** · Guard **248/248** · E2E 212/219 *(exactly the 7 pre-existing fixture/DTO failures independently reproduced on a pristine HEAD clone — 5 checked-in fixture JSONs absent from the tree + 2 unrelated catalog cases; all store-free)* · Launcher 162/162 · SquadHarness 194/194 · ItemSeedValidator 81/81 · CheatCore 40/40 · AtomImporter 33/33 · TreeBinder 41/41 · ElementEnumGen 14/14.
  - **Guards (all 6):** `guard-single-writer`, `guard-secondary-no-unity`, `guard-funnel-delta`, `guard-actor-hub`, `guard-dal`, `guard-test-substrate` — all exit 0.
  - **Leak position:** baseline **216 → 26**; the runtime alarm reports **0 survivors** across Data+Server+Core, and around the default profile it reports **0** (was 436 before the migration).
  - Note: two intermittent full-suite failures were seen under load (`DelveBattleSessionManagerTests.Resume_...`, a SquadHarness case); both pass in isolation and across repeated runs, both are in files this program never modified, and neither reproduced. Recorded, not hidden.
  - Files: none (verification). Scope: M.

### Checkpoint 6 — Complete
- [ ] ⭐ All acceptance criteria met; leak impossible to reintroduce silently; owner reviews for merge.

## Phase 7 — Test profiles (module `test-profiles`, T25–T29)

> **Owner decision 2026-09-12:** the default dev/agent run must write nothing to disk and run no long
> test; the file-bound + heavy set runs in CI, nightly, and at the release gate. Tagging and migration
> are complementary — T18b–T18f still migrate what *can* be memory, so it writes nothing in *every*
> profile. Spec: [spec-test-profiles.md](../docs/architecture/data-test-substrate/spec-test-profiles.md).

- [x] **Task T25: fix the suite's slowest test** ✅ 2026-09-12 (commit `d2f42a06`)
  - `RpgStoreStoragePlanTests.Memory_stores_are_independent_under_parallel_creation` was the repo's single slowest test at **124.6s** (24 parallel stores × full 46-table `Init()`). Split into: 24 concurrent *constructions* asserting 48 distinct DB names (no `Init` needed), + 4 initialized stores each asserting `Assert.Single` (stronger than the old distinct-count). **Measured 0.52s (240x)**.
  - Deps: T5. Scope: XS.

- [x] **Task T26: tag `DiskSemantics`** (51 methods) ✅ 2026-09-12 (commit `630df206`) — Deps: T6. Scope: M.
  - Description: add `[Trait("Category","DiskSemantics")]` to the file-bound set — the 5 file-bound classes (`RpgStoreDalSmokeTests`, `RpgStoreSmokeTests`, `LegacyMonoMigratorTests`, `ColdArchiveCompactionTests`, `StoragePurgeTests`), `CreatureSpeciesImportCliTests`, and the real-corpus fixture classes (`SeedImportRunnerTests`, `PassiveTreeImportRunnerTests`, and `AtomImportTests`' disk case at method level). Class-level when every method qualifies, method-level when only some do.
  - Acceptance: each tagged file's `Assert.` count is unchanged; `--filter "Category=DiskSemantics"` returns exactly the tagged set; no untagged test lost.
  - Verify: `dotnet test … --filter "Category=DiskSemantics"` + per-file assert-count diff vs HEAD.
  - Files: those test files. Scope: M.
- [x] **Task T27: tag `Heavy`** (12 methods) ✅ 2026-09-12 (commit `630df206`) — Deps: T26. Scope: M.
  - Description: tag the ≥20s / long-run tests that are **not** disk-semantic, from the measured list: `Actions/ActionUnlockGrantWiringTests` (110s), `TreeStateVolumeTests.Two_thousand_actors…` (56s), the long `ContentHashStoreTests` cases, `Items/ItemSetStoreTests` corpus round-trips, `Items/ActionStockSpendStoreTests`, `Delve/DelveScopeTests` long cases. Method-level where only some methods qualify.
  - Acceptance: no test carries both categories (DiskSemantics wins); the tagged set matches the measured ≥20s list minus the disk-semantic ones.
  - Verify: `--filter "Category=Heavy"`; a re-measure shows no untagged test ≥20s remains.
  - Files: those test files. Scope: M.
- [x] **Task T28: `test-fast.ps1` + the profile wiring** ✅ 2026-09-12 — **filter correct; "writes no disk" acceptance blocked on T18b–T18f**
  - Description: `scripts/test-fast.ps1` owns the default filter (`Category!=DiskSemantics&Category!=Heavy`, repeatable `-Project`); `deploy-play.ps1` gained a test-fast step **after** the guard chain (all 13 guards byte-identical); `testing-standard.md` gained the profile table + the "guards run on `full` only" rule; new `.github/workflows/nightly.yml` (cron 03:17 + dispatch) runs the **full** suite wrapped in the leak alarm. CI is `full` by construction (its existing steps are unfiltered) — T28 appended a step that **asserts** no test filter was added to `ci.yml` (only the BalanceGuard one at `:140`), rather than duplicating the suite.
  - Verified: test-fast prints its filter and runs **1225**; static gate exit 0; all 13 deploy-play guards intact; both workflows parse.
  - **The worker found a real sequencing defect in this program's own spec, and the gatekeeper reproduced it:** the default profile **still writes disk** because **102 baseline files are neither `DiskSemantics` nor `Heavy`** — they are simply *not yet migrated* (T18b–T18f: Items 20, Server 49, Delve 12, Actions 5, DataRoot 6, Core 4, PassiveTree 3, E2E 3). Proof: an isolated run of `ArmouryTests` in a private temp root leaked **14** `fusionrpg-armoury-*` dirs, while the alarm around `test-fast.ps1` reported **436** survivors.
  - **Second nuance the worker caught:** method-level tagging does not stop a class whose **constructor** writes — `ActionUnlockGrantWiringTests`' ctor creates `fusionrpg-action-unlock-wiring-*` and its `Dispose` swallows, so its untagged methods still leak in the default profile. Class-level tagging (or migration) is required for ctor-writing classes.
  - **Corrected sequencing:** profiles can land (the filter is right and the machinery is proven), but `spec-test-profiles.md` success criterion 1 ("`test-fast.ps1` writes no disk") is only satisfiable **after T18b–T18f migrate the remaining 102 files**. Recorded here so no future session claims the default is disk-free early. Nightly will be red until then — which is the alarm doing its job.
  - Files: `scripts/test-fast.ps1`, `scripts/deploy-play.ps1`, `docs/contributing/testing-standard.md`, `.github/workflows/{ci,nightly}.yml`. Scope: M.
  - Deps: T26, T27.
- [x] **Task T29: profile checkpoint** ✅ 2026-09-12
  - Report: **default = 1225, full = 1288, tagged = 51 DiskSemantics + 12 Heavy** → `51 + 12 + 1225 = 1288` exactly. The relationship holds at run time (no pinned total anywhere).
  - **The key criterion is now satisfied:** with the migration complete (T18f), the leak alarm around `test-fast.ps1` (`-IsolateTemp`) reports **exit 0, 0 survivors** — it reported **436** survivors when T28 landed, before the migration retired the remaining 102 files. The default profile now genuinely writes no disk.
  - Guards still run only on `full` (`test-fast.ps1` runs the suite, not the guards); no assertion count changed for any tagged file (verified per-file at T26/T27 and re-verified for the migration's 5 file-bound classes at T18f).
  - Files: none (verification). Scope: XS.

### Checkpoint 7 — Profiles
- [ ] ⭐ Default run's **filter** is correct and the machinery is proven; the **disk-free** half of the checkpoint completes only after T18b–T18f (T29). `full` still covers everything; guards unchanged.

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
