# Spec: `affix-authoring`

**Module id:** `affix-authoring` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 9 of 10 · the only expensive stage
**Depends on:** `affix-schema` (module 1), `patron-absorption` (module 6) · **Model calls: yes**

## Objective

The seedsmith pipeline for **named, multi-atom, slotted** affixes — *"Master of Fire and Ice"* — the
half `affix-library` (module 3) deliberately does not generate, because a bundle's identity is a
judgement, not a derivation (Q9: *"single-family affixes rule-generated from the atom library;
multi-atom named affixes LLM-authored, because their identity is a judgement"*).

**Late on purpose.** By the time this module runs, the schema (1), resolver (2), single-family library
(3), producer (4) and both absorptions (5, 6) are proven. This is the only stage in the whole program
that spends real model calls, and it spends them last, against a fully proven mechanism.

## Design

### Reuse the pipeline shape this session already built and proved for real, per A6

`effect-pipeline-ideal.md` A6's own warning: *"the risk is that feature two forks feature one's prompt
structure, affinity vocabulary and validators... duplication one layer up from where the shared SDK
just removed it."* `demon-seed`'s `classify-pipelines` (module 7) already built and — as of 2026-09-02
— **proved against real LM Studio calls** the exact machinery this module needs:

| Piece | Already built, where |
|---|---|
| local-model transport, self-heal on a named defect, no-silent-drop fallback | `seedsmith.pipeline.llm_caller.call_with_self_heal` (ported from a sibling project's production translate pipeline, generalized 2026-09-01) |
| option permutation + 3-way majority vote on load-bearing fields | `seedsmith.adapters.demons.anchor.permute`/`vote` — wired into a real classification loop 2026-09-02, proven against real species with genuine recorded disagreement |
| constrained decoding via JSON Schema | `llm_caller.call_model(..., schema=...)` — measured against `google/gemma-4-26b-a4b-qat`: unconstrained fails on a hostile prompt, constrained returns clean conforming JSON at no latency cost |
| run-control: pause/resume/cancel/rerun/overwrite-all over a long batch | `seedsmith.adapters.demons.run.runner` |

**This module's own pipeline is a new adapter over that same machinery**, the same relationship
`demon-seed`'s own `classify-pipelines` has to `llm_caller` — not a fork, not a re-implementation.

### What the model picks — and what it must never pick (P1, restated)

**⛔ CORRECTED 2026-09-05 — the table below understated how far the other two modules had already
settled this.** The original table named four model-picked rows. Read against the real, later, binding
decisions in modules 1 and 8 (re-verified against live code during `content-stack-todo.md`'s ep-9
pass), rows 2-4 are not open picks this module still owes — each is decided, and decided to a specific
home that is *not* this module's own output:

| The model picks | Why not |
|---|---|
| the affix's **name** and which atom refs it bundles | *(this module's own scope — unchanged, shipped)* |
| ~~which slots the bundle declares, and their domain~~ | the runtime shape is real (`AffixRefRow`, `Resolver.ResolveSlots`), but the only real domain vocabulary (`RpgStore.Containers.cs`'s `DomainMembers`) is hardcoded to `element` alone, and zero shipped content anywhere uses a slot — a model would be inventing a pattern string with no exemplar, exactly the guess P1 forbids. Genuinely unbuilt; needs an eligible-slot-pattern registry that does not exist yet, not a pick from this module |
| ~~an ordinal affinity per candidate affix: `core` / `likely` / `occasional`~~ | shipped, but as a property of a **(container, affix) pairing**, owned by whichever feature pipeline draws a shared affix (`demon-seed`'s `species-effects`, `tools/seedsmith/seedsmith/adapters/demons/effects/schema.py`) — not a property of the affix entity this module produces container-agnostically. Attaching one hardcoded affinity here would fight every feature's own per-container affinity for the same shared bundle |
| ~~eligibility TAGS to attach (module 8 consumes them)~~ | **module 8 decided the opposite direction** (`spec-eligibility-tags.md` §"tags are DERIVED from the affix's refs... no schema change, no new authoring field") — shipped as `AffixTags.Of`. An authored `tags` field here would contradict that binding decision |

`affix_class` and every magnitude remain table-derived exactly as the original row 1 said; the removed
rows never needed a "the tables pick" counterpart because there is no authored counterpart to pick.

Seedsmith's founding P1 binds exactly as hard here as everywhere else in this repo's seed pipelines: *"a
model has no calibrated sense of scale, so a number it picks is a plausible-looking guess that survives
review because nothing looks wrong with it."* The model names an affix and picks its refs; it never
writes a weight, a tier bound, or a value range.

### Voted fields

Following `demon-seed`'s own Q25 precedent (permute everywhere, vote the load-bearing few): the affix's
**name/identity** and its **ref bundle composition** are the two judgement calls with the highest cost
of being wrong (a bad name ships forever in a player-facing string; a bad bundle composition is a
schema-legal but thematically nonsensical affix) — both are 3-way voted, same machinery, same
`resolve_vote` semantics (3-0 high, 2-1 split with minority recorded, 1-1-1 unresolved, never the first
sample by default).

## Commands

```powershell
python -m seedsmith effects generate --kind affix --pipeline <id> --dry-run
python -m seedsmith effects run start --all
python -m pytest tools/seedsmith/tests/test_affix_authoring.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/effects/affix/prompts.py     new — the affix judgement + schema,
                                                                   same PipelineSpec shape as
                                                                   anchor/prompts.py
tools/seedsmith/seedsmith/adapters/effects/affix/derive.py       new — affix_class derivation from
                                                                   the bundle's refs, mirroring
                                                                   anchor/derive.py's own pattern
tools/seedsmith/seedsmith/workflow/graphs/effect_affix.py        new — build_pipeline_graph reused
                                                                   verbatim from demon_anchor.py's
                                                                   own shape
data/seed/effects/affixes/*.json                                 new — authored output, seed-contract
                                                                   canonical serialization
tools/seedsmith/tests/test_affix_authoring.py                    new
```

## Code style

```python
# One pipeline SHAPE, many content pipelines (A6). This reuses call_with_self_heal / permute / vote
# / run.runner verbatim - the same relationship demon-seed's classify-pipelines already has to
# llm_caller, proven end to end against real calls 2026-09-02.
from ....pipeline.llm_caller import call_with_self_heal
from ...demons.anchor.permute import order_for
from ...demons.anchor.vote import VOTED_FIELDS, resolve_vote
```

## Testing strategy

| Test | Asserts |
|---|---|
| `affix_class_is_derived_never_authored` | same P1/seed-contract §2.1 discipline as every other affixClass check in this program |
| `named_bundle_composition_is_3_way_voted` | reuses `resolve_vote`, proven with a real fixture split |
| `a_1_1_1_split_on_bundle_composition_resolves_unresolved` | never silently the first sample |
| `no_magnitude_ever_appears_in_authored_output` | the numeric-smuggling audit, same five cases `anchor-contract` already proves |
| `pipeline_shape_matches_demon_seeds_classify_pipelines_exactly` | A6's own regression guard — a structural diff test, not a vibe check |
| `zero_bare_HTTP_calls_outside_llm_caller` | grepped, matching `llm_caller.py`'s own dependency-isolation test convention |

## Boundaries

**Always:** reuse `llm_caller`/`permute`/`vote`/`run.runner` verbatim; derive `affix_class`, never
author it; run a real `preflight` before any real batch, exactly like `demon-seed`'s own gate.

**Ask first:** forking any piece of the reused machinery instead of extending it — per A6, that is the
exact duplication this module exists to avoid.

**Never:** let a model author a weight, a tier bound, or a value range; skip the small-batch quality
checkpoint before a full run — the same discipline this session's own `demon-seed` real-run proof
established (evaluate a small diverse batch's vote/disagreement signal before committing to the full
corpus).

## Success criteria

- [ ] The pipeline shape is a verified structural match to `classify-pipelines`, not a parallel
      implementation.
- [ ] Every authored affix passes the numeric-smuggling audit.
- [ ] `affix_class` is derived for every authored bundle, proven by test.
- [ ] A real small-batch proof run (not the full corpus) demonstrates real vote signal before any
      larger commitment — the same checkpoint this session ran for `demon-seed`.
