# Spec: `species-effects`

**Module id:** `species-effects` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 15 of 16
**Model calls:** yes. **This is the module whose absence made every generated demon do nothing.**

## Objective

Turn each species anchor into a **`species-passive.{speciesId}` container seed** — the fixed core, the
affix pool, and the eligibility tags — so a generated demon has effects and not just a stat block.

Owner, 2026-09-01: *"we really miss these pipeline on our specie generator — we just generate demon
species without ship atom container for it … we will generate atom seed for specie **after** other
pipeline complete, because they need data from specie seed like family, rarity, favor, lore."*

## Design

### 1. Why it runs last, and why it cannot run alone

The pipeline is a **function of the anchor**, so it needs `anchor-emit`'s output. It also needs
something to choose *from*, and that belongs to a different program:

| Prerequisite | Owner |
|---|---|
| anchors — family, rarity, element, aptitude, lore | `anchor-emit` (module 8), this program |
| the ten-rung rarity enum | `rarity-migration` (module 10), this program |
| **an affix library, a slot mechanism, and a container schema** | **[effect-pipeline](../effect-pipeline-ideal.md)** — a different program |

**`demon-seed` cannot finish on its own**, and this is the module where that becomes true. Stated here
so a build session does not discover it at task start.

### 2. What the pipeline reads, and what each input constrains

| Anchor field | Constrains |
|---|---|
| `rarity` | the fixed-core band, `prefix_rolls`, `suffix_rolls`, and the `min_tier`/`max_tier` window — **entirely, and numerically** |
| `elementPrimary` · `elementSecondary` | which element variants a slot may bind to |
| `aptitudePrimary` · `aptitudeSecondary` | which channel families are thematically eligible — Might to power, Fortitude to mitigation, Vigor to shield |
| `posture` (derived) | a cross-check: a Bastion species drawing only offensive affixes is a defect |
| `resourceProfile` | which `resource.delta` families are legal at all |
| `family` · `traits` | the identity a family's members should visibly share |
| `flavorInfo` / lore | **the actual judgement** — what this creature does, in its own words |
| `threatBand` | **nothing.** It is a `Θ` offset, so it belongs to magnitude, not to membership |

That last row matters: a species is not made stronger here. Strength is `species-generator`'s, through
one `P(Θ)`. **This module decides *what*, never *how much*.**

### 3. What the model picks — and what the tables pick

| The model picks | A table picks |
|---|---|
| which affix families this species is eligible for | the fixed-core count band — from rarity |
| the slot bindings (which elements a slot may take) | `prefix_rolls` / `suffix_rolls` — from rarity |
| an **affinity ordinal** per affix: `core` · `likely` · `occasional` | the tier window — from rarity |
| the container's eligibility tags | the weight each affinity maps to; every magnitude |

**The affinity ordinal is the mechanism that keeps P1 intact.** A model may never write `weight: 40`,
but it can reliably say *"a fire drake's fire-power affix is **core**; its ice-resist is
**occasional**."* A tuning table turns three ordinals into three weights, so a balance pass retunes all
904 species with one file save rather than a regeneration run.

### 4. ⭐ `core` means the fixed core, not a heavy weight

From the ideal doc's adversarial review (A2), and it is a correctness rule, not a preference.

A weight cannot express *"always"*. If twelve affixes are marked `core` and rarity gives
`prefix_rolls = 3`, **nine of them never appear** — no error, container valid, and `core` has quietly
stopped meaning anything.

| Affinity | Lands in |
|---|---|
| `core` | `effect_container_atom` — **always present**, enforceable |
| `likely` · `occasional` | pool rows, guarded by the existing `pool_rolls ≤ distinct drawable groups` rule |

**The fixed core carries its own rarity band** (0-2 at the low rungs). Without it a rung-1 species could
hold five guaranteed effects while its pool band says 0-1. With it, a rung-1 species can still have
exactly one defining effect — which the pool-only reading made impossible.

### 5. Prefix and suffix are two budgets

