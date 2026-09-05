# Passive skill trees — the ideal

**Status:** idea phase, opened 2026-09-04. **Not a spec. No build authorized.** 36 owner decisions, 16 research documents (`../research/passive-tree/`), four measured results. §11 and §12 record the enrichment and strengthening rounds; **§11.4 is the live open list**. **Idea phase CLOSED 2026-09-05**; `/spec` opened the same day. D33 is no longer a gate — the squad harness is the first module built, and the numbers it settles are tunables (§14), not spec preconditions.
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

## 2. Decisions locked by the owner (2026-09-04 → 2026-09-05)

| # | Decision | Consequence |
|---|---|---|
| D1 | **Free build stays.** No player class; classes remain Zomboss patterns | Confirms the 2026-08-25 correction; trees add identity without adding a class container |
| D2 | **All four acquisition sources**: skill points · aptitude thresholds · items/affixes · demon aspect | The `skillPointsPerTheta: 1` grant, minted since 2026-08-26 with **zero consumers**, finally has a spender |
| D3 | **Every skill has two unlock tracks**: skill points unlock *new bonuses* (discrete); souls scale *bonus power* (unlimited, arithmetic cost) | Matches the shipped two-ladder economy exactly — see §4 |
| D4 | **Concentration is rewarded by a bounded Herfindahl multiplier** | `F = 1 + (Fmax−1)·H`, `H = Σ(shareᵢ)²` — see §3 |
| D5 | ~~`Fmax = 1.5`~~ → **revised 2026-09-04 to a small nudge, `Fmax` 1.15–1.25, tunable** | §3.5's sweep showed no `Fmax` reverses the concentration penalty, so the multiplier is not the lever — deep-tier MECHANISM nodes are. Keeping it small stops focus being strictly worse on average without pretending it fixes the gap **2026-09-05: retained provisionally, pending a D25-inclusive sweep.** [11](../research/passive-tree/11-adversarial-debate.md) argues `F` is machinery §3.5 measured as doing nothing, carried at the cost of two tunables, a UI acceptance criterion and D8's exploitable self-spent rule. But **every measurement of `F` ran without D25**, which is now the one mechanism shown to move the ordering (2.3×). Whether they compose or `F` is redundant beside it is unmeasured |
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
| D23 | **A demon species tree's reward is a UNIQUE tree** — nodes no other tree has, with **its own generation pipeline** (owner: *"better to spend effort for it now, maybe deploy agent to enrich it"*) | The strongest identity/collection pull, and the honest price for D17's lock: you give up build freedom and receive something unobtainable elsewhere. Cost is real — per-species authored content at 840-entry scale — which is why it gets a pipeline of its own rather than riding the generic tree generator |
| D28 | **Cross-unlock credits ONE tree — your largest posture-mate — never a sum** (measured, 2026-09-05) | The sweep (`--crossunlock`, [09](../research/passive-tree/09-crossunlock-sweep.md)) is the first result in this program where **a corner beats spread**: 49.9% vs 47.7%, against 43.4% vs 54.4% with cross-unlock off. Crediting the full sum also flips the ordering but compresses every build into 48.6–51.8% — a rule that gives everyone everything stops discriminating. One mate is O(1), explainable, and **bounded by construction**: no k-way build can compound it **Bounded 2026-09-05 by re-measurement under D26+D29** ([16](../research/passive-tree/16-depth-exhaustion.md)): the concentration reward holds to `Θ ≈ 300` and **inverts above it**, because every build saturates D29's 10 authored tiers and tree power equalises at 1.00×. The original sweep ran at `Θ=100`, where the cap is inert, so it could not have seen this |
| D33 | ~~Squad-scope re-measurement **gates** every further balance decision~~ → **amended 2026-09-05: NOT a gate.** The harness is the first module built (`squad-harness`), and the balance numbers it settles are **tunables**, resolved after the spec rather than before it (owner: *"we make spec and build, tunable later, remove this gate"*). The finding stands and is what the module exists to answer | ⛔ **Every balance number in this program is a 1v1 duel.** `DominanceGuard` predicts per ordered pair (`DominanceGuard.cs:55`); the game fields six (`WebMatchService.cs:339`, `const int maxSquad = 6`); D21 gives each its own tree, share vector and `F`. **"Squad" appeared zero times in this document.** Six pure corners collect 6× `Fmax` while the SQUAD covers every layer breadth was meant to buy — the risk half of *"become stronger but become weaker too"* is paid by the actor and absorbed by the squad. Generalises red-team F4 one level up: **`H` measures commitment in a scope narrower than the scope at which power is delivered, and any such gap is arbitrage** |
| D34 | **`skillPointsPerTheta` becomes per-scope** | Required by D25, not optional: it is a single scalar (`AptitudeTuning.cs:13`) while aptitude points already ship a four-scope table. If every actor reads `Θ_player`, **50 demons × 31 nodes at Θ=100 is 1,550 — the whole catalog — and D25's bound breaks outright** |
| D35 | **Status trees gate on their OWN quantity, outside `AllocationScope`** (owner, 2026-09-05, replacing D31) | `AllocationScope` is the set whose members are summed into aptitude shares (`AptitudeAllocation.cs:51-57`). Status mastery is not aptitude points, so it does not belong in that enum at any slot number. Gate on times-the-status-was-applied. **No shipped code changes**, and the unowned slot-5 dependency disappears |
| D36 | **D25's curve is specified**: the Nth node costs `first + (N−1)·step`, `first = 5`, `step = 2`, with `skillPointsPerTheta` 1 → 11 | Derived, not guessed (`first = step·(k+1)/2`, `g = 3·s·step·k²/5`), and it reproduces D29's own two calibration points exactly. **Measured 2.3× concentration reversal — larger than anything `Fmax` or `b` achieved** — and it does NOT break D26's flatness, because it prices a different currency. Needs one new §10.2 row, which `guard-power.ps1` **cannot catch the absence of** (G2/G3 key on a `level`-named parameter; `nodesOwned` sits in `DropVolume`'s blind spot). [12](../research/passive-tree/12-rising-unlock-cost.md) |
| D29 | **Tree shape is 10 tiers × 2 branches, 40 nodes per tree** — **20 per branch, ROOTLESS** (owner, 2026-09-05; exact figure resolved by `spec-tree-plan.md`) | Deeper than Last Epoch's ~29. Extends D26's ladder to `req(t) = 5·t(t+1)/2` → 5 · 15 · 30 · 50 · 75 · 105 · 140 · 180 · 225 · 275. Tier 7 is where an all-in build sits at `Θ=100`; **tier 10 lands at `Θ≈170`** (computed: an all-in build holds ~0.542 of the share vector at 3 points/Θ, so `1.626·Θ ≥ 275`), so the deepest tiers are late-game targets rather than dead content — which is the correct shape under PS-8 (endless grind), where content must keep existing above the current ceiling. Generic corpus: **39 × 40 = 1,560 nodes**. It also rhymes with the two other ten-step ladders now shipped, the rarity rungs and D-star's ten stars |
| D30 | **Every species gets a FULL 29-node unique tree** (owner, 2026-09-05) | The maximum-identity reading of D23. **~24,389 generation calls and ~24,000 nodes across 841 species** — larger than the entire demon classification run, and the largest single content commitment in the program. **The generation is not the cost; the REVIEW is**, because D24 requires the catalog be reviewed before it ships. This is why D23 already gives it a pipeline of its own: it needs batching, sampling gates and a distribution check that a human can audit without reading 24,000 nodes **Amended 2026-09-05: a species tree is 40 nodes too, not 29** — D10's one-shape rule wins. Corpus becomes **35,200 nodes / ~100,800 calls**, a 38% machine-time increase that costs **nothing in review**, because [13](../research/passive-tree/13-review-pipeline.md) measured review as scaling with TREES, not nodes (a 40-node card and a 29-node card are the same look). Three agents found the 29-vs-40 contradiction independently |
| D31 | **`status_mastery` takes `AllocationScope` slot 6, after the item program takes 5** (owner, 2026-09-05) | Keeps D19's shape and accepts the dependency rather than working around it. Two things follow and must be tracked: the status tree **cannot be built until the item program lands its fifth scope** (`item-ideal.md:1443`), and `AptitudeAllocation.Single` (`:38`) still rejects any id that is not one of the twelve aptitudes — so it needs a second, separate change before a scope can name a status. `AptitudeAffixPrice.cs:32`'s `> 4` branch resolves itself once slot 5 exists ⛔ **SUPERSEDED 2026-09-05 by D35.** Both premises I gave for it were false: nothing reads the enum ordinal (`scope_key` is TEXT), and slot 5 is scheduled by no program — `item-ideal.md:1443` sits under *"Needs another program"* and names a different owner than `item-todo.md:2857` files it under. The real collision is `AptitudeAllocation.Total()` summing **every** enum member into the aptitude share denominator `decisions.md:103` locks, so slot 5 and slot 6 are identically broken |
| D32 | **Target distribution is near-uniform with a NAMED theme allowance** (owner, 2026-09-05) | Uniform is the target (8.3% per aptitude, 16.7% per element); a small explicit per-axis exception is declared as data — `earth` may run to roughly 1.5× uniform because plants really are earthy — with everything else held inside a stated band. The judgement §9 demanded gets **argued once, in a file**, instead of re-litigated per species. Lives in `data/tuning/passive-tree-targets.v1.json`; the check gate fails loudly on drift |
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

