# Spec: eligibility-axis (A-E1)

**Status: DRAFTED 2026-09-03** — added by the spec-coverage audit as the **highest-damage orphan in
either program.** Module **A-E1**, action-corpus. **No dependencies. Nothing in this program is useful
without it.**

> **⚠️ SCOPE WIDENED 2026-09-03** by the adversarial spec review (finding F1). This module was written to
> own `scope`/`scopeKey` alone. **Five more corpus fields have no home in `ActionRow` either**, and every
> one is load-bearing downstream — so this module owns the whole schema surface, or the corpus is
> authored into a row that cannot hold it. See §3.0.

**What it owns: the surface that says who may hold an action, and the fields the corpus needs to say it.** The corpus generates actions in three
eligibility tiers — general, family, signature. **Nothing in the code can express that distinction.** So
every stage can run to completion, commit a corpus, and the game still cannot decide which action a
species may unlock.

---

## 1. Why this is the founding gap

The ideal names it as its own **⭐ real gap** and as Part I's module 1 — *"the one to insist on… until it
exists the unlock ladder — **fully built, fully tested** — cannot be called by anything but a test."*

And a spec written before this one already says nobody owns it
(`spec-corpus-loader.md:138-141`):

> *"Never invent the C# eligibility surface. `ActionRow` has no field naming who may hold an action…
> **No module in map §4 owns it** — recorded here so the gap stays visible rather than being absorbed by
> this one."*

**Six specs carry `scopeKey` as a brief or seed field.** None makes the runtime able to answer the
question those fields describe. **A corpus with no eligibility surface is content nothing can read.**

---

## 2. What exists today

| Thing | State | Evidence |
|---|---|---|
| `ActionRow` has **no** field naming who may hold an action | ⛔ **real gap** | `ActionRow.cs:18-53` — verified field by field |
| The only `scope` in the action layer is **effect** scope, an unrelated concept | **built** | `ActionRow.cs:72` — `ActionScopeRow(ActionId, AtomId, ActionEffectScope)`, defaulting to `EachTarget`. **Do not overload it** |
| `UnlockLadder` / `UnlockState` | **built, fully tested** | and reachable only from tests, because nothing can compute a candidate set |
| `SpeciesBasics.SpeciesKey` is an **opaque key**, deliberately not a catalog join | **built** | `ActionRow.cs:83` states this |
| Family assignments are keyed on **demon ids**, not `species_key` | **built**, mismatched | `data/seed/demons/_generated/family-assignments.json`; `grep species_key tools/seedsmith` returns nothing |
| Whether `scope`/`scopeKey` is a column or a table | ⛔ **an open decision** the ideal §6 defers | — |

**Sorted: real gap**, and the only one in this program that no amount of content generation can route
around.

---

## 3. The contract

### 3.0 ⛔ Six fields, not two — the full gap (F1)

