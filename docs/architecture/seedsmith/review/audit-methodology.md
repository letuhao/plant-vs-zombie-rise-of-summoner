# Seedsmith spec set — adversarial audit: statistics, algorithms, mathematics

**Scope of this pass:** methodological soundness of the maths only — is each technique the right
tool, applied correctly, with degenerate cases handled and arithmetic that checks out. Not prose,
naming, or project management; other passes own those. Read: `seedsmith-map.md`, all seven files
under `seedsmith/`, and — where a spec claims a number is "locked" or "verified" — the actual
registry file it cites (`data/seed/items/_registry/core.v1.json`, `bands.v1.json`), because a
locked constant is exactly the kind of claim this lens should not take on faith.

**Where this pass lands overall:** the population-vs-sample framing is the right foundational call
and is argued well. The algorithm choices (Hopcroft–Karp, Kahn/Tarjan, PAVA, largest-remainder,
MinHash) are, with one exception, the correct textbook tool for the stated job. The failures are
concentrated in two places: degenerate-case handling that the spec never mentions (Pielou, PAVA's
"pooled = inversions" claim), and one place where the spec's own worked derivation contradicts the
locked registry it explicitly set out to defer to. That contradiction is the headline finding.

---

## 1. `hi_t < lo_{t+1}` — the guardrail asserts the opposite of the locked registry

**Severity: BLOCKER**

`spec-numerics.md` §3.3 states, as one of the "Guardrails, asserted on every resolve":

> "**Band containment** — `lo_t ≤ m_t ≤ hi_t`, and `hi_t < lo_{t+1}` so tier windows do not overlap
> into ambiguity."

This is checkable against the same document's own §1 table of locked constants: magnitude ratio
`r = 1.75`, band width `bandFloor 670 / bandCeiling 1330` (±33%). Composing them:

```
hi_t      = 1.330 × m_t
lo_(t+1)  = 0.670 × m_(t+1) = 0.670 × 1.75 × m_t = 1.1725 × m_t
```

`1.330 > 1.1725`, so `hi_t > lo_(t+1)` — **the bands overlap**, for every channel using the default
width and ratio. The guardrail as written would raise on the very first resolve.

This isn't a case of the spec inventing a wrong formula from scratch — it is a direct
misstatement of the registry it says it read first. `bands.v1.json` (`tierScaling.overlap`) not
only computes the identical numbers, it states the **opposite requirement**:

> `"requirement": "hi_t must be >= lo_(t+1) for every adjacent tier pair — the rarity ladder's
> overlap guarantee (OD4) depends on it, and it must hold by construction, not by luck..."`
> `"conclusion": "1985 > 1750, so hi_t (1.33 × m_t) is always >= lo_(t+1) (0.67 × 1.75 × m_t =
> 1.1725 × m_t) for the default magnitude ratio. Overlap holds for every family that uses the
> default width and ratio."`

Same arithmetic (1985 vs. 1750, 1.33 vs. 1.1725), opposite conclusion. `bands.v1.json` also proves
the duration ratio (1.4) overlaps "even more comfortably," and documents a known tie edge-case
(`might`, `hi_1 = lo_2 = 5`) that the seedsmith spec's stated invariant would also reject as a
violation rather than the accepted tie it is.

`spec-numerics.md` §1 opens with "reading before proposing saved inventing a parallel system that
would have silently disagreeing with the one the atom layer already uses" — and then, on the one
guardrail the module is supposed to assert on every resolve, does exactly that. Fix: the guardrail
should read `hi_t ≥ lo_(t+1)` (overlap required, matching OD4), or — if the intent really is
non-ambiguous tier *assignment* rather than non-overlapping *ranges* — the spec needs to say what
invariant it actually means, because "windows do not overlap" is not it.

---

## 2. Pielou evenness is undefined at `S = 0` and `S = 1`, and nothing guards it

**Severity: MAJOR**

`spec-analytics.md` §1.2: `J = H / ln(S)`, "**the headline number**: 1.0 is perfectly even, 0 is
total dominance." No mention of what happens when `S` (cells occupied) is 0 or 1.

