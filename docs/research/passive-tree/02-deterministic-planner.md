# Passive trees — stage 1, the deterministic planner (2026-09-05)

**Status:** research. Not a spec, no build authorized. Answers the owner's question:
*"how to make tree correctly? how math and deterministic engine make the plan?"*

**Scope:** everything that happens **before** an LLM sees anything. The output of this stage is a
single reproducible JSON artifact — the **plan** — that fixes shape, budget, tier ladder, node class
and property vocabulary, and leaves holes for stage 2 to fill.

**Owner constraint added 2026-09-05:** *the tree catalog is STATIC, SHARED and identical for every
player.* Concrete values are baked before the game runs so builds can be learned and planned. This is
**not** the loot model. The planner therefore emits a deterministic artifact, never a runtime roll.

**Evidence marking** follows [../passive-tree-prior-art-2026-09-04.md](../passive-tree-prior-art-2026-09-04.md):
**FACT** = read from cited code or a cited first-tier source this session. **INFERENCE** = derived
from a fact. **RECALL** = general knowledge, not verified in-repo.

---

## 0. Answer up front

| Question | Answer |
|---|---|
| **Real roster `n`** | **39 trees are derivable from closed rosters today** — 12 aptitudes + 6 elements + 21 statuses. Demon families add `F`, and **`F` does not exist as a closed roster**: `family` is declared an *open* axis and the shipped corpus carries **699 distinct family tokens over 841 entries**. `n = 39 + F` and the planner cannot read `F` |
| **Real point supply** | **3 aptitude points per `Θ`** (commander scope) and **1 skill point per `Θ`**. At `Θ=100`: 300 aptitude points, 100 skill points |
| **Recommended topology** | **7 tiers × 2 branches, 14 nodes per branch, 29 nodes per tree** (LE parity) as a **layered DAG**, not a strict tree. Corpus **1,131 nodes at n=39**; **1,682 at n=58** |
| **Tier count the ladder supports** | **The ladder never runs out** — `Θ` is uncapped by PS-8, and `T_max ≈ √(1.2·Θ)` for an all-in build. What must be chosen is the *authored* depth. `tierCount = T_max(Θ_designTarget, s=1)`; 7 tiers is the depth an all-in build reaches at **`Θ ≈ 39`** |
| **What is conserved** | The **scalar `PowerVector.Total`** of a tree, and its **50/50 offence/defence split**. Not the category vector — that is where identity lives |
| **How shape can differ while value is equal** | **`D20` freezes the per-tier budget; the width vector `w[t]` is free.** A tier's budget is `B_b·t/T_tri` regardless of `w`, so every archetype sums to the same total **by construction, not by check** |
| **Mechanism vs magnitude** | **Commensurable enough to conserve, not commensurable enough to substitute.** One budget + a **class quota** that the budget may not trade against. A quota needs no exchange rate; two budgets would |
| **Node potency ceiling** | `maxNodeShare = 1/((tierCount+1)·w[tierCount])` of the tree budget — **derived from the topology**, `0.125` at the recommended shape |
| **Reproducibility** | Zero RNG. Inputs are three roster mirrors + one tuning version + the planner version. Gate is `--check`: regenerate and byte-diff, exit 1 on drift, exactly as `tools/tuning/resource_ownership.py` already does |

**Three things that contradict what is currently written down** — detail in §11:

1. `DESIGN-GATE.md`'s atom row says *"5 attach points, 12 kinds, **8 triggers**"*. The code says
   **7 / 16 / 13** (`AtomKindRegistry.cs:21`, `:31`, `:36`). Code beats docs.
2. `D9` says *"each demon family"*. There is no closed family roster; `family` is specified as an
   **open** axis (`spec-anchor-contract.md:58`) and the corpus has 699 distinct values.
   **`D9` is not executable as written.**
3. `D20`'s ladder is **not** flat reward-per-point at tier 1. `W(1)/req(1) = 0.100·b` against an
   asymptote of `0.200·b` — **tier 1 is a 2× worse deal than every tier after it.** Deliberate entry
   tax or off-by-one; the owner should say which. §4.3 gives the one-character fix if it is the latter.

---

## 1. Roster derivation (D9) — counted, not recalled

`D9`: *"12 primary + all elemental + all status + each demon family"*. The generator must **read**
the rosters (ideal §8: *"Twelve is a measured outcome, not a decision"*). Counted this session:

| Roster | Count | Source of truth (code) | Checked-in mirror the planner can read |
|---|---|---|---|
| **Aptitudes** | **12** | `src/FusionRpg.Core/Stats/Aptitudes/Aptitude.cs:36` — `Count = PostureCount × PerPosture` = `3 × 4`; the twelve rows are `:40-51` | `data/seed/aptitudes/roster.json` — 12 entries ✅ |
| **Elements** | **6** | `src/FusionRpg.Core/Combat/Element/ElementTable.cs:125-130` — fire, ice, air, earth, light, dark | `data/seed/elements/roster.json` — 6 entries ✅ |
| **Statuses** | **21** | `src/FusionRpg.Core/Status/StatusCatalogBootstrap.cs:16-58` — counted: 8 UnityCc + 8 overlay-authored + 5 contagion | ⛔ **none exists.** `find data -iname "*status*"` returns only `data/seed/atoms/fx-status.json` and `data/tuning/status.v1.json` |
| **Demon families** | ⛔ **not a roster** | see below | — |

**FACT — the aptitude count is computed, not typed.** `AptitudeCatalog.Count` is a product of
`PostureCount = 3` and `PerPosture = 4` (`Aptitude.cs:30-36`), with the comment saying so: *"a
thirteenth aptitude or a fourth posture changes this by construction rather than by a second,
forgettable edit."* The planner must read the roster file, never the literal 12 — and the precedent
exists: `data/tuning/set-charm-gen.v1.json`'s `populations.aptitudeCountNote` refuses to transcribe
12 for exactly this reason.

### 1.1 ⛔ `D9`'s demon-family clause has no closed roster to read

**FACT.** `data/seed/demons/species/*/*.json` — **503 files, 841 entries**, counted this session.
The `family` field is an **array of free-text strings**:

```json
"family": ["Nut-type", "Heavy Artillery"]
```

Counted over all 841 entries:

| Measure | Count |
|---|---|
| Distinct `family` tokens (all positions) | **699** |
| Distinct **first** `family` token | **525** |
| Species per first-family (mean) | **1.60** |
| Largest first-family | `undead`, 63 entries |

**FACT — this is by design, not by drift.** `docs/architecture/demon-seed/spec-anchor-contract.md:58`
classifies `family` as **`CLASSIFIED, open` — *"grows organically"*, not enumerable**, and
`spec-classify-pipelines.md:86` names the rule `family-open`: *"a new `family` value is allowed and
recorded, never rejected — none, the axis is open by construction."*

**FACT — the intended shape was ~19.** `spec-roster-metrics.md:38` targets *"no family holding more
than ~10%"* with *"19 families over 904"* as the reference figure. The shipped corpus is 27× that.

**INFERENCE — consequence for the planner.** A per-family tree at 525 families would be 525 trees of
1.6 species each: not identity, just noise. `D9` is therefore **not executable as written**, and the
gap is upstream of this program. Two ways out, both owner decisions:

| Option | What it means |
|---|---|
| **A — declare a closed family roster** | `data/seed/demons/families/roster.json`, ~15–25 curated ids, each species' free-text `family` mapped onto one. The planner reads the roster; the corpus keeps its open descriptive field |
| **B — drop family trees; use `DemonType` scope directly** | `AllocationScope.DemonType` already exists (`AptitudeAllocation.cs:8`) and its budget rate ships (`PointBudget.PointsFor`). One tree per *species* is `D23`'s deferred round, so this leaves the demon axis entirely to `D23` |

