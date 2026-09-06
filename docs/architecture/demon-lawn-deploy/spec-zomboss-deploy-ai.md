# Spec: zomboss-deploy-ai (`demon-lawn-deploy` module 3)

**Status: proposed — pending owner review. No build authorized.**

**Greenfield module.** Depends on `lawn-deploy-core` (the mechanism) and `lawn-deploy-events` (the
trigger) both existing first — this module only decides the zombie side's own *choice* once a trigger
has already fired.

## Objective

When a lawn-deploy event is available on the zombie side (from `lawn-deploy-events`), decide whether
Zomboss deploys a unique demon, and which one, without a human player to ask. **Never a Commander** —
`demon-lawn-deploy-map.md`'s own Correction (strengthen pass, 2026-09-06) rules this out for both sides,
and `commander-surface-map.md`'s own scope confirms independently that "Commander" is a player-empire-
only concept, explicitly listing "Zomboss in the player list" as out of its own scope — Zomboss never
has a Commander of its own kind to deploy in the first place. A prior draft of this spec said "a unique
demon or Commander" in its own Objective; that was a real error, not a simplification. Scope is strictly
the lawn/match timescale — not the world-map turn AI.

## Why this is not `ai-commander` reused

`spec-ai-commander.md` governs a **days-scale, turn-based, fog-of-war belief-state** policy over a
sector/lane graph (`IWorldView`, `WorldCommand`) — a different data model, a different decision cadence,
and a different determinism contract (hashed, replayed turn state) than a **seconds-scale, live-match**
decision. Reusing that module would mean bending a turn-resolution abstraction around a live-combat
tick, or bending live-combat around a turn abstraction — both worse than a small, separate policy. What
*does* carry over as precedent (style, not code): `spec-ai-commander.md`'s own Assumption 1 ("the AI is
blind on the same terms as the player, no privileged read, no handicap") and Assumption 3 ("difficulty is
which policy, not a stat bonus") are principles this module should adopt too — Zomboss choosing a unique
demon to deploy should read only what a player-observed board state would show, and difficulty should be
a different policy choice, not Zomboss quietly getting better stats. **Unlike `ai-commander`, that
principle here is enforced by prose only so far — see Correction 2 below for the fix.**

## ⛔ Corrections (strengthen pass, 2026-09-06)

1. **"Commander" removed from this module's own scope entirely.** See Objective above.
2. **"No privileged reads" needs an enforcing type, not a promise.** `spec-ai-commander.md` doesn't just
   assert its AI is blind — it makes a leak structurally impossible: `IWorldView` is the ONLY thing a
   policy touches, and leak-prone calls (`LaneGraph.Build`) were retyped so they physically cannot accept
   `WorldState` (its own Correction 3). This module's first draft only said "reads only what a
   player-observed board state would show" in prose — a promise a future call site could quietly break.
   **Fix**: this module must define its own `ILawnBoardView` (name provisional) — a narrow, read-only
   projection of live board state (visible units, HP, wave) — and the scorer's own function signature
   takes ONLY that type, never `MatchRuntime`/`Board` directly. This is now a concrete deliverable of
   this module's own build, not a design ideal to keep in mind.
3. **The deploy write path must be named explicitly.** The first draft never stated that Zomboss's own
   deploy still goes through the exact same `DeployAsync`/Funnel path a player's deploy uses — a real,
   easy-to-violate boundary now stated directly (see Boundaries) so a builder isn't tempted to give
   Zomboss a shortcut write.
4. **Zomboss's own demon roster is a named, blocking, owner-level gap — not an implementation detail.**
   Elevated out of this spec's own Open Questions into `demon-lawn-deploy-map.md`'s own "Deliberately
   deferred" section directly, so it isn't lost track of the way a spec's own open question sometimes is.

## Assumptions (surfaced, not hidden)

1. Zomboss "owns" a roster of unique demons the same shape a player does (mint/summon path unspecified —
   likely a fixed or level-scaled pool, not player-parity gacha). This needs an owner decision — see the
   map's own deferred-items list (Correction 4).
2. The policy is a deterministic scorer over visible board state (wave, plant lineup, HP) read through
   `ILawnBoardView` (Correction 2), not a learned model or a general rules engine — matching this repo's
   own rejected-alternatives list in `spec-ai-commander.md` §Assumptions 4 (no `NRules`, no Unity
   utility-AI packages, `Core` stays Unity-free).
