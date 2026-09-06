# Fog of war (siege board) — the ideal

**Status:** idea phase, 2026-09-06. Not a spec. No build authorized. Folds into `base-defense` as a
module once specced — the same "one map, one plan, one todo pair" precedent decision 45 already set
for `structure-seed` — not a separate program.

**Why this doc exists now:** reopened by owner decision against this session's own recommendation to
leave it deferred (see `base-defense-program.md` memory / session record). The owner's own later
instruction was explicit: *"if we don't have plan or spec for them we will enrich them"* — fog of war
is the one item in the whole 29-module program's remaining backlog with neither. Everything else
already has a spec; this doc is what makes that no longer true.

**Principles restated, not linked** (`docs/DESIGN-GATE.md`, `AGENTS.md`): read → verify against code →
propose, never the reverse. A comment is not evidence — every claim below was checked against the
actual file it cites. "Does the lawn/board support X" is almost always the wrong question — the right
one is whether a mechanism already exists and is simply unwired. As the inventory below shows, this
feature is the rare case where the *opposite* risk applies: the seam was built two modules ago,
correctly, for exactly this day, and the risk is re-deriving it from scratch instead of reading it.

## What this is

Fog of war on the tactical siege board: each side's read of the board — `IBattleView`, the one seam
every AI decision and (eventually) every render already goes through — shows only what that side
currently has eyes on. A besieging or defending player sees a live, true picture of ground within
vision, nothing beyond it. This is a new, small, tactical-board-scoped mechanism; it does not touch or
extend the world-map's own, already-shipped, sector-scale "belief" system (see below).

## What already exists

**Built** — the load-bearing precedent for treating this as low-risk, not speculative:

- **`IBattleView`** (`src/FusionRpg.Core/Actions/IBattleView.cs:5-16`) was designed for this from day
  one. Its own doc comment: *"T33 (spec-action-selection.md §4): the read seam fog of war will swap —
  'IBattleView from day one, even while it returns everything.' ... under fog, 'nearest target' becomes
  'nearest KNOWN target' ... this interface is what confines that change to one implementation later."*
  `docs/architecture/action/spec-action-selection.md:48-54,119` restates the same intent even more
  plainly: *"Fog of war is deferred, not refused ... With the seam, fog is later an implementation swap
  behind one interface. Without it, it is an AI rewrite. The interface costs one indirection now."*
- **Every AI read already goes exclusively through that seam**, enforced structurally, not by
  convention: `spec-action-selection.md:104` — *"an architecture test failing if `StubIntentSource`
  references the battle state directly."* `siege-ai`'s live intent source (17.4, currently being built
  against the real, un-fogged view) will need **zero changes** when fog lands — it was built to only
  ever see through the swappable seam, on purpose, two modules before this one exists.
- **A decorator precedent already proves the "wrap `IBattleView` and filter" shape works in this exact
  codebase**: `BloodthirstyView : IBattleView` (`src/FusionRpg.Core/Actions/BasicAttack.cs:247-261`)
  wraps an inner view and reorders `LiveActorKeys`. A `FoggedBattleView` would be the same shape,
  filtering instead of reordering — new code, not a new pattern.
- **The hardest usual part of fog of war — line-of-sight geometry — is already built, tested, and
  deterministic**: `LineOfFire.Trace`/`HasLineOfFire`/`CanFire`
  (`src/FusionRpg.Core/Battle/Siege/LineOfFire.cs`) is a complete Bresenham line trace between two board
  cells, canonicalized so `Trace(a,b)` and `Trace(b,a)` compute identically, built for `siege-obstacles`'
  own hard line-of-fire blocking. "Is there an unobstructed line from a viewer to this cell" is the same
  question, answered by the same function.
- **`siege-cover`'s own obstruction math is a different, named mechanic** — "reduces, never blocks," a
  soft per-shot penalty (`docs/architecture/base-defense/spec-siege-cover.md:116-120,361-365`), and
  `LineOfFire.cs:12-14`'s own doc comment already draws this line explicitly: *"Deliberately independent
  of `siege-cover`'s own obstruction math (a DIFFERENT, softer mechanic ... This is a hard yes/no."*
  Fog of war is a **third**, again-different concept (hides information entirely, rather than reducing a
  hit chance or blocking a shot) and must not be folded into either existing mechanic.
- **The same shape already ships at the world-map scale.** `World/Intel/` (`FactionIntel.cs`,
  `IWorldView.cs`, `IntelRecorder.cs`, `IntelSeed.cs`), `RememberedSlot`, and "belief" overloads across
  `World/Ai/ThreatMap.cs`, `ValueMap.cs`, `BelievedSupply.cs`, `World/Loam/Habitability.cs` are a
  complete, closed "a faction's own stale/partial view, distinct from ground truth" system —
  `Habitability.cs:17-20`'s own doc comment uses the word directly: *"not owner-only, so this never
  needs ownership or fog beyond what the caller already scoped."* Tactical fog is a sibling of an
  already-proven idea at a different scale, not an unprecedented new paradigm — though it is a
  genuinely separate, smaller mechanism (per-battle cells, not per-turn sectors), not a literal reuse of
  `RememberedSlot` itself.
