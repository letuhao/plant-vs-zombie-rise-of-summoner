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

## Amendment 2026-09-06 — the mechanism for "referenced, not re-expressed," and a second real gap this
## spec's own text did not yet name

Owner direction, `seed-to-concrete` T6.2: retire `PatronSecondaryPlugin`'s bespoke combat-math path
**if and only if** battle-engine + the atom/container system genuinely already cover what it does —
architectural consistency, not a rewrite for its own sake (Patron predates both; the spec's own §"the
mechanism already exists" text is what makes this achievable without inventing new runtime).

### What is now confirmed, read directly rather than assumed

- **A real, PRODUCTION precedent for "grant an `EffectId`, no overlay, let the compiled def's own
  `ModifyDerivedStat` action rows carry the magnitude" already exists and reaches a live lawn entity**:
  `BattlefieldOwnSideReactor.BuildGrant` (`src/FusionRpg.Core/Battle/BattlefieldOwnSideReactor.cs`) —
  named directly in `GrantedDerivedAtomReader`'s own doc comment as *"the only production grant path"*
  that *"makes a real aura reach a lawn entity."* `PatronSecondaryPlugin`'s own current grant
  (`EffectId`, no overlay, `OwnerKey: Match`) already matches this shape exactly — the delivery half
  this spec already designed is not hypothetical, it is the same shape a real feature already proves
  live. `GrantedDerivedAtomReader`'s own "scope grammar" explicitly includes `match` as a first-class
  scope alongside `plant:{typeId}`/`entity:{ptr}` — Patron's own match-wide grant is a supported shape,
  not an edge case to special-case around.
- **`fx.patron_aura`'s own compiled def has zero `ModifyDerivedStat` action rows today** — confirmed
  (not assumed) by reading `data/seed/containers/patron.json` directly: `"atoms": []`. This — not a
  missing delivery mechanism — is the entire remaining gap on the *delivery* side.
- **`PowerLadder`/`ClampedLevelScale` (`ValueSpec`, built 2026-09-02, the same day as this file's own
  correction) are real, tested, reusable infrastructure — and this spec's own "referenced, not
  re-expressed" decision, dated one day later (2026-09-03), still stands and is NOT overturned by their
  existence.** They were built as general-purpose compile-time-bake infrastructure (real value for
  future magnitudes that need it), not specifically to reopen this module's own already-decided
  question. Composing them to *reproduce* `AuraMilli`'s formula as two independent atom expressions
  would satisfy the letter of "an atom carries the number" while reintroducing exactly the risk this
  file's own §"why this and not add Star to CurveInput" already rejected: two independently-expressed
  formulas are only *provably* equal by a sweep, never by construction, and drift silently the moment
  either copy is tuned alone. This amendment does not reopen that decision — it names the mechanism the
  original text left unspecified for actually *referencing* the function.

### The real remaining gap #1: how "referenced, not re-expressed" is mechanically expressed

No `ValueSpec` marker today can say "the value is whatever this named external function currently
returns" — `powerLadder`/`clampedLevelScale` both *compute* a number at compile time from owner
context; neither *delegates* to an arbitrary named formula. **Proposed: a third, equally closed
marker**, `{"externalRef": "patron.auraMilli"}` — a literal, closed, reviewed string id (never a
free-form expression or a class/method name reflected at runtime, matching every other closed-marker
precedent in this file's own vocabulary), resolved by `AtomCompiler.ResolvedParams` via a small,
explicit `Dictionary<string, Func<ExternalRefContext, long>>` registry (one entry, `"patron.auraMilli"
→ ctx => PatronPolicy.AuraMilli(ctx.Rarity, ctx.Star, ctx.Level, ctx.PTheta, ctx.PowerTuning)`) —
literally calling the same function, so the byte-identity gate is satisfied by construction, matching
this file's own already-decided reasoning exactly, now made buildable. Compiling an `externalRef` atom
whose id is not in the registry throws, naming the unknown ref — never silently prices at zero
(matching `powerLadder`'s own established "missing context throws" rule).

### The real remaining gap #2: rarity/star/level/Θ are PER-PLAYER, not per-authored-content

Not named anywhere in this file's original text, found this session while scoping the actual
implementation: `AuraMilli`'s four inputs (`rarity`, `star`, `level`, `pTheta`) all belong to
**whichever specific demon a given player has currently designated as patron** — genuinely different
per player, and changing over that demon's own lifetime (promotion changes `star`; leveling changes
`level`). `AtomCompiler.Compile`'s own `ownerLevel`/`ownerTheta` parameters
(`src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs:27-35`) are each a single value for the WHOLE
compile call — they answer "what is the level/Θ of the ONE owner this push is for," which is exactly
right for a species' own base stats or one demon's own equipment, but Patron's aura is resolved
**inside a push that may also carry the player's own, unrelated level/Θ** (the same multi-owner union
`AtomPushService.OwnersForPlayer` now builds, `seed-to-concrete` T6.1) — the patron demon's own
rarity/star/level/Θ are not the same numbers as the player's.

**Corrected the same day, before any code was written** — freezing these four inputs onto a
per-player `effect_binding`/atom row (the original plan here) was found wrong by tracing the data
flow: a per-player-varying VALUE written onto the SHARED atom/container catalog bumps the GLOBAL
`catalog_revision` on every designation or level-up, forcing every OTHER connected player to needlessly
re-sync for a change that touches only one player. `mods-absorption` (T6.1) avoids this for equipment
because the ATOM's own value is genuinely SHARED (every copy of an item grants the same flat bonus) —
Patron's aura has no such shared value; it is different for every player's own patron, so there is
nothing to freeze onto a catalog row that stays valid to reuse across a player-agnostic push.

**Decided instead: resolve `externalRef` via a CALLBACK, computed fresh at push time, never written
anywhere.** `AtomCompiler.Compile` gains one new parameter, `externalRefs: Func<string, long>? = null`
— the exact same shape `curves: Func<string, CurveTable?>` already has, keeping `AtomCompiler` itself
exactly as pure and domain-agnostic as it is today (it only ever invokes a callback it was handed; it
never imports `PatronPolicy` or anything Patron-specific). `AtomPushService.Build` — which already has
`_store` access and already knows which player a push is for — supplies this callback, backed by a
LIVE lookup: that player's current `rpg_patron` row → the designated specimen's own current
rarity/star/level → `PatronPolicy.AuraMilli` (unchanged, called directly) → the real number, computed
fresh on every single push, always current, never stale, and never persisted anywhere the shared
catalog's own revision could see it. No new DB write path, no freeze/refresh/withdraw lifecycle to get
right, no per-player row at all.

### Revised project structure

```text
data/seed/containers/patron.json                     edit — patron.aura's atoms carry ONE
                                                        stat.derived atom per element slot, ValueSpec
                                                        {"externalRef": "patron.auraMilli"} — the
                                                        formula is referenced, never re-expressed
src/FusionRpg.Core/Effects/Atoms/ValueSpec.cs         edit — the new ExternalRef field + Validate()
src/FusionRpg.Core/Effects/Atoms/AtomCompiler.cs      edit — Compile's new externalRefs callback
                                                        parameter; ResolvedParams invokes it
src/FusionRpg.Server/AtomPushService.cs               edit — supplies the callback, backed by a live
                                                        RpgStore.Patron.cs lookup for the pushed player
src/FusionRpg.Core/Effects/Plugins/PatronSecondaryPlugin.cs   edit — grants through InstanceProducer
                                                        instead of computing AuraMilli inline
tests/FusionRpg.Core.Tests/Atoms/ExternalRefMagnitudeTests.cs   new — the marker mechanism, mirroring
                                                        PowerLadderMagnitudeTests' own shape
tests/FusionRpg.Core.Tests/Effects/PatronAbsorptionGridEqualityTests.cs   new — the ⛔ acceptance gate
```

### Revised testing strategy (additive to the table above)

| Test | Asserts |
|---|---|
| `an_externalRef_atom_resolves_via_the_supplied_callback` | `externalRef` calls whatever callback `Compile` was handed, not a re-derivation |
| `an_externalRef_atom_with_no_callback_supplied_throws_naming_the_ref_id` | closed vocabulary, never silently zero |
| `atompushservice_supplies_the_real_patron_auraMilli_for_a_player_with_a_patron_set` | the live lookup, real `_store` round trip, calls the real unchanged `PatronPolicy.AuraMilli` |
| `a_player_with_no_patron_set_contributes_nothing_rather_than_throwing` | the common case (no patron) is not an error |
| `two_pushes_after_a_promotion_reflect_the_new_star_immediately` | fresh-every-push means no staleness and no refresh step is needed — proves the design's own main claim |
