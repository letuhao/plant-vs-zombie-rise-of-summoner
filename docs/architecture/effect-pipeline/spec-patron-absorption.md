# Spec: `patron-absorption`

**Module id:** `patron-absorption` · **Program:** [effect-pipeline](../effect-pipeline-map.md) · **Build order:** 6 of 10
**Depends on:** `instance-producer` (module 4)

## Objective

`PatronSecondaryPlugin` (`PatronSecondaryPlugin.cs:13-29`) grants `EffectId = "fx.patron_aura"` under
`GrantId = "patron:aura"` directly — a hot-path Secondary plugin, not a container. Move it to a real
`patron.*` container bound through `Instantiator`/`InstanceProducer`, on the **same Secondary layer**
`AtomRunner` already occupies (E15's own description: *"the Secondary effect runner"*).

Owner, Q13: absorb it — `patron.*` becomes a real container kind rather than a plugin.

**The ground is already staked.** `data/seed/containers/patron.json` exists and is committed, verified
2026-09-02 byte-for-byte:

```json
{ "id": "patron.aura", "kind": "patron", "poolRolls": 0, "atoms": [],
  "tags": { "marker": "fx.patron_aura" } }
```

An empty `patron.aura` container, right id, right kind, carrying a marker naming the exact `EffectId`
the plugin emits today. This module fills it in; it does not create it.

## Design

### The risk this module carries that `mods-absorption` does not

`PatronPolicy.AuraMilli(rarity, star, level, pTheta, powerTuning)` is a **shipped formula** with a SIM
half already shipped and a LIVE gate still open. It scales **continuously** with star and level; a
container's atoms carry **discrete tiers**. `mods-absorption` migrates stored save data — this module
relocates a **hot-path plugin whose output is under an open LIVE gate**. Different data, different
risk, different proof — that is why the map keeps them as separate modules rather than one.

### The mechanism already exists

E2's `effect_curve` (integer-per-mille interpolated points) is exactly how a value spec reads a
continuous input. Patron's atom keys its curve on star/level rather than taking a flat tier — nothing
new is needed in the atom kind vocabulary, only a container authored against the already-shipped curve
mechanism.

### ⛔ The acceptance gate is equality, not a spot check

Before/after **byte-identical** behaviour across the full `(rarity × star × level × Θ)` grid — not a
sample. If absorption moves a single number, the patron program's existing SIM results are invalidated
and the open LIVE gate gets harder, not easier. This is the module's actual deliverable; everything
else here is plumbing to make that equality provable.

```text
for every (rarity, star, level, Θ) combination the SIM sweep already covers:
    assert PatronPolicy.AuraMilli(...)  ==  atom-resolved patron.aura value at the same inputs
```

### What changes and what does not

| | Before | After |
|---|---|---|
| Emission path | `PatronSecondaryPlugin` computes `AuraMilli` inline, grants `patron:aura` | `patron.aura` container resolves through `Instantiator`, binds via `InstanceProducer` |
| Formula | `PatronPolicy.AuraMilli` | **unchanged** — the container's `effect_curve` is authored to reproduce it exactly |
| `EffectId` | `fx.patron_aura` | unchanged — the container's marker tag already names it |
| SIM results | valid against the plugin | must remain valid against the container, proven by the grid equality test |

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PatronAbsorption"
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~PatronPolicy"   # must still pass unchanged
```

## Project structure

```text
data/seed/containers/patron.json                     edit — fill the staked patron.aura entry with
                                                        real atoms/effect_curve, poolRolls stays 0
                                                        (fixed core only — patron.aura is deterministic
                                                        per (star, level), never rolled)
src/FusionRpg.Core/Effects/Plugins/PatronSecondaryPlugin.cs   edit — grants through InstanceProducer
                                                        instead of computing AuraMilli inline
tests/FusionRpg.Core.Tests/Effects/PatronAbsorptionGridEqualityTests.cs   new — the ⛔ acceptance gate
```

## Code style

```csharp
// patron.aura is staked, not greenfield (data/seed/containers/patron.json, committed before this
// module). The formula does not move - it is reproduced via effect_curve so the SIM half's existing
// results stay valid. Verify with the grid equality test before touching PatronSecondaryPlugin.cs.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `patron_aura_container_reproduces_auramillis_full_grid` | ⛔ the acceptance gate — every (rarity, star, level, Θ) combination the SIM sweep covers, byte-identical |
| `patron_aura_stays_fixed_core_never_rolled` | `poolRolls` stays 0 — deterministic per input, not a loot roll |
| `effectid_and_grantid_are_unchanged_after_absorption` | downstream consumers see no difference |
| `sim_results_remain_valid_against_the_container` | the existing SIM test suite, re-run against the new path, not just the old |
| `patronsecondaryplugin_no_longer_computes_auramilli_inline` | the plugin now only produces/binds |

## Boundaries

**Always:** prove the grid equality before merging; keep `patron.aura` fixed-core (no roll).

**Ask first:** any change to `PatronPolicy.AuraMilli` itself while this module is in flight — the
formula must hold still for the equality proof to mean anything.

**Never:** approximate the curve; ship this with a spot-check instead of the full grid; let the LIVE
gate's status change as a side effect of this module (it stays exactly as open or closed as it was).

## Success criteria

- [ ] `patron.aura` resolves through `Instantiator`/`InstanceProducer`, not the plugin's inline formula.
- [ ] The full `(rarity × star × level × Θ)` grid is byte-identical before and after, proven by test.
- [ ] Every existing patron SIM test still passes.
- [ ] The open LIVE gate's status is unchanged by this module — no new risk introduced to it.
