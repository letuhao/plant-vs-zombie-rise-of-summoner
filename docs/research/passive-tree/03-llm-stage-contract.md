# Passive trees — the language stage, and the fence around it

**Status:** research note, 2026-09-05. **Not a spec. No build authorized.** Stage 2 of the passive-tree
enrichment round: given [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) D13's
generation order — deterministic plan first, then *"an LLM fill vocabulary, categories, atom pools and
bonuses"* — this note specifies that second stage as a **contract with hard validation**.

**Owner constraint, 2026-09-05:** the tree catalog is **static, shared and identical for every player**.
Generation happens at author time; the result is frozen, committed content. Nothing here is a
per-player roll. (The per-player roll exists elsewhere and is a different mechanism — see §6.5.)

Claims are marked **FACT** (verified in code or data this session, with `file:line`), **INFERENCE**
(reasoned from a fact, and the reasoning is shown), or **RECALL** (from a document, not re-verified).

---

## 0. The answer, up front

**The owner asked: "how does the language stage solve the vocabularies? pool? enum?"**

**Neither a pool nor an enum on its own. Three layers, and the middle one is the part that is
usually missed.**

| Layer | What it is | What it stops |
|---|---|---|
| **1. Closed enum, inlined in the JSON Schema** | every id the stage may name is a member of a shipped vocabulary, printed into the request and enforced by constrained decoding | inventing an id |
| **2. ⭐ A per-call PERMITTED SUBSET, not the whole vocabulary** | the planner filters each enum down to the values this node's **quota** allows, before the call | inventing a *distribution* — the failure §9 already measured |
| **3. A declared-target check gate over the emitted corpus** | quotas re-derived independently and compared to a target file; drift fails loudly | a bug in layer 2, and hand edits afterwards |

Layer 2 is the whole answer to the owner's real question. **An unrestricted enum of twelve aptitudes
is exactly what produced Onslaught 39.5% against Ferocity 0.2%** (§9 of the ideal, re-verified: 841
entries in 503 files, counted this session — `data/seed/demons/species/`, 504 files including
`_index.json`). Shrinking or reordering that enum does not fix it. **Removing the wrong options from
the call does.**

And the fence has a fourth property that costs nothing: **there is no numeric field for the stage to
write.** `audit_schema` (`tools/seedsmith/seedsmith/pipeline/model.py:113-206`) rejects a schema that
contains one, at `Pipeline.__post_init__` construction time
(`model.py:250-256`) — before a single call is made. So *"the language stage can never move a balance
number"* is not a policy, it is an unsampleable state.

**One correction the rest of this note rests on:** D22 asserts that composing from the shipped atom
catalog *"hands D14's property-keyed exclusions an existing property space: atom tags."* **That is
half true, and the half that is false is the load-bearing half.** Atom tags are a free-form JSON blob
with no vocabulary and no membership check (`AtomRow.cs:40`, `AtomRowValidator.cs:184`), and the atom
rows shipped today carry only provenance keys. §4 gives the real property space and what it costs to
close it.

---

## 1. What already exists — do not invent a pipeline

Seedsmith is not a sketch. It is a built Python application with a CLI, a metric registry, a
deterministic planner, a workflow runtime with checkpoint/resume, three generation adapters and an
enforced no-numbers contract. **The tree stage is one more adapter, not a new tool.**

### 1.1 The real modules and their roles

**FACT**, counted this session: **201** modules under `tools/seedsmith/seedsmith/` plus **72** test
files under `tools/seedsmith/tests/` — 273 first-party Python files, excluding the vendored
`.venv-verify/`.

| Path | Role |
|---|---|
| `tools/seedsmith/seedsmith/__main__.py:10-13` | 13-line shim — `from .report.cli import main` |
| `tools/seedsmith/seedsmith/report/cli.py` | the entire CLI: `check`, `report`, `metrics`, `demons`, `items generate`, `effects generate` |
| `tools/seedsmith/seedsmith/corpus/` | load a seed folder into a typed graph |
| `tools/seedsmith/seedsmith/budget/` | declarative targets — `derive.py`, `model.py` |
| `tools/seedsmith/seedsmith/metrics/` | 20 metric modules; the check catalogue |
| `tools/seedsmith/seedsmith/metrics/registry.py:18-21` | **an OPEN-loop metric may never gate** — enforced at registration |
| `tools/seedsmith/seedsmith/numerics/resolve.py` | `resolve()` / `explain()` — every magnitude, from bands |
| `tools/seedsmith/seedsmith/planner/` | deterministic findings → work order |
| `tools/seedsmith/seedsmith/briefkit/render.py` | work order → per-partition brief |
| `tools/seedsmith/seedsmith/pipeline/model.py` | `Pipeline`, `audit_schema`, the numeric guard |
| `tools/seedsmith/seedsmith/pipeline/llm_caller.py` | transport, constrained decoding, bounded self-heal |
| `tools/seedsmith/seedsmith/pipeline/run.py` | scratch → gate → move |
| `tools/seedsmith/seedsmith/pipeline/provenance.py` | `Provenance`, `ProvenanceLedger`, `should_generate` |
| `tools/seedsmith/seedsmith/workflow/` | typed state, graph nodes, SQLite checkpoint, `runner.py`'s retry split |
| `tools/seedsmith/seedsmith/workflow/validators/` | the tier-2 deterministic validator battery |
| `tools/seedsmith/seedsmith/adapters/demons/` | the 8-pipeline anchor classifier |
| `tools/seedsmith/seedsmith/adapters/actions/` | 12 stages incl. `distribution_planner`, `validate_heal` |
| `tools/seedsmith/seedsmith/adapters/items/setgen/` · `charmgen/` | the newest precedent (2026-09-04) |
| `tools/seedsmith/seedsmith/adapters/effects/affix/` | affix authoring — the model picks a bundle |

### 1.2 How it separates plan from generated content

**FACT.** Three separate mechanisms, and all three are shipped:

1. **The vocabulary is counted from the corpus on every call, never transcribed.**
   `adapters/items/setgen/vocab.py:113-129` walks `data/seed/items/affix-families/*.json` fresh and
   buckets by `kindId`; an unclassified kind **raises** (`vocab.py:124-127`) rather than being dropped.
2. **The cap is applied in the brief, before the call.**
   `setgen/brief.py:26-34` filters each family's advertised roles down to the twelve legal ones
   *before printing them*, because *"printing that verbatim would put a dropped role in front of the
   model in the same document that tells it those roles do not exist."*
3. **Pricing happens after, and refuses rather than repairs.**
   `setgen/distribute.py:143-144`: *"Nothing here mutates the draft into legality. A draft that broke
   a rule is REFUSED with the rule named, because silently repairing it teaches the next call nothing."*

### 1.3 How it gates distribution

**There is no `--check` / `--emit` flag pair in this repo.** The charter's phrasing describes a shape
that exists under two different names, and both are worth copying:

**(a) `check` / `report` with `--gate` and stable exit codes.**
`report/cli.py:44-47` — `EXIT_CLEAN = 0`, `EXIT_GAP = 1`, `EXIT_CANNOT_RUN = 2`, `EXIT_REFUSED = 3`.
`cli.py:134-138` filters findings to metrics registered with `gates=True` when `--gate` is passed.

**(b) `--dry-run` as the default, `--write` explicitly refused.**
`cli.py:985-991` and `cli.py:329-334` — `items generate --write` returns `EXIT_REFUSED` with a message
saying the generation graph is not wired. The reasoning is at `cli.py:285-289`: *"A real run is ~1,800
model calls; a flag you must remember to pass to avoid spending them is a flag someone eventually
forgets."* **Adopt this verbatim for the tree stage.**

### 1.4 ⭐ The distribution gate the tree stage should copy exactly

**FACT.** `tools/seedsmith/seedsmith/metrics/demon_roster.py` + `data/tuning/demon-roster-targets.v1.json`
is a working instance of *"target distribution declared as data, compared against the emitted corpus,
fails loudly on drift."* Its own docstring (`demon_roster.py:7-8`) states both principles it obeys:
*"Every target here is declared in tuning (P2)"* and *"Every metric here is CLOSED-loop (P3)."*

The loader is three lines (`demon_roster.py:26-28`); the target file is 26 lines of integer per-mille
thresholds (`data/tuning/demon-roster-targets.v1.json:1-26`). Eight metrics compare the emitted anchor
corpus against it — `GridFill` (252 element-pair × aptitude cells), `SingleElementShare`,
`AptitudeDistribution`, `ThreatBandOccupancy`, `RarityMonotonicity`, `FamilySizeSpread`,
`PostureBalance`, `UnresolvedCount`. Every finding carries `evidence` and a machine-readable `remedy`
naming the pipeline that could close it (e.g. `demon_roster.py:97`).

**`DemonRoster/UnresolvedCount` (`demon_roster.py:370`) is the only metric in the repo promoted to
`gates = True`**, and its justification (`:358-365`) is the template for promoting a tree gate: an
unresolved field silently produced zero-stat species, so *"Gating the RATE here stops a full run
early — before spending thousands of model calls."*

Three sibling instances of the same shape: `metrics/pipeline_health.py` +
`data/tuning/demon-pipeline-health-targets.v1.json`; `metrics/corpus_coverage.py` +
`data/tuning/demon-corpus-targets.v1.json`; and — for **quotas** rather than shares — the symmetric
`quota_drift_findings` at `adapters/actions/coverage_report/derive.py:257-282`, which catches
**overshoot** as well as undershoot and **re-derives the quota independently rather than trusting the
stored brief** (`derive.py:70-98`).

