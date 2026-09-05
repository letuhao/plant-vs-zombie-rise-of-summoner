# Spec: `plan-apply` (RB5)

**Module id:** `plan-apply` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 6 of 6**
**Depends on:** RB4 `rebalance-plan` · **Model calls: none** (it *requests* generation; it never authors)

## Objective

**Apply an approved rebalance plan to the real corpus — reversibly, with provenance, and never
inventing identity.** This is the only module in the program that writes to
`data/seed/demons/species/**`, and it is last on purpose: everything before it is measurement and
proposal, which are safe to run at any time.

## Design

### It never authors a species

An `add` move says *"a species with these characteristics is needed"*. It does **not** write one.
Authoring identity — name, flavour, the actual creature — belongs to the existing `demon-seed`
generator (`DemonSpeciesGen` / `classify-pipelines`), which already does it and already has the
prompts, the vote machinery and the quality gates for it.

This is the program's own Law 2: **deterministic code decides the shape; the model writes the
identity.** A rebalance module that started naming creatures would be the exact boundary violation
the whole architecture exists to prevent.

So `add` emits a **request** — a structured "wanted" row the demon-seed generator consumes:

```jsonc
{ "move": "add", "cellId": "aptitude=Ferocity|element=air|posture=Finesse",
  "wanted": { "aptitudePrimary": "Ferocity", "elementPrimary": "air", "posture": "Finesse" },
  "reason": "cell empty; target 4, actual 0" }
```

### Reassign writes, and therefore carries the strictest rules

A `reassign` edits a committed row, so:

- **One axis per move.** A move that would rewrite two characteristics is two moves, reviewable
  separately. A row silently changing on several axes at once is unreviewable.
- **Provenance is stamped, never overwritten.** The row keeps its original value and gains a
  `_rebalance` record: previous value, new value, the plan that moved it, the cited reason, the date.
  **The corpus must never lose the fact that a value was machine-moved** — a later reader has to be
  able to tell an authored decision from a rebalance.
- **`basis` and `confidence` are updated honestly.** A row moved because its classification was
  low-confidence does not silently become high-confidence; it records that a deterministic engine
  placed it.

### Reversible by construction

Every apply emits a **reverse plan** alongside the forward one: applying the reverse restores the
corpus byte-for-byte. Proven by test, not asserted — apply, reverse, compare hashes.

This is what makes the module safe to run at all. `git` is not the safety net here: this repo's
binding rule is that **the assistant never runs a git write command**, so reversibility has to live
in the artefact, not in version control.

### Idempotent and resumable

Applying the same plan twice is a no-op the second time (each move records its own application), and
a plan interrupted halfway can be re-run to completion without double-applying. A partial apply that
cannot be resumed would leave 841 rows in an unknown state.

### It refuses more than it accepts

The module hard-refuses, naming the offending move:

- a plan whose `corpusHash` does not match the corpus on disk — **the corpus moved under the plan**,
  which is the single most likely real failure mode in a repo with concurrent work;
- a `reassign` with no cited reason (RB4 should never emit one; this is the second gate);
- a move that would write an illegal axis value (`posture: "unresolved"` can never be *created*);
- a move touching a row that already carries a `_rebalance` record for the same axis in this plan.

## Commands

```powershell
python -m seedsmith roster apply --plan <path> --dry-run    # default; prints the diff, writes nothing
python -m seedsmith roster apply --plan <path> --commit     # the only writing command in the program
python -m seedsmith roster apply --reverse <path>           # restore
python -m pytest tools/seedsmith/tests/test_plan_apply.py
```

**`--dry-run` is the default and `--commit` is explicit**, matching this program's own
small-batch-before-full-run discipline.

## Project structure

```text
tools/seedsmith/seedsmith/adapters/roster/apply/writer.py     new — the sole corpus writer
tools/seedsmith/seedsmith/adapters/roster/apply/reverse.py    new — reverse-plan emission
tools/seedsmith/seedsmith/adapters/roster/generate_apply.py   new — CLI entry point
data/seed/roster/_plans/applied-<round>.json                  new — what was applied, and its reverse
data/seed/demons/species/**                                   edit — ONLY by this module
tools/seedsmith/tests/test_plan_apply.py                      new
```

## Code style

```python
# The row KEEPS its original value and gains a record. A reader must always be able to tell an
# authored decision from a machine move -- overwriting provenance would erase exactly that.
row.setdefault("_rebalance", []).append({
    "axis": axis, "from": previous, "to": new_value,
    "plan": plan_id, "reason": reason, "appliedUtc": stamp,
})
```

## Testing strategy

| Test | Asserts |
|---|---|
| `dry_run_is_the_default_and_writes_nothing` | filesystem assertion, not a flag check |
| `apply_then_reverse_restores_the_corpus_byte_for_byte` | hashed before and after |
| `applying_the_same_plan_twice_is_a_no_op` | idempotence |
| `an_interrupted_apply_resumes_without_double_applying` | simulated partial failure |
| `PLANTED_VIOLATION_a_stale_corpusHash_is_refused` | the corpus moved under the plan — the likeliest real failure |
| `PLANTED_VIOLATION_a_reassign_without_a_reason_is_refused` | second gate behind RB4's |
| `PLANTED_VIOLATION_a_move_creating_an_illegal_value_is_refused` | `posture: "unresolved"` can never be created |
| `a_reassign_stamps_provenance_and_keeps_the_previous_value` | the record is additive |
| `a_reassign_never_silently_raises_confidence` | honesty about why the value changed |
| `one_move_touches_exactly_one_axis` | a two-axis move is refused as two moves |
| `an_add_move_emits_a_REQUEST_and_authors_nothing` | no species name or flavour is ever written here |
| `the_module_is_the_only_writer_of_the_species_corpus` | repo-wide grep guard, mirroring `guard-single-writer.ps1`'s convention |

## Boundaries

**Always:** default to `--dry-run`; emit a reverse plan; stamp provenance additively; verify
`corpusHash` before writing; refuse loudly and by name.

**Ask first:** applying a plan containing any `reassign` against authored, high-confidence rows —
even when RB4 permitted it, this is content someone decided.

**Never:** author a species name, flavour or identity; overwrite provenance; write more than one axis
per move; create an illegal axis value; write without a matching `corpusHash`; run a git command.

## Success criteria

- [ ] `--dry-run` is the default; only `--commit` writes.
- [ ] Apply → reverse restores the corpus to an identical hash, proven by test.
- [ ] A stale `corpusHash` is refused, naming the mismatch.
- [ ] Every `reassign` leaves a `_rebalance` record preserving the previous value and its reason.
- [ ] `add` moves emit requests consumed by the existing demon-seed generator; this module authors no
      identity.
- [ ] A guard proves this is the only module writing `data/seed/demons/species/**`.
