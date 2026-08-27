# Spec: defence-actions (A8)

**Status: REWRITTEN 2026-08-27** against the sealed [action-ideal.md](../action-ideal.md) §2.2 — decision
**3**. Module **A8** in the [action map](../action-map.md).

> ## ⛔ This module is NO LONGER blocked on timeline B6
>
> The previous revision filed **guard** as a *reaction*, on the unbuilt `WReact` lane, and waited on the
> battle-timeline kernel's **B6**. The sealed ideal makes guard a **stance**.
>
> **This spec's own text is the argument:** *"A stance is an ordinary action and needs nothing new; the
> whole reason A8 waits on B6 is the second row."* Guard is now the first row, so **B6 is no longer this
> module's gate.** The reaction lane stays a real, later feature.
>
> ### ⚠️ But it lands AFTER `A5`'s gate passes, not inside it
>
> An earlier line here said *"A8 ships with `A5`."* That is wrong, and `decisions.md`'s **Golden ordering
> across streams** says why: **"freeze first, move last — if a mover overlaps a freezer, neither can
> attribute a hash change to its own work and the freezer's proof is worthless."**
>
> `A5` is a **freezer**; `A8` is a **behaviour change**. Guard moves no golden while no actor guards, but
> *"it should not move one"* is exactly the claim a freezer exists to prove — and it cannot prove it about
> itself while something else is landing in the same window. **`A8` is unblocked, not un-sequenced.**

Depends on **A1**, **A3**. No longer depends on **B6**.

## Objective

Make defending a **choice** as well as a stat.

Today defence is entirely passive — `combat.defense.*`, shields, and dodge all happen to you. Nothing an
actor *does* is defensive, so no defensive decision exists.

## 0. Guard is an action; block and parry are stats

**Vocabulary reconciled 2026-08-24 and unchanged:** the `derived-stats` program specifies `block.rate` /
`parry.rate` as **passive stat contests**. Same word, two mechanisms, and the collision would have shipped.

| | **Guard** (this module) | **Block / parry** (evasion-chain) |
|---|---|---|
| What it is | an **action** the defender chooses | a **stat contest** on incoming hits |
| Cost | `poise` — three parts, §2 | none |
| Effect | raises defensive channels while held | rolls per hit and removes damage |
| Decision | the player's | the build's |

**They compose, and that is the point.** Guarding raises the very stats the passive layer then rolls — so a
guarded actor blocks *more often* because they guarded. One is a moment, the other a disposition.

**A8 authors no damage math.** It grants; the damage layer keeps owning what the grant does.

## Design

### 1. A stance is an ordinary action, plus a status

**No new FSM state.** The shipped machine is `Charging → Ready → Committed → Resolving → Recovering`.

```text
raise    an ordinary action; resolves once
held     a self-granted STATUS; A4 refuses every other action while it holds
release  a SECOND action with its OWN action_id -> the riposte
```

> ### The release has its own `action_id` — decided, not left open
>
> An earlier draft said *"a second action (or the same action toggled)"*. That is ambiguous, and **three
> consumers need the answer**:
>
> | Consumer | Why it matters |
> |---|---|
> | `CooldownLedger` | keys `(ActorKey, CooldownKey)` — one id means raise and release **share one clock** |
> | `A4` gate 0 | must answer *"is this action the release?"* — an id comparison, or a flag |
> | `A15` dedup | groups by `action_id` |
>
> **Distinct id.** It makes gate 0 an ordinal comparison rather than a flag lookup, gives the riposte its
> own cooldown and its own rung, and lets an item grant a better release without touching the raise.

**Every other action carries a refusal while the stance holds** — including movement. *Guard-while-moving is
a different skill, not a basic action.* That is `A4`'s condition gate, not new machinery.

> **If this module grows a runtime of its own, something is wrong** — the same claim `A9` makes for
> movement, and for the same reason.

### 2. ⛔ Three findings that are not stylistic

#### 2.1 It must NOT hold a `W` slot while enabled

A8's earlier text scoped a stance to *"until your next turn."* **This one is indefinite**, and at `W = 1` an
indefinite hold **freezes the entire board** — the exact failure `A9` §1 names for movement: *"if movement
took a slot, at `W = 1` only one actor on the board could ever move."*

> **Guard consumes a slot to RAISE, then releases it. The status persists, not the slot.**

`slot_consuming` is `true` for the raise and the released riposte, and the held state occupies nothing.

#### 2.2 It needs a per-tick hold cost, or it trips the HARD criterion

Two actors both guarding forever deal and take nothing — `netAttrition ≤ 0` on both sides, which is the
**termination invariant**, and `decisions.md` makes it **blocking**: *"no later layer can repair a pool that
refills faster than it drains."*

`when = perTick` already exists, and *"failing to pay ends the action through the interrupt path"* is shipped
semantics — so **a mutual guard resolves arithmetically, with no special case.**

> This is a **third** cost component beyond [spec-guard-economy.md](../class-system/spec-guard-economy.md)'s
> flat-commit-plus-absorb-drain, which was decided for guard-as-a-proc. **A stance needs the third.**

#### 2.3 Guard pays `poise`, not `stamina`

`decisions.md`'s Resource model, amended 2026-08-26: *"**`poise` pays for guarding**… `stamina` no longer
claims guard."* [resource-hub-ssot.md](../resource-hub-ssot.md) §2's older line listing guard under
`stamina` is stale and is on `A3`'s reconciliation list.

