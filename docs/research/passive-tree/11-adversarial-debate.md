# Passive trees — adversarial debate (2026-09-05)

**Status:** research, not a spec, no build authorized. This is a **steelman exercise**: build the
strongest case against [passive-tree-ideal.md](../../architecture/passive-tree-ideal.md) (D1–D32) and
the enrichment set ([01](01-static-vs-rolled.md) … [09](09-crossunlock-sweep.md)), then rebut each
attack from the design's own evidence and deliver a verdict.

**The concessions in §7 are the most valuable part of this file.** They say which foundations are safe
to build on. A weak objection nobody believes is worse than no objection, so nothing is argued here
that I would not defend.

Claims are marked **FACT** (read in `src/`, `data/` or `tools/` this session with `file:line`),
**INFERENCE** (derived from a cited fact, not measured) or **RECALL** (not verified in-repo).
Code beats docs; docs beat comments.

---

## 0. Verdicts

| # | Thesis | Verdict | One line |
|---|---|---|---|
| 1 | 25,900 authored nodes is a content graveyard | **DESIGN HOLDS on D29; GENUINELY UNRESOLVED on D30** | 1,560 generic nodes is defensible content. My review-cost attack on D30 was answered the same day by [13](13-review-pipeline.md) — review scales with *trees*, not nodes — but the 6× **generation** lever (~46 extra machine-hours) survives, and D30's affordability is conditional on a tree-card artifact that does not exist |
| 2 | Volume defeats D24's learnability goal | **DESIGN HOLDS** | Learnable means *predictable and plannable*, not *memorizable* — and the payoff (a build code that means the same thing to two players) is a property of determinism, not of size. Conditional on [07](07-learnability-and-surface.md)'s IA actually being built, which is unpriced |
| 3 | The mechanism quota cannot be authored at scale | **THESIS HOLDS, in a refined form** | Not "there are too few mechanisms to fill the quota" — the quota is trivially fillable. The defect is that it is checked on a *structural* definition while the value it exists to deliver is *behavioural*, so R-M1 can pass at every tier with nodes §3.5 already measured as worthless |
| 4 | The concentration fix belongs in the resolver, not in `F` | **DESIGN HOLDS on the resolver; THESIS HOLDS on `F`** | Multiplicative layers are load-bearing (scale invariance for the quadratic ladder) and the two shipped alternatives are both *worse* under PS-8. But `F` itself is machinery the program's own sweep found does nothing, kept alongside D28, which the sweep found does |
| 5 | Free build + 39 trees is a checklist, not a build system | **DESIGN HOLDS, conditionally** | D25 + D29's tier-10 depth + the unbounded soul track answer it — but D25 today is a decision with **no shape and no number**, and until it has one, §1.3's "everything unlocks at Θ≈1,450" is still the live arithmetic |
| 6a | **(novel)** Every balance number in this program is a 1v1 duel; the game is fielded 6-vs-wave | **THESIS HOLDS** | `BuildSquad`'s `maxSquad = 6`. `H`, `F` and the whole focus/spread finding are measured at a scope one level below where power is actually delivered — the same defect class as F4, one level up |
| 6b | **(novel)** D15 conserves a budget in a unit the program has already measured is not value | **THESIS HOLDS** | Equal `PowerVector.Total` per tree, against corners measured from 0.3% to 97.9% win share. Equal budget across unequal aptitudes *locks in* the imbalance the tree layer exists to fix |

---

## 1. "25,900 authored nodes is not shippable content, it is a content graveyard"

### The attack

D29 gives 39 trees × 40 nodes = **1,560 generic nodes**. D30 gives **~24,389 species nodes** across 841
species. Total **≈25,900** — the ideal states the figure itself (`passive-tree-ideal.md:518`).

Path of Exile's tree is roughly 1,300 nodes (RECALL, prior art §2.5) and is hand-authored over a decade
by a team whose whole job it is. This design proposes **twenty times** that, generated, and then commits
under D24 to *reviewing* it before it ships (`passive-tree-ideal.md:441`).

Volume is the defect, not the review process. No sampling gate makes 24,000 generated nodes good; it
makes them *statistically unobjectionable*, which is a different property. The red team already priced
the honest number: at 30 seconds of human review per node, 45,800 nodes is **~380 hours**
([06-red-team.md](06-red-team.md) §7), and D30's 24,389 alone is ~200 hours of reading nodes nobody
asked for. Meanwhile a player at Θ=100 holds 100 skill points (`aptitudes.v5.json:16`,
`skillPointsPerTheta: 1` — FACT) and under D25 will unlock materially fewer than 100 nodes. That is
**under 0.4% of the corpus**. The other 99.6% is cost with no reader.

The sharpest form: **the research told the owner the cheaper structure and the owner picked against it
without answering the argument.** [03-llm-stage-contract.md](03-llm-stage-contract.md) §8.2 costs both
options explicitly — 841 × 4 unique nodes = 5,046 calls versus 841 × 29 = 24,389 — and names it *"the
single biggest cost lever in this program."* [06](06-red-team.md) §7 independently proposes the same
restructure (shared pool + ~3 unique nodes = 2,523 unique nodes, *"a tenth of the review cost"*) and
observes that **D23 and D24 are in direct tension and neither names the other.** D30 chose 29, and its
consequence column concedes the point — *"the generation is not the cost; the REVIEW is"* — then does
not say how the review gets done.

### The rebuttal

**What the player actually sees is not 25,900.** This is the strongest counter and it is decisive for
the generic half.

