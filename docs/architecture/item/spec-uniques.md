# Spec: `uniques`

**Module id:** `uniques` · **Program:** [item](../item-map.md) · **Build order:** 17 of 21
**Depends on:** `affix-legality` (8), `item-power-reads` (9)
**Rulings:** D7 (rule 7 lifted), D15, D16, D26, D27 · lane [G1 `ssot-uniques.md`](ssot-uniques.md)

## Objective

Ship the **unique as a content class**: an ordinary `item` container whose identity is authored rather
than rolled, plus the validators that stop it from being either a strictly-better rare or a trophy.

**G1's line, and this module builds nothing that crosses it:**

> **A unique may break every rule that lives in the generator, and no rule that lives in the machine.**

**Users:** the importer (every check below is import- or load-phase), module 10 `item-card` (which needs
`sourceKind` to separate identity lines from the variance line), module 11 `drop-volume` (which needs an
acquisition channel), module 20 `item-surfaces` (which renders the flavour text).

## Design

### G1's premise is true in shipped code — verified from the loop structure, not from a comment

`ContainerValidator.Validate` has two loops and they check different things.

| Loop | Lines | Checks |
|---|---|---|
| **Fixed core** | `ContainerValidator.cs:47-60` | `DuplicateSeq` (`:49`), `UnknownAtom` (`:53`), `ValidateOverrides` (`:58`). **Nothing else** |
| **Pool** | `ContainerValidator.cs:81-125` | negative weight (`:84`), `UnknownAtom` (`:89`), `DuplicateAtomInContainer` (`:112`), **`TierOutOfWindow` (`:115`, `:118`)**, group derivation (`:127-137`) |

**`TierOutOfWindow` appears exactly twice and both are inside the pool loop.** A container's fixed core
is never tier-checked. And `ValidateOverrides` (`:174-215`) refuses a `kind_id` rewrite (`:197`), an
undeclared param (`:204`) and a malformed value spec (`:208-210`) — and **never compares a magnitude
against a band**. So an out-of-band identity atom loads clean today, by construction.

> ⚠ **One lane citation does not survive.** `ssot-uniques.md` §3.2 quotes a comment at
> `ContainerValidator.cs:87` — *"The window governs what the POOL may offer; a fixed core says what the
> thing is."* **That comment is not in the file.** `:87` is the negative-weight rejection. The
> *behaviour* the lane described is real and is verified above from the loop structure — which is what
> the lane's own design-gate box said it did — but the quote should not be repeated.

### ⛔ Four lane facts that are stale, and one that changes the design

| Lane says | Today | Consequence here |
|---|---|---|
| `pool_rolls ≤ 1` on a unique (§3.6) | **`pool_rolls` no longer exists.** `ContainerRow.PrefixRolls` / `SuffixRolls` replaced it (T3.2, `ContainerRow.cs:120-127`) because D2 and PoE cap the two classes separately | The shape rule becomes **`PrefixRolls + SuffixRolls ≤ 1`**, and `UniqueShapeInvalid` reads both |
| The variance pool holds atoms | **A pool row references an *affix*, never a bare atom** (`ContainerRow.cs:37`, `definitions.md` §4a) | The variance slot draws one **affix** from 3–6 authored affix rows |
| ⛔ "the **D6 quarantine** … every `combat.*` channel binds nowhere … the largest single constraint on what this lane can author, larger than SC2" (§4.3, §9.14) | **Lifted.** `stat.derived` is `Lawn = Full, Battle = Full, Sim = None` (`AtomKindRegistry.cs:255`); the comment above it records both re-openings and names their consumers (`BattleStatComposer` 2026-08-23, `AtomDerivedSubsystem` 2026-08-30) | **Crit, elemental power and defence, accuracy, dodge and the shield stat stack are authorable now.** The wave-1 palette is not eleven of twelve kinds — it is twelve of twelve on the lawn and five of twelve in battle. §4.3's "practical palette" paragraph is void |
| "33 reason codes" (§6.3) | **34** (`AtomRejection.cs`, `AtomRejectionReason`), and **`ContentRuleViolated` does not exist** | See *Reason codes* below — the decision landed, the code did not |
| "v1 count: 20 uniques" (§4.6) | Superseded by the lane's own 2026-08-23 banner: **144** = 8 per partition × 18 partitions, allocated as a Latin square | Build against 144. `UniqueAxisCollision` is exactly saturated at that count, which is why it must be a validator and not a guideline |

