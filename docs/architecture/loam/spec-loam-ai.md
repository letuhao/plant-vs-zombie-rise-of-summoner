# Spec: loam-ai (wave 3)

**Status:** **Sealed 2026-08-23** — owner-approved (all design calls resolved below, per the same
authorization as `loam-legions`: "spec and build them... clear every missing"). Module id `loam-ai` in
the [loam capability map](../loam-map.md). Depends on `loam-maps` (shipped) and `loam-legions`
(specced). **Design source:** [empire-economy-ideal.md](../empire-economy-ideal.md) §8.7, §8.10 ("what
it asks of the AI"), §10.5, §12.3 · [spec-loam-ai-survival.md](spec-loam-ai-survival.md) (the module
this one succeeds).

`spec-loam-ai-survival.md` says its own boundary plainly: *"No loam value axes, no `Sever`, no march
gate, no tuning. Those are `loam-ai`... if this module grows a second rule, it has become `loam-ai`
early."* This is that module.

## Objective

Give Zomboss (and, since §12.3 retracted the asymmetric framing, the player's own AI-driven forces —
one `ValueMap`, one weight set, for both sides) the rest of what loam-turn's economy needs an opponent
to understand: which ground is worth *keeping* versus merely worth *taking*, when cutting an enemy's
supply is worth more than expanding your own, and enough sense not to march an army to its own
starvation.

Success looks like: Zomboss stops trying to hold barren ground he cannot keep, but still takes it to
sever a chain when that is worth more; a severed enemy junction visibly hurts the side that lost it,
the same way it would hurt the player; and no AI-driven legion runs its `loam-legions` leash out by
accident — that reads as a bug, not as a character, per the ideal's own framing.

## Design

### The habitability gate — `ValueMap` gets a new multiplicative term, not a new axis

`SectorValue` (`ValueMap.cs:20`) sums six per-mille-weighted axes (Yield, Strategic, Defensibility,
Cost, Risk, Curiosity) into `total`, then applies `Overextension` as a **post-hoc multiplicative
penalty, last, large enough to drive `Total` negative** (`ValueMap.cs:99-103`) — the existing precedent
for exactly this shape of gate.

