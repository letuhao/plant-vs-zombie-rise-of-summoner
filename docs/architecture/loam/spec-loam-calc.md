# Spec: loam-calc (wave 1)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-calc` in the
[loam capability map](../loam-map.md). Depends on `loam-model`.
**Design source:** [empire-economy-ssot.md](../empire-economy-ssot.md) §3 ·
[economy-principles.md](../economy-principles.md) §13.

## Objective

Every piece of loam arithmetic, **pure and wired to nothing** — production, territory components,
upkeep, balance, the fade rate, habitability — plus the instrumentation that lets the numbers be *found* instead of
argued about.

Success looks like: each answer is workable on paper against a hand-built fixture, a hundred-turn
scripted run prints a net-flow table, and the turn engine still does exactly what it did yesterday
because nothing calls any of this yet.

**This module repeats the pattern that worked.** W25–W34 built every AI evaluation table against
fixtures with nothing wired, and checkpoint 9 was literally *"the tables exist and still nothing has an
opinion."* It caught real defects early and cheaply, and mutation testing later found five vacuous
tests that coverage had called 100%. Same shape here.

## Design

### The six calculators

Each is a static class over a `WorldState` (or, where the AI will later need it, over ids and rows
alone — see **Belief-safety** below).

#### 1 · `LoamProduction`

```
production(sector) = Σ over slots that are loam sources of  seepPerTurn(slotType)
```

Wave 1's only source is the `rootbed` slot itself (map finding A10 — untended ground seeps).

**There is no chain gate**, per the owner's S3 resolution. A source produces where it stands,
unconditionally. Connectivity does not decide *whether* loam appears — it decides **who can spend
it**, which is `TerritoryComponents` below. That is ideal §8.1 restated as §12.7 concluded it: a cut
source does not go dark, it simply stops being able to help the rest of you.

#### 2 · `TerritoryComponents`

`(world, factionId)` → the connected components of that faction's held sectors, each a stable-ordered
set, the whole collection ordered by its lowest member id so a replay cannot drift.

**This is the module's most load-bearing function** and it is four lines of flood-fill. Loam is
fungible inside a component and not across one, so severing a lane splits an economy in two with no
routing algorithm anywhere in the program.

It is **not** `SupplyGraph.ConnectedSectors`, and the difference matters: that one seeds from Seats
and answers *"is this in supply"*. This one has no seeds and answers *"which blocks of my territory
can pay for each other"*. Same graph, different question — and the spec says so here because the two
are one careless refactor away from being merged into something that is wrong for both.

#### 3 · `LoamUpkeep`

```
upkeep(sector) = ( base
                 + Σ garrison upkeep
                 + f(DevelopmentLevel, DangerBand) )
                 × FractureIntensityMilli / 1000
                 × UpkeepHandicapMilli / 1000
```

The handicap is a declared per-faction lever (`loam-model`), 1000 for everyone unless a world says
otherwise.

> **Quantities are `long`, so this is safe by construction** (`loam-model`). Two multipliers plus
> "divide once" **overflows `int`** — a base sum near 350 gives `350 × 3000 × 2000` ≈ 2.1e9, already at
> `int.MaxValue`, and wrapping produces **negative upkeep**, which reads as free territory rather than
> crashing.
>
> ```csharp
> long upkeep = sum * intensityMilli * handicapMilli / 1_000_000;   // sum is long; no cast needed
> ```
>
> An earlier draft fixed this with an explicit `(long)` cast, the house style at `SupplyGraph.cs:109`
> and `ValueMap.cs:88`. Making the **quantity** `long` is better: a cast has to be remembered every
> time, a type does not. Divide once, so rounding still happens in one place.

> **⚠️ Assumption on an open decision (map finding A3).** The ideal contains **two** multipliers —
> distance (§8.3) and chaos intensity (§12.6) — which double-count the same intuition. This spec
> implements **intensity only**. Distance already costs the player in logistics; charging it again
> inside upkeep is invisible on screen and unfalsifiable in tuning, because a stalled empire gives no
> clue which multiplier stalled it. If the owner wants distance back it is one term and one test —
> but it should be a decision, not a leftover.