### D7 lifts the rung ceiling — and it does not lift the unique's own

**D7 (amended §2f.2): lift `ssot-rarity` rule 7.** That rule set `promote_from = 0` from ordinal 80 up
(`ssot-rarity.md:239-247`), leaving `sunwoven` and `almanac` drop-only. With D8 gating aptitude affixes
by rung, the strongest affix family sat behind luck. **Promotion now reaches ordinal 100 and no rung is
drop-only on any axis.**

Two things follow, and they pull in opposite directions, so both are stated:

1. **§4.5 rule 1 survives unchanged.** *"Every unique at ordinal ≥ 90 must be `source-locked` or
   `deterministic`, never plain `drop`."* That is about **acquisition**, not promotion — an item you
   cannot find is a different problem from a rung you cannot reach. `UniqueUnreachable` stays.
2. ⛔ **A unique is still never promoted, and that is a *structural* limit, not a progression ceiling.**
   Promotion *"only adds"* affixes drawn in the new rung's window (`ssot-rarity.md:230-231`); a unique
   is **defined** by drawing at most one. Promoting one would either break its shape or do nothing.

```csharp
// Structural limit, exempt from AGENTS.md's no-hard-ceilings rule and saying so here as that rule
// requires: promotion ADDS pool draws (ssot-rarity.md §3.7 rule 3), and a unique is DEFINED as
// PrefixRolls + SuffixRolls <= 1. This is not a progression ceiling -- the rung ladder itself now
// reaches ordinal 100 for everything (D7 lifted rule 7); it is the class's own shape. The player's
// path to a stronger unique is a different unique, or enhancement, or the variance reroll.
const bool UniquesArePromotable = false;
```

### ⭐ A unique's L0 channel weight is structurally zero — and must say so

Effect-pipeline module 12 `affix-channel-weights` turns a power class into a **pool rate**. A unique's
identity atoms are **fixed-core rows, never drawn**, so no channel weight applies to them — not "their
weight is tuned to zero", but *there is no draw for a weight to modify*.

Per `AGENTS.md` a structural limit is exempt from the no-ceilings rule **and must say so in a comment**.
The one place this bites is a reviewer reading L0's coverage report and seeing every unique at 0.00: the
comment is what tells them that is correct rather than a gap.

The **variance slot is the exception and is fully L0-governed** — it is one real draw from an authored
affix pool, so it carries a power class and a channel weight like any other draw.

### What a unique is, in the schema

Three facts, unchanged from §3.4 except for the roll columns:

1. An ordinary `effect_container` with `Kind = ContainerKind.Item` and the `item.` prefix
   (`ContainerRow.cs:9`, `:141`). **No new `container_kind`** — D27's four (`gem`/`set`/`charm`/`combo`)
   are all for other mechanisms, and this module asks for none of them.
2. **Identity:** 1–3 atoms in the fixed core, authored, possibly out of band, possibly of a kind no
   affix pool offers. Their *values* may roll inside an authored `OnInstantiate` band (±15% of midpoint)
   — the core loop calls `Freeze` exactly as the drawn loop does (`Instantiator.cs:119` vs `:131`), so
   this is shipped behaviour, not a request.
3. **Variance:** `PrefixRolls + SuffixRolls ≤ 1`, drawn from 3–6 affix rows authored *for this unique*,
   with `MinTier == MaxTier` so ilvl narrowing is a no-op inside and a structural refusal outside.

### The mutual-relevance mechanism — three validators and one unmeasured invariant

