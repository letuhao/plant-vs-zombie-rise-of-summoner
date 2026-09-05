# Spec: `squad-harness`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `squad-harness` · **Wave:** 0 · **Depends on:** — · **Depended on by:** — (nothing
imports it; it settles tunables the other modules name by key)

> ⚠️ **Citation convention: `BattleModels.cs`, `BattleRunState.cs` and `BattleEngine.cs` are cited by
> SYMBOL, never by line.** `battle-tempo` and `base-defense` are editing all three right now — all
> three are dirty in the working tree this session — and this spec reads Battle more than any other in
> the program. The 2026-09-05 seam audit found ten stale line citations here and published
> corrections; re-checked this session, **several of those corrections had themselves already drifted**
> (`ActiveCommanderAura` :266 → :279, `BattleOutcome.Stalemate` :272 → :285, `RecomposeDerived`'s call
> site :323 → :343). A symbol survives an edit; a line number does not. Files outside those three keep
> `file:line`, and the one line pair this spec still quotes — `BattleModels.cs:218-221` in §11 — is
> quoted because the correction itself is the argument, and it was re-read this session.

---

## Objective

**The question, in one sentence: does the 1v1 ordering this program's balance rests on survive at the
six-actor scope the game is actually played at?**

Every measured statement in `passive-tree-ideal.md` is a duel. `DominanceGuard.Measure` builds one
`Predictor.Actor` per build and calls `Predictor.Predict(actors[i], actors[j])` for each ordered pair
(`DominanceGuard.cs:44-57`, the call itself at `:55`). The game fields six: `BuildSquad` guards
`const int maxSquad = 6` (`WebMatchService.BuildSquad`) and `BattleEngine` resolves `"squad"` against
`"wave"` until one side has no active actor (`BattleEngine.Resolve`, whose outcome ternary reads
`AnyActive("squad")` and `AnyActive("wave")`). D21 gives each of
those six its own tree state, its own share vector, its own `H` and its own `F` — so a player fielding
six pure corners collects the concentration reward six times while the *squad* covers every defensive
layer breadth was supposed to buy ([11-adversarial-debate.md §6a](../../research/passive-tree/11-adversarial-debate.md)).

That is the arbitrage D33 names: **`H` measures commitment in a scope narrower than the scope at which
power is delivered.**

**Who reads the output.** The owner, when setting `concentration.fmaxMilli` and `concentration.wMilli`; and
`tree-plan` / `tree-resolve`, which name those keys in their specs and take whatever value lands.

**What success looks like.** Three columns of the same table — 1v1 closed form, 1v1 trials, 6v6 trials
— with a stated resolution, and a one-word verdict per column saying whether the build-class ordering
is the same. Two further measurements ride the same machinery and are named rather than smuggled in:
**A10's Erosion differential** (§10.1), which the map's `tree-language --write` gate reads, and
**D15's budget-vs-value evidence** (§11 S4), which no other module was scoped to produce. All three
are numbers and verdicts: this module **ships no balance change and writes no `data/tuning` value**
(map assumption 5, `passive-tree-map.md:92-93`).

---

## Design

### 1. What a "squad build" is

A **squad build** is an ordered list of six **actor builds**. An actor build is exactly what the 1v1
sweep already measures, plus the tree state D21 gives it:

```text
ActorBuild  = (AptitudeAllocation allocation, TreeShare[] treeShares, long theta)
SquadBuild  = (string id, string kind, ActorBuild[6] actors)
```

`AptitudeAllocation` is the shipped type (`AptitudeAllocation.cs:28`), summed over scopes before share
is taken (`:51-57`). `treeShares` is the tree layer expressed in the same aptitude-point-equivalents
`tools/HybridViability` already uses (`Program.cs:216-220`) — this module models trees exactly as that
tool does and adds no new resolver math (§4).

An actor build is constructed with the **same corner-shape helper** the duel sweep uses — spike `k`
aptitudes, floor the other `12 − k` at `4167` per-mille (`HybridViability/Program.cs:79-95`). Keeping
one constructor is what makes the two scopes comparable at all; a squad member built a different way
would make every difference in §5's table unattributable.

#### 1.1 Per-actor build state has no read path today, and the harness must construct it

`WebMatchService.AptitudeChannelMods` merges exactly two scopes — the commander allocation and
`EffectiveSpeciesAllocation` (`WebMatchService.AptitudeChannelMods`) — and the species scope keys on
`player:{playerId}:species:{speciesId}` (`SpeciesAllocation.cs:15`). **So two squad members of the same
species cannot differ today.** Counted across non-test `src/`: `AllocationScope.Commander` 19
occurrences, `DemonType` 21, `Aspect` 3, `UniqueDemon` 3 — and all three `UniqueDemon` sites are a
tuning rate row (`AptitudeTuning.cs:204`) and a scope-key string pair (`RpgStore.Aptitudes.cs:58,67`).
There is no producer and no reader.

This is a **wiring gap, not a wall**, and it is `tree-state`'s to close. What it means here is narrow
and must be said plainly: the harness builds its six actor builds **in memory**, never through
`RpgStore`, so it measures the shape D21 designs rather than the shape the store can currently
persist. §14 open question 1 is the owner's call on whether the *shipped* shape should also be
measured, because the answer decides whether D33's arbitrage exists today or only after D21 lands.

#### 1.2 The sweep, sized

**Duel roster** — the identical 91 builds `tools/HybridViability` constructs (`Program.cs:106-115`):
12 corners + 66 two-way (C(12,2)) + 12 three-way + 1 even-twelve = **91**, giving 91 × 90 = **8,190**
ordered pairs.

**Squad roster** — 23 squads, giving 23 × 22 = **506** ordered pairs:

| id shape | composition | count |
|---|---|---|
| `mono-<apt>` | six copies of one corner | 12 |
| `posture-<p>` | six actors round-robin over one posture's four corners | 3 |
| `rainbow-balanced` | six distinct corners, two per posture — **the D33 exploit build** | 1 |
| `rainbow-force-finesse` | six distinct corners, three FORCE + three FINESSE | 1 |
| `mono-spread` | six copies of even-twelve | 1 |
| `mono-hybrid2-<a+b>` | six copies of one 2-way hybrid, one sampled per posture pair | 3 |
| `mono-hybrid3-<a+b+c>` | six copies of one 3-way hybrid | 1 |
| `mixed-corner-spread` | three corners + three even-twelve | 1 |

Posture is **read** from the shipped catalog (`AptitudeCatalog.Get(id).Posture`), never re-declared —
the rule `HybridViability/Program.cs:307` already follows.

The roster is deliberately small. The full space of six-actor squads over 12 corners is 12,376
multisets and its ordered matchup matrix is ~153 million; measuring it is not the question. The
question is whether *these* named shapes reorder, and 23 named shapes answer it.

### 2. Squad vs wave, or squad vs squad — what the shipped code supports

**The engine resolves `"squad"` against `"wave"`, and that is the only shape it has.** `BattleSetup`
carries exactly two lists (`BattleSetup.Squad` and `BattleSetup.Wave`); `Resolve` throws on an empty
either side (its first two statements) and ends when `AnyActive("squad")` or `AnyActive("wave")` goes
false.

