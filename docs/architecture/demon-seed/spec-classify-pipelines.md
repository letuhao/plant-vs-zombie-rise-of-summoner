# Spec: `classify-pipelines`

**Module id:** `classify-pipelines` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 7 of 16
**Model calls:** yes — this is the only module that makes them.

## Objective

Eight pipelines, each owning **one judgement**, that read a species' captured lore and fill the
classified attributes of the anchor.

Owner, Q22: *"each pipeline must cover 1 or some attributes."*
Owner, Q25: eight pipelines, three-way vote on five fields.

## Design

### 1. Why eight rather than one

Ideal §4.7 and Q22 agree: a pipeline answering fifteen attributes at once is measurably less reliable
than one answering a single well-described attribute. The decomposition rule is **one shared
judgement per pipeline** — not "one attribute per pipeline", which would split `reach` from
`attackTempo` even though a single reading of the lore decides both.

| # | Pipeline | Attributes | The judgement it makes |
|---|---|---|---|
| 1 | `element-primary` | `elementPrimary` | *what is this creature made of / aligned with?* |
| 2 | `element-secondary` | `elementSecondary` | *is there a real second nature, or is this pure?* |
| 3 | `aptitude-primary` | `aptitudePrimary` | *what is it good at?* |
| 4 | `aptitude-secondary` | `aptitudeSecondary` | *what is its supporting strength, if any?* |
| 5 | `threat-audit` | `threatBand` | *does the number's rung match the lore?* — and supplies the rung outright when `basis` is `inferred` |
| 6 | `deployment` | `deployMode`, `acquisition` | *how does this creature enter play?* |
| 7 | `kit-shape` | `attackTempo`, `reach`, `targetPreference`, `resourceProfile` | *how does it fight?* |
| 8 | `identity` | `family`, `traits`, `variants`, `rarity` | *what kind of thing is it, and how special?* |

**Element and aptitude are split primary/secondary deliberately.** Asking one call for both invites
the model to pick a secondary that justifies the primary it just chose, which manufactures dual typing
where none exists. Two calls, and the secondary call is shown the primary as context but is explicitly
told `none` is a full answer.

### 2. What every pipeline is given, and what it is never given

**Given:** `displayName`, `flavorInfo`, `flavorIntroduce`, `side`, the enrichment description, and —
where the pipeline needs it — the `basis` and any already-decided anchor fields it must stay
consistent with.

**Never given:** raw `hp`, `attack`, `armor` numbers. A model shown "hp: 3000" starts reasoning about
magnitude, and it has no calibrated sense of scale — seedsmith's P1, the founding rule of the whole
tool. `threat-audit` is the single exception and it is shown a **rung name**, never a number (§4).

### 3. `threat-audit` — the number wins, and the model checks it

Q16: *"Number wins, and the LLM audits the result."*

The pipeline is shown the species' lore and the rung `threat-band` computed, phrased as a yes/no
judgement with a reason:

```text
This creature was measured as a "warden" (rung 5 of 10, where 1 is a nuisance and
10 is a calamity). Does its description support that? Answer agree / too-low / too-high.
```

| Verdict | Effect |
|---|---|
| `agree` | the computed rung stands |
| `too-low` / `too-high` | **the computed rung still stands**, and the disagreement is recorded |

**The audit does not override.** It produces a review queue — seedsmith's P3 open-loop rule: a metric
that cannot verify its own fix produces a queue, never a pass. A systematic pile of `too-low` verdicts
in one score range is a signal to retune `demon-threat.v1.json`, which is a balance edit a human makes
once, not a per-species override the model makes 904 times.

**Where `basis` is `inferred` or `blocked`**, there is no computed rung to audit, and this pipeline
instead *chooses* the rung from lore (Q26). Its output carries `basis: inferred` so nothing downstream
mistakes a read for a measurement.

### 4. Cross-field validation, and repair rather than reject

Q12: *"Reject and repair, naming the conflict."*

| Validator | Rule | Repair |
|---|---|---|
| `posture-resource` | `posture` is derived from `aptitudePrimary`; a Bastion demon whose `resourceProfile` omits `poise` is incoherent | re-prompt pipeline 7 naming the conflict |
| `element-distinct` | `elementSecondary` may not equal `elementPrimary` | re-prompt pipeline 2; `none` is an acceptable answer |
| `pure-flag` | if both aptitudes share a posture, `pure` is set — this is a flag, **not** a rejection (Q2) | none; it is a label |
| `variant-count` | the count of `variants` comes from `rarity`'s count band, not from the model | truncate or extend deterministically |
| `acquisition-nonzero` | `DemonAcquisition.None` is a catalog error (`DemonRarity.cs:11`) | re-prompt pipeline 6 |
| `family-open` | a new `family` value is allowed and recorded, never rejected | none — the axis is open by construction |

