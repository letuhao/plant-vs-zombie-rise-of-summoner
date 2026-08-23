# Seedsmith — the algorithmic core

**Status:** Proposed 2026-08-23. Specs the maths shared by `numerics` and `metrics`
([seedsmith-map.md](../seedsmith-map.md) §3). Nothing is built.

Every algorithm here is deterministic and dependency-free — Python stdlib plus, at most, integer
arithmetic. No model is consulted, no floating-point value reaches a seed file, and every function
is a pure function of the corpus and the budget.

---

## 0. The one framing decision everything else follows from

**The corpus is a population, not a sample.**

This sounds pedantic and it changes half the toolkit. When you have 144 uniques, you are not
*estimating* how many uniques there are from a sample — you have all of them. There is no sampling
error, so there is no null hypothesis, and a p-value would be answering a question nobody asked.

So: **no significance testing anywhere in this design.** No chi-square goodness-of-fit, no t-tests,
no confidence intervals on counts. Those are tools for inferring a population from a sample, and
applying them here would produce authoritative-looking numbers that mean nothing. (A chi-square over
role counts would also violate its own assumptions — it wants expected ≥ 5 per cell, and plenty of
these cells hold 2 or 3.)

What replaces them: **effect size against a declared tolerance.** `budget` states the target and the
tolerance; a metric reports the deviation and whether it is inside the band. That is a statement
about the content, not about a hypothetical population it was drawn from.

---

## 1. Distribution — is anything over- or under-represented?

Three layers, cheapest first. Each answers a different question and the middle one is the one people
usually skip.

### 1.1 Per-cell deviation — *where* is it wrong

For each cell *c* (a kind, or a role×frame, or a band×element) with observed count `o_c` and
budgeted count `e_c`:

```
deviation_c      = o_c - e_c
relative_c       = (o_c - e_c) / e_c          # e_c > 0
```

Report cells outside `budget`'s declared tolerance. Complexity O(n). Actionable, and it names the
partition to fix.

**Where `e_c = 0`** (nothing was budgeted) any content is unbudgeted content — a different finding
(`UnbudgetedCell`), not an infinite ratio.

### 1.2 Evenness — *is the spread healthy at all*

Per-cell deviation cannot answer "are uniques lumpy across roles?" because every cell can sit inside
tolerance while the shape is still wrong. This is a diversity question, and ecology has spent a
century on exactly this problem — counts of things across categories — so the indices are borrowed
rather than invented.

Let `p_i = o_i / Σo` over the cells of one dimension.

| Index | Formula | Reads as |
|---|---|---|
| **Shannon entropy** | `H = -Σ p_i · ln p_i` | information content; 0 when everything is one cell |
| **Pielou evenness** | `J = H / ln(S)`, S = cells occupied | **the headline number**: 1.0 is perfectly even, 0 is total dominance |
| **Simpson dominance** | `D = Σ p_i²` | probability two random picks land in the same cell |
| **Richness** | `S` | how many cells are occupied at all |

`J` is the one to report because it is normalised — comparable across dimensions of different sizes,
which raw entropy is not. Richness `S` is reported beside it because **evenness and richness fail
independently**: content spread perfectly evenly across three of fifteen roles scores `J = 1.0` and
is still badly broken. Reporting either alone is how a lopsided corpus passes.

Worked, on the real defect from the wave-2 review — humanoid uniques half as common as plant across
four roles: per-cell deviation flags four cells, `J` on the frame dimension drops below its target,
and the two findings together say *both* which cells and how bad the shape is.

### 1.3 Inequality — *how concentrated*

**Gini coefficient** over the sorted count vector, with the **Lorenz curve** as its picture:

```
G = ( Σ_i (2i - n - 1) · x_i ) / ( n · Σ_i x_i )     # x sorted ascending, 1-based i
```

Gini and Pielou disagree usefully. Pielou is entropy-based and reacts strongly to the *number* of
occupied cells; Gini is rank-based and reacts to *how skewed the tail is*. A corpus with one
enormous role and fourteen equal small ones scores poorly on Gini and only moderately on `J`.
Reporting both costs nothing and the disagreement is informative.

**Caveat that must ship with these:** a diversity index needs a target like any other metric (P2).
"Pielou 0.72" is not a verdict. `budget` declares the acceptable band per dimension, and the honest
default for a first run is *measure, do not gate* — collect the numbers, look at them, then set the
band from what a healthy corpus actually looks like.

