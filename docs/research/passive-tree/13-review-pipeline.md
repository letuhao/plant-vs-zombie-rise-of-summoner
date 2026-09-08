# Passive trees — the review pipeline for a 24,389-node species catalog (2026-09-05)

**Status:** research note. **Not a spec. No build authorized.** Designs the *review* half of the
pipeline [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) D23 reserves for species
trees, against D30 (*"every species gets a FULL 29-node unique tree"*) and D24 (*"the tree CATALOG is
STATIC, SHARED and IDENTICAL for every player"* — reviewed, then committed).

Builds on [03-llm-stage-contract.md](03-llm-stage-contract.md), which already specifies the
generation contract, the quota algorithm and 29 validation gates. **Nothing here re-derives those.**
This note answers the question 03 left open: *29 machine gates pass — now what does a human read?*

Claims are marked **FACT** (counted or quoted from code/data this session, with `file:line`),
**INFERENCE** (reasoned from a fact, reasoning shown), or **RECALL** (from a document, not
re-verified).

---

## 0. The answer, up front

### The three numbers

| | |
|---|---|
| **Full read is not an option** | 25,949 nodes × 30 s (the repo's own assumed rate, `06-red-team.md:298`) = **216 hours**. At a realistic judgement rate of 60 s it is **432 hours** |
| **The sample, if the unit is the node** | **384 nodes** for ±5% at 95% (378 with the finite-population correction). It buys a corpus-level defect *rate* and nothing else — 0.45 nodes per species tree, so 99% of trees are never seen |
| **The sample, if the unit is the TREE** | **60 trees read whole**, zero rejects ⇒ *"no more than 4.9% of trees carry a reviewer-rejectable defect"* at 95% (Clopper–Pearson, computed this session). At a 90-second tree card that is **90 minutes** |

### The review artifact, in two sentences

**One HTML card per tree, generated from the committed plan + concrete catalog, showing the whole
tree as a 2 × 10 lattice of `name + one-line rendered effect` beside the species' own almanac
`reason` sentence — so the reviewer's only question, *"do these nodes belong to that creature,"* is
answerable in one look rather than by reading 29 JSON objects.** Beside it sits the panel that
carries most of the leverage: the **three nearest sibling trees by name/effect fingerprint**,
rendered side by side, which is the only way "841 trees that are all subtly the same" becomes
visible at all — and it is a failure no per-node review can ever detect.

### The verdict on D30

**D30 is affordable, and the reason is that review cost scales with TREES, not with nodes.**

- A 29-node tree card and a 4-node tree card take about the same time to judge. So
  [03-llm-stage-contract.md](03-llm-stage-contract.md) §8.2's *"4-vs-29 is the single biggest cost
  lever"* is true of **generation** and false of **review**. The owner picked 29; the review pipeline
  is nearly indifferent to it.
- **Census of all 841 species trees at 90 s per card ≈ 21 hours.** At 3 minutes it is 42 hours. That
  is 3–5 working days per full regeneration, and it certifies the one property sampling cannot:
  per-species recognition.
- The price nobody has written down is **machine** time, not human time:
  841 × 29 base calls + one voted field × 2 = **73,167 model calls ≈ 50–63 hours** at the demon run's
  own measured rate. Resumable per species, but 2–3 days of wall clock.
- Budget **two to three** review passes over the catalog's life, not one. The demon corpus needed
  three corpus-wide reprompts (`attackTempo`, `rarity`, `sunwoven`) after its first run completed.

**So: yes, at roughly 20–45 hours of human review per pass and ~60 hours of machine time — but only
if the tree card is built before the run, not after.** Reviewing this corpus without it is 216 hours
and will not happen, which means D24 would silently become "trusted, not reviewed." That is exactly
what happened last time, and §1 is the evidence.

---

## 1. What was actually done last time — the honest answer

**The 841-entry species corpus was gated by machine and spot-checked by hand. No human read it.**
That is not a criticism of the run; it is the baseline this pipeline has to beat, and it needs saying
plainly because D24 now makes review a shipping requirement rather than a nicety.

### 1.1 What the machine checked

**FACT.** `tools/DemonQualityReport/Program.cs` (437 lines, read in full this session) is the tool
that certified the corpus. It scans four dimensions:

| § | Dimension | What it reads |
|---|---|---|
| 1 | Classification integrity | duplicate anchors, index/disk drift, unresolved rate per voted field (`Program.cs:96-141`) |
| 2 | Catalog diversity | normalized Shannon entropy + used/possible + unused values, per closed vocabulary (`:158-205`) |
| 3 | Generation quality | expands every anchor through the real `SpeciesExpander`, counts refusals by reason (`:210-245`) |
| 4 | Balance | every species duelled against a self-calibrated median baseline through the **real** combat pipeline, 300 trials (`:250-300`) |

**Not one of those four reads a word of prose.** There is no check on `reason`, on `traits`, or on
whether a species' classification matches its own lore. Verified by reading the whole file: the only
string comparisons are enum membership and id equality.

### 1.2 What a human actually looked at

**FACT, counted this session across all 840 indexed entries in `data/seed/demons/species/`:**

| Signal | Count | Share |
|---|---|---|
| Entries carrying a `_provenance.manualCorrection` block | **3** | 3.6‰ |
| Entries in the on-disk review-queue bucket (`zombie/_needs-review.json`) | **1** | 1.2‰ |
| Voted-field judgements resolved `high` (3-0) | 3,665 | 83.7% |
| Voted-field judgements resolved `split` (2-1) | **695** | **15.9%** |
| Voted-field judgements `deterministic-fallback` | 10 | 0.2% |
| Voted-field judgements still `unresolved` | 11 | 0.3% |

The three corrected species are `Peashooter`, `SunFlower` and `UltimateHypnoDoom`, all stamped
`"by": "battle-timeline session, owner-approved"` — found by a *sweep looking for something else*,
not by a review pass. **695 two-to-one splits were resolved by majority vote and no human adjudicated
one of them.**

Adding up every hand-inspected species named in `tasks/demon-corpus-self-heal-todo.md` — the
10-species dedup spot-check (A2), 3 hand-verified merges (Checkpoint B), 8 rarity smoke species (E1),
8 sunwoven smoke species (G2), 2 model-interrogated (G3), 2 smoke (G4), 10 `fix_unresolved` (F4) —
gives **roughly 40 species, about 5% of the corpus**, and almost every one was checked for a *single
field value*, never read as a whole entry.

### 1.3 The blind spot, found this session

**FACT.** `DemonQualityReport` skips any file whose name begins with `_`
(`tools/DemonQualityReport/Program.cs:77`). `data/seed/demons/species/zombie/_needs-review.json`
begins with `_`. It holds one entry: a **stale 2026-09-02 copy of `SnorkleZombie`**, while
`_index.json` points at a different, newer copy in `zombie/undead.json` (2026-09-04). The two
disagree:

| field | indexed copy | stale copy in the review bucket |
|---|---|---|
| `rarity` | `sprout` | `chaff` |
| `elementPrimary` | `earth` | `dark` |
| `family` | `["undead","aquatic-reanimates"]` | `["aquatic undead"]` |

This is **the exact defect class the self-heal plan closed at scale** — 217 stale duplicates fixed in
Phase A2 — and one instance survives, invisible to the tool that then reported
*"840 anchor entries on disk, 840 distinct species ids, 840 indexed — clean"*
(`tasks/demon-corpus-self-heal-todo.md:35-37`).

It also explains the corpus count every passive-tree document repeats. **Counted this session: 840
entries in 502 species files, plus 1 entry in the `_`-prefixed review bucket = 841 across 503 files.**
The ideal's §9 count of 841 is right; it is right by including a species the quality tool cannot see.

**Two lessons this pipeline must carry:**

1. **A review queue that is merely a file is not a review queue.** It must be *counted* by the same
   report that certifies the corpus, or parking something in it removes it from every metric.
2. **A gate with an exclusion rule has a blind spot the size of that rule.** The `_`-prefix skip was
   a reasonable convention borrowed from `AtomImporter`; it silently became a hole.

### 1.4 The verdict on the baseline

**The 841-species corpus was gated, not reviewed.** The gating was genuinely good — better than most
generated corpora get, with real balance simulation and a real before/after
(`tasks/demon-corpus-self-heal-todo.md:239-266`). But every property that needed a human eye was
either machine-proxied (entropy standing in for diversity) or unexamined (prose, coherence,
sameness). Under D24 that is no longer sufficient, because D24 makes the *catalog* the thing shipped.

**One structural difference makes the tree corpus harder than the demon corpus, and it should be said
now:** a demon anchor's fields are *classifications of a creature the game already shipped* — a wrong
one is wrong against an existing referent, and simulation can catch its consequences. A tree node's
name and flavour are **new authored content with no referent**. Nothing can check them but a person.

---

## 2. The review budget

### 2.1 The rate

**There is no measured human review rate anywhere in this repo.** The only figure written down is an
assumption: *"At 30 seconds of human review per node"* (`06-red-team.md:298`). I am adopting it and
stating the sensitivity rather than pretending it is evidence.

**INFERENCE**, and it is the assumption everything below rests on:

| Rate | What it corresponds to |
|---|---|
| **15 s/node** | Reading nodes *inside* a tree card, where tree context is already loaded and only the node line is new |
| **30 s/node** | The repo's own figure — read the node, check it against its tier and branch |
| **60 s/node** | Real judgement: read it, compare it to its siblings, check its exclusion, decide |
| **60–90 s/tree** | Judging a whole tree from a card: is this coherent, does it read as this species, is anything obviously wrong |

The 60–90 s/tree figure is the one that has to be **calibrated on a real 20-tree pilot before the
full run is authorized**. That is this repo's own established discipline — E1 smoke-tested 8 species
before spending 2,584 calls (`tasks/demon-corpus-self-heal-todo.md:273-283`) — and the same rule
applies to a human throughput claim.

### 2.2 The arithmetic

Corpus, from the ideal §11.3: **1,560 generic nodes + 24,389 species nodes ≈ 25,949.**

| Approach | Hours |
|---|---|
| Read every node at 15 s | **108** |
| Read every node at 30 s | **216** |
| Read every node at 60 s | **432** |
| Read every node at 90 s | **649** |

**A 40-hour week of nothing but node review would take between three weeks and four months.** There
is one reviewer. Full review is not a schedule problem; it is arithmetically excluded.

⚠️ **The node count itself is inconsistent between two decisions and nobody has reconciled them.**
D30 (`passive-tree-ideal.md:58`) says *"FULL 29-node unique tree"*, and 841 × 29 = 24,389 exactly.
D29 (`:57`) sets tree shape at **10 tiers × 2 branches, ~40 nodes** and computes the generic corpus
as 39 × 40 = 1,560. If species trees follow D29's shape — and D10 says *"same shape everywhere"* —
D30's real figure is **841 × 40 = 33,640**, a 38% increase in both node count and call count that
appears in no cost estimate. This is a genuine open question (§9, item 1), not a re-litigation of
D30.

### 2.3 What the budget must therefore buy

Since a full read is excluded, the pipeline has exactly three levers, and it should use all three:

1. **Move properties from the human to the machine** wherever a machine can decide them (§4).
2. **Change the unit of review from the node to the tree** (§3) — worth more than the other two
   combined, because it divides the population by 29.
3. **Make the per-unit review faster** with an artifact designed for judgement rather than
   inspection (§6).

---

## 3. The sampling design

### 3.1 What is being claimed, precisely

Sampling supports a claim about a **population** at a **confidence**. Naming the population is the
part that gets skipped, so it is named first:

| Design | Population | The claim | The claim it does NOT support |
|---|---|---|---|
| **A. Node sample** | 25,949 nodes | *"the corpus-wide reviewer-rejection rate is p ± e"* | anything about any particular tree |
| **B. Tree cluster sample** | 880 trees | *"at most X% of trees carry a rejectable defect"* | anything about a *named* tree |
| **C. Tree census** | 880 trees | *"every tree was looked at by a person"* | that every *node* was read |

**D30's value is per-species recognition** (`03-llm-stage-contract.md:973`: *"it does not need to be
distinguishable from 903 others; it needs to feel like **that demon**"*). Recognition is a per-species
property and does not pool. **No sample of 60 trees certifies 841 identities.** So designs A and B
are quality-control instruments for the *generator*, and design C is the only one that discharges
D24 for a species catalog. §8 argues C is affordable; A and B still earn their place, because they
are what tells you whether to *start* the census.

### 3.2 The numbers, computed

**FACT** (computed this session: exact Clopper–Pearson one-sided upper bounds at α = 0.05, and Wald
sample sizes at z = 1.96 with p = 0.5, the worst case).

**Acceptance sampling — how much a clean sample proves:**

| n read | 0 rejects ⇒ true rate ≤ | 1 reject ⇒ ≤ | 2 ⇒ ≤ | 3 ⇒ ≤ |
|---:|---:|---:|---:|---:|
| 29 | 9.81% | 15.34% | 20.16% | 24.61% |
| 45 | 6.44% | 10.11% | 13.34% | 16.34% |
| **60** | **4.87%** | 7.66% | 10.12% | 12.42% |
| 93 | 3.17% | **5.00%** | 6.62% | 8.13% |
| 150 | 1.98% | 3.12% | 4.14% | 5.09% |
| 300 | 0.99% | 1.57% | 2.08% | 2.56% |

**Rate estimation — how much a sample measures:**

| Margin at 95% | n (infinite) | n at N = 25,949 | n at N = 841 |
|---|---:|---:|---:|
| ±10% | 96 | 96 | 86 |
| **±5%** | **385** | **379** | **264** |
| ±3% | 1,068 | 1,025 | 471 |
| ±2.5% | 1,537 | 1,451 | 544 |

**The finite-population correction is negligible at node scale** (385 → 379) and material at tree
scale (385 → 264). That asymmetry is itself an argument for the tree as the unit.

### 3.3 The three-tier protocol

**Tier 1 — CENSUS, not sampled.** Some populations are small enough and risky enough to read whole:

| Population | Expected size | Why census |
|---|---|---|
| Nodes carrying an **exclusion** | ~30‰ cap (`03`'s `exclusionRate` gate) ≈ 780 | D14's whole mechanism. A wrong predicate is a silent no-op the player never sees fire |
| Nodes the run **escalated** (`FAILED:<reason>` after bounded repair, gate 26) | unknown, expected small | Already known-bad; the machine asked for a person |
| Nodes with an **unresolved vote** (gate 24's 1-1-1 split) | ≤ 50‰ by gate | The demon run's 695 splits went unadjudicated; do not repeat that |
| Every entry in the **review queue** | should be small | §1.3 — a queue nobody counts is a hiding place |

At 30 s each, ~780 exclusion nodes ≈ **6.5 hours**. Affordable, and it is the highest-value reading
in the whole protocol.

**Tier 2 — CLUSTER SAMPLE over trees, for the generator's health.** Draw **60 trees**, read each
whole via its card. Zero rejects ⇒ *"at most 4.9% of trees carry a rejectable defect, at 95%
confidence."* Cost at 90 s/card: **90 minutes.**

Stratify by the axes on which the corpus is most likely to fail *unevenly* — the neglected-corner
principle `seedsmith.sampling`'s own docstring already states (*"a corpus fails in its neglected
corners"*, `tools/seedsmith/seedsmith/sampling/__init__.py:5-6`):

| Stratum axis | Levels | Why this axis |
|---|---|---|
| **Favour triple** (D17: aptitude × element × status) | the quota cells | The axis with the measured 166× problem (`passive-tree-ideal.md:384`) |
| **Side** | plant / zombie | Two different lore corpora, one prompt |
| **Rarity rung** | 10 | `fused` is 55% of the corpus after Phase E; the other nine rungs are the corners |
| **Tree category** | primary / elemental / status / family / species | Five different briefs |

Use the shipped primitive, not a new one: `stratified_sample(...)` (`sampling/__init__.py:38`)
already guarantees *"every non-empty stratum gets at least one sample"* then apportions the remainder
by largest-remainder, and seeds from `metric id + corpus revision` so *"a reviewer can re-read
exactly what they read last week and diff their own judgement against it"* (`:9-11`). That
reproducibility property is what makes a second reviewer's disagreement measurable.

**Tier 3 — THIN NODE SAMPLE over rare quota cells.** A 60-tree cluster sample under-covers rare
cells by construction: a status holding 4‰ of the quota appears in ~3 nodes corpus-wide and will
almost never land in 60 trees. Draw **~200 nodes** stratified by quota cell with the one-per-stratum
guarantee. Cost at 30 s: **1.7 hours.** Claim: *"no quota cell is systematically broken."* This is
the tier that catches "every `frostbite` node is the same sentence."

**Total, per full regeneration: ≈ 10 hours** for tiers 1–3, before any census.

### 3.4 The design effect, stated honestly

**INFERENCE.** Cluster sampling costs statistical efficiency: `deff = 1 + (b−1)·ICC` with b = 29
nodes per cluster. If defects cluster within a tree — and they will, because a bad tree usually has a
bad *brief* — the ICC is high and 60 trees × 29 nodes is worth far less than 1,740 independent nodes
for estimating a node-level rate.

**That is the right trade and it should be made deliberately.** A high ICC means the tree, not the
node, is the natural unit of failure, and therefore of rejection and regeneration. Reading 1,740
nodes as 60 whole trees buys a *tree-level* claim that is directly actionable; reading 1,740
scattered nodes buys a precise node-level rate that names no action.

---

## 4. What the machines check, and what is left over

[03-llm-stage-contract.md](03-llm-stage-contract.md) §7 lists 29 gates. Mapped against what a human
would otherwise have to judge:

### 4.1 Properties the machine closes completely — do not sample these

| Property | Gates | Human review adds nothing because |
|---|---|---|
| **No number was authored by the model** | 1, 11 | The field does not exist in the schema. `audit_schema` refuses at `Pipeline.__post_init__`, before a call is made — an unsampleable state, not a policy |
| **Every id is real** | 6, 7 | Constrained decoding plus enum membership. Two layers |
| **Distribution / skew** | 8, 9 | The permitted subset makes an out-of-quota value unsampleable; `QuotaDrift` re-derives the quota independently and catches overshoot too. **This is the 166× failure, fully machine-closed** |
| **Budgets, potency ceilings, per-tree equal value** | 12, `TreeEqualValue` | Arithmetic. Refuses rather than clamps |
| **Op legality** | 13 | A `More` op on a derived channel is a vocabulary fact |
| **Reachability, orphans, unsatisfiable prerequisites** | 23 | Graph arithmetic over the plan, before any call |
| **Id grammar, stability, collisions** | 17, 18 | `IdRefused`, and `name_collision` against `takenNames` |
| **Length, field-echo, subject-name echo, language mixing** | 20, 21 | All measured defects with mechanical signatures — *7 of 8 outputs began `"DOCTRINE: "`*; *87% code-switched* |
| **Idempotence, determinism, offline** | 27, 29 | Byte-hash comparison |
| **Balance in real combat** | — | `tools/CombatSim` drives the real resolver; `DemonQualityReport` §4 is the working precedent at 840-entry scale |

**So the charter's question — "is balance machine-checkable?" — is answered yes, twice over.** Budget
and quota conformance are checkable by construction, and *consequential* balance is checkable by
simulation, which this repo already does over a corpus of this size.

### 4.2 Properties the machine only *proxies* — sample these

| Property | What the machine gets | What it misses |
|---|---|---|
| **Motif / theme adherence** | Gate 22: token presence, anti-motif tokens | A node that uses every motif word and means none of them |
| **Diversity** | Normalized Shannon entropy per vocabulary (`DemonQualityReport.cs:400-415`) | Entropy is high when 841 trees use all 12 aptitudes evenly *and are all the same tree wearing 12 hats* |
| **Mechanism floor** | Gate 14: is `nodeClass` `mechanism` at deep tiers | Whether the mechanism does anything. `nodeClass` is a **plan-side label** (`03` §2.1 makes it GENERATED), so the gate checks the plan against itself |
| **Near-duplication** | Gate 19: lexical Jaccard over 5-gram shingles | **Semantic** sameness. `metrics/dedup.py:12-17` states this as a deliberate documented gap — conceptual clustering *"ships only once `axis` is added to the 516 adjective canonical entries"* |

### 4.3 Properties with no machine answer at all — these are the review

**FACT, and this is the repo's own ruling, enforced in code:** an OPEN-loop metric may never gate.
`MetricRegistry.register` raises — *"an OPEN-loop metric may never gate (P3) — refusing to register"*
(`tools/seedsmith/seedsmith/metrics/registry.py:20`). `Quality/FlavourGeneric`
(`metrics/quality.py:51-66`) is the shipped instance: *"Is the writing any good — has no machine
answer, so this NEVER reports a pass/fail. It writes a stratified sample into a review queue."*

**So the charter's question — "is FLAVOUR machine-checkable?" — is answered No, in code, already.**

The five irreducibly human properties, and what each costs:

| # | Property | Why no machine can decide it | Sampling tier |
|---|---|---|---|
| **H1** | **Name ↔ effect coherence.** Does "Kindling Wrath" plausibly mean *this* effect? | The machine can prove the name is unique and well-formed. Meaning is not a property of the string | 2, 3 |
| **H2** | **Flavour quality.** Is the line worth reading? | Ruled OPEN-loop in code | 2 |
| **H3** | **Is a mechanism node interesting?** | ⭐ **Decomposes, and half of it IS machine-checkable.** *"Does it change anything measurable"* → simulate it in `CombatSim` and read the win-share delta (`passive-tree-ideal.md:485-486`: mechanism nodes ARE measurable). *"Is it legible and worth building toward"* → human | Sim for the first half; tier 1 census on exclusions and tier 2 for the rest |
| **H4** | **Species recognition.** Does this tree read as *that* demon? | The whole point of D23/D30, and there is no referent to check against but the lore | **Census (§8)** |
| **H5** | **Corpus-scale sameness.** Are 841 trees secretly one tree? | Lexical dedup catches copies; the failure here is 841 *different* sentences expressing one idea. `metrics/dedup.py:12-17`'s documented gap | Tier 2, plus the corpus sheet's name-token table (§6.3) |

**H5 is the failure this corpus is most likely to actually have, and it is the one no per-node review
can see.** A reviewer reading node 4,112 in isolation has no way to notice it is the 400th variation
on "rage when hurt." That is why §6 spends its leverage on side-by-side rendering rather than on
prettier node inspection.

---

## 5. Escalation and rejection

### 5.1 The principle the repo already chose

**FACT.** `setgen/distribute.py:143-144`: *"Nothing here mutates the draft into legality. A draft
that broke a rule is REFUSED with the rule named, because silently repairing it teaches the next call
nothing."* The review ladder inherits it: **a rejection names the rule and regenerates; it does not
edit.**

The one sanctioned exception already exists and should be kept, with its discipline intact: the
`manualCorrection` block (§1.2), which records `from` / `to` / `by` / `why`. **A hand correction is
legal, must be provenance-stamped, and its rate is itself a metric.** If manual corrections exceed a
declared threshold, the prompt is wrong and the batch should have been rejected.

### 5.2 The ladder

| Rung | Trigger | Action | Cost |
|---|---|---|---|
| **0. Auto-repair** | Gate failure inside the run | `call_with_self_heal`, 1 generation + 2 repairs, then `FAILED:<reason>` recorded — never blank (gate 26) | in-run |
| **1. Node reject** | One node fails H1/H2 in review; its siblings are fine | Regenerate that node, with the reviewer's reason appended to the brief as an anti-motif. Never hand-write it | ~3 calls |
| **2. Tree reject** | ≥ 2 nodes rejected in one tree, or the tree fails H4 (does not read as the species) | Regenerate the whole tree. A tree-level defect is nearly always a brief-level defect | ~29 base + 58 vote calls |
| **3. Cell reject** | The tier-3 sample shows a quota cell is systematically weak (e.g. every `frostbite` node is the same) | Fix the cell's permitted subset or its motif set in the plan, regenerate every node in that cell | cell size |
| **4. Batch reject → REPROMPT** | The tier-2 sample's reject count reaches the acceptance number, **or** any tier-1 census finding is systemic | Stop. Fix the prompt. Redeploy corpus-wide at pipeline scope | the shipped shape: `rerun --pipeline <id> --all`, ~1 call/tree |
| **5. Owner escalation** | A `nullification` exclusion appears; a decision the plan cannot make; a `legitimateSkew` question | Queue it. Do not resolve it in the run | — |

**Rung 4 is not hypothetical — it is what the demon corpus actually did, three times.** `attackTempo`
entropy 0.00 → prompt fixed → `rerun --pipeline kit-shape --all`. `rarity` 59‰ unresolved → prompt
fixed using `ssot-rarity.md` §3.3's own rung descriptions → 2,584 calls, ~106 min, unresolved fell to
17‰. `sunwoven` 0/840 → root-caused by interrogating the model directly → the bar rewritten →
redeployed to 35 species (`tasks/demon-corpus-self-heal-todo.md:273-370`). **Rung 4 costs about one
call per unit at pipeline scope, which is why it is affordable to be strict.**

### 5.3 The acceptance numbers

**INFERENCE**, from §3.2's table. For a 60-tree tier-2 sample:

| Rejects in 60 | 95% upper bound on the tree defect rate | Verdict |
|---:|---:|---|
| 0 | 4.9% | **Accept** — proceed to census |
| 1 | 7.7% | **Accept with a named finding** — the rejected tree regenerates (rung 2), census proceeds |
| 2 | 10.1% | **Hold.** Draw 30 more trees; 2 in 90 ⇒ ≤ 6.8% |
| ≥ 3 | 12.4% | **Batch reject (rung 4).** More than one tree in ten is bad; fix the prompt |

⚠️ **These are starting values and must say so**, exactly as `demon-roster-targets.v1.json`'s own
`_note` does, and for the reason `distribution.py:97-98` gives: *"nobody can name a correct Pielou
value in advance."* Calibrate on the pilot (§2.1). They belong in
`data/tuning/passive-tree-targets.v1.json` beside `03` §5.3's gates, not in a report — the balance
surface is data (DESIGN-GATE §2 invariant 12).

### 5.4 What makes a batch unshippable

Copy `RunReport.verdict`'s discipline (`setgen/verdict.py:83-96`): **`FAIL` beats `NOT_MEASURED`, and
a held partition alone denies a pass.** A tree lot is unshippable when any of:

1. A gating metric FAILED **or did not run**. An absent check is never a pass
   (`metrics/registry.py:38`).
2. Any `nullification` exclusion exists — it is the only form that names a node, and a generated
   corpus cannot maintain one (`03` §6.3 removes it from the schema entirely).
3. `PassiveTree/UnresolvedCount` exceeds 50‰ — the one gate `03` §7 promotes to `gates=True`, for
   `demon_roster.py:357-370`'s stated reason: *"gating the RATE here stops a full run early."*
4. Any tier-1 census population is **unread**. A census is not a sample; partial is failure.
5. The tier-2 acceptance number is reached (§5.3).
6. `QuotaDrift` exceeds tolerance in either direction.
7. Any node is unreachable, or any prerequisite is unsatisfiable.

**And one that is specific to D24:** the concrete catalog must regenerate byte-identically from
unchanged seeds. `tools/DemonSpeciesGen/Program.cs:17`'s `--check` is the shipped pattern —
*"compare against what is on disk; write nothing; exit 1 if anything differs."* Without it,
*"identical for every player"* is a claim about one build machine.

---

## 6. The review artifact

**This is where the leverage is.** Everything above assumes a reviewer can judge a tree in 90
seconds. Nothing about raw JSON permits that. Three surfaces, generated deterministically from the
committed plan + catalog, checked in beside them, and diffable.

### 6.1 The tree card — the primary surface

One card per tree, one screen, no scrolling. Generated as static HTML into
`docs/research/passive-tree/_review/<lot>/<treeId>.html`, or rendered by the existing report command.

```text
┌───────────────────────────────────────────────────────────────────────────────┐
│ SnorkleZombie · aquatic undead · zombie · sprout        [23 gates green]  4/40 │  <- header
│ favour: Bulwark · earth · chilled          archetype: gated-deep   budget 98%  │
├───────────────────────────────────────────────────────────────────────────────┤
│ "The ability to ignore direct projectile damage while approaching stealthily   │  <- the RECOGNITION
│  makes it a significant tactical threat rather than a mere raider."            │     anchor: the
│  traits: amphibious, burrowing, ambush                                         │     species' own words
├──────────────────────────┬────────────────────────────────────────────────────┤
│ OFFENSIVE                │ DEFENSIVE                                          │  <- the LATTICE
│ t1  Silt Bite       +8%  │ t1  Mudskin              +6% phys mitigation       │     2 x 10, one line
│ t2  Undertow       +12%  │ t2  Held Breath          +9% chill resist          │     per node, tier
│ ...                      │ ...                                                │     ordered
│ t7  * Drag Under         │ t7  * Silt Shroud                        [excl]    │     * = mechanism
│ ...                      │ ...                                                │
│ t10 * Deep Water Claim   │ t10 * The Water Remembers                          │
├───────────────────────────────────────────────────────────────────────────────┤
│ NEAREST SIBLINGS   ZombieShark 0.71 · Snorkelmancer 0.68 · TideGhoul 0.64      │  <- the SAMENESS
│   Silt Bite / Undertow / Drag Under   vs   Reef Bite / Riptide / Pull Under    │     panel
├───────────────────────────────────────────────────────────────────────────────┤
│ NEEDS A PERSON  1 flavour sampled · 1 exclusion · 0 unresolved votes           │  <- only OPEN-loop
│ [ accept ]  [ reject: ______________ ]  [ owner ]                             │     findings appear
└───────────────────────────────────────────────────────────────────────────────┘
```

Six design rules, each earned:

1. **Hide everything the machine already proved.** Twenty-three green gates are one chip, not
   twenty-three rows. A reviewer who reads machine-verified facts is spending human attention on a
   machine's job. Only OPEN-loop and NOT_MEASURED findings get a line — the discipline
   `registry.py:38` already draws.
2. **The whole tree at once, in a fixed lattice.** 2 × 10 is 20 cells; `CreaturesLayer.tsx:18-24`'s
   shipped volume rule puts ≤ 24 in the *render-all* tier (`07-learnability-and-surface.md:271-274`).
   The tree is exactly the size a person can take in without scrolling, which is not a coincidence —
   it is why the tree is the right review unit.
3. **Render effects through the shipped magnitude contract, never as raw channel ids.**
   `formatMagnitude` takes a `Magnitude` with a `UnitClass` and **has no overload for a bare number**
   (`web/fusion-rpg-web/src/i18n/magnitude.ts:15`). `07`'s §2.2 flags `label={id}` as a live GG-23
   defect on a player surface; a review surface that inherits it makes the reviewer decode ids
   instead of judging content.
4. **The species' own `reason` sentence sits beside the nodes.** H4 is *"does this read as that
   creature"*, and that question is only askable if the creature is on the same screen. The anchor
   already carries the sentence, per-field confidence and traits — **the review artifact costs
   nothing to assemble, because the judgement inputs are already committed.**
5. **⭐ The sameness panel is the highest-value element and the hardest to skip.** Three nearest trees
   by fingerprint over `(node names, chosen affix ids, quota cells)`, rendered as parallel name
   lists. `metrics/dedup.py` already computes MinHash/LSH neighbours in O(n) buckets. This makes H5 —
   corpus-scale sameness — a *visible* property of a single card. Nothing else in this design does
   that, and no amount of node-level review ever will.
6. **The verdict control writes data, not prose.** Accept / reject-with-reason / escalate-to-owner
   append to `data/seed/passive-tree/_review/<lot>.json`. That file is then an **input to the next
   run's brief** (a reject reason becomes an anti-motif) and to the metric that counts rejection
   rate. A review that produces no machine-readable artifact cannot be measured, and a review nobody
   can measure is indistinguishable from one that did not happen.

### 6.2 What a reviewer is actually asked

Three questions, in this order, because the cheap one goes first:

1. **Does anything look wrong at a glance?** (a name repeated, an empty tier, a mechanism at t1)
2. **Do these twenty nodes read as this creature?** (H4)
3. **Is this tree meaningfully different from the three beside it?** (H5)

If all three are yes, accept — that is the 60–90 second path. If any is no, the reviewer types one
reason and moves on. **Diagnosis is not the reviewer's job**; rung 1–4 triage is the pipeline's.

### 6.3 The corpus sheet — one page for the whole lot

Read once before the tree cards, to decide whether the census is worth starting:

| Panel | Shows | Answers |
|---|---|---|
| **Quota grid heat map** | the (aptitude × element) cells, coloured by count vs quota | is the 166× failure back? |
| **Name-token frequency** | the 50 most common words across all node names, with tree counts | ⭐ H5 at corpus scale — if `wrath` appears in 300 trees, the census is premature |
| **Exclusion census** | every exclusion, its form, its predicate, its printed text | tier 1, in one list |
| **Nearest-neighbour top 20** | the 20 most similar tree pairs, by fingerprint | the worst sameness offenders, named |
| **Rejected so far** | rejection rate against the acceptance number, live | when to stop and reprompt |
| **Machine verdict** | every gate: `PASS` / `FAIL` / `NOT_MEASURED`, plus `missing_thresholds` | `setgen/verdict.py`'s shape — a gate with no number is visible *before* the run |

### 6.4 The one thing not to build

**Do not build an interactive tree editor.** The repo has already made this decision once in an
adjacent place: `stages/world/xyflowGuard.test.ts` enforces that the world stage abandoned
`@xyflow/react` for hand-rolled rendering on an authored grid
(`07-learnability-and-surface.md:277-280`). A review surface needs to be *generated, diffable and
checked in* — an editor is a second source of truth for content that is supposed to be regenerated
from a plan.

---

## 7. Incremental review

### 7.1 The rule

**Only the diff is re-reviewed, and node-id stability is what makes that sentence mean anything.**

[01-static-vs-rolled.md](01-static-vs-rolled.md) §7.3 settles the id scheme: a **composed structural
slug** `skill.<treeId>-<branch>-t<tier>-<nodeKey>`, and rejects the two alternatives for exactly this
reason — a content hash *"renames every node it touched"* on every rebalance, and a positional
ordinal renumbers on insertion, which `data/seed/README.md:109-111` already refuses.

**INFERENCE, and it is the load-bearing consequence:** with stable ids, re-review is `O(diff)`. With
content-hash ids, every rebalance is a full 25,949-node re-review — 216 hours, i.e. never. **Id
stability is not a nicety for build guides; it is the single decision that makes a second review pass
possible at all.**

### 7.2 The change classes

| Change | What re-review costs | Why |
|---|---|---|
| **Magnitude retune** (`data/tuning/` only) | **Zero human review.** Machine gates only | `01` R6: *"a magnitude retune does not touch ids and does not migrate anything."* Nothing a human judged has changed |
| **New trees** (a new species, a new element) | Full protocol, over the new lot only | The lot is its own population; the acceptance numbers apply unchanged |
| **Prompt-version bump** for one pipeline | Re-review the fields that pipeline owns, over the trees it touched | The shipped `rerun --pipeline <id>` scope. Cards render only the changed lines, highlighted |
| **Plan change** (tier ladder, quota targets, archetype) | Full re-review of the affected trees | The plan is the brief; a changed brief is new content |
| **Node retired** (`enabled: false`, `01` R2) | Census the retirements | Small, and each one has a player-visible consequence |

### 7.3 The diff card

The tree card gains a mode: render the tree as it is, with changed nodes highlighted and the previous
value struck through in place. **A reviewer judges a change in the context of the tree it lives in,
never as an isolated line** — the same argument as §3.4's, applied to time instead of space.

The lot's identity is the `catalog_revision` pair `(from, to)`. `catalog_revision` is already the
repo's one monotonic import counter, bumped once per transaction and only when something changed
(`effect-atom/definitions.md:279-283`).

### 7.4 Two hazards to name now

⚠️ **`ProvenanceLedger.record` raises on a re-recorded row** — *"a second write means idempotence
failed"* (`pipeline/provenance.py:109-118`). Regenerating after a prompt bump therefore needs
`provenance-supersede`, which `03` §6.4 already flags as **core backlog and unbuilt**
(`seedsmith-map.md` §3c). **An incremental review pipeline cannot run its second pass without it.**

⚠️ **A retired node makes an actor unloadable today.** `AptitudeAllocation.cs:38-39` throws on an
unknown id, per row from `RpgStore.Aptitudes.cs:129`. `01` R5 already names the fix — reject once at
a defined import boundary, naming every offending id in one report, never lazily on every actor load.
At D21's scale one bad row would otherwise brick a save.

---

## 8. Honest verdict on D30

**The owner chose the largest option. It works, and here is the true price.**

### 8.1 The reframing that makes it work

The 380-hour figure in `06-red-team.md:298` is correct **for per-node review**, and per-node review is
the wrong design. Three facts, together, change the arithmetic by an order of magnitude:

1. **The tree is the unit of judgement.** The three questions in §6.2 are all tree-level. Node-level
   review cannot answer any of them, and answers instead a question (*"is this node individually
   fine?"*) that the 29 machine gates already own.
2. **The tree is the unit of rejection.** You regenerate a tree, not a node — a tree's defect is
   almost always its brief.
3. **A tree card is a fixed-size object.** Twenty cells is inside the shipped render-all tier. So a
   29-node card and a 4-node card cost the same look.

⇒ **`03` §8.2's "4 unique nodes vs 29" is a generation lever worth 6×, and a review lever worth
almost nothing.** That is a direct answer to the cost objection D30 was raised against.

### 8.2 The price, itemised

| Line | Cost | Basis |
|---|---|---|
| **Model calls, first full run** | 73,167 calls ≈ **50–63 h** | 841 × 29 base + 1 voted field × 2. Rates: 16,272 calls ≈ 14 h (`03:723`); 2,584 calls ≈ 106 min (E1, measured) |
| **Human census, 841 tree cards** | **21 h** at 90 s · 14 h at 60 s · **42 h** at 3 min | §2.1's assumption, sensitivity shown |
| **Tiers 1–3** (exclusion census + 60-tree sample + 200-node sample) | **≈ 10 h** | §3.3 |
| **Building the review artifact** | ~10–15 h, one time | §6. A report renderer, a fingerprint, a verdict queue. Every input is already committed |
| **Expected reprompt passes** | **× 2–3** on the human lines | The demon corpus needed three. Each reprompt is ~841 calls at pipeline scope, then a re-review of the affected lot |

**Realistic total for the first catalog: ~60–135 hours of human review, front-loaded, plus ~2–3 days
of resumable machine time.** Steady state after that is `O(diff)` — a magnitude retune costs zero
human review (§7.2), which is the property that makes the catalog *maintainable* rather than merely
shippable.

### 8.3 What is genuinely bought, and what is not

**Bought:** every one of 841 species trees is looked at by a person, once, with the species' own lore
on the same screen. That discharges D24 honestly and it certifies H4 (recognition), the property D30
exists for and the only one sampling cannot reach.

**Not bought:** every *node* is not read. In an 841-tree census a reviewer reads ~20 node lines per
card, but as a lattice, not as 20 separate judgements. So the residual claim is: *"every tree was
judged; individual nodes carry the machine's 29 gates plus a sampled human rate with a ~5% margin."*
**Say that in the acceptance record. Do not say "the catalog was reviewed" unqualified** — that is
precisely the overclaim §1 found in the demon baseline.

### 8.4 The largest reviewable volume, since the charter asks

**Review capacity is measured in trees, not nodes.** At 90 s per card and a 40-hour budget per pass,
the ceiling is **~1,600 trees per review pass**. D30 asks for 880 (841 species + 39 generic). It
fits, with room.

The volume that does *not* fit is the one red-team F6 names: if the demon-family axis is resolved as
**699 family trees**, the roster becomes 1,579 trees and the corpus ~45,800 nodes
(`06-red-team.md:290-300`). That is at the ceiling on the first pass and over it on every reprompt.
**The family taxonomy decision, not D30, is what would break this pipeline** — and it is already an
open item (D27 defers it to build order).

### 8.5 Conditions

D30 is affordable **if and only if**:

1. **The tree card is built before the run**, and its throughput is calibrated on a 20-tree pilot.
   Without it the number really is 216 hours and the review will not happen.
2. **The `_`-prefix blind spot is closed**, and the review queue is counted by the same report that
   certifies the corpus (§1.3).
3. **Ids are composed structural slugs** (`01` §7.3), so pass two is `O(diff)`.
4. **`provenance-supersede` lands** before any second generation pass (§7.4).
5. **The D29/D30 node-count contradiction is resolved** (§2.2) — 29 or 40 changes the call budget by
   38%.

---

## 9. Open items

Only questions nobody has answered. A recommendation nobody has disputed is a decision, and an
answerable question is a task — both are above, not here.

1. **Is a species tree 29 nodes or 40?** D30 says 29 (841 × 29 = 24,389 exactly); D29 says every tree
   is 10 tiers × 2 branches ≈ 40, and D10 says *"same shape everywhere."* At 40 the corpus is 33,640
   species nodes and ~100,800 calls. **Owner ruling, and it moves the machine budget by 38%.**
2. **What is the real per-tree review rate?** Everything in §8 rests on 60–90 s. Unmeasured. A
   20-tree pilot answers it in half an hour and should gate the full run.
3. **Is a two-reviewer agreement pass wanted on any tier?** The seeded sample is reproducible by
   construction (`sampling/__init__.py:9-11`), so inter-reviewer agreement is measurable at no extra
   sampling cost. Whether it is worth a second person's time is not mine to decide.
4. **What is the acceptable manual-correction rate?** §5.1 makes hand correction legal and stamped.
   Above some rate it means the prompt is wrong. The demon corpus's rate was 3.6‰ — but that was a
   floor set by how little was reviewed, not a ceiling set by quality.

### Gaps I could not close, stated honestly

- **I did not build or run anything.** Every hour figure is arithmetic over a stated assumption, not
  a measurement. §2.1 and open item 2 say so.
- **I did not read a single one of the 840 species entries end to end** — I counted their provenance
  fields programmatically and read two in full (`SunFlower`, `SnorkleZombie`). So my claim that the
  corpus was not read by a human is evidenced by *provenance and task records*, not by inspecting the
  content for defects. If someone reviewed it without recording it, I would not know.
- **I did not run the seedsmith or C# test suites.** This note proposes no code change, so there is
  no *"this moves goldens"* constraint to test (evidence rule 4).
- **The design effect in §3.4 is reasoned, not estimated.** The intra-tree correlation of defects
  cannot be known before a corpus exists. The pilot measures it.
- **I did not verify that `data/generated/demons/`'s file shape is what a tree's concrete stage would
  follow** — `03` §9 flagged the same gap and it is still open.

---

## 10. Pre-proposal checklist

```
[x] I identified the subsystem(s) this touches - passive trees, seedsmith generation,
    the demon species corpus, review/metrics.
[x] I read every doc in the DESIGN-GATE §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md, passive-tree-ideal.md (D13/D14/D17/D23/D24/D29/D30/D32, §6, §9, §10, §11),
    research/passive-tree/01 and 03 in full, and the relevant sections of 02, 06, 07.
[x] I checked decisions.md for a lock covering this - via the ideal's own D24/D30 and
    06-red-team's F6/F7 readings of decisions.md:97,103.
[x] Every factual claim cites file:line, or says it was counted this session.
[x] I verified claims against CODE and DATA, not comments - counted 840 entries in 502 files
    plus 1 in the review bucket, counted 3 manualCorrection blocks and 695 split votes, and
    read all 437 lines of DemonQualityReport rather than trusting its summary.
[ ] I tested (not assumed) any constraint I am reporting. NOT APPLICABLE and said so in §9:
    no code change is proposed. The one constraint I do report - the `_`-prefix blind spot -
    was verified by reading the skip at Program.cs:77 and confirming the surviving stale
    SnorkleZombie copy on disk.
[x] Nothing contradicts a §2 invariant. Two are load-bearing and both hold: #11 (no hard
    ceilings - every threshold here is an acceptance number in tuning, and the potency
    ceiling refuses rather than clamps) and #12 (the balance surface is data - the acceptance
    numbers belong in data/tuning/passive-tree-targets.v1.json, not in a report).
[ ] Corrections are propagated. TWO FINDINGS ARE NOT YET PROPAGATED, deliberately, because
    this note does not edit data/ or other docs:
      - the surviving stale SnorkleZombie duplicate in zombie/_needs-review.json, and the
        `_`-prefix blind spot in DemonQualityReport/Program.cs:77 that hides it;
      - the D29/D30 node-count contradiction (29 vs 40 nodes per species tree).
    Both are reported here and belong in demon-corpus-self-heal's record and the ideal
    respectively.
```

---

## 11. Related

- [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) — D23, D24, D29, D30, §9's measured skew, §11.3's corpus total
- [01-static-vs-rolled.md](01-static-vs-rolled.md) — the freeze line, R1–R6, §7 node-id stability
- [03-llm-stage-contract.md](03-llm-stage-contract.md) — the 29 gates, the quota algorithm, §8's species pipeline
- [06-red-team.md](06-red-team.md) — F6, and the 380-hour figure this note reframes
- [07-learnability-and-surface.md](07-learnability-and-surface.md) — the volume rule and the magnitude contract the card reuses
- `tasks/demon-corpus-self-heal-plan.md` / `-todo.md` — the baseline: what a corpus review has actually looked like here
- `tools/DemonQualityReport/Program.cs` — the four machine dimensions, and the blind spot at `:77`
