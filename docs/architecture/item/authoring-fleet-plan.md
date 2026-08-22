# The authoring fleet — plan for building the item seed corpus

**Status:** Proposed 2026-08-22, revised the same day to **Diablo-2 scale** after a lane-coverage audit found
nine missing partitions. A **build plan for data**, dependent on [seed-contract.md](seed-contract.md)
being agreed first. No authoring is authorized yet.

Target: **~2 400 authored entries expanding to ~30 000 rows** — the scale of the ARPGs this design draws
on, rather than a demo corpus.

---

## 1. The principle: partition so collision is impossible

The failure mode of a large parallel authoring effort is not bad writing. It is **two authors producing
the same thing, or subtly different versions of the same thing**, in files neither can see.

Three devices prevent it, in order of importance:

1. **One authored unit per file, one file per agent.** Two agents never touch the same bytes. Merge
   conflicts become structurally impossible rather than manageable.
2. **Disjoint reserved vocabulary.** Each partition receives a name-word pool nobody else holds. This is
   what stops thematic collision, and it must be handed *down* from the foundation wave — it cannot be
   negotiated between peers who cannot see each other.
3. **Deterministic post-validation.** Id and name-key uniqueness across the whole corpus is a script,
   not a judgement call.

**Never ask an agent to "author the equipment items".** Ask it to author *one role, one frame, one class
band, twelve identities, from this word pool*. Small, bounded, checkable.

---

## 2. Coverage audit — every lane, and what it needs authored

The first draft of this plan listed nine partitions and missed nine more. Full audit:

| Lane | Authored content | Partition |
|---|---|---|
| I1 rarity | 10 rungs: names, colours, pips, count bands, tier windows | **W0** — everything references it |
| I2 equip slots | 15 roles × 2 frame vocabularies, budget weights | **W0** |
| I3 categories | Category taxonomy (10) | **W0** |
| I3 base types | 750 identities across 30 role-frames | W1 · 60 agents |
| I4 sockets — gems | Insert definitions | W1 · 3 agents |
| I4 sockets — words | Named ordered combinations | W1 · 1 agent |
| I4 sockets — resonances | **None — 25 rows are rule-generated** | — |
| I5 sets | Membership, thresholds, **combination bonuses** | W1 · 5 agents |
| I6 enhancement | Milestone atoms + risk/cost curves | W1 · 1 agent |
| I7 reroll | Operation definitions + cost curves | W1 · folded into curves |
| I8 affixes | ~100 families, tier anchors, name words | W1 · 15 agents |
| I9 materials | 21 material ids | W1 · 1 agent |
| I9 recipes | Craft, salvage, enhance shapes | W1 · 1 agent |
| I10 **charms** | **Missed entirely in the first draft** — own base types, AP costs, resonances | W1 · 3 agents |
| I11 requirements | Attribute definitions — **gated on the five-or-none decision** | W1 · 1 agent, conditional |
| I12 generation | Drop tables | W1 · 4 agents |
| I13 inventory | Loot-filter defaults only | W1 · folded |
| G1 uniques | 300 hand-authored items | W1 · 20 agents |
| G2 consumables | ~60 definitions | W1 · 3 agents |
| G3 presentation | ~110 display templates | W1 · 6 agents |
| G4 granted actions | **v1 is zero rows** — gated on the action program | — |
| *(cross-cutting)* | **The curve table** — every balance curve in one place | W1 · 1 agent |
| *(cross-cutting)* | **Locale strings** — see §6, not a partition | — |

**What was missing:** rarity, roles, categories, charms, enhancement milestones, reroll definitions, the
curve table, attributes, and the locale-string question. Charms was the real omission — a whole item
category with its own combination mechanic.

**What is correctly absent:** socket resonances (rule-generated), granted actions (zero rows at v1),
quest items (no consumer, deliberately), and the role×family legality matrix (derived from each family's
declared role groups rather than authored — worth ~1 100 cells).

---

## 3. Three waves

### Wave 0 — foundation (must complete before any volume authoring)

Produces the constraints every later agent obeys. **Design work, not data entry**, and its quality
multiplies across the entire corpus.