- **`S = 1`**: `ln(1) = 0`. Also `H = -1·ln(1) = 0` (the single occupied cell has `p = 1`). So
  `J = 0/0` — an indeterminate form, `NaN` in every mainstream numeric stack, not "0 is total
  dominance" as the prose implies. A partition with everything crammed into one cell — the exact
  case the metric exists to flag — is the case where it silently returns garbage instead of the
  worst possible score.
- **`S = 0`**: `ln(0) = -∞`; `H` is a sum over an empty index set (`0`). `0 / -∞` depends on how the
  implementation short-circuits; in a naive loop it is more likely a `ZeroDivisionError` or, worse,
  silently coerced to `0.0` by a library default.

This is not a hypothetical corner: `spec-budget.md` §5's own worked example is exactly the failure
case —

> `"dimension": "unique × rungBand", "shape": "uniform", ... "evenness": { "pielouMin": 0.90 }`

— if a healthy corpus regresses to "all uniques in one band," `S` drops to 1 and the metric that is
supposed to scream about it instead throws or returns `NaN`. And `spec-metrics.md` §6 rules out the
easy fix of leaning on another metric to pre-screen the empty case: *"Metrics are pure ... a metric
that needs another metric's output is a design error"* — so Distribution/evenness cannot assume
Coverage already caught `S = 0`; it has to guard itself, and no guard is specified anywhere in
either document.

The same document (§1.2 caveat block) is careful to warn that "evenness and richness fail
independently" and that a target needs calibration before gating — good instincts that stop one
step short of the actual crash case. Needs an explicit convention (e.g. `S ≤ 1 ⇒ J := 0`, `S = 0 ⇒
NOT_MEASURED`) stated in the spec, not left to whoever implements it to discover by exception.

---

## 3. Gini's ceiling depends on `n` — comparing it across dimensions of different cardinality is not apples-to-apples

**Severity: MAJOR**

`spec-analytics.md` §1.3 gives the standard rank-based Gini formula and argues Gini and Pielou
"disagree usefully" and that "reporting both costs nothing." True for a *single* dimension. But
Gini's maximum achievable value is bounded by group count: for `n` categories, all mass in one
cell gives

```
G_max(n) = (n-1)/n
```

(derivable directly from the cited formula: only the top-ranked term survives, `(2n-n-1)·x_n =
(n-1)·x_n`, over `n·x_n`). For the 15-role dimension the ceiling is `14/15 ≈ 0.933`, not 1 — and for
a dimension with a different cardinality (band×element, kind, etc.) the ceiling is different again.
The spec's own catalogue mixes dimensions of very different sizes (§2 map: "role, or role×frame, or
band×element"), and nowhere does it normalise Gini by `G_max(n)` before treating two Gini numbers
from different dimensions as comparable severity signals. A role-dimension Gini of 0.80 (out of a
possible 0.933) is far more concentrated than a 70-cell dimension's Gini of 0.80 (out of a possible
0.986) — same number, different meaning — and the spec's own selling point for Pielou over raw
entropy ("normalised — comparable across dimensions of different sizes, which raw entropy is not")
is never extended to Gini, which needs exactly the same correction and doesn't get it.

This matters specifically because 15 (roles) is one of the smallest dimensions in the catalogue —
precisely the case the reviewer's brief flagged, and precisely where the uncorrected ceiling bites
hardest.

---

## 4. `baseShare`'s derivation plugs a midpoint into a convex formula — Jensen's inequality says that understates the true expected gain

**Severity: MAJOR**

`spec-numerics.md` §6 solves for `baseShare` using:

```
gain_per_channel = (SLOTS × affixesPerItem / effectiveChannels) × baseShare × r^(meanTier - 1)
```

I reproduced the published table independently from `core.v1.json`'s actual `countBand` /
`tierWindow` values, taking `affixesPerItem` and `meanTier` as the **midpoints** of each rung's
`countBand` and `tierWindow` (e.g. fused: countBand {2,3}→2.5, tierWindow {2,4}→3): the arithmetic
reproduces all six published multipliers (1.28×, 1.80×, 2.13×, 2.97×, 4.35×, 5.09×) at
`baseShare = 35‰` to two decimal places. **The stated arithmetic is correct as a computation.** The
objection is to the model, not the sum.

