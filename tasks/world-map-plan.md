# Implementation Plan: world map — wave 1 (foundation)

Specs: [../docs/architecture/world/spec-world-model.md](../docs/architecture/world/spec-world-model.md) · [spec-turn-engine.md](../docs/architecture/world/spec-turn-engine.md) · [spec-world-movement.md](../docs/architecture/world/spec-world-movement.md). Map: [../docs/architecture/world-map-program.md](../docs/architecture/world-map-program.md). Ideal: [../docs/architecture/world-graph-ideal.md](../docs/architecture/world-graph-ideal.md). Tasks: [world-map-todo.md](world-map-todo.md).
Named pair per repo convention — `tasks/plan.md` / `todo.md` hold the perf-v3 stream.

**Gate:** the three specs are **Draft — pending owner review**. This plan is written against them; if module boundaries move in review, the plan is rewritten rather than patched. No code until the owner approves both.

## Overview

Build the map's foundation: the places, the clock, and the first verbs. Wave 1 ends when a legion can march across a hand-authored six-sector world, meet an enemy mid-lane, clear a guarded slot, claim ground, and lose supply when the chain is cut — with the whole campaign replaying byte-identically from `(seed, template, command log)`.

Nothing on the map earns anything yet (economy is wave 3), nothing generates maps yet (wave 4), and combat is a deliberately disposable placeholder until the combat stream's seam lands.

## Options adopted in review (2026-08-21)

Four alternatives were taken over the first draft's defaults. All four **reduce** wave-1 work or defer decisions to the module that owns them; each is reversible by the owner.

| Decision | Instead of | Why |
|---|---|---|
| **Discrete-event movement resolution** | a fixed `StepsPerTurn = 12` | no arbitrary constant baked into `RulesetVersion`; crossings are exact rather than quantised; a quiet turn costs almost nothing |
| **Per-slot guards named by encounter id** | a `guard_strength` scalar on the sector | wave 1 never invents combat numbers it is unqualified to balance; guards defend the vein, not the ground; clearing a rich sector becomes several fights |
| **Turn reports re-derived, hot tail stored** | `report_json` on every turn forever | the engine is deterministic, so a report is reproducible; the log keeps hashes and versions, which are what detect drift |
| **FE renders from fixtures first** | FE blocked behind the state endpoint | W13 can start immediately, in parallel with all server work — the map becomes visible before movement exists |

## Architecture decisions (from the specs — locked)

- **`TurnEngine.Step` is pure**: no I/O, no clock, no ambient state. A guard test enforces the no-wall-clock half.
- **The barrier ships with exactly one policy** (`WaitForAllCommitted`). The interface exists so `Step` never learns why it fired — that keeps the RTS and idle policies reachable — but no second policy is built here.
- **Movement is a discrete-event queue** ordered by `(timeMilli, entityId)`, integer per-mille within the turn, with a monotonicity assert: processing an event may enqueue later events, never earlier ones.
- **One turn is one transaction**, correlation-idempotent, mirroring `ExecuteSummon` / `ExecuteFusion`.
- **Determinism discipline is `BattleEngine`'s**, reused verbatim: integer per-mille, stable ordering by entity id, named derived RNG streams, version stamps, golden hashes.
- **One entity table with a `kind`**; **derived state is recomputed, never stored** (supply, banner element, claimable).
- **Combat is a port.** `IBattleResolver` with a placeholder that wave 3 deletes; the map never reads combat internals and never authors combat numbers.
- **The map FE uses React Flow (`@xyflow/react`, MIT)** — owner decision after a library survey. Sectors are custom React nodes, lanes custom edges, positions authored, dragging and connecting off: a viewer, not an editor. Rejected: Cytoscape/Sigma/G6 (analysis or 100k-node scale we do not have), vis-network (physics fights authored positions), from-scratch SVG (re-solving pan/zoom for no gain). Leaflet `CRS.Simple` remains the fallback if the map ever wants a painted backdrop.
- **All new server code lives under `Core/World/*`** — no edits to `Core/Battle`, `Core/Combat`, `Core/Status`, or any injector path, so concurrent streams stay untouched.

## Dependency graph

