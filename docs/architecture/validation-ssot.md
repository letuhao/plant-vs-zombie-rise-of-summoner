# Validation — SSOT

**Status: binding.** Applies to every test, guard script, audit, and spec "Testing" block. Read this
before you write an assertion about a corpus, a roster, or generated content.

> **The rule: a guardrail validates the CONTRACT and the CLOSED enums. It never validates the size of a
> derived population, the total number of items, or the authored text of names and open enums.**
> And because the corpus grows every time content ships, the validation criteria must be **stable
> across generations** — a green suite today must still be the same suite tomorrow, not one that needs
> its numbers bumped.

---

## 0. The tax this exists to avoid

A test that asserts `len(species) == 904` does not guard anything. The day a 905th species ships it
fails, the "fix" is to change `904` to `905`, and it goes green again having caught nothing. What it
actually did was add a mandatory chore to every seed extension — and train the next reader to edit the
expected value instead of asking what the number *means*.

In the SeedSmith generation pipelines this is worse than a chore. **The creature-seed corpus is a
population: it grows whenever a new species ships, and that is the normal case, not an event.** A
pinned literal makes the pipeline look broken every time it succeeds at its job. The owner's own words:

> *"the corpus will extend frequently when new creature species ship — it affects all seed — so the number
> of seed will never a constant, it change every day."*

This is the same defect the repo already logged once, from the other side: a session proposed
rewriting `DerivedStatRegistryTests` to "replace the literal 84 with the formula" when the test
**already computed** the formula and the literal was a deliberate canary
(`DESIGN-GATE.md` §4, 2026-08-24). Both directions come from the same confusion — not knowing whether
a number is a *constant* or a *reading*.

---

## 1. The test: closed vocabulary, or derived population?

Before writing any assertion, classify what you are asserting about. **This is the whole standard** —
the rest is mechanics.

| | **Closed vocabulary** | **Derived population** |
|---|---|---|
| What it is | An enum / registry the *code* owns. It changes only when a developer edits a declaration | A set that grows when *content* ships (species, families, items, anchors, corpus rows) |
| Cardinality | **Constant** — pin it | **A reading** — never pin it |
| Examples | `ActionCategory` (5) · `ActionTargetMode` (6) · 18 atom kinds · 9 attach points · 13 triggers · 6 `resource.*` ids · `CreatureRarity` ladder (10) · the 14-trait `CreatureTraitPool` · `AREA_SHAPES` · `PAIRING_ROLES` · `RELATIONS` | species (904 today) · family memberships (1,183) · consolidated families (227) · anchors · briefs (6,655) · accepted/rejected rows · description text · `atomFamilies` picks |
| A test may assert | The exact count and the exact members — **and say why the number is the contract** | Only the **contract** (§2). If a reader needs the scale, use a canary (§4) |
| If it changes | The test *should* fail — a declaration moved and a human must review it | The test *must not* fail — content shipped, which is the point |

**The tell.** Ask: *"who changes this number?"* If a **developer** changes it by editing code, it is a
closed vocabulary and a pinned literal is correct. If a **content author / generator** changes it by
adding a seed row, it is a derived population and any literal is a future false failure.

