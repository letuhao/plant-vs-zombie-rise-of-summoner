# Spec: ai-commander (wave 2)

**Build status (2026-08-22):** W25–W36 built and green — the commit seam, all six evaluation tables, the utility scorer, and `frontier-rules` with all seven rules. **W37 (flip Zomboss, re-bless) and W38 (acceptance) remain**, so the template still points both AI factions at `stand-fast` and nothing decides anything in a shipped world yet.

**Status:** Draft — pending owner review. Fourth pass, 2026-08-22: **audited against the shipped code, symbol by symbol.** The second pass was written against `world-intel`'s and `world-topology`'s *intent*; the third checked its central claims; this one checked the rest and found four more, one of which changes how risky the module is. §Corrections is the list — it is first because each entry is a thing a builder would otherwise hit halfway through. Module id `ai-commander` in the [world map program](../world-map-program.md). Depends on [world-intel](spec-world-intel.md) and [world-topology](spec-world-topology.md), both built and green.

## Assumptions I am making

1. **The AI is blind on the same terms as the player.** No privileged read, no map-wide sight, no handicap. `IWorldView` is the belief state `world-intel` builds, and it is the *only* thing a policy touches.
2. **The AI remembers nothing between turns beyond what its faction believes.** A deterministic scorer over a slowly-changing belief is stable on its own. If it dithers, the fix is stored intent — which becomes hashed, replayed state, so it is a decision to take deliberately rather than discover.
3. **Difficulty is which policy, not a stat bonus.** Handicaps remain available and unused. Fog already supplies fallibility; a blind AI walks into things without being told to.
4. **No new dependency.** Surveyed and rejected: `NRules` (conflict-resolution ordering is deliberately unspecified — fatal for replay), Unity utility-AI packages (`Core` is Unity-free by guard), `FluidHTN` (a planner where this needs a scorer; worth revisiting for multi-turn operations in wave 3+). The Infinite Axis Utility System *design* is borrowed; none of its code is.

## Objective

Zomboss takes his turns, so the barrier has someone to wait for — and he takes them **blind**, from the same fog you play in.

Every commander submits the same `WorldCommand` through the same path. The engine never learns which is which. If a policy ever needs something the command vocabulary cannot express, that is a signal the vocabulary is wrong, not a reason for a back door.

Success looks like: a legion you left on the frontier is in real danger by turn ten, and when Zomboss makes a mistake you can see *why* — he was acting on a report six turns old.

## Corrections — what the code says that the earlier drafts did not

Eight claims did not survive reading the shipped source. Each is load-bearing; each would otherwise have surfaced mid-build as a rewrite.

### 1. The AI fill belongs in the store, not the endpoint

The draft said `POST /api/world/{id}/commit` gains the responsibility. The barrier does not live at the endpoint — `RpgStore.CommitWorldTurn` ([RpgStore.WorldTurns.cs:225](../../../src/FusionRpg.Data/Sqlite/RpgStore.WorldTurns.cs)) inserts the commit row, reads the committers, fires `WaitForAllCommitted`, steps the engine, rewrites the graph and advances the turn, all inside one transaction. Filling at the endpoint would mean the player's commit returns `waiting` with a null hash for a turn that did in fact resolve, and **every non-HTTP caller would never advance at all** — which is most of the tests. The fill goes inside `CommitWorldTurn`, between the caller's commit insert and `ReadCommittersUnlocked`.

### 2. ⛔ Once the AI commits, *every* commit call resolves a turn

This is the finding that changes the module's risk profile, and it was invisible until the first two were settled.

Today the HTTP commit path **never advances a world**. `WaitForAllCommitted` needs all three factions, and nothing ever commits for the wild or Zomboss, so the barrier only fires in tests that commit as each faction by hand. Auto-filling the AI is precisely what turns commit into a turn-resolving operation — and the moment it does:

