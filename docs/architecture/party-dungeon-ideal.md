# Party dungeon (the Delve) — the ideal

**Status:** idea phase, 2026-09-05. **Not a spec. No build authorized.** No module ids, no build
order, no acceptance criteria. This exists to be argued with and cut down before it becomes a
capability map. **Part II (§11, same day) enriches the fourteen sub-mechanisms the owner listed
as seedsmith seed generators paired with in-game concrete generators**; its corrections to Part I
are folded in where they land and marked *(Part II)*. **All sixteen owner decisions were closed on
2026-09-05** — see the ✅ boxes under §8 and §11.9; where the owner chose against a recommendation,
the box wins over the prose above it. **Reviewed 2026-09-05 by a six-lens structured review —
record at [party-dungeon/audit-2026-09-05.md](party-dungeon/audit-2026-09-05.md).** Verdict: the
mechanics are ready for `/spec`; the capability map waits on four `decisions.md` rows (sixth stage
`delve`, the store-side shape of decision 5, two ADRs for spirit-as-nerve, the five-slot lock of
decision 16), one retraction (decision 11's A10 claim — `siege-board` already implements it), and the
owner forks in the record's §5. The sentence-level corrections the review found are applied below and
marked *(review)*; where a ✅ box's premise was wrong the box keeps the owner's decision and gains a
footnote.

**Program id:** `party-dungeon`. Player-facing word: **the Delve** (a *domain* is the place, a *delve*
is the run). Sibling of `base-defense-ideal.md` and `world-graph-ideal.md` §13, which already
sketches this feature in one paragraph — this document gives that paragraph its full shape.

**Owner's ask (2026-09-05), verbatim in substance:** a party mode where the commander brings five
unique demons into a dungeon/domain split into many rooms with a lot of event generation, hunting
relics, treasure and a boss. Darkest Dungeon, Persona and Shin Megami Tensei as the reference feel.
The most hardcore mode in the game — high risk, high reward. Party slots tunable, with three raid
modes: 1 party, 2 parties, 4 parties.

---

## 0. The principles this design is built under

Restated here, not linked, because a downstream session reads this document and not its links.

1. **Every RPG feature lives in the RPG layer. It is never built by changing what PvZ is.** The
   Delve is a web-mode, server-authoritative feature. It never asks PvZ to represent a party, a room,
   a relic or a boss. PvZ may later *enrich* it (a live lawn as one kind of room) but never *gate* it.
2. **Standalone-first.** Every RPG feature must be fully playable and CI-provable with the game
   closed. The Delve is playable with the game closed from its first build.
3. **Server-authoritative, seeded, replayable.** Web outcomes resolve server-side with recorded seeds
   and correlation-idempotent commands. A played Delve's determinism is `(setup, seed,
   decision-trace)` — the same rule the battle kernel already carries for live sessions. No wall
   clock inside the resolver; virtual time only.
4. **One power ladder.** Contests read `Θ` (linear, difference-based); magnitudes read `P(Θ)`. The
   inventory of power-shaped scales in `ssot-power-scale.md` §10 is **closed** — this design adds no
   private `f(depth)`. Dungeon depth is expressed through a term `Θ` already contains (§4.3 below).
5. **The balance surface is data.** Every number this document introduces lives in
   `data/tuning/dungeon.v{n}.json` or a `data/seed/dungeon/` seed. A missing tunable is a load
   rejection, never a default.
6. **No hard progression ceilings.** No cap on depth, on haul, on raid count, on relic power.
   Absolute bounds derive from the arithmetic and throw. Structural limits (room counts per graph,
   recursion) say so in a comment. **No stamina, no daily limits, no wall-clock pacing** — this
   repo has already ruled twice that a clock is a business model, and this game has none.
7. **Every faucet names its sink in the same change** (`economy-principles.md` P1), a faucet that
   scales needs a sink that scales on the same read (P2, PS-5), and a stock needs two competing
   sinks on different horizons (P6). The Delve is a faucet. Its sinks are named in §6.
8. **Magnitudes are `long`.** Widen before multiplying, divide by 1000 last, overflow throws.
9. **Seed → concrete → per-player.** Seedsmith emits seeds (identity, ordinals); the runtime rolls
   concrete objects per player through the one shared SDK, `Instantiator.TryInstantiate`. This
   design never writes a second roll implementation. The LLM writes identity; deterministic code
   writes magnitude.
10. **A game is a stage with layers, not a document with pages** (GG-1). The Delve is a place the
    player *acts in*, so it is a stage, and every panel opens over it.

---

## 1. What this is

You pick a **domain** — a lair, a rift, a vault under a sector — and a **raid**: one party of five
bound demons, or two parties, or four. You provision the raid from your treasury, then descend.

The domain is a **graph of rooms** rolled from a seed the moment you commit. You see the rooms one
lane ahead, the way a legion sees the sector next door. Every room is one thing: an ordinary fight,
an elite fight, a cache, a curio you can meddle with, a wild demon you can bargain with, a shrine, a
rest, a merchant, a trap. The deepest room is the **boss**. Between rooms your demons carry their
wounds, their hunger and their nerve with them — nothing resets until you rest, and rest is a room
you have to find.

Everything you take is **unbanked** until you walk out. An extraction is available from any room,
always, but leaving early forfeits the boss and every room past it. A wiped party drops its haul in
the dark; the domain keeps it. A downed demon is out for the rest of the delve and comes home
wounded — recovering takes days, and its loyalty remembers the loss.

With two or four parties you split the graph: each party takes a route, clears its own rooms, and
the routes converge on the boss. The boss fight is the rendezvous — every standing demon from every
party, against a boss and its retinue sized to the raid. A raid can carry more out, and it has more
to lose.

Deeper domains pay more because they *are* more: the room's danger is the same number the map
already uses for a sector, and every magnitude in the game — enemy health, relic power, soul yield —
reads off that one number. There is no separate "hardcore multiplier". Going deeper is the multiplier.

---

## 2. What already exists — the three buckets

Surveyed 2026-09-05 across `src/FusionRpg.Core/{Battle,Expeditions,World,Items,Demons,Effects,
Power}`, `src/FusionRpg.Data`, `src/FusionRpg.Server`, `data/tuning`, `data/seed`, and the design
docs named in `DESIGN-GATE.md` §1 for battle, demons, world map, economy, resources, status, items,
power, tunables, standalone and GUI.

### 2.1 The headline

**Roughly two thirds of the Delve is built.** The battle resolver, the seeded roll SDK, the loot
pipeline, the relic class, the soul economy, the contract gate, the specimen soft-lock, the
determinism rig, the fog model and the per-tier squad-slot pattern all ship today. What does not
exist is the **room graph**, the **event deck**, **state carried across fights**, a **party identity
inside a battle**, and the **stage** the player would play it on.

Three findings that shape the design more than the rest:

1. **Expeditions are the Delve with the rooms removed.** `ExpeditionResolver` already rolls a seeded
   chain of ticks — battle, boss battle, quiet, found-souls, wild-demon-met, injury — from
   per-tick RNG streams, with squad slots per tier in data (`expeditions.v1.json:10-13`,
   `squadSlots` 2/3/4/5), a sealed seed that never leaves the server, pro-rated recall, and
   exactly-once reward application. It is a *line* of ticks, not a graph of rooms, and it is
   auto-resolved. The Delve is the same machinery **played** and **branched**.
2. **The interactive seat exists and nobody sits in it.** `BattleEngine.Resolve` takes an
   `IIntentSource` (`BattleEngine.cs:175`), `InteractiveIntentSource` records every declaration and
   replays from a trace (`InteractiveIntentSource.cs:30-91`), the `decisions_json` column is created
   (`RpgStore.cs:603`) — and no production path passes the source, no wave selects a
   `RequiresLiveInput` profile, and nothing writes the column. The Delve is the second live profile
   after `siege`.
3. **The loot pipeline has no production caller.** All twelve steps plus pity, idempotency and the
   store are built and tested (`LootPipeline.cs:105-118`, `RpgStore.Loot.cs`), and `grep` finds zero
   callers in `src/` outside the definition. `RelicCatalog.cs:6` says of itself *"no acquisition
   system exists yet"*. **The Delve is the natural first faucet for both.**

### 2.2 Built

| Thing | Evidence |
|---|---|
| **Seeded expedition chain** — tiers, ticks, event bands, boss tick last | `ExpeditionResolver.cs:106` (`DeriveStream(seed, "tick:" + t)`), `:186-194` (battle ticks at `b·T/(B+1)`, boss on the final tick), `:107-149` (four-way band roll from `expeditions.v1.json:16-21`) |
| **Squad slots per tier, in data, enforced twice** | `expeditions.v1.json:10-13` → `ExpeditionTuning.cs:51`; refused at `RpgStore.Expeditions.cs:42` (`squad.toolarge`) and `ExpeditionResolver.cs:66` |
| **Sealed seed, no stored outcome, exact recall** | `ExpeditionEndpoints.cs:33` rolls; `:302-305` *"the sealed seed never leaves the server before collect"*; `ExpeditionE2ETests.cs:151` proves it. `spec-expeditions.md:40`: the server stores only `(tier, squad_json, seed)` |
| **Exactly-once reward envelope** | `RpgStore.Expeditions.cs:270-274` — one transaction gated on the `Dispatched → terminal` transition |
| **Cross-mode specimen soft-lock** | `RpgStore.Expeditions.cs:60-63` (`specimen.deployed`, `specimen.on-expedition`), reverse check `:139`, release on close `:182`; refused from contracts (`RpgStore.Contracts.cs:359`) and fusion (`RpgStore.Fusion.cs:326, :351`) too |
| **Contract gate on every fielding path** | `RpgStore.Expeditions.cs:64-67` — `specimen.unbound`, `specimen.insubordinate`. Loyalty ±15/−10 per win/loss applied at `ExpeditionEndpoints.cs:183` (`ApplyContractResults`); all numbers in `contracts.v1.json` |
| **Injury as a resolver-internal debuff** | `ExpeditionResolver.cs:145-148` picks a victim; `:246-259` applies `−atk/InjuryPowerDivisor` on `combat.power.omni` for the remaining battles. Spec `spec-expeditions.md:99` names *persistent injuries* as ask-first |
| **Wild-join and material stubs** | `ExpeditionResolver.cs:121-129` (250‰ join on a wild-met tick, minted with `Origin = "expedition"` at `ExpeditionEndpoints.cs:158`); shards `:97-98`; essence `:134-136`; persisted `RpgStore.Expeditions.cs:231-235` |
| **Battle side size is unbounded** | `BattleModels.cs:202-206` — two `IReadOnlyList`s; only non-empty and key validation (`BattleEngine.cs:177-197`). A five-demon party is legal input today. Shipped waves already field 4/6/7/6 enemies (`WaveCatalog.cs:115-118`) |
| **Θ-driven enemy magnitudes, PS-3 explicit** | `WaveCatalog.cs:144-146` → `BattleRuleset.BaseHp/BaseAtk/BaseDefense(theta)` → `PowerLadder`/`ChannelLadder` off `power-scale.v2.json` (`BattleModels.cs:172-175`); rates read `Θ` directly (`:186-196`) |
| **Θ_content already composes a sector's danger** | `Power/ContentContext.cs:16` — `ContentContext(DangerBand, WorldTier, ZombossLevel, RealmsAdvanced)`; `PowerIndexComposer.cs:72` applies `Wm` to `DangerBand`. `SectorTypeCatalog.cs:55-102`: homeworld 0 … warcamp 4 … **boss-lair 6** (`Flags = Boss`) |
| **`hybrid-atb` live in all shipped waves** | `BattleModeProfile.cs:199-206` (W=4, `FixedIncrement`, `EarlyBoundWithFallback`, ActionPoints(2), `OrdersBySpeed`); `WaveCatalog.cs:106-118`. Initiative + AP economy + per-actor turn FSM live in `Resolve` (`BattleEngine.cs:323-452`) |
| **`W` per wave** | `WaveDef(... int? W = null)` (`WaveCatalog.cs:31`) — the "this boss is strictly serialized" lever is real |
| **Determinism rig and goldens** | xoshiro256**/splitmix64, FNV-named streams (`SeededRng.cs:9-86`); platform stamp (`BattleModels.cs:307-311`); 4 battle hashes + 32-seed sweep + 4 expedition tier hashes (`ExpeditionResolverTests.cs:205-218`) |
| **Coward retreat and immortal revive** | `BattleRunState.cs:581-595` sets `Retreated` (leaves alive, no die event); `:565-579` revives on `ImmortalCharges` |
| **Loot pipeline, 12 steps + volume + pity + store** | `LootPipeline.cs:105-118`; item level reads the *content* only (`:172-175`); `DropVolume.cs:35-42` linear in `Θ_actor` (§10 row 28); `RpgStore.Loot.cs:95, :312, :393, :511`; `LootManifest.FirstClearGrant` exists |
| **Source kinds and correlation shapes** | `LootPipeline.cs:91-98` — `web-wave`, `expedition-tier`, `world-sector`, undesigned; unknown kind **throws** (`:97`) |
| **No drop caps anywhere, by decision** | `data/seed/loot/tables.v1.json` `_meta.noCap`: *"There is no drop cap, no inventory ceiling and no per-run or per-period limit anywhere"* (D26) |
| **Relic class, equippable, three slots** | `RelicCatalog.cs:19-49` (four relics, `Slot` weapon/armor/trinket); `UniqueEquipmentCatalog.cs:12` `DefaultSlots`; `/api/relics` (`RelicEndpoints.cs:15`); equip via `/api/unique/actors/{id}/equipment` |
| **Item sinks** | `MutationOp.cs:13-38` — enhance, temper, reforge, transfer, socket-add, socket-insert; salvage `Items/Materials/SalvagePolicy.cs`; tuning `enhancement.v1.json`, `materials.v1.json` |
| **Souls: ledger, watermark, spend, Θ-scaled faucet** | `RpgStore.Souls.cs:102, :123, :189`; `SoulEarnPolicy.cs:74-80` — `KillEarn`/`MatchEndEarn` multiply unchanged constants by `contentScale(Θ)`; thirteen faucet/sink reasons `:49-66` including `expedition` |
| **`contentScale(Θc) = P(Θc) / pin`, applied exactly once inside `Instantiator`** | `Power/ContentScale.cs`; `Instantiator.cs:98-107` requires `thetaContent` and `tuning` — *"Absence is a rejection, not a default of 1.0"* |
| **Seeded roller precedent with visible pity** | `SummonRoller.cs:61-62` — `Roll(banner, focus, count, pity, rng) → (Results, Pity)`; `LootPity.cs`, `CraftPityCounter.cs` |
| **Specimen FSM, watchdog, boot sweep** | `UniqueActorDtos.cs:8-12` (`Roster/Deploying/ActiveBound/Recovering/Retired`); `UniqueActorDeployWatchdog.cs:5`. `Retired` is a tombstone set by release (`RpgStore.UniqueActors.cs:198-216`), never by combat |
| **World nouns a room graph can borrow** | `WorldSector`/`WorldSlot`/`WorldLane` (`WorldState.cs:96-230`) — `GuardWaveId` on a slot (`:107-111`), `OneWay`/`Gated`/`GateKeyId` on a lane (`LaneTypeCatalog.cs:24-25`, `WorldState.cs:228`); `SlotKind` has `Lair, Tear, Vault, Shrine, Market, Anomaly, Hazard` (`SlotTypeCatalog.cs:7-29`) |
| **Fog as "unexplored room"** | `Visibility.cs:5-16` (`None/Glimpse/Full`), `SightLanes = 1`, scout doubles it; `SectorPhase` `Unknown → Explored → …` (`WorldState.cs:17-25`) |
| **Wounds that persist and mend** | `WorldEntityMember.Wounds` (`WorldState.cs:249`), healed per turn in supply at `SupplyGraph.cs:165-167`, read by the placeholder resolver `:40, :122-123` |
| **The world↔combat seam** | `BattleSeam.cs:30-96` — `BattleRequest` (kinds `Sector/Lane/Guard`, `GuardWaveId`, `SlotIndex`), `BattleOutcome`, `IBattleResolver` |
| **Stage/layer shell, keymap, toasts, dialogs** | `information-architecture.md` §1-§4; Expeditions is already a band-2 layer (`routes.tsx:118` redirects `/expeditions` into the Sanctum stack) |

### 2.3 Wiring gap — inert, **not** an architectural wall

Each row names the specific line that is switched off.

| Gap | The inert line | Why it matters here |
|---|---|---|
| **The second seat has a seam and no caller** | `BattleEngine.cs:175` — `Timeline.IIntentSource? intentSource = null`; all three production call sites omit it (`WebMatchService.cs:104, :146, :251`) | A played Delve passes a real source here. `InteractiveIntentSource.cs:30-91` already records and replays; `BattleSessionRegistry.cs` exists |
| **No shipped profile is interactive** | `RequiresLiveInput` true only on `siege` (`BattleModeProfile.cs:232`), which no wave selects; `WebMatchService.cs:197` *"Inert today"* | The Delve is the second `RequiresLiveInput: true` row |
| **`DecisionsJson` is read and never written** | `RpgStore.cs:603` creates it; no writer in `src/` | The played-battle persistence half is a column and a reader with no producer |
| **Expeditions bypass the profile resolver** | `WaveCatalog.ProfileForExpedition` (`WaveCatalog.cs:65`) — callers are tests only (`ExpeditionInteractiveBarTests.cs:34`, `ProductionProfilePathTests.cs:52`); `ExpeditionService.CollectAsync` never calls it | The Delve resolves its profile from content the way the map says it should |
| **`Retreated` has one producer, a trait** | `BattleRunState.cs:591` sets it only for `coward` | A player-ordered retreat is the same flag from a different source — no new state |
| **Downed/revive exists in the timeline FSM, not in `Resolve`** | `ActorTurnMachine`, `ActionRunner`, `ReadinessDriver`, `RendezvousLane`: 21 test files. *(Corrected 2026-09-05, wave-2 verification:)* B38 already constructs one `ActorTurnMachine` per actor (`BattleRunState.cs:136-141`); the engine transitions only `Ready/Committed/Resolving/Recovering/Charging` (`BattleEngine.cs:419-466`) and **never enters `Downed` or `Dead`** — `BattleEngine.cs:65-68` is still a binary `Alive => Hp > 0` | `decisions.md` row 42 already locks `Downed` as *"HP ≤ 0 is veto-capable, never a terminal edge"*. The Delve needs Downed; the engine has it built and not wired |
| **Reaction lane and rendezvous are gated off** | *(review correction, refined 2026-09-05 during propagation)* `battle.v4.json:37` ships `hybrid-atb.wReact: 1` and `BattleEngine.cs:230` **does read** `activeProfile.WReact` into `ReactionLane`; the lane is consumed only under `activeProfile.UsesTimelineDispatch` (`BattleEngine.cs:373`), which **no catalog row or test sets** — so the inert line is the dispatch flag, not the value and not a missing reader; `RendezvousEnabled` false everywhere | Press-turn (SMT) and link strikes are profile rows away — owner already put both in scope (2026-08-21) |
| **`resource.delta` reaches the bag only from a test seam** | `BattleEngine.cs:132-137` — *"Null in every production and golden call site"* | Hunger/spirit attrition in a delve fight is a grant through this seam |
| **Loot pipeline has zero production callers** | `LootPipeline.Resolve` (`:134`) — tests only; no `LootEndpoints.cs` exists | The Delve is the first caller. A fifth `sourceKind` and correlation shape at `LootPipeline.cs:91-98` |
| **`Instantiator.TryInstantiate` has zero production callers** | `Instantiator.cs:98`; five specs independently record this | Every "we need a runtime generator" finding below is a wiring gap on this SDK, not a new build |
| **Relics have no faucet** | `RelicCatalog.cs:6` *"no acquisition system exists yet"* | The Delve grants them. `LootManifest.FirstClearGrant` is the exact slot for a boss's guaranteed relic |
| **Turn engine `Events` phase is a pass-through** | `TurnEngine.cs:272` `return world;` after calendar report lines only | Not needed by the Delve, but the map's own event slot exists if domains later rotate with seasons |
| **`SlotKind.Anomaly` / `Vault` / `Tear` / `Lair` have no reader** | `SlotTypeCatalog.cs:70-76` rows; `grep` finds no verb | These are the domain *entrances* on the map, already in the catalog |
| **`WorldEntityMember.InstanceId` has no writer** | `WorldState.cs:244`; only `RaiseResolver.cs:132-136` mints members and never sets it | A legion of named specimens is modelled, hashed and persisted; the Delve's party is the first writer if it is ever launched from a legion |
| **The world's battle resolver is never supplied** | `TurnEngine.cs:83` `resolver ?? PlaceholderBattleResolver.Instance`; both call sites pass none | Not the Delve's job, but the same `BattleRequest.Guard` shape is what a room encounter is |
| **`WebMatchService.cs:339` `const int maxSquad = 6`** | A balance number as a `const`, refused with `squad.toolarge` | The Delve must not copy this; `expeditions.v1.json`'s `squadSlots` is the pattern |
| **Actor resource pools and exhaustion are built, and battle never constructs them** *(Part II correction — this row was a real gap in the first draft)* | `Actions/Cost/ActorResourcePools.cs:13-25` holds all six pools and *"Seeds every pool from a caller-supplied stored value"*; `ExhaustionPolicy.cs:35` registers `exhaustion.{resourceId}` statuses; `CostLedger` has **zero production constructors** and `Battle/` references pools only inside the uninstantiated timeline lane (`ReactionCounter.cs:39`) | The carry-across-rooms seat exists in the action layer; `BattleEngine.ActorState` (`BattleEngine.cs:23-82`) is the one place that lacks a pools reference |
| **`Θ_content` is never composed in production** *(Part II)* | `ContentContext` (`Power/ContentContext.cs`) has zero `new ContentContext(` sites in `src/`; `WaveCatalog.cs:115-118` bakes `theta` literals; `WaveCatalog.cs:141` sets `Level = theta` from content only, so the species `thetaOffset` (`SpeciesExpander.cs:66-67`, `speciesBaseTheta: 0` in `demon-shape.v1.json:25`) never reaches a wave enemy — a calamity and a nuisance fight at the same `Θ` today | The Delve is the first production producer of a composed `Θ_content`, and the encounter generator is where `Θ_room + thetaOffset(species)` is finally summed |
| **A gate can be shut, never opened** *(Part II)* | `MarchResolver.cs:58-60` refuses `lane.gated` when `GateKeyId != null`; no writer anywhere nulls it or checks a carried key | The Delve's key mechanic is the first writer for the verb the lane nouns already declare |

### 2.4 Real gap — no mechanism anywhere

| Gap | What would have to be built |
|---|---|
| **A room graph** | No sub-graph or instance container exists — `WorldState` is one flat `Sectors`/`Lanes` list with no scoping field. The nouns exist; the *container* and the *generator* do not. `world-generator` is deliberately last in its own program and unstarted |
| **An event deck** | No event catalog or generator anywhere in `src/` (zero hits for `EventCatalog`/`RandomEvent`). The expedition band roll (`ExpeditionResolver.cs:107-149`) is the only precedent, and it is four inline branches |
| **State carried across fights** | `ActorState.Hp = setup.MaxHp` at spawn (`BattleEngine.cs:26-30`); `BattleActorSetup` has no current-HP field. Statuses and shields *can* carry in (`InitialStatuses`, `InnateShield`, `BattleModels.cs:66-71`) and `HpRemaining` carries out (`:252-255`). The missing piece is one inbound field |
| **Party identity inside a battle** | `Side` is a bare string `"squad"`/`"wave"` hard-coded in eight engine decisions (`BattleEngine.cs:255, :281, :289, :299, :311, :485, :492-494, :498`); `BattleOutcome` is `Victory/Defeat/Stalemate` only. Two allied parties can already share `"squad"`; telling them apart (economy, auras, report) needs a label |
| ~~**Actor resource pools in `BattleEngine`**~~ *(Part II: reclassified as a wiring gap — see §2.3. `ActorResourcePools` and `ExhaustionPolicy` are built; only the battle actor lacks a reference to them.)* | `ActorState` carries hp and combat state only (`BattleEngine.cs:23-82`). `resource-hub-ssot.md` §11 says pools *"persist across a run and refill at rest"* — the Delve is the first run that needs that to be true |
| **A durable wound / recovery timer** | `Recovering → Roster` is one write (`unique-actor-runtime.md:121`); `Retired` is release only. No injury/fatigue state on a specimen. The expedition injury is resolver-internal by design |
| **A boss or elite flag** | `WaveDef` and `BattleActorSetup` carry no kind discriminator (`WaveCatalog.cs:31`, `BattleModels.cs:7-90`); `demon-threat.v1.json`'s `thetaOffset` reaches `Θ` but nothing marks *this actor is the boss* |
| **Wave content is code, not data** | Four hand-written rows at `WaveCatalog.cs:115-118`; no wave/encounter file among the 58 in `data/tuning/`. The Delve authors many encounters and inherits this gap — fix it, do not extend it |
| **A `delve` stage** | `railState.ts:31` declares `"battle"` with nothing behind it; `information-architecture.md` has four stages and no dungeon. The lawn's Phaser island is PvZ-shaped throughout |
| **A player-ordered retreat action** | No action, no engine path other than `coward` and wipe/`MaxRounds` |

---

## 3. Prior art

From the research pass commissioned for this document (2026-09-05, sources inline; items the
fetcher could not confirm are marked *unverified*) plus the in-repo files
`research/genre-mechanics/07-rts-and-autobattler.md` §7.1/§8 and
`08-endless-scaling-meta-progression.md` §4.

### 3.1 Darkest Dungeon — the attrition reference

| Mechanic | Number | Source |
|---|---|---|
| Party | fixed 4 | darkestdungeon.wiki.gg/wiki/Quest |
| Stress | 0–200; resolve check at 100 → affliction, or virtue at base 25%; heart attack at 200 | wiki.gg/wiki/Virtue, /Heart_attack |
| Death's Door | at 0 HP; each further hit rolls deathblow, base 33% die (67% resist), **resist hard-capped at 87%** — so every hit at the door is ≥13% lethal | wiki.gg/wiki/Death's_Door |
| DD2 Death's Door | base 60% survive, clamp 33–90% *(review-corrected from 5–90%)*, **−10% resist per survived hit** | darkestdungeon2.wiki.fextralife.com |
| Torch | 0–100 in five bands (>75 / 51–75 / 26–50 / 1–25 / 0); −6 per new corridor tile, −1 re-traversed, +25 per torch item; scouting base 25%, +15% at full light; crit +1/+2/+3 in the three dark bands; 0 light: Shambler ambush 1/8/12% by tier | wiki.gg/wiki/Light_Meter, /Scouting |
| Hunger | corridor-only check 7.5/10/12.5% by light band; eat = 1 food per hero, +5% HP; refuse = −20% HP, +20 stress | wiki.gg/wiki/Hunger |
| Retreat | +25 stress, 70% success first try +5% per attempt; abandon quest +20 stress; aborting the final dungeon kills one hero | wiki.gg/wiki/Retreating |
| Layout | rooms joined by 1–8-tile corridors; a room holds ≤1 curio; Short/Medium/Long = 0/1/2 camps | wiki.gg/wiki/Dungeon_Map, /Quest |
| Curio outcome tables | sum to 100%, with a supply-item override to a guaranteed outcome (Iron Maiden: 40/20/13.3/6.7/20 by hand, 100% loot with Herbs) | wiki.gg/wiki/Curios |
| Quest types | Explore 90% of rooms · Cleanse every room fight · Gather N curios · Boss | wiki.gg/wiki/Quest |
| Rank legality (in-repo, computed over 110 skills) | 31% of skills usable from any rank; rank 4 usable by 62% of skills and hit by 57%; 19% of skills move somebody | `07-rts-and-autobattler.md` §7.1 |
| Roster | 28–30 heroes (the "25" in the brief is wrong) | wiki.gg/wiki/Stage_Coach |

**Documented failure modes.** The July-2015 corpses patch took Steam reviews *"from extremely positive
to somewhat mixed"*; Red Hook shipped toggles and kept the default. Radiant mode was added to cut
*grind*, not difficulty (faster XP, cheaper town, looser level gates — enemies unchanged; *"shorter dungeons" is not in the patch, review*). The stress
spiral: abandon/retreat penalties compound, and the 87% resist cap guarantees lethality at the door.
DD2 replaced rooms with a road of nodes, stress 0–200 with 0–10, and torch with **Loathing** (0–4:
+10/25/40% flame drain, boss +10% HP at L4) — and patched a node type because *"it feels bad to pick
a Resist node to keep Loathing under control and then get penalized."*

### 3.2 SMT / Persona — the turn-economy and dive-depth reference

- **Press Turn:** one icon per party member (4); weakness/crit costs half an icon; miss costs 2;
  null costs 2 (Nocturne) or all (IV/V, *unverified* split); repel/drain cost all. Enemies use the
  same rules. (megatenwiki.com/wiki/Press_Turn_System)
- **One More / Baton Pass:** weakness grants an extra act; a third consecutive pass makes the skill
  free. (megatenwiki.com/wiki/Baton_Pass)
- **Magatsuhi:** gauge fills 15–25% per turn; a Magatsuhi skill costs no press turn.
- **Tartarus:** 264 floors, random layouts between fixed boss floors; the Reaper spawns after ~3–4
  minutes on a floor and follows you. **Mementos:** paths of 2–13 areas, a safe room per group,
  Reaper at ~2 minutes idle. (megatenwiki.com/wiki/Tartarus)
- **Failure modes:** protagonist death = game over (heated community threads); P3's Tired/Sick
  fatigue was **removed** in Reload; SP attrition forcing return trips was patched over the series
  with safe rooms and free-skill passes.

### 3.3 Slay the Spire — the room-graph reference

- **Grid 7 columns × 15 rows, 6 path walks from the bottom, ≥2 distinct starts, no crossings.**
  Row 1 all Monster; row 9 all Treasure; row 15 all Rest; boss connects to every row-15 node.
- **Unassigned node weights:** Monster 53%, Event 22%, Elite 8%, Rest 12%, Merchant 5%. At
  Ascension 1+: elites *"about 60% more"* (≈12.8%) — *review-corrected; the 16% was a Steam-guide
  figure the cited wiki does not carry. §11.2's `80 → 130‰` (×1.6) is right by construction.*
- **Rules:** no Elite or Rest before row 6; no Rest on row 14; Elite/Merchant/Rest never
  consecutive on a path; siblings of one parent differ in type.
- **Unknown rooms:** Treasure 2% (+2%/miss), Shop 3% (+3%/miss), Monster 10% (+10%/miss,
  *unverified*), else Event; reset on hit and between acts.
- **Event pools:** act events (no repeat within act), 6 shrines (once per act), 15 one-time events
  (once per run); eligibility gated on act, floor, gold, HP band, relic count, curse.
- **Reward shape:** elite = relic + 25–35 gold + card; fight relic rarity 50/33/17; chest relic
  small 75/25/0, medium 35/50/15, large 0/75/25.
- **Ascension (in-repo §4.1):** 20 rungs, **only 4 of 20 are a bigger number**; 16 change a rule or
  an economy. Dead Cells' 5 rungs each *remove a resource* and give something back. Hades' 63 heat is
  15 orthogonal dials the player composes (Jury Summons +20% enemies ×3; Lasting Consequences −20%
  healing ×5; Extreme Measures upgrades one boss each).
  (slaythespire.wiki.gg/wiki/Map_Generation, /Events, /Elites; deadcells.wiki.gg; hades.fandom.com)
- **Hades door previews:** the next room's reward is shown before you choose it. Chaos gates: pay HP
  now, curse for 3–4 rooms, then a permanent blessing.
- **Failure modes:** correlated randomness (StS seeds card/relic streams per floor, so a bad seed is
  run-deciding — forgottenarbiter.github.io/Correlated-Randomness); fixed-map boredom is why Gungeon
  authors *flows* (Hollow 4, Proper 8) not rooms.

### 3.4 Multi-party raids

| Game | Shape | Source |
|---|---|---|
| FF VI Kefka's Tower | 3 parties of 4 from 12+; each path has its own fights, chests, bosses; converge at a switch room | finalfantasy.fandom.com/wiki/Kefka's_Tower |
| SaGa Frontier | 3 configurable parties of 5; choose which party takes each encounter | wikipedia |
| Etrian Odyssey | guild 30, party 5; FOEs move when you move and can join a fight | etrian.fandom.com |
| FFXIV | light party 4 · full party 8 · alliance 24 = 3×8, each alliance clears its own trash, converges on bosses | ffxiv.consolegameswiki.com/wiki/Raids |
| WoW Flex | 10–30; player-observed boss HP 100/110/119/127% for 10/11/12/13 — diminishing per head (*unverified*, Blizzard never published) | mmo-champion thread 1345425 |
| Monster Hunter | shared faint pool; 3rd faint fails the quest for everyone | steamcommunity 582010 |
| Fire Emblem Radiant Dawn | four armies converging in Part IV; Part 3's return to the weaker army is the canonical bench-warmer failure | fireemblem.fandom.com |

**Failure modes:** needing 2–3× the roster (Kefka's Tower forces 12 usable units), difficulty cliffs
when the weaker party's route is played, and a shared death pool letting one weak party fail the
raid.

### 3.5 What persistence does to risk — the finding that decides §4.6

In-repo, `07-rts-and-autobattler.md` §8.2, four dials with shipped examples: **priced death**
(WC3: revive ≈ half hero cost, capped at 550 gold — risk-taking stays high); **priced time** (XCOM:
soldier unavailable for days — risk-taking drops sharply, B-team death spiral); **group-state
failure** (DoW2 Last Stand: incapacitated not killed, lose only when all three are down at once —
*"persistence without a resource cost, the cleverest of the four"*); **no refund** (Fire Emblem
classic, Battle Brothers: *"risk-taking collapses, and the observed player response is not caution
but reloading"*). The conclusion: *"persistence is worth having exactly to the extent that the thing
lost is recoverable by spending something the player controls."*

Extraction games sharpen it: Tarkov's average survival is ~20–30%, and the documented behaviours are
*"gear fear"*, *"ratting"* and hatchet-running — players stop bringing gear. Dark and Darker prices
the deeper dive (High-Roller 70 gold entry, more go-deeper portals). Diablo III's first year: 9% of
characters were Hardcore (Blizzard infographic 2013).

### 3.6 Attrition without a wall clock

Every attrition device in the survey is *spatial* or *per-action* — torch per tile, hunger per
corridor, SP per skill, Reaper per minute on a floor, FTL fuel per jump — and the ones that were
wall-clock (P3's fatigue) were removed. The energy-system literature is unanimous that a meter the
player cannot choose *what* to spend on reads as a wall. This matches the repo's own rulings
(`standalone-rpg-map.md` *"no stamina system"*, `ssot-power-scale.md` §11.7 on daily caps).

---

## 4. The shape

### 4.1 Domains, and where they come from

A **domain** is an authored-by-seed place with a `DangerBand`, an element climate, a depth, and a
boss. It is content in the same sense a sector type is: a catalog row that the generator reads.

- **Now:** domains are picked from the Sanctum, exactly the way an expedition tier is picked today —
  the Delve layer lists the domains you have found, their band, their depth and their boss. A domain
  is *found* by play (an expedition's `WildDemonMet`/`FoundSouls` band gains a `FoundDomain` sibling;
  a codex milestone; a first clear of a shallower domain reveals a deeper one).
- **Later, when `world-stage` lands:** a domain is a `Lair`, `Tear`, `Vault` or `Anomaly` slot on a
  sector you can reach — the catalog rows already exist with no reader (`SlotTypeCatalog.cs:70-76`).
  Entering from the map issues the same delve request; nothing in the Delve knows which door it came
  through. This is the seam `world-graph-ideal.md` §13 already draws (*"exploring it is a roguelite
  delve"*), and it is why the Delve is its own program rather than a world-map module: the map's FE
  is frozen pre-refactor and its generator is deliberately last.

### 4.2 The room graph

A seeded **layered DAG**, Slay-the-Spire shaped, because that shape has the best-documented
generation rules in the genre and the repo's own world ideal already names it as the delve's prior
art (`world-graph-ideal.md:597`).

- **Dimensions per domain tier** — rows (depth), width (columns), path walks — are tunables. The
  shipped expedition ladder is the size reference: a scout-sized delve is ~4 rows, a warpath-sized
  one ~10–12. Slay the Spire's 7×15 is a 50-fight run and is too long for a five-demon party with
  persistent wounds; Darkest Dungeon's Short/Medium/Long is the right *ladder*.
- **Node kinds** are a catalog (like `SectorTypeCatalog`): `fight`, `elite`, `cache`, `curio`,
  `wild`, `shrine`, `rest`, `merchant`, `trap`, `unknown`, `boss`. Each kind is one row with weight,
  earliest row, latest row, adjacency bans, and sibling rule. Seeds in `data/seed/dungeon/rooms/`
  give each kind its identity variants (a "flooded reliquary" is a `cache` with a climate); the
  deterministic layer turns ordinals into weights.
- **Guaranteed shapes**, restating `world-graph-ideal.md` §13 and the StS rules: row 1 is all
  ordinary fights; one fixed `cache` row in the middle; the row before the boss is all `rest`; no
  elite or rest before a tunable row; elite/merchant/rest never consecutive; siblings differ; at
  least two distinct routes; nothing unreachable; the boss connects to every last-row node.
- **`unknown` nodes** roll on entry with an incrementing pity per kind (StS: treasure +2%/miss, shop
  +3%/miss), reset on hit and per delve. The pity state is the `SummonRoller` shape
  (`(Results, Pity)` in, out), persisted with the delve.
- **Sight:** you see one row ahead as `Glimpse` (kind and band, not contents), and a `scout` action
  — or a scouting curio outcome — reveals two. This is `Visibility.SightLanes`/`ScoutSightLanes`
  applied to a room graph; no new model.
- **Determinism:** the graph is a pure function of `(domainId, seed, raidMode)`, rolled from named
  streams `dungeon:graph`, `dungeon:room:{r}:{c}`, `dungeon:event:{r}:{c}`, `dungeon:loot:{r}:{c}`.
  The seed is sealed at commit and never leaves the server, exactly as an expedition's does. Played
  decisions are the trace.

### 4.3 Depth is `DangerBand`, not a new curve

**No private `f(depth)`.** A room's `Θ_content` is composed by the existing
`PowerIndexComposer` from a `ContentContext` whose `DangerBand` is the room's band. The domain's
entrance band is its catalog row; each row deeper raises the band by a tunable step; the boss room
sits at the `boss-lair` band (6 today, worth 30 Θ at `Wm = 5`). This reuses §10 row 23
(`mapLevel(M) = Wm · DangerBand(M)`) without amending it. Every magnitude — enemy HP through
`BaseHp(θ)`, relic power through `contentScale` inside `Instantiator`, soul yield through
`KillEarn`/`MatchEndEarn` — reads that one number, once.

*"Depth: more enemies or stronger?"* is already closed in `ssot-power-scale.md` §10.3: *"Both, on
separate owners. Enemy level is `Θ_content`; enemy count is encounter design."* Elite rooms and raid
scaling therefore change **count and W**, never a multiplier on `P(Θ)`.

**Reward escalation is the same number.** Going deeper pays more because `contentScale(Θ)` is
larger; drop *count* reads `Θ_actor`, not the room (`LootPipeline.cs:192`, §10 row 28 — deliberate, so
volume never goes quadratic), so depth raises item level and magnitude, never count *(review
correction — the first draft said count rose with depth; it does not)*. There is no "hardcore
bonus" multiplier anywhere in this design; a proposal to add one is a new §10 row and should be
refused on the same grounds the soul caps were removed.

### 4.4 Rooms are encounters on the battle kernel

A `fight`/`elite`/`boss` room is one `BattleSetup` resolved by `BattleEngine.Resolve` with a **real
`IIntentSource`** — the player's declarations over a live SignalR session, recorded to a
`DecisionTrace` and persisted in the column that already exists. The profile is a new catalog row,
`delve`: the `hybrid-atb` shape with `RequiresLiveInput: true`, `OrdersBySpeed`, and — because the
owner named SMT as the feel and already put press-turn in scope — a `PerSide` economy row so a
weakness hit refunds an action. Whether press-turn is *the* delve economy or one of two is a
config choice (`ITurnEconomy` is pluggable), not a design one.

**Autopilot is one explicit call, not free** *(review correction)*: the delve host resolves the
`delve` profile from the encounter anchor and passes it, with an automated `IIntentSource`, straight
to `BattleEngine.Resolve` (`:172-175`). It must never ride `WaveCatalog.ProfileForExpedition`, which
**throws** on a live profile (`WaveCatalog.cs:66-73`), nor `WebMatchService.ProfileForWave`, which
returns `classic-round` for any id not in `WaveCatalog` (`:53-56`). The policy that plays an
un-steered party is base-defense's `siege-ai` seam, a consumed dependency; `StubIntentSource` is
basic-attack only. `world-graph-ideal.md` §13: *"who decides is a parameter — autopilot policy
or player. Same seed, same rewards, with a decision premium for playing it yourself."* The stub
intent source that resolves expeditions is that policy. A delve can therefore also be dispatched and
auto-resolved room by room, which is what makes it CI-provable with nobody watching, and what a
2-party raid needs for the party you are not currently steering.

**What carries between rooms.** Current HP, statuses, shields, resource pools, the downed flag, and
the haul. The engine reads `HpRemaining` out today and has no inbound current-HP field; that one
field, added under the `[JsonIgnore(WhenWritingDefault)]` precedent so the four expedition hashes do
not move, is the whole of the carry-in change. `InitialStatuses` and `InnateShield` already carry.

### 4.5 Attrition — the resource hub, finally used as designed

`resource-hub-ssot.md` §11 says pools *"persist across a run and refill at rest. They are not
per-encounter."* No run has needed that until now. Two pools do the torch-and-stress job, and both
already have a normative "pays for" meaning:

- **`hunger` (Sun on the plant side) is the supply meter.** Every room costs hunger — more for a
  fight, less for a rest — and the `rest` room and provisions restore it. At zero the exhaustion
  status debuffs derived stats (built as design in §10 of the hub; the debuff is a container of
  atoms). This is Darkest Dungeon's torch/food with one gauge instead of two, and because it is
  per-room it is never a wall clock.
- **`spirit` is the nerve meter.** Horror curios, elite auras and a boss's presence drain it; a
  shrine or a rest restores some. Exhausted spirit is *"identity failure"* in the hub's own words —
  the closest thing this repo has to affliction — and it hooks straight into the thing the
  summoner already tracks: a demon that comes home with spirit at zero takes the contract's `−10`
  loss on top of any battle it lost. Spirit is *"depleted only by harm"* (§2 of the hub); a
  horror event is harm.

Both pools are drawn by grants through the `resource.delta` seam (`BattleEngine.cs:132-137`), which
is built and inert. The real gap is that `ActorState` has no pools at all; the Delve is where they
get added, looping `ResourceIds` so all six are covered by construction (the hub's six-coverage
rule).

**No torch light bands.** The DD torch's second job — trading loot and crit for stress and surprise
— is a *player-chosen risk dial*, and this game already has one: the domain's band and the
`unknown` room. Adding a light meter would be a third attrition gauge on a four-headline-stock
economy (P7).

### 4.6 Risk — what a delve puts on the table

Restating §3.5: the thing lost must be recoverable by spending something the player controls, or
the design converts risk into reloading. Server-sealed seeds make reloading impossible here, which
is exactly why the loss has to be priced rather than absolute — an unreloadable no-refund death is
the harshest combination in the survey. *(Review correction:)* Darkest Dungeon **does** ship it by
default on all three modes; its mitigation is cheap replacement — free level-0 recruits — a fifth
dial `07` §8.2 lacks, and §8.2 is itself marked INFERENCE. Decision 1's permadeath rungs are closer
to Darkest Dungeon than the recommendation below was.

| Loss | Shape | Recovery |
|---|---|---|
| **Haul** | Unbanked until extraction. A wiped party's haul stays in the domain. | Nothing — this is the risk. Extraction is available from any room, always, and forfeits everything past it |
| **A downed demon** | `Downed`, out for the rest of the delve (Last Stand shape: the *party* fails only when all are down). A rest room or a revive action stands it back up at a spirit cost | In-delve, by spending a room or an action |
| **Coming home wounded** | Every demon downed at any point returns in `Recovering` for real days (today that transition is instant), with the contract's `−10` loss applied | Priced time, and — because priced time alone produces XCOM's B-team spiral — **priced souls**: a recovery ritual, the exact shape of `contract-ritual`, shortens it |
| **Nerve** | Spirit drained to zero at extraction = a second `−10` | Rest, shrines, the ritual |
| **Retreat** | Always allowed; costs spirit (DD: +25 stress) and forfeits the boss | — |
| **Death** | **Not the default.** An opt-in raid modifier, *Oath*, makes a downed demon at extraction `Retired` for real, and in exchange opens bands the domain otherwise refuses — deeper rooms, not a reward multiplier | The player's choice, once, at commit |

The Oath is the Hades/StS-ascension lesson: hardcore is a *rule change the player composes*, not a
number. Its reward is reached through `Θ`, never through a private bonus.

### 4.7 Events, curios and the deck

An **event** is a seeded row: identity (name, flavour, climate) from `data/seed/dungeon/events/`,
outcome table as ordinals the deterministic layer turns into ‰ weights, an optional **supply
override** (Darkest Dungeon's "bring the herbs" — a provision that turns a gamble into a guaranteed
outcome), and a **repeat scope**: per-delve, per-domain, once-per-player. Eligibility predicates
reuse the action layer's own predicate vocabulary (band, row, hp band, haul size, has-provision) —
never a fourth vocabulary.

Outcomes are drawn from the same closed effect vocabulary everything else uses: `resource.delta`,
`status`, `shield.grant`, `stat.derived` for a delve-long buff, `ui.present` for the banner, and a
loot draw through the pipeline for a `cache`. **No event may write a stat directly** — it grants,
scoped to the delve (`WhereScope.Battlefield` + a delve owner scope), and the grant is withdrawn at
extraction.

The **wild** room is the negotiation: a wild demon met in a room can be fought, bargained with
(spirit or souls), or recruited on the existing `WildJoinResult` path with `Origin = "delve"`. The
join chance stays the expedition's `wildJoinMilli` shape, in the dungeon tuning file. Moon phases
and mood are a later seed axis, not a v1 mechanic.

**Failure modes to refuse by construction:** an outcome table that has a strictly dominant choice
(DD's "always bring holy water") is priced by making the override a provision with a slot cost; a
node that is always-take or never-take is a validator failure — every kind must carry at least one
real downside on some band.

### 4.8 Raids — 1, 2 and 4 parties

A **raid mode** is a tunable row: party count, squad slots per party, and the boss retinue rule.
Squad slots follow `expeditions.v1.json`'s `squadSlots` pattern (per mode, in data) — never a
`const`. The owner's five is the top expedition tier's number and `LoadoutSet.MaxSize`'s; the
`WebMatchService` `6` is the anti-pattern and stays out.

- **Parties take separate routes** through one graph (FF VI / FFXIV shape): each party's room is its
  own `BattleSetup`, its own trace, its own haul. The player steers one party at a time; the others
  run on autopilot until steered, with the same seed and the same rewards. Route choice is the raid
  decision — two parties on two routes see and clear more rooms; four parties reach the boss with
  more standing bodies and more unbanked haul.
- **The boss room is the rendezvous.** Every standing demon from every party forms the squad side.
  The engine accepts an unbounded squad; what changes is `W` (per wave, already content-owned) and
  the **retinue** — the boss brings lieutenants in proportion to the raid, which is enemy *count*
  and therefore encounter design, not a `Θ` change. A party that never reached the boss room is not
  in the fight.
- **Party identity** is a label (`PartyIndex`) on the actor setup, not a third side. Two allied
  parties already share `"squad"` today; the label is what lets the economy (`PerSide` scope keys
  on `Side` at `BattleEngine.cs:359-360`), commander auras and the report tell them apart. Added
  under the `WhenWritingDefault` precedent, it moves no golden.
- **Shared failure pool, per Monster Hunter:** the raid fails when no party has a standing demon,
  not when one party wipes.
- **The bench-warmer problem** (Radiant Dawn) is answered by the contract economy rather than by
  design: a 4-party raid needs twenty bound demons paying tribute, so the roster you can afford *is*
  the raid you can field. Capacity is already price-capped, never hard-capped
  (`ContractPolicy.cs:166-177`).

### 4.9 Payout

- **Relics and gear** come through `LootPipeline.Resolve` with a fifth `sourceKind` — `dungeon-room`
  for caches and elites, `dungeon-clear` for the boss — and a correlation derived server-side from
  the delve id and room coordinate, per the pipeline's own rule that a client never picks its loot
  correlation. The boss's guaranteed relic is `LootManifest.FirstClearGrant`. Item level reads the
  room's `Θ_content` only, so a shallow domain cannot drop deep gear (`LootPipeline.cs:172-175`).
- **Souls** through the existing `KillEarn` per room and `MatchEndEarn` **once per delve at extraction
  on `Θ_run`** *(review S1-1 — per-room victory souls made two row-1 fights the best faucet in the
  game; both reads already `contentScale`-scaled)*
  plus event souls with reason `delve`; `discovery` for a species first seen in a domain.
- **Materials, shards, essence** exactly as expeditions grant them.
- **Wild joins** with `Origin = "delve"`.
- **Specimen and species XP** on the existing per-battle-won paths.
- **A domain's first clear** reveals the next domain and writes a codex entry — the "atlas entry"
  the world ideal's vault row names.

Nothing is capped. Volume is linear in `Θ`, strength is `P(Θ)`, and the only throttle is what the
raid can carry out alive.

### 4.10 The stage

`#/delve/{id}` is the **sixth** stage beside Sanctum, World, Lawn, Battle and `siege` (*review*: `siege` was
approved as the fifth on 2026-09-04, `decisions.md:95`, not merely proposed by the
base-defense ideal). The room graph is the stage; a fight is drawn *on* it, not by bouncing to
`#/battle`; the Relics, Creatures and Pacts layers open over it as they open over everything (GG-1,
GG-7). Its HUD carries the raid's parties, the steered party's resources and haul, and the initiative
rail during a fight. The `battle` stage id already declared in `railState.ts:31` with nothing behind
it is the seam; `information-architecture.md` §2 gains a row and §7's unlock ladder gains *"Delve —
first domain found."* A delve in progress survives a page refresh because the state is the server's
and the URL encodes the stack (GG-8).

