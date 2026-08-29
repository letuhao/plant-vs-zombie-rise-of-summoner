# Derived stats — capability map

**Program id:** `derived-stats` · **Branch:** `features/derived-stat-extension`
**Status:** Map **approved 2026-08-24**. All **12 module specs written**. **Built and verified
2026-08-25** — see [tasks/derived-stats-todo.md](../../tasks/derived-stats-todo.md) for the phase-by-
phase evidence (all 7 phases, all 7 checkpoints cleared). Owner review and commit pending.

| # | Spec | Owns |
|---|---|---|
| 1 | [spec-stat-taxonomy.md](derived-stats/spec-stat-taxonomy.md) | four classes, pair rule, mitigation-order rule, divisor rule, `guard-stat-pairs.ps1` |
| 2 | [spec-cap-consolidation.md](derived-stats/spec-cap-consolidation.md) | **one home for a channel cap** — kills a live double-clamp bug, retires 3 dead columns |
| 3 | [spec-catalog-extension.md](derived-stats/spec-catalog-extension.md) | the 157, R1 first, **both** classes per channel, count-canary update |
| 4 | [spec-element-families.md](derived-stats/spec-element-families.md) | 16 element families, §6 generated not listed |
| 5 | [spec-status-potency.md](derived-stats/spec-status-potency.md) | duration/intensity split, Q1's one term, 4 staleness fixes |
| 6 | [spec-skill-modifiers.md](derived-stats/spec-skill-modifiers.md) | cooldown (race), effectiveness (feeder, pinned pre-mitigation) |
| 7 | [spec-actor-channels.md](derived-stats/spec-actor-channels.md) | resources, `move.range`, xpRate, breakthroughSuccess |
| 8 | [spec-healing-pair.md](derived-stats/spec-healing-pair.md) | `heal.power` unpaired `Pool`, finishes `leech` |
| 9 | [spec-mitigation-chain.md](derived-stats/spec-mitigation-chain.md) | pen/absorption, amp/reduction placement |
| 10 | [spec-evasion-chain.md](derived-stats/spec-evasion-chain.md) | parry/block on a **single attack table**, `ClampedContest` extracted from shield |
| 11 | [spec-reflection.md](derived-stats/spec-reflection.md) | reflect pairs, shared `ProcDepthLimit` |
| 12 | [spec-unbuilt-reconcile.md](derived-stats/spec-unbuilt-reconcile.md) | the finding register, standing channel-claim guard |

Reconciled outside the program directory: [action/spec-defence-actions.md](action/spec-defence-actions.md) — block → **guard** (F2).
**Artifacts:** specs → `docs/architecture/derived-stats/spec-<module-id>.md` ·
plan → `tasks/derived-stats-plan.md` · tasks → `tasks/derived-stats-todo.md`
(the bare `tasks/plan.md` / `todo.md` pair belongs to the perf stream — [AGENTS.md](../../AGENTS.md)).

**Approved input:** [actor-hub-ssot.md §3H](actor-hub-ssot.md) — 157 new channels, 99 → 256, the
four-class taxonomy, R1–R5 reconciliations and Q1–Q6 all decided 2026-08-24.
**External inventory:** [../research/chaos-derived-stats-audit.md](../research/chaos-derived-stats-audit.md).

---

## 1. What kind of program this is

**A reconcile program, not a greenfield one.** Nothing here invents a subsystem. Every module either
(a) registers channels into a catalog that already exists, or (b) makes an existing mechanism read
them. The second half of the work is equally important and easier to forget:

> **Six subsystems are approved-but-unbuilt or mid-build.** Adding 157 channels underneath them
> silently invalidates parts of their specs. Those specs get corrected **in this program**, while
> correction is free — after they ship it is a rebalance.

| Subsystem | State (from [decisions.md](decisions.md)) | What §3H changes underneath it |
|---|---|---|
| Battle time model | Approved 2026-08-21, **not yet built** | `turn.*` stays unregistered but is now formally **race** class; readiness/haste reasoning must cite it |
| Action model | Approved 2026-08-22, **not yet built** | `move.range` registers here, not there; `skill.cooldown.*` is race class; two cost pools |
| Resource model | Approved 2026-08-22, **not yet built** | `resource.max/regen/efficiency` become real channels; exhaustion composes through the catalog |
| Shield layer | Approved 2026-08-21, **in progress** | `block.*` reuses its contest helper; the 3-shield cap is now load-bearing on a *different* decision (Q6) |
| Combat resolution SSOT | **Building** | §6.7's pipeline order becomes the **normative rule** deciding feeder vs contest |
| Effect atoms | Shipped, with 8 declared-inert + `leech` half-built | **`stat.derived` is quarantined (D6)** and its family library is sized at *"12 generated families (~420 rows)"* — see §1.1 |

