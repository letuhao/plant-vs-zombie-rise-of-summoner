# Spec: dungeon-seed-contract

Status: **APPROVED by the owner 2026-09-05 (wave 1) — written against code; not built.** Wave 1, second module.

Module id `dungeon-seed-contract` in the [party-dungeon map](../party-dungeon-map.md) (row 2). Depends on `dungeon-registries` (the seven JSON registries under `data/seed/dungeon/_registry/` and the two tuning schemas). External: demon-seed module 7 `threat-audit` (a `threatBand` on every species anchor — 657 of 841 lack it, `audit-2026-09-05.md` §1(i)), and [item/seed-contract.md](../item/seed-contract.md) as the parent law every rule below specialises. Anchor template copied from [demon-seed/spec-anchor-contract.md](../demon-seed/spec-anchor-contract.md). Model calls: **the pipelines only**; the planner, the audit and the emit are model-free.

## Objective

Define the seven seed shapes the Delve's corpora are written in, the seedsmith adapter that generates them, and the planner that decides — before any model call — what each generated entry *is*. Every field carries one ownership level; no field carries a number; the generation order is derived from the shapes, never written down; a rerun over unchanged inputs is byte-identical; and the cost of a run is printed before it is spent.

Success looks like: `python -m seedsmith dungeon plan --dry-run` prints the §7 table; `dungeon run start --all` fills six domains, ~106 rooms, ~60 events, ~40 encounters and 15 quests under `data/seed/dungeon/`; `dungeon audit` is green; a second run writes nothing; and `delve-graph-roll`, `encounter-generator`, `event-deck`, `delve-quests`, `domain-catalog` and `unique-pipeline` read the committed files with no model anywhere near the runtime.

## Locked anchors (owner, 2026-09-05 — quoted, not paraphrased)

- **Decision 6** (ideal §11.9): *"you define pipelines for seedsmith and generate seed; in game runtime don't use LLM — this seed generator is only contained in seedsmith; in game use the seed structure to generate random event/map/enemy in each dungeon based on our architecture."* Consequence (a): the mode-collapse guard moves **into** the pipeline — a deterministic planner stage before any model call (structure-seed decision 33), per-cell `budget` targets, open-loop flavour review. Consequence (b): runtime generators are pure functions of seed structures.
- **Decision 13** (ideal §11.9): *"the dungeon seedsmith generator runs as ordered sub-pipelines … because a unique 'much binds event / dungeon pattern / boss drop' and needs its dependency seeds resolved first."* The review ruled the listed sequence is the intent and **the tool derives the real order from `reference_fields`** (audit §1(h), S1-5).
- **Decision 15** (ideal §11.9): *"a dungeon can be entered only once or entered many times, that depends on the dungeon … A one-run dungeon drops very strong items and has +7 difficulty."* The domain carries `entry: once | many`.
- **R2** (ideal §11.10): *"`entry: once|many` is **PLANNED** by the seedsmith budget, never a free model pick."* A two-value enum worth +35 Θ is a magnitude by proxy (S1-6).

## Design

### 1. The ownership tables — five levels, one per field

[seed-contract.md](../item/seed-contract.md) §2: *"A field with no declared level is a contract defect."* Its four levels plus the one this program adds:

| Level | Who sets it | In the seed file? |
|---|---|---|
| **AUTHORED** | the model writes it — identity, flavour, judgement | yes |
| **VALIDATED** | the model names it; a frozen registry owns it (`RegistrySet.is_legal`, `adapters/base.py:80-81`) | yes, checked |
| **DERIVED** | code computes it from other fields | never — echoed under `_derived` only |
| **GENERATED** | a generator emits whole rows from the seed | never — `data/generated/` |
| **PLANNED** | **the planner fixes it from the budget before the call; the model is shown it and may not change it** | yes, and the schema pins it as `const` per brief |

PLANNED exists because a wrong ordinal that resolves to a Θ delta is as invisible as a wrong number (S1-6). Every PLANNED field's description ends *"The planner always supplies this; a value different from the brief is a defect, not a choice."* — which is also its `none` statement: PLANNED enums never admit `none` because the planner never leaves them empty.

Every id is PLANNED: the planner mints `<kind>.<cell>-<nnn>` from the cell and a sequence that continues from the high-water mark (seed-contract §7.3). The model never writes an id — four tracking-id defects in the item build (seedsmith-map Appendix A row 6) are the reason. Every anchor carries `_provenance` and `_derived` exactly as `data/seed/demons/species/plant/aerial-flora.json` does; `reason` (AUTHORED free text) is the model's own account of its picks. Negative clauses are abbreviated in the tables; the schema carries them in full and `every_description_names_what_the_field_is_not` enforces their presence. The review's G-pass drafted five clauses (audit §4, G9); that working text is not in the tree, so they are re-derived here on the fields the finding named rather than quoted.

