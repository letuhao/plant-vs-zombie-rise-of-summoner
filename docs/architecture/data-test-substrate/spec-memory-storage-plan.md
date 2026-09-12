# Spec: `memory-storage-plan`

**Module id:** `memory-storage-plan` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 1 of 6
**Model calls:** none. **The module every other module depends on** — it is the `src` extension that
makes an in-memory store possible at all.

## Objective

Give `RpgStore` a **storage plan** so it can run against named shared-memory SQLite databases instead
of the two files it has always used, with **zero behavior change** for the production file path.

Today `RpgStore` has exactly one constructor — `RpgStore(string dataDir)` (`RpgStore.cs:32`) — and it
always computes `_hotPath`/`_mediaPath` as files (`:36-37`). The tests build a temp dir, call `Init()`,
and leak 65.5 GB because SQLite pooling holds the file handle past `Dispose` (proven). The fix starts
here: the store must be constructible against RAM.

**Success looks like:** `RpgStore.InMemory()` returns a store that `Init()`s the full schema and
round-trips SQL with **no file on disk**, while `RpgStore(dataDir)` is byte-for-byte the current
production behavior.

## Assumptions I'm making (correct me now)

1. **`RpgStore(string dataDir)` must keep working unchanged.** Production (`Program.cs:326-332`) and
   `FUSIONRPG_DATA` depend on it; this module is test-first, production is untouched.
2. **The storage plan is a store-level concept, not a per-connection one.** The factory learns to
   *open* a memory URI; the store decides *which* URIs.
3. **One keeper connection per memory database** is held for the store's lifetime. Proven: an open
   keeper survives a process-wide `ClearAllPools`, a pooled-only DB does not.
4. **Legacy migration is skipped in memory**, because it reads a real `rpg.sqlite` from disk.
5. **No `data/tuning/` file is involved** — this is structural, not balance (see Tunables).

## Design

### 1. The seam — a private plan constructor beside the public one

**The audit caught a defect in this spec's own first draft:** `new RpgStore(uri)` **cannot** work. The
constructor does `_dataDir = Path.GetFullPath(dataDir)` (`:35`) then
`_hotPath = Path.Combine(_dataDir, HotFileName)` (`:36`) — a memory URI passed as `dataDir` would be
rewritten into a filesystem path and re-create a file. The public constructor must stay file-only.

The shape is therefore a **private constructor taking an explicit plan**, with two public factories:

```csharp
public RpgStore(string dataDir)              // :32  PRODUCTION PATH — unchanged, file plan
private RpgStore(string hotPath, string mediaPath, bool inMemory)  // the real seam
public static RpgStore InMemory()           // builds two unique memory URIs
```

`InMemory()` builds `file:rpg-hot-{guid}?mode=memory&cache=shared` and the media twin, holds nothing
else, and the store opens its keepers. `_dataDir` is left **empty** for the memory plan (see §5 —
archive must fail loudly, not resolve a bogus dir).

The two `Open` funnels are the entire substrate (`:3835`, `:3837`):

```csharp
private SqliteConnection OpenUnlocked()      => SqliteConnectionFactory.Open(_hotPath);
private SqliteConnection OpenMediaUnlocked() => SqliteConnectionFactory.Open(_mediaPath);
```

They keep their signature. What changes is what `_hotPath`/`_mediaPath` **contain** — a file path for
the file plan, a memory URI for the memory plan. `HotPath`/`MediaPath` (`:24-25`) return that value
unchanged, so the **82 test call sites** that do `SqliteConnectionFactory.Open(_store.HotPath)` keep
working verbatim.

### 2. `SqliteConnectionFactory` — recognize a memory URI (the only branch)

Current (`SqliteConnectionFactory.cs:8-24`): `Path.GetFullPath(path)` (`:10`),
`Directory.CreateDirectory` (`:13`), `Mode = ReadWriteCreate` (`:18`), `Cache = Shared` (`:19`).

The memory branch:

```csharp
public static SqliteConnection Open(string path, bool readOnly = false)
{
    if (IsMemoryUri(path))                       // "file:" + "mode=memory"
        return OpenMemory(path);                 // no GetFullPath, no CreateDirectory
    ... existing file path unchanged ...
}
```

