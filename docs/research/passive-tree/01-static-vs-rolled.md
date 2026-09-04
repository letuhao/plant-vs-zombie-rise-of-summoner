# Passive trees — what is baked, what is state, what is rolled (2026-09-05)

Enriches [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) (23 owner decisions,
D1–D23) against the owner constraint stated 2026-09-05:

> *"the passive skills tree need concrete value before the game run / it is different with item loot
> mechanism / it need solid stats, so user can learn it, if it random every new player create, it will
> cause confuse, use cannot built because they need to relearn"*

**Evidence marking follows the convention already used in
[passive-tree-prior-art-2026-09-04.md](../passive-tree-prior-art-2026-09-04.md).**
FACT = quoted or counted from code or a cited document. INFERENCE = drawn from a fact.
RECALL = general knowledge, unverified in-repo — a lead, not a citation.

---

## 1. Answer up front — the freeze line

**The constraint and the principle do not conflict.** The seed-to-concrete law is a law about *where
magnitudes come from*, not a law that every generator must roll per player. The repo already ships the
exact split the owner is asking for, in the demon program, and it is written down in three places as
**two layers**: shared deterministic stats, per-player rolled effects.

| Layer | For the passive tree | When it is decided | Same for every player? | Precedent in repo |
|---|---|---|---|---|
| **(a) Baked at BUILD time, shipped as data** | tree roster · tree shape · node ids · node links · tier gates (`req(t)`, D20) · which atoms each node grants · each node's authored magnitude and its soul-scaling curve · exclusion properties (D14) | dev machine, before packaging | **YES — identical for all** | `data/generated/demons/**.json`, 830 files, committed (counted 2026-09-05) |
| **(b) Per-ACTOR state** | which nodes are allocated · soul level per node · earned/spent skill points · gear-granted points (D11) | player's machine, as they play | no — this is the build | `rpg_aptitude_allocation` (scope, scope_key, aptitude_id, points) |
| **(c) Rolled per player at runtime** | **nothing** | — | — | — |

**(c) should be empty, and stays empty.** Argued in §4. Per-player variance is not removed from the
game by this; it already has three homes that are not the tree — rolled item affixes, the
`species-passive` container that rolls per player (`demon-seed` module 16), and D11's gear-granted
points, which change *what you can afford* without changing *what the tree is*.

**The one-line version:** the passive tree is a **catalog**, and catalogs are content. Content is
shared and hashed; player state is not. That line is already drawn in code —
`spec-content-hash.md:31`: *"The line is exactly the code-or-data rule's line: **content is hashed,
player state is not.**"*

---

## 2. The seed-to-concrete principle, read at its source

### 2.1 What it actually says

FACT — `effect-pipeline-ideal.md:24-40`, the owner's own words, marked *"binding for **every**
generation feature"*:

> *"Seedsmith generate seed, game generator in game runtime generate concrete object, per player game
> store that object … every generator use this sdk baseline, so no need to duplicated code for all."*

```text
SEED       seedsmith, offline, enums only, no magnitudes       committed, diffable
   |
   v       the GAME RUNTIME resolves it, seeded
CONCRETE   a frozen atom list with rolled values
   |
   v
STORED     that player's own tables. "each player play they own game"
```

**INFERENCE — this mandates a SEED/CONCRETE split, not per-player randomness.** The word doing the
work is *seeded*, and a seeded resolution whose seed is the same for everyone is deterministic and
shared. The law forbids two things: a model picking a number, and a subsystem shipping hand-authored
magnitudes it derived privately. It does not require the resolution's input seed to be per-player.

### 2.2 The precedent for a shared-deterministic layer is explicit and it is not one line

FACT — `tasks/seed-to-concrete-plan.md:40-41`, under *Architecture decisions (locked in the ideals)*:

> **Two layers.** Species *stats* are deterministic and shared; only *effects* roll.

FACT — `DESIGN-GATE.md:45`, the *Demon species generation* row:

> Species *stats* are deterministic and shared; only *effects* roll, per player, at runtime.

FACT — `docs/architecture/demon-seed/spec-species-generator.md:163-166`, the module's own Boundaries
block:

> **Scope note (2026-09-01).** This module produces the **shared** layer only — stats, which are
> deterministic and identical for every player. The per-player *effect* roll is `player-materialise`
> (module 16) … **Only effects roll; stats never do.**

