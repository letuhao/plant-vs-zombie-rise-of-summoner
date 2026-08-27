# Spec: usability-conditions (A4)

**Status: REVISED 2026-08-27** against the sealed [action-ideal.md](../action-ideal.md).

**What changed:** a **sixth gate** for the guard stance (§1a) · an **inventory leaf** owed to the
consumables lane (§3a) · affordability now spans **six** resources, and `A3` answers the
item-is-not-a-resource question in this module's favour (§3a).

Module **A4** in the [action map](../action-map.md). Depends on **A1**, and contributes to the effect-atom program's **`E3`** rather than forking it.

## Objective

Answer one question, fast and with a reason: **may this actor use this action against this target, right now?**

`A5` needs it because an action that cannot be used must not be committed. `A7` needs it far more — the stub AI evaluates it across every action it holds against every candidate target, every turn.

## Design (locked on approval)

### 1. Usability is a sequence of gates, not one predicate

Treating it as a single condition is the mistake to avoid: affordability, cooldown, and range are not predicate-shaped, they are lookups with different costs and different answers.

Evaluated in this order, **cheapest first**, stopping at the first refusal:

| # | Gate | Cost | Owner |
|---|---|---|---|
| 1 | **Enabled / bound** — does this actor hold this action at all | dictionary hit | A1 |
| 2 | **Cooldown** — `CooldownLedger.IsReady` | one lookup | kernel, shipped |
| 3 | **Affordability** — every cost row satisfiable | O(costs), ≤3 | A3 — **a seam here, see below** |
| 4 | **Range** — Chebyshev, or pass with no board | O(1) | A2 |
| 5 | **Condition** — the compiled predicate | ≤16 nodes | E3 |

### 1a ⛔ Gate 0 — the stance refusal, and it comes FIRST

Guard is a **stance** (`A8`): while it holds, **every other action is refused, movement included.**

| # | Gate | Cost |
|---|---|---|
| **0** | **Stance** — is this actor mid-stance, and is this action the release? | per-actor |

It sits **before** `bound` for one reason, and it is not cost — no measurement was taken and a status probe
is not obviously cheaper than a dictionary hit. It is **per-actor**, so `A7` hoists it out of the target
loop **and** out of the action loop. An actor holding a stance refuses every candidate at
zero per-target cost.

**Its refusal is typed like the rest** — `StanceHeld` — because the FE must say *"you are guarding"*
rather than greying a button with no reason, and `A7` must know that re-checking next tick **can** change
the answer.

*Guard-while-moving is a separate action*, so it passes gate 0 by being the release rather than by
exemption. **There is no exemption list**, which is what stops this gate acquiring one.

The order is deliberate and load-bearing for `A7`: gate 1 is per-actor and hoistable out of the target loop, gates 2 and 3 are per-action, and only gates 4 and 5 are per-target. An implementation that evaluates all five per `(action, target)` pair does the same work an order of magnitude more often.

**Gate 3 is a seam, and a no-op until `A3` lands** (audit R2-1). The build order is `A1 → A2 → A4 → A5 → A3`, so this module ships **before** the cost table exists. `IAffordabilityCheck` returns affordable while there is no cost system; `A3` supplies the real implementation behind the same interface.

Reordering `A3` earlier would be worse: it would put the cost system inside `A5`'s byte-identity gate, for an action that has no costs. The seam is the cheaper answer, and it is one interface.

### 2. Refusal is typed, never a boolean

```
UsabilityResult = Usable | NotBound | OnCooldown | CannotAfford(resourceId)
                | OutOfRange | TooClose | ConditionFailed | NoValidTarget
                | StanceHeld | MissingStock(stockId)
```

A boolean is useless to both consumers. The FE needs *"not enough spirit"* on a greyed button; `A7` needs to know whether re-checking next tick could change the answer — `OnCooldown` and `CannotAfford` become true with time, `NotBound` never does. A bare `false` forces both to re-derive what the gate already knew.

### 3a ⛔ The inventory leaf — owed to another lane, and now answered

[item/ssot-consumables.md](../item/ssot-consumables.md) §5(c): *"`A4`'s usability condition needs a leaf
that reads **do I hold ≥ 1 of this stock row**. The leaf list is closed and none of them reads
inventory."*

**Answered here, and the answer is that it belongs here rather than in `A3`.**

`A3` §8 declines to widen `resource_id` to admit an item, for three reasons — the closed six-id set, the
different rollback semantics, and the decisive one:

> **Costs scale with `Θ` and rungs; an item does not.** *One potion* is one potion at every level, so it
> fails the pure-number property the cost economy rests on.

So consuming the item is a **precondition**, and this module reads it:

| Leaf | Param | Reads |
|---|---|---|
| `holdsStock` | `(stockId, minQty)` | inventory |

**It is a leaf, not a gate**, because it is per-actor-per-action and predicate-shaped — unlike affordability,
which is a lookup over a cost list. Adding it is a **reviewed change to `E3`'s closed list**, and it needs a
reader: `FactReader` gains a narrow, readonly stock probe, following `HpMilli`'s existing shape.

#### Mode matrix — where the stock is read from

The leaf's guard rail answers **when** the count is read, not **where from**, and the two runtimes differ:

