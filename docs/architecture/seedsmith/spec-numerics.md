# Seedsmith — `numerics`

**Status:** Proposed 2026-08-23. Nothing is built.

Resolves every magnitude in the item corpus from bands and registry constants. P1's home: a model
never picks a number, and no number is ever stored in a seed file.

---

## 1. What I did not get to decide, and that is the finding

I set out to choose balance formulas. Almost all of them already exist, locked, in
`bands.v1.json` — and reading before proposing saved inventing a parallel system that would have
silently disagreed with the one the atom layer already uses.

**Locked. Implement exactly; do not re-derive:**

| Constant | Value | Source |
|---|---|---|
| Magnitude ratio per tier | **r = 1.75** (t1→t5 ≈ 9.4×) | `powerBand.tierScaling.magnitudeRatioPerMille` |
| Duration ratio per tier | **r = 1.4**, *mandatory*, never 1.75 | same — three items at 1.75 chain a permanent lock |
| Band width | **±33%** (`bandFloor 670`, `bandCeiling 1330`) | same |
| Reference level | **20** | same |
| Band → tier | trivial…extreme → 1…5 | `powerBand.tierMap` |
| Affixes per rung | `countBand {min,max}` | `core.v1.json rarity.ladder` |
| Tier window per rung | `tierWindow` | same |
| Role budget share | `budgetWeightMilli`, Σ = 1000 | `core.v1.json roles.list` |
| Unique / set-member premium | **1.5 AE** each | ssot-uniques §3.5, ssot-sets §3.5 |
| **1 AE** | one rolled affix at its rung's tier-window **midpoint** | ssot-sets.md:187 |

The four channel-family formulas are specified too — `primaryChannel`, `flatDerivedChannel`,
`sigmoidDerivedChannel`, `statusMagnitudeAndDuration`. The primary one:

```
m1   = round_legible(sharePermille × referenceBaseGameUnits(20) / 1000)
m_t  = round_legible(m1 × 1750^(t-1) / 1000^(t-1))
lo_t = round_legible(670  × m_t / 1000)
hi_t = round_legible(1330 × m_t / 1000)
```

Verified against both committed examples: vitality 30‰ × 680 ÷ 1000 = 20 ✓, might 45‰ × 92 ÷ 1000
= 4 ✓.

**`referenceBaseGameUnits` is read from shipped code**, not authored — `BattleRuleset.BaseHp(20)` =
680, `BattleRuleset.BaseAtk(20)` = 92 (`src/FusionRpg.Core/Battle/BattleModels.cs:60-61`). If the
ruleset changes, every magnitude moves with it, which is correct and is why it must never be copied
into a data file.

---

## 2. The one thing genuinely open — and it is the whole balance surface

`sharePermille` per channel. The registry states plainly that it is authored elsewhere, in an
artefact that **does not exist yet**, and forbids improvising it:

> *"A generator with no authored share for a channel must reject at import, not guess one."*

That artefact — call it **tier-bands** — is the entire tunable surface of item balance. Everything
else is arithmetic. So this spec builds tier-bands as *the* rebalance knob, and `numerics` refuses
to resolve a channel that has no authored share rather than defaulting one.

### 2.1 What `sharePermille` actually means

`m1 / referenceBase = sharePermille / 1000`. The share **is** the relative increment a tier-1 affix
gives against that channel's own reference base. `vitality: 30` means *a t1 vitality affix is worth
3.0% of a level-20 character's base HP*.

That makes the units directly comparable across channels, which is what lets one number per channel
carry the whole balance.

### 2.2 The v1 values I am choosing, and why they diverge from the examples

Both committed examples are labelled *"illustrative, inherited, not balanced"*, and they are
internally inconsistent: vitality gives 3.0% of base HP while might gives 4.5% of base attack, for
the same 1 AE. Since **1 AE is defined as one affix at midpoint tier**, two affixes at the same tier
costing the same AE must deliver the same power, or the unit means nothing.

So v1 normalises, and factors the share into two parts so the balance question has exactly one home:

```
sharePermille[c] = baseShare × channelWeight[c] × opWeight[op]
```

| Term | v1 value | Meaning |
|---|---|---|
| `baseShare` | **35‰** | one t1 affix ≈ 3.5% of its channel's reference base. Between the two inherited anchors (30 and 45), so nothing moves far. |
| `channelWeight[c]` | **1.0 for all 14** | how much a percent of *this* channel is worth versus a percent of any other. **The vector telemetry refits.** |
| `opWeight[Flat]` | 1.0 | baseline |
| `opWeight[Increased]` | 1.0 | additive with other Increased; same value per point |
| `opWeight[More]` | **0.55** | multiplicative, so it compounds where Increased dilutes. ≈ 1/1.8. Less magnitude for the same AE. |
| `durationRatio` | 1.4 | locked, mandatory |