**1.1 Domain** — `domains/<id>.json`. Cell: climate (6) × dangerBand (4) = 24.

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `domainId` | PLANNED | `domain.<climate>-<band>-<nnn>` | not a name — never shown |
| `name`, `flavor` | AUTHORED | free text, no markup | not a description of mechanics |
| `theme` | VALIDATED | `themes.v1.json` (84 rows) · no `none`: a domain without a theme has no loot binding | not the climate; a fire domain may carry a charnel theme |
| `climate` | PLANNED | six `ElementTypeId` (`ActorElementTypes.cs:3-11`) · no `none`: planner supplies | not the theme, not the boss's element |
| `dangerBand` | PLANNED | `shallow · mid · deep · abyssal` → `DangerBand` int via `bands.dangerBand.*` (row 23, `ssot-power-scale.md:638`) · planner supplies; first ship is six `many` domains at `shallow` (band 2 — `very-easy` composes band 0 and is refused, not clamped; `domain-catalog` §7 corrected the earlier `≥ mid` clause 2026-09-05) | not difficulty — rungs are the player's pick |
|`permadeathFromRung`|VALIDATED, optional (added by `domain-catalog` §Drift 4, 2026-09-05)|a rung id from `difficulty-rungs.v1.json`, or absent — absent means `difficulty.permadeathFromRung` (the tuning default, `spec-difficulty-ladder.md` §4); a domain may only RAISE the gate above the default|not a permadeath flag; not a number; never lower than the tuning default|
| `entry` | PLANNED | `once · many` (R2) · planner supplies | not "how long" — size is the layout's |
| `layoutTemplateId` | PLANNED | a `layouts/` id, rotated per cell | not chosen for theme fit |
| `bossSpeciesRef` | VALIDATED | species ids with `threatBand ∈ {tyrant … calamity}` (`demon-threat.v1.json` rungs 7–10), inlined per climate · no `none`: every domain has a boss | not the retinue; not a HypnoAlly flag |
|`firstClearRef`|VALIDATED, optional (added by `unique-pipeline` §5, 2026-09-05)|a rung-80+ `deterministic` unique container id (`item.<slug>`), or `none` — the `dungeon-clear` first-clear grant names it by id (decision 13: *"granted by id and never categorically"*); instantiated at `Θ_boss` on its own stream, banked at the clear|not a table; not a role or frame; never a weight|
| `retinueFamily` | VALIDATED | `families.v1.json` · `none` legal (boss stands alone) | not the boss's own family by default |
| `roomPalette` | VALIDATED, refs | ≥ 1 `rooms/` id per room kind the layout can place · no `none` | not an ordering — the roll picks per cell |
| `questPool` | VALIDATED, refs | ≥ 2 `quests/` ids · no `none` (ideal §11.1) | not rewards — a quest names its own |
| `lootBinding` | PLANNED | room kind → `drop.dungeon.<climate>.<kind>` table id (item `drop-table` kind, planner-emitted) | not weights — the table carries `dropBand` |
| `entranceHint` | VALIDATED | `Lair · Tear · Vault · Anomaly` (`SlotTypeCatalog.cs:14-20`) · no `none`: decision 14 maps each to a theme | not a map position |
| `variants`, `tags` | VALIDATED | seven variants; dungeon tag registry · `none` legal on both | tags are not room kinds |
| **DERIVED, never in the file** | | `sizeBand` (from the layout — S2-12), entrance band int, boss band, row counts, `Θ_content` per row, `onceEntry.*` values | |

**1.2 Room archetype** — `rooms/<id>.json`. Cell: kind (11) × climate (6 + `none`), minus the four climate-neutral kinds = **53**.

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `roomId` | PLANNED | `room.<kind>-<climate>-<nnn>` | — |
| `kind` | PLANNED | `room-kinds.v1.json` (11) · planner supplies | not the event kind — a `curio` room draws `curio` events but is not one |
| `climate` | PLANNED | six elements · `none` legal and the only value for `rest · merchant · boss · unknown` (§3 legality) | not a bias — a `none` room is climate-blind |
| `name`, `flavor`, `reason` | AUTHORED | free text | flavour is not a hint about the outcome |
| `hazardBand` | AUTHORED, **voted** | `none · light · heavy` → `bands.hazardBand.*.hungerPerMille` · `none` legal | not danger — a heavy-hazard room can be an easy fight |
| `sightBand` | AUTHORED | `dim · lit · scouting` · no `none`, and **no default**: the field is required and `additionalProperties: false` makes omission unsampleable (ai-native README §3); a call that returns without it is a QUALITY retry with the defect named, never a silent `lit` | not room size |
| `dispositionBase` | VALIDATED, **voted** on `wild` | `eager · open · wary · hostile` (ideal :1390) · `none` required on every kind but `wild` | not the Δ-band shift — that is runtime |
| `encounterRef` | VALIDATED, refs | an `encounters/` id whose `formation` fits the kind (`fight`/`wild` → `pack`, `elite` → `party`, `boss` → `boss`) · `none` on non-fight kinds | not a species |
| `eventPool` | VALIDATED, refs | ≥ 1 `events/` id whose `kind` fits · `none` on `fight · elite · boss · cache` | not the whole deck — the runtime filters again |
| `secretEligible` | VALIDATED | `yes · no` — a two-value enum, not a bool, so `none` is refused with the reason "secrecy is decided, never unknown" | not "hidden from the map" |
| `tags` | VALIDATED | dungeon tag registry · `none` legal | — |

