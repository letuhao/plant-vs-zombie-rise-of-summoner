# Test architecture audit — the memory-VFS serialization

**Status:** measured 2026-09-13 on a **clean** machine (48.8 GB free, no competing agent load;
an earlier attempt was invalidated by a heavy concurrent workload — see §0).

This is the sequel to [test-burden-audit.md](test-burden-audit.md), which found that Data.Tests gets
**slower** with more threads. That document proposed capping parallelism at 2 threads. **This document
shows that cap is a workaround for a real architectural defect**, names the defect from SQLite's own
source, and gives the fix that actually uses the machine.

---

## 0. Two invalid measurements first (so they are not trusted)

1. **A machine control that was optimized away.** The first control used
   `acc += ...; GC.KeepAlive(acc)` and reported "6.58× at 8 threads" from **38ms** of work that should
   have taken ~250ms — Release JIT had eliminated it. Any conclusion drawn from it was unsafe.
2. **A heavy concurrent workload.** A later run coincided with an unrelated heavy LLM process; the
   owner killed it. Every number below is from the clean machine, re-measured.

Both are recorded because they are exactly the kind of result a future session would otherwise re-trust.

---

## 1. The control, done properly — this machine scales

Dependent (un-hoistable) arithmetic, results written to a shared sink so nothing can be elided,
**fixed unit count** so speedup cannot be misread:

| threads | total | speedup vs 1t | efficiency |
|---|---|---|---|
| 1 | 557ms | 1.00× | 100% |
| 2 | 271ms | 2.06× | 103% |
| 4 | 137ms | 4.07× | 102% |
| 8 | 70ms | **7.91×** | **99%** |

**The machine gives near-perfect parallelism.** So a workload that does not scale is a property of
*that workload*, not of the box.

---

## 2. The store path does not scale — and it is not CPU, GC, or locking

Same fixed-unit structure, same machine, same moment:

| threads | store cycle | speedup | efficiency |
|---|---|---|---|
| 1 | 438ms | 1.00× | 100% |
| 2 | 391ms | 1.12× | 56% |
| 4 | 632ms | 0.69× | 17% |
| 8 | 995ms | **0.44×** | **5%** |

At 8 threads the cycle runs at **less than half** its single-threaded rate. Measured against that:

- **Not GC:** 2 MB allocated *total*, **zero** gen0/gen1/gen2 collections across the whole run.
- **Not CPU:** the control above proves the CPU is available.
- **But 87% of thread time is blocked** at 8 threads (8,199ms of thread-time produced 1,040ms of wall).

So it is a **blocking primitive**, and the question is its scope.

---

## 3. The discriminator — memory VFS vs file VFS

Four arms, identical open-and-close shape, 3,000 units each, 1 vs 8 threads:

