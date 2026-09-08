# Implementation plan: demon progression sources

**Status: partially implemented 2026-09-08.** The typed source grammar, general-species source gate,
dedicated allocation isolation, and replay identity slice are landed. Full provenance diagnostics and
the lawn consumer's atomic terminal settlement remain open; do not mark this plan source-complete yet.

Covers the three approved cross-cutting progression specs under `docs/architecture/demons/`:
[spec-progression-source-contract.md](../docs/architecture/demons/spec-progression-source-contract.md),
[spec-general-empire-fallback.md](../docs/architecture/demons/spec-general-empire-fallback.md), and
[spec-dedicated-progression-isolation.md](../docs/architecture/demons/spec-dedicated-progression-isolation.md).
The lawn-specific consumer is planned in [demon-lawn-deploy-plan.md](demon-lawn-deploy-plan.md), which
depends on this initiative.

## Overview

Make progression source selection explicit at every demon spawn and composition boundary. A gameplay
mechanism declares one typed source: `EmpireGeneral` uses the per-player/per-species empire fallback;
`UniqueSpecimen` uses its own instance progression; `Commander` uses the Commander source. The source is
durable activity provenance, never a `typeId` inference. This plan migrates the existing generic lawn,
unique composition, web battle, and expedition paths without creating a second power ladder.

## Architecture decisions

- The closed source contract is the first dependency. Unknown or missing provenance fails closed and earns
  neither species XP nor species allocation.
- `pvz_activity_facts.source_kind/source_id` carry the typed claim using the `demon.progression.v1`
  grammar; capture origin remains in `plugin_id` and payload. Data owns parsing and validation.
- Generic run-completion awards select only `EmpireGeneral` facts. A unique with the same species/type is
  excluded from the species progression set.
- Unique and Commander composition never merges `EffectiveSpeciesAllocation`. A missing dedicated plan
  yields an explicit empty dedicated input, not a fallback.
- Existing settled ledgers are not rewritten. New evaluation and replay paths are source-gated.
- Level-derived magnitudes remain on the existing `PowerLadder`; balance values stay in versioned tuning
  files and use `long`/checked arithmetic.

## Dependency graph

```
D0 source contract + provenance parser
  ├── D1 general empire fallback
  └── D2 dedicated progression isolation
          └── lawn-deploy-progression (demon-lawn-deploy plan Phase 4)
```

`D0` must land first. `D1` and `D2` can proceed in parallel after the contract is available, but the
generic projector and dedicated composition migration must be verified together before any consumer is
called source-complete. The current implementation passes the focused source and isolation checks;
observable invalid-source diagnostics and the lawn XP phase's atomic Bound settlement remain open.

## Phases

### Phase 0 — source contract

Define and validate the closed source type, canonical `source_kind/source_id` grammar, producer rules,
activity propagation, and missing-source diagnostics. Add a replay-stable lifecycle occurrence identity at
the activity seam where a consumer needs it; the lawn plan owns the Injector capture details.

### Phase 1 — general empire fallback

Change general-demon species XP and allocation consumers to require `EmpireGeneral`. Preserve the
per-player/per-species durable row and automatic allocation. Prove that normal engine-spawned lawn
populations still receive the fallback while unique and Commander facts do not.

### Phase 2 — dedicated isolation

Route unique and Commander composition through their dedicated inputs, remove species fallback from
unique/web battle composition, and remove species XP from expedition/dedicated outcomes. Keep ActorHub as
the sole composer and preserve existing specimen XP and level-unlock behavior.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| A same-type unique is classified as a general demon | High | Persist and parse a typed source claim; no type/side inference; add same-type cross-credit tests |
| Existing activity rows lack the new claim | Medium | Keep settled history intact; fail closed only for new evaluation/replay paths and record diagnostics |
| Removing species allocation changes unique combat unexpectedly | High | Add a dedicated empty-input path, compare ActorHub contributions, and rehydrate only through the existing Cold path |
| Generic completion query still scans by type only | High | Require `EmpireGeneral` in both per-fact and run-completion queries; add a unique-spawn regression |
| New source fields are trusted from an Intent payload | High | Data derives `UniqueSpecimen` from owned row + correlation; Injector may echo but cannot author claims |

## Verification commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter "ProgressionSource|Progression|Aptitude"
dotnet test tests/FusionRpg.Data.Tests --filter "ProgressionSource|Species|UniqueActor|Expedition"
dotnet test tests/FusionRpg.Server.Tests --filter "UniqueActor|WebMatch|Progression"
dotnet test tests/FusionRpg.Guard.Tests
python scripts/audit-overflow.py
python scripts/audit-magic-numbers.py --summary
```

Run the full Core/Data/Server suites at the Phase 2 checkpoint. Any unrelated failures must be named and
reproduced in isolation before this initiative is marked complete.

## Open questions (not pre-work gates)

- Whether the activity table eventually gains a first-class source column instead of the existing
  provenance fields. Default: retain the closed `source_kind/source_id` grammar until query pressure or a
  migration requires an additive column.
- Whether a future dedicated allocation plan is authored for every unique kind. Default: empty dedicated
  input with a visible diagnostic; never inherit empire species points.

## Completion checkpoint

- Every producer and consumer handles all three source variants explicitly.
- General species progression is demonstrably limited to `EmpireGeneral`.
- Unique and Commander composition/rewards demonstrably never use the empire species fallback.
- The dependent lawn progression plan can consume `UniqueSpecimen` claims without a second classifier.
