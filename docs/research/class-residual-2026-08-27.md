# Residual-fit pass — 2026-08-27 — P8.1, step one: re-measure with elements live

**Module:** `residual-fit` · **Task:** class-system-todo.md P8.1 · **Depends on:** Checkpoint 7 (passed)

## What was measured

The 3-archetype trio (FORCE/FINESSE/BASTION), on the REAL, class-system-owned `AptitudeModel`
(`tools/CombatSim/builds/{force,finesse,bastion}.json`'s own point shares, unchanged — read, not
edited), assigned genuinely different elements for the first time this program has done so:
FORCE=fire, FINESSE=air, BASTION=earth. This is not an arbitrary choice — it matches the assignment
`tools/CombatSim/archetypes/{force,finesse,bastion}.json` already use (see "What was NOT measured"
below for why those files themselves were not the instrument).

Element relations (`ElementRingMatrix`, `src/FusionRpg.Core/Combat/Element/ElementTable.cs:121-156`):
fire beats ice, ice beats earth, earth beats air, air beats fire (a 4-element sub-ring); light/dark
mutually strong; everything else neutral. So: air (FINESSE) beats fire (FORCE); earth (BASTION) beats
air (FINESSE); fire (FORCE) vs earth (BASTION) is **neutral** (non-adjacent in the ring).

## Method