```
W1 catalogs ──► W2 WorldState + first-light template + validation
                      │
                      ▼
                W3 schema + CreateWorld/Load (Data)
                      │
                      ▼
                W4 DTOs + read endpoints + SIM create
                      │
                W5 command model + command store
                      │
                W6 TurnEngine (phases, barrier, event queue, report, hash)
                      │
                W7 turn transaction + turn log (hot tail + re-derive)
                      │
                W8 determinism guards ── Checkpoint 2 (the SSOT gate)
                      │
      ┌───────────────┼───────────────┐
      ▼               ▼               ▼
 W9 movement     W12 supply      (W10 needs W9)
      │            traversal
      ▼
 W10 ZOC + contact + clear + placeholder resolver
      │
      ▼
 W11 claim ──────► Checkpoint 3 (wave-1 acceptance)

W13 FE map (fixtures) ──► W14 FE orders + playback     [parallel from day one]
```

**W13 has no server dependency** — it renders from a checked-in fixture, so the FE and the engine progress independently and meet at W14. **W12 is parallel-safe** with W9–W11: supply is a traversal over ownership, needing the phase pipeline rather than movement.

## Phases

1. **Model + storage (W1–W4).** The nouns exist, validate, persist, and read back. Zero behavior.
2. **The clock (W5–W8).** Commands, phases, event queue, turn transaction, hashes, guards. No gameplay verbs beyond `stand-fast`. Ends at **Checkpoint 2 — the SSOT gate**, the highest-value checkpoint in the wave.
3. **The first verbs (W9–W12).** Movement, contact, clear, claim, supply. Ends at Checkpoint 3, the wave-1 acceptance scenario.
4. **FE (W13–W14), running in parallel from the start.** Fixture-driven map first, live orders once W11 lands.

High-risk work is deliberately early: determinism and the turn transaction land before any gameplay depends on them.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Determinism drift via dictionary iteration, float, or wall clock | High | W8 guard tests (symbol scan, hash stability, replay), stable-ordering asserts in every golden |
| Event-queue bugs — a missed or mis-ordered crossing is silent | High | monotonicity assert on every enqueue; property test over random speed/progress pairs; crossing point must lie between both start positions |
| Seven new tables land before gameplay proves the shape | Medium | W2 proves the model in memory first; W3 persists only what W2 validated; `EnsureColumn` keeps later columns cheap |
| Placeholder resolver quietly becomes permanent | Medium | behind `IBattleResolver`, named `Placeholder*`, test asserts no production registration outside the world module; wave 3 deletes it as an explicit task |
| Report re-derivation is slower than expected on long campaigns | Low | hot tail keeps recent turns instant; if replay cost bites, periodic state snapshots are an additive change, not a redesign |
| React Flow re-render churn on pan with rich sector cards | Medium | memoized or module-scope node/edge components; render-count test in W13; playback writes transforms through refs, never React state |
| Concurrent streams (combat unification, VFX, perf) touching Core | Medium | everything new under `Core/World/*`; full suites at every checkpoint, not just filtered runs |

## Definition of done for the wave

- `dotnet test tests\FusionRpg.Core.Tests`, `...Data.Tests`, `...E2E.Tests`, `...Guard.Tests` green; `cd web\fusion-rpg-web; npm run test` green.
- `.\scripts\guard-dal.ps1` green (run all four guards at the final checkpoint).
- Wave-1 acceptance scenario passes: 20 turns, golden state hash, byte-identical replay, one transaction and one turn-log row per turn.
- No file modified outside `Core/World/*`, `Data/Sqlite/RpgStore.World*.cs`, `Contracts/WorldDtos.cs`, `Server/World*.cs`, `web/.../features/world/*`, and their tests.
- Owner receives a commit message draft and touched paths — **git hands-off; the agent never commits.**

## Open questions

1. **Guard wave ids: validated or opaque in wave 1?** Opaque strings are cheaper now and let the combat stream define the catalog; validation is a one-line change when it does.
2. **Report hot-tail depth** (proposed 50) — a guess until a real campaign measures replay cost.
3. **Homeworld loss consequences** (ideal §10.5) — the menu now includes **partial capture** across the homeworld's four sites and **a per-world difficulty setting**; still the owner's tone call, still not blocking this wave.
4. **The commander slot before heroes exist** — candidate: reuse the shipped **patron** demon rather than inventing a second concept. Ideal-level, not wave-1.


---