FACT — `demon-seed-map.md:180-183`, recording that the per-player decision was checked against modules
11–13 and did *not* invalidate them:

> the owner's **two-layer** answer — shared definitions, per-player materialisation — keeps species
> *stats* deterministic and global … **Only effects roll.**

FACT — the shared layer is not a plan, it is on disk. `data/generated/demons/` holds **830** committed
files (counted 2026-09-05). One row, `data/generated/demons/Bamboo.json`, carries `theta: 13`,
`pTheta: 452`, and ten `resource.*` magnitudes — real numbers, checked in, identical for every player.
`demon-seed-map.md:41` labels that stage **`CONCRETE — checked in, diffable, reviewable`**.

⚠️ **Two documents are stale on this and will mislead a later session.** `demon-seed-map.md:47` and
`spec-species-generator.md:24` both say *"`data/generated/` does not exist"* / *"is absent from the
repo"*. It exists, with 830 files. Verified by listing, not inferred.

### 2.3 The tree's own container kind already refuses to roll

FACT — `effect-atom/spec-container-schema.md:22` lists the six container kinds:
`item | trait | skill | species-passive | patron | world-buff`. A passive skill is a `skill` container.

FACT — `spec-container-schema.md:50-56`, the note that superseded the old "core alone" claim:

> **"Traits, skills, and species passives use the core alone" is superseded 2026-09-01 … species
> passives now roll a pool too … `trait.{traitId}` containers (fusion, Q10) and `skill` containers
> **still use the core alone** — this document's original claim held for those, not for
> `species-passive`.** Item templates and `species-passive` containers roll the pool.

**This is the finding that settles the charter's first question.** The one amendment that turned a
non-rolling container kind into a rolling one was applied to `species-passive` and deliberately not to
`skill`. The passive tree's own container kind is, in the shipped contract, a **fixed core with no
pool** — exactly the "concrete value before the game run" the owner is asking for. Nothing needs to
change to get it; something would have to change to lose it.

FACT — no `skill`-kind container is authored today: across the four files in `data/seed/containers/`
there are 7 entries — 5 `item`, 1 `patron`, 1 `trait`, 0 `skill` (counted 2026-09-05). So the tree is
not fighting existing content; it is the first author of that kind.

---

## 3. Where a node's effect lives in L0–L4

The resolution model is normative in `effect-atom/definitions.md` §4a (`definitions.md:179-252`) and
designed in `effect-pipeline-ideal.md` §5. Mapping the tree onto it:

| Layer | What it decides | Does a passive node use it? |
|---|---|---|
| **L0** — pool composition | which affixes are even candidates, at what rate, given power/rarity class | **No.** L0 exists to stop a strong affix dropping from a weak source (`effect-pipeline-ideal.md` §5.6). A tree node is not a drop; it is a purchase at a printed price |
| **L1** — container shape | how many atoms, chance each appears | **Yes, degenerately.** The node is a fixed core with `prefix_rolls = suffix_rolls = 0`. `spec-container-schema.md:50` — *"A container with no pool rows is a plain fixed list"* |
| **L2** — channel pool / slots | *which* derived stat, chosen at resolve time | **No for the tree's own identity.** A node that reads *"+X power of a random element"* is unlearnable by construction. If a tree ever wants element breadth it authors a slot **resolved at bake time**, not at player time |
| **L3** — value range | the min/max a magnitude rolls into | **No range.** `RollPolicy.Fixed` — *"Never resolves — the number is the number. `Min == Max`"* (`ValueSpec.cs:12-13`). `data/seed/README.md:70-72`: a range needs a roll policy, and *"a fixed value with a range is refused"* |
| **L4** — resolve | pick atoms, pick stats, freeze numbers | **Yes, but with nothing left to pick.** L4 still runs — it is what produces `effect_instance` / `effect_binding` and puts atoms on an actor's list. With no pool, no slot and no range, it is a bind, not a draw |

**So: a passive tree node's effect lives at L1 (fixed core) + L4 (bind), and deliberately does not use
L0, L2, or L3's range.** INFERENCE, but the mechanism it rests on is FACT: every layer it skips is
optional in the shipped schema, and skipping all three is the already-named shape *"a plain fixed
list"*.

