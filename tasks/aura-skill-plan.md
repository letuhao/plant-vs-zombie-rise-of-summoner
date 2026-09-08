# Implementation plan: aura-skill

**Map:** [../docs/architecture/aura-skill-map.md](../docs/architecture/aura-skill-map.md) ·
**Ideal:** [../docs/architecture/aura-skill-ideal.md](../docs/architecture/aura-skill-ideal.md) ·
**Specs:** [../docs/architecture/aura-skill/](../docs/architecture/aura-skill/) ·
**Defect log:** [../docs/architecture/derived-pipeline-audit-2026-08-30.md](../docs/architecture/derived-pipeline-audit-2026-08-30.md)

**Status: proposed 2026-08-30.** Tasks: [aura-skill-todo.md](aura-skill-todo.md).

---

## Overview

A commander runs a continuously-active aura that buffs their own side; the enemy is affected through
the contest differential rather than a second grant. Twelve aptitudes, eleven side-wide auras plus
**Focus**, which reverses and buffs the commander's own action cooldowns.

**This plan is front-loaded with its riskiest task, deliberately.** Two adversarial audits established
that there is **no path in the tree from "an aura is on" to "a derived channel moved"**, and that
battle's one runtime-computed derived path runs **once at `BattleEngine` construction** — channels are
frozen for the match (`BattleEngine.cs:30,46`: `Derived` is get-only, one call site).

The owner chose to **add a recompose seam** rather than accept a pre-match-only model. That is kernel
work against deterministic, golden-tested code, so it sits in **Phase 0** — where failing is cheap and
sends the toggle model back for a decision, rather than being discovered after four phases built on
top of it.

**Phases 0 and 1 carry shippable value regardless.** The entire HoMM3 half, three pre-existing defect
fixes, and the magnitude formula are independent of every aura unknown.

## Architecture decisions carried in

| # | Decision | Source |
|---|---|---|
| Q7 | Aura grants to **own side only**; the enemy half is emergent from the contest | owner, 2026-08-29 |
| Q8 | **One active at a time**, `maxActiveAuras` tunable, oldest evicted | owner, 2026-08-30 |
| Q10 | **Two axes multiply** — `k(rung) · share^γ · P(Θ)` via the shared read function | owner, 2026-08-30 |
| Focus | **Reverses** — buffs the commander's own action cooldowns, `RelationKind.Self` | owner, 2026-08-30 |
| — | **Omni channels**, closed by arithmetic (`omni + element` is additive, `Σw = 1`) | audit, 2026-08-30 |
| — | **Divisive cooldown** — matches shipped `TurnReadiness.EffectiveRate` | audit, 2026-08-30 |
| — | **Battle first** — `decisions.md:92` standalone-first is binding | audit, 2026-08-30 |

## The acceptance rule — three gates, not one

The map's single end-to-end rule was correct in intent but unusable as a gate: it can only be ticked at
the very end, so it gives no signal until then. Split:

| Gate | Assertion | Runnable |
|---|---|---|
| **A — magnitude** | a hand-computed expected value for a named `(k, share, Θ)` | **today** |
| **B — delivery** | an aura in `BattleSetup` raises `combat.power.omni` on a friendly squad actor by that value; absent, it does not | after T12 |
| **C — toggle** | disabling returns the channel to its prior value | after T4 + T13 |

**Gate C depends entirely on T4.** Battle is match-frozen today; the recompose seam is what makes gate
C assertable at all. If T4 cannot be made golden-neutral, gate C is deleted and `aura-action-shape`
collapses into a pre-match loadout choice — that outcome goes back to the owner, it is not engineered
around.

---

## Phase 0 — foundation and the seam

> **The spike this plan originally opened with is no longer needed.** Its central question — *is battle
> match-frozen?* — was answered by reading rather than building: `BattleEngine.Derived` is a get-only
> property assigned once, with exactly one `BattleStatComposer.Compose` call site in the whole battle
> tree (`BattleEngine.cs:30,46`). **Battle is match-frozen. Confirmed.**
>
> The owner's decision (2026-08-30) is to **add a recompose seam** rather than accept a pre-match-only
> model, which preserves the full toggle/evict design. That makes T4 kernel work against
> deterministic, golden-tested code — the highest-risk task in the program, and the reason it sits in
> Phase 0 where a failure is cheap rather than late.

Phase 0 also lands three pre-existing defect fixes that are worth doing regardless of auras, and one
(**T3**) that **must** precede any authored action row.

## Phase 1 — the HoMM3 half, independent of every aura unknown

