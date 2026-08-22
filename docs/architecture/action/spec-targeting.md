# Spec: targeting (A2)

Module **A2** in the [action map](../action-map.md). Depends on **A1**.

> **This module does not build targeting.** `TargetSpec`, `TargetResolver`, `BoardSnapshot`, and `CombatPolicy` ship and work — every mode the action program needs (`Single` · `Multi` · `Random` · `All` · `Area`) already resolves, sorts deterministically by ordinal ptr, and takes an injected `ICombatRng`. What is missing is an **authoring contract** and two **caster-relative** gates.

## Objective

Give actions a **typed, closed** way to say who they hit, compiling to the shipped resolver — and add the two things a caster-relative action needs that a board-anchored effect never did.

Three problems, precisely:

**1. `TargetSpec` is a wire DTO, not an authoring surface.** Modes are strings compared with `OrdinalIgnoreCase`; filters are `Dictionary<string, object?>` parsed per call. Content cannot inherit that — the atom program's rule is **no dictionaries, no string comparison** on the per-hit path, and its conditions are a typed tree over a closed leaf list. Actions must not reintroduce what atoms removed.

**2. `side` is absolute, so an action cannot say "enemies."** The filter matches `"plant"` or `"zombie"` literally. Authored once, an action means the same side for everybody — so today the same skill needs two rows, one per faction, and they drift.

**3. Nothing is caster-relative in space.** `row` and `col` filters are absolute board coordinates and `Area` is anchored at a **cell**, not gated by distance from the caster. "Enemies within 3 of me" is not expressible, and no `distance(a, b)` exists anywhere in the codebase.

## Design (locked on approval)

### 1. `ActionTargetSpec` — the typed contract

A record of closed enums and integers. No strings compared at runtime, no dictionaries, no `object?`.

| Field | Type | Notes |
|---|---|---|
| `Mode` | enum `Self · Single · Multi · Random · All · Area` | `EventTarget`, `Actor`, and `Selected` are **not** exposed — they are capture-path and debug modes, not action authoring |
| `Relation` | enum `Self · Ally · Enemy · Any` | §2 — replaces the absolute `side` filter |
| `Count` | int? | For `Multi` / `Random` |
| `Shape` | enum? `Row · Column · Square · Rectangle` | `Area` only |
| `Size` / `Width` / `Height` | int? | `Area` only |
| `AnchorSource` | enum `Caster · PrimaryTarget · ChosenCell` | §4 — distinct from `AnchorOrigin` (`Corner`/`Center`), which is rectangle geometry |
| `Filters` | `ActionTargetFilters` | A typed record, §3 |
| `MaxTargets` | int? | Capped by `CombatPolicy.ResolveMaxTargets` as today |
| `Ordering` | enum `OrdinalPtr · SourceOrder` | §2a — added by `A5`. Defaults to `OrdinalPtr` |

### 2a. `Ordering` — added because `A5` found the two orders disagree

`TargetResolver` sorts its pool by **ordinal ptr** before selecting. The battle engine's `SelectTarget` takes **the first active enemy in `actors` list order**. Those are different, and routing the basic attack through the resolver unchanged would retarget it and **move every golden**.

Both orders are correct for their own reason. The ordinal sort was added deliberately — dictionary enumeration order once leaked into report bytes in this codebase. The engine's list order is equally real, and the goldens encode it.

So the choice becomes **a visible data value rather than two code paths that silently disagree**: new content defaults to `OrdinalPtr`; the basic attack is authored `SourceOrder`. Any future action states which it means.

`SourceOrder` is the order entities appear in the `BoardSnapshot`, so building the snapshot in engine list order is what makes it faithful — a detail `A5` must assert rather than assume.

Range (`MinRange` / `MaxRange` / `RangeChannel`) lives on `rpg_action`, not here — it gates **whether the action may be used at all** (`A4`) as well as which targets qualify, so it is not a property of the target rule alone.