**D3's soul track is a curve read, not a roll.** FACT — `CurveTable.cs:4-9` declares
`CurveInput { Level, Rarity, Tier }` and `CurveTable`'s own summary says *"Scaling is a curve
reference, never a formula."* A node's magnitude at soul level *k* is therefore
`authored value × curve(k)` — deterministic, printable at every level, identical for every player who
reaches that level. That is what makes the tree *plannable*: a player can read the level-9 value before
spending on level 1.

⚠️ **One schema question this raises, flagged not answered.** `CurveInput` has three members and its
own comment says *"Adding one is a reviewed change (E2 boundaries)"* (`CurveTable.cs:3`). Whether a
soul level rides `CurveInput.Level` or earns a fourth member is a reviewed change to E2, not a
decision this document may make.

---

## 4. The freeze line in detail, and why (c) is empty

### 4.1 (a) — baked at build time, identical for all

Everything that a build guide would need to quote:

- the tree roster (D9: 12 primary + all elemental + all status + each demon family, `n ≈ 40–60`)
- per tree: the 2 branches × tiers shape (D10), and its shape archetype (D15)
- per node: id, tier, branch, prerequisite links, the atoms it grants, each atom's authored magnitude,
  the curve its soul track reads, its exclusion properties (D14), and whether it is a **mechanism** or
  a **magnitude** node (the D13 requirement §3.5 of the ideal added)
- the tier gate ladder `req(t) = 10 + 2.5·t·(t−1)` (D20)
- `Fmax` and `w` — but these are **tunables**, not catalog: `data/tuning/passive-tree.v{n}.json`
  (`tunables-ssot.md:38`, and the ideal §8 already says so)

### 4.2 (b) — per-actor state

FACT — the shape already exists and is in production. `RpgStore.Aptitudes.cs:36-44`:

```sql
CREATE TABLE IF NOT EXISTS rpg_aptitude_allocation (
  scope TEXT NOT NULL, scope_key TEXT NOT NULL,
  aptitude_id TEXT NOT NULL, points INTEGER NOT NULL,
  PRIMARY KEY (scope, scope_key, aptitude_id));
```

Three properties of it transfer to the tree directly, and each is already argued in that file:

1. **Inputs only, never resolved values.** `RpgStore.Aptitudes.cs:8-11`: *"INPUTS only, never a
   resolved channel value … a stored channel value would be a second SSOT that goes stale the moment a
   coefficient moves."* A tree row stores `{nodeId → soulLevel}` and an allocated flag, never the
   node's current magnitude.
2. **Sparse by construction.** `AptitudeAllocation.cs:42-44` returns `Empty` for `points == 0`, so a
   zero never becomes a row. D21's "sparse storage is a hard requirement" (~1,450 possible per-skill
   soul levels per actor) has a working precedent, not just a preference.
3. **The `(scope, scope_key)` key is already the D21 key.** The same two columns address a commander,
   a demon type, an aspect and a unique demon (`RpgStore.Aptitudes.cs:23-31`). "Every actor carries its
   own tree state" needs no new addressing scheme.

FACT — this store is no longer callerless. `SaveAllocation` / `LoadAllocation` have **6** production
call sites across `AptitudeEndpoints.cs:52,80`, `AuraDerivedEndpoints.cs:59`, and
`WebMatchService.cs:264,417` (counted 2026-09-05).

FACT — the spender for skill points still does not exist. `SkillPointsPerThetaMilli` appears exactly
three times in the tree: declared at `AptitudeTuning.cs:13`, parsed at `AptitudeTuning.cs:156`,
asserted once in `AptitudeTuningTests.cs:90`. Zero production consumers. D2's claim holds as written.

### 4.3 (c) — why nothing rolls, including for a demon species tree

Four arguments, strongest first.

**1. The owner constraint is a design requirement, not a preference.** A rolled catalog makes a build
guide false for the reader. Every comparator hand-authors its tree
(`passive-tree-prior-art-2026-09-04.md` §6, *"No comparator generates a passive tree"*), and the thing
we are generating is the *authoring*, not the *player's copy*.

