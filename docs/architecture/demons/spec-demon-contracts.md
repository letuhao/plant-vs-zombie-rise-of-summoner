# Spec: demon-contracts

Status: **shipped 2026-08-21** (Checkpoint G passed; owner decisions below). Module id `demon-contracts` in the [demon system map](../demon-system-map.md). Depends on `demon-core` (specimens + profiles), `soul-economy` (the ledger this module spends from), and `demon-fusion` (the release valve a capacity cap makes meaningful). **Pure standalone scope** — server + web only, no injector work, no PvZ dependency; every rule here is provable with the game closed.

## Objective

Turn "demons I own" into "demons who serve me." A contract binds one demon to the summoner: it occupies a slot, it carries loyalty that rises with use and rots when tribute goes unpaid, and it can refuse to be fielded. Owning stays unlimited; **fielding** becomes finite and earned.

Success looks like: a player with 40 demons has 12 under contract, sees the daily Soul tribute their army costs, buys a 13th slot for 300 Souls, watches a neglected demon slide from Devoted back to Bound, and gets a flat refusal when they try to send an unbound imp on an expedition.

## Locked decisions (owner, 2026-08-21)

1. **Scope — capacity *and* obedience.** A contract is both a binding slot (how many you can field) and a loyalty record (how well they serve).
2. **Loyalty moves by use *and* time decay.** Deploying and winning raises it; elapsed real days with unpaid upkeep lower it.
3. **Disobedience is a hard refusal.** Below the deploy floor a demon simply cannot be fielded — no stat penalty, no RNG roll, no new random stream.
4. **Decay floors at the deploy threshold.** Time can strip everything a demon *earned*, never its deployability. Only battle defeats can push a demon under the line.
5. **Capacity = base slots + Soul-priced upgrades**, rising price — the permanent late-game sink the economy lacks.
6. **Upkeep is per contracted demon, rarity-scaled, charged daily**, settled lazily from the balance.
7. **Personality modifies loyalty rates** — gain, decay, and upkeep multipliers rolled per specimen.
8. **Existing rosters auto-bind best-first up to capacity**; the overflow sits unbound — still owned, still fusable, not deployable.

## Design

### The two states that matter

Every demon is in exactly one of three conditions, and every refusal in this module names one:

| Condition | Meaning | Costs upkeep | Decays | Deployable |
|---|---|---|---|---|
| **Unbound** | owned, no contract — the real Reserve | no | no | **no** (`contract.unbound`) |
| **Bound** | contracted, loyalty ≥ deploy floor | yes | yes | yes |
| **Insubordinate** | contracted, loyalty < deploy floor | yes | (already floored) | **no** (`contract.insubordinate`) |

Unbound is deliberately **free and frozen**: no tribute, no decay, loyalty preserved exactly as it was. A benched hoard costs nothing, and a returning player never finds their army dead — only their *contracted* army poorer. Binding charges one day of upkeep up front (the pact fee), so bind-for-one-battle-then-release costs the same as simply keeping it.

### Numbers (`ContractPolicy`, Core — pure integers, spec-locked; tuning is ask-first)

```
Loyalty scale                0 … 1000
DeployFloor                  200          below this = insubordinate
BindLoyalty                  300          a fresh contract starts here
Ranks (loyalty → rank, own-channel bonus in per-mille):
    < 200   Insubordinate    — (cannot field)
  200–399   Bound            +0‰
  400–599   Sworn            +15‰
  600–799   Trusted          +35‰
  800–1000  Devoted          +60‰
Gains        battle/expedition won with the demon  +15
             lost                                  −10   (may cross the floor)
             daily gain cap per demon              +60   (losses are uncapped)
Decay        25 per unpaid day, floored at DeployFloor
Upkeep/day   common 2 · rare 5 · epic 12 · legendary 25   (× personality upkeep %)
Ritual       +100 loyalty (× personality gain %), price 50/100/200/400 by rarity
Slots        base 12 · k-th purchased slot costs 300 × k · hard max 48 total
Settle       whole UTC days since last settle, clamped to 30
```

