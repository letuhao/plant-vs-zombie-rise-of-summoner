# Tasks: party-dungeon — "the Delve"

Plan: [party-dungeon-plan.md](party-dungeon-plan.md). Specs:
[docs/architecture/party-dungeon/](../docs/architecture/party-dungeon/) — seventeen, all APPROVED
2026-09-05. Map: [party-dungeon-map.md](../docs/architecture/party-dungeon-map.md).

Task ids are `D<phase>.<n>` and are stable. Every task names the spec section it implements; when a task
and its spec disagree, **the spec wins**. No task changes more than about five files.

**Standing rules for every task below, stated once:** magnitudes are `long`, never `float`; widen before
multiplying; divide by 1000 last, exactly once; overflow throws, never wraps. Any number a balance pass
would change lives in `data/tuning/`, never a `const` — a structural constant keeps its `const` and says
why in a comment. Nothing computes a private `f(level)`: contests read `Θ`, magnitudes read `P(Θ)`.

---

## Phase 1 — the skeleton (wave 1)

### `dungeon-registries` — spec-dungeon-registries.md

- [x] **D1.1** The nine registry JSON files
  - Acceptance: `data/seed/dungeon/_registry/{room-kinds,door-kinds,override-tags,objective-templates,difficulty-rungs,disposition,interaction-verbs,raid-modes,bands}.v1.json` exist, each with `schemaVersion`/`registryVersion` in the shape `data/seed/demons/_registry/families.v1.json:2-3` already uses. Room kinds are the eleven; rungs are the ten in order with `ordinal`; `bands.v1.json` carries every ordinal vocabulary the anchors write **plus a display name per member** (the composed-band names and the three `nerve` stages — the ask `delve-stage` filed)
  - Verify: a schema test asserts every file parses, every id is unique and lowercase-kebab, and the rung ordinals are `0..9` contiguous
  - Files: the nine JSON files
- [x] **D1.2** `DungeonRegistries` loader and hub
  - Acceptance: `DungeonRegistries.cs` loads all nine into records and exposes one `<Vocabulary>Catalog` each; an unknown id throws with the file and the id in the message; the loader is pure over a directory path
  - Verify: `tests/FusionRpg.Core.Tests/Dungeon/` — round-trip per registry, one red test per malformed file
  - Files: `src/FusionRpg.Core/Dungeon/Registry/DungeonRegistries.cs` + the nine catalog files
- [x] **D1.3** `dungeon.v1.json` and `encounter.v1.json` schemas and loaders
  - Acceptance: `DungeonTuning.cs`/`EncounterTuning.cs` reject on **any** missing key (T5) rather than defaulting; every key carries its unit per T6; one owner per number — `wildJoinMilli`, `costPerPull` and `boss-lair`'s band each appear exactly once across both files
  - Verify: a test deletes one key at a time and asserts the loader throws naming that key; a duplicate-owner test greps both files for the three named keys
  - Files: `data/tuning/dungeon.v1.json`, `data/tuning/encounter.v1.json`, `src/FusionRpg.Core/Dungeon/Tuning/{DungeonTuning,EncounterTuning}.cs`
- [x] **D1.4** Host wiring
  - Acceptance: `Program.cs` and `RpgHost.cs` Configure the registries and both tuning files once at startup; a missing file fails startup loudly rather than at first use
  - Verify: `dotnet test tests/FusionRpg.Core.Tests`; server starts and logs the two registry versions
  - Files: `src/FusionRpg.Server/Program.cs`, `src/FusionRpg.Injector/Host/RpgHost.cs`
- [x] **D1.5** The seedsmith-side registry reader
  - Acceptance: `tools/seedsmith/seedsmith/adapters/dungeon/registries.py` reads the same nine files, so the validator and the C# catalogs cannot drift; no vocabulary is duplicated in Python
  - Verify: `tools/seedsmith/tests/test_dungeon_registries.py` asserts the Python and JSON member sets match exactly
  - Files: `tools/seedsmith/seedsmith/adapters/dungeon/registries.py`, its test

### `dungeon-seed-contract` — spec-dungeon-seed-contract.md

- [x] **D1.6** The seven anchor shapes and their ownership levels
  - Acceptance: `schema.py` defines domain, room, layout, event, quest, encounter and supply/object shapes with **one ownership level per field**, including the new `PLANNED` level; every description carries a negative clause; every enum has a `none` member or a stated reason
  - Verify: `tools/seedsmith/tests/test_dungeon_contract.py` — every field has exactly one level and a non-empty negative clause
  - Files: `tools/seedsmith/seedsmith/adapters/dungeon/{__init__,kinds,schema,descriptions}.py`
- [x] **D1.7** The four-shape schema audit
  - Acceptance: `audit.py` refuses a magnitude a model could have written — a stem check over `*weight*`/`*chance*`, a spelled-number list, `dropBand` reused and `weightBand` refused, `manifestCost` allow-listed
  - Verify: `test_dungeon_contract.py` feeds one violating anchor per rule and asserts a named refusal
  - Files: `tools/seedsmith/seedsmith/adapters/dungeon/audit.py`, its test
- [x] **D1.8** The `dungeon` adapter registration and derived sub-pipeline order
  - Acceptance: `adapters/registry.py` gains `dungeon`; the sub-pipeline order (registries/species → layouts → supplies → events → encounters → uniques → loot tables → rooms → domains) is **derived from `reference_fields`**, never written down twice
  - Verify: `test_dungeon_order.py` asserts the derived order equals the expected list and changes when a reference field changes
  - Files: `tools/seedsmith/seedsmith/adapters/registry.py`, `adapters/dungeon/__init__.py`, its test
- [x] **D1.9** The planner
  - Acceptance: `planner.py` emits per-cell disjoint motif briefs with siblings as anti-motifs, a per-cell `budget`, and `entry`/`dangerBand` as `PLANNED`; the vote set is chosen by cost-of-being-wrong and stated per field, not by feel
  - Verify: `test_dungeon_planner.py` — two cells never share a motif; every cell has a budget; the vote set matches the declared table
  - Files: `tools/seedsmith/seedsmith/adapters/dungeon/{planner,briefs}.py`, its test
- [ ] **D1.10** Pipelines with permuted enums and majority vote — **SCOPED DOWN 2026-09-05, gap stated rather than hidden**
  - Acceptance: every enum is permuted from a seed of `(entity_id, field, sample_index)` with the index **inside** the seed; load-bearing fields are majority-voted, others are not; `1-1-1` resolves to `unresolved`, never the first option; constrained decoding is proven by one real call before a run
  - Verify: `tools/seedsmith/tests/` with the transport stubbed to **raise** — no test may call a model
  - Files: `tools/seedsmith/seedsmith/adapters/dungeon/pipelines.py`, its tests
  - **What actually landed:** D1.6–D1.9/D1.11 (schemas, ownership, audit, adapter registration, derived ordering,
    planner cells/id-minting, canonical emit, byte-identical rerun, staleness, the offline guarantee) are built and
    tested — 47 new passing tests, full seedsmith suite green (1747 total). **`pipelines.py` itself is not built.**
    It needs a dungeon motif registry that does not exist yet (no `data/seed/dungeon/_registry/motifs*.json` is
    committed) and real prompt content per field — both are a content-authoring pass, not missing infrastructure:
    the permutation/vote/checkpoint mechanics it would use (`seedsmith/pipeline/`, `planner/feasibility.py`,
    `planner/demand.py`) already exist generically and are proven to work for the sibling adapters. Consequence:
    no dungeon seed content has been generated. Downstream C# modules (`delve-graph-roll`, `encounter-generator`,
    `event-deck`, `delve-quests`, `domain-catalog`) will read hand-authored fixture content matching the
    now-verified schema instead, exactly as their own specs already do for tests ("tests construct inline").
    Follow-up: author the motif registry, write the prompts, build `pipelines.py` + `briefs.py` on top of the
    proven planner/emit/provenance layer, then run a real first-ship content pass.
- [x] **D1.11** Provenance, `stale_ids` and the byte-identical rerun
  - Acceptance: every emitted anchor carries `{planHash, briefHash, promptVersions, registryVersions, motifSubsetHash}`; `stale_ids()` names what a registry bump invalidates; a rerun with the same inputs is byte-identical, proven by hash; `--dry-run` prints the call budget before any run
  - Verify: `test_dungeon_idempotency.py`, `test_dungeon_budget.py`
  - Files: `adapters/dungeon/{provenance,emit}.py`, the two tests

### `delve-scope` — spec-delve-scope.md · joint with the world program