- **`Cache=Shared` is already set** (`:19`) — it is exactly what a shared-memory DB requires, so the
  locking/cache model does not change.
- **`PRAGMA journal_mode=WAL`** (`:32`) is a no-op on memory (`journal_mode` returns `memory`,
  proven). The memory branch sets `busy_timeout`/`synchronous`/`temp_store` but **omits** WAL rather
  than asking for a mode memory cannot provide.
- **`readOnly: true` + memory must be rejected explicitly** (throw), not passed through — SQLite
  cannot open a shared-cache memory DB read-only (proven), and today's `readOnly` path would silently
  produce a confusing failure. The 10 test read-only sites move to a plain open (module 3).
- **Production is untouched:** the file branch is the current code.

### 3. Lifetime — the keeper (this is the load-bearing part)

A shared-memory DB exists only while **at least one connection is open**. The store holds one keeper
`SqliteConnection` per memory DB (`_hotKeeper`, `_mediaKeeper`) opened in the constructor and disposed
in `Dispose`. Proven behaviors the spec relies on:

| Behavior | Verified |
|---|---|
| A second connection sees the first's schema **and rows** | yes (`cache=shared`) |
| The DB vanishes when all connections close | yes |
| An **open keeper survives a global `ClearAllPools`** | yes — parallel-safe |
| A pooled-only DB does **not** survive `ClearAllPools` | yes — so per-test clearing is unnecessary |
| 300 unique DBs created+closed, no clear | bounded (heap 18→7 MB after clear) |

**Uniqueness:** the memory DB name is per-store-unique (`rpg-hot-{guid}` / `rpg-media-{guid}`) so
xUnit's parallelism cannot cross two tests. This is why the plan uses unique names, not one shared DB.

**`IDisposable` is required, and it is new on this type.** `RpgStore` does **not** implement
`IDisposable` today (verified). Adding it is mandated for keeper release. Two consequences the spec
must state:
- The file plan's `Dispose` is a **no-op** (both keepers null), so production behavior is unchanged.
- `Program.cs:332` does `AddSingleton(new RpgStore(dataDir))` — the DI container **will** call
  `Dispose` at shutdown once the type is `IDisposable`. That is correct and harmless (no-op), but the
  implementer must not put any other teardown in it.

### 4. `Init()` — skip the file-only steps under the memory plan

`Init()` (`:46-51`) unconditionally does `Directory.CreateDirectory(_dataDir)` /
`Directory.CreateDirectory(ArchiveDir)` (`:48-49`) and runs `LegacyMonoMigrator.TryMigrate` /
`HealOrphanMediaTables` (`:50-51`). Under the memory plan these four steps are skipped (there is no
dir, and there is no legacy file to migrate). Everything else — `EnsureHotSchema`, `ShardRungs.Migrate`,
`EnsureMediaSchema`, `SeedPlayerIfEmpty`, backfills — runs **unchanged**, so a memory store is
schema-identical to a file store. Idempotency confirmed: `EnsureHotSchema` declares **46**
`CREATE TABLE IF NOT EXISTS` and **24** `CREATE INDEX IF NOT EXISTS`, zero bare `CREATE TABLE`
(verified), so re-`Init()` on an existing memory DB is safe.

### 5. Archive and path properties under the memory plan — fail loudly

`ArchiveDir` is `Path.Combine(_dataDir, "archive")` (`:26`) and the archive path resolver does
`Path.GetFullPath(Path.Combine(_dataDir, uri…))` (`RpgStore.Compaction.cs:711`). A memory store has no
`_dataDir`, so **every archive entry point must throw under the memory plan** — four writers
(`Compaction.cs:140,276,484,623`), purge (`Storage.cs:258-288`), and `PromoteClosedRunCapture`. It
must **not** resolve `Path.Combine("", "archive")` into a cwd-relative path and silently write there —
that is exactly the class of invisible file leak this program exists to end.

