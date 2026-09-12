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

### 1. The seam — one private plan, three public doors (owner: Q1 = "both, maximum customization")

**The audit caught a defect in this spec's own first draft:** `new RpgStore(uri)` **cannot** work. The
constructor does `_dataDir = Path.GetFullPath(dataDir)` (`:35`) then
`_hotPath = Path.Combine(_dataDir, HotFileName)` (`:36`) — a memory URI passed as `dataDir` would be
rewritten into a filesystem path and re-create a file.

**Owner decision (2026-09-12):** do not close the API to one door. Offer the `RpgStoreOptions` record
**and** a boolean on the string constructor, so callers customize freely; a test must never be blocked
by a narrow seam. Both are safe because **the memory plan never lets a URI reach `GetFullPath`**.

```csharp
// The existing string constructor is REPLACED by this one — the optional bool defaults to false,
// so every existing `new RpgStore(dataDir)` call binds here unchanged. There is exactly one string
// constructor, not two overloads (two would be ambiguous-looking and redundant).
// Owner Q1 option 3 — bool selects the plan. When inMemory == true, dataDir is IGNORED
// (unique memory names are generated); it is never passed to GetFullPath/Path.Combine.
public RpgStore(string dataDir, bool inMemory = false)

// Owner Q1 option 2 — full customization at the boundary.
public sealed record RpgStoreOptions
{
    public string? DataDir { get; init; }        // file plan only; ignored when InMemory
    public bool InMemory { get; init; }
    public string? HotName { get; init; }        // name a specific memory DB (default: unique guid)
    public string? MediaName { get; init; }
}
public RpgStore(RpgStoreOptions options)

// Convenience factory — the 95% case. Sugar over the options door.
public static RpgStore InMemory()
```

**The one safety rule that keeps both doors honest:** if `InMemory` is true, `DataDir` is never
resolved — a plan where `InMemory && DataDir` is set is a caller error, and a `DataDir` that looks
like a URI (`file:` / `mode=memory`) under the file plan **throws** rather than being silently
`GetFullPath`-mangled. That is the audit's finding, enforced instead of documented.

`_dataDir` is left **empty** for the memory plan (see §5 — archive must fail loudly, not resolve a
bogus dir).

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

**Uniqueness and parallelism (owner decision 2026-09-12 — this is a requirement, not a nicety).**
Every construction is an **independent instance** with its own two memory DBs named
`rpg-hot-{guid}` / `rpg-media-{guid}`. **There is no singleton and no process-wide shared DB** — the
owner's words: *"the memory db should support multiple instance, singleton option will make the test
unable to run parallel, cause our test slow and burden; it should support instance depend on each PC
resources."* Concretely:
- Each store owns its keepers; two stores share nothing. xUnit's own parallelism then scales to the
  machine's cores with no lock and no cross-test interference.
- Because a keeper is per-instance and survives a global `ClearAllPools` (proven), one test's cleanup
  cannot kill another test's DB.
- The keeper count is one per DB per instance (2 per store), so N parallel stores cost 2N open
  connections — bounded, and the 300-instance spike stayed bounded (heap 18→7 MB after clear).

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
  **Owner decision (2026-09-12): either in-place clear+re-`Init` or recreate the memory DBs is
  acceptable — "nothing different" from the file plan's own observable result** (empty schema, ready to
  use). The only invariant: `Reset()` on a memory store must **never touch the filesystem** and must
  leave a usable empty store. The archive-delete branch is file-only and is skipped. The existing
  `ClearAllPools` (`:930`) is harmless to an open keeper (proven) and may stay; if the recreate path is
  chosen, the old keepers are disposed and the archive branch stays skipped. The implementer picks
  whichever keeps `Reset` semantically identical; the spec does not force one.
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
// One private plan decides the substrate; the public doors all funnel into it.
private readonly bool _inMemory;
private readonly string _dataDir;          // "" under the memory plan
private SqliteConnection? _hotKeeper;
private SqliteConnection? _mediaKeeper;

// The single real seam. Every public constructor resolves to this.
private RpgStore(string hotPath, string mediaPath, string dataDir, bool inMemory)
{
    _hotPath = hotPath; _mediaPath = mediaPath; _dataDir = dataDir; _inMemory = inMemory;
}

// Owner Q1 option 3 — the bool door. One line: build options, funnel to the options door.
public RpgStore(string dataDir, bool inMemory = false)
    : this(RpgStoreOptions.For(dataDir, inMemory)) { }

// Owner Q1 option 2 — the options door. RpgStoreOptions.Resolve is the ONLY place that
// decides file-vs-memory, and it THROWS if a file:/mode=memory URI arrives with InMemory == false.
public RpgStore(RpgStoreOptions options)
    : this(RpgStoreOptions.Resolve(options).Hot, RpgStoreOptions.Resolve(options).Media,
           options.InMemory ? "" : Path.GetFullPath(options.DataDir!), options.InMemory) { }

