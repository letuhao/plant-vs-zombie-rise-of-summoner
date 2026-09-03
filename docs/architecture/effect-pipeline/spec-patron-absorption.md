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
continuous input.

> **⛔ CORRECTED AND DECIDED 2026-09-03 (owner removed themselves as a gate) — *"Patron's atom keys its
> curve on star/level"* is not buildable as written, and the fix is not a curve.**
>
> **Two facts, both checked:**
>
> - **`CurveInput` has three members — `Level`, `Rarity`, `Tier`
>   (`src/FusionRpg.Core/Effects/Atoms/CurveTable.cs:4-9`). There is no `Star`.**
> - **More decisively, `AuraMilli` takes four inputs, not one.** Its signature is
>   `AuraMilli(DemonRarity rarity, int star, long level, int pTheta, PowerTuning)`
>   (`src/FusionRpg.Core/Demons/Patron/PatronPolicy.cs:53`), and its body is
>   `clamp(rarityBase + perStar×star + level, 0, cap) + K×P(Θ)/1000` (`:56-64`). **A `CurveTable` is a
>   1-D interpolation over a single input.** No single curve — and no `CurveInput` member — can
>   reproduce a four-input expression with a clamp and a `P(Θ)` term inside it.
>
> **The claim was right about the kind vocabulary and wrong about the curve.** No new atom *kind* is
> needed; a *curve* is not what carries this number.
>
> **DECIDED: the absorption moves the binding, not the arithmetic.** `patron.aura` becomes a real
> container on the atom layer — the marker, the element wiring, the Secondary-layer grant, all the
> things `data/seed/containers/patron.json` already stakes out — and **its magnitude continues to come
> from `PatronPolicy.AuraMilli`**, referenced rather than re-expressed.
>
> **Why this and not "add `Star` to `CurveInput` and author four curves":**
>
> 1. **It is the only shape that can satisfy this module's own acceptance gate.** §"The acceptance gate
>    is equality" demands **byte-identical** output across the full `(rarity × star × level × Θ)` grid.
>    The only construction that guarantees that is *the same function*. Four composed curves would have
>    to reproduce a clamp and a ladder term through linear interpolation, and would be proven equal by
>    a sweep rather than by construction — which is how the patron program's SIM results get
>    invalidated by a rounding difference nobody predicted.
> 2. **It is reversible in the direction the program wants.** The magnitude can move onto curves later,
>    once a value spec can compose more than one input and E44 has fitted coefficients. Nothing about
>    this decision blocks that; it just refuses to do it in the module whose deliverable is equality.
> 3. **It costs no vocabulary.** `CurveInput` stays at three, so nothing else in the atom layer has to
>    absorb a fourth member it has no use for. (`effect_curve.input` is stored as **TEXT**
>    (`RpgStore.Curves.cs:26-31`), so appending a member later is safe — it is simply not needed now.)
>
> **What would overturn it:** a value spec that composes several curve reads. That is a real, useful
> thing to build; it is `spec-value-spec-and-curve.md`'s axis, not this module's, and it should not be
> invented under a byte-identity gate.
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
