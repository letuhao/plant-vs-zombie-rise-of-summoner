# Ideal: seedsmith's **second feature** — demons

**Phase:** idea. **This document is where this phase stops** — no specs, no plan, no code.
**Status:** exploration, 2026-08-31. Nothing here is approved or authorized to build.

**Program: `seedsmith`. Feature: `demons`.** This is not a new program and not a demon-program
module — it is the **second feature** seedsmith was built to accept.

> [seedsmith-map.md:7](seedsmith-map.md) — *"Items are the first feature; the core is
> feature-agnostic by construction, **because the second feature must not rewrite it**."*

⛔ **A framing error, corrected 2026-08-31.** The first draft of this document proposed a new
demon-program module called `demon-forge` that would *consume* seedsmith's machinery. That was
wrong, and the owner corrected it: the request was *"demon seed **in the seedsmith**"* from the
start. The distinction is not cosmetic — as a seedsmith feature this is an **adapter** implementing
an existing five-method Protocol, and the entire planner/briefkit/pipeline stack applies to it
**unchanged**. As a separate program it would have re-derived all of that. The wrong framing would
have cost a rewrite of work that already exists and passes 299 tests.

**Naming is therefore mostly settled by the seam**, not chosen: the feature lives at
`tools/seedsmith/seedsmith/adapters/demons/`, beside `adapters/items/` and `adapters/_stub/`. Two
words remain unavailable for anything *inside* it:

| Unavailable | Because |
|---|---|
| `seed` (as "creative brief") | `almanac_seed` is an existing specced table — the *factual* per-type capture (name, flavor, parsed cost/cooldown, observed hp/attack/armor + confidence flags). A second "seed" meaning "creative brief" collides with a table meaning "measured fact" ([spec-almanac-seed.md](almanac/spec-almanac-seed.md)) |
| `codex` | Already the **player-facing discovery UI** — [demon-system-map.md:25](demon-system-map.md) *"Codex = almanac FE with discovery states"*, shipped by `demon-domain-fe` with a `rpg_demon_codex` table carrying `seen`/`discovered` |

---

## 0. The principles this is built on, restated inline

A downstream session reads this document, not its links. So:

**Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The PvZ game
owns the board, vanilla damage, spawn/die and the sun bank. We **observe** its events and contribute
**signed deltas** back. We never rewrite it, never read its current state, and never make a feature
depend on it representing a concept. Nothing in this document asks PvZ to know what a demon is.

**Standalone-first.** Every RPG feature must be playable with the game closed. The injector may
*enrich* a feature, never *gate* one. This program's output is committed source, so a fresh install
needs no game data at all.

**One power ladder.** Contests read `Θ` (linear, difference-based); magnitudes read
`P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`. No subsystem owns a private `f(level)`. **A generated artifact may
never contain a magnitude a model chose** — magnitudes come from the ladder, and a model-invented
number looks plausible while being anchored to nothing.

**Magnitudes are `long`**, never `float` (integer-exact fails at `Θ`=232, inside normal play).
**The balance surface is data** — a number a balance pass would change lives in
`data/tuning/<domain>.v{n}.json`, not in code. **No hard progression ceilings.**

**SQL only inside `FusionRpg.Data`.** **Deltas, never absolutes.** **Single writer** —
`EntityStatWriter`; the Funnel is the only Secondary → Bag path.

**Determinism is the product.** Every generator in this repo that already works is seeded from
content, never from a clock or `Random`.

---

## 1. What the owner asked for

> *"build new pipeline that agents will read almanac make see for demon, we will generate item
> specific, action specific, aspect specific, commander specific and something like environment
> specific for each demon"*

Read plainly: agents read the almanac, produce a per-demon record, and five generators consume that
record to produce content that is **specific to that demon** — items, actions, aspects, commander
effects, environment affinities.

---

## 2. Findings — built / wiring gap / real gap

### BUILT

**B1 — The deterministic generation pattern already exists and works.**
[`DemonSpeciesGenerator.cs`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs)
generates the whole species roster from captured game data. Its discipline is exactly what this
program needs and must not re-invent:

- **FNV-1a over `(typeId, salt)`** is the randomness source —
  [`DemonSpeciesGenerator.cs:173-189`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs),
  whose own comment reads *"never wall clock, never Random"*. Different salts (`"element"`, `"sec"`,
  `"t1"`, `"variant"`, `"essence"`) give independent draws from one stable input.
- **The output is committed C#** — `EmitCSharp` at
  [`:192`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs) writes
  `DemonSpeciesCatalog.Generated.cs`, header *"Do not hand-edit — rebalance via the generator, then
  re-emit."* This is what makes gameless-first hold.
