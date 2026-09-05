# Seedsmith — capability map

**Status (2026-09-01):** Map approved 2026-08-23. **Feature 1 (core, W1-W3) BUILT** — 395 tests green, `seed_graph` absorbed and deleted. **Feature 2 (demons, D1-D4) BUILT** — 84-species corpus emitted, families/motifs/themes generated from a real model run. **Feature 3 (generation runtime, G0-G4) SPECCED AND SEALED**, not built — see §3d.

A Python application that owns the health of every seed corpus in the repo: it validates what is
there, measures what is missing or lopsided, and emits a **deterministically-planned** work order
for the LLM pipelines that fill the gaps. Items are the first feature; the core is feature-agnostic
by construction, because the second feature must not rewrite it.

> **Name.** `seedsmith` — decided. `seed-contract.md` §1 keeps *generator* for the thing that
> expands authored bands into ~30,000 database rows.

---

## 1. Why, in one paragraph of evidence

The item corpus reached 1,438 entries, 0 referential errors and 0 reachability gaps — while nine of
its 126 allocated partitions were **empty**, and nobody noticed for three waves. Two validators were
green because neither was asked "is there *enough*, and is it *evenly spread*?" Separately, every
gap that actually closed in that session closed by **script** — 180 set members, 144 acquisition
rows, 740 enhance tracks — in seconds, while the one lane that used agents ran three times because a
constraint lived in a document instead of in code.

Seedsmith is those two observations made permanent: **measure coverage and balance as first-class
properties, and let deterministic code do everything deterministic code can do.**

---

## 2. Principles

**P1 — The LLM writes identity; deterministic code writes magnitude.**
A model names a thing, gives it flavour, and chooses which concept it embodies. It never chooses a
number. This is `seed-contract.md` §3's no-numbers rule generalised from human authors to
pipelines, and the reason is unchanged: a model has no calibrated sense of scale, so a number it
picks is a plausible-looking guess that survives review because nothing looks wrong with it. Base
stats, tier magnitudes, drop weights, costs and curve points are resolved by `numerics` from bands,
role budget weights and the rarity ladder — never generated, never reviewed, never argued about.

**P2 — A metric without a declared target is an opinion.**
"Too few uniques" means nothing until something states how many there should be. The expected count
for uniques currently exists in three documents and disagrees three ways (20, 300, 144). `budget`
makes the target a single declarative artifact so every metric is *actual vs declared* and every
disagreement is a diff.

**P3 — Every metric declares whether it can verify its own fix.**
*Closed-loop*: "60 consumables have no flavour" — detectable, and the fix is verifiable, because the
field is populated or it is not. *Open-loop*: "the flavour is generic" — detectable, not verifiable
by machine. Open-loop metrics produce a review queue, never a pass. Without this split you get green
dashboards over prose nobody read.

**P4 — The plan is deterministic.**
Findings → work order is a pure function: which partitions to author, in what dependency order, at
what model tier, with which constraints. No model decides what work to do. This is the direct
lesson of the unique lane, where the expensive failures were all planning failures that a model
faithfully executed.

**P5 — Feature knowledge lives in adapters.**
The core knows about corpora, budgets, metrics, findings and plans. It knows nothing about roles,
frames, rung bands or drop tables. Everything item-shaped lives in `adapter-items`.

---

## 3. Modules

| id | Capability | Depends on |
|---|---|---|
| `corpus` | Load a seed folder into a typed, queryable graph; ids, kinds, partitions, edges | — |
| `numerics` | Deterministic value resolution: band → number, base stats, curve evaluation, budget-weight math. **P1 lives here.** | `corpus`, `adapter` |
| `budget` | Declarative targets — expected counts and distributions per kind, role, frame, band, element | `corpus`, `adapter` |
| `metrics` | The check catalogue: coverage, linkage, distribution, balance. Emits typed findings with severity and loop-kind | `corpus`, `budget`, `numerics` |
| `report` | Human CLI, CI gate, machine-readable findings for the planner, **deterministic sampling** for open-loop verdicts | `metrics` |
| `planner` | **Deterministic** findings → work order: partitions, ordering, model tier, constraints. Refuses provably-unsatisfiable orders | `metrics`, `budget` |
| `briefkit` | Work order → per-partition briefs, generated from allocation + budget + constraints, never transcribed | `planner`, `adapter-*` |
| `pipeline` | LLM execution: structured output schemas, guardrails, validate-before-accept, bounded retry | `briefkit`, `metrics` |
| `adapter-items` | All item-specific knowledge: kinds, registries, entry shapes, role/frame/band model | `corpus` |

### 3b. Feature 2 — demons (proposed 2026-08-31)

Ideal: [seedsmith-demons-ideal.md](seedsmith-demons-ideal.md), including its §6 adversarial audit —
the `A#` references below are that audit's findings, and three of them changed this module set.