### 4.11 Rejected shapes

| Shape | Why not |
|---|---|
| **A fifth expedition tier with rooms, auto-resolved** | Cheapest by far, and it is not what was asked: the owner's references are all *played*. It is also already covered — autopilot is one explicit call on the intent seam (§4.4), so the auto-resolved delve is a mode of this design, not a competitor to it |
| **A world-map module (a `delve` on a sector slot)** | The right *home* eventually, and the seam is drawn for it. But the map's FE is frozen pre-refactor, its generator is deliberately last, and gating the Delve on `world-stage` would park a playable mode behind a UI program. Launch from the Sanctum now; the map gets a door later |
| **Three battle sides, or a per-party side string** | Eight hard-coded side decisions in the engine and a two-value outcome. A label on the actor is additive and golden-safe; a third side is a resolver rewrite for a distinction the boss rendezvous does not need |
| **A light/torch meter** | A third attrition gauge; the domain band and `unknown` rooms already carry the risk dial. P7 |
| **Permadeath by default** | §3.5: no-refund plus unreloadable is the harshest combination in the survey and produces gear fear, not tension. Opt-in Oath instead |
| **A hardcore reward multiplier** | A private power-shaped scale — the §10 anti-duplication clause refuses it, and the soul-cap removal already argued why depth through `Θ` is the honest instrument |
| **Real-time timers (Reaper)** | Virtual time only in Core; the Reaper's job — punishing lingering — is done by hunger per room |

