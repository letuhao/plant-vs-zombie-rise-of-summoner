# Tier-System — the ideal

**Status:** idea phase, 2026-09-13. Not a spec. No build authorized.
**⛔ AUDITED then DISPOSED 2026-09-13. Read § DISPOSITION first, then § AUDIT for the evidence.**

---

## § DISPOSITION — owner policy, 2026-09-13

> **Owner, 2026-09-13:** *"we will clear dead/blocked instead of keep unfinish. we only have do or defer,
> no blocked — if feature is not right we will withdraw."*

**There is no "blocked" state in this repo.** Every item below is **DO** (right and actionable now),
**DEFER** (right, not yet — with a named trigger that returns it), or **WITHDRAW** (not right; it goes,
and its reasoning stays only as a trail). Nothing is left half-finished.

### WITHDRAW

| Item | Why it goes |
|---|---|
| **D7 — lineage as the family key** | **Disproven by measurement**, not by opinion: 61–113 groups, **median 2**, 49 singletons, 272–333 species with no lineage at all, and the graph is a cyclic DAG where 80% of species have multiple roots. It is not a grouping. § D7-AUDIT keeps the numbers as the trail |
| **The family layer, for now** | Its only purpose was to let the species layer stay thin. With the species layer deferred (below), it is a solution to a problem nobody has yet. Re-derive it *after* species selection exists, against real play |
| **`tier-system` as a new program** | `gameplay-tiers-ideal.md:156` already closed the graduation list — *"never by reopening sealed ideals."* The surviving work redistributes into the programs that already own it (creature-seed, item, drop-tables, creature-lawn-deploy). **This document becomes a reasoning trail, not a program** |

### DO — actionable now, each independently valuable

| # | Work | Why now |
|---|---|---|
| **1** | **Species selection** — wire `Encounter.Build` to a real delve room; widen `WaveCatalog.Band` past 4 rungs and give it an actual roll; add a species target to `ObjectiveTargetKind` | The real first module. A player currently meets **10 of 904** creatures. The 419-anchor climate-weighted selector with an anti-repeat cap **is already built** and needs a caller, not a design |
| **2** | **`threatBand` writer** (D8) | The classification is **free and already proven** — the existing module scored all 719 unresolved anchors, well spread across ten rungs. Only the write-back is missing. Fixes 81% of the roster sitting on one rung. Ship the `SpeciesExpander.cs:31-33` refusal in the same commit so the next gap cannot hide |
| **3** | **Species-magnitude synthesizer** | A7's finding: the values are already in SQLite (`RpgStore.Species.cs:155-160`). A synthesizer in `ImportCreatureSpecies` closes it in **~1 file with zero committed corpus** — turning a 904-row content program into an importer change. Must follow #2, which rewrites every magnitude |
| **4** | **The live defects** — FE roster sort (4+ sites keyed on a retired vocabulary, sort silently dead today), 84 retired rarity ids in `themes.v1.json` (needs `--rebuild`, owned by seedsmith `theme-refresh`) | These are **bugs**, not design. The roster sort is visible to players right now |
| **5** | **Propagation guard T-3** | `CreatureRarityLadder.RungCount = 10` is a hardcoded const while `All` already uses `Enum.GetValues` — **they disagree by construction** the moment the enum widens, and `OneRungAbove` then throws a message that lies about the cause. No test guards it. Small, and it prevents a silent failure |

### DEFER — with the trigger that brings each back

| Item | Trigger |
|---|---|
| **D2 — species materials** (general / species-unique; family layer withdrawn) | ⭐ **CONFIRMED AS A DECISION 2026-09-13 — see [species-craft-ideal.md](species-craft-ideal.md).** A species-bound set piece is enhanced with **its own species' material**: 844 species-themed sets already exist, and without a species hook at the bench the binding is decoration. The deferral is **sequencing only** — DO #1 ships first, because MH's documented fix for species-bound gear whose species is unreachable was to *make the species farmable*, never to invent a substitute. Sinks still owed on return (DESIGN-GATE Economy row). ⚠ **Two earlier framings here were wrong and are struck:** that trophies are "an additional fantasy layer, not the cost substrate" (they are the point, not a garnish), and an audit verdict that "disproved" the coupling by testing a *normative* claim as an *empirical* one — **its facts stand, its verdict does not.** The observation that the 10 `elevate` recipes spend only the closed 27 ids is true, and describes exactly the undecided state this decision changes |
| **D1 — invented species** | `CreatureTypeId` is decoupled from `GameTypeId`. Today it is a pure function of `(side, gameTypeId)` and the catalog **throws on a duplicate**, so the borrowed-art fallback hits a boot throw on the first same-side repeat — and it is also the progression key, so two species would share one XP row |
| **D5 — deterministic exchange** | Rides with D2 |
| ~~**E5 — item→item upgrade tree**~~ | **→ MOVED TO DO, 2026-09-13.** Owner challenge: *"no reason to defer them if they don't need a larger program."* Re-sized and correct — it is one bounded schema change in a program that already exists. Now [gear-climb-ideal.md](gear-climb-ideal.md) |
| ~~**E6 — rarity promotion executor**~~ | **→ MOVED TO DO, 2026-09-13.** Re-sizing found it is nearly built: `ssot-rarity.md` §3.7 is a complete operation spec, the balance is measured (§7.2), the cost is priced, **10 recipes are authored**, and the consumer is registered. Missing: one `op_kind` + one executor. Now [gear-climb-ideal.md](gear-climb-ideal.md) |
| **D1 — invented species** *(revised)* | Still deferred, but **smaller than stated**: `CreatureTypeId` is persisted as `type_id` and keyed by progression/aptitude rows, so recomputing it is a migration — **but freezing is not.** Stamp each existing species' current computed value as its permanent allocated id and allocate new species above the max: zero migration, and the boot throw disappears. Trigger: that allocation lands. ⚠ Found while sizing: `SlotFilter.cs:49` computes `CreatureTypeId` **without the `+50,000` plant offset** the other two sites apply — a latent divergence that bites the moment DO #1 wires that path |

### What this leaves

**Five things to do, none of them a tier.** That is the honest outcome: the tier design was downstream of a
missing game system, and the audit's value was finding that before a spec was written against it.

---

---

## ⛔ § AUDIT — six independent reviews, 2026-09-13

Commissioned before `/spec`. Four returned blocking findings. **The headline is that this document
mis-stated its own problem:** the tier machinery is in better shape than the doc claimed, and the thing
that actually blocks species-keyed content is **not a tier gap at all**.

### A1 — ⛔ **The game has no species-selection surface, and that kills D2's species layer outright**

Measured against the real corpus, not assumed:

| Faucet | What it can actually reach |
|---|---|
| **Web-battle waves** | `WaveCatalog.Build()` draws from **4 of 10 rungs**, and selects `pool[i % pool.Count]` (`WaveCatalog.cs:164`) — **no RNG**, the alphabetically-first N of each band. **All four waves together ever instantiate 10 distinct species.** Forever |
| **Expeditions** — the only live species→material faucet | `RollWildSpecies` (`ExpeditionResolver.cs:227-236`) rolls inside **3 of 10 rungs**: Chaff 84%, Cultivated 15%, Heirloom 1%. Reachable pool **242 species**; a given Heirloom species appears on ~**0.02%** of wild ticks |
| **Delve** | Not a faucet — `Encounter.Build` has no production call site |

**658 of 904 species live in rungs no faucet ever touches**, and `Fused` — **486 species, 54% of the whole
roster** — appears in *neither* faucet's band list.

So a species-unique trophy is an id with no faucet for ~73% of the roster, and a degenerate one for the ten
species that repeat forever. **D2's species layer cannot ship until something makes an arbitrary species
encounterable on demand** — a hunt / contract / bounty verb. `grep -rn "targetSpecies\|huntTarget\|bounty"`
finds nothing. **That verb is a prerequisite this document does not contain, and it is the real first
module of a Monster-Hunter loop.**

### A2 — ⛔ **D7 is falsified twice over, by two independent derivations**

| Derivation | Groups | Mean | Median | Shape |
|---|---|---|---|---|
| Nearest-root | 61 | 9.36 | 5.0 | over only 571 reachable plants |
| parentA-chain root | **113** | **5.59** | **2** | **49 singletons**, 72 of 113 groups ≤2 |
| Connected components | **2** | 352 | — | sizes `[695, 10]` — the fusion graph is **one blob** |

Species with **no derivable lineage**: 272–333 of 904 depending on rule — and the second audit found it is
**not only the 227 zombies**: 101 *plants* are isolated too, while 56 zombies *do* have lineage. The doc's
"empty by construction for zombies" framing was too tidy. **Lineage does not carry a family layer** — its
median group of 2 is worse than the theme labels it was chosen to beat.

### A3 — ⛔ **The MH craft step is unexpressible in the shipped cost matrix, independent of trophies**

`CostClassMatrix.Allows` (`:123-137`) is a switch over five classes that **throws on `_`** — a trophy is a
sixth. Worse, **`Forge` admits `Substrate` only** (`:130`), not Shard and not Essence. So D2's flagship
shape — *"1 generic filler + 3 species commons + 1 rare gate"* — **cannot be priced today even if every
trophy id existed.** The doc argued the vocabulary and never checked whether the recipe shape it was buying
is representable. And `MaterialCatalog.Build()` builds all 27 ids as a **cross-product of closed enums**;
adding ~900 species ids converts `MaterialCatalog.All` from a closed vocabulary into a **derived
population**, which is the exact line `validation-ssot.md` draws.

### A4 — ⛔ **D1's borrowed-art fallback is a boot throw, not a content choice**

`CreatureTypeId` is a pure function of `(side, gameTypeId)` (`ConcreteSpeciesSeedReader.cs:91`), and
`CreatureSpeciesCatalog.cs:110-111` **throws on a duplicate**. So **two same-side species may never wear
the same `GameTypeId` — the catalog refuses to load.** Today 102 `gameTypeId`s are shared, but all 102 are
plant/zombie pairs, so nothing is broken *yet*; D1's fallback is precisely the mechanism that creates
same-side sharing, and it hits the throw on the first repeat. `CreatureTypeId` is also the **progression
key** (`RpgXpAwardMap.cs:110`, `SpeciesProgression.cs:18`), so two species sharing borrowed art would share
one XP row. **`CreatureTypeId` must be decoupled from `GameTypeId` before the flag ships.**
*(The "two-class art quality" worry does **not** land — 204 species already wear another actor's art across
sides and it is invisible.)*

### A5 — Governance, ownership, and sizing corrections

- **C1 — this program should probably not exist as a new program.** `gameplay-tiers-ideal.md:156`
  (undisputed owner decision 8) says any build *"graduates under `legion-unit` (new) or the existing
  creature/world programs — **never by reopening sealed ideals**."* That list is closed and does not
  contain `tier-system`. **This doc's own §5 "no SOLID-violating parallel path" tick is therefore in
  question at the program level, not the code level.**
- **Dup-1 — E6 is not an unowned edge.** Rarity promotion is fully specced by **item module 15
  `enhance-reroll`**: `ssot-rarity.md:235` (*"I6 owns the operation and its cost"*) with seven legality
  rules at `:239-259`, priced at `ssot-materials-crafting.md:204`, and `RarityBudgetKeys.cs:32` already
  names `"enhance-reroll (15)"` as `promote_from`'s consumer. What is missing is an executor and an
  op_kind — **a wiring gap *inside* module 15**, not a gap "no program owns".
- **Dup-2 — §3's headline deliverable belongs to `creature-lawn-deploy`.**
  `spec-lawn-deploy-core.md:233` already defines the `trait.species-magnitude-{speciesId}` convention and
  `:239-240` already calls full-corpus generation *"real, separate follow-up work."*
- **D4 sizing — "the cheapest high-value fix" is wrong.** `SpeciesMagnitudeContainerId` is **one container
  per species** (`RpgStore.UniqueActors.cs:1569`), so this is a **904-row generation program**, not one
  content gap. The other three blockers are genuinely small.
- **D8 has a measured ceiling.** The deterministic parse keys on `伤害:`/`韧性:`; across the 904 almanac
  rows **427 carry a damage line, 381 a toughness line, and 200 carry neither** — so the no-model path tops
  out at **704/904 (78%)** and leaves a ~200-species residue for model calls. The
  `SpeciesExpander.cs:31-33` refusal change is correct and should ship in the same commit.
- **Eleven cross-program asks are filed nowhere.** `grep -rl "tier-system" docs/ tasks/` returns only this
  file. The convention is a `## Filed by the <program> program` section in the **owning** map
  (`item-map.md:299` is the worked example). **Two are already contested:**
  `deployment-hierarchy-map.md:89` has *already filed* for the 11th `op_kind` **and** a new material class —
  so two programs want the same closed-enum slot, and the other one filed first.