**This is the feature §1 was built for:** *"Items are the first feature; the core is feature-agnostic
by construction, because the second feature must not rewrite it."* Nothing below adds a planner, a
briefkit or a pipeline. It adds an adapter, the pipelines that fill it, and two metric families.

| id | Capability | Depends on |
|---|---|---|
| `demon-corpus-emit` | **C# dev-tool** — `DemonSpeciesCatalog` + `almanac_seed` + `recipes` → `data/seed/demons/*.json`, committed. C# because it reads SQLite and SQL belongs in `FusionRpg.Data` | — (outside seedsmith) |
| `adapter-demons` | The `SeedAdapter`: kinds, registries, legality, **per-kind motif expression rules** (A1), and a deliberately empty `channels()` (A4) | `corpus`, `demon-corpus-emit` |
| `family-extract` | LLM stage A — candidate family labels from name + description, each recording its `basis` | `adapter-demons`, `pipeline` |
| `family-consolidate` | Candidate labels → the append-only family vocabulary. **Deterministic, or committed-and-deliberate** (A6) | `family-extract` |
| `motif-derive` | Motifs + anti-motifs per demon. Pure derivation, carrying `basis` | `family-consolidate` |
| `demon-metrics` | Per-demon coverage (A5) + motif-sharing that **excludes tautological both-`basis=name` pairs** (A2) | `metrics`, `motif-derive` |
| `demon-themes` | Demons become **themes** the items and action corpora consume (A3) | `motif-derive` |

**Spec audit:** [review/audit-demons-specs.md](seedsmith/review/audit-demons-specs.md) — 8 findings,
3 blockers and 1 contradiction, all applied to the specs below.

**Module specs** (written 2026-08-31, **approved by the owner 2026-08-31 and BUILT** — D1-D4 all
complete, CP-D4 reached): [demon-corpus-emit](seedsmith/spec-demon-corpus-emit.md) ·
[adapter-demons](seedsmith/spec-adapter-demons.md) ·
[family-extract](seedsmith/spec-family-extract.md) ·
[family-consolidate](seedsmith/spec-family-consolidate.md) ·
[motif-derive](seedsmith/spec-motif-derive.md) ·
[demon-metrics](seedsmith/spec-demon-metrics.md) ·
[demon-themes](seedsmith/spec-demon-themes.md)

**Why `family-extract` and `family-consolidate` are two modules and not one:** their determinism
differs. Extraction is a model call — non-deterministic, therefore recorded and content-addressed.
Consolidation decides the taxonomy every other module inherits, so it must be reproducible.
Collapsing them hides a non-deterministic step inside a deterministic-looking artifact (A6).

**Build order.** `D1` foundation: `demon-corpus-emit` → `adapter-demons`. `D2` taxonomy:
`family-extract` → `family-consolidate` → `motif-derive`. `D3` measurement: `demon-metrics` —
**gates D4**, because without A2/A5 there is no way to tell whether the taxonomy is real structure or
a tautology. `D4` consumption: `demon-themes`.

**D1 has standalone value:** it makes demons queryable by every metric seedsmith already has, with
**zero model calls** — the same property that made W1 worth shipping alone.

### ⛔ Cross-program dependency — `aspect-scope` (audit S2)

D2's `aspect` kind depends on **`aspect-scope` being built**, not merely approved. It was approved
2026-08-31, but the tier does not exist in code: `DemonSpeciesDef` still carries `ElementPrimary`,
`ElementSecondary` and `TraitPool` on the *species*.

**Owner: the demon program**, whose queue this feature does not control. Recorded here as a first-
class dependency rather than a footnote in one module's open questions, because a dependency on
another program's unscheduled work is the kind that surfaces late and at the worst moment.

**Declaring the `aspect` kind in D1 is harmless; generating into it before the tier exists is not.**

**Owner decision 2026-08-31: the demon program builds `aspect-scope` first, and D2's aspect
generation waits for it.** D2's other kinds are not blocked — only aspect generation is. This makes
the sequencing explicit rather than leaving D2 to discover the missing tier mid-build.

### Roster decisions — the species cap is gone (owner, 2026-08-31)

`DemonSpeciesGenerator` had a hard cap of **24** species. It is **removed**: `Generate` now takes
`int? maxSpecies = null` meaning *no limit*, so every captured species becomes a demon and a PVZ
update that adds almanac entries adds demons with no code change. Full reasoning, including why the
caps register's original "no conflict" verdict was wrong, is in
[`ssot-power-scale.md`](power/ssot-power-scale.md) §11.10a.

Three consequences this feature must carry:

| Consequence | Where it lands |
|---|---|
| `n` is a **measurement**, not a fixed design point — 84 eligible rows today, rising toward ~904 | `spec-demon-metrics` §2.2a; every sharing figure reports `demonCount` |
| **Rarity is a snapshot, not an attribute.** `RarityForRank` is proportional in `count`, so a growing roster moves demons between tiers at unchanged rank (Common → Epic at rank 20 as `count` goes 24 → 904) | `spec-demon-themes` §2.4a; `spec-demon-corpus-emit` §9 Q1 |
| **Membership churn is milder** — nothing is evicted by a better-ranked rival any more; a demon leaves only if the game drops its type | `spec-demon-themes` §2.4a |

⚠️ **Open, owner's call, not a defect:** `RarityForRank` grants Legendary to `rank < 2` — absolute,
while Epic and Rare are proportional. On a 900-demon roster that is two legendaries in the world.
Whether that is the intent or an unscaled constant is a balance decision, and changing it moves the
committed catalog.

✅ **The catalog is regenerated: 24 → 84 species** (2026-08-31, from
`dist/FusionRpg.Server/data`, 907 captured type rows → 84 usable). `Validate()` passes; the split is
2 legendary / 14 epic / 21 rare / 47 common, 14 light / 14 dark, 2 hypno, 2 capture-only.

Regenerating required fixing `tools/DemonCatalogGen`, which **could not run at all**: it never called
`DerivedStatPolicy.Configure`, so `RpgStore`'s static ctor threw (tunables-ssot T5 — no built-in
defaults). The catalog had quietly stopped being regenerable.

It also moved one golden legitimately: `ExpeditionResolverTests.Tier_goldens_are_locked`, because
`ExpeditionResolver.WildBand` picks wild enemies from `DemonSpeciesCatalog.All` and indexes by
`rng.NextInt(band.Count)` — a bigger roster rolls a different enemy. Re-blessed with that reason
recorded; the resolver's own determinism tests stayed green unchanged, proving it was selection and
not a math break.

Rarity itself also changed: `RarityForRank`'s legendary tier was a flat `rank < 2` and is now
proportional like the others (**owner, 2026-08-31**) — see `ssot-power-scale.md` §11.10a. At 84
species the split is 7 / 14 / 21 / 42.

### D5 — `power-estimate`: the roster's missing power signal (owner decision 2026-08-31)

**The constraint on seeding all ~900 species is not text — it is observed HP.** Measured
2026-08-31: **100%** of the 84 eligible species have flavour text (889 of 904 almanac rows overall),
but only **84 of 904** have `hp_base > 0`. `almanac_seed.hp` does not close the gap (82 rows — it
mirrors observed stats), and sun cost is no fallback either (`cost_status` is `absent` for 815 of
904). So the almanac gives names and flavour for ~900 species and **no power signal for 820 of them**,
while `RarityForRank` ranks by observed HP.

**Decision: an LLM estimates a power tier from the almanac text, recorded with `basis` and marked
provisional.**

| Property | Rule |
|---|---|
| Input | the species' own name and flavour text — the same corpus `family-extract` reads |
| Output | a **tier**, never a number (`audit_schema` rejects a numeric field mechanically) |
| Honesty | carries `basis` exactly as family labels do, and `blocked` is a legal answer |
| Lifetime | **provisional** — superseded the moment that species is actually observed, never competing with a real measurement |

**Why provisional is the load-bearing half.** An estimated tier that outlives its evidence becomes
indistinguishable from a measured one, and rarity silently stops meaning "observed power". Marking it
means capture coverage *improving* is what retires the estimate — the estimate degrades gracefully
instead of hardening into fact.

Rejected: ranking by `type_id`/almanac order (deterministic and model-free, but unlock order is not
power, and nothing later corrects it), and shipping all 820 as Common (honest, but rarity would stop
distinguishing anything for species that rarely spawn — possibly forever).

⚠️ **Not yet specced.** This is a recorded decision and a module slot, not a spec. It also depends on
a `provisional` marker existing on the species side, which is demon-program code — the same
cross-program shape as `aspect-scope` above.

### Scope of the "no core change" claim (audit S8)

`spec-adapter-demons` §1 sets the criterion *"not one line of core code changed"*. **That holds for
D1 only.** `demon-metrics` (D3) adds two files under `metrics/`, and `demon-themes` (D4) edits
`adapters/items/registries.py`. Both are justified in their own specs — the metrics are genuinely
generic, and the items change adds a *vocabulary* rather than a concept — but the claim is a D1
property and should not be carried across the feature by implication.

**Scope decisions taken 2026-08-31:**

- **`environment` ships as a `KindSpec` but nothing generates into it in v1.** With no world host,
  `sector:` bindings are rejected, so generated environment content would be flavour nothing reads —
  and coverage would report those partitions "covered", making the feature look more finished than it
  is (A7). The kind costs nothing and keeps the adapter shape stable for when the world host arrives.
