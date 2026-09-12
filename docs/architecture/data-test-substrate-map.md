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

## 2. The modules

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
cannot re-introduce a leak. Data.Tests alone (145 of 210 sites) is the proving ground — if the shape
is wrong it is found against the smallest blast radius. The probe lands **before** the remaining
projects migrate, so the rest is guarded as it moves. `archive-target` is sequenced after the probe
so it cannot delay the bulk win; `substrate-standard` is last because it documents a shape that must
already exist and be enforced.

## 3. What the program deliberately does not do

| Excluded | Why |
|---|---|
| Delete the Data test suite | The tests guard our SQL surface and caught real store bugs. The defect is the substrate, not the coverage. |
| Fix the leak while keeping files | Removes the leak but keeps ~1.6 MB × 210 writes/run — the owner's actual objection. |
| Transaction-rollback isolation | Our store commits internally; rollback cannot undo it (documented precondition absent). |
| EF Core in-memory provider / mock `IRpgDb` | No raw SQL / deletes exactly what these tests prove. |
| One shared in-memory DB wiped between tests | xUnit runs in parallel; a shared DB is cross-test interference. |
| Re-tune CI runtime in this program | Orthogonal (e.g. `ZombossAdaptiveStoreTests` 30× `Init()`); a separate concern. |
| Change production server storage | `RpgStore(dataDir)` and `FUSIONRPG_DATA` stay file-backed. |

## 4. Amendments this program owes before it builds

Listed so they are not discovered mid-task. All are reviewed changes to documents that win over a spec.

| Document | Change | Owed by |
|---|---|---|
| [decisions.md](decisions.md) | A new row locking the store's **storage plan** seam (file default; memory is test-first) — this locks behavior, so it needs an ADR row first | `memory-storage-plan` |
| [data-architecture.md](data-architecture.md) §1/§6 | State that `RpgStore` has a storage plan; that the DAL boundary is unchanged (all SQL still in Data); and that a shared-cache memory DB cannot be opened read-only | `substrate-standard` |
| [contributing/session-boundary.md](contributing/session-boundary.md) | None. Noted only because the drift checker currently reports a `tasks/sessions/**` glob overlap between this record and `session-boundary-standard-20260912-a3f2` — see Handoff | — |

## 5. Related

- Ideal: [data-test-substrate-ideal.md](data-test-substrate-ideal.md) — spike-audited, rev 2
- The DAL law: [data-architecture.md](data-architecture.md) §6 · `scripts/guard-dal.ps1`
- The validation standard this probe must obey (assert the relationship, never a population count):
  [validation-ssot.md](validation-ssot.md)
- Plan/todo (Phase 2, not yet written): `tasks/data-test-substrate-plan.md` · `tasks/data-test-substrate-todo.md`