`DataDir` (`:23`) returns `""` for the memory plan. Note: **no `src/` code reads `DataDir`/`HotPath`/
`MediaPath`/`ArchiveDir`** (verified — the sole consumers are tests and the archive internals above),
so this is a test-facing property, and a test must never assert it is a real path on a memory store.

### 6. What is deliberately NOT here

- **Archive** stays file-addressed. Making it memory is module `archive-target`, a separate refactor.
  A memory store that calls an archive entry point **throws** (§5), and the archive tests are
  file-bound (`data-test-substrate-map.md` §2).
- **`Reset()`** (`:837`) calls `ClearAllPools()` and deletes archive files (`:928-935`) then re-`Init()`.
  Under memory it must clear the keepers' contents and re-`Init()` — **not** touch the filesystem. Its
  `ClearAllPools` (`:930`) is harmless to an open keeper (proven). Named so the implementer does not
  assume `Reset` is transparent.
- **The test helper** (`DataTestStore`) is module `test-store-helper`, on top of this.

## Commands

```powershell
# Build + the module's own tests
dotnet build src/FusionRpg.Data/FusionRpg.Data.csproj
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --filter "FullyQualifiedName~MemoryStoragePlan"

# The production path must not move
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj

# DAL boundary + substrate gate
.\scripts\guard-dal.ps1
.\scripts\guard-test-substrate.ps1
```

## Project structure

```
src/FusionRpg.Data/Sqlite/
  SqliteConnectionFactory.cs   →  + memory-URI branch (the one unlock)
  RpgStore.cs                  →  + InMemory()/plan field, keeper(s), Init() skip, Dispose
  LegacyMonoMigrator.cs        →  unchanged (skipped, never edited)
tests/FusionRpg.Data.Tests/
  MemoryStoragePlanTests.cs    →  new: memory round-trip + production-path parity
docs/architecture/data-test-substrate/spec-memory-storage-plan.md   → this file
```

## Code style

```csharp
// The plan is decided once, in the private constructor. One place owns the substrate.
private readonly bool _inMemory;
private readonly string _dataDir;          // "" under the memory plan
private SqliteConnection? _hotKeeper;
private SqliteConnection? _mediaKeeper;

private RpgStore(string hotPath, string mediaPath, string dataDir, bool inMemory)
{
    _hotPath = hotPath; _mediaPath = mediaPath; _dataDir = dataDir; _inMemory = inMemory;
}

public static RpgStore InMemory()
{
    var id = Guid.NewGuid().ToString("N");
    var hot = SqliteConnectionFactory.MemoryUri("rpg-hot-" + id);
    var media = SqliteConnectionFactory.MemoryUri("rpg-media-" + id);
    var store = new RpgStore(hot, media, dataDir: "", inMemory: true);
    store._hotKeeper = SqliteConnectionFactory.Open(hot);
    store._mediaKeeper = SqliteConnectionFactory.Open(media);
    return store;
}
```

Conventions: the memory URI is built by **one** factory helper (`MemoryUri`), never inlined at call
sites; the keeper lifetime is the store's, and `Dispose` is the only releaser; a comment states **why**
production is unaffected at the URI branch.

## Testing strategy

xUnit, `tests/FusionRpg.Data.Tests` (net8.0). Tests land **with** the code (this module is the
substrate other modules build on, so its tests are the proof).

| Concern | Test |
|---|---|
| Memory store round-trips SQL with no file | `MemoryStoragePlanTests` |
| Memory store is schema-identical to file store | same table set from `sqlite_master` |
| No file is created (`HotPath`/`MediaPath` are not files) | `File.Exists` false after `Init()` |
| A memory store's archive call **throws** (no silent cwd write) | `Assert.Throws` on `PromoteClosedRunCapture` |
| Read-only + memory is rejected | `Assert.Throws` at the factory |
| Production `RpgStore(dataDir)` unchanged | existing `RpgStoreSmokeTests` / `RpgStoreDalSmokeTests` stay green |
| Parallel isolation | two memory stores, distinct data |
| Keeper holds the DB across open/close | close a working conn, reopen, data present |
| `Reset()` on a memory store touches no file | reset, then `File.Exists` false + data cleared |

**Assert the contract, never a count** (`validation-ssot.md`): assert the table-set relationship and
row round-trips, never "N tables" or "N files".

