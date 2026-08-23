# Spec: world-topology (wave 2)

**Status:** Draft — pending owner review. Module id `world-topology` in the [world map program](../world-map-program.md). Depends on `world-model` only. Can be built in parallel with `world-intel`. **Blocks `ai-commander`.**

## Assumptions I am making

1. **The lane graph is common knowledge**, so topology is computed over the true graph rather than per faction — consistent with `world-intel`'s assumption 1. Only *ownership* is filtered per faction, and that is the caller's business.
2. **No graph library.** Surveyed: [QuikGraph](https://github.com/KeRNeLith/QuikGraph) (MS-PL) ships DFS, BFS, A\*, shortest path, max flow and MST — **none of the three algorithms this module needs**, and it documents no iteration-order guarantee, which replay depends on. What is needed is roughly 85 lines of textbook code.

## Objective

Answer one question well: **what does it cost the empire to lose this sector?**

Not "is it valuable" — `ai-commander`'s value matrix owns that — but "is it *load-bearing*". A sector whose fall splits your territory in two is worth garrisoning even if it produces nothing, and a rich sector with three redundant routes to it can be left lightly held.

Success looks like: the AI reinforces the junction rather than the prize, and later the map view can tell a human the same thing at a glance.

## Design

### The three readings, and why one of them subsumes the others

| Reading | Answers | Cost |
|---|---|---|
| **All-pairs travel cost** | how far is everything from everything | Floyd–Warshall, `O(V³)` — fine to a few hundred sectors, and wave 1 has six |
| **Articulation points** | whose loss disconnects the graph outright | Tarjan, `O(V+E)` |
| **Reconnection cost** | how much *worse* everything gets if this falls | the delta between the two all-pairs runs |

**Reconnection cost is the one to build**, because it says everything the other two say and says it as one comparable number:

```
reconnect[s] = Σ over surviving pairs (a,b) :  cost'(a,b) − cost(a,b)
```

where `cost'` is all-pairs recomputed with `s` removed. A pair that becomes unreachable contributes a large fixed penalty rather than infinity, so the result stays an integer and stays comparable.

Read it as: **unreachable pairs appear** ⇒ `s` is an articulation point. **Large finite delta** ⇒ `s` is a chokepoint. **Near zero** ⇒ `s` is redundant, hold it lightly. One number, three meanings, and it is the number a garrison decision actually wants.

Articulation points are still computed separately, because "does this cut the empire" is a boolean worth having cheaply and Tarjan is `O(V+E)` against the delta's `O(V⁴)`.

### Scoping the traversal

Every reading takes a **sector filter**, so the same code answers three different questions:

- filter = all sectors → the map's topology
- filter = one faction's holdings → *that empire's* internal connectivity, which is what the AI wants
- filter = holdings minus a hostile-held sector → what a zone of control actually costs you

Lanes are edges when `State == Open`, the type carries supply, and a gate is not shut. Cost is `LaneCost.For(...)` with a null banner — topology is about the ground, not about who happens to be walking it.

### Cost, honestly

**Measured 2026-08-23** (task L11 / loam-map finding A5) — this section previously *asserted* these
numbers without anyone having run them, and DESIGN-GATE evidence rule 4 does not exempt our own docs.
Ring-with-chords topology, Debug build, `ReconnectionCostBench`, **three separate process runs** (a
single-shot Stopwatch reading is noisy on a cold JIT, so one run is a sample, not a measurement):

| Nodes | Lanes | Sweep (3 runs) |
|---|---|---|
| 8 | 9 | 0.1–0.2 ms |
| 16 | 18 | 11.5–16.8 ms |
| 32 | 36 | 6.4–10.9 ms |
| 64 | 72 | 46.8–79.7 ms |
| 128 | 144 | **606.5–700.0 ms** |

16 nodes consistently timed *slower* than 32 across all three runs — repeatable, not a fluke, and
plausibly a JIT tier-up or first-touch allocation cost landing on the smaller run rather than
anything about the algorithm (both are far below the 64/128 numbers that actually matter for the
size-table decision). Not chased further: it does not change either conclusion below.

So **sixty is fine**: under 80 ms per turn is comfortable for a turn-based commit, and the `huge`
world tier is confirmed shippable.

**128 is now measured: ~0.6–0.7 s.** That lands inside the 0.4–0.8 s estimate this section carried
before the run — arithmetic that happened to be right, not a substitute for having run it. It changes
no decision: the `giant` tier (`empire-economy-ssot.md` §4's size table) was already gated on the
Tarjan-first optimisation unconditionally, independent of what the raw sweep measured, so this
confirms the gate was correctly placed rather than moving it.

`O(V⁴)` for the full reconnection sweep is fine at six sectors and fine at sixty. It is not fine at
six hundred, and `world-generator` may well produce that; it is already uncomfortable at 128 without
the optimisation, which the size table anticipated. Two escape hatches, neither built now: compute
reconnection cost only for articulation points and chokepoint candidates (Tarjan first, then the
delta for the handful that survive), or cap the sweep to sectors within N hops of the frontier. Both
are optimisations of a correct, simple thing — worth writing down so nobody discovers the cliff by
surprise.

Recomputed per turn like `SupplyGraph`, never cached: same reasoning, and the cliff above is the only thing that would change it.

### Determinism

Tarjan's low-link values and Floyd–Warshall's relaxation both depend on iteration order — two orders give different-but-equally-valid answers, and that breaks replay. Every loop walks sectors and lanes in **ordinal id order**, and there is a test that reversing the input order changes nothing.

Integer per-mille throughout, like everything else in `Core/World`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Topology
dotnet test tests\FusionRpg.Guard.Tests
```

## Structure

```
src/FusionRpg.Core/World/Topology/ → LaneGraph.cs, AllPairsCost.cs,
                                     ArticulationPoints.cs, ReconnectionCost.cs
tests/FusionRpg.Core.Tests/World/Topology/ → one file per algorithm, plus hand-built shapes
```

## Code style

Pure static functions over `WorldState` plus a sector filter. No allocation per query beyond the result. Integer costs. No RNG, no wall clock, nothing stored.

## Testing strategy

Tested against **hand-built graph shapes** with known answers rather than only against `first-light`, because that map is too small and too well connected to exercise the interesting cases:

- **A path** `A–B–C`: `B` is an articulation point, `A` and `C` are not.
- **A cycle** `A–B–C–A`: nothing is an articulation point; every reconnection cost is small and positive.
- **A barbell** — two clusters joined by one lane: the join is the only articulation point and has by far the highest reconnection cost. This is the shape the whole module exists for.
- **A star**: the hub cuts everything; the spokes cut nothing.
- **Severing a lane** changes the answer immediately — nothing is cached.
- **A disconnected filter** (a faction holding two islands) does not throw, and reports the islands rather than pretending they are joined.
- **Ordering:** reversing sectors and lanes in the input leaves every result identical.
- **`first-light` sanity:** `ash-waste` is the only articulation point on the whole map and the most expensive sector to lose. **The homeworld is not critical** — ember-hollow and frost-mire reach each other round through ash-waste, so losing the capital strands nothing. Being important and being load-bearing are different things, which is the distinction this module exists to draw. *(An earlier draft of this spec claimed the opposite; the test disagreed and the test was right.)*

## Boundaries

- **Always:** ordinal iteration order; integer costs; recompute per turn; take a sector filter rather than assuming the whole map.
- **Ask first:** adding betweenness centrality (the fancier reading — real, but not yet needed); caching results between turns; adding a graph dependency.
- **Never:** floating point; `System.Random`; letting topology read faction *belief* — it works on the public graph, and filtering by ownership is the caller's job.

### The human sees this too

Reconnection cost is computed for the AI, but withholding it from the player would be the computer having the fun — the
thing [Sid Meier's rules](http://www.designer-notes.com/game-developer-column-5-sids-rules/) warn against most directly.
It is already computed, the numbers are already integers, and a commander looking at their own territory would know
which sector is the neck of the bottle.

So `#/world` gets a **lifeline overlay**: a toggle that shades each sector you hold by what its loss would cost you, and
marks outright articulation points. It reads only the public graph filtered to your holdings, so it leaks nothing — it
tells you about *your own* territory, which you can already see.

This also keeps the human and the AI on the symmetric footing `world-intel` exists to guarantee: same fog, same map,
same analysis.

## Success criteria

1. Reconnection cost ranks a barbell's join above everything else, on a graph built for the test. 2. Articulation points match the textbook answer on all four hand-built shapes. 3. Reversing input order changes no result. 4. Nothing is cached — severing a lane changes the next answer. 5. The lifeline overlay shades a human's own holdings and leaks nothing about anyone else's. 6. Suites and guards green.

## Open questions

What the fixed penalty for a newly unreachable pair should be — too low and cutting the empire in half looks cheap, too high and it drowns every other signal. Whether the AI should weight reconnection cost by what is *at* the disconnected end (losing a junction that strands your capital is not the same as one that strands a waste) — that is arguably the value matrix's job, not this module's. *(Resolved: the map **does** surface this to the human as a lifeline overlay — see below.)*
