# Spec: predicate-tree (E3)

Module **E3** in the [atom effect map](../effect-atom-map.md). No dependencies; pure. Nothing in the game changes.

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Own the `when` condition of an atom: a typed AND/OR/NOT tree over a **closed** leaf list, depth-limited, rejecting unknown leaves, compiled to an evaluator that runs per hit without allocating.

This is the single place in the schema that can quietly become a programming language. Everything below is about preventing that.

## Design (locked on approval)

### Closed leaf list — 8 (plus three approved additions below — 11 total)

| Leaf | Param | Reads | Source in today's code |
|---|---|---|---|
| `sideIs` | plant \| zombie \| bullet | event | `filters.side` |
| `typeIdIs` / `typeIdIn` | int / int[] | event | `filters.typeId` |
| `actorIsKiller` | bool | event | OnDeath filter |
| `hasStatus` | statusId | StatusRuntime | new |
| `hpBelowMilli` / `hpAboveMilli` | ‰ of MaxHp | actor | berserker / coward shapes |
| `elementIs` | element id | actor | `ActorElementTypes` |
| `rowIs` / `colIs` | int | board | target filters |
| `isMindControlled` | bool | actor | `excludeMindControlled` |

Adding a leaf is a **reviewed code change** — each needs a reader. Same rule as kinds (E1).

#### Two leaves requested by the action program — approved 2026-08-22

Added by the action stream, owner-approved, and recorded here because this list is the SSOT for it. See [action/spec-usability-conditions.md](../action/spec-usability-conditions.md) (A4).

| Leaf | Param | Reads | Why |
|---|---|---|---|
| `resourceBelowMilli` | `(resourceId, ‰ of max)` | actor | *"Only usable below half spirit"* |
| `resourceAboveMilli` | `(resourceId, ‰ of max)` | actor | *"Only while qi is full"* |

**These generalise `hpBelowMilli` / `hpAboveMilli` rather than adding a new idea** — same per-mille shape, same actor read, one extra parameter naming which pool. `hp` is one of the five resources, so the existing pair becomes the special case of the new one; whether to collapse them is an implementation choice, not a vocabulary one.

**Reader requirement:** `EntityFacts` gains resource values, following `HpMilli`'s existing shape. Four ints (`stamina`, `hunger`, `spirit`, `qi` as ‰ of max) — `hp` already has its own. Resource semantics are the [resource hub](../resource-hub-ideal.md)'s; this list only needs the numbers readable.

#### A third leaf requested by the action program — approved 2026-08-27

| Leaf | Param | Reads | Why |
|---|---|---|---|
| `holdsStock` | `(stockId, minQty)` | inventory | *"do I hold ≥ 1 of this?"* — the precondition a consumable action checks |

**Owed to [item/ssot-consumables.md](../item/ssot-consumables.md) §5(c)** since 2026-08-22, and it lands
here rather than as a cost because [action/spec-action-costs.md](../action/spec-action-costs.md) §8
declined to widen `resource_id`: **costs scale with `Θ` and rungs; an item does not.** *One potion is one
potion at every level*, so an item fails the pure-number property the cost economy rests on. It is a
**precondition**, and preconditions are leaves.

**Reader requirement:** `FactReader` gains a narrow, readonly stock probe following `HpMilli`'s shape. The
count is read into the fact struct at evaluation setup — **the leaf itself performs no I/O**, per this
module's own boundary (*"never a leaf that performs I/O, reads a clock, or draws RNG"*).

**Not requested, deliberately:** `cellFree` — it needs a board, the battle board is deferred, and a leaf with nothing to read is a leaf that cannot be tested. It comes with `A10` or not at all.

### ⚠️ `OnDamageDealt` inverts side and typeId — name it, do not inherit it

On `OnDamageDealt`, today's overlay `filters.side` and `filters.typeId` refer to the **damaged** entity, not the attacker (`EffectProcAndOwner.cs:103–118`, `ResolveFilterTarget` inverts side). That is a live trap and an author will hit it on their first `searing_strike`.

**Locked:** **every** leaf declares its subject explicitly — not just side and type. The inversion is a property of *the event*, so `hasStatus` and `hpBelowMilli` on `OnDamageDealt` are exactly as ambiguous as `sideIs`. Omitting it is `AmbiguousSubject`. The legacy inversion is preserved only by the E11 migration, which writes `subject: target` into migrated rows so the 49 fixtures stay byte-identical, and the drift is recorded in the migration notes rather than carried into new content.

### Structure and limits

```csharp
sealed record PredicateNode;              // And(children) | Or(children) | Not(child) | Leaf(id, args)
```

