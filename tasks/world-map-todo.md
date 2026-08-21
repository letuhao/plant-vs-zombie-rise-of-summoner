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

- [ ] **Task W9: Movement — lane cost, event-ordered marching, the `move` command**
  - Description: `LaneCost` (integer per-mille, corridor and ley discounts, banner element computed not stored); movement seeding arrival events and solving lane crossings arithmetically; the `move` command wired into the movement phase.
  - Acceptance: cost goldens per lane type incl. both discounts; a three-turn march resumes exactly; movement never goes negative or exceeds budget; **property test** — for random speed/progress pairs the crossing time lies strictly between both start positions and is identical under either processing order.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/{LaneCost,MovementMath,MovementCommands}.cs`, `Turn/TurnPhases.cs` wiring, tests. Scope: M.
  - Dependencies: W8.

- [ ] **Task W10: Zone of control, contact, `clear`, placeholder resolver**
  - Description: `ZoneOfControl` (entering a hostile-occupied sector halts; supply does not route through it); `ContactResolver` for the three contact cases in stable entity order; the `clear` command issuing a battle request against a slot's `guard_wave_id`; `IBattleResolver` + `PlaceholderBattleResolver` applying survivors, wounds, deaths, rout.
  - Acceptance: each contact row tested incl. the stationary-defender case and same-faction stacking; **marching through a guarded sector is free** (guards never halt movement); `clear` flips only the targeted slot; a legion already inside a hostile sector is not re-halted on every event; mutual destruction resolves; the placeholder is registered only by the world module.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/{ZoneOfControl,ContactResolver}.cs`, `Turn/{IBattleResolver,PlaceholderBattleResolver}.cs`, tests. Scope: M.
  - Dependencies: W9.

- [ ] **Task W11: Claim**
  - Description: the `claim` command — a sector with no hostile entity and **every slot cleared** flips `owner_faction_id` and phase `held` when a legion holding a committed claim is present at Snapshot.
  - Acceptance: claiming what you already own is a reported no-op; claiming with any guard still intact is dropped with a reason naming the slot; two claims on one sector resolve by initiative with the loser blocked but keeping movement.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/MovementCommands.cs`, `Turn/TurnPhases.cs`, tests. Scope: S.
  - Dependencies: W10.

- [ ] **Task W12: Supply traversal + attrition** *(parallel-safe with W9–W11)*
  - Description: `SupplyGraph` — a stable-order breadth-first pass per faction from its homeworld through owned sectors over open lanes not under hostile zone of control, run in the Pressure phase; disconnected sectors reported; out-of-supply legions take attrition once per turn.
  - Acceptance: cutting one junction disconnects exactly the expected set; reconnecting restores it; attrition applies once per turn, never twice; nothing about supply is cached between turns.
  - Verify: Core filtered tests.
  - Files: `Core/World/Movement/SupplyGraph.cs`, `Turn/TurnPhases.cs`, tests. Scope: M.
  - Dependencies: W8 (needs the phase pipeline, not movement).

### Checkpoint 3 — wave-1 acceptance
- [ ] The acceptance scenario: 20 turns with marches, a lane crossing, a `clear`, a claim, and a supply cut ⇒ golden state hash, byte-identical replay, one transaction and one turn-log row per turn.
- [ ] Every ZOC, contact, clear, claim, and supply case has a passing test.
- [ ] Full suites + all four guard scripts green.
- [ ] Commit message draft and touched paths handed to the owner (**git hands-off — never commit**).

## Phase 4 — FE (parallel from day one)

Renderer: **React Flow (`@xyflow/react`, MIT)** — owner decision. Sectors are custom React nodes, lanes custom edges, positions authored, dragging and connecting off. Keep the lawn's split: pure folds in `.ts` with `.test.ts` beside them, renderer as a thin host. This adds the program's first new web dependency, superseding the earlier "no new dependencies" assumption.

- [ ] **Task W13: `#/world` map render (fixture-driven)**
  - Description: add `@xyflow/react`; `worldViewModel.ts` folds a world-state payload into `{nodes, edges}`; `SectorNode.tsx` renders the sector card (ownership border, climate accent, phase/intel treatment, slot pips with guard state, type icons from `/api/icons/{side}/{typeId}.png`); `LaneEdge.tsx` renders lane type and width as stroke; `WorldPage.tsx` hosts the canvas plus a sector inspector. **Renders from a checked-in fixture** so this task has no server dependency; the live query is swapped in at the end of W14.
  - Acceptance: the fixture world renders recognisably with stable positions across refresh; node and edge components memoized or module-scope — a **render-count test proves panning does not re-render sector cards**; selection drives the inspector; data flows through the bus layer, never a direct fetch.
  - Verify: `cd web\fusion-rpg-web; npm run test` (Vitest: fold + render-count) and a manual look.
  - Files: `web/fusion-rpg-web/src/features/world/{worldViewModel.ts,WorldPage.tsx,SectorNode.tsx,LaneEdge.tsx}` + tests, `fixtures/first-light.json`, `package.json`. Scope: M.
  - Dependencies: **none** — starts immediately. Regenerate the fixture from the real DTO once W4 lands.

- [ ] **Task W14: Live data, order queue, End Turn, report playback**
  - Description: `lib/bus/world.ts` queries replace the fixture; `worldSelection.ts` (selection + pending-order reducer, pure) and `turnPlayback.ts` (turn report → ordered keyframes, pure); select a legion, queue `move` / `clear` / `claim`, submit, press End Turn, then watch the report play back — legions animated along the lane path via `getPointAtLength`, driven by `requestAnimationFrame` writing transforms through refs.
  - Acceptance: an order round-trips and appears in the pending list; End Turn advances exactly one turn; playback renders in report order and matches the server's; the animation loop causes **zero** React re-renders of the graph.
  - Verify: Vitest for both folds and the no-re-render assertion; manual pass against a SIM server.
  - Files: `web/fusion-rpg-web/src/features/world/{worldSelection.ts,turnPlayback.ts,LegionMarker.tsx}`, `lib/bus/world.ts` + tests. Scope: M.
  - Dependencies: W13, W11.

### Checkpoint 4 — you can see it
- [ ] A human opens `#/world`, moves a legion, clears a guard, ends a turn, and watches what happened.
