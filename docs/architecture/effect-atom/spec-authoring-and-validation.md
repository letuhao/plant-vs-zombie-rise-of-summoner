# Spec: authoring-and-validation (E14a + E14b)

Modules **E14a** and **E14b** in the [atom effect map](../effect-atom-map.md).

**The module splits.** E11 must import seed rows, and the importer was three positions later — so Checkpoint D was unreachable as sequenced.

| | Owns | Depends on | Position |
|---|---|---|---|
| **E14a** | seed/migration file format, `tools/AtomImporter`, schema-validation wiring, all-or-nothing import | E5, E8 | **before E11** |
| **E14b** | budget validation, power drift, content lint, the one-row claim test | E11, E9 | after E9 |

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Make authoring a new effect actually cost **one row**, and make every guarantee this program claimed into a test that fails. Without this module, "data-driven" is a schema with no way to put data in it and no way to know the data is sane.

## Design (locked on approval)

### The authoring path — files, not an editor

Seed and migration files under `data/seed/atoms/` and `data/seed/containers/`, loaded by the same validator the runtime uses. **No runtime loader, no external content, no editor in wave 1** — an editor arrives only if a spec asks for one.

Files are the right first surface because they diff, review, and version like code, while the *content* still lives in rows at runtime. A designer edits a file, the importer validates and upserts, the content hash moves, and the change is visible in review.

**Built 2026-08-22.** Four folders, not two: `atoms/`, `containers/`, `curves/`, `rarity/`. `effect_curve` and `rarity` are both hashed content tables with an upsert of their own, and leaving either unauthorable would make a covered table reachable only by hand-editing the database — the same shape as E1 refusing `capPerMatch` for a counter E15 had already shipped. An atom that scales through a curve is a validation failure until that curve exists, so the two must land in **one** import, not two.

The importer sweeps exactly those four folders. `data/seed/` also holds the item seed corpus, which is a different format read by `tools/ItemSeedValidator`; a recursive sweep of the whole seed root would refuse all 125 of its files and call the import broken.

Envelope: `{ "schemaVersion": 1, "kind": "atom" | "container" | "curve" | "rarity", "entries": [ … ] }`. The kind comes from the **file**, not the folder — encoding the layout twice means two places to be wrong. A `schemaVersion` this importer does not know is refused rather than read optimistically, or a format that grew a field would import with that field silently dropped.

**JSON columns are stored canonically.** `when`, `params`, `tags`, `power` and `powerOverride` are authored as nested objects and written through `ContentHash.CanonicalJson`, never as the author typed them. Storing raw text would make re-indenting a file differ from the stored column — which bumps `revision`, a hashed column, and so moves the content hash for an edit that changed no content. Format: [data/seed/README.md](../../../data/seed/README.md).

### The four validations

**1. Schema validation (E14a)** — every row through E1/E2/E3/E4/E5. **Import is all-or-nothing**: one bad row and nothing is imported, because a partial import produces a content hash for a state nobody authored. Per-row rejection at *load* (E4/E5) is a different phase — defence in depth against a database edited outside the importer.

*Built:* validate-first and one transaction are **two** guarantees, and the module needs both. Validating everything before the first write stops a known-bad file from landing half a catalog; the single transaction stops a crash, a locked database or a constraint nobody predicted from doing the same thing. `RpgStore.ImportContent` therefore reads the stored catalog, validates the whole batch against *stored ∪ incoming*, and only then opens one transaction for atoms, containers, curves and bands together — so a container may reference an atom authored in the same import, which is how a new item and its affixes normally arrive.

`--check` runs the whole thing and rolls back. That is not the same as validating the files: it resolves every cross-table reference against the real catalog and lets the database itself refuse a write, which is what an author wants to know before an import lands.

**The revision bump is conditional, not just once.** Once per transaction rather than per row — or a fifty-row file moves it fifty times. And **not at all** when nothing changed: `catalog_revision` is what E6 reproduces against and what E19 negotiates on, so a bump for content that did not change makes every connected receiver re-download the full push. Idempotency needed a prerequisite fix in E4/E5 (skip the update when no column differs) before it was reachable at all.

**2. Budget validation (E14b)** — rarity R may spend at most N power, looked up by the container's `rarity` FK. A content test enumerates every container, sums its atoms' vectors (E9), and **fails naming the offender**. This is the *only* role the budget plays: it never drives generation (E5 does).

*Honest caveat:* at E14b the only containers are E11's migration output — legacy effects and one trait, none of which carry a rarity. The test therefore enumerates almost nothing until real item content exists, and it must **say so in its output** rather than passing silently and looking green.

**3. Power drift (E14b)** — recompute every atom's power and compare to its stored `power_json`. Beyond **±25% per category, floor 1 point** without a `power_note` is a failure (definitions §7). This is what keeps "computed base + stored override" honest rather than decorative.

**4. Runtime support (E14b)** — a **lint**, not a validation. A container has no "claimed runtime" column, and the same container is legitimately bindable on the lawn and rejected in battle — that is the point of the living matrix. So E14b *reports* containers whose atoms have no consumer in any runtime, and the real check stays at bind time (E6). The earlier wording required a column E5 does not define.

### The Checkpoint D claim, as a test

> **A new effect using an existing kind costs one row, no build.**

`OneRowClaimTests` lives in **E11**, not here — Checkpoint D is where the claim is made, and E14b is five modules later. E14b only re-runs it as a regression.

