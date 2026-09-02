# Spec: `instance-producer`

**Module id:** `instance-producer` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 4 of 10 · ⭐ **the payoff**
**Depends on:** `resolution-order` (module 2), `affix-library` (module 3)

## Objective

**The missing call.** Four modules ship, tested, with zero production callers, verified 2026-09-02:

| Symbol | File:line | Production callers |
|---|---|---|
| `Instantiator.TryInstantiate` | `Instantiator.cs:92` | zero — five test files only |
| `RpgStore.SaveInstance` | `RpgStore.AtomInstances.cs:113` | zero — four test files only |
| `RpgStore.Bind` | `RpgStore.AtomInstances.cs:205` | (this module's own new caller) |
| `ActionSeeder.Generate` | `ActionSeeder.cs:32` | zero — one test file only |

`ResolveBindings` (`RpgStore.AtomInstances.cs:286`) is not hardcoded to return empty — it genuinely
reads `ListBindings(owner)` and iterates real rows. It returns empty **because nothing has ever written
a row**, not because of a short-circuit. This module writes the first one.

Write the producer function: given a container id and a real `OwnerScope`, resolve it (module 2), save
the instance, bind it to the owner. One function, one real caller, and the four-module chain that was
"proven correct end to end by tests, unreachable end to end in production" becomes reachable.

## Design

### The function

```csharp
public static class InstanceProducer
{
    // Resolve -> SaveInstance -> Bind, in one call. Returns the rejection from whichever step failed
    // first, or Ok with the binding id. Never partially commits: SaveInstance and Bind both happen
    // inside RpgStore's own transaction boundary, or neither does.
    public static AtomRejection Produce(
        RpgStore store, ContainerRow container, Func<string, AtomRow?> lookupAtom,
        long rollSeed, int thetaContent, PowerTuning tuning, OwnerScope owner,
        string slot, int priority, string source,
        out string? bindingId, VariantShift? variant = null, long catalogRevision = 0);
}
```

This is deliberately thin — every real decision (resolution order, streams, variant shifts) already
lives in module 2; every real storage decision (`SaveInstance`, `Bind`, their transaction shape)
already lives in E6's shipped code. **This module's only new logic is the wiring between them**, which
is exactly why it is small relative to what it unblocks (`effect-pipeline-map.md` §1).

### `PowerJson` stays null — E9 backfills

`Instantiator.TryInstantiate` already leaves `InstanceRow.PowerJson` nullable and does not compute
power on the instantiation path (verified: no `PowerReads` call anywhere in `Instantiator.cs`). This
module does not change that. `effect-pipeline-ideal.md` A3's own warning stands: `PowerReads.
IntegerFifthRoot` is a binary search over `BigInteger`, "needed because five categories near 6000 each
already overflow Int64" — correctly off the instantiation path today, and this module must keep it
that way. Power backfills later (E9), not here.

### Reproducibility, proven not assumed

`definitions.md:246` (the reproducibility law, cited by line number verified 2026-09-02 — it moved from
`:170` when §4a was inserted above it): *"Same `(container_id, catalog_revision, roll_seed) ⇒
identical `effect_instance_atom` rows."* This module adds `variant` to that tuple (module 2's own
addition) and must prove the SAME thing holds with it: `(container_id, catalog_revision, roll_seed,
variant)` reproduces identically. A test that only proves the pre-variant tuple is not sufficient
coverage for what this module ships.

### The mixed-source invariant this module must not violate

`effect-pipeline-map.md` §1 names four paths that can reach an actor's effect list, with a disposition
per path (1: this module, new · 2: `mods_json`, absorbed by module 5 · 3: patron plugin, absorbed by
module 6 · 4: `AuraContentCatalog`, deferred by its owning program). **This module stands up path 1
beside a working path 2 that still exists until module 5 runs** — `rpg_unique_stat_mods.mods_json`
still builds real grant blobs from equipped slots today. `instance-producer` must not bind an
equipped-item effect through path 1 while path 2 is still live for the same slot, or an actor could
receive the same source twice. **This module's own test fixture stays away from equipped-item
ownership entirely** (use a `species-passive` or `trait` container) — the equipped-slot case is
module 5's, deliberately sequenced after this proof, never inside it.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~InstanceProducer"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~AtomEndToEnd"   # T3.7, see below
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
```

## Project structure

```text
src/FusionRpg.Core/Effects/Atoms/InstanceProducer.cs       new — the Produce() function above
tests/FusionRpg.Core.Tests/Atoms/InstanceProducerTests.cs  new
tests/FusionRpg.Core.Tests/Atoms/AtomEndToEndTests.cs      new — T3.7, ⭐ THE PROOF: fixture container
                                                              -> Produce() -> ResolveBindings returns
                                                              non-empty -> AtomPushService compiles
                                                              -> AtomRunner receives an entry. First
                                                              time this path runs in production shape.
data/seed/effects/_fixture/*.json                          new — the one fixture container T3.7 rolls,
                                                              a species-passive or trait, never an item
```

## Code style

```csharp
// Every real decision already lives one layer down (Resolver, SaveInstance, Bind). This function is
// the wiring, deliberately thin - the effect-atom program's own four modules did the hard part.
public static AtomRejection Produce(...)
```

## Testing strategy

| Test | Asserts |
|---|---|
| `produce_writes_an_instance_and_a_binding_for_a_real_owner` | the whole point |
| `resolvebindings_returns_non_empty_after_produce` | the exact sentence `effect-atom-map.md:213` names as inert, proven no longer inert |
| `atompushservice_compiles_after_produce` | E15's own consumer, reached for the first time |
| `atomrunner_receives_an_entry` | ⭐ T3.7's own acceptance line, "the first time in the repo's history this path runs in production shape" |
| `same_container_revision_seed_variant_reproduces_identically` | the extended reproducibility law |
| `powerjson_stays_null_after_produce` | A3's own guard — power is backfilled, never computed here |
| `producing_for_an_equipped_item_slot_is_not_this_modules_test_surface` | a scope-discipline test — the fixture container is never `item`-kind bound to an equipped slot |
| `partial_failure_never_leaves_an_orphaned_instance_with_no_binding` | the transaction boundary holds |

## Boundaries

**Always:** call `Resolver.Resolve` (module 2), never reimplement resolution; leave `PowerJson` null;
prove `ResolveBindings` non-empty as the module's own acceptance test, not by inspection.

**Ask first:** widening `Produce`'s signature to cover the equipped-item case before module 5 lands —
that is exactly the "two risks in one change" the map explicitly sequences apart.

**Never:** compute power on this path (A3); bind an equipped-slot effect while `mods_json` is still the
live path for it; let this module's fixture double as an item-container test.

## Success criteria

- [ ] `ResolveBindings` returns non-empty for a real owner, proven by test, not by inspection.
- [ ] `AtomPushService` compiles the produced instance and `AtomRunner` receives an entry — E6/E7/E15/E19
      are no longer inert.
- [ ] The reproducibility law holds across the full `(container_id, catalog_revision, roll_seed,
      variant)` tuple.
- [ ] `PowerJson` is null on every produced instance.
- [ ] The fixture used for the proof is never an `item`-kind container bound to an equipped slot.
