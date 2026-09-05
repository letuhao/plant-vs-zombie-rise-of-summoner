# Spec: `tree-state`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-state` · **Wave:** 2 · **Depends on:** [`tree-catalog`](spec-tree-catalog.md) ·
**Depended on by:** `tree-resolve`, `tree-surface`

---

## Objective

Own **layer (b) of the freeze line**: what a single actor has done to the shared catalog. Which nodes
it owns, how deep each is soul-levelled, what the next node costs, what a respec does, and where a
stale node id is rejected.

`tree-state` stores **effort** — which nodes, how many souls — and never derived power, so a rebalance
needs no migration ([passive-tree-map.md:64-65](../passive-tree-map.md)). It does not multiply anything
by `P(Θ)`; `tree-resolve` is the only module that does (map:66).

**The one-line contract:** persist the owned node **set** and the soul level per node. Derive budget,
spend, price and power on every read.

---

## Design

### 1. Sparse storage is a hard requirement, and the arithmetic says why

**The catalog side.** D27 ships 12 primary + 6 elemental + 21 status = **39 generic trees**, and D29
fixes each at 10 tiers × 2 branches = **40 nodes** — exactly 40, everywhere, species trees
included. So one actor faces **1,560** possible owned nodes and 1,560 possible soul levels, before
demon-family or species trees exist
([passive-tree-ideal.md:60-61](../passive-tree-ideal.md)).

**The actor side is uncapped, and this is the half that decides the storage shape.** D21 gives *every*
actor its own tree state — commander and each demon alike. The roster has no ceiling:

- `ContractPolicy.Capacity(purchasedSlots) => BaseSlots + Math.Max(0, purchasedSlots)`
  (`ContractPolicy.cs:171`), whose own comment at `:168-170` says the `Math.Min` was removed because
  *"the escalating price… was always the real scarcity control, not this `Math.Min`. A roster of 2,012
  costs 600,300,000 cumulative souls; that is the limit, not a hard-coded 48."*
- `ContractPolicy.CanBuySlot(purchasedSlots) => true` (`:182`) — *"Always true post-T3.6."*
- And **unbound** specimens are unlimited: a new demon *"simply arrives unbound when capacity is
  full"* (`RpgStore.Contracts.cs:80-83`). An unbound demon is still an actor, so it still carries tree
  state.

A dense row per actor is therefore `2,012 × 1,560 ≈ 3.1 million` rows for one player, all zero. **Not
an option.** Only non-zero entries may ever persist.

#### 1.1 The table

```sql
CREATE TABLE IF NOT EXISTS rpg_tree_node_state (
  scope       TEXT    NOT NULL,
  scope_key   TEXT    NOT NULL,
  node_id     TEXT    NOT NULL,
  soul_level  INTEGER NOT NULL,      -- 0 means owned but not deepened; a real state
  PRIMARY KEY (scope, scope_key, node_id)
);
CREATE INDEX IF NOT EXISTS ix_tree_node_state_key ON rpg_tree_node_state(scope, scope_key);
```

**Row presence means owned.** There is no `owned` column, because a column that is always `1` is not
information. A node that is not owned has no row; a node owned but never soul-levelled has a row with
`soul_level = 0`.

**Three properties are inherited, not invented** — `rpg_aptitude_allocation`
(`RpgStore.Aptitudes.cs:36-46`) is the shipped precedent and this is the same shape:

1. **Inputs only, never a resolved value.** `RpgStore.Aptitudes.cs:9-14`: *"INPUTS only, never a
   resolved channel value… a stored channel value would be a second SSOT that goes stale the moment a
   coefficient moves."* This table stores a node id and a soul level. It never stores a magnitude, a
   price, a spent total, or a remaining budget.
2. **Sparse by construction.** `SaveAllocation` (`RpgStore.Aptitudes.cs:76`) is a delete-then-insert
   of non-zero rows only — *"no row for an unspent aptitude — nothing to persist"* (`:96`). Copy that
   transaction shape exactly.
3. **`(scope, scope_key)` is already the D21 key.** The same two columns address a commander, a demon
   type, an aspect and a unique demon (`AllocationScope` at `AptitudeAllocation.cs:8`;
   `ScopeToText` at `RpgStore.Aptitudes.cs:52-60`). *"Every actor carries its own tree state"* needs no
   new addressing scheme.

**A node id already carries its tree** (`skill.<treeId>-<branch>-t<tier>-<nodeKey>`,
[`tree-catalog`](spec-tree-catalog.md) §3.1), so there is no `tree_id` column and **no per-tree read**.
One actor is one row-set. That is load-bearing for §6.

### 2. The rising unlock cost (D25/D36)

**The curve.** The Nth node an actor owns costs

```text
cost(N)       = first + (N - 1) * step                first = 5, step = 2
cumulative(N) = N * first + step * N * (N - 1) / 2

  first = unlockCost.firstPoints , step = unlockCost.stepPoints   (R2 names, §8)
  at the shipped pair this collapses to  cumulative(N) = N(N + 4)
```

the same arithmetic shape as `RpgXpCurve.XpToNext` and `ContractPolicy.NextSlotPrice`
(`ContractPolicy.cs:176-177`) — which is already rendered to players in their own words at
`contractView.ts:50-54` (*"18 / 24 slots · next 900 Souls · 84 Souls/day"*). **D25 is not a new
mechanic to the player.**

**Store the owned node SET; derive both budget and spend.**

```text
budget(actor) = skillPointsPerThetaMilliByScope[scope] * sourceValue(actor)
spend(actor)  = cumulative(count of VALID owned nodes)
available     = budget - spend
```

Nothing is debited from a stored balance, because there is no stored balance. Three consequences, and
they are the reason to do it this way:

| | Consequence |
|---|---|
| **Respec** | Clear the node set. The budget is the full `g·Θ` again. Nothing is "refunded" because nothing was ever debited |
| **Re-buying the same build** | Costs exactly what it cost before — `cumulative` is a function of the count, and the count restarts at 0 |
| **Buy cheap, respec rich** | **Impossible.** There is no stored price to refund at |
| **Hidden tax** | **None.** The same set costs the same amount |

**The lemma that makes derive-on-read safe, and it must be a test.** `cumulative(N)` depends only on
`N`, so the cost of a *set* of N nodes is independent of the order they were bought in —
`Σ(first + (i−1)·step)` has no order term. Without that property, derive-on-read would disagree with
pay-as-you-go and the store would have to remember a price after all. **This is the same trick D18
used on Grim Dawn's order-sensitivity, applied to the price instead of the allocation** — which is why
D25 and D18 compose with no special case.

#### 2.1 What counts as an owned node

Structural, not tunable — `tunables-ssot.md` §1's test is *"if changing it breaks whether the system
works rather than how the game feels, it is structural."* Each row below is a `const` with a comment
citing its reason.

| Thing | Counts toward the price? | Why |
|---|---|---|
| Node unlocked with self-earned skill points | **Yes** | — |
| Node unlocked with item-granted points, item still equipped | **Yes** — the item paid for the node it is pricing | D11. The alternative needs per-node provenance, which destroys D11's stated advantage: *"points flow through the tree's own rules… no special case to define, enforce or test"* |
| Node left invalid by unequipping (D11's red state) | **No** — grants nothing, costs nothing to hold | Otherwise unequipping leaves you paying more for nodes that give you nothing: a pure penalty with no compensation, which is exactly the trap D8's *self-spent only* amendment closed |
| Soul level on a node | **No** — a different track | D3: points unlock *new bonuses*, souls scale *bonus power*. Souls are unlimited by design, so counting them would make the unlock price unbounded in a currency with no ceiling — converting a soft economic bound into a genuine wall |
| Nodes on the actor's **other** trees | **Yes** — that is the whole point | A per-tree count makes the first node of a 40th tree cost 5 again. At `Θ=100` the dial affords 31 nodes (41 at §2.2d's corrected `g`) while 39 trees × tier 1 is 156 — a per-tree count hands a spread build four to five times the breadth for the same budget |

