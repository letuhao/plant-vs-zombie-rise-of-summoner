# Spec: `tree-binder`

**Status:** spec, 2026-09-05. Module of [passive-tree](../passive-tree-map.md). No build authorized.

**Module id:** `tree-binder` · **Wave:** 2 · **Depends on:** `tree-plan`, `tree-language`, `tree-catalog`
**Model calls:** none, ever. **This module is the reason no model ever picks a number.**

Predecessor research: [04-number-and-atom-binder.md](../../research/passive-tree/04-number-and-atom-binder.md).
This spec builds on it. Where D29 (10 tiers, 40 nodes) supersedes the 7-tier arithmetic that note was
written against, every figure here is re-derived — and the per-mille defect gets **worse**, not better
(§3.5): at the shipped archetypes' real shares it does not round badly, it stores **zero**.

---

## Objective

Stage 3 of D13's generation order, and deterministic end to end. Take the plan's budget share and the
language stage's chosen affixes, and emit **one integer per node: the node's share of `P(Θ)`, in
per-million.** Compose the affix's atoms, check that every channel it writes is a legal target for the
kind of node it is, and refuse — never repair, never clamp — anything that is not.

**It emits a coefficient, not a magnitude. That is the single design choice that lets one static,
shared catalog (D24) be correct for every player at every `Θ`.** A magnitude would have to be baked at
some `Θ` and would then be either wrong for everyone else or re-scaled a second time, which PS-2
forbids.

---

## Design

### 1. What a passive node IS, structurally

**A node is an affix inside a `skill` container.** Not a bare atom, not a new entity.

`definitions.md` §4a: *"The pool's roll unit is an **affix** — a named bundle of atom refs … drawn
together as one roll."* The affix is the right unit because a real node is usually more than one atom
— §6's reflect node is two `stat.derived` atoms that must arrive together, and one-atom-per-row cannot
correlate two draws.

A node is **not** a container: `container_kind` is a closed six-value set
(`item | trait | skill | species-passive | patron | world-buff`), and a node is a part of a skill, not
a peer of an item.

**A `skill` container uses the fixed core alone.** `spec-container-schema.md:50-56`'s 2026-09-01
amendment made `species-passive` roll and deliberately left `skill` on the core — so
`prefix_rolls = suffix_rolls = 0` and **the draw never runs**. A passive tree uses the affix vocabulary
without inheriting the loot model, which is exactly what D24 asks for. D24 costs nothing to hold;
something would have to change to lose it.

**The layer is reachable.** `InstanceProducer.Compose` (`InstanceProducer.cs:28`) has two production
callers outside its own tests — `SpeciesMaterialiser.cs:55` and `RpgStore.AtomInstances.cs:341`
(`ProduceAndBind`) — so there is a live path from catalog row to bound atom on a real actor. There is
no *"the atom layer is unreachable"* caveat to carry into this module.

### 2. The vocabulary this module composes from — counted in `src/`, not quoted

| | Counted | Declaration | Guard |
|---|---:|---|---|
| Attach points | **7** | `AtomKind.cs:8-30`; `AtomKindRegistry.cs:21` | `AtomKindRegistryTests.cs:31` pins the const to `Enum.GetValues` |
| Kinds | **16** | `AtomKindRegistry.cs:31`; 16 rows at `:476-869` | `AtomKindRegistryTests.cs:30` pins the const to `All.Count` |
| Triggers | **13 declared, 11 authorable** | `AtomKind.cs:95-99`; `AtomKindRegistry.cs:36` | `AtomKindRegistryTests.cs:112` pins the const to `All.Length` |
| Elements | **6** (+ `omni` sentinel) | `ActorElementTypes.cs:3-11`, `:19-29` | |
| Statuses | **21** | `StatusCatalogBootstrap.cs:16-58` | resolved fresh per `Validate` (`AtomKindRegistry.cs:87-91`) |
| Aptitudes / postures | **12 / 3** | `Aptitude.cs:38-51` / `:11` | the count is a product (`:30-35`), never typed |
| Primary stat channels | **23** | `ModifierOp.cs:68-75` | `stat.modify`'s vocabulary (`AtomKindRegistry.cs:71`) |
| Derived stat channels | **267 registered + 9 open prefix families** | `DerivedStatRegistry`; prefixes at `:318-388` | 267 asserted in four test files (`StatTaxonomyTests.cs:183`, `AtomCatalogSsotDriftTests.cs:46`, `ElementHubDocDriftTests.cs:73`, `SeedCatalogTests.cs:28`); resolved fresh per `Validate` (`AtomKindRegistry.cs:84-85`) so the vocabulary widens with no guard edit |
| `UnitClass` | **13** | `StatClass.cs:29-100` | ⚠ the enum's own doc comment at `:26` still says *"ten-class"* — stale by three |
| `StatClass` | **4** | `StatClass.cs:7-22` | explicitly *"orthogonal to `UnitClass`"* |
| Predicate leaves | **12** | `PredicateNode.cs:17-31` | depth ≤ 4, ≤ 16 nodes |

**Two classifications of a channel already exist and both are normative** — the 13-class `UnitClass`
ledger (`design/spec-magnitude-and-units.md` §3) and the sheet's six render states
(`design/spec-derived-stat-sheet.md` §3). `DESIGN-GATE.md:34` warns that inventing a third is a known
past failure. **§4's rule table invents none — it is a *use* of `UnitClass`, keyed by it.**

### 3. The coefficient — budget share to stored integer

#### 3.1 What comes in, what goes out

```text
IN   from tree-plan       treeShareMilli, treeBudgetMilli,
                          per-node budgetShareMilli   -- PER MILLE OF ONE BRANCH, authoritative,
                                                         read as given and never recomputed,
                          potency.maxNodeShareMilli   -- the ceiling, SAME denominator (R5)
     from tree-language   affixIds[] (1..3), and through them kindId, channel, op, trigger,
                          predicate

OUT  one integer per node: kMicro — the node's share of P(Θ), in per-million
```

**A node's roll unit is an affix, and a node carries `affixIds[]` — one to three of them (R6).** §6's
reflect node is two `stat.derived` atoms that must arrive together; one affix id per node cannot
express it, and three is the authored ceiling. Ids are `skill.<treeId>-<branch>-t<tier>-<nodeKey>`
(R3) — no `/`, no dot inside the body, and `nodeKey` is minted once by the plan and **read back** on
regeneration, never recomputed from position.

#### 3.2 The runtime read is already shipped and already correct

```csharp
var pThetaValue = new PowerLadder(powerTuning).Value(theta);
result[key] = checked((int)((long)spec.PowerLadderKMilli * pThetaValue / 1000));
```
`src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs:463-464`

That one line already satisfies four of CLAUDE.md's five overflow rules: `PowerLadder.Value` returns
`long` (`PowerLadder.cs:58`), the coefficient is **widened before multiplying**, the divide is **last
and exactly once**, and the narrowing is `checked` so it **throws, never wraps**. It refuses rather
than guessing when no owner `Θ` is in scope (`:456-462`). The fifth rule — `long` for the *result* —
is the one gap, and §5 prices it.

**This is the right source; `Min/Max` + `contentScale` is the wrong one.** `ContentScale` is applied
once inside `Instantiator` and freezes at instantiation (`ContentScale.cs:5-9`) — correct for a
dropped item, wrong for a passive that must track its owner's `Θ` for the rest of the game.

#### 3.3 The formula, end to end

All inputs are integers. **One division, at the end.**

```text
  treeShareMilli        how much of an actor's power the tree layer carries at full investment
  treeBudgetMilli       this tree's share of that — 1000 for every tree (D15, equal expected value)
  budgetShareMilli      the PLAN'S OWN per-node number: this node's per-mille of ONE BRANCH budget.
                        Read as given
  branches              2 (D29) — the plan's denominator is one branch, a tree is two
  channelAnchorMilli    the channel's own pin at Θ=20, over hp's pin:  pin_ch · 1000 / pin_hp

  num    = treeShareMilli · treeBudgetMilli · budgetShareMilli · channelAnchorMilli
  denom  = branches · 1_000_000
  kMicro = roundHalfAwayFromZero(num, denom)
```

Dimensionally: `k` is a dimensionless fraction of `P(Θ)`. All four numerator inputs are per-mille, so
the product carries `1000⁴ = 10¹²`; expressing the answer in per-million cancels `10⁶`, leaving `10⁶`,
and the branch split contributes the factor 2. Exactly one division, as CLAUDE.md rule 4 requires.

