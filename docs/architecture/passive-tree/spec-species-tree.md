# Spec: `species-tree`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `species-tree` · **Wave:** 4 · **Depends on:** `tree-language`, `tree-binder`,
`tree-review`
**Model calls:** ~105,800 for a first full corpus. This is the largest single content commitment in
the program.

---

## Objective

Generate **840 unique per-species passive trees** — one per demon species, 40 nodes each, nodes no
other tree has — under a build-favour lock the planner assigns *before* generation, and emit the one
extra artifact that makes the corpus usable: a summary sentence per species for the Codex.

D23, in the owner's words: *"better to spend effort for it now, maybe deploy agent to enrich it."*
D30 takes the maximum-identity reading — every species gets a full tree, not a badge.

**The one-line contract:** the planner decides *what build favour this species locks*, and the
language stage only ever chooses from options the planner already permitted. **The generation is not
the hard part. The review is** — which is why this module is sequenced after
[`tree-review`](spec-tree-review.md) and inherits its whole protocol rather than inventing one.

---

## Design

### 1. Why this needs its own pipeline

The roster is **five tree categories** — `primary | elemental | status | family | species`. Three of
them exist today and [`tree-language`](spec-tree-language.md) handles all three as 39 shared trees
whose brief is a roster row: an aptitude, or an element, or a status. `family` waits on a closed
taxonomy (§7.3). **`species` is this module, and it is a category in its own right, not a variant of
the generic pipeline** — a species tree's brief is a *creature*, and five things change at once.

| | Generic pipeline (`tree-language`) | This module |
|---|---|---|
| **Population** | 39 trees | **840 species** — 21× |
| **Input** | one roster row | the **species anchor**: 18 classified fields plus the almanac lore, already committed |
| **Quota axes** | 6 (nodeClass, trigger, element, status, channelFamily, exclusionForm) | the same **plus the D17 favour triple** — the axis with the measured 166× problem |
| **Distinctness bar** | *differentiation* — 39 trees must be tellable apart | ⭐ **recognition** — *"it does not need to be distinguishable from 903 others; it needs to feel like that demon"* (`spec-set-charm-gen.md` D17) |
| **Uniqueness** | nodes drawn from a shared affix library | **nodes no other tree has** (§5) |

Two of those are differences of *kind*, not degree, and either alone justifies a separate module:

**The distinctness bar inverts the metric.** Differentiation is a corpus property and machine-proxied
by entropy and lexical dedup. Recognition is a *per-species* property with no referent but the lore —
it does not pool, so no sample certifies it and only a census reaches it
([`tree-review`](spec-tree-review.md) §2). A pipeline whose acceptance criterion is per-subject needs
its own lot structure, its own review budget and its own card.

**The favour lock reverses the direction of choice.** In the generic pipeline the tree's aptitude is
given by its roster row. Here it must be *assigned*, and D17 is explicit about how: *"a deterministic
planner → agent-inspects-seed → validated-against-target pipeline, never an LLM free choice."* That is
a different call shape from §6 of the generic contract, and putting it inside `tree-language` would
mean one adapter with two contradictory selection models.

**What does NOT change, and is therefore not re-specified here:** the node record
([`tree-catalog`](spec-tree-catalog.md) §2 — map assumption 4, species trees reuse it), the **24**
validation gates ([`tree-language`](spec-tree-language.md) §7 owns them and is the only document that
numbers them — **cite a gate by name, never by ordinal**, because the research doc's numbering has
already drifted against the spec's twice), the no-numbers schema audit, the permitted-subset fence,
the bounded self-heal, the byte-identity requirement, and every escalation rung. **This module adds
three things to that pipeline** — the favour lock (§3), the uniqueness gate (§5) and the Codex
sentence (§6) — and inherits the rest verbatim.

### 2. The corpus, counted

**FACT, counted 2026-09-05 over `data/seed/demons/species/`:**

| | Count |
|---|---:|
| Non-`_` species files | **502** |
| Anchor entries in them | **840** |
| Distinct species ids | **840** |
| Keys in `_index.json` | **840** |
| Entries in `_`-prefixed files | **1** — a stale duplicate, §2.1 |

**840 species, not 841.** Every passive-tree document that says 841 counted the stale duplicate. The
corrected figures: **33,600 species nodes** (840 × 40), a **35,160-node** whole corpus over **879
trees**, and — pleasingly exactly — **100,800 node generation calls** (§7).

The skew D17 locks against, recomputed over the 840 indexed entries:

| aptitudePrimary | count | share | | elementPrimary | count | share |
|---|---:|---:|---|---|---:|---:|
| Onslaught | 332 | **39.5%** | | earth | 379 | **45.1%** |
| Bulwark | 133 | 15.8% | | fire | 138 | 16.4% |
| Retribution | 113 | 13.5% | | light | 102 | 12.1% |
| Focus | 89 | 10.6% | | ice | 95 | 11.3% |
| Precision | 50 | 6.0% | | dark | **70** | 8.3% |
| Fortitude | 49 | 5.8% | | air | 56 | **6.7%** |
| Pierce | 25 | 3.0% | | | | |
| Agility | 19 | 2.3% | | | | |
| *unresolved* | **11** | 1.3% | | | | |
| Vigor | 7 | 0.8% | | | | |
| Might | 6 | 0.7% | | | | |
| Composure | 4 | 0.5% | | | | |
| Ferocity | 2 | **0.24%** | | | | |

Uniform is 8.3% per aptitude and 16.7% per element. **Onslaught 332 : Ferocity 2 is a 166× ratio**,
and `earth` alone is 45%. Two cells move against the ideal's §9 table (`dark` 70 not 71, `unresolved`
11 not 12) because the stale duplicate is the extra `dark` and the indexed `SnorkleZombie` is one of
the eleven unresolved. **Nothing about D17, D32 or this module changes; the numbers are restated
because a pipeline that locks against a distribution should be able to count it.**

