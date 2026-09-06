# Spec: `generator-harness`

**Module id:** `generator-harness` · **Program:** [item-seedgen](../item-seedgen-map.md) · **Build order:** 1 of 10 — foundation
**Depends on:** nothing (every other module depends on this)

## Objective

One shared resume/append/reconcile/selective-overwrite base every generator in this program builds on,
so eight modules don't each invent their own ledger, and none of them defaults to a destructive
full-corpus overwrite. Not a new pattern: `setgen` already proved this at ~1,800-entry scale
(`adapters/items/setgen/run.py`: *"Resume is not optional... The ledger is a single JSON file keyed by
subject id, written after each subject completes... reuses [the demon harness's] atomic file lock
discipline"*). This module extracts that logic into a shared, reusable piece and adds the one thing it
doesn't do yet: validate an existing entry's actual shape before trusting a ledger hit, so a corpus a
hand edit broke out of band gets reconciled, not silently skipped.

**Target users:** every other `item-seedgen` module's own generator command; indirectly, whoever runs
those commands (the owner, or a future automated build pass).

## 2. Acceptance criteria

1. A shared `RunLedger` (or equivalently-named) component: given a list of subject ids and an
   `is_valid(subject_id, entry) -> bool` check the caller supplies, returns exactly the subjects that
   need work — never-attempted ones, and ones whose CURRENT on-disk entry fails `is_valid` (not just
   ones missing a ledger row).
2. The ledger file itself is written via temp-file-then-atomic-replace, matching the demon harness's
   already-proven discipline — a killed process mid-run leaves the ledger in its last-good state, never
   half-written.
3. Default CLI shape, standardized across every module that uses this harness: `--write` runs
   append+reconcile (the default, always). `--overwrite <id[,id...]>` explicitly regenerates named
   subjects even if `is_valid` would have skipped them. `--overwrite all` regenerates the whole corpus —
   present, but never the default, and requires the explicit literal `all`, not a bare `--overwrite`.
4. Reconcile is demonstrably NOT "was there a ledger row" — a test corpus with a structurally invalid
   entry AND a ledger row claiming it's done must still be re-queued.
5. A dry-run mode (`--dry-run`) reports what WOULD be generated/reconciled/overwritten without writing,
   matching `setgen`'s own existing dry-run convention (referenced in its module docstring).

## 3. Commands / interfaces touched

- New shared module, likely `tools/seedsmith/seedsmith/pipeline/run_ledger.py` (alongside the existing
  cross-adapter `pipeline/llm_caller.py`, `pipeline/model.py`) — a library, not its own CLI entry point.
- Every module 2-10 below imports and uses this rather than writing its own resume logic.

## 4. Project structure

```text
tools/seedsmith/seedsmith/pipeline/run_ledger.py    new — RunLedger class, atomic write, plan_run()
tools/seedsmith/tests/test_run_ledger.py            new — the acceptance criteria above, as tests
```

## 5. Code style

Match `setgen/run.py`'s own established shape and docstring density — this is a direct generalization
of code that already exists and already works, not a fresh design. Reuse the demon harness's atomic-lock
primitive rather than re-implementing file locking a second time (grep `adapters/demons/run/` for the
existing implementation before writing a new one).

## 6. Testing strategy

- Resume: seed a partial ledger, confirm `plan_run` returns only the unattempted subjects.
- Reconcile: seed a full ledger claiming everything done, corrupt one on-disk entry, confirm that one
  subject is returned by `plan_run` despite its ledger row.
- Overwrite: `--overwrite one-id` touches only that id; every other valid entry is byte-unchanged.
- Overwrite-all requires the literal `all`; any other value is refused, not silently treated as one id.
- Kill-mid-run simulation (write ledger, `os._exit` or equivalent before completion) leaves the ledger
  parseable and consistent with what was actually completed.

## 7. Boundaries

**Always:** use this harness for any generator producing more than one entry. Default every module's
`--write` to append+reconcile.

**Ask first:** nothing structural here — this module's whole point is removing a class of destructive
default, not introducing a new gate.

**Never:** let append-mode silently accept a structurally invalid existing entry because a ledger row
merely exists. Never make `--overwrite all` reachable by a bare flag with no value — the literal `all`
is required, so a typo'd `--overwrite` (no argument) fails loudly rather than defaulting to "everything."
