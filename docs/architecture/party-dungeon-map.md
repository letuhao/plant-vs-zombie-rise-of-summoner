# Capability map: party dungeon (the Delve)

**Status: APPROVED 2026-09-05 — module boundaries, build order and the four prerequisite
`decisions.md` rows (§Prerequisites) all approved by the owner the same day; the rows are appended to
`decisions.md` verbatim.** Module specs are written per wave, in dependency order, each verified against
code and the ideal before it is placed. No module is built until its gate's prerequisites land.

**Ideal it implements:** [party-dungeon-ideal.md](party-dungeon-ideal.md) — Part I (the shape), Part II
(fourteen sub-mechanisms as seed generators paired with in-game generators), and **twenty-eight owner
decisions** (§8, §11.9, §11.10 boxes — the boxes override the prose). **Review it inherits:**
[party-dungeon/audit-2026-09-05.md](party-dungeon/audit-2026-09-05.md) — six lenses, 98 findings, 9 S1
and 17 S2 clusters, all eleven forks ruled the same day.
**Module specs:** [party-dungeon/](party-dungeon/), one per module id below.
**Plan / tasks (later phase):** `tasks/party-dungeon-plan.md` · `tasks/party-dungeon-todo.md` — the
prefixed pair per `AGENTS.md`; the bare pair is another stream's and is never a fallback.

---

## What this program is

A **party dungeon crawl** on the RPG layer: the commander provisions a raid of one, two or four
parties of up to five bound demons, enters a **domain** — a seeded graph of rooms rolled fresh each run
— and plays room by room through fights, elites, caches, curios, wild demons, shrines, rests, merchants
and traps to a boss, carrying wounds, hunger and nerve between rooms and an **unbanked haul** in a
per-party carry grid until extraction. Depth is `DangerBand` inside the one power ladder; difficulty is
ten named rungs with an open tail; loss is priced by default and permanent on the upper rungs. Every
piece of content is a **seed** written offline by seedsmith and a **concrete object** rolled per player
by the game runtime, which never calls a model.

## What it is not

- **Not a change to what PvZ is.** Zero injector work. Every row below is `Core/`, `Data/`,
  `Server/`, `web/` or `tools/seedsmith/`. The lawn may later *enrich* it (a live lawn as a room kind,
  §7 of the ideal); it never gates it.
- **Not a second power ladder.** No module owns a curve. Depth, rungs, the tail and once-entry are
  `DangerBand` deltas into `ssot-power-scale.md` §10 row 23; magnitudes read `P(Θ)` once, through
  `Instantiator`, `LootPipeline` or `BattleRuleset`; counts and `W` are encounter design.
- **Not a second roll SDK.** Structural rolls (which room, which event, which species fills a slot)
  reuse `SeededRng.DeriveStream` + `WeightedChoice`; every magnitude inside the thing picked goes
  through `Instantiator.TryInstantiate`.
- **Not the battle board, the siege stage, the structure corpus, the world generator or the uniques
  corpus.** Those are other programs' modules; this program **consumes** them and files asks on their
  maps (§External dependencies). Decision 11's "build A10 here" is retracted (review R1).
- **Not an expedition refactor.** Expeditions keep running; the Delve is the same battle resolver
  *played* and *branched*. Whether expeditions later re-express as auto-resolved delves is the owner's
  call after the world map (ideal §7).
- **Not wall-clock paced.** No stamina, no daily limit, no real-time recovery (review R6). Recovery
  is measured in delves.

## Assumptions — correct these now

1. The four `decisions.md` rows in §Prerequisites are approved **with this map**, as base-defense's
   fifth-stage row was approved with its map. If any is refused, the module that needs it is held.
2. Wave 1 ships **both** entrances (decision 2): the Sanctum picker is this program's; the map door is
   an **ask on `world-stage-map.md`** (`world-inspector` action + `world-commands` order) that issues
   the same delve request with a `domainId` — no delve-built map FE, no legion leaves the map (R10).
3. The threat-band hole (657 of 841 species anchors without `threatBand`) is closed by demon-seed
   module 7's `threat-audit` **before** `encounter-generator` ships. It is an external dependency, not
   this program's work.
4. The first shipped content is **six `many` domains, one per climate at `shallow`**, from the
   seedsmith pipelines (decision 6); once-entry domains follow once `world-generator` can place them
   or the Sanctum picker offers them (decision 15).