> **§13 is the full three-bucket inventory** (Built / wiring gap / real gap) with `file:line`.
> This section is the shorter *"nothing here needs a new vocabulary"* argument.

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

## 7. Open — for the next rounds *(SUPERSEDED — see §11.4)*

> **Superseded 2026-09-05.** Every item below was answered or reclassified across the two
> rounds. **§11.4 is the live list.** Kept for the reasoning trail, per this repo's habit of marking
> superseded rather than deleting.

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
| Precision | 50 | 5.9% | | dark | 70 | 8.3% |
| Fortitude | 49 | 5.8% | | air | 56 | **6.7%** |
| Pierce | 25 | 3.0% | | | | |
| Agility | 19 | 2.3% | | **posture** | | |
| *unresolved* | 11 | 1.3% | | Force | 394 | 47% |
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
| ⛔ | **D9 is not executable.** `family` is an OPEN axis (`spec-anchor-contract.md:58`), **698 distinct tokens** (the 699 figure counted the stale hidden duplicate) over 841 entries against `spec-roster-metrics.md:38`'s expected 19. Three agents found this independently | Curate a closed family roster, or amend D9 |
| ⛔ | **D14 is decorative today.** Atom tags are free-form JSON with no vocabulary (`AtomRow.cs:40`), so a property-keyed exclusion can key on posture and nothing else. §6 step 2 makes this critical-path | Land `spec-eligibility-tags.md`'s derived-tag registry first |
| ⛔ | **D16 conversion nodes are a real capability gap**, not a wiring one — no kind among the 16 writes an element payload, and the failure is silent (`OverlayCombatCalculator.cs:128-172`) | Allocate no budget to conversion nodes until a 17th kind is reviewed |
| ⛔ | **D19's fifth scope collides with a live gate** — `AptitudeAffixPrice.cs:32` branches on `> 4`, `item-ideal.md:1443` books slot 5, and `AptitudeAllocation.Single:38` cannot name a status | Re-scope D19 |
| ✅ | ~~**Breadth is not bounded**~~ — **CLOSED by D25.** At `skillPointsPerTheta: 1` with `Θ` uncapped the whole catalog unlocked at `Θ≈1,450`. Unlock cost now rises with the number of nodes an actor already owns — a soft economic bound, so PS-8 is satisfied and nothing is ever refused | **Done (D25)** |
| ⛔ | **The tier gate reads FOUR incommensurable quantities at one threshold.** `req(6)=105` would mean aptitude points, specimen levels, element mastery and almanac XP interchangeably — but they grow at different exponents, and the per-scope rate table `{3,4,4,6}` MULTIPLIES sources, so it cannot equalise two different growth curves. **Half-closed 2026-09-05:** specimen levels now read the shared arithmetic curve (effort-power M1), so they and aptitude points finally share a shape. `element_mastery` and almanac XP still have **zero `src/` hits** | Gate on ONE index; the other three convert INTO it, never sit beside it |
| ⚠ | **`PowerLadderKMilli` is per-mille** — a tier-1 node rounds with **17% error**, larger than one tier step, destroying D20's linear per-tier power | Per-million sibling; three lines |
| ⚠ | **Migration fails hard.** `AptitudeAllocation.cs:38` throws on an unknown id, per-row from `RpgStore.Aptitudes.cs:129`. At 1,450 nodes one retired id makes an actor **unloadable** rather than red | Reject once at an import boundary, never lazily per load |
| ⚠ | **D18 contradicts `decisions.md:103`** (souls are a fighting *faucet*), and respec is free today — `RespecPolicy` has zero callers | Re-price |
| ✅ | ~~**D20's tier 1 is a 2× worse deal**~~ — **CLOSED by D26.** The cause was two ladders indexed differently: `req` on `t(t−1)/2`, power on `t(t+1)/2`. Sharing one index makes reward-per-point `b/5` at **every** tier, exactly rather than approximately | **Done (D26)** — 5 · 15 · 30 · 50 · 75 · 105 · 140 |