> ### ⛔ The plan distributes. This module reads. `tierWeight(t)` and `weightTotal` are gone (R4).
>
> **`nodes[].budgetShareMilli` is authoritative.** This module does not derive a share from a tier
> weight, from a weight total, or from any other reconstruction of `tree-plan` §3's arithmetic. It
> already listed the field as an input and then never used it; it now uses it, and the two names it
> used instead are **deleted from this spec**, not deprecated.
>
> ~~`num = treeShareMilli · treeBudgetMilli · tierWeight(t) · channelAnchorMilli`, `denom = 1000 ·
> weightTotal`~~ — **superseded, and it was wrong, not merely redundant.** That form distributes tier
> budget ∝ `w[t]·t` while `tree-plan` §3 distributes ∝ `t`, the width vector entering one level down
> at `nodeBudget[t] = tierBudget[t] / w[t]` and never entering the tier column at all. The two are the
> same function **only when `w[t]` is constant** — one of the three shipped archetypes. On
> `gated-deep` the capstone landed at **56‰ of `budgetTotal` against the plan's 91‰**, a factor of
> 3.25 on *the exact node the potency ceiling is calibrated against*, and no test on either side would
> have noticed.
>
> **`weightTotal` was also never structural.** It is `2 · Σ_t w[t]·t` and therefore archetype-dependent
> — **220** for `broad-and-flat`, **178** for `gated-deep`, **252** for `late-crown` (`tree-plan` §3).
> A module that hardcodes one of the three is wrong for the other two. §3.6's old row calling it
> structural is corrected below.
>
> **The ceiling comparison is now like-for-like (R5).** `budgetShareMilli` and
> `potency.maxNodeShareMilli` share one denominator — **one branch** — so the check is a direct
> integer comparison with no conversion. The older ‰-of-`budgetTotal` reading of the ceiling was a
> silent 2× and is superseded.

**Rounding is half-away-from-zero**, the convention `PowerLadder.RoundHalfAwayFromZero`,
`ContentScale.Apply` and `ChannelLadder.RoundHalfAwayFromZero` already share. Nothing new is invented.

**`channelAnchorMilli`, and why it exists.** `P(Θ)` is hp-shaped — pinned at `P(20) = 680`
(`data/tuning/power-scale.v2.json`: `curve.pinIndex 20`, `curve.pinValue 680`). `combat.power` is
atk-shaped and `combat.defense` is defense-shaped, and the same shipped file publishes their pins:
`atk` 92, `defense` 22. So `channelAnchorMilli(atk) = 92·1000/680 = 135` and
`channelAnchorMilli(defense) = 22·1000/680 = 32`. **Derived at bake time from the tuning file's own
pins, never authored** — so a dial change cannot leave it stale.

> **The exact alternative, named rather than hidden.** `ChannelLadder`
> (`src/FusionRpg.Core/Power/ChannelLadder.cs`) computes a channel's own ladder exactly,
> `B_ch = B · pin_ch / pin_hp`, as one `long` numerator over a `long` denominator, rounded once. It is
> *more* correct than folding a constant ratio into `kMicro`: the ratio reproduces the pin exactly at
> `Θ=20` and diverges slightly at low `Θ` (for atk at `Θ=0` it gives 10.8 against `C_ch = 12`),
> converging as `Θ` grows. But `ValueSpec.PowerLadder` reads `PowerLadder.Value` only, so using
> `ChannelLadder` needs a **second reviewed `ValueSpec` source**. **Decision: fold the ratio** — zero
> code change, bounded and monotone error, concentrated where the numbers are smallest — and record
> `ChannelLadder` as the upgrade if the low-`Θ` divergence ever shows up in play.

#### 3.4 Worked example, from the plan's own emitted share

D29: 10 tiers × 2 branches, 40 nodes. Tunables at their placeholder values:
`treeShareMilli = 1000`, `treeBudgetMilli = 1000`.

Node: **`broad-and-flat`, tier 5, offensive branch, first of the two, `combat.power.fire`,
`stat.derived`, op `flat`.** `tree-plan` §3 emits `budgetShareMilli = 45` for it — the tier's 91‰ split
over `w[5] = 2` as 45 and 46, the residual going to the second node. `channelAnchorMilli = 135`, the
atk anchor.

```text
num    = 1000 · 1000 · 45 · 135           =  6,075,000,000
denom  = 2 · 1,000,000                    =      2,000,000
kMicro = round(6,075,000,000 / 2,000,000) =          3,038      (0.3038% of P(Θ))
```

Stored in the catalog: `"kMicro": 3038`. **Nothing else about the magnitude is stored.** Its sibling in
the same tier carries `budgetShareMilli = 46` and stores **3,105** — a real 2‰ difference between two
nodes of one tier, because the plan absorbs its rounding residual and this module does not second-guess
it.

> **Why this number moved, said plainly.** The superseded form gave **3,068** here, from the *exact*
> share 45.4545‰. It is not that 3,068 was mis-computed — it is that the exact share is not what ships.
> `budgetShareMilli` is an emitted integer, and the whole point of R4 is that this module reads what
> the plan emitted rather than reconstructing what the plan meant. Two modules rounding independently
> is how the archetype defect above stayed invisible.
>
> **Consequence, and it is a transfer of ownership rather than a loss.** `kMicro(t)/kMicro(1) == t`
> exactly, for all ten tiers, **is no longer this module's property to assert.** The plan's emitted
> tier column is `18, 36, 55, 73, 91, 109, 127, 145, 164, 182`, which is linear to its own stated
> round-half-up rule and not to exact integer ratio. D26's flatness is asserted **once, in
> `tree-plan`**, over the quantity that is actually authoritative. What this module asserts instead is
> that `kMicro` is an exact, reproducible function of `budgetShareMilli` — see Testing.

At runtime, per actor — `P(Θ)` from `ssot-power-scale.md` §4.5's published table for the shipped dial
(`cMilli 80000`, `AMilli 26200`, `bMilli 400`, verified in `data/tuning/power-scale.v2.json`):

| `Θ_node` | `P(Θ)` | `kMicro · P(Θ) / 1e6` |
|---:|---:|---:|
| 20 (the pin) | 680 | **2** fire power |
| 50 | 1,880 | 5 |
| 100 | 4,680 | 14 |
| 500 | 63,080 | 193 |
| 1,000 | 226,080 | 693 |

**The node is 0.31% of the actor's atk-equivalent power at every `Θ`, by construction.** That is the
whole point: the catalog number is a share, the ladder supplies the scale, and there is exactly one
ladder.

#### 3.5 ⛔ Per-mille does not merely round badly. It stores ZERO, on every archetype

The shipped field is `PowerLadderKMilli`, an `int` in **per-mille** (`ValueSpec.cs:92`). Run the plan's
**real** emitted shares through it. With `treeShareMilli = treeBudgetMilli = 1000` the formula collapses
to `kMicro = round(budgetShareMilli · channelAnchorMilli / 2)` — so `67.5 · share` on the atk anchor
(135) and `16 · share` on the defense anchor (32).

**Lead with the worst of it, because this is not a precision complaint:**

| archetype · branch | anchor | tier | emitted `budgetShareMilli` | exact `kMicro` | exact k (‰) | stored `kMilli` |
|---|---:|---:|---:|---:|---:|---:|
| `gated-deep` · off | 135 | 1 | 6 | 405 | 0.405 | **0** |
| `gated-deep` · def | 32 | 1 | 6 | 96 | 0.096 | **0** |
| `gated-deep` · def | 32 | 2 | 12 | 192 | 0.192 | **0** |
| `gated-deep` · def | 32 | 3 | 18 | 288 | 0.288 | **0** |
| `broad-and-flat` · def | 32 | 1 | 9 | 144 | 0.144 | **0** |
| `broad-and-flat` · def | 32 | 3 | 27 | 432 | 0.432 | **0** |
| `late-crown` · def | 32 | 1 | 18 | 288 | 0.288 | **0** |

**A stored `kMilli` of 0 is a node that grants nothing.** It composes, it validates, it renders in the
sheet, the player buys it with skill points, and it does exactly nothing — no error, no failing test,
no `capped` render state, because nothing was capped. Counted per tree, each branch on its own anchor
as §3.3 requires:

| archetype | dead nodes per tree | where |
|---|---:|---|
| `gated-deep` | **12 of 40** | offensive tier 1 (3 nodes), defensive tiers 1–3 (9 nodes) |
| `broad-and-flat` | **6 of 40** | defensive tiers 1–3 (6 nodes) |
| `late-crown` | **1 of 40** | defensive tier 1 |

