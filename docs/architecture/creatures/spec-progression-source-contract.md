# Progression source contract

**Module:** `progression-source-contract`  
**Status:** approved 2026-09-08; partial implementation landed 2026-09-08
**Depends on:** `creature-core`  
**Decision:** [decisions.md](../decisions.md) — *Creature progression source and spawn ownership (2026-09-08)*

## Objective

Make the owner of every creature spawn/deploy mechanism declare the actor's progression source. This prevents an actor's `typeId` from silently selecting the wrong progression system when new gameplay modes are added.

The contract has three closed source variants:

| Source | Meaning | May use empire species progression? |
|---|---|---|
| `EmpireGeneral` | A non-unique, non-Commander general creature. Its mechanism has no dedicated progression. | Yes; this is the fallback. |
| `UniqueSpecimen` | A minted unique creature identified by its specimen instance. | Never. |
| `Commander` | A Commander-derived contribution selected by the player. | Never. |

The word *empire* describes durable global ownership, not a first-run UI requirement. World-map UI may expose it later; the source contract must not wait for that UI.

## Scope and non-goals

This module defines the typed source claim, its validation, and its propagation between spawn/deploy, activity, and composition boundaries. It does not:

- create a new spawn mechanism;
- add a progression curve, stat formula, source id, or database table;
- change species allocation, unique allocation, Commander allocation, or their tuning;
- expose empire progression in the first-run flow or world-map UI.

Each future gameplay mechanism adds a reviewed source producer or an explicit new variant. It cannot send a free-form source string and let a consumer guess from the actor type.

## Contract

The implementation uses a closed value object with the versioned `Kind`, canonical `Id`, and a
stable `ScopeKey`; Data performs player/catalog ownership checks at the boundary. The equivalent
shape is:

```csharp
public abstract record CreatureProgressionSource(string Kind, string Id, string ScopeKey)
{
    public sealed record EmpireGeneralSource(string SpeciesId)
        : CreatureProgressionSource("creature.progression.v1", $"general:{SpeciesId}", SpeciesId);
    public sealed record UniqueSpecimenSource(string InstanceId, string CorrelationId)
        : CreatureProgressionSource("creature.progression.v1", $"unique:{InstanceId}:{CorrelationId}", InstanceId);
    public sealed record CommanderSource(string CommanderId)
        : CreatureProgressionSource("creature.progression.v1", $"commander:{CommanderId}", CommanderId);
}
```

Rules:

1. A spawn/deploy mechanism constructs the claim from its authoritative identity. `typeId`, combat side, and UI route are not classifiers.
2. `EmpireGeneral` requires a catalog-valid species and player. It never carries a specimen id.
3. `UniqueSpecimen` requires an owned `rpg_unique_actors.instance_id`. Its species may be read only through that row; it is not a fallback key.
4. `Commander` requires the player's valid Commander selection. It represents the Commander contribution, not an active lawn fighter.
5. An absent, malformed, contradictory, or unknown claim is ineligible for progression awards and must not resolve empire species allocation. Record an observable validation failure; do not substitute `EmpireGeneral`.

The source claim travels with activity provenance. Reuse the existing `pvz_activity_facts.source_kind` and `source_id` fields rather than add a parallel table, but reserve a closed grammar for this purpose:

| `source_kind` | `source_id` | Validation owner |
|---|---|---|
| `creature.progression.v1` | `general:{speciesId}` | the general-spawn adapter verifies the player and catalog species. |
| `creature.progression.v1` | `unique:{instanceId}:{correlationId}` | Data resolves the correlation to the owned `rpg_unique_actors` row before writing the fact. |
| `creature.progression.v1` | `commander:{commanderId}` | the Commander adapter verifies the player selection. |

`plugin_id = "pvz.capture"` and the event payload retain capture provenance; the source fields name the progression claim. A single parser/serializer at the Data boundary owns this grammar. The Injector may echo an opaque source token supplied with an Intent, but never constructs or validates a claim itself. For a unique, Data derives the canonical source from the persisted actor plus the correlation after the ack, not from a caller-supplied `instanceId` or `source="extra"`. External inputs are parsed and validated before they reach progression code; consumers receive the typed contract only. Any DTO addition is additive and optional for backward-compatible transport, but missing data follows rule 5.

