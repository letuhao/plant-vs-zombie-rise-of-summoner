# Spec: power-vector (E9)

Module **E9** in the [atom effect map](../effect-atom-map.md). Depends on **E4**, **E2**. *(Not E3 — predicates are deliberately not priced; see E3.)*

> **Reads [definitions.md](definitions.md)** — the shared vocabulary pinned after the 2026-08-22 audit. Where this spec and the definitions disagree, **the definitions win**.

## Objective

Give every atom a **price**, in a unit comparable across every kind of effect. From that one price everything else derives: item power is the sum over an item's atoms, actor power is a function over stats and bound items, and an authoring budget is a ceiling on that sum.

## Design (locked on approval)

### A vector, never a scalar, as SSOT

```text
offense · survivability · control · utility · economy
```

Diablo 3 needed three separate aggregates (Damage / Toughness / Recovery) for exactly this reason, and its sheet numbers are *still* wrong because they omit multiplicative sources. Adding a crit-rate atom to a crit-damage atom underprices both; adding an offense atom to a defense atom compares things that do not compare.

The vector is stored. Any scalar is a **derived read** (E10), never truth.

### The cost function

```text
power[category] = coeff(kind, channel, category)
                × normalize(magnitude, referenceScale)
                × conditionality
```

**`coeff`** — a per-kind, per-channel table. Same idea as WoW's per-stat cost multipliers (combat ratings 1.0, Stamina 2/3).

**`normalize`** — the part that cannot be skipped. `+10 hp` is ten hit points; `+10 fire power` is ten **resolver points** at 0.1 sigmoid units. Calibration: `critical-hunter` grants +150 crit-rate points and moves crit from ~7.6% to ~26.9%; the patron aura divides ‰ by ten, so its 150‰ clamp is +15 points. A coefficient table without normalisation prices those alike and is wrong by an order of magnitude. Reference scales come from `effect_curve` (E2), so a value and its price read **one source**.

**`conditionality`** = `(chance/1000) × triggerFrequency × icdFactor × targetCountFactor`. **`conditionality = 1` when the atom declares no trigger** — permanent modifiers (`stat.modify`, `stat.derived`) are not event-driven, and without this short-circuit the 26 passive families price at zero. Otherwise all four factors are defined:

| Factor | Definition |
|---|---|
| `triggerFrequency` | expected fires per battle-minute for that trigger — a **`power_trigger_frequency` table**, data, sweep-fittable, hashed |
| `icdFactor` | `min(1, triggerFrequency⁻¹ / (icd_ms/60000))`; `1` when `icd_ms = 0` **or `triggerFrequency = 0`** (else it divides by zero) **or `triggerFrequency = 0`** (else it divides by zero) |
| `targetCountFactor` | `min(maxTargets, expectedTargets)` from the target spec; `1` for single-target |

`triggerFrequency` is a table rather than a constant deliberately: it is a balance number, the sweep must be able to propose against it, and as a code constant it would move every golden with **no content-hash change** — the one outcome E8 exists to prevent.

For an `OnApply` range the priced magnitude is the **mean**; variance itself has value, and the formula ignores that by design.

### Coefficients live in data

`power_coefficient` rows are the authored values; a sweep writes proposals to `power_coefficient_proposal`; a test reports the gap. That is what makes "hand-authored now, fitted later" mechanically possible rather than aspirational — the sweep never rewrites shipped numbers, and humans decide what ships.

### Computed base plus stored override

`power_json` is computed. `power_override_json` wins when set, and **`power_note` is required** — an override without a reason is a rejection (E4). A test recomputes every atom and reports drift beyond **±25% per category, floor 1 point**.

Not 5%: this module documents that the formula is *knowingly* wrong by 12.5% on multiplicative pairs, so a tight tolerance would fail every crit and element atom on day one. Not 50%: that cannot detect a real mistake. 25% catches order-of-magnitude errors — the class the units trap produces — while tolerating the interaction error the marginal read exists to handle.

The running list of overrides is also the running list of shapes the formula is bad at. That is a feature: it is how we learn where the cost function needs work.

### Actor power — defined here, cached here

```text
actorPower(actor) = price( Σ over atoms on the actor's effect list, grouped by channel )

  not  Σ over atoms of price(atom)
```

**Base stats contribute nothing.** That is what makes E10's "marginal on an empty actor ≈ stored power" true, and it keeps actor power a measure of *what was granted* rather than of the level curve.

