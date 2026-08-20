# Spec: soul-economy (V1 wave 3)

Module id `soul-economy` in the [demon system map](../demon-system-map.md). Depends on `demon-core` (spend targets), feeds `demon-summoning`.

## Objective

Souls are the strategic currency of the demon game: earned from real play, spent on summoning (V1) and later on rituals, fusion, and Domain upgrades. The vision's rule: the player should regularly face meaningful spend choices.

Success looks like: playing runs accrues Souls automatically from Activity facts, balances survive restarts and trims, spends are atomic and idempotent, and nothing ever double-earns or goes negative.

## Design

**Same pattern as the XP ledger** (append + watermarked snapshot), because it is proven and compaction-safe.

### Data (DDL in `FusionRpg.Data`)

| Table | Key | Columns (essence) |
|---|---|---|
| `rpg_soul_ledger` | `id`; **UNIQUE(player_id, reason, dedupe_key)** | `player_id`, `delta` (+earn/−spend), `reason`, `ref_kind`/`ref_id` (e.g. `activity_fact`/id, `summon`/correlationId), `t`, `payload_json`, `dedupe_key` |
| `rpg_soul_balances` | `player_id` | `balance`, `through_ledger_id` (watermark), `earned_total`, `spent_total`, `revision`, `updated_utc` |

Ledger is SSOT; balance is the durable watermarked projection (rebuild = snapshot + hot tail). Retention: tail-trim + cold archive exactly like the XP ledger (retain tail default 10 000/player — additive constant beside the sealed policy, same archive/verify/trim flow).

### Earn rules (code-authored policy, versioned)

`SoulEarnPolicy` in Core maps Activity facts → deltas. V1 seed values (balance later): `ZombieKilled` +2 · `MatchEnded(victory)` +150 · `MatchEnded(defeat)` +25 · `PlantLost` 0 · `ExtraSpawnFired` 0. Dedupe key = the activity fact id, so replays/re-ingest never double-earn. Earn projection runs where Activity facts are appended (server ingest path, Cold plane — never the injector).

### Spend

Single store call: `TrySpendSouls(playerId, amount, reason, correlationId)` — atomic check-and-append under the store gate; insufficient balance → refusal (server returns 409 `souls.insufficient`); same `correlationId` replay returns the original result (idempotent). No negative balances, ever.

### Server

`GET /api/souls/{playerId}` (balance + totals) · `GET /api/souls/{playerId}/ledger?limit&afterId` · `POST /api/test/seed-souls-demo` (SIM). Spends are **not** a public generic endpoint — only feature endpoints (summon, later rituals) spend, each with its own reason. SignalR `SoulsUpdated`.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests; dotnet test tests\FusionRpg.Core.Tests
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Demons/        → SoulEarnPolicy.cs
src/FusionRpg.Contracts/          → SoulDtos.cs
src/FusionRpg.Data/Sqlite/        → RpgStore.Souls.cs (ledger, balance watermark, TrySpend, trim)
src/FusionRpg.Server/             → SoulEndpoints.cs + earn projection hook in the Activity append path
tests/                            → Data.Tests (idempotent earn/spend, watermark rebuild, trim survival), Core.Tests (policy table)
```

## Testing strategy

Data.Tests: earn dedupe (same fact twice → one ledger row), spend atomicity + insufficiency + correlation replay, balance rebuild from snapshot + tail after simulated trim. Core.Tests: earn policy golden table. Server e2e (SIM): seed activity → balance reflects policy → spend → 409 on overdraft.

## Boundaries

- **Always:** ledger append-only until archive+trim; every mutation carries a dedupe/correlation key; watermark before trim (refuse otherwise, like XP).
- **Ask first:** changing earn values (game balance), new spend sinks, cross-player transfers.
- **Never:** SQL outside Data; earning from anything but recorded Activity facts; negative balances; any real-money or network-shop concept — Souls are single-player and gameplay-earned only.

## Success criteria

1. Playing a SIM match produces the policy-exact Soul total. 2. Double-ingest earns once. 3. Overdraft refused; replayed spend returns the original result. 4. Balance survives snapshot+trim rebuild test. 5. `guard-dal` green; all suites green.