3. A single policy id is selected per level/difficulty (mirroring `WorldFaction.PolicyId` in
   `WorldTemplateCatalog.cs`'s own shape), not a per-decision dynamic choice. Which policy id applies to
   which difficulty is a **tunable** (see Tunables below), not a hardcoded switch.

## Tunables (strengthen pass finding — the first draft named zero of these)

Per this repo's own tunables-ssot.md rule, every one of the following is a balance-surface number and
must live in a new `data/tuning/zomboss-deploy-ai.v1.json`, never a bare literal:
- The scorer's own per-feature weights (however "score this board state" is actually computed).
- The difficulty → policy-id mapping (Assumption 3).
- Zomboss's own roster size/composition, if level-scaled (Assumption 1).

## Commands

No commands beyond standard build/test until `lawn-deploy-core`/`lawn-deploy-events` land and this
module's own concrete shape is planned.

## Project structure (proposed, not confirmed)

- `src/FusionRpg.Core/Match/Ai/` (new) — `ILawnBoardView` (Correction 2) and the pure scorer type reading
  only that interface. Exact location deferred to planning once module 2 exists to depend on.
- `data/tuning/zomboss-deploy-ai.v1.json` (new) — the tunables above.

## Code style

Deterministic, hash-seeded — named precisely (strengthen pass finding; the first draft asserted
determinism without naming the seed): derive via
`SeededRng.DeriveStream(matchSeed, "zomboss-deploy-ai:{caseId}")`, the same `(seed, label)` convention
`spec-ai-commander.md`'s own `DeriveStream(worldSeed, "ai:{factionId}:{turn}")` already establishes for
an analogous need. Never `System.Random`, never wall-clock — matching `SeededRng`'s own discipline
throughout this codebase.

## Testing strategy

- Unit, with a named scenario table (strengthen pass finding — the first draft's "given synthetic board
  snapshots" was too vague): `tests/FusionRpg.Core.Tests/Match/Ai/ZombossDeployAiTests.cs` (new), cases
  covering: no eligible demon available (policy declines), exactly one eligible demon (policy picks it),
  multiple candidates at different board states (policy's own ranking is asserted, not just "it picked
  something").
- A determinism test, matching `spec-ai-commander.md`'s own explicit form: same
  `(board snapshot, matchSeed, caseId)` fed twice ⇒ byte-identical decision.
- A `ILawnBoardView`-boundary test proving the scorer's own type signature cannot compile against
  `MatchRuntime`/`Board` directly — the same kind of "the leak is structurally impossible" proof
  `spec-ai-commander.md`'s own Correction 3 established for `IWorldView`.
- No live E2E possible until modules 1-2 are real and this module has an actual roster to draw from
  (Correction 4).

## Boundaries

- **Always do**: read board state only through `ILawnBoardView` — no privileged reads, enforced by the
  type boundary itself (Correction 2), not just a comment. Always deploy through the existing
  `DeployAsync`/Funnel path (Correction 3) — never a new write.
- **Ask first**: where Zomboss's own demon roster/pool comes from (Assumption 1, Correction 4) — a real,
  unresolved, owner-level design question, not an implementation detail.
- **Never do**: give Zomboss's AI a stat or information advantage as a stand-in for difficulty — per
  `spec-ai-commander.md`'s own established principle, difficulty is which policy, not a handicap. Never
  let the scorer accept a type that could carry full `WorldState`/`MatchRuntime`/`Board` access.

## Open questions (real — blocking task breakdown, not filler)

1. ~~Where does Zomboss's own unique-demon roster come from?~~ Elevated to the map's own "Deliberately
   deferred" section (Correction 4) — still unresolved, now tracked at program level so it isn't lost.
2. Is there one policy for all difficulties, or does difficulty select among several? (Assumption 3 names
   the shape; the actual mapping is still undecided.)
3. Does this module ever need to coordinate with the world-map `ai-commander` (e.g., a legion's own
   composition influencing which demons Zomboss can field in a lawn encounter tied to that legion), or
   are the two genuinely independent? Not resolved by anything read this pass.
4. What does `ILawnBoardView` (Correction 2) actually need to expose — is "wave, plant lineup, HP"
   sufficient, or does the scorer need something more (element matchups, existing statuses)? Deferred to
   this module's own planning pass once modules 1-2 exist to test against.
