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

## Phase 1 — Foundation: an in-memory store (module `memory-storage-plan`)

- [ ] **Task T1: ADR row — the storage plan seam**
  - Description: add a `decisions.md` row locking "RpgStore has a storage plan: file default, memory is a first-class test substrate" (the module owes this before it locks behavior). Point the Data/architecture-map row at the spec.
  - Acceptance: `decisions.md` carries the row; no existing locked row contradicts it.
  - Verify: doc read-through + `grep` the new row.
  - Files: `docs/architecture/decisions.md`, `docs/architecture/data-architecture.md`. Scope: XS.
  - Dependencies: none — spec approved 2026-09-12.

- [ ] **Task T2: `SqliteConnectionFactory` memory-URI branch**
  - Description: `Open(path, readOnly)` detects a `file:…mode=memory&cache=shared` URI and opens it without `Path.GetFullPath`/`CreateDirectory`/forced `ReadWriteCreate`; add `MemoryUri(name)` helper; omit WAL on memory; **throw** on `readOnly:true` + memory.
  - Acceptance: a memory URI opens and round-trips SQL; a file path behaves exactly as before; read-only+memory throws a named error.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests --filter FullyQualifiedName~MemoryStoragePlan` + `guard-dal.ps1` (SQL stays in Data).
  - Files: `src/FusionRpg.Data/Sqlite/SqliteConnectionFactory.cs`, `tests/FusionRpg.Data.Tests/MemoryStoragePlanTests.cs`. Scope: S.
  - Dependencies: T1.

- [ ] **Task T3: `RpgStoreOptions` + three doors + keepers + `Dispose`**
  - Description: public `RpgStoreOptions` record (`DataDir`, `InMemory`, `HotName`, `MediaName`) + `For`/`Resolve` (Resolve throws on a URI under the file plan); one private plan ctor that opens keepers iff memory; replace `RpgStore(string)` with `(string, bool = false)`; public `RpgStore(RpgStoreOptions)`; static `RpgStore.InMemory()`; implement `IDisposable` (file plan no-op).
  - Acceptance: all three doors build an init-able store; production `new RpgStore(dir)` binds unchanged; `Dispose` releases keepers.
  - Verify: Data build + `MemoryStoragePlanTests`.
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.cs`, `tests/FusionRpg.Data.Tests/MemoryStoragePlanTests.cs`. Scope: M.
  - Dependencies: T2.

- [ ] **Task T4: `Init()` skip + archive throw + `Reset()` in memory**
  - Description: under the memory plan skip `CreateDirectory`/`ArchiveDir`/`LegacyMonoMigrator.TryMigrate`/`HealOrphanMediaTables`; make every archive entry point (4 writers, purge, `PromoteClosedRunCapture`) throw a dedicated named exception; `Reset()` clears in place and re-`Init()`s without touching the filesystem.
  - Acceptance: memory `Init()` creates no file; archive call throws (not a cwd write); `Reset()` leaves an empty usable store with no file.
  - Verify: `MemoryStoragePlanTests` (archive-throw + reset cases).
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.cs`, `RpgStore.Compaction.cs`, `RpgStore.Storage.cs`, `MemoryStoragePlanTests.cs`. Scope: M.
  - Dependencies: T3.

- [ ] **Task T5: Module-1 proof tests (the module's Definition of Done)**
  - Description: memory round-trips SQL with zero files; table set equals the file store's (`sqlite_master` relationship); all three doors; URI-trap throw; read-only throw; archive throw; **`Parallel.For` N-store independence**; keeper survives `ClearAllPools`; existing smoke tests unmodified.
  - Acceptance: every row in the spec's testing table is a passing test; `RpgStoreSmokeTests`/`RpgStoreDalSmokeTests` pass **without change**.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests` + `guard-dal.ps1` + `guard-test-substrate.ps1`.
  - Files: `tests/FusionRpg.Data.Tests/MemoryStoragePlanTests.cs`. Scope: M.
  - Dependencies: T4.

### Checkpoint 1 — Foundation
- [ ] Data suite green; production smoke tests untouched; `guard-dal` + `guard-test-substrate` green; owner reviews the seam before migration starts.

## Phase 2 — The test helper (module `test-store-helper`)

