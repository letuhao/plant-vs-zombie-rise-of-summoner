# Spec: `tree-catalog`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-catalog` · **Wave:** 1 · **Depends on:** `tree-plan` · **Depended on by:**
`tree-binder`, `tree-review`, `tree-state`, `tree-surface`

---

## Objective

Define and ship **the baked artifact**: the on-disk record every passive-tree node is, the id that
survives regeneration, the version and migration rule that keeps a saved allocation meaningful, and
the load path that turns committed files into rows.

This module is the only one that defines the on-disk record
([passive-tree-map.md:63](../passive-tree-map.md)). Everything else reads it.

D24, in the owner's words: *"the passive skills tree need concrete value before the game run… it need
solid stats, so user can learn it, if it random every new player create, it will cause confuse, user
cannot build because they need to relearn."*

**The one-line contract:** the catalog stores **coefficients**, not magnitudes. That is what makes one
static file correct for every player at every `Θ`.

---

## Design

### 1. The freeze line

Three layers, and the third is empty by decision, not by omission
([01-static-vs-rolled.md §1](../../research/passive-tree/01-static-vs-rolled.md)).

| Layer | Holds | Decided | Same for every player? |
|---|---|---|---|
| **(a) Baked at build time, committed as data** | tree roster · tree shape and shape archetype · node ids · prerequisite links · tier gate ladder · which affix each node grants · each atom's **coefficient** and scale axis · the soul curve reference · exclusion properties · `enabled` | dev machine, before packaging | **yes — byte-identical** |
| **(b) Per-actor state** — owned by [`tree-state`](spec-tree-state.md) | which nodes are owned · soul level per node | player's machine, as they play | no — this is the build |
| **(c) Rolled per player at runtime** | **nothing** | — | — |

**(c) is empty, and the shipped contract already gives us that for free.**
`spec-container-schema.md:50-56` records the 2026-09-01 amendment that made `species-passive` roll a
pool and **deliberately left `skill` containers on the core alone**: *"`trait.{traitId}` containers
(fusion, Q10) and `skill` containers still use the core alone."* A passive node lives in a `skill`
container, so `prefix_rolls = suffix_rolls = 0` and the draw never runs. **D24 is the shipped
contract, not a new ask** — something would have to change to lose it.

This does not break seed → concrete. It lands on the split the demon program already uses and
[DESIGN-GATE.md:45](../../DESIGN-GATE.md) states directly: *"Species stats are deterministic and
shared; only effects roll, per player, at runtime."* The tree catalog belongs to the shared half.

**Per-player variance is not lost.** It has three homes that are not the tree: rolled item affixes,
the per-player `species-passive` roll, and D11's gear-granted points — which change what you can
afford without changing what the tree is.

### 2. The node record

Two record types ship. Both are content; neither carries a magnitude.

#### 2.1 `TreeRecord` — one per tree

| Field | Type | Notes |
|---|---|---|
| `treeId` | `string` | authored, allocated once from the roster (D9/D27). Never a position |
| `category` | `enum { Primary, Elemental, Status, Family, Species }` | which roster it came from. **Five, confirmed by ruling R7** — species is a category, not a variant, and the map gives it its own module. `tree-plan` emits only the first four and uses different tokens for two of them (`aptitude` → `Primary`, `demonFamily` → `Family`); that rename is a straight enum mismatch that would surface as a failed check at import, so **the importer maps the plan's tokens onto these five and refuses any token outside the map, naming it** |
| `gateQuantity` | `string` | the ONE index this tree's tier gate reads. Never four incommensurable quantities at one threshold |
| `shapeArchetype` | `string` | D15 — the plan's archetype id (broad-flat, spiked, gated-deep …) |
| `tiers` | `int` | 10 (D29). **Structural**, not tunable — it is what the tree *is* |
| `branches` | `int` | 2 (D10). Structural |
| `nodesPerTier` | `int[]` | the plan's `w[t]`; generated data, not a constant |
| ~~`weightTotal`~~ | ~~`long`~~ | ~~`Σ tierWeight` over every node in the tree — the binder's denominator~~ **Removed by ruling R4.** The binder no longer distributes by `tierWeight(t)/weightTotal`; it reads the plan's `budgetShareMilli` per node, which the plan already emits and the binder already listed as an input. Keeping the field would ship a dead number that *looks* structural and is not — it is 220 for `broad-and-flat`, 178 for `gated-deep`, 252 for `late-crown` |
| `catalogVersion` | `int` | matches the file's own `v{n}` |
| `enabled` | `bool` | a retired tree is disabled, never deleted |

#### 2.2 `NodeRecord` — one per node, **40** per tree, everywhere including species

