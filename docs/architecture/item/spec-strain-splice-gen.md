# Spec: `strain-splice-gen`

**Module id:** `strain-splice-gen` · **Program:** [item](../item-map.md) · **Build order:** 21 of 21 · ⭐ **model calls**
**Depends on:** `affix-legality` (8), `sockets` (16) · **surfaces owed by** `item-surfaces` (20)
**Rulings:** D20, D21, D22 (as amended §2f.2), D23, D24, D25, D27 · lane [ssot-sockets.md](ssot-sockets.md) §4.4–4.6
**Read first:** ssot-sockets' amendment banner (`:10-36`) — six of its statements are superseded, and the
banner was amended twice on 2026-09-03.

## Objective

Generate the **102 socket combinations** D20 specifies — **36 Strains** (12 aptitudes × 3 archetypes) and
**66 Splices** (`C(12,2)`, every unordered aptitude pair) — as seedsmith-configured content on the twelve
aptitude grid. This is the program's **second model call**, and it also owns **retiring or migrating the
existing `socket-word` corpus**.

## Design

### D20's vocabulary, and the word that is banned

| | Count | Shape | Fiction |
|---|---:|---|---|
| **Strain** | **36** = 12 aptitudes × 3 archetypes | **one** aptitude | a single cultivated line |
| **Splice** | **66** = `C(12,2)` | **two** aptitudes joined | two lines fused — the base game's own verb |
| | **102** | | |

⛔ **Never the word "runeword"**, in ids, names, prompts, tests or comments. D20 checked `Strain` and
`Splice` against every rarity rung, slot role, plant slot name and power class for collision — *"one word,
four meanings"* is a named defect (`enrichment-contract.md` §1). ⚠ The shipped corpus and code still say
`socket-word` / `sockword` / `gem.word-*`; migrating that vocabulary is this module's (below).

⭐ **The grid is what makes 102 affordable.** 12 + 3 authored values produce 36; 12 produce 66. Nobody
authors 102 rows. Aptitudes are shipped and closed — `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:40-51`,
count computed as `PostureCount × PerPosture` (`:36`).

### ⛔ P1: the model writes identity, deterministic code writes magnitude

`audit_schema` (`tools/seedsmith/seedsmith/pipeline/model.py:53`) rejects any bare `number`/`integer`
field — `NUMERIC_JSON_TYPES` at `:42` includes `integer` deliberately — and `Pipeline.__post_init__`
(`:143-149`) **raises at construction**, so a numeric field never reaches a model call. Every schema
carries a `blocked` variant (`BLOCKED_FIELD` at `:39`).