- [ ] **Task T6: `DataTestStore` core**
  - Description: `DataTestStore.Create()` (memory) and `CreateFileBacked()` (file semantics), wrapping `RpgStore`, holding keepers for the store's lifetime, implementing `IDisposable`/`IAsyncDisposable`.
  - Acceptance: both factories return an init-able store; `Create()` creates no temp dir; `CreateFileBacked()` uses a unique temp dir.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests --filter FullyQualifiedName~DataTestStore`.
  - Files: `tests/FusionRpg.Data.Tests/DataTestStore.cs`, `tests/FusionRpg.Data.Tests/DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T5.

- [ ] **Task T7: Leak-proof file cleanup (the R3 rule, mechanized)**
  - Description: `CreateFileBacked()`'s dispose does `SqliteConnection.ClearAllPools()` then `Directory.Delete`, and a **failure throws** (asserted, never swallowed). No `catch { }`.
  - Acceptance: a held-open file store still deletes after `ClearAllPools`; a simulated failure surfaces as an exception.
  - Verify: `DataTestStoreTests` (cleanup-failure case).
  - Files: `tests/FusionRpg.Data.Tests/DataTestStore.cs`, `DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T6.

- [ ] **Task T8: Helper proof tests**
  - Description: memory store no-file; file store deletes cleanly; failure-not-swallowed; helper introduces **no** `guard-test-substrate` baseline entry.
  - Acceptance: all helper assertions pass; gate has no line for the helper.
  - Verify: `guard-test-substrate.ps1` + `DataTestStoreTests`.
  - Files: `tests/FusionRpg.Data.Tests/DataTestStoreTests.cs`. Scope: S.
  - Dependencies: T7.

### Checkpoint 2 — Helper
- [ ] Helper green; gate still green; owner reviews the helper API before the migration.

## Phase 3 — Pilot: prove the pattern at minimum blast radius

- [ ] **Task T9: Migrate 5 representative store tests to memory**
  - Description: migrate `ActionStoreTests`, `AffixStoreTests`, `AtomStoreTests`, `CurveStoreTests`, `ElementStoreTests` to `DataTestStore.Create()`; remove each baseline line; the 3 read-only sites in these files switch to a plain open.
  - Acceptance: all 5 pass in memory; no file created; baseline loses their lines; gate green.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionStoreTests|...~AffixStoreTests|...~AtomStoreTests|...~CurveStoreTests|...~ElementStoreTests"` + `guard-test-substrate.ps1`.
  - Files: the 5 test files + `scripts/test-substrate-baseline.txt`. Scope: M.
  - Dependencies: T8.

- [ ] **Task T10: Pilot checkpoint — pattern proven or reverted**
  - Description: confirm no assertion had to change (if one did, the file was mis-classified and belongs in the file-bound set), confirm duration, and confirm the baseline shrank by exactly 5 lines.
  - Acceptance: zero assertion changes; baseline −5; owner signs the pattern off.
  - Verify: `git diff` shows construction/teardown only; `guard-test-substrate.ps1`.
  - Files: none (verification). Scope: XS.
  - Dependencies: T9.

### Checkpoint 3 — Pilot
- [ ] ⭐ Zero assertion drift across 5 files; owner approves before the bulk migration.

## Phase 4 — The migration (module `store-test-migration`)

> Every task: replace the temp-dir ctor with `DataTestStore.Create()`, replace `Dispose`'s swallowed delete, delete each fixed file's `scripts/test-substrate-baseline.txt` line. **Exclude `CreatureSpeciesImportCliTests.cs`** (owned by `cold-process-test-build-e5b1`).

- [ ] **Task T11: Data.Tests root batch A** (8: ActionCatalogBuilder, ActionCatalogStore, AffixImportPath, AllocationStore, AlmanacSeedEnrichment, AlmanacSeed, AptitudePreset, AtomInstance) — Deps: T10. Scope: M.
- [ ] **Task T12: Data.Tests root batch B** (8: AtomRowWiring, BindResolution, ChannelPolicyStore, ContainerStore, ContentBoot, ContentHashStore, ContractGate, ContractOps) — Deps: T10. Scope: M.
- [ ] **Task T13: Data.Tests root batch C** (8: ContractRegression, ContractSettle, ContractStore, CreatureLawnDeployCommanderRefusal, CreatureLawnDeployHypnoRefusal, CreatureLawnDeployMagnitude, CreatureLawnDeploy, CreatureStore) — Deps: T10. Scope: M.
- [ ] **Task T14: Data.Tests root batch D** (8: EligibilityAxisMigration, ExpeditionRewardApply, ExpeditionStore, FusionInheritancePicks, FusionStore, GateCounterSeed, GetMaxEventId, InstanceProducerStore) — Deps: T10. Scope: M.
  - **Excludes** the pilot's `CurveStoreTests`/`ElementStoreTests` (already migrated by T9).
