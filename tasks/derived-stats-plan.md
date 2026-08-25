# Implementation Plan — `derived-stats`

**Branch:** `features/derived-stat-extension` · **Map:** [../docs/architecture/derived-stats-map.md](../docs/architecture/derived-stats-map.md)
**Tasks:** [derived-stats-todo.md](derived-stats-todo.md) · **Approved input:** [actor-hub-ssot.md §3H](../docs/architecture/actor-hub-ssot.md)
**Status:** Plan — awaiting review. **No build authorized.**

> `tasks/plan.md` and `tasks/todo.md` belong to the **perf v3** stream. This program uses the prefixed
> pair per [AGENTS.md](../AGENTS.md).

---

## Overview

Register **157 new derived channels (99 → 256)**, make every existing mechanism read them, and correct
the six approved-but-unbuilt specs the new catalog invalidates — while correction is still free.

Twelve modules, twelve specs, all written and reviewed. This plan sequences them.

**Two things this program fixes that were not in its original scope**, both found while speccing and
both verified in shipped code:

1. **A live cap bug.** `status.resist.{dot|cc|contagion}` is clamped to `0.95` twice — hardcoded at
   compose, tunable at apply — so `categoryResistCap` can *lower* the cap but **cannot raise it**. A
   balance pass would edit that key and see nothing happen.
2. **A vocabulary collision** between `block` the stat and `block` the action, caught before either
   shipped. A8's category is now `guard`; the two compose rather than compete.

---

## Architecture decisions

**1. Phases 0 and 2 ship inert, and that is the point.** The taxonomy and the 157 channels land with
**no reader**. That looks like horizontal slicing and normally would be — here it is what makes every
later golden attributable. A channel no formula reads, defaulting to `0`, is arithmetically a no-op.
Same discipline that let the power program attribute every moved golden; the vertical proof is pulled
into Checkpoints 2 and 5 rather than dropped.

**2. `cap-consolidation` goes before `catalog-extension`, not after.** Registering 157 more capped
channels into a two-home system multiplies the defect. Fix the thing the new rows would multiply,
*then* add the rows.

**3. Doc ahead of code, twice.** `decisions.md`'s R1 restatement lands **before**
`CombatChannelFamilies` grows (the lock says "84"), and `element-hub-ssot.md` §6 becomes generated
before anything reads the new families. No window where a shipped lock contradicts shipped code.

**4. Goldens move in exactly one phase.** Phases 0–4 are byte-identical by construction. Phase 5 is
where behaviour changes, and each moved golden is attributed to one task. **A golden moving anywhere
else is a defect, not a rebalance.**

**5. Two classifications, and only one is ours.** `statClass` (Contest · Race · Pool · Feeder) answers
*does it need a counterpart*; `unitClass` — the ten-class ledger in
[design/spec-magnitude-and-units.md](../docs/design/spec-magnitude-and-units.md) — answers *what
arithmetic is it and how does it render*. Orthogonal, both required, **neither redefined here**.

**6. Parry and block resolve on the existing hit roll.** A cumulative attack table, one draw, so the
`SeededRng` stream is untouched and `evasion-chain` cannot perturb `mitigation-chain`'s goldens. An
earlier draft rolled separately and would have forced an ordering constraint between two modules that
are otherwise parallel.

---

## Dependency graph

```text
Phase 0  stat-taxonomy                        [inert - metadata + guard]
   │
   ▼
Phase 1  cap-consolidation                    [one real bug; goldens byte-identical at 0.95]
   │
   ▼
Phase 2  catalog-extension                    [inert - 157 channels, zero readers]
   │
   ├──► Phase 3  element-families              [semantics + docs, no code]
   │        │
   │        ▼
   │    Phase 5  mitigation-chain ─┬─► reflection
   │             evasion-chain ────┘           [GOLDENS MOVE HERE, and only here]
   │
   └──► Phase 4  status-potency ∥ skill-modifiers ∥ actor-channels ∥ healing-pair
                                                [parallel - different families, different consumers]
                     │
                     ▼
Phase 6  unbuilt-reconcile                     [last - correcting a moving target costs twice]
```

---

## Phases

| # | Phase | Modules | Goldens |
|---|---|---|---|
| **0** | Foundation | `stat-taxonomy` | none |
| **1** | One home for a cap | `cap-consolidation` | none at `0.95`; **content hash restamps** |
| **2** | Registration | `catalog-extension` | none |
| **3** | Element semantics | `element-families` | none |
| **4** | Non-element readers | `status-potency` · `skill-modifiers` · `actor-channels` · `healing-pair` | none at defaults |
| **5** | Combat chain | `mitigation-chain` · `evasion-chain` · `reflection` | **move, attributed per task** |
| **6** | Reconcile | `unbuilt-reconcile` | none |

