# Party dungeon (the Delve) — the ideal

**Status:** idea phase, 2026-09-05. **Not a spec. No build authorized.** No module ids, no build
order, no acceptance criteria. This exists to be argued with and cut down before it becomes a
capability map. **Part II (§11, same day) enriches the fourteen sub-mechanisms the owner listed
as seedsmith seed generators paired with in-game concrete generators**; its corrections to Part I
are folded in where they land and marked *(Part II)*.

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
| **Downed/revive exists in the timeline FSM, not in `Resolve`** | `ActorTurnMachine`, `ActionRunner`, `ReadinessDriver`, `RendezvousLane`: 21 test files, **zero constructions in `src/`**; `BattleEngine.cs:65-68` is a binary `Alive => Hp > 0` | `decisions.md` row 42 already locks `Downed` as *"HP ≤ 0 is veto-capable, never a terminal edge"*. The Delve needs Downed; the engine has it built and not wired |
| **Reaction lane and rendezvous are gated off** | `WReact` 0 and `RendezvousEnabled` false on every profile; `ReactionLane.cs:60` gates on `wReact > 0` | Press-turn (SMT) and link strikes are profile rows away — owner already put both in scope (2026-08-21) |
| **`resource.delta` reaches the bag only from a test seam** | `BattleEngine.cs:132-137` — *"Null in every production and golden call site"* | Hunger/spirit attrition in a delve fight is a grant through this seam |
| **Loot pipeline has zero production callers** | `LootPipeline.Resolve` (`:134`) — tests only; no `LootEndpoints.cs` exists | The Delve is the first caller. A fifth `sourceKind` and correlation shape at `LootPipeline.cs:91-98` |
| **`Instantiator.TryInstantiate` has zero production callers** | `Instantiator.cs:98`; five specs independently record this | Every "we need a runtime generator" finding below is a wiring gap on this SDK, not a new build |
| **Relics have no faucet** | `RelicCatalog.cs:6` *"no acquisition system exists yet"* | The Delve grants them. `LootManifest.FirstClearGrant` is the exact slot for a boss's guaranteed relic |
| **Turn engine `Events` phase is a pass-through** | `TurnEngine.cs:272` `return world;` after calendar report lines only | Not needed by the Delve, but the map's own event slot exists if domains later rotate with seasons |
| **`SlotKind.Anomaly` / `Vault` / `Tear` / `Lair` have no reader** | `SlotTypeCatalog.cs:70-76` rows; `grep` finds no verb | These are the domain *entrances* on the map, already in the catalog |
| **`WorldEntityMember.InstanceId` has no writer** | `WorldState.cs:244`; only `RaiseResolver.cs:132-136` mints members and never sets it | A legion of named specimens is modelled, hashed and persisted; the Delve's party is the first writer if it is ever launched from a legion |
| **The world's battle resolver is never supplied** | `TurnEngine.cs:83` `resolver ?? PlaceholderBattleResolver.Instance`; both call sites pass none | Not the Delve's job, but the same `BattleRequest.Guard` shape is what a room encounter is |
| **`WebMatchService.cs:238` `const int maxSquad = 6`** | A balance number as a `const`, refused with `squad.toolarge` | The Delve must not copy this; `expeditions.v1.json`'s `squadSlots` is the pattern |
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
| DD2 Death's Door | base 60% survive, clamp 5–90%, **−10% resist per survived hit** | darkestdungeon2.wiki.fextralife.com |
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
*grind*, not difficulty (faster XP, shorter dungeons, cheaper town — enemies unchanged). The stress
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
  Ascension 1+: Elite 16%, Monster 45%.
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
larger, and drop *count* rises linearly through `DropVolume` (§10 row 28). There is no "hardcore
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

**Autopilot is free.** `world-graph-ideal.md` §13: *"who decides is a parameter — autopilot policy
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
the harshest combination in the survey and none of the referenced games ships it by default.

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
- **Souls** through the existing `KillEarn`/`MatchEndEarn` per room (already `contentScale`-scaled)
  plus event souls with reason `delve`; `discovery` for a species first seen in a domain.
- **Materials, shards, essence** exactly as expeditions grant them.
- **Wild joins** with `Origin = "delve"`.
- **Specimen and species XP** on the existing per-battle-won paths.
- **A domain's first clear** reveals the next domain and writes a codex entry — the "atlas entry"
  the world ideal's vault row names.

Nothing is capped. Volume is linear in `Θ`, strength is `P(Θ)`, and the only throttle is what the
raid can carry out alive.

### 4.10 The stage

`#/delve/{id}` is a fifth stage beside Sanctum, World, Lawn, Battle (and `siege`, proposed by the
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
| **A fifth expedition tier with rooms, auto-resolved** | Cheapest by far, and it is not what was asked: the owner's references are all *played*. It is also already covered — autopilot falls out of the intent seam for free, so the auto-resolved delve is a mode of this design, not a competitor to it |
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
| `depth.bandStepPerRow` | how fast the band rises per row | 1 per 2 rows |
| `depth.bossBand` | the boss room's band | 6 (`boss-lair`) |
| `attrition.hunger.perRoom.{fight,elite,event,rest}` | hunger cost per room kind | tunable, rest negative |
| `attrition.spirit.perElite`, `attrition.spirit.bossPresence`, `attrition.spirit.retreat` | nerve drains | DD +25-stress shape |
| `attrition.restHealMilli` | HP/hunger/spirit restored at a rest | ‰ of max |
| `risk.downedRecoveryDays` | days in `Recovering` per downed demon | priced-time dial |
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
    caps (project-wide), magic numbers, resource model, standalone-first, game GUI. None
    contradicted; each is restated where it constrains a choice.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — five parallel inventories plus direct reads of
    BattleRunState.cs:565-595, RpgStore.UniqueActors.cs:190-225, RelicCatalog.cs, TurnEngine.cs
    Events phase, Instantiator.cs:85-120, expeditions.v1.json, contracts.v1.json.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting — NOT APPLICABLE: this document proposes
    no change and reports no "moves goldens" claim; the two golden-safe additions it names
    (an inbound HP field, a party label) rely on the WhenWritingDefault precedent, which is
    cited, not run.
[x] Nothing contradicts a §2 invariant. The one place this design touches an invariant's edge —
    resource pools persisting across fights — is the resource hub's own §11 rule, unused until now.
[x] Corrections are propagated: none needed; this document creates no map, plan or task.
```

---

## 10. Related

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
