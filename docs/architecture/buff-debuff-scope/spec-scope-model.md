# Spec: `scope-model`

**Module id:** `scope-model` · **Program:** [buff-debuff-scope-map.md](../buff-debuff-scope-map.md) ·
**Status:** Draft — pending owner review.

**Depends on:** nothing · **Blocks:** `battlefield-scope`, `world-map-scope` (`membership-events` does
not depend on this module — it emits events independently of how they're consumed)

---

## Assumptions I am making

Surfacing these before any code, per this repo's spec process — correct them now rather than after
the fact.

1. **Resolved, owner, 2026-08-29: extract a shared relation type, rather than duplicate or take a
   dependency on `Actions/`.** Verified where this actually lands, not left as "somewhere shared":
   `Actions/ActionTargetSpec.cs`'s own compiled form, `TargetSpec`, lives in **`FusionRpg.Contracts`**
   ([`CombatDtos.cs:45`](../../../src/FusionRpg.Contracts/CombatDtos.cs)) — an assembly both `Core`'s
   `Actions/` and (eventually) `Scope/` can depend on without one depending on the other. **But
   `TargetSpec` itself has no relation concept to extract** — it is a raw JSON wire DTO (`Filters` is an
   untyped `Dictionary<string, object?>`, `Mode`/`Shape` are bare strings with const-string helper
   classes, not enums), and structurally should stay that way; it is the injector-facing wire format,
   not a typed authoring surface. So this is not an extraction of an existing type — it is a **new**,
   small, typed enum (`RelationKind` or similar: `Self`/`Ally`/`Enemy`/`Any`) added to
   `FusionRpg.Contracts`, with `ActionTargetSpec.cs`'s own `ActionRelation` updated to reference it
   (a small, mechanical edit — same four values, same names, moved rather than duplicated) rather than
   defining its own copy. `scope-model`'s `WhoSelector` references the same Contracts-level type.
   **This is a real edit to existing, shipped code**, not scope-model's alone to make silently — worth
   its own explicit line item at Plan/Tasks time, not folded invisibly into this module's file list.
2. **`ScopeWho.OwnSide`/`EnemySide` needs a documented second delivery shape, not just a WHO value —
   and it is specific to the live-PvZ host, not "battlefield" generally.** Corrected during audit,
   2026-08-29: G8's *"the `TakeDamage` prefix reads one side-wide cached value"* was first read as a
   `battlefield`-wide constraint. It is not. `grep`-confirmed: every `TakeDamage` reference in this
   repo lives in `FusionRpg.Injector` (`EntityStatWriter.cs`, `GameHooks.cs`) — and
   `EntityStatWriter.cs`'s own doc comments on `AddPlantHp`/`AddZombieHp` say *"Never TakeDamage"*,
   confirming the RPG's write-side deliberately avoids that path; the constraint is about a Unity-side
   Harmony **read** hook that has no equivalent anywhere in `BattleEngine`/SIM. **G8 is live-PvZ-only.**
   `BattleEngine`-driven battles (expeditions/web-RPG) compute damage entirely in C#, never touch
   Unity's `TakeDamage`, and are not bound by this constraint at all.
   So `WhereScope.Battlefield` is not one execution host — it is two, sharing one grant-issuing front
   end (`EffectBag`/`owner_kind = match`) but with **materially different readers**: `BattleEffectHost`/
   `BattleEffectSink` for SIM/expeditions (this session's own A18a-e machinery), and the injector's
   already-proven overlay/Funnel path for live PvZ (patron.aura's own shipped precedent). The
   compatibility table needs a **host** sub-dimension under `Battlefield` (`Live` / `Sim`) to say which
   a given kind supports — mirroring `AtomKindRegistry`'s own existing per-runtime columns
   (`resource.delta`/`shield.grant`/`status.apply`/`stat.modify` already carry separate Battle/Sim
   support states there; this is the same shape, not a new one). `WorldMap` has no such split — it has
   exactly one host.
3. **The compatibility table is data checked against a closed enum set, not a general rule engine.**
   Mirrors `definitions.md` §9's four-state runtime-support matrix exactly — a maintained table, audited
   against code, not inferred from kind metadata.

## Objective

Define the `(WhereScope × WhoSelector)` vocabulary as pure types, plus the compatibility contract that
says which `(atom kind × WhereScope × WhoSelector)` combinations are legal and how each legal one is
delivered. No execution, no host reference — `battlefield-scope` and `world-map-scope` execute against
this; this module only defines what there is to execute.

**Users:** `battlefield-scope`, `world-map-scope` (both read the compatibility table to know what they
must support), `membership-events` (reads `WhoSelector.OwnSide`'s definition to know which FSM
transitions are scope-relevant), and — later, deferred — the aura-skill/commander work.

**Success is measurable:** a `(kind, where, who, host)` triple resolves to `Full`/`Partial`/`None` plus a
delivery-shape tag — `host` (`Live`/`Sim`) only meaningful under `WhereScope.Battlefield`, absent for
`WorldMap`; an unlisted triple rejects `ScopeUnsupported` naming all four components; the G8 case
(`stat.modify`+`defense`) resolves to the side-wide-constant shape under `Live` and the per-entity-grant
shape under `Sim` — the SAME kind, two different hosts, two different answers — proving the table
distinguishes hosts rather than treating `Battlefield` as one undifferentiated case.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~ScopeModel
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-single-writer.ps1
```

No DAL/SQL in this module (pure in-memory vocabulary), so `guard-dal.ps1` is not touched by it — the
single-writer guard is listed because `WhoSelector` will eventually gate combat-relevant reads and it
costs nothing to run early.

## Project structure

```
src/FusionRpg.Contracts/RelationKind.cs        → new: Self/Ally/Enemy/Any, the shared type (Assumption 1)
src/FusionRpg.Core/Actions/ActionTargetSpec.cs → edited: ActionRelation references RelationKind instead
                                                   of defining its own copy — same 4 values, same names
src/FusionRpg.Core/Scope/           → WhereScope.cs, WhoSelector.cs, ScopeCompatibility.cs,
                                       ScopeRejectionReason.cs (WhoSelector's side case wraps RelationKind)
tests/FusionRpg.Core.Tests/Scope/   → WhereScopeTests.cs, WhoSelectorTests.cs,
                                       ScopeCompatibilityTests.cs, ScopePurityGuardTests.cs
```

**A new top-level `Core/Scope/` directory, not nested under `Actions/` or `Effects/`.** It is used by
both a battle-shaped consumer and a world-map-shaped one; nesting it under either would misname its
actual scope. Matches the existing sibling-directory convention (`Actions/`, `Battle/`, `Effects/`,
`World/`, `Match/` are already flat siblings under `Core/`).

**Gets its own purity guard, same shape as `ActionsPurityGuardTests` (P0.1's own precedent):** no wall
clock, no ambient RNG, no floating point, no dictionary enumeration. No tick-path exemption needed —
nothing here has `TargetResolver`'s LINQ requirement, so the default kernel-wide ban stays on,
unweakened, for this directory.

## Code style

Follow `ActionTargetSpec.cs`'s established idiom exactly — it is the closest sibling in this codebase
and there is no reason to diverge:

```csharp
public enum WhereScope
{
    Battlefield = 0,
    WorldMap,
}

public static class WhereScopes
{
    public static string Name(WhereScope scope) => scope switch
    {
        WhereScope.Battlefield => "battlefield",
        WhereScope.WorldMap => "worldMap",
        _ => "",
    };

    public static bool TryParse(string? text, out WhereScope scope) { /* mirrors ActionRelations.TryParse */ }
}
```

Enums plus a paired `<X>s` static class for `Name()`/`TryParse()` — never a raw string compared at
runtime, matching `ActionRelations`/`ActionTargetModes`/`ActionAreaShapes` exactly.

**No balance number in this module.** This is structural vocabulary, not content — if a `kMilli` or a
per-mille weight appears here, it belongs in `data/tuning/`, not `Scope/` (same rule
`spec-zomboss-patterns.md` §7 states for its own module).

## Testing strategy

- **Enum round-trip:** every `WhereScope`/`WhoSelector` value survives `Name()` → `TryParse()`; an
  unknown string rejects rather than defaulting silently.
- **Compatibility table, both directions:** every listed `(kind, where, who, host)` quadruple resolves to
  its declared support state; an **unlisted** one rejects `ScopeUnsupported` naming all four components
  (mirrors T5's "a planted inverted row FAILS" discipline — asserted, not just documented).
- **The G8 case, real, both hosts:** `stat.modify`+`defense` under `(Battlefield, OwnSide, Live)`
  resolves to the side-wide-constant shape; the identical kind under `(Battlefield, OwnSide, Sim)`
  resolves to the per-entity-grant shape — the direct test for Assumption 2, proving the table
  distinguishes hosts rather than only claiming to in prose.
- **Purity:** `ScopePurityGuardTests` — a full scan plus the same six planted-violation cases
  `ActionsPurityGuardTests` uses (`DateTime`, `Random`, `Guid.NewGuid`, `.GetHashCode(`, `double`,
  `float`, dictionary enumeration), with no tick-path exemption to prove absent.
- **Architecture:** a source-scan test asserting nothing under `Core/Scope/` references
  `FusionRpg.Core.Battle`, `FusionRpg.Core.World`, or `FusionRpg.Core.Effects` — dependency direction
  stays outward, matching T33's "an architecture test fails if the intent source touches battle state
  directly" precedent from the action program.
- **`RelationKind` extraction regression:** `ActionTargetSpec.cs`'s own existing test suite (all
  `~ActionTargeting` tests, per T6-T8's evidence) re-run green after the reference swap, with zero new
  failures — a mechanical rename, proven, not just described as one.

## Boundaries

- **Always:** pure types and validation only; rejection reasons drawn from a closed, named list (mirror
  `definitions.md` §10's discipline: adding one is a reviewed change); every enum value has a `Name()`/
  `TryParse()` pair.
- **Ask first:** adding a third `WhereScope` or a sixth `WhoSelector` value beyond what's named here —
  this is meant to become an equally closed vocabulary, the same discipline the atom layer already
  enforces on its own kinds/triggers.
- **Never:** reference `Battle/`, `World/`, or `Effects/Atoms/` runtime types from `Scope/`; encode a
  balance number; assume a `(kind, where, who, host)` quadruple is legal without it being in the table;
  define a second `Self`/`Ally`/`Enemy`/`Any`-shaped enum anywhere once `RelationKind` exists.
  **`FusionRpg.Contracts` is an expected, deliberate dependency** — not an oversight of the
  "never reference Battle/World/Effects" rule above, which is about `Core`-internal subsystems only.

## Success criteria

1. `WhereScope`/`WhoSelector` types exist, each with `Name()`/`TryParse()`, tested round-trip.
2. The compatibility table resolves every listed quadruple correctly and rejects every unlisted one with
   `ScopeUnsupported` naming all four components.
3. The G8-shaped delivery-mechanism distinction (Assumption 2) — `Live` vs. `Sim` disagreeing on the same
   kind — is enforced by a passing test, not just asserted in a comment.
4. `Core/Scope/` has its own purity guard, green, with the tick-path exemption proven absent.
5. An architecture test proves `Scope/` never references `Battle/`, `World/`, or `Effects/Atoms/`.
6. `RelationKind` exists in `FusionRpg.Contracts`; `ActionTargetSpec.cs`'s `ActionRelation` references it
   with zero behavior change — its own existing tests and all 4+ known call sites stay green, unmoved.
