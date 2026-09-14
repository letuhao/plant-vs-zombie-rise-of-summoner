# Test burden audit

**Status:** measured 2026-09-13 on the main worktree, Windows, **32 logical cores**. Numbers are from a
machine already at **~82% CPU** with four concurrent agent worktrees; treat them as *relative*, not as
absolute CI timings.

This document exists so the same investigation is not repeated from scratch. It records what was
measured, **what was ruled out and how**, and the one methodological trap that makes most of the
"slow test" data in this repo unreadable.

---

## 0. Read this first — the trap

**Do not read per-test durations out of a full-suite TRX and treat them as cost.** xUnit reports
wall-clock time, and under `--blame-hang` + high parallelism that number absorbs contention. In
Data.Tests the top 25 classes showed **53×–230× inflation** (median ~160×): e.g.
`DomainProgressStoreTests` reported **222.2s** in-suite and **1.0s** when run alone.

**Correct method:** get the total from the suite run, then re-measure any suspect class **in
isolation** before concluding anything. A class whose isolated wall is a few seconds is not slow — the
suite is.

---

## 1. Suite totals (all 12 CI projects)

| Project | Tests | Wall | Summed | Max test | Mean | ≥10s |
|---|---|---|---|---|---|---|
| **Data.Tests** | 1,288 | **351s** | **10,404s** | 47.0s | 8.1s | **370** |
| Guard.Tests | 248 | 53s | 146s | 17.2s | 0.59s | 2 |
| SquadHarness.Tests | 194 | 20s | 80s | 11.6s | 0.41s | 1 |
| Core.Tests | 13,369 | 89s | 61s | 4.4s | **0.0046s** | 0 |
| Server.Tests | 404 | 60s | 32s | 6.2s | 0.08s | 0 |
| E2E.Tests | 221 | 32s | 16s | 4.4s | 0.07s | 0 |
| AtomImporter.Tests | 33 | 17s | 12s | 3.4s | 0.38s | 0 |
| Launcher.Tests | 162 | 7s | 7s | 1.3s | 0.04s | 0 |
| ItemSeedValidator.Tests | 81 | 5s | 1s | 0.1s | 0.013s | 0 |
| CheatCore / TreeBinder / ElementEnumGen | 95 | 8s | 0.2s | ~0 | ~0.003s | 0 |
| **TOTAL** | **16,095** | **~11 min** | 10,759s | — | — | — |

**Data.Tests is 96.7% of all summed test time.** Everything else is already cheap. The `summed` column
is inflated for the same reason §0 describes — it is listed only to show where the concentration is.

---

## 2. Data.Tests is 4.2× slower in parallel than sequentially

Measured twice, on the same machine state:

| `xUnit.MaxParallelThreads` | Wall |
|---|---|
| 1 | 112.6s |
| **2** | **87.0s ← optimum** |
| 4 | 126.4s |
| 8 | 222.1s |
| 16 | 303.9s |
| 32 (default on this box) | **367.1s** |

Monotonic. Per-store cycle cost went **60ms at 1 thread → 272ms at 32**.

### The control that makes this meaningful

The same machine, same moment: a **pure-CPU** workload scaled **6.58× at 8 threads (82% efficiency)**,
while a **store cycle** degraded to **0.50× (6% efficiency)**.

| Workload | 1t | 2t | 4t | 8t |
|---|---|---|---|---|
| Pure CPU (allocation-free arithmetic) | 249ms | 143ms (1.74×) | 73ms (3.41×) | **38ms (6.58×)** |
| Store cycle (`RpgStore.InMemory()` + `Init()`) | 483ms | 409ms (1.18×) | 652ms (0.74×) | **971ms (0.50×)** |

**So this is not "the machine is busy".** The machine can scale; the store path specifically cannot.
There is a serialization point inside it.

---

## 3. Where the time is

One store cycle ≈ **60ms**, and `Init()` is essentially all of it:

| Phase | Cost |
|---|---|
| `RpgStore.InMemory()` construction (2 keepers) | 0.07ms |
| `Init()` | **~56ms** |
| — 40 `Ensure*SchemaUnlocked` methods (sum) | 8.66ms |
| — `EnsureColumn` ×41 | 1.24ms |
| **`EnsureHotSchema` incl. its inline body** | **58.44ms** |
| re-`Init()` on the same store | **2.2ms** |

Because re-Init is 2.2ms, the expense is **first-time population work**, and it sits in
`EnsureHotSchema`'s own 37KB body — not in its sub-method calls.

### The unresolved gap (stated, not explained away)

`EnsureHotSchema`'s extracted SQL executes in **~2.7ms** (three blocks: 0.55 + 0.20 + 1.85ms), and its
40 sub-methods total 8.66ms — yet the method measures **58ms**. **~46ms is currently unexplained.**

