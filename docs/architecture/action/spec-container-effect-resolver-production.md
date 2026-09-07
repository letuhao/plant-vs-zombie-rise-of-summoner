# Spec: container-effect-resolver-production (A24)

Module **A24** in the [action map](../action-map.md) §15. Promotes the `container-effect-resolver-
not-wired` finding (`action-plan.md` §5, found 2026-09-06 during T59.8) from an unscheduled deferral
into a real, scheduled module — because a Stop-hook challenge correctly rejected "named as deferred"
as a legitimate closure for a gap this same program both found and self-classified, in the same
session, without the standing every OTHER deferred row on that table has (A9/A10/seedsmith predate
this program's involvement; this one does not).

> **Read `action-map.md` §14 and `spec-action-container-binding.md` (A18a) before this spec.** A18a
> already built the seam this module supplies a real implementation for — re-reading it here would
> duplicate, not correct, that spec.

## Objective

`IContainerEffectResolver` (A18a) and `BattleRunState.BindContainers` (`BattleRunState.cs:541-571`)
are correctly built and already wired into every `BattleEngine.Resolve` call — but no production
caller has ever supplied a real resolver. `WebMatchService`'s three real battle-resolve call sites
(`WebMatchService.cs:134,186,315`) pass `actionCatalog:` only; `containerResolver`/`onEffectHostReady`
stay their `null` defaults. Every action A21's importer produces has a non-empty `ContainerId` (the
composer refuses to draw zero atoms), so `BindContainers` throws `ArgumentException` at battle setup
for any real imported action a real specimen holds — proven, not assumed, by
`BuildSquadEquippedActionsTests.A_generated_imported_unlock_ladder_grant_reaches_BuildSquad_but_a_real_battle_cannot_yet_activate_it`.

**What "done" looks like:** a real, `RpgStore`-backed resolver exists, is wired into all three
`WebMatchService` call sites, and a held action whose container's atoms all classify `Compiled`
(`Compilability.AtomPath.Compiled` — a fixed-value `stat.modify`/`stat.derived`/`resource.delta`/
`status.apply`/`shield.grant` etc., no per-hit roll, no per-binding state) now binds **and fires**
inside a real `WebMatchService`-shaped battle, proven end to end, not merely "no longer throws at
setup."

**What this module does NOT do, stated precisely because the investigation that produced this spec
found the boundary the hard way:**