---

## 2. Coverage — is anything simply absent?

### 2.1 Allocation coverage

Set difference between allocated partitions and partitions holding ≥1 entry. This is the check that
would have caught all nine empty partitions on day one. O(n), trivial, and its absence cost three
waves.

### 2.2 Combinatorial (t-way) coverage

Content lives in a cross-product: role × frame × band × element × rarity × class. Full cartesian
coverage is neither achievable nor desirable — 15 × 2 × 4 × 7 × 10 × … is far more cells than the
corpus should have.

Borrow from **combinatorial testing**: the empirically useful target is not full coverage but
**pairwise (2-way) coverage** — every *pair* of values from every *pair* of dimensions co-occurs at
least once. The software-testing literature is consistent that most interaction defects are found by
2-way and the returns fall off sharply after 3-way; the same logic applies to content holes, because
a player notices "there is no fire jewellery" long before they notice a missing 5-way combination.

```
for each pair of dimensions (A, B):
    required = {(a, b) for a in values(A) for b in values(B) if legal(a, b)}
    seen     = {(entry[A], entry[B]) for entry in corpus}
    missing  = required - seen
```

Report `|seen| / |required|` per dimension pair, and list the missing pairs. O(n · d²).

`legal(a, b)` matters and comes from `adapter-items`: `ward-array` × hybrid is illegal by
`hybridEligible`, uniques × `jewel-minor` is banned by ssot-uniques §3.7. Counting illegal pairs as
holes would generate permanent false findings — the single most likely way for this metric to become
noise everybody ignores.

This catches the wave-2 finding *"the top rarity band has zero fire/ice/air/earth uniques"*
mechanically: it is a missing set of (band, element) pairs.

---

## 3. Feasibility — can the plan be satisfied *before* anyone runs?

The most expensive failure in the agentic build: 5 themes × 15 uniques = 75 entries competing for
8 roles × 5 axes = 40 slots. Eighteen agents faithfully executed an allocation that arithmetic
forbade. This is a **bipartite matching problem** and it is exactly decidable.

**Layer 1 — pigeonhole.** `Σ demand > Σ capacity` ⇒ infeasible. O(n). Catches the 75-into-40 case
instantly and would have caught the real one.

**Layer 2 — Hall's condition.** Pigeonhole misses *local* starvation: totals fit, but one subset of
demands can only use a too-small subset of slots. Hall's marriage theorem is the exact criterion — a
perfect matching exists iff every subset `S` of demands satisfies `|N(S)| ≥ |S|`, where `N(S)` is
the slots they can reach. Checking all subsets is exponential, so do not; instead:

**Layer 3 — max bipartite matching, Hopcroft–Karp**, O(E√V). If the maximum matching is smaller than
the demand, it is infeasible, and — via **König's theorem** — the minimum vertex cover names *which*
constraint binds. That last part is what makes the finding actionable rather than a shrug: not
"infeasible" but "these six roles are the bottleneck".

**Construction, when feasible.** For the balanced case the corpus actually has — n themes each
needing one of n axes per role — the cyclic Latin square `axis = (roleIndex + themeIndex) mod n` is
a closed-form solution and needs no search. That is exactly what was used by hand; `planner` should
emit it, verify it, and never ask a model to allocate.

---

## 4. Dependency order — derive the stages, stop hand-labelling them

Kinds were hand-tagged `1a`/`1b`/`1c`, and drop tables were labelled `1c` while referencing uniques
that were also `1c` — 274 errors, fixed by relabelling to `1d`. Hand-maintained ordering drifts from
the graph it describes the moment anything is added.

Derive it instead. Build the kind-level reference graph (kind A references kind B), run **Kahn's
topological sort**, and the resulting layers *are* the stages. O(V + E).