### 1.5 The shipped artifact shape

**FACT.** `data/seed/` holds 17 domain folders. A seed entry is a JSON array of objects, each carrying
a `_provenance` block. The real example — `data/seed/demons/species/plant/sunflower-kin.json` — carries
per-pipeline `attempts` and `promptVersions`, a `dumpHash`, `emittedUtc`, per-field `confidence`
(`high`/`split`), `minorityValues`, and a `manualCorrection` block with `from`/`to`/`by`/`why`. Every
content field is an enum id, a string list, or prose. **There is not one number in the entry** apart
from `gameTypeId`, which is an identifier.

Registries live under `_registry/` and carry `schemaVersion`, `registryVersion`, `frozen`, and a `_meta`
block whose fields are as load-bearing as the data: `immutability`, `appendOnlyRule`,
`closedVocabularyNote`, `idGrammarNote`, `sourceRefs`, and — the one worth copying hardest —
`designNotes.cutRationale`, a list of candidate values **that were turned away and why**
(`data/seed/items/_registry/tags.v1.json`). The tree stage's own registries should carry the same block:
it is what stops the next session re-proposing a value that was already argued down.

### 1.6 The two newest adapters, read as precedent

**`setgen`** (`adapters/items/setgen/`, 12 modules) is the closest existing analogue to a tree node
generator, and its `__init__.py:1-19` docstring labels each module by which side of the P1 line it
sits on. The flow, verified end to end:

```text
tuning.load()  →  vocab.build()  →  themes.load_*()  →  holdback_report()
        │  every threshold required, no defaults (tuning.py:94-102)
        │  vocabulary counted from the corpus (vocab.py:113-129)
        │  ungeneratable partitions HELD, not laundered (themes.py:41,66-73)
        ▼
   run.plan_run(...)  →  RunPlan{subjects, held, already_done}
        │  id minted BEFORE the call (emit.set_id); brief built (brief.py:48-85)
        │  RunPlan.complete is False if anything was held (run.py:63-67)
        ▼
   [ model call — schema.set_schema(tuning) as response_format ]
        ▼
   distribute.distribute_set(...)  →  SetPlan{thresholds, roll_plan, problems}
        │  problems empty ⇔ legal; NOTHING is repaired
        ▼
   cells.cell_report / dedup.dedup_report  →  verdict.RunReport.verdict
```

Three things to steal outright:

- **`verdict.GATING_METRICS` is a dict of metric-id → the tuning attribute holding its threshold**
  (`verdict.py:33-39`), and `missing_thresholds(tuning)` (`:50-57`) reports any gate with no resolvable
  threshold. The dry-run JSON prints both (`cli.py:311-323`). A gate with no number is visible before
  the run, not after.
- **`RunReport.verdict` (`verdict.py:83-96`) cannot be laundered:** `pass` requires every gating metric
  to have both *run* and *cleared*; a held partition alone denies a pass; `FAIL` beats `NOT_MEASURED`.
- **`schema.threshold_pieces` (`schema.py:27-28`) is the one legal number** — `{"type": "integer",
  "enum": [2,3,4,6]}`, read from tuning *"so the schema and the distributor cannot disagree about what
  is legal."* An enum of integers is a vocabulary, and `audit_schema` permits it (`model.py:124-126`).

**`charmgen`** (`adapters/items/charmgen/rules.py`) is the same shape, smaller, with one extra lesson:
the forbidden-family set is **computed from the corpus** rather than hand-listed (`rules.py:67-72`),
because *"a hand-list would go stale the first time a family moved."*

---

## 2. The division of labour — every field of a tree node

The repo already has a vocabulary for this and it is four levels, not three
([item/seed-contract.md](../../architecture/item/seed-contract.md) §2, lines 46-60):

| Level | Who sets it | Where the value lives |
|---|---|---|
| **AUTHORED** | the agent chooses it | the seed file |
| **DERIVED** | the importer computes it from authored fields | a column, never the seed |
| **GENERATED** | a generator emits whole rows from authored input | `data/generated/`, checked in |
| **VALIDATED** | the author names it, a frozen registry owns it | the seed file, checked against the registry |

> *"`VALIDATED` is the level the audit was right to separate out: an author writes
> `role: "core-protective"`, but does not get to invent roles. **Naming a value and owning a value are
> different rights.**"* (`seed-contract.md:59-60`)

**Use these four names, not a new triple.** The charter's FROZEN / CHOSEN / FREE maps onto them exactly
— FROZEN is DERIVED-or-GENERATED, CHOSEN is VALIDATED, FREE is AUTHORED — and inventing a parallel
naming here would be the same defect `spec-action-seeding.md` §3 names: *"Inventing a third vocabulary
is the exact defect the atom program exists to stop."*

### 2.1 The table

| Field | Level | Charter reading | Why |
|---|---|---|---|
| `nodeId` | **GENERATED** | FROZEN | Minted by the planner before the call, as `setgen/emit.py` mints `entry_id`. An id the stage chooses is an id that collides and an id that churns between runs |
| `treeId` | **GENERATED** | FROZEN | The tree roster is D9's, derived from the aptitude/element/status/family rosters — never named by the stage |
| `branch` | **GENERATED** | FROZEN | D10: two branches, offensive and defensive, everywhere |
| `tier` | **GENERATED** | FROZEN | D20's `req(t) = 10 + 2.5·t·(t−1)` |
| `unlockRequirement` | **GENERATED** | FROZEN | D20, same formula. A number |
| `prerequisiteNodeIds[]` | **GENERATED** | FROZEN | D13: "skill links" are the plan's |
| `nodeClass` (`mechanism` \| `magnitude`) | **GENERATED** | FROZEN | §3.5's hard requirement — *"the plan must distinguish mechanism nodes from magnitude nodes, and guarantee that deep tiers carry mechanisms."* If the stage chooses this, it will choose `magnitude`, because magnitude nodes are easier to write |
| `potencyBand` | **GENERATED** | FROZEN | D13/R7's per-node potency ceiling. A **band**, resolved to a number by `numerics.resolve` — never authored, never seen by the stage (`seed-contract.md` §3) |
| `shapeArchetype` | **GENERATED** | FROZEN | D15: equal expected value, not equal shape. The archetype is the plan's decision about *this tree*, and the node inherits it |
| `quotaCell` | **GENERATED** | FROZEN | §5. The (aptitude, element, status, nodeClass, trigger) cell this node was allocated. It is what filters the enums |
| `affixIds[]` / `atomRefs[]` | **VALIDATED** | CHOSEN | Named from the permitted subset; membership checked against the shipped affix library. The **affix** is the unit, not the bare atom (`definitions.md` §4a, via DESIGN-GATE §1) |
| `kindId` | **VALIDATED** | CHOSEN — but see note | Legal to name from the 16, but in practice **DERIVED** from the chosen affix's atoms. Do not ask for both; a model asked for two facts that must agree will eventually disagree (`setgen/schema.py`'s own reason for omitting `apCost`) |
| `attachPoint` | **DERIVED** | FROZEN | One per kind, `AtomKindRegistry.Build()`. Never a second field |
| `affixClass` (prefix/suffix) | **DERIVED** | FROZEN | `seed-contract.md:75` — *"Present in a seed file → reject."* |
| `channel` | **VALIDATED** | CHOSEN | From the 267 registered + 9 open prefix families, **filtered to the node's aptitude cell** |
| `elementSlot` | **VALIDATED** | CHOSEN | From `omni` + 6, filtered by quota |
| `statusId` | **VALIDATED** | CHOSEN | From the 21, filtered by quota |
| `trigger` | **VALIDATED** | CHOSEN | From the **11 authorable** of 13 (§3.3) |
| `affinity` (`core` \| `likely` \| `occasional`) | **VALIDATED** | CHOSEN | ⭐ [effect-pipeline-ideal.md](../../architecture/effect-pipeline-ideal.md) §6.4's trick — *"A model cannot be allowed to write `weight: 40`, but it can reliably say 'a fire drake's fire-power affix is **core**.'"* A tuning table turns three ordinals into three weights |
| `exclusion.predicate` | **VALIDATED** | CHOSEN | Keys must come from the plan's emitted property set (§4). Never a node id |
| `exclusion.form` (`reroute` \| `precedence` \| `nullification`) | **VALIDATED** | CHOSEN | D14's ladder, as a three-member enum |
| `name`, `nameKey` | **AUTHORED** | FREE | Bounded by grammar and length; deduplicated |
| `flavor` | **AUTHORED** | FREE | ≤140 chars, no mechanics, no numbers — `family_propose/prompts.py:88-96`'s own rule |
| `printedText` | **AUTHORED** | FREE, template-composed | The exclusion's player-facing sentence. See §4.3 — this is *composed*, not free prose, so both sides print the same rule |
| `rationale` | **AUTHORED** | FREE | **Review queue only.** An OPEN-loop field may never gate (`metrics/registry.py:18-21`) |
| `blocked` + `reason` | **AUTHORED** | FREE | The decline path. Required on every schema (`model.py:199-204`) |
| every magnitude, tier value, weight, duration, chance | **DERIVED** | FROZEN | `numerics.resolve` (`numerics/resolve.py:75-99`). **No such field exists in the schema at all** |

### 2.2 Why nothing numeric is negotiable

**FACT.** `MAGNITUDE_DENY_NAMES` (`pipeline/model.py:63-71`) already contains `tier`, `rung`, `duration`,
`chance`, `cost`, `weight`, `damage`, `hp`, `atk` — plus any name ending `Milli`. The name check
*"fires even when the value is a legal, enum-closed vocabulary, on purpose"* (`model.py:130-136`).