### 11.3 Owner decisions this round created — all closed 2026-09-05

| # | Question | Answer |
|---|---|---|
| 1 | Cross-unlock | **Measured, not argued.** It rewards concentration after all; credit one mate (D28) |
| 2 | The family roster | Ship the roster whole; curation is a build-order task (D27) |
| 3 | Tier-1 entry tax | Reconcile the indices instead — reward-per-point is now exactly flat (D26) |
| 4 | Content volume | Full 29-node unique tree per species (D30) |
| 5 | Breadth bound | Rising unlock cost per node owned (D25) |
| 6 | Tree size | 10 tiers × 2 branches, ~40 nodes (D29) |
| 7 | Status tree gate | `AllocationScope` slot 6, after items (D31) |
| 8 | Target distribution | Near-uniform with a named theme allowance (D32) |

**Total authored corpus: ~1,560 generic nodes + ~24,389 species nodes ≈ 25,900.** That is the number
the generation pipeline and the review gate both have to survive, and it is now a decided figure
rather than an open range.

### 11.4 What is still genuinely open

Only two, and neither blocks specification:

1. **`w`** — the points/souls blend weight in `H` (§3.2). **Promoted 2026-09-05 from a minor measurement to a PRIMARY design parameter** ([16](../research/passive-tree/16-depth-exhaustion.md)). Past `Θ≈300` the point track is exhausted for every build, so `H_points` is identical for everyone and only `(1−w)·H_souls` can still tell builds apart. **At `w = 1` the design has no late game.** The default of 0.5 was chosen when this was thought to be a tuning nicety.

