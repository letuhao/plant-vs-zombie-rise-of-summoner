# Spec: `fusion-recipe-generator`

**Module id:** `fusion-recipe-generator` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 17 of 18 (new)
**Model calls:** yes, bounded to gap species only. **Depends on:** `species-generator` (11).

## Objective

Move fusion-recipe assignment out of `DemonRecipeCatalog.Build()`'s in-process, no-backtracking
algorithm and into the same seed → concrete discipline every other demon-seed artifact already
follows: a deterministic pass does the vast majority of the work for free, an LLM is asked only to
fill the specific gaps the deterministic pass cannot mathematically close, and a deterministic
reconciler is the only thing with write authority over the committed output.

Owner, 2026-09-05, found running the real `catalog-runtime` flip: *"make new seedsmith pipeline for
fusion with deterministic engine read the whole demon species seed and make distribution index... fill
fusion hole gap by LLM propose the recipe, deterministic engine validate and reconcile... then finally
output fusion seed and we will load it in the game runtime."*

## The defect this replaces

`spec-demon-fusion.md` (shipped 2026-08-21) locked recipe generation as **"code-generated from the
species catalog"** — a single deterministic pass, `DemonRecipeCatalog.Build()`, assigning each
summonable rare-or-better species exactly one recipe from the nearest populated rarity rung below it.
This held at 84 species because no rung ever had more OUTPUTS than its input rung had CAPACITY for
distinct unordered pairs.

**It stopped holding at 829 species, found live, not by inspection:** running the real store-backed
catalog for the first time, `DemonRecipeCatalog.Build()` threw `InvalidOperationException: No unused
input pair available for 'jacksonzombie'`. The real distribution, queried directly from
`dist/FusionRpg.Server/data/rpg-hot.sqlite`:

| Rarity (ladder order) | Count | Nearest populated rung below | Below count | Max distinct pairs `C(n,2)` |
|---|---|---|---|---|
| Sunwoven | 4 | Firstseed | 37 | 666 |
| **Almanac** (top rung) | **21** | **Sunwoven** | **4** | **6** |

21 top-rung outputs need 21 unique input pairs; 4 candidate inputs support at most 6. **This is a
genuine capacity ceiling, not a search-order defect** — verified by first fixing the search order
(backtracking across every `a` candidate before giving up) and confirming the same output still fails,
for the reason the numbers above already show: no ordering of 4 elements produces more than 6 pairs.

**This is also the exact defect class `roster-metrics` (module 10) already exists to catch** — its own
declared target is *"rarity distribution: monotone decreasing across the ten rungs"* (§2 of that spec).
`Almanac (21) > Sunwoven (4)` is a non-monotone rung, the same shape as that spec's own worked example
("rung 7 commoner than rung 4"). `roster-metrics` runs on **anchors**, before rarity is assigned by
expansion, so it could not have caught this specific case even if it had been run and gated — rarity is
computed during `species-generator`, not stated on the anchor. **This module's own distribution index
(§1 below) is the same check, moved to run where the information the check needs actually exists: after
expansion, not before it.**

## Design

### 1. `distribution-index` — deterministic, no model calls

Reads every concrete species (`data/generated/demons/*.json`, `species-generator`'s own committed
output — the same input `species-import` reads, never the live database). For every rarity rung at or
above `DemonRecipeCatalog.OutputEligibilityFloor` (`Cultivated`):

- the output count at this rung (species with `Acquisition != CaptureOnly`)
- the nearest populated rung below (mirrors `DemonRecipeCatalog.InputPoolBelow`'s own walk-down —
  reused directly, not reimplemented, via a thin CLI wrapper over the real C# method so the index and
  the runtime algorithm can never silently disagree about which rung is "below")
- that rung's own count, and `C(n,2)`
- **shortfall** = `output count > C(n,2)`

Output: a report (`fusion-distribution-index.json`, uncommitted — a diagnostic, not a seed) naming
every shortfall rung, its output count, its input pool, and the deficit (`output count − C(n,2)`).

**A run with zero shortfalls exits 0 and produces no further pipeline stages** — `fusion-recipe-propose`
never runs an LLM over a roster that does not need one. This is the same "closed-loop first, spend a
model call only where determinism cannot close the gap" discipline `roster-metrics` §4 already commits
this program to.

### 2. `fusion-recipe-propose` — LLM, bounded to shortfall rungs only