### A6 — Stale premises this doc carried

- **Durability is not idea-phase.** `spec-item-durability-repair.md:3` reads *"written against shipped code
  2026-09-13"* and `deployment-hierarchy-map.md:11` records D1–D6 *"all locked by the owner."* This doc's
  "No build authorized" framing is what hid the ownership collision above.
- **`decisions.md:131` was overstated.** `KillerActorKey` is scoped to *"web/standalone battles where the
  server owns HP and resolution"*; on the lawn it carries nothing — which contradicts E1's "what exists".
- **Cite drifts:** `RarityLadder.PromoteFrom` is `:24`, not `:22-23`; `RpgStore.Delve.cs:711` is a closing
  brace (the skip is `:699`, and that loop is the **quest-reward** path, not the general haul path).

### A7 — ⭐ **The 904-container blocker has a ~1-file path the doc never considered**

The magnitude values **are already in SQLite.** `RpgStore.Species.cs:155-160` writes
`creature_species_magnitude` on import and reads it back at `:309`; measured over the committed tree,
**904 of 906 generated species carry magnitudes — mean 12.97 channels, 11,724 channel-entries.** So a
**synthesizer inside `ImportCreatureSpecies`**, calling the existing `UpsertAtom` / `UpsertContainer`
(`RpgStore.Atoms.cs:113`, `RpgStore.Containers.cs:220`), closes the gap with **one file and zero committed
corpus**. That reframes §3's headline item from *"a 904-row content program"* to **an importer change** —
and it is the single best finding of this audit.

### A8 — Cost and sequencing corrections (the "cheap" claims mostly fail)

- ⛔ **E3a is impossible as written.** `ExpeditionResolver.cs:172-173` are **const declarations**. The shard
  actually mints at `:94-98`, in the **battle-tick branch keyed on `isBoss`**, with **no species, element or
  rarity in scope** — and its own comment says shards drop *"at plan time, win or lose"*, so **there is no
  killed creature there at all.** Delivering "the killed creature's own rung" means moving the faucet to the
  battle-report/collect path: a different module. It also breaks `ExpeditionResolverTests.cs:149-161` (which
  reflects on the const *names*) and all four tier golden hashes. *What survives:* all ten shard ids already
  exist, so the no-vocabulary-change claim holds.
- ⛔ **E2 is not "one arm".** `LootMintAt.Mint` signals success via `out InstanceRow?` and `LootMintResult`
  carries only an instance id — a material has neither. The pipeline **never calls `Mint` for a Material**;
  the grant is emitted inline and `continue`s (`LootPipeline.cs:329-336`). Plus a player-id type mismatch
  (`PersistLootUnlocked` keys on `string`, the credit helpers on `long`). Honest size: **2–3 Data files +
  tests**, not one call.
- ⛔ **The propagation refactor is ~30 sites across ~24 files, not 6–8** — and two of my citations were
  wrong. `Dungeon/Tuning/EncounterTuning.cs:40-43` is the **threat** ladder, not rarity, so the "three
  duplicates" are duplicates of *two different ladders*; removing it needs a signature change plus 6
  call-site edits and would **lose an unguarded check** (no test covers that rejection path).
  `load_rarity_ids` is at `droptablegen/tuning.py:138-142`, not `:215-219`. There is also a **third legacy
  four-value vocabulary** I missed: `adapters/creatures/registries.py:36`. TS is ~10 declaration sites.
  *(`RungCount` → `Enum.GetValues` is genuinely clean: 3 call sites.)*
- ⚠ **The themes fix needs `--rebuild`, and that breaks append-only.** The merge *preserves* existing rarity
  (`adapters/creatures/themes.py:71-74`), so a normal re-run fixes nothing; `--rebuild` *"discards published
  themes, which append-only normally forbids"* and rewrites all 904 rows. Blast radius is low — `setgen`
  already forbids keying on theme rarity.
- ⛔ **My stated order was wrong and would pay for regeneration twice.** The threatBand fill rewrites every
  magnitude, so the container work must **follow** it, not precede it. A working order that regenerates
  **once**: **(d) FE repoint → (b) themes rebuild → (e) threatBand + one regeneration → (a) magnitude
  delivery → propagation guards → E2 → E6 → E1 → E3/E4/E5.**
- ✅ **D8's classification really is free.** The existing module was *run* this audit:
  `seedsmith creatures threat-band --histogram` scored all **719** anchors, well spread across all ten rungs
  (88/57/73/70/77/69/89/76/51/69). **But no writer exists** — both CLIs are report-only
  (`report/cli.py:1479-1563`), and the only model-free writer stamps the rung-4 default
  (`anchor/derive.py:70-71`). D8 needs a **new seedsmith module**, not a re-run.

### A9 — Ownership: the asks land in different maps than the doc assumed

- **`materialgen` and `droptablegen` are owned by `item-seedgen`, not seedsmith** — `item-seedgen-map.md:90`
  (module 3 `materials-gen`) and `:98` (module 10 `drop-tables-gen`). `seedsmith-map.md` contains **zero**
  occurrences of either.
- **`materialgen` structurally refuses D2's ask**, in its own words (`materialgen/__init__.py:1-10`): it
  authors *"`name` / `flavor` / `tags` for a material id — **never a new material id**"*. Another program
  (`loam-relics-and-wonders-ideal.md:244-245`) already hit this refusal and honoured it.
- **Defect 1 lands in seedsmith's `creature-themes`/`theme-refresh`** (`seedsmith-map.md:101`, `:355`), **not**
  creature-seed — and no module row currently owns a rarity migration of that registry.
- **`Repair` is already queued for the same 11th `MutationOpKind` slot**, and it was **filed**
  (`spec-item-durability-repair.md:107`, `:396`). Two programs, one closed-at-ten enum, one filing.
- **D7's vocabulary closure would overturn a named passing test.** `spec-anchor-contract.md:113-115` and
  `spec-classify-pipelines.md:86` make `family` *"open by construction"*, pinned by
  `new_family_value_is_recorded_not_rejected` (`:154`).
- ⭐ **Rule T-5 is the resolver creature-seed has been waiting for.** `creature-seed-map.md:296` records its
  last open amendment as ⛔ *"not actually actionable yet… `thetaOffset` has zero consumers anywhere in
  `src/`"*. T-5 is exactly the consumer that makes it actionable — this doc should name itself as the trigger.
- **`data/seed/loot/**` does not exist**; the real corpus is `data/seed/items/drop-tables/`
  (`item/entry-shapes.md:508-509`).
- **`species-rank` is an unlisted 19th creature-seed module** already claiming a `threatBand × rarity` grid
  and fusion/wave/expedition gates (`spec-species-rank.md:32-34`, `:56-63`) — adjacent surface to D7 and E6,
  self-filed as GAP-4 in `tasks/creature-seed-todo.md:100-109`.

### ⭐ A10 — THE REFRAME: this program's first module is not a tier

A dedicated species-selection trace, **verified twice by independent derivations**, found the finding that
reorders everything above.

| Surface | Pool | Selection | Repeat behaviour |
|---|---|---|---|
| Rift waves (`WaveCatalog.cs:165`) | 246/904 eligible, ordinal-first-N actually taken | **no RNG at all** | **10 species, identical forever** |
| Expedition battles (`ExpeditionResolver.cs:85`) | 4 wave ids, tier-switched | **no RNG** | fixed per tier |
| Expedition wild-met (`:235`) | ~34/159/49 by rarity gate | uniform in band | the **only** real roll — and it is not a fight |
| **Delve rooms (`Encounter.cs:177`)** | **≤419 anchors, climate-weighted, same-species anti-repeat cap** | **weighted RNG** | ⛔ **INERT — zero production callers** |
| World raise (`RaiseResolver.cs:157`) | 6 (one per climate) | **no RNG, ordinal-first** | always the same 6 |
| Lawn Zomboss deploy (`ZombossDeployPolicy.cs:79`) | hundreds | **`argmax(rarity)`** | **~3 species per entire run** |

**The ten species a player can ever fight in a wave, forever:** AshThreePeater, BoatImp, BucketZombieDuck,
ConeZombie, Apple, Bamboo, BigSunNut, BedRockSnowZombie, BlackElephantZombie, AcientSunNut. Confirmed on two
derivation paths (filename ordinal sort and parsed `speciesId`), byte-identical. The `Sunwoven` band holds
only 4 species, so `rift-tyrant`'s single sunwoven slot is **always** AcientSunNut and the other three are
unreachable in combat by any code path.

**And there is no way to hunt a chosen species.** `ObjectiveTargetKind` is closed at
`{RoomKind, CurioKind, ItemKind, Boss, None}` (`ObjectiveTemplateCatalog.cs:5-12`) — **no `SpeciesKind`**;
`grep -i species src/FusionRpg.Core/Delve/Quests/` returns **no matches at all**.

> ### The conclusion this audit forces
>
> **The tier machinery is in far better shape than this document assumed. What is actually broken is that
> the game shows the player 10 of its 904 creatures.** Species tiers, species trophies, species drop tables
> and family materials are all downstream of a species the player never meets. **No amount of tier design
> fixes that, and every hour spent on D2/D7 before it is spent on content with no faucet.**
>
> **The good news is that the fix is mostly already built.** `Encounter`/`SlotFill` is a real
> 419-anchor, climate-weighted selector with an XCOM-style same-species cap — it needs a caller, not a
> design. So the honest first module of a Monster-Hunter loop here is:
>
> 1. **Wire `Encounter.Build`** to a real delve room (a named wiring gap, not new architecture).
> 2. **Widen `WaveCatalog.Band`** past 4 rungs and give it an actual roll — its own comment already admits
>    the 4-rung limit was a stale-roster artifact.
> 3. **Add a species target to the objective vocabulary** — the "hunt this thing" verb, which is the
>    smallest possible version of the MH fantasy and unblocks every species-keyed idea in this document.
>
> Only then do trophies, families and tier-keyed drops become observable. **This reordering is the single
> most valuable output of the whole exercise**, and it is a stronger result than the design it replaces.

### A11 — Citation audit: every load-bearing claim confirmed, but the citations need a pass

