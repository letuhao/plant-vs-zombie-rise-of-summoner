# Task list: demon progression sources

Plan: [demon-progression-plan.md](demon-progression-plan.md). Specs:
[spec-progression-source-contract.md](../docs/architecture/demons/spec-progression-source-contract.md),
[spec-general-empire-fallback.md](../docs/architecture/demons/spec-general-empire-fallback.md), and
[spec-dedicated-progression-isolation.md](../docs/architecture/demons/spec-dedicated-progression-isolation.md).
The dependent lawn consumer is tracked in [demon-lawn-deploy-todo.md](demon-lawn-deploy-todo.md) as Phase 4.

Dependency order: D0 → (D1 || D2) → checkpoint → dependent lawn progression.

## Build status (2026-09-08)

The first implementation slice is landed in the working tree: Core now has the closed source-value
grammar; death captures carry a per-match lifecycle occurrence; Data returns canonical fact ids on
replay, excludes `source=extra` facts from empire species projection, and records dedicated unique
specimen lawn-kill receipts; unique ActorHub and web squads read `uniqueDemon` allocation only; and
expedition specimen rewards no longer mirror into species rows. Atomic provenance ownership validation,
crash-safe terminal settlement, and the full guard sweep remain open below.

## Phase 0 — `progression-source-contract`

### D0.1 — Typed source model and closed grammar · **M** · 4-5 files — **PARTIAL 2026-09-08**

Implement the three source variants and Data-owned parser/serializer for the versioned
`demon.progression.v1` `source_kind/source_id` grammar. Validate ownership, catalog species, Commander
selection, instance/correlation, and contradictory inputs. Unknown or missing values fail closed with an
observable diagnostic.

- **Acceptance:**
  - [ ] All three variants round-trip without type inference.
  - [ ] A client/Injector-provided instance id cannot impersonate an owned `UniqueSpecimen`.
  - [ ] Unknown, missing, cross-player, or contradictory claims earn neither species progression nor allocation.
- **Verification:** Core parser tests and Data provenance round-trip tests; `dotnet test tests/FusionRpg.Core.Tests
  --filter ProgressionSource`; `dotnet test tests/FusionRpg.Data.Tests --filter ProgressionSource`.
- **Dependencies:** None.
- **Files likely touched:** `src/FusionRpg.Core/Demons/`, `src/FusionRpg.Contracts/`,
  `src/FusionRpg.Data/Sqlite/`, `tests/FusionRpg.Core.Tests/`, `tests/FusionRpg.Data.Tests/`.
- **Estimated scope:** Medium.

### D0.2 — Activity propagation and canonical replay identity · **M** · 4 files — **PARTIAL 2026-09-08**

Carry the typed claim through every activity fact used by source-specific progression. Replace ptr-only
death identity with an Injector-emitted per-match lifecycle occurrence id; a duplicate returns the existing
canonical fact id, while pointer reuse creates a new fact. Keep capture origin separate from progression
source in the existing fields.

- **Acceptance:**
  - [ ] Spawn/death/result facts preserve source claim and occurrence id through Data projection.
  - [ ] Duplicate delivery is the same canonical fact; reused ptr is a distinct fact.
  - [ ] Existing settled ledgers remain unchanged and malformed new provenance is visible/fail-closed.
- **Verification:** activity projection/replay tests plus `dotnet test tests/FusionRpg.Data.Tests --filter Activity`.
- **Dependencies:** D0.1.
- **Files likely touched:** `src/FusionRpg.Core/Activity/PvzActivityKinds.cs`,
  `src/FusionRpg.Data/Sqlite/RpgStore.cs`, `src/FusionRpg.Contracts/EffectDtos.cs`,
  `tests/FusionRpg.Data.Tests/`.
- **Estimated scope:** Medium.

### Checkpoint D0 — source contract ready

- [ ] Source grammar, ownership validation, activity propagation, and replay identity tests pass.
- [ ] `guard-dal.ps1` and `guard-secondary-no-unity.ps1` pass.
- [ ] No downstream consumer still guesses a source from `typeId`.

## Phase 1 — `general-empire-fallback`

### D1.1 — Source-gated species XP projector · **M** · 3-4 files — **PARTIAL 2026-09-08**

Update normal lawn species XP and run-completion selection to consume only parsed `EmpireGeneral` facts.
Preserve the existing per-player/per-species progression row, automatic allocation, run dedupe, and
PowerLadder arithmetic. A `UniqueSpecimen` or `Commander` fact is excluded even when it shares a species.

- **Acceptance:**
  - [ ] General lawn spawns earn the expected species XP once per configured rule.
  - [ ] Unique/Commander spawns with the same type/species never enter the species completion set.
  - [ ] Missing/invalid source earns no species XP and records a diagnostic.
- **Verification:** Data tests for same-species cross-credit, run replay, and per-player isolation;
  `dotnet test tests/FusionRpg.Data.Tests --filter Species`.
