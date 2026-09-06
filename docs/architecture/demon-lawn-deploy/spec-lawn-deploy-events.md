# Spec: lawn-deploy-events (`demon-lawn-deploy` module 2)

**Status: proposed — pending owner review. No build authorized.**

**Greenfield module — flagged plainly.** Unlike `lawn-deploy-core`, no existing trigger/condition
evaluator was found for the live PvZ lawn. `AmbushDraw` (`src/FusionRpg.Core/Delve/Events/AmbushDraw.cs`)
is the nearest analog in shape, but it is Delve-only and its own header says it is "partially built —
nothing in Delve/Attrition calls this yet." `demon-system-map.md`'s "Ecology / blood moon / roaming" row
names an intended "run modifiers" concept for the lawn but it is unbuilt (`docs/guide/mechanisms/
world-events.md` states plainly: "Vision … Not finished / not playable yet"). Everything below is
design, not a discovered wiring gap — treat every claim as an assumption to confirm, not a citation.

## Objective

Decide, during a live lawn run, *when* a unique demon deploy becomes available — for the plant
(player-facing) side. **Never a Commander** — `demon-lawn-deploy-map.md`'s own Correction (strengthen
pass, 2026-09-06) rules that out entirely; a prior draft of this spec asked whether the deployed demon
is "always the roster's Commander" as an open question, which is now answered: no, by design, a
Commander/Patron-designated specimen is refused at `lawn-deploy-core`'s own layer regardless of what this
module decides. Depends on `lawn-deploy-core` existing (the deploy mechanism itself) — this module only
gates access to it.

## ⛔ Corrections (strengthen pass, 2026-09-06)

1. **"Commander" removed from this module's own scope.** See Objective above — this was a real,
   self-contradicting error in the first draft, not a wording nit.
2. **Ownership checks need a Hot/Cold-safe snapshot, not a live Cold-plane read mid-tick.** Assumption 1
   below gates availability on "a deploy the player already owns the demon for" — but a roster is
   Cold-plane data, and the trigger evaluator is specced (Assumption 2) as a pure function over live
   match state, which is Hot. Reading Cold-plane roster ownership inside a Hot-path tick is exactly the
   "no server round-trip on the hit path" violation `demon-system-map.md:36` already forbids. **Fix**:
   the roster (or just the subset eligible to deploy) is snapshotted once, at `board.start`, the same
   Hot/Cold discipline `commander-surface-map.md`'s own program-level acceptance already establishes for
   an unrelated but structurally identical problem (a commander's aura read once at match start, never
   re-polled mid-run). This module's own trigger evaluator reads only that frozen snapshot, never a live
   query.

## Assumptions (surfaced, not hidden)

