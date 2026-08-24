# Spec: status-contest

Module **`status-contest`**, wave 3 in the [power map](../power-map.md). Depends on **`power-index`**.

> **Reads [ssot-power-scale.md](ssot-power-scale.md)** — the parent SSOT. Where this spec and the
> SSOT disagree, **the SSOT wins**.

**Status:** Owner approved 2026-08-24 — build authorized. **Built the same day** (power-todo.md
T3.1/T3.2/T3.3, all three done) — full test suite green throughout, no golden re-blessed.

---

## 1. Objective

Fix the two shipped defects in SSOT §6 and retire the last private curve.

This is the only module in the program that **changes live behaviour by design**, and the only one
that amends a locked ADR. Everything else is a refactor.

## 2. Design

### 2.1 The three changes

| # | From | To | Why |
|---|---|---|---|
| 1 | `ProgressionPowerCurve.PowerFromLevel = 2^min(L,12)` | `progression.power = Θ` | Geometric on a difference-based contest (SSOT §2); capped, so progression stops at 12 |
| 2 | `StatusPolicy.ResistFromPowerRatio = 0` | `1.0` | Attacker's level counts, defender's does not — two identical actors contest at `delta = tierPower`, not 0 |
| 3 | `RpgXpPowerScale.ForKill` (stub 1.0) | deleted | Its documented future job — *"scale kill XP by zombie power"* — is `Θ_content`. A stub whose replacement now exists is dead code |
| 4 | `effectiveApplyScale = ApplyScaleK × matchPower` | `= ApplyScaleK` | **Added by audit F3.** The power-scaled divisor makes the apply roll a *ratio* contest, and under a linear `Θ` a fixed gap then **loses value** as Θ grows (0.5010 → 0.5000 at gap 5). One regime everywhere: constant divisor |
| 5 | `netFactor = clamp(delta, 0, Max)` | `= 1 + delta / NetFactorScale` | **Added by audit F4.** A raw difference used as a multiplier: parity and +1 both give 1.0×, **+2 gives 2.0×** — a cliff — and one retired world (`Wa=25`) gives 25×. Normalizing removes the cliff and retires the `delta == 0` special case |

### 2.2 Fix order is not cosmetic

**Land #2 first.** `ResistFromPowerRatio = 1.0` makes a matched pair contest at `delta = 0`
**regardless of curve shape** — including under the shipped exponential. That single constant makes
the system safe to look at while #1 is in review.

#1 alone would not: at `Θ=12` vs `Θ=11` under `2^L` the gap is `4096 − 2048 = 2048`, still a 2048x
potency multiplier. Both are needed; the order decides how dangerous the intermediate state is.

### 2.3 The measured before/after

From the SSOT §6.0 probe (matched pair, base magnitude 20, shipped evaluator):

| Θ | today: netFactor | today: magnitude | after #1+#2 |
|---|---|---|---|
| 0 | 1 | 20 | 20 |
| 6 | 64 | 1,280 | 20 |
| 12 | 4,096 | 81,920 | 20 |
| 50 | 4,096 (capped) | 81,920 | 20 |

A matched pair yields base magnitude at **every** Θ, via the shipped even-match case
(`ResistanceEvaluator.cs:214`, `delta == 0 -> netFactor = 1.0`). Mismatched pairs then scale with the
*gap*, which is what a contest should do.

### 2.4 The divisor fix belongs here — an earlier draft of this spec said otherwise

This section previously argued the scaled divisor *"stops being harmful under a linear `Θ`"* and
deferred it. **Audit F3 measured the opposite.** Under a linear `Θ` with `s = K × matchPower`, a
fixed gap **decays**: gap 5 gives `p_apply` 0.5010 at Θ=10 and 0.5000 at Θ=10,000. Every status apply
converges to a coin flip no matter how far ahead the attacker is.

So the divisor is not a pre-existing wart the linear curve tolerates — it is a defect the linear
curve *activates*, in the opposite direction from the one it fixes. It is change #4, here.

**Both halves of the evaluator then read power the same way**, which is the property the SSOT's
theorem needs and which no amount of curve-choosing can supply on its own.

### 2.5 ADR P1 amendment — landed in `decisions.md` 2026-08-24 (T3.1/T3.2)

> **P1 UpdatePower — amended 2026-08-24.** The POC curve `ProgressionPowerCurve (2^min(level,12))` is
> **retired**. `progression.power` reads `Θ` from `IPowerIndexProvider`
> ([ssot-power-scale.md](ssot-power-scale.md) §4), linear per the difference-contest
> theorem (§2). `StatusPolicy.ResistFromPowerRatio` moves `0 -> 1.0` so a matched pair contests at
> zero. `IncludeTierPowerInDelta` stays `true`. Reason: the POC curve produced `netFactor = 4096` for
> two identical level-12 actors — measured, SSOT §6.0. Latent only because
> `InjectorProgressionPowerProvider.SetLevel` had no caller.