| Agent | Produces | Model |
|---|---|---|
| F1 | **Naming conventions and reserved word pools**, partitioned per role × frame so no two volume agents can collide | Opus |
| F2 | **The tag vocabulary registry** — closed and validated. Free-text tags rot into `heavy`/`weighty`/`bulky` | Sonnet |
| F3 | **Theme registry** — ~15 themes for uniques, sets and charms, with element and frame affinity | Sonnet |
| F4 | **Class ladders per frame** — cloth/leather/plate against fibre/bark/heartwood, with the implicit slate legal to each role | Sonnet |
| F5 | **Worked exemplars** — four files chosen to cover the *hardest* schema variations, not four nice examples: one simple base type, one entry with multiple cross-references, one edge case, and one **rule-breaking unique** | Opus |
| F6 | **The registries**: rarity ladder, role and frame vocabularies, category taxonomy | Sonnet |
| F7 | **The band enums and curve mapping** — `powerBand`, `costBand`, `dropBand`, `variance`, and what each resolves to per channel family. This is what makes seed-contract §3 executable | Sonnet |
| F8 | **Naming grammar and id-namespace allocation** — legal name patterns, collision normalization, one namespace prefix per partition | Sonnet |
| **V1** | **The deterministic validator.** A code task, not an authoring task | Opus |

**Wave 0 is not one parallel batch — it is three layers.** An exemplar must use real registry values, and
word pools need the naming grammar and the role list. Correcting this after dispatch would have meant
re-running F1 and F5 against registries they had guessed at.

| Layer | Agents | Why it waits |
|---|---|---|
| **0a** | F2 tags · F3 themes · F4 class ladders · F6 registries · F7 bands · F8 naming grammar | depend on nothing but the lane documents |
| **0b** | **F1** word pools · **V1** validator | F1 needs F8's grammar and F6's roles; V1 needs real registry files to validate against |
| **0c** | **F5** exemplars | must use frozen values from every registry above |

**Nine agents, and one of them writes code.** Two further changes from the first draft, both from the
fan-out audit:

**F5 matters more than it looks** — a worked exemplar is worth more than a page of instructions to a
small model, and it is the cheapest quality lever here. But four *nice* examples teach the easy case. The
exemplars must cover the variations that actually go wrong, and uniques especially: that lane exists to
break rules interestingly, which a perfect well-behaved example cannot teach.

**V1 is a prerequisite, not a wave-2 deliverable.** If the pilot is a test of the contract (§7.2), then
the mechanical check has to exist when the pilot runs. A contract test with no validator is an opinion.
Nobody owned building it in the first draft — it is code, so it was invisible to a plan made of
authoring partitions.

### Wave 1 — volume authoring, Diablo-2 scale, in THREE stages

**Wave 1 splits**, because pure independence was not achievable as first written. Uniques name a base
type; drop tables name base types and uniques; sets name roles. A partition cannot reference a peer that
is still being written.

**Two stages was still wrong, and the pilot proved it.** A gem references the affix family it grants,
and both sat in 1a — so a gem cited `atom.hit-followthrough` while the affix partition that mints it was
mid-write, and a re-run of that partition renamed the family out from under it. Base types have the same
shape of dependency through their implicits; they escaped only because they happened to reference
*shipped* families rather than newly minted ones.

The real dependency order has three levels, so:

| Stage | Contains | References |
|---|---|---|
| **1a — define** | affix families · materials · curves | nothing authored |
| **1b — build** | base types · gems | 1a only |
| **1c — compose** | uniques · sets · charms · socket words · recipes · enhancement milestones · consumables · drop tables · display templates | 1a and 1b |

Nothing inside a stage references anything else inside it. Two synchronisation points, both cheap
relative to what a dangling reference costs to find later.

