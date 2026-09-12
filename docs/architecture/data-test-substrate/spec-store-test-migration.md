# Spec: `store-test-migration`

**Module id:** `store-test-migration` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 3 of 6
**Depends on:** [`memory-storage-plan`](spec-memory-storage-plan.md), [`test-store-helper`](spec-test-store-helper.md).
**Model calls:** none.

## Objective

Move every in-memory-safe store test off the disk and onto `DataTestStore.Create()`, in a way that
cannot change a single assertion and cannot re-introduce a leak.

The scan (`../data-test-substrate-map.md` §2) classified every substrate-touching test file into four
classes; this module is the execution of that classification:

| Class | Files | Substrate |
|---|---|---|
| **A — pure store** | 191 in scope (129 Data.Tests + 54 Server + 4 E2E + 4 Core) | memory |
| **B — archive** | 2 (`ColdArchiveCompactionTests`, `StoragePurgeTests`) | file until `archive-target`, then memory |
| **C — temp-only, no store** | 35 | **not migrated** (not store tests) |
| **D — file-semantics** | 3 smoke/legacy | file, leak-proof helper |

**Success looks like:** no store test in `tests/**` creates an ad-hoc temp store; every migrated test
passes with **construction/teardown changed and nothing else**; the gate's baseline is strictly
smaller than the `7183a59e` snapshot.

## Assumptions I'm making (correct me now)

1. **A migrated test's assertions never change.** If one must, the file was mis-classified and belongs
   in the file-bound set — that is a finding, not a fix (the pilot T9 proves the rule).
2. The helper's `Create()` is a drop-in for the ctor + `Init()` pair; the caller's `_store` field may
   be assigned from `DataTestStore.Create().Store`.
3. The four test projects that construct the store (Data, Server, E2E, Core) are all in scope; Server
   and E2E boot a `WebApplication`, so their construction seam differs (see §3).

## Design

### 1. The migration recipe (applied per file, mechanically)

1. Replace `new RpgStore(<temp dir>)` + `Directory.CreateDirectory` + `Init()` with
   `DataTestStore.Create()` (memory) — or `CreateFileBacked()` for the file-bound set only.
2. Replace the `Dispose`'s `try { Directory.Delete(...) } catch { }` with the helper's dispose.
3. **Convert `readOnly: true` memory-bound opens.** A shared-cache memory DB cannot be opened
   read-only (proven — `spec-memory-storage-plan.md` §2), so
   `SqliteConnectionFactory.Open(_store.HotPath, readOnly: true)` becomes a **plain open**
   (`SqliteConnectionFactory.Open(_store.HotPath)`); the test still reads, it just does not ask for a
   read-only handle. **The 9 sites in 8 files:**
   `ActionStoreTests` ×2 · `AllocationStoreTests` · `ChannelPolicyStoreTests` · `GateCounterStoreTests` ·
   `TreeStateVolumeTests` · `ItemGrantStoreTests` · `DomainImportTests` · `DomainProgressStoreTests`.
   The one file-bound read-only (`ColdArchiveCompactionTests.cs:93`, reading a real archive file)
   **keeps** `readOnly: true`.
4. Remove the file's line from `scripts/test-substrate-baseline.txt`. The ratchet only shrinks
   (`testing-standard.md` R4): a stale line fails the gate.
5. Nothing else changes. The batch's test filter + `guard-test-substrate.ps1` must pass with the
   baseline smaller.

### 2. Batch order (proving ground first)

- **Pilot (T9):** 5 representative files, including the only pilot file with read-only
  (`ActionStoreTests`). If the pattern is wrong, it is found here.
- **Then** the batches, largest-folder-first, each ≤ ~8 files (`tasks/data-test-substrate-todo.md`
  T11–T18f).

### 3. Server.Tests and E2E — different construction, same destination

`Server.Tests` (55 sites) and `E2E` (6) do not construct the store inline in a test class the way
Data.Tests does; they boot a `WebApplication`/build a service provider and register the store. The
migration there replaces the **store construction** in the test host setup with the helper's store,
preserving the DI registration shape. `Core.Tests` (4 sites) constructs the store directly, like Data.

### 4. The file-bound set — leak-proof, not memory

Five classes keep real files **and** get the helper's leak-proof file path:

| File | Why it must stay file |
|---|---|
| `LegacyMonoMigratorTests` | reads a real `rpg.sqlite`; asserts a disk backup sidecar |
| `RpgStoreDalSmokeTests` | `AssertWal` — `PRAGMA journal_mode` returns `'wal'` |
| `RpgStoreSmokeTests` | `File.Exists(HotPath/MediaPath)` |
| `ColdArchiveCompactionTests` | archive `*.sqlite` slices written/listed/verified on disk |
| `StoragePurgeTests` | purge deletes real archive files |

Converting these to memory would **delete real coverage**. They move onto `CreateFileBacked()`, which
fixes their leak without changing what they assert (module `archive-target` moves the archive two to
memory later; the legacy + smoke three stay file permanently).

