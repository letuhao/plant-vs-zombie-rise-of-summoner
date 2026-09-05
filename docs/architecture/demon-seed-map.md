# Capability map: `demon-seed`

**Status:** proposed 2026-09-01, after the twenty-six cleared questions in
[demon-seed-ideal.md](demon-seed-ideal.md). **Eighteen modules** (fourteen at first draft;
`species-effects` and `player-materialise` were added 2026-09-01 after the effect-pipeline idea phase —
see §3a; `fusion-recipe-generator` and `fusion-recipe-runtime` were added 2026-09-05 after the real
`catalog-runtime` flip found fusion recipes could not scale to the real roster — see §3b). **Four of
them (`rarity-migration`, `species-generator`, `species-import`, `catalog-runtime`) did not exist in
the first draft of this map** — they are the consequence of the owner's Q23 and Q24 answers, which
turned this from "a generator that replaces a generator" into "the first program that actually builds
the seed → concrete chain."

> **The program in one sentence.** LLM pipelines read the almanac and classify every one of ~904
> captured species into an **enum-only anchor**; deterministic code then expands that anchor into
> every number the game uses, and no model ever picks a magnitude.


> **Plan:** [tasks/seed-to-concrete-plan.md](../../tasks/seed-to-concrete-plan.md) and [-todo.md](../../tasks/seed-to-concrete-todo.md) — **one plan spans both this map and its sibling**, because Phase 5 is a single vertical slice built from modules of each. Neither program can finish alone.
---

## 1. The architecture the owner chose — `seed → concrete`, not codegen

Q23's answer is the spine of this map, and it is bigger than the option it was picked from:

> *"we will build runtime to read json in our rpg server. Json will consider as seed data like item
> generating feature. Seedsmith generate seed, rpg server generate concrete version that use in game,
> same as item seed and concrete item principle that we already define. This principle apply for all
> other feature in seedsmith include action seed and in game concrete action."*

That is [item/seed-contract.md](item/seed-contract.md) §1's law, applied to demons:

> **The seed is generator input. It is not rows.**

```text
almanac_seed · spawn_stats · recipes         SQLite, captured from the game
        |  corpus-dump          (C#, through the DAL)
        v
data/seed/demons/_dump/**.json               development SOT · capture stamp · content hash
        |  power-parse · threat-band · classify-pipelines   (seedsmith)
        v
data/seed/demons/species/**.json             THE ANCHOR = THE SEED — enums only, no numbers
        |  species-generator   (deterministic: Theta, P(Theta), aptitude share, rarity bands)
        v
data/generated/demons/**.json                CONCRETE — checked in, diffable, reviewable
        |  species-import      (all-or-nothing transaction)
        v
SQLite  -->  catalog-runtime  -->  WaveCatalog · SummonRoller · LaneCost · RpgStore · endpoints
```

~~**Honest scope statement: `data/generated/` does not exist.** `seed-contract.md` carries the status
*"Proposed 2026-08-22 … Nothing is authorized to be authored from it yet"*, and there is no generator
and no generated tree in the repo today — verified, the directory is absent.~~ `tools/AtomImporter`
exists and is the transaction discipline `species-import` extends, ~~but the middle stage of the chain
is **new work this program does first**~~. Anyone reading this map as "the item pipeline already exists,
just point it at demons" is reading it wrong.

**Corrected 2026-09-05 — the middle stage has landed.** `data/generated/demons/` holds **830 committed
JSON files**, one per concrete species, all tracked (counted this session; first committed 2026-09-04 in
`52713f4`). The generator exists too: `DemonSpeciesGenerator` / `SpeciesExpander` /
`ConcreteSpeciesSerializer` under `src/FusionRpg.Core/Demons/Generation/`, with the import side in
`RpgStore.Species.cs`. The struck text was written on 2026-09-01, when the stage was still proposed, and
was never revisited. The last sentence still stands: the chain was not free, and this is not the item
pipeline pointed at demons.

---

## 2. Five findings that shaped this map, each verified in code