`ActionRow`'s complete field list, read field by field (`ActionRow.cs:15-54`): `ActionId, Name, Kind,
Rung, Tags, Enabled, Revision, Grantable, DefaultAttackEligible, ContainerId, Envelope, Targeting,
MinRange, MaxRange, RangeChannel, RequiresLineOfSight, ConditionsJson`. `CompiledAction.cs:17-35`
matches.

| Corpus field | In `ActionRow`? | Who depends on it |
|---|---|---|
| `scope` / `scopeKey` | ⛔ no | the candidate-set query — §3.1, §3.2 |
| `category` | ⛔ no — `ActionCategory` **exists** (`ActionEnums.cs:26-33`) but names derived channels only (`DerivedStatChannels.cs:471-486`) and **sits on no row** | A-S3's fingerprint · A-S5's cell key · A-T1's `categoryMilli` · A-S6's `roleLeanMatch`/`categoryScarcity` · A-M1's `category = Movement` rejection |
| `pairingRole` | ⛔ no | A-S3's fingerprint · A-S5's coverage metric |
| `structureAxes` | ⛔ no | A-S3's fingerprint · A-S4's g2 |
| `atomFamilies` | ⛔ no | the pool references E30 makes possible. ⛔ **RENAMED 2026-09-03 (review):** this row said `atomPools`, one of **four** names in circulation for what read as one field. Resolved in `spec-distribution-planner.md` §3 step 8: `atomFamilies` is the canonical stored name (the code of record's own word — `AtomRow.FamilyId`, `ActionSeeder.cs:61`; `IsPayoff(string atomFamily)`, `EnablerPayoffPairings.cs:26`); `allowedAtomFamilies` is the **brief's permitted set**, a genuinely different thing; `sortedAtomFamilies` is a fingerprint rendering, not a field |
| `rungBand` | ⛔ no — `Rung` is a **single `int`** (`ActionRow.cs:23`), not a band | A-S1's windows · A-S4's g2 · A-S6's `-rungCeiling` |

**The program declares itself content-only** (`action-corpus-map.md:20-22` — *"It does not build the
action runtime"*), which is why no module claimed these. **That declaration is right about the runtime
and wrong about the schema:** a corpus needs somewhere to be written.

**So this module owns the schema extension, and only the schema extension.** It adds fields and the one
query that reads them. It does not touch `ActionCompiler`, `ActionValidator`'s existing rules, the
loadout, or the ladder's logic.

⚠️ **`rungBand` → `Rung` needs a stated collapse rule.** `StructureBudgetGuard.Check` resolves
`rungTable.TryGet(row.Rung)` — **one** row — while a `[5,10]` band spans budgets of 3 and 7 axes. A band
that silently becomes its minimum, or its maximum, is a balance decision made by an implementation
detail. **State the rule here or the guard checks the wrong budget.**

### 3.1 The axis

Two fields on the action row:

| Field | Values | Meaning |
|---|---|---|
| `scope` | `general` · `family` · `species` | Which tier's eligibility rule applies. **Exactly the three the corpus generates** — A1 fixed this at three, because `ActionKind` already distinguishes innate from unlocked and a fourth value would encode the same fact twice |
| `scopeKey` | `null` for `general`; a family id for `family`; a species key for `species` | The specific holder set |

**`scope` is a closed enum in code**, following every other action vocabulary. **`scopeKey` is an opaque
string**, matching `SpeciesKey`'s existing discipline — the action layer deliberately does not join into
the demon catalog, and this module must not be the thing that introduces that coupling.

**Column, not table.** Every action has exactly one scope; a table would model a many-to-many that does
not exist, and would make the candidate-set query a join instead of a filter. **The ideal deferred this
decision; this spec makes it and states the reason so it can be overturned on evidence rather than
taste.**

### 3.2 The candidate-set query

The one operation the whole program needs:

```
candidates(actor) = { a : a.scope = general }
                  ∪ { a : a.scope = family  ∧ a.scopeKey = familyOf(actor) }
                  ∪ { a : a.scope = species ∧ a.scopeKey = actor.speciesKey }
