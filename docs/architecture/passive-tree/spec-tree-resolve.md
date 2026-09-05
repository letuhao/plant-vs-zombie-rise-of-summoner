# Spec: `tree-resolve` — how tree power reaches combat

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-resolve` · **Wave 3** · **Depends on:** `tree-state`, `mechanism-wiring` ·
**Reads:** `tree-catalog` · **Blocks:** nothing (it is the last arithmetic step)

> **This is the only module that multiplies anything by `P(Θ)`** ([passive-tree-map.md](../passive-tree-map.md)
> §"Boundaries between modules"). `tree-plan` emits a plan, `tree-binder` writes coefficients,
> `tree-state` stores effort. Power exists only here.

---

## 1. Objective

Turn one actor's tree state — which nodes are owned, how deep each is in souls, how many points sit
in each tree — into **ordinary derived-channel contributions**, through the seams the shipped
resolver already uses.

Four things happen in this module and nowhere else:

1. **Tier gates** — which nodes are open at all (D26's ladder, read against base allocation).
2. **Cross-unlock** — D28's largest posture-mate credit, exactly one lender, never a sum.
3. **The concentration index** — `H` and the focus multiplier `F` (D4/D5/D8).
4. **The soul→`Θ` read** — `Θ_node = Θ_actor + Ws·soulLevel`, and the single `P(Θ)` multiply.

**Users:** the lawn's derived compose, battle's squad compose, and `tree-surface` (which renders the
gate arithmetic rather than recomputing it).

**Success is measurable:** the same actor, resolved on the lawn and in battle, produces the same
channel totals; `F` never exceeds `Fmax` at any resource level; and no magnitude in this module is
ever held in a type that stops being exact inside playable `Θ`.

---

## 2. How tree power enters combat

### 2.1 It extends the shipped resolver. It does not fork one.

[passive-tree-map.md](../passive-tree-map.md) assumption 2 states this, and the code supports it
without a new path:

| Seam | Shipped today | What `tree-resolve` adds |
|---|---|---|
| Lawn / web derived compose | `ActorHub.ResolveDerived` loops every registered `IActorStatSubsystem` and `DerivedComposer` folds — `ActorHub.cs:52-57`. `AtomDerivedSubsystem` sits at the reserved `foundation.effect` order **350** and turns bound `stat.derived` atoms into `DerivedModifier`s — `AtomDerivedSubsystem.cs:46-61` | A **producer of `BoundDerivedAtom`s**, fanned into the same `boundDerivedAtoms` delegate `ActorHubBootstrap.CreateDefault` already accepts (`ActorHub.cs:139,155`) |
| Battle squad compose | `BattleStatComposer.Compose(setup, traits, equipment)` reads bound `stat.derived` atoms as `BattleChannelMod`s — `BattleStatComposer.cs:99-102`, the shape `TraitAtomSource.FromContainers` (`TraitAtomSource.cs:55-89`) and `EquipAtomSource` (`EquipAtomSource.cs:37-53`) already ship | A **third source of the same shape** — `TreeAtomSource`, emitting `BattleChannelMod(ChannelId, long Amount)` (`BattleModels.cs`, by symbol — that file is under concurrent edit by `battle-tempo`, so it is cited by symbol and never by line) |

| Lawn write | `EntityStatWriter` / Funnel → FA10 | **Nothing.** Tree contributions never touch a Unity field directly; they compose into the same snapshot every other derived producer feeds |

**So there is no parallel combat path, no new subsystem, and no new order band.** A tree node is a
container of atoms (D22), and `stat.derived` is the kind whose entire purpose is direct
derived-channel mods (`AtomKindRegistry.cs:505`). Its runtime matrix is already
`RuntimeSupportMatrix(Full, Full, None)` — lawn and battle both execute it, Sim does not
(`AtomKindRegistry.cs:534`).

`actor-hub-ssot.md` §6.1 states the rule this satisfies: *"a derived write needs both a registered
channel and a registered producer."* Trees adopt `stat.derived` the same way patron, stars, injuries
and contracts are scheduled to.

### 2.2 Why a fan-in and not a new subsystem

**Three subsystems are registered today** — `RpgProgressionSubsystem`, `AptitudeSubsystem` and
`AtomDerivedSubsystem`, at `ActorHub.cs:145,148,155`, the last two behind optional delegates.
**This module adds a fourth to none of them.**

`ActorHub.Register` replaces by `SubsystemId` (`ActorHub.cs:34`), so registering a second
`AtomDerivedSubsystem` would silently evict the first. The two honest options are a composing
delegate or a distinct id at a new order. **Take the composing delegate**: order carries no meaning
between these producers (`DerivedComposer`'s `FlatSum` and `SumIncreased` are commutative, which is
why `AptitudeSubsystem.Order` says so in its own comment at `AptitudeSubsystem.cs:94-95`), and a new
order band would be an entry `actor-hub-ssot.md` §6's registry does not have.

**There is no collision with `mechanism-wiring`, and this is stated so a builder does not
re-litigate it.** That module registers a genuinely new subsystem, `status.derived`
(`spec-mechanism-wiring.md` §5) — a live status's `StatMods` are not atoms, so it cannot be a fan-in
and correctly takes its own id. Because `Register` matches on `SubsystemId` and nothing else
(`ActorHub.cs:34`), the two coexist by construction: `mechanism-wiring` adds a fourth id,
`tree-resolve` adds a producer behind the existing `boundDerivedAtoms` delegate, and neither can
evict the other. Their order relative to each other carries no meaning for the same reason the fan-in
does not — the fold is commutative.

Attribution survives the fan-in because `BoundDerivedAtom` carries `SourceId`
(`AtomDerivedSubsystem.cs:88-89`), which flows into `DerivedContributionBag` and reaches the player
through `ChannelContributions.tsx:10-35`. **Source id convention: `tree.{treeId}.{nodeId}`** — one
row per node, so GG-49 is satisfied by construction and `tree-surface` needs no second attribution
component.

### 2.3 Mechanism nodes

§3.5 of the ideal is a design constraint, not a tuning value: *"a focus build cannot be rescued with
MAGNITUDE. It can only be rescued with MECHANISM."* Mechanism nodes are exactly the deep-tier nodes
whose atoms are **not** `stat.derived` — reflect (`EffectRuntime.cs:491`), threshold triggers
(`PredicateNode.cs:24` + `FactReader.cs:71`), on-hit responses off `OnDamageDealt`
(`BasicAttack.cs:176`).

**Those atoms are `mechanism-wiring`'s to make executable and scorable.** `tree-resolve` owns only
the decision *whether a node's atoms are live for this actor right now* — gate open, exclusion not
firing, depth ≥ 1 — and the magnitude of the ones that carry one. It does not dispatch, trigger, or
execute anything.

---

## 3. Tier gates (D26, D12)

### 3.1 The ladder

```text
req(t) = k · t(t+1)/2       k = tierLadder.reqScalePoints, tunable, default 5
       → 5 · 15 · 30 · 50 · 75 · 105 · 140 · 180 · 225 · 275
