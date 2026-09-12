# Capability map: `data-test-substrate`

**Status:** proposed 2026-09-12, after [data-test-substrate-ideal.md](data-test-substrate-ideal.md)
(idea phase, spike-audited). **Six modules.** Not a spec yet — the human reviews module boundaries,
dependency direction and build order here before any `spec-<module-id>.md` is written.

> **The program in one sentence.** A Data test must run against SQLite in RAM, because a file path
> was the only constructor the store had — so the store gains an in-memory storage plan, the tests
> move onto it through one leak-proof helper, and a probe makes any regression back to disk go red.

---

## 1. The problem, in one paragraph

The test suite writes ~1.6 MB of SQLite per test to the OS temp dir and fails to delete it: SQLite
connection pooling holds the file handle past `Dispose`, `Directory.Delete` throws `IOException`, and
**153 of 154** Data.Tests delete sites swallow it with an empty `catch { }`. One full run leaked
**30,978 dirs / 65.5 GB** (measured). The tests assert **our** SQL (schema, constraints, upserts,
watermarks, migrations), never SQLite's own disk engine — so the file substrate buys nothing and
costs SSD writes. Spike-proven this session: ATTACH across memory DBs works; `Open(readOnly:true)` on
shared memory fails; a process-wide `ClearAllPools` leaves an **open keeper** alive; reads are ~29×
faster in memory.

## 2. The test inventory (scanned 2026-09-12, before the modules were fixed)

Scanned every `tests/**/*.cs`. This is the evidence the spec must cover, not a guess.

| Measure | Count |
|---|---|
| Test files touching substrate patterns | **384** |
| Files that construct `RpgStore` | **196** / **210** sites |
| Files that create a temp dir (the leak surface) | **230** |
| — in the four approved test projects | **209** (Data 134, Server 55, Core 17, E2E 3) |
| — **outside** the boundary (Guard 9, Launcher 9, AtomImporter 3) | **21** |
| `Directory.Delete` sites with an empty `catch { }` | **153 of 154** in Data.Tests |
| `[Fact]`/`[Theory]` in scope | Data 1,249 · Server 399 · Core 8,292 · E2E 219 |

### The four substrate classes (every store file lands in exactly one)

| Class | Files | What they need | Substrate |
|---|---|---|---|
| **A — pure store** | **191** | SQL round-trips, constraints, upserts, watermarks. No assertion about a file. | memory |
| **B — archive** | **2** (`ColdArchiveCompactionTests`, `StoragePurgeTests`) | archive `*.sqlite` slices written, listed, verified, trimmed; purge deletes real files | memory-capable, but needs module 5's abstraction |
| **C — temp-only, no store** | **35** | subprocess fixtures, guard probes, seed-file writes | mostly stays file (see below) |
| **D — file-semantics** | **3 smoke/legacy** | see the file-bound set | **file** |

### The genuinely file-bound set — **five** files, not one

The scan found two the ideal under-counted, because they assert file/WAL behaviour directly:

| File | What it asserts that only a file can |
|---|---|
| `LegacyMonoMigratorTests` | reads a real `rpg.sqlite`; asserts a **disk backup sidecar** |
| `RpgStoreDalSmokeTests` | **`AssertWal`**: `PRAGMA journal_mode` returns `'wal'`; `File.Exists(HotPath/MediaPath)` |
| `RpgStoreSmokeTests` | `File.Exists(HotPath/MediaPath)`; legacy-hot-present no-op |
| `ColdArchiveCompactionTests` | archive slices on disk (`File.Exists`, `EnumerateFiles`) |
| `StoragePurgeTests` | purge deletes real archive files; path-escape guard |

**This is why the inventory step matters.** The smoke tests assert WAL mode and file existence —
behaviour an in-memory DB cannot reproduce (`journal_mode` degrades to `memory`, proven). They are
**not** in the ideal's original "~5–8" guess by accident; they are exactly the class the spec must
**exclude from memory on purpose** and name, rather than migrate and break.

### What the scan corrects in this map

1. **The in-scope store surface is 209 temp-creating files, not "~210 sites" loosely.** Module 3's
   real count is **A (191) + B (2) + D (3) = 196 files**, over 209 temp creators.
2. **`archive` is 2 files, not "a tail"** — but its `src` cost is unchanged (four writers + purge).
3. **21 temp-dir creators sit outside the approved boundary** (Guard 9, Launcher 9, AtomImporter 3).
   They leak too. Whether they are in this program is an **owner scope question** (module 7 below).