## Boundaries

- **Always:** keep `RpgStore(string dataDir)` behavior identical; route every open through
  `SqliteConnectionFactory`; dispose keepers; run `guard-dal.ps1` and `guard-test-substrate.ps1`.
- **Ask first:** any change to `RpgStore(string dataDir)` or `Init()`'s file path; adding a public
  `RpgStoreOptions` type; touching `LegacyMonoMigrator`.
- **Never:** open SQLite read-only against a shared-memory DB (proven impossible — `readOnly: true`
  fails); `ClearAllPools` as a per-test requirement (unnecessary with a keeper); add a `data/tuning/`
  entry for storage; let production take the memory branch.

## Success criteria

1. `RpgStore.InMemory()` `Init()`s the full schema and round-trips SQL with **zero** files created.
2. The memory store's table set equals the file store's (`sqlite_master`).
3. `RpgStore(string dataDir)` + `FUSIONRPG_DATA` path is unchanged — the existing Data smoke tests
   pass **without modification**.
4. Two memory stores are isolated under parallel execution.
5. `guard-dal.ps1` and `guard-test-substrate.ps1` pass; the new test file introduces no gate entry.
6. No `data/tuning/` change.

## Numeric types

No magnitude is produced or consumed — this is storage plumbing. No overflow surface. (Recorded
because the spec template asks, and "none" is the honest answer.)

## Tunables

**None.** The storage plan is structural: changing it breaks *whether the system works*, not *how the
game feels*, which `tunables-ssot.md` §1 defines as **not a tunable**. No `data/tuning/` key is
introduced, and none may be.

## ActorHub gate

Not applicable — this produces no actor combat/derived/AppliedCombat number and consumes none. It is
persistence substrate, below the stat stack.

## Open questions

1. **Shape:** static `RpgStore.InMemory()` + private plan constructor, vs a public `RpgStoreOptions`
   record. This spec recommends the **private constructor + static factory now** — the smallest
   surface, and the audit showed a public `RpgStore(uri)` overload is a trap (the ctor's
   `Path.GetFullPath`/`Path.Combine` at `:35-36` would mangle a URI). `RpgStoreOptions` can wrap it
   later if a second plan appears.
2. **`IDisposable` on the production type:** required for keeper release. Confirm adding it is
   acceptable — the file plan's `Dispose` is a no-op, and `AddSingleton(new RpgStore(...))`
   (`Program.cs:332`) will simply call it at shutdown.
3. **`Reset()` under memory:** this spec says clear the keepers' contents and re-`Init()`. Idempotency
   is verified (`EnsureHotSchema` is all `IF NOT EXISTS`), so the remaining question is only whether
   `Reset` should instead recreate the DB — recommend the reuse path.

**Resolved by the audit (were open in the first draft):** the production-path constructor cannot take
a URI (§1); archive must throw rather than resolve `Path.Combine("", "archive")` (§5); `DataDir` is
`""` and has no `src/` consumer (§5); schema idempotency is confirmed, not assumed (§4).

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem identified: **Data / SQL / schema** → read `data-architecture.md` and
      `contributing/architecture-map.md` this session.
- [x] Boundary recorded (`data-test-substrate-20260912-9d6a`); drift noted and owner-accepted.
- [x] `decisions.md` checked — no lock covers test storage; this module **owes** a storage-plan row.
- [x] Every factual claim cites file:line (`RpgStore.cs:32,36-37,46-51,3835,3837,837,930`;
      `SqliteConnectionFactory.cs:8-24,32`).
- [x] Claims verified against code, not comments.
- [x] Constraints tested, not assumed: read-only memory **fails**, keeper survives `ClearAllPools`,
      ATTACH works, WAL no-op — all spiked this session.
- [x] No §2 invariant contradicted. **DAL boundary preserved**: the memory branch lives inside
      `FusionRpg.Data`, so `guard-dal.ps1` still passes and no SQL leaves Data.
- [x] No population-count assertion; no balance literal.
- [x] ActorHub gate: not applicable (stated why).
- [x] No new parallel path; `BattleStatComposer` untouched.