**1.3 Layout template** — `layouts/<id>.json`. **Model-free: every field PLANNED**, emitted by the planner from `dungeon.v1.json` bands. `layoutId`; `sizeBand` (`short · medium · long`); `widthBand` (`narrow · regular · broad`); `branchiness` (`linear · forked · webbed`); `gateDensity`, `secretDensity`, `oneWayDensity` (`none · sparse · dense`, `none` legal — "no gates" is a layout); `raidModes` (subset of `solo · pair · quad`, never empty). The ideal's `fixedRows` field is **gone** (S2-12): first-row fights, mid cache and rest-before-boss are validator rules in `delve-graph-roll`, not seed truth. A layout carries no prose.

**1.4 Event** — `events/<id>.json`. Cell: kind (6) × planning theme (8, a planner-fixed subset of the 84, recorded by `motifSubsetHash`) = 48.

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `eventId`, `kind`, `theme` | PLANNED | `curio · encounter-event · shrine · trap · bargain · story`; theme subset | kind is not the room kind |
| `name`, `flavor`, `reason` | AUTHORED | free text | flavour never names the good option |
| `climateAffinity` | AUTHORED | six elements · `none` legal (climate-blind) | not an eligibility rule — affinity weights, it never gates |
| `repeatScope` | AUTHORED | `per-delve · per-domain · once-per-player` · no `none`: every event repeats somehow | not `chainRef` |
| `eligibility` | AUTHORED tree over VALIDATED leaves | the twelve `PredicateNode` leaves (`PredicateNode.cs:32` is `HoldsStock`) plus the four `event-deck` adds; leaf arguments are **bands** (`hpBand: low · half · high`), never `Milli` values · `none` = always eligible, legal | not the outcome filter — `EligibilityRule` is for container draws (ideal §11.3) |
| `outcomes[]` (2–4) | AUTHORED | each `{ordinal, dropBand, effects[]}` | not a menu of equal options |
| `outcomes[].ordinal` | **voted** | `good · mixed · bad · nothing` · `nothing` legal only on `story` (validator) | not the player's expectation — a `bad` may read as tempting |
| `outcomes[].consequence` | VALIDATED (added by `event-deck` §5, wave 3) | `none · loot · encounter · scout` · `none` legal and the reading for every row until authored | not an effect — atoms carry effects; a drop, a fight or a scout radius is a consequence handed to its owner (`dungeon-loot`, `Encounter.Build`, `delve-scope`) |
| `outcomes[].dropBand` | **voted** | `staple · frequent · occasional · seldom · exceptional` (`bands.v1.json:451-490`) — **never `weightBand`** (S2-12) | not rarity; not a probability |
| `outcomes[].effects[]` | AUTHORED refs | `{family: atom.*, powerBand}` — the `Instantiator` container the importer builds; `container_id` DERIVED | not a stat write — an event grants |
| `supplyOverride` | VALIDATED | an `override-tags` registry tag (`herbs · key · holy · bait · watch`) · `none` legal | not a supply id — the tag is on the event, the supply carries the tag |
| `chainRef` | VALIDATED, same-kind ref | an `events/` id · `none` required unless `kind: story` | not a prerequisite |

**1.5 Quest** — `quests/<id>.json`. Cell: objectiveTemplate (9) × scope (3) = 27.

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `questId`, `objectiveTemplate`, `scope` | PLANNED | `objective-templates.v1.json` (9); `delve · domain · roster` | template is not the reward |
| `name`, `flavor` | AUTHORED | free text | — |
| `targetRef` | VALIDATED | a **kind** ref (room kind, event kind, species family) · `none` legal for count-only templates | not an id, never a number |
| `countBand` | AUTHORED, **voted** | `few · some · most · all` → `quests.countBand.*Milli` · **`none` legal and required on the six count-less templates** (`kill-boss · extract-with-item-kind · bring-demon-home-alive · finish-under-hunger · survive-no-downed · spend-no-provision` — added by `delve-quests` §1, 2026-09-05) | not a difficulty — `all` on a short layout is easy |
| `rewardBand` | AUTHORED, **voted** | the tier-window ordinals in `quests.rewardBand.*` | not souls, not an item |
| `repeatScope` | AUTHORED | as events | — |
| `prereqRefs`, `chainRef` | VALIDATED, same-kind refs | `none` legal | not unlocks — quests reward, never unlock (ideal §11.3) |
| **DERIVED** | | `riskPaired` — sink-avoidance templates eligible only at rung ≥ `hard` or paired with a risk objective (D14) | |