5. `hybrid-atb`'s `wReact: 1` (`battle.v4.json:37`) is inherited by the `delve` profile row and stays
   inert until the engine reads it — a wiring fact, not a design one.

## Prerequisites — `decisions.md` rows owed before any module is named for build

Drafted here for approval; on approval they are appended to `decisions.md` verbatim and the four
modules that need them (`delve-stage`, `delve-scope`, `delve-attrition`, `unique-pipeline`) unblock.

| # | Row | Draft text |
|---|---|---|
| **P1** | Game GUI — sixth stage | *"**`delve` is the SIXTH stage** (`#/delve/{id}`), approved 2026-09-05 with the party-dungeon map. The Delve is a place the player acts in (GG-4), so it is a stage, not a layer. The `battle` id at `railState.ts:31` is base-defense's (decisions 40/44) and is not reused. The room graph is the stage; a fight is drawn on it; the pack, wild-talk and event surfaces are band-2 layers; the extraction summary (with any wipe/permadeath notice folded in) is the one band-3 result; drops, level-ups, joins and first-clears report at band 4. `information-architecture.md` §1/§2/§4/§5/§7 and the stage-count CI assertion move with this row."* |
| **P2** | World store — delve worlds | *"**A delve is a `WorldState` row of `kind='delve'`** (decision 5). `rpg_worlds` gains `parent_world_id` and `kind` columns — never `mode`, which is the clock axis (`RpgStore.World.cs:25-27`). `WorldValidation.Validate(world, profile)` gains a delve profile that skips rules 4/5/11/13 and reads a `RoomTypeCatalog`/`DoorTypeCatalog` pair (same `SectorTypeDef`/`LaneTypeDef` shapes) that is **not** served on `/api/world/catalog`. `GetActiveWorld` filters `kind='map'`. **The delve host never calls `TurnEngine.Step`** — rooms are moved through by the delve resolver; `LegionSupply`, loam, growth and pressure never run on a delve world. World goldens do not move (the header row hashes `TemplateId, Seed, CurrentTurn` only). Jointly owned with the world program as module `delve-scope`."* |
| **P3** | Status SSOT + Resource model — nerve | *"**`spirit` pays for nerve.** Spirit drain from horror curios, elite auras and boss presence applies stacks of a **staged `nerve` status** (stage names tunable; unsettled → shaken → afflicted as the starting shape), each stage a container of atoms, resolved through `StatusRuntime`. The shipped `StatusStacking` (`Refresh/Replace/Coexist`, `ResistanceEvaluator.cs:18-23`) is a re-apply policy, so a **stack counter with stage thresholds is a small new build** beside the `Counter` kind. `StatusCatalog` gains the `nerve.*` ids (ADR-locked list, 21 → 21 + stages). `resource-hub-ssot.md` §2's normative 'pays for' column gains *"`spirit` — essence cost; **and nerve**: drained by harm in a delve, restored only by rest and supplies, never by regen"*. Amended 2026-09-05 with the party-dungeon map."* |
| **P4** | Action model — extended action slots | *"**`LoadoutSet.MaxSize = 5` is the structural base of a `loadout.slots` derived channel**, registered in the Actor Hub like every other derived channel; the three readers (`LoadoutSet.cs:60`, `AutoEquip.cs:55`, `CapPolicy.cs:39`) read `base + channel`. The channel is fed by an **extend-action-slot** atom effect — `stat.derived` on `loadout.slots` if the closed 16-kind vocabulary admits it, else a reviewed seventeenth kind — carried by rung ≥ 90 uniques as a fixed core and by normal drops at `loot.extendSlotChanceMicro` (100 per million, drawn per-million on a named stream). One extend-slot item counts at a time (`affix.exclusiveTags`). Unique **passives** sit outside the five slots; unique **actions** occupy one. Decided by the owner 2026-09-05 (ideal §11.9 #16, §11.10 R7)."* |

**Propagations owed alongside (evidence rule 6), not gated on approval:** `decisions.md:50` (1) "WReact 0"
is stale; row 42's "an existing content id" gains "or an encounter anchor id"; `ssot-power-scale.md`
§11.1's three caps rows are stale (all three now derive and throw) and §10 row 27's `WebMatchService`
line is `:396-403`; `structure-seed-ideal.md:73` (seven rows, three kinds); `effect-atom/definitions.md`
(16 kinds / 13 triggers / eight `OwnerKind`s); `PredicateNode.cs:11` "unbuilt" comment; `information-
architecture.md` §1 "four stages"; demon-seed documents' "408"; `06-unsourced.md` access blocks
(megatenwiki bot gate, diablowiki 403, game8 empty bodies).