| Field | Type | Notes |
|---|---|---|
| `nodeId` | `string` | `skill.<treeId>-<branch>-t<tier>-<nodeKey>` — §3 |
| `treeId` | `string` | |
| `branch` | `enum { Off, Def }` | D10's two branches |
| `tier` | `int` | 1..10; the tier gate it sits behind (D26/D29) |
| `nodeKey` | `string` | allocated once by the plan within `(tree, branch, tier)`, **never reclaimed**, never derived from the node's effect or its display order |
| `prereqNodeIds` | `string[]` | link structure; every entry must resolve inside the same tree |
| `nodeClass` | `enum { Magnitude, Mechanism }` | the requirement ideal §3.5 added to D13: *"a focus build cannot be rescued with MAGNITUDE… a node that only adds magnitude is, for a focused build, measurably worthless."* The plan guarantees deep tiers carry mechanisms; the catalog carries the label so `tree-review` and the sweep can count them |
| `affixIds` | `string[]`, **1..3** | ~~`affixId` (`string`)~~ — corrected by ruling R6. **A node is one or more affixes, never a bare atom**: an affix is a named bundle drawn together (`definitions.md` §4a), and a reflect node is two atoms that must arrive together, which is exactly why the roll unit is an affix and why one is not always enough. The plan, `tree-language` and `tree-binder` all already carry an array; the single string was this record's alone. Empty is a refusal; more than three is a refusal (§6) |
| `budgetShareMilli` | `int` | the plan's own share for this node, **‰ of ONE BRANCH**, copied through verbatim and never recomputed (R4: the plan's value is authoritative). This is the number the potency ceiling is checked against — §2.5. Added by ruling; it widens the record, and §Boundaries' *ask first* is noted rather than skipped |
| `atoms` | `NodeAtom[]` | §2.3 |
| `excludeProps` | `string[]` | D14's property keys, drawn from the plan's frozen property vocabulary |
| `tagsJson` | object | D22's payoff — the exclusion property space is atom tags, which already ship |
| `enabled` | `bool` | a retired node keeps its id and renders greyed; §4 |
| `retiredAtRevision` | `int?` | set once, when `enabled` flips false. Printed to the player |

#### 2.3 `NodeAtom` — the magnitude fields, and there is no magnitude among them

| Field | Type | Notes |
|---|---|---|
| `kindId` | `string` | one of the **16** kinds (`AtomKindRegistry.KindCount`, `AtomKindRegistry.cs:31`) |
| `attachPoint` | `enum` | one of the **7** (`AtomKindRegistry.cs:21`) |
| `channelId` | `string` | validated against the live vocabulary at load — `AtomKindRegistry.cs:84-85` for derived, `:71` for primary. An unregistered channel is refused, never silently written |
| `op` | `enum` | flat / increased / replace / flag |
| `trigger` | `enum?` | one of the **11 authorable** of 13 — `OnGranted`/`OnRemoved` are runtime lifecycle states no atom may name (`AtomKind.cs:104-111`) |
| `whenJson` | object? | predicate tree — 12 leaves, 2 subjects, depth ≤ 4, ≤ 16 nodes (`PredicateNode.cs`) |
| **`kMicro`** | **`long`** | **the node's share of `P(Θ)`, in per-million.** The only number the catalog carries about strength |
| `scaleAxis` | `enum { PTheta, Theta, FlatPermille }` | §2.4 |
| `unitClass` | `enum` | one of the **13** `UnitClass` members (`StatClass.cs:29-100`). Stored, so the read path never re-derives it |
| `soulCurveId` | `string?` | D3's deepen track: a **curve reference, never a formula** (`CurveTable.cs:14-15`). This is what lets a player read level 9's value before buying level 1 |

**`kMicro` is a `long` even though today's values are small.** CLAUDE.md rule 1 is *"`long` for any
magnitude `contentScale` can touch"*, and this integer is multiplied by `P(Θ)` downstream. Storing it
narrower puts a narrowing cast on the hot path — the exact defect the overflow rules exist to end.

**Why per-million and not per-mille.** The shipped field is `PowerLadderKMilli`, an `int` in per-mille
(`ValueSpec.cs:92`). At per-mille a tier-1 node's coefficient rounds with a ~~**~17% error**~~
**+63% error** — the ~17% figure was measured against a **7-tier** tree, whose tier-weight sum was
112; `spec-tree-binder.md:189` re-derived it at D29's ten tiers, where the same budget spreads over
the ten-tier sum of 220 for the uniform archetype, every coefficient roughly halves and the rounding
error roughly doubles. **Both sums are arithmetic on a width vector, not a stored field** — the
`weightTotal` this module used to carry is gone (R4), and quoting the numbers here is a derivation
trail, not a live citation. The conclusion is independent of the distribution rule either way: what
drives the error is how small a shallow node's coefficient is, and ten tiers make it smaller than
seven do. Larger
than one whole tier step either way — which destroys D26's *linear per-tier power* pairing rule at the
shallow end. At per-million the same worst case is 0.04%. The fix is a `PowerLadderKMicro` sibling resolved by
the same three lines at `AtomCompiler.cs:456-466` with `/ 1_000_000`. **That is a wiring gap in the
atom layer, not this module's to land** — named here so it is not discovered at build time. See
Boundaries.

#### 2.4 `scaleAxis` — a use of `UnitClass`, not a third classification