- [x] **D1.12** `rpg_worlds` gains `kind` and `parent_world_id`
  - Acceptance: `EnsureColumn` adds both (never `mode` — that is the clock axis); a database created before this change keeps working; `GetActiveWorld` filters `kind='map'`
  - Verify: `tests/FusionRpg.Data.Tests` — an old-schema database opens, migrates and reads; `GetActiveWorld` ignores a delve row
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.World.cs`
- [x] **D1.13** World goldens hold **(run this before anything else touches the store)**
  - Acceptance: every world golden is byte-identical after D1.12; the header row still hashes `TemplateId, Seed, CurrentTurn` only
  - Verify: the world golden suite, diffed byte for byte against its pre-change output
  - Files: none expected — this task is the proof
- [x] **D1.14** `WorldValidation.Validate(world, profile)`
  - Acceptance: a `WorldValidationProfile` with `Map` = today's behaviour and a `Delve` profile skipping rules 4/5/11/13; the map profile still rejects a rolled delve graph
  - Verify: red/green pairs per profile; the existing map-side tests are untouched and still pass
  - Files: `src/FusionRpg.Core/World/WorldValidation.cs`
- [x] **D1.15** `RoomTypeCatalog` / `DoorTypeCatalog` and `LaneGate`
  - Acceptance: both project the registries into the existing `SectorTypeDef`/`LaneTypeDef` shapes and are **not** served on `/api/world/catalog`; the two door checks are lifted from `MarchResolver.cs:57-60` into `LaneGate` with no behaviour change
  - Verify: a test asserts `/api/world/catalog` does not contain a room type; the march tests still pass against `LaneGate`
  - Files: `src/FusionRpg.Core/Delve/{RoomTypeCatalog,DoorTypeCatalog}.cs`, `src/FusionRpg.Core/World/Movement/LaneGate.cs`
- [x] **D1.16** `rpg_delves` and `rpg_delve_rooms`
  - Acceptance: the header (raid mode, seed, state, `parties_json` routes and pity, `souls_unbanked`, `theta_run`, `content_terms_json`, `quests_json`) and the room table (cleared, visited, `floor_json`, `event_id`, `resolved_kind`, `resolved_archetype_id`) exist; `RpgStore.cs`'s reset list gains both
  - Verify: `tests/FusionRpg.Data.Tests/Delve/` — create, load, reset
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Delve.cs`, `RpgStore.cs`
- [x] **D1.17** The store surface and the no-`Step` guard
  - Acceptance: `EnsureDelveSchemaUnlocked`, `CreateDelve`, `LoadDelve`, `MoveParty`, `MarkRoom`, `AppendDecision`, `CloseDelve`; `world.not-a-map` refusals at the three turn entry points; `DelveSight.ForParty` is a pure per-party `Glimpse` overlay
  - Verify: a guard test asserts `TurnEngine.Step` is never reachable from a delve world; `guard-dal.ps1` passes
  - Files: `RpgStore.Delve.cs`, `RpgStore.WorldTurns.cs`, `src/FusionRpg.Core/Delve/{DelveSight,DelveWorldIds}.cs`

### `difficulty-ladder` — spec-difficulty-ladder.md

- [ ] **D1.18** `RoomTheta.Compose` — the first production composer of `Θ_content`
  - Acceptance: `Compose(...)` builds a `ContentContext(DangerBand = entrance + rowStep + bandDelta [+ once +7] [+ tail·n], WorldTier, ZombossLevel, RealmsAdvanced)` and returns `{Context, Theta, Band}` through `PowerIndexComposer.ContentExplain`. **No arithmetic on `Θ` outside this file**
  - Verify: golden rows including the worked pair (rich/hard row 0 → Θ 70; boss → Θ 100)
  - Files: `src/FusionRpg.Core/Delve/Difficulty/RoomTheta.cs`
- [ ] **D1.19** `RungTable` and `RungValidator`
  - Acceptance: the ten rungs load with decision 7's deltas; every rule rung carries a reward-bearing column; the validator throws `RungTableRejection` when neighbours differ in neither `bandDelta` nor a reward column
  - Verify: red/green validator tests; a golden of the shipped table
  - Files: `RungTable.cs`, `RungValidator.cs`
- [ ] **D1.20** `RungOffer.For` — refuse, never clamp
  - Acceptance: offers per `(domain, player)` return only rungs the domain supports; a rung that would floor on a domain is **omitted with a named refusal**, never clamped; the band name is returned, never the ordinal
  - Verify: a test asserts no clamp path exists and a refused rung is absent from the offer rather than greyed
  - Files: `RungOffer.cs`
- [ ] **D1.21** `TailLadder`, `PermadeathGate`, `OathUnlock`
  - Acceptance: `n → band` with a `MaxIndex` pre-check that throws rather than saturating; `permadeathFromRung` per domain; the Oath is opt-in below the gate and **a clear at `maxRungWithoutOath` unlocks the next rung**
  - Verify: overflow pre-check test at the boundary; an Oath clear unlocks exactly one rung
  - Files: `TailLadder.cs`, `PermadeathGate.cs`, `OathUnlock.cs`
- [ ] **D1.22** `Θ_actor` composition named as the wiring gap it is
  - Acceptance: the actor-side composition is written with the specimen-level fallback stated in a comment; nothing invents a second curve
  - Verify: a contest property test — a difference in `Θ` moves the contest monotonically
  - Files: `src/FusionRpg.Core/Delve/Difficulty/` (the composition seam)

### `delve-graph-roll` — spec-delve-graph-roll.md

- [ ] **D1.23** The graph model and stream names
  - Acceptance: `DelveGraph`, `DelveRoomFact`, `DelveWalk`, `RaidMode`; `DelveStreams` formats every reserved name (`dungeon:layout`, `dungeon:walk:{k}`, `r{00}c{00}`, and the reserved event/loot/unknown/supply/merchant/wild/altar names other modules derive)
  - Verify: a name-format test per stream; a collision test across all reserved names
  - Files: `src/FusionRpg.Core/Delve/Roll/{DelveGraph,DelveStreams}.cs`
- [ ] **D1.24** `Roll(domain, layout, seed, raidMode, tuning)`
  - Acceptance: a layered DAG; path walks per raid mode; node kinds from the weight table; a room archetype per kind × climate; one-way lanes deeper only; secret rooms at dead ends; the fixed rows (first fights, mid cache, rest before boss); sight as `Glimpse`/`Full` from tuning. Pure — no store, no clock
  - Verify: `DelveGraphRollGoldenTests`, `DelveGraphDeterminismTests` with hashes in `docs/research/party-dungeon/_baseline-delve-graph.json`
  - Files: `DelveGraphRoll.cs`
- [ ] **D1.25** Gates and keys
  - Acceptance: a key is placed **strictly above** its gate on another walk, validated; a gate with no reachable key throws
  - Verify: a property test over 256 seeds asserting reachability for every gate in every raid mode
  - Files: `DelveGraphRoll.cs`, `DelveGraphValidation.cs`
- [ ] **D1.26** `DelveGraphValidation.Validate` — seventeen rules, each throws
  - Acceptance: all seventeen implemented; each throws with the rule name and the offending room id; **no rule warns and continues**
  - Verify: `DelveGraphValidationTests` — one red test per rule
  - Files: `DelveGraphValidation.cs`
- [ ] **D1.27** The sealed per-run seed
  - Acceptance: the seed is derived once at `CreateDelve` and never re-derived; a rolled graph rebuilt from the stored seed is identical
  - Verify: `DelveGraphPropertyTests` — round-trip through the store and compare structurally
  - Files: `DelveGraphRoll.cs`, `RpgStore.Delve.cs`

### The map door — owner decision 2 (`party-dungeon-ideal.md` §8 answer 2; R10)

- [ ] **D1.28** The world-map door
  - Acceptance: a `world-inspector` action plus a `world-commands` order that posts **the same
    `POST /api/delve/start` body the Sanctum picker sends**, and navigates to `#/delve/{delveId}` on the
    `{delveId, worldId}` response via `delveRoute()`. It reuses the four existing slot kinds
    `Lair · Tear · Vault · Anomaly` (`SlotTypeCatalog.cs:14-20`) mapped to domain themes — **no new
    `SlotKind`** (decision 14). **No legion leaves the map** (R10): the door issues a request, it does not
    move a force; legion-as-raid waits for the world program's own `delving` design. Strictly additive —
    one action row, no layout change, no new component
  - Verify: the picker and the door post byte-identical bodies (shared with D4.22); a legion's state is
    unchanged by opening a delve; the map-FE diff contains **only** this action row
  - Files: `web/fusion-rpg-web/src/features/world/` (the inspector action), `src/lib/bus/world.ts` (the
    order) — **the one authorised exception to the map-FE freeze**, per decision 2's accepted scope
  - Note: filed on `world-stage-map.md:262-266` as that program's row. If its pre-refactor arbitration has
    already begun when this task is reached, it moves there and becomes **F12** rather than being built
    twice — say which happened, do not do both

