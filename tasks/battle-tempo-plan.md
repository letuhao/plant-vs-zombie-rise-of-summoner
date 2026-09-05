# Implementation Plan: `battle-tempo`

Program `battle-tempo` — [capability map](../docs/architecture/battle-tempo-map.md) · **eight** module
specs under [docs/architecture/battle-tempo/](../docs/architecture/battle-tempo/).
Task list: [battle-tempo-todo.md](battle-tempo-todo.md).

**Task count, corrected 2026-09-05:** **31 tasks across eight modules** — 23 complete, 1 partial
(`RL2`), 3 open (`LAND1`, `LAND2`, `RL3`), 4 newly added (`BR1`–`BR4`, below). ⚠️ This header
previously read *"27 of 28 tasks complete or correctly partial; 2 blocked; 1 owner-only"* — arithmetic
that summed to 30 against a real total of 27, and a module count that called `timeline-dispatch` an
"8th module" when it was the 7th. Both corrected by counting.

**New module, 2026-09-05: `battle-resources`**
([spec-battle-resources.md](../docs/architecture/battle-tempo/spec-battle-resources.md)) — the module
that finally gives a battle actor something to spend. `TD4` found that
`BattleStatComposer.cs:120-128` seeds **no `resource.*` channel at all**, so every battle actor holds
all six pools at max 0 and `reaction-lane`'s counter declines every time by correct logic on empty
input. Scope is forced wider than `poise` alone by `resource-hub-ssot.md` §8's own normative
six-coverage rule (*"a family that covers a subset is a defect, never a feature"*), so the module
seeds all six by looping `ResourceIds`. **This unblocks `RL2` (partial → complete) and `RL3` (blocked
→ measurable) without an owner decision** — per the 2026-09-05 gate rule, the poise question failed
both gate bars (it is reversible, and no default was ever named), so it ships behind a tunable marked
`unmeasured` rather than stalling the plan.