## 3. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Resistance|FullyQualifiedName~Status"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Server.Tests
.\scripts\prove-status-full.ps1
```

## 4. Structure

```
src/FusionRpg.Core/Stats/Derived/IProgressionPowerProvider.cs  (delete — superseded by power-index)
src/FusionRpg.Core/Status/StatusPolicy.cs                      (edit — ResistFromPowerRatio 0 -> 1.0)
src/FusionRpg.Core/Stats/Derived/Subsystems/RpgProgressionSubsystem.cs (edit — reads Θ)
src/FusionRpg.Core/Progression/RpgXpPowerScale.cs              (delete)
src/FusionRpg.Core/Progression/RpgXpAwardMap.cs                (edit — drop the stub multiply)
docs/architecture/decisions.md                                 (edit — P1 amendment, §2.5)
docs/architecture/rpg-progression.md                           (edit — §6 propagation, see §8)
tests/FusionRpg.Core.Tests/Status/ResistanceEvaluatorTests.cs  (edit — the red test)
```

## 5. Testing strategy

| Case | Expect |
|---|---|
| **The red test** | matched pair at `Θ=12`: `netFactor` goes `4096 -> 1.0`, magnitude `81,920 -> 20`. Written **before** the fix, red, then green |
| Matched at every Θ | `delta == 0` and `netFactor == 1.0` for `Θ in {0,1,6,12,50,1000}` |
| Gap drives potency | `Θ=12` vs `Θ=10` gives `delta == 2`; `Θ=10` vs `Θ=12` gives `delta == -2` -> potency_floor |
| Symmetry | `delta(a,b) == -delta(b,a)` for all pairs — the property `ResistFromPowerRatio = 0` broke |
| Existing stub test updated | `Neutral_stub_tier_power_contributes_to_delta` asserted `delta == 1.0` for two identical actors. It must now assert **0.0** — it encoded the bug |
| `RpgXpPowerScale` gone | no reference in `src/`; kill XP unchanged (the stub returned 1.0, so removing the multiply is arithmetically inert) |
| Attacker-less path | **Genuinely unchanged (delta stays 0) — but not automatically, and this row's silence on *why* hid a real defect. Found building T3.1 (2026-08-24):** naively, `AttackerLess()` zeroing only the *attacker's* channels means a normal defender's own tier power still counts as resist, giving `delta = -1`, not 0. Run through the full battle suite, that `-1` sends every scripted DoT/CC/rider status (`BattleEngine.cs`'s "land attacker-less at t0") to `netFactor`'s `MinNetFactor` floor — **completely inert** (`BattleStatusTests.Dot_kills_through_rounds` went `Victory → Stalemate`). Fixed properly, not patched around: `ComputeDelta` gained an `attackerLess` parameter that excludes `defender.TierPower × ResistFromPowerRatio` from the contest when there is no real attacker side to contest it with (immunity/category/omni resist still apply). Net effect matches this row's original claim — delta is 0 — but by an explicit, intentional exclusion rather than an accident of the bug being fixed |
| **Golden movement is expected here** | status goldens move. Each moved hash is attributable to #1 or #2 and re-blessed knowingly, one commit, with the before/after in the message |

## 6. Boundaries

**Always** — write the red test first · land #2 before #1 · attribute every moved golden.

**Ask first** — anything touching `effectiveApplyScale` (§2.4) · changing `IncludeTierPowerInDelta`
or `MaxNetFactor`.

**Never** — land this before the ADR amendment · re-bless a golden without naming which change moved
it · leave `IProgressionPowerProvider` alive beside `IPowerIndexProvider`.

## 7. Success criteria

1. Red test flips `4096 -> 1.0`.
2. `delta` antisymmetric across all pairs.
3. `RpgXpPowerScale` and `IProgressionPowerProvider` deleted, suites green.
4. `decisions.md` P1 amended; `rpg-progression.md` propagated.
5. Every moved golden attributed in the commit message.

## 8. Open

**None.** The propagation debt below is scoped work — evidence rule 6 makes it obligatory, not a
decision.

**Resolved during T3.1's build (2026-08-24):** §5's "Attacker-less path" row said "unchanged" without
explaining why that's true — and naively it *isn't*: excluding the tier-power-resist term for
attacker-less requests needed an explicit code change (`ComputeDelta`'s new `attackerLess`
parameter), not just "the formula happens to still give 0". Without it, every scripted DoT/CC in
battle went completely inert (`BattleStatusTests` — a real, caught-by-the-full-suite defect, not
golden drift). See the note in place in §5 for the full trace.

### The debt

`rpg-progression.md` currently says `IProgressionPowerProvider.UpdatePower` is *"Not in code"*. It is
— `IProgressionPowerProvider.cs:15`. That doc also describes `progression.power` as a hardcoded 1.0
stub, and `actor-hub-ssot.md` §3.B says the same. Both need a pass in this module; evidence rule 6
(*"when you correct something, propagate it"*) makes that in scope, not optional.