- **Derived today:** element primary/secondary, rarity from observed-HP rank
  ([`:75-82`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs)), deploy mode,
  variants, trait pool, acquisition flags.
- **24 species shipped** in `DemonSpeciesCatalog.Generated.cs`.

**B2 — The factual input table is specced.** [`almanac_seed`](almanac/spec-almanac-seed.md) turns the
raw capture streams into one typed per-type row: `display_name`, `flavor_info`, `flavor_introduce`,
`sun_cost`, `cooldown_sec`, `hp`, `attack`, `armor` — **with explicit confidence flags**
(`cost_status` ∈ `absent|parsed|unparsed`, and null-if-unobserved). Its own objective names this
program's consumer: *"This is the generator the Demon program's species catalog already commits to
needing."*

**B3 — Honest coverage numbers already measured** (2026-08-23, in that spec): only **89 of 677**
plant almanac entries (13%) carry a `cost` at all; only **66/677 plants and 18/227 zombies** have a
`spawn_stats` sample before coverage runs. Any design that assumes rich text for every demon is
already contradicted by measurement.

**B5 — The fusion graph is captured, persisted and queryable — but it is *not* a taxonomy.**
PvZ Fusion's `PlantMixTreeManager.ChildToParents` lands in a real `recipes` table
(`parent_a, parent_b, result` — [`RpgStore.cs:174`](../../src/FusionRpg.Data/Sqlite/RpgStore.cs),
insert at `:2557`, public `ListRecipes()` at `:2582`). The capture works: the old
zero-entries bug (`EnqueueRecipes()` auto-latching `_recipesDumped` at injector boot, before any
board existed) is **fixed**, live evidence 2026-08-23 ([almanac-todo.md](../../tasks/almanac-todo.md)
T2/T3).

⛔ **Corrected 2026-08-31, owner: this is a crafting graph, not a family tree.** A first draft of
this document proposed deriving `familyId` from it. That is wrong — *"A + B = C"* says nothing about
whether A and B are **kin**. Wall-nut and tall-nut are family; neither is the other's fusion parent.
Fusion lineage and taxonomy are different relations that happen to both be graphs, and conflating
them would have produced families that are really just "things that combine", silently.

**What `recipes` is genuinely good for:** fusion lineage — which the shipped `demon-fusion` module
already trades in (star merges, discoverable recipes, trait inheritance). Keep the source; drop the
claim.

**B4 — A proven content-generation stack exists in `tools/seedsmith`** (299 tests, CP-G reached
2026-08-31): feasibility refusal, derived ordering, an exemplar gate, work-order scheduling, the
declare/fulfil demand split, content-addressed briefs, schema-guardrailed pipelines with provenance
and an open-loop review queue. **This is not a neighbouring tool to borrow from — it is the same program.** The demons feature adds an adapter; it adds no planner, no briefkit, no pipeline.

### WIRING GAP

**W1 — The generator is specced to read lore and portraits, and reads neither.**
[spec-demon-core.md:17](demons/spec-demon-core.md) commits the tool to read *"`types` catalog for
species + names, **`type_almanac_dump` for lore text**, `type_icons` for portraits, `spawn_stats`
baselines for power tiering."*

The shipped input record is
[`CapturedTypeSeed(Side, TypeId, TypeName, DisplayName, HpBase)`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs)
— **no lore text, no icon, no attack, no armor, no cost.** `Name` is the raw captured display string
([`:53`](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs)).

This is a **wiring gap, not an architectural limit**: the data exists, the table that normalizes it
is specced, the consuming spec names it, and the record simply lacks the fields. Widening
`CapturedTypeSeed` is the single highest-leverage change this program could make.