- A **retried** commit (a client that never saw the response) reads the *new* current turn, commits it, fills the AI again, and **burns a second turn**. The method's own comment says a duplicate commit is a no-op; that is true only while the barrier never fires.
- `CommitWorldTurn` calls `LoadWorldState` and captures `turn` **before** taking `_gate`. A commit that lands between the load and the lock leaves the fill deciding against a resolved world and filing orders into a turn that is over.

Both are the same hole and take the same fix: **the caller says which turn it means to end.** `CommitWorldTurn(worldId, commanderId, int expectedTurn)`, with the turn re-read **inside** the lock and a mismatch refused as `turn.stale`; `CommitWorldTurnRequest` gains `Turn`, and the FE sends the turn it rendered. A retry then refuses instead of costing the player a turn they never played, and the pre-lock read stops being load-bearing.

This is a behaviour change to a shipped, tested method, and it is not optional — without it this module makes an existing latent bug reachable from a browser refresh.

### 3. Topology and supply cannot be reached through `IWorldView`

`LaneGraph.Build`, `ReconnectionCost.For` and `SupplyGraph.ConnectedSectors` all take `WorldState` — the truth. A policy calling any of them reads the whole map, which is the leak this module exists to prevent. `LaneGraph.Build` only ever reads sector ids and lanes, so it takes an overload over `(IReadOnlyList<string> sectorIds, IReadOnlyList<WorldLane> lanes)` and both `WorldState` and `IWorldView` feed it. Supply is a genuine fork — §Believed supply.

### 4. `LaneCost.For` also takes `WorldState`

Not for the arithmetic — `length × type × hazard` is local to the lane — but for `LaneTouchesClimate`, which sweeps every sector to decide whether a ley lane's 800‰ discount applies. Under fog, climate is `IntelSnapshot.Climate`, and it is only known for sectors the faction has *seen*.

So `LaneCost.For` gains an overload taking a climate lookup rather than a world, and the truth-side caller passes one built from `WorldState`. The consequence is a feature, not a workaround: a faction that has never scouted a ley lane's endpoints **does not know the discount applies and over-prices the march**. Fog reaches into route planning, exactly where it should.

### 5. `PolicyId` is inside the state hash

[WorldCanonical.cs:30](../../../src/FusionRpg.Core/World/WorldCanonical.cs) writes it into the faction row. Both non-player factions currently read `stand-fast`, so **pointing Zomboss at `frontier-rules` re-blesses every golden built from the template** — for a real behaviour change, which is the only acceptable reason. It also means an unknown policy id is silently hashed today; `WorldValidation` must reject one, the way every other catalog reference is checked.

### 6. Command ids decide execution order

`Reveal` sorts by `(CommanderId, CommandId)` ordinally ([TurnEngine.cs:102](../../../src/FusionRpg.Core/World/Turn/TurnEngine.cs)), so a policy's ids are not cosmetic — they are the intra-commander ordering. `ai-{turn}-{entityId}`: unique per commander per turn (the store's idempotency key), stable across a re-run, sorted by entity.

### 7. The seed deriver already exists

`SeededRng.DeriveStream(seed, label)` is what `TurnCalendar` and `BattleEngine` use. The AI's stream is `DeriveStream(worldSeed, $"ai:{factionId}:{turn}")` — not a hand-rolled mix. One faction's rolls can then never shift another's, and adding a faction shifts nobody's.

### 8. The turn endpoint cannot show a reason yet

`GET /api/world/{id}/turn/{n}` projects `WorldTurnReportDto` from the stored **report**, which `Step` produces and which knows nothing about commands. Reasons live on command rows, and `ListWorldCommandsUnlocked` is private static with no public counterpart. Surfacing the audit trail therefore needs a `Commands` list on the DTO and a store method to fill it — a small addition, but one the earlier draft assumed for free.

## Design

### Where the AI runs, and why it is the load-bearing decision

**Outside `Step`, before the barrier.** The AI is a commander that files commands; it is never a phase.