### 1.1 Two consequences found while surveying, not obvious from §3H

**`action-map.md` is about to invent a channel this program already owns.** Its line 177 records
*"our envelope has `SpeedChannel` but **no bounds and no cooldown-reduction channel** — a real gap this
program should close"*, and D3 (line 200) schedules adding one to `ActionEnvelope`.
**§3H's `skill.cooldown.{category}` is that channel.** Left alone, two programs ship two answers to one
question — precisely the failure the power ladder was written to end. `unbuilt-reconcile` points D3 at
the catalog instead of a new envelope field.

**The atom corpus roughly doubles.**
[effect-atom/atom-family-library.md §3.2](effect-atom/atom-family-library.md) sizes `stat.derived` as
**12 generated families (~420 rows)** — `12 × 7 slots × 5 tiers`. At 28 families that is **~980**, and
each of the 16 new families needs flavour names authored per element (the existing pattern is
*Ember / Frost / Gale / Stone / Radiant / Umbral*). **That authoring is not in this program's scope** —
it is the item/affix corpus's — but it is a cost this program creates and must hand over deliberately.

**Hard dependency, stated plainly:** `stat.derived` has **no executor in any runtime** until the atom
program's **E12** wires `BattleStatComposer` to read bound atoms at squad build. Until then every new
channel is registered, composable and readable by code — but **not authorable as content that binds**.
This program does not block on E12 and must not claim otherwise; it ships the catalog, and content
follows when E12 lands.

---

## 2. Modules

Stable kebab-case ids. Referenced by every downstream plan and task.