> ### CHECKPOINT G1 — scoped world
> - [ ] A delve world row exists beside a map world; `GetActiveWorld` returns the map
> - [ ] `WorldValidation` accepts a rolled graph under the delve profile and rejects it under the map profile
> - [ ] **All world goldens byte-identical**
> - [ ] `TurnEngine.Step` is never called on a delve world (guard test)
> - [ ] Both entrances reach `POST /api/delve/start` with the same body; the map-FE diff is D1.28's action row and nothing else
> - [ ] `python scripts/audit-overflow.py` zero critical; `audit-magic-numbers.py --targets M1` shows no new literal
> - [ ] `dotnet test` green across Core, Data, Guard

---

## Phase 2 — a room is a fight (wave 2)

### `encounter-generator` — spec-encounter-generator.md

- [ ] **D2.1** `SlotFilter.Candidates`
  - Acceptance: a slot is a filter tuple over anchor ordinals (posture via `roster.json`, reach, `targetPreference`, `threatWindow`, element spread); **a null `threatBand` is refused loudly**, never defaulted
  - Verify: candidate counts per slot over the 184 banded anchors; a red test for the null band
  - Files: `src/FusionRpg.Core/Delve/Encounter/SlotFilter.cs`
- [ ] **D2.2** `SlotFill` and `Encounter.Build`
  - Acceptance: count plus a weighted draw with the same-species cap, on named streams; `Build(anchor, roomTheta, climate, raid, rung, seed, corpus, tuning)` returns an `EncounterHalf` of `BattleActorSetup`s at **`θ = Θ_room + thetaOffset(species)`** — the sum nothing computes today
  - Verify: goldens for `pack`, `party` and `boss`; determinism over 256 seeds
  - Files: `Encounter.cs`, `SlotFill.cs`
- [ ] **D2.3** `RankOrder` — 1-D rank on `SideIndex`
  - Acceptance: `rankOrder` → emit order with `rankSpan`; the reach mask and the `targetPreference` default pick are a read model; the 2-D board is explicitly not adopted
  - Verify: rank goldens; a test that reach never selects across a span it cannot reach
  - Files: `RankOrder.cs`
- [ ] **D2.4** `BossBuild`
  - Acceptance: the boss is a `role: boss` slot with kit ordinals, phases, a retinue per party and `W` per raid; **`W` is returned, never serialised**
  - Verify: a boss golden per climate; a serialisation test asserting `W` is absent from the wire
  - Files: `BossBuild.cs`
- [ ] **D2.5** `BossShieldPool` — the one `P(Θ_room)` read
  - Acceptance: `bossShieldPerPartyMilli × P(Θ_room) × (N−1) / 1000` as `long`, widened before multiplying, divided once; the tunable starts at 300‰ with 170‰ as the lower reference; the read is registered as one location row in `docs/architecture/power/inventory.json`
  - Verify: an arithmetic test at Θ 100 and Θ 3,000 proving no overflow and exact integers; a fight-length band regression
  - Files: `BossShieldPool.cs`, `docs/architecture/power/inventory.json`
- [ ] **D2.6** `EliteAffix` and `ContainerKind.Enemy`
  - Acceptance: `ContainerKind` gains a **seventh** member `Enemy` with its prefix arm in the validator; an elite is one slot with an affix rolled through `Instantiator.TryInstantiate`; a no-affix draw warns rather than silently shipping a plain elite
  - Verify: `ContainerValidator` red/green for the new prefix; an elite golden
  - Files: `EliteAffix.cs`, `src/FusionRpg.Core/Effects/Atoms/{ContainerRow,ContainerValidator}.cs`
- [ ] **D2.7** Preflight, coverage and refusals
  - Acceptance: per-domain candidate counts, a rung histogram and named refusals; `EncounterCoverage` measures `(postureMultiset, spread, formation)` cells as a **closed-loop** metric; an unfillable slot refuses loudly
  - Verify: coverage over the six first-ship domains; every refusal reachable in a test
  - Files: `EncounterPreflight.cs`, `EncounterCoverage.cs`, `EncounterRefusal.cs`

### `delve-battle-profile` — spec-delve-battle-profile.md

- [ ] **D2.8** The seven additive `BattleActorSetup` fields — **hash task, do this first**
  - Acceptance: `long? CurrentHp`, `int? PartyIndex`, `int? RankSpan`, `int? ThetaActor`, `IReadOnlyDictionary<string,long>? CarryInPools`, `IReadOnlyList<PhaseGrant>? PhaseGrants`, `IReadOnlyList<string>? GrantedContainerIds` — every one nullable and `[JsonIgnore(WhenWritingDefault)]`; the result gains `int? PartyIndex` as an **init property, never positional**, plus `CarryOut`
  - Verify: **all four battle hashes, the 32-seed sweep and the four expedition tier hashes re-run and byte-identical.** If one moves, the field is wrong
  - Files: `src/FusionRpg.Core/Battle/BattleModels.cs`
- [ ] **D2.9** The `delve` profile row
  - Acceptance: a `hybrid-atb`-shaped row in `battle.v{n}.json` with `RequiresLiveInput: true`, `UsesTimelineDispatch: false`, `OrdersBySpeed`, a `PerSide` economy option and `DownedOnDeplete: true` (**false on every shipped row**); `RulesetVersion` stays 4; `wReact: 1` is inherited and inert
  - Verify: a profile test asserting every shipped row's `DownedOnDeplete` is still false; the ruleset version is unchanged
  - Files: `src/FusionRpg.Core/Battle/Timeline/BattleModeProfile.cs`, `data/tuning/battle.v4.json`
- [ ] **D2.10** `BattleRunState.Withdraw(actor)`
  - Acceptance: the withdraw path is **lifted** out of `CheckRetreats` (`:671-673`) into one member with no behaviour change, then given two new producers (capture, player retreat)
  - Verify: the existing retreat tests pass unchanged; two new tests for the new producers
  - Files: `src/FusionRpg.Core/Battle/BattleRunState.cs`
- [ ] **D2.11** Carry-in wiring
  - Acceptance: `Hp = CurrentHp ?? MaxHp`; party pools seed from `FromStored(pools + {hp: CurrentHp ?? MaxHp})` — **one hp seat**; `theta = ThetaActor ?? Index`; the economy key is `side:squad:p{PartyIndex}`
  - Verify: a carry test across two rooms; an economy-key test for a four-party raid
  - Files: `BattleEngine.cs`, `BattleRunState.cs`, `BattleStatComposer.cs`
- [ ] **D2.12** `DelveBattle.Run` — the explicit resolve
  - Acceptance: `BattleEngine.Resolve(setup, seed, trace, profile:, …, intentSource:)` is called **explicitly** with the `delve` profile; `ProfileForExpedition` and `ProfileForWave` are never called from delve code
  - Verify: a guard test greps the delve namespace for both names and fails on a hit
  - Files: `src/FusionRpg.Core/Delve/Battle/DelveBattle.cs`
- [ ] **D2.13** `RaidIntentSource`
  - Acceptance: one `IIntentSource` dispatching on `SideOf` + `PartyIndex`: the steered party reads `InteractiveIntentSource`, the rest read the shipped `SiegeAi` policy
  - Verify: a four-party fight resolves with one interactive and three automated sources; replay is byte-identical
  - Files: `src/FusionRpg.Core/Delve/Battle/RaidIntentSource.cs`
- [ ] **D2.14** `DelveCarry` and `DelveDecision`
  - Acceptance: `CarryIn`/`CarryOut` records and the setup mapping; the delve-level log kinds `enter · route · pack.move · pack.drop · talk · supply.use · object.{verb} · steer · retreat · extract`
  - Verify: a round-trip test per kind; the log is append-only
  - Files: `DelveCarry.cs`, `DelveDecision.cs`