| Stage | Partition | Agents | Each authors | Model |
|---|---|---|---|---|
| **1b** | Base types — 30 role-frames × 2 class bands | **60** | ~12 identities | Haiku 4.5 |
| **1b** | Base types — the commander `standard` role, 2 frames, single band | **2** | ~10 identities | Haiku 4.5 |
| **1a** | Affix families — by the 15 affix groups | **15** | ~7 families | Sonnet |
| **1b** | Gems | **3** | ~20 each | Haiku 4.5 |
| **1a** | Materials | **1** | 21 | Haiku 4.5 |
| **1a** | Curves (enhancement, reroll, salvage, drop) | **1** | ~25 | Sonnet |
| ~~1a~~ | ~~Attributes~~ | **0** | — | **deferred — see build log** |
| — | *1a frozen and validated before 1b starts; 1b before 1c* | | | |
| **1c** | Uniques — by theme | **20** | ~15 uniques | Sonnet |
| **1c** | Sets — by theme | **5** | ~6 sets | Sonnet |
| **1c** | Charms — by axis | **3** | ~20 charms | Sonnet |
| **1c** | Socket words | **1** | ~25 | Sonnet |
| **1c** | Recipes | **1** | ~30 | Haiku 4.5 |
| **1c** | Enhancement milestones | **1** | ~10 | Haiku 4.5 |
| **1c** | Consumables | **3** | ~20 each | Haiku 4.5 |
| **1c** | Drop tables | **4** | ~10 each | Haiku 4.5 |
| **1c** | Display templates | **6** | ~18 families | Haiku 4.5 |

**125 agents: 79 Haiku 4.5, 46 Sonnet.** 81 in stage 1a, 44 in stage 1b.

Both stages are fully parallel *within* themselves. The stage boundary is the only synchronisation point
in the whole build, and it exists for exactly one reason: a reference must resolve against frozen
content, never against a peer mid-write.

**Base types split two ways per role-frame** rather than one agent taking 25. Quality degrades with
batch size well before context does; past roughly 15 identities an agent starts repeating itself. Split
the partition, never stretch the agent.

The Haiku/Sonnet line is one test: **does this partition invent vocabulary, or consume it?** Consumers
get Haiku. Inventors get Sonnet.

### Wave 2 — verification

**Script first.** Most of what a review agent would do is deterministic, and a validator does it better,
faster and repeatably:

| Deterministic — a script | Semantic — an agent |
|---|---|
| JSON schema validation per `schemaVersion` | Thematic drift between partitions |
| Id grammar and **global uniqueness** | Names technically unique but reading identically |
| `nameKey` uniqueness | Flavour quality spot-checks |
| Tag-vocabulary conformance | Role-fit sanity — *does this affix belong on boots?* |
| **Computed field present in a seed file → reject** | Tone consistency across the two frames |
| Cross-reference resolution | Whether a theme actually reads as that theme |
| Tier-gap and band-copied lints (specified by E14a) | |

**1 script + 8 Sonnet reviewers**, each reviewing *across* partitions rather than within one — the
collisions worth catching are exactly the ones no single partition can see.

---

## 4. Fleet total

| Wave | Agents | Model mix |
|---|---|---|
| 0 — foundation **and the validator** | 9 | 3 Opus, 6 Sonnet |
| 1a — defining partitions | 82 | 66 Haiku 4.5, 16 Sonnet |
| 1b — referencing partitions | 44 | 15 Haiku 4.5, 29 Sonnet |
| 2 — verification | 8 | 8 Sonnet |
| **Total** | **143** | **81 Haiku · 59 Sonnet · 3 Opus** |

*(1a: 60 base-type + 2 commander-standard + 15 affix + 3 gem + 1 material + 1 curve. Attributes deferred;
the commander `standard` role was added in wave 0b after cross-checking the registries against each
other — it exists in `core.v1.json` and had neither vocabulary nor a partition.)*

**The gate costs real time, and that should be said plainly.** Wave 0 grew from 6 agents to 9 including a
code task, and the freeze checklist is ~18 artifacts rather than 4 loose notes. That is a genuine delay
before a single item is authored. It is worth paying, for one reason: **the danger was never the 125
agents — it was letting 125 agents discover the contract for you.**

Corpus this produces:

| Content | Authored | Generated rows |
|---|---|---|
| Base types | 750 | ~3 000 containers |
| Affix families | ~100 | ~900 atoms + pool rows |
| Uniques | 300 | ~3 000 |
| Sets | 30 | ~120 tiers |
| Charms | 60 | ~240 |
| Gems and words | 85 | ~400 |
| Everything else | ~200 | ~1 000 |
| **Total** | **~2 400** | **~30 000** |