| Model emits | Code resolves |
|---|---|
| the name, `nameKey`, flavour | the id, `seq`, partition |
| the four ingredient **families** (from the gem corpus's own vocabulary) | position order, `min_tier` |
| the granted atom **families** | every magnitude, via `seedsmith.numerics.resolve` |
| `hostRole` / `hostFrame`, or none | `min_sockets`, from the ingredient count |

**A Strain grants mechanisms, not magnitudes** (ssot-sockets §8.3) — a proc, a rider, a spawn. That is
also what keeps it from being *"a volume discount with a name"*.

### Ingredient count is **4** — and nothing in the shipped corpus can hold four

**D20 as amended (§2f.2): the ingredient count is 4, matching `socket_max`'s cap.** ssot-sockets §4.1
fixes the structural maximum at 4 for four stated reasons, the third being *"a word is at most four
ingredients, which is memorable."*

⛔ **Measured over all 740 shipped base types (`data/seed/items/base-types/**`, read 2026-09-03): the
maximum `socketMax` anywhere is 2.**

| Role | `socketMax` distribution |
|---|---|
| `armament-primary` | 0×18 · 1×26 · **2×4** |
| `core-guard` | 0×11 · 1×24 · **2×13** |
| every other role | 0 / 1 / 2 only; `jewel-minor-a` additionally has 24 entries with `socketMax` **absent** |

`socketsNow` is capped at `base_type.socket_max` (ssot-sockets §4.1; D23 prices the top-up but does not
raise the cap — and §2f.2 corrects D23 to *"a pricing ruling, not the resolution of a blocking
contradiction"*). **So no Strain and no Splice is buildable on any shipped chassis** — and neither are the
ten existing 3-ingredient socket-words.

> ⛔ **Hard dependency on module 6 (`base-types`):** it must issue `socketMax = 4` on at least
> `armament-primary` and `core-guard`, which are the two roles ssot-sockets §4.1 already assigns 4.
> ⚠ [item-ideal.md](../item-ideal.md) §2g #7 is the same finding from the other end — the lane's
> `socket_max` table *"uses the old twelve role ids and assigns nothing to `ward-array`, `infusion` or
> `retinue`"*. **Both must be fixed together, and this module is inert until they are.**

### D21 — a low-rarity, **non-set** base

| | Set | Strain / Splice |
|---|---|---|
| Axis | **across** items — collect the pieces | **within** one item — fill its sockets |
| Exclusivity | a set piece **may not** carry a Strain or Splice | |

⚠ **§2f.2 strikes D21's *"base rarity: high"* row for sets** (D15: a set has no rarity). **The
exclusivity rule stands on its own**, and it is a validator, not a tuning knob.

⭐ **This is what makes low-rarity items permanently valuable.** A `chaff` breastplate is the *only*
chassis a Splice can live in — a second progression route, not a consolation prize.

### D22 as amended — affinity is a **bonus**, not a requirement

> **§2f.2:** *"Owner: revert to bonus. The hard requirement **could never fail** — on a low-rarity chassis
> every socket is crafted, and D24 lets the crafter choose the affinity, so it was a fee wearing a gate's
> name. Matching affinity now grants an **enhanced tier**, reusing §4.2's `+1` pattern."*

⚠ **The two layers treat affinity differently on purpose, and that must be said wherever either is
documented or it reads as an inconsistency:** soft (`+1` to effective count) for the generated resonance
floor; **an enhanced tier, still soft**, for Strains and Splices.

**A Strain therefore always resolves; matching affinity only makes it better.** Failure is impossible, so
a Strain is *a plan* (§8.7.3) rather than a second lottery: find a cheap base of the right role, open its
sockets (`socket.add`, D23 — priced by rarity), imbue them (`socket.imbue`, D24 — ⚠ still unpriced,
[item-ideal.md](../item-ideal.md) §2g #9), fill them.

### ⚠ Nothing maps twelve aptitudes to six socket elements — **do not invent it**

**Verified 2026-09-03: no such mapping exists anywhere.** Aptitudes are 12
(`Aptitude.cs:40-51`, keyed on posture and derived-channel families — *dodge*, *crit-denial*, *reflect*);
concrete elements are 6 (`src/FusionRpg.Core/Stats/Derived/ActorElementTypes.cs:3-11`, and
`core.v1.json.elements.concrete`). No file in `src/FusionRpg.Core/Combat/Element/` mentions aptitude, and
`core.v1.json` carries no bridge. **§2f.2 names this explicitly as something D22's revert *sidesteps*.**

**Recommended reading of D22-as-amended, which needs no mapping at all:** the enhanced tier keys on **each
ingredient gem's own element** matching its socket's affinity — exactly §4.2's existing per-insert
attunement test. The Strain's *aptitude* never has to become an element.

> **Decider: the owner.** If the bonus is meant to key on the Strain's **aptitude** rather than its
> ingredients' elements, a 12 → 6 mapping is required, it is a new reviewed vocabulary, and it is not this
> module's to declare. **This spec does not invent one.**

### ⛔ Learnability: 127 combinations, against a bar of ~45

ssot-sockets §4.4 sized the catalogue at *"twenty-five generated containers plus ≤ 20 words… ~45. That is
a size a player can learn. Four hundred would not be."* D20's own amendment concedes the point:

```text
25 resonances  (Pure 18 + Ring 4 + Eclipse 1 + Diversity 2, §4.4)
+ 102 Strains and Splices
= 127     -- 2.8x the stated learnable bar; §8.2's wiki-dependency failure is LIVE
```

**The mitigation §4.4 already names is promoted from a nicety to a requirement** (D20, and
[item-map.md](../item-map.md) §4 module 20):

| Requirement | Detail |
|---|---|
| **Compendium reveal** | a Strain is revealed once the player has held every ingredient at least once — *"content the game gives you, not knowledge you import"* |
| **Socket-UI preview** | what the current fill produces, and what is **one insert away** — §8.2 adds that at four ingredients the hint must also cover *one **swap** away* |

⛔ **Neither is this module's to build. Both belong to module 20 `item-surfaces`.** State it as a
dependency: **shipping 102 combinations without module 20 ships the wiki-dependency failure.** They need
not ship in the same wave, but the plan must not sequence 21 before 20 and call it done.

### Retiring the `socket-word` corpus — measured, and it is not what it is usually called

**Measured 2026-09-03** (`data/seed/items/socket-words/sockwords.json`, one file, `kind: "socket-word"`):

| Fact | Value |
|---|---|
| Entries | **25** |
| Ingredients per entry | **2 ×15, 3 ×10** — never 4 |
| `hostRole` | 17 unset; `armament-primary` 3, `armament-secondary` 2, `core-guard` 1, `footing` 1, **`ward-array` 1** |
| `hostFrame` | 18 unset; `plant` 6, `humanoid` 1 |
| Runtime ids | `gem.word-*` — the `word` vocabulary D20 renames |
| Ingredient keying | ⚠ **gem *families*** (`atom.searing-strike`, `atom.bulwark`, …), **not elements**. Element appears only as an atom **param** on 6 of 50 `fixedAtoms` |

⚠ **The corpus is family-keyed, not element-keyed.** A brief that describes it as element-keyed will
produce a migration that does not fit it.

**It is load-bearing in three places, so it cannot simply be deleted:**

| Site | Consequence |
|---|---|
| `tools/seedsmith/seedsmith/adapters/items/kinds.py:79-82` — the `socket-word` `KindSpec` | `assert len(KINDS) == 15` at `:104` fails if the kind is removed rather than renamed |
| `tools/seedsmith/seedsmith/metrics/linkage.py:156-194` — `Registration/IngredientUnsatisfiable`, **`gates = True`** | reads `by_kind("socket-word")` against `by_kind("gem")`; **reports no findings today** (run 2026-09-03) and would report none over an empty kind either — a silently-covered partition |
| `data/seed/items/_registry/*` partition ledger + `Coverage/EmptyPartition` | an emptied partition reports as a gap, which is the correct and visible outcome |

> **Recommended: migrate, do not delete.** Rename the kind `socket-word` → `combination`, keep its
> `KindSpec` shape (`runtimeId`, `minSockets`, `ingredients`, `fixedAtoms` all survive verbatim), add
> `combinationKind: strain | splice` and `aptitudes: [...]`, and **regenerate all 25 at 4 ingredients**.
> ⚠ The one entry hosted on `ward-array` is outside the twelve-role hybrid core and can never be worn by a
> hybrid — regenerate its host too. **Alternative:** keep 25 legacy words alongside 102, taking the
> catalogue to 152. Rejected: it deepens exactly the learnability failure §4.4 named.
>
> **Decider: the owner** on rename-vs-retain, because it moves a `gates = True` metric and the `KINDS`
> assertion. Either way `Registration/IngredientUnsatisfiable` must follow the kind, or a gating check
> quietly stops gating.

### Container kind, ids, and the per-actor cap

**D27 gives combinations `combo`** — *"the 25 generated resonances **and** the 102 Strains/Splices"*.
⚠ This **supersedes ssot-sockets §4.5's** deliberate reuse of `gem` for combination containers, and its
stated reason (*"one code change for a layer beats two"*) no longer applies now that four kinds are being
added together. `ContainerKind` is a six-value enum (`ContainerRow.cs:7-14`) with a matching regex
(`ContainerValidator.cs:17-19`) and a `PrefixOf` arm (`:142`); the grammar row in `definitions.md` §1 is
the SSOT the regex mirrors and **wins over any spec** — an ask, owned by effect-atom
([item-ideal.md](../item-ideal.md) §2g #3).

```text
combo.strain-{aptitude}-{archetype}      combo.strain-might-offense        36
combo.splice-{aptitudeA}-{aptitudeB}     combo.splice-might-agility        66   -- pair sorted by
                                                                                -- Aptitude ordinal,
                                                                                -- so C(12,2) yields one id
```

Sorting the pair by ordinal is what makes a Splice **unordered by construction** rather than by a
uniqueness check that fires after 66 rows exist. One segment after the prefix, no second dot — legal.

⚠ **A per-actor cap is owed** ([item-ideal.md](../item-ideal.md) §2g #8): *"One-per-item is capped; twelve
Splices on one actor is not. Tunable, start at 3."* It is a count over equipped items, so its natural home
is module 12's evaluator at assignment time, priced in `data/tuning/`. **Named here; not built here.**

⚠ **ssot-sockets §5.4's `bind_ordinal` request is module 16's**, not this module's — but a 4-ingredient
combination is exactly the case that makes two identical inserts tie in a sort `definitions.md` §5
requires to be total.

## Commands

```powershell
cd tools\seedsmith
python -m pytest tests/test_strain_splice_gen.py -q

python -m seedsmith items generate --kind combination --shape strain --dry-run
python -m seedsmith items generate --kind combination --shape splice --dry-run

python -m seedsmith check ..\..\data\seed\items --adapter items --gate
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Registration/IngredientUnsatisfiable
python -m seedsmith check ..\..\data\seed\items --adapter items --metric SemanticDedup/NearDuplicate
python -m seedsmith check ..\..\data\seed\items --adapter items --metric Coverage/EmptyPartition
```

⚠ **No `items` subcommand exists.** `build_parser` (`tools/seedsmith/seedsmith/report/cli.py:776-901`)
registers only `check`, `report`, `metrics`, `demons`, `effects`. Module 13 adds the group; this module
extends it. 102 entries is small enough not to need the `demons run` resume harness (`:869-871`), but the
gem-supply precheck below must run **before** the first call, not after.

## Project structure

```text
tools/seedsmith/seedsmith/adapters/items/combogen/          new
  grid.py         12 aptitudes x 3 archetypes -> 36; C(12,2) -> 66; ids sorted by ordinal
  brief.py        aptitude semantics from Aptitude.cs; NEVER the word "runeword"
  schema.py       closed-enum output; audit_schema-clean by construction
  supply.py       PRECHECK: every ingredient family a live gem supplies, before any model call
  emit.py         combo.strain-* / combo.splice-*; combinationKind + aptitudes
tools/seedsmith/seedsmith/adapters/items/kinds.py           EDIT — socket-word -> combination
tools/seedsmith/seedsmith/metrics/linkage.py                EDIT — the gating metric follows the kind
data/seed/items/socket-words/                               RETIRE — migrated to combinations/
data/seed/items/combinations/                               new — 102 entries, partitioned
data/tuning/strain-splice.v1.json                           new — per-actor cap, affinity tier bonus
```

## Code style

```python
# A Splice is an UNORDERED pair, so the id sorts the two aptitudes by their shipped ordinal
# (Aptitude.cs:40-51). Sorting at id-mint time makes C(12,2)=66 true by construction; a uniqueness
# check would only discover (might, agility) and (agility, might) after both had been generated.
def splice_id(a: Aptitude, b: Aptitude) -> str:
    lo, hi = sorted((a, b), key=lambda x: x.ordinal)
    return f"combo.splice-{lo.id}-{hi.id}"

# PRECHECK, before the first model call and not after: Registration/IngredientUnsatisfiable gates CI
# (linkage.py:155-194), and a 102-entry run that mints families no gem supplies is 102 wasted calls
# plus a red gate. The live gem corpus carries 40 entries across 34 families - a real constraint, not
# a formality.
def supplied_families(corpus) -> frozenset[str]:
    return frozenset(g["family"] for g in corpus.by_kind("gem") if g.get("family"))
```

## Testing strategy

| Test | Asserts |
|---|---|
| `the_grid_yields_exactly_36_strains_and_66_splices` | 12×3 and `C(12,2)`, against `Aptitude.All` |
| `a_splice_pair_is_unordered_by_id_construction` | `(might, agility)` and `(agility, might)` mint one id |
| `no_id_name_or_prompt_contains_the_word_runeword` | ⛔ D20, asserted over the emitted corpus **and** the brief |
| `the_schema_is_audit_schema_clean` | `audit_schema(COMBO_SCHEMA) == []` |
| `a_bare_integer_magnitude_field_fails_pipeline_construction` | mechanical P1, proven not asserted |
| `blocked_is_a_legal_answer_and_writes_nothing` | `on_persist` injected and never called |
| `every_combination_takes_exactly_four_ingredients` | D20 as amended (§2f.2) |
| `every_ingredient_family_is_supplied_by_a_live_gem` | the precheck, and the gating metric agrees |
| `IngredientUnsatisfiable_still_gates_after_the_kind_rename` | ⭐ the migration's real risk |
| `the_KINDS_assertion_still_holds_after_the_rename` | `kinds.py:104` — 15 kinds, renamed not removed |
| `no_shipped_base_type_can_host_a_four_ingredient_combination_today` | ⛔ the module-6 dependency, as a **failing** fixture that flips when `socketMax` is re-issued |
| `a_set_piece_may_not_carry_a_strain_or_splice` | D21's exclusivity, the one hard rule left |
| `matching_affinity_grants_an_enhanced_tier_and_never_gates` | D22 as amended — failure must be impossible |
| `a_mismatched_affinity_still_produces_the_combination` | the same rule from the abuse side |
| `no_aptitude_to_element_mapping_is_introduced` | ⚠ the gap stays visible; the bonus keys on gem element |
| `resonance_affinity_stays_a_plus_one_and_is_not_re_specified_here` | the two layers differ **on purpose** |
| `a_combination_grants_a_mechanism_not_a_flat_add_on_its_ingredients_channel` | §8.3's generator rule |
| `the_catalogue_size_is_reported_as_127_against_the_45_bar` | ⭐ the learnability debt is measured, not implied |
| `the_ward_array_hosted_legacy_entry_is_rehosted_or_retired` | it can never be worn by a hybrid |
| `re_running_over_an_unchanged_grid_is_byte_identical` | seedsmith's content-addressing law |

## Boundaries

**Always:** derive the grid from `Aptitude.All`, never a transcribed list; sort a Splice's pair by ordinal
at id-mint time; precheck gem supply before the first model call; resolve every magnitude through
`numerics`; keep the affinity bonus soft.

**Ask first:** rename-vs-retain on the `socket-word` kind (it moves a `gates = True` metric and the
`KINDS` assertion); any 12-aptitude → 6-element mapping; where the per-actor Strain/Splice cap is
enforced; the fourth `ContainerKind` value `combo` (effect-atom's `definitions.md` §1 + `ContainerRow.cs`).

**Never:** use the word *runeword*. Never let the model emit a weight, rate, duration or magnitude —
`audit_schema` rejects a numeric field mechanically, and **that check is the enforcement, not review**.
Never make matching affinity a **requirement** — §2f.2 reverted that and the hard version could never fail
on a crafted chassis. Never let a set piece carry a combination. Never invent an aptitude → element
mapping. Never raise `base_type.socket_max` above 4 — it is a **legibility limit, not a progression
ceiling**, and D23 requires that to be said in a comment where it is declared (module 6's).

## Success criteria

- [ ] 36 Strains and 66 Splices generated from the twelve-aptitude grid, Splice pairs unordered by id
      construction, and the word *runeword* appears nowhere in ids, names, briefs, tests or comments.
- [ ] Every combination takes exactly **4** ingredients, and every ingredient family is supplied by a live
      gem — prechecked before the first model call and confirmed by
      `Registration/IngredientUnsatisfiable`.
- [ ] Matching affinity grants an enhanced tier and **never gates**; a mismatched fill still produces the
      combination, proven by test.
- [ ] No 12 → 6 aptitude-to-element mapping is introduced; the gap is recorded, not filled.
- [ ] A set piece cannot carry a Strain or Splice; a low-rarity non-set chassis can.
- [ ] The `socket-word` kind is migrated rather than deleted, `kinds.py`'s 15-kind assertion still holds,
      and its gating metric still gates.
- [ ] The module-6 dependency is explicit and tested: a fixture proves no shipped base type can host four
      ingredients today, and flips when `socketMax` is re-issued.
- [ ] The 127-combination catalogue size is reported against §4.4's ~45 bar, and module 20's compendium
      reveal plus socket-UI preview are recorded as **requirements**, not niceties.
- [ ] Every magnitude comes from `numerics`; a numeric schema field fails `Pipeline` construction.
- [ ] Re-running over an unchanged grid is byte-identical.
