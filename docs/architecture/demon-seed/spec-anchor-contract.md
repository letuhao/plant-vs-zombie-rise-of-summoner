# Spec: `anchor-contract`

**Module id:** `anchor-contract` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 2 of 16
**Model calls:** none — this module defines the structure the pipelines are constrained by.

## Objective

Define the species anchor: a JSON structure with a prose description on every attribute, an explicit
`none` on every closed enum, a declared ownership level per field, and a schema audit that
**mechanically rejects any numeric field**.

Owner, Q22 — and this is the sentence the whole module is built on:

> *"closed contract is not closed enum, it is well defined structure json, so LLM know how to generate
> each attribute in json because it understand the description of each attribute. each pipeline must
> cover 1 or some attributes."*

**Reliability comes from structure plus description, not from a short value list.** A frozen
vocabulary is a planning convenience; the descriptions are the mechanism.

## Design

### 1. The anchor is a seed, so it obeys the seed contract's four ownership levels

[item/seed-contract.md](../item/seed-contract.md) §2: *"A field with no declared level is a contract
defect, not an author's judgement call."* Every anchor field declares one:

| Level | Who sets it | Lives in |
|---|---|---|
| **CAPTURED** | the dump — copied, never chosen | the seed, echoed for provenance |
| **CLASSIFIED** | a pipeline chooses it from a described vocabulary | the seed |
| **DERIVED** | computed from other anchor fields by rule | never in the seed |
| **GENERATED** | `species-generator` emits it into `data/generated/` | never in the seed |

Every magnitude is GENERATED. **The anchor holds no numbers at all**, which is what makes the audit in
§4 mechanical rather than a judgement call.

### 2. The eighteen attributes

| Attribute | Level | Vocabulary | `none` legal |
|---|---|---|---|
| `side` | CAPTURED | `plant` · `zombie` | no |
| `speciesId` | CAPTURED | from `TypeName` | no |
| `gameTypeId` | CAPTURED | int, an identifier — **not a magnitude** | no |
| `elementPrimary` | CLASSIFIED | fire · ice · air · earth · light · dark | no |
| `elementSecondary` | CLASSIFIED | the same six, or `none` | **yes** |
| `aptitudePrimary` | CLASSIFIED | the twelve aptitudes | no |
| `aptitudeSecondary` | CLASSIFIED | the twelve, or `none` | **yes** |
| `posture` | DERIVED | Force · Finesse · Bastion, from `aptitudePrimary` | no |
| `pure` | DERIVED | bool — both aptitudes share a posture (Q2) | no |
| `threatBand` | CLASSIFIED (audited) | ten threat nouns, `threat-band`'s ladder | no |
| `rarity` | CLASSIFIED | the ten item rungs, `chaff`..`almanac` | no |
| `deployMode` | CLASSIFIED | `PlantAvatar` · `HypnoAlly` | no |
| `acquisition` | CLASSIFIED | flags: Summonable · CaptureOnly · EventOnly | no |
| `variants` | CLASSIFIED (named) / DERIVED (count) | seven known variants; **count comes from rarity** | no |
| `resourceProfile` | CLASSIFIED | subset of hp · stamina · hunger · spirit · qi · **poise** | no |
| `basis` | DERIVED | observed · stated · inferred · blocked | no |
| `family` | CLASSIFIED, **open** | grows organically | no |
| `traits` | CLASSIFIED, **open** | grows organically | no |
| `attackTempo` | CLASSIFIED | ponderous · slow · steady · quick · flurry | no |
| `reach` | CLASSIFIED | melee · short · long · siege | no |
| `targetPreference` | CLASSIFIED | frontline · backline · swarm · elite · structure · indiscriminate | no |

That is twenty-one keys for eighteen design variables — `speciesId`, `gameTypeId` and `pure` are
bookkeeping the ideal doc's count did not include. **Stated plainly so a reader does not think a field
was smuggled in.**

`resourceProfile` includes **poise**. The owner caught this omission directly: `DerivedStatChannels.cs`
registers six actor resources, not five.

### 3. `none` is a value, never a missing key

Ideal §6.2 ③, from SC2's Archon, Ghost, Ravager, Baneling and Queen carrying neither Light nor Armored
and thereby being immune to a large share of every bonus-damage term in the game:

> **Tag absence is a stat.**

