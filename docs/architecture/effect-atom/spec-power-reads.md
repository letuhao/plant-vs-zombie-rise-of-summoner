# Spec: power-reads (E10)

Module **E10** in the [atom effect map](../effect-atom-map.md). Depends on **E9**, **E18** (the matchup read consumes its two matrix tables).

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

**One price, three reads.** E9 stores the vector; this module serves it to three consumers who need three different shapes — and stops each from using the wrong one.

```text
vector             — the SSOT, stored per atom / item / actor        (E9)
display scalar     — a geometric mean, for humans and sorting
matchup-conditioned — vector × element ring, for AI and difficulty
marginal           — vector(with) − vector(without), for AI and balance
```

## Design (locked on approval)

### Why three, and not one

Game-AI research is consistent that an evaluation value is almost never one number, and the shapes it takes map cleanly onto ours:

| Shape | What varies | Prior art | Our read |
|---|---|---|---|
| Weighted feature vector | nothing | chess evaluation = Σ feature × weight | the stored vector |
| **Matchup matrix** | *against whom* | RTS counter and damage-per-frame matrices, learned from replays | matchup-conditioned |
| Search / difference | context | MCTS where static evaluation is inadequate | marginal |

**A human barely needs power** — a player compares two items by reading them. The reader that cannot function without a number is the **AI**: it must pick a target, decide commit-or-retreat, and price a trade, in code, now.

### 1. Display scalar — geometric mean

**`geomean(vᵢ + 1) − 1` over all five categories**, and `0` when every category is zero.

The plain geometric mean is wrong here: any zero factor makes the product **exactly** zero, and most atoms touch one or two of five categories — so nearly every atom would score 0 and "balanced beats glass cannon" would compare 0 to 0.

**Stated limitation, not hidden:** two things touching *different numbers* of categories are scored on different bases and are **not** meaningfully comparable. The scalar sorts like-for-like; it is not a universal ranking. Anything needing a real comparison uses the vector or the marginal read.

**Cautionary reference, deliberately copied and deliberately limited:** Pokémon GO's `CP = (Atk × √Def × √Sta × CPM²)/10` is close to this shape and is documented as misleading — attack is not under a root, so it dominates, and CP does not compare across species. We take the shape; we refuse the claim that the number is precise. It is for sorting and rough comparison, nothing else.

### 2. Matchup-conditioned — the read that cannot be retrofitted

"How strong is this actor" and "how strong is this actor **against that one**" are different questions, and our element ring already makes the answers differ by ±250‰ per slot — compounding to **+562.5‰** for double-strong, because slots multiply.

Computed on demand as vector × the element matrices (E18 data). It is impossible to retrofit onto a stored scalar, which is why the vector is SSOT.

**Two matrices, not one.** The combat ring and the shield matrix are asymmetric — light and dark are mutually +1 in shields. A matchup read that uses the wrong table is wrong by 25%.

### 3. Marginal — how multiplicative pairs get priced correctly

*(`ActorPowerCache` moved to **E9**, which needs it for spawn recursion. E10 consumes it.)*

```text
marginal(actor, atom) = vector(actor WITH atom) − vector(actor WITHOUT it)
```

The difference captures whatever multiplies, **by construction**. This is the resolution to the open problem E9 leaves: stored atom power stays context-free for budgets and display, where approximately right is fine; AI and the balance sweep read marginal, where exactly right matters.

It costs little because actor power is memoized and the AI layer already evaluates actor power to make decisions.

The gap between the two reads is itself the deliverable: the sweep reports it, and that report **is** the list of shapes the cost function misprices.

### The AI contract

Written now, before an AI layer exists, so it inherits a stated seam rather than a discovered one:

- AI reads the **vector** and the **matchup-conditioned** read. It **never** reads the display scalar.
- AI owns normalisation, response curves, and weights. Utility AI clamps every consideration to `[0,1]` and multiplies, which is what keeps a decision bounded as considerations accumulate — so power arrives as a *normalized, curved consideration*, never a raw magnitude. That conversion is theirs, not ours.
- AI reads **atom-declared tags**, never atom internals.

### Report stamping

If difficulty or rewards read actor power, the report carries **the power that was used** — never a power recomputed later (E8). A recomputed number under a different `contentHash` is a different number.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PowerReads"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/Power/PowerScalar.cs      (new — geometric mean)
src/FusionRpg.Core/Effects/Atoms/Power/MatchupRead.cs      (new — vector × element matrices)
src/FusionRpg.Core/Effects/Atoms/Power/MarginalRead.cs     (new — with/without difference)
tests/FusionRpg.Core.Tests/Atoms/PowerReadsTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Balanced actor vs glass cannon, equal vector sum | balanced reads **higher** on the scalar |
| An atom touching 2 of 5 categories | scalar is `geomean(vᵢ + 1) − 1` over **all five**; the three zeros contribute a factor of 1, not annihilation |
| Domination | if `v ≥ w` componentwise then `scalar(v) ≥ scalar(w)` — property test over random vectors, because this is the rule the previous formula broke |
| Every category zero | scalar is exactly 0 |
| Two things touching different category counts | **documented as incomparable** — asserted by a test that the doc-comment says so, not by a numeric claim |
| Fire attacker vs ice defender | matchup read exceeds the neutral read |
| Double-strong slots | reflects the **multiplied** 1.5625, not an added 1.5 |
| Matchup read using the shield matrix on a combat question | caught by a test — the two tables are not interchangeable |
| Marginal of a crit-rate atom on an actor with high crit damage | **strictly exceeds** its stored context-free power — this row is only distinguishable because actor power composes channels before pricing; under the old per-atom sum it was identically equal |
| Marginal of the same atom on an actor with no crit damage | at or below stored power |
| Marginal on an empty actor | approximately equals stored power |
| Scalar reachable from an AI code path | **deferred, honestly** — no AI layer exists, so an architecture rule over an empty namespace passes forever and guards nothing. The contract ships as a documented rule that the AI spec inherits, and the test lands **with** that layer |
| Report | carries stamped power, never recomputed — **this is E10's row**, not E8's; no power exists at E8's position |

## Boundaries

**Always:** treat the vector as SSOT; compute matchup and marginal on demand; stamp the power that was used.

**Ask first:** changing the scalar's combination function; exposing a new read.

**Never:** store the scalar as truth; let AI read the display scalar; use the combat matrix for a shield question or the reverse; recompute a report's power after the fact; claim the scalar means anything precise.
