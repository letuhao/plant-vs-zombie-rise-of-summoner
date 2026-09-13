# Technical report + transition plan — passive-tree atom distribution

**Program:** `passive-tree-repair`. **Report written:** 2026-09-13. **Revision the measurements were
taken at:** working tree at `31f21339` (the commit log for this program starts at `d352a027`).

**Companion artifacts:** plan [`passive-tree-repair-plan.md`](passive-tree-repair-plan.md) · task list
[`passive-tree-repair-todo.md`](passive-tree-repair-todo.md) (its top has a `▶ RESUME HERE` block) ·
skill [`.agents/skills/seedsmith-passivetree-repair/SKILL.md`](../.agents/skills/seedsmith-passivetree-repair/SKILL.md).

**Method note.** Every claim below was verified against code or a live command in this session, per
[DOC/DESIGN-GATE](../docs/DESIGN-GATE.md) §3 ("a comment is not evidence; code beats docs"). Where a
figure is a *reading* of a corpus it is labelled as such and the command is given; where a document's
own claim conflicted with code, the code wins and the conflict is named. Anything NOT measured is
marked NOT_MEASURED rather than inferred.

---

## 0. Executive summary

The passive tree does not contribute to combat. Measured live: of 1,680 planned nodes, **407 bind and
1,273 are refused**; every one of the **337** bound atoms is `stat.modify`, **zero** are `stat.derived`,
and the resolve path (`PassiveTree.Resolve.TreeAtomSource`) reads only `stat.derived` — so
**readable = 0.0% of bound**. 164 of the 407 "bound" nodes carry no atom at all.

The problem statement's framing is **directionally correct but mis-attributes the cause.** There is no
missing *generic* engine: a deterministic engine (`seedsmith/numerics`, `effect-pipeline`'s `Resolver`)
and an LLM execution engine (`seedsmith/pipeline/llm_caller`) both exist, are tested, and are used by
the action pipeline. What is missing is a **specific pair of engines that the architecture names but
never built** — `effect-pipeline` modules **11 `affix-power-class` (the LLM stage)** and **12
`affix-channel-weights` (the deterministic stage)** — plus the **wiring** that would let the tree layer
use the shared effect resolver at all. The tree is a **bespoke parallel path**: it has its own
`Catalog/Binding/Resolve` stack and never calls `Resolver`, `ChannelPool`, `Instantiator` or
`InstanceProducer`. That is the architectural gap, and it is a *missing join and a missing L0 layer*,
not a missing engine.

Fixing it has three parts (report §3): connect the tree to the shared deterministic resolver; build the
L0 pair so affix selection has a principled, power-class-aware distribution policy; and supply the
balance anchors the mechanism kinds need. Two of these are **owner decisions** and are the immediate
blockers (§3.4).

---

## 1. Root cause analysis

### 1.1 The one-line failure

A tree node's affix resolves to a **primary** atom (`stat.modify`), but the tree's resolver reads only
**derived** atoms (`stat.derived`). Every bound node is therefore computed, stored, reported as a
contribution, and then silently dropped at read time.

### 1.2 Measured state (commands run this session)

```powershell
dotnet run --project tools/TreeBinder -- --out <tmp>     # live bind
$env:PYTHONPATH="tools/seedsmith"; python -m seedsmith trees census   # distribution + readable
dotnet run --project tools/FamilyExpandGen -- --check                 # upstream refusal census
```

| Measure | Reading | Note |
|---|---|---|
| Trees / planned nodes | 42 / 1,680 | generic corpus; +5 species PoC, unplanned |
| Bound / refused (live) | **407 / 1,273** | 24.2% bind rate |
| Bound atoms by kind | `stat.modify` **337** · `stat.derived` **0** | the defect |
| Bound nodes with **no** atom | **164 / 407** | mechanism/non-ladder affixes |
| **Readable** (kind the resolver reads) | **0.0% of bound** | the expensive failure |
| Committed-corpus census | 266 bound / 1,414 refused (15.8%) | older corpus; live is 407 |
| Mechanism vs magnitude bind rate | **2.1%** vs **29.5%** | mechanism = deep-tier payoff |
| Tiers 8–10 composition | **100% mechanism** | so deep tiers produce nothing |
| Chosen affixes that cannot resolve | **75 of 101** | R1 |
| Families `FamilyExpandGen` refuses | **94 of 125** | R7–R10 |
| Binder `op 'more'` refusals | **80 → 0** | fixed in this program (P4.2) |
| Binder crash on pool channels | **fixed** | P4.1; was exit `-532462766` |

**Read this as one sentence:** the tree *binds* a third of its nodes, and **none** of them do anything.

### 1.3 The cause chain, layer by layer

```
tree-language (LLM, picks affixIds)     → picks from a BRANCH tag, ignores the node's channelFamily
        │                                  and knows nothing of power class or channel weight
        ▼
FamilyExpansion (upstream, item-side)   → refuses 94/125 families: 40 "no tier-magnitude formula"
        │                                  (every mechanism kind), 20 "no referenceBase" (no curve),
        │                                  30 op anomalies, 4 missing pools
        ▼
tree-binder (deterministic, bespoke)    → prices whatever resolved, on PRIMARY channels (stat.modify)
        │                                  via its own AffixComposer, NOT the shared Resolver
        ▼
tree-catalog (on-disk, kMicro)          → stores NodeAtom{ kindId, channelId, op, kMicro }
        ▼
tree-resolve (deterministic)            → reads ONLY kindId == "stat.derived"  → drops 100%
```

Each arrow is a verified seam. The failure is **not** in one layer; it is that the chain was built
beside the shared effect pipeline rather than on it, and the layer that distributes kinds
(`tree-language`) was never given the inputs to do so.

### 1.4 Root causes, classified

Classification follows the repair skill's layering rule (A generator/brief · B persistence/wiring ·
C metric/gate · D stale evidence · E incomplete run).