## External dependencies — other programs' modules this program consumes

| Dependency | Owner map / module | What this program needs | Gate |
|---|---|---|---|
| `threat-audit` over the 657 anchors without `threatBand` | `demon-seed-map.md` module 7, pipeline 5 | a `threatBand` on every species anchor, so `threatWindow` filters mean something | before `encounter-generator` |
| `battle-clock-profile` | `base-defense-map.md` | `MaxRounds`/`RoundDurationMs` on the profile; the `delve` profile row lands after it | before `delve-battle-profile` |
| `siege-ai` (one `IIntentSource` dispatching on `SideOf`) | `base-defense-map.md` | the automated policy that plays un-steered parties (R9) | before raids of 2/4 |
| `siege-board` + `board-render` (A10) | `base-defense-map.md` | the 2-D board the Delve adopts later; rank collapses into column | none — v1 ships 1-D rank |
| `siege-waves` roster-add (`Spawn`), `combatant-kind` | `base-defense-map.md` | in-battle summoning, summoner and fixture bosses | none — v1 bosses are seated at setup |
| base-defense decision 46 (pause = persisted decision log) | `base-defense-map.md` / `spec-interactive-turns.md` | freeze-on-switch for a steered party (R9) | before `delve-battle-profile` |
| `structure-schema` 18th field `interaction`; `structure-catalog-import` reader | `base-defense-map.md` modules 23–29 | dungeon objects as structures | none — v1 objects are curios in the event deck |
| `consumable` `ContainerKind` (D27) and the item-cost row on actions (A3) | `item-map.md`, `action-map.md` | supplies usable in a fight; a capture seal consumed | before `supplies-and-objects`, `wild-room` |
| item module 17 `uniques` amended (generated, rung ≥ 80, X4-gated); `seedsmith-map.md` `unique-pipeline` | `item-map.md` | the uniques pipeline this program's domains bind | before `unique-pipeline` |
| contracts upkeep and slot/ritual prices on the player's highest cleared content Θ | `demon-system-map.md` `demon-contracts` follow-up | the soul sink that keeps binding past the pin (review S2-8) | ships in this program's **first** wave |
| `world-generator` placing `Lair/Tear/Vault/Anomaly` entrances | `world-map-program.md` wave 4 | once-entry domains placed on the map | none — Sanctum picker offers them meanwhile |
| map-door action in `world-inspector` + `world-commands` | `world-stage-map.md` | the map entrance (decision 2), issuing this program's delve request | wave 1 ask |
| T10 decision trace, T14 profile row in `battle.v{n}.json` | `battle-timeline-map.md` | the per-battle trace; the `delve` profile as tuning | before `delve-battle-profile` |

## Modules

Stable kebab-case ids, chosen once. Every module is provable with the game closed. "Model?" is
whether the module itself calls a model (only seedsmith modules do, offline).