There is **no structure term yet** — structures do not exist until wave 4. The formula is written so
that adding `Σ structure upkeep` is one summand, not a reshape.

> **Exemptions (G-B, G-C).** Unowned sectors produce nothing and cost nothing. A faction holding no
> loam source anywhere is skipped entirely rather than being charged and fading — the same shape
> `SupplyGraph` already uses for factions with no Seat.

#### 4 · `LoamBalance`

`production − upkeep`, per sector, **summed per component**, and summed per faction. Trivial, and deliberately its own named
thing: ideal §12.4's central claim is *most sectors lose money*, and a design whose most important
property has no function to ask it about will never have that property tested.

#### 5 · `FadePolicy`

```
shortfall ⇒ StabilityMilli falls, by an amount scaled to how deep the shortfall is
surplus   ⇒ StabilityMilli recovers, more slowly than it falls
```

Recovery must be slower than decay. Symmetric rates make a sector oscillate on the boundary and turn a
dramatic mechanic into a flickering number.

Fade is **graded** (ideal G7): production and stability degrade together well before zero, so a player
feels ground slipping rather than reading that it slipped.

#### 6 · `Habitability`

```
habitable(sector) ⇔ it holds at least one loam source
```

The owner's settlement rule (ideal §8.10), and its wording never changes across the program — only the
**set of sources** grows when `loam-structures` lands. Wave 1: rootbed slots. Wave 4: wells and
waystations too.

### Belief-safety, and the guard that is already watching

`loam-ai` will need production, upkeep and habitability over what a faction **believes**, and
`WorldDeterminismGuardTests` **already fails any file under `World/Ai/` that mentions `WorldState`** —
a guard that has been seen to fail, after an incident where a broken heredoc meant it had never
actually run.

So each calculator ships **two overloads** from the start, exactly as `ReconnectionCost` and
`LaneGraph` did:

- `For(WorldState world, ...)` — the truth side
- `For(<the rows and ids it actually reads>, ...)` — the belief side

Retrofitting the second overload later is what forced corrections in the AI program three times. Build
both now; the second one costs almost nothing when the first is written to expect it.

### The instrumentation harness (map finding A9)

Not a dashboard — **a test-shaped tool**, in the same suite as the determinism goldens.

Given a template, a seed and a turn count, it replays and prints:

| Metric | Principle | Healthy |
|---|---|---|
| Net flow per faction per turn | P1 | Oscillates around zero; never monotone positive |
| Share of sectors running a deficit | §12.4 | **Most of them.** If this is low, the central economic claim is not true in practice |
| Binding frequency — how often loam blocks an action | P3 | Often, at every empire size |
| Income growth vs upkeep growth as territory grows | P2 | Same order; divergence is the failure and is visible long before it is felt |
| Yield concentration — share from the best sector | P9, P12 | Falls as depletion bites |

Two of these become **assertions**, not just output: net flow must not be monotone positive over a
long run, and the deficit share must stay above a floor. Those are the two that fail silently and
expensively.

### What this module must not do

