# Passive skill trees — the ideal

**Status:** idea phase, 2026-09-04. **Not a spec. No build authorized.** This is the "later discuss"
conversation that [class-system-map.md](class-system-map.md) §5 reserved by name: *"there are many sub
features for class system, includes passive skills, will be added later"* (owner, 2026-08-26).

**Owner framing, 2026-09-04:** *"we will make have many build tree"* · *"every skill tree will cost and
award same and we decide it by math functions"* · *"there are no skill tree that so op"* · *"spend all in
one is risk and reward — become stronger but become weaker too (lack of defense)"*.

> Quotes are lightly normalized for two recurring input-method artifacts only (`althemetic` → arithmetic,
> `desterministic` → deterministic). No wording was otherwise changed.

---

## 1. What this program is for

The class system ships a balanced **allocation** layer and stops there. Its own acceptance record says
why that is not enough: the dominance matrix is **soft red — `Bulwark` beats all 11 corners** — and the
named fix was always a later layer, *"a passive scaling damage with damage taken, a reflect build, an
anti-turtle punish"* (class-system-map §4b). This is that layer.

It is also where **identity** lives. Under free build there is no class name to carry meaning, and an
aptitude is deliberately *"a MECHANISM, never a FLAVOUR"* — **168 of 259 channels (65%) are reserved for
the skill/item layer** (class-system-ideal §4.1). Trees are what spend that reservation.

---

## 2. Decisions locked by the owner (2026-09-04)