`ActorPowerCache` lives in **E9**, not E10 — the spawn recursion below needs it to terminate, and E10 comes later. Memoized on `(actor, catalog_revision, binding-set hash)`.

### Spawn recursion — depth 1, memoized

`5% on death, spawn 2 zombies with 500 hp / 100 atk` is worth `0.05 × 2 × power(that actor)`. So an atom's price calls the **actor** power function — mutually recursive by construction, the same shape as a card game pricing a summon by the body it makes.

**Actor power aggregates channel totals and prices the composition** — it does *not* sum per-atom prices ([definitions.md](definitions.md) §7, closing **D2**). And the spawned **body** is priced from its `hp`/`maxHp`/`atk`, not treated as base stats worth nothing, or `spawn.entity{hp: 5000}` would price at zero (**D3**).

Two rules make it terminate: **depth 1** (a spawned actor's own spawn atoms are priced at depth 1 and then truncated) and **memoized actor power**. Without both, a chain of summoners prices forever.

### The budget is validation, not generation

Rarity R may spend at most N power. That is a **content test that fails over the ceiling** — it never drives which atoms roll. Generation is pool + tier weights (E5). The budget curve reads `effect_curve` like every other scaled value, so whether level or rarity drives it is data, not schema.

### What stays open, honestly

**Multiplicative pairs.** Crit rate × crit damage, the element ring, shield layers all multiply — and the shipped proof is `ElementHub`: two strong slots give `1.25 × 1.25 = 1.5625`, **+562.5‰**, where naive addition says +500‰. A per-atom cost function prices each half in isolation and underprices both.

E9 does not solve this. **E10's marginal read does**, where it matters. Stored atom power stays context-free and approximately right, which is all budgets and display need.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter "FullyQualifiedName~Power"
```

## Structure

```
src/FusionRpg.Core/Effects/Atoms/Power/PowerVector.cs        (new — five categories, integer)
src/FusionRpg.Core/Effects/Atoms/Power/CostFunction.cs       (new — coeff × normalize × conditionality)
src/FusionRpg.Core/Effects/Atoms/Power/CoefficientTable.cs   (new — data-backed)
src/FusionRpg.Core/Effects/Atoms/Power/ActorPowerCache.cs    (new — moved here from E10)
src/FusionRpg.Data/Sqlite/RpgStore.Power.cs                  (new — coefficient, proposal, AND trigger-frequency tables)
tests/FusionRpg.Core.Tests/Atoms/PowerVectorTests.cs
tests/FusionRpg.Core.Tests/Atoms/PowerComputeTests.cs   (drift itself is E14b's)
```

## Testing strategy

| Case | Expect |
|---|---|
| `+10 hp` vs `+10 fire power` | prices differ by the normalisation ratio **within ±25%**, not merely "differ" |
| Same atom, two computations | identical vector — pure function |
| Chance 500‰ vs 1000‰, same payload | **exactly** half, ±1 point of rounding |
| ICD 0 vs ICD 1000 ms on a damage trigger | ratio equals `icdFactor`, computed — not merely "higher" |
| `OnApply` range 100–200 | priced at the mean, 150 |
| Spawn atom | equals `chance × count × power(spawned actor)` at depth 1 |
| Chain of summoners | terminates; depth-1 truncation asserted |
| Override without `power_note` | rejected (E4) |
| Override drifting beyond tolerance | drift test reports it, with the note |
| Sweep proposal written | shipped coefficients unchanged; content hash unchanged (E8) |
| Actor power, repeated reads | **memoized** — one computation per `(actor, catalog_revision, binding-set hash)`. Without it the spawn recursion cannot terminate |
| `power_json` backfill onto E6 instances | every instance gets its power; byte-identity holds against a re-instantiate |
| Actor power, repeated reads | **memoized** — one computation per `(actor, catalog_revision, binding-set hash)`. Without it the spawn recursion cannot terminate |
| `power_json` backfill onto E6 instances | every instance gets its power; byte-identity holds against a re-instantiate |

## Boundaries

**Always:** store the vector; keep the cost function pure and integer; normalise against the curve table; require a note on every override.

**Ask first:** changing a coefficient that ships; adding a power category; raising the drift tolerance.

**Never:** store a scalar as truth; let the budget drive generation; recurse past depth 1; let a sweep overwrite authored coefficients; price a derived channel with a primary-channel coefficient.