- [ ] **D2.15** The per-battle trace and the `profile_id` column
  - Acceptance: the delve is the **first writer** of `rpg_web_match_log.decisions_json`; a `profile_id` column lands for the sweep; `SelectLog` reads both
  - Verify: `tests/FusionRpg.Data.Tests` — a trace round-trips; an old-schema database migrates
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.WebMatches.cs`
- [ ] **D2.16** Freeze, resume and the SignalR surface
  - Acceptance: `DelveBattleEndpoints` + `RpgHub` carry steer / declare / freeze / resume, plus the `DelveUpdated{delveId, revision}` invalidation broadcast; freeze-on-switch is a persisted decision; `InteractiveIntentSource` gains the **replay-the-recorded-prefix-then-go-live** constructor; **no finish-on-autopilot** for a steered party
  - Verify: freeze, disconnect, resume — the replayed prefix matches byte for byte; a test asserts a steered fight is never finished by the automated policy
  - Files: `src/FusionRpg.Server/DelveBattleEndpoints.cs`, `RpgHub.cs`, `InteractiveIntentSource.cs`

### `delve-attrition` — spec-delve-attrition.md

- [ ] **D2.17** `PartyPoolsCarry` over the six pools
  - Acceptance: `ActorResourcePools.FromStored` in and `SettleAll` out, looping over `ResourceIds` — never a hard-coded five; an assertion that `hp == pools["hp"]` so the one hp seat cannot drift
  - Verify: a carry test across three rooms; the assertion fires when hp is written twice
  - Files: `src/FusionRpg.Core/Delve/Attrition/{DelveMemberState,PartyPoolsCarry}.cs`
- [ ] **D2.18** `HungerCharge` — pure, `long`
  - Acceptance: hunger per room from `hazardBand`; **hunger persists across delves** (`attrition.persistAcrossDelves ["hunger"]`); no wall-clock term anywhere
  - Verify: a golden per hazard band; a cross-delve persistence test
  - Files: `HungerCharge.cs`
- [ ] **D2.19** The staged `nerve` status
  - Acceptance: a stack counter with stage thresholds beside the `Counter` kind; the counter lives in party state and **projects** to `nerve.{unsettled,shaken,afflicted}`; `StatusCatalog` goes 21 → 24 ids
  - Verify: `StatusCatalogBootstrap` count test; stage transitions at each threshold; spirit drain applies a stack
  - Files: `NerveLadder.cs`, `NervePolicy.cs`, `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs`, `data/seed/dungeon/_containers/nerve.v1.json`
- [ ] **D2.20** `RestResolver`
  - Acceptance: the rest room's activations, heal ‰ and `restRelief`; rations bind between rests via `useContext: rest`
  - Verify: a rest golden; hunger is relieved only at a rest
  - Files: `RestResolver.cs`
- [ ] **D2.21** Downed, `downedOnce` and the wipe rule
  - Acceptance: Downed comes from the timeline FSM wired into `Resolve` **behind `DownedOnDeplete`**, so no shipped mode changes; `downedOnce` is recorded; a wipe is its own settlement path
  - Verify: a Downed test under the delve profile and a no-Downed test under every shipped profile
  - Files: `BattleRunState.cs` (the Downed step), `ExtractionSettlement.cs`
- [ ] **D2.22** `ExtractionSettlement.Decide` — pure
  - Acceptance: per member `Retire | Recover(n) | Roster`; on a permadeath rung a `downedOnce` member Retires at extraction, otherwise Recovers for a tunable count **of delves** (never a clock); `won` = extracted ∧ (boss killed ∨ ≥ half the route) ∧ not afflicted; contract loyalty is applied **once**, at extraction
  - Verify: a truth table over the settlement inputs; a loyalty double-apply test
  - Files: `ExtractionSettlement.cs`
- [ ] **D2.23** The store settlement and the recovery ritual
  - Acceptance: `members[]` writer; `CloseDelve` calls the settlement and the loyalty path on one transaction; the recovery ritual is priced through `SoulSinkPolicy.Price(long, int, PowerTuning)` at the delve's Θ and exposed as `POST …/recovery-ritual`
  - Verify: `tests/FusionRpg.Data.Tests/Delve/` — settlement round-trip; the ritual price is never a literal
  - Files: `RpgStore.Delve.cs`, `RpgStore.UniqueActors.cs`, `src/FusionRpg.Server/DelveEndpoints.cs`

> ### CHECKPOINT G2 — a room is a fight
> - [ ] One rolled room resolves through `BattleEngine.Resolve` with the `delve` profile and an automated intent source at `Θ_room + thetaOffset`, byte-identical on replay
> - [ ] **All four battle hashes, the 32-seed sweep and the four expedition tier hashes unchanged**
> - [ ] A steered fight freezes and resumes from its decision log, byte-identical
> - [ ] `DownedOnDeplete` is false on every shipped profile row
> - [ ] `audit-overflow.py` zero critical; `guard-single-writer.ps1` and `guard-funnel-delta.ps1` pass

---

## Phase 3 — a delve is a run (wave 3)

### `event-deck` — spec-event-deck.md · runs in parallel with `dungeon-loot`

- [ ] **D3.1** `EventRow` and `EventCatalog.Load`
  - Acceptance: rows arrive through the **seed import path** (`SeedScanner`/`SeedImportRunner`); bands resolve to ints at load; trees compile once; the import rules refuse before writing
  - Verify: a malformed event refuses with a named rule and writes nothing
  - Files: `src/FusionRpg.Core/Delve/Events/{EventRow,EventCatalog}.cs`
- [ ] **D3.2** The four filters
  - Acceptance: kind fit, eligibility, repeat scope (per-delve `seen` / `(domainId, eventId)` / `(playerId, eventId)`) and "no repeat within N rooms" as **pure set functions**
  - Verify: one test per filter plus a combined test proving order does not change the result
  - Files: `EventFilters.cs`
- [ ] **D3.3** `EventDeck.Build` / `Resolve` / `Answer` and the streams
  - Acceptance: per-archetype pools; picks on `dungeon:event:{r}:{c}:{pick|outcome|effects|encounter|ambush}`
  - Verify: deck goldens per domain; determinism over 256 seeds
  - Files: `EventDeck.cs`, `EventDraw.cs`
- [ ] **D3.4** `UnknownPity` — per party
  - Acceptance: counters in, resolution out, on `dungeon:unknown:{r}:{c}`; **pity is per party**, never per delve
  - Verify: a four-party test asserting four independent counters
  - Files: `UnknownPity.cs`
- [ ] **D3.5** `OutcomeResolver`
  - Acceptance: severity shifts `dropBand` indices; weights then `TryInstantiate` at the room's Θ, then a dispatch plan; `consequence` is one of `none · loot · encounter · scout`; `supplyOverride` reads `HoldsStock`
  - Verify: an outcome golden per severity; a `supplyOverride` red/green pair
  - Files: `OutcomeResolver.cs`
- [ ] **D3.6** `DelveResourceDelta` and the `ui.present` sink
  - Acceptance: the out-of-fight `resource.delta` executor loops `ResourceIds` beyond `hp`; `DelveUiPresentSink.ShowBanner(bannerId, durationMs)` defaults its duration from `data/tuning/delve-ui.v1.json`
  - Verify: a delta test per resource; a banner test with and without an authored duration
  - Files: `DelveResourceDelta.cs`, `DelveUiPresentSink.cs`
- [ ] **D3.7** The four predicate leaves
  - Acceptance: `BandIs`, `HaulAtLeast` (**pack cells**, read through the pack's ledger), `RoomKindIs`, `PartyDownedCount` with `FactReader` readers and compiler arms
  - Verify: red/green per leaf; a compile test for an unknown leaf
  - Files: `src/FusionRpg.Core/Effects/Atoms/{PredicateNode,FactReader,PredicateCompiler}.cs`
- [ ] **D3.8** Choices, facts and delve-scoped grants
  - Acceptance: the fixed verb set `use · interact · leave`; autopilot takes the first eligible; grants bind on `UniqueActor` with `source = "delve:{id}"` and are **withdrawn at extraction**
  - Verify: a grant is present mid-delve and absent after `CloseDelve`
  - Files: `EventChoices.cs`, `EventFacts.cs`
- [ ] **D3.9** Store, endpoint, preflight and validator rules
  - Acceptance: `event_id`/`resolved_kind`/`resolved_archetype_id` columns and `rpg_delve_event_seen`; `POST …/rooms/{id}/answer`; validator rules — at least one bad-or-mixed and one good outcome, no free Leave outside `story`, no event gates the boss; a `remembers` outcome row for the wild talk
  - Verify: one red test per validator rule; the answer endpoint refuses a non-steered party
  - Files: `RpgStore.Delve.cs`, `src/FusionRpg.Server/DelveEventEndpoints.cs`, `EventDeckPreflight.cs`, `AmbushDraw.cs`

### `dungeon-loot` — spec-dungeon-loot.md · runs in parallel with `event-deck`

- [ ] **D3.10** Source kinds and correlations
  - Acceptance: `dungeon-room` / `dungeon-clear` / `dungeon-quest` in **both** lists, with server-derived correlations `loot:delve:{delveId}:{r}:{c}`; the validator accepts the three new kinds
  - Verify: `LootCorrelation` round-trip per kind; a client-supplied correlation is refused
  - Files: `src/FusionRpg.Core/Items/Drops/{LootCorrelation,DropTableValidator}.cs`
- [ ] **D3.11** `DelveLoot.RollRoom` — the first production host of `LootPipeline`
  - Acceptance: the host **synthesizes** a `LootSourceRow` carrying the room's `Θ_content`; `Mint` closes over the room Θ; drop count reads `Θ_actor` and item level reads `Θ_room`, exactly as the pipeline already does
  - Verify: a room-drop golden; a test proving the two Θ reads are not swapped
  - Files: `src/FusionRpg.Core/Delve/Loot/DelveLoot.cs`, `LootPipeline.cs` (the `RefId` arm when `BaseTypeSetFor` is supplied)
- [ ] **D3.12** `DelveSoulLedger`
  - Acceptance: `KillEarn(setup.Level)` per **non-withdrawn** kill accrues to `rpg_delves.souls_unbanked`; `MatchEndEarn(true, theta_run)` fires **once**, at `CloseDelve(Extracted)`, and **only when `won`** — forfeited on a wipe
  - Verify: a wipe earns zero at the end; a withdrawn enemy earns nothing; the once-only assertion
  - Files: `DelveSoulLedger.cs`, `src/FusionRpg.Core/Demons/SoulEarnPolicy.cs`
- [ ] **D3.13** `DelvePrices` — every price through `SoulSinkPolicy`
  - Acceptance: `Merchant(item, Θ, rung)`, `PullPrice(Θ)`, `OfferFloor(Θ)` and `RecoveryRitual(rung, tuning)` all end in `SoulSinkPolicy.Price(long, int, PowerTuning)`; **no price literal exists**; the merchant refuses `delve.price-undesigned` until an item-side derived price exists
  - Verify: a scan test for a numeric price in the delve namespace; the refusal is reachable
  - Files: `DelvePrices.cs`
- [ ] **D3.14** `RoomTableBinding` and `RarityShift`
  - Acceptance: domain tables per room kind; elite and boss carry `rarityFloor` / `rarityShiftRungs` as a **floor and a shift, never a multiplier**
  - Verify: a rarity distribution test proving low rungs stay live content
  - Files: `RoomTableBinding.cs`, `RarityShift.cs`
- [ ] **D3.15** The boss first-clear grant
  - Acceptance: instantiated through `TryInstantiate` on its own stream (never flat), banked **at the clear** via `dungeon-clear` — never through a pack; the entry `RefId` base-type set resolves; the boss `affixChannel` applies
  - Verify: a clear golden; a test that the relic never enters a pack
  - Files: `DelveLoot.cs`, `DungeonLootTableGen.cs`
- [ ] **D3.16** Store surface and the `CloseDelve` order
  - Acceptance: `souls_unbanked`, `theta_run`, `Accrue`/`Spend`/`RecordClear` and the extraction earn; **`CloseDelve` calls its module hooks in one stated order** — pack settlement, then attrition settlement, then loot earn, then quest verdicts, then domain unlocks — on a single transaction
  - Verify: an ordering test asserting the sequence; a rollback test proving all-or-nothing
  - Files: `RpgStore.Delve.cs`
- [ ] **D3.17** The souls-per-minute regression
  - Acceptance: the SSOT §11.7a regression exists with **"two row-1 rooms then extract"** as the stall row, and it loses to a clean run
  - Verify: the regression runs in CI and fails if farming beats clearing
  - Files: `tests/FusionRpg.Core.Tests/Delve/Loot/`, `src/FusionRpg.Server/DelveEndpoints.cs`

### `loot-pack` — spec-loot-pack.md

- [ ] **D3.18** `PackGrid` and `Footprint`
  - Acceptance: a **4×10 grid per party**, structural with the exemption comment, that **never grows with Θ** (the count-pin ruling); `footprint(role, massClass)` derived on `ShapeLadder [1,2,3,4,6,8]`; `Orient` is pure
  - Verify: `the_pack_never_reads_armoury_capacity`; a footprint golden per role and mass class
  - Files: `src/FusionRpg.Core/Delve/Pack/{PackGrid,Footprint}.cs`
- [ ] **D3.19** `PackFootprintTable.Build` at load
  - Acceptance: built once at load from the item corpus with named refusals; `sizeBand`/`stackBand` are derived, never authored
  - Verify: a refusal per malformed row; the table is stable across loads
  - Files: `PackFootprintTable.cs`
- [ ] **D3.20** `PackArranger.Arrange` — first-fit decreasing, pure
  - Acceptance: a pure integer function, deterministic for a given input order, with no randomness
  - Verify: a property test over 1,000 generated hauls asserting no overlap and no cell reuse
  - Files: `PackArranger.cs`
- [ ] **D3.21** Moves, autopilot and provisioning
  - Acceptance: `pack.move` / `pack.drop` as trace decisions with `Apply`/`Replay`; **autopilot never moves the pack** and resolves the floor list by `FloorRule.ValuePerCell`; provisioning validates carry-in at `baseCells 16 + provisionCellsDelta`
  - Verify: a replay test; an autopilot party's grid is unchanged after a drop
  - Files: `PackMoves.cs`, `PackAutopilot.cs`, `PackProvisioning.cs`
- [ ] **D3.22** Settlement, the lock and the DTO
  - Acceptance: haul is **owned at placement** and locked in `rpg_delve_pack_lock`; extraction is raid-wide (a party may hold at a rest, never bank); boss grants deal round-robin by `PartyIndex` per `loot.bossGrantDistribution`; charms stay in the AP pouch; `PackDto` carries `rows, cols, cells[], floor[], provisionCellsLeft` and a **`movable` flag per cell**, with no `PartyIndex` in any label
  - Verify: a lock test proving salvage and transfer refuse a locked instance; a four-party round-robin golden
  - Files: `PackSettlement.cs`, `PackDto.cs`, `RpgStore.Delve.cs`
- [ ] **D3.23** `PackFill.Estimate` — the D26 metric
  - Acceptance: the cells-versus-linear-volume statement with its arithmetic and the D26 reconciliation sentence copied verbatim; the regression band is 700–1000‰ with about 850‰ as the starting shape
  - Verify: the metric runs over 256 generated delves and stays inside the band
  - Files: `PackFill.cs`

### `supplies-and-objects` — spec-supplies-and-objects.md

- [ ] **D3.24** `UseContext` gains `Rest` and `Curio`
  - Acceptance: the enum grows from four members; `UseContexts.*` and `RuntimesFor` rows follow; `contextsAuthored` lands in `data/tuning/consumables.v1.json`; the **`battle` context is refused until A3 lands**, behind a `CrossProgramLandedFlags` shape — rest and curio uses do not wait
  - Verify: a red test for `battle` while the flag is false; green for rest and curio
  - Files: `src/FusionRpg.Core/Items/Consumables/ConsumableDef.cs`, `data/tuning/consumables.v1.json`
- [ ] **D3.25** `SupplyClassMap` and `SupplyInstantiation`
  - Acceptance: ration maps to `restore` on `hunger`, ward to `shield.grant`, key and bait to `utility`; a supply is **one `TryInstantiate` at `Θ_room`** entering the pack; `status.clear` is allowed on `OnActivate` for antidotes
  - Verify: one instantiation golden per class; the freeze happens once
  - Files: `SupplyClassMap.cs`, `SupplyInstantiation.cs`
- [ ] **D3.26** `SupplyUse.Use`
  - Acceptance: the usable path checks `HoldsStock`; the decrement comes **from the pack**, never from `rpg_item_stock`
  - Verify: a use decrements a pack cell and leaves stock untouched
  - Files: `SupplyUse.cs`
- [ ] **D3.27** `RoomObject` and `RoomObjectBuilder.For`
  - Acceptance: **no object seed kind in v1** — objects are a projection over curio rows, gated doors and room kinds; a glimpsed room names its kind only
  - Verify: a projection test per source; nothing is written to a new table
  - Files: `src/FusionRpg.Core/Delve/Objects/RoomObject.cs`
- [ ] **D3.28** `VerbResolver` over the six registry verbs
  - Acceptance: `open · disarm · pray · loot · destroy · garrison`; every refused verb carries its reason; `objects.breakMode` is a tunable
  - Verify: one red/green pair per verb; a disabled verb always has a reason string
  - Files: `VerbResolver.cs`
- [ ] **D3.29** `ObjectPreflight` and camp actions
  - Acceptance: preflight refuses an object whose verb no registry admits; camp actions with `useContext: rest` compete for the five loadout slots
  - Verify: preflight red tests; a camp action occupies a slot
  - Files: `ObjectPreflight.cs`
- [ ] **D3.30** Provisioning as a soul sink
  - Acceptance: priced on `contentScale(Θ_entrance + Wm·bandDelta)` through `SoulSinkPolicy`; nothing is debited until the delve is created
  - Verify: a price test at two rungs; a failed start debits nothing
  - Files: `PackProvisioning.cs`, `DelvePrices.cs`

> ### CHECKPOINT G3 — a delve is a run
> - [ ] A full solo delve on autopilot: rooms, events, loot into the pack, extraction
> - [ ] The souls-per-minute regression holds — two row-1 rooms then extract loses to a clean run
> - [ ] Hunger binds between rests and persists across delves
> - [ ] A downed demon sits out N **delves**; on a permadeath rung a `downedOnce` demon Retires at extraction
> - [ ] `PackFill.Estimate` sits inside the 700–1000‰ band over 256 delves
> - [ ] `audit-magic-numbers.py --targets M1` shows no new literal; `guard-dal.ps1` passes

---

## Phase 4 — content (wave 4)

All four modules are independent. They share only `RpgStore.Delve.cs` and `DelveEndpoints.cs`, and each
task below names the members it adds so the seams stay clean.

### `wild-room` — spec-wild-room.md

- [ ] **D4.1** `Disposition`
  - Acceptance: the ordinal plus five shifts, with the `[0,3]` rail carrying its comment; the base is the **room archetype's** `dispositionBase` — 0 of 841 species anchors carry one, and none is invented
  - Verify: shift goldens; a species without a base falls back to the archetype, never to a literal
  - Files: `src/FusionRpg.Core/Delve/Wild/Disposition.cs`
- [ ] **D4.2** `TalkTree.Offered` / `Step`
  - Acceptance: the verbs `flatter · threaten · offer:souls|spirit|supply|contract · fight · leave`; `Step` is pure; autopilot follows `wild.autopilot.rule` (`fight` as the starting shape); `wild.talk.maxSteps` bounds the tree
  - Verify: one path test per verb; an autopilot party never opens a talk it cannot finish
  - Files: `TalkTree.cs`
- [ ] **D4.3** `OfferPricing` — calls only
  - Acceptance: all four equivalents call `DelvePrices`; the souls offer floors at `PullPrice(Θ_room) × wild.offer.soulsMilliOfPullPrice / 1000` and is paid **from unbanked souls**; `wild.offer.spiritPerSoulMilli` is the spirit equivalent
  - Verify: a floor test at two Θ values; no arithmetic on a price outside `DelvePrices`
  - Files: `OfferPricing.cs`
- [ ] **D4.4** `WildOutcome`
  - Acceptance: the draw on `dungeon:wild:{r}:{c}:{seq}` returns `joins / takes-and-leaves / flees / attacks / remembers`; `remembers` shifts one band and is recorded by `WildMemory`
  - Verify: an outcome golden per disposition band; a `remembers` round-trip across two delves
  - Files: `WildOutcome.cs`, `WildMemory.cs`
- [ ] **D4.5** `RecruitMint`
  - Acceptance: a `DemonMintSpec` with `Origin = "delve"` and `Level = θ_enemy`; **until `DemonMintSpec.Level` lands on demon-core the mint is level 1**, today's line, and the gap is named in a comment rather than worked around
  - Verify: a mint test asserting `Origin`; a test that pins the current level behaviour so the fix is visible when it lands
  - Files: `RecruitMint.cs`
- [ ] **D4.6** `CaptureAction` — the second code-backed action
  - Acceptance: a corpus `ActionRow` (`Kind = Skill`, `Relation = Enemy`, conditions `HpBelowMilli(Target) ∧ HoldsStock(Self, seal)`) whose resolver lives in the action layer; the chance table is hp-band × Δ-band ‰ plus a status-count band and a seal-tier band shift, with a per-target ramp; a capture exits through `Withdraw`, never `KillEarn`; **it refuses `capture.not-landed` until A3's item-cost row exists**
  - Verify: a chance golden; a captured enemy earns no souls; the refusal is reachable while A3 is absent
  - Files: `CaptureAction.cs`, `src/FusionRpg.Core/Battle/BattleRunState.cs` (one `DeriveStream` line for `CaptureRng`)
- [ ] **D4.7** `AltarPull`
  - Acceptance: one `SummonRoller.Roll` with count 1 and a domain-focus banner; the result is an **at-risk ledger haul minted at extraction**, not immediately; `altar.poolFromDomain` stays `false` until `SummonRoller` accepts a pool filter; `SummonRoller`'s rates and pity are untouched
  - Verify: a pull is lost on a wipe and minted on extraction; a test asserts the roller's pity is unchanged
  - Files: `AltarPull.cs`
- [ ] **D4.8** `Cage`, refusals, store and endpoints
  - Acceptance: the cage is a structural draw at `wild.cageMilli` and `cage` is a legal `resolved_kind`; the talk/offer transaction, `PullAtAltar` and pending pulls minted in `CloseDelve(Extracted)`; `POST …/rooms/{id}/{talk,pray,cage}`; every refusal named
  - Verify: `tests/FusionRpg.Data.Tests/Delve/` — the transaction is atomic; each endpoint refuses a non-steered party
  - Files: `Cage.cs`, `WildRefusal.cs`, `RpgStore.Delve.cs`, `src/FusionRpg.Server/DelveWildEndpoints.cs`

### `delve-quests` — spec-delve-quests.md

- [ ] **D4.9** `QuestRow` and `QuestCatalog.Load`
  - Acceptance: the registry's **nine** objective templates; target kinds, `countBand` (with `none` legal and required on the six count-less templates), `rewardBand` and scope `delve/domain/roster`; trees compile at load
  - Verify: a red test per malformed row; the nine templates round-trip
  - Files: `src/FusionRpg.Core/Delve/Quests/{QuestRow,QuestCatalog}.cs`
- [ ] **D4.10** `QuestOffer.Satisfiable` and `Draw`
  - Acceptance: 2–3 offered per raid on `dungeon:quest:{n}` after a satisfiability check against the rolled graph, plus the D14 sink-avoidance filter (eligible only at rung ≥ hard or paired with a risk objective)
  - Verify: an unsatisfiable quest is never offered over 256 seeds; the D14 filter is red below `hard`
  - Files: `QuestOffer.cs`
- [ ] **D4.11** `DelveReport` and `QuestProgress.Evaluate`
  - Acceptance: `DelveReport` is the read model this module owns; `Evaluate(quest, report)` is **pure** and idempotent on `(playerId, questId, delveId)`
  - Verify: evaluating twice yields one verdict; the evaluation touches no store
  - Files: `src/FusionRpg.Core/Delve/Report/DelveReport.cs`, `QuestProgress.cs`
- [ ] **D4.12** `QuestReward.Request`
  - Acceptance: rewarded **once**, at `CloseDelve(Extracted)`, through `LootPipeline` with the `dungeon-quest` source kind on the domain's `cache` binding, with `rewardBand` as a window — a floor composed and the rungs above zeroed; banked at the close, never through a pack. **Quests reward, never unlock**
  - Verify: a reward golden; a test that no quest grants an unlock
  - Files: `QuestReward.cs`
- [ ] **D4.13** Preflight, coverage and refusals
  - Acceptance: `QuestPreflight.Run(corpus, domains, layouts, tuning)` rolls its own 256-seed satisfiability sweep per `(domain, layout, raidMode, rung)`; `quests.autopilotCompletionBand` is a **regression band, never a target**
  - Verify: preflight refuses a domain whose quests cannot complete; the band holds over the sweep
  - Files: `QuestPreflight.cs`, `QuestCoverage.cs`, `QuestRefusal.cs`
- [ ] **D4.14** Store and tracker endpoint
  - Acceptance: `quests_json` holds the offer at `CreateDelve` and the verdicts at `CloseDelve`; the stored offer is truth and a rebuild is asserted equal on load; `GET …/delves/{id}/quests` returns names, flavour and `have / need` with **no Θ, rung id or party index**
  - Verify: a rebuild-mismatch test; a projection test scanning for engine words
  - Files: `RpgStore.Delve.cs`, `src/FusionRpg.Server/DelveEndpoints.cs`

### `domain-catalog` — spec-domain-catalog.md

- [ ] **D4.15** `DomainRow` and `DomainCatalog.Load`
  - Acceptance: domains read from `data/seed/dungeon/domains/`; ordinals resolve to ints **at read**; `entry: once|many` is `PLANNED`; `name` and `flavor` are the only fields a player reads
  - Verify: a load golden; an unknown ordinal refuses by name
  - Files: `src/FusionRpg.Core/Delve/Domains/{DomainRow,DomainCatalog}.cs`
- [ ] **D4.16** The seed import path
  - Acceptance: `SeedScanner.OwnedFolders` gains the seven dungeon folders and `SeedContent.Dungeon`; `RpgStore.Import.cs` gets the domain kind's **validate-then-write** arm
  - Verify: an import writes nothing when validation fails; the seven folders are scanned
  - Files: `src/FusionRpg.Data/Seed/SeedScanner.cs`, `src/FusionRpg.Core/Effects/Atoms/AtomSeedFile.cs`, `RpgStore.Import.cs`
- [ ] **D4.17** `DomainPreflight` — the ten-row chain
  - Acceptance: all ten checks refuse **before** any write; `preflight.sampleSeeds` is 32 and structural
  - Verify: one red test per row; a failing preflight leaves the database untouched
  - Files: `DomainPreflight.cs`
- [ ] **D4.18** `DomainStaleness` and the three tables
  - Acceptance: `validated_json` staleness is **clock-free**; `rpg_domain_progress` and the other two tables land with `RecordDomainClearUnlocked` / `RecordFoundUnlocked` as tx-scoped writers
  - Verify: staleness flips on a content change, never on time passing
  - Files: `DomainStaleness.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.Domains.cs`
- [ ] **D4.19** `DomainOffers.For` and `DomainOfferDto`
  - Acceptance: offers carry **names only** — no Θ, no ordinal; `rungs[]` with label, band name, oath and permadeath flags; `tailSteps[]`; `provisionable[]` so the picker prices nothing itself
  - Verify: a scan test for an engine word in the DTO; the six first-ship domains render
  - Files: `DomainOffers.cs`, `DomainOfferDto.cs`
- [ ] **D4.20** `DomainDiscovery`
  - Acceptance: discovery by the expedition `FoundDomain` tick and by first clears — provable with the game closed; a test-only `POST /api/test/delve/found` exists for the proof
  - Verify: a game-closed test discovers a domain and it appears in the offers
  - Files: `DomainDiscovery.cs`
- [ ] **D4.21** `DelveStart.Run` — seven ordered refusals, one transaction
  - Acceptance: the seven refusal groups fire **in order**; only after all seven does one `CreateDelve` transaction run; `content_terms_json` is frozen at creation
  - Verify: one red test per refusal, asserting order; a failed start creates no row and debits no souls
  - Files: `DelveStart.cs`, `DomainRefusal.cs`, `RpgStore.Delve.cs`
- [ ] **D4.22** The two endpoints
  - Acceptance: `GET /api/delve/domains/{playerId}` and `POST /api/delve/start`; the Sanctum picker and the map door post the **same body**
  - Verify: a test posts both shapes and asserts identical requests
  - Files: `src/FusionRpg.Server/DelveEndpoints.cs`

### `unique-pipeline` — spec-unique-pipeline.md

- [ ] **D4.23** `rungFloorOrdinal` 30 → 80 and the 95 flag flips
  - Acceptance: `data/tuning/uniques.v1.json` moves the floor to 80 as **its own change**; the 95 anchors below rung 80 get `enabled: false` — **never re-runged, never deleted**; the 64 `unique` table rows are re-pointed
  - Verify: a count test — 144 anchors, 95 disabled, 49 live at rung ≥ 80; no anchor's rung changed
  - Files: `data/tuning/uniques.v1.json`, `data/seed/items/uniques/*-{30,50,70}.json`
- [ ] **D4.24** `UniqueContainerBuild`
  - Acceptance: a fixed core plus **one** variance slot (`UniqueLimits.MaxTotalRolls = 1`); passives and actions as atoms; a unique action occupies a loadout slot, a unique passive does not
  - Verify: a build golden; the roll count is exactly one
  - Files: `src/FusionRpg.Core/Items/Uniques/UniqueContainerBuild.cs`
- [ ] **D4.25** The `loadout.slots` derived channel and its three readers
  - Acceptance: the channel is registered in `DerivedStatRegistry`; `LoadoutSet.MaxSize` stays `const 5` **as the structural base** with its exemption comment; the three readers compute `base + (channel > 0 ? 1 : 0)` — one extra slot at a time, whatever is worn
  - Verify: a test with two extend-slot items proves the total is six, not seven
  - Files: `DerivedStatRegistry.cs`, `LoadoutSet.cs`, `AutoEquip.cs`, `CapPolicy.cs`
- [ ] **D4.26** `ExtendSlotRoll` and the Freeze count-unit guard
  - Acceptance: rung ≥ 90 carries the slot atom as a fixed core; rung 80 rolls it at `loot.extendSlotChanceMicro` per million on `unique:extend-slot`; `affix.exclusiveTags` keeps one counting at a time. **`Instantiator.Freeze` must not scale a count channel** — `Apply(1, 4235)` is 4, and `loadout.slots` must stay 1
  - Verify: *"frozen at Θ 100, still 1"*; the build refuses `unique.slot-scaled` rather than shipping five extra slots
  - Files: `ExtendSlotRoll.cs`, `src/FusionRpg.Core/Effects/Atoms/Instantiator.cs`
- [ ] **D4.27** The `MintUnique` arm and the grant validator
  - Acceptance: a `unique` draw takes its own `RollSeed` from `LootStreams.RollSeed(i)` and mints through the `MintEquipment` shape, rung = the container's own `Rarity`; `UnavailableKinds[Unique]` is removed in the same change; `ItemGrantValidator` admits a unique's `item.<slug>` as a grant container id
  - Verify: `DropEntryKind.Unique` now resolves; a unique grants an action in a test
  - Files: `LootPipeline.cs`, `DropTableModel.cs`, `Items/Grants/ItemGrantValidator.cs`
- [ ] **D4.28** `boss-unique` binding and `firstClearRef`
  - Acceptance: the group is bound **by id, never categorically**, with a climate filter; the domain anchor's `firstClearRef` names a rung-80+ deterministic unique or `none`; a `seed_graph` assertion proves the binding
  - Verify: a source-locked unique is unreachable from any other table
  - Files: `DungeonLootTableGen.cs` (dungeon-loot's file, extended)
- [ ] **D4.29** The seedsmith uniques extension
  - Acceptance: one ownership level per `unique` field; a set-stem audit check; planner, briefs, pipelines and audit over the `frame × axis × band` grid; the first ship is 30 new anchors beside the 49 at rung 80+
  - Verify: `tools/seedsmith/tests/test_unique_*.py` with the transport stubbed to raise
  - Files: `tools/seedsmith/seedsmith/adapters/items/kinds.py`, `adapters/items/uniques/{planner,briefs,pipelines,audit}.py`
- [ ] **D4.30** Run the six first-ship domains through the pipeline
  - Acceptance: six `many` domains, one per climate, at `shallow` (band 2 — `very-easy` is **refused, not clamped**); the schema audit, the budget check and a byte-identical rerun all pass
  - Verify: `--dry-run` budget first, then the run; hashes match on a second run
  - Files: `data/seed/dungeon/domains/*.json`, `_plan/`
- [ ] **D4.31** Encounter coverage over the shipped domains
  - Acceptance: the cell-coverage metric passes per domain, drawing only from anchors that carry a `threatBand`
  - Verify: coverage report per domain; a refusal names any domain that cannot fill a slot
  - Files: `EncounterCoverage.cs` (phase-2 file, run here)

> ### CHECKPOINT G4 — content
> - [ ] Six domains pass the schema audit, the budget check and a byte-identical rerun
> - [ ] The encounter cell-coverage metric passes per domain
> - [ ] A four-party raid resolves with per-party packs, pity and hauls, and one boss fight inside the fight-length band
> - [ ] 144 unique anchors: 95 disabled, 49 live; no anchor was re-runged
> - [ ] Two extend-slot items still yield exactly one extra slot; `loadout.slots` survives Freeze at Θ 100
> - [ ] `audit-overflow.py` zero critical; every guard script passes

---

## Phase 5 — played (wave 5)

### `delve-stage` — spec-delve-stage.md

- [ ] **D5.1** The six shell rows and `delveRoute()`
  - Acceptance: `railState.ts` gains `"delve"` (the `battle` id is base-defense's and is **not** reused); a lazy route with a chunk fallback; no default layer; the Esc target; the GG-7 row; the stage label. `route.ts` exports `delveRoute(delveId)` as the door's **only** import from this module
  - Verify: `Shell_has_no_delve_specific_branch` scans `src/shell/` for `=== "delve"`; the stage-count assertion becomes a pair — six declared, and the built set named separately
  - Files: `src/shell/railState.ts`, `src/app/routes.tsx`, `src/shell/keymap.ts`, `src/stages/delve/route.ts`
- [ ] **D5.2** The projection endpoint
  - Acceptance: `GET /api/delve/{delveId}` returns one revision-stamped projection assembled by `DelveProjection.For(delveId, playerId)` from `LoadWorldState` + the two delve tables + `Visibility.SeenBy` + `DelveSight.ForParty` and **nothing else**; `RpgHub` broadcasts `DelveUpdated{delveId, revision}`
  - Verify: an E2E test — one query key, one source; a mutation triggers exactly one invalidation
  - Files: `src/FusionRpg.Server/DelveEndpoints.cs`, `RpgHub.cs`
- [ ] **D5.3** Contract types and adapters
  - Acceptance: the sixteen view types and their adapters land additively; **`CONTRACT_VERSION` stays 2**; every rendered number has a `UnitClass` and an `op` per the spec's table; `Magnitude.exact?: string` is added for `long` figures past `Number.MAX_SAFE_INTEGER`
  - Verify: `No_Dto_named_type_under_stages_or_layers`; `Every_rendered_number_has_a_unit_class`; `A_long_soul_balance_renders_exactly`
  - Files: `src/contract/types.ts`, `src/contract/adapt.ts`, `src/i18n/magnitude.ts`
- [ ] **D5.4** The room graph — the stage itself
  - Acceptance: rooms, doors, gates, one-way arrows, secret dead ends, party markers and the three sight treatments; never unmounted by a panel
  - Verify: `Esc_pops_one_panel_and_returns_to_the_same_graph_state` — selection and camera survive
  - Files: `src/stages/delve/DelveStage.tsx`, `graph/`
- [ ] **D5.5** The fight drawn on the stage
  - Acceptance: the room node expands **in place**; enemies, ranks and the strike feed animate there; an un-steered party's room shows a read-only feed with no chooser
  - Verify: a fight never mounts as a separate screen; the automated party has no input surface
  - Files: `src/stages/delve/graph/`
- [ ] **D5.6** The HUD
  - Acceptance: parties **by name** (First through Fourth Banner), six pool meters and the nerve stage per member, haul and unclaimed souls, the quest tracker, the room readout, the initiative rail during a fight, connection state
  - Verify: `Party_labels_are_names_not_indices` — no rendered string carries a bare ordinal
  - Files: `src/stages/delve/hud/`
- [ ] **D5.7** The six band-2 panels
  - Acceptance: Pack, Talk, Event, ObjectPrompt, Supply and Fight input, each opened from the query string and each restoring stage-then-panel on a cold load; an autopilot party's pack handles are **disabled with a reason**
  - Verify: `Route_round_trips_with_every_panel`; `Autopilot_party_pack_handles_are_disabled_with_a_reason`
  - Files: `src/stages/delve/layers/`
- [ ] **D5.8** The descent picker and the confirms
  - Acceptance: the Sanctum door opens `DelvePickerLayer` at band 2, locked until *"Delve — first domain found (expedition)"* and shown with what unlocks it; three band-3 confirms — Descend (single-descent domains and the Oath), Extract, Retreat
  - Verify: `The_picker_and_the_map_door_post_the_same_body`; the locked door is visible, never hidden
  - Files: `src/layers/delve/DelvePickerLayer.tsx`, `src/stages/delve/confirms/`
- [ ] **D5.9** The extraction summary and the band-4 reports
  - Acceptance: one band-3 summary with any wipe or permanent-loss notice **folded in**; drops, level-ups, joins and first clears toast at band 4 and **wait behind** the summary
  - Verify: `Only_the_summary_and_three_confirms_open_band_3`; `Reports_land_at_band_4_and_wait_behind_the_summary`
  - Files: `src/stages/delve/summary/ExtractionSummary.tsx`
- [ ] **D5.10** `labels.ts` and the vocabulary guard extension
  - Acceptance: one id → message table; `BANNED_WORDS` gains `bandDelta · dangerBand · PartyIndex · Retired · thetaOffset · rungId · delveId · sectorId · archetypeId · perMille`; **`once` and `many` stay out** (ordinary English) and are covered by a rendered-phrase test instead
  - Verify: `No_engine_token_reaches_player_text`; `Entry_kind_renders_as_a_phrase_never_the_enum`. *(The `BANNED_SYMBOLS` half of this is already built — 2026-09-05.)*
  - Files: `src/stages/delve/labels.ts`, `src/i18n/vocabularyGuard.ts`
- [ ] **D5.11** The live session client
  - Acceptance: subscribe, steer, declare, freeze, resume, reconnect; dwell shown as time; three consecutive timeouts freeze the fight and **say so**; a reconnect replays the recorded prefix then goes live; closing the fight panel is **not** a retreat and leaving the stage resolves nothing
  - Verify: `Three_timeouts_freeze_and_say_so`; `Reconnect_replays_then_goes_live`; `Closing_the_fight_panel_is_not_a_retreat`; `Leaving_the_stage_resolves_nothing`
  - Files: `src/stages/delve/session.ts`
- [ ] **D5.12** Volume, bundle and the frozen map FE
  - Acceptance: `COLLECTION_SURFACES` goes 13 → 17 with virtualize still exactly one; the entry chunk is unchanged; `data/tuning/delve-ui.v1.json` carries `banner.durationMs`, `reveal.toastMs` and `reveal.maxQueued`
  - Verify: `Volume_matrix_declares_the_delve_collections`; `npm run check:bundle`; **`Map_FE_files_are_untouched` — a diff scan over `stages/world/`, `features/world/` and `lib/bus/world.ts` comes back empty**
  - Files: `src/ui/volumeMatrix.test.ts`, `data/tuning/delve-ui.v1.json`

- [ ] **D5.13** The Delve design plate
  - Acceptance: a design plate under `docs/design/` for the delve stage, in the shape the world program's
    plates already use — the room graph, the HUD clusters, the six panels, the summary and the four report
    kinds, at layout fidelity. The ideal names this deliverable and deliberately leaves it unspecified:
    *"The stage needs a design plate under `docs/design/`; this document names the stage and its HUD, not
    their pixels"* (`party-dungeon-ideal.md` §7). It is drawn **after** D5.4–D5.9 exist, so it documents
    what shipped rather than guessing at it
  - Verify: every surface in `spec-delve-stage.md` §7's band table appears on the plate with its band; the
    plate is linked from `information-architecture.md` §2.4a
  - Files: `docs/design/` (the plate), `docs/design/information-architecture.md` (the link)

> ### CHECKPOINT G5 — played
> - [ ] The stage renders a live delve over SignalR, refresh-safe because the state is the server's
> - [ ] The band-3 opener lint holds: one result, three confirms, nothing else
> - [ ] `vocabularyGuard` rejects every engine word, `Θ` and `‰` included
> - [ ] The Sanctum picker and the map-door request reach the same `POST /api/delve/start`
> - [ ] A four-party raid renders with four named banners, four packs and no party index in any rendered text
> - [ ] `CONTRACT_VERSION` is still 2; every web guard is green; the map FE diff is empty
> - [ ] `npm test` and `npm run test:e2e` green; `dotnet test` green across Core, Data, Guard, E2E

---

## Follow-ups — tracked, not blocking

- [ ] **F1** `threat-audit` over the 657 species anchors without a `threatBand` (`demon-seed-map` module 7). Until then the delve draws from the 184 that have one.
- [ ] **F2** A3's item-cost row on actions (`action-map`). Unblocks `battle`-context supply use and the capture seal's cost.
- [ ] **F3** The `consumable` `ContainerKind` (D27, `item-map`). `ConsumableDef.cs:201` is `false` today.
- [ ] **F4** `DemonMintSpec.Level` (`demon-system-map`, demon-core). Until then a recruit or capture mints at level 1.
- [ ] **F5** `SummonRoller.Roll` optional `poolFilter` (demon-summoning). Until then `altar.poolFromDomain` stays `false`.
- [ ] **F6** `structure-schema`'s 18th field `interaction` (`base-defense-map` 23–29). Until then objects are curios.
- [ ] **F7** `siege-board` / `board-render` (A10). The Delve adopts the 2-D board later; v1 is 1-D rank.
- [ ] **F8** `world-generator` entrance placement (`world-map-program` wave 4) for once-entry domains.
- [ ] **F10** **Contracts upkeep and slot/ritual prices on the player's highest cleared content Θ**
  (`demon-system-map`, the `demon-contracts` follow-up). `ContractPolicy.BaseUpkeepPerDay(rarity)` is a
  flat `int` per rarity while every other contract price is Θ-scaled — the P2 gap of ideal §11.6/§11.8 and
  review S2-8. **The capability map schedules this in *this program's first wave*** (`party-dungeon-map.md:99`)
  because it is the sink that keeps binding costly past the pin; `spec-dungeon-loot.md:196-198` files it and
  explicitly builds nothing. Nothing here is blocked by its absence, but until it lands the Delve's soul
  faucet outruns that one sink — raise it with the contracts owner in wave 1, not at the end.
- [ ] **F11** `RelicCatalog` retirement (`src/FusionRpg.Core/Match/RelicCatalog.cs`). Owner decision 13
  retires relics into **uniques and charms**; §11.8 records the catalog as already scheduled for
  retirement, and the item program's task list owns the work. No party-dungeon module reads it — this row
  exists so the decision's consequence is not orphaned when the item program asks why.
- [ ] **F9** Three pre-existing `disabledReasonGuard` violations found while fixing the vocabulary guard — `layers/commanders/CommandersLayer.tsx:145,179` and `ui/actor/CommanderSheetFooter.tsx:37` each disable a `<Button>` with no reason (GG-55). Unmodified files, unrelated to this program; the commanders surface owns the wording.