Nothing here is called from `TurnEngine`. `Production` and `Pressure` stay `return world;` until
`loam-turn`. **`RulesetVersion` does not move, and no golden moves.** That is the acceptance criterion
that keeps this module honest — if a golden moves, something got wired that should not have been.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Loam
dotnet test tests\FusionRpg.Core.Tests          # goldens must be unmoved
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.World.Loam
.\scripts\mutate.ps1  -Set loam-calc            # new mutant set, authored with the module
```

## Project structure

```
src/FusionRpg.Core/World/Loam/TerritoryComponents.cs
src/FusionRpg.Core/World/Loam/LoamProduction.cs
src/FusionRpg.Core/World/Loam/LoamUpkeep.cs
src/FusionRpg.Core/World/Loam/LoamBalance.cs
src/FusionRpg.Core/World/Loam/FadePolicy.cs
src/FusionRpg.Core/World/Loam/Habitability.cs
src/FusionRpg.Core/World/Loam/LoamPolicy.cs      → every constant, one file, each with its reasoning
tests/FusionRpg.Core.Tests/World/Loam/*.cs
tests/FusionRpg.Core.Tests/World/Loam/EconomyHarnessTests.cs
scripts/mutants/loam-calc.json
```

`LoamPolicy` holding every number in one file is the `MovementPolicy` precedent: constants that will be
tuned repeatedly belong together, each with the sentence explaining why it is what it is.

## Code style

Integer only; the float guard enumerates all world sources and needs no extension. Per-mille for
multipliers and rates; **`long` for quantities**, so arithmetic promotes without a cast. **Multiply in `long`, divide exactly once** — the
`long` prevents the overflow above, the single division keeps rounding in one place — the W34 lesson, where a curve that rounded three times drifted off
the curve it was named after.

## Testing strategy

**Per calculator**, against hand-built fixtures with answers workable on paper — not against
`first-light`, which the AI program proved is *actively misleading* (a ZOC-projecting wild pack on the
one interesting junction, a Seat in nearly every sector, and a hub that lights the whole map).

Named cases every calculator must have:

- **Ordering** — reversing sectors, slots or entities changes no answer.
- **Components** — one territory with a severed lane yields **two** components; reversing sector order changes neither their contents nor their order; an unowned sector never joins one.
- **The severed economy** — a rich half and a poor half, once split, stop subsidising each other. This is the S3 resolution asserted rather than assumed.
- **Intensity** — the same sector at 500 and at 2000 costs half and double, and at 1000 costs exactly
  the unmultiplied sum.
- **No overflow at the extremes** — the largest legal `(sum, intensity, handicap)` triple produces a
  positive, correct answer. A test at the boundary, because this one fails silently and produces a
  *negative* upkeep, which would read as free territory rather than as a crash.
- **The deficit claim** — a fixture built from ordinary ground runs a deficit. This is ideal §12.4
  asserted rather than believed.
- **Fade asymmetry** — recovery is strictly slower than decay.
- **Habitability** — a sector with a rootbed is habitable; the same sector unchained is not; a sector
  with no source never is.
- **Belief parity** — the two overloads return identical answers on the same data, so the belief side
  cannot silently drift from the truth side.

**Mutation, not just coverage.** A `loam-calc` mutant set ships with the module. The AI program's
retraction is the reason: an earlier *"all 22 mutants caught"* was **false** because a concurrent
stream had `Core` uncompilable and `dotnet test` exits non-zero for build failures too. The script now
refuses a red baseline — and the deeper lesson was that ordered-rule and formula code hides vacuous
tests that coverage reports as fully covered.

## Boundaries

- **Always:** two overloads per calculator (truth and belief); every constant in `LoamPolicy` with its
  reasoning; a mutant set authored with the module; fixtures hand-built, not `first-light`.
- **Ask first:** re-introducing the distance multiplier (A3); merging `TerritoryComponents` with `SupplyGraph.ConnectedSectors` (they answer different questions); any constant that would make loam stop
  binding (**P3**); adding a sixth calculator.
- **Never:** calling any of this from `TurnEngine` in this module; floats; a second implementation of
  supply connectivity — `SupplyGraph.ConnectedSectors` is the one; `WorldState` inside anything
  `World/Ai/` will later consume.

## Success criteria

1. Every calculator has a fixture whose answer can be checked by hand.
2. A severed territory becomes two components that cannot pay for each other, proven by test.
2. **No golden moves and `RulesetVersion` is unchanged** — proof that nothing got wired early.
3. The harness runs 100 turns and prints the table; net-flow and deficit-share are assertions.
4. The mutant set is fully caught, on a **verified-green baseline**.
5. Both overloads of every calculator agree, proven by test rather than by inspection.

## Decided (2026-08-23)

- **A3 is closed: no distance multiplier.** This was written as an "assumption on an open decision" and
  nobody disagreed for a day of design work. It is a decision now — intensity carries remoteness, and
  the reason is unchanged: two multipliers make a stalled empire unfalsifiable.
- **`f(DevelopmentLevel, DangerBand)` is one term**, because A8's relationship — yield must outrun
  upkeep or nobody develops — is only visible and tunable when it is a single expression.

## Still open, and correctly so

- **Every number.** Deliberately unanswered here: the harness exists so they can be measured against a
  real map in `loam-maps`, and choosing them now would be guessing with extra steps.

That is the only one. It is one item, not a list, and it has a method attached.
