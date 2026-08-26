# The deterministic core — balance as a theorem instead of a measurement

**Date:** 2026-08-25 · **Tool:** [`tools/CombatSim`](../../tools/CombatSim/README.md) (`predict`, `search --analytic`) ·
**Subject:** [class-system-map.md](../architecture/class-system-map.md)

**Status: proof record.** The closed form and its validation are real and reproducible. Every
coefficient it evaluates is still a **model** coefficient — see §7.

---

## 1. The question

> If one actor puts 1,000 points into Might and another puts 1,000 into Fortitude, can we state the
> win rate **by mathematics** rather than by simulating it?

Yes, and the surprising part is how little had to change: the shipped combat formulas were already
almost entirely closed-form. What was missing was not a different game — it was the **expectation**.

---

## 2. Why a closed form exists at all

**One swing has a finite outcome space.** Miss, parried, blocked, clean, clean-and-crit — five atoms,
each with a probability and a damage the shipped formulas already compute exactly:

```text
p(hit)   = sigmoid(accuracy − dodge, 100)          CombatProbability.Sigmoid
p(parry) = max(0, parry.rate − parry.break)/1000   linear per-mille contest
D(clean) = DivisiveMitigation(base + power, defense·pierceFactor, k, base + power) × ampFactor
D(parry) = base − ClampedContest.Apply(...)        integer per-mille, clamped [0, 950‰]
```

So the mean and variance of a swing are **exact finite sums**, and so is any nonlinear function
downstream of them. Reflection — a share of what landed, bounced back — is enumerated atom by atom
rather than evaluated at the mean, because `E[f(D)] ≠ f(E[D])` and the guard branches make the map
piecewise.

**From a per-round distribution to a win rate** is the one place an approximation enters. Depleting
`h` HP at mean `μ` per round with variance `σ²` is a renewal first passage:

```text
E[T] = h/μ                Var[T] = h·σ²/μ³
```

Both times race; both are sums over many rounds, so both are approximately normal, and

```text
P(A wins) = Φ( (E[T_A] − E[T_B]) / sqrt( Var[T_A] + Var[T_B] − 2ρ·SD_A·SD_B ) )
```

**That is the win rate.** The `ρ` term is not optional decoration: when reflection is live, one swing
damages *both* actors, so the two kill-times move together and the variance of their difference
shrinks. Dropping it costs 5 points of win rate on a reflect matchup and nothing anywhere else
(measured — §4).

---

## 3. The invariance theorem — the property that makes any of this worth having

A balance statement is only worth proving if it stays true. It does, and **by construction, not by
luck**:

| Quantity | Scales as |
|---|---|
| a contest channel (`k · share · spanPoints`) | **`Θ`-free** — `share ∈ [0,1]` |
| a magnitude channel (`k · share · P(Θ)`) | `P(Θ)` |
| authored base damage, hp | `P(Θ)` |

Every probability in the model is a function of contest channels only, so **every probability is
`Θ`-free**. Every damage term is built by functions that are **homogeneous of degree 1** in the
magnitudes — `DivisiveMitigation` is `offense × kL/(kL + defense)` with `L` itself a ladder quantity,
and `ClampedContest` scales its base, its delta and its bounds together. Therefore:

```text
μ ∝ P(Θ)        σ ∝ P(Θ)        h ∝ P(Θ)
E[T] = h/μ            ∝ P/P     = Θ-free
Var[T] = h·σ²/μ³      ∝ P·P²/P³ = Θ-free
W = Φ(ΔT/SD)                    = Θ-free      ∎
```

**Measured against the claim:** the closed form returns *identical* win rates at `Θ` = 10, 20, 50,
100, 300, 1,000 and 5,000 — a 14,000× change in `P(Θ)`, zero drift to displayed precision. The
simulated version of this test measured 0.9% drift; that 0.9% was sampling noise and integer rounding,
not a property of the design.

> **The design rule this yields, and it is the one most easily broken:**
> **every function that combines magnitudes must be homogeneous of degree 1.** A constant in a
> denominator (`defense/(defense + 100)`) breaks it silently — the mitigated fraction then drifts with
> the ladder and the balance you proved at `Θ`=20 is gone by `Θ`=200. `DivisiveMitigation` is safe only
> *because* its `K` reads ladder quantities (combat-damage-ssot.md §6.3a).

---