A save is `(seed, template, command log)`. If the AI ran inside `Step`, its choices would stop being in the log and replay would have to re-run it — meaning **every future AI improvement would break every existing replay and golden**. Filing commands instead makes AI decisions *data*: replay never re-runs a policy, so Zomboss's brain can be rewritten in wave 5 and every wave-2 replay still reproduces byte-identically.

There is a test for exactly this claim, and it is the one that matters most in this module.

### The four layers

| Layer | What it is | Stored |
|---|---|---|
| World | `WorldState` — the truth | yes |
| **Belief** | per-faction intel, built by `world-intel` | **yes** — it is history, not a function of now |
| Evaluation | derived tables over belief, rebuilt every turn | no |
| Decision | candidate orders → choice → `WorldCommand[]` | no; the *output* is the command log |

Belief is the only stored layer the AI reads, and it is not the AI's — it is the faction's, and the human's view comes from the same place. `IntelSeed.ForTemplate` gives every faction an opening belief at world creation, so Zomboss can act on turn 1 rather than staring at a black map until something wanders past.

### Two graphs, and not confusing them

The module needs adjacency twice, and the two answers differ.

- **The supply lens** — `LaneGraph`, which by its own contract excludes lanes that carry no supply. A deep rift and a temporal current are absent from it even though an army can walk down both. `ReconnectionCost` and believed supply use this one.
- **The march lens** — every lane a legion can traverse, priced by `LaneCost`. `ReachMap` and `ThreatMap`'s spread use this one.

Building threat spread on the supply lens would make an enemy across a rift invisible to fear while being two days' march away. `Ai/MarchGraph.cs` builds the march adjacency from a view; `Ai/Hops.cs` is the unweighted BFS over it. Neither is `AllPairsCost`, which is weighted per-mille and answers a third question.

⚠️ **`first-light` cannot catch this mistake.** Its six lanes are `rift`, `ley` and `corridor` — every one of them supply-carrying — so the two lenses are *identical* on the shipped map and a policy built on the wrong graph would pass every test we have. `deep` and `one-way` exist in `LaneTypeCatalog` and appear in no template. The distinction therefore needs a purpose-built fixture, and this is the second time a wave-2 defect could only hide because `first-light` is too small to express it (the first was marching *through* a sector revealing nothing).

### ThreatMap — fear, spread by ignorance

The interesting table, because fog makes it interesting. A remembered enemy is not *there*; it is somewhere within however far it could have marched since you looked.

`world-intel` already made the two readings first-class: `RememberedForce.Defensive` is the exact count if you stood with it and the band's **ceiling** if you only glimpsed it; `RememberedForce.Offensive` is the exact count or the band's **midpoint**. The map reads whichever the question calls for and never touches a band directly.

For each force in belief that `ZoneOfControl.IsHostile` says is hostile — a pure faction-id comparison, so it is belief-safe as it stands — with `age = currentTurn − snapshot.LastSeenTurn`:

```
freshness   = max(0, 1000 − age × StaleDecayPerTurn)      // StaleDecayPerTurn = 150 ⇒ nothing from turn 7
spreadHops  = min(age, MaxSpreadHops)                     // MaxSpreadHops = 4
strength    = force.Defensive   when the question is "should I defend?"
            = force.Offensive   when the question is "should I attack?"

for each sector s within (spreadHops + 2) hops of where it was last seen:
    beyond      = max(0, hops(lastSeen, s) − spreadHops)
    proximity   = max(0, 1000 − beyond × 400)             // zero at 3, so the +2 bound is exact
    threat[s]  += strength × freshness / 1000 × proximity / 1000
```

Read it as: a **fresh** sighting is a sharp, local fear. A **three-turn-old** sighting is full-strength worry across everything within three lanes. A **seven-turn-old** sighting is nothing at all, because you genuinely do not know. Uncertainty makes you defend more places, which is correct, and stale intel eventually stops mattering, which is what makes scouting pay.

