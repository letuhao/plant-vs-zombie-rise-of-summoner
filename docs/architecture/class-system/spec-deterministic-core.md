# Spec: `deterministic-core` — the closed form, in Core

**Module id:** `deterministic-core` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `aptitude-tuning` · **Blocks:** `balance-guard`

---

## 1. Objective

Move the closed form out of `tools/CombatSim` and into `FusionRpg.Core`, so that **predicting a
matchup costs microseconds and needs no trials** — which is what makes balance a CI assertion instead
of a periodic exercise.

```text
two allocations + Θ
   → per-swing 5-atom mixture      (miss | parry | block | clean | clean+crit)
   → renewal first passage          E[T] = h/μ,  Var[T] = h·σ²/μ³
   → normal race with correlation   P(win) = Φ(ΔT / SD)
```

**Users:** `balance-guard` (its only consumer today); `residual-fit` (the thing a measurement
disagrees with); anyone asking *"does this coefficient change break the matchup matrix?"*

**Success is measurable:** 144 corner evaluations complete in **microseconds**, and the predicted win
rate agrees with the simulator to the residuals recorded in
[class-system-ideal.md](../class-system-ideal.md) §0.0.3 — **1.8% / 2.4%** on core combat, **4.1% /
7.7%** with actions, status and regeneration all live.

---

## 2. It calls shipped functions and adds only the expectation

**This is the property that makes the module trustworthy, and it is easy to lose.** The closed form
contains **no combat math of its own**. It calls the shipped resolver's functions and computes the
*expected value* of what they do — so a change to `OverlayCombatCalculator`, `ShieldMath` or
`ResistanceEvaluator` moves the prediction automatically.

> **A closed form that re-implemented the formulas would be a second combat SSOT**, and
> [decisions.md](../decisions.md)'s *Combat resolution SSOT* row exists to stop exactly that: *"One
> combat formula set + one apply path, everywhere."* A second one that only *predicts* is still a
> second one, and it would drift silently — the residual would read as model error and be fitted away.

**The one thing it legitimately adds** is the probability arithmetic: mixing the five outcome atoms,
turning per-swing damage into a first-passage time, and racing two of them.

> **It does not implement the aptitude read functions either.** `k · share^γ · P(Θ)` belongs to
> [aptitude-tuning](spec-aptitude-tuning.md) §2.1 (decided 2026-08-26), and this module calls it. An
> earlier version of the map had this module and `aptitude-resolve` parallel *"sharing only the
> config"* — they would have shared the arithmetic and each written it once. **A divergence there
> would read as model error and be fitted away by `residual-fit`**, which is the one failure this whole
> program is built to prevent.

### 2.1 The four corrections the POC paid for — port them, do not re-derive them

Each of these was a wrong model that measured plausibly until it was checked against the code. They are
listed so the port carries them rather than rediscovering them
([class-system-ideal.md](../class-system-ideal.md) §8.8c and the analytic record):

| Correction | The defect | Residual |
|---|---|---|
| **Reflect is unmitigated** | `TryReflect` builds the bounce with **no `ElementPayload`**, so `OverlayCombatMath.Finalize` early-returns — reflected damage is unmitigated, unavoidable and uncritable. The model treated it as a full second attack | 25.3% → — |
| **Kill-time correlation** | The two first-passage times are not independent; the race needs a `ρ` term | 5.1% → — |
| **Shield double-count** | `ShieldRuntime` computes `maxHp = grant.BaseHp + capacity` and reads the channel itself; seeding the pool with capacity too doubled every shield. **Corollary: a shield needs a grant to exist** — capacity only adds to one | **30.8% → 3.5%** |
| **Status rides the multiplied hit** | A `skill-strike` at ×1.8 applies a ×1.8 status, because the status scales off the packet it rode in on. The form used the authored base | **15.4% → 4.1%** |

> **The fourth one is the methodological lesson, not just a number.** Each layer alone measured under
> 4%; the error was **invisible until both actions and status were on**. Measure combinations, never
> features one at a time.

### 2.2 One rule the POC added after breaking it

> **A model may not be optimised against while a known error in it is unfixed.**

Recorded because it was earned: a DoT over-count (`p × mag × dur` per swing, where the shipped
semantics are *refresh*, not stack) sat in a doc comment while a coefficient search was run against it.
The search converged beautifully on the wrong thing. The fix is the uptime form
`1 − (1 − p)^duration`, and the rule is worth more than the fix.

---

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter Deterministic