4. **A 6th substrate class exists that the ideal missed: C-temp-only (35 files).** They are not
   store tests; most write a *fixture* or drive a subprocess. They should **not** move to the store
   helper — a different (smaller) concern.

---

## 3. The modules

| # | Module id | Responsibility | Depends on |
|---|---|---|---|
| 1 | `memory-storage-plan` | **The `src` extension.** `SqliteConnectionFactory` learns to open a `file:…mode=memory&cache=shared` URI (skip `GetFullPath`/`CreateDirectory`/forced `ReadWriteCreate`); `RpgStore` gains a storage plan that makes hot + media named shared-memory DBs with one keeper connection each, and skips `LegacyMonoMigrator` / archive-dir creation under it. Production `RpgStore(dataDir)` unchanged. | — |
| 2 | `test-store-helper` | One shared fixture/helper (`DataTestStore.Create()` memory default, `CreateFileBacked()` for file semantics) that constructs the store, holds the keepers, and disposes them; a failed temp-delete in file mode is a **failure, not a swallowed catch**. Test-only. | 1 |
| 3 | `store-test-migration` | Move the store sites onto the helper: **Data.Tests first** (145), then Server.Tests (55), E2E (6), Core.Tests (4). Bodies keep their assertions; only construction/teardown move. Includes the 10 `Open(readOnly:true)` sites switching to a plain in-memory open. | 1, 2 |
| 4 | `disk-write-probe` | **The guard.** A CI/guard check that counts the test temp root before/after a run and fails on any survivor, plus a `0` `rpg-*.sqlite` assertion for the in-memory suite. A regression to disk is caught at the line. | 2 |
| 5 | `archive-target` | **Optional follow-on.** An archive-*target* abstraction (create / open / list / exists / delete) threaded through the four compaction writers and purge, so archive slices can also be memory-backed. Without it, archive tests stay file-backed. | 1 |
| 6 | `substrate-standard` | The binding standard: store tests default to in-memory; disk only when the thing under test is a file; cleanup failure is a failure; never swallow a temp-delete. Plus the `data-architecture.md` amendment and the "no read-only memory open" rule. | 1, 3, 4 |

**Why `archive-target` is its own module and separable.** The spike proves archive *can* be memory,
but the code addresses it straight through the filesystem — four writers each do
`Directory.CreateDirectory(ArchiveDir)` + `Path.Combine` (`RpgStore.Compaction.cs:140,276,484,623`),
`WriteCaptureArchiveFile(absPath, …)` opens that path (`:348-353`), and purge does
`Path.GetFullPath(Path.Combine(_dataDir, uri…))` (`:711`). That is a real refactor, not a branch.
It also has a genuine cut point: modules 1–4 reach essentially all 210 store sites and remove the
bulk of the writes without it. Cutting 5 leaves only the archive/purge tests file-backed.

### Dependency graph

```text
memory-storage-plan ──┬──► test-store-helper ──┬──► store-test-migration ──► substrate-standard
                      │                         └──► disk-write-probe ──────► substrate-standard
                      └──► archive-target ─────────► store-test-migration   (archive/purge sites only)
```

No cycles. Every arrow points one way.

### Build order

```text
memory-storage-plan
  -> test-store-helper
  -> store-test-migration   (Data.Tests only — the proving ground)
  -> disk-write-probe
  -> archive-target          (optional; gates only the archive/purge slice of 3)
  -> store-test-migration   (Server.Tests, E2E, Core.Tests)
  -> substrate-standard
```

**Why this order.** Module 1 is the whole unlock and is independently verifiable (an in-memory store
round-trips SQL). Module 2 makes disposal leak-proof *before* any test migrates, so the migration
cannot re-introduce a leak. Data.Tests alone (134 temp creators, 145 store sites) is the proving
ground — if the shape is wrong it is found against the smallest blast radius. The probe lands
**before** the remaining projects migrate, so the rest is guarded as it moves. `archive-target` is
sequenced after the probe so it cannot delay the bulk win; `substrate-standard` is last because it
documents a shape that must already exist and be enforced.

### The file-bound exclusion is part of module 3, not a failure

Module 3 must **not** migrate the five file-bound files to memory. It must move them onto the
helper's `CreateFileBacked()` path, which fixes their leak (clear pools → delete, failure not
swallowed) *without* changing what they assert. `RpgStoreDalSmokeTests`'s `AssertWal` and the two
`File.Exists` smoke assertions are **real coverage of the production file substrate**; converting
them to memory would delete that coverage. The spec states this explicitly so a future session does
not "finish the migration" by breaking them.