---

## 5. Is Haiku 4.5 enough?

For 79 of 125 volume partitions, yes — and those are the genuinely repetitive ones.

**Haiku is enough when** the vocabulary is fixed, the schema is strict, an exemplar exists, and the
output is one small file. Base types, gems, materials, recipes, drop tables, display templates,
enhancement milestones.

**Haiku is not enough when** the agent invents vocabulary, weighs thematic fit, or holds several
documents in tension. Affix families carry tier anchors and role legality the whole corpus inherits.
Uniques exist to break rules *interestingly*, the opposite of pattern-following. Sets and charms need
coherence across members. Socket words are 25 pieces of pure invention. Curves are balance judgement
wearing a numeric costume.

**The risk with a 79-agent Haiku fleet is not errors — the validator catches those. It is sameness.**
Individually correct, collectively monotonous. Three defences: wave 0's disjoint word pools, a
per-partition variation directive, and reviewers told specifically to hunt for it.

---

## 6. The locale-string question

Every entry carries a `nameKey`, never a display string. So who writes the strings?

**Not a separate partition.** The authoring agent emits key *and* string together in its own file, and a
script extracts the string table. A dedicated translation partition would need every other partition
finished first, and would ask an agent to name 2 400 things it never designed.

The contract needs one line saying so, because it currently implies keys without saying where strings
live.

---

## 7. Sequencing, the freeze gate, and the pilot

```text
Wave 0 (9)  ──►  FREEZE GATE  ──►  pilot (5)  ──►  contract test  ──►  [revise + re-pilot]
                                                          │
                                                          ▼  clean
                    stage 1a (81)  ──►  validate + freeze  ──►  stage 1b (44)
                                                                      │
                                                        script  ──►  Wave 2 (8)  ──►  fix pass
```

### 7.1 The freeze gate

**Anything that can change the shape, meaning, ownership, or validation of an authored file must be
frozen before fan-out.** Not agreed — *frozen*, with a version number.

```
[ ] Seed schema v1, machine-checkable          [ ] Id namespace allocation
[ ] Field ownership matrix (4 levels)          [ ] Naming grammar + collision normalization
[ ] Content scope: what ships, what is zero    [ ] Band enums + curve mapping
[ ] Rarity registry v1                         [ ] Cross-reference rules (1a/1b staging)
[ ] Role + frame registry v1                   [ ] Locale / nameKey rules
[ ] Tag vocabulary v1                          [ ] Deletion + id-retirement rules
[ ] Theme registry v1                          [ ] Rerun + batch-ownership rules
[ ] Class ladders v1                           [ ] Provenance fields (contract, registry,
[ ] Reserved word pools, per partition             exemplar, prompt, model versions)
[ ] Exemplars v1 (hardest cases)               [ ] The validator, running
[ ] Pilot acceptance criteria
```

**Registry lifecycle:** `DRAFT → VALIDATED → FROZEN v1 → CONSUMED`. Once a partition has consumed a
registry, **that registry does not change.** If a change proves unavoidable it is `v2`, with an explicit
decision on which partitions must re-run — because a silently edited registry splits the corpus into
pre-edit and post-edit semantics, and nothing downstream can tell which file used which.

### 7.2 The pilot is a test of the contract, not of the authors

This is the single most important structural rule here, and the first draft got it wrong by treating the
pilot as a quality review.

The pilot asks: **can five independent agents produce conforming files with zero contract
interpretation?** Not: are these five outputs good?

Every problem found is classified:

| Class | Response |
|---|---|
| **Authoring error** | re-run that agent |
| **Model quality** | re-run, or move that partition up a model tier |
| **Contract ambiguity** | 🛑 **stop** — fix the contract, re-pilot |
| **Contract missing** | 🛑 **stop** — fix the contract, re-pilot |
| **Registry error** | 🛑 **stop** — registry v2, re-pilot |
| **Exemplar error** | 🛑 **stop** — fix the exemplar, re-pilot |
| **Validator gap** | 🛑 **stop** — fix the validator, re-pilot |

**Five of seven classes stop the fleet.** Never hand-fix a pilot file and proceed: a hand-fix converts a
contract defect into an invisible one, and 120 agents then reproduce it at scale. The pilot's whole
purpose is to fail cheaply.

