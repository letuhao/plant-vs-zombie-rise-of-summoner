# Spec: loam-texture (wave 5)

**Status:** **Sealed 2026-08-23** — owner-approved, same authorization as the four specs before it.
Module id `loam-texture` in the [loam capability map](../loam-map.md). Depends on `loam-structures`
(specced). **Design source:** [empire-economy-ideal.md](../empire-economy-ideal.md) §9.1–§9.4 · A1's
own 500-hour-test closure (`loam-map.md` §1, already ✅ closed for every mechanic named here).

This is the last module in the build order and the map's own cut list names it loosely — *"whatever
survives A1's 500-hour test."* A1 already ran that test against every mechanic below and closed it
(`loam-map.md`): bounded worlds mean "permanent" only ever means "for this world," and wardens have
their own separate cure. This spec is not re-litigating that audit; it is building what it already
cleared.

## Objective

Six mechanics, each *derived* rather than bolted on (every one names what shipped code it reuses,
per the ideal's own discipline in §9): the **granary** (storage, a real planning horizon), **fade
contagion** (a lost sector makes its neighbours fade faster), the **Unmade** (faded ground births
hostile wildlife, for free, off an existing faction kind), **wardens** (permanently bind a demon to
stop one sector fading, at the cost of a `demon-contracts` binding slot forever), **prospecting** (a
stance that finds hidden rootbeds), and **Fracture surges** (the calendar's already-rolling `Plague`
finally does something).

Success looks like: a frontier with a granary can stage a push the same ground could never fund
turn-by-turn; losing one sector visibly threatens its neighbours, not just itself; barren corridors
neglected over a long game grow genuinely dangerous without a single authored spawn table; and none of
this needs a new number invented from nothing — every constant below is either reused from a
already-shipped, already-hashed, currently-unread field, or explicitly scheduled for a harness.

## Design

### Granary — a new `StructureKind`, because this one is a real second kind

`structure-substrate` shipped exactly one `StructureKind` (`LoamSource`) on the stated principle of not
inventing kinds before there is a real reason. There is now one: a granary does not produce and does
not gate habitability, it raises **capacity** — a third thing a structure can do, so it needs its own
kind rather than overloading `LoamSource`.

- **New**: `StructureKind.Storage`. `StructureDef { StructureId: "granary", Kind: Storage,
  RequiredSlotKind: Wildland, CostMilli: GranaryCostMilli, YieldMultiplierMilli: 1000 (unused for this
  kind) }` plus a new `StructureDef.CapacityBonus` (`long`) field, read only by `Storage`-kind
  structures.
- `LoamPhases.Production`'s existing cap (`room = Max(0, LoamPolicy.LoamCapacity - sector.LoamStock)`)
  becomes `room = Max(0, EffectiveCapacity(sector) - sector.LoamStock)`, where `EffectiveCapacity` is
  `LoamPolicy.LoamCapacity + CapacityBonus` for any sector holding an active granary, unchanged
  otherwise — additive to `LoamProduction`'s existing shape, not a rewrite, the same discipline
  `loam-structures`' own well multiplier already followed.
- **Overflow above the raised cap is still lost and still reported**, per this program's own standing
  rule (`spec-loam-turn.md`'s "overflow is per sector, a faction summary hides the actionable half") —
  a granary raises the ceiling, it does not remove one.

### Fade contagion — `PressureMilli`, confirmed unread, is exactly the field this needs

Verified against shipped code (not assumed from the doc): `WorldSector.PressureMilli` exists
(`WorldState.cs:110`), is hashed, persisted, and projected to the wire — and nothing anywhere computes
or reads it. Same for `DepletionMilli` (`WorldState.cs:111`), reserved below for the well over-draw
mechanic this module does not build (deep tap is "high value," §9.2, not core — cut from this wave,
see **What stays cut**).

- **Each `Production`**, for every sector whose `StabilityMilli` *fell* this turn (a fading, not a
  recovering, sector — `LoamPhases.Pressure` already knows this per-sector from `FadePolicy.Apply`'s
  own branch), raise `PressureMilli` on every **lane-adjacent** sector by a fixed
  `ContagionPressurePerTurn` (harness-tuned), capped at some `MaxPressureMilli`. A sector whose own
  neighbours are not fading has its `PressureMilli` decay back toward zero at a fixed rate, so
  contagion is a live signal of nearby trouble, not a ratchet.
- **`FadePolicy.DecayFor` gains one more term**: a sector's own `PressureMilli` adds to its effective
  deficit magnitude before the existing `BaseDecayMilli`/`MaxDecayMilli` clamp — reusing `DecayFor`'s
  existing shape (the same additive-then-clamp pattern its deficit term already uses) rather than a
  parallel decay formula. A sector next to a loss decays faster than the same sector in isolation would
  — this is the whole mechanic, and it costs one more addend inside a function this program already has.

### The Unmade — `WorldFactionKind.Wild` and `StandFastPolicy`, confirmed, with one real gap named

Verified: `WorldFactionKind.Wild` exists (`FactionKindCatalog.cs:17`), `StandFastPolicy` exists
(`StandFastPolicy.cs:19`, a no-op policy filing "stand fast" every turn, its own doc comment already
naming it *"the wild's permanent policy... a hazard on the map, not a third empire"*), and
`first-light` already wires a `Wild` faction with `PolicyId = "stand-fast"` (`WorldTemplateCatalog.cs:
79`). **The Unmade need no new AI and no new faction kind — they are `Wild` entities, spawned onto
`Lost` barren ground, using a policy that already exists.**

- **The one real gap**: `two-hearths` currently has **no `Wild` faction row at all** — confirmed by
  direct search, not assumed. The Unmade mechanic is faction-gated by construction: a sector fully
  faded (`SectorPhase.Lost`, no working source) accumulates an Unmade-spawn countdown, and when it
  completes, a new `Wild`-owned entity is placed there — but only on a map whose `WorldFactionKind.Wild`
  faction actually exists. **This is a map-authoring task for `two-hearths`, not a code gap** — adding a
  `Wild` faction row the same shape `first-light` already has, the day this module is implemented, not
  a schema change.
- Spawn rate and the entity's own strength are harness-tuned constants, not guessed here; the mechanism
  (place a `Wild`-owned `WorldEntity` at a `Lost` sector after N neglected turns) needs no new state
  beyond a per-sector neglect counter.

### Wardens — extends `demon-contracts`' shipped binding machinery, does not replace it

Verified: `ContractPolicy.Capacity()`/`CanBuySlot()` (`ContractPolicy.cs:79-81,163-168`) and
`RpgStore.BindContract`/`ReleaseContract` (`RpgStore.Contracts.cs:231-269,277`) are real, shipped,
capacity-gated, and already carry a `LoyaltyRank` concept. Binding a warden is **the same capacity
check, a different, permanent kind of bind**:

- **New**: a `BindAsWarden` action, capacity-gated by the same `ContractPolicy.Capacity()` check
  `BindContract` already runs, but the resulting bind is flagged non-releasable —
  `ReleaseContract`'s existing guard (already blocks release while deployed/on-expedition/patron) gains
  one more case: a warden bind refuses release unconditionally, for the life of the world.
- **New**: `WorldSector.WardenBindingId` (`string?`) — a sector with one set is exempt from
  `LoamPhases.Pressure`'s fade calculation entirely (its `StabilityMilli` neither rises nor falls while
  the binding holds), the literal shape of *"the sector stops fading."*
- **Cure already in place, per A1's own closure**: the demon consumed is gone from the roster
  permanently, via a mechanism (`demon-contracts` binding slots, already Soul-priced and scarce) that
  existed before this module and needed no new economy invented for it.
- **What warding does *not* exempt, resolved**: a warded sector's own upkeep is still summed into its
  component's total and still drawn from the pooled stock — warding stops the *fade*, not the *cost*.
  `LoamForecast.Weakest` (and `LoamPhases.Pressure`'s own inline selection it mirrors) must exclude
  warded sectors from candidacy entirely, the same structural exclusion this program already applies to
  G-B/G-C cases rather than a soft preference. **The edge case this closes**: if every sector in a
  component is warded and the component still cannot pay, there is no eligible fade target at all — the
  shortfall is reported (a turn-report entry naming the faction and the amount, the same obligation
  every other shortfall already carries) and otherwise **goes unapplied that turn**, rather than the
  selection throwing on an empty candidate list or silently picking a warded sector anyway. This is a
  real, rare, expensive-to-reach state (binding-slot scarcity already prices it), stated rather than
  left implicit the way the audit found it.
- **Warding vs. capture, resolved**: warding exempts `FadePolicy` only — a warded sector can still be
  taken militarily via ordinary combat/siege (or as a `loam-ai` `Sever` target, since severance is a
  combat outcome, not an economic one). **Capture releases the binding**: the warden's demon is not
  transferred to the new owner and is not returned to the original faction's roster — the binding ends,
  the specimen is gone, matching the same "permanent, no refund" shape this program has already used
  for a structure lost mid-construction (`spec-loam-structures.md`). A warded sector is still only as
  safe as the army defending it; the ward is an economic guarantee, never a military one.

### Prospecting — extends `world-intel`'s belief model, not the loam calculators

A dowser stance (or a light unit kind) reveals rootbed/well/waystation-bearing slots specifically, at a
range beyond ordinary scouting — this is a new observation kind inside `IntelRecorder`/`IntelState`
(`FusionRpg.Core.World.Intel`), not a change to anything under `World/Loam`. Scoped narrowly here on
purpose: prospecting answers *where the prizes are*, which is an intel question, and folding it into
the loam calculators would blur a boundary this program has held since wave 1 (loam pure and unwired
from belief/AI concerns).

### Fracture surges — `TurnCalendar.Plague`, confirmed rolled, confirmed inert

Verified: `TurnCalendar.Roll(turn, seed)` (`TurnCalendar.cs:27`) is pure in `(turn, seed)`, already
rolls `Plague` as one of five `CalendarRoll` values, and — confirmed by direct search — **nothing
anywhere reacts to a `Plague` roll** beyond a cosmetic log label (`TurnEngine.cs:220`) and a
roll-correctness test. This module is what makes it do something:

- **While the current turn's roll includes `Plague`**: the surge multiplier applies **inside**
  `DecayFor`'s pre-clamp sum, not to its return value. Audit-caught and worth stating exactly why: the
  shipped function is `scaled = BaseDecayMilli + deficit/DecayScaleDivisor*DecayPerDeficitUnitMilli;
  return Max(0, Min(MaxDecayMilli, scaled))` — **one** clamp, at the end, over the combined total.
  Multiplying the function's already-clamped *return value* by `SurgeDecayMultiplierMilli` would let a
  sector already at the `MaxDecayMilli` ceiling exceed it (a contagion-inflated deficit clamped to 300,
  then scaled `×1.5`, becomes 450 — 50% past the ceiling `MaxDecayMilli`'s own doc comment calls
  load-bearing: *"no single turn can zero a sector outright."*). The correct shape scales the *input*:
  `scaled = (BaseDecayMilli + deficit/DecayScaleDivisor*DecayPerDeficitUnitMilli) *
  SurgeDecayMultiplierMilli / 1000`, still followed by the one existing `Max(0, Min(MaxDecayMilli,
  scaled))` clamp — so a surge can push more sectors *toward* the ceiling, never past it. Recovery
  (`RecoveryMilli`) is untouched — a surge makes holding harder, it does not make giving up easier.
- **Already visible ahead of time**, per the calendar's own existing doc comment (*"a client can
  honestly show next week before it arrives"*) — no new prediction machinery, the roll already is one.

### What stays cut from this wave

Per §9.2/§9.4's own "high value, not core" and "rejected" lists, kept out on purpose, not forgotten:

- **Deep tap, scorched root, reavers** — genuine mechanics, lower priority than the four core ones;
  `DepletionMilli` is reserved for deep tap but not wired by this spec.
- **Loam market, loam as a battle resource, loam grades/tiers, the Fracture as a commanding AI faction,
  per-demon loam upkeep, randomised yields** — explicitly rejected in the design source (§9.4), each
  with its own stated reason; not re-argued here.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Loam
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Intel
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure (proposed)

```
src/FusionRpg.Core/World/StructureCatalog.cs             → StructureKind.Storage, granary row, CapacityBonus field
src/FusionRpg.Core/World/Loam/LoamPolicy.cs               → GranaryCostMilli, CapacityBonus, ContagionPressurePerTurn, MaxPressureMilli, SurgeDecayMultiplierMilli (harness-tuned)
src/FusionRpg.Core/World/Loam/LoamPhases.cs               → EffectiveCapacity; contagion spread pass; surge multiplier read from CalendarRoll
src/FusionRpg.Core/World/Loam/FadePolicy.cs               → PressureMilli and surge terms added to DecayFor
src/FusionRpg.Core/World/WorldState.cs                    → WorldSector.WardenBindingId, a neglect-turn counter for Unmade spawning
src/FusionRpg.Core/World/WorldCanonical.cs                → new fields into the hash — part of the batched post-gate golden move, see the note below
src/FusionRpg.Core/World/Intel/IntelRecorder.cs           → the prospecting observation kind
src/FusionRpg.Data/Sqlite/RpgStore.Contracts.cs           → BindAsWarden; ReleaseContract's warden refusal case
src/FusionRpg.Core/World/WorldTemplateCatalog.TwoHearths.cs → add a Wild faction row (map-authoring, not a schema change)
tests/FusionRpg.Core.Tests/World/Loam/LoamTextureTests.cs (new)
docs/architecture/decisions.md                            → the batched golden-move row (see the note below)
```

## Code style

Same throughout: `long`/integer accumulation, per-mille multipliers, every new constant a harness
target rather than a guess, additive extensions to `FadePolicy`/`LoamPhases` rather than parallel
formulas.

## Testing strategy

- **Granary raises the cap, overflow above it is still lost and still reported.**
- **Contagion**: a sector adjacent to a fading one decays faster than the same sector with no fading
  neighbour; the effect decays back to baseline once the neighbour stabilizes or is lost.
- **The Unmade spawn on neglected, `Lost`, barren ground** on a map with a `Wild` faction, and provably
  do **not** spawn on a map without one (the exemption re-proven explicitly, per this program's own
  G-C lesson about re-checking an exemption everywhere its logic could apply).
- **A warden-bound sector never fades**, regardless of its component's own solvency, and its binding
  cannot be released — ever, unconditionally, unlike an ordinary contract.
- **A fully-warded component with a shortfall applies no fade and does not crash** — the explicit edge
  case above, proven rather than left to whatever the selection code happens to do on an empty list.
  **A warded sector's own upkeep still counts against its component's pool** — warding is proven to
  reduce the component's *available* stock exactly as any other sector's would, not remove a cost.
- **Capturing a warded sector releases the binding**, and the binding cannot be recovered by either
  side afterward — proving the ward is economic, not military, protection.
- **The decay-clamp fix, proven directly**: a sector already at `MaxDecayMilli` under contagion, with a
  `Plague` surge also active the same turn, still clamps at exactly `MaxDecayMilli` — the single most
  important assertion this spec adds, given the audit finding it corrects.
- **Prospecting reveals rootbed/well/waystation slots at range**, and does not reveal anything else a
  normal scout would not already see.
- **A `Plague` month measurably worsens decay** world-wide for its duration and never touches recovery.
- **Regression**: `AbandonRuleTests`'s 100-turn and `TwoHearthsCampaignTests`'s 60-turn properties both
  still pass with every mechanic above wired in — the widest-reaching regression bar in this program so
  far, since this module touches `FadePolicy` and `LoamPhases` directly.

## Boundaries

- **Always:** every new constant harness-tuned, not guessed; `PressureMilli`/`DepletionMilli` used for
  exactly the purpose their own doc comments already anticipated; wardens extend `demon-contracts`,
  never a parallel binding system.
- **Ask first:** wiring the Unmade onto any map other than by adding an explicit `Wild` faction row —
  no implicit "everyone gets wildlife" default.
- **Never:** deep tap, scorched root, reavers, or anything on the §9.4 rejected list built as a side
  effect of implementing the six mechanics above; a second decay formula beside `FadePolicy.DecayFor`'s
  extended one.

## Success criteria

1. Granary, contagion, the Unmade, wardens, prospecting, and surges all pass their named tests above.
2. `AbandonRuleTests`'s 100-turn and `TwoHearthsCampaignTests`'s 60-turn properties still pass.
3. Every new numeric constant is harness-tuned, none hand-picked in this document.
4. `two-hearths` gains a `Wild` faction row as part of this module's own change, not a follow-up.
5. Contagion and a `Plague` surge combined, on an already-severe deficit, still clamp at exactly
   `MaxDecayMilli` — proven directly, not merely asserted, given the audit finding this corrects.
6. All five post-gate specs' new hashed fields land in one batched golden move, reason recorded once.
7. All four guard scripts green.

## Resolved (2026-08-23)

- **Granary needs a new `StructureKind.Storage`** — a genuine third thing a structure can do (raise
  capacity), not a misuse of `LoamSource`.
- **Contagion and surges both extend `FadePolicy.DecayFor` additively** rather than introducing a
  second decay formula — `PressureMilli` and the surge multiplier are two more terms in one function.
- **The Unmade need `two-hearths` to gain an explicit `Wild` faction row** — confirmed as a real,
  narrow gap (the map has none today), scoped as part of this module's own change rather than a
  separately-tracked follow-up.
- **Wardens extend, not replace, `demon-contracts`' binding capacity and loyalty machinery** — the only
  new rule is that a warden bind is permanent and never releasable.
- **Prospecting lives in the intel/belief layer, not under `World/Loam`** — keeps the loam calculators
  pure and unwired from AI/fog concerns, the same boundary held since wave 1.
- **Surge scales `DecayFor`'s pre-clamp input, never its output** (audit-caught — the original wording
  described post-clamp scaling, which would have let a surge push decay past `MaxDecayMilli`).
- **Warding exempts fade, not upkeep** — a warded sector still costs its component pool; a fully-warded,
  unpayable component applies no fade to anyone rather than crashing or silently choosing a warded
  sector anyway.
- **Capture releases a warden binding permanently** — no transfer, no refund, matching this program's
  existing "lost mid-construction" shape.

## A note on the golden-move count across this program's post-gate slice

This spec's own field (`WardenBindingId` plus a neglect-turn counter) was originally going to be
counted as its own golden move, following each of the four specs before it doing the same. An
adversarial audit (2026-08-23) caught this: `tasks/loam-plan.md` explicitly closed the golden-move
budget at **two**, and `spec-loam-maps.md` — itself pre-gate — states outright *"it is the last time in
this program."* Five post-gate specs each independently reopening that closed budget, one field
addition at a time, is exactly the kind of drift this program's own discipline exists to catch.

**Resolved, across all five post-gate specs, here rather than in each one separately**: every new
hashed field the five post-gate modules add — `loam-legions`' `WorldEntityMember.Role` and
`WorldEntity.CarriedLoam`, `structure-substrate`'s `WorldSlot.StructureId`, `loam-structures`'
`WorldSlot.ConstructionTurnsRemaining`, and this spec's `WardenBindingId`/neglect counter — ships as
**one batched golden move**, not five, following the precedent this repo already has for exactly this
situation (`docs/architecture/decisions.md`'s "Golden ordering across streams," where independent
streams land their re-blesses together rather than paying the sweep/sign-off cost once per stream).
This is the post-gate program's **third** golden move overall (after `loam-model`'s and `loam-turn`'s
pre-gate ones), not its seventh — one hash bump, taken when all five modules are ready to implement
together, not one per module as each spec was drafted. `spec-loam-legions.md`,
`spec-structure-substrate.md`, and `spec-loam-structures.md` each independently called their own field
"a golden move" (numbered third/fourth/unlabeled) before this cross-spec pass caught the conflict;
those sections are superseded by this note and should be read as "part of the one batched post-gate
move," not as separate re-blesses.