`DESIGN-GATE.md:34` warns that inventing a third channel classification is a known past failure.
**This module invents none.** `scaleAxis` is a three-valued *function of* `UnitClass`, computed by the
binder at bake time and stored so the read path is a lookup rather than a rederivation:

| `scaleAxis` | For which `UnitClass` | The read |
|---|---|---|
| `PTheta` | `GameUnits`, `GameUnitsPerSecond`, `ReciprocalPoints` | `kMicro · P(Θ_node) / 1e6` |
| `Theta` | `SigmoidPoints`, `SigmoidMultiplierPoints`, `StatusPotencyPoints` | `kMicro · Θ_node / 1e6` — **PS-3: contests read `Θ`, linear.** `P(Θ)` on a rate input is a design error, and two of these fail *silently*: the number on the sheet rises and the multiplier does not |
| `FlatPermille` | `PerMilleRatio` | flat per-mille points planned against the clamp. A bounded ratio — exempt from PS-8 **by nature, and the node's own comment must say so** |

`Milliseconds`, `Count`, `Flag`, `LadderIndex`, `AptitudePoints` and `LoamUnits` are **refused at
load** as magnitude targets. `LadderIndex` is refused hardest: `progression.power` *is* `Θ`, so a node
writing it is a private second ladder — the defect §10 of the power SSOT exists to end.
`AptitudePoints` is refused by construction anyway: an aptitude is a **source, not a registered
channel** (`decisions.md:103`), so it is not in `DerivedChannels()` and `stat.derived` cannot name one.
That is the same construction D11 relies on.

### 2.5 The potency ceiling — one name, one denominator, and an honest verdict

**This section replaces a check that could not be implemented as written.** The load-path table used to
say *"`kMicro` … above the plan's `nodePotencyCeiling` → reject"*. Both halves were wrong:

- **The name.** There is no key called `nodePotencyCeiling`. The canonical key is
  **`potency.maxNodeShareMilli`** in `data/tuning/passive-tree.v1.json` (ruling R2), and per ruling R5
  its unit is **‰ of ONE BRANCH**, default **182**. The `91‰-of-budgetTotal` form is superseded — it
  was a silent 2×, and `gated-deep`'s capstone reads `budgetShareMilli = 182` against it.
- **The unit.** `kMicro` is a **post-anchor** per-million share of `P(Θ)`. The channel anchor alone is a
  factor of ~0.135 on atk, and it differs per channel, so `kMicro` is not comparable to a per-mille
  budget share **in any denominator**. Comparing them was a dimensional error, not a rounding one.

**The check, stated so it can be implemented.** The ceiling is a **budget-share** ceiling, so it is
checked against a budget share:

```text
refuse when   node.budgetShareMilli > potency.maxNodeShareMilli      both ‰ of one branch
```

`budgetShareMilli` is the plan's own emitted number, carried on `NodeRecord` verbatim (§2.2, R4). Like
compared with like, no conversion required, and nothing on this path reads `kMicro`.

**The conversion, stated once so the two numbers can never silently disagree.** `kMicro` is derived
from the share, not the other way round:

```text
kMicro = round_half_away( budgetShareMilli · branchBudgetMicro · channelAnchorMilli / 1_000_000 )
```

The inverse is **not** a legal check, because `channelAnchorMilli` varies per channel and does not
divide out of a branch sum. What the catalog asserts instead is **reproducibility**: `--explain` prints
this chain for any node, and `--check` proves the emitted `kMicro` is what the chain produces. A
`kMicro` that disagrees with its own share is a generator bug, and that is what the byte-identity gate
is for.

> ⛔ **And the ceiling refuses nothing today — say so rather than let a test imply otherwise.** 182‰ is
> `1000 / ((tierCount + 1) · minTerminalWidth)` doubled onto the branch denominator, and
> `minTerminalWidth = 1` is already a hard precondition of the plan. So the ceiling *is* the shipped
> archetype set's own supremum: `gated-deep`'s crown touches it exactly, and it touches it because it
> was derived from it. As a balance rule it is circular — *"is a 182‰ capstone too potent?" "No,
> because we defined too-potent as above 182‰."*
>
> **Verdict: at this module's boundary it is a documentation constant plus a corruption check**, and it
> is worth keeping on those terms alone. It still fires on a hand-edited generated file, on a generator
> regression, and on a node emitted by an archetype added without passing `tree-plan`'s R-P2. It does
> **not** prove the corpus is balanced, and no success criterion here claims it does.
>
> **Owed by `tree-plan`, not by this module:** deciding whether to set the ceiling *below* the
> topological maximum, at which point R-P2 becomes a real admissibility test that `gated-deep` has to
> pass on its merits. Until that decision lands, this spec ships one test —
> `a_node_share_above_the_ceiling_is_refused`, driven by a **synthetic** over-budget row — and no test
> that treats the shipped corpus passing as evidence the ceiling binds.

### 3. Node id stability — **settled**, and what the alternatives break