**"An item swap must not change what your next node costs, net"** is an invariant that is obvious in a
spec and easy to lose in code. It gets a named test.

#### 2.2 `first = step·(k+1)/2` is a coupling, not two dials — and its precondition does not hold

**The algebra, first, because everything below turns on it.** With `first = step·(k+1)/2` the
cumulative cost of owning `N` nodes collapses to

```text
cumulative(N) = N·first + step·N(N−1)/2 = (step/2)·N·(N + k)
```

and if — **and only if** — the actor gains a **constant `k` nodes per tier**, so that `N(T) = k·T`,

```text
cumulative(N(T)) = (step·k²/2)·T(T+1)        against        W(T) = b·T(T+1)/2
reward per skill point = W(T) / cumulative = b / (step·k²)      — no T term, flat at every tier
```

At `step = 2, k = 4` that is `first = 5` and a flat `b/32`, exactly as D26 did for
reward-per-aptitude-point. Take the obvious dial instead (`first = step = 1`) and the
ratio runs from `b/10` at tier 1 to `b/16` asymptotically — a **1.6× gradient favouring shallow
nodes**, which is D20's tier-1 defect with the sign flipped, in the currency D26 did not look at. It
is invisible unless the algebra is written down.

##### ⛔ 2.2a The defect: `k = 4` is the corpus AVERAGE, and the corpus is deliberately not uniform

~~With `k = 4` nodes per tier (D29: 40 nodes / 10 tiers) … that condition is what makes
reward-per-skill-point flat at **every** tier.~~ **That claim is false for two of the three shipped
archetypes, and this spec was the one that made it.**

`k = 4` is 40 ÷ 10 — an average over the whole tree. `spec-tree-plan.md:221-243` ships three width
vectors that are **deliberately** not uniform, because non-uniformity is the entire mechanism by which
D15 gets *"equal expected value, not equal shape"*. Per tier, across both branches:

| archetype | nodes per tier, `t = 1..10` |
|---|---|
| `broad-and-flat` | 4 4 4 4 4 4 4 4 4 4 |
| `gated-deep` | 6 6 6 4 4 4 4 2 2 2 |
| `late-crown` | 2 2 4 4 4 4 4 4 6 6 |

So `k = 4` holds for one archetype in three and ranges 2–6 in the others. Working the consequence with
the shipped pair — `cumulative(N) = 5N + 2·N(N−1)/2 = N(N+4)`, `W(T) = b·T(T+1)/2` — reward per skill
point is:

| tier | `broad-and-flat` | `gated-deep` | `late-crown` |
|---:|---|---|---|
| 1 | b/32 | b/60 | b/12 |
| 2 | b/32 | b/64 | **b/10.7** |
| 3 | b/32 | **b/66** | b/16 |
| 5 | b/32 | b/52 | b/21 |
| 7 | b/32 | b/46 | b/24 |
| 10 | b/32 | b/32 | b/32 |

*(Worked example, `gated-deep` at `t = 3`: `N = 6+6+6 = 18`, `cumulative = 18·22 = 396`, `W = 6b`,
`6b/396 = b/66`. `late-crown` at `t = 2`: `N = 4`, `cumulative = 4·8 = 32`, `W = 3b`, `3b/32 = b/10.7`.)*

- **`gated-deep`** runs `b/66` at tier 3 to `b/32` at tier 10 — a **2.06× gradient favouring depth**.
- **`late-crown`** runs `b/10.7` at tier 2 to `b/32` at tier 10 — a **3.0× gradient favouring shallow**.
- **At tier 2, two trees the plan certifies as equal value differ by 6.0×** in reward per skill point
  (`b/10.7` against `b/64`). At tier 7 — where an all-in build sits at `Θ = 100` — the spread is
  still 1.92×.

**All three agree exactly at tier 10 and nowhere else.** The whole tree costs the same 1,760 skill
points under every archetype, which is why this was missed: **the derivation was checked at the
endpoint.** Below `Θ ≈ 170` nobody is at the endpoint, and partial depth is the whole game there.

##### 2.2b The three ways out, and the one that ships — (c), owned by `tree-plan`

Three ways out were considered:

**(a) Make the price archetype-aware** — derive `first` per archetype. **Ruled out, with the working.**
No positive `(first, step)` makes `gated-deep` flat at every tier. Its cumulative node counts are
`N(T) = 6,12,18,22,26,30,34,36,38,40`, so flatness needs
`cumulative(N(T)) ∝ T(T+1)`, and matching `T = 1` against `T = 10` requires
`55·(6·first + 15·step) = 40·first + 780·step`, i.e. `290·first = −45·step` — **a negative `first`**.
An arithmetic price has two dials and the non-uniform width vector needs a per-tier one; a per-tier
price is a different mechanic, not a retune. (It would also mean the answer to *"what does my next node
cost"* depends on which tree you point at, which is a much larger change to the player's model than the
gradient it fixes.)

**(b) Bound the width-vector spread in `tree-plan`.** Constrain the archetype set to a constant
per-tier node count — or a stated, tested spread around one — and take D15's variation somewhere that
is not inside a cost ladder.

**(c) Accept the gradient, bound it, and drop the flatness claim.** State the worst cross-archetype
ratio as a shipped invariant with a number, and stop asserting flatness.

> ~~**Recommendation on the record: (b).**~~ **Superseded within the same audit fold — the answer is
> (c), and `tree-plan` owns it.** `tree-plan` was handed this defect in parallel, worked the same
> three options, and landed on (c) with a refusal attached. Its two counter-arguments are better than
> the (b) recommendation they replace, and one of them is about *this* module:
>
> - **Against (b):** flatness under a quadratic price requires `N(T) ∝ T`, which means a **uniform**
>   width vector. "Bound the spread" is therefore not a mild constraint — tightened enough to restore
>   flatness it collapses every archetype onto `[2,2,2,2,2,2,2,2,2,2]`, which is D15's own named
>   failure, *"every tree feels the same."* The earlier recommendation treated this as a cheap trade
>   because it read D15's payoff as node **potency**; that reading is right about potency and wrong
>   about what a uniform width costs — the width vector is also what makes `gated-deep` gate and
>   `late-crown` crown.
> - **Against (a), and this half is `tree-state`'s to concede:** an archetype-aware price would have to
>   key on **tier**, not on nodes owned, and that **breaks §2's order-independence lemma** — the
>   property that makes derive-on-read safe and that D18 and D25 compose through. The working above rules (a)
>   out as arithmetically impossible; it is also structurally forbidden by this module's own contract,
>   and that is the stronger reason.
>
> **What ships, and who owns which half:**
>
> | Half | Owner | Artifact |
> |---|---|---|
> | The **bound** and the refusal that enforces it | **`tree-plan`** | `R-A1`, `archetype.rewardSpreadMaxRatioMilli = 6000` (‰) — the shipped three pass at equality, and a fourth archetype that widens the spread is refused, naming the tier and the two archetypes |
> | The **price** staying a function of the count alone | **`tree-state`** | §2's order-independence lemma, already a named test. This is what forbids (a), so it is now load-bearing for someone else's decision and must not be relaxed |
> | The **stated number** in the player-facing and spec-facing prose | **`tree-state`** | §2.2a's table and the flatness claim, corrected below |
>
> **The flatness claim, corrected.** `reward per skill point = b/(step·k²)` is flat **at constant
> width `k`**, and `broad-and-flat` is the only shipped archetype with constant width. Across the
> shipped set the honest statement is: **the spread is 6.0× at tier 2, monotone non-increasing from
> tier 2 onward, and exactly 1.00× at tier 10.** `gated-deep` is the worse buy early and the better
> buy late; `late-crown` is front-loaded and flattens; `broad-and-flat` is `b/32` at every depth.
> **That is a pacing difference, not a power difference** — every archetype spends the same
> `budgetTotal` and reaches the same place — and it is now stated with a bound instead of denied.