**Recommendation: A, with `F ≈ 19`**, because it is the figure `spec-roster-metrics.md` already
argued for and it keeps `D9` intact. Then **`n = 39 + 19 = 58`**.

### 1.2 The real `n`, stated three ways

```text
n_closed  = 12 + 6 + 21           = 39     ← what the planner can read TODAY
n_target  = 39 + F                = 58     ← with a closed family roster at F = 19
n_max     = 39 + 525              = 564    ← if "family" is taken literally  ⛔ not viable
```

The ideal's *"`n ≈ 40–60`"* (§3.1) is **confirmed** for `n_target`, and `n_closed = 39` sits just
under it. `H`'s no-normalization argument holds at both (§3.1: the `1/n` term is a rounding error and
dropping it removes every `n` dependence, so the roster may grow forever).

### 1.3 Gate-quantity coverage — a wiring gap, not a wall

Each tree needs a **gate quantity** for `req(t)` to read (ideal §5). Verified against code:

| Tree category | Count | Gate quantity | State in code |
|---|---|---|---|
| Primary (aptitude) | 12 | Commander-scope aptitude points | ✅ shipped — `PointBudget.PointsFor(AllocationScope.Commander, …)` |
| Elemental | 6 | `Aspect` scope / `element_mastery` | ⚠️ scope **exists** (`AptitudeAllocation.cs:8`), **source does not**. `PointBudget.cs:14-18`: *"Aspect's own source (`element_mastery`) is owned by the demon program's `aspect-scope` module and does not exist yet"* |
| Status | 21 | `status_mastery` (D19) | ⛔ **not in the enum.** `AllocationScope` has exactly 4 values |
| Demon family | F | `DemonType` / almanac XP | ✅ rate shipped; source supplied by caller |

**INFERENCE.** 27 of 39 trees have no live gate source. Per `CLAUDE.md`'s RPG-layer rule this is a
**wiring gap**, not an architectural wall — the scope enum takes a fifth value and the rate table
takes a fifth row (`D19` already priced that: *"the shipped four-row per-scope rate table grows to
five"*). The planner does not need it to *emit a plan*; the runtime needs it to *enforce one*.

---

## 2. Topology (D10)

### 2.1 Is the tree a tree, a DAG, or a layered graph?

**Recommend a layered DAG: tiers are layers, edges run only `t → t+1` within one branch, one shared
root, no cross-branch edges.**

The justification is that **the gate is already a threshold, so no edge can be load-bearing**:

- **FACT (ideal §4):** a tier opens on *(a)* the tier below being unlocked and *(b)* the actor's own
  base allocation in that tree's gate quantity. The gate reads a **quantity**, not a parent node.
- **FACT (prior art §1, Last Epoch):** *"Prerequisites are points-thresholded, not node-thresholded"*
  — predecessors carry a minimum points-invested requirement rather than a binary allocated flag.

If reachability is decided by a per-tier threshold, then a node at tier `t` is reachable exactly when
tier `t` is open, whatever the edges say. **Edges are therefore layout and reading order, not
gating.** Calling the structure a strict tree would claim a constraint the runtime does not enforce.

Why not the alternatives:

| Shape | Rejected because |
|---|---|
| **Strict tree** (one parent each) | Forces a unique path to any deep node, so a "spiked" archetype can only be reached by buying filler. That is PoE's *implicit* concentration tax (prior art §2.5), and we deliberately replaced it with an explicit, measurable `F` (D4) |
| **General DAG** (cross-branch edges) | Couples the offensive and defensive budgets. §4's equal-value proof needs the two branch budgets to be independent; a cross edge makes "how much of this tree is offence" undecidable |

### 2.2 Graph invariants the plan must satisfy

Machine-checkable, all of them, at emit time:

| # | Invariant |
|---|---|
| G1 | Every node has exactly one `tier ∈ [1, tierCount]` and one `branch ∈ {offensive, defensive}` |
| G2 | Per-tier, per-branch node count equals the archetype's declared width `w[t]` exactly |
| G3 | Every node at `t > 1` has ≥1 parent at `t−1` **in the same branch** — no orphan |
| G4 | Every node at `t < tierCount` has ≥1 child at `t+1` in the same branch — no dead end above the crown |
| G5 | Every edge satisfies `tier(child) = tier(parent) + 1` — no skips, hence acyclic by construction |
| G6 | No edge crosses a branch; the root is the only node with no parent and belongs to no branch |
| G7 | Every node is reachable from the root by following parents (implied by G3+G6, asserted separately so a violation names the node) |
| G8 | Node ids are unique across the whole corpus and derived only from `(treeId, branch, tier, index)` |
| G9 | `Σ_t w[t]` equals `nodesPerBranch` for every tree — the corpus size is `n × (2·nodesPerBranch + 1)`, exactly |

`G9` is what makes the corpus size a stated number rather than an outcome.

### 2.3 Three concrete topologies, with real corpus sizes

`nodesPerTree = 2 × nodesPerBranch + 1` (the `+1` is the shared root that both branches hang from).

| | Tiers | Per-branch width `w` | nodes/branch | nodes/tree | corpus @ `n=39` | corpus @ `n=58` |
|---|---|---|---|---|---|---|
| **T1 · Compact** | 5 | `2,2,2,2,2` | 10 | **21** | 819 | 1,218 |
| **T2 · LE-parity** ⭐ | 7 | archetype-dependent, `Σ=14` | 14 | **29** | **1,131** | **1,682** |
| **T3 · Broad** | 7 | `Σ=19` | 19 | **39** | 1,521 | 2,262 |

Against a player's actual affordance — **`Θ` skill points**, one per `Θ` (§3.1):

| | unlocks affordable @ `Θ=100` | share of a `n=58` corpus | exclusion rules @ 2% target (D14) |
|---|---|---|---|
| T1 | 100 | 8.2% | 24 |
| **T2** | 100 | **5.9%** | **34** |
| T3 | 100 | 4.4% | 45 |

**Tradeoffs, plainly:**

- **T1** keeps the generated corpus under ~1,200 nodes and is the cheapest to author and review, but
  5 tiers means `req(5) = 60` — an all-in build maxes a tree at `Θ = 20`, the item calibration
  point. Depth stops mattering almost immediately.
- **T2** matches the only shipped comparator's density (**FACT**, prior art §7 open item 1: Last
  Epoch ships ~29 nodes per tree), lands the corpus at the same order of magnitude as the 841-entry
  species corpus this repo has already generated, and `req(7) = 115` keeps the crown aspirational.
- **T3** buys width at the cost of per-node distinctiveness: 2,262 nodes over a 6-element / 21-status
  / 12-aptitude property space means many near-duplicates, which is the failure the action program's
  `dedup-select` fingerprint (`tools/seedsmith/.../distribution_planner/fingerprint.py`) exists to catch.

**Recommend T2.** Owner decision #1 in the ideal §7 was *"tree size — skills per tree and tiers per
branch"*; this is the concrete proposal against it.

---

## 3. The tier ladder (D20) — verified arithmetic

### 3.1 The point supply, from code

**FACT.** `data/tuning/aptitudes.v5.json:15-17`:

```json
"grant": { "aptitudePointsPerTheta": 3, "skillPointsPerTheta": 1 }
```

**FACT.** `data/tuning/aptitudes.v5.json:22-27` — the per-scope table `PointBudget` actually reads:

```json
"aptitudePointsPerThetaMilliByScope": { "commander": 3, "demonType": 4, "aspect": 4, "uniqueDemon": 6 }
```

**FACT — despite the name, "Milli" does not divide.** `PointBudget.PointsFor` is
`checked { return sourceValue * rate; }` (`PointBudget.cs:37-38`), and the field remark says so
explicitly: *"for why this does NOT divide by 1000 despite the 'Milli' name."* Commander rate is
therefore **3 points per `Θ`**, agreeing with the `grant` block.

**FACT — `grant.aptitudePointsPerTheta` has no production reader.** It is parsed
(`AptitudeTuning.cs:155`) and asserted in one test (`AptitudeTuningTests.cs:89`); nothing else reads
`AptitudeGrant.AptitudePointsPerThetaMilli`. The tuning file's own `_note` says the same. The
effective rate is the `pointEconomy` one — and both are 3, so nothing is currently wrong.

**FACT — `skillPointsPerTheta` is parsed and has zero consumers.** `AptitudeTuning.cs:156`;
the only other hit in `src/`/`tests/` is `AptitudeTuningTests.cs:90`. This is exactly the spender
`D2` was written to supply.

```text
aptitudePoints(Θ) = 3 · Θ        (commander scope)     ← the GATE quantity
skillPoints(Θ)    = 1 · Θ                              ← the UNLOCK currency
```

**Two different quantities, and conflating them is the easy mistake.** `req(t)` gates on *aptitude
points allocated to that tree's gate quantity*; nodes are bought with *skill points*. The published
sweep (`tools/HybridViability/Program.cs:216-234`) models the gate with aptitude points, which is
correct for the primary trees it swept.

### 3.2 `req(t)` — the arithmetic, verified

```text
req(t) = 10 + 2.5·t·(t−1)
```

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 |
|---|---|---|---|---|---|---|---|---|---|---|
| `req(t)` | 10 | 15 | 25 | 40 | 60 | 85 | 115 | 150 | 190 | 235 |
| 1st diff | — | 5 | 10 | 15 | 20 | 25 | 30 | 35 | 40 | 45 |
| 2nd diff | — | — | 5 | 5 | 5 | 5 | 5 | 5 | 5 | 5 |

**Confirmed.** `D20`'s sequence `10 · 15 · 25 · 40 · 60 · 85 · 115` reproduces exactly, and the
second difference is constant at 5 — quadratic, as `D20` states.

**FACT — it is integer-exact, and it is the same shape as `P(Θ)`.** `t(t−1)` is a product of
consecutive integers and therefore always even, so `2.5·t(t−1) = 5·t(t−1)/2` is always an integer
multiple of 5. Rewritten in the ladder's own vocabulary
([`ssot-power-scale.md`](../../architecture/power/ssot-power-scale.md) §4.2, `P(Θ) = C + A·Θ + B·Θ(Θ−1)/2`):

```text
req(t) = reqBase + reqStep · t(t−1)/2       reqBase = 10, reqStep = 5
       ≡ P-shape with C = 10, A = 0, B = 5
```

Same integer-exactness argument as §4.7 of the SSOT, for the same reason.

### 3.3 How many tiers does the ladder support?