**W2 — `aspect-scope` is specified and unbuilt.**
[spec-aspect-scope.md](demons/spec-aspect-scope.md) — *"Status: proposed 2026-08-26, awaiting owner
review. **Not authorized to build.**"* It moves `ElementPrimary`/`ElementSecondary`/`TraitPool` off
the species down one tier so **one species yields N aspects**, *"generated, never authored"*, and it
carries a **byte-identical migration path** (§3.1: seed the element salt so each species reproduces
today's trait pool). The owner's own words in it: *"one plant type maybe have many element type… not
only element types, maybe affect trait / initial skills or something? strong and weakness?"*

**That is the "aspect specific" half of this request, already designed.** It is blocked on a review,
not on a discovery.

### REAL GAP

**R1 — There is no creative kernel anywhere, for anything.** Nothing in the repo carries a demon's
*concept*, its motifs, its tone, or what it is explicitly **not**. `DemonSpeciesDef` is entirely
mechanical. Without this, five generators asked for "an item for `dollgold`" produce five unrelated
things that share only an id.

**R2 — Nothing declares what each generator is entitled to produce.** seedsmith's declare/fulfil
split exists for the item corpus; there is no per-demon equivalent saying *this demon gets 2 items in
these roles, 1 action in this trigger phase, this aspect count, this commander effect, these sector
affinities.*

**R4 — Family does not exist anywhere, and must be *classified from natural language*.**
Owner, 2026-08-31: *"family don't exist, we only crawling it from almanac natural language text,
need a classify llm pipeline."* There is no captured field, no enum, and no derivation — the only
signal is `almanac_seed.flavor_info`/`flavor_introduce` and the display names themselves.

This makes family the feature's **first pipeline**, and it is a *different shape* from anything
seedsmith has built: every pipeline shipped so far **generates** content, whereas this one
**assigns a label**. Three consequences fall straight out of that difference — see §3.5.

**R3 — "Environment specific" has no home yet.** The world map has sectors, slots, lanes and biome-ish
types, and `spec-instance-and-binding.md` added `sector:{id}` and `slot:{id}` owner scopes for
exactly this class of thing — but no demon→environment relationship is designed anywhere.

---

## 3. The shape this suggests

### 3.0 It is an adapter, and that decides most of the architecture

`SeedAdapter` is a five-method Protocol
([`adapters/base.py`](../../tools/seedsmith/seedsmith/adapters/base.py)): `kinds()`, `dimensions()`,
`legal_combinations()`, `registries()`, `channels()`. A demons adapter implements those five and
**inherits every module built for items**, because none of them knows what an item is:

| The owner's five "generators" | Where it lands | Why |
|---|---|---|
| item-specific | **the items corpus, as a demon *theme*** | `set` already *requires* `themeKey`; 31 sets are already themed — audit **A3** |
| action-specific | the action corpus, referencing the demon | same reasoning as items |
| aspect-specific | a `KindSpec` in the demons adapter | genuinely demon-shaped |
| commander-specific | a `KindSpec` in the demons adapter | genuinely demon-shaped |
| environment-specific | a `KindSpec` in the demons adapter | genuinely demon-shaped (⚠️ audit **A7** — no consumer in v1) |

⛔ **Corrected by audit A3.** The first version of this table put all five in the demons adapter.
`Corpus.load` is **single-root**, so demon "items" would have been a *different thing* from real
items — unequippable, and outside the item corpus's own rules. **The demon is a theme**; items and
actions stay in their own corpora and reference it. What remains in the demons adapter is what is
genuinely demon-shaped, and everything below still applies to those kinds.

Consequences worth stating plainly, because they shrink the work dramatically:

- **"What each generator is entitled to produce" is already built.** That is `declare`/`fulfil`
  (P5) — a demon's needs become `NeedSpec`s, and reuse-before-generate is the default.
- **Generation order is already derived, never labelled.** `reference_fields` on each `KindSpec`
  produce the kind graph; Kahn layers it; Tarjan names any cycle. If an action references an aspect,
  aspects generate first — structurally, with nothing to remember.
- **The briefs, the "never a number" audit, provenance, idempotence and the open-loop review queue
  all already exist** and are feature-agnostic.
- **`_stub` proves the seam continuously.** Its docstring: *"If the core ever reaches into item
  concepts, this stops passing — a cheap, loud, continuous proof that the feature seam is real."*
  A demons adapter is the first real test of that claim.

What is genuinely **new** is therefore small: the adapter itself, and the creative kernel (R1) that
no adapter interface has an opinion about.

### 3.1 The load-bearing idea: motifs are the coherence mechanism

> ⛔ **Qualified by audit A1 (§6):** shared vocabulary alone yields *repetition*, not coherence.
> Motifs need **per-kind expression rules** — a motif is a material to an item, a tempo to an
> action, a terrain to an environment. Read A1 before treating this section as sufficient.

The single design decision that makes this work is small: **the record carries 3–5 motifs, and every
generator draws its vocabulary from that same list.**

Because the item, action, aspect, commander and environment generators all read the same motifs, the
sword, the skill, the aura and the biome sound like **one demon** — *without any generator knowing
the others exist*. No cross-generator coordination, no shared context window, no ordering constraint
between them. Coherence becomes a property of the input rather than a thing to enforce afterwards.

The mirror of it matters as much: **anti-motifs** — what this demon is explicitly not. Prior art and
this repo's own history agree that drift is the failure mode; a generator told only what a thing *is*
will happily add what it isn't.

### 3.2 The fields, their openness, and what consumes each

**The governing rule: the record adds what `DemonSpeciesDef` lacks; it never restates it.** Element,
rarity, deploy mode and trait pool already live in the catalog. Duplicating them creates two sources
of truth that will eventually disagree, and the copy nobody updates is the one that decides.

**Openness is three states here, not two** — copied from this repo's own registry precedent
(`data/seed/items/_registry/core.v1.json`), because the middle state is the one that matters:

| State | Marker in the item registry | Meaning |
|---|---|---|
| **Frozen** | `"frozen": true` | Never changes. The element ring; effect-atom's 12 kinds / 7 triggers. |
| **Append-only** | `"appendOnly": "roleId values and their list position are append-only"` | New values may be appended; existing values **and their positions** never move. |
| **Open** | (no marker) | Free text. |

Append-only exists because **position is load-bearing**: a list index feeds derived ids and content
hashes, so reordering silently moves content that was already generated. Most demon vocabularies want
this state — the roster grows, and it must never renumber.

#### Anchor — derived from capture, never authored

| Field | State | Source | Consumed by |
|---|---|---|---|
| `speciesId` | append-only | `DemonSpeciesCatalog` | join key for everything |
| `gameTypeId`, `side` | derived | `types` | links the demon to the body it wears |
| `displayName` | derived | `almanac_seed` | demon catalog, briefs |
| `flavorInfo` | **open** | `almanac_seed` | the raw material motifs are derived *from* |
| `hp` / `attack` / `armor`, `sunCost`, `cooldown` | derived | `almanac_seed` | power tiering — **read, never re-derived** |
| `coverage`, `basis` | closed enums | `almanac_seed` flags + the classifier's own basis | lets a generator **decline**, and tells re-derivation what to revisit |

`coverage` is the field most likely to be dropped as bookkeeping, and it is the one that keeps the
corpus honest: only **89 of 677** plants carry a cost at all (B3). Without an explicit
"we do not know this", a generator fills the gap with something plausible.

#### Taxonomy — the closed vocabularies

| Field | State | Why that state | Consumed by |
|---|---|---|---|
| `families[]` | append-only vocabulary, **multi-valued** | **LLM-extracted from name + description** (R4, §3.5) — not derived, not captured. A demon may belong to several | **partition key** (a demon appears in several partitions), motif inheritance, coverage metric |
| `motifs` (3–5) | append-only **vocabulary**, per-demon selection | vocabulary grows; existing entries must never renumber | all five generators — §3.1's coherence mechanism |
| `antiMotifs` | same vocabulary | what this demon is **not** | drift prevention |
| `tone` | frozen, small set | a growing tone list is a smell, not a feature | voice consistency across generators |
| `aspectCount` | closed, bounded int | `aspect-scope` needs a bound, not a number | `aspect-scope` (W2) |

**`familyId` earns its place twice**, which is why it is worth deriving rather than skipping:

It is also the feature's **riskiest** field, because it is the only one produced by judgement rather
than by capture or derivation — see §3.5. It still earns its place twice:

1. **It is the natural partition key** — ⛔ *but see audit A5 (§6): with `families[]` multi-valued,
   family coverage no longer implies per-demon coverage, and both metrics are needed.* `Coverage/EmptyPartition` and the stratified sampler then
   work on demons exactly as they do on items — and the sampler's guarantee ("every non-empty
   stratum gets at least one sample") becomes "no family goes ungenerated or unreviewed".
2. **It is a motif inheritance channel.** Everything in the wall-nut family shares *defensive,
   shell, endurance* without each demon restating it — which is the difference between a corpus that
   feels designed and one that feels enumerated.

#### Generation contract — closed, and already built

| Field | State | Consumed by |
|---|---|---|
| `demands[]` — kind, count, constraints | closed | the planner's declare/fulfil (P5); **reuse-before-generate is already the default** |
| `sectorAffinity[]` | append-only | the environment generator (R3) |

#### Provenance — derived

`contentHash`, `promptVersion`, `θAnchor`, `budgetVersion` — built in seedsmith G2, feature-agnostic.

#### What is actually open

Only `flavorInfo` (captured prose) and the generated content itself. **Everything else is closed,
append-only, or derived** — which is precisely what makes briefs inlinable (§3.3), the corpus
diffable, and content hashes stable across regenerations.

### 3.3 Two rules that transfer verbatim from seedsmith

**Inline literally, never cite.** *"Tags come from `tags.v1.json`"* cost **51 invented tags** — an
agent cannot follow a filename, so it fills the gap. Every closed vocabulary the record depends on is
written into it in full. seedsmith enforces this by grepping the rendered brief for citation-shaped
text and refusing on a match, rather than trusting a convention.

**Never a number.** A schema carrying a numeric magnitude field is rejected *mechanically* — magnitudes
come from `numerics`/`P(Θ)`. The record should carry **bands and shares**, never values.

### 3.4 What "confidence" forces

B3's measurement makes one thing unavoidable: **most demons will have thin or absent lore.** A design
that requires rich flavor per demon fails for 87% of plant types. The record must therefore carry
coverage explicitly and let a generator **decline** — which is exactly the `blocked` variant
seedsmith's pipeline already requires of every schema, precisely so a model with nothing to work from
says so instead of inventing.

---

### 3.5 The family classifier — the feature's first pipeline, and its riskiest

Family must be **classified from natural-language text** (R4). Every pipeline seedsmith has built so
far *generates* content; this one *assigns a label*, and the difference changes three things.

**1. The taxonomy does not exist yet either — so it is two stages, not one.**
You cannot classify into a vocabulary before the vocabulary exists. Stage A proposes the family set
from the corpus; stage B assigns each demon into it. Collapsing them into one call invites a model to
invent a fresh family per demon, which produces a taxonomy with no sharing — the exact failure §3.1
and Q7 exist to prevent, arriving one step earlier than expected.

Stage A's output is **an append-only vocabulary** (§3.2) and should be reviewed once by a human
before stage B ever runs. It is small — tens of families over hundreds of types — and it is the
single artifact everything downstream inherits.

**2. Thin source text is the danger, and B3 already measured how thin.** Most types have little or
no lore. A classifier handed a name and nothing else **will** confidently assign a family from the
name alone — and `wall-nut`/`tall-nut` sharing a suffix is a genuinely useful prior, right up until
it silently becomes the *only* evidence and the taxonomy is really just string matching wearing a
model's confidence.

So the output must distinguish **what it classified from**:

| `basis` | Meaning |
|---|---|
| `text` | assigned from real flavour/description content |
| `name` | assigned from the name pattern only — a prior, not evidence |
| `blocked` | neither was sufficient — the demon has no family yet |

`blocked` is not a failure (G1's rule): a demon with no usable text **must** be able to have no
family, because an invented family propagates into every generator that inherits motifs from it.
That is one wrong label becoming five wrong pieces of content.

**3. It is closed-loop, but only per-item.** "Is this demon in the wall-nut family" has a right
answer a human can check — so it gates, unlike flavour quality. But whether the *taxonomy itself* is
good has no machine answer, so **stage A is open-loop and stage B is closed-loop**. They are two
pipelines with two different verdict models, and giving stage A a pass/fail field would be exactly
the "mark its own homework" defect G3 refuses (`audit_open_loop_schema`).

**What this buys back:** once families exist, they are the partition key (§3.2), which means
`Coverage/EmptyPartition` and the stratified sampler start working on demons for free — including
the guarantee that no family goes unreviewed.

## 4. Prior art, with what it actually contributes

**Schema-governed LLM pipelines / world bibles.** Narrative entities are formalized into a *world
bible*; a unified schema specifies the formal structure; generation is **normalization-repaired** and
**engine-aligned** before admission. The contribution here is the admission step: generated content is
not trusted, it is *admitted* after validation — which is seedsmith's scratch → gate → move by another
name. ([Systems 14(2):175](https://doi.org/10.3390/systems14020175))

**Dependency-driven prompt pipelines.** Each generation stage is *"explicitly conditioned on the
structured outputs of all preceding stages"*, with structured JSON at each stage, to address
*"coherence, consistency, and controllability issues that frequently undermine naïve LLM-based
generation."* The contribution: staging is what buys coherence, and the conditioning must be on
**structured output**, not on prose. ([arXiv 2604.25482](https://arxiv.org/html/2604.25482v1))

**Dwarf Fortress raws.** A creature is a base definition plus **castes** (sub-species) plus **creature
variations** — *"a series of tokens that are added to or removed from the creature."* The
contribution: **reuse by transformation, not by copy.** An aspect is a token-delta on a species, which
is precisely `aspect-scope`'s design (W2) arrived at independently.
([Creature token](https://dwarffortresswiki.org/index.php/Creature_token) ·
[Entity token](https://dwarffortresswiki.org/index.php/Entity_token))

**Caves of Qud.** Identity is **layered** — histories, relationships with neighbours, cultures,
architecture — with *"specific tools and parameters interacting within and across categories to create
dynamic identities."* The contribution: an entity's identity is composed from layers that each mean
something on their own, which is why the anchor/kernel/constraint split above is three layers rather
than one blob.
([Game Developer](https://www.gamedeveloper.com/design/tapping-into-the-potential-of-procedural-generation-in-caves-of-qud))

**The documented failure mode all four share** is drift: naïve generation produces locally-plausible,
globally-incoherent content. Every mitigation above is structural — schema, staging, token-deltas,
layering — not "a better prompt."

---

## 5. Open questions — ALL CLOSED, 2026-08-31

Four closed by evidence earlier in the day; the remaining four answered by the owner. **Nothing in
this document is now waiting on a decision.**

### The thesis the owner's answers produce

Taken together, Q1 + Q6a + Q6b are not four separate rulings — they are one position, and it is
cleaner than the mixed authored/derived design this document originally proposed:

> **Derive everything. Track what the derivation was based on. Re-derive when the input improves.**

Nothing is hand-authored, so *"generated, never authored"* (B1) holds for the creative layer as well
as the mechanical one, and the roster stays reproducible at any size. The known weakness — thin
source text (B3) — is treated as a **temporary state of the input**, not a permanent property of the
design, because lore enrichment is a planned later pipeline (Q6b).

**This promotes provenance from bookkeeping to the load-bearing mechanism.** `promptVersion`,
`basis` and `coverage` are no longer audit fields; they are *how you know what to regenerate* after
enrichment lands. A corpus that cannot answer "which demons were classified from a name only, under
which prompt version" cannot be improved incrementally — it can only be rebuilt wholesale. G2's
idempotence and provenance were built for items; here they carry the feature's whole upgrade path.

### ✅ Q1 — motifs: **PURE DERIVATION, no human pass.** *(owner)*

No authoring, no edit pass. The generator derives motifs from captured text, and thin input produces
thin motifs **that are marked as such** rather than quietly padded.

**The honest cost, recorded rather than smoothed over:** for the majority of types the derivation
will be working from a name and little else (B3 — 87% of plants carry no cost field, most have thin
lore). First-pass motif quality will be visibly weak for those, and the weakness is *supposed* to be
visible — that is what `basis` and `coverage` are for. The fix is enrichment (Q6b), not a human pass.

### ✅ Q2 — `aspect-scope`: **APPROVED. Build all five kinds.** *(owner)*

The demons adapter ships with all five `KindSpec`s — item, action, **aspect**, commander,
environment. This is an approval of another program's spec
([demons/spec-aspect-scope.md](demons/spec-aspect-scope.md), previously *"proposed, not authorized to
build"*) and it unblocks that module in the demon program independently of this feature. It carries a
byte-identical migration path, so today's trait pools are reproducible after the move.

### ✅ Q6a — family: **PURE LLM EXTRACTION, and a demon may have MORE THAN ONE.** *(owner)*

> Owner: *"Pure llm extraction, base on name and description, a demon maybe have some family, need
> build a llm pipeline to do that, this is a part of seedsmith."*

Two consequences, the second of which changes a field:

**1. No hand-authored vocabulary.** Stage A is a pipeline, not a writing task. The two-stage shape
still earns its place — extract candidate labels per demon, **consolidate into a vocabulary**, then
assign — because collapsing extraction and assignment into one call is what produces a fresh family
per demon and a taxonomy with zero sharing (§3.1, Q7). Consolidation is the step that makes families
mean something; it is not the same step as extraction.

**2. `familyId` becomes `families[]` — multi-valued.** A demon belongs to zero, one or several
families. This is a real change to §3.2's field table and it propagates:

- **As a partition key**, a demon now appears in *several* partitions. That is fine for coverage and
  for stratified sampling — both are set operations — but it means partition counts no longer sum to
  the roster size, and any metric that assumes they do would be quietly wrong.
- **As a motif inheritance channel**, a demon inherits from every family it belongs to. Q7's
  demons-per-motif metric becomes more important, not less: multi-membership is exactly how a
  vocabulary drifts toward everything-is-related.

### ✅ Q6b — `basis = name`: **WORK REMAINING, and enrichment is a planned pipeline.** *(owner)*

> Owner: *"Option 1, we will come back with some llm lore enrichment later."*

A name-classified demon counts as **unfinished** in coverage — the same discipline
`almanac_seed.cost_status` already applies to parsed-vs-absent cost. The difference between *we know
this* and *we guessed from a string* stays visible in the metrics rather than being absorbed.

**Lore enrichment is therefore a named future pipeline, not a hope.** It is what turns `basis = name`
into `basis = text`, and it is the reason pure derivation (Q1) is a sound choice rather than a
resigned one. Two things follow that a spec must not lose:

- **Enrichment is open-loop.** "Is this generated lore any good" has no machine answer, so it samples
  for review and never marks its own homework (G3's `audit_open_loop_schema`).
- **Re-derivation must supersede, not duplicate.** After enrichment, motifs and families are derived
  again from better text. G2's ledger raises on a re-recorded row, so superseding needs to be a
  deliberate path rather than a second write — worth naming in the spec, because "regenerate after
  enrichment" is the feature's whole upgrade story and it currently collides with an idempotence
  guard built to refuse exactly that.

### ~~Q3, Q5, Q7~~ — closed earlier by evidence

Q3 environment affinity = flavour in v1, `sector:` scope reserved and already gated. Q5 corpus =
emitted JSON under `data/seed/demons/`. Q7 motif budget = a metric (demons-per-motif), not a cap.

### ~~Q4, Q6-original~~ — dissolved

Q4 answered by the framing (it is a seedsmith adapter). Q6-original assumed family came from the
fusion graph; it does not (B5, corrected).

---

## 6. Adversarial audit — 2026-08-31

A red-team pass over this document's own claims. Eight findings; **three change the design**, three
add a rule it was missing, two are risks to carry. Each is written attack-first, because a finding
stated as a conclusion is one nobody can check.

### ⛔ A1 — Shared motifs produce *repetition*, not coherence. **Changes the design.**

**The attack.** §3.1 claims that because five generators read the same 3–5 motifs, their output
sounds like one demon. Give five generators `shell, endurance, patience` and they produce *Shell of
Patience*, *Enduring Shell*, *Patient Shell*, *Shellfield*. That is not coherence — it is a
thesaurus. And **no metric catches it**: every schema validates, every gate passes, coverage is
complete, and the corpus is unreadable.

**Why it lands.** Motifs are shared *vocabulary*. Coherence needs that vocabulary expressed
*differently per kind* — an item expresses a motif as material and form, an action as tempo and
effect shape, an environment as terrain, a commander effect as doctrine. The design supplied the noun
and forgot that each kind is a different part of speech.

**The fix: per-kind expression rules**, carried on each `KindSpec`. Cheap to state, and it is the
difference between five variations on a word and five facets of one idea.

### ⛔ A2 — With thin text, motifs and families are the same signal counted twice. **The sharpest finding.**

**The attack.** Q1 chose pure derivation; Q6a chose LLM extraction from **name + description**. B3
measured that most types have little description. So for the majority, *both* motifs and families
derive from **the same string — the name**. Every "-nut" demon then gets nut-ish motifs *and* the nut
family, and family-based motif inheritance (§3.2) appears to work beautifully.

It is a tautology. The inheritance is not structure discovered in the content; it is one token read
twice. **And every metric reports success** — demons-per-motif looks high (real sharing!), families
look populated, coverage looks complete.

**The fix: measure agreement only where it carries information.** `basis` already separates `text`
from `name` (Q6b). Extend it: where a demon's motifs **and** families are both `basis = name`, that
demon's family-motif agreement is excluded from the sharing metric rather than counted as evidence
for it. Enrichment (Q6b) is what eventually breaks the tie — another reason it is a named pipeline
rather than a hope.

### ⛔ A3 — "Item specific" does not belong in the demons corpus. **Changes the architecture, for the better.**

**The attack.** §3.0 listed five kinds including `item`, all in the demons adapter. But
`Corpus.load(root)` is **single-root** — one corpus, one directory — and items live in
`data/seed/items/`. So either demon items are a *different thing* from real items (a player cannot
equip them; the feature is cosmetic), or the demons feature writes into another adapter's corpus (and
§3.0's clean adapter story is false).

**Why it lands, and the way out.** The item corpus **already has a theme axis**: `unique` carries
optional `theme`/`themeKey` and **`set` *requires* `themeKey`**
([`adapters/items/kinds.py:60,63`](../../tools/seedsmith/seedsmith/adapters/items/kinds.py)).
Measured in the live corpus: **31 sets and 8 uniques already carry a theme.**

**So a demon is a theme.** Items stay in the items corpus and reference the demon; the demons feature
contributes themes, not items. Strictly better than the original framing:

- No cross-corpus write; `Corpus.load`'s single-root constraint is respected rather than fought.
- A demon's signature gear is naturally a **set** — already theme-required, already has members and
  thresholds, and there are 31 existing instances to pattern-match against.
- The items adapter's own rules (roles, frames, affix families, budgets) keep applying, because the
  items are real items.

### ✅ A4 — `numerics` does not apply to demons, and that is load-bearing.

`adapter.channels()` is consumed by exactly one subsystem — `numerics`
([`numerics/model.py:68`](../../tools/seedsmith/seedsmith/numerics/model.py)). A demons adapter has no
magnitudes to resolve, so it returns an empty channel list and that subsystem is inert here. **That is
correct, not a gap** — it makes §3.3's "never a number" structural rather than a guardrail, because
there is no numeric path to misuse. Stated so nobody later "fixes" the empty list.

### ⛔ A5 — Multi-valued families weaken the coverage guarantee more than §3.2 claims.

**The attack.** Q6a made `families[]` multi-valued, but §3.2 still claims families-as-partition-key
buys "no family goes ungenerated". If a demon belongs to three families, **a handful of multi-family
demons can satisfy every partition** while most of the roster gets no content — and coverage reports
green.

**The fix.** Family coverage answers *"is every family represented"* — a real question, but a
different one from *"does every demon have content"*. Both are needed, and the second is the one a
player would notice missing.

### ⛔ A6 — Consolidation is the hard step, and §3.5 gave it one sentence.

**The attack.** Merging `nut`, `wall-nut`, `defensive-nut`, `shell-type` into one family is
**clustering**, and it is where the taxonomy is actually decided. An LLM doing it fresh each run
returns a different taxonomy each run — breaking determinism, this repo's stated product, in the one
artifact everything else inherits from.

**The fix.** Split the stage honestly: **extraction is LLM** (non-deterministic, therefore recorded
and content-addressed like any other generation); **consolidation is deterministic over the recorded
extractions** — or, if it is itself a model call, its output is committed and re-run only
deliberately, exactly as `DemonSpeciesCatalog.Generated.cs` already is. Either is fine. Silently
re-running a non-deterministic consolidation is not.

### ⚠️ A7 — The environment kind has no consumer in v1. **Risk carried deliberately.**

Q3 settled that `sector:` bindings are rejected without a world host, so environment content in v1 is
flavour **nothing reads**. Generating ahead of a consumer is legitimate — the content is cheap and
the world map is coming — but it should be explicit, because coverage will report those partitions
"covered" and the feature will look more finished than it is.

### ⚠️ A8 — "Regenerate after enrichment" collides with a guard built to refuse it.

Repeated from §5 Q6b because it is the audit's only *code-level* collision:
`ProvenanceLedger.record` **raises** on a re-recorded row, deliberately, because a second write is how
idempotence fails. Enrichment's whole purpose is to produce a second, better write. Superseding must
be a deliberate path carrying its own provenance link — not a bypass of the guard, and not a silent
overwrite.

### What the audit did not find

No contradiction with a §0 invariant. No PvZ-layer dependency — nothing here asks the game to know
what a demon is. No new power-shaped scale (A4). No SQL outside `FusionRpg.Data` (Q5 keeps the corpus
as emitted files).

---

## 7. Design-gate checklist ([DESIGN-GATE.md](DESIGN-GATE.md) §5)

```
[x] I identified the subsystem(s) this touches.
    — demons, almanac, data/SQL, power/scaling, tunables, the atom layer, seedsmith.
[x] I read every doc in the §1 row(s) for those subsystems, this session.
    — demon-system-map.md, demons/spec-demon-core.md, demons/spec-aspect-scope.md (§1),
      almanac/spec-almanac-seed.md, almanac-map.md, effect-atom/spec-instance-and-binding.md,
      overlay-control-loops.md, event-pipeline-v2-ssot.md, tunables-ssot.md (via CLAUDE.md's
      standard), DESIGN-GATE.md §1-§3/§5, seedsmith spec-planner.md §5-§8.
[x] I checked decisions.md for a lock covering this.
    — decisions.md:90 locks the species catalog as "generated deterministically from captured game
      data (types/almanac/icons/spawn_stats)". This document does not contradict it; W1 reports that
      the shipped generator reads a subset of those four sources.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments.
    — CapturedTypeSeed's field list, the FNV-1a salt discipline, the rarity-from-HP-rank rule and
      EmitCSharp were read in DemonSpeciesGenerator.cs, not inferred from the spec.
[x] I read the surrounding section of every rule I quoted.
[x] I tested (not assumed) any constraint I am reporting.
    — the two name collisions were grepped, not assumed: `almanac_seed` is a real specced table and
      `codex` is a real shipped table + FE module. `demon-forge` was grep-checked for collision.
[x] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
    — §0 restates the ones that bind; §3.3's "never a number" exists to keep invariant 14 (one power
      ladder) true through a generative path.
[ ] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks.
    — **Not yet, deliberately.** This is an idea doc; nothing is approved. `demon-system-map.md`
      gains no module row until the owner accepts a program here. **W1 is the exception worth
      flagging now**: the gap between spec-demon-core.md:17's four named sources and
      CapturedTypeSeed's actual fields is a real inconsistency in a shipped module, and it is worth
      recording against `demon-core` whether or not this program ever exists.
```
