# Spec: `tree-review`

**Status:** spec, 2026-09-05, with owner decisions **D37–D41 folded in** the same day. Module of
[passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-review` · **Wave:** 3 · **Depends on:** `tree-catalog` · **Depended on by:**
`species-tree`
**Model calls:** none. This module spends human attention, not tokens.

---

## Objective

Make a 35,160-node catalog reviewable by one person, and make the second pass cost `O(diff)`.

D24 makes review a **shipping requirement**: the catalog is what ships, so *"reviewed, then
committed"* is the contract, not a nicety. The honest baseline is that the last corpus of this shape
was **gated by machine and spot-checked by hand** — nobody read it
([13-review-pipeline.md §1.4](../../research/passive-tree/13-review-pipeline.md)). This module exists
so that does not repeat under a decision that forbids it.

**The one-line contract:** the unit of review is the **tree**, never the node — because a full
node-by-node read is arithmetically excluded (§1) and because the failure this corpus is most likely
to actually have is only visible between trees (§3.3).

---

## Design

### 1. The review budget

#### 1.1 The corpus, counted this session

| Category | Trees | Nodes each | Nodes |
|---|---:|---:|---:|
| Aptitudes | 12 | 40 | 480 |
| Elements | 6 | 40 | 240 |
| Statuses | 21 | 40 | 840 |
| **Shared subtotal** | **39** | | **1,560** |
| Demon species | **840** | 40 | **33,600** |
| **Total** | **879** | | **35,160** |

**FACT, counted 2026-09-05.** `data/seed/demons/species/` holds **840** anchor entries across **502**
non-`_` files, **840** distinct species ids, and `_index.json` carries **840** keys. Species tree size
is 40, not 29: D30 as amended defers to D10's one-shape rule and D29's 10 tiers × 2 branches
(`passive-tree-ideal.md:62`).

⚠ **The ideal's headline figure is 35,200 and its §9 skew table is over 841.** Both were computed
with the stale duplicate included. The verified corpus is **35,160 nodes over 879 trees**, and §9's
table shifts by two cells at 840: `dark` is **70**, not 71, and `unresolved` is **11**, not 12 —
because the stale copy is the extra `dark` and the indexed `SnorkleZombie` is one of the eleven
unresolved. Onslaught 332 (39.5%) against Ferocity 2 (0.24%) is unchanged, and so is the **166×**
ratio D17 and D32 are aimed at. The difference is not material to any decision; it is recorded
because a review pipeline that cannot count its own population has no business certifying one.

#### 1.2 A full read is arithmetically excluded

There is **no measured human review rate anywhere in this repo.** The only figure written down is an
assumption — *"At 30 seconds of human review per node"* (`06-red-team.md:298`). It is adopted here
with its sensitivity stated, not dressed up as evidence.

| Rate per node | Whole corpus | Working weeks at 40 h |
|---|---:|---:|
| 15 s — reading a node line inside a card, tree context already loaded | **147 h** | 3.7 |
| 30 s — the repo's own figure | **293 h** | 7.3 |
| 60 s — real judgement: read it, compare it to siblings, check its exclusion | **586 h** | 14.6 |

**There is one reviewer.** A full node-by-node read is between seven weeks and four months of doing
nothing else. This is not a scheduling problem to be negotiated; it is excluded by arithmetic, and
any pipeline that pretends otherwise ends up in the baseline's position — *"trusted, not reviewed"*.

#### 1.3 What the budget buys instead

Three levers, and the second is worth more than the other two combined.

1. **Move properties from the human to the machine** wherever a machine can decide them (§4).
2. **Change the unit of review from the node to the tree** — it divides the population by 40.
3. **Make the per-unit review fast** with an artifact built for judgement, not inspection (§5).

| Line | Hours | Basis |
|---|---:|---|
| Tree census, 879 cards @ 90 s | **22.0** | §5's card; 14.7 h at 60 s, 44.0 h at 3 min. **All 879 — the 39 shared trees included**, ~58 min of the total (closed question 4) |
| Tier 1 census — exclusion nodes, ~1,055 @ 30 s | **8.8** | 30‰ of 35,160, the `exclusionRate` cap |
| Tier 2 — 60 tree cards @ 90 s | **1.5** | §3 |
| Tier 3 — 200 nodes @ 30 s | **1.7** | §3 |
| **One full pass** | **≈ 34** | |
| Building the card and the corpus sheet | 10–15, one time | §5. Every input is already committed |

**Budget two to three passes, not one.** The demon corpus needed **three** corpus-wide reprompts after
its first run completed — `attackTempo` (entropy 0.00), `rarity` (59‰ unresolved → 17‰, 2,584 calls,
~106 min, `tasks/demon-corpus-self-heal-todo.md:281`), and `sunwoven` (0/840 → 4, after the bar was
rewritten, `:355-370`). So the realistic human cost of the first catalog is **≈ 78–117 hours**,
front-loaded, and steady state after that is `O(diff)` (§6).

### 2. What is claimed, and what is not

Sampling supports a claim about a **population**, at a **confidence**. Naming the population is the
step that gets skipped, so it goes first.

| Design | Population | The claim it supports | The claim it does **NOT** support |
|---|---|---|---|
| **A. Node sample** | 35,160 nodes | *"the corpus-wide reviewer-rejection rate is p ± e"* | anything about any particular tree |
| **B. Tree cluster sample** | 879 trees | *"at most X% of trees carry a rejectable defect"* | anything about a **named** tree |
| **C. Tree census** | 879 trees | *"every tree was looked at by a person"* | that every **node** was read |

**D30's value is per-species recognition, and recognition does not pool.** *"It does not need to be
distinguishable from 903 others; it needs to feel like that demon"*
(`03-llm-stage-contract.md:973`). No sample of 60 trees certifies 879 identities. So A and B are
quality-control instruments **for the generator** — they tell you whether to *start* the census — and
**C is the only design that discharges D24 for a species catalog.**

**Say this in the acceptance record, in these words:** *"Every tree was judged. Individual nodes carry
the machine's gates plus a sampled human rate with a ±5% margin."* Do **not** write *"the catalog was
reviewed"* unqualified. That unqualified sentence is precisely the overclaim the demon baseline made.

### 3. The sampling design

#### 3.1 The numbers, computed this session

Exact Clopper–Pearson one-sided upper bounds at α = 0.05, and Wald sample sizes at z = 1.96 with
p = 0.5 (the worst case). **Acceptance sampling — what a clean sample proves:**

| n trees read | 0 rejects ⇒ true rate ≤ | 1 ⇒ ≤ | 2 ⇒ ≤ | 3 ⇒ ≤ |
|---:|---:|---:|---:|---:|
| 20 (the pilot) | 13.91% | 21.61% | 28.26% | 34.37% |
| 45 | 6.44% | 10.11% | 13.34% | 16.34% |
| **60** | **4.87%** | 7.66% | 10.12% | 12.42% |
| 90 | 3.27% | 5.16% | 6.83% | 8.39% |
| 150 | 1.98% | 3.12% | 4.14% | 5.09% |

**Rate estimation — what a sample measures**, ±5% at 95%: **384** for an infinite population,
**381** at N = 35,160 nodes, **268** at N = 879 trees. The finite-population correction is negligible
at node scale and material at tree scale, which is itself an argument for the tree as the unit.

#### 3.2 The three tiers

**Tier 1 — CENSUS.** Some populations are small enough and risky enough to read whole.

| Population | Expected size | Why census, not sample |
|---|---:|---|
| Nodes carrying an **exclusion** | ≤ 1,055 (the 30‰ `exclusionRate` cap; D40's target is ~2%, i.e. ~703 — the budget is sized on the cap) | D14's whole mechanism, and **all three forms including `nullification`** (D40). A wrong predicate is a silent no-op the player never sees fire |
| Nodes the run **escalated** — `FAILED:<reason>` after bounded repair | small | Already known-bad; the machine asked for a person |
| Nodes with an **unresolved vote** (a 1-1-1 split) | ≤ 50‰ by gate | The demon run's **695** two-to-one splits were resolved by majority and no human adjudicated one. Do not repeat that |
| Every entry in the **review queue** | should be zero | §7 — a queue nobody counts is a hiding place |

**Tier 2 — CLUSTER SAMPLE over trees, for the generator's health.** Draw **60 trees**, read each whole
via its card. Zero rejects ⇒ *"at most 4.87% of trees carry a rejectable defect, at 95% confidence."*
Cost at 90 s/card: **90 minutes.**

Stratify on the axes where the corpus is most likely to fail **unevenly** — the neglected-corner
principle the shipped sampler's own docstring states (*"a corpus fails in its neglected corners"*,
`tools/seedsmith/seedsmith/sampling/__init__.py:5-7`):

| Stratum axis | Levels | Why this axis |
|---|---:|---|
| **Favour triple** (D17: aptitude × element × status) | the quota cells | The axis with the measured 166× problem |
| **Side** | 2 (plant / zombie) | Two lore corpora, one prompt |
| **Rarity rung** | 10 | The corpus is bunched; the other rungs are the corners |
| **Tree category** | 5 (primary / elemental / status / family / species) | Five different briefs |

Use `stratified_sample(...)` (`sampling/__init__.py:37-60`) — **do not write a second sampler.** It
already guarantees *"every non-empty stratum gets at least one sample"*, apportions the remainder by
largest remainder, and seeds from `metric id + corpus revision` so *"a reviewer can re-read exactly
what they read last week and diff their own judgement against it"* (`:9-11`). That reproducibility is
what makes a second reviewer's disagreement measurable at no extra sampling cost.

**Tier 3 — THIN NODE SAMPLE over rare quota cells.** A 60-tree cluster sample under-covers rare cells
by construction: a status holding 4‰ of the quota appears in ~140 nodes corpus-wide and will rarely
land inside 60 trees. Draw **~200 nodes** stratified by quota cell, with the same one-per-stratum
guarantee. Cost at 30 s: **1.7 h.** Claim: *"no quota cell is systematically broken."* This is the tier
that catches *"every `frostbite` node is the same sentence."*

#### 3.3 The design effect, stated honestly

Cluster sampling costs statistical efficiency: `deff = 1 + (b−1)·ICC`, with b = 40 nodes per cluster.
If defects cluster within a tree — and they will, because a bad tree is usually a bad *brief* — the
ICC is high, and 60 trees × 40 nodes is worth far less than 2,400 independent nodes for estimating a
node-level rate.

**That is the right trade, and it should be made deliberately.** A high ICC means the tree is the
natural unit of failure, and therefore of rejection and of regeneration. Reading 2,400 nodes as 60
whole trees buys a *tree-level* claim that names an action; reading 2,400 scattered nodes buys a
precise node-level rate that names none. The ICC itself cannot be known before a corpus exists — the
pilot (§8, open question 1) measures it.

### 4. What machines check, so humans do not

#### 4.1 Closed completely by machine — do not sample these

[`tree-language`](spec-tree-language.md) §7 owns the validation gates — **24 of them** — and it is
the only document that numbers them. **Every citation here names a gate and never its ordinal.** The
research doc's numbering has already drifted twice against the spec's: the mechanism floor is gate 16
now, not 14, and near-duplicate is 20, not 19. A numbered citation in a sibling spec breaks silently
the next time a gate is inserted, and it breaks in the direction that looks correct.

These are the properties those gates close outright:

| Property | Gate | Why a human adds nothing |
|---|---|---|
| **No number was authored by the model** | **Schema has no numeric field** | The field does not exist in the schema. `audit_schema` refuses at `Pipeline.__post_init__` before a call is made — an unsampleable state, not a policy |
| **Every id is real** | **Constrained decoding is actually on** · **Contract** | Constrained decoding **plus** enum membership. Two layers |
| **Distribution / skew** | **Quota conformance, per call** · **`PassiveTree/QuotaDrift`** | The permitted subset makes an out-of-quota value unsampleable; `QuotaDrift` re-derives the quota independently and catches overshoot too. **This is the 166× failure, fully machine-closed** |
| **Budgets, potency ceilings, per-tree equal value** | **`PassiveTree/TreeEqualValue`**, over `tree-plan`'s budget arithmetic | Arithmetic. Refuses rather than clamps (D15 made machine-checkable) |
| **Op legality** | **Brief conformance** | A `More` op on a derived channel is a vocabulary fact |
| **Reachability, orphans, unsatisfiable prerequisites** | **Plan reachability** · **`PassiveTree/ExclusionResolvable`** | Graph arithmetic over the plan, before any call |
| **Id grammar, stability, collisions** | **`PassiveTree/NameCollision`**, plus `tree-catalog`'s `IdRefused` | `IdRefused`; `name_collision` against `takenNames` |
| **Length, field echo, subject-name echo, language mixing** | **Text style** | Measured defects with mechanical signatures — *7 of 8 outputs began `"DOCTRINE: "`*, *87% code-switched* |
| **Idempotence, determinism, offline** | **Idempotence** · **Offline guarantee** | Byte-hash comparison |
| **Balance in real combat** | — | `tools/CombatSim` drives the real dispatcher; `DemonQualityReport` §4 is the working precedent at 840-entry scale |

✅ **`PassiveTree/TreeEqualValue` is OWNED — resolved 2026-09-05.** It is not among
`tree-language`'s 24 because it is not a language-stage gate. `spec-tree-plan.md` §3.2 claims and
defines it, and the seam is a real one worth stating: **the metric has two halves under one name.**
The **plan-side** half runs over the emitted plan alone, before any model call, and proves the
*budget* is equal. The **content-side** half — the one this module runs, over `tree-binder`'s prices
— proves the generated content *honoured* that budget. Same name, different input, different stage.
**Neither substitutes for the other**, which is exactly why this module may skip hand-sampling
budgets: the plan half already refused an unequal one.

**So "is balance machine-checkable?" is answered yes, twice over:** budget and quota conformance by
construction, and *consequential* balance by simulation over a corpus of exactly this size.

#### 4.2 Only proxied by machine — sample these

| Property | What the machine gets | What it misses |
|---|---|---|
| **Motif / theme adherence** | token presence, anti-motif tokens, the **Brief conformance** gate | A node that uses every motif word and means none of them |
| **Diversity** | normalized Shannon entropy per vocabulary | Entropy is high when 879 trees use all 12 aptitudes evenly **and are all one tree wearing 12 hats** |
| **Mechanism floor** | is `nodeClass` `mechanism` at deep tiers — **`PassiveTree/MechanismFloor`** | Whether the mechanism *does* anything. `nodeClass` is a plan-side label, so the gate checks the plan against itself. **The behavioural half REPORTS — see below** |
| **Near-duplication** | lexical Jaccard over 5-gram shingles — **`PassiveTree/NearDuplicate`** | **Semantic** sameness. `metrics/dedup.py:12-17` states this as a deliberate documented gap — conceptual clustering ships only once `axis` reaches the adjective entries |

✅ **The deep-tier behavioural sample REPORTS; it does not gate — settled 2026-09-05.**
[`tree-plan`](spec-tree-plan.md) §4 owns the mechanism quota and raised the question in its own open
list: does a `CombatSim` score over a sample of deep-tier mechanism nodes **block the catalog** or
**file a finding**? **It files a finding**, and the reason is one line: the score is a proxy for a
proxy — a win-share delta at one scope, over a *sample*, of a quota that is itself a stand-in for
*"is this interesting"* — and this module does not let an instrument that thin deny a lot a pass. It
is the same posture D40 takes on nullification: answer a reading risk with presentation, not with
removal.

**It is not toothless.** The finding lands on the corpus sheet and in the verdict queue, and a
*systemic* result — a whole quota cell scoring at zero — is a rung-3 or rung-4 trigger (§6.2), which
fixes the plan or the prompt corpus-wide. The escalation ladder is where a behavioural finding gets
its force, not §6.4's unshippable list. `PassiveTree/DeepMechanismValue` registers with
`gates = False`. **That is a choice, not a registry requirement** — the simulated half of H3 is
CLOSED-loop and could legally gate, unlike the OPEN-loop metrics §4.3 covers. It does not, for the
reason above, and saying so keeps the two arguments apart.

#### 4.3 No machine answer at all — this is the review

**This is the repo's own ruling, enforced in code.** `MetricRegistry.register` raises — *"an OPEN-loop
metric may never gate (P3) — refusing to register"*
(`tools/seedsmith/seedsmith/metrics/registry.py:18-21`). The shipped instance is
`Quality/FlavourGeneric` (`metrics/quality.py:51-66`): *"Is the writing any good — has no machine
answer, so this NEVER reports a pass/fail. It writes a stratified sample into a review queue."*

**So "is flavour machine-checkable?" is answered No, in code, already.** Five irreducibly human
properties, and where each is paid for:

| # | Property | Why no machine decides it | Tier |
|---|---|---|---|
| **H1** | **Name ↔ effect coherence** — does "Kindling Wrath" plausibly mean *this* effect? | Uniqueness and well-formedness are provable. Meaning is not a property of the string | 2, 3 |
| **H2** | **Flavour quality** — is the line worth reading? | Ruled OPEN-loop in code | 2 |
| **H3** | **Is a mechanism node interesting?** | **Decomposes, and half is machine-checkable.** *"Does it change anything measurable"* → simulate in `CombatSim` and read the win-share delta. *"Is it legible and worth building toward"* → human | sim + tiers 1–2 |
| **H4** | **Species recognition** — does this tree read as *that* demon? | The point of D23/D30, with no referent but the lore | **census** |
| **H5** | **Corpus-scale sameness** — are 879 trees secretly one tree? | Lexical dedup catches copies; this failure is 879 *different* sentences expressing one idea | tier 2 + the corpus sheet |

**H5 is the failure this corpus is most likely to have, and the one no per-node review can ever
see.** A reviewer reading node 4,112 in isolation has no way to notice it is the 400th variation on
*"rage when hurt."* That is why §5 spends its leverage on side-by-side rendering rather than on
prettier node inspection.

### 5. The tree card — where the leverage is

Everything above assumes a reviewer can judge a tree in 90 seconds. **Nothing about raw JSON permits
that.** Three surfaces, generated deterministically from the committed plan plus the committed
concrete catalog, and diffable.

#### 5.1 The card

One card per tree, one screen, no scrolling.

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│ SnorkleZombie · undead / aquatic-reanimates · zombie · sprout  [23 green] 4/40 │ <- header
│ favour: Bulwark · earth · chilled       archetype: gated-deep     budget 98%   │
├───────────────────────────────────────────────────────────────────────────────┤
│ "Its ability to bypass projectile defenses and approach stealthily makes it a  │ <- RECOGNITION
│  significant tactical threat rather than a mere nuisance."                     │    anchor: the
│  traits: submerged-stealth · bullet-resistant-underwater · amphibious-approach │    species' own
├──────────────────────────┬────────────────────────────────────────────────────┤    words
│ OFFENSIVE                │ DEFENSIVE                                          │ <- the LATTICE
│ t1  Silt Bite       +8%  │ t1  Mudskin           +6% physical mitigation      │    2 x 10, one
│ t2  Undertow       +12%  │ t2  Held Breath       +9% chill resistance         │    line per node
│ ...                      │ ...                                                │
│ t7  * Drag Under         │ t7  * Silt Shroud                          [excl]  │    * = mechanism
│ t10 * Deep Water Claim   │ t10 * The Water Remembers                          │
├───────────────────────────────────────────────────────────────────────────────┤
│ NEAREST SIBLINGS   ZombieShark 0.71 · Snorkelmancer 0.68 · TideGhoul 0.64      │ <- the SAMENESS
│   Silt Bite / Undertow / Drag Under   vs   Reef Bite / Riptide / Pull Under    │    panel
├───────────────────────────────────────────────────────────────────────────────┤
│ NEEDS A PERSON  1 flavour sampled · 1 exclusion · 0 unresolved votes           │ <- OPEN-loop only
│ [ accept ]  [ reject: ______________ ]  [ owner ]                              │
└───────────────────────────────────────────────────────────────────────────────┘
```

The header, the anchor sentence and the traits are read straight out of the committed anchor — the
example above is `SnorkleZombie`'s real `reason`, `traits`, `family` and `rarity` from
`data/seed/demons/species/zombie/undead.json`. **The card costs nothing to assemble, because every
judgement input is already committed.**

#### 5.2 Six design rules, each earned

1. **Hide everything the machine already proved.** Twenty-three green gates are one chip, not
   twenty-three rows. A reviewer who reads machine-verified facts is spending human attention on a
   machine's job. Only OPEN-loop and `NOT_MEASURED` findings get a line — the discipline
   `metrics/registry.py:34-52` already draws.
2. **The whole tree at once, in a fixed lattice.** 2 × 10 is 20 rows of two cells. A lattice is
   *one entity's own bounded content*, so it is a GG-61 surface, not the GG-50 collection rule —
   `RENDER_ALL_MAX = 24` (`web/fusion-rpg-web/src/layers/creatures/CreaturesLayer.tsx:21`) governs
   collections, and a 40-cell fixed lattice is not one. The tree is exactly the size a person can take
   in without scrolling, which is not a coincidence: **it is why the tree is the right review unit.**
3. **Render every effect through the shipped magnitude contract, never as a raw channel id.**
   `formatMagnitude` takes a `Magnitude` carrying a `UnitClass` and **has no overload for a bare
   number** — that omission is the deliberate GG-46 guard
   (`web/fusion-rpg-web/src/i18n/magnitude.ts:5-15`). A review surface that renders `label={id}` makes
   the reviewer decode ids instead of judging content, and it is a live defect elsewhere already.
   ⚠ **This constrains where the renderer lives** — see §5.4.
4. **The species' own `reason` sentence sits beside the nodes.** H4 is *"does this read as that
   creature"*, and the question is only askable if the creature is on the same screen.
5. **⭐ The sameness panel is the highest-value element and the hardest to skip.** Three nearest trees
   by fingerprint over `(node names, chosen affix ids, quota cells)`, rendered as parallel name lists.
   `metrics/dedup.py` already computes MinHash/LSH neighbours in O(n) buckets, so the neighbour list is
   free. **This makes H5 — corpus-scale sameness — a visible property of a single card.** Nothing else
   in this design does that, and no amount of node-level review ever will.
6. **The verdict control writes data, not prose.** Accept / reject-with-reason / escalate-to-owner
   append to `data/seed/passive-tree/_review/<lot>.json`. That file is then an **input to the next
   run's brief** — a reject reason becomes an anti-motif — and to the metric that counts the rejection
   rate. A review that produces no machine-readable artifact cannot be measured, and a review nobody
   can measure is indistinguishable from one that did not happen.

#### 5.3 What a reviewer is actually asked

Three questions, cheapest first:

1. **Does anything look wrong at a glance?** (a repeated name, an empty tier, a mechanism at t1)
2. **Do these forty nodes read as this creature?** (H4)
3. **Is this tree meaningfully different from the three beside it?** (H5)

Three yeses is an accept — that is the 90-second path. Any no, and the reviewer types one reason and
moves on. **Diagnosis is not the reviewer's job**; §6's triage is the pipeline's.

#### 5.4 Where the renderer lives — one implementation of the magnitude contract, not two

Rule 3 has a consequence that must not be discovered during the build. The concrete catalog is
produced by `tools/PassiveTreeGen` in **C#** ([`tree-catalog`](spec-tree-catalog.md) §5, *"Seedsmith
stops at the seed"*), and the player-facing string is composed in **TypeScript** by `formatMagnitude`
plus the `DisplayLine` template (`web/fusion-rpg-web/src/contract/types.ts:97`). A third renderer, in
Python or in C#, would be a second source of truth for how a number reaches a person — the exact
defect `data/seed/derived-stats/catalog.json`'s `_meta` refuses for channel expansion (*"hand-listing
the expansion here would create a second source of truth with a delay fuse"*).

**So the card renderer is a Node script inside the web package**, beside the two that already ship
there (`web/fusion-rpg-web/scripts/check-bundle.mjs`, `gen-tokens.mjs`). It imports `formatMagnitude`
directly, reads `data/generated/passive-tree/`, and writes static HTML. The reviewer then sees the
string the player will see, by construction.

**Fallback, if a renderer must live elsewhere:** a drift test pins it against a fixture set produced
by the TypeScript one, the shape `SeedCatalogMatchesCode` and `AtomCatalogSsotDriftTests` already use.
It is strictly worse — a drift test proves two implementations agree today — and it is named only so
the choice is made deliberately rather than by accident.

#### 5.5 The corpus sheet — one page for the whole lot

Read **before** the tree cards, to decide whether the census is worth starting at all.

| Panel | Shows | Answers |
|---|---|---|
| **Quota grid heat map** | the (aptitude × element) cells, coloured by count vs quota | is the 166× failure back? |
| **Name-token frequency** | the 50 commonest words across all node names, with tree counts | ⭐ H5 at corpus scale — if `wrath` appears in 300 trees, the census is premature |
| **Exclusion census** | every exclusion, its form, its predicate, its printed text | tier 1, in one list |
| **Nearest-neighbour top 20** | the 20 most similar tree pairs by fingerprint | the worst sameness offenders, named |
| **Rejected so far** | rejection rate against the acceptance number, live | when to stop and reprompt |
| **Machine verdict** | every gate `PASS` / `FAIL` / `NOT_MEASURED`, plus `missing_thresholds` | a gate with no number is visible **before** the run |
| **Hidden-file census** | every `_`-prefixed file under the seed roots, **and how many were visited** | §7 |

**Rendering the sheet writes a row, and the census will not start without it.** The sheet carries a
`sheetRevision` — the `catalog_revision` pair it was rendered from, plus a hash of its own inputs — and
`trees review --census` **refuses to start** a lot whose `_review/<lot>.json` holds no `sheetRead` row,
or whose row names a different `sheetRevision` than the sheet on disk. The row is
`{lot, sheetRevision, by, utc}`, written when the reviewer dismisses the sheet.

This is the smallest honest mechanism for *"read the corpus sheet first."* It does not prove anyone
understood the name-token panel; it proves the sheet for **this** revision was opened and closed by a
named person at a recorded time, and it fails loudly when a reviewer starts a census against a stale
sheet — which is the failure that actually happens.

#### 5.6 What not to build

**Do not build an interactive tree editor.** The repo already made this call in an adjacent place —
`stages/world/xyflowGuard.test.ts` enforces that the world stage abandoned `@xyflow/react` for
hand-rolled rendering on an authored grid. A review surface must be *generated, diffable and
regenerable*; an editor is a second source of truth for content that is supposed to come from a plan.

**Per-tree cards are regenerated, not committed.** 879 HTML files would dominate every diff while
carrying no information their inputs do not already carry. **The corpus sheet and the verdict queue
are committed** — the sheet because its per-lot diff is the review's own history, the queue because it
is not derivable from anything. This is a deliberate narrowing of doc 13 §6's *"checked in beside
them"*, and it is stated so it reads as a decision rather than an omission.

### 6. Escalation and rejection

#### 6.1 The principle the repo already chose

`setgen/distribute.py:143-144`: *"Nothing here mutates the draft into legality. A draft that broke a
rule is REFUSED with the rule named, because silently repairing it teaches the next call nothing."*
**A rejection names the rule and regenerates; it never edits.**

The one sanctioned exception exists and keeps its discipline: the `manualCorrection` block, which
records `from` / `to` / `by` / `why`. **A hand correction is legal, must be provenance-stamped, and its
rate is itself a metric.** Above a declared threshold it means the prompt is wrong and the batch should
have been rejected.

#### 6.2 The ladder

| Rung | Trigger | Action | Cost |
|---|---|---|---|
| **0. Auto-repair** | a gate fails inside the run | `call_with_self_heal`: 1 generation + 2 repairs, then `FAILED:<reason>` recorded — **never blank** | in-run |
| **1. Node reject** | one node fails H1/H2; its siblings are fine | Regenerate **that node**, with the reviewer's reason appended to the brief as an anti-motif. Never hand-write it | ~3 calls |
| **2. Tree reject** | ≥ 2 nodes rejected in one tree, **or** the tree fails H4 | Regenerate the **whole tree**. A tree-level defect is nearly always a brief-level defect | 40 base + 80 vote calls |
| **3. Cell reject** | the tier-3 sample shows a quota cell is systematically weak | Fix the cell's permitted subset or its motif set **in the plan**, regenerate every node in the cell | cell size |
| **4. Batch reject → REPROMPT** | the tier-2 reject count reaches the acceptance number, **or** any tier-1 census finding is systemic | **Stop. Fix the prompt.** Redeploy corpus-wide at pipeline scope | ~1 call per unit |
| **5. Owner escalation** | a decision the plan cannot make; a `legitimateSkew` question. ~~a `nullification` exclusion appears~~ — **withdrawn 2026-09-05 (D40)**: the form is sanctioned, so its mere existence escalates nothing. A nullification whose **presentation** fails (§6.4 rule 2) is a reject, not an escalation | Queue it. Do not resolve it inside the run | — |

**Rung 4 is not hypothetical — it is what the demon corpus actually did, three times**
(`tasks/demon-corpus-self-heal-todo.md:270-370`). It costs about one call per unit at pipeline scope,
**which is exactly why it is affordable to be strict.**

#### 6.3 The acceptance numbers

From §3.1, for a 60-tree tier-2 sample:

| Rejects in 60 | 95% upper bound | Verdict |
|---:|---:|---|
| 0 | 4.87% | **Accept** — proceed to census |
| 1 | 7.66% | **Accept with a named finding** — the rejected tree regenerates (rung 2), census proceeds |
| 2 | 10.12% | **Hold.** Draw 30 more; 2 in 90 ⇒ ≤ 6.83% |
| ≥ 3 | 12.42% | **Batch reject (rung 4).** More than one tree in ten is bad — fix the prompt |

⚠ **These are starting values and must say so in the file that holds them**, exactly as
`demon-roster-targets.v1.json`'s own `_note` does, and for `distribution.py:97-98`'s stated reason:
*"nobody can name a correct Pielou value in advance."* They are calibrated on the pilot (§8) and they
live in `data/tuning/passive-tree-targets.v1.json` — **the balance surface is data**, not a report.

#### 6.4 What makes a lot unshippable

Copy `RunReport.verdict`'s discipline (`setgen/verdict.py:83-96`): **`FAIL` beats `NOT_MEASURED`, and
a single held partition denies a pass.** A tree lot is unshippable when any of:

1. A gating metric **failed or did not run.** An absent check is never a pass (`metrics/registry.py:34-52`).
2. **An exclusion fails its presentation contract** (D40). ~~*Any `nullification` exclusion exists.*~~
   was the wrong rule and is not restored; **this is the rule the owner's decision does license, and
   this module may now enforce it.** Every exclusion — all three forms — must satisfy all three:
   **(a)** both sides print the rule; **(b)** both name the **same** winner, in the same words;
   **(c)** the catalog marks the loser **inert**, never un-unlocked, so
   [`tree-surface`](spec-tree-surface.md) §8 can render a trait the player still owns. A pair that
   prints on one side only, names two different winners, or asks the surface to hide the node is a
   **hard finding, and it denies the lot a pass.** `PassiveTree/ExclusionRate` reports the rate; the
   presentation check is `PassiveTree/ExclusionPresentation`, and unlike the rate it gates.
3. `PassiveTree/UnresolvedCount` exceeds 50‰ — the one metric promoted to `gates=True`, for
   `demon_roster.py:357-370`'s stated reason: gating the *rate* stops a full run early.
4. Any tier-1 census population is **unread**. A census is not a sample; partial is failure.
5. The tier-2 acceptance number is reached (§6.3).
6. `QuotaDrift` exceeds tolerance **in either direction** — overshoot is a defect too.
7. Any node is unreachable, or any prerequisite is unsatisfiable.
8. **Any `_`-prefixed file under a seed root holds an entry** (§7).
9. The concrete catalog does not regenerate byte-identically from unchanged seeds — `--check`, the
   shipped pattern at `tools/DemonSpeciesGen/Program.cs:17`: *"compare against what is on disk; write
   nothing; exit 1 if anything differs."* Without it, *"identical for every player"* is a claim about
   one build machine.

✅ **The owner ruled, 2026-09-05 (D40): the narrowing is OUT and all three forms stay.** Reroute →
Precedence → **Nullification** all survive, *"so the generator is never forced to refuse a pair it
cannot reroute or order."* The previous fold was right to withdraw the old rule 2 — a spec may not
narrow a locked decision by listing the consequence as an unshippable condition — and it was right
that a warning is not a decision. **What it could not do was enforce anything. That is now lifted:**
the owner answered the *"reads like a bug"* risk with **presentation, not removal**, and a
presentation requirement is precisely the kind of thing this module is built to enforce.

So the treatment of an exclusion is now:

| | Reports | Gates |
|---|---|---|
| **How many** exclusions exist, and of which form | `PassiveTree/ExclusionRate`, against `exclusion.targetShareMilli` (~2% — D14, restated by D40) | no — a rate above target is a finding, and the corpus sheet's exclusion census carries it |
| **Whether each one is presentable** | — | **yes** — `PassiveTree/ExclusionPresentation`, §6.4 rule 2 |
| **Whether each one is any good** | tier 1 reads **every** exclusion — census, not sample, unchanged | the reviewer's verdict, through the ladder |

**`nullification` gets no special-case treatment in any row of that table.** It is the rung the
generator reaches when it can neither reroute nor order a pair, it is censused like every other
exclusion, and it is judged on the same three questions. The one thing it must not be is *quiet* —
which is what the presentation gate is for.

### 7. The `_`-prefix blind spot — a named gate

**FACT, verified this session.** `tools/DemonQualityReport/Program.cs:77` skips any file whose name
begins with `_`:

```csharp
if (Path.GetFileName(file).StartsWith('_')) continue; // notes/exemplars, matching AtomImporter's own convention
```

`data/seed/demons/species/zombie/_needs-review.json` begins with `_`. It holds one entry: a stale
2026-09-02 copy of `SnorkleZombie`, while `_index.json` points at a newer 2026-09-04 copy in
`zombie/undead.json`. The two disagree:

| field | indexed copy | stale copy in the review bucket |
|---|---|---|
| `rarity` | `sprout` | `chaff` |
| `elementPrimary` | `earth` | `dark` |
| `family` | `["undead","aquatic-reanimates"]` | `["aquatic undead"]` |

This is **the exact defect class Phase A2 of the self-heal plan closed 217 times**, and one instance
survived — invisible to the tool that then reported *"840 anchor entries on disk, 840 distinct species
ids, 840 indexed — clean."* Read the whole file (437 lines) and it never reads a word of prose either:
the only string comparisons are enum membership and id equality, so `reason` and `traits` are
uninspected by any machine, by anyone, today.

**Two rules this module carries, and one metric that enforces both:**

1. **A review queue that is merely a file is not a review queue.** It must be *counted* by the same
   report that certifies the corpus, or parking something in it removes it from every metric.
2. **A gate with an exclusion rule has a blind spot the size of that rule.** The `_`-prefix skip was a
   reasonable convention borrowed from `AtomImporter`; it silently became a hole.

`PassiveTree/HiddenFileCount` walks every seed root **without the skip**, counts entries in every
`_`-prefixed file, and is a hard finding at one. It also reports `visitedFileCount` — how many files
the walk actually opened — because a green metric that visited nothing and a green metric that visited
forty empty files are the same output and completely different facts. It appears on the corpus sheet
(§5.5) and in §6.4 rule 8. **The species pipeline must not inherit the blind spot it is being built
next to.**

### 8. Incremental re-review

**Only the diff is re-reviewed, and node-id stability is what makes that sentence mean anything.**

[`tree-catalog`](spec-tree-catalog.md) §3 settles the scheme: a **composed structural slug**
`skill.<treeId>-<branch>-t<tier>-<nodeKey>`, with a content hash and a positional ordinal both
refused. That is not a nicety for build guides — **it is the single decision that makes a second
review pass possible at all.** With stable ids, re-review is `O(diff)`. With content-hash ids, every
rebalance is a full 35,160-node re-review: 293 hours, i.e. never.

| Change | What re-review costs | Why |
|---|---|---|
| **Magnitude retune** (`data/tuning/` only) | **Zero human review.** Machine gates only | `tree-catalog` R6: a retune touches no id and migrates nothing. Nothing a human judged has changed |
| **New trees** (a new species, a new element) | Full protocol over the new lot only | The lot is its own population; the acceptance numbers apply unchanged |
| **Prompt-version bump** for one pipeline | Re-review the fields that pipeline owns, over the trees it touched | The shipped `rerun --pipeline <id>` scope. Cards render only the changed lines, highlighted |
| **Plan change** (tier ladder, quota targets, archetype) | Full re-review of the affected trees | The plan is the brief; a changed brief is new content |
| **Node retired** (`enabled: false`) | Census the retirements | Small, and each has a player-visible consequence |

**The diff card** is the same card in a second mode: the tree as it now is, changed nodes highlighted,
the previous value struck through in place. **A reviewer judges a change inside the tree it lives in,
never as an isolated line** — §3.3's argument, applied to time instead of space. The lot's identity is
the `catalog_revision` pair `(from, to)`, that counter being the repo's one monotonic import counter,
bumped once per transaction and only when something changed.

**Two hazards, named now rather than discovered on pass two:**

⚠ **`ProvenanceLedger.record` raises on a re-recorded row** — *"a second write means idempotence
failed"* (`tools/seedsmith/seedsmith/pipeline/provenance.py:109-118`), loud rather than
last-write-wins. Regenerating after a prompt bump therefore needs `provenance-supersede`, which is
core seedsmith backlog and **unbuilt**. **An incremental review pipeline cannot run its second pass
without it.**

⚠ **A retired node makes an actor unloadable today.** `AptitudeAllocation.Single` throws on an unknown
id (`src/FusionRpg.Core/Stats/Aptitudes/AptitudeAllocation.cs:36-39`), called per row from the store.
`tree-catalog` R5 already owns the fix — reject once at a defined import boundary, naming every
offending id in one report, never lazily per actor load.

---

## Commands

```powershell
# render a lot: cards + corpus sheet, from committed plan + concrete catalog
node web/fusion-rpg-web/scripts/render-tree-cards.mjs --lot <lot> --out .review/<lot>

# draw the samples - seeded, reproducible, one per non-empty stratum
python -m seedsmith trees review --lot <lot> --tier 1        # the census populations
python -m seedsmith trees review --lot <lot> --tier 2 --n 60 # the cluster sample
python -m seedsmith trees review --lot <lot> --tier 3 --n 200
python -m seedsmith trees review --lot <lot> --census    # every tree; refuses without a sheetRead row

python -m seedsmith trees review --lot <lot> --verdict       # acceptance number vs rejects so far
python -m seedsmith trees review --diff <fromRev> <toRev>    # the O(diff) pass
python -m seedsmith check data/seed/passive-tree --gate      # exit 1 on a gates=True finding
python -m pytest tools/seedsmith/tests/test_tree_review.py

dotnet run --project tools/PassiveTreeGen -- --check         # byte-identity, exit 1 on drift
```

`--dry-run` is the default everywhere a run could spend calls, and `--write` must be passed
explicitly — `cli.py:285-289`'s reason applies unchanged: *"a flag you must remember to pass to avoid
spending them is a flag someone eventually forgets."*

## Project structure

```text
tools/seedsmith/seedsmith/adapters/trees/review/sample.py     the three tiers, over sampling/
tools/seedsmith/seedsmith/adapters/trees/review/fingerprint.py tree fingerprint + nearest siblings
tools/seedsmith/seedsmith/adapters/trees/review/verdict.py    acceptance numbers, the ladder
tools/seedsmith/seedsmith/metrics/passive_tree.py             PassiveTree/* incl. HiddenFileCount,
                                                              ExclusionPresentation (gates, D40) and
                                                              DeepMechanismValue (reports, S4.2)
web/fusion-rpg-web/scripts/render-tree-cards.mjs              the card + corpus sheet (S5.4)
data/tuning/passive-tree-targets.v1.json                      acceptance numbers, sample sizes
data/seed/passive-tree/_review/<lot>.json                     the verdict queue - COMMITTED
docs/research/passive-tree/_review/<lot>/sheet.html           the corpus sheet - COMMITTED
tools/seedsmith/tests/test_tree_review.py
```

Registers into the existing `metrics/registry.py` rather than standing beside it, so `check --gate`
picks it up without a second entry point.

## Code style

Match `metrics/demon_roster.py`: a metric is a class, returns typed findings with a severity and a
loop kind, declares its `needs`, and **never prints**.

```python
class HiddenFileCountMetric(Metric):
    """Every `_`-prefixed file under a seed root, counted WITHOUT the skip that hides it.

    `DemonQualityReport/Program.cs:77` skips `_`-prefixed files - a convention borrowed from
    AtomImporter that silently became a hole: one stale SnorkleZombie duplicate survived inside
    `zombie/_needs-review.json` while the tool reported "840 indexed - clean". A gate with an
    exclusion rule has a blind spot the size of that rule, so this metric is defined by NOT
    having one. A queue nobody counts is a hiding place, not a queue.
    """

    id = "PassiveTree/HiddenFileCount"
    family = "PassiveTree"
    loop = Loop.CLOSED
    gates = False
    needs = frozenset({"tree_seed_roots"})
    covers: "tuple[str, ...]" = ()

    def run(self, ctx: Ctx) -> "list[Finding]":
        findings: "list[Finding]" = []
        visited = 0
        for root in ctx.tree_seed_roots:
            for path in sorted(root.rglob("_*.json")):
                if path.name == "_index.json":
                    continue
                visited += 1
                count = len(_entries(path))
                if count == 0:
                    continue
                findings.append(Finding(
                    metric=self.id, severity=Severity.GAP, subject=str(path),
                    message=f"{count} entr{'y' if count == 1 else 'ies'} parked in a `_`-prefixed "
                            f"file - invisible to every tool that skips them",
                    evidence={"path": str(path), "entryCount": count},
                    remedy="tree-review: adjudicate and move, or delete; never leave it parked"))
        # A green with visitedFileCount == 0 means the walk found nothing to look at - a different
        # thing from "the files are empty", and the one this metric exists to distinguish.
        findings.append(Finding(
            metric=self.id, severity=Severity.NOTE, subject="(corpus)",
            message=f"walked {visited} `_`-prefixed file(s) across "
                    f"{len(ctx.tree_seed_roots)} seed root(s)",
            evidence={"visitedFileCount": visited,
                      "rootCount": len(ctx.tree_seed_roots)}))
        return findings
```

## Testing strategy

| Test | Asserts |
|---|---|
| `a_parked_entry_in_an_underscore_file_is_a_finding` | §7, on a fixture that reproduces the real `_needs-review.json` |
| `an_empty_underscore_file_is_not_a_finding` | notes and exemplars stay legal |
| `the_sample_is_reproducible_from_metric_id_and_revision` | the same draw twice; `sampling/__init__.py:9-11`'s promise |
| `every_non_empty_stratum_gets_at_least_one_sample` | mechanically, over a skewed fixture |
| `a_rare_quota_cell_appears_in_the_tier_3_sample` | the tier that catches a broken cell |
| `sixty_clean_trees_report_the_four_point_nine_percent_bound` | the Clopper–Pearson number, computed not tabled |
| `three_rejects_in_sixty_is_a_batch_reject` | §6.3's ladder |
| `a_held_partition_alone_denies_a_pass` | `verdict.py:83-96`'s discipline, adopted |
| `a_gate_that_did_not_run_is_never_a_pass` | `FAIL` beats `NOT_MEASURED` |
| `an_open_loop_metric_cannot_be_registered_with_gates_true` | mechanically, over the registry |
| `every_acceptance_number_lives_in_tuning` | P2, mechanically — no threshold in code |
| `a_magnitude_retune_produces_an_empty_review_diff` | §8's headline property |
| `a_renamed_node_id_produces_a_full_tree_diff` | the id-stability dependency, proven not assumed |
| `the_card_renders_every_effect_through_formatMagnitude` | no raw channel id reaches the card |
| `the_card_fits_a_fixed_two_by_ten_lattice_for_every_tree` | one card shape, all 879 |
| `the_sibling_panel_names_three_distinct_trees` | never the tree itself, never a duplicate |
| `a_verdict_writes_a_machine_readable_row` | rule 6 — a review that produces no artifact did not happen |
| `hidden_file_count_reports_the_number_of_files_it_visited` | §7 — a green with `visitedFileCount` 0 is a walk that looked at nothing |
| `a_canary_parked_entry_is_a_finding_in_the_same_run` | the fixture root carries one parked entry every run, so "green" can never mean "the walk never ran" |
| `a_census_refuses_to_start_without_a_matching_sheet_read_row` | §5.5 — missing row, and a row naming a stale `sheetRevision` |
| `an_exclusion_printed_on_one_side_only_denies_the_lot_a_pass` | §6.4 rule 2 (a), D40 |
| `an_exclusion_naming_two_different_winners_denies_the_lot_a_pass` | §6.4 rule 2 (b) — the failure that makes an exclusion read as a bug |
| `a_loser_marked_unlocked_rather_than_inert_denies_the_lot_a_pass` | §6.4 rule 2 (c) — the catalog must let `tree-surface` render a trait the player still owns |
| `a_well_presented_nullification_ships` | D40, stated as a test so the withdrawn rule cannot creep back: a nullification that satisfies (a)–(c) is censused, reported and **passes** |
| `the_deep_mechanism_value_metric_never_gates` | §4.2 — a below-threshold behavioural sample files a finding; only the ladder stops a lot |
| `the_shared_corpus_lot_requires_a_completed_census` | closed question 4 — 39 trees, same protocol, its own sheet and verdict queue |

Fixtures are synthetic lots with a deliberately injected defect — a duplicated name across 300 trees,
an empty tier, an exclusion pair whose two sides name different winners, a parked `_` entry.
**That is the only way to prove a check would notice.**

## Boundaries

**Always:** state the population, the claim and the confidence together; draw samples through
`sampling.stratified_sample`; keep every acceptance number in `data/tuning/`; render effects through
`formatMagnitude`; count `_`-prefixed files **and report how many were visited**; write a `sheetRead`
row before a census starts; write every verdict as data; name the rule when rejecting; census the
exclusion nodes — **all three forms, `nullification` included** — and enforce their presentation
contract (§6.4 rule 2); census the shared corpus as its own lot, exactly as a species lot.

**Ask first:** changing an acceptance number (it is a judgement about how much risk ships, and the
whole point is that it is explicit); adding a second reviewer for an agreement pass; skipping the
census for a lot; committing per-tree cards after all.

**Never:** let an OPEN-loop metric contribute to a pass; enforce a narrowing of a locked decision
the owner has not ruled on (§6.4 rule 2's note records what that cost once, and what the owner
actually ruled); call a lot reviewed when a census population is unread;
report a sampled result as a claim about a named tree; write *"the catalog was reviewed"*
without the qualification in §2; repair a rejected node by hand without a `manualCorrection` stamp;
build an interactive tree editor; add a second implementation of the magnitude contract; sample a
property the machine already closes completely.

**Dependencies this module names but does not own:**

- **`provenance-supersede`** — seedsmith core backlog, unbuilt. **Pass two cannot run without it**
  (§8). A wiring gap, not a wall.
- **Node-id stability** — owned by [`tree-catalog`](spec-tree-catalog.md) §3. If that scheme changes,
  every number in §8 changes with it.
- **The import-boundary rejection** (`tree-catalog` R5) — without it a retired node bricks an actor
  load rather than showing red.

## Success criteria

- [ ] A reviewer judges a real tree card in ≤ 90 s, **measured on a 20-tree pilot** before the full
      run is authorized — the same discipline that smoke-tested 8 species before spending 2,584 calls.
- [ ] Every claim in the acceptance record names its population and its confidence, and the
      unqualified sentence *"the catalog was reviewed"* appears nowhere.
- [ ] Every tier-1 census population is read to completion, and its size is reported even when zero.
- [ ] `PassiveTree/HiddenFileCount` is green over the real seed roots **and reports a non-zero
      `visitedFileCount`**, and the same run finds the one parked entry in the canary fixture root —
      so a green can never mean the walk looked at nothing. (The canary lives in the test fixtures,
      never under a seed root; §6.4 rule 8 stands unchanged for the real corpus.)
- [ ] A magnitude retune produces an empty human review queue, proven by test.
- [ ] Every acceptance number resolves from `data/tuning/passive-tree-targets.v1.json`;
      `missing_thresholds` is empty before a run starts.
- [ ] No census starts without a `sheetRead` row for that lot naming the sheet's current
      `sheetRevision` — `{lot, sheetRevision, by, utc}`, written when the sheet is dismissed (§5.5).
      A census run against a missing or stale row refuses, proven by test.
- [ ] The card renders no raw channel id anywhere, proven by test.
- [ ] Every exclusion in the lot is censused and passes the presentation contract — both sides print
      the rule, both name the same winner, and the loser is marked **inert** rather than un-unlocked
      (§6.4 rule 2, D40). A `nullification` that satisfies it ships; one that does not denies the lot
      a pass.
- [ ] The 39 shared trees complete a census as their own lot, with their own corpus sheet and
      `sheetRead` row — about **58 minutes** at 90 s a card (closed question 4).

## Open questions

**Three**, all genuine. A recommendation nobody has disputed is a decision, and an answerable question
is a task — neither is listed here. The fourth was answerable and is answered below.

1. **What is the real per-tree review rate?** Every hour figure above rests on 60–90 s and it is
   **unmeasured**. A 20-tree pilot answers it in half an hour and should gate the full run. The pilot
   also yields the first estimate of the intra-tree defect correlation §3.3 needs.
2. **Is a two-reviewer agreement pass wanted on any tier?** The seeded sample is reproducible by
   construction, so inter-reviewer agreement is measurable at **no extra sampling cost**. Whether it is
   worth a second person's time is not this document's call.
3. **What manual-correction rate is acceptable?** §6.1 makes hand correction legal and stamped; above
   some rate it means the prompt is wrong. The demon corpus's rate was 3 entries in 840 — but that was
   a floor set by how little was reviewed, not a ceiling set by quality.
### Closed 2026-09-05

4. ~~**Does the shared corpus get a census too, or only the species lots?**~~ **Closed: yes, it gets a
   census.** 39 trees × 90 s is **58 minutes** — and this spec was already pricing it, since §1.3's
   census line is *879* cards, not 840. The argument settles itself on cost: **an hour is cheaper
   than the argument about whether an hour is worth it.** What that hour buys is not marginal either
   — the shared corpus is the whole learnable vocabulary of the game (`tree-surface` §1: 1,560 nodes,
   4.4% of the corpus, and the only part a build guide can be written about), so a defect there is
   read by every player rather than by the owners of one bloodline.

   **Specified, so it is not re-derived:** the 39 shared trees are **one census lot**, run under the
   same protocol as a species lot — its own corpus sheet, its own `sheetRead` row, its own verdict
   queue, every card judged, and the acceptance record saying *"every tree was judged."* It is a
   separate lot from the species corpus because the two land at different times and a lot is the unit
   that ships. Tiers 1–3 still run over it; the census does not replace them, and it is what makes
   the claim about the shared corpus a **C**-type claim (§2) rather than a **B**-type one.

   ⚠ Under D37 the shared corpus arrives in **category waves** as `gate-counters` lands each gate
   quantity, so in practice this is up to three census lots — 12 primary trees now, 6 elemental and
   21 status as they become generable. The per-lot cost is proportional and the total is unchanged.

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §1, §2 — the catalog is reviewed before it is committed; review is a shipping requirement | **D24** |
| §1.1 — 879 trees × 40 nodes; species trees are the same shape as every other | **D10**, **D29**, **D30** as amended |
| §1.3, §5 — review cost scales with **trees**, which is what makes D30 affordable | **D30**, **D23** |
| §3.2 — the favour triple is the first stratum axis | **D17**, and §9's measured 166× skew |
| §3.2, §5.5 — the quota grid is checked against a declared target, not against the corpus | **D32** |
| §4.1 — budgets and per-tree equal value are machine-checked, so humans do not sample them | **D13**, **D15** |
| §3.2, §6.4 — the exclusion census over all three forms; `nullification` ships, and the **presentation contract is enforced** (both sides print, same winner named, loser marked inert) | **D14**, as settled by **D40** |
| §4.2 — the mechanism floor is proxied by a plan-side label, so deep tiers are sampled | **D13** as extended by ideal §3.5 |
| §5.2 rule 3 — effects render through the shipped magnitude contract | **D24**'s learnability criterion (ideal §10.2) |
| §5.2 rule 2 — a fixed 2 × 10 lattice, one card shape for every tree | **D10**, **D29** |
| §6.4 rule 9 — byte-identical regeneration, or *"identical for every player"* is unproven | **D24** |
| §8 — `O(diff)` re-review; a magnitude retune costs zero human review | **D24** (ideal §10.2's *"node id stability becomes load-bearing"*) |
| §8 — a retired node is displayed, never silently repaired; the escape hatch is a full respec | **D11**, **D18** |
| §6.2 rung 3 — a systematically weak cell is fixed **in the plan**, not per node | **D13** |
| Boundaries — no exclusion budget where the mechanism is a real gap | **D16** |
| §1.1, §7, closed question 4 — the roster ships whole, and a category lands as its own lot: the shared corpus is censused in category waves as `gate-counters` unblocks each one | **D27**, **D9**, **D37** |
| §4.2 — the deep-tier behavioural sample **reports**; a systemic result escalates through the ladder rather than denying a lot a pass | closes [`tree-plan`](spec-tree-plan.md)'s open question 2 |

**Belongs to a sibling module, not here:** D1–D8, D12, D19–D22, D25, D26, D28, D31, D33–D36 —
`tree-plan` (the ladder, the archetypes, the property vocabulary, the targets), `tree-state` (the
per-actor store, unlock cost, respec), `tree-resolve` (every `P(Θ)` multiply, `F`, cross-unlock),
`squad-harness` (D33), `species-tree` (the pipeline this module reviews the output of). **D19, D20 and
D31 are superseded** — by D35, D26 and D35 — and are implemented nowhere, by design.
