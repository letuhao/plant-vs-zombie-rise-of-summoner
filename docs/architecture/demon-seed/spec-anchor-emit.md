# Spec: `anchor-emit`

**Module id:** `anchor-emit` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 8 of 16
**Model calls:** none.

## Objective

Write the classified anchors to `data/seed/demons/species/**.json` as **seed files** under
[item/seed-contract.md](../item/seed-contract.md)'s law, each carrying enough provenance that a future
session can tell what it was derived from and whether that is still true.

Owner, Q19: *"Re-derive everything once, then append-only from there."*

## Design

### 1. The anchor is a seed file, and the law is already written

> **The seed is generator input. It is not rows.** — `seed-contract.md` §1

So this module's output is **not** the thing the game reads. It is what `species-generator` expands.
Three consequences, taken directly from that contract:

1. **Anything a formula can compute is not written here.** No magnitude, no `Theta`, no `P(Theta)`, no
   allocation. `posture`, `pure` and `basis` are derived and are written **as a convenience echo with a
   `_derived` marker**, so a reader is never confused about who owns them.
2. **The output is committed and diffable.** A generated row nobody can review is a row nobody reviewed.
3. **A new computed field later costs zero seed files.** `species-generator` grows a column; every
   anchor on disk stays valid and untouched.

### 2. Layout

```text
data/seed/demons/species/
  plant/<family>.json        entries grouped by family, sorted by speciesId
  zombie/<family>.json
  _index.json                speciesId -> file, for a single-species reread
```

Grouped by family rather than one file per species: 904 files is a directory nobody can review, and
family is the grouping a human actually reads by. `_index.json` keeps single-species lookup O(1) so
`run-control` can resume without loading the tree.

Canonical serialisation is `corpus-dump`'s — sorted keys, two-space indent, `\n`, CJK unescaped,
explicit nulls — because these files are hashed too.

### 3. Provenance — the upgrade path, not bookkeeping

Every entry carries `_provenance`:

| Field | Why it is there |
|---|---|
| `dumpHash` | which snapshot this was derived from — the question every later session asks first |
| `promptVersion` | per pipeline; a description change invalidates exactly the fields that pipeline owns, not the whole entry |
| `basis` | `observed` / `stated` / `inferred` / `blocked` |
| `confidence` | per voted field: `high` / `split` / `unresolved` |
| `minorityValues` | what the losing vote said, where there was one |
| `auditVerdict` | `agree` / `too-low` / `too-high` from `threat-audit` |
| `emittedUtc` | when |

**This is the mechanism that makes `inferred` upgradeable rather than permanent.** Q26's answer works
only because provenance records *why* a value is what it is: when the owner later encounters a species
in-game, `spawn_stats` promotes it to `observed`, and re-derivation corrects that one entry — visibly,
in a diff, with the reason attached.

The existing `_provenance.motifs` pattern in `commander_effect.py` is the precedent, and its
`stale_ids()` staleness check is the shape to reuse: **an entry is stale when what it was derived from
has changed**, compared by recorded value, not by timestamp.

### 4. Re-derive once, append-only after — Q19

| Phase | Behaviour |
|---|---|
| **The one re-derivation** | every species, including the existing 84, is classified fresh. The old `DemonCorpusEmit` corpus is superseded and its tool deleted |
| **After that** | a species already in the tree is **not rewritten** unless it is stale (§3) or explicitly named by `run-control`'s rerun/overwrite verb |

Idempotency is a property this module must prove, not claim: a second run over an unchanged dump with
no rerun flag must produce **byte-identical files**, verified by hash. This is the exact defect that
was found and fixed in the commander-effect generator — it rewrote all 84 entries stochastically every
run — so the test exists because the failure already happened once.

### 5. What is deleted

`tools/DemonCorpusEmit` is removed in this module, not earlier. It is the current producer of
`data/seed/demons/demon/**`, and deleting it before its replacement emits is how a corpus goes missing
for a wave. Its 84-entry output is superseded by `species/**` and the old tree is removed in the same
change.

**`DemonSpeciesCatalog.Generated.cs` is NOT touched here.** The runtime keeps reading it until
`catalog-runtime` moves the nine consumers. A window where both exist is deliberate: it is the only
way to diff the new derivation against the shipped one.

### 6. The diff against the shipped 84 is a deliverable

Because both exist during that window, the re-derivation can be compared field by field against what
the C# generator produced. A large disagreement on `elementPrimary` is not automatically wrong — the
old generator assigned elements by a hash, not by reading anything — but it is the single best
sanity check available before 820 more species are trusted.

## Commands

```powershell
python -m seedsmith demons emit --dump data/seed/demons/_dump
python -m seedsmith demons emit --check              # exit 1 if the tree would change
python -m seedsmith demons emit --stale              # list entries whose inputs moved
python -m seedsmith demons emit --diff-legacy        # field-by-field vs the shipped 84
python -m pytest tools/seedsmith/tests/test_anchor_emit.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/anchor/emit.py        writer + staleness
tools/seedsmith/seedsmith/adapters/demons/anchor/provenance.py  the record
data/seed/demons/species/**                                     committed output
tools/seedsmith/tests/test_anchor_emit.py
```

## Code style

```python
# Stale means "what this was derived from has changed" - compared by recorded value,
# never by mtime. Same shape as commander_effect.stale_ids(), which exists because the
# generator used to rewrite all 84 entries stochastically on every run.
def stale_ids(entries, dump_hash, prompt_versions) -> list[str]:
```

## Testing strategy

| Test | Asserts |
|---|---|
| `rerun_over_unchanged_dump_is_byte_identical` | the idempotency defect, by hash |
| `changed_dump_hash_marks_exactly_the_affected_entries_stale` | not the whole tree |
| `changed_prompt_version_marks_only_that_pipelines_fields` | field-level granularity |
| `no_magnitude_appears_in_any_emitted_file` | the seed-contract rule, mechanically |
| `derived_fields_carry_the_derived_marker` | ownership is never ambiguous |
| `unresolved_field_is_written_as_unresolved_not_omitted` | a missing key must not mean "unsure" |
| `index_resolves_every_species` | resume works |
| `legacy_diff_reports_per_field_agreement` | the sanity check exists |

## Boundaries

**Always:** write canonical bytes; record provenance on every entry; prove idempotency by hash; keep
derived fields marked.

**Ask first:** deleting `tools/DemonCorpusEmit` (it is the current corpus producer); changing the file
grouping after anchors are committed.

**Never:** write a magnitude; rewrite a non-stale entry without an explicit rerun; delete the legacy
corpus before the new one emits; touch `DemonSpeciesCatalog.Generated.cs` in this module.

## Success criteria

- [ ] A second run over an unchanged dump produces byte-identical files.
- [ ] Every entry names the dump hash and prompt versions it came from.
- [ ] `--stale` lists exactly the entries whose inputs moved, and no others.
- [ ] No emitted file contains a number that is not an identifier.
- [ ] The legacy diff reports per-field agreement against the shipped 84 before they are superseded.
