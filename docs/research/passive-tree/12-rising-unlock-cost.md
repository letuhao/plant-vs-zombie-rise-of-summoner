# D25 — rising unlock cost, specified

**Decision being specified (owner, 2026-09-05):** *"Unlock cost rises with the number of nodes an
actor already owns — arithmetic, per actor."*
([passive-tree-ideal.md:61](../../architecture/passive-tree-ideal.md))

**Status:** research. Not a spec, no build authorized. Every claim is marked FACT (read in code or
data this session) / INFERENCE (algebra over facts) / RECALL (from a doc read this session), and
cites `file:line`.

---

## 0. Answer up front

**The curve.** The Nth node an actor owns costs

```text
cost(N)       = first + (N − 1) · step                   skill points
cumulative(N) = N · first + step · N(N − 1) / 2          what N nodes cost in total
```

the same arithmetic shape as `RpgXpCurve.XpToNext` (`RpgProgression.cs:80-87`) and
`ContractPolicy.NextSlotPrice` (`ContractPolicy.cs:176-177`). Recommended starting values, **derived
not guessed** (§3.4):

| Tunable | Value | Where | Why this value |
|---|---|---|---|
| `unlockCost.first` | **5** | `data/tuning/passive-tree.v1.json` (new) | `first = step·(k+1)/2` with `k = 4` nodes per tier — the index-reconciliation condition that makes reward-per-skill-point **exactly** flat at every tier (§4.3) |
| `unlockCost.step` | **2** | same | The smallest integer `step` for which `first` is also an integer |
| `grant.skillPointsPerTheta` | **1 → 11** | `data/tuning/aptitudes.v{n}.json:17` (exists, retune) | `g = 3·s·step·k²/5 = 10.4`, rounded up so the wallet clears the gate with a small surplus at every tier (§4.4) |

**Does D25 break D26's flatness? No.** D26's identity `W(t)/req(t) = b/5` is arithmetically untouched
— D25 adds no term to either side, because it prices a *different currency*. And the stronger
version: D25 is **the same shape** as D26, so it produces the same result in its own currency. Both
are triangular ladders read against a budget linear in `Θ`, so both give `tier ∝ √Θ` and
`tree power ∝ Θ`. The flatness is robust even to miscalibration — a wrong `g/step` moves the
*constant*, never the *shape*. Full algebra in §4. **One honest amendment:** D26's claim stops being
universal and becomes per-build. Reward-per-point is `b/5` for any build whose wallet reaches its
gate; a spread build sits below that line by construction. That asymmetry is not a defect — it *is*
D25's concentration reward.

**Does it actually reward concentration? Yes, decisively — and by construction, not by measurement.**
At `Θ=100` with the recommended dial, a focused build's tier gate opens tier 7 (28 nodes) and its
wallet affords 31; a spread build's gates open tier 2 in twelve trees (96 nodes) and its wallet
affords the same 31. **The focused build completes what its gates opened; the spread build is priced
out of two thirds of its own breadth.** Working that through the shipped tree-power model (§5) turns
`spread 36b > focus 28b` without D25 into `focus 28b > spread 12b` with it — a **2.3× reversal**.
This is the first mechanism in the program that rewards concentration *structurally*: D4's `F` was
measured not to (ideal §3.5) and D28's cross-unlock needed a sweep just to establish the sign.
**One coupling to record:** D25 rewards concentration only in composition with D20/D26's
linear-per-tier power. Against a *flat* per-tier reward it would be breadth-neutral. If a later
change flattens per-tier power, D25's second purpose dies silently.

**Respec (D18): neither refund. Store the owned node set, derive both budget and spend.** The
"refund at the escalated price or at base" dilemma dissolves, exactly the way D18 dissolved Grim
Dawn's order-sensitivity. Cost is a pure function of the node *count*, so re-buying the same 28 nodes
after a full reset costs the same 896 skill points it cost the first time. No exploit, no hidden tax,
and **price is order-independent by construction** — which is what makes derive-on-read safe. §6.

**Per-actor or account-wide? Per-actor, confirming D25 — and it does not defeat the bound, because
the budget is per-actor too.** A fresh demon does start at price 5, but it also starts at specimen
level 1 with almost no skill points to spend at price 5. Roster breadth already has its own
escalating price (`ContractPolicy.NextSlotPrice`, `ContractPolicy.cs:176-177`) and §11.1a's own
verdict that the price *is* the cap. **One wiring requirement:** `skillPointsPerTheta` is a single
scalar today while aptitude points already ship a four-scope rate table — per-actor pricing needs the
same table, or every demon reads the commander's `Θ` and the bound really does break. §8.1.

---

## 1. The problem is real — and the number is 1,560, not 1,450

### 1.1 The grant rate, verified

**FACT.** `data/tuning/aptitudes.v5.json:17` — `"skillPointsPerTheta": 1`, inside the `grant` block.

**FACT.** It is parsed at `AptitudeTuning.cs:158` into `AptitudeGrant.SkillPointsPerThetaMilli`
(`:13`) via `NonNegativeMilli` (`:291-296`), which reads the raw `Int64` and **neither multiplies nor
divides by 1000** — the `Milli` suffix is a naming artifact, the same one `PointBudget.cs:44-46`
documents for its sibling field (*"why this does NOT divide by 1000 despite the `Milli` name"*). So
the shipped value is **1 whole skill point per `Θ`**.

**FACT.** It has **zero consumers.** `grep -rn "SkillPointsPerTheta" --include=*.cs src/ tests/ tools/`
returns exactly two hits: the record declaration and the parse site above. Nothing spends it, nothing
asserts its magnitude. That matters twice over — it confirms the ideal's D2 claim, and it means
**retuning the grant today is free**: no migration, no golden, no balance regression. D25 is its
first spender, so D25 owns its calibration.

### 1.2 The unlock-everything threshold under D29's shape

**FACT.** D29 fixes the shape: *"10 tiers × 2 branches, ~40 nodes per tree… Generic corpus:
**39 × 40 = 1,560 nodes**"* ([passive-tree-ideal.md:60](../../architecture/passive-tree-ideal.md)).
The 39 is D27's `12 primary + 6 elemental + 21 status`; demon family and species trees are sequenced
separately.

**INFERENCE.** At the shipped grant and the natural default of one skill point per node, the whole
generic catalog unlocks at

```text
Θ = 1,560 nodes ÷ 1 skill point per Θ = 1,560
```

