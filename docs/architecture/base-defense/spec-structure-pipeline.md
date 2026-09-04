# Spec: `structure-pipeline`

**Module 27 of 29 · level c4 · depends on `structure-planner` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. Folded in by owner decision 45.

---

## Objective

**The one stage that calls a model — and it writes identity only.**

Seedsmith **Law 2**, and the whole reason every module before this one is model-free:

> *"The LLM writes identity. Deterministic code writes magnitude … a model has no calibrated sense of
> scale, so a number it picks is a plausible-looking guess that survives review because nothing looks
> wrong with it."*

The planner already fixed **which rows exist, which tier is stronger, and what may sit where**. This
stage fills in name, family, flavour and `reason`, and picks enums from vocabularies the plan opened.

**Success looks like:** a corpus that extends the hand-authored distribution rather than replacing it,
with a byte-identical rerun proven by hash.

---

## ⛔ This is an INVENTION pipeline, and that is not the demon one

Decision 43 confirmed static plants stay demons, so — per `structure-seed-ideal.md` §3 — there is no
corpus to classify:

| | Demon (classify) | Structure (**invent**) |
|---|---|---|
| Input | one captured almanac entry per call | a **combination** — (role × slot kind × climate) |
| Failure mode | mis-assignment; **caught by majority vote** | **mode collapse and generic flavour — vote does not catch it** |
| Metric loop | closed — a field is populated or it is not | flavour distinctness is **open-loop**, so it produces a **review queue and never a pass** |

**Copying the demon pipeline's shape without noticing this gets all three wrong.** It is stated at the
top for that reason.

---

## The contract

### 1. Enum selection is the most bias-prone task there is, and this contract is entirely made of it

> *"Enum selection is the most bias-prone task shape there is, and an enum-only contract is entirely
> made of it. **Reordering options alone swings accuracy by up to 75 points.**"*

**Permute every enum, seeded from `(entity_id, field, sample_index)`.** Free, and:

> ⚠️ **`sample_index` must be *inside* the seed, or the three votes are one sample with extra steps.**

### 2. Vote only the load-bearing fields

*"Majority-vote only the load-bearing fields. Voting everything triples the run."* The plan declares
which (`plan.voteFields`), chosen by **cost-of-being-wrong** — *"a vote set chosen by vibes"* is a
listed red flag.

**`1-1-1` → `unresolved`, never the first option.** A three-way split is a real signal that the row is
ambiguous; taking option one silently converts it into a confident wrong answer.

### 3. Constrained decoding, proven

> *"**Prove constrained decoding is on** with one real call before the run."*

One call, asserted, before any batch. Not a config check — a real call whose output is verified to be
inside the vocabulary.

### 4. TRANSIENT is not QUALITY

> *"A pause is transient — **replay, no new call**. Name the defect when re-prompting; **bound repairs
> at two**."*

Two distinct paths that a naive retry loop merges, at which point a rate limit and a bad answer cost
the same and neither is diagnosable.

### 5. Idempotency, proven by hash

> *"Stochastic output breaks idempotency and you will not notice. Provenance + `stale_ids()` +
> byte-identical rerun proven by hash. **This repo has already shipped this bug once.**"*

The harness is built and proven in `structure-corpus`, where the inputs are hand-authored and trivially
idempotent. **This module inherits it rather than inventing it** — which is the point of ordering the
model-free modules first.

### 6. The model never touches a magnitude

Enforced by `structure-schema`'s audit over the **output**, not by prompt discipline. *"Enforced
mechanically by a schema audit, never by review."*

It also never chooses:

- **how many rows exist** — the plan did
- **which tier is stronger** — the ladder did
- **what may sit on which slot** — the plan did
- **any ordinal's interval** — tuning does

### 7. Mode collapse is watched explicitly

The failure vote cannot catch. Two cheap guards:

- **n-gram overlap across `reason` and `family` within a role** — nine "Sturdy Wall" variants share
  vocabulary long before a human notices.
- **A review queue, not a gate.** Flavour distinctness is open-loop, and *"an open-loop metric never
  contributes to a pass."*

---

## Tunables

`data/tuning/structure-seed.v{n}.json` → `plan.voteFields`, `plan.voteCount`, and the collapse-guard
thresholds. **No magnitudes are authored here, and none are authored by the model.**

## Numeric types

None produced. Every output is an enum, an ordinal, an id or free text — the invariant
`structure-schema`'s audit enforces.

## Boundaries

**Always:** permute every enum with `sample_index` inside the seed · vote only declared fields ·
`1-1-1` → `unresolved` · prove constrained decoding with a real call · bound repairs at two · commit
every generated row.

**Ask first:** widening the vote set · a third repair attempt.

**Never:** a model picking a magnitude, weight, probability or duration · a new roll beside
`Instantiator` (Law 1: *"no need to duplicated code for all"*) · a test that calls a model · treating an
open-loop metric as a pass · re-running the searches `06-unsourced.md` already exhausted.

---

## Testing

**Tests never call a model. Stub the transport so it *raises*.**

| Test | Asserts |
|---|---|
| `Enum_options_are_permuted_per_entity_field_and_sample` | the 75-point bias |
| `Sample_index_is_inside_the_seed` | three votes are three samples |
| `Only_declared_fields_are_voted` | budget |
| `A_1_1_1_split_yields_unresolved` | never the first option |
| `Constrained_decoding_is_proven_before_the_run` | one real call, asserted |
| `A_transient_failure_replays_without_a_new_call` | TRANSIENT ≠ QUALITY |
| `Repairs_are_bounded_at_two` | |
| `Rerun_is_byte_identical_proven_by_hash` | the harness inherited from `structure-corpus` |
| `Stale_ids_are_detected` | provenance |
| `No_generated_row_holds_a_number` | Law 2, over real output |
| `Mode_collapse_guard_flags_repeated_vocabulary` | and **flags — never fails** |
| `Open_loop_metrics_never_contribute_to_a_pass` | |
| `Transport_stub_raises_if_a_test_calls_a_model` | the discipline itself |

## Success criteria

1. The model writes identity only — proven by the schema audit over generated output.
2. Byte-identical rerun proven by hash.
3. Constrained decoding proven with a real call before the run.
4. Vote set declared, not defaulted; `1-1-1` resolves to `unresolved`.
5. Mode-collapse guard produces a review queue, never a pass.
6. Zero model calls in any test.

## Open questions

None.
