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

---

## 6. Test profiles

Not every test belongs on every run. A test whose **subject is the disk** (WAL mode, a legacy
`rpg.sqlite` migration and its sidecar, archive slices, purge) or that takes **≥20s** earns a trait
so it can be excluded from the routine loop and run where the disk is cheap and the wait is
acceptable — CI, nightly, and the release gate. Nothing is deleted or weakened: a tagged test still
runs in `full`.

### The two categories

| Category | Meaning | Where |
|---|---|---|
| `DiskSemantics` | The file **is** the thing under test — it must write a real fixture tree. | Excluded from `default`; runs in `full`/nightly/gate. |
| `Heavy` | ≥20s or cold-process or long-run, with no disk requirement. | Excluded from `default`; runs in `full`/nightly/gate. |

A test is tagged at the **class** level when every method qualifies, at the **method** level when
only some do. When both could apply, `DiskSemantics` wins — it is the stronger statement, and either
exclusion removes the test from the default profile.

### The four profiles

| Profile | Where | Command |
|---|---|---|
| **default** | local dev, agents, `deploy-play.ps1` | `.\scripts\test-fast.ps1` → `dotnet test <proj> --filter "Category!=DiskSemantics&Category!=Heavy"` |
| **full** | CI pull-request | `dotnet test <proj> -c Release` (no filter) |
| **gate** | `release.yml` tags | `dotnet test <proj>` (no filter), required |
| **nightly** | `.github/workflows/nightly.yml` | `dotnet test <proj>` (no filter) |

**The default lives in exactly one place.** `scripts/test-fast.ps1` owns the filter, so a developer
and an agent cannot drift. `deploy-play.ps1` calls it. CI keeps calling `dotnet test` directly with
**no** filter, so CI is `full` by construction and can never accidentally inherit the dev default.
A negative filter includes uncategorized tests, so only the *excluded* tests need a trait.

### The guards run on `full` only

**`guard-test-substrate.ps1` (the static gate) and `test-substrate-leak-alarm.ps1` (the runtime
alarm) run against the `full` profile, never the default.** The default profile *intentionally*
writes the file-bound directories — those are the `DiskSemantics` tests — so a disk-leak alarm run
around it would either false-positive every time or need an ever-growing allowlist. The static gate
reads source and is profile-independent, but it is paired with the alarm for the same "one run, both
halves" reason. **Do not "optimize" either guard onto the fast profile.**

`full` is the only profile that catches a disk regression — hence the nightly workflow: it reruns
everything unfiltered within a day, instead of waiting for a release tag.

---

## 7. The baseline register — every remaining line and why

`scripts/test-substrate-baseline.txt` is the ratchet. It holds `path : code` lines with **no inline
comments** (the parser splits on `:` and would read a comment as part of the code), so the *reasons*
live here. After T18b–T18e (2026-09-12) every remaining line falls into four honest groups. The groups
are described by **membership, not by a count** — a pinned total is a population reading that goes
stale the moment a file is fixed or a test project changes, which is the anti-pattern
[validation-ssot.md](../architecture/validation-ssot.md) bans.

### Group 1 — file-bound by subject (`tests/FusionRpg.Data.Tests`)

| File | Why it keeps real files |
|---|---|
| `RpgStoreDalSmokeTests` | asserts `PRAGMA journal_mode == 'wal'`; memory has no WAL |
| `RpgStoreSmokeTests` | asserts `File.Exists(HotPath/MediaPath)` |
| `LegacyMonoMigratorTests` | migrates a real legacy `rpg.sqlite` and asserts a disk sidecar |
| `ColdArchiveCompactionTests` | archive `*.sqlite` slices written/listed/verified on disk |
| `StoragePurgeTests` | purge deletes real archive files |
| `CreatureSpeciesImportCliTests` | launches a cold subprocess that imports the real committed tree (owned by the `cold-process-test-build` session) |

These are **excluded from the default profile** via `DiskSemantics` (T26) and run at `full`/nightly/gate.
They must **not** be converted to memory — that would delete real coverage (`substrate-standard` R2).

After T18f (2026-09-12) the four store classes among them (`RpgStoreDalSmokeTests`,
`ColdArchiveCompactionTests`, `StoragePurgeTests`, `RpgStoreSmokeTests`) route their store and cleanup
through the leak-proof **file** helper or `ClearAllPools` + a throwing delete, so their cleanup can no
longer swallow. Three then dropped off the static baseline entirely (`RpgStoreDalSmokeTests`,
`ColdArchiveCompactionTests`, `StoragePurgeTests`): their disposal is the helper's, so the scanner no
longer finds `new RpgStore(` beside a temp path. `LegacyMonoMigratorTests` and `RpgStoreSmokeTests`
keep a baseline line **only for the `temp-store` pattern** — both legitimately boot a store over a
**hand-built on-disk file** (a legacy `rpg.sqlite`; a hot-only folder whose media is recreated), which
is exactly the disk-is-the-subject case R2 permits. `CreatureSpeciesImportCliTests` remains baselined
end to end because it is another session's cold-process test, not migrated here.

**Being on the static baseline is not a defect for a file-bound class** — the baseline is what stops a
*new* file-backed store appearing anywhere else. After T18f the only `swallowed-delete` lines left
inside this program's four Store test projects are the two files the program deliberately does **not**
own: `CreatureSpeciesImportCliTests` (another session's cold-process test) and
`BaseTypeSocketMaxCorpusTests` (class C, a fixture-leak fix, not a store migration). Every other
`swallowed-delete` line is in the out-of-boundary Guard/Launcher/AtomImporter projects (Group 4).

### Group 2 — file-bound host (`tests/FusionRpg.E2E.Tests`)

`RpgApiFactory.cs : temp-store` — a `WebApplicationFactory<Program>` boots the **real server**, which
reads `FUSIONRPG_DATA` from disk. Its dispose is leak-proof (clears pools, does not swallow), but it
cannot be memory without changing production `Program.cs`, which this program does not touch.

### Group 3 — class-C, not a store test (`tests/FusionRpg.Server.Tests`)

`BaseTypeSocketMaxCorpusTests.cs : swallowed-delete` — no `new RpgStore(`; it writes a real nested
JSON fixture tree for `BaseTypeSocketMaxCorpus.Load(root)`. A fixture-leak fix, not a store migration.

### Group 4 — outside this program's boundary

`FusionRpg.Guard.Tests`, `FusionRpg.Launcher.Tests`, `FusionRpg.AtomImporter.Tests`. These are **not
store tests** and not in the four test projects this program owns; their `swallowed-delete` lines are
baselined and ratcheted by the gate but not migrated here. A future program can take them.

**The ratchet only shrinks.** Fixing any line above requires removing it; a stale line fails the gate.
Adding a line is a review event requiring a stated reason — and if the file is genuinely file-bound,
it belongs in a group above, not silently added.
