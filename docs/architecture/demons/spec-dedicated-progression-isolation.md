# Dedicated progression isolation

**Module:** `dedicated-progression-isolation`  
**Status:** approved 2026-09-08; specification only  
**Depends on:** `progression-source-contract`, unique-actor runtime  
**Decision:** [decisions.md](../decisions.md) — *Demon progression source and spawn ownership (2026-09-08)*

## Objective

Ensure a unique demon always resolves through its specimen progression and a Commander contribution always resolves through Commander progression. Neither path may receive the empire general-demon species fallback, even if it has the same species or `typeId` as an eligible lawn general demon.

This is the isolation half of the multi-progression model. It prevents a unique's dedicated deployment/battle mechanism from accidentally inheriting lawn general power or awarding empire species XP.

## Scope

This module updates dedicated-source composition and rewards in the web-battle/expedition consumers and establishes the isolation rule the lawn consumer must obey. Lawn-specific unique XP attribution and participation settlement belong to [demon-lawn-deploy/spec-lawn-deploy-progression.md](../demon-lawn-deploy/spec-lawn-deploy-progression.md). It covers:

- `UniqueSpecimen` composition from the authoritative unique actor row and its own progression inputs;
- `Commander` composition from the existing Commander allocation input;
- removal of species allocation from unique and Commander paths;
- removal of species XP awards from dedicated outcomes and expedition rewards;
- diagnostics for missing dedicated state.

It does not author a new unique-stat plan, redefine unique level curves, add unique unlocks to first-run onboarding, change Commander selection rules, or change general lawn progression.

## Source-specific resolution

| Typed source | Allocation/progression input | Forbidden input |
|---|---|---|
| `UniqueSpecimen(playerId, instanceId)` | The owned `rpg_unique_actors` row, specimen level, and the existing `UniqueDemonAllocation` path when a dedicated allocation plan is available. | `EffectiveSpeciesAllocation`, `SpeciesAllocationSource`, species XP. |
| `Commander(playerId, commanderId)` | Existing Commander allocation and Commander-owned progression. | `EffectiveSpeciesAllocation`, species XP. |

The unique's species remains catalog/identity metadata. It may drive presentation, traits, or a dedicated system's rules, but it is never an implicit route to empire species progression.

`UniqueDemonAllocation` already expresses a specimen-level allocation source but has no production composition caller. This module wires a dedicated allocation only when a valid, owned unique allocation plan is available. Until the owning content system supplies such a plan, the dedicated aptitude input is explicitly empty and diagnosed; it must never fall back to species allocation. The unique's existing specimen level and other dedicated combat inputs still remain authoritative.

## Runtime behavior

The unique deploy handshake remains governed by the unique actor runtime: the server creates/adopts the instance identity, the injector stays SQLite-free, and runtime bindings track admission/lifetime. The unique spawn/deploy adapter declares `UniqueSpecimen` using that persisted instance id.

`UniqueActorHubCompose` and web battle squad composition must use the typed source to choose dedicated inputs. They must no longer create or merge `EffectiveSpeciesAllocation` for a unique actor. The existing Commander contribution may still be composed where the relevant battle/lawn context already applies it, but it is resolved as `Commander`, independently of the unique source.

All selected inputs continue through the existing ActorHub bootstrap. No dedicated code may write actor stats directly, fold a second derived sheet, invent a new `ContributionSourceId`, or introduce a private power function.

## Rewards and persistence

Activity and match outcomes with `UniqueSpecimen` can update the unique's own durable progression only through the existing unique progression rules. They must not produce `RpgActorKinds.Species` awards. [demon-lawn-deploy/spec-lawn-deploy-progression.md](../demon-lawn-deploy/spec-lawn-deploy-progression.md) owns the lawn's attributed-kill and Bound-duration faucets; this module supplies its non-fallback rule. The generic lawn match-completion projector must therefore select fielded species only from facts carrying `EmpireGeneral`; it must exclude every `UniqueSpecimen` fact even when the side and `typeId` map to a known species. The same rule applies to expedition/battle resolution: `RpgStore.Expeditions` currently grants both unique and species XP, so the dedicated-source branch must retain only the valid unique reward.

Existing settled ledgers remain intact; this module does not deduct prior species XP or rewrite activity history. New and replayed dedicated-source outcomes are source-gated. A missing unique row, ownership mismatch, or unknown source is rejected/diagnosed and earns neither unique nor species progression.

## Power and tuning boundaries

Unique and Commander systems may have different progression inputs and deploy mechanisms, but their level-derived power remains part of the repository's one `PowerLadder`. This module adds no balance number, progression ceiling, or tuning file. Any future dedicated allocation plan must be authored by its owning system, be tunable where balance-facing, and use `long` for magnitudes.

## Migration targets

- `src/FusionRpg.Server/UniqueActorHubCompose.cs:46` currently merges `EffectiveSpeciesAllocation` for each unique actor; replace that merge with `UniqueSpecimen` resolution.
- `src/FusionRpg.Server/WebMatchService.cs:647` currently builds unique battle actors with species allocation; route through the source-aware dedicated resolver instead.
- `src/FusionRpg.Data/Sqlite/RpgStore.Expeditions.cs:338` currently awards both unique and species XP; remove the species branch for a dedicated source.
- `src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:76-115` currently infers run-completion species eligibility from `plant.place`/`zombie.spawn` type data. It must instead read the parsed `EmpireGeneral` claim; this prevents a unique lawn deploy from silently levelling the matching empire species.
- `src/FusionRpg.Core/Stats/Aptitudes/UniqueDemonAllocation.cs:29` is the existing dedicated allocation primitive; do not duplicate its budget logic in server or web code.

## Acceptance criteria

- A unique's species id cannot cause it to receive empire species allocation in lawn, deploy, or web battle composition.
- A Commander contribution cannot cause a species allocation or species XP award.
- A unique outcome awards only its eligible dedicated progression and never species XP.
- A unique lawn spawn sharing a `typeId` with a general demon cannot enter the generic species run-completion set.
- Absence of a unique allocation plan results in an explicit empty dedicated allocation, never a general fallback.
- General-demon behavior remains owned by `general-empire-fallback` and is not reimplemented here.

## Verification when implemented

```powershell
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Server.Tests
dotnet test tests/FusionRpg.SquadHarness.Tests
dotnet test tests/FusionRpg.Guard.Tests
```

Add regression tests proving that a unique and a general demon with the same species have different resolution paths; a unique with no plan stays empty rather than inheriting species points; Commander-only composition has no species contribution; and expedition resolution no longer records species XP for unique results.

## Design-gate checklist

- [x] Read the design gate, demon system map, decisions, unique runtime, data/runtime architecture, ActorHub SSOT, and power-scale SSOT in this session.
- [x] Verified the current unique composition, battle composition, expedition reward, and dedicated allocation paths in the files named above.
- [x] Kept the progression-source behavior lock in `decisions.md` as the governing decision.
- [x] Identified implementation and verification commands.
- [ ] No constraint test was run: this is a documentation-only specification.
