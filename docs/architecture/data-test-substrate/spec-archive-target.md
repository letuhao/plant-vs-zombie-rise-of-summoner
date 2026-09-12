# Spec: `archive-target`

**Module id:** `archive-target` · **Program:** [data-test-substrate](../data-test-substrate-map.md) · **Build order:** 5 of 6 · **Optional** (cuttable)
**Depends on:** [`memory-storage-plan`](spec-memory-storage-plan.md).
**Model calls:** none.

## Objective

Let an archive **target** be a file *or* a named memory DB, so the two archive test classes
(`ColdArchiveCompactionTests`, `StoragePurgeTests`) can also leave the SSD — the last slice of
"completely to memory".

The spike proved an archive DB *can* be memory (`ATTACH 'file:…mode=memory&cache=shared' AS hot`
works, memory→memory and file-hot→memory). But the compaction code addresses the archive **straight
through the filesystem**, not through an abstraction:

- Four writers each do `Directory.CreateDirectory(ArchiveDir)` + `Path.Combine(ArchiveDir, fileName)`
  (`RpgStore.Compaction.cs:140-143`, `:276-279`, `:484-487`, `:623-626`).
- They hand the absolute path to `WriteCaptureArchiveFile(absPath, …)` (`:348`) /
  `WriteSegmentArchiveFile(absPath, …)` (`:683`), which `SqliteConnectionFactory.Open(absPath)` and
  `ATTACH DATABASE $p AS hot` using `_hotPath`.
- Reads/verify/delete go through `ResolveArchiveAbsPath` → `Path.GetFullPath(Path.Combine(_dataDir,
  uri…))` (`:711`) and `File.Delete` (`:145,281,489,628`).
- Purge does `TryResolveSafeArchivePath` (`RpgStore.Storage.cs:258`) + `TryDeleteArchiveFile`
  (`:280`) with a **path-escape guard** (`:264-271`).

**Success looks like:** the archive target is an interface; the file implementation is byte-identical
to today's behavior; a memory implementation stores slices in named memory DBs; the two archive test
classes run on it.

## Assumptions I'm making (correct me now)

1. **The file plan is the production path and must not change** — `RpgStore(dataDir)` keeps writing
   `archive/*.sqlite` exactly as today.
2. The abstraction is internal to `FusionRpg.Data` (`guard-dal.ps1` stays green; no SQL leaves Data).
3. **This module is cuttable.** If it grows past the four writers + purge, it defers to its own
   program and the archive tests stay file-backed via the leak-proof helper. The bulk win is already
   delivered by Phases 1–4.
4. The path-escape guard (`TryResolveSafeArchivePath`) is a **file** concern; on a memory target the
   equivalent guard is "the name is one this target created", not a `StartsWith` on a filesystem root.

## Design

### 1. The abstraction

```csharp
internal interface IArchiveTarget
{
    bool IsMemory { get; }
    IArchiveSlice Create(string kind, string fileName);   // returns a handle to write
    bool Exists(string uri);
    IEnumerable<string> List(string kind);
    bool TryDelete(string uri);                            // path-escape-safe
    // open a slice for verify/read
    SqliteConnection Open(string uri, bool readOnly);
}
```

- **File target:** wraps today's `ArchiveDir` + `Path.Combine` + `Path.GetFullPath` guard +
  `File.Delete`. **Behavior identical** — this is the production path.
- **Memory target:** each slice is a named shared-memory DB (`rpg-archive-{kind}-{name}-{guid}`); the
  store holds a keeper for each live slice; `Open` returns a connection to it; verify `ATTACH`es
  `_hotPath` (which is itself a memory URI — proven to work).

### 2. Threading it through

Replace the four writers' direct filesystem calls with `_archiveTarget.Create(kind, fileName)` +
`IArchiveSlice.WriteFrom(hotSql)`; replace `ResolveArchiveAbsPath`/`File.Exists`/`File.Delete` with
the target's `Open`/`Exists`/`TryDelete`; replace purge's path-guard with the target's
`TryDelete` (whose contract is "only deletes a uri this target owns").