> **Ruling R3, 2026-09-05: this scheme is the one that ships.** `skill.<treeId>-<branch>-t<tier>-<nodeKey>`
> is grammar-verified against `container_id` and `tree-plan` is being corrected to adopt it in place of
> its own `<treeId>/<off|def>/t<tier>/<index>` — which used both a `/` and a positional ordinal, and
> could not survive this validator. What follows is no longer a recommendation.

The property that has to hold, because build guides are the payoff:

> **The same conceptual node keeps the same id across every regeneration in which it still exists,
> regardless of what was added, removed, retuned or reordered around it.**

| Scheme | Verdict | What breaks |
|---|---|---|
| **Content hash** (`node-<sha of its effect>`) | ⛔ | **Every rebalance renames every node it touched.** A magnitude retune would orphan allocations, so §4's R6 becomes impossible. It also collides with the hash's existing job: `spec-content-hash.md` computes a content hash precisely to make *a changed number visible*, so using it as identity makes change and identity the same thing |
| **Positional ordinal** (`fire-tree-node-07`) | ⛔ | **Insertion renumbers everything after it.** The repo already refuses this shape for exactly this reason — `data/seed/README.md:109-110`: an ordinal already held *"is refused rather than renumbered underneath the content naming it"* |
| **Composed structural slug** | ✅ **recommended** | Nothing, provided the coordinates it composes are themselves stable — which §3.1 is about |

#### 3.1 The format, and the constraint on it

```text
skill.<treeId>-<branch>-t<tier>-<nodeKey>

  treeId    authored, allocated from the roster. Never a position
  branch    off | def
  tier      the tier gate it sits behind
  nodeKey   allocated ONCE by the plan within (tree, branch, tier), never reclaimed,
            never derived from the node's effect or its display order
```

**Hard constraint, verified:** `container_id` allows **no dot in its body** — the grammar is
`^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`
(`item/seed-contract.md:131-133`, whose own note records *"Two lanes discovered this the hard way"*).
Every separator inside the body is a hyphen.

**What makes `nodeKey` stable is allocation, not derivation.** This is the mechanism
`item/seed-contract.md:118-123` already uses: a namespace is allocated per partition, the sequence
within it belongs to the author, ids are never reused, and *"Global uniqueness validation then catches
operational mistakes rather than doing the work."*

> **The rule, stated as a rule because a downstream module's cost model depends on it (R3):**
> **`nodeKey` is MINTED ONCE by `tree-plan` and READ BACK from the seed on every regeneration. It is
> never recomputed from the node's position, order, effect or content.**
>
> That read-back is the whole property. `tree-review`'s `O(diff)` re-review — only nodes whose content
> actually changed go back through the review pipeline — is a claim about *identity survival across a
> regeneration*, and it is true only because the key comes out of the seed rather than out of a
> counter. Recompute the key from position and every insertion re-mints the ids after it, every
> unchanged node reads as new, and `O(diff)` silently becomes `O(corpus)` — 35,160 nodes back through
> review for a one-node insert, with no error anywhere to say why.

That is the difference between a stable id and an ordinal wearing a slug's clothes.

**An authored id that disagrees with its coordinates is an `IdMismatch` refusal, kept as authored so
the validator can say so** — `data/seed/README.md:62-64`'s existing rule for `atom_id`, not a new one.

**Ids survive a magnitude rebalance untouched**, because no coordinate in the slug is a magnitude.
That is the whole reason to prefer it, and §4's R6 depends on it.

### 4. Catalog versioning and migration

Most of this the repo already enforces; the module adopts rather than invents.

| Rule already enforced | Source |
|---|---|
| An id is **never reused** — *"not after a deletion, not after a rejected review, not ever"* | `item/seed-contract.md:135` |
| An entry that should not exist is `enabled: false`, **file kept, id retired forever** | `item/seed-contract.md:201` |
| Deleting a content row is forbidden — *"content is disabled, never deleted (`enabled = 0`)"* | `effect-atom/definitions.md:319` |
| `catalog_revision` is one monotonic integer, bumped **once per import transaction** | `definitions.md:279-283` |
| Config is versioned, never hand-edited; a tool republishes `v{n+1}` and the old file stays | `tunables-ssot.md:88` |
| A missing tunable is a **load rejection naming it**, never a built-in default | `tunables-ssot.md:91-93` |

**The rules this module adds:**

- **R1 — a node id is never reused and never renumbered.** Same standing as every other content id.
- **R2 — a removed node is retired, not deleted.** `enabled: false`, `retiredAtRevision` set. It
  renders greyed, unallocatable, with its retirement printed. So *"what happens to a node that no
  longer exists"* mostly does not arise: the node still exists, it just cannot be bought.
- **R3 — an allocation row naming a retired node is DISPLAYED as invalid, never silently repaired.**
  It grants nothing and, per [`tree-state`](spec-tree-state.md), **costs nothing to hold**. This is
  D11's own rule (*"displayed as invalid (red), never silently repaired"*) applied one layer over.
- **R4 — a catalog revision that retires an ALLOCATED node grants a free full respec.** D18 already
  makes respec a full reset in one transaction, so the migration is a mechanic that already exists, at
  price zero. No partial-refund path, no orphan-unlock cascade, no per-node compensation table — the
  three things the Grim Dawn prior-art finding warns about.
