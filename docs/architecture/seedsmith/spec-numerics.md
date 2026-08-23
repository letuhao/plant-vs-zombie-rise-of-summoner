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
- **Band containment** — `lo_t ≤ m_t ≤ hi_t`, and `hi_t < lo_{t+1}` so tier windows do not overlap
  into ambiguity.
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

**The knob stays live.** `solve_base_share(target_multiplier)` is part of the module, so the
argument is always about the target — a number with game meaning — and never about 35 itself.
`effectiveChannels = 5` is the model's one soft assumption; it is measurable from the corpus once
affixes exist across channels, and `numerics` recomputes it rather than hard-coding it.