**This deliberately moves the two examples** — vitality 30 → 35, might 45 → 35. That is the point:
starting from "every channel is worth the same per percent" is a *defensible null hypothesis* that
telemetry can then disprove channel by channel. Starting from inherited numbers that were never
balanced means every later measurement is confounded by an unexplained 1.5× offense bias nobody
chose.

`channelWeight` starting at all-1.0 is not a claim that all channels are equally valuable. It is a
claim that **we do not yet know**, encoded honestly, with one obvious place to write down what we
learn.

---

## 3. Adjustability — how rebalancing works

### 3.1 Three layers, and only one of them ever changes

| Layer | Where | Changes when |
|---|---|---|
| **Shape** — the formulas | code, versioned with the module | a locked registry constant changes (rare, needs a registry bump) |
| **Constants** — tier-bands | `data/seed/items/_tuning/tier-bands.v{n}.json` | **rebalancing** |
| **Resolved** — the numbers | computed, never written to a seed file | automatically, whenever either of the above does |

Because magnitudes were never in the seed corpus, **rebalancing edits no content.** One tuning file
changes and 1,438 entries resolve differently. That is the payoff of `seed-contract.md` §3's
no-numbers rule and the reason it was worth enforcing so stubbornly.

### 3.2 The API

```python
tuning  = TierBands.load(version="latest")
values  = resolve(corpus, tuning)                    # pure; no I/O, no mutation

proposed = tuning.adjust({"channelWeight.might": 0.85,
                          "channelWeight.vitality": 1.10})

report   = rebalance(corpus, tuning, proposed)       # what would move, and by how much
report.publish(version=tuning.version + 1)           # writes tier-bands.v{n+1}.json only
```

`rebalance` is a **diff, not a mutation** — it reports every magnitude that moves, grouped by
channel and tier, with the largest movers first, and writes nothing until `publish`. Nobody should
change a balance constant without seeing what it does to 1,438 items first.

### 3.3 Guardrails, asserted on every resolve

Cheap invariants, checked before any value is returned:

- **Monotonicity** — `m_1 < m_2 < … < m_5` per channel. Violation means a bad ratio or a rounding
  collision at small magnitudes; §5 of the analytics spec checks the same property on the corpus.
- **Band containment** — `lo_t ≤ m_t ≤ hi_t`.
- **Band OVERLAP is required, not forbidden** — `hi_t ≥ lo_(t+1)` for every adjacent pair.

  > An earlier draft of this spec asserted `hi_t < lo_(t+1)`, "so tier windows do not overlap into
  > ambiguity". That is backwards, and it would have raised on the first resolve of every channel.
  > `bands.v1.json` `tierScaling.overlap` states the requirement and proves it with the same
  > arithmetic: `1330/670 ≈ 1.985 > 1.750`, so `hi_t = 1.33·m_t` always clears
  > `lo_(t+1) = 0.67·1.75·m_t = 1.1725·m_t`.
  >
  > Overlap is **design guarantee OD4** — a well-rolled lower rung must be able to beat a
  > badly-rolled higher one, or the rarity ladder becomes a strict staircase and every drop below
  > your current rung is instantly worthless. The bands are built to overlap *by construction, not
  > by luck*, and a validator that forbade it would have destroyed the property the ladder exists
  > for. Ties count: `might` resolves `hi_1 = lo_2 = 5` and the registry accepts that explicitly,
  > so the comparison is `≥`, never `>`.
  >
  > Recorded rather than quietly corrected because of how it happened. §1 of this document opens by
  > noting that reading first avoided inventing a parallel system — and then the one invariant the
  > module asserts on every resolve contradicted the registry it had just finished quoting. Reading
  > a file is not the same as reading the section that governs the line you are writing.
- **Apportionment closure** — role shares resolve to integers summing exactly to the budget, via
  largest-remainder. Naive rounding drifts and every downstream check inherits the error.
- **Integer-only output** — per-mille integers throughout; no float reaches a comparison.
- **No silent defaults** — an unshared channel raises, per the registry's own instruction.

### 3.4 Explainability

`explain(entry_id)` prints the derivation chain — share, reference base, tier, ratio, rounding — for
one entry. Without it, a balance argument becomes two people asserting numbers at each other. With
it, disagreements land on a specific line of a specific formula.