```

D26 reconciled the requirement index to the power index. `W(t) = b·t(t+1)/2` over
`req(t) = k·t(t+1)/2` is `b/k` at **every** tier — flat identically, not flat within 11%. D20's
sequence indexed `t(t−1)/2` and made tier 1 a 2× worse deal; sharing one index is the fix, and it is
the reason `k` is a single tunable rather than a table of ten thresholds. **A ten-row threshold table
would let a balance pass break the flatness silently.**

`tierReached` is computed by an **ascending integer loop bounded by the catalog's own authored tier
count**, never by a closed form — solving `k·t(t+1)/2 ≤ g` needs a square root, and a float has no
place on a gate that decides whether content exists. The loop bound is structural (the tree's own
shape, like `SacrificesForStar`'s range) and must say so in a comment: it is a **content bound, not a
progression cap**. Nothing is refused; there is simply no node above the authored depth to buy.

**The authored depth is read from the catalog, never typed here.** D29 sets it at 10 today. Hardcoding
10 would put a second copy of a content decision in the resolver.

### 3.2 The gate reads base allocation, and D12 is true by construction

D12: *"tier gates read base allocation, never item bonuses."* This needs no enforcement code, and the
reason is worth stating because it is easy to "fix" something that is already correct.

**An aptitude is a SOURCE, never a registered channel** — `Aptitude.cs:12-14`: *"`share` normalises
over the actor's own total, so a granted aptitude would dilute the other eleven."* There is therefore
**no item-derived quantity that could inflate a gate**: an item cannot write an aptitude, so it
cannot move the quantity `req(t)` is measured against at all.

D11 (superseding the original D11): **items grant SKILL POINTS, not node unlocks.** Skill points are
the *purchase wallet* — they buy a node whose tier is already open — so under §3.3's corrected
reading they never touch the ladder. Removing the gear removes the points, and the nodes those points
held are **displayed as invalid, never silently repaired** (§13, and test 18). That is a purchase
question, not a gate question, and keeping the two apart is exactly what makes D12 free.

So the gate quantity is **this actor's own aptitude allocation in the tree's gate quantity**, and the
purchase wallet is skill points. `H` reads a third projection again — self-bought node count and soul
levels (§5.2). Three quantities, deliberately, and §3.3 is the table that keeps them apart.

### 3.3 One index, and the other quantities convert into it

> ⚠️ **Corrected 2026-09-05.** This section previously read *"`tree-resolve` gates on ONE index —
> **skill points** spent in the tree,"* and §3.2 widened it to *"points spent in this tree, whatever
> their provenance."* Both were wrong, and this was the most consequential defect in the spec: it
> broke D12's construction, changed what D26's flatness measures, invalidated `tree-plan`'s Θ-sizing
> table and left D28's credit undefined. `tree-plan` §7, `tree-state` §2.2, `squad-harness` §4,
> `tree-surface` §7.2 and the ideal §4/D12 all read aptitude points; this spec was the only one that
> did not, and it is the one that changes.

The red team's F5 finding stands: `req(6) = 105` cannot mean aptitude points, specimen levels,
element mastery and almanac XP interchangeably, because they grow at different exponents.
Half-closed since — specimen level now reads the shared arithmetic curve
(`ssot-power-scale.md` §10.2 row 27), so it and aptitude points finally share a shape.
**`element_mastery` is comments only** (`PointBudget.cs:13,15,22`, which says outright that it *"is
owned by the demon program's `aspect-scope` module and does not exist yet"*) and **`status_applied`
has zero `src/` hits** — both grepped this session.

**Rule: `tree-resolve` gates on ONE index — APTITUDE POINTS allocated to this tree's gate
quantity.** Nodes are *bought* with skill points; tiers are *opened* by aptitude allocation. They are
different currencies, and conflating them is what `tree-plan` §7 calls, by name, the easy mistake.

| | Opens a tier | Buys a node |
|---|---|---|
| Currency | **Aptitude points** in the tree's gate quantity | **Skill points** |
| Read by | `req(t)` — this module, §3.1 | `tree-state`, priced under D25 |
| Can an item feed it? | **No.** An aptitude is a source, not a channel (§3.2) | Yes, under D11 |
| Supply rate | `aptitudePoints(Θ) = 3·Θ` at Commander scope (`tree-plan` §2) | `grant.skillPointsPerThetaMilliByScope` (D34) |

**`grant.skillPointsPerThetaMilliByScope` is the purchase wallet's rate, not the gate's, and this
module never reads it.** That is the whole of D34's involvement here.

A tree whose category earns its gate quantity from a different scope is converted to
aptitude-point-equivalents **before** it reaches this module. `tree-state` owns that conversion; the
resolver never sees a specimen level or a mastery count. A tree whose gate quantity does not exist
yet is not blocked here either: it resolves to zero aptitude points, which resolves to tier 0, which
resolves to no contribution. **Inert, not broken.**

**But "inert" is the state of 27 of the 39 shared trees today** — 6 elemental and 21 status, 1,080 of
the 1,560 shared nodes (ideal §13.4). A permanently-tier-0 tree is arithmetically fine here and
disastrous on a player surface, where it reads as a wall the player failed to climb. This module's
part of the fix is one field: `TreeResolveReport` carries **why** a tree is at tier 0 —
*no aptitude allocated yet* versus *this tree's gate quantity has no producer* — as a value read from
the catalog, never inferred from the zero. Inferring it from a zero would swallow a real bug in the
same state as a known content gap. `tree-surface` §9.1 renders the distinction.

---

## 4. Cross-unlock (D28)

### 4.1 The rule

```text
credit(i) = max{ base(j) : j ≠ i, stanceGroup(j) == stanceGroup(i) }     0 if no mate
gate(i)   = base(i) + credit(i)
```

`base(i)` is **tree `i`'s own aptitude allocation** (§3.3) — the same quantity `req(t)` reads. That
is not an incidental match: the sweep below builds `p` from `pts[id] / total * budget` over the
twelve aptitudes (`tools/HybridViability/Program.cs:362`), so under the superseded skill-point
reading every cell of §4.2's table would have measured something the model never computed.

**Exactly one lender. Never a sum.** The measured model is
`tools/HybridViability/Program.cs:363-372` — the `"largest"` arm at `:367`, and its own comment at
`:372` reading *"the GATE reads the credit"*. Power stays linear per tier; only the gate reads the
credit.

**Bounded by construction.** One mate is `O(1)`, and no k-way build can compound it — that is the
property `full` (a sum) lacks. `full` also flips the ordering, but it compresses every build into
48.6–51.8% ([09-crossunlock-sweep.md](../../research/passive-tree/09-crossunlock-sweep.md)): *"a rule
that gives everyone everything stops discriminating."*

`stanceGroup` is a catalog property, read never re-declared. For the twelve primary trees it is the
shipped `Posture` (`Aptitude.cs:11,38-51`), whose own comment records that a posture is *"READ… never
stored"* — display and grouping vocabulary by design, which is exactly what this is. **A tree the
catalog gives no stance group gets `credit = 0`.** That is the safe default and it is the only one
the measurement covers: every sweep ran over the twelve aptitudes.

### 4.2 The measured range, recorded rather than the claim

D28's benefit is bounded to `Θ ≲ 300`
([16-depth-exhaustion.md](../../research/passive-tree/16-depth-exhaustion.md), re-measured with
D26's ladder and D29's cap):

| Θ | corner | spread | tree power, in-posture-4 ÷ pure |
|---|---|---|---|
| 150 | 49.1% | 48.3% | 0.70× |
| 250 | 48.2% | 47.1% | 0.87× |
| **300** | **47.6%** | **47.6%** | **1.00× — crossover** |
| 600 | 46.3% | 49.6% | 1.00× |

A pure build saturates the authored depth around `Θ≈170`; a spread build around `Θ≈275`. Between
them the spread build is still climbing while the focused one has nothing left to buy, so the gap
closes. Above it every build sits at the top tier of every tree it touches and cross-unlock stops
discriminating at all.

**This is structural, not a tuning miss.** Any finite authored depth saturates under PS-8's endless
`Θ`; ten tiers only decides *when*. The consequence for this module is one line in §5.1: above the
crossover, `H_nodes` is identical for every saturated build, so **only `H_souls` can still tell
builds apart**, which is why `w` is the primary late-game parameter and not a nicety.

---

## 5. The concentration index

### 5.1 The functions

```text
H_nodes  = Σ_i (n_i / Σn)²                n_i = self-bought NODE COUNT in tree i
H_souls  = Σ_i (s_i / Σs)²                s_i = self-spent SOUL LEVELS in tree i
H        = w · H_nodes + (1 − w) · H_souls
F        = 1 + (Fmax − 1) · H
```

Both `w` and `Fmax` are **tunables with units**, never values in code (§8).

> ⚠️ **Corrected 2026-09-05 — the first term used to be order-dependent.** It read
> `H_points = Σ (p_i/Σp)²` with `p_i = self-spent POINTS in tree i`. Under D25 a node's price rises
> with how many you already own **across all trees**, so the same final build bought in a different
> order splits into a different per-tree *point* share — and `H` reads exactly that vector. Two
> players following the same build guide in a different order would end up with a different `F`. Doc
> 10's A13 found it; §16's D18 row asserted the opposite. **Node count is the order-free quantity:**
> the set of nodes you own is a property of the build, not of the route to it, and D25 prices by *how
> many* rather than *which* — the same reason `tree-state` §2.2 can prove the total cost is order-free.
> Souls were never affected: `s_i` is a depth count, not a price.

**Count, not budget share.** The plan also emits `budgetShareMilli` per node, which is equally
order-free and would weight a capstone above a tier-1 node. Count is the choice here because `F` is a
*shape* function over how commitment is spread, and because the readout `tree-surface` §6 prints is
`1/H` — *"about 2 paths"* — which is only honest if `H` is an index over something countable. The
trade-off is stated rather than hidden: **two builds with identical node counts but very different
node potencies read as equally focused.**

**Why the two currencies are blended and not summed** (D8, ideal §3.2): nodes are finite per `Θ`
because the points that buy them are, and souls are unlimited, so a direct sum lets souls eventually
swamp the share vector and node allocation stops affecting focus. One index per currency, blended
once.

**Why no `1/n` normalisation.** The textbook form is `H* = (H − 1/n)/(1 − 1/n)`. At `n = 39` that
term is a rounding error, and dropping it removes every `n` dependence — **so the roster can grow
forever without re-scaling anybody's existing build** (ideal §3.1). A tree with zero investment
contributes zero to `H`. That is the property that lets D27 ship the roster whole and add categories
in any order.

**Empty denominators read zero, never a uniform default.** `Σn = 0` gives `H_nodes = 0`, not `1/n`.
This is the rule `AptitudeAllocation` already states for itself at `:19-22` — *"empty means all-zero
shares, never 1/12 each… treating 'nothing chosen' as 'chose evenly' would silently invent a default
nobody set."* A fresh actor therefore reads `F = 1.000`, which is correct: no commitment, no
compensation.

**`F ∈ [1, Fmax]` is provable at any resource level.** `H_nodes` and `H_souls` are each Herfindahl indices
over a probability vector, so each is in `(0, 1]` when non-empty and `0` when empty; `H` is a convex
combination of two values in `[0,1]`, so `H ∈ [0,1]`; therefore `F ∈ [1, Fmax]`. **A 10× build is
arithmetically impossible rather than merely unlikely** — which is why a bounded multiplier is where
the design puts its convexity (ideal §3.1).

### 5.2 `H` reads self-spent only (D8) — and that is exploitable

D8 as amended: *"`H` reads spent points + souls — self-spent only. Gear-granted points add power,
never focus."* The stated reason is sound: a good off-build drop must never lower your multiplier.

**The red team's F4 finding is not solved, and this spec does not pretend it is.**
[06-red-team.md](../../research/passive-tree/06-red-team.md) §5: D2 lists four acquisition sources —
skill points, aptitude thresholds, items/affixes, demon aspect — and the amendment names **only
gear**. So:

> Self-spend 100% of your points in one tree → `H_nodes = 1` → `F = Fmax`.
> Take all your breadth from gear, aptitude thresholds and demon aspect.
> You now hold a wide build **and** a pure build's focus multiplier.

That strictly dominates an honest pure build: same `F`, more total tree power. It is worse on the two
unnamed sources than on gear — an aptitude-threshold grant is **self-directed** (the player chose
where the aptitude points went, so "not self-spent" is a fiction), and a demon-aspect grant is
per-actor under D21, so *whose `H` it enters* has never been asked.

**What this module builds today.** `H` reads a `selfSpent` projection that `tree-state` supplies,
and the resolver never infers provenance itself. **The set that projection contains is an owner
ruling, not an implementation detail** — §15.1.

The projection's *shape* is fixed even while its *membership* is open, which is exactly what lets the
module be built while the ruling waits:

```text
selfSpent(actor) → per tree i:   n_i   self-bought node count    long, ≥ 0
                                 s_i   self-spent soul levels    long, ≥ 0