### 7.3 Dispatch notes

- **A pilot batch of 5 before the other 120.** A systematic misreading caught after 5 agents costs 5
  agents; caught after 125 it costs the corpus.
- Pick the five to span the hard cases — at least one Haiku partition, one Sonnet partition, and one that
  makes cross-references.
- ⚠ **Every brief must carry the owner's scope decisions, not just the source documents.** Learned in
  wave 0a: an agent briefed only on the lane SSOTs correctly applied the content-budget recommendation
  and marked `insert`, `charm` and `consumable` as declare-only — content the owner had since decided
  ships. The lane documents are a snapshot of the design round, and **an agent reading only them will
  faithfully reproduce every decision made since then in reverse.** The scope decision belongs in the
  brief, and in the registry the brief points at.
- Every agent stamps `_meta.batch`, so a bad batch is findable and re-runnable without suspecting
  everything else.
- Agents write only under their own partition path. No agent may edit the contract, the registries, or
  another partition.
- Wave 1 is **fully parallel and independent** — no agent reads another's output, and none needs to.
  That property is what this whole plan exists to buy.

---

## 8. Orchestration — keeping the driving session alive

**This plan is useless if dispatching it exhausts the session that dispatches it.** 139 agents each
returning a paragraph is ~80 000 tokens of notification before the orchestrator writes a word, and
unlike a design round **none of it is worth reading** — the file on disk is the deliverable, not the
agent's account of writing it.

Three rules, in order of impact:

### 8.1 Agents return a token, not a report

Every wave-1 agent's final message is **exactly** one line:

```text
OK <partition-id> <entries-written> <file-path>
```

No summary, no narration, no findings. If the agent has a genuine blocker it writes
`BLOCKED <partition-id> <one sentence>` and stops. This is the single highest-leverage rule here, and it
works under any dispatch mechanism.

The design round did the opposite on purpose — those agents were producing *judgement*, and the summary
was the product. These agents produce *files*, and a summary is pure overhead.

### 8.2 The validation script is the reporting channel

The orchestrator learns whether the corpus is good from **one script run**, not from 139 accounts. Id
collisions, missing fields, tag violations and cross-reference failures are all things a validator
reports better than any agent can, in one table, once.

So the reporting shape is: agents write silently → script validates → orchestrator reads **one** report
→ a fix pass targets only the named partitions.

### 8.3 Dispatch through a workflow, not 139 individual calls

Individual agent calls each notify the caller on completion. A workflow script fans out deterministically
and returns **one** result, which is precisely the difference between a session that survives the build
and one that does not. It also lets the pilot gate be mechanical — run 5, check, fan out the rest —
rather than a thing the orchestrator has to remember to do.

*(This session carries a "keep workflows under 15 agents" guideline. A 125-agent data build is exactly
the case that guideline says to override, and the setting can be raised in `/config`.)*

### 8.4 Run the build in a fresh session

The design history is on disk. A build session needs [seed-contract.md](seed-contract.md), this plan, and
the wave-0 registries — not the twenty-four documents that produced them. Starting fresh costs nothing
and removes the largest single context load before the first agent is dispatched.

---

## 9. What must be settled before dispatch

1. ~~The content cut conflicts with this scale.~~ **Settled by the owner 2026-08-22: Diablo-2 scale.**
   The content-budget decision (`decision-d4-content-budget.md`) had recommended ~880 cells with sockets
   and charms at zero; that is overruled and both ship. **One part of it survives and should still be
   taken:** the cuts that are free arithmetic rather than scope reduction — chiefly deriving the
   role×family legality matrix instead of authoring ~1 100 cells twice. Scope was the disagreement;
   duplicated work was not.
2. **The attribute partition is conditional** on the five-or-none decision — still open.
3. **The importer does not exist** (E14a unbuilt), so nothing can import these files yet. Authoring ahead
   of it is legitimate — the files are the deliverable — but format churn would land on a corpus rather
   than on four exemplars. **Agreeing the contract first is what makes that risk acceptable.**
4. **Two contract questions bite authors directly**: whether `affixClass` is authored or derived, and who
   owns the tag vocabulary.
