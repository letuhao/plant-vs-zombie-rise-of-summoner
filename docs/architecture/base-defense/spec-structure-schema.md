# Spec: `structure-schema`

**Module 23 of 29 · level c0 · no dependencies · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04. **Folded in by owner decision 45** — `structure-seed` is now a module set
inside this program, not its own. Design record: [structure-seed-ideal.md](../structure-seed-ideal.md).

---

## Objective

**The seed contract for a structure: seventeen fields, and not one of them is a number.**

That last clause is the module. Seedsmith **Law 2** — the model writes identity, deterministic code
writes magnitude — is *"enforced mechanically by a schema audit, never by review"*, and this is the
schema plus its audit.

> *"a model has no calibrated sense of scale, so a number it picks is a plausible-looking guess that
> survives review because nothing looks wrong with it."* A wrong enum is visible. `hp: 4200` reads
> exactly as plausibly as `hp: 2400`, and over hundreds of rows nobody re-derives it.

**This is a level-0 module and it calls no model.** *"A parse, a table, a schema and a dump produce real
value with zero tokens spent, and they make the expensive stage's inputs reviewable."*

---

## What already exists (verified at HEAD, 2026-09-04)

**Built.**

- The **species anchor** shape — **415** files under `data/seed/demons/species/plant/`, **503** across
  all species (measured 2026-09-04; the ideal's *"408"* and an earlier draft's *"~841"* are both stale —
  841 was the seedsmith **stage-run** figure, 841 anchors × 8 pipelines). Carries the `_provenance` and
  `_derived` machinery the ideal says to *"copy wholesale"*.
- **`data/seed/` holds sixteen domains and none is structures** — actions, aptitudes, atoms,
  channel-policy, channel-pools, containers, curves, demons, derived-stats, elements,
  external-reference, items, loot, rarity, resources, zomboss. This module adds the seventeenth.
- `StructureCatalog` — **four hand-authored C# rows** and a `Validate` that already enforces kebab ids,
  no duplicates, no negative cost. The validation stance to extend.
- `WorldIds.RequireKebab` — the id rule.
- `SlotKind` — a shipped enum, **14 values**, which `requiredSlotKind` validates against.
- The ten-rung rarity ladder ([ssot-rarity.md](../item/ssot-rarity.md)) — shared, and per §4 it buys
  *breadth and ceiling, never power*.

**Real gap.** No structure seed schema, no `data/seed/structures/` subtree, no audit.

---

## The contract

### 1. The anchor — seventeen fields, mapped from the species anchor

#### ⛔ Four ownership levels, not three — pass 3

§1 law 4 names **four**, and *"a field with none is a contract defect"*:

| Level | Means |
|---|---|
| `AUTHORED` | the agent chooses |
| `DERIVED` | the importer computes |
| **`GENERATED`** | **a generator emits rows** — the level `structure-pipeline`'s output needs, and which an earlier draft of this spec omitted entirely |
| `VALIDATED` | the author names it, a **frozen registry** owns it |

> *"Naming a value and owning a value are different rights."*

A row produced by `structure-pipeline` is `GENERATED`; a row in `structure-corpus` is `AUTHORED`. **The
schema records which**, so provenance is a field rather than a convention.

Field-for-field, with ownership levels:

| Field | Level | Notes |
|---|---|---|
| `structureId` | AUTHORED | kebab, registry-checked |
| `family` | AUTHORED | "earthwork", "emplacement", "works" |
| `role` / `roleSecondary` | VALIDATED | the ten of §4; `none` legal |
| `requiredSlotKind` | VALIDATED | the shipped 14-value enum |
| `elementPrimary` / `elementSecondary` | VALIDATED | attuned works; `none` legal |
| `tempo` | AUTHORED ordinal | emplacements only; `none` otherwise |
| `reach` | AUTHORED ordinal | deterministic layer turns it into cells |
| **`strengthBand`** | AUTHORED ordinal | → HP and damage via `P(Θ)`. **See §2 — this is decision 32's material tier** |
| `rarity` | VALIDATED | shared ten-rung ladder; breadth and ceiling only |
| `traits` | VALIDATED | decision 11 gives structures traits |
| `costProfile` | AUTHORED ordinal | *which* materials in what **ratio band** — never an amount |
| `targetPreference` | VALIDATED | §5.20's First / Last / Close / Strong |
| `variants` | AUTHORED | a tier chain is a variants list, **not four rows** |
| **`acquisitionPaths`** | VALIDATED | **See §3 — the reconciliation decision 35 requires** |
| `footprint` | AUTHORED ordinal | one cell / small / large |
| `coverTier` | AUTHORED ordinal | none · light · heavy · trench |
| `controlPoint` · `obstacleVerbs` | **DERIVED** | from role + slot kind |
| `reason` | AUTHORED | free text — identity, never magnitude |
| `_provenance` · `_derived` | — | copy the species machinery wholesale |

**`side` is deliberately absent.** Decision 12: structures have no ownership. *"A `side` field would be
a lie."*

#### ⛔ `role` (10) and `StructureKind` (3) — reconciled, pass 3

§2.3 names the mismatch without resolving it: *"`StructureKind` has 2 values … §5.21 names ten roles."*
(`siege-construction` adds `Refinery`, making three.) Two vocabularies over one concept, and left alone
one of them becomes decoration.

| | `role` | `StructureKind` |
|---|---|---|
| Lives in | the **seed** (10 values, §5.21) | the **catalog** (3 values) |
| Answers | *what is this structure for?* | *which engine path reads it?* |
| Example | `Extract`, `Refine`, `Store`, `Deny`, `See` | `LoamSource`, `Storage`, `Refinery` |

**`StructureKind` is DERIVED from `role`, never authored beside it.** Extract and Multiply → `LoamSource`;
Store and Bank → `Storage`; Refine → `Refinery`; every other role → a kind that reads as *"no economic
engine path"*. One authored vocabulary, one derived discriminator, and the mapping is a table in this
module rather than a judgement made twice.

**A role with no kind mapping throws at load** — the same loud-over-silent stance `Validate` already
takes.

### 2. `strengthBand` IS decision 32's material tier — one ordinal, not two

Decision 32 says HP is `P(Θ_development) × an authored material tier`, from *"llm to generate variant
like stone wall, iron wall that iron wall have more defense than stone wall."*

**`strengthBand` already is that ordinal.** Adding a separate `materialTier` beside it would be two
ordinals feeding one magnitude — the second-vocabulary defect, and a guaranteed source of "which one
wins" bugs.

```jsonc
"strengthBand": "iron"   // an ORDINAL on a declared ladder: rubble < timber < stone < iron < ...
```

The **ladder itself is planned, not emergent** (decision 33): `structure-planner` fixes which tiers
exist and in what order **before any model call**, and the model then picks one. Without that, the
ordering the mechanics rest on is whatever the model happened to name.

`structure-state.MaxHpOf` reads the ordinal → `StructurePolicy.TierMultiplierMilli` → `× P(Θ)`.
**The interval lives in tuning; the ordinal lives here.**

### 3. `acquisitionPaths` vs `acquisition` — reconciled, not stacked

Decision 35 adds `acquisitionPaths`; §5 already had `acquisition`. **Two different questions:**

| Field | Asks | Values |
|---|---|---|
| ~~`acquisition`~~ | how it reached the **map** | built · authored-on-map · captured |
| **`acquisitionPaths`** | how it reaches the **board** | built · assembled · summoned · laboured |

**Resolution: keep only `acquisitionPaths`, and drop `acquisition`.** The map-scope question is
answerable from world state — an authored-on-map structure is one present at turn zero; a captured one
is one whose sector changed hands. It is **derivable**, and a seed field for a derivable fact is a
second source of truth that will disagree.

`VALIDATED`, a subset of the four, **`none` illegal** — a structure no path can produce is a catalog row
that can never appear on a board.

### 4. The audit — this is the module, mechanically