```

Four rules on it, all buildable and testable today:

1. **`tree-state` decides membership; the resolver decides nothing.** A node enters `n_i` if and only
   if `tree-state` marks that unlock self-bought. The resolver carries no provenance rule of its own
   and therefore cannot drift away from whatever the ruling settles.
2. **A node counts once, at 1** — never weighted by what it cost. That is the property that makes `H`
   order-free (§5.1), and the ruling must not break it.
3. **A tree with no self-bought node is absent from the vector**, not present at zero — the same rule
   §5.1 states for empty denominators. Membership never invents a row.
4. **Until the ruling lands, membership is: nodes bought with skill points the actor spent directly,
   and soul levels the actor bought directly.** Item-granted, aptitude-threshold and demon-aspect
   unlocks are excluded, and a test asserts that exclusion is a **stated rule** rather than an
   accident — so widening it later is a one-line change in `tree-state` with a golden that moves,
   not an archaeology exercise.

`n_i` and `s_i` are `long` under §7.2 rule 1, even though neither plausibly nears `int`'s ceiling: a
count on a PS-8 track does not get a narrower type because today's numbers are small.

### 5.3 What `F` multiplies

**`F` multiplies every tree-derived contribution — magnitude and contest alike.** Two reasons, and
the second is the load-bearing one:

1. D6: the multiplier applies to all trees equally. An exception per read mode would make the Focus
   readout a half-truth the player has to learn (§tree-surface L6).
2. **`F` is `Θ`-invariant**, so it cannot violate §2's theorem. That theorem is *"in a contest whose
   sigmoid divisor is constant, the power curve must be **linear in the index**"*
   (`ssot-power-scale.md` §2) — it forbids a geometric *curve in `Θ`*, because a fixed one-level gap
   then grows without bound. `F` is a bounded constant in `Θ`: `F · (c + m·Θ)` is still linear in `Θ`,
   a one-step gap is worth the same at `Θ=10` and `Θ=10,000`, and at parity both sides carry their
   own `F` so it cancels. The worst `F` can do to a contest is scale a fixed gap by ≤ `Fmax`.

**`F` never multiplies anything that is not tree-derived.** Aptitude contributions, item affixes,
statuses and shields are untouched — `F` is a shape function over *how tree commitment is spread*,
and applying it to a channel total would silently scale every other producer that wrote to it.

### 5.4 `F` is provisional, and this module must not hide that

Ideal §15: *"Whether `F` survives — kept provisionally, pending a D25-inclusive, squad-scope sweep
(D33)."* §3.5 measured that no `Fmax` reverses the concentration ordering; D25 is the mechanism that
did (2.3×), and the two have never been measured together.

**Design consequence:** `Fmax = 1000‰` (i.e. `F ≡ 1.0`) must be a legal, tested configuration that
removes `F` from the arithmetic entirely without removing a code path. If `F` is withdrawn later it
is a tuning change, not a refactor.

---

## 6. The reads, and which ladder each one takes

### 6.1 PS-3, applied line by line

> **Rule PS-3. Contests read `Θ`. Magnitudes read `P(Θ)`. Never the other way round.**
> (`ssot-power-scale.md` §4.6)

| This module's read | Reads | Because |
|---|---|---|
| A node's magnitude contribution (`combat.power.*`, `combat.defense.*`, `combat.shield.*`, hp/atk/defense — `GameUnits`) | **`P(Θ_node)`** | It is a magnitude. This is the module's one `P(Θ)` multiply |
| A node's contest contribution (`combat.accuracy.*`, `combat.dodge.*`, `combat.crit.rate.*`, `combat.crit.resist.*`, `status.power.*`, `status.resist.*`) | **`Θ_node`, linearly** | §2's theorem. A geometric read here makes a fixed depth gap unboundedly decisive |
| `Θ_node = Θ_actor + Ws·soulLevel` | **additive inside `Θ`**, before either read | Row 18's precedent (`ssot-power-scale.md` §10.2, `thetaOffset`): *"lives inside `Θ` itself, additive, before `P(Θ)` runs — not a bounded display value scaled a second time"* |
| `tierReached` from `gate` | **neither** | A threshold on **allocated aptitude points** (§3.3). Not derived from a level, so it is not a power-shaped scale |
| `F` from `H` | **neither** | A bounded ratio over a share vector. `Θ`-free by construction (§5.3) |

**No new `f(level)` is written anywhere in this module.** Writing one is the exact defect
`ssot-power-scale.md` §10's anti-duplication clause exists to end — three incompatible curves shipped
simultaneously before it.

### 6.2 The soul→`Θ` read, and why the coefficient never moves

`tree-binder` bakes a **coefficient**, not a magnitude — which is what lets one static catalog be
correct for every player at every `Θ` (map §"Boundaries"). Depth buys a larger `Θ` to read the ladder
at; it never scales the coefficient.

**The arithmetic that makes this the right choice, not a preference.** The soul track's cost is
arithmetic (D3): cumulative cost to depth `L` is `Σ(first + (k−1)·step) ≈ (step/2)·L²`. With
`Θ_node = Θ + Ws·L`, the reward is `P(Θ + Ws·L) ≈ (B/2)(Θ + Ws·L)²` — quadratic in `L`. Cost and
reward are both quadratic, so **power is linear in effort** and `ssot-power-scale.md` §10.5's promise
holds: an hour of play buys the same absolute power at hour 5 and hour 500.

Scaling the coefficient instead gives linear reward against quadratic cost — power ∝ √effort, which
is §4's claim failing. That was red-team F9; this is its answer.

**This needs one new `ssot-power-scale.md` §10.2 row** for `Ws`, by row 18's precedent. It is a
reviewed change to that document and this spec requests it rather than making it.

> ⚠️ **`guard-power.ps1` cannot catch the absence of that row.** G2/G3 key on a parameter named
> `level`/`lvl`/`index` (`guard-power.ps1:74`); `soulLevel` matches, but a method named
> `ThetaForNode(int thetaActor, long soulLevel)` still needs its file listed in `inventory.json` or
> G3 fails — while a helper whose parameters are named `thetaActor`/`depth` sits in the same blind
> spot `DropVolume` already occupies (`ssot-power-scale.md` §10.2 row 28: *"it passed
> `guard-power.ps1` only because the parameter is spelled `thetaActor` rather than `level`"*). **The
> row goes in deliberately. A green guard is not evidence it is there.**

---

## 7. Numeric rules

### 7.1 The thresholds, measured

CLAUDE.md's table, with the `Θ` at which each type stops being able to hold a magnitude on the
shipped curve (`B = 0.4`):

| Type | Exact-integer ceiling | First `Θ` that breaks it | Use in this module |
|---|---|---|---|
| `float` | 16,777,216 | **232** | **Never.** Fails inside normal play, and it is non-deterministic |
| `int`, per-mille | 2,147,483,647 | **3,213** | **Never** for a magnitude or a per-mille coefficient |
| `int`, whole units | 2,147,483,647 | 103,557 | Only for a tier index and a node count, both bounded by the catalog |
| `double` | 9,007,199,254,740,992 | 6,710,822 | Only at the hand-off in §7.3, which is a decided exception |
| `long` | 9,223,372,036,854,775,807 | 214,748,300 | **The default for every magnitude in this module** |

### 7.2 The four rules, and where each one bites here

1. **`long` for any magnitude.** Every node contribution, every intermediate, every emitted amount.
2. **Never `float`.** Row 1.
3. **Widen before multiplying.** `(long)a * b`, never `(long)(a * b)` — the cast binds to the result,
   so the multiply has already overflowed.
4. **Divide by 1000 last, exactly once.** Two per-mille factors (`kMilli`, `fMilli`) compound against
   a `P(Θ)` that legitimately reaches into the quintillions, so a `long × long × long` chain
   overflows on the *intermediate* even when the true answer fits. `AptitudeReadFunctions` already
   documents and solves this (`AptitudeReadFunctions.cs:18-27`): **`decimal`, 96-bit exact integer
   precision, is the widening type**, and it throws its own `OverflowException` rather than wrapping.
   Adopt it verbatim — same trap, same fix, no second copy of the reasoning.
5. **Overflow throws, never wraps.** No silent `unchecked` on any magnitude path.

### 7.3 Two shipped types this module must not inherit

| Shipped | Where | Why `tree-resolve` cannot use it |
|---|---|---|
| `ValueSpec.PowerLadderKMilli` is an **`int` per-mille** | `ValueSpec.cs:92` | Per-mille rounds a tier-1 node with **17% error** — larger than one tier step, which destroys D26's exactly-flat reward-per-point. `mechanism-wiring`'s `PowerLadderKMicro` sibling (three lines) is the fix; until it lands, tree coefficients are read from the catalog as `long` per-million, never through `ValueSpec` |
| `AtomCompiler` narrows the compiled ladder value to **`int`** — `checked((int)((long)spec.PowerLadderKMilli * pThetaValue / 1000))` | `AtomCompiler.cs:464` | It is `checked`, so it throws rather than wraps — correct behaviour, wrong width. A tree magnitude reaches `int`'s ceiling at `Θ` 103,557 and would start *throwing* on a legal build. `tree-resolve` carries its own `long`/`decimal` read and never routes a tree magnitude through the compiler's ladder branch |

**The one decided exception.** `BoundDerivedAtom.Amount` and `DerivedModifier.Value` are `double`
(`AtomDerivedSubsystem.cs:88-89`, `DerivedModifier.cs:6`) — the lawn's derived fold is `double` end to
end, and `ssot-power-scale.md` §10.7 decided in 2026-08-23 that it stands. So: **compute in
`long`/`decimal`, throw on overflow, hand over a `long` that is exactly representable as `double` up
to 2^53 — first failing `Θ` 6,710,822.** The battle path takes `long` natively
(`BattleChannelMod.Amount` — cited by symbol, §2.1) and inherits nothing. This is recorded so the
choice is visible, not discovered later by a golden that moved.

---

## 8. Tunables

**One file: `data/tuning/passive-tree.v1.json`**, following `tunables-ssot.md` §2's shape. **Every
key carries its unit** (T6), and a missing key is a **load rejection naming it**, never a built-in
default (T5). The file does not exist yet — checked this session, `data/tuning/` has no
`passive-tree*` — so this module creates it, and there is no `passive-tree-gen.v1.json` variant:
every key below lives in this one file, under these names.

| Key | Unit | Default | Why it is tunable |
|---|---|---|---|
| `concentration.fmaxMilli` | multiplier, per-mille | `1200` (D5's 1.15–1.25 band) | A balance pass moves it. `1000` must be legal and tested — §5.4 |
| `concentration.wMilli` | 0..1 blend weight, per-mille | `500` | **The primary late-game parameter** (D8, [16](../../research/passive-tree/16-depth-exhaustion.md)). At `w = 1000‰` the design has no late game, because above the crossover `H_nodes` is identical for every saturated build |
| `tierLadder.reqScalePoints` | **aptitude points** | `5` | D26's `k`, and the unit is the corrected one (§3.3). Its pairing with linear per-tier power is what makes reward-per-aptitude-point exactly `b/k` |
| `soulTrack.thetaPerSoulLevelMilli` | `Θ` per soul level, per-mille | **unmeasured** | `Ws`. Needs its own `ssot-power-scale.md` §10.2 row before it ships (§6.2) |

**Not tunables, and each says why:**

- **The authored tier count** is catalog data, not config and not code (§3.1).
- **`b`** — aptitude-point-equivalents per tier — is **not a balance dial**. §3.5 swept it across
  `{0, 2, 5, 10, 20}` and no value reverses the ordering; it is a content-density choice.
- **The loop bound** in `tierReached` is structural, and its comment must say so.
- **`grant.skillPointsPerThetaMilliByScope`** is a real tunable and it is **not this module's** — it
  prices the purchase wallet in `tree-state` and lives in `aptitudes.v{n}.json` (D34). Listing it here
  would be the second copy that §3.3 exists to prevent.

`TreeReadFunctions` is a **Math file and therefore a balance surface** (T3) — no bare literal in it,
only named tunables and named structural constants.

---

## 9. Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter TreeResolve
dotnet test tests\FusionRpg.Core.Tests --filter TreeConcentration
.\scripts\guard-power.ps1              # one ladder, pin holds, no private f(level)
.\scripts\guard-single-writer.ps1      # combat writes only via EntityStatWriter
.\scripts\guard-funnel-delta.ps1       # HP deltas only via Funnel -> FA10
python scripts\audit-overflow.py --targets A3
python scripts\audit-magic-numbers.py --domain passive-tree
.\scripts\coverage.ps1 -Namespace FusionRpg.Core.PassiveTree
```