- **R5 — a node id the catalog has NEVER had is rejected ONCE, at the import boundary**, with every
  offending id named in one report. **Never lazily, per actor load.** The defect this rule exists to
  prevent is shipped and live: `AptitudeAllocation.Single` throws
  `ArgumentException("unknown aptitude id …")` at `AptitudeAllocation.cs:39`, and the load path
  calls it **per row** at ~~`RpgStore.Aptitudes.cs:132`~~ **`RpgStore.Aptitudes.cs:149`** — the
  `allocation += AptitudeAllocation.Single(scope, r.GetString(0), r.GetInt64(1))` inside
  `LoadAllocationUnlocked`'s reader loop, verified 2026-09-05. At twelve aptitudes that trade is fine. At
  39 × 40 = 1,560 node ids per actor, one retired id makes the actor **unloadable** rather than red.
  `tree-state` owns the boundary; this module owns the rule and the report format.
- **R6 — a magnitude retune touches no id and migrates nothing.** Ids are structural; coefficients are
  content. A rebalance bumps `catalog_revision`, the next read sees new numbers at the same nodes, and
  no per-actor row changes. **This is the property that makes a live game tunable, and it is why R1
  matters.**

**Version numbering.** Files are `v{n}`, side by side, never hand-edited — the shape
`data/tuning/aptitudes.v1..v5.json` and `data/seed/items/_registry/classes.v1/v2.json` already use.
⚠ One inconsistency worth not propagating: `classes.v2.json` carries `"registryVersion": 4`
internally, so a filename version and an internal counter are not the same number today. **Here they
are the same number, and a test asserts it.**

### 5. Where it ships

```text
data/seed/passive-tree/**.json          THE SEED - enums, shapes, budgets, allocated node keys,
                                        the frozen property vocabulary. No magnitudes.
        |  tools/PassiveTreeGen  (deterministic C#)
        v
data/generated/passive-tree/**.json     CONCRETE - coefficients, committed, diffable, reviewable,
                                        byte-identical on regeneration, identical for every player
        |  import  (all-or-nothing transaction, bumps catalog_revision once)
        v
SQLite: effect_container(kind='skill') + effect_container_pool + the tree's node/link tables
```

**Both directories, and the line between them is already drawn.** `tunables-ssot.md` reserves
`data/tuning/` for *"a number a balance pass would change"*. Node ids, links, structure and which atom
a node grants are **content**, so they live in `data/seed/` and `data/generated/`. The balance dials go
to `data/tuning/passive-tree.v1.json` — which does not exist yet (`data/tuning/` listed 2026-09-05) —
under the canonical names ruling R2 fixes, **each carrying its own unit in the key**:
`concentration.fmaxMilli`, `concentration.wMilli`, `tierLadder.reqScalePoints`,
`soulTrack.thetaPerSoulLevelMilli`, `unlockCost.firstPoints` / `unlockCost.stepPoints`, and
`potency.maxNodeShareMilli`. ~~`Fmax`, `w`, `unlockCost.*`, `tierLadder.k`, `Ws`,
`nodePotencyCeiling`~~ and every `passive-tree-gen.v1.json` variant are superseded — the trap R2 exists
to close is that writing `1.2` into a per-mille key yields `F = 1.0012` and passes every test either
spec currently writes.

**The precedent is on disk, not in a plan.** `data/generated/demons/` holds **831 committed JSON
files** (counted 2026-09-05; the research docs say 830, counted a day earlier — the difference is
uncommitted generation, not a disputed rule). ⚠ Two documents still assert `data/generated/` does not
exist — `demon-seed-map.md:47` and `spec-species-generator.md:24`, the latter now struck through in
place. Do not read either as evidence.

**The generator is C#, not Python**, for `spec-species-generator.md:32-42`'s three reasons, all of
which apply unchanged: it must **call** the shipped `PowerLadder.Value(Θ)` rather than transcribe it
(*"a Python transcription of them is a second curve by another name"*); the widening and precision
decisions do not survive a port; and the runtime is C#. **Seedsmith stops at the seed.**

**Frozen registry.** The property vocabulary D14's exclusions key on is a **frozen** registry file in
`data/seed/passive-tree/_registry/`, following `data/seed/items/_registry/`'s eight `"frozen": true`
files and `bands.v1.json:8`'s own immutability note. Ideal §6 step 2 is explicit that it *"must exist
before any node text is written"* — a generated corpus cannot maintain named-pair exclusions.

### 6. The load path

**Who reads it, and when.**

1. `tools/PassiveTreeGen` writes `data/generated/passive-tree/`. Committed. `--check` gates CI on
   staleness, copied verbatim from `tools/DemonSpeciesGen/Program.cs:17`.
2. A **boot-time importer inside `FusionRpg.Data`** reads the generated files into SQLite in one
   all-or-nothing transaction and bumps `catalog_revision` once. SQL lives only in `FusionRpg.Data`
   (`data-architecture.md`; `scripts/guard-dal.ps1` enforces it and scans `src/`, so a generator in
   `tools/` must not open a connection at all).