**Status, 2026-09-05: 27 of 28 tasks complete or correctly partial; 2 tasks genuinely blocked on named,
external gates; 1 owner-only.** Built: `poise-unification` (all 4), `action-timing` (all 4),
`tempo-content` (both), `MEAS`, `commitment-binding` (both), `reaction-lane` (RL1/RL4 complete, RL2
partial — see below), `forecast-rail` (all 4), and an 8th module, `timeline-dispatch` (D14's fix, all
four tasks): **`TD1`** specs it ([spec-timeline-dispatch.md](../docs/architecture/battle-tempo/spec-timeline-dispatch.md)),
**`TD2`** lands its additive pieces, **`TD3`** builds and measures the dispatch branch itself, and
**`TD4`** live-wires `reaction-lane`'s own counter mechanism into it. Every completed task is probed
against real compiled code with an executed falsifier (`Core.Tests` itself stays blocked by
pre-existing, unrelated WIP in other streams — see PU1's own evidence).

**D14 is resolved — built and measured, not just specced.** `spec-timeline-dispatch.md` gives the
built design: a local, per-round discrete-event dispatch (`RunTimelineActionPhase`) behind an opt-in
profile flag defaulting `false` for every shipped profile, so it moves no golden by construction.
Measured: `W` +12.92 percentage points win rate (W=1 vs W=4), `Commitment` −0.725 average rounds-to-win
(EarlyBound vs LateBound), both falsified against a flag-off control. Two real, previously-undiscovered
defects surfaced and were fixed along the way, both confirmed inert for the atomic path:
`BasicAttackEnvelope.Commitment` was hardcoded, making `DefaultCommitment` unreachable regardless of
dispatch completeness; and a dead attacker's own already-scheduled `Resolve` could still land a hit
(fixing this moved the `W` numbers above from their first, buggy measurement). This closes
`battle-tempo-todo.md` Checkpoint B and unblocks `LAND1`/`LAND2`'s dependency.

**`LAND1`'s own sweep has now been run, per the owner's explicit direction** ("run the sweep, stop
before sign-off"): flag staged on for `HybridAtb`, full sweep measured, flag reverted — nothing landed,
no golden re-blessed, no `RulesetVersion` bump. A THIRD real defect surfaced and was fixed in the
process (a non-monotonic clock feeding per-battle-persistent `Cooldowns`/`ResourcePools`, caught only by
a real-content seed, never by a synthetic probe). Result: **zero win-rate movement on the existing
golden shape** (verified as a real "moved nothing," not a silent no-op), but a small, real shift on more
volatile, asymmetric content. `LAND2` (the win-rate sweep sign-off, **owner-only by the plan's own
original design**, not a gate discovered after the fact) remains untouched and correctly gated — the
sweep above is the input to that decision, not a substitute for it.

**`RL2`'s own remaining gap is now built too** (`TD4`): the defender's counter-intent wiring
(`ReactionLane.TryEnter`/`ReactionCounter.TryCounter`/`Exit`) fires for real inside
`RunTimelineActionPhase`, using a new per-battle resource-pool registry (`LawnActorResourcePools`,
reused verbatim) and a new dedicated tunable file (`data/tuning/reaction-lane.v1.json`). That wiring
surfaced a THIRD real, previously-undiscovered gap: every battle actor's resource pools (poise
included) are always empty, because no derived-stat composer sets a `resource.max.*`/`resource.regen.*`
channel for a battle actor (grep-confirmed across `BattleStatComposer.cs`/`BattleEffects.cs`/
`BattleDerivedModifierLedger.cs`). The lane correctly, honestly declines every counter today — the
mechanism works; nothing gives a battle actor poise to spend yet, and deciding how much is a
balance/design question outside this program's own "intent, cost, and payoff" scope for `RL2`. `RL3`
stays blocked on that gap plus its own pre-existing, independent dependency on the owner-gated Phase 2
sweep. See the map's own D14 entry and the todo's `TD1`–`TD4` evidence for the full reasoning.

---

## Overview

`B34`'s staged sweep measured three of the engine's four scheduling axes at **exactly 0.00 %**:
`AdvancePolicy`, `W` and `Commitment`. All three zeros have **one cause** — every action's
`ActionEnvelope.WindupTicks` is `0`, so `Committed → Resolving` is instantaneous and no slot is ever
held, no commitment can be tested, and no defender can react.

This program gives actions time, gives species a speed, and turns on the two capabilities that become
observable once a window exists. **It is content and wiring, not architecture** — the envelope, the
columns, the profile rows, the reaction lane and the forecast projection are all built already.

---

## Architecture decisions carried in from the specs

These are settled. The plan implements them; it does not revisit them.

| # | Decision | Consequence for the build |
|---|---|---|
| **5 / 8** | Wind-up is **payoff-scaled**, driven by **`qPowerMilli`** | One coefficient, no second curve. `PowerBudgetMilli` is `long?` and null on pre-column tables — it would leave wind-up undefined |
| **D1** | The cap is **relative to `roundDurationMs`**, never an absolute literal | A configurable soft cap in tuning; `DurationMin/MaxTicks` already reserve the shape |
| **D2** | Timing derives at **catalog build**, not in the seeder | ⛔ **No Python change at all** — `ContentValidation.Budget` is C# |
| **11** | The basic attack's token is **a felt beat** | ⚠️ Bigger balance pass; it sits on the floor every actor shares |
| **D5** | `action-timing` + `tempo-content` land **together** as one mover | One `RulesetVersion` bump, one re-bless, one sweep, one sign-off |
| **D6 / D11** | Re-selection resolves the **already-compiled** `ActionTargetSpec` | ⛔ No second selection seam. `BattleRunState.BasicAttackCompiled` already carries it |
| **D3 / 9** | The rail renders **`BattleTrace.Turns`** — a record, not a forecast; trace **opt-in** | ⚠️ The boot sweep shares the resolve helper — see Risks |
| **12** | A counter's damage **is** `Riposte(spent, cap)` — the spend is the attack | Uses shipped, tested code instead of a new damage path |
| **13** | The two `poise` stacks are **reconciled now** | New root module `poise-unification`; refuse semantics win |

---

## Dependency graph

```
poise-unification ─────────────────────────────┐
                                               │
action-timing ──┬── commitment-binding ── reaction-lane
                │
tempo-content ──┴── forecast-rail
```

**Build order:** `poise-unification` · `action-timing` · `tempo-content` (three parallel roots) →
`commitment-binding` → `reaction-lane` · `forecast-rail`

⭐ **`poise-unification` is deliberately first.** It is the only module that touches no battle path,
moves no golden, and blocks nothing — both `poise` stacks have zero production callers. It can land
before the mover and de-risks `reaction-lane` early.

---

## Phase structure, and why it is shaped this way

The phase boundaries are **golden-movement boundaries**, not feature boundaries. `decisions.md`'s
*Golden ordering across streams* is explicit that two re-bless events cost two sweeps and two owner
sign-offs for the same goldens — so everything that moves goldens is squeezed into **one** landing, and
everything after it must be provably byte-identical on top.

| Phase | Modules | Golden movement |
|---|---|---|
| **0** | `poise-unification` | **None** — provable: zero production callers |
| **1** | `action-timing` + `tempo-content`, built and **measured, not landed** | none yet — measurement runs against a staged profile |
| **2** | ⛔ **The single landing** — bump, re-bless, sweep, sign-off | **All of the program's** |
| **3** | `commitment-binding` | byte-identical on top |
| **4** | `reaction-lane` | byte-identical on top (`classic-round` provably untouched) |
| **5** | `forecast-rail` | none — reads and renders only |

⚠️ **Phase 1 builds but does not land.** The staged sweep must size each axis *separately* before the
joint re-bless, because the re-bless itself cannot separate them. This is the `B34` shape applied in
advance, and it is the only chance to get attribution.

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| ⛔ **A joint re-bless cannot attribute the two deltas** | High | Staged sweep in Phase 1, *before* landing. Measure wind-up alone, then tempo alone, then both. Record all three |
| ⚠️ **A "felt beat" on the shared combat floor is a large balance pass** | High | Owner accepted it (decision 11). Size it in Phase 1's sweep and report the win-rate delta *before* the bump, so the number is a decision and not a surprise |
| ⛔ **The boot sweep shares `ResolveAndIngest`** (`WebMatchService.cs:229` → `:241`) | Medium | Thread the trace as a **parameter defaulting to null**; pass it only from `:109`/`:150`. ⚠️ Picking "the right call site" would silently trace the bulk path |
| ⚠️ **Phase 0 edits another program's finished work** — class-system P7.1–P7.3, 12 green tests | Medium | Migrate every property, never delete one. Update `spec-guard-economy.md` in the same pass so no doc describes dead behaviour |
| ⚠️ **Refuse-vs-floor overturns a documented PS-8 rationale** | Medium | State the reasoning in the code comment and the spec: PS-8 forbids *progression ceilings*, not affordability; `stamina`/`qi` already refuse through the same `TrySpend` |
| ⚠️ **Golden fixtures may barely move** — `BattleGoldenTests` builds actors from its own builders, not the species catalog | Low | Measure before predicting, as `B35` did. A small delta is a finding, not a failure |
| ⚠️ **`M1` regression** — timing numbers are exactly the kind that get inlined | Low | `audit-magic-numbers.py` in every phase's verification, with a planted-literal falsifier |

---

## Verification standard for every task

Beyond the repo's Definition of Done:

1. ⭐ **A falsifier for every behavioural assertion.** A passing test proves nothing until it can fail —
   break the code on purpose and confirm the test reddens.
2. **Measure, don't predict.** Three predicted movers in `battle-timeline` moved nothing, and one
   unpredicted mover also moved nothing. Report what the goldens actually did.
3. **Real rows, not fixtures**, wherever content is involved — the `AuthoredEligibilityResolves`
   lesson: a synthetic row proved the mechanism while the shipped content stayed unreachable.
4. **Guards** — `guard-single-writer`, `guard-funnel-delta`, `guard-dal`, `guard-secondary-no-unity` —
   plus `audit-overflow.py` and `audit-magic-numbers.py` on any touched path.

---

## Coverage audit — 2026-09-05

Every spec's **testing strategy** and **success criteria** were walked item by item against the task
list. **Six gaps, and two of them were spec bugs rather than plan bugs.**

| | Spec item with no task | Fix |
|---|---|---|
| ⛔ | `forecast-rail` §5.2 / SC2 — *"the projection mutates nothing"*. The ideal calls `TurnOrderForecast` a pure projection; **nothing asserted it** | New task **FR4**. ⭐ Runs independently of the rail — it guards the property §2.1 depends on and needs no surface, DTO or trace |
| ⛔ | `reaction-lane` §5.1, §5.4, §5.6 — all four `ReactionOutcome` values, nested-resolution determinism, depth unreachable by ordinary content | New task **RL4** |
| ⚠️ | `commitment-binding` §5 — *"the envelope overrides the profile"*. Precedence was in the acceptance criteria but nothing verified it | Added to **CB1** verify |
| ⚠️ | `poise-unification` §5.3 — one pool proven visible to `Resolve` **and** `SettleAll`; and `PhaseModel`'s regen parameter left untouched | Added to **PU1** verify |
| ⚠️ | `tempo-content` §6.4, §6.6 — `swift` not double-counted; `M1 = 0` for `referenceIntervalMs` | Added to **TC1**/**TC2** verify |

**The two spec bugs — both caught by checking the specs against code, not against each other:**

- ⛔ **`spec-action-timing.md` §6.3a asserted against `powerBudgetMilli`** — the driver **decision 8
  rejected**. The design sections were updated when that decision landed; the testing section was not.
  ⭐ **The test itself would have re-introduced the second curve the decision removed.**
- ⛔ **`spec-reaction-lane.md` §5.1 named a `ReactionOutcome.LaneClosed`** that does not exist. The real
  value is **`NoLane`** (`ReactionLane.cs:9`).

⭐ **What the audit did not find:** any module with no tasks, any success criterion with no verification
after these five additions, and any task not traceable to a spec. Coverage is now **23 tasks across six
modules**, with every spec's §6/§8 items mapped.

---

## Open questions

⚠️ **This section read "None" until 2026-09-05 and that was wrong** — `TD4` had already surfaced a real
one, and the claim that "the only numbers still open are tunables… rather than blockers" was the
specific sentence that hid it. Recorded honestly now:

1. ⛔ **What gives a battle actor its resource pools.** Not a tunable — a wiring gap.
   `BattleStatComposer` seeds no `resource.*` channel, so all six pools are max 0 for every battle
   actor and no action in a battle can cost anything. Specced as `battle-resources`; the coefficients
   ship marked `unmeasured` and the sizing is a later balance pass. **Not a gate** (2026-09-05 gate
   rule: reversible, and it now has a named default).
2. ⚠️ **Whether Phase 2 is still worth landing as scoped.** `LAND1`'s sweep measured **zero** win-rate
   movement on the existing golden shape — so the attribution risk the whole phase structure was built
   around does not arise for today's goldens. `LAND2` is therefore a smaller decision than this plan
   originally implied: not *"approve this shift"* but *"approve a change that provably moves nothing
   today and will matter on volatile content later."* Owner's call, and it should be made against that
   framing rather than the original one.

Thirteen decisions remain settled across the map's two review rounds; the eleven findings are folded
into the module specs. The remaining **balance numbers** (wind-up coefficient, the relative cap,
`referenceIntervalMs`, the counter's poise spend range, and now the resource baselines) are Phase 1 /
Phase 4 / `battle-resources` outputs rather than blockers.

⛔ **One owner gate is real and must not be self-approved:** the Phase 2 win-rate sweep sign-off, on the
`combat-unification-plan.md:76` precedent.