- **`provenance-supersede` is core backlog, not this feature.** Re-derivation that supersedes rather
  than duplicates (A8) is cross-cutting — items hit the same wall the first time anything regenerates
  — and burying a general fix inside a demons module is how it becomes demon-shaped by accident.
  Tracked below in §3c.
- **`lore-enrich` is deferred**, named rather than scheduled: it is what turns `basis = name` into
  `basis = text`, and it depends on `provenance-supersede`.

### 3c. Core backlog surfaced by feature 2

| id | Capability | Why it is core, not demons |
|---|---|---|
| `provenance-supersede` | A re-derivation path that supersedes a prior generation instead of duplicating it. `ProvenanceLedger.record` currently **raises** on a re-recorded row — deliberately, since a second write is how idempotence fails — but regeneration after better input is a legitimate second write | Any corpus that regenerates hits this. Items will hit it first in practice |

**Dependency direction** is strictly downward in that table; nothing depends on `pipeline`.

### 3c-bis. `frame-classify` — requested by the item program (owner decision 2026-09-03)

**Proposed, not built.** Recorded here rather than only in the consumer's map, because a cross-program
ask that lives in one document surfaces late — the same reason `aspect-scope` is a first-class row in
§3b instead of a footnote.

| id | Capability | Model? | Depends on |
|---|---|---|---|
| `frame-classify` | LLM stage — each species' **body frame**: `humanoid` \| `plant` \| `hybrid`, from name + flavour text, carrying `basis`. Published through the theme registry | **yes** | `adapter-demons`, `pipeline` |

**Why it belongs to this feature and not to items.** A frame describes a *body*, so it is species data
— the same reasoning that sent per-species aptitude vectors to the demon program
([item-ideal.md](item-ideal.md) D19). And it is **the shape this pipeline already produces**:
`family-extract` and `motif-derive` both read a species' own text and return a judgement about what it
is. Frame is one more such judgement, with the same honesty contract — a `basis` field, `blocked` a
legal answer, and **an enum output that `audit_schema` can mechanically confirm carries no number**.

⭐ **It fixes a conflation rather than inheriting one.** `DemonSpeciesDef.Side` carries faction *and*
body in one field, and the shipped roster already breaks it: `peashooterzombie`, `ironpeazombie`,
`cherrynutzombie` and `bucketnutzombie` are zombie-**side** with plant **bodies**. A classifier reading
flavour text can see that; anything derived from `Side` cannot. **`hybrid` becomes a classification
outcome instead of a special case.**

**Consumer:** [item-map.md](item-map.md) §3.1 — its modules `slot-roles` (3) and `base-types` (6) both
key on frame, and its plan opens by resolving this dependency.

```
corpus ── adapter ─┬─ numerics ─┐
                   ├─ budget ───┼─ metrics ─┬─ report
                   └────────────┴───────────┴─ planner ── briefkit ── pipeline
```

---

### 3d. Feature 3 — generation runtime (capability map, 2026-09-01)

