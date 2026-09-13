# Implementation plan: `data-test-substrate`

**Capability map:** [docs/architecture/data-test-substrate-map.md](../docs/architecture/data-test-substrate-map.md) — 6 modules.
**Ideal:** [docs/architecture/data-test-substrate-ideal.md](../docs/architecture/data-test-substrate-ideal.md) — spike-audited rev 2.
**Module 1 spec:** [docs/architecture/data-test-substrate/spec-memory-storage-plan.md](../docs/architecture/data-test-substrate/spec-memory-storage-plan.md) — approved 2026-09-12.
**Tasks:** [data-test-substrate-todo.md](data-test-substrate-todo.md).
Named pair per repo convention — `tasks/plan.md` / `tasks/todo.md` belong to the perf stream and were not read.

> **Definition of Done:** this repo has no vendored DoD doc; the standing bar is
> `CONTRIBUTING.md` + `AGENTS.md` (build + the touched project's tests + the boundary guards). Each
> task below states its own verification against that bar.

---

## Overview

Move the C# store test suite off the SSD and onto in-memory SQLite. A store test asserts **our** SQL
layer, never SQLite's disk engine; it wrote files only because a file path was the only constructor the
store had. One full local run leaked **30,978 temp dirs / 65.5 GB** because connection pooling held the
file handle past `Dispose` and 153 of 154 delete sites swallowed the exception.

This plan delivers, in order: the `src` storage plan (module 1), the leak-proof test helper (module 2),
a piloted migration of every store test to memory (module 3), the archive abstraction for the last
file-bound slice (module 5), and the binding standard + ADR (module 6). **The hard gate (module 4)
already shipped** (`scripts/guard-test-substrate.ps1`, commit `7183a59e`) and now baselines + ratchets
all 216 pre-existing violations, so **no new violation can be introduced while this plan runs**.

---

## Architecture decisions (locked in the spec; restated so the task list is readable)

- **One private plan, three public doors** (owner Q1): the existing `RpgStore(string dataDir, bool
  inMemory = false)` ctor, a public `RpgStoreOptions` record, and a static `RpgStore.InMemory()` sugar.
  The API is opened, not narrowed.
- **The URI trap is enforced, not documented:** a `file:`/`mode=memory` URI with `InMemory == false`
  **throws** in `RpgStoreOptions.Resolve`, because the production ctor's `GetFullPath`/`Path.Combine`
  would otherwise mangle it into a file path.
- **No singleton, no shared DB** (owner Q3): every store is an independent instance with its own
  `rpg-hot-{guid}`/`rpg-media-{guid}` and its own keepers, so tests parallelize to the machine's cores.
- **One keeper connection per memory DB**, held for the store's lifetime, released in `Dispose`.
  Proven: an open keeper survives a global `ClearAllPools`; a pooled-only DB does not — so per-test
  clearing is unnecessary.
- **Archive stays file-addressed permanently** — module 5 (`archive-target`) was **cut by the owner on 2026-09-13** (see Phase 5).
  A memory store's archive entry point **throws** until then.
- **The 5 file-bound test classes never move to memory** (`RpgStoreDalSmokeTests` asserts
  `journal_mode == 'wal'`; `RpgStoreSmokeTests` asserts `File.Exists`; `LegacyMonoMigratorTests` asserts
  a disk sidecar; `ColdArchiveCompactionTests` / `StoragePurgeTests` assert archive files on disk).
  They get the leak-proof **file** helper.
- **No `data/tuning/` key.** The storage plan is structural, per `tunables-ssot.md` §1.

## Dependency graph

```text
T0a..T0e module specs (each before its module's first task)
T1 decisions.md ADR ─┐
                     ├─► T2 factory memory-URI branch ─► T3 store plan + keepers + Dispose ─► T4 Init/Reset/archive-throw ─► T5 module-1 tests
                     │
T5 ─► T6 helper core (Create/CreateFileBacked/Dispose) ─► T7 helper cleanup (ClearAllPools + assert) ─► T8 helper tests
T8 ─► T9 pilot migration (5 files) ─► T10 checkpoint-verify
T10 ─► T11..T18f migration batches (Data.Tests folders, Server, E2E, Core, file-bound tail) ─► T19 checkpoint-verify ─► T19b runtime leak alarm
T5 ─► (T20a–T22 archive-target — CUT by owner 2026-09-13)
T19b,T23 ─► T24 final gate
T5 ─► T25 fix the 124.6s test (done)
T6 ─► T26 tag DiskSemantics ─► T27 tag Heavy ─► T28 test-fast.ps1 + profiles (CI full, nightly, release gate) ─► T29 profile checkpoint
```

Build order follows the graph. T0a–T0e are the module specs the gated workflow requires before their
implementation; T1 is a doc unlock; T2–T5 are the foundation; T6–T8 the helper; T9–T19 the migration;
T19b the runtime probe; T23–T24 the standard and final gate; T25–T29 the
test profiles (owner decision 2026-09-12 — the default dev/agent run writes nothing and runs no long
test).

---

## Phases and the slice each one delivers

| Phase | Slice — what is demonstrably true at the end | Tasks |
|---|---|---|
| **0** | Every module has a spec before its implementation (the gated workflow's per-module Specify) | T0a–T0f |
| **1** | An in-memory `RpgStore` works end-to-end with no file on disk; production path byte-identical | T1–T5 |
| **2** | One leak-proof test helper exists; a failed temp-delete is a failure, not a swallow | T6–T8 |
| **3** | ⭐ **The proving ground** — 5 store tests run in memory; the pattern is proven at minimum blast radius | T9–T10 |
| **4** | ⭐ **The migration** — every in-memory-safe store test runs on RAM; the file-bound 5 use the leak-proof file helper; a runtime alarm proves zero leaks | T11–T19b |
| **5** | ~~Archive slices are memory-capable too~~ — **CUT by owner 2026-09-13** (premise changed; the classes are already `DiskSemantics`-excluded and leak-proof) | T20–T22 |
| **6** | The standard is binding, documented, and the whole suite is leak-proof | T23–T24 |
| **7** | ⭐ **Quiet default** — local dev/agents write nothing to disk and run no ≥20s test, while CI/nightly/release still run everything | T25–T29 |

**Phase 5 is CUT (owner, 2026-09-13).** The decision to keep it (2026-09-12) rested on those two archive
classes writing disk in the default dev loop with a swallowing cleanup. Both facts changed before Phase 5
ran: they are now `DiskSemantics`-tagged (T26 → excluded from `default`) and leak-proof through
`DataTestStore.CreateFileBacked()` (T18f → a failed cleanup throws). The remaining gain — RAM instead of
ephemeral CI disk for two already-excluded classes — does not justify refactoring 793 lines of production
archive data-movement plus 15 file-system assertions. The file-bound set therefore stays at **six**
classes; archive stays file-bound and `DiskSemantics` permanently. Full before/after evidence is in
`data-test-substrate-todo.md` Phase 5.

**Phase 7 is the owner's "dev loop must not be harmful" answer.** It does not delete coverage: the
tagged tests still run in CI, nightly, and at the release gate. It exists because the measurements
showed the cost was real — 102 baseline files still wrote disk, 16 tests ran ≥20s, and the single
worst test (124.6s, this program's own) has been fixed to 0.52s.

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| A migrated test asserts a file/WAL property that memory cannot reproduce | High | The scan already classified every file (`data-test-substrate-map.md` §2); the 5 file-bound classes are explicitly excluded, and the pilot (T9) catches a mis-classification early |
| **A migrated test re-opens `_store.HotPath` with `readOnly: true`** — 9 sites across 8 files; a shared-cache memory DB **cannot** be opened read-only (proven), so each throws once the store is in memory | High | Each batch that contains one of the 9 sites converts it to a **plain open** (remove only `readOnly: true`; the read stays a read). The 9 sites are named in T11/T12/T15/T17/T18b/T18c and the pilot's T9. This is a per-file edit, not a global replacement — the file-bound `ColdArchiveCompactionTests` keeps its read-only open because it reads a real archive file |
| The migration silently re-introduces a leak | High | The gate (`guard-test-substrate.ps1`) baselines + ratchets; each batch task must **shrink** the baseline line for the files it fixes, and the gate fails on a stale line (`testing-standard.md` R4) |
| Parallel test regression (cross-test DB interference) | High | T5 includes a `Parallel.For` independence test before any migration; unique per-instance names are structural, not convention |
| `ClearAllPools` in one test killing another's DB | Med | Proven an open keeper survives it; T5 asserts this explicitly |
| Production path drifts | High | T3/T5 assert the existing `RpgStoreSmokeTests`/`RpgStoreDalSmokeTests` pass **unmodified** |
| Migration touches a file another session owns | Med | `CreatureSpeciesImportCliTests.cs` is excluded (owned by `cold-process-test-build-e5b1`); `ci.yml` is shared and owner-coordinated |
| Archive abstraction grows past the cut point | Med | **Resolved by the cut** (2026-09-13): the module is not built, so the risk is gone — see Phase 5 |
| `IDisposable` on the production type changes DI shutdown | Low | File plan's `Dispose` is a no-op; verified `Program.cs:332` calls it harmlessly |

## Module specs (written 2026-09-12 — all six complete)

The map says each module gets a `spec-<module-id>.md` and the gated workflow says Specify precedes
Plan/Tasks **per module**. All six now exist under `docs/architecture/data-test-substrate/`, so no
implementation task starts against an unspecified contract:

| Module | Spec | Status |
|---|---|---|
| `memory-storage-plan` | [`spec-memory-storage-plan.md`](../docs/architecture/data-test-substrate/spec-memory-storage-plan.md) | ✅ written (audit caught the `new RpgStore(uri)` defect) |
| `test-store-helper` | [`spec-test-store-helper.md`](../docs/architecture/data-test-substrate/spec-test-store-helper.md) | ✅ written |
| `store-test-migration` | [`spec-store-test-migration.md`](../docs/architecture/data-test-substrate/spec-store-test-migration.md) | ✅ written (read-only conversion + file-bound set) |
| `disk-write-probe` | [`spec-disk-write-probe.md`](../docs/architecture/data-test-substrate/spec-disk-write-probe.md) | ✅ written (static shipped; runtime alarm specified) |
| `archive-target` | [`spec-archive-target.md`](../docs/architecture/data-test-substrate/spec-archive-target.md) | ✅ written (the deepest module, explicitly cuttable) |
| `substrate-standard` | [`spec-substrate-standard.md`](../docs/architecture/data-test-substrate/spec-substrate-standard.md) | ✅ written (R1–R5 shipped; amendment specified) |

## Out of scope (spec/ideal-locked)
Deleting the suite; fixing the leak while keeping files; transaction-rollback isolation (store commits
internally); EF in-memory provider or mocking `IRpgDb`; one shared wiped DB; re-tuning CI runtime
(e.g. `ZombossAdaptiveStoreTests`' 30× `Init()`); production server storage; the 21 temp-leakers
outside the four test projects (they are baselined and ratcheted by the gate, not migrated here);
`CreatureSpeciesImportCliTests.cs` (another session).

## Open items

- ~~Spec approval~~ — approved 2026-09-12; all four owner questions decided and folded in.
- **No open questions remain.** The archive tail (T20–T22) was **cut by the owner on 2026-09-13** once its
  premise changed (the two classes are already excluded from the default profile and leak-proof); see
  Phase 5 for the before/after evidence. `spec-archive-target.md` remains the contract if a future
  program wants it.
- **Owner-run at the end:** the final gate (T24) includes a full suite run whose duration and
  temp-dir delta are reported; the 65.5 GB cleanup is an owner action, not a task here.

---

## Parallelization

**The fan-out protocol is binding for any multi-agent wave:**
[data-test-substrate/fan-out-protocol.md](../docs/architecture/data-test-substrate/fan-out-protocol.md).
It names the three collision points — `scripts/test-substrate-baseline.txt` (contiguous lines; two
batches conflict at the boundary), `AGENTS.md`/`CLAUDE.md` (gitignored; 36 tests use `AGENTS.md` as
their repo-root marker), and `.github/workflows/ci.yml` (T19b + T28) — and gives each exactly one
writer. Subagents follow the recipe, run their focused filter + the read-only gate, and **report**;
the gatekeeper session deletes the baseline lines, runs the independent gate, and commits.

**Blockers cleared 2026-09-12 (Wave 0):** `.kilo/setup-script.ps1` now copies `AGENTS.md` +
`CLAUDE.md` into every new worktree (verified: parses, and an existing worktree has neither file so
the copy fires); the single-writer rule is recorded; the 124.6s test is fixed to 0.52s.

**Wave shape (owner decision: clean the blocks, then fan out everything):**

| Wave | Agents | Work | Collision risk |
|---|---|---|---|
| **1** | 3 parallel | T26/T27 tags · T19b leak alarm (owns the `ci.yml` edit) · T28 profiles (appends after T19b) | none — disjoint files, no baseline, sub-second gates |
| **2** | at most 1 migration + 1 `src/`-only | T18b–T18f migration (baseline is **serial**) beside T20a (`src/…/Archive/**`, disjoint) | bounded — one baseline writer |
| **3** | serial | T29 checkpoint, T23/T24 final gate | run on a quiet machine (full suite) |

- **Parallel-safe after T10:** T11–T18c (disjoint Data.Tests folders) — but each shares the baseline,
  so they serialize through the gatekeeper.
- **Must be sequential:** T2→T5 (one seam), T6→T8 (one helper), T20a→T20b (one abstraction).
- **Needs coordination:** T18d (Server, boots `WebApplication`), T18e (E2E), T19b/T28 (`ci.yml`).

**Honest limit:** the migration is bottlenecked by the single baseline file and a ~6-minute full-suite
gate per task, on a box already carrying other agent streams (measured CPU 74%, 30 node processes, 4
worktrees). Fan-out realistically buys ~2x, mostly from Wave 1 — not 5x.