```python
# The one law, enforced by a scan rather than by review.
# Every field is an enum, an ordinal, a registry id, or free text. NOT ONE IS A NUMBER.
def audit_no_magnitudes(anchor: dict) -> list[str]:
    ...
```

Rejects any numeric value outside the declared identity keys. **A schema audit, not a linting
suggestion** — it fails the build.

Three more audits from the AI-native contract, all cheap:

| Audit | Rule |
|---|---|
| **`none` is a value; a missing key is a defect** | Tag absence is a stat. A missing key is rejected; `"none"` is accepted |
| **Every description carries a negative clause** | *"a description with no negative clause is half-written"* — say what the field is **not** |
| **Closed structure, not a frozen vocabulary** | *"a well-defined structure with a description per attribute"* |

### 5. The subtree

```
data/seed/structures/
  _manifest.json
  _index.json
  works/<structure-id>.json
```

Mirroring `data/seed/demons/species/`'s shape, because a reviewer already knows that shape.

**Generated rows are committed.** *"A generated row nobody can diff is a row nobody can review."*

---

## Tunables

`data/tuning/structure-seed.v{n}.json`. **Every number this program's structures use lives here, and
none of them lives in a seed file** — that split is the whole point.

| Block | Rows |
|---|---|
| `bands` | ordinal → interval for `strengthBand`, `reach`, `tempo`, `footprint`, `coverTier` |
| `cost` | `costProfile` ratio band → material amounts |
| `budget` | per-role target counts, for the skew guard (`structure-metrics`) |

## Numeric types

**None in the schema — that is the invariant.** The ordinals it carries become `long` magnitudes only
after the deterministic layer reads them (`structure-state`), and `long` because
`CLAUDE.md` rule 1 covers HP and damage unconditionally.

## Boundaries

**Always:** every field an enum, ordinal, id or free text · `none` a value, a missing key a defect ·
a negative clause per description · commit generated rows.

**Ask first:** a second axis beyond `role` (§4 says **element** is the honest candidate; **tier is
not** — *"a stronger version is not a different unit"*).

**Never:** a numeric field in a seed row · a `materialTier` beside `strengthBand` · re-add
`acquisition` · a `side` field · a tier chain as four rows instead of a `variants` list.

---

## Testing

Tests **never call a model** — stub the transport so it *raises*.

| Test | Asserts |
|---|---|
| `No_seed_field_holds_a_number` | **the module's whole point**, over every committed row |
| `A_missing_key_is_rejected_and_none_is_accepted` | tag absence is a stat |
| `Every_field_description_has_a_negative_clause` | |
| `Strength_band_is_the_only_magnitude_ordinal` | no `materialTier` beside it |
| `Acquisition_paths_may_not_be_empty` | `none` illegal |
| `No_acquisition_field_exists` | §3's reconciliation, enforced |
| `No_side_field_exists` | decision 12 |
| `Every_field_declares_one_of_the_FOUR_ownership_levels` | **P3-4** — a field with none is a contract defect |
| `Generated_rows_are_marked_generated` | provenance as a field, not a convention |
| `Structure_kind_is_derived_from_role` | **P3-6** — never authored beside it |
| `A_role_with_no_kind_mapping_throws_at_load` | loud over silent |
| `Ids_are_kebab_and_unique` | `WorldIds.RequireKebab` |
| `Required_slot_kind_validates_against_the_shipped_enum` | all 14 |
| `Schema_audit_fails_the_build_on_a_numeric_field` | not a warning |
| `Transport_stub_raises_if_a_test_calls_a_model` | the discipline, enforced |

## Success criteria

1. The schema exists, and **no committed row holds a number** — proven by scan.
2. `strengthBand` is the single magnitude ordinal, and is decision 32's material tier.
3. `acquisitionPaths` replaces `acquisition`; both do not ship.
4. The audit fails the build rather than warning.
5. Zero model calls anywhere in this module.

## Open questions

None. The second-axis question is recorded as an *Ask first*, with §4's own answer (element, not tier).