**① `DemonSpeciesGenerator` has no production callers.**
[DemonSpeciesGenerator.cs](../../src/FusionRpg.Core/Demons/Generation/DemonSpeciesGenerator.cs) is
referenced only from `tests/FusionRpg.Core.Tests/Demons/DemonCatalogTests.cs`. Its *output* is
load-bearing in nine places. **Retiring the generator is cheap; retiring the catalog is
`catalog-runtime`, and it is the riskiest module here.**

**② The corpus that seedsmith reads today is circular.**
[DemonCorpusEmit/Program.cs:45-52](../../tools/DemonCorpusEmit/Program.cs#L45-L52) does
`foreach (var s in DemonSpeciesCatalog.All) -> store.GetAlmanacSeed(s.Side, s.GameTypeId)`. It walks
the 84 species the C# generator already emitted and asks the database about *those*. The other ~820
species sit in `almanac_seed` and have never been visible to seedsmith. **`corpus-dump` exists because
of this one line**, and it must walk `ListAlmanacSeed()` instead. A one-line difference with an 11x
consequence.

**③ `ssot-rarity.md` §4.3 does not merely omit demons — it refuses them.**

> *"**Demons keep their own ladder.** `DemonRarity` stays a four-value code enum"* … the band map
> exists *"**not** so the two ladders can be merged later."*

Owner Q4 and Q24 override this. `rarity-migration` owns the reversal and the amendment.

**④ `powerBand` is already taken, and it means something else.**
[PowerBandDisplay.cs:7](../../src/FusionRpg.Core/Hud/PowerBandDisplay.cs#L7) maps pinned `Theta` to a
compact lawn HUD badge in `1..BadgeMax`. The ideal doc's `powerBand` is a *species threat rung* and has
nothing to do with it. **Renamed to `threatBand` across this program** — which also matches Q14's
threat-scale nouns, so the rename costs nothing and removes a collision that would otherwise have
shipped.

**⑤ `Theta` has no species term today.** [ssot-power-scale.md](power/ssot-power-scale.md) §5.3 composes
`Theta` from Dave level, realms advanced, PvZ runs, Zomboss level, map depth and world size — **six
axes, no species offset**. `threatBand -> Theta offset` is therefore an addition to §10's closed
inventory, and §10 says adding one is a reviewed change to that document. Owed; listed in §5 below.

---

## 3. The modules

| # | Module id | Responsibility | Model calls | Depends on |
|---|---|---|---|---|
| 1 | `corpus-dump` | All ~904 `almanac_seed` rows + earliest `spawn_stats` baseline + recipes -> committed JSON with capture stamp and content hash. **The development-phase SOT** (Q10) | — | — |
| 2 | `anchor-contract` | The seed's JSON structure: 18 attributes, a prose description per attribute, an explicit `none` on every closed enum, a declared ownership level per field, and a schema audit that mechanically rejects a numeric field (Q22) | — | — |
| 3 | `power-parse` | Deterministic 韧性/伤害 parse plus `spawn_stats` observation -> a numeric seed and a 4-value `basis`. **Covers most of the roster with no model call** (Q6) | — | 1 |
| 4 | `threat-band` | Tuning table: parsed number -> one of ten threat-noun rungs -> a `Theta` offset. **A table, never a formula** (Q3, Q14) | — | 3 |
| 5 | `dump-preflight` | The skill that refuses to start a run unless the dump exists, its hash is current, the model answers, and the venv is installed — **and asks the human** for whatever is missing (Q13) | — | 1, 2 |
| 6 | `option-permutation` | Enum option order seeded from `speciesId`; three-way majority vote on the five load-bearing fields; reports a per-field disagreement rate (Q8, Q25) | — | 2 |
| 7 | `classify-pipelines` | **Eight** LLM pipelines, each owning one judgement. Cross-field validators, including posture <-> resource repair (Q5, Q12, Q16, Q17, Q25) | **yes** | 2, 4, 6 |
| 8 | `anchor-emit` | Writes `data/seed/demons/species/**.json` plus provenance (dump hash, prompt version, basis, vote record). One full re-derivation, append-only after (Q19) | — | 7 |
| 9 | `run-control` | Run state machine: pause · resume · cancel · rerun · overwrite-all, with a run record that survives the process (Q20) | — | 7 |
| 10 | `rarity-migration` | `DemonRarity` 4 -> 10 rungs, its consumers, and the `ssot-rarity.md` §4.3 reversal (Q4, Q24) | — | — |
| 11 | `species-generator` | **Seed -> concrete.** Anchor enums -> every number: `Theta`, `P(Theta)`, aptitude allocation, rarity count band and tier window, tempo/reach as real intervals. Writes `data/generated/demons/` | — | 8, 10 |
| 12 | `species-import` | Concrete JSON -> SQLite in one all-or-nothing transaction, extending `tools/AtomImporter`'s discipline | — | 11 |
| 13 | `catalog-runtime` | **The risky one.** The nine production readers of `DemonSpeciesCatalog.All` move to store-backed reads | — | 12 |
| 14 | `roster-metrics` | Distribution guard over element pair x aptitude x threat band x rarity. **The D2-Hammerdin control** | — | 8 |
| 15 | `species-effects` | **The container.** Anchor -> a `species-passive.{speciesId}` seed: fixed core, affix pool, affinity ordinals, eligibility tags. Without it a generated demon has a stat block and no effects | **yes** | 8, 10, **effect-pipeline** |
| 16 | `player-materialise` | **Runtime, per player.** At profile creation, roll every species container against that player's world seed and write it to their tables. Frozen for the save; append-only afterwards | — | 12, 15 |
| 17 | `fusion-recipe-generator` | Distribution index over the real concrete roster finds rarity rungs whose input pool cannot support a unique pair per output (found real, 2026-09-05: 21 `Almanac` outputs vs. 4 `Sunwoven` inputs); an LLM proposes a cross-rung pairing only for those deficits; a deterministic reconciler validates and writes the committed recipe seed | **yes, bounded to shortfall rungs only** | 11 |
| 18 | `fusion-recipe-runtime` | `DemonRecipeCatalog` moves from live in-process computation to a `Configure`d read of the committed seed — the same seam `catalog-runtime` already proved for the species catalog, applied to its sibling | — | 17 |

### Dependency graph

```text
corpus-dump ---+--> power-parse --> threat-band ----+
               |                                     |
               +--> dump-preflight <--+              |
                                      |              v
anchor-contract ----------------------+--> option-permutation --> classify-pipelines
                                                                          |
                                                     +--------------------+--------------+
                                                     v                                   v
                                                anchor-emit                         run-control
                                                     |
                              +----------------------+------------------+
                              v                      v                  v
                       roster-metrics        species-generator <-- rarity-migration
                                                     |
                                                     +--------------------------+
                                                     v                          v
                                              species-import          fusion-recipe-generator
                                                     |                          |
                                                     v                          v
                                              catalog-runtime          fusion-recipe-runtime
                                                     |
                                                     v
   effect-pipeline ──►  species-effects  ──►  player-materialise
```

No cycles. Every arrow points one way.

### Build order

```text
corpus-dump -> anchor-contract -> power-parse -> threat-band -> dump-preflight
  -> option-permutation -> classify-pipelines -> anchor-emit -> run-control
  -> roster-metrics -> rarity-migration -> species-generator -> species-import -> catalog-runtime
  -> species-effects -> player-materialise      (15 and 16 gate on effect-pipeline)
  -> fusion-recipe-generator -> fusion-recipe-runtime   (17-18 gate on species-generator only,
                                                          independent of 12-16's own chain)
```

**Why the front of that order.** The first five modules make **no model calls at all** and still
produce a real, measured basis for most of the roster. That is the same "standalone value before a
single token is spent" property the seedsmith G0/G1 wave was built on. `roster-metrics` sits
immediately after `anchor-emit` deliberately: the moment 904 anchors exist, the distribution guard is
what says whether they are worth expanding into concrete rows at all.

---

---

## 3a. The gap that added modules 15 and 16 (found and closed 2026-09-01)

**Owner, after the first fourteen were written:** *"we really miss these pipeline on our specie
generator — we just generate demon species without ship atom container for it."*

**He was right.** The first fourteen modules took an almanac entry all the way to a stat block and a
runtime catalog, and **not one of them produced an effect container.** A demon generated by that map had
an element, an aptitude, a rarity and full stats, **and did nothing.** The gap was invisible because
every module was individually correct — a missing row, not a wrong one.

**Closed by two modules, not one**, because the idea phase separated the two halves:

- **15 `species-effects`** — dev-time. Anchor to container seed. Runs after `anchor-emit` because it is
  a function of the anchor: rarity sets the bands and tier window, element and aptitude constrain
  eligible families, `resourceProfile` gates the resource families, and the lore carries the judgement.
- **16 `player-materialise`** — runtime. Rolls that container per player at profile creation, against
  the player's world seed, and freezes it.

**What the audit did *not* find.** Modules 11-13 were written before the per-player decision and might
have been invalidated by it. They were not: the owner's **two-layer** answer — shared definitions,
per-player materialisation — keeps species *stats* deterministic and global, so `species-generator`'s
committed `data/generated/` tree and `catalog-runtime`'s shared snapshot both stand. **Only effects
roll.**

**⛔ `demon-seed` cannot finish on its own.** Module 15 needs an affix library, a slot mechanism and a
container schema, and all three belong to
[effect-pipeline-ideal.md](effect-pipeline-ideal.md) §6 — a different program. That dependency is real,
and it is stated here rather than discovered at task start.

---

## 3b. The gap that added modules 17 and 18 (found and closed 2026-09-05)

**Found running the real `catalog-runtime` flip, live, not by inspection.** With the store-backed
roster finally loaded, the FIRST downstream consumer to break was not species-related at all:
`DemonRecipeCatalog.Build()` — `spec-demon-fusion.md`'s own "code-generated from the species catalog"
recipe assignment, shipped 2026-08-21 — threw `InvalidOperationException: No unused input pair
available for 'jacksonzombie'`.

**Root cause, found by direct query against the real database, not assumed:** 21 `Almanac`-rarity
(top-rung) species need a unique fusion input pair each; the nearest populated rung below,
`Sunwoven`, has only 4 species, and 4 elements support at most `C(4,2)=6` distinct unordered pairs.
This is the exact same defect class `roster-metrics` (module 10) already declares a target for —
*"rarity distribution: monotone decreasing across the ten rungs"* — but that module runs on anchors,
before rarity is assigned during expansion, so it could not have caught this specific case even fully
built and gated.

**What was tried first, and why it was reverted.** A per-output backtrack across every candidate `a`
(trying a different first input when the greedy first choice's pairs were already claimed) fixed a
DIFFERENT, narrower collision (`newyearzombie`) but could not fix the Almanac/Sunwoven case — no
reordering of 4 elements manufactures a 7th pair. A second attempt widened the input pool by
ACCUMULATING rungs downward (Sunwoven + Firstseed together) — this technically supplied enough
capacity, but silently violated `DemonRecipeCatalogTests`'s own tested invariant that both of a
recipe's inputs come from the exact same single nearest-populated rung below the output. **Reverted
before landing** once the test caught it: a technically-working fix that breaks a deliberate, already-
tested game-design rule is not a fix, it is a different, unreviewed game design.

**Owner's actual direction, once the tradeoff was surfaced (2026-09-05):** *"make new seedsmith
pipeline for fusion with deterministic engine read the whole demon species seed and make distribution
index... fill fusion hole gap by LLM propose the recipe, deterministic engine validate and reconcile...
then finally output fusion seed and we will load it in the game runtime."* This is the exact seed →
concrete shape every other artifact in this program already follows, applied to a fourth kind of
content (recipes) that had never gone through it — recipes were the one thing this program still
computed live, in-process, with no anchor, no seed file, and no LLM in the loop at all.

Closed by two modules, mirroring §3a's own split between generation-time and a narrower runtime seam:

- **17 `fusion-recipe-generator`** — dev-time. Distribution index (deterministic) finds shortfall
  rungs; an LLM proposes a pairing ONLY for the specific deficits the deterministic pass cannot close;
  a deterministic reconciler is the sole write authority over the committed seed. Bounded scope: for
  the real corpus this is 21 deficit outputs, not a re-derivation of all 829 recipes.
- **18 `fusion-recipe-runtime`** — the same `Configure`/`UseScoped` seam `catalog-runtime` already
  proved for `DemonSpeciesCatalog`, applied to `DemonRecipeCatalog`'s much smaller shape (a single
  committed JSON file, `SpeciesBuildPlanCatalog`'s pattern, not a SQLite import).

**What the audit did *not* find.** Every OTHER rarity rung has ample pairing capacity (checked against
the real distribution, not assumed) — `Sunwoven` itself (4 outputs, drawing from `Firstseed`'s 37) is
nowhere close to its own ceiling. This is a single, isolated, well-bounded gap, not a sign the whole
recipe-assignment design needs rework.

---

## 4. What this program deliberately does not do

| Excluded | Why | Owner reference |
|---|---|---|
| `aspect` generation | Blocked on two unbuilt programs — hybrid element typing, and the passive skill graph | Q9 |
| Ability / passive generation | ~1,500-3,500 named ability instances is a larger program than this one; the anchor is an index, not a roster | ideal §6.2 ⑥ |
| Any change to what PvZ is | Every RPG feature lives in the RPG layer. The lawn keeps its own numbers and only ever receives a signed progression delta | Q18, `CLAUDE.md` |
| Web-battle balance | `species-generator` produces the RPG's own base stats; whether they are *fun* is a balance pass, not a generation one | — |

---

## 5. Amendments this program owes before it builds

These are reviewed changes to documents that win over any spec. **Listed here so they are not
discovered halfway through a task.**

| Document | Change | Owed by |
|---|---|---|
| [decisions.md](decisions.md):95 | *"species catalog generated deterministically from captured game data"* -> **captured** deterministically, **derived** in seedsmith, made **concrete** in the server | `species-generator` |
| [decisions.md](decisions.md) | Revert the approved `aspect-scope` spec | Q9, before any aspect work |
| [item/ssot-rarity.md](item/ssot-rarity.md) §4.1, §4.3 | Demons adopt the ten-rung ladder; the four-row band map becomes a migration shim with an end date, not a permanent wall | `rarity-migration` |
| [power/ssot-power-scale.md](power/ssot-power-scale.md) §5.3, §10 | A species `Theta` offset joins the composition and the closed inventory | `threat-band` |
| [item/seed-contract.md](item/seed-contract.md) | Its status line stops saying nothing is authorized, or demon seeds declare their own subtree under the same law | `anchor-emit` |
| [demons/spec-demon-fusion.md](demons/spec-demon-fusion.md) §"Recipe fusion" | *"built deterministically from `DemonSpeciesCatalog` at startup"* -> a deterministic pass PLUS a bounded, LLM-filled gap for rungs the deterministic pass cannot mathematically satisfy, reconciled and committed as a seed, never computed live in the shipped game | `fusion-recipe-generator` |
| [demons/spec-demon-fusion.md](demons/spec-demon-fusion.md) §"Boundaries" ("Ask first: … recipe-graph shape changes") | Cleared, not silently taken: `crossRungGapFill` lets a bounded subset of recipes draw inputs from more than one rung below the output — a real graph-shape change, distinct from the computation-method amendment on the row above. Owner, 2026-09-05 (quoted in `spec-fusion-recipe-generator.md`'s own Objective): authorized the whole pipeline shape this change is one consequence of | `fusion-recipe-generator` |
| [demons/spec-demon-fusion.md](demons/spec-demon-fusion.md) §"Recipe fusion" ("every summonable rare/epic/legendary species gets exactly one recipe") | Narrowed to best-effort: a deficit whose LLM vote never resolves ships with **no recipe**, named as `unresolved` rather than fabricated. A separate loosening from the two rows above — coverage, not method or graph shape | `fusion-recipe-generator` |

---

## 6. Related

- Ideal: [demon-seed-ideal.md](demon-seed-ideal.md) — twenty-six cleared questions, nine research passes
- Prior art: [../research/game-design/](../research/game-design/) — seven files, read before re-searching
- The tool: [seedsmith-map.md](seedsmith-map.md) — this program is a new feature inside it
- The law it obeys: [item/seed-contract.md](item/seed-contract.md) §1-§3
- The atom layer: [effect-atom/definitions.md](effect-atom/definitions.md) — settles the aspect decomposition (ideal §7)
