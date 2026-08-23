# Spec: structure-substrate (wave 4)

**Status:** **Sealed 2026-08-23** — owner-approved, per the same authorization as `loam-legions` and
`loam-ai`. Module id `structure-substrate` in the [loam capability map](../loam-map.md). Depends on
`loam-turn` (shipped). **Design source:**
[empire-economy-ideal.md](../empire-economy-ideal.md) §8.2, §8.8, §8.10 · [loam-map.md](../loam-map.md)
§1 A10 (why this waited).

## Objective

Give the map a generic structure model — a slot can hold a building, and a catalog describes what
that building costs, what it changes about the slot's upkeep, and what it yields — **designed now that
four waves of loam have actually been played**, per A10's own argument for why this module was moved
from first to fourth: *"designed today it is a guess about what a generator must express; designed
after wave 4 it is a description of something we have played."*

Success looks like: a slot can carry exactly one structure, validation refuses a structure on a slot
kind it was not built for, and the catalog's shape needs no schema change when `loam-structures` adds
its first two real rows (Well, Waystation) — the plumbing should already fit content it has not seen
yet, the same way `SlotTypeCatalog` already did for `Rootbed` back in `loam-model`.

**This module is deliberately content-light.** It builds the mechanism; `loam-structures` (next in the
build order) builds the first two things that use it. A generic model validated against a single
placeholder row and a real one is a better test of the plumbing than a model built to fit two
structures it already knows about.

## Design

### New state

- `WorldSlot.StructureId` (`string?`) — a new field on the shipped record (`WorldState.cs:76-90`; no
  collision, confirmed clean). Null means the slot carries no structure, which is every slot today.
- `StructureCatalog` — a new catalog, mirroring `SlotTypeCatalog`'s own shape exactly rather than
  inventing a different one: dictionary-backed, eager `Validate()` at static init, `IsKnown`/`Get`
  accessors (`SlotTypeCatalog.cs:32-116` is the template).
- `StructureKind` — a new enum. **One value for this wave: `LoamSource`.** Ideal §8.10's own framing —
  *"a category, not a building... a sector is habitable iff it holds a working one"* — needs exactly
  one kind to express Well and Waystation both; more kinds belong to whatever `sector-development`
  eventually adds, not invented here on spec.
- `StructureDef`: `StructureId`, `Name`, `Kind` (`StructureKind`), `RequiredSlotKind` (`SlotKind` — what
  the slot beneath it must already be), `CostMilli` (upfront, long), `YieldMultiplierMilli` (per-mille,
  default `1000` = unchanged, following the `LaneTypeDef.CostMultiplierMilli` / `WorldFaction.
  UpkeepHandicapMilli` per-mille convention already used everywhere else in this program rather than a
  raw multiplier float).

**Why a multiplier, not a flat yield field**: `loam-structures`' two planned rows need opposite shapes
— a Well multiplies whatever its Rootbed slot already yields; a Waystation sits on a Seat, which
yields nothing on its own, so any multiplier applied to zero is still zero. One field expresses both
without a second "flat yield" column that would only ever be populated by a future structure this
module has not seen. If `sector-development` later needs flat, structure-only yield independent of the
underlying slot, that is a new field added when there is a real row to test it against — the same
discipline A10 is arguing for, applied one level down.

### Validation — Rule 14

`WorldValidation.cs` currently runs 13 rules (`Rule1`–`Rule13`, `WorldValidation.cs:22-39`; the file's
own header comment says "seven creation rules" and is already stale — worth a one-line fix in the same
change, not a new task). **Rule14StructureSlotKindMatches**, appended after `Rule13`: for every slot
with a non-null `StructureId`, resolve the structure via `StructureCatalog.Get`, and require the slot's
own `SlotTypeId` to resolve (via `SlotTypeCatalog`) to a `SlotKind` equal to that structure's
`RequiredSlotKind`. Same shape as `Rule6SlotShape` (`WorldValidation.cs:194-218`), which already
validates a sector-type/slot-type pairing the identical way — this rule pairs slot-type and structure
instead of sector-type and slot-type, reusing the pattern rather than a new validation idiom.

### DTO

`WorldSlotDto` (`src/FusionRpg.Contracts/WorldDtos.cs:25-34`) gains `StructureId` (`string?`), mirroring
`WorldSlot` exactly — no owner-gating question here, since a structure sitting in a slot is exactly as
visible as the slot itself already is (governed by the same intel/fog rule the slot's other fields
already follow).