- **Dependencies:** D0.2.
- **Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs`,
  `src/FusionRpg.Core/Progression/`, `tests/FusionRpg.Data.Tests/`.
- **Estimated scope:** Medium.

### D1.2 — Source-aware general allocation adapter · **M** · 3-5 files — **PARTIAL 2026-09-08**

Make the general-demon allocation path explicit at composition boundaries. Retain the empire-wide,
per-player/per-species row and automatic primary-stat distribution; reject unique/Commander source input
instead of falling through to it.

- **Acceptance:**
  - [ ] `EmpireGeneral` resolves the existing automatic allocation unchanged.
  - [ ] `UniqueSpecimen` and `Commander` cannot resolve this adapter as a fallback.
  - [ ] The same type id resolves differently when the declared source changes.
- **Verification:** Core allocation tests and Server composition tests; `dotnet test tests/FusionRpg.Server.Tests
  --filter Progression`.
- **Dependencies:** D0.2.
- **Files likely touched:** `src/FusionRpg.Core/Stats/Aptitudes/SpeciesAllocationSource.cs`,
  `src/FusionRpg.Server/`, `tests/FusionRpg.Core.Tests/`, `tests/FusionRpg.Server.Tests/`.
- **Estimated scope:** Medium.

## Phase 2 — `dedicated-progression-isolation`

### D2.1 — Unique ActorHub dedicated input · **M** · 3-5 files — **PARTIAL 2026-09-08**

Route unique composition through the authoritative specimen row and `UniqueDemonAllocation` when a valid
plan exists. Remove `EffectiveSpeciesAllocation` from `UniqueActorHubCompose` and web battle composition;
empty dedicated input is explicit and diagnosed until a plan exists.

- **Acceptance:**
  - [ ] A unique sharing a species with a general demon has no species allocation contribution.
  - [ ] A valid owned unique allocation is consumed through ActorHub only.
  - [ ] Missing dedicated state is empty/diagnosed, never a general fallback.
- **Verification:** Server and squad composition tests; `dotnet test tests/FusionRpg.Server.Tests --filter
  "UniqueActor|WebMatch"`.
- **Dependencies:** D0.2, D1.2.
- **Files likely touched:** `src/FusionRpg.Server/UniqueActorHubCompose.cs`,
  `src/FusionRpg.Server/WebMatchService.cs`, `src/FusionRpg.Core/Stats/Aptitudes/UniqueDemonAllocation.cs`,
  `tests/FusionRpg.Server.Tests/`.
- **Estimated scope:** Medium.

### D2.2 — Dedicated rewards and expedition isolation · **M** · 3-4 files — **PARTIAL 2026-09-08**

Remove species XP from unique/Commander outcomes while retaining specimen XP and existing level-gain action
unlock handling. Keep each reward in its existing transaction and preserve settled history.

- **Acceptance:**
  - [ ] Expedition/web-battle unique rewards update only the specimen progression.
  - [ ] Level-gain unlocks still run through the existing transactional helper.
  - [ ] Replays remain idempotent and never create a species ledger row.
- **Verification:** Data expedition/reward tests and progression-ledger assertions; `dotnet test
  tests/FusionRpg.Data.Tests --filter "Expedition|UniqueActor|Progression"`.
- **Dependencies:** D0.2, D1.1.
- **Files likely touched:** `src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs`,
  `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs`, `tests/FusionRpg.Data.Tests/`.
- **Estimated scope:** Medium.

### D2.3 — Isolation regression sweep · **M** · 4-5 files — **TODO**

Exercise all three source variants across lawn, web battle, and expedition consumers. Confirm ActorHub is the
only composition gate, no private level formula exists, and old unrelated paths remain byte-identical.

- **Acceptance:**
  - [ ] Every production source consumer handles all three variants explicitly.
  - [ ] No unique/Commander path awards species XP or reads `EffectiveSpeciesAllocation`.
  - [ ] General-demon behavior remains unchanged under valid `EmpireGeneral` provenance.
- **Verification:** Core/Data/Server/Guard suites; `python scripts/audit-overflow.py`;
  `python scripts/audit-magic-numbers.py --summary`.
- **Dependencies:** D2.1, D2.2.
- **Files likely touched:** `tests/FusionRpg.Core.Tests/`, `tests/FusionRpg.Data.Tests/`,
  `tests/FusionRpg.Server.Tests/`, `tests/FusionRpg.Guard.Tests/`.
- **Estimated scope:** Medium.

### Checkpoint D2 — progression sources complete

- [ ] D0, D1, and D2 acceptance criteria pass with no source-inference path remaining.
- [ ] Existing settled history is preserved; new source-gated replays are exact-once.
- [ ] [demon-lawn-deploy-todo.md](demon-lawn-deploy-todo.md) Phase 4 is unblocked.