#### 2.1 The blind spot this module must not inherit

`tools/DemonQualityReport/Program.cs:77` skips any file beginning with `_`, and
`data/seed/demons/species/zombie/_needs-review.json` begins with `_`. It holds one entry: a stale
2026-09-02 `SnorkleZombie` disagreeing with the indexed 2026-09-04 copy on `rarity` (`chaff` vs
`sprout`), `elementPrimary` (`dark` vs `earth`) and `family` (`["aquatic undead"]` vs
`["undead","aquatic-reanimates"]`). The tool that reported *"840 indexed — clean"* could not see it.

**This module reads the anchors, so it would silently inherit the same hole.** Two rules:

1. **The species roster is read from `_index.json`, and every file is walked without the `_` skip.**
   A species present on disk but not indexed, or indexed twice, halts the run naming both paths —
   never *"pick the first one."*
2. **`PassiveTree/HiddenFileCount`** ([`tree-review`](spec-tree-review.md) §7) runs over this
   module's own seed roots too, and a parked entry makes a lot unshippable. The review queue this
   pipeline creates must be *counted by the report that certifies it*, or parking something in it
   removes it from every metric.

### 3. The build-favour lock (D17)

D17: a species tree **locks a build-favour triple — primary tree + element + status.** That lock is
the price the player pays for something unobtainable elsewhere, so *which* triple each species gets is
a distribution decision, not a flavour decision.

**The owner's fear is measured fact, not a hypothesis.** The 166× skew in §2 was produced by a
pipeline whose enums were **open** — eight classifiers each free to pick any of 12 aptitudes and any of
6 elements. The mechanism is documented: position bias and label bias compound, and *"a biased
classifier produces output where every individual answer looks right and the aggregate is skewed.
Species by species, a reviewer sees nothing wrong."* Permutation and 3-way voting are worth up to ~8
points of per-entry accuracy against a 166× aggregate spread. **They are the right tool for per-entry
correctness and the wrong tool for aggregate shape. Only removing the option from the call fixes
aggregate shape.**

#### 3.1 The four steps

```text
1  planner   quota[(aptitude, element, status)] := largest_remainder_count(
                 targets.mechanicalFavour.weightsMilli, ORDER, 840)
             # exact integer marginals, sum == 840 by construction, ties broken on declared order

2  planner   assign each species one cell, seeded from speciesId - reproducible, model-free.
             Emit 2-3 ALTERNATES per species, drawn from the SAME quota.

3  stage     the call receives ONE cell, its alternates, and the species' own lore, and answers
             ONE question: "does this favour fit this creature, and if not, which of these
             alternates does?"   -- every option is already inside the quota

4  gate      PassiveTree/FavourDrift compares the emitted favour distribution against
             data/tuning/passive-tree-targets.v1.json, re-derived independently, symmetric
             (overshoot fails too), and fails loudly on drift
```

**Step 3 is the shape that makes the 166× problem structurally impossible**, because no answer the
stage can give is outside the quota. It is also *cheaper* than free choice: a 2–3 way pick over a
narrow set is the task shape enum classification is most reliable at, and the `unresolved` rate
becomes a direct measurement of how well the target fits the corpus rather than a mystery.

**Step 1 must rebalance on override, and this is the line that gets forgotten.** A hard constraint —
a species whose anchor already fixes an element, say — consumes its cell **by force**. Its drawn value
must return to the pool and the pool must be re-apportioned over the remaining species. Skip it and
the forced species consume their quota twice, once by force and once by draw, and the residual free
species inherit the deficit. **That is the original defect wearing a planner's uniform.**

#### 3.2 The target D32 names

D32: **near-uniform with a NAMED theme allowance.** Uniform is the target (8.3% per aptitude, 16.7%
per element); a small explicit per-axis exception is declared **as data** — `earth` may run to roughly
1.5× uniform because plants really are earthy — with everything else held inside a stated band.

That exception lives in `data/tuning/passive-tree-targets.v1.json` under `legitimateSkew`, with a
`_why` recording that a row there is **a claim that an imbalance is theme, not bias**. The judgement
§9 of the ideal demanded gets argued **once, in a file**, instead of re-litigated per species. The
file is shaped like `data/tuning/demon-roster-targets.v1.json` — integer per-mille throughout, a
`_note` recording provenance, and no axis listing its own members (aptitudes come from
`data/seed/aptitudes/roster.json`, elements from `data/seed/elements/roster.json`, statuses from the
status catalog mirror, **so a thirteenth aptitude changes the grid by construction rather than by a
forgotten edit**).

Every threshold in it is a **starting value and must say so**, for `distribution.py:97-98`'s reason:
*"nobody can name a correct Pielou value in advance."*

### 4. The decoupling corollary — two fields, not one

The ideal's §9 corollary, and it is the cheapest structural decision in this module:

> **A species' *thematic* favour and its *mechanical* lock need not be the same field. If they are one
> field, thematic truth (plants are earthy) becomes mechanical skew (everyone plays earth).**

So the anchor's existing `elementPrimary` / `aptitudePrimary` — which are **classifications of what
the creature is** — are **inputs to the brief, never the lock**. The lock is a separate, planner-owned
field:

| Field | Owner | Means | Distribution |
|---|---|---|---|
| `elementPrimary`, `aptitudePrimary`, `posture`, `traits`, `reason` | the demon corpus, already committed | **what this creature is** | whatever the lore is — 45% earth is *correct* here |
| `mechanicalFavour: { aptitude, element, status }` | **this module's planner** | **what building into this species rewards** | quota-assigned, near-uniform, D32 |

**Three things follow, and each is worth the field on its own:**

1. **Lore stays honest.** Nobody has to pretend a sunflower is not earthy in order to keep the build
   space even.
2. **The skew becomes a *reported* property rather than an inherited one.** If the two fields agree
   for 45% of species, that is a measurable fact about theme, not a silent 45% earth lock.
3. **A species whose thematic and mechanical favour differ is the most interesting content in the
   corpus** — an earthy creature that rewards a `light` build has a reason to exist, and the stage is
   asked to *find that reason in the lore*, which is a much better prompt than "pick an element."

⚠ **This does not license an arbitrary lock.** Step 3 of §3.1 still asks whether the assigned favour
*fits*, and a species may still answer *"none of these three."* That answer is `unresolved`, it is
counted, and above 50‰ it stops the run — the shipped `UnresolvedCount` discipline, which exists for
exactly this: gating the **rate** stops a full run early, before thousands of calls are spent.

### 5. Uniqueness as a checkable property

D23 promises **nodes no other tree has.** *"Unique"* has to mean something a machine can verify across
840 trees, or the promise is a hope. Three strengths, and this module gates on all three:

| | Property | Checked by | Cost |
|---|---|---|---|
| **U1** | **Text uniqueness** — the node's `name` and `flavor` appear in no other node, corpus-wide | the shipped exact-match and 5-gram-shingle dedup (`metrics/dedup.py`); `name_collision` against `takenNames` at generation time | free |
| **U2** | **Composition uniqueness** — the node's `(affixIds multiset, quotaCell)` fingerprint appears in no other **tree** | a reverse index over the concrete catalog: fingerprint → set of treeIds. O(n) | free |
| **U3** | **Namespace uniqueness** — at least `speciesUniqueAffixMin` of the tree's 40 nodes reference an affix in that species' own namespace, referenced by no other tree | the same reverse index, over `affix.species.<speciesId>.*` | **real** — §5.2 |

**U1 + U2 together are what makes a species tree unique in the sense a player experiences**: a node
they have not seen before, doing a combination they have not seen before. **U3 is what makes it
mechanically unobtainable elsewhere**, and it is the one that costs.

#### 5.1 The gate

`PassiveTree/SpeciesUniqueness` reports three findings and gates on none of them until the pilot
calibrates the thresholds (the shipped posture — promote one gate at a time, and only after a real run
has been measured against it):

```text
U1  any name or flavor string appearing in > 1 node                      -> GAP, names both nodes
U2  any (affixIds, quotaCell) fingerprint appearing in > 1 tree          -> GAP, names both trees
U3  any species tree with fewer than `speciesUniqueAffixMin` own-namespace
    nodes, OR any affix.species.<id>.* referenced from another tree      -> GAP, names the tree
```

The tree-level companion is already specified: `tree-review`'s sibling panel renders the **three
nearest trees by fingerprint** side by side on every card, which is the only way the failure U1–U3
cannot see — 840 *different* sentences expressing one idea — becomes visible at all.

#### 5.2 What U3 actually costs, stated in numbers

**FACT:** the shipped authored-affix corpus is `data/seed/effects/affixes/all.json`, and it holds
**two** entries — `affix.authored.affix-draw-000` and `-001`, both emitted by the `affix-authoring`
pipeline on 2026-09-04 with per-field vote confidence. The shared affix library it draws atoms from is
**98 families** under `data/seed/items/affix-families/`.

So U3 at `k` unique affixes per species costs **840 × k authored affixes**, on top of the node
generation. At k = 40 (every node bespoke at the affix level) that is 33,600 new affixes — more than
340× the entire shipped affix library, and an authoring problem larger than this whole program.

**Recommendation, and it is a recommendation because nobody has disputed it:** `speciesUniqueAffixMin`
defaults to **4** — a small unique core at the deep tiers where a mechanism node actually rescues a
focus build — with the other 36 nodes unique by U1 + U2 over the shared library. 840 × 4 = **3,360**
authored affixes, which is the same order as one of the demon corpus's own reprompt passes. The number
is a **tunable** in `data/tuning/passive-tree-targets.v1.json`, so a later decision to raise it is a
file save and a regeneration of one lot, not a spec change.

⚠ **This does not reduce D30's node count and does not reduce the call budget.** All 40 nodes are
authored per species — that is what §7's 100,800 calls buy. `speciesUniqueAffixMin` governs how many
of them carry an *affix* nobody else can reach, which is a different question from how many are
*written for this creature*.

#### 5.3 Building U3 while the ruling is open

**4 is a narrowing of D23's *"nodes no other tree has"*, and narrowing a locked decision is the
owner's call** — so it stays open (open question 1). That is not a reason to leave the gate
unspecified: **the ruling changes one integer and nothing else.** Not the schema, not the id grammar,
not the review protocol, not the call budget. Build to this, and the owner's answer is a tuning edit
plus one lot's regeneration.

1. **The threshold is a tunable, read every run.** `speciesUniqueAffixMin`, an integer in
   `data/tuning/passive-tree-targets.v1.json`. `PassiveTree/SpeciesUniqueness` reads it and never
   carries a literal, per the `_require`/`_validate` discipline that *"refuses to substitute a
   default"*.
