# Structure seed — the ideal

**Status:** idea phase, 2026-09-04. **Not a spec. No build authorized.**

**What it serves:** [base-defense-ideal.md](base-defense-ideal.md) needs a structure corpus —
§5.21 estimates **24–40 types** against the **4** that exist. This document is how that corpus gets
authored, and it is written against the worked example rather than from scratch:
[demon-seed-map.md](demon-seed-map.md) and the **408** shipped species anchors under
`data/seed/demons/species/`.

---

## 1. The laws, restated inline

A downstream session reads this document, not its links.

1. **Seed → concrete → per-player. Three layers, and the middle one rolls.** Seedsmith emits seeds
   offline — enums only, no magnitudes, committed and diffable. The **game runtime** rolls the concrete
   object per player, seeded, like Diablo loot. `Instantiator.TryInstantiate`
   (`Effects/Atoms/Instantiator.cs:92`) is the shared SDK: `(container_id, catalog_revision, roll_seed)`
   → `InstanceRow`. **Never design a second roll.** `ActionSeeder.cs:19,45` already reuses
   `Instantiator.Draw` verbatim, *"unchanged, only its visibility widened."*
2. **The LLM writes identity; deterministic code writes magnitude.** *"A model has no calibrated sense
   of scale, so a number it picks is a plausible-looking guess that survives review because nothing
   looks wrong with it."* A wrong enum is visible; `hp: 4200` reads exactly as plausibly as `2400`.
   Enforced by schema audit, **never by review**.
3. **The seed is generator input. It is not rows.** Authors write judgement; a generator expands it;
   the importer validates and upserts. Generated output is **checked in** — *"a generated row nobody
   can diff is a row nobody can review."*
4. **Four ownership levels, and a field with none is a contract defect** — `AUTHORED` (the agent
   chooses) · `DERIVED` (the importer computes) · `GENERATED` (a generator emits rows) · `VALIDATED`
   (the author names it, a frozen registry owns it). *"Naming a value and owning a value are different
   rights."*
5. **One power ladder.** Contests read `Θ` linearly; magnitudes read `P(Θ)`. No private `f(level)`.
6. **`long` for every magnitude, never `float`**, widen before multiplying, divide by 1000 last exactly
   once, overflow throws. **No hard progression ceilings.**
7. **Ordinals, never numbers.** `threatBand`, `attackTempo`, `reach` are ordinals the deterministic
   layer turns into intervals. If a model is about to pick "1.5 seconds", stop.

---

## 2. What already exists — three buckets

### 2.1 Built

| Thing | Evidence |
|---|---|
| **A complete seed → anchor pipeline at scale** | **408** species files under `data/seed/demons/species/plant/` plus zombie, with `_index.json` keyed by species id |
| **The anchor shape, and it is enums-only** | `aerial-flora.json`: `aptitudePrimary: "Bulwark"`, `attackTempo: "steady"`, `threatBand: "nuisance"`, `reach: "short"`, `rarity: "fused"`, `deployMode: "PlantAvatar"`. **Not one magnitude.** The only number is `gameTypeId: 1204`, an identity key |
| **Provenance that makes idempotency checkable** | `_provenance` carries per-field `attempts`, per-field `confidence`, `dumpHash`, `emittedUtc`, `minorityValues`, `promptVersions`, `auditVerdict`, `basis` |
| **`_derived` declares ownership in-file** | `"_derived": ["basis", "posture", "pure"]` — the anchor states which of its own fields it did not author |
| **`none` as a real value** | `aptitudeSecondary: "none"`, `elementSecondary: "none"` — absence is a stat, not a missing key |
| **The roll SDK** | `Instantiator.TryInstantiate` with `RollSeed`, `CatalogRevision`, `ThetaContent`, `ContentFingerprint()` |
| **The importer and seed file format** | `tools/AtomImporter`, all-or-nothing import, content lints — item seeds are *"more of the same corpus under a new subtree"* |

### 2.2 Wiring gap

| Gap | The inert line |
|---|---|
| **The concrete-roll layer has no production caller** | `Instantiator.TryInstantiate` — zero. Every *"we need a runtime generator"* finding for structures is therefore a wiring gap on a shipped SDK, not a new build |
| **`StructureDef.Name` has no reader** | Outside its own validator. Nothing in the game or web UI can name a structure — so a generated corpus has no surface today |

### 2.3 Real gap — and the first one is the whole problem

⭐ **Structures are not seed content. They are a C# literal.**

```csharp
static readonly IReadOnlyList<StructureDef> Seed = new StructureDef[]
{
    new() { StructureId = "loam-source-placeholder", ... }
```

The variable is *called* `Seed` and is a hardcoded array of **four** rows. Compare 408 JSON anchors for
demons. And `data/seed/` holds **sixteen** domains — actions, aptitudes, atoms, channel-policy,
channel-pools, containers, curves, demons, derived-stats, elements, external-reference, items, loot,
rarity, resources, zomboss — **and none is structures**.