**The `ATTACH` source stays `_hotPath`** — the writers already attach the *hot* DB into the archive
file; with the hot DB in memory and the archive in memory, the `ATTACH` is memory→memory, proven.

### 3. Why an interface and not a bool

The map's §3 says archive needs a real abstraction, not a branch, because there are **four** writers +
reads + purge + a path-escape guard. A bool would scatter the decision across six sites; one target
object keeps the decision in one place and makes the file path reviewable as "unchanged".

### 4. What this deliberately does not do

- Does not change the file plan's bytes.
- Does not make the legacy-migration or the two smoke tests memory (they stay file permanently —
  they assert file/WAL behavior).
- Does not add cross-run archive querying (the deferred `IColdPathQuery`, unrelated).

## Commands

```powershell
dotnet test tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj --filter "FullyQualifiedName~ColdArchive|FullyQualifiedName~StoragePurge"
.\scripts\guard-dal.ps1
.\scripts\guard-test-substrate.ps1
```

## Project structure

```
src/FusionRpg.Data/Sqlite/
  RpgStore.Compaction.cs   → four writers + verify/delete through the target
  RpgStore.Storage.cs      → purge through the target
  Archive/IArchiveTarget.cs, FileArchiveTarget.cs, MemoryArchiveTarget.cs  (new, internal)
tests/FusionRpg.Data.Tests/
  ColdArchiveCompactionTests.cs → CreateFileBacked() then memory
  StoragePurgeTests.cs          → CreateFileBacked() then memory
```

## Code style

```csharp
// One decision point. The file target is today's code, moved — not rewritten.
var slice = _archiveTarget.Create(kind: "events", fileName);
slice.WriteFromHot(_hotPath, whereClause);
```

## Testing strategy

| Concern | Test |
|---|---|
| File target byte-identical | existing archive tests green **unmodified** |
| Memory target stores + reads a slice | create, verify rows, list, delete |
| Memory `ATTACH` of the hot memory DB works | archive a closed run, read it back |
| Purge deletes a memory slice, refuses a foreign uri | path-escape-equivalent guard |
| File plan production behavior unchanged | `RpgStoreSmokeTests`/`RpgStoreDalSmokeTests` green |

**Assert the contract, never a count** (`validation-ssot.md`): a slice's rows, not "N slices".

## Boundaries

- **Always:** file target behavior identical; the target owns path/name safety; keep it internal to Data.
- **Ask first:** changing the archive file naming; adding a third target kind.
- **Never:** let the file plan change; skip the escape guard on the file target; put archive SQL
  outside `FusionRpg.Data`.

## Success criteria

1. The four writers + purge go through `IArchiveTarget`; the file target's behavior is unchanged.
2. A memory target stores, verifies, lists, and deletes slices; memory→memory `ATTACH` works.
3. `ColdArchiveCompactionTests` and `StoragePurgeTests` pass on a memory target.
4. The file-bound set drops from 5 to 3 (legacy + the two WAL/file smoke tests).
5. `guard-dal.ps1` + `guard-test-substrate.ps1` green.

## Numeric types / Tunables / ActorHub gate

Not applicable — persistence plumbing. No magnitude, no tuning key, no stat surface.

## Open questions

None. The shape is fixed by the map §3 and the spike's `ATTACH` proof; the module is explicitly
cuttable if the abstraction grows past the four writers + purge.

## Gate checklist (DESIGN-GATE §5)

- [x] Subsystem: Data/SQL/schema → docs read this session.
- [x] Boundary recorded; drift owner-accepted.
- [x] `decisions.md` — the storage-plan row (T1) covers the store; the archive target is internal, no
      new lock needed.
- [x] Claims cite file:line (`Compaction.cs:140-143,276-279,348,484-487,623-626,683,711`;
      `Storage.cs:258,264-271,280`).
- [x] Verified against code (read this session).
- [x] Constraints tested: memory `ATTACH` works both directions (spike).
- [x] DAL boundary preserved (internal to Data).
- [x] No population-count assertion.
- [x] ActorHub: N/A.
- [x] Cuttable, so it cannot block the bulk win.
