# Data test substrate — the ideal

**Status:** graduated 2026-09-12 — see below. This doc is the reasoning trail: **it is not the spec,
and it is not the contract** (the six specs under `data-test-substrate/` are).
**Graduated 2026-09-12:** the capability map and all six module specs are written
([map](data-test-substrate-map.md); specs under `data-test-substrate/`), the hard gate shipped
(`7183a59e`), and the plan + task list exist (`tasks/data-test-substrate-{plan,todo}.md`). This ideal
is kept as the reasoning trail; **the specs are the contract and the build may proceed**.

The problem: the local C# test suite writes **~1.6 MB of SQLite per test** to the OS temp
directory and then fails to delete it. One full run leaked **30,978 directories / 65.5 GB**; the
owner reclaimed it and reported it as SSD wear. This doc proposes moving the suite **completely to
in-memory SQLite** — including the small tail that looked file-bound — and extends `src` so an
in-memory store is a first-class thing the store can be constructed as.

**Which loop this extends.** None of the ten loops in
[`docs/guide/the-loops.md`](../guide/the-loops.md). This is **developer infrastructure** for the
**Data** module (`software-architecture.md:51`), the persistence harness every loop rides on. It
adds no loop, stock, class, or clock.

---

## What this is

**A store test runs against a database that lives in RAM.** SQLite does this natively: a named
in-memory database with `Mode=Memory;Cache=Shared`. The repo's tests assert **our** layer — schema,
constraints, upserts, watermarks, migrations, archive bookkeeping — none of which is about SQLite's
disk engine. They wrote files only because a file path was the **only constructor the store had**
(`RpgStore.cs:32`).

The deliverable: the default Data test creates **zero files**; the previously file-bound tail runs
on a memory *or* tmpfs-backed substrate through the same helper, so even it stops wearing the SSD;
and a probe makes any regression to real disk **red instead of silent**.

---

## What already exists (three buckets)

### Built — the mechanisms this rides on

| Thing | Where | What proves it |
|---|---|---|
| Single connection funnel | `SqliteConnectionFactory.Open` (`src/FusionRpg.Data/Sqlite/SqliteConnectionFactory.cs:9`) | Every hot/media open goes through it. |
| Store funnels to two properties | `OpenUnlocked()` / `OpenMediaUnlocked()` (`RpgStore.cs:3835`, `:3837`) | `=> SqliteConnectionFactory.Open(_hotPath)` / `(_mediaPath)` — the whole substrate is two lines. |
| `Cache=Shared` already the mode | `SqliteConnectionFactory.cs:19` | Exactly what shared-memory requires; locking model unchanged. |
| Store can be wiped in place | `RpgStore.Reset()` (`RpgStore.cs:837`) | Calls `ClearAllPools()` (`:930`); SIM suite already uses it. |
| `HotPath`/`MediaPath` are a public test seam | `ActionStoreTests.cs:207` etc. | Tests already reopen the store's own file for verification. |
| Leak cause proven | spike this session (see §Prior art) | Pooling holds the handle past `Dispose`; `ClearAllPools()` frees it. |

### Measured disk surface

| Measure | Count |
|---|---|
| `new RpgStore(` in tests | **210** — Data 145, Server 55, E2E 6, Core 4 |
| `Directory.Delete(` in tests | **310** |
| `Path.GetTempPath` in tests | **320**, in 242 files |
| `.Init()` in tests | **219**, in 197 files |
| `ClearAllPools` in tests | **15**, in 14 files (296 of 310 deletes never clear the pool) |
| Data.Tests deletes with empty `catch { }` | **153 of 154** (all 153 catch bodies empty, verified) |
| `Open(..., readOnly: true)` in tests | **10**, all in Data.Tests; **zero in `src/`** |
| Leak from one run | **30,978** dirs / **65.5 GB** (~1.6 MB hot + 36 KB media each) |

### Wiring gap — machinery exists, inert for memory