- **T5 + T6 — the HoMM3 half.** Wiring the allocation delegate and `Θ` hydration makes the commander's
  level and stats reach live lawn plants and zombies **with zero aura content**. Highest
  value-to-risk in the program, and it ships even if every aura task stalls.
- **T7 + T8 — `OVERLAY-COMBAT`.** Independent, lawn-only, closes a loop open since 2026-08-20.
  ⚠️ It **does not unblock any aura** — it gates the reader, not the writer.

Phase 0's defect work (T1–T3) is worth doing on its own merits too: `Overlay` replace-not-add is a live
hazard today, and **T3 must land before any authored action row** or the first one poisons the battle
log permanently.

## Phases 2–4 — commanders, delivery, content

Now plannable in detail, because the four blocking owner decisions landed on 2026-08-30: the recompose
seam, commanders as real actors (**Crazy Dave and Dr. Zomboss**), D3's "both" fix, and a **tunable**
tier-mapped rung band.

The R3 answer also closes the Zomboss question: if both commanders are real actors, *"equal commanders
cancel"* becomes reachable (**T17**) rather than aspirational — which matters, because own-side-only
rests entirely on that property.

**Two tasks are L and must be split before they start** — T9 (commander identity / loadout ownership /
pools) and T15 (equip endpoint / persistence / UI). T12 and T18 likewise.

---

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| **T4's recompose seam moves goldens** | **High** — the toggle model dies; gate C deleted; T13 collapses to a pre-match choice | T4 is Phase 0 so this surfaces early and cheaply. **Stop-and-ask, do not engineer around** |
| Delivery needs `stat.derived` un-quarantined after all | **High** — an atom-layer change against a closed vocabulary | T12 owns the decision; escalate rather than assume |
| **T3 lands late and an action row is authored first** | **High** — permanently poisoned battle log rows that re-throw on every replay | T3 is Phase 0 and is a **hard ordering rule** at the top of the todo |
| No production own-side oracle exists | Medium | T12 either builds one or uses a narrower selector for v1 **and says so** |
| Fixing D1 without D2 | Medium | Same task. The plan does not allow them to separate |
| `guard-class-system.ps1` is **currently red** (G3 Might/Ferocity double-counted atk) | Medium | Pre-existing, unrelated. Named so nobody attributes it to this program — but T6 adds a third contribution to the same channels and must not make it worse |
| Zomboss never runs auras | Medium | Closed by the R3 decision — both commanders are real actors, so T17 delivers "equal commanders cancel" |
| Task sizing drift | Medium | **T9, T12, T15 and T18 are L and must be split before they start.** Named in the todo rather than left to discover |

## Open questions (owner)

**All four blocking questions were answered by the owner on 2026-08-30** — recompose seam, commanders
as real actors, D3 "both", tunable tier-mapped rungs. What remains is non-blocking:

1. **The `commanderOnly` item role** — a second, unacknowledged answer to "how does the commander buff
   the squad", never authored. Whether its atoms stack with aura atoms, and against which budget, is
   undecided. **Re-verified still open 2026-08-30:** it exists in authored item seed data
   (`data/seed/items/_registry/classes.v1.json`, `core.v1.json`, `naming.v1.json`,
   `affix-families/g-precision.json`) and in `spec-aura-content.md`, with **zero consumers in `src/`**.
   Still an owner decision.
2. ~~**W4 has a gate and no owner** — `aura-content` gates Retribution on it and `aura-surface` tests it;
   nothing fixes it.~~ — **CLOSED 2026-08-30, this question is stale.** `actorResolve` is passed at all
   five production `DispatchInstant` call sites (`EffectBag.cs:488`/`:557`, `StatusEffectBridge.cs:80`/
   `:123`, `CheatCommandRunner.cs:1326`), and it is not an inert null — `EffectRuntime.cs:436` assigns
   `bag.ActorResolve = InjectorCombatBridge.ResolveActor` on the real injector and
   `FoundationHarness.cs:112` does so for the harness. Found by re-reading this plan against code
   during Phase 5's final-proof pass; the wording above ("only the argument is missing") described a
   state that no longer exists.
3. **`patron.aura` becomes irrelevant** past ~15 points of commander investment (it clamps at 150‰, an
   aura does not). Coherence, not a defect.

## Verification commands

```powershell
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
dotnet test tests\FusionRpg.Data.Tests
.\scripts\guard-class-system.ps1        # NOTE: currently RED, pre-existing
.\scripts\guard-power.ps1
python scripts\audit-magic-numbers.py --targets M1
python scripts\audit-overflow.py
```