| # | Decision | Consequence |
|---|---|---|
| D1 | **Free build stays.** No player class; classes remain Zomboss patterns | Confirms the 2026-08-25 correction; trees add identity without adding a class container |
| D2 | **All four acquisition sources**: skill points · aptitude thresholds · items/affixes · demon aspect | The `skillPointsPerTheta: 1` grant, minted since 2026-08-26 with **zero consumers**, finally has a spender |
| D3 | **Every skill has two unlock tracks**: skill points unlock *new bonuses* (discrete); souls scale *bonus power* (unlimited, arithmetic cost) | Matches the shipped two-ladder economy exactly — see §4 |
| D4 | **Concentration is rewarded by a bounded Herfindahl multiplier** | `F = 1 + (Fmax−1)·H`, `H = Σ(shareᵢ)²` — see §3 |
| D5 | ~~`Fmax = 1.5`~~ → **revised 2026-09-04 to a small nudge, `Fmax` 1.15–1.25, tunable** | §3.5's sweep showed no `Fmax` reverses the concentration penalty, so the multiplier is not the lever — deep-tier MECHANISM nodes are. Keeping it small stops focus being strictly worse on average without pretending it fixes the gap |
| D6 | **The multiplier applies to all trees equally** | Offensive and defensive commitment are equally valid; symmetric and explainable |
| D7 | **Hybrids stay Neutral, not Penalized** | A 2–3 way build sits 18–24% behind pure — behind, but alive. Spreading across everything is deliberately weak |
| D8 | **`H` reads spent points + souls** — **amended 2026-09-04 (R2): self-spent only.** Gear-granted points add power, never focus | Both currencies are commitment. Requires the two-index blend of §3.2. The self-spent qualifier closes the trap D11's amendment would otherwise reopen: a good off-build drop must never lower your multiplier |
| D9 | **Tree roster**: 12 primary + all elemental + all status + each demon family (+ demon species, deferred) | `n ≈ 40–60`, which *simplifies* the math — see §3.1 |
| D10 | **Same shape everywhere**: every tree is 2 branches (offensive/defensive) × tiers | One generator archetype, one set of math functions |
| D11 | ~~Item-granted skills respect the tier gate~~ → **SUPERSEDED 2026-09-04 (R1). Items grant POINTS, not node unlocks.** Removing the gear removes the points; affected nodes are **displayed as invalid (red), never silently repaired** | Strictly cleaner: points flow through the tree's own rules, so the tier gate is respected **by construction** — no special case to define, enforce or test. Same trick `spec-primary-stats.md` §3.1 already uses to make item-fed aptitudes impossible. Prior art: Last Epoch, *"Gear adds points, not nodes"* |
| D12 | **Tier gates read base allocation, never item bonuses** | Already true by construction — see §5 |
| D13 | **Generation is deterministic-first**: math decides tree shape, power ladder, unlock requirements and skill links; **only then** does an LLM fill vocabulary, categories, atom pools and bonuses | Balance is a property of the plan, not of the generated content. **Extended 2026-09-04 (R6/R7): the plan emits a budget + shape archetype per tree (equal expected value, NOT equal shape), a per-node potency ceiling, and the property vocabulary D14's exclusions key on** |
| D14 | **Exclusion is property-based and expressed as a printed runtime no-op** (R3/R4) — never a named-pair list, never an allocation block | A named-pair list is O(n²) and cannot survive LLM generation across ~50 trees; a property predicate (*"no effect if the damage is converted"*) is O(1) and covers nodes that do not exist yet. Escalation ladder: **Reroute → Precedence → Nullification**, both sides print the rule and name the same winner. Target rarity ~2% of nodes |
| D15 | **Equal expected value, not equal shape** (R6) | *"No tree is OP"* is a balance property and stays machine-checkable; *"every tree feels the same"* is a failure mode. Two trees may cost and award the same in aggregate while one is broad and flat and another hides a hard-to-reach spike |
| D16 | **Conversion nodes rewrite element payload tags, not just magnitudes** (R8) | Otherwise a player's element-keyed affixes silently stop applying — *"a conversion that changed only the number would silently create dead stats."* Our weighted `ElementPayload` components already carry the mechanism |
| D17 | **Demon species trees lock a build-favour triple: primary tree + element + status** (owner, 2026-09-04). Extending that favour into the seeds is a **deterministic planner → agent-inspects-seed → validated-against-target** pipeline, never an LLM free choice | Owner: *"need a balance distribution to keep diversity, avoid LLM decide everything cause the game favour some primary stats and element build, ignore others."* **This fear is already measured fact — see §9.** The corpus a species tree would lock against is skewed 165:1 today |
| D18 | **Respec is a FULL reset** — skill distribution *and* primary stats together — priced in **souls**. **Items cannot be respecced**; an item build is re-farmed, not rebuilt, which is why it costs more effort | Owner: *"user can respec everything… so user can rebuild with replay whole game."* This **dissolves the Grim Dawn order-sensitivity problem entirely** (open item #2): with no partial respec there is no orphaned unlock, because allocation is cleared and redistributed as one transaction. Implementation stays derive-on-read so no second source of truth can drift. `pointEconomy.respecPrice` already exists |
| D19 | **`status_mastery` becomes a fifth `AllocationScope`** — per-status progression earned through use, symmetric with Aspect's `element_mastery` | Closes the one tree category with no gate quantity. Cost: the shipped four-row per-scope rate table grows to five |
| D20 | ~~**Tier thresholds are QUADRATIC: `req(t) = 10 + 2.5·t·(t−1)`**~~ → **superseded by D26**; the QUADRATIC shape and the linear-power pairing rule both survive, only the indexing changes. Original: → 10 · 15 · 25 · 40 · 60 · 85 · 115 (owner's sequence). **Binding pairing rule: per-tier power must grow LINEARLY with tier** | See §3.5 and [../research/passive-tree/02-deterministic-planner.md](../research/passive-tree/02-deterministic-planner.md). The sequence is not arithmetic — its *second* differences are constant (5), so it is quadratic. Paired with linear per-tier power it yields flat reward-per-point at every depth (§10.5's property). Paired with *constant* per-tier power it **inverts the whole design** |
| D21 | **Every actor carries its own tree state** — Commander and each demon alike (owner, 2026-09-04) | Maximum build expression: each demon is genuinely built, not a stat block. **This promotes sparse storage from a nicety to a hard requirement** — ~50 trees × ~29 skills is ~1,450 possible per-skill soul levels *per actor*, so only non-zero entries may ever be persisted (§7.9). A dense row per actor is not an option at this scope |
| D22 | **Passives compose from the shipped atom catalog** — no passive-specific effect vocabulary | Avoids the exact defect the atom program exists to prevent (*"inventing a third vocabulary"*), and hands D14's property-keyed exclusions an existing property space: atom tags |
| D23 | **A demon species tree's reward is a UNIQUE tree** — nodes no other tree has, with **its own generation pipeline** (owner: *"better to spend effort for it now, maybe deploy agent to enrich it"*) | The strongest identity/collection pull, and the honest price for D17's lock: you give up build freedom and receive something unobtainable elsewhere. Cost is real — per-species authored content at 841-entry scale — which is why it gets a pipeline of its own rather than riding the generic tree generator |
| D28 | **Cross-unlock credits ONE tree — your largest posture-mate — never a sum** (measured, 2026-09-05) | The sweep (`--crossunlock`, [09](../research/passive-tree/09-crossunlock-sweep.md)) is the first result in this program where **a corner beats spread**: 49.9% vs 47.7%, against 43.4% vs 54.4% with cross-unlock off. Crediting the full sum also flips the ordering but compresses every build into 48.6–51.8% — a rule that gives everyone everything stops discriminating. One mate is O(1), explainable, and **bounded by construction**: no k-way build can compound it |
| D25 | **Unlock cost rises with the number of nodes an actor already owns** — arithmetic, per actor (owner, 2026-09-05) | Closes the unbounded-breadth hole: at `skillPointsPerTheta: 1` with `Θ` uncapped, the whole catalog unlocked at `Θ≈1,450` and the tree stopped being a choice. This is a **soft economic bound, not a ceiling**, so PS-8 is satisfied — nothing is refused, breadth just prices itself. **It is also the concentration-reward-on-the-cost-side that cross-unlock was meant to be, with the sign pointing the right way.** Same arithmetic-cost shape the soul track already uses (§4), so it adds no new ladder |
| D26 | **The tier requirement is reconciled to the power index: `req(t) = 5·t·(t+1)/2`** → 5 · 15 · 30 · 50 · 75 · 105 · 140 (owner, 2026-09-05: *"our power ladder and effort spent is scattered, maybe we need reconcile them for balance and persistency"*) | D20's *"flat reward-per-point at every depth"* was **false at tier 1** — `W/req` was `0.100·b` against a `0.200·b` asymptote, a **2× worse deal**, because the requirement indexed `t(t−1)/2` while power indexed `t(t+1)/2`. Sharing one index makes it exact: `W(t) = b·t(t+1)/2` over `req(t) = 5·t(t+1)/2` is **`b/5` at every tier, by construction** — not flat within 11%, flat identically. The owner's original sequence was a correct instinct expressed on a mismatched index |
| D27 | **The roster ships whole** — 12 primary + 6 elemental + 21 status + demon family + species (owner, 2026-09-05: *"ship everything, we will make spec and plan to build one by one later"*) | D9 is **not descoped**. `family`'s 699 open tokens are a **build-order task, not an idea-phase blocker** — the curation gets tracked and sequenced when planning starts. §3.1's dropped `1/n` term is what makes this safe: *"the roster can grow forever without re-scaling anybody's existing build"*, so categories can land in any order without invalidating one another |
| D24 | **The tree CATALOG is STATIC, SHARED and IDENTICAL for every player** — concrete values baked before the game runs (owner, 2026-09-05) | Owner: *"the passive skills tree need concrete value before the game run… it is different with item loot mechanism… it need solid stats, so user can learn it, if it random every new player create, it will cause confuse, user cannot build because they need to relearn."* **LEARNABILITY is now a stated requirement, not a nicety.** This is the one place the binding seed → concrete → **per-player** principle does *not* apply: generation is a BUILD-time step whose output is committed content, and the only per-player state is *allocation* (which nodes, how many souls). A rolled catalog would make build knowledge worthless and build guides impossible — see §10 |


> **D8, D11, D13–D16 were amended or added on 2026-09-04 from verified prior art** — see
> [../research/passive-tree-prior-art-2026-09-04.md](../research/passive-tree-prior-art-2026-09-04.md)
> (Last Epoch, Grim Dawn, PoE; source-cited and tier-marked). Superseded wording is kept struck through
> rather than deleted, so the record shows what changed and why.

**Deferred by the owner to its own round:** demon **species** trees — *"consider as reward; building it
takes advantage but will block some builds… demon species tree will define how to block."* A tree that
*costs* options is a different mechanic and is not specified here.

---

## 3. The concentration function

### 3.1 The function

```text
H = Σ (shareᵢ)²                     Herfindahl index over invested trees
F = 1 + (Fmax − 1) · H              the focus multiplier on tree-derived power
Fmax = 1.15-1.25 (tunable, D5 as revised)
```

Per-tree power stays **linear in investment**; `F` is a pure *shape* function of how commitment is
spread, never of how much was spent.

**Why no `1/n` normalization.** The textbook form normalizes by tree count, `H* = (H−1/n)/(1−1/n)`. With
D9's roster (`n ≈ 50`) that term is a rounding error — and dropping it removes every `n` dependence, so
**the roster can grow forever without re-scaling anybody's existing build.** A tree with zero investment
contributes zero to `H`. This was the one real objection to a wide roster, and it disappears.

| Trees invested (even) | H | F (at the superseded `Fmax`=1.5) | vs pure |
|---|---|---|---|
| 1 | 1.000 | **1.500** | — |
| 2 | 0.500 | 1.250 | −18% |
| 3 | 0.333 | 1.167 | −24% |
| 4 | 0.250 | 1.125 | −27% |
| 12 | 0.083 | 1.042 | −31% |

Uneven splits fall out naturally: 70/30 → `F = 1.29`; 50/25/25 → `F = 1.19`.

**Why concentration needs convexity.** Concave ("diminishing returns per tree") curves mathematically
*reward spreading* — that is what concavity means. Rewarding focus requires convexity somewhere, and a
bounded multiplier is the safest place to put it: `F ≤ Fmax` is provable at any resource level, so a
10× build is arithmetically impossible rather than merely unlikely.

**The risk side needs no math.** All-in on Might means zero Fortitude and zero Bulwark. The gaps are the
punishment; `F` is only the compensation.

### 3.2 Blending the two currencies (D8)

Points and souls are different units, and **souls are unlimited while points are not** — summed directly,
souls would eventually swamp the share vector and point allocation would stop affecting focus. Compute
one index per currency and blend:

```text
H = w · H_points + (1 − w) · H_souls          w tunable
```

Units never mix, souls cannot dominate, and soul commitment still counts. The multiplier does then move
as souls are spent — acceptable, and arguably good feedback, **because `F` is bounded**: the
spend → stronger → farm → spend loop terminates at `Fmax` instead of running away.

### 3.3 What `Fmax` must be solved against — MEASURED 2026-09-04, and it inverts the assumption

A uniform shape multiplier gives **every pure corner the same 1.5×**, so corner-vs-corner ordering — what
the dominance matrix tests — is largely preserved. `Fmax`'s real effect is **corner vs hybrid**, which
`balance-guard` has never reported.

**It has now been measured.** `DominanceGuard.Measure` already accepts an arbitrary build list, so no
guard change was needed — only different builds passed in (`tools/HybridViability`, 91 builds at Θ=100:
12 corners, 66 two-way, 12 three-way, 1 even-twelve). Class against class, win share of attacker:

| attacker ↓ / defender → | corner | hybrid2 | hybrid3 | spread |
|---|---|---|---|---|
| **corner** | 50.0% | 43.3% | 40.1% | 41.2% |
| **hybrid2** | 56.7% | 50.0% | 47.1% | 43.7% |
| **hybrid3** | 59.9% | 52.9% | 50.0% | 35.8% |
| **spread** | 58.8% | 56.3% | 64.2% | — |

> ⛔ **Spreading currently BEATS concentrating, monotonically.** A 2-way hybrid beats a corner 56.7%; a
> 3-way beats it 59.9%; the even-twelve build beats corners 58.8% and 2-way hybrids 56.3%. The mean
> win-share ordering is **spread 57.7% > hybrid3 53.3% > hybrid2 50.4% > corner 43.7%** — the exact
> reverse of D4–D7's intent.

**Why, and it is not a bug.** The twelve aptitudes cover complementary layers — mitigation, shield,
dodge, guard, crit-denial, penetration — and defensive layers compose *multiplicatively*. A corner build
maxes one axis and floors eleven, so every opponent finds an open one. Breadth is mechanically favoured
by the resolver we shipped. This is the ordinary ARPG truth that a glass cannon dies.

**But the mean hides the shape, and the shape is what the owner actually asked for.** Corner results are
strongly **bimodal**: `Might` alone scores **97.9%** — the single strongest build in all 91 — while
`Focus` alone scores **0.3%**, the weakest. Spread scores 57.7% and nothing else. So concentration is
already high-variance: *"spend all in one is risk and reward, become stronger but become weaker too"* is
**already true in the shipped model** — expressed as variance, not as a higher average.

**What this changes for `Fmax`:**

1. `F` is not adding to an existing concentration advantage. It is **fighting an existing concentration
   penalty** of roughly 7–14 points of win share.
2. **`Fmax = 1.5` is more likely too small than too large** — the opposite of the risk flagged when it
   was chosen. And because `F` scales only tree-derived power, not allocation, its leverage depends on
   what share of total power trees carry — currently unknown and worth deciding deliberately.
3. The design goal *"someone who builds everything has no advantage"* is **not true today** — the
   even-twelve build is both the safest and among the strongest. Making it weak is a separate change to
   the resolver or the allocation curve; `F` alone will not do it.

**Caveat, stated plainly:** the closed form reads **allocation only**. Tree power, passives and `F` are
not in it, and the action layer (which prices `stamina`/`qi`/cooldowns) is still unbuilt — so a build's
whole utility half is invisible here. This is a *baseline*, not a verdict. Re-measure once trees carry
power. Verdict artifact: [`_hybrid-viability.json`](../research/class-system/_hybrid-viability.json).

### 3.5 Tree power was modelled and swept — magnitude cannot rescue a focus build (2026-09-04)

The owner's condition for this whole layer: *"if we cannot design focus build in passive tree, the
system will become almost useless — the primary stats distribution should work together with the skill
tree to make the build variable."* So the tree model was built and swept
(`tools/HybridViability --trees`), expressing tree power in **aptitude-point-equivalents** and folding
it back into the same allocation the closed form already reads — no new resolver math:

```text
p_i  = share_i · (Θ · aptitudePointsPerTheta)      points in tree i      (Θ=100 → 300 points)
T_i  = max{ t : req(t) ≤ p_i },  req(t)=10+2.5t(t−1)   tier reached      (D20)
W_i  = b · T_i(T_i+1)/2                            tree power, linear per tier (D20 pairing rule)
F    = 1 + (Fmax−1)·H                              focus multiplier      (D4)
p_i' = p_i + F · W_i                               effective points
```

`b` — how many aptitude points one tier of a tree is worth — is the parameter the design had not
decided. Swept across `b ∈ {0, 2, 5, 10, 20}` × `Fmax ∈ {1.0, 1.25, 1.5}`, mean win share:

| b | Fmax | corner | hybrid2 | hybrid3 | spread |
|---|---|---|---|---|---|
| 0 | 1.00 | 43.7% | 50.4% | 53.3% | 57.7% |
| 5 | 1.25 | 43.8% | 50.5% | 53.0% | 59.0% |
| 10 | 1.50 | 43.6% | 50.6% | 52.6% | 59.1% |
| **20** | **1.50** | **43.0%** | 50.7% | 52.3% | **59.1%** |

> ⛔ **Not one cell reverses the ordering.** At `b = 20` — a single tier worth twenty aptitude points,
> far beyond anything plausible — the focused build is still *worse*, and marginally worse than at
> `b = 0`. Raising `Fmax` does nothing either.

**Why, and this is the actionable part.** Tree power modelled as *more of the same aptitude* hits the
exact saturation that made concentration weak to begin with. At `b = 20` a corner's spike share climbs
from 0.54 to 0.71 and its win rate does **not** move — because the opponent is not beating it on
magnitude. It wins because the focused build has no mitigation, no shield and no dodge, and defensive
layers compose multiplicatively. **More Might does not fill an empty defensive layer.**

**Conclusion — a design constraint, not a tuning value:**

> **A focus build cannot be rescued with MAGNITUDE. It can only be rescued with MECHANISM.**
> Deep-tier passives must grant things the resolver does not otherwise have — *"a passive scaling damage
> with damage taken, a reflect build, an anti-turtle punish"* (class-system-map §4b) — not larger numbers
> on channels the build already maxes. A node that only adds magnitude is, for a focused build,
> measurably worthless.

This converges with three things already written down: the aptitude rule that *"an aptitude reaches a
MECHANISM, never a FLAVOUR"* with **65% of channels reserved for the skill layer**; Last Epoch's own
*"a single node in a tree can be like adding a whole new skill to the game"*; and the reason the closed
form cannot score these nodes — they are outside its saturating ratio math by construction.

**What it changes:**

| | |
|---|---|
| `Fmax` | Stays a **small nudge (1.15–1.25, D5 revised)**. The sweep shows it is not the lever; sizing it precisely is not worth further measurement |
| `b` | **Not a balance dial after all** — no value works, so it is a content-density choice, not a tuning one |
| **D13 generator** | Gains a hard requirement: the plan must distinguish **mechanism nodes from magnitude nodes**, and guarantee that deep tiers carry mechanisms. A generator that emits only magnitude scaling produces a tree that measurably does not work |
| Re-measure | Only worthwhile once mechanism nodes exist in the resolver. A magnitude-only sweep has now been done and is closed |

---

---

## 4. Structure

**Two unlock tracks per skill** (D3):

| Track | Currency | Shape | Buys |
|---|---|---|---|
| Unlock | Skill points | Discrete, finite (`skillPointsPerTheta`) | A **new bonus** |
| Deepen | Souls | **Unlimited**, arithmetic cost | **Bonus power scale** |

This is the shipped ladder pairing, and it earns a proven property. `ssot-power-scale.md` §10.5:

```text
cumulative cost   Σ(first + (k−1)·step) ≈ (step/2)·L²      quadratic
power at index    C + A·Θ + B·Θ(Θ−1)/2  ≈ (B/2)·Θ²         quadratic
                  ⇒ power ∝ total investment               LINEAR in effort
```

An hour of play buys the same absolute power at hour 5 and hour 500. Souls are therefore **uncapped by
design** (PS-8, "endless grind is the SSOT"), and the arithmetic cost is a **cost ladder** — explicitly
exempt from the one-ladder rule (§10 row 6). The *bonus* it buys is not exempt and must read `P(Θ)`.

**Tiers** (D10): every tree is 2 branches × tiers. A tier opens on (a) the tier below being unlocked, and
(b) the actor's own base allocation in that tree's gate quantity. **Both branches share one tier
requirement** — which is the pure-build discount: one investment opens offence *and* defence.

**Cross-unlock within a major category:** skill points spent in another tree of the same posture can
satisfy a tier requirement. This is a *second* concentration reward, on the cost side. It compounds with
`F`, and **both must sit inside the same closed form** or the combined effect goes unmeasured.

---

## 5. What this lands on that already exists

Nothing here needs a new vocabulary; four systems already carry the load.

| Need | Already shipped |
|---|---|
| 3 major categories × 4 stats | **Postures** FORCE / FINESSE / BASTION, twelve aptitudes (`spec-primary-stats.md` §2) |
| The share vector `H` reads | `share = points in aptitude / points across all aptitudes` (`spec-aptitude-tuning.md` §2.1) |
| "Tier gate ignores item bonuses" | **True by construction.** An aptitude is a SOURCE, not a channel (§3.1): items cannot feed aptitude points, because the share denominator is the actor's own total — `+5 Might` on an item *"would be a nerf to eleven other stats"* |
| Per-category budgets and gate quantities | `AllocationScope { Commander, DemonType, Aspect, UniqueDemon }` with a shipped per-scope rate table and **no caps** (`PointBudget.cs`) |
| The two-ladder grind property | `ssot-power-scale.md` §10.5 |
| A spender for skill points | `grant.skillPointsPerTheta: 1`, parsed, zero consumers |

**The scope alignment is close to one-to-one** and is the most promising answer to "what gates a
non-primary tree": Commander (`Θ_player`) → primary trees · Aspect (`element_mastery`) → elemental trees ·
DemonType (almanac XP) → demon family trees · UniqueDemon (specimen level) → demon species tree. Three of
four scopes ship today; Aspect *"lights up the moment a caller has a real value to pass"*.

⚠️ **Status trees have no scope and no gate quantity yet** — the one category this mapping does not cover.

---

## 6. Generation (D13–D16)

Order is the balance mechanism, not a preference:

1. **Deterministic plan** — math functions decide tree shape, tier ladder, unlock requirements, skill
   links, and **the potency ceiling each node may carry** (D13/R7).
2. **Property vocabulary** — the plan emits the closed set of properties (tags, conversion states,
   damage types) that D14's exclusions key on. **This must exist before any node text is written:**
   a generated corpus cannot maintain named-pair exclusions (O(n²) and unbounded as content grows),
   so a node can only ever exclude against a *property* that the plan already named.
3. **Distribution engine** — allocates the budget across trees at **equal expected value, not equal
   shape** (D15): each tree receives a budget *and* a shape archetype (broad-and-flat, spiked,
   gated-deep …), so trees stay balanced without becoming interchangeable.
4. **LLM pipelines** — fill vocabulary, category, atom effect pool and bonus *within* the plan's budget,
   its potency ceiling, and its property vocabulary.

This is the repo's binding **seed → concrete** principle applied to trees — but on the **shared-deterministic** side of it, never the per-player side. See §10 (D24): the catalog is committed content, and the only per-player thing is allocation. The
`seedsmith-design` skill must be loaded before this pipeline is specced — it carries the generation
principles and the failure modes this repo has already paid for.

> **Why step 2 is not optional.** Path of Exile and Last Epoch hand-author every node, so a designer can
> hold pairwise conflicts in their head. **No comparator generates a passive tree.** Generation is our
> largest departure from the genre, and property-keyed exclusion is the mechanism that makes it
> survivable — it is O(1) as content grows and covers nodes that do not exist yet.

---

## 7. Open — for the next rounds

**Closed since the first draft:** respec dependency (D18 — full reset dissolves it), status gate (D19),
tier threshold shape (D20), exclusion mechanism (D14).

### Owner decisions still owed

1. **Tree size** — skills per tree and tiers per branch. Reference point: Last Epoch ships ~29 nodes per
   tree, and ~50 trees at that density is ~1,450 generated nodes *per actor's catalog* (the catalog is
   shared; only allocation is per-actor — D21).