| Mode | Stock source | Wave 1 |
|---|---|---|
| **battle** | server-authoritative, resolved at action-set assembly (`A15`) | ✅ supported |
| **PvZ lawn** | the overlay is a **stateless observer** and never reads current game state | ⛔ **unsupported** — a consumable action is **not bindable** in lawn mode |

**An unsupported mode named is fine; an unstated one is the `resource.delta` defect again.**

⚠️ **The leaf must not perform I/O.** `E3`'s boundary is explicit — *"never a leaf that performs I/O,
reads a clock, or draws RNG."* The stock count is read into the fact struct at evaluation setup, exactly as
resource values are.

### 3. Conditions reuse `E3`, and this module contributes leaves to it

The compiled predicate is `E3`'s: `ICompiledPredicate.Evaluate(ref FactReader)`, **`MaxDepth = 4`, `MaxNodes = 16`**, a closed `LeafId` list, and allocation-free evaluation through a struct reader. Those caps are inherited, not renegotiated.

Shipped leaves already cover most of what actions want — `SideIs`, `TypeIdIs`, `TypeIdIn`, `HasStatus`, `HpBelowMilli`, `HpAboveMilli`, `ElementIs`, `RowIs`, `ColIs`, `IsMindControlled`, `ActorIsKiller`. In particular **"silenced" is `HasStatus`** and needs nothing new.

**Two leaves are genuinely missing**, both from resources not existing when `E3` was written:

| Leaf | Shape | Why |
|---|---|---|
| `ResourceBelowMilli` | `(Subject, resourceId, milli)` | *"Only usable below half spirit."* Conditional actions keyed on a pool |
| `ResourceAboveMilli` | `(Subject, resourceId, milli)` | *"Only while qi is full."* |

They follow `HpBelowMilli`'s existing per-mille shape exactly, so they are the same idea generalised — which is the argument for adding them rather than a bespoke check.

**⚠️ Cross-program ask, and it is not free.** These leaves need `EntityFacts` to carry resource values, which is a change to a shipped record struct in the atom program. Four ints (per-mille of max, matching `HpMilli`) is cheap, but it is **their type and their reviewed change** — this module does not make it unilaterally. If refused, resource-conditional actions are out of wave 1 and that is stated, not worked around with a second condition language.

`CellFree` is deliberately **not** requested yet: it needs a board, `A10` is deferred, and a leaf with nothing to read is a leaf that cannot be tested.

### 4. Affordability is a gate, not a leaf

Tempting to add `CanAfford` as a predicate leaf. Refused: the cost rows already say what an action needs, a leaf would duplicate them, and the two would drift the first time a cost gained a modifier. Gate 3 reads the cost table; the predicate never does.

### 5. No board means gates 4 and 5 still answer

Range passes with no board (`A2` §4). Position leaves (`RowIs`, `ColIs`) read facts that are absent — they must evaluate **false**, not throw, and an action whose condition depends on position is simply unusable until `A10` lands. That is honest and quiet; throwing would take the battle down.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionUsability"
```

## Structure

```
src/FusionRpg.Core/Actions/UsabilityResult.cs      (typed refusal)
src/FusionRpg.Core/Actions/UsabilityEvaluator.cs   (the five gates, ordered, short-circuiting)
tests/FusionRpg.Core.Tests/Actions/
```

No new predicate machinery. The two leaves land in `E3`'s files, as `E3` rows.

## Testing strategy

- **Each gate refuses with its own reason** — five tests, one per gate, each asserting the *specific* result rather than "not usable". A test that only asserts unusable passes when the wrong gate fires.
- **Short-circuit is proven, not assumed** — a `FactReader.Reads` count of zero when an earlier gate refuses. `E3` already instruments reads, so this is measurable rather than argued.
- **Gate order is asserted directly.** An action that is simultaneously on cooldown *and* unaffordable must report `OnCooldown`. Without this the order is a comment, and the FE's message changes silently when someone reorders.
- **Evaluation allocates zero bytes** — `A7` runs this across every action × target pair each turn, and `FactReader` is a struct precisely so it can.
- **Position leaves are false, not throwing, with no board** — asserted, because this is the difference between an unusable action and a crashed battle.
- **The caps are inherited** — a condition of depth 5 or 17 nodes is rejected by `E3`'s compiler, proven by a planted over-limit tree rather than trusted.

## Boundaries

- **Always:** reuse `E3`'s compiler, caps, and `FactReader`; return typed refusals; keep evaluation allocation-free; order gates cheapest-first.
- **Ask first:** any new `LeafId` — that is `E3`'s closed list and a reviewed change there; extending `EntityFacts`; changing `MaxDepth` or `MaxNodes`.
- **Never:** a second condition language; a `CanAfford` leaf duplicating the cost table; a bare boolean result; a position leaf that throws when the board is absent.

## Success criteria

1. `A7` can ask "may I?" across every action × target pair with no allocation and no wasted per-target work.
2. Every refusal names its cause, so the FE can explain it and the AI can decide whether to retry.
3. Not one line of new predicate machinery — two leaf rows in `E3`, or an explicit note that resource-conditional actions wait.
4. With no board, actions remain usable or unusable — never crashing.
