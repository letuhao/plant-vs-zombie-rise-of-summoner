# Spec: match-source-core (wave 1)

Module id `match-source-core` in the [standalone RPG map](../standalone-rpg-map.md). Depends on `standalone-charter`. Consumed by `expeditions` and `web-battles`. Hardened per the [2026-08-21 structured review](audit-2026-08-21.md) — the pipeline adaptation checklist in §Preconditions is binding.

## Objective

A server-side battle engine and match producer, so web-mode gameplay creates **real matches** — runs, events, Activity facts, XP, and Souls — through the exact pipeline injector matches use. After this module, "a match happened" no longer implies "the game was open."

Success looks like: given a demon squad and a wave definition, the server resolves a deterministic battle, the pipeline records it indistinguishably from any other run (source-tagged `web`, game `webrpg-1`), and every existing projection (runs list, facts, XP, Souls) just works — provable entirely in CI.

## Design

### BattleEngine (Core, pure)

New `FusionRpg.Core/Battle/`: `BattleEngine.Resolve(BattleSetup, SeededRng) → BattleReport` — **pure and deterministic**: no I/O, no clock, no ambient state; same setup + seed ⇒ byte-identical report.

- **Inputs:** squad = demon specimen snapshots (level, element typing, traits, equipment) composed into derived stats via the existing **ActorHub** compose path (server-side profiles, as the status prove-packs already do); wave = enemy roster from a code-authored `WaveCatalog` (species + counts + level scaling).
- **Resolution loop:** round-based auto-combat. Damage runs the shipped **OverlayCombatMath** (typed power/defense + **ElementHub** matchup + hit/crit); statuses run **StatusRuntime** instances; traits fire as **EffectBag** grants (FT1–FT4). Mutations route through a **battle-local Core `EffectFunnel` instance + battle `IEffectActionSink`** so merge semantics, `|amount|`/depth caps, and `ProcDepthLimit` match PvZ mode exactly — bypassing the Funnel would fork combat numbers between modes. `EntityStatWriter`/Unity paths stay PvZ-only; battle state is engine-owned memory. Guard note: `Core/Battle/` must not contain the tokens the funnel/writer guards grep for (`EntityStatWriter`, `AddPlantHp`, …) — naming discipline is part of this spec.
- **Round clock (locked before any golden test):** `RoundDurationMs = 1000`; per-round order: status ticks (DoT pulses, expiries) → initiative-ordered attacks → on-death triggers → round end. Every reused subsystem is millisecond-clocked (ICD, `periodMs`), so this constant is contract, not style; changing it bumps `rulesetVersion` and re-blesses goldens.
- **Determinism discipline (from prior-art research; binding):** vendor an owned PRNG in Core (xoshiro256** or PCG32) as `SeededRng` — **never `System.Random`**, whose seeded sequence is not stable across .NET versions (the existing `SeededCombatRng` wraps it and must not back goldens). Per-system streams derived by splitmix from the run seed (`initiative`, `damage`, `crit`, `status`, `essence`, `proc`) so an extra roll in one system never shifts another; expedition streams (`tick:{t}`, `battle:{i}`) live in spec-expeditions.md. Integer/fixed-point combat state only — no float in any game-affecting branch; no dictionary enumeration in the resolver (stable-sorted collections by actor id). Reports are stamped `(engineVersion, rngAlgoVersion, rulesetVersion, seed)`; replay guarantees byte-identical results only within matching versions.
- **Rounding canon (decided 2026-08-21, before goldens):** the engine's **integer per-mille arithmetic is canonical** for web mode (e.g. element share ±250‰ with truncating division). The PvZ overlay's double-based path never computes the same battles, so "shared math" means shared *semantics* (Funnel merge, caps, proc depth, matchup relations), not bit-level parity between modes.
- **Output `BattleReport`:** ordered event list in the **existing kind vocabulary — lean profile**: `board.start`, `zombie.spawn`/`plant.spawn` analogues, `zombie.die`/`plant.die`, `match.result`, **`board.end` (mandatory — runs never close or compact without it)**. **No per-attack damage events**; per-actor tallies live in the report and persist via `snapshot_json`. ~40 events per 20-actor battle. Actor ids use the synthetic scheme `web:{matchKey}:{n}` — load-bearing for `entities` uniqueness, Activity fact dedupe, XP-ledger dedupe, and non-collision with unique-actor `last_ptr` matching. The engine emits no wall-clock timestamps; `WebMatchService` stamps strictly monotonic `t` values at ingest.

### WebMatchService (Server)