**The ideal's Θ≈1,450 figure is superseded, and the charter was right to flag it.** 1,450 is
`50 trees × 29 nodes` — Last Epoch's density against the pre-D29 roster estimate, quoted in the
ideal's own §7 open item 1 as the reference point. Under D29's decided shape the correct figure is
**1,560**. The gap is 8% and changes no conclusion; recording it stops a wrong number propagating
into a spec.

**A second, independent bound, and it is lower.** The tier gate reads aptitude points, not skill
points. Tier 10 needs `req(10) = 5·10·11/2 = 275` points in the tree's gate quantity (D26/D29).
Twelve primary trees at tier 10 is `12 × 275 = 3,300` aptitude points, and the commander rate is 3
per `Θ` (`aptitudes.v5.json:24`):

```text
Θ = 3,300 ÷ 3 = 1,100      the AP gate for twelve primary trees at tier 10
```

**INFERENCE.** So the two constraints land within 1.4× of each other and the ideal's Θ≈1,450 sits
between them. **The hole is real under either reading.** Both figures are inside the endless-grind
band: `Θ` composes from Dave level, realms advanced, runs, map depth and world size
([ssot-power-scale.md §5.3](../../architecture/power/ssot-power-scale.md)), and `Wa = 25,000‰` per
realm means `Θ = 1,560` is roughly 62 realms advanced — not a fantasy number.

**One caveat, stated rather than buried.** The 1,560 counts elemental and status trees whose gate
quantities do not exist in code. `element_mastery` has three doc-comment hits and no counter, no
store and no endpoint; almanac XP has zero code hits repo-wide; status mastery has three hits, all
under `docs/research/` ([08-effort-power-reconciliation.md §1, §3](08-effort-power-reconciliation.md),
greps re-run this session). So the *skill-point* arithmetic is exact, and the *gate* half of the
picture is only complete for the twelve primary trees.

---

## 2. Why arithmetic, and why it is a soft bound rather than a cap

**RECALL.** PS-8: *"A cap on a magnitude is a progression ceiling until proven otherwise"*
([ssot-power-scale.md §11](../../architecture/power/ssot-power-scale.md)). D25 must refuse nothing.

**INFERENCE — the proof, and it is one line.** Under an arithmetic price the nodes affordable at
index `Θ` are

```text
N(Θ) = ⌊ √( (first/step − ½)² + 2gΘ/step ) − (first/step − ½) ⌋
```

which for `first = 5, step = 2, g = 11` simplifies to `N(Θ) = ⌊√(4 + 11Θ) − 2⌋`. It is strictly
increasing and unbounded in `Θ`, and `Θ` is uncapped by decision (§5.2, *"PvZ runs are uncapped — the
weight is the instrument, not a ceiling"*). **Therefore every node in the catalog is reachable at
some finite `Θ`.** A cap refuses forever; this defers. That is exactly the distinction PS-8 asks
for, and here it is provable rather than asserted.

**Why arithmetic specifically, and not geometric.** A geometric price gives `N ∝ log Θ` — still
unbounded, but the marginal node outruns any budget growth a player can perceive, which is "refused"
in everything but name. Arithmetic gives `N ∝ √Θ`: breadth keeps growing, visibly, forever, just
slower than depth. That is the shape §11.1a already blessed for contract slots — *"a cap is a cliff;
the continuous instrument is the real control"* — and the shape the soul track already uses (D3,
ideal §4). **The owner's stated reason (*"adds no new ladder"*) checks out**, and §4 proves the
stronger version of it.

**A property nobody has named yet, and it is the best argument for the shape.** Cumulative cost of
the whole catalog is `≈ (step/2)·N²`, so the price of full breadth scales with **the square of the
catalog size**. Doubling the content quadruples the cost of owning all of it. **The bound therefore
re-tunes itself as content ships** — D27's *"ship the roster whole"* and D30's 24,389 species nodes
cannot reopen the hole, with no balance pass required. A flat price has the opposite property: every
tree added makes full breadth cheaper per unit of what it gives you.

---

## 3. The curve, exactly

### 3.1 The formula and its types

```csharp
// data/tuning/passive-tree.v1.json — unlockCost.{first,step}
public static long CostOfNextNode(long nodesOwned, PassiveTreeTuning tuning)
{
    if (nodesOwned < 0) throw new ArgumentOutOfRangeException(nameof(nodesOwned), ...);
    checked { return tuning.UnlockCost.First + nodesOwned * tuning.UnlockCost.Step; }
}

public static long TotalCostOf(long nodeCount, PassiveTreeTuning tuning)
{
    if (nodeCount <= 0) return 0;
    // n(2a + (n-1)d)/2 — the product is always even, so the halving is exact
    checked { return nodeCount * (2 * tuning.UnlockCost.First + (nodeCount - 1) * tuning.UnlockCost.Step) / 2; }
}
```

`long` throughout, `checked` throughout, divide last and exactly once — CLAUDE.md's numeric rules.
`TotalCostOf` is deliberately the same expression as `RpgXpCurve.TotalToReach`
(`RpgProgression.cs:93-100`), including its *"always even, so the halving is exact"* remark, because
it is the same sum.

### 3.2 Why `long` is not ceremony here

**INFERENCE.** The price itself is small. At the largest catalog anyone has proposed — 25,900 nodes,
D29's 1,560 plus D30's 24,389 — the marginal price is `5 + 2·25,899 = 51,803` and the cumulative is
`670,913,600`. Both fit `int`.

**The budget does not.** The budget is `g·Θ`, and `Θ`'s own ceiling is `PowerLadder.MaxIndex`
(`Power/PowerLadder.cs:65`, computed by binary search rather than declared — roughly `2.147×10⁸` at
the shipped `bMilli = 400`). At `g = 11` that is `2.36×10⁹` — **past `int.MaxValue`**. Once the
budget is `long`, comparing it against an `int` price is the narrowing defect §11.2a catalogues four
times over. Both sides are `long`.

**Overflow is structurally unreachable and must still throw.** `cumulative(N)` reaches
`long.MaxValue` at `N ≈ 3.04×10⁹` nodes — five orders past the largest authored catalog, and node
count is bounded by *content*, which §11.10 classifies as a different axis from progression. So this
is a type boundary, not a ceiling. `checked` is still required (CLAUDE.md rule 5): an unreachable
overflow that wraps is still a wrap.

### 3.3 The tunable file

**Proposed path: `data/tuning/passive-tree.v1.json`.**