- [ ] **Task T15: Data.Tests root batch E** (8: GateCounterStore, LoadoutStore, LoamPersistence, OnboardingCheckpointStore, OnboardingProjection, PassiveTreeState, SoulLedgerTrim, SoulStore) — Deps: T10. Scope: M.
  - **Excludes** `PassiveTreeImportRunnerTests`, `SeedImportRunnerTests`, `CreatureSpeciesImportCliTests` (separate subprocess/fixture cases — T16).
- [ ] **Task T16: Data.Tests root batch F — subprocess/fixture store tests** (PassiveTreeImportRunner, SeedImportRunner, PatronStore, PlayerMaterialise, PowerCoefficientImport, PowerStore, RunPoolStore, ShardRungsMigration — 8) — Deps: T10. Scope: M.
  - **Excludes `CreatureSpeciesImportCliTests.cs`** — owned by `cold-process-test-build-20260912-e5b1`.
- [ ] **Task T17: Data.Tests root batch G** (8: SpeciesExpedition, SpeciesImportStore, SpeciesProgression, SpeciesRespec, SpecimenMaterialisedRoll, StructureInstanceStore, SummonStore, TreeStateVolume) — Deps: T10. Scope: M.
- [ ] **Task T18: Data.Tests root batch H** (8: UniqueActorStore, UniqueEquipmentAtomBinding, WardenContract, WatermarkSmoke, WebGameIsolation, WebMatchStore, WorldAiAcceptance, WorldAiCommit) — Deps: T10. Scope: M.
  - The remainder (World*/Xp/Zomboss store tests, `Sqlite/**`, `Items/**`, `Delve/**`, `Actions/**`, remaining `PassiveTree/**`) is sub-divided in T18a–T18c so no task is XL.

- [ ] **Task T18a: root remainder + Sqlite/** (WorldCommand*, WorldStore, WorldTurnCommit, WorldTwentyTurnCheckpoint, WorldWaveOneAcceptance, WorldSeedStore, WorldGraphDiff, WorldCommandTurnGuard + `Sqlite/PlayerCommanderStoreTests` ≈ 9) — Deps: T10. Scope: M.
- [ ] **Task T18b: Items/** (23 files → 3 sub-batches of ~8) — Deps: T10. Scope: M ×3.
- [ ] **Task T18c: Delve/** (13) + Actions/** (5) + remaining PassiveTree/** (2) → 3 sub-batches of ~7 — Deps: T10. Scope: M ×3.
- [ ] **Task T18d: Server.Tests (54 A-tier)** — each boots a `WebApplication`/store directly; migrate construction + teardown in 7 sub-batches of ~8 — Deps: T10. Scope: M ×7 (never one XL task).
- [ ] **Task T18e: E2E (4 A-tier) + Core.Tests (4 store sites)** — Deps: T10. Scope: S.
- [ ] **Task T18f: the 5 file-bound classes to `CreateFileBacked()`** (`LegacyMonoMigratorTests`, `RpgStoreDalSmokeTests`, `RpgStoreSmokeTests`, `ColdArchiveCompactionTests`, `StoragePurgeTests`) — fix the leak **without** changing their file/WAL assertions — Deps: T10. Scope: M.

- [ ] **Task T19: Migration checkpoint — the whole suite is leak-proof**
  - Description: every migrated file's baseline line is gone; the file-bound 5 use the leak-proof helper; no test in `tests/**` creates a temp store; runtime duration + temp-dir delta reported.
  - Acceptance: `guard-test-substrate.ps1` green with a **smaller** baseline than `7183a59e`; a full Data+Server+E2E+Core run leaves **0** `rpg-*.sqlite` files outside the file-bound tmp dirs.
  - Verify: `guard-test-substrate.ps1` + full suites + a temp-root count before/after.
  - Files: `scripts/test-substrate-baseline.txt`. Scope: M.
  - Dependencies: T11–T18f.

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

- [ ] **Task T23: `data-architecture.md` amendment**
  - Description: state the store's storage plan, that the DAL boundary is unchanged, and that a shared-cache memory DB cannot be opened read-only; cross-link `testing-standard.md`.
  - Acceptance: the doc describes the shipped shape; no stale claim.
  - Verify: doc read-through + `decisions.md` consistency.
  - Files: `docs/architecture/data-architecture.md`. Scope: XS.
  - Dependencies: T19, T22.

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