A repair re-prompts **naming the specific conflict**, using the existing `call_with_self_heal` path,
whose own docstring states the reason: *"a bare retry teaches the model nothing; naming the reason is
what fixes it."* Two repairs, then the field is `unresolved` and reported — an infinite repair loop is
how a run silently costs ten times its budget.

### 5. Language

Prompts are English; the source lore is Chinese. **The lore is passed through verbatim, never
translated first** — a translation step would be a second model call inserting its own errors upstream
of every judgement, and the existing `language.py` validator already covers output-language rules.

Output values are always the contract's ASCII enum ids, enforced by constrained decoding, so no
language ambiguity reaches the anchor.

### 6. Transport

`seedsmith.pipeline.llm_caller`, unchanged: `response_format: json_schema`, reasoning disabled,
`temperature` fixed per pipeline, `call_with_self_heal` for the repair path. `dump-preflight` check 6
has already proven the schema is actually enforced before any of this runs.

**Nothing here calls a model at game runtime.** Seedsmith is a development tool (Q10); the game never
sees it.

## Commands

```powershell
python -m seedsmith demons generate --pipeline element-primary --species <id>   # one species, one pipeline
python -m seedsmith demons generate --all                                        # the full run, via run-control
python -m seedsmith demons generate --dry-run                                    # render prompts, call nothing
python -m pytest tools/seedsmith/tests/test_classify_pipelines.py
```

`--dry-run` renders every prompt without calling — the cheapest way to review a description change
across 904 species before spending 14 hours on it.

## Project structure

```text
tools/seedsmith/seedsmith/workflow/graphs/demon_anchor.py     the eight graphs
tools/seedsmith/seedsmith/adapters/demons/anchor/prompts.py   prompt bodies, one per pipeline
tools/seedsmith/seedsmith/workflow/validators/anchor.py       the cross-field validators
tools/seedsmith/tests/test_classify_pipelines.py
```

Reuses the existing `workflow/` runtime — graphs, nodes, checkpointing, validators — rather than
adding a second execution model beside it.

## Code style

Match `workflow/graphs/commander_effect.py`: a graph per judgement, nodes named for what they do,
validators registered rather than inlined.

## Testing strategy

Every test stubs the transport. **No test in this suite calls a real model** — the existing
`test_offline_guarantee.py` discipline applies.

| Test | Asserts |
|---|---|
| `no_pipeline_receives_a_raw_magnitude` | greps the rendered prompt for `hp`/`attack` values — P1, mechanically |
| `threat_audit_sees_a_rung_name_never_a_number` | the one exception stays an exception |
| `audit_disagreement_does_not_change_the_rung` | Q16's "number wins" |
| `inferred_species_gets_a_rung_and_keeps_basis_inferred` | Q26 |
| `secondary_element_none_is_accepted_not_repaired` | the pure case is legal |
| `posture_resource_conflict_repairs_with_the_conflict_named` | Q12 |
| `repair_stops_after_two_attempts` | the runaway-cost guard |
| `new_family_value_is_recorded_not_rejected` | the open axis stays open |
| `dry_run_makes_zero_calls` | proven by a transport stub that raises |

## Boundaries

**Always:** one judgement per pipeline; pass lore verbatim; name the conflict when repairing; stop
after two repairs; keep every prompt free of magnitudes.

**Ask first:** merging or splitting a pipeline (it changes the call budget and the reliability
profile); changing a prompt's description text after a full run has been emitted, since it invalidates
provenance.

**Never:** let a model pick a magnitude; let the audit override the computed rung; translate the lore
first; call a model from anything the game runs; retry without naming the reason.

## Success criteria

- [ ] Eight pipelines, each with a single stated judgement.
- [ ] No rendered prompt anywhere contains a captured magnitude, proven mechanically.
- [ ] A `too-high` audit verdict leaves the rung unchanged and appears in the review queue.
- [ ] A posture/resource conflict repairs by naming the conflict, and gives up after two tries.
- [ ] The whole suite passes with the transport stubbed to raise on any real call.