2. **`0` is legal and tested.** It means *"no species-namespace requirement"* — U1 and U2 still gate,
   U3's own-namespace clause reports nothing, and the second half of U3 (an
   `affix.species.<id>.*` referenced from another tree) still fires. A threshold whose lowest
   defensible value crashes the gate is a threshold nobody can lower after a real run.
3. **Which nodes carry the unique affix is planner-owned and deterministic, never a generation-time
   choice.** The plan marks the deepest `speciesUniqueAffixMin` **mechanism** nodes — deepest tier
   first, ties broken on branch order then `nodeKey`. Deepest, because that is where a mechanism node
   actually rescues a focused build. **Deterministic and bottom-anchored is what makes a later
   increase cheap:** raising the number adds marks at the shallow end and never moves, renames or
   re-owns a node that already has one, so a re-review costs `O(diff)` and not a corpus.
4. **The namespace is `affix.species.<speciesId>.*`, and its ids are minted once and read back on
   regeneration** — the same rule `tree-catalog` applies to `nodeKey`. Recomputing an affix id from a
   node's position would make every archetype change a full re-authoring.
5. **The reverse index is built over the concrete catalog and gates on nothing until the pilot
   calibrates**, per §5.1's shipped posture: promote one gate at a time, and only after a real run has
   been measured against it.

**What the ruling would change, exhaustively:** the integer, and therefore the authored-affix bill
(840 × `k`). Nothing above depends on its value.

### 6. The Codex summary sentence

[14-learnability-at-scale.md §3.3](../../research/passive-tree/14-learnability-at-scale.md) settles
where a species tree lives on the player surface, and the finding is load-bearing for this module's
output contract:

> **A species tree is not a choice, so it needs no chooser.** Every other browse exists because the
> player picks from it. A player cannot pick a bloodline — it is a property of the demon they bound.
> There is no build-planning reason to put 840 of them side by side, because no decision is taken by
> comparing them.

So the tree is reached two ways: **to spend**, from that demon's own actor sheet, pinned above the
shared paths; **to read**, from the Demon Codex, which already ships as a species reference with a
discovery state (`web/fusion-rpg-web/src/features/demons/DemonsPage.tsx:365-390` renders one card per
species with a name and a rarity badge, greyed until `seen`/`discovered`).

**The one real cross-species question is a collection question, not a build question:** *"which demon
should I bind next?"* The Codex answers it at the resolution the Codex already works at — a rarity
badge, an element, the favour triple, and **one line naming what the bloodline is for.** Not by 40
node descriptions × 840.

**So this module emits one summary sentence per species**, and it is a first-class field of the seed,
not a rendering afterthought:

| | |
|---|---|
| **Field** | `codexSummary` — AUTHORED, ≤ 140 chars, no numbers, no mechanics jargon |
| **Says** | what building into this bloodline *rewards*, in the player's words |
| **Where** | `data/seed/passive-tree/species/<speciesId>.json`, beside the nodes; rendered on the Codex card |
| **Cost now** | 840 base + 1,680 vote calls ≈ **2,520 calls, 1.2–2.2 h** — it rides the same run |
| **Cost later** | a second pass over 840 artifacts, with `provenance-supersede` unbuilt (§8) |

**Booking it now is the whole point.** Doc 14 §10.3 files it as *"cheap if booked into the D30
pipeline now, expensive as a second pass over 841 artifacts"* — 841 there is the stale count and
**840** is the verified figure (§2); the argument is unchanged by it. It is also the single cheapest way to
make 33,600 nodes usable: a player reads 840 sentences over the life of the game, and never 33,600
node lines.

⚠ **One live defect sits on the surface this lands on.** `DemonsPage.tsx:367-388` maps the **entire**
species catalog into a grid with no volume strategy — `(catalog.data?.species ?? []).map(...)` — which
is 840 DOM subtrees against a search-first threshold of 240
(`CreaturesLayer.tsx:21-22`). It is a live GG-50 violation today, independent of passive trees, and it
is the surface a bloodline reference would be hung off. **Named, not owned** — it belongs to whoever
owns the Codex, and it should be fixed before anything is added to that grid.

### 7. The cost, in numbers

#### 7.1 Machine

```text
node generation   840 species x 40 nodes                    =  33,600 base calls
vote              33,600 x 1 voted field x (3 - 1)          =  67,200 vote calls
                                                               -------
                                                              100,800
favour lock       840 base + 1,680 vote (S3)                =   2,520
codex summary     840 base + 1,680 vote (S6)                =   2,520
                                                               -------
                                                              105,840 calls
```

**Vote exactly one field, and name it: `affixIds`.** Being wrong there is expensive to fix later — it
decides what the node *does* — while `name`/`flavor` are cheap to regenerate and `elementSlot` /
`statusId` / `nodeClass` are already narrowed to one or two options by the quota. Voting every field
takes the run past 300,000 calls for no measured benefit; the shipped comment on the equivalent
decision says adding one voted field *"moves the call budget by a third of the run."*

Three **measured** rates from this repo's own runs bracket the wall clock:

| Measured run | Rate | 105,840 calls |
|---|---:|---:|
| 2,860 calls in ~80 min, `workers=4` (`tasks/demon-corpus-self-heal-todo.md:165`) | 2,145/h | **49 h** |
| 2,584 calls in ~106 min, `workers=4` (`:281`) | 1,463/h | **72 h** |
| 16,272 calls ≈ 14 h (`03-llm-stage-contract.md:723`) | 1,162/h | **91 h** |

**49–91 machine hours, resumable per species — two to four days of wall clock.** Resumability is not
optional at this size: the workflow's SQLite checkpoint and the `run start/pause/resume/rerun` verbs
already ship for exactly this shape, and a run that cannot resume is a run that must be perfect.