| Limit | Value | Why |
|---|---|---|
| Max depth | **4** | a bare leaf is depth 1; `And(leaf, leaf)` is depth 2. Deep enough for `A AND (B OR (C AND D))`, shallow enough to bound cost and power pricing |
| Max nodes | **16** | counts internal nodes **and** leaves; leaf `value` args do not count. A second bound so a wide flat tree cannot evade the depth limit |
| Zero-child `And`/`Or`, or `Not` with ≠ 1 child | **rejected** (`EmptyNode`) — distinct from an *absent* predicate, which is legal and means "always" |
| Unknown leaf | **rejection** | never ignored — the rule the whole program runs on |
| Empty tree | legal | means "always", the common case |

`NOT` is included because the owner picked the expressive option; it costs one opcode and one test row, and without it half the useful conditions need a mirrored leaf.

### Compilation — per the measured benchmark

The E13 benchmark measured three shapes doing identical work: `Dictionary<string,object>` + nested-dict tree at **179 ns/atom**, a typed object graph at **7 ns**, and a *recursive* int-opcode span walker at **47 ns**. Two conclusions, and the second one surprised us:

1. **Dictionaries and string comparison are out** — 25× the cost of a typed graph, and against the perf plan's own "no dictionaries or strings allocated on the record path". This conclusion is robust.
2. ~~Recursion is out too.~~ **Withdrawn 2026-08-22.** The 7 ns winner *is* a typed object graph, and `AndNode.Evaluate` calling `child.Evaluate` **is** mutual recursion — so a no-recursion law would disqualify the form the measurement chose. The 47 ns loss is better explained by `ref int pc` plus span bounds checks defeating inlining. Blaming recursion was over-reading the data.

So: `Compile(tree) → CompiledPredicate`, with the encoding chosen by **E13 against real content** — typed graph versus flattened short-circuit ranges — not asserted here. This module ships the interface and the equivalence tests that let E13 swap encodings safely.

```csharp
CompiledPredicate.Evaluate(in FactReader facts) → bool     // no allocation; encoding chosen by E13
```

`FactReader` is a narrow readonly struct over the event, actor, and board facts the leaves need — the module never reaches into `StatusRuntime` or the board itself.

### Predicates are **not** priced — decided 2026-08-22

An earlier draft claimed a tree contributes a conditionality multiplier to E9. It does not, and cannot: nobody can compute how often "target HP below 25%" is true without simulating the game.

So `conditionality` stays at its four computable factors (definitions §7), and **an atom behind a predicate is priced as if unconditional**. That over-prices it, which is the safe direction for a budget ceiling — content is never cheaper than the budget thinks. The limitation is documented rather than papered over with a made-up constant.

The depth limit therefore exists for one reason, not two: bounding hot-path cost.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Atom.Predicate"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/PredicateNode.cs        (new — typed tree, leaf ids)
src/FusionRpg.Core/Effects/Atoms/PredicateCompiler.cs    (new — validate + compile)
src/FusionRpg.Core/Effects/Atoms/FactReader.cs           (new — readonly struct over event/actor/board)
tests/FusionRpg.Core.Tests/Atoms/PredicateCompilerTests.cs
tests/FusionRpg.Core.Tests/Atoms/PredicateEquivalenceTests.cs
```

## Testing strategy

| Case | Expect |
|---|---|
| Unknown leaf id | rejection `UnknownLeaf` — never ignored, never `false` |
| Depth 5 | rejection `DepthExceeded` |
| 17 nodes at depth 3 | rejection `NodeCountExceeded` |
| `sideIs` without `subject` | rejection `AmbiguousSubject` |
| Empty tree | evaluates `true` |
| **Equivalence fuzz** | 10⁴ random valid trees × random fact sets: compiled result ≡ a naive reference interpreter, every time. This is what lets E13 change encodings without fear |
| Short-circuit | `And(false, expensive)` does not read the second leaf's fact; asserted via a counting `FactReader` |
| Allocation probe | **zero** bytes over 10⁵ evaluations |
| ~~No recursion~~ | **test removed** — it would disqualify the measured winner (see above) |
| Missing `subject` on **any** leaf | `AmbiguousSubject` |
| Zero-child `And` | `EmptyNode` |

## Boundaries

**Always:** reject unknown leaves; declare leaf subject explicitly; keep `FactReader` narrow and readonly; keep the equivalence fuzz green before touching the encoding.

**Ask first:** adding a leaf; raising the depth or node limits; changing short-circuit semantics.

**Never:** an expression string or any parsed syntax; a dictionary or string comparison per hit; a leaf that performs I/O, reads a clock, or draws RNG; a default subject on any leaf.