Threat outlives the intel ladder on purpose: `IntelLadder.FreshTurns = 5` is when a memory stops being *shown* as scouted, and 7 is when it stops being *worth acting on*. A rumour you no longer trust still makes you nervous for two more turns.

The two readings *are* the estimation model — pessimism where being wrong is fatal, realism where it is merely expensive. No probability, no priors, no floating point.

### ValueMap — worth, relative to this empire

Faction-relative, not faction-neutral: a fire vein is worth more to an empire short of fire. Six axes, each normalised to per-mille so no axis wins by accident of scale, combined by weights the policy owns.

| Axis | Reads | Note |
|---|---|---|
| **Yield** | believed slot types and elements × `INeedVector` | marginal utility — the reason value is faction-relative. A glimpse carries **no slots**, so this reads zero for anything not surveyed, and that is honest rather than a bug |
| **Strategic** | `ReconnectionCost` over the **believed supply graph**, normalised against this empire's own maximum | what it costs the empire to lose this |
| **Defensibility** | count of believed march lanes to sectors this faction does not believe it holds | a chokepoint is cheap to keep; a crossroads is not |
| **Cost** | `ReachMap` turns, plus guards still believed intact | what taking it actually costs |
| **Risk** | `ThreatMap`, inverted | |
| **Curiosity** | for `Unknown` sectors only: mean known yield × `OptimismMilli` (700) | see below |

`value = Σ(axis × weight) / Σ(weight)`, then **minus an overextension penalty** if holding it would leave it outside believed supply. That penalty must be able to drive the total below zero, because the classic 4X AI failure is blobbing outward until nothing is defensible, and the only cure is for bad ground to score *worse than nothing*.

Normalising the strategic axis against **this empire's** maximum rather than the map's is deliberate: the map-wide maximum is not knowable under fog, and a ratio against your own worst-case loss is the question a garrison decision is actually asking.

`INeedVector` is a uniform stub until `sector-development` ships stockpiles. The shape is right now; the numbers arrive later and the AI gets smarter with no AI changes.

**Curiosity is what makes anyone explore.** If an unknown sector is worth zero, nobody ever goes to look. Valuing it at the mean of what you *do* know, times an optimism factor below 1000, makes the unknown attractive in proportion to how good the map has been so far — beaten by a good known target, preferred over a poor one — and self-limiting, because when nothing is unknown there is nothing to be curious about.

### Believed supply, and the traversal both halves share

`SupplyGraph.ConnectedSectors` walks out from Seats the faction holds, across open supply-carrying lanes, skipping anything held against it. Under fog every one of those inputs changes: ownership comes from `Believed(id)?.OwnerFactionId`; a Seat is only known from a **survey**, because a glimpse carries no slots, so a faction can hold a sector and not know it has one; zone of control is judged from remembered forces; and `IWorldView` masks an unseen lane to `Open`. The believed network is therefore **optimistic**, and a faction discovers a cut chain by starving in it. That is the correct behaviour and it gets a test.

Two inputs, one rule. The traversal moves to `Movement/SupplyReach.cs` — seeds, an adjacency, a `usable` predicate, stable id order — with `SupplyGraph.ConnectedSectors` as its truth-side caller and `Ai/BelievedSupply.cs` as its belief-side one. Copying the BFS instead would leave two rules that must be kept identical while their inputs deliberately differ, which is the version of this that rots.

### ReachMap and the believed frontier

`ReachMap` is per **entity** — banner element changes ley costs, via `BannerElement.Of` — a Dijkstra over the march graph yielding `turns = ceil(cost / MovementPolicy.BudgetFor(stance))`. Severed and shut-gate lanes are not edges; unseen lanes read `Open` and therefore *are*, which is the same optimism supply has and for the same reason. `MovementRemaining` carries a part-marched legion mid-lane, so `ceil` is the right rounding: a multi-turn march is a real thing the engine already models.