**But both lists are `IReadOnlyList<BattleActorSetup>`, and combat is side-symmetric.** The complete
set of side asymmetries in the resolver, by grep over `src/FusionRpg.Core/Battle/`:

| Asymmetry | `file:line` | Effect on a measurement |
|---|---|---|
| Outcome naming — `Victory` = wave wiped, `Defeat` = squad wiped | `BattleEngine.Resolve`'s outcome ternary | Vocabulary only. Read `Outcome`, do not read "who is the player" |
| The `greedy` loot tally counts squad-side survivors only | `BattleEngine.Resolve`'s `greedySurvivors` count | Not a combat input; unused here |
| Event naming, `plant.*` vs `zombie.*` | `BattleReportEmitter`'s spawn/die envelopes | The emitter, not the resolver; unused here |
| `SideOf` is 0/1 | `BattleRunState.SideOf` | Symmetric by construction |

**So squad-vs-squad is expressible today by placing the opposing six on the `wave` side with
`Side = "wave"`.** It is a labelling convention, not a mechanic, and the harness must say so at the
top of its own output rather than implying the engine grew a PvP mode.

**Squad-vs-authored-wave is measurable but answers a different question, and cannot run at the Θ this
program measures.** `WaveCatalog` ships four waves — `rift-skirmish`, `rift-warband`,
`rift-onslaught`, `rift-tyrant` — at content indices 1, 3, 6 and 10, and every enemy's `Level` is that
index (`WaveCatalog.cs:116-119`, `:135-145`). A Θ=100 squad against a Θ=10 wave is a stomp and
measures nothing. Parameterising the wave roster by Θ is `src/` work this module may not do (§13).

**Decision: squad-vs-squad is the primary mode.** It is the direct analogue of the dominance matrix,
it needs no `DemonSpeciesCatalog` and no wave content, and it is what "does the ordering transfer"
actually asks. Squad-vs-wave ships as a mode behind `--opponent wave` and is reported separately,
never mixed into the transfer table.

### 3. Elements stay neutralised, and the harness must report that

