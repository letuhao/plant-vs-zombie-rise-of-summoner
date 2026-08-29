# Spec: action-container-binding (A18a)

Module **A18a** in the [action map](../action-map.md) §12.1. First of the A18a–e split — see that
section for why a single "resolve whichever action A17 chose" module turned out to bundle five
independently testable capabilities. Depends on A17 (built, Checkpoint E closed 2026-08-28).

> **Read [action-map.md](../action-map.md) §12 and §12.1 before this spec.** It records the split,
> the dependency order (A18a → A18b → {A18c, A18d} → A18e), and what stays out of scope (grant-writer,
> Server/API, FE, board/movement).

## Objective

Define the **ephemeral binding seam**: what it means to attach a chosen `CompiledAction`'s atom
container to a real battle actor, for one battle, with no durable grant-writer anywhere in the
picture. Every later A18 sub-module (b–e) needs this seam to exist before it has anything to fire.

**What "done" looks like:** a `CompiledAction` carrying a non-empty `ContainerId` resolves, once at
`BattleRunState` construction (mirroring T36's compile-once loadout pattern), to a real
`EffectGrantDto` bound into `BattleEffectHost.Bag` under that actor's `entity:{key}` owner key — the
exact same DTO shape the Funnel and the injector already accept, so the sealed Foundation contract is
untouched. Nothing fires yet (A18b's job); this module only proves the grant exists and is queryable.

**What this module does NOT do:**
- Fire any trigger or execute any action plan — that is A18b (`OnActivate`) plus whichever of A18c/d/e
  owns the landed kind.
- Resolve a container's **weighted pool** (`effect_container_pool`) via `Instantiator.Draw` — every
  container this module binds is **fixed-core only**. `Instantiator`'s full roll pipeline (moment 2:
  `OnInstantiate` values, `rollSeed`, `ThetaContent`/content-scale) is shaped for **item drops**, not
  **repeated skill casts within one already-resolved battle** — re-rolling variance on every use of a
  skill an actor already owns is a different feature (and a real design question for a later module,
  not silently answered here). A pooled container is a loud bind-time rejection, not a silent partial
  bind.
- Build a durable `EffectBinding`/`InstanceRow`. §12's own scope call ("no real player-owned loadout
  persistence yet") extends to this seam: the grant this module creates lives exactly as long as the
  `BattleRunState` that created it — same lifetime as `Cooldowns`, `Shields`, and every other
  battle-local runtime piece.

## Assumptions I'm making — correct me now or I proceed with these

1. **The container→effectId resolution happens OUTSIDE `BattleEngine`, at the same seam `ActionCatalog`
   already uses.** Verified against code: `ActionCompiler.Compile` (`Actions/ActionCompiler.cs:17`)
   takes `containerAtomIds` only to validate scope references (`ActionValidator.ValidateScope`) — it
   passes `row.ContainerId` straight through to `CompiledAction.ContainerId` **unresolved**. No
   existing code anywhere turns a `ContainerId` into the `EffectDefDto.EffectId`(s)
   `AtomCompiler.Compile` produced from that container's atoms (confirmed: `EffectGrantDto`/
   `EffectDefDto`, `FusionRpg.Contracts/EffectDtos.cs:75-132`, carry no `containerId` field at all —
   `AtomCompiler` groups atoms by ICD key, not by container, so one container can compile to **one or
   more** `effectId`s). Building this resolution is this module's actual work, not a pre-existing gap
   to route around.
2. **The resolver is supplied by the caller, not computed by `BattleEngine`.** Same pattern A17 used
   for `ActionCatalog?` (an optional constructor parameter, `null` meaning "nothing to resolve") —
   `BattleEngine`/`BattleRunState` stay decoupled from atom-layer internals (A5's own boundary:
   "no effect vocabulary invented here"). A20 (`synthetic-loadout-harness`) is the natural production
   supplier once it exists; until then, tests construct one directly, matching how A17's tests built
   `ActionCatalog` directly.
