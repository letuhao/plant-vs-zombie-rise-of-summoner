# Spec: duration-resolver (A14)

**Status: proposed 2026-08-27.** Module **A14** in the [action map](../action-map.md). New module, from the
sealed [action-ideal.md](../action-ideal.md) §5 — decisions **10, 14**.

Depends on **A1**. Consumed by every action that applies a status.

## Objective

**Make "how long" mean the same thing in a 12-round battle and a 40-minute lawn run**, and stop a control
duration from becoming a permanent lock as it climbs the rung ladder.

## Design

### 1. The reference is the VICTIM, not the fight

An earlier draft anchored duration to fight length. That was retracted: **fight length exists in one mode
only.**

> `MaxRounds` has exactly **one reader** — `BattleEngine.cs:306`, where it produces a `Stalemate` — and the
> injector never references `BattleRuleset` at all. It is a **battle-mode loop guard**, and deriving a
> universal bound from it pushes a web-engine constant into the lawn, violating the standalone-first
> boundary in the injector direction.

The anchor that exists in every mode is **the victim's own action cadence**.

> **Control duration is authored in VICTIM TURNS and resolved to ticks at apply time.**

*"Stun for 2 of your turns"* is meaningful in a 12-round battle, in a 40-minute lawn run, at `Θ`=10 and at
`Θ`=5,000 — **without a single tick constant anywhere.**

| Property | Why it falls out |
|---|---|
| **Mode-free** | no fight-length reference, so nothing leaks between the battle engine and the lawn |
| **`Θ`-free by construction** | both sides scale, so it is contest-shaped and PS-3 is satisfied without trying |
| **Still rides the rung** | a top-rung control steals more turns than rung 1 |
| **Safe without an absolute bound** | *"you lose at most N of your actions"* is a **bounded ratio** — PS-8 exempt, **and the declaration must say so in a comment** |

### 2. Only control uses the relative form

**Because only control removes agency.**

| family | bound | authored in |
|---|---|---|
| **control** — stun, freeze, root | small, tunable | **victim turns** |
| **DoT / debuff** | none needed — it kills or expires | ticks |
| **buff / stance** | none | ticks |

Giving all three the relative form would be machinery for two families that never had the failure mode.

### 3. Clamp-and-convert — what makes it a SOFT cap

When duration hits its bound, **the rung's remaining growth converts into intensity.**

A top-rung control is then not *"the same stun, longer"* but *"the same stun, far harder to resist."*
Nothing is lost, it is **redirected** — which is what makes it a soft cap rather than a ceiling, and what
satisfies PS-8's *"removed or made a configurable soft cap."*

**No new machinery:** [status-ssot.md](../status-ssot.md) already splits `status.duration.*` from
`status.intensity.*` with identical omni/category/perId shape.

#### 3.1 ⛔ The leak that makes the difference between a real cap and a decorative one

The shipped apply pipeline is:

```text
effectiveDuration = baseDuration x durationNetFactor
  where durationNetFactor comes from
    status.duration.{omni,category,id} - status.durationReduction.{...}
```

**That chain is uncapped today.**

> **The clamp must be the LAST step of Phase 2, after `durationNetFactor`** — never a validation on the
> authored row. A clamp applied at authoring time is one a duration-stacking build walks straight through:
> the difference between a cap that holds and a cap that reads correct in the catalog and never fires in a
> fight.

### 4. Per-mode resolution behind one interface

```csharp
long ToTicks(int victimTurns, ActorRef victim);   // one method, one implementation per mode
```

| Mode | Resolution | State |
|---|---|---|
| **battle** | `turn.speed` → `nextReadyTick` | ⛔ **BLOCKED — neither exists** |
| **PvZ lawn** | a cadence to be measured | ⛔ open, deferred by owner |