The 1v1 baseline neutralises elements deliberately and says so in its own coverage block:
`StrikeMixture` is omni-only (`StrikeMixture.cs:22-24`) and `DominanceGuard.StandardCoverage()` reports
`ElementAxis: "NEUTRALISED"` (`DominanceGuard.cs:82`). For the transfer comparison to mean anything,
the squad runs must match: leave `ElementPrimary`/`ElementSecondary` null, which resolves to
`ActorElementTypes.Neutral` (the `ActorElementTypes.Create` call in `BattleEngine`'s actor constructor),
and leave `HybridSecondaryWeightMilli` at its shipped `0` (the `HybridPayload.Build` call beside it,
`battle.v4.json` `hybrid.secondaryWeightMilli`).

The harness emits its own `coverage` block, shaped like `DominanceGuard.StandardCoverage()`, naming
the axes it did not exercise. A measurement without its coverage is a claim.

### 4. The tree model, and what it may not do

Tree power enters exactly as `tools/HybridViability --trees` already models it (`Program.cs:216-220`),
lifted into a shared type rather than re-derived:

```text
p_i  = share_i · (Θ · aptitudePointsPerTheta)              points in tree i
T_i  = min(10, max{ t : req(t) ≤ p_i }),  req(t) = 5·t(t+1)/2      D26 ladder, D29 cap
W_i  = b · T_i(T_i+1)/2                                    linear per tier (D26 pairing rule)
H    = w · H_points + (1 − w) · H_souls                    D8, blended per §3.2
F    = 1 + (Fmax − 1) · H                                  D4
p_i' = p_i + F · W_i                                       effective points, folded back
```

`F` and `H` are computed **per actor** (D21), never per squad. The whole finding under test is that a
per-actor `H` and a per-squad delivery of power are different scopes; computing one squad-wide `H`
would assume the answer.

Two model corrections doc 16 and doc 12 already established, both required before a squad sweep means
anything above Θ ≈ 300:

- **D25's rising unlock cost.** Every existing sweep sets `W = b·T(T+1)/2`, assuming you own every
  node up to your tier; D25 makes you own `O(√Θ)` of them
  ([`passive-tree-ideal.md:588`](../passive-tree-ideal.md)). Until the model prices ownership, D28
  reads *measured, pending a D25 re-run*, not closed.
- **The soul track.** `tools/HybridViability` models tier-derived power only, so above Θ ≈ 300 — where
  the point track saturates for every build — it *"silently reports a saturated late game because it
  cannot see the half that still grows"*
  ([16-depth-exhaustion.md](../../research/passive-tree/16-depth-exhaustion.md)). `w` is the parameter
  the whole late game hangs on, and it is unmeasurable until the model has both tracks.

Both land in stage S2/S3 (§11), not S1.

### 5. The transfer artifact — the whole point of the module

Results only "transfer" if you can see both scopes side by side, and a naive comparison confounds two
changes at once: the scope changed (1 → 6) **and** the engine changed (closed-form `Predictor` →
trial-based `BattleEngine`). So the harness runs **three columns**, not two:

| Column | Scope | Engine | Entry point |
|---|---|---|---|
| `duelClosedForm` | 1v1 | `Predictor.Predict`, no round limit | `DominanceGuard.Measure` (`DominanceGuard.cs:38`) |
| `duelTrials` | 1v1 | `BattleEngine.Resolve`, seeded trials | `BattleEngine.Resolve` |
| `squadTrials` | 6v6 | `BattleEngine.Resolve`, seeded trials | same |

`duelClosedForm` is **recomputed in process**, not read from
`docs/research/class-system/_hybrid-viability.json` — the same rule `tools/DominanceBaseline` follows
for the same reason (one tuning load, one Θ, so a drift in the checked-in artifact shows up as a diff
rather than propagating silently). The harness compares its recomputation against the checked-in file
and warns on mismatch; it never rewrites it.

**Artifact:** `docs/research/passive-tree/_scope-transfer.json`, one row per build class
(`corner`, `hybrid2`, `hybrid3`, `spread`) and three win-share columns, plus:

- `orderingByColumn` — the build-class ordering each column produces, e.g.
  `["spread","hybrid3","hybrid2","corner"]`
- `transfers` — `true` only when all three orderings are identical **and** every pairwise gap that
  decides the ordering exceeds its own half-width (§9.2). Anything else is `false` with a
  `whyNot` string.
- `resolutionMilli` — the 95% half-width, per-mille, of every reported cell.
- `coverage` — §3's block.

The full 506-cell matrix goes to `docs/research/passive-tree/_squad-scope.json`; the transfer file is
the one a human reads.

### 6. What it settles — named tunable keys, with units

Per ideal §14 (`passive-tree-ideal.md:695-708`); every one lands in
`data/tuning/passive-tree.v1.json`, and **this module proposes values, never writes them** (§13).

**Every key carries its unit in its name and lives in one file** — the naming rule is not cosmetic:
writing `1.2` into a per-mille key yields `F = 1.0012` and passes every test this spec writes.
`concentration.fmax`, `concentration.w` and `soulThetaWeight` are the superseded spellings; the
harness names the keys below and nothing else.

| Key | Unit | Today | What the harness produces |
|---|---|---|---|
| `concentration.fmaxMilli` | multiplier in per-mille (≥ 1000) | 1200, retained provisionally (D5) | The squad-scope value, if any, at which the corner class stops being strictly worse than the spread class — and an explicit "no value in the swept range does this" if that is the answer, which is what §3.5 found at 1v1. `1000` (no multiplier at all) is a legal value the sweep must include, because D5 is provisional |
| `concentration.wMilli` | 0..1 blend in per-mille | 500, *"until swept"* | The value at which the ordering holds **above** the point-track crossover. Doc 16 promotes this from a tuning nicety to the load-bearing late-game parameter; **at `wMilli = 1000` there is no late game at all** |
| `soulTrack.thetaPerSoulLevelMilli` (`Ws`) | Θ per soul level, per-mille | unmeasured | A bounded range, once S3 teaches the model the soul track. Owes an `ssot-power-scale.md` §10.2 row before use |
| **D28's largest-mate rule** | — (a rule, not a value) | adopted, *"measured, pending a D25 re-run"* | Evidence, not a decision. The harness re-runs `none` / `largest` / `quarter` / `full` at squad scope across Θ, and reports whether `largest` still reverses the ordering and where the reversal expires. Keeping or replacing the rule is the owner's |

**Not settled here, deliberately.** `b` is not a balance dial — §3.5 swept it across `{0,2,5,10,20}`
and no value works, so it is a content-density choice (§14's own last-but-one row). `tierLadder.k = 5`
is derived by D26, not measured. `unlockCost.first/step` are derived by D36.

### 7. Determinism, and the numeric rules

**The engine is already deterministic.** *"No I/O, no clock, no ambient state: same setup + seed +
platform ⇒ byte-identical report"* (`BattleEngine`'s own type doc). The harness's job is not to add
determinism; it is to not lose it.

**Seeding.** Every trial's seed is a pure function of its coordinates and nothing else:

```text
seed(a, d, k) = mix64(runSeed, attackerIndex a, defenderIndex d, trialIndex k)
```

No `Random()` without a seed, no `DateTimeOffset.Now` anywhere in the measurement path (only in the
artifact's `at` provenance field, which is excluded from the determinism hash), no `Guid.NewGuid`, no
`Environment.TickCount`, no unordered dictionary enumeration feeding an ordered output. Parallelism is
permitted **only** where each trial's seed is already independent of execution order and results are
re-sorted by `(a, d, k)` before aggregation.

**Common random numbers.** Trial `k` uses the same `seed(a, d, k)` in every column, so the
`duelTrials` and `squadTrials` estimates of the *same* pair share their randomness. This is the
variance reduction that makes §9.2's resolution affordable; without it the difference estimator's
standard error is √2 times each arm's.

**Numeric types** (CLAUDE.md "Numeric overflow", `ssot-power-scale.md` §9.4):

| Quantity | Type | Why |
|---|---|---|
| Outcome counts (`victories`, `defeats`, `stalemates`) | `long`, `checked` | A magnitude. 506 pairs × 40,000 trials is inside `int` today and would not be after one `--trials` change; that is exactly the "small only at the calibration point" defect |
| Win share | `long` per-mille | A **bounded ratio** (0..1000) and exempt by that clause — but computed `checked((long)victories * 1000 / decided)`: widen before multiplying, divide by 1000 exactly once, at the end |
| HP, damage, defense carried into a setup | `long` | Already `long` on the shipped record (`BattleActorSetup.MaxHp`/`.Atk`/`.Defense`), and `BattleChannelMod.Amount` too. Never narrowed |
| Θ | `long` in, `int` at the engine boundary | `BattleActorSetup.Level` is `int`; the harness rejects a Θ above `int.MaxValue` loudly at parse rather than casting |
| `F`, `H`, `share` | `double` | Bounded ratios in a **reported, non-persisted, non-hashed** model value. The determinism hash covers per-mille integers only (§9.1), so no `double` reaches a hashed path |

**No `float` anywhere.** No `unchecked` on any counting path. Overflow throws.

**No caps introduced.** Two numbers look like ceilings and are not: squad size **6 is structural** — it
mirrors the shipped game rule in `WebMatchService.BuildSquad`, so changing it would change what is being
measured, not how the game feels; and `--trials` is a **per-run measurement budget**, §11.3's exempt
class. Both carry that sentence as a comment. `BattleRuleset.MaxRounds` (50, `battle.v4.json`
`ruleset.maxRounds`) is the engine's own horizon, not this module's — see §8.

### 8. Stalemate is an outcome, never a loss

`decisions.md:103` is explicit and it is a hard lock: *"Win rate is the metric — never fight length,
damage dealt or kill time… and never under a clock, which manufactures a pass by penalising long
fights."* `DominanceGuard` honours it by always using the no-`roundLimit` overload
(`DominanceGuard.cs:24-27`, `:54`).

`BattleEngine` **has** a horizon — `maxBattleTick = MaxRounds × RoundDurationMs`
(`BattleEngine.Resolve`'s `maxBattleTick`) — and returns `BattleOutcome.Stalemate` when it is reached.
A trial-based harness therefore cannot inherit the no-clock property, and
pretending otherwise is how the banned defect gets back in.

**The rule: `winShareMilli = victories × 1000 / (victories + defeats)`. Stalemates are excluded from
the denominator and reported as their own count.** A cell whose stalemate rate exceeds 20% is flagged
`lowConfidence` and its ordering contribution is refused, because at that point the horizon, not the
build, is deciding. This is the same judgement `DominanceGuard`'s type doc makes, applied to an engine
that cannot avoid having a clock.

### 9. Resolution — the harness must know what it cannot see

#### 9.1 The determinism hash

SHA-256 over the artifact's canonical form with the provenance fields blanked — the exact idiom
`BattleGoldenTests.Hash` uses (`BattleGoldenTests.cs:144-149`), which blanks `EnvironmentStamp` and
`ContentHash` so a portable hash stays portable across CI and machines. The harness's hash input is the
per-mille integer grid plus the roster ids, and excludes `at`, `environmentStamp`, `wallClockMs` and
every `double`.

#### 9.2 The noise floor is measured, not assumed

`tools/CombatSim/Marginal.cs:21-23` records it: *"3,000 duels resolve to about 0.9pp"* — and that is
what `sqrt(0.25/3000) = 0.91pp` gives, checked. So:

| Half-width wanted (95%) | Trials per cell |
|---|---|
| 1.8pp | 3,000 |
| 1.0pp | ~9,600 |
| 0.5pp | ~38,400 |

The gaps this program cares about are **not one size**. §3.3's concentration penalty is 7–14pp and is
visible at 3,000. Doc 16's crossover at Θ ≈ 300 is **0.5pp or less** (47.6% vs 47.6%) — *below the
noise floor of a 3,000-trial run*. A harness that reported a crossover it could not resolve would be
worse than no harness.

> ⛔ **Doc 16's crossover is a CLOSED-FORM result, and a null result here does not refute it.** Those
> numbers come from `DominanceGuard.Measure` → `Predictor.Predict`, which is deterministic: no trials,
> therefore no sampling noise, so `Θ ≈ 300` is exact *for the model*
> ([16-depth-exhaustion.md §Resolution](../../research/passive-tree/16-depth-exhaustion.md)). This
> harness is trial-based, and the gap the table turns on is **0.8pp at Θ = 400** against a **0.9pp**
> floor at 3,000 trials. So the honest outcome of a screening run at that Θ is *"cannot separate"*, and
> the artifact must say that in those words. **`transfers: false` for want of resolution is not
> evidence that the model is wrong** — reading it that way would let a harness that measured nothing
> overturn a result that was computed exactly. The refine pass at 40,000 trials brings the half-width
> to ≈ 0.5pp, which is the only depth at which this harness has anything to say about doc 16 at all.

**Two-stage design.** A screening pass at `--trials 3000` over all 506 pairs; then `--refine`
re-measures only the cells whose gap falls inside their own half-width, at `--trials 40000`. Common
random numbers (§7) apply to the refinement too. Every reported cell carries its half-width, and
**`transfers` is `false` whenever an ordering rests on a gap inside it.**

### 10. Which mechanism nodes are measurable today

§3.5's conclusion is that a focus build *"cannot be rescued with MAGNITUDE… only with MECHANISM"*, so a
squad sweep that can only see magnitude reproduces the 1v1 result by construction. Verified this
session against code:

**Measurable at squad scope today**

| Node class | Why | `file:line` |
|---|---|---|
| Anything expressed as a change to the shipped resolver functions | `BattleEngine` runs the SSOT resolver, and the closed form calls the same functions rather than re-implementing them, so both columns move together | `BattleEngine`'s type doc (*"attack resolution runs the SSOT resolver"*); `StrikeMixture.cs:11-20` |
| On-hit riders | Battle raises `OnDamageDealt` — `partial class BattleEngine` in `Actions/`, which is why a `Battle/`-folder grep misses it | `BasicAttack.cs:184` (comment at `:176`) |
| Shields, including innate | `DamageApplyPipeline` runs the shield gate in Battle exactly as overlay does | the `DamageApplyPipeline.Apply` call in `BattleRunState`; `BattleActorSetup.InnateShield` |
| Statuses, DoTs and CC | `BattleStatusSpec` is carried per actor and pulses on the kernel queue | `BattleStatusSpec`; `BattleEngine.Resolve`'s status pulse |
| Traits | `TraitBattleCatalog`, configured from `battle.v{n}.json` | `BattleTuningHub.Configure` |
| Commander auras | `ActiveCommanderAura`, delivered per side | `BattleSetup.ActiveAuras` |

**Blocked on `mechanism-wiring`, and named by their inert line**

| Node class | The inert line |
|---|---|
| Anything triggered by `OnDamageTaken`, `OnSpawn` or `OnDeath` | **Zero hits** for any of the three across `src/FusionRpg.Core/Battle/` and `src/FusionRpg.Core/Actions/` — grepped this session. `OnDamageDealt` is the only trigger Battle raises |
| `stat.derived` re-evaluating per hit (M1 conditional scaling) | `AtomKindRegistry.cs:535` — `AtomTriggers.None` |
| `stat.derived` scored in Sim | `AtomKindRegistry.cs:534` — the SIM slot of the `RuntimeSupportMatrix` is `RuntimeState.None` |
| A status's derived-channel write composing (§4a layer parity) | `ActorHubBootstrap.CreateDefault` registers three subsystems (`ActorHub.cs:145,148,155`) and the third is conditional on `boundDerivedAtoms` |
| Battle's derived recompose beyond construction | `BattleRunState.RecomposeDerived` — one caller, the construction-time `foreach (var aura in setup.ActiveAuras)` loop |
| **M7 Retaliation / reflect** | **Correcting the ideal here.** §13.1 lists reflect as *"production caller wired — `EffectRuntime.cs:491`"*, but that file is `src/FusionRpg.Injector/Effects/EffectRuntime.cs` — the **lawn**. Reflect lives in `CombatDamageDispatcher.TryReflect` (`CombatDamageDispatcher.cs:85`), reached only from `DispatchInstant`; Battle applies HP through `DamageApplyPipeline.Apply` instead (the call in `BattleRunState`), and `reflect` has **zero hits** in `src/FusionRpg.Core/Battle/`. **Reflect is not measurable at squad scope today.** Doc 05 ranks it #2 and *"ship content today"* — true on the lawn, not in Battle |

Both lists go in the harness's `coverage` block, so a null result is never mistaken for a measurement.

#### 10.1 A10 — the Erosion differential, and why this harness can run it in wave 0

`mechanism-wiring`'s **A10** is the acceptance test for that whole module, and the map gates
`tree-language --write` on it (`passive-tree-map.md:42-47`) because the step after the plan costs
~4,680 model calls for the generic corpus and ~105,840 for species. **The measurement is this
module's**, so its shape belongs here. `mechanism-wiring` §11.1 owns the bars and the verdict table; this
section owns producing the numbers and refusing to round an unresolved run into a pass.

**The quantity — a difference of differences**, four arms at one Θ, one roster and one seed stream:

```text
ΔW_spread = W(corner attacker WITH erosion  vs spread defender)
          − W(corner attacker WITHOUT erosion vs spread defender)
ΔW_corner = W(corner attacker WITH erosion  vs corner defender)
          − W(corner attacker WITHOUT erosion vs corner defender)
D         = ΔW_spread − ΔW_corner
```

Both defenders already exist in the duel roster (§1.2) — the twelve corners and `even12` — so no new
build shape is minted, and the attacker is the same corner in all four arms.

**The bars, restated so the run can fail:** direction `D > 0`; effect size, the 95% lower bound on `D`
above **3.0pp**; resolution, `D`'s own 95% half-width at or below **1.0pp**; plus the selectivity ratio
`ΔW_spread ≥ 2 × ΔW_corner`. An interval straddling the bar reports **`UNRESOLVED`**, which is not a
pass — §9.2's refusal rule, applied to the one criterion the program's build order depends on.

**The trial count is arithmetic, not a guess.** `D` is a linear combination of four win-share
estimates, so absent common random numbers its 95% half-width is `1.96 · 2 · sqrt(0.25/n) = 1.96/√n` —
**1.0pp at n ≈ 38,400**, which is §9.2's existing `--refine 40000` tier and needs no new budget. §7's
common random numbers pair the with/without arms on the identical `seed(a, d, k)`, so the paired
difference is tighter than that bound; 40,000 is the conservative figure and **the achieved half-width
is reported, never assumed.**

**Erosion is expressible against the shipped engine today — this is what puts A10 in wave 0.**
`BattleActorSetup.ChannelMods` is the caller's own additive derived-channel overlay,
`BattleChannelMod(string ChannelId, long Amount)`, and `BattleStatComposer.Compose` folds it into the
composed snapshot, validating every id against the full registered channel set and **throwing** on an
unknown one — so a typo is a crash, not a silent zero. The resolver then reads all eight of the
taxonomy's defensive families off the defender's snapshot: absorption
(`OverlayCombatCalculator.cs:109`), reduction (`:116`, `:160`), dodge (`:118`, `:163`) and parry/block
rate (`:183-184`). A **static** Erosion — a fixed flat subtraction across the defensive vector, floored
at each channel's registered default — therefore runs through the shipped resolver with **no new
wiring and no harness math**. That is doc 05 §6.4 step 1, and it is `mechanism-wiring`'s **A10a**.

**What the static form does not prove, said plainly.** It settles §4c's *causal* claim — that a flat
per-layer subtraction costs breadth more than focus — which is the claim `tree-plan`'s deep-tier budget
rests on. It does not prove the shipped **delivery vehicle** reproduces it. Erosion as designed is a
stacking status applied on `OnDamageDealt`, and in Battle `BattleStatusSpec` carries **no `StatMods` at
all** (it is `StatusId`, `MagnitudePerPulse`, `DurationMs`, `PeriodMs`, `GrantChanceMilli`), while
`BattleDerivedModifierLedger.Add` has exactly one caller in `src/` — the construction-time
`foreach (var aura in setup.ActiveAuras)` loop in `BattleRunState`. That half is `mechanism-wiring`'s
**A10b** and is not this module's to build. The `coverage` block names the split, so an A10a pass is
never reported as an A10b pass.

**Column.** `duelTrials`, restated on `squadTrials` for the six-actor scope. **Never
`duelClosedForm`** — `DominanceGuard.Measure` takes `IReadOnlyList<AptitudeAllocation>` and nothing
else, so a channel-level mechanism has no way into the closed form through it. The transfer table's
`whyNot` says exactly that rather than leaving the cell blank.

### 11. Staging — reusing doc 05 §6.4, not re-deriving it

[05-mechanism-taxonomy.md §6.4](../../research/passive-tree/05-mechanism-taxonomy.md) already scoped
this in four steps with effort estimates. Mapped onto this module:

| Doc 05 step | Effort there | Here |
|---|---|---|
| **1.** Implement mechanism nodes as changes to shipped resolver functions | zero — a design constraint | **Not this module's.** It is a constraint on `tree-binder`; the harness inherits the benefit (§10 row 1) |
| **2.** Extend the closed form with two phases; change `TerminationGuard.ToActor:123`'s `BaseDamage: 0.0, ShieldMaxHp: 0` | ~1 session | **Not this module's — it edits `src/`** (§13 Never). The harness consumes it if it lands; nothing here waits on it |
| **3.** A trial-based sibling to `DominanceGuard` resolving each arrow over `BattleEngine` | ~1–2 sessions | **This is S1.** Ships first |
| **4.** Make Battle fire `OnDamageTaken` / `OnDamageDealt` | its own piece of work | `mechanism-wiring`'s, and **already half done**: `OnDamageDealt` fires (`BasicAttack.cs:184`). What remains is `OnDamageTaken` / `OnSpawn` / `OnDeath` |

**One correction to step 3, and it removes its only flagged blocker.** Doc 05 says *"`ToActor` being
`internal` (`TerminationGuard.cs:111`) needs one visibility decision."* It does not. The trial path
never builds a `Predictor.Actor`: it builds a `BattleActorSetup` from
`AptitudeResolver.ResolveForBattle(...)` — public (`AptitudeResolver.cs:79`) — plus
`BattleRuleset.BaseHp/BaseAtk/BaseDefense(level)` — public (`BattleModels.cs:218-221`, re-read this
session; this spec used to cite `:172-175`, which `battle-tempo`'s edits had already moved, and that
stale line was the only thing standing between doc 05's flagged blocker and its retraction). **No
visibility change, no `InternalsVisibleTo`, no `src/` edit.**

**Ships in S1** (the whole of §5's three columns, at Θ = 100):

- The duel roster, the squad roster, the seeded trial loop, the two artifacts, the determinism hash.
- Modes `duel`, `squad`, `transfer`, `verify`.
- The answer to D33 as it stands at the calibration point.

**S2 — the concentration model at squad scope.** Modes `concentration` and `crossunlock`: the §4
model, D25's ownership cost folded in, `fmax` × `w` × Θ sweep, D28's four credit rules. This is what
produces §6's `concentration.fmaxMilli` and D28 evidence.

**S3 — the soul track and the Θ range.** Teaches the model D3's second ladder so `concentration.wMilli`
becomes measurable, and sweeps Θ ∈ {100, 150, 200, 300, 400, 600} — the range doc 16 showed the
existing sweeps never covered. Produces `soulTrack.thetaPerSoulLevelMilli` and the `w` verdict.

**S4 — the D15 budget measurement. Claimed, not optional.** Doc 11 §6b measures that
`PowerVector.Total` is not value: at identical Θ and identical budgets the twelve corners span
0.3%–97.9% mean win share. **No other module in `passive-tree-map.md` is scoped to produce that
evidence** — `tree-plan` owns the budget *rule*, and its headline *"no tree is OP"* rests on a scalar
the program has already measured is not value. Nobody owning the proof of a load-bearing claim is worse
than the claim being wrong, so **this module owns it**: a `budget` mode, using the finite-difference
machinery `tools/CombatSim/Marginal.cs` already has, reporting marginal value per budget point across
the duel roster with the same half-widths every other cell here carries.

The measurement is not a question and is no longer staged as one. **Whether D15's rule should change on
the strength of it is the owner's**, and that is what §14 open question 3 now asks.

---

## Commands

```powershell
# S1 --- the three columns and the transfer verdict. This is the module's headline command.
dotnet run --project tools/SquadHarness -- transfer --theta 100 --trials 3000 --seed 20260905

# S1 --- the bridge run alone: same 91 builds as tools/HybridViability, resolved by trials
dotnet run --project tools/SquadHarness -- duel --theta 100 --trials 3000 --seed 20260905

# S1 --- the six-vs-six run alone, full 506-cell matrix
dotnet run --project tools/SquadHarness -- squad --theta 100 --trials 3000 --seed 20260905 `
    --out docs/research/passive-tree/_squad-scope.json

# S1 --- refine only the cells whose ordering rests on a gap inside its own half-width
dotnet run --project tools/SquadHarness -- transfer --theta 100 --trials 3000 --refine 40000 --seed 20260905

# S1 --- squad against authored wave content, reported separately, never in the transfer table
dotnet run --project tools/SquadHarness -- squad --opponent wave --wave rift-tyrant `
    --theta 10 --trials 3000 --seed 20260905

# S1 --- determinism self-check: every mode, run twice in one process, hashes compared
dotnet run --project tools/SquadHarness -- verify --seed 20260905

# S1 --- mechanism-wiring A10a: the Erosion differential. Four arms, refined to a 1.0pp half-width on D.
# This is the run the map's `tree-language --write` gate reads (section 10.1).
dotnet run --project tools/SquadHarness -- erosion --theta 100 --trials 3000 --refine 40000 `
    --seed 20260905 --out docs/research/passive-tree/_erosion-differential.json

# S2 --- Fmax x w at squad scope (D5, D8). Per-mille in, per-mille out: 1000 means "no multiplier".
dotnet run --project tools/SquadHarness -- concentration --fmax-milli 1000,1150,1200,1250 `
    --w-milli 0,500,1000 --b 5 --theta 100 --trials 3000 --seed 20260905

# S2 --- D28's four credit rules at squad scope
dotnet run --project tools/SquadHarness -- crossunlock --rule none,largest,quarter,full `
    --theta 100 --trials 3000 --seed 20260905

# S3 --- the range doc 16 says every previous sweep missed
dotnet run --project tools/SquadHarness -- transfer --theta 100,150,200,300,400,600 `
    --trials 3000 --refine 40000 --seed 20260905

# S4 --- D15: marginal win share per budget point, across the duel roster (doc 11 section 6b)
dotnet run --project tools/SquadHarness -- budget --theta 100 --trials 3000 --seed 20260905

# Tests and audits
dotnet test tests/FusionRpg.SquadHarness.Tests
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --targets M1
.\scripts\guard-power.ps1
```

Every mode takes `--theta` (a comma list is a sweep), `--trials`, `--seed` and `--out`. `--seed` is
**required** — there is no default, for the same reason `tunables-ssot.md` T5 refuses a default tuning
value: a seed nobody chose behaves like one somebody did, and the run stops being reproducible from
its own command line.

## Project structure

```text
tools/SquadHarness/SquadHarness.csproj          Exe, net8.0, ProjectReference -> src/FusionRpg.Core only
tools/SquadHarness/Program.cs                   thin CLI: parse args, pick the mode, print, write
tools/SquadHarness/TuningBootstrap.cs           the live-highest-version Configure block (+ BattleTuningHub)
tools/SquadHarness/BuildFactory.cs              Build(params string[] spikeIds) - the shared corner shape
tools/SquadHarness/SquadRoster.cs               Duels() -> 91 builds; Squads() -> 23 squad builds
tools/SquadHarness/TreeModel.cs                 req(t), W(T), H, F, D25 ownership cost (S2)
tools/SquadHarness/SquadMatch.cs                one ordered pair -> long counts, seeded, checked
tools/SquadHarness/Modes.cs                     the mode table (see Code style)
tools/SquadHarness/TransferReport.cs            the three columns, orderings, half-widths, hash
tools/SquadHarness/Erosion.cs                   the four A10 arms, D, its half-width, the verdict
docs/research/passive-tree/_scope-transfer.json the artifact a human reads
docs/research/passive-tree/_squad-scope.json    the full 506-cell matrix
docs/research/passive-tree/_erosion-differential.json  A10's four arms, D, and PASS/FAIL/UNRESOLVED
tests/FusionRpg.SquadHarness.Tests/FusionRpg.SquadHarness.Tests.csproj
tests/FusionRpg.SquadHarness.Tests/DeterminismTests.cs
tests/FusionRpg.SquadHarness.Tests/RosterTests.cs
tests/FusionRpg.SquadHarness.Tests/AggregationTests.cs
```

The tool is **not** a single top-level `Program.cs` the way `tools/HybridViability` and
`tools/DominanceBaseline` are. Those are untestable by construction — there is no type to reference —
and determinism is this module's hard requirement, so the measurement types have to be reachable from
a test project. `Program.cs` stays a thin CLI over them, and the test project takes a
`ProjectReference` on the tool exactly as `tests/FusionRpg.ItemSeedValidator.Tests` does.

`TuningBootstrap.cs` reads the **highest** `data/tuning/<domain>.v{n}.json` per domain, never a
hand-picked version literal — the rule `HybridViability/Program.cs:45-57` and
`DominanceBaseline/Program.cs` both already follow, and it configures one hub the duel tools do not:
`BattleTuningHub.Configure`, without which `BattleRuleset.Tuning` throws by design (its own
`?? throw new InvalidOperationException`).

## Code style

```csharp
/// <summary>
/// One measurement mode. Adding a mode is adding a row to <see cref="All"/> and nothing else: the
/// CLI, the artifact writer and the determinism test all enumerate this table, so a mode that is
/// not here cannot be run, and a mode that IS here is covered by
/// <c>DeterminismTests.Every_mode_repeats_byte_identically</c> without a second edit.
///
/// <para>The <c>args.Contains("--trees")</c> idiom tools/HybridViability uses
/// (<c>Program.cs:226</c>, <c>:300</c>) is correct for two modes and does not survive seven:
/// nothing enumerates the flags, so nothing can assert over all of them, and the determinism test
/// this module exists to pass would silently cover only the modes someone remembered to list.</para>
/// </summary>
public sealed record MeasurementMode(
    string Name,
    string Question,                                   // the one sentence this mode answers, printed above its table
    Func<RunSpec, MeasurementResult> Run,
    string DefaultArtifactPath);                       // repo-root-relative; --out overrides

public static class MeasurementModes
{
    public static readonly IReadOnlyList<MeasurementMode> All = new[]
    {
        new MeasurementMode(
            "squad",
            "Does the 1v1 build-class ordering survive at the six-actor scope the game is played at? (D33)",
            spec => Sweep.Run(spec, SquadRoster.Squads(spec.Theta)),
            "docs/research/passive-tree/_squad-scope.json"),
        // ... duel, transfer, erosion, concentration, crossunlock, verify, budget
    };
}

/// <summary>
/// One ordered squad pair, resolved over the shipped engine. Nothing here re-implements combat: the
/// numbers come back out of <see cref="BattleEngine.Resolve"/>, which is already byte-identical for
/// a given (setup, seed, platform) -- BattleEngine's own type doc.
///
/// <para><b>Stalemates leave the denominator</b> (decisions.md:103 -- "win rate is the metric...
/// never under a clock"). BattleEngine HAS a horizon (Resolve's maxBattleTick), so a
/// trial harness cannot inherit the closed form's no-clock property; excluding stalemates and
/// reporting them separately is the closest honest equivalent. A cell over StalemateFlagMilli is
/// refused, not scored, because past that the horizon is deciding, not the build.</para>
///
/// <para><b>long, checked, divide last.</b> Counts are magnitudes and must not be int: 506 pairs at
/// 40,000 trials is inside int today and would not be after one --trials change, which is exactly
/// the "small only at the calibration point" defect CLAUDE.md names. The share is a BOUNDED RATIO
/// (0..1000) and so exempt from the long-magnitude rule, but it is still widened before the
/// multiply and divided by 1000 exactly once, at the end.</para>
/// </summary>
public static PairOutcome Measure(SquadBuild attacker, SquadBuild defender, RunSpec spec)
{
    long victories = 0, defeats = 0, stalemates = 0;

    for (var k = 0; k < spec.Trials; k++)
    {
        // Common random numbers: the seed is a pure function of the pair and the trial index, so
        // the same k is the same randomness in every column of the transfer table (spec 7).
        var seed = Seeds.Mix(spec.RunSeed, attacker.Index, defender.Index, k);
        var report = BattleEngine.Resolve(SetupFor(attacker, defender), seed);

        switch (report.Outcome)
        {
            case BattleOutcome.Victory:   checked { victories++; }  break;
            case BattleOutcome.Defeat:    checked { defeats++; }    break;
            default:                      checked { stalemates++; } break;
        }
    }

    var decided = checked(victories + defeats);
    var winShareMilli = decided == 0 ? 0L : checked(victories * 1000L) / decided;
    return new PairOutcome(victories, defeats, stalemates, winShareMilli);
}
```

## Testing strategy

Determinism is the hard requirement, so it is asserted three ways, not one. Tests live in
`tests/FusionRpg.SquadHarness.Tests/`.

| Test | Asserts |
|---|---|
| `Every_mode_repeats_byte_identically` | Each row of `MeasurementModes.All`, run twice in one process at a small `--trials`, produces the identical determinism hash. Enumerates the table, so a new mode is covered without editing the test |
| `A_second_process_reproduces_the_hash` | The same command run as a fresh process reproduces the hash — catches static state carried across runs, which an in-process repeat cannot see |
| `Reordering_the_roster_does_not_move_a_cell` | A pair's outcome depends on `(a, d, k)` only. Shuffling the roster changes which cells exist, never what a surviving cell says — the assertion that kills accidental order-dependence |
| `Parallel_and_serial_agree` | The parallel and serial sweep paths produce the identical hash. If they ever diverge, parallelism is removed, not debugged into place |
| `The_hash_excludes_provenance` | Changing `at` / `environmentStamp` / `wallClockMs` does not move the hash. `BattleGoldenTests.Goldens_do_not_depend_on_the_platform` is the precedent (`BattleGoldenTests.cs:163-169`) |
| `No_double_reaches_the_hash_input` | Reflection over the hashed record: every field is `string`, `long`, or a list of those |
| `A_missing_seed_is_a_refusal_naming_it` | `--seed` has no default (T5's shape, applied to a seed) |
| `Counts_are_long_and_overflow_throws` | A synthetic count near `long.MaxValue` throws rather than wrapping |
| `Win_share_divides_by_a_thousand_exactly_once` | Against a hand-computed per-mille table; catches an early divide |
| `Stalemates_leave_the_denominator` | A 40/30/30 outcome split scores 571‰, not 400‰ |
| `A_high_stalemate_cell_is_refused_not_scored` | Over the flag threshold the cell reports `lowConfidence` and contributes to no ordering |
| `The_duel_roster_is_the_ninety_one_HybridViability_builds` | 12 + 66 + 12 + 1, and every allocation equal to the one `HybridViability`'s own corner shape produces — computed, never a literal `91` |
| `Elements_are_neutral_in_every_generated_setup` | §3: `ElementPrimary`/`Secondary` null on every actor in both rosters, so the bridge column really is comparable |
| `Every_squad_has_exactly_six_actors` | Ties the roster to `WebMatchService.BuildSquad`'s own `maxSquad` bound |
| `Transfers_is_false_when_an_ordering_rests_inside_its_half_width` | §9.2's refusal, on a synthetic grid |
| `The_recomputed_closed_form_matches_the_checked_in_artifact` | Warns, never fails the run, on drift against `_hybrid-viability.json` |
| `The_erosion_differential_is_a_difference_of_differences` | `D` on a synthetic four-arm grid, against a hand-computed value. Catches the easy error of reporting `ΔW_spread` alone, which is not what A10 asks |
| `An_erosion_interval_straddling_the_bar_is_UNRESOLVED_not_PASS` | The refusal that makes A10 able to fail. A synthetic `D` whose confidence interval contains 3.0pp must not report `PASS` |
| `Erosion_writes_only_registered_channels` | Every channel the static Erosion touches survives `BattleStatComposer.Compose`'s own validation — a typo throws rather than contributing a silent zero |
| `The_erosion_arms_share_their_seeds` | The with/without arms of one cell use the identical `seed(a, d, k)`; without that pairing the half-width claim in §10.1 is wrong by √2 |

**Golden-artifact policy.** The artifacts under `docs/research/passive-tree/` are **measurements, not
goldens**: they change whenever tuning changes, and pinning them would make a rebalance
indistinguishable from a determinism break — the defect `BattleReport.ContentHash`'s own doc comment
describes (`BattleReport.ContentHash`'s doc comment). The determinism hash is asserted **within a run**, never
against a checked-in value.

## Boundaries

**Always:** measure and report; construct actor builds through the one shared corner-shape helper;
read the highest `data/tuning/<domain>.v{n}.json`; configure every hub the engine needs, including
`BattleTuningHub`; keep elements neutralised and say so in `coverage`; seed every trial from
`(runSeed, a, d, k)`; use `long` and `checked` on every count; divide by 1000 once, last; exclude
stalemates from the denominator and report them; publish a half-width with every cell and refuse an
ordering that rests inside it; name every axis the run could not exercise.

**Ask first:** whether the primary verdict is scored against mirror squads or authored waves (§14.1);
whether the shipped commander-replicated allocation shape should be measured alongside D21's per-actor
shape (§14.2); whether D15's *rule* changes once S4's budget evidence lands (§14.3); adding a mode that
changes what "a squad" means.

**Never:**

- **Ship a balance change.** This module's output is numbers and a verdict.
- **Write a `data/tuning` value.** It proposes `concentration.fmaxMilli`, `concentration.wMilli` and
  `soulTrack.thetaPerSoulLevelMilli`; `tools/tuning/publish.py` writes them, on the owner's call
  (`tunables-ssot.md` T4).
- **Edit the shipped resolver.** No change to `BattleEngine`, `OverlayCombatCalculator`, `Predictor`,
  `StrikeMixture`, `DominanceGuard` or `TerminationGuard` — including doc 05 §6.4 step 2's
  `TerminationGuard.ToActor:123` change, which is real work but belongs to whoever owns the closed
  form. A harness that edits the thing it measures cannot report on it.
- **Re-implement a combat formula.** Every number comes back out of `src/`
  (`spec-deterministic-core.md` §7's own boundary, which `StrikeMixture.cs:11-20` cites).
- **Touch `src/`, `tests/` outside its own test project, `data/`, or another module's spec.**
- Write SQL, or reach `RpgStore` — the harness builds its actors in memory (§1.1) and `guard-dal.ps1`
  scans only `src/`, so `tools/` is a blind spot and discipline has to be the guard here.
- Declare a private `f(level)`. Every Θ read goes through the shipped `PowerLadder` via
  `AptitudeResolver` — `ssot-power-scale.md` §10's anti-duplication clause.
- Report fight length, damage dealt, kill time, or a round limit as a balance metric
  (`decisions.md:103`).
- Cap, clamp, or narrow a magnitude. Squad size 6 is structural and says so; `--trials` is a per-run
  budget and says so; nothing else bounds anything.
- Claim a difference smaller than its own half-width.

## Success criteria

- [ ] `transfer` prints three columns and one word, and the word is derived, not asserted.
- [ ] Every reported cell carries a half-width, and `transfers` is `false` whenever an ordering rests
      on a gap inside one.
- [ ] The same command, run twice in two processes, produces the identical determinism hash.
- [ ] Shuffling the roster moves no surviving cell.
- [ ] The parallel and serial paths agree by hash.
- [ ] The duel roster is provably the same 91 builds `tools/HybridViability` measures — asserted by
      constructing them, not by asserting the number 91.
- [ ] Every squad has exactly six actors, tied by test to `WebMatchService.BuildSquad`'s `maxSquad`.
- [ ] Stalemates never enter a denominator; a high-stalemate cell is refused, not scored.
- [ ] No `float` on any path; no `double` in the hash input; `scripts/audit-overflow.py` reports no
      critical finding.
- [ ] `scripts/audit-magic-numbers.py --targets M1` names no new balance literal in the tool.
- [ ] `scripts/guard-power.ps1` green, and the tool declares no `f(level)` of its own.
- [ ] Zero files changed under `src/`, `data/` or `tests/` outside this module's own test project.
- [ ] The `coverage` block names elements, the six blocked mechanism classes of §10, the A10a/A10b
      split of §10.1, and the
      stalemate horizon — a null result is never presented as a measurement.
- [ ] The Erosion differential `D` is reported with a direction, an effect size and its own half-width,
      and an interval straddling the 3.0pp bar reports `UNRESOLVED` — never `PASS`. This is the run the
      map's `tree-language --write` gate reads.
- [ ] A screening run that cannot separate doc 16's Θ ≈ 300 crossover says *"cannot separate"* in those
      words, and the artifact never presents that as a refutation of a closed-form result.
- [ ] D33 has an answer at Θ = 100 after S1, and across Θ ∈ {100…600} after S3.

## Open questions

Three. All are owner decisions this module may not make for itself; everything else in this spec is
answerable from code and has been answered.

1. **Is the primary verdict scored against mirror squads, or against authored waves?** Both run
   (§2). Mirror squads are the direct analogue of the dominance matrix and need no content; authored
   waves are what a player actually faces. The two can disagree, because §3.5's causal story — *"a
   corner maxes one axis and floors eleven, so every opponent finds an open one"* — depends on the
   opponent being a *build*. A wave is not. **Recommendation on the record: mirror squads decide the
   verdict, waves are reported beside it.** The wave path additionally needs a Θ-parameterised wave
   roster (`WaveCatalog.cs:116-119` pins Levels at 1/3/6/10), which is `src/` work this module cannot
   do and nothing currently schedules.
2. **Should the harness also measure the shipped allocation shape, not just D21's?** Today a squad
   differentiates per *species*, not per *actor* (§1.1), and the commander allocation replicates
   across the whole roster (`decisions.md:103`). D33's arbitrage needs per-actor build state, which
   `tree-state` will add. Measuring both says whether the exploit exists **now** or only **after**
   `tree-state` lands — which changes whether `tree-state` needs a mitigation designed in from the
   start. Cost is one extra roster function.
3. **Does D15's rule change once the budget measurement lands?** The measurement itself is settled —
   S4 is claimed by this module (§11), because doc 11 §6b's finding (`PowerVector.Total` is not value;
   the program's own artifact spans 0.3%–97.9% at identical budgets) is the evidence `tree-plan`'s
   *"no tree is OP"* rests on, and nobody else was scoped to produce it. What stays open is the
   consequence: if equal budget buys wildly unequal value, D15 either keeps an equal-budget rule and
   accepts the spread, or gains a value-based correction that `tree-plan` would have to implement.
   **That is a design decision, not a measurement**, and this module may not make it.

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §1, §2 measure at the six-actor scope; per-actor `H` against squad-scope delivery | **D33** |
| §1 a squad build is six actor builds, each with its own allocation, tree state and `H`/`F` | **D21** |
| §4 `F = 1 + (Fmax−1)·H`, `H = Σ share²`, computed per actor | **D4** |
| §6 `concentration.fmaxMilli` re-measured at squad scope, `1000` included; the D25-inclusive sweep D5 is pending | **D5** |
| §4 one `F`, applied to every tree alike — no per-tree multiplier in the model | **D6** |
| §5 hybrid neutrality re-tested; `hybrid2`/`hybrid3` are their own build classes | **D7** |
| §4, §6 `H = w·H_points + (1−w)·H_souls`; `concentration.wMilli` is the primary output | **D8** |
| §4 no `1/n` term — roster size never enters the model | **D9**, **D27** |
| §4 tier ladder `req(t) = 5·t(t+1)/2` | **D26** (superseding **D20**) |
| §4 tier cap 10, two branches, ~40 nodes per tree | **D29**, **D10** |
| §4 D25's ownership cost folded in before D28 is re-read | **D25**, **D36** |
| §4, §11 the soul track must enter the model before any Θ > 300 claim | **D3**, doc 16 |
| §6 D28's four credit rules re-run at squad scope; the largest-mate rule reports, never decides | **D28** |
| §1.1 six actors, six budgets — a demon must not read `Θ_player` | **D34** |
| §10 mechanism nodes must be scoreable, and the blocked classes are named | **D13**, ideal §3.5 |
| §3, §10 the harness reads one shared catalog shape; nothing rolls per run | **D24** |
| §6, §13 every number it settles is a tunable key with a unit; it writes none of them | ideal **§14**, map assumption 5 |
| §11 S4 `budget` mode — this module produces the budget-vs-value evidence `tree-plan`'s headline rests on; whether the rule changes is §14.3 | **D15** (doc 11 §6b) |
| §10.1 the Erosion differential `D`, with a direction, a 3.0pp effect size and a 1.0pp half-width — the measurement behind `mechanism-wiring` A10 and therefore behind the map's `tree-language --write` gate | **D13**, taxonomy §4c |
| §7, §13 no cap, no clamp; squad size 6 declared structural | PS-8, `ssot-power-scale.md` §11 |
| §8 win rate only, stalemates excluded and reported | `decisions.md:103` |

**Decisions with no home here, by design:** D1, D2, D11, D12, D14, D16, D17, D18, D19, D22, D23, D30,
D31 (superseded by D35), D32, D35 — every one belongs to a named module in
`passive-tree-map.md:14-26`. **D15 used to be listed here as homeless.** It is not: the measurement is
claimed above and lands in S4. Only the decision it feeds — whether the equal-budget rule changes —
sits outside this module, and that is §14 open question 3, an owner call rather than a gap.

## Design-gate checklist

```
[x] I identified the subsystem(s) this touches: passive tree, battle/turns, stats, tunables, power.
[x] I read every doc in the DESIGN-GATE.md §1 row(s) for those subsystems, this session:
    passive-tree-ideal.md, passive-tree-map.md, research 05/11/16, tunables-ssot.md,
    ssot-power-scale.md (§4.7, §9.4, §10, §11), decisions.md, CLAUDE.md numeric rules.
[x] I checked decisions.md for a lock covering this - the Class system row (:103) locks win rate as
    the metric and bans a clock; the Battle time model row (:42) locks virtual time. Both are
    honoured, and the clock tension is named explicitly in section 8 rather than papered over.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments - including two corrections to the ideal:
    reflect's production caller is the INJECTOR's (CombatDamageDispatcher.TryReflect is unreachable
    from Battle, which uses DamageApplyPipeline.Apply), and doc 05 section 6.4 step 3's
    TerminationGuard.ToActor visibility blocker does not apply to this design.
[x] I read the surrounding section of every rule I quoted.
[ ] I tested (not assumed) any constraint I am reporting. NOT DONE, and named: no build is
    authorized, so nothing here was executed. Three claims are arithmetic, not measurement, and are
    shown as such - the 91-build count (12+66+12+1), the trial/half-width table
    (sqrt(0.25/n), checked against Marginal.cs:21-23's own measured 0.9pp at 3,000), and the
    scope-occurrence counts in section 1.1 (counted by grep over non-test src/, this session).
    Section 10.1 adds a fourth: D's half-width is 1.96*2*sqrt(0.25/n) = 1.96/sqrt(n), which is
    1.0pp at n = 38,416 - arithmetic, shown, and deliberately the same 40,000 tier section 9.2
    already budgets. What A10 will actually MEASURE is unrun, and is named as such there.
[x] Nothing contradicts a section 2 invariant. Invariant 9 (standalone-first) is satisfied: this is
    a tools/ measurement over Core, with the game closed.
[x] Corrections are propagated to prose, Structure, Testing, Boundaries and Decisions here.
    NOT propagated outside this file, deliberately - the brief forbids editing any other doc. Two
    items are owed to their owners: passive-tree-ideal.md section 13.1's reflect row, and
    05-mechanism-taxonomy.md section 6.4 step 3's visibility note.
```
