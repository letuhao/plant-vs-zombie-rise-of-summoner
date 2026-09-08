# Decision consistency audit — what D25–D32 broke (2026-09-05)

**Status:** research. Not a spec, no build authorized. Read-only pass; nothing in `src/`, `tools/`,
`tests/` or `data/` was touched.

**The question.** [`passive-tree-ideal.md`](../../architecture/passive-tree-ideal.md) now carries 32
owner decisions. **D25–D32 landed on 2026-09-05, after documents 01–07 were written**, and documents
08 and 09 were written mid-stream. This audit walks every place a later decision invalidates an
earlier finding, an earlier decision, or a shipped fact.

**Evidence marking** follows the sibling docs. **FACT** = read in code, data or a doc this session,
with `file:line`. **INFERENCE** = algebra over facts. **RECALL** = not verified in-repo.

**Arithmetic in this document was computed, not quoted.** Where I re-derive a published number I say
so and show the working, per evidence rule 5.

⚠️ **Working-tree caveat.** `tools/HybridViability/Program.cs` was modified at 05:44 today and now
carries `D29MaxTier` (`:312-317`), which was **not** in the build that produced
[09-crossunlock-sweep.md](09-crossunlock-sweep.md). All line numbers below are working-tree values.

---

## 0. Findings, severity-ranked

| # | Sev | What breaks | Evidence | Fix |
|---|---|---|---|---|
| A1 | **Critical** | **D30 defeats D24's own reason to exist.** D24 is a learnability decision; D30 makes the catalog scale with the roster, so a 30-demon player must learn 2,430 nodes and a 100-demon player 4,460 — against PoE's ~1,300, which doc 07 already called too many | `ideal:58` vs `:64,412-425`; `07:132,137-142,177-190` | Adopt `06:315`'s shared pool + small unique cap, or state that D24's learnability criterion applies to generic trees only |
| A2 | **Critical** | **D25 is a cap by the caps register's own definition and has no §11 row.** *"A flat rate facing a scaling cost"* is named a cap in `ssot-power-scale.md:783`; §11.7 is exactly that shape. D25 asserts PS-8 compliance instead of registering a verdict | `ideal:61`; `ssot-power-scale.md:778-787`; §11.10 is the right section | Add a §11.10 row with a verdict, and a §10 cost-ladder row beside the two doc 02 already owes |
| A3 | **High** | **D25 has never been modelled, and it is the one thing that actually unsupports doc 09.** Every published sweep sets `W = b·T(T+1)/2` — you own *every* node up to your tier. Under D25 you own `O(√Θ)` of them | `Program.cs:262,373`; `ideal:61` | Add a `--d25` term before any further win-share argument; D28's adoption rests on the un-modelled numbers |
| A4 | **High** | **D25 repeats the exact defect D26 was adopted to fix.** It claims *"same arithmetic-cost shape the soul track already uses, so it adds no new ladder"* — matching the shape while mismatching the index is doc 08's whole thesis, and the one shipped count-escalating cost that *was* audited (`StarPolicy`) graded **DECAYING**, *"the same index mismatch as D20"* | `ideal:61`; `08:§1 row 9`; `StarPolicy.cs:76-82` | State D25's reward index and pair it, the way D26 paired `req` to `W` |
| A5 | **High** | **D31's three named collisions are the wrong three, and it misses a live-balance one.** `AptitudeAffixPrice.cs:32` is ANDed with a `false` const so nothing flips; `AptitudeAllocation.cs:103` auto-sums a new scope into the aptitude **share denominator**, which `decisions.md:103` locks | `AptitudeAffixPrice.cs:30,32`; `ItemPowerReadsTests.cs:206`; `AptitudeAllocation.cs:103`; `AptitudeTuning.cs:199-205`; `PointBudget.cs:57` | Re-write D31's consequence list against §5's table |
| A6 | **High** | **D31's "blocked until item takes slot 5" is an assumed constraint.** Nothing in the repo reads the ordinal — SQLite stores `scope` as TEXT, zero `(int)scope` casts, zero DTOs, zero web mirror. Slot *number* has no technical meaning | `RpgStore.Aptitudes.cs:36-47,53-70`; `Contracts`/`web` = zero hits | Drop the blocker or restate it as a coordination preference |
| A7 | **High** | **D29's 40 nodes/tree violates doc 02's G9, and 1,560 omits D27's family trees.** `2×nodesPerBranch+1` is always odd; 10 tiers × width 2 gives **41**. And `39 = 12+6+21` — D27 ships family too | `ideal:57,63`; `02:§2.2 G9, §2.3` | Say whether the shared root survives; restate the corpus as a function of `n` and `F` |
| A8 | **High** | **Every T-dependent number in doc 02 is stale, and the potency ceiling is stale in the direction that matters.** `maxNodeShare = 1/((T+1)·w[T])` is **91‰** at T=10, not the `125` emitted in §6, §7.5 and §9.1 | `02:§0,§2.3,§3.3,§4.2,§4.3,§5.2,§6,§7.3,§7.4,§7.5,§9.1,§12` | Re-emit the plan block at `tierCount=10`; the derivation is right, the constants are not |
| A9 | **Medium** | **D30's 29 nodes contradicts D29's 40 and D10's "same shape everywhere".** D30 anchors on doc 02's T2 recommendation, which D29 rejected the same day | `ideal:42,57,58`; `02:§2.3` | Pick one, or amend D10 to allow a species archetype |
| A10 | **Medium** | **D30 restates the D24 review tension rather than resolving it.** D24 §10.2 says balance is checked *"against the real corpus"*; D30 answers with *"batching, sampling gates"* | `ideal:58,441-442`; `06:302-305,315` | Call it an amendment to D24, not a compatibility claim |
| A11 | **Medium** | **D32's named allowance is far below the measured skew, so D32 is a re-assignment mandate.** Earth is 2.7× uniform, not 1.5×; the aptitude axis gets no allowance at all and needs ~500 species moved | `ideal:60` vs `:370,384` | Say the quota is a *re-assignment*, and name the thematic cost |
| A12 | **Medium** | **D25 × D11 reopens the trap D11's amendment closed.** Gear-granted points advance the escalation counter, so an off-build drop permanently raises the price of every later on-build node | `ideal:40,43,61` | Rule whether gear-fed spends advance the counter |
| A13 | **Medium** | **D25 makes `H` order-dependent** — the thing D18 was adopted to dissolve. Total cost stays order-free; the *per-tree points share* does not | `ideal:40,50,61` | Read `H` on node budget or node count, never on points paid |
| A14 | **Medium** | **D25 is per actor and D21 gives every actor its own tree state**, so the breadth bound does not reach the roster. Each new demon restarts the ladder at `c0` | `ideal:53,61` | Say whether legion-scale breadth is deliberately unpriced |
| A15 | **Low** | **D29's `Θ≈170` uses a different sizing rule from doc 02's own.** `02:§3.3` fixes `tierCount = T_max(Θ, s=1)`, which gives `Θ ≈ 92`; D29 computes at `s = 0.542` | `ideal:57`; `02:§3.3`; `Program.cs:79-80,86` | State which convention the tunable `_note` records |
| A16 | **Low** | **D25's own `Θ≈1,450` is stale** — it quotes a 1,450-node catalog that D29 replaced the same day | `ideal:61` vs `:57` | One-word fix |
| A17 | **Low** | **D17 says 165:1; §9 measures 332:2 = 166:1** | `ideal:49` vs `:384` | One-character fix |
| A18 | **Low** | **~20 stale volume figures across docs 01, 03, 06, 07 and the ideal itself**, all descended from `~50 × 29 = 1,450` | listed in §9 | Propagate once, per evidence rule 6 |