2. **Node potency ceiling** — the figure the plan enforces so no node forces a build (R7). Falls out
   of the budget math once tree size is fixed, which D29 just did.

**Blocked on other programs, tracked not open:** D14 needs `spec-eligibility-tags.md`'s derived-tag
registry before property-keyed exclusion can key on anything but posture. D16 needs a 17th atom kind
(a reviewed `decisions.md` change) before conversion nodes can carry budget. D31 needs the item
program's fifth `AllocationScope`.

---

## 12. Strengthening round, 2026-09-05

Six parallel investigations against the locked decision set, plus one re-measurement. Findings in
[../research/passive-tree/](../research/passive-tree/) docs 10–16, all `file:line` cited.

| # | File | What it did |
|---|---|---|
| 10 | [decision-consistency-audit](../research/passive-tree/10-decision-consistency-audit.md) | 18 findings across D1–D32; refuted 6 of my 8 suspicions |
| 11 | [adversarial-debate](../research/passive-tree/11-adversarial-debate.md) | Six theses attacked; **found the squad-scope defect (D33)** |
| 12 | [rising-unlock-cost](../research/passive-tree/12-rising-unlock-cost.md) | Specified D25 → D36; measured a 2.3× reversal |
| 13 | [review-pipeline](../research/passive-tree/13-review-pipeline.md) | Review scales with trees, not nodes; D30 is affordable |
| 14 | [learnability-at-scale](../research/passive-tree/14-learnability-at-scale.md) | A species tree is not a choice, so it needs no chooser |
| 15 | [dependency-map](../research/passive-tree/15-dependency-map.md) | Build order; **three pieces of required work are UNOWNED** |
| 16 | [depth-exhaustion](../research/passive-tree/16-depth-exhaustion.md) | The concentration reward expires at `Θ ≈ 300` |

### 12.1 The two findings that outrank the rest

**Scope mismatch (D33).** Every number in this program is 1v1; the game is six-a-side. This is not a
refinement — it is the possibility that `F`, `Fmax`, hybrid neutrality, D28 and *"magnitude cannot
rescue focus"* all rest on the wrong unit of analysis. Measuring it is cheap and it gates the rest.

**Depth exhaustion (§16).** Any finite authored depth saturates under PS-8's endless `Θ`. Ten tiers
only decides *when* — measured at `Θ ≈ 300`. Past it the point track is identical for every build, so
**all late-game differentiation must come from the soul track**, which promotes `w` from a tuning
nicety to the parameter the whole late game hangs on.

Both share one shape, and it is worth naming because it will recur:

> **A measurement is only as good as the scope it was taken in.** One found a scope too narrow in
> *space* (one actor, six deployed); the other a scope too narrow in *range* (`Θ=100`, endless
> ladder). Neither was a wrong calculation.