- **Does not make every real action activate.** The only real seed atom families that exist today
  (`atom.fortitude`, `atom.vitality`, `data/seed/atoms/generated/family-expand.g-life.json`) are
  authored `stat.modify` with `roll: onApply` and `min != max` — `Compilability.Classify`'s Rule 3
  (`Compilability.cs:149-150`) routes **both** to `AtomPath.Runner`, never `Compiled`. Verified
  directly against the seed file, not assumed from the kind name alone.
  `Instantiator.Draw` (called by `ActionCorpusComposer.Compose`, `ActionCorpusComposer.cs:121`) only
  **selects which atom ids** populate a container's fixed core — it does not roll each atom's own
  internal min/max range into a fixed value; the persisted container members are the same
  unrolled template rows `RpgStore.GetAtom` already returns for hand-authored content
  (`spec-action-container-binding.md`'s own §Objective already named this: "every container this
  module binds is fixed-core only... `Instantiator`'s full roll pipeline... is shaped for item drops,
  not repeated skill casts"). So: **this module closes the gap for the *class* of Compiled-path
  content, and provably does NOT close it for the three real actions that exist in the database
  today** — those still fail, but this module makes the *reason* precise (their atoms compile to zero
  `EffectDefDto`s) instead of generic (no resolver supplied at all). That is real, measured progress,
  named honestly rather than overstated.
- **Does not build a battle-sim consumer for the Runner path.** `RunnerEntry`/`AtomRunner`
  (`Effects/Atoms/AtomRunner.cs`, `RunnerEntry.cs`) has **zero** occurrences anywhere under
  `src/FusionRpg.Core/Battle/` — confirmed by a repo-wide grep scoped to that directory returning no
  files. `BattleEngine.Resolve` has no mechanism to execute a Runner-path atom at all, for ANY content
  type, not only actions. This is a separate, more foundational gap than A24's own scope — named below
  as `battle-runner-path-not-wired`, not attempted here.
- **Equipped-item `stat.derived` delivery (errata 2026-09-08).** Historically this section said
  `BattleStatComposer.UseEquipment` had **no** production caller — that was true at authoring time.
  **Closed:** Server boot wires `BattleStatComposer.UseEquipment(EquippedBoundAtoms.SourceFromStore(...))`
  → `FromEquippedResolver` (`equip:{role}:{itemRef}`). Residual gaps: lawn labels stay `grant:` until
  equip-tagged push without double-count; most catalog equip atoms are still `stat.modify` (content),
  not `stat.derived`.

## Assumptions I'm making — correct me now or I proceed with these

1. **The resolver is built fresh per `BattleEngine.Resolve` call, not cached across calls.**
   `WebMatchService`'s existing `_store.BuildActionCatalog(RungPolicy.Table)` is ALREADY rebuilt fresh
   at all three call sites, every real match — this module adds no new caching discipline beyond what
   the codebase already accepts for the sibling catalog it sits beside. A revision-keyed cache (the
   shape `AtomPushService`'s own `GetCatalogRevision()` short-circuit uses) is real future work, not
   required for correctness, and not built here (no premature optimization for a database that holds
   3 real actions today).
2. **Scope is exactly the containers real actions reference — never Item/Trait/Patron/WorldBuff
   containers.** The new `RpgStore.ListActionContainers()` query joins through `rpg_action.container_id`
   only, the same join `BuildActionCatalog` already performs at `RpgStore.ActionCatalog.cs:58` for a
   different purpose (scope validation, not compilation). This keeps A24 inside the action program's
   own boundary by construction, not by a runtime kind filter that could silently drift.
3. **A container whose every member atom lands on the Runner path is a loud rejection, not a silent
   skip** — falls out of `BindContainers`'s own EXISTING behavior (`BattleRunState.cs:552-556`: empty
   `EffectIdsFor` result throws `ArgumentException` naming the actor and container) once the resolver
   correctly reports zero effect ids for such a container. No new rejection code needed; this module
   only has to make sure it does not silently invent a fake non-empty answer to dodge that throw.
4. **Double-grant avoidance: use `.Defs` only, discard the compiler's own auto-emitted `.Grants`.**
   Not a new design choice — the third application of an existing, precedented pattern
   (`AtomPushService.PatronAuraAtoms()`, `AtomPushService.cs:307-309`, and `ConstructionActions`'s own
   `onEffectHostReady`, `DistrictAssaultResolver.cs:147-148`, both discard `AtomCompiler.Compile`'s
   auto-grants and let a separate, explicit call be the one source of `EffectGrantDto`s).
   `BindContainers` is that one explicit source for actions (`BattleRunState.cs:560-568`); registering
   the compiler's OWN `.Grants` too would double-grant the same effect to the same actor.
5. **`RuntimeId.Battle` is the correct compile target**, matching `ConstructionActions.cs:137`'s own
   choice for the same kind of call (a server-triggered `BattleEngine.Resolve`, not the live Unity lawn
   `AtomPushService` compiles for).

If any of these is wrong, say so before A25 (or whatever module eventually tackles
`battle-runner-path-not-wired`) is specced against this one's shape.

## Design (locked on approval)

### 1. `RpgStore.ListActionContainers()` — the scoping join

```csharp
// src/FusionRpg.Data/Sqlite/RpgStore.ActionContainerEffects.cs
public IReadOnlyList<ContainerRow> ListActionContainers()
{
    var seen = new HashSet<string>(StringComparer.Ordinal);
    var containers = new List<ContainerRow>();
    foreach (var actionId in ListActionIds())
    {
        var row = GetAction(actionId);
        if (row is null || string.IsNullOrEmpty(row.ContainerId)) continue;
        if (!seen.Add(row.ContainerId)) continue;
        var container = GetContainer(row.ContainerId);
        if (container is not null) containers.Add(container);
    }
    return containers;
}
```

Mirrors `BuildActionCatalog`'s own existing loop shape (`RpgStore.ActionCatalog.cs:42-60`) exactly —
same enumeration, same null-tolerance for a raced delete, deduplicated by container id since two
actions may share one container in principle (no shipped content does yet, but the dedup costs one
`HashSet` and removes the question).

### 2. `ActionContainerEffectResolverFactory.Build` — compile once, per container, in `FusionRpg.Data`

```csharp
// src/FusionRpg.Data/Sqlite/ActionContainerEffectResolverFactory.cs
public static class ActionContainerEffectResolverFactory
{
    public static (IContainerEffectResolver Resolver, IReadOnlyList<EffectDefDto> Defs) Build(RpgStore store)
    {
        var byContainer = new Dictionary<string, IReadOnlyList<string>>(StringComparer.Ordinal);
        var defs = new List<EffectDefDto>();

        foreach (var container in store.ListActionContainers())
        {
            var atoms = new List<AtomRow>(container.Atoms.Count);
            foreach (var entry in container.Atoms)
            {
                var atom = store.GetAtom(entry.AtomId);
                if (atom is not null) atoms.Add(atom);
            }
            if (atoms.Count == 0) continue;

            var compiled = AtomCompiler.Compile(atoms, RuntimeId.Battle, catalogRevision: store.GetCatalogRevision());
            if (compiled.Defs.Count == 0) continue; // every member atom needs the runner -- BindContainers'
                                                     // own existing throw surfaces this, precisely, at bind time

            byContainer[container.ContainerId] = compiled.Defs.Select(d => d.EffectId).ToList();
            defs.AddRange(compiled.Defs);
        }

        return (new DictionaryContainerEffectResolver(byContainer), defs);
    }
}
```

Per-container compilation (not one batched compile over every action's atoms together) is deliberate:
`AtomCompiler.Compile` groups by `EffectiveIcdKey()` globally across whatever it is handed
(`AtomCompiler.cs:76-77`) — compiling two unrelated containers' atoms in one call risks merging their
ICD groups if two containers ever shared an icd key by coincidence. Compiling per-container, exactly
`AtomPushService.PatronAuraAtoms()`'s own granularity, makes that structurally impossible.

### 3. Wiring — `WebMatchService`'s three call sites

Each of `WebMatchService.cs:134,186,315` gains two more named arguments, built once per call from the
same `(resolver, defs)` tuple:

```csharp
var (containerResolver, containerDefs) = ActionContainerEffectResolverFactory.Build(_store);
...
BattleEngine.Resolve(setup, seed, trace, profile: ProfileForWave(setup.WaveId),
    actionCatalog: _store.BuildActionCatalog(RungPolicy.Table),
    containerResolver: containerResolver,
    onEffectHostReady: host =>
    {
        if (host.Bag.Catalog is InMemoryEffectCatalog catalog)
            foreach (var def in containerDefs) catalog.Upsert(def);
    });
```

Identical shape to the one real working precedent, `DistrictAssaultResolver.cs:134-149`
(`containerResolver:` + `onEffectHostReady:` pushing a precomputed def list into the same
`Host.Bag.Catalog` `BindContainers` later reads from), generalized from siege's 4 hardcoded containers
to every real, `RpgStore`-authored action container.

### 4. Golden-safety, argued and then proven

For every existing golden/fixture, `EquippedActionIds` is empty or hand-constructed with no real
container-bearing action (Phase 14's own audit already established this). Consequence, reasoned
through before the test run that must confirm it:

- `onEffectHostReady`'s push adds only **new** `EffectDefDto` keys — `atom.fortitude`/`atom.vitality`
  do not collide with any id in `EffectAtomCatalog.CreateAll()`'s static set (checked directly: neither
  string appears anywhere under the curated `fx-*.json`/`patron-aura.json`/etc. files that catalog is
  generated from). `Upsert`-ing a new key changes nothing about any existing key's def.
- Nothing reads `Host.Bag.Catalog` except `EffectBag.Grant`'s own `_catalog.Get(grant.EffectId)`
  lookup (`EffectBag.cs:242`), which only runs when a grant actually happens. No existing golden actor
  holds a container-bearing action, so `BindContainers` never grants any of these new defs for them —
  the extra catalog entries sit inert.
- The only new cost is the resolver build itself: one more pass over `ListActionIds()` per resolve,
  the same order of work `BuildActionCatalog` already does unconditionally, every real match, today.

**Argued is not proven.** The full suite (`Core.Tests`, `Data.Tests`, `Server.Tests`) must run
green, with zero golden byte output moved, before this module counts as closed — matching every other
module this program has closed this way.

## Filed alongside this module, not fixed by it

- **`battle-runner-path-not-wired`** (new, more severe than this module's own scope): `BattleEngine.Resolve`
  has no execution mechanism for `AtomPath.Runner` atoms at all — confirmed by an empty
  repo-wide grep for `RunnerEntry`/`AtomRunner` usage under `src/FusionRpg.Core/Battle/`. Affects every
  content type whose atoms need a per-hit roll, per-binding state, or predicate tree, not only actions.
  Both real seed atom families (`atom.fortitude`, `atom.vitality`) fall in this bucket, which is *why*
  this module alone cannot make the 3 real imported actions playable — proven directly (§Objective
  above), not inferred. No module id assigned yet; a real design-and-build task on the scale of A18b's
  own original `OnActivate` work, likely larger, since "the Secondary runner (E15)" this repo's own
  atom-path taxonomy names as the Runner path's intended home has never been checked for whether it has
  ANY battle-sim-compatible shape at all (open question, not answered by this investigation).
- ~~**`equip-atom-source-not-wired`**~~ **CLOSED 2026-09-07/08** (Server `EquippedBoundAtoms` /
  `FromEquippedResolver`). Residual: non-`stat.derived` equip kinds; lawn `grant:` labels.

## Tunables

None. This module wires an existing mechanism (`IContainerEffectResolver`, `AtomCompiler.Compile`,
`EffectBag.Catalog.Upsert`) against a new caller; it authors no balance number.

## Numeric types

None new.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionContainerEffectResolver"
dotnet test tests\FusionRpg.Server.Tests --filter "FullyQualifiedName~WebMatch"
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.Server.Tests
```

## Project structure

```
src/FusionRpg.Data/Sqlite/RpgStore.ActionContainerEffects.cs   (new: ListActionContainers)
src/FusionRpg.Data/Sqlite/ActionContainerEffectResolverFactory.cs  (new: Build)
src/FusionRpg.Server/WebMatchService.cs                        (3 call sites gain containerResolver/onEffectHostReady)
tests/FusionRpg.Data.Tests/Actions/ActionContainerEffectResolverFactoryTests.cs   (new)
tests/FusionRpg.Server.Tests/WebMatchContainerActivationTests.cs                 (new)
```

## Testing strategy

- **A hand-authored, Compiled-path-eligible container binds AND fires end to end.** Build a real
  `RpgStore`, author one action whose container holds a fixed-value (`min == max`, no `roll: onApply`)
  `stat.modify` atom, run it through `ActionContainerEffectResolverFactory.Build`, then through a real
  `BattleEngine.Resolve` call shaped like `WebMatchService`'s own (same optional-args pattern) with an
  actor holding that action equipped — assert the grant lands in `Host.Bag` AND (moving past A18a's own
  narrower "grant exists" bar) that the resulting stat change is observable in the battle report. This
  is the module's real acceptance bar, not merely "construction no longer throws."
- **The real, Runner-path-only imported action still fails, precisely.** Import a real
  `atom.fortitude`-based action via the existing `ActionCorpusImporter`, run it through the new
  factory, and assert `EffectIdsFor` for its container returns empty (not "throws inside the
  factory") — proving the SPECIFIC, named reason (§Objective's "does not make every real action
  activate") rather than leaving the earlier, generic exception as the only evidence.
- **Zero effect id collides with the static catalog.** A direct assertion, not just the grep already
  performed while writing this spec — a container's compiled `EffectId`s never appear in
  `EffectAtomCatalog.CreateAll()`'s own key set.
- **Golden-neutral.** Full `Core.Tests`/`Data.Tests`/`Server.Tests` green, zero golden byte output
  moved — every existing fixture's `EquippedActionIds` still resolves exactly as before.
- **Determinism.** Two builds of the resolver against the same `RpgStore` content produce
  byte-identical `EffectDefDto`s (same discipline every other compile-shaped module in this program
  already holds itself to).

## Boundaries

- **Always:** scope `ListActionContainers` to containers an authored `rpg_action` actually references —
  never widen it to "every container in the database" (that pulls in Item/Trait/Patron/WorldBuff
  content this program does not own).
- **Ask first:** wiring `battle-runner-path-not-wired` or `equip-atom-source-not-wired` — both named
  above, both real, neither is this module's to fix without its own spec and its own review.
- **Never:** register the compiler's own auto-emitted `.Grants` (double-grant risk, §Assumption 4);
  cache the resolver across `BattleEngine.Resolve` calls (no revision-invalidation strategy exists yet
  to make a stale cache safe — build it fresh, matching `BuildActionCatalog`'s own accepted cost, until
  a real performance reason says otherwise).

## Success criteria

1. A real, `RpgStore`-backed `IContainerEffectResolver` is wired into all three `WebMatchService`
   `BattleEngine.Resolve` call sites.
2. A held action whose container's atoms are ALL `Compilability.AtomPath.Compiled` now binds and fires
   inside a real `WebMatchService`-shaped battle — proven by an observable effect in the battle report,
   not merely a non-throw.
3. The three real, `atom.fortitude`/`atom.vitality`-based imported actions still cannot activate — but
   the failure is now precisely attributed (their atoms compile to zero defs, a Runner-path gap) rather
   than generically attributed (no resolver supplied at all).
4. `battle-runner-path-not-wired` and `equip-atom-source-not-wired` are named, in `action-plan.md` §5
   and this spec, with the exact evidence this investigation produced — not silently dropped once A24
   itself closes.
5. Zero goldens moved. Full `Core.Tests`/`Data.Tests`/`Server.Tests` green.
