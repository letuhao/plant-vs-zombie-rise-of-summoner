# Spec: `corpus-dump`

**Module id:** `corpus-dump` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 1 of 16
**Model calls:** none.

## Objective

Export **every** captured species from SQLite into a committed JSON tree that seedsmith reads as the
development-phase source of truth, with a capture stamp and a content hash so a downstream run can
prove which snapshot it was derived from.

Owner, Q10: *"seedsmith is just a dev tool, it not work in the game runtime. so we need to dump data
game data and use it as SOT in our development phase, not player who play the game."*

### The defect this module exists to fix

`tools/DemonCorpusEmit/Program.cs:45-52`:

```csharp
var species = DemonSpeciesCatalog.All;          // the 84 the C# generator emitted
foreach (var s in species)
{
    var a = store.GetAlmanacSeed(s.Side, s.GameTypeId);   // ask the DB about *those*
```

**The emitter walks the generator's own output and asks the database to confirm it.** Every species the
C# generator never picked is invisible to seedsmith — not missing from the database, just never asked
for. `corpus-dump` walks `RpgStore.ListAlmanacSeed()` (`RpgStore.AlmanacSeed.cs:296`) instead, which is
the full table.

**This module does not modify `DemonCorpusEmit`.** That tool keeps serving the existing 84-species
corpus until `anchor-emit` replaces it; two emitters coexisting is cheaper than one emitter with a
mode flag, and the old one is deleted in `anchor-emit`, not here.

## Design

### 1. What is exported

| Source | DAL call | Rows |
|---|---|---|
| `almanac_seed` (+ enrichment) | `ListAlmanacSeed()` -> `AlmanacSeedDto` | all |
| earliest `spawn_stats` baseline per `(side, type_id)` | the existing two-query loader behind `RebuildAlmanacSeed` | all observed |
| recipes | `ListRecipes()` | all |

`AlmanacSeedDto` already carries everything the anchor needs as input: `Side`, `TypeId`, `TypeName`,
`DisplayName`, `FlavorInfo`, `FlavorIntroduce`, `SunCost`, `CooldownSec`, `CostStatus`, `Hp`, `Attack`,
`Armor`, `ArmorMax`, `StatsObserved`, `ContractVersion`, `RebuiltUtc`, `Enrichment`
(`RpgStore.AlmanacSeed.cs:9-27`). **No new column is needed anywhere.**

### 2. The capture stamp and the content hash — Q13

Two different questions, so two different fields:

| Field | Answers | Computed from |
|---|---|---|
| `capturedUtc` | *when did the game last write this?* | `max(RebuiltUtc)` over the exported rows — the store's own value, never `DateTime.UtcNow` |
| `contentHash` | *is what I am reading the same bytes as what produced that anchor?* | SHA-256 over the canonically-serialised payload, excluding the envelope itself |
| `dumpFormatVersion` | *can this reader read this file?* | a constant, bumped by hand when the shape changes |

**`capturedUtc` must not be wall-clock time.** A dump re-exported from an unchanged database must be
byte-identical, or every downstream provenance record churns on every run and "is this stale?" stops
being answerable. Re-running the tool against an unchanged DB produces an identical file, hash
included — this is a test, not an aspiration.

### 3. Canonical serialisation

Ordering and formatting are load-bearing because the hash is:

- keys sorted ordinal; rows sorted by `(side, typeId)` ordinal
- two-space indent, `\n` line endings, trailing newline
- `JavaScriptEncoder` configured for CJK ranges so Chinese names are not `\uXXXX`-escaped — the same
  choice `DemonCorpusEmit/Program.cs:1-5` already makes with `UnicodeRanges`
- `null` written explicitly, never omitted — an absent key and a null key must not hash differently

### 4. Layout

```text
data/seed/demons/_dump/
  _manifest.json          envelope: dumpFormatVersion, capturedUtc, contentHash, counts per side
  almanac/plant.json      one array, sorted by typeId
  almanac/zombie.json
  spawn-baseline.json     earliest observed sample per (side, typeId)
  recipes.json
```

`_manifest.json`'s hash covers the other four files' bytes, so a partial or interrupted write cannot
present as a valid dump.

## Commands

```powershell
dotnet run --project tools/DemonCorpusDump -- <server data dir> [output root]
dotnet run --project tools/DemonCorpusDump -- <server data dir> --check   # exit 1 if the tree would change
dotnet test tests/FusionRpg.Core.Tests --filter CorpusDump
```

`--check` is what CI runs: it regenerates into a temp directory and diffs, so a stale committed dump
fails the build instead of silently disagreeing with the database.

## Project structure

```text
tools/DemonCorpusDump/Program.cs        arguments, report, exit codes only
tools/DemonCorpusDump/DumpWriter.cs     canonical serialisation + hash
src/FusionRpg.Core/Demons/Generation/DumpEnvelope.cs   the record shapes, so tests can hold them
tests/FusionRpg.Core.Tests/Demons/CorpusDumpTests.cs
data/seed/demons/_dump/**               committed output
```

**Every database read goes through `RpgStore`.** `scripts/guard-dal.ps1` scans `src/` only, so a raw
`SqliteCommand` in `tools/` would pass the guard — and the guard's silence is not permission. No SQL
in this module.

## Code style

```csharp
// The whole table, not the catalog's opinion of it. DemonCorpusEmit walked
// DemonSpeciesCatalog.All and could therefore never see a species the C# generator
// had not already picked - the defect this tool exists to fix.
var rows = store.ListAlmanacSeed();
```

Match `DemonCorpusEmit/Program.cs`: a comment says *why*, arguments and reporting stay in `Program.cs`,
decisions live next door where a test can reach them.

## Testing strategy

| Test | Asserts |
|---|---|
| `Dump_is_byte_identical_on_rerun` | same DB in, same bytes out, hash included |
| `Dump_covers_every_almanac_row` | exported count equals `ListAlmanacSeed().Count`, **not** `DemonSpeciesCatalog.All.Count` — the regression test for the circularity defect |
| `Manifest_hash_changes_when_any_payload_byte_changes` | flip one field, hash moves |
| `Cjk_names_are_not_escaped` | a Chinese `DisplayName` round-trips as UTF-8 |
| `Null_and_absent_hash_identically_is_false` | an omitted key is rejected at write time |
| `Check_mode_exits_1_when_committed_tree_is_stale` | the CI gate actually gates |

Fixtures are a small in-memory store, not the 520MB live database.

## Boundaries

**Always:** read through `RpgStore`; sort before writing; commit the output; treat `capturedUtc` as
data from the store.

**Ask first:** adding a table to the dump (it widens the contract every downstream module reads);
changing `dumpFormatVersion`.

**Never:** write SQL in `tools/`; use `DateTime.UtcNow` in the envelope; filter rows by anything —
completeness is the whole point; touch the live game database.

## Success criteria

- [ ] The dump contains every `almanac_seed` row, verified by count against the DAL, not by eye.
- [ ] Two consecutive runs against an unchanged database produce identical bytes.
- [ ] `--check` exits 1 on a stale tree and 0 on a fresh one.
- [ ] No SQL appears anywhere under `tools/DemonCorpusDump/`.
- [ ] `_manifest.json` names a hash that a downstream provenance record can quote verbatim.