| # | Class | Root cause | State |
|---|---|---|---|
| **R1** | A | `vocab.permitted_for_branch` offers all branch-tagged families (125) but only 26 have generated atoms; it ignores the node's own `quotaCell.channelFamily`, so the plan's distribution axis never constrains the pick | **OPEN** — fix is P5, blocked |
| **R2** | C | `NodeAtomOp` lacked `More` though `stat.modify` supports it; shared enum refused 80 real nodes | **FIXED** (`38286a0d`) |
| **R3** | B | `AffixComposer` called `GetString()` on a pool-object `channel` → unhandled exception killed the whole run | **FIXED** (`bd5b651b`) |
| **R4** | B | Binder prices `stat.modify`; resolver reads only `stat.derived` → 100% dropped | **OPEN** — spec fork, P4.3 |
| **R5** | B | Mechanism-class atoms are never carried into `NodeAtom` → 164 bound-but-empty nodes, deep tiers dead | **OPEN** — P6.1 |
| **R7** | A | `FamilyExpansion.TryReferenceBaseM1` implements only magnitude formulas; every other kind is refused (40 families) | **OPEN** — P2.1, blocked |
| **R8** | C | `Replace`/`Flag` have no defined tier semantics anywhere (19 families) | **OPEN** — P2.3 |
| **R9** | A | `params.op` is read as a modifier for kinds where it is a *verb* (`cherry`, `add`) | **OPEN** — P2.2 |
| **R10** | A | 20 channels (accuracy/dodge/crit/shield/interval/status) have no published curve | **OPEN** — P3.1 |
| **R6** | D | Committed `data/generated/passive-tree` is stale and pre-fix; `g-armour`/`g-precision`/`g-tempo` drifted | **FIXED for family-expand** (`bbe47eb1`); tree regeneration is P8, blocked |

### 1.5 Why the *distribution* specifically is broken

