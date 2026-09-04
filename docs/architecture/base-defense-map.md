# Capability map: base defense (the siege stage)

**Status: APPROVED 2026-09-04, then AMENDED to 21 modules the same day** after the
[completeness audit](base-defense/_completeness-audit.md) found 7 owner decisions unspecced and 3 specs
outright wrong. Gate 0 taken in full; the fifth-stage `decisions.md` amendment approved alongside. Module specs live in
[base-defense/](base-defense/), one per module id below.

**Gate 0 was run before any spec was written — see [§ Gate 0 results](#gate-0-results-run-2026-09-04).
Six §3 rows moved.**

**Ideal it implements:** [base-defense-ideal.md](base-defense-ideal.md) — **46 owner decisions** across
eleven rounds (§0), plus a **four-lens adversarial audit** (§11: economy, playability, engineering,
architecture) whose findings are verified against source.
**Plan / tasks:** `tasks/base-defense-plan.md` · `tasks/base-defense-todo.md` (the prefixed pair per
`AGENTS.md` — `tasks/plan.md` is the perf stream's and is never a fallback).

---

## What this program is

A **siege**: a turn-based tactical board where a base is defended and assaulted, on its own stage
(`#/siege/{id}`). The board is the **district around the Seat** (decision 26) — outer ground carrying
obstacles and buildings, a **central defense area** holding the legions that are the win condition.
Both sides move. Buildings are a new actor kind with no ownership; possession is by occupation.

## What it is not

- **Not a redesign of the battle kernel.** It adds a **fourth mode-profile row**, not a branch —
  *"adding a mode should mean adding a row"*.
- ~~**Not the structure content pipeline.**~~ ⛔ **Decision 45 REVISED decision 30**: `structure-seed`
  is now a **module set inside this program** (modules 23–28), not a separate one. One map, one plan,
  one todo pair. Its ideal, [structure-seed-ideal.md](structure-seed-ideal.md), stays as the design
  record — only the program boundary changed.
- **Not the planet economy.** Sector-scale economy and inter-sector trade are `sector-development`
  plus an economy program (decision 19). This program reads `DevelopmentLevel` and defaults it (§5.10).
- **Not the played seat's machinery.** `spec-interactive-turns.md` owns T6/T10/T11 — **consume, never
  re-derive** (audit F4, the largest scope overlap found).

## Assumptions — correct these now

1. **In-place work** in the existing solution. No new assembly.
2. **`structure-seed` may land after this program starts.** Every structure input is defaulted (§5.10).
3. **The `siege` stage is gated on a `decisions.md` amendment** (audit §11.5) — proposed on the Game
   GUI row, **not yet approved**. Only `siege-stage` is blocked by it; everything else proceeds.
4. **Force-size numbers stay tunable and unset** (decision 29). Modules must not bake a field cap.
5. **Golden discipline:** phases 0–2 are golden-free by construction; phase 3 is **one batched landing**
   sharing a triage pass with whoever else moves `RulesetVersion`.

---

## Modules

**Twenty-nine**, in two families that barely touch:

| Family | Modules | Shape |
|---|---|---|
| **The siege** | 1–22 | Engine, world seam, board, AI, FE |
| **Structure content** (folded in by decision 45) | 23–29 | Seed schema → corpus → catalog → **instantiate** → planner → pipeline → metrics |

The two families meet at exactly one point: `structure-catalog-import` (25) replaces the four
hand-authored `StructureCatalog` rows the siege ships against. **Everything else is independent**, and
the content family is **model-free until module 27** — *"a parse, a table, a schema and a dump produce
real value with zero tokens spent."*

Levels 0–2 are golden-free and can start immediately; level 3 is the one golden-locked landing;
levels 4–6 build on it.

> ### ⛔ Four modules added 2026-09-04 by the completeness audit
>
> The first seventeen described **how to build a board** and never said **what the game on it is.**
>
> | Module | Covers | Why it was missed |
> |---|---|---|
> | **`siege-objective`** | Decisions **1, 4, 5, 10** — the win condition, legion slots, max members per legion, the field cap, the Core as a pure arena | The headline finding. Seventeen specs, no objective |
> | **`siege-obstacles`** | §5.18's four kinds + Emplacement | `Mine` had no home anywhere; `Wire` was specced as a movement cost when it is a **stamina** cost |
> | **`siege-engagement`** | Decision **24** — one engagement per map turn, siege spans turns because engagements repeat | `siege-resolver` assumed every assault resolves to a winner |
> | **`world-graph-diff`** | §8 prerequisite 1 | *"No longer a follow-up"*, and it had become nobody's |
>
> **And three specs were wrong rather than thin**, which is worse — a missing spec gets written, a
> wrong one gets built: `battle-clock-profile` gave a siege unit **one action per turn** (move *or*
> attack, on a 24-cell board); `siege-cover` used **per-mille where the contest uses flat points**;
> `siege-ai` did the cover-seeking §5.17 forbids — **owner-overridden as decision 31**, with the risk
> recorded.

| Module id | Responsibility | Depends on |
|---|---|---|
| `battle-clock-profile` | **Move `MaxRounds`/`RoundDurationMs` onto `BattleModeProfile`** (they are global today — audit F2, the one thing `[JsonIgnore]` cannot save), `classic-round` = today's values so goldens hold byte-for-byte. Add the **`siege` profile row** with `OrdersBySpeed` and **jitter disabled** (F6) and `RequiresLiveInput` | — |
| `siege-supply` | The **besieged-base supply exemption** (F1) — *a base with stores is not a legion in the field*. Also names, without fixing, the capital-immunity defect (F1b) | — |
| `siege-board` | **`A10 battle-board`**: `GridSpec`, cell occupancy, integer Chebyshev distance. The grid vocabulary already exists (`GridPos`, `GridDistance`); this gives it a board | — |
| `siege-pathing` | **Deterministic heap A\***, integer costs, **explicit ordinal tie-break** — `ReachMap`'s own comment is the warning: *"a heap would need the same tie-break written explicitly or a replay could disagree with itself"* | `siege-board` |
| `district-layout` | **`(sectorId, worldSeed, slots) → board`**, with a **stability contract**: byte-stable on replay, stable across turns and slot growth, unchanged by capture, entry edge from `OnLaneId` | `siege-board` |
| `siege-seam` | Widen `BattleRequest` with a board projection; widen `BattleOutcome`/`BattleSideOutcome` with per-slot results, spend, and **`Withdrawn`** (F5 — the raid has no verb today). Grow `BattleApplication` a third and fourth entry point. **Verified unhashed and unpersisted — moves no golden** | `siege-board` |
| `structure-state` | Structure **HP and cell** on `WorldSlot` via the `faction-scope` **conditional-row** precedent; **slot-level depletion** (F10 — `DepletionMilli` is sector-scoped and already claimed); the repair resolver (proportional, `buildCost × ratio × hpFraction`) | `siege-seam` |
| `combatant-kind` | The **actor-kind discriminator** on `BattleActorSetup`, `[JsonIgnore(WhenWritingDefault)]`. Gate `AnyActive` and the forced-basic-attack path so a structure never enters initiative and never keeps a battle alive | `battle-clock-profile` |
| `siege-positions` | Make `PositionOf` real; assign `EffectBag.BoardSnapshot`; pass a board to `Status.Tick` — **all three gated on a board existing**, `null` otherwise. Sets `boardAvailable: true` at the one production call site | `siege-board`, `combatant-kind` |
| `siege-waves` | Mid-battle **roster growth** for batches; the **batch trigger as a clock** (F8 — state-based is turtle-exploitable); **bounded/resumable drain** (F9/C7 — the kernel baseline's one named hole) | `combatant-kind` |
| `siege-cover` | ⛔ **REWRITTEN for decision 35 — the HoMM3 shooting model, not terrain cover.** Four multipliers: **cover area** (authored radius per obstacle kind), **range penalty**, **obstruction penalty** (a unit *or* obstacle in the line **reduces** power, never blocks), and a **`ProjectilePenalties` flag** saying which a shot pays. Spans the **battle engine and the action system**. `RequiresLineOfSight`'s first reader. **Does not spend the vocabulary-change budget** — cover is per-shot, so no membership is entered | `siege-positions`, `siege-obstacles` |
| `siege-construction` | **The four acquisition paths** (decision 27): built (materials, accumulates) · assembled (consumable, immediate) · summoned (`qi`) · laboured (`stamina`/`hunger`). Paths 3–4 are ordinary actions and need no new economy | `siege-seam`, `structure-state` |
| `siege-economy` | **Board income** — nodes yield per turn to whoever garrisons them; the **depot** budget seeded from world stock and reconciled spend-only; the **capture-transfers-stockpile** fix (F11) | `siege-construction` |
| `siege-ai` | **R1–R6**: aggro tier separate from target choice, additive score with a risk term, objective fallback, frozen acting order, deterministic and readable. One `IIntentSource` **dispatching on `SideOf`** — a wrapper, no signature change | `siege-positions`, `siege-cover` |
| `siege-resolver` | `IBattleResolver` implementation, supplied at **BOTH `RpgStore.WorldTurns.cs:509` AND `:603`** — wiring only `:509` makes every re-derived turn report disagree with what happened | `siege-ai`, `siege-seam` |
| `board-render` | The **generic board layer** the FE lacks: `GridSpec` passed not imported, a generic entity registry, a caller-supplied kind→visual mapping, `createGame({scenes})`, cell picking, and the **camera bridge** between the pure `Camera` model and a Phaser camera | `siege-board` |
| `siege-stage` | The `siege` stage, its route, and the six conditional shell files. Plus §7 cost 5's `*Dto` guard. **Pause = a persisted DECISION LOG replayed on resume** (decision 46), never a session in memory — so §2 rule 7 holds unconditionally and a pause survives a server restart. ⚠️ **Prerequisite: a `decisions_json` writer**, which `spec-interactive-turns.md` (T10) owns and which does not exist today | `board-render`, `siege-resolver` |
| `siege-objective` | **The win condition** (decision 1) · legion slots and max members (4) · **the field cap** (5) — authored, symmetric, never derived from empty cells · the Core as a pure arena (10) · `DefenderBonusMilli` shrinking so the defender is not paid twice | `combatant-kind`, `district-layout` |
| `siege-obstacles` | §5.18's **Trench · Rampart · Wire · Mine · Emplacement** — five rows, five distinct decisions. Wire taxes **stamina**; Mine is **revealed** (F9) and fires on the cell-entry transition `siege-cover` already introduces | `structure-state`, `siege-cover`, `siege-construction` |
| `siege-engagement` | Decision **24** — `Spent` as a normal outcome, what persists between engagements (world state) and what must not (board state), `IsUnderSiege` **derived not stored**, no engagement cap | `siege-resolver`, `siege-objective` |
| `world-graph-diff` | §8 prerequisite 1. **Measure first** (C5): statement reuse may beat a diffing writer. Equivalence-guarded by `WorldCanonical` read-back | — |
| `battle-stage` | **Decision 44** — `#/battle` as **playback of a resolved `BattleReport`** on the generic layer. Retires the dead stage id **by using it**; proves `board-render` generic with a genuinely different second consumer. **Thin is a constraint**: if a requirement is not derivable from a report that already exists, it is out of scope | `board-render` |
| `structure-schema` | The seed contract — 17 fields, **not one a number**, audited mechanically. `strengthBand` **is** decision 32's material tier (one ordinal, not two); `acquisitionPaths` **replaces** `acquisition` | — |
| `structure-corpus` | **Hand-author ~36 rows from the research** (§5.18 + §5.21), dump today's four, prove the importer. ⛔ **Invention, not datamine** (decision 43) — so hand-authoring first is the guard against mode collapse, which majority vote cannot catch | `structure-schema` |
| `structure-catalog-import` | `StructureCatalog` reads the corpus instead of a C# literal. **The one place an ordinal becomes a magnitude** | `structure-corpus` |
| `structure-planner` | **Decision 33** — a deterministic, committed, diffable plan **before any model call**: the ordered tier ladder, per-role targets, slot legality, variant counts, and the call budget | `structure-corpus` |
| `structure-pipeline` | The **only** module that calls a model, and it writes **identity only**. Permuted enums, declared vote set, `1-1-1` → `unresolved`, byte-identical rerun proven by hash | `structure-planner` |
| `structure-instantiate` | **Pass 3 (P3-3)** — Law 1's missing middle layer: the game runtime rolls a **concrete per-player instance** via `Instantiator.TryInstantiate`, which has **zero production callers** today. A **wiring** module — traits and actions roll; **HP and every ordinal-derived magnitude do not** (decision 32) | `structure-catalog-import` |
| `structure-metrics` | Every metric declares **closed or open**; **an open-loop metric never fails a build**. Skew checked at plan and at output; rarity proven not to be a power axis | `structure-pipeline` |

**No cycles.** `siege-supply` and `battle-clock-profile` deliberately depend on nothing — they are the
two unblocking changes, and both are small.

## Build order

```text
0.  battle-clock-profile · siege-supply · world-graph-diff  (parallel, no deps)
1.  siege-board                                            (then:)
2.  siege-pathing · district-layout · siege-seam           (parallel)
3.  structure-state · combatant-kind                       (parallel)
3b. siege-objective                                        (needs combatant-kind)
4.  siege-positions · siege-waves · siege-obstacles        (parallel)
5.  siege-cover · siege-construction                       (both CONSUME obstacles)
6.  siege-economy · siege-ai
7.  siege-resolver          ← ⭐ playable and CI-provable HERE, with no FE
7b. siege-engagement                                       (needs siege-resolver)
8.  board-render
8b. siege-stage · battle-stage                             (parallel; both need board-render)

    the content family, independent of every level above:
c0. structure-schema
c1. structure-corpus
c2. structure-catalog-import          ← the only join: replaces the 4 hand-authored rows
c3. structure-instantiate · structure-planner   (parallel)
      instantiate = Law 1's middle layer, wiring not a build
      planner     = still zero tokens spent
c4. structure-pipeline                ← ⭐ the FIRST model call in the whole program
c5. structure-metrics
```

**The content family runs in parallel with the siege family** and joins once, at `c2`. Its first four
modules spend **no tokens at all** — which is the seedsmith rule *"order the build so the model-free
modules come first"*, and it means the expensive stage's inputs are reviewable before it runs.

**`siege-objective` joins level 3** because the win condition is what `siege-resolver` evaluates, and
because the field cap is what makes `siege-waves` mean anything. **`world-graph-diff` sits at level 0**
and starts with a measurement, not a build.

> ### ⛔ `siege-obstacles` is at level 4, and it used to be at 5b
>
> Pass 3 broke a cover↔obstacles **dependency cycle** by making obstacles the structure-vocabulary
> module. **Pass 4 found the build order still encoding the old, cyclic ordering** — it read
> *"5b. siege-obstacles (needs both of 5)"* while the module table already said cover depends on
> obstacles.
>
> **A spec table and a build order that disagree is worse than either being wrong alone**, because each
> looks authoritative on its own. Corrected above; the ordering now follows the declared dependencies.

Three orderings are deliberate and worth confirming:

- **`battle-clock-profile` is first and is not negotiable.** `MaxRounds` is global, so a siege that
  needs more than 50 rounds cannot get them without moving all eight goldens. Every later module
  resolves battles under whatever horizon this sets.
- **Step 7 is the standalone-first gate.** Auto-resolve through `siege-ai` + `siege-resolver` is
  provable in CI with **no FE at all**, which is the *"gameless-first"* invariant met before the
  largest line item starts.
- **`board-render` is the largest single module** — measured against the repo, the lawn Phaser island
  is ~2,166 LOC and `stages/world` is ~6,518. Budget it at world-stage scale, not at a reuse.

---

## Gates

Three, and they are different in kind.

**Gate 0 — the inventory is current** (before step 1). The ideal's §3 was surveyed against `HEAD`;
the working tree carries **741 insertions across 15 files** in `Core/Battle` plus an untracked
`World/Growth/`, and four §3 rows are already false (audit C1). Re-run the inventory, and **extend
`WorldDeterminismGuardTests` to `Core/Battle` and `Core/Effects`** — `EffectBag.cs:180` defaults
`UtcNow` to a real wall clock and the guard covers `Core/World` only (C4). Cheapest possible moment.

**Gate A — the seam holds** (after step 3). `BattleRequest` carries a board projection and
`BattleOutcome` carries per-slot results, round-tripped; a `Withdrawn` outcome survives; structure HP
hashes through the **conditional row** with **zero world goldens moved**; and `classic-round` resolves
byte-identically after `MaxRounds` moves onto the profile. **Nothing above level 3 is safe before this
passes.**

**Gate B — a siege resolves, deterministically** (after step 7). A scripted siege runs to a stable
outcome; the same `(seed, template, command log)` reproduces it byte-identically; **the resolver is
supplied at both call sites and a re-derived turn report matches the original**; and every new RNG
stream is **structurally unreachable** with the feature absent — an early return, not a defaulted
value (C3).

---

---

## Gate 0 results (run 2026-09-04)

**Part 1 — the inventory is current.** The 741 insertions in `Core/Battle` that had falsified four
§3 rows were committed as `4195a2d update battle engine`; the tree is now clean under
`src/FusionRpg.Core/Battle`. The re-survey moved **six** rows, and every module spec below is written
against the corrected reading, not against §3 as first written.

| # | §3 said | HEAD says | Effect |
|---|---|---|---|
| 1 | `battle-clock-profile` adds `OrdersBySpeed` and `RequiresLiveInput` | **Both already exist** — `BattleModeProfile.cs`, shipped by B39 and T6/B21, each with a recorded per-row rationale | Module shrinks to *move the clock + add one row* |
| 2 | The siege phase is new | **`SiegePhase` already exists** (`World/Turn/SiegePhase.cs`) and means something else — clearing a slot's guard, `BattleKinds.Guard` | **Name collision.** New work takes `BattleKinds.District` and its own phase file; `SiegePhase` keeps its meaning |
| 3 | Adding a mode is "a row plus wiring" | Exactly **three lines**, named by the catalog's own doc comment: a row in `BattleModeProfileCatalog`, one arm in `Resolve`, one entry in `ModeProfileArchitectureTests.KnownProfileIds` | `battle-clock-profile`'s acceptance becomes literal |
| 4 | The kind discriminator is `[JsonIgnore(WhenWritingDefault)]` | The shipped precedent is **plain `[JsonIgnore]`** — twice on `BattleActorSetup` (`Index`, `SpecimenId`), both recording the same incident: without it, `ExpeditionResolverTests.Tier_goldens_are_locked` moved | `combatant-kind` follows the shipped form, not an invented one |
| 5 | Extending the determinism guard is a change of unknown size | **One line** — `WorldDeterminismGuardTests.cs:144` hard-codes `Path.Combine(root, "src", "FusionRpg.Core", "World")` | Gate 0 part 2 is a one-line change plus its fixture assertions |
| 6 | The FE has four stages | **Three are built** — `web/fusion-rpg-web/src/stages/` holds `lawn`, `sanctum`, `world`. `battle` is declared-but-unbuilt | The amendment adds a fifth *declared* stage to a list where one is already unbuilt — which is the amendment's own third cost, now measured |

Confirmed unchanged (re-verified, not assumed):

- **Both resolver call sites** are still `RpgStore.WorldTurns.cs:509` and `:603`, and **both omit the
  resolver argument** — so both take `PlaceholderBattleResolver` today. `siege-resolver`'s
  two-call-site requirement stands exactly as written.
- **`MaxRounds`/`RoundDurationMs` are global**, static on `BattleRuleset`, read at
  `BattleEngine.cs:240`, `:251` and `:476`. F2 stands.
- **`BattleRunState.PositionOf` returns `null` unconditionally** (`BattleRunState.cs:407`). The board
  is still inert.
- **`WorldCanonical.cs:98`'s conditional row** is intact and is the precedent `structure-state` copies,
  including its recorded reason: appending to the existing row instead "moved every prior hash for a
  value that did not actually change".
- **`StructureCatalog`** still ships **four** hand-authored rows, all loam-flavoured
  (`LoamSource`/`Storage`), with no HP and no cell — the defaulting in §5.10 is still required.

**Part 2 — the guard is extended. DONE 2026-09-05, and it was not one line.** `WorldDeterminismGuardTests`
gains a `Core/Battle` + `Core/Effects` scan for the clock/RNG check. It did go red on first run — but
on **eight** hits, not the one predicted, because the naive widening exposed three real defects in the
guard itself that had never mattered while it only scanned `Core/World`: the scan stopped at the
**first** match per (file, symbol) rather than finding all of them; it was **comment-blind**, so four
of the eight hits were doc comments *explaining* the very rule they tripped; and the float-purity
check would have forced an out-of-scope fixed-point refactor of `Core/Battle`'s pre-existing
derived-stat/aura `double` architecture (aura-skill T4) had it widened alongside the clock/RNG check —
so it deliberately did not. All three are fixed in the guard itself, each with its own proving test.

The one real, predicted finding — `EffectBag.cs:188` (line moved from `:180`) defaulting `UtcNow` to
`() => DateTimeOffset.UtcNow` — is fixed: the field now throws if read unset, and every production
composition root (three deterministic hosts already wired their own clock; the injector's live PvZ
host now wires the wall clock explicitly, on purpose, rather than inheriting it silently) says which
clock it wants. `GUARD` 202/202, `CORE` 6311/6311, boundary guards green. Full evidence:
`tasks/base-defense-todo.md` G0.1/G0.2.

---

## What this program does not touch

No injector work anywhere. No changes to `EntityStatWriter`, the effect Funnel, or FA10. No new event
kinds in the existing ingest vocabulary. `#/lawn`, `#/world` and `#/sanctum` are untouched. SQL stays
inside `FusionRpg.Data`.

## Open items carried from the ideal

**None open.** All three items this section used to carry are closed:

| Was carried | Now |
|---|---|
| The `decisions.md` amendment for the fifth stage — *"owner approval owed"* | ✅ **Approved** 2026-09-04 |
| `structure-seed`'s two questions, *"which belong to that program"* | ✅ **Closed** by decisions **43** and **45** — and it is no longer a separate program |
| The four force-size tunables, deliberately unset (decision 29) | **Answered, with "unset"** — a balance-pass input, not a design gate |

**Two things are owed, neither a design decision:**

1. ⚠️ **A `decisions_json` writer** (decision 46). The column is built and read; **no writer exists**
   anywhere in `src/`. It belongs to `spec-interactive-turns.md` (T10), not here — but a paused siege
   cannot resume without it, and per `DecisionTrace`'s own comment the boot sweep may **overwrite a
   played result with an AI re-resolve** in the meantime. **That risk is live today for every played
   battle, independent of this program.**
2. A coordination check: `structure-state` is the one
golden-locked landing, and this map says it should *"share a triage pass with whoever else moves
`RulesetVersion`."* Ten other task files mention `RulesetVersion`; **whether any of them has a move
queued is not answerable from this repo**, and it is cheap to ask before level 3 rather than at
landing time.
