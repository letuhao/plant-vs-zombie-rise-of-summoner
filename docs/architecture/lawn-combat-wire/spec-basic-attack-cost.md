# Spec: `basic-attack-cost`

**Program:** `lawn-combat-wire` · **Map:** [../lawn-combat-wire-map.md](../lawn-combat-wire-map.md)
**Depends on:** `resource-subtick`, `basic-attack-grant`

---

## Objective

Make the lawn basic attack cost a resource, per the existing design: **the basic attack consumes a
resource; no resource, no trigger** (D2, owner). The rule is already implemented at the right place —
`UsabilityEvaluator.cs:66` → `CostLedger.Check` → `UsabilityReason.CannotAfford`, with the real ledger
passed at `BasicAttack.cs:163-166`, so an unaffordable action is never declared.

It is **vacuous today** for four separate reasons, and all four must land together.

Success: a lawn actor pays `stamina` per swing, regenerates it over time, and contributes no elemental
delta while empty.

## Tech stack

`FusionRpg.Core` (cost ledger, pools, regen) + `FusionRpg.Injector` (the lawn's cost gate, the regen
kernel kind). Unity-free Core half.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "FullyQualifiedName~CostLedger|ActorResourcePools"
dotnet test tests/FusionRpg.Core.Tests --filter "Category=BalanceGuard"
```

## The four wires — any three without the fourth ships the feature silently inert

| # | Wire | Inert line today |
|---|---|---|
| 1 | **A `stamina` cost row for `act.attack`** | `BattleRunState.cs:81` `Costs: Array.Empty<CompiledActionCost>()`. Delivered as data by `basic-attack-seed`; this module consumes it |
| 2 | **Seed `resource.max.*` on the lawn Hub** | `ActorHub.cs:147` `seedResourceBaseline = false`; sole `true` caller is `UniqueActorHubCompose.cs:75` (Server). The injector's Hub (`CheatState.cs:49`) never opts in, so **max is 0 for every lawn actor** |
| 3 | **Regen as a third kernel kind** | `KernelDriveHost.cs:69-70` has `KindDotPulse` and `KindShieldUpkeep`; regen is not among them |
| 4 | **The lawn's missing `CostLedger` call** | `grep CostLedger src/FusionRpg.Injector` → **zero hits**. The lawn has pools but no gate |

**Wire 2 is the dangerous one.** With max 0, an actor can never afford anything, so under D6 it
contributes no delta — which is *exactly indistinguishable from the bug this whole program exists to
fix*. Shipping 1, 3 and 4 without 2 would look like the feature simply doesn't work.

## Exhaustion suppresses the rider, not the shot

We do not own PvZ's sim loop and may not modify vanilla projectile behaviour
(`combat-damage-ssot.md:611`), so "no resource, no trigger" **cannot stop the pea from flying**. Out of
stamina ⇒ the shot lands for its vanilla/`attackDamage` number and **no elemental delta is added**; the
actor waits for regen and triggers again.

Same underlying rule as battle — *the RPG gates only the RPG's own contribution* — applied at a
different level of control over the clock. **Verified there is no coupling that would also drop the
stat bleed:** `progression.bonus.*` reaches Unity through a compose-time `EntityStatWriter` write,
independent of packet creation.

**Explicitly out of scope:** decaying an exhausted actor's stats. That belongs to a resource-exhaustion
feature with real statuses/debuffs (owner, 2026-09-13); doing it ad-hoc here would overlap and confuse
the status system.

## Cost cadence

- **One payment per swing**, not per victim — a piercing pea hitting five zombies is one attack
  (`lawn-hit-entry`'s swing-id dedupe).
- **Regen on the 100 ms kernel**, carry-corrected, with the sub-tick unit from `resource-subtick`. Not
  a frame counter: 60 frames is not a second unless the machine holds 60 fps, which would make regen
  hardware-dependent.
- Cost is authored **against regen**, never against max (`action-ideal.md:223`). With regen on a
  real-time grid this is a rate-vs-rate comparison and needs no round↔second fiction.

## Lawn pool lifecycle — state it, do not inherit it

`resource-hub-ssot.md:284` says pools *"persist across a run and refill **at rest**"*. **The lawn does
not do that**, and the spec must say so rather than silently contradicting the lock:

- `KernelDriveHost` runs only between `BeginBoard`/`EndBoard` (`MatchHost.cs:127,153,189`) and ticks
  with `* Time.timeScale` — so regen does not run paused, and does not exist between matches.
- Pools are created full per ptr and dropped on death (`LawnActorResourcePools.cs:38,50`).

**Declared rule: lawn pools are match-scoped and full at spawn.** If that is wrong, amend
`resource-hub-ssot.md` explicitly — do not leave the two in conflict.

## Code style

One authority. The lawn calls the **same** `CostLedger` battle does; per-context differences are
authored **data**, never a second gate — a parallel cost path would be a §2.15 SOLID failure.

`CostLedger.Check` inspects only `OnCommit`-timed rows; an `OnDeclare` cost would be invisible to the
declare-time gate. Author the basic attack's cost as `onCommit` unless that is deliberately changed.

## Testing strategy

| Level | Cases |
|---|---|
| Core unit | An actor with stamina pays once per swing; five victims of one bullet cost one payment |
| Core unit | An actor at zero stamina declares no action → no packet, no delta |
| Core unit | The same actor after regen triggers again |
| Core unit | `progression.bonus.*` still reaches Unity while exhausted — the stat bleed is untouched |
| Core unit | Pool max is non-zero for a lawn actor once wire 2 lands (the regression that catches a silent-inert ship) |
| Core unit | Regen accrues on the 100 ms grid and does not run while paused |
| Regression | Battle behaviour unchanged where no cost row is authored for a mode |
| Live | `lawn-combat-live-proof`'s falsifier |

## Boundaries

- **Always:** one `CostLedger`; per-context cost is data.
- **Always:** land all four wires together.
- **Ask first:** authoring the actual stamina/regen numbers — those are a balance act, and balance is
  a separate program (D1). Ship a documented placeholder, per this repo's own "shipping a guess is
  fine, calling it balance is not" posture.
- **Never:** a lawn-only cost gate; decay stats on exhaustion; charge per victim; suppress the vanilla
  shot.

## Success criteria

- [ ] A lawn actor's `resource.max.stamina` is **non-zero** — the anti-silent-inert check.
- [ ] One swing costs one payment regardless of victim count.
- [ ] At zero stamina: the pea still flies, no elemental delta is added, and the stat bleed is intact.
- [ ] Regen restores the actor and the next swing contributes again.
- [ ] Lawn pool lifecycle is stated in the spec and matches the code, or `resource-hub-ssot.md` is
      amended.
- [ ] No second cost gate exists; `guard-actor-hub` green.
