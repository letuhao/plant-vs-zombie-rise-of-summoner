# Spec: `usage-direction` (FC3)

**Module id:** `usage-direction` · **Program:** [roster-balance](../roster-balance-map.md) · **Build order: 2 of 2**
**Depends on:** FC1 `usage-stats` · **Model calls: none**

## Objective

**Make the existing generation pipelines read real usage history.** FC1 measures that 39 of 98
families have never been picked and the top 10 hold 45.1% of every real pick. This module is what
turns that measurement into a bias on the *next* round, so coverage improves by construction instead
of by hoping the model spreads out — which measurement already proved it doesn't do on its own.

## Design

### What actually changes, and how little

`general_propose`/`family_propose`/`signature_propose` already have a proven wiring point for
exactly this shape: `family_glossary` (built 2026-09-05, the fix that moved unresolved 60%→13.3% by
adding semantic labels to each family option) is threaded as an optional keyword through
`build_context` → `sample_draft` → `propose_*_action` → `generate_*_actions.regenerate()`, defaulting
to `None` and proven byte-identical when omitted. `usage_weights` is added as a sibling parameter,
identically threaded, identically optional.

```text
today:   corpus -> briefs (full 98-family pool, unweighted) -> model picks -> hope evens out  [it didn't]
after:   usage-stats (real history) -> briefs (pool rendered with a weight per family) -> model picks -> evens out by construction
```

### Direction is a bias, not a cage — the one rule that must never break