~~`FrontierSet` includes **unknown neighbours**: the edge of what you hold and the edge of what you know are different sets.~~ **Wrong, corrected 2026-08-22 during W32.** `Visibility` makes every sector you own an observation post with a one-lane radius, so everything adjacent to your territory is at all times *at least* glimpsed — a neighbour you have never laid eyes on cannot exist. `FrontierSet` returns two sets, `Held` and `Contested`, and a third would be permanently empty.

Unknown ground is still a target; it is a **reach** question rather than an adjacency one, which is how the Explore rule was already written (*within `ExploreTurns`*, not *adjacent*). Ask `ReachMap` and `IWorldView.Believed`.

This is the fourth claim in this spec undone by the same fact — **holding ground grants full sight of it** — after the two struck from §Believed supply. Worth stating once as a rule: *nothing about your own territory is ever uncertain to you.* Fog is about other people's ground and about the past, never about where you are standing.

### The decision layer

```csharp
public readonly record struct PolicyOrder(WorldCommand Command, string Reason);

public interface IFactionPolicy
{
    string PolicyId { get; }
    IReadOnlyList<PolicyOrder> Decide(IWorldView view, ulong seed);
}
```

The view carries `FactionId` and `CurrentTurn`, so neither is a separate parameter — there is then no way to hand it one faction's belief and have it act as another, and no way for the turn a policy thinks it is to disagree with the turn its belief came from.

`FactionPolicies.Resolve(policyId)` is a catalog like every other: known ids only, and `WorldValidation` rejects a faction whose `PolicyId` is not in it. `stand-fast` becomes real (it is already the id both non-player factions carry). `frontier-rules` is the one that plays — ordered, first match wins per entity:

| # | Rule | Fires when | Order |
|---|---|---|---|
| 1 | **Defend** | a Seat I believe I hold has threat (defensive reading) **greater than the believed strength already standing there**, and this legion can reach it | `move` |
| 2 | **Finish** | standing where a slot is believed guarded and nothing hostile is visible | `clear`, lowest guarded slot index |
| 3 | **Take** | standing somewhere believed claimable | `claim` |
| 4 | **Recover** | wounds above `RecoverAtMilli` (400) and in believed supply | `stance hold`, or `stand-fast` if already holding |
| 5 | **Explore** | an `Unknown` sector is within `ExploreTurns` (3) and this legion is the cheapest to send | `stance scout` if not already scouting, else `move` |
| 6 | **Expand** | the best-value reachable sector I do not believe I hold scores above zero | `move` |
| 7 | **Hold** | nothing else did | `stand-fast` |

Explore sits *after* the concrete opportunities and *before* speculative expansion: you finish what is in front of you, then look, then commit.

Rules rather than scoring, deliberately — scoring wants an economy to score against and there is not one yet. The tables above are what both need, and they are what this module is really building.

Two details the earlier draft got wrong and that a builder would hit on day one:

**Defend's threshold cannot be "threat above zero".** Spread makes threat non-zero almost everywhere on a six-sector map, so that rule would fire permanently and nothing would ever expand. It compares against what is already garrisoned, which is the question a commander actually asks.

**A stance costs the turn you commit it** (`world-movement`), so Recover and Explore file the stance *or* the action, never both. A policy that filed `stance scout` and `move` together would watch the move dropped every turn and re-file it forever.

### The invariant that bounds everything: one order per entity per turn

`frontier-rules` walks `view.OwnForces` in ordinal id order and emits at most one `PolicyOrder` per entity; `stand-fast` emits exactly one entity-less `stand-fast` per faction. That single rule does four jobs: it bounds the AI's write the way `MaxCommandsPerSubmit = 200` bounds a client's, it makes `ai-{turn}-{entityId}` collision-free by construction, it makes the command log readable one line per legion, and it makes "did the policy do something silly twice" a countable assertion rather than a judgement call.