```

- **Deterministic order.** The result is sorted by `actionId`, ordinal — every downstream roll depends on
  it, and an unordered candidate set makes replay undefinable.
- **A miss is empty, never everything.** An actor whose family is unknown gets the general tier only.
  **The failure mode to design against is a null `scopeKey` silently matching all rows.**
- **`familyOf(actor)` is the join that does not exist.** Family assignments are keyed on demon ids;
  `speciesKey` is opaque. **This module owns defining that resolution — and it must be a real mapping,
  not a string-equality accident.**

### 3.3 What it plugs into

`UnlockState.TryAccept` already takes an unlock id and is fully tested. This module gives it a candidate
set to be called *with* — so the ladder stops being reachable only from tests.

---

## 4. What this module must NOT do

- **Overload `ActionEffectScope`.** It answers *"does this atom apply once or per target"* — a different
  question that happens to share a word.
- **Make `speciesKey` a foreign key.** `ActionRow.cs:83` records that as deliberate; the family
  resolution is a lookup this module owns, not a schema coupling it introduces.
- **Add a fourth scope value.** A1 closed this at three.
- **Let a null `scopeKey` match anything but `general`.** A null that matches everything is the worst
  failure this module could ship — it would silently make every species-specific action universal.
- **Decide eligibility with a model.** This is a data structure and a query.
- **Duplicate `eligibility-tags`.** `effect-pipeline` module 8 owns **tag-based affix** eligibility with
  per-container allow/deny. **That is a different axis on a different entity** — affixes on containers,
  not actions on actors. Two vocabularies for one concept is the defect `spec-action-seeding.md` §3 names;
  **this spec must state the boundary and hold it.**

---

## 5. Testing strategy

| # | Test | Proves |
|---|---|---|
| 1 | `candidates(actor)` returns the general tier plus that actor's family and species rows, and **nothing else** | The core query |
| 2 | An actor with an **unknown family** gets general-tier rows only | §3.2's miss rule |
| 3 | **Planted violation:** a `species`-scoped row with a null `scopeKey` **does not** appear for an unrelated actor | The worst failure mode, asserted |
| 4 | The candidate set is **ordinally sorted and stable** across two calls | Replay is definable |
| 5 | `UnlockState.TryAccept` is driven **from a real candidate set**, not a test fixture | The ladder stops being test-only |
| 6 | A `family`-scoped row whose `scopeKey` names no known family is a **load-time refusal** | No orphan rows |
| 7 | **Planted violation:** a fourth `scope` value fails to compile or is refused | A1's closure held mechanically |
| 8 | `ActionEffectScope` is untouched — its defaults and behaviour unchanged | §4's first boundary |

**Test 3 is the one to write first.** Everything else is correctness; test 3 is the difference between a
working eligibility system and one that silently grants every species action to everybody.

---

## 6. Acceptance criteria

1. `scope` (closed, three values) and `scopeKey` (opaque string) exist on the action row and persist.
1b. `category`, `pairingRole`, `structureAxes`, `atomFamilies` and `rungBand` exist and persist, and
   the `rungBand` → `Rung` collapse rule is stated and tested (§3.0). ⛔ **The rule is now stated**:
   `Rung = rungBand[1]`, the band's **ceiling**, in `spec-distribution-planner.md` §3 step 4 — the
   only value consistent with that module's union-to-ceiling structure-axis rule, since
   `StructureBudgetGuard.Check` must resolve the same row the axes were drawn from
   (`StructureBudgetGuard.cs:41`).
1c. `ActionCategory` is **reused**, never redeclared — it ships already and a second categories vocabulary
   is the exact defect `spec-action-seeding.md` §3 names.
2. `candidates(actor)` implements §3.2 exactly, ordinally sorted.
3. A null `scopeKey` matches **only** `general` — asserted by a planted violation.
4. An unknown family yields general-tier only.
5. `familyOf(actor)` is a defined mapping, not string-equality luck, and its failure is empty rather than
   wrong.
6. `UnlockState.TryAccept` is exercised from a real candidate set in at least one test.
7. `ActionEffectScope` is unchanged.
8. The boundary against `effect-pipeline` module 8's tag eligibility is stated in the map, not only here.

---

## 7. Dependencies and cross-program hazards

| | |
|---|---|
| **Depends on** | Nothing. **Should be built first in this program** — six specs already reference `scopeKey` as a field |
| **Blocks** | Every generation stage in a practical sense: the corpus is unusable without it |
| **seedsmith D2** | Family assignments cover **53 of 84** species and are keyed on demon ids. `familyOf` will return empty for 31 species — **correct behaviour, and it means the family tier reaches 53** until the assignments grow |
| **effect-pipeline module 8** | `eligibility-tags` — a different axis on a different entity. Keep them apart |
| **`action-corpus-ideal.md` §6** | Deferred the column-vs-table decision; §3.1 makes it with a stated reason |
