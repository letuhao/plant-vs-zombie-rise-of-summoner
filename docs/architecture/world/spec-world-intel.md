# Spec: world-intel (wave 2)

**Status:** Draft — pending owner review. Module id `world-intel` in the [world map program](../world-map-program.md). Depends on `world-movement`. **Blocks `ai-commander`.**

## Assumptions I am making

1. **The graph shape is common knowledge; its contents are not.** Everyone can see that six sectors exist and how the lanes join them — you just do not know what is *in* them. Same as opening a Heroes map: the land is drawn, the ground is dark. Hiding the graph itself would make the map unreadable and the FE unbuildable.
2. **Visibility is presence plus one lane**, doubled by the `scout` stance at half movement. Detailed below; resolved from prior art rather than guessed — see *Three calls, and what decided them*.
3. **Belief is stored, not derived.** It is an accumulation of history, not a function of the current world, so it cannot follow the "recompute everything" rule the rest of the module family follows. It is hashed, replayed and migrated like any other state.
4. **One belief set per `(world, faction)`** — "per session" in your words. No sharing between factions; there are no alliances yet.

## Objective

Everyone plays blind — the human and every AI alike.

Each faction sees only where it has presence or reach, remembers what it saw and *when*, and acts on beliefs that may be badly out of date. Nothing reads the truth except the engine.

This is the prerequisite for `ai-commander`, and not for fairness reasons: **an AI that reads the whole world cannot be tuned.** It never walks into anything, never defends the wrong flank, never gets surprised — so it has no legible failure modes, and the only lever left is artificial handicaps. Fog gives fallibility for free.

Success looks like: you march into a sector you scouted eight turns ago and find something that was not there before, and you are not annoyed, because you knew the intel was old.

## Design

### Three calls, and what decided them

The owner has no game-AI background and asked these to be settled from evidence rather than preference. Each is a decision, not an option.

**A glimpse reveals a strength *band*, never an exact roster.**