```jsonc
{
  "schemaVersion": 1,
  "version": 1,
  "_meta": {
    "owner": "docs/architecture/passive-tree/spec-unlock-economy.md",
    "note": "D25's unlock economy. The Nth node an actor owns costs first + (N-1)*step skill points -- the same arithmetic cost ladder as RpgXpCurve (ssot-power-scale.md S10 row 6) and ContractPolicy.NextSlotPrice. Working values derived against D26's tier ladder and D29's shape; not a validated balance decision.",
    "coupling": "first = step*(k+1)/2, where k is nodes per tier (D29: 40 nodes / 10 tiers = 4). That is the index-reconciliation condition that makes reward-per-skill-point flat at every tier, exactly as D26 did for reward-per-aptitude-point. Changing D29's tree shape means re-deriving first, not just retuning it.",
    "grant": "The faucet these prices drain is grant.skillPointsPerTheta in aptitudes.v{n}.json -- read in place, never copied here (tunables-ssot.md S2: a copied number is a future drift bug with a delay fuse).",
    "rebalance": "Never hand-edit. python tools/tuning/publish.py passive-tree <dotted.key>=<value> writes v{n+1}; the old version stays on disk for revert (tunables-ssot.md T4)."
  },
  "unlockCost": {
    "first": 5,
    "step": 2
  }
}
```

**Two deliberate omissions.**

1. **The grant stays where it is.** `grant.skillPointsPerTheta` already ships in
   `aptitudes.v5.json:17` with a live loader. tunables-ssot §2: a number two domains need *"belongs
   to whichever owns the concept; the other reads it rather than copying it."* The tree reads
   `AptitudeTuning.Grant.SkillPointsPerThetaMilli` in place. Moving it is churn on a live loader for
   no benefit.
2. **What counts as an owned node is structural, not tunable.** tunables-ssot §1's test: *"if
   changing it breaks whether the system works rather than how the game feels, it is structural."*
   Counting soul levels would break D3's two-track split; counting invalid nodes would break D11's
   construction (§7). Those are `const`s with comments, per T2 — not config rows.

On units (T6): skill points are whole units with no sub-unit, and the block name `unlockCost` carries
the unit the way `progression.v1.json`'s `xpCurve` block does for XP. **No `Milli` suffix, and the
absence is deliberate** — the shipped `SkillPointsPerThetaMilli` misnomer (§1.1) is precisely the
trap T6 exists to prevent, and it should not be propagated into a second file.

### 3.4 Calibration — the two derivation rules

Let `k` = nodes per tier (D29: `40/10 = 4`), `s` = the share of the point vector a fully committed
build holds, `g` = `skillPointsPerTheta`, `b` = aptitude-point-equivalents per tier unit (D20's
pairing rule).

**FACT — `s = 0.54163`.** `tools/HybridViability/Program.cs:79-80,86`: a corner build gives its spike
`Total − Floor·(roster.Length − 1)` = `100,000 − 4,167 × 11 = 54,163` out of `100,000`. This is
`BestResponse.DominanceMatrix`'s own corner shape — the eleven other aptitudes are *floored*, not
zeroed — and it is where D29's `1.626·Θ` comes from: `3 × 0.54163 = 1.625`.

**Rule 1 — the flatness condition** (derived in §4.3):

```text
first = step · (k + 1) / 2                  k = 4  ⇒  first = 2.5 · step
```

**Rule 2 — the wallet-tracks-gate condition** (derived in §4.4):

```text
g = 3 · s · step · k² / 5                   s = 0.54163, step = 2, k = 4  ⇒  g = 10.40
```

Round **up** to `g = 11`. The focused build should always be able to complete the tier its gate
opened, with a small surplus, because the surplus is what pays for breadth and breadth is what D25 is
pricing. Rounding down makes the wallet bind even for the focused build, turning a breadth tax into a
depth tax.

**The calibration table.** `AP = 1.625·Θ`; tier from `req(t) = 5t(t+1)/2`; nodes for tiers 1..T is
`4T`; cost is `N(N+4)` at `first=5, step=2`; budget is `11Θ`.

| `Θ` | AP in tree | Gate opens | Nodes needed | Cost | SP budget | Affords | Verdict |
|---|---|---|---|---|---|---|---|
| 20 | 32.5 | tier 3 | 12 | 192 | 220 | 12 | exact |
| 50 | 81.3 | tier 5 | 20 | 480 | 550 | 21 | +1 |
| 100 | 162.5 | **tier 7** | 28 | 896 | 1,100 | 31 | +3 |
| 170 | 276.3 | **tier 10** | 40 | 1,760 | 1,870 | 41 | +1 |
| 300 | 487.5 | tier 10 (max) | 40 | 1,760 | 3,300 | 55 | +15 → a second tree |
| 1,000 | 1,625 | tier 10 (max) | 40 | 1,760 | 11,000 | 102 | ~2.5 trees |
| 10,000 | 16,250 | tier 10 (max) | 40 | 1,760 | 110,000 | 329 | ~8 trees |
| 100,000 | — | — | — | — | 1,100,000 | 1,046 | ~26 trees |
| **221,804** | — | — | 1,560 | 2,439,840 | 2,439,844 | **1,560** | the whole generic catalog |

**INFERENCE.** The `Θ=100 → tier 7` and `Θ=170 → tier 10` rows reproduce D29's own two stated
calibration points exactly, which is the check that this dial is solved against the decided shape
rather than an invented one.

And the last row is the bound: **the whole generic catalog moves from `Θ = 142` (recommended grant,
flat price) to `Θ = 221,804` — a 1,564× move, and nothing is refused along the way.** The ratio is
`≈ N + first/step`, i.e. it *is* the catalog size, which is §2's self-retuning property showing up as
a number.

---

## 4. Does D25 break D26? — the algebra

**This is the most important question in the brief, so it is worked rather than asserted.**

### 4.1 What D26 actually claims, read in its own row

**RECALL, [passive-tree-ideal.md:62](../../architecture/passive-tree-ideal.md):**
*"`W(t) = b·t(t+1)/2` over `req(t) = 5·t(t+1)/2` is **`b/5` at every tier, by construction** — not
flat within 11%, flat identically."*