- A **cycle** means two kinds reference each other and no ordering exists — report it with the cycle
  path (**Tarjan's SCC**, O(V+E), gives the offending component directly).
- The derived layer for `drop-table` would have been "after uniques" automatically, the moment the
  first drop table referenced one, with no label to forget to update.

---

## 5. Monotonicity — does the power ladder actually climb?

Wave 2 found `verdant-graft-90` reading flatter than its own `verdant-graft-50`. Rarity ordinal
should predict resolved power monotonically.

**Spearman rank correlation** between rarity ordinal and resolved power gives one headline number
(want ≈ +1) and is rank-based, so it does not care about the units power is measured in.

But a correlation says the ladder is *mostly* right and never says *which rung* is wrong. For that,
**isotonic regression via PAVA** (pool-adjacent-violators, O(n)) fits the closest monotone
non-decreasing sequence to the observed one; the points PAVA had to pool **are** the inversions, by
construction. Report those as the finding — "band 90 sits below band 50" with both numbers — rather
than a correlation coefficient nobody can act on.

---

## 6. Near-duplicate detection — two names, one idea

The existing canonical-word-set normalizer already catches *"Ashen Fang"* vs *"Ashfang"* vs *"Fang
of Ash"*. It cannot catch *"Rotwake"* vs *"Blightrise"*, and it cannot catch eleven names that are
all some variation on dark-and-decaying. Three deterministic layers, no model:

**6.1 Exact and canonical** — hash of the canonical token set. Already shipped in C#.

**6.2 Lexical near-duplicates** — character 5-gram shingles → **MinHash** signatures → **Jaccard**
similarity, with **LSH banding** to avoid the O(n²) all-pairs comparison at 1,400+ names. Flags pairs
above a threshold. Standard, cheap, and finds *"Sapvein"* vs *"Sapveil"*.

**6.3 Conceptual clustering — and the assumption that did not survive checking.**

The first draft of this spec claimed the word pools already group vocabulary by concept, so entropy
over pool membership would catch *"twelve words for hard and old"* with no model. **That is wrong,
and the audit caught it.**

The pools group by **where a word may be used**, not by what idea it expresses.
`classRungAdjectivePools["armour.humanoid.plate"]` holds *soldered, rigid, ponderous, beaten* — a
construction method, a stiffness, a weight and a damage state, in one pool. The actual failure case
is worse than neutral: nine adjectives all meaning hard-and-old would be drawn from the *same* pool,
so pool-entropy would read **0 — its healthiest possible value — precisely when the partition is at
its most repetitive.** A metric that is confidently wrong in exactly the case it was built for is
worse than no metric.

**What the fix actually requires: one new field of authored data.** The concept axes already exist
in prose — the wave-1 brief names them (*age, origin, condition, colour, provenance, use, damage,
growth stage*) — they were simply never encoded. So add `axis` to each adjective's canonical entry:

```json
{ "canonicalId": "ponderous", "axis": "weight",
  "surfaceForms": { "adjective": ["Ponderous"] } }
```

**Scope: 516 words.** Of 1,293 canonical entries, 516 carry an adjective surface form; nouns need no
axis because the pools already keep head-nouns disjoint by role. A bounded, one-time job against a
closed vocabulary — and a registry addition, so it needs an owner bump.

**Once the axis exists**, the metric is Shannon entropy over *axes* within a partition (§1.2's
index, different population), and it does what was originally claimed: nine adjectives spanning one
axis flags, nine spanning six does not.

**Until it exists, this metric does not ship.** It is listed as blocked on the registry addition
rather than implemented against pool membership, because implementing it against the wrong grouping
would produce a green light on the exact defect it is meant to find. Name clustering stays open-loop
and human-sampled (§8) in the meantime.

---

## 7. Text quality — screening only, never a verdict

For flavour text, deterministic proxies exist and must be labelled for what they are.

- **Type–token ratio** and **hapax rate** — vocabulary richness. Length-sensitive, so use a
  standardised variant (fixed-window MATTR) rather than raw TTR across texts of different lengths.
- **Character n-gram entropy** — flags template-filled text where only one word varies.
- **Missing / empty** — the trivially checkable one, and the one that actually mattered: 60
  consumables with no flavour at all.

**These measure variety, not quality.** A high-TTR sentence can be nonsense, and a beautiful line can
score low. They are screening heuristics that decide *what a human should read first*, which is why
every one of them is open-loop under P3 and feeds §8 rather than a pass/fail gate.

---

## 8. Sampling — how a human gets a verdict cheaply

Open-loop metrics need eyes. Simple random sampling is the wrong tool: with 18 unique partitions, a
random 10 will over-represent whichever partitions are largest and may miss a whole rung band.

Use **stratified sampling** — partition the population by the dimension that matters (kind, then
band), allocate the sample across strata, draw within each. Two allocation choices, both worth
having:

- **Proportional** — sample size per stratum ∝ stratum size. Answers "what does the corpus look
  like on average?"
- **Neyman / equal** — over-sample small strata. Answers "is any *part* of this bad?", which is
  usually the actual question, since a corpus fails in its neglected corners.

Seed the PRNG from a stable key (`metric id + corpus revision`) so **the same sample is reproducible**
— a reviewer can re-read exactly what they read last week and diff their own judgement. Unseeded
sampling makes review unrepeatable and quietly worthless.

---

## 9. `numerics` — resolving bands to numbers

P1's home. Three problems, each with a standard solution and a specific failure mode to avoid.

### 9.1 Band → magnitude

Power bands are ordinal (`low`, `medium`, `high`, …) and must resolve to integers. Game power
ladders are multiplicative, not additive — the step from tier 4 to 5 should feel like the step from
1 to 2 — so the base curve is **geometric**: `value = base · growth^(tier-1)`, with `base` and
`growth` declared per channel family in the registry, never per entry.

Where a channel needs a shape a geometric curve cannot express (diminishing returns on resistances,
say), interpolate the authored `curve` points with **PCHIP** — piecewise cubic Hermite, monotone and
**shape-preserving**. Not a natural cubic spline: a natural spline overshoots between points, which
on a power curve means a tier that is silently stronger than the tier above it. Monotonicity is not
a nicety here, it is the property §5 checks for.

### 9.2 Budget apportionment — the one that quietly corrupts data

Role weights sum to 1000‰ and base stats are integers per-mille. Naive `round(total · weight)` per
role does **not** sum back to the total — rounding drift — so the budget silently gains or loses
points, and every downstream balance check inherits the error.

This is the **apportionment problem**, solved in the 18th century for allocating legislative seats.
Use **largest remainder (Hamilton)**: floor everything, then hand the remaining units to the largest
fractional remainders. Sum is exact by construction.

Its known pathology, the **Alabama paradox** — increasing the total can *decrease* an individual
allocation — is real and irrelevant here, because the total is a fixed design constant rather than
something that grows. Noted so the next reader does not re-litigate it. If the total ever does
become dynamic, switch to **Sainte-Laguë**, which is divisor-based and immune.

### 9.3 Integers everywhere

Per-mille integers end to end; no float reaches a seed file or a comparison. Where division is
unavoidable, round half to even and state it. Float equality in a validator is how a gate becomes
flaky.

---

## 10. Complexity budget

The whole suite must run in CI on every push, so nothing may be worse than near-linear at corpus
scale (~1,500 entries now, ~30,000 generated rows later).

| Analysis | Complexity | At 30k rows |
|---|---|---|
| Per-cell deviation, diversity indices, Gini | O(n log n) | trivial |
| Allocation coverage | O(n) | trivial |
| Pairwise coverage | O(n · d²) | trivial, d ≈ 6 |
| Topological sort, SCC | O(V + E) | trivial |
| PAVA isotonic regression | O(n) | trivial |
| Hopcroft–Karp matching | O(E√V) | fine; only over the planned allocation, not the corpus |
| MinHash + LSH | O(n · k) | the only one needing care; LSH is what keeps it off O(n²) |

---

## 11. What was considered and rejected

Naming these so they are not re-proposed later.

| Rejected | Why |
|---|---|
| **Chi-square / any significance test** | The corpus is a population, not a sample (§0). A p-value answers a question nobody asked. |
| **Benford's law** | Applies to naturally-occurring multi-scale magnitudes. Authored game values are designed, not observed; it would flag good data. |
| **Embeddings for name similarity** | Would work, but needs a model, and §6.3 gets most of the value from concept groups the registry already encodes. Revisit only if 6.3 proves too blind. |
| **Natural cubic spline for curves** | Overshoots between control points, silently breaking monotonicity (§9.1). |
| **Naive rounding for budget split** | Rounding drift; sums stop matching (§9.2). |
| **Simple random sampling for review** | Over-represents large strata and misses whole bands (§8). |
| **Full t-way coverage above t=2** | Cell count explodes and returns fall off sharply; 2-way is the established sweet spot (§2.2). |
| **Readability scores (Flesch, etc.)** | Calibrated for English prose paragraphs, not eight-word item flavour. Would produce confident nonsense. |
