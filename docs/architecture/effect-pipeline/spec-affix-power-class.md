# Spec: `affix-power-class`

**Module id:** `affix-power-class` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 11 of 12
**Depends on:** `affix-schema` (module 1), `affix-library` (module 3)
**Added:** owner decision 2026-09-03 — the L0 layer, [effect-pipeline-ideal.md](../effect-pipeline-ideal.md) §5.6

## Objective

Assign every affix a **power class**: one value from a closed, append-only enum saying how strong the
affix is *as an idea*, independent of its tier or its numbers. This is the classification half of L0;
[`affix-channel-weights`](spec-affix-channel-weights.md) (module 12) is the deterministic half that turns the class into
a rate.

**The class is a judgement, so a model makes it. The class is never a number, and never a rate.** That
is `seedsmith-map.md` P1 without amendment — *the LLM writes identity, deterministic code writes
magnitude* — and the owner restated it for this module: *"the LLM will resolve by closed enum; our
deterministic engine will distribute and resolve atom effect rate by the enum."*

## Design

### ⭐ Classify the FAMILY, not the affix

**The unit of classification is the atom family, not the affix row.** `affix-library` (module 3)
rule-generates the overwhelming majority of affixes 1:1 from the atom catalog, so classifying affixes
directly would mean ~980 model calls instead of **98** — and, worse, it would permit an incoherence the
family view forbids by construction:

> `combat.power` at t1 classified `filler` while `combat.power` at t5 is classified `pinnacle`.

**Tier already carries strength.** `ssot-rarity.md` §3.3 gives each rarity rung a tier window, and L3
gives each tier a value range — so a family's tiers are the *same idea at different magnitudes*, and one
class describes all five. A model asked to rank tiers is being asked for a magnitude judgement wearing an
enum's clothes.

| | Rejected: classify each affix | ✅ Chosen: classify each family |
|---|---|---|
| Model calls | ~980 | **98** |
| Tier coherence | not guaranteed | **structural** — one class covers all tiers |
| Re-runs when the library regenerates | every affix | only genuinely new families |

### An affix's class is derived, exactly as its tags are

```text
powerClassOf(affixId) := MAX over the affix's refs of  familyPowerClass(ref)
                         (a slot ref contributes its slotAtomPattern's family, or nothing)
```

**`MAX`, not sum or average.** An affix bundle is as strong as its strongest member for gating purposes
— a bundle containing one `pinnacle` atom must not launder itself into a lower class by averaging with
two `filler` atoms. Laundering is the exact abuse the layer exists to prevent.

This mirrors [`eligibility-tags`](spec-eligibility-tags.md)'s own decision — *"an affix's tags are
DERIVED from its refs' atoms, exactly as its `class` already is"* — for the same three reasons stated
there: **it cannot contradict the bundle**, it keeps one home for the fact, and the safe-direction rule
already covers slots (an unresolvable slot ref contributes nothing, so the derived class can only be too
**low**, which is visible as an affix appearing where it should not — see Boundaries).

⚠ **One additive escape hatch, reversible and initially unused.** A hand-authored bundle whose
*combination* is stronger than any member — the emergent case — may carry an authored
`power_class_floor` read as `MAX(derived, floor)`. It is one nullable column, it can only raise, and
nothing sets it in v1. Recorded now because the alternative is discovering it after ~980 affixes exist.

### The enum — five classes, append-only, consecutive ordinals

**Append-only ordinals, consecutive — matching every closed roster in this codebase.**

> ⚠ **Corrected 2026-09-03.** An earlier draft claimed *"spaced by 10 — the house convention
> (`ElementRow`, `Aptitude`, `rarity`)"*. **No roster does this**: `ElementRow` is 0–5, `Aptitude` 0–11,
> `DemonRarity` 0–9, and the `rarity` table has no rows at all. The precedent was invented. Spacing may
> still be worth arguing on its own merits; it cannot be argued from precedent.

| Ordinal | `power_class` | What it means | Rough share of the library |
|---:|---|---|---:|
| 0 | `filler` | pads a pool. Nobody builds around it | ~40% |
| 1 | `notable` | a build takes it if offered | ~30% |
| 2 | `potent` | shapes a build's direction | ~20% |
| 3 | `defining` | a build is *about* this effect | ~8% |
| 4 | `pinnacle` | top-shelf. **The thing channels exist to gate** | ~2% |

> ⛔ **The names deliberately share no word with a rarity rung.** Rarity is `chaff · sprout · grafted ·
> cultivated · fused · chimeric · heirloom · firstseed · sunwoven · almanac`. **Power class and rarity
> are different axes and must never be confusable** — "one word, four meanings" is a named defect in
> `item/enrichment-contract.md` §1, and `ssot-rarity.md` §4.3 already had to correct a case where
> *unique* and *set* were mistaken for rarity rungs.

**The shares are a target, not a constraint**, and they are what makes the classification checkable: a
run that returns 60% `pinnacle` has not classified anything, and `budget` should say so. They belong in
`data/tuning/`, not in code.

### Honesty: `basis`, and `blocked` is a legal answer

The class carries a **`basis`**, exactly as family labels do in seedsmith's `family-extract`, and
**`blocked` is a legal answer** — a family the model cannot confidently place is reported, never guessed
into `filler`.

