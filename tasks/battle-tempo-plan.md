# Implementation Plan: `battle-tempo`

Program `battle-tempo` — [capability map](../docs/architecture/battle-tempo-map.md) · six module specs
under [docs/architecture/battle-tempo/](../docs/architecture/battle-tempo/).
Task list: [battle-tempo-todo.md](battle-tempo-todo.md).

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

**None.** Thirteen decisions are settled across the map's two review rounds; the eleven findings are
folded into the module specs. The only numbers still open are **tunables for the balance pass**
(wind-up coefficient, the relative cap, `referenceIntervalMs`, the counter's poise spend range), which
are Phase 1 and Phase 4 outputs rather than blockers.

⛔ **One owner gate is real and must not be self-approved:** the Phase 2 win-rate sweep sign-off, on the
`combat-unification-plan.md:76` precedent.