- A species tree is gated on the `UniqueDemon` scope — specimen level (`passive-tree-ideal.md:277`, and
  `rpg_unique_actors.level` is the only shipped source, [08](08-effort-power-reconciliation.md) §3).
  **You cannot see a species tree for a species you do not own.** A player with 30 demons has
  1,560 + 30 × 29 = **2,430 nodes in reach**, not 25,900.
- The browsing unit is the *tree*, never the node ([07](07-learnability-and-surface.md) §4.2, derived
  from the shipped volume rule at `CreaturesLayer.tsx:18-24`). One tree is 40 cells, render-all. Nodes
  flattened across trees are **never rendered**. So "25,900" is a corpus statistic, not a UI surface.
- Under PS-8 the deepest tiers *must* exist above the current ceiling. D29's own arithmetic puts tier 10
  at `Θ≈170` (`passive-tree-ideal.md:64`). Content the player has not reached yet is not a graveyard; it
  is the endless-grind SSOT working as designed.

**And the generic 1,560 is not a large number for this repo.** The demon program already ships 841
species entries across 503 files and 830 committed generated files
([01-static-vs-rolled.md](01-static-vs-rolled.md) §1 — counted, FACT). 1,560 nodes is smaller than work
already delivered and reviewed here.

**But the rebuttal does not reach D30.** Every argument above is about *reach* and *browsing*, and none
of them reduces the **review** obligation D24 creates. D24 says the catalog is reviewed before it ships —
for every player, including the 811 species this player will never own. The 2,430-nodes-in-reach argument
is a statement about one player; the review bill is over the corpus.

### Verdict — **DESIGN HOLDS on D29; GENUINELY UNRESOLVED on D30**

D29's 1,560 generic nodes survives cleanly: gated by depth, browsed one tree at a time, smaller than
content this repo has already shipped.

**On D30 I have to concede the load-bearing half of my own attack.**
[13-review-pipeline.md](13-review-pipeline.md) landed while this debate was being written and answers it
directly: **review cost scales with TREES, not with nodes.** A 29-node tree card and a 4-node tree card
take about the same time to judge, so 03 §8.2's *"4-vs-29 is the single biggest cost lever"* is true of
**generation** and false of **review** — a full census of all 841 species trees is ~21 hours at 90 s per
card, not the ~200 I charged. That kills the "200 hours of review nobody asked for" line, and it should
be recorded as killed.

What survives is narrower and worth stating precisely:

1. **The generation lever is real and unchanged.** 13 prices D30's run at **73,167 model calls ≈ 50–63
   hours** of machine time against 03 §8.2's 5,046 calls (~4.3 h) for the 4-node structure. That is
   ~46 extra machine-hours and 2–3 days of wall clock per regeneration, and 13 budgets **two to three
   passes**, not one. So the 6× lever moved from the human column to the machine column; it did not
   disappear.
2. **D30's affordability is conditional on an artifact that does not exist.** 13's own verdict:
   *"yes … but only if the tree card is built before the run, not after. Reviewing this corpus without it
   is 216 hours and will not happen, which means D24 would silently become 'trusted, not reviewed.'"*
   That precondition is not in D30, not in D24, and not in §11.4's open list.
3. **The identity argument for 29 over 4 is still unstated.** 13 shows 29 is *affordable*; it does not
   show it is *better*. **3–5 nodes nobody else has is already "unobtainable elsewhere"**, which is D23's
   actual promise ([06](06-red-team.md) §7).

Unresolved rather than decided: the cost objection is answered, the benefit case for 29 has still not
been made, and the precondition that makes the answer true is unbuilt.

---

## 2. "A static catalog and generated content are in tension, and D24 lost"

### The attack

D24's stated purpose is the owner's, quoted verbatim: *"it need solid stats, so user can learn it, if it
random every new player create, it will cause confuse, user cannot build because they need to relearn"*
(`passive-tree-ideal.md:412`).

Determinism answers *rerolling*. It does not answer *volume*. A 25,900-node corpus that never changes is
still unlearnable — it is simply unlearnable in a stable way. D24 buys the property that your knowledge
does not expire; it does not buy the property that the knowledge is acquirable.
[07](07-learnability-and-surface.md) §1.2 says this in the design's own voice: *"Fifty small maps are
harder to learn than one big one"*, and PoE's advantage is that its 1,300 nodes are **one spatial map**
where "I am here and I am going there" is a memory of a picture. Fifty — now 880 — disjoint lattices have
no shared geography at all.

So D24's learnability goal is defeated by D29 + D30 regardless of determinism. The design won the
argument it was having and lost the one that mattered.

### The rebuttal

**"Learnable" is being read as "memorizable", and the design never claimed that.** Read the owner
quote's own consequence: *"user cannot build because they need to relearn."* The failure being named is
**invalidated knowledge**, not incomplete knowledge. A player who knows one tree deeply and nothing else
can still build; a player whose tree was re-rolled last patch cannot.

Three mechanisms deliver that weaker, correct reading, and each is cited:

1. **L2 — "a node does not move."** A rebalance changes a magnitude in `data/tuning/`, never the shape
   ([07](07-learnability-and-surface.md), L2, against the shipped pattern `AptitudeTuning.cs:156`). This
   is the load-bearing half of learnability and it is a **new rule the document asks for**, not an
   assumption.
2. **Scarcity does the filtering.** At Θ=100 the player can reach a fraction of a percent of the corpus,
   so *"the surface's job is not to show it all — it is to help the player find the seven percent that is
   theirs"* ([07](07-learnability-and-surface.md) §1.3). Level 0 of the IA opens on the 1–4 trees you
   have actually invested in.