---

## 4. The path to data-driven balance

`channelWeight` is a vector of 14 numbers, initialised to 1.0 and explicitly unknown. Gameplay
telemetry refits it. The interface belongs in this spec even though the module comes later, because
it constrains what the game must log.

**What must be logged**, per battle: items equipped (container ids), resolved magnitudes per
channel, outcome, time-to-kill, damage taken and dealt. Without resolved magnitudes on the record,
a later ruleset change makes historical rows unreadable.

**How the fit works.** Marginal contribution of each channel to win probability — logistic
regression of outcome on per-channel magnitudes, giving a coefficient vector, normalised so the mean
weight stays 1.0 (the fit determines *relative* worth; `baseShare` sets absolute scale, and letting
both float makes the system unidentifiable).

**Three failure modes that must be designed against, not discovered:**

- **Confounding by availability.** A channel that appears on common items looks strong because it is
  *worn*, not because it is good. Condition on item availability, or weight by drop rate.
- **Collinearity.** `vitality`, `fortitude` and `bulwark` all move maxHp by different ops; their
  magnitudes correlate and a naive fit splits credit arbitrarily. Fit the *channel*, then apportion
  across ops by the locked `opWeight`, rather than fitting fourteen families independently.
- **Feedback.** Rebalancing changes what players equip, which changes the next dataset. Refit on a
  fixed window after a change, never continuously, or the weights oscillate.

**Cadence:** propose a refit, view the `rebalance` diff, publish a new tier-bands version. Every
version is retained, so any balance state is reproducible and any change is revertible by pointing
at the previous file.

---

## 5. Open, and deliberately so

- **`channelWeight` for the 14 primary families** ships at 1.0. Correct as an admission of ignorance;
  it will be wrong, and telemetry is how it stops being wrong.
- **The other three channel groups** — `flatDerivedChannel`, `sigmoidDerivedChannel`,
  `statusMagnitudeAndDuration` — have locked formulas and need their own shares. Same treatment,
  specced when their families are resolved.
*(`baseShare` was open in the first draft of this spec and is now derived — see §6.)*

---

## 6. Deriving `baseShare` instead of guessing it

A scale constant chosen by feel is a constant nobody can argue with, so it is worth one step of
arithmetic to make it answerable. The question that determines it is a design question with a real
answer: **how much stronger is a fully-geared character than a naked one?**

Everything needed is already fixed. `BattleRuleset` makes base stats linear in level
(`BaseHp = 80 + 30L`, `BaseAtk = 12 + 4L`), `core.v1.json` gives affixes per rung and the tier
window, and `r = 1.75` is locked. So for a loadout at rung R:

```
gain_per_channel = (SLOTS × affixesPerItem / effectiveChannels) × baseShare × r^(meanTier - 1)
```

with `SLOTS = 15` and `effectiveChannels ≈ 5` — affixes spread across roughly five combat-relevant
channels, so a single channel receives about a fifth of them. Solving across the ladder:

| baseShare | grafted | fused | chimeric | heirloom | sunwoven | almanac |
|---|---|---|---|---|---|---|
| 20‰ | 1.16× | 1.46× | 1.64× | 2.13× | 2.91× | 3.34× |
| 30‰ | 1.24× | 1.69× | 1.96× | 2.69× | 3.87× | 4.51× |
| **35‰** | **1.28×** | **1.80×** | **2.13×** | **2.97×** | **4.35×** | **5.09×** |
| 40‰ | 1.31× | 1.92× | 2.29× | 3.25× | 4.83× | 5.68× |

**Chosen: `baseShare = 35‰`, from a declared target of endgame ≈ 4.5× naked** (the solver gives
36.6‰ for exactly 4.5×; 35 is the round number one notch under, and lands at 4.35×).

Why that target. Below ~3× gear stops mattering and the game is a level-treadmill; much above ~6×
and base stats are noise, which makes early play feel weightless and every balance change hostage to
loot. Four-to-five keeps both halves legible. The progression it produces is also smooth —
1.3 → 1.8 → 2.1 → 3.0 → 4.4 — with no cliff between adjacent rungs, which the ladder needs anyway
for §5's monotonicity check to pass on real content.

**Excluded from this number, deliberately:** the `+X` enhancement track, unique and set premiums,
sockets and charms. Those stack on top, so a fully-optimised endgame character lands nearer 5–5.5×.
The 4.5× target describes *base gear*, because that is the part `baseShare` controls; folding the
extras in would make one constant answerable for four systems.

### 6.0 Superseded — the item system is scale-free, so this question moved