Two independent verification passes checked every `file:line` and every count. **All priority claims
confirmed exactly** — `LootMintAt.Mint`'s refusing default arm, `DropEntryKind.Material` absent from
`UnavailableKinds`, `MutationOpKind` = 10 with no promotion kind, `CraftOperation.Elevate` with no executor
or route, `grep -rl "species-magnitude" data/` → nothing, `ReconcileCreatureMagnitudeBindingsUnlocked`
called on every deploy and failing closed, `CreatureTypeIdFloor = 10_000` and its validator throw, all four
FE sites, and **every creature- and item-corpus count** (904 anchors, 84 retired rarities, 19 vs 783
families intersecting `{sunflower}`, 104 gems with `tier` 0/104, 1,178 base types, the exact θ
distribution, 1,295 recipes, 677+227 sides, §10's 33 rows). `audit-overflow.py` was re-run: **0 critical**.

**Corrections that change meaning** (not mere drift):

- ⛔ **Several "zero production callers" claims are too strong.** `Encounter.Build` *is* called at
  `DomainEncounterCoverage.cs:96`; `EncounterPreflight.Run` at `DomainEncounterPreflightBridge.cs:75`;
  `WorldSectorLootSource.TryResolve` at `ClaimResolver.cs:129`. The **transitive** claim survives — those
  callers are themselves test-only — but the literal one does not. Worse, I inherited
  `DelveBattleSessionManager.cs:21`'s own stale "confirmed by a direct search" comment, which is exactly
  the *"code beats comments"* rule I was applying to others. **Confirmed still inert:**
  `EncounterCorpusBuilder.Build`, `EncounterSeedFile.LoadAll`, `DomainEncounterPreflight.Build`,
  `RpgStore.ImportDungeonDomains`.
- ⛔ **`BattleReporting.cs:92-93` uses `SiegeLoot.TryResolve`, not `WorldSectorLootSource`** — wrong type named.
- ⛔ **`RarityLadder.PromoteFrom` has a production caller** — `RpgStore.Items.cs:946` writes the constant `1`
  into `rarity_budget` for every rung. The accurate claim is *"no caller that varies by rung."*
- ⛔ **`RarityTuningCoverageTests` does not guard what I said.** It covers **5 tuning files, not 11**, and
  names **two explicit exceptions** (`RecipeCost`, `InheritCostByRarity`, seven rungs each). It is still a
  good pattern, but it is not the blanket propagation guard Rule T-4 cited it as.
- ⛔ **Propagation cost is *higher* than stated: 12 rarity-keyed tuning files (not 11) and 10 CSS vars
  (not 1)** — `tokens.css:45-54` carries one `--color-rarity-{id}` per rung.
- ⭐ **And Rule T-3 is more necessary than I argued.** `CreatureRarityLadder.RungCount = 10` is a hardcoded
  const while `All` (`:50-51`) already uses `Enum.GetValues<CreatureRarity>()` — **so the two disagree by
  construction the moment the enum widens**, and `OneRungAbove` then throws a message that *lies about why*
  (`"already the top rung (Almanac)"`). **No guard test asserts the identity.**
- ⛔ **Self-contradiction on `Forge`, now resolved.** The Wiring-gap list is right — `Forge` has no
  executor and no route. An earlier reading-gate line claimed a survey had "corrected"
  `crafting-coverage-engine.md`'s "no forge executor" note. **That correction was itself wrong and is
  struck; the original doc was right.**
- ⛔ **`minTierPlan` is not a real key.** No combination entry carries that field; the `[1,1,2,2]` shape is
  *derived* from per-ingredient `minTier` × `quantity`. The 76 combos, `grantedTier == 1` on all, 232
  ingredient entries and 118 at tier ≥ 2 are all confirmed — but a spec would go looking for a field that
  does not exist.
- **`item-map.md` has 21 modules, not 25**, and **X1–X7 are cross-program *dependencies*, not module ids.**
- **The 67-recipe breakdown omitted `reroll-all: 2`** (listed ops summed to 65). The dependent "24 of 67
  unreachable" figure is nonetheless correct — it only closes *because* those 2 are included.
- **Two grep claims were stated wrong even though their substance holds.** `enhanceTrack` returns 17,913
  hits under `src/` (copied seed JSON) — **zero in any `.cs`**, which is the real claim. "Zero occurrences
  of `circuit`" is 30 `.cs` hits, all the word *short-circuit* — no socket-circuit implementation exists.
- **Broken seedsmith cites:** `materialgen/vocab.py:248` is past EOF (file is 174 lines; the quote is `:163`,
  the assert `:120`); `droptablegen/tuning.py:215-219` is past EOF (file is 163 lines; `load_rarity_ids` is
  `:138-143`); `numerics/resolve.py:74` → the `bands[tier-1]` read is `:90`.
- **Quote hygiene:** the `gameplay-tiers-ideal.md:93` principle was paraphrased (now restored verbatim); the
  §10 preamble was stitched from two sentences; `ssot-rarity.md` §3.6 was truncated before *"the owner asked
  for"*; `tunables-ssot.md` T1 does not itself name the `data/tuning/<domain>.v{n}.json` path (§1/§2 do).
- **Also:** the item **class ladder is 4 rungs for *armour* only** (weapon 3, offhand 2, jewel 3, standard 2);
  D8's *"covers most of the roster with no model call"* belongs to module **3 `power-parse`**, not module 4;
  `store.GetSockets` has **7** production call sites, not 3 (all still card/surface/workbench); and six
  further `src/` comments still assert a non-existent `item_base_type` table that
  `RpgStore.BaseTypes.cs:21` creates.

**Nothing in this list overturns a decision.** The design findings in A1–A10 stand; this is a hygiene debt
that a `/spec` pass would otherwise inherit.

### What survived cleanly

PS-3 and the one-ladder rule (no private `f(tier)` anywhere) · `ssot-rarity.md` §3.2/§3.6 (no
rarity→magnitude leak) · caps/PS-8 classification · standalone-first in both directions · no pinned
population counts · **D2 genuinely clears the source-tagging ban** (that rule is an SC8 *mode-gate* rule,
and a species key creates no mode gate) · D3, D5, D6 undisputed and well-grounded · E3a and E5 collide with
nothing · `drop-tables-ideal.md` contemplates no species axis, so **E1 is uncontested territory**.

---

**Program:** `tier-system`. Map (when approved) → `docs/architecture/tier-system-map.md`; plan →
`tasks/tier-system-plan.md` + `tasks/tier-system-todo.md`. **Parent survey:**
[gameplay-tiers-ideal.md](gameplay-tiers-ideal.md) (2026-09-12), whose §"The shape" this doc executes on.
This doc amends no sealed ideal. It does not reopen `item-ideal.md`, `creature-seed-ideal.md`,
`drop-tables-ideal.md`, `strain-splice-host-ideal.md`, or `action-skill-tiers-ideal.md` — it names the
**edges between them** that no program owns.

---

## Which loop this extends

Named from [the-loops.md](../guide/the-loops.md), which is the product-vision SSOT. This doc invents no loop.

- **Spine B — creature summon and fusion.** Species tiers, fusion of duplicates, wild joins and capture.
- **Spine C — item collection and progression** — *"find, vault, equip, compare, craft, socket, salvage."*
  The whole crafting/material/socket half of this doc lives here.
- **Place 2 — idle expeditions, core forever.** Already described in the vision as
  *"Monster Hunter–style: pick creatures who are not on a live task, dispatch, wait, collect. …
  **Materials** for actors and empire"* — the request's MH framing is already the shipped pitch for this place.
- **Place 3 — farm, hunt, defend**, specifically the **Hunt** verb (*"lawn kills, capture, wild joins,
  roaming, delves, map prey"*).
- **Place 6 — the Delve**, today's live consumer of species threat.

The lawn stays the first core loop, not the whole game. **No fourth stock** — souls, essence and loam are
the three, and materials are a *shelf*, not a wallet (`ssot-materials-crafting.md` §6.4: *"Twenty-one rows
is a shelf, not a bag"*). No stamina gate, no player class, no prestige wipe, and expeditions are not
replaced by delves.

---

## Load-bearing principles, restated inline

A downstream session reads this doc, not its links. These are restated because each one constrains a
choice below.

- **Every RPG feature lives in the RPG layer — never by changing what PvZ is.** Tiers, materials, drop
  tables, recipes and sockets are RPG-layer data modelling in `FusionRpg.Core`/`FusionRpg.Data`. Nothing
  here touches PvZ's board, spawn or AI. *"Can the lawn express a species tier"* is the wrong question;
  *"does the RPG layer have the record/runtime for it, and is it wired"* is the right one.
- **One power ladder.** `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`. **Rule PS-3: contests read `Θ` (linear,
  difference-based); magnitudes read `P(Θ)`. Never the other way round.** `ssot-power-scale.md` §10 is a
  closed inventory of 33 rows (counted) and its own preamble calls itself *"the anti-duplication clause —
  a power-shaped number that is not in this table does not have permission to exist."*
- **The sanctioned tier→magnitude path already exists, and it is a table, not a curve.** §5.3: a tier
  contributes *"a per-rung integer added directly into `Θ` before `P(Θ)` is applied … **never a formula**:
  the captured PvZ stat distribution is lumpy, not smooth, so a fitted curve would empty half the rungs.
  `P(Θ)` is applied once, downstream … the offset is never scaled a second time."* Shipped at
  `Creatures/Generation/SpeciesExpander.cs:66-68`. **A new tier ladder earns a `thetaOffset` column in
  tuning plus a §10 row. It never earns a curve.**
- **The balance surface is data.** Every threshold, weight, drop odd and recipe cost lives in
  `data/tuning/<domain>.v{n}.json` (`tunables-ssot.md` T1), carries its unit (T6), and a missing value is
  a load rejection naming it, never a silent default (T5).
- **No hard progression ceilings.** A tier top is content breadth, not a wall; absolute bounds throw
  rather than clamp. **A drop-*probability* floor is exempt** — PS-8 exempts bounded ratios by their
  nature, and `tunables-ssot.md` §1 lists "drop odds" as an ordinary tunable.
- **Rarity never touches a magnitude.** `ssot-rarity.md` §3.6 bans `CurveInput.Rarity` on
  `container_kind = 'item'`: *"a multiplier on the rung makes rarity dominant and destroys the overlap."*
  A rung sets a **count band, a tier floor and a tier ceiling** — nothing else.
- **Seed → concrete → per-player.** Seedsmith emits enum-only seeds offline; deterministic code writes
  every magnitude; only effects roll per player. Enforced mechanically, not by review.
- **A guardrail validates the contract and closed enums — never a population count.** Every corpus size
  in this document is a **reading that grows as content ships**, never an acceptance value.
- **Each tier axis owns its own closed vocabulary.** Owner-confirmed 2026-09-12 and binding on all future
  tier work (`gameplay-tiers-ideal.md:93`), quoted **verbatim** — an earlier draft paraphrased it and
  dropped the load-bearing final clause: *"unifying all ranks into one shared enum is lazy design,
  collapsing distinct axes (scarcity vs hunting fantasy) into one name **that can never say anything the
  other doesn't**."* **This doc therefore proposes shared propagation machinery over distinct vocabularies,
  never one unified ladder.**

---

## What this is

In the player's language: hunting a *particular kind of creature* should be how you get the stuff that
builds a *particular kind of gear*. A tougher creature of the same kind should yield a better grade of the
same material, an early creature should stay worth hunting because its common parts never stop being an
ingredient, and the thing you are saving up for should be visible on a bench rather than guessed at.

In system language: **every layer in the chain already has a tier ladder, and almost none of the edges
between the layers exist.** Species carry two ten-rung ladders and a ten-step star; items carry a ten-rung
rarity ladder, a five-step affix tier, an item-level gate and a four-rung class ladder; materials carry a
four-step substrate grade and a ten-rung shard band; sockets carry per-role ceilings and rarity grants.
What is missing is not a ladder. It is: **nothing connects a creature to a drop, nothing connects a drop to
the material shelf, nothing connects a material grade to a socket, and no item can be an input to making
another item.** This document names those edges, the three measured inconsistencies already sitting in the
chain, and the propagation contract that makes adding a rung cheap instead of a 22-file chore.

---

## What already exists

Verified against `src/`, `data/`, `tools/` and the shipped corpora on 2026-09-12/13 by eight parallel
code-verified surveys plus a direct reading pass. **Counts were obtained by counting and are readings, not
constants.** Three buckets, exact words.

### Built

**The ladders themselves — every layer has one.**
- Creature `threatBand`: 10 rungs `nuisance…calamity`, `data/tuning/creature-threat.v1.json`, each with a
  `thetaOffset` — measured `0, 4, 9, 13, 18, 22, 27, 31, 36, 40`.
- Creature/item `rarity`: 10 rungs `chaff…almanac`, ordinals **spaced by 10 specifically so a rung can be
  inserted at 15 or 85 without renumbering** (`ssot-rarity.md` §3.3, §8.2). Creatures adopted this ladder
  2026-09-01; the old four-value enum is *"a migration shim only"* (§4.3).
- Specimen `star` 1–10, **capped by rarity** — `Creatures/Fusion/StarPolicy.cs:29`, cap at `:74`.
- Item affix tier t1–t5 (`Items/IlvlTierLadder.cs:13`), the ilvl gate `MinIlvlByTier = {1,1,8,18,32}`
  (`:10`) and the collapsing envelope `maxTier = Math.Min(bandMaxTier, MaxTierAt(ilvl))` (`:29-34`).
- Item **class ladder**, 4 rungs per frame — `classes.v3.json`: humanoid armour `cloth→leather→scale→plate`,
  plant armour `fibre→husk→bark→heartwood`.
- Material substrate `grade` 1–4 and shard bands over the 10 rungs; **27 material ids**, counted from code
  (`MaterialCatalog.All`): 10 shards + 8 substrates (2 frames × 4 grades) + 6 essences + 3 catalysts.
  The creature side's 16 (`CreatureMaterialCatalog.cs:19-27`) are **reused, not re-minted**, by the item
  side at `Items/Materials/MaterialCatalog.cs:74`.
- Socket per-role ceilings (15 rows, `sockets.v1.json:5-21`) and rarity socket grants
  (`chaff {0,0}` … `almanac {2,4}`), parser-checked for overlap and monotonicity
  (`SocketTuning.cs:285-302`).
- Action `Rung` 1–10 with per-rung tier windows (`action-rungs.v2.json`).

**`threatBand → Θ → P(Θ) → magnitudes` is the one automatic propagation path — and it is a *build-time
bake*, not a runtime read.** `SpeciesExpander.cs:66-68` computes `thetaOffset → theta → pTheta` and every
magnitude reads that one `pTheta` (`:103`). Proof by measurement over all 904 generated species:
`Blover` θ=0 → `pTheta` 80 → hp 480; `Gargantuar` θ=13 → `pTheta` 452 → hp 6,328. **But
`CreatureThreatTuningLoader.Parse` has zero callers in `src/FusionRpg.Server`, `src/FusionRpg.Injector` or
`src/FusionRpg.Data`** — every caller is a `tools/*` CLI. The result is baked into
`data/generated/creatures/*.json` (which carry `theta`/`pTheta`/`magnitudes` and **drop `threatBand`
entirely**), and `theta`/`pTheta` are then stored in `creature_species` (`RpgStore.Species.cs:143-144`) and
**never read back into combat**. See the wiring gap below for where the baked result stops.

**`star` is genuinely live through the sanctioned gate.** `StarLoyaltySubsystem.cs:41-42` folds star into
derived stats, registered on **ActorHub** at `ActorHub.cs:157-158` and wired at
`UniqueActorHubCompose.cs:62-71` — a real `IActorStatSubsystem` contribution, not a private fold. Its
null-delegate guard cannot fire in production because registration is itself conditional.

**A species→material edge already exists — exactly one.** `Expeditions/ExpeditionResolver.cs:134`:
`var essence = "essence." + species.ElementPrimary.ToElementId();`, fired when a wild creature is met and
does **not** join, for exactly `+1` (`:136`). It reads `ElementPrimary` only — never species id, never a
tier. Credited via `RpgStore.Expeditions.cs:204`, spent at `RpgStore.Fusion.cs:527`. End to end.

**Tier-aware crafting already exists on the creature side, twice.**
`FusionCostTable.StarMerge(CreatureRarity baseRarity)` spends a shard **of the creature's own rarity rung**
(`StarPolicy.cs:94-97`), and `CreatureRecipeDef(RecipeId, OutputSpeciesId, InputSpeciesIdA, InputSpeciesIdB)`
mints an output species from two band-below inputs. **This is the A+B→C shape the item side does not have,
already shipped one layer over.**

**The crafting engine is real.** Ten `CraftOperation` verbs (`CostClassMatrix.cs:11-44`), all ten priced in
`data/tuning/materials.v1.json`. Six execute end to end with a route and an executor: `salvage`, `upcycle`,
`temper`(enhance), `bore`(socket-add), `socket`(socket-insert), `imbue`(socket-imbue). Recipe corpus is a
reading of **67 rows** — `temper` 35, `elevate` 10, `forge` 7, `reroll-one` 5, `upcycle` 4, `bore` 3,
`socket` 1, `forge-gem` 0, `imbue` 0.

**Material tier ← item tier is a real computed dependency, not a table.** `MaterialTuning.cs:122`:
`return 1 + Math.Min(MaxGrade - 1, itemLevel / ItemLevelPerGrade);`, read by `SalvagePolicy.cs:68` and
`MaterialRecipeCatalog.cs:337`. The **grade lock** this creates is the thing volume cannot buy. Salvage
returns a shard of **rung−1, never the item's own** (`SalvagePolicy.cs:71-79`).

**The loot pipeline is twelve real, tested steps** (`LootPipeline.Resolve`, `:181`), with the per-source
carrier already generic: `loot_source(source_kind, source_id, table_id, content_level, first_clear_grant)`.
Item level reads content and nothing else (`:219-221`), volume reads `Θ_actor` linearly, quality reads
`P(Θ_content)` — the D18 two-axis split, already shipped.

**Both generators the request asks for already exist.** `adapters/items/materialgen/` (731 lines) mirrors
`MaterialCatalog.Build()` mechanically and asserts `len(ISSUABLE) == 27`, refusing invented ids
(`vocab.py:248`: *"materials-gen never invents a new material id"*) — run this session:
`{"toGenerate": 0, "alreadyPresent": 31}`. `adapters/items/droptablegen/` (964 lines) — run this session:
83 entries on disk; the model picks only a name and one `dropBand` and *"never sees a role, a frame, a
material ref, a weight, or a curve id"* (`schema.py:6-8`).

**Tier already shapes generation in four places** — `numerics/resolve.py:74` (`bands[tier-1]`, the canonical
one), `creatures/anchor/derive.py:187` (rarity decides variant *count*), `species_effects.py:52`,
`items/uniques/briefs.py:82-92`.

**Numeric and validation discipline hold.** `python scripts/audit-overflow.py` → **0 critical**, and zero
findings on the item/material/recipe/fusion path. `ConcreteSpecies.PTheta` is `long`; `Theta` stays `int`
correctly because it is an index, not a magnitude. `validation-ssot.md` §1 lists the rarity ladder as a
**closed vocabulary**, so pinning 10 is correct. `RarityTuningCoverageTests.cs:29-43` already asserts every
rarity-keyed tuning map carries exactly ten rungs — **a genuine propagation guard, and the pattern to copy.**

**A disjoint creature id-space already exists, and `GameTypeId` is already modelled as a costume.**
`CreatureSpeciesCatalog.cs:51` — `public const int CreatureTypeIdFloor = 10_000;`, *"Disjoint id-space
floor: web-battle events must never collide with PvZ type ids"* — with a wide per-side split
(`CreatureTypeId = floor + (side == "plant" ? 50_000 : 0) + GameTypeId`, zombie 10,000+, plant 60,000+) and
a validator that throws below the floor (`:108-109`). `ConcreteSpecies.cs:50` describes `GameTypeId` as
*"the PvZ type id whose art/dumps this species **wears**."* `CreatureAcquisition`
(`Summonable`/`CaptureOnly`/`EventOnly`, `CreatureRarity.cs:32-38`) already expresses "not obtainable on
the lawn", and `CreatureDeployMode` (`PlantAvatar`/`HypnoAlly`) already expresses *how* a species shows up
in a run. **A species whose identity is not a PvZ actor is therefore already expressible — it is a content
and art question, not an architectural one.**

**Two cross-layer bridges are already built and populated.** The creature theme registry
(`data/seed/creatures/_registry/themes.v1.json`, 904 themes) publishes one-way to items and carries per
species a `rarity`, `motifs`, and an explicit `expression.item: "material and form — what it is made of,
what shape it takes"`. And `decisions.md:132` already locks a **`unique-species` set class** — species-bound,
parameterised ten- and fifteen-role templates, exactly two thresholds, never eligible for a hybrid body.

### Wiring gap

*Machinery exists and is inert. None of these is a wall; each names the line.*

- **Drop tables → the material shelf is one missing credit call.** `DropEntryKind.Material` is **not** in
  `UnavailableKinds` (`DropTableModel.cs:146-179`), so a table may draw it, and `LootPipeline.cs:334`
  already emits the grant — but `PersistLootUnlocked` (`RpgStore.Loot.cs:466-537`) writes no
  `rpg_creature_materials` row and `LootMintAt.Mint`'s default arm (`LootMintAt.cs:104-107`) returns
  *"mintAt supports only Equipment and Unique today"*. The delve banking loop skips it
  (`RpgStore.Delve.cs:711`). **One arm, not a system.**
- **Five of the ten craft verbs have no executor** — `Elevate`, `Forge`, `ForgeGem`, `RerollOne`,
  `RerollAll` appear only in `CostClassMatrix.cs`'s vocabulary and pricing tables. Consequence, counted:
  **24 of the 67 authored recipes are priced, loaded, and served by `GET /api/items/workbench/recipes`
  with no POST that can spend them.**
- **Rarity promotion is the tier-progression verb and it cannot be persisted.** `MutationOpKind`
  (`MutationOp.cs:13-48`) is a closed 10-member namespace with **no rarity-promotion kind**, and
  `RpgStore.AppendInstanceOp` takes a `MutationOpKind` — so an elevate has no way to be written. Ten
  authored `elevate` recipes are unreachable; a POST refuses `material.operation-mismatch`.
  `RarityLadder.cs:22-23` already declares the rule it would walk — *"no rung is drop-only. All ten promote
  from a lower rung"* — as a stub returning a hardcoded `1`, with no callers.
- **Gems carry no tier at all.** Counted: **104 gem entries**, `powerBand` 104/104, `tier` **0/104**,
  `element` 8/104. `ItemCardEndpoints.cs:127` `UnauthoredInsertTier = 1` makes every insert report tier 1.
  **Consequence, counted: all 76 shipped combinations use `minTierPlan [1,1,2,2]`, so 118 of 232 ingredient
  slots require tier ≥ 2 and no shipped gem can satisfy them.** The combination system is half
  unsatisfiable by content, not by design.
- **Eight-socket topology is decided and unapplied.** `decisions.md:133` retires the four-maximum and
  defines circuits as `floor(socketIndex / 4)`. Shipped tuning still says `structuralCeiling: 4`
  (`sockets.v1.json:23`), mirrored by `SocketTuning.cs:41`, with a boot throw at `:148-152` making 8
  unloadable — and **zero occurrences of "circuit" anywhere in `src/`**.
- **`imbue` and `forge-gem` are priced and routeable with zero recipes.** `/api/items/workbench/socket-imbue`
  is live and `materials.v1.json:54-61` prices imbue, but 0 of 67 recipes author either verb, so both
  always refuse `material.recipe-unknown` (`ItemWorkbench.cs:372`).
- **Gem upcycling is tuning-only.** `sockets.v1.json:40-44` `upcycleInputPerOutput: 3` is parsed and then
  referenced nowhere else in `src/` — no executor, no recipe, no endpoint. (The *material* upcycle, 5→1,
  does work.)
- **The per-item upgrade path is authored and read by nothing.** All **1,178** base-type entries carry an
  `enhanceTrack` (`[{"atLevel":4,"family":"atom.enhance-edge"}, …]`) plus a 19-row milestones file, and
  `grep -rn "enhanceTrack" src/` returns **zero hits**. `ItemWorkbench.cs:272` passes
  `Array.Empty<AtomAppend>()`, so no milestone atom is ever appended.
- **Sockets reach no combat.** `store.GetSockets` has three production readers, all card/surface/workbench.
  Nothing in `Battle/`, equip runtime or the injector reads a socket.
- ⭐ **The baked species magnitudes reach a live call site and then dead-end on missing content.**
  `ReconcileCreatureMagnitudeBindingsUnlocked` (`RpgStore.UniqueActors.cs:1602-1607`) **is** reached in
  normal play — every deploy, via `:216` — but always no-ops: it resolves
  `SpeciesMagnitudeContainerId(profile.SpeciesId)`, gets null from `GetContainer`, and fails closed.
  **`grep -rl "species-magnitude" data/` returns nothing** — zero committed `trait.species-magnitude-*`
  containers exist, confirmed by the test's own header (`CreatureLawnDeployMagnitudeTests.cs:13`:
  *"No real committed species-magnitude atom/container content exists yet"*). **This single missing corpus
  is what severs the baked `theta`→magnitudes result from gameplay.** It is a content gap behind a live,
  correct call site — the cheapest high-value fix in this document.
- **The encounter generator computes species-tier difficulty and nothing calls it.** `Encounter.cs:200`
  composes `roomTheta + OffsetFor(ThreatBand)`, but `Encounter.Build` (`:91`) has **zero call sites in
  `src/`** — every real call is a test. The same holds for `EncounterCorpusBuilder.Build`,
  `EncounterPreflight.Run`, `EncounterSeedFile.LoadAll`, `DomainEncounterPreflight.Build` and
  `RpgStore.ImportDungeonDomains` — an entire inert subtree, each layer citing the one below it.
- **Delve loot floors read room kind, never a species.** `Delve/Loot/RarityShift.cs:23-32, 108-127`
  resolves `Rung.RarityFloor` / `RoomKindRarityFloor` — reinforcing that no species signal reaches any
  drop decision anywhere.
- **`threatBand` is excluded from the unresolved-skip list**, so a null band **silently** takes the rung-4
  default at generation time (`SpeciesExpander.cs:31-33`) instead of refusing. That is the mechanism behind
  defect 3 below.
- **Anchor `family[]` is dropped at the C# boundary.** All 904 anchors carry `family` and the species file
  layout *is* the family grouping (549 files), but `AnchorRow.cs:34-40` has no `Family` field, so 0 of 906
  generated files carry it. **One `IReadOnlyList<string> Family` on the record re-opens it.**
- **`retinueFamily` reaches the database and dead-ends** — parsed `DomainSeedFile.cs:46`, persisted
  `RpgStore.Domains.cs:279`, read by no creature-selection code.
- **`WorldSectorLootSource.TryResolve` is built and tested with zero production callers**; siege and
  world-sector both resolve a source row and stop (`BattleReporting.cs:94-98`, `ClaimResolver.cs:124-134`).
- **`affix_channel ∈ {drop, boss}` is authored and inert** — `DropTableModel.cs:34-36` says so verbatim.
- **Species cannot be generated *by* tier, but nothing forbids it.** There is no `--rarity`/`--tier` target
  in the creatures CLI; rarity and threatBand are classification *outputs* that may be `unresolved` and are
  healed afterwards. The machinery to *consume* a tier exists (`numerics/resolve.py:74`); no entry point
  *offers* one. Likewise `--band` in `basetypegen` shapes numbers but not the model's answer space —
  the candidate pool is role+frame only (`brief.py:71`).
- **Automatic propagation is itself a wiring gap.** Measured: adding one rung to `threatBand` (**declared in
  tuning**) touches **6 sites**, one of which is a defect — a hardcoded C# copy of all ten ids at
  `Dungeon/Tuning/EncounterTuning.cs:42` — *"every other consumer is a table lookup, so it does not move."*
  Adding one rung to `CreatureRarity` (**declared as a C# enum**) touches **≥22 files**: 4 C# edits, 11
  rarity-keyed tuning files, 4 Python tuples, 2 web declarations plus a CSS var, 3+ tests. A second
  measurement counting edits rather than files put the same job at ~43. **The difference is not the ladder;
  it is where the ladder is declared.**

### Real gap

*No mechanism exists anywhere.*

- **No creature → drop-table link.** `DropTableValidator.KnownSourceKinds` (`:58-59`) is a closed eight-value
  list — `web-wave, expedition-tier, world-sector, pvz-run, dungeon-room, dungeon-clear, dungeon-quest,
  siege-assault` — with **no species kind**. `LootRequest` has no species field; a grep for "species" across
  `Items/Drops/`, `Delve/Loot/` and `RpgStore.Loot.cs` returns one comment. **The generic carrier already
  exists** (`LootSourceRow(SourceKind, SourceId, TableId, ContentLevel)`), so this is a vocabulary and
  authoring job, not new architecture.
- **No per-species material, and the vocabulary refuses one today.** All 27 ids are keyed by element,
  rarity rung, frame+grade, or verb — there is no species axis in `MaterialId`. `MaterialCatalog.ClassOf`
  **throws** on anything outside the set and explicitly refuses source-tagged ids like `essence.fire.pvz`.
  A species-keyed material is an **ask-first vocabulary change**, not a data edit — **now decided in D2**,
  as a three-layer general/family/species-unique model with the species layer held to 1–2 per species.
  Relatedly, the two shard faucets that exist mint from hardcoded constants —
  `ExpeditionResolver.cs:172-173` `"shard.chaff"` / `"shard.cultivated"` — **never from the killed
  creature's own rung**, which E3a fixes with no vocabulary change at all.
- **No item upgrade tree, and the recipe schema cannot express one.** Exhaustive check: all 1,042 JSON files
  under `data/seed/items/` parsed, all 3,363 distinct keys collected — **zero** structural hits for
  `upgradeTo`/`evolvesTo`/`successor`/`nextTier`/`consumes`. A recipe's only inputs are
  `AuthoredCostLine(MaterialId, CostBand)` and `CostClassMatrix.Allows` admits only the five material
  classes, so **an item cannot be a recipe input**; an item id in a cost line is refused at import. All 67
  rows are `outputKind ∈ {container, material, mutation}`. The **class ladder is the closest thing to an
  item chain and it is ordering only** — no field or code links `cloth` to `leather`, and the registry's
  `rung` integers are read by no C# item code.
- **Material grade ↔ socket/gem tier is unlinked**, and deliberately so today: `decisions.md:133` states
  *"Gem grade, rarity, and socket count remain separate axes."* A grep for `MaterialGrade|GradeOf` inside
  `Items/Sockets/` and `ItemWorkbench.cs` returns nothing. The three `bore` recipes *name* a grade, but
  hand-authored per row, not derived.
- **Items carry no intrinsic element.** None of the 1,178 base types has an `element` key; an item's element
  is derived at read time from its drawn affix variants (`ItemWorkbench.cs:578-580`) and its only consumer
  is salvage essence yield. Element→combat exists; **the item→element→combat edge exists in no file.**
- **No durability, wear or repair in code.** A module spec was written 2026-09-12
  (`deployment-hierarchy/spec-item-durability-repair.md`, per-instance `(max, current)`, `max` DERIVED,
  two-tier repair) but its parent ideal is idea-phase and self-labelled *"No build authorized"*, and a
  search for `durabilit|repair|wear|condition_pct` across `src/` finds only unrelated hits.
- **PvZ lawn kills carry no species or depth signal.** `RpgStore.Souls.cs:20-27` — *"a `ZombieKilled` fact
  records only that a kill happened"* — and `pvz-run` is an `UndesignedSourceKind` refused by name at
  `LootPipeline.cs:210-213`, so a lawn kill cannot reach the loot pipeline at all.

### Four measured inconsistencies already in the chain

Each was found by counting or by direct trace, and each is a defect in the dependency chain the request asks
to make consistent. **Defect 4 is player-visible today.**

1. **The theme registry carries a retired rarity vocabulary for 84 of 904 species.** The anchors are fully
   migrated (904 species, ten-rung ids only), but `themes.v1.json` still carries `common` 42, `rare` 21,
   `epic` 14, `legendary` 7 — a four-value ladder `ssot-rarity.md` §4.3 calls *"a migration shim only … no
   new code may branch on it after the migration completes."* Item module 13 `set-charm-gen` is specced to
   consume this registry, so a species→item generation run today reads a retired rung for 84 species.
2. ~~**Two creature-family vocabularies have drifted to near-disjoint.**~~ **Corrected 2026-09-13 — this is
   not drift.** The registry has **19** canonical ids, the anchors carry **783** distinct labels, and the
   intersection is **`{sunflower}`** — but measurement showed the two are **orthogonal concepts sharing a
   field name**, not two versions of one vocabulary: the registry is *PvZ lineage* (Chinese native labels
   curated into ancestral lines), the anchor labels are *LLM-authored themes*. The species file layout
   follows the themes (547 stems, mean **1.65** species per group), and `family-assignments.json` covers
   **53 of 904**. The real defects are narrower and both remain: the theme vocabulary is **open and
   sprawling** (`carnivorous flora` beside `artillery-flora`; `explosive-flora` beside
   `explosive-botanical`), and **neither axis reaches C# at all** — `AnchorRow.cs:34-40` has no `Family`
   field, so 0 of 906 generated files carry either one. See **D7**.
3. **The species tier ladder is content-starved where the machinery is best.** 719 of 904 anchors carry no
   `threatBand`, which puts **730 of 904 species on the rung-4 default** (θ=13). The measured θ distribution
   is `{0:136, 4:3, 9:1, 13:730, 18:10, 22:1, 27:12, 31:1, 36:4, 40:6}`. The best-propagating ladder in the
   repo is running on one rung for 81% of its corpus — *a content problem, not a mechanism problem.*
4. **Four FE sites still key on the retired four-value rarity vocabulary, and the misses are visible.** The
   server ships ten-rung ids (`CreatureRarity.cs:52-65`), but `rosterSplit.ts:5`'s
   `RARITY_ORDER {legendary, epic, rare, common}` misses every real id and falls to `?? 9`, so **every
   specimen ranks equal and the Active/Reserve "rarity desc" sort silently degenerates to created-date
   order**; `CreaturesPage.tsx:396-403` returns `3` for every real rarity, making that sort inert;
   `CreaturesPage.tsx:30-36`'s badge map falls through to the raw id string; and `patronView.ts:8-13` falls
   to `?? 0`, so the patron aura preview always reads base 0. (`contract/adapt.ts:182-191` *is* the correct
   ten-rung ladder — but it serves item rarity, not creature profile rarity.) This is the same migration
   shim as defect 1, surfacing on a different side of the wire.

---

## Prior art

Numbers, formulas and documented failure modes. Sources cited; anything unverified is flagged as unverified
rather than repeated as fact.

### Monster Hunter — the requested loop, and how it re-tiers

- **The same monster re-tiers its own materials by rank**: `Rathalos Scale` (LR) → `Scale+` (HR) →
  `Shard`/`Cortex` (MR), and **an LR Rathalos cannot drop a Ruby at all**
  ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Rathalos_Scale_Plus)).
- **Real drop rates.** Rathalos Ruby at HR: body carve **1%**, tail **2%**, capture **1%**, head break **3%**,
  silver reward **6%**, gold **13%** ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Rathalos+Ruby)).
  Rathalos Plate: carve **7%**, capture **7%**, quest reward **3%**. **MR deliberately loosened the gem wall
  by roughly 10×** — Rath Gleam: carve **14%**, capture **14%**, **back break 30%**
  ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Rath+Gleam)). *The top rank made rares more
  common, not rarer.*
