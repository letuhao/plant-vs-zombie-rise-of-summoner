# Spec: `residual-fit` — measure what the closed form cannot express, then fit it back

**Module id:** `residual-fit` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-26 -- owner's /goal directive commands execution of the class-system plan to completion; supersedes this "awaiting owner review" header, which was never flipped after that directive landed.**

**Depends on:** `balance-guard` · `point-economy` · **Blocks:** nothing. It is the last module and it never ends.

---

## 1. Objective

Simulate what layer 1 cannot express — depleting pools, action order, party composition, live play —
**measure the gap against the prediction**, and fit the tuning config to close it.

**The owner's framing, and it is the whole justification for this module existing separately:**

> *The simulator simulates a real fight, where things the math cannot control — RNG, combination,
> timing — live. The simulator is a POC. **A real system tunes on real data.***

**Layer 3 is only meaningful once layer 1 exists.** Without a prediction there is nothing for a
measurement to disagree with, and a simulator with no model to falsify is just an expensive way to
produce a number. That is why this is last, and why it depends on `balance-guard` rather than on the
predictor directly.

**Users:** whoever runs a balance pass; the tuning config, which is this module's only output.

---

## 2. Its first two steps are fixed, not open

**This is the difference between a plan and a backlog.** Both were found while measuring, both are
known defects in the *instrument*, and until they are fixed every number this module produces is
measured through a distorted lens.

### 2.1 Step one — re-measure with **elements live**

Every dominance measurement in this program **neutralised elements** — all builds set to one element —
because an uncontrolled element matchup was silently adding ±25% to two of three arrows. **That control
was correct at the time**, and it means:

> Every result in the design record, including the dominant corner, was measured on a **1-D slice** of
> what [spec-aspect-scope.md](../demons/spec-aspect-scope.md) makes a **2-D matchup space**.

With `aspect` as a tier, a team is chosen on **posture × element**, and a single dominant posture is
much less decisive — you would still be picking aspects against the ring.

**So the dominance severity is an upper bound until this is redone, and redoing it is step one rather
than a late refinement.** Ideal §7c.7 is explicit that it should be *"the first thing `residual-fit`
does rather than the last."*

### 2.2 Step two — make **`stamina` bind**

Measured:

```text
strike        cost 1,544 stamina/round   vs   regen 3,784/round   ->  NEVER runs dry
skill-strike  cost 3,791 qi/round        vs   regen 1,872/round   ->  binds
```