> ### ⛔ C1 — both modes are blocked, and the battle one was written as if it worked
>
> **Verified 2026-08-27, by grep rather than by reading:**
>
> - `DerivedTurnChannels.Speed` is a **`const string`**. `DerivedStatRegistry` has **zero `turn.*`
>   entries** — the channel is not registered and has no reader.
> - `nextReadyTick` **is not computed**. The only `Readiness` in the kernel is the enum member
>   `TimelineEventKind.Readiness = 0`.
>
> The caps register already said so: *"`turn.speed` / `turn.haste` / `turn.moveSpeed` stay
> **declared-but-unregistered vocabulary**, owned by the battle stream."*
>
> **This is `A1` §6's `resource.delta` defect — declared, named and inert — reproduced in a spec written
> the same day.**
>
> **So this module ships the SEAM and no resolver.** `IDurationResolver` lands; `BattleDurationResolver`
> waits on a **Phase 0 dependency: the battle stream registering `turn.speed` with a reader.** A resolver
> that reads a channel returning nothing is worse than none, because it looks finished.

**Authored content never changes when the lawn answer arrives.** That is the point of the seam: the day a
real lawn is measured, it is **one implementation**, not a re-authoring of every control action. Same shape
as `Relation` compiling to `TargetSpec[2]` — **author once, resolve per mode.**

### 5. The PvZ clock — real time drives, wall-clock is never stored

[battle-turn-ideal.md](../battle-turn-ideal.md) §4 already specifies the profile: *"Inverted: the game's
frame clock is the source and we **sample** it into ticks."*

And, in the same section, the invariant: *"Replay is virtual-time replay… **Wall-clock never enters the
recording.**"* A duration stored in real milliseconds breaks byte-identical replay, which is what the
content-hash and golden apparatus rest on.

> **Sample the frame clock into integer ticks at the boundary; store ticks.** The lawn gets real-time
> pacing at no determinism cost.

### 6. ⚠️ The seconds trap

`status.apply` takes **`duration` in SECONDS as a float** — *"FA2 predates the integer-ms rule and was not
changed for it; declaring `durationMs` here would validate a key nothing reads."* Everything else in this
repo is integer ms.

**This module owns the conversion**, and it is the one place the float appears. Nothing downstream of
`ToTicks` sees a float.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~DurationResolver"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Status"
```

## Structure

```
src/FusionRpg.Core/Actions/Duration/IDurationResolver.cs   (the seam - ONE method)
src/FusionRpg.Core/Actions/Duration/BattleDurationResolver.cs
src/FusionRpg.Core/Actions/Duration/DurationClamp.cs       (clamp-and-convert; runs LAST)
tests/FusionRpg.Core.Tests/Actions/DurationResolverTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| **The `Deep Freeze` fixture** | 4s Freeze + 10s Chill against a ~1s turn is a **14-turn lock**. Authored in turns it is bounded; the test asserts the *authored* form and the *almanac* form differ, which is the whole reason the module exists |
| Two actors, `turn.speed` differing 2× | the same authored "2 turns" resolves to **different tick counts** — the claim of the module, asserted directly |
| `Θ`=20 vs `Θ`=5,000 | resolved turns **identical**; PS-3 satisfied by construction, not by clamping |
| Clamp position | a duration-stacking actor with large `status.duration.omni` is **still bounded** — a planted authoring-time clamp **fails this test** |
| Clamp-and-convert | at the bound, further rungs raise **intensity**, and total effect is asserted to keep rising — a hard clamp fails |
| Bounded-ratio comment | an architecture test asserts the PS-8 exemption comment exists at the declaration |
| DoT and buff families | resolve in **ticks**, never through the turn path — a planted turn-authored DoT is rejected |
| No resolver registered | throws naming the mode, **never** silently defaults to ticks |
| Float leakage | an architecture test: no `float`/`double` crosses `ToTicks`'s boundary |
| Determinism | integer ticks only; no wall-clock read anywhere in the module — purity scan |

## Boundaries

**Always:** author control in victim turns; clamp after `durationNetFactor`; convert the excess to
intensity; store ticks.

**Ask first:** the lawn cadence reference (open, deferred); giving DoT or buffs the relative form.

**Never:** an absolute tick bound derived from `MaxRounds`; a wall-clock value in a stored duration; a
clamp at authoring time; a silent default when no resolver is registered.

## Success criteria

1. One authored control duration resolves correctly in battle, and the lawn is **one implementation away**
   with no content change.
2. A duration-stacking build is bounded, proven against a planted authoring-time clamp.
3. At the bound, further rungs still increase total effect via intensity.
4. `Θ` never moves a resolved turn count.
5. No float and no wall-clock survives the boundary.
