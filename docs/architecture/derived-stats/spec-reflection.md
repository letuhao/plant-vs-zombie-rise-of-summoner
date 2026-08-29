# Spec — `reflection`

**Program:** `derived-stats` · **Map:** [../derived-stats-map.md](../derived-stats-map.md)
**Depends on:** `mitigation-chain` · **Last of the combat chain**
**Status:** Spec — awaiting review. Not built.

---

## 1. Objective

**Bounce damage back at the attacker, and prove it terminates.**

Four channels, two `Contest` pairs, both **role-inverted**:

| Defender raises | Attacker suppresses | Contest over |
|---|---|---|
| `reflect.rate` | `reflect.resist.rate` | does damage bounce |
| `reflect.damage` | `reflect.resist.damage` | how much bounces |

This is the only module in the program that **creates a new damage event**. Everything else modifies
one. That is the whole risk, and §2 is the whole spec.

---

## 2. Termination

Two actors with `reflect.rate > 0` reflect at each other. Without a bound this is an infinite loop on
the game thread.

**The mechanism already exists.** `ProcDepthLimit` (default **6**) is resolved per packet at
[CombatDamageDispatcher.cs:26](../../../src/FusionRpg.Core/Combat/CombatDamageDispatcher.cs) and
carried on the DTO ([CombatDtos.cs:112](../../../src/FusionRpg.Contracts/CombatDtos.cs)).

> **Reflection consumes `ProcDepthLimit`. It does not get a second depth counter.**

Two counters would let a reflection loop nested inside a proc chain exceed either budget while
satisfying both — the failure mode of every independent-limit design.

### 2.1 Three rules that make the bound real

1. **A reflected packet carries the parent's remaining depth, decremented.** Not a fresh budget — that
   is the same defect as a second counter wearing different clothes.
2. **At depth exhaustion the packet is dropped, not clamped to zero and applied.** A zero-damage packet
   still fires `OnDamageDealt` atoms and still emits events; dropping is the terminal state.
3. **A reflected packet is itself reflectable** — bounded by rule 1. Banning re-reflection would be a
   special case that hides whether the bound actually works. **Keep the general rule and let the
   counter prove it.**

### 2.2 Reflection is not recursion in the pipeline

The reflected packet **re-enters at the top of the dispatcher**, as a new signed `DamagePacket` from
the original defender to the original attacker. It does not recurse inside
`OverlayCombatCalculator`.

This matters for invariant 2 (record-then-drain): the bounce is a *later* event, not an in-frame
callback. It goes through the Funnel like everything else, and the RPG never computes damage at the
moment of a hit.

---

## 3. Where it reads from

Reflection fires on the **final damage after mitigation and amplification** —
[spec-mitigation-chain.md](spec-mitigation-chain.md)'s output — because bouncing a pre-mitigation
number would reflect damage the defender never took.

```text
finalDamage (post-mitigation, post-crit, post-amp)
  └─ rateDelta   = reflect.rate(defender) − reflect.resist.rate(attacker)
     p_reflect   = sigmoid(rateDelta / ReflectRateScale)
     roll once
        └─ dmgDelta = reflect.damage(defender) − reflect.resist.damage(attacker)
           bounced  = finalDamage × reflectShare(dmgDelta)
           → new DamagePacket(defender → attacker, depth − 1)
```

**Shields absorb the original hit before reflection reads it?** No — reflection reads `finalDamage` as
computed, *before* the shield gate. A shield protects its owner; it does not reduce what the owner
bounces back. Stated because the opposite reading is defensible and picking silently would make the
behaviour un-reviewable.

**`reflectShare` is bounded in `[0, 1]`** — you cannot bounce more than you took. **Bounded ratio,
PS-8 exempt, comment required.** `reflect.damage` itself stays an uncapped magnitude; the *share* is
what is bounded.

---

## 4. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Reflect|FullyQualifiedName~ProcDepth"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1
.\scripts\guard-stat-pairs.ps1
```

---

## 5. Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Combat/CombatDamageDispatcher.cs` | reflection stage; depth decrement on the bounced packet |
| `src/FusionRpg.Core/Combat/OverlayCombatCalculator.cs` | the two contests |
| `data/tuning/combat.v1.json` | `reflectRateScale`, `reflectShareScale` |
| `docs/architecture/combat-damage-ssot.md` §7 | the apply flow gains the bounce edge |

---

## 6. Testing strategy

### 6.1 Termination — the tests that justify the module

| Test | Asserts |
|---|---|
| **`MutualReflectorsTerminate`** | Two actors, `reflect.rate` at maximum, `reflect.damage` at maximum → resolution **halts**, total packets ≤ `ProcDepthLimit` |
| `ReflectedPacketInheritsDepth` | Depth decrements across the bounce; a fresh budget is not issued (§2.1.1) |
| `DepthExhaustionDrops` | The terminal packet is **dropped**, not applied at zero — no `OnDamageDealt` fires (§2.1.2) |
| `ReflectionInsideProcChainSharesBudget` | A reflection nested in a proc chain cannot exceed the shared limit (§2) |
| `ThreeWayReflectTerminates` | Three mutual reflectors — the case a two-actor test misses |

`MutualReflectorsTerminate` must be written **before** the feature and must be observed failing.

### 6.2 Behaviour

| Test | Asserts |
|---|---|
| `NoGoldensMoveAtZero` | All four at `0` → no reflection, nothing moves |
| `ResistAnswersRate` · `ResistAnswersDamage` | Each pair cancels at equality |
| `CannotBounceMoreThanTaken` | `reflectShare ≤ 1.0` (§3) |
| `ReflectsPreShield` | Reflection reads pre-shield-gate damage (§3) — the documented reading, made falsifiable |
| `BounceGoesThroughFunnel` | New packet, not an in-frame callback (§2.2); `guard-funnel-delta.ps1` green |
| `BounceIsDeterministic` | Same seed → same bounces, same order |

---

## 7. Boundaries

**Always** — decrement the shared depth. Drop at exhaustion. Route the bounce through the Funnel as a
new signed packet.

**Ask first** — changing `ProcDepthLimit`'s default. It is shared with every other proc chain and
raising it for reflection raises it for all of them.

**Never** — a second depth counter (§2). Reflect a pre-mitigation number (§3). Let `reflectShare`
exceed 1. Recurse inside the calculator. Ban re-reflection as a special case instead of relying on the
bound (§2.1.3).

---

## 8. Success criteria

- [ ] Four channels live, two `Contest` pairs, defender marked as the raising side.
- [ ] **Mutual and three-way reflectors provably terminate**, bounded by the shared `ProcDepthLimit`.
- [ ] Depth inherited and decremented; exhaustion **drops**.
- [ ] `reflectShare` bounded `[0,1]`, commented PS-8 exempt; `reflect.damage` uncapped.
- [ ] Bounce is a new Funnel packet; `guard-funnel-delta.ps1` green.
- [ ] `git status tests/` clean at zero.

---

## 9. Open questions

**One, and §3 states a reading rather than leaving it open** — whether shields absorb before reflection
reads the damage. **Decided as: no**, a shield protects its owner and does not reduce what the owner
bounces. The opposite is defensible; `ReflectsPreShield` makes whichever answer is chosen falsifiable
rather than incidental, and flipping it is a one-line change plus a golden re-bless in this module.