### 2. `Relation` — resolved against the caster, precompiled per side

An action says `Enemy`; the resolver needs `"plant"` or `"zombie"`.

**Compile both.** For a given action, `Relation` has at most two concrete resolutions, so `A6` emits a `TargetSpec` **per caster side** and the runtime picks one by index. No per-call `TargetSpec` construction, no dictionary build, and the shipped resolver is used unmodified.

`Self` resolves to the caster's ptr directly and never enters the pool. `Any` clears the side filter.

**This is the single highest-value item in the module** — it is what lets one authored action serve both factions, and it is the difference between a content library and two content libraries that drift.

### 3. `ActionTargetFilters` — typed, covering exactly what ships

Today's filter keys, given types: `side` (replaced by `Relation`), `typeId`, `typeIdIn`, `excludeMindControlled`, `row`, `col` (exact or `{min,max}`).

| Field | Type |
|---|---|
| `TypeIds` | `IReadOnlyList<int>?` — subsumes both `typeId` and `typeIdIn`; one is a list of one |
| `ExcludeMindControlled` | `bool?` — null keeps today's default (true unless the side is explicitly plant) |
| `Row` / `ColMin` / `ColMax` | int? — absolute board filters, kept because content may legitimately want "the front column" |

Anything not on this list is **rejected at authoring**, not ignored. Growing the list is a reviewed change, matching the atom program's closed-leaf discipline.

### 4. Range — the gate that does not exist yet

`MaxRange` is Chebyshev cells: `max(|Δcol|, |Δrow|)`. Not arbitrary — the shipped `Square` area shape of size *n* **is** a Chebyshev ball of radius `(n−1)/2`, so this is the metric the existing code already implies. Manhattan would contradict a shape that ships.

`MinRange` exists from day one because a minimum cannot be retrofitted: adding one after actions are authored rewrites every row.

**With no board — and there is no board until `A10` — every range check passes.**

> Not an error. Not an empty result. **Passes.**

This is what keeps `A5` byte-identical: with no coordinates, range excludes nobody and targeting behaves exactly as it does today. A range check that throws or returns empty when coordinates are absent breaks the freeze, and that is the single most important line in this spec.

`Area` is the exception and is handled loudly: it needs cells to enumerate, so **an action with `Mode = Area` is rejected at bind time while no board exists**, following the atom program's bind-time-rejection precedent. Loud beats a silent no-op.

### 5. What compiles where

```
authoring:  ActionTargetSpec (typed, in rows)
   ↓ A6 compile
runtime:    TargetSpec[2]  — one per caster side, built once
   ↓ per resolve
            TargetResolver.Resolve(spec, board, ev, policy, rng)   [shipped, unmodified]
   ↓
            range gate: Chebyshev filter, or pass-through with no board
   ↓
            IReadOnlyList<string> ptrs
```

The range gate runs **after** the resolver rather than inside it, so the shipped resolver is untouched and the gate is trivially provable as a no-op without a board.

### 6. Determinism and allocation

- Ordering follows `Ordering` (§2a). The range gate must be a **stable filter** over that order, never a re-sort.
- The gate applies **before** the random pick, or the same seed gives different results depending on who was out of range.
- `Core/Actions/` is outside the **tick-path** rules but **inside the purity rules** (`A1` §9). LINQ and per-call allocation are permitted here; a wall-clock read, an ambient `Random`, or a `double` is not.

#### 6a. `Random` needs a named stream — it does not have one (audit C2)

`TargetResolver.PickRandom` takes an injected `ICombatRng`, which is what makes it replay-safe. The battle's named streams are `initiative`, `crit`, `essence`, and `status`. **There is no `target`.**

An action using `Mode = Random` with no named stream either draws from an unnamed source — nondeterministic — or borrows an existing one and **desyncs every draw after it**. The second is worse: it produces a plausible battle that does not replay.