2. **§9's target distribution** — which corpus skew is legitimate theme (plants really are earthy) and
   which is LLM bias to be corrected.
3. **The species-tree pipeline (D23)** — its own generator, separate from the generic tree plan. Needs
   the `seedsmith-design` skill loaded before it is specced.

### Measurement-gated (solve, don't argue)

6. **`Fmax`** — sweep it against the dominance matrix. **Unblocked:** `DominanceGuard.Measure` already
   accepts an arbitrary build list, so hybrids need no guard change — only different builds passed in
   (`tools/DominanceBaseline/Program.cs` builds the 12 corners today).
7. **`w`** — points/souls blend weight (§3.2). Default 0.5 until swept.
8. **Node potency ceiling** (D13/R7) — the figure the plan enforces so no node forces a build.

### Engineering defaults (no decision needed unless objected to)

9. **Sparse soul state** — persist only non-zero `{skillId → soulLevel}` entries, so an actor's row grows
   with what they did, not with the content catalog.

## 8. Inherited constraints (non-negotiable)

- **One ladder.** Tree bonus power reads `P(Θ)`; a new power-shaped scale needs a reviewed row in
  `ssot-power-scale.md` §10 — *"a power-shaped number that is not in this table does not have permission
  to exist."*
- **No caps on magnitudes.** Souls are unlimited by design; absolute bounds throw, never clamp.
- **`long` for every magnitude**, widen before multiplying, overflow throws.
- **Balance numbers are config**, never `const` — `Fmax`, `w`, tier thresholds all live in `data/tuning/`.
- **Twelve is a measured outcome, not a decision** — the generator reads the aptitude roster, never
  hardcodes 12.

