# Spec: `run-control`

**Module id:** `run-control` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 9 of 16
**Model calls:** none — it decides which ones `classify-pipelines` makes.

## Objective

A run state machine over a 16,000-call, ~14-hour job: **pause · resume · cancel · rerun ·
overwrite-all**, with a run record that survives the process.

Owner, Q20: *"full run with state machine that support pause/resume/cancel/rerun/overwrite all. so we
will generate what we want and stop when we want."*

## Design

### 1. Why this is a module and not a flag

At the budget `option-permutation` §6 computes — 7,232 base calls plus 9,040 vote calls — a full run
does not fit in one sitting, one terminal, or one uninterrupted machine state. **"Stop when we want"
is the requirement, and it only means anything if stopping loses nothing.**

The workflow runtime already provides the hard half. `workflow/graphs/checkpoint.py` opens a LangGraph
`SqliteSaver`; `workflow/runner.py` provides `resume()` with a documented distinction this module must
not blur:

> **TRANSIENT** (endpoint down, timeout, 5xx) → `resume()`: replay from checkpoint, **no new model
> call**. **QUALITY** (a validator rejected the draft) → a genuinely new generation with the defect
> named.

**A user-requested pause is TRANSIENT.** Resuming after a pause must replay the checkpoint, not
re-generate — otherwise pausing costs money and changes answers, and nobody pauses twice.

### 2. The state machine

```text
              +--------------------------- cancel ------------------------+
              |                                                            v
   idle --> running --> paused --> running --> ... --> completed        cancelled
              |  ^         |
              |  +- resume -+
              v
           failed --(resume)--> running
```

| Verb | Effect |
|---|---|
| `start` | requires a matching preflight record; refuses if one is already `running` |
| `pause` | finish the in-flight species, checkpoint, stop. **Never mid-species** |
| `resume` | continue from the checkpoint; TRANSIENT semantics, no regeneration |
| `cancel` | stop and mark the run terminal; emitted species stay, they are already valid seeds |
| `rerun <selector>` | re-generate a named subset, ignoring "already emitted" |
| `overwrite-all` | the Q19 one-time full re-derivation; **requires an explicit confirmation token** |

`pause` finishing the in-flight species matters: a species half-classified across eight pipelines is
not a resumable unit, and treating it as one is how a resumed run emits an anchor with four fields
from before the pause and four from after — with two different prompt versions in one entry.

### 3. The run record

`data/seed/demons/_runs/<runId>.json`, committed **only for completed runs**; in-progress records live
beside the checkpoint DB and are gitignored.

| Field | Purpose |
|---|---|
| `runId` | ULID; sortable, so the latest run is the last line |
| `state` | the machine's state |
| `preflight` | the record `dump-preflight` wrote, copied in — not referenced |
| `dumpHash` | refuses to resume against a different dump |
| `selector` | what this run was asked to cover |
| `completed` / `failed` / `skipped` | species id lists, not counts |
| `promptVersions` | per pipeline, at start |
| `callsMade` | actual, for the next budget estimate |

**Lists, not counts.** "412 completed" cannot answer "was `normalzombie` done?", which is the question
a resume actually asks.

### 4. Selectors — "generate what we want"

```text
--all
--side zombie
--family gargantuar
--species normalzombie,conezombie
--pipeline element-primary          (one judgement across the roster)
--basis inferred                    (only the ones that were guessed)
--unresolved                        (only fields a vote could not settle)
--stale                             (only entries whose inputs moved)
```

`--basis inferred` and `--unresolved` are the two that make the design pay off: after a full run, they
are exactly the sets a human wants to revisit, and both are computable from provenance without a model
call.

### 5. Refusals

| Situation | Behaviour |
|---|---|
| no preflight record, or it names a different dump hash | refuse |
| preflight was run with `--skip-model` | refuse — CI's escape hatch never reaches a real run |
| another run is `running` | refuse; name the `runId` |
| `overwrite-all` without the confirmation token | refuse; print the token |
| resume against a changed dump | refuse; offer `rerun --stale` instead |

`overwrite-all` discards work that cost 14 hours. It gets a typed token, in the same spirit as any
other irreversible action.

### 6. Interruption is not a state

A killed process leaves the record in `running`. The next `start` detects a record whose process is
gone, reports it, and offers `resume` — it does not silently take over, and it does not refuse
forever. **A crash must be recoverable without hand-editing JSON.**

## Commands

```powershell
python -m seedsmith demons run start --all
python -m seedsmith demons run pause
python -m seedsmith demons run resume
python -m seedsmith demons run cancel
python -m seedsmith demons run rerun --basis inferred
python -m seedsmith demons run overwrite-all --confirm <token>
python -m seedsmith demons run status          # state, progress, ETA from callsMade
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/run/machine.py    states + transitions, pure
tools/seedsmith/seedsmith/adapters/demons/run/record.py     persistence
tools/seedsmith/seedsmith/adapters/demons/run/selectors.py  the eight selectors
tools/seedsmith/tests/test_run_control.py
data/seed/demons/_runs/**                                   completed records, committed
```

`machine.py` is pure — states and legal transitions with no I/O — so every transition is testable
without a database.

## Code style

```python
# A user pause is TRANSIENT, not QUALITY: resume replays the checkpoint and makes no new
# call. Blurring this makes pausing cost money and change answers - runner.py's own
# docstring is the reason, and it costs $150 to learn the other way.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `pause_resume_makes_no_new_model_call` | transport stub raises; resume still completes |
| `pause_never_splits_a_species` | a paused species is either fully emitted or not started |
| `resume_against_changed_dump_refuses` | and names `rerun --stale` |
| `skip_model_preflight_cannot_start_a_run` | the CI hatch is sealed |
| `overwrite_all_requires_the_token` | irreversible action gated |
| `dead_process_record_offers_resume` | crash recovery without hand-editing |
| `record_lists_species_ids_not_counts` | the resume question is answerable |
| `every_selector_resolves_without_a_model_call` | all eight |
| `two_concurrent_starts_refuse_the_second` | and name the running id |

## Boundaries

**Always:** treat a pause as transient; finish the in-flight species; copy the preflight record in;
list species ids; refuse on a dump mismatch.

**Ask first:** adding a selector that can widen a run's scope.

**Never:** regenerate on resume; start without a matching preflight; overwrite without the token;
require hand-editing a record to recover from a crash.

## Success criteria

- [ ] A pause-and-resume cycle completes a full run with zero additional model calls, proven by a
      raising stub.
- [ ] No anchor entry ever mixes prompt versions from two sides of a pause.
- [ ] `rerun --basis inferred` and `--unresolved` resolve their sets without a model call.
- [ ] A killed run is recoverable with one command.
- [ ] `overwrite-all` cannot run by accident.