**1.6 Encounter** — `encounters/<id>.json`. **An encounter is a filter over the species corpus, never a list of species** (S2-13). Cell: formation (3) × elementSpread (3) = 9; density is measured as distinct `(postureMultiset, spread, formation)` shapes, not entries per cell (ideal §11.4).

| Field | Level | Vocabulary · `none` | Is not |
|---|---|---|---|
| `encounterId`, `formation`, `elementSpread` | PLANNED | `pack · party · boss`; `mono · dual · rainbow` | formation is not a count |
| `name`, `reason` | AUTHORED | free text | — |
| `slots[]` | AUTHORED | `{posture, reach, targetPreference, countBand}`; `reach`/`targetPreference` admit `none` (any) | not species; not a role noun — "front-line" is `Bastion ∧ melee\|short` |
| `slots[].countBand` | **voted** | `lone · few · several · many` → `slot.countBand.*.{min,max}` (the `dungeon-registries` vocabulary; `one`/`pair` are spelled numbers and are refused by §2) | not a per-party count |
| `threatWindow` | AUTHORED, **voted** | `{floorRung, ceilRung}` in the ten threat nouns | not a Θ — the offset ladder is tuning |
| `rankOrder` | AUTHORED | slot ids front → back | not a target priority |
| `tempo` | AUTHORED | five `attackTempo` values · `none` legal | not initiative |
| `synergyHint` | VALIDATED | a pair of `TraitBattleCatalog` ids · `none` legal | not a guarantee — the roll may miss it |
| `affixRoll` | PLANNED | a rarity rung id · `none` on non-elite | a rung is breadth and ceiling, never a multiplier |
| `boss` (formation `boss` only) | AUTHORED | `{build: ZombossPattern id (VALIDATED), phasing: none · breakpoint · escalating (VALIDATED — the `dungeon-registries` band; `breakpoint` is one hp threshold, `escalating` two, thresholds in `encounter.v1.json` `phase.*`), phaseTrigger: hp-threshold · round · ally-down · none, signatureAction: action id · none}` | not a species — the domain pins `bossSpeciesRef` at runtime |
| `boss.retinue` | AUTHORED | `{slotRef, countBand}` — the retinue is a slot with a `countBand`; per-party growth is `difficulty.rungs[].bossRetinuePerPartyDelta` and `raid.modes` in tuning (`dungeon-registries` retired `raid.modes.*.bossRetinuePerParty` and there is no `retinuePerParty` int on the anchor) | not a count of parties |

**1.7 Supply / object extension** — `supplies/<id>.json`. The consumable stays where it is (`data/seed/items/consumables/`, fields at `adapters/items/kinds.py:91-94`, `manifestCost` already a structural count). This corpus holds an **extension record**, single-root by construction: `consumableRef` (VALIDATED against the consumable id vocabulary), `overrideTags[]` (VALIDATED, `none` legal — but a supply with `none` here and no `restore` atom is refused, ideal §11.5), `useContextAdds[]` (VALIDATED subset of `rest · curio`). `sizeBand`, `stackBand` and price are **DERIVED** and never in any file. Objects: v1 has none as seed — curios are `events/` rows; anything with `obstacleVerbs` is a structure and belongs to base-defense's `structure-schema` (the `interaction` axis is that program's 18th field, an ask on its map).

**Unique dungeon binding** (the uniques pipeline is `unique-pipeline`; this module owns only the binding fields on the item `unique` kind): `dungeonBinding: {encounterRef, eventRef}` — VALIDATED against this corpus's ids as vocabularies, each `none` legal, both `none` refused for a source-locked unique; `rarity` **PLANNED** at rung ≥ 80 (`firstseed · sunwoven · almanac`); `fixedAtoms[].powerBand` **voted**. A unique is granted by id from a domain table, never categorically.

### 2. The schema audit — four shapes, a stem check, a spelled-number list

`numeric_audit` (`adapters/demons/anchor/audit.py:83-136`) already rejects a bare `number`/`integer`, a `pattern` matching every digit probe (`:34`, `:53-63`), an all-numeric-string `enum` (`:66-73`) and a deny-listed name (`:26-32`, `:76-80`). The dungeon audit reuses it and adds:

- **Stem check** — any property name matching `*weight*` or `*chance*` (case-insensitive, anywhere in the name) is refused. `weightBand` dies here (S2-12); `dropBand` is the one frequency vocabulary.
- **Spelled-number list** — an `enum` containing any of `one … ten` as a member is refused; the fix is a true band with a `{min,max}` tuning row (`countBand: lone`) or an allow-listed structural int with a comment (`phaseCount`).
- **Allow-list**, pinned by test: `{manifestCost}` for this adapter — the one structural count the consumable already carries, with the "never enters balance arithmetic" comment, beside demons' `gameTypeId` (`schema.py:81`). Phase count and retinue size are NOT integers on any anchor: phasing is the `none · breakpoint · escalating` band and retinue is a slot `countBand` (reconciled with `spec-dungeon-registries.md`).
- **PLANNED `const` check** — the per-call schema carries every PLANNED field as `const`; a schema exposing a PLANNED field as a free `enum` fails the audit.
- **Metadata excluded** — `_provenance`/`_derived` are skipped as `emit.py:30-44` skips `_`-prefixed keys; their version ints are not anchor content.

### 3. The adapter, and the derived order

A `DungeonAdapter` registered as `"dungeon"` in `adapters/registry.py:11-16`, one `KindSpec` per shape (`base.py:24-37`). **Intra-corpus references are `reference_fields`; cross-corpus inputs are vocabularies.** `Corpus.load` is single-root, so species, themes, consumables and drop tables enter as `RegistrySet.vocabularies` with their versions recorded — they are frozen inputs, not edges. `planner/ordering.py:68-104` builds kind edges only from declared reference fields; `:107-145` layers them with Kahn; `:148` names any cycle with Tarjan. The order below is what those functions **compute** from §1; no stage label exists anywhere.

```text
frozen inputs   registries.v1 (7)  ·  species (threatBand — threat-audit)  ·  themes/motifs/families  ·  consumables
                          │
layer 0  (no refs)   layout* · supply-ext · encounter · event · quest        * model-free, planner-emitted
                          │  (item corpus, between layers: unique-pipeline reads encounter/event ids as vocabularies;
                          │   the planner emits drop.dungeon.<climate>.<kind> tables listing uniques by id — model-free)
layer 1  rooms      encounterRef → encounter ; eventPool → event
layer 2  domains    roomPalette → room ; questPool → quest ; (lootBinding, bossSpeciesRef: vocabularies)
```

Two honest notes. The brief's "supplies → events" is a **Linkage** finding, not an edge — `supplyOverride` is a registry tag, so `OverrideTagUnsupplied` checks that some supply carries each tag; ordering ignores it. And the planner's **domain plan** (cells, PLANNED values, ids) exists before layer 0 runs — that is what "domains first" in decision 13 correctly names. Same-kind refs (`chainRef`, `prereqRefs`) order nothing (`ordering.py:96-97`) and are checked for cycles by the corpus validator. Forward and cyclic references are forbidden (seed-contract §7.1): a `room` naming an `encounters/` id that does not exist yet rejects at validation, and the run refuses to start on `KindOrder.ok == False`.

`legal_combinations` (`base.py:72`) encodes one real `False`: `kind ∈ {rest, merchant, boss, unknown} ⇒ climate == none`, and the converse for the seven climate-bearing kinds (`none` legal, elements legal). That is what makes 53 cells honest rather than 77 with 24 permanent Coverage false positives.

### 4. The planner (structure-seed decision 33)

Model-free, runs first, output committed as `data/seed/dungeon/_plan/plan.v{n}.json`:

