# Spec: `demon-corpus-emit`

Module `demon-corpus-emit` in the [seedsmith map](../seedsmith-map.md) §3b (feature 2 — demons).
Wave **D1**. Depends on nothing inside seedsmith.

Ideal: [seedsmith-demons-ideal.md](../seedsmith-demons-ideal.md). Audit findings referenced as `A#`
are that document's §6.

**Status: APPROVED by the owner 2026-08-31. Authorized to build.**

---

## 1. Objective

Produce the demon seed corpus on disk, so seedsmith can read it the way it reads every other corpus.

seedsmith's `Corpus.load(root)` reads **files** — *"a file is a seed file iff its top-level JSON
object has both a non-empty `kind` and an `entries` list"* — and it must never learn to read SQLite.
But every fact about a demon lives in SQLite (`almanac_seed`, `recipes`) or in committed C#
(`DemonSpeciesCatalog.Generated.cs`). Something has to bridge that, once, at dev time.

**Done means:** `dotnet run --project tools/DemonCorpusEmit -- <server data dir>` writes
`data/seed/demons/*.json`, the output is committed, and a fresh install needs no game data to use it.

### Why this is C#, not Python

The obvious shortcut is to let the Python adapter read the SQLite files directly. **It is the wrong
call**, for a reason that outlives this module: SQL belongs inside `FusionRpg.Data`. `guard-dal.ps1`
would not catch a violation here — it scans `src/` only, and `tools/` is a documented blind spot — so
this is a case where the guard's silence is not permission. A second SQL dialect for the same tables,
in another language, outside the boundary, is exactly the drift the boundary exists to prevent.

`DemonCatalogGen` already establishes the pattern: a C# dev-time tool that reads captured data
through the DAL and emits a **committed artifact**. This is that tool, emitting JSON instead of C#.

---

## 2. Design

### 2.1 Inputs, and what each is trusted for

| Source | Contributes | Trust |
|---|---|---|
| `DemonSpeciesCatalog.Generated.cs` | `speciesId`, `name`, `side`, `gameTypeId`, `demonTypeId`, element(s), rarity, deploy mode, acquisition, variants, trait pool | **Authoritative.** Never re-derived here |
| `almanac_seed` | `display_name`, `flavor_info`, `flavor_introduce`, `sun_cost`, `cooldown_sec`, `hp`, `attack`, `armor`, `cost_status` | Factual capture, **with its confidence flags carried through unchanged** |
| `recipes` | fusion lineage (`parent_a`, `parent_b`, `result`) | **Lineage only — never taxonomy.** See §2.4 |

**The catalog is never restated, only referenced.** A demon's element and rarity live in
`DemonSpeciesCatalog`; copying them into the corpus creates two sources of truth, and the copy nobody
updates is the one that decides. The emitted entry carries `speciesId` and the fields the catalog
does **not** have.

### 2.2 Output shape

`data/seed/demons/<kind>/<partition>.json`, in seedsmith's own seed-file format:

```json
{ "kind": "demon",
  "_meta": { "partition": "zombie/legendary" },
  "entries": [
    { "id": "dollgold", "nameKey": "...", "name": "黄金套娃僵尸",
      "gameTypeId": 22, "side": "zombie",
      "flavorInfo": "...", "flavorIntroduce": null,
      "coverage": { "cost": "absent", "stats": "observed", "flavor": "thin" },
      "lineage": { "parents": [12, 19], "children": [] } } ] }
```

`kind`, `_meta.partition` and `entries` are seedsmith's contract, not this module's invention.

### 2.3 Coverage is emitted, never inferred later

`almanac_seed` already distinguishes parsed / unparsed / absent cost and null-if-unobserved stats.
That distinction **must survive into the corpus**, because everything downstream depends on it:
`motif-derive` uses it to set `basis`, `demon-metrics` uses it to exclude tautological pairs (A2), and
`lore-enrich` uses it to know what to revisit.

Measured, not assumed (B3, 2026-08-23): **89 of 677** plant entries carry a cost at all; **66/677
plants and 18/227 zombies** have a stats sample. A corpus that renders those as `0` or omits the field
is indistinguishable from one where the value is genuinely zero — and the whole feature's honesty
rests on telling those apart.

### 2.4 Lineage is emitted; family is not

`recipes` is a **crafting graph**: `A + B = C`. It says nothing about whether A and B are kin —
wall-nut and tall-nut are family, and neither is the other's fusion parent. An earlier draft of the
ideal proposed deriving `familyId` from it; the owner corrected that, and the correction is load-
bearing here: **this module emits `lineage` and never emits `families`.**

