# Spec: `siege-fog`

## Objective

Give the tactical siege board hidden information: a side's own read of the board — through `IBattleView`
for decisions, through `IBoardOccupancy` for movement — shows only what that side currently has vision
on, instead of the whole board. This is the module that makes `docs/architecture/base-defense-fog-of-war-ideal.md`
(idea phase, 2026-09-06, all three open product questions resolved there by the owner) real. Success is
narrow and mechanical, not experiential: an unseen enemy is absent from `IBattleView.LiveActorKeys` and
does not block `BoardPathfinder`, for both sides, identically, with zero change to any existing
consumer of either interface.

**Non-goal, stated up front:** this module does not decide how fog LOOKS. Rendering is `siege-stage`'s
own job, later, once this mechanism exists to render.

## What already exists (verified at HEAD, 2026-09-06)

Restated from the ideal doc because a downstream session reads this spec, not that doc's own links:

- **`IBattleView`** (`src/FusionRpg.Core/Actions/IBattleView.cs`) was built for exactly this swap, from
  day one — its own doc comment names fog of war as the reason the interface exists at all, and every
  AI read already goes through it exclusively (an architecture test fails otherwise, per
  `docs/architecture/action/spec-action-selection.md:104`).
- **`BloodthirstyView : IBattleView`** (`src/FusionRpg.Core/Actions/BasicAttack.cs:247-261`) is a
  working decorator precedent — wraps an inner view, changes what it reports. This module's own
  `FoggedBattleView` is the same shape.
- **`IBoardOccupancy`** (`src/FusionRpg.Core/Battle/Board/IBoardOccupancy.cs`) is the same design again,
  independently: *"A parameter, not a fact of the board ... the pathfinder takes a view rather than
  reading `BoardState` directly."* `BoardPathfinder.Find` (`Battle/Board/BoardPathfinder.cs:48`) already
  takes occupancy as an injected `IBoardOccupancy occ` parameter. Two implementations already ship:
  `SolidOccupancy` (terrain + every real occupant) and `TerrainOnlyOccupancy` (terrain only, `siege-ai`'s
  own reachability-planning need). This module's `FoggedOccupancy` is a third sibling in the same file.
- **`LineOfFire.Trace`/`HasLineOfFire`** (`src/FusionRpg.Core/Battle/Siege/LineOfFire.cs`) is a complete,
  deterministic, canonicalized Bresenham line trace between two cells — the exact geometric primitive
  vision needs, already built and tested for `siege-obstacles`' own line-of-fire blocking.
  `siege-cover`'s own obstruction math is a separate, softer, "reduces not blocks" mechanic
  (`LineOfFire.cs:12-14`) and is not reused here — fog is a third, distinct concept from both.
- **`structure-schema`'s `See` role** (`tools/seedsmith/seedsmith/adapters/structures/anchor/schema.py`)
  is a reserved-but-unmapped content role since 2026-09-05 (`base-defense-todo.md` 23.3) — this module
  is what gives it a real mechanical effect.
- **`CombatantKind`** (`src/FusionRpg.Core/Battle/BattleModels.cs:197-206`) is a coarse, two-value
  discriminator (`Animate`/`Structure`) — verified directly, NOT a per-unit-type catalog. It cannot
  carry a per-kind vision value on its own; see §2 for what this means for v1 scope.
- **No hidden-information mechanic exists on the tactical board today.** `siege-ai`'s `Stealth` field
  (`Battle/Siege/SiegeAi.cs`) is a targeting-priority demotion on an already-fully-known candidate, a
  different thing, confirmed by reading it directly.
- **The world-map's own `FactionIntel`/`RememberedSlot` belief system** (`World/Intel/`, `World/Ai/*.cs`)
  is a proven sibling *idea* at a different scale (sectors, per-turn) — this module does not extend,
  read, or depend on it; the tactical board's per-battle cells are a separate, smaller mechanism that
  merely follows the same "restricted view over ground truth" shape.