# Implementation Plan: world map — wave 2 (fog, topology, and the first opponent)

Specs: [spec-world-intel.md](../docs/architecture/world/spec-world-intel.md) · [spec-world-topology.md](../docs/architecture/world/spec-world-topology.md) · [spec-ai-commander.md](../docs/architecture/world/spec-ai-commander.md).

**Gate:** all three specs are **Draft — pending owner review**. `world-intel` and `world-topology` shipped 2026-08-22 (W15–W24, checkpoints 5–7). `ai-commander` is planned separately below, against the code those two produced rather than against intent.

## Overview

Wave 1 built a world that plays. Wave 2 makes it a world you have to *find out about*.

Two modules, both prerequisites for the AI and both useful on their own. **`world-intel`** gives every faction — human and AI alike — its own fog: what it can see now, what it remembers, and how stale that memory is. **`world-topology`** answers one question about the lane graph: what does it cost the empire to lose this sector.

Neither builds an opponent. That is the point: an AI that reads the whole truth cannot be tuned, because it never makes the mistakes that make an opponent legible, and the only lever left is artificial handicaps. Fog first, brain second.

## Decisions taken during specification (2026-08-22)

Three questions the owner explicitly delegated — "use web search and algorithm to resolve them, I don't have experience" — were settled from prior art rather than deferred. All three are recorded with their reasoning in the intel spec.

| Question | Decision | What decided it |
|---|---|---|
| Does a glimpse reveal enemy strength? | **A band, never an exact figure** — six tiers, each with a midpoint and a ceiling | BattleTech's bare "blip" gives nothing to decide with; War in the East 2 prints an *estimated* strength with a `?`. The estimate is the middle path, and it needs no RNG |
| What does `scout` cost? | **Half a turn's movement for twice the sight** | Total War already prices sight in movement — ambush trades all of it. Half follows Sid Meier's *double it or cut it by half* |
| Is intel age shown to the player? | **Yes, explicitly — "seen N turns ago"** | Sid Meier's *the player should have the fun, not the computer*. Hiding the date creates note-taking, not tension |

The band's two readings are also the AI's whole estimation model: **ceiling when deciding whether to defend, midpoint when deciding whether to attack.** Pessimism where being wrong is fatal, realism where it is merely expensive — no probability, no priors, no floats.

## Architecture decisions (from the specs — locked)

- **Belief is stored, and it is the first exception to "recompute everything".** It is an accumulation of history, not a function of the current world, so it cannot be derived. It is hashed, replayed and migrated like any other state.
- **The graph shape is public; its contents are private.** You can see six sectors and the lanes joining them; you cannot see what is in them. Hiding the graph itself would make the map unreadable.
- **A new `Intel` phase bumps `RulesetVersion` to 2.** Wave-1 goldens re-bless once, deliberately, with the reason in the commit message. Stored version-1 reports will refuse to re-derive rather than fabricate — which is the behaviour already built for exactly this.
- **`IWorldView` becomes the only read path.** Once it exists, no policy and no endpoint touches `WorldState` directly.
- **Topology works on the public graph**, takes a sector filter, and is recomputed per turn like `SupplyGraph` — never cached.
- **No graph library.** QuikGraph (MS-PL) ships DFS, BFS, A*, shortest path, max flow and MST — none of the three algorithms needed here — and documents no iteration-order guarantee, which replay depends on. The three are ~85 lines of textbook code.
- **Everything new stays under `Core/World/*`**, as in wave 1, so concurrent streams remain untouched.

## Dependency graph

```
        (nothing — pure over WorldState)
                    │
        ┌───────────┴───────────┬──────────────┐
        ▼                       ▼              ▼
  W15 LaneGraph +         W17 strength    W18 visibility
      AllPairsCost            bands
        │                       │              │
        ▼                       └──────┬───────┘
  W16 articulation +                   ▼
      reconnection cost         W19 belief model + storage
        │                              │
        │                              ▼
        │                       W20 Intel phase + RulesetVersion 2
        │                              │
        │                              ▼
        │                       W21 IWorldView + BelievedWorldView
        │                              │
        │                    ┌─────────┼─────────┐
        │                    ▼         ▼         ▼
        │              W22 projection  W23 scout  W24 FE fog
        │                  + leaks      price
        └──────────────────────┴─────────┴─────────┘
                               │
                    ai-commander (wave 2, after the spec amendment)
```