3. **Grant-once-at-compile, not grant-per-use.** Verified against the existing Foundation model
   (`EffectBag.Grant`/`OnEvent`, `Effects/EffectBag.cs:180,311`): a grant is a **binding** (mostly
   persistent — "equipped"), and firing an event against already-bound grants is how the *existing*
   `OnDamageDealt`/`OnSpawn` triggers already work everywhere else in this codebase. Re-granting on
   every activation would be a second, parallel binding model invented for this one case. So: every
   actor with a non-empty `EquippedActionIds` gets its container(s) bound **once**, at
   `BattleRunState` construction, alongside T36's existing loadout-compile loop — regardless of
   whether that action is ever chosen that battle (same as a trait's atoms being live the moment it is
   equipped). A18e's later "timed, reverting buff" work is what makes a **triggered** `stat.modify`
   different from today's already-working **permanent, no-trigger** case (E1's own documented split,
   `AtomKindRegistry.cs:100-102`) — this module does not need to solve that; it only needs the grant
   to exist so A18b–e have something to fire against.

If any of these three is wrong, say so before A18b is specced against it — every later sub-module
inherits this one's shape.

## Design

### 1. `IContainerEffectResolver` — the seam

```csharp
namespace FusionRpg.Core.Actions;

/// <summary>Resolves a compiled action's ContainerId to the EffectDefDto ids AtomCompiler produced
/// from that container's atoms — the seam A20 (synthetic-loadout-harness) is the production supplier
/// for; tests construct one directly, same as ActionCatalog today.</summary>
public interface IContainerEffectResolver
{
    /// <summary>Empty span for a non-existent or pooled container — loud rejection at bind time
    /// (see §2), never a silent skip.</summary>
    IReadOnlyList<string> EffectIdsFor(string containerId);
}
```

A minimal, in-memory `DictionaryContainerEffectResolver` (constructed from a
`IReadOnlyDictionary<string, IReadOnlyList<string>>`) ships with this module as the test-facing
default — the same weight class as `ActionCatalog.Build`, not a new content-authoring pipeline.

### 2. Binding, once, at `BattleRunState` construction

Extends T36's existing loadout-compile loop (`BattleRunState.cs:203-227`) rather than adding a second
pass over `Actors`:

```
foreach actor with a compiled loadout (existing T36 loop):
    foreach CompiledAction in the actor's held actions:
        if action.ContainerId is empty -> skip (basic attack and every container-less action; today
            that is everything, since A20 has not shipped)
        effectIds = resolver.EffectIdsFor(action.ContainerId)
        if effectIds.Count == 0 -> throw ArgumentException naming the actor key and the container id
            (a non-empty ContainerId that resolves to nothing is loud, matching A17's own
            "an equipped id must resolve against a real catalog" precedent — never a silent no-op)
        foreach effectId in effectIds:
            Bag.Grant(new EffectGrantDto {
                GrantId = $"battle:{actorKey}:{action.ActionId}:{effectId}",
                EffectId = effectId,
                OwnerKind = "entity",
                OwnerKey = EffectOwnerKeys.Entity(actorKey),
                PluginId = "battle",
                Priority = 0,
            })
```

`resolver` is a new optional constructor parameter on `BattleRunState`/`BattleEngine.Resolve`
(`IContainerEffectResolver? containerResolver = null`) — an 8th optional trailing parameter, same
additive pattern B14's `profile` and A17's `actionCatalog` already established. `null` is legal
exactly when no actor's loadout carries a non-empty `ContainerId` (true for every caller today); a
`CompiledAction` with a `ContainerId` and no resolver supplied throws loudly, same shape as A17's
`actionCatalog` check.

