# Spec: `species-generator`

**Module id:** `species-generator` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 12 of 16
**Model calls:** none, ever. **This module is the entire reason no model ever picks a number.**

## Objective

Expand each enum-only anchor into a **concrete** species: every magnitude, every allocation, every
interval, written to `data/generated/demons/` as committed, diffable rows.

Owner, Q23: *"Seedsmith generate seed, rpg server generate concrete version that use in game, same as
item seed and concrete item principle that we already define."*

## Design

### 1. This is the middle stage of the chain, and it does not exist yet

> **The seed is generator input. It is not rows.** — [item/seed-contract.md](../item/seed-contract.md) §1

```text
data/seed/demons/species/**.json  ->  species-generator  ->  data/generated/demons/**.json
```

**Honest statement: `data/generated/` is absent from the repo.** `seed-contract.md` carries the status
*"Proposed 2026-08-22 … Nothing is authorized to be authored from it yet"*, and no generator or
generated tree exists today. This module builds that stage for the first time. It is not "point the
item generator at demons" — there is no item generator to point.

That also makes this module a **precedent**, not just a feature. The shape chosen here is the shape
the item and action programs will follow, so it is worth getting the boundaries exactly right.

### 2. Where it lives — C#, in the server's own assembly

Not Python. Three reasons, in order of weight:

1. **It must call the shipped arithmetic, not reimplement it.** `PowerLadder.Value(Θ)`,
   `ContentScale.Milli`, `AptitudeReadFunctions.Magnitude(kMilli, share, shareExponentMilli, pTheta)` —
   these are the one power ladder, and a Python transcription of them is a second curve by another
   name, which is the exact defect `ssot-power-scale.md` exists to prevent.
2. `AptitudeReadFunctions.Magnitude` deliberately uses `decimal` as its widening type because a
   `long × long × long` chain overflows spuriously. That precision decision does not survive a port.
3. The runtime is C#, and the owner's framing is *"rpg server generate concrete version"*.

**Seedsmith stops at the seed.** That boundary is the same one `spec-action-seeding.md` §2.1 already
drew for actions: *"The LLM half belongs to the authored corpus, and that corpus is seedsmith's."*

### 3. What is derived, and from what

| Concrete field | Derived from | Function |
|---|---|---|
| `theta` | `threatBand.thetaOffset` + the species' base | additive, small integer |
| `pTheta` | `theta` | `PowerLadder.Value(theta)` — **the one ladder, PS-3** |
| aptitude point allocation | `aptitudePrimary`, `aptitudeSecondary`, `pure` | the existing `PointBudget` / `AptitudeTuning` split |
| `maxHp`, `attack`, `defense`, every channel magnitude | allocation share + `pTheta` | `AptitudeReadFunctions.Magnitude` |
| resource pool sizes | `resourceProfile` × share × `pTheta` | same function, per resource |
| `attackIntervalMs` | `attackTempo`, or the **stated** interval from `power-parse` | table; a stated value wins |
| `rangeCells` | `reach` | table |
| variant count | `rarity`'s count band | `ssot-rarity.md` §3.3 |
| ~~atom pool rolls / tier window~~ | **moved to `species-effects` (module 15)** | it is a property of the *container*, not of the stat block. Emitting it here would be a second source of truth |
| `BaseRarity` (legacy 4-value) | — | **removed**; `rarity-migration` has already widened the enum |

**Q21 is load-bearing here.** HP and damage share one curve — both read `P(Θ)` — and their divergence
comes from *allocation share*, which the aptitude system already owns. There is no second growth rate
anywhere in this table, and adding one later is a change to `ssot-power-scale.md`, not to this module.

### 4. A stated tempo beats a classified one

`power-parse` §3 extracts an interval from text like `伤害：20×6/1.5秒` for a large share of the roster.
Where a stated interval exists, it is used directly and `attackTempo` is recorded as a **label** rather
than a source. Where it does not, `attackTempo` maps through a table. The concrete row records which,
so a later observation can correct it without guesswork.

### 5. Numeric rules, non-negotiable

From `CLAUDE.md`, and they bind harder here than anywhere else in the program because this is where
magnitudes are actually born:

- **`long` for every magnitude.** `float` is banned outright — it stops being integer-exact at index
  232, inside normal play.
- **Widen before multiplying:** `(long)a * b`, never `(long)(a * b)`.
- **Divide by 1000 last, exactly once.** Per-mille intermediates sit 1000× closer to the ceiling.
- **Overflow throws.** No `unchecked`, no clamp. A clamp turns *"your gear stopped mattering"* into a
  bug with no symptom.