### 12.2 Corrected by this round

- **My own framing was wrong** on doc 09: it did *not* run on D20's superseded ladder, and never
  assumed 7 tiers. D26 supplies four of its eight rows and both tier functions are unbounded loops.
  D29's cap is provably inert at `Θ=100` (`req(10)=275 ≤ 300 < 330`). What doc 09 lacked was
  **high-`Θ` coverage** — corrected in place in §16.
- **What actually unsupports doc 09 is D25**, not the ladder: every sweep sets `W = b·T(T+1)/2`,
  assuming you own every node up to your tier, while D25 makes you own `O(√Θ)` of them. D28 reads
  *measured, pending a D25 re-run* — not closed.
- **D14 is a SOFT blocker, not hard.** `ep-8 eligibility-tags` is complete and `AffixTags.cs` ships
  (124 lines, tested). Missing: a call site and a vocabulary — the corpus carries exactly **3**
  semantic values.
- **Battle does fire `OnDamageDealt`** (`BasicAttack.cs:176`) — it is `partial class BattleEngine` in
  another folder, so doc 05's Battle-folder grep missed it. **On-hit mechanism nodes are measurable
  today.**
- **The unlock-everything threshold is 1,560, not `Θ≈1,450`** — the old figure was 50 trees × 29.
- **840 species are indexed, not 841.** The 841st is a stale duplicate of `SnorkleZombie` in
  `zombie/_needs-review.json`, hidden from the quality tool by its `_`-prefix skip
  (`DemonQualityReport/Program.cs:77`) — the same defect class Phase A2 fixed 217 times, surviving
  inside the blind spot of the tool that then reported "0 duplicates".

### 12.3 Unowned work — nothing schedules these

1. **The fifth `AllocationScope`.** Three programs each point at a different owner. **D35 removes this
   dependency entirely**, which is the main reason it was worth taking.
2. **Three `ssot-power-scale.md` §10 rows** (`req(t)`, `W(T)`, soul→`Ws`), against a program with zero
   open tasks. D36 adds a fourth.
3. **ep-11 / ep-12** (`affix-power-class`, `affix-channel-weights`) — specced 2026-09-03, in no task
   list, and the only named call site for the `AffixTags` code that already shipped.

### 12.4 Build order

**Wave 0** — three §10 rows · `PowerLadderKMicro` (per-mille rounds a tier-1 node with **17%** error,
larger than one tier step) · the import-boundary migration fix · **the squad-scope harness (D33)**.
**Wave 1** — the four mechanism wirings, critical path first: **B4a, a fourth `IActorStatSubsystem`**,
~90 lines by the shipped `AtomDerivedSubsystem` precedent, which unblocks Erosion, layer parity and
conditional scaling at once.
**Then** the twelve primary trees, then one wave per gate quantity as it lands.

---

## 13. What already exists — the three buckets

The idea phase's most valuable output, and the one this document was missing until 2026-09-05. Sorted
by the only distinction that matters when planning: **can it run today, is it inert, or does it not
exist.**

> **A wiring gap is not a wall.** An inert path — a default-off toggle, a null delegate, a debug-only
> entry, a built API with no production caller — is unfinished wiring, not an architectural limit.
> This repo has already paid for that confusion once: an aura design read the injector's Unity write
> surface, found four inert paths, and concluded the feature could reach 5 of 12 aptitudes. The real
> answer was 11 of 12, and every one of the four was wiring.

### 13.1 Built — works end to end today

