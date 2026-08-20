# Spec: demon-summoning (V1 wave 4 — the V1 ship gate)

Module id `demon-summoning` in the [demon system map](../demon-system-map.md). Depends on `demon-core` + `soul-economy`. V1 ships when this module and its minimal FE are playable end-to-end.

## Objective

Souls-funded summoning that mints real demon specimens into the roster. Per the adopted design rules: gacha is a **parallel** acquisition path (capture arrives later), rarity represents *potential and uniqueness* rather than raw power, and every pull mints an **individual** — "duplicates" are distinct specimens whose value deepens when fusion lands.

Success looks like: the player earns Souls by playing, spends them at the Summon panel, watches results reveal with rarity/variant/element/traits, and finds the new demons in the roster and Codex — fully offline-provable in SIM.

## Design

### Banner catalog (code-authored)

`SummonBannerCatalog` in Core. Two banners at V1 (the second exists so standard-vs-focused is a real spend decision — the economy review's "one banner = conveyor belt" finding):

- `standard-rift` — **100** Souls/pull, 10-pull **900**. Pool = every `summonable` species (acquisition flag from demon-core), weighted common 74% · rare 20% · epic 5% · legendary 1%.
- `element-focus` (rotating) — **120**/pull, 10-pull **1,080**. One element's species get 3× weight within their rarity band; rotation is code-scheduled, not time-of-day-dependent (determinism).

**Pity v2 (2026-08-21 review; the old "10-pull guarantees rare+" fired only ~5% of the time — cosmetic):**
- 10-pull rare floor kept (cheap, harmless).
- **Epic hard pity at 25**: 25 pulls without epic+ guarantees one; counter resets on any epic+.
- **Legendary soft ramp + hard ceiling**: base 1%; from pull 41 without a legendary, +6%/pull; hard guarantee at pull 55. Expected first legendary ≈ 43–48 pulls ≈ 6–9 hours at the v2 earn rate — a real first-week arc instead of an unbounded lottery (~13% of players would never see one in 200 pulls at flat 1%).
- Counters are per-player, persist across sessions **and banners**, reset only on a hit of that tier, and are **visible in the Summon panel** ("12/25 to guaranteed Epic · 31/55 to guaranteed Legendary") — a no-money game has no reason for opacity; visible counters turn dead pulls into progress.
- Storage: pity counters live in a per-player row updated inside the pull transaction (derivable from `rpg_summon_log` for audit).

Variant roll per pull (species-allowed variants; `shiny`-class odds 1/64). Trait roll: 1–3 traits from the species pool by rarity (common 1, rare/epic 2, legendary 3). All rolls from the owned seeded PRNG (`gacha` stream — see match-source-core's determinism discipline; never `System.Random`), seed recorded per pull batch.

### Pull flow (server-side, Cold plane, one atomic sequence)

```
POST /api/demons/summon { playerId, bannerId, count(1|10), correlationId }
  → ONE gate-serialized store transaction:
      replay check by (player_id, correlation_id) in rpg_summon_log
        → hit: validate stored (bannerId, count) matches the request, return stored results
      spend check → insufficient: rollback, 409 souls.insufficient (refusals write nothing)
      soul ledger spend + roll results (seeded, pity counters advanced)
      + mints (UniqueActor Roster + profile, origin=summon)
      + codex upsert (MAX-state lattice: seen, first-ever → discovered, never downgrade)
      + pity counter row update
      + rpg_summon_log append (player_id, correlation_id UNIQUE per player, banner_id,
        count, results_json, rng_seed, t)
  → after commit: SignalR DemonsUpdated + SoulsUpdated → return results
```

Crash-safety (corrected by the 2026-08-21 review — the old two-transaction flow had a third post-crash state where Souls were spent but nothing was recorded, and replay would re-roll a fresh seed): spend, mints, codex, pity, and log commit **atomically or not at all** in a single store transaction. Correlation uniqueness is **per player** (a global unique key would let one save replay another save's correlation and receive its results without spending).

### Data

`rpg_summon_log` — `id`; `correlation_id` UNIQUE; `player_id`, `banner_id`, `results_json`, `rng_seed`, `t`. (Profiles/codex/souls tables come from the upstream modules.)

### Duplicates: the V1 valve (adjudicated 2026-08-21)

Every pull mints an individual — but at ~74% common that's 4–5 common specimens per hour into a roster with no sink until fusion. The valve, none of which destroys anything:

- **Active roster (cap 24) / Reserve split**: the player hand-picks the Active roster; everything else lands in a Reserve tab that **stacks by species** with a count badge (`imp-grunt ×17`, expandable). The grid the player looks at stays team-sized.
- **Lock + nickname at reveal**: optional rename in the reveal flow; `locked` sorts to top and protects from future fusion consumption. These two are the cheapest possible implementation of "demons are individuals" and belong at V1, not later.
- Reserve banner text: "Reserved demons become fusion and ritual material in a future update" — sets the hoard expectation explicitly.
- **No release/dismiss valve in V1**: any pre-fusion refund teaches players to liquidate the exact commons fusion will want. (Arknights-style dupe→essence conversion is noted as a candidate *inside* the fusion module, not before it.)

### FE (minimal V1 slice of `demon-domain-fe`)

`#/demons` page: Souls balance header · Summon panel (×1 / ×10, disabled below cost, **pity counters displayed**, results reveal ordered common→legendary with nickname/lock controls) · Active/Reserve roster (portrait from linked type icons, rarity frame, element badges, traits) · Codex tab (species grid: undiscovered = silhouette + `???`; discovery rewards from the soul-economy faucet surface here). Uses the existing bus layer (`lib/bus`) — no direct fetch.

### V1 playable floor (resolved 2026-08-21)

**V1 is an internal gate, not an announced release** — the owner declined the Patron-demon bridge, so the first player-facing announced ship is **expeditions**. V1 still includes the near-free floor items: visible pity counters, nickname/lock at reveal, and codex discovery rewards. No injector work in this module.

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
