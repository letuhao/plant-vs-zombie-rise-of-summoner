# Spec: demon-summoning (V1 wave 4 — the V1 ship gate)

Module id `demon-summoning` in the [demon system map](../demon-system-map.md). Depends on `demon-core` + `soul-economy`. V1 ships when this module and its minimal FE are playable end-to-end.

## Objective

Souls-funded summoning that mints real demon specimens into the roster. Per the adopted design rules: gacha is a **parallel** acquisition path (capture arrives later), rarity represents *potential and uniqueness* rather than raw power, and every pull mints an **individual** — "duplicates" are distinct specimens whose value deepens when fusion lands.

Success looks like: the player earns Souls by playing, spends them at the Summon panel, watches results reveal with rarity/variant/element/traits, and finds the new demons in the roster and Codex — fully offline-provable in SIM.

## Design

### Banner catalog (code-authored)

`SummonBannerCatalog` in Core. V1: one banner, `standard-rift` — cost **100** Souls/pull, 10-pull for **900**. Pool = every `seed`-eligible species weighted by rarity: common 74% · rare 20% · epic 5% · legendary 1%. **Pity:** a 10-pull guarantees ≥1 rare-or-better (roll the guarantee slot last if none landed). Variant roll per pull (species-allowed variants; `shiny`-class odds 1/64). Trait roll: 1–3 traits drawn from the species trait pool by rarity (common 1, rare/epic 2, legendary 3).

### Pull flow (server-side, Cold plane, one atomic sequence)

```
POST /api/demons/summon { playerId, bannerId, count(1|10), correlationId }
  → replay check: correlationId already in rpg_summon_log → return stored results
  → TrySpendSouls(cost, "summon", correlationId)   → 409 souls.insufficient
  → roll results (server RNG; seed recorded in the log for reproducibility)
  → per result: atomic mint (UniqueActor Roster + demon profile, origin=summon)
  → codex upsert (state=seen; first-ever species → discovered)
  → append rpg_summon_log(correlation_id UNIQUE, banner_id, results_json, rng_seed, t)
  → SignalR DemonsUpdated + SoulsUpdated → return results
```

Crash-safety: log append is the last write inside the same store transaction scope as the mints — a replayed `correlationId` either finds the log (returns stored results) or the spend refusal; partial mints must be impossible (single gate-serialized store call).

### Data

`rpg_summon_log` — `id`; `correlation_id` UNIQUE; `player_id`, `banner_id`, `results_json`, `rng_seed`, `t`. (Profiles/codex/souls tables come from the upstream modules.)

### FE (minimal V1 slice of `demon-domain-fe`)

`#/demons` page: Souls balance header · Summon panel (×1 / ×10, disabled below cost, results reveal ordered common→legendary) · roster grid of demon specimens (portrait from linked type icons, rarity frame, element badges, traits) · Codex tab (species grid: undiscovered = silhouette + `???`). Uses the existing bus layer (`lib/bus`) — no direct fetch.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests; dotnet test tests\FusionRpg.Data.Tests
.\scripts\guard-dal.ps1
cd web\fusion-rpg-web; npm run test            # FE unit (Vitest)
```

## Structure

```
src/FusionRpg.Core/Demons/        → SummonBannerCatalog.cs, SummonRoller.cs (pure, seeded-RNG injectable)
src/FusionRpg.Contracts/          → summon DTOs in DemonDtos.cs
src/FusionRpg.Data/Sqlite/        → summon log + atomic pull in RpgStore.Demons.cs / RpgStore.Souls.cs
src/FusionRpg.Server/             → summon endpoint in DemonEndpoints.cs
web/fusion-rpg-web/src/features/demons/ → SummonPanel, DemonRoster, CodexGrid + bus queries/mutations
tests/                            → Core.Tests (roller distribution + pity golden tests, deterministic via seed), Data.Tests (atomicity, replay), FE Vitest
```

## Testing strategy

`SummonRoller` is pure and takes an injected RNG: golden tests for pity guarantee, rarity distribution over a fixed seed sequence, trait-count-per-rarity, variant odds. Data.Tests: correlation replay returns identical results; spend+mint atomicity (forced failure mid-sequence leaves no partial state). SIM e2e: seed Souls → 10-pull → roster has 10 new specimens, codex updated, balance exact, replayed request changes nothing.

## Boundaries

- **Always:** server-authoritative rolls (recorded seed); correlation idempotency on every pull; catalog discipline on species/trait/variant ids.
- **Ask first:** odds/cost changes (game balance), additional banners, pity redesign, any specimen-destroying mechanic (that's fusion's job later).
- **Never:** client-side rolls; real-money anything; deleting/overwriting specimens; injector involvement (summoning is entirely Cold-plane).

## Success criteria

1. SIM e2e: earn → pull → roster/codex/balance all correct, replay-safe. 2. Roller golden tests lock distribution + pity. 3. ×10 completes atomically or not at all. 4. FE panel playable against a live server. 5. All suites + `guard-dal` green.

## Open questions

Reveal animation polish and a dedicated `#/domain` shell are deferred to the fuller `demon-domain-fe` module.
