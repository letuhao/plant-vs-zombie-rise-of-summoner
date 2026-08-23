# Spec: loam-turn (wave 2)

**Status:** **Sealed 2026-08-23** — owner-approved.
 Module id `loam-turn` in the
[loam capability map](../loam-map.md). Depends on `loam-calc`.
**Design source:** [empire-economy-ssot.md](../empire-economy-ssot.md) §3 ·
[spec-turn-engine.md](../world/spec-turn-engine.md) (the phase order is locked there).

## Objective

Wire `loam-calc` into the turn, and let ground be lost to the Fracture for the first time. Two dormant
phases wake up, `RulesetVersion` goes to 4, and the world starts having an opinion about whether you
can afford what you hold.

Success looks like: a sector cut from its chain visibly runs down over several turns and is gone; a
sector with a rootbed and a line home holds forever; and the whole thing replays byte-identically from
`(seed, template, command log)` exactly as before.

## Design

### Where each piece lands in the locked phase order

`Reveal → Movement → Sieges → Production → Growth → Pressure → Events → Snapshot → Intel`

| Phase | Today | After this module |
|---|---|---|
| `Production` | `return world;` | **Yield**: `LoamProduction` per sector, added to `LoamStock`, capped at the policy capacity. Overflow is reported and lost |
| `Growth` | `return world;` | **Untouched.** It belongs to recruits and development — `sector-development`'s business, not ours |
| `Pressure` | `SupplyGraph.Run` | **Upkeep and fade**, *after* the existing supply pass. Per component: sum upkeep, draw it from the component's pooled stock, and let any shortfall drive `FadePolicy`. A sector at zero stability is lost |

Production before Pressure is the existing order and it is the right one: a sector earns, then pays.
It also means a sector can be saved by its own yield in the same turn it would otherwise have slipped,
which is the difference between a tense mechanic and an unfair one.

**`Pressure` runs loam *after* `SupplyGraph.Run`** so that a force lost to attrition this turn is not
still being fed by it — garrison upkeep must read the garrison that survived.

### Paying, per component — and who fades when a component cannot pay

Owner decision 2026-08-23 (map §7, S3): loam is **fungible within a connected component** of a
faction's territory. Each turn, `Pressure`:

1. asks `TerritoryComponents` for the faction's blocks,
2. sums the component's upkeep and its pooled stock,
3. draws what it can, **proportionally from each sector's stock**, remainder settled in ordinal id
   order so the arithmetic is reproducible to the unit,
4. and if the component cannot pay, applies the shortfall as fade.

**Which sectors fade is the whole design, so it is a stated rule and not an accident.** The shortfall
lands on the component's **weakest contributors first** — worst net balance, ordinal id as tiebreak —
until it is covered. That is ideal §7.7's automatic allocation: the player never distributes loam,
they only ever choose what to give up, and until they have a way to say so the game gives up the
ground that was least worth holding.

A player-set priority override belongs after the gate, with the FE that would let them set it.

**Proportional draw, not "empty the nearest sector".** Draining one sector to zero while its neighbour
sits full would make raiding a stockpile feel arbitrary and would leak the draw order into gameplay.

### Losing a sector — and why it happens in `Pressure`, not `Snapshot`

When `StabilityMilli` reaches zero: ownership clears, `Phase` becomes `SectorPhase.Lost`, structures
would ruin (none exist yet), and the report says so, naming the sector.

`Snapshot` is where **commands** settle — claims and postures. A fade is not a command; it is the
world acting on its own. Routing world-driven loss through `ClaimResolver` would tangle two unrelated
flows in the one file where ownership is already subtle.

**The consequence to notice:** `Pressure` runs *before* `Snapshot`, so ground that fades this turn can
be claimed the same turn. That is correct, and it is not exploitable, because of the next rule.

### `Lost` is not terminal

A sector at zero stability becomes `SectorPhase.Lost` and is unowned. It can be claimed again like any
other unowned ground — and if it still has no working source it will simply fade again, which is the
settlement rule doing its own enforcement (below). Nothing about `Lost` needs to be cleared by hand;
`ClaimResolver` moves it on the next successful claim.

### The settlement rule needs no enforcement — the fade is the enforcement

Ideal §8.10 says barren ground can be *taken* but never *kept*. So a claim on uninhabitable ground is
**allowed**, not refused:

- Refusing it would delete a real play — seizing a corridor to cut an enemy chain (§8.7's `Sever`).
- Allowing it costs nothing, because a sector with no working source simply fades again.
- It also closes the reclaim loophole automatically: a sector that faded for want of a source cannot
  be held by re-claiming it, since re-claiming does not create a source.

**What it does need is a warning.** `TurnReportEntry` gained a `SectorId` in W39, so a claim on
uninhabitable ground emits an entry naming that ground and marking the holding as temporary. Without
it the first player to try this files a bug — the same UI obligation §10.5 places on marching a legion
past its loam.

### `RulesetVersion` 4

`Production` and `Pressure` stop being pass-throughs, so how a turn resolves has moved. Stored reports
refuse to re-derive across an engine/ruleset version change rather than fabricating one — already
built, already correct, and proven by W7.

**Goldens move a second time in this program**, after `loam-model`'s field addition. That is two
re-blesses total and both are expected: W20 did exactly this and recorded both reasons on the
constant. Two known moves with written reasons is a healthy program; one surprise move is not.

### What must not change

The barrier, the command log, the discrete-event queue, `Snapshot`'s claim and posture handling,
`Intel` running last, and the pure-`Step` contract. This module adds two phase bodies. It does not
touch how a turn is driven.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Turn
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure

```
src/FusionRpg.Core/World/Turn/TurnEngine.cs        → Production and Pressure bodies; RulesetVersion 4
src/FusionRpg.Core/World/Loam/LoamPhases.cs        → the two phase passes, so TurnEngine stays readable
src/FusionRpg.Core/World/Turn/TurnReport.cs        → no shape change; new entry kinds only
docs/architecture/world/spec-turn-engine.md        → phase table updated; the order itself is unchanged
docs/architecture/decisions.md                     → RulesetVersion 4 row
tests/FusionRpg.Core.Tests/World/Turn/*.cs
tests/FusionRpg.Data.Tests/World*.cs               → golden re-bless with its reason
```

`TurnEngine` is already the busiest file in the module. The two passes live in `LoamPhases` and are
called from it, one line each — the same reason `SiegePhase` and `MovementPhase` are their own files.

## Code style

Integer only. Multiply before dividing, divide once. Report entries name their sector structurally via
`SectorId`, never by writing the name into prose — W39 established that, and the reason was that
matching a sector name out of a sentence works until somebody writes a different sentence.

## Testing strategy

**The named scenarios**, each a world built to make one thing true:

- **A chained rootbed sector holds forever** — run 50 turns, stability never falls. The control.
- **A cut sector runs down and is lost** — sever the lane, watch stock drain, stability fall, and the
  sector become `Lost` on a predictable turn. The headline.
- **A component is saved by its own yield** — production in the same turn covers upkeep that would
  otherwise have started a fade. Proves the phase order is doing its job.
- **A rich core carries a poor frontier** — a deficit sector holds indefinitely because it is
  connected to surplus. This is ideal §12.4's central claim asserted, and it is the test that would
  have failed under the per-sector accounting these specs originally implied.
- **Severing splits the economy** — cut the lane, and the far half starves on a predictable turn while
  the near half is untouched. The headline consequence of the S3 resolution.
- **The weakest ground goes first** — when a component cannot pay, the sector that fades is the worst
  net contributor, not an arbitrary one.
- **Barren ground claimed is barren ground lost** — the claim is accepted, the warning entry is
  emitted naming the sector, and it fades anyway.
- **Reclaiming does not rescue** — re-claiming a faded sector without a source does not stop the fade,
  which is the loophole the settlement rule closes for free.
- **Overflow is reported** — production above capacity is lost, and said to be lost.
- **A handicap is announced** — a faction whose `UpkeepHandicapMilli` is not 1000 has that stated in the report, once, so no reader ever mistakes a balance lever for a bug.

**Determinism, which is the assertion that actually matters:**

- The same `(state, commands, seed)` twice gives an identical state and hash.
- Reordering the input command list changes nothing.
- **The store's hashes are reproduced by the pure engine from the command log alone** — the W38
  property, re-run at `RulesetVersion` 4.
- A stored `RulesetVersion` 3 report **refuses** to re-derive rather than fabricating.

**Guards:** all four scripts, plus `WorldDeterminismGuardTests` — no wall clock, no unowned RNG, no
floats.

## Boundaries

- **Always:** the locked phase order; one re-bless with its reason on the constant; `LoamPhases` pure
  in `(state, seed)`; every report entry that concerns ground names it via `SectorId`.
- **Ask first:** touching `Growth` (it belongs to `sector-development`); changing the phase order;
  any behaviour that would make a *third* golden move in this program.
- **Never:** wall clock or unowned RNG inside `Step`; routing fade through `ClaimResolver`; a silent handicap; refusing a
  claim on barren ground — the fade is the enforcement, and refusing deletes a real strategy.

## Success criteria

1. All six named scenarios pass, each failing if its rule is removed.
2. Replay from `(seed, template, command log)` is byte-identical at `RulesetVersion` 4.
3. Exactly one golden re-bless in this module, reason recorded.
4. A version-3 stored report refuses re-derivation.
5. All four guard scripts green; no new float, clock or RNG violation.

## Decided (2026-08-23)

- **Overflow is reported per sector.** The turn report is where detail lives, and a per-faction summary
  would hide *which* sector is wasting — which is the only actionable half of the fact.
- **The capacity constant lives in `LoamPolicy`**, with every other number.