---

## 10. Project structure

```text
src/FusionRpg.Core/PassiveTree/TreeResolver.cs          Resolve + ResolveForBattle, the two adapters
src/FusionRpg.Core/PassiveTree/TreeReadFunctions.cs     the magnitude and contest reads. Balance surface: no literals
src/FusionRpg.Core/PassiveTree/TierGate.cs              req(t), tierReached, the authored-depth bound
src/FusionRpg.Core/PassiveTree/CrossUnlock.cs           D28's largest-mate credit
src/FusionRpg.Core/PassiveTree/ConcentrationIndex.cs    H_points, H_souls, H, F
src/FusionRpg.Core/PassiveTree/TreeResolveReport.cs     the diagnostic projection tree-surface renders
src/FusionRpg.Core/PassiveTree/TreeTuning.cs            the four tunables, loaded not defaulted
src/FusionRpg.Core/PassiveTree/TreeTuningHub.cs         host wiring, PowerTuningHub's shape
src/FusionRpg.Core/Battle/TreeAtomSource.cs             the battle adapter, TraitAtomSource's shape
data/tuning/passive-tree.v1.json                        the four keys
tests/FusionRpg.Core.Tests/PassiveTree/TreeResolverTests.cs
tests/FusionRpg.Core.Tests/PassiveTree/ConcentrationIndexTests.cs
tests/FusionRpg.Core.Tests/PassiveTree/TierGateTests.cs
tests/FusionRpg.Core.Tests/PassiveTree/TreeOverflowTests.cs
```

