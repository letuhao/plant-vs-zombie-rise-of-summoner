# Tasks: world map — wave 1 (foundation)

Plan: [world-map-plan.md](world-map-plan.md) · Specs: [world-model](../docs/architecture/world/spec-world-model.md) · [turn-engine](../docs/architecture/world/spec-turn-engine.md) · [world-movement](../docs/architecture/world/spec-world-movement.md)
**Gate:** specs are Draft — pending owner review. No task starts until they are approved.
Adopted in review: discrete-event movement · per-slot guards by encounter id · re-derived turn reports · FE from fixtures. See the plan's options table.

## Phase 1 — Model + storage (no behavior)

- [x] **Task W1: World catalogs + bootstrap validation** *(done 2026-08-21 — 13 tests; Core 1438/1438, Guard 40/40. Lane types exclude `warded`/`severed` by design: those are lane **state** on the row, and the catalog rejects them as types.)*
  - Description: `SectorTypeCatalog`, `SlotTypeCatalog`, `LaneTypeCatalog`, `FactionKindCatalog` in `Core/World`, following `StatusCatalogBootstrap` — stable kebab-case ids, self-validation at bootstrap, unknown ids reject. Climates reuse `ElementTypeId`.
  - Acceptance: every catalog validates (no duplicate ids, cross-catalog references resolve, a type that forbids Seats can never be base-capable); unknown lookups throw with the id in the message.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~World`.
  - Files: `Core/World/{SectorTypeCatalog,SlotTypeCatalog,LaneTypeCatalog,FactionKindCatalog}.cs`, tests. Scope: S.
  - Dependencies: none.

- [x] **Task W2: `WorldState` model + `first-light` template + the seven validation rules** *(done 2026-08-21 — 11 tests, one rejecting case per rule; Core 1456/1456, Guard 40/40. Added `WorldCanonical` (canonical text form) early: the determinism test needed it and W6's state hash will reuse it. Sector climate is nullable — the homeworld is the one place the fracture never touched.)*
  - Description: the in-memory world (sectors, slots incl. `guard_wave_id`/`guard_state`, lanes, factions, entities) with stable ordering; `WorldTemplateCatalog` building the authored six-sector `first-light` deterministically from `(templateId, seed)`; `WorldValidation` implementing the spec's creation rules 1–7.
  - Acceptance: two builds from the same `(template, seed)` are deep-equal; each of the seven rules has a rejecting case; every collection exposes stable iteration order; the template includes at least one guarded slot and one no-Seat sector.
  - Verify: Core filtered tests; a determinism test builds twice and compares canonical serialization.
  - Files: `Core/World/{WorldState,WorldTemplateCatalog,WorldValidation}.cs`, tests. Scope: M.
  - Dependencies: W1.

- [x] **Task W3: Schema + `CreateWorld` / `LoadWorldState`** *(done 2026-08-21 — 11 tests; Data 201/201, Core 1483/1483, Guard 40/40, `guard-dal` OK. DDL lives in `RpgStore.World.cs` with a one-line hook in `EnsureHotSchema` — smallest possible edit to a file another stream is also touching. **Deviation:** the "forced mid-creation failure leaves zero rows" criterion cannot be triggered through the public API, because validation runs before the transaction and makes every constraint violation unreachable — a stronger property than the test asked for. Covered instead by asserting a refused creation leaves no header, no graph, and no listing.)*
  - Description: the seven tables in `EnsureColumn` style; creation in one gate-serialized transaction validating before commit; loading returns the Core model in stable order.
  - Acceptance: create → load → deep-equal with the in-memory build; forced mid-creation failure leaves zero rows across all seven tables; an entity neither at a sector nor on a lane is rejected at the gate.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World`; `.\scripts\guard-dal.ps1`.
  - Files: `Data/Sqlite/RpgStore.World.cs`, schema block, tests. Scope: M.
  - Dependencies: W2.