`RunWebMatch(playerId, setup, correlationId)`:

```
1. Replay check: rpg_web_match_log by (player_id, correlation_id) → hit ⇒ return recorded run
2. Append rpg_web_match_log(player_id, correlation_id UNIQUE per player, match_key, setup_json,
   seed, engine/ruleset versions)  ← written BEFORE ingest; the durable idempotency anchor
3. Resolve via BattleEngine
4. Ingest the report through a DEDICATED single-transaction insert (store-level InsertEvents),
   game=webrpg-1, source=web, explicit playerId — never the shared ingest channel
5. Existing projections do the rest (run row, facts, XP, Souls once soul-economy exists)
```

Step 4 is deliberate: the shared channel batches ≤800 events with shared-fate rollback — a replayed `board.start` hitting the unique `match_key` index would roll back and silently drop interleaved **live PvZ events**. Web reports get their own transaction; a crash between 2 and 4 is recovered by a boot sweep that re-ingests logged matches with no run row (resolution is deterministic from the logged setup + seed). One-economy invariant unchanged: no new ledger paths.

### Preconditions — pipeline adaptations (binding; verified against code by the 2026-08-21 review)

These land **in or before** this module; each is small and surgical:

1. Thread the envelope `game` into `UpsertType` / `BumpTypeKilled` / `UpsertTypeFromSpawn` (today they stamp `pvzrh-3.8.1` unconditionally → web battles would pollute the real almanac catalog).
2. Gate the global `metrics` bumps on game (web kills must not inflate PvZ dashboard counters).
3. Add `runs.game` (`EnsureColumn`) + stamp on `board.start`; expose in `RunItem` so the runs list shows the profile. (Charter's decisions.md amendment qualifies the "game profile is not a DB column" lock.) Fact source attribution derives by run join — no facts column.
4. Ingest honors an explicit player id for web `board.start` (today it stamps `current_player_id`, which mis-credits saves if the player switches mid-resolution).
5. Gate `EffectGrantSessionRecorder.NoteMatchLifecycle` and `UniqueActorService.ObserveEvents` to PvZ-game events — otherwise a web battle resolved during a live PvZ match **wipes the live match's effect grants**.
6. Suppress almanac type-XP awards (`ZombieSpawned`/zombie-kind) for `webrpg-1` runs, and keep demon species ids in a disjoint id space (≥10000) — web battles must not level PvZ almanac progression. (Related pre-existing fix, filed separately: the XP ledger dedupe is run-unscoped; defeat XP dedupes on the literal `"run"`.)
7. Exclude closed `webrpg-1` runs from capture KeepLastN accounting and per-run archive files — their events are reproducible from the logged seed, and expedition volume would otherwise evict real PvZ capture runs from hot.
8. FE live feeds (`#/log`, lawn view) filter by game so a millisecond-resolved battle burst doesn't render as capture traffic.
9. `SimEngine`/test trigger gains a game override (it currently stamps `pvzrh-3.8.1` always) so webrpg e2e runs are possible in SIM.

### Explicitly not in this module

No FE beyond the feed filter, no expedition timers, no player-facing API beyond a SIM/test trigger (`POST /api/test/web-match` under `FUSIONRPG_SIM=1`) — `expeditions` owns the player surface. No new event kinds without ask-first. No injector changes.

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
- **Pipeline e2e (SIM):** run a web match → assert run row (game `webrpg-1`), Activity facts, and XP ledger all appear with correct dedupe (Souls join this assertion once the soul-economy module exists — they are a projection this pipeline *enables*, not one that exists today); replayed correlation adds nothing; a live-PvZ-concurrent web match leaves the PvZ grant session untouched.
- **Golden-seed regression suite:** N seeds with expected result hashes in CI; any determinism break or balance change surfaces as a diff that must be consciously blessed with a version bump.

## Boundaries

- **Always:** engine purity (no I/O/clock); reuse existing event kinds; seed recorded on every run; correlation idempotency.
- **Ask first:** new event kinds; changes to wave/reward scaling (game balance); exposing battle APIs beyond the test trigger.
- **Never:** touch Funnel/EntityStatWriter (PvZ-mode-only paths); write ledgers outside the ingest projections; nondeterministic resolution; SQL outside Data.

## Success criteria

1. Three golden battles locked and deterministic. 2. Subsystem-integration tests prove shared math (element/status/trait effects visible in outcomes). 3. SIM e2e: a web match produces run + facts + XP + Souls with zero injector involvement. 4. Guards green; all existing suites green. 5. `runs` list in the FE shows web matches alongside PvZ runs with their profile id.
