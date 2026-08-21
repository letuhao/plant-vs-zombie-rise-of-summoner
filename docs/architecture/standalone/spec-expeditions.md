# Spec: expeditions (the announced ship)

Status: **shipped 2026-08-21** — full loop live in SIM/FE (`#/expeditions`); resolver goldens locked per tier.

Module id `expeditions` in the [standalone RPG map](../standalone-rpg-map.md). Depends on `match-source-core` (battles) and the demon V1 modules (specimens to send, Souls to earn). Anchors below were locked by the owner 2026-08-21; the resolver/timer shape follows the plan refinement in [demon-standalone-plan.md](../../../tasks/demon-standalone-plan.md).

## Objective

Send squads of demons on timed expeditions from the web FE, with no game and no injector: dispatch → wait (or recall) → collect a battle-by-battle reveal with events and rewards. This is the first fully playable web loop — the announced ship gate (Checkpoint D).

Success looks like: a player dispatches 3 demons on a 4-hour expedition, comes back later, collects, and watches two battles resolve plus a found-souls event — every reward landing through the existing economy, every battle a real pipeline match, all of it provable in SIM with a force-due hook.

## Locked anchors (owner, 2026-08-21)

- **Tiers:** 30 m / 4 h / 8 h / 20 h. Squad slots by tier: 2 / 3 / 4 / 5. No stamina or energy system.
- **Content = chain + events:** 1 / 2 / 3 / 4 battles by tier, plus a boss wave at 20 h; battle ticks interleave with seed-rolled event ticks (`found-souls`, `wild-demon-met`, `injury`).
- **Rewards = all channels:** Souls + player XP via the pipeline (battles are real web matches), specimen XP per battle won, wild-join mints (origin `expedition`), fusion material stubs into a per-player inventory.
- **Seed-sealed at dispatch;** recall pro-rates at tick boundaries. Specimens on an expedition are soft-locked against PvZ deploy, and vice versa.
- Soul-ledger tail-trim (the P4 deferral) lands in this wave — expedition volume makes it real.

## Design

### Timeline model

An expedition is a fixed tick timeline generated deterministically from `(tier, squad, seed)`:

| Tier | Duration | Ticks | Battles | Slots |
|---|---|---|---|---|
| `scout-30m` | 30 m | 6 × 5 m | 1 | 2 |
| `forage-4h` | 4 h | 8 × 30 m | 2 | 3 |
| `hunt-8h` | 8 h | 8 × 1 h | 3 | 4 |
| `warpath-20h` | 20 h | 10 × 2 h | 4 + boss | 5 |

Battle ticks are evenly spaced across the timeline (boss on the final tick at 20 h). Every non-battle tick rolls on the `loot` stream: nothing / `found-souls` (tier-scaled Souls) / `wild-demon-met` (a wild-join candidate) / `injury` (a squad member fights the remaining battles with a power debuff — resolver-internal channel mod, no persistent state in V1).

### ExpeditionResolver (Core, pure)

`ExpeditionResolver.Resolve(tier, squad, seed, elapsedTicks)` → ordered tick outcomes: battle setups (tier-scaled `WaveCatalog` waves), event results, and a COMPLETE rewards manifest (Souls amounts with the greedy squad multiplier applied, wild-join rolls with species/variant/traits, material drops, per-battle specimen XP) — reward policy lives here, never in the server layer. Pure and integer-only, same discipline as `BattleEngine`. Streams derive from the expedition seed with per-tick names (`tick:{t}`, plus `battle:{i}` for battle seeds) rather than one shared `loot` stream — per-tick derivation is what makes recall pro-rating exact by construction. `elapsedTicks` is the recall pro-rating input: only elapsed ticks resolve; a full collect passes the tick count.

Because the seed is sealed at dispatch, **lazy resolution at collect is provably identical to eager resolution at dispatch** — the server stores no outcome, only `(tier, squad_json, seed)`.

Wild-join pool: non-capture-exclusive, non-legendary species; rarity weights 84/15/1 (‰-scaled), shiny 1/64 (same as summons). Materials are stubs for `demon-fusion`: element essences + rarity shards from a small validated `DemonMaterialCatalog` (unknown ids reject).

### Data