**It never runs out.** `Θ` is uncapped by `PS-8` and by owner decision (§10.6: *"No cap. `Wr = 0.25`,
uncapped — 'this game is infinity grind'"*). Inverting `req`:

```text
T(p) = ⌊ 0.5 + √( 0.25 + (p − 10)/2.5 ) ⌋ ,     p = s · 3Θ
                                                s = share of the point budget in this tree
⇒ for an all-in build (s = 1):   T ≈ √(1.2 · Θ)
```

Computed, not estimated:

| `Θ` | budget `3Θ` | `T_max` (all-in) | `√(1.2Θ)` | `T` (2-way, `s=0.5`) | `T` (even-12, `s=1/12`) |
|---|---|---|---|---|---|
| 20 | 60 | **5** | 4.9 | 3 | **0** — below `req(1)` |
| 50 | 150 | **8** | 7.8 | 5 | 2 |
| 100 | 300 | **11** | 11.0 | 8 | 3 |
| 200 | 600 | **15** | 15.5 | 11 | 5 |
| 500 | 1,500 | **24** | 24.5 | 17 | 7 |
| 1,000 | 3,000 | **35** | 34.6 | 24 | 10 |

Two readings worth naming:

- **`T ∝ √Θ`.** Depth is a *square-root* reward on `Θ` while the tree's magnitude per tier reads
  `P(Θ)` (quadratic). The two do not fight; they are different axes, which is the same separation
  `PS-3` makes between contests and magnitudes.
- **At `Θ = 20`, an even-twelve build unlocks nothing at all** — `300/12 = 25` points per tree is
  below `req(1) = 10`… no: `60/12 = 5` is below 10. This is a *feature* that answers §3.3's measured
  problem head-on: the current model rewards spreading, and the tier gate is the first mechanic in
  the design that punishes it, at low `Θ`, for free.

**So the question is not "how many tiers fit" but "how deep do we author".** The sizing rule:

> **`tierCount = T_max(Θ_designTarget, s = 1)`** — author to the depth an all-in build reaches at the
> `Θ` band the content targets. Deeper is dead content; shallower and the gate stops being a decision.

`tierCount = 7` corresponds to `Θ_designTarget ≈ 39` (`req(7) = 115`, `115/3 = 38.3`). That is the
owner's own written sequence, and it is defensible: a 7-tier catalog is fully open to an all-in build
in the late-early game and still aspirational to a 12-way spread until `Θ ≈ 460`.

### 3.4 ⛔ The pairing rule is *almost* flat — and tier 1 is a 2× worse deal

`D20`'s binding pairing rule: per-tier power grows **linearly** with tier. Cumulative tree power
through tier `T` is therefore triangular (this is the form the sweep used,
`HybridViability/Program.cs` §"tree power, linear per tier"):

```text
W(T) = b · T(T+1)/2
```

`D20` claims this yields *"flat reward-per-point at every depth"*. Computed:

| `T` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 10 | 12 | → ∞ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| `W(T)/b` | 1 | 3 | 6 | 10 | 15 | 21 | 28 | 36 | 55 | 78 | — |
| `req(T)` | 10 | 15 | 25 | 40 | 60 | 85 | 115 | 150 | 235 | 340 | — |
| **`W/req` (`×b`)** | **0.100** | 0.200 | 0.240 | **0.250** | **0.250** | 0.247 | 0.244 | 0.240 | 0.234 | 0.229 | **0.200** |

**It is flat from tier 2 onward** — 0.200 → 0.250 → 0.200, a ±11% band about the midpoint, which is
as flat as a design ever needs to be. **Tier 1 is the exception: 0.100, exactly half the asymptote.**

**Why.** The power ladder is indexed on the triangular number `T_t = t(t+1)/2`; the requirement
ladder is indexed on `T_{t−1} = t(t−1)/2` plus a base of 10. The two triangular indices are **off by
one**, and the base of 10 is the entry tax.

**The one-character fix, if flatness is what was wanted:** index the requirement on the same
triangular number as the power.

```text
req'(t) = reqStep · t(t+1)/2          reqStep = 5  →  5, 15, 30, 50, 75, 105, 140
⇒ W(T)/req'(T) = b/reqStep = 0.200·b   EXACTLY, at every tier, forever
```

**This is an owner decision, not a defect to fix silently.** Both readings are defensible:

| Keep `req(t) = 10 + 2.5t(t−1)` | Move to `req'(t) = 5·t(t+1)/2` |
|---|---|
| Tier 1 is a deliberate commitment threshold — you pay 10 before the tree gives anything | Reward-per-point is *provably* constant, which is the property `§10.5` earns for the XP/power pair |
| Matches the sequence the owner wrote down | Entry is cheaper (5), so a low-`Θ` spread build is less punished — which cuts against §3.3's finding |

**Recommendation: keep the owner's sequence, and stop claiming exact flatness.** State the real
property: *"reward per point is flat within ±11% from tier 2 on; tier 1 costs double, on purpose."*
Both `reqBase` and `reqStep` become tunables so the decision is a file save.

### 3.5 ⛔ `req(t)` and `W(T)` each owe a row in `ssot-power-scale.md` §10

**FACT.** §10's anti-duplication clause: *"A power-shaped number that is not in this table does not
have permission to exist. Adding a row is a reviewed change to this document, not a convenience."*

Both of these are power-shaped and neither is in the table:

| New scale | Classification | Precedent |
|---|---|---|
| `req(t) = reqBase + reqStep·t(t−1)/2` | **Cost ladder, not a power ladder** — it is a threshold on an already-`Θ`-derived quantity, and it never multiplies a magnitude | **Row 6** (`XpToNext = first + (L−1)·step`), which §10.1 keeps precisely because *"It is the cost ladder, not a power ladder"* |
| `W(T) = b·T(T+1)/2` — tree power in aptitude-point-equivalents | **A magnitude.** Must read `P(Θ)`, never a private `f(level)` | Handled by §5.1's rule below: **the plan emits dimensionless shares, never magnitudes** |

**The rule that keeps the whole program inside one ladder:**

> **The plan emits per-mille SHARES of a budget. It never emits a magnitude.** The runtime multiplies
> a share by `P(Θ)` at read time. So the plan is `Θ`-free, stable forever, and cannot be a second
> power curve — there is no level in it.

This also matches how `PowerVector` is already specified: §10 row 12 — *"Prices relative content.
Must stay scale-free — scaling it double-counts."*

---

## 4. Budget distribution at equal expected value (D15 / R6)

### 4.1 The budget unit — what exactly is conserved

**FACT — the repo already has a unit that prices any effect, and it ships.**
`src/FusionRpg.Core/Effects/Atoms/Power/PowerVector.cs:19-20` is an integer 5-vector:

```text
PowerVector(Offense, Survivability, Control, Utility, Economy)     — int points, exact, hashable
```

and `src/FusionRpg.Core/Effects/Atoms/Power/CostFunction.cs:22-24` prices one atom:

```text
power[category] = coeff(kind, channel) × normalize(magnitude, referenceScale) × conditionality
```

**FACT — a scalar budget read is already sanctioned.** `PowerVector.Total` (`:62`) is the plain sum,
and `ContentValidation.Budget` compares exactly that against a rarity ceiling
(`ContentValidation.cs:74-76`). This is **not** `PowerScalar.Of` — the geomean read that §10 row 13
forbids as a balance input (*"Display only… Never a balance input"*).

**AE is the human-readable convenience unit.** `ssot-sets.md:187-188`: *"one AE is one rolled affix
at the middle of the set's tier window"*, and a set's cap is stated as `1.5 AE per member piece`. The
planner should carry both: `Total` for arithmetic, AE for the review conversation.

**What is conserved — and, just as importantly, what is not:**

```text
CONSERVED, identical for every tree:
    Btotal      =  Σ over the tree's nodes of price(node).Total
    Boff        =  Bdef  =  Btotal / 2            (D6: symmetric, offence and defence equal)

NOT conserved — this is where identity lives:
    the CATEGORY VECTOR within a branch
```

**INFERENCE — why the category vector must be free.** Conserving all five categories per tree would
force a `Might` tree and a `Bulwark` tree to carry identical offense/survivability mixes. That is the
*"every tree feels the same"* failure `D15` names by name. Conserving the scalar `Total` **and** the
50/50 branch split gives `D13`'s property (*"every skill tree will cost and award same"*) and
`D15`'s freedom at the same time. The branch split is what stops "no tree is OP" from being gamed by
a tree that spends everything on offence.

### 4.2 The distribution function — and why the sum is a construction, not a check

`D20`'s pairing rule is **binding**, so the per-tier budget is **not** free:

```text
tierBudget[t] = B_b · t / T_tri ,        T_tri = tierCount·(tierCount+1)/2
                                         B_b   = Btotal / 2   (one branch)
```

Per-tier power is proportional to `t` — linear, exactly as `D20` requires. And:

```text
Σ_{t=1..T} tierBudget[t] = B_b · (Σ t)/T_tri = B_b · T_tri/T_tri = B_b       identically
```

**The shape freedom is one layer down.** A tier's fixed budget is split evenly among the `w[t]` nodes
in that tier:

```text
nodeBudget[t] = tierBudget[t] / w[t] = B_b · t / (T_tri · w[t])
```

`w` never appears in the sum. **So every archetype spends exactly `B_b` per branch and exactly
`Btotal` per tree, by construction — there is no post-hoc normalization and nothing to check.**

> **This is the whole answer to "equal expected value, not equal shape".**
> `D20` freezes **how much each tier is worth**. The archetype chooses **how many pieces that worth
> is cut into**, and §5 chooses **what kind of thing each piece is**. Two trees with the same budget
> and wildly different feel, provably.

### 4.3 Three shape archetypes, worked

`tierCount = 7`, `T_tri = 28`, `nodesPerBranch = 14` for all three (so `G9` holds and the corpus size
is uniform). Figures are fractions of one branch budget `B_b`, computed and verified:

**A · broad-and-flat** — `w = [2,2,2,2,2,2,2]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | **Σ** |
|---|---|---|---|---|---|---|---|---|
| `w[t]` | 2 | 2 | 2 | 2 | 2 | 2 | 2 | 14 |
| `tierBudget` | .0357 | .0714 | .1071 | .1429 | .1786 | .2143 | .2500 | **1.0000** |
| `nodeBudget` | .0179 | .0357 | .0536 | .0714 | .0893 | .1071 | **.1250** | |

**B · gated-deep** — `w = [3,3,3,2,1,1,1]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | **Σ** |
|---|---|---|---|---|---|---|---|---|
| `w[t]` | 3 | 3 | 3 | 2 | 1 | 1 | 1 | 14 |
| `tierBudget` | .0357 | .0714 | .1071 | .1429 | .1786 | .2143 | .2500 | **1.0000** |
| `nodeBudget` | .0119 | .0238 | .0357 | .0714 | .1786 | .2143 | **.2500** | |

**C · late-crown** — `w = [1,1,2,2,2,3,3]`

| `t` | 1 | 2 | 3 | 4 | 5 | 6 | 7 | **Σ** |
|---|---|---|---|---|---|---|---|---|
| `w[t]` | 1 | 1 | 2 | 2 | 2 | 3 | 3 | 14 |
| `tierBudget` | .0357 | .0714 | .1071 | .1429 | .1786 | .2143 | .2500 | **1.0000** |
| `nodeBudget` | .0357 | .0714 | .0536 | .0714 | .0893 | .0714 | **.0833** | |

Figures above are the **exact fractions**; §7.5 carries the same numbers rounded to per-mille under
§7.3's rule, which is what the plan actually emits.

**All three sum to 1.0000 of `B_b`. The strongest single node differs by nearly 3×** — `.2500`
(gated-deep, at the crown) against `.0893` (late-crown, at tier 5 — its crown is *wider*, so no single
node there is the prize). A gated-deep `Might` tree hides a capstone worth a quarter of a branch;
a late-crown `Focus` tree has no spike anywhere and asks you to finish it. Same cost, same award,
completely different builds.

**Archetype assignment must be deterministic**, and the roster ordinals are append-only
(`Aptitude.cs:16-17`: *"an existing aptitude's ordinal never changes, a retired one's is never
reused"*), so:

```text
archetype(tree) = archetypes[ ordinal(tree) mod archetypes.length ]
```

Round-robin over an append-only ordinal gives balanced archetype counts, is reviewable by eye, and is
stable for every tree that already exists when a new one is appended.

---

## 5. The mechanism / magnitude split — the load-bearing part

§3.5's measured conclusion is the constraint everything here answers to:

> *"A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM."*
> — swept `b ∈ {0,2,5,10,20} × Fmax ∈ {1.0,1.25,1.5}`, **not one cell reversed the ordering**.

### 5.1 How the plan *expresses* the split — derived, never asserted

**Do not add a hand-set `class` flag the LLM can lie about.** The distinction already falls out of
the shipped cost function.

**FACT.** `spec-power-vector.md` §"The cost function": *"**`conditionality = 1` when the atom declares
no trigger** — permanent modifiers (`stat.modify`, `stat.derived`) are not event-driven, and without
this short-circuit the 26 passive families price at zero."*

So:

```text
MAGNITUDE node  ≔  every bound atom has AttachPoint.Stat
                   AND kind ∈ {stat.modify, stat.derived}
                   AND conditionality == 1      (no trigger, no predicate)

MECHANISM node  ≔  anything else — it binds a non-Stat attach point,
                   or it declares a trigger, or it declares a predicate.
                   It changes WHEN or WHAT happens, not HOW MUCH.
```

Both halves are checkable against shipped vocabulary — **7 attach points, 16 kinds, 13 triggers**
(`AtomKindRegistry.cs:21`, `:31`, `:36`). The plan **freezes** `class` per node slot; stage 2 must
bind atoms that satisfy it, and the emit gate re-derives `class` from the bound atoms and refuses on
disagreement. A declared class that the content contradicts is a rejection, not a warning.

`D22` is satisfied for free: *"Passives compose from the shipped atom catalog — no passive-specific
effect vocabulary."*

### 5.2 What fraction of each tier is mechanism

A monotone non-decreasing ramp, with the deepest tier pinned:

```text
mechShareMilli[t] = mechFloorMilli + (mechCapMilli − mechFloorMilli)·(t−1)/(tierCount−1)
mechNodes[t]      = round( w[t] · mechShareMilli[t] / 1000 )

mechFloorMilli = 0      (tier 1 is pure magnitude — a readable, boring first step)
mechCapMilli   = 1000   (the deepest tier is 100% mechanism — §3.5 made structural)
```

Computed at `tierCount = 7` for the three archetypes:

| archetype | mechanism nodes per tier | mechanism total |
|---|---|---|
| broad-and-flat | `0, 0, 1, 1, 1, 2, 2` | **7 / 14** |
| gated-deep | `0, 0, 1, 1, 1, 1, 1` | **5 / 14** |
| late-crown | `0, 0, 1, 1, 1, 2, 3` | **8 / 14** |

Two hard rules on top, both refusals:

- **R-M1 — the deepest tier is 100% mechanism.** This is §3.5's conclusion, structural. It is the
  reason `mechCapMilli` is a `1000` you may lower only with an owner decision recorded next to it.
- **R-M2 — `mechShareMilli` is monotone non-decreasing.** A tree that gets *less* mechanical as you
  go deeper is the exact failure mode the sweep measured.

Note that gated-deep carries the fewest mechanism nodes (5) but the *most potent* one (`.2500 B_b`),
while late-crown carries the most (8) and the weakest (`.0833 B_b`). That is the archetype difference
expressed in the axis that actually matters, and it comes out of the arithmetic rather than a taste
call.

### 5.3 Are the two commensurable? — the honest answer

**Yes for conserving a budget; no for substituting one for the other.** Both halves are load-bearing.

**Commensurable, because `CostFunction.Price` already prices both** into the same integer
`PowerVector`, using the same `coeff × normalize × conditionality` form. A mechanism node prices
lower per unit of raw stat *because* its conditionality is below 1, which is the correct direction.
This is shipped code, not a proposal: `src/FusionRpg.Core/Effects/Atoms/Power/CostFunction.cs:37+`.

**But three measured caveats say the price is not the whole value:**

| # | Caveat | Evidence |
|---|---|---|
| 1 | The cost function is **knowingly wrong on multiplicative pairs** — the very thing mechanism nodes are. Two strong element slots give `1.25 × 1.25 = +562.5‰` where addition says `+500‰` | `CostFunction.cs` class doc; `spec-power-vector.md` §"What stays open, honestly". `ContentValidation.DriftTolerancePercent = 25` exists *because of this*, not out of vagueness |
| 2 | **Marginal value diverges wildly by build.** §3.5 measured a magnitude node as worth ≈0 to a focused build (`b = 20` moved the corner's win rate by **−0.7 points**, the wrong way) while mechanism is the only thing that helps | ideal §3.5's sweep table |
| 3 | The context-aware read that *would* fix (1) and (2) is **E10's marginal read**, and E9 explicitly defers to it: *"E9 does not solve this. E10's marginal read does"* | `spec-power-vector.md` §"What stays open" |

**So: one budget, plus a quota the budget may not trade against.**

```text
CONSTRAINT 1 (budget)  Σ price(node).Total = Btotal          — one number, conserved
CONSTRAINT 2 (quota)   |{ nodes at tier t with class=mechanism }| = mechNodes[t]   — per tier, exact
```

**Why a quota and not a second budget.** Two budgets require an **exchange rate** between mechanism
points and magnitude points — and caveats 1–3 say that rate is exactly what cannot be computed today.
A quota requires **nothing**: it is a count, it is checkable, and it never has to answer "how many
magnitude points is a reflect worth". State it that way:

> **If you make them two budgets you owe an exchange rate. A quota owes nothing.**

**The fallback, if a quota later proves too blunt** — stated so it is not re-derived from scratch:
add `mechanismPriceMultiplierMilli` to the planner tuning as a **declared, UNMEASURED** exchange
rate, flagged the way `pointEconomy`'s tier weights already are (*"shipping a guess is fine; calling
it balance is not"*, `aptitudes.v5.json` `_weightsWhy`). Then price mechanism nodes at
`Total × multiplier` inside the same single budget. Do not do this until E10 exists — an unmeasured
exchange rate silently rebalances every tree at once.

### 5.4 Where the budget may and may not reach

**FACT — and a constraint this plan must respect.** `ContentValidation.Budget`'s own doc comment
(`ContentValidation.cs:58-60`): *"A content test that fails naming the offender — and **never** a
generation input: which atoms roll is E5's pool and tier weights."*

Read in its section, that governs the rarity budget on rolled containers. **The principle transfers
and should be adopted rather than argued with:**

- The plan hands stage 2 a **ceiling and a class quota**. ✅
- The plan does **not** decide which atoms a node binds — that stays a **pool + weights** decision,
  the same shape `D13` already gives the LLM stage (*"atom effect pool and bonus within the plan's
  budget"*). ✅

---

## 6. Node potency ceiling (R7)

**FACT (Eleventh Hour Games, via prior art §4):** *"individual nodes should not be so potent that you
feel forced to build it in a particular way."*

Expressed against the plan's own budget, not as a magic number:

```text
maxNodeShareMilli  =  1000 / ( (tierCount + 1) · minTerminalWidth )
```

**Derivation.** The largest node any archetype can produce is the deepest tier's budget split among
the fewest nodes:

```text
max nodeBudget = tierBudget[T] / w[T]
               = (Btotal/2) · T/T_tri / w[T]
               = (Btotal/2) · T / (T(T+1)/2) / w[T]
               = Btotal / ((T+1) · w[T])
```

At `tierCount = 7` and a terminal width of 1: `1/(8·1) = 0.125` → **`maxNodeShareMilli = 125`**, which
admits `gated-deep`'s single capstone exactly and refuses anything deeper or narrower.

Two rules, both refusals at plan-emit time:

- **R-P1** — no node's budget may exceed `maxNodeShareMilli/1000 × Btotal`. Violation names the
  archetype, the tier and the width.
- **R-P2** — the *set* of shipped archetypes must be admissible: for every archetype,
  `1/((tierCount+1)·w[tierCount]) ≤ maxNodeShareMilli/1000`. So adding a `w[T] = 1` archetype to a
  deeper ladder is a reviewed change, not an accident.

The ceiling **falls out of the topology**, so a change to `tierCount` or to the archetype set moves it
by construction — no second edit, the same property `AptitudeCatalog.Count` has.

**Where it lives:** `data/tuning/passive-tree-gen.v1.json` → `potency.maxNodeShareMilli`, with a
`_note` recording the derivation so nobody re-picks 125 by feel later.

---

## 7. The plan schema — what stage 1 hands stage 2

One file per run, canonical JSON, deterministic key order. `FROZEN` = stage 2 may read but never
write. `HOLE` = stage 2 must fill; the emit gate refuses a plan with an unfilled hole.

### 7.1 Top level

```jsonc
{
  "schemaVersion": 1,                      // FROZEN
  "plannerVersion": "1.0.0",               // FROZEN — bump on any math change
  "_provenance": {                         // FROZEN, excluded from planHash
    "emittedUtc": "…",
    "inputs": [ { "path": "data/seed/aptitudes/roster.json", "sha256": "…" }, … ],
    "tuning": { "domain": "passive-tree-gen", "version": 1, "sha256": "…" }
  },
  "planHash": "…",                         // FROZEN — sha256 over canonical JSON minus _provenance

  "roster":            { … },              // §7.2  FROZEN
  "ladder":            { … },              // §7.3  FROZEN
  "propertyVocabulary":{ … },              // §7.4  FROZEN — must exist BEFORE any node text
  "archetypes":        [ … ],              // §7.5  FROZEN
  "trees":             [ … ]               // §7.6  FROZEN skeleton + HOLES
}
```

### 7.2 `roster` — FROZEN, derived, never typed

```jsonc
"roster": {
  "aptitudes": ["Might","Fortitude", …],   // 12, read from data/seed/aptitudes/roster.json
  "elements":  ["fire","ice","air","earth","light","dark"],   // 6
  "statuses":  ["butter","freeze", …],     // 21 — NEEDS data/seed/statuses/roster.json (does not exist)
  "demonFamilies": [ … ],                  // F  — NEEDS a closed roster (§1.1); empty is legal, and
                                           //      the plan then emits 39 trees and says so
  "counts": { "aptitudes": 12, "elements": 6, "statuses": 21, "demonFamilies": 0, "trees": 39 }
}
```

`counts` is emitted so a reader can see `n` without counting, and so a roster change shows up as a
one-line diff.

### 7.3 `ladder` — FROZEN

```jsonc
"ladder": {
  "tierCount": 7,
  "reqBase": 10, "reqStep": 5,             // req(t) = reqBase + reqStep·t(t−1)/2
  "req": [10,15,25,40,60,85,115],          // emitted, not just described — diffable
  "tierBudgetMilli": [36,71,107,143,179,214,250],   // per branch, Σ = 1000 (rounding note below)
  "branchSplitMilli": 500,
  "branches": ["offensive","defensive"],
  "pairingRule": "power-linear-in-tier"    // D20, structural — changing it inverts the design
}
```

⚠️ **`tierBudgetMilli` sums to 1000 only under a fixed rounding rule, and the rule must be in the
spec.** Compute `1000·t/T_tri` in per-mille, **round half up**, then absorb any residual into the
**deepest** tier. At `tierCount = 7` the rounded terms already sum exactly
(`36+71+107+143+179+214+250 = 1000`), so the residual is 0 — but at other tier counts it will not be,
and two correct-looking implementations would then disagree in the last per-mille and fail `--check`
for no reason.

**The same rule applies one level down, at the node.** `nodeBudgetMilli[t] = round(tierBudgetMilli[t]
/ w[t])`, with the residual absorbed by the **last node in that tier**, so
`Σ_nodes nodeBudget = tierBudget` exactly. Without that, `w[t] = 3` (which does not divide 71 or 107)
silently leaks a per-mille per tier and `C1`'s equal-value assertion fails on rounding alone.

### 7.4 `propertyVocabulary` — FROZEN, and it must exist before any node text

`D14`/`R3`: exclusions key on **properties**, never on named pairs, because a named-pair list is
O(n²) and unmaintainable under generation. Every id below is read from shipped code, so a property
that does not exist cannot be referenced:

```jsonc
"propertyVocabulary": {
  "nodeClass":      ["mechanism","magnitude"],                  // §5.1, derived from atoms
  "branch":         ["offensive","defensive"],
  "tier":           [1,2,3,4,5,6,7],
  "posture":        ["Force","Finesse","Bastion"],              // Aptitude.cs:11
  "aptitude":       [ …12… ],
  "element":        [ …6… ],                                    // ElementTable.cs:125-130
  "status":         [ …21… ],                                   // StatusCatalogBootstrap.cs:16-58
  "atomAttachPoint":["Stat","Resource","Status","Shield","Board","Match","Ui"],   // 7
  "atomKind":       [ …16… ],                                   // AtomKindRegistry.KindCount = 16
  "atomTrigger":    [ …13… ],                                   // AtomKindRegistry.TriggerCount = 13
  "combatFamily":   [ …28… ],                                   // DerivedStatChannels.cs:186-216
  "conversionState":["converted","unconverted"],                // D16 / R8 — ElementPayload tags
  "actionTag":      ["Offensive","Defensive","Heal","Buff","Debuff","Movement","Summon","Utility"]
}
```

**FACT — the exclusion predicate mechanism is already shipped and needs no new type.**
`src/FusionRpg.Core/Effects/Atoms/EligibilityRule.cs:21-22` carries `RequireTags` (bare keys, all
must be present) and `AnyOfTags` (`"key:value"` pairs, at least one must match), evaluated by
`EligibilityRule.IsEligible` (`:36`). Affix tags come from `AtomRow.TagsJson` via `AffixTags.tagsOf`
(`AffixTags.cs:14`). `D14`'s *"no effect if the damage is converted"* is exactly an
`AnyOfTags: ["conversionState:converted"]` predicate — O(1), and it covers nodes that do not exist yet.

### 7.5 `archetypes` — FROZEN

```jsonc
"archetypes": [
  { "id": "broad-and-flat", "widths": [2,2,2,2,2,2,2],
    "nodeBudgetMilli": [18,36,54,72,90,107,125], "maxNodeMilli": 125,
    "mechNodes": [0,0,1,1,1,2,2] },
  { "id": "gated-deep",     "widths": [3,3,3,2,1,1,1],
    "nodeBudgetMilli": [12,24,36,72,179,214,250], "maxNodeMilli": 250,
    "mechNodes": [0,0,1,1,1,1,1] },
  { "id": "late-crown",     "widths": [1,1,2,2,2,3,3],
    "nodeBudgetMilli": [36,71,54,72,90,71,83],   "maxNodeMilli": 89,
    "mechNodes": [0,0,1,1,1,2,3] }
]
```

`nodeBudgetMilli` is per-mille **of one branch budget**, computed as
`round(tierBudgetMilli[t]/w[t])` under §7.3's rule — so these arrays are derived from
`ladder.tierBudgetMilli`, not independently authored. `maxNodeMilli` is emitted so `R-P2` is a
one-line check against `potency.maxNodeShareMilli` rather than a re-derivation. (`late-crown`'s
`maxNodeMilli` is 89, at tier 5 — not at the crown, which is what its name is about.)

### 7.6 `trees[]` — FROZEN skeleton, HOLES for stage 2

```jsonc
{
  "treeId": "tree.aptitude.might",          // FROZEN — derived from category + roster id
  "category": "aptitude",                   // FROZEN — aptitude|element|status|demonFamily
  "subject": "Might",                       // FROZEN — the roster entry
  "gateQuantity": "aptitude.Might@Commander",// FROZEN — what req(t) reads (§1.3)
  "archetype": "gated-deep",                // FROZEN — ordinal mod 3
  "budgetTotal": 4200,                      // FROZEN — PowerVector.Total points, identical every tree
  "budgetPerBranch": 2100,                  // FROZEN — D6

  "name": null,                             // HOLE
  "flavour": null,                          // HOLE

  "nodes": [
    {
      "nodeId": "tree.aptitude.might/off/t7/0",  // FROZEN — (tree, branch, tier, index), G8
      "branch": "offensive",                     // FROZEN
      "tier": 7,                                 // FROZEN
      "parents": ["tree.aptitude.might/off/t6/0"],// FROZEN — G3/G5/G6
      "class": "mechanism",                      // FROZEN — §5.1; re-derived at emit, refuses on mismatch
      "budget": 525,                             // FROZEN — nodeBudgetMilli[7]/1000 × budgetPerBranch
      "budgetShareMilli": 250,                   // FROZEN — of one branch
      "requiredProperties": ["posture:Force"],   // FROZEN — what stage 2 must key on

      "name": null,                              // HOLE
      "text": null,                              // HOLE
      "atoms": null,                             // HOLE — must satisfy `class` and price ≤ `budget`
      "tags": null,                              // HOLE — keys must come from propertyVocabulary
      "exclusions": null                         // HOLE — EligibilityRule shape only, never a named pair
    }
  ]
}
```

**Three rules that make the holes safe to hand over:**

1. **`tags` keys must come from `propertyVocabulary`.** A tag the plan never named is a rejection.
   This is what makes `D14`'s exclusions O(1) — a predicate can only reference a closed set.
2. **`atoms` must price at or under `budget`** via `CostFunction.Price`, summed as
   `PowerVector.Total`, with `ContentValidation`'s existing ±25% drift tolerance
   (`ContentValidation.cs:44`) — the same tolerance the item corpus already uses, for the same reason.
3. **`class` is re-derived from the bound atoms** and compared to the frozen value. Disagreement is a
   refusal naming the node, never a silent repair.

---

## 8. Reproducibility

### 8.1 Inputs that make the plan byte-reproducible

**There is no RNG in stage 1 at all.** Nothing is sampled, seeded, or shuffled. The plan is a pure
function of:

| Input | Hashed into `_provenance.inputs` |
|---|---|
| `data/seed/aptitudes/roster.json` | ✅ |
| `data/seed/elements/roster.json` | ✅ |
| `data/seed/statuses/roster.json` **(must be created)** | ✅ |
| `data/seed/demons/families/roster.json` **(must be created, §1.1)** | ✅ |
| `data/tuning/passive-tree-gen.v{n}.json` | ✅ domain + version + sha256 |
| `plannerVersion` | ✅ |

Everything else — archetype assignment, tier budgets, node ids, parent edges, mechanism quotas — is
derived arithmetic over those. `archetype = archetypes[ordinal mod k]` is the only assignment
decision, and it reads an append-only ordinal, so appending a roster entry changes no existing tree.

### 8.2 The check gate

**Copy the shipped contract exactly** — `tools/tuning/resource_ownership.py:20-23`:

> `python tools/tuning/resource_ownership.py --check` — regenerate + diff vs the shipped file.
> Exit codes: `0` = generated edges match the shipped file exactly, `1` = drift.

So:

```powershell
python tools\tuning\passive_tree_plan.py --emit    # writes data/seed/passive-tree/plan.v1.json
python tools\tuning\passive_tree_plan.py --check   # regenerate, byte-diff, exit 1 on drift
```

Three assertions the gate must make, beyond the byte diff:

| # | Assertion |
|---|---|
| C1 | **Equal value.** `Σ price.Total` is identical across all `n` trees, and `Boff == Bdef` in each |
| C2 | **Graph invariants** `G1`–`G9` hold for every tree |
| C3 | **Quota and ceiling.** `mechNodes[t]` matches the ramp for every tier; no node exceeds `potency.maxNodeShareMilli`; the deepest tier is 100% mechanism |

`planHash` is a second, cheaper gate: a CI job that only compares the hash catches drift without
regenerating, the same role `_provenance.dumpHash` already plays in the species seeds.

### 8.3 Determinism hazards to specify, not discover

- **Per-mille rounding.** §7.3's floor-then-remainder-to-deepest rule must be in the spec. Two
  reasonable implementations otherwise disagree in the last per-mille.
- **Key order.** Canonical JSON, sorted keys, `\n` endings, no trailing whitespace — otherwise the
  hash moves on a Windows/Linux round trip.
- **No `double` anywhere in the planner.** `PowerVector` is `int` throughout for exactly this reason
  (`PowerVector.cs:14-16`: *"a double would make two runs of the same catalog disagree in the last
  bit and move a content hash for nothing"*). Per-mille integers everywhere.
- **`long` for anything a `contentScale` can touch.** The plan emits shares, so the planner's own
  arithmetic stays small — but the *runtime* multiply `share × P(Θ)` is a magnitude and is `long`,
  widened before multiplying, divided by 1000 last, overflow throwing.

---

## 9. Tunables — every number a balance pass would touch

Two domains, because the planner and the runtime are tuned by different people at different times —
the same split `set-charm-gen` vs `item-power` already uses.

### 9.1 `data/tuning/passive-tree-gen.v1.json` — the PLANNER's balance surface

| Key | Proposed value | Why it is a tunable |
|---|---|---|
| `topology.tierCount` | `7` | Sets authored depth; a balance pass targeting a different `Θ` band changes it |
| `topology.nodesPerBranch` | `14` | Corpus size and LLM cost |
| `topology.archetypes[].widths` | see §7.5 | The whole shape surface |
| `ladder.reqBase` | `10` | §3.4's entry tax — the one number that decides whether tier 1 is a 2× worse deal |
| `ladder.reqStep` | `5` | The quadratic coefficient (`2.5 = reqStep/2`) |
| `ladder.branchSplitMilli` | `500` | `D6` symmetry; a pass could try 550/450 |
| `budget.treeTotalPoints` | *unmeasured* | `PowerVector.Total` per tree. Ship a guess, flag it |
| `budget.tierWeightMode` | `"power-linear-in-tier"` | ⚠️ **structural, not tunable.** `D20`: pairing it with *constant* per-tier power *"inverts the whole design"*. Keep as an enum with one legal value + a comment |
| `potency.maxNodeShareMilli` | `125` | Derived (§6), but a pass may want it tighter |
| `mechanism.floorMilli` | `0` | Tier-1 mechanism share |
| `mechanism.capMilli` | `1000` | ⚠️ Lowering it contradicts §3.5's measured conclusion. Tunable, but the `_note` must say so |
| `exclusion.targetShareMilli` | `20` | `D14`'s ~2% target |
| `archetypeAssignment` | `"ordinal-round-robin"` | Closed enum |

### 9.2 `data/tuning/passive-tree.v1.json` — the RUNTIME's balance surface

| Key | Proposed value | Source |
|---|---|---|
| `focus.fmaxMilli` | `1150`–`1250` | `D5` as revised |
| `focus.pointsSoulsBlendMilli` | `500` | `w` in §3.2; ideal §7 item 7: *"Default 0.5 until swept"* |
| `respec.priceSouls` | reuse `pointEconomy.respecPrice` | `D18`; already exists at `aptitudes.v5.json` |
| `soulDeepen.firstCost`, `.step` | *unmeasured* | The arithmetic cost ladder of `D3` — a **cost** ladder, exempt from the one-ladder rule per §10 row 6 |

**Nothing above may be a `const`.** `T3` — Policy/Catalog/Rules/Ruleset/Math files are the balance
surface and carry no bare literals. `T5` — a missing tunable is a load rejection naming it, never a
built-in default.

---

## 10. What this program owes elsewhere before it can build

| # | Owed | To whom |
|---|---|---|
| 1 | `data/seed/statuses/roster.json` — mirror of the 21 in `StatusCatalogBootstrap` | this repo; the planner cannot reference `FusionRpg.Core` (`tunables-ssot.md` §7.2) |
| 2 | A **closed** demon-family roster, or `D9` amended to drop family trees | owner (§1.1) |
| 3 | Two reviewed rows in `ssot-power-scale.md` §10 — `req(t)` as a cost ladder, and the share→`P(Θ)` read | the power SSOT (§3.5) |
| 4 | `AllocationScope.StatusMastery` — a fifth enum value + a fifth rate-table row | `D19`; class-system |
| 5 | `Aspect`'s source value (`element_mastery`) | the demon program's `aspect-scope` module (`PointBudget.cs:14-18`) |

None of 1–5 blocks *writing* the plan. Items 1, 2 and 4 block *emitting* a complete one.

---

## 11. Contradictions with what is currently written down

| # | Written | Code / measurement says | Verdict |
|---|---|---|---|
| 1 | `DESIGN-GATE.md` §1 atom row: *"5 attach points, 12 kinds, **8 triggers** (`AtomKindRegistry.TriggerCount`)"* | `AttachPointCount = 7` (`AtomKindRegistry.cs:21`), `KindCount = 16` (`:31`), `TriggerCount = 13` (`:36`) | ⛔ **The gate file is stale on the exact number it says wins over every spec.** It was corrected 7→8 on 2026-09-03 and has drifted again. Code beats docs |
| 2 | `D9`: *"each demon family"* | `family` is `CLASSIFIED, **open**` (`spec-anchor-contract.md:58`); 699 distinct tokens over 841 entries; `spec-roster-metrics.md:38` expected 19 | ⛔ **`D9` is not executable.** Needs a closed roster or an amendment (§1.1) |
| 3 | `D20`: *"yields flat reward-per-point at every depth"* | `W/req` is `0.100b` at `t=1` against a `0.200b` asymptote — **tier 1 is half** (§3.4) | ⚠️ Flat from tier 2 on (±11%). The claim overstates tier 1. Owner decides: keep the entry tax, or index `req` on `t(t+1)/2` for exact flatness |
| 4 | ideal §3.1: *"`Fmax = 1.5` (tunable)"* in the code block | `D5` revises it to 1.15–1.25 | ⚠️ Cosmetic: the §3.1 block was not updated when `D5` was. Worth a one-line fix in the ideal |
| 5 | ideal §2 `D13`: *"only then does an LLM fill … bonuses"* | `ContentValidation.cs:58-60`: the budget is *"**never** a generation input"* | ✅ **Not a contradiction — a constraint to honour.** The plan hands a *ceiling and a quota*; pools + weights still choose the atoms (§5.4) |

Nothing here contradicts a `DESIGN-GATE.md` §2 invariant. Specifically: `PS-8` is respected (`Θ` is
uncapped, `T ∝ √Θ` has no terminal tier, and the potency ceiling is a **bounded ratio** — exempt by
nature and marked so); `PS-3` is respected (the plan emits shares, the runtime reads `P(Θ)`); `PS-4`
is respected (`PowerVector` prices are relative and never multiplied by `contentScale`).

---

## 12. Open questions for the owner

Only questions that a decision — not a measurement — can close.

1. **The demon-family roster (§1.1).** Option A (declare ~19 closed families) or Option B (drop
   family trees, leave the demon axis to `D23`)? This is the only thing standing between `n = 39` and
   `n = 58`.
2. **`ladder.reqBase` (§3.4).** Keep `10` and accept that tier 1 is a deliberate 2× entry tax, or
   move to `req'(t) = 5·t(t+1)/2` for provably constant reward-per-point? Both are defensible; the
   current *claim* of flatness is not.
3. **Topology (§2.3).** T1 / T2 / T3. Recommendation is **T2 · 29 nodes**, matching the only shipped
   comparator.
4. **`budget.treeTotalPoints`.** A number nobody can measure until trees carry power. Recommend
   shipping a guess with the `aptitudes.v5.json` posture — *"shipping a guess is fine; calling it
   balance is not"* — and re-measuring once mechanism nodes exist in the resolver (ideal §3.5:
   *"Re-measure only worthwhile once mechanism nodes exist"*).

---

## Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, class system, power ladder,
    tunables, effect atoms, demon seeds.
[x] I read every doc in the §1 row(s) this session: DESIGN-GATE.md, passive-tree-ideal.md,
    ssot-power-scale.md (§4, §10, §11 head), tunables-ssot.md, class-system-ideal.md (§4.2, §8.1d),
    class-system-map.md (§4b, §5), passive-tree-prior-art-2026-09-04.md,
    effect-atom/spec-power-vector.md, item/ssot-sets.md §3.5, demon-seed/spec-anchor-contract.md,
    demon-seed/spec-classify-pipelines.md, demon-seed/spec-roster-metrics.md.
[x] I checked decisions.md is referenced by the ideal's own D-list; this doc proposes no lock
    change. It does report three places where a written claim disagrees with code (§11).
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — every roster count was counted from source,
    and the 841-entry / 699-family figures were computed over the seed files.
[ ] I read the surrounding section of every rule I quoted — PARTIAL. I read the full section for
    ContentValidation's "never a generation input", spec-power-vector's conditionality rule, and
    ssot-power-scale §10/§10.5. I did NOT read all 1,108 lines of ssot-power-scale.md; §11.1-§11.10
    were read only as headings.
[x] I tested (not assumed) the constraints I report: req(t)'s sequence, the second differences,
    W/req at 12 tiers, T_max at six Θ values, and all three archetypes' budget sums were COMPUTED,
    not asserted. Roster counts were counted.
[ ] Nothing contradicts a §2 invariant — CONFIRMED for PS-3/PS-4/PS-8. But §11 names three places
    where this document contradicts a WRITTEN claim (the gate's atom counts, D9's family clause,
    D20's flatness claim), each stated explicitly rather than quietly worked around.
[ ] Corrections are propagated — NOT DONE. This is a research doc; §11's three corrections need
    landing in DESIGN-GATE.md §1, passive-tree-ideal.md D9/D20, and the ideal's §3.1 Fmax block.
    I did not edit those files, per this task's read-only scope.
```