# The POC, which this module must reproduce
cd tools\CombatSim
dotnet run --no-build -- predict -a force-ns,finesse-ns,bastion-ns --theta 100 -n 4000
dotnet run --no-build -- trinity --actions basic --status -a force --theta 100
```

---

## 4. Project structure

```text
src/FusionRpg.Core/Balance/Analytic/StrikeMixture.cs      the 5 atoms, from the shipped resolver
src/FusionRpg.Core/Balance/Analytic/FirstPassage.cs       renewal mean/variance
src/FusionRpg.Core/Balance/Analytic/Race.cs               the normal race + correlation, Phi
src/FusionRpg.Core/Balance/Analytic/PhaseModel.cs         shield effective-HP and the reflection gate
src/FusionRpg.Core/Balance/Analytic/Predictor.cs          the entry point
tests/FusionRpg.Core.Tests/Balance/PredictorTests.cs
tests/FusionRpg.Core.Tests/Balance/PredictorMatchesSimulatorTests.cs
```

**`Balance/`, not `Combat/`.** Nothing in the hot path may call this. It is an analysis surface that
happens to live in Core so a test can reach it without a tools reference.

---

## 5. Code style

**`double` throughout, and this is the one place that is correct.** Probabilities, variances and
`Φ` are not magnitudes — they are bounded ratios and statistics over them. The overflow standard's
`long` rule applies to a *magnitude*, and a win probability is not one.

**But every magnitude it reads in must arrive as `long`** and be widened before any multiply. The
seam where `long` becomes `double` is one function, named, and commented with why.

**No RNG. No trials. No time.** A method here is a pure function of its arguments or it does not
belong. This is what lets `balance-guard` run in CI without a seed, a budget or a flake.

```csharp
/// <summary>
/// NOT a balance metric. A bound on the numeric integration only - a fight that has not resolved
/// within it contributes to neither side's win probability. Raising it must never change a verdict;
/// PredictorTests asserts that it does not.
/// </summary>
const int RoundLimit = ...;
```

That comment is load-bearing. `RoundLimit` looks exactly like a cap on fight length, and
[class-system-ideal.md](../class-system-ideal.md) §0.1.2 records the session that mistook one for a
balance instrument and had to retract the conclusion. It is a **structural limit**, PS-8 exempt, and it
says so.

---

## 6. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Predictor_matches_the_simulator_within_the_recorded_residual` | Core combat ≤ 2.4% max; all four axes live ≤ 7.7% max. **The residuals are the spec** |
| 2 | `Win_rate_is_exactly_theta_invariant` | Identical from `Θ`=10 to `Θ`=5,000. Not approximately — **exactly**, by homogeneity |
| 3 | `Reflected_damage_is_unmitigated` | §2.1 correction 1, asserted against `OverlayCombatMath` rather than assumed |
| 4 | `A_shield_needs_a_grant_to_exist` | Capacity with a zero baseline does nothing. §2.1 correction 3's corollary |
| 5 | `Status_rides_the_action_multiplied_hit` | §2.1 correction 4 |
| 6 | `Dot_uptime_is_refresh_not_stack` | §2.2's fix, as an assertion |
| 7 | `RoundLimit_does_not_change_a_verdict` | Double it; every matchup verdict identical. §5 |
| 8 | `Predictor_is_pure` | Same inputs, same output, no statics, no clock |
| 9 | `144_corners_complete_in_microseconds` | The performance property `balance-guard` depends on. A wall-clock assertion with generous headroom, not a benchmark |

**Test 1 is the module.** Everything else protects a specific way it has already been wrong.

---

## 7. Boundaries

**Always** — call the shipped combat functions; keep every method pure; state a residual with its
measurement conditions.

**Ask first**

- Adding a term the simulator cannot check. An unfalsifiable improvement to a model is not an
  improvement.
- Any use of this module on a hot path.

**Never**

- Re-implement a combat formula (§2).
- Optimise a coefficient against the model while a known error in the model is unfixed (§2.2).
- Treat `RoundLimit` as a balance parameter (§5).
- Report a residual measured on one layer as though it covered several (§2.1's fourth row).

---

## 8. Success criteria

1. Residuals within §1's recorded bands, asserted.
2. `Θ`-invariance exact.
3. All four §2.1 corrections carried, each with a test.
4. Pure, no RNG, no clock.
5. 144 corners in microseconds.
6. **No combat formula is implemented here** — a reviewer can trace every damage number to a shipped
   function.

---

## 9. Open

**9.1 `poise` needs the phase treatment shields got.** Ideal §7 records that shields were closed by
phase decomposition — effective HP plus a gate on reflection — and that *"`poise` will need the same
treatment when it is registered."* That is `guard-economy`'s trigger, not this module's, but the seam
belongs here and the module should be shaped so adding a second absorbing phase is not a rewrite.

**9.2 The residuals are 1v1 only.** Nothing here has met a party, and `status.*.contagion` is
unmeasurable in a duel because there is no second host. Named, not hidden.

---

## 10. Design-gate checklist

```
[x] Subsystems identified: combat damage, shields, status, elements, power scale, caps.
[x] Read this session: DESIGN-GATE.md, decisions.md (Combat resolution SSOT, Combat mitigation
    shapes, Shield layer, Status SSOT, Power scale, Caps rows), ssot-power-scale.md §4.6/§4.7/§11,
    class-system-ideal.md (§0.0.3, §0.1.2, §5d, §8.8a-c), spec-aptitude-tuning.md.
[x] Every factual claim cites a document section or a shipped type.
[x] Verified against CODE - CLOSED 2026-08-26, both files re-opened this session and both hold:
    CombatDamageDispatcher.cs:81-82 states it in the file - "No ElementPayload on the bounce:
    OverlayCombatMath.Finalize passes an ElementPayload-less packet through unchanged" - and
    ShieldRuntime.cs:123 is literally `var maxHp = grant.BaseHp + capacity;`. The port still carries
    a test for each, because a re-read proves today's code, not tomorrow's.
[x] Read the surrounding section of every rule quoted - the Combat resolution SSOT row in full;
    PS-8's exemption classes before calling RoundLimit structural.
[x] Constraints tested, not assumed - the residuals in §1 are MEASURED numbers from the POC, run,
    not estimated. §2.2 is a rule that exists because the opposite was done once and cost a search.
[x] Nothing contradicts a §2 invariant. PS-8: RoundLimit is a structural limit and §5 requires the
    comment saying so, plus test 7 proving it changes no verdict - stronger than a comment.
[x] Corrections propagated - §2.1 is the propagation; the map's §4 carries the same residuals.
```

---

## 11. Related

- [../research/class-analytic-balance-2026-08-25.md](../../research/class-analytic-balance-2026-08-25.md) — the derivation and its validation
- [class-system-ideal.md](../class-system-ideal.md) §0.0.3 (the residuals), §8.8c (the corrections)
- [decisions.md](../decisions.md) — *Combat resolution SSOT*, *Combat mitigation shapes*
- [tools/CombatSim/README.md](../../../tools/CombatSim/README.md) — the POC this ports