So a field literally named `tier` on a tree node **is refused by the shipped audit**, regardless of its
type. That is why the table above calls the plan's own tier `tier` (it is planner-side, never in a
model schema) and calls the model-facing strength `potencyBand` — a **band**, not a tier, matching
`seed-contract.md` §3's table of *"Instead of / Author writes / Who resolves it"*.

**INFERENCE:** if a tree node schema ever needs to reference a tier window, it does so the way
`setgen/schema.py:14-16` does — by not having the field at all, and letting the distributor read it
from the plan.

---

## 3. The closed vocabularies a tree node touches — counted, not quoted

**Every number below was counted in `src/` this session.** Where a document disagrees, the code wins
and the document is named as stale.

| # | Vocabulary | Declared at | **Counted** | Doc claim | Verdict |
|---|---|---|---:|---:|---|
| 1 | Atom **kinds** | `src/FusionRpg.Core/Effects/Atoms/AtomKindRegistry.cs:31` (const), rows `:476-869` | **16** | DESIGN-GATE.md:40 says 12 | ⛔ stale |
| 2 | Atom **attach points** | `AtomKind.cs:8` (enum), `AtomKindRegistry.cs:21` (const) | **7** | DESIGN-GATE.md:40 says 5 | ⛔ stale |
| 3 | Atom **triggers** | `AtomKind.cs:81` (`AtomTriggers`), `AtomKindRegistry.cs:36` (`TriggerCount`) | **13** | DESIGN-GATE.md:40 says 8 | ⛔ stale |
| 4 | Atom **tags** | — | **not a vocabulary** | D22 implies one | ⚠ see §4 |
| 5 | **Elements** | `Stats/Derived/ActorElementTypes.cs:3`, roster `:21` | **6** (+`omni` sentinel `:19`) | — | ✅ |
| 6 | **Statuses** | `Status/StatusCatalogBootstrap.cs:13` | **21** | 21 | ✅ |
| 7 | **Aptitudes** / **postures** | `Stats/Aptitudes/Aptitude.cs:38` / `:11` | **12** / **3** | 12 / 3 | ✅ |
| 8 | **Derived stat channels** | `Stats/Derived/DerivedStatRegistry.cs:37` | **267** registered + **9** open prefix families | `data/seed/derived-stats/catalog.json` `_meta.counts` prose says 261 | ⚠ the prose is stale by 6; the file's own 53 `entries` still expand to 267 |
| 8b | **Primary stat channels** | `Stats/ModifierOp.cs:26` (`StatChannels`), `All` at `:69` | **23** | — | ✅ |
| 9a | `ActionKind` | `Actions/ActionEnums.cs:10` | **3** | 3 | ✅ |
| 9b | `ActionCategory` | `Actions/ActionEnums.cs:26` | **5** | 5 | ✅ |
| 9c | `ActionTag` | `Actions/ActionEnums.cs:39` | **8** | 8 | ✅ |
| 9d | `ActionTargetMode` | `Actions/ActionTargetSpec.cs:14` | **6** | 6 | ✅ |
| 9e | `ActionAreaShape` | `Actions/ActionTargetSpec.cs:42` | **4** | 4 | ✅ |
| 10a | Item **rarity ladder** | `Items/RarityLadder.cs:16` | **10** | 10 | ✅ |
| 10b | `DemonRarity` | `Demons/DemonRarity.cs:16` | **10** | 10 | ✅ |
| 11 | `AllocationScope` | `Stats/Aptitudes/AptitudeAllocation.cs:8` | **4** | 4 | ✅ (D19 asks for a 5th) |
| 12 | `StatClass` | `Stats/Derived/StatClass.cs:7` | **4** | 4 | ✅ |
| 13 | `UnitClass` | `Stats/Derived/StatClass.cs:29` | **13** | its own doc `:26-28` says ten | ⛔ stale |
| 14 | Item **tag registry** (data-only) | `data/seed/items/_registry/tags.v1.json` | **21** over **7** axes | its own `designNotes.targetCount` says 19 | ⚠ stale by 2 |

### 3.1 ⛔ The DESIGN-GATE atom row is stale on all three of its numbers

**FACT.** `docs/DESIGN-GATE.md:40` reads: *"The vocabulary is **closed**: 5 attach points, 12 kinds,
**8 triggers** (`AtomKindRegistry.TriggerCount`; this row said **7** until 2026-09-03 … **this file wins
over any spec**, so the stale count outranked every correct one)."*

The code it cites says otherwise:

```csharp
public const int AttachPointCount = 7;   // AtomKindRegistry.cs:21
public const int KindCount        = 16;  // AtomKindRegistry.cs:31
public const int TriggerCount     = 13;  // AtomKindRegistry.cs:36
```

`AtomTriggers.All` (`AtomKind.cs:97-101`) lists 13 members; two guard tests pin the const to the array
(`tests/FusionRpg.Core.Tests/Atoms/AtomKindRegistryTests.cs:112`,
`.../TriggerVocabularyTests.cs:45`). [atom-catalog-ssot.md](../../architecture/effect-atom/atom-catalog-ssot.md)
§2, §3 and §4 already carry 16 / 7 / 13, corrected by E34/E35/E36/E37/E41 on 2026-09-04.

**The irony is worth stating because it is the argument for the fix:** that row's own parenthetical
brags about repairing a 7→8 staleness while asserting a number that is wrong by five, *and it claims
precedence over every document that is right.* A row that outranks correct sources must be the one
thing in the repo that cannot drift.

⚠️ Two stale prose sites inside the correct files themselves:
`AtomKindRegistry.cs:6` ("5 attach points, 12 kinds") and `:11` ("all ten opcodes"), fifteen lines above
the consts that say 7 and 16; and `StatClass.cs:26-28` ("ten-class unit ledger… Do not add an eleventh
member") over a 13-member enum. **Evidence rule 2 in action — a comment is not evidence.**

### 3.2 What the tree stage's selection space actually is

Composing the rows a passive node can touch:

| Axis | Members | Filtered per call to |
|---|---:|---|
| Affix ids (the roll unit) | **98** authored families today, expanding to **60 capability + 242 stat picks** with element variants | the node's quota cell |
| Atom kinds | 16 | derived from the affix, never asked |
| Attach points | 7 | derived |
| Triggers | 13 declared, **11 authorable** | the node's `nodeClass` and quota |
| Elements | 6 + `omni` | the tree's own element, or the quota |
| Statuses | 21 | the quota |
| Aptitudes | 12 | the tree's own aptitude |
| Postures | 3 | derived from aptitude (`Aptitude.cs:40-51`) |
| Derived channels | 267 + 9 open prefixes | the aptitude's channel family group |
| Rarity rungs | 10 | not used by trees (see below) |

**Two triggers of the thirteen are not authorable.** `OnGranted` and `OnRemoved` are in `AtomTriggers.All`
but are runtime lifecycle states no kind may carry
([atom-catalog-ssot.md](../../architecture/effect-atom/atom-catalog-ssot.md) §3, citing `definitions.md`
§14.2). **The tree schema's trigger enum therefore has 11 members, and the two are omitted rather than
allow-listed** — an option that cannot be sampled needs no validator.

**INFERENCE — rarity does not apply to a tree node.** A rung selects `prefix_rolls`/`suffix_rolls` and a
tier window for a *rolled* container. A tree node is static, shared content: it has no roll. Its
strength comes from D20's tier ladder and D13/R7's potency band. **Do not put a rarity field on a tree
node** — it would be a second progression ladder, which is what `ssot-power-scale.md` §10 exists to
refuse.

### 3.3 Where the machine-readable mirrors live

The stage does not need to reference C#. Four checked-in mirrors already exist and are drift-tested:

| Mirror | Contents |
|---|---|
| `data/seed/derived-stats/catalog.json` | 53 families + 9 prefix-family rows + the axis expansion rules. ⚠ its `_meta.counts` prose says 261; the entries expand to 267 |
| `data/seed/elements/roster.json` | 6 entries |
| `data/seed/aptitudes/roster.json` | 12 entries, with `role` and `reading` strings |
| `data/seed/channel-pools/pools.v1.json` | 12 entries |

`data/seed/derived-stats/catalog.json`'s `_meta` says exactly why they are families and not expanded
channels: *"hand-listing the expansion here would create a second source of truth with a delay fuse."*
Drift is pinned by `SeedCatalogMatchesCode` and `AtomCatalogSsotDriftTests`.

---

## 4. The property vocabulary for exclusions (D14 / R3)

§6 step 2 of the ideal is unambiguous: *"the plan emits the closed set of properties (tags, conversion
states, damage types) that D14's exclusions key on. **This must exist before any node text is
written.**"* So: what exists to key on?

### 4.1 ⛔ The honest finding — "atom tags" is not a vocabulary today

D22 ([passive-tree-ideal.md](../../architecture/passive-tree-ideal.md):55) says composing from the atom
catalog *"hands D14's property-keyed exclusions an existing property space: atom tags."*

**FACT, and it changes the design:**