> **⛔ CORRECTED 2026-08-27 (class-system-todo.md P8.2):** the `3,784` regen figure above predates this
> session's own recovery-dial fix (§5d.4a, `recovery.scaleMilli` 374) and is now stale — that dial's
> global nerf, applied for an unrelated reason (the termination invariant), already dropped
> `resource.regen.stamina` to **1,939 (Vigor) / 1,674 (Agility)** at the twelve-corner spike shape,
> with the other ten corners already **under** 1,544 as an unplanned side effect. So the reservation
> was never as wide as `3,784` implied even before this task started — only two of twelve corners
> remain open. **The determined fix — Vigor `kMilli` 1500→1063, Agility `kMilli` 1200→990 (solved
> against a 0.90 target ratio, matching Might/Bulwark's own shipped level) — is measured, Θ-invariant
> (verified empirically, not assumed) and proven by two executable regression tests
> (`TerminationGuardTests.cs`:
> `Assert_theRealTwelveCornerShape_staminaStillExceedsTheCitedCost_forVigorAndAgility_aKnownGapOwnedByP83`
> pins today's still-open gap on the shipped file; `Assert_theP82CandidateCoefficients_...` proves the
> candidate values close it for all twelve corners). Not yet published — `tools/tuning/publish.py`'s
> own T4 rule means a value change goes out as a versioned `v{n+1}`, never a hand-edit, and doing so
> correctly also means updating the ~21 hardcoded `"aptitudes.v1.json"` references found across the
> repo (including production host startup code, `RpgHost.cs:116` and `Server/Program.cs:94`) — a
> materially bigger, cross-cutting change than a single coefficient edit. `P8.3` ("fit and publish
> `v{n+1}`") owns the actual publish, bundled with the separate Vigor-vs-Bulwark joint fit §5d.4b
> requires (fixing either invariant alone moves the other — this stamina fix does not touch that HP-side
> dial and was verified not to interact with it: `TerminationGuard`/`Predictor` have no stamina read at
> all today). Full write-up: `docs/research/class-residual-2026-08-27.md`.

**An actor can `strike` forever.** So `resource.max.stamina` and `resource.regen.stamina` — five
sources each across all twelve aptitudes — currently buy **nothing**, and the physical half of the
action economy exerts no pressure at all.

> **The sizing rule, and it is the same family as every other one in this program:**
> **an action cost only matters if it exceeds the regeneration of the pool it draws on.** A cost sized
> against the *pool* looks meaningful and is not; sized against the *regen rate* it decides whether the
> economy exists.
>
> Compare the recovery rule — *regeneration is sized against peer damage, never against the pool* —
> same shape, other direction. **A number is only meaningful relative to the thing that opposes it.**

**Why this outranks any per-aptitude tuning:** `stamina` being free is the **top reservation for nine
of twelve aptitudes**. One number moves more of the distribution than any amount of per-aptitude
adjustment could.

### 2.3 What both steps have in common

Neither is a balance change. **Both are repairs to the measuring instrument**, and doing them in the
other order — tuning first, then fixing the lens — would mean fitting coefficients against a
distortion and then having to undo it.

---

## 3. What "reserved" means, and why 15–47% of the distribution is unmeasurable

Weighted by coefficient, because an edge at `k=30` is not an edge at `k=0.2`:

| Aptitude | reserved | biggest reservation |
|---|---|---|
| **Agility** | **47%** | `stamina` is free; nothing drains `spirit` |
| Might · Composure | 42% | `stamina` free; `progression.*` is meta; nothing drains `spirit` |
| Fortitude | 39% | `progression.*` is meta |
| **Focus** | **36%** | nothing drains `spirit`; `stamina` is free |
| Vigor · Precision · Onslaught | 32% | `stamina` is free |
| Ferocity | 28% | `stamina` is free |
| Bulwark | 27% | `stamina` is free |
| Pierce | 21% | `stamina` is free |
| **Retribution** | **15%** | `stamina` is free |

> **⛔ CORRECTED 2026-08-27 (class-system-todo.md P8.2):** this table is a **manual** audit
> (`_meta.measurable`'s own citation: "reader census, class-system P1.5/P1.6, 2026-08-26") — there is
> no script that produced it and none to "re-run" today; `_meta.measurable`'s own prose confirms the
> field is DESIGNED text, not a computed artifact. P8.2's determined stamina fix (see §2.2's own
> correction above) removes `` `stamina` is free `` as a valid reason for every row above once
> **published** (P8.3) — qualitatively, that drops one clause from Agility/Might·Composure/Focus's
> multi-reason cells and empties Vigor·Precision·Onslaught/Ferocity/Bulwark/Pierce/Retribution's
> single-reason cells entirely (their remaining reservation, if any, would need a fresh audit — this
> fix does not create one). **The precise re-weighted percentages are NOT recomputed here**: doing so
> faithfully needs the same coefficient-weighting method the original manual audit used, which this
> session was not able to reconstruct with confidence, and a plausible-looking guess would be worse
> than an honest gap. Building an actual reader-census **script** so this becomes mechanically
> re-runnable is P8.4's own job ("the reader census script recomputes it and fails if the file
> disagrees") — deferred there, not attempted here against a method this task cannot verify it has
> right.

> **An aptitude whose value depends on an unbuilt mechanism is a RESERVATION, not a defect.**
>
> The tempting fix is to flatten it into something the current harness can measure — give an aptitude
> more damage and it stops reading as dead. That trades **a gameplay mechanism for a measurable
> number**, and the mechanism is the point. Cooldown play, cost-reduction play and positioning play are
> *different kinds of decision*; collapsing them into damage makes the game smaller and duller in
> exchange for a green test.
>
> **Delegate the fix to the layer that owns the mechanism** (owner decision, 2026-08-26).

**So this module's job is not to close every reservation.** It is to (a) fix the two instrument
defects, (b) fit what *is* measurable, and (c) **keep the reservation table accurate** so nobody mistakes
an unmeasured coefficient for a balanced one. The config says so in its own `_meta.measurable`, and
keeping that field honest is a deliverable.

---

## 4. Commands

```powershell
cd tools\CombatSim
dotnet run --no-build -- predict  -a <builds> --theta 100 -n 4000     # model vs simulator
dotnet run --no-build -- trinity  --actions basic --status -a force --theta 100
dotnet run --no-build -- marginal -a force-ns,finesse-ns,bastion-ns --theta 100
dotnet run --no-build -- search   --analytic -m aptitudes.v1 -a force-ns,finesse-ns,bastion-ns --theta 100

# Publishing a fitted config. Never hand-edit (T4).
python tools\tuning\publish.py aptitudes <key>=<value> --publish
```

---

## 5. Project structure

```text
tools/CombatSim/                            the POC, kept - it is the falsifier, not the product
data/tuning/aptitudes.v{n}.json             the ONLY output of this module
docs/research/class-residual-<date>.md      each fitting pass, with what was measured and under what conditions
```

**This module ships no `src/` code.** If it needs a new Core type, that type belongs to whichever
module owns the concept — this one measures and publishes numbers.

---

## 6. Code style — the rules that are actually method, not syntax

**1. A model may not be optimised against while a known error in it is unfixed.**
Earned: a DoT over-count sat in a doc comment while a coefficient search ran against it. The search
converged beautifully on the wrong thing. §2.3 is this rule applied to the two instrument defects.

**2. Measure combinations, never features one at a time.**
Actions alone measured under 4%. Status alone measured under 4%. **Together they measured 15.4%**,
because status rides the action-multiplied hit. The error was invisible until both were on.

**3. Report every number with its coverage.** A residual measured with elements neutralised is a
residual measured with elements neutralised, and saying so costs one clause.

**4. A tuning change and a code change never land together** (T7). A golden that moves must be
attributable to exactly one of them.

**5. Publish versions, never hand-edit** (T4). Reverting a bad balance pass is restoring a file.

---

## 7. Testing strategy

This module's "tests" are measurements, and they are graded differently from unit tests.

| # | Check | Passes when |
|---|---|---|
| 1 | Residual, all four axes live | within the recorded band, **with its conditions stated** |
| 2 | `Θ`-invariance | exact after every fit — a fit that breaks homogeneity has broken the theorem, not the balance |
| 3 | **Termination invariant** | green. **HARD.** A fit that improves the matrix and breaks this has failed |
| 4 | Dominance matrix | measured and reported **with coverage**. SOFT — a red is a finding, not a failure |
| 5 | `_meta.measurable` | accurate for every coefficient. **A stale reservation table is a lie with a green test beside it** |
| 6 | Sizing rule | every action cost exceeds its pool's regen, or is documented as deliberately free |

**Check 3 is the one that can be lost.** Every fit changes recovery or damage, and the invariant is
`damage − recovery`. Re-running it after each pass is not optional.

---

## 8. Boundaries

**Always** — fix the instrument before fitting; state coverage with every number; re-run the hard
criterion after every fit; publish `v{n+1}`.

**Ask first**

- Closing a reservation by giving an aptitude a new mechanism. That is a **design decision** and it
  belongs to the layer that owns the mechanism (§3).
- Any fit that moves a battle golden.

**Never**

- Optimise against a model with a known unfixed error (§6 rule 1).
- Fit a coefficient whose channel is reserved — you would be fitting noise, and worse, **freezing it**.
- Measure fight length, damage dealt or kill time. **Win rate, and nothing else.**
- Apply a clock to make a matrix pass.
- Land a tuning change and a code change together (§6 rule 4).
- Hand-edit a published config (§6 rule 5).

---

## 9. Success criteria

1. **Step one done:** every headline number re-measured with elements live, and the dominance severity
   restated as a measurement rather than an upper bound.
2. **Step two done:** `stamina` binds — its cost exceeds its regen — and the reservation table
   re-computed afterwards.
3. Residuals within band, each reported with its conditions.
4. `Θ`-invariance exact after every fit.
5. Termination invariant green after every fit.
6. `_meta.measurable` accurate for every coefficient.
7. Every fitting pass has a dated research note saying what was measured and under what conditions.

---

## 10. Open — and these are genuinely open, not deferred

**10.1 The two acceptance criteria are coupled and must be solved jointly.** Nerfing regeneration to fix
an unkillable pair made the dominance problem **worse**: before the nerf a 25-round clock cleared the
dominant corner; after it, `Bulwark` dominates at every clock tested. **This does not argue against the
nerf** — an unkillable pair is the worse defect, because no later layer can repair it while a dominant
corner is exactly what a counter-passive is for. But it means the two dials move against each other and
a fit that optimises one alone will regress the other.

**10.2 Nothing here has met a real player.** A coefficient fitted against a duel is a **hypothesis about
the game**, not a measurement of it. Party composition, item interaction, action ordering under a real
timeline and actual human play are all outside the instrument. **The module never finishes**, which is
why it has no downstream dependency and why "a real system tunes on real data" is the owner's framing
rather than a caveat.

---

## 11. Design-gate checklist

```
[x] Subsystems identified: combat damage, status, shields, resources, elements, power scale,
    tunables, caps.
[x] Read this session: DESIGN-GATE.md, decisions.md (Combat resolution SSOT, Combat mitigation
    shapes, Element Hub SSOT, Resource model, Magic numbers, Caps rows), tunables-ssot.md
    (T4/T7 in full), ssot-power-scale.md §4.6/§11, class-system-ideal.md (§0.0.3, §0.1, §0.2,
    §5d.3, §5d.4a, §5d.4b, §7c.7, §8.1b, §8.1c, §8.1d, §8.8a).
[x] Every factual claim cites a document section.
[x] Verified against the MEASUREMENT record: the stamina/qi cost-vs-regen numbers, the 15-47%
    reservation table, the 15.4% combination residual and the clock-after-nerf reversal are all
    recorded run results, not estimates.
[x] Read the surrounding section of every rule quoted - §5d.4b in full for §10.1, so the coupling is
    quoted with its conclusion (the nerf was still right) rather than as a bare tension.
[x] Constraints tested, not assumed - both §2 steps are DEFECTS FOUND BY MEASUREMENT, which is the
    strongest form of this box. §6 rule 1 exists because the opposite was done once, and it cost a
    completed search.
[x] Nothing contradicts a §2 invariant.
[x] Corrections propagated - §2.1's upper-bound framing is carried in ideal §8.8a, the map §4b and
    spec-balance-guard.md §2.1; all of them land together.
```

---

## 12. Related

- [class-system-ideal.md](../class-system-ideal.md) §0.0.3 (residuals), §5d.4b (the coupling), §7c.7 (elements neutralised), §8.1b/§8.1d (stamina and the reservations)
- [spec-balance-guard.md](spec-balance-guard.md) — the checks this must keep green
- [spec-deterministic-core.md](spec-deterministic-core.md) — the prediction this disagrees with
- [tunables-ssot.md](../tunables-ssot.md) — T4 (versioned, never hand-edited), T7 (never both at once)
- [tools/CombatSim/README.md](../../../tools/CombatSim/README.md)

---

## 13. What "matchup" means for REAL data (P9.2), added 2026-08-27 — genuinely new, not covered above

§§1–12 above define matchup for CombatSim's world: two named POSTURES/archetypes (or two spiked
aptitudes), symmetric, both sides player-authored. class-system-todo.md P9.1's real corpus (nine live
web battles, a real player, 2026-08-27) exposed that this definition does not transfer: a real
expedition battle is PvE, one side is a randomly-rolled wave (never repeats exactly — no two of the
nine battles fought the identical wave), and "win rate over an exact repeat" would mean every real
matchup has n=1 forever. A different, narrower definition is needed for this data source specifically;
nothing here changes §§1–12, which still govern CombatSim's own symmetric world.

