# Spec: `option-permutation`

**Module id:** `option-permutation` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 6 of 16
**Model calls:** none itself — it decides how many `classify-pipelines` makes, and how they are combined.

## Objective

Neutralise the two measured biases in LLM enum selection — **label bias** and **position bias** — by
shuffling option order deterministically per species, and by taking a majority vote across three
shuffles on the five load-bearing fields.

Owner, Q8: *"Permute everywhere; majority-vote the load-bearing fields."*
Owner, Q25: eight pipelines, three-way vote on five fields.

## Design

### 1. The evidence this module exists for

Ideal §4.7, the strongest research finding in the round: **enum selection is the most bias-prone task
shape there is.** Reordering options alone swings measured accuracy by up to 75 percentage points on
GPT-4-class models; majority voting across permutations recovers up to 8 points. A model asked to pick
from a list is partly answering "which position is this?" rather than only "which value is this?"

**This is not a hypothetical risk being defended against.** The anchor is *entirely* enum selection —
that is the whole design. So the bias applies to every field this program produces, and it would apply
invisibly: a systematically first-position-favouring classifier produces a roster that looks plausible
species by species and is skewed in aggregate. `roster-metrics` is the only thing that would catch it
afterwards; this module is what prevents it.

### 2. Permute everywhere; vote where it is expensive to be wrong

| | Applies to | Cost |
|---|---|---|
| **Permutation** | **every** enum in every pipeline | free — it is a list reordering |
| **Majority vote (3 samples)** | five fields only | 2 extra calls per field per species |

The five voted fields, per Q25:

| Field | Why it is load-bearing |
|---|---|
| `elementPrimary` | the most expensive axis to change later; feeds the matchup table and 196 combat channels |
| `aptitudePrimary` | determines `posture`, which validates `resourceProfile`, and keys the class system |
| `rarity` | five verified consumers, both pity thresholds, and the summon economy |
| `threatBand` | sets the `Theta` offset, so it scales every magnitude the species ever has |
| `deployMode` | binary and irreversible in feel — a `HypnoAlly` misfiled as `PlantAvatar` is a different creature |

Everything else takes a single permuted sample. **A field whose wrong value is cheap to fix later does
not deserve three calls**, and the budget is real: voting on all twenty-one fields would triple a
16,000-call run.

### 3. Determinism — seeded from `speciesId`, never from a clock

```python
seed = blake2b(speciesId.encode() + b"|" + field.encode() + b"|" + str(sampleIndex).encode(),
               digest_size=8)
```

Three consequences, all required:

- **Re-running a species produces the identical permutation**, so a rerun that changes an answer means
  the *model* changed its mind, not that the prompt moved. Without this, the disagreement rate in §5
  measures nothing.
- **Two species never share an order**, so a bias that favours position 1 cannot systematically favour
  the same *value* across the roster.
- The permutation is reproducible from the seed file alone, so provenance does not have to store the
  order — it stores the `speciesId` and the sample index, which it already has.

`sampleIndex` is part of the seed, so the three votes genuinely differ. A three-way vote across three
identical orders is one sample with extra steps, and it is the obvious way to build this wrong.

### 4. Resolving a vote

| Outcome | Result |
|---|---|
| 3-0 | the value, `confidence: high` |
| 2-1 | the majority value, `confidence: split`, **the minority value recorded** |
| 1-1-1 | **no value** — the species is flagged `unresolved` for that field and does not silently take the first |

A 1-1-1 on a load-bearing field is a genuine signal that the species is ambiguous, and it is exactly
the case where a default is most damaging. `roster-metrics` reports the unresolved set; a human or a
targeted rerun resolves it. **Three disagreeing answers must never be averaged into the first one.**

### 5. The disagreement rate is a deliverable

Per field, over the whole roster: how often the three samples disagreed. This is the only direct
measurement of how reliable the contract actually is, and it feeds two decisions:

- a field with a high rate has a **weak description**, and `anchor-contract` §5's negative clause is
  the fix — this is the feedback loop that makes descriptions improvable rather than guessed
- a field with a near-zero rate is a **candidate to drop from the vote set**, halving its cost

Reported per field and per side, because a description can be clear for zombies and vague for plants.

### 6. What it costs

At eight pipelines, five voted fields, ~904 species:

```text
904 x 8 pipelines                  =  7,232 base calls
904 x 5 fields x 2 extra samples   =  9,040 vote calls
                                     ------
                                     16,272 calls
```

Roughly 14 hours on the local 26B model at the observed pace. **This is why `run-control` exists**, and
why the vote set is five fields rather than twenty-one.

## Commands

```powershell
python -m seedsmith demons permute --species <id> --field elementPrimary   # show the three orders
python -m seedsmith demons vote-report                                     # disagreement rate per field
python -m pytest tools/seedsmith/tests/test_option_permutation.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/anchor/permute.py   seeding + ordering
tools/seedsmith/seedsmith/adapters/demons/anchor/vote.py      resolution + confidence
tools/seedsmith/tests/test_option_permutation.py
```

## Code style

```python
# sampleIndex is IN the seed. Three votes over three identical orders is one sample
# with extra steps - the obvious way to build this wrong. See spec section 3.
def order_for(species_id: str, field: str, sample_index: int) -> list[str]:
```

## Testing strategy

| Test | Asserts |
|---|---|
| `same_species_same_order_across_runs` | determinism |
| `three_samples_have_three_distinct_orders` | the "obvious way to build this wrong" regression |
| `different_species_get_different_orders` | no systematic position-to-value mapping |
| `three_way_split_is_unresolved_not_first` | the 1-1-1 rule |
| `split_vote_records_the_minority` | the evidence survives |
| `vote_set_is_exactly_the_five_named_fields` | pins the budget so a sixth needs a decision |
| `disagreement_rate_is_reported_per_field_and_side` | the deliverable exists |

## Boundaries

**Always:** permute every enum; seed from `speciesId` + field + sample index; record minority values;
report disagreement rates.

**Ask first:** adding or removing a voted field — it moves the call budget by ~1,800 calls per field.

**Never:** seed from a clock or a global counter; resolve a three-way split by taking the first;
average enum values; vote without permuting.

## Success criteria

- [ ] The three samples for any species use three genuinely different orders, proven by test.
- [ ] A 1-1-1 split yields `unresolved`, never a value.
- [ ] The disagreement rate is reported per field and per side after a full run.
- [ ] Re-running an unchanged species reproduces the identical permutations.
- [ ] The voted set is exactly the five fields named here.
