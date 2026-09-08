# Spec: `zomboss-adaptive`

Module 8 in the [species-build capability map](../species-build-map.md). **No dependencies.**

⛔ **Corrected 2026-09-05 by the spec review.** An earlier draft declared a dependency on `resolver-memo`
(1). That was **fictional**: module 1 memoizes `AptitudeResolver.Resolve` (the derived/lawn path), while
the Zomboss is a battle actor and battle resolves through `ResolveForBattle` — a different function the
memo never touches. The dependency bought nothing and would have serialised two independent modules.

## Objective

Wire the nine already-authored Zomboss patterns, and make him adapt — **rotating on level-up, counter-
building on a lose streak, with his pattern revealed after the next fight** (owner decision 4).

Almost all of the mechanism ships already and has **zero production callers**:

| Piece | State |
|---|---|
| Nine authored patterns (3 pure + 6 mixed, permille shares + `AuraId`) | **built** — `ZombossPatterns.cs:25-89` |
| Shares → a real, budget-capped `AptitudeAllocation` | **built** — `ZombossPattern.ToAllocation(scope, budget)` |
| A switchable active pattern | **built** — `ZombossCommanderAllocation.cs` (`SetActivePattern`; `Refresh` is what actually produces the allocation) |
| Anything calling them | **wiring gap** — its own comment says they *"already existed with ZERO production callers"* |

**Success looks like:** a player can read the opponent, learn the cycle by fighting it, and never be
surprised by a build they had no way to see coming.

## Design

### Surfaces — audit finding A8

**Battle and expedition only.** `ZombossPattern` appears in **zero injector source files**; lawn enemy
composition is the host game's own waves. The spec says this plainly so nobody expects him on the lawn,
and so the acceptance criteria do not ask for something that cannot exist.

### Adaptation, and its limits

Two triggers, both tunable, both rate-limited:

1. **Rotation on level-up.** When the Zomboss's own level rises, he may re-pattern. Seeded and
   deterministic — the pick is a function of `(seed, level)`, never a live roll.
2. **Counter-build on a lose streak.** After the player wins `loseStreakThreshold` consecutive
   encounters, the next pick is biased toward the pattern that counters the player's own dominant
   posture.

The counter relation is **already documented in the pattern table's own comment** and is not re-derived
here: *"Onslaught breaks Bulwark+Retribution, so FORCE counters BASTION; Pierce breaks Fortitude+Vigor,
so FINESSE counters FORCE; Precision+Ferocity break Agility+Composure, so BASTION counters FINESSE."*

⛔ **The rate limit is not optional.** Perfect adaptation — even lagged — converges on *every player
build is equally bad*, which destroys the RPS that free build depends on
(`class-system-ideal.md` §7b.5, arriving from the opposite direction). At most one re-pattern per
`repatternCooldownEncounters`, and the counter-bias is a **weight, not a guarantee**: it raises the odds
of the countering pattern, it does not select it. A deterministic hard counter is the Mario Kart failure
mode with extra steps.

### The reveal — decision 4, and it is the good part

**His pattern is revealed after the fight, not before.** That produces a *symmetric one-fight
information lag*:

| | Knows | Acts on |
|---|---|---|
| Zomboss | the build you beat him with last time | one-fight-old information |
| Player | the pattern he ran last time | one-fight-old information |

**Neither side holds current information about the other.** That is what separates this from the four
systems the ideal's §6.3 records as backlash cases (RE4's hidden rank, Mario Kart, EOMM): they were
punished for adaptation that stayed **hidden**, not adaptation that arrived **lagged**. Staleness is a
tunable; a secret is not.

It also lands on the principle the class system already borrowed: *"a blind opponent that visibly acts
on old information is more interesting than a sharp one, and it is the only version that can be tuned."*
Here it is made mutual.

**Delivery:** the pattern id travels on the existing battle report, which is already the post-fight
channel. `revealDelayEncounters` is a tunable so "after the next fight" can become "immediately" or
"after the match" without a code change.

### Determinism

Battles are reproducible from `(setup, seed)`. **The pattern is part of the setup**, resolved before the
battle runs — never rolled during resolution. A pattern chosen mid-resolve would make a battle
irreproducible from its own inputs, which the battle program's own contract forbids.

### Fairness is already guaranteed, and stays

`ToAllocation` caps spend at the supplied budget, so however often he re-patterns his build is drawn
from **the same finite pool the player draws on** — *"a harder Zomboss is a higher `Θ` or a better
allocation, never a stat nobody could have had"* (`class-system-ideal.md` §6.1). Free re-patterning is
an asymmetry of **adaptation speed**, not of magnitude. That is legal and deliberate.

