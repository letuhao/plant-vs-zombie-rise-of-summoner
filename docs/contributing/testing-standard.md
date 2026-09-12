# Test substrate standard

**Status: binding for every automated session.** A test must not write its data to the developer's
SSD, and a cleanup that fails must never be silent. This is the policy the
[`guard-test-substrate.ps1`](../../scripts/guard-test-substrate.ps1) gate enforces.

**Why this exists.** SQLite connection pooling holds the database file handle past `Dispose`, so a
`Directory.Delete` on a test's temp dir throws `IOException` — and 153 of 154 delete sites swallowed
it with an empty `catch { }`. One full local run leaked **30,978 directories / 65.5 GB**. The defect
was not the test assertions; it was the substrate and the silent catch. Full reasoning:
[data-test-substrate-ideal.md](../architecture/data-test-substrate-ideal.md).

---

## 1. The rules

**R1 — A store test runs in memory by default.** A test for the SQL layer (schema, constraints,
upserts, watermarks, joins, migrations) constructs its store through the shared test helper in
memory. It does not create a temp directory and does not build an `RpgStore` from a path.

**R2 — Disk only when the disk is the thing under test.** A test may use a real file **only** when
what it asserts is file behaviour: a legacy `rpg.sqlite` migration and its backup sidecar, `PRAGMA
journal_mode` returning `'wal'`, `File.Exists` on hot/media, archive slices on disk, storage purge.
Those are named in the gate's baseline and must say in the test why a file is required.

**R3 — A failed cleanup is a failure.** Never `catch { }` a temp delete — not `catch { /* temp */ }`,
not `catch (Exception) { }`. If the delete fails, the test fails and says why. An empty catch around
a delete is the specific pattern that hid 65.5 GB; it is banned outright.

**R4 — The baseline only shrinks.** The gate carries a baseline of the files that predate this
standard. **Removing a line is required when the file is fixed; adding one is a review event** that
needs a reason and the owner's sign-off. A stale baseline line (fixed but not removed) fails the
gate, so the ratchet cannot quietly grow back.

**R5 — Assert the relationship, never a population count.** The leak guard and the "0 files" check
assert a delta (`before == after`) and the closed-enum/structural properties — never a fixed count of
files or tests, per [validation-ssot.md](../architecture/validation-ssot.md).

---

## 2. What the gate bans (mechanically)

| Code | Bans | Rationale |
|---|---|---|
| `swallowed-delete` | `Directory.Delete(...)` inside an empty or comment-only `catch` block, anywhere in `tests/**` | R3. This is the exact shape that hid the leak. |
| `temp-store` | A `tests/**` file that both constructs `new RpgStore(` and references `Path.GetTempPath` | R1/R2. After migration a store test is memory or an allowlisted file-semantics case — never an ad-hoc temp path. |

A file is exempt only by an explicit baseline entry. There is no silent allowlist.

---

## 3. How to satisfy the gate

```csharp
// Before — the defect
_dir = Path.Combine(Path.GetTempPath(), "fusionrpg-actions-" + Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(_dir);
_store = new RpgStore(_dir);
_store.Init();
public void Dispose() { try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ } }

// After — in memory
using var store = DataTestStore.Create();   // in-memory, keeper held, no temp dir

// After — file semantics only, leak-proof
using var store = DataTestStore.CreateFileBacked();   // clears pools before delete; delete failure throws
```

---

## 4. How this is enforced

- `scripts/guard-test-substrate.ps1` — the gate. Run by `deploy-play.ps1`, CI, and
  `tests/FusionRpg.Guard.Tests`.
- `scripts/test-substrate-baseline.txt` — the ratchet. One line per `path : code`.
- The instruction files (`AGENTS.md`, `CLAUDE.md`) point here rather than restating the rule.

Update the baseline only with `guard-test-substrate.ps1 -UpdateBaseline`, review the diff, and state
why any line was **added**.

---

## 5. Applying it to the rest of the suite

This standard covers `tests/**`. The store migration (in-memory substrate) is the
[data-test-substrate](../architecture/data-test-substrate-map.md) program; this gate lands first so
no new violation can be introduced while that migration proceeds.