> **A `target` stream is derived like the others: `SeededRng.DeriveStream(seed, "target")`.**

Adding the stream is inert until something draws from it, so it is free to introduce. **The first action that uses `Random` is golden-affecting** and belongs in the movers bucket — not in `A5`, whose basic attack is `Single`.

#### 6b. Filters are re-parsed on every resolve (audit C3)

An earlier draft claimed precompiling `TargetSpec[2]` avoids a per-call dictionary. It does not. Inside the shipped resolver:

```csharp
var map = JsonOverlay.FromObject(filters);   // FilterPool — every Resolve call
```

The outer `TargetSpec` is precompiled; the filter dictionary inside it is **parsed again per call**, and `A7` evaluates candidates in a loop. This is a parse per candidate.

**Resolution: compile filters once into a predicate over `BoardEntitySnap`** and pass the pre-filtered pool, rather than handing `TargetResolver` a dictionary it re-reads. The typed filter record (§3) already has everything needed to build that predicate at load.

If that turns out to require touching the shipped resolver, the fallback is to accept the parse and **measure it** — but the spec must not claim a property the code does not have. That is how a perf assumption becomes a perf surprise.

#### 6c. One distance function, two callers (audit I6)

`A4`'s gate asks *"is any target in range"*; this module's gate asks *"which targets qualify"*. Both are real questions and both use the same Chebyshev function against the same board. **One implementation in `GridDistance`, two callers** — two copies of one rule is how they drift.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionTargeting"
```

## Structure

```
src/FusionRpg.Core/Actions/ActionTargetSpec.cs      (typed record + closed enums)
src/FusionRpg.Core/Actions/ActionTargetFilters.cs
src/FusionRpg.Core/Actions/TargetSpecCompiler.cs    (typed → TargetSpec[2], per side)
src/FusionRpg.Core/Actions/GridDistance.cs          (Chebyshev + no-board short circuit)
src/FusionRpg.Core/Actions/ActionTargetResolver.cs  (compile output → shipped resolver → range gate)
tests/FusionRpg.Core.Tests/Actions/
```

## Testing strategy

- **One authored action serves both sides.** `Relation = Enemy` compiled for a plant caster and a zombie caster picks opposite pools from the same row. This is the module's reason to exist, so it is the first test.
- **No-board range passes** — asserted directly, and asserted *again* as part of `A5`'s byte-identity fixture. Two tests on purpose: one proves the rule, one proves the freeze depends on it.
- **Range gates before the random pick** — same seed, same board, one target moved out of range: the surviving picks must match the in-range subset, not a reshuffle. A gate applied after the pick passes a naive test and fails this one.
- **Chebyshev matches the shipped `Square` shape** — a `Square` of size *n* centred on a cell must contain exactly the cells within Chebyshev radius `(n−1)/2`. If this fails, the metric choice is wrong, not the test.
- **Unknown filter keys are rejected**, proven against a planted unknown key rather than assumed.
- **`Area` with no board is rejected at bind time**, not silently empty.
- **Ordering is ordinal and stable through the gate** — filtering must not reorder, proven with a set whose in-range members are non-adjacent in sort order.

## Boundaries

- **Always:** compile to the shipped `TargetResolver`; keep ordinal ordering; keep the injected RNG; reject unknown authoring values.
- **Ask first:** exposing `EventTarget` / `Actor` / `Selected` to action authoring; adding a filter key; adding an area shape; changing the distance metric.
- **Never:** a second resolver; a dictionary or string comparison on the authoring or resolve path; a range check that throws or empties when there is no board; re-sorting after the gate.

## Success criteria

1. One authored action works for both factions — no per-side duplicate rows.
2. `"enemies within 3 of me"` is expressible; today it is not.
3. With no board, targeting behaves exactly as it does today, and `A5` proves it byte-identically.
4. No dictionary and no runtime string comparison survives on the authoring path.
5. `TargetResolver` is called, not modified.