Existing facts do not need a destructive rewrite or reward rollback. They retain their settled ledger effects. The fail-closed rule applies to new evaluation paths and any future replay.

## Producers and consumers

| Boundary | Responsibility |
|---|---|
| Lawn general-spawn adapter | Produces `EmpireGeneral` for an eligible normal lawn spawn. |
| Unique mint/deploy adapter | Produces `UniqueSpecimen` from the persisted instance id. |
| Commander selection/composition adapter | Produces `Commander` from the selected Commander identity. |
| Activity append and progression projection | Persists/parses the claim and evaluates award eligibility from it. Every spawn/death/result fact that can affect a source-specific award retains the same claim and a replay-stable lifecycle occurrence id. |
| ActorHub and battle composition adapters | Select the already-owned allocation input from it; they do not classify an actor. |

`PvzActivityFactDto` already transports `sourceKind` and `sourceId`; `RpgStore.AppendPvzActivityFact` already persists them. The implementation must carry the parsed source into `ApplyRpgProgressionFromActivityUnlocked` and run-completion award evaluation instead of dropping it at that boundary. It must also replace ptr-only death deduplication with an Injector-emitted, per-match monotonic lifecycle occurrence id. On a replay, Data returns the existing canonical fact id; on ptr reuse, the new occurrence gets a new fact. A source-specific evaluator never receives a synthetic `factId = 0` on a duplicate.

## Architecture constraints

- The injector remains Hot and does not access SQLite. Spawn/deploy admission stays in the existing Intent path; durable source provenance is Cold data. See [overlay-control-loops.md](../overlay-control-loops.md) and [unique-actor-runtime.md](../unique-actor-runtime.md).
- Source selection feeds existing `ActorHubBootstrap` allocation inputs. It does not add a parallel derived-stat fold or new `ContributionSourceId`; ActorHub remains the sole composer of actor-derived output.
- Source-specific progression may use different existing inputs, but all level-derived power remains on the single `PowerLadder`. This module adds no numeric magnitude or tunable.
- Source parsing must be a closed typed switch. Unknown values fail closed and are visible in diagnostics; they are not treated as a future-compatible general creature.

## Current-code evidence and migration targets

- `CreatureProgressionSource` now owns the closed grammar (`src/FusionRpg.Core/Creatures/CreatureProgressionSource.cs`).
- `RpgStore.Progression` now requires a parsed `EmpireGeneral` claim before species XP (`src/FusionRpg.Data/Sqlite/RpgStore.Progression.cs:20`, `:135`).
- Unique composition now reads Commander plus `UniqueCreature` allocation (`src/FusionRpg.Server/UniqueActorHubCompose.cs:38-56`; `src/FusionRpg.Server/WebMatchService.cs:649-670`).

Those paths were migrated by the two dependent modules; future producers must continue using this
contract rather than adding another ad-hoc discriminator.

## Acceptance criteria

- A typed claim can be serialized to and parsed from existing activity provenance with no type-based inference.
- Every production source consumer handles all three variants explicitly.
- Missing or invalid provenance cannot grant species XP or empire species allocation.
- A unique source is produced from its owned row plus accepted correlation; `source="extra"`, `typeId`, and a client-provided instance id cannot impersonate it.
- A replayed lifecycle occurrence resolves to the same canonical fact id, while a reused ptr has a distinct occurrence and fact id.
- A new source requires a source-producer change and exhaustive consumer review.

## Verification when implemented

```powershell
dotnet test tests/FusionRpg.Core.Tests
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Guard.Tests
```

Add Core tests for all valid variants, missing/invalid source rejection, and a regression test proving the same `typeId` can never select a source. Add Data tests for round-trip parsing and activity rows with invalid provenance.

## Design-gate checklist

- [x] Read the design gate, creature system map, architecture decisions, data architecture, runtime boundaries, ActorHub SSOT, and power-scale SSOT in this session.
- [x] Verified the current allocation and progression paths in code named above.
- [x] Recorded the behavior lock in `decisions.md` before this specification.
- [x] Identified implementation and verification commands.
- [x] Core source, tuning, Data projection, and Server build checks were run for the implementation slice.
- [ ] Full conformance remains open: atomic terminal settlement, verified killer attribution, and active-match-time capture are tracked in the lawn-deploy plan.
