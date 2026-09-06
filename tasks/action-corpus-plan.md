# Implementation plan: `action-corpus` — A-S7 `coverage-assignment`

**Spec:** [spec-coverage-assignment.md](../docs/architecture/action-corpus/spec-coverage-assignment.md).
**Map:** [action-corpus-map.md](../docs/architecture/action-corpus-map.md).
**Tasks:** [action-corpus-todo.md](action-corpus-todo.md).

⛔ **Scope note.** The `action-corpus` capability map lists 15 modules; A-S0 through A-S6, A-P1-A-P3,
A-T1/A-E1/A-G1/A-R1/A-U1/A-C1 are **already built and tested** this session (real code under
`tools/seedsmith/seedsmith/adapters/actions/`, real committed rounds 1-2/903-907). This plan is scoped
to the one thing that is genuinely new: **A-S7 and its two small touch-points on already-shipped
modules** (A-S1's `pairing.forcedEnabler` field, and a splice line in each of the three propose
pipelines' `finalize_candidate`). It does not re-plan or re-verify the other 15 modules.

**Not `tasks/action-plan.md`/`action-todo.md`.** Those are a different, already-closed program (the
action *runtime* — `ActionRow`/`ActionCompiler`/etc., closed 2026-08-28) and were not read or touched.

---

## Why this plan exists — the real, measured trigger

A prompt-only diversity cue (roster-balance FC3) was built, proven correct in isolation, shipped, and
then run for real three times — and it moved nothing (59/100 families used, flat across three real
batches, `docs/research/action-corpus/_usage-2026-09-06.json`). The same soft-cue shape already
existed for pairing roles (`atom.chill-punisher`/`atom.rot-punisher`, still 0-usage after real runs).
**A cue the model can ignore is not a guarantee — this plan replaces the guarantee-shaped work the cue
was standing in for**, with a deterministic assignment-and-splice mechanism instead of a bigger cue.

---

## Architecture recap (full detail in the spec — restated so the task list is readable)

- **A-S7 computes `requiredFamilies`** (0 or 1 family id) per brief: a pairing-role brief's own
  payoff/enabler family, or the next family in a round-robin ordered by CURRENT real usage ascending
  (recomputed fresh every round from FC1's report — never a persisted cursor, §5 of the spec).
- **`finalize_candidate` splices it in** after voting, on an ACCEPTED outcome only — never rescues an
  unresolved vote, never touches `allowedAtomFamilies` (constraint 4 stays intact), zero model calls.
- **A-S1 gains one small, additive field** (`pairing.forcedEnabler`) so A-S7 doesn't have to re-derive
  `assign_pairing_roles`'s own internal tie-break logic.

```
A-S1 (+ forcedEnabler) ─► A-S7 (+ requiredFamilies) ─► A-S2 (unaffected, copies fields through)
                                                    ─► A-P1 / A-P2 / A-P3 (splice on accept)
```

---

## Phases

### Phase 0 — the one dependency A-S7 needs first

Small, foundational, additive-only change to an already-shipped module.

### Phase 1 — A-S7's own algorithm, model-free

The round-robin + conflict-skip logic, pure functions, tested against both synthetic fixtures and the
real committed FC1 report.

### Phase 2 — A-S7's CLI

The file-handoff entrypoint (`generate_coverage_assignment.py`), mirroring A-S1/A-S2's own
`--plan`/`--plan-out` shape.

### Phase 3 — the splice, in all three propose pipelines

Three identical, small, independently-testable changes — `finalize_candidate` in `general_propose`,
`family_propose`, `signature_propose`.

### Phase 4 — real proof

One small real batch through the full chain, confirming the previously-flat 59/100 number actually
moves. **No gate here** — this is a small, reversible, real-content run of exactly the kind this
session has already done repeatedly without needing separate sign-off each time; per the capability
map's own constraint 2 ("small-batch proof before any full run… the owner decides when to fully run"),
it ships with a small default `--count`, not a blocking approval step.

---

## Risks

| Risk | Impact | Mitigation |
|---|---|---|
| A required family collides with `multiplicativePairs` | Would recreate the exact defect constraint 4's forbidden-pairs rule prevents | Checked at assignment time (§4 step 5 of the spec), reusing `validate_no_multiplicative_conflict` verbatim — never a parallel implementation |
| The splice fires on an unresolved/blocked candidate | Would fabricate content the vote itself rejected | `finalize_candidate`'s own `outcome` check gates the splice; a planted-violation test proves it |
| A stale plan (missing `pairing.forcedEnabler`, pre-Phase-0) reaches A-S7 | Silent wrong assignment for enabler-role briefs | A-S7 refuses, naming the missing field, rather than defaulting to `role: "none"` behavior |
| The real proof batch (Phase 4) still shows no movement | The mechanism itself would need re-diagnosis, not just this plan's tasks | Phase 4 is evidence-gathering, not a success declaration — report the real number either way |

---

## Verification commands

```powershell
python -m pytest tools/seedsmith/tests/test_distribution_planner.py     # Phase 0
python -m pytest tools/seedsmith/tests/test_coverage_assignment.py      # Phase 1-2 (new)
python -m pytest tools/seedsmith/tests/test_general_propose.py tools/seedsmith/tests/test_family_propose.py tools/seedsmith/tests/test_signature_propose.py   # Phase 3
python -m pytest tools/seedsmith/tests                                  # full sweep, every phase
python -m seedsmith.adapters.actions.generate_usage_stats --gate        # Phase 4 real evidence
```

---

## Deferred, with a reason

- **Retrofitting `requiredFamilies` onto already-shipped rounds (901-907).** Explicitly named as an
  open, undecided content question in the spec's own §10 boundaries — a real decision (would change
  already-accepted, already-committed candidates) that this plan does not make unilaterally. The
  mechanism applies forward from whichever round it first runs in; nothing before that round is
  touched.
- **A non-round-robin (e.g. weighted-random) rotation order.** Only worth building if a real run shows
  round-robin's own strict ascending-usage order produces a worse content shape than expected — no
  such evidence exists yet.