| Claim | Reality |
|---|---|
| there is an atom tag vocabulary | **No.** No enum, registry or const list exists anywhere in `src/`. Searched for `AtomTags`, `TagKeys`, `KnownTags`, `TagVocabulary` — no hits |
| tags are validated | **No.** `AtomRow.TagsJson` is `string = "{}"` (`Effects/Atoms/AtomRow.cs:40`); `AtomRowValidator.cs:184` checks only *"is it a JSON object"*, with no membership check — unlike every other atom param, which uses `ParamDef.Vocabulary` |
| atom rows carry semantic tags | **Not today.** Counted this session: 66 atom entries under `data/seed/atoms/**`, carrying only `generatedFrom` (45), `generator` (45), `category` (1), `source` (1) — provenance, not semantics |
| the affix corpus carries them | **Partly.** 98 affix families under `data/seed/items/affix-families/`, and their `tags` use exactly **three distinct values**: `offensive` (41), `defensive` (40), `utility` (17). That is one axis — `combat-posture` from `tags.v1.json` — and nothing else |

**So the property space that D14 needs is three values wide, and it is not enough to write a single
interesting exclusion.** Stating that now costs a paragraph; discovering it after ~1,450 nodes are
generated costs the run.

### 4.2 ⭐ But the *mechanism* is built, and it is the right one

**FACT.** `src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs` ships a property predicate with
exactly the semantics D14 asks for:

```csharp
public sealed record EligibilityRule(
    IReadOnlyList<string> RequireTags,   // bare KEYS, any value: "element" matches {element: fire}
    IReadOnlyList<string> AnyOfTags,     // "key:value" pairs, at least one must match
    IReadOnlyList<string> Allow,         // affix ids admitted regardless
    IReadOnlyList<string> Deny);         // affix ids excluded regardless — always wins
```

`EligibilityResolver.IsEligible` / `.DrawablePool` / `.Validate` are built
(`EligibilityRule.cs:30-95`), including a `UnsatisfiablePool` refusal for a rule that selects zero
eligible affixes of a class with a non-zero roll budget. **Its only callers are its own tests**
(`tests/FusionRpg.Core.Tests/Atoms/EligibilityRuleTests.cs`) — a wiring gap, not an architectural wall.

The tag *source* is decided too, and decided the right way.
[spec-eligibility-tags.md](../../architecture/effect-pipeline/spec-eligibility-tags.md) rules that an
affix's tags are **derived** from its refs' atoms, never authored:

```text
tagsOf(affixId) := union over the affix's CONCRETE refs of AtomRow.TagsJson
                   (a slot ref contributes its slotAtomPattern's family tags, or nothing)
```

built as `AffixTags.Of` (`src/FusionRpg.Core/Effects/Atoms/AffixTags.cs:18`), with a stated safe
direction: an unresolved ref contributes nothing, so *"the derived set can only ever be too narrow,
never too wide."*

**The reason this matters for a generated corpus is the same reason `affixClass` is derived:** *"a model
that names its own class can contradict the bundle it just picked."* A property the stage **derives**
cannot be gamed by the stage.

### 4.3 What the plan must therefore emit — and what it costs

**INFERENCE.** The plan's property set is a **key:value tag registry**, shaped like
`data/seed/items/_registry/tags.v1.json` (7 axes, 21 values, `frozen`, with a `cutRationale`), stamped
onto atom rows at family-emit time so `AffixTags.Of` picks it up. Proposed axes, each named because a
predicate needs it — **not one added speculatively**:

| Axis | Values | The exclusion it makes writable | Source |
|---|---|---|---|
| `element` | `fire ice air earth light dark omni` (7) | *"no effect on converted damage"* | `ActorElementTypes.cs:21` |
| `channelFamily` | the 53 family ids | *"does not stack with another source of the same family"* | `data/seed/derived-stats/catalog.json` |
| `op` | `Flat Increased More` (3 primary) / `Flat Increased Replace Flag` (4 derived) | *"the More-op rule"*, already enforced for sets (`setgen/distribute.py:196-200`) | `atom-catalog-ssot.md` §4.1, §4.2 |
| `posture` | `offensive defensive utility` (3) | the existing axis; keep it, it is already authored on 98 families | `tags.v1.json` `combat-posture` |
| `conversionState` | `none source target` (3) | ⭐ **the D16 axis, and it does not exist yet** | new — see below |
| `contest` | `Contest Race Pool Feeder` (4) | *"has no effect against a target that cannot contest"* | `StatClass.cs:7` |

**Five of six are free** — each is a projection of a vocabulary the repo already closed, so stamping
them is a deterministic function over the atom row, not a judgement. **`conversionState` is the one
genuinely new axis**, and it is new because D16 is new: *"Conversion nodes rewrite element payload
tags, not just magnitudes."* The mechanism it needs is shipped —
`ElementPayload` is a weighted component list validated to sum to 1
(`src/FusionRpg.Core/Combat/Element/ElementPayload.cs:24-38`) — but nothing tags an atom as a converter.

**Cost, stated plainly:** one `data/seed/atoms/_registry/atom-tags.v1.json` (6 axes, ~24 values), one
stamping pass at family-emit time, and one validator turning `AtomRowValidator.cs:184`'s shape-only
check into a membership check. **Until that lands, a property-keyed exclusion can only key on
`posture`, and the D14 mechanism is decorative.** That is a prerequisite, not a nice-to-have.

### 4.4 A worked exclusion, against real repo vocabulary

Take a Fire tree, deep tier, `nodeClass: mechanism`. The plan has allocated it the quota cell
`(aptitude: Ferocity, element: fire, nodeClass: mechanism)` and a `potencyBand: high`.

The stage picks an affix that amplifies fire damage. The conflict it must survive is a *conversion*
node in another tree that turns fire into physical — **a node that may not exist yet**, which is
precisely why the exclusion cannot name it.

**Seed (what the stage writes):**

```json
{
  "nodeId": "tree.fire.off.t5.a",
  "affixIds": ["affix.kindling-wrath"],
  "elementSlot": "fire",
  "affinity": "core",
  "exclusion": {
    "form": "precedence",
    "predicate": { "anyOfTags": ["conversionState:target"] },
    "printedTextKey": "tree.exclusion.precedence.conversion"
  },
  "name": "Kindling Wrath",
  "flavor": "The fire remembers what it was told to burn.",
  "blocked": ""
}
```

**Everything numeric is absent.** `potencyBand` came from the plan; the magnitude comes from
`numerics.resolve(channel, op, tier, tuning, progression, point)`
(`tools/seedsmith/seedsmith/numerics/resolve.py:75-99`), which itself refuses to guess an unshared
channel (`UnsharedChannelError`, `:12-19`).

**The predicate resolves through the built resolver:** `EligibilityResolver.IsEligible` with
`AnyOfTags = ["conversionState:target"]` against `AffixTags.Of(affix)`. It is `O(1)` per node and
covers every conversion node that will ever exist.

**Printed text, composed from a template rather than written free** — so both sides print the same rule
and name the same winner, which is the whole point of Last Epoch's printed no-op
([prior art](../passive-tree-prior-art-2026-09-04.md) §2.3):

> **Kindling Wrath** — *"Applies before conversion. If this skill's fire damage has already been
> converted, this node has no effect."*
>
> **Alloy Discipline** (the conversion node, printing the same rule from its own side) — *"Converts
> fire damage to physical. Nodes that apply before conversion have no effect on converted damage."*

**The ladder, applied in order** ([prior art](../passive-tree-prior-art-2026-09-04.md) §2.3, D14):

| Form | This case | When to escalate |
|---|---|---|
| **Reroute** | *"if the damage is converted, this node instead amplifies the converted type"* — no conflict ever occurs | **Try first.** The stage should be asked for this and only fall through if the affix genuinely cannot reroute |
| **Precedence** | what the example does — the conflict is *defined*, not forbidden | the common case |
| **Nullification** | *"Alloy Discipline will not work"* — a named node declared inoperative | ⛔ **last resort, and it is the only form that names a node**, so it is the only form a generated corpus cannot maintain. Target: zero. If the stage emits one, escalate to review |

