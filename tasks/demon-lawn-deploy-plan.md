# Implementation plan: demon lawn deploy

Covers all three module specs under `docs/architecture/demon-lawn-deploy/` (`spec-lawn-deploy-core.md`,
`spec-lawn-deploy-events.md`, `spec-zomboss-deploy-ai.md`) and the capability map they sit under
(`docs/architecture/demon-lawn-deploy-map.md`). All four documents went through an adversarial
strengthen pass (2026-09-06, four independent reviews) before this plan was written — see each spec's
own "⛔ Corrections" section for what was found and fixed. This plan does not re-litigate those findings;
it schedules the work they produced.

## Overview

Let a player-owned unique demon (never a Commander or Patron-designated one) deploy onto the live PvZ
lawn during triggered events, on both sides — the plant side player-initiated, the zombie side AI-driven.
Three modules, strictly sequential: the deploy mechanism itself, the trigger layer that decides when it's
available, and the Zomboss policy that decides what it does with an available trigger.

## Architecture decisions (locked in the specs — restated so the task list is readable)

- **A demon specimen deploys through the existing `POST /api/unique/actors/{id}/deploy` pipeline
  unchanged at the plumbing level.** No new Injector code. The only new work is server-side content
  resolution (trait bindings) and two refusal/decision points (Commander/Patron, `DeployMode`→side).
- **Binding is a reconciled diff, run on every deploy — never a mint-time snapshot.** A specimen's traits
  can change in place after mint (promotion), so re-binding must happen at the one place that's certain
  to run before combat: deploy time.
- **Commander and Patron never deploy, full stop.** `demon-system-map.md`'s own Axis 2 (2026-09-06) is
  unambiguous and this plan does not reopen it.
- **Every numeric knob is tunable.** Trigger conditions/costs (`data/tuning/lawn-deploy-events.v1.json`)
  and the Zomboss scorer's weights/roster (`data/tuning/zomboss-deploy-ai.v1.json`) are real balance
  surfaces, never bare literals.
- **Determinism is named, not asserted.** Both AI-adjacent modules derive their randomness via
  `SeededRng.DeriveStream(matchSeed, "<module>:<id>")`, the same convention `spec-ai-commander.md`
  already established — never `System.Random`, never wall-clock.
- **Reuse existing ownership infrastructure, don't re-derive it.** `decisions.md`'s "Buff/debuff scope"
  row (2026-08-29/30) already shipped `SpecimenOwnershipOracle` — resolves which player owns a spawned
  board ptr, built specifically so a hypnotized/side-swapped entity still resolves to its real owner.
  `zomboss-deploy-ai`'s own `ILawnBoardView` should read through this oracle for side/ownership
  questions, not invent a second one.

## Gates vs. checkpoints — read before objecting to what's NOT gated here

Two real, owner-level open questions exist in the specs (the `side`-column mutation approach, and
Zomboss's own demon-roster source). **Neither is a pre-work gate in this plan.** Per this repo's own
rule (a hard gate is reserved for a genuinely irreversible action with no answerable default; everything
else ships behind a reversible/tunable default, tracked as a non-blocking follow-up): both open questions
get a stated default below, and the build proceeds. If the owner later prefers a different answer, it's
a follow-up task, not a rebuild — nothing about these defaults locks in an irreversible schema choice.

- **`side`-column decision (spec-lawn-deploy-core Open Q4)**: default is **update the column at
  `HypnoAlly` deploy time** (T1.4) — the more physically-true representation, matching what
  `/api/actors/{id}/derived` already assumes elsewhere. Reversible: a later decision to make that
  endpoint `DeployMode`-aware instead is a small, isolated change.
- **Zomboss's own demon roster (zomboss-deploy-ai Open Q1, elevated to the map's own deferred list)**:
  default is **the same summonable species pool the player draws from, filtered to the current level's
  own threat band** (T3.2) — no new content authoring, no new roster table. Reversible: a dedicated
  Zomboss-only pool is a later, additive change if the default plays poorly.

## Dependency graph

```
T1.1 (Commander/Patron refusal) ──┐
T1.2 (reconcile-diff binding)  ───┼──> Checkpoint 1 ──> T2.x (events) ──> Checkpoint 2 ──> T3.x (zomboss AI) ──> Checkpoint 3
T1.3 (overflow-safety check)  ────┤
T1.4 (side-column decision)   ────┤
T1.5 (species-magnitude path) ────┤
T1.6 (live E2E)               ────┘
```

T1.1-T1.5 can build in parallel (independent files/concerns); T1.6 depends on all of them landing. T2.x
cannot start meaningfully until Checkpoint 1 (a demon can actually deploy with real stats). T3.x cannot
start until Checkpoint 2 (a real trigger exists to hang a Zomboss decision on).

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| `AtomCompiler.cs`'s `int`-typing silently narrows a `long` demon magnitude | High — silent stat corruption, exactly what CLAUDE.md's overflow rule exists to prevent | T1.3 runs BEFORE T1.2's binding logic is considered final — a real investigation task, not an assumption |
| A promoted demon's stale binding ships anyway because the diff logic is untested against the promotion case specifically | High — silent, invisible balance bug | T1.2's own acceptance requires a promotion-then-redeploy test, not just a first-deploy test |
| Zomboss's default roster (T3.2) makes for a degenerate/unfun AI on release | Medium, but explicitly not a build blocker | Named as a tunable default with an explicit "reversible" note above — ship, observe, retune |
| `lawn-deploy-events`' trigger conditions are pure design/balance guesses | Medium | Same "pick starting values, tune from play" precedent this repo has already used successfully elsewhere this session (T2.11's own phased-rollout decision) |

## Verification commands (run after every task, full suite after each checkpoint)

```powershell
dotnet test tests/FusionRpg.Data.Tests --filter DemonLawnDeploy
dotnet test tests/FusionRpg.Core.Tests --filter "LawnDeployEvents|ZombossDeployAi"
dotnet test tests/FusionRpg.E2E.Tests --filter DemonLawnDeploy
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-dal.ps1
python scripts\audit-overflow.py
python scripts\audit-magic-numbers.py --summary
```

## Owner-only steps

- The live-lawn E2E checks in Checkpoints 1 and 3 (server restart + real game launch) — matching this
  repo's own established server-lifetime rule for assistant sessions (`Start-Process`, never
  `deploy-play.ps1 -RestartServer` from an assistant tool call). Assistant-reachable per the
  already-established `live-lawn-check-is-assistant-reachable` precedent — not owner-only in practice,
  just flagged here because it's the one step that touches a running game.
- No other step in this plan requires the owner directly — both real open questions ship behind a
  default per the Gates section above.

## Open questions (tracked, not blocking)

- §4a-shaped cost question: does a lawn-deploy trigger cost anything beyond the tuning file's own
  frequency cap (souls? a cooldown?) — ships with "no cost beyond frequency" as the T2.2 default,
  same "ask first on game balance, default to no-op" pattern this repo already uses elsewhere.
- Whether `zomboss-deploy-ai` ever needs to coordinate with the world-map `ai-commander` — not resolved,
  not blocking either module; tracked as a T3.x follow-up if it surfaces during build.