`r^(meanTier-1)` is a convex function of tier. Tier is explicitly drawn from a *window*
(`tierWindow {min, max}`), i.e. it varies — that's what a window means. By Jensen's inequality, for
any non-degenerate distribution of tier across that window:

```
E[r^(T-1)] ≥ r^(E[T]-1)
```

with equality only if tier never actually varies. Plugging the **midpoint** tier into the formula
computes `r^(E[T]-1)`, the *lower* bound, not `E[r^(T-1)]`, the true expected gain. Concretely, for
chimeric's tierWindow {2,4}, assuming even a simple uniform draw over {2,3,4}:

```
E[r^(T-1)] = (r^1 + r^2 + r^3)/3 = (1.75 + 3.0625 + 5.359375)/3 ≈ 3.39
r^(E[T]-1) = r^2 = 3.0625
```

— an ~11% gap, growing with window width and with `r`. (`affixesPerItem` is fine to midpoint-summarise:
it enters the formula linearly, and the mean of a linear function equals the function of the mean —
no Jensen bias there. Only the exponentiated `meanTier` term is affected.)

Consequence: the table's multipliers are **systematically biased low** relative to what a
non-degenerate roll distribution actually produces, and the spec leans on the table's precision to
make a specific choice — *"the solver gives 36.6‰ for exactly 4.5×; 35 is the round number one
notch under, and lands at 4.35×"* — that precision is not real if the true expected multiplier at
`baseShare = 35` is already closer to 4.5–4.8× once the bias is corrected. The fine distinction
between 35 and 36.6 that the spec treats as a deliberate, reasoned choice may not survive contact
with the actual roll distribution. Not fatal — `solve_base_share` is explicitly kept live and
`effectiveChannels` is already flagged as the "one soft assumption" — but the meanTier-midpoint
substitution is a second soft assumption that isn't flagged at all, and it biases in a known
direction.

---

## 5. The `baseShare` gain model has no term for op-type mix, despite the same spec insisting Flat/Increased/More combine differently

**Severity: MAJOR**

`spec-numerics.md` §2.2 is explicit that operations do not combine the same way:

> `opWeight[Increased] | 1.0 | additive with other Increased; same value per point`
> `opWeight[More] | 0.55 | multiplicative, so it compounds where Increased dilutes.`

But the §6 `gain_per_channel` formula that derives `baseShare` contains no `opWeight` term at all —
it sums `SLOTS × affixesPerItem / effectiveChannels` affixes on one channel as if every one of them
combines the same (implicitly additive) way. Two consequences:

- If any of the affixes counted are `More`-type, the true combination is multiplicative
  (`Π(1 + opWeight·m_i)`, roughly), which is **not** equal to the linear sum used, and diverges more
  as stack count grows — exactly the property §2.2 exists to describe.
- Because the formula never touches `opWeight`, the derived 35‰ implicitly assumes an affix mix
  that is either 100% Flat/Increased-equivalent, or that the mix doesn't matter — a claim the spec
  never states and the corpus's actual op-type distribution (unmeasured, per the spec's own
  admission that `sharePermille`'s underlying tier-bands artefact "does not exist yet") cannot
  currently confirm or refute.

This is the direct case the reviewer's brief asked about: **the model does conflate Flat/Increased
and More**, in the one place (the global balance constant) where getting that conflation wrong has
the widest blast radius.

---

## 6. `opWeight[More] = 0.55` gets none of the derivation rigor the spec demands of `baseShare`

**Severity: MAJOR**

Contrast the treatment of the two constants in the same document. `baseShare` gets an entire
section (§6) titled *"Deriving `baseShare` instead of guessing it,"* with a named target
(4.5× naked), a solver (`solve_base_share`), and an explicit table of alternatives. `opWeight[More]`
(§2.2) gets one line:

> `"More" | 0.55 | multiplicative, so it compounds where Increased dilutes. ≈ 1/1.8. Less magnitude
> for the same AE."`