### Tunables

`data/tuning/zomboss-adaptive.v1.json`: `loseStreakThreshold`, `counterBiasPermille`,
`repatternCooldownEncounters`, `revealDelayEncounters`, and the rotation weights. A missing key is a
**load rejection naming it**.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter Zomboss
dotnet test tests\FusionRpg.Core.Tests
python scripts\audit-magic-numbers.py --targets M1
```

## Project structure

```
src/FusionRpg.Core/Battle/Ai/ZombossPatternSelector.cs      the pure selector
src/FusionRpg.Core/Battle/Ai/ZombossCommanderAllocation.cs  gains a real caller + a scope argument
src/FusionRpg.Core/Battle/BattleModels.cs                   pattern id on the setup + report
src/FusionRpg.Server/WebMatchService.cs                     ⛔ THE SEAM — builds the enemy side
src/FusionRpg.Server/ExpeditionEndpoints.cs                 routes expeditions through that same seam
data/tuning/zomboss-adaptive.v1.json
tests/FusionRpg.Core.Tests/Battle/Ai/ZombossPatternSelectorTests.cs
```

⛔ **The seam was missing from an earlier draft of this list, and without it success criterion 1 is
unreachable** — nothing would have carried a pattern into an actual enemy squad. Two concrete
consequences the implementer meets immediately:

- **`ZombossCommanderAllocation` hard-codes the Commander scope**, including `PointsFor(Commander, theta, …)`.
  A Zomboss pattern is a *named allocation*, not a player's commander build, so the scope becomes an
  argument rather than a constant. That is a real signature change, not a wiring detail.
- The enemy side is built server-side, so this module touches `FusionRpg.Server` — which the earlier
  file list did not admit.

The selector is **pure**: `(history, level, seed, tuning) → patternId`. No store, no clock, no I/O — so
every adaptation rule is provable in `Core.Tests` without a battle.

## Code style

- Pattern ids resolve through `ZombossPatterns.Resolve`, which **throws rather than returning null** —
  its own comment explains why: *"a null would read as 'this Zomboss has no build', which is
  indistinguishable from the human."* Keep that.
- Enumerate patterns via `ZombossPatterns.All` (ordinal) so a seeded generator stays reproducible.
- Every threshold, weight and cooldown from tuning; the selector carries no bare literal.
- `long`/permille for the bias; bounded ratios say so.

## Testing strategy

1. **Determinism:** the same `(history, level, seed)` always yields the same pattern; a different seed
   generally differs.
2. **Rate limit binds:** within `repatternCooldownEncounters`, no second re-pattern occurs even when
   both triggers fire. Fails if the limit is removed.
3. **Counter-bias is a weight, not a guarantee:** over many seeds the countering pattern is more likely
   after a lose streak, and **is not always chosen**. Both halves asserted — the second is what keeps it
   out of the Mario Kart failure mode.
4. **Lose-streak threshold:** below it, no counter-bias applies.
5. **Budget cap holds:** an allocation from any pattern at any budget never exceeds that budget — the
   anti-cheat property, re-asserted here because this module is what makes it reachable.
6. **Reveal timing:** the pattern id appears on the report of the fight *after* the one it was used in,
   per `revealDelayEncounters`; at delay 0 it appears immediately.
7. **Setup, not resolve:** a battle resolved twice from the same setup produces the same pattern —
   proven by resolving the same `(setup, seed)` twice.
8. **Self-cancelling patterns stay unauthored** — the six mixed patterns are the only three
   non-self-cancelling pairs; a test pins the roster at nine so a future edit cannot quietly add a bad
   one.

## Boundaries

- **Always:** rate-limit adaptation; keep the selector pure; put the pattern in the setup; reveal
  through the existing report channel.
- **Ask first:** the tunable values; adding a tenth pattern (a content review, per §6.2 of the ideal);
  making the counter deterministic rather than weighted.
- **Never:** roll a pattern during resolution; exceed the budget cap; hide the pattern permanently; give
  the Zomboss a stat a player could not have had; put him on the lawn (he is not there).

## Success criteria

1. The nine shipped patterns have a real production caller.
2. Adaptation is rate-limited and weighted — proven by test, including that the counter is *not* always
   chosen.
3. The pattern is revealed on the following fight's report, at a tunable delay.
4. Battles stay reproducible from `(setup, seed)`.
5. The budget cap holds for every pattern at every budget.
