# Tasks: Demon RPG + standalone-first

Plan: [demon-standalone-plan.md](demon-standalone-plan.md). Test: `dotnet test tests\FusionRpg.Core.Tests` (+ Data/Guard/E2E per task). Full-auto; check off as they land.

## P1 standalone-charter
- [x] T1: `webrpg-1` constant in `RpgConstants` + unit test; NOT in game-profiles.json
  - Verify: Core tests green. Files: `src/FusionRpg.Contracts/Dtos.cs`, test. S
- [x] T2: decisions.md amendment (standalone-first, 4 guardrailed roles, gameless-first, runs.game qualification) + software-architecture.md §1/§3 update
  - Verify: docs consistent with charter spec. Files: 2 docs. S

## P2 element-extension — DONE (826/826 Core, guards green)
- [x] T3: `ElementRoster` constant + extend `ElementTypeId` (light, dark); strict name→id parse rejecting digits (also fixed `OverlayCombatCalculator.ParseComponents` numeric-parse hole)
- [x] T4: `ElementRingMatrix` light/dark mutual-counter pairs; roster-generated 6×6 golden matrix + dual-type composition + byte-identical 4-element regression (`ElementExtensionTests`)
- [x] T5: `DerivedStatChannels` 40→56 (generated family × roster); all 8 `CombatDerivedReader` maps; `ElementFxPalette` light (255,232,120) / dark (150,90,220) + roster-driven `Concrete()`; exhaustiveness walk test; Count test → 56
- [x] T6: element-hub-ssot.md roster/matrix/locks updated + decisions.md Element Hub row amended

## P3 demon-core — DONE (835 Core / 74 Data / 3 E2E at completion)
- [x] T7: Core catalogs — `DemonRarity`+`DemonAcquisition`+`DemonDeployMode`, `DemonTraitCatalog` (14 traits incl. void-touched/chaos-marked), `DemonSpeciesCatalog` + validation
- [x] T8: generator — `Core/Demons/Generation/DemonSpeciesGenerator` (pure, FNV-seeded) + `tools/DemonCatalogGen` (reads via DAL); ran against captured DB (339 type rows) → committed 24-species roster (2 legendary hypno bosses, 3 light, 3 dark, 2 capture-only rares); determinism + validation tests
- [x] T9: Data — `rpg_demon_profiles` + `rpg_demon_codex` (MAX-state lattice) + atomic `MintDemon` (one transaction) + nickname/lock (revision bumps); 6 Data tests
- [x] T10: Server — `/api/demons/catalog|{playerId}|codex`, nickname/lock endpoints, `DemonsUpdated`; 3 E2E tests

## P4 soul-economy — DONE (83 Data / 2 E2E; tail-trim deferred, see note)
- [x] T11: `SoulEarnPolicy` v2 + golden tables (incl. stall-defeat-never-beats-win assert)
- [x] T12: Data — ledger + watermarked balances; earns inside the fact transaction (result normalized like runs projection); `TrySpendSouls` (per-player correlation replay, refusals write nothing); `AwardSouls` idempotent
  - Deferred: soul-ledger tail trim/archive (≤55 rows/match — volume tiny; wire into compaction when expedition volume makes it real)
- [x] T13: Server — `/api/souls/{playerId}` + `/ledger` + SIM seed route; E2E: sim match earns policy-exact 103, seed reads back

## P5 match-source-core
- [x] T14: owned PRNG — `Core/Battle/SeededRng.cs` (xoshiro256** + splitmix64 streams, rejection-sampled bounds, per-mille integer rolls); golden sequence locked, stream-independence + distribution tests
- [x] T19 (pulled first): fixed latent XP-ledger dedupe bug — run-scoped ledger dedupe (`{runId}:{factDedupe}`); regression tests prove second defeat + reused-ptr kills both award
Remaining match-source tasks moved into **Wave B/C** below (replanned 2026-08-21 — summoning has zero pipeline dependencies, so the V1 gate builds first; see [demon-standalone-plan.md](demon-standalone-plan.md)).