## 4. What the program deliberately does not do

| Excluded | Why |
|---|---|
| Delete the Data test suite | The tests guard our SQL surface and caught real store bugs. The defect is the substrate, not the coverage. |
| Fix the leak while keeping files | Removes the leak but keeps ~1.6 MB × 210 writes/run — the owner's actual objection. |
| Transaction-rollback isolation | Our store commits internally; rollback cannot undo it (documented precondition absent). |
| EF Core in-memory provider / mock `IRpgDb` | No raw SQL / deletes exactly what these tests prove. |
| One shared in-memory DB wiped between tests | xUnit runs in parallel; a shared DB is cross-test interference. |
| Re-tune CI runtime in this program | Orthogonal (e.g. `ZombossAdaptiveStoreTests` 30× `Init()`); a separate concern. |
| Change production server storage | `RpgStore(dataDir)` and `FUSIONRPG_DATA` stay file-backed. |
| Migrate the 35 temp-only (class C) files | They are not store tests; most write a fixture or drive a subprocess. A different, smaller concern. |
| Migrate the 5 file-bound files to memory | `AssertWal` / `File.Exists` / legacy sidecar are real coverage of the production file substrate. They get the leak-proof file helper, not memory. |

### Scope question the scan surfaced (owner decision)

**21 temp-dir creators leak outside this program's boundary** — `FusionRpg.Guard.Tests` (9),
`FusionRpg.Launcher.Tests` (9), `FusionRpg.AtomImporter.Tests` (3). They create temp dirs and swallow
deletes, so they leak on every run exactly like the store tests did. They are not in the six modules
because they are not SQL-store tests, and three of them are **outside the approved test paths**
(Guard/Launcher/AtomImporter). Options: (a) leave them to a later program, (b) widen this program's
`paths` and add a 7th module `non-store-temp-cleanup`, or (c) apply only the shared leak-proof
delete helper (no memory) to them. The scan is why this is visible now instead of after the store
work ships.

**Resolved by the gate, not deferred.** The `disk-write-probe` module was promoted ahead of the
migration and landed as `scripts/guard-test-substrate.ps1` (committed `7183a59e`), so all 21
out-of-boundary leakers are now **baselined and ratcheted** rather than silently leaking: the gate
covers `tests/**` regardless of project, and each must shrink as its project is touched. The
separate `non-store-temp-cleanup` module is therefore **not needed** — the gate plus the existing
baseline reaches them without widening this program's `paths`.

## 5. Amendments this program owes before it builds

Listed so they are not discovered mid-task. All are reviewed changes to documents that win over a spec.

| Document | Change | Status / owed by |
|---|---|---|
| `docs/contributing/testing-standard.md` | The binding standard (R1–R5) + the gate | ✅ **Landed 2026-09-12** (`7183a59e`) |
| `scripts/guard-test-substrate.ps1` + `scripts/test-substrate-baseline.txt` | The hard gate + the shrinking ratchet | ✅ **Landed 2026-09-12** (`7183a59e`); wired into `deploy-play.ps1`, `ci.yml`, Guard.Tests |
| `AGENTS.md` / `CLAUDE.md` | A pointer to the standard and the gate | ✅ **Edited locally 2026-09-12** — both gitignored/untracked, so the durable record is the standard, not the pointer |
| [decisions.md](decisions.md) | A new row locking the store's **storage plan** seam (file default; memory is test-first) — locks behavior, so it needs an ADR row first | owed by `memory-storage-plan` |
| [data-architecture.md](data-architecture.md) §1/§6 | State that `RpgStore` has a storage plan; the DAL boundary is unchanged (all SQL still in Data); and a shared-cache memory DB cannot be opened read-only | owed by `substrate-standard` |

## 6. Related

- Ideal: [data-test-substrate-ideal.md](data-test-substrate-ideal.md) — spike-audited, rev 2
- The DAL law: [data-architecture.md](data-architecture.md) §6 · `scripts/guard-dal.ps1`
- The validation standard this probe must obey (assert the relationship, never a population count):
  [validation-ssot.md](validation-ssot.md)
- Plan/todo (Phase 2, not yet written): `tasks/data-test-substrate-plan.md` · `tasks/data-test-substrate-todo.md`