- **The content schema already reserves a role for this.** `structure-schema`'s own
  `ROLE_TO_STRUCTURE_KIND` (`tools/seedsmith/seedsmith/adapters/structures/anchor/schema.py`, evidenced
  in `base-defense-todo.md` 23.3) names `See` as one of five roles with no `StructureKind` yet — a
  vision/detection structure has been a reserved, intentional content slot since 2026-09-05, with no
  mechanic to give it meaning. Fog of war is what makes `See` real.
- **`siege-ai`'s existing `Stealth` field is not fog of war, and should not be confused with it.**
  `Battle/Siege/SiegeAi.cs` (tested by `Stealth_demotes_and_taunt_promotes_through_the_same_field`) is a
  targeting-*priority* demotion on an already-fully-known candidate — not hidden information. No real
  hidden-information mechanic exists on the tactical board today.

**Wiring gap** — none, in the usual sense. The seam is real, live, and already the AI's only read path.
The gap is simply that its one implementation, `BattleRunState`
(`src/FusionRpg.Core/Battle/BattleRunState.cs:35`), "returns everything" per `IBattleView`'s own framing
of its current state — there is nothing inert to switch on; there is a second implementation still to
write.

**Real gap** — nothing exists yet for:
- Vision-range computation (nothing today decides how far a unit or structure can see).
- Per-side "what does this side currently know" state during a battle (the tactical scale has no
  equivalent of the world map's `RememberedSlot` — deliberately not proposed as a literal reuse, see
  above).
- Rendering (`siege-stage`, module 21, has no fog/dim-cell concept yet — moot until its own FE is
  further along, named here only so a future `siege-stage` task does not rediscover this from scratch).