Family is classified from natural language by `family-extract` / `family-consolidate`. Emitting a
family here — even a plausible one — would produce a taxonomy that is really "things that combine",
silently, with every metric reporting success.

### 2.5 Determinism

Same inputs ⇒ byte-identical output. Entries sorted by `speciesId`, object keys emitted in a fixed
order, no timestamps in the payload. This is not tidiness: the output is committed, so a
non-deterministic emitter produces diff noise on every run and makes a real change invisible inside
it.

---

## 3. Commands

```powershell
dotnet run --project tools/DemonCorpusEmit -- <server data dir>   # emit
dotnet test tests\FusionRpg.DemonCorpusEmit.Tests
.\scripts\guard-dal.ps1                                            # SQL stays in FusionRpg.Data
```

---

## 4. Structure

```
tools/DemonCorpusEmit/                → Program.cs (arg parsing, file writing)
src/FusionRpg.Core/Demons/Generation/ → DemonCorpusBuilder.cs  (pure: rows -> entries)
src/FusionRpg.Data/Sqlite/            → existing reads only; no new SQL if avoidable
data/seed/demons/                     → emitted, committed output
tests/FusionRpg.DemonCorpusEmit.Tests/
```

**The builder is pure and lives in Core**, taking already-read rows and returning entries. Only
`Program.cs` touches the filesystem and the DAL. This is the same split `DemonSpeciesGenerator` uses
(`Generate` is pure; `EmitCSharp` renders) and the same reason: CI can test the logic without a
database, and the untestable part stays as thin as possible.

---

## 5. Numeric types and tunables

This module **copies** numbers; it never computes one. `hp`/`attack`/`armor` are `long` end to end
(magnitudes), and a null stays null rather than becoming `0`. No tunables: nothing here is a number a
balance pass would change.

---

## 6. Testing strategy

| Case | Expect |
|---|---|
| Same inputs emitted twice | **byte-identical** output files |
| A type with no `spawn_stats` sample | `hp`/`attack`/`armor` **null**, `coverage.stats = "unobserved"` — never `0` |
| A type with `cost_status = 'unparsed'` | cost null, coverage says `unparsed` — distinct from `absent` |
| A species in the catalog with no `almanac_seed` row | entry still emitted, all captured fields null, coverage fully "absent" |
| Fusion rows present | `lineage` populated; **`families` absent from every entry** |
| Emitted file | loads through seedsmith's own `Corpus.load` with no adapter changes |
| Catalog fields (element, rarity) | **not present** in the emitted entry — a test asserts their absence, so the two-sources-of-truth rule cannot erode quietly |

That last one is the test most likely to be argued with later and the one worth keeping: it is
cheaper to reject a duplicated field than to reconcile two catalogs.

---

## 7. Boundaries

- **Always:** emit coverage flags unchanged; sort deterministically; keep the builder pure; treat the
  species catalog as authoritative and reference it rather than copy it.
- **Ask first:** adding a field the catalog already has; emitting anything derived rather than
  captured; reading a capture source not listed in §2.1.
- **Never:** emit `families` (§2.4); write SQL outside `FusionRpg.Data`; render an unobserved value as
  `0`; put a timestamp in the payload.

---

## 8. Success criteria

1. `data/seed/demons/` loads through `Corpus.load` unmodified.
2. Byte-identical across runs, proven by a test, not by inspection.
3. Every `almanac_seed` confidence distinction survives into `coverage`.
4. No entry carries a field the species catalog already owns.
5. `guard-dal.ps1` green.
6. A fresh clone with no game data can read the committed corpus.

---

## 9. Open questions

**Both closed 2026-08-31.**

1. ~~Partition key.~~ **DECIDED: `side/rarity` for D1; families are added as a *second* stratum in
   D2, not as a replacement.** `side/rarity` needs no classification and is available immediately,
   which is what D1 requires. Swapping it out later would move every `Coverage/EmptyPartition`
   finding at once; adding a stratum beside it does not.
   ⚠️ **Note for D2 onward:** rarity is not stable across captures — it is recomputed by rank over a
   growing pool now that the species cap is removed (`spec-demon-themes` §2.4a shows a demon going
   Common → Epic at unchanged rank). A rarity partition is a snapshot, and coverage findings should
   be read as such.
2. ~~`lineage` transitive or direct?~~ **DECIDED: direct parents only.** It is sufficient for
   `demon-fusion`'s current use, and the transitive closure is derivable from direct edges whenever a
   lineage feature wants it — the reverse is not. Emitting the closure now would also make the
   payload grow non-linearly with an uncapped roster, for a consumer that does not exist.