## WAVE A — demon-summoning — DONE (Checkpoint A passed 2026-08-21: 888 Core / 91 Data / 107 E2E / 40 Guard / 204 Vitest, guards green)

Commit draft for the owner:
```
Demon summoning V1: banners + pity v2 + atomic pulls + #/demons FE
  Two banners (standard 100/900, rotating element-focus 120/1080), epic hard
  pity 25 / legendary soft 41 hard 55 with visible counters, one-transaction
  pulls (spend+mint+codex+discovery+pity+log, per-player correlation replay),
  Active-24/Reserve roster with lock+nickname, codex with discovery rewards.
```

- [x] A1: `SummonBannerCatalog` (standard-rift 100/900 + rotating element-focus 120/1,080, 3× element weight) + `SummonRoller` (pure; takes pity-counter state in, returns results + new counters; rarity 74/20/5/1, epic hard pity 25, legendary soft 41 +6%/pull hard 55; variant `shiny` 1/64; traits 1–3 by rarity; `gacha` SeededRng stream)
  - Acceptance: roller is pure/deterministic; pity counters cross-banner; guarantee slots roll last.
  - Verify: fixed-seed distribution goldens; pity table tests (24 pulls no epic → 25th is; 54 no legendary → 55th is; counters reset on hit). Files: `Core/Demons/SummonBannerCatalog.cs`, `SummonRoller.cs`, tests. M
- [x] A2: Data — `rpg_summon_log` (per-player UNIQUE correlation, results_json, rng_seed) + `rpg_summon_pity` row + `ExecuteSummon` ONE gate-serialized transaction: replay-check → spend → mints → codex (+ discovery `AwardSouls`) → pity update → log
  - Acceptance: replay returns stored results and validates stored (banner, count) vs request; refusal writes nothing; discovery rewards land in the same transaction.
  - Verify: forced mid-sequence failure ⇒ zero rows in ledger/actors/profiles/codex/pity/log; replay-identical test; overdraft test. Files: `RpgStore.Summons.cs`, schema block, tests. M
- [x] A3: Server `POST /api/demons/summon` (+ pity in `GET /api/demons/{playerId}`), pushes `SoulsUpdated` + `DemonsUpdated`; SIM e2e: seed → ×1 and ×10 pulls → roster/codex/balance/counters exact → replayed request changes nothing
  - Files: `DemonEndpoints.cs`, E2E test. S
- [x] A4: FE `#/demons` — bus queries/mutations (`lib/bus`), Souls header, Summon panel (×1/×10, disabled below cost, visible pity counters), reveal flow (rarity-ordered, nickname/lock inline), Active-24/Reserve species-stacked roster, Codex grid (silhouette `???`, discovery rewards shown); route + nav registration; Vitest for pity display + reserve stacking logic
  - Files: `web/fusion-rpg-web/src/features/demons/*`, routes, nav. L

### Checkpoint A — V1 internal gate
- [x] Full SIM demo loop offline (earn → pull → collect → manage); all suites + guards green; commit draft handed to owner.

## WAVE B — pipeline adaptations — DONE (Checkpoint B passed: 888/95/108/40/40 + guards; FE log filter deferred into C4 where web events first exist; explicit-player web board.start lands with C4's dedicated insert per plan)

- [x] B1: `runs.game` — EnsureColumn + stamp from envelope on `board.start` + `RunItem.Game` + runs list FE shows profile badge
  - Verify: pvzrh run stamps `pvzrh-*`; synthetic `webrpg-1` board.start stamps `webrpg-1`; existing runs tests green. Files: `RpgStore.cs`, `Dtos.cs`, runs FE. S
- [x] B2: pollution guards — thread envelope `game` into `UpsertTypeFromSpawn`/`UpsertType`/`BumpTypeKilled`; gate `BumpFromKindUnlocked` metrics to pvzrh games
  - Verify: webrpg zombie.spawn/die batch leaves pvzrh `types` rows + all metrics byte-identical (regression test). Files: `RpgStore.cs`, tests. M