#### 7.2 Human

From [`tree-review`](spec-tree-review.md) §1.3, applied to the 840-tree species lot:

| Line | Hours |
|---|---:|
| Census — 840 tree cards @ 90 s (H4, recognition; the only design that reaches it) | **21.0** |
| Tier 1 — exclusion / escalated / unresolved-vote census | **8.4** |
| Tier 2 — 60 tree cards @ 90 s | 1.5 |
| Tier 3 — 200 nodes @ 30 s | 1.7 |
| **One full pass** | **≈ 33** |

**Budget two to three passes.** The demon corpus needed **three** corpus-wide reprompts after its
first run completed — `attackTempo`, `rarity` and `sunwoven` — each root-caused, fixed in the prompt,
and redeployed at pipeline scope for about one call per unit. So:

> **≈ 76–114 hours of human review for the first species catalog** (33 h × 2–3 passes, plus
> `tree-review`'s one-time 10–15 h to build the card and the corpus sheet), against **49–91 hours of
> resumable machine time.**

Steady state after that is `O(diff)`: a magnitude retune costs **zero** human review, because ids are
structural and nothing a human judged has changed.

#### 7.3 The one number that would break this

**Review capacity is measured in trees.** At 90 s per card and a 40-hour pass, the ceiling is ~1,600
trees. This module asks for 840 of the program's 879. **It fits, with room.**

The volume that does *not* fit is the demon-**family** axis. `family` is an OPEN axis and the corpus
carries **698 distinct family tokens across the 840 indexed entries** (counted 2026-09-05; the 699
figure in the research includes the stale duplicate's `"aquatic undead"`). Resolved as one tree per
token, the roster becomes ~1,580 trees — at the ceiling on the first pass and over it on every
reprompt. **The family taxonomy decision, not D30, is what would break this pipeline**, and D27
already defers it to build order. This module must be given a *closed* family roster or none at all.

### 8. What this inherits, and what blocks it

Named here so they are raised at task start rather than discovered mid-run.

| Dependency | State | Effect on this module |
|---|---|---|
| **`provenance-supersede`** | seedsmith core backlog, **unbuilt**. `ProvenanceLedger.record` raises on a re-recorded row — *"a second write means idempotence failed"* (`pipeline/provenance.py:109-118`) | ⛔ **Hard.** A prompt-version bump cannot regenerate. §7.2 budgets 2–3 passes, and pass 2 cannot start without this |
| **The theme registry** | `data/seed/demons/_registry/themes.v1.json` ships **84** themes against **840** species (counted). `theme-refresh` / `theme-enrich` are named as the fix and unbuilt | ⚠ The brief's motif source is 10× too coarse. Mitigable — the anchor's own `traits` and `reason` carry per-species motifs — but say so before the run |
| **An atom-tag vocabulary** | `AffixTags.cs` ships (124 lines, tested) with no production call site; the affix corpus carries exactly **3** semantic tag values | ⚠ **Soft.** D14's property-keyed exclusion can key on posture and little else. Nodes may carry `excludeProps`; the vocabulary can be enriched later without regenerating |
| **The 17th atom kind (D16)** | Real gap: no kind among the 16 writes an element payload, and the failure is silent | ⛔ **Allocate no budget to conversion nodes.** A conversion node would contribute zero forever, with no error |
| **Coefficient resolution** | The shipped field is `PowerLadderKMilli`, an `int` in **per-mille** (`ValueSpec.cs:92`) | Owned by [`tree-binder`](spec-tree-binder.md), which re-derived the error at D29's ten tiers: **tier 1 is +63%, not the 17% this spec used to carry** (that figure was computed at seven tiers). `PowerLadderKMicro` is the fix it proposes. Not a blocker for authoring; a blocker for believing the numbers |
| **A closed `family` roster** | 698 open tokens | §7.3 — this module ships with families excluded from the roster until one exists |

#### 8.1 The gate quantity — this module is not in the ideal's §13.4 position

The ideal's §13.4 finding is real and it is alarming at a glance: **27 of the 39 generic trees have no
gate quantity in code**, so 1,080 of the 1,560 generic nodes would ship authored, reviewed, committed
and permanently at tier 0. Verified again this session: `element_mastery` exists only in comments
(`PointBudget.cs:13,15`, `AptitudeTuning.cs:20`, which says outright that its source *"does not exist
yet"*), and `status_applied` has **zero hits in `src/`**.

**A reader who has just read that number will ask whether the 840 species trees are in the same
position. They are not, and the difference is worth stating precisely rather than asserting.**

| Piece | State, verified 2026-09-05 |
|---|---|
| **The quantity itself — specimen level** | ✅ **Live and persisted.** `rpg_unique_actors.level` (`RpgStore.cs:398`) is written by `AwardUniqueActorXpUnlocked` (`RpgStore.UniqueActors.cs`, by symbol), which levels on `RpgXpCurve.XpToNext(RpgActorKinds.Specimen, level)` and writes the new level back. It reaches the client as `UniqueActorDto.Level` (`UniqueActorDtos.cs:22`) |
| **The scope that prices it** | ✅ Declared and rate-loaded. `AllocationScope.UniqueDemon` (`AptitudeAllocation.cs:8`), its rate row (`AptitudeTuning.cs:204`), and the store's TEXT↔enum round trip (`RpgStore.Aptitudes.cs:58,67`) |
| **The caller that binds the two** | ⛔ **Absent.** Nothing in `src/` passes `AllocationScope.UniqueDemon` to `PointBudget.PointsFor` or `CheckScope` — every call site is `Commander` or `DemonType` (`AptitudeEndpoints.cs:50,95,129,160`, `SpeciesBuildEndpoints.cs:58`, `SpeciesAllocation.cs:35`, `ZombossCommanderAllocation.cs:54`) |

**The position, stated plainly: this is a wiring gap, and it is a different kind of thing from
§13.4's two.** `element_mastery` and `status_applied` have no quantity anywhere — nothing counts them,
nothing stores them, and no amount of wiring produces one; somebody has to design a counter first.
Specimen level is counted, stored and levelled in production today. What is missing is the binding
from that index to an aptitude budget, and its twin already ships: `SpeciesAllocation.cs:35,62` does
exactly this for `DemonType`, including the index transform (`PointBudget.DemonTypeSourceFromLevel`,
`PointBudget.cs:40`) that a `UniqueDemon` source would mirror — *"species level is an index, so it is
`max(0, level − 1)`."*

**So §13.4's finding does not extend to the 840 species trees, and this module is not gated on it.**
Two consequences follow, and they point in opposite directions, so both are stated:

1. **Generating the species corpus early does not strand it** the way generating the elemental and
   status corpora early would. When the binding lands, 33,600 nodes become reachable; they are not
   waiting on a quantity nobody has designed.
2. **The binding should still land before the census.** A reviewer judging 840 tree cards against a
   ladder that reads zero everywhere is judging the writing and not the tree. That is `tree-state`'s
   work, it is small, and it belongs in the build order ahead of §7.2's 33-hour pass.

Whether specimen level is a *sufficient* gate for D26's ladder is a separate question and still
`tree-state`'s call — open question 2, unchanged by any of the above.

---

## Commands

```powershell
# 1. plan - deterministic, model-free, committed, diffable
python -m seedsmith trees species plan --targets data/tuning/passive-tree-targets.v1.json
python -m seedsmith trees species plan --check      # re-derive and compare; exit 1 on drift

# 2. the favour lock - one narrow call per species, every option inside the quota
python -m seedsmith trees species favour --dry-run
python -m seedsmith trees species favour --write --workers 4

# 3. nodes + the codex sentence
python -m seedsmith trees species generate --dry-run                 # the default
python -m seedsmith trees species generate --write --lot <lot> --workers 4
python -m seedsmith trees species run start|pause|resume|status|rerun --pipeline <id>

# 4. gates and review
python -m seedsmith check data/seed/passive-tree --gate
python -m seedsmith trees review --lot <lot> --tier 1                 # tree-review owns this
dotnet run --project tools/PassiveTreeGen -- --check                  # byte-identity
python -m pytest tools/seedsmith/tests/test_species_tree.py
```

`--dry-run` is the default and `--write` must be passed explicitly, for `cli.py:285-289`'s stated
reason: a real run is ~105,800 calls, and *"a flag you must remember to pass to avoid spending them is
a flag someone eventually forgets."*

## Project structure

```text
tools/seedsmith/seedsmith/adapters/trees/species/plan.py        quota -> per-species cell + alternates
tools/seedsmith/seedsmith/adapters/trees/species/prompts.py     the three briefs (favour, node, codex)
tools/seedsmith/seedsmith/adapters/trees/species/schemas.py     the three response schemas, no numbers
tools/seedsmith/seedsmith/adapters/trees/species/roster.py      _index.json, walked WITHOUT the _ skip
tools/seedsmith/seedsmith/metrics/passive_tree.py               FavourDrift, SpeciesUniqueness (+ tree-review's)
data/tuning/passive-tree-targets.v1.json                        quotas, legitimateSkew, thresholds
data/seed/passive-tree/plan/species/<speciesId>.json            THE PLAN - model-free
data/seed/passive-tree/species/<speciesId>.json                 THE SEED - enums + prose + codexSummary
data/generated/passive-tree/species/<speciesId>.json            CONCRETE - coefficients (tools/PassiveTreeGen)
tools/seedsmith/tests/test_species_tree.py
```

**Seedsmith stops at the seed.** The concrete stage is `tools/PassiveTreeGen` in C#, for
[`tree-catalog`](spec-tree-catalog.md) §5's reasons — it must *call* the shipped `PowerLadder` rather
than transcribe it, because *"a Python transcription of them is a second curve by another name."*

## Code style

Match `adapters/actions/distribution_planner/derive.py`: the planner is pure, deterministic, and does
its arithmetic in integers with the repo's widening discipline. It never calls a model.

```python
def assign_favour_cells(species_ids: "list[str]", targets: Mapping[str, Any],
                        forced: "Mapping[str, dict]") -> "dict[str, FavourAssignment]":
    """One mechanical-favour cell per species, plus 2-3 alternates from the SAME quota.

    The lock is NOT the anchor's own elementPrimary/aptitudePrimary (ideal S9's corollary): if
    thematic favour and mechanical lock are one field, thematic truth (plants are earthy, 45% of
    the corpus) becomes mechanical skew (everyone plays earth). This assigns the second field.

    `forced` holds species whose cell is fixed by a hard constraint. A forced species returns its
    DRAWN value to the pool and the pool is re-apportioned over the remainder - skip that and the
    forced species consume their quota twice, once by force and once by draw, and the residual
    species inherit the deficit. That is the original 166x defect wearing a planner's uniform.

    Deterministic: seeded from speciesId, ties broken on ORDER's own declared position, never on
    dict iteration order. Re-running over an unchanged roster is byte-identical.
    """
    order = _cell_order(targets)                       # declared, never a dict's own order
    quota = largest_remainder_count(                   # long, widened, divided by 1000 once, last
        targets["mechanicalFavour"]["weightsMilli"], order, len(species_ids))

    for species_id, cell in forced.items():            # narrow, never widen
        quota[_key(cell)] -= 1
        if quota[_key(cell)] < 0:
            raise ValueError(
                f"{species_id}: forced cell {_key(cell)} overdraws its quota - "
                f"refused, not rebalanced silently")

    free = [s for s in species_ids if s not in forced]
    return _apportion(free, quota, order, forced)      # rebalance over the REMAINING species
```

The refusal on an overdrawn quota is deliberate and matches the shipped rule: *"Nothing here mutates
the draft into legality. A draft that broke a rule is REFUSED with the rule named, because silently
repairing it teaches the next call nothing."*

## Testing strategy

| Test | Asserts |
|---|---|
| `the_roster_is_840_species_read_from_the_index` | §2, mechanically — the count is derived, never typed |
| `a_species_on_disk_but_not_indexed_halts_the_run` | §2.1 rule 1; never "pick the first one" |
| `a_parked_entry_in_an_underscore_file_is_a_finding` | the blind spot is not inherited |
| `the_favour_quota_sums_to_the_species_count` | largest-remainder, exactly, over a skewed fixture |
| `a_forced_cell_returns_its_draw_to_the_pool` | §3.1's step that gets forgotten |
| `an_overdrawn_forced_quota_is_refused_not_rebalanced` | refuse with the rule named |
| `every_alternate_offered_is_inside_the_quota` | **the 166× fix** — no answer can break the target |
| `the_permitted_subset_is_what_reaches_the_schema_enum` | out-of-quota values are unsampleable, not rejected after |
| `an_emitted_corpus_matching_the_target_passes_favour_drift` | and an injected 30% element skew fails it |
| `favour_drift_catches_overshoot_as_well_as_undershoot` | symmetric, per the shipped quota-drift shape |
| `mechanical_favour_is_not_read_from_the_anchor_element` | §4 — the two fields are independent by test |
| `a_species_may_answer_none_of_these_and_it_is_counted` | `unresolved`, never silently the first option |
| `unresolved_above_fifty_permille_stops_the_run` | the one gate promoted to `gates=True` |
| `no_node_name_or_flavor_repeats_across_the_corpus` | U1 |
| `no_affix_composition_fingerprint_repeats_across_trees` | U2 |
| `every_species_tree_meets_speciesUniqueAffixMin` | U3, with the threshold read from tuning |
| `a_species_namespace_affix_referenced_by_two_trees_is_a_finding` | U3's reverse index |
| `species_unique_affix_min_of_zero_is_legal_and_u1_u2_still_gate` | §5.3 rule 2 — the lowest defensible value must not crash the gate |
| `the_marked_nodes_are_the_deepest_mechanism_nodes` | §5.3 rule 3, deterministically, ties on branch order then `nodeKey` |
| `raising_species_unique_affix_min_never_unmarks_a_marked_node` | §5.3 rule 3 — the property that keeps a later ruling an `O(diff)` re-review |
| `every_species_emits_exactly_one_codexSummary` | §6 — no species ships without one |
| `codexSummary_carries_no_number_and_no_channel_id` | the schema audit, at construction time |
| `no_response_schema_contains_a_numeric_field` | `audit_schema`, before a call is made |
| `a_rerun_over_unchanged_seeds_is_byte_identical` | determinism, proven by hash |
| `the_plan_is_reproducible_from_species_id_alone` | model-free, seeded, diffable |

Fixtures are synthetic rosters with a deliberately injected defect — an element at 30%, a forced cell
that overdraws, two trees sharing a fingerprint, a species with no summary. **That is the only way to
prove a check would notice.**

## Boundaries

**Always:** assign the favour cell in the planner, before generation; offer only alternates already
inside the quota; keep the mechanical lock in its own field, separate from the anchor's thematic
fields; read the roster from `_index.json` and walk every file without the `_` skip; emit one
`codexSummary` per species; vote exactly one field and say which; check the emitted distribution
against a declared target, symmetric; resume rather than restart; refuse with the rule named.

**Ask first:** raising `speciesUniqueAffixMin` above 4 (it multiplies the affix-authoring cost by
840 per unit, and the value is the owner's ruling — §5.3 builds to any of them); adding a second
voted field (it moves the call budget by roughly a third of the run); adding a quota axis; shipping a
lot whose census is incomplete; adding family trees to the roster before a closed family taxonomy
exists.

**Never:** choose the species-namespace nodes at generation time rather than in the planner (§5.3
rule 3); recompute a species affix id from a node's position instead of reading the minted one back;
let the language stage pick a favour from an open enum — that is the measured 166× defect,
and permutation and voting do not fix aggregate shape; use the anchor's `elementPrimary` as the
mechanical lock; re-classify anything the demon corpus already decided; write a number in any
response schema; skip the rebalance after a forced override; allocate budget to a conversion node
until a 17th atom kind lands; author a species tree that fails U1 or U2; ship a species without a
Codex sentence; add 840 bloodlines to a browse — a species tree is not a choice, so it needs no
chooser; leave an entry parked in a `_`-prefixed file.

## Success criteria

- [ ] 840 species trees exist, 40 nodes each, and the count is **derived from `_index.json`**, never
      typed into code.
- [ ] The emitted `mechanicalFavour` distribution is inside D32's band on every axis, proven by
      `FavourDrift` against a declared target and re-derived independently.
- [ ] `unresolved` favour is below 50‰, and every unresolved species is named in the review queue.
- [ ] U1, U2 and U3 are all green, with `speciesUniqueAffixMin` read from tuning.
- [ ] Every species carries exactly one `codexSummary`, ≤ 140 chars, with no number in it.
- [ ] No response schema contains a numeric field — proven at `Pipeline.__post_init__`, before a call.
- [ ] The plan regenerates byte-identically from an unchanged roster; so does the concrete catalog
      under `PassiveTreeGen --check`.
- [ ] The 840-tree census completes under [`tree-review`](spec-tree-review.md)'s protocol, and the
      acceptance record says *"every tree was judged"* — not *"the catalog was reviewed."*
- [ ] `PassiveTree/HiddenFileCount` is green over this module's own seed roots.
- [ ] The run resumes cleanly after a deliberate mid-run kill, with no duplicate provenance row.

## Open questions

Three. Everything else in this document is a recommendation nobody has disputed, which makes it a
decision, or an answerable question, which makes it a task.

1. **How many of a species tree's 40 nodes must carry a species-namespace affix (`speciesUniqueAffixMin`)?**
   §5.2 recommends 4 and shows the cost curve: `k` costs 840 × `k` authored affixes against a shipped
   authored corpus of **two**. At 40 it is 33,600 affixes and the module does not ship. It is a
   tunable, so a later change is a file save — but the first run bakes a corpus, and the honest place
   to decide is before it. **§5.3 specifies U3 well enough to build at any value**, so this question
   blocks the first *lot*, never the module.
2. **Does a species tree gate on `UniqueDemon` specimen level, and does that satisfy D26's ladder?**
   `AllocationScope.UniqueDemon` ships (`AptitudeAllocation.cs:8`), and **counted this session it has
   exactly three references in `src/`** — the tuning table's own row (`AptitudeTuning.cs:204`) and the
   store's scope-key round-trip (`RpgStore.Aptitudes.cs:58,67`). **No production code ever saves or
   loads an allocation at that scope**, and `Aspect` is in the identical position
   (`AptitudeTuning.cs:203`, `RpgStore.Aptitudes.cs:57,66`). ✅ **One correction to the ideal while
   verifying this:** §13.2's row *"every caller passes `Commander`"* is stale — `DemonType` **is**
   wired end to end (`SpeciesAllocation.cs:35,62`, `AptitudeEndpoints.cs:85-99`,
   `RpgClient.cs:394`). So two scopes are reached, not one, and the gap is `Aspect` and `UniqueDemon`.
   The tier gate must read **one** index; the ideal's half-closed finding is that specimen levels now
   share the arithmetic curve with aptitude points, so they finally have the same *shape*. Whether
   that is *sufficient* is `tree-state`'s call, not this module's — and this module needs the answer
   before its tier ladder means anything. **§8.1 separates the two halves of this**: the gate
   *quantity* exists and is live, so the species corpus is not in the ideal's §13.4 position; only the
   binding from specimen level to an aptitude budget is unwritten, and that is a wiring gap.
3. **Is `nullification` reachable at all for a species tree?** The generic schema removes it — it is
   the only exclusion form that names a node, and a generated corpus cannot maintain one. A species
   tree is the most plausible place a genuine nullification would arise (a bloodline that *refuses* a
   mechanic is good content). Recommendation: keep it out of the generated schema and reachable only
   through the hand-authored `allow`/`deny` escape hatch the eligibility rule already has. **Needs an
   owner ruling, because it narrows D14's locked ladder.**

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §1 — a species tree is unique, with its own generation pipeline | **D23** |
| §1, §2, §7 — every species gets a full tree, at 840-species scale | **D30** as amended |
| §1, §2 — 40 nodes, 10 tiers × 2 branches, same shape as every other tree | **D10**, **D29** |
| §3 — the favour triple is locked: primary tree + element + status | **D17** |
| §3.1 — deterministic planner → agent inspects → validated against target; never a free choice | **D17**, and §9's measured 166× |
| §3.2 — near-uniform target with a named theme allowance, declared as data | **D32** |
| §4 — thematic favour and mechanical lock are separate fields | ideal §9's corollary, under **D17** |
| §3, §5 — the plan decides; the stage only picks from permitted options | **D13** |
| §5 — nodes no other tree has, made machine-checkable | **D23** |
| §5, §8 — nodes compose from the shipped atom/affix catalog; no passive-specific vocabulary | **D22** |
| §5.1, §7.2 — review is by tree card and census, inherited whole | **D24**, **D30** |
| §2, §6 — the catalog is static, shared and identical for every player | **D24** |
| §6 — one Codex sentence per species; a bloodline is never in a browse | **D21** (every actor carries its own tree), doc 14 §3.3 |
| §7.3 — the roster ships whole; families wait for a closed taxonomy | **D27**, **D9** |
| §8 — no budget for conversion nodes until a 17th atom kind lands | **D16** |
| §8 — property-keyed exclusion, keyed on properties the plan named | **D14** |
| Open question 2 — the status axis of the favour triple is content, not a gate | **D35** (replacing D19/D31) |

**Belongs to a sibling module, not here:** D1–D8, D11, D12, D18, D25, D26, D28, D33, D34, D36 —
`tree-plan` (the ladder and the archetypes), `tree-catalog` (the record and id stability), `tree-state`
(the per-actor store, unlock cost, respec), `tree-resolve` (`F`, cross-unlock, every `P(Θ)` multiply),
`tree-surface` (the actor sheet and the Codex render), `squad-harness` (D33). **D19, D20 and D31 are
superseded** — by D35, D26 and D35 — and are implemented nowhere, by design.
