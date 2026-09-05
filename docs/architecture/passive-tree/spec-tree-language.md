# Spec: `tree-language`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-language` · **Wave:** 1 · **Depends on:** `tree-plan`
**Model calls:** the only ones in the whole pipeline. **It never writes a number.**

Predecessor research: [03-llm-stage-contract.md](../../research/passive-tree/03-llm-stage-contract.md).
This spec builds on it and does not re-derive it; where D29 (10 tiers, 40 nodes) supersedes the
7-tier arithmetic that note was written against, the numbers here are re-derived and say so.

---

## Objective

Stage 2 of D13's generation order. The plan has already fixed shape, tier ladder, budgets, archetypes,
the potency ceiling and the property vocabulary. This module decides **what each node is about** —
which effects it grants, what it is called, and whether it conflicts with a property — choosing every
value from a closed vocabulary that the plan has already narrowed to this node's quota cell.

The whole design is one sentence: **the permitted subset becomes the schema's `enum`, so an
out-of-quota value is unsampleable, not merely rejected.** Everything else in this spec is the
scaffolding that keeps that sentence true.

Owner constraint (D24): the catalog is static, shared and identical for every player. This stage's
output is committed content, not a per-player roll.

---

## Design

### 1. The four ownership levels — the repo's existing names, not a parallel set

[item/seed-contract.md](../item/seed-contract.md) §2 already names four levels, and the charter's
FROZEN / CHOSEN / FREE maps onto them exactly: FROZEN is DERIVED-or-GENERATED, CHOSEN is VALIDATED,
FREE is AUTHORED. Inventing a parallel triple here would be the defect
[action/spec-action-seeding.md](../action/spec-action-seeding.md) §3 names by name.

| Level | Who sets it | Where the value lives |
|---|---|---|
| **AUTHORED** | the language stage chooses it | the seed file |
| **DERIVED** | the importer computes it from authored fields | a column, never the seed |
| **GENERATED** | a generator emits whole rows from authored input | `data/generated/`, checked in |
| **VALIDATED** | the stage names it, a frozen registry owns it | the seed file, checked against the registry |

### 2. The frozen / chosen / free table — one row per node field

**Every FROZEN row below names a field `spec-tree-plan.md` actually emits, at the level it emits it.**
The plan landed after this spec's first draft and four names did not reconcile; §2.2 records what
moved and why, so a reader who remembers the old names is not left guessing.

| Field | Level | Charter reading | Why |
|---|---|---|---|
| `nodeId` | **GENERATED** | FROZEN | Minted by the plan as `skill.<treeId>-<branch>-t<tier>-<nodeKey>` before the call, the way `setgen/emit.py:33-63` mints `entry_id`. No `/` and no dot inside the body — the `container_id` grammar (`item/seed-contract.md:132`) forbids both. `nodeKey` is minted **once** and read back on regeneration, never recomputed from position. An id the stage picks is an id that collides and an id that churns between runs |
| `treeId` | **GENERATED** | FROZEN | D9/D27's roster, read from `data/seed/aptitudes/roster.json`, `data/seed/elements/roster.json` and the status catalog mirror — never typed, never named by the stage |
| `branch` | **GENERATED** | FROZEN | D10: two branches, offensive and defensive, everywhere |
| `tier` | **GENERATED** | FROZEN | D29: ten tiers. **The field is named `tier` on the plan side only** — see §2.1 |
| `class` (`mechanism` \| `magnitude`) | **GENERATED** | FROZEN | The plan's per-node field is `nodes[].class`; `nodeClass` is the name of the **quota axis and property key**, not of the field. §3.5 of the ideal is a hard requirement: deep tiers must carry mechanisms. Given the choice the stage picks `magnitude`, because a magnitude node is easier to write |
| `parents[]` | **GENERATED** | FROZEN | D13's "skill links" are the plan's; reachability is checked plan-side before any call |
| `budgetShareMilli` | **GENERATED** | FROZEN | The plan's per-node share, **‰ of one branch budget**, consumed by `tree-binder`. Numeric, so it is never in a model schema |
| `potencyBand` | **GENERATED** | FROZEN | The plan's ordinal label over `potency.bandEdgesMilli[]`. **The only size signal this stage ever sees**, per `seed-contract.md` §3 — a label, never a number |
| `quotaCell` | **GENERATED** | FROZEN | §4. The `(nodeClass, trigger, element, status, channelFamily, exclusionForm)` cell this node was allocated. It is what filters every enum below |
| `permittedIds` | **GENERATED** | FROZEN | The plan's per-axis id lists derived from `quotaCell`. **This is what becomes the schema `enum`** (§4.2 step 6) — this stage prints it, it does not compute it |
| `requiredProperties[]` | **GENERATED** | FROZEN | The plan states what the node must key on; the stage may not widen it |
| `affixIds[]` | **VALIDATED** | CHOSEN | Named from the permitted subset, checked against the shipped affix library. **The affix is the roll unit, not the bare atom** (`definitions.md` §4a) |
| `affinity[]` (`core` \| `likely` \| `occasional`) | **VALIDATED** | CHOSEN | [effect-pipeline-ideal.md](../effect-pipeline-ideal.md) §6.4's trick — a model cannot be trusted with `weight: 40` but can reliably say an effect is *core* to a node. A tuning table turns three ordinals into three weights |
| `kindId` | **DERIVED** | FROZEN | Legal to name from the 16, but derived from the chosen affix's atoms instead. A model asked for two facts that must agree will eventually disagree (`setgen/schema.py`'s own reason for omitting `apCost`) |
| `attachPoint` | **DERIVED** | FROZEN | One per kind, from `AtomKindRegistry.Build()`. Never a second field |
| `channel` | **DERIVED** | FROZEN | Derivable from the affix, so not asked. Same rule as `kindId` |
| `elementSlot` | **VALIDATED** | CHOSEN | From `omni` + the 6 roster elements, filtered by the cell — and **forced** for an elemental tree |
| `statusId` | **VALIDATED** | CHOSEN | From the 21, filtered by the cell |
| `trigger` | **VALIDATED** | CHOSEN | From the **11 authorable** of 13 (§3). `OnGranted`/`OnRemoved` are omitted from the enum, not allow-listed |
| `exclusion.form` (`none` \| `reroute` \| `precedence`) | **VALIDATED** | CHOSEN | D14's ladder minus `nullification` — §5 |
| `exclusion.propertyKeys[]` | **VALIDATED** | CHOSEN | Keys must come from the plan's `propertyVocabulary`. **Never a node id** |
| `name`, `nameKey` | **AUTHORED** | FREE | Bounded by grammar and length, deduplicated corpus-wide |
| `flavor` | **AUTHORED** | FREE | ≤140 chars, no mechanics, no numbers — `family_propose/prompts.py:88-96`'s own rule |
| `printedText` | **AUTHORED** | FREE, template-composed | The exclusion's player-facing sentence, composed from a template so both sides print the same rule and name the same winner |
| `rationale` | **AUTHORED** | FREE | **Review queue only.** An OPEN-loop field may never gate — `metrics/registry.py:17-21` refuses to register one with `gates=True` |
| `blocked` | **AUTHORED** | FREE | The decline path. Required on every seedsmith schema (`pipeline/model.py:40`, `:113-206`) |
| every magnitude, coefficient, weight, duration, chance | **GENERATED** | FROZEN | `tree-binder`'s. **No such field exists in this stage's schema at all** |