##### 2.2c The test that would have caught it — and the one that did not

`reward_per_skill_point_is_flat_when_first_equals_step_times_k_plus_one_over_two` asserts the algebra
for **a given `k`**. It passes on a `k = 4` fixture and never sees the archetype set. **Checking the
endpoint is precisely what failed** — all three archetypes agree at tier 10, so any test that samples
the full tree, or that takes `k` as a parameter, is green while the corpus is skewed 6×.

**The replacement, and it fails today by design:**

```text
reward_per_skill_point_is_within_band_over_every_shipped_archetype_and_every_tier
  for each archetype in tree-plan's shipped set:
      for T in 1..10:
          N    = Σ_{t≤T} width[t]                   # ACTUAL widths, both branches, never 40/10
          r[a] = (T·(T+1)/2) / (N·(N+4))            # b cancels; exact integer ratio, no float
      assert max(r) / min(r) <= archetype.rewardSpreadMaxRatioMilli / 1000
  assert the spread is EXACTLY 1000‰ at T == tierCount
```

Three things make it catch what the old one could not: it reads `tree-plan`'s **actual** width
vectors rather than a `k` fixture, it asserts at **every** tier rather than only the endpoint where
all three archetypes agree by construction, and it compares against a **named tunable** so tightening
the bound later is a one-line change with a visible failure.

**It is green as shipped, and that is the point.** `archetype.rewardSpreadMaxRatioMilli = 6000` is the
measured maximum of the shipped three, so the set passes **at equality** — the test's whole job is to
refuse a *fourth* archetype that widens the spread, and to make anyone tightening the number see
exactly which tier and which pair fails.

**Two tests, not one — the old one keeps a narrower job.**
`reward_per_skill_point_is_flat_when_first_equals_step_times_k_plus_one_over_two` stays, with its
scope written into its own name: it proves the *algebra* at **constant width**, and proves it fails at
`first = step`. What it must never again be read as is evidence about the shipped corpus. The band
test is the corpus one. Two assertions, two subjects, and the pairing is what stops a `k`-parameterised
proof being mistaken for a corpus property a second time.

**Changing D29's tree shape means re-deriving the price, not just retuning it** — and, as 2.2a shows,
`tree-plan` ships three shapes, so `first = step·(k+1)/2` fixes only the uniform one. The coupling and
its scope both live in the tuning file's `_meta`.

##### ⛔ 2.2d The same `k²` runs through the grant, so `skillPointsPerTheta` is mis-calibrated too

The archetype defect is not confined to the price. **The wallet was calibrated by the same
constant-width algebra**, and this spec's Open question 3 carried the derivation as
~~`g = 3·s·step·k²/5 = 10.40`~~. Two separate things are wrong with that line, and they were found by
re-deriving it rather than by reading it.

**The derivation, re-run from the shipped constants.** Under R1 the tier gate reads **aptitude**
points, so the two currencies meet at the `Θ` where a tier opens:

```text
aptitude supply     a·Θ                    a = grant.aptitudePointsPerTheta = 3   (commander)
tier T's gate       req(T) = s·T(T+1)/2    s = tierLadder.reqScalePoints    = 5   (R2)
  ⇒ tier T opens at Θ_T = s·T(T+1) / (2a)

nodes owned to tier T at constant width k          N(T) = k·T
their cumulative price, when first = step·(k+1)/2  = (step·k²/2)·T(T+1)
the wallet at that same Θ                          g·Θ_T = g·s·T(T+1) / (2a)

  ⇒ g = a · step · k² / s          — the T terms cancel; it is an identity in T
```

At `a = 3, step = 2, k = 4, s = 5` that is `3 · 2 · 16 / 5` = **19.2**.

1. ~~**The written form is wrong** — `s` cancels itself out.~~ **RETRACTED 2026-09-05.** It does not.
   `s` in `3·s·step·k²/5` is the **corner spike share, 0.54163** — defined at
   `docs/research/passive-tree/12-rising-unlock-cost.md:255` and read from
   `tools/HybridViability/Program.cs:79-80,86` — while the `5` in the denominator is
   `tierLadder.reqScalePoints`. Two different symbols that happen to be adjacent. Nothing cancels,
   and `3 · 0.54163 · 2 · 16 / 5` **is exactly 10.40**. The same 0.54163 is where D29's `1.626·Θ`
   comes from (`3 × 0.54163 = 1.625`), so it is load-bearing elsewhere too.

2. ~~**The value does not reproduce, so `11` is unreconciled.**~~ **RETRACTED.** It reproduces
   exactly, and **`11` is `10.40` rounded up, as the original text said.**

**What survives, and it is worth keeping.** The derivation above is not a correction of an error — it
is a **different question with a different answer**, and the gap between them is exactly the share:

```text
19.2 / 10.40  =  1.846  =  1 / 0.54163
```

- **10.40** answers *"what grant matches a corner build whose spike holds 54% of the share vector"* —
  the build `HybridViability` actually constructs, and the one every sweep in this program measured.
- **19.2** answers *"what grant clears the gate for a build putting its whole allocation in one
  tree"* — share 1.0, which no measured build has.

So the open question is not arithmetic, it is **which build the wallet should be sized against**, and
that is a real owner decision this spec should carry rather than resolve. Under 10.40 a corner build
is funded to its measured shape and a hypothetical pure-single-tree build is under-funded; under 19.2
the reverse. **`g = 11` stands** until that is decided; republishing it is not blocked on an
arithmetic fix, because there was none.

**And `g ∝ k²` is what carries the archetype defect into the grant.** Width enters *squared*, so the
two non-uniform archetypes are mis-funded in opposite directions by the same `k = 4` calibration.
Substituting an effective width is not exact — the shipped `first = 5` makes the real cumulative
`N(N+4)` for every archetype, while flatness would need `N(N+k)` — so the honest numbers are the ones
§2.2a already computed, and they agree with the wallet arithmetic exactly:

| archetype | worst tier | wallet vs bill at the correctly-derived `g` | same ratio in §2.2a's table |
|---|---|---|---|
| `broad-and-flat` | — | clears exactly at every tier | `b/32` flat |
| `gated-deep` | 3 | wallet 192 against a bill of `18·22 = 396` — **2.06× under-funded** | `b/66` vs `b/32` |
| `late-crown` | 2 | wallet 96 against a bill of `4·8 = 32` — **3.0× over-funded** | `b/10.7` vs `b/32` |

**What this module does about it.** The recalibration is a balance decision, not a spec edit: `g` is
one global tunable and moving it moves every wallet in the game, so it is an **Ask first** item, not
something this fold changes. `11` stays in §8's table, flagged, until the owner republishes it. Two
things that do *not* change: §2.1's breadth argument survives the recalibration (at `g = 19.2` the
`Θ = 100` wallet affords 41 nodes against 39 trees × tier 1 = 156, so a per-tree count would still
hand a spread build several times the breadth), and §7's `long` conclusion strengthens, because a
larger `g` makes the budget larger.