`affixClass` is **derived, never authored** — `seed-contract.md` §2.1: *permanent-modifier kinds are
prefixes; triggered kinds are suffixes*, and *"present in a seed file → reject."*

So this module never writes a class. It does have to respect two budgets, and per Q8 a species passive
is **prefix-heavy**: the prefix side is what the race *is*, the suffix side is what it *does*. A species
with no suffixes is a stat block; one with no prefixes is a gimmick. The band table says which, per rung.

**A mixed-class affix bundle consumes one of each budget** (ideal A1). This module must count it that
way or a species silently over-fills one side.

### 6. Output — a seed, so it holds no numbers

```text
data/seed/demons/species-effects/
  plant/<family>.json      container seeds, grouped and sorted like the anchors
  zombie/<family>.json
```

Each entry: `container_id`, `container_kind: species-passive`, tags, the fixed-core affix ids, the pool
affix ids with their affinity ordinals, and slot declarations. **No weight, no tier, no magnitude, no
`pool_rolls`** — every one of those is derived from rarity by `species-generator` or resolved at roll
time. The `anchor-contract` numeric audit applies to this schema unchanged.

Provenance mirrors `anchor-emit`: anchor hash, prompt version, affinity record. A rerun over unchanged
anchors is byte-identical, proven by hash.

## Commands

```powershell
python -m seedsmith demons effects --species <id>          # one species
python -m seedsmith demons effects --all                   # via run-control
python -m seedsmith demons effects --dry-run               # render prompts, call nothing
python -m seedsmith demons effects --check                 # exit 1 if the tree would change
python -m pytest tools/seedsmith/tests/test_species_effects.py
```

## Project structure

```text
tools/seedsmith/seedsmith/workflow/graphs/species_effects.py   the graph
tools/seedsmith/seedsmith/adapters/demons/effects/prompts.py   prompt bodies
tools/seedsmith/seedsmith/adapters/demons/effects/schema.py    the container-seed schema
data/tuning/demon-species-effects.v1.json                      affinity->weight, core band, roll bands
data/seed/demons/species-effects/**                            committed output
```

Reuses `run-control` and `option-permutation` rather than adding a second runtime beside them.

## Code style

Match `workflow/graphs/commander_effect.py`: one graph, nodes named for what they do, validators
registered rather than inlined.

## Testing strategy

| Test | Asserts |
|---|---|
| `no_numeric_field_survives_the_audit` | the seed holds no weight, tier or magnitude |
| `core_affinity_lands_in_the_fixed_core` | A2, mechanically |
| `fixed_core_respects_its_rarity_band` | a rung-1 species cannot carry five guaranteed effects |
| `mixed_bundle_counts_against_both_budgets` | A1 |
| `posture_conflict_is_repaired_naming_the_conflict` | a Bastion species drawing only offence |
| `resource_family_illegal_outside_resourceProfile` | rejected, not silently dropped |
| `threatBand_does_not_influence_membership` | strength stays `species-generator`'s |
| `rerun_over_unchanged_anchors_is_byte_identical` | idempotency by hash |
| `dry_run_makes_zero_calls` | transport stub raises |

Every test stubs the transport — `test_offline_guarantee.py`'s discipline applies.

## Boundaries

**Always:** read the anchor; keep the seed numeric-free; map `core` to the fixed core; count a mixed
bundle against both budgets; record provenance.

**Ask first:** widening the affinity vocabulary beyond three ordinals; changing the fixed-core band.

**Never:** let the model pick a weight, a tier or a magnitude; write `affixClass`; let `threatBand`
change what a species carries; ship before `effect-pipeline` provides the affix library.

## Success criteria

- [ ] Every species emits a `species-passive.{speciesId}` container seed.
- [ ] The numeric audit finds nothing in the emitted tree.
- [ ] A `core` affix always appears on the rolled instance, proven by test.
- [ ] A rung-1 species carries at most its banded fixed core.
- [ ] A rerun over unchanged anchors is byte-identical.
