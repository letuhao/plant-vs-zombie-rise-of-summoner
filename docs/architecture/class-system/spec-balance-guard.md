# Spec: `balance-guard` — two criteria, wired differently on purpose

**Module id:** `balance-guard` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `deterministic-core` · `aptitude-resolve` · **Blocks:** `residual-fit`

---

## 1. Objective

Make balance a **CI assertion** rather than a periodic exercise — and wire the two criteria with the
**different standing** they actually have, because getting that wrong fails in a way nothing else
catches.

**Users:** CI; anyone changing a coefficient in `data/tuning/aptitudes.v{n}.json`.

**Success is measurable:** changing one `kMilli` and re-running turns the guard red or green **in
milliseconds, with no rebuild** — the property `aptitude-tuning` §1 names as its own success test, one
layer up.

---

## 2. ⛔ The two criteria are not equals — this is the module

| | **Termination invariant** (ideal §5d) | **Dominance matrix** (ideal §8.8b) |
|---|---|---|
| Asserts | no pairing of builds that **both hold offence** has `netAttrition ≤ 0` on both sides | no row of the corner matrix beats every other, on **win rate**, with **no clock** |
| Can a later layer fix a failure? | **No.** It is an economy identity — a pool refilling faster than it drains never empties, and content added on top **inherits** the defect | **Yes.** A passive scaling damage with damage taken, a reflect build, a counter-action, an anti-turtle status |
| Standing | **HARD — fails the build** | **SOFT — reports, does not fail the build** |
| Day-one result | ✅ **green**, net attrition +3,937 to +14,107 | ⛔ **red** — `Bulwark` beats all 11 corners |

> **The failure mode to design against, because nothing else catches it:** wire both as blocking and
> the module never lands, since the soft half is red by design today. Wire both as advisory and **the
> one defect no later layer could repair becomes a warning nobody reads.**

### 2.1 The soft red is an UPPER BOUND, and the guard must say so in its own output

Two independent, measured reasons the dominance result overstates the problem
([class-system-ideal.md](../class-system-ideal.md) §8.8a):

1. **Elements were neutralised in every run** (§7c.7) — every build set to one element, so it is a 1-D
   slice of what `aspect-scope` makes a 2-D matchup space.
2. **15–47% of every aptitude is reserved** against an unbuilt mechanism (§8.1d) — the corner test sees
   roughly **two thirds** of each build, and not the same two thirds for each.

**So the guard prints coverage alongside verdict.** Which element axis was live; which channel families
were reserved. A red row must read as *"the live part of these builds is unbalanced"* and never as
*"this design is unbalanced."*

```text
DOMINANCE  [SOFT]  RED   Bulwark 11/11
  coverage: elements NEUTRALISED (1 of 7 roster slots live)
            reserved families: resource.*.stamina, skill.cooldown.*, resource.efficiency.*, move.range
            -> approx 2/3 of each build measured. This is an UPPER BOUND on severity.
TERMINATION [HARD] GREEN  min netAttrition +3,937 over 66 offence-holding pairs
```

**A guard that reports a number without its coverage invites the reader to treat a partial measurement
as a complete one** — which is the mistake §8.8a itself had to be corrected for, in its own document.

### 2.2 Why corners, and not a gradient

The marginal test (`dW/d(share)`) was the primary instrument at ±3.6%. Coefficient work compressed it
**10×**, and every aptitude is now worth within a percent of every other — **a local gradient that flat
cannot rank twelve options.**

The dominance matrix still shows a **100% spread** on the same builds, because **free build converges
to corners**, and corners are where the differences live. Spike each of the twelve to the maximum a
legal allocation permits, play every spike against every other: **144 closed-form evaluations, instant,
and it cannot miss.**

> **Best-response *chasing* is not an acceptable substitute, and this is measured.** It reported a fixed
> point at `Bulwark 55` — *"nothing beats it"* — while a direct check showed `Vigor 55` beating that
> same build **100%**. A hill-climb that misses a 100% counter is not evidence of absence. Keep the
> marginal test as a **secondary read**; never as the gate.

---

## 3. Commands

```powershell
# ⛔ CORRECTED 2026-08-27: a bare `--filter BalanceGuard` matches vstest's FullyQualifiedName-substring
# shorthand (verified empirically), and neither `TerminationGuardTests` nor `DominanceGuardTests`
# contains that literal substring -- confirmed silently matching zero tests and exiting 0. Both classes
# now carry `[Trait("Category", "BalanceGuard")]`; the property-qualified form below is the one that
# actually selects them (also verified). Left as a correction, not a silent rewrite, since a prior
# session could otherwise re-run the original line and mistake "no tests matched" for "nothing to test."
dotnet test tests\FusionRpg.Core.Tests --filter "Category=BalanceGuard"

# The same two checks from the POC, ahead of the build
cd tools\CombatSim
dotnet run --no-build -- predict --actions basic -a <builds> --theta 100    # flags NEVER ENDS
dotnet run --no-build -- trinity --actions basic -a force --theta 100       # marks the matrix
```

---

## 4. Project structure

```text
src/FusionRpg.Core/Balance/Guards/TerminationGuard.cs     the HARD half
src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs       the SOFT half + coverage report
src/FusionRpg.Core/Balance/Guards/CoverageReport.cs       what was live, what was reserved
tests/FusionRpg.Core.Tests/Balance/TerminationGuardTests.cs
tests/FusionRpg.Core.Tests/Balance/DominanceGuardTests.cs
```

**Two files, not one class with a flag.** The standing difference is the design; a `bool blocking`
parameter would make it a runtime accident.

---

## 5. Code style