---

## 9. The diversity risk is measured, not hypothetical (D17)

The owner's stated fear — *"avoid LLM decide everything cause the game favour some primary stats and
element build, ignore others"* — **has already happened in the shipped demon corpus.** Species seeds
already carry `aptitudePrimary` / `aptitudeSecondary` / `elementPrimary` / `elementSecondary` / `posture`,
so the "build favour" D17 locks against exists today. Measured across **all 841 entries in 503 species
files** (2026-09-04):

| aptitudePrimary | count | share | | elementPrimary | count | share |
|---|---|---|---|---|---|---|
| Onslaught | 332 | **39.5%** | | earth | 379 | **45.1%** |
| Bulwark | 133 | 15.8% | | fire | 138 | 16.4% |
| Retribution | 113 | 13.4% | | light | 102 | 12.1% |
| Focus | 89 | 10.6% | | ice | 95 | 11.3% |
| Precision | 50 | 5.9% | | dark | 71 | 8.4% |
| Fortitude | 49 | 5.8% | | air | 56 | **6.7%** |
| Pierce | 25 | 3.0% | | | | |
| Agility | 19 | 2.3% | | **posture** | | |
| *unresolved* | 12 | 1.4% | | Force | 394 | 47% |
| Vigor | 7 | 0.8% | | Bastion | 298 | 35% |
| Might | 6 | 0.7% | | Finesse | 137 | 16% |
| Composure | 4 | 0.5% | | | | |
| Ferocity | 2 | **0.2%** | | | | |