**An unclassified family is not a zero.** It is excluded from L0's weighting until classified, and
`affix-channel-weights` treats it as `notable` with a recorded `unclassified` flag so a gap is *visible as a
default* rather than invisible as an absence. A silent `filler` default would be the worst outcome: the
strongest unclassified effect would land in the cheapest pool.

### Why this is not `power-estimate`, and needs no `provisional` marking

seedsmith's `power-estimate` (D5) marks its output **provisional** — *"superseded the moment that
species is actually observed"* — because it estimates a **measurable** quantity (observed HP) that a real
measurement later contradicts.

**This module estimates nothing measurable.** *"Is `Master of Fire and Ice` a top-shelf effect?"* has no
later observation that refutes it. And ⭐ **the class is an input to balance rather than an output of
it**: a balance pass moves the *rates* in module 12's tuning table, never the classifications. That is
what makes an authored class stable here where an estimated tier had to be provisional.

## Commands

```powershell
# classify (seedsmith stage; model calls)
cd tools\seedsmith
python -m seedsmith affix power-class --adapter items --dry-run
python -m seedsmith affix power-class --adapter items

# verify the distribution against its declared target
python -m seedsmith check --adapter items --metric PowerClassDistribution
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/effects/power_class/      new — the classifier stage
  classify.py            one family -> (power_class, basis); closed-enum structured output
  derive.py              affix class := MAX over refs; the slot safe-direction rule
  registry.py            the append-only enum, consecutive ordinals
tools/seedsmith/seedsmith/metrics/power_class.py             new — distribution vs declared shares
data/seed/items/_registry/power-classes.v1.json              new — the enum, checked in
data/tuning/affix-power-class.v1.json                        new — target shares, not code
src/FusionRpg.Core/Effects/Atoms/AffixPowerClass.cs          new — the C# enum + parse, mirrors the registry
src/FusionRpg.Data/Sqlite/RpgStore.Containers.cs             effect_affix += power_class, power_class_floor
```

## Code style

```csharp
// MAX, never sum or average: a bundle is as strong as its strongest member for gating. Averaging
// would let one `pinnacle` atom launder itself into a cheaper pool behind two `filler` ones, which
// is the abuse L0 exists to prevent.
static AffixPowerClass ClassOf(AffixRow affix, Func<string, AffixPowerClass?> familyClassOf) =>
    affix.Refs
        .Select(r => familyClassOf(FamilyOf(r)))   // a slot ref with no pattern yields null
        .Where(c => c is not null)
        .DefaultIfEmpty(AffixPowerClass.Notable)   // unclassified defaults VISIBLY, never to Filler
        .Max()!.Value;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `family_is_the_classification_unit_not_the_affix` | 98 classifications cover ~980 affixes |
| `all_tiers_of_one_family_share_one_class` | the incoherence this design forbids is impossible |
| `bundle_class_is_the_max_of_its_members` | over a real multi-ref bundle |
| `a_pinnacle_atom_cannot_be_averaged_down_by_fillers` | the laundering abuse, asserted directly |
| `a_slot_ref_with_no_pattern_contributes_nothing` | the safe direction, and it lowers rather than raises |
| `an_unclassified_family_defaults_to_notable_and_is_flagged` | **never silently `filler`** |
| `blocked_is_a_legal_answer_and_is_not_a_class` | honesty, same as `family-extract` |
| `power_class_floor_can_only_raise` | the escape hatch cannot weaken a class |
| `the_schema_audit_rejects_a_numeric_power_class` | mechanical P1 enforcement, not review |
| `power_class_names_collide_with_no_rarity_rung_id` | the two-axes guard, as a test |
| `distribution_far_from_declared_shares_is_a_finding` | a 60%-`pinnacle` run fails visibly |
| `re_running_over_unchanged_families_is_byte_identical` | content-addressed, per seedsmith's own law |

## Boundaries

**Always:** classify families; derive affix class as `MAX` over refs; carry `basis`; accept `blocked`;
default an unclassified family to `notable` **with a flag**.

**Ask first:** adding a sixth power class (the enum is closed and reviewed, exactly as the 12 atom kinds
and 8 triggers are); setting `power_class_floor` on any affix.

**Never:** let the model emit a weight, a rate, a probability or any number — `audit_schema` rejects a
numeric field mechanically, and that check is the enforcement, not review. Never derive power class from `AtomRow.TagsJson`. ⚠ **Corrected 2026-09-03:** that field carries generator
*provenance*, not thematic tags; **D28** has E43 stamp the family's `offensive`/`defensive`/`utility` tags
through. Either way they carry no strength information — the prohibition stands, its stated reason did not. Never let a power-class id equal a rarity rung id.

## Success criteria

- [ ] 98 families classified, `basis` on every row, `blocked` counted separately and not coerced.
- [ ] An affix's class is derived and stored nowhere twice; `MAX` proven over a real bundle.
- [ ] A numeric field in the model's output is rejected mechanically, proven by test.
- [ ] The distribution is reported against declared target shares, and a degenerate run is a finding.
- [ ] No power-class id collides with any rarity rung id, enforced by test.
- [ ] Re-running over unchanged families produces a byte-identical artifact.
