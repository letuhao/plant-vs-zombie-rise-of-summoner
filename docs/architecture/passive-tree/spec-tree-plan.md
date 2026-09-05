# Spec: `tree-plan`

**Status:** spec, 2026-09-05, with owner decisions **D37–D41 folded in** the same day. Module of
[passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-plan` · **Wave:** 0 · **Depends on:** nothing · **Model calls:** none.
**Wave-0 siblings:** `squad-harness`, `mechanism-wiring`, `gate-counters` (D37).
**Consumed by:** [`tree-language`](spec-tree-language.md), [`tree-binder`](spec-tree-binder.md),
[`tree-catalog`](spec-tree-catalog.md).

---

## Objective

Stage 1 of the three-stage generator, and the only stage where balance is decided. It emits one
reproducible artifact — **the plan** — that fixes topology, the tier ladder, every tree's budget, the
shape archetypes, the node potency ceiling, the property vocabulary, and the per-node quota cells.
It emits no node text and no magnitude.

D13 states the reason this stage exists at all: *"Balance is a property of the plan, not of the
generated content."*

**The property this module guarantees, stated so it can be checked by a machine:**

> Every tree in the corpus costs the same and awards the same, in one conserved scalar, split equally
> between an offensive and a defensive branch — while no two trees need have the same shape.

**Read that as a completion property, because that is what it is.** The conserved scalar is summed
over all 40 nodes, so it is exact at tier 10 and only there. In **skill points**, mid-progression
reward per point differs across archetypes by up to **6.0×** at tier 2, decaying monotonically to
exactly 1.00× at completion. §3.1 states the gradient, bounds it (`R-A1`) and says why bounding it
tighter would cost the archetype axis. Anyone quoting the sentence above without §3.1 is quoting half
of it.

That is D15's *"equal expected value, not equal shape"*, and both halves are load-bearing. *"No tree
is OP"* is machine-checkable and is asserted by a test (§Testing, `C1`). *"Every tree feels the
same"* is a **failure mode**: it is what happens if you conserve the category vector instead of the
scalar, and this module deliberately does not.

Owner framing this answers, verbatim: *"every skill tree will cost and award same and we decide it by
math functions"* · *"there are no skill tree that so op"* (passive-tree-ideal.md §0).

---

## Design

### 1. Topology at D29's ten tiers — and the G9 correction

D29 locks **10 tiers × 2 branches, ~40 nodes per tree**. Doc 02's emit-time invariant `G9` says

```text
nodesPerTree = 2 × nodesPerBranch + 1        ← the "+1" is a shared root
```

which is **always odd**. Ten tiers at a mean width of 2 gives 20 nodes per branch, so `G9` as written
produces **41**, not 40. The audit flagged this
([10-decision-consistency-audit.md](../../research/passive-tree/10-decision-consistency-audit.md) A7,
§7.1) and left it for a spec to settle. It is settled here, and the shared root is **removed**.

**The arithmetic:**

```text
nodesPerBranch = Σ_{t=1..10} w[t] = 20        (mean width 2, D29)
with root:     2 × 20 + 1 = 41                ≠ D29's 40, and odd for every possible w
without root:  2 × 20     = 40                = D29 exactly, and even for every possible w
```

**Three reasons the root is the half to drop, not the 40:**

1. **It has no budget, and there is nowhere to get one.** The budget column is per branch —
   `tierBudget[t] = B_b · t / T_tri` — and the root belongs to no branch (doc 02 `G6`). A node with
   zero budget can bind no atom and carry no effect. It is a node the language stage must name and
   write text for, that does nothing.
2. **It gates nothing.** Doc 02 §2.1 establishes that reachability is decided by the per-tier
   threshold `req(t)`, not by edges: *"Edges are therefore layout and reading order, not gating."*
   A root above tier 1 would be a gate the runtime does not enforce.
3. **`G1` and `G6` already contradict each other with it.** `G1` requires every node to have exactly
   one tier and one branch; `G6` says the root has neither. Removing the root removes the exception
   and both invariants become universal.

**`G9`, restated. This is the version the emit gate checks:**

```text
G9   Σ_{t=1..tierCount} w[t] == nodesPerBranch  for every archetype
     nodesPerTree == 2 × nodesPerBranch          — EVEN by construction
     corpus       == n × 2 × nodesPerBranch      — exactly, not approximately
```

`G6` is restated with it: no edge crosses a branch, and **a node has no parent if and only if its
tier is 1**. Every tier-1 node is a root of its own branch.

**Resolved node count: 40 per tree, 20 per branch, 10 per branch per tier ladder.**

**Corpus, as a function of the roster rather than as a memorised figure:**

| Roster | `n` | Corpus |
|---|---:|---:|
| Closed rosters readable today (12 aptitudes + 6 elements + 21 statuses) | 39 | **1,560** |
| With a closed demon-family roster at `F = 19` (D27's shipped roster, once curated) | 58 | 2,320 |

`39 × 40 = 1,560` is the figure D29 states, and it is right for the trees the planner can name
today. It is **not** the whole of D27's roster — `F` is 0 only because no closed family roster
exists (§9). The plan emits the number it actually produced, so the difference is a one-line diff and
never a stale recollection.

The graph is a **layered DAG**, one shared reading order per branch: layers are tiers, edges run
`t → t+1` inside one branch only, no cross-branch edges, no skips. Doc 02 §2.1's reasoning is adopted
unchanged and is not re-derived here.

### 2. The tier ladder (D26) — and the flatness verified at every tier

> ## ⛔ `req(t)` is a threshold on APTITUDE POINTS. It is not a skill-point price.
>
> **This is the single most-confused line in the program, so it is stated here rather than only in
> §7, because §2 is where a downstream module reads the ladder.**
>
> | | Currency | What it does | Who spends it |
> |---|---|---|---|
> | `req(t)` | **aptitude points**, allocated to the tree's `gateQuantity` | **opens** tier `t` | the allocation screen |
> | `unlockCost` | **skill points** | **buys** one node | the tree screen |
>
> They are different currencies and the ladder reads the first. Nodes are *bought* with skill points;
> a tier is *opened* by aptitude points. `tierLadder.reqScalePoints` carries the unit **aptitude
> points** in its key, per R2.
>
> **Why this reading and not the other, argued from inside the design rather than by vote:** D12
> locks *"tier gates read base allocation, never item bonuses."* That is true **by construction**
> under the aptitude reading, because an aptitude is a SOURCE, not a registered channel — an item
> cannot feed it, so there is nothing to exclude. Under a skill-point reading D12 would need
> enforcing somewhere, and no module enforces it.
>
> **The manifest says so in data, and the emit gate refuses the alternative:**
>
> ```text
> ladder.gateCurrency = "aptitudePoints"        enum with exactly one legal value
>
> R-G0   A plan whose ladder.gateCurrency is anything other than "aptitudePoints" is REFUSED
>        at emit and at --check, naming the field. A regenerated plan that asserts a skill-point
>        gate does not ship; it exits 3.
> ```
>
> `spec-tree-resolve.md` is corrected to match this; it was the one spec reading skill points.

```text
req(t) = k · t(t+1)/2        k = 5 APTITUDE POINTS (tunable: tierLadder.reqScalePoints)
W(T)   = b · T(T+1)/2        cumulative tree power, linear per tier (D20's pairing rule, binding)
```

Computed, not quoted:

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `req(t)` | 5 | 15 | 30 | 50 | 75 | 105 | 140 | 180 | 225 | 275 |
| `W(T)/b` | 1 | 3 | 6 | 10 | 15 | 21 | 28 | 36 | 45 | 55 |
| **`W/req` (`×b`)** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** | **1/5** |

D26's claim is exact, and it is exact **because the two ladders share one index**, not because the
numbers happen to line up:

```text
W(T)/req(T) = [b·T(T+1)/2] / [k·T(T+1)/2] = b/k        the T(T+1)/2 cancels
```

The three tiers D20's superseded ladder never printed are the ones worth stating separately, since
they are new: at `t = 8`, `36b/180 = b/5`; at `t = 9`, `45b/225 = b/5`; at `t = 10`,
`55b/275 = b/5`. Reward per point is `b/5` at every depth, forever, and the cancellation does not
depend on `tierCount` — extending the ladder never breaks it.

**`req(t)` is integer-exact.** `t(t+1)` is a product of consecutive integers and always even, so
`k·t(t+1)/2` is an integer for every integer `k`. No rounding enters the ladder.

**Depth against the point supply**, computed from the shipped rate (§7): `aptitudePoints(Θ) = 3·Θ` at
commander scope, `s` = the share of that budget sitting in one tree.

| `Θ` | budget `3Θ` | `T` at `s=1` | `T` at `s=0.5` | `T` at `s=1/12` |
|---:|---:|---:|---:|---:|
| 20 | 60 | 4 | 3 | 1 |
| 50 | 150 | 7 | 5 | 1 |
| **92** | 276 | **10** | 6 | 2 |
| 100 | 300 | 10 | 7 | 2 |
| 300 | 900 | 18 | 12 | 5 |

**The sizing rule this module uses, and the convention it records.** Doc 02 §3.3 fixes
`tierCount = T_max(Θ_designTarget, s = 1)`; at `req(10) = 275` against `3Θ` that is **`Θ ≈ 92`**.
D29 quotes `Θ ≈ 170`, computed at `s = 0.542` — the share an all-in build actually holds in the
measured share vector, not a hypothetical 100%. Both are correct readings of different conventions
(finding A15). The tunable `designTarget.thetaAllIn` records the `s = 1` figure because it is the
sharper bound and it is the planner's own function, and its `_note` carries the `s = 0.542` reading
beside it so nobody re-derives one and thinks the other is wrong.

**Above `Θ ≈ 300` the ten authored tiers saturate for every build**
([16-depth-exhaustion.md](../../research/passive-tree/16-depth-exhaustion.md)). That is a real
consequence of a finite authored depth and it owes `ssot-power-scale.md` §11.10 a row (§10). It is
not a magnitude cap: nothing is refused, and growth past it moves onto the soul track, which is
uncapped by design.

### 3. Budget distribution at equal expected value (D15)

**What is conserved:**

```text
CONSERVED, identical for every tree in the corpus:
    budgetTotal      = Σ over the tree's nodes of price(node).Total     (PowerVector.Total, int points)
    budgetPerBranch  = budgetTotal / 2                                  (D6, symmetric)

NOT conserved — this is where identity lives:
    the five-category PowerVector mix inside a branch
```

`PowerVector` is an integer 5-vector and `Total` is its plain sum
(`src/FusionRpg.Core/Effects/Atoms/Power/PowerVector.cs:19-20`, `:62`). `Total` is the sanctioned
scalar read — `ContentValidation.Budget` compares exactly that against a ceiling
(`ContentValidation.cs:60-84`). It is deliberately **not** `PowerScalar.Of`, which
`ssot-power-scale.md` §10.2 row 13 marks *"Display only… Never a balance input"*.

Conserving all five categories would force a `Might` tree and a `Bulwark` tree to carry the same
offence/defence mix. That is exactly the *"every tree feels the same"* failure D15 names. Conserving
the scalar and the 50/50 branch split gives the owner's *"cost and award same"* while leaving the
mix free.

**The distribution function.** D20's pairing rule fixes the per-tier column, so it is not free:

```text
tierBudget[t] = B_b · t / T_tri ,   T_tri = tierCount·(tierCount+1)/2 ,   B_b = budgetTotal / 2
```

**The archetype weights cancel out of the sum — verified, not assumed.** `Σ_{t=1..T} t` **is**
`T_tri` by definition, so:

```text
Σ_{t=1..T} tierBudget[t] = B_b · (Σ_{t=1..T} t) / T_tri = B_b · T_tri / T_tri = B_b
```

The cancellation is an **identity in `T`**: it holds at `T = 10`, at `T = 7`, and at any other value,
because the numerator is the definition of the denominator. The width vector `w[t]` enters one level
down and never enters the sum at all:

```text
nodeBudget[t] = tierBudget[t] / w[t] = B_b · t / (T_tri · w[t])
```

So **every archetype spends exactly `B_b` per branch and exactly `budgetTotal` per tree, by
construction.** There is no post-hoc normalisation, nothing to renormalise, and the test in §Testing
asserts the construction rather than patching a drift.

**Per-mille column at `tierCount = 10`** (`T_tri = 55`), round half up, residual absorbed by the
**deepest** tier. Computed:

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | **Σ** |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| exact ‰ of `B_b` | 18.18 | 36.36 | 54.55 | 72.73 | 90.91 | 109.09 | 127.27 | 145.45 | 163.64 | 181.82 | 1000 |
| **emitted ‰** | 18 | 36 | 55 | 73 | 91 | 109 | 127 | 145 | 164 | 182 | **1000** |

The residual is 0 at ten tiers. **The rounding rule is still binding**, because it is not 0 at every
tier count and two correct-looking implementations would otherwise disagree in the last per-mille and
fail `--check` for nothing.

> ### ⛔ `nodes[].budgetShareMilli` is AUTHORITATIVE. The binder reads it and never recomputes it.
>
> **`tree-binder` reads `nodes[].budgetShareMilli` — ‰ of one branch budget — and uses it as given.
> It does not derive a share from `tierWeight(t)`, from `weightTotal`, or from any other
> reconstruction of this section's arithmetic.** It already lists the field as an input; it now uses
> it, and drops `tierWeight`/`weightTotal` entirely (R4).
>
> **This closes a live defect, not a hypothetical one.** The binder distributed budget ∝ `w[t]·t`
> while this module distributes ∝ `t` — the width vector enters one level *down*, at
> `nodeBudget[t] = tierBudget[t] / w[t]`, and never enters the tier column at all. The two disagreed
> most on the exact node the potency ceiling is calibrated against: `gated-deep`'s capstone landed at
> **56‰** under the binder's arithmetic against this plan's **182‰**. Two modules, one number, a
> factor of 3.25, and no test on either side would have noticed.
>
> **`weightTotal` is not structural, and that is the reason it cannot be a shared constant.** It is
> archetype-dependent. Computed as `Σ over the tree's nodes of that node's tier` — i.e.
> `2 · Σ_{t=1..10} w[t]·t`, both branches — over the shipped three:
>
> | archetype | `w` | `Σ w[t]·t` | `weightTotal` |
> |---|---|---:|---:|
> | `broad-and-flat` | `[2,2,2,2,2,2,2,2,2,2]` | 110 | **220** |
> | `gated-deep` | `[3,3,3,2,2,2,2,1,1,1]` | 89 | **178** |
> | `late-crown` | `[1,1,2,2,2,2,2,2,3,3]` | 126 | **252** |
>
> Any consumer that hardcodes one of those three, or treats it as a corpus constant, is wrong for two
> archetypes out of three. **The plan distributes; the binder reads.** That is the whole contract, and
> it is why the field is FROZEN in the schema.

**Three shape archetypes, worked at `tierCount = 10`, `nodesPerBranch = 20`.** All figures are
per-mille of one branch budget `B_b`, computed with the same round-half-up rule, residual to the last
node in the tier:

**A · `broad-and-flat`** — `w = [2,2,2,2,2,2,2,2,2,2]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | **Σ** |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `w[t]` | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 20 |
| `tierBudget‰` | 18 | 36 | 55 | 73 | 91 | 109 | 127 | 145 | 164 | 182 | **1000** |
| `nodeBudget‰` | 9,9 | 18,18 | 27,28 | 36,37 | 45,46 | 54,55 | 63,64 | 72,73 | 82,82 | **91,91** | |

**B · `gated-deep`** — `w = [3,3,3,2,2,2,2,1,1,1]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | **Σ** |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `w[t]` | 3 | 3 | 3 | 2 | 2 | 2 | 2 | 1 | 1 | 1 | 20 |
| `tierBudget‰` | 18 | 36 | 55 | 73 | 91 | 109 | 127 | 145 | 164 | 182 | **1000** |
| `nodeBudget‰` | 6,6,6 | 12,12,12 | 18,18,19 | 36,37 | 45,46 | 54,55 | 63,64 | 145 | 164 | **182** | |

**C · `late-crown`** — `w = [1,1,2,2,2,2,2,2,3,3]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | **Σ** |
|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| `w[t]` | 1 | 1 | 2 | 2 | 2 | 2 | 2 | 2 | 3 | 3 | 20 |
| `tierBudget‰` | 18 | 36 | 55 | 73 | 91 | 109 | 127 | 145 | 164 | 182 | **1000** |
| `nodeBudget‰` | 18 | 36 | 27,28 | 36,37 | 45,46 | 54,55 | 63,64 | 72,**73** | 54,54,56 | 60,60,62 | |

**All three sum to 1000‰ of `B_b`, so all three cost and award exactly the same.** Their strongest
single node differs by **2.5×**: `gated-deep` hides a capstone worth 182‰ of a branch, while
`late-crown`'s largest node is 73‰ and sits at tier 8, not at the crown — its crown is *wider*, so
nothing there is the prize. Same cost, same award, different builds. That is D15, and it falls out of
the arithmetic rather than a taste call.

#### 3.1 D15 is a COMPLETION property. In skill points, mid-progression value differs by 6.0×

**This is the defect the archetypes carry, and it is stated before the assignment rule because a
reader who stops at the paragraph above will believe something false.**

The equal-value proof above is in **budget points**, summed over all 40 nodes. The player does not
spend budget points. They spend **skill points**, at D36's rising price — `first = 5`, `step = 2`,
so the cumulative cost of owning `N` nodes is

```text
cost(N) = Σ_{i=1..N} (5 + 2(i−1)) = N² + 4N = N(N+4)
```

`spec-tree-state.md:143` derives `first = step·(k+1)/2` from *"`k = 4` nodes per tier (D29: 40 nodes
/ 10 tiers)"*. **4 is the corpus average.** It is the true per-tier node count of exactly one
archetype — `broad-and-flat`, the uniform one — and this module deliberately ships two that are not
uniform. The flatness that derivation buys is real for `K = 4` and is bought by nobody else.

**Reward per skill point at depth `T`**, computed — `W(T) = b·T(T+1)/2` against `cost(N(T))` where
`N(T) = 2·Σ_{t≤T} w[t]`:

| `t` | `broad-and-flat` | `gated-deep` | `late-crown` | spread |
|---:|---|---|---|---:|
| 1 | b/32 | b/60 | b/12 | 5.0× |
| **2** | **b/32** | **b/64** | **b/10.7** | **6.0×** |
| 3 | b/32 | b/66 | b/16 | 4.1× |
| 4 | b/32 | b/57.2 | b/19.2 | 3.0× |
| 5 | b/32 | b/52 | b/21.3 | 2.4× |
| 6 | b/32 | b/48.6 | b/22.9 | 2.1× |
| **7** | **b/32** | **b/46.1** | **b/24** | **1.9×** |
| 8 | b/32 | b/40 | b/24.9 | 1.6× |
| 9 | b/32 | b/35.5 | b/28.7 | 1.2× |
| **10** | **b/32** | **b/32** | **b/32** | **1.00×** |

**At tier 2, two trees this module certifies as equal value differ by 6.0× in the currency the player
actually spends.** All three agree at tier 10 and **nowhere else** — because at tier 10 every tree
owns all 40 nodes, so both the numerator and the denominator are identical by construction. *That is
why it was missed: `C1` checks the endpoint, and the endpoint is the one point where the defect
cannot appear.*

**Decision: accept the gradient, bound it, and refuse an archetype set that widens it.** The two
alternatives were worked and both cost more than they buy:

- **Constrain the width vectors until the gradient is flat.** Under `c(N) = c₀ + (N−1)d`, cumulative
  cost is quadratic in `N`, so reward per point is flat **only when `N(T) ∝ T`** — i.e. only for a
  uniform width vector. Bounding the gradient tightly therefore means collapsing every archetype
  toward `[2,2,2,2,2,2,2,2,2,2]`, which is precisely D15's named failure mode: *"every tree feels the
  same."* This would trade a stated 6× pacing difference for the loss of the whole archetype axis.
- **Make the unlock price archetype-aware.** To flatten a non-uniform archetype the price would have
  to key on **tier** rather than on nodes owned. That breaks D18's order-independence proof
  (`spec-tree-state.md:117-122`), which holds precisely because the price is a function of the count
  alone. It is also `tree-state`'s decision, not this module's.

**So the gradient is a pacing difference, not a power difference, and it is stated with its bound:**

> `gated-deep` is the **worse buy early and the better buy late**; `late-crown` is front-loaded and
> flattens; `broad-and-flat` is flat at `b/32` at every depth. The spread is **6.0× at tier 2**, is
> monotone non-increasing from tier 2 onward, and is **exactly 1.00× at completion**. D15 holds as
> written at tier 10 and is qualified everywhere above it.

**One new refusal at plan-emit time:**

- **R-A1 — the reward-per-skill-point spread is bounded.** For every tier `t` in `1..tierCount`,
  compute `r_a(t) = W(t) / cost(N_a(t))` for every archetype `a`, and refuse if
  `max_a r_a(t) / min_a r_a(t) > archetype.rewardSpreadMaxRatioMilli / 1000`. The shipped value is
  **6000** (‰), which is the measured maximum of the shipped three, so the shipped set passes at
  equality and **a fourth archetype that widens the spread is refused, naming the tier and the two
  archetypes.** The refusal names numbers, so tightening the bound is a one-line tuning change plus a
  visible failure, not an archaeology exercise.

**Which module owns the correction, since `tree-state` is being asked the same question.** The defect
is one arithmetic fact read two ways, so it needs one owner per half and not a shared one:

| Half | Owner | Why |
|---|---|---|
| The **shape** — the width vectors, and the bound on how far apart they may put reward per point | **this module** | The archetypes are emitted here, `R-A1` is checkable from the plan alone with no runtime and no model call, and a fourth archetype is added here. A bound enforced anywhere else is enforced after the offending artifact already exists |
| The **price** — `first`, `step`, and whether `cost(N)` may key on anything but the node count | `tree-state` | D36 is its decision and D18's order-independence proof (`spec-tree-state.md:117-122`) holds precisely because the price is a function of the count alone. Making the price archetype-aware would break that proof, so only the module that owns the proof may trade it away |

**What `tree-state` owes back is a correction, not a decision:** its `first = step·(k+1)/2` derivation
is stated as if `k = 4` were structural. It is the corpus **average**, true of `broad-and-flat` alone.
The flatness that derivation buys is real, and it is bought by one archetype in three. That sentence
needs fixing there; the gradient it produces is bounded here.

**The test that would have caught it, and why the existing ones did not.**
`reward_per_point_is_exactly_b_over_k` asserts `W(t)·k == req(t)·b` — that is the **aptitude** ladder,
which is archetype-free and passes trivially. `every_tree_has_the_same_budget` (`C1`) compares totals,
i.e. tier 10 only. The missing test walks the ladder:

```text
reward_per_skill_point_spread_is_bounded_at_every_tier
    for t in 1..tierCount:
        r[a] = W(t) / cost(2 · prefix_sum(w_a, t))      # exact integer ratio, no float
        assert max(r)/min(r) <= archetype.rewardSpreadMaxRatioMilli
    and assert the spread is EXACTLY 1000‰ at t == tierCount
```

**Checking the endpoint is what failed.** Any property this module conserves over a whole tree owes a
per-tier walk as well, and that is now a standing rule for this spec: an invariant asserted at
`t = tierCount` is asserted at every `t`, or it is labelled a completion property in its own text.

**Archetype assignment is deterministic and append-safe:**

```text
archetype(tree) = archetypes[ ordinal(tree) mod len(archetypes) ]
```

Roster ordinals are append-only (`Aptitude.cs:16-17`: *"an existing aptitude's ordinal never changes,
a retired one's is never reused"*), so appending a roster entry never reassigns an existing tree's
archetype and never re-mints a node id.

#### 3.2 `PassiveTree/TreeEqualValue` — this module owns it

`spec-tree-review.md:202` leans on a gate named **`PassiveTree/TreeEqualValue`** to justify *not*
sampling budgets by hand, and `:210` correctly notes it is not among `tree-language`'s 24 — those are
stage-2 content gates and this is plan-side arithmetic. It was left unowned by both. **It is this
module's**, and it is named here so it stops being a gate that every spec cites and none builds.

| | |
|---|---|
| **Name** | `PassiveTree/TreeEqualValue`, registered beside the other `PassiveTree/*` metrics so `tree-review` reads it through the same registry as `QuotaDrift` and `CellOccupancy` |
| **Input** | the emitted plan alone — `budgetTotal`, `budgetPerBranch`, `nodes[].budgetPoints`, `nodes[].budgetShareMilli`, `archetypes[].widths[]` |
| **Asserts** | `C1` (every tree's `Σ budgetPoints` identical; `Σ offensive == Σ defensive`), `P-1`/`P-2` (§5.2), and **`R-A1` at every tier** (§3.1) |
| **Verdict** | refuses, naming the tree, the branch, the tier and the node. It never clamps |
| **Runs** | at `--emit` and at `--check`, before any model call. It is arithmetic over a committed artifact, so it costs nothing and there is no reason to sample it |

**The half this module does not own, stated so the seam is not silently empty.** Research doc 03
describes the metric as *"per-tree summed potency bands vs the plan's budget"* — that comparison reads
**generated content** against the plan, so it runs after stage 3 over `tree-binder`'s priced affixes.
Same name, same numbers, later input. This module owns the definition and the plan-side half;
`tree-review` runs the content-side half against prices `tree-binder` produced. Neither substitutes
for the other: the plan half proves the **budget** is equal, the content half proves the **content
honoured it**, and `R-A1` is the part that proves equal budget is not equal pacing.

### 4. The mechanism / magnitude split — one budget, one quota

§3.5's measured conclusion is the constraint this section answers to: **a focus build cannot be
rescued with magnitude, only with mechanism.** The sweep ran `b ∈ {0,2,5,10,20} × Fmax ∈ {1.0,1.25,1.5}`
and *not one cell reversed the ordering*.

**The class is derived, never a flag the language stage sets.** The distinction already falls out of
the shipped cost function, whose conditionality term is `1` when an atom declares no trigger:

```text
MAGNITUDE node  ≔  every bound atom has AttachPoint.Stat
                   AND kind ∈ {stat.modify, stat.derived}
                   AND conditionality == 1     (no trigger, no predicate)

MECHANISM node  ≔  anything else — a non-Stat attach point, or a declared trigger, or a predicate.
                   It changes WHEN or WHAT happens, not HOW MUCH.
```

Both halves are checkable against the shipped vocabulary — **7 attach points, 16 kinds, 13 triggers**,
counted this session from `AtomKind.cs:9-30` (the enum members), `AtomKindRegistry.cs:476-869` (the
sixteen `new("…")` rows), and `AtomKind.cs:82-95` (`AtomTriggers`, of which **11 are authorable** —
`OnGranted`/`OnRemoved` are runtime lifecycle states, not authorable triggers). D22 is satisfied for
free: no passive-specific effect vocabulary is invented.

**Per-tier mechanism share**, a monotone ramp with the deepest tier pinned:

```text
mechShareMilli[t] = rampStartMilli + (rampEndMilli − rampStartMilli)·(t−1)/(tierCount−1)
mechNodes[t]      = round_half_up( w[t] · mechShareMilli[t] / 1000 )

mechanism.rampStartMilli = 0      tier 1 is pure magnitude — a readable, boring first step
mechanism.rampEndMilli   = 1000   the deepest tier is 100% mechanism
```

> ### ⛔ There is no `mechanismFloor`. The name is retired everywhere — field, tunable and gate.
>
> **The emitted interface is `archetypes[].mechNodes[]`: a per-tier COUNT of mechanism nodes, one
> array per archetype. Nothing else is emitted, and no consumer may reconstruct a threshold from it.**
>
> `spec-tree-language.md:157` reads `cell.nodeClass := "mechanism" if tier >= t.mechanismFloor` and
> its gate 16 fails *"any deep-tier `magnitude` node"*. That is a **threshold**; this is a **ramp**,
> and no threshold reproduces it. `broad-and-flat` puts **one of two** nodes at tiers 4–7 in the
> mechanism class and `gated-deep` **one of three** at tier 3, so there is no tier `f` for which
> *"tier ≥ f ⇒ mechanism"* is the same rule. Pick `f = 8` and five mechanism nodes per branch sit
> below it unexplained; pick `f = 3` and the gate fails a plan that is correct by construction.
>
> The collision was also live inside this spec: a tunable named `mechanism.floorMilli` held **0**, so
> a consumer reading a key called `mechanismFloor` and finding `0` would have concluded that *every*
> tier is a mechanism tier. That key is renamed `mechanism.rampStartMilli` above, and the targets
> file's `gates.mechanismFloor.*` is renamed `gates.mechanismRamp.deepestTierShareMilli`.
>
> **The ramp is the requirement, not a convenience.** A per-tree scalar cannot reproduce a per-tier
> count that depends on `w[t]`: the same `mechShareMilli[t]` yields 1 node in a width-2 tier and 2 in
> a width-3 tier, and the whole point of §3.5's conclusion is *where the mechanism sits*, not merely
> how deep it starts.
>
> **`tree-language` consumes `mechNodes[t]` as an exact per-tier count** — its own step 4 is already
> shaped for it (*"HARD CONSTRAINTS OVERRIDE THE DRAW"*). Gate 16 becomes an exact count check
> matching `C3`, and the deep-tier criterion becomes `mechNodes[T] == w[T]`, which is `R-M1`.

At `tierCount = 10` the ramp is `[0, 111, 222, 333, 444, 556, 667, 778, 889, 1000]‰`. Computed per
archetype:

| archetype | `mechNodes[t]`, t=1..10 | total | tier 10 |
|---|---|---:|---|
| `broad-and-flat` | 0, 0, 0, 1, 1, 1, 1, 2, 2, 2 | 10 / 20 | 2 of 2 ✅ |
| `gated-deep` | 0, 0, 1, 1, 1, 1, 1, 1, 1, 1 | 8 / 20 | 1 of 1 ✅ |
| `late-crown` | 0, 0, 0, 1, 1, 1, 1, 2, 3, 3 | 12 / 20 | 3 of 3 ✅ |

Two rules on top, both **refusals** at plan-emit time:

- **R-M1 — the deepest tier is 100% mechanism**, i.e. `mechNodes[tierCount] == w[tierCount]` for every
  archetype. Structural, from §3.5. `mechanism.rampEndMilli` may be lowered only with an owner
  decision recorded next to it in the tuning file's `_note`.
- **R-M2 — `mechShareMilli` is monotone non-decreasing in `t`.** A tree that gets *less* mechanical
  as it deepens is the exact failure the sweep measured.

Note `gated-deep` carries the fewest mechanism nodes (8) but by far the most potent one (182‰), while
`late-crown` carries the most (12) and the weakest. The archetype difference lands on the axis that
was measured to matter.

**One budget plus a quota, and why it is not two budgets:**

```text
CONSTRAINT 1 (budget)  Σ price(node).Total per tree == budgetTotal          — one number, conserved
CONSTRAINT 2 (quota)   |{ nodes at tier t with class == mechanism }| == mechNodes[t]   — per tier, exact
```

Two budgets would owe an **exchange rate** between mechanism points and magnitude points. That rate
is precisely what cannot be computed today: `CostFunction` is knowingly wrong on multiplicative pairs
(the reason `ContentValidation.DriftTolerancePercent = 25` exists at all,
`ContentValidation.cs:43-44`), the marginal value of a node diverges wildly by build (§3.5 measured a
magnitude node as worth ≈0 to a focused build), and the context-aware read that would fix both is
E10's marginal read, which `spec-power-vector.md` explicitly defers to. **A quota owes nothing:** it
is a count, it is checkable, and it never has to answer *"how many magnitude points is a reflect
worth"*.

> **If you make them two budgets you owe an exchange rate. A quota owes nothing.**

**⚠ The known weakness, stated plainly.** *The quota is checked structurally; the value it stands in
for is behavioural.* `class` is derived from the shape of the bound atoms — a non-Stat attach point,
a trigger, a predicate. Nothing in that derivation asks whether the mechanism **does anything**. A
tree can therefore pass `mechNodes[10] == w[10]` at 100% with ten nodes that §3.5 would measure as
worthless, and the emit gate will be green.

Four things mitigate it, and none of them turns a structural check into a behavioural one:

1. **`mechanism-wiring` is in the same wave, deliberately** (map §"Why `mechanism-wiring` is in wave
   0"). Its four inert lines are what make a mechanism node *scorable at all* — chiefly the fourth
   `IActorStatSubsystem` (`ActorHub.cs:145,148,155` registers three) and `stat.derived`'s
   `RuntimeState.None` / `AtomTriggers.None` (`AtomKindRegistry.cs:534-535`). Until they land, a
   behavioural check has nothing to read. If they never land, the mechanism budget buys nodes that
   measurably do nothing — which is why they are wave 0 and not wave 3.
2. **A mechanism-diversity quota**, not just a count. The quota cell carries `trigger` and
   `channelFamily`, so a tree cannot fill its mechanism quota with ten copies of one trigger; the
   `CellOccupancy` gate (median ≤ 2) refuses that at corpus level.
3. **`tree-review` samples behaviourally.** `tools/CombatSim` drives the real dispatcher
   (`Simulator.cs:59`) and Battle already fires `OnDamageDealt` (`BasicAttack.cs:176`), so a sampled
   deep-tier node *can* be scored end to end once (1) lands. Sampling, not the whole corpus.
4. **`squad-harness` re-measures the premise** at the scope the game is actually played at (D33).

**Residual risk, owned and not hidden:** a mechanism node that is structurally legal, novel in its
cell, and outside the review sample can still be worth nothing. This module cannot close that — the
close is `tree-review`'s sampling design plus (1) landing. Stating it here is the point; it is not a
reason to hold the plan.

### 5. The node potency ceiling — one denominator, and an honest label

Eleventh Hour Games' rule, via prior art §4: *"individual nodes should not be so potent that you feel
forced to build it in a particular way."*

#### 5.1 The denominator, fixed: one BRANCH, on both sides

**The single most dangerous thing this section used to do was measure two comparable numbers against
two different denominators.** `nodes[].budgetShareMilli` is ‰ of **one branch**;
`potency.maxNodeShareMilli` was ‰ of **`budgetTotal`**. So `gated-deep`'s capstone read
`budgetShareMilli = 182` against a ceiling of `91`, and any consumer comparing the two directly was
wrong by exactly **2×**, in the safe-looking direction. `tree-catalog`'s own load-path check was
written against the wrong one.

**Both are now ‰ of one branch budget `B_b`, per R5.** There is one denominator in this module and it
is a branch:

```text
maxNodeShareMilli = round_half_up( 2000 / ((tierCount + 1) · minTerminalWidth) )   ‰ of ONE BRANCH
```

**Derivation** — the largest node any archetype can produce is the deepest tier's budget split among
the fewest nodes:

```text
max nodeBudget = tierBudget[T] / w[T]
               = B_b · T / T_tri / w[T]
               = B_b · T / (T(T+1)/2) / w[T]
               = B_b · 2 / ((T+1) · w[T])
```

**At `tierCount = 10` with `minTerminalWidth = 1`:**

```text
2000 / (11 · 1) = 181.81…‰   →  round half up  →  182‰ of ONE BRANCH
```

~~`91‰ of budgetTotal`~~ is superseded — same magnitude, wrong denominator, and it is the form that
produced the 2× trap. The reasoning that replaced doc 02's `125` still stands: doc 02's constant was
derived at `tierCount = 7`, and shipping it at ten tiers would admit a capstone worth **1.37×** the
derived value (finding A8).

**The comparison rule, stated so a consumer can actually run it:** every emitted `budgetShareMilli` is
compared against `potency.maxNodeShareMilli` **directly**, same units, no conversion. Anything that
needs a factor of two has the wrong number. ~~`R-P1`~~ is retired as an acceptance test in §5.2 — what
survives here is the **denominator** rule, which is real, and not a refusal, which was not.

#### 5.2 What the ceiling refuses — which, at the shipped topology, is nothing

**Stated plainly, because the previous version dressed a tautology as a check.**

`nodeBudget[t] = B_b · 2t / ((T+1)·T·w[t]/T)`… more simply, per mille of a branch it is
`2000·t / (T(T+1)·w[t])`, maximised over `t`. `w[t] ≥ 1` is a hard precondition —
`node_budget_milli` raises below 1 — and `t ≤ T`, so the maximum of `t/w[t]` is `T/1` and the
supremum is exactly `2000/(T+1)`. **The ceiling is that supremum.** Therefore:

| Claim previously made | Truth |
|---|---|
| R-P1 refuses an over-potent node | **No.** It compares a construction against its own supremum. At `tierCount = 10` no legal width vector can reach 183‰ |
| R-P2 refuses an inadmissible archetype | **No.** `2000/((T+1)·w[T]) ≤ 2000/(T+1)` reduces to `w[T] ≥ 1`, which is already a precondition |
| *"a `w[T] = 1` archetype on a **deeper** ladder fails"* | **False, and deleted.** A deeper ladder makes the maximum node *smaller* (`2000/(T+1)` falls as `T` rises). Deeper is strictly safer; the direction was backwards |

**Decision: `potency.maxNodeShareMilli` is a DOCUMENTATION CONSTANT, and it is labelled one.** It
records the topology's own maximum so that a reader, a reviewer or a downstream load-path has one
authoritative number to quote instead of re-deriving it — the role the register rows in
`ssot-power-scale.md` play. It does not answer *"is a 182‰ capstone too potent?"*, because the answer
under this derivation is *"no, by definition"*, and a check whose answer is fixed in advance is not a
check.

The acceptance tests that pretended otherwise are deleted (§Testing). **Two real checks survive**,
and neither is a balance refusal:

- **P-1 (derivation guard, keeps doc 02's two-place-edit hazard closed).** The emitted constant must
  equal `round_half_up(2000/((tierCount+1)·minTerminalWidth))` for the emitted `tierCount` and the
  emitted `minTerminalWidth`. A hand-edited value fails `--check` naming the field. This one can and
  does fire.
- **P-2 (rounding guard).** No emitted, rounded `budgetShareMilli` may exceed the derived maximum, at
  **every** tier count from 1 to 40 — not only at 10. The residual-absorption rule pushes the deepest
  tier above its exact fraction wherever the residual is non-zero (it is 0 at ten tiers and not at
  every tier count), so this fires on a rounding bug. It is an implementation guard, and it is
  described as one.

**What would make it bite, and who decides.** Setting the ceiling **below** `2000/(tierCount+1)` — say
150‰ of a branch — would turn it into a real admissibility test that `gated-deep` would have to pass
on its merits, and it would currently fail at 182‰. **That is the check ~~`R-P2`~~ claimed to be and
was not.** It is also a balance decision with a visible cost — the `gated-deep` capstone gets reshaped
or the archetype is dropped — so it is an **Ask first** item below, not something this spec quietly
assumes. The behavioural question the EHG rule actually asks
is answered by `tree-review`'s sampling and `squad-harness`, not by an arithmetic identity.

**Caps register standing.** `maxNodeShareMilli` is a **bounded ratio** — a per-mille share of a
budget, domain closed at 1000 — and is exempt under `ssot-power-scale.md` §11.6, which requires the
exemption be said out loud. It never clamps: `P-1`/`P-2` refuse and name the offender, because a
silent clamp turns *"this node stopped mattering"* into a bug with no symptom.

**Key and unit:** `potency.maxNodeShareMilli` — *per-mille of one branch budget*, value **182** — in
`data/tuning/passive-tree.v1.json` (R2), with a `_note` carrying the derivation, the denominator, and
the sentence *"documentation constant: at the shipped topology nothing can exceed it."*

### 6. The property vocabulary (D14) — this module DEFINES it, it does not read one

D14 makes exclusion **property-based**: a printed runtime no-op keyed on a property, never a named
pair. A named-pair list is `O(n²)` and cannot survive generation across the 39 trees this module
plans, let alone the 879 in the whole corpus; a property
predicate is `O(1)` and covers nodes that do not exist yet. The vocabulary must therefore exist
**before any node text is written** (§6 step 2).

**D22 says the property space is atom tags. That is half true, and the false half is load-bearing.**
Counted this session over all 740 files under `data/seed/`: **53 objects carry a `tags` map, across
9 distinct keys, and 6 of those keys are provenance** (`generatedFrom`, `generator`, `migratedFrom`,
`effectId`, `deterministicSourceFor`, `marker`). **Exactly three tag values in the whole corpus are
semantic** — `category=offense`, `source=trait`, `grant=first-clear`. Tags are a free-form JSON blob
with no membership check (`AtomRow.cs:38-39`). `AffixTags` ships (124 lines, tested) and derives an
affix's tags from its refs' atoms, but a union over an empty vocabulary is still empty.

**So this module does not read a property vocabulary. It defines one, and the plan is where it
lives.** Every axis is either read from a closed roster mirror or is a closed set declared here; a
property the plan never named cannot be referenced by an exclusion, by a tag, or by a permitted
subset. That is what makes D14's predicate `O(1)`.

| Axis | Members | Source |
|---|---:|---|
| `nodeClass` | 2 | `mechanism` \| `magnitude` — derived (§4) |
| `branch` | 2 | `offensive` \| `defensive` (D10) |
| `tier` | 10 | `1..tierCount` |
| `posture` | 3 | `posture` field of `data/seed/aptitudes/roster.json` |
| `aptitude` | 12 | `data/seed/aptitudes/roster.json`, counted at load |
| `element` | 6 + `omni` | `data/seed/elements/roster.json`, counted at load |
| `status` | 21 | `data/seed/statuses/roster.json` — **owed, §9** |
| `atomAttachPoint` | 7 | `data/seed/atoms/vocabulary.json` — **owed, §9** |
| `atomKind` | 16 | same mirror |
| `atomTrigger` | 13 (11 authorable) | same mirror |
| `channelFamily` | 53 | `entries` of `data/seed/derived-stats/catalog.json`, counted at load |
| `conversionState` | 2 | `converted` \| `unconverted` (D16/R8) |
| `exclusionForm` | 3 | `reroute` \| `precedence` \| `nullification` (D14's ladder, **all three kept — D40**) |

**Two rules on the vocabulary, both refusals:**

- **No axis lists its own members in the tuning file or in code.** Every count is read and counted at
  load. A thirteenth aptitude changes this grid by construction, exactly as `AptitudeCatalog.Count`
  is `PostureCount × PerPosture` rather than a typed 12 (`Aptitude.cs:29-36`).
- **A missing mirror is a refusal naming it (`EXIT_CANNOT_RUN`), never an empty axis.** T5's rule
  applied to rosters: a default is a value nobody chose that behaves like one somebody did.

**`conversionState` is emitted; conversion nodes are not.** D16 needs an element-payload write and
**no kind among the 16 does one**, with a silent failure mode
(`OverlayCombatCalculator.cs:128-172` loops the payload's own components). So the axis exists so an
exclusion can key on it — D14's own example is *"no effect if the damage is converted"* — while
`quotas` allocates **zero** nodes to conversion until a reviewed 17th kind lands. Vocabulary yes,
budget no.

**The predicate mechanism needs no new type.** `EligibilityRule` already carries `RequireTags` and
`AnyOfTags` (`"key:value"` pairs) with `IsEligible` evaluating them. D14's example is exactly
`AnyOfTags: ["conversionState:converted"]`.

### 7. The point supply and the gate quantity — read, never assumed

**FACT, `data/tuning/aptitudes.v5.json:15-18`:**

```json
"grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 }
```

**FACT, `:22-27`** — the table `PointBudget` actually reads:

```json
"aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }
```

**Despite the name, "Milli" does not divide.** `PointBudget.PointsFor` is
`checked { return sourceValue * rate; }`, and the type's own remark says so. Commander rate is
therefore **3 points per `Θ`**, agreeing with the `grant` block.

**`skillPointsPerTheta` still has zero production consumers** — verified by grep over `src/`,
`tests/` and `tools/` excluding `bin`/`obj`: declared at `AptitudeTuning.cs:13`, parsed at `:158`,
asserted once at `AptitudeTuningTests.cs:90`, and read nowhere else. This is the spender D2 was
written to supply, and it arrives in `tree-state`, not here.

**Two different quantities, and conflating them is the easy mistake.** `req(t)` gates on **aptitude
points allocated to that tree's gate quantity**; nodes are bought with **skill points**. This module
touches only the first.

**The plan emits `gateQuantity` as an opaque id and never resolves it.** That is the correct
boundary, and it is what lets a complete plan be emitted for the 27 trees whose gate source has not
been built yet:

| Category (R7) | `gateQuantity` | State in code, verified this session | Owner |
|---|---|---|---|
| `primary` (12) | `aptitude.<Id>@Commander` | ✅ shipped — `PointBudget.PointsFor(AllocationScope.Commander, …)` | shipped |
| `elemental` (6) | `element_mastery.<id>@Aspect` | ⛔ **comments only today.** The scope exists (`AllocationScope.Aspect`, `AptitudeAllocation.cs:8`); the source does not. All four `src/` hits for `element_mastery` are XML doc comments, and `PointBudget.cs:15` records the old ownership: *"owned by the demon program's `aspect-scope` module and does not exist yet"* | **`gate-counters`, wave 0 (D37)** — the comment's attribution to the demon program is superseded |
| `status` (21) | `status_applied.<id>` — **outside `AllocationScope`** (D35) | ⛔ **zero `src/` hits today.** D35 correctly removed the `AllocationScope` dependency, and removed the only place the counter was going to live with nothing replacing it | **`gate-counters`, wave 0 (D37)** |
| `family` (`F`) | `species_level@DemonType` | ✅ rate shipped, source via `PointBudget.DemonTypeSourceFromLevel` | shipped |

#### 7.1 ⛔ A tree's gate quantity must EXIST before that tree's content is generated

> ### D37 (2026-09-05): the two missing quantities have an owner and a schedule. All 39 trees are reachable.
>
> `status_applied.<id>` and `element_mastery` are built by **`gate-counters`, a wave-0 module of this
> program** — the counters, their persistence, and the `PointBudget` binding. The rule below is
> unchanged and still binding: **content waits for its gate.** What changed is that the wait is now
> **bounded and owned**, so this is a *sequencing* rule, not a permanent hole.
>
> ~~*"1,080 nodes ship permanently at tier 0"*~~ and ~~*"only the 12 primary trees are reachable"*~~
> are superseded. **The plan plans all 39 trees; all 39 are reachable once wave 0 completes.** The
> generation order below is a schedule, and `gateState` is what advances it.

**Ideal §13.4, verified in code above: the gate quantity is not built yet for 27 of the 39 trees.**

```text
  12 primary trees × 40 =   480 nodes   generable today            31%
   6 elemental      × 40 =   240 nodes   generable when gate-counters lands element_mastery
  21 status         × 40 =   840 nodes   generable when gate-counters lands status_applied
  ------------------------------------------------------------------
  27 trees          × 40 = 1,080 of 1,560 generic nodes — 69% — wait for wave 0,
                           and would be authored, reviewed and committed unreachable
                           if generated ahead of it
```

A node behind a tier whose `req(t)` reads a quantity nothing produces yet is not a wiring gap that
resolves itself; until the carrier lands, `req(1) = 5` is a threshold on a number that is
structurally zero. **Nothing is broken by the plan — the plan is cheap, mints no content and makes no
model call — but 1,080 nodes of authored content generated ahead of the counters would be bought
before they could be delivered.** That is a scheduling defect with a known fix date, and `R-G1` is
what keeps the schedule honest.

**R-G1 — the generation gate.** A tree's `gateQuantity` must have a **production carrier in `src/`**
before that tree's content is generated. This module emits a `gateState` per tree
(`carrier` \| `pending`) from a checked-in evidence row, and stage 2 refuses — exit 3, naming the tree
and the missing quantity — when asked to generate content for a `pending` tree. `--emit` is
unaffected: planning a `pending` tree is free and the plan is where the absence becomes visible.

**R-G2 — the plan emits the 12 primary trees first.** `trees[]` is ordered by `generationWave`, then
by roster ordinal, and wave 0 is exactly the primary trees. This is not cosmetic: it is the order the
build follows, it makes *"generate what is reachable"* the default rather than a discipline, and it
matches the ideal's own build order — *"one wave per gate quantity as it lands."*

| Wave | Trees | Nodes | Unblocked by |
|---:|---|---:|---|
| 0 | 12 `primary` | 480 | nothing — shipped today |
| 1 | 6 `elemental` | 240 | **`gate-counters`** landing `element_mastery` (D37) — a wave-0 sibling of this module, not an unscheduled dependency on another program |
| 2 | 21 `status` | 840 | **`gate-counters`** landing the `status_applied.<id>` counter (D37). D35 left it without a home; D37 gave it one |
| 3 | `F` `family` | 40·`F` | a closed demon-family roster (§9 item 5) |

The wave a tree sits in is **derived from `gateState`, never hand-assigned** — so the day
`element_mastery` gets a carrier, one evidence row moves and 240 nodes become generable without a
spec edit. Under D37 that day is scheduled rather than hoped for, and the same is true of the 840
status nodes.

**Four incommensurable quantities at one threshold is a known, half-closed finding**, and it is
**`tree-resolve`'s** to close, not this module's: *"Gate on ONE index; the other three convert INTO
it, never sit beside it."* Half-closed already — specimen levels now read the shared arithmetic curve
(`ssot-power-scale.md` §10.2 row 27). The plan therefore emits `gateQuantity` **and**
`gateIndexKind`, naming which conversion each tree's gate owes, so the gap is visible in data rather
than discovered at runtime.

### 8. The quota cells — anti-skew, before any call

The corpus this exists to avoid is measured, not hypothetical: the shipped species corpus is
Onslaught 39.5% against Ferocity 0.2% (**166×**) and `earth` 45.1% against `air` 6.7%, produced with
the enum open. Permutation and voting recover *per-entry* accuracy; only **removing the option from
the call** fixes *aggregate shape*.

**Reuse the shipped apportionment primitive. Do not write a second one.**
`largest_remainder_count` (`tools/seedsmith/seedsmith/adapters/actions/distribution_planner/derive.py:73-90`)
distributes a whole total across a closed, ordered vocabulary by largest remainder, with `_widen_mul`
(`:57-63`) raising `OverflowError` rather than wrapping, and ties broken on the declared order rather
than on dict iteration order. `expand_counts` (`:92-104`) flattens the result deterministically.

```text
1  N := Σ over trees of nodesPerTree                       # 39 × 40 = 1,560 today
2  for each axis a in {nodeClass, trigger, element, status, channelFamily, exclusionForm}:
       quota[a] := largest_remainder_count(targets[a].weightsMilli, ORDER[a], N)
3  seq[a] := expand_counts(quota[a], ORDER[a])
4  for each tree in roster order, each (branch, tier, index) slot in canonical order:
       cell := { a: seq[a][cursor] for a in AXES } ; cursor += 1
       # HARD CONSTRAINTS OVERRIDE THE DRAW, and they only ever NARROW:
       cell.element   := tree.element    if tree.category == elemental
       cell.aptitude  := tree.aptitude   if tree.category == primary
       cell.status    := tree.status     if tree.category == status
       cell.nodeClass := "mechanism"     if this slot is inside mechNodes[tier]
5  REBALANCE: a slot whose draw was overridden RETURNS its drawn value to the pool, and the pool is
   re-apportioned over the remaining slots
6  permittedIds[axis] := the ids of that axis whose value == cell[axis]
7  emit
```

**Step 5 is the line that gets forgotten.** An elemental tree's element is forced, so its drawn
element must go back to the pool. Skip it and the forced trees consume their own quota twice — once
by force, once by draw — and the free trees inherit the deficit. That is the original skew wearing a
planner's uniform.

**Step 6 is the load-bearing line.** `permittedIds` is what `tree-language` prints into its JSON
Schema `enum`, so under constrained decoding an out-of-quota value is **unsampleable**, not merely
rejected afterwards.

**`legitimateSkew.rows` ships empty for the generic trees.** D32's named theme allowance was argued
against the *species* corpus, whose skew this corpus does not inherit — generic trees are generated
fresh under the quota, so near-uniform is reachable by construction. The allowance belongs to
`species-tree`, which does lock against real species. Writing an unused allowance here would be a
number nobody chose.

---

## Commands

```powershell
# emit the plan (no model calls, no RNG)
python -m seedsmith trees plan --emit

# regenerate into a temp tree and byte-diff against what is committed.
# 0 = identical, 1 = drift (names the first differing path), 2 = an input is missing or invalid
python -m seedsmith trees plan --check

# compare two plans: budget deltas, archetype reassignments, quota-cell moves, and — the one
# that matters under D24 — node ids added, removed or re-minted
python -m seedsmith trees plan --diff data/seed/passive-tree/plan.v1.json <other-plan.json>

# the two roster mirrors this module owes, same --check/--emit contract as tools/ElementEnumGen
dotnet run --project tools/ElementEnumGen -- --status-check
dotnet run --project tools/ElementEnumGen -- --atom-vocab-check

# tests and audits
python -m pytest tools/seedsmith/tests/test_tree_plan_ladder.py
python -m pytest tools/seedsmith/tests/test_tree_plan_budget.py
python -m pytest tools/seedsmith/tests/test_tree_plan_graph.py
python -m pytest tools/seedsmith/tests/test_tree_plan_ids.py
python -m pytest tools/seedsmith/tests/test_tree_plan_quota.py
python -m pytest tools/seedsmith/tests/test_tree_plan_repro.py
python scripts\audit-magic-numbers.py --domain passive-tree   # must find nothing in this module
```

Exit codes follow the shipped CLI contract exactly (`report/cli.py:44-47`): `0` clean, `1` gap,
`2` cannot run, `3` refused.

---

## Project structure

The planner is a **seedsmith adapter, not a new tool** — it reuses `largest_remainder_count`, the
corpus loader, the metric registry and the CLI's exit codes rather than growing a second copy of a
carefully-written integer algorithm. It makes **no model calls**; the language stage is a separate
adapter under the same package (map assumption 1: nothing in `src/` generates a node).

```text
tools/seedsmith/seedsmith/adapters/trees/__init__.py
tools/seedsmith/seedsmith/adapters/trees/plan/roster.py        reads the mirrors; refuses on a missing one
tools/seedsmith/seedsmith/adapters/trees/plan/ladder.py        req(t), tierBudgetMilli, rounding rule
tools/seedsmith/seedsmith/adapters/trees/plan/archetypes.py    widths, node budgets, the mechanism ramp
tools/seedsmith/seedsmith/adapters/trees/plan/quota.py         the §8 algorithm, incl. step 5 rebalance
tools/seedsmith/seedsmith/adapters/trees/plan/vocabulary.py    the §6 property vocabulary
tools/seedsmith/seedsmith/adapters/trees/plan/invariants.py    G1-G9, R-G0/R-G1/R-G2, R-M1/R-M2,
                                                               R-A1, P-1/P-2, C1-C3
tools/seedsmith/seedsmith/adapters/trees/plan/ids.py           nodeKey allocation and READ-BACK, the
                                                               container_id grammar check
tools/seedsmith/seedsmith/adapters/trees/plan/emit.py          canonical JSON, planHash, --check/--diff

data/tuning/passive-tree.v1.json                               the program's ONE tunable file (R2) --
                                                               this module's keys sit beside
                                                               tree-resolve's and tree-state's
data/tuning/passive-tree-targets.v1.json                       quota targets + gates (D32): weight
                                                               vectors over rosters, not scalar dials

data/seed/statuses/roster.json                                 NEW mirror this module owes
data/seed/atoms/vocabulary.json                                NEW mirror this module owes

data/seed/passive-tree/plan.v1.json                            the manifest — roster, ladder, vocabulary,
                                                               archetypes, quota marginals, planHash
data/seed/passive-tree/plan/<treeId>.v1.json                   one file per tree, diffable

tools/seedsmith/tests/test_tree_plan_ladder.py
tools/seedsmith/tests/test_tree_plan_budget.py
tools/seedsmith/tests/test_tree_plan_graph.py
tools/seedsmith/tests/test_tree_plan_ids.py
tools/seedsmith/tests/test_tree_plan_quota.py
tools/seedsmith/tests/test_tree_plan_repro.py
```

**Why a manifest plus one file per tree.** Doc 02 emits one file; doc 03 emits one per tree. Both
reasons are good and they do not conflict: 39 files of 40 nodes each are readable in a diff, and one
`planHash` over the manifest plus the sorted per-tree hashes gives CI a single cheap gate.

---

## Code style

The budget distribution, which is the module's load-bearing arithmetic:

```python
# tools/seedsmith/seedsmith/adapters/trees/plan/ladder.py
#
# STRUCTURAL, not tunable: 1000 is the per-mille denominator (tunables-ssot.md section 1, "Literal").
_PER_MILLE = 1000


def tier_budget_milli(tier_count: int) -> "list[int]":
    """D20's pairing rule as integers: tier `t` is worth `t / T_tri` of ONE BRANCH budget, so
    per-tier power is linear in `t` (binding — pairing it with CONSTANT per-tier power inverts the
    whole design) and the column sums to 1000 identically.

    `Sum(t for t in 1..T)` IS `T_tri` by definition, so the cancellation is an identity in `T`, not a
    coincidence at ten tiers -- which is what makes D15's equal expected value a CONSTRUCTION rather
    than a post-hoc normalisation. The width vector `w[t]` enters one level down (`node_budget_milli`)
    and never appears in this sum at all.

    Integer discipline (CLAUDE.md, "Numeric overflow"): widen before multiplying, divide by 1000
    exactly once and last, let overflow throw. Round half up, then absorb the residual into the
    DEEPEST tier -- the residual is 0 at ten tiers but is NOT 0 at every tier count, and two
    correct-looking implementations that round differently would disagree in the last per-mille and
    fail `--check` for nothing.
    """
    if tier_count < 1:
        raise ValueError("tier_budget_milli: tier_count must be at least 1")
    t_tri = tier_count * (tier_count + 1) // 2
    out: "list[int]" = []
    for t in range(1, tier_count + 1):
        scaled = _widen_mul(_PER_MILLE, 2 * t)          # widened BEFORE the multiply
        out.append((scaled + t_tri) // (2 * t_tri))     # round half up; ONE division, and it is last
    out[-1] += _PER_MILLE - sum(out)                    # residual to the deepest tier
    return out


def node_budget_milli(tier_budget: int, width: int) -> "list[int]":
    """One tier's budget split among its `w[t]` nodes, residual to the LAST node in the tier so
    `sum(nodes) == tier_budget` exactly. Without this, `w[t] = 3` against a tier budget of 55 leaks a
    per-mille per tier and C1's equal-value assertion fails on rounding alone."""
    if width < 1:
        raise ValueError("node_budget_milli: width must be at least 1")
    base = tier_budget // width
    nodes = [base] * width
    nodes[-1] += tier_budget - base * width
    return nodes
```

`_widen_mul` is imported from the shipped `distribution_planner/derive.py:57-63`, not re-implemented.

**No `float` anywhere in the planner.** `PowerVector` is `int` throughout for exactly this reason
(`PowerVector.cs:13-16`: *"a double would make two runs of the same catalog disagree in the last bit
and move a content hash for nothing"*). Per-mille integers everywhere; the one place a magnitude
appears is the runtime multiply `share × P(Θ)`, which is `tree-resolve`'s and is `long`.

---

## Testing strategy

**The equal-value property is a test, not a comment.** So is byte reproducibility, and so is every
graph invariant.

| Test | Asserts |
|---|---|
| `every_tree_has_the_same_budget` | **C1.** `Σ node.budgetPoints` is identical across all `n` trees, and `Σ offensive == Σ defensive` in each. This is *"no tree is OP"*, machine-checked |
| `equal_value_holds_for_any_width_vector` | The identity, not the instance: over generated `w` with `Σw == nodesPerBranch` and arbitrary tier counts 1..40, `Σ tierBudget == 1000` and `Σ nodeBudget == tierBudget` per tier, always |
| `archetype_shapes_actually_differ` | The inverse guard. The strongest node differs by ≥ 2× between the widest and narrowest crown — otherwise D15 has silently collapsed into "equal shape" and the corpus is interchangeable |
| `reward_per_point_is_exactly_b_over_k` | `W(t)·k == req(t)·b` as integers at every `t` in 1..40 — exact equality, never a tolerance |
| `req_is_integer_at_every_tier` | `k·t(t+1)/2` for every integer `k` in 1..100, `t` in 1..1000 |
| `tier_budget_column_sums_to_one_thousand` | For every tier count 1..40, including the ones where the residual is non-zero |
| `graph_invariants_g1_to_g9` | All nine, per tree, per archetype. `G9` in its corrected form: `nodesPerTree` is `2 × nodesPerBranch` and is **even** |
| `no_node_has_a_parent_at_tier_one` | `G6` restated — the shared root is gone and cannot creep back |
| `node_count_is_forty_and_the_arithmetic_is_shown` | Fails naming `2 × Σw` if a width vector stops summing to `nodesPerBranch` |
| `deepest_tier_is_all_mechanism` | **R-M1**, every archetype |
| `mechanism_share_is_monotone` | **R-M2** |
| `reward_per_skill_point_spread_is_bounded_at_every_tier` | **R-A1** — §3.1's missing test. Walks `t = 1..tierCount`, computes `W(t)/cost(N_a(t))` per archetype as an exact integer ratio, refuses above `archetype.rewardSpreadMaxRatioMilli`, and asserts the spread is **exactly 1000‰ at `t == tierCount`**. This is the test that catches the 6.0× gradient; `C1` cannot, because tier 10 is the one point where the defect is invisible by construction |
| `potency_ceiling_is_recomputed_not_read` | **P-1.** The emitted `182` must equal `round_half_up(2000/((tierCount+1)·minTerminalWidth))` for the **emitted** `tierCount` and `minTerminalWidth`. A hand-edited value fails `--check` naming the field |
| `no_rounded_share_exceeds_the_derived_maximum` | **P-2**, at every tier count 1..40. The residual-absorption rule pushes the deepest tier above its exact fraction wherever the residual is non-zero, so this fires on a rounding bug and on nothing else. An implementation guard, described as one |
| `the_gate_currency_is_aptitude_points` | **R-G0.** A plan whose `ladder.gateCurrency` is anything but `"aptitudePoints"` exits 3 naming the field, so a regenerated plan cannot re-assert a skill-point gate (§2) |
| `a_pending_tree_refuses_stage_two_generation` | **R-G1**, and `trees[]` is ordered wave-0-first (**R-G2**), so *generate what is reachable* is the default rather than a discipline |
| `node_ids_are_read_back_never_recomputed` | Emit, insert a node at tier 1, re-emit: every surviving node keeps its `nodeKey` and therefore its `nodeId`. This is the property `tree-review`'s `O(diff)` re-review rests on (§Node ids) |
| `node_ids_match_the_container_id_grammar` | Every emitted `nodeId` matches `^skill\.[a-z0-9-]+$` — no `/`, no dot in the body (`item/seed-contract.md:131-133`) |
| `plan_is_byte_identical_across_two_runs` | Emit twice into two temp trees, compare bytes. Minting order is the canonical `(tree, branch, tier, slot)` walk, so two from-scratch emits allocate the same `nodeKey`s — and `node_ids_are_read_back_never_recomputed` is what covers the case the from-scratch test cannot: an emit against a plan that already exists |
| `check_exits_zero_against_the_committed_plan` | The `--check` contract, copied from `resource_ownership.py:20-23` |
| `flipping_one_input_byte_fails_check_and_names_it` | Mutate each hashed input in turn; drift must be attributed, not merely detected |
| `plan_hash_excludes_emitted_utc` | Otherwise every run is drift |
| `roster_counts_are_read_never_typed` | Greps this module's source for a bare `12`, `6`, `21`, `53`, `16`, `13`, `7` |
| `a_missing_roster_mirror_refuses` | Exit 2 naming the file — never an empty axis, never a default |
| `absent_family_roster_emits_pending_not_silence` | `demonFamilies: []` **and** a `_pending` entry, so `F = 0` is visible |
| `quota_marginals_are_exact` | Every axis's emitted counts sum to `N` and match an independent re-derivation |
| `overridden_draws_return_to_the_pool` | §8 step 5 — force every elemental tree's element and assert the residual axis stays on target |
| `no_conversion_node_carries_budget` | D16 stays at zero until a 17th kind lands |
| `exclusion_predicates_only_key_on_named_properties` | A key outside `propertyVocabulary` is a refusal |
| `no_float_in_the_planner` | AST scan of this module's source for float literals and `/` on ints |
| `widen_before_multiply_is_used` | `_widen_mul` is the only multiply path in the ladder and quota modules |

**Two tests were DELETED, and the deletion is the finding.** ~~`no_node_exceeds_the_potency_ceiling`
(R-P1)~~ and ~~`every_shipped_archetype_is_admissible` (R-P2)~~ compared a construction against its own
supremum, so both passed by definition (§5.2). A tautology dressed as a check is worse than no check:
it reads as coverage on the one number a reviewer is most likely to trust without re-deriving, and it
occupies the row where a real check would otherwise be missed.

Coverage says what the tests touched; mutation says what they would notice. The budget and ladder
modules are the two where a survivor matters, so both carry a `scripts/mutants/*.json` set before
this module is called done.

---

## Boundaries

**Always**

- Read every roster count at load and count it. `AptitudeCatalog.Count` is `PostureCount × PerPosture`
  for the same reason (`Aptitude.cs:29-36`); this module inherits the discipline.
- Emit **dimensionless per-mille shares**. The runtime multiplies a share by `P(Θ)` at read time, so
  the plan is `Θ`-free, stable forever, and cannot become a second power curve — there is no level
  in it.
- Widen before multiplying; divide by 1000 once, last; let overflow throw.
- Refuse and name the offender. A violated invariant names the tree, the archetype, the tier and the
  node.
- Emit the numbers, not only the formulas that produced them — `req[]`, `tierBudgetMilli[]`,
  `mechNodes[]` are all diffable arrays.

**Ask first**

- Changing `topology.tierCount`, `branchCount` or `nodesPerBranch`. These are structural, they
  re-mint node ids, and under D24 a re-minted id is a migration, not a balance pass.
- Lowering `mechanism.rampEndMilli` below 1000. It contradicts §3.5's measured conclusion and violates
  `R-M1`, and the `_note` must record the decision beside the value.
- Adding a fourth archetype. `R-A1` refuses one that widens the reward-per-point gradient past
  `archetype.rewardSpreadMaxRatioMilli`, which is where the admissibility question actually lives —
  **not** in a terminal-width rule, since `w[T] ≥ 1` is already a precondition and a deeper ladder
  makes the largest node *smaller*, not larger (§5.2).
- Lowering `potency.maxNodeShareMilli` below `2000/(tierCount+1)`. That turns a documentation constant
  into a live admissibility test which `gated-deep`'s 182‰ capstone would fail, so it reshapes or
  drops an archetype (§5.2).
- Adding an axis to `propertyVocabulary`. The set is closed on purpose; widening it is the exact
  defect the atom program exists to stop.

**Never**

- **Emit node text.** No name, no flavour, no printed sentence. Those are holes.
- **Emit a magnitude.** No hp, no damage, no coefficient, no `P(Θ)` read. The plan carries shares and
  counts; `tree-binder` writes coefficients and `tree-resolve` is the only module that multiplies by
  `P(Θ)`.
- **Hardcode a roster count.** Not 12, not 6, not 21, not 53, not 16/13/7. Twelve is a measured
  outcome, not a decision.
- Use `float`, or any RNG. There is no sampling, seeding or shuffling in stage 1 at all.
- Choose which atoms a node binds. The plan hands a **ceiling and a quota**;
  `ContentValidation.cs:58-60` is explicit that the budget is *"**never** a generation input"*.
- Write a hand-set `nodeClass` flag. It is derived from the bound atoms and re-derived at emit; a
  declared class the content contradicts is a refusal, never a silent repair.
- Allocate budget to a conversion node before a reviewed 17th atom kind exists.
- Resolve a `gateQuantity`. The plan names it; `tree-resolve` reads it.
- Add a private `f(level)`. `req(t)` is a **cost ladder** and owes `ssot-power-scale.md` §10 a row
  (§10 below); nothing else in this module is power-shaped.

---

## The plan schema

> ### ⛔ This schema is FROZEN as of 2026-09-05. It has a consumer.
>
> `tree-language` was written against an unspecced producer and declared the interface it *required*
> — `quotaCell`, `requiredProperties`, `propertyVocabulary`, `mechanismFloor`, `budgetShareMilli`,
> `nodeClass`, `shapeArchetype`, `tierRequirement`, `affixIds` — saying outright that *"the names must
> be reconciled when `spec-tree-plan.md` lands."* It has landed, and **four of those nine did not
> reconcile.** They are settled here, one name at one level, and the reconciliation is the reason the
> schema is frozen now rather than at first build: a name is free to change while nobody reads it and
> expensive afterwards.
>
> | Name `tree-language` required | Settled name | Level | Note |
> |---|---|---|---|
> | `quotaCell` | `nodes[].quotaCell` | node | ✅ unchanged |
> | `requiredProperties` | `nodes[].requiredProperties[]` | node | ✅ unchanged |
> | `propertyVocabulary` | `propertyVocabulary.<axis>[]` | manifest | ✅ unchanged |
> | `budgetShareMilli` | `nodes[].budgetShareMilli` | node | ✅ name unchanged; **denominator fixed to one branch** (§5.1) |
> | `affixIds[]` | `nodes[].affixIds[]`, **1..3** | node | ✅ unchanged; `tree-catalog`'s singular `affixId` changes (R6) |
> | `nodeClass` | **`nodes[].nodeClass`** | node | ⛔ was `nodes[].class`. **Renamed.** `class` is a reserved word in several languages this data passes through, and `NodeRecord.nodeClass` is what the catalog already uses |
> | `shapeArchetype` (declared per node) | **`archetype`** | **tree** | ⛔ re-levelled. One fact per tree, not 40 copies of it |
> | `tierRequirement` (declared per node) | **`ladder.req[]`**, indexed by `nodes[].tier` | **manifest** | ⛔ re-levelled. One array of 10, not 40 copies of one lookup |
> | `mechanismFloor` | **does not exist** — read `archetypes[].mechNodes[]` | archetype | ⛔ retired (§4). A floor is not a ramp |
>
> **Every field below has exactly one name at exactly one level.** A consumer reading a name at the
> wrong level is a refusal at load, not a silent default.

`FROZEN` = the planner writes it; downstream reads it and may never write it. `HOLE` = `null` on
emit, filled by `tree-language`; the stage-2 emit gate refuses a plan with an unfilled hole.

### Manifest — `data/seed/passive-tree/plan.v1.json`

| Field | Type | State |
|---|---|---|
| `schemaVersion` | int | FROZEN |
| `plannerVersion` | string | FROZEN — bump on any math change |
| `_provenance.emittedUtc` | string | FROZEN, **excluded from `planHash`** |
| `_provenance.inputs[]` | `{path, sha256}` | FROZEN — every mirror and tuning file |
| `_provenance.tuning` | `{domain, version, sha256}` × 2 | FROZEN |
| `_pending[]` | string[] | FROZEN — declared absences (e.g. `"demonFamilies"`), never silence |
| `planHash` | string | FROZEN — sha256 over canonical manifest minus `_provenance`, plus the sorted per-tree hashes |
| `roster.aptitudes[]` / `.elements[]` / `.statuses[]` / `.demonFamilies[]` | string[] | FROZEN — read from mirrors |
| `roster.counts` | `{aptitudes, elements, statuses, demonFamilies, trees}` | FROZEN — emitted so `n` is visible without counting |
| `ladder.tierCount` | int | FROZEN — structural |
| `ladder.branches[]` | string[2] | FROZEN — `["off", "def"]`, the tokens the node id uses |
| `ladder.gateCurrency` | enum, one legal value | FROZEN — **`"aptitudePoints"`**. Any other value is refused (`R-G0`, §2) |
| `ladder.reqScalePoints` | int | FROZEN — from tuning, unit **aptitude points**. Was `ladder.kPoints` (R2) |
| `ladder.req[]` | int[10] | FROZEN — **the per-tier requirement, at manifest level.** `tree-language`'s per-node `tierRequirement` is this array indexed by `nodes[].tier` |
| `ladder.tierBudgetMilli[]` | int[10] | FROZEN — ‰ of one branch budget, Σ = 1000 |
| `ladder.branchSplitMilli` | int | FROZEN — 500 (D6) |
| `ladder.pairingRule` | enum, one legal value | FROZEN — `"power-linear-in-tier"`; structural (D20) |
| `potency.maxNodeShareMilli` | int | FROZEN — **182, ‰ of ONE BRANCH**, same denominator as `budgetShareMilli` (§5.1). A documentation constant; `P-1` checks the derivation, not the balance |
| `potency.minTerminalWidth` | int | FROZEN — 1. Emitted because `P-1` recomputes the ceiling from it and `tierCount` |
| `propertyVocabulary.<axis>[]` | string[] / int[] | FROZEN — §6's thirteen axes |
| `archetypes[].id` | string | FROZEN |
| `archetypes[].widths[]` | int[10] | FROZEN — Σ = `nodesPerBranch` |
| `archetypes[].nodeBudgetMilli[][]` | int[10][] | FROZEN — derived from `tierBudgetMilli`; ‰ of one branch |
| `archetypes[].maxNodeMilli` | int | FROZEN — the archetype's own largest node, ‰ of one branch, emitted so a reviewer compares rather than re-derives |
| `archetypes[].rewardPerPointMilli[]` | int[10] | FROZEN — §3.1's gradient, per tier, emitted so `R-A1` is a comparison and the pacing difference is visible in the diff |
| `archetypes[].mechNodes[]` | int[10] | FROZEN — **the mechanism interface.** Per-tier COUNT. There is no `mechanismFloor` (§4) |
| `quota.<axis>` | `{id: count}` | FROZEN — exact marginals, Σ = `N` |
| `trees[]` | `{treeId, treeSlug, file, generationWave, gateState, sha256}` | FROZEN — the index, ordered by `generationWave` then roster ordinal (`R-G2`) |

### Per tree — `data/seed/passive-tree/plan/<treeId>.v1.json`

| Field | Type | State | Note |
|---|---|---|---|
| `treeId` | string | FROZEN | `tree.<category>.<subject>` — the dotted id, and the file name: `data/seed/passive-tree/plan/<treeId>.v1.json`. **`.v1.json`, not `.json`** — one character, and a consumer opening the wrong one gets a missing file rather than an unfilled hole |
| `treeSlug` | string | FROZEN | `<category>-<subject>`, **no dot** — the dotted `treeId` is illegal inside a `container_id` body, so node ids compose from this (§Node ids) |
| `category` | enum(5) | FROZEN | **`primary`\|`elemental`\|`status`\|`family`\|`species`** (R7). This module emits only the first four; `species-tree` emits the fifth. ~~`aptitude`\|`element`\|`demonFamily`~~ are superseded renames — the catalog's `TreeRecord` and `tree-review`'s stratum axis both already use the five |
| `subject` | string | FROZEN | the roster entry |
| `gateQuantity` | string | FROZEN | opaque; never resolved here (§7) |
| `gateIndexKind` | enum | FROZEN | which conversion `tree-resolve` owes |
| `gateState` | enum(2) | FROZEN | `carrier`\|`pending` — whether the gate quantity has a production carrier in `src/`. `R-G1` refuses stage-2 generation for `pending` (§7.1) |
| `generationWave` | int | FROZEN | derived from `gateState`, never hand-assigned. Wave 0 is the 12 `primary` trees (`R-G2`) |
| `archetype` | string | FROZEN | `ordinal mod len(archetypes)`. **This is `tree-language`'s `shapeArchetype`, and it lives here — on the tree, not on each of its 40 nodes** |
| `budgetTotal` | int | FROZEN | `PowerVector.Total` points; identical for every tree |
| `budgetPerBranch` | int | FROZEN | `budgetTotal / 2` |
| `name` | string | **HOLE** | |
| `flavour` | string | **HOLE** | |
| `nodes[].nodeId` | string | FROZEN | **`skill.<treeSlug>-<branch>-t<tier>-<nodeKey>`** (R3, G8). ~~`<treeId>/<off\|def>/t<tier>/<index>`~~ is superseded — it used a `/` and a positional ordinal, and could not be stored. See §Node ids |
| `nodes[].nodeKey` | string | FROZEN | `[a-z0-9-]+`, **minted once** by this module within `(tree, branch, tier)` and **read back** on every regeneration. Never reclaimed, never derived from position, order or effect |
| `nodes[].branch` | enum(2) | FROZEN | |
| `nodes[].tier` | int | FROZEN | planner-side only; **must not appear in a model schema** — `MAGNITUDE_DENY_NAMES` (`pipeline/model.py:63-71`) refuses a field named `tier` regardless of type |
| `nodes[].index` | int | FROZEN | layout order within the tier. **Not part of the node id** — reordering a tier for readability moves this and nothing else (§Node ids) |
| `nodes[].parents[]` | string[] | FROZEN | G3/G5/G6; reading order, **not a gate** |
| `nodes[].nodeClass` | enum(2) | FROZEN | `mechanism`\|`magnitude`, derived (§4); re-derived at stage 3 and refused on mismatch. **One name at one level** — never `class`, which is a reserved word in several languages this data passes through |
| `nodes[].budgetPoints` | int | FROZEN | what the binder prices against |
| `nodes[].budgetShareMilli` | int | FROZEN | ‰ of one branch budget |
| `nodes[].potencyBand` | string | FROZEN | the **only** size signal the language stage ever sees — an ordinal label, never a number, per `seed-contract.md` §3 |
| `nodes[].quotaCell` | `{axis: id}` | FROZEN | §8 |
| `nodes[].permittedIds` | `{axis: string[]}` | FROZEN | what goes into stage 2's schema `enum` |
| `nodes[].requiredProperties[]` | string[] | FROZEN | e.g. `"posture:Force"` |
| `nodes[].name` | string | **HOLE** | |
| `nodes[].text` | string | **HOLE** | |
| `nodes[].affixIds[]` | string[] | **HOLE** | **1..3** (R6) — a reflect node is two atoms that must arrive together, which is why the roll unit is an affix and why one is not enough. Must satisfy `nodeClass` and price ≤ `budgetPoints` |
| `nodes[].tags` | `{k: v}` | **HOLE** | keys must come from `propertyVocabulary` |
| `nodes[].exclusion` | `{form, predicate, printedText}` \| null | **HOLE** | `EligibilityRule` shape only, never a named pair |
| `nodes[].rationale` | string | **HOLE** | review queue only; an open-loop field may never gate |

**Three rules that make the holes safe to hand over:**

1. **A `tags` key the plan never named is a rejection.** This is what keeps D14's exclusions `O(1)`.
2. **`affixIds` must price at or under `budgetPoints`** via `CostFunction.Price`, summed as
   `PowerVector.Total`, inside `ContentValidation`'s existing ±25% drift tolerance
   (`ContentValidation.cs:43-44`) — the same tolerance the item corpus already uses, for the same
   reason.
3. **`nodeClass` is re-derived from the bound atoms** and compared to the frozen value. Disagreement
   names the node and refuses.

**There is no numeric magnitude field anywhere in the schema.** That is not a policy — `audit_schema`
(`pipeline/model.py:113-206`) rejects a model schema containing one at `Pipeline.__post_init__`,
before a single call is made.

### Node ids

> ### ⛔ `<treeId>/<off|def>/t<tier>/<index>` is superseded. It could not have been stored.
>
> The old form used a `/` and a positional ordinal. **Both are refusals, not style preferences:**
>
> - `container_id`'s grammar is `^(item|trait|skill|species-passive|patron|world-buff)\.[a-z0-9-]+$`
>   (`item/seed-contract.md:131-133`, whose own note records *"Two lanes discovered this the hard
>   way"*). A `/` is illegal inside the body, and so is a dot — which rules out composing from the
>   dotted `treeId` as well.
> - A positional ordinal renumbers everything after an insertion. `data/seed/README.md:109-110`
>   already refuses that shape outright: an ordinal in use is *"refused rather than renumbered
>   underneath the content naming it."*

**The scheme, matching `spec-tree-catalog.md` §3.1 (R3):**

```text
skill.<treeSlug>-<branch>-t<tier>-<nodeKey>

  treeSlug   the tree's `<category>-<subject>` slug -- NOT the dotted `treeId`
  branch     off | def                    the two tokens of ladder.branches[]
  tier       the tier gate it sits behind
  nodeKey    allocated ONCE by this module within (tree, branch, tier); never reclaimed, never
             derived from the node's effect, its position, or its display order
```

`spec-tree-catalog.md` writes the first token as `<treeId>`. **That token is this module's
`treeSlug`** — the dotted `treeId` cannot appear in a `container_id` body, which is exactly why the
plan emits both. Same scheme, one token spelled two ways, said here so nobody composes an id from the
dotted form and gets a load refusal for a reason the grammar never explains.

> **`nodeKey` is MINTED ONCE by this module and READ BACK from the committed plan on every
> regeneration.** `--emit` against an existing plan reads each surviving node's key out of the seed
> and mints a fresh one only for a node that has none. It is never recomputed.
>
> **The read-back is a cost model, not a tidiness rule.** `tree-review`'s `O(diff)` re-review — only
> nodes whose content actually changed go back through the pipeline — is a claim about *identity
> surviving a regeneration*, and it is true only because the key comes out of the seed rather than out
> of a counter. Recompute it from position and one inserted tier-1 node re-mints every id after it,
> every unchanged node reads as new, and `O(diff)` silently becomes `O(corpus)`: **35,160 nodes back
> through review for a one-node insert, with no error anywhere to say why.**

Under D24 a re-minted id is a **migration, not a balance pass**. That is the whole reason
`topology.tierCount`, `branchCount` and `nodesPerBranch` sit in **Ask first**, and the reason
`nodes[].index` is deliberately outside the id.

---

## Reproducibility

**There is no RNG in stage 1.** Nothing is sampled, seeded or shuffled. The plan is a pure function
of these inputs, each hashed into `_provenance.inputs`:

| Input | Owner |
|---|---|
| `data/seed/aptitudes/roster.json` (12 entries, verified) | class-system, shipped |
| `data/seed/elements/roster.json` (6 entries, verified) | element hub, shipped |
| `data/seed/statuses/roster.json` (21) | **owed — this module emits it** |
| `data/seed/atoms/vocabulary.json` (7 / 16 / 13) | **owed — this module emits it** |
| `data/seed/derived-stats/catalog.json` (53 family entries, verified) | actor hub, shipped |
| `data/seed/demons/families/roster.json` | **absent — `F = 0`, declared in `_pending`** |
| `data/tuning/passive-tree.v{n}.json` | this module (its keys), `tree-resolve` and `tree-state` (theirs) |
| `data/tuning/passive-tree-targets.v{n}.json` | this module |
| `plannerVersion` | this module |
| the committed plan itself, for `nodeKey` read-back (§Node ids) | this module — an **allocation** input, not a derivation input |

Everything else — archetype assignment, tier budgets, parent edges, mechanism quotas, quota cells — is
derived arithmetic over those. `archetype = archetypes[ordinal mod k]` is the only assignment
decision, and it reads an append-only ordinal, so appending a roster entry changes no existing tree.

**Node ids are the one exception, and it is a deliberate one.** `nodeKey` is **allocated**, not
derived (§Node ids), so the committed plan is itself an input: `--emit` reads each surviving node's
key back out of `data/seed/passive-tree/plan/<treeId>.v1.json` and mints only for a node that has
none. Determinism is unaffected — a re-emit against the same committed plan is byte-identical, which
is exactly what `--check` asserts — but *reproducibility from the rosters alone is not claimed and
must not be*. A from-scratch emit into an empty directory is a **re-mint of the whole corpus**, which
under D24 is a migration. `--emit` refuses to mint a key for a node that already has one, so the
mistake is a refusal rather than a silent 35,160-node rename.

**Determinism hazards, specified rather than discovered:**

- **Per-mille rounding.** Round half up; residual to the deepest tier at the ladder level and to the
  last node at the tier level. Both rules are in §3 and both are tested.
- **Canonical JSON.** Sorted keys, 2-space indent, `\n` endings, UTF-8 without BOM, no trailing
  whitespace — otherwise the hash moves on a Windows/Linux round trip.
- **No `double`, no `float`, anywhere.**
- **`emittedUtc` is excluded from `planHash`**, or every run is drift.

**The check gate** copies the shipped contract exactly (`tools/tuning/resource_ownership.py:20-23`):
`--check` regenerates and byte-diffs, exit 1 on drift naming the first differing path. Three
assertions on top of the byte diff:

| # | Assertion |
|---|---|
| **C1** | **Equal value.** `Σ budgetPoints` identical across all `n` trees; `Σ offensive == Σ defensive` in each |
| **C2** | **Graph.** `G1`–`G9` hold for every tree, with `G9` in its corrected even form |
| **C3** | **Quota, ramp and derivation.** `mechNodes[t]` matches the ramp at every tier; the deepest tier is 100% mechanism; every axis's quota marginals re-derive exactly; `P-1` recomputes `potency.maxNodeShareMilli` and `P-2` finds no rounded share above the derived maximum. C3 asserts the ceiling's **derivation**, not a balance refusal — §5.2 |
| **C4** | **Pacing.** `R-A1` — the reward-per-skill-point spread is bounded at **every** tier, and is exactly 1000‰ at `tierCount`. C1 is the completion property; C4 is the one that walks the ladder |

`planHash` is the second, cheaper gate: a CI job comparing the hash catches drift without
regenerating, the role `_provenance.dumpHash` already plays for the species seeds.

---

## Tunables

`tunables-ssot.md` is binding: a number a balance pass would change lives in config, never as a
`const`; a structural constant stays in code **and says why it is not tunable**.

### `data/tuning/passive-tree.v1.json` — the program's ONE tunable file (R2)

**Every passive-tree tunable lives in this one file, and every key carries its unit in its name**
(tunables-ssot T6). ~~`data/tuning/passive-tree-gen.v1.json`~~ is superseded and no such file ships:
a second file is how one dial came to have two spellings across two specs, and a spelling without a
unit is how `1.2` gets written into a per-mille key. The rows below are **this module's** keys;
`tree-resolve`'s and `tree-state`'s sit beside them in the same file and are listed at the end of this
section so nobody copies one up here.

| Key | Unit | Value | Note |
|---|---|---|---|
| `tierLadder.reqScalePoints` | **aptitude points** per unit of `t(t+1)/2` | 5 | D26's `k`. Its pairing with linear per-tier power is what makes reward-per-point exactly `b/k`. The unit is in the key because the currency is the single most-confused thing in the program (§2, `R-G0`). ~~`ladder.kPoints`~~ superseded |
| `budget.treeTotalPoints` | `PowerVector.Total` points per tree | **UNMEASURED** | Ship a guess and say so, per `aptitudes.v5.json`'s own posture: *"shipping a guess is fine; calling it balance is not"* |
| `budget.branchSplitMilli` | ‰ of `budget.treeTotalPoints` to the offensive branch | 500 | D6 symmetry; a pass could try 550/450 |
| `potency.maxNodeShareMilli` | **‰ of ONE BRANCH budget** | **182** | Same denominator as `budgetShareMilli` (§5.1) — ~~91‰ of `budgetTotal`~~ was the same magnitude against the wrong denominator, a silent 2×. **A documentation constant** (§5.2): at the shipped topology it is the topology's own maximum, so it refuses nothing. `P-1` checks the derivation. **Bounded ratio — exempt under `ssot-power-scale.md` §11.6, and it refuses rather than clamps.** Lowering it below `2000/(tierCount+1)` makes it bite and is **Ask first** |
| `potency.minTerminalWidth` | count | 1 | `P-1` recomputes the ceiling from this and `tierCount`, which is what keeps the derivation a one-place edit — doc 02's two-place hazard closed |
| `potency.bandEdgesMilli[]` | ‰ of one branch budget, ascending | — | Turns `budgetShareMilli` into the ordinal `potencyBand` the language stage sees. The model never sees a number |
| `mechanism.rampStartMilli` | ‰ of a tier's nodes, at tier 1 | 0 | Tier 1 is pure magnitude. ~~`mechanism.floorMilli`~~ superseded: the old name read as a threshold and this is a **ramp** (§4), and a consumer reading `0` from a key called *floor* would have concluded every tier is a mechanism tier |
| `mechanism.rampEndMilli` | ‰ of a tier's nodes, at `tierCount` | 1000 | ⚠ Lowering it contradicts §3.5's measured conclusion and violates `R-M1`. Tunable, but the `_note` must record the owner decision beside the value. ~~`mechanism.capMilli`~~ superseded |
| `archetype.rewardSpreadMaxRatioMilli` | ‰ ratio, `max_a r_a(t) / min_a r_a(t)` | **6000** | **`R-A1`** (§3.1). The measured maximum of the shipped three — 6.0× at tier 2 — so the shipped set passes at equality and a fourth archetype that widens the gradient is refused, naming the tier and the two archetypes. Tightening it is a one-line change with a visible, named failure |
| `exclusion.targetShareMilli` | ‰ of all nodes carrying an exclusion | 20 | D14's ~2% target, restated by **D40**. `tree-review` censuses every one of them and enforces the presentation contract — both sides print the rule and name the same winner |
| `archetypeAssignment` | closed enum | `"ordinal-round-robin"` | |
| `designTarget.thetaAllIn` | `Θ` | 92 | The `s = 1` reading of `req(tierCount)/3`. `_note` carries D29's `s = 0.542` reading (`Θ ≈ 170`) so the two conventions are never confused (A15) |

### `data/tuning/passive-tree-targets.v1.json` — quota targets and gates (D32)

| Key | Unit |
|---|---|
| `quotas.nodeClass.weightsMilli` | ‰ over `{mechanism, magnitude}`, Σ = 1000 |
| `quotas.trigger.weightsMilli` | ‰ over the **11 authorable** triggers |
| `quotas.element.weightsMilli` | ‰ over `omni` + the roster elements |
| `quotas.status.weightsMilli` | ‰ over the status roster |
| `quotas.channelFamily.weightsMilli` | ‰ over the derived-stat families |
| `quotas.exclusionForm.weightsMilli` | ‰ over `{reroute, precedence, nullification}`. **All three are reachable (D40)** — ~~`nullification: 0`~~ is superseded. The rung ships **small and non-zero**, calibrated on `tree-review`'s pilot, because a generator that cannot reach it is forced to refuse a pair it can neither reroute nor order |
| `legitimateSkew.rows[]` | — | **empty for generic trees** (§8); `species-tree` owns the argument |
| `gates.cellOccupancy.medianMax` | count | 2 |
| `gates.quotaDrift.toleranceUnits` | count | 1, symmetric |
| `gates.mechanismRamp.deepestTierShareMilli` | ‰ | 1000 |
| `gates.exclusionRate.maxSharePermille` | ‰ | 30 |
| `gates.nearDuplicateRate.maxSharePermille` | ‰ | 5 |

No axis lists its own members here — a `rosterNote` records where each is read from, matching
`demon-roster-targets.v1.json`.

### Structural, in code, with the comment that says why

| Constant | Value | Why it is not tunable |
|---|---|---|
| `TIER_COUNT` | 10 | D29 and ideal §14: *"the tree's own shape… structural"*. Changing it re-mints every node id, and under D24 a re-minted id is a **migration**, not a balance pass |
| `BRANCH_COUNT` | 2 | D10; the offensive/defensive split the equal-value proof needs to keep independent |
| `NODES_PER_BRANCH` | 20 | `TIER_COUNT × mean width`; sets the corpus size and therefore every node id |
| `PAIRING_RULE` | `"power-linear-in-tier"` | Enum with one legal value. D20: pairing the ladder with *constant* per-tier power *"inverts the whole design"* |
| `_PER_MILLE` | 1000 | A denominator, not a dial |

**This deliberately disagrees with doc 02 §9.1**, which lists `topology.tierCount` and
`topology.nodesPerBranch` as tunables. The ideal's §14 table is the later, owner-facing decision and
it names them structural; D24's node-id stability is the reason the later reading is the right one.
The disagreement is named here rather than resolved silently.

**In the same file, but NOT this module's surface — R2's canonical spellings:**
`concentration.fmaxMilli` (1200), `concentration.wMilli` (500), `unlockCost.firstPoints` (5),
`unlockCost.stepPoints` (2), `soulTrack.thetaPerSoulLevelMilli` (unmeasured) and
`pointEconomy.respecPrice` belong to `tree-resolve` and `tree-state`;
`grant.skillPointsPerThetaMilliByScope` belongs to `aptitudes.v5.json`, its own domain — **its
commander value is settled at 11 and is a tunable (D38), so `squad-harness` can move it without
reopening a spec; the other three scope values are still open.** This module
reads none of them and must not restate a value for one — a copied number is what this repo already
calls *"a future drift bug with a delay fuse"*.

~~`concentration.fmax`~~, ~~`concentration.w`~~, ~~`unlockCost.first`/`.step`~~, ~~`ladder.kPoints`~~
and ~~`soulThetaWeight`~~ are superseded spellings and must not be written anywhere. **The unit
belongs in the key because dropping it is not a cosmetic loss:** `1.2` written into a bare `fmax`
yields `F = 1.0012` and passes every test either spec currently writes.

---

## Decisions implemented

| # | Decision | Where in this module |
|---|---|---|
| D1 | Free build stays | Honoured by omission — the roster has no class category (§1) |
| D2 | Four acquisition sources | Not here. `tree-state` / `tree-resolve` |
| D3 | Two unlock tracks | Half here: the plan fixes the **point** track's ladder (§2). The soul track is `tree-state` |
| D4 · D5 · D7 · D8 | Concentration `F`, `Fmax`, hybrids, `H`'s inputs | Not here. `tree-resolve` |
| D6 | The multiplier applies to all trees equally | `ladder.branchSplitMilli = 500` — offence and defence are equal by construction (§3) |
| D9 | Tree roster | §1 roster derivation, read from mirrors, never a literal count |
| D10 | Same shape everywhere | §1 topology — one archetype family, `2 × tierCount` lattice |
| D11 · D12 | Items grant points; gates read base allocation | Not here. `tree-state` |
| D13 | Deterministic-first generation | The whole module. No RNG, no model call, holes for stage 2 |
| D14 | Property-based exclusion | §6 — the plan **defines** the vocabulary; `exclusionForm` is a closed enum of three, **all three reachable under D40**, with the rung's weight small and non-zero |
| D15 | Equal expected value, not equal shape | §3 — proved as an identity, asserted by `C1`, and its inverse guarded by `archetype_shapes_actually_differ` |
| D16 | Conversion rewrites payload tags | §6 — axis emitted, **zero budget allocated** until a reviewed 17th kind |
| D17 | Species build-favour triple | `species-tree`. The quota mechanism it needs is §8's |
| D18 | Respec is a full reset | Not here. `tree-state` |
| D19 | `status_mastery` as a fifth `AllocationScope` | **Superseded by D35.** No requirement in this module |
| D20 | Quadratic tiers, linear per-tier power | §2 — the *indexing* is superseded by D26; the **pairing rule survives and is structural** |
| D21 | Every actor carries its own tree state | Not here. `tree-state` |
| D22 | Compose from the shipped atom catalog | §4 and §6 — every atom axis is the shipped vocabulary; no passive-specific one is invented |
| D23 | Species trees get their own pipeline | `species-tree`. This module supplies the shape and the ladder |
| D24 | The catalog is static, shared, identical | §Reproducibility — zero RNG, committed artifact, `--check` gate, and node ids **allocated once and read back** (§Node ids), never derived from position |
| D25 | Rising unlock cost | Not here. `tree-state`. The plan deliberately prices **no** unlock |
| D26 | `req(t) = k·t(t+1)/2` | §2, with `W/req = b/k` verified at every tier including 8, 9 and 10 |
| D27 | The roster ships whole | §1 — corpus stated as a function of `n` and `F`; `F = 0` is declared in `_pending`, never assumed |
| D28 | Cross-unlock credits one mate | Not here. `tree-resolve` |
| D29 | 10 tiers × 2 branches, ~40 nodes | §1, **with the `G9` correction**: 40 exactly, rootless, and even by construction |
| D30 | Every species gets a full tree | `species-tree`. The 40-node shape it inherits is §1's |
| D31 | `status_mastery` at slot 6 | **Superseded by D35.** No requirement in this module |
| D32 | Near-uniform with a named theme allowance | §8 + `passive-tree-targets.v1.json`; `legitimateSkew.rows` ships empty for generic trees, with the reason |
| D33 | The squad harness is not a gate | `squad-harness`. Every number it settles is named here as a tunable key with a unit, so it lands without reopening this spec |
| D34 | `skillPointsPerTheta` becomes per-scope | Not here. `tree-state` |
| D35 | Status trees gate on their own quantity | §7 — `gateQuantity: "status_applied.<id>"`, deliberately outside `AllocationScope` |
| D36 | The unlock-cost curve | Not here. `tree-state` |
| D37 | The two missing gate quantities are built inside this program | §7, §7.1, `R-G2`'s wave table and §9 item 7 — `gate-counters` owns them, the corpus order is a schedule, and all 39 trees are reachable |
| D38 | `g = 11` at commander scope, tunable | Not here. `aptitudes.v5.json` owns the key; this module names it in §Tunables and states no value for it |
| D39 | `H` reads the final allocation, self-spent only | Not here. `tree-resolve`. Nothing in the plan is order-sensitive, so nothing here changes |
| D40 | All three exclusion forms kept; nullification printed loudly | §6's `exclusionForm` axis and §Tunables — the rung is reachable, the ~2% target stands, and `tree-review` censuses and enforces the presentation |
| D41 | `speciesUniqueAffixMin = 8`, tunable | Not here. `species-tree`. The deepest-mechanism-first marking it relies on reads this module's `mechNodes[]` and `nodeKey` (§4, §Node ids) |

**Ids I could not place as a requirement anywhere in stage 1:** **D19** and **D31**, and in both
cases because they are superseded by D35 rather than because they have no home. Every other id lands
either as a requirement above or as a named other module's.

---

## What this module owes elsewhere

| # | Owed | To whom | Blocks |
|---|---|---|---|
| 1 | `data/seed/statuses/roster.json` — the 21-status mirror | this module emits it (`ElementEnumGen` pattern) | emitting a complete plan |
| 2 | `data/seed/atoms/vocabulary.json` — the 7 / 16 / 13 mirror | this module emits it | emitting `propertyVocabulary` |
| 3 | A **§10 cost-ladder row for `req(t)`** in `ssot-power-scale.md` | the power SSOT | shipping. Row 6's precedent (`XpToNext`) is exact: a threshold on an already-`Θ`-derived quantity that never multiplies a magnitude |
| 4 | A **§11.10 content-breadth row** for the ten authored tiers | the caps register | shipping. The honest verdict: nothing is refused, and past `Θ ≈ 300` growth moves to the uncapped soul track — but §11.10a is explicit that a breadth verdict expires when its premise does, and generated content is exactly that premise |
| 5 | A closed demon-family roster, or D27's curation sequenced | owner / build order | `F > 0`. Not a blocker on emitting a plan for the 39 closed-roster trees |
| 6 | `mechanism-wiring`'s four inert lines | wave 0 sibling | nothing here, but without them the mechanism budget buys nodes nothing can score (§4) |
| 7 | `gate-counters`' two quantities — `element_mastery` and `status_applied.<id>` (D37) | wave 0 sibling | nothing here — `--emit` plans a `pending` tree for free. It gates **generation waves 1 and 2**, i.e. 1,080 of the 1,560 generic nodes (§7.1) |

Items 1 and 2 are this module's own work. Items 3 and 4 are reviewed changes to another document and
must land before this module ships — `guard-power.ps1` cannot catch either absence, because its
G2/G3 checks key on a parameter named `level`/`lvl`/`index` and `req(t)`'s parameter is `t`.

---

## Success criteria

- [ ] `python -m seedsmith trees plan --check` exits 0 against the committed plan, and exits 1 naming
      the first differing path when any hashed input moves by one byte.
- [ ] Two emits produce byte-identical output, including on a Windows/Linux round trip.
- [ ] `C1` passes: all `n` trees carry an identical `budgetTotal`, and `Σ offensive == Σ defensive` in
      every one.
- [ ] `equal_value_holds_for_any_width_vector` passes for tier counts 1..40 and arbitrary width
      vectors — the property is asserted as an identity, not as an instance.
- [ ] `archetype_shapes_actually_differ` passes: the strongest node differs by ≥ 2× across the
      archetype set, so equal value has not collapsed into equal shape.
- [ ] Every tree emits exactly **40** nodes, `G9` holds in its corrected even form, and no node at
      tier 1 has a parent.
- [ ] Reward per point is `b/k` at every tier from 1 to 40, asserted as exact integer equality.
- [ ] `P-1` passes: `potency.maxNodeShareMilli` is **recomputed** from the emitted `tierCount` and
      `minTerminalWidth` at check time rather than read from the file, and `P-2` finds no rounded
      `budgetShareMilli` above the derived maximum at any tier count 1..40. Both are implementation
      guards — at the shipped topology the ceiling refuses nothing, and §5.2 says so rather than
      shipping a tautology in the shape of a check.
- [ ] `R-A1` passes: the reward-per-skill-point spread is walked at **every** tier, not only at
      `tierCount`, and stays within `archetype.rewardSpreadMaxRatioMilli` (6000‰). The 6.0× gradient
      at tier 2 is emitted in `archetypes[].rewardPerPointMilli[]`, so a fourth archetype's pacing
      cost is visible in the diff before it is argued about.
- [ ] `R-G0` passes: `ladder.gateCurrency` is `"aptitudePoints"`, and a plan asserting anything else
      exits 3 naming the field.
- [ ] Every `nodeId` matches `^skill\.[a-z0-9-]+$`, and a re-emit after an insertion re-mints no
      surviving node's `nodeKey`.
- [ ] `R-G1`/`R-G2`: every tree carries a `gateState` from a checked-in evidence row, `trees[]` is
      ordered wave-0-first, and stage 2 exits 3 when asked to generate content for a `pending` tree.
- [ ] The deepest tier of every archetype is 100% mechanism, and `mechShareMilli` is monotone.
- [ ] `scripts/audit-magic-numbers.py --domain passive-tree` reports zero findings in this module.
- [ ] Not one roster count appears as a literal in this module's source.
- [ ] A missing roster mirror exits 2 naming the file; an absent family roster emits `_pending`
      rather than an unexplained zero.
- [ ] `ssot-power-scale.md` §10 carries the `req(t)` row and §11.10 carries the authored-depth row
      before this module is called done.
- [ ] The plan contains no field a magnitude could hide in, and `tree-language`'s schema derived from
      it passes `audit_schema` unchanged.

---

## Open questions

Only questions a **decision** can close. Everything else below the line is a task with an owner.
**One is open.** The second is kept below with its answer, because a closed question that vanishes
gets re-asked.

1. **`budget.treeTotalPoints` — the one number nobody can measure yet.** It is `PowerVector.Total`
   points per tree, and no measurement can produce it until trees actually carry power in the
   resolver (ideal §3.5: *"re-measure only worthwhile once mechanism nodes exist"*). The proposal is
   to ship a flagged guess with the posture `aptitudes.v5.json` already established —
   *"shipping a guess is fine; calling it balance is not"* — and re-measure once `mechanism-wiring`
   and `squad-harness` land. **This needs an owner nod on the posture, not on the number.**

### Closed 2026-09-05

2. ~~**Does `tree-review`'s deep-tier behavioural sample gate, or report?**~~ **Closed: it reports.**
   §4's known weakness is real — the mechanism quota is structural while the value is behavioural —
   and a sampled `CombatSim` score over deep-tier mechanism nodes is the only check that reaches it.
   **A below-threshold sample files a review finding; it does not block the catalog.** One line of
   justification: a sampled behavioural score is a *proxy for a proxy* (a win-share delta at one
   scope, over a sample, of a quota that is itself a stand-in for interestingness), and this program
   has already ruled twice that a measurement that thin presents rather than refuses — D40 answers
   the *"reads like a bug"* risk with presentation instead of removal, and `tree-review`'s own ladder
   escalates a systemic finding to a **prompt fix** (rung 4) rather than to a shipping block.
   `tree-review` §4.2 carries the rule; the sample still stops a lot the moment it is *systemic*,
   through that ladder rather than through a gate on this module's quota.

**Not open — tasks with owners, listed so they are not mistaken for questions:** the two roster
mirrors (§9 items 1–2, this module's own work); the `ssot-power-scale.md` rows (items 3–4, reviewed
changes with the argument already written above); the demon-family roster (item 5, D27 sequences it
as build-order work); the `tierCount`-structural-vs-tunable disagreement between the ideal §14 and
doc 02 §9.1 (resolved in §Tunables, with the reason).

---

## Design-gate checklist

```
[x] I identified the subsystems this touches - passive trees, the power ladder, tunables, effect
    atoms, the class system's point economy, the status and element rosters.
[x] I read every doc in the DESIGN-GATE §1 row(s) this session: DESIGN-GATE.md, passive-tree-map.md,
    passive-tree-ideal.md (all of it), research 02 (all), 03 (§0-§3, §5), 10 (§0-§2, §7),
    ssot-power-scale.md §10/§10.2/§10.3/§11.6/§11.9/§11.10, tunables-ssot.md §1-§4,
    decisions.md (searched for a passive-tree lock; there is none).
[x] I checked decisions.md for a lock covering this. None exists for the passive tree program;
    the class-system row (:103) and the action-slot row (:116) were read and neither constrains it.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments. Counted this session: 12 aptitudes
    (Aptitude.cs:40-51 rows AND roster.json entries), 6 elements (ElementTable.cs:125-130 AND
    roster.json), 21 statuses (StatusCatalogBootstrap.cs:16-58 -- 8 UnityCc + 8 overlay + 5
    contagion), 7 attach points (AtomKind.cs enum members), 16 kinds (AtomKindRegistry.cs:476-869,
    enumerated), 13 triggers (AtomKind.cs AtomTriggers, 11 authorable), 53 derived-stat families
    (catalog.json entries), 3 semantic atom tag values across 740 seed files.
[x] I read the surrounding section of every rule I quoted -- ContentValidation's "never a generation
    input", ssot-power-scale §11.6/§11.10 including §11.10a's expiry warning, tunables-ssot §1's
    grey-zone tiebreaker, and doc 02 §2.1's "edges are layout, not gating".
[x] I tested (not assumed) the arithmetic I report. req(t), W/req at every t including 8/9/10, the
    tierBudgetMilli column and its zero residual at ten tiers, all three archetypes' budget sums and
    largest nodes, the mechanism ramps, the potency ceiling at T=10, T_max at eight Theta values,
    and the corpus sizes were all COMPUTED, not quoted.
[x] Nothing contradicts a §2 invariant. PS-3 holds (the plan emits shares; the runtime reads P(Θ)).
    PS-4 holds (PowerVector prices are relative and never multiplied by contentScale). PS-8 holds
    (no magnitude is capped; the potency ceiling is a bounded ratio that refuses rather than clamps,
    and it says so). The one place this document corrects something WRITTEN is doc 02's G9, and it
    is corrected explicitly in §1 with the arithmetic shown, not worked around.
[ ] Corrections are propagated -- PARTIAL, and deliberately so. This spec's own prose, schema,
    testing, boundaries and success criteria all carry the rootless-40 G9, the 182-per-mille-of-one-
    branch ceiling, R3's node ids, R2's tunable names and R6/R7. It does NOT edit
    passive-tree-ideal.md (D29's "~40"), passive-tree-map.md, or research docs 02/10, because this
    task's scope is one file. Those three edits are owed and are listed here so the propagation is a
    tracked line rather than a forgotten one. The sibling corrections this spec now depends on --
    tree-language dropping `mechanismFloor`, tree-catalog's `affixId` becoming `affixIds[]`, and
    tree-binder dropping `tierWeight`/`weightTotal` -- are being made in the same fold, and each is
    named at the point in this spec that relies on it.
[x] D37-D41 folded 2026-09-05. D37 rewrote 7.1 from a permanent hole into a schedule owned by
    `gate-counters` and moved R-G2's "unblocked by" column onto it; D38's commander value is named
    in Tunables as another domain's settled tunable; D40 made the `nullification` rung reachable
    (its quota weight is no longer pinned at 0); D41 is named as `species-tree`'s and its
    dependency on `mechNodes[]`/`nodeKey` is recorded. Open question 2 is closed -- the deep-tier
    behavioural sample REPORTS -- and question 1 is left open, because nobody has answered it.
```
