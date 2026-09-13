# General empire fallback

**Module:** `general-empire-fallback`  
**Status:** approved 2026-09-08; partial implementation landed 2026-09-08
**Depends on:** `progression-source-contract`, species-build allocation transport  
**Decision:** [decisions.md](../decisions.md) — *Creature progression source and spawn ownership (2026-09-08)*

## Objective

Apply the empire's per-player, per-species progression only to an eligible general creature whose gameplay mechanism explicitly declared `EmpireGeneral`. The first consumer is the normal lawn run. This makes the level-3 introduction of general creature species progression understandable without leaking its fallback into unique or Commander paths.

## Scope

This module wires the existing species-build allocation through an explicit general source in the lawn path and gates its XP awards the same way. It covers:

- normal lawn spawn registration and lawn actor composition;
- activity and run-completion species XP attributable to those registrations;
- automatic primary-stat allocation already owned by species build;
- observable rejection of untagged or mismatched general claims.

It does not create a world map, empire-management screen, unique creature progression, Commander progression, manual allocation UI, or new balance values.

## Eligibility rule

An actor receives the empire species allocation only when all of the following are true:

1. Its spawn mechanism supplied `CreatureProgressionSource.EmpireGeneral`.
2. The claim's player owns the run context.
3. The claim's species exists in the catalog and matches the registered general-spawn species.
4. The source is still valid at the composition/award boundary.

No fourth condition may be replaced with `typeId` inference. A source mismatch or missing claim omits the empire allocation and species XP for that event; it must be diagnosed, not coerced into a general source.

## Runtime and persistence behavior

The lawn spawn adapter creates an `EmpireGeneral(playerId, speciesId)` claim when the normal PvZ lawn mechanism registers an eligible general creature. It persists that provenance with subsequent activity facts using the source-contract serializer.

At actor composition, a source-aware adapter selects the current `EffectiveSpeciesAllocation(playerId, speciesId)` only for `EmpireGeneral`. It passes that already-built allocation to the existing ActorHub aptitude input. It must not add a second stat fold, an actor-local scaling curve, or a new contribution source. Existing aptitude output remains attributable through the current ActorHub/Aptitude subsystem.

At progression projection:

- placement and spawn facts award species XP only when their typed source is `EmpireGeneral`;
- run-completion fielded-species awards include only registered `EmpireGeneral` sources;
- duplicate facts preserve the current idempotency behavior;
- the award's player/species key comes from the validated claim, not the event's `typeId` alone.

The source contract is carried from `RpgStore.AppendPvzActivityFact` into both activity-time and match-end progression evaluation. `RpgXpAwardMap` may continue to map eligible event kinds to configured award rules, but it must receive the validated source context before it returns a species award.

## Allocation and power boundaries

The existing `SpeciesAllocation` scope remains `(playerId, speciesId)`. Its existing level, point-budget, and automatic primary-stat behavior are reused. This module does not add a cap, a hard-coded threshold, or a new formula. Any existing or future species magnitude continues to derive from the single `PowerLadder` and current tuning files.

The fallback is global to the player's empire in data scope even though this module's only presentation/runtime consumer is the lawn. A future gameplay mode may reuse it only by declaring `EmpireGeneral` through the source contract and adding its own reviewed spawn adapter.

## Implementation status

The normal lawn projection now emits `creature.progression.v1/general:{speciesId}` claims from the
catalog and gates placement/spawn plus run-completion species XP on that parsed claim. Dedicated
unique and Commander sources are excluded. Focused Core/Data regression suites pass; the remaining
ownership diagnostics and atomic terminal settlement work is tracked by `creature-lawn-deploy` Phase 4.

## Migration targets

- Source-gated composition is now enforced at the unique/server boundaries; general allocation remains
  the existing `EffectiveSpeciesAllocation` path for general consumers.
- Activity provenance now flows through `ApplyRpgProgressionFromActivityUnlocked` and
  `ApplyRunCompletionSpeciesAwardsUnlocked` (`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:20`, `:76`).
- Keep the existing `species-build/spec-allocation-transport.md` payload shape; this module selects when it is legal to consume it, not a new allocation transport.

No historical award rollback is part of this module. New unclassified facts fail closed until their producer is updated.

## Acceptance criteria

- A normal registered lawn general creature receives only its owner's species allocation.
- A valid general activity and match-end fielded entry can award exactly the existing configured species XP once.
- The same species id attached to a `UniqueSpecimen`, `Commander`, unknown, or absent source receives no empire allocation and no species XP.
- Primary-stat automation remains the species-build behavior; no manual allocation UI is introduced.
- World-map UI is not required for correctness.

## Verification when implemented

```powershell
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Server.Tests
dotnet test tests/FusionRpg.Guard.Tests
```

Add integration coverage for: a lawn general with species allocation; a source/species mismatch; an untagged fact; activity-time XP; and match-end XP with mixed general and dedicated sources. Preserve existing allocation-transport contracts and idempotency tests.

## Design-gate checklist

- [x] Read the design gate, creature system map, decisions, species-build allocation transport, data/runtime architecture, ActorHub SSOT, and power-scale SSOT in this session.
- [x] Verified the current species allocation and activity/match-end award paths in `SpeciesAllocationSource.cs` and `RpgStore.Progression.cs`.
- [x] Kept the progression-source behavior lock in `decisions.md` as the governing decision.
- [x] Identified implementation and verification commands.
- [x] Focused species, expedition, activity, and source-parser tests were run.
- [ ] Full guard/conformance sweep remains open and is recorded in `tasks/creature-progression-todo.md`.
