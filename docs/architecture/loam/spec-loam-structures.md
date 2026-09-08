# Spec: loam-structures (wave 4)

**Status:** **Sealed 2026-08-23** — owner-approved, same authorization as the three specs before it in
this build order. Module id `loam-structures` in the [loam capability map](../loam-map.md). Depends on
`structure-substrate` and `loam-legions` (both specced). **Design source:**
[empire-economy-ideal.md](../empire-economy-ideal.md) §8.2, §8.4, §8.10, G1, G5 · this program's own
S3/S6 resolution (**superseding** several passages below — see "What the ideal gets wrong now," first).

## What the ideal gets wrong now, and must not be copied forward

Sections §8.1–§8.4 and §8.10's "does a well work when home has fallen" describe a **"loam flows along
a chain to the homeworld"** model with waystations as **anchor points measured by distance from home**,
feeding a **distance-based upkeep multiplier**. Both halves of that model are superseded by decisions
this program actually made and shipped:

- **S3/S6 (`loam-map.md`)**: loam pools **per connected component**, not per chain-to-homeworld.
  Nothing in the shipped rules reads `Flags.Home` at all — confirmed, and why `two-hearths` has two
  capitals with no special-cased "home."
- **A3 (`loam-map.md`)**: the distance multiplier on upkeep was **dropped**. Intensity already carries
  remoteness; distance was found to double-count the same intuition.

So **"waystations are anchor points for a distance-weighted upkeep formula"** does not apply — there is
no distance term left to feed. What survives, restated against the shipped model: a waystation is a
**structure that grants a sector `LoamSource` status** (habitability), on a Seat, producing no loam of
its own — exactly `structure-substrate`'s `StructureKind.LoamSource` category, nothing about distance
from a "home" this program's rules no longer recognize. §8.10's own sub-decision about "does a well
work when home has fallen" is **moot** under component pooling — there is no singular home to fall;
each component sustains itself or does not, independent of any one sector's fate. Not carried forward.

**What does survive, cleanly**: G5's range rule (a waystation must be founded within reach of ground
already anchored — a build-eligibility gate, not an upkeep term) and G1's bootstrap spend (already
specced in `loam-legions`).

## Objective

Two real structures — the **well** (multiplies a rootbed's seep) and the **waystation** (creates a
`LoamSource` on a Seat where none exists naturally) — plus the machinery both need: a range rule
gating where a waystation may be founded, and construction that takes turns and can fail partway.

Success looks like: a rootbed with a well produces visibly more than one without; a Seat sector can be
settled and made habitable purely by paying and waiting, no natural source required; that settlement
is genuinely risky — the ground can still fade and the half-built structure can still be lost before it
finishes; and the *"creep vs. leap"* choice from §8.10 is real, because both expansion modes now exist
in code, not only in one rootbed-only wave.

## Design

### Well — multiply, don't replace

`StructureDef { StructureId: "well", Kind: LoamSource, RequiredSlotKind: Rootbed, YieldMultiplierMilli:
WellYieldMultiplierMilli }`. `LoamProduction.For` (currently: `SeepPerTurn` summed per Rootbed slot,
`LoamProduction.cs`) extends to: for each slot, its base yield (unchanged — `SeepPerTurn` if Rootbed,
else `0`) multiplied by its structure's `YieldMultiplierMilli` if one is present and active (not still
under construction — below), else `1000` (unchanged). A well on a rootbed multiplies that slot's own
seep; a rootbed with no well behaves exactly as it does today — this is an additive change to
`LoamProduction`, not a rewrite, and every existing loam test that never builds a well should not need
to change.

### Waystation — a source where there was none

`StructureDef { StructureId: "waystation", Kind: LoamSource, RequiredSlotKind: Seat,
YieldMultiplierMilli: 1000 }` — the multiplier is irrelevant here since a Seat's own base yield is
already `0`; `0 × anything = 0`, matching §8.2's "produces nothing" without a special case in the
formula. What a waystation actually buys is **habitability**: `Habitability.For` extends from *"any
slot is a Rootbed"* to *"any slot is a Rootbed, or holds an **active** `LoamSource` structure."*
"Active" excludes a waystation still under construction — see below — which is exactly what makes G1's
tension real: the sector is not yet real ground while the waystation is being built.