3. `tree-state`, `tree-resolve`, `tree-review` and `tree-surface` read the imported rows. None of them
   re-parses a file.

**Validation at load — failing loudly beats clamping.** Every one of these is a refusal naming the
offending row. Never a repair, never a default.

| Check | Refusal |
|---|---|
| `channelId` not in the live vocabulary | reject — the rule `AtomKindRegistry.cs:84-85` already applies |
| `kindId` / `attachPoint` / `trigger` outside the closed vocabularies (16 / 7 / 11 authorable) | reject |
| `scaleAxis` disagrees with the channel's `UnitClass` | reject — §2.4's table is the authority |
| a refused `unitClass` (`Milliseconds`, `Count`, `Flag`, `LadderIndex`, `AptitudePoints`, `LoamUnits`) carrying a non-zero `kMicro` | reject |
| `kMicro <= 0` | reject |
| `budgetShareMilli > potency.maxNodeShareMilli` (both ‰ of **one branch**) | reject — §2.5. ~~`kMicro` above the plan's `nodePotencyCeiling`~~: no such key, and a per-million post-anchor coefficient is not comparable to a per-mille share |
| `affixIds` empty, or longer than 3 | reject (R6) |
| a `prereqNodeId` resolving to nothing, or to another tree | reject |
| a duplicate `nodeId` anywhere in the corpus | reject |
| an authored `nodeId` disagreeing with its coordinates | `IdMismatch`, kept as authored |
| a stored allocation naming an id no catalog revision ever had | reject the **import**, naming every such id in one report (R5) |
| a required tunable missing | reject naming it (`tunables-ssot.md:91-93`) |

**No clamp anywhere on this path.** A clamp turns *"this node stopped mattering"* into a bug with no
symptom (PS-8).

---

## Commands

```powershell
dotnet run --project tools/PassiveTreeGen -- --seed data/seed/passive-tree --out data/generated/passive-tree
dotnet run --project tools/PassiveTreeGen -- --check                  # byte-identity gate; exit 1 on drift
dotnet run --project tools/PassiveTreeGen -- --explain <nodeId>       # every derivation step, shown
dotnet test tests/FusionRpg.Core.Tests --filter TreeCatalog
dotnet test tests/FusionRpg.Data.Tests --filter TreeCatalogImport
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --summary
.\scripts\guard-dal.ps1
.\scripts\guard-power.ps1
```

`--explain` prints the whole chain for one node — plan inputs, tier weight, channel anchor, the single
division, the stored `kMicro`. It is how a balance question gets answered without reading the code.

## Project structure

```text
tools/PassiveTreeGen/Program.cs                                  arguments, --check, --explain
src/FusionRpg.Core/PassiveTree/Catalog/NodeRecord.cs             the record shapes
src/FusionRpg.Core/PassiveTree/Catalog/NodeId.cs                 slug composition + IdMismatch
src/FusionRpg.Core/PassiveTree/Catalog/ScaleAxis.cs              the UnitClass -> axis function
src/FusionRpg.Core/PassiveTree/Catalog/CatalogValidator.cs       every refusal in the load-path table
data/seed/passive-tree/_registry/properties.v1.json              frozen exclusion vocabulary
data/seed/passive-tree/**                                        the seed
data/generated/passive-tree/**                                   committed output
src/FusionRpg.Data/Sqlite/RpgStore.TreeCatalog.cs                the import transaction (the only SQL)
tests/FusionRpg.Core.Tests/PassiveTree/TreeCatalogTests.cs
tests/FusionRpg.Data.Tests/PassiveTree/TreeCatalogImportTests.cs
```

## Code style

```csharp
/// <summary>
/// The catalog stores a COEFFICIENT, never a magnitude: kMicro is this node's share of P(Θ)
/// in per-million. One static file is then correct for every player at every Θ, which is the
/// whole of D24. `long` throughout, widened before the multiply, ONE division and it is last
/// (CLAUDE.md's numeric rules); `checked` so an authored dial that overflows throws instead of
/// wrapping. There is deliberately no Math.Min on the result — a node whose SHARE exceeds
/// potency.maxNodeShareMilli is REFUSED by CatalogValidator, never quietly trimmed (PS-8); the
/// share is what the ceiling is checked against, not this coefficient (§2.5).
///
/// budgetShareMilli is the PLAN's number, per-mille of ONE BRANCH, read not recomputed (R4).
/// tierWeight and weightTotal are gone: the binder used to distribute the tier budget itself,
/// proportional to w[t]*t, while the plan distributes proportional to t — they agree only for the
/// uniform archetype. Gated-deep's capstone landed at 56 permille under the binder's rule against
/// the plan's 91, both as permille of the TREE; restated in this record's denominator (permille of
/// ONE BRANCH, R5) that is 112 against 182. Quote both numbers in one denominator or the gap
/// silently doubles, which is the exact trap R5 exists to close.
/// </summary>
static long NodeKMicro(long budgetShareMilli, long branchBudgetMicro, long channelAnchorMilli)
{
    if (budgetShareMilli <= 0)
        throw new ArgumentOutOfRangeException(nameof(budgetShareMilli), budgetShareMilli, "share must be positive");

    checked
    {
        var num = budgetShareMilli * branchBudgetMicro * channelAnchorMilli;
        return RoundHalfAwayFromZero(num, 1_000_000L);   // the only division
    }
}
```