So: a missing key is a **schema violation**, not an unsure model. Constrained decoding must make the
key unskippable, and the validator rejects `additionalProperties` and any absent required key. A model
that does not know says `none`; it never says nothing.

### 4. The numeric audit — mechanical, not editorial

Seedsmith already has this check. `audit_schema` rejects a schema declaring `"type": "number"` or
`"type": "integer"` in a generated field. This module extends it with three cases the existing check
would miss:

1. a `string` field whose `pattern` admits a bare number (`^[0-9]+$`)
2. an `enum` whose members are numeric strings (`"1"`, `"2"`)
3. a field named in the "magnitudes" deny-list (`hp`, `atk`, `damage`, `defense`, `cost`, `weight`,
   `chance`, `permille`, anything ending `Milli`)

`gameTypeId` is the one legal integer and is allow-listed **by name with a comment saying why**: it is
an identifier for a lookup, it never enters arithmetic, and it is CAPTURED rather than classified.

### 5. Descriptions are the deliverable, not documentation

Each attribute carries a `description` that a model reads at generation time. It states what the field
means, what distinguishes adjacent values, and what the field is *not*:

```json
"reach": {
  "type": "string",
  "enum": ["melee", "short", "long", "siege"],
  "description": "How far this creature can affect a target. 'melee' touches the adjacent cell only. 'short' covers a few cells ahead. 'long' covers most of a lane. 'siege' outranges the lane and is usually paired with a slow tempo. This describes REACH, not movement speed and not area of effect - a creature that walks fast but hits only what it touches is 'melee'."
}
```

**The negative clause is not padding.** §4.7's finding is that enum selection is the most bias-prone
task shape there is; the most common error is a plausible neighbouring value, and the sentence that
prevents it is the one saying what the field is not.

### 6. Vocabularies may grow; the structure may not drift

Q22 demoted ideal §6.2 ④ from a constraint to planning information. `family` and `traits` are open by
construction. `threatBand` is the designated growth axis because nothing consumes it yet. `element`,
`aptitude` and `rarity` are expensive to widen and stay put — **as a cost statement, not a rule.**

## Commands

```powershell
python -m seedsmith demons contract --print          # the resolved JSON Schema
python -m seedsmith demons contract --audit          # the numeric audit, exit 1 on a finding
python -m pytest tools/seedsmith/tests/test_anchor_contract.py
```

## Project structure

```text
tools/seedsmith/seedsmith/adapters/demons/anchor/schema.py       the schema, built in code
tools/seedsmith/seedsmith/adapters/demons/anchor/descriptions.py the prose, one constant per attribute
tools/seedsmith/seedsmith/adapters/demons/anchor/audit.py        the numeric audit
tools/seedsmith/tests/test_anchor_contract.py
```

Descriptions live in their own module because they are edited far more often than the shape, and a
diff that mixes the two is unreviewable.

## Code style

Match the existing demon adapter: frozen sets for vocabularies, module-level constants in
`SCREAMING_CASE`, docstrings that name the spec.

## Testing strategy

| Test | Asserts |
|---|---|
| `every_attribute_has_a_description` | no attribute ships without prose — the reliability mechanism |
| `every_description_names_what_the_field_is_not` | each contains a negative clause |
| `every_closed_enum_admits_none_or_declares_why_not` | `none` present, or an explicit allow-list entry |
| `no_numeric_field_survives_the_audit` | all five audit cases, each with a crafted violation |
| `gameTypeId_is_the_only_allowlisted_integer` | pins the exception so a second one needs a test change |
| `additionalProperties_is_false_everywhere` | a hallucinated key rejects |
| `resourceProfile_has_six_members` | regression: poise was missing once |

## Boundaries

**Always:** declare an ownership level per field; give every attribute a description with a negative
clause; keep `none` legal on optional enums.

**Ask first:** widening `element`, `aptitude`, or `rarity`; adding an attribute.

**Never:** put a magnitude in the anchor; allow a missing key to mean "unsure"; let the model author
`posture`, `pure` or `basis` — all three are derived, and a model that can write them can contradict
its own primary answer.

## Success criteria

- [ ] The audit rejects all five numeric-smuggling shapes, each proven by a failing fixture.
- [ ] Every attribute has a description containing an explicit negative clause.
- [ ] `resourceProfile` carries six resources.
- [ ] The schema is consumable directly as an LM Studio `response_format: json_schema`.
- [ ] A downstream module can name a field's ownership level without reading any other document.