**`src/FusionRpg.Core/PassiveTree/` is shared with `tree-state` and `tree-catalog`.** This module adds
files to it; it does not own the folder.

**No SQL.** `tree-state` owns persistence; `guard-dal.ps1` enforces that SQL lives only in
`FusionRpg.Data`, and this module reads a state object handed to it.

**No Unity.** Nothing here reads a Unity field, so `guard-secondary-no-unity.ps1` stays green and
standalone-first holds — the same check `07-learnability-and-surface.md` §8 already ran for the whole
design.

---

## 11. Code style

```csharp
/// <summary>
/// One node's magnitude contribution: k · P(Θ_node) · F, in whole game units.
///
/// <para><c>decimal</c> is the widening type, not <c>checked long</c>, for the reason
/// <see cref="Stats.Aptitudes.AptitudeReadFunctions"/> already documents at its class level: two
/// independent per-mille factors (<paramref name="kMicro"/>'s scale and <paramref name="fMilli"/>)
/// compound against a <paramref name="pThetaNode"/> that legitimately reaches the quintillions, and a
/// long*long*long chain overflows on the INTERMEDIATE product even when the true answer fits a
/// <c>long</c> comfortably. <c>decimal</c> has 96-bit exact integer precision and throws its own
/// <see cref="OverflowException"/> rather than wrapping, so this is "throws, never wraps" end to end
/// while never spuriously rejecting an input whose true answer was always representable.</para>
///
/// <para>The division is by the two factors' COMBINED scale (1_000_000 × 1_000), done ONCE, LAST —
/// CLAUDE.md's rule 4. Per-mille intermediates are 1000x closer to the ceiling, so dividing early is
/// how a correct-looking implementation loses a tier's worth of precision.</para>
/// </summary>
public static long Magnitude(long kMicro, long pThetaNode, long fMilli)
{
    if (kMicro < 0) throw new ArgumentOutOfRangeException(nameof(kMicro), kMicro, "a node coefficient must not be negative");
    if (pThetaNode < 0) throw new ArgumentOutOfRangeException(nameof(pThetaNode), pThetaNode, "P(Θ) must not be negative");
    if (fMilli < 1000) throw new ArgumentOutOfRangeException(nameof(fMilli), fMilli, "F is bounded below by 1.000 — see spec §5.1");

    decimal raw = (decimal)kMicro * pThetaNode * fMilli;
    decimal rounded = Math.Round(raw / 1_000_000_000m, MidpointRounding.AwayFromZero);

    if (rounded > long.MaxValue || rounded < long.MinValue)
        throw new OverflowException($"tree magnitude overflow: kMicro={kMicro} pTheta={pThetaNode} fMilli={fMilli}");
    return (long)rounded;
}
```

