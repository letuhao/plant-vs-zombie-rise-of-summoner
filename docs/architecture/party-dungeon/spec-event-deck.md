# Spec: event-deck

Status: **APPROVED by the owner 2026-09-05 (wave 3) — written against shipped code and the eight earlier
approved specs; unbuilt.** Every `file:line` below was opened this session; drift against the map row and the brief
is reported in §Drift. Every number is a starting shape, never a balance decision.

Module id `event-deck`, row 9 of the [party-dungeon map](../party-dungeon-map.md) (`:119`; wave 3, parallel
with `dungeon-loot`, `:139`). Depends on `dungeon-seed-contract` (the event anchor §1.4, `spec-dungeon-seed-contract.md:76-90`;
the supply extension `:121`), `delve-graph-roll` (`Facts` — `kind`, `archetypeId`, `partyRouteMask`, `:64`; the
reserved streams it never draws, `:130-132`) and `delve-attrition` (`PartyState`, `NerveLadder`/`NervePolicy`,
`RestResolver`'s `RestOutcome.Ambushed` seam, `spec-delve-attrition.md:419-422`). Reads `dungeon-registries`
(`events.*`, `nodes.unknown.pity.*`, `rest.ambushMilli`, `difficulty.rungs[].{eventSeverityTier,unknownPityStepMultMilli}`,
the `override-tags` registry, `bands.{eventKind,outcomeOrdinal,repeatScope}` — `spec-dungeon-registries.md:74, :80,
:129, :134, :140, :143, :311`), `difficulty-ladder` (`RoomTheta`, `RungTable.Get`, `spec-difficulty-ladder.md:354-355`),
`encounter-generator` (`Encounter.Build`, `spec-encounter-generator.md:60`), `delve-scope` (`rpg_delve_rooms`,
`parties_json`, `decisions_json`, `RpgStore.Delve.cs` — `spec-delve-scope.md:72-73, :79-88, :272`). Consumed later,
never gating: `dungeon-loot` (the `loot` consequence), `delve-quests` (outcome rows in the delve report),
`supplies-and-objects` (tag-bearing supplies), `wild-room` (`remembers`), `delve-stage` (the banner). Gate **G3**
(`party-dungeon-map.md:159`); coverage feeds **G4** (`:160`). Format: [standalone/spec-expeditions.md](../standalone/spec-expeditions.md).

## Objective

The in-game event generator over seedsmith event anchors: a pure resolver that, given a room the graph fixed,
the party standing in it, the sealed seed, the rung and the tuning, decides **which** event the room holds,
**what** an `unknown` room turns out to be, **which** outcome fires and **what** it does — every magnitude frozen
once through `Instantiator.TryInstantiate`, every consequence handed to the module that owns it, nothing chosen
by a model or a clock. No event catalog, deck or generator exists in `src/` (ideal `:193`); the only precedent is
the expedition band roll — the right stream discipline as four inline branches (`ExpeditionResolver.cs:106-125`).

Success looks like: a solo delve on autopilot walks eleven rooms, meets four events and one `unknown` that pity
turned into a cache, spends one `herbs` supply for a guaranteed outcome, loses spirit to a horror curio and gains
`nerve.unsettled` through attrition's ladder, and replays byte-identically; over 32 seeds no event id appears
twice in a delve; a domain whose deck cannot fill a room is refused at import with the archetype named, never at play.

## Locked anchors

- **Decision 6 (ideal §11.9 box, `party-dungeon-ideal.md:1674-1681`):** *"in game runtime don't use LLM … in game
  use the seed structure to generate random event/map/enemy"* — runtime generators *"are pure functions of seed
  structures and never call a model."*
- **Law 2 (§10, `:790-796`):** *"A graph, a deck draw or a slot fill is not an atom container … which event … reuse
  `SeededRng.DeriveStream` and `WeightedChoice` … as long as the structural roller never touches a magnitude."*
  **Law 3 (`:797-799`):** *"The LLM writes identity; deterministic code writes magnitude."*
- **§4.7 (`:475-479`):** *"No event may write a stat directly — it grants, scoped to the delve … and the grant is
  withdrawn at extraction."* `:486-489`: a dominant choice is *"priced by making the override a provision with a
  slot cost; a node that is always-take or never-take is a validator failure."*
- **§11.3 (`:1069-1079`):** *"Which event is a compositional draw, not an `Instantiator` call … `WeightedChoice` on
  `DeriveStream(seed, "dungeon:event:{r}:{c}")`; unknown-room pity rides `dungeon:unknown:{r}:{c}` … the chosen
  outcome's effect bundle is the `Instantiator` container … and nothing else."* `:1088-1093`: bind delve-long
  grants on `UniqueActor` with `source = "delve:{id}"`, withdraw by source — *"Recommend the second for v1."*
  `:1127-1131`: ≥1 `bad`-or-`mixed` and ≥1 `good`; no free Leave outside `story`; no event gates the boss.
- **R11 (§11.10, `:1757`):** pity is *"per party, matching per-party packs, hauls and routes"* (review §5 #11, `audit:304-305`).
  **S2-12 (`audit:222`):** `dropBand` is the one frequency vocabulary. **S2-14 (`:224`):** the banner is a band-2 layer.
- **Seed contract §1.4 (`:76-90`):** `kind` (`curio · encounter-event · shrine · trap · bargain · story`), `repeatScope`
  (`per-delve · per-domain · once-per-player`, *"every event repeats somehow"*), `eligibility` (twelve leaves *"plus the
  four `event-deck` adds"*, band arguments), `outcomes[] (2–4) {ordinal, dropBand, effects[]}`, `supplyOverride`,
  `chainRef`; `:88`: `effects[]` is *"the `Instantiator` container the importer builds; `container_id` DERIVED."*
- **Registries (`:134`):** *"No `events.dropBand.*` keys: outcome weights resolve through `bands.v1.json`'s `weightTable`"*
  — `1000 / 300 / 90 / 25 / 7` (`data/seed/items/_registry/bands.v1.json:453-459, :463-483`), *"a plain positive integer weight"* (`:462`).
- **Attrition (`:183`):** *"a horror curio outcome (`event-deck`, `resource.delta` on `spirit`) | the outcome's amount |
  `stackPerCurio`"*; `:204-206`: *"the deck owns the draw … a `watch` action's status is read by its eligibility through `HasStatus`."*
- **Determinism:** integer only, `SeededRng` never `System.Random`, no wall clock (`spec-turn-engine.md:76` via `spec-delve-graph-roll.md:41-43`).

## Design

### 1. Inputs

`EventDeck.Resolve(deck, room, party, seen, seed, rung, roomTheta, tuning) → EventResolution`, all read models
owned elsewhere: the **deck** (§2); the **room** — `DelveRoomFact (row, col, kind, archetypeId, baseBand,
partyRouteMask)` plus the archetype's `eventPool` and `climate`; the **party** — attrition's `PartyState` for the
entering `PartyIndex` (`members[]`: `pools`, `statuses[]`, `nerveStacks`, `downed`, `spec-delve-attrition.md:75-81`)
plus its pity counters and haul cell count from `parties_json`; the **seen sets** (§8); the sealed `ulong` **seed**;
the **`RungDef`** (`eventSeverityTier`, `unknownPityStepMultMilli.*`, `spec-difficulty-ladder.md:233, :236`);
**`RoomTheta`** (`Θ_room`, the `thetaContent` of every container roll); `DungeonTuning` and the weight table. The
player's **choice** (§6) arrives in a second pure step, `EventDeck.Answer(resolution, choice, …)`, with a persisted
decision between the two.

Output `EventResolution { EventId, Kind, Choices[], DrawnOutcomeOrdinal, Instance, Consequence, Banner, Warnings }`
— a record, never a write; the host (`RpgStore.Delve.cs`) applies it (§5's dispatch table, §8's rows).

### 2. Deck build and filters

**Reader.** The corpus reaches the runtime through the seed import path — `SeedScanner` + `SeedImportRunner` →
`RpgStore.ImportContent` (`SeedImportRunner.cs:47`; the class `tools/AtomImporter/Program.cs` now calls) — and Core
loads rows with a pure `EventCatalog.Load(rows, tuning)` in the `ConsumableCatalog.Load(defs, tuning)` shape
(`ConsumableCatalog.cs:62-65`): every band resolved to its integer at load, every eligibility tree compiled once with
`PredicateCompiler.TryCompile` (`PredicateCompiler.cs:156`), rejections naming the id, no I/O.

**Deck per domain** = the union of `eventPool` over the archetypes in the domain's `roomPalette` (seed contract
`:51`, `:70`) — *"the anchors the domain's palette reaches"*, and what §Testing's coverage measures. **Pool per
room** = `archetype.eventPool` ∩ four filters, in order, each a set operation with no draw:

1. **Kind fit** — `curio → curio`, `shrine → shrine`, `trap → trap`, `merchant → bargain`, `wild → story`, `rest →
   encounter-event` (ambush rows, §7), `unknown → any` after pity says *event*. Checked at **load**: a pool entry
   whose kind does not fit its archetype is a corpus refusal naming both ids (`:70`'s *"whose `kind` fits"*).
2. **Eligibility** — `compiled.Evaluate(ref facts)` over §6's facts; an absent tree is `Always` (`PredicateCompiler.cs:26`).
3. **Repeat scope** (§8) — `per-delve`: not in this delve's `seen`; `per-domain`: no `(playerId, domainId, eventId)`
   row; `once-per-player`: no `(playerId, eventId)` row.
4. **Recent cells** — no event whose `(kind, theme)` cell was drawn in the last `events.noRepeatRooms` rooms of
   *this party's* `route[]`. A hard filter made safe by preflight (§9): every archetype's pool holds more distinct
   cells than `noRepeatRooms`, so the filter cannot empty a pool the import accepted.

**Weights.** `WeightedOption<EventRow>(row, events.climateAffinity.{match|none|off}Milli)` — `match` when
`climateAffinity == room.climate`, `none` when climate-blind, `off` otherwise (`:82`: *"affinity weights, it never
gates"*). `Weight` is `int` (`WeightedChoice.cs:6`); ‰ of a relative weight fits.

### 3. Draw and streams

Every stream is `SeededRng.DeriveStream(seed, name)` (`SeededRng.cs:26-27`), one per step — the tick shape of
`ExpeditionResolver.cs:92, :106`. Root `dungeon:event:{r}:{c}`, reserved by `delve-graph-roll` (`:130-132`). Picks are
`WeightedChoice.Pick(options, rollSeed, streamName)` (`WeightedChoice.cs:25`), `rollSeed` the stream's first `NextULong()`
as `long`; weight ≤ 0 is skipped (`:31`); an empty list throws (`:36-37`) — rethrown here as `EventDeckRefusal` naming the room.

| Step | Stream | Draw |
|---|---|---|
| which event | `dungeon:event:{r}:{c}:pick` | `Pick` over §2's pool |
| which outcome | `…:outcome` | `Pick` over §5's shifted bands |
| the effect bundle | `…:effects` | `rollSeed` for `TryInstantiate` |
| a re-picked archetype (§4; §5 `encounter`) | `dungeon:unknown:{r}:{c}:archetype` · `dungeon:event:{r}:{c}:encounter` | `Pick` over the palette's `(kind, climate)` cell |
| unknown-node pity | `dungeon:unknown:{r}:{c}` | three `NextPerMille()` in registry order (§4) |
| ambush | `dungeon:event:{r}:{c}:ambush` | one `NextPerMille()` against `rest.ambushMilli`, then `:pick` on the rest pool |

An event resolves **once per room, at first entry**, by whichever party arrives first; a second party finds it
spent (`rpg_delve_rooms.event_id`, §8) — the stream is the room's. `events.offeredPerRoom` (starting 1) is the
count of `:pick` draws; a second would use `:pick:1` on the pool minus the first.

### 4. Unknown-node pity

An `unknown` room resolves to one of `unknownResolvesTo[]` — `cache · merchant · fight` (`spec-dungeon-registries.md:72`)
— or to an event. The Slay the Spire model (ideal §3.3 `:261-262`, marked *unverified* and kept as provenance,
`audit:419`): a base chance and a per-miss step per kind; a hit resets that kind; counters reset per delve.

```text
for kind in [cache, merchant, fight]:                              // registry order
    chance‰ = base(kind) + step(kind) · rung.unknownPityStepMultMilli.{kind} / 1000 · misses[party][kind]
    if NextPerMille() < chance‰ → resolve(kind); misses[party][kind] = 0; other kinds += 1; stop
else → event (§2–§3 on the unknown archetype's pool); every kind += 1
```

`base`/`step` are `nodes.unknown.pity.{kind}.{baseMilli,stepMilli}` (`:129`); the multiplier is the rung's economy
column. Counters live **per party** in `parties_json.pity{}` (R11), `int`, reset at entry. `chance‰` is `long` and
may pass 1000 — certainty, a bounded ratio doing its job, **not a cap** (comment in code). A resolved kind
re-picks an archetype of that kind from the palette cell `(kind, climate)` on `dungeon:unknown:{r}:{c}:archetype`
— an `unknown` archetype has `encounterRef: none` (seed contract `:69`) — so the fight is the re-picked
archetype's `encounterRef` through `Encounter.Build` at this room's `Θ_room`, the cache is `dungeon-loot`'s
`dungeon-room` draw on the re-picked `cache` archetype, the merchant prices on this room's Θ (N6). An empty cell
throws, the roller's own refusal (`spec-delve-graph-roll.md:114-116`). The resolution is persisted (§8): miss
counters are history the seed alone cannot rebuild.

### 5. Outcomes — band → weight → atoms → consequence

**Severity.** `rung.eventSeverityTier` (0 through `nightmare`, 1 from `hell`, `spec-difficulty-ladder.md:107`) is StS
Ascension 15 — *"Many events have less positive outcomes and more severe consequences"* (`docs/research/genre-mechanics/08-endless-scaling-meta-progression.md:473`,
a **rule** row). It shifts **band indices**, never numbers: for tier `t`, a `good` outcome's `dropBand` moves `t`
steps toward `exceptional`, a `bad` outcome's `t` steps toward `staple`; `mixed`/`nothing` stay. The index is
bounded by the five-member enum — an ordinal rail, exempt and commented. Weight = `weightTable[band]`; `Pick` normalises.

**Forced outcome.** On `use:{tag}` (§6) no `:outcome` draw happens: the outcome is the row `supplyOverride` designates
(the importer records the forced ordinal — DD's curio shape, Iron Maiden `40/20/13.3/6.7/20` by hand, *"100 % loot
with Herbs"*, ideal §3.1 `:224`), and the spend is `supplies-and-objects`' `SupplyUse.Use(…, UseContext.Curio)` over
the party's **pack** — `loot-pack` decrements the stack as a `pack.drop`; `rpg_item_stock` (`RpgStore.Items.cs:96`) is
the armoury and stays home. `HoldsStock` facts are loaded from the pack; the leaf is the check, the decrement the
cost (`:1076`). The answer is a `talk` row and the spend a `supply.use` row (supplies §3) — two entries, one transaction. `:outcome` is still derived and discarded, so `:effects` never moves with the answer.

**Effects.** The outcome's container goes through `Instantiator.TryInstantiate(container, lookupAtom, lookupAffix,
rollSeed, thetaContent: Θ_room, tuning, out instance, origin, catalogRevision)` (`Instantiator.cs:98-107`) —
`contentScale` once, byte-identical on replay. `InstanceOrigin` has no delve member; **filed on the effect-atom
program** (`Delve`, or v1 reads `Drop` with the binding source carrying scope). A rejection is a refusal (§9). The
instance's atoms dispatch **by kind to their owners**:

| Atom kind | Where it lands | Write owner |
|---|---|---|
| `resource.delta` on the six pools | `ActorResourcePools.Add(id, amount)` per targeted member (`ResourcePoolState.cs:93-106`, attrition §2), then `ExhaustionPolicy.Sync`. **A negative spirit delta also adds `attrition.nerve.stackPerCurio` and calls `NervePolicy.Sync`** — never a `nerve.*` id from here | attrition `PartyState.Write` |
| `status.apply` (not `nerve.*`) | appended to `members[].statuses[]` as `BattleStatusSpec`, riding into the next fight as `InitialStatuses` (`spec-delve-battle-profile.md:171-172`); a `nerve.*` id in an event container is an **import refusal** | attrition's carry |
| `shield.grant` | `members[].shield` (`BattleInnateShield`) | attrition's carry |
| `stat.derived` (delve-long buff) | a binding on the member's `UniqueActor`, `source = "delve:{delveId}"`, withdrawn at `CloseDelve` by source — the `ReconcileUniqueEquipmentAtomBindingsUnlocked` shape (`RpgStore.UniqueActors.cs:665`); `OwnerKind` stays eight (`OwnerScope.cs:20-30`) | `RpgStore.Delve.CloseDelve` |
| `ui.present` (`op:banner`) | the host's `IUiPresentSink.ShowBanner(bannerId, durationMs)` (`UiPresentSink.cs:18-33`) — a `DelveUiPresentSink` appending to the room's presentation list for `delve-stage`; the lawn's `EffectBag.ExecPresentUi` (`EffectBag.cs:697`) is off this path and battle has no sink (no hit under `Core/Battle/`) | this module |
| any other kind | import refusal — the event vocabulary is the five above | — |

**The `resource.delta` executor beyond `hp` is this module's build** (map `:119`): the lawn executor routes
`ApplyResourceDelta` into a `DamagePacket` (`EffectBag.cs:474-489`), battle consumes it as FA10 on a ptr
(`BattleEffects.cs:134-140`), and `AtomKindRegistry.cs:548-550` records *"only `hp` executes until E28 fix #1 ships."*
Between rooms there is no ptr: `DelveResourceDelta.Apply` reads the frozen `channel` and `amount` from the
`InstanceRow`, widens to `long`, calls `Add` on the pool named — the loop over `DerivedStatChannels.ResourceIds`,
never a hand list; it runs only outside a fight, and in-fight `hp` stays FA10's.

**Targets.** The atom's `target` param (`AtomKindRegistry.cs:554`) admits `party` (every standing member, default)
or `one` (the first standing member in `members[]` order); a downed member is touched only by a `revive`-class supply.

**Consequence.** Atoms carry effects, not drops or fights. **One field is filed on `dungeon-seed-contract` §1.4:**
`outcomes[].consequence`, VALIDATED, closed `none · loot · encounter · scout`, `none` legal and the reading for every
row until it lands. `loot` → `dungeon-loot`'s `dungeon-room` source with the room's `Θ_content` and the domain's
`lootBinding[room.kind]` (map row 10; `LootRequest` `LootPipeline.cs:20-28`, `LootSourceRow.ContentLevel`
`DropTableModel.cs:52-57`) — **never a private table**; `LootCorrelation.Derive` throws on an unknown kind today
(`LootPipeline.cs:91`), which is why the kind is `dungeon-loot`'s to add. `encounter` → a `fight` archetype re-picked on
`dungeon:event:{r}:{c}:encounter`, then `Encounter.Build(anchor, roomTheta, climate, raid, rung, seed, corpus,
tuning)` (`spec-encounter-generator.md:60`) and `DelveBattle.Run`; effects apply **before** the fight. `scout` → the
party's `scoutSightLanes` radius for this room (`spec-delve-scope.md:200-201`). Door seal/reveal and a quest hook are
**seams only**: the delve resolver is the one writer of `GateKeyId` (`spec-delve-graph-roll.md:169-171`), and
`delve-quests` reads the delve report, which gains `events[] (roomId, eventId, outcomeOrdinal, choice)` — the hook is that row.

### 6. Choices and predicates

**Choices, v1.** The approved anchor has no `choices[]` — one interaction, a weighted outcome, an optional forced
path (`:85-89`). An event presents **one to three** choices from a fixed verb set: `use:{tag}` (present iff
`supplyOverride ≠ none`; eligible iff `HoldsStock(tag-bearing supply) ≥ 1`), `interact` (always), `leave` (only
`kind: story`; `chainRef` continues in a later room of the same kind). **Autopilot picks the first eligible in the
order `use · interact · leave`** — a supply carried in is spent when it applies. The answer is appended to
`decisions_json` as `{seq, kind: "talk", partyIndex, payload: {surface: "event", eventId, choice}}`
(`spec-delve-battle-profile.md:141-145`; `talk` is shared with `wild-room`, `payload.surface` discriminates).
Per-choice predicate trees are a seed-contract widening — **ask first**.

**Leaves that exist** (`PredicateNode.cs:19-33`, twelve). Usable unchanged: `HasStatus` (Text → interned bit,
`PredicateCompiler.cs:178`, `FactReader.cs:79-80`; `nerve.{unsettled,shaken,afflicted}` and `watch` are the ids events
read), `HpBelowMilli`/`HpAboveMilli` (Value 0..1000, `:129-131`), `ElementIs` (climate), `HoldsStock` (Text stockId,
Value minQty ≥ 1, `:133-138`). Refused by the event validator as meaningless here: `SideIs` (ordinals
`plant/zombie/bullet`, `:192-197`), `TypeIdIs/In`, `ActorIsKiller`, `IsMindControlled`, `RowIs`/`ColIs`. **Bands on
the anchor, `Milli` in the tree** (`:84`): the importer maps `hpBand: low|half|high` → `HpBelowMilli(bands.hpBand.low.milli)`
/ `HpBelowMilli(half)` / `HpAboveMilli(high)`.

**Real gaps — four leaves, each a reviewed code change** (`PredicateNode.cs:4-5`: *"each needs a reader on
`FactReader`"*): `BandIs` (Value: the room's `DangerBand`), `HaulAtLeast` (Value: occupied pack cells, an `int`
count), `RoomKindIs` (Value: the `RoomKindCatalog` ordinal), `PartyDownedCount` (Value: minimum downed). Each adds a
`LeafId`, a `ValidateLeaf` arm (`:110-140`), a `BuildLeaf` node (`:169-189`) and an `EntityFacts` field
(`FactReader.cs:25-38`) with a reader. **Facts for an event**, built once by the host: `Self` = the party —
`HpMilli` the **lowest** standing member's hp‰, `StatusMask` the **union** over members, `Stock0..3Qty` interned
supply quantities; `Target` = the room — `ElementId` climate, `Row`/`Col`, `Band`, `RoomKind`. Two reader limits
become import rules: ≤ **four distinct stock ids** per tree (`FactReader.cs:85-87`) and ≤ 64 interned status ids (`:80`).

### 7. Ambush and curio seams

**Ambush.** `RestResolver.Resolve` calls `EventDeck.DrawAmbush(room, party, seen, seed, rung, roomTheta, tuning) →
AmbushDraw { Ambushed, EventResolution? }` and sets `RestOutcome.Ambushed` (`spec-delve-attrition.md:204-206, :422`).
`NextPerMille() < rest.ambushMilli` on `dungeon:event:{r}:{c}:ambush` (DD's 33 % night ambush, ideal `:1334`; 330‰ per
attrition `:292`); on a hit, §2–§3 over the **rest archetype's** pool restricted to `encounter-event`, every ambush
row carrying `Not(HasStatus watch)`. **The one designed empty-pool case:** a hit whose *eligible* pool is empty is
*no ambush* plus a warning row — the pool was non-empty before eligibility (preflight proves it per rest
archetype). Its `consequence` is `encounter` at the rest room's `Θ_room`, formation `pack`; an ambush is a fight, not a drain.

**Curio → attrition, exactly.** Per member targeted by a `resource.delta` on `spirit` with a negative frozen
amount: `pools.Add("spirit", amount)`; `nerveStacks += attrition.nerve.stackPerCurio`; `NervePolicy.Sync(member)` —
three calls on `PartyState`, in that order, one transaction with the room write. A positive delta (a shrine) is
`pools.Add` alone and the ladder re-resolves on `Sync` (`:185-186`). The deck never reads a stage name and never
writes a `nerve` family id.

### 8. Repeat scope and persistence

| Scope | Set | Where it lives | Reset |
|---|---|---|---|
| `per-delve` | `seen` = every `rpg_delve_rooms.event_id` of this delve | **filed on `delve-scope`:** `rpg_delve_rooms` gains `event_id`, `resolved_kind`, `resolved_archetype_id` (`TEXT NULL`) — roll outcomes history cannot rebuild, the `key_for_lane_id` argument (`spec-delve-scope.md:84`) | never in-delve; a `many` re-entry rewrites the rows (`:222-224`) |
| `per-domain` | `(playerId, domainId, eventId)` | **filed on `delve-scope` (its "ask first: a third delve table", `:322`):** `rpg_delve_event_seen(player_id, scope, scope_key, event_id, delve_id)` — it must outlive the graph rows and the `once` archive | never |
| `once-per-player` | `(playerId, eventId)` | the same table, `scope_key = ''` | never |

Every scope is **at least per-delve**, so the invariant is absolute: *no event id twice in one delve*. Rows are
written with the room's `event_id` at the **draw**, not at extraction — a wipe does not un-see an event. Pity
counters live in `parties_json` (`:72`); the answer in `decisions_json`. On load the resolution is rebuilt from
`(seed, room, party state at entry, seen)` and asserted equal to the persisted `event_id` — the roller's
validate-on-load posture (`spec-delve-graph-roll.md:17-19`).

### 9. Refusals and preflight

Every refusal is a thrown `EventDeckRefusal` naming the room, the archetype and the filter that emptied the pool
— no flag, no fallback, no blank room. At play: an empty pool after §2 (unreachable after preflight; still
thrown); a `TryInstantiate` rejection (propagated with its reason); `consequence: loot` on a kind the domain's
`lootBinding` has no table for; `consequence: encounter` on an empty palette cell; a persisted `event_id` the replay
does not reproduce.

**Preflight** — `EventDeckPreflight.Run(corpus, domains, supplies, tuning)`, model-free, run by the domain importer
and the tests, refusing the **domain** with the row named: every `eventPool` id exists and fits (§2 rule 1); every
tree compiles on event-legal leaves, ≤ 4 stock ids, known status ids; ≥ 1 `good` **and** ≥ 1 `bad`-or-`mixed` per
event; `nothing` only on `story`; every `supplyOverride` tag is carried by ≥ 1 supply (`OverrideTagUnsupplied`, seed
contract `:149`, repeated so the runtime never trusts the tool); `chainRef` acyclic, same kind; no `nerve.*` id or
non-event atom kind in any container; `> events.noRepeatRooms` distinct cells per archetype pool; ≥ 1
`encounter-event` per `rest` archetype; `RoomKindIs boss` refused (*"no event may gate the boss"*, `:1129-1130`).

### 10. Determinism

Pure over `(deck, room facts, party state at entry, seen sets, seed, rung, Θ_room, tuning)`: same inputs ⇒
byte-identical `EventResolution`. Pools and sets enumerate in ordinal id order; every draw is a named stream; the
instance reproduces by `Instantiator`'s own contract (`Instantiator.cs:84-85`); the answer is player input persisted
before it is applied. No `System.Random`, `DateTime`, `Environment.TickCount`, store or I/O under `Core/Delve/Events/`;
`StatusRuntime.Apply(input, rng, now)` (`StatusRuntime.cs:189`) is the next fight's call with the battle's virtual `now`, never this module's.

## Tunables

All in `data/tuning/dungeon.v1.json`; schema and T5 loader are `dungeon-registries`' (`:164-172`) — new keys enter
there and through `publish.py`. Every value is a starting shape.

| Key | Unit | Owner | Starting shape |
|---|---|---|---|
| `nodes.unknown.pity.{cache,merchant,fight}.{baseMilli,stepMilli}` | ‰ long | registries `:129` (read; consumer row corrected to this module, `spec-delve-graph-roll.md:234`) | 20/20 · 30/30 · 100/100 (StS 2 %/+2, 3 %/+3, 10 %/+10) |
| `difficulty.rungs[].eventSeverityTier` · `.unknownPityStepMultMilli.*` | tier int · ‰ long | ladder `:233, :236` (read) | 0, 1 from `hell`; 1000 identity, 2000 on `very-easy` |
| `events.noRepeatRooms` · `events.offeredPerRoom` · `rest.ambushMilli` · `attrition.nerve.stackPerCurio` | rooms · draws · ‰ long · stacks | registries `:134, :140`; attrition `:294` (read) | 3 · 1 · 330 · 1 |
| **new** `events.climateAffinity.{match,none,off}Milli` | ‰ relative weight, long | this module | 1000 · 1000 · 500 |
| **new** `bands.hpBand.{low,half,high}.milli` | ‰ of max hp, long | this module | 250 · 500 · 750 |
| **not keys** | | | `events.dropBand.*` (items registry owns it, `:134`); `events.weightBand.*` (dead, S2-12); a severity ‰ (the tier shifts an index) |

**Structural, commented in code:** the five-member `dropBand` index rail (§5); the reader's four stock slots and
64 status bits (`FactReader.cs:80, :85-87`); §4's `chance‰ ≥ 1000` is certainty, not a clamp.

## Numeric types

| Quantity | Type | Why |
|---|---|---|
| frozen atom amounts, pool deltas, shield capacity | `long` | magnitudes `P(Θ_room)` reaches; widened before any multiply; `Instantiator` divides once |
| `Θ_room`, `DangerBand`, severity tier, band indices, pity counters, `noRepeatRooms`, stock quantities, downed count, pack cells | `int` | bounded ordinals, counters, cells |
| every `*Milli`; §4's `chance‰` | `long` | `base + step · mult / 1000 · misses` in `long`; the `int` roll is widened to compare |
| `WeightedOption.Weight` | `int` | the library's (`WeightedChoice.cs:6`); `1000/300/90/25/7` and the affinity ‰ fit |
| `rollSeed` | `ulong` → `long` | `NextULong()` reinterpreted for `TryInstantiate` (`Instantiator.cs:102`) |

No `float`/`double` here; `StatusApplyInput.BaseMagnitude` (`StatusRuntime.cs:69`) is the status layer's, never computed here.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Delve.Events"                                  # goldens, properties, refusals, coverage
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Battle|FullyQualifiedName~Expedition|FullyQualifiedName~Predicate"   # hashes and E3 untouched
dotnet test tests\FusionRpg.Data.Tests  --filter "FullyQualifiedName~Delve"
.\scripts\guard-dal.ps1 ; .\scripts\guard-funnel-delta.ps1
python scripts\audit-magic-numbers.py --domain dungeon ; python scripts\audit-overflow.py
python -m seedsmith dungeon audit                                                                                    # runs EventDeckPreflight
```

## Structure

```
src/FusionRpg.Core/Delve/Events/
  EventRow.cs · EventCatalog.cs      Load(rows, tuning): bands → ints, trees compiled, §9 import rules
  EventDeck.cs                       Build(domain, corpus) → per-archetype pools; Resolve(...); Answer(...)
  EventFilters.cs · EventDraw.cs     kind fit · eligibility · scope · recent cells (pure sets); stream names, picks
  UnknownPity.cs                     §4 — per-party counters in, resolution out
  OutcomeResolver.cs                 severity shift → weights → TryInstantiate → dispatch plan → consequence
  DelveResourceDelta.cs              the out-of-fight resource.delta executor over ResourceIds
  EventChoices.cs · EventFacts.cs    the verb set, autopilot = first eligible; party/room EntityFacts builder
  AmbushDraw.cs · EventDeckPreflight.cs · EventCoverage.cs · EventDeckRefusal.cs · DelveUiPresentSink.cs
src/FusionRpg.Core/Effects/Atoms/PredicateNode.cs, FactReader.cs, PredicateCompiler.cs → BandIs, HaulAtLeast, RoomKindIs, PartyDownedCount
src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs    → event_id/resolved_* columns; rpg_delve_event_seen; the apply transaction; withdraw-by-source at CloseDelve
src/FusionRpg.Server/DelveEventEndpoints.cs     → POST …/rooms/{id}/answer — the steered party's choice
tests/FusionRpg.Core.Tests/Delve/Events/ · tests/FusionRpg.Data.Tests/Delve/
```

## Code style

Static pure resolvers with seed and tuning as parameters — the `WaveCatalog.Enemies`/`DelveGraphRoll` voice; no
logging, `Warnings` is the record; rejections name the room and the filter.

```csharp
public static EventResolution Resolve(EventDeck deck, DelveRoomFact room, PartyState party, SeenSets seen,
    ulong seed, RungDef rung, RoomTheta theta, DungeonTuning tuning)
{
    var pool = EventFilters.Apply(deck.PoolFor(room.ArchetypeId), room, party, seen, tuning);   // ordinal id order
    if (pool.Count == 0) throw new EventDeckRefusal(room, "empty pool after filters — never a blank room");

    string S(string step) => $"dungeon:event:{room.Row}:{room.Col}:{step}";
    long Seed(string step) => (long)SeededRng.DeriveStream(seed, S(step)).NextULong();

    var ev = WeightedChoice.Pick(pool.Select(e => new WeightedOption<EventRow>(e, AffinityWeight(e, room, tuning))).ToList(),
        Seed("pick"), S("pick"));
    var outcome = WeightedChoice.Pick(ev.Outcomes.Select(o => new WeightedOption<EventOutcome>(o,
        tuning.DropBandWeight(Severity.Shift(o.Ordinal, o.DropBand, rung.EventSeverityTier)))).ToList(),
        Seed("outcome"), S("outcome"));

    var rejection = Instantiator.TryInstantiate(outcome.Container, deck.LookupAtom, deck.LookupAffix,
        Seed("effects"), thetaContent: theta.Theta, deck.PowerTuning, out var instance);     // Θ_room, once
    if (!rejection.IsOk || instance is null) throw new EventDeckRefusal(room, ev.EventId, rejection);

    return new EventResolution(ev.EventId, ev.Kind, EventChoices.For(ev, party), outcome.Ordinal, instance,
        outcome.Consequence, ev.BannerId, Warnings: Array.Empty<string>());
}
```

## Testing strategy

- **Goldens:** one resolution per (domain tier × `eventKind`) — 4 × 6 = 24 — hashed over `EventResolution` in
  canonical order against a fixture corpus, blessed once; one unknown-room golden per `unknownResolvesTo` kind.
- **Property, 256 seeds per domain:** no `eventId` twice in one delve; a `per-domain` event never reappears for the
  same `(playerId, domainId)` across three delves; the ordinal is one the anchor lists; the instance reproduces over
  `(container, revision, seed, Θ_room)`; `use:{tag}` yields the forced ordinal and the same `:effects` bytes.
- **Pity converges:** P(no cache in k unknown rooms) < 1 % within the k the shapes predict; a hit resets exactly one
  counter; two parties' counters move independently (R11). **Severity:** tier 1 moves a `good` `staple` to `frequent`
  and a `bad` `frequent` to `staple`; tier 0 is byte-identical to the corpus weights.
- **Empty pool refuses:** a pool emptied by scope throws `EventDeckRefusal` naming room and filter; the preflight
  rejects the same corpus before any delve exists. **Autopilot = first eligible:** holding the tag → `use`; else
  `interact`; `leave` only on `story`; the answer is a `talk` row with `surface: event`.
- **Curio seam:** a −spirit outcome moves `pools["spirit"]`, adds `stackPerCurio`, and the live `nerve.*` id changes
  only through `NervePolicy.Sync` — a counting fake sees zero direct `nerve.*` applies. **Ambush:** a hit with no
  watch → `encounter` at the rest room's Θ; all on watch → no ambush plus one warning; a miss → `Ambushed = false`.
- **New leaves:** each compiles, validates its arg, reads its fact; a fifth stock id and an unknown status id are
  import refusals; the E3 and `PredicateCompiler` suites are unchanged.
- **Coverage (G4 input):** `EventCoverage.Report(domain, rung, 32 seeds)` — distinct `(eventKind, outcomeOrdinal)`
  cells per domain tier against the budget row (`_plan/budget.v1.json`, seed contract §7).
- **Untouched:** the four battle hashes, the 32-seed sweep, the four expedition tier hashes and the world goldens run
  in the same command — no `BattleSetup` or `WorldState` field is emitted. **No clock, no `System.Random`:** a guard
  test over `Core/Delve/Events/` (the `spec-turn-engine.md:138` scan shape).

## Boundaries

- **Always:** the draw on the reserved streams, one per step; every magnitude through `TryInstantiate` at `Θ_room`;
  every consequence to its owner (`dungeon-loot`, `Encounter.Build`, attrition's `PartyState`); scope rows with the
  room's `event_id` in one transaction; the answer persisted before it is applied; refuse with the room named.
- **Ask first:** per-choice predicate trees (`choices[]`); a `seal`/`reveal` consequence that writes a lane; a
  second `offeredPerRoom` draw; a `Delve` `OwnerKind` instead of the source binding; a fifth `consequence` value.
- **Never:** a number chosen by the model — bands in, integers out; a private drop table or a drop outside
  `LootPipeline`; a direct `nerve.*` grant bypassing attrition's ladder; a wall clock or `System.Random`; a blank
  room on an empty pool; a stat write — an event grants; `weightBand` or any `*weight*`/`*chance*` stem on an
  anchor; a `float` magnitude; SQL outside `FusionRpg.Data`; an in-fight HP change that bypasses FA10.

## Success criteria

1. Twenty-four goldens blessed; the 256-seed sweep green on the six first-ship domains. 2. Zero repeated event ids
per delve over the sweep; `per-domain`/`once-per-player` honoured across delves. 3. Every §9 refusal has a named
throwing test; the preflight rejects each red fixture and accepts the shipped corpus. 4. Autopilot deterministic and
equal to *first eligible*; a steered answer replays from `decisions_json`. 5. Attrition's counting fake sees spirit
and stacks move through `PartyState` only. 6. Coverage meets the budget row (G4); battle, expedition and world hashes
byte-identical. 7. `guard-dal`, `guard-funnel-delta`, the no-clock guard, `audit-magic-numbers --domain dungeon` green;
`audit-overflow.py` adds no critical.

## Interface exposed to dependents

| Member | Returns | Consumer |
|---|---|---|
| `EventDeck.Build(domain, corpus, tuning)` · `Resolve(...)` · `Answer(resolution, choice, ...)` | `EventResolution { EventId, Kind, Choices[], DrawnOutcomeOrdinal, Instance, Consequence, Banner, Warnings }` | `RpgStore.Delve` (the apply transaction); `delve-stage` (choices and banner, band 2) |
| `UnknownPity.Resolve(room, partyPity, seed, rung, tuning)` | `UnknownResolution { Kind: cache\|merchant\|fight\|event, ArchetypeId?, NextPity }` | `RpgStore.Delve`; `dungeon-loot` (a resolved cache); `encounter-generator` (a resolved fight) |
| `AmbushDraw.Draw(room, party, seen, seed, rung, theta, tuning)` | `AmbushDraw { Ambushed, EventResolution? }` | `delve-attrition` `RestResolver` |
| `Consequence == loot` · delve report `events[] (roomId, eventId, outcomeOrdinal, choice)` | a `dungeon-room` request `(roomId, Θ_content, lootBinding table)` · read model | `dungeon-loot` · `delve-quests` (`gather-curio-kind`), `wild-room` (`remembers`) |
| `DelveResourceDelta.Apply(instance, members, tuning)` · `EventDeckPreflight.Run` · `EventCoverage.Report` | pool deltas + nerve stacks through `PartyState` · refusals · `(eventKind, outcomeOrdinal)` cells | `supplies-and-objects` (a supply used at `curio`/`rest` is the same executor) · `domain-catalog` importer, `dungeon audit`, G4 |
| **Filed asks** | `outcomes[].consequence` (seed contract §1.4); `event_id`/`resolved_*` columns + `rpg_delve_event_seen` (delve-scope); `InstanceOrigin.Delve` (effect-atom); two tuning blocks (registries) | those modules |

## Drift found this session (report, not fixed here)

- **`AtomImporter` is a tool, not a Core class:** `tools/AtomImporter/Program.cs` calls `SeedScanner`/`SeedImportRunner`
  (`SeedImportRunner.cs:47`, `SeedScanner.cs:6-10`); `grep "class AtomImporter" src/` is empty. The map row's *"seed →
  rows via `AtomImporter`"* means the seed import path; the Core reader is a `Load(rows, tuning)` catalog (§2).
- **`PredicateNode.cs:11-18` is not a party-dungeon comment** — `:7-17` is the `HoldsStock` note, already corrected
  2026-09-05 (`:12`); the map's propagation row (`:82`) is done. `HoldsStock` is `:32`, not `:30` (seed contract `:84`);
  the enum spans `:19-33`, not `:17-31` (ideal `:1049`).
- **`UseContext` has four members** (`Menu · Dispatch · Battle · Lawn`, `ConsumableDef.cs:43-56`); attrition `:150-151` lists three.
- **`repeatScope` has no "repeatable" member** (seed contract `:83`); **the anchor has no `choices[]` and no
  loot/encounter field on an outcome** — §6 ships the fixed verb set, §5 files `outcomes[].consequence`.
- `docs/research/genre-mechanics/` carries no StS `?`-room or DD curio numbers; those are the ideal's §3.1 `:224` and
  §3.3 `:261-264` (*unverified*, kept as provenance per `audit:419`); the number used from that folder is the A15 rule row.

## Design-gate checklist

```
[x] Subsystems: effect atoms (predicate tree, Instantiator, kind registry, ui.present), resource pools / attrition,
    status (nerve via ladder only), battle RNG streams, loot pipeline (consumer), delve store, seedsmith event
    corpus, tunables, party dungeon.
[x] Read this session, in order: party-dungeon-map.md (row 9, G3/G4, external deps, dependency direction); the
    eight APPROVED specs in full; ideal §3.1/§3.3, §4.7, §10, §11.3, §11.5 (:1289-1338), §8 box, §11.9 box, §11.10;
    audit §1(h)-(i), S1, S2 (S2-12, S2-14), §4 (D14, G8/G9, R-series), §5 #11, §6 :337, §9 :419; spec-expeditions.md
    (format); DESIGN-GATE §5. decisions.md :113-116 present; none locks a deck shape.
[x] Every code claim cites file:line opened this session (PredicateNode, PredicateCompiler, FactReader, AtomCompiler
    :283, AtomKindRegistry, AtomKind, Instantiator :98-107, WeightedChoice, SeededRng, ExpeditionResolver :92/:106,
    StatusRuntime, ConsumableDef, OwnerScope, EffectBag, BattleEffects, UiPresentSink, LootPipeline, DropTableModel,
    RpgStore.UniqueActors :713, RpgStore.Items :96/:302, SeedImportRunner, ConsumableCatalog, bands.v1.json,
    08-endless-scaling :473); drift reported above. Verified against CODE, not comments: "only hp executes" is the
    registry comment AND the executor path; AtomImporter's absence is a grep of src/; the leaf list and the four stock
    slots are the enum and the struct. Surrounding sections read for every quoted rule (§11.3, §4.7, attrition §4-§5,
    seed contract §1.4 with §1.2/§3, the registries events row).
[ ] Constraints not tested — nothing was run; this spec changes no code. "Hashes untouched" is argued from the module
    emitting no BattleSetup/WorldState field; the first build task is the proof.
[x] No §2 invariant contradicted: no injector, no private curve, no magnitude in a seed, no second roll SDK, no cap
    (the two rails are bounded ordinals and say so), tunables in data. Two readings added and named: severity shifts a
    band index (§5); the ambush's post-eligibility empty pool is "no ambush" (§7).
[x] Propagations landed 2026-09-05 (verification pass): seed contract §1.4 gains `outcomes[].consequence` and the
    `:32` cite; delve-scope gains the three columns and `rpg_delve_event_seen`; registries gains the two tuning
    blocks and the pity consumer note; attrition :150 corrected; the map row names the seed import path;
    `InstanceOrigin.Delve` and `status.clear`-on-`OnActivate` are filed on effect-atom-map.md §19.
```
