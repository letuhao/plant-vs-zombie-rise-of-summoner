# Spec: action-catalog (A6)

Module **A6** in the [action map](../action-map.md). Depends on **A1**, **A5**.

## Objective

Load authored action rows, compile them once into the runtime form, and hand the battle engine something it can use without touching a content row mid-fight.

## Design (locked on approval)

### 1. Actions are server-side only — there is no push

The atom program compiles content and **pushes** it to the injector, because atoms run on the lawn. Actions do not:

> **Actions are a battle-mode concept.** PvZ mode is a stateless observer with no queue and no per-actor machine — the lawn never schedules an action. The battle engine that *does* run actions is server-side.

So the injector never needs an action row, an action id, or a compiled action. **`A6` is load, compile, and cache — not compile and push**, and this module is a fraction of the size the map first implied.

If actions ever reach the lawn, that is a new decision with a new spec, and it inherits the atom program's push plumbing rather than growing a second one.

### 2. Compile once, at load

| Stage | Output |
|---|---|
| Read | `rpg_action` + `rpg_action_cost` + `rpg_action_effect_scope`, joined |
| Validate | `A1`'s validator — reject, never coerce |
| Compile | `ActionTargetSpec` → **`TargetSpec[2]`, one per caster side** (`A2` §2) · **filters → a predicate over `BoardEntitySnap`**, so nothing re-parses a dictionary per resolve (`A2` §6b) · `conditions_json` → `E3`'s compiled predicate · `ValueSpec` → curve-scaled bounds |
| Cache | Keyed by `action_id`, immutable, swapped wholesale on revision change |

**Nothing parses JSON during a battle.** The typed contract exists so the resolve path meets no dictionary, no string comparison, and no allocation it did not plan.

### 3. Revision and the content hash

Actions are content, so their rows join the **content hash** that `E8` stamps into the report beside `engineVersion`, `rngAlgoVersion`, `rulesetVersion`, and `seed`.

> A changed action number must produce a **changed hash**. Otherwise a balance tweak silently invalidates every golden and every recorded battle, and the report claims a reproducibility it no longer has.

Instances are excluded, exactly as they are for atoms — the hash covers definitions, never rolls.

### 4. Failure is loud and at load

An invalid action fails **at load**, naming the row and the reason. A battle must never discover that an action is malformed, because by then a player is mid-fight and the only options are crash or silently skip — and silently skipping is worse.

This matches the atom program's bind-time rejection stance, for the same reason.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionCatalog"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionCatalog"
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Actions/ActionCatalog.cs       (immutable cache, revision swap)
src/FusionRpg.Core/Actions/ActionCompiler.cs      (rows → runtime form)
src/FusionRpg.Data/Sqlite/RpgStore.Actions.cs     (all SQL — shared with A1)
tests/FusionRpg.Core.Tests/Actions/
```

## Testing strategy

- **A malformed row fails at load, naming the row** — one test per validation rule, each against a planted bad row so the validator is proven able to fail.
- **Nothing allocates on the resolve path after compile** — asserted in bytes, the way the kernel suite already does it.
- **A changed action value changes the content hash**, and an unchanged catalog does not. Both directions, because a hash that always changes is as useless as one that never does.
- **Revision swap is atomic** — a battle in flight keeps the catalog it started with. Content editing mid-session must not retarget a committed action.
- **No JSON is parsed after load**, proven by compiling a catalog and then asserting the parse path is never entered during a resolve.

## Boundaries

- **Always:** compile at load; keep SQL inside `FusionRpg.Data`; reject loudly and early; join the existing content hash.
- **Ask first:** anything that would send an action to the injector — that is a scope change, not a feature.
- **Never:** parse content during a battle; a second push mechanism; a hash that covers instances.

## Success criteria

1. Adding an action is **rows only** — no build, no code.
2. The battle path meets no JSON, no dictionary, and no string comparison.
3. A changed value is visible in the report hash.
4. The injector remains entirely unaware that actions exist.
