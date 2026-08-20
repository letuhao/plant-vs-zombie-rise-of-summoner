# Spec: match-source-core (wave 1)

Module id `match-source-core` in the [standalone RPG map](../standalone-rpg-map.md). Depends on `standalone-charter`. Consumed by `expeditions` and `web-battles`.

## Objective

A server-side battle engine and match producer, so web-mode gameplay creates **real matches** — runs, events, Activity facts, XP, and Souls — through the exact pipeline injector matches use. After this module, "a match happened" no longer implies "the game was open."

Success looks like: given a demon squad and a wave definition, the server resolves a deterministic battle, the pipeline records it indistinguishably from any other run (source-tagged `web`, game `webrpg-1`), and every existing projection (runs list, facts, XP, Souls) just works — provable entirely in CI.

## Design

### BattleEngine (Core, pure)

New `FusionRpg.Core/Battle/`: `BattleEngine.Resolve(BattleSetup, ISeededRng) → BattleReport` — **pure and deterministic**: no I/O, no clock, no ambient state; same setup + seed ⇒ byte-identical report (summon-roller precedent, engine-scale).

- **Inputs:** squad = demon specimen snapshots (level, element typing, traits, equipment) composed into derived stats via the existing **ActorHub** compose path (server-side profiles, as the status prove-packs already do); wave = enemy roster from a code-authored `WaveCatalog` (species + counts + level scaling).
- **Resolution loop:** round-based auto-combat. Damage runs the shipped **OverlayCombatMath** (typed power/defense + **ElementHub** matchup + hit/crit); statuses run **StatusRuntime** instances (DoT pulses, CC skips, resistance two-phase); traits fire as **EffectBag** grants (FT1–FT4 triggers) whose FA plans are executed against battle-local actor state — no Funnel/Writer involved, that path is PvZ-mode-only; battle state is plain memory owned by the engine.
- **Output `BattleReport`:** ordered event list in the **existing kind vocabulary** (`board.start`, `zombie.spawn`, `zombie.die`, `plant.die` analogues for squad members, `match.result`, …), outcome, per-actor tallies, seed. Reusing kinds — not inventing a parallel vocabulary — is what makes every downstream projection free.

### WebMatchService (Server)

`RunWebMatch(playerId, setup, correlationId)`: mint `matchKey` → resolve via BattleEngine → feed the report's events through **EventIngest** (source `web`, game `webrpg-1`, seed recorded in `modifiers_json`) → existing projections do the rest (run row, Activity facts, XP awards, Soul earns). Correlation-idempotent: a replayed correlation returns the recorded run instead of re-resolving. No new ledger paths — the one-economy invariant.

### Explicitly not in this module

No FE, no expedition timers, no player-facing API beyond a SIM/test trigger (`POST /api/test/web-match` under `FUSIONRPG_SIM=1`) — `expeditions` owns the player surface. No new event kinds without ask-first. No injector changes.

## Commands

```powershell
dotnet test tests\FusionRpg.Core.Tests      # engine golden battles
dotnet test tests\FusionRpg.Data.Tests      # (unchanged; pipeline writes via existing store paths)
.\scripts\guard-dal.ps1; .\scripts\guard-funnel-delta.ps1
```

## Structure

```
src/FusionRpg.Core/Battle/    → BattleEngine.cs, BattleSetup/Report models, WaveCatalog.cs, ISeededRng reuse
src/FusionRpg.Contracts/      → BattleDtos.cs (report/result wire shapes)
src/FusionRpg.Server/         → WebMatchService.cs (+ SIM trigger endpoint)
tests/FusionRpg.Core.Tests/Battle/    → golden battles, determinism, subsystem-integration cases
tests/FusionRpg.E2E.Tests / SIM       → report → ingest → facts/XP/Souls end-to-end
```

## Code style

Engine mirrors the Core house style: pure static-ish services, injected RNG, catalog discipline (unknown species/trait/element → reject), no logging side effects — the report *is* the log.

## Testing strategy

- **Determinism:** same setup+seed twice ⇒ identical reports (golden files for 3 canonical battles: stomp-win, close-win, wipe).
- **Subsystem integration:** element matchup swings a fixed battle; a DoT trait kills through a round; CC skips a turn; resistance blocks an apply — each asserted against the same shipped math the PvZ overlay uses.
- **Pipeline e2e (SIM):** run a web match → assert run row (game `webrpg-1`), Activity facts, XP ledger, and Soul ledger all appear with correct dedupe; replayed correlation adds nothing.

## Boundaries

- **Always:** engine purity (no I/O/clock); reuse existing event kinds; seed recorded on every run; correlation idempotency.
- **Ask first:** new event kinds; changes to wave/reward scaling (game balance); exposing battle APIs beyond the test trigger.
- **Never:** touch Funnel/EntityStatWriter (PvZ-mode-only paths); write ledgers outside the ingest projections; nondeterministic resolution; SQL outside Data.

## Success criteria

1. Three golden battles locked and deterministic. 2. Subsystem-integration tests prove shared math (element/status/trait effects visible in outcomes). 3. SIM e2e: a web match produces run + facts + XP + Souls with zero injector involvement. 4. Guards green; all existing suites green. 5. `runs` list in the FE shows web matches alongside PvZ runs with their profile id.