**Every shipped archetype has dead nodes, and they sit in the shallow tiers — the ones every new build
buys first.** Across 39 shared trees that is on the order of a few hundred silently inert nodes before
species trees are counted at all.

The rounding error is bad too, on the surviving rows:

| archetype · tier (atk anchor) | emitted share | exact k (‰) | stored `kMilli` | error |
|---|---:|---:|---:|---:|
| `broad-and-flat` · 1 | 9 | 0.6075 | 1 | **+64.6%** |
| `broad-and-flat` · 2 | 18 | 1.2150 | 1 | **−17.7%** |
| `broad-and-flat` · 3 | 27 | 1.8225 | 2 | +9.7% |
| `broad-and-flat` · 5 | 45 | 3.0375 | 3 | −1.2% |
| `broad-and-flat` · 10 | 91 | 6.1425 | 6 | −2.3% |

Tier 1 and tier 2 **both store `1`**: two adjacent tiers become arithmetically indistinguishable, so
"per-tier power grows linearly with tier" — D20's binding pairing rule, which D26 exists to make
exact — is not merely approximate, it is false at the shallow end. Raising `treeShareMilli` does not
fix it; the error is scale-invariant in the ratio and only moves which tier is worst.

⚠ **The predecessor's `−17.0%` is superseded and is deliberately not restated.**
[04](../../research/passive-tree/04-number-and-atom-binder.md) §3.5 computed it against a **seven-tier**
tree, before D29. It does not transfer to ten tiers, and quoting it understates the defect by roughly
4×. Every figure above is computed against the shipped archetypes' own emitted shares.

**The fix, stated as a reviewed change and not smuggled in.** A per-million sibling on `ValueSpec`:

```
PowerLadderKMicro : int      // per-million, mutually exclusive with every other source,
                             // exactly as PowerLadder already is (ValueSpec.Validate:126-135)
```

resolved by the same shape at `AtomCompiler.cs:456-466` with `/ 1_000_000`. At per-million the worst
error above becomes under **0.1%** (half a unit on 614).

**This is a wiring gap in CLAUDE.md's precise sense, not an architectural wall:** the path exists end
to end and is executable today; what is missing is one field's resolution. It is a change to
`src/FusionRpg.Core`, so it is an **Ask first** item, not something this module lands quietly.

> ### ⛔ `PowerLadderKMicro` is THIS MODULE'S, stated because three specs pointed at each other
>
> The seam audit found no owner: `spec-tree-resolve.md` assigns the field to `mechanism-wiring`,
> `spec-mechanism-wiring.md` says *"Nothing else"* and lists no `ValueSpec.cs` among its modified
> files, and this spec claimed it only as one line in an Ask-first list. Three claimants, no owner —
> and a field nobody adds is a corpus of nodes that store `0`.
>
> **`tree-binder` owns `ValueSpec.PowerLadderKMicro` and its `AtomCompiler` arm.** The argument is not
> seniority, it is testability: this module computes the coefficient, so it is the only one that can
> assert what the rounding does to it. `per_mille_stores_zero_on_every_archetype` and
> `per_million_error_is_under_a_tenth_of_a_percent` are both this module's tests and neither can live
> anywhere else. It stays **Ask first** — it widens a shipped contract — but it is asked by this
> module, on this module's schedule, and it blocks this module's first catalog.

⚠️ One honest note on the existing line: C# integer division truncates toward zero, so
`AtomCompiler.cs:464` **truncates** where §3.3's bake **rounds half away from zero**. At per-million
resolution the truncation is under one unit on a magnitude of hundreds and is not worth a second
reviewed change; it must be *stated* in the field's doc comment so nobody later "fixes" one side and
moves every golden.

#### 3.6 Where the balance numbers live

Per `tunables-ssot.md` and invariant 12: `data/tuning/passive-tree.v1.json`, with the standard
`schemaVersion` / `version` / `_meta.owner` / `_meta.rebalance` header.

| Key | Class | Why |
|---|---|---|
| `treeShareMilli` | **tunable** | The single biggest dial the tree layer has. Ideal §3.3 names it undecided: *"its leverage depends on what share of total power trees carry — currently unknown."* §3.4's `1000` is a placeholder chosen so the arithmetic runs |
| `channelAnchorMilli` (per channel family) | **derived, not authored** | computed from `power-scale.v{n}.json`'s own pins at bake time, so a dial change cannot leave it stale |
| `soulTrack.thetaPerSoulLevelMilli` | tunable | §5. **Per-mille** `Θ` per soul level — the canonical name and unit (R2). Value unmeasured |
| `budgetShareMilli` | **neither — an input** | `tree-plan` emits it and this module reads it. Not authored here, not tunable here, not re-derived here |
| tiers = 10, branches = 2 | **structural** | the tree's own shape (D29). Changing one changes what the tree *is*, not how it feels — and the comment must say so |

~~`tierWeightShape`~~ and ~~`weightTotal`~~ are **deleted, not deprecated** (R4). `tierWeightShape`
described a distribution this module no longer performs. `weightTotal` was listed above as
*structural* and is not: it is `2 · Σ_t w[t]·t`, which is **220 / 178 / 252** across the three shipped
archetypes, so a consumer treating it as a corpus constant was wrong for two of the three.
`tree-catalog` keeps it as a per-tree reporting field; nothing in this module's arithmetic reads it.

**No cap anywhere in this chain.** The only ceilings are the absolute bounds in §5.3, and both throw.

### 4. Channel legality — which `UnitClass` values accept a "+X" node

#### 4.1 The rule table

| `UnitClass` | Example channels | A "+X" node? | The rule the binder applies |
|---|---|---|---|
| **`GameUnits`** | `combat.power.*` `combat.defense.*` `combat.shield.{capacity,toughness,pen}.*` `combat.{parry,block}.{strength,shred}.*`; `hp` `maxHp` `atk` `defense` `arm*` | ✅ **the canonical case** | `kMicro · P(Θ_node) / 1e6`, with `channelAnchorMilli` from the channel's own pin |
| **`GameUnitsPerSecond`** | `combat.shield.regen.*` | ✅ | same read, but the anchor must be a **per-second** pin. Folding the hp pin in here silently multiplies the node by the tick rate |
| **`ReciprocalPoints`** | `combat.{penetration,absorption,amplification,reduction}.*` | ✅ **with care** | uncapped points feeding `PierceFactor(d,s) = 1/(1+max(0,d)/s)` — asymptotic. Ladder-scaling is legal (nothing clamps), but the plan must budget the **factor**, not the points: doubling points past the scale buys almost nothing |
| **`PerMilleRatio`** | `combat.reflect.{rate,damage,resist.*}.*` `combat.{parry,block}.{rate,break}.*` | ⚠️ **flat per-mille only, never ladder-scaled** | A bounded ratio, clamped in code (`CombatDamageDispatcher.cs:99,104`; `OverlayCombatCalculator.cs:183-184`). Grant flat points planned against the clamp. `P(Θ)`-scaling saturates it in a few tiers and **kills the soul track on that node**. Exempt from PS-8 by nature — **and the node's comment must say so** |
| **`SigmoidPoints`** | `combat.accuracy.*` `dodge` `crit.rate` `crit.resist` | ⛔ **`+X · P(Θ)` is a design error** | PS-1: `contentScale` never touches a rate input. PS-3: contests read `Θ`, linear. A node here grants `k·Θ_node`, never `k·P(Θ_node)` |
| **`SigmoidMultiplierPoints`** | `combat.crit.damage.*` `crit.resist.damage.*` | ⛔ **worse — it saturates** | Same PS-1/PS-3 rule, **plus** the multiplier is bounded to (1.0×, 2.0×). Past ≈+250 points a node buys almost nothing, so a magnitude node here is worthless at depth *and* its soul track is dead. **Fails silently** — §4.2 |
| **`StatusPotencyPoints`** | `status.{power,resist,duration,durationReduction,intensity,intensityReduction}.*` | ⛔ | `StatClass.Contest` with a declared `CounterpartOf`, so `Θ`-linear. **Also capped:** `status.resist.{dot,cc,contagion}` carry `_categoryResistCap` (`DerivedStatRegistry.cs:106-110`, and again on the sparse prefix path at `:330`), while `status.resist.omni` deliberately does not — two cells of the same row behave differently. **Fails silently** — §4.2 |
| **`Milliseconds`** | durations, `icd_ms` | ⛔ as a magnitude | A duration is not a power magnitude. `P(Θ)`-scaling it produces unbounded uptime, which is a *mechanism* change dressed as a number |
| **`Count`** | `count`, `maxTargets` | ⛔ | Discrete. A `P(Θ)`-scaled count is an unbounded spawn/target explosion and collides with the per-frame runtime caps, which are perf protection and legitimately hard |
| **`Flag`** | `status.immune.{tag}` `status.immuneReduction.{tag}` | ⛔ — **never a number** | `MaxPriorityFlag`, cap 1. A node here is a switch; budget it as a mechanism |
| **`LadderIndex`** | `progression.power` `progression.realm` | ⛔ **forbidden** | This **is** `Θ`. A node writing it is a private second ladder — the exact defect `ssot-power-scale.md` §10 exists to end |
| **`AptitudePoints`** | the twelve aptitudes | ⛔ **structurally impossible, and that is a feature** | An aptitude is a SOURCE, not a registered channel (`decisions.md:103`), so it is not in `DerivedChannels()` and `stat.derived` refuses it at load — the same construction D11 relies on |
| **`LoamUnits`** | loam | ⛔ | Not a derived channel. `resource.economy`'s vocabulary is `sun\|money\|points\|maxSun\|maxMoney`; loam/soul/essence/shard are not atom-authorable — a hard load-time refusal |