## The contract

### 1. One shared visibility function — never two implementations of "can X see Y"

`SiegeVisibility.IsVisible(viewerSide, cell, board, ranges)` (or equivalent — naming is `/plan`'s call,
the CONTRACT is not): true when `cell` is within SOME live friendly actor or structure's own vision
range (§2) on `viewerSide`, **and** `LineOfFire.HasLineOfFire` is true between that viewer's position
and `cell`. Both `FoggedBattleView` (§3) and `FoggedOccupancy` (§4) call this SAME function — never two
divergent visibility computations that could disagree with each other.

- Pure function of board state at a point in time — no caching, no incremental update, matching
  `LineOfFire`/`GridDistance`'s own existing "recompute, don't cache" precedent on this board.
  Deterministic — same board, same inputs, same answer, forever (R5's own standing rule for anything on
  this board, restated because `siege-ai`'s R5 acceptance test already proves determinism for scoring
  and would need to keep proving it once a fogged view feeds that scoring).
- Numeric type: vision range and cell distances are small, board-bounded `int` tile counts — the SAME
  type `GridDistance.Chebyshev`/`BoardPathfinder`'s own coordinates already use. `long` is CLAUDE.md's
  rule for *magnitudes* (hp, damage, stock); a tile count bounded by board side length (authored in
  tens, per `SiegeTuning.SideByBaseTier`) is a coordinate, not a magnitude, and stays `int` — the same
  reasoning `BoardPath.TotalCost` already documents for choosing `long` where a sum is unbounded and
  `int` everywhere a value is structurally small.

### 2. Vision range — per kind for structures, one shared default for everyone else (v1 scope, named honestly)

Decision 2 (idea doc) chose "varies by kind" over one flat number, specifically so a `See`-role
structure's vision is real. Verified against code (§ above): `CombatantKind` has only two values and no
per-species/per-unit-type catalog was found for animate combatants this pass — inventing one to give
every demon and legion member its own authored vision range is real, unscoped, separate work this
module does not also try to do. **v1 scope, stated rather than silently narrowed:**

- Every `StructureDef` gains `VisionRangeTiles` (`int`, default = `fog.defaultVisionRangeTiles` — no
  structure ships with an unauthored, unreadable range by omission). A `See`-role structure authors a
  larger value than a non-`See` structure; this is the mechanical payoff the idea doc named.
- Every animate combatant uses `fog.defaultVisionRangeTiles` uniformly at v1. Authoring a per-species
  vision range is future scope for whichever module first has an animate-combatant catalog to hang it
  on — named here as a real gap, not assumed solved.

### 3. `FoggedBattleView : IBattleView`

Wraps the real view (`BloodthirstyView`'s own decorator shape). `LiveActorKeys` excludes any actor whose
own cell is not visible to the requesting side per §1. `PositionOf`/`FactsOf`/`HeldActionsOf` return the
same "unknown" sentinel `IBattleView`'s own contract already defines for an absent actor (§ its own
`PositionOf` doc: "null when no board exists yet") for anything filtered out — never a new sentinel.
Scope stops at board/roster visibility, matching `IBattleView.cs:12-15`'s own stated boundary: cost,
cooldown and stance seams are never touched by fog.

### 4. `FoggedOccupancy : IBoardOccupancy`