Proposal: [seedsmith-agent-runtime-proposal.md](seedsmith-agent-runtime-proposal.md) ·
Audit: [review/audit-agent-runtime-proposal.md](seedsmith/review/audit-agent-runtime-proposal.md)
(`R#` below = that audit's findings). **All owner decisions closed 2026-09-01.**

**Why this feature exists:** D1–D4 built a *classifier* — 84 species sorted into families. It
generates no content. `aspect`, `commander-effect` and `environment` are declared kinds that nothing
writes into, which is why `Coverage/DemonUncovered` reports 84 gaps. This feature is the generator.

**The layer distinction that defines it:** `planner` answers *"which content, in what order"* (job
orchestration — solved, kept). Nothing answers *"inside ONE generation: what steps, what state, when
to branch/retry/resume"* (workflow definition). That is what `workflow-runtime` adds.

| id | Capability | Model? | Depends on |
|---|---|---|---|
| `dependency-baseline` | `pyproject.toml`, exact pins, lockfile, isolated venv, CI install-from-lock, offline env-var assert, `response_format` constrained decoding in `llm_caller` | No | — |
| `motif-prose-filter` | Restrict motif derivation to prose; drop stat/mechanic lines; prefer `flavorIntroduce` | **No** | `dependency-baseline` |
| `workflow-runtime` | LangGraph seam: typed state, plain-function nodes, graph wiring, SQLite checkpoint/resume, bounded loops, dual-retry split, fan-out | No (infra) | `dependency-baseline` |
| `quality-gates` | Deterministic validator library (tiers 1–2). **CoVe fully specified but NOT built** — audit S6: shoehorning is caused by bad motifs, which `motif-prose-filter` fixes for free; build CoVe only if it is *measured* to persist | No (tiers 1–2 are model-free) | `workflow-runtime` |
| `commander-effect` | The first real per-demon content generator | Yes | `motif-prose-filter`, `workflow-runtime`, `quality-gates` |

**Module specs** (written 2026-09-01, audited, **SEALED — approved by the owner 2026-09-01,
authorized to build**; audit: [review/audit-generation-runtime-specs.md](seedsmith/review/audit-generation-runtime-specs.md),
10 findings all applied, **zero open questions remain**). **Amended 2026-09-06:**
`spec-commander-effect.md` gained a corpus-wide near-duplicate check on `doctrine` — the sealed
version measured per-item quality only, with no distribution/diversity gate at corpus scale, despite
its own §9 probe already reproducing the thesaurus-collision failure at single-demon scale (Jaccard
mean 0.52 across 3 generations for one demon). Found while auditing whether every seedsmith
generator, not only `adapter-items`/`tree-plan`, has a deterministic pre-generation coverage check.
[dependency-baseline](seedsmith/spec-dependency-baseline.md) ·
[motif-prose-filter](seedsmith/spec-motif-prose-filter.md) ·
[workflow-runtime](seedsmith/spec-workflow-runtime.md) ·
[quality-gates](seedsmith/spec-quality-gates.md) ·
[commander-effect](seedsmith/spec-commander-effect.md)

**Build order:** `dependency-baseline` → (`motif-prose-filter` ∥ `workflow-runtime`) →
`quality-gates` → `commander-effect`.

`motif-prose-filter` and `workflow-runtime` are independent once the baseline lands and may run in
parallel. **`motif-prose-filter` is the highest value-per-cost item in the feature** (R1) — no model,
no framework, and it fixes the input every later generator consumes.

**Locked decisions (owner, 2026-09-01) — not re-litigated in the specs:**

| Decision | Choice | Why |
|---|---|---|
| Workflow engine | **LangGraph**, pinned `==1.2.11` | 4 claims verified by execution; nodes stay plain functions |
| Structured output | **LM Studio constrained decoding**, zero deps | Hostile-prompt A/B: unconstrained returned prose and failed `json.loads`; constrained produced valid schema-conforming JSON at no latency cost |
| Checkpoint store | **`SqliteSaver`** | seedsmith is dev tooling and **never ships** — `guard-dal`'s invariant protects the shipped game's data layer, which this is not part of. **Scope: checkpoints only**; Python still never reads the game's SQLite |
| Model | **Local Gemma-26B**, no hosted tier | Measured 8/8 first-attempt pass, 0/8 anti-motif violations |
| `environment` generation | **Cancelled** | Deterministic mapping — `spec-pipeline.md:109`: a pipeline for work a script can do is a slow, expensive, non-reproducible script |
| `lore-enrich` | **Deferred**, and blocked | Would record synthetic text as `basis="text"`, corrupting the honesty signal `MotifSharing` depends on (R4). Needs `basis="enriched"` first |
| `aspect` generation | **Blocked** | `aspect-scope` approved but unbuilt in the demon program |

---

### 3c-ter. Theme registry — two defects filed by the item program (D34, 2026-09-04)

⛔ **`data/seed/demons/_registry/themes.v1.json` is stale: 84 themes against 386 shipped species**
(`data/seed/demons/species/` — 292 plant + 94 zombie, counted 2026-09-04). The registry is a snapshot
of a corpus this pipeline **generates**, and the corpus grows every run. Any downstream consumer that
reads it as the species population is reading fiction.

> ⭐ **This is the defect that made an item-program question look like a product decision.** [item-ideal.md](item-ideal.md)
> §2g #9d read *"31 of 84 themes are `basis = name`, that is 37%, module 13 needs a standing answer"* —
> and 37% of a stale snapshot is not a rate. The owner's correction was the right one: **the number is
> a defect, not an input.**

| id | Capability | Model? | Depends on |
|---|---|---|---|
| `theme-refresh` | Republish `themes.v1.json` over the **whole** species corpus, not a snapshot. Staleness becomes a pipeline check, not something a consumer discovers | no | `adapter-demons` |
| `theme-enrich` | LLM stage — for any theme at `basis: "name"`, generate the flavour text that raises it to `basis: "text"`. **The same shape `family-extract` and `motif-derive` already are**, with the same honesty contract | **yes** | `theme-refresh`, `pipeline` |

**Why `theme-enrich` and not an "ask first" downstream.** `basis: "name"` is not a property of the
species — it is a record of what the pipeline had when it ran. **This pipeline generates the missing
input**, exactly as the species and action generators do. A consumer that designs around name-basis
themes is designing around absent data instead of asking for it.

**Consumer:** [item-map.md](item-map.md) module 13 (`set-charm-gen`), which drops its per-run gate once
`theme-enrich` lands. Also unblocks [item-ideal.md](item-ideal.md) §2g #9c's `set` `themeKey`
requirement, which keys on `speciesId` and therefore needs the full corpus published.

## 4. Build order

**W1 — measurement (standalone value).** `corpus`, `adapter-items`, `numerics`, `budget`, `metrics`,
`report`. On completion this finds the nine empty partitions automatically, plus every distribution
skew, without a single model call. Worth shipping alone even if W2 never happens.

**W2 — planning.** `planner`, `briefkit`. Turns findings into a dispatchable work order and the
briefs to execute it. Still no model calls.

**W3 — generation.** `pipeline`. The LLM layer, one pipeline per metric, guardrailed.

Each wave gates on the previous being green.

---

## 5. Boundaries — what seedsmith is not

- **Not the band→rows generator.** That expander is a separate, later thing (`seed-contract.md` §1).
- **Not a replacement for `tools/ItemSeedValidator`.** The C# validator stays the **referential**
  gate: ids resolve, vocabularies are closed, computed fields are absent. 71 tests, wired to CI, no
  reason to port. Seedsmith owns **sufficiency, balance, numerics and planning**. Two tools, two
  questions, one clean boundary. Consolidation is a later option, not a v1 goal.
- **Not a place for game-balance opinions.** It measures against `budget`. If the budget is wrong,
  the fix is a budget edit, not a metric edit.
- **`tools/seed_graph` is absorbed**, not kept alongside. Its `Corpus`, `Acquisition`, `Finding` and
  check registry become the first cut of `corpus` and `metrics`; its 16 tests come with them.

---

## 6. The metric catalogue, sketched

Not the spec — the shape, so the map can be judged. Each is closed-loop unless marked.

| Family | Asks |
|---|---|
| **Coverage** | Does every allocated partition have content? Every role×frame? Every element? *(This is the family that would have caught all nine empty partitions on day one.)* |
| **Linkage** | Is everything reachable and completable? *(Today's `seed_graph` checks.)* |
| **Distribution** | Is any kind, role, band or element over- or under-represented against `budget`? |
| **Balance** | Do resolved magnitudes sit inside their declared budget envelope? Are rarity rungs monotonic? |
| **Registration** | Is anything acquirable absent from every drop table? Any table entry pointing at nothing? |
| **Quality** *(open-loop)* | Flavour present, tone on-theme, names not clustered. Produces a review queue. |
| **Constraint** | Are the rules that live only in lane documents actually held? |
| **Feasibility** | Can the planned allocation be satisfied at all, before anything is dispatched? |
| **Exemplar conformance** | Does each exemplar validate as real content of its own kind? |
| **Semantic dedup** | Do two entries say the same thing in different rows? |

---

## 7. Decisions — resolved 2026-08-23

1. **Name** → `seedsmith`. "Generator" stays reserved for the band→rows expander.
2. **Budget authorship** → **derived, then corrected.** A script reads every count already stated in
   the SSOTs and the fleet plan, emits `budget.json`, and marks each conflict inline — the uniques
   row will read *20 (ssot §5.33) vs 300 (fleet plan) vs 144 (shipped)*. The owner resolves a marked
   diff rather than recalling numbers.
3. **v1 scope** → **items, plus a stub adapter that exists only in the test suite.** The core cannot
   quietly reach into item concepts if a second, fake adapter compiles and passes. Roughly 5% the
   cost of a real second feature and it fails loudly the moment the interface leaks.
4. **The 8 empty partitions** → **left open, as seedsmith's first work order.** Known-answer
   end-to-end test: `metrics` must find exactly those eight, `planner` must order them, `briefkit`
   must brief them.

---

## 7b. The operating model — what a human actually does

Owner decision, and it is the requirement that shapes everything else:

> *Seedsmith must cover every gap class the agentic generation produced. The human controls
> seedsmith, monitors metrics, samples output to validate by hand, and improves seedsmith when it
> turns out to have a coverage gap. Manual work is minimised.*

Four consequences that are not obvious:

- **`report` owes a sampling mode**, not just totals. "60 of 60 consumables now have flavour" is a
  closed-loop pass; a human still needs to read eight of them to know whether the flavour is any
  good. Sampling is how open-loop metrics (P3) get their verdict, so it is a first-class feature
  rather than a debugging convenience: `--sample N` per metric, deterministic seed so the same
  sample can be re-read.
- **A miss found by a human becomes a metric, permanently.** When sampling catches something the
  catalogue did not, the fix is a new metric plus its regression test — never a one-off content
  edit. That is the loop that makes manual effort decline over time instead of recurring.
- **The catalogue's completeness is itself testable.** Appendix A lists every defect class this
  corpus actually produced. A metric family must claim each row. An unclaimed row is a known
  coverage gap in seedsmith, visible rather than latent.
- **Feasibility is checked before dispatch, not after.** The single most expensive failure in the
  agentic build was an allocation that could not be satisfied — 75 uniques competing for 40
  (role, band, axis) slots — and eighteen agents faithfully executed it before anyone noticed.
  `planner` refuses to emit a work order it can prove is unsatisfiable.

---

## Appendix A — defect taxonomy from the agentic build

Every class of defect the item corpus actually produced, and the metric family that must catch it.
This is the completeness test for the catalogue: **an unclaimed row is a coverage gap in seedsmith.**

| # | Defect actually observed | Caught by | Owner |
|---|---|---|---|
| 1 | Partition id / id template transcribed wrong | Identity | C# (exists) |
| 2 | Invented vocabulary — tags, elements outside the closed set | Vocabulary | C# (exists) |
| 3 | Name collisions, three-word names, possessives, rarity words in names | Naming | C# (exists) |
| 4 | Reference derived from a pattern instead of looked up (`item.humanoid-core-a-001`) | Referential | C# (exists) |
| 5 | Reference invisible to the resolver (snake_case vs kebab) | Referential | C# (fixed) |
| 6 | Tracking id vs runtime id confused — **four separate times** | Referential | C# (fixed) |
| 7 | Content rules that live only in a lane document until something violates them — the class, not any one rule (see note below) | **Constraint** | seedsmith |
| 8 | An allocation that is **arithmetically unsatisfiable** before a single agent runs | **Feasibility** | seedsmith |
| 9 | An exemplar propagating a wrong shape to every agent that reads it — **three times** | **Exemplar conformance** | seedsmith |
| 10 | Content that ships unreachable — no drop path, no recipe | Linkage | absorbed from `seed_graph` |
| 11 | A set nothing can complete — members declared by role, never pinned | Linkage | absorbed |
| 12 | A whole feature unbound — 10 milestones, no base type granting them | Linkage | absorbed |
| 13 | **Allocated partition with zero entries** — nine, of which eight were accidental, unnoticed for three waves | **Coverage** | seedsmith |
| 14 | Distribution skew — humanoid uniques half of plant across four roles; top rarity band entirely dark/light | **Distribution** | seedsmith |
| 15 | Rarity ladder not monotonic — a band-90 unique reading flatter than its own band-50 | **Balance** | seedsmith |
| 16 | Two entries rendering identically for mechanically different families (`Increased` vs `More`) | **Semantic dedup** | seedsmith |
| 17 | Flavour absent — 60 consumables, 30 of 70 charms, three silent themes | Quality *(open-loop)* | seedsmith |
| 18 | Names legally distinct but all saying one idea | Quality *(open-loop)* | seedsmith |
| 19 | Same-stage / wrong-order references between kinds authored in parallel | **Dependency order** | `planner` |
| 20 | A material that drops and nothing consumes | Linkage *(note)* | absorbed |

Twelve of twenty rows are seedsmith's to own; the rest are already gated and stay where they are.

> **Correction, 2026-08-23 audit.** Row 7 originally named the jewel-minor ban, the 8-of-15 role
> quota and the one-per-(role, band, axis) rule as *"never enforced"*. That was **false when
> written**: `UniqueRuleCheck.cs` and `SetRuleCheck.cs` enforce all of them, wired at
> `Validator.cs:70-71` and covered by tests — code added earlier the same day, by the same author as
> this map. The grounding reviewer caught it.
>
> The defect class is real; the examples were stale. Those rules lived only in prose for the whole
> agentic build and were violated 28 + 10 + 1 times before anyone wrote a predicate. What `Constraint`
> owns is **the recurrence** — the next rule that exists only in a lane document — not re-implementing
> five checks that now ship in C#. Seedsmith's job there is to notice that a documented rule has no
> corresponding check *at all*, in either tool.
>
> Worth keeping visible because it is the exact failure this map warns about elsewhere: asserting the
> state of the codebase from memory rather than reading it.

---

## 8. Specs

| Module | Spec | Carries |
|---|---|---|
| `numerics` | [spec-numerics.md](seedsmith/spec-numerics.md) | locked formulas, the tier-bands artefact, `rebalance`, the telemetry refit path |
| — | [spec-analytics.md](seedsmith/spec-analytics.md) | the algorithms `metrics` and `numerics` share |
| `budget` | [spec-budget.md](seedsmith/spec-budget.md) | target derivation, conflict preservation, distribution shape |
| `planner` | [spec-planner.md](seedsmith/spec-planner.md) | feasibility, derived ordering, scheduling |
| `pipeline` | [spec-pipeline.md](seedsmith/spec-pipeline.md) | guardrails, structured output, open-loop review |
| `corpus` `adapter` `report` `briefkit` | [spec-foundation.md](seedsmith/spec-foundation.md) | the interfaces and the feature seam |

Next: `tasks/seedsmith-plan.md` and `tasks/seedsmith-todo.md`, then build W1.

---

## 9. Audit — 2026-08-23

Five adversarial reviewers, one lens each: methodology, grounding, buildability, game design, gaps.
**66 findings, 11 of them BLOCKER.** Reports in [review/](seedsmith/review/).

The audit paid for itself twice over on its first two findings, both of which were mine and both of
which would have shipped:

- **The overlap guardrail was inverted.** `spec-numerics` asserted `hi_t < lo_(t+1)` — "so tier
  windows do not overlap into ambiguity". `bands.v1.json` `tierScaling.overlap` requires the exact
  opposite and proves it with the same arithmetic, because overlap is design guarantee **OD4**: a
  well-rolled lower rung must be able to beat a badly-rolled higher one. The guardrail would have
  raised on the first resolve of every channel. Written one paragraph after the spec congratulated
  itself on reading the registry first.
- **`metrics` had no spec at all.** The map listed it, six documents referenced it, nothing defined
  it.

### Owner decisions, resolving four blockers

| # | Blocker | Decision |
|---|---|---|
| 1 | Multi-set membership risks set jail; audit wanted a structural cap | **No cap.** The problem is the missing pipeline, not the absence of a rule — see [spec-planner §8](seedsmith/spec-planner.md#8-generation-pipelines--the-architecture-the-agentic-build-never-had). A planner that resolves member demands with sight of every set spreads them deliberately; a cap only refuses at an arbitrary number. |
| 2 | `opWeight[More] = 0.55` stands in for a non-constant relationship | **Ship it.** An adjustable tuning number, revisited for balance later. |
| 3 | Appendix A omits its own most frequent defect class | **Pipelines plus ordering plus validators.** Correct for the ordering half; the residue — logic bugs *inside* a check — is answered by mutation testing (`scripts/mutate.ps1`), now scoped into W1. |
| 4 | Calibrating a budget threshold is the same motion as editing a target to hide a failure | **Not material.** `budget` is a config file set before a run, not a live gate being negotiated. |

Decision 1 and decision 3 turned out to be the same decision. Both blockers traced to one absent
thing — **dependency-correct generation order** — and the set case showed that ordering is needed
not only *between* kinds but *inside* one: a set is five ordered stages, three of them deterministic,
and the agentic build asked a single agent to do all five at once.

### ~~Still open~~ ✅ CLOSED 2026-09-01 — all four resolved by shipped work

This section was written 2026-08-23, **before W1 was built**. Re-verified against the tree; every
item is done, and none needed a decision:

| # | Was open | Resolved by | Verified |
|---|---|---|---|
| B1 | undefined interface types | `adapters/base.py` | `KindSpec`, `Dimension`, `Channel`, `RegistrySet`, `SeedAdapter` all defined |
| B2 | item vocabulary inside the feature-agnostic modules | the `_stub` adapter + its conformance suite | `_stub` present, **10 seam tests**; the demons adapter (feature 2) shipped without the core learning a demon concept |
| B3 | no CLI specification | `seedsmith/report/cli.py` | `python -m seedsmith --help` → `{check, metrics}`, working |
| B4 | no CI cutover for absorbing `seed_graph` | S10's cutover | `tools/seed_graph/` **deleted**; `ci.yml:85` runs *"Item seed reachability (seedsmith)"* with the cutover recorded in-line |

The grounding corrections landed with the specs they belonged to.

## Filed by the party-dungeon program (2026-09-05)

| Ask | Filed by | Shape |
|---|---|---|
| `dungeon` adapter and pipelines | `party-dungeon/spec-dungeon-seed-contract.md` (approved) | `adapters/registry.py:13-15` gains `dungeon`; seven corpus kinds under `data/seed/dungeon/`; `python -m seedsmith dungeon contract --audit \| plan \| run \| audit \| emit`; planner per-cell motif briefs; provenance `{planHash, briefHash, promptVersions, registryVersions, motifSubsetHash}`; `stale_ids`; nothing exists on disk today (`party-dungeon/spec-domain-catalog.md` §Drift 5) |
| `uniques` extension | `party-dungeon/spec-unique-pipeline.md` §1 | one ownership level per `unique` field on `adapters/items/kinds.py:56-60`; a set-stem audit check; `adapters/items/uniques/{planner,briefs,pipelines,audit}.py` over the `frame × axis × band` grid (30 cells, 2–3 per cell, first ship 30 beside the 49 at rung 80+); `python -m seedsmith items uniques contract --audit \| plan --dry-run \| run \| audit`; tests stub the transport to raise |

