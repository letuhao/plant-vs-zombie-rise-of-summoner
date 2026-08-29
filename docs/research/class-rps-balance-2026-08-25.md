# Measurement — posture RPS balance and the `unitClass` scale question

**Date:** 2026-08-25 · **Tool:** [`tools/CombatSim`](../../tools/CombatSim/README.md) ·
**Subject:** [class-system-ideal.md](../architecture/class-system-ideal.md)

**Status: measurement record. Nothing here is specced, shipped, or signed off.** Every coefficient is
a *model* value used to test structure; the real `unitClass` and coefficient decisions still have to be
made against `src/`.

---

## 1. What was asked

Two questions, which turned out to be one:

1. **Is there an aptitude-point distribution across three postures that produces a healthy
   rock-paper-scissors cycle** — each posture beating one and losing to one, at rates that are decisive
   but not certain?
2. **Are the 16 combat families with `unitClass: null` `Θ`-scale or `P(Θ)`-scale?** — the question that
   had blocked three separate pieces of work.

They are one question because a distribution that balances at one `Θ` and collapses at another is not
an answer for a game with an endless ladder.

---

## 2. Method

Every number comes from the **real combat pipeline** — `CombatDamageDispatcher.DispatchInstant` driven
through `FoundationHarness`. No combat math is reimplemented in the tool.

| | |
|---|---|
| **Fitness** | three arrows measured as mutual duels to the death; score penalises distance from a 65% target, disqualifies a reversed arrow, and penalises any arrow above 90% |
| **Search** | simulated annealing, multi-restart, each restart from a fresh random allocation |
| **Ladder** | `P(Θ)` from the shipped `PowerLadder.Value(Θ)` and `power-scale.v2.json` — not a reimplemented curve |
| **Invariance test** | same point *shares* replayed at Θ = 10, 20, 50, 100, 300, 1000 |

### 2.1 Controls — two of which were established late, and that matters

| Control | Why |
|---|---|
| Identical pools (hp, damage, shield) for all builds | otherwise a build wins by being better-statted, not better-allocated |
| Identical point budget (100, renormalised every perturbation) | the search compares *allocations*, and can never win by spending more |
| Initiative alternates each duel | otherwise whichever build is named first wins the close matchups and the matrix reads as a cycle when it is really turn order |
| **Same element for all builds** | **added late — see §5.1** |

### 2.2 Reproduction

```powershell
cd tools\CombatSim
dotnet run --no-build -- ladder  -m h-contest,h-magnitude,h-split   # the three hypotheses
dotnet run --no-build -- search  -m h-split -n 900 --restarts 20 --steps 65 --theta 100
dotnet run --no-build -- explain -a finesse,bastion --theta 100 -n 20000
```

---

## 3. Result

### 3.1 The `unitClass` question

| Model | Max drift, Θ 10 → 1000 | Behaviour |
|---|---|---|
| all 16 as **contest** (`Θ`-scale) | 27.8% | settles, then frozen at 0/100 |
| all 16 as **magnitude** (`P(Θ)`-scale) | **100.0%** | matchups fully **invert** — `FINESSE v BASTION` runs 0% → 100% |
| **split + share-read contests** | **0.9%** | **invariant** |

**Both framings of the original question are wrong.** The answer has two parts:

1. **Split by what the formula compares each family against.** `parry/block.strength` and `.shred` are
   measured against `baseLong` — *the hit itself* — so they are **magnitudes**. The rest feed a bounded
   ratio through a small scale (`pierceFactor`, `ampFactor`, permille rates), so they are **contests**.
2. **A contest reads allocation *share*, not an absolute point count.** Points accrue ∝ `Θ`, so an
   absolute read makes the *difference* between two builds grow ∝ `Θ` and `sigmoid(delta/scale)`
   saturates — every contest becomes deterministic at depth. Measured: the cycle held only in a narrow
   band around Θ=100 and collapsed to 0/100 by Θ=300.

Part 2 reproduces the property [`ssot-power-scale.md` §2](../architecture/power/ssot-power-scale.md)
already locks with its own baselines — `BaseAccuracy = 220 + 26L` against `BaseDodge = 26L`, built so
**level cancels at parity**. The `Θ` term is a shared baseline that cancels between two actors at the
same depth; only allocation differentiates them, and that gap must stay bounded.

### 3.2 The cycle

| | FORCE > BASTION | BASTION > FINESSE | FINESSE > FORCE | spread |
|---|---|---|---|---|
| search, Θ=100, seed 307 | 65.8% | 64.4% | 66.6% | **2.1%** |
| independent verify, seed 8888, 3,000 duels/arrow | 64.3% | 64.0% | 67.4% | **3.4%** |
| across Θ 10 → 1000 | ~64% | ~63.5% | ~63.3% | **drift 0.9%** |

Closing cycle, every arrow near target, **invariant across a 628× change in `P(Θ)`**.

### 3.3 Allocation

Points per build, normalised to 100.

| FORCE | | FINESSE | | BASTION | |
|---|---|---|---|---|---|
| Might | 40.3 | Pierce | 59.2 | Fortitude | 36.8 |
| Retribution | 29.7 | Agility | 22.4 | Ferocity | 26.6 |
| Vigor | **15.0** | Composure | 15.4 | Precision | 21.6 |
| Onslaught | **15.0** | Focus | 3.0 | Bulwark | **15.0** |

**Bold values sit exactly on the 15-point floor** — the search wanted them lower. Those aptitudes are
still underpowered relative to their peers. Focus at 3.0 is expected and correct (§4.3).

---

## 4. The rules the search found

Nine failures, nine rules. Each was **diagnosed from measurement, not argued** — and none was known in
advance.