Not yet checked, in priority order:
1. The 5 single-statement `CREATE INDEX`/`CREATE UNIQUE INDEX` calls the extractor did not time.
2. Whether the store's own `Exec` differs from raw execution in connection/transaction state.
3. Whether `lock (_gate)` interacts with the schema path.

**Do not name a cause for this gap without a probe.** That is the whole lesson of the ruled-out list.

---

## 4. Ruled out — each by measurement, with the number

| Hypothesis | Verdict | Evidence |
|---|---|---|
| `SqliteConnection.ClearAllPools()` global lock convoy | **No** | With vs without, per store: 4t 85 vs 92ms, 8t 160 vs 151ms, 32t 272 vs 276ms. Identical. |
| Shared-cache mode (`Cache=Shared`, the factory's shape) | **No (at low scale)** | Private vs shared at 2 tables: 2.52× vs 2.00× at 8t — sub-ms, just scheduling. A realistic-scale re-test was attempted but the probe failed to compile; **not fully cleared at 70-statement scale.** |
| GC pressure | **No** | 260 KB allocated per store; 0 gen0 collections at 1t, 1 at 8t. `IsServerGC=False`. |
| One giant multi-statement `Exec` shape | **No** | 105 statements in one call = 2.88ms; one-per-statement = 3.31ms. Scaling is linear: 0.29ms@5 stmts → 7.17ms@140. |
| Raw DDL throughput | **No** | 70 `CREATE TABLE` = 1.18ms. |
| Connection open/close cost | **No** | 0.026ms normal open; 0.005ms for the expected read-only throw. |
| Machine contention / "busy box" | **No** | The pure-CPU control scaled 6.58× on the same box (§2). |
| Connection pooling vs not | **Partial** | A 2-statement no-pool cycle is 0.112ms and the real store cycle 52.4ms — a 470× gap — so pooling is not the bulk cost; but pooling was not isolated at realistic schema scale. |

---

## 5. Two real defects found

### 5.1 `EnsureColumn` swallows an exception 41× per store (9.7× wasteful)

`src/FusionRpg.Data/Sqlite/RpgStore.cs`:

```csharp
void EnsureColumn(SqliteConnection db, string table, string column, string def)
{
    try { Exec(db, $"ALTER TABLE {table} ADD COLUMN {column} {def};"); }
    catch { /* already exists */ }
}
```

On any store where the column **already exists** — every boot after the first, and every fresh
in-memory test store — this **throws and swallows a real `SqliteException`**, 41 times per store.

Measured: **1.24ms** (alter-in-catch) vs **0.13ms** (a single `PRAGMA table_info` check) = **9.7×**.

Two problems, not one: the cost, and it is a `catch { }` swallow — the exact class
[`testing-standard.md`](testing-standard.md) R3 bans, sitting in production code rather than a test.

### 5.2 The stale-build trap (cost me a wrong diagnosis)

`dotnet test --no-build` on a test project **after editing `src`** reuses a test assembly that predates
the change, because the test project references the edited project. It made a correct fix look broken.
**Rebuild the referencing test project after any `src` change**, or omit `--no-build`.

---

## 6. Recommendations, by evidence

| Action | Basis | Confidence |
|---|---|---|
| **⚠ SUPERSEDED by [test-architecture-audit.md](test-architecture-audit.md).** Capping at `MaxParallelThreads=2` (87s vs 367s at 32) treats the symptom: the cause is a **process-global mutex in SQLite's in-memory VFS**, so the correct lever is a **process** boundary — 2 concurrent processes measured **23.9s vs 47.4s sequential (2×)**, cores used. The cap would permanently limit the suite to ~2 cores | **High** — the defect is proven from SQLite's `memdb.c` source |
| **Fix `EnsureColumn`** to a `PRAGMA table_info` check | 9.7× on the call; removes a banned swallow | **High** — measured both ways |
| **Re-measure on an idle machine** before further tuning | The first burden numbers were from an 82%-CPU box; the architecture audit re-ran clean and its conclusions hold | **Done** — see the architecture audit §0 |
| **Leave Core/Server serialization alone** | `[assembly: CollectionBehavior(DisableTestParallelization = true)]` at `Core.Tests/AssemblyInfo.cs:6` and `Server.Tests/AssemblyParallelism.cs:38` is deliberate (static-hub safety) and already efficient: Core 13,369 tests mean **4.6ms** | **High** |
| **Investigate the 46ms gap** | Partly explained by the architecture audit (the memdb mutex is on the open path, and `Init` opens 3 connections); the remaining per-store breakdown is still unproven | — |

---

## 7. Why this is tracked

The suite is 16,095 tests and ~11 minutes, and **96.7% of it is one project** whose per-test numbers
mislead at first glance. Without this document, the next session re-derives the same wrong conclusion
("these 370 tests need optimization") and spends the same effort. The two load-bearing facts to carry
forward:

1. **Per-test TRX durations are not cost** on this repo — always re-measure in isolation.
2. **Data.Tests wants ~2 threads, not 32** — and that is a setting, not a refactor.