The `stand-fast` emission is worth its row: a faction that files nothing still commits, so the log would not distinguish *chose to do nothing* from *was never asked*. Two rows per turn for the wild is a price worth paying to tell those apart.

### The consideration arithmetic, built now

`ResponseCurves` and `Consideration` land here even though nothing scores yet: pure integer arithmetic, no world knowledge, provable in isolation, so wave 3 inherits a tested scorer and only has to choose *which* considerations to write.

Per the [Infinite Axis Utility System](https://www.gameai.com/iaus.php): a consideration maps one input through a curve to per-mille; a behaviour's score is the **product** of its considerations. The product is the point — a single zero kills a behaviour outright, replacing a tier of guard clauses.

- Curves `0..1000 → 0..1000`: `Linear`, `Inverse`, `Quadratic`, `InverseQuadratic`, `Smoothstep`, `Threshold(t)`. No logistic: it cannot be done in integers without an approximation nobody would trust.
- **Compensation**, because multiplying N scores drags everything toward zero (0.8³ = 0.51):
  `modifier = 1000 − 1000/n` · `makeUp = (1000 − score) × modifier / 1000` · `final = score + makeUp × score / 1000`
- **Momentum** — a bonus to the previous choice, which damps oscillation between near-ties — is specified but **not implemented**, because it needs memory across turns and assumption 2 says there is none. It is the concrete thing that would force stored intent.

### The commander loop

Inside `CommitWorldTurn`, after the caller's commit row and before the committers are read: for every faction with a non-null `PolicyId` not already committed this turn, **in ordinal faction order** — resolve the policy, run it against `new BelievedWorldView(world, factionId)`, insert its commands and reasons, insert its commit row. Then read the committers once and let the barrier decide.

Safe there: policies are pure and fast, and this is the code path that already owns the turn. The inserts reuse the commit's connection and transaction rather than re-entering `SubmitWorldCommands`, which would open a second transaction against a world it re-loads.

**A policy that throws is not caught.** The transaction rolls back, the commit does not land, the world is exactly as it was, and the next commit throws again — visibly. The alternative is a swallowed exception that turns a bug in integer arithmetic into a faction that quietly stops playing, which is the hardest class of defect this codebase could ship. Purity is what makes this safe: there is nothing to half-finish.

The store gains a dependency on `Core.World.Ai`. It already depends on `Core.World.Turn` for `TurnEngine` and `WorldCommandAdmission`; no SQL leaves `FusionRpg.Data`, so `guard-dal.ps1` is unaffected.

### The AI explains itself

Policies return a reason with every order. `reason` becomes a nullable column on `rpg_world_commands` via `EnsureColumn`, written by the AI fill and left null by the public submit path — deliberately **not** a field on `WorldCommand`, because that record is the replay unit and an audit string has no business inside it. Bounded to 200 characters at insert, and kept for the life of the world: commands cannot be trimmed (they *are* the save), so a reason with a shorter retention than the command it explains would go missing exactly when someone went looking for it.

Surfacing it needs the DTO work in correction 8. A line reads:

> `zomboss: move e-zomboss-band-1 → ash-waste — expand, value 640 (yield 300, strategic 810, risk 120), 2 turns`

Commands are not part of the state hash, so this costs nothing in determinism. An AI you can audit by reading what happened is worth more than one that is marginally cleverer — and under fog it is the only way to tell a mistake from a bug.

### Who gets which policy

Zomboss gets `frontier-rules`. **The wild keep `stand-fast`.** `first-light` authors `e-wild-pack-1` as a `Warband` on `hold` with `MovementRemaining = 0` — a garrison by posture, not by kind, so it *does* project a zone of control and is dangerous to walk past. What keeps it terrain-with-teeth is the stance, and a policy is what would take that away. Giving them an expansionist policy would turn a hazard into a third empire racing the player for every sector. Civilization draws the same line — barbarians raid from camps and never found cities — and it is the difference between a map with danger on it and a map with two opponents on it. If the wild ever need to act, that is a new policy with its own rules, not the same rules at different weights.

## Non-goals

No multi-turn plans, no stored intent, no diplomacy or inter-faction negotiation, no difficulty handicaps, no scoring of behaviours (the arithmetic ships; nothing calls it), no change to which commands exist, and no attempt to make Zomboss good. He needs to be *legible* first; a blind opponent that visibly acts on old information is more interesting than a sharp one, and it is the only version that can be tuned.

## Expected fallout

Two shipped test files assert the way turns end today, and correction 2 changes it. Both are expected edits, not regressions, and neither is a licence to loosen an assertion that finds something real:

- `tests/FusionRpg.Data.Tests/WorldTurnCommitTests.cs` — its `CommitAll("wild", "zomboss")` helper exists because the AI never committed. Committing as the player now resolves the turn on its own.
- `tests/FusionRpg.E2E.Tests/WorldTurnE2ETests.cs` — commits as each of the three commanders in sequence. After this module the second call is a *stale-turn refusal*, which is the new behaviour under test rather than a broken one.

Plus the golden re-bless from correction 5.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Ai
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/World/Ai/         → MarchGraph.cs, Hops.cs, ThreatMap.cs, ValueMap.cs, ReachMap.cs,
                                       FrontierSet.cs, BelievedSupply.cs, SlotValueCatalog.cs,
                                       INeedVector.cs, UniformNeeds.cs, IFactionPolicy.cs,
                                       FactionPolicies.cs, StandFastPolicy.cs, FrontierRulesPolicy.cs
src/FusionRpg.Core/World/Ai/Utility/ → ResponseCurves.cs, Consideration.cs
src/FusionRpg.Core/World/Movement/   → SupplyReach.cs (extracted traversal), SupplyGraph.cs, LaneCost.cs
src/FusionRpg.Core/World/Topology/   → LaneGraph.cs (overload over sector ids + lanes)
src/FusionRpg.Core/World/            → WorldValidation.cs (reject an unknown PolicyId)
src/FusionRpg.Data/Sqlite/           → RpgStore.WorldTurns.cs (AI fill; expectedTurn; reason column;
                                       a public per-turn command lister)
src/FusionRpg.Contracts/             → WorldDtos.cs (CommitWorldTurnRequest.Turn, report commands)
src/FusionRpg.Server/                → WorldEndpoints.cs (pass the turn through; surface reasons)
web/fusion-rpg-web/src/features/world/ → send the rendered turn with End Turn
tests/FusionRpg.Core.Tests/World/Ai/ → one file per table, one per rule, curve tests
```

Under `Core/World/` so it inherits `WorldDeterminismGuardTests` for free — that scan is `SearchOption.AllDirectories` over `src/FusionRpg.Core/World`, so the no-clock, no-`System.Random` and no-floating-point rules cover `Ai/` the moment the folder exists, with nothing to wire up.

## Code style

Pure functions over `IWorldView` with an injected seed. Integer per-mille throughout. Stable ordinal ordering. Nothing derived is stored. Policies return commands and reasons; they never touch `WorldState`, and after this module lands nothing outside the engine and the store does.

## Testing strategy

**The architectural claim, made executable.** Replay a stored command log through the engine with a *deliberately different* policy registered, and assert the hashes are unchanged. This is what proves AI work can never break existing saves, and it is the most valuable test in the module.

**Blindness.**
- A policy handed a belief with an unseen sector never files an order that names it — by construction, since `IWorldView` cannot return it, and by a test that would catch a leak added later.
- A source-scan rule added to `WorldDeterminismGuardTests`, scoped to `World/Ai/`: nothing there may mention `WorldState`. The leak this module fears is not a wrong answer, it is a right answer arrived at by cheating, and no behavioural test catches that.
- The ley-discount blindness from correction 4: two factions with identical legions price the same lane differently when one has scouted its endpoints and the other has not.

**The tables.**
- Threat: a fresh sighting concentrates; a three-turn-old one spreads across three hops at full strength; a seven-turn-old one contributes nothing anywhere; the defensive reading is never below the offensive one; a hostile force across a **deep rift** raises threat while contributing nothing to believed supply — written against a purpose-built two-lane fixture, because `first-light` has no non-supply lane and cannot fail this test.
- Value: overextension drives a sector below zero; curiosity makes an unknown sector attractive, loses to a good known target, beats a poor one, and stops mattering when nothing is unknown; a Seat outranks a wildland; the strategic axis ranks a barbell join top.
- Believed supply is optimistic: a faction whose chain is cut behind a lane it cannot see still believes it is supplied, and finds out by taking attrition.
- `SupplyReach` extraction: `SupplyGraph.ConnectedSectors` returns exactly what it returned before, on the shipped scenarios, unchanged.

**The rules.** One scenario that fires each and one that does not, for all seven; plus the two the audit added — a legion already scouting does not re-file the stance, and Defend does not fire on a Seat whose garrison already covers the threat.

**The invariant.** No policy emits two orders for one entity in one turn, asserted over a 20-turn run rather than a single call.

**Determinism.** Same `(belief, faction, seed)` ⇒ byte-identical commands twice; reversing entity order changes nothing; command ids are stable across a re-run; two AI factions' streams are independent (adding one does not move the other's orders).

**The commit path.**
- Committing as the player advances the turn with no other call.
- A **retried** commit is refused as `turn.stale` and the world stands still — the correction-2 regression, and the one most worth writing first.
- The fill is inside the commit transaction: a policy that throws leaves no commit row, no commands, and the turn where it was.
- One reason per AI command, none for a player command, truncated at 200 characters.
- A faction with no legions still commits, so the barrier releases.

**Acceptance.** The 20-turn scenario re-run with Zomboss live writes one turn-log row per turn, replays byte-identically, and every order in the log has a reason that names a sector that faction had seen.

## Boundaries

- **Always:** read through `IWorldView`; submit through the same command path as the human; integer per-mille; stable ordering; recompute; one order per entity per turn; give every command a reason.
- **Ask first:** giving the AI anything the player cannot see; stat handicaps; storing AI memory; a new command kind; changing the rule order; giving the wild an active policy; catching a policy exception.
- **Never:** AI code inside `Step`; a policy touching `WorldState`; caching evaluation tables between turns; `System.Random` or a wall clock; a network hop in the decision path.

## Success criteria

1. End Turn advances with no manual commits, from the store as well as the endpoint, and a repeated End Turn refuses instead of burning a turn.
2. Every AI order is explicable from its faction's belief alone — no order ever names something that faction has not seen.
3. Swapping the policy leaves an existing command log's hashes unchanged.
4. The 20-turn scenario runs with Zomboss live and replays byte-identically.
5. Every rule and every table has a passing test; nothing under `World/Ai/` mentions `WorldState`.
6. All suites and all four guard scripts green.

## Known cost, accepted

Pointing Zomboss at `frontier-rules` changes `WorldCanonical`'s faction row and **re-blesses every golden built from the template**. That is a behaviour change — the world now has an opponent in it — and the reason goes on the golden constant, as with wave 2's four.

## Open questions

Whether `frontier-rules` is worth playing against, or whether the utility scorer must arrive before `sector-development` — only a playtest answers that, and the rules-first order stands until one says otherwise, because a scorer with nothing to score against is a tuning exercise with no signal. Whether `MaxSpreadHops = 4`, `ExploreTurns = 3`, `RecoverAtMilli = 400` and `OptimismMilli = 700` survive a map larger than six sectors; each is a single constant with its reasoning attached, and the first three are what a playtest is actually testing.