For each shortfall rung, the deterministic pass (§3) has already assigned every pair the pool's
capacity allows (`C(n,2)` recipes, filled by the existing `DemonRecipeCatalog` search order — reused
as a library call, not reimplemented). The **deficit** — outputs still needing a recipe — is what goes
to the model. **Which specific outputs land in the deficit set is itself a pinned, deterministic fact,
not an accident of iteration order** — see §3 step 1's own note on this.

The LLM is never asked to invent a pairing from nothing, and it is never shown an unbounded corpus. It
is shown:
- the deficit output species (id, element, rarity)
- **the nearest 2–3 *populated* rungs below the output's own rung** (walking down the same way
  `InputPoolBelow` already does, just not stopping at the first rung with ≥2 candidates) — for the
  real corpus's one known deficit (Almanac), that is Sunwoven ∪ Firstseed ∪ Heirloom, ~45 species, not
  the ~808-829-species remainder of the whole roster. Bounding this is not an optimization; an
  unbounded prompt is unauditable and untunable, the same reason `classify-pipelines`' own prompts are
  scoped per-field rather than shown the whole anchor schema at once.
- **`acquisition` alongside id/element/rarity for every shown candidate** — omitting it invites the
  model to propose a `CaptureOnly` species, which §3 step 2 refuses unconditionally regardless (owner
  lock 6). Showing it is a cheap way to not waste a proposal on a candidate that can never survive
  reconciliation.
- every pair already claimed (so it cannot propose a duplicate)