**Matchup key: `(wave id, dominant aptitude)`.**

- **Wave id** (`runs.level_name`, e.g. `rift-skirmish`) stands in for difficulty/composition — the one
  axis that is stable and repeats across many battles, unlike the exact roll of enemies within it.
- **Dominant aptitude** — the highest-share entry in the battle's own `aptitude.snapshot` event
  (P9.1's fix). Ties or an all-zero allocation report as `mixed`/`unfunded` respectively rather than
  picking one arbitrarily (the same "never fabricate a label the data doesn't support" instinct as
  "insufficient, never imputed" below).
- **Why not the full share vector**: a real player's allocation is continuous across twelve
  dimensions and any two allocations are exceedingly unlikely to match exactly — the same "always
  n=1" problem the wave axis already has. Bucketing to one dominant aptitude is the same move §7c
  already makes at the archetype layer (four aptitudes folded into one posture); this folds twelve
  continuous shares into one label the same way, one level finer since real players are not
  constrained to three named postures.
- **A run with no `aptitude.snapshot` event** (any battle recorded before P9.1's fix landed) is
  **excluded from aggregation and reported as excluded**, never silently dropped — attributing it to a
  matchup would be a guess, not a measurement.
- **Win rate**: victory-only counts as a win (§8's own "never fight length, damage or kill time," win
  rate and nothing else); defeat and stalemate both count toward the denominator but not the numerator.
- **Sparse matchups**: `insufficient: true` below a sample-count floor. The real, observed rate is
  still reported alongside the flag — "insufficient" is a warning a consumer must heed, not a reason
  to withhold real data (§6 rule 3, "report every number with its coverage").

This section is the P9.2 module's own design record — no new module id, no capability-map entry: P9.2
was already inside `residual-fit`'s scope (class-system-plan.md's own module table), and this is a
definition this module needed, not a new one.