```csharp
/// <summary>
/// HARD. A failure here fails the build: no later layer can repair a pool that refills faster than
/// it drains, so content added on top inherits the defect (ideal §0.2.1).
/// </summary>
public static TerminationVerdict Assert(IReadOnlyList<AptitudeAllocation> builds, long theta);

/// <summary>
/// SOFT. A failure REPORTS with its coverage and does not fail the build - the action/passive/skill
/// layer is what fills a dominant corner, and it is red by design today (ideal §8.8a, §0.2).
/// </summary>
public static DominanceReport Measure(IReadOnlyList<AptitudeAllocation> builds, long theta);
```

**The verb is the contract.** `Assert` throws; `Measure` returns. A reader cannot mistake which is
which, and neither can a caller.

**The exemption is narrow and it is real.** Two builds that bought **no offence at all** genuinely
cannot resolve, and that must stay **possible** — banning it would be a hard restriction and PS-8
refuses those. It is a degenerate pair, **outside** the invariant rather than an exception inside it,
and the code must express it that way (a filter on the input, not a special case in the verdict).

---

## 6. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Termination_fails_the_build_on_a_planted_unkillable_pair` | A synthetic build with recovery ≥ damage on both sides throws. **The guard's own guard** |
| 2 | `Termination_exempts_two_offence_less_builds` | The degenerate pair is filtered out, not special-cased — §5 |
| 3 | `Termination_is_green_on_the_shipped_config` | Day one, and it stays a regression test |
| 4 | `Dominance_reports_and_does_not_throw` | Red on the shipped config **and the test is green**. §2's failure mode, made unrepresentable |
| 5 | `Dominance_report_carries_coverage` | Element axis and reserved families present. A report without them fails |
| 6 | `Corners_beat_the_gradient_on_the_same_config` | The marginal spread is under 1% where the corner spread is 100%. §2.2, as an assertion rather than a memory |
| 7 | `Guard_runs_in_milliseconds` | Generous headroom; the property that lets it sit in CI |
| 8 | `No_clock_is_applied` | A round limit is not consulted. Ideal §0.1.2: a clock manufactures a pass by penalising long fights, which is what a survival or cc build legitimately makes them |

---

## 7. Boundaries

**Always** — print coverage with a dominance verdict; keep the two halves in separate types; state
the metric as **win rate** and nothing else.

**Ask first**

- Promoting the dominance half to blocking. That is a **product decision** — it means the
  action/passive/skill layer is declared complete enough to be held responsible.
- Adding a third criterion.

**Never**

- Apply a clock to make the matrix pass (§6 test 8).
- Measure fight length, total damage or kill time. **Win rate, and nothing else** (ideal §0.1) —
  duration metrics penalise survival and cc builds for playing correctly.
- Use best-response chasing as the gate (§2.2).
- Report a dominance verdict without its coverage (§2.1).
- Ban the offence-less pair (§5).

---

## 8. Success criteria

1. Termination blocking and green; dominance reporting and red — **both correct on day one**.
2. A planted unkillable pair fails the build.
3. Every dominance report carries coverage.
4. Milliseconds, in CI, no seed, no flake.
5. No clock, no duration metric anywhere in the module.
6. A coefficient change flips the verdict with **no rebuild**.

---

## 9. Open

**9.1 When the soft half should become hard.** Not a technical question — it is the point at which the
layers that fill a dominant corner exist and can be held responsible. [class-system-map.md](../class-system-map.md)
§5 lists them. **The guard should make the transition a one-line change** so the decision is cheap when
it arrives.

**9.2 Party and multi-actor pairings.** Both criteria are 1v1 today. `status.*.contagion` cannot even
be measured in a duel. Named as a limitation of the instrument, not of the design.

---

## 10. Design-gate checklist

```
[x] Subsystems identified: combat damage, status, shields, resources, power scale, caps.
[x] Read this session: DESIGN-GATE.md, decisions.md (Combat resolution SSOT, Caps, Power scale
    rows), ssot-power-scale.md §11 (PS-8 and its exemption classes), class-system-ideal.md
    (§0.0.3, §0.1, §0.1.2, §0.2.1, §5d, §5d.5, §8.8a, §8.8b), spec-deterministic-core.md.
[x] Every factual claim cites a document section.
[x] Verified against the MEASUREMENT record rather than restated from memory: the +3,937/+14,107
    attrition band, the 100%-vs-under-1% spread, and the Bulwark-55/Vigor-55 chasing failure are
    all from the ideal's recorded runs.
[x] Read the surrounding section of every rule quoted - §0.2.1 in full (the hard/soft argument, not
    just the table row); PS-8's exemption classes before calling the offence-less pair exempt.
[x] Constraints tested, not assumed - the two day-one verdicts are RUN results, not predictions.
    That the marginal test lost resolution was measured, not inferred.
[x] Nothing contradicts a §2 invariant. PS-8: §5 refuses to ban the offence-less pair precisely
    because banning it would be a hard restriction.
[x] Corrections propagated - §2.1's coverage-with-verdict requirement is carried in ideal §8.8b and
    the map's §4b, all three landing together.
```

---

## 11. Related

- [class-system-ideal.md](../class-system-ideal.md) §0.2.1 (why they are not equals), §5d.5 (the hard criterion), §8.8b (the soft one)
- [spec-deterministic-core.md](spec-deterministic-core.md) — what this calls
- [spec-aptitude-tuning.md](spec-aptitude-tuning.md) §2.2 — the sizing rule this guard makes executable
- [class-system-map.md](../class-system-map.md) §4b, §5 — the standing, and the layers that fill the soft half