**Target rarity ~2% of nodes** (D14, computed from Last Epoch's own Mage tree page). At ~1,450 nodes
that is ~29 exclusions — small enough to review by hand, which is the honest reason the ladder works.

⚠️ **Reroute and Precedence are printed no-ops, not allocation blocks.** Both nodes stay allocatable;
`H` (the Herfindahl focus index, D4) still counts the points spent on the nullified one. **INFERENCE:**
that is correct and should be stated, because the alternative — refunding points on a nullified node —
would make `F` depend on which other trees you took, which §3.2 was written to avoid.

---

## 5. Anti-skew — the quota mechanism

### 5.1 The measurement this exists to answer

**FACT, re-verified this session.** `data/seed/demons/species/` holds 503 species files (504 including
`_index.json`) carrying **841 entries** — matching §9's own count exactly. Its measured skew:
Onslaught 332 (39.5%) against Ferocity 2 (0.2%), a **166× ratio**, with uniform at 8.3%. `earth` 379
(45.1%) against `air` 56 (6.7%), with uniform at 16.7%. Force outnumbers Finesse 2.9:1.

**INFERENCE, and this is the design consequence:** that corpus was produced with the enum *open* — 8
pipelines each free to pick any of 12 aptitudes and any of 6 elements per species. The bias research the
repo already did explains the mechanism
([ai-native-generation/README.md](../ai-native-generation/README.md) §2): position bias and label bias
compound, *"reordering the options alone swings measured accuracy … by up to 75 percentage points"*,
and — the sentence that matters here —

> *"A biased classifier produces output where **every individual answer looks right** and the
> *aggregate* is skewed. Species by species, a reviewer sees nothing wrong."*

**Permutation and voting do not fix this.** Permutation is worth **up to 8 points** of accuracy recovery
(same source) against a 166× spread. They are the right mitigations for *per-entry correctness*; they
are the wrong tool for *aggregate shape*. **Only removing the option from the call fixes aggregate
shape.**

### 5.2 The algorithm

The repo already has the apportionment primitive, and it is arithmetically careful in exactly the way
the repo's overflow rules demand (`AGENTS.md`, Hard boundaries: *"`long` for any magnitude, never
`float`, widen before multiplying, divide by 1000 last, and let overflow throw"*).

**FACT.** `tools/seedsmith/seedsmith/adapters/actions/distribution_planner/derive.py:73-90`:

```python
def largest_remainder_count(weights_milli, order, total) -> "dict[str, int]":
    """`weights_milli` sums to 1000 … Distributes `total` whole units across `order` by largest
    remainder: `long`, widened before multiplying, divided by 1000 last, exactly once. Ties break
    on `order`'s own declared position — a total function, never dependent on `weights_milli`'s
    own dict iteration order."""
```

with `_widen_mul` (`:57-63`) raising `OverflowError` outside the `long` range rather than wrapping, and
`expand_counts` (`:92-104`) flattening `{key: count}` into a deterministic per-ordinal sequence.
`plan_subject` (`:516-540`) already calls it three times over three different totals — category, target
mode, and area shape conditioned on the target-mode allocation.

**The tree algorithm, stated as steps:**

```text
INPUT   trees[]           from D9's roster, itself derived from the aptitude / element /
                          status / demon-family rosters — never hardcoded (§8's rule)
        nodesPerTree      from the tree's shape archetype (D15)
        targets           data/tuning/passive-tree-targets.v1.json   ← declared, not implied

1  N := sum over trees of nodesPerTree                     # the whole catalog, one integer

2  for each quota AXIS a in {nodeClass, trigger, element, status, channelFamily, exclusionForm}:
       quota[a] := largest_remainder_count(targets[a].weightsMilli, ORDER[a], N)
   # exact integer marginals, sum == N by construction, ties broken on declared order

3  seq[a]   := expand_counts(quota[a], ORDER[a])           # deterministic flattening

4  for each tree t, in roster order:
       for each (branch, tier, ordinal) slot in t's archetype, in canonical order:
           cell := { a: seq[a][cursor] for a in AXES }     # one cell per node, no re-draw
           cursor += 1
           # ⛔ HARD CONSTRAINTS OVERRIDE THE DRAW, and they narrow, never widen:
           cell.element   := t.element   if t is an elemental tree
           cell.aptitude  := t.aptitude  if t is a primary tree
           cell.nodeClass := "mechanism" if tier >= t.mechanismFloor      # §3.5's requirement
           node.quotaCell := cell

5  # rebalance: a slot whose draw was overridden returns its drawn value to the pool, and the
   # pool is re-apportioned over the REMAINING slots. Same shape as
   # numerics/rebalance.py's existing redistribution. Without this, overrides silently
   # skew the residual — which is the original defect wearing a planner's uniform.

6  for each node: permittedIds[axis] := the ids of that axis whose tag/enum value == cell[axis]
       # THIS is what is printed into the schema and the brief. Not the whole vocabulary.

7  emit data/seed/passive-tree/plan/<treeId>.json          # model-free, committed, diffable
```

**Step 6 is the load-bearing line.** The permitted subset goes into the JSON Schema's `enum`, so under
constrained decoding an out-of-quota value is **unsampleable**, not merely rejected afterwards. That is
the same technique `family_propose/prompts.py:63-66` already uses — enums shipped empty in the constant
and *"filled at call time"*, `deepcopy`d per call at `:150-160` *"so two calls in the same process never
alias each other's enum."*

**Step 5 is the line that gets forgotten.** An elemental tree's element is forced, so its drawn element
must go back to the pool. Skip it and the forced trees consume their own quota twice — once by force,
once by draw — and the residual free trees inherit the deficit.

### 5.3 The target-distribution data file

Shape, following `data/tuning/demon-roster-targets.v1.json` and `data/tuning/set-charm-gen.v1.json`
(integer per-mille throughout, `_note` recording provenance and honesty about what is fitted):

```jsonc
{
  "schemaVersion": 1,
  "version": 1,
  "domain": "passive-tree",
  "_meta": {
    "owner": "docs/architecture/passive-tree/spec-<module>.md",
    "note": "The SHAPE a generation run must produce and the thresholds a run is judged against.
             No magnitude lives here - those come from seedsmith.numerics (P1). Every vector is
             per-mille and sums to 1000; largest_remainder_count turns it into exact counts.",
    "rosterNote": "No axis lists its own members. Aptitudes are read from
                   data/seed/aptitudes/roster.json, elements from data/seed/elements/roster.json,
                   statuses from the status catalog mirror. A thirteenth aptitude changes this
                   grid by construction rather than by a forgotten edit."
  },

  "quotas": {
    "nodeClass":     { "weightsMilli": { "mechanism": 350, "magnitude": 650 } },
    "trigger":       { "weightsMilli": { "...": "over the 11 AUTHORABLE triggers" } },
    "element":       { "weightsMilli": { "...": "over omni + the 6 roster elements" } },
    "status":        { "weightsMilli": { "...": "over the 21 status ids" } },
    "channelFamily": { "weightsMilli": { "...": "over the 53 derived-stat families" } },
    "exclusionForm": { "weightsMilli": { "reroute": 600, "precedence": 400, "nullification": 0 } }
  },

  "legitimateSkew": {
    "_why": "OPEN ITEM 2 of the ideal's Section 7 lands HERE, argued once, in data - not
             re-litigated per node. A row here is a claim that an imbalance is THEME, not bias.",
    "rows": []
  },

  "gates": {
    "cellOccupancy":     { "medianMax": 2 },
    "quotaDrift":        { "toleranceUnits": 1 },
    "mechanismFloor":    { "minMechanismSharePermilleAtDeepTiers": 1000 },
    "exclusionRate":     { "maxSharePermille": 30, "nullificationMax": 0 },
    "unresolvedRate":    { "maxSharePermille": 50 },
    "nearDuplicateRate": { "maxSharePermille": 5 }
  }
}
```

Two notes on the values shown. `nearDuplicateRate` 5‰ is **Pokémon's measured true near-duplicate
rate** — 18 pairs of 1,025, every one a deliberate designed twin
(`docs/research/game-design/03-roster-scale.md` §2, via `set-charm-gen.v1.json`'s own derivation block).
`cellOccupancy.medianMax` 2 is the band every well-regarded roster in that same measurement sits in
(Summoners War 1.02, HSR 1.7-1.8, Arknights 1.97; Fire Emblem Heroes, the worst documented, is 15.3 with
a max of 129). **Neither is invented here.** The rest are starting values and must say so, exactly as
`demon-roster-targets.v1.json`'s own `_note` does.

⚠️ **`legitimateSkew` is the owner decision the ideal's §7 item 2 already owes**, and putting it in the
target file is the cheapest place to spend it. The ideal's own corollary says why: *"a species' thematic
favour and its mechanical lock need not be the same field. If they are one field, thematic truth (plants
are earthy) becomes mechanical skew (everyone plays earth)."*

### 5.4 The check gate

Copy `metrics/demon_roster.py` module-for-module. Seven metrics, `PassiveTree/*`:

| Metric | Compares | Fails when |
|---|---|---|
| `PassiveTree/QuotaDrift` | emitted per-axis counts vs the target, **re-derived independently** | `abs(count − quota) > toleranceUnits` — symmetric, catching overshoot, per `coverage_report/derive.py:257-282` |
| `PassiveTree/MechanismFloor` | `nodeClass` at tiers ≥ the archetype's floor | any deep-tier node is `magnitude` — **§3.5's "a generator that emits only magnitude scaling produces a tree that measurably does not work"** |
| `PassiveTree/CellOccupancy` | `(channelFamily, sorted trigger+element multiset)` per node | median > 2 — the `cells.py:56-68` key, adapted |
| `PassiveTree/ExclusionRate` | exclusion count / node count, and the form split | rate > 30‰, or **any** `nullification` |
| `PassiveTree/ExclusionResolvable` | every predicate key against the plan's property registry | an unresolvable key, or an `EligibilityResolver.Validate` `UnsatisfiablePool` |
| `PassiveTree/TreeEqualValue` | per-tree summed potency bands vs the plan's budget | a tree outside its budget — D15's *"equal expected value"* made machine-checkable |
| `PassiveTree/UnresolvedCount` | per-voted-field `unresolved` rate | rate > 50‰ — ⭐ **promote this one to `gates=True`**, for the same reason `demon_roster.py:358-365` gives: it stops a full run early, before thousands of calls are spent |

Every finding carries `evidence` and a machine-readable `remedy`. Absence reports `NOT_MEASURED`, never
a pass — `metrics/registry.py:45-52`: *"an absent check is never indistinguishable from a healthy pass."*

---

## 6. The prompt / response contract

### 6.1 The unit of work is one node

**Options, and why one node wins:**

| Unit | Calls | Problem |
|---|---:|---|
| One tree | ~50 | ~29 nodes in one response. Guardrail 2 is *"narrow scope per call — one partition, one kind"* (`pipeline/model.py:11`). A 29-node response cannot carry a per-node quota cell in its schema, so layer 2 of the fence disappears |
| One tier-branch group | ~500 | Better, but the quota cell varies within a tier, so the enum still has to be the union of its members' cells |
| **One node** | **~1,450** | ⭐ **The only unit at which the permitted subset is exact.** Sibling deduplication is handled separately, and the mechanism already exists |

**Sibling dedup without widening the call.** `distribution_planner/derive.py:521-522,575-576` already passes
`accepted_neighbours` and `avoid_neighbour_k` — the k-nearest already-accepted siblings by rendered
fingerprint — into a per-subject brief. The tree stage passes the same node's tier-siblings. **This is
how you get "do not repeat what you just wrote" without putting 29 nodes in one schema.**

**Cost, computed before choosing the shape** (§9 of the ai-native research: *"Cost is a design input,
not a footnote"*):

```text
nodes                 ~50 trees x ~29 nodes                     =  1,450
base calls            1,450 x 1 pipeline                        =  1,450
vote calls            1,450 x 1 voted field x (3 - 1)           =  2,900
                                                                   -----
                                                                   4,350 calls
```

At the demon run's measured rate (16,272 calls ≈ 14 h on the local model) that is **≈ 3.7 h**. Affordable,
and about a quarter of a run this repo has already done twice.

**Vote exactly one field, and name it.** `affixIds` — because being wrong there is expensive to fix later
(it decides what the node *does*), while `name`/`flavor` are cheap to regenerate and `elementSlot` /
`statusId` / `nodeClass` are already narrowed to one or two options by the quota. Voting every field
would take the run to ~13,000 calls for no measured benefit, and `schemas.py:209-211`'s own comment says
adding one voted field *"moves the call budget by a third of the run."*

### 6.2 The request

Following `setgen/brief.py:48-85`'s anatomy, with its two deliberate omissions — **any number**, and
**any option outside the permitted subset**.

```text
SYSTEM
  You author ONE passive skill node for a build tree. You never write a number: not a
  strength, not a duration, not a chance, not a tier — tables you never see decide every
  magnitude. You never invent an effect id, an element, a status or a channel; you pick from
  the lists given, or you set `blocked`. You never name another node — an exclusion keys on a
  PROPERTY, never on a name.

USER
  Tree: {treeDisplayName} — {treeReading}
  Branch: {offensive|defensive}.  Depth: {shallow|mid|deep}.
  This node must be a {mechanism|magnitude} node.
    - a MECHANISM node grants something the resolver does not otherwise have.
    - a MAGNITUDE node makes an existing thing larger.

  Motifs to express: {motifs}.  Avoid entirely: {antiMotifs}.

  Choose, and nothing else:
    1. `affixIds`  — {1..3} from the list below. They are this node's whole effect.
    2. `affinity`  — how central each is to this node: core | likely | occasional.
    3. `exclusion` — only if this node's effect genuinely conflicts with a PROPERTY below.
                     Prefer `reroute`. Most nodes have none.
    4. `name`, `nameKey`, `flavor`.

  Never choose a number, a strength, a duration or a tier. Those are resolved after you answer.

  Legal effects ({n}):        {permitted affix ids, permuted, with one-line descriptions}
  Legal exclusion properties: {the plan's key:value property set, permuted}
  Already written in this tier — do not repeat these: {k nearest siblings, name + effect}

  If this brief cannot carry a node you would be happy to ship, set `blocked` and say why.
```

Four repo rules this obeys, each with a citation:

- **Inline, never cite.** `setgen/brief.py:3-5`: *"a citation teaches the model to write about the
  document instead of the content."*
- **A number that must be conveyed is rendered as a label.**
  `family_propose/prompts.py:193-197` renders rung bands as *"an early tier, with few structural axes
  available"*, *"never the raw pair the model could copy into its own answer as though it were a real
  magnitude."* Hence `shallow|mid|deep` above, never `tier: 5`.
- **Every negative clause also lives in the schema.** `family_propose/prompts.py:40-44`: *"a description
  that lives only in prose beside a schema is a description the audit cannot read."*
- **Options permuted, seeded from the entity id and the sample index.**
  `seed = blake2b(nodeId + "|" + field + "|" + str(sample_index))`, and
  `verify_permutation` (`validate_heal/derive.py:79-92`) **raises** if the rendered order does not
  reproduce `order_for(...)` — so a claimed permutation that did not happen is caught, not trusted.

### 6.3 The response

```jsonc
{
  "type": "object",
  "additionalProperties": false,
  "required": ["affixIds", "affinity", "exclusion", "name", "nameKey", "flavor", "blocked"],
  "properties": {
    "affixIds": {
      "type": "array", "minItems": 1, "maxItems": 3,
      "items": { "type": "string", "enum": [] },   // ← FILLED PER CALL with the permitted subset
      "description": "The effects this node grants. Pick only from the list. This is NOT a
                      description of the node — never invent an id, and never name an effect
                      that is not offered."
    },
    "affinity": {
      "type": "array",
      "items": { "type": "string", "enum": ["core", "likely", "occasional"] },
      "description": "How central each chosen effect is, in the same order. This is NOT a
                      strength and NOT a weight — a table turns these three words into numbers
                      you never see."
    },
    "exclusion": {
      "type": "object", "additionalProperties": false,
      "required": ["form", "propertyKeys"],
      "properties": {
        "form": { "type": "string", "enum": ["none", "reroute", "precedence"] },
        "propertyKeys": { "type": "array", "items": { "type": "string", "enum": [] } }
      },
      "description": "Only when this node's effect genuinely conflicts with a listed property.
                      It is NOT a way to make the node stronger, and it never names another
                      node — use `none` unless the conflict is real."
    },
    "name":     { "type": "string", "description": "The node's own name. It is NOT the tree's
                                                    name and NOT a restatement of the effect ids." },
    "nameKey":  { "type": "string", "pattern": "^tree\\.node\\.[a-z0-9-]+$",
                  "description": "Lowercase-kebab key. It is NOT free text and never contains
                                  a dot inside its body." },
    "flavor":   { "type": "string", "maxLength": 140,
                  "description": "One line a player reads under the name. It is NOT a rules
                                  description: never say what the node does mechanically, and
                                  never write a number, a duration or a range." },
    "blocked":  { "type": "string",
                  "description": "The exact empty string when you WERE able to author the node —
                                  the normal case. Only non-empty when the brief gives you
                                  nothing to work from. Do NOT put a real answer here." }
  }
}
```

**Notes, each earned by a measured defect:**

- `nullification` is **absent** from the `form` enum, not merely discouraged. It is the only exclusion
  form that names a node, so an unsampleable option is a validator you never have to write. If a case
  genuinely needs one, it arrives as a review-queue escalation, not as a generated field.
- `blocked` as an **empty-string sentinel** rather than a boolean, per `anchor/prompts.py:61-83`'s live
  finding: the first-draft description *"let a real local model fill this field with something
  plausible-but-wrong ('plant', echoing the species' `side`) rather than leaving it empty."* The fixed
  wording (`:75-82`) is worth copying verbatim.
- No `tier`, no `potency`, no `channel`. `tier` is on `MAGNITUDE_DENY_NAMES` (`model.py:63-71`) and
  would be **refused at schema construction** even as a closed enum. `channel` is omitted because it is
  derivable from the chosen affix, and a model asked for two facts that must agree will eventually
  disagree (`setgen/schema.py`'s stated reason for omitting `apCost`).
- Both enums ship **empty in the constant** and are filled per call, `deepcopy`d — so `audit_schema`
  audits the *shape*, never one call's pool snapshot (`family_propose/prompts.py:63-66`, `:150-160`).

### 6.4 Determinism and reproducibility

The catalog is static and shared, so **stochastic output is the thing that must not leak**. Three
mechanisms, all shipped:

1. **The output is committed as data.** Confirmed as required by the owner constraint — see §6.5 for
   where.
2. **`should_generate` skips a non-stale entry** (`pipeline/provenance.py:77-95`), checking the
   *finding* before the ledger: *"A finding that a human closed, or that another pipeline closed, must
   stop this one too."*
3. **A rerun over unchanged inputs is byte-identical, proven by hash** — the pattern in
   [ai-native-generation](../ai-native-generation/README.md) §6, *and this repo has already shipped the
   defect it prevents:* the commander-effect generator rewrote all 84 entries on every run, and only a
   byte-comparison found it. Canonical serialisation is part of it — sorted keys, fixed indent, `\n`,
   explicit nulls, CJK unescaped.

⚠️ `ProvenanceLedger.record` **raises** on a re-recorded row (`provenance.py:109-118`) — *"a second write
means idempotence failed"*, loud rather than last-write-wins. Regeneration after a prompt-version bump
therefore needs `provenance-supersede`, which is **core backlog and unbuilt**
([seedsmith-map.md](../../architecture/seedsmith-map.md) §3c). A tree program that plans a v2 prompt
run inherits that dependency.

### 6.5 Where the frozen output lives

**Confirmed: the language stage's output is committed, and in three places, matching
[demon-seed-map.md](../../architecture/demon-seed-map.md) §1's chain exactly.**

```text
data/seed/passive-tree/plan/<treeId>.json      THE PLAN — model-free. Shape, tier ladder,
                                               unlock requirements, links, potency bands,
                                               quota cells, the property registry.
        │  the language stage                  ← the only model calls in the whole pipeline
        ▼
data/seed/passive-tree/nodes/<treeId>.json     THE SEED — enums + prose. No numbers.
                                               Carries _provenance: planHash, promptVersion,
                                               model, confidence, minorityValues.
        │  numerics.resolve  (deterministic)
        ▼
data/generated/passive-tree/<treeId>.json      CONCRETE — every magnitude, checked in,
                                               diffable, reviewable. THIS is what ships.
```

**The static/shared constraint changes one thing and only one.** [demon-seed-map.md](../../architecture/demon-seed-map.md)
§3a's two-layer answer is *"shared definitions, per-player materialisation — only **effects** roll."*
A tree node has no roll, so **there is no `player-materialise` stage for trees.** `data/generated/` is
the end of the chain, and the same bytes reach every player.

✅ **`data/generated/` exists, and the map that says otherwise is stale.**
[demon-seed-map.md](../../architecture/demon-seed-map.md) §1 (written 2026-09-01) states:
*"Honest scope statement: `data/generated/` does not exist … verified, the directory is absent."*
**Counted this session: `data/generated/demons/` holds 830 committed JSON files.** So the middle stage
of the seed → concrete chain has since been built for demons, and the tree stage is following a path
that exists rather than one that is planned. That correction belongs back in `demon-seed-map.md` §1.

⚠️ **What that does not tell you** is whether the *tree*'s own concrete generator is anything more than
`numerics.resolve` in a loop. **INFERENCE:** for a static shared catalog it is exactly that — the plan
already carries the potency band, the channel and the op, and `resolve()` turns the three into a
number. There is no roll to reproduce and no per-player seed to thread.

---

## 7. Validation gates — the exhaustive list of ways content is rejected

Ordered by when they fire. **Every row names a shipped mechanism or says it is new.**

| # | Gate | What checks it | Rejects | Failure shape |
|---|---|---|---|---|
| 1 | **Schema has no numeric field** | `audit_schema` (`pipeline/model.py:113-206`), raised from `Pipeline.__post_init__` (`:250-256`) | bare `number`/`integer`; a `pattern` admitting a bare number (compile-and-probe, `:73-90`); an enum of numeric strings; a **name** on `MAGNITUDE_DENY_NAMES` or ending `Milli`, *regardless of type*; **no `blocked` variant** | `ValueError` at construction — **before any call** |
| 2 | **Every description has a negative clause** | `audit_descriptions` (`validate_heal/schema_audit.py:30-67`) | a missing description, or one with no `not`/`never` sentence | `SchemaDefect(path, "carries no negative clause")` |
| 3 | **Constrained decoding is actually on** | `run_preflight` (`validate_heal/preflight.py:51-91`), one probe call with a single-member enum | a reply whose keys are not exactly `{"acknowledged"}` with the exact value | `"failed"` blocks the run; `"skipped"` never does |
| 4 | **The tuning file is complete** | `_require` / `_validate` (`setgen/tuning.py:94-102`, `:153-201`) | any missing key — *"refusing to substitute a default; an unreviewed number here reaches every generated entry"* | `SetCharmTuningError` at load |
| 5 | **Gates all have thresholds** | `missing_thresholds(tuning)` (`setgen/verdict.py:50-57`), surfaced in the dry-run JSON (`cli.py:321`) | a registered gate with no resolvable number | listed before the run spends anything |
| 6 | **Contract** (G1) | `run_g1` (`validate_heal/gates.py:59-107`) | missing required key; **any extra key**; declared-type mismatch; a `bool` where a number belongs; enum violation on scalars and on array items | `"'x' is not one of […]"` etc., all collected |
| 7 | **Closed-enum membership** | (6) plus constrained decoding | an id outside the printed enum | unsampleable *and* rejected — two layers |
| 8 | **⭐ Quota conformance, per call** | the permitted subset in the schema's `enum` (§5.2 step 6) | any value outside this node's quota cell | unsampleable |
| 9 | **⭐ Quota conformance, per corpus** | `PassiveTree/QuotaDrift`, modelled on `quota_drift_findings` (`coverage_report/derive.py:257-282`) with the quota **re-derived, never read from the brief** (`:70-98`) | `abs(count − quota) > 1`, **in either direction** | `"{cell}: {n} accepted vs quota {q} — overshoot by {d} (+{r:.1%})"` |
| 10 | **Brief conformance** (G2) | `run_g2` (`gates.py:114-177`) | an effect outside `allowedAtomFamilies`; one in `forbiddenAtomFamilies`; an **anti-motif** in the expressed motifs; an axis outside the rung-band budget | hard defects, re-prompted with the defect named |
| 11 | **Numeric fields untouched** | there is no such field (1), plus `audit_no_magnitude_smuggling` over the rendered **brief** (`distribution_planner/derive.py:432-462`) | a bare int / float / numeric string anywhere in a brief | `ValueError("bare numeric field … refused")` |
| 12 | **Potency ceiling** | a `distribute_node` in the `setgen/distribute.py:134-236` mould | summed node potency over the plan's per-node ceiling, or a tree over its budget | `"NodeBudgetExceeded: …"` — **refused, never clamped** (AGENTS.md: absolute bounds throw) |
| 13 | **Op legality** | the `MORE_OP_FAMILIES` refusal shape (`setgen/distribute.py:196-200`) | a `More`-op family where the tree rules forbid one; a derived channel with a `More` op (derived ops are `Flat`/`Increased`/`Replace`/`Flag` — **no `More`**, `atom-catalog-ssot.md` §4.2) | `"SetTierForbiddenAtom"`-shaped, rule named |
| 14 | **Mechanism floor** | `PassiveTree/MechanismFloor` (new) | a deep-tier node with `nodeClass: magnitude` | **§3.5: a magnitude-only deep tier is measurably worthless to a focused build** |
| 15 | **Exclusion properties resolvable** | every predicate key against the plan's property registry, then `EligibilityResolver.Validate` (`EligibilityRule.cs:74-95`) | an unknown key; a rule selecting zero eligible affixes for a non-zero budget | `UnsatisfiablePool`, *"rejected at load, never discovered as a silent under-fill"* |
| 16 | **Exclusion form / rate** | `PassiveTree/ExclusionRate` (new) | rate > 30‰; **any** `nullification`; any predicate naming a node id | rate and the offending ids |
| 17 | **Id stability and grammar** | `emit.set_id` / `IdRefused` (`setgen/emit.py:33-63`), `_assert_container_grammar` (`:101-107`) | a themeKey substituted for an id (two dots); a non-kebab id; a legacy-partition collision; a seq outside 1..899 (900-999 reserved for hand corrections) | `IdRefused` with the rule in the message — **refused, never sanitised** |
| 18 | **No duplicate ids or names** | `name_collision` (`validators/field_echo.py:69-94`) against `context["takenNames"]`; `dedup.dedup_report` exact-match (`setgen/dedup.py:66-96`) | a name already used | ⭐ measured: **83 of 83** commander effects were named identically to their demon, and it was caught by a corpus metric, not a per-item check (`field_echo.py:52-56`) |
| 19 | **Near-duplicate rate** | `setgen/dedup.py`'s **local exact** Jaccard, deliberately not the shared MinHash | rate > 5‰ | ⚠ the shared MinHash over-reports 7× on real pairs (0.120 true vs 0.844 estimated, `dedup.py:6-16`) — *"gating on a signal that over-reports by 7× would fail every run for the wrong reason"* |
| 20 | **Text style** | `field_echo` (`:15-34`), `subject_name_echo` (`:48-66`), `language_consistency` (`language.py:26-44`), `non_empty` (`:37-45`) | a value opening with its own field name (**measured: 7 of 8 outputs began `"DOCTRINE: "`**); a name equal to the subject's; CJK + Latin prose mixed in one value (**measured: 87% code-switched, and the prompt caused it**) | named per field |
| 21 | **Text length** | `maxLength: 140` on `flavor` | over-long flavour | schema-level, unsampleable |
| 22 | **Motif coverage / anti-motif** | `motif_coverage` (`validators/motif.py:14-26`), `anti_motif_violation` (`:29-35`) | a draft using none of the subject's motifs; any anti-motif token | `"uses anti-motif 'x', which this subject is defined against"` |
| 23 | **Reachability** | new, deterministic over the plan | a node whose `prerequisiteNodeIds` are unsatisfiable at its tier; a tier with no reachable node; an orphan | plan-side, **before any call** |
| 24 | **Vote resolution** | `resolve_vote_field` (`validate_heal/derive.py:102`), `verify_permutation` (`:79-92`) | fewer than 3 samples; a rendered order that does not reproduce `order_for(...)`; a 1-1-1 split | `"1-1-1 vote — unresolved, value is None"` — **never silently the first option** |
| 25 | **Persist-time re-gate** | `pipeline/run.py:122-127` — the gate runs **twice** | anything that slipped past the heal loop | `escalated[key] = "failed the gate at persist time"` |
| 26 | **Bounded repair** | `call_with_self_heal` (`llm_caller.py:207-267`), `max_heal` explicit | more than 1 generation + 2 repairs | on exhaustion, `"FAILED:<reason>"` recorded — *"never blank, never silently dropped"* |
| 27 | **Idempotence** | `should_generate` (`provenance.py:77-95`) + a byte-hash rerun test | a second generation of an unchanged entry | `SkipReason.ALREADY_GENERATED`; `ProvenanceLedger.record` **raises** on a duplicate row |
| 28 | **Run verdict** | `RunReport.verdict` (`setgen/verdict.py:83-96`) | a gating metric that failed, or **did not run**, or a held partition | `FAIL` beats `NOT_MEASURED`; a held partition alone denies a `PASS` |
| 29 | **Offline guarantee** | `tools/seedsmith/tests/test_offline_guarantee.py` | a test that reaches a model | the transport stub **raises** on an unexpected call |

**Two things this list deliberately does not gate on.** `rationale` and any "is the node good?" judgement
are **OPEN-loop** — detectable, not machine-verifiable — so they produce a review queue and never a pass
(`metrics/registry.py:18-21` refuses to register an OPEN-loop metric with `gates=True`).
*"An open-loop metric that contributes to a pass verdict is a lie with a checkmark on it."*

**Promote exactly one gate to start:** `PassiveTree/UnresolvedCount`. Everything else runs and reports.
That is the shipped posture — `cli.py:12-14` and `demon_roster.py:370` — and the reason is that a gate
promoted before a real run has been measured against a threshold nobody can name in advance
(`distribution.py:97-98`: *"nobody can name a correct Pielou value in advance"*).

---

## 8. The species-tree pipeline (D23) — why it is separate, and how it stays affordable

D23 makes a demon species tree's reward **a unique tree — nodes no other tree has, with its own
generation pipeline.** D17 locks each species to a build-favour triple (primary tree + element +
status). The scale is 841 species (verified §5.1).

### 8.1 The five differences from the generic pipeline

| | Generic tree pipeline | Species tree pipeline |
|---|---|---|
| **Population** | ~50 trees | **841 species** |
| **Input** | the tree's own roster row — an aptitude, an element, a status | the **species anchor**, which is 18 fields of already-classified judgement plus the almanac lore |
| **Quota axes** | 6 (nodeClass, trigger, element, status, channelFamily, exclusionForm) | the same **plus the D17 favour triple**, which is the axis with the measured 166× problem |
| **Distinctness bar** | *differentiation* — 50 trees must be tellable apart | ⭐ **recognition, not differentiation.** [spec-set-charm-gen.md](../../architecture/item/spec-set-charm-gen.md) D17: *"It does not need to be distinguishable from 903 others; it needs to feel like **that demon**"* |
| **Uniqueness** | nodes drawn from a shared affix library | nodes **no other tree has** — so the pool must be per-species, not shared |

### 8.2 What makes it affordable at 841

**Three levers, and the first is worth more than the other two combined.**

**① Most of the judgement is already done and committed.** The anchor already carries
`aptitudePrimary`, `aptitudeSecondary`, `elementPrimary`, `elementSecondary`, `posture`,
`resourceProfile`, `family`, `traits`, `attackTempo`, `reach`, `targetPreference`, `rarity`,
`deployMode` — plus a `reason` sentence in the species' own terms, per-field `confidence`, and a
`dumpHash`. **The species tree pipeline does not re-classify anything.**
[effect-pipeline-ideal.md](../../architecture/effect-pipeline-ideal.md) §6.3 already tabulates exactly
what each anchor field constrains, and it is the same table a species tree needs.

**② The tree is small and only its identity is unique.** D23's *"nodes no other tree has"* does not
require ~29 bespoke nodes. **INFERENCE, and it is the cost argument:** a species tree is
`(a shared archetype skeleton) + (a small unique core)`. If the unique core is 3-5 nodes and the rest
are the species' favoured-tree nodes re-keyed, the call count is:

```text
841 species x 4 unique nodes                 =  3,364 base calls
841 x 1 voted field x 2                      =  1,682 vote calls
                                                ------
                                                5,046 calls   (~4.3 h, local)
```

versus 841 × 29 = **24,389** for a fully bespoke tree — larger than the whole demon classification run.
**The 4-vs-29 decision is the single biggest cost lever in this program, and it is an owner decision,
not a technical one.**

**③ The favour triple is assigned by the planner, then *inspected*, never chosen.** This is D17's own
wording — *"a deterministic planner → agent-inspects-seed → validated-against-target pipeline, never an
LLM free choice."* Concretely, and it is a different call shape from §6:

```text
1  planner: quota[(aptitude, element, status)] := largest_remainder_count(target, ORDER, 841)
2  planner: assign each species a cell — seeded from speciesId, so it is reproducible
3  the stage receives ONE cell and the species' own lore, and answers ONE question:
     "does this favour fit this creature, and if not, which of these {2-3 alternates} does?"
     — the alternates are also drawn from the quota, so no answer can break it
4  check gate: the emitted favour distribution vs the target, per §5.4
```

**Step 3 is the shape that makes the 166× problem structurally impossible**, because every option the
stage can pick is already inside the quota. It is also cheaper than free choice: a 2-3 way pick over a
narrow set is the task shape enum classification is *most* reliable at, and the `unresolved` rate
becomes a direct measurement of how well the target fits the corpus.

⚠️ **The theme registry it would read is stale by 4.5×.** `data/seed/demons/_registry/themes.v1.json`
ships 84 themes against 841 shipped species entries — filed as a seedsmith defect
([seedsmith-map.md](../../architecture/seedsmith-map.md) §3c-ter), with `theme-refresh` and
`theme-enrich` named as the fix and **unbuilt**. A species tree program inherits that dependency and
should say so at task start rather than discovering it mid-run.

⚠️ **`AllocationScope` has four members** (`AptitudeAllocation.cs:8`) and D19 asks for a fifth
(`status_mastery`). `UniqueDemon` is the scope a species tree gates on and it ships today; the status
trees are the category with no gate quantity, which the ideal's §5 already flags.

---

## 9. Open questions

**Only questions nobody has answered.** A recommendation nobody has disputed is a decision, and an
answerable question is a task — both are recorded above rather than here.

1. **Is `nullification` allowed to exist at all?** §6.3 removes it from the schema on the grounds that
   it is the only form that names a node. D14 lists it as the ladder's last rung. **Recommendation:
   keep it out of the generated corpus and reachable only through a hand-authored override**, which is
   the `allow`/`deny` escape hatch the eligibility rule already has. Needs an owner ruling because it
   narrows a locked decision.
2. **How many unique nodes does a species tree carry — 4 or 29?** §8.2's cost lever. A 6× difference in
   run cost and a real difference in what D23's *"unobtainable elsewhere"* promise means.
3. **What goes in `legitimateSkew`?** The ideal's §7 item 2, unchanged. This note gives it a home in
   `data/tuning/passive-tree-targets.v1.json` and a shape; it does not answer it.
4. **Does a passive node ever carry a trigger the resolver cannot reach?** The atom catalog's runtime
   table shows most kinds are `battle ✖`, and the tree layer's own value is measured against the
   *closed-form allocation model* (§3.3, §3.5), which reads neither. **Unanswerable from documents** —
   it needs the mechanism nodes to exist first, which §3.5 already says: *"Re-measure only worthwhile
   once mechanism nodes exist in the resolver."*

### Gaps I could not close, stated honestly

- **The atom tag registry does not exist** (§4.1). Every property-keyed exclusion in this note assumes
  one. Until it lands, §4.4's worked example cannot be authored — its `conversionState` key resolves to
  nothing. This is a hard prerequisite and I have not costed it beyond "one registry, one stamping pass,
  one validator change."
- **I did not open any of the 830 files in `data/generated/demons/`** (§6.5) — I counted them and
  confirmed the directory is real, but I did not verify that their *shape* is what a tree's concrete
  stage would follow. Someone specifying that stage should read one before assuming it.
- **I did not run the test suites.** Nothing here proposes a code change, so there was no constraint of
  the *"this moves goldens"* shape to test. If a later spec asserts one, evidence rule 4 applies.
- **`nodesPerTree` is the ideal's own open item 1** and every call-count in §6.1 and §8.2 is
  parameterised on ~29 from Last Epoch. They move linearly with it.

---

## 10. Pre-proposal checklist

```
[x] I identified the subsystem(s) this touches — atom layer, seedsmith generation, passive trees.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md, passive-tree-ideal.md, atom-catalog-ssot.md, spec-action-seeding.md,
    demon-seed-map.md, seedsmith-map.md, item/seed-contract.md, spec-set-charm-gen.md,
    effect-pipeline-ideal.md §5-§6, spec-eligibility-tags.md, ai-native-generation/README.md,
    passive-tree-prior-art-2026-09-04.md.
[x] I checked decisions.md for a lock covering this — via DESIGN-GATE §1's atom row, which is
    what guards the attach-point list, and which is STALE (§3.1).
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — and found three comments contradicting the
    code fifteen lines below them (§3.1).
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. NOT APPLICABLE and said so in §9:
    this is a research note proposing no code change, so no "moves goldens" claim is made.
[x] Nothing contradicts a §2 invariant. Two are load-bearing here and both are obeyed:
    #11 (no hard ceilings — the potency ceiling REFUSES rather than clamps, gate 12) and
    #12 (the balance surface is data — every threshold lives in a tuning file, §5.3).
[x] Corrections are propagated: §3.1 names DESIGN-GATE.md:40, AtomKindRegistry.cs:6 and :11,
    and StatClass.cs:26-28 as the four sites needing the fix. THEY ARE NOT YET EDITED —
    this note does not touch docs/ outside its own file, and the DESIGN-GATE row explicitly
    outranks every other document, so amending it is an owner call, not a side effect.
```

---

## 11. Related

- [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) — D13-D23, §6 generation order, §9 the measured skew
- [passive-tree-prior-art-2026-09-04.md](../passive-tree-prior-art-2026-09-04.md) — R3/R4, the exclusion ladder
- [ai-native-generation/README.md](../ai-native-generation/README.md) — the one law, enum bias, the checklist
- [seedsmith-map.md](../../architecture/seedsmith-map.md) — P1-P5, and the modules §1.1 lists
- [item/seed-contract.md](../../architecture/item/seed-contract.md) §2-§3 — the four ownership levels
- [item/spec-set-charm-gen.md](../../architecture/item/spec-set-charm-gen.md) — the closest analogue, module 13
- [effect-pipeline/spec-eligibility-tags.md](../../architecture/effect-pipeline/spec-eligibility-tags.md) — the built predicate
- [effect-atom/atom-catalog-ssot.md](../../architecture/effect-atom/atom-catalog-ssot.md) — 16 / 7 / 13, and why each moved