- **Capture ≈ 5 reward rolls vs a kill's 3 carves**; investigations add 2–5 bonus slots against the same
  table with different weights ([GameRevolution](https://www.gamerevolution.com/guides/365207-monster-hunter-world-capture-vs-kill-rewards-get)).
- **Craft-step shape, consistent across the weapon tree: 1 generic filler + 3 species-tier commons +
  exactly 1 rare gate.** e.g. `Monster Solidbone ×5` (generic) + `Pink Rathian Shard ×4` + `Cortex ×3` +
  `Rathian Mantle ×1` ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Wyvern+Blade+Luna)).
- **Weapon trees branch and roll back** — downgrading refunds **all materials, no zenny**, and is blocked once
  augmented ([GameWith](https://gamewith.net/monsterhunterworld-iceborne/article/show/9212)). **Armor is not a
  tree** — flat craft plus Armor Spheres worth +1/+5/+20/+80, each level = **+2 defense**
  ([Fandom](https://monsterhunter.fandom.com/wiki/Armor_Spheres)).
- **Rank step size**: HR armour pieces 32–60 def → max 54–72 → augmented 86; MR 114–150 → 130–158 → 152–176 —
  roughly a **2× step** ([Fextralife](https://fextralife.com/monster-hunter-world-iceborne-all-armor-limits/)).
  Monster HP LR→HR ≈ ×3.0 (player-reported, **precise values unverified** — the Fandom HP table is paywalled).
- **The documented failure mode is the rare-drop wall.** GamesRadar on Iceborne: builds *"walled off unless
  you spend literally hundreds of hours grinding Tempered Investigations"*; Wilds executive director Kaname
  Fujioka **finished World without completing his build**
  ([GamesRadar](https://www.gamesradar.com/games/monster-hunter/monster-hunter-wilds-lead-was-also-crushed-by-monster-hunter-worlds-brutal-decoration-rng-i-ended-up-finishing-the-game-without-having-completed-my-build/)).
- **Mitigations that shipped**: Elder Melder Wyverian Prints — **Gold = any HR gem for 100 points,
  deterministic**, throttled to about **1 Gold Print/week**
  ([Fextralife](https://monsterhunterworld.wiki.fextralife.com/Elder%20Melder); weekly cap player-reported).
- **Keeping low tiers alive — three shipped mechanisms**: rank-suffixed re-tiering; **generic fillers
  (`Monster Solidbone`) any monster supplies**; and **Guiding Lands**, where early monsters return at MR with
  region levels 1–7 gating tempered variants ([PC Gamer](https://www.pcgamer.com/mhw-iceborne-guiding-lands-guide-levels-tempered-monsters/)).
- **Inventory bloat is real**: MHW's datamined `itemData` spans IDs **0–2315** (includes placeholders;
  distinct *usable* materials **unverified**) ([modding wiki](https://github.com/Ezekial711/MonsterHunterWorldModding/wiki/Item-IDs)).
- **The socket analogue's correction arc is decisive.** MHW decorations were pure RNG — published Capcom
  feystone table Warped **0/77/18/5**, an Attack Jewel at **0.30%** even from the best stone — and melding
  did not fix it. **Rise made decorations fully craftable, Sunbreak added Aurora Melding at 100% on one chosen
  skill, and Wilds made all decorations craftable**
  ([Fextralife](https://fextralife.com/monster-hunter-end-game-guide-drop-percent/),
  [Game8](https://game8.co/games/Monster-Hunter-Rise/archives/327175)). **The series moved sockets from RNG to
  deterministic.**
- **No MH-like ships a pity counter** (Dauntless, Wild Hearts, God Eater, Toukiden all checked). This repo's
  guarded rungs at 70 and 90 are already ahead of the genre. Three deterministic mechanisms worth noting:
  Dauntless's **guaranteed common break-part on a successful break** plus **Cell Fusion** (2 identical →
  1 higher rarity, same perk); Wild Hearts' **100% material refund on revert**; God Eater's **Exchange, which
  converts only into materials you have already obtained once**, with per-mission drop percentages **shown
  in-game**.

### Path of Exile — the axis split, structurally

- Every modifier carries a **mod level**; the roll **discards every mod whose level exceeds the item's ilvl
  before the weighted draw**, so a low-ilvl item cannot roll a top tier and **there is no post-roll clamp**
  ([Modifiers](https://pathofexile.fandom.com/wiki/Modifiers)). Mod level also raises the item's level
  requirement to **80%** of its value.
- **Rarity controls count only** — Rare = up to 3 prefixes + 3 suffixes; Normal = 0. The proof the axes are
  orthogonal: **a Normal, rarity-zero, ilvl-86 base is among the most valuable items in the economy.**
- A real tier ladder (`+# to maximum Life`, body armour): T1 **+8–10 @ ilvl 15**, T2 **+11–14 @ 24**,
  T3 **+15–19 @ 36**, T4 **+20–24 @ 48**, T5 **+25–30** ([poedb](https://poedb.tw/us/Body_Armours)).
  **The top of that ladder is unverified** — poewiki hard-blocks automated fetch. GGG's own manifesto gates
  new top tiers at **ilvl 81 / 83 / 84** ([GGG](https://www.pathofexile.com/forum/view-thread/1290356)).
- **Deterministic crafting is priced as variance insurance, not value.** 6-link base chance is **1/1500 per
  Orb of Fusing**; the Crafting Bench deterministic 6-link costs a flat **1500 Orbs**
  ([vhpg](https://www.vhpg.com/orb-of-fusing/)) — **roughly EV-neutral and variance-free.** (A widely repeated
  "mean ~1000, 95% CI 836–1244" figure traces only to an SEO blog and is **unverified**.)
- GGG on the tension, in their own words: *"Why would I use a regular Exalted/Divine/Annul Orb when I can get
  one through Harvest that has a deterministic result?"* and *"We don't want to take away the feeling of
  closing your eyes and Exalting an item"* ([manifesto](http://www.pathofexile.com/forum/view-thread/3069670)).
  The 3.14 Harvest nerf drew a ~400-page thread.

### Last Epoch — the designer statement for keeping the axes apart

EHG decouple rarity from affix tier explicitly: Exalted is **defined by carrying a T6/T7 affix**, not by
affix count. Their stated reason for making T6/T7 drop-only: T5 access made it *"a little too easy to get
near perfect items so quickly. This makes it far less exciting to hunt for gear instead of simply gambling
and crafting."* Tier gates: **T5 from level 32, T6 from 55, T7 from 90**
([EHG](https://forum.lastepoch.com/t/introducing-tier-6-and-7-item-affixes/22279)). Their **Forging
Potential** model is the anti-bricking pattern: *"Each craft uses a random amount of Forging Potential,
normally around 1 to 15 … **until then all crafts will be successful**"* — crafts never fail; a depleting
budget ends craftability instead ([EHG](https://forum.lastepoch.com/t/crafting-changes-coming-to-eternal-legends-update-0-8-4/45597)).

### Diablo — the cautionary tales

- **D4's collapsed axis.** Item Power ran in bands (0: 1–149 … 4: 625–724, 5: 725+) but affix values did not
  move *within* a band — Icy Veins' worked example shows crossing 725 rerolling Thorns 273 → 470 while
  intra-band upgrades are noise ([exputer](https://exputer.com/guides/diablo-4-item-power-breakpoints/),
  [Icy Veins](https://www.icy-veins.com/d4/news/diablo-4-item-level-breakpoints-and-why-theyre-important/)).
  **A continuous number that was mechanically a five-value enum.** Blizzard's Season 4 fix: affixes cut to
  **3 on Legendary, 2 on Rare**; **Greater Affixes at 1.5×** on Ancestral only; and *"Legendary items dropped
  from enemy level 95+ are always 925 item power"* — i.e. **they deleted the intra-tier gradient and made the
  tier itself the only signal** ([Blizzard](https://news.blizzard.com/en-us/article/24077223/galvanize-your-legend-in-season-4-loot-reborn)).
  Greater-Affix probability is **not published** — do not quote a number.
- **The tier-reset treadmill, documented.** Season 4's systems were **not retroactive** — legacy items can
  neither Temper nor Masterwork — and it **repeated in Season 5**, with **no grandfathering shipped**
  ([Dot Esports](https://dotesports.com/diablo/news/diablo-4-legacy-items-explained)). D3 set the precedent:
  pre-1.0.4 legendaries *"became worthless overnight."*
- **D2 runes — the upcycle ladder is deliberately non-viable at the top.** 33 ranks, El#1 (req lvl 11) → Zod#33
  (req lvl 69); official Arreat Summit ratios are **3:1 with no gem for ranks 1–9**, then 3:1 plus a gem, then
  **2:1 plus an escalating gem grade from Pul upward**
  ([Arreat Summit](https://classic.battle.net/diablo2exp/items/cube.shtml)). Compounding, one Zod from El costs
  **14,281,868,906,496 El runes** (player-compiled) — **a floor-raiser for low ranks only, never a path to the
  top.** Gems follow 3→1 across five grades (**81 chipped → 1 perfect**).
- **D2 base restrictions made plain bases a currency**: a runeword requires the **exact** socket count, a
  non-magical base, and the correct order ([Fandom](https://diablo.fandom.com/wiki/Rune_Words)). *This repo
  deliberately rejected exact-count matching* (D41 unordered, `minSockets` as a floor) for top-up-economy
  reasons — prior art exists on both sides and the repo's choice is already recorded.
- **D3 legendary gems** give a clean tier-upgrade probability ladder keyed on (GR tier − gem rank):
  **+10 = 100%, +9 = 90%, … 0 = 60%, −1 = 30%, −2 = 15%, −3 = 8%, −5 = 2%, −16 or lower = 0%**, with 3 base
  attempts per rift (+1 for no death, +1 for empowering) ([maxroll](https://maxroll.gg/d3/resources/legendary-gem-mechanics)).

**The cross-genre takeaway the sources support:** every system that survived keeps **two knobs** — a
tier/quality axis naming discrete legible steps, and a scarcity axis controlling count or gate. D4 collapsed
them into one number and had to undo it. This repo already has them separate, in shipped code.

---

## The shape

**Tier-System is not a new system, and proposing one would be the defect.** Every layer already owns a
ladder, the owner has already ruled those vocabularies stay distinct, and four programs already own the
layers. What no program owns is **the edges between them**, the **propagation contract**, and the
**consistency of the chain**. So the shape is three things, in this order.

### 1. The propagation contract — declare ladders in tuning, prove it with a guard

The measurement is unambiguous: a ladder declared in **tuning** costs **6 sites** to extend; a ladder
declared as a **C# enum** costs **≥22 files**. The difference is the declaration site, not the ladder.

- **Rule T-1: a new tier ladder is declared once, in `data/tuning/`, and every consumer reads it.**
  `droptablegen/tuning.py:215-219 load_rarity_ids` is the in-repo pattern that already does this.
- **Rule T-2: no code may restate a ladder's ids.** Three duplicates exist and should be deleted rather
  than copied — `Dungeon/Tuning/EncounterTuning.cs:42`, and the two independent Python `RARITY` tuples in
  `creatures/anchor/schema.py` and `structures/anchor/schema.py`.
- **Rule T-3: a derived count is derived, never mirrored.** `CreatureRarityLadder.RungCount` should come
  from `Enum.GetValues`, and a guard should assert the identity — the exact assertion whose absence makes
  today's enum-declared ladder unsafe, since `OneRungAbove` throws if the const drifts.
- **Rule T-4: coverage is guarded, not remembered.** Generalise `RarityTuningCoverageTests.cs:29-43` — which
  already asserts every rarity-keyed tuning map carries every rung — to each ladder, plus the TS union and
  the Python tuples. This guards a **closed vocabulary**, which `validation-ssot.md` §1 explicitly permits;
  it must never assert a corpus size.
- **Rule T-5: a tier reaches a magnitude only as an additive `thetaOffset` into `Θ`, from a table, with
  `P(Θ)` applied once downstream** — and it owes a `ssot-power-scale.md` §10 row. Never a curve, never a
  second scaling.

This is a **modest refactor of ~6–8 sites**, and it is the prerequisite that makes everything below cheap
instead of compounding.

### 2. The six missing edges, each small and each already carried

| # | Edge | What exists | What is genuinely new |
|---|---|---|---|
| E1 | **creature → drop table** | `LootSourceRow(SourceKind, SourceId, TableId, ContentLevel)` is already the generic carrier; `decisions.md:131` already puts `KillerActorKey`/`killerPtr` on the `die` occurrence | A ninth `source_kind` and its authored tables. Vocabulary + content, not architecture |
| E2 | **drop → material shelf** | `DropEntryKind.Material` is drawable and the grant is already emitted | One arm in `LootMintAt.Mint` and one credit call in `PersistLootUnlocked` |
| E3 | **species tier → which material** | `ExpeditionResolver.cs:134` already reads `ElementPrimary` | Two steps, in order. **E3a (no vocabulary change):** read the species' own **rung**, replacing the hardcoded `"shard.chaff"`/`"shard.cultivated"` at `:172-173` — MH re-tiering with ids that already exist. **E3b (D2):** the three-layer general/family/species-unique model, which *is* the ask-first vocabulary change |
| E4 | **material grade → socket** | grade is already computed from ilvl (`MaterialTuning.cs:122`); bore recipes already name a grade by hand | Derive rather than author it. Deliberately separate today per `decisions.md:133`, so this is an owner decision, not a bug |
| E5 | **item → item** | the creature side already ships `CreatureRecipeDef(Output, InputA, InputB)` | An item-typed cost line, a consume-and-replace `op_kind`, and a successor edge — the one place the schema genuinely cannot express the request. **D3: in scope, but graduates to its own spec** rather than riding this program |
| E6 | **rung → rung (promotion)** | 10 recipes authored and priced; `PromoteFrom` declares "all ten promote" | An `Elevate` executor and an 11th `MutationOpKind`. Until then, the ladder has no climb |

**Sequencing follows the evidence, cheapest-unknown-first — but the species-magnitude container corpus
(§3) comes before all of it**, because until that lands the species tier ladder reaches no gameplay and
nothing built on it is observable. Then: E2 (one arm) → E3 (one read) → E1 (one source kind + content) →
E6 (the climb) → E4/E5 (owner decisions first).

### 3. The consistency pass — four measured defects, fixed before new content

Reconcile the theme registry's 84 retired rarity ids; reconcile the 783-vs-19 family vocabularies (or
declare the registry the SSOT and re-derive); repoint the four FE sites still keyed on the retired
four-value ladder (defect 4 is player-visible — the roster sort is silently inert today); and **fill the
threatBand corpus**, because 730 of 904 species sitting on one default rung means the tier ladder that works
best is the one carrying the least content. Nothing downstream is worth building on a chain with these in it.

**And one content gap outranks all six edges on value-per-effort:** ship the first
`trait.species-magnitude-*` containers. The call site is already live and already correct on every deploy
(`RpgStore.UniqueActors.cs:1602-1607`); it fails closed only because the corpus is empty. Until it lands,
the entire `threatBand → Θ → P(Θ)` bake — the one propagation path this whole design rests on — reaches no
gameplay at all, and no tier work downstream is observable.

**Alternatives rejected, with reasons:**

- **One unified tier enum across species/item/material/socket.** Rejected — the owner ruled it lazy design
  2026-09-12, and the evidence agrees: the ladders already mean different things (`threatBand` feeds `Θ`,
  rarity feeds count and window, grade gates content, star gates investment). Collapsing them is D4's Item
  Power, and D4 undid it.
- **A `tierPower` multiplier so higher tiers hit harder.** Rejected by PS-3 and by `ssot-rarity.md` §3.6's
  standing ban on `CurveInput.Rarity` for items — *"a multiplier on the rung makes rarity dominant and
  destroys the overlap."*
- **A per-species material id for all 904 species.** Rejected as the default: `MaterialCatalog.ClassOf`
  throws outside the closed set by design, MH's own itemData sprawl (IDs 0–2315) is the documented failure
  mode, and the vision forbids a fourth wallet. The **bounded** version — a trophy class keyed on
  `(family, rung)` rather than on species — is the open question below, and MH's `Monster Solidbone` proves
  the generic layer must survive beside it either way.
- **Exact-socket-count matching for combinations (D2 runeword shape).** Rejected — already settled as D41
  unordered with `minSockets` as a floor, for top-up-economy reasons.
- **Making rarity promotion the tier climb on its own.** Insufficient: promotion is in-place
  (`outputKind: mutation`, no `outputRef`), so it can never change what an item *is*. It is E6, not E5.

---

## Tunables

Every number this would introduce, and which versioned file owns it. Nothing here is a `const`.

| Number | Meaning | Owner |
|---|---|---|
| Per-rung `thetaOffset` for any new tier ladder | The one sanctioned tier→magnitude path; additive into `Θ`, `P(Θ)` applied once | `data/tuning/<ladder>.v{n}.json` + a `ssot-power-scale.md` §10 row |
| Species-tier → material grade/rung map | Which grade a creature of rung *n* yields (MH's `Scale`→`Scale+`→`Shard`) | New `data/tuning/creature-yield.v1.json` |
| **Species-unique trophies per species (1–2)** | D2's thin top layer; settable to 1, or to 0 for general creatures — the dial that bounds the id count against MH's own sprawl | Same `creature-yield.v{n}.json` |
| **General/family material generation targets** (`countPerFamily`, `countGeneral` — whole ids) | How much volume the two lower layers carry, which is what makes a 1–2 species layer sufficient | `data/tuning/creature-yield.v{n}.json` (**not** the seedsmith mirror — Core never reads a file, T7.2) |
| **Borrowed-art flag per species (D1)** | Whether a species wears another PvZ type's art, and which. Explicit and load-rejecting when absent (T5), never inferred | Anchor schema + `data/tuning/` — a **flag**, not a number; listed here because it must not become a silent default |
| Per-species-kind drop weights (`weightPerMillion`) and rare-gate rates (`gateRatePerMillion`) | E1's tables; the "1 generic + 3 commons + 1 rare gate" shape | `data/seed/loot/**` through the existing `droptablegen`; unit matches the shipped `drop-rate-floor.v1.json` |
| ~~`MinDropRatePerMillion`-style floor~~ | ⚠ **Corrected 2026-09-13 — this already ships.** `data/tuning/drop-rate-floor.v1.json` (`minRatePerMillion: 1`), read by `Items/Drops/DropRateFloor.cs`. Consume it; do not re-introduce it | *(shipped)* |
| Deterministic-exchange price — `exchangePriceSouls` + `exchangeTokensPerGrant` (whole units) | Variance insurance priced at ≈ the EV of the random path (PoE: 1/1500 vs flat 1500). **If it is derived from `P(Θ)`/`contentScale` rather than flat, it is a cost ladder and owes its own §10 row** | `data/tuning/materials.v{n}.json` `operations` (the shipped ten-verb cost table) |
| Promotion cost curve per rung (`promoteCostSoulsMilli`) | E6's climb; a configurable soft cap, never a hard stop. **It is a cost ladder, so it owes its own `ssot-power-scale.md` §10 row** — §10 rows 6, 26, 27, 31 and 33 are the precedent, and row 18 shows an authored per-rung table still earns one | `item-rarity.v1.json` beside `enhanceCapMilli` |
| Gem tier ids + upcycle ratio | Closing the 118/232 unsatisfiable-slot gap; `upcycleInputPerOutput` already exists unread | `data/tuning/sockets.v{n}.json` |
| Socket structural ceiling 4 → 8 and the doubled role table | Already decided (`decisions.md:133`), not yet applied; structural, commented, PS-8-exempt | New `sockets.v{n}.json` revision — never an edit of `v1` |

**Structural (stays `const`, with a comment saying why):** `SocketCircuitSize = 4`, `PowerVector.One = 1000`,
recursion and termination guards, per-frame caps.

---

## What this deliberately does not decide

- **Exact tier counts, rung edges, drop percentages or craft quantities** — balance data, owed to a spec.
- **The exact per-species trophy count, and whether the species layer covers all 904 or only unique
  species** — D2 settles the *model*; the number is a tuning value and both readings ride the same machinery.
- **How the 783 anchor family labels reconcile to a canonical set** — the one thing still open, and a
  content decision with real cost either way.
- **What a bespoke modded PvZ actor costs in assets and injector work** — D1 puts it after the flagged
  borrowed-art fallback, explicitly budgeted, explicitly later.
- **Whether the eight-socket revision lands with this program or with `strain-splice-host`** — that ideal
  already owns the migration order; this doc only records that the decision is unapplied.
- **Durability's interaction with tiers** — `deployment-hierarchy` module 7 owns it and is review-gated.
- **Any FE surface** — a tier bench, a compendium or a material shelf UI belongs to `item-surfaces`
  (module 20) and `gui-lego`, and a player menu goes through `/idea-ui`, not here.
- **The `pvz-run` loot source** — reaching lawn kills is a separate, named, unbuilt task
  (`RpgStore.Souls.cs:20-27`), and standalone-first means the chain must work without it.

---

## Decisions — owner, 2026-09-13

Four were put to the owner and answered; two stood undisputed and are recorded as decisions rather than
left as questions, per this repo's own rule.

**D1 — Invented species are in scope, and they borrow PvZ art by explicit flag.** A species need not exist
in the almanac. Its **fallback** presentation is *lawn-wearing* — it wears an existing PvZ `GameTypeId` for
art and dumps — and **that borrowing is an explicit flagged field, never an inferred default.** Owner:
*"fallback lawn-wearing with explicit flag or parameter for it, so if we have time and money, we will design
new pvz engine mod plant/zombie — they need asset and real cost."* The art upgrade path (bespoke modded PvZ
actors) is therefore a **budgeted content investment, not an architecture change**, and the flag is what
keeps the borrowed state visible and replaceable. Consequences, stated so they are not discovered later:
- The flag obeys `tunables-ssot.md` T5 — **a missing value is a load rejection naming it, never a silent
  default.** "Which species are wearing someone else's costume" must be queryable, not archaeology.
- **Standalone-first is satisfied, not strained.** Invariant 9 forbids the injector *gating* a feature;
  here it only ever *enriches* (a bespoke actor replaces a borrowed costume). This decision does not take
  the dangerous direction.
- **This does not fix defect 3.** 730/904 species sit at the rung-4 default because 719 anchors lack a
  `threatBand` *classification*, not because the corpus is small — invented species add rows beside them.
  What they *do* fix is **top-rung thinness** and the fusion-recipe shortfall the creature program already
  hit (20 eligible `Almanac` outputs against only `C(4,2)=6` pairs at the rung below).
- **`WaveCatalog.Band` has no acquisition filter** (`:153-154`, already a pinned quirk), so an off-lawn
  species could march in a wave unless that filter is added.

**D2 — Trophy materials are species-keyed, in a three-layer model, with the per-species count tunable.**
The owner's reasoning is the load-bearing part and is recorded because it inverts the naive read:
*"Monster Hunter generates 10+ materials per large monster because the game only has 100+, so it is very
huge. We cover by general and family, and make unique species 1 or 2."*

| Layer | Who supplies it | Role |
|---|---|---|
| **General** | seedsmith generates more of them | The volume layer — MH's `Monster Solidbone`; the filler any creature can drop, and what keeps low-tier creatures permanently relevant |
| **Family** | seedsmith, keyed on the family registry | The "this kind of thing" layer — MH's per-monster-class commons |
| **Species-unique** | **1 or 2 per species, tunable** | The trophy — the rare gate on a craft step |

The ratio is the whole point: **MH affords ~10 per monster at ~100 monsters; at 904 species that ratio is
impossible**, so volume moves to the general and family layers and the species layer stays deliberately
thin. The per-species count is a **tunable, not a `const`** (owner: *"they must tunable"*) — which also
means the model need not commit now to whether the species layer covers all 904 or only unique species:
**that is a tuning value, and both readings are expressible with the same machinery.** Honest risk to carry
into `/spec`: even at 1–2, 904 species is ~900–1,800 trophy ids before general and family are counted — the
same order as MH's own `itemData` span (IDs 0–2315), which is the documented inventory-legibility failure.
The mitigation is that the count is a dial and can be set to 1, or to 0 for general creatures.
**→ DEFERRED** (§ DISPOSITION). D7 keyed the family layer on lineage; lineage was **disproven** 2026-09-13
(§ D7-AUDIT). Lineage-as-key is **withdrawn**; the family layer itself is **deferred** until species
selection ships, then re-derived against real play rather than against a graph.

**⚠ D2 owes three statements before `/spec` (invariant audit, 2026-09-13).** It is not wrong, but its
evidence is incomplete in ways that would ambush a reviewer:

1. **It clears the source-tagging ban — for a reason worth writing down.** `ssot-materials-crafting.md:161`
   forbids source-tagged ids because *"a source-tagged id would let PvZ gate something, violating SC8."*
   That is a **mode-gate** rule: an id keyed on *which client produced it* becomes unobtainable when that
   mode is closed. A species-keyed id is keyed on *what you hunted*, and a species is reachable from
   expedition, delve and later lawn — several modes, none required. **No mode gate, so D2 passes.**
2. **But it reverses two other refusals in the same table, and must say so.** `:160` refuses a **role**
   axis — *"Twelve roles × anything is the scavenger hunt"* — and D2 proposes a **species** axis at 904,
   two orders of magnitude past the twelve that were refused. `:162` refuses a **zone** axis because
   *"per-zone ids make every recipe a travel itinerary"* — and a trophy id makes a recipe a *hunting*
   itinerary, which is precisely the requested fantasy. Both are **deliberate reversals of sealed
   reasoning** (§3.4, §"Why 21 is the right number", §8.1 currency bloat), not oversights — and a reversal
   that is not named reads as a defect to the next reviewer.
3. **A trophy class is a *sixth spend class*, not a widening of the five.** `:86` — *"This is the table
   other lanes cite. **Names here are final**."* The doc currently calls this only "an ask-first vocabulary
   change"; it is an amendment to §3.1's closed five-class table and should be stated at that weight.

**And one foreseeable guard breakage:** `tools/seedsmith/.../materialgen/vocab.py:120` asserts
`len(ISSUABLE) == 27` — correct today, as a closed vocabulary mirroring `MaterialCatalog.Build()`. Under D2
the trophy ids grow with content, so that assertion silently becomes **a pin on a derived population**, the
exact shape `validation-ssot.md` forbids. Replace it with a reconciliation canary —
`len(ISSUABLE) == len(MaterialCatalog.All)` — in the same change.

**D3 — Both climbs ship: rung promotion now, item→item tree as its own spec.** E6 (an `Elevate` executor
plus an 11th `MutationOpKind`) unblocks 10 already-authored recipes. E5 (item-typed cost line,
consume-and-replace `op_kind`, successor edge) is a real schema change the item program deliberately does
not have, so it graduates to its own spec rather than riding this one. The creature side's
`CreatureRecipeDef(Output, InputA, InputB)` is the shape to copy.

**D4 — Block on the whole chain before new tier content.** All four consistency defects *and* the empty
`trait.species-magnitude-*` corpus are fixed first: 81% of species are mechanically identical on the ladder
this design keys on, the roster sort is already visibly dead to players, and the tier→magnitude bake
currently reaches no gameplay at all.

**D5 — A deterministic exchange ships *with* the first tier content** (undisputed). MH shipped the
rare-drop wall first and spent three titles walking it back — melder prints, then craftable decorations,
then all decorations craftable — and its own executive director finished the game without completing his
build. Priced as variance insurance at roughly the EV of the random path, per PoE's flat-1500 bench against
a 1/1500 roll.

**D6 — Gem grade, rarity and socket count stay separate axes** (undisputed; `decisions.md:133` already
locks it). Materials gate the **cost** of socket work, never its **capacity** — which honours "socket tiers
integrate with material tiers" without collapsing an axis split the repo already paid for.

**D7 — Two family axes, two closed vocabularies, two different jobs.** The "19-vs-783 drift" was a
misdiagnosis, corrected by measurement: these are **orthogonal concepts that happen to share a field name**,
not two versions of one thing. The registry's 19 ids are **PvZ lineage** (`cherry` → 樱桃炸弹 · 樱桃坚果 ·
樱桃射手 …, 45 native labels curated into 19 lines). The 783 anchor labels are **LLM-authored themes**
(`undead` 64, `artillery-flora` 17, `pyro-botanical` 8, with a long tail and visible sprawl —
`carnivorous flora` beside `artillery-flora`, `explosive-flora` beside `explosive-botanical`). The species
**file layout follows the themes, not lineage** — 547 distinct stems, **mean 1.65 species per group**, and
only `sunflower` overlaps the registry.

| Axis | Vocabulary | Source | Job |
|---|---|---|---|
| **Lineage** | the registry, extended | **Derived, not authored** — plant ancestry from the 1,295 captured fusion recipes (`_dump/recipes.json`, 705 distinct ids; `CreatureCorpusBuilder` already builds parent/child); the 227 zombies extend the registry's native-label map, whose `bucket`/`dolls`/`hypno` entries are already that shape | **The trophy-material family key** |
| **Theme** | the 783 labels, **closed** into a canonical archetype set | Normalized once, then future runs pick **from** the closed list | A second axis — drop-table variety, elemental leaning, set themes. **Never the material key** |

> ⛔ **D7 IS DISPROVEN AND REOPENED — adversarial audit, 2026-09-13.** The justification below rested on a
> claim that lineage families would average **"~48 species"**. **That number was never measured; it is
> `904 / 19 = 47.6`** — the hand-curated registry's entry count divided into the roster, presented as if it
> were a lineage measurement. A full computation over the real graph disproves it. The measured truth, and
> why it kills the decision as written, is in **§ D7-AUDIT** immediately below. The original reasoning is
> kept struck-through rather than deleted, because the *shape* of the argument (group size decides which
> axis can carry materials) survives even though its numbers did not.

~~Why lineage carries the materials: **group size**. 783 labels over 904 species averages 1.15 species per
group, which collapses the middle layer straight back into per-species trophies and defeats D2's whole
point; 19-or-so lineages over 904 averages ~48. It is also MH's own concept — Rathalos and Rathian share
ancestry, not a theme — and it is **derived from shipped data with zero model calls, so it cannot sprawl.**~~

Two consequences to carry into `/spec`, stated rather than discovered:
- **Closing the theme vocabulary is mandatory, not optional.** Making a theme a key makes generated text
  load-bearing, which the repo's guardrail rule is right to distrust. Once closed it is an ordinary closed
  enum and pinning its count is correct (`validation-ssot.md` §1). Precedent for the bridge:
  `CreatureTraitPoolCuration` already maps anchor flavour onto a closed gameplay vocabulary.
- **Two axes is more surface, and that is deliberate.** It is also consistent with this repo's binding
  principle that *each axis owns its own closed vocabulary* — ancestry and theme are genuinely different
  questions, so collapsing them would be the defect, not the saving.

### § D7-AUDIT — what the lineage graph actually contains

Computed 2026-09-13 over `data/seed/creatures/_dump/recipes.json` + the species corpus, matching
`CreatureCorpusBuilder`'s own semantics (plant-side ids only). **All counts are readings of today's corpus.**

| Measure | Reading |
|---|---|
| Recipes resolving to a real plant species | **1,005** of 1,295 (290 dropped; 117 distinct result ids have no species anchor) |
| Plant species **in** the graph | **571** of 677 — **106 plants are isolated** (15.7%) |
| Roots (no parents) | **61** — Peashooter, SunFlower, CherryBomb, WallNut, PotatoMine, … plus ~20 `Gold*`/`Big*`/`Huge*` that are roots only because their own recipes were never captured |
| Families, nearest-root rule | **61 · mean 9.36 · median 5.0** over the 571 reachable |
| Families with ≤2 members | **19–23** depending on rule; **11–21 singletons** |
| Largest family | Peashooter **59–81** (10.3–14.2% of all reachable plants); top 3 take **23–28%** |
| **Roster with NO derivable lineage** | **333 / 904 = 36.8%** — all 227 zombies (empty *by construction*) plus 106 isolated plants |
| Lineage key across the **whole** roster | **394 groups, mean 2.29** — the same order as the alternative it was meant to beat |
| The theme alternative, re-measured | mean **1.52** members per label (not the 1.15 this doc previously claimed) |

**And the graph is not a tree — it is barely even a DAG.**
- **Depth 0: 61 · depth 1: 502 · depth 2: 8.** Almost every fused plant is a *direct* child of two roots;
  there is no deep ancestry to inherit from.
- **459 of 571 (80.4%) descend from more than one root.** Parent counts per non-root: 420 have 2, 16 have
  3, 37 have 4, 17 have 6, 7 have 8, and one has **15**.
- **379 nodes have an ambiguous *nearest* root** (ties at equal distance). The tie-break used was lowest
  `gameTypeId`, which is arbitrary — and it is why Peashooter (`id 0`) acts as a magnet.
- **45 back-edges, 7 self-parent nodes, 41 mutual parent pairs** (A fuses into B *and* B fuses into A).
  Ancestry is not well-founded; closure needs an explicit visited-stack to terminate.
- Two reasonable assignment rules **disagree on 12.8% of species** — so a species' family would be a
  property of the tie-break, not of the game.

**Four independent reasons the decision fails as written:** it covers only 63% of the roster and **zero
zombies**; its tail (19–23 families ≤2 members) is exactly as useless as the rejected alternative; its head
is a monolith (one "Pea material" most players would ever see); and the assignment is not canonical — a
player would see `CherryNut` filed under *WallNut* lineage when it is visibly half cherry.

**The one shape the data does support** is **multi-membership**: a species belongs to *every* ancestral root
family. Measured: **61 families, mean 19.0, median 16, min 2, max 81, avg 2.03 families per species — no
singletons and no tie-break**, and it matches the fiction, because a fusion genuinely *is* both parents.
It does not fix the 333 species with no lineage, and it makes a trophy drop a **set rather than a scalar**,
which is a real change to the crafting model.

**D7 is therefore reopened.** The replacement decision is the owner's; the options are recorded in
§ DISPOSITION above — lineage-as-key is **withdrawn**, the family layer **deferred**.

**D8 — Fill `threatBand` deterministically first, spend model calls only on the residue.** creature-seed
modules **3 `power-parse`** and **4 `threat-band`** already exist for exactly this — a deterministic parse
of the captured stat text mapped through the tuning table's rung thresholds, designed to *"cover most of
the roster with no model call."* Run that across the 719 unresolved anchors first; classify only what it
cannot resolve. This fixes the root cause rather than the symptom, and it is the cheapest path to closing
defect 3.

⚠ **Consequence worth acting on in the same change:** `SpeciesExpander.cs:31-33` deliberately **excludes**
`threatBand` from the unresolved-skip list, so a null band silently takes the rung-4 default instead of
refusing. That silence is what let 730/904 accumulate unnoticed. Once real coverage exists, that default
should become a refusal — otherwise the next classification gap hides exactly the same way.

### Deferred — the family key, if the layer ever returns

**Not an open question and not a blocker** (§ DISPOSITION): lineage-as-key is **withdrawn** and the family
layer is **deferred** behind species selection. These candidates are kept only so a future pass does not
re-derive them from scratch — and the strongest argument against all of them is that the layer existed to
support a species layer that cannot ship yet.

| Option | Coverage | Group size | Cost |
|---|---|---|---|
| **Curated registry, extended + multi-membership** | **All 904** — the registry already carries zombie lines (`bucket` → 铁桶僵尸) | human-chosen, no tie-break | A bounded curation pass, 19 → ~40-80 ids. The lineage graph *proposes* memberships; humans own the vocabulary |
| **Lineage, multi-membership only** | 63% — still zero zombies | 61 families, mean 19.0, no singletons | Free (derived), but needs a second key for 333 species and makes a drop a set |
| **Key on a universal axis** (element / aptitude) | All 904 by construction | element ≈150/group (6 groups); aptitude ≈75/group (12) | Near-free — but element is *already* the 6 shipped `essence.*` ids, so it adds no new fantasy |
| **Drop the middle layer** (D2 general + species-unique only) | n/a | n/a | Zero — but reintroduces exactly the sprawl D2's arithmetic was avoiding |

**Trigger to revisit:** species selection ships (DO #1) and the layer is judged against real play, not
against a graph.

---

## Reading gate (this session, per DESIGN-GATE §1)

**Product vision:** [the-game.md](../guide/the-game.md), [the-loops.md](../guide/the-loops.md) — genre
(RPG + empire building), three stocks, loops named above. **Anything at all:**
[software-architecture.md](software-architecture.md), [decisions.md](decisions.md) (Product vision;
Set topology classes 2026-09-10; Eight-socket topology 2026-09-10; Battle death attribution 2026-09-08),
DESIGN-GATE §2 invariant 15 (SOLID — this doc contributes edges to existing gates and forks no parallel
path), session-boundary policy. **Item rarity / the ten-rung ladder / creature rarity:**
[item/ssot-rarity.md](item/ssot-rarity.md) (read in full — §3.2 axis split, §3.3 ladder, §3.6 what rarity is
not, §3.7 promotion, §3.8 pity, §4.3 the creature reversal, §4.4 the `rarity_budget` registry, §8 failure
modes). **Caps / power / magnitudes:** [power/ssot-power-scale.md](power/ssot-power-scale.md) §4.6 PS-3,
§5.3 the `thetaOffset` rule, §10 the closed 33-row inventory, §11 PS-8. **Tunables:**
[tunables-ssot.md](tunables-ssot.md) T1–T8. **Economy / materials:**
[item/ssot-materials-crafting.md](item/ssot-materials-crafting.md) (read in full — the five spends, the 21→27
id vocabulary, §3.4's hard rule against source-tagged ids, §5 salvage, §5.3 the grade lock, §8 failure modes).
**Drop tables:** [drop-tables-ideal.md](drop-tables-ideal.md) + [drop-tables-map.md](drop-tables-map.md)
(4 modules, D1–D3). **Creature generation:** [creature-seed-map.md](creature-seed-map.md) (18 modules),
[creature-seed/spec-species-rank.md](creature-seed/spec-species-rank.md). **Item program:**
[item-map.md](item-map.md) (25 modules, X1–X7). **Crafting direction:**
[../ideas/crafting-coverage-engine.md](../ideas/crafting-coverage-engine.md) (four axes that must not
collapse). **Sibling tier ideals:** [gameplay-tiers-ideal.md](gameplay-tiers-ideal.md) (the parent survey and
its binding §93 principle), [strain-splice-host-ideal.md](strain-splice-host-ideal.md),
[action-skill-tiers-ideal.md](action-skill-tiers-ideal.md). **Validation:**
[validation-ssot.md](validation-ssot.md) (closed vocabulary vs derived population).
**Code verified at the `file:line` cites above**, not from comments — and the surveys corrected three stale
doc claims in the process (`ItemWorkbench.cs:105`'s "no `item_base_type` table" against
`RpgStore.BaseTypes.cs:21`, and `creature-mechanism-gaps-ideal.md:337`'s zero-consumer claim). ⛔ **A third
"correction" is itself struck: `crafting-coverage-engine.md`'s "no forge executor" note was RIGHT.**
`MaterialRecipeCatalog` only prices and validates forge (`:284`); there is no `Forge()` method and no POST
route. See A11. **One
claim in an earlier draft of *this* document was falsified during the same pass and corrected here rather
than left standing:** the `threatBand → Θ → P(Θ)` path was first written up as a working automatic
propagation path; a full consumer trace showed it is build-time only and that its terminal binding
dead-ends on an empty container corpus. The correction is carried in Built, Wiring gap, §3 of The shape,
and decision D4.
Counts verified by counting; populations treated as readings. Prior art web-searched this session with
sources inline and unverified figures flagged.

**Boundary honesty:** `.\scripts\session-boundary-check.ps1` was run this session — 9 records, 4 active,
13 drift overlaps **between other sessions**, none claiming `docs/architecture/tier-system-*` or any path
this doc writes. This session wrote no `tasks/sessions/*.json` record (no `/session-start` tool in this
harness), so **the DESIGN-GATE §5 boundary box cannot be ticked** until the owner records one. The only
path written is this file.

**§5 checklist:** subsystems identified ✓ · boundary record ⛔ **untickable** (above) · gate docs read this
session ✓ · `decisions.md` checked ✓ · claims cite `file:line` ✓ · verified against code, not comments ✓ ·
surrounding sections read ✓ · constraints measured rather than assumed ✓ (propagation cost, θ distribution,
ingredient satisfiability, registry drift all counted) · §2 invariants hold ✓ · corrections propagated —
⚠ **partial**: the three stale doc claims above are named here but **not yet fixed in their own files** ·
no population count pinned ✓ (every corpus size labelled a reading) · **ActorHub — ⚠ CORRECTED
2026-09-13: an earlier draft ticked this "N/A", which was false.** This feature's own top-ranked
deliverable — the `trait.species-magnitude-*` corpus — is consumed at `RpgStore.UniqueActors.cs:1618-1621`
via `ProduceAndBind(container, …)` under source `"creature-magnitude"` (`:1541`), and bound derived atoms
reach the composer through `AtomDerivedSubsystem`, registered on the Hub at `Stats/Derived/ActorHub.cs:168`
(beside `StarLoyaltySubsystem` at `:158`). So the whole `threatBand → Θ → P(Θ) → magnitudes` chain
terminates in **actor derived magnitudes composed by ActorHub**. The correct tick is **"contributes via
ActorHub as a registered atom reader"** — which is the sanctioned path, with no private fold — and the doc
therefore **owes a GG-49 `ContributionSourceIds` grammar id for `"creature-magnitude"`** · no
SOLID-violating parallel path ✓ (every edge contributes to an existing gate; nothing forks a second
composer, ladder or pipeline).