**Finding from T14 (2026-09-12) — the classifier missed a second kind of file-bound case.** Class A
("pure store") was defined by *what the test asserts*, but a memory-bound test can also reach a file
**through the store's own API**: the archive entry points (`TrimSoulLedgerTails`,
`CompactAfterRunClosed`, `PromoteClosedRunCapture`, `TrimHotTailsNow`, `DeleteArchives`,
`PurgeClosedRunCapture`, `DeleteClosedRuns`) are file-backed by construction and **throw**
`StorePlanException` on a memory store (module `memory-storage-plan` §5). A store test that calls one
is file-bound even though its assertions are about ledger arithmetic.

The census (2026-09-12) found **3 mis-classified store files**: `ExpeditionRewardApplyTests` (1 of its
6 tests — `Soul_trim_keeps_a_mixed_earn_spend_ledger_consistent`), `SoulLedgerTrimTests`, and
`WebGameIsolationTests` (the last two are still temp-backed in the baseline and **must** use
`CreateFileBacked()` when their batches run, or they throw). `CompactionWorkerTests` (E2E) is **not**
one of them — the census flagged it on a call-site match, but it uses a `FakeHotCompactor` and never
constructs an `RpgStore`, so it is not a store test and must not be migrated. The rule for the real ones:

- If **only some methods** in a class need the archive (e.g. `ExpeditionRewardApplyTests` — 1 of 6),
  that method uses its own `DataTestStore.CreateFileBacked()` and the class stays memory.
- If the **class's subject is the archive**, it moves to the Tier-3 file-bound set.

This is the pilot's value restated: a per-file assertion count is not enough; a migration batch must
also scan for archive entry-point calls. `docs/architecture/data-test-substrate-map.md` §2's classifier
is corrected to include "calls a `RequireFileArchive`-guarded entry point" as a file-bound signal.

### 5. Exclusions

- `tests/FusionRpg.Data.Tests/CreatureSpeciesImportCliTests.cs` — owned by session
  `cold-process-test-build-20260912-e5b1`. Not touched; the batch that would contain it (T16) notes
  the handoff.
- `ci.yml` is shared with the same session; only the runtime alarm task (T19b) writes it, with owner
  coordination.

## Commands

```powershell
# Per batch (example filter)
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --filter "FullyQualifiedName~AffixStoreTests|FullyQualifiedName~AtomStoreTests"
# Every batch
.\scripts\guard-test-substrate.ps1
# The whole migration checkpoint
dotnet test tests/FusionRpg.Data.Tests --blame-hang
dotnet test tests/FusionRpg.Server.Tests --blame-hang
```

## Project structure

Changes are confined to `tests/FusionRpg.{Data,Server,E2E,Core}.Tests/**` and
`scripts/test-substrate-baseline.txt`. No `src/` change.

## Code style

```csharp
// Before
_dir = Path.Combine(Path.GetTempPath(), "fusionrpg-affixes-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(_dir);
_store = new RpgStore(_dir);
_store.Init();

// After
_testStore = DataTestStore.Create();
_store = _testStore.Store;
```

## Testing strategy

The migration's tests **are** the migrated tests: a batch is done when its files pass **unmodified**
apart from steps 1–3 and the gate's baseline shrank by that batch's file count.

| Concern | Test |
|---|---|
| Each batch passes | the batch's `dotnet test` filter |
| No assertion changed | `git diff` shows construction/teardown only |
| The baseline shrank | `guard-test-substrate.ps1` + `git diff scripts/test-substrate-baseline.txt` |
| The file-bound set still proves file semantics | `RpgStoreDalSmokeTests`/`RpgStoreSmokeTests` green |

**No population count is asserted** (`validation-ssot.md`); the "baseline shrank" check is a
relationship (line count decreased by the batch size), not a pinned total.

## Boundaries

- **Always:** change construction/teardown only; convert the 9 read-only sites; shrink the baseline.
- **Ask first:** any assertion change (a mis-classification finding); touching a file another session
  owns.
- **Never:** migrate the 5 file-bound classes to memory; touch `CreatureSpeciesImportCliTests.cs`;
  `catch { }` a delete; add a baseline line (that is a review event).

## Success criteria

1. No store test in `tests/**` creates an ad-hoc temp store (the gate's `temp-store` baseline reaches
   only genuinely file-bound or not-yet-migrated files).
2. Migrated tests pass with **zero assertion changes**.
3. The 9 memory-bound read-only sites are plain opens; the one file-bound read-only is unchanged.
4. The gate passes with a baseline strictly smaller than `7183a59e`.
5. A full Data+Server+E2E+Core run leaves no `rpg-*.sqlite` outside the file-bound temp dirs.

## Numeric types / Tunables / ActorHub gate

Not applicable — test-only. No magnitude, no tuning key, no stat surface.

## Open questions

None. The classification, the recipe, and the exclusion set are all fixed by the scan
(`../data-test-substrate-map.md` §2) and the owner's decisions.

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/test infrastructure → docs read this session.
- [x] Boundary recorded; drift owner-accepted; two exclusions named.
- [x] No decisions.md row owed.
- [x] Claims cite file:line (`ColdArchiveCompactionTests.cs:93`; the 9-site inventory from the scan).
- [x] Verified against code: all 9 read-only sites open `_store.HotPath` (read this session).
- [x] Constraints tested: read-only memory fails (spike).
- [x] No DAL change.
- [x] "Baseline shrank" is a relationship, not a count.
- [x] ActorHub: N/A.
- [x] File-bound set named so no future session "finishes" the migration by breaking them.