**Personality** (rolled at mint from the specimen's seeded stream; pre-existing demons derive one deterministically from `instanceId`) — integer percentages applied as `x * pct / 100`:

| Personality | Gain | Decay | Upkeep |
|---|---|---|---|
| `loyal` | 120 % | 80 % | 100 % |
| `stoic` | 90 % | 60 % | 100 % |
| `proud` | 100 % | 100 % | 130 % |
| `calculating` | 100 % | 90 % | 110 % |
| `feral` | 80 % | 150 % | 70 % |

**Why the Bound band pays +0‰:** a fresh contract sits at 300 = Bound, so nothing a test or a new player owns changes any existing combat number. The battle and expedition goldens stay byte-identical on adoption; loyalty bonuses only appear once a demon has *earned* Sworn. That property is a test, not a hope (success criterion 5).

### Settlement — lazy, day-quantised, idempotent

There is no background sweep and no offline simulation. `SettleContracts(playerId, utcNow)` runs at the head of every contract-relevant call:

1. Whole UTC days elapsed since `last_settled_utc`, clamped to **30** (a six-month absence settles 30 days — bounded work, bounded bill).
2. For each elapsed day, in order: `due = Σ upkeep(bound demons)`. If balance ≥ due → `TrySpendSouls(due, "upkeep", dedupe "upkeep:{playerId}:{yyyy-MM-dd}")`. If not → **no charge, every bound demon decays** that day (floored at `DeployFloor`). All-or-nothing per day: you either paid the tribute or you didn't.
3. Stamp `last_settled_utc` to the settled day boundary.

The per-day dedupe key is the replay gate — the same house pattern as every other ledger write. Settling twice for the same day charges once, and a crash mid-settle resumes exactly where it stopped. The whole pass is one gate-serialized transaction, and tests drive it through the existing `DateTimeOffset? utcNow = null` injection precedent from `RpgStore.Expeditions`.

Cost ceiling: ≤ 48 bound demons × ≤ 30 days of integer math, ≤ 30 ledger rows. Nothing here scales with roster size beyond the slot cap.

### Migration — one-shot auto-bind

**Built addition (plan §Wave G decision 1):** a newly minted demon — pull, fusion output, or wild join —
also binds automatically when a slot is free, and simply arrives unbound when capacity is full. It is
free (no pact fee; that fee exists to stop bind/release churn, and a mint is not churn). Without it,
every demon acquired after migration would land unfieldable behind a button press with no decision
behind it while slots stand empty.

The first settle for a player with no `rpg_contract_state` row binds best-first — rarity desc, then star, then level, then oldest — until base capacity is full, each at `BindLoyalty` with a rolled personality, and stamps `migrated_utc`. Deterministic, idempotent, and free (no pact fee on migration). The overflow is unbound and shows up in the FE with the capacity header explaining exactly why.

### Gates — where a refusal actually lands

Contracts guard every path that *fields* a demon, and nothing else:

| Path | Insertion point | Refusals |
|---|---|---|
| Web battle squad | `WebMatchService.BuildSquad`, after `squad.unknown-specimen` | `squad.unbound`, `squad.insubordinate` |
| Expedition dispatch | `RpgStore.Expeditions`, after `specimen.on-expedition` | `specimen.unbound`, `specimen.insubordinate` |
| PvZ deploy | `TryBeginDeploy`, **only for specimens that have a demon profile** | `contract.unbound`, `contract.insubordinate` |
| Patron designation | `SetPatron`, before pricing | `patron.unbound`, `patron.insubordinate` |

Unique actors without a demon profile are untouched on the PvZ path — a test pins that, because that path predates demons entirely.

Releasing is refused for a demon that is on an expedition (`contract.on-expedition`) or is the active patron (`contract.is-patron`) — mirroring the fusion guard. Fusion itself needs no new rule: **unbound demons are fully consumable, and that is the point** — the capacity cap is what finally gives fusion its pressure. Retirement (fusion consumption) releases the contract inside the same transaction, freeing the slot.

### Data (all DDL in `FusionRpg.Data`, `EnsureColumn` migration style)

| Table | Key | Columns (essence) |
|---|---|---|
| `rpg_demon_contracts` | `instance_id` (FK → `rpg_unique_actors`) | `player_id`, `bound` (0/1), `loyalty`, `personality`, `bound_utc`, `released_utc`, `gain_day` + `gain_today` (the daily gain cap window), `revision` |
| `rpg_contract_state` | `player_id` | `purchased_slots`, `last_settled_utc`, `migrated_utc`, `revision` |

Rank is **derived**, never stored — a pure Core function of loyalty, so a policy change can never leave stale ranks in the DB. Both tables join `Reset()`.

New ledger reasons in `SoulEarnPolicy.Reasons`: `upkeep`, `contract-slot`, `contract-ritual`. The pact fee reuses `upkeep` with dedupe `bind:{instanceId}:{yyyy-MM-dd}`.

### Server + FE

- `GET /api/contracts/{playerId}` — settles first, then returns capacity (used / total / next slot price), total daily tribute, and per-demon rows (rank, loyalty, personality, upkeep, deployable).
- `POST /api/contracts/bind` · `/release` · `/ritual` · `/slots/buy` — all take `correlationId` and are replay-idempotent; 409 on `souls.insufficient`.
- Hub: `ContractsUpdated` + `SoulsUpdated`.
- SIM test hook: `POST /api/test/contracts/settle { days }` in the existing test group, so E2E can travel time without touching the wall clock.
- FE (`#/demons`): capacity header (`18 / 24 slots · next 900 Souls · 84 Souls/day`), per-card contract badge with rank, a loyalty bar, personality, and bind/release actions; insubordinate cards carry a ritual CTA. Expedition and battle pickers disable unbound/insubordinate demons with the reason inline, so refusals are visible before they're returned.

### Explicitly not in this module

Random disobedience rolls, loyalty effects inside live PvZ (the patron aura stays what it is), Soul refunds for releasing, trading, faction/reputation loyalty (that's `world-events`), and any change to earn-v2 kill/victory math.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests    # ContractPolicy: ranks, personality math, settle arithmetic
dotnet test tests\FusionRpg.Data.Tests    # settle idempotency, decay floor, gates, migration
dotnet test tests\FusionRpg.E2E.Tests     # SIM: bind → dispatch → time travel → tribute → refusals
.\scripts\guard-dal.ps1
cd web\fusion-rpg-web; npm test
```

## Structure

```
src/FusionRpg.Core/Demons/Contracts/  → ContractPolicy.cs, LoyaltyRank.cs, DemonPersonality.cs
src/FusionRpg.Contracts/              → ContractDtos.cs
src/FusionRpg.Data/Sqlite/            → RpgStore.Contracts.cs (schema, settle, bind/release/ritual/slots, gates)
src/FusionRpg.Server/                 → ContractEndpoints.cs
web/fusion-rpg-web/src/               → lib/bus/contracts.ts, features/demons/ contract badge + capacity header
tests/                                → Core policy, Data transactions/gates, E2E SIM loop, Vitest view math
```

## Code style

Core policy pure and integer (no floats, no `DateTime.Now`); store partial gate-serialized with revision bumps and `DateTimeOffset? utcNow = null` injection like `RpgStore.Expeditions`; every Soul movement goes through the existing ledger with a dedupe key; refusals write nothing. No SQL outside `FusionRpg.Data`, no Unity anywhere.

## Testing strategy

- **Policy (Core):** rank boundary table (199/200/399/400/…/1000); personality multiplier arithmetic incl. integer truncation; daily-gain-cap window; decay floor math; slot price ladder; settle-day arithmetic across month and year boundaries.
- **Data:** settle twice for one day charges once; 30-day clamp on a long absence; insolvent day decays instead of charging; decay never lands below `DeployFloor`; defeat *can* cross it; migration is deterministic and one-shot; pact fee charged on bind but not on migration; release refused for patron/on-expedition; retirement frees the slot; every gate refuses with its exact reason and writes nothing; correlation replay on all four POSTs.
- **E2E (SIM):** bind → dispatch expedition (ok) → release → dispatch (`specimen.unbound`) → ritual on an insubordinate demon restores deployability → advance 3 days → tribute charged exactly, balance exact.
- **Regression lock:** run the battle and expedition goldens after adoption — a fresh roster is Bound (+0‰), so the hashes must be **byte-identical**. Any drift means the loyalty bonus leaked into the base case.
- **Vitest:** capacity header math, rank labels, disabled-picker reasons.

## Boundaries

- **Always:** integer per-mille and per-cent math; one transaction per operation; dedupe key on every Soul movement; refusals write nothing; rank derived not stored; gates named by condition (`unbound` vs `insubordinate` are different messages).
- **Ask first:** any tuning of the numbers block; gating a path not listed above; making upkeep partial-payable; raising the 48-slot ceiling.
- **Never:** a background timer or sweep job; simulating offline battles; RNG in the obedience decision; SQL outside `FusionRpg.Data`; touching earn-v2 kill/victory math; consuming or releasing the active patron.

## Success criteria

1. Capacity is server-authoritative — every fielding path refuses an unbound demon, with the PvZ path leaving non-demon actors untouched.
2. Upkeep settles idempotently, is bounded at 30 days, and an insolvent day decays instead of charging.
3. Decay never drops a demon below `DeployFloor`; only defeats do.
4. Migration auto-binds best-first, exactly once, deterministically.
5. **Battle + expedition goldens byte-identical after adoption** (Bound = +0‰).
6. All suites and all four guards green.

## Open questions

Whether a demon left insubordinate for a long stretch should eventually break its own contract (auto-release, freeing the slot) — deferred; it interacts with `world-events` and needs no answer to ship this module.