And asked to propose ONE cross-rung or reused-tier pair per deficit output, with a one-line rationale
(matching `classify-pipelines`' own `reason` field convention). **The model never picks a magnitude and
never decides validity** — it proposes a candidate pair; §3 is the only authority on whether that
candidate survives.

Schema: `speciesId` (existing, validated against the real roster, matching `anchor-contract`'s own
closed-enum discipline), `inputA`, `inputB` (existing speciesIds, unordered for voting purposes — see
below), `reason` (free text, logged, never gates anything).

**Voting is per-member, not whole-pair — `option-permutation`'s `resolve_vote` is the wrong precedent
for this, `resolve_set_vote` is the right one, and the difference is measured, not theoretical.**
`resolve_vote`'s whole-value `Counter` equality is built for a closed handful of enum strings
(elementPrimary, rarity, …); a fusion pair is drawn from a candidate pool of dozens of species, and
three independent samples agreeing on the exact same *unordered pair* is combinatorially unlikely at
that width — this repo already measured the analogous failure for whole-*set* agreement over a
~98-option pool ("a 40–55% unresolved rate that five rounds of prompt work could not move: the ceiling
was in the aggregation, not the model," `anchor/vote.py`'s own `resolve_set_vote` docstring) before
fixing it with per-member majority. This module reuses that fix, not the one that already failed at
smaller scale:

1. **Canonicalize each sample's pair** to an unordered, sorted tuple before any comparison — a sample
   proposing `(A, B)` and another proposing `(B, A)` are the same vote, not a disagreement.
2. **Tally per candidate species** across the three canonicalized samples (mirrors
   `resolve_set_vote`'s member-level `Counter`, applied to the two-element set each sample proposed).
3. A species enters the resolved pair when at least 2 of 3 samples proposed it. If fewer than exactly
   two species clear that threshold, the deficit is **unresolved** — reported by name, never filled by
   sample 0's raw pick (the same rule `resolve_set_vote` already enforces for the identical reason).
4. **Assign `inputA`/`inputB` from the resolved, unordered pair the same way `DemonRecipeCatalog`
   already assigns them for every other recipe** — the candidate matching the output's primary element
   becomes `A` (ties broken by `SpeciesId` ordinal, `TryFindPair`'s own existing tie-break), the other
   becomes `B`. No new assignment rule invented for gap-fills; A/B carries no gameplay asymmetry either
   way (`spec-demon-fusion.md` lock 5's "pick one guaranteed trait from any input" is symmetric), so
   this exists only so `Catalog_is_deterministic_and_ids_are_stable`-style tests keep meaning something.

### 3. `fusion-recipe-reconcile` — deterministic, no model calls, sole write authority

1. Runs the EXISTING `DemonRecipeCatalog.Build()` algorithm exactly as shipped (`TryFindPair`'s
   per-`a`-candidate backtracking, already fixed this session) to assign every recipe the deterministic
   pass can support. This handles all but the shortfall rungs' deficits — for the real corpus, **at
   least 808 of 829** eligible outputs (Almanac's own `C(4,2)=6` pairs land inside this same
   deterministic pass, since `Build()` fills every pair a pool's capacity allows before giving up — the
   remaining ≤15 Almanac outputs are what reach the model). **Which specific outputs end up in that
   remainder is a pinned fact of `Build()`'s own existing iteration order (rarity, then `SpeciesId`
   ordinal) — reusing the real method, unmodified, is what makes this deterministic rather than an
   accident; a reimplementation that iterated in a different order would resolve a different subset
   and is exactly the drift this step exists to prevent.**
2. For each deficit output, takes §2's per-member-voted proposal (if the vote resolved to a pair — an
   `unresolved` vote skips straight to step 3 below) and validates it exactly as strictly as this same
   step already validates everything else:
   - both `inputA`/`inputB` exist in the real roster (`DemonSpeciesCatalog.IsKnown`)
   - neither is `Acquisition == CaptureOnly` (spec-demon-fusion.md's own owner lock 6, unconditional —
     an LLM proposal is not an exception to a locked decision)
   - the pair is not already claimed by any other recipe (deterministic OR another proposal) —
     `usedPairs` is one shared set across both sources
   - `inputA != inputB`
   - **the cross-rung relaxation is recorded, not hidden**: a reconciled recipe whose two inputs are
     NOT both at the single nearest-populated rung below the output (`DemonRecipeCatalog`'s own default
     rule) carries a `crossRungGapFill: true` flag in the committed seed — a human or a later audit can
     always tell a normal recipe from a gap-fill one without re-deriving the distribution index.
   - **Deficit outputs are processed in `SpeciesId` ordinal order** (the same tie-break every other
     iteration in this pipeline already uses) — this is what makes "which proposal wins a `usedPairs`
     collision between two different deficits" a pinned, reproducible fact rather than dependent on
     whatever order a Python dict or an LLM batch response happened to return results in.
3. A proposal that fails ANY check, or that never resolved a vote in §2, is refused and the output is
   reported as **still unresolved** — this module never invents a fallback pairing on a validation
   failure. An unresolved output ships with NO recipe (matching how
   `SpeciesBuildPlanCatalog.SharesFor`'s own "no entry, not a made-up one" rule already treats a
   genuinely-incomplete case) rather than a silently-wrong one.
4. Writes the canonical output (§4). Refuses the WHOLE write if any DETERMINISTIC-pass recipe is
   internally inconsistent (same discipline `species-import`'s own "one bad row refuses everything"
   rule already establishes for this exact program) — a reconciliation bug must never partially ship.

### 3a. Freeze on commit — a clean re-run must not reshuffle an already-resolved gap-fill

Every other LLM-touched artifact in this program freezes its answer once committed and only
re-derives when its real input changed (`anchor-emit`'s `_provenance.dumpHash` staleness check: *"an
entry with no `_provenance` at all [is always re-derived]; otherwise, re-derive only if the dump hash
moved or a prompt version bumped"*). This module carries the identical rule for the same reason: an
LLM call is not a pure function of its input the way every OTHER step in this pipeline is, so without
an explicit freeze, running `reconcile` twice against an *unchanged* roster could legitimately propose
a *different* valid pair for the same deficit — silently breaking the "same seed ⇒ same concrete
output" property this whole program is built on (`demon-seed-map.md` §1's own law: *"the seed is
generator input, not rows"*).

Each gap-fill entry in the committed seed (§4) therefore carries its own `_provenance`:

```json
"provenance": { "corpusContentHash": "sha256:...", "promptVersion": 1 }
```

`corpusContentHash` is a hash over the exact set of candidate species shown to the model for this
deficit (§2's bounded 2–3-rung pool) — **not** a hash of the whole 829-species corpus, so a change to
an unrelated species never invalidates an already-resolved gap-fill. On each `reconcile` run: an
existing entry whose `corpusContentHash` still matches and whose `promptVersion` is current is kept
verbatim, no model call made; only a genuinely-changed candidate pool or a bumped prompt version
re-derives that one entry. This is `anchor-emit`'s own re-derivation rule, applied one level up the
pipeline, not a new one.

### 4. The committed seed

```text
data/generated/demons/_fusion-recipes.json
```

Underscore-prefixed (matches `_species-build-plan.json`'s own convention: a single generated file, not
one per recipe — recipes are far smaller than full species stat blocks, so `species-generator`'s
one-file-per-species shape is the wrong precedent to copy here; `redistribution-plan`'s single-file
shape is the right one). Canonical, sorted, diffable — same serializer discipline as
`ConcreteSpeciesSerializer`/`SpeciesBuildPlanSerializer`.

```json
{
  "recipe.jacksonzombie": {
    "outputSpeciesId": "jacksonzombie",
    "inputSpeciesIdA": "...",
    "inputSpeciesIdB": "...",
    "crossRungGapFill": false
  },
  "recipe.somealmanacoutput": {
    "outputSpeciesId": "somealmanacoutput",
    "inputSpeciesIdA": "...",
    "inputSpeciesIdB": "...",
    "crossRungGapFill": true,
    "provenance": { "corpusContentHash": "sha256:...", "promptVersion": 1 }
  }
}
```

`provenance` is present ONLY on `crossRungGapFill: true` entries — a deterministic-pass recipe is a
pure function of the committed species corpus already (§3 step 1's own reused, unmodified method), so
it needs no freeze key of its own; adding one would be a second source of truth for a fact the corpus
content hash (module 12's own `species-import --check`) already covers.

### 4a. The cost model this module must not silently break

`FusionCostTable.Recipe(resultRarity)` (`StarPolicy.cs`) prices a recipe purely off its OUTPUT's
rarity — a fixed lookup, blind to which rung the inputs actually came from. Bounding the candidate pool
to the nearest 2–3 rungs (§2) keeps a `crossRungGapFill` recipe's real input cost in the same
neighborhood as a normal one, but does not make them identical — an Almanac recipe drawing from
Heirloom (two rungs down) still consumes measurably cheaper specimens than one drawing from Sunwoven
(one rung down) at the same Souls/shard/essence price. This is a real, if narrow, economy question
(the exposure is bounded to ≤15 recipes on the real corpus, all at the single top rung), not something
this module's own reconciler can resolve on its own authority — `spec-demon-fusion.md`'s own Boundaries
already name cost-table tuning as "Ask first: game balance." **Named here so it is decided once, not
discovered by a player finding the cheapest gap-fill recipe first:** either the fixed per-rarity price
stands as-is (the ≤15-recipe exposure is accepted as negligible), or `crossRungGapFill` recipes get a
rung-distance cost multiplier mirroring the promotion cost bump `spec-demon-fusion.md`'s own "Costs"
table already uses when a band jumps. Not this module's call; recorded so the call gets made.

## Commands

```powershell
dotnet run --project tools/DemonRecipeDistributionIndex           # §1, deterministic, no model calls
python -m seedsmith demons fusion propose                          # §2, model calls, only if §1 found a shortfall
python -m seedsmith demons fusion reconcile                        # §3+§4, deterministic, writes the seed
python -m seedsmith demons fusion reconcile --check                # compare against committed, exit 1 if stale
dotnet test tests/FusionRpg.Core.Tests --filter FusionRecipe
python -m pytest tools/seedsmith/tests/test_fusion_recipe.py
```

## Project structure

```text
tools/DemonRecipeDistributionIndex/Program.cs         §1 — thin CLI over the real DemonRecipeCatalog
                                                       search logic, reused not reimplemented
tools/seedsmith/seedsmith/adapters/demons/fusion/
  distribution.py                                     reads the index's JSON output
  schema.py                                            the proposal shape (§2)
  prompts.py                                           the bounded, deficit-only prompt
  vote.py                                               per-member majority over a canonicalized pair,
                                                         reusing anchor/vote.py's resolve_set_vote shape
                                                         (NOT resolve_vote — see §2's own reasoning)
  reconcile.py                                          §3 — calls into the same C# validation via a CLI seam
  emit.py                                               §4 — canonical serializer
data/generated/demons/_fusion-recipes.json             the committed seed
tools/seedsmith/tests/test_fusion_recipe.py
tests/FusionRpg.Core.Tests/Demons/Fusion/FusionRecipeReconcileTests.cs
```

## Code style

Match `anchor/vote.py` and `anchor/emit.py` exactly — this is the same pipeline shape with a narrower
scope (recipes, not full anchors), not a new pattern.

## Testing strategy

| Test | Asserts |
|---|---|
| `zero_shortfall_roster_never_calls_the_model` | §1's own closed-loop-first rule, mechanically |
| `deterministic_pass_alone_matches_todays_DemonRecipeCatalog_for_every_non_shortfall_output` | reconciliation does not change a single recipe that already worked |
| `the_deficit_set_is_the_same_across_two_independent_runs_with_no_roster_change` | §3 step 1's pinned-order claim, proven rather than assumed |
| `a_pair_proposed_as_A_B_and_B_A_by_different_samples_votes_as_agreement` | canonicalization happens before tallying, §2 |
| `two_of_three_samples_choosing_a_species_resolves_it_even_when_the_third_disagrees_on_the_partner` | the per-member rule actually differs from whole-pair equality — the exact scenario whole-value voting would call `unresolved` |
| `a_1_1_1_split_with_no_species_at_majority_is_unresolved_not_sample_zeros_pick` | mirrors `resolve_set_vote`'s own identical rule |
| `a_capture_only_proposed_input_is_refused_not_silently_dropped` | owner lock 6 binds an LLM proposal too |
| `a_duplicate_proposed_pair_is_refused` | uniqueness holds across both sources |
| `two_deficits_colliding_on_usedPairs_resolve_the_same_way_across_reruns` | §3's pinned `SpeciesId`-order fold, not incidental dict/batch order |
| `crossRungGapFill_is_set_only_on_a_reconciled_gap_fill_recipe` | the flag is honest, not decorative |
| `an_unresolved_deficit_ships_with_no_recipe_not_a_fabricated_one` | the honest-gap rule, mirrored from `SpeciesBuildPlanCatalog` |
| `a_rerun_with_an_unchanged_candidate_pool_never_calls_the_model_again` | §3a's freeze-on-commit rule, the reproducibility property itself |
| `a_changed_candidate_pool_re_derives_only_the_affected_entry` | §3a, scoped invalidation via `corpusContentHash` |
| `the_LLM_prompt_never_includes_a_species_more_than_two_rungs_below_the_output` | §2's bounded-pool fix, mechanically |
| `reconcile_check_refuses_a_stale_committed_file` | the same staleness discipline every other generator in this program already has |
| `real_corpus_end_to_end` | run against the REAL 829-species corpus (not a synthetic fixture) — the exact gap that let the original bug ship in `species-build`'s own G1/G2 |

## Boundaries

**Always:** run the deterministic pass first and completely before any model call; bound every model
call to the nearest 2–3 populated rungs below the output, never the whole corpus; canonicalize a
proposed pair before voting; flag every cross-rung recipe; freeze a resolved gap-fill until its own
candidate pool changes; refuse rather than fabricate on a failed validation or an unresolved vote; keep
the deterministic reconciler as sole write authority.

**Ask first:** relaxing owner lock 6 (capture-only exclusion) for any reason; changing which rungs count
as "shortfall" (a balance judgement); widening the LLM's proposal scope beyond deficit-filling; §4a's
cost-model question (a `crossRungGapFill` recipe's price, currently identical to a same-rung one).

**Never:** let the model choose a pairing outside the real roster; let a proposal bypass validation;
silently reuse a pair already claimed; ship a partially-reconciled file; re-call the model for an
already-resolved, still-current gap-fill entry.

## Amendments this module owes

Both already cleared by the owner (2026-09-05, quoted in the Objective above) when this module's
direction was set — recorded here, and in `demon-seed-map.md` §5, so a future reader does not need to
reconstruct the authorization from chat history:

- `spec-demon-fusion.md`'s own Boundaries name *"Ask first: … recipe-graph shape changes"* —
  `crossRungGapFill`'s cross-rung input rule for a bounded subset of recipes is exactly that shape
  change, distinct from the computation-METHOD amendment (deterministic code → deterministic-plus-LLM)
  already on record.
- `spec-demon-fusion.md`'s design text states *"every summonable rare/epic/legendary species gets
  exactly one recipe"* as a coverage guarantee. This module narrows that to best-effort with a named
  `unresolved` escape hatch (success criteria below) — a real, separate loosening from the
  computation-method change, not implied by it.

## Success criteria

- [ ] The real 829-species corpus's one known shortfall (`Almanac`/`Sunwoven`) reconciles as far as
      per-member voting actually converges — **not asserted to reach 100% coverage**, since an LLM
      vote can genuinely stay unresolved (§2's own rule); every gap-fill that DOES resolve is flagged
      `crossRungGapFill: true`, and every output that does not is named, not silently dropped.
- [ ] Every non-shortfall recipe is byte-identical to what today's `DemonRecipeCatalog.Build()` already
      produces — this module adds coverage, it does not re-derive what already works.
- [ ] A clean re-run with no roster change reproduces the identical committed seed byte-for-byte,
      including every gap-fill entry — proven by test, not assumed from "the LLM step ran once."
- [ ] `--check` refuses a stale committed file, matching every sibling generator's own discipline.
- [ ] Zero fabricated recipes: every unresolved deficit is named, not guessed.