### What this module does not decide

No Well, no Waystation, no construction time, no range rule, no habitability-gate wiring into
`LoamProduction`/`Habitability` — all four are `loam-structures`' job, which depends on this module
existing first. This module ships a catalog that validates correctly against one placeholder row
(proving the mechanism) and is ready for `loam-structures` to add its first real ones without a schema
change.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests --filter FullyQualifiedName~Structure
dotnet test tests\FusionRpg.Core.Tests
dotnet test tests\FusionRpg.Data.Tests
dotnet test tests\FusionRpg.E2E.Tests --filter FullyQualifiedName~World
dotnet test tests\FusionRpg.Guard.Tests
```

## Project structure (proposed)

```
src/FusionRpg.Core/World/WorldState.cs                → WorldSlot.StructureId
src/FusionRpg.Core/World/WorldCanonical.cs             → the field into the hash — part of the batched post-gate golden move, see spec-loam-texture.md
src/FusionRpg.Core/World/StructureCatalog.cs (new)     → StructureKind, StructureDef, the catalog
src/FusionRpg.Core/World/WorldValidation.cs            → Rule14StructureSlotKindMatches; header comment fixed
src/FusionRpg.Contracts/WorldDtos.cs                   → WorldSlotDto.StructureId
tests/FusionRpg.Core.Tests/World/StructureCatalogTests.cs (new)
tests/FusionRpg.Core.Tests/World/WorldValidationTests.cs → Rule14 cases
docs/architecture/decisions.md                          → the batched golden-move row (spec-loam-texture.md owns this note)
```

## Code style

Same as `SlotTypeCatalog`: a static, eagerly-validated dictionary, no runtime mutation, `IsKnown`
before `Get` at every call site that cannot guarantee the id already validated.

## Testing strategy

- **The catalog validates itself** at static init the same way `SlotTypeCatalog` does — a duplicate id,
  a missing name, or a kind/required-slot-kind mismatch in the seed data is a startup failure, not a
  runtime surprise.
- **Rule14 fires and declines**: a structure on a slot of the wrong kind is refused; the same structure
  on the right kind is accepted.
- **DTO round-trip**: `StructureId` reaches the wire unchanged, null when absent, for both the slot and
  its owner-agnostic visibility (matches the slot's own existing fog treatment, not a new rule).
- **Golden move**: this field lands in the **one** batched golden move across all five post-gate specs
  (`spec-loam-texture.md` owns this decision, added after an adversarial audit caught each spec
  independently reopening a budget `tasks/loam-plan.md` had explicitly closed at two) — not a move of
  its own.

## Boundaries

- **Always:** the catalog's eager-validation pattern; per-mille for any multiplier, matching every
  other multiplier in this program; `Rule14` follows `Rule6`'s existing shape.
- **Ask first:** adding a second `StructureKind` before `loam-structures` or a later module has a real
  structure that needs one — this module ships exactly the one kind current design calls for.
- **Never:** a flat yield field invented for a structure this module has not seen; runtime mutation of
  the catalog; a second validation idiom when `Rule6`'s already fits.

## Success criteria

1. `WorldSlot.StructureId` and `StructureCatalog` exist; the catalog validates at static init.
2. `Rule14` fires on a slot-kind mismatch and accepts a correct pairing.
3. `WorldSlotDto.StructureId` round-trips.
4. The new field ships in the one batched post-gate golden move, not a separate move.
5. All four guard scripts green.

## Resolved (2026-08-23)

- **One `StructureKind` (`LoamSource`) for this wave** — matches what `loam-structures` actually needs;
  a second kind is added when a module has a real reason for one, not spec'd speculatively here.
- **`YieldMultiplierMilli`, not a flat yield field** — fits both planned structures (Well multiplies an
  existing yield, Waystation multiplies a zero) without a column only one future row would ever use.
- **This module ships plumbing, not content** — Well and Waystation, the range rule, and construction
  time all belong to `loam-structures`, kept out of this spec on purpose so the mechanism gets tested
  on its own terms before real structures are layered onto it.

**Resolved after an adversarial audit (2026-08-23)**: no findings against this spec's own content — the
catalog mechanics, `Rule14`, and DTO shape all checked out clean against `SlotTypeCatalog.cs` and
`WorldValidation.cs`'s existing `Rule6` pattern. The one change here is the golden-move count, corrected
per the cross-spec finding recorded in `spec-loam-texture.md`.