The prior art splits. [BattleTech](https://battletech.fandom.com/wiki/Scouting) gives a sensor contact as a bare "blip" — something is there, nothing more — and only visual spotting identifies it. [War in the East 2](https://www.dornshuld.com/rules/wite2/10-0.html), a far more rigorous wargame, floors detection the moment you are adjacent at a level that *does* show combat value, and prints an **estimated** strength with a `?` when part of a stack is unknown.

That `?` is the answer. Bare presence gives neither the player nor the AI anything to decide with; an exact number makes fog cosmetic. A band gives you enough to act on and enough room to be wrong, which is the whole point of fog. It also needs no RNG, so it costs nothing in determinism.

**`scout` costs half a turn's movement and sees two lanes.**

Total War already prices sight in movement, and prices it steeply: [ambush stance](https://r2encv2.totalwar.com/en/manual/single-player/0018_enc_page_campaign_play_military_stances/index.html) immobilises an army entirely in exchange for extended line of sight, while forced march buys +50% movement and gives up the ability to react. Sight-for-mobility is the established trade; only the rate is open.

Half — `MovementMilli = 500`, `SightLanes = 2` — sits between Total War's two poles and follows [Sid Meier's first rule](http://www.designer-notes.com/game-developer-column-5-sids-rules/): *double it or cut it by half*. A stationary garrison adopting `scout` for free vision is a feature rather than an exploit — that is exactly what a lookout is.

**The map shows how old your intel is.**

Sid Meier again, and this one is nearly a rule: *the player should have the fun, not the designer or the computer* — avoid hidden systems where the computer is the one enjoying itself, and keep the player informed about the consequences of their decisions.

A commander knows when their last report came in. Hiding the date does not create tension, it creates note-taking, and [bookkeeping the character would never have to do](https://www.lessthanthreegames.com/blog/2024/07/17/designing-games-for-how-we-learn/) is cognitive load rather than difficulty. The interesting uncertainty is *what changed*, not *how long ago you looked*. It also keeps the human and the AI on visibly symmetric footing, which is the thing this module exists to guarantee.

### Seeing, now

A faction **sees** a sector this turn if:

- it owns it, **or**
- one of its entities stands in it, **or**
- it is within `SightLanes` of either of those, over **open** lanes.

Evaluated against the world **at turn start and at turn end**, unioned. That one detail carries a surprising amount:

- A legion that marches *through* a sector and out the far side reports on it. Anything else is absurd — you were standing in it.
- A faction driven out of a sector this turn remembers it as of **this turn**, not as of whenever it last happened to look. Losing ground tells you what took it.
- No special case is needed for either, which is why the rule is written this way rather than as an end-of-turn snapshot with exceptions bolted on.

`SightLanes = 1` normally, `2` for a force in the `scout` stance — which is what finally makes that stance mean something, at the price of `MovementMilli = 500`, half a turn's march. A severed lane carries no sight. An entity on a lane sees both ends of it.

Adjacent sight is a **glimpse**, not a survey: owner, phase, danger band, and any force present *as a strength band* — never slot detail, never an exact roster. You have to stand on ground to know what is buried in it, which is what makes claiming a rich sector a gamble rather than a lookup.

### Strength bands

A band is what a glimpse reports and what a memory keeps. Standing in a sector gives the exact figure; everything else is banded.

| Band | Name | Strength | What a reader may assume |
|---|---|---|---|
| 0 | *empty* | 0 | nothing |
| 1 | skirmish | 1 – 499 | midpoint 250, ceiling 499 |
| 2 | warband | 500 – 1 499 | midpoint 1 000, ceiling 1 499 |
| 3 | host | 1 500 – 3 999 | midpoint 2 750, ceiling 3 999 |
| 4 | legion | 4 000 – 9 999 | midpoint 7 000, ceiling 9 999 |
| 5 | horde | 10 000+ | midpoint and ceiling both twice the floor |

A `StrengthBandCatalog`, validated at bootstrap like every other catalog — bands are content, not a `switch`.

Two readings are what make this usable by a policy: **read the ceiling when deciding whether to defend, the midpoint when deciding whether to attack.** Pessimism where being wrong is fatal, realism where being wrong is merely expensive. Both integer, both deterministic — and `ai-commander` needs no estimation model beyond choosing which one to ask for.

### Remembering

Anything seen is snapshotted into that faction's belief, stamped with the turn. Leaving does not erase it — it ages.

The four states reuse the `IntelState` enum that already exists, and are **derived** from `(lastSeenTurn, currentTurn, seenThisTurn)` so the ladder cannot drift from the data:

| State | Meaning |
|---|---|
| `Watched` | Seen this turn. Full detail if you are standing in it, a glimpse if it is next door |
| `Scouted` | Remembered, seen within `FreshTurns = 5` |
| `Rumored` | Remembered, older than that |
| `Unknown` | Never seen. Position and lanes only |

The snapshot holds owner, phase, climate, danger band, per-slot type and guard state, the forces present **at the detail they were seen** — exact strength if you stood there, a band if you only glimpsed — and `lastSeenTurn`. Deliberately **not** the whole sector record: remembering the truth exactly would make fog cosmetic.

**A destroyed force is forgotten, not remembered.** Stale references to things that no longer exist are their own bug class; if you watched it die, you know it died.

`WorldSector.Intel` stops being a global field — under fog it means nothing globally. The template's authored value becomes the **player faction's starting belief** at world creation, which is how `first-light` keeps its "you have heard rumours of Frost Mire" opening.

### Where it lives

`rpg_world_faction_intel`, keyed `(world_id, faction_id, sector_id)`. Small — sectors × factions — and written once per turn.

### When it updates

A new **`Intel` phase**, immediately before `Snapshot`: everything else has settled, so you see the world as it ends the turn rather than as it was halfway through.

This **bumps `RulesetVersion` to 2**, which is a real cost and worth naming: wave-1 goldens re-bless, and stored reports from version 1 will refuse to re-derive rather than fabricate. That refusal is the behaviour already built for exactly this, and using it is better than smuggling intel into `Snapshot` and turning the last phase into a grab bag.

### On the wire

`GET /api/world/{worldId}/state` gains a viewer — `?asFaction={id}`, defaulting to the player faction — and returns **believed** state, never truth:

| The viewer's state | What comes back |
|---|---|
| `Unknown` | Sector id and layout position. Nothing else |
| `Rumored` / `Scouted` | The remembered snapshot, plus `lastSeenTurn` and the derived state |
| `Watched` | Current truth, at glimpse or full detail depending on presence |

Lanes are always returned — the graph is common knowledge (assumption 1) — but a lane's `state` is only trustworthy if either end is visible.

The seed stays absent, as it already is. This endpoint is now the only place fog can leak, so it is where the tests point.

### On the map

Three sector treatments, driven off the state the projection already carries: **unknown** as a silhouette with no name, **remembered** dimmed with an explicit **"seen N turns ago"** stamp, **watched** as it draws today. Forces show a band name where the intel is banded and an exact strength where it is not, so a card never implies more certainty than the viewer has. The `unknown` flag in `worldViewModel` was written for this and becomes real rather than a proxy for the authored `IntelState`.

The checked-in fixture is regenerated for a viewer, so the FE keeps rendering without a server.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Intel
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
cd web\fusion-rpg-web; npm run test
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/World/Intel/  → Visibility.cs, FactionIntel.cs, IntelSnapshot.cs,
                                   IntelPhase.cs, IWorldView.cs, BelievedWorldView.cs
src/FusionRpg.Data/Sqlite/       → RpgStore.WorldIntel.cs
src/FusionRpg.Server/            → WorldEndpoints.cs projection only
web/fusion-rpg-web/src/features/world/ → SectorNode.tsx treatments, fixture regenerated
tests/…/World/Intel/             → visibility cases, staleness ladder, leak tests
```

## Code style

Pure functions over `WorldState` for visibility; the phase applies the result. Integer turns, stable ordinal ordering, no wall clock, no RNG. `IWorldView` is the only thing policies and projections read — nothing outside the engine touches `WorldState` directly once this lands.

## Testing strategy

- **Seeing:** you see what you stand in and what you own; one lane out is a glimpse; two lanes out is invisible unless scouting; a severed lane blocks sight; a `scout` legion marches half as far.
- **Bands:** every strength maps to exactly one band; boundaries are exclusive at the top; `ceiling >= midpoint >= floor` for all six; a glimpse never carries an exact figure and standing in a sector always does.
- **Remembering:** leaving a sector keeps the snapshot with the turn stamped; re-entering refreshes it; a force destroyed in front of you is forgotten rather than remembered.
- **The ladder:** `Watched → Scouted → Rumored` transitions on exactly the right turn; `Unknown` never regresses out of being known.
- **No leaks — the sharp ones:** the projection for a faction never carries a sector it has never seen, never carries slot detail for a glimpse, and never carries a force standing where that faction cannot see. One test per row of the table above.
- **Determinism:** intel is state, so the 20-turn acceptance scenario still replays byte-identically, and the golden is re-blessed once at `RulesetVersion 2` with the reason in the commit message.
- **Migration:** a world created before this lands loads, and its factions start with the belief the template authored rather than with nothing.
- **FE:** unknown sectors render without a name; a remembered sector shows its stamp; the render-count test still passes.

## Boundaries

- **Always:** project through `IWorldView`; store belief per `(world, faction)`; stamp what you saw with the turn you saw it; treat the graph shape as public and contents as private.
- **Ask first:** changing `SightLanes`, `FreshTurns`, the `scout` movement price, or the band table; letting factions share intel; making the graph shape itself hidden; remembering more of a sector than the snapshot above.
- **Never:** let a projection carry something the viewer has not seen; let a policy or an endpoint read `WorldState` directly once `IWorldView` exists; recompute belief from the current world (it is history, not a function); keep a destroyed entity in memory.

## Success criteria

1. Every faction, human and AI, reads the world only through `IWorldView`. 2. The state endpoint never leaks an unseen sector, slot, or force — one test per leak. 3. Belief survives a save and replays byte-identically. 4. `#/world` renders unknown, remembered and watched distinctly, and a remembered sector says when it was seen. 5. All suites and all four guard scripts green.

## Open questions

Whether the band table needs a sixth tier once `sector-development` inflates army sizes. It is a catalog, so that is a data edit rather than a design question, but somebody should look at it when yields land.

*(Four earlier questions — a grace turn on losing a sector needed no answer once visibility was defined over the turn's start **and** end, which covers eviction without a special case. The other three — what a glimpse reveals, what `scout` costs, and whether intel age is shown — were resolved from prior art rather than deferred to the owner. The reasoning is in* Three calls, and what decided them.*)*