| Capability | Evidence |
|---|---|
| The power ladder `P(Θ)` and its exact-integer read | `PowerLadder.ValueMilli`; `AtomCompiler.cs:463-464` already widens before multiplying, divides once, throws on overflow |
| 12 aptitudes in 3 postures | `Aptitude.cs:36-51` — the count is *computed* (`PostureCount × PerPosture`), never typed |
| 6 elements · 21 statuses | `ElementTable.cs:125-130` · `StatusCatalogBootstrap.cs:16-58` |
| The share vector `H` reads | `AptitudeAllocation.Share`, summed over scopes at `:51-57` |
| Balance is *measurable*, not arguable | `DominanceGuard.Measure` accepts an arbitrary build list — `tools/HybridViability` needed no guard change |
| Per-scope point budgets, uncapped | `PointBudget.PointsFor` |
| The soul track reads a **curve**, not a formula | `CurveTable.cs:4-9` — *"scaling is a curve reference, never a formula"*. This is what lets a player see level 9's value before buying level 1 |
| The atom vocabulary | `AtomKindRegistry.cs:21,31,36` — **7** attach points, **16** kinds, **13** triggers |
| The rising-cost precedent, already in player words | `ContractPolicy.NextSlotPrice:176-177`, rendered at `contractView.ts:50-54`. **D25 is not a new mechanic to the player** |
| A `skill` container does not roll | `spec-container-schema.md:50-56` — the 2026-09-01 amendment made `species-passive` roll and **deliberately left `skill` on the core alone**. D24 is the shipped contract, not a new ask |
| Shared-deterministic generated content, on disk | `data/generated/demons/` — 830 committed concrete files |
| Reflect — **on the lawn only** | `CombatDamageDispatcher.TryReflect`, reachable from `DispatchInstant`. **Corrected 2026-09-05:** this row previously cited `EffectRuntime.cs:491`, which is the *ShieldGate* wiring, not reflect. **Battle does NOT reflect** — it applies HP through `DamageApplyPipeline.Apply` (`BattleRunState.cs:465`), a different path. So doc 05's M7 Retaliation is **not measurable at squad scope** despite being ranked "ship content today" |
| Threshold triggers | `PredicateNode.cs:26` (`:24` is `ActorIsKiller`) + `FactReader.cs:71`, with `OnDamageTaken` firing on the lawn |
| Battle fires `OnDamageDealt` | `BasicAttack.cs:176` — it is `partial class BattleEngine` in another folder, which is why a Battle-folder grep misses it. **On-hit mechanism nodes are measurable today** |
| A simulator on the real dispatcher | `tools/CombatSim/Simulator.cs:59` |
| Eligibility tags | `AffixTags.cs` — 124 lines, tested; `ep-8` complete |
| The player surface's reserved slot | `PassivesTab.tsx:12-20`, a locked placeholder |
| A grant with no spender, waiting for this program | `grant.skillPointsPerTheta: 1`, parsed, zero consumers since 2026-08-26 |

### 13.2 Wiring gap — the machinery exists and is inert

Each row names the **specific inert line**. None of these is a wall.

| Gap | The inert line | Size |
|---|---|---|
| **A status's derived-channel write never composes** — the critical path for mechanism nodes | `ActorHub.cs:145,148,155` registers exactly three subsystems; status mods are upserted to the *primary* bag at `EffectRuntime.cs:81`, which none of the three reads | ~90 lines, by the shipped `AtomDerivedSubsystem` precedent |
| `stat.derived` is unscored in Sim, so the sweep cannot see the node class §3.5 prescribes | `AtomKindRegistry.cs:534` — `RuntimeState.None` | M |
| Battle's derived recompose runs once, at construction | `BattleRunState.RecomposeDerived` — **cite by SYMBOL, not line**: another program moved it `:313 → :323` during this session | S |
| `AffixTags` has no production call site | `EligibilityRule.cs:30-95`, callers = its own tests | S |
| `Instantiator` / `TryInstantiate` built but unreached | awaiting `effect-pipeline` module 4 | — |
| `PowerLadderKMilli` is **per-mille**, so a tier-1 node rounds with 17% error — larger than one tier step | `ValueSpec.cs:92` | 3 lines |
| **Two** of four `AllocationScope`s unreached — **corrected 2026-09-05**, this row previously said three | `DemonType` is wired END TO END: `SpeciesAllocation.cs:35,62` is a real producer, plus the tuning row and the store round-trip. `Aspect` and `UniqueDemon` have only the tuning row (`AptitudeTuning.cs:203-204`) and the scope-key round-trip (`RpgStore.Aptitudes.cs:57-58,66-67`) — no producer, no save, no load | S |
| ~~`RespecPolicy.PriceOf` returns Hunger against D18's souls, zero callers~~ — **BOTH WRONG, corrected 2026-09-05.** It returns `RespecResource.Soul` (`RespecPolicy.cs:46`; `:15` records Hunger as the PRIOR value) and it has **two** production callers (`RpgStore.SpeciesRespec.cs:154`, `SpeciesBuildEndpoints.cs:90`). D18 and the shipped code AGREE | — |
| Battle raises no `OnDamageTaken` / `OnSpawn` / `OnDeath` (it *does* raise `OnDamageDealt`) | `src/FusionRpg.Core/Battle/` | M |
| Two of the four gate quantities do not exist | `element_mastery` and almanac XP have **zero `src/` hits** | M |

