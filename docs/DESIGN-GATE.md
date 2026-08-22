# Design gate — read before you propose anything

**Status: binding.** Applies to every session, human or automated, before any spec, plan, proposal,
ADR, audit finding, or sentence beginning "we should".

---

## 0. The rule

**Before you propose a change to a subsystem, you must have read that subsystem's authoritative
documents in the current session, and you must cite them.**

Not skimmed. Not recalled from a summary. Not inferred from a code comment or a filename.

If you have not read them, you do not yet have an opinion. Say *"I need to read X first"* and read it.
That costs one tool call. Proposing against a system you have not read costs the owner an hour of
correcting you, and it is the single most common failure in this repo's history.

**The sequence is: read → verify against code → then propose.** Never propose → get corrected → read.

---

## 1. The reading gate — topic index

Find the row for what you are about to touch. Read the middle column **before** you write anything.
The right column is the thing sessions actually get wrong; read it as a warning, not a summary.

| If you are about to touch… | You MUST have read | What sessions get wrong |
|---|---|---|
| **Anything at all** | [architecture/software-architecture.md](architecture/software-architecture.md) · [architecture/decisions.md](architecture/decisions.md) | Proposing something already locked in `decisions.md` |
| **How the injector talks to the game** | [architecture/event-pipeline-v2-ssot.md](architecture/event-pipeline-v2-ssot.md) · [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) | **Record-then-drain.** Hooks record a struct and return; effects are decided later in a budgeted drain, and records carry to the next frame. G5: worst case degrades to **delayed effects, never frame drops**. Never argue a design from "it must complete inside the frame" |
| **Where logic may live (server vs injector)** | [architecture/overlay-control-loops.md](architecture/overlay-control-loops.md) · [architecture/pvz-middle-layer.md](architecture/pvz-middle-layer.md) | **The RPG never reads PvZ's current state and never guesses it.** Two async systems. The RPG observes *past* events and contributes a **signed delta** later. It does not compute damage at the moment of the hit |
| **Combat damage / HP** | [architecture/combat-damage-ssot.md](architecture/combat-damage-ssot.md) · [architecture/effect-funnel.md](architecture/effect-funnel.md) | Deltas, never absolutes. `mode=set` on current HP is rejected. FA10 is `Add`. FA10 never calls Unity `TakeDamage` |
| **Stats** | [architecture/stat-system.md](architecture/stat-system.md) · [architecture/actor-hub-ssot.md](architecture/actor-hub-ssot.md) | One writer. Combat writes go through `EntityStatWriter`, never ad-hoc Unity patches |
| **Effects (Foundation)** | [architecture/effect-system.md](architecture/effect-system.md) · [architecture/effect-data.md](architecture/effect-data.md) · [architecture/effect-runtime.md](architecture/effect-runtime.md) · [architecture/effect-funnel.md](architecture/effect-funnel.md) | Foundation is **sealed**. The Funnel is the only Secondary → Bag path |
| **The atom / Secondary effect layer** | [architecture/effect-atom/definitions.md](architecture/effect-atom/definitions.md) **(wins over any spec)** · [architecture/effect-atom/atom-catalog-ssot.md](architecture/effect-atom/atom-catalog-ssot.md) · [architecture/effect-atom-map.md](architecture/effect-atom-map.md) | The vocabulary is **closed**: 5 attach points, 12 kinds, 7 triggers. Adding one is a reviewed change, not a convenience |
| **Status effects** | [architecture/status-ssot.md](architecture/status-ssot.md) | `StatusCatalog` is ADR-locked code-first. 21 declared, ~13 functional |
| **Elements** | [architecture/element-hub-ssot.md](architecture/element-hub-ssot.md) | Two matrices, not one — the shield matrix is asymmetric with the combat ring |
| **Data / SQL / schema** | [architecture/data-architecture.md](architecture/data-architecture.md) · [contributing/architecture-map.md](contributing/architecture-map.md) | SQL lives **only** in `FusionRpg.Data`. `guard-dal.ps1` enforces it — and scans only `src/`, so `tools/` is a blind spot |
| **Match / actor lifecycle** | [architecture/match-runtime.md](architecture/match-runtime.md) · [architecture/unique-actor-runtime.md](architecture/unique-actor-runtime.md) · [architecture/unique-entity-effects.md](architecture/unique-entity-effects.md) | IL2CPP reuses pointers. `entity:{ptr}` grants must be withdrawn on death before reuse |
| **Performance** | [runbook/perf-probe-plan.md](runbook/perf-probe-plan.md) · [research/perf/00-baseline.md](research/perf/00-baseline.md) | Lag is **main-thread scans and uncached resolves**, not SignalR or the server. Do not re-litigate transport without new probe data |
| **Battle / turns** | [architecture/battle-timeline-map.md](architecture/battle-timeline-map.md) · [architecture/battle-turn-ideal.md](architecture/battle-turn-ideal.md) | Battle consumes FA10 only; it never grants and never calls `OnEvent` |
| **World map** | [architecture/world-map-program.md](architecture/world-map-program.md) | Specs pending owner review — no build authorized |
| **Standalone / web RPG** | [architecture/standalone-rpg-map.md](architecture/standalone-rpg-map.md) · `decisions.md` standalone-first row | **The web RPG is the core game; PvZ is extension gameplay.** Web outcomes *are* server-authoritative — do not generalise the injector's constraints onto it, or its model onto the injector |