- **`SqliteConnectionFactory` cannot address a memory URI.** `Open` does `Path.GetFullPath(path)`
  (`:10`) and forces `Mode = ReadWriteCreate` (`:18`). A `file:name?mode=memory` URI is not a filesystem
  path. **One branch fixes it** (detect a `file:` URI, skip `GetFullPath`/`CreateDirectory`, pass
  `Mode=Memory;Cache=Shared`). Proven sufficient by the spike below.
- **`Init()` unconditionally touches disk.** `Directory.CreateDirectory(_dataDir)` / `ArchiveDir`,
  `LegacyMonoMigrator.TryMigrate`, `HealOrphanMediaTables` (`RpgStore.cs:46` body). Inert for memory;
  skipped behind the storage plan.
- **`ArchiveDir` is a path by construction** (`RpgStore.cs:26`) — but the spike shows archive is not
  actually file-bound (below).

### Real gap

- **No "this store is in memory" anywhere.** One constructor (`:32`); nothing in `FusionRpg.Data`
  mentions `:memory:`/`Mode=Memory` (grep, this session).
- **No leak guard.** Nothing counts temp dirs before/after a run; nothing fails a swallowed delete.

---

## Prior art — and what the live spike proved

### External (cited, not re-derived)

- **Mechanism.** `Data Source=<name>;Mode=Memory;Cache=Shared` shares one named in-memory DB across
  connections; it persists **only while at least one connection stays open**
  ([Microsoft.Data.Sqlite in-memory](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/in-memory-databases),
  [connection strings](https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings)).
  Keeper connection is the documented idiom.