**Counted verdict, stated precisely because the count matters:** of the thirteen classes, **three
accept a ladder-scaled `+X`** (`GameUnits`, `GameUnitsPerSecond`, `ReciprocalPoints`); a **fourth
accepts a flat per-mille `+X` but never a ladder-scaled one** (`PerMilleRatio`); **three accept a
`Θ`-linear point grant and never a `P(Θ)` one** (`SigmoidPoints`, `SigmoidMultiplierPoints`,
`StatusPotencyPoints`); and **six refuse outright** (`Milliseconds`, `Count`, `Flag`, `LadderIndex`,
`AptitudePoints`, `LoamUnits`). 3 + 1 + 3 + 6 = 13.

#### 4.2 The three that fail SILENTLY — the ones a generated corpus will get wrong

Nothing errors in any of these. The catalog composes, the sheet renders, and the node does nothing.

1. **`SigmoidMultiplierPoints`.** The sheet's number rises; the multiplier does not, because it is
   bounded to (1.0×, 2.0×). A deep-tier node here is measurably worthless *and* its soul track is dead.
2. **Capped `StatusPotencyPoints`.** A node past `_categoryResistCap` composes, renders, and does
   nothing. The `capped` render state exists to make this visible — but only if the node's budget was
   priced against the cap in the first place.
3. **`LowerIsBetter` channels — a "+X" here is a self-nerf.** `ModifierOp.DirectionOf` (`:84-99`)
   returns `LowerIsBetter` for **five** primary channels: `attackInterval`, `produceInterval`,
   `attackCountdown`, `produceCountdown` and `takeDmgMultiplier`. A generator reading the channel name
   and writing `+X` authors a penalty that the cost function correctly prices as negative (the sign
   flip at `ModifierOp.cs:57-64`) while the node's own copy describes it as a buff. `takeDmgMultiplier`
   is the trap with a name that invites it — and its own comment says in terms that it *"is NOT the
   authoring surface for 'enemies take more damage'"*.

   ⚠️ [04](../../research/passive-tree/04-number-and-atom-binder.md) §5.3 named only
   `takeDmgMultiplier`. **Counted this session: the set is five.**

**Where this is encoded:** `tree-plan`'s `propertyVocabulary` carries `unitClass` and `direction` as
node properties, because D14's exclusions already key on properties and both are deterministic
projections of shipped code. The binder then *checks* rather than *judges*: a node whose composed
channel's `UnitClass` disagrees with its `nodeClass` and op is refused, with the class named.

### 5. The soul track (D3) — the deepen ladder

#### 5.1 One soul level multiplies nothing. It adds to `Θ`.

```text
Θ_node    = Θ_actor + (soulTrack.thetaPerSoulLevelMilli · soulLevel(node)) / 1000
magnitude = kMicro · P(Θ_node) / 1_000_000
```

**The tunable is `soulTrack.thetaPerSoulLevelMilli`, per-mille, in
`data/tuning/passive-tree.v1.json` (R2).** ~~`soulThetaWeight` / `Ws`, "an integer `Θ` per soul
level"~~ is superseded — it was one number under two names in two units across four specs, and
per-mille is the reading `tree-resolve`, the module that actually reads it, ships. The old `Ws = 1` is
`thetaPerSoulLevelMilli = 1000`.

⚠ **That is a second division, and it is legal for a stated reason.** CLAUDE.md rule 4 governs the
**magnitude** path. `Θ_node` is a ladder *index*, not a magnitude, and the per-mille divide happens
once, before `P()` is ever called; the magnitude path below it still divides exactly once. The comment
must say so, or someone will "fix" it by folding the two together and multiplying a `P(Θ)` by 1000.

**The node's coefficient never moves.** Soul levels move the index the ladder is read at.

#### 5.2 Why the naive alternative is wrong, with the arithmetic

The obvious design — *"each soul level adds x% to the node's bonus"* — scales `kMicro`. Against
`ssot-power-scale.md` §10.5's property, and with D3's arithmetic cost ladder whose cumulative cost of
`L` levels is `Σ(first + (k−1)·step) ≈ (step/2)·L²`:

| Design | Power after `L` levels | Power per unit effort |
|---|---|---|
| **Coefficient scaling** (naive) | `k·L·P(Θ)` — linear in `L` | `∝ L / L² = 1/L` — **decays.** Hour 500 buys a fifth of what hour 5 bought |
| **Index offset** (this design) | `k·[P(Θ₀+L) − P(Θ₀)] ≈ k·(B/2)·L²` | `∝ L²/L² = k·B/step` — **constant** |

```text
cumulative soul cost  ≈ (step/2)·L²      quadratic
power gained          ≈ k·(B/2)·L²       quadratic
                      ⇒ power ∝ souls spent   LINEAR in effort
```

The index offset is not one option among several. **It is the only shape that preserves the property
the ideal §4 already claims for this track** — and the ideal's own §4 arithmetic is exactly this proof
written for the actor rather than for the node.

**`Θ_node` is derived at the read site and never persisted as a second actor `Θ`.** Merging soul
levels into `Θ_actor` would make one node's souls raise every other node's magnitude, which is the
opposite of *"spend all in one is risk and reward"*.

#### 5.3 Overflow — where a `long` gives out, and it throws

Two absolute bounds sit on this path. **Both throw; neither clamps**, so PS-8 is satisfied: nothing is
refused as a design ceiling, a type boundary is reached and reported.

With the shipped dial, `P(Θ) = 0.2Θ² + 26.2Θ + 80` (`cMilli 80000`, `AMilli 26200`, `bMilli 400`).

**Wall 1 — the compiled parameter is `int`.** `AtomCompiler.cs:464` narrows with `checked((int)…)`,
so `OverflowException` is thrown the moment `kMicro · P(Θ_node) / 1e6 > 2,147,483,647`.