Phase 4's four modules are genuinely parallel — different families, different consumers, no shared file.

---

## Checkpoints

| # | After | Gate |
|---|---|---|
| **0** | Phase 0 | Four classes normative in code, `§H.0` and the seed catalog · guard fails on **four** planted violations and passes on `main` · all 99 shipped channels classify, `shield.capacity`/`regen` landing in `Pool` · **`git status tests/` clean** |
| **1** | Phase 1 | **`RaisingTheCapActuallyRaisesIt` green — and observed failing first** · exactly one clamp per cap · dead columns retired · **content-hash change and golden stability asserted separately** · goldens byte-identical at `0.95` · a missing tunable rejects naming the channel |
| **2** | Phase 2 | **256 channels resolve**, count derived from `families × roster` · no non-element family in `AllCombatChannelIds` · seed catalog expands to exactly what `CreateDefault()` registers · **`git status tests/` clean** · composer allocation re-measured at 196, not assumed |
| **3** | Phase 3 | §6 states the generation rule, not a table · drift test covers **both** §6 and the stat sheet, and fails on a planted drift · **both** deferred lists retitled |
| **4** | Phase 4 | Long-weak **and** short-brutal expressible · `status.resist.{element}` read, zero new channels · effectiveness provably pre-mitigation · **four simultaneous exhaustion debuffs tested** · `leech` heals · **`git status tests/` clean at defaults** |
| **5** | Phase 5 | Every new modifier placed in §6.7 with its class named · **one** saturation curve · **zero extra RNG draws** · mutual and three-way reflectors terminate · **each moved golden attributed to exactly one task** |
| **6** | Phase 6 | All eleven findings resolved or deferred **with a reason** · `NoSpecClaimsAnUnregisteredChannel` green · no spec claims an unregistered channel · both audits clean |

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **The adversarial audit pass was never run.** Six of this session's own claims were falsified by reading; the passes run so far verify *citations*, not *reasoning* | **High** | Every task below carries a verification command, and each phase is byte-identical by construction so a wrong claim surfaces as a moved golden rather than as shipped behaviour. **Run the adversarial pass before Phase 5** — it is the first phase where a wrong claim cannot be caught by "goldens clean" |
| A golden moves outside Phase 5 | **High** | Every phase 0–4 task asserts `git status tests/` clean. A move means the claim "this reads nothing yet" was false — stop and find why |
| Content-hash restamp in Phase 1 read as a golden move | Medium | Checkpoint 1 asserts the two **separately**. A session seeing "hashes changed, goldens clean" will otherwise assume one is wrong |
| `cap-consolidation` changes behaviour while moving the cap's home | **High** | T1.1 is written and observed **failing** before T1.3 deletes anything. Goldens byte-identical at the shipped `0.95` is the acceptance criterion |
| `ClampedContest` extraction moves a shield golden | Medium | Landed as **two separate steps** — extract and prove byte-identical *before* parry/block exist |
| 196 channels overrun the composer's reference cache | Medium | Checkpoint 2 re-runs the allocation test rather than assuming. **2.3× the size the cache was measured against** |
| Reflection fails to terminate | **High** | `MutualReflectorsTerminate` and `ThreeWayReflectTerminates` written **before** the feature and observed failing. Shared `ProcDepthLimit`, never a second counter |
| Atom corpus doubling (~420 → ~980 rows) lands as a surprise on the item stream | Medium | T6.3 hands it over **by name**, with the E12 quarantine dependency stated. Not this program's authoring work |

---

## Out of scope

Named so they are not smuggled in: **primary stats** and **`element_mastery`** (deferred by the owner,
their own program); **commander/economy/social stats** (world-map program); **`turn.*` registration**
(battle stream, when it has a reader); **any balance value for a new channel** (T7 — extract with
values unchanged, tune separately); **atom corpus authoring** (item stream, handed over in T6.3); the
**`"ladderIndex"` web contract change** (web stream, recorded in T6.3).

---

## Open questions

**None blocking.** Three are recorded in module specs and each is answerable without owner input:

| Where | Question | Kind |
|---|---|---|
| `catalog-extension` §6.3 | Does the reference cache absorb 2.3×? | **Measurement** — Checkpoint 2 runs it |
| `actor-channels` §9 | Exhaustion debuff magnitudes | **Deferred by T7** — structure now, tuning separately |
| `reflection` §9 | Do shields absorb before reflection reads? | **Reading stated** (no), made falsifiable by `ReflectsPreShield` |