---

## 5. Tunables

Every number this design introduces, and where it lives. `data/tuning/dungeon.v1.json`, one
domain, owner `docs/architecture/party-dungeon/` once specced; published by
`tools/tuning/publish.py dungeon <key>=<value>`, never hand-edited. Units in the key name.

| Key | Meaning | Starting shape (not a balance decision) |
|---|---|---|
| `raid.modes.{solo,pair,quad}.parties` | party count | 1 / 2 / 4 |
| `raid.modes.*.squadSlots` | demons per party | 5 (the top expedition tier's number) |
| `raid.modes.*.bossRetinuePerParty` | extra lieutenants per party beyond the first | 1 |
| `raid.modes.*.bossW` | concurrency width in the boss room, overrides the wave | 4 / 6 / 8 |
| `graph.tiers.{short,medium,long}.rows` | depth | 4 / 7 / 11 |
| `graph.tiers.*.width` | columns | 4 / 5 / 6 |
| `graph.tiers.*.pathWalks` | StS "6" | 3 / 4 / 6 |
| `graph.cacheRow`, `graph.restRowBeforeBoss` | fixed rows | middle / true |
| `graph.eliteEarliestRow`, `graph.restEarliestRow`, `graph.noRestRow` | StS 6 / 6 / 14 | tier-relative |
| `nodes.{kind}.weightMilli` | unassigned node weights | fight 530, event 220, elite 80, rest 120, merchant 50 (StS); ascension-style rows raise elite |
| `nodes.unknown.pity.{cache,merchant,fight}.baseMilli/stepMilli` | unknown-room pity | 20/20, 30/30, 100/100 |
| `depth.rowsPerBandStep` *(was `depth.bandStepPerRow` — a prose rate; renamed by the difficulty-ladder spec, review S2-2)* | rows per band step | 2 |
| `depth.bossBandDelta` *(was the absolute `depth.bossBand: 6` — a copy of `boss-lair`'s band that a long corridor could overtake; review N4)* | bands added to the last corridor's band for the boss room | +1 |
| `attrition.hunger.perRoom.{fight,elite,event,rest}` | hunger cost per room kind | tunable, rest negative |
| `attrition.spirit.perElite`, `attrition.spirit.bossPresence`, `attrition.spirit.retreat` | nerve drains | DD +25-stress shape |
| `attrition.restHealMilli` | HP/hunger/spirit restored at a rest | ‰ of max |
| `risk.downedRecoveryDelves` *(was `…Days`; owner removed the wall clock after review)* | delves sat out per downed demon — virtual time, never real days | priced-time dial |
| `risk.recoveryRitualSouls` | per rarity, the priced-souls escape | the `contract-ritual` shape |
| `risk.retreatSpiritCost` | leaving early | — |
| `risk.oath.bandUnlock` | bands opened by the Oath | +2 |
| `events.repeatScope.*` | deck scopes | — |
| ~~`events.provisionSlots`~~ | **Retired (Part II, owner clarification):** provisions have no separate slot count — they occupy cells of the per-delve carry grid beside future haul (§11.7) | — |
| `wild.joinMilli`, `wild.bargainSpiritCost` | the negotiation | 250 (expeditions') |
| `provision.priceSouls.*` | pre-entry sink, **scaled by the domain's `contentScale`** so the sink tracks the faucet (PS-5) | — |
| `merchant.priceSouls.*`, `merchant.markupMilli` | the in-delve sink | — |

Seeds, `data/seed/dungeon/{domains,rooms,events,curios}/`, per the seed-to-concrete rule: identity
and ordinals from seedsmith, weights and magnitudes from the deterministic layer, concrete rolls per
player through `Instantiator`.

**Wave/encounter composition** is the one number this design will not leave in code: `WaveCatalog`'s
four hand-written rows are the anti-pattern, and the Delve authors dozens of encounters. They go in
`data/seed/dungeon/encounters/` read by the catalog, which is the fix the base-defense ideal also
asks for.

---

## 6. The economy, in one table (P1: faucet and sink in the same change)

| Faucet the Delve opens | Scales on | Sink named for it | Scales on |
|---|---|---|---|
| Relics and gear (`LootPipeline`, first production caller) | `P(Θ)` strength, `Θ` count | enhance / temper / reforge / socket / salvage (built); equip-now vs salvage-later is P6's two horizons | per-item `+n`, own axis |
| Souls per room and per event | `contentScale(Θ)` | provisioning before entry and the in-delve merchant (**new**, priced on the same `contentScale`); recovery ritual (**new**); contract tribute, which rises with the roster a raid demands (built) | same read |
| Materials, shards, essence | flat, as expeditions | fusion, enhancement (built) | flat |
| Wild joins | — | tribute per bound demon (built) | — |
| Specimen and species XP | cost ladders, rows 6/26/27 | — (progression, not a stock) | — |

The unbanked haul is not a sink and must not be counted as one — a haul lost is a faucet that did
not fire. What keeps the Delve from being the `+2`/kill incident is that every soul it pays is
already `contentScale`-scaled by the shipped policy, and the two new sinks are priced on the same
read.

**Souls remain the only headline stock this adds pressure to.** No new currency (P7). Provisions are
an *item* category on the ten-rung ladder (materials plausibly stop at 70, per `ssot-rarity.md`
§4.3), not a stock.

---

## 7. What this deliberately does not decide

- **Any number.** §5 gives shapes and starting points; the balance pass owns them.
- **Species-specific boss kits** and the elite roster — content, through the demon-seed and action
  programs, on the existing `HypnoAlly` boss-class species the expeditions spec promised and never
  wired.
- **Whether expeditions are later re-expressed as auto-resolved delves.** The owner has already said
  expeditions are refactored *after* the world map is complete; this design makes that refactor a
  profile choice rather than a rewrite, and stops there.
- **The world-map door.** Which slot kinds open a domain, whether a legion's `delving` state
  (`world-graph-ideal.md:430`) is the entry, and what a sector gains from a cleared domain are the
  world program's, behind its own seam.
- **A live PvZ lawn as a room kind.** The one-axis rule allows it (breadth, never power); it is an
  extension, and it needs the injector's kernel drive, which is sequenced last in its own program.
- **Press-turn versus action-points as the delve's default economy.** `ITurnEconomy` is pluggable;
  this is a config choice made at spec time with the owner's feel in hand.
- **Plates.** The stage needs a design plate under `docs/design/`; this document names the stage
  and its HUD, not their pixels.

---

## 8. Open questions — owner decisions only

Each is a real fork where the two readings produce different work. Recommendations first.

1. **Death: priced time plus priced souls by default, permadeath as the opt-in Oath (recommended)
   — or permadeath by default.** §3.5 and §4.6 make the case; the counter-case is that the owner asked
   for *the most hardcore mode in the game*, and an opt-in is softer than a default. If default
   permadeath is chosen, the Oath collapses into the base rule and `risk.oath.*` is deleted.
2. **Entrance: Sanctum-launched now, map door later (recommended) — or map-only.** Map-only makes the
   Delve wait on `world-stage`'s refactor and the world generator. Sanctum-launched ships a playable
   mode with the same seam the map will use.
3. **Raid boss scaling: retinue and `W` (recommended) — or a boss HP multiplier per party.** The
   multiplier is a new power-shaped scale and needs a reviewed `ssot-power-scale.md` §10 row before
   it may exist; retinue and `W` are both already content-owned knobs.
4. **`spirit` as the nerve meter (recommended) — or a seventh resource.** The resource hub's "pays
   for" column is normative and adding a resource is an ADR. Spirit's exhaustion is already
   *"identity failure"*, which is the affliction fantasy; the alternative is a `nerve` pool with its
   own ADR, its own six-coverage obligations and a seventh row in every derived family.

Everything else in this document is a recommendation nobody has disputed, and is therefore a
decision until the owner says otherwise.

**Part II adds nine more, consolidated in §11.9.** They are the forks the seven sub-mechanism passes
found where two readings produce different work; every other recommendation in §11 stands as a
decision on the same terms.

> ### ✅ All four decided by the owner, 2026-09-05 — read these, not the options above
>
> 1. **Death: priced time plus souls by default (option 1), AND permadeath on rungs `very-hard` and
>    above, per domain, tunable.** Owner: *"default option 1 and some specific dungeon hard, very hard
>    difficult and above will permanent death — make them tunable."* So the rung table gains a
>    `permadeathFromRung` column and each domain seed may override it; the Oath is the opt-in on rungs
>    below that gate (§11.9 #8). A downed demon on a permadeath rung is `Retired` at extraction; below
>    it, it is `Recovering` for a tunable count of **delves** (`risk.downedRecoveryDelves` — *amended by the
>    owner after review, 2026-09-05: the real-time clock is removed; this game has no wall-clock
>    pacing*) with the `−10` loss and the soul ritual as the priced escape.
> 2. **Entrance: both from day one** — the Sanctum picker *and* the world-map door ship in the first
>    wave. §4.1's "later" is retracted; the map door reuses the four existing slot kinds (§11.9 #14).
>    Scope consequence accepted by the owner: the first wave touches the map FE.
> 3. **Raid boss scaling: retinue and `W`, plus a shield pool** whose capacity per extra party is
>    `P(Θ_room)` through the built shield layer — no new curve, one more dial to tune
>    (`raid.modes.*.bossShieldPerPartyMilli`, a share of `P(Θ)`).
> 4. **`spirit` is the nerve meter, realised as a stackable, staged status** — owner: *"nerve will be
>    a status with multiple stage (stackable status, already define and build this mechanism)."*
>    *(Review footnote: the shipped `StatusStacking` is a re-apply policy — `Refresh / Replace /
>    Coexist`, `ResistanceEvaluator.cs:18-23` — not a staged stack counter; the ladder is a small
>    build, and because `StatusCatalog` is ADR-locked and the resource hub's "pays for" column is
>    normative, two `decisions.md` rows precede it.)* So
>    nerve is not a raw pool read: spirit drain applies stacks of a `nerve` status whose stages are the
>    Darkest Dungeon affliction ladder (unsettled → shaken → afflicted, names tunable), each stage a
>    container of atoms, using `StatusRuntime`'s existing stacking. The resource hub's "pays for"
>    column is amended for `spirit` in the same change.

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches: battle/turns, demons (roster, contracts),
    expeditions, world map, economy, resources, status, items/rarity/loot, power, tunables,
    standalone, GUI.
[x] I read every doc in the §1 row(s) for those subsystems, this session — software-architecture,
    decisions, battle-timeline-map, battle-turn-ideal, standalone-rpg-map, world-map-program,
    world-graph-ideal (§5.1, §7.2, §12, §13, §14), demon-system-map, spec-demon-contracts,
    spec-soul-economy, spec-expeditions (via inventory), economy-principles, resource-hub-ssot,
    status-ssot §9, item/ssot-rarity §3.3/§4.3, ssot-power-scale §5/§10/§11, tunables-ssot,
    game-gui-principles (GG-1, GG-44, GG-52/53), information-architecture §1-§4/§7,
    research/genre-mechanics README + 07 §7.1/§8 + 08 §4, research/game-design README.
[x] I checked decisions.md for a lock covering this: rows 42 (battle time model — Downed,
    live sessions, decision-trace), 50 (hybrid-atb, W per wave, interactive = live SignalR),
    caps (project-wide), magic numbers, resource model, standalone-first, game GUI. *(Review: the
    Game GUI row IS contradicted — `siege` was approved as the fifth stage on 2026-09-04, so the Delve
    is the sixth and needs its own row.)* Otherwise none contradicted; each is restated where it
    constrains a choice.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — five parallel inventories plus direct reads of
    BattleRunState.cs:565-595, RpgStore.UniqueActors.cs:190-225, RelicCatalog.cs, TurnEngine.cs
    Events phase, Instantiator.cs:85-120, expeditions.v1.json, contracts.v1.json.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting — *(review: no longer "not applicable";
    decisions 5 and 16 touch the world header hash's neighbours and the `DerivedStatRegistry` count
    canary; the review reasoned from the hash code and ran no suite)* the first draft proposed
    no change and reports no "moves goldens" claim; the two golden-safe additions it names
    (an inbound HP field, a party label) rely on the WhenWritingDefault precedent, which is
    cited, not run.
[x] Nothing contradicts a §2 invariant. The one place this design touches an invariant's edge —
    resource pools persisting across fights — is the resource hub's own §11 rule, unused until now.
[ ] Corrections are propagated — *(review: NOT yet. Nine stale-doc propagations are owed to other
    documents; listed in party-dungeon/audit-2026-09-05.md §6.)*
```

---

## Related (Part I)

- [world-graph-ideal.md](world-graph-ideal.md) §5.1 (vault/lair/anomaly slots), §13 (the delve
  paragraph this expands), §14
- [base-defense-ideal.md](base-defense-ideal.md) — sibling stage (`siege`), shares the wave-data gap
  and the `RequiresLiveInput` seam
- [standalone-rpg-map.md](standalone-rpg-map.md) and [standalone/spec-expeditions.md](standalone/spec-expeditions.md)
  — the shipped line this design branches
- [battle-timeline-map.md](battle-timeline-map.md) T6/T10/T11 — the live-session modules the Delve
  is the second consumer of
- [item-map.md](item-map.md) — the loot pipeline the Delve is the first caller of
- [demons/spec-demon-contracts.md](demons/spec-demon-contracts.md) — the gate and the loss rule
- [resource-hub-ssot.md](resource-hub-ssot.md) §2, §10, §11
- [power/ssot-power-scale.md](power/ssot-power-scale.md) §5, §10.3, §11.7a
- [economy-principles.md](economy-principles.md) P1, P2, P6, P7
- [research/genre-mechanics/07-rts-and-autobattler.md](../research/genre-mechanics/07-rts-and-autobattler.md) §7.1, §8
- [research/genre-mechanics/08-endless-scaling-meta-progression.md](../research/genre-mechanics/08-endless-scaling-meta-progression.md) §4

**Next step:** answer §8, then `/spec` for a capability map (`party-dungeon-map.md`) and module
specs under `docs/architecture/party-dungeon/`. No plan, no tasks, no code from this phase.

---

# Part II — the sub-mechanisms, as seed generators paired with in-game generators

**Added 2026-09-05.** The owner listed fourteen sub-mechanisms, each phrased as *"seedsmith X seed
generator and in-game X generator based on seed."* That phrasing is the repo's binding generation rule
applied fourteen times, so Part II is organised around it. Seven passes, one per group, each read the
seedsmith design domain (`seedsmith-design` skill, `ai-native-generation/README.md`,
`item/seed-contract.md`, `structure-seed-ideal.md`, the game-design failure modes) and the code it
cites; every claim below carries `file:line`, and every pass sorted its findings into built / wiring
gap / real gap. Corrections to Part I are folded in above and marked *(Part II)*.

## 10. The generation laws this part is written under

Restated once, inline, because each of the seven sections below leans on them.

1. **Seed → concrete → per-player. Three layers, and the middle one rolls.** Seedsmith emits seeds
   offline — enums, ordinals, registry ids, free text, **never a magnitude, weight, probability or
   quantity** — committed and diffable. The game runtime rolls the concrete object per player, seeded.
   `Instantiator.TryInstantiate` (`Effects/Atoms/Instantiator.cs:98-107`) is the shared SDK for
   anything that is a container of atoms: `(container, catalogRevision, rollSeed, thetaContent)` →
   `InstanceRow`, byte-identical on replay, `contentScale` applied exactly once inside it. **Never a
   second roll implementation** beside it. It has zero production callers today, so most "we need a
   generator" findings are wiring gaps.
2. **A graph, a deck draw or a slot fill is not an atom container.** `ContainerKind` is a closed
   six-member enum (`ContainerRow.cs:7-15`: `Item, Trait, Skill, SpeciesPassive, Patron, WorldBuff`).
   Structural rolls — which room, which event, which species fills a slot — reuse
   `SeededRng.DeriveStream(seed, name)` (`Battle/SeededRng.cs:26-27`) and `WeightedChoice`, the shape
   `ExpeditionResolver` and `SummonRoller` already use. That is not a second roll SDK **as long as the
   structural roller never touches a magnitude** — every number inside the thing it picked still enters
   through `Instantiator`, `LootPipeline` or `BattleRuleset.BaseHp(θ)`.
3. **The LLM writes identity; deterministic code writes magnitude.** A wrong enum is visible; `hp:
   4200` is not. Enforced by the four-shape schema audit (numeric-string pattern, numeric enum members,
   deny-listed names, unlisted integers), never by review.
4. **Invention is not classification.** The demon pipeline classified 502 anchor files that already
   had names and almanac art (`data/seed/demons/species/`, 415 plant + 87 zombie; the species index
   lists 840 entries; the "408" older documents cite is stale). Rooms, events, encounters, quests and
   domains **do not exist until someone invents them**, so their failure mode is mode collapse and
   generic flavour — twelve variations of "Dark Crypt" — which majority vote does not catch. Every
   invention corpus below is **hand-authored first** (two or more per grid cell) — *superseded by
   decision 6: seedsmith pipelines with a planner-issued per-cell brief replace the floor* — then extended by a
   model that fills thin cells, with flavour distinctness as an open-loop review queue that never
   contributes to a pass.
5. **Four ownership levels, and a field with none is a contract defect:** AUTHORED (the author
   chooses) · VALIDATED (the author names it, a frozen registry owns it) · DERIVED (the importer
   computes it) · GENERATED (a generator emits whole rows). Bands, never numbers.
6. **One power ladder; grid density computed, not estimated** (~3.6 entries per cell is the safe band,
   ~12.6 the failure zone); **rarity buys breadth and ceiling, never power**; **tag absence is a stat**
   (every closed enum admits `none`); **no new vocabulary where one exists** (predicates, resources,
   triggers, categories, roles); **`long`, no `float`, no `const` balance number, no hard ceiling.**

## 11. The seven groups

The owner's fourteen, grouped by what they share a generator with:

| Owner's item | Group |
|---|---|
| seedsmith map seed generator · in-game map generator, random per run · *(added)* the domain seed | §11.1 |
| power ladder for dungeon · ten-level difficulty (very easy … impossible) | §11.2 |
| seedsmith event seed generator · in-game event generator · quest seed generator · in-game quest generator | §11.3 |
| seedsmith boss kind demon + boss generator · party seed + party generator for enemies · group of enemy + group generator | §11.4 |
| dungeon interactive object / building / obstacle generator · supply item generator + usable mechanism | §11.5 |
| recruit / summon / capture demon in dungeon | §11.6 |
| special loot + special item generator + dungeon-specific loot table · Diablo 2 loot pack, arrange minigame, item size | §11.7 |

### 11.1 Domains, maps, and the per-delve roll

**Three anchors, all enums-only, under `data/seed/dungeon/`.** This is an invention pipeline; the
first corpus was to be authored by hand — *superseded by decision 6: pipelines with a planner brief
(review record §1(h), S1-6)*.

*Domain anchor* (`domains/<id>.json`): `domainId` (AUTHORED, allocated namespace) · `name`/`flavor`
(AUTHORED) · `theme` (VALIDATED, the frozen theme registry under `data/seed/demons/_registry/`) ·
`climate` (VALIDATED, `ElementTypeId` or `none`) · `dangerBand` (AUTHORED ordinal `shallow · mid ·
deep · abyssal` → an integer `DangerBand` via tuning) · `sizeBand` (`short · medium · long`) ·
`layoutTemplateId` (VALIDATED) · `bossSpeciesRef` (VALIDATED species id in the top threat rungs) ·
`retinueFamily` · `lootBinding` (a `tables.v1.json` table id per room kind) · `questPool` (subset of
the objective templates, `none` illegal) · `roomPalette` (subset of room-archetype ids) ·
`entranceHint` (VALIDATED `SlotKind` ∈ `{Lair, Tear, Vault, Anomaly}` — `SlotTypeCatalog.cs:14-20` —
for the later map door) · `variants` · `reason` · `_provenance`/`_derived`. **DERIVED, never in the
file:** the entrance band integer, the boss band, row counts, weights, `Θ_content` per row.

*Room archetype anchor* (`rooms/<id>.json`) — *"a flooded reliquary is a `cache` with a climate"*:
`kind` (VALIDATED, the room-kind catalog `fight · elite · cache · curio · wild · shrine · rest ·
merchant · trap · unknown · boss`, one C# row each like `SlotTypeCatalog`) · `climate` · `hazardBand`
(`none · light · heavy` → hunger cost) · `sightBand` (`dim · lit · scouting`) · `encounterRef`
(§11.4) or `eventPool` (§11.3) · `secretEligible` · `tags`.

*Layout template anchor* (`layouts/<id>.json`): `depthBand`, `widthBand`, `branchiness` (`linear ·
forked · webbed` → path walks), `gateDensity`, `secretDensity`, `oneWayDensity`, `fixedRows` (subset
of `{firstRowFights, midCache, restBeforeBoss}`, `none` illegal), `raidModes`.

**Grid density.** Room kind (11) × climate (7) = 77 cells, but four kinds are climate-neutral (`rest`,
`merchant`, `boss`, `unknown`): **53 honest cells → ~190 rooms at 3.6 per cell; ~106 (two per cell)
is the hand-authored floor.** Domains: (climate × danger band) = 24 cells → 48–72 domains; the first
ship is six, one per climate at `shallow`. The cheap axis to widen later is climate; kind is the
expensive one (every kind has catalog rules, bans, tunables and a HUD affordance). Danger is **not**
a distinctness axis.

**The per-delve roll is a pure function** `Roll(domain, layout, seed, raidMode, tuning)` — no clock,
no store — validated the way a world is (throws, never flags). "Random each run" is a fresh sealed
seed minted at commit and stored beside `(domainId, raidMode)`, exactly as expeditions store `(tier,
squad_json, seed)`; the graph is persisted by the world store (decision 5) and the roll is the pure function that
validates the persisted rows *(review)*. Named streams so an extra roll in
one concern never shifts another: `dungeon:layout`, `dungeon:walk:{k}`, `dungeon:kind:{r}:{c}`,
`dungeon:room:{r}:{c}`, `dungeon:gate|secret|oneway:{r}:{c}`, and — consumed at *entry*, not at roll —
`dungeon:event|loot|unknown:{r}:{c}`.

**Structural rules added to §4.2's fairness rules:** gates and keys reuse the lane nouns verbatim
(`LaneTypeDef.Gated`, `WorldLane.GateKeyId`, `LaneTypeCatalog.cs:25`) — a key is an item the party
carries, granted by a cache or elite on a *different* walk, and **the key must lie strictly above the
gate on a walk that does not pass through it**, validated by BFS on the ungated graph then per key;
one-way lanes point deeper only, never sideways, so retreat is always extraction; secret rooms attach
to a dead end or a rest (Gungeon's *"1/5 chance of being attached to any room"*), never on the boss
row, never adjacent to another secret, and are `cache`/`shrine`/`merchant` only; reachability is a
validator that **throws** — a DAG walk cannot orphan a node by construction, so an orphan is a code
defect, not bad luck; sight radius reads `sight.lanes`/`sight.scoutLanes` from tuning, not
`Visibility.cs:33`'s `const`; raid mode changes the walk count, never the row count.

**Reuse `WorldSector/WorldLane/WorldSlot` in a scoped instance, or a new type?** For reuse: the nouns
already carry `DangerBand`, `Climate`, `GuardWaveId`, `Gated`/`GateKeyId`, `OneWay`; the validator
and `Visibility` work unchanged; a delve could be a `WorldState` with `rpg_worlds.mode = 'delve'`.
Against: `WorldSector` drags eleven fields a room never has (`LoamStock`, `StabilityMilli`,
`WardenBindingId`, …, `WorldState.cs:130-211`); `WorldValidation` rules 4–5 *require* a homeworld and
Seat counts; `Visibility` keys on faction ownership, not a party; the turn engine, loam, supply and AI
all iterate `Sectors`; and **`WorldState` has no scoping field**, so scoping means a filter in every
world query of a program whose FE is frozen. **Recommendation: a lightweight `DelveGraph`
(`DelveNode`, `DelveLane`) in `Core/Delve/`, sharing catalogs and algorithms, not records** — one
table pair (`rpg_delves`, `rpg_delve_rooms`), nothing in the world tables moves, and the map door
later writes a `domainId` into the same delve request.

**Prior art with numbers** (sources in the pass; the ones that decide shape): HOMM3 templates guard
objects by *value* — *"if total AI value is less than 2000, then no guard is generated"*, treasure as
(range, density) triples (heroes.thelazy.net Template_Editor); Enter the Gungeon authors **flows**
(Hollow 4, Proper 8), ~300 rooms on stage 1, secrets at dead ends with a 1/5 any-room chance, corridors
4–30 units, placement backtracks up to 3 times (boristhebrave.com 2019/07/28); Spelunky's 4×4 grid with
the 1–5 solution walk (tinysubversions.com/spelunkyGen); Binding of Isaac `rooms = 3.33 × depth + 5–6`,
five required dead ends, specials placed from the farthest dead end inward, secret-room candidate
weight reduced by 3 with two neighbours and 6 with one (bindingofisaacrebirth.wiki.gg
Level_Generation); Diablo 1 retries under a floor-tile threshold and keeps one set-piece per level
(boristhebrave.com 2019/07/14). **Failure modes:** Compton's *"10,000 bowls of oatmeal"* — perceptual
differentiation is carried by the room *archetype* (name, flavour, climate-typed pool), not the graph
shape; dead-end fatigue — Isaac makes dead ends a resource (specials need them) and this design does
the same (secrets, caches attach there) while hunger per re-walked lane is the Darkest Dungeon respawn
analogue; unreachable rooms — the genre retries, this repo throws.

**Tunables (`data/tuning/dungeon.v1.json`, units in the key, beyond §5):** `bands.dangerBand.*`,
`bands.depth.*.rows.{min,max}`, `bands.width.*.cols.{min,max}`, `bands.branchiness.*.pathWalks`,
`bands.{gate,secret,oneWay}Density.*`, `graph.secretAttachAnyMilli` (Gungeon's 1/5),
`graph.secretAppearMilli` (Gungeon's 90%), `graph.minDeadEnds` (Isaac's 5 — **structural, says so**),
`graph.raid.{solo,pair,quad}.walks`, `sight.lanes`, `sight.scoutLanes`, `bands.sightBand.*.extraLanes`,
`bands.hazardBand.*.hungerPerMille`, `nodes.{kind}.{weightMilli,earliestRow,latestRow}`,
`budget.roomsPerCell.{min,target}` (seedsmith's skew guard, not runtime).

### 11.2 Difficulty — ten named rungs, one ladder, an open tail

**There is no dungeon power ladder, and the owner's phrase resolves to this:** the Delve is a
*consumer* of the one ladder, and its whole difficulty vocabulary is inputs to `Θ_content` plus rule
modifiers that are not power-shaped at all. A room reads terms that are all built: `DangerBand`
(`ContentContext.cs`, `PowerIndexComposer.cs:71-74`, `Wm = 5000‰`, §10 row 23), `WorldTier`,
`ZombossLevel`, `RealmsAdvanced` on the content side with `Wf = Wa` enforced by `ValidateWeights`,
and the species `thetaOffset` (`demon-threat.v1.json`, ten rungs 0…40, §10 row 18). Worked, at the
shipped weights and `B = 0.4`: a `rich` entrance (band 3), tier 1, two realms, a `raider` species
(+13) → `Θ_room = 15 + 5 + 50 = 70`, `Θ_enemy = 83`, `P(83) = 3,616` (the enemy's `MaxHp`),
`contentScale = ×5.32`; the boss room at band 6 → `Θ = 98`, `P = 4,549`, `×6.69` *(the difficulty-ladder spec corrects
this line: the absolute boss band is retired for `depth.bossBandDelta` on the last corridor's band,
so an 11-row `rich` domain's boss composes to `Θ = 100`; and `×5.32` is `contentScale(83)`, the soul
faucet's read of `Θ_enemy`, whereas the loot pipeline reads the room's `Θ`, `×4.24` at Θ 70)*. `P(Θ)` stays in
`long` until `Θ = 214,748,299`, where `PowerLadder` throws.

**The composition point does not exist in production** — `ContentContext` has zero constructors in
`src/`, `WaveCatalog.cs:141` sets `Level = theta` from content only, `speciesBaseTheta` is 0 — so the
Delve is the first producer of a composed `Θ_content`, and its encounter generator is where
`Θ_room + thetaOffset` is finally summed. The consumers are ready and starved: `KillEarn(int
thetaEnemy, …)` and `TryInstantiate(…, int thetaContent, …)` both *require* a `Θ`.

**Does a ten-rung difficulty need a new §10 row? No — if a rung is a `DangerBand` delta.** The
rung table says *"hard = entrance band +1"* and the room's `ContentContext.DangerBand` is
`entranceBand + rowStep·row + bandDelta`. Row 23 already reads the shipped `int` field; the rung only
decides what integer goes in. The alternative — a `rungThetaOffset` column like `demon-threat`'s — is a
second name for `Wm·Δband` and would need a row-18-style reviewed amendment; **refused**, because two
names for one axis is how three curves shipped at once. Choosing the band delta also keeps depth and
difficulty on one axis: a hard delve is *the same place, deeper*.

**Rungs change rules and economy, not only numbers.** Research: StS spends 4 of 20 rungs on
multipliers; Dead Cells 0 of 5; Hades is 15 orthogonal dials; *"rules are expensive and finite, so
spend them where the population actually is; multipliers are free and infinite, so run them
underneath and past the end."* Proposed shape — the band delta moves on **five** of the ten rungs,
every rung flips at least one knob, rung 3 is the identity row so a medium delve's golden moves only
when the entrance band moves:

| # | Rung | `bandDelta` | Modifiers switched on (cumulative) | Precedent |
|---|---|---|---|---|
| 1 | very-easy | −1 (floors at 0) | rest weight ×1.5, hunger ×0.5, unknown-cache pity step ×2 | DD Radiant (grind down, not enemies) |
| 2 | easy | 0 | hunger ×0.75, wild join +50‰ | — |
| 3 | **medium** | 0 | **identity row** | the entrance band as authored |
| 4 | hard | +1 | elite weight 80 → 130‰ | StS A1 |
| 5 | very-hard | +1 | +1 enemy per fight; rest heal 100 → 75% | StS A5 |
| 6 | nightmare | +2 | merchant markup +10%, hunger ×1.25 | StS A11/A16 |
| 7 | hell | +2 | rest every other row; event tables shift one column toward bad; elite kit tier 2 | Dead Cells BC1, StS A15/A18 |
| 8 | abyss | +3 | boss retinue +1 per party, boss `W` +2, spirit drain ×1.5 | Hades Extreme Measures |
| 9 | hopeless | +3 | rest rows only before the boss (the guaranteed row stays — §4.2 is structural); unknown-fight pity ×2; boss kit tier 3 | Dead Cells BC2/BC4, StS A19 |
| 10 | impossible | +4 | double boss (a second boss-class species in the retinue); +2 enemies per fight; elites gain a second action row | StS A20 |

Ownership of every knob, per §10.3's *"both, on separate owners"*: **power** owns only `bandDelta`
(no hp/atk/damage/yield column exists on the table — one would be a private scale, refused);
**encounter design** owns counts, retinue, `W`, double boss, kit tiers, elite/rest/unknown weights;
**economy** owns hunger, rest heal, spirit drain, markup, event severity, wild join — these make *the
player smaller*, which has more distinct flavours than making the enemy bigger. Extraction is
deliberately **off** the ladder (always available, §4.6 — Tarkov's gear fear is the cost of removing
it). The Oath is orthogonal and composable on top.

**"Impossible" is a name, not a ceiling (PS-8).** Rung 10 is the last *authored* rung, not the last
rung: a `tail` block — `startsAfterRung: 10`, `bandStepPerPlus: 1`, `label: "abyss +{n}"`,
`rulesFrozenAtRung: 10` — with `n` a `long` the player picks at commit (M+ key level, D3 GR tier).
No upper bound in the file; the only absolute bound is `PowerLadder.MaxIndex`, which **throws**. Past
the authored rungs only the number moves — D4's shipped shape (four named Torments over a 200-tier
Pit) and the enhancement peril band's `toLevel: null` precedent in this repo. Because the tail is a
band, its reward is still `contentScale(Θ)`: +1 band ≈ +8–9% on `P(Θ)` around Θ 70–100, *flattening*
as Θ grows because `P` is quadratic and the step is additive — the opposite of an exponential ladder's
failure.

**The contest side.** A rung adds a fixed absolute `5·bandDelta` to `Θ_actor − Θ_content`, identical
at realm 1 and realm 1,000 because realms enter both sides. With the shipped `/100` divisor: +5 Θ →
our hit 0.71, theirs 0.97; +10 → 0.40 / 0.99; +20 ("impossible") → 0.047 / 0.999. **A rung is a wall
exactly when the gap can only be closed by an axis that enters both sides** — realms do not close it;
Dave level and runs do — in the SSOT's composition. *(Review:)* today the squad-side contest reads the
**specimen level** (`BattleStatComposer.cs:108` `theta = setup.Index`, fed from
`WebMatchService.cs:396-403`), so the actor-side `Θ_actor` composition is a **wiring gap** and, until
it lands, a +35 gap costs 35 specimen levels per demon on row 27's cost ladder. "Impossible" is a rung that becomes "medium" when the player has earned twenty
Dave levels — the D3 Torment shape. The ladder must **never** add a content axis that grows with the
player's own progression (a `bandDelta` tied to `Θ_actor` would be Last Epoch corruption: *"infinitely
scaling, i.e. eventually unbeatable"*). The player's advantage lives where §4.5 of the SSOT puts it:
gear whose item level read the room's `Θ` at drop, breadth (five kits vs a count-scaled encounter),
roster (element matchups, press-turn).

**Reward side, PS-5:** provisioning and the merchant are priced on `contentScale(Θ_entrance +
Wm·bandDelta)` — the same number the first room's kill pays on; `merchantMarkupMilli` is a ‰ on that
price, not a second curve. The soul faucet is `KillDelta × contentScale(Θ_enemy)` with the species
offset inside, so an abyss room full of `nuisance` species pays less than the same band's `tyrant` —
the stall-farm exploit stays dead without a cap. First-clear per `(domainId, rung)` is a discrete
unlock, not a volume cap.

**Prior art with numbers:** WoW M+ +10% HP and damage per key level, cut to 8% above +20 (wowhead);
D3 Torment T1 819% HP / 396% damage → T16 13,888,770% / 64,725% — durability and lethality split by
×215 (diablowiki); D3 GR ×1.17 HP per tier all the way, damage brackets ×1.13 → ×1.07 → ×1.02
(maxroll); D4 Pit monster level +1 per tier, HP +17% then +32%, damage +4.7% then +2.4% (maxroll); PoE
monster life ×1,901 over 100 levels vs accuracy ×70 — the *contest* stat grows two orders slower than
the *magnitude* stat, the same split as `Θ`/`P(Θ)` (RePoE); D2 −40/−100 resistances and Hell
immunities — difficulty as a *resistance rule*; Grim Dawn −25%/−25% on Elite/Ultimate; Monster Hunter
multiplayer HP 100/163/200/234% for 1–4 hunters (game8); Payday 2 HP ×1/1/2/3/6/6/6 — the last three
rungs differ only by rules because the multiplier had topped out; Doom Eternal's Ultra-Nightmare and
Wolfenstein's Mein Leben apply permadeath *on top of* the top rung — the Oath's exact shape; Last Epoch
corruption and D3 1.0 Inferno as the two named community failures of a pure multiplier ladder.
**No shipped ten-rung rule-heavy ladder exists**; the shape proposed is StS's method at Torment's
length.

**Tunables:** `difficulty.rungs[].{id, bandDelta}`, `.{eliteWeight, restWeight, restHeal,
hungerPerRoom, spiritDrain, merchantMarkup, wildJoin}Milli`, `.enemyCountDelta.{fight,elite}`,
`.bossRetinuePerPartyDelta`, `.bossWDelta`, `.provisionCellsDelta` *(replaces the retired slot count —
the rung removes pack cells, §11.7)*, `.{restEveryOtherRow, restRowsOnlyBeforeBoss, doubleBoss}`,
`.{eventSeverityTier, eliteKitTier, bossKitTier}`, `.unknownPityMilli.*`,
`difficulty.tail.{enabled, startsAfterRung, bandStepPerPlus, rulesFrozenAtRung}`,
`domain.maxRungWithoutOath` (per domain seed; the Oath opens 9–10 and the tail — this replaces §5's
`risk.oath.bandUnlock`). Validator: neighbouring rungs differ in ≥1 modifier; no rung may carry a
day/time key; no rung column may name an actor axis.

### 11.3 Events and quests

**The event anchor** (`data/seed/dungeon/events/<id>.json`, invention pipeline, hand-author ~60 first
toward a ~170 target over 6 kinds × 8 themes = 48 cells): `eventId` · `name`/`flavor` · `theme`
(VALIDATED, the frozen registry) · `climateAffinity` · `kind` (VALIDATED `curio · encounter-event ·
shrine · trap · bargain · story`) · `repeatScope` (`per-delve · per-domain · once-per-player`) ·
`eligibility` (an AUTHORED tree over a VALIDATED closed leaf list — **E3's predicate vocabulary, not a
new one**) · `outcomes[]` (2–4 entries, each `{ordinal ∈ good · mixed · bad · nothing, weightBand ∈
rare · uncommon · even · common · dominant, effects → container ids}`) · `supplyOverride` (a provision
tag that forces an ordinal — Darkest Dungeon's *"bring the herbs"*) · `chainRef` (a `story` kind's next
chapter). **DERIVED:** the ‰ weights (band → ‰ through one table, normalised over the outcomes that
survive eligibility), every magnitude (through `ContentScale` of the room's `Θ`).

**The predicate vocabulary is E3's and it already covers most of what an event needs.**
`ActionRow.cs:52-53` — `ConditionsJson` is *"compiled through E3, not here"*; a malformed tree is
`BadConditionsJson`, *"never silently treated as 'always'"*. The leaf list is closed at twelve
(`PredicateNode.cs:17-31`): `SideIs, TypeIdIs, TypeIdIn, ActorIsKiller, HasStatus, HpBelowMilli,
HpAboveMilli, ElementIs, RowIs, ColIs, IsMindControlled, HoldsStock`, every leaf with a required
`Subject`. Usable for events unchanged: hp band, `HasStatus`, `ElementIs` (climate), `HoldsStock`
(has-provision, `(stockId, minQty)`). **Missing, small and additive:** `BandIs`, `HaulAtLeast`,
`RoomKindIs`, `PartyDownedCount` — *"adding one is a reviewed code change, because each needs a
reader on FactReader"*, and the list has been widened once already this way (`HoldsStock`). A
*second* eligibility vocabulary exists — `EligibilityRule(RequireTags, AnyOfTags, Allow, Deny)`
(`EligibilityRule.cs:20-24`) — and it selects **affixes for a container draw**; use it for "which
outcome containers may this event draw from", never for "may this event appear". **Predicates are
priced** (`action-ideal.md:791-840`, floor 400‰): an outcome gated on a provision the player controls
is priced as if it fires ≥40% of the time, which is the guard against "always bring holy water".

**Outcome effects only through the closed atom vocabulary** — 7 attach points, 16 kinds, 13 triggers
(`AtomKindRegistry.cs:21, :31, :36`; `definitions.md:38`'s "12" is stale, code wins): `resource.delta`
(hunger/spirit drain — *"only `hp` executes until E28 fix #1 ships"*, a **wiring gap** on that
executor), `status.apply`, `shield.grant`, `stat.derived` for a delve-long buff (Battle Full since E12),
`ui.present` for the banner (**Lawn Full, Battle None** — the event banner in a web-mode delve needs a
present sink for the battle runtime, a wiring gap), and a `cache` draws through `LootPipeline`. No
event writes a stat; it grants.

**The deck — two draws, two mechanisms.** *Which event* is a **compositional** draw, not an
`Instantiator` call: filter the catalog by kind, climate, eligibility and repeat scope (per-delve
against this delve's `seen` set; per-domain against `(domainId, eventId)` rows; once-per-player
against `(playerId, eventId)`), apply "no repeat within N rooms" against the trace, then
`WeightedChoice` on `DeriveStream(seed, "dungeon:event:{r}:{c}")`; unknown-room pity rides
`dungeon:unknown:{r}:{c}` with state `(kind → misses)` persisted with the delve. *Which outcome and
what it does*: the outcome index is the same stream's next per-mille against the band-derived weights,
or forced by a `supplyOverride` the player holds and chooses to spend (`HoldsStock` is the check, the
stock decrement is the cost) — and **the chosen outcome's effect bundle is the `Instantiator`
container**: `TryInstantiate(container, lookups, rollSeed from the stream, thetaContent = room Θ)`
freezes every magnitude once. That is the whole answer to *"what part of an event is a container"*:
the effect bundle, and nothing else. **Built:** the expedition band roll as the only precedent
(`ExpeditionResolver.cs:106-148` — the right shape, four inline branches), the stream discipline, the
pity shape, `Instantiator.Draw` (already reused verbatim by `ActionSeeder.cs:19,47`), `WeightedChoice`,
the replay contract, and `rpg_item_stock` — which **exists** (`RpgStore.Items.cs:96, :302`;
`PredicateNode.cs:10`'s "unbuilt" comment is wrong — a comment is not evidence). **Real gap:** the
catalog reader, the four-filter selector, the repeat-scope stores, and the extraction-time withdraw;
everything the selector *calls* exists.

**Scoping a delve-long grant.** `OwnerScope.cs` has eight kinds (`Match, Plant, Zombie, Entity,
Player, Sector, Slot, UniqueActor` — `definitions.md` §6's "seven" is stale); none is "this delve".
Either a ninth `OwnerKind.Delve` (the `UniqueActor` precedent, added for the same durable-but-scoped
reason), or bind on `UniqueActor` with `source = "delve:{id}"` and withdraw by source at extraction —
`ReconcileUniqueEquipmentAtomBindingsUnlocked` already produces and withdraws by source.
**Recommend the second for v1**: no new kind, one `WithdrawSource` at extraction.

**Quests — no quest system exists, whole.** `grep -rni "quest|objective|bounty" src/` finds only
prose, `ItemCategoryTable.cs:29`'s unused `"quest"` category, `SlotUnlock.cs:3-13`'s
`ISlotUnlockRule` implemented by nothing, and `decisions.md:27`'s *"Not RPG quests"* — the lawn
activity facts are explicitly **not** the quest truth. **The quest anchor:** `objectiveTemplate`
(VALIDATED, closed: `explore-rooms · cleanse-fights · gather-curio-kind · kill-boss ·
extract-with-item-kind · bring-demon-home-alive · finish-under-hunger · survive-no-downed ·
spend-no-provision`) · `targetRef` (a **kind**, never a number, or `none`) · `countBand` (`few · some
· most · all` → N from the domain's row×width; DD's "90% of rooms" is `most`) · `rewardBand` (a tier
window resolved through `LootPipeline` with a `dungeon-quest` source kind — never a gold number) ·
`scope` (`delve · domain · roster`) · `prereqRefs`/`chainRef` · `repeatScope`. Three scopes are three
fact sources on one evaluator, exactly how `FactReader` already works (*"resolved at evaluation setup
… never I/O from inside the leaf"*). **The minimal runtime in three sentences:** a quest is a
predicate over a **report** (the delve report, extended with room-kind counts, curio gathers, the
downed list, hunger at extraction, haul by kind) plus a reward manifest; evaluation is pure and
idempotent on `(playerId, questId, delveId)` under the expedition exactly-once envelope
(`RpgStore.Expeditions.cs:270-274`); rewards go through `LootPipeline.Resolve` with a server-derived
correlation `loot:delve:{delveId}:quest:{questId}` (spelling settled by `spec-dungeon-loot.md` §3, source kind `dungeon-quest`), never a hand-written grant. Doran and Parberry's
structural analysis (>750 quests, 9 motivations each with 2–7 strategies as verb-noun pairs) decides
the template list: our templates are their strategies with the noun replaced by a kind ref and the
count by a band.

**Prior art with numbers:** StS Act 1/2/3 carry 12/16/8 exclusive events plus 16 multi-act *(review-corrected)*, 6 shrines once per act, 15 one-time
per run; *The Cleric* (35g → heal 25% max HP; 50g → remove a card; Leave) and *Golden Idol* (take →
relic, then Outrun / Smash 25% max-HP damage / Hide −8% max HP); **StS2's designers: *"we're trying to
avoid having any options where you simply do nothing and pass through"*** — a free "Leave" is the
failure. FTL blue options gate on crew and equipment (Rock 5, Engi 7, Slug 10 blue events) — the
`supplyOverride` shape. Hades Chaos gates: pay HP now, curse for 3–4 encounters, then a run-long
blessing — a `bargain` kind, `status.apply` now, `stat.derived` later. DD quests: six types; resolve
gain 2/4/8 · 3/6/12 · 4/8/16 by length × difficulty. D3 bounties: 5 per act, one Horadric Cache per
act (hot-fixed from one per bounty). Skyrim Radiant: *"go to place X, kill person Y"* — a template with
a proper-noun slot and no motivation, which `targetRef`-as-kind plus theme avoids.

**Validator rules, from the failure modes:** every event's outcome list must contain ≥1 `bad`-or-
`mixed` *and* ≥1 `good`; a `nothing`/Leave option is refused unless the kind is `story`; a supply
override is priced at the 400‰ floor and costs pack cells (§11.7); no event may *gate* the boss or a
room kind; quests reward, never unlock, in v1; distribution skew checked as actual-vs-declared per
(kind × theme).

**Tunables:** `events.weightBand.{rare,uncommon,even,common,dominant}Milli`, `events.noRepeatRooms`,
`events.offeredPerRoom`, `quests.offeredAtEntry`, `quests.countBand.{few,some,most,all}Milli`,
`quests.rewardBand.*` (tier-window ordinals); event-leaf `predicateFrequency` is a row in the existing
`power_predicate_frequency` table, not a new file.

### 11.4 Encounters — boss, enemy party and enemy group are one generator

**The argument, with evidence.** A party, a group and a boss fight are the same object in the engine
today: `WaveDef(WaveId, Name, ContentIndex, Enemies, Profile, W)` (`WaveCatalog.cs:31`) — a flat list
of setups plus a `Θ`; the expedition boss is not a kind, it is a wave id (`ExpeditionResolver.cs:167`
`BossWaveId = "rift-tyrant"`); `BattleActorSetup` carries no kind discriminator. So the three asks are
**one seed shape with three `formation` values and one `role` value.** No separate boss corpus.

**No fourth role vocabulary.** Three exist and cover the slot question between them: the 12 aptitudes
→ 3 postures (Force · Finesse · Bastion, `posture` DERIVED from `aptitudePrimary`,
`data/seed/aptitudes/roster.json`); the anchor ordinals `reach` (melee · short · long · siege),
`targetPreference` (frontline · backline · swarm · elite · structure · indiscriminate), `attackTempo`
(five); and `ActionCategory` / the 8 `ActionTag`s for what a *kit* does. **A slot is a filter tuple
over anchor ordinals, never a new noun:** "front-line" is `posture: Bastion ∧ reach: melee|short`;
"striker" is `Force`; "skirmisher" is `Finesse`; "artillery" is `reach: long|siege ∧ targetPreference:
backline`. **Support, controller and summoner have no anchor expression today** — nothing on the
anchor says "heals" — and only become filterable when `species-effects` attaches containers whose atoms
carry `ActionTag`s. Real gap, stated rather than papered over with a new enum.

**The encounter anchor** (`data/seed/dungeon/encounters/<id>.json` — the wave-as-data fix Part I
demands): `formation` (VALIDATED `pack · party · boss`) · `slots[]` each `{posture, reach?,
targetPreference?, countBand ∈ one · pair · few · many}` · `rankOrder` (AUTHORED ordinal, slot ids
front → back) · `elementSpread` (`mono · dual · rainbow`, resolved against the room climate) ·
`synergyHint` (a pair of trait ids from `TraitBattleCatalog`'s 14) · `tempo` (filters species
`attackTempo`; the `W` override is derived, never authored) · `threatWindow` (`{floorRung, ceilRung}`
in threat nouns) · a `role: boss` slot + `retinueRule` · `affixRoll` (a rarity rung id or `none` — the
elite dial). Every weight, ‰, HP, count and Θ is DERIVED or GENERATED.

**Runtime resolution.** The room hands `(Θ_content, climate, seed)`; the generator draws
`dungeon:encounter:{r}:{c}`, filters the anchor corpus per slot (`threatBand ∈ threatWindow`, posture,
reach, target preference, element ∈ spread(climate)), picks `count ∈ countBand`, draws species, and
emits `BattleActorSetup`s exactly as `WaveCatalog.Enemies` does (`WaveCatalog.cs:125-153`) with stats
from `BattleRuleset.BaseHp/Atk/Defense(θ)` — **where `θ = Θ_room + thetaOffset(species)`**, the sum
nothing computes today (§11.2). **Corpus facts the generator must refuse loudly, not default:** over
the anchors on disk, `threatBand` is present on a minority of files (125 of 502 files carry the key;
the encounter pass counted 657 of 841 index entries without it), and `DemonThreatTuning.OffsetFor`
falls back to `inferredDefaultRung` (rung 4, `raider`, +13) for the rest — so a `threatWindow` filter
would see most of the corpus as `raider`; `reach: siege` has **zero** anchors, so a `siege` slot can
never fill; `targetPreference` and `RangeCells` have **zero readers in `src/`** — the two role axes
exist and nothing in battle consumes them (wiring gap).

**Boss is a role, not a species field.** `DemonDeployMode.HypnoAlly` is a **lawn** expression
(`demon-system-map.md:7`, *"designated boss-class species deploy as hypno-zombie allies"*; assigned by
rank at `DemonSpeciesGenerator.cs:78`, 120 HypnoAlly / 721 PlantAvatar) — it says how a captured demon
walks on the lawn, not that the species is a delve boss. A boss slot is filled by `threatWindow` from
the top rungs (tyrant … calamity: 23 anchors today, thin but real) at the room's `boss-lair` band:
`Θ` = 30 (`Wm·6`) + up to +40, both shipped. **The shipped threat ladder already covers "boss"**;
nothing new joins §10. The boss **kit**, as ordinals: `build` (a `ZombossPattern` id — 9 exist, 3 pure
+ 6 mixed, `ZombossPatterns.cs:33-77`; `ZombossCommanderAllocation.Refresh(theta, tuning)` has one
production caller and nothing feeds a wave actor an allocation — wiring gap) · `phaseCount` (`none ·
two · three`; precedent: `berserker`'s two HP thresholds, `TraitBattleCatalog.cs:46-47` — a phase *is*
an HP-threshold grant; `BattleStatModifierLedger` and `RecomposeDerived` are the seam *"a real trigger
… calls this at the moment it happens"*) · `phaseTrigger` (`hp-threshold · round · ally-down`) ·
`signatureAction` (an action id; **real gap** for a phase that *changes the kit* — loadouts bind once
at `BindContainers`, no mid-battle rebind) · `affixContainer` · `retinueRule` (`{slotRef, perParty:
one · pair}` — count and `W`, never HP, §8 Q3). **The Darkest Dungeon boss shapes that need engine
work are the base-defense program's builds, named as dependencies:** a *summoner* boss (Necromancer:
a skeleton per attack, cap 3, life-linked) needs the roster that changes mid-battle (`Actors.Add`,
zero hits); a *fixture* boss (Hag's pot, Prophet's pews 25/40/55 HP) needs the non-acting destructible
combatant.

**Elite = one slot with an affix roll**, D2 champion-pack shape: one marked monster, ordinary
minions. The affixes roll through **`Instantiator.TryInstantiate`** — the item roll, unchanged — with
`rollSeed` from the encounter stream and `thetaContent` the room's Θ; the rung sets *"a count band, a
tier floor, and a tier ceiling. Nothing else"* (`ssot-rarity.md:101`). The instance's atoms reach the
actor through the same grant path `BindContainers` uses. **Two blockers:** `ContainerKind` has no
enemy kind (`ContainerRow.cs:7-15`; a seventh kind or ride `trait` — owner call), and the affix
library that would be drawn from holds **two entries** (`data/seed/effects/affixes/all.json`) — D2's
monster-mod vocabulary (Extra Fast, Cursed, Fire Enchanted, Multishot, Stone Skin, Mana Burn) is the
*identity* list `effect-pipeline`'s affix authoring would need to grow; without it an elite has nothing
to roll. PoE 3.20's rule after removing Archnemesis — *"mods do one specific thing"*, 1–2 in acts,
2–3 in maps — is the guard against the unwinnable combo, plus `affix.exclusiveTags` for pairs that may
not co-roll.

**Formation on a null board — the cheap version and its cost.** There is no geometry:
`PositionOf(actorKey) => null` (`BattleRunState.cs:474`), *"which is what makes NearestEnemy's own
SourceOrder fallback the live behavior today"*. What exists is **`SideIndex` — "position within its own
side (adjacency)"** (`BattleEngine.cs:50`), set from setup order and already consumed by `loyal`'s
`GuardsAdjacentAlly`. **Darkest Dungeon proves a 1-D rank per side makes formation a decision** — in-
repo, computed over 110 skills: rank 4 usable by 62% of skills and hit by 57%; 31% usable from anywhere;
19% of skills displace. **The cheap version: rank = `SideIndex`**, `rankOrder` decides emit order,
`reach` becomes a contiguous target mask over the enemy's ranks, `targetPreference` the default pick
within the mask. No board, no cell, no `A10`. **Cost, honestly:** it fixes the meaning of setup order
(new content only — no golden moves); displacement (knockback, pull) is a write to `SideIndex`, which is
`get`-only — a small engine change; size-2 enemies (DD's Large, up to Unimaginable = 4 ranks) need a
`rankSpan` on the setup; when `A10`'s 2-D board arrives, rank collapses into column and nothing
authored is lost. What a real board buys that rank cannot — lanes, AoE by cell, movement as a turn —
none of the owner's references (DD, SMT, Persona) use.

**Grid density is inverted here.** Composition cells = posture multisets over 5 slots (21) × spread
(3) × formation (3) × tempo (3) = 567; an encounter corpus is a *sampler*, not a roster — each cell
resolves at runtime against hundreds of anchors. The failure to measure is **too few distinct shapes**,
not too many per cell. Closed-loop metric: distinct `(postureMultiset, spread, formation)` cells
reachable per domain tier; a domain whose fights all land in three cells fails. StS's sibling rule
(no two siblings alike) applies to encounters on one graph row.

**Pack vs party vs boss — three formation rows, one generator:** `pack` = one posture, `many` (4–7,
matching the shipped 4/6/7/6), flat, the wild room and ordinary fights; `party` = 3–5 distinct slots,
≤1 posture repeated, authored front → back, elite fights and rival parties (the same five-slot shape
the player brings — the SaGa / FF VI symmetry, free); `boss` = one `role: boss` + retinue × parties.
HypnoAlly species are enemies like any other; the generator reads `threatBand`, never `deployMode`.

**Prior art with numbers:** DD Necromancer 105/158/215 HP by tier, summons ≤3 life-linked skeletons;
Hag 66/99/135 HP, pot drains 8.75% max HP per action; Prophet pews 25/40/55 HP paying up to 7,500 gold
(darkestdungeon.wiki.gg); TFT — 65% of 85 champions carry exactly two traits, first breakpoint never
above 3 (in-repo); SMT Nocturne Matador — a null costs the turn, a miss costs 2 icons; Persona 5 —
*"all bosses are immune to instant death"*, mid-bosses refuse negotiation; PoE 3.20 Archnemesis removed
after two leagues — bundled mods stacked into immune, over-tanky, loot-converting rares; Etrian FOEs
step when you step and join a fight in progress; XCOM 2 pods = leader + followers, alien-led pods roll
the follower once (the same-shape failure); L4D Director build-up → peak → fade → relax 30–45 s, ≤30
commons; Monster Hunter tempered — same moves, more HP — the stat-only modifier (*"a faster Banshee is
still a Banshee"*).

**Tunables (`data/tuning/encounter.v1.json`):** `slot.countBand.{one,pair,few,many}.{min,max}`,
`threatWindow.defaultRungs`, `threatWindow.bossFloorRung`, `spread.{mono,dual,rainbow}.offClimateMilli`,
`tempo.*.wOverride`, `retinue.perParty.{one,pair}`, `affix.eliteRung.{tier}`, `affix.exclusiveTags[]`,
`phase.hpThresholdMilli[]`, `summon.capPerBoss` (DD's 3; structural until the roster-add path exists),
`rank.spanMax` (structural, says so), `pack.sameSpeciesMaxMilli` (XCOM guard).

### 11.5 Objects and supplies

**Objects split by a rule, not a list.** *An object is a STRUCTURE if it is still there after you
interact with it and it changes how the room is fought; it is a CURIO if the interaction is the whole
of it — one seeded draw, then it is spent.* A door you open stays a door; a sarcophagus you open is an
outcome table. Obstacle, cover, door/gate, trap, totem, altar, barricade → structures, reusing the
structure anchor **whole** (`structure-seed-ideal.md` §5: `footprint`, `coverTier`, derived
`obstacleVerbs` from base-defense §5.18's closed eight — BLOCK · SLOW · BLOCK-LOF · COVER · DENY ·
CHANNEL · CONCEAL · BITE — and decisions 11–15: a new kind of actor, no level, receives nothing, no
ownership, garrison to use). Chest, shrine-offering, bookcase, iron maiden, corpse, wild-demon bargain
→ curios, the event deck's rows (§11.3). The validator enforces the line: a row with `obstacleVerbs ≠
none` or a garrison action is a structure; a row whose only content is an outcome table is an event.

**Dungeon use adds one VALIDATED `interaction` axis** — `open · disarm · pray · loot · destroy ·
garrison · none` — not a second obstacle-verb list: obstacle verbs say what the object does to
movement and fire; interaction verbs say what an actor may do *to* it, each mapped onto an action from
the corpus with a usability predicate from E3's twelve leaves (`HasStatus`, `HpBelowMilli`,
`HoldsStock` are the ones an object needs). `garrison` is decision 15, `destroy` is decision 12,
`open`/`disarm` are `key`-supply overrides. **Placement, both readings:** a room has no cells today
(*"with no board every range check must pass"*, `decisions.md` Action-model row), so a placed object
means exactly two things now — a **pre-fight modifier** (its `coverTier` is a flat `combat.dodge.*`
delta on the side the seed assigns it to, a contest, *"exactly as decisive at Θ=200 as at Θ=1"*; its
obstacle verbs collapse to inert, honestly) and an **interaction prompt** between fights. When `A10`
lands, the same row gains a cell footprint and BLOCK/SLOW/BLOCK-LOF start to bite with zero
re-authoring — `footprint` and `coverTier` were ordinals from day one. **Grid density:** dungeon
structures span five of the ten roles (Defend, Deny, Enable, See, Move); `interaction` is correlated
with role like `controlPoint` and is therefore DERIVED, not an axis; **~18–20 delve structure types
sit at 3.6–4.0 per cell**; element is the honest second axis if more are wanted. **Correction to
`structure-seed-ideal.md:73`:** `StructureCatalog.cs:82-164` holds **seven** rows and `StructureKind`
has **three** values (`LoamSource, Storage, Yield`), not four and two; the conclusion — *"the first
module is making structures seed content at all"* — stands.

**Supplies are the consumable anchor plus three fields. No seventh vocabulary.** The real shape,
`data/seed/items/consumables/k1.json:24-39`: `id · nameKey · name · classId · useContext[] · family
(atom.*) · powerBand · manifestCost · tags` — not one magnitude (`manifestCost: 1` is a structural
count). Six classes, closed: `restore · draught · ward · board · revive · utility`; `useContext` is a
closed set whose widening *"is additive and never invalidates a row"*. A `supplyKind` enum (ration /
antidote / key / charm / ward / bait) would be a seventh vocabulary beside `classId × family` —
**refused**; mapping instead: ration → `restore` feeding `hunger`; bandage → `restore` on hp; antidote
→ `utility` (status clear); key → `utility` (override only, no atoms); charm → `draught` (delve-scoped
`stat.derived`); ward → `ward` (`shield.grant`); bait → `utility` (wild-room override). Torch-analog:
**none** — §4.5 refuses a light meter; hunger is the one supply gauge. The three additions:
`useContext` widened with `rest` and `curio`; `overrideTags[]` (VALIDATED — which override tags this
supply satisfies: `herbs`, `key`, `holy`, `bait`, `watch`); `sizeBand`/`stackBand` **DERIVED** from
`classId` (+ `powerBand`) via tuning — never authored as w×h — and price DERIVED from grade × class ×
`contentScale(Θ_domain)` (`seed-contract.md` §2.1: *"price · weight · durability · salvage yield —
DERIVED"*). **The override lives on the event row, by supply tag**, not on the supply by event id —
the only direction `seed-contract.md` §7.1's *"forward references: forbidden"* allows, and it reads
the same `HoldsStock` leaf eligibility already needs: one predicate, no second roll. A new antidote
variant qualifies for every `antivenom`-tagged curio without touching an event file.

**The usable mechanism is `OnActivate`, and it is mostly wired for web mode.** `AtomKind.cs:90`
declares it; `AtomKindRegistry.cs:47-48` allows it on `resource.delta / status.apply / shield.grant /
stat.modify`; **battle raises it** (`Actions/BasicAttack.cs:101`, `Trigger = AtomTriggers.OnActivate`);
`resource.delta` is Battle = Full. The injector side is still inert (`MoveDrainHost.cs:14` *"INERT"*)
and irrelevant to a web-mode delve. So *using a supply* = one action whose container fires on
`OnActivate`, whose usability is `HoldsStock`, whose cost is the item. **Missing, both named:** `A3`'s
cost model *"has no shape for spending an item"* (`ActionCostRow(ActionId, ResourceId, AmountSpec,
When, AllowLethal)` — resource ids only, `ActionRow.cs:122-123`), and a `consumable` `ContainerKind`
(D27 mints four, not this one). Wiring gaps: `status.clear` stays on `AtomTriggers.Events`, so an
antidote cannot fire on `OnActivate` today (`AtomKindRegistry.cs:25-28`); `useContext: battle` is
refused at import until the action layer exists (`UseContextUnsupported`). **Which pool a supply
refills** (`resource-hub-ssot.md` §2, normative): ration → `hunger`; ward → not a pool, a shield;
spirit tonic → `spirit` (*"depleted only by harm"* — so only a supply restores it, never regen).

**The pack tension, from the owner's clarification.** There is no separate provision slot count: a
supply consumes `sizeBand` cells per stack of the per-delve carry grid (§11.7), and every cell a
ration occupies on entry is a cell a relic cannot occupy on exit. Darkest Dungeon's **16 slots shared
by provisions and loot** is the exact precedent, and this is §4.5's risk dial without a torch: pack
light and starve, pack heavy and leave gear in the dark. `manifestCost` (dispatch-time manifest
places) and `sizeBand` (delve-time cells) are two fields for two moments, not one renamed. Unused
supplies come home and keep their cells (DD refunds ~5%, which is why nobody thinks about it) — a
stock that competes with loot for the way out. Provisioning before entry is the soul sink, priced per
class × grade × `contentScale(Θ_domain)` (PS-5); the in-delve `merchant` sells the same rows at
`merchant.markupMilli`.

**Rest is actions from the corpus, not a system.** DD camping: **12 respite points per camp**, shared
skills cost 2, firewood 0/1/2 by length, 33% night ambush. Our shape: a `rest` room grants
`rest.activations` action uses; camp skills are ordinary actions with `useContext: rest`, paid from the
six pools, and **they occupy the five equipped slots** (`LoadoutSet.cs:40` `MaxSize = 5`, a structural
const) — DD's "4 of 7 camping skills" tension for free; the ambush is an event-deck row the `rest`
room may draw; a `watch` action applies a status the ambush's eligibility reads through `HasStatus`.

**Prior art with numbers:** DD provisions — Food 75g ×12, Torch 75g ×8, Shovel 250g ×4, Skeleton Key
200g ×6, Holy Water 150g ×6, Herbs 200g ×6, Bandage/Antivenom 150g ×6, Laudanum 100g ×6; recommended
torches 5/10/14 and food 8/16/24 by length; each supply overrides 5–9 curios — the density an override
registry should match (darkestdungeon.wiki.gg Provisions, Curios). **DD2 removed the provisioning shop
entirely** — the strongest evidence that always-buy provisioning is a solved, boring decision. D2 belt
4 rows, tomes hold 20 scrolls; Spelunky 2 starts 4 bombs / 4 ropes, Bomb Bag $2,250 + $250 per level —
the canonical risk-supply pair; Tarkov meds as pools with charges (IFAK 300, Salewa 400, Grizzly
1,800); Persona 5 SP Adhesive — the "make attrition go away" accessory players regard as mandatory.

**Validator rules:** a supply that overrides nothing *and* refills nothing is refused (`overrideTags
= none` and no `resource.delta` atom); per-domain override tags so the shopping list differs by
climate; distribution `budget` per class and per tag.

**Tunables:** `pack.sizeByClass.*` (cells), `pack.sizeBandStep`, `pack.stackByClass.*`,
`provision.priceByClass.*`, `provision.gradeStepMilli`, `merchant.markupMilli`, `rest.activations`,
`rest.ambushMilli`; the override-tag registry under `data/seed/dungeon/_registry/`; structure ordinal
bands in `data/tuning/structure-seed.v{n}.json` (**does not exist yet**).

### 11.6 Recruit, capture, summon

**Fifteen built rows, four wiring gaps, three real gaps.** Built: the wild-join coin
(`ExpeditionResolver.cs:121`, `wildJoinMilli` 250‰) minting through `MintDemonUnlocked` with traits
from `SummonRoller.RollTraits` (*"shared by summons and wild joins"*); a free-string `Origin` with
`summon`/`fusion`/`expedition` in use and `capture` **reserved and never written**
(`spec-demon-core.md:29`); the wild pool excluding capture-only species and the top rung; capture-only
species as silhouettes (`CaptureOnly`, *"≤15% capture-only, never legendary"*); personality derived,
never rolled live (`ContractPolicy.cs:195-196` `PersonalityFor(instanceId)`); the three contract
conditions and the free auto-bind on mint when a slot is free; price-capped capacity
(`ContractPolicy.cs:168-177`); the predicate leaves `HpBelowMilli`/`HasStatus`/`HoldsStock` with a
required `Subject`; `SummonRoller` with two guards at ordinals 70 and 90 and a banner shape carrying
`HasElementFocus`; `Retreated` as "leaves the fight alive"; `spawn.entity` as a registered Board atom
(`AtomKindRegistry.cs:683`) and the `runner` slot the paperdoll spec names as *"the stolon, the exact
botanical word for a summon slot"*. **Wiring gaps:** the wild path has no *talk* — one coin, no inputs;
`SummonRoller.RollSpecies` pools from the whole catalog (a domain pool is one filter argument, not a
new roller); the anchor has no temperament field (the `PersonalityFor` one-liner is the seam, keyed by
room instead of `instanceId`); `ActionCostRow` is resource-only, so `HoldsStock` can *gate* on a seal
but nothing *consumes* one. **Real gaps:** no capture verb anywhere (249 hits for "capture" in `src/`,
every one the board-capture / dump sense); a roster that changes mid-battle (`Actors` materialised
once at `BattleRunState.cs:182-184`, zero `Actors.Add` hits); no battle-kernel executor for a Board
atom (`BattlefieldScopeExecutor.cs:11` executes against the injector's live capture fields).

**Recruit — the wild room is a talk, not a coin.** A short decision tree the player walks, never a
retry loop; every roll draws from `dungeon:wild:{r}:{c}`, advanced once per decision; the room is
consumed on exit so a client cannot re-roll. Four inputs, all derived, none live: **personality** (the
same one-liner with `"dungeon:wild:{r}:{c}"` as key, recorded on the mint so the contract row agrees
with the talk); **threat vs you** (`Δ = Θ_wild − Θ_party`, a difference-based contest bucketed into
tunable bands `far below … far above` — SMT's *"cannot recruit above your level"* becomes the *far
above* row's weights, which may set `joins` to 0‰, a data decision); **tide** (optional, v1 off — one
ordinal per domain from the seed, Nocturne's Kagutsuchi as the shape); **the offer** (the SMT four:
souls · spirit via `resource.delta` · a supply from the pack · a released contract from the home roster (*review*: under decision 12 no demon rides in the pack),
released — a real sink), with a 5×4 preference table of ordinals (`craves / accepts / scorns`), P5's
Upbeat-likes-Funny matrix reshaped onto what we own. **Outcome table, ordinals → ‰:** `joins · takes
and leaves · flees (an essence shed) · attacks (a fight at the room's band, no re-talk)`; the species
seed carries a disposition ordinal (`eager · open · wary · hostile`) mapping to a base row, and the Δ
band, tide and preference each shift the row **one rung**, never a multiplier. A `craves` offer never
guarantees `joins` — SMT V's own rule, *"paying can still fail and refusing can still succeed"*. The one
guarantee is Darkest Dungeon's: a provision override, costing pack cells. **What a recruit is:**
minted at room close with `Origin = "delve"`, traits from `RollTraits`, effects later through the one
SDK (`player-materialise`). **Recommend: pack cell, unbound, counted as haul** — the Pokémon box, not
the Diablo mercenary — because the contract gate already refuses an unbound demon on every fielding
path and binding mid-delve would be free, so a mid-delve fighter is a **free reinforcement**: the exact
*"recruit is always better than fight"* degenerate. The recruit fights for nobody until extraction,
binds then if a slot is free, and is **haul** — *"a wiped party drops its haul in the dark"* applies,
or the wild room is the one risk-free faucet in the Delve.

**Capture — weaken, then bind.** The demon map's row was written for the lawn (*"capture conditions
are read Hot, capture resolution is Cold"*); in a delve both facts are a read of `BattleRunState`
(`ActorState.Hp`, the battle's `StatusRuntime`) — RPG layer, one server-side resolve. **Capture is an
action in the corpus**, not a verb on the engine: `Relation = Enemy`; usability `and(hpBelowMilli(target,
X), hasStatus(target))` plus `holdsStock(seal)`; cost a **seal** (a `consumable` provision, consumed on
attempt — the item-cost row is the one wiring gap; souls through `SoulSinkPolicy` on the room's Θ is
the fallback); on success the target leaves the fight alive — the `Retreated` shape from a new producer
— and the report carries a `captured` row the room-close mints from; **no die event, so no
`KillEarn`** — a captured demon pays no kill souls, the trade that keeps fighting worth doing. **Roll
shape — Pokémon's, as prior art only:** pret/pokeemerald `a = ((3·maxHP − 2·HP)·catchRate·ball·status)
/ (3·maxHP)`, `a ≥ 255` guarantees, else four shakes at `b = 65536 / (255/a)^(3/16)`; status ×2.5 /
×1.5; Great ×1.5, Ultra ×2. Here only the shape survives: hp term → an **hp band** ordinal; level term →
the same **Δ band** as recruit; status → a **count band**; `‰ = capture.chanceMilli[hpBand][ΔBand] +
statusBonusMilli[countBand] + sealTierShift` — a table lookup, integer per-mille, one draw on
`dungeon:capture:{r}:{c}:{attempt}`. No exponent, no float. **Does rarity lower the chance?** Pokémon
yes, SMT no. **Recommend no**: rarity buys breadth, never power; scarcity of high rungs lives in the
*encounter* roll (the 840/150/10 shape of `RollWildSpecies`), the power term is the threat band, already
in Δ. **Spam:** every attempt costs a seal and an action-point turn, plus a **per-target ramp** — each
failed attempt shifts the next roll one band down (DD2's *"−10% resist per survived hit"* inverted).
**No cross-delve pity**: the summon guards exist because a pull has no setup the player controls; a
capture has three (HP, status, seal).

**Summon.** (1) An **altar room** — the Delve's third in-run soul sink after provisioning and the
merchant — runs the shipped `SummonRoller.Roll` against a domain-bound banner (the domain's climate is
the `HasElementFocus`; the pool filter gains a species predicate from the domain seed), priced through
`SoulSinkPolicy` on the room's Θ (PS-5); results mint with `Origin = "delve"` into the pack as haul.
**Pity shared with the Sanctum altar**, per the spec's own *"persist across sessions and banners"* — a
second pity stock is a second thing to explain for no new decision. What the altar adds is P6's two
horizons on souls: spend the unbanked haul in the dark, or carry it out. (2) **A demon summoning in
battle** (the `runner` slot firing `spawn.entity`) is blocked by one real gap and this document must
not call it impossible: *"a roster that can change mid-battle … one build serves both"*
(`base-defense-ideal.md` §3.4). The build: `BattleRunState.Spawn(setup, side, partyIndex)` appending
to `Actors`/`ByKey`, seating the newcomer under the profile's rule, emitting the existing `spawn`
event, immediately counted by `AnyActive`; plus a Board-atom executor for the battle kernel. (3) A boss
calling its retinue mid-fight is the same gap from the enemy side.

**Economy.** Bound = a daily sink (`baseUpkeepPerDay` per rung × personality, floored at 1); unbound =
free and frozen; capacity price-capped. **A binding discount for a delve-caught demon — recommend
against**: mint-time bind is already free, and a per-origin upkeep multiplier makes *origin* a power
axis on the roster economy with no sink beside it. If "a pact struck in the dark" should mean
something, the lever is **personality** — the recruit's comes from the room and the talk. **A P2 note
for the contracts owner, not this program's fix:** slot and ritual prices are `SoulSinkPolicy`-scaled
on Θ; `UpkeepPerDay` is not (`ContractPolicy.cs:133-134`, flat `int`) — a deep delve mints heirlooms
whose 12/day tribute is flat while the souls that fund it are `contentScale`-scaled. The Delve widens
an existing gap; it does not create it.

**Prior art with numbers:** SMT V — join only at your level or lower, demands escalate and may repeat;
SMT III — full Kagutsuchi makes demons *"act drunk, raise their attack"* and refuse talk, new phase
*"calm and easy for conversation"*; fusion accidents 8/256 off full moon, 16/256 on; Persona 5 — the
published 4×4 personality × answer matrix (Upbeat likes Funny; Irritable likes Serious; Timid likes
Kind; Gloomy likes Vague), a level gate lifted by Sun rank 10, 21 of 210 Personas (10%) gated behind
maxed confidants; Pokémon obedience `P ≈ obedienceLevel / (level + obedienceLevel)`; Dragon Quest
Monsters scouting as the attack calculation re-read as a percentage; Digimon World deterministic
recruit-by-request with prosperity points 1/2/3 by stage; DD2 recruits at inns from a random pool;
Etrian guild 30 / party 5; SMT V 26 owned / 3 fielded, *"the stock size itself is an unlockable"*; WoW
hunter loyalty (removed 3.0.2) and happiness (removed 4.1.0) — the in-repo finding that surviving agency
systems express agency as an unlock, never as a decaying meter. **Failure modes:** negotiation as a
solved lookup (the answer raises a *band*, never *sets* the outcome; the one sure path is a priced
provision); capture spam (seal + AP + ramp); recruit-beats-fight (no kill souls, no XP, unbound in the
pack).

**Tunables:** `wild.outcome.{eager,open,wary,hostile}.{joins,takesLeaves,flees,attacks}Milli`,
`wild.deltaBands[]`, `wild.deltaShiftRungs[]`, `wild.offerPreference.{personality}.{souls,spirit,item,
demon}`, `wild.offer.soulsMilliOfRoomYield`, `wild.offer.spiritMilli`, `wild.tide.{enabled,
shiftRungs[]}`, `wild.provisionOverrideTag`, `capture.usableBelowMilli`,
`capture.chanceMilli[hpBand][deltaBand]`, `capture.statusBonusMilli[countBand]`,
`capture.sealTierShiftMilli[]`, `capture.failStepBands`, `altar.{bannerId, poolFromDomain,
pullPriceSouls, sharedPity}`.

### 11.7 Loot — domain tables, special items, and the loot pack

**Dungeon loot tables are almost entirely a wiring gap.** Built: the authored input shape (`d1.json`
`{entryKind, role, frame, dropBand}`, 40 tables / 92 groups / 468 entries across `d1–d4`; `dropBand`
a five-value enum with its own `weightTable` in `bands.v1.json`); `rarityFloor` already authored on 17
entries and applied *first* by `RarityDraw`; the generated runtime shape with `sources[]` rows
`{sourceKind, sourceId, tableId, contentLevel, firstClearGrant}` and step 4's lookup by `loot_source`
(`LootPipeline.cs:158, :178`); step 3 reading content only; **`contentLevel` *is* `Θ_content`**
(`WaveCatalog.cs:6-7`) so a room's content level is its composed Θ with `Wm·DangerBand` inside it;
volume top-level only; the first-clear grant, deterministic, once per `(player_id, source_kind,
source_id)`; no caps by ruling; pity keyed on rung per player; server-derived correlation. **Wiring
gaps, each naming its line:** two source-kind lists that both need `dungeon-room` and `dungeon-clear`
(`LootPipeline.cs:91-98` throws on unknown; `DropTableValidator.cs:52-53` `KnownSourceKinds` rejects
`drop.unknown-source-kind`) with correlations `loot:delve:{delveId}:{r}:{c}` and
`loot:delve:{delveId}:clear`; `loot_source` rows are static seed rows (`ImportLootCorpus` deletes and
re-inserts) so the delve host **synthesizes** the `LootSourceRow` with the room's Θ when it builds
`LootContentView.Sources` — the view is host-built and nothing requires the dictionary to come from the
table; no production host builds the view and `Mint` is a delegate slot constructed only in tests; step
6 ignores an authored base-type-set `ref` (`LootPipeline.cs:306` reads `BaseTypesFor(frame, role)`,
`entry.RefId` unread) — resolving it is how *"the vault drops only heartwood and bark"* is expressed
without a multiplier; the `boss` affix channel is *"authored and inert — a WIRING GAP, not a wall"*
(`DropTableModel.cs:34-36`); the `unique` entry kind is refused by name until module 17 (64 authored
rows nobody can import); the `dropBand → weight` emitter does not exist yet (`LootCorpus.cs`), though
the mapping data does. **Real gap:** the domain catalog naming tables per room kind; `consumable`
payloads (module 18) — provisions cannot drop or be carried as items until it lands.

**What "special" means with no multiplier on the rung** (`ssot-rarity.md` §3.6: *"a multiplier on the
rung makes rarity dominant and destroys the overlap"*): (1) **domain-themed uniques** — the uniques
corpus is already partitioned theme × rung (18 files, `charnel-bloom-70.json`, `umbral-swarm-90.json`);
the frozen theme registry maps a climate to a theme, and a domain's cache/boss table lists uniques **by
id** (*"a unique is granted by id and never categorically"*); (2) **a domain-only base-type subset** via
the entry's set `ref`; (3) **a rarity floor and shift on the boss group**, StS large-chest shaped
(0/75/25): boss `rarityFloor: heirloom` + shift toward `sunwoven`, elite `cultivated`, cache none — both
columns exist (`DropTableModel.cs:97-98`); (4) **pity per domain — refuse**: *"a guaranteed unique means
every player converges on the same handful in the same week"* (`ssot-uniques.md` §4.5); the domain's
deterministic answer is the first-clear grant. **One wiring detail to get right:** the first-clear
grant bypasses `MintEquipment` (`LootPipeline.cs:208-209`, no `RollSeed`, no `Mint`) — a store that
persists it flat ships a boss relic that ignores depth; the host must instantiate it through
`TryInstantiate` with the boss room's Θ on its own named stream.

**Relics are not a class.** `RelicCatalog` is a four-row stub with `Rarity` 1–4 (not the ten rungs)
and slots `weapon/armor/trinket` (not the fifteen roles), whose effect ids already migrated to
containers; `item-map.md` module 4 already *"retires `rpg_unique_equipment` and the 3-item
`UniqueEquipmentCatalog` stub"*. The word "relic" names two things the item corpus already has: a
**source-locked unique** (worn, carries a rung) and a **charm** (carried, unequipped, `player:{id}`
binding at run start, `ap_cost ∈ {1,2,3,5}`) — which is what a Slay-the-Spire relic actually is.
**Recommend:** map the four stub rows onto rungs and retire the catalog with module 4; no `trinket`
role (the fifteen are closed). **Finding:** 64 uniques say `acquisition: source-locked` and none names
a source — the lock lives in *which table references the id*, which is what `acquisition.py` already
computes; `seed_graph` reachability should assert every source-locked unique is referenced by exactly
one domain table.

**The special item generator is a wiring gap, not a build.** Seedsmith authors the shipped unique
shape (`id`, `nameKey/name/flavor/iconKey`, `frame`, `baseType` ref, `rarity`, `powerAxis`,
`fixedAtoms[{family, powerBand}]`, `varianceSlot`, `counterPressure`, `tags`, `acquisition`) — all
identity and bands. The runtime rolls through `TryInstantiate` with the room's Θ; a unique has
`pool_rolls = 0`, so only the fixed core and variance slot roll; strength is `P(Θ_room)` through
`contentScale`, nowhere else. Every step 0–10 is built and tested; `Mint` is a delegate slot;
`TryInstantiate` and `LootPipeline.Resolve` have zero production callers. The Delve adds a domain
catalog and one host — no new roller.

**The loot pack — a Diablo 2 grid, per party, per delve.** Owner: *"where your backpack is limit so
you cannot bring a whole empire items in the dungeon — imbalance."* The pack limits what you **bring
in** as well as what you **carry out**.

1. **Size is DERIVED, never authored as w×h.** No base type carries a size, footprint or weight
   field (grep over `base-types/`: zero). Every base type *does* carry a five-value **mass-class tag**
   (`tags.v1.json:73-79`, `light … heavy`) whose registered consumer is *"weight/encumbrance formula
   (class · role · frame · tags)"* — a mechanism `seed-contract.md` §8 says *"can be added without
   touching one authored file."* So `footprint(role, massClass)` is a DERIVED column at import: a role
   footprint ordinal (`armament-primary` tall, `core-guard` broad, jewels 1×1) nudged one step by mass
   class. The precedent for a size an author *may* write is charms' `ap_cost` — *"never rolled … if it
   were rolled, the whole game becomes rerolling for a 1-AP copy of a 5-AP charm."* Footprint is a
   base-type fact, never an instance roll, never on a rarity axis (a Sunwoven hatchet is the same shape
   as a Chaff one).
2. **D26 and D5, reconciled.** D26 verbatim: *"keep item system purely item generate, drop and apply
   to actor. we need balance item, not balance the whole game"*; withdrawn from the item program: drop
   caps and *"inventory ceilings | D5 — unlimited capacity."* D5 verbatim: *"we need inventory feature,
   make it category and list first, reserve and share for all for now, we will add inventory management
   mini game in future."* The armoury chose unlimited because *"neither of limited inventory's two real
   jobs (field-pressure or monetisation) applies here — no in-run inventory exists at all"*
   (`spec-inventory-and-workshop.md` §1); `DropVolume.cs:11-14` chose linear volume because *"quadratic
   growth in item COUNT floods an armoury whose management minigame is deferred (D5)."* The Delve
   creates the first in-run inventory, so field-pressure now has a home, and **the pack is that
   deferred minigame, scoped to the run.**

   > **Reconciliation, for the spec to copy:** *The loot pack is a structural per-delve carry limit on
   > what a raid brings in and walks out with; it is owned by the party-dungeon program, it never
   > touches drop volume, drop tables or the armoury, and the armoury at home stays uncapped exactly as
   > D5 and D26 require — a haul that does not fit is a faucet that did not fire (§6), not a cap on the
   > player.*

   The grid's `rows × cols` carries the exemption comment (*structural per-run limit, not a
   progression ceiling; the stash is uncapped*) and a test `the_pack_never_reads_armoury_capacity`.
   `ssot-inventory.md:670`'s *"inventory tetris as accidental gameplay — no capacity at all, no grid"*
   is armoury-scoped; the pack is run-scoped and empties at extraction, so the two rulings do not
   collide — and the spec must say so in one sentence or the next session reads I13 as forbidding it.
3. **Loaded at provisioning from the uncapped armoury** — supplies, spare gear, capture seals for the
   wild room, swap gear — all occupy cells future haul will need. Eating a provision frees its cell.
   **Per party, not shared** (2/4 parties = 2/4 packs): parties take separate routes with their own
   haul and trace, *"a wiped party drops its haul in the dark"* needs per-party ownership, and a shared
   pack across two routes is a teleporting bag. At the boss rendezvous packs stay separate and boss
   grants are dealt round-robin by `PartyIndex` over the manifest's grant index — deterministic, no
   roll. This gives *"a raid can carry more out, and it has more to lose"* its literal mechanism.
4. **Arrangement.** Auto-arrange is **first-fit decreasing** by footprint area, then manifest grant
   index, then id ordinal — integer cells, no RNG, a pure function of `(pack, grants)`, so autopilot
   and replay are byte-identical; skyline packing is rejected unless integer-only (a `float` height map
   is Law 5 in a UI). Manual moves (`pack.move`, `pack.drop`) are decisions in the trace, like intents.
   At each reveal, grants that do not fit go to a *floor* list; the player drops a pack item to make
   room or leaves the grant. Dark and Darker's lesson is value-per-cell. **Charms do not occupy cells**
   — they sit in the AP pouch bound at run start, which is Diablo 4's fix for Diablo 2's charm problem
   (a separate Talisman tab). Stack sizes are tunables per material class.
5. **Size, not weight.** Diablo 2 has no weight; Darkest Dungeon has slots and stacks; Tarkov has
   both, and weight is the invisible half. A scalar weight limit is a magnitude cap the player grinds to
   the last unit and cannot see, and it collides with the caps rule. A grid is structural, visible, and
   it *is* the arrangement game the owner asked for; mass class still earns its keep by nudging
   footprint one step.

**Prior art with numbers:** Diablo 2 inventory **4×10**, stash 8×6 (LoD), Horadric Cube 3×4 in a 2×2
footprint (one source says 4×4 — unresolved); sizes 1×1 potions/rings/gems/runes, charms 1×1 / 1×2 /
1×3, helms 2×2, body armour and two-handers 2×3, up to 2×4 (diablo.fandom.com Inventory;
diablo2.diablowiki.net Inventory_size); D2R shared stash 3 tabs × 10×10; Resident Evil 4 attaché 6×10
= 60 → 120; Tarkov secure containers 2×2 → 3×3 → 4×3; Darkest Dungeon **16 slots**, gold 1,750 per
slot, gems 1–5, trinkets 1, torches 8, food 12; Path of Exile 12×5 = 60 fixed, stash tab 12×12;
Dark and Darker 50 slots per run, value-per-slot decides what leaves; Backpack Hero — *"moving an item
can sometimes provide a greater benefit than finding a completely new piece of equipment"* (adjacency
makes arrangement a game, not a tax); Slay the Spire — no relic limit, by design; Diablo 4 moved
charms to a Talisman tab, up to 6 slots. **Failure modes:** tetris as a tax (the grid is run-scoped
only); charms eating space (AP pouch); autoloot removing the decision (auto-arrange places, never drops).

**Tunables:** `raid.modes.{solo,pair,quad}.pack.{rows,cols}`, `pack.footprint.{role}`,
`pack.footprint.massStep.{massClass}`, `pack.stack.{materialClass}`, `pack.provision.footprint.*`,
`loot.rooms.{elite,boss}.rarityFloor`, `loot.rooms.boss.rarityShiftBand`, `loot.rooms.{kind}.affixChannel`
(`drop` / `boss`), `loot.bossGrantDistribution` (`round-robin` — a rule id, not a number). Everything
strength-shaped stays where it is (`item-drop-volume`, `item-rarity`, `power-scale`); this adds **no**
row to `ssot-power-scale.md` §10.

### 11.8 What the seven passes found that the owner's list did not name

- **`Θ_content` is composed nowhere in production** (§11.2). The Delve is the first producer, and the
  encounter generator is where species offset finally meets room Θ. This is the single highest-leverage
  wiring gap in the whole program: three shipped consumers are starved of it.
- **Two lists, three registries, one predicate vocabulary** — every group found the same pattern: the
  machinery exists, and the delve adds a *row* (`sourceKind`, `ContainerKind`, `OwnerKind`, predicate
  leaf, `useContext` value, `Origin` string) rather than a *system*. Each such row is a reviewed change
  to a closed list, and each is named above so none is smuggled in.
- **Keys and gates** have nouns and a "shut" rule with no "open" verb (§11.1).
- **Formation** can be a 1-D rank on `SideIndex` without a board (§11.4).
- **Rest and camp** are corpus actions in the five slots, not a system (§11.5).
- **The pack is two-way** (§11.7) and retires `events.provisionSlots`.
- **Relics dissolve into uniques and charms** (§11.7); `RelicCatalog` is already scheduled for
  retirement.
- **Upkeep is not Θ-scaled while every other contract price is** — a P2 gap for the contracts owner
  (§11.6).
- **Counts in older documents are stale and were corrected here:** 502 anchor files / 840 index
  entries (not 408); `StructureCatalog` seven rows and three kinds (not four and two); 16 atom kinds
  and 13 triggers (not 12 and 8); eight `OwnerKind`s (not seven); `rpg_item_stock` exists.

### 11.9 Open decisions added by Part II — owner only

Each is a fork where the two readings produce different work. Recommendations first; everything else
in §11 is a recommendation nobody disputed and stands as a decision.

5. **Room graph type: a lightweight `DelveGraph` in `Core/Delve/` sharing catalogs and algorithms
   (recommended) — or a scoped `WorldState` with `mode = 'delve'`.** The second touches every world
   query, `WorldValidation` rules 4–5 and a frozen map FE; the first adds one table pair.
6. **Hand-author the first corpora before any model pass (recommended: ~106 rooms, 6 domains, ~60
   events) — or run the invention pipelines from the research alone.** The first costs authoring time
   and zero tokens; the second risks the mode-collapse corpus `structure-seed-ideal.md` §3 predicts.
7. **Where "medium" sits: at the entrance band as the identity row (recommended) — or one band below
   it.** Recommended keeps an authored domain's band meaning what the sector catalog says.
8. **Does the Oath gate rungs 9–10 and the tail (recommended) — or only the tail?** Pairing the
   hardcore names with the hardcore rule keeps §4.6's promise that the Oath *"opens bands the domain
   otherwise refuses."*
9. **Accept the D26 reconciliation sentence in §11.7 (recommended)** — the pack is this program's
   structural per-delve limit and the armoury stays uncapped. The alternative discards the owner's
   *"cannot bring a whole empire"* and the bring-in / carry-out tension.
10. **Enemy affix container kind: a seventh `ContainerKind` (`enemy`) with its own id prefix
    (recommended) — or ride `trait`.** One regex edit, and the kind is greppable forever.
11. **Formation now as a 1-D rank on `SideIndex` (recommended) — or wait for the `A10` board.** Rank
    costs one setter and a span field and collapses into column later.
12. **A recruit rides in the pack, unbound, as haul (recommended) — or joins an empty party slot
    between rooms.** The second is one line and turns the wild room into free reinforcement; if chosen,
    `joins` must forfeit the room's souls and pay the pact fee from unbanked souls.
13. **Relics retire into source-locked uniques and charms (recommended) — or stay a class.** Staying
    means a sixteenth role and a second rarity scale beside the ten rungs.

Retired by Part II: §5's `risk.oath.bandUnlock` (replaced by `domain.maxRungWithoutOath`), §5's
`events.provisionSlots` (the pack), and §4.7's *"costs a provision slot"* (now: costs pack cells).

> ### ✅ All nine decided by the owner, 2026-09-05 — read these, not the options above
>
> Where the owner chose against the recommendation, the recommendation is retracted, not argued.
>
> 5. **Room graph: a scoped `WorldState` with `mode = 'delve'`** — *not* a new `DelveGraph`. Rooms
>    are `WorldSector`s, doors are `WorldLane`s, persisted by the world store. The spec therefore owns
>    the scoping work the recommendation tried to avoid: a parent-world column on `rpg_worlds`, a mode
>    filter in every world query that iterates `Sectors` (turn engine, loam, supply, AI, intel), a
>    `WorldValidation` profile that relaxes rules 4–5 for `delve` mode, and `Visibility` keyed on the
>    party for a delve world. The irrelevant sector fields stay at their defaults and are never read.
> 6. **Corpora come from seedsmith pipelines, not a hand-written floor.** Owner: *"you define pipelines
>    for seedsmith and generate seed; in game runtime don't use LLM — this seed generator is only
>    contained in seedsmith; in game use the seed structure to generate random event/map/enemy in each
>    dungeon based on our architecture."* Two consequences: (a) §10 law 4's mode-collapse guard moves
>    *into* the pipeline — a deterministic planner stage before any model call (structure-seed
>    decision 33), per-cell `budget` targets, open-loop flavour review — rather than a hand-authored
>    corpus; (b) the runtime's generators are pure functions of seed structures and never call a model,
>    exactly as §10 law 1 states.
> 7. **"Medium" sits one band below the entrance band.** Rung 3 has `bandDelta −1`; `hard` (rung 4) is
>    the authored band and becomes the identity row for modifiers. A named domain reads harder by
>    default. The §11.2 table shifts accordingly: very-easy −2 (floors at 0), easy −1, medium −1,
>    hard 0, very-hard 0, nightmare +1, hell +1, abyss +2, hopeless +2, impossible +3; the tail starts
>    from +3.
> 8. **Oath = opt-in permadeath on rungs below the permadeath gate** (recommended reading kept).
>    Above the gate it is implied; swearing it below unlocks `domain.maxRungWithoutOath`'s bands.
> 9. **D26 reconciliation accepted verbatim.** The pack is this program's structural per-delve limit;
>    the armoury stays uncapped.
> 10. **Enemy affixes: a seventh `ContainerKind`, `enemy`**, with its own id prefix.
> 11. **Formation: 1-D rank on `SideIndex` now, AND the `A10` 2-D board is committed as a later module
>     of *this* program**, not left to the action program.
> 12. **Recruited and captured demons teleport home at once, bound if a contract slot is free** — not
>     a pack cell, not a party slot. Never at risk in the delve, never usable in it. The §11.6 guard
>     against "recruit beats fight" therefore rests entirely on the *costs* (no `KillEarn`, no XP, a
>     seal or an offer spent) and on the encounter roll's rarity shape, not on haul risk; the spec
>     must keep both.
> 13. **Relics retire into uniques and charms — and uniques become a first-class Diablo-style
>     pipeline.** Owner: *"bring unique items into the game, like Diablo and some other games …
>     unique items are very strong, rarity 8+ and cannot be a set item; it will have unique affix,
>     fixed atom effect + random atom effect; bring new passive skills grant and unique action grant
>     to the game."* Rules recorded: rung ≥ 80 (`firstseed` and up); never `set_eligible`; a fixed
>     core plus a rolled variance (the shipped unique shape already has both); passive and action
>     grants as atoms on the unique's container. **The seedsmith item generator is extended for
>     uniques, and the dungeon seedsmith generator runs as ordered sub-pipelines** — domains → rooms →
>     encounters → events → loot tables → uniques — because a unique *"much binds event / dungeon
>     pattern / boss drop"* and needs its dependency seeds resolved first.
>
> **Three more, raised by the answers above and decided in the same pass:**
>
> 14. **The map door uses the existing `Lair / Tear / Vault / Anomaly` slot kinds** (recommended),
>     each mapping to a domain theme; no new `SlotKind`.
> 15. **A delve world's lifetime depends on the domain: both shapes exist.** Owner: *"option 1 and 3 —
>     a dungeon can be entered only once or entered many times, that depends on the dungeon; LLM
>     resolves it in the seed and world-stage handles the seed on the world-map generator *(review:
>     the placer is `world-generator`, wave 4 of the world program — world-stage owns no generator)*. A one-run
>     dungeon drops very strong items and has +7 difficulty."* So the domain seed carries an
>     `entry` ordinal (`once · many`, VALIDATED, the pipeline picks it): `many` domains are a standing
>     sub-world re-rolled on entry (one row per domain); `once` domains are one row per delve, archived
>     at extraction, sealed for that player afterwards, and carry an **entrance `bandDelta` of +7 and a
>     rarity floor on their boss table** — both **tunables** (`domain.onceEntry.bandDelta`,
>     `domain.onceEntry.bossRarityFloor`), never literals, and "+7" is a band delta feeding row 23,
>     not a multiplier. The world-map generator (`world-generator`, a later module of the world
>     program) places them; until it lands, the Sanctum picker offers them.
> 16. **Unique grants: passives sit outside the five action slots; unique *actions* occupy a slot —
>     and a new "extend action slot" effect exists.** Owner: *"option 3, also add a new extend-action-
>     slot atom effect, very very rare on normal drops (0.01% chance) and can be higher in unique
>     items; some rarity 9+ unique items can have a fixed extend-action-slot atom effect — that is
>     why people use unique items, because they are very rare and very strong."* Recorded for the
>     spec: (a) `LoadoutSet.MaxSize = 5` stays the structural base, and a **`loadout.slots` derived
>     channel** (registered in the Actor Hub like every other derived channel) adds to it — this keeps
>     the extension in the RPG layer's stat stack and needs no new atom *kind* if `stat.derived` can
>     target it, which the spec must verify against `AtomKindRegistry`'s closed 16 before proposing a
>     seventeenth; (b) the 0.01% is `loot.extendSlotChanceMilli` (0.1‰ — note the per-mille floor
>     needs a per-ten-thousand unit or a `Micro` key; the spec picks the unit and names it, per T6);
>     (c) on rung ≥ 90 uniques the effect may be a **fixed core atom**, never rolled.

### 11.10 Review decisions — owner rulings on the twelve forks the review raised (2026-09-05)

The six-lens review ([party-dungeon/audit-2026-09-05.md](party-dungeon/audit-2026-09-05.md) §5)
returned eleven forks plus one sub-fork. The owner ruled on all of them the same day. These bind
like the ✅ boxes above; where one amends an earlier decision, the amendment is stated.

| # | Fork | Ruling |
|---|---|---|
| R1 | A10 battle board (amends decision 11) | **Consume base-defense's `siege-board` + `board-render`.** Decision 11's second half is retracted: the Delve ships 1-D rank now and adopts the board when it lands; rank collapses into column. One note on `action-map.md`; no `decisions.md` row |
| R2 | Once-entry +7 | **Keep the tunable, with conditions:** the picker shows the effective band *name*, never the delta; the stack rule with rung deltas is written (it stacks); the "very strong items" promise rests on `bossRarityFloor`; `entry: once\|many` is **PLANNED** by the seedsmith budget, never a free model pick |
| R3 | Permadeath meaning (refines decision 1) | **`downedOnce`:** on a permadeath rung a demon downed at *any point* is `Retired` at extraction even if revived — the revive lets it finish the run, not escape the rule. A wipe Retires the whole party and drops the haul; the named mitigation is cheap replacement at the pull price |
| R4 | Oath unlock key (amends decision 8) | **A clear at `maxRungWithoutOath` itself opens the next rung.** The Oath below the gate stays as opt-in permadeath and a first-clear key, not the unlock mechanism |
| R5 | Recruit price (refines decision 12) | **The offer floors at the altar pull price at the room's Θ via `SoulSinkPolicy`, paid from unbanked souls;** spirit, supply and released-contract offers priced as equivalents. The bind stays free; teleport-home stands. Altar pulls are **at-risk haul on the delve ledger, delivered at extraction** (decision 12 named recruits and captures only) |
| R6 | Recovery clock (amends decision 1) | **Virtual time only — a downed demon sits out a tunable number of delves (`risk.downedRecoveryDelves`).** The real-time clock is removed. Owner: *"this game is not a paywall game; we don't limit players by a stamina system like some cheap mobile game."* Principle 6 now holds without a recorded tension |
| R7 | Extend-slot drop (refines decision 16) | **Keep the 0.01% as `loot.extendSlotChanceMicro: 100`** on a per-million stream (`CombatProbability.cs:15` precedent); one extend-slot unique counts at a time (`affix.exclusiveTags`); the slot count stays structural with the exemption comment; the rung-9+ fixed core stands |
| R8 | Rung twins (refines decision 7) | **Every rule rung carries a reward-bearing column; the delta column is unchanged.** Encounter design's `enemyCountDelta` or loot's `rarityFloor`/`rarityShiftBand` step per rung; validator: neighbouring rungs differ in `bandDelta` *or* a reward column, never only a penalty. `hard` is the identity row; modifiers the first table hung on `hard` move one rung up; `depth.bossBand` becomes `depth.bossBandDelta` on the last corridor's band; a rung whose band would clamp on a domain is not offered |
| R9 | Autopilot and steering switch | **A `siege-ai`-class policy per un-steered party; switching away freezes the fight as a persisted decision log** (base-defense decision 46). "Same seed, same rewards" holds; autopilot is never a competitor to steering |
| R10 | Map door and legions | **No legion leaves the map:** the door issues the same Sanctum delve request with a `domainId`. Legion-as-raid waits for the world program's own `delving` design |
| R11 | Unknown-room pity scope | **Per party**, matching per-party packs, hauls and routes |
| R12 | Once-entry wipe (refines decision 15) | **A wipe seals the domain but the boss loot already earned is kept** — Diablo 3's Greater-Rift shape. `onceEntry.failKeepsBossLoot: true` and `onceEntry.sealOnWipe: true`, both tunables per domain; the haul other than the boss grant is lost as everywhere |

**Standing S1 fixes the review found that needed no owner call, recorded here so the spec inherits
them:** victory souls (`MatchEndEarn`) fire **once per delve at extraction** on `Θ_run` = deepest room
cleared, forfeited on a wipe — rooms pay `KillEarn` only (per-room victory souls made two row-1 fights
the best faucet in the game); decision 5's remedy list is replaced by the review's §1(a) shape
(`parent_world_id` + `kind` columns — never `mode`, which is the clock axis — a
`WorldValidation.Validate(world, profile)` overload skipping rules 4/5/11/13, room/door catalogs not
served on `/api/world/catalog`, a `kind='map'` filter in `GetActiveWorld`, and the delve host never
calling `TurnEngine.Step`); the actor-side `Θ_actor` composition is a wiring gap (§11.2); the
`threat-audit` run over the 657 anchors without `threatBand` is an external dependency on demon-seed
module 7; decision 13's sub-pipeline list is the intent and the tool derives the real order from
`reference_fields` (review §1(h)); extraction is raid-wide (a party may hold at a rest, never bank);
the delve stage is the **sixth** and needs a new id `delve` with a Game-GUI `decisions.md` row; the
four `decisions.md` rows and nine stale-doc propagations the record lists precede the capability map.

### 11.11 What Part II deliberately does not decide

Any number in any table above (starting shapes for the balance pass); the exact room-kind catalog
rows beyond the eleven named; which of the fourteen sub-mechanisms ships first (the capability map's
job — but the model-free halves come first everywhere: a schema, a registry, a dump, a catalog reader
produce value with zero tokens spent and make every later generator's inputs reviewable); the
`species-effects` dependency that makes support/controller slots filterable; the `A10` board; the
roster-add build that unblocks in-battle summoning and summoner bosses (base-defense's); the item-cost
row on actions and the `consumable` container kind (the item program's); module 17 uniques as per-run
drops and module 18 consumable payloads.

**Next step is unchanged:** answer §8 and §11.9, then `/spec` for `party-dungeon-map.md`. The map
should order the fourteen by dependency — domain and map catalogs first (model-free), then the
encounter generator (it is where `Θ_content` is finally composed and every other generator reads it),
then events, supplies and the pack together (they share the grid and the override registry), then
recruit/capture/summon, then quests.
