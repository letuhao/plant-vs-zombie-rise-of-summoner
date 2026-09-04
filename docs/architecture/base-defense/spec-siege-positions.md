# Spec: `siege-positions`

**Module 9 of 21 · level 4 · depends on `siege-board`, `combatant-kind` · [base-defense-map.md](../base-defense-map.md)**
**Status:** spec, 2026-09-04.

---

## Objective

**Turn on the three inert lines that make the board visible to everything already built for it.**

This is the module the ideal's §3 inventory was written to justify. Positional targeting is not a new
capability — `ActionTargetResolver`, `UsabilityEvaluator`, `GridDistance` and `StubIntentSource` are
all built and all correct. They are wired to a `PositionOf` that returns `null`.

Three lines, and they are **wiring gaps**, not architectural limits:

| # | `file:line` | Inert form | Why it is inert |
|---|---|---|---|
| 1 | `BattleRunState.cs:407` | `public GridPos? PositionOf(string actorKey) => null;` | *"no board exists"* — its own comment |
| 2 | `EffectBag.BoardSnapshot` | never assigned in a battle | the battle host never had a board to assign |
| 3 | `ActionValidator` `boardAvailable` | `false` at every production call site | same |

**Success looks like:** with a board, range gates bite, `Area` actions are legal, and "nearest enemy"
means nearest — and with no board, every one of those behaves exactly as it does today.

---

## What already exists (verified at HEAD, 2026-09-04)

**Built, and this is the module's whole argument.**

- `ActionTargetResolver.cs:37,60` — takes a `BoardSnapshot`, narrows to in-range, re-wraps.
- `GridDistance.InRange` — *"with no board, every range check passes"*.
- `ActionValidator.cs:41` — `Area` targeting rejected when `!boardAvailable`.
- `ActionSeeder.cs:51` — `boardAvailable` filters eligible shapes at seed time.
- `StubIntentSource.cs:50-51,107,121` — reads `PositionOf` for caster/target and for nearest-target
  selection; `:101` documents the fallback: with `PositionOf` null it uses *"plain listed order —
  `SourceOrder`"*.
- `IBattleView.PositionOf` — the seam, with `null` as the documented absence sentinel.
- `BasicAttack.cs:40,212` — already delegates `PositionOf` through its wrapper.

**The vocabulary is already large and already closed.** Per `CLAUDE.md`'s RPG-layer rule, the right
question is never *"can the lawn express this"* — it is whether the RPG layer has the channel and
whether it is wired. Here: it has, and it is not. This module is the wiring.

---

## ⚠️ Name collision, again — two different `BoardSnapshot`s

There are **two** board concepts and they must not be confused:

| Type | Is | Used by |
|---|---|---|
| `Core/Combat/BoardSnapshot.cs` | the **injector's live lawn capture** — `BoardEntitySnap`, `FindPtr`. `BattlefieldScopeExecutor.cs:11` records its shape *"matches the injector's live capture fields exactly"* | `CombatDamageDispatcher`, `TargetResolver`, `EffectBag.BoardSnapshot` |
| `Core/Battle/Board/BoardState` (this program) | the **tactical grid** from `siege-board` | the siege |

**They are not the same thing and neither becomes the other.** This module *adapts*: it projects a
`BoardState` into a `BoardSnapshot` so the existing combat consumers work unchanged.

```csharp
/// <summary>
/// Projects the tactical board into the injector-shaped snapshot the combat path already consumes.
/// An ADAPTER, deliberately — Core/Combat/BoardSnapshot's field shape mirrors the injector's live
/// capture and must not drift toward a grid, and the tactical board must not grow ptr semantics.
/// Two representations, one conversion, in one place.
/// </summary>
public static BoardSnapshot ToCombatSnapshot(BoardState board, BattleRunState state);
```

Without this adapter the temptation is to widen `Core/Combat/BoardSnapshot` with grid fields, which
would couple the injector's capture format to a tactical-board feature it will never use.

---

## The contract

### 1. `PositionOf` becomes real, and stays null-safe

```csharp
// BattleRunState.cs:407 — was `=> null` ("no board exists"). Now: null when there still isn't one,
// which is every caller that does not supply a board, which is every caller outside a siege.
public GridPos? PositionOf(string actorKey) =>
    _board is { } b && b.Positions.TryGetValue(actorKey, out var p) ? p : null;
```