`world-topology` (W15–W16) shares no code with `world-intel` and can be built by a second pair of hands from day one.

## Slicing

1. **Phase 5 — the pure pieces.** Four tasks, all parallel-safe, none wired into the engine. Nothing can break, and the two hardest algorithms get proven against hand-built graphs before anything depends on them.
2. **Phase 6 — belief becomes state.** Storage, then the phase, then the read interface. The `RulesetVersion` bump is isolated in one task so exactly one re-bless happens, with one reason.
3. **Phase 7 — fog reaches the player.** The projection is where fog can leak, so it gets a test per leak; the FE makes the whole thing visible, which is the only way anyone will notice if it feels wrong.

High-risk work is early again: the version bump and the leak surface both land before the AI depends on them.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The `RulesetVersion` bump invalidates wave-1 goldens and every stored report | High | isolated in W20; goldens re-blessed once with the reason in the commit message; the re-derivation refusal across versions is already built and already correct |
| Fog leaks through the projection — the only place it can | High | one test per row of the visibility table, plus a property test that a viewer's payload never names a sector id that viewer has never seen |
| Belief drifts from truth where a faction is standing | High | belief is written once per turn in one phase; a test asserts that for a sector you occupy, belief equals truth exactly |
| Tarjan and Floyd–Warshall are order-dependent — two orders give different-but-valid answers, which breaks replay | High | ordinal iteration everywhere; a test reverses sectors and lanes in the input and asserts every result is identical |
| `O(V⁴)` reconnection sweep hits a cliff when the generator makes large maps | Medium | fine to a few hundred sectors and wave 1 has six; two escape hatches written into the spec (articulation-first filtering, frontier-capped sweep) so nobody discovers the cliff by surprise |
| The FE fixture silently drifts once the state endpoint becomes a projection | Medium | `WorldFixtureTests` already fails on DTO drift; W24 regenerates the fixture for a viewer and the test keeps it honest |
| Wave 2 grew during the spec pass — `stance` was never implemented, and nothing in the game could heal | Medium | both fall inside W23 rather than becoming new tasks; the acceptance golden is the canary, since `first-light` authors neither stance |
| Belief storage grows as sectors × factions × worlds | Low | 200 sectors × 8 factions is 1 600 rows per world; written once per turn, read once per projection |

## Definition of done for the wave

- All five suites green: Core, Data, E2E, Guard, and `npm run test`.
- All four guard scripts green.
- The wave-1 acceptance scenario still passes at `RulesetVersion 2`, with the golden re-blessed exactly once and the reason recorded.
- A faction's projection never carries a sector, slot, or force it has not seen — one test per leak.
- Reconnection cost ranks a barbell's join above everything else on a graph built for the test.
- `#/world` renders unknown, remembered and watched distinctly, and a remembered sector says when it was seen.
- No file modified outside `Core/World/*`, `Data/Sqlite/RpgStore.World*.cs`, `Contracts/WorldDtos.cs`, `Server/World*.cs`, `web/.../features/world/*`, and their tests.
- Owner receives a commit message draft and touched paths — **git hands-off; the agent never commits.**

## Open questions

1. **A sixth strength band** once `sector-development` inflates army sizes. It is a catalog, so a data edit rather than a redesign — but somebody should look when yields land.
2. **Whether `RecoveryMilli = 150` is the right rate.** Seven turns from near-death to whole is a guess; the lever is one constant and a playtest will say.
3. **Whether the wild should get a more passive policy than Zomboss** rather than the same rules with different weights — an `ai-commander` question, carried here so it is not lost.

