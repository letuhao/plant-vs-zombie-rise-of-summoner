# Spec: `species-import`

**Module id:** `species-import` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 13 of 16
**Model calls:** none.

## Objective

Load `data/generated/demons/**.json` into SQLite in **one all-or-nothing transaction** — or write
nothing and say exactly why.

## Design

### 1. Extend the existing importer's discipline; do not mint a second one

`tools/AtomImporter` already does this job for the atom corpus, and its `Program.cs` states the
contract this module inherits verbatim:

> *"Reads `data/seed/**.json`, validates everything, and writes it in one transaction — or writes
> nothing and says why."*
> Exit codes: `0` imported/checked/validated clean, `1` refused, `2` could not start.

`seed-contract.md` §0 gives the reason not to fork it: *"Minting a second format would fork the content
hash and the import transaction."* Same argument applies to a second importer — two transaction
disciplines is one too many, and the second one is always the weaker.

### 2. But it is a separate root, and `SeedScanner` already knows why

`SeedScanner.cs:6-13` carries a warning that is directly relevant:

> *"The one decision in this tool that can be silently wrong. `data/seed/` also holds the item seed
> corpus — a different format read by `tools/ItemSeedValidator` — so a recursive sweep of the seed root
> refuses all ~125 of its files and reports the import broken."*

**The same trap is now set for demons.** `data/seed/demons/species/**` is a third format under the same
root. Two consequences, both required:

1. `SeedScanner`'s owned-folder list is **explicit**, and demon anchors are **not** added to it — the
   atom importer must keep ignoring them.
2. This module reads `data/generated/demons/`, not `data/seed/`, which sidesteps the collision
   entirely. The generated tree is a different root by construction.

A test pins the atom importer's root list so a future recursive-sweep "simplification" cannot silently
swallow demon files.

### 3. All-or-nothing, and what that means for 904 species

One transaction over the whole roster. A single invalid row rolls the entire import back.

**This is the right trade even at 904 rows**, and the reason is the failure it prevents: a partial
import leaves the catalog internally inconsistent — a recipe pointing at a species that did not land, a
wave band missing its rarity — and the game starts anyway, failing later somewhere unrelated. A refused
import fails in the one place a human is watching.

The refusal names the **first** failing row and the **count** of failures, not just the first. An
importer that reports one error per run turns a ten-defect batch into ten round trips.

### 4. Validation, in order

| Stage | Check | On failure |
|---|---|---|
| structural | schema of the concrete row shape | refuse, name the file and index |
| referential | every `family`, `trait`, `variant`, `element`, `aptitude`, `rarity` id resolves | refuse, name the dangling id |
| numeric | every magnitude fits `long`; no negative where a positive is required | refuse |
| **staleness** | the generated tree's hash matches what `species-generator --check` would produce | refuse — importing a stale tree is how the database and the repo disagree |
| uniqueness | `speciesId` and `demonTypeId` are unique; `demonTypeId >= DemonTypeIdFloor` | refuse |

The `demonTypeId` floor is `DemonSpeciesCatalog.DemonTypeIdFloor = 10_000`
(`DemonSpeciesCatalog.cs:29`), and its stated reason is that *"web-battle events must never collide
with PvZ type ids."* With 904 species the id space is far busier than it was with 84, so the collision
check stops being theoretical.

### 5. SQL lives in `FusionRpg.Data`

`scripts/guard-dal.ps1` enforces this for `src/`. It does **not** scan `tools/`, and the guard's
silence is not permission — the same rule the checkpoint module already recorded on the Python side:
*"Python still never reads the game's SQLite … that stays C#-through-the-DAL, for a reason shipping
does not affect."*

So: the transaction and every statement live in `FusionRpg.Data`; `tools/DemonSpeciesImport` holds
arguments, a report, and exit codes. Exactly the split `AtomImporter/Program.cs` already documents for
itself.

### 6. Idempotent

Importing the same generated tree twice leaves the database byte-identical, verified by a row-level
hash rather than by row count. Upsert by `speciesId`, and **delete rows whose id is absent from the
tree** — a species removed upstream must not linger, or the catalog silently accumulates the union of
every import that ever ran.

## Commands

```powershell
dotnet run --project tools/DemonSpeciesImport -- --db <server data dir>
dotnet run --project tools/DemonSpeciesImport -- --db <dir> --check     # validate + roll back
dotnet test tests/FusionRpg.Data.Tests --filter SpeciesImport
.\scripts\guard-dal.ps1
```

`--check` mirrors `AtomImporter`'s: validate against the real catalog and roll back, writing nothing.

## Project structure

```text
tools/DemonSpeciesImport/Program.cs             arguments, report, exit codes
src/FusionRpg.Data/Sqlite/RpgStore.Species.cs   the transaction and every statement
src/FusionRpg.Core/Demons/ConcreteSpeciesRow.cs the shape both sides agree on
tests/FusionRpg.Data.Tests/SpeciesImportTests.cs
```

## Code style

Match `AtomImporter/Program.cs`: the tool holds arguments and a report; every content decision is in
Core, every statement is in the data project, and a comment says so.

## Testing strategy

| Test | Asserts |
|---|---|
| `one_invalid_row_rolls_back_all_904` | nothing written |
| `refusal_names_first_failure_and_total_count` | not one-per-run |
| `dangling_family_id_refuses` | referential stage |
| `stale_generated_tree_refuses` | the repo/database agreement |
| `duplicate_demonTypeId_refuses` | the id space is busy now |
| `demonTypeId_below_floor_refuses` | the PvZ collision guard |
| `reimport_is_row_identical` | idempotency by hash |
| `species_removed_upstream_is_deleted` | no accumulating union |
| `atom_importer_still_ignores_demon_seed_folders` | pins `SeedScanner`'s root list |
| `no_sql_outside_the_data_project` | the guard, as a test |

## Boundaries

**Always:** one transaction; validate before writing; report every failure count; keep SQL in
`FusionRpg.Data`; delete absent ids.

**Ask first:** adding a table; changing the concrete row shape.

**Scope note (2026-09-01).** This module imports the **shared** layer: species definitions, their
derived stats, **and their `species-passive` container definitions** from `species-effects` (module 15).
It never writes a player's rows — that is `player-materialise` (module 16), which runs at profile
creation and rolls against the player's own world seed.

**Never:** partial import; SQL in `tools/`; add demon folders to `SeedScanner`'s roots; import a stale
tree; leave orphaned species rows behind.

## Success criteria

- [ ] A single bad row leaves the database untouched.
- [ ] A stale generated tree is refused, not imported.
- [ ] Re-importing produces an identical database, verified by row hash.
- [ ] `guard-dal.ps1` passes, and a test proves no SQL sits in `tools/`.
- [ ] The atom importer still refuses to sweep demon seed folders.