- [x] **Task W4: DTOs + read endpoints + SIM create hook** *(done 2026-08-21 — 6 E2E tests; E2E 145/145, all four guards OK. The seed is deliberately absent from every wire projection — a client that knows it can predict rolls the server has not committed. `CreateWorldRequest.Seed` is a **string**: a ulong does not survive JavaScript's number type.)*
  - Description: `WorldDtos`; `GET /api/world/{playerId}`, `GET /api/world/{worldId}/state`; `POST /api/test/world/create` behind `FUSIONRPG_SIM=1`; SignalR `WorldUpdated` on revision bump. Reads only.
  - Acceptance: SIM create → state read returns the full graph with layout coordinates and slot guard state; wire projections never leak the world seed.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, tests. Scope: S.
  - Dependencies: W3.

### Checkpoint 1 — the world exists ✅ 2026-08-21
- [x] Core **1489/1489** · Data **202/202** · E2E **145/145** · Guard **40/40**; **all four** guard scripts OK.
- [x] `first-light` creates, persists, reloads byte-identically (`WorldCanonical`), and every malformed case is refused before a single row is written.
- [x] The state DTO is frozen enough for W13's fixture — `GET /api/world/{id}/state` already carries layout coordinates, nullable climate, and per-slot guard state.
- [x] Review pass (`/review`): 3 defects found and fixed with regression tests — culture-sensitive canonical form, untrimmed ids, and intact-guard-without-encounter (which would have soft-locked a sector).

## Phase 2 — the clock (still no gameplay verbs)

- [x] **Task W5: Command model + admission + command store** *(done 2026-08-21 — 10 Core + 7 Data + 1 E2E; Core 1507/1507, Data 209/209, E2E 146/146. `stand-fast` optionally names an entity, which makes the ownership and existence checks real in wave 1 instead of deferred to W9. Payload is typed fields, not a JSON blob — the store serializes it, so a typo is a compile error. Batch submit reports **per command**: one stale order must not throw away the rest of a commander's turn.)*
  - Description: `WorldCommand` records (commanderId, commandId, kind, payload) with admission validation at submit; `rpg_world_commands` keyed `(world_id, turn, commander_id, command_id)`; `POST /api/world/{id}/commands` idempotent on replay. v1 kind: `stand-fast`.
  - Acceptance: a replayed submission changes nothing; a command referencing a missing entity is refused at admission with a reason; one commander's submission never overwrites another's.
  - Verify: Data + E2E filtered tests.
  - Files: `Core/World/Turn/WorldCommand.cs`, `Data/Sqlite/RpgStore.WorldTurns.cs`, `Server/WorldTurnEndpoints.cs`, tests. Scope: M.
  - Dependencies: W4.

- [x] **Task W6: `TurnEngine` — phases, barrier, event queue, report, hash** *(done 2026-08-22 — 20 tests. **Wrote the world's own `TurnEventQueue` rather than reusing `Battle/Timeline/EventQueue`**: theirs ties by insertion sequence and carries cancel/reschedule for combat delay effects, the spec calls for an entityId tie-break (stable if a seeding loop is reordered) and no cancellation — and coupling a determinism-critical path to a file another stream is actively reshaping is not worth ~60 lines. `SeededRng` **is** reused. Calendar rolls implemented (week/month/special/plague) as report entries only; effects belong to sector-development.)*
  - Description: pure `Step(state, commands, seed)` running the locked phase order; the **discrete-event queue** (integer per-mille turn time, ordered by `(timeMilli, entityId)`, monotonicity assert on enqueue); `ITurnBarrier` + `WaitForAllCommitted` as the only implementation; stub commanders auto-committing `stand-fast`; `TurnReport` (accepted and dropped commands with reasons); `StateHasher`.
  - Acceptance: same `(state, commands, seed)` twice ⇒ identical state and hash; an illegal-at-reveal command is dropped into the report, never thrown; reordering the input command list changes nothing; enqueueing an event earlier than the one being processed throws in tests.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Turn`.
  - Files: `Core/World/Turn/{TurnEngine,EventQueue,ITurnBarrier,WaitForAllCommitted,TurnPhases,TurnReport,StateHasher}.cs`, tests. Scope: M.
  - Dependencies: W5.

- [x] **Task W7: The turn transaction + turn log + report re-derivation** *(done 2026-08-22 — 8 tests; Data 221/221. `WriteWorldGraphUnlocked` extracted so creation and every turn share one write path. Reports are stored for a 50-turn hot tail and **re-derived by replay** beyond it — proven by trimming to zero and asserting the re-derived entries are identical; re-derivation refuses across an engine/ruleset version change rather than fabricating a report.)*
  - Description: `POST /api/world/{id}/commit`; when the barrier fires, one transaction loads state, steps, writes mutated rows, appends `rpg_world_turn_log` (hash + versions, `report_json` only within the hot tail), bumps `current_turn` and the world revision. `GET /api/world/{id}/turn/{n}` serves a stored report or **re-derives** it by replaying the command log, refusing rather than fabricating when engine versions differ. SIM `run-turns` drives N turns from a scripted log.
  - Acceptance: exactly one turn-log row per turn; a replayed commit returns the stored result and mutates nothing; forced mid-turn failure leaves zero rows and does not advance `current_turn`; a re-derived old report is byte-identical to the one that was stored before trimming.
  - Verify: Data + E2E filtered tests, including a trim-then-re-derive equality test.
  - Files: `Data/Sqlite/RpgStore.WorldTurns.cs`, `Server/WorldTurnEndpoints.cs`, tests. Scope: M.
  - Dependencies: W6.

- [x] **Task W8: Determinism guards** *(done 2026-08-22 — 4 guard tests; Guard 44/44. Source scan over `Core/World/**` bans wall-clock reads, `System.Random`, and floating-point in stored state, plus a self-check that the guard would actually catch a violation.)*
  - Description: a guard test asserting no `DateTime.Now` / `UtcNow` / `Environment.TickCount` / `System.Random` symbol under `Core/World/`; hash-stability test; a replay test running the same scripted log twice from scratch.
  - Acceptance: all three green; a deliberately introduced wall-clock read makes the guard fail (verified once by hand, not committed).
  - Verify: `dotnet test tests\FusionRpg.Guard.Tests` + Core filtered tests.
  - Files: `tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs`, Core tests. Scope: S.
  - Dependencies: W7.

### Checkpoint 2 — the SSOT gate ✅ 2026-08-22
- [x] 20 turns of `stand-fast` produce a stable hash sequence; the same script + seed reproduces it exactly, a different seed diverges.
- [x] **Replay from `(seed, template, command log)` through the pure engine reproduces the store's hashes** — the sharpest assertion in the wave: if persistence ever perturbs state, store and engine diverge here.
- [x] One turn-log row per turn, none beyond; a report trimmed out of the hot tail re-derives to exactly what was stored.
- [x] Determinism guards green. Core **1540/1540** · Data **227/227** · E2E **146/146** · Guard **44/44**; all four guard scripts OK.
- [x] Defect caught by this checkpoint: `WorldCanonical` was hashing the world **id**, so two identical worlds hashed differently and no golden could ever be portable. The id is identity, not state — removed.
- [ ] **Owner review before Phase 3.**

## Phase 3 — the first verbs

- [x] **Task W9: Movement — lane cost, event-ordered marching, the `move` command** *(done 2026-08-22 — 22 tests; Core 1562/1562. Two real defects the tests caught: integer truncation made a crossing point differ by 1 depending on which legion computed it (now solved once in a canonical frame and mirrored, so the two answers are exact complements), and re-issuing an unchanged standing order after partial progress was refused as non-contiguous — a multi-turn march could never continue. **A ley lane's element is the climate of the sectors it joins** rather than a stored column: no schema change, and matching *either* end keeps cost symmetric, which is what lets two legions crossing in opposite directions agree on where they meet.)*
  - Description: `LaneCost` (integer per-mille, corridor and ley discounts, banner element computed not stored); movement seeding arrival events and solving lane crossings arithmetically; the `move` command wired into the movement phase.
  - Acceptance: cost goldens per lane type incl. both discounts; a three-turn march resumes exactly; movement never goes negative or exceeds budget; **property test** — for random speed/progress pairs the crossing time lies strictly between both start positions and is identical under either processing order.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/{LaneCost,MovementMath,MovementCommands}.cs`, `Turn/TurnPhases.cs` wiring, tests. Scope: M.
  - Dependencies: W8.

- [x] **Task W10: Zone of control, contact, `clear`, placeholder resolver** *(done 2026-08-22 — 19 tests. The Movement phase moved out of `TurnEngine` into `MovementPhase` so the engine stays a readable list of phases. **Deviations:** `clear` carries its **sector id** as well as the slot index (the spec's payload was entity+slot, which cannot express "you are not standing there" and so cannot catch a stale client); rout is a stored `Routed` flag on the entity rather than the spec's `idle · marching · routed` state, because idle/marching are derivable from position and this module's own boundary forbids storing derived state. **Open for the owner:** wave 1 has no fallback, so a routed force the winner is standing over is finished off next turn — pinned by a test so changing it is a decision, not a surprise.)*
  - Description: `ZoneOfControl` (entering a hostile-occupied sector halts; supply does not route through it); `ContactResolver` for the three contact cases in stable entity order; the `clear` command issuing a battle request against a slot's `guard_wave_id`; `IBattleResolver` + `PlaceholderBattleResolver` applying survivors, wounds, deaths, rout.
  - Acceptance: each contact row tested incl. the stationary-defender case and same-faction stacking; **marching through a guarded sector is free** (guards never halt movement); `clear` flips only the targeted slot; a legion already inside a hostile sector is not re-halted on every event; mutual destruction resolves; the placeholder is registered only by the world module.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/{ZoneOfControl,ContactResolver}.cs`, `Turn/{IBattleResolver,PlaceholderBattleResolver}.cs`, tests. Scope: M.
  - Dependencies: W9.

- [x] **Task W11: Claim** *(done 2026-08-22 — 11 tests. Claims settle in Snapshot, because everything they depend on — who is standing where, who is still alive, which guards are left — is only decided once the rest of the turn has run. **Deviation:** no separate initiative tie-break between rival claims, because the case cannot arise: two hostile forces in one sector each block the other's claim, and the battle they are already in is what decides it. Two friendly claims collapse to one flip plus a reported no-op.)*
  - Description: the `claim` command — a sector with no hostile entity and **every slot cleared** flips `owner_faction_id` and phase `held` when a legion holding a committed claim is present at Snapshot.
  - Acceptance: claiming what you already own is a reported no-op; claiming with any guard still intact is dropped with a reason naming the slot; two claims on one sector resolve by initiative with the loser blocked but keeping movement.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/MovementCommands.cs`, `Turn/TurnPhases.cs`, tests. Scope: S.
  - Dependencies: W10.

- [x] **Task W12: Supply traversal + attrition** *(done 2026-08-22 — 11 tests. **A faction with no Seat of its own has no supply network and takes no attrition** — the wild do not starve for want of a capital they never had; without that rule every neutral force on the map would quietly bleed to death. A held sector that contains a Seat is its own source, so in `first-light` only ash-waste can actually be cut off — which is what the cutting tests use.)*
  - Description: `SupplyGraph` — a stable-order breadth-first pass per faction from its homeworld through owned sectors over open lanes not under hostile zone of control, run in the Pressure phase; disconnected sectors reported; out-of-supply legions take attrition once per turn.
  - Acceptance: cutting one junction disconnects exactly the expected set; reconnecting restores it; attrition applies once per turn, never twice; nothing about supply is cached between turns.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/SupplyGraph.cs`, `Turn/TurnPhases.cs`, tests. Scope: M.
  - Dependencies: W8 (needs the phase pipeline, not movement).

### Checkpoint 3 — wave-1 acceptance ✅ 2026-08-22
- [x] `WorldWaveOneAcceptanceTests` — 20 scripted turns in which a legion marches, clears two guards, claims the sector, meets a warband head-on mid-lane, pushes to the frontier, claims that too, and then has Zomboss walk in behind it and cut the supply line. Golden final hash `79a7bee…`, one turn-log row per turn, and the **pure engine reproduces the store's hashes from `(seed, template, command log)` alone**.
- [x] A `never fired:` assertion names any wave-1 verb the scenario stopped exercising — march, clear, crossing, claim, zone of control, supply cut, attrition — so the scenario cannot quietly rot into twenty turns of standing still.
- [x] Every ZOC, contact, clear, claim, and supply case has a passing test (Core World **148**).
- [x] Core **1651/1651** · Data **234/234** · E2E **146/146** · Guard **44/44**; all four guard scripts OK.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).
- [ ] **Owner decision:** should a routed force retreat instead of being finished off where it stands?

## Phase 4 — FE (parallel from day one)

Renderer: **React Flow (`@xyflow/react`, MIT)** — owner decision. Sectors are custom React nodes, lanes custom edges, positions authored, dragging and connecting off. Keep the lawn's split: pure folds in `.ts` with `.test.ts` beside them, renderer as a thin host. This adds the program's first new web dependency, superseding the earlier "no new dependencies" assumption.

- [x] **Task W13: `#/world` map render (fixture-driven)** *(done 2026-08-22 — 22 web tests. The fixture is **generated from the live DTO** and an E2E test (`WorldFixtureTests`) fails if the two ever drift, with `FUSIONRPG_BLESS_WORLD_FIXTURE=1` to re-bless after a deliberate change. **Deviation:** slot art is a letter glyph, not `/api/icons/{side}/{typeId}.png` — that endpoint keys on the game's numeric type ids, and world slot types are kebab-case strings with no such id. The render-count test measures real renders by counting the card's `Handle` children, so it proves the card itself bails out rather than asserting `memo` in the abstract.)*
  - Description: add `@xyflow/react`; `worldViewModel.ts` folds a world-state payload into `{nodes, edges}`; `SectorNode.tsx` renders the sector card (ownership border, climate accent, phase/intel treatment, slot pips with guard state, type icons from `/api/icons/{side}/{typeId}.png`); `LaneEdge.tsx` renders lane type and width as stroke; `WorldPage.tsx` hosts the canvas plus a sector inspector. **Renders from a checked-in fixture** so this task has no server dependency; the live query is swapped in at the end of W14.
  - Acceptance: the fixture world renders recognisably with stable positions across refresh; node and edge components memoized or module-scope — a **render-count test proves panning does not re-render sector cards**; selection drives the inspector; data flows through the bus layer, never a direct fetch.
  - Verify: `cd web\fusion-rpg-web; npm run test` (Vitest: fold + render-count) and a manual look.
  - Files: `web/fusion-rpg-web/src/features/world/{worldViewModel.ts,WorldPage.tsx,SectorNode.tsx,LaneEdge.tsx}` + tests, `fixtures/first-light.json`, `package.json`. Scope: M.
  - Dependencies: **none** — starts immediately. Regenerate the fixture from the real DTO once W4 lands.

- [x] **Task W14: Live data, order queue, End Turn, report playback** *(done 2026-08-22 — 30 web tests + 5 E2E. **Found and closed a gap in W7:** its server surface — `POST /api/world/{id}/commit` and `GET /api/world/{id}/turn/{n}` — was never actually wired, only the store methods were; both now exist with E2E coverage of the barrier. The page falls back to the checked-in fixture when the player has no world, so it is still worth opening against an empty server. `LegionMarker` reads the lane's own `<path>` through `getPointAtLength` and writes `transform` via a ref inside `requestAnimationFrame` — a test pumps eight frames and asserts the React tree rendered **once**.)*
  - Description: `lib/bus/world.ts` queries replace the fixture; `worldSelection.ts` (selection + pending-order reducer, pure) and `turnPlayback.ts` (turn report → ordered keyframes, pure); select a legion, queue `move` / `clear` / `claim`, submit, press End Turn, then watch the report play back — legions animated along the lane path via `getPointAtLength`, driven by `requestAnimationFrame` writing transforms through refs.
  - Acceptance: an order round-trips and appears in the pending list; End Turn advances exactly one turn; playback renders in report order and matches the server's; the animation loop causes **zero** React re-renders of the graph.
  - Verify: Vitest for both folds and the no-re-render assertion; manual pass against a SIM server.
  - Files: `web/fusion-rpg-web/src/features/world/{worldSelection.ts,turnPlayback.ts,LegionMarker.tsx}`, `lib/bus/world.ts` + tests. Scope: M.
  - Dependencies: W13, W11.

### Test pass — 2026-08-22

Went hunting rather than adding coverage to what was already green. Two real defects, one invariant pinned:

- **Fixed (engine):** a force routed *again* while it was already recovering had the new rout cancelled by the old one — Snapshot inferred "has served its turn" from the turn-start state, so two routs cost one turn. A rout is now spent at the **top** of the turn it pays for, which makes the bookkeeping independent of any resolver's wound arithmetic. Unreachable with the wave-1 placeholder (it kills anything it routs twice), so `RoutLifecycleTests` proves it against a resolver of its own — which is what the seam is for.
- **Fixed (map view):** the page routed a mid-march legion from the sector it was heading toward, producing a path without the lane under its feet. The engine refuses those as `path.not-contiguous`, so the order looked perfectly fine in the queue and was silently dropped when the turn resolved. `routeForLegion` now keeps the current lane at the head; `MovementTurnTests` pins the engine side of the same contract from the other direction.
- **Pinned, no defect:** crossing positions are exact complements across 73 speed/progress combinations (`CrossingSymmetryTests`) — two forces that meet are in the same place, and neither is pushed backwards by the meeting.

Core **1727** · Data **234** · E2E **152** · Guard **44** · web **277**; `npm run build` clean.

### Checkpoint 4 — you can see it
- [x] Everything the checkpoint needs is built and green: Core **1651** · Data **234** · E2E **152** · Guard **44** · web **272**, `npm run build` clean, all four guard scripts OK.
- [ ] **Owner playtest** — open `#/world`, move a legion, clear a guard, end a turn, watch the playback. That is the one thing tests cannot sign off.


---

# Tasks: world map — wave 2 (fog + topology)

Plan: [world-map-plan.md](world-map-plan.md) · Specs: [world-intel](../docs/architecture/world/spec-world-intel.md) · [world-topology](../docs/architecture/world/spec-world-topology.md)
**Gate:** both specs are Draft — pending owner review. No task starts until they are approved.
Resolved during specification: glimpses report a strength **band** · `scout` costs **half movement** for **twice the sight** · intel age is **shown** · `hold` is the missing **recovery** verb · the lifeline overlay ships to the human too. Reasoning lives in the specs, not here.

## Phase 5 — the pure pieces (no engine changes, all parallel-safe)

- [x] **Task W15: Lane graph + all-pairs travel cost** *(done 2026-08-22 — 17 tests. The graph is the **supply lens, not the march lens**: an edge exists where a lane could hold an empire together, so deep rifts and temporal currents are absent even though an army can walk down both. `Unreachable` is a large sentinel rather than `int.MaxValue`, because `ReconnectionCost` sums it across thousands of pairs and an overflow would turn "the empire split" into a negative number.)*
  - Description: `LaneGraph` (edges = open lanes whose type carries supply and whose gate is not shut, cost `LaneCost.For(...)` with a null banner) and `AllPairsCost` (Floyd–Warshall), both taking a **sector filter** so the same code answers "the whole map", "one faction's holdings", and "holdings minus a hostile-held sector".
  - Acceptance: correct costs on four hand-built shapes (path, cycle, barbell, star); a disconnected filter reports islands rather than pretending they are joined; reversing sectors and lanes in the input changes nothing; severed and shut-gate lanes are not edges.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Topology`.
  - Files: `Core/World/Topology/{LaneGraph,AllPairsCost}.cs`, tests. Scope: S.
  - Dependencies: none — can start immediately.

- [x] **Task W16: Articulation points + reconnection cost** *(done 2026-08-22 — 18 tests. Tarjan is iterative rather than recursive: a generated map could be deep enough to blow the stack, and a stack overflow inside a turn is not diagnosable from a replay. **A spec claim turned out to be wrong** — `first-light`'s homeworld is not an articulation point, because ember-hollow and frost-mire reach each other round through ash-waste. Being important and being load-bearing are different things; the spec now says so.)*
  - Description: `ArticulationPoints` (Tarjan, `O(V+E)`) and `ReconnectionCost` — the summed delta in all-pairs cost when one sector is removed, with a fixed penalty for pairs that become unreachable so the result stays an integer and stays comparable.
  - Acceptance: articulation points match the textbook answer on all four shapes; a barbell's join scores far above everything else; a cycle has no articulation points and small positive reconnection costs; `ash-waste` ranks highest of `first-light`'s non-home sectors; reversing input order changes nothing; severing a lane changes the next answer (nothing cached).
  - Verify: Core filtered tests.
  - Files: `Core/World/Topology/{ArticulationPoints,ReconnectionCost}.cs`, tests. Scope: M.
  - Dependencies: W15.

- [x] **Task W17: Strength bands** *(done 2026-08-22 — 19 tests. Six tiers with a floor, ceiling and midpoint, validated for gaps and for midpoints outside their own band. A negative strength reads as `empty` rather than throwing — it should be unreachable, but a band table that throws on a number is a worse failure than one that says nothing is there.)*
  - Description: `StrengthBandCatalog` — six tiers, each with floor, ceiling and midpoint, validated at bootstrap like every other catalog. Bands are what a glimpse reports and what a memory keeps.
  - Acceptance: every strength maps to exactly one band; boundaries are exclusive at the top; `ceiling >= midpoint >= floor` for all six; the catalog self-validates (no gaps, no overlaps, ascending); band 0 is exactly zero.
  - Verify: Core filtered tests.
  - Files: `Core/World/Intel/StrengthBandCatalog.cs`, tests. Scope: S.
  - Dependencies: none — can start immediately.

- [x] **Task W18: Visibility** *(done 2026-08-22 — 12 tests. Visibility is the **union of turn start and turn end**, which removes two special cases at once: a legion that marches through a sector reports on it, and a faction driven out of one remembers it as of this turn. A force mid-lane glimpses both ends and sees its radius from each. Guard-kind entities watch nothing on their faction's behalf — the same rule that makes marching past one free.)*
  - Description: `Visibility.SeenBy(world, factionId)` → per sector, one of `None` / `Glimpse` / `Full`. Full where you own it or stand in it; glimpse within `SightLanes` over open lanes; `SightLanes = 1`, `2` in the `scout` stance. An entity on a lane sees both ends.
  - Acceptance: you see what you stand in and what you own; one lane out is a glimpse; two lanes out is nothing unless scouting; a severed lane blocks sight; an entity mid-lane sees both ends; a `Guard`-kind entity grants nothing (it defends a slot, not ground); results are in stable sector order.
  - Verify: Core filtered tests.
  - Files: `Core/World/Intel/Visibility.cs`, tests. Scope: M.
  - Dependencies: none — the `scout` *cost* is W23; this task only reads the stance.

### Checkpoint 5 — the pieces exist ✅ 2026-08-22
- [x] Core **1813/1813** · Guard **44/44**. Nothing outside `Core/World/{Topology,Intel}` touched; no engine behaviour changed at all.
- [x] Every algorithm proven against hand-built shapes — path, cycle, ring, barbell, star, two islands — with answers you can work out on paper. `first-light` is used only for sanity, because it is too small and too well connected to exercise anything interesting.
- [x] Ordering test present in every one of the four modules: reversing sectors, lanes or entities changes no answer.
- [x] Two test premises of mine were wrong and the code was right: a temporal current carries no supply so the one-way rule never fires, and a triangle costs *nothing* to lose a corner because the other two were already touching. Both are now pinned as facts rather than corrected away.

## Phase 6 — belief becomes state

- [x] **Task W19: Belief model + storage + template seeding** *(done 2026-08-22 — 21 tests. `WorldSector.Intel` became **`AuthoredIntel`**: it is template input seeding the player's opening belief, never live state, and naming it apart from `WorldState.Intel` avoids a permanent trap. A snapshot's slots and forces persist as JSON rather than sub-tables, because belief is always read whole for one sector and never queried by slot. **A force on the road is recorded against the ground it is walking toward** — a fog that hides an army until it arrives removes the only tension worth having.)*
  - Description: `IntelSnapshot` (owner, phase, climate, danger band, per-slot type and guard state, forces at the detail they were seen, `lastSeenTurn`) and `FactionIntel`; the `rpg_world_faction_intel` table keyed `(world_id, faction_id, sector_id)`; the template's authored `IntelState` becomes the **player faction's starting belief** at world creation.
  - Acceptance: create → load round-trips byte-identically through `WorldCanonical`; `first-light` starts with Dave believing what the template authored and the other factions believing nothing; a world created before this migrates rather than throwing; the four-state ladder is **derived** from `(lastSeenTurn, currentTurn, seenThisTurn)`, never stored.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World`; `.\scripts\guard-dal.ps1`.
  - Files: `Core/World/Intel/{IntelSnapshot,FactionIntel}.cs`, `Data/Sqlite/RpgStore.WorldIntel.cs`, `Core/World/WorldCanonical.cs`, tests. Scope: M.
  - Dependencies: W17, W18.

- [x] **Task W20: The `Intel` phase, and `RulesetVersion` 2** *(done 2026-08-22. **Two re-blesses, not the one the plan expected** — the first because claiming a sector no longer rewrites its authored intel, the second for the phase itself. Leaving a knowingly-wrong line in `ClaimResolver` to protect a hash would have been the worse trade; both reasons are recorded on the golden constant. Everything except the golden stayed green through the bump, including the store-versus-engine replay — which is the assertion that actually matters.)*
  - Description: a new `Intel` phase immediately before `Snapshot` — everything else has settled, so a faction sees the world as it ends the turn. Writes belief for every faction from `Visibility` + `StrengthBandCatalog`. Bumps `RulesetVersion` to 2.
  - Acceptance: standing in a sector makes belief equal truth for that sector, exactly; leaving keeps the snapshot with the turn stamped; re-entering refreshes it; a force destroyed in front of you is **forgotten**, not kept as a ghost; belief replays byte-identically; the wave-1 acceptance golden is re-blessed **once**, deliberately, with the reason in the commit message; a stored version-1 report refuses to re-derive rather than fabricating.
  - Verify: Core + Data filtered tests; the 20-turn acceptance scenario.
  - Files: `Core/World/Intel/IntelPhase.cs`, `Core/World/Turn/TurnEngine.cs`, `tests/.../WorldWaveOneAcceptanceTests.cs` (golden), tests. Scope: M.
  - Dependencies: W19.

- [x] **Task W21: `IWorldView` + `BelievedWorldView`** *(done 2026-08-22 — 10 tests. The interface is defined by what it **cannot** answer: a policy holding one has no way to ask what is really in a sector it has never visited, so an AI consulting the truth becomes a compile error rather than a discipline problem. Visibility is recomputed per view rather than stored on the snapshot — "can I see it now" changes when a legion moves, "when did I last see it" does not.)*
  - Description: the read interface every policy and projection will use, and the implementation that answers from belief rather than truth. `WholeWorldView` exists only as a test double, never registered in production.
  - Acceptance: a view for a faction never returns a sector it has never seen; a glimpsed sector returns a band, never an exact figure; a occupied sector returns the exact figure; the view is a pure read with no allocation of world state; the same `(world, belief, faction)` yields an identical view twice.
  - Verify: Core filtered tests.
  - Files: `Core/World/Intel/{IWorldView,BelievedWorldView}.cs`, tests. Scope: M.
  - Dependencies: W20.

### Test pass — 2026-08-22 (after Checkpoint 6)

Hunted rather than topped up coverage. Two real defects in the fog, both proven RED first:

- **Fixed: marching *through* a sector revealed nothing about it.** Visibility spans the turn's start and end, which covers where a force set off from and where it stopped — but not the ground in between, and a route cannot be read back out of a destination. `MarchOutcome` now carries the sectors actually set foot in, `MovementPhase` aggregates them per faction, and the Intel phase surveys them. **`first-light` cannot show this** — 560 + 900 outruns a turn's budget, so no legion ever crosses a sector on the stock map, which is exactly why it hid. `MarchedThroughTests` shortens the ley lane to force it. The golden did not move, for the same reason.
- **Fixed: `IWorldView` handed back true lane state.** A faction could tell a lane was severed on the far side of a map it had never visited — the quietest possible leak, and one that would have reached the wire in W22. A lane with neither end in sight now reads `Open` whether it is or not, so you route over it confidently and find out the hard way. Everything else about a lane stays public: where it goes, how long, how wide, what kind.

World tests **299** · Data **235** · Guard **44** · E2E **152**.

*(Unrelated: two `Overlay/OverlaySwitchStateTests` failures are another stream's in-flight work — its test file was written against a source file it had not finished editing. Nothing under `Overlay/` is mine.)*

### Checkpoint 6 — fog is real and it replays ✅ 2026-08-22
- [x] Belief survives a save, reloads byte-identically through `WorldCanonical`, and the 20-turn scenario replays byte-identically at `RulesetVersion 2` — store hashes still reproduced by the pure engine from the command log alone.
- [x] **Two** goldens re-blessed rather than the planned one, each with its reason recorded on the constant. See W20.
- [x] Core **1850/1850** · Data **235/235** · E2E **152/152** · Guard **44/44**; all four guard scripts green.
- [ ] **Owner review before Phase 7** — the belief model is cheap to change now and expensive later. Specifically worth a look: what a snapshot remembers, that a force in transit is recorded against its destination, and `FreshTurns = 5`.

## Phase 7 — fog reaches the player

- [x] **Task W22: Per-faction projection + the leak tests** *(done 2026-08-22 — 12 E2E tests. `entities` on the wire became **the viewer's own forces only**; everything else is believed, per sector, at whatever detail it was seen. The property test — no payload may name a sector its viewer has never seen — earned its place immediately by catching two things the per-field tests missed: a surveyed slot lost its `guardWaveId`, so nothing could tell "cleared" from "never guarded"; and **only the player got opening belief**, so a warband could not describe the ground under its own feet until a turn had been committed. Also fixed: the author's opening was being *downgraded* by a weaker present-day glimpse — the better of the two wins now, not the more recent.)*
  - Description: `GET /api/world/{worldId}/state?asFaction={id}` (defaulting to the player faction) returns **believed** state. Unknown sectors carry id and layout position only; remembered carry the snapshot plus `lastSeenTurn`; watched carry current truth at glimpse or full detail. Lanes are always returned — the graph is public.
  - Acceptance: **one test per leak** — never a sector never seen, never slot detail for a glimpse, never a force the viewer cannot see, never the seed; plus a property test that a viewer's payload names no sector id absent from that viewer's belief; an unknown `asFaction` is refused rather than defaulting to omniscience.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, tests. Scope: M.
  - Dependencies: W21.

- [x] **Task W23: The `stance` command, `scout`'s price, and what `hold` is for** *(done 2026-08-22 — 12 tests. `stance` existed in the movement spec's table and was never a command kind, so both new stances were dead letters. **The store's command payload had no `Stance` field**, which the 20-turn acceptance scenario caught by reporting `stance.unknown` — an order can round-trip through the database and come back malformed, and only an end-to-end scenario sees it. `hold` now costs all movement and buys entrenchment plus recovery, which closes the fact that **nothing in the game could heal**. `first-light` authors the wild pack as a garrison; wave 1 ignored that and handed it a full budget anyway, so three wave-1 tests changed — and one of them, the crossing property suite, would otherwise have gone silently vacuous behind an early return.)*
  - Description: `stance` as a command kind — it is in the movement spec's table and was **never implemented**, so a
    legion's posture is whatever the template authored and can never change; both new stances are dead letters without
    it. Takes effect at the *next* Snapshot refill, so committing to a posture costs the turn you commit. Then
    `scout` → `MovementMilli = 500` for `SightLanes = 2`, and `hold` → no movement, counts as stationary for the
    defender bonus, and recovers `RecoveryMilli = 150` of each member's health per turn **in supply**.
  - Acceptance: a scouting legion marches half as far and sees twice as far; a held legion cannot move and a `move`
    order for it is dropped with a reason; a held legion in supply recovers and one out of supply still starves;
    recovery never takes a member above full health; a stance change does not take effect the turn it is filed;
    `first-light` authors no scouts and no holds, so **the acceptance golden must not move** — if it does, something
    else changed and that is the finding.
  - Verify: Core filtered tests, and the acceptance scenario as a canary.
  - Files: `Core/World/Turn/{WorldCommand,WorldCommandAdmission,TurnEngine}.cs`,
    `Core/World/Movement/SupplyGraph.cs`, `Core/World/Turn/PlaceholderBattleResolver.cs`, tests. Scope: M.
  - Dependencies: W18.

- [x] **Task W24: Fog on the map** *(done 2026-08-22 — 10 web tests. Three treatments, an explicit "seen N turns ago" stamp, and forces shown by band name where the intel is banded. The **lifeline overlay** is a second module-scope `nodeTypes` map rather than a flag on node data: React Flow re-mounts every custom node when that object's identity changes, so both maps have to be constants and toggling swaps between two stable ones. `claimable` now requires a *survey* — a glimpse reports no slots, and "no slots left to clear" must never read as "clear".)*
  - Description: three sector treatments — unknown as a silhouette with no name, remembered dimmed with an explicit **"seen N turns ago"** stamp, watched as it draws today. Forces show a band name where the intel is banded and an exact figure where it is not. Plus the **lifeline overlay** from `world-topology` — a toggle shading your holdings by what their loss would cost. The checked-in fixture is regenerated for a viewer.
  - Acceptance: an unknown sector renders without a name and without slots; a remembered sector shows its age; a card never implies more certainty than the viewer has; a **lifeline overlay** toggle shades your own holdings by reconnection cost and marks articulation points, and shows nothing about anyone else's territory; the render-count test still passes (panning re-renders nothing); `WorldFixtureTests` still matches the live DTO.
  - Verify: `cd web\fusion-rpg-web; npm run test`; `npm run build`; E2E fixture test; a manual look.
  - Files: `web/.../features/world/{SectorNode.tsx,worldViewModel.ts,worldTypes.ts,fixtures/first-light.json}` + tests. Scope: M.
  - Dependencies: W22.

### Test pass — 2026-08-22 (after Checkpoint 7)

Two findings, one in the code and one in a test of mine:

- **Fixed: two DTO fields nothing populated.** `developmentLevel` and each slot's `state` were on the wire and always read zero/`Intact` — indistinguishable from a sector that genuinely had none, and a trap laid for whoever ships `sector-development`. A survey now records both; a glimpse still records neither, because you read development off the ground rather than from one sector away. Fourth golden re-bless of the wave, reason on the constant.
- **Fixed: a test of mine proved nothing.** `A_dug_in_defender_counts_as_stationary` passed `DefenderStationary = true`, so it would have passed whether or not the entrenchment code existed at all. It now passes `false` and carries a control — the same two forces with nobody dug in destroy each other — which is what makes the first assertion mean something.

Core **1902** · Data **235** · E2E **164** · Guard **47** · web **288**.

### Checkpoint 7 — you can see the fog ✅ 2026-08-22
- [x] Core **1897** · Data **235** · E2E **164** · Guard **47** · web **288**; `npm run build` clean; all four guard scripts green.
- [x] One test per leak, plus the property test that catches the ones nobody wrote a test for. It found two on its first run.
- [x] Three re-blesses across wave 2 rather than the planned one — every one a behaviour change with its reason recorded on the golden constant. W23 needed none: the stance order restored exactly what the wild pack used to do implicitly, and the hash did not move.
- [x] **The mechanical half of the playtest is automated** (`web/.../e2e/world.spec.ts`, 10 Playwright tests): unseen ground renders as a silhouette with no name or slots; ground you stand on renders in full; a remembered sector reads "seen N turns ago"; a counted force shows a number and a glimpsed one shows a band and never a number; the inspector fills in and reports intel age; the lifeline overlay is silent until asked, marks what is load-bearing, and turns off again; an order queues rather than sending and can be taken back. The `data-testid` hooks were already in the components — asking a human to eyeball what they were built for was the wrong call.
- [x] **Owner look, 2026-08-22 — confirmed working.** Deployed live (web build → publish → `Start-Process`), a pre-wave-2 world migrated by ending one turn, and the owner marched a legion out and back. Two findings, neither catchable by a test:
      **(1) the force picker was invisible** — a bare `<button>` styled `text-muted` inside a list, reading as a label, with March/Claim *hidden* rather than disabled when nothing was selected, so nothing said a step had been missed. Now a bordered, hoverable, `aria-pressed` row with a hint line. The Playwright tests drove it by role the whole time and passed: a test proves a control exists, never that a person can find it.
      **(2) `first-light` cannot stay foggy.** ash-waste is a hub touching four of six sectors and the homeworld is self-visible by ownership, so one march to the centre lights the whole map permanently — memory decays but never returns to `Unknown`. The dark map is a three-turn opening, not a condition. On this map the live tension is **staleness**, not ignorance: `ThreatMap`'s spread carries it, and `Unknown`/curiosity in `ValueMap` will almost never fire. A constraint for `world-generator` (wave 4), and a warning against tuning constants against a map that cannot exercise them.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).

*(Not ours: `e2e/audit.spec.ts` fails on a pre-existing locator ambiguity — `getByRole("link", { name: "Stats" })` matches both **Stats** and **PvzStats**, since Playwright's `name` is a substring match. Verified by removing the World nav link and re-running: identical failure. `exact: true` would fix it, but it is another stream's file. It does mean `npm run test:all` is red on `main`.)*
- [ ] **Then:** `spec-ai-commander.md` was rewritten against fog on 2026-08-22 and is coherent with what shipped; it now wants planning against the code rather than against intent.

---

# Tasks: world map — wave 2b (`ai-commander`)

Plan: [world-map-plan.md](world-map-plan.md) · Spec: [ai-commander](../docs/architecture/world/spec-ai-commander.md)
**Gate:** the spec is Draft — pending owner review. No task starts until it is approved.

Settled during the spec audit, so nobody re-litigates it here: the AI runs **outside `Step`** and files commands · the fill lives in **`RpgStore.CommitWorldTurn`**, not the endpoint · **one order per entity per turn** · the wild keep `stand-fast` · a policy that throws is **not caught**. Reasoning lives in the spec.

## Phase 8 — the turn can end (no intelligence anywhere)

- [x] **Task W25: `expectedTurn` — a commit means one specific turn** *(done 2026-08-22 — 9 tests. The check is refused in **both** directions and placed after `commander.unknown`, so a stranger cannot learn which turn is open. The world is now loaded **inside** `_gate` — the pre-lock read could be resolved out from under the call. Caught in passing: four demon-contract test files anchored `Day0` to a hard-coded 2026-08-21 while `Mint` stamps state from the real clock, so every "N days elapsed" assertion drifted by one per day. Not this stream's code; re-anchored to today because it fails a little worse every morning.)*
  - Description: `CommitWorldTurn(worldId, commanderId, int expectedTurn)` — required, no default — re-reading the world's turn **inside `_gate`** and refusing a mismatch as `turn.stale`. `CommitWorldTurnRequest` gains `Turn`; the endpoint 400s without it; `WorldPage` sends the turn it rendered. Ships and is verified **before any AI exists**, because it is the guard rail everything after it leans on.
  - Acceptance: committing the open turn behaves exactly as today; committing a resolved turn is refused `turn.stale` and changes nothing; a commit that lands while another is resolving refuses rather than filing into a closed turn; the endpoint rejects a body with no turn; the FE's End Turn round-trips.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~WorldTurn`; `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`; `npm run test` in `web/fusion-rpg-web`.
  - Files: `Data/Sqlite/RpgStore.WorldTurns.cs`, `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, `web/.../features/world/WorldPage.tsx`, tests. Scope: M.
  - Dependencies: none. **Do this one first even if the rest is deferred** — it is a latent bug fix that stands on its own.

- [x] **Task W26: The policy seam, the catalog, and a policy that decides nothing** *(done 2026-08-22 — 10 tests, and a guard rule that has been **seen to fail**. The first version of it never compiled — an escaping slip put a bare backslash in a char literal, so the whole Guard project was broken and the rule had never once run while I believed it was passing. It now catches a planted `WorldState` mention, ignores the doc comments that explain why the type is out of bounds, and its verdict is factored out so the proof is a permanent test rather than a plant-and-remember. Also pinned: **`FusionRpg.Core` targets net6.0 / C# 10** — nothing here may use C# 11 or 12 syntax.)*
  - Description: `IFactionPolicy` (`Decide(IWorldView view, ulong seed)` — the view already carries faction and turn, so neither is a parameter), `PolicyOrder(WorldCommand, string Reason)`, `FactionPolicies.Resolve`, `StandFastPolicy` (one entity-less `stand-fast` per faction, so the log distinguishes *chose nothing* from *was never asked*), and `WorldValidation` rejecting a faction whose `PolicyId` is not in the catalog. Plus the guard rule: nothing under `World/Ai/` may mention `WorldState`.
  - Acceptance: a world naming an unknown policy fails validation with the sector-style message; `stand-fast` emits exactly one order per faction with a reason; `Decide` is pure — same view and seed twice gives byte-identical output; the guard rule fails when a `WorldState` mention is planted and passes once removed.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Ai`; `dotnet test tests\FusionRpg.Guard.Tests`.
  - Files: `Core/World/Ai/{IFactionPolicy,FactionPolicies,StandFastPolicy}.cs`, `Core/World/WorldValidation.cs`, `tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs`, tests. Scope: M.
  - Dependencies: none (Core-only; can start alongside W25).

- [x] **Task W27: The fill inside the commit, and the claim that justifies the whole design** *(done 2026-08-22 — 11 tests. The turn now resolves when the human ends it, which nothing in the browser could ever make happen before — the barrier wanted three factions and only one of them was a person. The **replay-swap claim passes**: a stored log reproduces its hashes through the pure engine with no policy involved at all. Five test files changed premise as budgeted, and the fix that keeps the goldens byte-identical is that **an explicit commit speaks for a faction and suppresses the fill** — which is exactly the escape hatch a scripted scenario needs, so the two 20-turn scenarios still drive the wild and Zomboss by hand and still hash to their goldens.)*
  - Description: in `CommitWorldTurn`, after the caller's commit row and before the committers are read — for each faction with a non-null `PolicyId` not yet committed, in ordinal order: resolve, run against `new BelievedWorldView(world, factionId)`, insert commands **and reasons** on the commit's own connection and transaction, insert its commit row. `reason` is a nullable column via `EnsureColumn`, bounded to 200 chars, written only by the fill. Seed is `SeededRng.DeriveStream(worldSeed, $"ai:{factionId}:{turn}")`.
  - Acceptance: **committing as the player alone advances the turn**; ⭐ *replaying a stored command log with a deliberately different policy registered leaves every hash unchanged* — the architectural claim, and the most valuable test in the module; a policy that throws leaves no commit row, no commands, and the turn where it was; a faction with no legions still commits; one reason per AI command, none for a player command, truncated at 200; **no golden moves.**
  - Verify: `dotnet test tests\FusionRpg.Data.Tests`; `dotnet test tests\FusionRpg.Core.Tests`.
  - Files: `Data/Sqlite/RpgStore.WorldTurns.cs`, tests. Scope: M.
  - Dependencies: W25, W26.

- [x] **Task W28: Reasons on the wire** *(done 2026-08-22 — 2 E2E + 4 web tests. Correction 8 was half wrong: `ListWorldCommands` **was** already public, so only the reason needed a new reader. Commands are read from the log rather than the report, so a turn trimmed out of the hot tail still says what everyone was trying to do. The FE panel leaves out the player's own orders — you know why you gave them — and describes a command kind it has never heard of rather than dropping it, because silence would read as "the AI did nothing", which is the one thing the panel exists to disprove.)*
  - Description: a public per-turn command lister on the store (`ListWorldCommandsUnlocked` is private static with no counterpart), a `Commands` list on `WorldTurnReportDto` carrying commander, kind, subject and reason, and the turn-report panel showing them. The audit trail is a success criterion, so it is not folded into W27.
  - Acceptance: `GET /api/world/{id}/turn/{n}` returns each command with its reason in `(commander, seq)` order; a player command shows a null reason rather than an empty string; a turn whose report has been trimmed still lists its commands, because commands are never trimmed.
  - Verify: E2E World filtered tests; `npm run test`.
  - Files: `Data/Sqlite/RpgStore.WorldTurns.cs`, `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, `web/.../features/world/*`, tests. Scope: S.
  - Dependencies: W27.

### Checkpoint 8 — End Turn advances, and nothing has an opinion ✅ 2026-08-22
- [x] Core **2060** · Data **295** · Guard **53** · E2E **169** · web **292**; `npm run build` clean; all four guard scripts green.
- [x] End Turn advances the world from the browser with no manual commits; pressing it twice is refused `turn.stale` instead of burning a turn.
- [x] The policy-swap replay test passes: a stored log reproduces its hashes through the pure engine with no policy involved at all.
- [x] **No golden moved.** Both 20-turn scenarios still hash to their goldens — because an explicit commit speaks for a faction and suppresses the fill, which is the escape hatch a scripted scenario needs.
- [x] Two bugs found that were nobody's task: a **retried commit could burn a turn** the moment the AI made the barrier reachable, and four demon-contract test files were anchored to a hard-coded date that drifts by one day every morning.
- [x] Collision, not ours: `FusionRpg.Core` (net6.0 / **C# 10**) briefly stopped compiling when a concurrent stream wrote C# 12 primary constructors into `Effects/Atoms/`. They rewrote them. Worth remembering — **nothing in this module may use C# 11 or 12 syntax**.

## Phase 9 — the tables (pure over belief, nothing wired, all parallel-safe)

- [x] **Task W29: The march graph, hop distance, and the fixture `first-light` cannot provide** *(done 2026-08-22 — 16 tests. The two lenses became **one builder with a `LaneLens` parameter** rather than two graphs — my first attempt smuggled rifts past the supply filter by inventing a shadow lane-type catalog, which is drift dressed as design. Two of my own tests then contradicted each other and both were right: "a severed lane is not marchable" is a fact about the ground, while "an unseen lane reads open" is a fact about belief — so the first moved to the lens and the second stayed on the view. The ley-discount test pins correction 4: two identical legions price the same lane differently when one has scouted its endpoints.)*
  - Description: `MarchGraph` (every lane a legion can traverse, built from an `IWorldView`) and `Hops` (unweighted BFS from one sector). Plus the two seam overloads the audit found: `LaneGraph.Build(sectorIds, lanes)` and `LaneCost.For(climateLookup, lane, banner)`, with the truth-side callers passing lookups built from `WorldState`.
  - Acceptance: the march graph includes a `deep` rift and a `one-way` current that `LaneGraph` excludes — asserted on a **purpose-built two-lane fixture**, because all six of `first-light`'s lanes carry supply and the two lenses coincide there; hop counts match by hand on path, cycle and barbell; a one-way current is walked one way only; the ley discount is *absent* for a faction that has not scouted the lane's endpoints and present for one that has; `SupplyGraph` and `ReconnectionCost` return exactly what they returned before on every shipped scenario.
  - Verify: Core filtered `~Ai` and `~Topology` and `~Movement`.
  - Files: `Core/World/Ai/{MarchGraph,Hops}.cs`, `Core/World/Topology/LaneGraph.cs`, `Core/World/Movement/LaneCost.cs`, tests. Scope: M.
  - Dependencies: W26 (folder + guard exist).

- [x] **Task W30: Believed supply, and the traversal both halves share** *(done 2026-08-22 — 7 tests, and a spec claim disproved. The traversal moved to `SupplyReach` and `SupplyGraph` returns byte-identical answers. The headline test took four attempts and each failure taught something real: **a faction always has full sight of ground it owns**, so every lane inside its own supply chain has both ends visible and can never be the masked one — the "believes a cut lane is intact" divergence the spec promised **cannot happen for supply at all**, only for march planning. The divergence that does happen is ownership: ground taken from you out of sight stays yours in belief, and your chain runs straight through it. The doc comment now says so instead of the opposite. Also flushed out by the failures: `first-light` puts a ZOC-projecting wild pack on ash-waste and a Seat in nearly every sector, so any supply scenario built on it needs both stripped or it proves nothing.)*
  - Description: extract the BFS from `SupplyGraph.ConnectedSectors` into `Movement/SupplyReach.cs` (seeds, adjacency, `usable` predicate, stable id order); `SupplyGraph` becomes its truth-side caller and `Ai/BelievedSupply.cs` its belief-side one.
  - Acceptance: `SupplyGraph.ConnectedSectors` is unchanged on every shipped scenario — the extraction is provably behaviour-preserving; a faction whose chain is cut behind a lane it cannot see **still believes it is supplied**, and finds out by taking attrition; a faction that holds a Seat it has only glimpsed does not count it as a source, because a glimpse carries no slots.
  - Verify: Core filtered `~Movement` and `~Ai`.
  - Files: `Core/World/Movement/{SupplyReach,SupplyGraph}.cs`, `Core/World/Ai/BelievedSupply.cs`, tests. Scope: M.
  - Dependencies: W29 (march/supply lens distinction settled).

- [x] **Task W31: ThreatMap — fear, spread by ignorance** *(done 2026-08-22 — 13 tests, all 7 mutants caught. Two rules the spec did not name, both following from rules that already exist: **a guard's menace does not travel** (it projects no zone of control, so spreading its threat would make every guarded sector radiate menace it cannot deliver), and **every sector gets an answer including zero** (a missing key and a zero read the same to a person and differently to a caller). Verified in an isolated scratch copy while a concurrent stream had `FusionRpg.Core` uncompilable — their work is untracked, so deleting it in a copy of the tree gives a clean build without touching their files.)*
  - Description: threat per sector from every hostile force in belief, decayed by `StaleDecayPerTurn = 150`, spread over `min(age, MaxSpreadHops = 4)` hops of the **march** graph, falling off 400‰ per hop beyond that. Strength reads `RememberedForce.Defensive` or `.Offensive` — never a band directly.
  - Acceptance: a fresh sighting concentrates on one sector; a three-turn-old one is at full strength across three hops; a seven-turn-old one contributes nothing anywhere; the defensive reading is never below the offensive; **a hostile force across a deep rift raises threat** while contributing nothing to believed supply (the test that fails if anyone builds this on `LaneGraph`); reversing entity order changes nothing.
  - Verify: Core filtered `~Ai`.
  - Files: `Core/World/Ai/ThreatMap.cs`, tests. Scope: M.
  - Dependencies: W29.

- [x] **Task W32: ReachMap and the believed frontier** *(done 2026-08-22 — 18 tests, all 8 new mutants caught. `LaneGraph.Build` gained an optional `bannerElement` rather than a second builder — the ley discount is per legion, which is the whole reason reach is per entity. Two vacuous assertions of mine caught before they shipped: the ley test used `<=` (it would have passed if the discount never applied) and now picks a lane length of 1200 so the saving straddles a turn boundary and is visible as 1 turn against 2. `FrontierSet` returns three sets rather than one — held, contested, unknown — because under fog the edge of what you hold and the edge of what you know want opposite decisions, and merging them leaves every caller re-splitting the list.)*
  - Description: per-entity Dijkstra over the march graph, banner from `BannerElement.Of`, `turns = ceil(cost / MovementPolicy.BudgetFor(stance))`; `FrontierSet` over belief, which under fog includes **unknown neighbours** — the edge of what you hold and the edge of what you know are different sets.
  - Acceptance: reach matches hand-computed turns on the fixture; a `hold` stance reaches nothing and does not divide by zero; a scout's reach is half a marcher's; an unseen lane is treated as open, so reach is optimistic; the frontier of a faction that holds one sector includes every unknown neighbour of it.
  - Verify: Core filtered `~Ai`.
  - Files: `Core/World/Ai/{ReachMap,FrontierSet}.cs`, tests. Scope: M.
  - Dependencies: W29.

- [x] **Task W33: ValueMap — worth, relative to this empire** *(done 2026-08-22 — 21 tests. The overextension penalty is 1400‰ — deliberately more than the whole score, because it has to drive a total **below zero**: the classic 4X failure is blobbing outward until nothing is defensible, and the cure is for bad ground to score worse than nothing rather than merely least-best. `ReconnectionCost` needed the same belief-side overload `LaneGraph` got, which is correction 3 arriving exactly where the spec predicted. Risk with an empty threat map reads **all safe** rather than all-maximally-dangerous — an inverted axis with no data is the classic way to make an AI refuse to move.)*
  - Description: six per-mille axes (yield, strategic, defensibility, cost, risk, curiosity) weighted by the policy, minus an overextension penalty that can drive the total **below zero**; `SlotValueCatalog`, `INeedVector`, `UniformNeeds` as the stub until `sector-development` ships stockpiles.
  - Acceptance: overextension drives a sector below zero — the blobbing cure, and the one axis that must be able to go negative; curiosity makes an unknown sector attractive, loses to a good known target, beats a poor one, and reads zero when nothing is unknown; a Seat outranks a wildland; the strategic axis ranks a barbell join top; a glimpsed sector's yield is zero because a glimpse carries no slots, and that is asserted rather than tolerated.
  - Verify: Core filtered `~Ai`.
  - Files: `Core/World/Ai/{ValueMap,SlotValueCatalog,INeedVector,UniformNeeds}.cs`, tests. Scope: M.
  - Dependencies: W29, W30, W31, W32.

- [x] **Task W34: Response curves and considerations** *(done 2026-08-22 — 22 tests. Curves are integer-only and there is no logistic: it cannot be done without an approximation nobody would trust, and a curve 2‰ off on one machine is a replay that disagrees with itself. Smoothstep evaluates `3x²−2x³` in one division rather than three per-mille steps, which would round three times and drift off the curve it is named after. `Weakest` breaks ties ordinally so the sentence a turn report prints does not change between runs — the audit trail exists to tell a mistake from a bug, and one that varies is a source of both.)*
  - Description: the IAUS arithmetic, built now and called by nothing: six integer curves `0..1000 -> 0..1000`, the product-of-considerations score, and the compensation factor `1000 - 1000/n`. Wave 3 then inherits a tested scorer and only chooses *which* considerations to write. Momentum is specified and deliberately not implemented — it needs cross-turn memory, which would become hashed state.
  - Acceptance: every curve is monotone in the right direction and exact at both endpoints; the product of considerations is zero if any is zero; compensation raises a three-consideration score without ever exceeding 1000; no floating point anywhere (the guard scan already covers the folder).
  - Verify: Core filtered `~Ai`; `dotnet test tests\FusionRpg.Guard.Tests`.
  - Files: `Core/World/Ai/Utility/{ResponseCurves,Consideration}.cs`, tests. Scope: S.
  - Dependencies: none — independent of everything, can be built at any point.

- [x] **Task W39: The turn report's *entries* are still unprojected** *(done 2026-08-22 — 6 E2E tests. `TurnReportEntry` gained a nullable `SectorId` and every `report.Add` that happens *somewhere* now names where — structurally, not in prose, because matching a sector name out of a sentence works until somebody writes a different sentence. A line about nowhere in particular (a calendar tick, a command refused before it named ground) is shown to everyone: dropping those would leave a viewer unable to tell "nothing happened" from "you are not allowed to know". The comment on `/state` claiming to be the only place fog reaches the wire is corrected — it never was.)*
  - Description: W28's `commands` are filtered by belief (structured `SectorId`); `Entries` are not. They carry free text — `Subject` and `Detail` — so honest filtering needs a nullable `SectorId` on `TurnReportEntry` and a pass over every `report.Add` in the engine to fill it. Deliberately **not** done as a line of string-matching: filtering a sector name out of prose is the kind of fix that works until somebody writes a different sentence.
  - Acceptance: an entry naming a sector the viewer has never seen does not reach that viewer; `?asFaction=` shows it; the stored `report_json` shape stays backward-compatible (a nullable field reads as null on old rows); re-derivation still reproduces a trimmed report exactly.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`; the W22 property test extended to the turn endpoint.
  - Files: `Core/World/Turn/TurnReport.cs` + every `report.Add` call site, `Server/WorldEndpoints.cs`, tests. Scope: M.
  - Dependencies: none — independent of the AI, and the last hole in the fog.

### Checkpoint 9 — the tables exist and still nothing has an opinion ✅ 2026-08-22
- [x] Core **2337** · Data **353** · Guard **54** · E2E **175**; all four guard scripts green; **no golden moved**, no engine behaviour changed.
- [x] Every table proven against hand-built fixtures with answers workable on paper. `first-light` is for sanity only — on the two-lens question it is actively misleading, and on supply it silently neuters a test three different ways (a ZOC-projecting wild pack at ash-waste, a Seat in nearly every sector, and a hub that sees the whole map).
- [x] Ordering test in every module: reversing sectors, lanes or entities changes no answer.
- [x] Nothing under `World/Ai/` mentions `WorldState` — enforced by a guard that has been seen to fail.
- [x] **38 mutants, all caught** (`.\scripts\mutate.ps1 -Set world-ai`). Getting there cost six survivors and a retraction: an earlier *"all 22 caught"* was **false**, because a concurrent stream had `Core` uncompilable and `dotnet test` exits non-zero either way, so 22 build failures were counted as 22 tests noticing defects. The script now refuses to start on a red baseline, fails on a stale anchor, and normalises line endings when matching.
- [x] **A design claim died here.** `FrontierSet.Unknown` — "a neighbour you have never laid eyes on" — cannot be populated: an owned sector is an observation post, so everything adjacent to your territory is always at least glimpsed. Removed; an always-empty set is a lie in a type. That is the fourth claim undone by one fact — **holding ground grants full sight of it** — now written at the top of the capability map so it stops costing an afternoon each time.

## Phase 10 — the brain

- [x] **Task W35: `frontier-rules`, rules 1–3, and the one-order invariant** *(done 2026-08-22 — 26 rule tests across W35+W36. **Five of thirteen mutants survived on vacuous tests, all the same shape:** an ordered `?? ?? ??` chain hides its own bugs, because to test rule N you must build a world where rules 1..N-1 all decline — Take's guard was never exercised (Finish always answered first), Recover's supply check was never consulted (Take claimed the ground first). Two survivors were the code's fault: Defend had a guard redundant with the comparison below it, and Finish/Take can be swapped without changing anything, so the comment claiming otherwise was wrong.)*
  - Description: `FrontierRulesPolicy` walking `OwnForces` in ordinal order, first match wins, at most **one** order per entity, ids `ai-{turn}-{entityId}`. Defend (threat above the garrison already standing there — *not* above zero, which on six sectors fires permanently and deadlocks expansion), Finish (`clear`, lowest believed-guarded slot), Take (`claim`).
  - Acceptance: one scenario fires each rule and one does not; Defend does **not** fire on a Seat whose garrison already covers the threat; Finish picks the lowest guarded slot index; a `claim` filed on believed-stale ground is dropped by the engine with a reason, which is fog working rather than a bug; no entity ever receives two orders in one turn.
  - Verify: Core filtered `~Ai`.
  - Files: `Core/World/Ai/FrontierRulesPolicy.cs`, tests. Scope: M.
  - Dependencies: W33.

- [x] **Task W36: Rules 4–7, and the stance that costs a turn** *(done 2026-08-22 — Rules 4-7. **Explore was untestable on the four-sector map and nobody would have noticed:** a scouting legion sees *two* lanes, so everything close enough to reach was already glimpsed and everything unknown was past `ExploreTurns` — scouting revealed the very thing it was going to explore. Three mutants lived in that gap until a six-sector map on shorter lanes made the rule reachable at all.)*
  - Description: Recover (`stance hold` above `RecoverAtMilli = 400`, in believed supply), Explore (`stance scout` if not already scouting, else `move`, within `ExploreTurns = 3`, cheapest legion), Expand (best-value reachable unheld sector scoring above zero), Hold.
  - Acceptance: one scenario fires each and one does not; **a legion already scouting does not re-file the stance** — the oscillation bug, which would otherwise have every move dropped forever; Recover does not fire out of supply; Explore loses to a good known target and beats a poor one; Expand refuses a sector whose value is negative; the one-order invariant still holds across all seven rules over a 20-turn run.
  - Verify: Core filtered `~Ai`.
  - Files: `Core/World/Ai/FrontierRulesPolicy.cs`, tests. Scope: M.
  - Dependencies: W35.

- [x] **Task W37: Zomboss gets a brain — the wave's one golden re-bless** *(done 2026-08-22 — One golden re-blessed, one reason. **The prediction was checked rather than assumed, and the check found a real bug**: dumping the scenario's command log showed orders nobody wrote — the *first* commit of a turn was filling every AI faction that had not committed yet, so a scenario scripting two of them had its second one filled over. Orders already filed now speak for a faction as loudly as a commit does. After the fix the log is byte-identical across the flip, and the hash moves only because `PolicyId` sits in `WorldCanonical`'s faction row — because **`first-light` gives Zomboss no forces at all**, so a brain with nothing to command falls straight through to standing fast.)*
  - ✅ **Checked before building, and the risk does not materialise.** There is exactly one stored golden — `GoldenFinalHash` in `WorldWaveOneAcceptanceTests` — and that scenario commits for the wild and Zomboss **explicitly**, which suppresses the AI fill. So flipping `PolicyId` moves the hash for one reason only: the field is inside `WorldCanonical`'s faction row. Zomboss's behaviour in that scenario is unchanged, and the command log should come back byte-identical. **Assert that** rather than assuming it — if the log differs, the fill is running where it should not be, and the re-bless would bury the evidence.
  - ⚠️ **`first-light` will under-exercise the result** (owner playtest): ash-waste is a hub, so the map is fully lit within three turns and **Explore fires ~3 times then never again**, with curiosity reading zero thereafter. A passive-looking Zomboss in the playtest is most likely this, not a broken rule.
  - Description: `first-light` points Zomboss at `frontier-rules`. The wild stay `stand-fast`. Nothing else changes in this task, on purpose: `PolicyId` is inside `WorldCanonical`'s faction row, so this is the only place a hash moves and the reason goes on the golden constant.
  - Acceptance: exactly one re-bless, with the reason recorded; the wild's orders are byte-identical to before; every earlier task's assertion that goldens are unmoved still holds when re-run.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests`; `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`.
  - Files: `Core/World/WorldTemplateCatalog.cs`, golden constants, tests. Scope: S.
  - Dependencies: W36.

- [x] **Task W38: Acceptance — twenty turns against something that cannot see you** *(done 2026-08-22 — 8 tests, twenty turns of live decisions. The fog sweep passes over every order of every turn: nothing ever named ground its commander had not seen. Replay from the command log alone reproduces the stored hashes with no policy involved. The acceptance world **adds a Zomboss warband**, because the shipped map has none — pinned by its own test so a quiet opponent is never mistaken for a broken rule.)*
  - Description: the wave-1 acceptance scenario re-run with Zomboss live, plus the fog-honesty sweep over the whole command log.
  - Acceptance: one turn-log row per turn and one transaction per turn; the run replays **byte-identically** from `(seed, template, command log)`; every AI order carries a reason; ⭐ *every reason names only sectors that faction had already seen* — the success criterion the whole module exists for; Zomboss visibly acts on stale information at least once in twenty turns, captured as a named assertion rather than an anecdote.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`; full suite sweep.
  - Files: `tests/FusionRpg.E2E.Tests/World*.cs`. Scope: S.
  - Dependencies: W37.

### Checkpoint 10 — there is somebody on the other side ✅ 2026-08-22 (code); owner items open
- [x] Core **2388** · Data **362** · Guard **54** · E2E **175** · web **292**; all four guard scripts green.
- [x] Exactly one golden re-blessed, in W37, with its reason on the constant — and the claim that it moved for *one* reason was verified by dumping the command log both ways rather than asserted.
- [x] **50 mutants across the module, all caught.** Reaching that cost 13 survivors over three rounds and one retraction of a false "all caught" (a red baseline from a concurrent stream made 22 build failures look like 22 tests noticing defects).
- [ ] **Owner playtest** — the only thing tests cannot sign: play ten turns and say whether Zomboss is *legible*. Not whether he is good. Can you tell from the turn report why he did what he did, and does watching him act on a six-turn-old report read as a character or as a bug?
      ✅ **Fixed 2026-08-22 — the template now ships `e-zomboss-band-1` at black-gate.** He had a faction, a fortress and no army, so a brain gave him nothing to do; found by playing twenty turns, not by any test. Six tests across four files had quietly been using "zomboss" as their example of a faction that knows nothing, and were re-anchored on a faction id nobody plays.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).

### What this wave learned, in one line each
- **Holding ground grants full sight of it.** Four separate design claims died on this; it is now at the top of the capability map.
- **An ordered rule list hides its own bugs.** To test rule N, build a world where rules 1..N−1 decline — otherwise the assertion passes while the code never runs.
- **A scouting legion sees two lanes**, so on a small map scouting reveals what it set out to explore. Explore was untestable until the fixture grew.
- **Coverage says what the tests touched; mutation says what they would notice.** The second found five vacuous tests the first called 100%.
- **`first-light` cannot exercise this module**: no opponent army, a hub that lights the whole map in three turns, a Seat in nearly every sector, and a ZOC-projecting wild pack on the one interesting junction.

## Phase 11 — what playing it found (post-checkpoint, owner-directed 2026-08-22)

Neither of these came from a test. Both came from playing twenty turns and reading the report.

- [x] **Task W40: `Intel` moves after `Snapshot` — `RulesetVersion` 3** *(done 2026-08-22 — one `decisions.md` row, one spec update, no golden moved.)*
  - Description: at `RulesetVersion 2` the phase order was `… → Intel → Snapshot`, so a claim that settled in `Snapshot` was invisible to its own owner until the next turn. Zomboss re-filed the same claim on ground he already held and had it dropped — a stutter visible in the report as a commander who cannot remember what he just did. Order is now `Reveal → Movement → Sieges → Production → Growth → Pressure → Events → Snapshot → Intel`: belief records the world as it *ends* the turn.
  - Acceptance: T2 claims black-gate and T3 moves on; the locked-phase-order test names the new list; the row is in `decisions.md` because phase order is locked behaviour.
  - **The final acceptance golden did not move** — belief converges within a turn, so only intermediate hashes changed. Verified rather than assumed.
  - Files: `Core/World/Turn/TurnEngine.cs`, `docs/architecture/decisions.md`, `docs/architecture/world/spec-turn-engine.md`, tests.

- [x] **Task W41: `first-light` reshaped so fog can persist** *(done 2026-08-22 — 1 golden re-blessed with its reason, ~8 tests re-pathed, FE fixture regenerated.)*
  - Description: `l-ash-verdant` becomes `l-black-verdant`, hanging verdant-shelf off black-gate instead of the ash-waste hub. Ash-waste drops from four neighbours to three, the map gains a **second** articulation point, and the richest ground on the board now sits behind Zomboss.
  - Acceptance: over 14 turns Dave stays at **4 of 6 sectors known** (it used to be 6 of 6 by turn three) and Zomboss reaches 5; `ReconnectionCost` reports two load-bearing sectors instead of one; `Explore` and the curiosity axis stay live past turn three.
  - **Reverted mid-task, and worth keeping:** I also blamed "a Seat in nearly every sector" and started thinning them. `WorldValidation.Rule5SeatCounts` shows seat count is a consequence of sector *type* — `stable`/`rich`/`nexus`/`homeworld` are base-capable by definition. That is the type system working, not a map bug. Mixing base-capable and barren ground is a **`world-generator`** constraint; noted there, not patched here.
  - Files: `Core/World/WorldTemplateCatalog.cs`, golden constant, `web/…/world.fixture.json`, tests.

### Checkpoint 11 — the map can exercise the brain ✅ 2026-08-22
- [x] Core **2404** · Data **366** · Guard **54** · E2E **177** · web **292**; all four guard scripts green; three consecutive clean Core runs.
- [x] Fog is now a persistent condition rather than an opening move — the property the module was tuned against but the shipped map could not produce.
- [ ] **Owner decision — the oscillation.** On the reshaped map Zomboss alternates `defend black-gate` (threat 899) / `expand to verdant-shelf` (value 436) from T8 onward. That is the missing **momentum** term: a bonus to last turn's choice, specified in W34 and deliberately unbuilt because it needs cross-turn memory, which becomes hashed replayed state. Now backed by evidence rather than a hunch, so it is a real decision rather than a tuning nit.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).

## Phase 12 — sector development (wave 3)

Spec: [sector-development](../docs/architecture/world/spec-sector-development.md) · Program:
[world-map-program.md](../docs/architecture/world-map-program.md) · Constraint:
[empire-economy-ssot.md](../docs/architecture/empire-economy-ssot.md) A8.

**This is wave 3 of `world-map-program`, not of `world-stage`.** The two programs meet at exactly two
places, both already settled: `RulesetVersion` (world-stage's `world-commands` takes 5 → 6 first,
this phase takes 6 → 7), and the legion count (§8e.3 fixed it at **6–10, tunable**; this phase
authors the tuning row, `world-stage` consumes the number). Neither waits on the other beyond that.

**Numbering.** The file's existing tasks run **W1–W41** — not W90ish; Phase 11 closes at W41, and
W39 sits out of order inside Phase 9. Phase 12 therefore starts at **W42** and runs to **W60**.

**Three facts that size this phase**, all verified in the tree today:

- **`Growth` is a no-op.** `TurnEngine.cs:196-200` is `report.BeginPhase(Phases.Growth); return world;`
  in full. The phase exists, is in the locked order, and does nothing.
- **The player commands exactly one legion and cannot gain another.** Every `WorldEntityKind.Legion`
  in `src/` is `e-dave-legion-1`, one per template.
- **`DevelopmentLevel` is a cost with no producer.** It is stored (`WorldState.cs:135`), hashed
  (`WorldCanonical.cs:35`), projected (`WorldDtos.cs:74`), believed (`FactionIntel.cs:96`) **and
  charged for** (`LoamUpkeep.cs:44`) — and nothing in `src/` ever raises it; every assignment is a
  copy. `SectorPhase.Developed` (`WorldState.cs:12`) is referenced nowhere. Both are this module's
  to resolve.

**The build order below is identity-first, and that is deliberate.** Every hashed-behaviour change
lands with its tuning at the identity value, so it moves no golden; the single `RulesetVersion` bump
(W58) is what turns the numbers on. The arithmetic makes this exact rather than approximate:
`sum * intensity * handicap / 1_000_000` becomes `sum * intensity * handicap * season / 1_000_000_000`,
and at `seasonMilli = 1000` numerator and denominator both scale by 1000, so the integer quotient is
bit-identical. Two golden re-blesses total, both budgeted here rather than discovered per task.

- [ ] **Owner decision: the recruit rate's *shape*.** The 6–10-by-turn-40 target pins the endpoint, not the shape — a steady seat drip and a lair-heavy burst both land there and play completely differently. The method for settling the *numbers* already exists (the L9 harness pattern), so those are a scheduled measurement rather than a question. Which shape the game wants is a design call. Blocks W58's tuning values, not W43's mechanism.
- [ ] **Owner decision: what a season actually changes.** Three seams are available and all three are a one-line per-mille multiplier: yield (`LoamProduction.For`), upkeep (`LoamUpkeep.For`), movement (`MovementPolicy.BudgetFor`, `LaneCost.cs:38-43`). Endless Legend's winter changes movement *and* yields, which is the maximal reading; the minimal one is yield alone. **Movement is the riskiest of the three** — it interacts with zone of control and with the arithmetic lane-crossing solution, and a seasonal budget change makes a forecast the player already saw go stale. W47 and W48 build the upkeep seam because it is the one the spec prices; a movement seam is additional scope this decision would open.
- [ ] **Owner decision: whether a project is genuinely a second concept.** A project and a structure are both *"a cost, some turns, then a persistent effect"*, which argues for one catalog with a scope field. Against that: `RequiredSlotKind` and `YieldMultiplierMilli` are meaningless on a sector-wide project, and a catalog with columns null for half its rows is the shape that later grows a `switch`. Two catalogs is the safer default and the more verbose one. **Blocks W52**; W42–W51 are unaffected either way.

- [ ] **Task W42: The `growth` and `seasons` tuning blocks, published at identity**
  - Description: add `growth` (seat pulse per week, lair multiplier per-mille, special-week multiplier, raise cost, recruit soft cap if one is needed) and `seasons` (`count`, `monthsPerSeason`, and per-season yield / upkeep / movement multipliers, per-mille) to the world tuning file, plus `growth.legionTarget` (`min: 6`, `max: 10`, `byTurn: 40`) — **read by the harness, never by the engine**, because a legion count the engine enforced would be a hard progression ceiling. Seasons live beside `calendar` because a season *is* the calendar. Every multiplier ships at **1000‰ (identity)** and every pulse at **0**, so nothing downstream moves a hash until W58. The file is not hand-edited: `python tools/tuning/publish.py world <dotted.key>=<value>` writes `world.v2.json` and leaves `world.v1.json` on disk as the revert; both hosts pin the filename explicitly (`Program.cs:38`, `RpgHost.cs:66`), so a version bump is two host edits.
  - Acceptance: `world.v2.json` exists with both blocks and `_meta.owner` naming this spec; both host pins read `world.v2.json` and the server and injector still boot; a policy test reads every new key through a named accessor (no bare literal reaches a call site); `growth.legionTarget` is referenced by no file under `src/`.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.E2E.Tests`
  - Files: `data/tuning/world.v2.json`, `src/FusionRpg.Server/Program.cs`, `src/FusionRpg.Injector/Host/RpgHost.cs`, `src/FusionRpg.Core/World/Growth/RecruitPolicy.cs`, tests.
  - Dependencies: None.
  - Scope: S.

- [ ] **Task W43: `RecruitPolicy` — the seated weekly pulse, pure**
  - Description: **recruitment is seated, not lair-gated**, and the reason is arithmetic rather than taste: the obvious design (lairs release recruits weekly) cannot reach 6–10 legions by turn 40 on either shipped map. `first-light` has **one** lair slot and it is guarded (`WorldTemplateCatalog.cs:119`, `GuardState.Intact`); `two-hearths` also has exactly one. A single source behind a fight is not a rate. So every held sector with a Seat contributes a base pulse and a **cleared lair multiplies** its sector's pulse — reusing the exact shape `loam-structures` already ships (a rootbed seeps, a well multiplies it, `StructureDef.YieldMultiplierMilli`) rather than inventing a second economy idiom. Seats are dense enough to bite: 5 in `first-light`, 9 in `two-hearths`. Pure over `(turn, seed)`, no world mutation.
  - Acceptance: a pulse fires **on week boundaries and only on week boundaries** (`TurnCalendar.Roll(turn, seed).WeekBoundary`); a cleared lair multiplies its sector's pulse and an intact one does not; a special week scales the pulse and a plague month suppresses it — **and the plague beats the special week**, matching the rule `TurnCalendar.cs:52-54` already applies to growth; every number comes from W42's accessors.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Growth`
  - Files: `src/FusionRpg.Core/World/Growth/RecruitPolicy.cs`, `tests/FusionRpg.Core.Tests/World/RecruitPolicyTests.cs`.
  - Dependencies: W42.
  - Scope: M.

- [ ] **Task W44: The three hashed sector fields, and the one batched re-bless**
  - Description: add `WorldSector.RecruitStock` (`long`), `ProjectId` (`string?`) and `ProjectTurnsRemaining` (`int?`) to the model and to `WorldCanonical.Write`, which hashes sector rows field by field (`WorldCanonical.cs:34-37`) — so each one moves every world golden. **All three land together in one batched re-bless with `RulesetVersion` unchanged**, the L25 precedent recorded in `decisions.md`; discovering them one at a time is how a budget closed once gets reopened five times. Recruit stock is a **stock, not a rate** — a plain `long` count — for the same reason `WorldSector.LoamStock`'s own comment gives (`WorldState.cs:137-144`): per-mille means rate or fraction, and a stockpile is neither. It carries **no hard cap**; if accrual needs throttling it gets a configurable soft cap in tuning, declared in `ssot-power-scale.md` §11's register.
  - Acceptance: the three fields exist, hash, and default to zero/null on every existing template; exactly one re-bless, and its triage note proves the goldens moved for the field batch **and nothing else** (`RulesetVersion` is untouched by this task); `WorldValidation` rejects a negative `RecruitStock` and a `ProjectTurnsRemaining` without a `ProjectId`.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Guard.Tests`
  - Files: `src/FusionRpg.Core/World/WorldState.cs`, `WorldCanonical.cs`, `WorldValidation.cs`, the golden constant, tests.
  - Dependencies: W42.
  - Scope: M.

- [ ] **Task W45: Persist the three fields, and remember them under the existing fog rule**
  - Description: store and read back the three new fields, and give `RememberedSector` the same fields under the rule already in force — full detail at `SectorSight.Full` only (`IntelRecorder.cs:100`, `IntelSeed.cs:81`), and **never fogged for your own ground** (`world-map-program.md:46`). No new tables; the existing sector row grows three columns via the `EnsureColumn` pattern the loam program already used.
  - Acceptance: a world with a stocked, mid-project sector round-trips through create → save → load byte-identically; an existing database opens without migration error and reads the new columns as zero/null; a faction that glimpsed a sector remembers no recruit stock, and a faction that owns it remembers all three.
  - Verify: `dotnet test tests\FusionRpg.Data.Tests` · `.\scripts\guard-dal.ps1`
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.World.cs`, `src/FusionRpg.Core/World/Intel/FactionIntel.cs`, `IntelRecorder.cs`, tests.
  - Dependencies: W44.
  - Scope: M.

- [ ] **Task W46: Project the new state owner-only**
  - Description: recruit stock and project progress follow `WorldSectorDto`'s existing owner-only convention for economy numbers (`WorldDtos.cs:88-102`). A sector's `DevelopmentLevel` already is **not** owner-only and stays as it is — this task does not change what is already on the wire, only what it adds.
  - Acceptance: a viewer who does not own a sector receives no recruit stock and no project progress for it, asserted by the existing fog property test (no payload names a fact its viewer may not know); the owner receives all three; the world FE fixture is regenerated and the drift test (`WorldFixtureTests`) is green.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`
  - Files: `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`, `web/fusion-rpg-web/src/features/world/fixtures/first-light.json`, tests.
  - Dependencies: W45.
  - Scope: S.

- [ ] **Task W47: `CalendarRoll.Season` — derived, drawing nothing**
  - Description: **the calendar clock is already built and deterministic** (`TurnCalendar.Roll(turn, seed)`, `TurnCalendar.cs:31-58`, per-boundary derived streams at `:41` and `:48`, every rate tunable at `:27-29`). This task adds only the season, and it adds **no RNG and no state**: `season(turn) = (turn / (DaysPerWeek * WeeksPerMonth * MonthsPerSeason)) % SeasonCount`, a pure function of the turn. A season is **never fogged** — nobody is uncertain about what month it is — so belief computes it from the turn the same way `LoamUpkeep.cs:33-39` already argues for its terrain-or-self-knowledge terms.
  - Acceptance: `season(turn)` is a table test across a full cycle plus the boundaries either side; the `calendar:week:<turn>` and `calendar:month:<turn>` streams produce **byte-identical sequences before and after** the season member exists — that is the assertion that proves it draws nothing; the same `(turn, seed)` gives the same roll with and without the member.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Calendar` · `dotnet test tests\FusionRpg.Guard.Tests`
  - Files: `src/FusionRpg.Core/World/Turn/TurnCalendar.cs`, `tests/FusionRpg.Core.Tests/World/TurnCalendarSeasonTests.cs`.
  - Dependencies: W42.
  - Scope: S.

- [ ] **Task W48: The season factor inside `LoamUpkeep`, and its three truth-side callers**
  - Description: `LoamUpkeep.For` has **no calendar term today** (`LoamUpkeep.cs:40-47`). Add one to the five-argument overload and thread it through the `(world, sector)` overload and its truth-side callers — `LoamBalance.cs:13` and `LoamPhases.cs:124`. **The product gains a fourth per-mille factor and crosses `1_000_000_000`**, so: `sum` is already `long` and the chain promotes, **widen before multiplying**, the divide happens **exactly once and last**, and the whole expression sits in a `checked` block — an overflow here must **throw**, never wrap into negative upkeep, which is the defect `WorldState.cs:137-144` records having already happened once with `int`. Season and plague compose **multiplicatively on the pre-clamp input**, matching `LoamPolicy.SurgeDecayMultiplierMilli`'s own audit-resolved rule (`LoamPolicy.cs:143-148`). Ships at `seasonMilli = 1000`, so no golden moves.
  - Acceptance: at identity the upkeep for every sector of both templates is **bit-identical** to today's, proven by running the existing goldens unchanged; division happens once; a four-factor product at the top of its legal range does not overflow and a forced overflow **throws** rather than wrapping; no `float` or `double` appears anywhere on the path.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `python scripts\audit-overflow.py`
  - Files: `src/FusionRpg.Core/World/Loam/LoamUpkeep.cs`, `LoamBalance.cs`, `LoamPhases.cs`, `tests/FusionRpg.Core.Tests/World/LoamUpkeepSeasonTests.cs`.
  - Dependencies: W47.
  - Scope: M.

- [ ] **Task W49: The same term on the belief and forecast sides — all call sites move together**
  - Description: **this is the task the spec singles out as the one that fails silently.** `LoamUpkeep.For`'s other call sites are the AI's belief path (`FrontierRulesPolicy.cs:189`), the player-facing forecast (`LoamForecast.cs:60`) and the server's own forecast projection (`WorldEndpoints.cs:438` — a **fifth** site the spec's "four callers" count does not include; verified by grep). Omit the season from any of them and the AI plans against an upkeep it does not pay, or the forecast disagrees with the act — the precise failure §8c.6 lists as load-bearing about `Weakest`. Adding it to the truth side only is worse than not adding it at all, because it looks correct.
  - Acceptance: **one test walks every call site of `LoamUpkeep.For` and asserts a single answer** for the same sector in the same season — `LoamPhases`, `LoamBalance`, `LoamForecast`, `FrontierRulesPolicy` and `WorldEndpoints`; a grep-shaped assertion fails if a new call site appears that does not pass a season; the forecast the player sees and the upkeep the turn charges agree at a non-identity season multiplier.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`
  - Files: `src/FusionRpg.Core/World/Ai/FrontierRulesPolicy.cs`, `src/FusionRpg.Core/World/Loam/LoamForecast.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`, tests.
  - Dependencies: W48.
  - Scope: M.

- [ ] **Task W50: Fill the `Growth` phase — `GrowthPhases`, wired and accruing**
  - Description: replace `TurnEngine.cs:196-200`'s no-op with a call into a new `GrowthPhases.Growth`, which accrues `RecruitPolicy`'s pulse into each held sector's `RecruitStock` on week boundaries. `GrowthPhases` is its own file for the reason `LoamPhases.cs:8-10` gives for being one: `TurnEngine.cs` is already the busiest file in the module. **This task does not move the phase**; it fills it. Ships with W42's pulse at 0, so it accrues nothing and no golden moves.
  - Acceptance: with the pulse at 0 every existing golden is byte-identical and the locked-phase-order test is untouched; with a non-zero pulse in a test-local tuning, stock accrues on week boundaries only, in stable sector-id order, with no dictionary enumeration; the report gains one entry per accruing sector, naming the sector structurally (`TurnReportEntry.SectorId`, W39's field) rather than in prose.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Guard.Tests`
  - Files: `src/FusionRpg.Core/World/Growth/GrowthPhases.cs`, `src/FusionRpg.Core/World/Turn/TurnEngine.cs`, tests.
  - Dependencies: W43, W44.
  - Scope: M.

- [ ] **Task W51: The `raise` command, resolving in `Snapshot`**
  - Description: a pulse never spawns a legion by itself. `raise` spends a sector's recruit stock and founds a legion at that sector's Seat. It resolves in `Snapshot`, immediately after `BuildResolver` (`TurnEngine.cs:280`) and **for the identical reason that resolver states** (`BuildResolver.cs:14-17`): ownership is only decided once the rest of the turn has run, so the order re-validates at resolution rather than trusting Reveal-time admission. The new entity follows `SpawnTheUnmade` exactly (`LoamPhases.cs:246-257`) — a pure constructor, no RNG, and an id **derived from its cause**: `e-{factionId}-legion-{turn}-{sectorId}`, unique by construction because a raise consumes the sector's stock so at most one can succeed per sector per turn. A monotonic counter would be hidden state a replay has to reproduce. Which species a sector recruits is **the sector's climate**; no new selection mechanism. **A new command kind must be plumbed through all five sites in this same change** or it is silently lost — the defect that made `stance` a dead letter and that `world-stage`'s `world-commands` repaired for `sustain` and `build`.
  - Acceptance: `raise` is rejected with its own reason for each illegal case — not yours at Snapshot, no Seat slot, a hostile entity standing in it, `RecruitStock < RaiseCostPoints`; a raised legion's id is derived and stable across replay; the round-trip property test over **every kind in `WorldCommandKinds.All`** covers `raise` with its payload intact; **no hard cap on legion count exists anywhere in `src/`**.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `src/FusionRpg.Core/World/WorldCommand.cs`, `src/FusionRpg.Core/World/Growth/RaiseResolver.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs`, `src/FusionRpg.Contracts/WorldDtos.cs`, `src/FusionRpg.Server/WorldEndpoints.cs`.
  - Dependencies: W50, and `world-stage` Phase 0's `world-commands` (which owns the `Amount` / `StructureId` plumbing fix and the every-kind round-trip property test this task rides on).
  - Scope: L.

- [ ] **Task W52: `ProjectCatalog`, the `develop` command, and projects advancing in `Growth`**
  - Description: **blocked on the third owner decision above.** Slot buildings raise the slot; sector **projects** raise the sector — development level, defense, capacity — and a project is *"this sector is doing this for the next three turns"*, costing turns and materials, never a hidden industry stat. `ProjectCatalog` mirrors `StructureCatalog`'s shape exactly (dictionary-backed, eager `Validate()`, `IsKnown`/`Get` — `StructureCatalog.cs:48-140` is the template), and `develop` resolves in `Snapshot` beside `raise` and `build` for the same ownership-race reason. **Projects advance in `Growth`; structures keep advancing in `Production`** (`LoamPhases.DecrementConstruction`) — the split is deliberate, because reusing that loop would make one phase serve two modules and put a second module's fingerprints on a shipped ruleset.
  - Acceptance: the catalog validates at static init and rejects unknown ids at the write gate; `develop` is plumbed through all five command sites and covered by the every-kind round-trip test; **`Growth` runs after `Production`, so a project completing this turn affects next turn's yield and never this turn's** — asserted as its own test, because it is the stated consequence of the split; no `switch (id)` over project ids anywhere.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `src/FusionRpg.Core/World/Growth/ProjectCatalog.cs`, `DevelopResolver.cs`, `GrowthPhases.cs`, `src/FusionRpg.Core/World/WorldCommand.cs`, `src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs`.
  - Dependencies: W51, and the projects-as-a-second-concept decision.
  - Scope: L.

- [ ] **Task W53: Give `DevelopmentLevel` a producer**
  - Description: close the trap `empire-economy-ssot.md` A8 named — development priced as pure cost quietly kills the builder layer. Today `DevelopmentLevel` is stored, hashed, projected, believed and charged for, and **every assignment in `src/` is a copy**; a completed project is what raises it. This task is the one line that makes the number mean something, plus the belief and report consequences of a level actually changing.
  - Acceptance: a completed development project raises the sector's `DevelopmentLevel` by its authored amount, once, in `Growth`; the raise appears in the turn report naming its sector; a faction watching that sector at `SectorSight.Full` sees the new level and one that glimpsed it does not; no code path lowers the level (there is no de-development, and a silent decrement would be a hidden cap).
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Growth`
  - Files: `src/FusionRpg.Core/World/Growth/GrowthPhases.cs`, `src/FusionRpg.Core/World/Intel/IntelRecorder.cs`, tests.
  - Dependencies: W52.
  - Scope: S.

- [ ] **Task W54: Delete `SectorPhase.Developed`**
  - Description: declared at `WorldState.cs:12` and referenced **nowhere** in `src/`. This module deliberately does not make it real: development level is the number, and a phase mirroring it is derived state that rots, which `spec-world-movement.md` already forbids. Removing it is verifiably safe here, and the proof is the same property `SlotTypeCatalog.cs:25-28` relies on for `SlotKind`: `SectorPhase` is persisted and read back **by name, never by ordinal** (`RpgStore.World.cs:230` writes `s.Phase.ToString()`, `:429` reads `Enum.Parse<SectorPhase>`), and `WorldCanonical.Row` hashes the same string form (`WorldCanonical.cs:95-104`). A separate change from W53 on purpose — a deletion and a feature in one diff is a deletion nobody reviews.
  - Acceptance: the enum member is gone and the solution builds; every world golden is **byte-identical** (no ordinal was ever hashed); an existing saved world with no `Developed` row loads unchanged; a grep for `Developed` over `src/` and `tests/` returns nothing.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Data.Tests`
  - Files: `src/FusionRpg.Core/World/WorldState.cs`, tests.
  - Dependencies: W53.
  - Scope: XS.

- [ ] **Task W55: The A8 invariant — development must pay**
  - Description: `empire-economy-ssot.md:318` is binding: **development must raise yield faster than it raises upkeep, or nobody will ever develop.** Today only the upkeep half exists (`LoamPolicy.cs:52-53`). Add `DevelopmentYield` reading a new `development` block **in the same file** as `upkeep.developmentUpkeepPerLevel` — `data/tuning/loam.v{n}.json` — because the invariant is a comparison between two numbers and splitting them across files makes it unverifiable by reading. The file's `_meta.owner` gains this spec as a second owner. Ships at level 0 for every shipped sector, so it moves nothing until W53's producer runs.
  - Acceptance: **the invariant is asserted across the whole authored level range, not at one sample point** — for every level, marginal yield exceeds marginal upkeep; the test fails if either tuning row is edited to break it, and its message names A8; every magnitude on the path is `long` and divides by 1000 exactly once, last.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `python scripts\audit-magic-numbers.py --summary`
  - Files: `data/tuning/loam.v2.json`, `src/FusionRpg.Core/World/Growth/DevelopmentYield.cs`, `src/FusionRpg.Core/World/Loam/LoamProduction.cs`, `tests/FusionRpg.Core.Tests/World/DevelopmentYieldTests.cs`.
  - Dependencies: W53.
  - Scope: M.

- [ ] **Task W56: The yield structure kinds the reward layer needs**
  - Description: the structure **mechanism** is done and wired — `StructureCatalog`, `WorldSlot.StructureId` + `ConstructionTurnsRemaining`, `BuildResolver.Run` called at `TurnEngine.cs:280`, and `Rule14StructureSlotKindMatches` validating it. What is missing is rows and kinds. Add a new `StructureKind` for yield structures (a soul conduit, extractors, a hatchery on a lair) as catalog rows, plus the **flat structure-only yield field** `spec-structure-substrate.md` explicitly deferred to *"a new field added when there is a real row to test it against"* — this is that row. **Note the audit correction:** `StructureCatalog.cs` defers to `loam-structures`, not to this module; the deferral to `sector-development` is the substrate spec's. This module's structures produce **loam and recruits only** — the reward layer (souls, essence, materials) is unassigned and stays out (see the spec's fourth open question, which is not this phase's to answer).
  - Acceptance: every new row validates against `Rule14StructureSlotKindMatches` and its `RequiredSlotKind`; the flat yield field is read by `LoamProduction` and defaults to 0 so no existing structure changes; a hatchery on a lair multiplies that sector's recruit pulse through W43's policy rather than through a second code path; content is data — no `switch` over structure ids.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Structure`
  - Files: `src/FusionRpg.Core/World/Loam/StructureCatalog.cs`, `src/FusionRpg.Core/World/Loam/LoamProduction.cs`, `data/tuning/loam.v2.json`, tests.
  - Dependencies: W55.
  - Scope: M.

- [ ] **Task W57: Rename the `*CostMilli` tuning keys that are not per-mille (ask-first)**
  - Description: every `*CostMilli` in `data/tuning/loam.v1.json`'s `structures` block — `wellCostMilli` 200, `waystationCostMilli` 300, `granaryCostMilli` 150 — is a **whole loam unit, not a per-mille**: `StructureDef.CostMilli` is compared directly against `WorldEntity.CarriedLoam` at `BuildResolver.cs:101`, and that is a plain count. The maths is right and the name lies, which is exactly the kind of name that later grows a spurious `/ 1000`. This is a `publish.py` migration plus a loader edit, not a redesign. **The spec files this under ask-first**, so it does not land without the owner saying go.
  - Acceptance: the keys read `*Cost` and the field reads `Cost`; every structure cost comparison is unchanged in value, proven by the existing build goldens staying byte-identical; the old key names appear nowhere in `src/`, `data/tuning/` or the docs that cite them.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `python scripts\audit-magic-numbers.py --summary`
  - Files: `data/tuning/loam.v2.json`, `src/FusionRpg.Core/World/Loam/StructureCatalog.cs`, `src/FusionRpg.Core/World/Loam/LoamPolicy.cs`, `docs/architecture/loam/spec-structure-substrate.md`.
  - Dependencies: W56, and owner sign-off.
  - Scope: S.

- [ ] **Task W58: Turn the numbers on — `RulesetVersion` advances exactly once**
  - Description: the single behaviour bump, and the second and last re-bless. Everything above landed at identity; this task sets the real growth pulse (from the recruit-rate shape decision), the real season multipliers (from the what-a-season-changes decision) and takes the version bump with them. **Read the current value; do not hard-code.** `world-stage`'s `world-commands` takes 5 → 6 first, so the value in `TurnEngine.cs:42` at the moment this task runs is what gets incremented — it is **5 today** and must be re-read, not assumed. The row goes in `decisions.md` because hashed behaviour is locked behaviour, following `Intel`'s move (2→3), `loam-turn` waking two phases (3→4) and `LegionSupply` replacing attrition (4→5).
  - Acceptance: `RulesetVersion` is exactly one greater than whatever it read, and a test asserts the stored-versus-engine replay refuses across the bump rather than fabricating a report; the re-bless is **triaged in advance** with a predicted-delta writeup naming which goldens move and why, and the prediction is checked against the actual diff rather than assumed; `decisions.md` carries the row; this is the **second and final** re-bless of the phase.
  - Verify: `dotnet test tests\FusionRpg.Core.Tests` · `dotnet test tests\FusionRpg.Data.Tests` · `dotnet test tests\FusionRpg.E2E.Tests`
  - Files: `src/FusionRpg.Core/World/Turn/TurnEngine.cs`, `data/tuning/world.v3.json`, `docs/architecture/decisions.md`, the golden constants.
  - Dependencies: W50, W49, W55, and the first two owner decisions.
  - Scope: M.

- [ ] **Task W59: Forty turns, and a legion count that is measured rather than enforced**
  - Description: the acceptance run, living with the other world checkpoints. A forty-turn `first-light` run ends with the player commanding several legions **they chose to raise** rather than one the template handed them. The assertion is a **calibration assertion over tuning, not an engine limit**: if the count lands outside `growth.legionTarget`, the tuning moves — not the test's meaning. That distinction is the whole reason the target lives in `data/tuning/` and is read by the harness only.
  - Acceptance: the run reports the player's legion count and asserts it lands inside `growth.legionTarget` (6–10 by turn 40); the run **replays byte-identically** from its command log with no policy involved; reversing input entity order changes nothing; the season is visible in the turn report and a season boundary is observable inside the forty turns.
  - Verify: `dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World`
  - Files: `tests/FusionRpg.E2E.Tests/WorldSectorDevelopmentAcceptanceTests.cs`, `data/tuning/world.v3.json`.
  - Dependencies: W58.
  - Scope: M.

- [ ] **Task W60: Determinism sweep and the phase's guard close**
  - Description: confirm the guards caught everything the phase added. `WorldDeterminismGuardTests` scans `Core/World/**` for banned symbols and **picks up new files automatically** (`tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs:16-47`, plus the no-floats-in-world-state rule at `:51`), so `Growth/` is already in scope — this task proves it rather than assumes it, and closes the overflow and magic-number audits over the new arithmetic.
  - Acceptance: no wall clock, no `System.Random` and no floating point anywhere under `Core/World/Growth/`; any RNG that exists draws from a derived stream named `growth:recruit:<turn>`, one stream per concern, following `TurnCalendar.cs:41`'s convention so an extra draw in one never shifts another; `python scripts\audit-overflow.py` reports zero critical; `python scripts\audit-magic-numbers.py` adds no new entries for this phase's Policy/Catalog files.
  - Verify: `dotnet test tests\FusionRpg.Guard.Tests` · `.\scripts\guard-dal.ps1` · `python scripts\audit-overflow.py` · `python scripts\audit-magic-numbers.py --summary`
  - Files: `tests/FusionRpg.Guard.Tests/WorldDeterminismGuardTests.cs`, `src/FusionRpg.Core/World/Growth/*.cs`.
  - Dependencies: W59.
  - Scope: S.

### Checkpoint 12 — an army that grows, a year that changes, ground worth improving

- [ ] A forty-turn `first-light` run ends with the player commanding a legion count inside `growth.legionTarget`, raised by their own orders.
- [ ] The season is derived, hashed, replayed and visible in the turn report, and the HUD's season slot has a real field behind it.
- [ ] The A8 invariant passes across the **full authored level range** — development raises yield faster than upkeep, or nobody develops.
- [ ] Every kind in `WorldCommandKinds.All` survives the store round trip with every payload field intact — `sustain`, `build`, `raise` and `develop` included.
- [ ] **Exactly two golden re-blesses**, both triaged in advance, and `RulesetVersion` advanced **exactly once**.
- [ ] `DevelopmentLevel` has a producer; `SectorPhase.Developed` is gone.
- [ ] All suites and the four boundary guards green: `dotnet test tests\FusionRpg.Core.Tests`, `...\FusionRpg.Data.Tests`, `...\FusionRpg.Guard.Tests`, `...\FusionRpg.E2E.Tests`; `cd web\fusion-rpg-web; npm test`.
- [ ] **Owner playtest** — the only thing tests cannot sign: play forty turns and say whether raising a legion feels like a decision or a formality, and whether a season change is legible without reading the report.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).