**Refuted from the brief** — findings I was asked to confirm and could not: the premise that doc 09 ran
on D20's old ladder and a 7-tier bound (§1); the premise that D18's respec makes D25 an exploit or a
tax (§4.3); the premise that D17 makes D32 unachievable (§8); the premise that
`AptitudeAffixPrice.cs:32`'s `> 4` branch is a live gate (§5).

---

## 1. D29 vs doc 09 — the sweep survives, and the premise does not

**The brief asked:** does the largest-posture-mate conclusion survive D26's ladder and D29's tier
count, or is D28 resting on a stale measurement?

**It survives both.** Three checks, all computed.

### 1.1 D26 was already in the sweep — half the premise is false

**FACT.** `tools/HybridViability/Program.cs:311`:

```csharp
static int TierD26(double p) { var t = 0; while (5.0 * (t + 1) * (t + 2) / 2.0 <= p) t++; return t; }
```

Doc 09's own result table carries four `D26` rows next to four `D20` rows, and says so
(`09`: *"against both tier ladders (D20 as written, and D26's reconciled ladder)"*). D26 is the
adopted ladder, so the D26 rows are the operative half of that table and they were never run on the
old ladder. Doc 09 also records the verdict directly: *"D26 does not change the ordering… Corner is a
hair better under it (50.0% vs 49.9%)."*

### 1.2 Doc 09 never assumed seven tiers

**FACT.** `TierD20` (`:310`) and `TierD26` (`:311`) are unbounded `while` loops. There was no
authored-depth ceiling in the build that produced doc 09. `D29MaxTier = 10` and the two capped
variants (`:315-317`) were added to the file **today at 05:44**, after doc 09 was written at 01:23.

So doc 09 did not model a 7-tier tree. It modelled an *uncapped* ladder, which is strictly deeper than
either D20's seven printed rungs or D29's ten.

### 1.3 D29's ten-tier cap cannot bind at the swept Θ — arithmetic

**INFERENCE, from three facts.** Θ = 100 and the budget is 300 aptitude points
(`09`: *"Θ=100, budget 300 aptitude points"*). Under every one of the four credit rules the gate is
`p_i + credit(mates)`, and no credit rule can exceed the sum of all other trees' points — so the gate
is **bounded above by the whole 300-point budget**, for every build in the 52.

Invert the D26 ladder at exactly 300:

```text
req(t) = 5·t(t+1)/2
req(10) = 5·10·11/2 = 275  ≤ 300      ✅
req(11) = 5·11·12/2 = 330  >  300     ❌      ⇒ T_D26(300) = 10 exactly
```

**So `TierD26Capped` is byte-identical to `TierD26` for every build in doc 09's sweep.** The four
`D26@10` rows the owner's re-run will produce must reproduce the four `D26` rows exactly. If they do
not, the tool changed something else.

The D20 half moves, but only marginally and only in a superseded row:

```text
req(t) = 10 + 2.5·t(t−1)
req(11) = 10 + 2.5·110 = 285 ≤ 300    ✅
req(12) = 10 + 2.5·132 = 340 >  300   ❌      ⇒ T_D20(300) = 11
```

A cap at 10 removes one tier from whichever build reaches the top of the budget — worth
`b·(11·12/2 − 10·11/2) = 11·b` out of `66·b`, a −16.7% shave on one tree in the `full`-credit corner
cell only. That cell is on the ladder D26 superseded.

### 1.4 What IS unsupported in doc 09 — and it is not D29

**D25.** Doc 09's tree-power term is `Program.cs:373`:

```csharp
var w = B * tier * (tier + 1) / 2.0;    // power still linear per tier
```

That is the cumulative power of **owning every node up to your tier**. D25 prices nodes so you cannot.
At an arithmetic unlock cost `c(k) = c0 + (k−1)d`, `Θ` skill points buy `N ≈ √(2Θ/d)` nodes, not
`Θ` of them. So the model over-counts tree power for every build in every published table, and the
`treePwr in4/pure` column — the column D28 was decided on — is the one most exposed, because the two
builds differ precisely in how many trees their nodes are spread across.

> **Stated plainly: roughly none of doc 09 is invalidated by D26 or D29, and all of its win-share
> numbers are now conditional on D25 being modelled.** D28 should be marked *measured, pending a D25
> re-run* rather than closed.

**This does not mean D28 is wrong.** The mechanism doc 09 identified — a pure build's whole posture
borrowing from one large tree — is a *gate* effect and D25 acts on *unlocks*, so the direction is
likely preserved. But "likely" is what this program stopped accepting when it ran the sweep.

---

## 2. D29 vs doc 02 — what is T-dependent, and the proof that is not

### 2.1 The equal-value proof IS T-independent — verified, not assumed

The brief asked me to verify rather than assume. `02:§4.2`:

```text
tierBudget[t] = B_b · t / T_tri ,   T_tri = tierCount·(tierCount+1)/2
Σ_{t=1..T} tierBudget[t] = B_b · (Σ_{t=1..T} t) / T_tri = B_b · T_tri / T_tri = B_b
```

`Σ_{t=1..T} t` **is** `T_tri` by definition, so the cancellation is an identity in `T`. It holds at
`tierCount = 10`, at 7, and at any other value. **CONFIRMED T-independent.** D15/R6 survives D29
untouched, and `w[t]` still never appears in the sum, so shape freedom survives too.

### 2.2 The rounding rule §7.3 warns about also survives — computed

`02:§7.3` flags that `tierBudgetMilli` sums to 1000 *"only under a fixed rounding rule"* and that the
residual is 0 at `tierCount = 7` but *"at other tier counts it will not be"*. At `tierCount = 10`,
`T_tri = 55`, `round(1000·t/55)` half-up:

| t | 1 | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | Σ |
|---|---|---|---|---|---|---|---|---|---|---|---|
| exact | 18.18 | 36.36 | 54.55 | 72.73 | 90.91 | 109.09 | 127.27 | 145.45 | 163.64 | 181.82 | 1000 |
| rounded | 18 | 36 | 55 | 73 | 91 | 109 | 127 | 145 | 164 | 182 | **1000** |

Residual is **0** at ten tiers as well. The warning stands as a rule; it happens not to bite here.

### 2.3 What is T-dependent and now wrong