- **Isolation.** Each unique name is natural per-test isolation with no file
  ([EF Core testing](https://learn.microsoft.com/en-us/ef/core/testing/choosing-a-test-strategy)).
- **Rollback is unavailable to us.** Per-test transaction rollback (3–5× faster than delete) needs the
  code under test to not commit internally ([dotnet-guide](https://www.dotnet-guide.com/articles/ef-core-sqlite-inmemory-testing/)).
  Our store **commits internally**, so fresh unique DBs are the correct isolation.
- **FK-ordering trap.** `PRAGMA foreign_keys=ON` must run before schema creation
  ([dotnet-guide](https://www.dotnet-guide.com/articles/ef-core-sqlite-inmemory-testing/)) — carried to Open questions.

### Spike results (run this session, then removed)

| # | Question | Result |
|---|---|---|
| Speed | memory vs file, 2000 insert (txn) + 2000 point select | **insert 7 ms vs 8 ms; select 3 ms vs 88 ms** — ~29× on reads, writes comparable because WAL already batches |
| Keeper | pooled reopen of same name after keeper close | survives **without** `ClearAllPools` (pool holds it) — so `ClearAllPools` is not needed per test |
| Pooling=false | DB freed when keeper closes | confirmed: table gone (fresh) — gives a *deterministic* release option |
| Memory growth | 300 unique DBs created+closed, no clear | heap 18 MB → 7 MB after `ClearAllPools`; bounded |
| **ATTACH** | archive's `ATTACH ... AS hot` (`RpgStore.Compaction.cs:353`) against memory | **works** when both connections open via `file:` URI — memory→memory **and** file-cold→memory-hot |
| **read-only** | `Open(name, readOnly: true)` against a memory DB | **fails** (`unable to open database file`) — SQLite cannot open a *shared-cache memory* DB read-only |
| Parallel safety | does a global `ClearAllPools` kill another test's **open keeper**? | **No** — an open keeper survives; only pooled-idle DBs die |

**The spike inverted my rev-1 claim.** I had written that ~5–8 classes "genuinely need files." On
the evidence, **archive/compaction is not one of them** — ATTACH across memory DBs works, and the
cold archive can itself be a named memory DB. The only true file-bound case found is
**legacy migration** (reads a real `rpg.sqlite` on disk). The read-only case is a 10-site test idiom,
not a product need (`src/` never opens read-only), and is trivially replaced.

---

## The shape

**One seam, four tiers.** The store gains a **storage plan**; tests stop hand-rolling temp dirs.

```
RpgStore(string dataDir)                       // existing — file plan, unchanged
RpgStore.InMemory()  (or RpgStoreOptions)      // new  — hot+media are named shared-memory DBs,
                                               //         one keeper each; no legacy migrate
```

**Tier 0 — the `src` extension (the "memory test probe").**
`SqliteConnectionFactory` gains a URI-aware mode and the store gains a storage plan:

- `SqliteConnectionFactory.Open` detects a `file:` URI / `mode=memory` and opens it without
  `Path.GetFullPath`, `CreateDirectory`, or forced `ReadWriteCreate`.
- A `SqliteConnectionFactory` memory helper builds `file:{name}?mode=memory&cache=shared` and, in a
  variant, `Pooling=false` for deterministic release.
- `RpgStore` holds a **keeper** `SqliteConnection` per DB (hot, media, and — if memory-backed —
  each archive) for its lifetime.
- `Init()` skips `Directory.CreateDirectory` / `LegacyMonoMigrator` / `HealOrphanMediaTables` under
  the memory plan.
- `HotPath`/`MediaPath`/`ArchiveDir` return the memory URIs so **existing test idioms keep working
  verbatim** (including `Open(_store.HotPath)`).
- Production path is untouched: `RpgStore(dataDir)` behaves exactly as today.

**Tier 1 — in-memory default (~all 210 sites).** Each test constructs `DataTestStore.Create()` →
an in-memory store. Unique name per test = parallel-safe isolation. Because a keeper is held,
`ClearAllPools` is **never needed** for correctness (proven), and the DB dies with the keeper.

**Tier 2 — archive/compaction, also memory (the biggest `src` change).** The spike proves an archive
DB *can* be memory (`ATTACH 'file:…mode=memory…' AS hot` works, memory→memory and file-hot→memory).
But the compaction code is written **directly against the filesystem**, not through an abstraction:
four archive writers each do `Directory.CreateDirectory(ArchiveDir)` + `Path.Combine(ArchiveDir,
fileName)` (`RpgStore.Compaction.cs:140,276,484,623`), hand the abs path to
`WriteCaptureArchiveFile(absPath, …)` which opens it with `SqliteConnectionFactory.Open` and
`ATTACH` (`:348-353`), and the purge / `ArchivePathFor` side does `Path.GetFullPath(Path.Combine(
_dataDir, uri…))` (`:711`). So Tier 2 is **not a one-line seam** — it is a real
`IArchiveStore`-shaped abstraction (create / open / list / exists / delete over an archive *target*
that may be a file or a named memory DB), threaded through compaction and purge.

For that reason **Tier 2 is separable and optional**: it is the one place where "completely to
memory" costs a moderate `src` refactor rather than a branch. The recommendation is to ship
Tier 0/1/3/4 first (which reaches ~all 210 store sites and kills the bulk of the writes) and treat
Tier 2 as a **named follow-on**. The tests keep **logical** assertions (a slice exists, verify,
trim) either way; only *how* the archive target is addressed changes. `StoragePurgeTests.cs:81-126`
and `ColdArchiveCompactionTests.cs:86-364` are the affected assertions; the one that writes
`File.WriteAllText` over `ArchiveDir` (`:146`) is inherently a path-semantics test and stays Tier 3
regardless.

**Tier 3 — genuinely file-bound (now believed to be: legacy migration only).**
`LegacyMonoMigratorTests` reads a real legacy `rpg.sqlite` and asserts a backup sidecar on disk
(`LegacyMonoMigratorTests.cs:26-33`) — inherently files. It keeps real files through the same
helper's file mode with `ClearAllPools()` before delete, and **a failed delete is a test failure**,
never a swallowed catch. If the owner wants *zero* disk, this single class can run against a
`tmpfs`/RAM disk, but that is an OS feature, not a repo one — flagged, not assumed.

**Tier 4 — the probe (what you asked for).**
Two guards so this cannot drift back:
1. **Leak alarm** — count the test temp root before/after a run; any survivor fails the run and
   names the creating test. Turns invisible accumulation red.
2. **0-disk assertion** — a CI check asserts the in-memory suite creates **0** `rpg-*.sqlite`
   files. A regression to disk is caught at the line.

**One shared helper** replaces 210 constructors + 310 swallowed deletes: `DataTestStore.Create()`
(memory default) / `CreateFileBacked()` (Tier 3), `IDisposable`/`IAsyncDisposable`, disposing
keepers, clearing pools before delete in file mode. Test bodies keep assertions; only
construction/teardown move.

**Alternatives rejected:** delete the suite (trades SSD for correctness); keep files + fix `catch`
(still writes — your actual objection); transaction rollback (store commits internally);
EF in-memory provider (no raw SQL); mock `IRpgDb` (deletes what these tests prove); one shared DB
(cross-test interference under xUnit parallelism).

---

## What this deliberately does not decide

- **CI runtime** — a separate concern (`ZombossAdaptiveStoreTests` was 314 s via 30× `Init()`; fix is
  store reuse, orthogonal). This removes disk I/O; it does not re-tune the test count.
- **Full `RpgStoreOptions` record vs static `InMemory()`** — spec-level shape call.
- **Whether `FUSIONRPG_DATA` becomes memory-aware for Server/E2E** — a spec decision; the four Core
  and six E2E sites are in scope but their wiring differs (env var vs constructor).

---

## Tunables

Infrastructure; **no balance tunable**, and none must be added (structural knobs live in code):

| Knob | Home | Note |
|---|---|---|
| Storage plan (`File`/`Memory`) | a `const`/enum in `FusionRpg.Data` | Structural; says why not tunable. |
| Keeper lifetime, `Pooling=false` choice | the store instance | Structural. |
| Leak-guard scope + "0 files" assertion | CI guard script | Assert the **relationship** (delta = 0), never a population count (`validation-ssot.md`). |

---

## Open questions — owner decisions only

1. **Confirm the reach: all four projects in one program, or Data.Tests first?** Rev 2 says memory
   is achievable for ~all 210 sites; Data.Tests is the clean proving ground (145 of them).
2. **Archive in memory (Tier 2) — accept the small `src` archive-existence seam?** This is the one
   addition beyond the factory + storage plan. Without it, archive tests stay file-backed.
3. **Tier 3 legacy-migration test:** keep one real-file class, or also route it to a RAM disk?
4. **The 65.5 GB cleanup** — delete now (age-filtered around the running `dotnet test`), or wait for
   idle? Destructive; needs your call.
5. **Standard home** — `data-architecture.md` §1, or new `docs/contributing/testing-standard.md`?
6. **FK enforcement** — spec-time verify-against-code (the false-green trap). Not an owner
   preference, but it decides whether the probe needs an FK case.

---

## Honest gaps (DESIGN-GATE §5)

- **Session boundary not recorded.** New program `data-test-substrate`; paths:
  `src/FusionRpg.Data/Sqlite/**`, `tests/FusionRpg.{Data,Server,E2E,Core}.Tests/**`, `scripts/**`,
  `docs/architecture/data-architecture.md`. No active session owns these today. `/session-start`
  must run and the record land **before the first code edit**.
- **One spike artifact remains in the tree:** `tests/FusionRpg.Data.Tests/ZombossAdaptiveStoreTests.cs`
  carries an earlier same-session edit (the 30× `Init()` → shared-store fix). It is **not** part of
  this program's `paths`; it should be reverted or committed on its own before this program starts.
- **`software-architecture.md` read; `decisions.md` checked** — no lock covers test storage; the
  storage plan will add one at spec time.
- **Read-only memory is impossible** (proven). The 10 test sites must switch to a plain open for
  inspection; the spec must name that substitution explicitly so no one "restores" read-only.