### 3. The `poise` economy — one ratio, three parts

| Part | When | Rule |
|---|---|---|
| flat commit | raising | *committing costs, always* |
| absorb drain ∝ what the guard stopped | per absorb | *output is priced* |
| **per-tick hold** | while held | **termination** (§2.2) |

**Regen is sized LOW against peer pressure** — `r = poiseRegen / peerPressure < 1`, so **heavy hits break
the guard and attrition does not**. `r ≥ 1` is unbreakable and is the same defect the termination invariant
names. Per-encounter is the `r = 0` corner of the same continuum, not a rival to it.

**`poise` at zero is a broken guard, not death** — an exhaustion status, like every pool except `hp`. **The
exhaustion must not touch `poise`'s own regen channel** (`A3` §7's self-regen cycle).

### 4. The riposte — the release, and BASTION's missing offence

**Spent `poise` converts to damage on release.**

FORCE spends `stamina` to attack and FINESSE spends `qi` to cast; BASTION spends `poise` to **block**. Two
postures had an offence economy and one did not.

> A guard that costs nothing when it stops nothing would also **produce** nothing, and BASTION would still
> have no way to win. The riposte is what makes the absorb-drain shape *necessary* rather than merely tidy.

The conversion share is a **bounded ratio over an uncapped pool** — `[0,1]`, PS-8 exempt, **and the
declaration must say so in a comment.** It is not a cap on damage: output scales with `Θ` because the pool
does.

### 5. Brace, and the reaction lane

**Brace** — spend your turn raising mitigation until your next turn — is the *bounded* stance and ships
alongside guard. It is an ordinary action granting a timed buff.

**Reactions** — acting inside someone else's resolution, on a separate `WReact` pool — remain specced and
**deferred to timeline B6**. Everything below is recorded so the later module does not re-derive it:

- **`WReact` is a separate pool from `W`**, not an optimisation. A defender in `Recovering` must still be
  able to react; sharing one width would make reacting depend on whether you happened to be idle.
- **A reaction pays even when the hit misses.** Committing is what costs.
- **Bounded nesting** via `ProcDepthLimit` — the **same** counter reflection consumes, never a parallel
  allowance. Exceeding it **drops the reaction and emits telemetry**; it never recurses and never silently
  succeeds.
- **Interrupt and reaction are different mechanisms.** An interrupt breaks an attacker's committed action;
  a reaction is the defender's own action. They share no code path.

### 6. The payload dependency

A guard buff is a **status that writes derived channels**. `StatusPayloadKind.ModifyStat` had zero consumers
repo-wide; **E17 shipped one**, so this is a satisfied dependency rather than an open one. Recorded because
the earlier revision listed it as a risk.

Shields stay where they are: `shield.grant` is a shipped atom kind, `ShieldRuntime` is test-locked, and this
module **authors actions that grant shields** rather than moving shield mechanics.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DefenceAction"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Poise"
```

## Structure

```
src/FusionRpg.Core/Actions/Defence/StanceRuntime.cs   (raise / hold / release; NO new FSM state)
src/FusionRpg.Core/Actions/Defence/PoiseLedger.cs     (commit + absorb + hold)
src/FusionRpg.Core/Actions/Defence/Riposte.cs         (bounded ratio, PS-8 comment)
tests/FusionRpg.Core.Tests/Actions/DefenceActionTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| **At `W = 1`, one actor guards and another acts** | passes — **and a planted `slot_consuming` hold FAILS**, which is what makes §2.1 more than a sentence |
| **Two mutual guards** | **terminate**; a planted zero-hold version **hangs** |
| Every other action while the stance holds | refused with a typed reason naming the stance — **including movement** |
| Guard-while-moving | a **separate action**, not the basic one; asserted as two different `action_id`s |
| Raising with zero `poise` | refused by affordability, not by silence |
| `poise` reaching zero mid-hold | **exhaustion**, guard breaks, **actor does not die** |
| The exhaustion debuff | **does not touch `resource.regen.poise`** — rejected at load if it does |
| `r = poiseRegen / peerPressure` | **< 1**, asserted from emitted metrics across two seeded scenarios: one heavy-hit (**must break**), one attrition (**must not**) |
| Riposte | spent `poise` converts; output **scales with `Θ`** (uncapped pool), and the bounded-ratio comment exists |
| Guard raises `block.rate` | a guarded actor blocks measurably more often — the composition claim, asserted |
| No new FSM state | an architecture test: `TurnState` is unchanged |
| Goldens | unmoved while no actor guards |

## Boundaries

**Always:** release the `W` slot after the raise; charge the per-tick hold; pay `poise`; keep the riposte a
bounded ratio with its comment.

**Ask first:** the riposte share; making guard interruptible; a second stance shape.

**Never:** a new FSM state; an indefinite `W` hold; `stamina` as guard's cost; an exhaustion that reduces
`poise` regen; moving shield mechanics into this module; a parallel proc-depth allowance.

## Success criteria

1. Guard ships with `A5` — **no B6 dependency**.
2. At `W = 1` the board is not frozen, proven against a planted slot-holding version.
3. Two mutual guards terminate, proven against a planted zero-hold version.
4. A broken guard is exhaustion, never death.
5. `r < 1` is a measured number from emitted metrics, not an impression of a fight.