| `02` § | Result | At `tierCount = 10` |
|---|---|---|
| §0 answer table | *"7 tiers × 2 branches, 14 nodes/branch, **29 nodes/tree**… corpus **1,131** at n=39; **1,682** at n=58"* | 20 nodes/branch; 41/tree (G9) or 40 (D29); 1,599 / 2,378 |
| §0 answer table | *"Node potency ceiling… **0.125**"* | `1/((10+1)·1)` = **0.0909** → 91‰ |
| §2.3 | The whole T1/T2/T3 table — all ≤ 7 tiers | D29 chose none of the three |
| §3.2 | `req(t) = 10 + 2.5t(t−1)` and its 10-value table | superseded by D26 |
| §3.3 | `T_max` table, `T ≈ √(1.2Θ)`, *"tierCount = 7 corresponds to Θ_designTarget ≈ 39"* | D26 changes the inverse; at s=1, `req(10)=275` opens at `Θ ≈ 92` |
| §3.4 | Tier 1 is a 2× worse deal; the ±11% band | **closed by D26** |
| §4.2 | `T_tri = 28` | 55 |
| §4.3 | All three archetype tables, widths `[…7 entries…]`, `nodeBudget` rows, *"strongest single node .2500"* | 10-entry width vectors; strongest node is `.1818/w[10]` |
| §5.2 | `mechNodes` ramps `[0,0,1,1,1,2,2]` etc | 10-entry ramps |
| §6 | `maxNodeShareMilli = 125` and R-P1/R-P2 | **91**; shipping 125 at T=10 would admit a capstone worth **1.37×** the derived ceiling |
| §7.3 | `ladder` block — `tierCount: 7`, `reqBase: 10`, `req: [10,…,115]`, `tierBudgetMilli: [36,…,250]` | all replaced; **and `reqBase` is now 0** under D26, so the emitted formula string `reqBase + reqStep·t(t−1)/2` is wrong twice |
| §7.4 | `"tier": [1,2,3,4,5,6,7]` in `propertyVocabulary` | `[1..10]` |
| §7.5 | All three archetype blocks, `maxNodeMilli` 125/250/89 | all replaced; `gated-deep`'s 250 already violated `maxNodeShareMilli` at T=7 and violates 91 by 2.7× at T=10 |
| §9.1 | `topology.tierCount: 7`, `nodesPerBranch: 14`, `ladder.reqBase: 10`, `potency.maxNodeShareMilli: 125` | all replaced |
| §11 row 1 | *"`DESIGN-GATE.md` says 5/12/8"* | **fixed.** `DESIGN-GATE.md:40` now reads 7/16/13, verified 2026-09-05 |
| §11 row 3, §12 q2 | D20's flatness / the `reqBase` decision | **closed by D26** |
| §12 q1 | The family-roster decision | **closed by D27** (ship whole, curate as build-order work) |
| §12 q3 | Topology T1/T2/T3 | **closed by D29**, with a fourth option none of them named |

The `§6` ceiling deserves its own line because doc 02 argues it *"falls out of the topology, so a
change to `tierCount`… moves it by construction — no second edit."* That is true of the **formula**
and false of the **emitted constant**: §7.5 and §9.1 both carry `125` as data. A tierCount change is
therefore a two-place edit today.

---

## 3. D25 has never been measured — what it touches

D25 (`ideal:61`) is the only decision in the set that introduces a **new economic mechanism**. It has
no spec, no sweep, no tunable and no register row.

### 3.1 There IS a shipped precedent — two of them

The brief asked me to find one or say there is none. **There are two, both arithmetic in count-owned,
both per actor.**

**FACT.** `src/FusionRpg.Core/Demons/Contracts/ContractPolicy.cs:176-177`:

```csharp
public static long NextSlotPrice(int purchasedSlots, int thetaContent, PowerTuning tuning) =>
    SoulSinkPolicy.Price((long)SlotPriceStep * (Math.Max(0, purchasedSlots) + 1), thetaContent, tuning);
```

and its sibling `:168-171` carries **D25's own argument, already made and already accepted**:

> *"no ceiling — the escalating price (see `NextSlotPrice`) was always the real scarcity control, not
> this `Math.Min`. A roster of 2,012 costs 600,300,000 cumulative souls; that is the limit, not a
> hard-coded 48."*

`ssot-power-scale.md:815` (§11.1a, *"Why removing `MaxSlots` is safe — the price was already the cap"*)
is the register entry for that reasoning. **This is strong support for D25's shape.** It is also the
model for what D25 is missing: `NextSlotPrice` is wrapped in `SoulSinkPolicy.Price` so it tracks its
faucet, and it has a register row.

**FACT.** `src/FusionRpg.Core/Demons/Fusion/StarPolicy.cs:76-82` — `SacrificesForStar(n) = n + 1`,
cumulative `n(n+3)/2`. Same shape again.

### 3.2 The precedent that was audited failed the test D26 just introduced

**FACT.** `08-effort-power-reconciliation.md` §1 row 9 grades `StarPolicy`:

> *"`60/(n+3)` — a 2× worse deal at star 5 than star 1 · **DECAYING** — the same index mismatch as
> D20, bounded at 5 rungs."*

So the one count-escalating cost in this repo whose reward pairing has been audited was found to have
**exactly the defect D26 was adopted to remove**. D25 says (`ideal:61`):

> *"Same arithmetic-cost shape the soul track already uses (§4), so it adds no new ladder."*

**That is a shape claim standing in for an index claim, and doc 08 exists because those are different
things.** The soul track's index is a *per-node soul level*; D25's index is *nodes owned across the
whole actor*. Matching the shape while mismatching the index is precisely M1's defect (specimen level:
flat cost, quadratic reward, `08:§2 M1`) and precisely D20's (`req` on `t(t−1)/2`, power on
`t(t+1)/2`). D25 owes the same reconciliation D26 performed, and nobody has done it.

### 3.3 D25 is a cap by the register's own definition

**FACT.** `ssot-power-scale.md:782-787`, PS-8's own elaboration:

> *"A ceiling need not be a `const` to be a ceiling, and it need not be named like one. An inline
> `Math.Min`, a narrowing `(int)` cast, **a flat rate facing a scaling cost**, and a *threshold* that
> halves a payout are all caps. … §11.7 is a cap that was not a number at all — a flat faucet against
> a scaling sink."*

`skillPointsPerTheta: 1` (`data/tuning/aptitudes.v5.json:15-17`, FACT via `02:§3.1`) is a **flat
rate**. D25 makes it face a **scaling cost**. By the register's own worked example this is a cap of
the §11.7 kind, and PS-8 (`:778-780`) says *"everything else is a wall on the grind and **needs a
verdict in this table**."*

D25 asserts instead: *"a soft economic bound, not a ceiling, so PS-8 is satisfied — nothing is
refused."* **"Nothing is refused" is not the test PS-8 states.** The test is a registered verdict.
`§11.10 Content breadth — a different axis` is where it belongs, next to the species-cap row whose
own §11.10a exists to record a breadth cap the sweep got wrong the first time.