`RoundHalfAwayFromZero` is the convention `PowerLadder`, `ContentScale.Apply` and `ChannelLadder`
already share — nothing new is invented.

## Testing strategy

| Test | Asserts |
|---|---|
| `regenerating_unchanged_seeds_is_byte_identical` | the `--check` gate |
| `inserting_a_node_does_not_change_any_existing_id` | §3's whole point |
| `retuning_a_magnitude_does_not_change_any_id` | R6 |
| `a_retired_node_keeps_its_id_and_is_never_reissued` | R1 + R2 |
| `an_authored_id_disagreeing_with_its_coordinates_is_IdMismatch` | kept as authored, not rewritten |
| `no_node_id_contains_a_dot_in_its_body` | the `container_id` grammar |
| `every_stored_magnitude_field_is_long` | reflection over the record types; a `float` or `int` fails |
| `no_catalog_field_stores_a_resolved_magnitude` | reflection — the record carries `kMicro` and no absolute number |
| `an_unregistered_channel_is_refused_at_load` | not silently written |
| `scale_axis_matches_unit_class_for_every_atom` | §2.4's table, over the whole corpus |
| `a_sigmoid_channel_never_carries_the_pTheta_axis` | PS-3; the failure this catches is silent |
| `a_refused_unit_class_with_a_nonzero_coefficient_is_rejected` | `Flag`, `Count`, `LadderIndex` … |
| `a_dangling_prereq_is_rejected_naming_the_node` | never repaired |
| `a_node_carries_between_one_and_three_affix_ids` | R6 — zero is a refusal, four is a refusal |
| `a_reflect_node_ships_its_two_atoms_under_one_affix` | why the roll unit is an affix and one is not enough |
| `a_node_share_above_the_ceiling_is_refused` | §2.5, driven by a **synthetic** over-budget row. There is deliberately no test asserting the shipped corpus proves the ceiling binds — it cannot, and saying so is the point |
| `the_ceiling_and_the_share_use_the_same_denominator` | ‰ of one branch on both sides; the 2× the old `budgetTotal` form hid |
| `a_plan_category_token_outside_the_five_is_refused_naming_it` | R7, and the `aptitude`/`demonFamily` rename |
| `regeneration_reads_the_allocated_node_key_and_never_recomputes_it` | R3 — the property `tree-review`'s `O(diff)` rests on |
| `import_is_all_or_nothing_and_bumps_revision_once` | `definitions.md:279-283` |
| `an_unknown_node_id_fails_the_import_not_an_actor_load` | R5 — the defect at `AptitudeAllocation.cs:39` is not repeated |
| `no_math_min_on_any_coefficient_path` | grep test, PS-8 |
| `filename_version_equals_catalogVersion_field` | the `classes.v2.json` trap, refused |
| `overflow_throws_never_clamps` | a deliberately enormous plan budget |

## Boundaries

**Always:** store coefficients; `long` for every magnitude field; widen before multiplying; divide by
1000 exactly once, last; compose the id from the plan's allocated coordinates; commit the generated
output; refuse at load naming the offending id; keep `data/seed/` and `data/generated/` in the shape
the demon program already uses.

**Ask first:** adding a field to `NodeRecord` — it widens a contract four modules read. ⚠ **Two such
changes were made on 2026-09-05 under the cross-spec rulings and are called out rather than slipped in:**
`affixId` (string) became `affixIds` (1..3 strings) per R6, and `budgetShareMilli` was added per R4/R5
because without it the potency ceiling has nothing dimensionally valid to compare against.
`weightTotal` came *off* `TreeRecord` in the same pass. Also ask before: allowing a
node to author a **bake-time-resolved slot** (open question 3); changing the id format after the first
corpus is authored, because a reused or reshaped id repoints every reference that already resolved.

**Never:** bake an absolute magnitude into the catalog; use a content hash or a positional ordinal as
a node id; reuse or renumber an id; delete a content row (disable it); clamp a coefficient; write SQL
outside `FusionRpg.Data`; write a private `f(level)` — magnitudes read `P(Θ)` through the shared
`PowerLadder` and contests read `Θ`; allocate budget to a **conversion node** (D16) until a 17th atom
kind lands, because no kind among the 16 writes an element payload and the failure is **silent**
(`OverlayCombatCalculator.cs:128-172` loops the payload's own components, so an ice affix on a payload
with no ice component contributes zero forever, with no error).

**Dependencies this module names but does not own:**