Three scratch copies of the real build files, identical except for the `element` field (`fire` stayed
`fire`; `finesse`/`bastion` changed to `air`/`earth`), written to the session scratchpad — **not** to
`tools/CombatSim/builds/`, which is under active, concurrent, uncommitted editing by a different
session this whole day (confirmed static, `git diff --stat` unchanged, every time it was checked
today). `AptitudeModel.Resolve`/`Build.Load` check `File.Exists(nameOrPath)` before searching their
default directory, so an absolute path loads from anywhere without touching that directory — read-only
with respect to `tools/CombatSim` itself, confirmed before running anything (no `--out` passed to
`predict`, and `predict`'s own write path needs `--json` AND `--out` together, confirmed by reading
`Program.cs`'s own `Predict()` function).

`predict` runs BOTH engines together by default: `Analytic.Predict` (closed-form, confirmed
element-blind — no element parameter exists on its signature) and `Simulator.Duel` (the real Monte
Carlo engine, which DOES pin element types from each build's own `Element` field). The gap between
them (`predicted − simulated`) is exactly the element effect the closed form cannot see.

```powershell
cd tools/CombatSim
dotnet run -c Release --no-build -- predict -a "<scratch>/force-fire.json,<scratch>/finesse-air.json" --theta 100
dotnet run -c Release --no-build -- predict -a "<scratch>/force-fire.json,<scratch>/bastion-earth.json" --theta 100
dotnet run -c Release --no-build -- predict -a "<scratch>/finesse-air.json,<scratch>/bastion-earth.json" --theta 100
```

## Result

| Arrow | predicted (closed-form, element-blind) | simulated (element-live) | residual |
|---|---|---|---|
| FORCE(fire) v FINESSE(air) | 98.4% | **68.1%** | **30.3%** |
| FORCE(fire) v BASTION(earth) | 98.3% | 97.0% | 1.3% (matches `_baseline-residual.json`'s own recorded FORCE-v-BASTION figure exactly — this arrow's own element pairing is neutral, so nothing should move, and nothing did) |
| FINESSE(air) v BASTION(earth) | 6.2% (Bastion 93.8%) | 0.1% (Bastion **99.9%**) | 6.1% |

Re-run at seed 777 / 8,000 trials (vs the default seed 42 / 3,000): FORCE v FINESSE simulated **68.6%**
— stable, not a sampling fluke.

**Headline finding: elements-live SOFTENS Force's dominance over Finesse dramatically (98.4% → 68.1%)
but does NOT flip it, and does NOT produce a genuine three-way cycle.** Force remains the strongest of
the three postures even with the one element pairing that opposes it (air beats fire) fully live —
FORCE still beats FINESSE, just far less overwhelmingly, and FORCE-v-BASTION is untouched (neutral
pairing). This is a real, partial confirmation of ideal §7c.7 / spec-residual-fit.md §2.1's own
hypothesis ("a single dominant posture is much less decisive" with aspect live) — decisive dominance
becomes soft dominance — but it is not the full reversal a first, unrelated measurement (below)
suggested.

## What was NOT measured, and why — a real, structural scope boundary, not an oversight

**The 12-corner dominance matrix** (`trinity`'s own `BestResponse.DominanceMatrix`,
`tools/CombatSim/BestResponse.cs:91-116`) is **structurally element-blind, not merely
element-neutral-by-convention**: it calls `Analytic.Predict` directly (the same closed-form math
`predict`'s own "predicted" column uses), which has no element parameter on its signature at all —
confirmed by reading the function in full, not inferred. Making the 12-corner matrix elements-live
would require a code change to `BestResponse.cs` itself (e.g. routing it through `Simulator.Duel`
instead), and that file is under the exact same active, concurrent, uncommitted editing as every other
`tools/CombatSim` source file today. Editing it now would risk a direct collision with in-progress work
this session cannot safely coordinate with mid-turn — the identical judgment call class-system-todo.md's
own P3.4 already made and recorded ("a plain safety call... revisit once that stream's `tools/CombatSim`
changes land"), applied here to a second, independently-discovered instance of the same hazard.
**`_baseline-dominance.json`'s own `coverage.elementAxis` is therefore NOT changed by this pass** — the
12-corner matrix genuinely remains a 1-D slice, honestly, not silently. `class-system-todo.md`'s own
P8.1 evidence records this explicitly rather than either skipping the caveat or forcing a code edit
against a known collision risk.

**A first, misleading measurement attempt, corrected before being trusted, not after:**
`tools/CombatSim`'s own `matrix` command already runs a genuine elements-live simulator pass today, with
zero file changes, against `tools/CombatSim/archetypes/{force,finesse,bastion}.json` (force=fire,
finesse=air, bastion=earth — already assigned, unrelated to this session). Run first, it showed a
**complete reversal** (FORCE 0% v FINESSE, i.e. Finesse winning every duel) — which would have been a
dramatic, exciting confirmation of a genuine cycle, and was initially treated as one. Investigated
before being recorded as a finding: `Archetype`/`archetypes/*.json`
(`tools/CombatSim/Archetype.cs:12-25`) is a **completely separate, hand-authored, direct stat-block**
(`Hp`/`BaseDamage`/`ShieldHp`/a raw `Dictionary<string, StatRange> Stats`) — it has no connection to
`AptitudeAllocation`, `aptitudes.v1.json`, or any of this program's own point-economy machinery at all.
Its result does not describe class-system's own tuning and is **not** used as this pass's finding — the
real measurement above, against the actual `AptitudeModel`-driven builds, replaced it once the
distinction was found, rather than being reported alongside it as if the two were comparable.

## Conditions (§6 rule 3: report every number with its coverage)

Θ=100. Actions off, status off (matching every other cross-engine measurement this program has made at
this layer so far — `_baseline-residual.json`'s own recorded conditions). 3,000 simulated trials/arrow
(default), cross-checked at 8,000/seed 777 for the one surprising arrow. `aptitudes.v1.json`'s current
shipped coefficients, unchanged by this pass — this is a measurement, not a fit; no tuning value was
touched (§8: "fix the instrument before fitting" — this pass is purely instrument-fixing, step one of
two, per §2.3).

## ⛔ CORRECTED 2026-08-27 (second pass) — the 68.1% figure above was measured against the WRONG config, on BOTH axes named in "What was NOT measured"

**Both blockers named above turned out not to hold.** The "active, concurrent, uncommitted editing by a
different session" was this program's own earlier, static WIP (`git diff --stat tools/CombatSim/`
reported the exact same footprint at every check all session, which is what static, single-author WIP
looks like — a genuinely concurrent stream would have shown it changing). The owner confirmed directly:
no other session held that diff. Once that was settled, `tools/CombatSim`'s own resolver
(`AptitudeTuning.cs`/`AptitudeModel.cs`) was ported to apply `AptitudeMitigation` (P8.3's dial) the same
way `FusionRpg.Core.Stats.Aptitudes.AptitudeResolver.EffectiveKMilli` does — proven to agree within
measured floating-point/discretization tolerance by
`tests/FusionRpg.Core.Tests/Balance/ResolverMatchesSimulatorTests.cs` (class-system-todo.md P3.4).

**This exposed a second, independent staleness in the measurement above, beyond the missing mitigation
port: it read `aptitudes.v1.json` — this tool's own bundled POC copy — never `data/tuning/aptitudes.v2.json`,
the live shipped config carrying P8.2's stamina fix and P8.3's mitigation dial.** No `--models` override
was passed in the commands shown above, so it silently took the default.

**Re-measured** (`scripts/regen-class-system-baselines.ps1`, now pointing `predict --models` at the live
`data/tuning/aptitudes.v2.json` directly, same scratch-copy elements technique, same seed 8888):

| Arrow | predicted (closed-form) | simulated (element-live) | residual |
|---|---|---|---|
| FORCE(fire) v FINESSE(air) | 99.995% | **99.67%** | 0.33pp |
| FORCE(fire) v BASTION(earth) | 99.87% | 99.9% | −0.03pp |
| FINESSE(air) v BASTION(earth) | 1.91% (Bastion 98.09%) | 0.03% (Bastion **99.97%**) | 1.88pp |

**FORCE's dominance over FINESSE did not soften to 68% — it strengthened to ~99.7%, barely short of
absolute.** Isolating the cause: a same-element (fire/fire/fire, no ring effect at all) run against the
same live v2 config and the same mitigation-aware resolver produces FORCE v FINESSE 100.0%/100.0% —
so elements-live (fire vs air) contributes only a fraction of a percentage point of softening under
the current tuning, nowhere near the 30.3-point swing originally attributed to it. **The mitigation
dial was the real driver of the original 68.1% figure, not elements** — FINESSE's build
(`Agility 43 + Composure 39 + Pierce 15 + Focus 3`) leans on `combat.dodge`/`combat.crit.rate`-denial,
two families P8.3's own mitigation dial does not touch directly, but the OLD mitigation-blind
measurement was ALSO computed before P8.2's stamina-regen tightening — the combined effect of both
fixes, not either alone, is what closed most of the gap between "68%" and "99.7%". This is exactly the
"quietly wrong number, not an honestly missing one" failure mode P8.5's own finding already named for
`Analytic.Predict`/`AptitudeModel` — now measured and closed rather than just flagged.

**The 12-corner matrix's `coverage.elementAxis: "neutral"` is NOT a residual gap, and was never
correctly one to begin with — corrected understanding, not new data.** `class-system-ideal.md` §4.1
rule 2 is explicit and binding: *"Aptitudes feed `omni` only... An aptitude reaches a MECHANISM, never
a FLAVOUR... any aptitude→element mapping is arbitrary"* (§5c.10, referenced again at line 1122).
`BestResponse.DominanceMatrix`/`.Chase` spike INDIVIDUAL aptitudes, which by design never carry an
element at all — there is no principled per-aptitude element to assign, so "elements live" is not a
capability this matrix is missing, it is a property this matrix is correctly without. The
FORCE/FINESSE/BASTION 3-archetype measurement above is the ONLY place elements-live correctly applies,
because archetypes (bundles of four aptitudes each) are the layer that carries a flavour element —
exactly what this file already measures. `_baseline-dominance.json`'s own `coverage.tuningSync` note
now says this explicitly instead of describing it as an open gap.

Full downstream consequence: **class-system-todo.md P8.1 is complete**, not partially blocked — both
named blockers (the presumed concurrent edit, and the presumed missing element-axis capability) were
resolved by correcting the understanding behind them, not by building around them.

---

# P8.1, step two: make `stamina` bind — 2026-08-27

**Module:** `residual-fit` · **Task:** class-system-todo.md P8.2 · **Depends on:** Checkpoint 7 (passed)

## The cited baseline figure was already stale before this task started

spec-residual-fit.md §2.2 cites `strike cost 1,544 stamina/round vs regen 3,784/round`. That `3,784`
predates this session's own `recovery.scaleMilli` fix (§5d.4a, `1000 → 374`, solved for the termination
invariant, an unrelated HP-side reason). Since `resource.regen` is one dial's family covering **every**
recovery channel including stamina (`recovery.families: ["resource.regen"]`,
`data/tuning/aptitudes.v1.json`), that global nerf silently pulled stamina regen down too, as a side
effect nobody was targeting.

Re-measured against the real shipped config, at the same x1000-scaled twelve-corner spike shape
`TerminationGuardTests.cs`'s own Vigor-vs-Bulwark test uses (floor `4167`, spike
`100_000 − 4167×11`), Θ=100:

| Aptitude (spiked) | stamina.regen | vs cost 1,544 |
|---|---|---|
| **Vigor** | **1,939** | **exceeds — open gap** |
| **Agility** | **1,674** | **exceeds — open gap** |
| Might · Bulwark | 1,412 | binds |
| Fortitude · Onslaught · Composure · Pierce · Focus · Retribution · Precision · Ferocity | 1,063 | binds |

**Ten of twelve corners already bind, unplanned.** The real remaining task is two outliers, not a
system-wide fix — substantially narrower than §2.2's own framing ("nine of twelve" reservations)
suggested before this measurement.

## The determined fix

Solved against a target ratio of 0.90 (regen ÷ cost) — chosen because it matches Might/Bulwark's own
already-shipped, already-accepted level exactly, not an arbitrary new number:

```text
Vigor:   kMilli 1500 -> 1063   (1500 * 1544*0.90/1960 = 1063, using the small-scale Θ=100 measurement)
Agility: kMilli 1200 -> 990    (1200 * 1544*0.90/1685 = 990)
```

Verified at the x1000-scaled corner shape (the shape the regression test uses):

| Aptitude (spiked) | stamina.regen (candidate) | vs cost 1,544 |
|---|---|---|
| Vigor | 1,508 | **binds** (ratio 0.977) |
| Agility | 1,445 | **binds** (ratio 0.936) |
| all other ten | ≤1,365 | binds (unchanged shape, values shift slightly since they share edges with the same resolver, not because their own kMilli moved) |

## Θ-invariance, verified not assumed

Both changed edges are `resource.regen` — `familyRead`-classified `"magnitude"` — so, per
`ssot-power-scale.md`, they scale through the shared `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2` ladder. If an
eventual action-cost figure is *also* a `P(Θ)`-scaled magnitude (spec-action-costs.md:108,
`cost(rung, Theta) = anchorCost(Theta) * costMulti(rung)`, "ValueSpec, so it scales"), `P(Θ)` cancels in
the cost/regen ratio and the fix holds at every Θ, not just Θ=100.

Checked empirically (Vigor corner, candidate coefficients):

| Θ | stamina.regen | ratio to Θ=100 | P(Θ) ratio (C=80, A=26.2, B=0.4) |
|---|---|---|---|
| 20 | 219 | — | — |
| 100 | 1,515 | 6.92× | 6.88× |
| 500 | 20,430 | 13.49× (vs Θ=100) | 13.48× |

Matches the closed-form `P(Θ)` ratio to within the rounding noise expected from `long` truncation at
small values (larger at Θ=20, negligible by Θ=100→500) — confirms `resource.regen.stamina` is a genuine
`P(Θ)` magnitude like every other one on the ladder, not something scaling differently. The
`TerminationGuardTests.cs` regression test asserts `regen(Θ=500) > regen(Θ=20) > 0` as a standing,
lightweight version of this same check.

## Proof: two executable tests, not a claim

`tests/FusionRpg.Core.Tests/Balance/TerminationGuardTests.cs`:

- `Assert_theRealTwelveCornerShape_staminaStillExceedsTheCitedCost_forVigorAndAgility_aKnownGapOwnedByP83`
  — pins today's still-open gap against the **real shipped file**, mirroring the already-established
  Vigor-vs-Bulwark "known gap" test exactly.
- `Assert_theP82CandidateCoefficients_bringStaminaRegenUnderTheCitedCost_forAllTwelveCorners` — patches
  only the two changed `kMilli` values into the real shipped JSON text (a targeted, asserted string
  replace, not a hand-duplicated fixture — fails loudly if the shipped file's shape drifts), then proves
  all twelve corners bind, plus the Θ-invariance sanity check above.

Both green, 2026-08-27 (`--filter "FullyQualifiedName~TerminationGuardTests"`, 9/9 passed).

Also added: `ActorHub.ResolveDerived` now calls `PerfProbe.RecordValue("resource.regen.stamina", ...)`
on every resolve (`ActorHub.cs`, mirroring the existing `progression.power` call site exactly — same
mechanism, one more call site, no new plumbing, matching Checkpoint V's own evidence that this was the
only remaining work). This makes the **regen** half of "stamina binds" live in V5. The **cost** half has
no emitter anywhere in this codebase to call — `action-costs` is a separate, unimplemented program
(confirmed: zero hits for `anchorCost`/`ActionCost` under `src/FusionRpg.Core`, no
`data/tuning/*action*` file exists) — so a literal "assert cost > regen from two live V5 reads" is
structurally blocked on that program shipping its own cost computation. The regression tests above use
the cited constant (`1,544`, spec-residual-fit.md:56) in its place, exactly as the spec itself does.

## Why the fix is proven but NOT published

`tools/tuning/publish.py` was read in full before this decision. Two things it establishes:

1. **T4 ("config is versioned, never hand-edited")**: a coefficient *value* change (unlike this
   session's own earlier `pointEconomy`/`guardEconomy` **schema additions**, which added new keys with
   no prior value to preserve) must go out as a new `aptitudes.v{n+1}.json`, written by `publish.py`,
   never a hand-edit of the shipped file. `publish.py` also **refuses to add undocumented keys** — it
   only edits existing dotted paths, consistent with this being a revision tool, not a schema tool.
2. **The blast radius is real and bigger than one file.** `publish.py` writes a brand-new
   `aptitudes.v{n+1}.json` and never touches `v{n}` — so every hardcoded reference to the literal
   filename `"aptitudes.v1.json"` elsewhere in the repo would silently keep loading the OLD version
   after a publish, unless updated too. Grepped: **21 files** reference the literal filename, including
   **production host startup code** — `src/FusionRpg.Injector/Host/RpgHost.cs:116` and
   `src/FusionRpg.Server/Program.cs:94` both do
   `File.ReadAllText(Path.Combine(tuningDir, "aptitudes.v1.json"))`. No version-discovery mechanism
   exists anywhere in the codebase today (confirmed by reading both call sites directly) — publishing a
   `v2` today would not actually reach the running game or server without also editing those two files.

Given that, and given `spec-residual-fit.md` §5's own scope line ("this module ships no `src/` code") —
actually publishing here would mean either (a) silently shipping a config nothing loads, or (b) a
same-task edit to two production host files, a materially larger and more cross-cutting change than a
single coefficient fix, and one `class-system-todo.md`'s own task breakdown assigns elsewhere: **P8.3
("fit and publish `v{n+1}`")** is the dedicated, later step, and its own acceptance line ("no code change
in the same commit", T7) plus `class-system-ideal.md` §5d.4b's coupling rule (fixing the Vigor-vs-Bulwark
termination gap and the dominance matrix **must be solved jointly**, not sequenced) both point at
bundling this stamina fix with that HP-side joint fit into **one** coordinated version bump rather than
publishing twice. This stamina change does not itself touch the HP-recovery/termination path — verified,
not assumed: `TerminationGuard.Assert`/`Predictor.Predict` have no `resource.regen.stamina` read
anywhere (grepped) — so deferring its publish to ride alongside P8.3's own joint fit costs nothing and
avoids a wasted intermediate version.

## What P8.2 did NOT do, honestly

- **Did not publish** `aptitudes.v2.json` — deferred to P8.3 for the reasons above.
- **Did not re-weight the §3 reservation table's percentages** — that table is a **manual** audit
  (confirmed: `_meta.measurable`'s own citation names "reader census, class-system P1.5/P1.6,
  2026-08-26" with no script attached), and reproducing its exact weighting method without the original
  script risks a confidently-wrong number. Noted qualitatively in `spec-residual-fit.md` §3 instead
  (which cells lose the "stamina is free" reason); building the actual re-runnable script is P8.4's own
  named job.
- **Did not touch `recovery.scaleMilli` or any HP-side dial** — that is the separate, coupled §5d.4b fix
  P8.3 owns jointly with dominance.

---

# P8.3: the termination invariant, fully swept — a much bigger finding, and a joint two-dial fix

**Module:** `residual-fit` · **Task:** class-system-todo.md P8.3 · **Depends on:** P8.2

## The real scope: 30 of 66 pairs, not 1

`TerminationGuardTests.cs`'s own P5.2 test caught exactly one hand-picked pair (Vigor vs Bulwark),
because it mirrored `tools/CombatSim`'s own `trinity` corner. This task swept **every** `C(12,2) = 66`
unordered pair through `FusionRpg.Core`'s own `Predictor.Predict` (the SAME closed form
`TerminationGuard.Assert` uses internally — not `tools/CombatSim`'s separate `Analytic.Predict`, which
remains blocked by that tool's own concurrent-edit hazard, unchanged this whole session per repeated
`git diff --stat` checks). Cross-checked against the real, public `TerminationGuard.Assert` entry point
directly (not just the scratch replication) for four pairs, confirming the finding is real, not a
measurement artifact.

**Result: 30 of 66 pairs are unending on the shipped config**, not one. Eight aptitudes — Fortitude,
Vigor, Agility, Composure, Focus, Bulwark, Retribution, Precision — share an **identical** floor-level
`combat.power.omni` (1,217 at Θ=100, the twelve-corner spike shape): none of them sources a direct
offense edge, so a pure spike into any of them buys the same, small, fixed amount of damage output
regardless of which one it is. This produces a near-perfect mutual-stalemate **clique**: 28 of the 28
possible pairs among those eight are unending, plus two cross-cluster outliers (Fortitude vs Onslaught,
Bulwark vs Ferocity — the weakest offense aptitude and a defense-plus-max-hp outlier respectively).

## Why `recovery.scaleMilli` alone cannot fix it

Read `Predictor.Predict` in full (`src/FusionRpg.Core/Balance/Analytic/Predictor.cs:78-210`) rather than
guessing: `NetAttritionA = rateA - recovA`, where `recovA` is `PhaseModel.RecoveryPerRound` — for an
actor with no shield (this program's own `ToActor`/`TerminationGuard.ToActor` construction, `ShieldMaxHp:
0`), that reduces to exactly `hpRegen + poiseRegen` (poise reads 0 today, no edge feeds it yet). So
`recovery.scaleMilli` — a single multiplier over `resource.regen`/`combat.shield.regen` — is the only
existing lever, and it works: swept from 374 down to 26, violations fall 30 → 25 → 10 → 1 → 0
(monotonic, matching the formula's own linearity in `hpRegen`).

**But at `scaleMilli = 26`, `Might` becomes a fully dominant corner** — beats all eleven others outright,
confirmed against `BestResponse.cs`'s own documented definition ("beats all others means one build wins
the game outright") applied via the same `Predictor.Predict` sweep. This is `class-system-ideal.md`
§5d.4b's own warning ("the two invariants trade against each other... fixing either alone moves the
other"), just far more severe than its own first measurement (374, not 26) ever showed, because closing
30 violations instead of 1 needs a much deeper cut. **Trading 30 stalemates for one absolute-dominant
corner is not an improvement** — §8.8b's own worst-case failure mode, not a lesser one.

## Root cause: two DIFFERENT survival mechanisms, only one of which recovery.scaleMilli reaches

A targeted experiment — cutting only the SEVEN non-Retribution clique members' own `resource.regen.hp`
kMilli (Retribution deliberately excluded: its own hp-regen is what keeps `Might` in check, `0.11-0.13`
win share against it on the real config; folding it into the cut was tried FIRST and measured to make
`Might` dominant — a real, planted-then-rejected mistake) — closed the clique from 30 down to 6, not 0.
The residual six (all pairs among Fortitude/Agility/Composure/Bulwark) barely moved even as the hp-regen
cut got much more aggressive (kMilli near 0), which was the signal something else was load-bearing.

Reading each of those four aptitudes' own edges (`data/tuning/aptitudes.v1.json`) found it: **their
PRIMARY survival stat is not hp-regen at all.**

| Aptitude | hp-regen kMilli | Its own DOMINANT defensive edges |
|---|---|---|
| Bulwark | 300 (floor) | `parry.rate` 4,000, `block.rate` 4,000, `parry.strength` 1,500, `block.strength` 1,500, `defense` 900, `absorption` 1,000 |
| Fortitude | 300 (floor) | `defense` 3,000 |
| Composure | 300 (floor) | `heal.power` 8,000, `defense` 700, `crit.resist` 3,000+3,000 |
| Agility | 300 (floor) | `dodge` 3,000, `crit.resist` 900+900 |

`recovery.scaleMilli`'s own `families` (`resource.regen`, `combat.shield.regen`) never reached
`combat.defense`/`dodge`/`parry`/`block`/`absorption`/`heal.power` at all — these channels were reading
their full, un-damped value the entire time. Bulwark's own parry+block investment alone (11,000 kMilli)
dwarfs its 300 hp-regen by more than 36×; cutting hp-regen to near-zero barely touches its real
survivability.

## The fix: a second, independent dial — `AptitudeMitigation`

Built `src/FusionRpg.Core/Stats/Aptitudes/AptitudeTuning.cs`'s `AptitudeMitigation(ScaleMilli, Families)`
— structurally Recovery's own sibling (same `ScaleMilli`-over-`Families`-prefix-match shape,
`AptitudeResolver.EffectiveKMilli` extended with an `isMitigation` branch), but a **separate** field
entirely, because sharing Recovery's own 374 would apply a value SOLVED for hp-regen specifically to
channels (`parry.rate` at 4,000+) that were never scaled before at all — a naive shared-dial experiment
confirmed this: extending `recovery.families` to include the mitigation channels at the SAME 374 barely
moved violations (23 remaining), and driving that SHARED dial low enough to help gutted Bulwark's own
kit far more than intended. Two independently-sized dials were the only combination that worked.

**Calibrated (12x12 sweep, direct measurement, not a closed-form derivation like Recovery's own r):**

```text
resource.regen.hp:  Fortitude 300->21, Vigor 1200->83, Agility 300->21, Composure 300->21,
                     Focus 300->21, Bulwark 300->21, Precision 300->21   (Retribution untouched: 800)
mitigation.scaleMilli: 1000 (no-op) -> 300
```

**Result: 0 of 66 pairs unending, and no absolute-dominant corner — verified at Θ = 20, 100, 500, AND
2000**, not just the one point every other measurement in this program has used. Necessary because half
of the eight mitigation channels are `familyRead`-classified `contest` (Θ-free: `dodge`, `parry.rate`,
`block.rate`, `absorption`) and half are `magnitude` (Θ-scaled: `defense`, `parry.strength`,
`block.strength`, `heal.power`) — a genuinely mixed-mode dial, unlike Recovery (purely `magnitude`), so
Θ-invariance could not be assumed the way it could for the stamina fix in the P8.2 section above; it was
checked, and it held.

## Proof

`tests/FusionRpg.Core.Tests/Balance/TerminationGuardTests.cs`:

- `Assert_theShippedConfig_hasThirtyUnorderedTerminationViolations_aMajorGapFoundThisSession` — pins the
  exact count (30, not `>0`) against the real shipped file, via the real `TerminationGuard.Assert`.
- `Assert_theP83CandidateCoefficients_closeEveryTerminationViolation_withNoAbsoluteDominantCorner` —
  patches the seven `resource.regen.hp` values and `mitigation.scaleMilli` into the real shipped JSON
  text (targeted, asserted string replaces), then proves zero violations AND no absolute-dominant corner
  at all four Θ points above.

Both green (`--filter "FullyQualifiedName~TerminationGuardTests"`).

## What shipped now vs. what is still deferred

**Shipped now (schema only, `_note`-documented, T4-compliant):** `AptitudeMitigation` type +
resolver wiring + `data/tuning/aptitudes.v1.json`'s new `mitigation` block at a **no-op** `scaleMilli:
1000` — a schema addition, same class as this session's own earlier `pointEconomy`/`guardEconomy`
blocks, not a value change, so hand-editing it into v1 does not violate T4.

**Still deferred to the actual `tools/tuning/publish.py` invocation** (this task determines and proves
the numbers; publishing them is the mechanical step still pending, same split already established for
P8.2's own stamina fix): the calibrated `mitigation.scaleMilli: 300` and the seven `resource.regen.hp`
value changes, bundled with P8.2's own two stamina `kMilli` values into ONE coordinated `v2` — plus the
21-reference blast radius (`RpgHost.cs:116`, `Server/Program.cs:94`, and 19 more) that publishing a real
`v2` requires updating, or the running game/server would silently keep loading `v1`.

---

# Final status — published, wired, and verified end to end (2026-08-27)

Everything the two sections above deferred is now done, not just proposed:

**`tools/tuning/publish.py` couldn't reach `edges` (a JSON array) as originally built — extended, not
bypassed.** `set_path` walked dotted paths through nested dicts only; added a bracket selector,
`name[k1=v1,k2=v2]`, matching one array element by exact field equality (refusing on zero or multiple
matches, same "refuse, don't guess" posture as a missing dict key). Matches `tunables-ssot.md` §7.1's own
anticipated shape: "the second domain generalises it if the shape holds" — `aptitudes` is that second
domain, `contracts` the first. A real bug in the extension was caught and fixed *before* it touched any
real file: the top-level CLI `key=value` split grabbed the first `=` in the whole argument, which sits
INSIDE a selector's own `channel=resource.regen.hp` clause, not the actual separator — found via
`sys.argv` reflection, fixed with a bracket-depth-aware `split_kv`. Verified with 6 in-memory unit checks
(regression on plain paths; correct-edge selection tested in both directions among channel/source
duplicates; zero-match and ambiguous-match refusal) and a full dry run against a throwaway-domain copy of
the real file — parsed back through the actual C# `AptitudeTuningLoader`, swept at all four Θ points,
zero violations — before running it against the real `aptitudes` domain.

**Published**: 10 changes (2 stamina, 7 hp-regen, 1 mitigation scale) via one `publish.py` invocation.
`aptitudes.v1.json` stays on disk, untouched, forever (T4). `aptitudes.v2.json` written.

**The 21-reference blast radius resolved, not left catalogued.** Production hosts first, since they're
load-bearing: `RpgHost.cs` and `Server/Program.cs` both now load `aptitudes.v2.json`, with their wiring
kept character-identical after whitespace normalization (the exact check
`AptitudeHostInjectionTests.BothHosts_useTheIdenticalWiringPattern` runs) — verified by replicating that
test's own extraction logic in Python before trusting the edit, not by eyeballing two files. Six test
files' own shipped-path finders updated; `AptitudeTuningHub.cs`'s doc comment made version-agnostic so
this exact staleness can't recur silently on v3; `tools/ProveAptitude/Program.cs` updated. Left alone,
correctly: every reference to `tools/CombatSim/tuning/aptitudes.v1.json` (a different file, in the still
concurrent-edit-blocked directory), `.remember/` session notes, a frozen historical baseline snapshot,
and dated prose describing a specific past moment where "v1" is factually what existed at the time.

**Four `TerminationGuardTests.cs` tests had to be redesigned, not left broken, once v2 actually shipped**
— they'd asserted the OLD (v1) broken behavior against "the shipped config," which the publish made
false. Caught by running them, not assumed safe. Split into permanent v1-historical-record tests (load
`aptitudes.v1.json` explicitly by name — stays on disk forever) proving *why* v2 shipped, and live
v2-correctness tests checking the real, current file directly with no more string-patching needed (v2
already contains the calibrated values). The original P5.2 Vigor-vs-Bulwark test had the identical
problem and got the identical treatment.

**A real regression caught by the full-suite run itself, not by inspection**:
`AptitudeTuningTests.ParsesTheShippedFile` still asserted `tuning.Version == 1` — `publish.py` bumps the
JSON document's own `"version"` field on every publish (a field separate from the filename), and this
one assertion was missed when the sibling `Mitigation.ScaleMilli` assertion in the same test was fixed
earlier. First full-suite run reported it (`Expected: 1, Actual: 2`); fixed; re-verified.

**Final numbers, everything re-run after every fix above:** `--filter
"FullyQualifiedName~TerminationGuardTests"` 11/11; `--filter "FullyQualifiedName~AptitudeTuningTests"`
27/27; full `Core.Tests` **3,838/3,838**; `Guard.Tests` 111/111; `Server.Tests` 19/19; `Data.Tests`
484/484. `guard-class-system.ps1`/`guard-power.ps1`/`guard-dal.ps1`/`guard-single-writer.ps1` all pass
(G3 the sole, permanent, decision-12 exception — pre-existing in v1 too, confirmed unrelated to this
task). `audit-magic-numbers.py`/`audit-overflow.py`: unchanged from baseline.

**T7 ("no code change in the same commit as the tuning publish") — for the owner's own commit split**,
since this session does not commit (AGENTS.md, git hands-off): the DATA side is exactly one new file,
`data/tuning/aptitudes.v2.json` (`v1` untouched). The CODE side is everything else this pass touched —
`AptitudeTuning.cs`/`AptitudeResolver.cs` (`AptitudeMitigation`), `ActorHub.cs` (P8.2's regen
`RecordValue`), `RpgHost.cs`/`Server/Program.cs` (the version bump + host wiring), `tools/tuning/publish.py`
(the selector extension), and every touched test file.

---

# P8.5: re-run dominance and record — a second, independent sync gap discovered

**Module:** `residual-fit` · **Task:** class-system-todo.md P8.5 · **Depends on:** P8.3 (published)

## `_baseline-dominance.json` cannot be regenerated correctly via `trinity` right now — a NEW finding

`_baseline-dominance.json` (per `scripts/regen-class-system-baselines.ps1`) is produced by
`tools/CombatSim`'s own `trinity --json` command. Read `Trinity()` directly
(`tools/CombatSim/Program.cs:558`): it loads `var modelName = o.Models ?? "aptitudes.v1"` — **`tools/CombatSim`'s own INTERNAL copy**, `tools/CombatSim/tuning/aptitudes.v1.json`, not the shipped
`data/tuning/` config at all. That internal copy is a tracked file under the same directory already
under an active, uncommitted, concurrent-edit hazard from a different session all day (confirmed static
via repeated `git diff --stat` checks) — the identical hazard already blocking P8.1's own elements-live
12-corner re-measurement (P3.4's own class of problem).

**Worse than P8.1's own gap**: even loading `data/tuning/aptitudes.v2.json` into `trinity` via its own
`--models <absolute-path>` override (`AptitudeTuning.Load` → `AptitudeModel.Resolve`, confirmed to check
`File.Exists(nameOrPath)` first, the same safe pattern used throughout this session) would not produce a
CORRECT measurement: `tools/CombatSim`'s own `AptitudeTuning.cs`/resolver has no concept of the new
`AptitudeMitigation` dial at all — `System.Text.Json`'s default deserialization silently ignores the
unrecognized `mitigation` block rather than erroring, so `trinity` would load v2's file, apply the
stamina fix's edge values correctly (`resource.regen.stamina`/`resource.regen.hp` are plain edges,
`Load`-agnostic to which dial adjusted them), but **silently skip the entire mitigation-dial scaling** —
producing numbers that look plausible but are quietly wrong, worse than an obvious failure. Not
attempted for that reason: a subtly-incorrect measurement is worse than an honestly-labeled stale one.

## The real, v2-accurate dominance picture — measured via `FusionRpg.Core.Predictor` directly

Since `tools/CombatSim` cannot currently produce a correct answer, and since `FusionRpg.Core.Predictor`
is the SAME closed form `TerminationGuard.Assert` (the production guard) already uses — not a
re-derived formula, the actual SSOT — this is the accurate record until the sync gap closes. Same
twelve-corner spike shape as every other measurement in this document, Θ=100, no round limit, no
shields, no action economy, no status:

```
               Migh  Fort  Vigo  Onsl  Agil  Comp  Pier  Focu  Bulw  Retr  Prec  Fero
Might          0.50  1.00  1.00  1.00  1.00  1.00  0.98  1.00  1.00  0.13  1.00  0.97
Fortitude      0.00  0.50  1.00  0.00  1.00  1.00  0.00  1.00  1.00  0.00  1.00  0.00
Vigor          0.00  0.00  0.50  0.00  0.70  0.00  0.00  1.00  0.00  0.00  0.34  0.00
Onslaught      0.00  1.00  1.00  0.50  1.00  1.00  0.25  1.00  1.00  0.00  1.00  0.39
Agility        0.00  0.00  0.30  0.00  0.50  0.00  0.00  0.98  0.00  0.00  0.03  0.00
Composure      0.00  0.00  1.00  0.00  1.00  0.50  0.00  1.00  0.00  0.00  1.00  0.00
Pierce         0.02  1.00  1.00  0.75  1.00  1.00  0.50  1.00  1.00  1.00  1.00  0.62
Focus          0.00  0.00  0.00  0.00  0.02  0.00  0.00  0.50  0.00  0.00  0.00  0.00
Bulwark        0.00  0.00  1.00  0.00  1.00  1.00  0.00  1.00  0.50  0.00  1.00  0.00
Retribution    0.87  1.00  1.00  1.00  1.00  1.00  0.00  1.00  1.00  0.50  1.00  1.00
Precision      0.00  0.00  0.66  0.00  0.97  0.00  0.00  1.00  0.00  0.00  0.50  0.00
Ferocity       0.03  1.00  1.00  0.61  1.00  1.00  0.38  1.00  1.00  0.00  1.00  0.50
```

**No absolute dominant corner** (this repo's own bar: beats all 11 others). **Retribution is the new
near-dominant corner** — wins 10 of 11, loses only to Pierce (0.00) — a materially different picture
from v1's own record (`dominantCorners: []`, but the underlying `wins` matrix shows Vigor and Bulwark
each near-total against every OTHER corner, tying only each other — the exact unending pair P8.3 fixed).
The dominance PROBLEM moved, it did not disappear: fixing termination (§5d.4b's own coupling warning,
now confirmed a second time on live v2 data) shifted the "hardest to beat" build from a stalemate pair
to a single reflect-based aptitude that punishes the roster's own now-more-numerous decisive winners.

**Not a new defect requiring an immediate fix** — P8.5's own verify line explicitly allows this: "if
still red, that is a recorded number with an owner." Retribution's own near-dominance is SOFT (§8.8b,
not §5d) and, per this program's own `class-system-ideal.md` reasoning, exactly the shape a future
counter-passive/anti-reflect mechanism is supposed to answer, not something this phase should chase by
further hand-tuning without measurement (matching the same restraint already applied throughout Phase
8 — see the P8.3 section above's own rejection of blind iteration).

## `_baseline-dominance.json` updated honestly, not silently left stale

Added `coverage.tuningSync`, naming this exact gap and pointing at this section as the current, accurate
record. Did NOT re-run `trinity --json` to refresh `dominanceMatrix`/`chains`/`_meta.measuredAt` — doing
so would reproduce byte-identical numbers (nothing in `trinity`'s own inputs changed) and risk implying
a fresher measurement exists when none does. The file's pre-existing `dominanceMatrix`/`chains`/
`dominantCorners` content is left exactly as it was — a correct record of what v1 (via `tools/CombatSim`'s
own internal copy) measured, now explicitly labeled as not the current shipped config's own picture.

## Conditions

Θ=100. Actions off, status off, no shields, no round limit — matching every other closed-form
measurement in this document. `data/tuning/aptitudes.v2.json` (the real, live, published shipped
config), read via `FusionRpg.Core.Balance.Analytic.Predictor.Predict` and `TerminationGuard.ToActor`'s
own actor-construction pattern (independently replicated, cross-checked against the real, public
`TerminationGuard.Assert` entry point for 4 pairs earlier this session — same instrument already
verified faithful for the P8.3 measurement above).

---

# P8.6/P8.7: the tuning loop, end to end — and a real incident caught and fixed the same turn

**Module:** `residual-fit` · **Task:** class-system-todo.md P8.6/P8.7 · **Depends on:** P8.3 (published)

## Design

`spec-residual-fit.md` §5: "this module ships no `src/` code... measures and publishes numbers." Built
`tools/ResidualFitLoop` (a standalone console tool, matching `tools/ProveAptitude`'s own shape) rather
than adding fit logic to `FusionRpg.Core` — it calls the SHIPPED resolver
(`ActorHubBootstrap`/`AptitudeResolver`), guard (`TerminationGuard.Assert`), and telemetry mechanism
(`PerfProbe.RecordValue`, the SAME call sites `ActorHub.ResolveDerived` already carries for
`progression.power`/`resource.regen.stamina` — P1.10/P8.2) as black boxes, reimplementing none of
them. Runs on **simulated** (closed-form) data only, matching P8.6's own verify line — no server, no
`PerfProbe`-over-HTTP hop; that leg is Phase 9's own job (P9.1), not this one's.

Five phases, one command:

1. **Run** — resolve all twelve spike corners through the production resolver (fires the shipped
   `PerfProbe.RecordValue` call sites automatically) and sweep all `C(12,2)=66` pairs through the real,
   public `TerminationGuard.Assert`.
2. **Emitted metrics** — `PerfProbe.SnapshotAndReset()`.
3. **Aggregate** — per-corner `stamina.regen`, and the full list of termination violations.
4. **Fit** — a proportional-target rule (P8.2's own methodology, generalised): any metric exceeding a
   *cited* external target is scaled toward 90% of that target (the same, already-accepted ratio
   Might/Bulwark already ship at). Refuses to touch any channel in a hardcoded reserved-family list
   (mirroring `_meta.measurable`'s own prose) before computing anything, per P8.7.
5. **Publish** — shells out to `tools/tuning/publish.py` with the computed changes.

**Deliberately not attempted**: an automated guarded search for termination-violation fits that also
avoid creating a new absolute-dominant corner (P8.3's own harder case — a targeted cut tried and
rejected by hand after measuring it made `Might` dominant). Zero violations exist on the current shipped
config, so nothing today needs it; building a search loop that can be trusted to reproduce that same
judgment safely is real, separate scope this pass does not claim to have closed.

## A real design flaw, caught by testing the tool against a known input — not by inspection

First full run against `aptitudes.v1.json` (a deliberate regression check: does the fit algorithm
reproduce P8.2's own already-accepted, by-hand fix?) correctly recomputed a real cut to
`resource.regen.stamina` for Vigor and Agility — **but the FIRST draft of the reserved-family list also
refused to fit it**, since `_meta.measurable`'s own prose still calls the whole stamina/qi/hunger/spirit
family "unmeasurable." That would have made the loop unable to reproduce its own program's own already-
accepted fix. Fixed by distinguishing "no reader and no target" (genuinely reserved) from "no reader but
a real, cited target exists" (stamina's own case, via spec-residual-fit.md:56's `1,544`) — only the
first kind is refused.

**A second, more serious incident, caught immediately and reverted the same turn.** Testing the FULL
chain including publish (via a throwaway domain copy, `loopdrytest.v1.json`) revealed that
`ResidualFitLoop`'s own publish step hardcodes the `aptitudes` domain name — it does not derive the
domain from `--input`. Running `--input data/tuning/loopdrytest.v1.json` therefore measured against the
throwaway file but **published against the real, live `aptitudes` domain**, silently creating a real
`data/tuning/aptitudes.v3.json` on top of the already-correct, already-published `v2`, with numbers
computed from the wrong input. `v1`/`v2` themselves were never touched (`publish.py`'s own T4 guarantee
held even through this mistake) — but the spurious `v3` was real, on disk, in the actual `data/tuning/`
directory. Caught by checking the tool's own console output and `ls data/tuning/` immediately after the
run, not by a later review pass. Reverted immediately: `rm data/tuning/aptitudes.v3.json
data/tuning/loopdrytest.v1.json`, confirmed only `v1`/`v2` remained.

**Root-caused and fixed, not just reverted.** Added a safety check before the publish step:
`LatestAptitudesFileName` mirrors `publish.py`'s own `latest_version(domain)` logic in C#, and the loop
now refuses to publish unless `--input` resolves to the EXACT file that domain's own latest version
already is. `--dry-run` remains available for inspecting a non-live input safely. Verified the fix
directly: re-running the same mismatched-input command (without `--dry-run`, the exact shape that caused
the incident) now exits non-zero with an explicit refusal message, and creates no new file.

## Proof

`tests/FusionRpg.Core.Tests/Balance/ResidualFitLoopTests.cs`, invoking the real, cold `dotnet run`
against the shipped tool (same pattern as `ProveAptitudeJsonEmitTests`) — not a reimplementation of its
logic:

- `DefaultInvocation_onTheLiveShippedConfig_findsNothingToFit` — the real, safe, no-args production
  shape finds zero changes against the already-fixed v2 and never reaches `publish.py`.
- `Run_isDeterministic_identicalInputProducesIdenticalComputedChanges` — two `--dry-run` runs against
  identical input compute byte-identical fit output (P8.6's own determinism clause).
- `Run_againstV1_reproducesP82sOwnStaminaFit_notJustNoOp` — proves the fit ALGORITHM independently
  recomputes P8.2's own real fix against the known-broken v1 config, not merely a no-op.
- `PlantedReservedCoefficient_isRefusedAndSaidSo_neverFit` — P8.7's own literal acceptance line, proven.
- `PublishAttempt_withMismatchedInput_isRefused_notSilentlyAppliedToTheLiveDomain` — the permanent
  regression proof for the incident above; deliberately NOT `--dry-run`, the exact call shape that
  caused it.
- `FullChain_onAThrowawayDomain_actuallyPublishes_andTheResultParsesAndBinds` — proves the complete
  run→metrics→aggregate→fit→publish chain reaches `publish.py` and writes a real, valid, corrected file,
  against a disposable domain that can never collide with the real `aptitudes` one.

## Follow-up: a post-publish verification step, and `--domain`

Checkpoint 8's own "Θ-invariance exact and termination green **after every fit**" reads as a standing
loop property, not a fact true of today's config alone — added a step after `publish.py` succeeds that
re-loads the file it just wrote and re-sweeps all 66 pairs at Θ=20/100/500/2000 directly against the
published artifact (never re-trusting the pre-publish computation that produced it). Also added
`--domain` (generalising `LatestAptitudesFileName` into `LatestTuningFileName(dir, domain)`) so the
tool's own publish call — not a hand-rolled equivalent — can be driven safely against a disposable
name; the incident above happened specifically because `--input` could redirect measurement without
also redirecting publish.

**The new step immediately proved itself**, not just in the abstract: publishing the stamina-only fit
(Pattern A) against a throwaway `v1` copy correctly WARNS that termination is still violated, since
Pattern B (the guarded search) is deliberately unimplemented — the warning fired exactly where it
should, on a fit this pass always knew was partial. `PostPublishVerification_warnsHonestly_...` pins
this as a permanent regression test, and `FullChain_onAThrowawayDomain_...` was rewritten to go through
the tool's own `--domain`-scoped publish call instead of a parallel, manual `publish.py` invocation.

`ResidualFitLoopTests.cs`: **7/7 green**. Full `Core.Tests`: **3,848/3,848 green**. Magic-number/overflow
audits: unchanged from baseline aside from this pass's own additions (0 new findings).