**2. The tree's leverage depends on being learnable.** The ideal's own measured conclusion is that
*"A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM"*
(passive-tree-ideal §3.5). A mechanism node — reflect, damage-taken scaling, anti-turtle punish — is
only worth building toward if the player can see it before committing 115 points to reach tier 7. A
rolled mechanism at depth is a lottery ticket, not a build.

**3. Per-player variance already has three homes, and none of them is the tree.** Rolled item affixes
(`item` containers roll the pool — `spec-container-schema.md:50`); the per-player species passive roll
(`demon-seed` module 16, `demon-seed-map.md:177-178`); and D11's gear-granted points, which vary what
you can afford without varying what the tree is. Adding a fourth would buy variance the game already
has, at the cost of the one property this layer needs.

**4. The species tree (D17/D23) is static too, and the precedent is exact.** A species tree is derived
from a species anchor, and species anchors are **shared seed** — `data/seed/demons/species/**.json`,
841 entries across 503 files (the ideal's §9 count). The layer above them, the concrete stat block, is
already *"deterministic and identical for every player"* (`spec-species-generator.md:163-165`). A tree
derived from that anchor inherits the same standing by construction. Its uniqueness (D23) is
**per-species**, not per-player: every player who owns that species sees the same unique nodes, which
is what makes it a collection goal rather than a re-roll.

**The honest counter-argument, stated and rejected.** One could keep node identity fixed and roll only
each node's *magnitude* per player — "same tree, different numbers". That would technically satisfy
seed-to-concrete's letter. It fails the owner constraint's own words (*"it need solid stats"*), it
breaks build sharing (two players compare the same node and disagree), and it would make the ideal's
tier-power pairing rule (D20: per-tier power grows linearly) unverifiable, because there would be no
single per-tier power to verify. Rejected.

**Capability note, so this is not misread as a limitation.** Rolling *is* mechanically available: a
per-player world seed exists and reaches the whole save (`effect-pipeline-ideal.md:757,762` — Q2 and
Q7). Not rolling the tree is a **design choice made against an available mechanism**, not a gap.

---

## 5. What artifact ships the catalog, and where it lives

### 5.1 The two-directory shape, copied from the demon program

FACT — `demon-seed-map.md:33-44` and `item/seed-contract.md:32` both use the same chain, and
`spec-species-generator.md:28-30` says explicitly that its shape is *"a **precedent**, not just a
feature. The shape chosen here is the shape the item and action programs will follow."*

```text
data/seed/passive-tree/**.json         THE SEED — enums, shapes, budgets, properties; no magnitudes
        |  tree-generator   (deterministic C#: req(t), P(Θ), per-tier power, curve refs)
        v
data/generated/passive-tree/**.json    CONCRETE — checked in, diffable, reviewable, identical for all
        |  import          (all-or-nothing transaction, bumps catalog_revision)
        v
SQLite: effect_container(kind='skill') + effect_container_atom + the tree's own node/link tables
```

**`data/seed/`, not `data/tuning/`.** `tunables-ssot.md:38` draws the line: `data/tuning/` holds *"a
number a balance pass would change"* — costs, rates, weights, thresholds. The tree catalog is
*content* (ids, links, structure, which atom a node grants), and content lives in `data/seed/`. Both
directories are used: `Fmax`, `w`, and the `req(t)` coefficients go to
`data/tuning/passive-tree.v{n}.json`; the nodes go to `data/seed/passive-tree/`.

### 5.2 The generator must be C#, for a reason already argued

FACT — `spec-species-generator.md:32-42` gives three reasons the expander is C# and not Python, and all
three apply unchanged here: it must call the shipped `PowerLadder.Value(Θ)` and
`AptitudeReadFunctions.Magnitude` rather than transcribe them (*"a Python transcription of them is a
second curve by another name"*); `Magnitude` uses `decimal` as its widening type and *"that precision
decision does not survive a port"*; and the runtime is C#. **Seedsmith stops at the seed.**

### 5.3 The gate that makes "identical for every player" true rather than hoped

FACT — `tools/DemonSpeciesGen/Program.cs:17`:

> `--check    compare against what is on disk; write nothing; exit 1 if anything differs`

and `spec-species-generator.md:98-100`: *"The generated tree is committed, canonically serialised, and
regenerating over unchanged seeds produces byte-identical files. A `--check` mode gates CI on
staleness."* Its test list (`spec-species-generator.md:149`) names
`regenerating_unchanged_seeds_is_byte_identical` as the assertion behind it.

**Copy this exactly.** A tree generator ships `--check` and CI runs it. Without that gate, "the catalog
is the same for everyone" is a claim about a build machine.

### 5.4 The frozen-registry pattern the seed half should use

FACT — `data/seed/items/_registry/` holds 8 files carrying `"frozen": true` (counted 2026-09-05), each
with `schemaVersion`, `registryVersion`, and a `_meta` block that states its own immutability rule.
`bands.v1.json:8` is the model:

> *"FROZEN v1 once an owner flips `frozen` to true and authoring begins against it … A change to any
> number in this file after freeze is v2, minted only after an explicit owner re-run decision — never
> an in-place edit."*

And `build-themes.v1.json`'s `_meta.notFrozen` shows the deliberate opposite case, with its reason
written down. A passive-tree seed would want the same pair: a frozen vocabulary registry (node
property vocabulary — D14's exclusion keys, which §6 of the ideal says *"must exist before any node
text is written"*), and an unfrozen derived registry for anything read off a roster that is allowed to
grow.

---

## 6. Versioning and migration

### 6.1 The rules the repo already enforces, which decide most of this

| Rule | Source |
|---|---|
| An id is **never reused** — *"not after a deletion, not after a rejected review, not ever"* | `item/seed-contract.md:135` |
| An entry that should not exist is `enabled: false`, **file kept, id retired forever** | `item/seed-contract.md:201` |
| Deleting a content row is **forbidden** — *"content is disabled, never deleted (`enabled = 0`)"* | `effect-atom/definitions.md:319` |
| Ordinals are **append-only**; an ordinal already held is *"refused rather than renumbered underneath the content naming it"* | `data/seed/README.md:109-111` |
| `catalog_revision` is one monotonic integer, bumped **once per import transaction**, only when something changed | `definitions.md:279-283`; `data/seed/README.md:23-25` |
| Reproduction contract: same `(container_id, catalog_revision, roll_seed)` ⇒ identical rows | `definitions.md:261` |
| Config is versioned, never hand-edited; a tool republishes `v{n+1}` and the old file stays | `tunables-ssot.md:88` (T4) |
| A missing tunable is a **load rejection naming it**, never a built-in default | `tunables-ssot.md:93` (T5) |

FACT — the file-name-versioning is real, not aspirational: `data/tuning/` holds `aptitudes.v1..v5`,
`loam.v1..v4`, `power-scale.v1..v2`; `data/seed/items/_registry/` holds `classes.v1.json` and
`classes.v2.json` side by side (listed 2026-09-05). ⚠️ Note the one inconsistency, since a later
session will hit it: `classes.v2.json` carries `"registryVersion": 4` internally. The filename version
and the internal counter are **not** the same number today.

### 6.2 Proposed rule for the passive tree

**R1 — a node id is never reused and never renumbered.** Same standing as every other content id.

**R2 — a removed node is retired, not deleted.** It stays in the catalog with `enabled: false`. It
renders greyed, unallocatable, with its retirement printed. This is the existing rule, and it means the
question *"what happens to a node that no longer exists"* mostly does not arise: the node still exists,
it just cannot be bought.

**R3 — an allocation row naming a retired node is DISPLAYED as invalid, never silently repaired.**
This is not a new pattern; it is D11's own, borrowed from Last Epoch and already adopted in the ideal:
*"affected nodes are **displayed as invalid (red), never silently repaired**"*
(passive-tree-ideal D11). The points it holds do not grant their effect.

**R4 — a catalog version bump that retires an allocated node grants a FREE full respec.** D18 already
makes respec a full reset of skill distribution *and* primary stats in one transaction, priced in
souls, with `pointEconomy.respecPrice` shipped. **The migration is therefore the mechanic that already
exists, at price zero.** No partial-refund path, no orphan-unlock cascade, no per-node compensation
table — all three of which the Grim Dawn finding warns about
(`passive-tree-prior-art-2026-09-04.md` §2.4). INFERENCE, but the cheapness is a direct consequence of
D18, and D18's own text already claims it *"dissolves the Grim Dawn order-sensitivity problem
entirely."*

**R5 — an allocation row naming an id the catalog has NEVER had is a load rejection naming it.** Not a
silent drop, not a default. Same discipline as `tunables-ssot.md:93` and as
`RpgStore.Aptitudes.cs:47-50`'s own *"unknown scope rejects … Throws naming the bad value rather than
defaulting."*

**R6 — a magnitude retune does not touch ids and does not migrate anything.** Node ids are structural;
magnitudes are content. A rebalance bumps `catalog_revision`, the player's next read sees new numbers
at the same nodes, and nothing in (b) changes. This is the property that makes a live game tunable, and
it is the reason R1 matters.

⛔ **A defect this analysis found in the existing per-actor precedent, stated plainly.**
`RpgStore.Aptitudes.cs:129-130` loads by calling `AptitudeAllocation.Single` per row, and
`AptitudeAllocation.cs:38-39` **throws** `ArgumentException("unknown aptitude id …")` on an id the
catalog does not know. So a single unrecognised row does not degrade — it makes the whole actor
unloadable. At twelve aptitudes that is a fine trade. At D21's scale (~50 trees × ~29 nodes × every
actor), R5 must be applied at a **defined migration boundary** (import time, once, with the offending
ids named in one report), never lazily on every actor load. Otherwise one bad row bricks a save.

---

## 7. Node id stability

### 7.1 What the generator must guarantee

For a build guide written on 2026-10-01 to still be true on 2027-01-01, one property is needed:

> **The same conceptual node keeps the same id across every regeneration in which it still exists,
> regardless of what was added, removed, retuned or reordered around it.**

### 7.2 The three candidate schemes

| Scheme | What breaks |
|---|---|
| **Content hash** (`node-<sha of its effect>`) | ⛔ **Every rebalance renames every node it touched.** The id would change whenever a magnitude moves, so R6 becomes impossible and every retune orphans allocations. It also collides with the hash's existing job: `spec-content-hash.md` computes a content hash precisely to make *a changed number visible* — using it as identity would make change and identity the same thing |
| **Positional ordinal** (`fire-tree-node-07`) | ⛔ **Insertion renumbers everything after it.** The repo already refuses this shape for exactly this reason — `data/seed/README.md:109-111`: an ordinal is *"never renumbered underneath the content naming it"* |
| **Composed structural slug** ✅ | Nothing, provided the coordinates it composes are themselves stable |

### 7.3 Recommendation

**A composed structural slug, built from the deterministic plan's own stable coordinates**, following
the `atom_id` derivation the atom layer already ships — `{family}[.{variant}].t{tier}`, with an
authored id that disagrees being an `IdMismatch` refusal rather than a silent rewrite
(`data/seed/README.md:62-64`).

```text
skill.<treeId>-<branch>-t<tier>-<nodeKey>

  treeId    from the tree roster (D9) — an authored, allocated id, never a position
  branch    off | def                  — D10's two branches
  tier      the tier gate it sits behind (D20)
  nodeKey   allocated once by the plan within (tree, branch, tier), never reclaimed,
            never derived from the node's effect or its display order
```

**Hard constraint on the format, verified:** `container_id` allows **no dot in its body** — the grammar
is `^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`
(`item/seed-contract.md:131-133`), and the note there records that *"Two lanes discovered this the hard
way."* So every separator inside the body is a hyphen.

**What makes `nodeKey` stable is allocation, not derivation.** This is the mechanism
`item/seed-contract.md:121-123` already uses: a namespace is allocated per partition, the sequence
within it belongs to the author, ids are never reused, and *"Global uniqueness validation then catches
operational mistakes rather than doing the work."* Applied here: the deterministic planner allocates
each node's key once and records it in the seed; regeneration reads the allocated key rather than
recomputing a position. That is the difference between a stable id and an ordinal wearing a slug's
clothes.

**Test to write with the generator, mirroring the demon precedent's own list
(`spec-species-generator.md:143-151`):**

- `regenerating_unchanged_seeds_is_byte_identical` — the `--check` gate
- `inserting_a_node_does_not_change_any_existing_id`
- `retuning_a_magnitude_does_not_change_any_id`
- `a_retired_node_keeps_its_id_and_is_never_reissued`

---

## 8. The three biggest risks

**Risk 1 — the catalog is large enough that "generated once, shipped" is the only affordable shape,
and nobody has costed it.** D9's roster (`n ≈ 40–60`) at Last Epoch's ~29 nodes per tree is ~1,450
nodes (the ideal's own §7 figure), and D23 adds *per-species* unique nodes against a corpus of 841
entries in 503 files. FACT for scale comparison: `data/generated/demons/` is 830 files today and is
committed without apparent trouble. INFERENCE: a per-species tree corpus is roughly the same order,
plus the generic trees. That is fine as data, but it is a real review-surface question the plan owes an
answer to, and it is the one place the "bake it" answer costs something.

**Risk 2 — the migration boundary is undefined, and the shipped precedent fails loudly at actor
load.** §6.2's ⛔ note. `AptitudeAllocation.cs:38` throws on an unknown id, reached from
`LoadAllocation`. Applied unchanged to 1,450-node trees across every actor, one retired node in one
player's save is an unloadable actor rather than a red node. R5 has to name *where* the rejection
happens or it becomes an outage.

**Risk 3 — two authoritative documents currently state the opposite of the on-disk truth about
`data/generated/`.** `demon-seed-map.md:47` and `spec-species-generator.md:24` both say it does not
exist; it holds 830 files. A session reading the map to decide "does the shared-deterministic layer
exist" gets the wrong answer from the index. Cheap to fix, expensive to hit.

---

## 9. Open questions for the owner

1. **Where does a soul level enter the curve?** `CurveInput` has three members (`Level`, `Rarity`,
   `Tier`) and adding one is a reviewed E2 change (`CurveTable.cs:3-9`). Does a node's soul track ride
   `Level`, or does soul level earn a fourth input?

2. **Does the species tree (D23) ship as catalog data, or as a derivation run at import?** Both satisfy
   §1's freeze line. Shipping it as data makes the corpus reviewable and diffable at 841-entry scale;
   deriving it at import keeps the repo smaller but moves the review surface into a generator. The
   demon program chose *ship the data* (`demon-seed-map.md:41`), and consistency argues for the same
   choice — but the size is the owner's call, not this document's.

3. **Is a free full respec on catalog change acceptable as the whole migration story (R4)?** It is
   cheap because D18 made respec a full reset. It also means a rebalance hands every player a free
   rebuild, which is a real economic decision, not just a migration one.

4. **Does a tree node ever get to author a slot resolved at bake time (§3, L2)?** Allowing it lets one
   authored node fan out across six elements at bake time and stay fully learnable. Forbidding it keeps
   the node schema smaller. Neither breaks the freeze line; only *player-time* slot resolution would.

---

## 10. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, the effect/atom container
    layer, seed-to-concrete generation, tunables, data architecture.
[x] I read every doc in the §1 row(s) for those subsystems, this session:
    DESIGN-GATE.md; effect-pipeline-ideal.md §5 + §0/§1/§3.5/§7; effect-atom/definitions.md
    §4a/§5/§6; effect-atom/spec-container-schema.md; effect-atom/spec-content-hash.md;
    demon-seed-map.md; demon-seed/spec-species-generator.md; item/seed-contract.md;
    tunables-ssot.md; tasks/seed-to-concrete-plan.md; passive-tree-ideal.md;
    research/passive-tree-prior-art-2026-09-04.md.
[x] I checked decisions.md for a lock covering this — the Demon program row (line 97) and the
    Class system row (line 103) are the two that touch it; neither is contradicted.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — container kinds, RollPolicy, CurveInput,
    the allocation schema, the throw in AptitudeAllocation.Single, the caller counts, the
    file counts in data/generated and data/seed/containers.
[x] I read the surrounding section of every rule I quoted — in particular the "core alone"
    supersession, which reads the opposite way if only the superseding half is quoted.
[ ] I tested (not assumed) any constraint I am reporting. NOT DONE: no test suite was run.
    Nothing here proposes a code change, and no claim in this document rests on a test result;
    every count was taken by listing or grepping the tree directly and is dated 2026-09-05.
[x] Nothing contradicts a §2 invariant.
[ ] Corrections are propagated. NOT DONE, and two are owed: demon-seed-map.md:47 and
    spec-species-generator.md:24 both assert `data/generated/` is absent. It exists with 830
    files. This is research, not an edit pass — the fix belongs to whoever owns those docs.
```
