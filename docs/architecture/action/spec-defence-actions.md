# Spec: defence-actions (A8)

Module **A8** in the [action map](../action-map.md). Depends on **A5** and on the battle-timeline kernel's **B6** (the reaction lane), which is unbuilt.

## Objective

Make defending a **choice** rather than a stat: block, guard, and brace as first-class actions, and the reaction lane's first real content.

Today defence is entirely passive — `combat.defense.*`, shields, and dodge all happen to you. Nothing an actor *does* is defensive, so no defensive decision exists.

## Design (locked on approval)

### 1. Two shapes, and the distinction is the module

| Shape | When declared | Lane | Example |
|---|---|---|---|
| **Stance** | On your own turn | Ordinary `W` slot | *Brace* — spend your turn raising mitigation until your next turn |
| **Reaction** | On someone else's action | **`WReact`**, a separate pool | *Block* — spend something to reduce a hit as it lands |

A stance is an ordinary action and needs nothing new; the whole reason `A8` waits on `B6` is the second row.

**`WReact` is a separate pool from `W`, and that is not an optimisation.** A defender in `Recovering` must still be able to block. Sharing one width would make blocking depend on whether you happened to be idle, which is not a defensive system — it is a scheduling accident.

### 2. Reactions are actions, so nothing here is exempt

The membership rule holds: a reaction costs resource or time and needs a cooldown, so it is an action. Same table, same costs, same usability gates, same cooldown ledger.

Two consequences worth stating because they are easy to special-case away:

- **A reaction pays even when the hit misses.** Committing is what costs. Blocking a swing that would have missed anyway is a real, intended loss.
- **A reaction is gated by `A4` like anything else** — cooldown, affordability, range, condition. A defender out of `stamina` cannot block, and the refusal is `CannotAfford(stamina)`, not silence.

### 3. Bounded nesting, following the precedent that already exists

A reaction resolves **inside** the triggering resolution. That needs a depth limit, and this codebase already runs one for exactly this shape — `ProcDepthLimit`, and the event pipeline's generation cap.

> **Restated invariant: `Resolving` is atomic with respect to the clock, not with respect to the stack.**

Exceeding the depth **drops the reaction and emits telemetry**. It never recurses, and it never silently succeeds. A reaction chain that hits the limit is content wrong, and it should be visible as such.

### 4. A reaction budget, separate from the turn budget

Without one, every defender blocks every hit and the interesting decision disappears. The budget is spent from a different pool than the actor's turn — otherwise blocking silently costs you your next action, which is a different mechanic that nobody chose.

### 5. Interrupt and reaction are different mechanisms

Easy to build one as the other, so name them:

| | What it is | Who acts |
|---|---|---|
| **Interrupt** | Breaking an attacker's committed action before it resolves | Something happening *to* the attacker |
| **Reaction** | A defender acting inside the attacker's resolution | The defender's own action, on `WReact` |

They share no code path. A block is not an interrupt, and stunning a caster is not a reaction.

### 6. Shields are the natural payload, and stay where they are

`shield.grant` is a shipped atom kind with full battle support, so *brace* is a container granting a shield and needs no new effect machinery.

What this module must **not** do is move shield mechanics. `ShieldRuntime` is shipped, specced, and test-locked; `A8` authors actions that grant shields. The damage layer keeps owning absorption.

### 7. Golden impact

`WReact = 0` must be **byte-identical to having no reaction lane at all**. That is what allows the lane to exist in code before any content uses it, and it is the property to assert rather than hope for.

Defence content that actually fires changes outcomes, so it joins the movers bucket — never `A5`.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DefenceAction"
```

## Structure

```
src/FusionRpg.Core/Actions/Defence/ReactionSeat.cs   (WReact acquisition, depth guard)
tests/FusionRpg.Core.Tests/Actions/Defence/
```

The stance shape adds **no files** — it is an ordinary action row. If it needs code, it has been modelled wrongly.

## Testing strategy

- **`WReact = 0` is byte-identical to no lane** — the assertion that lets the lane ship dark. Run the eight goldens with the lane compiled in and the width at zero.
- **A defender in `Recovering` can still react** — the reason the pool is separate. With a shared width this test fails, which is the point of writing it.
- **Depth limit drops with telemetry and never recurses** — a deliberately self-triggering reaction chain, asserting both the drop *and* the emitted signal. A silent drop passes a naive test.
- **A reaction pays on a missed hit**, asserted on the pool rather than on the outcome.
- **Reaction budget exhaustion stops further blocks** in the same round, and the refusal names the budget.
- **A block is not an interrupt** — an architecture test that the two paths share no code. Cheap now; expensive once one has grown into the other.

## Boundaries

- **Always:** keep `WReact` separate from `W`; bound the nesting; make reactions pay like any action; author shields rather than reimplement them.
- **Ask first:** any change to `ShieldRuntime`; reactions outside the depth limit; a reaction that can trigger another reaction of the same kind.
- **Never:** a shared width with `W`; unbounded nesting; a reaction that skips `A4`; merging the interrupt and reaction paths.

## Success criteria

1. Defending is a decision with a cost, not a stat that happens to you.
2. A defender mid-recovery can still block.
3. `WReact = 0` leaves the game byte-identical.
4. The nesting stack is bounded, visible when it is hit, and never recursive.