**The test that would have caught it, and it belongs here because this module owns the wallet:**

```text
the_skill_wallet_clears_the_tier_it_just_opened_for_every_shipped_archetype
  for each archetype a, for T in 1..tierCount:
      theta  = reqScalePoints·T·(T+1) / (2 · aptitudePointsPerTheta)   # where tier T opens
      wallet = skillPointsPerThetaByScope[commander] · theta
      bill   = cumulative(Σ_{t≤T} width_a[t])                          # firstPoints/stepPoints
      assert wallet/bill within archetype.rewardSpreadMaxRatioMilli of 1000‰
```

It is the same band, read from the other side — the price test walks the reward ratio, this one walks
the wallet-to-bill ratio, and a `g` that does not reproduce from its own formula fails both. **The
existing derivation had no test at all**, which is why a formula that cannot produce its own stated
value survived in a spec.

#### 2.3 It is a soft bound, provably — not a ceiling

Under an arithmetic price the nodes affordable at index `Θ` are
`N(Θ) = ⌊√((first/step − ½)² + 2gΘ/step) − (first/step − ½)⌋`, strictly increasing and unbounded in
`Θ`, and `Θ` is uncapped by decision. **Every node in the catalog is reachable at some finite `Θ`** —
the whole generic catalog at `Θ ≈ 221,804`, well inside `PowerLadder.MaxIndex` (`PowerLadder.cs:65`,
computed by binary search, ≈2.147×10⁸). A cap refuses forever; this defers. PS-8 is satisfied
*provably*, not by assertion.

Three things must hold or the proof lapses, and all three are cheap to forbid:

- **no `Math.Min` on the price**,
- **no narrowing cast on the budget**,
- **no boolean `CanUnlock` that can return false.**

`CostOfNextNode`'s own comment must state that this is a soft economic bound, that nothing is refused,
and that the absence of a clamp is deliberate — PS-8 requires an exemption to say so.

**A property worth naming: the bound re-tunes itself as content ships.** Cumulative cost of the whole
catalog is `≈ (step/2)·N²`, so doubling the content quadruples the price of owning all of it. D27's
*ship the roster whole* and D30's **33,600** species nodes (840 species × 40) cannot reopen the
breadth hole, with no balance pass required. A flat price has the opposite property.

### 3. `skillPointsPerThetaMilliByScope` (D34) — required, not optional

**FACT.** `AptitudeGrant` carries `SkillPointsPerThetaMilli` as a **single scalar**
(`AptitudeTuning.cs:13`), parsed at `:158`, value `1` at `data/tuning/aptitudes.v5.json:17`. Its
sibling one block over is already a table:
`AptitudePointEconomy.AptitudePointsPerThetaMilliByScope` (`AptitudeTuning.cs:43-45`,
`aptitudes.v5.json:23-28`, `{commander 3, demonType 4, aspect 4, uniqueDemon 6}`). Skill points have
no scope table.

**Why that breaks the design outright.** With one scalar, "an actor's skill points" has no per-actor
definition, and the path of least resistance is that every actor reads `Θ_player`. Fifty demons would
then each get a full commander budget on its own fresh price ladder: **50 × 31 nodes at `Θ=100` is
1,550 — effectively the whole 1,560-node generic catalog, owned across the roster at the calibration
point.** D25's bound does not survive that.

**The conclusion does not depend on which `g` ships.** 31 is the count at the shipped `g = 11`; at
§2.2d's alternative sizing of 19.2 (an open owner question, not a correction) it is 41 nodes each, so fifty actors reach 2,050 and the hole gets
*wider*. This paragraph is the one place a `g` recalibration was worth re-checking, and it survives
it.

**Requirement:** `pointEconomy` gains `skillPointsPerThetaMilliByScope`, mirroring the sibling field
one line above it. The argument was already made once, for that sibling — `spec-point-economy.md`
§2.2's *"the single number becomes a table"*, quoted at `aptitudes.v5.json:20`.

```csharp
// The sibling of PointBudget.PointsFor (PointBudget.cs:51-58), same shape, same discipline.
public static long SkillPointsFor(AllocationScope scope, long sourceValue, AptitudeTuning tuning)
```

- Same `checked` multiply, same *no cap anywhere* comment (`PointBudget.cs:44-49`), same rejection of
  a negative source value.
- **A missing rate is a load rejection naming it**, never a default (`tunables-ssot.md:91-93`).
- **Retuning the grant today is free.** `SkillPointsPerThetaMilli` has zero production consumers —
  the only hits are the declaration and the parse site — so no migration, no golden, no balance
  regression. D25 is its first spender, so D25 owns its calibration.

**Blocked-on, tracked rather than open:** the four scope sources are in different states. Specimen
level now reads the shared arithmetic curve; species level is an index
(`PointBudget.DemonTypeSourceFromLevel`, `:40`); `element_mastery` and almanac XP have **zero `src/`
hits**. This module needs whichever scopes actually ship trees, not all four.

### 4. The migration boundary — reject once, never per actor load

**The shipped defect, stated exactly.** `AptitudeAllocation.Single` throws
`ArgumentException("unknown aptitude id …")` at `AptitudeAllocation.cs:39`, and the load path calls
it **per row** at ~~`RpgStore.Aptitudes.cs:132`~~ **`RpgStore.Aptitudes.cs:149`** — the
`allocation += AptitudeAllocation.Single(scope, r.GetString(0), r.GetInt64(1))` inside
`LoadAllocationUnlocked`'s reader loop (`:135`), which `LoadAllocation` (`:120`) delegates to.
Re-verified 2026-09-05. At twelve aptitudes that is a fine trade. At
1,560 node ids per actor across an uncapped roster, **one retired node id in one save makes that actor
unloadable rather than showing red.** One bad row bricks a save.

**The rule this module implements:**

| Row's node id | Where it is handled | What happens |
|---|---|---|
| Known and `enabled` | actor load | live; grants its effect; counts toward D25's price |
| Known and retired (`enabled: false`) | actor load | **displayed as invalid (red), never silently repaired** (D11). Grants nothing. Does **not** count toward the price (§2.1). Row kept |
| Never present in any catalog revision | **the catalog import transaction, once** | the **import** is rejected, with **every** offending id named in one report. The actor load never sees it |

**`LoadTreeState` never throws on an unknown node id.** It returns rows; classification happens
against the catalog afterwards, in memory, and the three-way result is what the surface renders. The
loud failure lives at the import boundary — the same transaction that bumps `catalog_revision` — where
one report can name every problem at once and a human can act on it before players do.

**And the escape hatch is already built.** A catalog revision that retires an *allocated* node grants a
free full respec (D18 at price zero, [`tree-catalog`](spec-tree-catalog.md) §4 R4). There is no
partial-refund path and no per-node compensation table to write.

This preserves the discipline `RpgStore.Aptitudes.cs:47-50` already states — *"Throws naming the bad
value rather than defaulting"* — while moving **where** it throws.

### 5. Respec (D18)

- **Full reset**: skill distribution and primary stats together, cleared and redistributed as **one
  transaction**. With no partial respec there is no orphaned unlock.
- **Scoped per actor**, `(scope, scope_key)`. The shipped store is per-key by construction —
  `SaveAllocation(scope, scopeKey, …)` (`RpgStore.Aptitudes.cs:76`) — and a roster-wide reset at a
  2,000-demon roster would be 2,000 delete-and-reinsert transactions under one lock. Price it per
  actor.