- **`ValueSpec.PowerLadderKMicro`** — the per-million sibling; `ValueSpec.cs:92` ships per-mille. A
  reviewed change to the atom layer, ~3 lines at `AtomCompiler.cs:456-466`. Without it a tier-1 node
  rounds with ~~17%~~ **+63%** error at D29's ten tiers (`spec-tree-binder.md:189`). **A wiring gap,
  not a wall.**
- **The 17th atom kind** for D16's element-payload conversion — a reviewed `decisions.md` change.
- **A derived-tag vocabulary** for D14. `AffixTags.cs` ships (124 lines, tested) but has no production
  call site, and the corpus carries **3** semantic tag values, so a property-keyed exclusion can key on
  posture and little else today. **Soft blocker:** the record can carry `excludeProps` before the
  vocabulary is rich.

## Success criteria

- [ ] Every strength number in the corpus is a `kMicro` coefficient; no absolute magnitude is stored,
      proven by reflection over the record types.
- [ ] `--check` is green in CI and fails on a stale generated tree.
- [ ] Inserting, retiring and retuning each leave every surviving id byte-identical, proven by test.
- [ ] Every load-path refusal in §6 has a test, and none of them repairs or clamps.
- [ ] `scripts/audit-overflow.py` reports no critical finding in this module.
- [ ] `scripts/guard-dal.ps1` green — no SQL outside `FusionRpg.Data`.
- [ ] `--explain` shows the complete chain for any node id.
- [ ] The frozen property registry exists **before** the first node text is authored.

## Open questions

Three, all genuine; none blocks the module's own structure.

1. **Where does a soul level enter the curve?** `CurveInput` has exactly three members —
   `Level`, `Rarity`, `Tier` (`CurveTable.cs:4-9`) — and its own comment says *"Adding one is a
   reviewed change (E2 boundaries)"*. Does a node's soul track ride `Level`, or earn a fourth member?
   That is an E2 decision, not one this module may make. It changes what `soulCurveId` means, not
   whether the field exists.
2. **Does the species tree (D23/D30) ship as catalog data, or derive at import?** Both satisfy §1's
   freeze line. Shipping the data makes ~~~35,200~~ **35,160** nodes across **879 trees** reviewable
   and diffable (840 species × 40 + 39 generic × 40; counted in `data/seed/demons/species/_index.json`,
   and `tree-review` §1.1 is the source the whole program cites); deriving keeps the repo
   smaller and moves the review surface into a generator. The demon program chose *ship the data*
   (`demon-seed-map.md:41`), and map assumption 4 says species trees reuse this record — so the size
   is the owner's call, not this document's. Owned by `species-tree`; recorded here because it is this
   record's blast radius.
3. **May a node author a slot resolved at bake time (L2)?** Allowing it lets one authored node fan out
   across six elements at bake time and stay fully learnable; forbidding it keeps the record smaller.
   Neither breaks the freeze line — only *player-time* slot resolution would.

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §1 freeze line; the catalog is committed content, identical for every player | **D24** |
| §1 (c) is empty; `skill` containers use the fixed core alone | **D24**, on `spec-container-schema.md:50-56` |
| §2.2 `nodeClass` distinguishes mechanism from magnitude nodes | **D13** as extended by ideal §3.5 |
| §2.2 `excludeProps` + `tagsJson` — property-keyed exclusion, never a named-pair list | **D14**, **D22** |
| §2.2 `branch` is two; `tier` is 1..10; **40** nodes per tree | **D10**, **D29** |
| §2.1 `shapeArchetype` — equal expected value, not equal shape | **D15** |
| §2.3 `soulCurveId` — souls are a curve read, never a roll | **D3** |
| §2.3 `kMicro` — coefficients, not magnitudes | **D24** plus the one-ladder rule |
| §2.4 refuses `AptitudePoints` by construction | **D11**, **D12** |
| §3 composed structural slug; ids survive a rebalance | **D24** (ideal §10.2, *"node id stability becomes load-bearing"*) |
| §4 R3 — a retired allocated node shows red, never silently repaired | **D11** |
| §4 R4 — the migration escape hatch is a free full respec | **D18** |
| §4 R5 — reject once, at the import boundary | ideal §11.2's *"migration fails hard"* row |
| §5 one record type serves generic and species trees | **D23**, **D30** (map assumption 4) |
| §5 the roster ships whole; a category can land in any order | **D27**, **D9** |
| Boundaries — no budget for conversion nodes yet | **D16** (a real gap, ideal §13.3) |

**Belongs to a sibling module, not here:** D2/D25/D34/D36 and the per-actor store
([`tree-state`](spec-tree-state.md)) · D4/D5/D6/D7/D8/D28 and every `P(Θ)` multiply (`tree-resolve`) ·
D13's plan stage, D15's distribution engine, D20/D26's ladder, D32's targets and D35's gate quantity
(`tree-plan`) · D17/D23/D30's own pipeline (`species-tree`) · D33 (`squad-harness`) · D1 (a standing
fact about the class system, implemented by no module). **D19, D20 and D31 are superseded** — by D35,
D26 and D35 respectively — and are implemented nowhere, by design.