**Resolved, an audit finding: this is not a drop-in change to both overloads as first written.** The
truth overload (`Habitability.For(WorldSector)`) can read `StructureId`/`ConstructionTurnsRemaining`
straight off the sector, no change needed beyond the field existing. The **belief** overload
(`Habitability.For(IEnumerable<string> slotTypeIds)`) takes slot type ids only — confirmed against its
actual signature — which is not enough information to know whether an active `LoamSource` structure is
present. Extending habitability correctly on the belief side means **first widening what belief
carries**: the Intel sector-snapshot's slot data needs a structure id and active/under-construction
flag added alongside the slot type it already carries, the same way `FractureIntensityMilli` was added
to belief in `loam-model`. **Resolved fog rule**: a structure and its construction state are visible on
the same terms as the slot itself (terrain-like, visible once scouted, per the precedent
`FractureIntensityMilli` already set) — not owner-only, since a structure sitting in a slot is exactly
as visible as the slot itself already is. `Habitability.For`'s belief overload signature changes to
accept this new information; every existing caller of that overload needs updating in the same change,
not left to guess the new parameter's shape.

### Construction — new state, because nothing like it exists yet

Confirmed by direct search: no "under construction," multi-turn build state exists anywhere in shipped
code today. Everything currently resolves same-turn. This module is the first thing that needs a
structure to exist *and not yet work*.

- **New**: `WorldSlot.ConstructionTurnsRemaining` (`int?`). Null means either no structure or a
  finished one; a positive value means `StructureId` is set but the structure is **not yet active** —
  it contributes to neither `LoamProduction` nor `Habitability` while this is above zero.
- **New command**: `WorldCommandKinds.Build` — a legion standing on a sector its own faction holds may
  order construction of a named structure on a compatible, empty slot. Cost (`StructureDef.Cost`, renamed off `CostMilli` world-map W57 — a whole loam unit, never a per-mille)
  is spent from the **issuing legion's own `CarriedLoam`** (`loam-legions`), not the component pool —
  this is G1's bootstrap spend exactly: the sector may have no connected pool of its own yet (that is
  the paradox G1 exists to solve), so the army's own reserves are the only thing that can pay. Sets
  `StructureId` and `ConstructionTurnsRemaining = StructureDef.BuildTurns` immediately; the slot is
  reserved from that turn even though the structure does nothing yet.
- **Each subsequent turn**, `ConstructionTurnsRemaining` decrements by one, inside the same per-sector
  pass `LoamPhases.Production` already runs. **Resolved, an audit finding: the exact ordering within
  that one pass, stated precisely rather than left to whichever way an implementation happens to write
  it.** The decrement happens **first**, then that same call's yield/habitability check reads the
  *post-decrement* value — so a structure with `BuildTurns = 3` is inert for the sector's first three
  `Production` passes (decrementing 3→2→1→0) and active starting the **fourth**, the same turn the
  counter reaches zero, not the turn after. "Completes" means "reaches zero," full stop — no extra turn
  hiding in the phrase.
