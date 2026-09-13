# Species-gear chain — capability map

**Status:** capability map, 2026-09-13. **19 modules specced; audited and corrected 2026-09-13.**
Awaiting owner approval. No build authorized.

⚠ **Read § Corrections before the module table.** A five-agent audit found ~35 defects across the
map and the specs; the module rows, build order and exclusion lists below are the **post-audit**
versions.

**Initiative id:** `species-gear-chain`
**Module spec path:** `docs/architecture/species-gear-chain/spec-<module-id>.md`
**Plan / tasks:** `tasks/species-gear-chain-plan.md` / `tasks/species-gear-chain-todo.md`

---

## Why this initiative exists, and why it is not called `tier-system`

[tier-system-ideal.md](tier-system-ideal.md) § DISPOSITION **withdrew `tier-system` as a program**:
`gameplay-tiers-ideal.md:156` already closed the graduation list — *"never by reopening sealed
ideals"* — and the surviving work belongs to programs that already exist. That withdrawal stands.

What the audit found instead (§ AUDIT A10, the reframe) is a **single dependency spine** that four
separate ideal docs each describe one segment of:

> **A player must be able to reach a species → the species must yield something → that something must
> feed a craft that climbs.**

Today the spine is severed at the first link: **a player meets 10 of 904 creatures.** Everything
species-keyed downstream — 844 species-themed set entries, species materials, species-shaped craft
costs — is decoration until that is fixed.

This map is therefore **an index over four approved ideals, not a new program**. Every module below
names the **existing program that owns it**; the module id is the stable handle downstream plans and
`/build` select by. If the owner prefers these specs filed directly under the owning programs'
directories instead of a shared one, that is a one-line change to this map and to no module content.

**Source ideals, all four owner-reviewed 2026-09-13:**

| Ideal | Covers |
|---|---|
| [tier-system-ideal.md](tier-system-ideal.md) | The propagation contract (T-1…T-5), the six edges E1–E6, the consistency pass, D1–D8 |
| [species-selection-ideal.md](species-selection-ideal.md) | The three moves that make species reachable |
| [species-craft-ideal.md](species-craft-ideal.md) | The species binding at the bench, species materials, socket caps by item kind |
| [gear-climb-ideal.md](gear-climb-ideal.md) | Promotion (E6), the risk ladder, item→item upgrade (E5) |

---

## Modules