Uniform would be 8.3% per aptitude and 16.7% per element. Actual spread is **Onslaught 332 : Ferocity 2 —
a 166× ratio** — and `earth` alone is 45%. Force posture outnumbers Finesse 2.9:1.

**Why this is load-bearing rather than cosmetic.** D17 makes a species tree *lock* a build favour. Locking
against this distribution means ~40% of all demons push Onslaught and ~45% push earth, while Ferocity,
Composure, Might and Vigor builds are collectively reachable through **2.2%** of the roster. The tree
layer would inherit and then amplify a skew produced by an earlier LLM pass.

**What the planner therefore owes** — this is more than "add a field":

1. **A target distribution**, declared as data, not implied by whatever the corpus happens to contain.
2. **Quota assignment before generation** — the planner decides how many species may carry each
   (primary, element, status) favour; the agent only chooses which of the *permitted* favours fits a given
   species thematically.
3. **A check gate** — the emitted corpus is validated against the target and fails loudly on drift, the
   same shape as the repo's existing `--check/--emit` distribution gates.
4. **A decision on legitimate skew:** plants genuinely are earthy, so some imbalance is theme rather than
   bias. The target distribution is where that judgement gets written down and argued once, instead of
   being re-litigated per species.

> **Corollary — a species' *thematic* favour and its *mechanical* lock need not be the same field.** If
> they are one field, thematic truth (plants are earthy) becomes mechanical skew (everyone plays earth).
> Decoupling them is the cheapest way to keep flavour honest and the build space even.