**Resolve is pure and idempotent**, the same seam contract `AptitudeSubsystem.cs:20-23` states: it
holds no state between calls, so two calls with the same inputs produce the same modifiers.

**Memoize by reference, never by an external bump.** `AptitudeSubsystem.cs:32-52` records why: a
manually-cleared generation stamp is invisible to any host that does not know to call it, and the
memo then serves stale state forever. Key on `(Side, TypeId, Θ)` plus the **tree-state reference**,
and re-resolve whenever that reference differs. Bounded growth holds — a changed state overwrites
that key's single slot.

**Perf.** The perf SSOT is settled by measurement: lag is main-thread scans and uncached resolves
(`runbook/perf-probe-plan.md`), so an uncached tree resolve on the hit path is the failure mode to
design against, not the transport. The work is small — a build at `Θ=100` owns 13–36 nodes under D25
([14-learnability-at-scale.md](../../research/passive-tree/14-learnability-at-scale.md) §5.1) — but
D21 gives every actor its own state, so the memo is not optional.

---

## 12. Testing strategy

| # | Test | Asserts |
|---|---|---|
| 1 | `Reward_per_point_is_flat_at_every_tier` | `W(t)/req(t)` is identical for `t = 1..authoredDepth`. D26's whole reason for existing — red under D20's index |
| 2 | `Tier_gate_reads_the_catalog_depth_not_a_literal` | Change the catalog's authored depth; `tierReached` follows. No `10` in the resolver |
| 3 | `Cross_unlock_credits_exactly_one_mate` | Three mates at 40/30/20 credit **40**, not 90. The largest-mate rule, not a sum |
| 3a | `Cross_unlock_credit_equals_max_and_differs_from_sum_on_the_same_fixture` | The same mate vector run through both readings, asserting the two answers are **different** and that the resolver returns `max`. A `max`-vs-`sum` swap is invisible on a one-mate fixture, which is how it survives a refactor |
| 3b | `The_gate_reads_aptitude_points_and_never_the_skill_wallet` | Move the actor's skill-point balance by any amount, including to zero, and `tierReached` does not change; move one aptitude point and it does. §3.3, and D12 made executable rather than argued |
| 4 | `Cross_unlock_cannot_compound_across_k_trees` | A four-of-one-stance build's total credit is bounded by its own largest tree |
| 5 | `A_tree_with_no_stance_group_gets_no_credit` | Element/status trees resolve with `credit = 0` |
| 6 | `F_never_exceeds_Fmax_at_any_resource_level` | Swept over degenerate shapes at four magnitudes of nodes and souls. §5.1's proof, made executable |
| 6a | `F_is_bounded_by_one_below_and_Fmax_above_for_every_share_vector` | **Both** bounds, over generated vectors — 1 tree, 39 trees, one-hot, uniform, and long-tailed. Test 6 asserts only the ceiling; §5.1's proof is two-sided and a floor breach is the one that would silently *shrink* a build |
| 6b | `H_stays_within_zero_and_one_and_reads_zero_when_empty` | The Herfindahl bound the `F` proof rests on, asserted on `H_nodes`, `H_souls` and the blend separately — so a broken term cannot hide inside a blend that still lands in range |
| 6c | `H_is_identical_for_two_shuffled_purchase_orders` | The same final node set, bought in reverse order under D25's rising price, produces a byte-identical `H`. §5.1's correction, and the property that makes `F` a build's value rather than a route's |
| 7 | `Empty_allocation_reads_H_zero_and_F_one` | Never `1/n`. `AptitudeAllocation.cs:19-22`'s rule, applied here |
| 8 | `F_is_theta_invariant` | The same build at `Θ=10` and `Θ=10,000` produces the same `F`. §5.3's argument, and what keeps §2's theorem true |
| 8a | `A_one_tier_contest_gap_is_worth_the_same_at_every_theta_with_F_applied` | §5.3's Θ-invariance argument stated as the property it actually protects: `F · (c + m·Θ)` is linear in `Θ`, so a fixed depth gap on a contest channel is constant across `Θ`. Test 8 pins `F` itself; this pins the theorem `F` must not break, which is what a refactor would take out |
| 9 | `Fmax_of_one_thousand_permille_removes_F_entirely` | `F ≡ 1.0`, byte-identical output to a build with the term deleted. §5.4 |
| 10 | `Souls_move_theta_never_the_coefficient` | Depth 0 → 9 changes `Θ_node`, and the emitted coefficient is unchanged |
| 11 | `Power_is_linear_in_effort_across_the_soul_track` | Cumulative soul cost against `P(Θ_node)` holds a constant ratio. §6.2, and §10.5's promise |
| 12 | `Contest_channels_read_theta_linearly` | A depth gap on `combat.crit.rate.*` is worth the same at `Θ=10` and `Θ=10,000`. PS-3 |
| 13 | `Magnitude_overflows_throw_and_never_wrap` | At the `Θ` the §7.1 table names, per type. Red on `int`, red on `float` |
| 13a | `The_soul_read_throws_rather_than_wrapping_at_long` | `Θ_node = Θ_actor + Ws·soulLevel` on a soul level large enough to overflow `long` throws, and the widening happens before the multiply — `(long)Ws * soulLevel`, never `(long)(Ws * soulLevel)`. PS-8 makes an unbounded soul level a legal input, so this is a reachable path, not a theoretical one |
| 14 | `Divide_happens_once_and_last` | A per-mille-first implementation is measurably wrong at tier 1 by more than one tier step |
| 15 | `Lawn_and_battle_resolve_to_the_same_totals` | The two adapters over one resolver. The parity shape `TraitAtomSource` already proves for traits |
| 16 | `Every_contribution_carries_its_node_source_id` | `tree.{treeId}.{nodeId}`, so GG-49 attribution is not a later retrofit |
| 17 | `An_excluded_node_contributes_zero_and_is_reported` | Nothing refunded, nothing silently repaired, winner named. D14 |
| 18 | `A_gate_that_closed_invalidates_rather_than_repairing` | Points withdrawn → the node reports invalid and contributes zero. D11 |
| 19 | `Missing_tunable_is_a_load_rejection_naming_the_key` | T5. No built-in default |
| 20 | `Memo_self_corrects_on_a_changed_state_reference` | No external bump. `AptitudeSubsystem.cs:32-52`'s lesson |