| Arm | 1t (µs/op) | 8t (µs/op) | speedup |
|---|---|---|---|
| Unique named memory db per iteration (`ownName` — today's store shape) | 41.8 | 81.0 | **0.52×** |
| **One** shared named memory db (`oneName`) | 31.0 | 60.7 | **0.51×** |
| Unique temp **file** db per iteration (`fileDb`) | 7,779 | 2,390 | **3.25×** ✅ |
| Private `:memory:`, no shared cache (`privateMem`) | 25.6 | 81.7 | **0.31×** |

**This eliminates every per-database explanation.** `privateMem` uses its *own* database, its *own*
connection, *no* shared cache, and a *unique* name space — and it still degrades to 0.31×. Meanwhile
**file** databases scale 3.25× on the same machine.

Therefore the serialization is in the **in-memory VFS itself**, process-global, independent of which
database you use.

---

## 4. The cause, from SQLite's source (not inferred)

`src/memdb.c` — the in-memory VFS — keeps a **file-scope registry of every in-memory database in the
process**, guarded by a **static, process-global mutex**:

```c
/*
** File-scope variables for holding the memdb files that are accessible
** to multiple database connections in separate threads.
**
** Must hold SQLITE_MUTEX_STATIC_VFS1 to access any part of this object.
*/
static struct MemFS {
  int nMemStore;                  /* Number of shared MemStore objects */
  MemStore **apMemStore;          /* Array of all shared MemStore objects */
} memdb_g;
```

and on every in-memory open/close:

```c
sqlite3_mutex *pVfsMutex = sqlite3MutexAlloc(SQLITE_MUTEX_STATIC_VFS1);
sqlite3_mutex_enter(pVfsMutex);      /* ONE mutex for the WHOLE process */
```

`SQLITE_MUTEX_STATIC_VFS1` is a **static** mutex — one instance per process, taken on the memdb open
path — so **every in-memory database open in the process serializes against every other**, no matter
which database, which connection, or whether shared-cache is on. The file VFS has no such registry: it
locks per file, which is why `fileDb` scales.

That is the whole architecture defect, and it explains all four arms exactly: `ownName` 0.52×,
`oneName` 0.51×, `privateMem` 0.31× (all in-memory), `fileDb` 3.25× (per-file locks).

Sources: [`src/memdb.c`](https://sqlite.org/src/doc/tip/src/memdb.c) ·
[SQLite threading modes](https://sqlite.org/threadsafe.html) (default is serialized) ·
[shared-cache mode](https://sqlite.org/sharedcache.html).

---

## 5. The fix the measurement implies: a **process** boundary, not a thread cap

If the serialization is process-global, then the unit of isolation must be **the process**. Measured
directly: two disjoint halves of the Data workload, run as **two concurrent `dotnet test` processes**.

| Mode | Wall |
|---|---|
| Sequential (half A, then half B — one process at a time) | **47.4s** |
| **Concurrent (two processes)** | **23.9s** |

**2× faster**, both suites fully green (143 and 134 tests). The machine's cores are used, and no thread
cap is imposed anywhere.

### What this means concretely

- **A per-thread cap is the wrong lever** — it does not remove the serialization, it just stops asking
  for parallelism the runtime cannot deliver. It wastes cores by design.
- **Sharding the suite across processes removes it**, because each process gets its own `memdb_g` and
  its own `SQLITE_MUTEX_STATIC_VFS1`.
- The existing `xunit` parallelism inside each process should then be **low** (2 was the measured
  optimum), because intra-process threads still contend on that one mutex; the concurrency that pays is
  **across processes**.

**A candidate shape:** shard Data.Tests by *class* into N process groups (CI already calls `dotnet test`
per project, so this extends naturally), run them concurrently, and keep intra-process threads low.
That is a CI-invocation change plus a small sharding manifest — not a test rewrite.

---

## 6. What this does NOT establish

- **The exact shard count** for this machine is not measured (2 halves proved the principle; 4 or 8
  shards were not tried). The optimum depends on core count and on how much of each shard is
  SQLite-bound.
- **Whether other test projects are affected** the same way (Core/Server disable intra-process
  parallelism already; they are cheap regardless). Not measured.
- **Whether a non-memory substrate would scale better** — `fileDb` scaled **3.25×** on this machine,
  which raises the possibility that some tests could use real files and scale. That is the opposite of
  what the [test substrate program](../architecture/data-test-substrate-map.md) moved toward, and it
  trades disk I/O and SSD wear for parallelism. **Not recommended without measurement, and worth
  stating explicitly so nobody re-derives it as an obvious win.**

---

## 7. Why this is tracked

The previous audit concluded "cap Data.Tests at 2 threads" and recorded it as the immediate win. That
was **a symptom treatment**, and it would have quietly left the suite unable to use more than two cores
forever. The actual defect is a **process-global mutex inside SQLite's in-memory VFS**, hit once per
database open — and Data.Tests opens thousands of them, because the in-memory store design gives every
store its own database.

Any future change to how tests get their databases should be read against this: **in-memory SQLite does
not parallelize within a process, and no amount of thread tuning changes that.**