## 4. Validation — and the defect the cross-check found on its first run

`predict --verify` computes every arrow in closed form, then simulates the same builds and prints the
gap.

| Pass | Mean residual | Max | What changed |
|---|---|---|---|
| first run | 8.7% | **25.3%** | — |
| after reading `TryReflect` | 1.9% | 5.1% | reflection modelled correctly |
| after adding the `ρ` term | **0.4%** | **0.7%** | kill-time correlation |

**The 25.3% was one matchup, not six** — which is the whole value of a closed form as a diagnostic. It
isolated the only pair where reflection is live, and reading the code settled it in one pass:

> `CombatDamageDispatcher.TryReflect` builds the bounce packet **with no `ElementPayload`**, and
> `OverlayCombatMath.Finalize` early-returns on a payload-less packet
> ([OverlayCombatMath.cs:42-43](../../src/FusionRpg.Core/Combat/OverlayCombatMath.cs)). So reflected
> damage is **unmitigated, unavoidable and uncritable** — a flat share of what landed, which no
> defense reduces, no dodge or guard stops, and no crit multiplies.

This is deliberate and documented (`CombatDamageDispatcher.cs:81-83`: *"`bounced` (already final) is
not re-mitigated"*). **The model was wrong, not the code** — and the model was wrong in a way that
predicted 1,698 self-damage against the shipped 3,886. Nothing else in this session would have caught
it; the simulated search ran for eighteen rounds on top of that mechanic without noticing.

Reproduce:

```powershell
cd tools\CombatSim
dotnet run --no-build -- predict -a force-ns,finesse-ns,bastion-ns --theta 100 -n 4000
dotnet run --no-build -- predict -a force-ns,finesse-ns,bastion-ns --theta 10,20,50,100,300,1000,5000 --no-verify
```

---

## 5. Solving instead of searching

The win rate is the least interesting output. The closed form is deterministic and costs microseconds,
so it can be **optimised over** rather than sampled:

```powershell
dotnet run --no-build -- search --analytic -m aptitudes.v1 -a force-ns,finesse-ns,bastion-ns `
    --theta 100 --restarts 24 --steps 220 --seed 307
# its result is kept as builds/{force,finesse,bastion}-solved.json:
dotnet run --no-build -- predict -a force-solved,finesse-solved,bastion-solved --theta 100 -n 6000 --seed 99991
```

| | arrows | spread | cost |
|---|---|---|---|
| simulated search (2026-08-25, §3.2 of the measurement record) | 65.8 / 64.4 / 66.6 | 2.1% | ~18 rounds of tuning |
| **closed-form solve** | **64.8 / 64.7 / 65.1** | **0.4%** | **2.3 s**, 5,280 matrix evaluations |
| simulator, checking what the math designed | 64.9 / 64.9 / 67.7 | — | 6,000 duels/arrow |

Max residual on the falsification pass: **2.6%**, and it lands on `FORCE v FINESSE` — the **shortest**
fight of the three (≈19 rounds against 24 and 55). That is where the CLT step is weakest, and it is
where the model itself predicted it would be weakest (§7). A residual that appears where the theory
says it should is a much better sign than a uniformly small one.

**Three build sets, kept apart on purpose:** `force/finesse/bastion` carry the simulated-search
allocation with shields; `*-ns` are their shield-free twins (the residual table in §4 and §6);
`*-solved` hold what the closed form designed. Mixing them is how a reproduction stops reproducing.

**The simulator's role inverts.** It stops being the source of the number and becomes the **falsifier**
of a number the math produced. That is a far stronger test, and it is the one that found §4's defect.

---

## 6. What breaks the closed form — named, not hand-waved

Five design rules keep a mechanic inside the deterministic core. Four of them the shipped combat
system already satisfies:

| # | Rule | Shipped status |
|---|---|---|
| R1 | Every mechanic resolves as an independent draw with known `p` and known payoff | ✅ all of them |
| R2 | Rounds are i.i.d. — no state carried between them | ⚠️ **shields violate it — now SOLVED by phase decomposition, §6.1. `poise` will too** |
| R3 | Feedback is linear and bounded | ✅ reflection, via `ProcDepthLimit` |
| R4 | No HP-conditional behaviour — no execute, no enrage | ✅ none exists. **Guard this one** |
| R5 | Clamps are piecewise closed-form | ✅ `min`/`max`/`Clamp` are fine |

**R2 is the only live violation, and it is a phase boundary rather than chaos.** A pool that absorbs
at full rate and then breaks makes rounds non-identical; the fix is to solve each phase and add the
times, not to abandon the model. Measured cost of ignoring it:

| Builds | Mean residual | Max | Effect |
|---|---|---|---|
| shield-free | 0.4% | 0.7% | — |
| shields on, **before** the phase model | 19.6% | 32.0% | two of three arrows reverse |
| shields on, **after** (§6.1) | **0.7%** | **1.4%** | closed |

**R4 is the rule to guard hardest**, because nothing violates it yet and an execute threshold or a
rage-below-30% looks harmless when it is proposed. It is the cheapest possible thing to say no to
today and the most expensive to remove later.

### 6.1 Shields — the phase boundary, solved (2026-08-25)

R2 was the one live violation, and it is now closed. **A depleting pool does not need two solved
phases when both phases face the identical incoming distribution** — and here they do, because
mitigation runs *before* the shield gate. Only the target changes.

While the shield stands, `ShieldMath.AbsorbLayer` takes the whole hit (`remainder` 0) and spends
`damageToShield` from the pool. So:

```text
effective HP from a shield  =  S × input / damageToShield
damageToShield              =  ClampedContest(input, pen − toughness, 1, input, ChipFloor, PenCap)
                               bounded to [100‰, 3000‰] of the hit
```

Two phases at the same rate sum to `(S_eff + hp)/mu`, which is what one pool of that size already
gives. **One extra term, no second solve.**

**The ratio is the shield-tank mechanic, not a detail.** At `pen = toughness` a shield point equals an
HP point; out-toughness the attacker and it is worth up to **10×**; get out-penetrated and it is worth
**1/3**. A 30× spread decided entirely by the opposing build — which is precisely why a shield tank is
a distinct build with a named counter rather than an HP tank with extra steps.

**And a second, non-obvious consequence that the residual found before anyone reasoned it out:**

> **A shield suppresses its owner's own reflection.** `reflectReadsPostShield: true` fires the bounce
> only on damage that reached HP, and a fully-absorbed hit reaches none. Modelling reflection without
> this gate left the closed form **31 points** off — on exactly the one matchup where a shield and a
> reflector meet. Shield and thorns are anti-synergistic.

First-order treatment: reflection operates only during the HP phase, so its mean, variance and
covariance all scale by `hp / (hp + S_eff)`.

**Measured after both corrections**, with shields live and bought from the aptitude distribution:

| | mean residual | max |
|---|---|---|
| shields ignored (the old single-phase core) | 19.6% | 32.0% |
| **shields as a phase + the reflection gate** | **0.7%** | **1.4%** |

Still not modelled: **shield regen and resource regen.** The duel runner does not tick either, so the
two agree — but both understate a regenerating pool, and neither can be trusted the day the action
layer starts spending `stamina` and `qi`.

---

## 7. Limitations

- **The coefficients are still model coefficients.** `unitClass` is `null` on 29 catalog families;
  §3's invariance is a property of the *structure*, and it holds for whatever coefficients are
  eventually chosen, but the specific numbers here are not shipped ones.
- **Duel only.** No party, no action layer, no items, no elements (deliberately neutralised), no
  status. Focus still scores zero and that is still correct.
- **The CLT step degrades on short fights.** Below roughly ten rounds the normal race is the wrong
  model; an exact discrete convolution is the fix, and it is not built.
- **Stat *ranges* are read at their midpoint.** Every build here is `Fixed`, so nothing is lost today
  — but a rolled range would need its own expectation, since the damage function is not linear in the
  channels.
- **`ρ` is carried from the per-round increments to the kill times unchanged.** That is the delta
  method, correct to first order; the 0.7% residual is the size of everything it omits.

---

## 8. Related

- [class-system-map.md](../architecture/class-system-map.md) — the program this proves out
- [class-rps-balance-2026-08-25.md](class-rps-balance-2026-08-25.md) — the simulated measurement that
  came first, and the nine structural rules it found
- [power/ssot-power-scale.md](../architecture/power/ssot-power-scale.md) §2, §4.6 — the theorem §3
  reproduces analytically
- [combat-damage-ssot.md](../architecture/combat-damage-ssot.md) §6 — every shape the core evaluates