**Resolved: habitability is a second gate of the same shape, not a seventh weighted axis.** A barren
sector (per `Habitability.For`'s belief-side overload, already shipped for exactly this "read terrain
from believed slot kinds" case) has its `Total` collapsed the same way `Overextension` collapses an
out-of-supply candidate's — reusing the pattern rather than inventing a parallel one. This deliberately
affects the *general* attractiveness score that `Expand`/`Take` read, which is exactly the number that
needs to stop recommending barren ground as something worth settling.

**It does not touch `Sever`.** The severance question ("is cutting *this* enemy sector worth an attack")
is a different question from "is this ground worth keeping," so it is answered by a separate score
entirely — the next section — rather than folded into `Total` where the habitability gate would
suppress it too.

### The severance score and the `Sever` rule

`ReconnectionCost.For(sectorIds, lanes, climateOf, include:)` is already used inside `ValueMap`'s own
`StrategicByS` axis, pointed at the *viewer's own* holdings (`ValueMap.cs:138`). §8.7/§12.3's whole
insight is that the same function, pointed at an **enemy's** holdings instead, is a raid-target score
for free — no new graph code, the existing doc comment already calls this out.

**New: `SeveranceScore.For(view, targetFactionId, sectorId)`** — a sibling to `ValueMap`, not a method
on it, since it answers a structurally different question (attacking someone else's topology, not
scoring my own candidates) and keeps `SectorValue`'s six-axis shape from growing a column that means
something different from the other six.

**Resolved, an audit finding worth stating plainly: `Sever` is scouting-gated by construction, not by
accident.** `ReconnectionCost.For` gates itself to zero whenever its scope has fewer than three sectors
(`ReconnectionCost.cs`'s own guard — two sectors cannot be disconnected from each other), and
`BelievedWorldView` reads any lane with neither end currently in sight as `Open` regardless of truth.
Pointed at an enemy's *believed* holdings, both effects combine to make `SeveranceScore` read low —
often flat zero — until the viewer has scouted a meaningful chunk of that enemy's territory as
enemy-owned. **This is accepted, not patched around**: `Sever` needing real intel on the target before
it can identify a worthwhile cut is thematically consistent with this program's own prospecting theme
(`loam-texture`: *"where the prizes are is the map's central unknown"*) applied to enemies instead of
resources — an AI (or player) with poor scouting *should* be unable to find good severance targets, the
same way it cannot find hidden rootbeds. **What this rules out**: `Sever` is not a substitute for
`Explore`, and a `Sever`-heavy opponent implies a well-scouted one; a fixture testing `Sever` must scout
the target's territory first, or it is testing the zero-fog degenerate case, not the rule.

**New rule, `Sever`, in `FrontierRulesPolicy`'s chain: after `Take`, before `Recover`.** The ideal's own
words — *"sitting high, above `Expand`"* — fix the ceiling but not the exact slot; placed here because
`Take` (claiming free, undefended ground) is strictly lower-risk than initiating an attack on an
enemy-held junction, so it should still win when both are available, while `Sever`'s payoff (crippling
an enemy's supply) is worth pre-empting mere self-maintenance (`Recover`) and routine expansion
(`Explore`/`Expand`). This is a reasoned placement, not a measured one — the same shape `Abandon`'s slot
between `Defend` and `Finish` was in wave 2, settled by argument and then confirmed by tests once a
fixture existed to run them against (`AbandonRuleTests`). The updated chain:
`Defend → Abandon → Finish → Take → Sever → Recover → Explore → Expand → Hold`.

`Sever` fires when: an enemy-held sector's `SeveranceScore` exceeds a threshold (harness-tuned via a new
`SeveranceThresholdTests` — named explicitly here, matching how `loam-legions`' constants each name
their own harness rather than leaving one unnamed), the entity can reach it, and no higher rule already
claimed the entity's turn. It issues a march toward that sector — the actual
"cutting" (setting `LaneState.Severed`) is a **combat/siege outcome once the attacker takes the
sector**, not a new instant command; `LaneState.Severed` is already read everywhere movement checks
lane state (`LaneGraph.cs:129`, `MarchResolver.cs:52`) but nothing currently *writes* it, and this
module is the first thing that has a reason to.

### The march loam gate — soft for the player, hard for the AI, and they live in different files

Ideal §10.5's split is explicit and the two halves belong nowhere near each other:

- **Soft, for the player**: a projected-exhaustion figure ("this legion runs dry on turn 14") reported
  alongside an admitted march order. This is pure reporting over already-carried state
  (`WorldEntity.CarriedLoam`, `loam-legions`) and belongs in the **movement** module that resolves a
  march and writes its report entries, not in AI code — `MarchResolver.March` already spends movement
  budget and writes nothing loam-related yet; it gains one more report line, using `TurnReportEntry`'s
  `SectorId` the same way L19's barren-claim warning already does. **This is a `loam-legions` follow-up
  item, not `loam-ai`'s to build** — noting it here because this spec is what surfaces the gap, but the
  code it touches is movement/report code, not `World/Ai`.
- **Hard, for the AI, and inside `loam-ai` proper**: `FrontierRulesPolicy`'s own route selection
  (`Expand`, `Sever`, and any rule that calls `Route(...)`) must not choose a path that would run a
  legion's carried loam below zero before arrival. **This is a filter inside the AI's own candidate
  selection, not a change to `MarchResolver.Validate`** — that function is the universal
  command-admission path shared with the player, and admitting the order is exactly what the
  player-facing half of this rule requires. Two different files enforce two different rules on the same
  underlying number; that is the design, not an inconsistency to reconcile.

**Resolved, an audit finding: the check needs an actual algorithm, and "given the in-supply top-up it
would receive along the route" was naming a requirement, not specifying one.** `ReachMap.For` only
returns a single total-turn count per destination (Dijkstra over march cost) — no per-stop breakdown,
no notion of which sector the legion occupies on which turn. A correct check does not need a full
turn-by-turn position simulation, though: a route's lane path is already a known, ordered list of
sectors, and `SupplyGraph.ConnectedSectors` (computed once for the route's starting `WorldState`, not
re-simulated turn by turn — the route is short relative to how fast supply status changes, and this is
a pre-march filter, not a live tracker) already says which of them are in supply. The check partitions
the route into **contiguous out-of-supply runs** by walking that ordered list once, and requires the
legion's `Capacity` to be at least `BurnPerMember × MemberCount × (length of the longest such run)` —
the leash only needs to survive the worst single stretch beyond supply, since any in-supply sector
along the way tops the legion back toward full before the next stretch begins. This is one pass over an
already-known list, not a new pathfinding algorithm, and it is what "given the in-supply top-up along
any in-chain portion of the route" should have said outright instead of gesturing at.

### What this does to `spec-loam-ai-survival.md`'s own boundary

Nothing retroactive — `Abandon` keeps its slot (rule 2, right after `Defend`). This module adds `Sever`
at rule 5 and the habitability gate to `ValueMap`; it does not touch `Abandon`'s own logic or its tests.
The class doc comment noting the design is "rules rather than scoring... until sector-development" is
now slightly stale (this module *does* add a scoring axis, ahead of `sector-development`) and should be
updated in the same change that adds `Sever`, not left to imply the comment never anticipated this.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Ai
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Loam
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure (proposed)

```
src/FusionRpg.Core/World/Ai/ValueMap.cs                 → habitability gate, mirroring Overextension's shape
src/FusionRpg.Core/World/Ai/SeveranceScore.cs (new)      → ReconnectionCost.For pointed at an enemy faction
src/FusionRpg.Core/World/Ai/FrontierRulesPolicy.cs       → new Sever rule (chain position 5); route filtering vs. the leash
src/FusionRpg.Core/World/Movement/MarchResolver.cs       → projected-exhaustion report line (loam-legions follow-up, not loam-ai proper)
tests/FusionRpg.Core.Tests/World/Ai/SeveranceScoreTests.cs (new)          → includes the fog-degenerate (unscouted) and well-scouted cases both
tests/FusionRpg.Core.Tests/World/Ai/SeveranceThresholdTests.cs (new)      → harness for the Sever-fires threshold
tests/FusionRpg.Core.Tests/World/Ai/FrontierRulesTests.cs → Sever fires/declines cases, chain-position regression
docs/architecture/decisions.md                           → the Sever-threshold constant, once tuned
```

## Code style

Same `World/Ai` boundary this program has held since wave 2: no `WorldState` token in any file under
`World/Ai` (guard-enforced), belief only. `SeveranceScore` takes an `IWorldView`, never truth.

## Testing strategy

- **Habitability collapses `Total` for barren ground**, proven the same way `Overextension` already is
  — a fixture where a candidate would otherwise be the top pick, made barren, and shown to drop out.
- **Severance survives the habitability gate**: a barren junction sector still scores high on
  `SeveranceScore` even though its `ValueMap.Total` has collapsed — the two are provably independent.
- **Severance under realistic fog**: a fixture where the enemy's territory is *mostly* unscouted proves
  `SeveranceScore` reads near-zero there (the accepted, scouting-gated behavior), and a second fixture
  where the target and its neighbours are scouted proves the score responds once the intel exists — both
  cases asserted, not just the convenient one.
- **`Sever` fires** against a genuine enemy articulation point and **declines** against enemy ground
  that is not a cut point (mirrors the W35 "a rule proven only by firing might fire always" discipline
  every rule in this chain already follows).
- **Chain position**: `Take` still wins over `Sever` when both are available in the same turn; `Sever`
  still wins over `Recover`/`Explore`/`Expand`.
- **The march loam gate, AI side**: a route that would exhaust a legion's leash before arrival is never
  chosen by `Expand` or `Sever`, even when it is otherwise the best-scoring candidate. Specifically: a
  route with one long out-of-supply stretch beyond the leash's reach is refused even when a *shorter*
  alternative route (more hops, but through supply for part of it, breaking the stretch into two shorter
  ones) would succeed — proving the check measures the worst contiguous run, not total route length.
- **Regression, not just new coverage**: `AbandonRuleTests`'s 100-turn survival test and
  `TwoHearthsCampaignTests`'s 60-turn campaign both still pass with `Sever` and the habitability gate
  live — a new rule earlier in the chain changing what fires for existing fixtures is exactly the kind
  of thing a rule-ordering change can silently break.

## Boundaries

- **Always:** `World/Ai` reads belief only, no exceptions; `Sever`'s exact chain position stated with
  its reasoning, not left implicit; the habitability gate reuses `Overextension`'s shape rather than
  inventing a second one.
- **Ask first:** any change to `Abandon`'s existing chain position or logic; folding severance into
  `SectorValue`'s six axes instead of keeping it a separate score.
- **Never:** a hard march refusal for the player (that is `WorldCommandAdmission`'s job and this spec
  explicitly keeps it soft); a second `ValueMap`/weight set for Zomboss (§12.3 closed that).

## Success criteria

1. Barren ground's `Total` collapses; severance score for the same sector does not.
2. `Sever` fires exactly where a genuine cut point exists, declines elsewhere, at chain position 5.
3. No AI-chosen route ever exhausts a legion's `loam-legions` leash before arrival.
4. `AbandonRuleTests`'s 100-turn and `TwoHearthsCampaignTests`'s 60-turn properties both still pass.
5. All four guard scripts green, including the `World/Ai`-reads-belief-only guard.

## Resolved (2026-08-23)

- **Habitability is a gate on `Total`, mirroring `Overextension`** — not a seventh weighted axis.
- **Severance is a separate score (`SeveranceScore`), not folded into `SectorValue`** — so the
  habitability gate cannot accidentally suppress it.
- **`Sever`'s chain position: 5th, after `Take`, before `Recover`** — reasoned from risk/payoff
  ordering, to be confirmed by fixtures the way `Abandon`'s placement was.
- **The march loam gate splits across two modules**: the soft, player-facing report belongs to
  `loam-legions` (movement/report code); the hard AI-side route filter belongs here, in
  `FrontierRulesPolicy`. Noted as a `loam-legions` follow-up item since that module is already sealed.

**Resolved after an adversarial audit (2026-08-23)**, which found two claims this spec made without
the algorithm to back them:

- **`SeveranceScore`'s fog behavior is accepted by design, not a bug** — it reads near-zero without
  real scouting of the target, matching this program's own prospecting theme rather than a defect to
  patch around.
- **The march loam gate's AI-side check is a single pass over the route's contiguous out-of-supply
  runs**, requiring `Capacity ≥ BurnPerMember × MemberCount ×` the longest run — not a full turn-by-turn
  position simulation, which nothing in shipped code (`ReachMap.For` included) can currently support.
- **`Sever`'s threshold gets its own named harness**, `SeveranceThresholdTests`, matching the naming
  discipline every other tuned constant in this program already follows.