The user's phrase "critical bugs in atom distribution" is precise, and the cause is upstream of the
binder:

1. **No power-class/weight policy.** `tree-language` picks affixes by `branch` tag only. The plan
   assigns each node a `channelFamily` (a derived channel, e.g. `progression.bonus.atk`,
   `combat.power.omni`) but the picker never reads it. So a node's *intended* channel and its *actual*
   atom are only accidentally related — measured, they diverge completely (plan says derived; corpus
   is primary).
2. **The kinds the plan wants are the kinds the expander refuses.** The plan's quota axis draws from
   the **derived** catalog; the expander can only produce atoms for `stat.modify`/`stat.derived` and
   refuses every mechanism kind. So even a correct picker would hit a wall.
3. **Concentration.** The 101 distinct chosen affixes have Herfindahl 0.027 (effective ≈ 37), because
   the resolvable set is only 26 — a narrow, accidental vocabulary, not a designed distribution.

This is the sense in which the *sub-pipeline layer* is failing: the layer that should turn "this node
should grant X from pool Y at rate Z" into a concrete atom does not exist for the tree, and the policy
inputs it would consume do not exist either.

---

## 2. Architectural gap analysis

### 2.1 What the architecture defines

Two programs own the machinery the tree needs; both are specified, and neither is connected to the tree.

**(a) `seedsmith` — the deterministic + LLM execution engines.** `seedsmith-map.md` §W defines layers:

| Layer | Module | Role | State (verified in `tools/seedsmith`) |
|---|---|---|---|
| deterministic math | `seedsmith/numerics` | formulas, `resolve`, apportion, PAVA, rebalance | **present** |
| deterministic planning | `seedsmith/planner` | demand, feasibility, ordering, schedule, validate | **present** |
| brief composition | `seedsmith/briefkit` | render one brief from a pool | **present** |
| LLM execution | `seedsmith/pipeline` | `llm_caller`, `run`, `open_loop`, `provenance`, `staleness` | **present** |
| per-feature adapter | `seedsmith/adapters/*` | `items`, `actions`, `trees`, `creatures`, … | **present** |

The tree adapter (`adapters/trees/nodegen/run.py:49`) **does** use `pipeline.llm_caller`, and the action
adapter (`generate_action_pipeline.py:9`) does too. So the **LLM execution engine is not missing.**

**(b) `effect-pipeline` — the "sub-pipeline layer" for effects.** 12 modules, **all 12 specs written**
(`docs/architecture/effect-pipeline/`). It defines the four-layer model the problem statement is
reaching for (`effect-pipeline-map.md` §2):