No stacking model, no worked example, no statement of what equivalence criterion 0.55 satisfies —
just "≈ 1/1.8" with no indication of where 1.8 comes from (it is not derived from any other locked
constant in the document; `r = 1.75` is close but not cited as the source). This is the exact
failure mode P1/P2 exist to prevent elsewhere in this same spec set — *"a number [with] no
calibrated sense of scale... a plausible-looking guess that survives review because nothing looks
wrong with it"* (map §2, P1) — applied here to a human author instead of a model, which the
principle doesn't exempt.

There is also a structural reason a single scalar cannot fully do this job: a constant discount can
equalize one `More` affix against one `Increased` affix at a chosen stack size, but multiplicative
and additive stacking diverge as stack count grows (`(1+0.55y)^N` vs. `N·(1+y)` are tangent at one
`N`, not equal for all `N`). Since `SLOTS = 15` and multiple affixes can land on the same channel
(the same premise §6 uses to build `gain_per_channel`), the AE-parity `opWeight[More]` is meant to
guarantee is only exact at whatever stack size it was implicitly tuned against — unstated — and
drifts at every other stack size, worst exactly at the high-affix-count endgame rungs where
`baseShare`'s own target (4.5×) is calibrated.

---

## 7. "No significance testing anywhere in this design" is right for the corpus census, overstated as a blanket rule

**Severity: MAJOR**

`spec-analytics.md` §0's core argument is sound and worth affirming rather than attacking: for a
count like "144 uniques," there is no sampling error because the corpus is enumerated exactly, not
estimated from a draw — a chi-square or confidence interval on that count really would answer a
question nobody asked. The supporting detail (chi-square's `expected ≥ 5` rule of thumb, "plenty of
these cells hold 2 or 3") is also correctly stated.

But the conclusion is written as unconditional: *"no significance testing anywhere in this
design."* `spec-numerics.md` §4 — in the same document set, explicitly building on `spec-analytics`
— describes a subsystem where sampling inference is not just legitimate but necessary:

> "Marginal contribution of each channel to win probability — logistic regression of outcome on
> per-channel magnitudes, giving a coefficient vector..."

Battles are not a census of some fixed population the way the item corpus is; they are draws from
an ongoing, noisy process (player choices, RNG combat outcomes), and a fitted logistic-regression
coefficient **is** a sample statistic with sampling variance. §4 already anticipates two of the
three failure modes that flow from ignoring that variance (confounding by availability,
collinearity between correlated channels) but never states the third: with no standard errors or
confidence intervals on the fitted `channelWeight` coefficients, a "refit" cannot distinguish a real
channel-strength signal from noise in a small or unbalanced battle sample — which is exactly the
scenario "confounding by availability" already flags as a risk (rare channels see few battles).
Some measure of estimation uncertainty (standard errors, a minimum-sample-size gate per channel
before it's eligible for refit, or shrinkage toward the uniform prior) belongs in §4, and the
"no significance testing anywhere" framing in §0 reads as though it forecloses that, when the two
sections are describing different statistical situations (population census vs. sampled process)
that call for different tools.

**Where sampling inference is legitimate — direct answer to the brief's question:** (a) the
telemetry refit above, and (b) MinHash itself (see finding 9) — both draw a sample (of battles; of
hash functions) to estimate something that is not directly observed, and both therefore carry
honest sampling error that the "population, not sample" framing doesn't have a slot for.

---

## 8. PAVA: "the points PAVA pooled ARE the inversions, by construction" is not strictly true

**Severity: MAJOR**

`spec-analytics.md` §5: *"isotonic regression via PAVA ... fits the closest monotone non-decreasing
sequence to the observed one; the points PAVA had to pool **are** the inversions, by construction."*

PAVA is the right tool for the stated job — that part is correct, standard, and O(n) as claimed.
The "pooled = inversions" claim is the part that doesn't hold in general, because PAVA's cascading
merges compare against the *evolving block average*, not the *original adjacent raw values* — a
later point can be swept into a pool purely because the pool's average (already pulled down by an
earlier violation) now exceeds it, even though that later point was never in a raw pairwise
violation with its own neighbour. Concrete counterexample, values `[10, 1, 5]`:

- Raw pairwise check: `(10,1)` violates (10 > 1). `(1,5)` does **not** violate (1 < 5 — a perfectly
  fine increase).
- PAVA: pool `{10,1}` → both become `5.5`. Compare the new block (5.5) against the next point (5):
  `5.5 > 5`, a **new** violation is triggered — one that did not exist in the raw data — so `5` gets
  pooled too. Final: all three points merge to `5.33`.

The point with raw value `5` is reported as "pooled" (its fitted value moved from 5 to 5.33) despite
never being part of an actual rank inversion in the source data — it was swept in by the cascade.
Applied to the actual use case (§5's own example, `verdant-graft-90` reading flatter than
`verdant-graft-50`): if a genuinely-broken low rung drags a genuinely-fine higher rung into the same
pool, the finding as currently specified — "report [the pooled points] as the finding... rather than
a correlation coefficient nobody can act on" — would name the fine rung as part of the problem
alongside the actually-broken one. The fix isn't to drop PAVA (still the right tool for the
monotone-fit itself); it's to report the *raw* pairwise violations directly (a strictly cheaper,
exact O(n) scan) rather than treating pool membership as a proxy for "was itself out of order," or
to explicitly flag partial/cascaded pool members differently from the pool's originating violation.