| # | Rule | Evidence |
|---|---|---|
| 1 | **Fight length decides whether RPS is probabilistic or deterministic** | at ~70 rounds a **4%-per-round** edge produced a **100%** win rate — variance averages away. Shortening to ~12 rounds moved two arrows straight into band. **The largest single lever, and it is not a stat** |
| 2 | The universal pair must actually be universal | `power`/`defense` sourced only by FORCE → it out-statted everyone on the two channels every actor needs |
| 3 | Accuracy is a gate, so it cannot be exclusive | FORCE had no accuracy source and could not hit a dodging FINESSE at all |
| 4 | Every aptitude needs a general component | Onslaught fed *only* guard-break and reflect-break → 100% dead weight against a posture with neither |
| 5 | **No posture may own only hard-counterable defences** | guard/reflect use `max(0, rate − break)/1000` — **linear, clamps to exactly 0**. Mitigation/shields/dodge/crit-denial are asymptotic or sigmoid and never reach 0. BASTION owned both hard ones → **F>B was 100% in all ten restarts**. Swapping Mitigation→BASTION and Reflect→FORCE dropped the score 1.001 → **0.318** in one change |
| 6 | A defence must fire often enough to be a defence | `parry.rate` at 90‰ was cut to 1.2% by `parry.break` |
| 7 | Constrain the search space, don't penalise afterwards | penalties left the degenerate corners reachable; they are strong attractors and the climb kept paying the fine |
| 8 | **Contests read share, not absolute points** | §3.1 part 2 |
| 9 | **A general mechanic cannot be posture-exclusive** | see below |

### 4.1 Rule 9 — the meta-rule, arrived at four times before being recognised

| General — all three postures source it | Specialised — one posture owns it |
|---|---|
| `power` · `defense` · `accuracy` · `crit` · `mitigation` | Guard · Dodge · Shields · Reflect |

Each time a general mechanic was left exclusive, **its owner's counter went absolute (100/0) and no
allocation could fix it**: `power` (rule 2), `accuracy` (rule 3), then `crit`, then `mitigation` —
which alone moved the last stuck arrow from **86.6% → 64.4%** and closed the cycle.

The specialist keeps the strongest coefficient; the other two get a real but weaker source. **That is
what turns a hard counter into a favourable matchup.**

### 4.2 Two sizing rules that fall out

- **Guard is one mechanism.** One aptitude granting *both* parry and block gave BASTION a third
  defence — two independent 27% procs, 54% of attacks guarded.
- **Neither half of a contest may saturate.** `neutralBase` is already half the hit, so a large
  `strength` coefficient reached the 95% cap at trivial investment and removal stopped responding to
  allocation entirely. Symmetrically, sigmoid `k` must put a realistic share gap near `delta ≈ 100`;
  below that the contest cannot move the outcome at all (a 7-point delta moved the miss rate 1.8%).

---

## 5. What went wrong in the process

Recorded because the process failures cost more than the wrong guesses did.

### 5.1 An uncontrolled variable ran for most of the search

Builds were assigned elements arbitrarily — `force=fire`, `finesse=air`, `bastion=earth`. The ring is
`fire → ice → earth → air → fire`, so **`earth` beats `air` and `air` beats `fire`**: the element
matchup was silently adding ±25% to two of the three arrows while postures were being tuned. Caught
only when a structural explanation ran out. All builds now share one element, making the ring neutral.

### 5.2 Six hypotheses before building the diagnostic

The `BASTION > FINESSE` arrow sat at 100% through six speculative fixes. The `explain` command — which
reports the per-side outcome breakdown — found it in **one run**: FINESSE landed a clean hit **5.9%**
of the time against BASTION's **48.2%**, because guard was eating 54.5% of its attacks. The diagnostic
should have been built first.

### 5.3 A calibration error in the tool, not the design

Magnitude edges computed `k × share × P(Θ)/P(20)` — a *ratio* — so at Θ=100 a `k=1.0` defence produced
**0.95 against damage of 7,020**. Mitigation did nothing, and two arrows pinned at 100% as a result.

---

## 6. Limitations — what this does and does not establish

**Does:** the *structure* supports a balanced, scale-invariant RPS cycle. The nine rules are properties
of the shipped combat formulas and transfer to any distribution built on them.

**Does not:**

- **These are model coefficients, not shipped ones.** `unitClass` is still `null` in
  `data/seed/derived-stats/catalog.json`; §3.1 is a measured *recommendation*, not a decision.
- **Duel fitness only.** No party composition, no elements in play (deliberately neutralised), no
  status effects, no actions or cooldowns. Focus scores 0 here and that is correct — its value is
  invisible to a duel, not absent.
- **Four aptitudes sit on the floor.** The distribution is balanced *given* a constraint that stops the
  search abandoning them, not because they earn their place.
- **One `Θ` was searched.** Invariance is verified across the ladder, but the optimisation ran at
  Θ=100 only — sound *because* invariance holds, and worth re-checking if any read mode changes.

---

## 7. Related

- [class-analytic-balance-2026-08-25.md](class-analytic-balance-2026-08-25.md) — **the sequel, and it supersedes this document's method.** The same three arrows solved in closed form: no trials, exact `Θ` invariance, and a 0.4% residual against the simulator. §5.2's lesson generalised — the diagnostic should have been built first, and the closed form *is* the diagnostic
- [class-system-map.md](../architecture/class-system-map.md) — the program this and the proof feed
- [class-system-ideal.md](../architecture/class-system-ideal.md) — the design this tests
- [power/ssot-power-scale.md](../architecture/power/ssot-power-scale.md) §2, §4.6 — `Θ` / `P(Θ)`, PS-3
- [combat-damage-ssot.md](../architecture/combat-damage-ssot.md) §6 — where every mechanism resolves
- [tools/CombatSim/README.md](../../tools/CombatSim/README.md) — the simulator