**Mutation, not just coverage.** `.\scripts\mutate.ps1` with a `passive-tree` set — the concentration
and gate arithmetic is exactly the shape where a covered line asserted by nothing is worth nothing.
Four mutants to write by hand: swap `max` for `sum` in cross-unlock (tests 3 and 3a must go red);
swap the divide order in the magnitude read (test 14); read the skill-point wallet instead of the
aptitude allocation in the gate (test 3b); and read *points paid* instead of *node count* in
`H_nodes` (test 6c — the defect this spec shipped in draft, so the mutant is a regression pin, not a
hypothetical).

---

## 13. Boundaries

**Always**

- Contribute through `IActorStatSubsystem` / bound atoms, and let `DerivedComposer` fold.
- Emit `long`. Widen with `decimal` before multiplying. Divide once, last. Let overflow throw.
- Read `P(Θ)` through the shared `PowerLadder`, never a local curve.
- Credit exactly one posture-mate.
- Carry a `SourceId` per node on every contribution.
- Read `stanceGroup` and the authored depth from the catalog.
- Gate on **aptitude points** in the tree's gate quantity, and never on the skill-point wallet (§3.3).
- Read `H` from an order-free projection — node count and soul levels, never points paid (§5.1).

**Ask first**

- **Which acquisition sources `H` counts as "self-spent"** (§5.2, §15.1). This is an owner ruling and
  it changes what a build is worth.
- **`Ws`'s value**, and its `ssot-power-scale.md` §10.2 row — the row is a reviewed change to that
  document, not something this module writes.
- Applying `F` to anything that is not tree-derived. The answer is expected to be no.

**Never**

- Write a new `f(level)`. `ssot-power-scale.md` §10's inventory is closed.
- Read `P(Θ)` for a contest, or `Θ` for a magnitude (PS-3).
- Cap a magnitude. The authored tier count is a content bound, not a ceiling, and it says so in a
  comment (PS-8).
- Clamp an overflow. Absolute bounds throw.
- Hold a magnitude in `float`, or in `int` past the §7.1 thresholds.
- Sum cross-unlock credits, or credit a second mate.
- Let a skill point — however it was granted — move a tier gate. That is D12, and §3.3 is why it costs
  no enforcement code.
- Weight a tree's entry in `H` by what its nodes cost. Count, not price (§5.1).
- Let `F` scale a channel total rather than this module's own contributions.
- Persist a resolved channel value. `stat-system.md`'s invariant: save inputs, not computed totals.
- Read PvZ's current state, or make a tree feature depend on PvZ representing a concept. Tree
  mechanics resolve in the RPG layer.

---

## 14. Success criteria

1. A tree contribution reaches a lawn entity and a battle actor **through the seams that already
   exist**, with no new subsystem, no new order band, and no second combat path.
2. `W(t)/req(t)` is identical at every tier, measured, not asserted.
3. Cross-unlock credits one lender and is bounded by construction — provable, not swept.
4. `F ∈ [1, Fmax]` at every resource level — **both bounds, asserted from the Herfindahl bound rather
   than swept** — and `Fmax = 1000‰` removes it cleanly.
4a. `H` is identical for two shuffled purchase orders of the same final build, so `F` is a property of
   the build and not of the route to it.
4b. A tier gate moves when one aptitude point moves and never when the skill-point wallet moves.
5. Power is linear in effort across the soul track, with the coefficient fixed.
6. Every magnitude is `long` end to end, and every overflow throws at the `Θ` the table names.
7. Every number a balance pass would move is a key in `data/tuning/passive-tree.v1.json` with its
   unit in its name.
8. `tree-surface` can render the gate, the lender, `H`, `F` and the excluded nodes from
   `TreeResolveReport` **without recomputing any of it**.

---

## 15. Open questions

### 15.1 D8's self-spent rule is exploitable, and it is not this spec's call

Stated in full in §5.2. `H` should arguably read every point the player *chose* and exclude only
points they did not — gear is chosen, so letting it move `F` is a trade-off rather than a trap, and
the off-build-drop objection dissolves because nobody is forced to equip it. **If gear stays excluded,
the rule must be written over all four D2 sources explicitly**, because "self-spent" has no defined
meaning for a threshold grant.

Owner ruling needed. Everything else in this module is buildable while it is open, because the
resolver reads a projection rather than inferring provenance.

### 15.2 `w`'s value cannot be measured by the shipped model

`w` is a tunable, so its value does not block this spec. But **`tools/HybridViability` models tier
power only** and cannot see the soul track — which above `Θ≈300` is precisely the half that is still
growing. Any `w` sweep run today reports a saturated late game because the model is blind to the
thing that carries it. Teaching the model the soul track is `squad-harness`'s neighbour, not this
module's, and it is worth booking rather than discovering.

### 15.3 The stance group for non-aptitude trees