**Owner decision, 2026-08-23** ([ssot-power-scale.md](../power/ssot-power-scale.md)): item content
declares **relative** ranges, and a separate power scale converts them to absolute at drop time,
from map depth and run count.

That dissolves the blocker §6.1 was wrestling with rather than answering it. "How strong is endgame
gear against endgame content?" is a **power-program** question now, because the multiplier lives
there. `baseShare` reverts to what it should always have been: an **internal relative constant** —
how much one affix is worth against base *at the calibration point*, used only to keep channels
comparable to each other.

The item corpus can therefore be finished, validated and balanced against itself with no progression
design in existence. §6.1 and §6.2 below are kept because their reasoning still governs the
calibration point and the seam, and because the two mistakes they record are worth not repeating.
§6.3's rule — never resolve at a hardcoded level — is unchanged and now doubly true: the caller
supplies the point, and the power scale is applied on top of what this module returns.

---

### 6.1 Audit correction — a multiplier is the wrong unit

The audit's game-design lens raised a BLOCKER: 4.5× is solved from player-side terms only, so it
cannot be checked against what the player is actually fighting. Chasing it produced something worse
than a missing cross-reference — **the unit itself is wrong.**

`WaveCatalog.cs:61-63` builds enemies from the *same* `BattleRuleset.BaseHp/BaseAtk/BaseDefense(level)`
the player uses. There is no monster table; both sides are one curve, and that curve is **linear in
level**. A multiplier on a linear base is therefore not a level-invariant statement of power:

| gear multiplier | at L1 | at L5 | at L10 | at L20 | at L30 |
|---|---|---|---|---|---|
| 1.80× | +2.9 lv | +6.1 | +10.1 | +18.1 | +26.1 |
| **4.35×** | **+12.3 lv** | **+25.7** | **+42.4** | **+75.9** | **+109.4** |

The same 4.35× is worth twelve levels early and seventy-six at the reference level. "Endgame = 4.5×
naked" does not describe one relationship; it describes a different one at every level, so no amount
of monster data could have validated it as stated. Solving for it accurately was solving the wrong
equation.

**Correction: the target is an effective-level delta, and `baseShare` is solved from it.**

```
solve_base_share(target_level_delta, reference_level)
```

A level delta is level-invariant *because both sides share the curve* — it says "full gear is worth
fighting N levels above you", which is a sentence a designer can hold an opinion about and a wave
table can falsify. Its multiplier equivalent falls out per level rather than being the input:

| level delta | at L1 | at L10 | at L20 | at L30 |
|---|---|---|---|---|
| +10 lv | 3.73× | 1.79× | 1.44× | 1.31× |
| +20 lv | 6.45× | 2.58× | 1.88× | 1.61× |

**And the target is now checkable, which is the part that matters.** Shipped content tops out at
`rift-tyrant`, **level 10, six enemies** — 380 hp each, 2,280 total. A geared level-20 character at
4.35× carries 2,958 hp and 400 attack against that. So the current anchor is **not supported by any
content that exists**; it was calibrated against an endgame that has not been built.

### 6.2 Owner decision — do not pin the anchor; make it swappable

**The power-scale and progression systems are not designed yet.** `WaveCatalog` is a stub — four
waves at levels 1, 3, 6 and 10 with hardcoded enemy counts — and the real progression design will
replace it. Owner direction, 2026-08-23:

> *Choose an architecture that can extend later. We will migrate the item system once the
> progression design is complete.*

That reframes the blocker correctly, and it is a better answer than the one §6.1 was reaching for.
§6.1 was still hunting for the right number; the requirement is that **nothing structural depends on
the number being right yet.** Two consequences.

**1. The progression model becomes a dependency, not an assumption.**

`numerics` currently bakes in a specific world: base stats linear in level, one shared curve for both
sides, level as the difficulty axis. All three are true of `BattleRuleset` today and none is
guaranteed to survive a progression redesign — a future model might scale exponentially, or replace
level with a stage or ascension axis entirely.

So they move behind one small seam:

```python
class ProgressionModel(Protocol):
    def reference_base(self, channel: str, point: ProgressionPoint) -> int: ...
    def axis(self) -> str: ...                      # "level" today; may become stage, tier, …
    def content_ladder(self) -> list[Encounter] | None: ...   # None while progression is a stub
```

`BattleRulesetProgression` implements it today by reading `BaseHp/BaseAtk/BaseDefense`. When the real
design lands, a second implementation replaces it and **no formula, no metric and no content
changes.** The seam is one protocol and one class — the minimum that makes the swap possible, not a
framework for imagined futures.