- [x] B3: concurrency guards — explicit-player web `board.start` (ingest honors envelope player for `source=web`); gate `EffectGrantSessionRecorder.NoteMatchLifecycle` + `UniqueActorService.ObserveEvents` to pvzrh events
  - Verify: web batch during a live pvzrh match leaves the grant session + ActiveBound actors untouched (the audit's wipe scenario as a test). Files: `EventIngest.cs`, `RpgStore.cs`, `RpgStore.UniqueActors.cs`, tests. M
- [x] B4: scope guards — suppress zombie-kind almanac XP for `webrpg-1` runs; exempt closed webrpg runs from capture KeepLastN + archive files; SimEngine game override for webrpg e2e; FE `#/log`+lawn feed filter by game
  - Verify: web kills award no zombie-type XP; 60 closed webrpg runs evict zero pvzrh runs and write zero archive files. Files: `RpgStore.Progression.cs`, `RpgStore.Compaction.cs`, `SimEngine.cs`, FE log filter. M

### Checkpoint B
- [x] Pollution regression suite green + full E2E suite green (minus foreign pipeline-v2 failure if still present).

## WAVE C — BattleEngine + WebMatchService

- [x] C1: engine skeleton — `BattleSetup`/`BattleReport`/`BattleActorState` models, `WaveCatalog` (code-authored waves over demon species + pvz types), round loop with `RoundDurationMs = 1000` and locked order (status ticks → initiative attacks → death triggers → round end), integer-only state, `(engineVersion, rngAlgoVersion, rulesetVersion, seed)` stamps
  - Verify: same setup+seed ⇒ byte-identical serialized report (before any subsystem lands). Files: `Core/Battle/*`, tests. M
- [ ] C2: subsystem integration — squads composed via ActorHub derived profiles (traits → EffectBag grants via battle-local `EffectFunnel` + battle `IEffectActionSink`), damage via `OverlayCombatMath` + ElementHub, statuses via `StatusRuntime` with injected clock
  - Verify: element matchup swings a fixed battle; DoT trait kills through a round; CC skips a turn; resistance blocks an apply — each vs the shipped math. Files: `Core/Battle/*`, tests. L
- [ ] C3: report emission — lean event vocabulary (`board.start`→spawns→dies→`match.result`→`board.end`), `web:{matchKey}:{n}` actor ids, no wall-clock in engine; 3 golden battles (stomp/close/wipe) with locked result hashes
  - Verify: golden hashes in CI; event list validates against the lean profile. Files: `Core/Battle/BattleReportEmitter.cs`, goldens. M
- [ ] C4: `WebMatchService` — `rpg_web_match_log` (per-player UNIQUE correlation, setup_json, seed, versions) written before ingest; dedicated single-transaction `InsertEvents` (never the shared channel); monotonic `t` stamping; boot sweep re-ingests logged matches with no run row; SIM trigger `POST /api/test/web-match`
  - Verify: SIM e2e — run row (`game=webrpg-1`) + facts + XP + Souls appear, replay adds nothing, crash-window (log without run) recovers on boot, concurrent-PvZ grant session untouched. Files: `Server/WebMatchService.cs`, `RpgStore.WebMatches.cs`, E2E. L

### Checkpoint C — match-source success criteria
- [ ] All five spec success criteria green; commit draft handed to owner.

## WAVE D — expeditions (the announced ship)

- [ ] D1: write `docs/architecture/standalone/spec-expeditions.md` from the map anchors (tiers 30m/4h/8h/20h, slots 2→5, no stamina, recall pro-rated to ticks, outcome sealed at dispatch via recorded seed, specimen soft-lock ⇄ PvZ deploy, rewards through WebMatchService) — present per the spec gate, then break into D2+ tasks
- [ ] D2+: dispatch/collect API + timers → soft-lock integration → FE screens → Checkpoint D (playable loop)
