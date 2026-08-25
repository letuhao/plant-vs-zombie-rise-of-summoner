# Spec: defence-actions (A8)

Module **A8** in the [action map](../action-map.md). Depends on **A5** and on the battle-timeline kernel's **B6** (the reaction lane), which is unbuilt.

> **Vocabulary reconciled 2026-08-24.** This spec previously called its reaction shape *block*, while the
> `derived-stats` program was concurrently specifying `block.rate` / `block.strength` as **passive** stat
> contests. Same word, two mechanisms, and the collision would have shipped. The category here is
> **guard**; `block` and `parry` are stats. §0 is the boundary, and the two are complementary rather than
> competing. See [../derived-stats/spec-evasion-chain.md](../derived-stats/spec-evasion-chain.md).

## Objective

Make defending a **choice** as well as a stat: **guard**, and brace, as first-class actions, and the reaction lane's first real content.

Today defence is entirely passive — `combat.defense.*`, shields, and dodge all happen to you. Nothing an actor *does* is defensive, so no defensive decision exists.

## 0. Guard is an action; block and parry are stats

The two layers answer different questions and **compose** — a guard action raises the very stats the passive layer then rolls.

| | **Guard** (this module) | **Block / parry** ([evasion-chain](../derived-stats/spec-evasion-chain.md)) |
|---|---|---|
| What it is | An **action** the defender chooses | A **stat contest** that happens on incoming hits |
| Cost | `stamina` / `WReact`, on a cooldown | None — it is passive |
| Effect | Applies a **timed buff** to defensive channels | Rolls per hit and removes damage |
| Decision | The player's | The build's |

**The composition is the point.** Guarding grants a status that raises `combat.defense.*`,
`status.resist.*`, and `block.rate` / `parry.rate` themselves — so a guarded actor blocks *more often*
because they guarded. Neither layer duplicates the other: one is a moment, the other is a disposition.

**A8 authors no damage math.** It grants buffs; the damage layer keeps owning what those buffs do — the
same boundary §6 already draws for shields.

### 0.1 The payload dependency this creates

A guard buff is a **status that writes derived channels**. Two facts make that a real dependency rather
than an assumption:

- `StatusPayloadKind.ModifyStat` has **zero consumers repo-wide** ([atom-catalog-ssot.md](../effect-atom/atom-catalog-ssot.md)). The owner decided 2026-08-22 to implement one; **A8 is a consumer of that decision**, not a workaround for it.
- `stat.derived` atoms are **quarantined (D6)** until the atom program's **E12**.

So A8's stance shape can ship as an ordinary action, but **its buff payload lands only once a
`ModifyStat` consumer exists.** Named here so it is a scheduled dependency rather than a mid-build
surprise.

## Design (locked on approval)

### 1. Two shapes, and the distinction is the module

| Shape | When declared | Lane | Example |
|---|---|---|---|
| **Stance** | On your own turn | Ordinary `W` slot | *Brace* — spend your turn raising mitigation until your next turn |
| **Reaction** | On someone else's action | **`WReact`**, a separate pool | *Guard* — spend something to reduce a hit as it lands |

A stance is an ordinary action and needs nothing new; the whole reason `A8` waits on `B6` is the second row.

**`WReact` is a separate pool from `W`, and that is not an optimisation.** A defender in `Recovering` must still be able to guard. Sharing one width would make guarding depend on whether you happened to be idle, which is not a defensive system — it is a scheduling accident.

### 2. Reactions are actions, so nothing here is exempt

The membership rule holds: a reaction costs resource or time and needs a cooldown, so it is an action. Same table, same costs, same usability gates, same cooldown ledger.

Two consequences worth stating because they are easy to special-case away:

- **A reaction pays even when the hit misses.** Committing is what costs. Guarding a swing that would have missed anyway is a real, intended loss.
- **A reaction is gated by `A4` like anything else** — cooldown, affordability, range, condition. A defender out of `stamina` cannot guard, and the refusal is `CannotAfford(stamina)`, not silence.

