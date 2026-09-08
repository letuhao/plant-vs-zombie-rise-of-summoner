# Passive trees — prior art vs our design (2026-09-04)

Comparison of [passive-tree-ideal.md](../architecture/passive-tree-ideal.md)'s D1–D13 against shipped
ARPG build systems. Sources are the repo's own research packs, which are source-cited and tier-marked:
[action-taxonomy/03-composable-skill-systems.md](action-taxonomy/03-composable-skill-systems.md) §6.1–6.2,
[arpg-effects/01-primary-attributes.md](arpg-effects/01-primary-attributes.md).

**Evidence marking follows the research packs' own convention.** FACT = quoted from a cited first-tier
source. INFERENCE = drawn from a fact. RECALL = the author's general knowledge of the game, *not*
verified in-repo — treat as a lead, not a citation.

---

## 1. Where we match the genre

| Our decision | Prior art | Verdict |
|---|---|---|
| Tier gates read invested allocation (D10/D12) | **FACT (Last Epoch):** *"Prerequisites are points-thresholded, not node-thresholded"* — predecessors carry a minimum points-invested requirement rather than a binary allocated flag | **Converged independently.** LE arrived at the same rule |
| Attributes gate progression, not just scale | **FACT (research pack §"Patterns worth naming" #2):** *"Attrs as soft/hard requirements (D2/PoE) — gate gear and skills; create respec tension"* | Standard. The named cost — respec tension — is one we have not yet priced (§4) |
| Specialization rewarded over spreading | Universal. PoE via travel cost, LE via a per-tree budget | Shared goal, **different mechanism** — see §3 |
| Two currencies (unlock vs deepen) | **FACT (LE):** gear affixes add skill levels that *"can exceed the normal 20 level limit"* — a second, separate depth track over the point track | Same two-track shape |

---

## 2. Where we diverge — and whether it holds

### 2.1 The deepest divergence: scarcity vs endlessness

**FACT — Last Epoch's balance IS the cap** (EHG dev blog, first-tier):

> *"These trees are **specifically designed to not be completable**. You will only obtain **20 points** to
> put into each tree total. This means that certain powerful nodes are **inherently mutually exclusive**
> from each other. This creates an interesting decision point."*

And on slot caps:

> *"if you apply restrictions to a system like this then it makes for interesting decision making. If we
> allow you to take 10 skills … then several of them will be the same ones you always take."*

**RECALL:** PoE likewise caps passive points (~120–130 from levels plus quests), so both major
comparators bound the tree budget. **We are the outlier** — souls are unlimited by D3, and the repo
forbids the LE answer outright (`PS-8`, *"endless grind is the SSOT"*, no hard progression ceilings).

**Our substitute, stated honestly:** we split the two axes where LE caps both.

| Axis | Last Epoch | Ours |
|---|---|---|
| **Breadth** (how many distinct bonuses) | Capped (20 pts/tree) | **Bounded** — skill points are finite (`skillPointsPerTheta × Θ`) |
| **Depth** (how strong each bonus) | Capped (same budget) | **Unlimited** — souls, arithmetic cost |

So mutual exclusivity survives on the **breadth** axis, which is where LE says the interesting decisions
live. Depth is where we let the grind run, and the arithmetic cost makes it a time price rather than a
choice. **INFERENCE:** this is a defensible reconciliation, but it moves the decision from *availability*
("I cannot have both") to *efficiency* ("I can eventually have both, but concentrating pays better").
Late-game builds will converge more than LE's do. The concentration multiplier is the only thing
resisting that convergence, which raises the stakes on `Fmax` being measured rather than guessed.

### 2.2 Items: we unlock nodes, LE adds points

**FACT (LE Skill Level Affixes, first-tier):** *"Gear adds points, not nodes."* Affixes add levels that
can exceed the cap; removing the gear removes the points, and *"Nodes which have lost points in this way
will be **highlighted in red** when you next view that skill's tree."*

**This is cleaner than our D11 and solves a problem we papered over.** D11 says an item-granted skill must
respect the tier gate — a special case we have to define, enforce and test. If items instead grant
**points**, the special case vanishes: points flow through the tree's own rules, so tier gates are
respected by construction, exactly as `spec-primary-stats.md` §3.1 already makes item-fed aptitudes
impossible by construction.

**INFERENCE — worth adopting:** the LE unequip behaviour (*display* the invalid state in red rather than
silently repairing it) is also the right pattern for us, and it matches this repo's habit of failing
loudly rather than clamping.

⚠️ **One interaction to settle if we adopt it:** D8 makes `H` read spent points. If gear grants points,
gear would move the focus multiplier — reintroducing exactly the trap D8's §3.2 was written to avoid.
Fix: `H` reads *self-spent* points only; gear-granted points add power, never focus.

### 2.3 Exclusion: our "blocking" vs LE's printed no-op

The owner deferred *"demon species tree will define how to block."* LE already shipped an answer.

**FACT (LE official Mage tree page):**

> *"This node is **incompatible with** the Insidious Conduction node. **If you have taken both, Insidious
> Conduction will not work.**"*

The conflicting node **stays allocatable and simply stops working**, both sides print the rule, and both
name the same winner — so resolution is deterministic and visible *without the UI modelling exclusion
groups at all*. Exclusivity language appears on roughly **2% of nodes** (computed from that page). Three
escalating forms are visible:

| Form | What it does |
|---|---|
| **Reroute** | the node redirects its effect when a conflicting node is present — no conflict ever occurs |
| **Precedence** | the conflict is defined rather than forbidden — *"…unless it has already been converted…"* |
| **Nullification** | last resort — one named node declared inoperative, printed on both nodes |

**The finding that matters most for us — exclude against a PROPERTY, not a NAME.** A node reading *"this
has no effect if the skill's damage is converted"* covers every present and future conversion node, where
a named-pair list needs a row per pair. **INFERENCE (from the pack):** property-based exclusion is O(1)
as content grows; named pairs are O(n²) — *"that, rather than restraint, is the likely reason explicit
exclusions stay at a low single-digit percentage of nodes."*

**This is load-bearing for D13.** Our trees are **LLM-generated at scale** across ~50 trees. A named-pair
exclusion list is not maintainable under generation — the generator would have to reason about every
pair it has ever emitted. **Property-based exclusion is the only form that survives generated content**,
and the deterministic plan must therefore emit *properties* (tags, conversion states, damage types) that
exclusions can key on, before the LLM writes a single node.

### 2.4 Cross-tree unlock makes respec order-sensitive — Grim Dawn already paid this

Our cross-unlock rule (skill points in a posture-mate satisfy a tier requirement) builds a dependency
graph between trees. Grim Dawn's devotion system has the identical shape and the identical cost.

**FACT (Grim Dawn, quoted in the pack):** an affinity graph with per-colour thresholds *"makes respec
order-sensitive — you cannot unlearn Devotion Points invested in a Constellation that provides you an
Affinity bonus which you need to maintain another Constellation."*

**We have not discussed what happens when a player respecs the points that were holding another tree's
tier open.** Options are cascade-revoke, block-the-respec (GD's answer), or grandfather. This is an open
item created by a decision already made, not a new feature.

**FACT (Grim Dawn, Zantai):** balance problems from a *legal* combination are fixed by nerfing the proc,
**never by deleting the binding** — *"It's really not enjoyable to find your devotion setup to suddenly be
invalidated."* Relevant to us because generated content will produce combinations nobody designed.

### 2.5 One shared tree (PoE) vs ~50 separate trees (ours)

**RECALL:** PoE runs a single ~1,300-node shared tree where class is only a *starting position*;
concentration is rewarded *emergently*, because reaching a distant cluster costs filler nodes along the
way. **INFERENCE:** that makes the concentration reward implicit and unmeasurable — you cannot state PoE's
focus bonus as a number. Ours is explicit (`F`), bounded, and sweepable by `balance-guard`. We trade
elegance for measurability, which fits a system that must prove balance rather than playtest it.

---

## 3. What is genuinely novel in our design

1. **An explicit, bounded concentration multiplier.** No comparator does this. PoE and LE both get
   specialization rewards *implicitly* — from travel cost and budget scarcity respectively. Making it a
   measured scalar (`F = 1 + (Fmax−1)·H`) is unusual, and it is the reason our claim "no tree is OP" can
   be *proved* in 2.3 seconds rather than argued.
   **Risk:** it is mathematical rather than felt. It needs the effective-tree-count surface
   (*"effective trees: 2.3 → +17%"*) or players will never perceive the rule they are being scored on.
2. **Generated trees.** PoE and LE hand-author every node. Nobody in the comparator set generates a
   passive tree. This is our largest departure and §4's main risk.

---

## 4. The tension our stated goal has with the genre's best practice

**Owner goal (D13):** *"every skill tree will cost and award same"* — no tree is OP.

**FACT — EHG states nearly the opposite as design intent:**

> *"**Commonly desirable nodes should be fairly accessible, while niche or unusual nodes are more
> difficult to reach**"* · *"individual nodes should not be so potent that you feel forced to build it in
> a particular way"*

Note what is and is not being said. The constraint EHG applies is on **potency** — no node so strong it
forces a build — *not* on uniformity. They deliberately make reach **unequal** so that discovering a
distant, niche node feels like a discovery. **A perfectly uniform tree set is perfectly predictable**, and
LE's texture comes precisely from the asymmetry we would be generating away.

**INFERENCE — the reconciliation, and it should go in the spec:** equalize **expected value**, not
**shape**. Two trees may cost the same and award the same *in aggregate* while distributing that budget
completely differently — one flat and broad, another with a hard-to-reach spike. That satisfies "no tree
is OP" (the balance property, machine-checkable) without satisfying "every tree feels the same" (a
failure mode). The deterministic plan should therefore emit, per tree, a **budget plus a shape
archetype**, not a uniform node table.

**FACT (LE, on taming runaway scaling):** Heartseeker's *"Recurve Chance being multiplied by 0.8 each time
it recurves"* was chosen because it *"helps constrain the value of stacking"* while giving *"a simple
description of its behavior that players could **intuitively understand**."* A diminishing-returns
constant picked for **explicability**, not only for balance — the same bar our `F` and soul-cost curves
should meet.

---

## 5. One more transferable mechanic

**FACT (LE transformation nodes):** *"Half of Fireball's base fire damage is converted to lightning …
Fireball **gains** a Lightning tag."* **INFERENCE (from the pack):** rewriting the *tag* alongside the
number is what stops conversion from bricking gear — every downstream modifier keyed on that tag follows
automatically, where *"a conversion that changed only the number would silently create dead stats."*

**Maps directly onto us:** a tree node that converts damage must rewrite the `ElementPayload` components,
not merely scale a magnitude — otherwise a player's element-keyed affixes silently stop applying. Our
element system already carries weighted payload components, so the mechanism exists; the rule is that
conversion nodes must be authored against it.

---

## 6. Recommendations

| # | Recommendation | Affects |
|---|---|---|
| R1 | **Reconsider D11** — items grant *points*, not node unlocks (LE model). Removes the tier-gate special case entirely | D11 |
| R2 | **`H` reads self-spent points only** — gear-granted points add power, never focus | D8 |
| R3 | **Exclusion must be property-based, never named-pair** — the only form that survives generation | D13, demon species round |
| R4 | **Adopt LE's escalation ladder** (Reroute → Precedence → Nullification) and keep exclusions rare (~2% of nodes) | demon species round |
| R5 | **Price the respec dependency** created by cross-unlock — cascade, block, or grandfather | open item |
| R6 | **Equalize expected value, not shape** — the plan emits budget + shape archetype per tree | D13 |
| R7 | **Bound node potency explicitly** — no node so strong it forces a build (EHG's own constraint) | D13 |
| R8 | **Conversion nodes rewrite element payload tags**, not just magnitudes | atom/effect layer |