### 2.1 Why nothing numeric is negotiable — and it is enforced before any call

`MAGNITUDE_DENY_NAMES` (`tools/seedsmith/seedsmith/pipeline/model.py:63-65`) already contains
`tier`, `rung`, `duration`, `chance`, `cost`, `weight`, `damage`, `hp`, `atk`, plus any name ending
`Milli` (`:71`). The name check fires *"even when the value is a legal, enum-closed vocabulary, on
purpose"*, and `audit_schema` (`:113`) runs from `Pipeline.__post_init__` (`:250-256`) — so a schema
with a field named `tier` raises `ValueError` at construction, **before a single model call is made**.

That is why the plan's own tier is planner-side only, and why the model-facing strength — if this
stage ever needed one, which it does not — would be a *band*, never a tier.

**Consequence, stated plainly: "the language stage can never move a balance number" is not a policy
in this document. It is an unsampleable state, enforced by shipped code.**

### 2.2 Reconciled against the emitted plan — four names moved, and one was never a field

The first draft of this spec was written against an input interface that did not exist yet, and said
so. `spec-tree-plan.md` now exists, so the guessing stops. Four of the nine plan fields this module
reads did not reconcile; all four are corrected above, and the old names are recorded here so a stale
reference is recognised rather than re-derived.

| Was written here | Is actually emitted | Level | Note |
|---|---|---|---|
| `nodeClass` (node field) | `nodes[].class` (`spec-tree-plan.md` per-tree schema) | node | `nodeClass` survives as the **quota axis / property-vocabulary key**. Same two members, different thing |
| `shapeArchetype` (node field) | `archetype` | **tree header**, not node | `archetype = archetypes[ordinal mod k]`. A node inherits it by reading its tree's header; it is not repeated per node |
| `tierRequirement` (node field) | `ladder.req[]`, `int[10]` | **manifest**, not node | One array for the whole corpus, `req(t) = 5·t(t+1)/2`. Nothing per-node about it |
| `mechanismFloor` | `archetypes[].mechNodes[]`, `int[10]` | **manifest**, per archetype | Not a rename — a different shape. §4.2 |

The five that reconciled unchanged: `quotaCell`, `permittedIds`, `requiredProperties[]`,
`budgetShareMilli` and `propertyVocabulary`. `potencyBand` was emitted by the plan and not read here;
it is now §6.2's size signal.

### 3. The closed vocabularies — counted in `src/` this session, not quoted

| # | Vocabulary | Declared at | **Counted** | Note |
|---|---|---|---:|---|
| 1 | Atom **attach points** | `AtomKind.cs:8-30` (enum), `AtomKindRegistry.cs:21` (`AttachPointCount`) | **7** | `Stat · Resource · Status · Shield · Board · Match · Ui` |
| 2 | Atom **kinds** | `AtomKindRegistry.cs:31` (`KindCount`), rows `:476-869` | **16** | Derived from the affix; never asked |
| 3 | Atom **triggers** | `AtomKind.cs:95-99` (`AtomTriggers.All`), `AtomKindRegistry.cs:36` | **13 declared, 11 authorable** | `OnGranted`/`OnRemoved` are runtime lifecycle states no kind may carry (`AtomKind.cs:104-111`) |
| 4 | **Elements** | `ActorElementTypes.cs:3-11`, roster `:21-29` | **6** (+ the `omni` sentinel, `:19`) | |
| 5 | **Statuses** | `StatusCatalogBootstrap.cs:16-58` | **21** | 8 engine wraps + 8 overlay-authored + 5 contagion |
| 6 | **Aptitudes / postures** | `Aptitude.cs:38-51` / `:11` | **12 / 3** | The count is a *product* (`PostureCount × PerPosture`, `:30-35`), never typed |
| 7 | **Derived stat channels** | `DerivedStatRegistry.cs` | **267 registered + 9 open prefix families** | 267 asserted in four test files (`StatTaxonomyTests.cs:183`, `AtomCatalogSsotDriftTests.cs:46`, `ElementHubDocDriftTests.cs:73`, `SeedCatalogTests.cs:28`); the 9 prefixes resolve dynamically in `TryResolveChannel` (`:318-388`) |
| 8 | **Primary stat channels** | `ModifierOp.cs:68-75` (`StatChannels.All`) | **23** | `stat.modify`'s vocabulary |
| 9 | **`UnitClass`** | `StatClass.cs:29-100` | **13** | The class's own doc comment at `:26` still says *"ten-class"* — stale by three. Counted, not quoted |
| 10 | **`StatClass`** | `StatClass.cs:7-22` | **4** | `Contest \| Race \| Pool \| Feeder`, explicitly orthogonal to `UnitClass` |
| 11 | **Affix families** (the roll unit) | `data/seed/items/affix-families/*.json` — 15 files | **98** | Counted this session |
| 12 | **Affix tag values** | the same 98 families | **3** | `offensive` 41 · `defensive` 40 · `utility` 17. See §5.1 |