"No rebuild of Core" is **not assertable from inside a process that already loaded Core**, so the test does not pretend to: it asserts the behavioural half, and the no-rebuild half is enforced by the test project referencing no new source. Saying that plainly is the alternative to quietly relaxing it.

### Content lint — the cheap checks that catch real mistakes

| Lint | Why |
|---|---|
| **(family, variant)** with a tier gap (1, 2, 4) | almost always a typo. Keyed on family+variant, not family — `elemental_power` holds 7 variants × 5 tiers, so a family-level check would hide a real gap in `ice` and invent false ones |
| Tier whose range does not exceed the tier below | a tier that is not stronger is not a tier |
| Two families writing the same channel with the same op | duplicate affix under two names |
| A pool group with one member | the group does nothing; likely a mistake |
| An atom no container references | dead content — legal, but worth surfacing |
| **A tier band copied between channel families** | the units trap (E2): `+10 hp` and `+10 fire power` differ by an order of magnitude |

Lints **warn**; validations **fail**. Keeping the two separate stops lint noise from blocking a legitimate edge case.

## Commands

```powershell
dotnet run --project tools\AtomImporter -- --check          # resolve everything, write nothing
dotnet run --project tools\AtomImporter                     # import
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AtomSeedFile"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~AtomImportTests"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Budget|PowerDrift"   # E14b
```

Seed root defaults to `data/seed`, found by walking up. Database comes from `--db <dir>`, then `FUSIONRPG_DATA`, then `dist/FusionRpg.Server/data`. Exit codes: `0` imported (or checked clean), `1` refused, `2` could not start — an empty sweep is `1`, never a silent green.

## Structure

**E14a, built 2026-08-22.** The blind spot is answered by moving, not by discipline. Two reasons,
and they are not the same one: the write path needs `RpgStore`'s private connection, gate and
unlocked writers, and `guard-dal.ps1` scans only `src/` — SQL under `tools/` sits outside the rule
that keeps SQL in one project. *Not* because a tool cannot be tested: `ItemSeedValidator` has a test
project and so does this one. What is left in the tool is arguments, a report, and `SeedScanner` —
which is a class, not top-level statements, so a test can hold it to the one thing it must never do.

```
data/seed/README.md                                       (the format, for authors)
data/seed/{atoms,containers,curves,rarity}/*.json         (authored content — corpus arrives with E11)
src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs          (the format: files -> rows, cross-file duplicates)
src/FusionRpg.Data/Sqlite/RpgStore.Import.cs              (validate-all -> one transaction -> one bump)
tools/AtomImporter/Program.cs                             (arguments + report; no SQL, no connection)
tools/AtomImporter/SeedScanner.cs                         (which files a sweep takes)
tests/FusionRpg.Core.Tests/Atoms/AtomSeedFileTests.cs     (25)
tests/FusionRpg.Data.Tests/AtomImportTests.cs             (17)
tests/FusionRpg.AtomImporter.Tests/SeedScannerTests.cs    (10 — new project, wired into CI)
```

E14b (later):

```
tests/FusionRpg.Core.Tests/Atoms/BudgetValidationTests.cs
tests/FusionRpg.Core.Tests/Atoms/PowerDriftTests.cs
tests/FusionRpg.Core.Tests/Atoms/ContentLintTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Seed file with one malformed row | import fails, names the row **and its file**, imports nothing (E14a) ✅ |
| A bad *container* in the same import as good atoms | the atoms do not land either — the failure is in another table ✅ |
| Duplicate `atom_id` across two seed files | import fails naming **both** files — last-write-wins would make content order-dependent on filesystem iteration ✅ |
| A container referencing an atom authored in the same import | accepted — a new item and its affixes arrive together ✅ |
| An atom scaling through a curve authored in the same import | accepted ✅ |
| Two rarity bands claiming one ordinal | refused; ordinals are append-only ✅ |
| A `schemaVersion` the importer does not know | refused, never read optimistically ✅ |
| Re-indenting a seed file, or reordering keys inside an object | **not** a content change: 0 rows changed, hash held ✅ |
| An edit to two rows | revision bumps **once**, not twice ✅ |
| A refused import | revision does not move ✅ |
| `--check` on a clean tree | reports what would change, writes nothing, revision and hash hold ✅ |
| A seed root holding `items/` | the item corpus is **never** swept — it is another tool's format ✅ |
| Files named `_something.json` | skipped as notes, matching the item seed tree's convention ✅ |
| Any sweep | ordinal-ordered, so a duplicate names the same two files on every machine ✅ |
| Container over its rarity budget | budget test fails, names the container and the overage |
| Budget test with no rarity-bearing containers | **reports "0 containers evaluated"** — never a silent green |
| Atom whose stored power drifts, no note | drift test fails |
| Same, with a note | reported, not failed |
| Container whose atoms have no consumer in any runtime | **lint warning**, not a failure |
| **Add one row, no rebuild** | effect grantable and firing — the Checkpoint D claim |
| Import twice | idempotent; content hash **and** catalog revision unchanged the second time ✅ |
| Lint findings | reported, never blocking |

## Boundaries

**Always:** validate before upsert; fail the import rather than importing partially; keep lints non-blocking; keep the one-row test green.

**Ask first:** adding a validation that can fail existing content; building an editor.

**Never:** a runtime content loader; external or user-supplied content; importing a file that failed validation; letting the budget influence which atoms roll.