- Movement/pathing interaction — see open question 3 below. `BoardPathfinder` (17.4's own dependency)
  has not been read with this question in mind.

## Prior art

- **XCOM 2's concealment**: every unit sees a flat **17 tiles**, regardless of unit type; weapons often
  outrange vision; a single **symmetric** rule — "if you can see them, they can see you." Concealment
  (not merely fog, a stronger *unseen entirely* state) lasts up to **~10 turns** before an enemy
  patrol's own vision crosses the party's position.
- **Fire Emblem's fog of war** (since *Thracia 776*) has a well-documented, widely-criticized failure
  mode: **the enemy AI can see through the fog while the player cannot** — an asymmetric, AI-favoring
  rule that reads as *artificially* harder rather than more tactical. **This is the one mistake this
  feature must not repeat.** Any asymmetry this program ships must be a stated, thematic,
  player-*knowable* design choice (e.g., "a dug-in defender sees further than an approaching attacker"),
  never an accidental AI-only information advantage.
- **Into the Breach** takes the opposite stance: perfect information, zero fog — all tension instead
  comes from fully telegraphed enemy intent. Worth naming because `siege-ai`'s own existing design
  already leans this direction on the *decision* side: 17.5-17.7 (`spec-siege-ai.md`) already commit to
  a deterministic, auditable, no-hidden-difficulty-thumb AI with a named, player-visible target-validity
  filter. Fog of war (hiding *where* things are) and a transparent, provable AI (never hiding *why* it
  decides what it decides) answer different questions and do not conflict — but this doc only ever
  proposes to hide board state, never decision logic, so a future spec should not quietly walk back
  17.5-17.7's own commitments in fog's name.

Sources:
- [Squad Tactics (XCOM 2) — XCOM Wiki](https://xcom.fandom.com/wiki/Squad_Tactics_(XCOM_2))
- [Fog of war — Fire Emblem Wiki](https://fireemblemwiki.org/wiki/Fog_of_war)
- [Fog of War (Advance Wars and Fire Emblem) — Atrocious Gameplay Wiki](https://atrociousgameplay.miraheze.org/wiki/Fog_of_War_(Advance_wars_and_Fire_Emblem))
- [Reimagining failure in strategy game design in Into the Breach — Game Developer](https://www.gamedeveloper.com/design/reimagining-failure-in-strategy-game-design-in-i-into-the-breach-i-)

## The shape

Proposed, for `/spec`, now that the three decisions above are settled:

- **One shared visibility computation, consumed by two decorators — never two implementations of "can
  side X see cell/actor Y."** A cell is visible to a side when it is within that VIEWER's own
  vision range (decision 2: read from the viewer's combatant/structure kind, not one flat number) of a
  live friendly actor/structure on that side, **and** `LineOfFire.HasLineOfFire` is true between viewer
  and cell — reusing the existing deterministic geometry rather than a second implementation.
- **`FoggedBattleView : IBattleView`** (`BloodthirstyView`'s own decorator shape) wraps the real view,
  filtering `LiveActorKeys`/`PositionOf`/`FactsOf`/`HeldActionsOf` down to what the requesting side can
  see, per the shared computation above. Scope strictly matches `IBattleView`'s own contract:
  board/roster visibility only — cost/cooldown/stance seams (`IBattleView.cs:12-15`'s own stated
  boundary) are untouched by fog.
- **`FoggedOccupancy : IBoardOccupancy`** (`SolidOccupancy`/`TerrainOnlyOccupancy`'s own sibling shape,
  `Battle/Board/IBoardOccupancy.cs`) reports a cell blocked by terrain or a KNOWN occupant only —
  decision 3's "fog affects pathing too": an unseen enemy does not block a path until the shared
  visibility computation says it is seen. `BoardPathfinder.Find` itself needs no change; it already
  takes occupancy as an injected parameter, exactly as `IBattleView` is already the AI's sole injected
  read.
- **Fog changes what a side can currently prove, never what is true.** The board's own ground truth
  (`BoardState`) is unchanged; `FoggedBattleView`/`FoggedOccupancy` are read-time filters over it. This
  keeps auto-resolve and played-interactive siege on the SAME resolver path (21.7's own "one resolver
  path" rule) as long as BOTH sides' decision-making (AI included) goes through the fogged views
  consistently — fog is symmetric (decision 1) specifically so neither side's resolution logic needs a
  special case.
- **Sequencing is already right, not something to re-litigate**: build `siege-ai`'s real intent source
  (17.4) now, against the full un-fogged view, exactly as `spec-action-selection.md` intended. The
  fogged decorators swap in later with zero changes to 17.4's own code or to `BoardPathfinder.cs` — that
  is the entire point of both seams existing first.

**Rejected alternative**: baking visibility checks directly into `BattleRunState`/`BoardPathfinder`
themselves instead of decorators. Rejected because it would touch the two classes every other battle
and movement feature also depends on, for a feature both interfaces' own boundaries were deliberately
built to keep at arm's length — and the decorator shape already has a working precedent
(`BloodthirstyView`) that touches zero existing files.

## Tunables

All in `data/tuning/siege.v1.json`, beside the existing `ai`/`construction`/board blocks (the balance-
pass test from `tunables-ssot.md`: every one of these is a number a balance pass would want to change):

- `fog.defaultVisionRangeTiles` — the fallback range for any combatant/structure kind that doesn't
  author its own (decision 2: per-kind, not flat — but a fallback still needs a value, so no new kind
  ships with an unreadable, undefined range by omission).
- `fog.structureVisionBonusTiles` (or a per-`StructureDef` `VisionRangeTiles` field, `/spec`'s call) —
  the `See`-role structure's own extended vision.
- `fog.enabled` — a rollout kill switch (mirroring `FUSIONRPG_PERF=0`'s own precedent elsewhere in this
  repo), not a permanent difficulty toggle.

## What this deliberately does not decide

- Any UI/rendering treatment — `siege-stage`'s own job, later, once the mechanism itself exists to
  render.
- Whether `siege-waves` or any other already-closed module ever consumes vision — this doc scopes only
  the mechanism and its first real consumer, `siege-ai`.
- Anything about the world-map's own `FactionIntel`/`RememberedSlot` belief system, which this feature
  does not extend, replace, or depend on.

## Decisions (owner, 2026-09-06)

1. **Vision is symmetric.** Both sides see equally far, under the same rule — the Fire Emblem
   AI-omniscience mistake never has a foothold because there is no asymmetric case to accidentally get
   wrong.
2. **Vision range varies by kind, not a flat number.** A `See`-role structure (`structure-schema`'s own
   reserved-but-unmapped role) grants its side extended vision; other kinds carry their own range. This
   is real, additional content-authoring surface (every combatant/structure kind now needs a vision-range
   value, not one shared constant) — named here so `/spec` scopes it honestly rather than as a one-line
   tunable.
3. **Fog affects pathing, not only information.** An unseen enemy does not block a path until spotted —
   true hidden information, not a read-only filter over an always-fully-resolved board. This is
   real, additional scope beyond the minimal decorator-only shape first proposed below: `BoardPathfinder`
   itself needs a fog-aware `IBoardOccupancy`, not just a fog-aware `IBattleView`.

**What decision 3 actually costs, found while re-reading `BoardPathfinder` against this exact
question**: less than it first sounds like, because the SAME "swappable view, not a fact of the board"
architecture already used for `IBattleView` turns out to already cover pathing too.
`BoardPathfinder.Find` (`src/FusionRpg.Core/Battle/Board/BoardPathfinder.cs:48`) takes occupancy as an
injected `IBoardOccupancy occ` parameter, never reads `BoardState` itself — and `IBoardOccupancy.cs`'s
own doc comment (`:5-8`) states the identical design intent `IBattleView`'s does: *"A parameter, not a
fact of the board — a unit standing in a doorway blocks it now and will not next round, so the
pathfinder takes a view rather than reading `BoardState` directly."* Two implementations already ship
side by side in that same file: `SolidOccupancy` (terrain + every real occupant blocks — actual
movement) and `TerrainOnlyOccupancy` (terrain only, for `siege-ai`'s own "can I ever get there in
principle" planning question). A third sibling, `FoggedOccupancy` (terrain + only KNOWN occupants
block, from one side's own vision), is the same shape again — `BoardPathfinder.cs` itself needs zero
changes, exactly like `BattleRunState`/the AI needing zero changes for `FoggedBattleView`.