// The 95% convenience door. Unique names per call = parallel-safe by construction.
public static RpgStore InMemory() => new(RpgStoreOptions.For("", inMemory: true));
```

Conventions: the memory URI is built by **one** factory helper (`SqliteConnectionFactory.MemoryUri`),
never inlined at call sites; `RpgStoreOptions.Resolve` is the **only** place that decides
file-vs-memory, and it **throws** if a `file:`/`mode=memory` URI is passed with `InMemory == false`
(the audit's trap, enforced); the private constructor opens the two keepers **iff** `inMemory`, so no
public door can forget them; the keeper lifetime is the store's, and `Dispose` is the only releaser; a
comment states **why** production is unaffected at the URI branch.

## Testing strategy

xUnit, `tests/FusionRpg.Data.Tests` (net8.0). Tests land **with** the code (this module is the
substrate other modules build on, so its tests are the proof).

| Concern | Test |
|---|---|
| Memory store round-trips SQL with no file | `MemoryStoragePlanTests` |
| Memory store is schema-identical to file store | same table set from `sqlite_master` |
| No file is created (`HotPath`/`MediaPath` are not files) | `File.Exists` false after `Init()` |
| All three doors work (`InMemory()`, `(dir, true)`, `RpgStoreOptions{InMemory}`) | one test per door, same assertions |
| The URI trap is enforced: `file:`/`mode=memory` with `inMemory:false` **throws** | `Assert.Throws` — not silently file-created |
| A memory store's archive call **throws** (no silent cwd write) | `Assert.Throws` on `PromoteClosedRunCapture` |
| Read-only + memory is rejected | `Assert.Throws` at the factory |
| Production `RpgStore(dataDir)` unchanged | existing `RpgStoreSmokeTests` / `RpgStoreDalSmokeTests` stay green |
| **Parallel independence** — N stores concurrently, no cross-talk, no clear | `Parallel.For` over many stores, each asserts its own rows |
| Keeper holds the DB across open/close | close a working conn, reopen, data present |
| `Reset()` on a memory store touches no file and leaves an empty usable store | reset, then `File.Exists` false + schema present + data cleared |

**Assert the contract, never a count** (`validation-ssot.md`): assert the table-set relationship and
row round-trips, never "N tables" or "N files".

## Boundaries

- **Always:** keep `RpgStore(string dataDir)` behavior identical; route every open through
  `SqliteConnectionFactory`; every memory store is an independent instance (no singleton, no shared
  DB); dispose keepers; run `guard-dal.ps1` and `guard-test-substrate.ps1`.
- **Ask first:** any change to `RpgStore(string dataDir)`'s file behavior or `Init()`'s file path;
  touching `LegacyMonoMigrator`; shrinking the public construction API (Q1 opened it deliberately).
- **Never:** open SQLite read-only against a shared-memory DB (proven impossible — `readOnly: true`
  fails); let a `file:`/`mode=memory` URI reach `GetFullPath` under the file plan; use a singleton or
  process-wide shared memory DB (breaks parallel test scaling); silently no-op an archive call on a
  memory store; add a `data/tuning/` entry for storage; let production take the memory branch.

## Success criteria

1. `RpgStore.InMemory()` `Init()`s the full schema and round-trips SQL with **zero** files created.
2. **All three public doors work** — `InMemory()`, `RpgStore(dataDir, inMemory:true)`, and
   `RpgStore(new RpgStoreOptions { InMemory = true })` — and produce the same usable store (Q1).
3. The memory store's table set equals the file store's (`sqlite_master`).
4. `RpgStore(string dataDir)` + `FUSIONRPG_DATA` path is unchanged — the existing Data smoke tests
   pass **without modification**.
5. **N memory stores run concurrently, isolated** — no singleton, no shared DB, no lock; scale is the
   machine's cores (Q3).
6. A memory URI with `inMemory:false`, read-only+memory, and any archive entry point on a memory store
   all **throw** rather than silently acting.
7. `guard-dal.ps1` and `guard-test-substrate.ps1` pass; the new test file introduces no gate entry.
8. No `data/tuning/` change.

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

## Owner decisions (2026-09-12) — all four answered

Recorded verbatim in intent, so downstream tasks do not re-litigate them:

1. **Seam shape (Q1) — "add both option 2 and 3, maximum customize instead of trying to close them;
   it blocks the test."** → **Adopted.** Both `RpgStore(string dataDir, bool inMemory)` **and** a
   public `RpgStoreOptions` record are public, over one private plan constructor. The API is opened,
   not narrowed. The audit's URI trap is handled by an explicit **throw** in `RpgStoreOptions.Resolve`
   (URI + `inMemory:false`) rather than by removing a door.
2. **Disposal (Q2) — `RpgStore` implements `IDisposable` (recommended).** → **Adopted.** `Dispose`
   closes keepers; the file plan's `Dispose` is a no-op; `AddSingleton(new RpgStore(...))` calling it
   at shutdown is correct.
3. **`Reset()` in memory (Q3) — "option 1 or 2, nothing different; also the memory db should support
   multiple instances; a singleton would make the test unable to run parallel and burden it — it
   should support instances per the PC's resources."** → **Adopted both halves:** (a) in-place clear
   **or** recreate are both acceptable, with the one invariant that `Reset` never touches the
   filesystem and leaves an empty usable store; (b) **no singleton** — every store is an independent
   instance and parallelism scales to cores (§3). This is a hard requirement, stated in the success
   criteria (#5).
4. **Archive call on a memory store (Q4) — dedicated exception with a named message (recommended).**
   → **Adopted.** A distinct `StorePlanException` (or dedicated `InvalidOperationException`) whose
   message names the memory plan and points at the `archive-target` module. It must **not** no-op and
   must **not** resolve a cwd-relative path.

**No open questions remain for this module.** The audit-resolved items (URI trap, archive throw,
`DataDir == ""`, schema idempotency) are in §1/§4/§5 and are enforced by tests, not prose.

## Deliberate non-decisions (deferred, not open)

- Whether `archive-target` eventually supports a memory archive — separate module.
- Whether `RpgStoreOptions` grows past the four fields above — add fields when a real consumer needs
  one, not speculatively (but the type is public now, per Q1, so this is additive and non-breaking).

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