---

## 10. The catalog is content, not loot (D24)

Owner, 2026-09-05: *"the passive skills tree need concrete value before the game run… it is different
with item loot mechanism… it need solid stats, so user can learn it, if it random every new player
create, it will cause confuse, user cannot build because they need to relearn."*

**This is a real distinction, not a preference.** Loot and trees answer different questions:

| | Item loot | Passive tree |
|---|---|---|
| The player's question | *"what did I find?"* | *"what should I aim for?"* |
| Value of variation | The whole point — a roll you have not seen is the reward | **Negative** — a tree you have not seen is an obstacle |
| Knowledge earned | About *this instance* | About *the game* |
| If it rerolls per player | Still works | **Build guides become lies, and planning becomes impossible** |

A tree is a **map the player navigates**. A map redrawn for every traveller is not a map.

### 10.1 Where this sits with `seed → concrete → per-player`

The repo's binding generator principle is *seedsmith emits seeds; the runtime rolls concrete
per-player objects*. **D24 does not break it — it lands on the same split the demon pipeline already
uses**, which `DESIGN-GATE.md` §1 states directly: *"Species **stats** are deterministic and shared;
only **effects** roll, per player, at runtime."* There is already a shared-deterministic layer; the
tree catalog belongs to it.

So the three-stage generator (§6) is a **build-time authoring pipeline whose output is committed
data**, not a runtime roller. Determinism stops being a nice property and becomes the shipping
mechanism: the generator's job is to produce an artifact, and the artifact is the content.