| Module id | Responsibility | Depends on |
|---|---|---|
| `stat-taxonomy` | Make §H.0 normative: four classes (**contest · race · pool · feeder**), the pair rule, and the **mitigation-order rule** that decides feeder vs contest. Add a guard so a new contest-class family without a counterpart fails CI. **Also settles the divisor rule** — see below | — |
| `cap-consolidation` | **One home for a channel cap: `data/tuning`.** Kills the hardcoded `0.95` in `DerivedStatRegistry` and the redundant second clamp in `ResistanceEvaluator` — today the tunable `categoryResistCap` **cannot raise the cap**, only lower it. Retires `effect_channel_policy`'s three dead columns (registry version bump). **Blocks `catalog-extension`** — registering 157 more capped channels into a two-home system multiplies the defect | `stat-taxonomy` |
| `catalog-extension` | Register the 157. **Declare both classes per channel** — `statClass` (this program) and `unitClass` (the nine-class ledger in [design/spec-magnitude-and-units.md](../design/spec-magnitude-and-units.md) §3) — and materialize `GameUnits` as `long` **where they leave composition** — `DerivedStatDef` keeps `double`, per §10.7 (R5, corrected). Register every ratio cap in §11.6 with its PS-8 exemption comment. Keep [data/seed/derived-stats/catalog.json](../../data/seed/derived-stats/catalog.json) in step. Move the two count assertions onto a derived total | **`cap-consolidation`** |
| `element-families` | Semantics for the 16 element-typed families in [element-hub-ssot.md](element-hub-ssot.md) §6 — how each interacts with the matchup matrix. **R1 moved out**: the `decisions.md` restatement must land *before* `CombatChannelFamilies` grows, so it is `catalog-extension`'s first task, not this module's | `catalog-extension` |
| `status-potency` | Split Phase-2's single `netFactor` into **duration** and **intensity** deltas (R4). Add **Q1's one term** — `resist.{element}` in the combine rule, zero new channels | `catalog-extension` |
| `skill-modifiers` | `skill.cooldown.{cat}` as **race**; `skill.effectiveness.{cat}` as **feeder pinned pre-mitigation** — the placement *is* its pair, so the spec must state that moving it later is breaking, not a refactor | `catalog-extension` |
| `actor-channels` | `resource.max/regen/efficiency` × 5, `move.range`, `progression.xpRate`, `progression.breakthroughSuccess`. Honour the two §H.7 collisions (`RpgXpAwardMap.Award.PowerScale`; ADR P1's realm pin) | `catalog-extension` |
| `healing-pair` | `combat.heal.power` — flat, **unpaired `Pool`** (owner 2026-08-24); anti-heal is a status, not a channel. **Finish `leech`'s heal half** — declared since the atom catalog shipped, never built | `catalog-extension` |
| `mitigation-chain` | Where `penetration/absorption` and `amplification/reduction` enter [combat-damage-ssot.md](combat-damage-ssot.md) §6.7, as **differences, both halves uncapped**. Retitle §5's *"Deferred from Chaos"* to *v1 shipped / v2 planned* (**R3**) | `element-families` |
| `evasion-chain` | Parry and block procs. Resolution order **Hit → Parry → Block → penetration → defense**. **One shared contest helper with `shield.toughness ↔ shield.pen`** (Q6) — chip floor `0.10×`, pen cap `3×`, never a second curve | `element-families` |
| `reflection` | `reflect.rate`/`reflect.damage` and their resists. **Reuses `ProcDepthLimit` (6)** rather than a second depth counter — two reflecting actors must terminate | `mitigation-chain` |
| `unbuilt-reconcile` | Correct the six specs in §1 so none contradicts the new catalog or the taxonomy. **No new mechanics** — this module only removes disagreement | all |

**No cycles.** `evasion-chain` reads the shield helper but does not modify shield semantics; that
direction is one-way.

### 2.1 The divisor rule — `stat-taxonomy` owes one more answer

[battle-turn-ideal.md:153](battle-turn-ideal.md) computes readiness as
`nextReadyTick = now + (BaseCost × ActionRank × HasteFactor) / Speed`. **A race-class stat in a
divisor.** That is a shape the caps standard has no row for yet, and it needs one before the battle
program builds against it:

> **A race-class stat used as a divisor requires a floor above zero. That floor is a structural limit
> (division by zero is a crash, not a balance outcome), not a progression cap — so it is exempt from
> PS-8 and must say so in a comment.**

It also makes `Speed` a **denominator**, where the overflow guidance inverts: the danger is a very
*small* value, not a very large one. Both belong in `stat-taxonomy`'s spec so the battle program cites
a decided rule rather than re-deriving it.

`battle-turn-ideal.md:241`'s reserved *"speed family"* — `speed` · `haste` · `moveSpeed` ·
`climbSpeed` · `swimSpeed` · `flightSpeed` · `jumpHeight` — is **entirely race class** under the new
taxonomy. None of them ever needed a pair, which is now a stated rule rather than an unexamined
absence.

---

## 3. Build order

```text
stat-taxonomy
   └─► cap-consolidation          ← one home for a cap, BEFORE 157 more arrive
          └─► catalog-extension
                 ├─► element-families ──┬─► mitigation-chain ──► reflection
                 │                      └─► evasion-chain
                 ├─► status-potency
                 ├─► skill-modifiers
                 ├─► actor-channels
                 └─► healing-pair
                                          └─► unbuilt-reconcile
```

`mitigation-chain` and `evasion-chain` stay genuinely parallel: `evasion-chain` resolves on the
**existing** hit roll via a cumulative attack table, so it adds **zero RNG draws** and cannot perturb
the other's goldens. An earlier draft rolled parry and block separately and would have forced an
ordering constraint here.

Everything under `catalog-extension` at the same level is **parallel** — they touch different families
and different consumers.

`unbuilt-reconcile` runs **last on purpose**: correcting six specs against a moving target means doing
it twice.

---

## 4. Interfaces at the boundaries

Per the spec convention, a contract lives in the **provider's** spec, not the consumer's.

| Boundary | Owner | Contract |
|---|---|---|
| Channel id registration + validation | `catalog-extension` | Unknown channel → reject. Unchanged rule, larger catalog |
| Element family list + omni rule | `element-families` → [element-hub-ssot.md](element-hub-ssot.md) §6 | Actor Hub registers; Element Hub defines semantics |
| Pipeline position of every new modifier | `mitigation-chain` → [combat-damage-ssot.md](combat-damage-ssot.md) §6.7 | **Before mitigation = inherited pair. After = own pair.** The rule, not a convention |
| Saturation curve for pool-vs-piercer contests | `shield-system-spec.md` §2.4 (**existing**) | `evasion-chain` consumes it; does not fork it |
| Proc recursion bound | `combat-damage-ssot.md` (**existing** `ProcDepthLimit = 6`) | `reflection` consumes it |
| Status category → id table | `status-ssot.md` §9.5 (**existing**) | `status-potency` consumes it |

---

## 5. Standards every module must satisfy

Not aspirations — each has an audit or guard that already runs.

| Standard | Check |
|---|---|
| **Magnitudes are `long`**; never `float`; widen before multiplying; divide by 1000 last; overflow throws | `python scripts/audit-overflow.py` · [CLAUDE.md](../../CLAUDE.md) |
| **No hard progression ceilings** — a cap on a magnitude is a ceiling (PS-8). Bounded ratios are exempt **and must say so in a comment** | [power/ssot-power-scale.md](power/ssot-power-scale.md) §11 |
| **Balance surface is config** — every new scale/rate in `data/tuning/<domain>.v{n}.json`, never a literal | `python scripts/audit-magic-numbers.py` · [tunables-ssot.md](tunables-ssot.md) |
| **One power ladder** — contests read `Θ` (linear), magnitudes read `P(Θ)`. No private `f(level)` | `scripts/guard-power.ps1` |
| **Omni is additive-only** — `totalPower = omni + category`, never `omni × category` | [actor-hub-ssot.md](actor-hub-ssot.md) §3 ban |
| **Single writer / Funnel** — no ad-hoc Unity stat patches | `guard-single-writer.ps1` · `guard-funnel-delta.ps1` |

**New guard this program owes:** `stat-taxonomy` adds one — a **contest-class family declared without
a counterpart fails CI.** The counterbalance rule has been a principle held in the owner's head; every
module below depends on it, so it becomes executable here.

---

## 6. Checkpoints

| # | After | Gate |
|---|---|---|
| **0** | `stat-taxonomy` | Four classes normative · pair guard fails on a planted unpaired contest family · every existing family classified, including the two unpaired shield pools |
| **0b** | `cap-consolidation` | **`RaisingTheCapActuallyRaisesIt` green** (fails on `main` today) · exactly one clamp per cap · dead columns retired · **content-hash change and golden stability asserted separately** · goldens byte-identical at `0.95` |
| **1** | `catalog-extension` | 256 channels resolve · `long`/ratio split explicit per channel · seed catalog expands to the same 256 · **zero goldens moved** (registration alone changes no behaviour) |
| **2** | element + non-element surfaces | Each family readable end to end · `decisions.md` restated (R1) · `status.resist.{element}` term live with 0 new channels |
| **3** | `mitigation-chain` + `evasion-chain` + `reflection` | Every new modifier placed in §6.7 with its class named · one saturation curve, not two · two mutually reflecting actors terminate · **each moved golden attributed to exactly one module** |
| **4** | `unbuilt-reconcile` | All six specs in §1 free of contradiction · `python scripts/audit-magic-numbers.py` and `audit-overflow.py` clean · no spec claims a channel that is not registered |

---

## 7. Explicitly out of scope

Named so they are not smuggled in.

| Out | Why | Where it goes |
|---|---|---|
| **Primary stats** (STR/VIT/DEX/INT/SPI or the Tinh five) | Owner deferred 2026-08-24 | **Answered 2026-08-26** — the `class-system` program, module 1: [class-system/spec-primary-stats.md](class-system/spec-primary-stats.md). Twelve aptitudes, not five; a **source**, not a registered channel, so nothing here needs to change |
| **`element_mastery`** — per-element progression | Feeds §3H's element families but is a progression design, not a catalog one | **Answered 2026-08-26** — [spec-primary-stats.md](class-system/spec-primary-stats.md) §3.3: it is **not** a primary stat. Per-element is *flavour*, and aptitudes stop at `omni`, so it belongs to the `aspect` tier. Handed forward with two conditions: it owes a [power/ssot-power-scale.md](power/ssot-power-scale.md) §10 row or a proof it is not power-shaped, and PS-3 applies to it |
| Commander / economy / crafting / social stats | Owner: *"commander stats, we design them in map feature"* | World map program |
| `turn.speed` · `turn.haste` · `turn.moveSpeed` registration | Battle stream owns them; they register when it gives them a reader | Battle program |
| Element roster changes (adding a 7th element) | Generation makes it free; deciding it is Element Hub's | — |
| Any **balance value** for a new channel | T7 — a migration that also retunes is unreviewable | A separate tuning pass |

---

## 8. Related

- [actor-hub-ssot.md](actor-hub-ssot.md) §3H — the approved channel proposal
- [element-hub-ssot.md](element-hub-ssot.md) · [combat-damage-ssot.md](combat-damage-ssot.md) · [status-ssot.md](status-ssot.md) · [resource-hub-ssot.md](resource-hub-ssot.md) · [shield-system-spec.md](shield-system-spec.md)
- [../research/chaos-derived-stats-audit.md](../research/chaos-derived-stats-audit.md) — frozen external inventory
- [power/ssot-power-scale.md](power/ssot-power-scale.md) · [tunables-ssot.md](tunables-ssot.md) — the standards