1. **Cells** from `budget.v1.json` (§7) × the legality function; each cell gets `target` and `firstShip` counts and its PLANNED values.
2. **Motif briefs** — for each cell, a disjoint partition of the motif registry (filtered by climate/theme) into `target` slots, allocated **for the full target up front** so adding entries inside the target never rewrites a sibling's brief. Each brief lists its own motifs and **every cell-sibling's motifs as anti-motifs**. Checked after generation by `motif_coverage` and `anti_motif_violation` (`workflow/validators/motif.py:14, :29`) — a closed loop, the mechanism that forced attempt 2 in the measured run.
3. **Ids** minted per cell, sequence from the high-water mark.
4. **Layouts and drop tables emitted** (§1.3; tables from a `dropBand` template per room kind plus the theme's unique ids).
5. **Feasibility** — pigeonhole then Hopcroft–Karp over slot demands (spec-planner §2); an infeasible cell is refused with its binding constraint named.

Closed loops that may contribute to a pass: motif coverage / anti-motif; `SemanticDedup` (`metrics/dedup.py:93`) over names across the whole corpus; per-cell budget **actual vs declared**; shape backstops `field_echo`, `non_empty`, `name_collision` (`validators/field_echo.py:15, :37, :69`) and the §2 audit. Open loop, never a pass: a stratified flavour sample per cell (`report --sample`, stable seed) that produces a review queue; a rejection becomes a new metric, not a content edit.

**Vote set, by cost of being wrong** (3 samples, options permuted by `order_for(entityId, field, sampleIndex)`, `permute.py:16-33`; 2-1 records the minority; 1-1-1 → `unresolved`, never the first option):

| Voted | Why the error is expensive |
|---|---|
| event `outcomes[].ordinal`, `outcomes[].dropBand` | a `bad` read as `good` inverts a deck row; a frequency band skews the whole deck |
| encounter `threatWindow`, `slots[].countBand` | both resolve to Θ-adjacent intervals at runtime |
| room `hazardBand`; `wild` `dispositionBase` | hunger per room; the talk tree's base row |
| unique `fixedAtoms[].powerBand` | a fixed core is never re-rolled |

Permute only, single sample: domain `theme`, `climateAffinity`, `tags`, `sightBand`, `repeatScope`, `tempo`, `synergyHint`, `elementSpread`-adjacent prose. **PLANNED is never asked.** Reasoning stays off (`quality-gates` §2.6); constrained decoding is proven by one real call before the run (`dungeon preflight`).

### 5. Bias controls

Position and label bias are structural for an enum-heavy contract (ai-native §2). Beyond permutation and the vote set: narrow pipelines (one judgement family per call — identity, kit, outcomes, eligibility, composition); closed vocabularies **inlined** in the brief from the registry at emit time, never cited by filename; every closed enum admits `none` or the schema states why (§1); `additionalProperties: false` and every key required so omission is unsampleable; the disagreement rate per field is a deliverable — a near-zero field leaves the vote set, a high one gets its description rewritten. Two repairs with the defect named, then `unresolved` and `blocked` preserved (`pipeline/model.py:18-19`); transient failures replay from checkpoint with no new call.

### 6. Idempotency

Every entry's `_provenance` records `{planHash, briefHash, promptVersions, registryVersions, motifSubsetHash, attempts, confidence, minorityValues}`. `stale_ids()` compares **recorded against current** (never mtime — the shape at `anchor/emit.py:47-68`, built after the 84-entry rewrite defect). The staleness key is `briefHash + promptVersions + registryVersions + motifSubsetHash`; `planHash` is recorded for provenance but a plan that adds a cell must not stale untouched entries, which is why the brief hash is separate and why §4 step 2 allocates motif slots for the full target. A non-stale entry is skipped, not regenerated.

**Encounters are filters, so `threat-audit` stales nothing**: no species snapshot is in an encounter's `registryVersions`. A domain's `bossSpeciesRef` is re-validated at audit time (threatBand still ≥ `tyrant`); a re-band that drops it is a refusal at import, not a regeneration. Layouts and drop tables are pure functions of tuning and registries.

Canonical serialisation as `render_family_file` (`emit.py:87-93`): sorted keys, two-space indent, `\n`, CJK unescaped, explicit nulls, one object per file, `_index.json` per directory. The test is a hash: run twice over unchanged inputs, `sha256` of every file identical.

### 7. Budget and the call ledger

Budget rows live in `data/seed/dungeon/_plan/budget.v1.json` (spec-budget §3 shape: `target`, asymmetric `tolerance`, `dimension`, `rationale`, `provenance`, `loop`). No `budget.v{n}.json` is committed anywhere yet, so this is the first; the ideal's placement under `data/tuning/dungeon.v1.json` is superseded — a runtime tuning file loaded with T5 rejection must not carry keys the runtime never reads.

| Corpus | Cells | Full target | Per cell | First ship |
|---|---|---|---|---|
| rooms | 53 | ~190 | 3.6 (safe band) | ~106 (2 per cell, min) |
| domains | 24 | 48–72 | 2–3 | 6 (one per climate at `shallow`, `many`) |
| events | 48 | ~170 | 3.5 | ~60 |
| encounters | 9 | ~81 | measured as distinct shapes | ~40 |
| quests | 27 | ~54 | 2 | 15 |

**Call ledger** (`entries × pipelines + entries × voted × 2`; heal allowance +15%, two heals bounded per call; ~3.7 s per call on the local 26B model the seedsmith map §3d locks, the rate the review's G16 implies):

| Anchor | Pipelines | Voted | Calls/entry | First ship | Full |
|---|---|---|---|---|---|
| domain | 2 | 0 | 2 | 12 | 120 |
| room | 2 | 1 (+1 on `wild`) | 4 | 424 (+~40 wild) | 760 (+~70) |
| event | 3 | 2 | 7 | 420 | 1,190 |
| encounter | 2 | 2 | 6 | 240 | 486 |
| quest | 1 | 2 | 5 | 75 | 225 |
| layout · supply-ext · tables | 0 | — | 0 | 0 | 0 |
| **base** | | | | **~1,210** | **~2,850** |
| **with heals** | | | | **~1,390 · ~1.4 h** | **~3,280 · ~3.4 h** |

`plan --dry-run` renders every brief and prints this table from the live budget; the table above is the acceptance value it must reproduce for the first ship. **The runtime never calls a model** (decision 6) — the ledger is seedsmith's whole cost.

## Numeric types

No seed file carries a number. The audit's own integer allow-list is `{manifestCost}`, a structural count with a comment; `_provenance` version ints are metadata outside the walk. Every magnitude these seeds imply — hunger per room, `‰` weights from `dropBand`, `{min,max}` counts, Θ offsets, `DangerBand` ints — is resolved by the consuming module from `data/tuning/dungeon.v1.json` / `encounter.v1.json` (owned by `dungeon-registries`) into `long` where it is a magnitude and `int` where it is a bounded count, per the overflow rules the power SSOT carries (`ssot-power-scale.md` §11). This module produces no magnitude and owns no tuning row.

## Commands

```powershell
cd tools\seedsmith
python -m seedsmith dungeon contract --print          # resolved schemas, one per kind
python -m seedsmith dungeon contract --audit          # §2 audit; exit 1 on a finding
python -m seedsmith dungeon plan --dry-run             # §7 table + rendered briefs; DEFAULT, calls nothing
python -m seedsmith dungeon plan --write               # plan.v{n}.json, layouts/, drop tables — model-free
python -m seedsmith dungeon preflight                  # constrained decoding proven by one real call
python -m seedsmith dungeon run start --all | resume | status   # the pipelines, checkpointed
python -m seedsmith dungeon audit                      # schema + stale_ids + budget actual-vs-declared + Linkage
python -m seedsmith dungeon emit                       # canonical files + _index.json
python -m pytest tests/test_dungeon_*.py -q            # transport stubbed to RAISE (test_offline_guarantee.py)
```

The `--dry-run`-as-default follows `report/cli.py:285`.

## Structure

```text
data/seed/dungeon/_registry/                      dungeon-registries' seven files (read, never written here)
data/seed/dungeon/_plan/{plan.v1.json, budget.v1.json}
data/seed/dungeon/{domains,rooms,layouts,events,quests,encounters,supplies}/<id>.json  + _index.json each
data/seed/items/drop-tables/dungeon-<climate>-<kind>.json    planner-emitted, item drop-table kind
tools/seedsmith/seedsmith/adapters/dungeon/
    __init__.py  kinds.py  registries.py  schema.py  descriptions.py  audit.py
    planner.py  briefs.py  pipelines.py  provenance.py  emit.py
tools/seedsmith/tests/test_dungeon_{contract,order,planner,idempotency,budget}.py
docs/architecture/party-dungeon/spec-dungeon-seed-contract.md   (this file)
```

Descriptions live apart from the shape, as the demon anchor does — they change far more often.

## Code style

Match `adapters/demons/anchor/*`: frozen tuples for vocabularies, `SCREAMING_CASE` constants, `OWNERSHIP` dict as the single level table (`schema.py:57-72`), docstrings naming this spec. One room, as emitted:

```json
{
  "roomId": "room.cache-ice-003",
  "kind": "cache", "climate": "ice",
  "name": "Rimelocked Reliquary",
  "flavor": "The offerings froze mid-fall. Nobody came back for them.",
  "hazardBand": "light", "sightBand": "dim", "dispositionBase": "none",
  "encounterRef": "none", "eventPool": "none", "secretEligible": "yes",
  "tags": ["frozen", "reliquary"],
  "reason": "A cache under frost reads as a place worth stopping for and paying a little hunger to reach.",
  "_derived": [],
  "_provenance": { "planHash": "…", "briefHash": "…", "promptVersions": {"room-identity": 1, "room-kit": 1},
                   "registryVersions": {"room-kinds": 1, "tags": 1, "motifs": 3}, "motifSubsetHash": "…",
                   "attempts": {"room-identity": 1, "room-kit": 1}, "confidence": {"hazardBand": "high"},
                   "minorityValues": {} }
}
```

One `KindSpec`:

```python
ROOM = KindSpec(
    kind="room", directory="rooms", namespace="room",
    required=frozenset({"roomId", "kind", "climate", "name", "flavor", "hazardBand", "sightBand",
                        "dispositionBase", "encounterRef", "eventPool", "secretEligible", "tags", "reason"}),
    # Ordering is DERIVED from these two fields (planner/ordering.py) — never from a stage label.
    reference_fields=frozenset({"encounterRef", "eventPool"}),
    motif_expression="a place — what the room looks like and what it costs to cross",
)
```

## Testing strategy

| Test | Asserts |
|---|---|
| `audit_rejects_all_four_smuggling_shapes` | bare integer, digit-admitting pattern, numeric-string enum, deny-listed name — one red fixture each, one green sibling |
| `audit_rejects_weight_and_chance_stems` | `weightBand`, `spawnChance` red; `dropBand` green |
| `audit_rejects_spelled_number_enums` | `["one","two"]` red; `["lone","pair"]` green |
| `allowlist_is_exactly_one` | pins `{manifestCost}`; `phaseCount`/`retinuePerParty` as integer fields are red fixtures |
| `planned_fields_are_const_in_every_call_schema` | a PLANNED field exposed as a free enum fails |
| `every_field_has_exactly_one_level` | over all seven `OWNERSHIP` tables |
| `every_description_names_what_it_is_not` | negative clause present on every attribute |
| `every_closed_enum_admits_none_or_declares_why` | `none` present, or a stated reason (PLANNED: "planner always supplies") |
| `order_is_derived_from_reference_fields` | `derive_kind_order` yields exactly the §3 layers from the seven specs |
| `a_cycle_is_rejected_with_members_named` | inject `encounter → room`; run refuses, `OrderCycle.members` names both |
| `forward_reference_rejects` | a room naming an absent encounter id fails validation |
| `legality_makes_53_cells` | neutral kinds only at `climate: none`; count is 53 |
| `motif_briefs_are_disjoint_within_a_cell` | sibling motifs appear as anti-motifs; no motif shared |
| `rerun_is_byte_identical_by_hash` | two runs over unchanged inputs and a stubbed transport → identical sha256 per file |
| `adding_a_cell_stales_nothing_else` | plan v2 adds a cell; `stale_ids()` returns only the new ids |
| `offline_guarantee` | transport stub raises on any call; suite green |
| `dry_run_matches_the_table` | `plan --dry-run` on the first-ship budget prints §7's counts |
| `budget_actual_vs_declared_closes` | an under-filled cell is a finding; filling it clears it |

## Boundaries

**Always:** one level per field; a negative clause per description; `none` or a reason; ids from the planner; briefs from the registry, inlined; provenance recorded and staleness by value; canonical bytes; `--dry-run` before spend.

**Ask first:** a new anchor kind; widening a registry (owned by `dungeon-registries`); adding to the integer allow-list; adding a field to the vote set; a fourth `useContext` value.

**Never:** a model picks a number, weight, probability or duration; a hand-written stage order; the runtime calls a model; a hosted model tier; a PLANNED field offered to the model as a choice; a species id in an encounter; a `weightBand` or any `*weight*`/`*chance*` name; a bool where a stated enum belongs; an open-loop metric in a pass.

## Success criteria

1. `contract --audit` green over all seven schemas, every smuggling fixture red. 2. `derive_kind_order` reproduces the §3 layers with no stage label in the tree. 3. `plan --dry-run` prints the §7 first-ship table and calls nothing. 4. First-ship corpora emitted; `dungeon audit` green; every cell within tolerance. 5. Second run writes zero bytes (hash test). 6. Full seedsmith suite green with the transport stubbed to raise.

## Interface exposed to dependents

The committed corpora under `data/seed/dungeon/` and their schemas (`contract --print`), nothing else: `delve-graph-roll` reads `layouts/` and a domain's `roomPalette`; `encounter-generator` reads `encounters/` as filter tuples and the domain's `bossSpeciesRef`; `event-deck` reads `events/` (and the extension `supplies/` for override tags); `delve-quests` reads `quests/`; `domain-catalog` reads `domains/`; `unique-pipeline` reads encounter and event ids as vocabularies and the `dungeonBinding` shape. Consumers resolve every band through their own tuning; none reads `_provenance`.

## Design-gate checklist

```
[x] Subsystems: seedsmith adapters/planner/pipeline; the dungeon seed corpora; item seed-contract.
[x] Read this session: party-dungeon-map, ideal §10/§11.1/§11.3/§11.4/§11.5/§11.7/§11.9/§11.10, audit-2026-09-05,
    seedsmith-design skill, ai-native README, item/seed-contract §1-§8, demon-seed spec-anchor-contract,
    structure-seed-ideal §3-§6, seedsmith-map §2/§3d, spec-pipeline, spec-quality-gates, spec-planner,
    spec-workflow-runtime, spec-budget, spec-adapter-demons §1-§2, spec-expeditions (format).
[x] decisions.md: rows at :113-116 appended 2026-09-05; none locks a seed shape.
[x] Every code claim cites file:line (base.py, registry.py, audit.py, schema.py, emit.py, permute.py,
    ordering.py, motif.py, field_echo.py, dedup.py, model.py, cli.py, items/kinds.py, SlotTypeCatalog.cs,
    ActorElementTypes.cs, PredicateNode.cs, bands.v1.json, demon-threat.v1.json, ssot-power-scale.md).
[x] Verified against code, not comments (e.g. dropBand's five members read from bands.v1.json:453-459).
[x] Surrounding sections read for every quoted rule.
[ ] Tested constraints: nothing was run — this spec changes no code; no golden or suite is claimed to move.
[x] §2 invariants: no injector, no private curve, no magnitude in a seed, no second roll SDK.
[ ] Gaps stated: the G9 five clauses are re-derived, not quoted (the pass file is not in the tree); the
    budget file location supersedes the ideal's; supplies → events is Linkage, not an ordering edge.
```
