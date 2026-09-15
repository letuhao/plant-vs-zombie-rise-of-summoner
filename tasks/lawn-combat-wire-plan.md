# Implementation plan: `lawn-combat-wire`

**Program:** `lawn-combat-wire` · **Map:** [../docs/architecture/lawn-combat-wire-map.md](../docs/architecture/lawn-combat-wire-map.md)
**Ideal:** [../docs/architecture/lawn-combat-wire-ideal.md](../docs/architecture/lawn-combat-wire-ideal.md) (D1–D9)
**Specs:** `docs/architecture/lawn-combat-wire/spec-*.md` (11 modules)
**Task list:** [lawn-combat-wire-todo.md](lawn-combat-wire-todo.md)

---

## Overview

Make a vanilla PvZ lawn hit carry RPG elemental damage. The RPG stat layer already reaches the lawn
(proven live: a buffed Peashooter's peas really do 1350); the RPG *combat* layer does not — 197
`combat.*` channels resolve on live entities and touch nothing.

Almost all of this is wiring. The overlay damage stack, the element ring, the VFX, the Funnel and FA10
are built and default-on; what is missing is the entry point, an attacker identity, a grant, and a few
numbers. Three of the eleven modules are **pre-existing defects worth landing regardless** of whether
this program ever ships.

## Architecture decisions carried from the ideal

- **D1 — two lanes, both live.** `progression.bonus.*` → `EntityStatWriter` → vanilla fields (incl.
  `attackDamage`) is the stat-bleed lane; `combat.*` → overlay damage is the other. **`combat.*` must
  never join `MergeAppliedCombat`** — that is the one prohibition the design rests on, and Task 5
  guards it. Balance is a separate program.
- **D3 — the lawn is a reflection with ≥1 step delay.** We do not own PvZ's sim loop. Byte-identical
  determinism belongs to siege/delve/world assault, never here.
- **D6 — exhaustion suppresses the rider, never the shot.** We may not modify vanilla projectile
  behaviour, so "no resource, no trigger" gates the RPG's own contribution only.
- **D8 — one attack is one action trigger.** A piercing pea hitting five zombies is one swing, five
  damage applications.
- **D9 — an effect-bearing lawn hit is carried and coalesced, never dropped.** `combat.hit` is
  droppable today *because it has no consumer*; this program changes that class.

## Dependency graph

```
T3 element-cache-invalidate ─┐
T4 combat-numerics ──────────┤
T5 resource-subtick ─────────┤   (independent; disjoint files; shippable alone)
T6 lawn-hit-attribution ─────┤
T7 basic-attack-seed ────────┘
                │
                ├─► T8  lawn-action-bridge      (needs T7)
                └─► T9  lawn-hit-entry          (needs T6, T4)
                            │
                            └─► T10 basic-attack-grant   ⚠ STRICTLY AFTER T9
                                        │
                        T11 calibration ─┼─► T12 basic-attack-cost   (needs T5, T8, T9, T10)
                                         │
                                         └─► T13 lawn-combat-live-proof
```

## The one hard ordering constraint

**`basic-attack-grant` (T10) must not ship before `lawn-hit-entry` (T9).** Binding the grant flips
`HasOnDamageDealtGrant()` true for every actor, opening `EventDrainHost.cs:48/72` for every hit — with
none of T9's liveness guard, never-drop rule, swing dedupe or instakill guard in place. That means
per-victim triggering (violating D8), the `next <= 0` → `ForceKill*` double-`Die()` path live, and a
rider on the lawnmower's 1,000,000-damage event.

**A half-deployed program is worse here than an undeployed one.** This is a real ordering constraint,
not a preference.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| The feature ships **silently inert** — `resource.max.stamina` is 0 for every lawn actor today, so under D6 "no delta" is indistinguishable from the bug we are fixing | High — looks like failure, is actually an unwired pool | T12 carries an explicit *"pool max is non-zero"* acceptance criterion, checked before anything else in that task |
| Per-hit cost regression — T10 pins the damage trigger-mask bit **on** permanently for every actor | High | T13 re-measures with the mask on. Ceiling **≤ 6% frame share at 300z** (existing figure: 4.44% with the mask off). On breach the feature ships behind the kill switch **defaulted off**, not green |
| A live probe run inside a debug session proves nothing — `EventDrainHost.Active` is false when `DebugRuntime.SessionActive`, and `EmitOverlayBreakdown` only emits *inside* one | High — this already happened once this session | T13 requires running outside a debug session and says how to confirm (a stamped `scenarioId` is the tell) |
| `OVERLAY-COMBAT` off with the grant bound ⇒ actors pay stamina for a delta that is dropped — **worse than undeployed** | Medium | T10 ships a feature-specific kill switch that disables grant-binding and cost-charging **together** |
| Two same-shape errors already occurred this session (inferring a call path from a component's existence) | Medium | T1/T2 are *reads*, scheduled before any code, precisely to stop a third |
| Four attack methods are unhooked (`QingZombie.AttackPlant` override, two `AttackPlants()`, two plant-side `AttackEffect(List)`) — those creatures deal **zero** elemental damage | Medium | T6 hooks them; accepting the gap is a scope change to be asked for, not a way to pass the task |
| **Injector-side tests are not in CI.** T8, T10 and T12 rest on behaviour only testable in `FusionRpg.Injector.Tests`, which needs interop refs and a legal game dir, and is **not** among CI's 12 projects | **High** — three tasks' acceptance criteria have no automated home, which is how a criterion quietly never gets checked | The **observer (T0)** is the answer: what cannot be unit-tested injector-side must be *measured* on a live board and read from the run file. Each of T8/T10/T12 must name which of its criteria are covered by Core tests, which by the observer, and which by neither — the last list must be empty |
| `publish.py` **cannot author the regen rows** — it refuses to invent a key, and `battle-resources` has no regen block, while the file forbids hand-editing | Medium | T11 extends `publish.py` with an add-key mode; T11 resized M |

## Orchestration — one lead agent as gatekeeper, no human gate until the end

**One lead agent dispatches and gates every worker. There is exactly one human gate, at the very end,
and it exists because the final proof needs human eyes on the actual game.**

```
Lead agent (gatekeeper — dispatches, verifies, gates; never builds)
  │
  ├─ Phase 0 ─ T1 T2 reads  ‖  T0 lawn-combat-observer (THE RULER — built first, used by all)
  │     └─► GATE 0
  │
  ├─ Phase 1 ─ five workers in parallel (T3 T4 T5 T6 T7) — disjoint files, no ordering
  │     └─► GATE 1  (lead re-runs every command itself)
  │
  ├─ Phase 2 ─ two workers (T8, T9) — T9 is the safety rules and is the gate for Phase 3
  │     └─► GATE 2
  │
  ├─ Phase 3 ─ T10 (strictly after T9) ‖ T11, then T12
  │     └─► GATE 3
  │
  └─ Phase 4 ─ T13 runs the proofs THROUGH THE RULER ──► ⛔ THE ONE HUMAN GATE
```

### The gatekeeper contract — the lead verifies, it does not read summaries

**A worker's report is a claim, never evidence.** Before passing any gate the lead must, itself:

1. **Re-run every verification command** named in the task and read the real output — not the
   worker's quotation of it.
2. **Read the actual diff**, not the description of it. Confirm the files changed are the files the
   task named, and that nothing else moved.
3. **Confirm the negative cases exist.** A task whose tests only assert success has not met a
   criterion that names a falsifier.
4. **Check `git status`** for another session's work before accepting changes to shared files
   (`GameHooks.cs`, `Program.cs`, `EventDrainHost.cs` are the high-traffic ones here).

This contract is not ceremony. This repo has twice had work reported complete that was not: a live
probe returned `ok:true` end-to-end for a feature that was entirely broken, and a compacted summary
cited a passing test file that never existed. **"Tests pass" from a worker means the lead has not yet
checked.**

### What the lead may decide alone, and what it must escalate

| Lead decides | Lead escalates to the human |
|---|---|
| Whether a task met its acceptance criteria | A spec is wrong and needs changing |
| Re-dispatching a worker that fell short | A scope change (e.g. accepting the four unhooked attack methods as permanently RPG-inert) |
| Task ordering within a phase | Any balance value that cannot be derived from a named anchor |
| Splitting a task that proved larger than sized | A guard or hard boundary would have to be weakened |
| Failing a gate and holding the phase | The perf ceiling is breached |

**The lead never relaxes an acceptance criterion to pass a gate.** If a criterion cannot be met, the
gate fails and the reason is reported — that is the lead succeeding at its job, not failing.

### Worker contract

Each worker gets one task, self-contained: the spec path, the acceptance criteria, the verification
commands, the files it may touch, and its dependencies. Workers report **commands run and raw output**,
never "done". A worker that cannot meet a criterion says so plainly rather than narrowing it.

Workers commit their own work (`repo-git.commit`, explicit `paths`, the worktree param) — but a commit
is not a gate pass. The lead gates after the commit exists and can be diffed.

### The observer — an instrument, because eyes do not produce metrics

**Human eyes are not the evidence source.** They cannot collect numbers, they cannot sample a
300-zombie wave, and they cannot tell 4.4% frame share from 6.1%. The owner catching the T14 defect by
looking at the screen was not the system working — it was the *instrument* having measured the wrong
scope, leaving a person as the only remaining check.

So the program needs a **ruler**: an instrument that produces numbers from the live board, from a scope
that cannot fabricate them. That is **Task 0 (`lawn-combat-observer`)** — built first, used by every
later phase, and the thing T13's proofs are actually read from.

**The ruler's hard requirement — it must not perturb what it measures.** This is not a general
principle here, it is a specific shipped trap:

| Instrument | Why it cannot be the ruler |
|---|---|
| `EmitOverlayBreakdown` | only emits **inside** a debug session (`InjectorCombatBridge.cs:~88-94`) — and a debug session sets `EventDrainHost.Active = false`, disabling the code path under test |
| Anything requiring `SessionMode` | bypasses coalescing entirely (`EventDrain.cs:216`), so the measured pipeline is not the shipped one |

An instrument that changes the system's behaviour when switched on measures a different system. **Any
metric the ruler reports must be collected with the feature in its shipped configuration.**

What it must produce, per run, as numbers:

- Per-hit: attacker ptr, victim ptr, swing id, vanilla amount, RPG delta, resolved elements, matchup
- Aggregates: hits, swings, triggers (**triggers must equal swings, not victims** — that is D8, measured)
- Resource: stamina spent, regen accrued, exhaustion events
- Dropped-record counters (D9 says none should drop — the counter proves it)
- Frame-share sample under a 300z wave

### ⛔ The one human gate — after the ruler reports

Everything before is agent-gated. The final gate is human, and it is narrow: **the human reads the
ruler's numbers and makes the product calls the numbers cannot make.**

| The ruler decides (mechanical) | The human decides (product) |
|---|---|
| Did triggers equal swings | Is this damage *fun*, or does it trivialise the lawn |
| Did any record drop | Is a measured 5.8% frame cost worth the feature |
| Did the Fire/Ice differential match the computed ratio | Does the elemental VFX read clearly on a busy board |
| Did an exhausted actor recover | Is the exhaustion cadence a mechanic or an annoyance |
| Frame share under load | Ship, ship-behind-switch, or stop |

**Eyes remain a secondary signal** — visual breakage, VFX that reads wrong, something the instrument
has no channel for. Useful, and never the metric.

The lead prepares everything (server up, injector deployed, board live, ruler running, all seven proofs
with their falsifiers executed) and **stops**. It does not self-certify T13, and it does not substitute
its own judgement for the product calls above.

## Verification posture

Three of eleven modules are pre-existing defects (T3, T4, T5) and each must land **without moving a
golden** — they are correctness and representation fixes, not balance changes. If a golden moves in
T4, the conversion is wrong, not the golden.

The program's own proof is T13, and every proof there is paired with a falsifier. A passing run with
no negative case proves nothing; that is the lesson this program was created from.

## Day-one answers the plan owes a builder — [audit]

Resolved here so nobody has to guess:

| Question | Answer |
|---|---|
| Session boundary for a 13-task program spanning Core/Injector/data | The lead agent records the session and its `paths` **before dispatching any worker**, per `contributing/session-boundary.md`. Workers inherit that fence and never widen it |
| Where the new basic-attack factory lives | `src/FusionRpg.Core/Actions/` — exact file name is T8's to choose, but it is **Core**, never the injector |
| How to run injector-side tests | Mostly you cannot in CI (see the risk table). T8/T10/T12 each declare per-criterion coverage: Core test, observer measurement, or neither — and *neither* must be empty |
| Do tuning loaders resolve `v{n+1}` | **Unverified — T11 must check before bumping**, not after |
| Kill-switch env var name | `FUSIONRPG_LAWN_BASIC_ATTACK`, fixed here so T10 and T12 agree |
| If T2 picks "ship the seed beside tuning" | The injector csproj copy rule changes — **T8 owns that**, since no other task lists a csproj |

## Open questions

None blocking. T1 and T2 are **tasks, not gates**: T1 is a file read, and T2 has a stated reversible
default (ship the first increment uncosted, restore cost in a later slice) so it cannot stall the
plan. Neither protects an irreversible action.

The genuinely deferred work — elemental reactions, status resolver, ICD, proc coefficient, plant-side
status, resource-exhaustion debuffs, per-actor vanilla defense — is tracked in the ideal's
"Deferred, tracked" table and owned elsewhere.

## Next run — audit 2026-09-15

An independent three-verifier audit of the long run (`f251e63d..38d5609e`) found T13 proofs 4, 5, 6, 7
and the perf ceiling closed without evidence matching their acceptance text, five Task 0–12 bullets
reworded before being ticked, and three real code defects (fixed in `7073ffcb`, test gap closed in
`bc27cb0c`). The todo carries the verdict per bullet and tasks `L-N1…L-N26`.

**Order.** Phase A (code/test debt, no game) → Phase B (owner decisions) → Phase C (live re-proof).
Phase C starts with `L-N22` (redeploy the audit fixes) because every live number before it ran with
traces on the hot path and a row-only shooter guess.

**Rules this run proved necessary — binding for the next lead:**

| Rule | Why (what went wrong) |
|---|---|
| Never edit an acceptance bullet in the same change that ticks it. Reword = separate commit, owner-visible | Five bullets and the perf breach clause were rewritten, then ticked |
| Board-wide aggregate counters never prove a per-ptr claim | Proof 4 "same ptr recovers" was ticked from global window totals |
| A code read never substitutes for a T13 live proof | Proof 6 closed by reading `InjectorEntityRegistry` |
| A cheat kill (`debug.kill`) is not the spec's deferred-delta kill | Proof 7 exercised one hook only |
| Debug-spawned, HP-pinned or debug-funded state is invalid input for Hub-stat or server-side proofs | Proof 5 read a pinned maxHp; T14 souls came from debug-spawned kills |
| A mid-match kill-switch toggle is not "feature off" | Bound grants keep firing; A/B did not isolate the feature |
| Perf breach, spec wording and product defaults escalate to the owner — the lead does not decide | Run decided "stays default ON" and deleted the breach clause |
| Ask before closing the owner's game process | Run force-killed the game to clear a DLL lock |
| `verify-change` on `src/FusionRpg.Injector/**` is not verification until `L-N23` lands | Injector-fallback runs only Core.Tests; no guards, no injector compile |

**Owner decisions owed (Phase B):** perf ceiling ship/switch-off/stop (`L-N1`); hypno re-bake spec
wording (`L-N11`); T4 double interior vs spec amendment (`L-N12`); whether the debug-funded soul balance
is acceptable (live-probe Task 13); battle stamina regen on or lawn-only (`L-N28` — `BattleHubCompose` shares
`ResourceBaselineSubsystem`, so T11's lawn calibration also regenerates stamina mid-battle, against resource-hub-ssot §11).

**Audit continuation, same day (Phase A closed).** `L-N10`, `L-N13`, `L-N16`, `L-N20` landed with tests and
killed mutants; `L-N15` added `scripts/mutants/lawn-combat.json` (21 mutants over `LawnElementResolver`,
`OverlayCombatCalculator`, `EventDrain`) and closed the one survivor with a test. Two more real defects
were found and fixed on the way: a hit recorded before its target died still reached `AddZombieHp` and ran
a second `Die()`, and dead-ptr marks never cleared on spawn, so a recycled address refused every RPG hit
(`80d7a9da`). Follow-ups added: `L-N27` (liveness on pooled reactivation — live) and `L-N28` (T11's regen
change reaches every `seedResourceBaseline` Hub caller, not only the lawn). None of this is deployed:
`L-N22` still gates every Phase C proof.