| Module id | Responsibility | Depends on | Owning program | From |
|---|---|---|---|---|
| `tier-propagation-contract` | T-1…T-5: a ladder is declared once in `data/tuning/`; no code restates its ids; derived counts are derived (`CreatureRarityLadder.RungCount`); coverage is guarded per ladder; a tier reaches a magnitude only as an additive `thetaOffset` owing a `ssot-power-scale.md` §10 row | — | `power` + `creature-seed` | tier §The shape 1 |
| `ladder-consistency-repair` | The four measured defects: 84 retired rarity ids in `themes.v1.json`; the FE roster sort dead at 4+ sites; the two duplicate Python `RARITY` tuples; `EncounterTuning.cs:42` | `tier-propagation-contract` | `seedsmith` (`theme-refresh`) + `fe-essentials` | tier §The shape 3, DO #4 |
| `threat-band-fill` | D8: write back the `threatBand` the existing module already scores for all 719 unresolved anchors; ship the `SpeciesExpander.cs:31-33` refusal in the same commit | — | `creature-seed` module 4 | tier DO #2 |
| `species-magnitude-synth` | A7: synthesize `trait.species-magnitude-*` containers in `ImportCreatureSpecies` from values already in SQLite — turns a 904-row content program into an importer change | `threat-band-fill` (it rewrites every magnitude) | `creature-seed` | tier DO #3, §The shape 3 |
| `wave-species-roll` | Replace `pool[i % pool.Count]` with a seeded weighted slot table; widen `Band` past four rungs; add the missing acquisition filter | — | `creature-lawn-deploy` | selection move 1 |
| `wild-species-spawn` | Replace the `"normalzombie"` literal in `SpawnTheUnmade` with a sector/climate-weighted roll; per-flag wild admission (`Summonable` admit, `CaptureOnly` **admit**, `EventOnly` refuse) | — | `loam` / `world-map-runtime` | selection move 3 |
| `delve-species-wiring` | Give `Encounter`/`SlotFill` a production caller; fix `SlotFilter.cs:49`'s missing `+50,000` plant offset in the same change | `threat-band-fill` (its null-`ThreatBand` refusal is correct) | `party-dungeon` | selection move 2 |
| `creature-drop-tables` | E1 a ninth `source_kind` + authored tables; E2 one arm in `LootMintAt.Mint` + one credit call; E3a read the species' own rung instead of the hardcoded `shard.chaff`/`shard.cultivated` | `species-magnitude-synth`, and at least one of `wave-species-roll` / `wild-species-spawn` / `delve-species-wiring` | `drop-tables` | tier §The shape 2, E1–E3a |
| `set-species-binding` | A real `speciesId` + `setClass` field on the set entry; `set-charm-gen` emits it forward; a **deterministic** repair pass extracts it backward from `themeKey`; refuse-and-report the ~40 non-`creature.*` keys rather than guessing | — | `item` module 13 | craft slice 1 |
| `species-cost-shaping` | `speciesCostMultiplierMilli` keyed on the declared species' rarity rung, **gated above a tunable threshold rung**; upgrade level and set membership stay orthogonal | `set-species-binding` | `item` | craft slice 2 |
| `socket-allowance-by-kind` | `socketAllowanceByKind` table beneath the existing 15-row per-role ceiling: ordinary = base max, set piece **reduced**, unique/boss **increased** — bounded above by `structuralCeiling: 4` until the eight-socket topology lands | — | `item` (sockets) | craft §Socket caps |
| `species-materials` | D2: the general / species-unique material layers (**family layer withdrawn**); widen the closed 27-id vocabulary as a reviewed change; `CostClassMatrix.Allows` admits a sixth class; new `data/tuning/creature-yield.v1.json` | `species-cost-shaping`, `creature-drop-tables` | `item` + `creature-seed` | craft slice 3, tier E3b |
| `craft-risk-ladder` | Crafting potential as a new per-instance column, **derived with an explicit authored override** (never a sentinel); exhaustion becomes a new durability decay source; enhancement's Safe/Risk bands fold in — one risk vocabulary | — (but carries two **cross-program asks**, below) | `item` module 15 + `deployment-hierarchy` module 7 | gear §Failure shape |
| `rarity-promotion` | E6: one reviewed `op_kind` amendment, one `MutationOpKind` member, one executor + POST on the five-verb `ItemWorkbench` pattern, and the `promoted_from_ordinal` mark on the card | `craft-risk-ladder`, `tier-propagation-contract` | `item` | gear §The shape 1 |
| `item-upgrade-tree` | E5: an item-typed cost line, a consume-and-replace `output_kind`, a successor edge on the **armour** class ladder; no reroll; affix-pool legality + the implicit swap presented before consuming; ⛔ **blocked on `requirement-profiles` (item module 23), UNBUILT** | `rarity-promotion`, `craft-risk-ladder` | `item` | gear §The shape 2 |
| ⭐ `gem-tier` | ⛔ **A WIRING gap, not a content gap — the ideal's own fix was illegal.** A gem's tier is already derived in production (`GemContainerBuild.cs:49` → `UniqueBudget.TierOfPowerBand`); the socket path just **hardcodes `1`** at three sites. Authoring a tier on a gem entry is an explicit **OwnershipViolation** (`entry-shapes.md:81`), so the ideal's *"gem tier ids in tuning"* row cannot ship. Also wires the unread `upcycleInputPerOutput` ladder | — | `item` module 16 | tier §Wiring gap |
| ⭐ `socket-combat-wiring` | Make a socketed insert actually reach combat. **Today sockets reach no combat at all** — every production reader is card/surface/workbench; nothing in `Battle/`, equip runtime or the injector reads a socket. Must contribute via **ActorHub**, never a second composer | `gem-tier` | `item` module 16 | tier §Wiring gap |
| ⭐ `enhance-track-wiring` | Make the authored per-item `enhanceTrack` milestone atoms append at the right enhance levels. **All 1,178 base types carry one; `grep enhanceTrack src/` returns zero hits**, and `ItemWorkbench` passes `Array.Empty<AtomAppend>()` | — | `item` module 15 | tier §Wiring gap |
| ⭐ `craft-executor-completion` | Executors for the remaining priced-but-inert craft verbs (`Forge`, `ForgeGem`, `RerollOne`, `RerollAll` — **`Elevate` is `rarity-promotion`'s**), plus the inverse defect: verbs that are routeable with **zero authored recipes** and so always refuse `material.recipe-unknown` | ⚠ **Corrected — nothing.** The module's own spec header overrides this row: *"Depends on: nothing that blocks it. **Runs beside `rarity-promotion`, not after it**"* — it needs zero enum members and can land in Phase 1, while `rarity-promotion` sits behind an external, two-deep closed-enum queue. **Expect this module to land first** | `item` module 14 + 16 | tier §Wiring gap |

**No cycles**, verified by building the declared graph and the specs' own implied graph and diffing
them. The two shared-tuning-file cases are ordering constraints, not cycles.

---

## Build order

⚠ **Corrected after the dependency audit.** Three edges were wrong or missing; the changes are
marked. The old order had `set-species-binding` in Layer 0, a layer **ahead of** the module that
rewrites the registry it reads.

```
Layer 0 (no dependencies — all EIGHT can run in parallel)
  tier-propagation-contract · threat-band-fill · wave-species-roll · wild-species-spawn¹
  socket-allowance-by-kind  · craft-risk-ladder (STAGE 1 ONLY²)
  gem-tier                  · enhance-track-wiring

Layer 1
  ladder-consistency-repair   ← tier-propagation-contract      [⛔ gated on the themes.v2 decision]
  species-magnitude-synth     ← threat-band-fill
  delve-species-wiring        ← threat-band-fill
  socket-combat-wiring        ← gem-tier

Layer 2
  set-species-binding         ← ladder-consistency-repair       [⭐ NEW EDGE — see Corrections #13]
  creature-drop-tables        ← species-magnitude-synth + any one selection module

Layer 3
  species-cost-shaping        ← set-species-binding

Layer 4
  species-materials           ← species-cost-shaping, creature-drop-tables

OUTSIDE the layered graph — blocked on external UNBUILT work, not on layer position
  craft-risk-ladder 2–4       ← deployment-hierarchy module 7 (durability)      UNBUILT
  rarity-promotion            ← craft-risk-ladder + item-side rung arithmetic   (now its own deliverable)
  item-upgrade-tree           ← rarity-promotion, craft-risk-ladder,
                                item module 23 requirement-profiles             UNBUILT

  craft-executor-completion  ⚠ NOT dependent on rarity-promotion — moved into Layer 0 by the
                              module's own spec correction (needs zero enum members; expect it to
                              land first). See the module table row above.
```

¹ moves to Layer 2 behind `species-magnitude-synth` if its Open question 2 resolves to species
magnitudes rather than the flat `UnmadeMemberHp`.
² the split `craft-risk-ladder`'s own Open question 4 recommends — **and which its § Caps section now
forbids shipping alone**, because Stage 1 without Stage 2 is a hard stop on a `long` magnitude.

**Recommended first three, and why** — the ideals argue this order and the evidence supports it:

1. **`threat-band-fill`** — 730 of 904 species sit on one default rung, so the ladder carrying the
   most weight carries the least content. It is free (the scoring already ran) and it gates two
   other modules.
2. **`wave-species-roll` + `wild-species-spawn`** — selection open question 1 recommends these first:
   neither needs `threatBand`, both are cheap, and they prove the slot-table shape before the Delve
   inherits it.
3. **`species-magnitude-synth`** — until it lands, the `threatBand → Θ → P(Θ)` bake reaches no
   gameplay and nothing downstream is observable.

`species-materials` is last on purpose. Its deferral was **sequencing only**: MH's documented fix for
species-bound gear whose species is unreachable was to *make the species farmable*, never to invent a
substitute.

---

## Cross-program asks this map owes (filed nowhere today)

These are not module work; they are amendments against programs that own locked design. They must be
**filed in the owning program's map**, the way `deployment-hierarchy-map.md:89` filed its own.

| Ask | Against | Note |
|---|---|---|
| `Elevate` `op_kind` row in `ssot-enhancement.md` §5.3's reserved table | `item` module 15 | `Repair` is already queued ahead of it for the same closed enum |
| A new per-instance `crafting potential` column beside durability's `(max, current)` | `deployment-hierarchy` module 7 | Module 7 is **owner-locked D1–D6** and written against shipped code, not idea-phase |
| Potential exhaustion as a **new decay source** | `deployment-hierarchy` module 7 | D1 (*"never destroyed by wear alone"*) holds unamended — decay drives to zero, only **repair** may destroy |
| Superseding `spec-enhance-reroll.md` §4's Safe/Risk bands | `item` module 15 | Reopens written design; one risk vocabulary, not two |
| Widening the closed 27-id material vocabulary + a sixth `CostClassMatrix` class | `item` (`ssot-materials-crafting.md` §3.1/§3.4) | A reviewed change, explicitly not a tunable |
| A ninth `source_kind` on `LootSourceRow` | `drop-tables` | Vocabulary + content, not architecture |
| A `ssot-power-scale.md` §10 row per new cost ladder (`promoteCostSoulsMilli`, `upgradeCostSoulsMilli`, any `thetaOffset` table) | `power` | Rows 6/26/27/31/33 are precedent; row 18 shows an authored per-rung table still earns one |
| A `ContributionSourceIds` (GG-49) id for the species-magnitude corpus | `actor-hub` | It reaches the Hub via `AtomDerivedSubsystem` as a registered atom reader — it contributes, it does not fold privately |
| A third creature category (**neutral, never a legion troop**) in `creature-system-map.md`'s Vocabulary | `creature-system` | Today a creature is general **or** unique; a permanently non-recruitable neutral is neither |
| ~~A second contested slot: the sixth `MaterialClass`~~ | — | ⛔ **Struck — a misreading, found during the `/plan` audit 2026-09-13.** `deployment-hierarchy-map.md:89` says *"shard-leg material class **at high rungs**"* — reusing the existing `Shard` class as a repair-recipe cost leg, exactly as `elevate` already does. It proposes no new class. **There is no collision**; `species-materials`' sixth class (`Trophy`) is an ordinary single-owner ask-first change against `ssot-materials-crafting.md` §3.1. Left here as the record of the error — this is the same "cited without opening" pattern the round-2 audit named but did not itself catch |
| ⭐ **`themes.v2.json` + migration** — the append-only rule and the registry `set-charm-gen` consumes | `seedsmith` (`spec-creature-themes.md` §2.4a) + `item` module 13 | `--rebuild`'s sanction expired once 844 set entries bound to `creature.*` keys |
| ⭐ **An eleventh `CraftOperation` member** for `item-upgrade-tree` | `item` | A **separate** closed enum from `op_kind`; *"adding a verb here is code"* |
| ⭐ **A `ssot-power-scale.md` §11 caps-register row** for `craft_potential` as a soft cap | `power` | PS-8: a cap on a magnitude is a progression ceiling until a verdict says otherwise |
| ⭐ **An amendment to `deployment-hierarchy` module 7's own Never list** (`spec-item-durability-repair.md:408` forbids per-item authored derived fields) | `deployment-hierarchy` module 7 | The owner decided the authored potential override; it has never been reconciled with `:408` |
| ⭐ **Schema ownership of `data/tuning/deployment-hierarchy.v1.json`** | `deployment-hierarchy` module 7 | The file does not exist and `craft-risk-ladder` is Layer 0, so a module that does not own it creates it — with a throw-on-missing-section parser |
| ⭐ **Replace three `27` pins with a reconciliation canary** (`materialgen/vocab.py:120`, `:124` — module-level `assert`s — and `test_recipes_gen.py:193`) | `item-seedgen` module 3 (`materials-gen` owns `materialgen`, **not** seedsmith) | Two of the three **hard-crash on import**, so widening the vocabulary breaks the build, not a test |
| ⭐ **`species-rank`** — an unlisted 19th `creature-seed` module already claiming a `threatBand × rarity` grid | `creature-seed` | Live overlap with `threat-band-fill` and `wave-species-roll`; reconcile before either builds |

⚠ **Filing is not optional and not this map's job alone.** Per the owner's 2026-09-13 decision, each
ask above is **written into the owning program's own map as a row**, the way
`deployment-hierarchy-map.md:89` filed its own. A cross-program ask that lives only here is an ask the
owning program never sees.

---

## Explicitly out of scope — and which state each is in

**Withdrawn** (gone; reasoning kept only as a trail in the ideals):

- **D7 — lineage as the family key.** Disproven by measurement: median group size **2**, 49
  singletons, 272–333 species with no lineage, cyclic DAG, 80% multi-root.
- **The family layer.** Its only purpose was to keep the species layer thin; re-derive it after
  species selection exists, against real play.
- **`tier-system` as a program.**

**Deferred, with the trigger named:**

| Item | Trigger |
|---|---|
| **D1 — invented species** | Freeze each species' currently-computed `CreatureTypeId` as its permanent allocated id and allocate new ones above the max. ⚠ `SlotFilter.cs:49` computes it *without* the `+50,000` plant offset — a latent divergence `delve-species-wiring` must fix |
| **D5 — deterministic exchange** | Rides with `species-materials` |
| **E4 — material grade → socket** | An owner decision, not a bug: `decisions.md:135` deliberately keeps them separate today |
| **The hunt interaction** | The world-stage extension. This initiative stops at *creatures are there and are varied* |
| **The eight-socket topology** | `decisions.md:135` is decided but unapplied; `strain-splice-host` owns the migration order |

**Reserved — named so the socket table leaves room, nothing designed:** **unique item** (world event —
⚠ reconcile against item module 17 `uniques`, which already exists, before either is specced),
**boss item** (4-party raid), **void item** (dropped by a **void beast** via a **void raid** — the void
sieging a player-held sector; the only reserved kind whose source is *defensive*). Also named only:
**demon** (a new empire, *"serious huge program"*) and **void beast** (non-empire).

**Never in this initiative:** any FE surface (a tier bench, compendium, material shelf, hunt board or
bestiary belongs to `item-surfaces` module 20 / `gui-lego`, and a player menu goes through `/idea-ui`);
the `pvz-run` loot source; changing what PvZ itself spawns.

---

## The guard this initiative must ship with, not after

⛔ **No single species' rewards may strictly dominate.** The Wilds/Arkveld case shows monoculture
follows **reward dominance, not the UI** — a map full of creatures whose drops are strictly ordered
will be farmed at exactly one sector. This is `roster-metrics` (creature-seed module 14) pointed at
encounters instead of anchors, and it lands **with** the hunt interaction, not after it.

It is a **report**, never a test assertion: roster coverage is a reading, and
`validation-ssot.md` bans pinning readings.

---

## Open questions the module specs must resolve (all balance data)

None of these blocks the map; each is named here so no module spec silently invents a `const`.

| # | Question | Module |
|---|---|---|
| 1 | What exactly derives crafting potential — the same `class`/`rarity`/`tags` as durability's `max`, or a subset? | `craft-risk-ladder` |
| 2 | How much durability does one craft past exhaustion cost? (**unit must match battle wear's** flat per-mille of `max`) | `craft-risk-ladder` |
| 3 | Is the class ladder E5's successor spine, or a new field? (It is read by **no C# item code** today, so "reuse" is not free) | `item-upgrade-tree` |
| 4 | Where is the species-cost threshold rung, and is it one value or per-verb? | `species-cost-shaping` |
| 5 | How many materials per species within the decided 1–2 band, and do general creatures get **zero**? | `species-materials` |
| 6 | Does the roll restoration ship before or with the `threatBand` fill? (Recommendation: moves 1 and 3 first) | `wave-species-roll`, `wild-species-spawn` |
| 7 | Does the map spawn table share the wave slot table or is it its own? (Recommendation: **its own**, keyed on sector and climate) | `wild-species-spawn` |
| 8 | What is the roster-coverage target? | `roster-metrics` — a report, never an assertion |

---

## ⛔ Corrections the module specs found — the map's own rows are amended by these

Each was measured against code or the shipped corpus while speccing, and each contradicts something
this map or a source ideal carried. **The spec is the authority; these are recorded so the map is not
read against them.**

| # | Where | The correction |
|---|---|---|
| 1 | `craft-risk-ladder`'s dependencies | The map lists **none**. True of its *design*, not its *build*: stages 2–4 need durability, and `deployment-hierarchy` module 7 is **unbuilt** (*"Nothing tests durability, because nothing implements it"*). **Stage 1 is independent; stages 2–4 are not.** The spec recommends splitting on that line |
| 2 | `delve-species-wiring` | `SlotFilter.cs:49` is not merely *"a latent divergence"* — it omits the plant offset the other two sites apply, and **102 `gameTypeId` values appear on both sides**, so it maps 102 plant/zombie pairs onto the **same** `CreatureTypeId`. A silent collision in an identity field |
| 3 | `set-species-binding` | The ideal expected a refusal backlog (*"844 of 884 resolve"*). **All 844 `creature.*` themeKeys resolve — 844 of 844** — but **only case-insensitively** (`creature.abyssswordstar` vs `AbyssSwordStar`). An exact-match repair resolves **zero** and looks like a content gap |
| 4 | `set-species-binding` | ⭐ `data/seed/creatures/_registry/themes.v1.json` carries **`speciesId` as a field** on all 904 rows — a real `themeKey → speciesId` index, a better join than parsing the key's suffix |
| 5 | `creature-drop-tables` | E3a is **not "one read."** The shard mints at `ExpeditionResolver.cs:94-98` from an `isBoss` ternary over two consts, **at plan time, with no species in scope** — and the comment records that plan-time is load-bearing for manifest determinism |
| 6 | `item-upgrade-tree` | ⭐ **The class ladder is only an upgrade spine for armour.** The registry's own rationale says the weapon ladder is ordered *"by combat role, not raw damage number"* (`blade → blunt → launcher`, melee → **ranged**) and the offhand's question is *"does it guard, or does it not."* Upgrading along those is D2's upgrade-becomes-downgrade |
| 7 | `item-upgrade-tree` | The ideal says the class ladder *"is read by no C# item code today."* It **is** read — `ItemSeedValidator/RegistrySet.cs:371`, `ReferenceCheck.cs:130`, and two seedsmith modules. Precisely: **read by the validator and generators, not by the runtime**, and every entry already carries a `rung` |
| 8 | `threat-band-fill` | The ideal's *"ship the `SpeciesExpander.cs:31-33` refusal in the same commit"* would **refuse 719 species**. The exclusion is deliberate and documented (a sanctioned fallback exists). The defect is that the fallback **leaves no trace** — so the fix is provenance + a report |
| 9 | `ladder-consistency-repair` | The 84 retired rarity ids are in **`data/seed/creatures/_registry/themes.v1.json`** (the items one is frozen and carries none) — and the repo's several *"themes.v1.json (84 rows)"* comments are a **stale reference to the old catalog size**, an unrelated coincidence |
| 10 | `tier-propagation-contract` | A **fourth** restated ladder, not in the ideal's three: `Items/RarityLadder.cs` `RungIds` restates the ten item rarity ids while its own class comment says the seed file is the authority |
| 11 | Every tunables row | The ideals write *"a new `<file>.v{n}.json` revision."* The shipped pattern is **one file carrying `schemaVersion` + `version`** (`sockets.v1.json` is at `version: 2`). The `v{n}` phrasing would have created second files |
| 12 | `species-materials` | The sixth `MaterialClass` has an **exact stated test** to pass — *"which of these five questions is unanswerable for my spend?"* The spec argues it against `Substrate` (the near miss) rather than asserting it |

## ⛔ Corrections — round 2, the five-agent audit (2026-09-13)

Five parallel audits — coverage, evidence, compliance, dependencies, adversarial — found **~35
defects**, the large majority in the specs rather than the ideals. All are fixed in the files; the
load-bearing ones are recorded here because **the pattern matters more than the individual errors**.

### Three failure patterns, named

1. **Cited without opening.** `ItemWorkbench`'s "five verbs" (`Temper`/`Bore`/`Socket`/`Imbue` are
   **not method names** — they are `CraftOperation` arguments *inside* `Enhance`/`SocketAdd`);
   `CreatureRarityLadder.IsTopRung` called on an **item** rarity (wrong type, would not compile);
   `Disposition` cited to `RpgStore.ItemUniques.cs` instead of `RpgStore.Items.cs`;
   `SocketCircuitSize`, which is a proposed snippet in a spec doc and **not shipped code**.
2. **Trusted a doc's status over the code.** `item-map.md` says module 23 `requirement-profiles` is
   *"approved 2026-09-09"*; a spec wrote *"exists"*. `grep -rn "RequirementProfile" src/` → **zero
   hits**. **Approved is not built.**
3. ⭐ **Read a struck claim and rebuilt on it anyway.** Five specs used figures the tier ideal's own
   § AUDIT had already corrected — `decisions.md:131`'s overstated kill attribution, E2's "one arm"
   (really 2–3 Data files plus a `string`/`long` player-id mismatch), the propagation refactor's
   6–8 sites (really ~30 across ~24 files), and `RarityTuningCoverageTests`' real coverage
   (5 tuning files with two **seven-rung** exceptions, not a blanket guard). This is the exact
   propose→get-corrected→read sequence DESIGN-GATE exists to stop.

### The findings with the highest rework cost

| # | Finding |
|---|---|
| 13 | ⭐ **`set-species-binding` was a layer ahead of the module that rewrites the registry it reads.** The spec contained the warning (*"sequence them"*) and the map ignored it. New edge; module moved to Layer 2 |
| 14 | ⛔ **`ladder-consistency-repair`'s root cause was wrong, and its fix path has an expired sanction.** The generator's no-op defect is described in the **past tense**; the inputs are already clean. The 84 survive because the registry is **deliberately append-only**, and `--rebuild`'s permission was conditional on *"nothing is bound to these keys yet"* — **844 set entries are now bound.** Owner decision: **`themes.v2.json` + migration** |
| 15 | ⛔ **T-5 as written was false of shipped code and made three sibling modules illegal.** `materials.v1.json` prices six verbs on `rung` as a **coefficient**. Split into T-5a (actor magnitude — additive `thetaOffset` only) and T-5b (economy cost — a rung coefficient owing its own §10 row). Success criterion 6 would otherwise have written the false rule into the power SSOT |
| 16 | ⛔ **`item-upgrade-tree`'s armour-only scope was argued from the wrong evidence, and armour fails that argument too.** The real vindication is `ssot-item-categories.md:493` (class rungs ×1.4) and `:627-628` (plate wins guard 2.3×, **never overlaps**). **And the constraint that exposes:** `:629-631` says cloth's entire compensation is its **class-tagged affix pool and implicit slate**, so "carry affixes untouched" **launders cloth-pool affixes onto a plate chassis** and silently swaps the implicit. Two new mandatory rules (Design §2a) |
| 17 | ⛔ **`socketAllowanceByKind` was declared a tunable on an append-only, `catalog_revision`-derived surface** — a post-ship edit silently re-sockets every item ever dropped at that rung, and **no build-time test catches it.** Rows are now append-only and revision-pinned. The derived window also bypassed **OD4 overlap, monotonicity and non-negativity**; all three are now load rejections |
| 18 | ⛔ **Three live `27` pins, two of them module-level `assert`s that hard-crash seedsmith on import.** Widening the material vocabulary breaks the build, not a test. Replaced with a reconciliation canary |
| 19 | **`rarity-promotion`'s declared dependency was satisfied by nothing.** `Items/RarityLadder` has no `IsTopRung`/`OneRungAbove`/`RungCount`, and `tier-propagation-contract` will not add them. Item-side rung arithmetic is now that module's own deliverable |
| 20 | **Two drop-table corpora, and the spec named the wrong one.** `data/seed/loot/README.md` has a section titled *"Why this is not `data/seed/items/drop-tables/`"*. `droptablegen` writes the latter; E1's runtime tables belong in the former. ⚠ The ideal's claim that `data/seed/loot/**` *"does not exist"* is also **false** |
| 21 | **A settled decision was reversed without saying so.** D5 (*"ships **with** the first tier content, undisputed"*) was re-opened as an open question recommending the opposite. Restored |
| 22 | **Measurements corrected:** 910 set entries, not 911 (the 911th was a generator ledger); `theme.*` 30, not 31; **seven** ladder restatements, not four; `waves.v1.json` has **no `version` field**, so a revision is an add, not a bump; `CreatureSpeciesCatalog` **already enforces** `creatureTypeId` uniqueness at load (the narrower real gap is that `SlotFilter`'s computed property bypasses that guard) |
| 23 | **Three fake open questions** (each answered in its own sentence) and **one manufactured certainty** (*"not a scope cut — it is a correctness requirement"*, which no owner decided) removed |
| 24 | **Unverifiable claims struck rather than repeated** — the "6 sites vs ≥22 files" measurement had no ladder, file list or command behind it |
| 25 | ⛔ **SOLID:** two specs proposed the same admission rule as **prose duplicated across two files**. Replaced with one `CreatureAdmission` declaring site in Core, a named member per context |

### What survived the audit

Every load-bearing **measurement** taken while writing the specs reproduced exactly on an independent
re-derivation: 904 species / 185 stored `threatBand` / 719 absent; the 730-on-one-rung reconciliation;
**102 colliding `gameTypeId`s**; 844-of-844 case-insensitive themeKey resolution with **0** exact
matches; `MaterialCatalog.All == 27 == 10+8+6+3`; 67 recipes with 10 `elevate`; exactly **10 distinct
species** reachable across the four shipped waves. Every long verbatim code-comment quote matched word
for word, with the behaviour verified in code rather than inferred from the comment.

**Tunables (T1–T8), validation-ssot, numeric overflow and the ActorHub gate came back clean across all
fifteen specs.** The failures were in claims *reasoned to*, not claims *measured*.

---

## Gate status

**DESIGN-GATE §5, honestly:** the four source ideals each carry their own reading-gate section
recording the documents read and the code verified at `file:line`. This map introduces no new claim
that is not carried by one of them. **The §5 boundary box remains untickable** — this session wrote
no `tasks/sessions/*.json` record, because `/session-start` does not exist in this harness. That gap
is unchanged from the ideal phase and is stated rather than hidden.

**Next step:** owner approves module boundaries, dependency direction and build order — then each
module gets its own spec at `docs/architecture/species-gear-chain/spec-<module-id>.md`, written in
dependency order.