| coefficient | `P(Θ)` at the wall | `Θ_node` at the wall |
|---|---:|---:|
| `kMicro = 1_000_000` (a whole-`P(Θ)` node) | 2.147×10⁹ | **103,557** — exactly CLAUDE.md's published `int` whole-units row |
| `kMicro = 3_038` (§3.4's tier-5 node) | 7.07×10¹¹ | **≈ 1,880,000** |

**Wall 2 — `long`.** With the narrowing widened to `long` (§7 item 2), the first refusal is the
`checked` multiply itself: `kMicro · P(Θ_node)` must fit `long`. At `kMicro = 3038` that gives
`P(Θ) ≤ 3.0×10¹⁵`, i.e. **`Θ_node ≈ 123,000,000`**. For a coefficient small enough that the multiply
survives, `PowerLadder.Guard` throws `PowerIndexOverflow` above `MaxIndex` (`PowerLadder.cs:65`,
computed from the loaded curve by binary search, never a constant) — **`Θ ≈ 214,748,300`** for
`bMilli = 400`, again exactly CLAUDE.md's published `long` row.

**So, stated as the answer to "at what soul level does a `long` overflow":** with
`thetaPerSoulLevelMilli = 1000` — one `Θ` per level — `soulLevel = Θ_wall − Θ_actor`. For §3.4's
tier-5 node that is **≈ 123 million soul levels** on the widened path (≈ 1.88 million on the shipped
`int` path). At an arithmetic cost ladder of step `s`, reaching 123 million levels costs
`≈ (s/2)·(1.23×10⁸)² ≈ s · 7.6×10¹⁵` souls. **That is not a ceiling
anyone reaches. It is a type boundary, and it throws.**

⚠️ Already on the books and reached sooner: `ssot-power-scale.md` §11.1 lists `ShieldMath.MaxInput`
and `ResourceDeltaMath.AmountCap` (both `1_000_000_000`) as conflicts that must change — **the first
clamps silently.** A tree node feeding either path inherits that wall long before it inherits `int`'s,
and this module must say so at the node rather than discover it in play.

#### 5.4 Does the soul track need a new `ssot-power-scale.md` §10 row? **Yes — exactly one.**

| Half of the design | New §10 row? | Precedent |
|---|---|---|
| **The magnitude function** — `magnitude = kMicro · P(Θ_node)` | **No.** | **Row 16**, `PatronPolicy`'s `pThetaTermMilli` (aura-skill T22, owner-signed 2026-08-30): *"it calls the shared `PowerLadder`, not a private `f(level)`, so §10's anti-duplication clause is satisfied."* Reading the one ladder is never a new scale |
| **The soul→`Θ` weight `soulTrack.thetaPerSoulLevelMilli`** — how a soul level becomes a ladder index | **Yes — one row in §10.2.** | **Row 18**, `thetaOffset` (species threat rung) is the exact shape: *"lives inside `Θ` itself, additive, before `P(Θ)` runs — not a bounded display value scaled a second time"*, and it got its own row. Row 19 got one for a non-`Θ` progression input on the same principle |

The row must record that `Θ_node` is derived at the read site and never persisted, and that
`soulTrack.thetaPerSoulLevelMilli` belongs in `data/tuning/passive-tree.v1.json` — **not** in `power-scale.v{n}.json`'s `weights` block, because
those compose `Θ_actor` and this one does not. §5 of that SSOT (`Θ_actor`'s composition) needs no
change.

**This module owes exactly one row.** The program owes four; the other three — `req(t)`, `W(T)` and
D36's `unlockCost` — belong to `tree-plan` and `tree-state`. ⚠️ `guard-power.ps1` **cannot catch the
absence of any of them**: its G2/G3 checks key on a parameter named `level`/`lvl`/`index`, and
`soulLevel`/`nodesOwned` land in the same blind spot `DropVolume`'s `thetaActor` already sits in. The
row has to be added deliberately; the guard stays green without it.

### 6. Mechanism node composition — worked from real kinds and triggers only

Ideal §3.5 is the constraint: *"A focus build cannot be rescued with MAGNITUDE. It can only be rescued
with MECHANISM."* Every example below uses only §2's counted vocabulary.

#### M1 — damage scaling with damage taken

```jsonc
// affix "vengeance.t5" — one atom
{
  "kind": "stat.modify",
  "when": { "trigger": "OnDamageTaken" },
  "icd_key": "vengeance",
  "params": {
    "channel": "atk",                        // one of the 23 primary channels
    "op": "flat",
    "amount": { "powerLadder": true, "kMicro": 3038 }
  }
}
```

**Executable today on both real runtimes.** `stat.modify` carries `AllTriggers`, which includes
`OnDamageTaken` (`AtomKindRegistry.cs:46-48`, `:497`), and its matrix is **Lawn Full / Battle Full /
Sim PlanOnly** (`:496`). Lawn: `EffectBag` → FA1 `ModifyStat` → `InjectorEffectActionSink` →
`EntityStatWriter`. Battle: `BattleStatModifierLedger` composes triggered `stat.modify` grants through
the same `PhasedComposeStrategy` the overlay uses.

**Honest caveat, and it is a wiring question not a wall:** stacking and decay are *grant* properties
(`max_stacks`, `icd_ms`), and the automatic un-apply fires only for `OnRemoved` on a `Passive` def. A
stack that decays on a timer is a grant shape to specify, not a kind to add.

#### M2 — a reflect build (why the unit is an affix, not an atom)

```jsonc
// affix "thornmail.t6" — TWO atoms, drawn together
[
  { "kind": "stat.derived",                  // no trigger: permanent modifier (definitions.md §14.2)
    "params": { "channel": "combat.reflect.rate.omni",   "op": "flat", "amount": 180 } },
  { "kind": "stat.derived",
    "params": { "channel": "combat.reflect.damage.omni", "op": "flat", "amount": 220 } }
]
```

Both channels are `PerMilleRatio`, so per §4 the amounts are **flat per-mille points, not
ladder-scaled** — and the node must say so in a comment (PS-8's exemption requires the declaration).
One atom per row cannot correlate the two draws, which is exactly why `definitions.md` §4a makes the
affix the unit.

**⛔ Reflect is LAWN-ONLY. Verified this session, and this spec was wrong.**
~~"Executable on lawn and battle; inert in sim."~~ Battle never reflects.

- `CombatDamageDispatcher.TryReflect` is `static` and private, called from exactly one place —
  `CombatDamageDispatcher.DispatchInstant` (`CombatDamageDispatcher.cs:71`), which re-enters itself
  with the reversed packet (`:122`).
- Every caller of `DispatchInstant` is an overlay/lawn path: `EffectBag` (`:534`, `:603`),
  `StatusEffectBridge` (`:86`, `:129`), and the injector's own runtime. **Nothing in
  `src/FusionRpg.Core/Battle/` calls it.**
- Battle applies HP through `DamageApplyPipeline.Apply` (`BattleRunState`, in
  `ApplyDamage`), and `SimEngine` calls the same helper. Both bypass the dispatcher entirely, so
  neither ever reaches the reflect branch — the *reader* is absent, quite apart from whether the atom
  composes.

**So the constraint is not the atom's runtime matrix.** `stat.derived` really is **Lawn Full / Battle
Full / Sim None** (`AtomKindRegistry.cs:533`), so the two channels compose fine in a battle; there is
simply no code in battle that reads them. That is a **wiring gap** in the precise sense — a missing
consumer, not a wall — and it is a *different* gap from the sim one, which is a missing effect host.

**Consequence, and it is the one that matters for pricing: M7 Retaliation is not measurable at squad
scope.** `squad-harness` resolves squads through the battle/sim path, so a reflect node contributes
exactly zero to any sweep it runs, at any budget. This module must not price a reflect node against a
squad-scope measurement, and a sweep that reports reflect as weak is reporting the missing reader, not
the design. Both consumers belong to `mechanism-wiring` (wave 0).

#### M3 — an anti-turtle punish, stat form

```jsonc
// affix "siegebreaker.t8" — three atoms, one bundle
[
  { "kind": "stat.derived", "params": { "channel": "combat.parry.break.omni", "op": "flat", "amount": 150 } },
  { "kind": "stat.derived", "params": { "channel": "combat.block.break.omni", "op": "flat", "amount": 150 } },
  { "kind": "stat.derived", "params": { "channel": "combat.shield.pen.omni",  "op": "flat",
                                        "amount": { "powerLadder": true, "kMicro": 4860 } } }
]
```

**This is the mechanism ideal §3.5 asked for, and the reason it works is arithmetic, not flavour.**
§3.5's finding was that *"defensive layers compose multiplicatively"* and *"more Might does not fill
an empty defensive layer."* `break` and `pen` do not add to the attacker's own layer — they
**subtract from the defender's**, which is the only thing that reaches a multiplicative stack. The
readers are live and subtractive: `Math.Max(0, ParryRate(def) − ParryBreak(atk))` and the block mirror
(`OverlayCombatCalculator.cs:183-184`); shield pen is `GameUnits` into `ShieldMath`.

**Mixed units in one bundle, which is the binder's job to get right.** `parry.break` and `block.break`
are `PerMilleRatio` → flat points. `shield.pen` is `GameUnits` → ladder-scaled, `kMicro` from §3.3 at
`broad-and-flat` tier 8, whose emitted `budgetShareMilli` is 72 (`1000·1000·72·135 / 2,000,000 =
4,860`). **A binder that scaled all three would saturate two of them in a few tiers and never say
so.**

**Derived ops are `Flat | Increased | Replace | Flag` — there is no `More` on the derived side**
(`AtomKindRegistry.cs:536-537`). A `More`-op derived atom is refused at load, and the binder refuses it
earlier, with the rule named.

#### M3a — which defensive layers can actually be driven to zero, counted

`tree-plan` reserves deep-tier budget for *"layer denial / bypass"*, and the ideal §13.3 lists it as a
gap that has no mechanism: *"every shipped 'break their X' is a saturating contest that provably never
reaches zero — `PierceFactor` bounded (0,1], shield pen capped, parry/block shred clamped."*

**Verified this session, and the blanket claim is false for two of the layers.** The distinction is
`break` versus `shred`, which are different channels with different readers, and the ideal's row names
only `shred`:

| Layer | Channel pair | Reader | Reaches zero? |
|---|---|---|---|
| **Parry rate** | `combat.parry.rate` ↔ `combat.parry.break` | `Math.Max(0.0, ParryRate(def) − ParryBreak(atk))` — `OverlayCombatCalculator.cs:183` | ✅ **YES.** Plain subtraction, floored at zero. Enough break sets the rate to exactly 0 |
| **Block rate** | `combat.block.rate` ↔ `combat.block.break` | the mirror at `OverlayCombatCalculator.cs:184` | ✅ **YES**, same shape |
| Parry / block strength | `…strength` ↔ `…shred` | `ClampedContest.Apply` (`OverlayCombatCalculator.cs:257,262`) | ⛔ no — clamped and saturating. **This is the pair the ideal's row describes** |
| Shield | `combat.shield.pen.*` | `ClampedContest.Apply` under `ShieldPolicy.PenCapKPm` (`ShieldMath.cs:90-91`) | ⛔ no — an explicit cap |
| Penetration family | `combat.penetration.*` | `PierceFactor(d,s) = 1/(1 + max(0,d)/s)` (`OverlayCombatCalculator.cs:383`) | ⛔ no — asymptotic, bounded (0,1] |
| Status resist | `status.resist.{dot,cc,contagion}` | `_categoryResistCap` (`DerivedStatRegistry.cs:106-110`, `:330`) | ⛔ no — capped (§4.2 item 2) |

**So the correction is narrow and it favours the design: two of the six shipped layers are switches,
not dials, and M3 is built on exactly those two.** `parry.break` and `block.break` reach zero; they
are also the only `PerMilleRatio` pair in the table, so per §4 they are granted as **flat per-mille
points planned against the reader**, never ladder-scaled — a scaled break saturates the layer in a few
tiers and then the node stops meaning anything.

⚠ **`passive-tree-ideal.md` §13.3's *"Layer denial / bypass"* row is stale and this spec does not edit
it.** Its claim is right for `shred`, `shield.pen`, `PierceFactor` and the resist caps, and wrong for
`parry.break` / `block.break`. Correcting the ideal is the owner's call; the arithmetic is above so
the correction costs a read, not a re-derivation. **`tree-plan`'s deep-tier denial budget should be
spent on the two rows that work**, and this module refuses to price it onto the four that do not.

### 7. Conversion nodes (D16) — the binder REFUSES

#### 7.1 Why this is a real capability gap, not wiring

`OverlayCombatCalculator` reads element-keyed derived channels **per component, looping the payload's
own component list** (`:128-172`). So an element-keyed affix contributes only through components
present in the payload. A node that converted 40% of a hit to ice by changing a magnitude and not the
payload would leave the player's `combat.power.ice.*`, `combat.crit.rate.ice.*` and
`combat.accuracy.ice.*` reading a payload with no ice component — **every one contributing exactly
zero, forever, with no error.** That is D16's *"a conversion that changed only the number would
silently create dead stats"*, confirmed at the loop that causes it.

Applying CLAUDE.md's three-question ladder honestly:

1. Does the RPG layer already have a channel/atom/runtime for conversion? **No.** The mechanism exists
   — `ElementPayload` is a weighted component list with a private constructor whose only entry is
   `From(components)`, validated to sum to 1 within `WeightSumEpsilon = 1e-6`
   (`ElementPayload.cs:7,16-37`) — but the **writer** does not.
2. Is a path inert? **N/A** — there is no path. **No kind among the 16 writes an element payload**, and
   there is no `Element` attach point among the 7. `resource.delta` has an `element` param
   (`AtomKindRegistry.cs:552`), but that *names* an element on a delta; it does not rewrite a payload.
3. Is this genuinely new? **Yes.**

So the correct word is **new capability**, and the correct process is a reviewed change to
`decisions.md`'s "Atom attach points" row (`decisions.md:112`, which says in terms that *"growing this
list is a reviewed change to this row"*). The cheapest shape is a **17th kind on the existing `Board`
or `Stat` attach point** rather than an eighth attach point — but that is a decision for whoever specs
it, not for this module.

#### 7.2 What the refusal looks like

**The binder allocates no budget to a conversion node until a 17th kind exists.** Concretely:

```text
tree-binder: REFUSED  skill.element-fire-off-t7-b3
  reason        ConversionKindUnavailable
  requested     an atom writing ElementPayload components (source=fire, target=physical)
  capability    no kind among the 16 writes an element payload; no Element attach point exists
  authority     decisions.md:112 "Atom attach points" — growing the list is a reviewed change
  consequence   this node's budgetShareMilli (64 permille of ONE BRANCH) was NOT priced, NOT emitted
  remedy        land the 17th kind, or re-plan this slot as a non-conversion node
```

Three properties this refusal must have, each for a stated reason:

1. **It refuses, it does not clamp and it does not substitute.** Emitting a magnitude-only "fire
   power" node in a conversion slot would buy the tree nothing and look correct in every report.
2. **It names the budget it did not spend.** A refused slot leaves a hole, and an unspent share is a
   slice of every tree's budget that buys nothing. `tree-plan` must re-apportion the refused slot's
   share over the remaining nodes — the same rebalance rule the quota draw needs (`tree-language` §4.2
   step 5) — or the tree quietly ships under its own budget.
3. **The run's verdict is `FAIL`, not `NOT_MEASURED`.** `RunReport.verdict`'s rule
   (`setgen/verdict.py:83-96`) applies: a held partition alone denies a pass.

**Consequence for `tree-plan`, stated here because this module is the one that discovers it:** stage 1
owes a flag that suppresses conversion nodes at plan time, so the refusal is a zero-count assertion in
a healthy run rather than a per-run event.

---

## Commands

```powershell
dotnet run --project tools/TreeBinder -- --seed data/seed/passive-tree --out data/generated/passive-tree
dotnet run --project tools/TreeBinder -- --check              # stale generated tree -> non-zero exit
dotnet run --project tools/TreeBinder -- --explain <nodeId>   # every derivation step, shown
dotnet test tests/FusionRpg.Core.Tests --filter TreeBinder
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --summary
.\scripts\guard-power.ps1
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
```

`--explain` prints the whole chain for one node — plan input, anchor pin, formula, rounding, stored
`kMicro`, the `UnitClass` check and its verdict. It is how a balance question gets answered without
reading the code, by the `DemonSpeciesGen --explain` precedent.

## Project structure

**This module is C#, not Python, and the reason is a rule rather than a preference.** It must read
`DerivedStatRegistry` for the 267 + 9 channel vocabulary, `AtomKindRegistry` for the 16 kinds and
their param schemas, and `power-scale.v{n}.json`'s pins — all of which are C# SSOTs. The
`species-generator` precedent says it outright: *"Never … reimplement `Magnitude` in Python."*

```text
tools/TreeBinder/Program.cs                                    arguments, --check, --explain, report
src/FusionRpg.Core/PassiveTree/Binding/CoefficientBinder.cs    §3.3's formula, testable
src/FusionRpg.Core/PassiveTree/Binding/ChannelLegality.cs      §4's rule table, keyed by UnitClass
src/FusionRpg.Core/PassiveTree/Binding/AffixComposer.cs        affix -> atom rows; op and kind legality
src/FusionRpg.Core/PassiveTree/Binding/BoundNode.cs            the emitted row shape
src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs                  + PowerLadderKMicro  (Ask first - §3.5)
src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs               + the /1_000_000 arm  (Ask first)
data/tuning/passive-tree.v1.json                               treeShareMilli, soulTrack.thetaPerSoulLevelMilli
data/generated/passive-tree/<treeId>.json                      committed output — THIS is what ships
tests/FusionRpg.Core.Tests/PassiveTree/TreeBinderTests.cs
```

`tree-catalog` owns the on-disk record shape; this module writes it and never redefines it.

## Code style

```csharp
// The catalog stores a SHARE of P(Θ), never a magnitude — that is what makes one static
// catalog correct for every player at every Θ (D24).
//
// budgetShareMilli is TREE-PLAN'S NUMBER and it is read as given (R4). Do not re-derive it
// from a tier weight: the plan distributes tier budget proportional to t and the width vector
// enters one level below, so any reconstruction here that multiplies by w[t] silently disagrees
// with the plan on every non-uniform archetype. That defect shipped once and cost gated-deep's
// capstone a factor of 3.25. There is no `tierWeight` and no `weightTotal` in this file.
//
// All four numerator inputs are per-mille, so the product carries 1000^4; per-million cancels
// 1000^3, and `branches` supplies the last factor — exactly ONE division (CLAUDE.md rule 4).
// `long` throughout, widened by declaration, `checked` so an overflow THROWS rather than
// wrapping. This is a magnitude path, not a display path.
static long CoefficientMicro(long treeShareMilli, long treeBudgetMilli, long budgetShareMilli,
                             long channelAnchorMilli, long branches)
{
    checked
    {
        var num = treeShareMilli * treeBudgetMilli * budgetShareMilli * channelAnchorMilli;
        var denom = branches * 1_000_000L;
        return PowerLadder.RoundHalfAwayFromZero(num, denom);   // the shipped convention, not a new one
    }
}
```

## Testing strategy

| Test | Asserts |
|---|---|
| `coefficient_matches_the_worked_example` | `budgetShareMilli` 45, atk anchor 135, branches 2 → `kMicro == 3038`; the sibling at 46 → `3105` |
| `the_binder_never_recomputes_the_plans_share` | source-shape assertion: no `tierWeight`, no `weightTotal`, no `w[t]` anywhere in `CoefficientBinder`. **R4's guard — the defect it replaces was invisible to every value test** |
| `every_archetype_binds_from_its_own_emitted_shares` | run all three shipped archetypes' full share vectors; `gated-deep`'s capstone lands at the plan's 182‰ of a branch, never at 56‰ |
| `kMicro_is_an_exact_function_of_budget_share` | `kMicro(share)` is monotone and reproducible for every share the plan can emit. **D26's flatness is asserted in `tree-plan`, over the authoritative column — this module does not re-assert it** (§3.4) |
| `per_mille_stores_zero_on_every_archetype` | at per-mille resolution: 12 dead nodes in `gated-deep`, 6 in `broad-and-flat`, 1 in `late-crown`, and tiers 1–2 indistinguishable on the atk anchor. **This test documents why `PowerLadderKMicro` exists and must not be deleted with it** |
| `per_million_error_is_under_a_tenth_of_a_percent` | the same shares at per-million: no stored zero, worst relative error < 0.1% |
| `node_share_is_compared_to_the_ceiling_in_one_unit` | `budgetShareMilli` vs `potency.maxNodeShareMilli`, both ‰ of one branch, no conversion on either side (R5) |
| `every_input_is_long_and_widened` | reflection over the binder's signature; a `float` or `int` magnitude parameter fails |
| `one_division_only` | source-shape assertion over `CoefficientBinder` — a second `/` on the magnitude path fails |
| `overflow_throws_never_clamps` | a deliberately enormous `Θ_node`; assert `OverflowException`/`PowerIndexOverflow`, and assert **no** `Math.Min` on the path |
| `no_cap_on_any_magnitude` | greps the binder for `Math.Min`/`Math.Clamp` on a magnitude path |
| `no_private_level_function_exists` | greps for `Math.Pow` or a curve shape outside `PowerLadder` |
| `channel_anchor_is_derived_from_the_tuning_pins` | change `power-scale`'s `atk.pinValue`; the anchor moves without a source edit |
| `gameunits_accepts_a_ladder_node` | the three ✅ classes bind |
| `sigmoid_classes_get_theta_linear_never_ptheta` | a `P(Θ)` amount on `SigmoidPoints` is refused with the class named |
| `permille_ratio_is_flat_never_scaled` | a `powerLadder` amount on `combat.reflect.rate.omni` is refused |
| `lower_is_better_channels_refuse_a_plus_x` | all **five** of `DirectionOf`'s `LowerIsBetter` primaries refuse, not just `takeDmgMultiplier` |
| `ladder_index_and_aptitude_points_refuse` | writing `progression.power` or an aptitude id is refused at bind, before load |
| `no_more_op_on_a_derived_channel` | derived ops are `Flat\|Increased\|Replace\|Flag` |
| `conversion_node_is_refused_with_the_rule_named` | the §7.2 refusal text, including the unspent budget line |
| `refused_slot_budget_is_reported_not_absorbed` | the run report names every unspent share |
| `soul_level_offsets_theta_never_the_coefficient` | `kMicro` is byte-identical at soul level 0 and 50; only `Θ_node` moves |
| `theta_per_soul_level_is_read_as_per_mille` | `thetaPerSoulLevelMilli = 1000` gives one `Θ` per level. Writing `1` must give a *thousandth*, not one — the exact failure R2 was written after |
| `reflect_is_not_priced_as_squad_measurable` | a reflect node's expected contribution in the battle/sim path is asserted **zero**, with the missing reader named, so a sweep's null result is never read as a balance finding (§6 M2) |
| `parry_and_block_break_are_flat_never_scaled` | a `powerLadder` amount on `combat.parry.break.*` is refused; the flat grant is planned against `OverlayCombatCalculator.cs:183-184`'s subtraction (§6 M3a) |
| `power_is_linear_in_souls_spent` | `ΔP / Σcost` is constant across `L`, within rounding — §5.2's property, measured |
| `regenerating_unchanged_seeds_is_byte_identical` | the `--check` gate |
| `explain_output_names_every_input` | the audit trail is complete for any node |

Coverage says what the tests touched; mutation says what they would notice. Run
`.\scripts\coverage.ps1 -Namespace FusionRpg.Core.PassiveTree` and add a `scripts/mutants/*.json` set
for the binder — a surviving mutant on the coefficient formula needs an explanation next to the code.

## Boundaries

**Always:** `long` for every magnitude; widen before multiplying; divide by 1000 (or 1,000,000) last
and exactly once on the magnitude path; round half away from zero, using the shipped helper; let
overflow throw; **read `tree-plan`'s `budgetShareMilli` as given**; derive `channelAnchorMilli` from
the tuning pins at bake time; check every composed channel's `UnitClass` before pricing it; refuse
with the rule named; commit the output; say which paths you wrote.

**Always, for citations:** cite `Battle*` files **by symbol, never by line** (R9). `battle-tempo` is
editing `BattleModels.cs` and `BattleRunState.cs` right now, and seventeen stale line citations across
this spec set trace to exactly that. Name the type and the method — *"`BattleRunState.ApplyDamage`,
which calls `DamageApplyPipeline.Apply`"* — and the citation survives the drift. This spec's own
`AtomCompiler.cs:463-464` and `:456-466` were re-checked this session and are correct as written.

**Ask first:** adding `ValueSpec.PowerLadderKMicro` and its `AtomCompiler` arm (§3.5 — it widens a
shipped contract); widening `AtomCompiler.cs:464`'s result from `int` to `long` (§5.3 — it moves the
first refusal by three orders of magnitude and is a `src/FusionRpg.Core` change); adding the one
`ssot-power-scale.md` §10.2 row for `soulTrack.thetaPerSoulLevelMilli`; adding a second `ValueSpec`
source for `ChannelLadder`.

**Never:** write a private `f(level)` — every magnitude reads the one `PowerLadder`; **re-derive a
node's budget share from a tier weight or a weight total** (R4 — the plan distributes, this module
reads); bake a magnitude into the catalog instead of a coefficient; cap or clamp a magnitude (absolute bounds **throw**, and a
bounded ratio must say in a comment that it is one); use `float` for a magnitude; scale a
`PerMilleRatio`, `SigmoidPoints`, `SigmoidMultiplierPoints` or `StatusPotencyPoints` channel by
`P(Θ)`; write `progression.power`, `progression.realm` or an aptitude id; allocate budget to a
conversion node before a 17th kind is reviewed; widen the atom vocabulary (7/16/13 is a reviewed
`decisions.md` change); invent a third channel classification beside `UnitClass` and the render
states; let a model near this module.

## Success criteria

- [ ] Every node's magnitude is `kMicro · P(Θ_node) / 1e6` through the shipped `PowerLadder` — no
      private curve anywhere in the module, proven by test.
- [ ] Every node's `kMicro` derives from `tree-plan`'s emitted `budgetShareMilli` and from nothing
      else — no `tierWeight`, no `weightTotal`, proven by a source-shape test.
- [ ] All three shipped archetypes bind from their own share vectors, and `gated-deep`'s capstone
      lands on the plan's 182‰ of a branch.
- [ ] `PowerLadderKMicro` exists, this module owns it, and no node in any archetype stores `0`.
- [ ] `scripts/audit-overflow.py` reports no critical finding in this module; `--targets A3` is clean
      for its files.
- [ ] No `Math.Min` or `Math.Clamp` guards a magnitude anywhere in the derivation.
- [ ] Every one of the 13 `UnitClass` values has an explicit verdict in `ChannelLegality`, and all
      three silent-failure classes refuse loudly instead.
- [ ] All five `LowerIsBetter` primary channels refuse a `+X` node.
- [ ] Conversion nodes are refused with the rule named, and every refused slot's unspent budget is
      reported.
- [ ] `soulTrack.thetaPerSoulLevelMilli` has a row in `ssot-power-scale.md` §10.2 before the soul
      track ships, under that name and in per-mille.
- [ ] `--check` gates a stale generated tree; `--explain` shows the full chain for any node.
- [ ] A rerun over unchanged seeds is byte-identical, proven by hash.

## Open questions

1. **`treeShareMilli`.** The single biggest dial the tree layer has, and the ideal §3.3 names it
   undecided: *"its leverage depends on what share of total power trees carry — currently unknown and
   worth deciding deliberately."* §3.4's `1000` is a placeholder chosen so the arithmetic runs, not a
   balance decision. It is a **tunable**, so this spec names the key and the unit and the value lands
   later — but the *decision* is owed before the first catalog is reviewed, because it sets every
   number a player reads.
2. **The 17th kind (§7).** A genuinely new capability and a reviewed change to `decisions.md:112`.
   Whether it lands on `Board` or `Stat` is that spec's call, not this one's. Until then this module
   refuses, and `tree-plan` should suppress the slot rather than let the refusal fire per run.

**Named, small and mechanical — not open, just unscheduled:** a sim consumer for `stat.derived`
(`AtomKindRegistry.cs:533`), without which the re-measurement ideal §3.5 schedules cannot see the
mechanism nodes it is scheduled to measure. It belongs to `mechanism-wiring` (wave 0), and this module
depends on it only for *scoring*, never for *binding*.

## Decisions implemented

| Decision | How this module implements it |
|---|---|
| **D3** | §5 — the soul track adds `thetaPerSoulLevelMilli · soulLevel / 1000` to the ladder index and never touches the coefficient, which is the only shape that keeps power linear in effort |
| **D13** | This *is* stage 3: deterministic, no model calls, running after the plan and the language stage |
| **D15** | `treeBudgetMilli = 1000` for every tree, so equal expected value survives the bake regardless of archetype — the shape never enters the sum |
| **D16** | §7 — the binder refuses to price a conversion node until a 17th kind is reviewed, names the unspent budget, and fails the run rather than substituting |
| **D20 / D26** | Honoured by **deferring to `tree-plan`**, which is where the linear tier column is emitted and asserted. This module multiplies the emitted share by an anchor and does not re-derive the ladder — the old `tierWeight(t) = t` reconstruction was a second copy of that arithmetic and disagreed with the first (§3.3, R4) |
| **D22** | Every node composes from the shipped 16 kinds and 13 triggers. No passive-specific effect vocabulary exists here |
| **D24** | The catalog stores a **coefficient, not a magnitude** — the single choice that makes one static, byte-identical, shared catalog correct for every player at every `Θ` |
| **D29** | 10 tiers × 2 branches is why the denominator carries `branches = 2`. §3.5's re-derivation against the shipped archetypes' real shares shows the per-mille defect is not a rounding complaint at all: it stores `0` |
| **ideal §8** | One ladder (`PowerLadder`, never a private `f(level)`); no caps on magnitudes; `long` everywhere; balance numbers in `data/tuning/passive-tree.v1.json` |
| **§10.2 row** | Exactly one new row, for `soulTrack.thetaPerSoulLevelMilli`, by row 18's precedent; none for the magnitude function, by row 16's |

**Decisions this module does not touch**, and where they live: D1, D2, D4–D8, D11, D12, D18, D21,
D25, D28, D33–D36 (`tree-state`, `tree-resolve`, `squad-harness`); D9, D10, D14, D27, D30, D32
(`tree-plan`, `tree-language`); D17, D23 (`species-tree`); D19 and D31 are superseded by D35.

---

## Design-gate checklist

```
[x] I identified the subsystem(s): atom layer, derived stats, power ladder, effect pipeline,
    passive trees.
[x] I read every doc in the DESIGN-GATE §1 row(s) this session: DESIGN-GATE.md,
    passive-tree-map.md, passive-tree-ideal.md (full), research 02/03/04,
    power/ssot-power-scale.md (§10, §10.2, §11.1), design/spec-magnitude-and-units.md §3,
    CLAUDE.md's overflow table, AGENTS.md's hard boundaries.
[x] I checked decisions.md for a lock covering this — "Atom attach points" (:112) and
    "Class system" (:103) both apply and are honoured.
[x] Counts verified BY COUNTING in src/ this session: 7 attach points, 16 kinds, 13 triggers
    (11 authorable), 6 elements, 21 statuses, 12 aptitudes / 3 postures, 23 primary channels,
    267 derived + 9 open prefix families, 13 UnitClass, 4 StatClass, 12 predicate leaves,
    5 LowerIsBetter primary channels.
[x] Every factual claim cites file:line.
[x] I verified claims against CODE, not comments — StatClass.cs:26 says "ten-class" over a
    13-member enum, and AtomKindRegistry.cs:6 says "5 attach points, 12 kinds" fifteen lines
    above consts of 7 and 16.
[x] 2026-09-05 audit fold, re-verified in code THIS session: power-scale.v2.json's atk/defense/hp
    pins (92 / 22 / 680, so the anchors are 135 and 32); TryReflect's single caller
    (CombatDamageDispatcher.DispatchInstant) and the absence of any Battle caller;
    OverlayCombatCalculator.cs:183-184's unclamped parry/block break against :257,262's clamped
    shred and ShieldMath's PenCapKPm; AtomCompiler.cs:456-466's shape; ValueSpec.cs:92 and
    :126-135.
[x] I read the surrounding section of every rule I quoted (PS-1/PS-3/PS-8, §10.5, §11.1,
    definitions §14.2).
[~] I tested (not assumed) any constraint I report. PARTIAL. No suite was run — this spec
    proposes code changes but lands none, and makes no "moves goldens" claim. The two
    constraints it now ASSERTS were computed rather than assumed: the per-mille zero table
    (§3.5) is arithmetic over tree-plan §3's own emitted share vectors, and the layer-denial
    table (§6 M3a) is a read of every reader it names.
[x] Nothing contradicts a §2 invariant. The one that comes close is §7's conversion kind, and
    it is named explicitly as a NEW CAPABILITY requiring a reviewed decisions.md change —
    never smuggled in as a wiring gap.
[~] Corrections propagated. PARTIAL. §6 M3a states the correction to
    passive-tree-ideal.md §13.3's "Layer denial / bypass" row (parry/block BREAK does reach
    zero; the row is right only about SHRED) and §6 M2 states the correction to this spec's
    own "lawn and battle" claim about reflect. Amending the ideal is the owner's call, not a
    side effect of this spec. Four further sites are stale and this spec edits none of them:
    DESIGN-GATE.md:34 ("nine-class UnitClass"), StatClass.cs:26 ("ten-class"),
    AtomKindRegistry.cs:6 ("5 attach points, 12 kinds"), ValueSpec.cs:24-26 (states the
    corrected-away resolver-points model). DESIGN-GATE's own row wins over any spec, so
    amending it is an owner call, not a side effect of this spec.
```

## Related

- [passive-tree-map.md](../passive-tree-map.md) — the module index
- [passive-tree-ideal.md](../passive-tree-ideal.md) — D1–D36, §3.5 mechanism-not-magnitude, §8, §14
- [spec-tree-language.md](spec-tree-language.md) — stage 2, which chooses what this module prices
- [04-number-and-atom-binder.md](../../research/passive-tree/04-number-and-atom-binder.md) — this module's predecessor
- [power/ssot-power-scale.md](../power/ssot-power-scale.md) — §10 rows 16, 18, 19, 20; §11.1's two clamps
- [design/spec-magnitude-and-units.md](../../design/spec-magnitude-and-units.md) §3 — the 13-class ledger
- [effect-atom/definitions.md](../effect-atom/definitions.md) — §4a the affix roll unit, §14.2 lifecycle triggers