**Verify by counting, then assert the contract.** `DESIGN-GATE.md` §3 rule 5 ("verify counts by
counting") and this standard do not conflict: you *count* to know the current scale and to write an
honest canary or a comment; you *assert* the relationship, not the count.

---

## 2. What a guardrail MAY assert (the contract)

Every one of these is stable across generations — adding a species, a family, or a brief does not
break them, but breaking the *structure* does. That is what a guardrail is for.

| Contract | Pattern (Seedsmith action pipeline) |
|---|---|
| **Envelope / schema** | `kind == "action-brief"`, `schemaVersion == 1`, every required key present with the right type |
| **Closed-enum membership** | every `slot.category ∈ ActionCategory`; `targetMode ∈ ActionTargetMode`; `areaShape ∈ AREA_SHAPES`; `pairing.role ∈ PAIRING_ROLES`; `structureAxes ⊆ rung axes`; `rarity ∈ RARITY_LADDER` |
| **Join / closure** | every motif key ⊆ catalog ids; every `family` value ∈ the family namespace; `scopeKey` resolves; every referenced atom family exists in the vocabulary |
| **Uniqueness** | `briefId` unique; authored `name` unique *per kind*; `speciesId` unique |
| **Internal reconciliation** | `Σ categoryMilli == 1000`; scope quota == plan counts; `Σ memberships == family-assigned count`; `len(entries) == Σ per-scope counts` |
| **Cross-artifact consistency** | plan `corpusHash` == the hash of its live inputs; the coverage report's recomputed quota == the plan's actual counts; `role-lean` keys == catalog ids |
| **Determinism** | same inputs → byte-identical output; shuffle the input order → same allocation |
| **Structural bounds** | non-empty (a description with no text is a defect); within a declared length limit; **no magnitude smuggling** (`audit_no_magnitude_smuggling`) |
| **Coverage of a domain** | every catalog id appears at least once; every closed enum member is reachable |

The first six are the heart of it. They answer *"is this artifact internally consistent and does it
join to its neighbours?"* — the question a content pipeline actually needs answered.

---

## 3. What a guardrail MUST NEVER assert

| Forbidden | Why | Instead |
|---|---|---|
| A population size or corpus total (`904`, `227`, `1,183`, `5,680`, `6,655`) | It is a reading; it moves when content ships | Assert the reconciliation: `len(species) == len(catalog_ids)`, `Σ memberships == assigned` |
| The **generated text itself** — a `name` equals `"Emberlash"`, a `description` equals a string | It is authored output; the model is *allowed* to change it | Assert it is non-empty, unique per kind, within length bounds, and free of smuggled magnitudes |
| An **open-enum value** — a specific `atomFamilies` pick, a specific motif list | Open vocabularies are the generator's choice within the contract | Assert membership in the closed set and non-emptiness, not the particular pick |
| The **generation-cycle outcome** — accepted == 31, rejected == 36, unresolved == 7 | It changes every cycle; a test that pins it oscillates red/green on model nondeterminism | Assert the invariants (every terminal row is classified; no candidate is both; totals reconcile) |
| Any claim read from a **sibling document** rather than counted from the source | `DESIGN-GATE.md` §3 rule 5 | Count it, or assert the relationship |

**"Quality of generated text" does not mean the text's value.** It means the text passes the contract:
present, unique, bounded, non-smuggling, and joined. A checker that asserts a *particular* generated
string is a snapshot test of the model, and it will fail the first time the model is reseeded — the
opposite of a quality gate.

---

## 4. The canary pattern — keeping the human-readable scale

Sometimes a reader genuinely benefits from seeing the current scale in the test output (a coverage
test, a "how big is this corpus" smoke). Do not pin it. Compute the formula, assert the live value
equals the formula, and let the number appear only as a printed reading or a comment:

```python
def test_catalog_covers_every_seed_record(self) -> None:
    """The contract: the catalog is exactly the records on disk, one row each."""
    on_disk = _count_species_records(SPECIES_ROOT)          # recomputed, never a literal
    catalog = load_catalog()
    self.assertEqual(len(catalog), on_disk)                 # relationship, not a number
    self.assertEqual(len({r.species_id for r in catalog}), on_disk)   # and unique
    # Scale, for the reader -- a print, not an assertion:
    print(f"live species roster: {on_disk}")
```

The C# precedent is `ElementRosterDataTests.The_channel_count_is_the_formula_not_a_literal` — it
asserts `families.Count × (elements.Count + 1) == ids.Count` and states the scale in a comment. Copy
that shape. If a count must appear in a message, build it from the recomputed values, so the message
stays true when the roster grows.

---

## 5. Where this is enforced

- **`DESIGN-GATE.md`** §3 evidence rule 7 (added 2026-09-11) and the §5 checklist line.
- **`CLAUDE.md`** and **`AGENTS.md`** hard boundaries — the rule in full, because a pointer does not
  survive a long session.
- **Each module spec's "Testing" block must not name a population literal.** "Roster size" acceptance
  criteria are relationship assertions; a spec that wrote `exactly 84 species rows` (or its later
  `exactly 904`) is corrected on sight — that wording is what produced the stale-number hunt in the
  first place.
- **Guard scripts** (`scripts/guard-*.ps1`) validate structure and closed sets, never row totals.

---

## 6. What this is not

- **Not a ban on pinned counts.** A closed vocabulary's count *is* its contract; pin it and say why.
  `ActionCategory` has 5 members and a sixth is a reviewed change — assert 5.
- **Not a licence to assert nothing.** "The corpus is whatever it is" is not a contract either. The
  contract is all of §2, and it is stricter than a count: a count can be bumped, but a broken join,
  a duplicate id, a non-reconciling sum, or a smuggled magnitude cannot be shrugged past.
- **Not retroactive busywork.** Convert assertions that pin a derived population or generated text.
  Leave a closed-vocabulary count alone.

---

## 7. Decided

| Date | Decision |
|---|---|
| 2026-09-11 | Standard created after a session "fixed" the stale `84` by replacing it with `904` — the same defect one generation later. The roster is a population that grows per shipped species; the corpus is a reading. Guardrails assert the contract (envelope, closed enums, joins, uniqueness, reconciliation, determinism, bounds) and never the population size, the item total, the generated text, or the per-cycle outcome. Validation must be stable across generations. |