This is cheap to fix and expensive to leave: `§11.10a`'s lesson is *"a cap that is already binding does
not read differently from one that is not — which is why 'is this reached?' must be computed, not
judged."*

### 3.4 D25 × D3's two-track economy

D3 (`ideal:35`) gives every skill two tracks: **unlock** (skill points, discrete, finite) and
**deepen** (souls, unlimited, arithmetic cost). §4's proven property — `ssot-power-scale.md` §10.5 —
is that a quadratic cost against a quadratic power makes **power ∝ effort**, and `ideal:481` names the
failure mode by name:

> *"Scaling the coefficient instead would give power ∝ √effort, which is §4's claim failing (red team
> F9, now answered)."*

D25 puts a quadratic cumulative cost on the **unlock** track, whose reward per unit is a *discrete
node*, not a scaling coefficient. Whether that lands on `∝ effort` or `∝ √effort` depends entirely on
how node value grows with node index — which the plan does fix (`02:§4.2`: node budget rises linearly
with tier), and which nobody has written down against D25.

**INFERENCE, and it is the sharpest open question in the set.** With `w` nodes per tier per branch,
reaching tier `T` means owning `N = 2wT` nodes and holding cumulative branch budget
`∝ T(T+1)/2 ∝ N²`. D25's cumulative cost is `≈ (d/2)N²`. **Those two are both quadratic in `N`, so
reward-per-skill-point is asymptotically flat — the same property D26 just proved for the gate axis.**
But only asymptotically:

```text
cost of the w·2 nodes at tier t   =  2w·c0 + d·(2w)·(2w·t − (2w+1)/2)     ← linear in t
reward at tier t                  =  b·t                                   ← linear in t
⇒ flat exactly iff  c0 = d·(2w+1)/4 ;  otherwise tier 1 is again a worse deal
```

So D25 has a tier-1 entry tax of its own, on a second axis, with the **same shape D26 was adopted to
remove from the first axis** — and it is removable by one relation between `c0` and `d`. That relation
depends on `w`, so it is **archetype-dependent**, which is the one thing `02:§4.2`'s equal-value proof
deliberately kept `w` out of.

### 3.5 D25 × D18 — checked, and they are consistent

The brief asked whether a respec refunds at the escalated price or at base, and warned that getting it
wrong is either an exploit or a tax.

**It cannot be an exploit, because of D18.** `Σ_{k=1..N}(c0 + (k−1)d)` depends only on `N`, never on
the order the nodes were bought. D18 (`ideal:50`) makes respec a **full reset** with no partial path,
so there is no state in which you can sell an expensive node and re-buy a cheap one. The arbitrage D25
would otherwise create is foreclosed by a decision that predates it. **This is a genuine, unremarked
piece of luck and it should be written down before someone proposes partial respec.**

**It can be a tax, and that is a one-line spec gap, not a contradiction.** If the refund is
`N × c0` rather than the cumulative sum, the player silently loses `d·N(N−1)/2`, which grows
quadratically with build size. Say "refund is the cumulative ladder sum" in the spec and it is closed.

### 3.6 D25 × D11 — the trap D11's amendment closed, reopened with the sign flipped

D8 as amended (`ideal:40`) is self-spent-only for a stated reason: *"a good off-build drop must never
lower your multiplier."* D11 (`:43`) makes items grant **points**, and keeps nodes whose points went
away **displayed as invalid (red), never silently repaired**.

**INFERENCE.** Under D25 the escalation counter reads *nodes owned*. Points from gear buy nodes; those
nodes advance the counter; the counter raises the price of every later self-spent node. Un-equipping
does not remove the node (D11 keeps it red), so plausibly does not unwind the counter either.

So: **an off-build drop cannot lower your focus multiplier, but it can permanently raise the price of
your on-build nodes.** Same class of trap, opposite sign, and D11's amendment note does not reach it.
Two clean rulings close it — either gear-fed spends do not advance the counter, or a red node does not
count as owned — and both are one sentence.

### 3.7 D25 × D8 — `H` becomes order-dependent

D8 says `H` reads **spent points**. Under D25 the points-per-node varies with purchase order, so two
players holding an identical final build have different per-tree *point* shares depending on which
tree they filled first — and `H = Σ share²` reads exactly that vector. Total cost stays order-free
(§3.5); the *share* does not.

D18's own justification (`ideal:50`) is that a full reset *"dissolves the Grim Dawn order-sensitivity
problem entirely."* It dissolves it for **unlocks**. D25 reintroduces it for the **share vector**, and
the share vector is what the focus multiplier reads. Fix: compute `H` on node budget or node count,
never on points paid. That is also the reading that survives D11.

### 3.8 D25 × D21 — the bound stops at one actor

D25 is *"arithmetic, per actor"*. D21 (`ideal:53`) gives **every** actor its own tree state. So a
player's total build breadth across a 100-demon roster is bounded only by `ContractPolicy.NextSlotPrice`,
not by D25 — each new demon restarts the escalation at `c0`. Whether that is deliberate (a roster *is*
supposed to be broad) or an oversight is an owner call, but it should be stated, because D25's own
justification is *"the whole catalog unlocked at Θ≈1,450 and the tree stopped being a choice"* — and
at roster scale it still is.

### 3.9 D25 × D29's volume — the number inside D25 is already stale

D25 says the hole appears at *"Θ≈1,450"*. That figure is `~50 trees × ~29 nodes` (doc 07:132,159).
D29, decided the same day, makes the generic catalog **1,560** by its own arithmetic and 1,599 by
doc 02's G9. Trivial, but it is inside the decision text.

---

## 4. D30 vs D24 — the review problem is real, and the learnability problem is worse

### 4.1 The review tension is restated, not resolved

`06:302-305` states it exactly:

> *"D23 does not survive contact with 841 species in its stated form, and the reason is D24, not
> generation throughput. §10.2 item 1 now requires 'The generator's output is reviewed, then
> committed.' D24 makes review mandatory; D23 makes the review surface 24,389 unique nodes. **The two
> decisions are in direct tension and neither names the other.**"*

D30 (`ideal:58`) now names it — *"The generation is not the cost; the REVIEW is"* — and answers with
*"batching, sampling gates and a distribution check that a human can audit without reading 24,000
nodes."*

**That is an amendment to D24, presented as compatibility.** D24 §10.2 item 1 (`ideal:441-442`) reads:

> *"The generator's output is reviewed, then committed. Balance is checked once, **against the real
> corpus**, before players see it — rather than being an argument about expected values over rolls."*

A sampling gate *is* an argument about expected values over a population. It may well be the right
call at 24,389 nodes, but D24 as written does not permit it, and `06:315` already costed the
alternative that does: a shared pool keyed on the `(primary, element, status)` triple plus ~3 genuinely
unique nodes per species — **2,523 unique nodes, a tenth of the review cost**, keeping the *"nobody
else has this"* payoff D23 is buying.

### 4.2 The learnability problem — and this is the critical finding