Wraps terrain blocking (`TerrainOnlyOccupancy`'s own check) plus ONLY occupants visible to the
pathing side per §1 (decision 3: an unseen enemy does not block a path). `BoardPathfinder.cs` itself
requires zero changes — it already takes occupancy as a parameter.

### 5. Symmetry (decision 1)

Both sides are fogged by the identical rule, using the identical §1 function. There is no
`PlayedSideId`-style special case anywhere in this module — the same discipline `SiegeIntentSource`'s
own fix already established (17.1's evidence: dispatch on plain data, never a live, one-sided read).
An architecture test should assert this directly (no branch in `SiegeVisibility`/`FoggedBattleView`/
`FoggedOccupancy` keyed on which side is asking) rather than trusting it by inspection.

### 6. Fog never changes what happens, only what a decorator's consumer can currently prove

`BoardState` (ground truth) is never mutated by anything in this module. The resolver — auto-resolve or
played-interactive alike — always computes the SAME outcome from the SAME true positions, because
BOTH sides' own AI/pathing consume the fogged decorators consistently (§5's symmetry is what makes this
safe): fog changes *what a side's own decision-making sees*, never *what the resolver computes from
truth*. This is what keeps 21.7's "one resolver path" rule intact — auto-resolve and played-interactive
sieges stay byte-identical to each other wherever both are un-watched by a human, exactly as they are
today, because fog's effect is entirely inside the read seam, never in `BattleEngine.Resolve` itself.

### 7. Out of scope, explicitly

- Rendering (dim/hidden cells, a "last known position" ghost marker) — `siege-stage`'s own job.
- Any change to `siege-waves`, `siege-cover`, or the world-map's own belief system.
- A per-species animate-combatant vision catalog (§2's named v1 simplification).

## Tunables

`data/tuning/siege.v1.json`, a new `fog` block, alongside the existing `ai`/`construction` blocks:

| Key | Meaning | Notes |
|---|---|---|
| `fog.enabled` | Rollout kill switch | Mirrors `FUSIONRPG_PERF=0`'s own precedent — not a difficulty toggle |
| `fog.defaultVisionRangeTiles` | Fallback range for any structure with no authored value, and every animate combatant at v1 | Must validate `> 0` — a zero-vision default would make the whole board permanently fogged for everyone, which is a config bug, not a design |

`StructureCatalog`'s `StructureDef.VisionRangeTiles` (§2) is content, not a tunable — authored per row
like `YieldMultiplierMilli` already is, not a global.

## Numeric types

Vision range and every distance this module computes: `int`, board-bounded tile counts — see §1's own
reasoning. No magnitude (`long`) is introduced by this module; nothing here is hp, damage, or a stock.

## Boundaries

- **Always:** compute visibility through the one shared §1 function; keep `BoardState` unmutated by
  both decorators; keep the symmetry of §5 provable by an architecture test, not by inspection.
- **Ask first:** any asymmetric-vision idea reopened later (decision 1 already closed this for v1);
  a per-animate-combatant vision catalog (real, separate scope, §2).
- **Never:** cache a visibility result across a board mutation; give the AI a live `IBattleView` read
  that bypasses `FoggedBattleView` "just this once" for convenience — that is the exact erosion
  `spec-action-selection.md:104`'s own architecture test exists to catch structurally.

## Testing

- `Fog_hides_an_actor_outside_vision_range` / `Fog_hides_an_actor_behind_a_LineOfFire_block` /
  `Fog_reveals_an_actor_inside_range_and_line_of_sight`.
- `Symmetry_is_structural` — a source/architecture scan proves neither `FoggedBattleView` nor
  `FoggedOccupancy` branches on which side is asking.
  `Same_board_produces_the_same_fog_for_either_side_by_the_same_rule` complements it behaviourally.
- `FoggedOccupancy_lets_a_path_cross_an_unseen_actors_cell` / `..._blocks_a_seen_actors_cell` —
  decision 3 proven directly against `BoardPathfinder.Find`, unmodified.
  `BoardPathfinder_requires_no_change` — an existing-signature/no-diff check, if practical, otherwise a
  stated verification note in the closing task.
- `A_See_role_structure_extends_its_sides_vision_further_than_a_non_See_structure` — the `StructureDef`
  §2 payoff proven directly, not just authored.
  `An_unauthored_structure_falls_back_to_the_configured_default` — no silent zero-vision structure.
- `Determinism_10000_times` — same shape as `siege-ai`'s own R5 test, since a live intent source will
  soon consume a fogged view and inherit its determinism requirement.
  `BattleState_is_never_mutated_by_either_decorator` — a before/after equality check.
- Full `CORE`/`DATA`/`BOUND`/`NUM` per this program's own standing verification discipline, plus a
  golden check: this module should be **golden-neutral** wherever a battle's own true outcome is
  concerned (§6) — a moved golden here is a signal the resolver, not just the read seam, changed, and
  needs its own explanation before being accepted.

## Success criteria

1. `FoggedBattleView`/`FoggedOccupancy` exist, are unit-tested against §1's shared function, and both
   `BattleRunState`/`BoardPathfinder.cs` remain unmodified.
2. Symmetry (decision 1) is proven structurally, not just by the tests that happen to be written.
3. A `See`-role structure's own vision bonus is real and authored, giving that content role its first
   mechanical purpose.
4. Swapping `siege-ai`'s eventual live intent source (17.4) from the real view to `FoggedBattleView`
   requires editing zero lines in `SiegeAi.cs`/`SiegeAiIntentSource.cs` — the acceptance test the whole
   `IBattleView` seam was originally built to make true.
5. Every played-interactive vs. auto-resolved siege pair (21.7) stays outcome-identical wherever no
   human is actually watching — fog changed a read, not a result.

## Open questions

None. The three product questions the idea doc raised (symmetry, vision-by-kind, fog-vs-pathing) were
all answered by the owner 2026-09-06 — see `base-defense-fog-of-war-ideal.md`'s own "Decisions" section
for the reasoning kept with each one. The one honestly-named gap this spec carries forward is §2's v1
scope limit (no per-animate-combatant vision catalog exists yet) — a scope statement, not an open
question: it does not block this module, and whichever future module wants richer animate-combatant
vision inherits `fog.defaultVisionRangeTiles` as its own starting point, not a blocker.

## As-built (2026-09-06)

Built the same day as spec'd, with **zero deviations from this contract** — every acceptance criterion
in §1-§6 landed exactly as written, so this section records confirmations, not corrections (unlike
`spec-siege-construction.md`'s own §11, which exists because that module's spec needed real fixes).

- §1: `SiegeVisibility.IsVisible` took a `Watcher` record struct (position + range) plus a
  `blocksVision` callback instead of the literal `(viewerSide, cell, board, ranges)` signature sketched
  in the contract — the CONTRACT (one shared pure function, both consumers call the same one) is what
  shipped; the exact parameter shape was always `/plan`'s call per this section's own opening line.
- §2: `StructureDef.VisionRangeTiles` and the `fog` tuning block shipped exactly as specified, including
  the `> 0` validation on `defaultVisionRangeTiles` and the `< 0` rejection on the per-structure field.
- §3-§4: `FoggedBattleView`/`FoggedOccupancy` shipped as the exact decorator shapes named, over the exact
  existing seams (`IBattleView`, `IBoardOccupancy`) — confirmed post-hoc via `git status --porcelain`
  that `BattleRunState.cs` and `BoardPathfinder.cs` both required zero edits, exactly as predicted.
- §5: symmetry proven structurally (reflection scan for side-specific fields/branches), not just
  behaviorally, per this section's own requirement.
- §6: golden-neutrality proven directly — `WorldWaveOneAcceptanceTests`/`WorldTwentyTurnCheckpointTests`
  both stayed at the exact same golden hash this module's predecessor task (15.4b) left them at. Zero
  goldens moved, because neither decorator is wired into any live resolver call site yet (correct at
  this stage — `siege-ai`'s 17.4 is what will eventually consume `FoggedBattleView` for real).
- Full evidence, per-task, lives in `tasks/base-defense-todo.md`'s own 30.1-30.6 — this note exists so a
  reader of this spec alone knows the module shipped, without having to cross-reference the todo file.