| # | Module id | Responsibility | Depends on | Model? | Wave |
|---|---|---|---|---|---|
| 1 | `dungeon-registries` | The closed lists as **one JSON registry each** under `data/seed/dungeon/_registry/`, read by both the C# catalogs and the seedsmith validator (review G20): room kinds (11), door kinds, override tags, objective templates, difficulty rung ids, disposition, interaction verbs. `data/tuning/dungeon.v1.json` and `encounter.v1.json` **schemas and loaders** with T5 rejection on any missing key, T6 units, one owner per number (no copies of `wildJoinMilli`, `costPerPull`, `boss-lair`'s band — review N13). Structural constants carry the exemption comment | — | No | **1** |
| 2 | `dungeon-seed-contract` | The seven anchor shapes (domain, room, layout, event, quest, encounter, supply/object extension) with **one ownership level per field** including the new **PLANNED** level; the four-shape schema audit plus a stem check (`*weight*`, `*chance*`) and a spelled-number list; `dropBand` reused (never `weightBand`); `manifestCost` allow-listed; a `dungeon` seedsmith adapter whose `reference_fields` **derive** the sub-pipeline order (registries/species → layouts, supplies → events, encounters → uniques → loot tables → rooms → domains); the **planner** (per-cell disjoint motif briefs with siblings as anti-motifs, per-cell `budget`, `entry` and `dangerBand` PLANNED, vote set by cost-of-being-wrong); provenance `{planHash, promptVersions, registryVersions, motifSubsetHash}` and a byte-identical rerun test; the call-budget `--dry-run` | `dungeon-registries` | **Yes** (pipelines); planner and audit are model-free | **1** |
| 3 | `delve-scope` | The store shape of decision 5 (row P2): `parent_world_id` + `kind` columns; `WorldValidation.Validate(world, profile)`; `RoomTypeCatalog`/`DoorTypeCatalog` off `/catalog`; `GetActiveWorld` filter; `rpg_delves` header (raid mode, seed, state, party routes, pity per party, haul ledger) and `rpg_delve_rooms` (cleared, visited, floor list); the rule that the delve host never calls `Step`. **Jointly owned with the world program**; hash-stability of world goldens is its gate | `dungeon-registries`; row P2 | No | **1** |
| 4 | `delve-graph-roll` | The pure structural roller `Roll(domain, layout, seed, raidMode, tuning)` → rooms and doors as `WorldSector`/`WorldLane` rows: layered DAG, path walks per raid mode, node kinds from the weight table, room archetype per kind × climate, gates and keys (key strictly above the gate on another walk, validated), one-way lanes deeper only, secret rooms at dead ends, fixed rows (first fights, mid cache, rest before boss), sight as `Glimpse`/`Full` from tuning; named streams; validators that **throw**; the sealed per-run seed | `dungeon-seed-contract`, `delve-scope` | No | **1** |
| 5 | `difficulty-ladder` | The **first production composer of `Θ_content`**: `ContentContext(DangerBand = entrance + rowStep + bandDelta [+ once +7] [+ tail·n], …)` through `PowerIndexComposer`; the ten-rung table with decision 7's deltas, every rule rung carrying a reward-bearing column (R8), the validator (neighbours differ in `bandDelta` or a reward column), `depth.bossBandDelta` on the last corridor, refuse-not-clamp for rungs that would floor on a domain; the `tail` block; `permadeathFromRung` per domain; the Oath as opt-in below the gate with **a clear at `maxRungWithoutOath` unlocking the next rung** (R4); once-entry `bandDelta` +7 with effective-band display, stack rule written, `sealOnWipe` + `failKeepsBossLoot` (R12); the **actor-side `Θ_actor` composition** listed as the wiring gap it is, with the specimen-level fallback stated | `dungeon-registries` | No | **1** |
| 6 | `encounter-generator` | Encounter anchors → `BattleActorSetup`s at `θ = Θ_room + thetaOffset(species)` (the sum nothing computes today); slots as filter tuples over anchor ordinals (posture, reach, targetPreference, threatWindow, element spread); formation as **1-D rank on `SideIndex`** with `rankSpan` (R1: board adopted later); `pack` / `party` / `boss`; the boss as a `role: boss` slot with kit ordinals, **retinue per party, `W` per raid, and a shield pool as a share of `P(Θ_room)` per extra party with a fight-length target band and RoR2's 0.3 as the reference** (review §1(d)); elites as one slot with an affix roll through `TryInstantiate` into a seventh `ContainerKind` `enemy`; the cell-coverage closed-loop metric; refuse-loudly on unfillable slots and missing `threatBand` | `dungeon-seed-contract`, `difficulty-ladder`; external `threat-audit` | No | **2** |
| 7 | `delve-battle-profile` | The `delve` mode-profile row (`hybrid-atb` shape, `RequiresLiveInput: true`, `OrdersBySpeed`, a `PerSide` economy option) in `battle.v{n}.json` after `battle-clock-profile`; the **explicit** `BattleEngine.Resolve(setup, seed, profile:, intentSource:)` call from the delve host (never `ProfileForExpedition`/`ProfileForWave`); the automated policy for un-steered parties consumed from `siege-ai`; `InteractiveIntentSource` for the steered party over SignalR; **freeze-on-switch** as a persisted decision log (decision 46); the **delve-level decision log** (route choice, pack moves, talk answers, steering) beside T10's per-battle trace; golden-safe additive fields on `BattleActorSetup` — `long? CurrentHp`, `int? PartyIndex`, `int? RankSpan` — all `WhenWritingDefault` (the review's goldens reasoning — record §2 S1-2 and the architecture pass's hash-input table: battle goldens hash `BattleReport` only, expedition tier hashes include `BattleSetup`), and `PartyIndex` on the result as an init property never positional; a second `Retreated` producer for capture; `decisions.md` row 42 clause | `encounter-generator`; external `battle-clock-profile`, `siege-ai`, decision 46, T10 | No | **2** |
| 8 | `delve-attrition` | Carry-across-rooms state: `ActorResourcePools` constructed for battle actors (the seat exists in `Actions/Cost`; battle lacks the reference), hunger per room from `hazardBand`, exhaustion statuses, **nerve** as spirit drain applying the staged status (row P3), the `rest` room's activations and heal ‰, rations `useContext: rest` (hunger binds between rests), `Downed` from the timeline FSM wired into `Resolve`, **`downedOnce`** and Retired-at-extraction on permadeath rungs (R3), the wipe rule, recovery **in delves** (`risk.downedRecoveryDelves`, R6) settled at the Data layer, the recovery ritual priced at the delve's Θ, contract loyalty results applied at extraction | `delve-battle-profile`; row P3 | No | **2** |
| 9 | `event-deck` | The event catalog reader (seed → rows via the seed import path, `SeedScanner`/`SeedImportRunner` — `AtomImporter` is the tool that calls it), the four-filter selector (kind, climate, E3 eligibility tree, repeat scopes: per-delve `seen` set / `(domainId, eventId)` / `(playerId, eventId)`), "no repeat within N rooms", unknown-room pity **per party** (R11) on `dungeon:unknown:{r}:{c}`; the outcome draw with `supplyOverride` via `HoldsStock`; the chosen outcome's effect bundle rolled through `TryInstantiate` at the room's Θ; delve-scoped grants bound on `UniqueActor` with `source = "delve:{id}"` and withdrawn at extraction; the four new predicate leaves (`BandIs`, `HaulAtLeast`, `RoomKindIs`, `PartyDownedCount`) with `FactReader` readers; `resource.delta` executor beyond `hp`; a `ui.present` sink for the battle runtime; validator rules (≥1 bad-or-mixed and ≥1 good; no free Leave outside `story`; no event gates the boss); a `remembers` outcome row for the wild talk | `dungeon-seed-contract`, `delve-graph-roll`, `delve-attrition` | No | **3** |
| 10 | `dungeon-loot` | The **first production host of `LootPipeline`**: `dungeon-room`/`dungeon-clear`/`dungeon-quest` source kinds in both lists with server-derived correlations `loot:delve:{delveId}:{r}:{c}`; the host **synthesizes** `LootSourceRow` with the room's `Θ_content`; `Mint` closing over the room Θ; the boss first-clear grant instantiated through `TryInstantiate` on its own stream (never flat); entry `RefId` base-type sets resolved; boss `affixChannel`; domain tables per room kind with elite/boss `rarityFloor`/`rarityShiftRungs`; **victory souls once per delve at extraction on `Θ_run`, forfeited on a wipe** (review S1-1) — rooms pay `KillEarn` only; the three in-run soul sinks (provisioning, merchant reading the *room's* Θ, altar) priced through `SoulSinkPolicy`; the once-entry `failKeepsBossLoot` rule; the SSOT §11.7a souls-per-minute regression with "two row-1 rooms then extract" as the stall row | `delve-graph-roll`, `difficulty-ladder`; external contracts-on-Θ | No | **3** |
| 11 | `loot-pack` | The per-party, per-delve **carry grid**: `raid.modes.*.pack.{rows,cols}` structural with the exemption comment and the test `the_pack_never_reads_armoury_capacity`; item **footprint DERIVED** at import from role footprint ordinal × the shipped five-value `mass-class` tag; supplies consume cells; **first-fit-decreasing auto-arrange** as a pure integer function; `pack.move`/`pack.drop` as trace decisions; autopilot resolves the floor list by rule id (`value-per-cell`); boss grants dealt round-robin by `PartyIndex`; charms stay in the AP pouch; extraction is **raid-wide** (a party may hold at a rest, never bank); the D26 reconciliation sentence copied verbatim; the cells-vs-linear-volume statement with its arithmetic and either a Θ-scaled cell count (tunable) or an explicit count-pin (review N5) | `dungeon-loot`, `delve-scope` | No | **3** |
| 12 | `supplies-and-objects` | The consumable anchor extension: `useContext` widened with `rest`/`curio`; `overrideTags[]`; `sizeBand`/`stackBand`/price **DERIVED**; the supply → class mapping (ration → `restore`→`hunger`, ward → `shield.grant`, key/bait → `utility`); the usable path on `OnActivate` with `HoldsStock` usability and an **item-cost row** on actions (external A3); `status.clear` allowed on `OnActivate` for antidotes; provisioning before entry as the soul sink priced on `contentScale(Θ_entrance + Wm·bandDelta)`; camp actions with `useContext: rest` competing for the five slots; objects: the structure/curio rule, the `interaction` verb registry as an ask on `structure-schema`, and v1 curios only | `event-deck`, `loot-pack`; external `consumable` kind, A3 | No | **3** |
| 13 | `wild-room` | Recruit as a decision tree on `dungeon:wild:{r}:{c}`: personality from the room key, `Δ = Θ_wild − Θ_party` bands (`Θ_party` = the commander's composed `Θ_actor`, recorded beside §10.2 row 11 — never a mean), the SMT four offers with **the souls offer flooring at the altar pull price from unbanked souls** (R5), disposition base rows, one-rung shifts, `joins / takes-and-leaves / flees / attacks / remembers`; **capture** as a corpus action (`Relation = Enemy`, `hpBelowMilli ∧ hasStatus ∧ holdsStock(seal)`, seal consumed, hp-band × Δ-band ‰ table + status count band + seal tier, per-target ramp, `Retreated` exit, no `KillEarn`); the **altar room** on `SummonRoller` with a domain-focus banner, shared pity, results as **at-risk ledger haul delivered at extraction**; every result teleports home at extraction, bound if a slot is free (decision 12); `Origin = "delve"` / `"capture"` | `event-deck`, `delve-battle-profile`, `dungeon-loot` | No | **4** |
| 14 | `delve-quests` | Quest anchors (closed objective templates, kind-ref targets, `countBand`, `rewardBand`, scope `delve/domain/roster`); 2–3 offered at entry on `dungeon:quest`; a predicate evaluated **once over the delve report at extraction**, idempotent on `(playerId, questId, delveId)` under the exactly-once envelope; rewards through `LootPipeline` `dungeon-quest`; sink-avoidance templates eligible only at rung ≥ hard or paired with a risk objective (review D14); quests reward, never unlock | `dungeon-loot`, `event-deck` | No | **4** |
| 15 | `domain-catalog` | Domains as content: the runtime catalog read from `data/seed/dungeon/domains/`; `entry: once|many` (PLANNED); the standing sub-world per `many` domain vs one row per `once` delve; sealing on extraction/boss kill, `sealOnWipe` with `failKeepsBossLoot`; the Sanctum **Delve layer** (picker: domains found, band name, entry, boss); domain discovery by the expedition `FoundDomain` tick (the game-closed proof) and first clears; the map-door request shape (`domainId` in, same delve request) filed on `world-stage-map.md`; `world-generator` placement filed on `world-map-program.md` | `delve-graph-roll`, `difficulty-ladder`, `dungeon-loot` | No | **4** |
| 16 | `unique-pipeline` | The seedsmith uniques extension (decision 13): rung ≥ 80, never `set_eligible`, fixed core + variance slot, passive and action grants as atoms, extend-slot as a fixed core on rung ≥ 90; the 95 shipped uniques below rung 80 decided (re-rung in place or `enabled: false`) and the 64 `unique` table rows re-pointed; source-locked uniques bound by the referencing domain table with a `seed_graph` assertion; the **`loadout.slots` derived channel** and its three readers (row P4); `loot.extendSlotChanceMicro` on a per-million stream; `affix.exclusiveTags` for extend-slot; `item-map.md` module 17 amended and `seedsmith-map.md` row filed | `dungeon-seed-contract`, `dungeon-loot`; row P4; external module 17 / X4 | **Yes** (pipeline) | **4** |
| 17 | `delve-stage` | The **sixth stage** `#/delve/{id}` (row P1): room-graph render with `Glimpse`/`Full` sight, the fight drawn on the stage, party HUD (parties by name, never index), resources and haul, initiative rail; the pack as a band-2 layer; the wild talk and event banner as layers; reward bands (extraction summary band 3 with wipe/permadeath folded in; drops, level-ups, joins, first-clears band 4 queued); the Oath and once-entry confirms; `BANNED_WORDS` extended (`Θ`, `bandDelta`, `Retired`, `PartyIndex`, `once|many`, engine ids); unlock row *"Delve — first domain found (expedition)"*; live SignalR session with reconnect and AFK per T6/T11; refresh-safe because state is the server's | `domain-catalog`, `loot-pack`, `wild-room`, `delve-quests`; row P1 | No | **5** |

**Dependency direction, no cycles.** Registries → seed contract → (scope, graph, ladder) → encounter →
profile → attrition → (deck, loot) → (pack, supplies) → (wild, quests, domain, uniques) → stage.
`unique-pipeline` depends on `dungeon-loot` for the table binding and on `dungeon-seed-contract` for
the adapter, never on the stage.

## Build order

```
Wave 1  dungeon-registries → dungeon-seed-contract → { delve-scope ∥ difficulty-ladder } → delve-graph-roll
Wave 2  encounter-generator → delve-battle-profile → delve-attrition
Wave 3  event-deck ∥ dungeon-loot → loot-pack → supplies-and-objects
Wave 4  wild-room ∥ delve-quests ∥ domain-catalog ∥ unique-pipeline
Wave 5  delve-stage
```

**Why this order.** Model-free modules first — a registry, a schema, a loader, a pure roller and a
composer produce value with zero tokens spent and make every later generator's inputs reviewable.
`encounter-generator` comes second because it is where `Θ_content` finally meets the species offset and
every other generator reads that number. The seedsmith pipelines (`dungeon-seed-contract`'s model
half, `unique-pipeline`) run once the runtime that consumes their output exists, so a bad corpus is
caught by a real consumer rather than by review. The stage is last because every layer it draws is
another module's read model.

## Gates

| Gate | Proves | After |
|---|---|---|
| **G0 — prerequisites** | The four `decisions.md` rows are appended; the propagations are made; `threat-audit` is scheduled on the demon-seed budget | before wave 1 build |
| **G1 — scoped world** | A delve world row exists beside a map world; `GetActiveWorld` returns the map; `WorldValidation` accepts a rolled graph under the delve profile and still rejects it under the map profile; **all world goldens byte-identical**; `Step` is never called on a delve world (guard test) | wave 1 |
| **G2 — a room is a fight** | One rolled room resolves through `BattleEngine.Resolve` with the `delve` profile and an automated intent source at `Θ_room + thetaOffset`, byte-identical on replay; **all four battle hashes, the 32-seed sweep and the four expedition tier hashes unchanged** (`WhenWritingDefault` fields proven); a steered fight frozen and resumed from its decision log | wave 2 |
| **G3 — a delve is a run** | A full solo delve on autopilot: rooms, events, loot into the pack, extraction; souls-per-minute regression (two row-1 rooms then extract loses to a clean run); hunger binds between rests; a downed demon sits out N delves; a permadeath rung Retires a `downedOnce` demon at extraction | wave 3 |
| **G4 — content** | Six domains from the pipelines pass the schema audit, the budget check and a byte-identical rerun; the encounter cell-coverage metric passes per domain; a 4-party raid resolves with per-party packs, pity and hauls and one boss fight within the fight-length band | wave 4 |
| **G5 — played** | The stage renders a live delve over SignalR; reward bands hold under a lint over band-3 openers; `vocabularyGuard` rejects every engine word; the Sanctum picker and the map-door request reach the same endpoint | wave 5 |

## What this program does not touch

The injector and anything under `pvz.*`; `BattleEngine`'s round order or resolver math; the
`EffectBag`/Funnel/Writer paths; the world turn phases; drop volume, drop tables' weights or the
armoury (D26); `SummonRoller`'s pity rules; expedition tiers or hashes; the map FE beyond the filed
ask.

## Open items carried from the ideal and the review

None owner-facing: all twenty-eight decisions are closed. Three spec-level items the modules must
answer in their own text — the exact `bossShieldPerPartyMilli` derivation from the `W` ratio
(`encounter-generator`); whether pack cells scale on Θ or count-pins (`loot-pack`); which of the 95
sub-rung-80 uniques are re-rung and which retired (`unique-pipeline`).