---

## 9a. MinHash/LSH is solving a problem the current corpus doesn't have yet

**Severity: MAJOR**

`spec-analytics.md` §6.2 and §10's complexity table both justify LSH banding as necessary to avoid
"the O(n²) all-pairs comparison at 1,400+ names," and §10 calls it "the only [analysis] needing
care." At the corpus's actual current size (~1,438 entries), `n² ≈ 2.07 million` pairs. Computing
exact Jaccard similarity between 5-gram shingle sets for ~2 million pairs of short strings (item
names, a few characters of shingles each) is not a performance problem on any machine this runs on
— it is milliseconds-to-low-seconds of work even without vectorisation, and trivially parallel if it
weren't. The complexity table's framing conflates two very different scales in one line: the current
corpus (~1,500, `n² ≈ 2M` — cheap) and the stated future target (~30,000 generated rows, `n² ≈ 900M`
— where exact all-pairs comparison genuinely would need care).

This is not a claim that LSH is the wrong tool for the eventual scale — it may well be the right
one at 30k rows. It is that introducing MinHash/LSH's approximation machinery (signature width,
banding parameters `b`/`r`, an accuracy/recall trade-off that has to be tuned and validated) **now**,
justified by a cost that only exists at a corpus 20× the current size, trades a simple, exact,
easy-to-test brute-force comparison for an approximate one before the approximation is needed. The
spec gives no signature length, no `b`/`r` values, and no stated target false-negative rate for the
near-duplicate check it's meant to run today — because, at today's scale, there's no forcing
function requiring any of that to be decided yet.

## 9b. MinHash's randomness isn't pinned down, and the spec's own opening claim says it should be

**Severity: MAJOR**