- `rpg_expeditions`: id, player_id, correlation_id (UNIQUE per player), state (`Dispatched`/`Collected`/`Recalled`), tier, squad_json, seed, dispatched_utc, due_utc, collected_utc.
- `rpg_expedition_members`: expedition_id, instance_id — the **soft-lock membership rows** (Cold-plane; no specimen FSM change). Active while the expedition is `Dispatched`. Consulted in BOTH directions: expedition dispatch refuses PvZ-deployed specimens; UniqueActor deploy refuses specimens on an active expedition.
- `rpg_demon_materials`: player_id, material_id, qty (per-player inventory; qty adds atomically at collect).
- Timers are just `due_utc` — no scheduler, no background jobs. Collect before due refuses; recall any time.

### Server

`POST /api/expeditions/dispatch` (playerId, tier, squad instance ids, correlationId) — validates ownership, lock state, slot count; seals a server-rolled seed; writes expedition + membership in one transaction. Correlation-idempotent (replay returns the recorded expedition).

`POST /api/expeditions/{id}/collect` — after due: resolver → each battle runs through **`WebMatchService`** (correlation `exp:{expeditionId}:{n}`, matchKey `exp-{expeditionId}-{n}` — colon-free so it can never be misread as a `web:{matchKey}:{n}` actor ptr; battles are real runs with facts/XP/Souls via the pipeline) → then one transaction applies event Souls (`AwardSouls`, new reason `expedition`), specimen XP, wild-join mints (origin `expedition`), and materials → state `Collected`, membership released. Idempotent by state + correlation.

`POST /api/expeditions/{id}/recall` — resolves only elapsed ticks (pro-rated), then the same reward path; state `Recalled`.

`GET /api/expeditions/{playerId}` — active + recent, with due timestamps for FE timers. SIM-only force-due hook (`FUSIONRPG_SIM=1`): rewinds `due_utc` so e2e tests never wait.

### FE

`#/expeditions`: dispatch from the Active roster (slot gating, locked specimens greyed), tier pick, live countdown timers, collect reveal battle-by-battle with event cards, materials shelf. Push updates ride the existing hub (`DemonsUpdated`, `SoulsUpdated`, runs feed).

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests      # resolver determinism + pro-rating goldens
dotnet test tests\FusionRpg.Data.Tests      # soft-lock + inventory + trim
dotnet test tests\FusionRpg.E2E.Tests       # SIM full loop with force-due
.\scripts\guard-dal.ps1
```

## Structure

```
src/FusionRpg.Core/Expeditions/   → ExpeditionResolver.cs, ExpeditionTierCatalog.cs, DemonMaterialCatalog.cs
src/FusionRpg.Data/Sqlite/        → RpgStore.Expeditions.cs (+ schema block), soul tail-trim in RpgStore.Souls.cs
src/FusionRpg.Server/             → ExpeditionEndpoints.cs
web/fusion-rpg-web/src/features/expeditions/
tests/                            → Core resolver goldens, Data lock/trim, E2E loop
```

## Code style

Resolver mirrors `BattleEngine` house style: pure, injected seed, catalog discipline, integer per-mille rolls, no logging — the manifest is the record.

## Testing strategy

- **Determinism:** same `(tier, squad, seed)` twice ⇒ identical manifest; goldens for one expedition per tier.
- **Pro-rating:** recall at tick k resolves exactly the first k ticks; recall at 0 elapsed ticks yields nothing.
- **Soft-lock both ways:** dispatch refuses a PvZ-deployed specimen; PvZ deploy refuses an expedition member; collect releases the lock.
- **SIM e2e:** dispatch → force-due → collect ⇒ battle runs exist (game `webrpg-1`), specimen XP awarded, wild-join mint has origin `expedition`, materials in inventory, Souls ledger consistent; replayed collect adds nothing.
- **Tail-trim:** ledger trim keeps the watermarked balance byte-identical after rebuild.

## Boundaries

- **Always:** seed sealed at dispatch; battles through `WebMatchService` only; rewards through existing ledgers; correlation idempotency; membership checks in both deploy directions.
- **Ask first:** new expedition tiers or reward scaling changes (game balance); persistent injuries; new material sinks.
- **Never:** background schedulers or clock-driven resolution (lazy only); outcome stored at dispatch; SQL outside Data; legendaries or capture-exclusive species in the wild-join pool.

## Success criteria

1. Dispatch→collect loop playable in FE against SIM (Checkpoint D gate). 2. Resolver goldens locked per tier. 3. Soft-lock proven both ways. 4. Soul-ledger tail-trim + archive lands with a rebuild-identical test. 5. All suites + guards green.