### 10.2 What this changes

1. **The generator's output is reviewed, then committed.** Balance is checked once, against the real
   corpus, before players see it — rather than being an argument about expected values over rolls.
2. **Node id stability becomes load-bearing.** A saved allocation references node ids across
   regenerations. D18 (respec is a full reset priced in souls) makes the migration escape hatch cheap,
   but it does not remove the need for a rule.
3. **Learnability is an acceptance criterion.** A tree the player cannot read, preview and plan
   against has failed even if its math is perfect.
4. **Build sharing becomes possible** — and it is the payoff. Two players can compare builds only
   because they are looking at the same tree.

The exact freeze line — what is baked, what is per-actor state, what (if anything) still rolls —
is worked out in [../research/passive-tree/01-static-vs-rolled.md](../research/passive-tree/01-static-vs-rolled.md).

---

## 11. Enrichment round, 2026-09-05

Seven parallel investigations, each read → verified against code → written up. All findings live in
[../research/passive-tree/](../research/passive-tree/) with `file:line` citations and FACT / INFERENCE /
RECALL marking. This section is the index and the verdict; the detail is in the files.

| # | File | Answers |
|---|---|---|
| 01 | [static-vs-rolled](../research/passive-tree/01-static-vs-rolled.md) | Where the freeze line falls (D24) |
| 02 | [deterministic-planner](../research/passive-tree/02-deterministic-planner.md) | Stage 1 — topology, tier ladder, equal-value distribution |
| 03 | [llm-stage-contract](../research/passive-tree/03-llm-stage-contract.md) | Stage 2 — what the language stage may choose, and its 29 gates |
| 04 | [number-and-atom-binder](../research/passive-tree/04-number-and-atom-binder.md) | Stage 3 — concrete magnitudes and concrete atoms |
| 05 | [mechanism-taxonomy](../research/passive-tree/05-mechanism-taxonomy.md) | What §3.5 says is the only lever that works |
| 06 | [red-team](../research/passive-tree/06-red-team.md) | The holes, severity-ranked |
| 07 | [learnability-and-surface](../research/passive-tree/07-learnability-and-surface.md) | D24's other half — the player must be able to learn it |

