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

### Earn rules (code-authored policy, versioned — v2 numbers per the 2026-08-21 economy review)

`SoulEarnPolicy` in Core maps Activity facts → deltas:

- `ZombieKilled` **+1, capped at 50 counted kills per match** — the cap is policy-layer and golden-tested; it kills the stall-farm exploit (uncapped per-kill pay rewards match *length*, and a deliberate 80-kill stall-defeat out-earned a fast clean win under the old +2).
- `MatchEnded(victory)` **+100 with repeat decay**: full for the first win per level per day, 50% thereafter (rested-bonus shape — caps the grind ceiling without punishing normal play). `defeat` **+25**. `PlantLost` / `ExtraSpawnFired` 0.
- **Codex discovery faucet**: first-ever discovery of a species pays by rarity — common 25 · rare 75 · epic 200 · legendary 500; codex milestones pay 500 at 50% and 1,500 at 100% (claimable at **90% catalog** once capture-exclusives exist — see the standalone map guardrails). Gives pulls an information axis of value beyond inventory.
- Target rate: ~5–8 pulls/hour of active play (the original +2/kill uncapped yielded ~20–25/hour and consumed the collection arc in a weekend).

Dedupe key = the activity fact id, so replays/re-ingest never double-earn. **The earn append runs inside the same store transaction as the fact append** — a crash between fact and earn would otherwise lose Souls undetectably, since the balance watermark only covers the soul ledger itself.

### Spend

Single store call: `TrySpendSouls(playerId, amount, reason, correlationId)` — atomic check-and-append under the store gate; insufficient balance → refusal (server returns 409 `souls.insufficient`). Idempotency contract (corrected by the 2026-08-21 review): a successful spend with a replayed `(playerId, correlationId)` returns the original success **without spending again**; a refusal writes no state, so a retried refusal simply re-evaluates. Feature flows that pair a spend with further writes (summoning) must run the whole sequence in **one** store transaction — see the summoning spec. No negative balances, ever.

**Threat model (documented, accepted):** localhost, no auth, user-owned SQLite — every guarantee here is an *honest-server* guarantee (tamper-evident via the ledger, not tamper-proof). Always-on debug endpoints can synthesize kill events; single-player self-cheat is out of scope.

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