`spec-analytics.md` opens: *"Every algorithm here is deterministic and dependency-free... every
function is a pure function of the corpus and the budget."* MinHash signatures are computed from a
family of hash functions (or permutations) that is conventionally drawn pseudo-randomly; nothing in
§6.2 says this family is fixed and seeded. Contrast §8 (Sampling), three sections later in the same
document, which is careful about exactly this: *"Seed the PRNG from a stable key (`metric id +
corpus revision`) so the same sample is reproducible."* §6.2 needs the identical treatment — a fixed
hash-function seed, stated once, reused every run — and doesn't get it. Without it, two runs over an
unchanged corpus can produce different candidate pairs from LSH banding, which is exactly the
"flaky gate" failure mode `spec-numerics.md` §9.3 warns about for float equality, arriving here by a
different route (unseeded pseudo-randomness rather than float rounding) that the doc doesn't
recognise as the same class of problem.

---

## 10. Largest-remainder: the wrong paradox is dismissed, the relevant one is never mentioned

**Severity: MAJOR**

`spec-numerics.md` §9.2 and `spec-budget.md` §4.3 both use Hamilton's largest-remainder method and
both correctly describe it (floor everything, hand remaining units to the largest fractional
remainders, sum is exact by construction — all correct). The dismissal of its known pathology:

> "Its known pathology, the **Alabama paradox** — increasing the total can *decrease* an individual
> allocation — is real and irrelevant here, because the total is a fixed design constant rather
> than something that grows."

Correct, as far as it goes: the Alabama paradox is specifically triggered by changing the *total*,
and if the total truly never changes, that specific scenario can't occur. But largest-remainder has
a second, independent pathology that this dismissal doesn't cover: the **population paradox** — a
category's *share increasing* can still cause its integer allocation to *decrease* (or another
category's to increase) purely from how remainders happen to fall, with the total held fixed. This
is triggered by *weight* changes, not total changes — and weight changes are exactly what this
system's own versioning anticipates:

> `spec-budget.md` §6: "`budget diff v1 v2` reports which targets moved..."

Revising `budgetWeightMilli` between budget versions (the stated mechanism for correcting a
proportional target, §4.3) is a population-paradox trigger, not an Alabama-paradox trigger. The
doc's own escape hatch — "If the total ever does become dynamic, switch to Sainte-Laguë, which is
divisor-based and immune" — is worth noting as incomplete rather than wrong: Sainte-Laguë (and
divisor methods generally) are immune to **both** the Alabama and population paradoxes, which is
exactly why apportionment theory prefers them when monotonicity matters more than exact-quota
(Balinski–Young: no method gets both). Since population-paradox is the realistic risk here, not the
one named, this is worth being explicit about rather than leaving a reader to assume "we checked
the paradoxes for this method" when only one of the two relevant ones was checked.

---

## 11. Spearman rank correlation doesn't address ties, and the design guarantees ties

**Severity: MINOR**

`spec-analytics.md` §5 uses Spearman rank correlation between rarity ordinal and resolved power,
"want ≈ +1." Standard, rank-based, unit-independent — correct choice. But `spec-numerics.md` §9.3
locks in "integer-only output" for every resolved magnitude, and the tier/rung structure is coarse
(a handful of rungs, `countBand`/`tierWindow` a few integers wide) — meaning many entries will
resolve to **identical** integer power values by construction, not by accident. Untied Spearman
silently uses whatever tie-breaking the implementation defaults to (often index order), which can
inflate or deflate the coefficient; the standard fix (average ranks + tie correction, or equivalent
to Pearson on ranks) isn't mentioned. Worth a one-line note in the spec given how likely ties are
here specifically, versus in a typical continuous-data Spearman use case.

---

## 12. Hopcroft–Karp assumes unit-capacity slots — never stated as an assumption

**Severity: MINOR**

`spec-analytics.md` §3 and `spec-planner.md` §2.2 both apply Hopcroft–Karp (correctly described:
`O(E√V)`, correct as the way to test Hall's condition without enumerating subsets) directly to the
demand↔slot graph. Hopcroft–Karp is specifically an algorithm for **unit-capacity** bipartite
matching. The worked incident (8 roles × 5 axes = 40 slots, "one demand per (role,axis) cell") is
consistent with unit capacity, and the Latin-square construction in the same section implies each
cell holds exactly one assignment — so the specific case is fine. But both sections present this as
the general feasibility mechanism for "the planned allocation" broadly, without stating that a slot
with capacity > 1 (plausible for a future allocation, e.g. "this role can hold up to 3 uniques of
different axes") needs either node-splitting into unit copies or a generalisation to capacitated
max-flow before Hopcroft–Karp applies unmodified. Not wrong for the documented case; understated as
a general tool.

---

## 13. König's theorem names the right structure; turning it into "these six pairs are the bottleneck" needs one more step the spec doesn't show

**Severity: MINOR**

`spec-analytics.md` §3 / `spec-planner.md` §2.3: "via König's theorem, the minimum vertex cover
names *which* constraint binds." König's theorem is the right theorem — in a bipartite graph, max
matching size equals min vertex cover size, and that's what turns "infeasible" from a boolean into
something with structure. But the *cover itself* is a mixed set of demand- and slot-side vertices,
not directly "the demand subset that's starved and the slots it's competing for" in the form the
worked example promises ("these six (role, axis) pairs are the bottleneck; either widen the role
allocation or reduce per-partition count to 8"). Producing that specific, readable statement from a
maximum matching requires the standard alternating-path construction (BFS/DFS from unmatched
demand-side vertices in the residual graph to find the Hall-violating set and its neighbourhood) —
closely related to König's theorem but a distinct extra step that isn't named or sketched anywhere
in either spec. Low risk (the theory is sound and the missing step is standard), but an implementer
reading only these specs would not know how to go from "matching size" to "named bottleneck pairs."

---

## 14. The cyclic Latin square construction needs roles and axes to be the same count — the actual incident wasn't square

**Severity: MINOR**

`spec-analytics.md` §3 / `spec-planner.md` §2.4: "the cyclic Latin square `axis = (roleIndex +
themeIndex) mod n` is a closed-form solution and needs no search," citing "n themes each needing one
of n axes per role." This construction is a genuine Latin square — for a fixed role, cycling over
theme gives every axis exactly once, and symmetrically for a fixed theme over role — **when there
are exactly `n` roles and `n` axes.** The incident actually described in the map and both specs is
`8 roles × 5 axes` (`5 themes × 15 uniques` competing for `8 roles × 5 axes = 40 slots`) — role count
(8) ≠ axis count (5), a non-square grid. The doc states the closed-form solution using the
square-case language ("n roles, n axes") without explaining how it was actually adapted to the
non-square 8×5 case that motivated writing this section in the first place, and the "144
collision-free triples" result cited (`spec-planner.md` §2.4) uses a different total (144, the
current unique count) than the 75-into-40 failure case, so the two numbers describing "what
actually happened" don't obviously correspond to the same `n`. Not necessarily wrong — there are
standard ways to adapt a Latin square to a rectangular grid (e.g. modulo the smaller dimension, or a
Latin rectangle) — but the spec presents the closed form as though the documented example is an
instance of it, without showing the adaptation.

---

## 15. Float-vs-integer boundary: `relative_c` is a float ratio feeding what may be a gating comparison

**Severity: NOTE**

`spec-analytics.md` §1.1: `relative_c = (o_c - e_c) / e_c` — ordinary floating-point division.
`spec-numerics.md` §9.3 states a general project discipline: "no float reaches a seed file **or a
comparison**... Float equality in a validator is how a gate becomes flaky" — but that section is
scoped to `numerics`, and it's left unstated whether `metrics`' distribution/tolerance comparisons
(which do gate, per `spec-metrics.md` §1) are held to the same rule. In practice the risk here is
smaller than a float-equality bug: these are inequality checks against a tolerance band, not
equality checks, and the zero-tolerance case (`spec-budget.md` §3) is safe because `o_c = e_c` gives
an exact `0.0` numerator with no rounding involved. Still worth a one-line statement of policy
(compute the comparison in exact integer arithmetic — `abs(o_c - e_c) × 1000` vs. `tolerance_permille
× e_c` — given the tolerance is itself declared as a per-mille integer) so the "no float in a
comparison" rule doesn't quietly stop at the `numerics` module boundary.

---

## What's actually sound (said plainly, not as a courtesy)

- **§6's `baseShare` table is honestly and correctly reproducible.** Independently recomputing it
  from `core.v1.json`'s real `countBand`/`tierWindow` values (using the stated midpoint convention)
  matches all six published multipliers to two decimal places. Whatever the objection in finding 4,
  the stated model was not misapplied — the arithmetic is exactly what it claims to be.
- **"The corpus is a population, not a sample" is the right call for every closed-loop
  coverage/distribution/balance metric**, and the chi-square rejection's supporting detail
  (`expected ≥ 5` per cell) is correctly stated, not just gestured at.
- **The Benford's law rejection (§11) is correctly reasoned** — authored, designed values are not
  the naturally-occurring multi-scale magnitudes the law describes, and applying it would flag
  intentional design as anomalous.
- **Hopcroft–Karp / Kahn's topological sort / Tarjan's SCC / largest-remainder are each the right
  standard tool for their stated job**, correctly described in complexity and in the property each
  one proves, modulo the scoping caveats in findings 10, 12, 13, 14 above.
- **The complexity budget table (§10) is right about everything except the one line it calls out as
  needing care** (finding 9a) — Kahn/Tarjan, PAVA, pairwise coverage and per-cell deviation are all
  correctly bounded at both the current and projected corpus scale.
