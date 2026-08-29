# Power scale — SSOT

**Status:** **Reconciled 2026-08-23, hardened by adversarial audit the same day**
([audit-2026-08-23.md](audit-2026-08-23.md) — 8 findings, 5 critical). Supersedes the 2026-08-23 proposal, which was written without
reading the shipped curves and contradicted three of them. Nothing new is built; two shipped
constants are named as defects with proposed values. **`decisions.md` P1 must be amended before any
of §6 is implemented.**

The single place that answers *"how strong is this thing, given where it came from?"* Every system
that produces a magnitude — items, enemies, rewards, status potency — reads this and nothing else.

---

## 0. What the first draft got wrong

Recorded because the failure log ([DESIGN-GATE.md](../../DESIGN-GATE.md) §4) is the argument for the
gate, and this is a clean example.

| The draft said | The code says |
|---|---|
| *"Player level enters the formula nowhere"* | `decisions.md:45` (**P1**) locks level → `progression.power` via `ProgressionPowerCurve`, and `StatusPolicy.IncludeTierPowerInDelta = true` ships ([StatusPolicy.cs:16](../../../src/FusionRpg.Core/Status/StatusPolicy.cs#L16)) |
| *"`scaleAt` shape is open, pick exponential / polynomial / soft-cap"* | The shared curve already ships and is **linear**: `BaseHp(level) = 80 + 30·level` ([BattleModels.cs:61](../../../src/FusionRpg.Core/Battle/BattleModels.cs#L61)), and item calibration already reads it at level 20 (`spec-numerics.md` §1) |
| *"Overflow is a real constraint… at 2% per level, level 3,200"* | Under the shipped linear curve `long` is reached around content level 3×10¹⁷. The overflow section was solving a problem the shipped math does not have |
| Three axes `M`, `R`, `L` with `R` = "run count" | The progression loop is **worlds**, not runs — *"You keep who you are. You lose where you were"*, each world starting at a higher size tier and Fracture intensity (`empire-economy-ssot.md` §4) |

The draft's *conclusion* — content declares relative power, one multiplication in one place — is
correct and is kept. Its arithmetic was invented rather than read.

---

## 1. Three things are called "power". They are not the same thing.

This is the first fix, because every downstream confusion starts here.

| Name | What it measures | Scale | Where it lives | State |
|---|---|---|---|---|
| **`contentScale`** | how much a magnitude is multiplied by, given where it dropped | ratio, ×1.0 at calibration | this document | proposed |
| **`progression.tierPower`** | an actor's standing in a *contest* — status apply and potency | points, same scale as `status.power.*` | [actor-hub-ssot.md](../actor-hub-ssot.md) §3.B, **locked** | shipped, stubbed at 1.0 |
| **`PowerVector`** | what an atom *costs* — a budget currency, five categories | integer points per category | [effect-atom/spec-power-vector.md](../effect-atom/spec-power-vector.md) (E9) | specced |

**They never convert into one another.** `PowerVector` prices content at authoring time;
`contentScale` multiplies magnitudes at drop time; `tierPower` decides contests at apply time. A
system that reads the wrong one produces numbers that look plausible and are wrong by orders of
magnitude — which is exactly the failure `spec-power-vector.md` calls the units trap.

---

## 2. The governing theorem — why *contests* must be linear

**Every contest in this codebase is a sigmoid over a *difference* of two same-scale quantities.**
Not a ratio. That is not an accident, and it constrains the curve completely.

```text
hit    σ((accuracy(att) − dodge(def)) / 100)          BattleModels.cs:69-72
crit   σ((critRate(att) − critResist(def)) / 100)     BattleModels.cs:71-72
status σ(delta / effectiveApplyScale),  delta = totalPower − totalResist
                                                       ResistanceEvaluator.cs:196-208
```

The shipped baselines are built so that **level cancels at parity**:

```text
BaseAccuracy(L) = 220 + 26·L
BaseDodge(L)    =       26·L
parity → σ((220 + 26L − 26L)/100) = σ(2.2) ≈ 0.900     BattleModels.cs:66-72
```

A level-20 duel and a level-2000 duel have the *same* hit rate. Only the **gap** matters. This is
locked by rate tests (`decisions.md:40`, "parity P(hit) 0.90±0.02").

> ### The theorem — and the condition it depends on
>
> **In a contest whose sigmoid divisor is constant, the power curve must be linear in the index.**
>
> A geometric curve `g^L` makes a fixed one-level gap worth `g^L(g−1)` — an advantage growing without
> bound. At level 12 under `2^L`, one level is worth 4,096 points against `status.power.*` values
> authored near 100. The contest stops being a contest.
>
> **The condition is load-bearing, and an earlier draft of this document omitted it.** Everything
> flips when the divisor scales with power (audit F3, measured):
>
> | Divisor | Curve | gap 5, Θ=10 → Θ=10,000 | |
> |---|---|---|---|
> | constant `/100` | linear | 0.5125 → 0.5125 | invariant |
> | constant `/100` | geometric | 0.6548 → 1.0000 | explodes |
> | `K × matchPower` | linear | 0.5010 → **0.5000** | **gap loses value** |
> | `K × matchPower` | geometric | 0.5017 → 0.5017 | invariant |
>
> Under a power-scaled divisor, linear is the *wrong* choice — a fixed advantage decays toward
> nothing. Both regimes ship today: `CombatPolicies.*Scale = 100.0` is constant,
> `effectiveApplyScale = ApplyScaleK × matchPower` is not.
>
> **Therefore the design is not "pick a curve" but "pick one regime and hold it":
> constant divisor, linear index, difference contest — everywhere.** `status-contest` removes the
> `× matchPower` term (§6.5). With one regime the theorem is true by construction rather than by
> accident, which is the only form worth relying on.

**This constrains contests, not magnitudes.** What a sword *hits for* can grow as fast as the design
wants; what decides *whether it hits* cannot. Keeping those apart is what §4.6 does, and it is why
this document can be linear and superlinear at the same time without contradicting itself.

---

## 3. Two ladders, two different mathematics

Conflating these is what makes late-game balance unfixable. They are separated here by rule.

| | **Rate ladder** | **Magnitude ladder** |
|---|---|---|
| Decides | hit, crit, status apply chance, potency factor | hp, atk, defense, item values, damage numbers |
| Reads | **level difference** | **absolute content level** |
| Maths | `σ(Δ / scale)` — linear terms, difference | `base × contentScale` — one multiplication |
| At parity | **invariant** — identical at every level | grows forever |
| Unbounded? | no, and must not be | yes, and must be |

> **Rule PS-1. `contentScale` never touches a rate input.** It multiplies magnitudes only.
> Multiplying accuracy, dodge, crit rate, crit resist, `status.power.*` or `status.resist.*` by
> `contentScale` destroys the parity invariance the rate tests lock.

> **Rule PS-2. A magnitude is scaled exactly once.** Content declares relative; this document
> multiplies. A second multiplier applied downstream re-creates the coupling this file exists to
> remove, and produces the classic bug where two systems each apply 1.5× and nobody can find the
> 2.25×.

---

## 4. The ladder — one index, one function

> **Owner decision 2026-08-23:** one mechanism, arithmetic progression, tunable factors. Geometric
> is out (§2 and §6 show what it costs). Pure linear is out — it is correct for contests and dull
> for magnitudes. The resolution is that those are **two different reads of one ladder**.

### 4.1 The index `Θ`

Every system reads a single integer, **`Θ`** — the power index. Nothing reads a raw level again.
`Θ` is an arithmetic combination of the game's ladders (§5); no exponent appears anywhere in it.

### 4.2 The function — an arithmetic progression on the *increment*

```text
P(Θ) = C + A·Θ + B·Θ(Θ−1)/2
ΔP(Θ) = P(Θ) − P(Θ−1) = A + B·(Θ−1)          ← the increment is the arithmetic progression
```

This is deliberately the **same shape as the shipped XP curve**,
`XpToNext(L) = first + (L−1)·step` ([rpg-progression.md](../rpg-progression.md)) — increment linear,
cumulative triangular. One mental model for both ladders.

**`B` is the only balance dial.** `B = 0` is pure linear; larger `B` bends the curve up. `A` is not
tuned independently — it is *derived* from the pin below.

### 4.3 The pin — retuning `B` must never move the item corpus

```text
constraint:  P(20) = 680        # BattleRuleset.BaseHp(20), the item calibration point
derived:     A = (680 − C − B·190) / 20,     C = 80
```

The item corpus is authored against `referenceBaseGameUnits(20)` (`spec-numerics.md` §1). Pinning
`P(20)` means **`B` can be retuned at any time without re-resolving a single item.** That is the
"adjust for balance anytime" property, made structural instead of promised.

### 4.4 It contains the shipped curve exactly

```text
B = 0  →  A = 30  →  P(Θ) = 80 + 30·Θ  ≡  BattleRuleset.BaseHp(Θ)
```

**The shipped linear curve is the `B = 0` special case.** Adopting this ladder with `B = 0` is a
pure refactor — zero golden movement. Setting `B > 0` is a deliberate ruleset change and bumps
`RulesetVersion`. That makes the migration in §8 safe to do in two steps: adopt the function first,
turn the dial second.

### 4.5 The band, measured

Recommended starting value **`B = 0.4`** (per-mille: `A = 26200`, `B = 400`, `C = 80`):

| Θ | P(Θ) | linear (B=0) | ratio | contentScale | local exponent |
|---|---|---|---|---|---|
| 10 | 360 | 380 | 0.95 | 0.53 | 1.07 |
| **20** | **680** | **680** | **1.00** | **1.00** | 1.13 |
| 50 | 1,880 | 1,580 | 1.19 | 2.76 | **1.28** |
| 100 | 4,680 | 3,080 | 1.52 | 6.88 | **1.43** |
| 200 | 13,280 | 6,080 | 2.18 | 19.5 | **1.61** |
| 500 | 63,080 | 15,080 | 4.18 | 92.8 | 1.79 |
| 1,000 | 226,080 | 30,080 | 7.52 | 332 | 1.88 |
| 5,000 | 5,130,080 | 150,080 | 34.2 | 7,544 | 1.97 |

The **local exponent** — `dlnP/dlnΘ` — is the number that answers the design brief. It runs
**1.1 → 1.9** across the whole playable range: never linear, never quadratic, and it *drifts upward*
so the curve keeps feeling generous as you climb. It reaches 1.5 at `Θ = 2A/B`, which is the single
number to move when the mid-game feels wrong.

| Want | Set |
|---|---|
| shipped behaviour, no ruleset change | `B = 0` (α ≡ 1.0) |
| gentle — long readable mid-game | `B = 0.2` (α 1.15 @ 50, 1.42 @ 200) |
| **recommended** | `B = 0.4` (α 1.28 @ 50, 1.61 @ 200) |
| steep — fast numeric escalation | `B = 0.8` (α 1.48 @ 50, 1.78 @ 200) |

### 4.6 Two reads of the ladder — the rule that keeps contests sane

This is how the design satisfies §2's theorem *and* the "don't be boring" brief at the same time.

| Read | Uses | Because |
|---|---|---|
| **Contest read** — hit, crit, status apply, status potency | **`Θ` itself** (linear) | Contests are differences (§2). A linear index keeps a one-step gap worth the same at Θ=10 and Θ=10,000, and keeps parity invariant |
| **Magnitude read** — hp, atk, defense, item values, damage, yields | **`P(Θ)`** (triangular) | The numbers the player *sees* grow superlinearly. This is where "not boring" lives |

> **Rule PS-3. Contests read `Θ`. Magnitudes read `P(Θ)`. Never the other way round.**

The player experiences escalating numbers on every screen while every fight stays a fight. Those are
usually in tension; separating the reads is what dissolves it.

### 4.7 Integer safety

`Θ(Θ−1)` is a product of consecutive integers and therefore always even, so `B_milli·Θ(Θ−1)/2` is
**exact in integer arithmetic** — no rounding, no float, satisfying `P13`
([economy-principles.md](../economy-principles.md)) and the world map's byte-identical replay lock.
Per-mille `P` exceeds `int64` near `Θ ≈ 2×10⁸`; `contentScale` asserts representability.

---

## 5. Composing `Θ` from the game's ladders

Five ladders exist. They compose **arithmetically** — weighted sum, integer weights, no products.

```text
Θ_actor   = Wd·daveLevel  +  Wa·realmsAdvanced  +  Wr·runTerm(pvzRuns)
Θ_content = Wz·zombossLevel  +  Wm·mapLevel(M)  +  Ww·worldTier  +  Wf·realmsAdvanced

contest   :  Θ_actor − Θ_content            # difference, per PS-3
magnitude :  P(Θ_content)                   # what the content is worth
```

| Ladder | Symbol | Bounded? | Role |
|---|---|---|---|
| **Crazy Dave level** | `daveLevel` | no | The main line. `rpg_actor_progression`, `kind=player` |
| **Realms advanced** | `realmsAdvanced` | **no — this is the infinite axis** | Prestige. One per retired world (`empire-economy-ssot.md` §4) |
| **PvZ Fusion runs** | `pvzRuns` | **yes, capped** | Extension gameplay |
| **Zomboss level** | `zombossLevel` | no | The antagonist ladder — the difficulty side of the contest |
| **Map depth / world tier** | `M`, `worldTier` | per world | Where in this world you are, and which size tier it is |

### 5.1 Why the grind is endless but does not run away

`realmsAdvanced` is unbounded, and it appears on **both** sides — `Wa` for the actor, `Wf` for the
content. That is what keeps the *difference* bounded while both sides climb forever. Magnitudes
escalate on `P(Θ)`; difficulty escalates with them; the fight stays a fight.

> **This was asserted before it was true.** An earlier draft claimed the gap stayed bounded because
> a retired world "raises `Θ_content` too" via size tier and Fracture intensity — but Fracture is not
> in the formula, and `Ww = 5` against `Wa = 25` made the gap widen **+20 Θ per world, forever**
> (audit F2). Citing an escalation the composition does not contain is the "a comment is not
> evidence" failure in its purest form.
>
> `Wf` is that escalation, written down — and it must equal `Wa` exactly. A first fix set `Wf = 20`
> against `Wa = 25`, reasoning that a small net gain per world would feel like progression. It still
> diverged: +5 Θ per world saturates a `/100` sigmoid by roughly world 100 (audit F8). The gap held a
> constant 20% of `Θ_actor`, which is what made it *look* bounded — but a sigmoid reads the absolute
> difference, not the ratio.
>
> **`Wf = Wa`, and the player's advantage lives elsewhere.** §4.5's three axes are magnitude, breadth
> and roster; content has a `Θ` and nothing else — no gear, no build, no roster. The player outpaces
> content on the two axes content cannot have, while the `Θ` contest stays a fair fight at any depth.

### 5.2 PvZ runs are uncapped — the weight is the instrument, not a ceiling

**Owner decision 2026-08-23: no cap. This game is infinite grind, and an axis that stops is a lie
about that.** `runTerm(pvzRuns) = Wr · pvzRuns`, linear and unbounded like every other axis.

The concern a cap was reaching for is real and stays on the record: `standalone-rpg-map.md`'s
one-axis rule says *"PvZ must never be the best source of something web mode also provides,"* and
uncapped lawn grinding could out-earn the prestige loop. **A cap was the wrong instrument for it.**
A cap is a cliff — it makes the axis dead past a threshold, which is exactly the "progression stops
mattering" failure §6.2 documents for `2^min(L,12)`. The right instrument is the **weight**, which is
continuous, config-driven, and adjustable without a refactor (§9).

So the rule is enforced by ratio, not by ceiling:

> **Rule PS-6. `Wr` is set so that PvZ runs are never the fastest source of `Θ`, and this is
> *measured*, not assumed.** `Θ` reports its per-axis composition (§9.1). If the run axis' share
> climbs past the prestige axis' share for a normal player, `Wr` is wrong and the report says so
> before anyone feels it.

That is the same shape `economy-principles.md` §13 uses for every other balance claim in this repo —
an assertion with a metric behind it rather than a constant chosen once and trusted forever.

### 5.3 The weights — starting values

**Owner decision 2026-08-23: pick numbers now, tune from play.** These are per-mille integers so
fractional weights need no floats, matching `P13`. Every one lives in the tuning file (§9); none
appears in code.

| Weight | Axis | Start (‰) | Rationale for the starting value |
|---|---|---|---|
| `Wd` | Dave level | **1000** (=1.0) | The unit. Everything else is expressed against one Dave level |
| `Wa` | realms advanced | **25000** (=25.0) | A retired world ≈ 25 Dave levels — the prestige loop is the dominant axis without making in-world levelling decoration |
| `Wr` | PvZ runs | **250** (=0.25) | ~100 lawn runs ≈ one world retirement. Uncapped (§5.2); the ratio, not a ceiling, is what holds PS-6 |
| `Wf` | realms advanced (**content side**) | **25000** (= `Wa`) | The escalation §5.1 always claimed. **`Wf = Wa` is an invariant, not a weight** — any gap makes the contest diverge and the sigmoid saturate (audit F8). Per-world progression is delivered by breadth and roster, which are outside `Θ` |
| `Wz` | Zomboss level | **1000** | Content side, parity with Dave by default |
| `Wm` | map depth | **5000** (=5.0) | `mapLevel(M) = Wm · DangerBand(M)`. Shipped bands run 0–6 (`SectorTypeCatalog`: homeworld 0, stable 1, barren 2, rich/nexus 3, storm/warcamp 4, boss-lair 6), so a boss-lair is worth **30 Θ** against the whole 5-tier size ladder's 25 — the deepest sector edges out the widest world, which is the intended shape. Confirmable by the world program; it no longer blocks |
| `Ww` | world size tier | **5000** (=5.0) | Five tiers, ~8 → ~128 nodes; a tier step is worth 5 levels of opposition |

**None of these is a considered balance decision.** They are defensible starting points chosen so the
system is runnable, exactly as `tier-bands.v1.json` says of its own values: *"working values chosen to
make the corpus resolvable, not a validated balance decision."* The §9 machinery is what makes
replacing them cheap.

---

## 6. The shipped defects this replaces

Both are **latent, not live** — `InjectorProgressionPowerProvider.SetLevel` has no caller anywhere in
`src/` or `tests/`, so `GetLevel` returns 0 and `PowerFromLevel(0) = 1.0`. They fire on the day the
ADR's promised *"SQLite hydrate later"* lands. Fixing them now costs nothing; fixing them after costs
a re-balance.

### 6.0 Measured, not argued

A probe over the shipped evaluator, matched attacker and defender at each level
(`ResistanceEvaluator.Evaluate`, base magnitude 20, `FixedStatusRng(0.0)`):

| L | tierPower | delta (matched) | netFactor | effectiveMagnitude | effectiveApplyScale | pApply |
|---|---|---|---|---|---|---|
| 0 | 1 | 1 | 1 | 20 | 100 | 0.5025 |
| 1 | 2 | 2 | 2 | 40 | 200 | 0.5025 |
| 3 | 8 | 8 | 8 | 160 | 800 | 0.5025 |
| 6 | 64 | 64 | 64 | 1,280 | 6,400 | 0.5025 |
| **12** | **4096** | **4096** | **4096** | **81,920** | 409,600 | 0.5025 |
| 20 | 4096 | 4096 | 4096 | 81,920 | 409,600 | 0.5025 |
| 50 | 4096 | 4096 | 4096 | 81,920 | 409,600 | 0.5025 |

Three things this shows that the arithmetic alone did not.

### 6.1 The root defect: one evaluator, two different mathematics

`pApply` is **0.5025 at every level.** That is not luck in the good sense — it is a cancellation:

```text
effectiveApplyScale = ApplyScaleK × matchPower          ResistanceEvaluator.cs:150-152
p_apply             = σ(delta / effectiveApplyScale)
                    = σ(tierPower / (100 × tierPower))  # matched pair
                    = σ(0.01)                            # tierPower cancels
```

Dividing by `matchPower` silently converts the **apply roll** into a *ratio* contest, which is
level-invariant by construction — the property §2 says a difference contest has to earn. Meanwhile
potency keeps the raw difference:

```text
netFactor = clamp(delta, 0, 10000)                       ResistanceEvaluator.cs:216   # no divisor
```

> **So the two halves of the same evaluator disagree about what power means.** The apply roll treats
> it as a ratio and is level-invariant; the potency factor treats it as a difference and explodes.
> This is the actual defect. The exponential curve is what makes it *visible*; a linear curve would
> reduce it from catastrophic to merely wrong.

**The stub hides it perfectly.** At `tierPower = 1.0`, `netFactor = delta = 1.0` — the identity
value. The shipped test `Neutral_stub_tier_power_contributes_to_delta`
(`ResistanceEvaluatorTests.cs:19-26`) asserts exactly that and passes, so the bug has a green test
sitting on top of it. `1.0` is the one value at which broken and correct agree.

### 6.2 The curve is geometric, and then it stops

```csharp
// IProgressionPowerProvider.cs:19  — ADR P1 "POC curve"
public static double PowerFromLevel(int level) =>
    level <= 0 ? 1.0 : Math.Pow(2, Math.Min(level, MaxExponent));   // MaxExponent = 12
```

Exponential *and* hard-capped is the worst of both. The table shows it: a base-20 status deals
**81,920** between two matched level-12 actors, and levels 20 and 50 are byte-identical to 12 —
progression stops mattering entirely at the cap.

> **Proposed:** `PowerFromLevel(L) = L`, uncapped. Linear per §2, and on the same scale as the
> `status.power.*` values it is summed with. Requires a `decisions.md` P1 amendment.

### 6.3 The contest is asymmetric — attacker's level counts, defender's does not

`IncludeTierPowerInDelta = true` adds the attacker's tierPower, while `ResistFromPowerRatio = 0`
multiplies the defender's away ([StatusPolicy.cs:9,16](../../../src/FusionRpg.Core/Status/StatusPolicy.cs#L9)).
Levelling makes you better at applying statuses and no better at resisting them. The probe's
`delta` column is the proof: two **identical** actors should contest at zero, and they contest at
`tierPower`.

> **Proposed:** `ResistFromPowerRatio = 1.0`. Matched pair → `delta = 0` → `netFactor = 1.0` via the
> shipped even-match case (`ResistanceEvaluator.cs:214`) → base magnitude, unmodified, at every
> level. Potency then obeys the same parity invariance the apply roll already has by accident and
> that hit and crit have by design.

### 6.5 The divisor — added by audit F3

`effectiveApplyScale = ApplyScaleK × matchPower` (`ResistanceEvaluator.cs:150-152`) is the third
defect, and it is the one that makes §2's theorem conditional rather than absolute.

Dividing the sigmoid by power turns the apply roll into a **ratio** contest. §6.1 noted that this
*hides* the exponential's damage; what §6.1 missed is what happens once the curve is linear:

| Curve | gap 5, Θ=10 → Θ=10,000 | |
|---|---|---|
| geometric + scaled divisor | 0.5017 → 0.5017 | invariant, by accident |
| **linear + scaled divisor** | 0.5010 → **0.5000** | **a fixed advantage decays to nothing** |
| linear + constant divisor | 0.5125 → 0.5125 | invariant, by design |

So the scaled divisor is not a wart the linear curve tolerates — it is a defect the linear curve
*activates*, in the opposite direction. A player 5 indices ahead has almost no better chance of
landing a status at Θ=10,000 than a matched one.

> **Proposed:** `effectiveApplyScale = ApplyScaleK`. One regime everywhere — constant divisor, linear
> index, difference contest. Both halves of the evaluator then read power the same way, which is what
> §2's theorem needs and what no choice of curve can supply on its own.

### 6.6 `netFactor` — added by audit F4

`netFactor = clamp(delta, 0, MaxNetFactor)` (`ResistanceEvaluator.cs:216`) uses a raw difference
directly as a **multiplier** on magnitude and duration:

| gap (Θ) | 0 | 1 | **2** | 5 | 25 |
|---|---|---|---|---|---|
| `netFactor` | 1.0 | 1.0 | **2.0** | 5.0 | 25.0 |

Parity and +1 both give 1.0× (the shipped `delta == 0` special case plus clamping), then **+2 abruptly
doubles it**. One retired world (`Wa = 25`) gives 25×. §6.0's table only measured *matched* pairs, so
the shape was never exercised — the same blind spot the stub created for §6.2.

> **Proposed:** `netFactor = 1 + delta / NetFactorScale`, with `NetFactorScale` a tuning constant
> (~10). Parity gives exactly 1.0 with no special case, a 10-index gap gives 2.0×, and the cliff is
> gone. The `delta == 0` branch is then dead and is deleted rather than left as a trap.

### 6.4 Fix order matters

`ResistFromPowerRatio = 1.0` **alone** fixes the matched case at every level, including under the
exponential curve — `delta = 0` regardless of shape. It does not fix mismatched pairs: at level 12
versus 11 the gap is `4096 − 2048 = 2048`, still a 2048× potency multiplier. Both changes are
needed, and 6.3 is the one that makes the system safe to look at while 6.2 is decided.

**Latency:** both are latent, not live. `InjectorProgressionPowerProvider.SetLevel` has zero callers
in `src/` or `tests/` (grep), so `GetLevel` returns 0 and `PowerFromLevel(0) = 1.0`. They fire on the
day the ADR's promised *"SQLite hydrate later"* lands.

## 7. Realm — resolved into `Θ`, not left as a multiplier

`progression.realm` ships as a channel, stubbed at 1.0, documented as *"Future breakthrough
multiplier"*, and `tierPower = power × realm` is **locked**
([actor-hub-ssot.md](../actor-hub-ssot.md) §3.B).

It is tempting to make `realm` the geometric ladder that §4 refuses to be. **Under §2 that is
illegal:** `realm` *multiplies* `tierPower`, and `tierPower` is a difference term in every contest —
so a realm multiplier is a geometric curve wearing a different hat, with the §6 failure mode
attached.

This SSOT resolves it instead:

> **A realm is a band of content, and advancing one is an additive step in `Θ` (`Wa·realmsAdvanced`,
> §5). It is not a multiplier on a contest.**

Concretely: keep `progression.realm = 1.0` permanently, and let realm advancement express itself
through `Θ` — where it raises magnitudes on `P(Θ)` and raises the opposition by the same ladder.
The player still feels a breakthrough (every number on screen jumps, because `P` is convex and `Wa`
is large); the contest maths never leaves the difference regime.

If a future design genuinely wants a live multiplier, it must first move the contests it touches from
difference to ratio — a `RulesetVersion` bump and a re-bless of every combat golden. Named here so
nobody discovers it late.

---

## 8. Migration — one scale, enforced across every feature

The point of sealing this SSOT is that everything else stops having its own opinion. This is the
cross-feature work, in dependency order. **Nothing here is authorized yet** — it is the shape of the
change, written so the sequencing hazard in step 0 is visible before anyone starts.

**Step 0 — adopt at `B = 0` first.** `P(Θ)` with `B = 0` *is* `BattleRuleset.BaseHp` (§4.4). Land the
function, the tuning file, and every call-site migration with `B = 0`, and **no golden moves**. Turn
the dial in a separate, single-purpose change that bumps `RulesetVersion` and re-blesses knowingly.
Doing both at once makes every moved golden ambiguous between "the refactor broke something" and
"the dial did its job."

| # | System | Today | After |
|---|---|---|---|
| 1 | **Power core** | — | `PowerLadder.Index(...)` → `Θ`; `PowerLadder.Value(Θ)` → `P(Θ)`; tuning file |
| 2 | **Battle baselines** | `BaseHp/Atk/Defense(level)` hardcoded, `BattleModels.cs:61` | derive from `P(Θ)`; identical output at `B=0` |
| 3 | **Battle rates** | `BaseAccuracy/Dodge/CritRate/CritResist(level)` | read **`Θ`**, not `P(Θ)` — PS-3. Parity invariance must be re-asserted by the existing rate tests |
| 4 | **Status contests** | `ProgressionPowerCurve = 2^min(L,12)`; `ResistFromPowerRatio = 0` | `progression.power = Θ`; `ResistFromPowerRatio = 1.0` (§6) |
| 5 | **Item magnitudes** | `contentLevel` → authored ranges, unscaled | `× contentScale = P(Θ_content)/680` at drop time, once (PS-2) |
| 6 | **Waves / expeditions** | literal levels 1/3/6/10, 2/5/9/14 | authored as `Θ_content` |
| 7 | **World sectors** | `DangerBand` int, no level mapping | `mapLevel(M) = 5 · DangerBand` → feeds `Θ_content` |
| 8 | **World lifecycle** | size tier + Fracture escalate per world | also emits `realmsAdvanced` → `Θ_actor` |
| 9 | **Economy yields** | soul/loam rates authored flat | scale on `P(Θ)` **only where a magnitude**; faucet/sink ratios stay `Θ`-invariant, or `P2` breaks |
| 10 | **Atom `PowerVector`** | prices at calibration | unchanged — it prices *relative* content and must stay scale-free (§1) |

**Two migration hazards worth naming now.**

- **Row 9 is the one that can quietly break the economy.** If a faucet scales on `P(Θ)` and its sink
  does not, `P1`/`P2` are violated by construction and no tuning fixes it — the repo already paid for
  that once with the `+2`/kill incident. Faucet and sink must scale on the *same* read or neither.
- **Row 10 is the one people will get wrong.** `PowerVector` is a price in relative space. Scaling it
  by `contentScale` double-counts, because the magnitudes it prices are already scaled. §1 exists to
  prevent exactly this.

**Enforcement.** Once step 0 lands, a guard test in the shape of the existing boundary guards —
*no file outside `Core/Power` may compute a magnitude from a raw level* — is what stops the drift
returning. Without it this document decays into advice within two months, which is what happened to
the three curves in §0.

---

## 9. Tuning — no power constant lives in code

> **Owner requirement 2026-08-23:** *"ensure the architecture allows we adjust easily by changing
> some config, no hard coded and refactor every adjustment."* This section is that requirement made
> structural rather than promised.

> **Rule PS-7. Every number in this document is data.** `PowerLadder` reads its constants from the
> tuning file at load and holds no literal. A balance change is a new tuning version and a restart —
> never an edit to a `.cs` file, never a rebuild, never a refactor.

### 9.1 The tuning file

`data/tuning/power-scale.v{n}.json`, versioned exactly like `data/seed/items/_tuning/tier-bands.v1.json`
— never hand-edited, republished by a tool, old versions retained for revert.

```jsonc
{
  "schemaVersion": 1, "version": 1,
  "curve":   { "cMilli": 80000, "bMilli": 400,        // A is DERIVED from the pin, never authored
               "pinIndex": 20, "pinValue": 680 },
  "weights": { "WdMilli": 1000, "WaMilli": 25000, "WrMilli": 250,      // actor side
               "WzMilli": 1000, "WmMilli": 5000,      // null is legal and throws at first use, but 5 is derived (5.3)
               "WwMilli": 5000, "WfMilli": 20000 },   // content side; Wf < Wa nets +5 Theta/world
  "contest": { "netFactorScaleMilli": 10000 },        // audit F4
  "report":  { "axisShareEnabled": true }             // PS-6's measurement
}
```

**`A` is absent on purpose.** It is solved from the pin (§4.3) at load. Authoring it would let the
pin drift, which is the one thing that would make retuning `B` move the item corpus.

**`bMilli` must be even.** In per-mille, `A_milli = (600000 − 190·B_milli)/20 = 30000 − 19·B_milli/2`,
which is an exact integer **iff `B_milli` is even**. An odd `B` is rejected at load naming the two
nearest legal values — it is not silently rounded, because a rounded `A` breaks the pin and the pin is
what protects the item corpus. `400` is legal; `401` is not. (Found while specing `power-ladder`.)

**`Wm: null` fails loudly rather than defaulting.** Same rule `numerics` already applies to an
unauthored channel share: *"a generator with no authored share must reject at import, not guess one."*

### 9.2 What the guard enforces

`power-guard` (map wave 4) is what keeps PS-7 true after the first person is in a hurry:

| Check | Fails when |
|---|---|
| No literal curve | A numeric literal appears in `Core/Power` outside the loader |
| No private `f(level)` | Any file outside `Core/Power` computes a magnitude from a raw level |
| Inventory closed | A power-shaped scale exists that §10's table does not list |
| Pin holds | `P(pinIndex) != pinValue` for any tuning version in `data/tuning/` |

### 9.3 Free, derived, and fixed

| Constant | Status | Notes |
|---|---|---|
| **`B`** | **free — the balance dial** | `0` = shipped linear; `0.4` decided. Bumps `RulesetVersion` |
| `Wd Wa Wr Wz Wm Ww` | free | §5.3 starting values; `Wm` owed |
| `Wf` | **derived — pinned to `Wa`** | Not a free weight. `Wf != Wa` diverges the contest and saturates the sigmoid (audit F8); rejected at load |
| `netFactorScale` | free | Audit F4. Larger = flatter potency response to a level gap |
| `A` | **derived** | `A = (pinValue − C − B·190)/20`. Never authored |
| `C`, `pinIndex`, `pinValue` | fixed | `80`, `20`, `680` — from `BattleRuleset` and the item corpus |

### 9.4 Integer safety

`Θ(Θ−1)` is a product of consecutive integers and therefore always even, so the triangular term is
**exact in integer arithmetic** — no rounding, no float, satisfying `P13` and the world map's
byte-identical replay lock. Per-mille `P` reaches `int64` near `Θ ≈ 2×10⁸`; `PowerLadder.Value`
asserts representability. The draft's three-way overflow decision is **withdrawn as moot** — it was
written for a geometric curve this SSOT does not use.

---

## 10. The complete inventory — every scale in the repo, and its verdict

**This is the anti-duplication clause.** A power-shaped number that is not in this table does not
have permission to exist. Adding a row is a reviewed change to this document, not a convenience.

Swept 2026-08-23 across `src/` and `docs/architecture/`.

### 10.1 Level curves — these collapse into `Θ`

| # | Scale | Shape | Location | Verdict |
|---|---|---|---|---|
| 1 | `BaseHp/BaseAtk/BaseDefense(level)` | linear `a+b·L` | `BattleModels.cs:61-63` | **Becomes `P(Θ)`.** Identical at `B=0` — `battle-magnitude` |
| 2 | `BaseAccuracy/Dodge/CritRate/CritResist(level)` | linear `a+b·L` | `BattleModels.cs:73-76` | **Becomes `Θ`** (rate read, PS-3) — `battle-rates` |
| 3 | `ProgressionPowerCurve.PowerFromLevel` | `2^min(L,12)` | `IProgressionPowerProvider.cs:19` | **Deleted.** Replaced by `Θ` — `status-contest` (§6) |
| 4 | `RpgXpPowerScale.ForKill` | stub `1.0` | `RpgXpPowerScale.cs:9` | **Deleted.** Its documented future job ("scale kill XP by zombie power") is `Θ_content` |
| 5 | `LoamPolicy.DevelopmentUpkeepPerLevel = 5` | linear | `LoamPolicy.cs:30` | **Economy magnitude — scales on `P(Θ)` only if its matching faucet does** (§10.4) |
| 6 | `XpToNext = first + (L−1)·step` | arithmetic | `rpg-progression.md` | **Kept, unchanged.** It is the *cost* ladder, not a power ladder — see §10.5 |

Row 17 (`RpgProgressionSubsystem`'s `level`-gated bonus flats, found latent by class-system P1.13,
2026-08-26) is **retired, not merely re-verdicted** — class-system P3.3 (2026-08-27) deleted the stub
from `RpgProgressionSubsystem.cs` entirely; `progression.bonus.{maxHp,atk,defense,arm1,arm2}` are
allocation-sourced now, through `AptitudeSubsystem`/`AptitudeResolver` (already governed by `aptitude-
tuning`'s PS-3 read functions, not a private `f(level)`), so there is nothing left for this table to
hold. The row number is retired with it, not reassigned.

### 10.2 Non-level scales — these are legitimate and stay

Each is bounded, or operates in relative space, so none can drift with level. They are listed so
nobody "unifies" them into `Θ` by mistake.

| # | Scale | Shape | Location | Why it stays |
|---|---|---|---|---|
| 7 | Affix tier ladder `m_t = m₁ × 1.75^(t−1)` | geometric, **5 rungs** | `ssot-affixes.md:320` | Bounded at t5 (9.4× total). A *within-item* quality ladder in relative space — it never sees a level. §2's theorem does not apply |
| 8 | Value band `lo/hi = 0.67/1.33 × m_t` | fixed ratio | `spec-numerics.md` | Roll width, relative |
| 9 | `m₁ = share × B_family(20)` | anchor | `ssot-affixes.md:177` | **This is the pin.** Already reads `BattleRuleset` at level 20 — §4.3 formalises what it already does |
| 10 | `ElementHub.SlotMultiplier` | ×1.25 / ×0.8 per slot | `ElementHub.cs:44` | Matchup, bounded, level-free |
| 11 | `CombatPolicies.*Scale = 100.0` | sigmoid divisor | `CombatPolicies.cs:10-12` | Units of the resolver, not a growth curve |
| 12 | `PowerVector` cost function | coeff × normalize × conditionality | `spec-power-vector.md` (E9) | **Prices relative content.** Must stay scale-free — scaling it double-counts (§1) |
| 13 | `PowerScalar.Of` — geomean over 5 categories | geometric mean | `PowerReads.cs:38` | **Display only, and it has no production caller.** Never a balance input |
| 15 | `double` in stat composition (14 sites) | IEEE-754 | `CombatDerivedReader`, `ElementHub`, `StatModifier`, `CombatPolicies` | **Decided 2026-08-23: it stands** — §10.7 |
| 14 | `maxTierAt(itemLevel)` — t3@8, t4@18, t5@32 | step function | `ssot-generation.md` §4.1 | Gates tier *access* by content level. A gate, not a magnitude |
| 16 | `PatronPolicy.AuraMilli(rarity, star, level)` | `rarityBase + perStar·star + level`, clamped | `PatronPolicy.cs:37` | **A different axis, found and added by `power-guard`'s own G2 sweep (T4.1, 2026-08-24).** `level` here is the *patron demon's own* level, not the actor's `Θ` — a small, hard-clamped (`AuraClampMilli`) aura bonus, spec-locked 2026-08-21, unrelated to the power ladder. Never reads `PowerTuning`, never should |

> **Rule PS-4. Rows 7–14, 16 are relative or bounded, and must never be multiplied by `contentScale`.**
> Row 12 is the one people will get wrong: `PowerVector` prices magnitudes that are *already* scaled.

### 10.3 Resolved — questions the sweep closed

| Was open | Resolution |
|---|---|
| **`B`'s shipping value** | **`0.4`.** Local exponent 1.28 → 1.88 across the playable band (§4.5). Chosen, not deferred — the §4.3 pin makes it revisable at any time with zero content churn, so agonising has no payoff |
| **`mapLevel(M)`** | **Closed.** `mapLevel(M) = Wm · DangerBand(M)`, linear in the shipped int field, with **`Wm = 5`** derived from the shipped `SectorTypeCatalog` bands (§5.3). The world program confirms or moves a weight in a tuning file — it no longer owes an unknown |
| **Depth: more enemies or stronger?** | **Both, on separate owners.** Enemy *level* is `Θ_content` and belongs here; enemy *count* is encounter design and does not. Depth raising both is legal precisely because they are different knobs |
| **`R` reset / shape** | The axis is `realmsAdvanced` (§5). It never resets, and it is a weight, not a curve |
| **`scaleAt` shape / overflow** | `P(Θ)`, triangular, integer-exact, contains `BaseHp` at `B=0` (§4) |
| **Is the 1.75 tier ladder a conflict?** | **No** — row 7. Bounded and level-free |
| **Should `progression.realm` become geometric?** | **No** — §7. It stays 1.0 permanently; realm advancement is additive in `Θ` |
| **XP curve vs power curve** | Both arithmetic-derived, and their ratio is the point — §10.5 |

### 10.7 `double` in stat composition — decided 2026-08-23: it stands

The overflow audit's A7 bucket (14 sites) asks whether the stat system should move off `double`. Two
concerns, and they resolve differently.

**Range is not the problem.** `double` is exact to 2⁵³ ≈ 9×10¹⁵, which the ladder reaches at
Θ ≈ 6.7 million. That is three orders past `int`'s per-mille limit and not the constraint.

**Determinism is the real concern, and the repo already answers it.** Combat resolution runs
`Math.Exp` in its sigmoid, whose last bit is not reproducible across architectures — and
`decisions.md:40` already handles that, not by eliminating `double` but by stamping it:

> *"Platform stamp on `BattleReport`; sweep guard refuses cross-arch re-resolution."*

**And the ops are genuinely fractional.** `Increased` and `More` modifiers are ratios; composing them
in integers would be wrong, not merely awkward. `stat-system.md` chose `double` deliberately.

> **Decision: `double` stands in stat composition.** The A7 findings are not defects. What PS-8 and the
> overflow standard bind is *magnitudes* — the `long` rule applies to the values composition
> **produces**, not to the arithmetic that composes ratios. Where a `double` result reaches a hashed
> output, the platform stamp is the shipped mitigation and stays.
>
> The one thing this does **not** license: a new `double` **magnitude** outside the composition path.
> Those are A1, not A7, and the audit keeps flagging them.

---

### 10.4 Economy — decided 2026-08-23

The hazard: a faucet scaling on `P(Θ)` against a flat sink is the `+2`/kill incident with extra steps
([economy-principles.md](../economy-principles.md) `P1`, `P2`).

> **Rule PS-5. Within one economy loop, faucet and sink scale on the same read, or neither does.**

Applied to loam: yield per sector and `DevelopmentUpkeepPerLevel` (row 5) are **both** magnitudes, so
either both take `P(Θ)` or both stay flat. Scaling one alone changes the growth *rate* of income
against expenditure, and `P2` is explicit that tuning cannot fix a growth-rate mismatch.

**Decided: neither.** Loam is a world-scoped throttle whose whole job is to bind locally
(`empire-economy-ssot.md` §5, *"loam is the throttle on every faucet the map has"*). Leaving the loam
loop `Θ`-invariant keeps that property exactly, and difficulty escalation across worlds already
arrives through size tier and Fracture intensity. **Souls, essence and materials** — which cross the
world boundary into the permanent treasury — are the ones that must scale, because they are spent
against `P(Θ)`-scaled content.

### 10.5 Why power ends up linear in *effort* — the property that makes the grind work

Both ladders derive from arithmetic progressions, and the interesting number is their ratio:

```text
cumulative XP to level L :  Σ(first + (k−1)·step)  ≈ (step/2)·L²      quadratic in L
power at index Θ         :  C + A·Θ + B·Θ(Θ−1)/2   ≈ (B/2)·Θ²         quadratic in Θ
                            ⇒ power ∝ total XP invested                LINEAR in effort
```

An hour of play buys the same absolute power at hour 5 and hour 500. Numbers on screen still
accelerate (`P` is convex in `Θ`), the contest stays flat (`Θ` is linear), and the *rate of reward
per unit time* never decays. That is the shape an endless-grind game wants, and it falls out of using
arithmetic progression on both ladders rather than being tuned in.

### 10.6 Closed — the last two, by owner decision 2026-08-23

| Was open | Decision | Note |
|---|---|---|
| **`Wa` versus `Wd`** | **`Wa = 25 · Wd`** | *"Just pick a number, we will balance-adjust later."* Recorded as a starting value, not a validated one (§5.3) |
| **`runCap`** | **No cap.** `Wr = 0.25`, uncapped | *"This game is infinity grind."* A cap is a cliff and would reproduce §6.2's dead-axis failure. The one-axis rule is held by **weight and measurement** (PS-6) instead of a ceiling |

**Nothing in this document is open, and nothing is owed by anyone.** The ADR P1 amendment is
**written into `decisions.md`**; §10.4 is **decided** (loam `Θ`-invariant, souls scale); `Wm = 5` is
**derived** from the shipped `SectorTypeCatalog`; the soul earn formula is **specified** (§11.7a) and
is a no-op at the calibration point; the `double` question is **decided** (§10.7).

What remains is one review gate: the owner reading the map and the ten specs before Phases 1–4 build.
Phases 0, M and D need nothing.

**The honest caveat on every number here:** the weights and `B` are *starting values chosen so the
system runs*, in exactly the sense `tier-bands.v1.json` says of its own. PS-7 is what makes that
acceptable — being wrong costs a config version, not a refactor.

---

## 11. Caps register — reconciling every ceiling to endless grind

**Endless grind is an owner decision and the SSOT other systems reconcile *to*.** A cap is therefore
not automatically wrong — but every one has to say which kind it is, and a progression ceiling has to
justify itself or be lifted.

> **Rule PS-8. A cap on a magnitude is a progression ceiling until proven otherwise.** Structural
> limits (recursion depth, buffer size) and bounded ratios (per-mille, 0..1) are exempt by their
> nature. Everything else is a wall on the grind and needs a verdict in this table.
>
> **A ceiling need not be a `const` to be a ceiling, and it need not be named like one.** An inline
> `Math.Min`, a narrowing `(int)` cast, a flat rate facing a scaling cost, and a *threshold* that
> halves a payout are all caps. `SoulEarnPolicy.VictoryFullPerDay` survived three sweeps because it
> refuses nothing and is named for a threshold — it was only visible by reading its **consumer**
> (audit F11). Grep the declarations, then read the use sites. §11.2a is what the first `const`-only sweep
> missed, and §11.7 is a cap that was not a number at all — a flat faucet against a scaling sink.

Swept 2026-08-23 across `src/` and the design docs.

### 11.1 Conflicts — these wall the grind and must change

| Cap | Value | Where | Why it conflicts |
|---|---|---|---|
| **`ShieldMath.MaxInput`** | `1_000_000_000` | `ShieldMath.cs:16` | An **absolute** magnitude ceiling. `contentScale` is 7,544× at Θ=5,000, so a 5,000-point hit already computes to 37.7 M and a 20,000-point one hits the wall by **Θ ≈ 13,000**. It clamps with `Math.Clamp` — **silently**, no throw |
| **`ResourceDeltaMath.AmountCap`** | `1_000_000_000L` | `ResourceDeltaMath.cs:7` | Same wall on the HP-delta path. Past it, every hit deals identical damage no matter how deep you are — the grind's numbers stop growing while its content keeps escalating |
| **`RpgStore.MaxSoulAward`** | `1_000_000_000` | `RpgStore.Souls.cs:157` | Per-award ceiling on souls. **Throws rather than clamps** — better than the two above — but SSOT §10.4 has souls scaling on `P(Θ)`, so a deep-world award eventually rejects. Its stated reason is real (*"keeps SQLite integer addition far from 64-bit overflow, which silently degrades to REAL and permanently corrupts the snapshot"*), so it must be **derived from the int64 bound, not a round decimal** |
| **`ContractPolicy.MaxSlots`** | `48` | `ContractPolicy.cs:80` | **Owner decision 2026-08-23: removed.** The legion and empire systems need a roster an order of magnitude past 48 — *"how to build an empire with only 48 contracts"*. See §11.1a: the cap was already redundant |

Where the wall lands, by base magnitude:

| base | Θ at the wall |
|---|---|
| 100 | 184,326 |
| 1,000 | 58,245 |
| 5,000 | 26,012 |
| 20,000 | **12,974** |

**Proposed:** the two `1e9` ceilings become `long` bounds derived from the overflow limit rather than
round decimal literals, and both **throw rather than clamp**. A silent clamp is the worst option
available — it turns "your gear stopped mattering" into a bug with no symptom. Owned by a new module,
`caps-reconcile`, sequenced with `content-scale` (wave 3) because that is when magnitudes start
scaling.

### 11.1a Why removing `MaxSlots` is safe — the price was already the cap

`NextSlotPrice(n) = SlotPriceStep × (n + 1)` = **300 × n** (`ContractPolicy.cs:165-166`) — an
arithmetic progression, the same shape as `XpToNext` and the ladder's `ΔP`. Cumulative cost is
therefore triangular:

| Total slots | Nth slot costs | Cumulative souls |
|---|---|---|
| 12 (free) | — | 0 |
| 48 (the old cap) | 11,100 | 199,800 |
| 112 | 30,300 | 1,515,000 |
| 512 | 150,300 | 37,575,000 |
| 2,012 | 600,300 | 600,300,000 |

**The hard cap was redundant.** Scarcity came from the escalating price, not from the ceiling at 48 —
which is exactly §5.2's principle applied to a different system: *a cap is a cliff; the continuous
instrument is the real control.* Removing it keeps growth bounded in practice and unbounded in
principle, which is what endless grind asks for.

**The warden mechanic survives intact.** `empire-economy-ssot.md` §7 cures the 500-hour problem by
having a warden permanently consume a binding slot, so *"the Nth is genuinely dearer than the first."*
That sentence is true **because of the price formula**, not because of the cap — it needed no ceiling
to be true, and it stays true at slot 2,012.

### 11.2 Progression ceilings — **all decided 2026-08-23: soft caps, never hard**

| Cap | Value | Where | Question |
|---|---|---|---|
| **Enhancement `+X`** | ~`+20` → **no cap** | `ssot-enhancement.md` §5 | **Decided: uncapped, with a risk formula as the soft cap.** Success rate falls per level, failure can break the item or drop a level, and every rate and cost is **configurable** — the throttle is the expected cost per level, which rises without ever hard-stopping. The shipped bands (Safe +1–8 / Risk +9–14 / Peril +15–, level-drop from +17) are already this shape; they simply stop at 20 for no reason |
| **Rarity promotion** | ordinal 80 → **soft cap** | `ssot-rarity.md:735` | **Decided: per-rarity adjustable promotion cost, and the ladder extends.** New rungs above `almanac` are expected, so the ceiling must be a number in a table, not a constant in code. `sunwoven`/`almanac` staying drop-*preferred* is a weighting, not a wall |

> **Both features are unbuilt.** Enhancement (lane I6) and rarity promotion (lane I1) are specs, not
> code — so this is a **design reconciliation**, not a migration. Reconciling now is free; reconciling
> after they ship is a re-balance. Both specs are owed an update: the ceiling becomes a configurable
> curve, and the curve reads the same tuning-file discipline as PS-7.

> **Correction:** an earlier draft of this register listed a single "enhancement level ceiling — 90"
> citing `ssot-rarity.md:735`. That conflated two ceilings in two different lanes: the **rarity
> promotion** ceiling (ordinal 80, lane I1) and the **enhancement `+X`** cap (~+20, lane I6). Only the
> second is a progression ceiling in the sense PS-8 means.
| World size tiers | 5 (~8 → ~128) | `empire-economy-ssot.md` §4 | **No conflict.** World *size* stops; world *count* (`realmsAdvanced`) does not, and that is the axis in `Θ`. Recorded so it is not mistaken for one |

### 11.2a Inline caps — what a `const` sweep misses

The first sweep grepped `const … Max|Cap|Limit`. **Four real ceilings are written inline** and were
invisible to it. Recorded because the next sweep will miss them the same way.

| Site | Code | Verdict |
|---|---|---|
| `EffectBag.cs:707` | `Damage = (int)Math.Min(int.MaxValue, Math.Abs(n.Amount))` | **Conflict.** A `long` amount narrowed to `int` — silently pinned at 2.147e9 |
| `EventDrain.cs:458` and `:475` | `Damage = (int)Math.Clamp(rec.Amount, int.MinValue, int.MaxValue)` | **Conflict**, two sites. Same narrowing on the drain path |
| `RpgStore.Expeditions.cs:301` | `Math.Min(rewards.EventSouls, MaxSoulAward)` | **Conflict — and it corrects §11.1.** `AwardSouls` *throws* on excess, but the expedition path **clamps**. Two policies for one ceiling, and the silent one is on the reward path |
| `DerivedComposer.cs:71-72` | `def.Cap.HasValue ? Math.Min(value, def.Cap.Value)` | **Not a conflict yet — a facility.** `DerivedStatDef.Cap` is nullable and **no channel sets one**. Governed by PS-8 the moment one does |

These join §11.1's list for `caps-reconcile`. The three narrowing casts are also A3 findings for the
overflow slice (`power-plan.md` P0.3) — the same defect seen from two directions, which is a good sign
both audits are pointed at something real.

### 11.3 Runtime and board caps — perf protection, not progression

These bound how much can exist **at one moment**, not how far you can get. A grind is unbounded over
time; a frame is not.

| Cap | Value | What it is | Endless grind? |
|---|---|---|---|
| `CapPolicyConfig.MaxLivingPlants` | 50 | RAM gate on **our Intent/FA4 spawns** — explicitly *"not vanilla waves"* (`CapPolicy.cs:9`) | **No conflict.** Bounds simultaneous entities, not lifetime progress |
| `CapPolicyConfig.MaxLivingZombies` | 80 | Same | No conflict |
| `CapPolicyConfig.MaxLivingBullets` | **−1** | Already **unlimited** — the sentinel for no cap | Already correct |
| `RpgClient.QueueCap` | 50,000 | Outbound event queue before the injector drops | No conflict — back-pressure |
| `InjectorCommandInbox.Cap` | 2,000 | HTTP fallback inbox depth | No conflict |
| `GameEventRing.DefaultCapacity` | 4,096 | Ring buffer for captured events | No conflict |
| `ResourceDeltaMath.MailboxCap` | 4,096 | Pending HP-delta mailbox | No conflict |
| `WorldEndpoints.MaxCommandsPerSubmit` | 200 | Orders per turn submission | No conflict — one turn's batch |
| `PerfEndpoints.Cap` | 240 | ~20 min of 5 s perf windows | No conflict — diagnostics |
| `BattleRuleset.MaxRounds` | 50 | A battle that cannot end | **No conflict.** Bounds a *battle*, not a career. An unbounded battle is a hang, not a grind |

### 11.4 Recursion and termination guards

Depth limits that stop a cycle. None sits on a magnitude or a progress axis.

| Cap | Value | What it is |
|---|---|---|
| `CostFunction.MaxSpawnDepth` | 1 | Spawn-atom pricing recursion — a summoner that summons summoners prices forever without it |
| `PredicateCompiler.MaxDepth` / `MaxNodes` | 4 / 16 | Predicate tree complexity at authoring time |
| `EventDrain` chain depth | 6 (1–8) | Effect chains triggering effects |
| `StatusPolicy.ProcDepthLimitDefault` | 6 | Proc-triggering-proc depth |
| `ContentHash.MaxJsonDepth` | 64 | Hash traversal guard |

**No conflict** — all of them. A cycle guard is not a ceiling on growth.

**The divisor rule (derived-stats program, T0.4).** [battle-turn-ideal.md:153](../battle-turn-ideal.md)
computes `nextReadyTick = now + (BaseCost × ActionRank × HasteFactor) / Speed` — a `Race`-class stat
(spec-stat-taxonomy.md §2.1) used as a divisor.

> A `Race` stat used as a divisor requires a floor above zero. That floor is a *structural limit* —
> division by zero is a crash, not a balance outcome — so it is PS-8 exempt and must say so in a
> comment where it is declared.

The overflow concern **inverts** for a denominator: the hazard is a very *small* value approaching
zero, not a large one, which is why this floor is registered here (recursion and termination guards)
rather than in §11.2 (progression ceilings) — it bounds a crash, not a player's power. No such stat is
registered yet: `turn.speed` / `turn.haste` / `turn.moveSpeed` stay declared-but-unregistered vocabulary
(actor-hub-ssot.md §H.6), owned by the battle stream. This row exists so the floor lands here, not in
§11.2, the day that stream gives one of them a reader.

### 11.5 Presentation caps — VFX and UI

How much can be drawn. Invisible to progression entirely.

`VfxRules.FloaterCap` 64 · `BurstCap` 24 · `GlobalCuePerTickCap` 32 · `CueQueueCap` 256 ·
`GlobalCap` 24 · `PerHostCap` 2 · `AuraMaxParticles` 6 · `ShieldBarPool` 32/3/3 ·
`OverlaySwitchLayout.MaxScale` 3 · `OverlayPausePolicy.MaxResumeScale` 10 ·
`CombatDebugObservability.Cap` 8 · `CheatCommandRunner.SeenCap` 512

**No conflict.** A number can be 10¹² and still render as one floater.

### 11.6 Bounded by nature — ratios and per-mille

Cannot overflow and cannot wall anything, because their domain is closed.

| Cap | Value | What it is |
|---|---|---|
| `ContractPolicy.LoyaltyMax` / `DeployFloor` | 1000 / 200 | Loyalty is a 0–1000 track; below 200 a demon refuses to deploy |
| `ContractPolicy.DailyGainCap` | 60 | Loyalty gain per day — a rate on a bounded track |
| `StatusPolicy.CategoryResistCap` | 0.95 | Resistance can never reach 100% |
| `StatusPolicy.MinNetFactor` / `ApplyScaleFloor` | 0 / 1.0 | Floors, not ceilings |
| `ShieldPolicy.ChipFloorKPm` / `PenCapKPm` | 100 / 3000 | Chip damage floor; penetration at best triples shield burn |
| Expedition `QuietCeilMilli` / `FoundSoulsCeilMilli` / `WildCeilMilli` | 400 / 750 / 900 | Per-mille roll thresholds on an event table |
| `ModifierOp.MinimumInterval` | 0.01 | Attack interval floor — a divide-by-zero guard |
| `ContractPolicy.MaxSettleDays` | 30 | Offline settlement window: *"a six-month absence settles thirty days"* |
| `DerivedStatPolicy.ResourceEfficiencyCap` | 1.0 | `resource.efficiency.{id}` — a cost-reduction ratio; 100% is the ceiling of "reduces cost", not a chosen balance value (spec-actor-channels.md §2.2, T4.4) |
| `DerivedStatPolicy.BreakthroughSuccessCap` | 1.0 | `progression.breakthroughSuccess` — a roll probability; 100% is the ceiling of "chance" (spec-actor-channels.md §4.2, T4.4) |

**No conflict.** `MaxSettleDays` is the only one worth a second look — it bounds *retroactive* upkeep
after a long absence, which is mercy, not a ceiling on progress.

### 11.7 Soul earn caps — **removed.** An earlier draft of this register defended them, wrongly

| Cap | Value | Verdict |
|---|---|---|
| `SoulEarnPolicy.KillCapPerMatch` | 50 | **Removed** (owner, 2026-08-23) |
| `PatronPolicy.KillSoulCap` | 50 | **Removed** |
| `SoulEarnPolicy.VictoryFullPerDay` | 3 | **Removed** — audit F11. Victory pays full for three wins a day then halves: a wall-clock throttle, and **three cap sweeps missed it** because it names a threshold, not a ceiling |

**What this register said first, and why it was wrong.** It argued *"endless grind is unbounded in
depth, not in income,"* classified these as legitimate `P1`/`P2` throttles, and cited the uncapped
`+2`/kill incident as settled precedent. The slogan is fine. The classification was not.

`P2` says *"a faucet that scales with holdings needs a sink that scales with holdings."* **The converse
binds just as hard, and this register missed it:** a sink that scales needs a faucet that scales.
Summon and slot costs scale — souls are on `P(Θ)` (§10.4) and slots are arithmetic at `300·n`, so a
late-game purchase runs to **millions or billions**. Kill income was pinned flat at **50 per match**.
Income over cost therefore trends to zero, which is `P1`'s other failure mode, the one the register
never checked for:

> *"A persistent negative gap is **starvation** — the player cannot act and stops playing."*

A flat cap against a scaling sink is not a throttle; it is starvation with a delay fuse.

**The exploit the cap was protecting against has a better fix.** `spec-soul-economy.md` records it
precisely: an 80-kill stall-defeat out-earned a fast clean win. That is a **count** exploit, and the
cap answered it by bounding count. Scaling earn by the enemy's **value** answers it at the root —
farming weak zombies pays little because each is worth little, and the exploit dies without a
ceiling that breaks at depth.

### 11.7a The replacement formula — and it is a no-op at the calibration point

Deleting a faucet's cap without saying what replaces it is how the `+2`/kill incident happened. So
the shape is specified here, and it is the smallest possible change: **keep today's constants and
multiply by `contentScale`.**

```text
soulsPerKill = KillDelta    × contentScale(Θ_enemy)      # KillDelta    = 1,   unchanged
victory      = VictoryDelta × contentScale(Θ_run)        # VictoryDelta = 100, unchanged
defeat       = DefeatDelta  × contentScale(Θ_run)        # DefeatDelta  = 25,  unchanged
```

**Three properties, all checkable:**

**1. Byte-identical today.** `contentScale(20) = 1.000`, so at the calibration point this pays exactly
`1 / 100 / 25` — the shipped numbers. The migration moves no golden and needs no balance decision;
the economy stream inherits its own constants.

**2. Faucet tracks sink — `P2` satisfied by construction.** Summon and slot costs scale on `P(Θ)`;
now so does income, on the same read. This is the whole reason the flat cap was starvation:

| Θ | `contentScale` | clean-win souls (40 kills) | vs today |
|---|---|---|---|
| 20 | 1.00 | 140 | 1.0× |
| 100 | 6.88 | 964 | 6.9× |
| 200 | 19.53 | 2,734 | 19.5× |
| 500 | 92.76 | 12,987 | 92.8× |

**3. The stall-farm exploit dies without a cap.** A stall-defeat farms weak early-wave spawns, whose
`Θ_enemy` is low, and forfeits the victory term entirely:

| Scenario | souls | souls/min |
|---|---|---|
| clean win, 40 kills @Θ20, 3 min | **140** | **46.7** |
| stall defeat, 80 kills @Θ5, 12 min | 50 | 4.2 |
| stall defeat, 200 kills @Θ5, 30 min | 88 | 2.9 |

The clean win wins on both total and rate, by 11× on rate. **And the rate is the honest metric** —
the original incident was measured in *pulls per hour*, not per match, so the regression test asserts
souls-per-minute rather than souls-per-match. A cap could never express that; a value-scaled faucet
does it for free.

> **Ownership.** This SSOT specifies the **shape**, because multiplying by `contentScale` is the power
> contract (PS-2: scaled exactly once). The demon/economy stream owns the **constants** — and they are
> today's, so there is nothing owed before `caps-reconcile` can proceed.

**PvZ item drops (2/run, 12/day) — also removed** (owner, 2026-08-23). An earlier draft of this
register defended them as a cross-mode one-axis rule. They are worse than the soul caps, because a
**per-day** limit is not a balance mechanism at all in this game:

> *"This is a single-player game, not an MMO. That is the worst design — it feels like forcing
> microtransactions."*

**The repo already made this exact argument and won it.** `standalone-rpg-map.md` §expedition anchors:

> *"**no stamina system** — with no monetization a stamina gate has no honest job."*

A daily drop cap *is* a stamina gate wearing a different name. It does not balance anything — it
paces the player against a wall clock, which is a business model, and this project has no business
model. The one-axis rule it was serving (*"PvZ must never be the best source of something web mode
also provides"*) is held by **relative value**, exactly as PS-6 holds it for `Wr`: make a PvZ drop
worth what it is worth, and measure the share. A clock is the wrong instrument for a design goal.

The `2/run` cap goes with it. A per-run limit is a count cap facing a scaling sink — §11.7's defect
in miniature.

### 11.8 Data retention — the ledger tail, not the balance

| Cap | Value | What it is |
|---|---|---|
| `KeepLastNFullCaptureRuns` | 50 | Full per-run capture kept hot; older moves to cold archive |
| `ActivityRetainTail` | 10,000 | Hot activity facts |
| `XpRetainTailPerActor` | 5,000 | Hot XP ledger rows |
| `SoulRetainTailPerPlayer` | 5,000 | Hot soul ledger rows |

**No conflict, and worth stating why:** these trim the *ledger tail*, never the balance. Balances are
watermarked projections (`economy-principles.md` `P14`), so a trimmed row is history moving to cold
storage, not progress being deleted. Level 40,000 survives having its first 10,000 XP rows archived.

### 11.9 Not caps at all

`DemonSpeciesCatalog.DemonTypeIdFloor` 10,000 is an **id namespace offset** — it keeps demon type ids
clear of game type ids. `ContentValidation.DriftFloor` 1 is a **tolerance floor** for the power-drift
test. `OverlaySwitchLayout.MinScale` 1 is a minimum, not a maximum. Listed because a naïve grep for
`Max|Cap|Floor|Limit` returns them and someone will otherwise re-triage them every sweep.

### 11.10 Content breadth — a different axis

| Cap | Value | Verdict |
|---|---|---|
| `DemonSpeciesGenerator.DefaultMaxSpecies` | 24 | **No conflict.** How many species the generator emits — an authoring quantity. More species is content work, not a progression ceiling |
| `ShieldPolicy.MaxShieldsPerActor` | 3 | **Design, not progression.** A stacking rule — three layers, drained outer-to-core. Removing it changes shield strategy, not how far a player can get |
| Affix tier ladder | 5 rungs | **No conflict** — §10.2 row 7. Bounded, level-free, relative |
| Rarity ladder | 10 rungs | **No conflict.** The ladder's *length* is content; `contentScale` multiplies what a rung is worth |

---

## 12. Design-gate checklist

```
[x] Subsystems identified: power, progression, stats/actor-hub, battle, world, economy, item.
[x] Read this session: DESIGN-GATE.md, software-architecture.md, decisions.md,
    actor-hub-ssot.md (§3.B-D, §4), rpg-progression.md, economy-principles.md,
    empire-economy-ssot.md (§2,§4,§5,§7), world-map-program.md, item/ssot-generation.md §4,
    seedsmith/spec-numerics.md §1-2, effect-atom/spec-power-vector.md.
[x] decisions.md checked — P1 (UpdatePower), P2, RpgProgression, Actor Hub SSOT,
    Combat resolution SSOT, Battle time model, Status SSOT.
[x] Every factual claim cites file:line.
[x] Verified against CODE, not comments — BattleModels.cs, IProgressionPowerProvider.cs,
    StatusPolicy.cs, ResistanceEvaluator.cs, WaveCatalog.cs, InjectorProgressionPowerProvider.cs.
[x] Read surrounding sections of every rule quoted (ssot-generation §4.1 read in full before
    the draft's "player level nowhere" quote was narrowed to magnitudes).
[x] Constraint tested, not assumed: §6.0 is a measured probe run through the shipped
    ResistanceEvaluator, not arithmetic. §6's latency verified by grep - SetLevel has zero
    callers in src/ or tests/.
[ ] CONTRADICTS A LOCK — named explicitly: §6 proposes changing ADR P1's POC curve
    (2^min(L,12) → L) and StatusPolicy.ResistFromPowerRatio (0 → 1.0).
    decisions.md P1 must be amended before implementation. This is a decision, not a fix.
[ ] Not propagated yet — rpg-progression.md still says UpdatePower is "Not in code" (it is,
    IProgressionPowerProvider.cs:15) and actor-hub-ssot.md §3.B still shows the 1.0 stub as the
    only contract. Both need a pass once P1 is amended.
```

**Measured, not asserted.** §6.0's table is a probe run against the shipped evaluator
(`dotnet test tests\FusionRpg.Core.Tests`), not arithmetic on paper. The probe was temporary and has
been removed; the permanent version belongs in `ResistanceEvaluatorTests` as two cases — matched pair
at level 12 asserting today's `netFactor = 4096`, and the same pair asserting `1.0` once
`ResistFromPowerRatio = 1.0` lands. That turns this document's central claim into a red test, which
is the right way for it to be owed.