`GrantId` is deterministic and battle-scoped (`battle:{actorKey}:{actionId}:{effectId}`) — two
resolves of the same setup grant byte-identical ids, matching every other determinism discipline this
program already holds (T36's own "two battles against the same catalog are independent and
deterministic" test).

### 3. What "bound" proves, at this checkpoint

`Bag.HasAnyGrant()` is true after construction whenever any actor's loadout carries a real container.
Nothing has fired. The acceptance bar for this module is narrow and deliberately so: prove the grant
exists, correctly owned, correctly ided — not that anything happens yet.

## Tunables

None. This module wires an existing mechanism (`EffectBag.Grant`) against a new caller; it authors no
balance number.

## Numeric types

None new. `GrantId`/`EffectId`/`OwnerKey` are strings, matching the existing `EffectGrantDto` shape.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~ActionContainerBinding"
dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-funnel-delta.ps1 ; .\scripts\guard-single-writer.ps1 ; .\scripts\guard-dal.ps1
```

## Project structure

```
src/FusionRpg.Core/Actions/IContainerEffectResolver.cs   (new: interface + Dictionary-backed default)
src/FusionRpg.Core/Battle/BattleRunState.cs              (extends T36's loadout-compile loop)
src/FusionRpg.Core/Battle/BattleEngine.cs                (containerResolver param threaded through Resolve)
tests/FusionRpg.Core.Tests/Battle/Adoption/ActionContainerBindingTests.cs
```

## Testing strategy

- **No container, no resolver needed** — every actor with an empty/default `ContainerId` (basic
  attack, every A17-era synthetic loadout) resolves exactly as before; `Bag.HasAnyGrant()` stays
  false. Proves this module is additive, not a behavior change to anything that already works.
- **A real container binds to the right owner** — construct a resolver mapping one containerId to one
  effectId that exists in `EffectAtomCatalog.CreateAll()` (a real, shipped def — not a synthetic one,
  so the DTO shape is proven against real content); assert `Bag`'s grant store holds it under
  `entity:{actorKey}`.
- **An unresolvable container throws loudly** — a `ContainerId` with no resolver, and separately a
  resolver that returns empty for that id, both throw `ArgumentException` naming the actor key and the
  container id.
- **A pooled container is rejected the same way an unresolvable one is, not silently fixed-core-only**
  — `IContainerEffectResolver.EffectIdsFor`'s own contract (§1) makes no distinction between "does not
  exist" and "exists but is pooled, and this module does not resolve pools" — both are simply an empty
  result, which the bind loop already rejects loudly (the same test as "an unresolvable container
  throws loudly," above). This is a deliberately unified rejection, not two separate error paths: the
  interface has no way to see INSIDE a container definition to tell the two cases apart, and building
  that visibility would mean designing against real container-schema types this module explicitly
  declines to depend on (§ Objective's own "fixed-core only" boundary). A resolver implementation is
  free to log its own reason internally; this module's own contract only promises the caller sees a
  named, loud refusal either way.
- **Determinism** — two resolves of the same setup against the same resolver instance produce
  byte-identical `GrantId`s.
- **Full suite + guards, no test edited outside this module's own new file.** Golden-neutral by
  construction: nothing here fires an event, so no existing battle's outcome can change.

## Boundaries

- **Always:** read the resolver only through `IContainerEffectResolver`, never reach into atom-layer
  internals (`ContainerRow`, `AtomRow`, `EffectAtomCatalog`) from `BattleEngine`/`BattleRunState`
  directly — the interface is the seam A18b–e (and eventually A20) build on.
- **Ask first:** widening this module to resolve pooled containers via `Instantiator.Draw` — that is a
  real design question (does a re-cast re-roll?) this spec explicitly declined to answer, not an
  oversight to quietly fix here.
- **Never:** grant per-use instead of once-at-construction (§ Assumption 3); invent a second grant-id
  or owner-key convention distinct from `EffectOwnerKeys`'s existing grammar.

## Success criteria

1. A `CompiledAction` with a real `ContainerId` resolves to one or more real `EffectGrantDto`s, bound
   into the exact same `Bag` ordinary battle-adoption code already uses (`Host.Bag`), under the
   actor's `entity:{key}` owner key.
2. An unresolvable or pooled container is a loud, named rejection — never a silent no-op and never a
   partial bind.
3. Zero goldens moved — this module only creates grants; nothing yet reads or fires them.

## Open questions

- Whether a re-cast of an already-bound skill (once A19 wires real cooldowns) should re-roll any
  `OnInstantiate`-policy values, or whether skill containers should be authored `Fixed`/`OnApply` only
  and never carry `OnInstantiate` at all. Deferred to whichever module first authors real skill content
  (A20, or later) — this module's own bind-once shape does not depend on the answer.
