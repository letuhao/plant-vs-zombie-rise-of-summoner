# Spec: `test-store-helper`

**Module id:** `test-store-helper` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 2 of 6
**Depends on:** [`memory-storage-plan`](spec-memory-storage-plan.md) (the store's in-memory storage plan).
**Model calls:** none.

## Objective

Give every store test **one** way to construct and dispose a store, so the 210 ad-hoc
`new RpgStore(tempDir)` + swallowed-`Directory.Delete` pairs become a single leak-proof call.

Today a store test opens with this shape, repeated in 196 files (`AffixStoreTests.cs:17-29` is typical):

```csharp
public class AffixStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    public AffixStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-affixes-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }
    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }
}
```

**153 of 154** Data.Tests delete sites are exactly that swallowed catch. SQLite pooling holds the
file handle past `Dispose`, so the delete throws and the swallow hides it — one run leaked
**30,978 dirs / 65.5 GB**. The helper makes the correct thing the easy thing.

**Success looks like:** a test writes `using var store = DataTestStore.Create();` and gets an
initialized in-memory store with no temp dir; a test that genuinely needs a file writes
`using var store = DataTestStore.CreateFileBacked();` and a failed delete **fails the test**.

## Assumptions I'm making (correct me now)

1. The helper lives in `tests/FusionRpg.Data.Tests` and is reachable by the other test projects that
   construct the store. `InternalsVisibleTo("FusionRpg.Data.Tests")` already exists
   (`FusionRpg.Data`), so the helper may use internals if needed.
2. A test file may still keep a `readonly RpgStore _store` field and assign it from
   `DataTestStore.Create().Store`, or hold the `DataTestStore` itself; the helper does not force a
   base class (the 196 subclasses differ too much to unify).
3. The helper is test-only; nothing in `src/` references it.

## Design

### 1. The surface — two factories, one disposable

```csharp
public sealed class DataTestStore : IDisposable, IAsyncDisposable
{
    public RpgStore Store { get; }
    public string? DataDir { get; }          // null for memory

    public static DataTestStore Create();             // in-memory (the default)
    public static DataTestStore CreateFileBacked();   // real files, leak-proof
    public void Dispose();
    public ValueTask DisposeAsync();
}
```

- `Create()` returns a store built via `RpgStore.InMemory()` (`spec-memory-storage-plan.md` §1), already
  `Init()`ed. **No temp dir, no file.**
- `CreateFileBacked()` builds a unique temp dir + `new RpgStore(dir)` + `Init()`, and records the dir
  for leak-proof disposal. **Only** the file-bound set uses it.
- `Store` is always initialized, so the caller never calls `Init()` itself.

### 2. Disposal — the R3 rule, mechanized

`Create()`'s dispose disposes the store (which releases the keepers — `spec §3`). `CreateFileBacked()`'s
dispose does, in order:

```csharp
public void Dispose()
{
    _store.Dispose();                       // releases keepers / closes the file
    if (_dataDir is not null)
    {
        SqliteConnection.ClearAllPools();   // pooling holds the handle otherwise (proven)
        Directory.Delete(_dataDir, recursive: true);   // NO catch — a failure is a test failure
    }
}
```

**The failure is not caught.** If `Directory.Delete` throws, the test fails and names the directory.
That is the whole point: `testing-standard.md` R3 bans the swallow, and the gate
(`guard-test-substrate.ps1`) mechanically catches a re-introduced one.

### 3. Why not a base class or an xUnit fixture

- **Not a base class:** the 196 test classes have incompatible constructors (some seed via helper
  methods, some take `ITestOutputHelper`); inheritance would force a rewrite far beyond construction.
- **Not `IClassFixture`/`ICollectionFixture`:** a shared fixture would share **one** store across tests,
  breaking the parallel-isolation requirement (owner Q3) and the "no singleton" rule.
  `Create()` per test is the point.

### 4. What this deliberately does not do

- Does not migrate any test (that is `store-test-migration`).
- Does not add a leak alarm (that is `disk-write-probe`; the helper's file path is what the alarm
  protects).
- Does not change `RpgStore` (that is `memory-storage-plan`).

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --filter "FullyQualifiedName~DataTestStore"
.\scripts\guard-test-substrate.ps1
```

## Project structure

```
tests/FusionRpg.Data.Tests/
  DataTestStore.cs        → the helper
  DataTestStoreTests.cs   → the helper's own proof tests
```

## Code style

```csharp
// Memory — the default. No file, no cleanup, parallel-safe by unique instance.
using var store = DataTestStore.Create();
var row = store.Store.UpsertAtom(...);

// File semantics only — still leak-proof; a failed delete throws.
using var fileStore = DataTestStore.CreateFileBacked();
```

Conventions: the caller never calls `Init()` (the helper does); the caller never calls
`SqliteConnection.ClearAllPools()`; no `catch { }` around a delete anywhere.

## Testing strategy

xUnit; the helper's own tests in `DataTestStoreTests`.

| Concern | Test |
|---|---|
| `Create()` yields an initialized store that round-trips SQL | upsert/select |
| `Create()` creates **no** directory | no temp dir with the helper prefix exists |
| `CreateFileBacked()` creates a real dir and deletes it on dispose | after dispose, the dir is gone |
| A **held** file store still deletes (pooling cleared) | open a conn, dispose, dir gone |
| A failed delete is a **failure**, not a swallow | inject an undeletable dir → `Dispose` throws |
| Two `Create()` stores are independent | distinct data, distinct DB names |
| The helper introduces **no** gate baseline entry | `guard-test-substrate.ps1` output |

**Assert the relationship, never a count** (`validation-ssot.md`): the no-file check asserts "no dir
with this store's name exists", never "N dirs".

## Boundaries

- **Always:** the helper `Init()`s; memory is the default; a failed file delete throws.
- **Ask first:** adding a third factory; changing the memory/file default.
- **Never:** `catch { }` around a delete; a shared/singleton fixture store; put helper code in `src/`.

## Success criteria

1. `DataTestStore.Create()` returns an initialized in-memory store with zero files.
2. `DataTestStore.CreateFileBacked()` returns an initialized file store whose dispose deletes the dir.
3. A failed file delete **throws** (never swallowed).
4. The helper adds no `guard-test-substrate` baseline line.
5. `guard-dal.ps1` unaffected (no `src/` change).

## Numeric types / Tunables / ActorHub gate

Not applicable — test-only infrastructure. No magnitude, no `data/tuning/` key, no stat surface.

## Open questions

None. The contract is fixed by `memory-storage-plan` §1/§3 and `testing-standard.md` R3.

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/test infrastructure → `data-architecture.md`, `architecture-map.md` read.
- [x] Boundary recorded; drift owner-accepted.
- [x] No decisions.md row owed (test-only).
- [x] Claims cite file:line (`AffixStoreTests.cs:17-29`; the 153/154 swallow count from the scan).
- [x] Verified against code (the representative test read this session).
- [x] Constraints tested: pooling holds the handle; `ClearAllPools` frees it (spike).
- [x] No DAL boundary change (test project only).
- [x] No population-count assertion.
- [x] ActorHub: N/A.
- [x] No new parallel path.