> **Removed from this table 2026-09-05.** `stat.derived` declaring `AtomTriggers.None` (`AtomKindRegistry.cs:535`) was listed here as a wiring gap. It is **not one — it is a law.** `definitions.md` §14.2, which wins over any spec, states that authoring a trigger on it is `TriggerNotAllowed`, enforced at `AtomKindRegistry.cs:467` and pinned by three test assertions. Conditional scaling arrives through the status path instead (`status.apply` on `OnDamageTaken` carrying a `ModifyStat` payload), which is what the first row of this table unblocks. Widening it is a reviewed `decisions.md` + `definitions.md` change, not a fix.

### 13.3 Real gap — no mechanism exists anywhere

| Gap | Why it is real, not wiring |
|---|---|
| **Element-payload conversion (D16)** | **No kind among the 16 writes an element payload**, and the failure is silent — `OverlayCombatCalculator.cs:128-172` loops the payload's own components, so an ice affix on a payload with no ice component contributes zero forever, with no error. Needs a 17th kind, a reviewed `decisions.md` change. **Allocate no budget to conversion nodes until it lands** |
| **Layer denial / bypass** — **PARTIALLY AVAILABLE, corrected 2026-09-05** | This row said every "break their X" is a saturating contest that provably never reaches zero. That is true of **shred**, which goes through `ClampedContest.Apply` (`OverlayCombatCalculator.cs:255,260`) — and false of **parry break** and **block break**, which are plain subtraction floored at zero (`:183-184`, `Math.Max(0.0, ParryRate − ParryBreak)`). **Of six shipped defensive layers, two are switches and four are dials** (shred, `shield.pen` under `PenCapKPm`, `PierceFactor`, the category resist cap). This matters: doc 05's M3 anti-turtle mechanism is built on exactly the two that work, so `tree-plan` may budget for it |
| **Squad-scope balance measurement (D33)** | `DominanceGuard` is pairwise *by type signature*, not merely by coverage |
| **The soul track in the balance model** | `tools/HybridViability` models tier power only. Above `Θ≈300` that is precisely the half that has stopped growing (§16) |
| **A closed `family` roster** | `family` is an OPEN axis (`spec-anchor-contract.md:58`); 699 distinct tokens across 841 entries |
| **An atom-tag vocabulary** | Tags are free-form JSON (`AtomRow.cs:40`); the corpus carries exactly 3 semantic values. Until this exists, D14's property-keyed exclusion can key on posture and nothing else |

---

### 13.4 ⛔ The gate quantity does not exist for 27 of 39 trees

**Found by the spec-coverage audit, 2026-09-05, and verified in code.** A tier gate reads a *gate quantity*. Three of the four named ones are not there:

| Tree category | Trees | Gate quantity | State |
|---|---|---|---|
| Primary | 12 | aptitude points, `Commander` scope | ✅ **shipped and wired** |
| Elemental | 6 | `element_mastery` | ⛔ comments only — `PointBudget.cs:15` says outright it *"is owned by the demon program's `aspect-scope` module and does not exist yet"* |
| Status | 21 | `status_applied.<id>` | ⛔ **zero hits in `src/`**. D35 correctly removed the `AllocationScope` dependency — and removed the only place the counter was going to live, with nothing replacing it |

> **1,080 of the 1,560 generic nodes — 69% — would ship authored, reviewed, committed and permanently at tier 0.** Only the 12 primary trees (480 nodes, 31%) are reachable today.

This does not break the design; the build order already says *"one wave per gate quantity as it lands."* What it changes is **when content may be generated**: generating the elemental and status corpus before their gate quantities exist buys 1,080 nodes nobody can reach.

**Species trees are a different, lesser problem — and the difference is the point.** Specimen level is **live and persisted**: `rpg_unique_actors.level` (`RpgStore.cs:398`), written by `AwardUniqueActorXpUnlocked` levelling on the shared `RpgXpCurve`, surfaced as `UniqueActorDto.Level`. The scope is declared and rate-loaded. **Only the caller is absent** — verified by counting: every site reaching `PointBudget` passes `Commander` (2) or `DemonType` (4), never `UniqueDemon`. That is a **wiring gap** with a shipped twin to copy (`SpeciesAllocation.cs:35,62` is the `DemonType` version of exactly what is needed). `element_mastery` and `status_applied` are not gaps of that kind — **nobody has designed those counters at all.** So §13.4's 69% figure is about the 39 GENERIC trees; the 840 species trees are not blocked by it.