### 11.1 Settled by evidence

- **D24 costs nothing to hold.** `spec-container-schema.md:50-56`'s 2026-09-01 amendment made
  `species-passive` roll and **deliberately left `skill` containers using the core alone**. A fixed core
  with no pool is already the contract; something would have to change to *lose* it.
- **Equal expected value is free.** `tierBudget[t] = B_b·t/T_tri` sums to `B_b` identically — the shape
  archetype `w[t]` never appears in the sum. D15/R6 is a property of D20, not extra machinery.
- **The binder emits a COEFFICIENT, not a magnitude.** One static catalog is then correct at every `Θ`
  for every player, and the read path already ships (`AtomCompiler.cs:463-464`).
- **The soul track needs exactly one new §10 row** — the soul→`Θ` weight, by row 18's precedent.
  `Θ_node = Θ_actor + Ws·soulLevel`; the coefficient never moves. Scaling the coefficient instead would
  give power ∝ √effort, which is §4's claim failing (red team F9, now answered).
- **Standalone-first holds.** Nothing in D1–D24 reads a Unity field.
- **Mechanism nodes ARE measurable** — `tools/CombatSim` and `BattleEngine` already drive the real
  resolver. Red team F2 is a **wiring gap**, not the architectural wall it looked like.

### 11.2 Broken, and now known

| | Finding | Fix |
|---|---|---|
| ✅ | ~~**Cross-unlock rewards BREADTH, not focus**~~ — **MEASURED 2026-09-05 and REVERSED.** `--crossunlock` says it is a **concentration** reward, as §4 always claimed: a pure Might build is a *Force* build, so its Fortitude/Vigor/Onslaught gates are satisfied by points it already spent and its **whole posture comes along free**. The four-of-one-posture build gets 0.62–0.69× a pure build's tree power, not 10.2×. The red team credited only invested trees; the advantage lives in the eleven floored ones | **Adopt the largest-mate rule (D28)** — the only candidate where a corner beats spread |
| ⛔ | **D9 is not executable.** `family` is an OPEN axis (`spec-anchor-contract.md:58`), **699 distinct tokens** over 841 entries against `spec-roster-metrics.md:38`'s expected 19. Three agents found this independently | Curate a closed family roster, or amend D9 |
| ⛔ | **D14 is decorative today.** Atom tags are free-form JSON with no vocabulary (`AtomRow.cs:40`), so a property-keyed exclusion can key on posture and nothing else. §6 step 2 makes this critical-path | Land `spec-eligibility-tags.md`'s derived-tag registry first |
| ⛔ | **D16 conversion nodes are a real capability gap**, not a wiring one — no kind among the 16 writes an element payload, and the failure is silent (`OverlayCombatCalculator.cs:128-172`) | Allocate no budget to conversion nodes until a 17th kind is reviewed |
| ⛔ | **D19's fifth scope collides with a live gate** — `AptitudeAffixPrice.cs:32` branches on `> 4`, `item-ideal.md:1443` books slot 5, and `AptitudeAllocation.Single:38` cannot name a status | Re-scope D19 |
| ⛔ | **Breadth is not bounded.** At `skillPointsPerTheta: 1` with `Θ` uncapped, the whole catalog unlocks at `Θ≈1,450` | Needs a non-cap mechanism (PS-8 forbids the LE answer) |
| ⚠ | **`PowerLadderKMilli` is per-mille** — a tier-1 node rounds with **17% error**, larger than one tier step, destroying D20's linear per-tier power | Per-million sibling; three lines |
| ⚠ | **Migration fails hard.** `AptitudeAllocation.cs:38` throws on an unknown id, per-row from `RpgStore.Aptitudes.cs:129`. At 1,450 nodes one retired id makes an actor **unloadable** rather than red | Reject once at an import boundary, never lazily per load |
| ⚠ | **D18 contradicts `decisions.md:103`** (souls are a fighting *faucet*), and respec is free today — `RespecPolicy` has zero callers | Re-price |
| ⚠ | **D20's tier 1 is a 2× worse deal** — `W/req` is `0.100·b` against a `0.200·b` asymptote, because req indexes `t(t−1)/2` while power indexes `t(t+1)/2` | Keep the entry tax deliberately, or use `req'(t)=5·t(t+1)/2` |

### 11.3 Owner decisions this round created

1. **Cross-unlock** — remove it, bound it, or fold it into the closed form and re-sweep. It is
   currently the single largest unmeasured term, and it pushes against D4–D7.
2. **The family roster** — curate 699 tokens to a closed set, or drop demon-family trees from D9.
3. **Tier-1 entry tax** — deliberate, or corrected to constant reward-per-point.
4. **Content volume** — 29 nodes/tree × D23 at 841 species is ~45,800 authored nodes. 4 unique nodes
   per species is ~5,046 generation calls (~4.3 h); 29 is ~24,389, larger than the whole demon
   classification run. This is a cost decision, not a technical one.
5. **Breadth bound** — what stops the catalog fully unlocking, given PS-8 forbids a cap.