Reading the surrounding row rather than the sentence: `req(t)` is a **threshold on aptitude points**,
read against the actor's base allocation in the tree's gate quantity (ideal §4: *"a tier opens on (a)
the tier below being unlocked, and (b) the actor's own base allocation in that tree's gate
quantity"*). Aptitude points are **not spent** on the tree — they are allocated to the aptitude and
*read*. D25 prices in **skill points**, a different currency with a different faucet.

**So the first answer is immediate: D25 adds no term to `W(t)` and no term to `req(t)`. The identity
`W(t)/req(t) = b/5` is arithmetically untouched.** Necessary, but not sufficient — the identity's
*purpose* was reward-per-effort, and effort now has two components.

### 4.2 Reward per effort, in both currencies

Write `Θ` for the shared index. Both currencies are linear in it:

```text
AP in the tree   p(Θ) = 3 · s · Θ            PointBudget.cs:51-58, aptitudes.v5.json:24
SP budget        B(Θ) = g · Θ                aptitudes.v5.json:17
```

Both ladders are triangular against those linear budgets:

```text
gate:    req(T)   = (5/2)·T(T+1)      ≤ p(Θ)     ⇒  T_gate   ∝ √Θ
wallet:  cost(kT) ≈ (step·k²/2)·T²    ≤ B(Θ)     ⇒  T_afford ∝ √Θ
```

**Both give `T ∝ √Θ`, so both give `W = b·T(T+1)/2 ∝ Θ`.** Tree power stays linear in `Θ` — the same
growth rate as the aptitude allocation it is expressed in — *whichever constraint binds*.

> **This is the verdict, and it is stronger than "does not break."** D25 cannot break D26's shape,
> because D25 **is** D26's shape. Miscalibrating `g/step` changes the **constant** of proportionality
> (`b/5` becomes some smaller constant) and never the **exponent**. The property is robust to being
> tuned wrong — exactly what you want from a number nobody has measured yet.

**The owner's stated reason for choosing arithmetic — *"Same arithmetic-cost shape the soul track
already uses (§4), so it adds no new ladder"* — is verified by this, not merely consistent with it.**
Any non-arithmetic price breaks the exponent match and makes tree power grow at a different rate from
allocation power, which is the star-merge defect
([08 §2 M4](08-effort-power-reconciliation.md)) one system over.

### 4.3 Reward per *skill point* — and it is not automatically flat

The second currency deserves its own flatness check, and running it turns up something worth fixing.

A build owning tiers 1..T completely holds `N = kT` nodes and `W(T) = b·T(T+1)/2`. Its cumulative
skill-point spend is

```text
cost(kT) = kT·first + step·kT(kT − 1)/2
         = (step·k²/2)·T²  +  (k·first − step·k/2)·T
```

For `W/cost` to be constant in `T`, the two quadratics must be proportional. `W = (b/2)(T² + T)`, so
the `T²` and `T` coefficients must be equal:

```text
step·k²/2  =  k·first − step·k/2      ⇒      first = step · (k + 1) / 2
```

**With `k = 4`: `first = 2.5·step`.** At `step = 2, first = 5`, reward-per-skill-point is
`b/(step·k²) = b/32` at **every** tier, exactly.

**What the obvious dial does instead.** Take "the Nth node costs N" (`first = step = 1`). Then
`first ≠ step·(k+1)/2` and the ratio runs:

| Tier | `W(T)` | `cost(4T)` | reward per skill point |
|---|---|---|---|
| 1 | `1.0·b` | 10 | `b/10` |
| 4 | `10.0·b` | 136 | `b/13.6` |
| 7 | `28.0·b` | 406 | `b/14.5` |
| 10 | `55.0·b` | 820 | `b/14.9` |
| ∞ | — | — | `b/16` |

A **1.6× gradient favouring shallow nodes** — tier 1 is a 1.6× better deal per skill point than the
asymptote. **This is D20's tier-1 defect with the sign flipped, in the currency D26 did not look
at**, and it works directly against D25's second stated purpose. It is invisible unless the algebra
is written down, and `first = step·(k+1)/2` removes it for one character of tuning.

> **Recommendation: adopt `first = step·(k+1)/2` as a stated coupling in the tuning file's `_meta`,
> not as two independent dials.** This is D26's own lesson — *"the owner's original sequence was a
> correct instinct expressed on a mismatched index"* — applied one currency over, at the cheapest
> possible moment, because nothing is built.

**One caveat, since D10 gives every tree two branches.** The derivation assumes tiers are bought
*complete* (`k = 4`). A single-branch dive buys `k = 2` per tier and collects half the tier's power,
giving `W/cost` rising from `b/24` at tier 1 to `b/16` asymptotically — a gradient favouring depth.
That sign is correct for the design and needs no correction, but it means the exact flatness is a
property of full-tier purchase, which is the conservative (most expensive) reading. §12 open item 1
is the spec question this rests on.

### 4.4 The wallet-tracks-gate condition

Setting `cost(kT)` equal to `B(Θ)` at the same `Θ` at which the gate opens tier `T`:

```text
req(T) = (5/2)·T(T+1) = 3sΘ                  ⇒  T(T+1) = 6sΘ/5
cost(kT) = (step·k²/2)·T(T+1)                   using first = step(k+1)/2, from §4.3
         = (step·k²/2)·(6sΘ/5) = gΘ           ⇒  g = 3·s·step·k²/5
```

With `s = 0.54163`, `step = 2`, `k = 4`: **`g = 10.40`**. That is §3.4's rule 2, and it is the whole
reason `skillPointsPerTheta` should move off 1.

### 4.5 What D26's claim becomes — the honest amendment

| | Before D25 | After D25 |
|---|---|---|
| The identity | `W(t)/req(t) = b/5` | **unchanged, exactly** |
| Its scope | every build, at every tier | **every build whose wallet reaches its gate**, at every tier |
| Below that line | did not exist — the gate was the only constraint | `W` is set by `T_afford` instead of `T_gate`; still `∝ Θ`, at a smaller constant |
| Who sits below it | — | **spread builds, by construction** (§5) |

That last row is not a regression. It is the mechanism working: D25's second stated purpose is
concentration reward *on the cost side*, and "the spread build is the one whose wallet cannot reach
its gates" is precisely what that means, expressed as algebra.

---

## 5. Does it reward concentration? — worked against D29's shape

The charter is right to be suspicious. The escalating price is **currency-blind**: the Nth node costs
`5 + 2(N−1)` whether it is your first tree's tier-10 node or your 39th tree's tier-1 node. D25 does
*not* price a spread node higher than a deep node. So the concentration reward has to come from
somewhere else — and it does, from the **reward** side, where D20's pairing rule makes a deep node
worth more.

### 5.1 The marginal read — the cleanest statement

Node `N` bought by a **focused** build sits at tier `N/k` and is worth `b·(N/k)/k = bN/k²`. Node `N`
bought by a **spread** build sits at tier 1 or 2 and is worth `≈ b/k`. Both pay `≈ step·N`.

```text
focused:   marginal reward / marginal cost  =  (bN/k²) / (step·N)  =  b/(step·k²)     constant
spread:    marginal reward / marginal cost  =  (b/k)   / (step·N)  =  b/(step·k·N)    ∝ 1/N
```

> **A focused player's 400th skill point buys the same power as their 4th. A spread player's buys
> `1/N` as much.** The price ladder is identical for both; what differs is what the price buys. That
> is the concentration reward, and it is exact.

### 5.2 The concrete case at `Θ = 100`

Both builds hold `B = 11 × 100 = 1,100` skill points and afford **31 nodes** (`31 × 35 = 1,085`).

| | Focused (corner, `s = 0.542`) | Spread (even-twelve, `s = 1/12`) |
|---|---|---|
| AP per invested tree | `1.625 × 100 = 162.5` | `0.25 × 100 = 25` |
| Tier the gate opens | **7** (`req(7) = 140`) | **2** (`req(2) = 15`; `req(3) = 30 > 25`) |
| Nodes the gates opened | `4 × 7 = 28`, in one tree | `4 × 2 × 12 = 96`, across twelve |
| Nodes the wallet affords | 31 | 31 |
| **Binding constraint** | **the gate** — 3 nodes spare | **the wallet** — priced out of 65 of 96 |
| Tree power realised | `W = b·7·8/2 = 28b` | 31 nodes ≈ 4 trees at tier 2 → `4 × 3b = 12b` |

**Without D25** (flat 1 skill point per node, same `g`) the wallet affords 1,100 nodes, so *both*
builds are gate-bound. Focused realises `28b`; spread realises `12 × 3b = 36b`.

```text
without D25:   spread 36b  >  focus 28b        spread wins, 1.29×
with D25:      focus  28b  >  spread 12b       focus  wins, 2.33×
```

**INFERENCE — the ordering reverses, and D25 is what reverses it.** For contrast: the `--trees` sweep
found that **no** value of `b` up to 20 and **no** `Fmax` up to 1.5 reverses the same ordering
([passive-tree-ideal.md §3.5](../../architecture/passive-tree-ideal.md)), and cross-unlock needed the
largest-mate rule plus a dedicated sweep to manage 49.9% against 47.7%
([09-crossunlock-sweep.md](09-crossunlock-sweep.md)). D25's effect is larger than either, and unlike
both it is structural rather than empirical.

**Marked INFERENCE, not measured.** This is algebra over the shipped tree model
(`tools/HybridViability/Program.cs:226-288`), not a run of it — the tool has no skill-point budget
today (§11). The win-share consequence still has to be measured; §11 says how.

### 5.3 The objection the charter raises, answered

*"Tier gates force a focused build to buy every node below it too."* True, and it is already in the
arithmetic: the focused build buys all `4T` nodes, not only the deep ones. It does not defeat the
mechanism, because the nodes it buys early are the ones priced early — the cheap prices and the cheap
nodes line up. That alignment is exactly what §4.3's `first = step·(k+1)/2` makes exact.

### 5.4 The coupling that must be recorded

D25 rewards concentration **only in composition with D20/D26's linear-per-tier power.** Substitute a
*constant* per-tier reward (`W(T) = b·T`) and §5.1's marginal read becomes `b/(k·step·N)` for **both**
builds — identical — and D25 becomes a pure breadth tax with no concentration signal at all.

D20 already flags the neighbouring version of this: *"Paired with constant per-tier power it
**inverts the whole design**."* This is the same dependency reaching one system further than D20
stated. **It belongs in D25's own row, not only in D20's** — because a generator that quietly emitted
flat-value nodes would kill D25's second purpose while every test stayed green.

---

## 6. Respec (D18)

**RECALL, D18:** *"Respec is a FULL reset — skill distribution and primary stats together — priced in
souls… with no partial respec there is no orphaned unlock, because allocation is cleared and
redistributed as one transaction."*

### 6.1 The dilemma dissolves — do not refund at all

The charter frames it as refund-at-paid versus refund-at-base, and notes that one is an exploit and
the other a hidden tax. **Both are wrong, because both assume skill points are a stored stock.** They
should not be.

**FACT — the repo already decided this shape, twice.** `rpg_aptitude_allocation` stores `points` only,
and its own header says *"INPUTS only, never a resolved channel value… a stored channel value would
be a second SSOT that goes stale the moment a coefficient moves"* (`RpgStore.Aptitudes.cs:9-14`). And
[08 §P4](08-effort-power-reconciliation.md) verified the pattern holds repo-wide: *"Every ladder
stores its effort and derives its power"* — `rpg_contract_state` stores `purchased_slots`, never a
price.

**So: persist the owned node set. Derive the budget (`g·Θ`) and derive the spend
(`TotalCostOf(count)`) on every read.** Then:

| | Consequence |
|---|---|
| **Respec** | Clear the node set. The budget is `g·Θ` again, in full. Nothing is "refunded" because nothing was ever debited from a stored balance |
| **Re-buying the same build** | Costs exactly what it cost before — `TotalCostOf` is a function of the count, and the count restarts at 0 |
| **Buy cheap, respec rich** | **Impossible.** There is no stored price to refund at |
| **Hidden tax** | **None.** The same set costs the same amount |

**The lemma that makes this safe, and it should be a test.** `TotalCostOf` depends only on `N`, so
the cost of a *set* of N nodes is independent of the order they were bought in. Without that
property, derive-on-read would disagree with pay-as-you-go and the store would have to remember a
price after all. **INFERENCE, and trivially checkable:** `Σ_{i=1..N}(first + (i−1)·step)` has no
order term.

This is the identical trick D18 used on Grim Dawn's order-sensitivity, applied to the price instead
of the allocation — which is why D25 and D18 compose with no special case.

### 6.2 Which shipped code enforces it

| Concern | Where it lands today | State |
|---|---|---|
| The transaction | `RpgStore.SaveAllocation` / `LoadAllocation` (`RpgStore.Aptitudes.cs:76,112`) — delete-then-insert-nonzero-only, i.e. **already sparse**, which is D21's §7.9 requirement | **Has production callers**, contrary to a note still in circulation: `AptitudeEndpoints.cs:54,99,127`, `WebMatchService.cs:303,388,491`, `AuraDerivedEndpoints.cs:72`. A passive-tree node set is a sibling partial-class slice on the same `_gate`, following the convention that file's own header argues for (`:16-24`) |
| The price of respec | `RespecPolicy.PriceOf` (`RespecPolicy.cs:32-36`) | **Zero production callers**, and it returns `RespecResource.Hunger` — **D18 says souls.** Already flagged (ideal §11.2: *"D18 contradicts `decisions.md:103`… respec is free today"*). D25 does not resolve it and does not need to; recorded so no spec assumes it is settled |
| "Never refused" | Copy `RespecPolicy`'s own shape verbatim (`:26-31`): *"There is no 'cannot respec' return here on purpose… this type only ever answers 'what does it cost,' never 'are you allowed.'"* | Exactly the API discipline PS-8 wants. `ContractPolicy.CanBuySlot` (`:181`) is the second precedent — always `true`, kept as a named check rather than deleted |

**One thing the spec must forbid explicitly, with a comment (PS-8 requires the comment):** no
`Math.Min` on the price, and no narrowing cast on the budget. §11.2a catalogues four shipped ceilings
that a `const` sweep missed because they were written inline, and a clamp here would turn "breadth
got expensive" into "breadth silently stopped" — a bug with no symptom.

---

## 7. What counts as "a node an actor owns"

### 7.1 Souls do not count

**D3 is explicit:** skill points unlock *new bonuses*; souls scale *bonus power*. A soul level is not
a node. Counting them would also be actively harmful — souls are unlimited by design (PS-8, ideal
§4), so a soul-inclusive count makes the unlock price unbounded in a currency with no ceiling, which
converts a soft economic bound into a genuine wall on the *soul* track. That is the exact thing D25
was chosen to avoid. **Structural, not tunable.**

### 7.2 Item-granted points — and D8's precedent, read carefully

**RECALL — the precedent the charter points at.** D8 was amended to *"self-spent only"*, reason:
*"a good off-build drop must never lower your multiplier."* **Read the shape of the trap, not just
its conclusion:** the trap was a **pure penalty with no compensation** — `H` fell, and nothing about
the drop paid for it.

**D25 is not that shape.** Under D11 items grant **points**, not node unlocks, and those points flow
through the tree's own rules. So an item that raises your node count also **paid for the nodes it is
pricing**. The drop funds its own inflation. The two cases differ in exactly the property that made
D8's trap a trap.

**And the alternative is worse.** Counting only self-funded nodes needs per-node provenance — which
node was bought with which currency — and that destroys D11's whole stated advantage: *"points flow
through the tree's own rules, so the tier gate is respected by construction — no special case to
define, enforce or test."* Provenance is such a special case, and one that must then survive respec,
item swaps and migration.

> **Recommendation: count every VALID owned node, whatever paid for it, and let the budget and the
> count move together.**

### 7.3 The one real trap, and its fix

There *is* a ratchet hiding here. D11: removing the gear removes the points, and affected nodes are
*"displayed as invalid (red), never silently repaired."* If invalid nodes still counted, unequipping
an item would leave you paying a higher price for nodes that grant you nothing — a pure penalty with
no compensation, which **is** D8's shape.

**So: the count is over VALID owned nodes.** Invalid ones grant nothing and cost nothing to hold.
Unequipping drops the points and the invalid nodes out of the count together, so there is no ratchet
in either direction. **Structural, not tunable** — and it needs a named test, because *"an item swap
must not change what your next node costs, net"* is the kind of invariant that is obvious in a
specification and easy to lose in an implementation.

### 7.4 Summary

| Thing | Counts toward the price? | Class |
|---|---|---|
| Node unlocked with self-earned skill points | **Yes** | — |
| Node unlocked with item-granted points, item still equipped | **Yes** — the item paid for it | structural `const`, comment cites D11 |
| Node left invalid by unequipping (D11's red state) | **No** — grants nothing, costs nothing | structural `const`, comment cites D8's trap shape |
| Soul level on a node (D3's deepen track) | **No** — different track, unlimited currency | structural `const`, comment cites D3 + PS-8 |
| Nodes on the actor's *other* trees | **Yes** — that is the whole point (§7.5) | — |

### 7.5 The count is across all of an actor's trees, never per tree

A per-tree count would make the 41st node — the first in a *new* tree — cost 5 again, which is the
flat-price hole with an extra step: 39 trees × a cheap first tier is precisely the breadth D25 exists
to price. **INFERENCE with a number:** at `Θ=100` the recommended dial affords 31 nodes, while
39 trees × tier 1 (4 nodes each) is 156. A per-tree count would hand a spread build five times the
breadth for the same budget.

---

## 8. Per-actor or account-wide

**D25 says per actor. Confirmed — but the charter's worry is the right worry and deserves answering.**

**The worry:** D21 gives every actor its own tree state, so a player with fifty demons gets fifty
fresh price ladders. Every new demon starts at price 5. Does breadth just move up a level?

**The answer: no, because the budget is per-actor too.** A fresh demon's skill points come from *its*
index, not the commander's. `PointBudget.PointsFor(scope, sourceValue, tuning)`
(`PointBudget.cs:51-58`) already takes the scope's **own** source value and multiplies by that
scope's own rate; the four sources are `Θ_player` / species level / `element_mastery` / specimen level
(`aptitudes.v5.json:21`). A demon at specimen level 1 has essentially no skill points to spend at
price 5. **Per-actor pricing against a per-actor budget is not a discount — it is a separate,
equally-priced ladder that must be paid for separately.**

**And roster breadth is already priced, by shipped code.** `ContractPolicy.NextSlotPrice(n) =
SlotPriceStep × (n+1)` (`ContractPolicy.cs:176-177`) — the same arithmetic shape, with §11.1a's own
verdict: *"The hard cap was redundant. Scarcity came from the escalating price, not from the ceiling
at 48."* A fifty-demon roster costs six figures in souls before a single node is bought. **The two
escalations compose**, and neither needed a cap.

**Why account-wide would be wrong, not merely different.** It would mean building a demon makes your
commander's next node dearer. That inverts D21's stated purpose (*"each demon is genuinely built, not
a stat block"*) and re-imports D8's trap at roster scale — a second actor becomes a pure penalty on
the first.

### 8.1 The wiring requirement this creates, and it is load-bearing

**FACT.** `AptitudeGrant` (`AptitudeTuning.cs:13`) carries `SkillPointsPerThetaMilli` as a **single
scalar**, while `AptitudePointEconomy` (`:43-45`) carries `AptitudePointsPerThetaMilliByScope` as a
**four-scope dictionary** (`:199-205`). Skill points have no scope table.

**INFERENCE.** With one scalar, "an actor's skill points" has no per-actor definition. The path of
least resistance — every actor reads `Θ_player` — would give a fifty-demon roster fifty *full
commander budgets*, each on its own fresh price ladder, and **that genuinely does defeat the bound**:
fifty actors × 31 nodes at `Θ=100` is 1,550 nodes, the whole generic catalog owned across the roster
at the calibration point.

> **Requirement: `pointEconomy` gains `skillPointsPerThetaMilliByScope`, mirroring
> `aptitudePointsPerThetaMilliByScope` one line above it.** The precedent is not merely nearby, it is
> in the same block, and `spec-point-economy.md` §2.2's own phrasing — *"the single number becomes a
> table"*, quoted at `aptitudes.v5.json:20` — is the argument, already made once for the sibling
> field.

**Blocked-on, tracked rather than open:** the four scope sources are in different states. Specimen
level now reads the shared arithmetic curve (08's M1 fix, `progression.v1.json:16`); species level is
an index (`PointBudget.DemonTypeSourceFromLevel`, `:35`); `element_mastery` does not exist
([08 §3](08-effort-power-reconciliation.md)). D25 does not need all four — it needs whichever scopes
ship trees.

---

## 9. Does D25 need a §10 row? — **Yes, exactly one**

Following [04-number-and-atom-binder.md §4.4](04-number-and-atom-binder.md)'s method, which answered
the same question for the soul track by splitting the design into halves and finding a precedent for
each:

| Half of D25 | New §10 row? | Precedent |
|---|---|---|
| **The cost function** — `first + (N−1)·step` over `nodesOwned` | **Yes — one row in §10.2** | Rows 26 and 27 are the direct precedent: both are *cost* ladders of exactly this shape, both got their own row rather than reusing row 6, and both give the same reason — *"a separate row rather than reusing row 6 because it reads its own tunable pair"* (row 26). D25 reads its own `(first, step)` in its own file |
| **The reward** — a node's magnitude | **No** | Row 16's `pThetaTermMilli` precedent: reading the shared `PowerLadder` is never a new scale. [04 §11.1](04-number-and-atom-binder.md) already establishes that the binder emits a **coefficient**, and the coefficient read path ships (`AtomCompiler.cs:463-464`) |
| **The soul→`Θ` weight `Ws`** | Already owed | 04 §4.4 claimed it; D25 adds no second one |

**§10.2, not §10.1, and the distinction matters.** Rows 6, 26 and 27 sit in §10.1 (*"Level curves —
these collapse into `Θ`"*) because their index **is** a level. D25's index is `nodesOwned`, a
per-actor counter. That is row 19's standing exactly — *"Input is `earnCount` (a per-holder counter,
never `Θ`), so this is not a level curve and never collapses into `Θ`"* — and row 19 is in §10.2.
**Row 6's verdict, row 19's placement.**

**Proposed row text:**

> | 29 | Passive-tree unlock cost — `cost(N) = first + (N−1)·step`, cumulative `N·first + step·N(N−1)/2` | arithmetic; triangular cumulative; `long`, `checked`, overflow throws | `Stats/PassiveTree/UnlockCostPolicy.cs`, `data/tuning/passive-tree.v1.json` | **Added by D25.** A **cost** ladder, exempt from the one-ladder rule for row 6's reason — only its ratio against `P(Θ)` matters (§10.5), and [12-rising-unlock-cost.md §4](../../research/passive-tree/12-rising-unlock-cost.md) proves that ratio is constant rather than assuming it. Input is `nodesOwned`, a per-actor counter, never `Θ` or a level, so it never collapses into `Θ` — row 19's standing. Its own row rather than row 6's because it reads its own tunable pair, per rows 26/27. `first = step·(k+1)/2` is a **coupling, not two dials**: it is what makes reward-per-skill-point flat at every tier, the same index reconciliation D26 performed for aptitude points. What the node *pays out* is not exempt and reads `P(Θ)` through the coefficient binder. |

**And an `inventory.json` row in the same change.** The mirror's own `_meta.rebalance` says *"Adding
a row is a reviewed change to `ssot-power-scale.md` §10 first, this file second"*, and
[08 §5](08-effort-power-reconciliation.md) found *"the second half did not happen, twice."*

**The guard will not catch its absence — say so out loud.** `guard-power.ps1`'s G2/G3 regex
(`scripts/guard-power.ps1:74`) matches a method whose parameter is named `level`, `lvl` or `index`.
`CostOfNextNode(long nodesOwned, …)` matches none of those. This is the identical blind spot
`DropVolume.VolumeScaleMilli` sat in — *"it passes the guard only because `thetaActor` is not spelled
`level`"* ([08 §5](08-effort-power-reconciliation.md)). **A passing guard is not evidence.** The row
has to be added deliberately.

---

## 10. Bounds, refusal, and the stealth-cap check

| Question | Answer |
|---|---|
| Where does a `long` price overflow? | `cost(N)` at `N ≈ 4.6×10¹⁸`; cumulative at `N ≈ 3.04×10⁹`. The largest catalog anyone has proposed is **25,900** nodes (D29's 1,560 + D30's 24,389). **Five orders of margin.** A type boundary, not a ceiling |
| Where does the *budget* overflow? | `g·Θ` at `Θ = MaxIndex ≈ 2.147×10⁸` is `2.36×10⁹` — **past `int.MaxValue`.** This is the real reason both sides must be `long` |
| Is the whole catalog reachable? | **Yes.** Generic catalog at `Θ = 221,804`; everything including species trees at `Θ ≈ 6.1×10⁷` — both inside `PowerLadder.MaxIndex` (`PowerLadder.cs:65`) |
| Is a node ever refused? | **No.** `N(Θ)` is strictly increasing and unbounded (§2), and `Θ` is uncapped by decision (§5.2). "Cannot afford yet" is deferral, not refusal |
| Soft bound or stealth cap? | **Soft bound — provably**, as long as three things hold: no `Math.Min` on the price, no narrowing cast on the budget, and no boolean `CanUnlock` in the API. All three are §11.2a's catalogued failure modes, and all three are cheap to forbid in the spec |
| Does it need a PS-8 comment? | **Yes.** PS-8 requires exemptions to *say so*. `CostOfNextNode`'s comment should state that this is a soft economic bound, that nothing is refused, and that the absence of a clamp is deliberate |

---

## 11. Measurement plan — what would prove D25 works

Everything above is algebra. The win-share consequence is not, and the harnesses to measure it exist.

**FACT — the scorer needs no change.** `DominanceGuard.Measure(IReadOnlyList<AptitudeAllocation>, long theta)`
(`Balance/Guards/DominanceGuard.cs:38`) already takes an arbitrary build list — the same finding the
ideal's §3.3 made for `Fmax` and 09 made for cross-unlock.

**FACT — the tree model does need one.** `tools/HybridViability/Program.cs:226-288` computes tree
power as `w = b·tier·(tier+1)/2` **straight from `TierFor(p)`**, i.e. it assumes every node below the
gate is free. That assumption is exactly what D25 removes, so the existing `--trees` numbers are the
no-D25 baseline and nothing more. Two further gaps in the same block: `TierFor` (`:231-235`, formula at `:234`) still
implements **D20**'s `10 + 2.5·t(t−1)`, not D26's `5·t(t+1)/2`, and there is no ceiling at D29's tier
ten.

### 11.1 The change

Add `--unlockcost` to `tools/HybridViability`:

1. Give each build a skill-point budget `g·Θ`.
2. Buy nodes **greedily deepest-first within the gates** each tree opens — the optimal play, so the
   result bounds the mechanism rather than describing player skill.
3. Compute `W_i` from **nodes actually owned**, never from the tier the gate opened.
4. Use D26/D29's ladder, capped at tier 10.

### 11.2 The quantity to sweep

**The breadth dial `g/step`** — the single ratio §4.4 shows the calibration turns on. Sweep
`g/step ∈ {1, 2, 5, 10.4, 20, 50}` × `Θ ∈ {50, 100, 170, 500, 1000}`, holding
`first = step·(k+1)/2` so the flatness condition is not accidentally varied at the same time (T7's
two-step rule, applied to a sweep instead of a refactor).

### 11.3 Success criteria, stated before the run

1. **Ordering.** `corner` mean win share > `spread` at every `Θ`. Today, with no unlock cost, spread
   wins in every cell of the `--trees` sweep (ideal §3.5), and cross-unlock's largest-mate rule wins
   by 2.2 points (09).
2. **Scale invariance.** The margin does **not** decay as `Θ` rises. §4.2 predicts it should not,
   because both constraints give `T ∝ √Θ`. A decaying margin means the algebra is wrong somewhere,
   and is the single most valuable thing this sweep could find.
3. **Mechanism visible.** Report **node counts per build kind**, not only win share. §5.2 predicts
   31 vs 31 affordable against 28 vs 96 opened. A sweep that moves win share without moving those
   counts is measuring something else, and would be a false positive.
4. **Flatness, with no simulator at all.** A plain unit test: `TotalCostOf(4T) / W(T)` is constant in
   `T` for `first = step·(k+1)/2`, and demonstrably **not** constant for `first = step`. The cheapest
   proof in the plan, and the one that guards §4.3 against a later retune.

**Artifact:** `docs/research/passive-tree/_unlock-cost-sweep.json`, matching
`_hybrid-viability.json`'s existing convention.

---

## 12. Open items

Two, and neither blocks a spec.

1. **What "the tier below is unlocked" means** — at least one node in the tier below, or the tier
   complete? §3.4's calibration assumes **complete**, which is the conservative (most expensive)
   reading, and §4.3's exact flatness is a property of it. The cheaper reading changes `k` from 4 to
   2 in both derivation rules, so it is worth deciding before `first` is published.
   **Recommendation:** at least one node in the tier below **of the same branch** — it preserves
   D10's two-branch identity and rewards a single-branch dive, whose reward-per-point gradient
   (§4.3's caveat) already points the right way.
2. **The four values of `skillPointsPerThetaMilliByScope`.** §8.1 requires the table; the rates
   themselves are a balance question residual-fit owns, exactly as the aptitude table's own
   `_weightsWhy` says of its `{3,4,4,6}` (`aptitudes.v5.json:22`). Shipping a guess is fine; calling
   it balance is not.

**Not open, tracked elsewhere:** `RespecPolicy`'s Hunger-versus-souls contradiction with D18 (§6.2)
belongs to whoever prices respec, not to D25.

---

## 13. Design-gate checklist

```
[x] Subsystems identified: power ladder, tunables, class-system point economy,
    passive tree, progression cost ladders, aptitude allocation persistence.
[x] Read this session: DESIGN-GATE.md (full), passive-tree-ideal.md (full, all 32
    decisions), ssot-power-scale.md 10 / 10.1 / 10.2 / 10.3 / 10.5 / 10.6 / 11 /
    11.1 / 11.1a / 11.2 / 11.2a / 11.3 / 11.10 / 12, tunables-ssot.md (full),
    research/passive-tree/08-effort-power-reconciliation.md (1-360),
    research/passive-tree/09-crossunlock-sweep.md (full),
    research/passive-tree/04-number-and-atom-binder.md 4.4 and 5.1,
    CLAUDE.md numeric-overflow and caps sections.
[x] Lock check: D25 is an owner decision recorded in passive-tree-ideal.md 2; no
    decisions.md row covers unlock cost. PS-8 and PS-5 were both read in their own
    sections, not quoted from a summary.
[x] Every factual claim cites file:line.
[x] Verified against CODE, not comments: AptitudeTuning.cs (parse and
    NonNegativeMilli), PointBudget.cs, RpgProgression.cs (XpToNext/TotalToReach),
    ContractPolicy.cs (NextSlotPrice/Capacity/CanBuySlot), RespecPolicy.cs,
    RpgStore.Aptitudes.cs, PowerLadder.cs, SoulSinkPolicy.cs, guard-power.ps1,
    HybridViability/Program.cs, aptitudes.v5.json, progression.v1.json.
[x] Read the surrounding section of every rule quoted: PS-8 read with its "a
    ceiling need not be a const" paragraph; D26 read with its req/W indexing
    explanation; D8's amendment read for the SHAPE of its trap rather than its
    conclusion; 11.1a read in full before reusing "the price was already the cap".
[x] Constraints tested rather than assumed: the "zero consumers" claim for
    skillPointsPerTheta was re-grepped (2 hits, declaration and parse). The
    "AllocationStore has zero production callers" note still in circulation is
    STALE -- LoadAllocation/SaveAllocation have seven production call sites,
    listed in 6.2. s = 0.54163 was read out of HybridViability's corner
    construction rather than assumed to be 1.0.
[x] Nothing contradicts a section-2 invariant. Invariant 11 (no hard ceilings) is
    the closest, and section 10 proves compliance rather than asserting it.
[ ] NOT propagated yet -- this is research, and three downstream edits are owed
    when a spec lands: ssot-power-scale.md 10.2 row 29, power/inventory.json's
    mirror row, and passive-tree-ideal.md D25's row (the concentration coupling
    from 5.4, and the corrected Theta = 1,560 in 11.2's closed line, which still
    reads 1,450).
[ ] NOT measured -- section 5's reversal is algebra over the shipped tree model,
    not a run of it. tools/HybridViability has no skill-point budget today
    (section 11). Section 11 is the plan; the numbers in 5.2 are INFERENCE and are
    marked as such.
```