Its cooldown reads `skill.cooldown.defense` from the derived catalog
([spec-skill-modifiers.md](../derived-stats/spec-skill-modifiers.md)) — **not an envelope-local channel.**
That module closes [action-map.md:177](../action-map.md)'s *"no cooldown-reduction channel"* gap for
every category at once, and A8 is one of its consumers.

### 3. Bounded nesting, following the precedent that already exists

A reaction resolves **inside** the triggering resolution. That needs a depth limit, and this codebase already runs one for exactly this shape — `ProcDepthLimit`, and the event pipeline's generation cap.

> **Restated invariant: `Resolving` is atomic with respect to the clock, not with respect to the stack.**

Exceeding the depth **drops the reaction and emits telemetry**. It never recurses, and it never silently succeeds. A reaction chain that hits the limit is content wrong, and it should be visible as such.

**Shared budget, not a parallel one.** `ProcDepthLimit` is the same counter
[spec-reflection.md](../derived-stats/spec-reflection.md) consumes. A guard reaction nested inside a
reflection bounce must not get a fresh allowance — two independent limits let a chain exceed either
budget while satisfying both.

### 4. A reaction budget, separate from the turn budget

Without one, every defender guards every hit and the interesting decision disappears. The budget is spent from a different pool than the actor's turn — otherwise guarding silently costs you your next action, which is a different mechanic that nobody chose.

### 5. Interrupt and reaction are different mechanisms

Easy to build one as the other, so name them:

| | What it is | Who acts |
|---|---|---|
| **Interrupt** | Breaking an attacker's committed action before it resolves | Something happening *to* the attacker |
| **Reaction** | A defender acting inside the attacker's resolution | The defender's own action, on `WReact` |

They share no code path. A guard is not an interrupt, and stunning a caster is not a reaction.

### 6. Shields are the natural payload, and stay where they are

`shield.grant` is a shipped atom kind with full battle support, so *brace* is a container granting a shield and needs no new effect machinery.

What this module must **not** do is move shield mechanics. `ShieldRuntime` is shipped, specced, and test-locked; `A8` authors actions that grant shields. The damage layer keeps owning absorption.

**Guard buffs are the second payload shape** (§0.1), and the same rule binds: A8 authors the grant, the
damage layer owns what the raised channels do.

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
- **A guard nested in a reflection bounce shares one depth budget** (§3) — the cross-module case neither spec would catch alone.
- **A reaction pays on a missed hit**, asserted on the pool rather than on the outcome.
- **Reaction budget exhaustion stops further guards** in the same round, and the refusal names the budget.
- **A guard is not an interrupt** — an architecture test that the two paths share no code. Cheap now; expensive once one has grown into the other.
- **Guarding raises `block.rate`, it does not replace the block roll** (§0) — the test that keeps the two layers from re-merging.

## Boundaries

- **Always:** keep `WReact` separate from `W`; bound the nesting on the **shared** counter; make reactions pay like any action; author shields and buffs rather than reimplement them; read cooldown from `skill.cooldown.defense`.
- **Ask first:** any change to `ShieldRuntime`; reactions outside the depth limit; a reaction that can trigger another reaction of the same kind.
- **Never:** a shared width with `W`; unbounded nesting; a second depth counter; a reaction that skips `A4`; merging the interrupt and reaction paths; **naming anything in this module `block` or `parry`** — those are stats (§0).

## Success criteria

1. Defending is a decision with a cost, **and** a disposition from stats — §0's two layers both exist and compose.
2. A defender mid-recovery can still guard.
3. `WReact = 0` leaves the game byte-identical.
4. The nesting stack is bounded on the **shared** `ProcDepthLimit`, visible when hit, never recursive.
5. No identifier in this module is named `block` or `parry`.