**So the first module is not a generator. It is making structures seed content at all**, and it is
model-free: a schema, a registry, an importer path and a dump of the four existing rows. Per the
generation discipline, *"order the build so the model-free modules come first — a parse, a table, a
schema and a dump produce real value with zero tokens spent, and they make the expensive stage's inputs
reviewable."*

| Other real gaps | Note |
|---|---|
| No structure seed corpus | — |
| `StructureKind` has 2 values | `LoamSource`, `Storage`. §5.21 of the base-defense ideal names ten roles |
| No structure generator of any kind | — |

---

## 3. The generation shape is **not** the demon generator's, and this is the load-bearing difference

**The demon pipeline classifies an existing corpus.** A Peashooter already exists — it has a name, an
almanac entry, art, a `gameTypeId`. The model reads it and assigns enums. Identity is *given*; the
model's job is judgement about a thing that already is.

**A structure corpus has to be invented.** There is no almanac of trenches.

That changes three things, and a spec written against the demon pipeline without noticing would get all
three wrong:

| | Demon (classify) | Structure (invent) |
|---|---|---|
| Input | one captured almanac entry per call | a **combination** — (role × slot kind × climate) |
| Failure mode | mis-assignment; caught by majority vote and confidence | **mode collapse and generic flavour** — nine variations of "Sturdy Wall". Vote does not catch it |
| Metric loop | closed — a field is populated or it is not | flavour distinctness is **open-loop**, so it produces a review queue and **never a pass** |

⚠️ **And the PvZ corpus is not available for reuse here.** PvZ's static plants — Wall-nut, Tall-nut,
Pumpkin, Spikeweed, Lily Pad — are exactly structures in our terms, and would be the obvious corpus.
**They are already demons.** The owner's own framing: *"cannot use soul to summon a wall, that confuse
with wallnut demon family."* Reclassifying them would take content out of the summon roster.

**So the source material is the design research, not a datamine:** base-defense §5.18's seventeen
historical works reduced to four obstacle kinds, and §5.21's ten economic roles. That is ~25–30 seed
concepts before any model is called — which is almost exactly §5.21's own 24–40 estimate, and it means
**the corpus can be authored by hand first and generated second.**

---

## 4. Grid density — computed, not estimated

The safe band is **~3.6 entries per cell** (Genshin, FGO); **~12.6 is the failure zone** (Fire Emblem
Heroes).

**Candidate axes:**

| Axis | Values |
|---|---|
| **role** | Extract · Refine · Multiply · Store · Move · Bank · Enable · Defend · See · Deny = **10** |
| **controlPoint** | yes / no = 2 |

Naively 10 × 2 = 20 cells for 24–40 types = **1.2–2.0 per cell — below the safe band**, meaning the
taxonomy is wider than the roster fills and most cells are empty or singleton.

⭐ **But `controlPoint` is not an axis.** Decision 25 makes it **derivable from role**: extract, refine,
multiply, store, bank and see all need someone to run them; deny (wire, mine) and the cover half of
defend (rampart, trench) have nothing to control. It is correlated, not orthogonal — so it is a
`DERIVED` field, not a second axis.

**One axis: 10 roles, 24–40 types = 2.4–4.0 per cell.** In band at the top, thin at the bottom. **Target
the upper half: ~36 types.**

⛔ **And the obvious second axis is the one the failure modes forbid.** §5.21 suggests tier chains
(Library → University → Research Lab). *"A stronger version is not a different unit"* — a faster Banshee
is still a Banshee, and a bigger wall is still a wall. **Tier is not a distinctness axis**, and rarity
buys breadth and ceiling, never power. If a second axis is wanted, **element** is the honest candidate
(an attuned totem projects its climate), because it changes *what the structure does*, not how much.

---

## 5. The shape — a structure anchor, mapped from the species anchor

Field-for-field against `aerial-flora.json`, with ownership levels:

| Species anchor | Structure anchor | Level | Notes |
|---|---|---|---|
| `speciesId` | `structureId` | AUTHORED | kebab, registry-checked |
| `side` (plant/zombie) | — **dropped** | | Decision 12: structures have **no ownership**. A `side` field would be a lie |
| `family` | `family` | AUTHORED | "earthwork", "emplacement", "works" |
| `aptitudePrimary/Secondary` | **`role` / `roleSecondary`** | VALIDATED | the ten of §4; `none` legal |
| `deployMode` | **`requiredSlotKind`** | VALIDATED | already a shipped enum with 14 values |
| `elementPrimary/Secondary` | `elementPrimary/Secondary` | VALIDATED | for attuned works; `none` legal |
| `attackTempo` | `tempo` | AUTHORED ordinal | emplacements only; `none` otherwise |
| `reach` | `reach` | AUTHORED ordinal | the deterministic layer turns it into cells |
| `threatBand` | **`strengthBand`** | AUTHORED ordinal | → HP and damage via `P(Θ)`, **never authored as numbers** |
| `rarity` | `rarity` | VALIDATED | the shared ten-rung ladder; breadth and ceiling only |
| `traits` | `traits` | VALIDATED | decision 11 gives structures traits |
| `resourceProfile` | **`costProfile`** | AUTHORED ordinal | *which* materials and in what **ratio band** — never an amount |
| `targetPreference` | `targetPreference` | VALIDATED | §5.20's First / Last / Close / Strong |
| `variants` | `variants` | AUTHORED | |
| `acquisition` | `acquisition` | AUTHORED | built · authored-on-map · captured |
| `posture` · `pure` (derived) | `controlPoint` · `obstacleVerbs` | **DERIVED** | §4; verbs from role + slot kind |
| `reason` | `reason` | AUTHORED | free text, identity not magnitude |
| `_provenance` · `_derived` | identical | | copy the machinery wholesale |