3. **The payoff is real and it is exactly what volume does not touch.** §5.3: a build serializes to
   `<catalog version> + [(treeId, nodeId, points, soulLevel)…]`, and **a code from another player
   resolves against your catalog and means the same thing.** That property holds at 1,560 nodes and at
   25,900. It is unavailable to any rolled catalog at any size. This is D24's real return.

**The PoE comparison also cuts the other way.** PoE's one-map advantage is a *browsing* advantage; its
learning cost is famously enormous. Fifty disjoint 40-node lattices, each internally simple and browsed
one at a time, is arguably the *easier* onboarding — the player learns one tree, not a continent. That is
INFERENCE, but so is the doc's own opposite claim, and it is labelled as such at
[07](07-learnability-and-surface.md) §1.2.

### Verdict — **DESIGN HOLDS**

D24's goal is stability of knowledge, and volume does not touch stability. The design has the right
target and, in L1–L7, the right mechanisms.

**One honest qualification, and it is not a hedge.** The whole rebuttal rests on
[07](07-learnability-and-surface.md)'s information architecture — level 0 defaults, tree-as-browsing-unit,
the plan object, the focus readout. **None of it exists.** [06](06-red-team.md) §11 lists *"a tree UI —
the whole learnability half"* as unpriced work, and `PassivesTab.tsx:12-20` today renders four
`LockedGridSlot`s. D24 §10.2 item 3 makes learnability an **acceptance criterion**. So the verdict is:
the design holds, and the thing it holds by is the largest unbudgeted deliverable in the program.

---

## 3. "The mechanism quota cannot be authored at scale"

This is the strongest attack available and it deserves the most care.

### The attack

§3.5 is the program's own load-bearing measurement: swept `b ∈ {0,2,5,10,20} × Fmax ∈ {1.0,1.25,1.5}`,
*"not one cell reverses the ordering"*, concluding **"a focus build cannot be rescued with MAGNITUDE. It
can only be rescued with MECHANISM"** (`passive-tree-ideal.md:207`). Everything downstream is built on
that sentence.

[02-deterministic-planner.md](02-deterministic-planner.md) §5.2 then makes it structural:

```text
mechCapMilli = 1000     (the deepest tier is 100% mechanism — §3.5 made structural)
R-M1 — the deepest tier is 100% mechanism.  Lowering it needs an owner decision.
R-M2 — mechShareMilli is monotone non-decreasing.
```

Now count the supply. [05-mechanism-taxonomy.md](05-mechanism-taxonomy.md) finds **ten** mechanism
classes. Of those:

- **M2** (conversion) has no passive attach point — *"no atom kind writes `packet.ElementPayload`"* — and
  D16 is a **genuinely new capability**, not a wiring gap (05 §M2; ideal `:497`).
- **M4** (resource trade) is inert — *"only `hp` executes today"* (`AtomKindRegistry.cs:541`), blocked
  behind the unbuilt action-cost layer.
- **M8** (cost structure) is inert — `EffectivenessMultiplier` has **no caller**
  (`OverlayCombatCalculator.cs:14-22`), same blocker.