`AptitudeReadFunctions.Magnitude` already honours all four and throws rather than wrapping. **Calling
it is how this module complies**; reimplementing any part of it is how it stops complying.

### 6. No caps

`AGENTS.md`: a cap on a magnitude is a progression ceiling until proven otherwise. This module writes
no `Math.Min` on a magnitude, no narrowing cast, no clamped ceiling. The only bounds are the overflow
guards in §5, which **throw**. Structural limits (array sizes, variant counts) are exempt and say so in
a comment.

### 7. Output is committed

`seed-contract.md` §1 consequence 2: *"A generated row nobody can diff is a row nobody can review."*
The generated tree is committed, canonically serialised, and regenerating over unchanged seeds
produces byte-identical files. A `--check` mode gates CI on staleness.

### 8. Adding a field later costs zero seed files

`seed-contract.md` §1 consequence 3. When price, weight, or a new channel arrives, a new formula reads
existing anchor fields and emits a new column. **Every anchor on disk stays valid and untouched.** This
is the property that makes the enum-only anchor worth the trouble, and it should be stated in the
module's own docstring so it is not accidentally traded away.

## Commands

```powershell
dotnet run --project tools/DemonSpeciesGen -- --seed data/seed/demons/species --out data/generated/demons
dotnet run --project tools/DemonSpeciesGen -- --check
dotnet run --project tools/DemonSpeciesGen -- --explain <speciesId>   # every derivation step, shown
dotnet test tests/FusionRpg.Core.Tests --filter SpeciesGenerator
python scripts/audit-overflow.py
```

`--explain` prints the full chain for one species — anchor field, function called, inputs, result. It
is how a balance question gets answered without reading the code.

## Project structure

```text
tools/DemonSpeciesGen/Program.cs                            arguments and report
src/FusionRpg.Core/Demons/Generation/SpeciesExpander.cs     the derivation, testable
src/FusionRpg.Core/Demons/Generation/ConcreteSpecies.cs     the row shape
data/tuning/demon-shape.v1.json                             tempo/reach tables
data/generated/demons/**                                    committed output
tests/FusionRpg.Core.Tests/Demons/SpeciesGeneratorTests.cs
```

## Code style

```csharp
// One ladder, one function. P(Θ) comes from PowerLadder and magnitudes from
// AptitudeReadFunctions.Magnitude - never a local f(level). Three incompatible curves
// shipped at once the last time a subsystem wrote its own.
var pTheta = new PowerLadder(tuning).Value(theta);
```

## Testing strategy

| Test | Asserts |
|---|---|
| `no_private_level_function_exists` | greps for `Math.Pow`/curve shapes outside the ladder call |
| `every_magnitude_is_long` | reflection over the concrete row type; a `float` or `int` magnitude fails |
| `overflow_throws_never_clamps` | a deliberately enormous `Theta` |
| `no_cap_on_any_magnitude` | greps for `Math.Min` on magnitude paths |
| `regenerating_unchanged_seeds_is_byte_identical` | the `--check` gate |
| `stated_interval_beats_classified_tempo` | §4 |
| `hp_and_damage_read_the_same_pTheta` | Q21, mechanically |
| `explain_output_names_every_input` | the audit trail is complete |
| `new_column_does_not_invalidate_an_anchor` | add a field, re-run, seeds untouched |

## Boundaries

**Always:** call the shipped ladder and aptitude functions; `long` for magnitudes; widen before
multiplying; divide by 1000 once, last; commit the output.

**Ask first:** adding a derived field (it widens the concrete contract `species-import` reads);
changing a tempo or reach table.

**Scope note (2026-09-01).** This module produces the **shared** layer only — stats, which are
deterministic and identical for every player. The per-player *effect* roll is `player-materialise`
(module 16), and the container it rolls is `species-effects` (module 15). **Only effects roll; stats
never do.**

**Never:** write a private `f(level)`; use `float` for a magnitude; cap a magnitude; reimplement
`Magnitude` in Python; let a model near this module.

## Success criteria

- [ ] Every magnitude comes from `AptitudeReadFunctions.Magnitude` reading one `P(Θ)`.
- [ ] `scripts/audit-overflow.py` reports no critical finding in this module.
- [ ] No `Math.Min` guards a magnitude anywhere in the derivation.
- [ ] `--check` gates a stale generated tree.
- [ ] `--explain` shows the complete chain for any species.
- [ ] Adding a new derived column requires editing zero seed files, proven by test.