**Two classifications of a channel already exist and both are normative** — the 13-class `UnitClass`
ledger and the sheet's six render states. This module invents no third; it does not classify channels
at all, because it never names one (§2's table: `channel` is DERIVED).

**Rarity does not apply to a tree node.** A rung selects roll counts and a tier window for a *rolled*
container; a tree node is static shared content and has no roll. A rarity field on a node would be a
second progression ladder, which is what `ssot-power-scale.md` §10 exists to refuse.

### 4. The quota mechanism — how D32 becomes an enum

#### 4.1 What it exists to prevent, measured

The shipped demon corpus is the counter-example. With the enum open — eight pipelines each free to
pick any of 12 aptitudes and any of 6 elements — 840 indexed entries came out at Onslaught 332 (39.5%) against
Ferocity 2 (0.2%): a **166:1 skew** against a uniform 8.3%, and `earth` alone at 45.1% against a
uniform 16.7% (ideal §9, re-verified in [03](../../research/passive-tree/03-llm-stage-contract.md) §5.1).

The bias research already explains why permutation and voting cannot fix this: they recover *per-entry
correctness*, worth up to 8 points, against a 166× spread in *aggregate shape*. **Only removing the
option from the call fixes aggregate shape.**

#### 4.2 The algorithm

The apportionment primitive is shipped, and it is already careful in exactly the way the repo's
overflow rules demand: `largest_remainder_count`
(`tools/seedsmith/seedsmith/adapters/actions/distribution_planner/derive.py:73-90`) — `long`, widened
before multiplying by `_widen_mul` (`:57-70`, which raises `OverflowError` rather than wrapping),
divided by 1000 last and exactly once, ties broken on the declared order rather than on dict
iteration order. `expand_counts` (`:92-104`) flattens `{key: count}` deterministically.

```text
INPUT   trees[]        from the plan's roster (D9/D27) — read, never hardcoded
        nodes[]        the plan's skeleton: 39 generic trees x 40 nodes = 1,560 (D29)
        targets        data/tuning/passive-tree-targets.v1.json  (D32, declared not implied)

1  N := 1,560                                          # the generic catalog, one integer

2  for each quota AXIS a in {nodeClass, trigger, element, status, channelFamily, exclusionForm}:
       quota[a] := largest_remainder_count(targets[a].weightsMilli, ORDER[a], N)
   # exact integer marginals, sum == N by construction

3  seq[a] := expand_counts(quota[a], ORDER[a])

4  for each tree t in roster order, each (branch, tier, index) slot in canonical order:
       cell := { a: seq[a][cursor] for a in AXES };  cursor += 1
       # HARD CONSTRAINTS OVERRIDE THE DRAW, and they narrow, never widen:
       cell.element   := t.element   if t is an elemental tree
       cell.aptitude  := t.aptitude  if t is a primary tree
       cell.nodeClass := "mechanism" if tier >= t.mechanismFloor

5  # REBALANCE: a slot whose draw was overridden returns its drawn value to the pool, and the
   # pool is re-apportioned over the REMAINING slots.

6  permitted[axis] := the ids of that axis whose tag/enum value == cell[axis]
   # THIS is what goes into the schema's `enum`. Not the whole vocabulary.

7  emit data/seed/passive-tree/plan/<treeId>.json      # model-free, committed, diffable
```

**Step 6 is the load-bearing line.** Under constrained decoding the permitted subset *is* the enum, so
an out-of-quota value cannot be produced. This is the technique `family_propose/prompts.py:63-66`
already uses — enums shipped empty in the constant and filled at call time, `deepcopy`d per call
(`:150-160`) so two calls in the same process never alias one another's enum.

**Step 5 is the line that gets forgotten.** An elemental tree's element is forced, so its drawn element
must go back to the pool. Skip it and the forced trees consume their quota twice — once by force, once
by draw — and the free trees inherit the deficit. That is the original defect wearing a planner's
uniform.

#### 4.3 The target file

`data/tuning/passive-tree-targets.v1.json`, shaped like `data/tuning/demon-roster-targets.v1.json`
(integer per-mille throughout, a `_note` recording provenance and what is fitted). **No axis lists its
own members** — aptitudes are read from `data/seed/aptitudes/roster.json`, elements from
`data/seed/elements/roster.json`, statuses from the status catalog mirror, so a thirteenth aptitude
changes the grid by construction rather than by a forgotten edit.

`legitimateSkew` is the file's own named-theme allowance (D32): a row there is a claim that an
imbalance is **theme, not bias** — argued once, in data, instead of re-litigated per node. It starts
empty and every row needs a `_why`.

Every threshold in that file is a **tunable** (§14 of the ideal). This spec names keys and units; it
carries no values a balance pass would move.

### 5. Property-keyed exclusion (D14)

The ladder, applied in order. Both sides print the rule and name the same winner.

| Form | What it is | Rule |
|---|---|---|
| **Reroute** | *"if the damage is converted, this node instead amplifies the converted type"* — no conflict ever occurs | **Ask for this first.** Fall through only if the effect genuinely cannot reroute |
| **Precedence** | the conflict is *defined*, not forbidden: *"applies before conversion"* | the common case |
| **Nullification** | a named node declared inoperative | ⛔ **Absent from the schema enum.** It is the only form that names a node, so a generated corpus cannot maintain it. Target: zero. A genuine case arrives as a review-queue escalation, never as a generated field |

**Target rarity ~2% of nodes** (D14). At 1,560 generic nodes that is ~31 exclusions — small enough to
review by hand, which is the honest reason the ladder works.

⚠️ Reroute and Precedence are **printed runtime no-ops, not allocation blocks.** Both nodes stay
allocatable and the concentration index still counts the points spent on the nullified one. Refunding
points would make the focus multiplier depend on which other trees you took, which is exactly what
§3.2's two-index blend was written to avoid.

The predicate mechanism is shipped and needs no new type: `EligibilityRule` (`EligibilityRule.cs:20-24`)
carries `RequireTags` (bare keys) and `AnyOfTags` (`key:value` pairs), evaluated by `IsEligible`
(`:36`), with `Validate` (`:74-95`) refusing a rule that selects zero eligible affixes against a
non-zero budget (`UnsatisfiablePool`, `:89`, `:93`). Tags are derived from the affix's own atoms by
`AffixTags.Of` (`AffixTags.cs:41`) — **derived, so the stage cannot game them.**

#### 5.1 The honest finding: the property vocabulary comes from `tree-plan`, not from the corpus

D22 says composing from the shipped atom catalog *"hands D14's property-keyed exclusions an existing
property space: atom tags."* **Half of that is true and the false half is load-bearing.**

- `AtomRow.TagsJson` is `string = "{}"` (`AtomRow.cs:40`), and `AtomRowValidator.cs:184` checks only
  *"is this a JSON object"* — no membership check, unlike every other atom param, which uses
  `ParamDef.Vocabulary`.
- Counted this session: the 98 affix families under `data/seed/items/affix-families/` carry exactly
  **three** distinct tag values — `offensive` 41, `defensive` 40, `utility` 17. That is one axis, and
  it is not enough to write a single interesting exclusion.

**So the property space this module keys on is `tree-plan`'s `propertyVocabulary`, emitted before any
node text is written (ideal §6 step 2), not the atom corpus's tags.** Until an atom-tag registry lands
and `AtomRowValidator.cs:184` becomes a membership check, a predicate can key on `posture` and nothing
else, and this module's exclusion gate must report that as a `NOT_MEASURED` rather than a pass.

Stating it now costs a paragraph. Discovering it after 1,560 nodes are generated costs the run.

### 6. The request / response contract

#### 6.1 The unit of work is ONE NODE

| Unit | Calls | Problem |
|---|---:|---|
| One tree | 39 | 40 nodes in one response. Guardrail 2 is *"narrow scope per call — one partition, one kind"* (`pipeline/model.py:11`). A 40-node response cannot carry a per-node quota cell in its schema, so **layer 2 of the fence disappears** |
| One tier-branch group | ~780 | Better, but the cell varies within a tier, so the enum becomes the union of its members' cells |
| **One node** | **1,560** | ⭐ **The only unit at which the permitted subset is exact.** |

**Sibling deduplication without widening the call.** `distribution_planner/derive.py:516-522,576`
already passes `accepted_neighbours` and `avoid_neighbour_k` — the k nearest already-accepted siblings
by rendered fingerprint — into a per-subject brief. This module passes the node's tier-siblings. That
is how you get *"do not repeat what you just wrote"* without putting 40 nodes in one schema.

**Cost, computed before the shape was chosen** (D29's corpus, re-derived from doc 03's 7-tier figures):

```text
generic nodes     39 trees x 40 nodes                    =  1,560
base calls        1,560 x 1 pipeline                     =  1,560
vote calls        1,560 x 1 voted field x (3 - 1)        =  3,120
                                                            -----
                                                            4,680 calls
```

At the demon run's measured rate (16,272 calls ≈ 14 h locally) that is **≈ 4 h** — about a quarter of
a run this repo has already done twice. **D30's 840 species trees at 40 nodes each are a separate
~100,800 calls and belong to `species-tree`, not here.**

**Vote exactly one field: `affixIds`.** Being wrong there is expensive to fix later — it decides what
the node *does* — while `name`/`flavor` are cheap to regenerate and `elementSlot`/`statusId`/
`nodeClass` are already narrowed to one or two options by the quota. `schemas.py:209-211` records that
each additional voted field *"moves the call budget by a third of the run."*

#### 6.2 What one call receives

Following `setgen/brief.py:48-85`'s anatomy, with its two deliberate omissions — **any number**, and
**any option outside the permitted subset**.

```text
SYSTEM
  You author ONE passive skill node for a build tree. You never write a number: not a
  strength, not a duration, not a chance, not a tier — tables you never see decide every
  magnitude. You never invent an effect id, an element, a status or a channel; you pick from
  the lists given, or you set `blocked`. You never name another node — an exclusion keys on
  a PROPERTY, never on a name.

USER
  Tree: {treeDisplayName} — {treeReading}
  Branch: {offensive|defensive}.  Depth: {shallow|mid|deep}.
  This node must be a {mechanism|magnitude} node.
    - a MECHANISM node grants something the resolver does not otherwise have.
    - a MAGNITUDE node makes an existing thing larger.

  Motifs to express: {motifs}.  Avoid entirely: {antiMotifs}.

  Choose, and nothing else:
    1. `affixIds`  — {1..3} from the list below. They are this node's whole effect.
    2. `affinity`  — how central each is: core | likely | occasional.
    3. `exclusion` — only if this node's effect genuinely conflicts with a PROPERTY below.
                     Prefer `reroute`. Most nodes have none.
    4. `name`, `nameKey`, `flavor`.

  Never choose a number, a strength, a duration or a tier. Those are resolved after you answer.

  Legal effects ({n}):        {permitted affix ids, permuted, one-line descriptions}
  Legal exclusion properties: {the plan's propertyVocabulary subset, permuted}
  Already written in this tier — do not repeat: {k nearest siblings, name + effect}

  If this brief cannot carry a node you would be happy to ship, set `blocked` and say why.
```

Four repo rules this obeys:

- **Inline, never cite.** `setgen/brief.py:3-5`: *"a citation teaches the model to write about the
  document instead of the content."*
- **A number that must be conveyed is rendered as a label.** `family_propose/prompts.py:193-197`
  renders bands as prose, *"never the raw pair the model could copy into its own answer as though it
  were a real magnitude."* Hence `shallow|mid|deep`, never `tier: 5`.
- **Every negative clause also lives in the schema.** `family_propose/prompts.py:40-44`: *"a
  description that lives only in prose beside a schema is a description the audit cannot read."*
- **Options permuted, seeded from the node id and the sample index**, with `verify_permutation`
  (`validate_heal/derive.py:79-92`) **raising** if the rendered order does not reproduce
  `order_for(...)` — a claimed permutation that did not happen is caught, not trusted.

#### 6.3 What one call returns

```jsonc
{
  "type": "object", "additionalProperties": false,
  "required": ["affixIds", "affinity", "exclusion", "name", "nameKey", "flavor", "blocked"],
  "properties": {
    "affixIds": { "type": "array", "minItems": 1, "maxItems": 3,
      "items": { "type": "string", "enum": [] },      // FILLED PER CALL with the permitted subset
      "description": "The effects this node grants. Pick only from the list. This is NOT a
                      description of the node — never invent an id." },
    "affinity": { "type": "array",
      "items": { "type": "string", "enum": ["core", "likely", "occasional"] },
      "description": "How central each chosen effect is, in the same order. This is NOT a
                      strength and NOT a weight." },
    "exclusion": { "type": "object", "additionalProperties": false,
      "required": ["form", "propertyKeys"],
      "properties": {
        "form": { "type": "string", "enum": ["none", "reroute", "precedence"] },
        "propertyKeys": { "type": "array", "items": { "type": "string", "enum": [] } } },
      "description": "Only when this node's effect genuinely conflicts with a listed property.
                      It is NOT a way to make the node stronger, and it never names another node." },
    "name":    { "type": "string", "description": "The node's own name. It is NOT the tree's name." },
    "nameKey": { "type": "string", "pattern": "^tree\\.node\\.[a-z0-9-]+$",
                 "description": "Lowercase-kebab key. It is NOT free text." },
    "flavor":  { "type": "string", "maxLength": 140,
                 "description": "One line under the name. It is NOT a rules description: never say
                                 what the node does mechanically, and never write a number." },
    "blocked": { "type": "string",
                 "description": "The exact empty string when you WERE able to author the node — the
                                 normal case. Do NOT put a real answer here." }
  }
}
```

Three notes, each earned by a measured defect:

- **`nullification` is absent from the `form` enum, not discouraged.** An option that cannot be
  sampled is a validator nobody has to write.
- **`blocked` is an empty-string sentinel, not a boolean**, per `anchor/prompts.py:61-83`'s live
  finding: a boolean let a real local model fill the field with something plausible-but-wrong rather
  than leaving it empty.
- **No `tier`, no `potency`, no `channel`.** `tier` would be refused at schema construction (§2.1);
  `channel` is derivable from the affix, and a model asked for two facts that must agree will
  eventually disagree.

#### 6.4 Where the output goes

```text
data/seed/passive-tree/plan/<treeId>.json    THE PLAN — model-free (tree-plan's)
        |  this module — the only model calls in the whole pipeline
        v
data/seed/passive-tree/nodes/<treeId>.json   THE SEED — enums + prose, no numbers.
                                             _provenance: planHash, promptVersion, model,
                                             confidence, minorityValues
        |  tree-binder (deterministic)
        v
data/generated/passive-tree/<treeId>.json    CONCRETE — coefficients, checked in. THIS ships.
```

**There is no per-player materialise stage for trees** (D24). `data/generated/` is the end of the
chain and the same bytes reach every player — which is the one place the binding
seed → concrete → per-player principle stops at the second arrow, on the same split
`DESIGN-GATE.md:45` already states for demon species stats.

### 7. Validation gates, ordered by when they fire

| # | Gate | What checks it | Failure shape |
|---:|---|---|---|
| 1 | **Schema has no numeric field** | `audit_schema` (`model.py:113`) from `Pipeline.__post_init__` (`:250-256`) | `ValueError` at construction — **before any call** |
| 2 | **Every description carries a negative clause** | `audit_descriptions` (`validate_heal/schema_audit.py:30-67`) | `SchemaDefect(path, "carries no negative clause")` |
| 3 | **Plan reachability** | new, deterministic over the plan: unsatisfiable parents, an empty tier, an orphan | plan-side, **before any call** |
| 4 | **The target file is complete** | `_require`/`_validate` in the `setgen/tuning.py:94-102` mould — *"refusing to substitute a default; an unreviewed number here reaches every generated entry"* | error at load |
| 5 | **Every gate has a threshold** | `missing_thresholds(tuning)` (`setgen/verdict.py:50-57`), printed in the dry-run JSON | listed before the run spends anything |
| 6 | **Constrained decoding is actually on** | `run_preflight` (`validate_heal/preflight.py:51-91`), one probe with a single-member enum | `"failed"` blocks the run |
| 7 | **Contract** | `run_g1` (`validate_heal/gates.py:59-107`) — missing key, any extra key, type mismatch, enum violation | all collected, re-prompted with the defect named |
| 8 | **⭐ Quota conformance, per call** | the permitted subset in the schema `enum` (§4.2 step 6) | **unsampleable** |
| 9 | **Brief conformance** | `run_g2` (`gates.py:114-177`) — an effect outside the permitted set, an anti-motif | hard defect, re-prompted |
| 10 | **Text style** | `field_echo` (`validators/field_echo.py:15-34`), `subject_name_echo` (`:48-66`), `language_consistency` (`language.py:26-44`) | measured defects: 7 of 8 outputs once began `"DOCTRINE: "`; 87% code-switched |
| 11 | **Vote resolution** | `resolve_vote_field` (`validate_heal/derive.py:102`), `verify_permutation` (`:79-92`) | `"1-1-1 vote — unresolved, value is None"` — **never silently the first option** |
| 12 | **Bounded repair** | `call_with_self_heal` (`llm_caller.py:207-267`) | on exhaustion `"FAILED:<reason>"` — never blank, never silently dropped |
| 13 | **Persist-time re-gate** | `pipeline/run.py:122-127` — the gate runs **twice** | `escalated[key] = "failed the gate at persist time"` |
| 14 | **Idempotence** | `should_generate` (`provenance.py:77-95`); `ProvenanceLedger.record` raises on a duplicate row (`:109-118`) | `SkipReason.ALREADY_GENERATED` |
| 15 | **`PassiveTree/QuotaDrift`** | emitted per-axis counts vs target, **re-derived independently**, per `coverage_report/derive.py:257-282` | `abs(count − quota) > toleranceUnits`, **in either direction** |
| 16 | **`PassiveTree/MechanismFloor`** | `nodeClass` at tiers ≥ the archetype's floor | any deep-tier `magnitude` node — ideal §3.5: *a magnitude-only deep tier is measurably worthless to a focused build* |
| 17 | **`PassiveTree/CellOccupancy`** | `(channelFamily, sorted trigger+element multiset)` per node, the `cells.py:56-68` key adapted | median > 2 |
| 18 | **`PassiveTree/ExclusionRate`** | exclusion count / node count, and the form split | rate over target, **any** `nullification`, or a predicate naming a node id |
| 19 | **`PassiveTree/ExclusionResolvable`** | every predicate key against the plan's `propertyVocabulary`, then `EligibilityRule.Validate` (`:74-95`) | unknown key, or `UnsatisfiablePool` |
| 20 | **`PassiveTree/NearDuplicate`** | `setgen/dedup.py`'s local exact Jaccard, deliberately **not** the shared MinHash | the shared MinHash over-reports 7× on real pairs (`dedup.py:6-16`) — gating on it would fail every run for the wrong reason |
| 21 | **`PassiveTree/NameCollision`** | `name_collision` (`validators/field_echo.py:69-94`) against `takenNames`, plus `dedup.dedup_report` | measured: **83 of 83** commander effects were once named identically to their demon, caught by a corpus metric and not by any per-item check |
| 22 | **⭐ `PassiveTree/UnresolvedCount`** | per-voted-field `unresolved` rate | **the one promoted to `gates=True`** — §7.1 |
| 23 | **Run verdict** | `RunReport.verdict` (`setgen/verdict.py:83-96`) | `FAIL` beats `NOT_MEASURED`; a held partition alone denies a `PASS` |
| 24 | **Offline guarantee** | `tools/seedsmith/tests/test_offline_guarantee.py` — the transport stub **raises** on an unexpected call | any test that reaches a model |

**Deliberately not gated:** `rationale` and any *"is this node good?"* judgement are OPEN-loop —
detectable, not machine-verifiable — so they produce a review queue and never a pass.
`metrics/registry.py:17-21` refuses to register an OPEN-loop metric with `gates=True`, and the reason
is worth keeping in the spec: an open-loop metric contributing to a pass verdict is a lie with a
checkmark on it.

#### 7.1 Exactly one gate is promoted to hard-fail first

**`PassiveTree/UnresolvedCount`.** Everything else runs and reports.

The template is `demon_roster.py:369` — the only metric in the repo at `gates = True` today — and its
justification (`:357-365`) transfers exactly: an unresolved field silently produced zero-stat species,
so *"gating the RATE here stops a full run early — before spending thousands of model calls."* The
same is true of `affixIds`: an unresolved node has no effect for `tree-binder` to price, so a run that
is systematically unresolvable should stop at hundreds of calls, not 4,680.

Every other gate starts `False` and is promoted as a deliberate, later, separate act, because a
threshold promoted before a real run is a threshold nobody can name in advance
(`distribution.py:97-98`).

---

## Commands

```powershell
# dry-run is the DEFAULT; --write is the explicit opt-in (cli.py:285-289's rule, adopted verbatim)
python -m seedsmith trees generate --tree tree.aptitude.might --dry-run --sample-brief
python -m seedsmith trees generate --all --dry-run          # prints gatingMetrics + gatesMissingAThreshold
python -m seedsmith trees generate --all --write            # the real run: 4,680 calls
python -m seedsmith check --family PassiveTree --gate       # exit 1 on a gap, 3 on a refusal
python -m seedsmith metrics --family PassiveTree
python -m pytest tools/seedsmith/tests/adapters/trees
```

`--write` returns `EXIT_REFUSED` (3) until the graph is wired, the way `items generate --write` does
today (`report/cli.py:329-334`) — *"a command that silently writes nothing is worse than one that says
so."* Exit codes are the shipped four: `EXIT_CLEAN = 0`, `EXIT_GAP = 1`, `EXIT_CANNOT_RUN = 2`,
`EXIT_REFUSED = 3` (`cli.py:44-47`).

## Project structure

Twelve modules in the `setgen` mould (`adapters/items/setgen/`, the newest precedent, 2026-09-04), each
labelled in `__init__.py` by which side of the no-numbers line it sits on.

```text
tools/seedsmith/seedsmith/adapters/trees/nodegen/
    __init__.py        module roles, one line each
    tuning.py          load passive-tree-targets.v1.json; every key required, no defaults
    vocab.py           affix ids + tags counted from the corpus fresh, never transcribed
    quota.py           largest_remainder_count over 6 axes; overrides; the rebalance of step 5
    plan_read.py       read tree-plan's emitted plan; refuse an unfilled hole
    brief.py           the per-node brief; permutation seeded from nodeId|field|sample
    schema.py          the response schema; enums empty in the constant, deepcopy'd per call
    run.py             plan_run -> RunPlan{subjects, held, already_done}
    emit.py            node id minting + grammar; IdRefused, never sanitised
    dedup.py           local exact Jaccard over tier siblings
    exclusion.py       form ladder, predicate keys checked against propertyVocabulary
    verdict.py         GATING_METRICS dict + missing_thresholds()
tools/seedsmith/seedsmith/metrics/passive_tree.py       the 8 PassiveTree/* metrics
data/tuning/passive-tree-targets.v1.json                D32's target + legitimateSkew + gates
data/seed/passive-tree/nodes/<treeId>.json              this module's committed output
tools/seedsmith/tests/adapters/trees/                   offline; the transport stub raises
```

## Code style

```python
def permitted(axis: str, cell: "Mapping[str, str]", vocab: Vocabulary) -> "list[str]":
    """The node's quota cell, narrowed to ids. This list IS the schema's `enum`, so an
    out-of-quota value is UNSAMPLEABLE rather than rejected afterwards.

    Counted from the corpus on every call (setgen/vocab.py:113-129's rule), never
    transcribed: an id that left the corpus cannot be printed, and one that joined it
    needs no edit here. An empty result is HELD, never widened — themes.py:41,66-73's
    rule, because laundering a cell is how a quota silently stops being a quota."""
    ids = [i for i in vocab.ids(axis) if vocab.tag(i, axis) == cell[axis]]
    if not ids:
        raise UnsatisfiableCell(f"{cell!r}: no {axis} id satisfies this cell — held")
    return ids
```

## Testing strategy

| Test | Asserts |
|---|---|
| `schema_has_no_numeric_field` | `audit_schema` over the real constant raises nothing, and raises when a `tier` field is added |
| `enum_is_empty_in_the_constant` | the shipped schema's enums are `[]`; only `fill_schema` populates them |
| `two_calls_never_alias_one_enum` | fill twice with different cells, assert the first is unchanged |
| `out_of_quota_value_is_absent_from_the_enum` | for a sampled cell, every out-of-cell id is missing from the printed enum |
| `quota_marginals_sum_to_the_corpus` | `Σ quota[a] == 1560` for every axis, exactly |
| `overrides_return_their_draw_to_the_pool` | force every elemental tree's element; assert the residual marginals still match target within tolerance |
| `quota_drift_is_re_derived_not_read` | mutate the stored brief; the drift metric still catches the corpus |
| `permutation_is_verified_not_trusted` | `verify_permutation` raises on a rendered order that does not reproduce `order_for` |
| `mechanism_floor_holds_at_deep_tiers` | no tier ≥ floor carries `nodeClass: magnitude` |
| `nullification_is_unsampleable` | `"nullification"` is absent from the `form` enum |
| `exclusion_predicate_never_names_a_node` | no `propertyKeys` entry matches the node-id grammar |
| `exclusion_keys_resolve_against_the_plan` | every key is in `propertyVocabulary`; `EligibilityRule.Validate` reports no `UnsatisfiablePool` |
| `unresolved_count_is_the_only_gating_metric` | `GATING_METRICS` has exactly one entry |
| `open_loop_metric_cannot_gate` | registering `rationale` quality with `gates=True` raises |
| `rerun_over_unchanged_inputs_is_byte_identical` | hash-compared; the commander-effect generator rewrote all 84 entries every run and only a byte-comparison found it |
| `run_never_reaches_a_model` | the offline transport stub raises on any unexpected call |

## Boundaries

**Always:** read every roster from its shipped mirror and count it fresh; put the permitted subset in
the schema `enum`; `deepcopy` the schema per call; hold an unsatisfiable cell rather than widening it;
refuse a draft rather than repairing it, naming the rule (`setgen/distribute.py:143-144` — *"silently
repairing it teaches the next call nothing"*); commit the output; state which paths you wrote.

**Ask first:** promoting a second gate to `gates=True`; adding a quota axis (it changes the target
file's shape and every prior plan's cells); adding a row to `legitimateSkew` (it is a balance claim);
changing the unit of work away from one node.

**Never:** put a number in a model schema — not a magnitude, not a coefficient, not a tier, not a
duration, not a chance; hardcode a roster size (twelve aptitudes is a *measured outcome*, read from
`AptitudeCatalog`/its mirror); let the stage name a `channel`, a `kindId`, an `attachPoint` or another
node; emit `nullification`; widen the atom vocabulary (7/16/13 is a reviewed `decisions.md` change);
write to `SPEC.md`, `tasks/plan.md` or `tasks/todo.md`; write outside
`data/seed/passive-tree/nodes/` and `data/tuning/passive-tree-targets.v1.json`.

## Success criteria

- [ ] `audit_schema` passes over the real schema constant, and fails when a numeric field is added.
- [ ] Every axis's emitted marginals match the target within `toleranceUnits`, checked by a metric
      that **re-derives** the quota instead of reading the brief.
- [ ] The 166:1 aptitude skew is not reproduced: no axis value exceeds its target by more than
      `toleranceUnits`, in either direction.
- [ ] No deep-tier node carries `nodeClass: magnitude`.
- [ ] Exclusions land at the target rate with zero `nullification` and zero node-id predicates.
- [ ] Exactly one metric is `gates=True`, and it is `PassiveTree/UnresolvedCount`.
- [ ] A rerun over unchanged inputs is byte-identical, proven by hash.
- [ ] The full generic run is ~4,680 calls, and the dry-run prints that figure before spending any.
- [ ] `python -m seedsmith check --family PassiveTree --gate` exits 0 on the committed corpus.

## Open questions

Only questions nobody has answered. A recommendation nobody disputed is a decision; an answerable
question is a task.

1. **Is `nullification` allowed to exist at all?** §5 removes it from the schema because it is the
   only form that names a node. D14 lists it as the ladder's last rung. **Recommendation: keep it out
   of the generated corpus and reachable only through a hand-authored `allow`/`deny` override**, which
   `EligibilityRule` already has. This narrows a locked decision, so it needs an owner ruling.
2. **What goes in `legitimateSkew`?** The ideal's §7 item 2, still owed. This spec gives it a home and
   a shape; it does not answer it. `earth` at roughly 1.5× uniform is D32's own worked example, not a
   decided row.

**Blocked on other work, tracked not open:** the atom-tag registry (§5.1) — until it lands, exclusion
predicates key on `posture` and nothing else, and gate 19 reports `NOT_MEASURED`.

**Interface not yet frozen:** `tree-plan` is wave 0 and unspecced. Every plan field this module reads
(`quotaCell`, `requiredProperties`, `propertyVocabulary`, `mechanismFloor`, `budgetShareMilli`) is the
interface this module *requires*; the names must be reconciled when `spec-tree-plan.md` lands.

## Decisions implemented

| Decision | How this module implements it |
|---|---|
| **D9 / D27** | The roster is read from the shipped mirrors and its size is emitted, never typed. Categories can land in any order |
| **D10** | `branch` is GENERATED — two branches everywhere, never a choice |
| **D13** | This *is* stage 2. The plan runs first and this module fills vocabulary, categories, atom pools and bonuses inside it |
| **D14** | §5 — property-keyed exclusion, Reroute → Precedence → Nullification, ~2% target, printed no-op, `nullification` unsampleable |
| **D15** | The archetype and per-node budget are GENERATED; this module inherits them and cannot make two trees the same shape or the same value |
| **D16** | The stage cannot author a conversion node: no kind writes an element payload, so no affix in the permitted set can be one. `tree-binder` owns the budget refusal |
| **D20 / D26 / D29** | `tier`, `tierRequirement` and the 10×2 skeleton are GENERATED from `req(t) = 5·t(t+1)/2`; the stage never sees a tier number, only `shallow\|mid\|deep` |
| **D22** | Every effect is an affix from the shipped catalog. No passive-specific effect vocabulary exists in this module |
| **D24** | Output is committed seed data, byte-identical on rerun, with no per-player materialise stage |
| **D30** | Its corpus is acknowledged and costed (~100,800 calls) and explicitly handed to `species-tree` |
| **D32** | §4 — near-uniform target plus a named theme allowance, declared in `passive-tree-targets.v1.json`, enforced as an enum and re-checked as a corpus metric |
| ideal §3.5 | `nodeClass` is GENERATED and `PassiveTree/MechanismFloor` gates it, because a generator left to choose will choose `magnitude` |

**Decisions this module does not touch**, and where they live: D1, D2, D4–D8, D11, D12, D18, D21,
D25, D28, D33–D36 (`tree-state`, `tree-resolve`, `squad-harness`); D3 (`tree-binder`, `tree-state`);
D17, D23 (`species-tree`); D19 and D31 are superseded by D35.

---

## Design-gate checklist

```
[x] I identified the subsystem(s): atom layer, seedsmith generation, passive trees.
[x] I read every doc in the DESIGN-GATE §1 row(s) this session: DESIGN-GATE.md,
    passive-tree-map.md, passive-tree-ideal.md (full), research 02/03/04,
    effect-atom/definitions.md (referenced sections), design/spec-magnitude-and-units.md §3,
    design/spec-derived-stat-sheet.md (§1/§3 via the research notes).
[x] Counts verified BY COUNTING in src/ this session, not quoted:
    7 attach points, 16 kinds, 13 triggers (11 authorable), 6 elements, 21 statuses,
    12 aptitudes / 3 postures, 267 derived channels + 9 open prefix families,
    23 primary channels, 13 UnitClass, 4 StatClass, 98 affix families, 3 tag values.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — StatClass.cs:26 still says "ten-class"
    over a 13-member enum, and AtomKindRegistry.cs:6 still says "5 attach points, 12 kinds"
    fifteen lines above consts of 7 and 16.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I report. NOT DONE and stated: this spec proposes
    no code change and makes no "moves goldens" claim. No suite was run.
[x] Nothing contradicts a §2 invariant. Two are load-bearing and both hold: #11 (no hard
    ceilings — the potency ceiling REFUSES, never clamps) and #12 (the balance surface is
    data — every threshold is a key in passive-tree-targets.v1.json).
[ ] Corrections propagated. NOT DONE — DESIGN-GATE.md:34's "nine-class UnitClass" and
    StatClass.cs:26's "ten-class" are both stale against a 13-member enum. DESIGN-GATE's own
    row wins over any spec, so amending it is an owner call, not a side effect of this spec.
```

## Related

- [passive-tree-map.md](../passive-tree-map.md) — the module index
- [passive-tree-ideal.md](../passive-tree-ideal.md) — D1–D36, §6 generation order, §9 the measured skew
- [spec-tree-binder.md](spec-tree-binder.md) — stage 3, which prices what this module chose
- [03-llm-stage-contract.md](../../research/passive-tree/03-llm-stage-contract.md) — this module's predecessor
- [item/seed-contract.md](../item/seed-contract.md) §2 — the four ownership levels
- [effect-pipeline/spec-eligibility-tags.md](../effect-pipeline/spec-eligibility-tags.md) — the built predicate