D24 exists for **learnability** (`ideal:64`, owner: *"user cannot build because they need to
relearn"*). Doc 07's whole surface analysis rests on one sentence (`07:177-178`):

> *"D21 gives the commander and every demon its own tree state. **The catalog is shared** (ideal §7,
> owner decision 1: 'the catalog is shared; only allocation is per-actor')."*

and closes with (`07:190`):

> *"the learning cost is `1,450`, the management cost is `101 × 1,450`, and **only the second one
> scales with the roster**."*

**D30 makes that false.** A full 29-node unique tree per species means the catalog itself scales with
the roster. Computed:

| Roster | Generic nodes (D29) | Species nodes (D30) | **Nodes the player must learn** |
|---|---:|---:|---:|
| commander only | 1,560 | 0 | 1,560 |
| + 30 demons | 1,560 | 870 | **2,430** |
| + 100 demons | 1,560 | 2,900 | **4,460** |

Doc 07 already flagged 1,450 as the problem (`07:137-142`):

> *"Path of Exile's shared tree is roughly 1,300 nodes… **We are proposing more nodes than PoE**, and
> PoE's is *one* tree… Ours is fifty disjoint pictures with no shared geography. Fifty small maps are
> harder to learn than one big one."*

D30 takes that from 1,450 disjoint nodes to 2,430–4,460, and every new demon adds 29 more that no
guide, no comparison and no shared vocabulary covers — because by D23's definition they are *"nodes no
other tree has."* D24's own image (`ideal:425`) is:

> *"A tree is a **map the player navigates**. A map redrawn for every traveller is not a map."*

**D30 gives every species a different map.** It is not redrawn per *player*, so D24's letter survives —
but D24's stated purpose does not. **This is the most dangerous inconsistency in the set**, because
24,389 authored-and-reviewed nodes is the least reversible commitment the program has made, and the
property it defeats is the one D24 was created to protect.

### 4.3 D30 vs D17 and D32 — 841 more chances to skew

D17 (`ideal:49`) locks a species tree's build-favour triple; D32 (`:60`) sets the target distribution.
D32's gate covers the **favour axes** — 8.3% per aptitude, 16.7% per element. It does not reach node
*content*: 24,389 uniquely authored nodes are drawn from 7 attach points, 16 kinds, 13 triggers and 28
combat families (`02:§7.4`, all read from code), and a corpus-wide bias toward, say, `on-hit` triggers
or `combat.crit.*` families would pass D32's check untouched.

`§9`'s own thesis is that this exact failure already happened once: a free-text LLM-authored field read
later as though it were a closed vocabulary. D30 multiplies the surface by 16 and D32's gate is sized
for the old one.

### 4.4 D30's 29 vs D29's 40

D30 says *"FULL **29-node** unique tree"*. 29 is `2 × 14 + 1` — doc 02's T2 recommendation at
**seven** tiers (`02:§2.3`), the topology D29 rejected the same day. D10 (`ideal:42`) says:

> *"**Same shape everywhere**: every tree is 2 branches × tiers. One generator archetype, one set of
> math functions."*

Either species trees are 40 nodes (corpus `841 × 40 = 33,640`, **+38%** on a number already called the
largest commitment in the program), or D10 needs an explicit species exception. D23 (`:55`) gives
species trees *"its own generation pipeline"*, which is an argument for the exception — but a pipeline
is not a shape, and D10 is not amended.

---

## 5. D31 vs shipped code — the collisions, enumerated

### 5.1 The three D31 names

| D31's claim | Verdict | Evidence |
|---|---|---|
| *"`AptitudeAffixPrice.cs:32`'s `> 4` branch resolves itself once slot 5 exists"* | **Half right, and it was never the gate.** `VocabularyReady => AptitudeVocabularyLanded && Enum.GetValues<AllocationScope>().Length > 4`, and `AptitudeVocabularyLanded` is a `const bool … = false` (`:30`). The count term flips true at 5 members; the expression stays `false` until the item program flips its own const. **No live pricing path changes.** What *does* break is a test: `ItemPowerReadsTests.cs:206` asserts `Equal(4, Enum.GetValues<AllocationScope>().Length)` | `AptitudeAffixPrice.cs:30,32,39-49`; `ItemPowerReadsTests.cs:201-208` |
| *"`item-ideal.md:1443` books slot 5"* | **Confirmed, verbatim.** *"A 13th atom kind or `aptitude.*` channel family, and a **fifth `AllocationScope`**, for D8"* | `item-ideal.md:1443` |
| *"`AptitudeAllocation.Single` (`:38`) still rejects any id that is not one of the twelve aptitudes"* | **Confirmed, and D31's consequence is right.** `if (!AptitudeCatalog.IsAptitudeId(aptitudeId)) throw new ArgumentException(...)`. The `scope` parameter is never validated; only the id is. So a sixth scope can name an aptitude and nothing else | `AptitudeAllocation.cs:36-39` |

**The guard's real weakness, which going second does not fix.** `AptitudeAffixPrice`'s count check is
*"a redundant, self-updating guard: a fifth scope value landing without this flag being flipped is
caught by a test"* (`:22-28`). It checks a **count**, not the presence of an *item* scope. A
`status_mastery` scope satisfies `Length > 4` just as well as an item one. Whether status takes 5 or
6, the guard can no longer distinguish, and the canary at `ItemPowerReadsTests.cs:206` will already
have been rewritten by whoever went first.

### 5.2 Every other consumer, and what each does at six members

Swept across `src/`, `tools/`, `tests/`, `web/`, `data/`, `scripts/`.