- **M5 / M10** (denial, bypass) are **genuinely new capability**: every shipped "breaks their X" channel
  is a saturating contest that provably never reaches zero (05 §M5's five-row evidence table).
- **M9** (timing) is invisible to every measurement the program owns (`Predictor.cs:161-171`).
- **M6** (stacking) *"fails the acceptance test on its own."*

That leaves **M1, M3, M7 and the two §4 constructions (layer parity, Erosion)** — and M1 is itself two
wiring gaps (`AtomKindRegistry.cs:535`; `BattleRunState.cs:124-127`). Call it **three usable classes
today, five after named wiring.**

Against that supply, R-M1 demands the deepest tier of **every** tree be 100% mechanism. At D29's shape
that is 39 generic trees × 10 tiers, plus 841 species trees. Taking only the deepest tier and one node
per branch, that is ~78 generic capstones and ~1,682 species capstones drawn from five classes. R-M2's
monotone ramp adds hundreds more. **The deep tiers will therefore be magnitude nodes wearing mechanism
costumes** — and §3.5 measured a magnitude node as worth ≈0 to the build it is supposed to rescue
(`b = 20` moved the corner's win rate by **−0.7 points**, the wrong way).

### The rebuttal

**The supply count is wrong, and the planner's definition of "mechanism" says why.**
[02](02-deterministic-planner.md) §5.1 does not classify by taxonomy class. It derives the class from the
shipped cost function:

```text
MAGNITUDE  ≔  every bound atom has AttachPoint.Stat AND kind ∈ {stat.modify, stat.derived}
              AND conditionality == 1  (no trigger, no predicate)
MECHANISM  ≔  anything else — a non-Stat attach point, OR a trigger, OR a predicate.
```

The combinatorial space behind that is large and closed by construction: **7 attach points, 16 kinds,
13 triggers** (`AtomKindRegistry.AttachPointCount` / `.KindCount` / `.TriggerCount` — FACT, read this
session at `:21`, `:31`, `:36`), against 261 registered derived channels, the predicate leaf list
(`PredicateNode.cs:17-31`) and `AtomRunner`'s `icd` / `charges` / `everyHits` / `capPerMatch` shaping
(`AtomRunner.cs:33-39`). Ten *classes* is a design taxonomy; the *node* space is the product, and it is
nowhere near exhausted at 1,760 capstones.

**And "distinct" is the wrong bar for a species tree anyway.** [03](03-llm-stage-contract.md) §8.1 makes
this explicit and it is the right call: a generic tree needs **differentiation** (50 trees must be
tellable apart); a species tree needs **recognition** — *"it does not need to be distinguishable from 903
others; it needs to feel like that demon."* Repetition across 841 species is not a defect there.

**Also: the quota is enforced by refusal, not by hope.** The emit gate re-derives `class` from the bound
atoms and *"refuses on disagreement. A declared class that the content contradicts is a rejection, not a
warning."* The corpus cannot silently drift to magnitude.

### Where the rebuttal fails, and this is the real finding

The rebuttal defeats the attack **as I stated it** and does not touch the attack **as it should be
stated**. Restated:

> The quota is checked on a **structural** definition. The value it exists to buy is **behavioural**. A
> node can satisfy `class = mechanism` at every tier of every tree — pass the emit gate, pass R-M1, pass
> R-M2 — and still be worth ≈0 to a focused build, because "has a trigger" is not the acceptance test.
> §3.5's test is *"helps a focused build specifically"*, and [05](05-mechanism-taxonomy.md)'s own summary
> table answers **"Neutral"** or **"Weak"** for M2, M4, M6, M8 and M9 — five of ten classes that all
> satisfy the structural definition.

The two documents are inconsistent with each other and neither notices. [02](02-deterministic-planner.md)
§5.1 says the split *"falls out of the shipped cost function"* and warns *"do not add a hand-set `class`
flag the LLM can lie about"* — correct, and it stops one failure mode. But
[05](05-mechanism-taxonomy.md) ranks mechanisms by *focus value* precisely because the structural property
does not imply it. A `status.apply` on `OnDamageTaken` granting `+X combat.power.omni` is structurally a
mechanism, prices lower for its conditionality (`CostFunction.cs:37+`), passes the emit gate — and is a
magnitude node with a trigger stapled to it. It is exactly the costume the attack predicted.

**And the harness cannot catch it.** F2 is a type-level fact, not a coverage gap:
`DominanceGuard.Measure(IReadOnlyList<AptitudeAllocation> builds, long theta)` — verified this session at
`src/FusionRpg.Core/Balance/Guards/DominanceGuard.cs:38`. A mechanism node is not expressible as an input.
So the emit gate proves the structural property, and **nothing proves the behavioural one.**

### Verdict — **THESIS HOLDS, in a refined form**

Not "there are too few mechanisms to fill the quota" — that version is wrong and the rebuttal kills it.
The version that holds: **the quota is checkable but not load-bearing.** R-M1 can be satisfied at 100% by
content §3.5 already measured as worthless, and the program has no gate that would notice.

The fix is narrow and already exists in pieces: [05](05-mechanism-taxonomy.md) §6.4 step 3 is a
trial-based sibling to `DominanceGuard` over `BattleEngine`, scoped at ~1–2 sessions; step 4 (Battle fires
`OnDamageTaken` / `OnDamageDealt` — zero grep hits in `src/FusionRpg.Core/Battle/` today) is its
prerequisite. **Until that lands, the correct statement in the ideal is [06](06-red-team.md) F2's option
1: deep tiers are playtested, not proved.** The ideal currently asserts provable balance in §3.1 and
denies it in §3.5, two sections apart, and that has not been reconciled.

---

## 4. "The concentration reward solves a resolver problem in the wrong layer"

### The attack

§3.3 measured that breadth beats focus, and §3.5 explained why: *"defensive layers compose
multiplicatively."* [05](05-mechanism-taxonomy.md) §2.1 gives the exact product, line by line:

```text
E[D_clean] = pHit × (1 − parry − block) × (base + power) × K/(K + defense·pierce) × ampFactor × critTerm
```

Every factor but `(base + power)` is individually saturating, and every factor is on a different aptitude
(`OverlayCombatCalculator.cs:163-165`, `:183-188`, `:426-442`, `:382-383`, `:402-406`, `:166-173` — all
FACT via 05 §2.1's table). Maximising a sum of concave functions under a linear budget equalises
marginals; the optimum spreads. **The resolver's composition is the cause.**

So the honest fix is to change the cause: make defensive layers additive, or cap their composition. What
the design does instead is bolt a Herfindahl multiplier (D4) and a cross-unlock rule (D28) *on top of* a
resolver that keeps pushing the other way. That is treating the symptom in a layer that cannot reach the
disease.

And the resolver's shape is not even hard-coded — it is **already a tunable**. `data/tuning/combat.v1.json`
ships `defenseShape: "divisive"` and `ampShape: "reciprocal"`, read into `CombatPolicy.Default` at
`CombatPolicy.cs:35,38` (FACT, verified this session). The alternatives exist as enum members
(`CombatTuning.cs:8-45`). The design ran a magnitude sweep across 15 cells and never ran the one-key
counterfactual.

### The rebuttal

**The multiplicative composition is load-bearing, and the two available flips are both regressions.** Read
the enum members' own docs, not just the enum:

- `DefenseShape.Subtractive` is *"the classic subtractive cliff"* — a defender whose defense exceeds
  offense **floors the hit at zero** (`CombatTuning.cs:10-13`). Total immunity from one stat.
- `AmpShape.LinearClamped`: *"once `reduction` exceeds `amplification` by one whole `AmpScale`, the
  multiplier is exactly 0 and the target takes literally nothing from any attack at any power"*
  (`CombatTuning.cs:29-33`).

Both are **hard ceilings on a magnitude path**, which invariant 11 / PS-8 forbids. The shipped shapes were
chosen precisely so *"mitigation may not reach total"* and *"the curve never crosses zero"*
(`OverlayCombatCalculator.cs:410-414`). So "flip the tunable" is available, and both available flips are
worse.

**Deeper: divisive mitigation is what makes the quadratic power ladder work.** The function's own doc
(`OverlayCombatCalculator.cs:412-414`, FACT): *"Scale-invariant: doubling offense and defense together
leaves the mitigated FRACTION unchanged, **which is the property a quadratic power ladder needs** and a
constant divisor cannot give (ssot-power-scale.md §2)."* Making layers additive would decouple mitigation
from `P(Θ)` and put the resolver at odds with the one-ladder rule. That is not a tuning change; it is an
architecture change needing a `decisions.md` row and re-blessed battle goldens
(`tests/FusionRpg.Core.Tests/Goldens`, driven by `BattleGoldenTests.cs`).

**And "breadth beats focus" is not a bug to be fixed.** §3.3 says so plainly: *"This is the ordinary ARPG
truth that a glass cannon dies."* A resolver in which stacking one axis wins is a worse game than one in
which it does not.

### Where the thesis survives — and it is about `F`, not the resolver

The attack aims at the wrong bolt-on. The one that does not survive scrutiny is **`F` itself**.

- §3.5 swept `Fmax ∈ {1.0, 1.25, 1.5}` and reported **not one cell reverses the ordering** — and at
  `b=20, Fmax=1.5` the corner is *marginally worse* than at `b=0, Fmax=1.0` (43.0% vs 43.7%).
- D5 accordingly shrank `Fmax` to **1.15–1.25**, explicitly *"not the lever."*
- D28's cross-unlock, measured in [09](09-crossunlock-sweep.md), **is** the lever: corner 43.4% → 49.9%,
  *"the first result in this program where a corner beats spread."*

So the program has one measured mechanism that works and one measured mechanism that does not, and it is
keeping both. `F`'s residual cost is not zero:

| What `F` costs | Cite |
|---|---|
| Two tunables (`Fmax`, `w`) and a two-index blend | ideal §3.2; `w` is still open at §11.4 |
| A UI requirement, now an acceptance criterion — L6, *"a player scored on a rule they cannot see will not believe the game is fair"* | [07](07-learnability-and-surface.md) §5.4 |
| A rendering problem with no unit class, risking a fourteenth | [07](07-learnability-and-surface.md) §9 item 4 |
| D8's self-spent-only rule, which [06](06-red-team.md) F4 shows is a breadth exploit on the three sources the amendment does not name | ideal `:40` vs D2 `:34` |

Four live obligations for a bounded ±15–25% nudge that the design's own sweep says changes no ordering,
sitting next to a rule that changes the ordering for free.

### Verdict — **DESIGN HOLDS on the resolver; THESIS HOLDS on `F`**

Do not touch the resolver: multiplicative saturating layers are correct, scale invariance is required by
the power ladder, and both shipped alternatives introduce the immunity PS-8 forbids. The "wrong layer"
charge fails.

But the thesis's underlying instinct — *the multiplier is machinery bolted onto a problem it cannot reach*
— is right, and §3.5 proved it before I did. **`F` should be re-argued now that D28 is measured, not kept
out of momentum.** Keeping it is defensible only if someone states what it buys that D28 does not; nobody
has.

---

## 5. "Free build plus 39 trees is not a build system, it is a checklist"

### The attack

D1 keeps free build: no class, no container, no exclusive commitment. Every tree is eventually
unlockable, bounded only by D25's rising cost. Under that shape allocation converges — there is one
optimal order to buy nodes in, everyone finds it, and the tree becomes a chore you complete rather than a
choice you make.

The arithmetic was measured before D25 landed: [07](07-learnability-and-surface.md) §1.3, at
`skillPointsPerTheta: 1` (FACT, `aptitudes.v5.json:16`) and `PointBudget.PointsFor = sourceValue × rate`
with **no cap anywhere** (`PointBudget.cs:31-41`) — at Θ≈1,450 **every node in every tree is unlocked**.
It also records the genre counter-evidence: Last Epoch's design rests on the opposite, *"these trees are
specifically designed to not be completable."* And it corrects the prior-art doc, which had logged breadth
as *"bounded"*: it is not bounded, it is merely **slow**.

### The rebuttal

**D25 closes it, and in the shape this repo requires.** Unlock cost rises with the number of nodes an
actor already owns — arithmetic, per actor. The ideal is explicit that this is *"a **soft economic bound,
not a ceiling**, so PS-8 is satisfied — nothing is refused, breadth just prices itself"*
(`passive-tree-ideal.md:66`), and it reuses the soul track's existing arithmetic-cost shape, so it adds no
new ladder. That is exactly the treatment invariant 11 asks for: not a cap, a price.

**D29's depth compounds it.** Tier 10 opens at `Θ≈170` for an all-in build. Every point spent broadening
is a point not spent reaching a capstone, and the capstone is where R-M1 puts the only nodes §3.5 says
matter. That is a real trade, not a checklist.

**D28 makes the trade asymmetric in the intended direction.** [09](09-crossunlock-sweep.md): a pure Might
build *is* a Force build, so *"its whole posture comes along for free"*, while the four-of-one-posture
spread saturates at **0.62–0.69×** a pure build's tree power. Concentration is now cheaper *and* stronger.

**And the soul track is the real long-term sink.** D3's Deepen track is unlimited and arithmetic-cost. A
player at Θ=1,450 who has unlocked everything has spent nothing on depth, and depth is where §4's
`power ∝ effort` property lives. "Completing" the unlock track is the beginning of building, not the end.

### Verdict — **DESIGN HOLDS, conditionally**

The structural answer is right and the mechanism is the correct kind.

**The condition is not rhetorical: D25 has no shape and no number.** Its text says "arithmetic" and
nothing else — no first cost, no step, no tuning key named. §11.4 lists only two open items and D25's
coefficient is not among them, which reads as more settled than it is. Until the step exists, §1.3's
"everything unlocks at Θ≈1,450" is still the live arithmetic, and whether D25 bites at Θ=200 or Θ=20,000
is the difference between a build system and a checklist. That is a task, not a risk — but it is not done.

Second, smaller: [09](09-crossunlock-sweep.md) reports honestly that **cross-posture pairs remain the
strongest kind at 53.0%**, above a corner's 49.9%, against the owner's stated intent that mixing two major
categories carry no advantage. Focus-vs-spread is fixed; focus-vs-two-posture-hybrid is not.

---

## 6. My own objections

### 6a. Every number in this program is a 1v1 duel. The game is fielded six at a time.

**This is the objection I would defend hardest, and nothing in the ideal or the nine research files raises
it.** Grepped: `squad` appears in the passive-tree doc set only as a *performance* concern
([06](06-red-team.md) F8) and once as UI vocabulary. Never as a balance scope.

**FACT — the game fields a squad.** `src/FusionRpg.Server/WebMatchService.cs:338`:

```csharp
const int maxSquad = 6;
```

`BuildSquad` assembles up to six `BattleActorSetup` rows, each with its own species, level, star mods,
loyalty mods and **its own effective species allocation** (`:392-417`; the per-species allocation read at
`:308-309`). `BattleEngine` resolves `"squad"` against `"wave"` and ends when either side is wiped
(`BattleEngine.cs:255`, `:502`).

**FACT — every balance number in this program is pairwise.** `DominanceGuard.Measure` builds one
`Predictor.Actor` per build and calls `Predictor.Predict(actors[i], actors[j])` for each ordered pair
(`DominanceGuard.cs:44-57`). `_hybrid-viability.json` is 91 builds at Θ=100, one actor each, with the
extreme corners at **0.27%** and **97.95%** mean win share (read this session — FACT). §3.3, §3.5 and
[09](09-crossunlock-sweep.md) all inherit that shape.

**Why this is not a nitpick.** §3.5's causal explanation is that *"a corner build maxes one axis and
floors eleven, so every opponent finds an open one."* That sentence is true of **one actor**. At squad
scope the player already fields six, and under D21 each carries its own tree state, its own share vector,
its own `H` and its own `F`. So:

1. A player fielding six pure corners collects **six times `Fmax`** — every actor maximally concentrated —
   while the *squad* covers every defensive layer §3.3 says breadth needs. Concentration reward and
   breadth coverage are collected simultaneously, because they are measured at different scopes.
2. The risk half of the owner's framing — *"spend all in one is risk and reward, become weaker too (lack
   of defense)"* — is paid by the actor and absorbed by the squad. The glass cannon does not die; it
   stands behind the bulwark you also own.
3. This is **the same defect as [06](06-red-team.md) F4, one level up.** F4: `H` reads self-spent points
   only, so take your breadth from the three sources `H` does not read. Mine: `H` reads one actor, so take
   your breadth from the five actors `H` does not read. The general form is **`H` measures commitment in a
   scope narrower than the scope at which power is delivered.** Any such gap is arbitrage.

**The honest counter, and why it does not close it.** Two readings could rescue the design:

- *Actors are not interchangeable* — species, element and level constrain which six you can field, so six
  perfect corners is not free. True, and it reduces the exploit's size. It does not change its sign.
  INFERENCE.
- *The lawn is different* — on the PvZ host the commander is one actor and plants do not carry trees.
  Partly true, but standalone-first (invariant 9) makes the web battle the core game, and the web battle
  is six-vs-wave.

**What it costs to answer.** Almost nothing, and the pieces exist. `DominanceGuard.Measure` already accepts
an arbitrary build list, which is how §3.3's hybrid sweep ran without a guard change
(`tools/HybridViability`). `BattleEngine` already resolves a squad deterministically
(`BattleEngine.cs:10-20`). The missing step is a squad-shaped harness — which
[05](05-mechanism-taxonomy.md) §6.4 **step 3 already scopes at ~1–2 sessions**, just aimed at 1v1.

**Verdict — THESIS HOLDS.** Every measured statement this program rests on — §3.3's inversion, §3.5's
"magnitude cannot rescue focus", [09](09-crossunlock-sweep.md)'s reversal, and therefore D4, D5, D7 and
D28 — is measured at a scope the game does not use. The results may survive re-measurement at squad scope.
They have not been asked to.

### 6b. D15 conserves a budget in a unit the program has already measured is not value

D15 locks **equal expected value, not equal shape**, and [02](02-deterministic-planner.md) §4.1 defines the
conserved quantity precisely:

```text
CONSERVED, identical for every tree:  Btotal = Σ price(node).Total     — PowerVector.Total
```

`PowerVector` is an integer 5-vector priced by `CostFunction` as
`coeff(kind, channel) × normalize(magnitude, referenceScale) × conditionality`
(`PowerVector.cs:19-20`, `CostFunction.cs:22-24` — cited FACT via 02 §4.1).

**The objection: `Total` is not value, and this program has already measured how badly.** In
`_hybrid-viability.json`, at identical Θ and identical point budgets, the twelve corners range from
**0.3% to 97.9%** mean win share. Same points, same budget, ~300× apart in outcome. Equal `Total` across
the Might tree and the Focus tree therefore does not make them equally good; it **guarantees** the Might
tree is the better tree, because the underlying aptitude is worth more per unit and D15 forbids
compensating for it.

This matters more than it sounds, because the tree layer is the one place the imbalance could be corrected.
§1 says so: the class system's dominance matrix is *"soft red — `Bulwark` beats all 11 corners"*, and *"the
named fix was always a later layer"* (`passive-tree-ideal.md:19-21`). D15 as written removes the fix from
the toolbox: a weak aptitude may not be given a stronger tree, because that would break equal expected
value.

**The design already knows two-thirds of this and did not join it up.**
[02](02-deterministic-planner.md) §5.3 lists three caveats on the same cost function, verbatim: caveat 1,
*"the cost function is knowingly wrong on multiplicative pairs"* — with
`ContentValidation.DriftTolerancePercent = 25` existing *because of it*; caveat 2, **"marginal value
diverges wildly by build"**; caveat 3, the fix is E10's marginal read, which does not exist. Those three
are raised to argue that mechanism and magnitude nodes cannot be traded *within* a tree. **Nobody applies
caveat 2 across trees, which is where D15 lives.** It is the same defect and it is larger there, because
across trees the divergence is measured at 300×, not inferred.

**The honest counter.** D15 carries a real property worth keeping: *"no tree is OP"* stays
machine-checkable, and un-equalising budgets hands a balance pass 39 free dials with no gate. So the fix
is not "let budgets vary by feel" — it is to say what the budget is conserving. Two shapes are available
and neither needs new machinery:

1. **Conserve `Total`, and state plainly that tree budget does not correct aptitude imbalance** — the
   imbalance is the resolver's and the class layer's to fix. Costs a sentence; costs the §1 claim that
   trees are where the soft-red matrix gets fixed.
2. **Conserve `Total × w[aptitude]`**, where `w` is a declared, UNMEASURED per-aptitude weight flagged the
   way `pointEconomy`'s tier weights already are (*"shipping a guess is fine; calling it balance is not"*,
   `aptitudes.v5.json` `_weightsWhy` — FACT, read this session). Machine-checkable, one data file, and it
   makes the correction a stated decision rather than a silent absence.

**Verdict — THESIS HOLDS.** D15 is not wrong to want a conserved quantity. It is wrong that
`PowerVector.Total` is the one, and the program's own artifact contains the counterexample.

---

## 7. What the design gets right — a critic's concessions

Specific, cited, offered as settled ground.

1. **§3.3 and §3.5 are the best work in the program, and a model for how to be wrong.** A measurement was
   run that **refuted the design's own premise** — spread beats concentration monotonically — and the
   result was published, not buried, then acted on (D5 shrank `Fmax`; §3.5 converted `b` from a balance
   dial into a content-density choice). Running the sweep that could kill your own decision is rare and
   should be said out loud.

2. **D24 and §10.1 are correct and correctly reconciled.** The static catalog does not violate
   seed → concrete → per-player; it lands on the shared-deterministic side that `DESIGN-GATE.md` §1 row 45
   already states for species. And [01](01-static-vs-rolled.md) §4.3 makes the strongest version of the
   honest counter (roll magnitudes only, keep identity fixed) and rejects it on three grounds. It also
   notes correctly that a per-player world seed **exists** — so not rolling is *"a design choice made
   against an available mechanism, not a gap."* That framing is exactly the one the design gate asks for
   — an unused capability is a choice, and an inert path is a wiring gap, never an architectural wall.

3. **D26 is a genuinely good catch, and the fix is exact rather than approximate.** D20's *"flat reward
   per point"* was false at tier 1 — `W/req` was `0.100·b` against a `0.200·b` asymptote, **a 2× worse
   deal** — because the requirement indexed `t(t−1)/2` and power indexed `t(t+1)/2`. Sharing one index
   makes it `b/5` at **every** tier by construction. A real defect found by arithmetic and closed by
   arithmetic.

4. **D11 (items grant points, not node unlocks) is right for the reason given.** It makes the tier gate
   true *by construction* rather than by a special case, mirroring the shipped aptitude rule where an
   aptitude is a SOURCE and not a channel. There is no rule to enforce and no test to write. Concede
   fully.

5. **D14's property-keyed exclusion is the only form that survives generated content.** O(1) versus O(n²),
   and it covers nodes that do not exist yet. The design also states its own blocker honestly — atom tags
   are free-form JSON today (`AtomRow.cs:40`), so it *"is decorative"* until `spec-eligibility-tags.md`'s
   registry lands. Naming your own critical-path dependency inside the decision table is the behaviour the
   design gate exists to produce.

6. **D28 was decided by measurement after the red team argued the opposite, and the sweep was allowed to
   overturn the red team.** [09](09-crossunlock-sweep.md) reversed F1 with a model that credited the
   *floored* trees the red team had omitted, then reported the residue it did **not** fix (cross-posture
   pairs still strongest at 53.0%) rather than declaring victory. Correct epistemics, correct write-up.

7. **§3.1's "no `1/n` normalization" argument is correct and gets stronger as the roster grows.** At
   n = 738 the dropped term is 0.00136. Dropping it genuinely lets the roster grow forever without
   re-scaling anybody's existing build — which is what makes D27's "ship the roster whole, curate later"
   safe rather than reckless.

8. **§9's skew measurement is exact and independently reproduced.** 841 entries across 503 species files;
   `Onslaught` 332 (39.5%) against `Ferocity` 2 (0.2%); `earth` 379 (45.1%). [06](06-red-team.md) §12
   re-counted and every figure matched. The corollary — *"a species' thematic favour and its mechanical
   lock need not be the same field"* — is the single best sentence in the document, and D32 is the right
   answer to it.

9. **The whole enrichment set marks FACT / INFERENCE / RECALL and lists what it could not close.**
   [05](05-mechanism-taxonomy.md) §9 ticks its "tested the constraint" box as `[~] PARTIAL` and says which
   claims are unmeasured. [03](03-llm-stage-contract.md) admits it never opened the 830 generated demon
   files it counted. That is the honest-gap discipline `DESIGN-GATE.md` §5 asks for, actually practised.

10. **Standalone-first holds throughout.** Nothing in D1–D32 reads a Unity field, and the overlay combat
    gate being default-off in the injector (`OverlayCombatFeature.cs:13`) is correctly named as a **wiring
    gap**, not an architectural wall. The program consistently designs against the RPG layer's own stack
    rather than against what PvZ happens to represent, which is the binding rule for every feature here.

---

## 8. If I could make only one change

**Re-run the program's balance measurements at squad scope before any further tree decision is taken.**

Not because the 1v1 results are wrong, but because nobody has asked whether they transfer. `maxSquad = 6`
(`WebMatchService.cs:338`) is the shape of the actual game; `Predictor.Predict(actors[i], actors[j])`
(`DominanceGuard.cs:55`) is the shape of every number D4, D5, D7 and D28 were decided from. If the 1v1
finding survives at six-vs-wave, four decisions become genuinely settled and the whole mechanism programme
is validated. If it does not, then `F`, `Fmax`, D7's hybrid neutrality, D28's largest-mate rule and §3.5's
*"magnitude cannot rescue focus"* are all resting on a scope mismatch — and the deepest tier of 880 trees
is about to be authored against it.

It is cheap: the build list is already arbitrary, `BattleEngine` already resolves squads deterministically,
and [05](05-mechanism-taxonomy.md) §6.4 step 3 already scopes the harness at ~1–2 sessions. Aim it at six
actors instead of one.

**Runner-up, for the record:** build [13](13-review-pipeline.md)'s **tree card before the species run, not
after.** 13's own verdict makes D30 affordable *only* on that condition, and without it D24 quietly
becomes "trusted, not reviewed" — which 13 says has already happened once in this repo. It is the cheapest
item on any list in this program and it is currently in nobody's.

---

## 9. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, class system / allocation, combat
    resolver, effect atoms, balance guards, battle runtime, player UI.
[x] I read every doc in the §1 row(s) for those subsystems, this session: DESIGN-GATE.md in full,
    passive-tree-ideal.md in full (533 lines, re-read after it changed on disk mid-session),
    research/passive-tree/01 (§1, §4.3), 02 (§4.1, §5, §6), 03 (§8, §9), 05 in full, 06 in full,
    07 (answer-up-front, §1, §4, §5, §9, §10), 08 (§3), 09 in full, 13 (§0 — it landed mid-session
    and refutes half of §1's attack, which is conceded there rather than left standing).
[!] Four sibling notes appeared in this directory while this file was being written -- 10, 13, 14 and 16.
    13 is read and answered in §1 (it refutes half that attack; conceded there rather than left
    standing). 14 is spot-checked and its "nine of twelve of doc 07's calls hold at the new scale"
    is consistent with §2's verdict. 10 and 16 are NOT folded in, and 16 in particular ("D28's
    justifying sweep ran at 7 tiers and D20's superseded ladder") bears directly on §4 and §5 --
    read it alongside this file.
[x] I checked decisions.md coverage via the research set — there is no passive-tree row; the ideal
    is idea phase and no build is authorized, so nothing here contradicts a lock.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments. Read directly this session, not taken from a
    research file: DominanceGuard.cs:38 (Measure's signature), :44-57 (pairwise Predict),
    AtomKindRegistry.cs:21/:31/:36 (7/16/13), aptitudes.v5.json:15-16 and :23-30,
    combat.v1.json (defenseShape/ampShape/caps), CombatTuning.cs:8-45 (both enums' own docs),
    OverlayCombatCalculator.cs:410-442 (DivisiveMitigation and its scale-invariance claim),
    WebMatchService.cs:338 (maxSquad = 6) and :335-418 (BuildSquad), ContractPolicy.cs:160-176,
    RpgStore.Contracts.cs:80-84, docs/research/class-system/_hybrid-viability.json (91 builds,
    corner extremes 0.0027 / 0.9795).
[x] I read the surrounding section of every rule I quoted — specifically CombatTuning.cs's enum
    member docs before claiming the alternative shapes are regressions, and 02 §5.3's three caveats
    in their own section before extending caveat 2 to D15 in §6b.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL, and stated: I ran no test suite
    and no tool — this task is research-and-write and forbids touching src/, tools/, tests/, data/.
    Every behavioural claim is marked INFERENCE. §6a's squad-scope finding is a claim about what has
    NOT been measured, verified by reading both harnesses' signatures; it is not itself a
    measurement, and §8 names exactly what would settle it.
[x] Nothing contradicts a §2 invariant. Two are load-bearing here and both are respected: #11
    (§4 rejects the resolver flips precisely because both introduce a reachable zero) and #14
    (§4 keeps divisive mitigation because scale invariance is what the quadratic ladder needs).
[x] Corrections are propagated — or rather, none are owed: this file proposes no edit to another
    document. The three reconciliations it argues for (the ideal's §3.1-vs-§3.5 provable-balance
    contradiction, D25's missing coefficient, D15's budget unit) are stated as findings for the
    owner, not applied.
```