### 13.5 ⛔ D2's fourth acquisition source has no carrier

D2 promises four sources. **`skillPoint` appears ONCE in all of `src/`** — `AptitudeTuning.cs:158`, the line that parses the tuning key. There is no channel, no `UnitClass` and no store column that can carry *"grants N skill points"*, and `tree-binder` refuses `AptitudePoints` with no equivalent among the 13 classes. Four specs write requirements assuming that carrier exists. **Skill points are specified end to end; the other three sources are a rule with no mechanism.**

## 14. Tunables — every number this introduces

`tunables-ssot.md` is binding: **a number a balance pass would change lives in
`data/tuning/<domain>.v{n}.json`, never as a `const`.** A structural constant stays in code *and says
why it is not tunable*. The test is one question — *would a balance pass ever want to change this?*

**Proposed home for this program's own numbers: `data/tuning/passive-tree.v1.json`.**

| Number | Unit | Value / default | Home | Status |
|---|---|---|---|---|
| `concentration.fmax` | multiplier | 1.15–1.25 (D5) | `passive-tree.v1.json` | **Tunable.** Retained provisionally — every measurement of it predates D25 |
| `concentration.w` | 0..1 weight | 0.5 | `passive-tree.v1.json` | **Tunable, and PRIMARY** — above `Θ≈300` it is the only thing separating builds (§16) |
| `unlockCost.first` | skill points | 5 | `passive-tree.v1.json` | **Tunable.** Derived as `step·(k+1)/2`, not guessed (D36) |
| `unlockCost.step` | skill points | 2 | `passive-tree.v1.json` | **Tunable** (D36) |
| `grant.skillPointsPerThetaMilliByScope` | points per Θ, per scope | 11 at Commander | `aptitudes.v{n}.json` | **Tunable.** A single scalar today; **per-scope is required, not optional** — at one scalar, 50 demons unlock the whole catalog (D34) |
| `tierLadder.k` | scalar in `req(t) = k·t(t+1)/2` | 5 | `passive-tree.v1.json` | **Tunable.** Its pairing with linear per-tier power is what makes reward-per-point exactly `b/5` (D26) |
| `nodePotencyCeiling` | ‰ of a tree's budget | **91** | `passive-tree.v1.json` | **Tunable, and now DERIVED not guessed:** `1000/(T+1)` at `T=10`. Doc 02's 125 was computed at T=7 and would admit a capstone worth **1.37×** the derived ceiling. A bounded ratio (§11.6 exempt), and it **refuses at emit time rather than clamping** |
| `soulThetaWeight` (`Ws`) | Θ per soul level | **unmeasured** | `passive-tree.v1.json` | **Tunable.** Needs its own `ssot-power-scale.md` §10.2 row, by row 18's precedent |
| target distribution | shares per aptitude / element | near-uniform + named theme allowance (D32) | `passive-tree-targets.v1.json` | **Tunable.** Shaped like `demon-roster-targets.v1.json` |
| `pointEconomy.respecPrice` | souls | exists today | `aptitudes.v{n}.json` | **Tunable.** D18 re-prices it; the `decisions.md:103` contradiction is unresolved |
| `b` — aptitude-point-equivalents per tier | scalar | — | — | **NOT a balance dial.** §3.5 swept it and no value works; it is a content-density choice |
| tiers = 10 · branches = 2 | count | D29 | code | **Structural** — the tree's own shape, like `SacrificesForStar`'s range. The per-tier node counts (`w[t]`, the shape archetype) *are* generated data |

⚠️ **`guard-power.ps1` cannot catch a missing §10 row for `unlockCost`.** Its G2/G3 checks key on a
parameter named `level` / `lvl` / `index`; `nodesOwned` lands in the same blind spot `DropVolume`'s
`thetaActor` already sits in. The row has to be added deliberately — the guard stays green without it.

## 15. What this deliberately does not decide

- **The species-tree generation pipeline's internals** (D23/D30) — it gets its own spec round.
- **Whether `F` survives.** Kept provisionally, pending a D25-inclusive, squad-scope sweep (D33).
- **How the injector renders any of this.** Standalone-first: the surface is web, and the injector may
  enrich it, never gate it.
- **The `family` taxonomy.** D27 ships the roster whole; curating 699 tokens is a tracked build-order
  task, sequenced when planning starts.
- **Any node's text, name or flavour.** That is the language stage's job, inside the plan's budget,
  potency ceiling and property vocabulary (§6).