**New, with no species analogue:**

| Field | Level | Why |
|---|---|---|
| **`footprint`** | AUTHORED ordinal | one cell / small / large. A magnitude only after the deterministic layer reads it |
| **`coverTier`** | AUTHORED ordinal | none · light · heavy · trench → the flat dodge delta base-defense §5.18 locks |

**Every one of the seventeen is an enum, an ordinal, a registry id or free text. Not one is a number.**
That is the audit the schema enforces.

---

## 6. Failure modes to design against, named before proposing

| Mode | Guard |
|---|---|
| **Distribution skew** — every individual row defensible, the *offering* degenerate (D2's Hammerdin) | A per-role count target in `budget`, checked as *actual vs declared*. §P2: *"a metric without a declared target is an opinion"* |
| **"A stronger version is not a different unit"** | Tier is not an axis (§4). A tier chain is a `variants` list, not four rows |
| **Mode collapse in invention** | The corpus is authored by hand first (§3), so the model extends a real distribution rather than defining one |
| **Generic flavour** | Open-loop by nature → a review queue, **never a pass** |
| **A model picking a magnitude** | Schema audit rejects any numeric field outside the identity keys |

---

## 7. Tunables

Every number this introduces. None is a `const`, and none is in a seed file.

| Block | File | Rows |
|---|---|---|
| `bands` | `data/tuning/structure-seed.v{n}.json` | ordinal → interval for `strengthBand`, `reach`, `tempo`, `footprint`, `coverTier` |
| `cost` | same | `costProfile` ratio band → material amounts |
| `budget` | same | per-role target counts, for the skew guard |

---

## 8. Open questions — owner decisions only

**None. Both were closed by the owner on 2026-09-04** (base-defense-ideal.md §0, round 10):

| Was | Decision | Answer |
|---|---|---|
| Own program or a module? | **45** | ⛔ **A module set inside `base-defense`.** Decision 30 is revised. This document stays as the design record; only the program boundary changed |
| Do static plants stay demons? | **43** | **Yes** — confirming §3's own argument. So the pipeline is **INVENTION**, not datamine: the source is the design research (§5.18 + §5.21), hand-authored first, generated second |

**Three obligations were added while these were open**, and they are part of the module set:

- **Decision 32** — a structure's HP is `P(Θ_development) × an authored MATERIAL TIER ordinal`. That
  ordinal is this schema's **`strengthBand`**; do not add a second one beside it.
- **Decision 33** — a **deterministic planner stage runs before any model call**, fixing which kinds,
  which tiers, how many variants and which slots. Promoted from the seedsmith guideline *"order the
  build so the model-free modules come first"* to a required stage.
- **Decision 35** — an **`acquisitionPaths`** field, `VALIDATED`, a subset of
  `{built, assembled, summoned, laboured}`, `none` illegal. ⚠️ **Reconcile it with §5's existing
  `acquisition`** (`built · authored-on-map · captured`) — those are two different questions (*how it
  reached the MAP* versus *how it reaches the BOARD*) and shipping both under similar names is the
  second-vocabulary defect. Name them apart or merge them deliberately.

<details><summary>The questions as originally posed</summary>

1. **Is this its own program, or a module of `base-defense`?** It has its own corpus, its own pipeline
   and its own metrics, which argues for its own map — the same shape `demon-seed` has beside
   `demon-system`. But it exists only to serve base defense.
2. **Do PvZ's static plants stay demons?** The owner's framing says yes. Worth confirming, because it
   is the difference between a datamine-classify pipeline (cheap, proven) and an invention pipeline
   (§3, and a different failure surface).

</details>

**Deliberately not open:** the band intervals, the per-role counts, and the corpus size — those are a
tuning pass once the schema exists.

---

## 9. Next step

The model-free half first, and it needs no decisions above to start:

1. **A structure seed schema** with the four ownership levels, and the `data/seed/structures/` subtree.
2. **A dump of today's four rows** into it, proving the importer path end-to-end.
3. **`StructureCatalog` reads the imported corpus** instead of a C# literal.

Only then does a generator have inputs worth reviewing. `/spec` after the two questions in §8.