§4.1 defaults a tree with no declared group to `credit = 0`, which is safe and matches every
measurement taken. Whether the six elements, twenty-one statuses and the demon families should have
groups of their own is a `tree-catalog` content question with a balance consequence, and it is
cheaper to answer once the harness can measure it.

---

## 16. Decisions implemented

| Decision | What this module does about it |
|---|---|
| **D3** two tracks per skill | Points decide *whether* a node contributes (§3); souls decide *at what `Θ`* it reads (§6.2) |
| **D4** bounded Herfindahl multiplier | §5.1 |
| **D5** `Fmax` a small nudge, tunable | §8; and `1000‰` is a legal configuration (§5.4) |
| **D6** the multiplier applies to all trees equally | §5.3 — and to both read modes, for the `Θ`-invariance reason |
| **D7** hybrids stay Neutral | Nothing in this module penalises a hybrid; `F` compensates focus, it does not tax breadth |
| **D8** `H` reads self-spent points **and** souls | §5.1's blend over **node count** and soul levels — points *paid* are order-dependent under D25 and cannot carry `H`; §5.2's projection, and §15.1's open ruling on its membership |
| **D11** items grant points, not node unlocks | §3.2 — the points are **skill** points, so they buy nodes and never move a gate; withdrawn points invalidate rather than repair (test 18) |
| **D12** tier gates read base allocation | §3.2 — true by construction, with the construction named, and §3.3 is why the construction actually holds: the gate reads an aptitude, which no item can write |
| **D13** deterministic plan first | This module consumes the plan's ladder; it decides no shape |
| **D14** printed exclusion as a runtime no-op | §13, test 17. Contribute zero, name the winner, refund nothing |
| **D18** respec is a full reset | No **unlock** here is order-sensitive, so no orphaned-unlock case exists to handle. ~~Nothing here is order-sensitive.~~ That was too wide: D25 made the *point-share* vector order-dependent, which is why `H` reads node count instead (§5.1) |
| **D21** every actor carries its own tree state | The resolver is per-actor and memoized per actor; nothing is global |
| **D22** passives compose from the shipped atom catalog | §2.1 — `stat.derived` and the mechanism kinds, no passive-specific vocabulary |
| **D24** the catalog is static and shared | Coefficients are read, never rolled. The only per-actor thing is state |
| **D25** unlock cost rises with nodes owned | Priced in `tree-state`; this module simply resolves whatever was bought |
| **D26** `req(t) = k·t(t+1)/2` | §3.1, test 1. `k` is in **aptitude points** (§3.3), so `W(t)/req(t) = b/k` is reward per aptitude point — the same unit `tree-plan` §2 sizes the ladder in |
| **D28** cross-unlock credits one largest mate | §4, tests 3–5, with the measured `Θ ≲ 300` bound recorded |
| **D29** 10 tiers × 2 branches | Read from the catalog, never typed here (§3.1) |
| **D33** squad scope | Not this module's to answer; every number it reads is a tunable the harness can move |
| **D34** `skillPointsPerTheta` becomes per-scope | It prices the **purchase wallet** in `tree-state`. This module never reads it, because the gate is aptitude points (§3.3) |
| **D36** the D25 curve is specified | Its `ssot-power-scale.md` row is `tree-state`'s to add, not this module's |
| **PS-3** contests read `Θ`, magnitudes read `P(Θ)` | §6.1, line by line |
| **PS-8** endless grind | No cap on any magnitude; the authored depth is a content bound and says so |

**Decisions this module does not touch:** D1, D2, D9, D10, D15, D16, D17, D19, D20 (superseded), D23,
D27, D30, D31 (superseded), D32, D35. D16 in particular — **no budget reaches a conversion node
here**, because no atom kind writes an element payload and the failure is silent
(`OverlayCombatCalculator.cs:128-172`). That is a real capability gap, not a wiring one, and it needs
a seventeenth kind through `decisions.md` first.

---

## 17. Design-gate checklist

```
[x] I identified the subsystem(s) this touches — passive trees, stats/derived
    channels, power scaling, effects/atoms, combat damage, battle.
[x] I read every doc in the DESIGN-GATE §1 rows for those subsystems, this
    session: DESIGN-GATE.md (whole), passive-tree-map.md, passive-tree-ideal.md
    (whole, D1-D36), power/ssot-power-scale.md (§2, §4.6, §4.7, §5, §10.1-10.3,
    §11 headings), tunables-ssot.md, CLAUDE.md's overflow table, actor-hub-ssot.md
    §6/§6.1, research/passive-tree 06/07/09/14/16.
[x] I checked for a lock covering this. There is still no passive-tree row in
    decisions.md; the map is approved, the ideal's idea phase is closed, and this
    spec carries "no build authorized" per the map's own status.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — ActorHub's three registrations
    and its CreateDefault opt-ins, AptitudeSubsystem's memo and Order comment,
    AptitudeReadFunctions' decimal widening, PowerLadder's TriangularMilli,
    AtomKindRegistry:534-535's real runtime matrix (Full/Full/None, not the
    all-None the ideal's older text implies), AtomCompiler:465's int narrowing,
    ValueSpec:92's int per-mille, BattleChannelMod's long amount, and the
    largest-mate arm in tools/HybridViability at :363-372.
[x] I read the surrounding section of every rule I quoted — §2's theorem includes
    its "condition is load-bearing" paragraph, which is what makes the
    Θ-invariance argument in §5.3 legitimate rather than convenient.
[~] I tested (not assumed) any constraint I am reporting. PARTIAL: no suite was
    run. Every claim is a read of source. The §4.2 and §7.1 numbers are quoted
    from the research docs and CLAUDE.md's own measured table, not re-derived.
[x] Nothing contradicts a §2 invariant. Standalone-first holds (nothing here
    reads a Unity field). No cap is proposed. One new power-scale row is
    REQUESTED, not assumed (§6.2), and the guard's blind spot around it is named.
[x] Corrections propagated within this document — prose, Structure, Testing and
    Boundaries all carry the same rules, and the two shipped types this module
    must not inherit appear in §7.3, §13 and test 13.
```

---

## 18. Related

- [passive-tree-map.md](../passive-tree-map.md) — the program index
- [passive-tree-ideal.md](../passive-tree-ideal.md) — D1–D36
- [spec-tree-surface.md](spec-tree-surface.md) — the sibling that renders `TreeResolveReport`
- [power/ssot-power-scale.md](../power/ssot-power-scale.md) — §2, §4.6, §10
- [tunables-ssot.md](../tunables-ssot.md) — T1–T7
- [actor-hub-ssot.md](../actor-hub-ssot.md) — §6's subsystem registry
- [09-crossunlock-sweep.md](../../research/passive-tree/09-crossunlock-sweep.md) ·
  [16-depth-exhaustion.md](../../research/passive-tree/16-depth-exhaustion.md) ·
  [06-red-team.md](../../research/passive-tree/06-red-team.md)
