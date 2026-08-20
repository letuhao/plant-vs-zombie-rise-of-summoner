# Spec: demon-core (V1 wave 2)

Module id `demon-core` in the [demon system map](../demon-system-map.md). Depends on `element-extension`.

## Objective

Give every demon a durable identity richer than a UniqueActor row: species, rarity, variant, element typing, trait slots, origin, and a per-player Codex of discoveries. This is the substrate every later module (summoning, capture, contracts, fusion) reads and writes.

Success looks like: a specimen can be minted with a full demon profile in one atomic operation, the Codex tracks what the player has seen vs. discovered, and all of it survives restarts and honors the DAL boundary.

## Design

**A demon = a UniqueActor specimen + a demon profile.** The UniqueActor FSM, equipment, XP, and deploy path are untouched; demon-ness is a profile keyed by the same `instanceId`.

### Content: species catalog (code-authored, like the status catalog)

`DemonSpeciesCatalog` bootstrap in Core (follows `StatusCatalogBootstrap` precedent): `speciesId` (kebab-case, stable), display name, linked game side+`typeId` (the PvZ type whose body/portrait it wears), element primary/secondary (extended roster), base rarity (`common|rare|epic|legendary`), allowed variants, trait pool (trait ids + weights), `deployMode` (`plant-avatar` | `hypno-ally` — per resolved decision 1; v1 stores it, deploy modules consume it later). Unknown species/trait/element ids reject — catalog discipline.

Traits v1 = stored identity only (`traitId` list per specimen, validated against a `DemonTraitCatalog` naming effect-grant templates). Wiring traits into live Effect grants happens in the deploy-facing modules.

### Data (all DDL in `FusionRpg.Data`, `EnsureColumn` migration style)

| Table | Key | Columns (essence) |
|---|---|---|
| `rpg_demon_profiles` | `instance_id` (FK → `rpg_unique_actors`) | `species_id`, `rarity`, `variant`, `element_primary`, `element_secondary`, `traits_json`, `origin` (`summon\|capture\|fusion\|seed`), `created_utc`, `revision` |
| `rpg_demon_codex` | `(player_id, species_id)` | `state` (`seen\|discovered`), `first_utc`, `updated_utc` |

Specimen SSOT stays `rpg_unique_actors` + profile; Codex is per-player discovery state; the species catalog is code, not DB. Mint = create UniqueActor (Roster) + profile + codex upsert in **one** store call.

### Server

`GET /api/demons/catalog` (species + traits, from bootstrap) · `GET /api/demons/{playerId}` (roster join: actor + profile) · `GET /api/demons/{playerId}/codex` · profile fields folded into existing `/api/unique/actors/{id}` reads. SignalR `DemonsUpdated` invalidation.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests; dotnet test tests\FusionRpg.Data.Tests
.\scripts\guard-dal.ps1                       # all new SQL inside FusionRpg.Data
```

## Structure

```
src/FusionRpg.Core/Demons/        → DemonSpeciesCatalog.cs, DemonTraitCatalog.cs, DemonRarity.cs, validation
src/FusionRpg.Contracts/          → DemonDtos.cs
src/FusionRpg.Data/Sqlite/        → RpgStore.Demons.cs (partial: profiles, codex, atomic mint)
src/FusionRpg.Server/             → DemonEndpoints.cs, DemonService.cs
tests/                            → Core.Tests/Demons/, Data.Tests (profile+codex round-trip, atomic mint)
```

## Code style

Match the existing patterns exactly: catalog bootstrap like `StatusCatalogBootstrap`, store partial like `RpgStore.UniqueActors.cs` (gate-serialized, revision bumps), DTOs in Contracts, no SQL outside Data, no Unity anywhere in this module.

## Testing strategy

Data.Tests: mint atomicity (actor+profile+codex in one call; failure leaves nothing), codex upsert idempotency, unknown species/element/trait rejection. Core.Tests: catalog validation (stable ids, element ids exist in extended roster, no duplicate species). Server e2e (SIM): mint → read roster → codex reflects `seen`.

## Boundaries

- **Always:** stable kebab-case ids; catalog discipline (unknown → reject, log, skip); revision bump on every profile write; species link must reference a real dumped game type where one exists.
- **Ask first:** DB schema beyond the two tables; adding rarity tiers; making species DB-authored instead of code-authored.
- **Never:** SQL outside `FusionRpg.Data`; touching UniqueActor FSM semantics; writing type-almanac rows from demon code (existing lock).

## Success criteria

1. Seed content: ≥ 8 species across 4 rarities with valid element typing (incl. ≥1 `light`, ≥1 `dark`, ≥1 `hypno-ally` boss species). 2. Atomic mint proven by tests. 3. Codex read shows `seen`/`discovered` correctly. 4. `guard-dal` green. 5. All existing suites stay green.

## Open questions

Portrait strategy for species whose linked type has no almanac dump yet (fallback icon vs. hide) — decide during FE work.