- **Never refused.** Copy `RespecPolicy`'s own API discipline verbatim (`RespecPolicy.cs:33-35`):
  *"Always available, always priced, never refused — there is no 'cannot respec' return here on
  purpose."* `ContractPolicy.CanBuySlot` (`:182`) is the second precedent.
- **Priced in souls**, the same resource and the same linear-escalation-on-a-count shape the shipped
  `RespecPolicy` already uses (§5.1). Not a new mechanic, and not a blocked one.

#### 5.1 ~~The pricing contradiction~~ — **there is none. Re-verified against code 2026-09-05**

> ⚠ **This section previously asserted a three-way contradiction and held respec's production wiring
> on it. All three legs were wrong, and the hold with them.** Recorded rather than deleted because the
> mistake is instructive: the claim was made from a stale reading of `RespecPolicy` and never re-run.

Three facts, each re-read this session:

1. `decisions.md:103` (Class system row) locks *"respec is available, unlimited, and **priced in a
   resource fighting also costs**"*, with no respec cap.
2. **D18 says souls.**
3. **`RespecPolicy.PriceOf` returns `RespecResource.Soul`** (`RespecPolicy.cs:46`), and `Soul` is the
   enum's **only** member (`:23`). `RespecResource.Hunger` is named exactly once, at `:15`, and is
   named there as *the prior value*: *"The prior `RespecResource.Hunger` value was an explicitly
   documented placeholder pending that answer, not a shipped default."* The answer landed —
   `spec-species-respec.md`'s own decision 1.

**All three agree, and souls are a resource fighting also costs** — `SoulEarnPolicy.Reasons.Respec`
(`SoulEarnPolicy.cs:68`) is already a ledger reason beside the earn reasons, so the lock at
`decisions.md:103` is satisfied by construction rather than by argument.

**And it has two production callers, not zero:**

| Call site | What it does |
|---|---|
| `RpgStore.SpeciesRespec.cs:176` | prices off the persisted counter inside the respec transaction, after the replay check, before the balance check |
| `SpeciesBuildEndpoints.cs:91` | the read-only quote endpoint — `GetSpeciesRespecCount` then `PriceOf`, returned as `priceResource`/`priceAmount` |

So respec is **already priced, already wired and already spending souls**. The old text's *"the
shipped allocate path is an unpriced full reset"* described the aptitude allocate endpoint, which is a
different path from species respec and was never what `RespecPolicy` priced.

**What this module actually needs, which is smaller than the old hold.** The price shape is settled
and reusable: linear escalation on a persisted per-subject count, `long` throughout, divided by 1000
last, `checked`, never refused (`RespecPolicy.cs:29-35`). A passive-tree respec adopts that shape with
its own counter and its own tunable amount. **What is genuinely undecided is only whether a tree
respec shares the species respec counter or keeps its own** — a scoping question, not a resource
question, and it is the one that stays in Open questions.

### 6. The read path cost — the answer to D21's real bill

**Today the whole squad shares one allocation read.** `WebMatchService.cs:388` does a single
`LoadAllocation(Commander, …)` and passes it down; the per-species fallback at `:491` reads it only
when the caller did not. D21 turns that into **one read per actor**, and every `LoadAllocation` takes
the global `lock (_gate)` (`RpgStore.Aptitudes.cs:125`; `_gate` is declared once at `RpgStore.cs:17`
and shared by *every* partial slice of the store) and opens a fresh connection (`:126`). At a 6-actor
squad — `const int maxSquad = 6`, `WebMatchService.cs:339` — and 39 trees, a naive per-(actor, tree)
read is **234 lock-serialised queries before the first turn**, on the standalone path where battles
*are* the loop.

**Three rules make that go away, and none is hard:**

1. **Batch-first API.** `LoadTreeStateBatch(IReadOnlyList<(AllocationScope Scope, string Key)> keys)`
   is the primary entry point: **one query, one lock acquisition, one connection**, returning every
   actor's sparse rows grouped by key. Battle setup reads the whole squad once.
2. **There is no per-tree read at all.** The primary key is `(scope, scope_key, node_id)` and the tree
   is inside the node id, so one actor is one row-set. The `n = 39` factor never enters.
3. **`LoadTreeState` (single key) exists for the editing surface only.** A named test asserts the
   battle path does not call it in a loop — the same seam discipline `SpeciesAllocationSeamTests`
   already enforces for `LoadAllocation`.

**One adjacent constraint, recorded so it is not tripped over.** `ListDemonRoster`
(`RpgStore.Demons.cs:154`) selects every non-retired specimen for a player with no `LIMIT` and no
cursor. **Tree state must not be joined onto it until it pages** — at a 2,012 roster that response is
already large, and "compare my demons' builds" is exactly the surface that would join them.

### 7. Numeric types

**`long` everywhere on both sides of the comparison.** CLAUDE.md's measured thresholds, applied:

| Type | Exact-integer ceiling | First `Θ` that breaks it | Used here? |
|---|---|---|---|
| `float` | 16,777,216 | **232** | **never** — inside normal play, and non-deterministic |
| `int` (per-mille) | 2,147,483,647 | **3,213** | never |
| `int` (whole units) | 2,147,483,647 | 103,557 | never for a magnitude |
| `long` | 9,223,372,036,854,775,807 | 214,748,300 | **the default for everything here** |

**The price is small; the budget is not.** At the shipped corpus — **35,160** nodes across **879**
trees (D29's 39 generic × 40 = 1,560 plus D30's 840 species × 40 = 33,600) — the marginal price of the
last node is `5 + 2·35,159 = 70,323` and the cumulative is `35,160 · 35,164 = 1,236,366,240`, i.e.
**1.24×10⁹**. ~~`5 + 2·25,899 = 51,803` … `670,913,600`~~ used the superseded ~25,900 figure and is
struck rather than adjusted, because a wrong corpus size is what made the old margin look comfortable.
Both still fit `int`, but the cumulative is now **58% of `int.MaxValue`** where it was 31% — the same
conclusion, arrived at with half the headroom, and one more content wave closes it. But the budget is
`g·Θ`, and at `Θ = PowerLadder.MaxIndex`
(`PowerLadder.cs:65`, computed by binary search, ≈2.147×10⁸) with `g = 11` that is ≈**2.36×10⁹** —
past `int.MaxValue`. Once the budget is `long`, comparing it against an `int` price is a narrowing
defect. **Both sides are `long`.**

**Widen before multiplying, divide by 1000 last and exactly once, let overflow throw.**
`(long)a * b`, never `(long)(a * b)` — the cast binds to the *result*, so the multiply has already
overflowed. `checked` on every product, including ones that are structurally unreachable: an
unreachable overflow that wraps is still a wrap.

**Where a `long` would overflow on the soul track.** A soul level enters as an index offset,
`Θ_node = Θ_actor + Ws·soulLevel` — where `Ws` is the symbol for
`soulTrack.thetaPerSoulLevelMilli / 1000` (R2; §8) — and it never multiplies the node's coefficient,
because coefficient scaling gives power ∝ √effort and breaks §10.5's linear-in-effort property. At
`Ws = 1` (i.e. `thetaPerSoulLevelMilli = 1000`),
`PowerLadder.ValueMilli` reaches `long.MaxValue` at `Θ ≈ 214,748,300`, so:

> **A `long` would overflow at `soulLevel ≈ 214,748,300 − Θ_actor`**, and `PowerLadder.Guard` throws
> `PowerIndexOverflow` there rather than wrapping (`PowerLadder.cs:104-105`). At an arithmetic cost
> ladder of step `s` that level costs ≈ `s · 2.3×10¹⁶` souls. **A type boundary, not a ceiling.**