**Unknown actor returns `null`, it does not throw.** A dead actor is removed from the board but may
still be named by an in-flight effect, and `null` already means "no position" everywhere in this
codebase. Throwing would convert a benign race into a crash.

### 2. `EffectBag.BoardSnapshot` is assigned

Assigned once, when the effect host is constructed for a battle **that has a board**. Left unassigned
otherwise, which is today's behaviour.

### 3. `Status.Tick` gets a board

Status effects with positional conditions (an aura that only reaches adjacent cells, a burn that
spreads) need one. The board is passed as an **optional trailing parameter**, following the exact
pattern `BattleEngine.Resolve` already uses for its four optional collaborators —
`trace`, `actionCatalog`, `containerResolver`, `intentSource`, each added the same additive way.

`null` means no board and preserves current behaviour precisely.

### 4. `boardAvailable: true` at the one production call site

`ActionValidator` and `ActionSeeder` both already branch correctly. The change is that the siege path
passes `true`. **One call site**, and its consequence — `Area` actions become legal — is what
`ActionValidator.cs:41` was written to gate.

### 5. Initial placement

`district-layout` supplies deterministic default placement (its §Open questions, recommendation (a)):
defenders in the `Core` by spiral order, attackers along the entry edge.

**This module makes that placement real; it does not make it interactive.** Pre-battle deployment
(decision 5) is a `siege-construction` and FE concern. Auto-resolve at step 7 must work with the
default placement alone — that is what makes step 7 the standalone-first gate.

### 6. Deterministic placement order

Actors are placed in **ordinal key order**, never in roster order and never in dictionary order:

```csharp
foreach (var actor in state.Actors.OrderBy(a => a.Key, StringComparer.Ordinal))
```

Same discipline as `LegionSupply.Resolve`'s `.OrderBy(x => x.Entity.EntityId, StringComparer.Ordinal)`.
Placement order determines which actor gets a contested cell, so it determines the battle.

---

## Tunables

**None.** Cell coordinates come from `district-layout`; costs from `siege-board`.

## Numeric types

None introduced. `GridPos` is `(int Row, int Col)` and stays so.

## Boundaries

**Always:** `null` board → today's behaviour, exactly · ordinal ordering for placement · adapt between
the two board types, never merge them.

**Ask first:** changing `IBattleView`'s signature — its doc comment says fog will swap the
implementation, and a signature change now costs that later flexibility.

**Never:** throw from `PositionOf` · widen `Core/Combat/BoardSnapshot` with grid fields · make
`Status.Tick`'s board parameter required · assign `EffectBag.BoardSnapshot` in a boardless battle.

---

## Testing

| Test | Asserts |
|---|---|
| `All_twelve_goldens_byte_identical_with_no_board` | **the gate** |
| `Position_of_returns_null_without_a_board` | `:407`'s contract preserved |
| `Position_of_returns_null_for_an_unknown_actor` | including one that just died |
| `Range_gates_bite_with_a_board` | an out-of-range target is excluded — and included without a board, per `GridDistance`'s documented rule |
| `Area_actions_are_legal_with_a_board_and_rejected_without` | both directions of `ActionValidator.cs:41` |
| `Nearest_target_is_nearest_with_a_board` | and falls back to `SourceOrder` without one (`StubIntentSource.cs:101`) |
| `Placement_is_identical_across_10000_runs` | ordinal ordering, and a contested cell resolves the same way every time |
| `Board_snapshot_adapter_round_trips` | positions and sides preserved |
| `Effect_bag_board_snapshot_is_unassigned_without_a_board` | |
| `Status_tick_without_a_board_is_byte_identical` | the optional parameter is genuinely optional |

## Success criteria

1. All twelve goldens byte-identical with no board.
2. Three inert lines are live and each has a test proving **both** the board and no-board paths.
3. `Core/Combat/BoardSnapshot` is unmodified.
4. Placement is deterministic over 10,000 runs.
5. No signature in `Core/Actions` changed.

## Open questions

None. `district-layout` carries the one placement question and recommends the answer this module
builds against.