1. A "case" is a run-scoped condition evaluated against live match state (wave number, sun/soul economy,
   HP thresholds, a specific level's own scripted moment) — not a persistent, cross-run economy system.
   This module does not touch souls/pity/summon economy; it only decides *availability* of a deploy the
   player already owns the demon for, checked against the Correction-2 snapshot.
2. The trigger check runs Hot (during the match, off `MatchRuntime`'s own tick or event stream) — never a
   server round-trip on the hit path, matching the hard constraint `demon-system-map.md:36` already
   states for all in-run demon behavior.
3. The plant-side UI surface (a prompt, a HUD affordance) is a **new** FE concern, not an extension of
   an existing screen. `commander-surface-map.md`'s own "creature deploy berths / T21 (plate 07)" is a
   distinct, **pre-run** squad-ceremony concept (checked directly — not a duplicate of this module's own
   **mid-run** trigger), so it is not reusable here as-is, though its FE conventions (how a berth picker
   presents demon choices) may be worth matching for visual consistency — a design nicety, not a
   dependency.
4. Trigger conditions, costs, and frequencies are **tunable**, not hardcoded — see the new Tunables
   section below. This corrects the first draft, which left every numeric knob as bare prose.

## Tunables (strengthen pass finding — the first draft named zero of these)

Every numeric knob below is a real balance-surface number per this repo's own tunables-ssot.md rule
("would a balance pass ever want to change this?") and must live in a new
`data/tuning/lawn-deploy-events.v1.json` once designed, never a bare literal in code:
- Trigger frequency/cadence (how often a case can fire per run).
- Any HP/wave/economy threshold that defines a case.
- The cost (if any — see Open Question 2) charged per trigger.
- A per-run trigger budget, if capped.

## Commands

No commands beyond standard build/test until a concrete implementation shape is chosen — this module
needs its own planning pass once `lawn-deploy-core`'s approach is confirmed built.

## Project structure (proposed, not confirmed)

- `src/FusionRpg.Core/Match/` — a new, small condition-evaluator type, matching `AmbushDraw`'s own shape
  (pure function over match state + the Correction-2 roster snapshot → yes/no + which case), not a rules
  engine (this repo has already surveyed and rejected general rules engines for replay-determinism
  reasons — `spec-ai-commander.md`'s own §Assumptions 4 names `NRules` rejected for exactly this; the
  same reasoning applies here).
- `data/tuning/lawn-deploy-events.v1.json` (new) — the tunables above.
- A new REST/SignalR surface for the plant-side prompt (exact shape deferred to planning).

## Code style

Match `AmbushDraw.cs`'s own pure-function-over-state shape. **Determinism, named precisely (strengthen
pass finding — the first draft only asserted "deterministic given a match seed" without naming the seed
input)**: derive the evaluator's own stream via `SeededRng.DeriveStream(matchSeed, "lawn-deploy-event:{caseId}")`
— the exact `(seed, label)` convention `SeededRng.cs:26` already establishes and `spec-ai-commander.md`'s
own `DeriveStream(worldSeed, "ai:{factionId}:{turn}")` already uses for an analogous need — never
`System.Random`, never wall-clock.

## Testing strategy

- Unit, with a named scenario table (strengthen pass finding — the first draft's "does the right case
  fire" was too vague to be a real test plan): `tests/FusionRpg.Core.Tests/Match/LawnDeployEventsTests.cs`
  (new), one fact per named case × {fires, does-not-fire-below-threshold, does-not-fire-if-already-used-
  this-run} — mirroring `spec-ai-commander.md`'s own per-rule fire/no-fire matrix, not a single smoke test.
- A determinism test: same `(matchSeed, caseId)` twice ⇒ byte-identical decision, matching
  `spec-ai-commander.md`'s own explicit determinism assertion shape.
- No live E2E possible until `lawn-deploy-core` is real.

## Boundaries

- **Always do**: keep the trigger check Hot and off the hit path, reading only the Correction-2 snapshot
  for ownership, never a live Cold-plane query mid-tick.
- **Ask first**: the exact trigger conditions themselves (frequency, cost, which waves/levels) — this is
  game-design/balance, not an architecture question this spec can answer, and belongs in the new tunable
  file once named.
- **Never do**: gate this on a new server round-trip inside a live match tick. Never read Cold-plane
  roster data directly from the trigger evaluator — always through the frozen snapshot (this replaces
  the first draft's redundant restatement of the same "Hot path" rule under both Always/Never; this is
  the one that actually adds a distinct constraint).

## Open questions (real — this module cannot be planned further without owner input)

1. What are the actual trigger conditions? ("in some cases" — named but not specified.)
2. Is there a cost (souls, a cooldown, a limited-uses-per-run budget) to triggering a deploy, or is it
   free once the condition fires?
3. ~~Does the player choose *which* owned demon deploys, or is it pre-selected (e.g., always the
   roster's Commander)?~~ **Resolved by Correction 1**: never a Commander. Still open: does the player
   choose among their eligible roster, or is a specific demon pre-selected some other way?
4. Where exactly does the Correction-2 roster snapshot live, and who owns refreshing it — is this a new,
   dedicated snapshot, or does it belong alongside whatever `commander-surface-map.md`'s own
   `match-snapshot`-shaped module (if/when authorized) already builds for the same Hot/Cold problem?