Full map: [README.md](README.md). The capability map of a program is the index of what exists for it —
never guess which spec is active from a filename.

---

## 2. Load-bearing invariants

These are settled. Re-deriving them from scratch is how sessions arrive at confident wrong answers.
If your proposal contradicts one, you have found either a real architectural change (say so
explicitly, and expect a decision) or your own misunderstanding (far more likely).

1. **Two async systems.** The RPG and PvZ do not share a clock and do not wait for each other. The RPG
   works from **past events**, never current game state, and never guesses it.
2. **Record-then-drain.** Hooks record and return. Decisions happen in a later budgeted drain.
   **Delay is the designed degradation mode**, not a failure to engineer around.
3. **Deltas, not absolutes.** Overlay mutations are signed deltas through the Funnel. Absolute HP/ATK
   from an overlay snapshot is rejected by contract.
4. **Single writer.** All combat writes go through `EntityStatWriter`.
5. **The Funnel is the only Secondary → Bag path.**
6. **SQL only inside `FusionRpg.Data`.**
7. **The game is the simulation, not a thin client** — but that does *not* make the overlay
   latency-bound. See invariant 2. Both halves of this sentence matter.
8. **Foundation is sealed** at its contract version. Secondary builds on top; it does not edit it.
9. **Standalone-first.** Every RPG feature must be playable with the game closed. The injector may
   *enrich* a feature, never *gate* one.
10. **Perf is a main-thread problem.** Settled by measurement in 2026-08.

---

## 3. Evidence rules

1. **Cite `file:line`.** A claim without a location is an opinion.
2. **Code beats documentation; documentation beats comments. A comment is not evidence.**
   A file comment saying it mirrors another file is not a coupling — open the file and check.
3. **Read the section, not the line.** Before quoting a rule as a general law, read its heading and
   its neighbours. A rule under *"What the Server may do during a run"* constrains the server during a
   run; it is not a universal principle.
4. **Test the constraint before you declare it.** "This would move the goldens" and "this needs owner
   sign-off" are *claims*. Run the suite. An assumed constraint that costs the owner a decision they
   did not need to make is the same defect as a wrong line of code.
5. **Verify counts by counting.** Not by trusting a number written elsewhere in the same doc set.
6. **When you correct something, propagate it.** A fix that lands in prose but not in the sibling
   Structure / Testing / Boundaries block, the map, and the task list has not landed. Re-grep after.

---

## 4. Failure log

Real incidents. Added to whenever a session burns owner time on a misconception. This section is the
argument for the gate — keep it factual and keep it growing.

| Date | The misconception | Root cause | What would have caught it |
|---|---|---|---|
| 2026-08-22 | "The proc roll must happen in the injector because a server round-trip cannot complete inside a frame." | Never read `event-pipeline-v2-ssot.md`. The pipeline is record-then-drain and **G5 explicitly makes delayed effects the designed worst case**. Argued from a constraint the architecture rejects | Reading the pipeline SSOT before reasoning about pipeline timing |
| 2026-08-22 | Quoted *"Server must not own authoritative proc RNG for lawn hits"* as a general architectural law | It sits under *"What Server **in a run** may do"* and concerns the UniqueActor FSM, not a ban on server-side rolling. Read the line, not the section | Evidence rule 3 |
| 2026-08-22 | "Fixing the effect RNG will move goldens, so it needs owner sign-off" | Assumed rather than tested. All 7 chance-gated fixtures use `chance: 1.0`, and the code short-circuits the draw at `chance >= 1.0` — the RNG was never consulted. **Zero goldens moved** | Evidence rule 4 — run the suite before escalating |
| 2026-08-22 | Treated a `VfxCatalog` comment (*"mirroring EffectSeedCatalog"*) as a cross-stream blocker | The file contains zero `fx.*` ids and keys on statusIds. The comment was stale prose | Evidence rule 2 |
| 2026-08-22 | "Plants have no armor" | Asserted from vanilla `arm1`/`arm2` being zombie-only, without reading the shield/resistance layer, which is side-agnostic | Reading `status-ssot.md` / the shield program before claiming a mechanic does not exist |

---

## 5. Pre-proposal checklist

Paste and complete before presenting any design work.

```
[ ] I identified the subsystem(s) this touches.
[ ] I read every doc in the §1 row(s) for those subsystems, this session.
[ ] I checked decisions.md for a lock covering this.
[ ] Every factual claim cites file:line.
[ ] I verified claims against CODE, not comments.
[ ] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting - "moves goldens",
    "needs sign-off", "breaks X" - and said what I ran.
[ ] Nothing contradicts a §2 invariant, or I named the contradiction explicitly.
[ ] Corrections are propagated to prose, Structure, Testing, Boundaries, map, and tasks.
```

**If you cannot tick a box, say so in the proposal.** An honest gap costs a sentence. A hidden one
costs the owner an hour.
