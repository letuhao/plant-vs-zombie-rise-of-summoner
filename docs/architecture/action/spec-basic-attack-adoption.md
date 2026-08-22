# Spec: basic-attack-adoption (A5)

Module **A5** in the [action map](../action-map.md). Depends on **A1**, **A2**, **A4**. **This is the seam proof.**

> **The whole point of this module is to fail loudly if the design is wrong.** The action envelope's fields were chosen from FFX, SMT, and FF15, and no real action has ever been driven through them. `A5` drives the one action that already exists — and the eight goldens must not move by a byte. If they do, the model is wrong and we find out here, before six more modules are built on it.

## Objective

Express the engine's existing basic attack as a **declared action**, driven through the action model, producing **byte-identical** battle reports.

Nothing about the game changes. If a player could tell the difference, this module failed.

## Design (locked on approval)

### 1. Scope — the attack's *shape*, not the trait tail

Today's inner loop, verbatim in order:

```
Active check → CC-lock check → SelectTarget → calculator.Compute → miss? continue
  → berserker ramp → essence riders → guardian split
  → ApplyHp(target) → ApplyHp(guardian) → host.Flush()
  → DamageDealt += damage + rider → ReviveImmortals → death recording
```

**Only the first four steps are adopted.** Everything from the berserker ramp onward is `EngineBehavior` trait logic and the death/revive cycle — those belong to the AI, rewards, and trigger-phase layers, and the atom program's `E12` explicitly leaves the seven `EngineBehavior` traits where they are. `A5` does not touch them.

So the action says: **"deal `Atk` as the attacker's element components to the selected enemy."** The tail after `calculator.Compute` stays engine code.

That is still the proof it needs to be: the action row, the target rule, the usability gates, and the full commit → resolve → finish cycle all drive a real action, and the bytes do not move.

### 2. The envelope must be degenerate, and that is the point

| Field | Value | Why |
|---|---|---|
| `windup_ticks` | 0 | The attack resolves in the same instant it commits, as today |
| `resolve_offsets_json` | `[0]` | Single hit |
| `recovery_ticks` | 0 | No lockout exists today |
| `time_cost_ticks` | 0 | The round loop, not readiness, still paces this |
| `slot_consuming` | false | No `W` contention exists in the round loop |
| `commitment` | `LateBound` | Matches today: the target is read at the moment of resolution |
| cooldown | `None` | |
| costs | none | A basic attack is free — which is why `A3` is sequenced *after* this module |

An all-zero envelope proves plumbing and nothing else — which is exactly right here. `A12`'s real action under a real profile is where non-zero timing gets exercised; conflating the two would put a behaviour change inside the byte-identity gate.

### 3. ⚠️ The hazard found while reading the shipped loop

`SelectTarget`'s default branch takes **the first active enemy in `actors` list order**:

```csharp
foreach (var a in actors)
    if (a.Active && a.Setup.Side != attacker.Setup.Side) { target = a; break; }
```

`TargetResolver` sorts its pool by **ordinal ptr** before selecting:

```csharp
pool.Sort((a, b) => string.Compare(a.Ptr, b.Ptr, StringComparison.OrdinalIgnoreCase));
```

**These are different orders, and routing the basic attack through `TargetResolver` unchanged would retarget it and move every golden.** The ordinal sort is correct and deliberate for effect targeting — it was added because dictionary enumeration order once leaked into report bytes — but the battle engine's list order is equally real, and the goldens encode it.

**Resolution: `A2` gains an explicit `Ordering` field — `SourceOrder | OrdinalPtr`.** New content defaults to `OrdinalPtr`; the basic attack is authored `SourceOrder`. The difference becomes a visible data value instead of two code paths that silently disagree, and any future action can state which it means.

This is exactly the class of thing `A5` exists to surface, and it was invisible from the docs.

### 4. Behaviours that must survive untouched

Each is a fixture, and each has already been identified as a byte-identity hazard:

| # | Behaviour | Breaks if |
|---|---|---|
| 1 | **Initiative draws happen inside the `OrderBy` key selector**, once per actor in list order filtered to `Active` | The action layer reorders or defers the draw |
| 2 | **CC-locked actors still draw initiative**, then skip their turn | The CC check moves before the ordering |
| 3 | **No valid target `break`s the round** — it does not `continue` | Modelled as a per-actor "pass", which would let the round run on |
| 4 | **A miss `continue`s**, but the crit stream has already advanced | The action layer skips `Compute` when it predicts a miss |
| 5 | **Essence rider draws happen only on a landed hit** | Riders are rolled before the hit check, desyncing the essence stream |
| 6 | **One `host.Flush()` per attack** | The action layer batches or splits the funnel window |
| 7 | **Element components come from `attacker.AttackComponents`** and reach `ApplyHp` | The action carries its own element payload instead |

Hazards 3 and 4 are the ones most likely to be "improved" by accident, because both look like bugs and neither is.

### 5. Envelope gaps fold in here

Per map decision D3, the three fields missing against the Chaos grounding land in this module, while goldens have not moved and the change is free:

- duration `min` / `max` bounds, so a stat cannot drive a cast to zero
- a **cooldown-reduction channel**
- `interrupt_cooldown_milli` (default `1000‰`), replacing `ActionRunner.Interrupt`'s current behaviour of starting no cooldown

All three are **additive and inert** for a zero-envelope attack, which is why this is the cheapest possible moment to add them.

### 6. Grid parameters are present and inert

`min_range` / `max_range` / `range_channel` / `anchor_source` / `requires_line_of_sight` are authored on the row. With no board every range check **passes** (`A2` §4), so targeting is unchanged. This module is where that rule stops being a design intention and becomes a tested one.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~BasicAttackAdoption"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Guard.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1 ; .\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Actions/BasicAttack.cs        (the authored row + its intrinsic binding)
src/FusionRpg.Core/Battle/BattleEngine.cs        (the inner loop calls the action path)
tests/FusionRpg.Core.Tests/Actions/BasicAttackAdoptionTests.cs
```

## Testing strategy

**The gate is the eight goldens, unchanged, with no test edited.** Everything else is diagnosis for when that fails.

- **All eight goldens byte-identical**, `RulesetVersion` still 2, and the content hash unmoved. A re-bless here means the model is wrong — stop, do not bless.
- **A parity ladder recording values, not counts.** Draw sequences per stream (`initiative`, `crit`, `essence`, `status`), target ptr per attack, and signed delta per apply, captured before and after via `BattleTrace` and compared element-wise. Counts matching while values differ is the failure a count-only comparison misses.
- **One fixture per hazard in §4** — seven tests, each engineered so that "improving" that behaviour turns it red. The no-target `break` and the miss `continue` are the two worth writing first.
- **`SourceOrder` versus `OrdinalPtr` produces different targets** on a board where the two disagree, proving §3's field is load-bearing rather than decorative. Without this test the field can be dropped and nothing complains until a golden moves.
- **No-board range passes**, asserted here as well as in `A2` — one test proves the rule, this one proves the freeze depends on it.
- **Six suites green with no test edits**: Core, Data, Guard, CheatCore, Launcher, E2E.

## Boundaries

- **Always:** keep the trait tail where it is; keep draw order, flush count, and `break`/`continue` semantics exactly; add the envelope gaps additively.
- **Ask first:** anything that would move a golden. There is no version of this module in which that is acceptable — it means the design needs changing, not the baseline.
- **Never:** re-bless a golden in this module. Never move `EngineBehavior` trait logic here — that is `E12` and the AI layer. Never let the action carry its own element payload.

## Success criteria

1. The basic attack is a **declared action** and the eight goldens are byte-identical.
2. The three envelope gaps are in, inert, and cost nothing.
3. `A2`'s typed target contract drove a real action — including the ordering distinction it exposed.
4. Any wrongness in `A1`'s model surfaced **here**, and not after `A3`, `A6`, `A7`, `A8`, and `A9` were built on top of it.