**2. An unvalidatable target reports `NOT_MEASURED`, never a pass.**

`content_ladder()` returning `None` — which is the honest answer while progression is a stub — means
the Balance family cannot check whether gear is correctly scaled, so it reports `NOT_MEASURED`
(spec-metrics §2) rather than green. The corpus still resolves, still validates for coverage,
linkage and distribution, and simply does not claim its power curve is right. That is the same
discipline that would have caught nine empty partitions: **absence of a check must never read as a
passing check.**

`baseShare = 35‰` therefore stays, explicitly as a **working value chosen to make the corpus
resolvable**, not as a balance decision. It is internally coherent, it is one function call to
recompute, and it is labelled so nobody later mistakes it for a validated constant.

### 6.3 Unbounded levels — why the share formulation survives, and what breaks if misread

Owner constraint, 2026-08-23: **the game has no level cap. Power grows without bound.**

That is fatal to one reading of the locked formula and harmless to another, so the distinction has
to be written down rather than left to whoever implements it.

**The failure mode.** `referenceLevel = 20` appears in the formula as
`referenceBaseGameUnits(referenceLevel)`. Read as *"evaluate every magnitude at level 20"*, a Flat
affix is a constant while the base grows linearly forever:

| player level | base HP | a fixed +105 HP affix | as a share of base |
|---|---|---|---|
| 20 | 680 | +105 | 15.4% |
| 100 | 3,080 | +105 | 3.4% |
| 1,000 | 30,080 | +105 | **0.3%** |

Gear decays to decoration. Every Flat channel dies this way; only `Increased` and `More` survive,
because a per-mille ratio is scale-free by construction. A corpus where half the channels quietly
stop mattering past some level is not a balance bug that shows up in a test — it shows up in a
player review two years later.

**Why the formulation is already right.** `sharePermille` is defined as *a fraction of the
channel's reference base*, not as an absolute quantity. So evaluating at the actual progression
point instead of a fixed one keeps gear at a constant share of base, forever:

```
m1(point) = share × referenceBase(channel, point) / 1000
```

`m1(L)/base(L) = share/1000` for every `L`, whatever shape the base curve has. Linear today,
exponential after a progression redesign — the ratio holds either way. This is the property that
makes the share-based formulation the correct one for an uncapped game, and it is the strongest
argument yet for `numerics` never storing a resolved number.

**Therefore, binding:**

- **`referenceLevel = 20` is a CALIBRATION anchor, never an evaluation point.** It is the level at
  which shares were chosen and at which two worked examples are quoted. Resolving a shipping
  magnitude at a hardcoded 20 is a defect, and it is the single easiest mistake to make while
  reading `bands.v1.json` literally.
- **Magnitudes resolve at a `ProgressionPoint`** — supplied by the caller: the item's level, the
  content level that dropped it (`loam`/`loot_source.content_level`, ssot-generation §4.1), or the
  wearer's. Which of the three is a generation decision, not a numerics one; `numerics` takes the
  point and applies the formula.
- **The five tiers stay five.** With unbounded levels, tier is *quality at a level*, not absolute
  power — a t5 affix is the best roll available, not a fixed number. This is what lets a locked
  five-rung ladder coexist with infinite progression, and it means the tier ladder never needs
  extending no matter how high levels go.
- **A guardrail:** `numerics` asserts that no resolve is called with the literal calibration level
  unless the caller explicitly asks for the calibration case. Cheap, and it catches the exact
  misreading above.

**A consequence worth flagging to whoever designs progression.** Because a share is scale-free,
gear's *relative* contribution is constant at every level — which means gear alone can never make a
character outgrow content that scales at the same rate. Growth has to come from somewhere: more
affixes, higher rungs, sockets, sets, the `+X` track. That is a healthy structure and it is worth
knowing it was a consequence of this formulation rather than a separate decision.

**Migration, when progression is designed.** Swap the `ProgressionModel` implementation, run
`solve_base_share` against the real ladder, publish a new tier-bands version, re-resolve. **No seed
file changes** — magnitudes were never stored in them. That property has now paid for itself three
times: rebalancing, the ruleset dependency, and now an entire progression redesign, none of which
touch a single authored row.

**The knob stays live.** `solve_base_share(target_multiplier)` is part of the module, so the
argument is always about the target — a number with game meaning — and never about 35 itself.
`effectiveChannels = 5` is the model's one soft assumption; it is measurable from the corpus once
affixes exist across channels, and `numerics` recomputes it rather than hard-coding it.