**The wall that actually binds first is one system over, and it is a wiring gap.**
`AtomCompiler.cs:464` narrows with `checked((int)…)`, so a whole-`P(Θ)` node throws at
`Θ ≈ 103,557` — exactly CLAUDE.md's `int` whole-units row. Widening that result to `long` moves the
first refusal to `Θ ≈ 214,748,300` and costs one cast. Owned by the atom layer; named here because the
soul track is the first system that can push a single node's magnitude that far.

**Storage.** `soul_level` is a SQLite `INTEGER` (64-bit) read with `GetInt64`, never `GetInt32`.

### 8. Tunables — every number, its unit and its file

`tunables-ssot.md` is binding: a number a balance pass would change lives in
`data/tuning/<domain>.v{n}.json`, never as a `const`. Ideal §14 is the program's own list; this is this
module's slice of it.

| Key | Unit | Value | File |
|---|---|---|---|
**Canonical key names are ruling R2's, and every key carries its own unit** — the whole point of T6.
The struck names are superseded and must not appear in code, config or a sibling spec.

| Key | Unit | Value | File |
|---|---|---|---|
| `unlockCost.firstPoints` ~~`unlockCost.first`~~ | skill **points** | 5 | `data/tuning/passive-tree.v1.json` (**new** — does not exist as of 2026-09-05) |
| `unlockCost.stepPoints` ~~`unlockCost.step`~~ | skill **points** | 2 | same |
| `soulTrack.thetaPerSoulLevelMilli` ~~`soulThetaWeight` (`Ws`)~~ | `Θ` per soul level, **per-mille** | **unmeasured** | same |
| `pointEconomy.skillPointsPerThetaMilliByScope.commander` | skill points per `Θ` | 11, **and it does not reproduce from its own stated derivation — §2.2d** | `data/tuning/aptitudes.v{n+1}.json` |
| `pointEconomy.skillPointsPerThetaMilliByScope.{demonType,aspect,uniqueDemon}` | skill points per that scope's own source unit | **unmeasured** | same |
| `pointEconomy.respecPrice` | **souls** (§5.1 — settled, not unresolved) | 10 today (`aptitudes.v5.json:30`) | same |

**Read in place, never copied.** `grant.skillPointsPerTheta` already ships with a live loader
(`aptitudes.v5.json:17`, `AptitudeTuning.cs:158`); the tree reads `AptitudeTuning` rather than
duplicating the value — tunables-ssot §2, *a copied number is a future drift bug with a delay fuse*.

**Why `Points` and not `Milli` on `unlockCost.*`, restated under R2.** The suffix names the **unit**,
and the unit here is whole skill points — the price of the first node is 5 points, not 0.005 of
anything. The shipped `SkillPointsPerThetaMilli` neither multiplies nor divides by 1000, so its
`Milli` is a naming artifact (`PointBudget.cs:44-46` documents the same one for its sibling), and
copying that artifact into a new file is exactly the trap T6 exists to prevent. R2 closes it the other
way: name the unit truthfully in the key.

> ⛔ **`soulTrack.thetaPerSoulLevelMilli` IS per-mille, and §7 reasons in whole `Θ`.** `Ws = 1` in §7's
> overflow arithmetic is `thetaPerSoulLevelMilli = 1000`, not `1`. Writing `1` into the per-mille key
> gives a soul level worth **one thousandth** of a `Θ` step and every test in this spec still passes —
> the same silent 1000× R2 names for `concentration.fmaxMilli`. The loader divides by 1000 exactly
> once, last, and a named test pins `1000 ⇒ Ws = 1`.

**Structural, with a comment saying so** (not tunable): what counts as an owned node (§2.1's five
rows); row-presence-means-owned; the two-track split between points and souls.

### 9. The `ssot-power-scale.md` obligations — **two rows, not one** — and why the guard will not catch them

#### 9.1 §10.2 — the cost-ladder row

D25's cost function needs **one new row in §10.2** — not §10.1, because its input is `nodesOwned`, a
per-actor counter, never a level. That is row 19's standing exactly (*"a per-holder counter, never
`Θ`, so this is not a level curve"*), with row 6's *cost ladder* verdict. Rows 26 and 27 are the direct
precedent for giving a cost ladder its own row rather than reusing row 6: *"a separate row rather than
reusing row 6 because it reads its own tunable pair."* The highest row in §10 today is **28**, so this
is **row 29**. An `inventory.json` mirror row lands in the same change — the mirror's own
`_meta.rebalance` requires it, and a prior audit found *"the second half did not happen, twice."*

What the node *pays out* needs no row: it reads `P(Θ)` through the shared `PowerLadder`, which is row
16's own precedent.

> ⚠ **`guard-power.ps1` cannot catch the row's absence — say so out loud.** Its G2/G3 regex
> (`scripts/guard-power.ps1:74`) matches a method whose parameter is named `level`, `lvl` or `index`.
> `CostOfNextNode(long nodesOwned, …)` matches none of those, exactly the blind spot
> `DropVolume.VolumeScaleMilli`'s `thetaActor` already sat in. **A passing guard is not evidence.**
> The row is added deliberately or not at all.