| Layer | Decides | Status (architecture's own words) |
|---|---|---|
| **L1** container shape | how many effects, chance each | BUILT |
| **L2** the channel pool | *which* derived stats | named "**the core of this program**" |
| **L3** value range | min/max a magnitude rolls into | BUILT |
| **L4** resolve | pick atoms, pick stats, freeze numbers | BUILT |

### 2.2 What exists vs what is missing (verified by reading `src/`)

| `effect-pipeline` module | In `src/`? | Evidence |
|---|---|---|
| 1 `affix-schema` | **YES** | `prefix_rolls`/`suffix_rolls` shipped in `RpgStore.Containers.cs` |
| 2 `resolution-order` | **YES** | `Effects/Atoms/Resolver.cs` (five-step order, per-layer RNG streams) |
| 3 `affix-library` | **YES** | `AffixLibraryGenerator.cs` |
| 4 `instance-producer` | **YES + wired** | `InstanceProducer.cs`; `RpgStore.ProduceAndBind` now has real callers (`EquipAtomSource`, `EventOutcomeDispatch`, `RpgStore.UniqueActors`, `DerivedAuditActor`) |
| 5 `mods-absorption` | **NO** | migration not landed |
| 6 `patron-absorption` | **NO** | migration not landed |
| 7 `world-seed` | **YES** | `WorldSeed.cs` |
| 8 `eligibility-tags` | **NO** | `AffixTags.cs` derives the wrong set (map §I1) |
| 9 `affix-authoring` | **NO** | LLM authoring pipeline not landed |
| 10 `dev-reforge` | **NO** | debug surface not landed |
| **11 `affix-power-class`** | **NO** | **the LLM engine** — no `AffixPowerClass*` in `src/` |
| **12 `affix-channel-weights`** | **NO** | **the deterministic engine** — no `AffixChannelWeights*`; `data/seed/channel-policy/defaults.json` is a **2-entry stub** |

**This is the crux.** Modules 11 and 12 are *exactly* the "deterministic engine and LLM-driven engine"
pair the problem statement names, and the map states why they are two modules:

> *"their determinism differs. Classification is a model call — non-deterministic, therefore recorded
> and content-addressed. The weight policy decides what every container draws, so it must be
> reproducible. **Collapsing them hides a non-deterministic step inside a deterministic-looking
> artifact.**"*

So the architecture did **not** fail to design this layer; it designed it, split it correctly along the
determinism boundary, and **the implementation stopped before modules 11–12** (added 2026-09-03). The
tree then shipped without the distribution policy those modules exist to provide.

### 2.3 Why the sub-pipeline does not function in the tree path

`effect-pipeline-map.md` §1 already warns of the exact failure mode and states the invariant:

> *"an actor never receives the same source through two paths"* … *"Stating a disposition per path is
> the point."*

The map dispositions **four** paths (atom layer, `mods_json`, patron plugin, aura catalog). The
passive tree is a **fifth, undispositioned path.** Verified by search: nothing under
`tools/TreeBinder/**` or `src/FusionRpg.Core/PassiveTree/**` references `Instantiator`, `ChannelPool`,
`EffectInstance`, `effect_binding`, or `Resolver`. The tree has its own:

- `PassiveTree/Catalog/NodeAtom.cs` — a catalog-local atom record,
- `PassiveTree/Binding/AffixComposer.cs` — a catalog-local affix→atom resolver,
- `PassiveTree/Binding/TreeBinderRun.cs` — a catalog-local pricer,
- `PassiveTree/Resolve/TreeAtomSource.cs` — a catalog-local fan-in.

None of these share code with `Effects/Atoms/Resolver`, `ChannelPool`, `AffixValidator`, or
`InstanceProducer`. So the tree cannot benefit from L2 pool resolution, from L0 weighting, from the
five-step resolution order, or from the E6/E7 instance/binding machinery. It re-derives a weaker
version of each.

**Consequence.** When the binder prices a `stat.modify` atom, it is doing the item-layer's job with
none of the item-layer's guards — which is why the primary channels it emits never line up with the
derived channels the plan's quota axis intends, and why the resolver's kind filter then drops them.

---

## 3. Proposed technical requirements

Three deliverables. Ordered because R-3 depends on R-1/R-2 existing as real seams.

### 3.1 R-1 — Deterministic engine: one shared resolution path for the tree

**Requirement.** A tree node must resolve its atoms through the **same** deterministic engine as
equipment/items, not a catalog-local copy.

| # | Requirement | Acceptance |
|---|---|---|
| R-1.1 | `tree-binder` resolves affix → atoms via `Effects/Atoms/Resolver` (module 2), not a parallel `AffixComposer.Resolve` | one resolution implementation; the tree's copy is deleted or delegates |
| R-1.2 | A tree node declares its channel via the plan's `quotaCell.channelFamily` and the resolution **respects it**; a mismatch is a refusal, not a silent divergence | a test asserts emitted `channelId` ∈ the node's permitted `channelFamily` set |
| R-1.3 | Pool-shaped channels resolve through `ChannelPool` (E30) at roll time, per `spec-channel-pool.md` §3.2/§3.4, or refuse by name | P4.1's named refusal is replaced by real resolution once a pool draw exists |
| R-1.4 | The tree's contribution reaches `ActorHub` through the shipped `IActorStatSubsystem` seam (no new subsystem, no private fold) | `guard-actor-hub` passes; one owned node moves a real channel |
| R-1.5 | Both read modes (lawn + battle) read the **same** atoms | `TreeFanInTests` extended to both; totals byte-identical (`spec-tree-resolve` §12 test 15) |
| R-1.6 | **Kind parity:** the kinds the binder emits are the kinds the resolver reads — for **every** kind, not just one | a test binds a `stat.derived` node AND a `stat.modify` node and reads both back; zero silent drops |

**R-1.6 is the minimal fix for the immediate defect.** It is implementable as **derived-only** (align
the binder to emit `stat.derived` on the plan's derived `channelFamily`, matching
`spec-tree-resolve.md` §2.1 and the frozen quota axis) — a smaller, spec-aligned change — or as the
owner's "both kinds" union, which additionally needs a primary producer path and a `decisions.md` row.

### 3.2 R-2 — LLM engine: the power-class classifier and its deterministic policy

**Requirement.** Ship `effect-pipeline` modules 11 and 12 as the architecture specifies them.

| # | Requirement | Acceptance |
|---|---|---|
| R-2.1 | `affix-power-class` (module 11): an LLM stage assigns each affix **one closed-enum power class**, carrying `basis`. Never a number, never a rate | every authored affix has a class + `basis`; the enum is closed; a `check` fails on a missing class |
| R-2.2 | The classification is **content-addressed and recorded** (non-deterministic step is isolated and replayable) | re-running with the same ledger row does not re-call the model |
| R-2.3 | `affix-channel-weights` (module 12): a deterministic `(powerClass × channel) → weight` policy in `data/tuning/`; `poolFor(container, channel, rarity)` composes the candidate list L1 draws from | `data/seed/channel-policy/defaults.json` grows beyond its current 2-entry stub into the six named channels (`drop·boss·set·socket·unique·craft`) |
| R-2.4 | L0 runs **before** L1 and **consumes no RNG** — it composes the list; L1's existing stream draws from it | proof: adding L0 shifts no historical roll |
| R-2.5 | `drop`-channel weight may be vanishingly small but **never zero** (the 0.01% floor), and any structural zero carries a comment saying why it is exempt | a test asserts the floor; a comment is required for each structural zero |
| R-2.6 | The tree consumes L0: `tree-language`'s permitted-affix set is the L0-composed candidate list for the node's channel, not the raw branch tag | R1's fix becomes a consequence of R-2, not a separate patch |

**R-2.4 and the module-11/12 split are non-negotiable** — they are what keep the LLM step out of the
reproducible path. Collapsing them is the named failure mode the map forbids.

### 3.3 R-3 — Sub-pipeline implementation: close the join

**Requirement.** Wire the tree into `effect-pipeline` explicitly and **record its disposition** as the
map requires.

| # | Requirement | Acceptance |
|---|---|---|
| R-3.1 | Add the passive tree as a **dispositioned path** in `effect-pipeline-map.md` §1 (path 5), with its ownership and its relationship to paths 1–4 | the invariant "an actor never receives the same source through two paths" holds and is stated for the tree |
| R-3.2 | The tree becomes a `skill`-container producer: a node is an **affix inside a `skill` container** (`spec-tree-binder.md` §1; `ContainerKind.Skill` is a legal kind) resolved by `Resolver` and materialised by `InstanceProducer` | a node yields a real `InstanceRow` + `BindingRow`; the resolver's five-step order runs |
| R-3.3 | `skill` container ids follow `definitions.md:41` (`^skill\.[a-z0-9-]+$`) | a load-time validator refuses a malformed id |
| R-3.4 | If the owner chooses the **primary** route, a producer path exists for `stat.modify` grants (the `BuildEquip`/FA1 shape) and **`decisions.md` is amended first** (architecture that locks behavior leads) | the decision row exists before code |
| R-3.5 | Regeneration is proof, not repair: after R-1/R-2, regenerate the corpus via the real CLI and re-audit the whole population | `FamilyExpandGen --check` = 0; `TreeBinder --check` reproduces byte-for-byte; census `readable > 0`; no tree binds 0% |
| R-3.6 | A live-boot proof: a real allocated node changes a number in **lawn and battle** | spec-tree-resolve §14 success criteria 1–4a |

### 3.4 The two owner decisions that gate everything

Nothing in §3.1–§3.3 can be implemented until these are answered. They are genuine product decisions,
not engineering choices — the two frozen specs give opposite answers.

**Decision A — which kind does a tree node carry?**
- **(A1) Derived-only**, per `spec-tree-resolve.md` §2.1 + the plan's `channelFamily` quota axis. The
  binder emits `stat.derived` on the plan's derived channel. Smallest change, spec-aligned, needs R-2
  (L0) + R-1.2 + the 8 derived families' pools. **The report recommends A1.**
- **(A2) Both kinds** (owner's recorded "both, unified"). Adds a primary producer path; requires a
  `decisions.md` row (R-3.4). Larger, and duplicates a capability the derived bridge may already give.

**Decision B — supply the balance anchors, or re-scope.**
- **(B1)** Publish the missing anchors: a `power-scale.v{next}` curve for the 20 curve-less channels,
  **and** a chance/duration anchor for `status.apply`. This is owned by the power program and must be a
  **new published version**, never an in-place edit.
- **(B2)** Explicitly exclude the mechanism kinds from the tree vocabulary for now, re-scoping the tree
  to magnitude-only, and file the mechanism work as a separate program.

`bands.v1.json` carries the required *ratios* (chance 1.75‰, duration 1.4‰) but **no base**, and its
own worked example is self-labelled *"illustrative, inherited, not balanced"*. The registry's rule is
*"reject at import, not guess one"* — so **inventing the anchor is the defect this program exists to
fix**, and B1/B2 is a human call.

---

## 4. Session transfer prompt

Copy the block below verbatim into the incoming agent's first message. It is self-contained.

```text
SESSION TRANSFER — passive-tree atom distribution (unplayable tree)

ROLE: You are resuming a repair of the passive-tree generation/resolution seam in
D:\Works\source\plant-vs-zombie-rise-of-summoner (Rise of Summoner, PVZ Fusion overlay).

READ FIRST, in this order — do not skip, do not summarise from memory:
1. AGENTS.md (design gate, git policy, parallel-program paths, session boundary)
2. docs/DESIGN-GATE.md §1 (find the subsystem rows for "Stats" and "Power/scaling")
3. .agents/skills/seedsmith-passivetree-repair/SKILL.md  ← the workflow this program runs on
4. tasks/passive-tree-repair-plan.md  and  tasks/passive-tree-repair-todo.md  ← its top has a
   "▶ RESUME HERE" block with the current state, the two blockers, and the first thing to re-check
5. docs/architecture/passive-tree/{spec-tree-resolve.md §2.1, spec-tree-binder.md §1/§4.1/§6,
   spec-tree-catalog.md §2.3}, docs/architecture/effect-pipeline-map.md §1/§2/§3,
   docs/architecture/seedsmith/spec-planner.md §8

THE PROBLEM (measured, not recalled):
- A tree node binds and then contributes NOTHING. Live: 42 trees, 1680 planned nodes,
  407 bound / 1273 refused. All 337 bound atoms are kindId=stat.modify; ZERO are stat.derived.
  164 of the 407 bound nodes carry no atom at all.
- src/FusionRpg.Core/PassiveTree/Resolve/TreeAtomSource.cs reads ONLY kindId=="stat.derived", so
  100% of bound atoms are dropped at read time. readable = 0.0% of bound.
- Reproduction:
    $env:PYTHONPATH="tools/seedsmith"; python -m seedsmith trees census
    dotnet run --project tools/TreeBinder -- --out <tmpdir>
- Mechanism nodes (all of tiers 8-10) bind at 2.1% vs magnitude 29.5%, so the deep-tier payoff the
  design requires is exactly what the pipeline deletes.

WHAT THE PROBLEM STATEMENT GETS RIGHT AND WRONG (verify, then proceed):
- RIGHT: the tree's atom distribution is critically broken; the effect sub-pipeline layer does not
  function for the tree.
- WRONG: "the system lacks a deterministic engine and an LLM engine". Both exist and are tested:
  seedsmith/numerics + seedsmith/planner + effect-pipeline's Resolver (deterministic), and
  seedsmith/pipeline/llm_caller (LLM). The tree adapter already calls llm_caller.
- THE ACTUAL GAP: effect-pipeline modules 11 affix-power-class (the LLM stage) and 12
  affix-channel-weights (the deterministic stage) are SPECCED BUT NOT IMPLEMENTED
  (data/seed/channel-policy/defaults.json is a 2-entry stub). AND the passive tree is a BESPOKE
  PARALLEL PATH: nothing in tools/TreeBinder or src/FusionRpg.Core/PassiveTree references
  Resolver / ChannelPool / Instantiator / InstanceProducer / effect_binding. The tree has its own
  Catalog/Binding/Resolve stack and never uses the shared effect pipeline. effect-pipeline-map §1
  dispositions four effect paths and the tree is an undispositioned FIFTH.

TWO BLOCKERS — DO NOT GUESS PAST THESE, ASK THE OWNER:
A. KIND FORK. The two frozen specs disagree on the kind a tree node carries.
   spec-tree-resolve §2.1 + the plan's channelFamily quota axis say stat.derived;
   the committed corpus and tree-binder are 100% stat.modify. Recommended: DERIVED-ONLY (A1) —
   smaller, spec-aligned. Alternative: BOTH kinds, which needs a decisions.md row first.
B. BALANCE ANCHORS. The mechanism-kind formulas' anchors are authored NOWHERE.
   bands.v1.json statusMagnitudeAndDuration has ratios (chance 1.75 per-mille, duration 1.4) but no
   formula and no sharePermilleOwnership key, and its worked example is marked "illustrative,
   inherited, not balanced". spec-numerics.md:210-212 says those shares are "specced when their
   families are resolved". tier-bands.v5.json has no per-family chance/duration surface; 0 of 51
   non-magnitude families carry an explicit amount. The registry's own rule is "reject at import,
   not guess one" — so inventing the anchor IS the defect. Either the owner publishes the anchors
   (a new power-scale version + a status.apply chance/duration anchor) or explicitly excludes the
   mechanism kinds and re-scopes the tree to magnitude-only.

WHAT IS ALREADY FIXED (do not redo): binder pool-channel crash (bd5b651b); NodeAtomOp.More with M3
kept loud at both load and bind (38286a0d); family-expand canonical + reproducible (bbe47eb1);
census instrument that counts READABLE atoms (4e21bc2f); orphan/unprovenance separation
(c82d6f5a, c3ef3d9a). See tasks/passive-tree-repair-todo.md for per-task gates and commit hashes.

FIRST THING TO CHECK ON RESUME (this may collapse Decision A to A1 for free):
the shipped progression.bonus.atk -> atk bridge. ActorHub.MergeAppliedCombat
(src/FusionRpg.Core/Stats/Derived/ActorHub.cs:89-100) reads progression.bonus.atk/defense/arm1/arm2/
maxHp and folds them into EntityFinal; EntityApply.cs:404 reads ProgressionBonusAtk. Determine
whether a real stat.derived atom on progression.bonus.atk reaches a live actor's atk on BOTH the
lawn and battle paths. If it does, the derived route is sufficient and the primary-producer route
(Decision A2) is unnecessary — record that finding instead of building A2.

IMMEDIATE NEXT STEPS (in order):
1. Run the two reproduction commands above and the existing suites to confirm the baseline:
     dotnet test tests/FusionRpg.Core.Tests
     $env:PYTHONPATH="tools/seedsmith"; python -m pytest tools/seedsmith/tests/adapters/trees -q
     .\scripts\guard-single-writer.ps1 ; .\scripts\guard-secondary-no-unity.ps1 ;
     .\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-dal.ps1 ; .\scripts\guard-test-substrate.ps1 ;
     .\scripts\guard-actor-hub.ps1
2. Verify the progression.bonus bridge (above) and report the finding.
3. Ask the owner Decisions A and B. Do not write code for P2.x/P3.x/P4.3/P5/P6 before they answer.
4. If authorised for A1 + B1/B2, implement in this order: R-2 (L0 modules 11+12) → R-1.2 (tree
   respects channelFamily) → R-1.6 (kind parity, derived) → P6.1 (mechanism carriage) →
   P7.1 (lawn+battle proof) → P8 (regenerate + re-audit).

HARD RULES (this repo, non-negotiable):
- Code beats docs; a comment is not evidence. Read the file before asserting.
- Never hand-edit data/seed/** or data/generated/** to make a metric pass — fix the generator and
  regenerate. Generated corpus is EVIDENCE, not the system under repair.
- Never weaken a threshold, delete a hard case, or move a golden to get green.
- A balance surface (tier-bands, power-scale, bands.v1) is PUBLISHED AS A NEW VERSION, never edited
  in place. bands.v1.json is frozen.
- Do not quote corpus counts as constants; a count is a READING, re-derive it.
- Commit via the repo-git.commit MCP tool with EXPLICIT paths, never all=true (parallel streams share
  this tree). Validate the message first. Push is owner-only.
- Use the program-prefixed paths: tasks/passive-tree-repair-plan.md and -todo.md. Never write
  tasks/plan.md or tasks/todo.md.
- If a task must be done, leave tests green and the tree committed; report blockers honestly rather
  than guessing past a product decision.
```

---

## Appendix — evidence index

| Claim | Command / file |
|---|---|
| 407 bound / 1,273 refused, 337 `stat.modify`, 0 `stat.derived`, 164 empty | `dotnet run --project tools/TreeBinder -- --out <tmp>` then count `atoms[].kindId` |
| `readable = 0.0% of bound` | `python -m seedsmith trees census` |
| Resolver reads only `stat.derived` | `src/FusionRpg.Core/PassiveTree/Resolve/TreeAtomSource.cs` |
| 94/125 families refused upstream | `dotnet run --project tools/FamilyExpandGen -- --check` |
| 51 non-magnitude families, 0 with `amount` | scan of `data/seed/items/affix-families/*.json` |
| L0 modules 11–12 unimplemented | no `AffixPowerClass*`/`AffixChannelWeights*` in `src/`; `data/seed/channel-policy/defaults.json` = 2 entries |
| Tree never calls the effect resolver | no `Resolver`/`ChannelPool`/`Instantiator`/`InstanceProducer` under `tools/TreeBinder` or `src/FusionRpg.Core/PassiveTree` |
| `skill` is a container kind | `src/FusionRpg.Core/Effects/Atoms/ContainerRow.cs:29`; `definitions.md:41` |
| `InstanceProducer` now has production callers | `RpgStore.AtomInstances.cs:351`, `RpgStore.Fusion.cs:325`, and `ProduceAndBind` callers |
| Two frozen specs disagree on kind | `spec-tree-resolve.md` §2.1 vs the corpus/binder; quota axis at `spec-tree-plan.md:739` |
| Anchors authored nowhere | `bands.v1.json` `statusMagnitudeAndDuration`; `spec-numerics.md:210-212`; `tier-bands.v5.json` |