- **The ground can still fade during construction** — this is the tension G1 names, not an edge case to
  prevent: an unfinished waystation grants no habitability yet, so a barren Seat under construction
  fades exactly as any other barren ground does, and the legion sitting on it may `Sustain` (the
  `loam-legions` command, spending carried loam 1:1 into the sector's `FadePolicy` balance) to keep it
  alive until the build finishes. Two `loam-legions` mechanics (carry, `Sustain`) and one
  `loam-structures` mechanic (`Build`) compose into G1's whole scenario without a new formula anywhere.
- **If the sector is lost while under construction**: **resolved, an audit finding: this needs new
  code, not a description of code that already exists.** Read directly, `LoamPhases.Pressure`'s `Lost`
  branch today only ever sets `LoamStock`/`StabilityMilli`/`Phase`/`OwnerFactionId` — it never touches
  `s.Slots` at all, so it does **not** already "ruin structures," despite `spec-loam-turn.md`'s own
  language anticipating this. This module adds the missing step: the `Lost` branch maps over the
  sector's slots and clears `StructureId`/`ConstructionTurnsRemaining` on every one, in the same
  `with` expression that already clears ownership — new code in an existing branch, not a latent
  behavior finally exercised. (This also surfaces a separate, pre-existing, out-of-scope gap the audit
  found: per-slot `OwnerFactionId` is never cleared on `Lost` either, today, for any slot — not this
  module's bug to fix, noted so a future reader doesn't mistake it for one.) A half-built waystation is
  not a refund, it is exactly the loss G1 warns the player about.
- **`Build` re-validates ownership at resolution, mirroring `ClaimResolver`.** An order admitted when
  filed can find its target sector already lost by the time it resolves later the same turn — the same
  race `ClaimResolver` already guards against (`claim.elsewhere`/entity-gone refusals). `BuildResolver`
  re-checks the founder still owns the sector at resolution time rather than trusting Reveal-time
  admission, the same discipline, not a new one.

### The range rule (G5)

**A waystation may only be founded on a Seat within `WaystationRangeHops` of a sector the founding
faction already holds that is itself currently habitable** (natural rootbed, or an *active*
`LoamSource` structure — explicitly not a sector merely held, and not one still under construction,
matching G5's own "measured from an *anchored* sector, not merely a held one"). Distance is **unweighted
hop count** — confirmed the correct choice over the march-cost-weighted alternative
(`AllPairsCost`), per `Hops.cs`'s own doc comment stating it is deliberately unweighted and therefore
distinct from that type. Computed via `Hops.Between(LaneGraph.Build(world, ...), fromSectorId,
toSectorId)`, the same graph-building call every other topology consumer in this program already uses.
`Build` refuses (with a report entry naming the sector, the same obligation every other refusal in this
program already carries) when no habitable sector of the founder's own is within range.

**Resolved, an audit finding: a faction that loses its only anchor is permanently locked out of ever
founding a new waystation, and that is accepted, not a bug.** `Rule11HomeworldHasARootbed` and
`Rule4Homeworld` only guarantee a rootbed at world *creation* — nothing exempts a homeworld from fading
to `Lost` or falling to conquest once play is underway, confirmed by direct search (no
faction-elimination or homeworld-protection logic exists anywhere in `Core/World`). A faction that
loses its sole Rootbed before founding any waystation elsewhere has zero eligible anchors and can never
found one again. **This is not carried as an unstated edge case**: it is the same philosophy this whole
program already committed to at §12.4 — *"baseline is a deficit... there is no size at which you are
comfortable"* — applied to its logical extreme. Losing your only real ground being effectively
terminal is consistent with a design that already refuses to soften the cost of losing ground anywhere
else in this program (`Lost` is not reversible by re-claiming barren ground either, per `loam-turn`'s
own settlement rule). No exemption is added.

### What this does to a real map

Per §8.10's own honest admission about `first-light`: this rule only has teeth if most sectors are not
already Seat-habitable by default. `two-hearths` is already built with sparse, deliberate rootbed
placement and long barren corridors (`spec-loam-maps.md`), so it already satisfies the shape this
module needs — no map rework required to exercise it.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Structure
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Loam
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure (proposed)

```
src/FusionRpg.Core/World/WorldState.cs                  → WorldSlot.ConstructionTurnsRemaining
src/FusionRpg.Core/World/WorldCanonical.cs               → the field into the hash — part of the batched post-gate golden move, see spec-loam-texture.md
src/FusionRpg.Core/World/StructureCatalog.cs             → well, waystation rows added
src/FusionRpg.Core/World/Loam/LoamPolicy.cs              → WellYieldMultiplierMilli, *Cost, *BuildTurns, WaystationRangeHops (harness-tuned)
src/FusionRpg.Core/World/Loam/LoamProduction.cs          → multiplier applied per active structure
src/FusionRpg.Core/World/Loam/Habitability.cs            → truth overload extended; belief overload's signature widened (structure id + active flag)
src/FusionRpg.Core/World/Intel/ (IntelRecorder.cs, IntelSeed.cs, FactionIntel.cs) → belief slot snapshot gains structure id + active flag, terrain-visible
src/FusionRpg.Core/World/Loam/LoamPhases.cs              → Production decrements ConstructionTurnsRemaining (pre-check) and reads active structures; Pressure's Lost branch maps over Slots to clear structure state (new code, not already-existing behavior)
src/FusionRpg.Core/World/Movement/BuildResolver.cs (new) → the Build command; resolves in Snapshot, same phase Claim already resolves in; re-validates ownership at resolution, mirroring ClaimResolver
src/FusionRpg.Core/World/Turn/TurnEngine.cs              → Snapshot resolves Build (confirmed the same phase Claim already resolves in — not "a new phase," that wording is resolved below)
tests/FusionRpg.Core.Tests/World/Loam/LoamStructuresTests.cs (new)
docs/architecture/decisions.md                           → the batched golden-move row (spec-loam-texture.md owns this note)
```

## Code style

Same discipline throughout: integer/`long` math, multiply-before-divide, per-mille for every
multiplier, one canonical hop-distance call (`Hops.Between`) reused rather than a second BFS.

## Testing strategy

- **A well multiplies, doesn't replace**: a rootbed with a well yields more than the same rootbed
  without one; a rootbed with no well is byte-identical to today's behavior (regression, not just new
  coverage).
- **A waystation grants habitability with zero yield**: a Seat with an active waystation is habitable
  (`Habitability.For` true) and contributes `0` production.
- **Construction is not yet real**: a slot with `ConstructionTurnsRemaining > 0` grants neither yield
  nor habitability; the sector can still fade during that window.
- **`Sustain` keeps a construction site alive** — a `loam-legions`-level integration test: a legion
  building a waystation on barren ground, `Sustain`-ing it every turn, survives to completion; the same
  scenario without `Sustain` fades and loses the half-built structure.
- **The range rule fires and declines**: `Build` refused beyond `WaystationRangeHops` from any of the
  founder's own habitable ground; accepted within it.
- **Loss during construction ruins the structure**: a sector lost mid-build has `StructureId` and
  `ConstructionTurnsRemaining` cleared the same turn ownership clears — no free partial refund. Written
  against the actual `Lost`-branch code, not the mistaken assumption it already did this.
- **The activation turn is exact**: a structure with `BuildTurns = N` is inert through its `N`th
  decrementing `Production` pass and active starting that same pass — not the turn after.
- **`Build` re-validates ownership at resolution**: an order admitted at Reveal against a sector lost to
  fade later the same turn is refused at `Snapshot`, not silently applied to a sector the founder no
  longer holds.
- **`two-hearths` needs no map changes** to exercise any of the above.

## Boundaries

- **Always:** an inactive (under-construction) structure contributes nothing to yield or habitability;
  loss during construction clears structure state in the same `Pressure` pass that clears ownership;
  `Hops`, not `AllPairsCost`, for the range rule.
- **Ask first:** any attempt to revive a distance-based upkeep term under a different name — A3 closed
  that question once already.
- **Never:** the "chain to homeworld" framing from the ideal's §8.1–§8.4 in any new code — this program
  reads no `Flags.Home`, and this module does not reintroduce a reason to.

## Success criteria

1. A well provably multiplies its rootbed's yield; an un-welled rootbed is unchanged.
2. A waystation provably grants habitability at zero yield.
3. Construction genuinely gates both effects until it completes, and can be lost mid-build.
4. The range rule fires and declines correctly against `two-hearths`, unmodified.
5. `WellYieldMultiplierMilli`/cost/build-turn constants and `WaystationRangeHops` are harness-tuned,
   not guessed — same discipline as every other number in this program.
6. `Habitability.For`'s belief overload correctly resolves active-`LoamSource` status once belief
   carries structure/construction data — not merely asserted to "extend."
7. All four guard scripts green.

## Resolved (2026-08-23)

- **The ideal's chain-to-homeworld/anchor-distance/upkeep-distance framing does not carry forward** —
  superseded by this program's own S3/S6 (component pooling) and A3 (distance dropped) resolutions.
  Only G5's range-to-build rule and G1's bootstrap spend survive, restated against what actually
  shipped.
- **Construction cost is paid from the building legion's own `CarriedLoam`**, not the component pool —
  the direct mechanical answer to G1's bootstrap paradox.
- **The range rule uses unweighted hop count (`Hops`)**, not march-cost-weighted distance
  (`AllPairsCost`) — confirmed against `Hops.cs`'s own doc comment distinguishing the two.
- **A structure under construction is inert** — no yield, no habitability — until it completes, which
  is what makes the ground-fading-during-construction tension in G1 a real risk rather than a fiction.

**Resolved after an adversarial audit (2026-08-23)**, which found five real gaps between this spec's
first pass and the code it claimed to extend:

- **`Habitability.For`'s belief overload cannot answer "active `LoamSource` present" from slot-type ids
  alone** — belief must first widen to carry structure id and active/under-construction status,
  terrain-visible, before the extension the spec originally described as a drop-in change is buildable.
- **Construction activates on its `BuildTurns`-th decrementing `Production` pass, same turn, not the
  turn after** — the original wording ("becomes active the following turn's `Production`") was
  genuinely ambiguous about pre- vs. post-decrement ordering; this resolves it exactly.
- **`Lost` does not already clear structure state — this module adds that step.** The original wording
  described existing behavior; `LoamPhases.Pressure`'s `Lost` branch, read directly, never touches
  `s.Slots` today.
- **A homeworld-loss dead-faction lockout is accepted, not patched** — consistent with this program's
  own §12.4 philosophy that losing your only real ground is meant to be close to terminal.
- **`Build` re-validates ownership at `Snapshot`**, confirmed the same phase `Claim` already resolves
  in (`TurnEngine.cs`'s `Snapshot` phase calls `ClaimResolver.Run`) — mirroring its existing
  re-validation pattern rather than trusting Reveal-time admission.
- **One batched golden move across all five post-gate specs**, not one per spec — see
  `spec-loam-texture.md`'s cross-spec note, which now owns this decision.