| Device | Shape | Standing |
|---|---|---|
| **Counter-pressure** | `counter_pressure ∈ {drawback, conditional, narrow}`, **checked against the content**: `drawback` ⇒ a negative-magnitude core atom exists; `conditional` ⇒ a core atom carries a non-empty `when_json`; `narrow` ⇒ summed raw-stat AE ≤ 60% of the rung baseline | **HARD** — refuses at import |
| **Budget** | total ≤ rung baseline + **1.5 AE**, the same premium I5 gives a set per piece; declared `budget_ae` must agree with summed content within ±25% (definitions §7's own drift tolerance) | **HARD** |
| **Anti-convergence** | one unique per `(role, rung band, power_axis)`; none on either `jewel-minor`; ≤ 8 of 15 roles per frame; never a set member | **HARD**, cross-row, import-phase only |
| **Parity** `W ∈ [25%, 75%]` | the probability a random rare at rung *n* beats the unique on total magnitude within one channel family | ⚠ **Stated, never measured.** §10.3 |

⚠ **The parity invariant is the one thing in this module that is not buildable as written.** It needs
I1's overlap simulator, which this module does not own and which §9.2 asks for. **Do not implement a
second simulator.** Ship the three hard validators; register parity as a reported metric with no
threshold until the harness exists, and say in the report that it is unbounded. A soft metric that
announces its own absence is honest; a threshold computed by a second implementation is not.

### Reason codes — the decision landed, the code did not

`ssot-uniques.md` §6.3 put the choice to the owner and **README #3 answered it**: *"One namespaced
`ContentRuleViolated`"* (item-ideal §2b.1). Verified: **`AtomRejectionReason` has 34 members and
`ContentRuleViolated` is not one of them.**

So this module's seven checks all raise **one** code with a namespaced rule id in the detail string:

| Rule id | Fires when |
|---|---|
| `unique.counter-pressure` | declared none, or declared one the content does not satisfy |
| `unique.budget` | above rung baseline + 1.5 AE, or declared vs summed drift > ±25% |
| `unique.axis-collision` | two uniques share `(role, rung band, power_axis)` |
| `unique.role-forbidden` | a `jewel-minor` role, or the 8-of-15 quota overflowing |
| `unique.rung-ineligible` | ordinal < 30, or `unique_eligible = 0` on that rung |
| `unique.set-membership` | `item_set_member` references a container with an `item_unique` row |
| `unique.unreachable` | `acquisition = 'drop'` at ordinal ≥ 90 |
| `unique.shape` | `PrefixRolls + SuffixRolls > 1`, `MinTier ≠ MaxTier`, > 3 identity atoms, or an `OnInstantiate` spread wider than ±15% of midpoint |

⚠ **Adding `ContentRuleViolated` is a reviewed change to effect-atom's closed enum** — it belongs in
*Ask first*, not in this module's own scope. It is one member and it is what stops seventeen lanes each
adding four.

### ⛔ `damage.convert` — recorded as an ask, depended on by nothing

`ssot-uniques.md` §4.3 requests a **13th atom kind** that re-routes a damage packet's element or
channel. `AtomKindRegistry.KindCount = 12` (`AtomKindRegistry.cs:20`) and the twelve are enumerated at
`:197-476`. **This module does not assume it, does not design around it, and authors nothing that needs
it.**

| | |
|---|---|
| **What is asked** | a kind that transforms a damage packet — *"your fire damage becomes ice"* |
| **Why the closed vocabulary cannot express it** | `stat.modify`'s ops are `Flat\|Increased\|More` and cannot emit `Override` (`AtomKindRegistry.cs:220`); `stat.derived` has `Replace` but replacing a derived channel is not converting a damage type |
| **Blocked on** | the damage consumer/applier spec, **which has no owner today** |
| **Standing rule** | *do not add the kind before the consumer* — that is the `status.expose.*` and `stat.derived` mistake for the third time |
| ~~**The open question**~~ | ✅ **RESOLVED — D39: add `Override`.** Owner, 2026-09-04: *"add override, this is funny feature."* Damage-type conversion ships as a real capability |

### ⭐ D39 — `Override` is added, and the consumer comes with it

**The ruling deliberately overrides a standing rule, so the override is stated rather than buried.**
That rule is *do not add the kind before the consumer* — the `status.expose.*` and `stat.derived`
mistake, made twice. D39 says build it anyway, because the feature is worth it.

⛔ **Therefore the consumer is part of the ask, not a follow-up.** `Override` added to
`stat.modify`'s op set (`AtomKindRegistry.cs:220`, today `Flat | Increased | More`) **and** a damage
applier that reads it. An `Override` that binds to nothing is the third instance of the same defect,
and this ruling is the reason to avoid it, not permission to repeat it.

| | |
|---|---|
| Request goes to | **effect-atom** — the op set is theirs |
| Shape of the ask | *"add `Override`, and here is the consumer that reads it"* — never the op alone |
| Item-side surface | a unique's conversion line on the item card (module 10), and a `battle-only` presentation tag if it does not resolve on the lawn |
| Blocked until | the applier exists. **This is a wiring order, not a refusal** |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Unique"
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ItemUnique"

# the cross-row checks are catalog properties, so they run over the whole corpus
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~UniqueCorpus"
```

## Project structure

```text
src/FusionRpg.Core/Items/Uniques/UniqueRow.cs             new — the 9 columns, as a record
src/FusionRpg.Core/Items/Uniques/UniqueValidator.cs       new — the 8 rule ids; per-row checks
src/FusionRpg.Core/Items/Uniques/UniqueCorpusValidator.cs new — the 4 CROSS-ROW checks, import-phase
src/FusionRpg.Core/Items/Uniques/UniqueParityMetric.cs    new — reported, unbounded until I1's harness
src/FusionRpg.Data/Sqlite/RpgStore.ItemUniques.cs         new — item_unique DDL + upsert + list
src/FusionRpg.Core/Effects/Atoms/AtomRejection.cs         edit — ContentRuleViolated (ASK FIRST)
data/seed/items/uniques/                                  new — the 144 authored rows
tests/FusionRpg.Core.Tests/Items/UniqueTests.cs           new
tests/FusionRpg.Core.Tests/Items/UniqueCorpusTests.cs     new
```

**`Instantiator` gains no unique branch.** That is the sentence to hold this module to — if it grows one,
the class stopped being data (§6.4).

## Code style

```csharp
// Counter-pressure is CHECKED against the content, never trusted from the column. This is I5's own
// device (SetTierForbiddenAtom) applied here for the same reason: a rule the importer enforces beats
// a rule a reviewer remembers. `drawback` reads SIGN, and sign carries meaning per kind
// (definitions.md §2) -- so the check asks the kind, it does not assume a negative number is a cost.
static bool SatisfiesCounterPressure(UniqueRow u, IReadOnlyList<ContainerAtomRow> core,
                                     Func<string, AtomRow?> lookupAtom, long rungBaselineAe) =>
    u.CounterPressure switch
    {
        "drawback"    => core.Any(a => HasNegativeMagnitudeOnAWantedChannel(lookupAtom(a.AtomId)!, a)),
        "conditional" => core.Any(a => !string.IsNullOrWhiteSpace(lookupAtom(a.AtomId)!.WhenJson)),
        // long, not int: AE x 100 against a rung baseline that contentScale can reach (AGENTS.md).
        // Widen BEFORE multiplying, and divide by 100 exactly once, last.
        "narrow"      => RawStatAeHundredths(core, lookupAtom) * 100L <= rungBaselineAe * 60L,
        _             => false,
    };
```

## Testing strategy

| Test | Asserts |
|---|---|
| `a_fixed_core_atom_out_of_band_loads_clean` | G1's premise, against the shipped validator — the tier window is pool-only |
| `tier_out_of_window_fires_only_from_the_pool_loop` | the same fact from the negative side, over a container with both a core and a pool |
| `an_override_is_never_band_checked` | `ValidateOverrides` well-formedness only (`ContainerValidator.cs:208-210`) |
| `a_unique_that_only_rolls_higher_numbers_is_refused` | §3.2's refusal: at least one atom the role's pool cannot offer at all |
| `prefix_plus_suffix_rolls_above_one_is_shape_invalid` | the **corrected** shape rule, not `pool_rolls` |
| `min_tier_must_equal_max_tier` | ilvl narrowing is a no-op inside, a structural refusal outside |
| `an_instantiate_spread_wider_than_fifteen_percent_is_refused` | the identity band |
| `counter_pressure_is_checked_against_content_not_trusted` | all three arms, each with a planted violation |
| `budget_refuses_above_baseline_plus_one_point_five_ae` | and the ±25% declared-vs-summed drift, both directions |
| `two_uniques_on_one_role_rung_band_and_axis_collide` | cross-row, import-phase |
| `no_unique_on_either_jewel_minor_role` | `UniqueRoleForbidden`'s second arm |
| `at_most_eight_of_fifteen_roles_per_frame_carry_a_unique` | the quota, as a count over the corpus |
| `a_unique_may_not_be_a_set_member` | `item_set_member` cross-table |
| `drop_acquisition_above_ordinal_ninety_is_unreachable` | §4.5 rule 1 — **unchanged by D7** |
| `a_unique_is_never_promotable_even_though_every_rung_now_is` | D7 lifted rule 7; the class rule is structural and survives |
| `promote_from_is_forced_to_zero_regardless_of_the_rung_budget_key` | the override is the unique's, not the rung's |
| `an_out_of_band_identity_atom_must_be_a_private_atom_row` | §4.6's brick-every-copy rule, as a lint |
| `a_private_identity_atom_is_referenced_by_exactly_one_container` | §8.6, the other half |
| `a_plant_unique_carrying_plating_or_carapace_is_refused` | frame physics, not frame taste — `ParamNotHonoured` |
| `a_stat_derived_identity_atom_binds_on_lawn_and_battle` | **the D6 quarantine is gone** (`AtomKindRegistry.cs:255`), asserted rather than assumed |
| `a_stat_derived_identity_atom_is_refused_for_sim` | Sim is still `None`; the promotion of the runtime check to import time still bites there |
| `every_unique_rule_raises_one_code_with_a_namespaced_rule_id` | README #3's decision, as a test |
| `the_144_corpus_saturates_the_axis_grid_without_a_collision` | 8 roles × 18 partitions, the Latin square |
| `parity_is_reported_and_declares_itself_unbounded` | the honest gap — a metric that says it has no threshold |
| `instantiator_has_no_unique_branch` | the design's own load-bearing sentence, as a guard |

## Boundaries

**Always:** keep a unique an ordinary `item` container occupying an ordinary `item_base_type` row; check
counter-pressure against content; run every cross-row check at import, never at load; force
`promote_from = 0` and comment that it is structural; make an out-of-band identity atom a **private**
atom row; carry `flavour_key` as a key, never a literal.

**Ask first:** ⛔ **`damage.convert`, a 13th atom kind** — recorded, blocked on an applier spec with no
owner — ✅ but the deferred-vs-refused call is **RULED as D39: neither. `Override` is added**, with the
damage applier as part of the same ask. Adding **`ContentRuleViolated`** to
`AtomRejectionReason` (effect-atom's closed enum, 34 today). Moving the rung floor below ordinal 30
(§10.7). Whether `counter_pressure` stays a hard requirement at all (§10.8) — some of the genre's
best-loved uniques have no drawback. Whether a unique is salvageable (§10.6, recommendation: no).
Requesting `unique_value_reroll` from I7/I6 (§10.5) — module 15's surface, not this one's.

**Never:** give a unique a rarity rung of its own (§4.1 — the flag is orthogonal, the rung is ordinary).
Never let a unique be a set member. Never derive the class from a shape — `PrefixRolls = 0` plus a fat
core is also a fully-crafted item, and a class anyone can forge is not a class. Never add a
`container_kind`, an atom kind, a trigger, a predicate leaf, or an instance column. Never write a second
parity simulator.

## Success criteria

- [ ] An out-of-band fixed-core magnitude loads clean, and a pool row at the same tier is refused —
      both proven by test against the shipped `ContainerValidator`.
- [ ] A hand-authored item that only rolls higher numbers is **refused**, with `unique.counter-pressure`
      or `unique.budget` naming why.
- [ ] All four cross-row checks run at import over the whole corpus, and the 144-row allocation passes
      the axis grid with no collision.
- [ ] `PrefixRolls + SuffixRolls ≤ 1` is the shape rule; no code path reads a `pool_rolls` column.
- [ ] A unique is never promotable, and the constant that says so carries the structural-exemption
      comment `AGENTS.md` requires.
- [ ] A `stat.derived` identity atom binds on lawn and battle; §4.3's quarantine paragraph is retired
      in the lane with `AtomKindRegistry.cs:255` cited.
- [ ] Every unique rule raises one namespaced code; no new member of `AtomRejectionReason` beyond the
      single `ContentRuleViolated` (and that one is approved before it lands).
- [ ] The parity invariant is **reported with no threshold** and the report says so; no second simulator
      exists.
- [ ] `Instantiator` has no unique branch.
- [ ] `damage.convert` is recorded as an open ask in this spec and in `ssot-uniques.md` §10.1, and
      nothing built here depends on it.