| Consumer | Behaviour at 6 | Verdict |
|---|---|---|
| `AptitudeAllocation.cs:8` — `enum AllocationScope { Commander, DemonType, Aspect, UniqueDemon }` | implicit ordinals 0–3, no `[Flags]`, no zero-sentinel; doc comment `:3-7` says *"Append-only… never reorder"* | appending is sanctioned |
| **`AptitudeAllocation.cs:103`** — `static readonly AllocationScope[] AllScopes = Enum.GetValues<AllocationScope>();` driving `Total` (`:54`), `GrandTotal` (`:62-63`), `Share` (`:83-84`) | **self-sizing, so it silently sums a sixth scope into the aptitude share denominator** | ⛔ **live balance change, unnamed by D31** — see §5.3 |
| `AptitudeTuning.cs:199-205` — rate dict built from **four hardcoded** JSON keys, no `Enum.GetValues` loop | a sixth scope has no rate row | ⛔ see §5.4 |
| `PointBudget.cs:57` — `tuning.PointEconomy.AptitudePointsPerThetaMilliByScope[scope]` | throws bare **`KeyNotFoundException`**, not the loader's named `AptitudeTuningRejection` | ⛔ violates `tunables-ssot.md:91` T5 |
| `RpgStore.Aptitudes.cs:53-70` — `ScopeToText` / `ScopeFromText`, both `switch` with throwing `_ =>` arms | a sixth member throws on every save and load until both arms are added | ✅ fails loud, by design |
| `RpgStore.Aptitudes.cs:36-47` — schema `scope TEXT NOT NULL` | **stores the string name, never the ordinal** | ✅ see §5.5 |
| `AptitudeAllocation.cs:30,32,34,44,94` — `Dictionary<(AllocationScope, string), long>` | enum used as a composite dict key, default comparer, no ordinal arithmetic | ✅ safe |
| `AllocationStoreTests.cs:107-112` — round-trip test over a **hardcoded 4-element array**, not `Enum.GetValues` | a sixth member is silently never round-trip tested | ⚠️ blind spot |
| `SpeciesAllocationSeamTests.cs:31,36,53` — a **literal source-text scan** for `"LoadAllocation(AllocationScope.DemonType"` across `src/**/*.cs` | sensitive to spelling, not meaning | ⚠️ will need touching |
| Literal `AllocationScope.<Member>` call sites — `SpeciesAllocation.cs:35,62`, `ZombossCommanderAllocation.cs:49-50`, `ZombossPattern.cs:27,37`, `AptitudeEndpoints.cs` (12 sites), `AuraDerivedEndpoints.cs:59`, `WebMatchService.cs:304,389,492`, `RpgClient.cs:373`, `tools/{DominanceBaseline,ResidualFitLoop,ProveAptitude,HybridViability}` | unaffected | ✅ |
| `src/FusionRpg.Contracts` | **zero references** | ✅ no wire surface |
| `web/fusion-rpg-web` | **zero references** — no TypeScript mirror; `types.ts:449-457`'s `AptitudesState` carries no scope field at all | ✅ |
| `scripts/`, `data/` | zero by enum name; only camelCase **string** keys in `aptitudes.v*.json` | ✅ |
| Switches with no `default`: **none**. `(int)scope` casts: **none**. `Enum.Parse`: **none**. Array-indexed-by-ordinal: **none** | — | ✅ |

### 5.3 The one that matters — a sixth scope dilutes every aptitude share

**FACT.** `AptitudeAllocation.cs:103` reads the enum with `Enum.GetValues`, and `Total` / `GrandTotal`
/ `Share` iterate it. **FACT.** `decisions.md:103` locks the semantics:

> *"An actor's allocation is the **sum of four scopes** (commander → demon type → aspect → unique
> demon)… `share` is taken **on the sum**, never per scope."*

**INFERENCE.** If a `status_mastery` scope ever holds points that `Single` accepts — which is exactly
what D31's *"second, separate change"* would enable — those points enter `GrandTotal` and shrink every
aptitude's `share`. `share` is the input to **both** PS-3 read functions (contests and magnitudes,
`decisions.md:103`), so this is a live combat-math change, not a plumbing one. It would also move the
class-system goldens.

D31 does not name it. It is the single most consequential thing about widening this enum, and it argues
for the red team's F3 fix (`06:§1`, *"Give status trees their own mastery counter, the way
`element_mastery` is specced"*) more strongly than any of the three collisions D31 does name.

### 5.4 A missing rate row fails the wrong way

D19 priced the change as *"the shipped four-row per-scope rate table grows to five"*; under D31 it
grows to six. **FACT:** the loader hardcodes four keys (`AptitudeTuning.cs:199-205`) and
`PointBudget.cs:57` indexes the dict directly, so a scope with no row throws `KeyNotFoundException` —
not the loader's own named rejection. `tunables-ssot.md:91` T5: *"A missing tunable is a load rejection
naming it. Never a built-in default."* A bare `KeyNotFoundException` names nothing.

### 5.5 Nothing reads the ordinal — so "slot 6" has no technical content

**FACT.** The DB stores `scope` as `TEXT` (`RpgStore.Aptitudes.cs:36-47`); both conversions are
hand-rolled string switches (`:53-70`); there are zero `(int)scope` casts repo-wide; no DTO in
`FusionRpg.Contracts` carries the enum; `web/` has no mirror.

**Therefore D31's ordering — *"takes slot 6, after the item program takes 5"* — has no serialization,
DB or wire consequence.** The only thing that reads the member *count* is `AptitudeAffixPrice.cs:32`
and its canary test, and as §5.1 shows, the count check cannot tell whose scope arrived. §11.4's
*"D31 needs the item program's fifth `AllocationScope`"* and D31's *"cannot be built until the item
program lands its fifth scope"* are therefore **an assumed constraint** — DESIGN-GATE evidence rule 4.
Keeping the ordering as a coordination convention is fine; recording it as a blocker costs the owner a
dependency they do not have.

**One forward-looking note, INFERENCE.** No `JsonStringEnumConverter` is registered anywhere in
`src/FusionRpg.Server`, so if `AllocationScope` ever reached a response body it would serialize as the
int ordinal — the repo's first ordinal-on-the-wire dependency. Not a problem today; worth a line in
whichever spec first puts a scope on the wire.

---

## 6. D26 at ten tiers — flatness holds exactly, and D25 is the only thing that can break it

### 6.1 The flatness claim at t = 8, 9, 10 — computed

```text
W(t)   = b · t(t+1)/2          (D20's surviving pairing rule: power linear per tier)
req(t) = 5 · t(t+1)/2          (D26)
W(t)/req(t) = b/5              — the t(t+1)/2 factor cancels identically, at EVERY t
```

| t | 8 | 9 | 10 |
|---|---|---|---|
| `req(t)` | 180 | 225 | **275** |
| `W(t)/b` | 36 | 45 | 55 |
| **`W/req` (×b)** | **0.200** | **0.200** | **0.200** |

D29's printed sequence `5 · 15 · 30 · 50 · 75 · 105 · 140 · 180 · 225 · 275` reproduces exactly.
**CONFIRMED: D26's property is structural, not numeric — extending the ladder cannot break it,
because the shared index cancels before any value is substituted.** This is the strongest thing in the
decision set and D29 does not touch it.

### 6.2 Does D25 break it? Reward-per-EFFORT is a different question, and the answer is "not stated"

**No, not literally** — and the reason is a distinction `02:§3.1` draws explicitly and D25 does not
mention:

> *"**Two different quantities, and conflating them is the easy mistake.** `req(t)` gates on *aptitude
> points allocated to that tree's gate quantity*; nodes are bought with *skill points*."*

D26's `b/5` is reward per **aptitude point**. D25 escalates **skill points**. Different currency,
different axis — so `W/req` is untouched and D26 stands.

**But reward per *effort* is neither of those, and it is not flat.** A player earns both currencies
from one `Θ`: 3 aptitude points and 1 skill point per `Θ`
(`data/tuning/aptitudes.v5.json:15-17,22-27`, FACT via `02:§3.1`). So the true price of tier `t` is
`5t` aptitude points **plus** the escalating skill-point cost of that tier's nodes, and only the first
half is flat.

Working, with `w` nodes per tier per branch and cost `c(k) = c0 + (k−1)d`:

```text
nodes owned before tier t      =  2w(t−1)
skill cost of tier t's nodes   =  2w·c0 + d·(2w)·(2w·t − w − ½)      ← LINEAR in t, not flat
reward at tier t               =  b·t                                 ← linear in t

⇒ reward per skill point is flat  ⟺  2w·c0 = d·w(2w+1)/2  ⟺  c0 = d(2w+1)/4
```

Three consequences, all unstated:

1. **D25 has a tier-1 entry tax of its own** unless `c0` and `d` satisfy that relation — the *same
   shape* of defect D26 was adopted to remove from the gate axis, on the other axis, two decisions
   later.
2. **The relation depends on `w`**, so it is archetype-dependent — and `02:§4.2`'s equal-value proof
   is built precisely on `w` never appearing. D25 puts `w` back into the effort accounting through a
   door the proof does not cover. Two trees with identical budgets no longer cost the same effort to
   *partially* build, and nobody finishes a 40-node tree at any realistic `Θ`.
3. **Node value per skill point differs ~3× across shipped archetypes** (`02:§4.3`: `gated-deep`'s
   capstone is `.2500 B_b`, `late-crown`'s best node `.0893`). Under a flat 1-point unlock that is a
   *shape* difference; under D25's per-node price it becomes a *value* difference in the same units
   the escalation is charged in. `gated-deep` becomes strictly the better buy.

**So: D26's stated property survives D25 intact, and the property the owner actually asked for —
*"every skill tree will cost and award same"* — does not, without a stated relation between `c0`, `d`
and `w`.** That relation is one line of tuning and one line of spec. It is the cheapest thing on this
list to close and the most expensive to discover after 25,900 nodes are authored.

---

## 7. D27 vs D29 vs D30 — the real corpus, and what is now stale

### 7.1 The arithmetic

**Roster.** D27 (`ideal:63`) ships *"12 primary + 6 elemental + 21 status + demon family + species."*
Closed rosters give `n = 39`; `F` (families) is undecided — `02:§1.2` gives `F ≈ 19` as the
recommendation, `525` as the literal reading, `0` as today's value.

**Nodes per tree.** D29 says 40. `02:§2.2` G9 says `nodesPerTree = 2 × nodesPerBranch + 1`, which is
**always odd**. Ten tiers at width 2 is 20 per branch → **41**, or 40 if the shared root is dropped.
D29 does not say which, and G9 is a machine-checkable emit-time invariant, so this must be decided
before the planner can refuse anything.

| Corpus | With root (G9, 41) | Without root (D29, 40) |
|---|---:|---:|
| Generic, `n = 39` (**what D29 counts**) | 1,599 | **1,560** |
| Generic, `n = 58` (`F = 19`, what D27 ships) | 2,378 | 2,320 |
| Species, `841 × 29` (D30) | 24,389 | 24,389 |
| Species at D29's own 40 (D10 consistency) | 34,481 | 33,640 |
| **Total as §11.3 states it** | — | **25,949** ("≈25,900" ✅) |
| **Total under D27's actual roster** | 26,767 | 26,709 |
| **Total if D10 is honoured for species** | 36,860 | 35,960 |

**Two errors in one figure.** `39 × 40 = 1,560` (a) drops the shared root and (b) sets `F = 0`,
excluding the demon-family trees D27 explicitly ships. §11.3's *"≈25,900"* inherits both.

### 7.2 Stale volume figures, listed so the propagation is one pass

Every one descends from `~50 trees × ~29 nodes = 1,450`.

| Location | Figure | Now |
|---|---|---|
| `ideal:53` (D21) | *"~50 trees × ~29 skills is ~1,450 possible per-skill soul levels per actor"* | 1,560–2,320 generic + 29 species |
| `ideal:61` (D25) | *"the whole catalog unlocked at `Θ≈1,450`"* | ≥1,560 |
| `ideal:328` (§7 item 1) | *"~50 trees at that density is ~1,450 generated nodes"* | closed by D29 |
| `01:188, :366, :432, :441` | 1,450 / `~50 × ~29` | as above |
| `02:§0, §2.3` | 1,131 / 1,682 / 29 nodes / T1–T3 | superseded by D29 |
| `03:393, :514` | *"~1,450 nodes"*, 2% exclusion target | ≥1,560 |
| `03:703-705, :716-718` | batching table — *"one node ~1,450"*, 1,450 base + 2,900 vote calls | ≥1,560 + 3,120; **and D30 adds 24,389 more single-node calls**, which `03:1000` already costed |
| `06:289-295, :473` | 1,131 / 21,402 / 24,389 / ≈45,800 | the 24,389 figure is still exactly right |
| `07:23, :65, :75, :129, :132, :157-159, :170, :181-182, :190` | the whole learnability sizing at 29 nodes / 1,450 total / 7 tiers | see §4.2 |

`07:157-170`'s table (*"at `Θ ≈ 1,450` every node in every tree is unlocked"*) is **superseded by
D25** rather than by D29 — which is the correct outcome, since it is the finding D25 was written to
close.

`07:65`'s *"2 branches × 7 tiers, ~29 nodes, rendered as a fixed lattice — 29 cells, render-all"*
survives D29 in substance: 40 or 41 cells is still render-all. The conclusion holds; the number moves.

---

## 8. D32 — near-uniform is achievable, and the premise that it is not is refuted

### 8.1 D17 does not lock the tree favour to the seed's existing triple

The brief asked whether near-uniform is achievable *given* D17 locks a species tree's favour to its
seed's triple, against a corpus measured at 166:1.

**D17 does not do that.** `ideal:49`, read in full:

> *"Demon species trees lock a build-favour triple… **Extending that favour into the seeds** is a
> **deterministic planner → agent-inspects-seed → validated-against-target** pipeline, never an LLM
> free choice."*

The favour is **written by the planner**, not read from the seed. §9 spells out the mechanism
(`ideal:395-397`):

> *"**Quota assignment before generation** — the planner decides how many species may carry each
> (primary, element, status) favour; the agent only chooses which of the **permitted** favours fits a
> given species thematically."*

and its corollary (`:404-406`) settles it outright:

> *"a species' **thematic** favour and its **mechanical** lock need not be the same field. If they are
> one field, thematic truth (plants are earthy) becomes mechanical skew (everyone plays earth)."*

**So yes — the tree layer can be uniform while the corpus it reads is not.** The decoupling is already
designed; D32 is the target it validates against. **REFUTED.**

### 8.2 What it actually costs, quantified — and this is the part nobody has said

D32's allowance is scoped to elements: *"`earth` may run to roughly **1.5×** uniform because plants
really are earthy."* Against `§9`'s measured corpus (`ideal:368-385`):

| Axis | Uniform | D32 allows | Measured | Species that must move |
|---|---:|---:|---:|---:|
| element `earth` | 16.7% (140) | 1.5× = 25% (210) | **45.1% (379)** | **169** |
| aptitude `Onslaught` | 8.3% (70) | no allowance named | **39.5% (332)** | **~250** |
| aptitude `Ferocity` | 8.3% (70) | no allowance named | **0.2% (2)** | **+68** |

**D32 is not a ratification of the corpus. It is a mandate to re-assign roughly 500 species' tree
favour away from their own seed's `aptitudePrimary` / `elementPrimary`.** That is exactly what the §9
corollary sanctions, and it is the right call — but it should be written down, because the cost is
thematic coherence at precisely the axes the corpus is thinnest on, and someone reviewing 841 species
trees will otherwise read it as a generation bug.

Also worth naming: **D32's named allowance is set below the measured value it names.** Earth is 2.7×
uniform today; the allowance is 1.5×. If the intent was to sanction the existing skew, the number is
wrong. If the intent was to halve it, say so.

### 8.3 One numeric slip

`ideal:49` says the corpus is skewed *"165:1"*; `:384` measures `Onslaught 332 : Ferocity 2`, which is
**166:1**. One character.

---

## 9. Checked and could not break

Named because a short list of real findings is only credible next to what survived.

| What I attacked | Verdict |
|---|---|
| **Doc 09's conclusion under D26** | **Survives.** The D26 ladder was already in the swept build (`Program.cs:311`) and doc 09 reports it in four of its eight rows |
| **Doc 09's conclusion under D29** | **Survives, and provably.** No credit rule can push a gate above the 300-point budget, and `T_D26(300) = 10` exactly (`req(10)=275 ≤ 300 < 330`). The new `D26@10` rows must reproduce the `D26` rows byte-for-byte |
| **"Doc 09 ran on 7 tiers"** | **False.** `TierD20`/`TierD26` are unbounded loops; the cap was added today |
| **`tierBudget[t] = B_b·t/T_tri` sums to `B_b`** | **T-independent.** `Σ_{t=1..T} t ≡ T_tri`, so the cancellation is an identity in `T`. D15/R6 survives D29 untouched |
| **The per-mille rounding residual at 10 tiers** | **Still 0** — computed: 18+36+55+73+91+109+127+145+164+182 = 1000. §7.3's warning stands as a rule but does not bite here |
| **D26's flatness at deep tiers** | **Exact at t = 8, 9, 10** and at every `t` — the `t(t+1)/2` factor cancels before substitution |
| **D25 vs D18's refund** | **Consistent, and D18 protects D25.** `Σ(c0+(k−1)d)` is order-free, and the full-reset-only rule forecloses the sell-high/buy-low arbitrage. Only the refund *basis* needs stating |
| **D25 has no precedent** | **False — two.** `ContractPolicy.NextSlotPrice` (`:176-177`) with its own register entry at `ssot-power-scale.md:815`, and `StarPolicy.SacrificesForStar` (`:76-82`) |
| **D31's `AptitudeAllocation.Single` claim** | **Exactly right.** `AptitudeCatalog.IsAptitudeId` throws on anything but the twelve; `scope` is never validated |
| **D31's `item-ideal.md:1443` citation** | **Verbatim accurate** |
| **D27 vs §3.1's dropped `1/n`** | **Consistent.** `H = Σ share²` carries no `n` (`ideal:91-94`), so appending trees never rescales an existing build. D27's *"categories can land in any order"* follows directly |
| **D17 blocks D32** | **Refuted** — see §8.1 |
| **`AllocationScope` ordinal on a wire or in the DB** | **None today.** TEXT column, string switches, zero casts, zero DTOs, zero web mirror |
| **DESIGN-GATE §2 invariants** | Nothing in D25–D32 contradicts one. Standalone-first (§2.9) holds — no decision in the set names a Unity field or a lawn read. PS-3/one-ladder (§2.14) holds for `req` and `W` on the `02:§3.5` shares rule. **The one exception is PS-8/§2.11, and it is §3.3's finding, stated explicitly rather than worked around** |

**Two pre-existing findings that D25–D32 did NOT close, listed so they are not assumed fixed:**

- **D18 still contradicts `decisions.md:103`.** The lock reads *"respec is available, unlimited, and
  priced in **a resource fighting also costs**"*; D18 prices it in souls, which `decisions.md:97`
  describes as a fighting **faucet**. `RespecPolicy.cs:35` ships `RespecResource.Hunger` with the
  reasoning written out at `:11-18`. Red team F7 remains open.
- **D31 overrides red-team F3 rather than resolving it.** F3's fix was *"Do not extend
  `AllocationScope`. Give status trees their own mastery counter, the way `element_mastery` is
  specced"* (`06:§1`). D31 extends it. That is a legitimate owner override, but §11.2's row for F3
  should say *overridden by D31*, not imply it was answered.

---

## Design-gate checklist

```
[x] I identified the subsystem(s): passive trees, class system, power ladder / caps register,
    tunables, effect atoms, demon seeds, item program.
[x] I read every doc in the §1 row(s) this session: DESIGN-GATE.md (in full), decisions.md
    (rows 94/97/103/107/108), passive-tree-ideal.md (in full), research/passive-tree/02 (in full),
    06 (§1, §7, §8), 07 (§1.2-§1.4), 08 (§1, §2 M1-M2), 09 (in full),
    ssot-power-scale.md §10 (rows 16/25/27/28), §10.5, §11 head, §11.1, §11.1a, §11.10, §11.10a,
    tunables-ssot.md T5, item-ideal.md §"Needs another program".
[x] I checked decisions.md for a lock covering this — row 103 (class system) is the live lock and
    §5.3 names where D31 touches it.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments: the AllocationScope sweep is a full
    src/tools/tests/web/data/scripts enumeration; RespecPolicy, ContractPolicy, StarPolicy,
    SoulSinkPolicy, AptitudeAffixPrice, AptitudeAllocation, PointBudget, RpgStore.Aptitudes and
    HybridViability/Program.cs were opened and read, not grepped for.
[x] I read the surrounding section of every rule I quoted — specifically PS-8's own elaboration
    (ssot:778-787) before calling D25 a cap, decisions.md:103 in full before §5.3, and D24 §10.2's
    whole item 1 before §4.1.
[x] I tested (not assumed) the constraints I report. COMPUTED, not quoted: T_D26(300)=10 and
    T_D20(300)=11; W/req at t=8,9,10; the tierBudgetMilli sum at 10 tiers; maxNodeShare at 10 tiers;
    the corner share 0.54163 from Program.cs:79-80,86; every corpus figure in §7.1; the D32
    re-assignment counts in §8.2. The D25 effort algebra in §3.4/§6.2 is marked INFERENCE.
[x] Nothing contradicts a §2 invariant, EXCEPT PS-8, and §3.3 names that contradiction explicitly
    rather than working around it.
[ ] Corrections are propagated — NOT DONE, and deliberately: this is a read-only audit. The
    propagation list is §7.2 (volume figures), §2.3 (doc 02's T-dependent block), §5 (D31's
    consequence list), and the ideal's own :49 / :53 / :61 / :328.
```

**I did not re-run any sweep.** The owner is re-running `--crossunlock` separately; §1.3 is a
prediction about what that run must produce, stated so it can be falsified.