An under-used family is **rendered more prominently / offered with a stronger hint**, never made the
only option and never excluded. The full 98-family (or brief's own allowed subset) pool is always
present. A single-option pool would turn generation into a template, which destroys the one thing a
model is actually better at than a lookup table — and it is unnecessary here, because the corpus
itself measures healthy (§0 of the map): there is no bad family to filter *out*, only under-exposure
to correct.

**This also means `allowedAtomFamilies` itself never changes.** `spec-distribution-planner.md`
constraint 4 (the C1 tier-widening gate) forbids narrowing that set per tier — and this module never
touches the set at all, only how prominently members of it are presented, so the constraint is
structurally out of reach rather than merely respected.

### The weight, computed from real history, never invented

```python
# per-mille; a family with zero picks gets the ceiling, a family already at the target share gets
# the floor -- linear between them, clamped, integers only.
def weight_milli(family: str, usage: "Mapping[str, int]", target_share_milli: int) -> int:
    observed_milli = (usage.get(family, 0) * 1000) // max(1, sum(usage.values()))
    if observed_milli >= target_share_milli:
        return FLOOR_WEIGHT_MILLI
    deficit_milli = target_share_milli - observed_milli
    return FLOOR_WEIGHT_MILLI + deficit_milli   # more deficit -> stronger boost, linear
```

`target_share_milli` is `1000 // 98` (the flat-uniform share) by default — deliberately the simplest
honest target, not a hand-tuned curve. **What would overturn it:** real evidence that a flat target
under- or over-corrects, which FC1's own regression report would show on the next round.

### It renders as emphasis, not as a hidden multiplier

The model never sees a number. Mirroring the `family_glossary` precedent exactly (which rendered
`id: Name [tag] -- effect` instead of a bare id), a weighted family renders with an explicit,
human-readable cue for the model — e.g. a short "underused, consider this" marker on families above
the median weight — so the bias is legible in the prompt itself, not a silent sampling trick outside
it. This keeps the technique consistent with this program's own established, measured-effective
pattern rather than introducing a new mechanism.

### Feedback: a round's own outcome becomes next round's input

FC1's *latest-round* usage count (not all-time) is what feeds the next round's weights — an
unresolved brief or a dedup-rejected candidate contributes nothing to usage, which already, correctly,
reads as "this family's exposure did not convert" without needing a separate signal.

### ⛔ CORRECTED 2026-09-06 — what "zero-cost verification" can and cannot prove here

An earlier draft of this section claimed the gain could be proven by "replaying the recorded
`samplePicks` under weighting" — copied from the vote-aggregation fix's own real technique without
checking whether it transfers. **It does not, and the difference matters.** The vote fix only
changed how *already-collected* model outputs were combined — the raw samples were fixed historical
facts, so replaying the aggregation rule over them was a genuine test of the new rule. This module
changes the **prompt shown to the model**, which happens *before* generation. There is no function
that maps "a differently-worded prompt" to "what the model would have answered" without actually
calling it — recorded `samplePicks` are answers to the *old* prompt and replaying them proves
nothing about a new one.

**What zero-cost verification can honestly prove, and what it cannot:**

- **Provable for free, using FC1's real usage report:** the weighting function assigns real,
  correct weights against real data — every one of the 39 real never-used families gets the
  ceiling weight; the real top-10 concentrated families get the floor. This is a correctness proof
  of the *mechanism*, not an outcome prediction.
- **Not provable without a real run:** whether the model actually diversifies its picks once shown
  the weighted prompt. That is an empirical question about model behavior and belongs with the
  plan's own already-deferred *"a real, full generation re-run under FC3 direction"* — it was never
  something this module could prove for free, and claiming otherwise would be exactly the kind of
  unmeasured claim this program's own history keeps correcting.

## Commands

```powershell
python -m seedsmith actions usage-weights                # today's weight table, from real history
python -m seedsmith actions propose --directed general   # generate using current weights
python -m pytest tools/seedsmith/tests/test_usage_direction.py
python -m pytest tools/seedsmith/tests/test_distribution_planner.py   # must stay green, untouched
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/actions/usage_direction/weights.py   new — weight_milli + rendering
tools/seedsmith/seedsmith/adapters/actions/general_propose/prompts.py   edit — optional usage_weights param
tools/seedsmith/seedsmith/adapters/actions/family_propose/prompts.py    edit — same
tools/seedsmith/seedsmith/adapters/actions/signature_propose/prompts.py edit — same
tools/seedsmith/tests/test_usage_direction.py                           new
```

## Testing strategy

| Test | Asserts |
|---|---|
| `omitting_usage_weights_is_byte_identical_to_today` | the optional-parameter contract, proven not argued — mirrors the `family_glossary` precedent's own test |
| `a_zero_usage_family_gets_the_ceiling_weight` | one of the 39 real never-used families, checked by name |
| `a_family_at_or_above_target_share_gets_the_floor_never_negative` | weights never go below the floor |
| `the_full_98_family_pool_is_always_present_never_narrowed` | `allowedAtomFamilies` unchanged with weighting on |
| `weights_render_as_a_legible_cue_never_a_bare_number` | the model-facing text never leaks a numeric weight |
| `⛔ allowedAtomFamilies_is_never_touched_so_constraint_4_cannot_be_violated` | direct equality check against the unweighted rendering's family set |
| `test_distribution_planner.py_passes_unchanged` | including its own planted-violation case |
| `every_real_never_used_family_gets_the_ceiling_weight_against_FC1s_real_report` | the honest zero-cost proof — mechanism correctness against real data, not a claim about model behavior |
| `the_real_top_10_concentrated_families_get_the_floor_weight` | the same, from the other direction |
| `computing_weights_twice_from_the_same_report_produces_identical_weights` | determinism |
| `a_dedup_rejected_candidate_does_not_count_as_usage` | only genuinely accepted, surviving content updates the signal |

## Boundaries

**Always:** keep the parameter optional and inert by default; render weight as legible text, never a
raw number; keep the full pool present.

**Ask first:** changing the target share away from flat-uniform — that is a real design call once
real data suggests it, not a default to guess at now.

**Never:** narrow `allowedAtomFamilies`; make any family the sole option; expose a numeric weight to
the model; use all-time usage where latest-round is correct (or vice versa); call a model to compute
a weight.

## Success criteria

- [ ] With no `usage_weights` supplied, output is byte-identical to today.
- [ ] A named test proves `allowedAtomFamilies` is never touched, so constraint 4 cannot be violated
      by construction (not merely by discipline).
- [ ] `test_distribution_planner.py` passes unchanged, including its planted-violation case.
- [ ] Every one of the 39 real never-used families receives the ceiling weight when weighted
      against FC1's own real report — the honest zero-cost proof of mechanism correctness.
- [ ] Whether the model's actual picks diversify once shown the weighted prompt is explicitly
      **not** claimed to be proven here — it requires a real run, already tracked as deferred.