The soul→`Θ` weight `Ws` needs a further §10.2 row (row 18's precedent, `thetaOffset`). That read
happens in `tree-resolve`, so the row belongs to that module — recorded here because this module
stores the level the row scales.

#### 9.2 §11 — the caps-register row this spec previously skipped

> ⛔ **A §10 row is not a §11 verdict, and D25 owes both.** An earlier draft of this section asked for
> the §10.2 row only and asserted PS-8 compliance in prose. That is the exact move §11's own preamble
> forbids: *"every one has to say which kind it is."*

Doc 10's finding **A2** is correct on the classification: `ssot-power-scale.md:783` names *"a flat
rate facing a scaling cost"* as a cap in so many words, and D25 **is** that shape — the skill wallet
`g·Θ` grows **linearly** in `Θ` while `cumulative(N) = N(N+4)` grows **quadratically** in `N`, so
affordable breadth grows only as `√Θ`. §11.7 is the same shape one system over (a flat soul faucet
against a scaling sink) and was *"a cap that was not a number at all"* — which is precisely why a
sweep grepping for `const … Max|Cap|Limit` will never find this one either.

**The row goes in §11.10 (*Content breadth — a different axis*)**, which is where A2 places it and
where the register already keeps bounds on how much content one holder can reach. It shares that
section with `ShieldPolicy.MaxShieldsPerActor` and the affix/rarity ladders. §11.2 was considered and
rejected: §11.2 is for ceilings whose verdict is *"soft cap, never hard"*, and D25 is not a cap that
was softened — it is a price with no ceiling in it at all.

**The row, drafted, in §11.10's own three-column shape:**

| Cap | Value | Verdict |
|---|---|---|
| **Passive-tree unlock price (D25)** | `cost(N) = firstPoints + (N−1)·stepPoints`, 5 / 2 | **No conflict — a soft economic bound, proven, not asserted.** A linear wallet against a quadratic price, so affordable breadth grows as `√Θ` rather than linearly. It is a *cap in shape* by §11's own rule and therefore owes this row, but it refuses nothing: `N(Θ)` is strictly increasing and unbounded, and the whole generic catalog is reachable at `Θ ≈ 221,804`, inside `PowerLadder.MaxIndex ≈ 2.147×10⁸`. **The proof is load-bearing and has three forbidden constructions attached** — no `Math.Min` on the price, no narrowing cast on the budget, no `CanUnlock` that can return false — each with a named test (§2.3). Unlike §11.7's soul faucet, the *sink* here is the scaling half and the faucet is the flat one, and that asymmetry is the design: breadth prices itself while depth stays free |

**Both rows land in one change, with the `inventory.json` mirror.** A §10.2 row without its §11
verdict is half the obligation, and this spec has now been wrong about that once.

> ⚠ **Neither row is machine-checkable, and neither guard will say so.** `guard-power.ps1` misses the
> §10 row for the reason above; nothing at all checks §11 for completeness — §11.10a is the register's
> own record of a verdict that was simply wrong for months. **Both are checklist items in Success
> criteria, not gates.**

---

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter TreeState
dotnet test tests/FusionRpg.Data.Tests --filter TreeNodeState
dotnet test tests/FusionRpg.Guard.Tests
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --targets M1
.\scripts\guard-dal.ps1
.\scripts\guard-power.ps1
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.PassiveTree
```

## Project structure

```text
src/FusionRpg.Core/PassiveTree/State/TreeNodeSet.cs           the immutable owned-set + soul levels
src/FusionRpg.Core/PassiveTree/State/UnlockCostPolicy.cs      cost(N), cumulative(N) - long, checked
src/FusionRpg.Core/PassiveTree/State/TreeStateReconciler.cs   live / retired / unknown classification
src/FusionRpg.Core/PassiveTree/State/PassiveTreeTuning.cs     the tuning record + loader (T5 rejects)
src/FusionRpg.Core/Stats/Aptitudes/PointBudget.cs             + SkillPointsFor (sibling of PointsFor)
src/FusionRpg.Data/Sqlite/RpgStore.TreeState.cs               the only SQL - partial slice on _gate
data/tuning/passive-tree.v1.json                              unlockCost.{firstPoints,stepPoints},
                                                              soulTrack.thetaPerSoulLevelMilli
data/tuning/aptitudes.v{n+1}.json                             skillPointsPerThetaMilliByScope
tests/FusionRpg.Core.Tests/PassiveTree/TreeStateTests.cs
tests/FusionRpg.Data.Tests/PassiveTree/TreeNodeStateTests.cs
```

`RpgStore.TreeState.cs` is a **partial-class slice**, sharing the one connection, one `_gate`, one
`EnsureHotSchema` dispatch and one `Reset()` — the convention `RpgStore.Aptitudes.cs:16-24` argues for
in its own header. A standalone class with its own connection would fork the pipeline and silently
drop out of `Reset()`.

## Code style

```csharp
/// <summary>
/// D25/D36. The Nth node an actor owns costs first + (N-1)*step skill points.
///
/// <para>A SOFT ECONOMIC BOUND, never a ceiling (PS-8): nothing is ever refused, breadth just
/// prices itself, and N(Θ) is strictly increasing and unbounded so every node is reachable at
/// some finite Θ. There is deliberately NO Math.Min here, no narrowing cast on the budget this
/// is compared against, and no CanUnlock that can return false — all three would turn "breadth
/// got expensive" into "breadth silently stopped", a bug with no symptom.</para>
///
/// <para>`long` on both sides: the price is small, but the budget is g*Θ, and at
/// PowerLadder.MaxIndex (~2.147e8) with g=11 that is ~2.36e9 — past int.MaxValue. `checked`
/// even though overflow is structurally unreachable: an unreachable overflow that wraps is
/// still a wrap (CLAUDE.md rule 5).</para>
/// </summary>
public static long CostOfNextNode(long nodesOwned, PassiveTreeTuning tuning)
{
    if (tuning is null) throw new ArgumentNullException(nameof(tuning));
    if (nodesOwned < 0)
        throw new ArgumentOutOfRangeException(nameof(nodesOwned), nodesOwned, "owned node count cannot be negative");

    // unlockCost.firstPoints / unlockCost.stepPoints (R2) — whole skill points, no /1000 anywhere.
    checked { return tuning.UnlockCost.FirstPoints + nodesOwned * tuning.UnlockCost.StepPoints; }
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `an_unowned_node_never_becomes_a_row` | sparsity; the `SaveAllocation` precedent |
| `owned_with_zero_souls_persists` | row presence means owned; `soul_level = 0` is a real state |
| `save_is_delete_then_insert_in_one_transaction` | the store holds the current set, not a change log |
| `total_cost_is_independent_of_purchase_order` | §2's lemma — the reason derive-on-read is safe |
| `rebuying_the_same_set_after_respec_costs_the_same` | no exploit, no hidden tax |
| `reward_per_skill_point_is_flat_when_first_equals_step_times_k_plus_one_over_two` | §2.2's algebra **at constant width only**, and demonstrably not flat at `first = step`. Never evidence about the shipped corpus — §2.2c |
| `reward_per_skill_point_is_within_band_over_every_shipped_archetype_and_every_tier` | §2.2c — reads `tree-plan`'s actual width vectors, asserts at every tier, against `archetype.rewardSpreadMaxRatioMilli`. Green at equality by design |
| `the_skill_wallet_clears_the_tier_it_just_opened_for_every_shipped_archetype` | §2.2d — the same band read from the wallet side; a `g` that does not reproduce from `a·step·k²/s` fails it |
| `an_item_swap_does_not_change_the_next_node_price_net` | §2.1's ratchet, closed |
| `soul_levels_never_count_toward_the_unlock_price` | D3; the two tracks stay separate |
| `an_invalid_red_node_costs_nothing_to_hold` | D8's trap shape, avoided |
| `the_count_is_across_all_trees_never_per_tree` | §2.1's last row |
| `a_missing_scope_rate_is_a_load_rejection_naming_it` | T5, never a default |
| `every_actor_reads_its_own_scope_budget` | D34 — a demon must not read `Θ_player` |
| `an_unknown_node_id_does_not_throw_on_actor_load` | §4 — the `AptitudeAllocation.cs:39` defect not repeated |
| `an_unknown_node_id_rejects_the_import_naming_every_offender` | §4's boundary, one report |
| `a_retired_node_loads_as_invalid_and_grants_nothing` | D11, never silently repaired |
| `respec_clears_one_scope_key_only` | §5's scoping |
| `respec_is_never_refused` | `RespecPolicy`'s own discipline |
| `batch_load_is_one_query_for_a_six_actor_squad` | §6 — count the connections opened |
| `battle_setup_never_calls_the_single_key_loader` | the seam, guarded |
| `no_math_min_and_no_narrowing_cast_on_the_price_or_budget` | grep test, PS-8 |
| `budget_overflow_throws_never_wraps` | `Θ` at `PowerLadder.MaxIndex` |
| `every_stored_and_derived_magnitude_is_long` | reflection; a `float` or `int` fails |

Mutation set worth adding to `scripts/mutants/`: flipping `first`/`step`, dropping the `checked`, and
turning the batch loader into a loop — each should be caught by a named test above.

## Boundaries

**Always:** store the owned node set and soul level, nothing derived; `long` on both sides of every
comparison; widen before multiplying; `checked` on every product; derive budget and spend on read;
batch the read; reject an unknown node id **once**, at the import boundary, naming every offender;
keep every SQL statement inside `FusionRpg.Data`; keep the `RpgStore` partial-class convention.

**Ask first:** **republishing `g` = `skillPointsPerThetaMilliByScope.commander`** — the shipped `11`
is sized against the measured corner share; §2.2d records an alternative sizing of 19.2 as an open owner question; it is one global
tunable that moves every wallet, so it is the owner's number, not this fold's; the four values of
`skillPointsPerThetaMilliByScope` — the table is required (§3), but
the rates are a balance question residual-fit owns, and *shipping a guess is fine; calling it balance
is not*; ~~**which resource respec costs**~~ **— closed, it is souls (§5.1)** — but **whether a tree
respec shares the species respec counter or carries its own**; whether a respec may ever be
roster-wide rather than per actor.

**Never:** store a price, a spent total, a remaining budget, or any resolved magnitude — that is a
second SSOT that goes stale the moment a coefficient moves; clamp the price or the budget; add a
boolean `CanUnlock` that can return false; count souls or invalid nodes toward the unlock price; count
per tree; throw on an unknown node id during an actor load; add a fifth `AllocationScope` member for
status mastery — **D35 removed that dependency**, and `AptitudeAllocation.Total()` sums *every* enum
member into the aptitude share denominator `decisions.md:103` locks, so a new member is a live
regression, not a slot; join tree state onto the unpaged `ListDemonRoster`; multiply anything by
`P(Θ)` — that is `tree-resolve`'s and only `tree-resolve`'s.

## Success criteria

- [ ] A player with 2,000 actors and 40 owned nodes each stores 80,000 rows, not 3.1 million — proven
      by a test that counts rows after a realistic build.
- [ ] `TotalCostOf` is order-independent, proven by test over shuffled purchase orders.
- [ ] Reward-per-skill-point is flat at every tier for the shipped price **at constant width**, and
      provably not flat for `first = step`.
- [ ] Across `tree-plan`'s shipped archetypes the reward-per-skill-point spread stays inside
      `archetype.rewardSpreadMaxRatioMilli` at every tier and is exactly 1000‰ at tier 10 (§2.2c).
- [ ] `skillPointsPerThetaMilliByScope.commander` reproduces from `g = a·step·k²/s` — **red today at
      `11`, and deliberately so** (§2.2d). It closes when the owner republishes `g`, not before.
- [ ] Every actor's budget reads its **own** scope rate; a demon reading `Θ_player` fails a test.
- [ ] One unknown node id fails the import with a report and leaves every actor loadable.
- [ ] A six-actor squad's tree state loads in **one** query, one lock acquisition, one connection.
- [ ] `scripts/audit-overflow.py` reports no critical finding; no `float` and no narrowing cast on any
      magnitude or budget path.
- [ ] `scripts/guard-dal.ps1` green; no SQL outside `FusionRpg.Data`.
- [ ] §10.2 **row 29**, the **§11.10 caps-register row** (§9.2) and the `inventory.json` mirror row
      all exist before this module is called done — no guard checks any of the three, so they are
      checklist items, not gates.

## Open questions

Three, all real; two of them are owner decisions this module may not make.

1. ~~**Which resource respec costs.**~~ **Closed 2026-09-05, against code — see §5.1.**
   `RespecPolicy.PriceOf` returns `RespecResource.Soul` (`RespecPolicy.cs:46`), `Soul` is the enum's
   only member (`:23`), Hunger is recorded at `:15` as the superseded placeholder, and there are two
   production callers (`RpgStore.SpeciesRespec.cs:176`, `SpeciesBuildEndpoints.cs:91`). D18,
   `decisions.md:103` and the shipped code agree. **What remains open is narrower and is a scoping
   question:** does a passive-tree respec advance the *species* respec counter, or carry its own? Its
   own is the default reading — the two are different subjects and sharing a counter would make a tree
   respec silently reprice a species respec — but it is the owner's to confirm before the counter is
   persisted, because changing it afterwards is a data migration.
2. **What "the tier below is unlocked" means** — at least one node in the tier below, or the tier
   complete? §2.2's calibration assumes **complete**, at the uniform archetype's `k = 4`: the
   conservative, most expensive reading and the only one exact flatness is a property of. The cheaper
   reading roughly halves the effective width, and since `g ∝ k²` (§2.2d) that moves the wallet by
   about **4×**, not 2× — so it is worth deciding **before `firstPoints` and `g` are published**, and
   it is the second input to the same recalibration Open question 3 names.
   Recommendation on the record: at least one node in the tier below **of the same branch** — it
   preserves D10's two-branch identity and rewards a single-branch dive, whose reward-per-point
   gradient already points the right way.
3. **All four values of `skillPointsPerThetaMilliByScope` — and the commander one is no longer
   "derived".** ~~The commander value (11) is derived (`g = 3·s·step·k²/5 = 10.40`, rounded up so the
   wallet clears the gate with a small surplus).~~ **Corrected 2026-09-05, §2.2d:** the correct form
   is `g = a·step·k²/s` under a **share-1.0** build, giving 19.2, while the shipped 10.40 sizes
   the same wallet against the **measured corner share 0.54163** — the two differ by exactly
   `1/0.54163`. **`11` is 10.40 rounded up and is reconciled**; what is open is which build the
   wallet should be sized against, which is an owner call, not an arithmetic fix.
   rounded derivation. At `11` the uniform archetype reaches tier 3 with 57% of the skill points the
   gate it just opened costs to fill. **Republishing `g` is an owner decision** (it moves every
   wallet in the game) and it is the one thing in this spec that must be settled before `first` and
   `step` are published, because the three numbers are calibrated against each other. The other three
   scopes remain unmeasured, exactly as the sibling table's own `_weightsWhy` says of its `{3,4,4,6}`.
   Shipping a guess is fine; calling it balance is not.

## Decisions implemented

| Requirement in this spec | Decision |
|---|---|
| §1 every actor carries its own tree state; sparse storage is mandatory | **D21** |
| §1.1 the `(scope, scope_key)` key; inputs only, never resolved values | **D21**, `decisions.md:103`'s four scopes |
| §2 the Nth node costs `first + (N−1)·step`; per actor; arithmetic | **D25**, **D36** |
| §2 store the owned set, derive budget and spend | **D25**, **D18** |
| §2.1 item-granted points count; invalid nodes and souls do not | **D11**, **D8**, **D3** |
| §2.2 `first = step·(k+1)/2` as a coupling, **valid at constant width only** (§2.2a–d) | **D36**, **D26**, **D29** |
| §2.3 a soft economic bound, provably not a ceiling | **D25** + PS-8 |
| §3 `skillPointsPerThetaMilliByScope` is required | **D34** |
| §3 the grant finally has a spender | **D2** |
| §4 reject once, at the import boundary; retired nodes render red | **D11**, ideal §11.2's *"migration fails hard"* |
| §4 the migration escape hatch is a free full respec | **D18** |
| §5 full reset, one transaction, per actor, never refused | **D18** |
| §7 the soul level is an index offset, never a coefficient multiplier | **D3** + §10.5's linear-in-effort property |
| §8 every balance number is a tunable key with a unit | ideal **§14** |
| Boundaries — no fifth `AllocationScope` member | **D35** (superseding **D19**/**D31**) |
| Boundaries — tier gates read base allocation, not item bonuses | **D12** |

**Belongs to a sibling module, not here:** D4/D5/D6/D7/D8's `H` and `F`, D28's cross-unlock, and every
`P(Θ)` multiply including the `Ws` §10 row (`tree-resolve`) · the record, ids and versioning
([`tree-catalog`](spec-tree-catalog.md)) · D13/D15/D20/D26/D29/D32/D35's gate quantity (`tree-plan`) ·
D14/D16/D22's vocabularies (`tree-language`, `tree-binder`) · D9/D27's roster (`tree-plan`) ·
D17/D23/D30 (`species-tree`) · D33 (`squad-harness`) · D10/D24 (structural inputs this module reads
but does not implement).

**Could not be placed in either of these two specs, and why:** **D1** (free build stays) is a standing
fact about the class system that no passive-tree module implements — it is a constraint, not a
requirement. **D19, D20 and D31 are superseded** — by D35, D26 and D35 respectively — and are
implemented nowhere, by design.