*(Resolved during the spec pass: `hold` is now immobility for defence and recovery, which also closes the fact that nothing in the game could heal; the grace turn on losing a sector needed no answer once visibility was defined over the turn's start and end; the lifeline overlay ships. The `stance` command turned out never to have been implemented at all, which is why W23 grew.)*

---

# Implementation Plan: world map — wave 2b (`ai-commander`, the first opponent)

Spec: [spec-ai-commander.md](../docs/architecture/world/spec-ai-commander.md) — fourth pass, audited symbol by symbol against the shipped `world-intel` and `world-topology`.

**Gate:** the spec is **Draft — pending owner review.** No task starts until it is approved.

## Overview

Wave 2 made the world something you have to find out about. This makes something else find it out too.

Zomboss files commands through the same path the player does, from the same fog, with no privileged read and no handicap. The module is three things stacked: a **safety fix** that must land before anything auto-commits, a set of **pure evaluation tables** over belief, and a **rule policy** that reads them and files orders.

The order matters more than usual here, and it is not the order the module is written in. The riskiest work is not the brain — it is the seam, because auto-committing turns an existing latent bug into one a browser refresh can reach.

## The finding that reorders the plan

Today the HTTP commit path **never advances a world.** `WaitForAllCommitted` needs all three factions; nothing ever commits for the wild or Zomboss; so the barrier only ever fires in tests that commit as each faction by hand.

Auto-filling the AI is exactly what makes `POST /commit` a turn-resolving operation. The moment it is:

- a **retried** commit reads the new current turn, commits *that*, fills the AI again, and burns a second turn — the method's comment promising a duplicate is a no-op is only true while the barrier never fires;
- `CommitWorldTurn` captures `turn` **before** taking `_gate`, so a commit landing in between leaves the fill filing orders into a turn that is already over.

One fix for both: the caller names the turn it means to end, re-read inside the lock, mismatch refused as `turn.stale`. **That is task one**, and it ships and is verified on its own before a single line of AI exists.

## Architecture decisions (from the spec — locked)

- **The AI runs outside `Step`, before the barrier.** It is a commander that files commands, never a phase. This is what makes AI decisions *data in the command log*, so replay never re-runs a policy and Zomboss's brain can be rewritten in wave 5 without breaking a single wave-2 save. There is a test for exactly this claim and it lands in Phase 8, before anything depends on it.
- **The fill lives in `RpgStore.CommitWorldTurn`, not the endpoint** — the barrier is there, and filling at the endpoint would leave every non-HTTP caller unable to advance.
- **A policy reads `IWorldView` and nothing else.** Enforced by a source-scan guard: nothing under `World/Ai/` may mention `WorldState`. No behavioural test catches a right answer arrived at by cheating.
- **Two graphs, kept apart.** `LaneGraph` is the *supply* lens and drops deep rifts and temporal currents; `MarchGraph` is what a legion can walk. Threat spread and reach use the march lens; reconnection cost and supply use the supply lens.
- **One order per entity per turn.** Bounds the AI's write, makes `ai-{turn}-{entityId}` collision-free by construction, and makes the log one line per legion.
- **A policy that throws is not caught.** One transaction; it rolls back; the world is untouched and the bug stays visible.
- **The wild keep `stand-fast`.** Only Zomboss gets a brain. A hazard, not a third empire.

## Dependency graph

```
  W25 expectedTurn (commit is idempotent per turn)   <- ships alone, no AI involved
        |
        v
  W26 policy seam + catalog + validation + stand-fast
        |
        v
  W27 the fill in CommitWorldTurn + reason column     <- the replay-swap test lands here
        |
        +--------------> W28 reasons on the wire
        |
        v
   === Checkpoint 8: End Turn advances. Nothing decides anything. ===
        |
        +---------+-----------+---------+-----------+---------+
        v         v           v         v           v         v
   W29 march   W30 believed  W31      W32 reach   W33       W34 curves
   graph+hops    supply     threat    +frontier   value    + considerations
        |         |           |         |           |         |
        +---------+---------+-+---------+-----------+         | (independent)
                            v
   === Checkpoint 9: the tables exist. Still nothing decides anything. ===
                            |
                  +---------+---------+
                  v                   v
            W35 rules 1-3       W36 rules 4-7
                  +---------+---------+
                            v
                     W37 Zomboss flips  <- the one golden re-bless
                            v
                     W38 acceptance
```

Phase 9's six tasks share no files and can be built in any order or in parallel.

## Slicing

1. **Phase 8 — the turn can end.** The whole seam with a policy that decides nothing. At the end of it End Turn works in the browser, the world advances, and the architectural claim is proven — with zero intelligence anywhere. Everything after this phase is quality of decision, which is the cheap half to get wrong.
2. **Phase 9 — the tables.** Six pure tasks over belief, nothing wired in, no behaviour change possible. Each is provable against hand-built fixtures the way W15–W18 were.
3. **Phase 10 — the brain.** Rules in two batches, then the single task that flips Zomboss and re-blesses the goldens, then acceptance.

**Why `stand-fast` before `frontier-rules`:** it separates "the plumbing works" from "the decisions are good". If End Turn misbehaves after Phase 10 there would be two candidate causes; after Phase 8 there is one, and it is already ruled out.

## Expected fallout, budgeted

| What moves | Where | Why it is not a regression |
|---|---|---|
| `WorldTurnCommitTests.CommitAll("wild","zomboss")` | Data tests | that helper exists only because the AI never committed; W27 makes committing as the player enough |
| `WorldTurnE2ETests` commits as all three commanders | E2E | after W25 the second call is a **stale-turn refusal**, which is the new contract under test |
| Every golden built from the template | Core/E2E | W37 only — `PolicyId` is inside `WorldCanonical`'s faction row, so flipping Zomboss changes the hash. **One re-bless, one reason, one task** |
| Nothing else | | Phases 8 and 9 must move **no** golden: `stand-fast` is a no-op kind and both factions already carry that policy id |

That last row is a checkpoint assertion, not a hope. If a golden moves before W37, something is wrong.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| Auto-commit burns turns on a retry or a double-click | **High** | W25 lands first and alone; `expectedTurn` is required, not optional, so a caller cannot forget it; the regression test is written before the fill exists |
| A policy reads `WorldState` and is subtly omniscient | High | source-scan guard scoped to `World/Ai/`, in the guard suite that already scans that tree recursively; plus the belief-only projection tests from W22 |
| Threat built on the supply graph — an enemy across a deep rift becomes invisible to fear | High | `first-light` **cannot catch this**: all six of its lanes carry supply, so the two lenses coincide. W29 ships a purpose-built two-lane fixture and the threat test uses it |
| The one golden re-bless spreads across tasks | Medium | W37 does nothing but flip the policy id and re-bless; every earlier task asserts goldens are unmoved |
| `frontier-rules` oscillates — a legion re-files a stance forever and never moves | Medium | a stance costs the turn it is committed, so Recover and Explore file the stance *or* the action, never both; one test per rule for the no-re-file case |
| Defend fires permanently because spread makes threat non-zero nearly everywhere | Medium | the rule compares threat against the garrison already standing there, not against zero; the six-sector map is small enough that "above zero" would have deadlocked expansion |
| ValueMap grows into the biggest task in the wave | Medium | `INeedVector` ships as a uniform stub, so Yield is a lookup; the other five axes read tables W29–W32 already built and tested |
| A policy exception wedges the world | Low | not caught, by decision: the transaction rolls back and the next commit throws again, visibly. Purity is what makes that safe — there is nothing half-finished to clean up |

## Definition of done for the wave

- All five suites green: Core, Data, E2E, Guard, `npm run test`; all four guard scripts green.
- End Turn advances the world from the browser with no manual commits, and a **repeated** End Turn refuses instead of burning a turn.
- Swapping the registered policy leaves an existing command log's hashes unchanged — the architectural claim, as a passing test.
- Nothing under `World/Ai/` mentions `WorldState`.
- Every AI order in a 20-turn run carries a reason, and every reason names a sector that faction had seen.
- Exactly one golden re-bless in the wave, in W37, with the reason recorded on the constant.
- No file modified outside `Core/World/*`, `Data/Sqlite/RpgStore.WorldTurns.cs`, `Contracts/WorldDtos.cs`, `Server/WorldEndpoints.cs`, `web/.../features/world/*`, and their tests.
- Owner receives a commit message draft and touched paths — **git hands-off; the agent never commits.**

## Open questions

1. **Is `frontier-rules` worth playing against?** Only a playtest answers it. The rules-first order stands until one says otherwise: a utility scorer with no economy to score against is a tuning exercise with no signal.
2. **Do the four constants survive a bigger map** — `MaxSpreadHops = 4`, `ExploreTurns = 3`, `RecoverAtMilli = 400`, `OptimismMilli = 700`? Each is a single named constant with its reasoning attached, and the first three are what a playtest is actually testing.
3. **Should the wild ever act?** Answered *no* for now, from prior art — a hazard, not a third empire. If it changes it is a new policy with its own rules, never the same rules at different weights.
