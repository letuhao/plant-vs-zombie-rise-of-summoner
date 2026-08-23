# Seedsmith — `pipeline`

**Status:** Proposed 2026-08-23. Nothing is built. **Wave 3** — gated on `metrics` and `planner`
being green, because a pipeline that cannot be graded is a pipeline nobody can trust.

The only module that calls a model. One pipeline per metric, each closing one finding class.

---

## 1. What the agentic build actually taught

Across roughly 90 agent runs, **not one failure was a generation failure.** No agent wrote
incoherent JSON or invented a fantasy unrelated to the brief. Every expensive failure was one of:

| Failure | Count | Root cause |
|---|---|---|
| Executed a brief that was wrong | ~40 re-runs | briefing, not generation |
| Followed an exemplar that was wrong | 3 waves | input validation |
| Produced valid-but-wrong output that passed | several | the gate did not check that property yet |
| Correctly reported BLOCKED | 7 | *the system working* |

**Design consequence:** the model is the reliable part. Effort belongs in the brief, the schema and
the gate — not in prompt cleverness. Every mechanism below exists to make a wrong instruction
impossible to follow, not to make the model smarter.

The seven BLOCKED reports matter as much as the failures. Each one caught a defect in *my* input
that no validator would have found, so **the escape hatch is a feature to preserve**, not friction
to engineer away.

---

## 2. Structure — one pipeline per metric

```
finding → brief (from planner) → generate → parse → validate → accept | retry | escalate
```

A pipeline is a small declarative object, not a script:

```python
Pipeline(
    metric      = "Quality/FlavourMissing",
    scope       = "one partition file",
    schema      = FLAVOUR_SCHEMA,          # structured output, enforced at the API boundary
    gate        = [c_sharp_validator, reachability, metric_recheck],
    max_retries = 2,
    on_persist  = "escalate",              # never silently accept
    model       = "local:gemma-26b",       # per §5
)
```

Binding it to a metric rather than to a task is what makes it gradeable: the pipeline succeeds iff
its metric goes from failing to passing. Nothing else counts as success — not the model's own
report, which the earlier waves showed to be optimistic in exactly the cases that mattered.

---

## 3. Guardrails

**3.1 Structured output at the API boundary.** A JSON Schema per pipeline, enforced by the provider
where supported and validated locally regardless. Never parse prose. This alone removes the entire
class of shape defects the earlier waves hit.

**3.2 Narrow scope per call.** One partition, one field family. The unique partitions each held
seven simultaneous constraints and that is where deviation appeared. A call that can only write
`flavor` on twenty consumables cannot get a role allocation wrong, because it is not being shown one.

**3.3 Closed vocabularies inlined, not referenced.** A brief that says "tags come from
`tags.v1.json`" invites invention; the earlier waves lost 51 tags that way. The brief carries the
literal legal list, generated from the registry at emit time — never transcribed. Same for elements,
where three invented values shipped.

**3.4 Never a number.** P1 is absolute and it is easy to enforce here: a pipeline whose schema
contains a numeric field for a magnitude is a design error, caught by a test over the schemas
themselves rather than at runtime. Bands are enums; magnitudes come from `numerics`.

**3.5 Validate before accept, always.** Output is written to a scratch location, gated, and only
then moved into the corpus. Nothing partially-valid ever lands. This also makes a failed run leave
no trace to clean up.

**3.6 Bounded retry with the error attached.** Two retries, each carrying the validator's actual
message. Beyond that, escalate — a third identical failure means the brief is wrong, and retrying a
wrong brief is how you spend a budget discovering nothing.

**3.7 Preserve BLOCKED.** Every schema carries a `blocked` variant with a reason string. An agent
that cannot proceed says so and writes nothing. A blocked partition is cheap; a partition that
guessed is expensive and invisible.

---

## 4. Idempotence and provenance

Re-running a pipeline over already-good content must be a no-op — pipelines run in loops and
regenerating passing content burns budget and churns the diff.

Every generated entry records `_provenance`: pipeline id, model, prompt version, budget version,
timestamp, and the finding it closed. That makes three things possible that were painful this
session: identifying every entry a bad prompt version produced, re-running exactly those, and
answering "why does this row exist" months later.

---

## 5. Model selection

| Work | Model | Why |
|---|---|---|
| Flavour text, names, descriptions against a tight schema | **local (Gemma 26B QAT)** | High volume, deterministic gate, free retries. Schema does the constraining, so a smaller model is sufficient. |
| Identity content with cross-file reasoning — uniques, sets | **hosted, stronger** | Must grep, verify ids, hold several constraints. This is where a weak model produces valid-but-wrong output. |
| Anything deterministic | **no model** | R1, R2 and R3 were scripts: 180 set members, 144 acquisition rows, 740 enhance tracks, in seconds. |

The third row is the one that matters most. **Before writing a pipeline, ask whether the task needs
a model at all** — most of the closed-loop findings this session did not, and the ones that did were
the ones needing prose or judgement. A pipeline for work a script can do is a slow, expensive,
non-reproducible script.

---

## 6. Open-loop pipelines and the review queue

A closed-loop pipeline verifies itself: `FlavourMissing` passes when the field is populated. An
open-loop one cannot — *is the flavour any good* has no machine answer — so it **never reports a
pass**. It writes content, marks it `needsReview`, and pushes a stratified sample (analytics §8)
into a review queue.

A human reads eight of sixty, not all sixty. Their verdict is recorded against the sample, and if
they reject it, the rejection becomes a **new metric wherever one can be written** — which is the
loop that makes manual effort fall over time instead of recurring. Sampling with a stable seed means
the same sample can be re-read and a judgement diffed against itself.

---

## 7. Cost and safety

- **Dry run first.** Every pipeline renders its briefs and prints the call count and token estimate
  before spending anything.
- **Hard call cap per run**, from configuration. A loop bug should cost one cap, not one budget.
- **Never touch a registry, an exemplar, a contract, or another partition.** Enforced by writing
  through a path allowlist derived from the work order, not by asking politely in the prompt — the
  earlier waves relied on the prompt and got lucky.
- **Git stays manual.** The pipeline writes files; the owner commits.

---

## 8. Acceptance

A pipeline is done when: its metric goes red→green on a corpus deliberately broken for the purpose;
it is a no-op on a healthy corpus; it escalates rather than loops on an unsatisfiable brief; and its
schema contains no numeric magnitude field. All four are testable without a model in the loop, using
a recorded fixture, so the test suite stays fast and offline.
