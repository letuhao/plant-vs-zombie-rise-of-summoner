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
Refined 2026-08-21 into sliced tasks (detail table in [demon-standalone-plan.md](demon-standalone-plan.md) §Refinement; trait split: Funnel-routed stats/HP vs engine-native behaviors — behaviors are outside the FA vocabulary by design):

- [x] C2a: ActorHub-composed battle stats — per-actor derived snapshots (level + trait stat mods → 56-channel reads via `CombatDerivedReader`), hit/dodge/crit via the `crit` RNG stream. Verify: crit/dodge swing fixed battles. M
- [x] C2b: battle-local `EffectFunnel` + `BattleEffectSink` — HP mutations merge/cap/apply to battle state. Verify: regen heals across rounds; opposite-sign sums net; caps hold. M
- [x] C2c: battle-local `StatusRuntime` (catalog bootstrap, `ResistanceEvaluator` over derived profiles, clock = round × 1000 ms). Verify: DoT kills through a round; CC skips a turn; resistance blocks an apply. L
- [x] C2d: `TraitBattleCatalog` — ALL 14 trait defs (7 Funnel-routed: berserker/regenerator/soul-eater/critical-hunter/guardian/swift/immortal; 7 engine-native behaviors: coward/bloodthirsty/loyal/greedy/genius/void-touched/chaos-marked). Verify: 14-row table test + behavior scenarios (coward survives a wipe, loyal redirects damage). L
- [x] C3a: `BattleReportEmitter` — lean event vocabulary, `web:{matchKey}:{n}` ids, service-stamped monotonic `t`. Verify: emitted list validates against the lean profile. S
- [x] C3b: 3 golden battles (stomp/close/wipe) + 32-seed sweep hash — blessed after all of C2 (`BattleGoldenTests`, locked 2026-08-21). M
- [x] C4a: Data — `rpg_web_match_log` (per-player UNIQUE correlation, setup_json, seed, versions) + dedicated explicit-player single-transaction web insert + boot-sweep query. Verify: crash-window test (log without run row re-ingests on boot). M
- [x] C4b: Server — `WebMatchService` (log-before-ingest, resolver, SIM trigger `POST /api/test/web-match`) + FE log/lawn feed filter by game (deferred from B4). Verify: SIM e2e run+facts+XP+Souls, replay adds nothing. L
- [x] C4c: concurrency e2e — web match during a live PvZ match leaves grant session + ActiveBound actors untouched through the real service path. S

### Checkpoint C — match-source success criteria
- [x] All five spec success criteria green (2026-08-21): goldens locked (`BattleGoldenTests`), subsystem tests prove shared math, SIM e2e web match → run+Souls with zero injector, guards + suites green (2 foreign in-flight VFX-stream failures excluded), runs list shows profile badges. Suites: Core 1177 (58 battle) / Data 106 / Guard 40 / E2E 112 / CheatCore 40 / Launcher 128 / Vitest 200.

Commit draft for the owner (Wave C):
```
Web battle engine: composed stats, all-14 traits, goldens, WebMatchService
  BattleEngine C2: ActorHub-composed per-actor snapshots (56-channel reads via
  CombatDerivedReader, integer per-mille hit/dodge/crit on a `crit` stream),
  battle-local EffectFunnel + FA10 sink over engine state, battle-local
  StatusRuntime on the round clock (DoT/CC/resistance), TraitBattleCatalog with
  all 14 traits (7 Funnel-routed, 7 engine-native behaviors — outside the FA
  vocabulary by design). C3: BattleReportEmitter (lean profile, web:{key}:{n}
  ptrs, clockless), 3 golden battles + 32-seed sweep hash locked. C4:
  rpg_web_match_log (log-before-ingest, per-player correlation), dedicated
  explicit-player single-transaction web insert + boot sweep, WebMatchService
  with SIM trigger POST /api/test/web-match, FE live-feed game filter,
  concurrency e2e (live PvZ session untouched).
```

## WAVE D — expeditions (the announced ship)

- [x] D1 (written 2026-08-21, presented with the Wave C report): `docs/architecture/standalone/spec-expeditions.md` from the LOCKED anchors (2026-08-21): tiers 30m/4h/8h/20h, slots 2→5, no stamina, recall pro-rated at tick boundaries, seed-sealed at dispatch; **content = chain + events** (1–4 battles by tier + boss wave at 20h, interleaved seed-rolled event ticks: found-souls / wild-demon-met / injury); **rewards = all channels** (Souls + player XP via pipeline, specimen XP per battle won, wild-join chance origin `expedition`, fusion material stubs in a per-player inventory); specimen soft-lock ⇄ PvZ deploy; soul-ledger tail-trim lands in this wave (volume becomes real)
- [x] D2: Data — `rpg_expeditions` (Dispatched/Collected/Recalled, tier, squad_json, seed, due_utc) + `rpg_demon_materials` + soft-lock membership checks in expedition dispatch AND UniqueActor deploy. Verify: locked specimens refuse cross-mode deploy both ways. M
- [x] D3: Core — `ExpeditionResolver` (pure: tier + squad + seed → battle setups via tier-scaled WaveCatalog + event ticks via `loot` stream + rewards manifest incl. wild-join rolls + materials). Verify: determinism + tick pro-rating goldens. L
- [x] D4: Server — dispatch/collect/recall endpoints; collect = resolver → battles through `WebMatchService` → specimen XP + wild-join mints (origin `expedition`) + materials + Souls; correlation-idempotent; SIM force-due hook. Verify: SIM e2e full loop. L
- [x] D5: soul-ledger tail-trim + archive (the P4 deferral lands here; XP-ledger pattern). Verify: trim/rebuild test (spec success criterion 4). M
- [x] D6: FE — expeditions UI (dispatch from Active roster, tier pick, slot gating, live timers, collect reveal battle-by-battle + events, materials shelf; `#/expeditions` route + nav). Vitest: tick/pro-rate display math. L
- [x] D7: Checkpoint D sweep + docs sync (standalone map status, doc map, expeditions spec status header). All suites + guards green.

### Checkpoint D — the announced ship gate
- [x] PASSED 2026-08-21: dispatch→collect loop playable in FE against SIM; specimens soft-locked both ways while deployed; all reward channels land through the one economy (event Souls via `expedition` ledger reason, battle Souls/XP via the pipeline, specimen XP with genius multiplier, wild-join mints origin `expedition`, materials shelf); soul-ledger tail-trim live in compaction. Suites: Core 1187 / Data 116 / Guard 40 / E2E 117 / CheatCore 40 / Launcher 128 / Vitest 205; guards 4/4; FE build clean.

Commit draft for the owner (Wave D):
```
Expeditions: the first playable web loop (dispatch → timers → collect)
  Core: ExpeditionTierCatalog (30m/4h/8h/20h, slots 2→5), pure ExpeditionResolver
  (per-tick derived RNG streams — recall pro-rating exact by construction; chain
  battles + boss at 20h; found-souls/wild-demon-met/injury events; per-tier
  goldens locked), DemonMaterialCatalog stubs. Data: rpg_expeditions +
  soft-lock membership rows consulted by BOTH expedition dispatch and PvZ deploy,
  rpg_demon_materials inventory, exactly-once reward apply (one state-gated
  transaction: souls + materials + specimen XP + wild-join mints), soul-ledger
  tail-trim + archive in compaction (P4 deferral). Server: dispatch/collect/
  recall endpoints, battles through WebMatchService with deterministic
  exp:{id}:{n} correlations, SIM force-due hook. FE: #/expeditions (tier pick,
  slot gating, live timers, collect reveal, materials shelf).
```

## Five-axis review of Waves C+D (2026-08-21, four-perspective fan-out) — ALL FIXED

- [x] CRITICAL: web-match replay gate was check-then-append across two lock acquisitions — concurrent same-correlation requests could double-ingest (orphan run + double souls). Fixed: the atomic `AppendWebMatchLog` IS the replay gate in both `RunWebMatchAsync` and `RunPlannedMatchAsync` (Created=false ⇒ validate stored row, replay).
- [x] Important: expedition wire payloads leaked the sealed seed (outcome pre-reading + ulong→JS precision loss) → server-side projection, seed/correlation/squad_json never leave.
- [x] Important: post-commit hub sends unguarded in collect/dispatch (a SignalR fault burned the one-time reveal) → best-effort try/catch, matching `WebMatchService.BroadcastAsync`.
- [x] Important: greedy multiplier + wild-join trait rolls lived in Server → folded into `ExpeditionResolver` (manifest complete in Core, one stream namespace); forage tier golden consciously re-blessed (other three tiers byte-identical — the move touched nothing else).
- [x] Important: `Reset()` missed all four new tables → added; regression test.
- [x] Important: collect-retry elapsed could shrink under clock skew, stranding committed tail battles → elapsed floored at the furthest already-logged battle's tick (`BattleSchedule` + ≤5 log lookups).
- [x] Important: funnel lower-cases target keys vs case-sensitive actor map (mixed-case key = silently unhittable actor); MaxHp<1 spawned die-event-less corpses; duplicate keys shadowed actors → loud validation at `Resolve` entry; dispatch replay now validates tier+squad (house pattern).
- [x] Suggestions taken: saturating damage math (wrap → 1-damage inversion), materialized initiative rolls, `BuildSquad` cap+dedupe, invariant-culture defensive seed parse, run-link subquery guard, band-constant renames + WHY comments (shards-at-plan-time, XpMilli multiplier, swift/berserker labels, mutual-wipe tie-break), FE timer gated on active expeditions + `lockedIds` memo fix, spec vocabulary synced (stream names, matchKey scheme, manifest ownership).
- Deferred with notes: boot-sweep 100-row window can starve behind permanently version-skipped rows (page or quarantine when a version bump first ships); squad stats snapshot-at-dispatch (today the soft-lock freezes stats, but any future XP path touching locked specimens breaks lazy≡eager — revisit with fusion); log-store incremental membership (per-event O(CAP) signature rebuild on hit-heavy capture traffic); retired-specimen XP silently dropped at collect (acceptable: retirement forfeits earnings).
- Post-fix sweep: Core 1203 / Data 121 / E2E 120 / Guard 40 / Vitest 206, guards 4/4, FE build clean.

## WAVE F — demon-fusion (spec: docs/architecture/demons/spec-demon-fusion.md; detail table in demon-standalone-plan.md §Wave F)

- [x] F1: `StarPolicy` + `FusionCostTable` (pure Core). 9 tests. S
- [x] F2: `DemonRecipeCatalog` — deterministic, 10 recipes (4 rare/4 epic/2 legendary), unique orderless input pairs, eager-warmed. 5 tests. M
- [x] F3: `FusionRoller` — pick-one + seeded rest, promotion keeps existing traits; `fusion:*` streams. 5 tests. S
- [x] F4: Data schema (`star`/`promoted`, lineage, fusion log, discovery; Reset covers all three new tables) + Retired filtering. 2 tests. M
- [x] F5: `ExecuteFusion` star-merge mode, ONE transaction (atomic-append replay gate — review C1 lesson applied from day one). 5 tests. M
- [x] F6: recipe + promotion modes; discovery pays `DiscoveryDelta` once (dedupe `recipe:{id}`); promotion keeps traits, resets stars, once only. 3 tests. M
- [x] F7: `FusionEndpoints` preview/execute/recipes (silhouette projection — undiscovered recipe ids/outputs never on the wire) + SIM fixtures (`/api/test/seed-materials`, `/api/test/mint-demon`) + guarded hub pushes. 3 E2E tests. M
- [x] F8: `BuildSquad` star channel mods (flat per-mille of level stats, floored at `star`); battle goldens re-run byte-identical. 2 tests. S
- [x] F9: FE `#/fusion` lab (mode tabs, base/sacrifice trays, pick-one trait selector, have/need cost, recipe book silhouettes) + star pips on `#/demons` cards. 5 Vitest. L
- [x] F10: Checkpoint F sweep + E2E legendary chain (commons → rares → epics → legendary purely via the recipe graph) + docs sync.

### Checkpoint F — fusion success criteria
- [x] PASSED 2026-08-21: all six spec success criteria green; battle goldens untouched (re-run proof); suites Core 1222 / Data 131 / Guard 40 / E2E 126 / CheatCore 40 / Launcher 128 / Vitest 211; guards 4/4; FE build clean.

Commit draft for the owner (Wave F):
```
Demon fusion: star merges, capped promotion, discoverable recipes
  Core: StarPolicy (caps 3/4/5/5, n+1 sacrifices, +30‰/star) + FusionCostTable,
  DemonRecipeCatalog (deterministic over the species catalog: one recipe per
  summonable rare+, band-below inputs, unique orderless pairs, eager-warmed),
  FusionRoller (pick-one guaranteed + seeded rest on fusion:* streams;
  promotion keeps existing traits). Data: star/promoted columns, append-only
  rpg_demon_lineage, rpg_fusion_log (atomic-append replay gate) +
  rpg_fusion_discovery; ExecuteFusion = ONE gate-serialized transaction for
  all three modes (refusals write nothing, locked specimens unconsumable,
  consumption = phase Retired — never deleted); roster filters Retired.
  Server: /api/fusion preview/execute/recipes with silhouette projection
  (undiscovered outputs never on the wire), SIM fixtures for deterministic
  tests. Battles: BuildSquad star channel mods only — engine and goldens
  untouched (re-run proof). FE: #/fusion lab + star pips. E2E proves the
  commons→legendary chain purely via fusion.
```

## Five-axis review of Wave F (2026-08-21, three-perspective fan-out) — ALL FIXED

- [x] Important: untrimmed base id could pass its own trimmed id as a sacrifice and consume itself (trim asymmetry between actor reads and the is-base string compare) → ids normalized once at `ExecuteFusion` entry; Prove-It test.
- [x] Important: replay lost the discovery reveal (`NewlyDiscovered`/`DiscoverySouls` hardcoded false/0, against the spec's stored-outcome promise) → flags stored in `output_json`, rebuilt on replay; live-specimen replay semantics documented (idempotency, not a snapshot); Prove-It test.
- [x] Economy consistency (review S5, adjudicated): a species first obtained via fusion or an expedition wild-join now pays the same `species:{id}` discovery bonus a summon would — shared dedupe keeps it once-ever regardless of path; test locks the double-bonus first craft.
- [x] Suggestions taken: merge log records the real caller seed; recipe mode refuses a stray base id (`base.unexpected`); discovery ledger-append return guarded (no phantom Souls banners); shiny odds reference `SummonRoller.ShinyOneIn` + gate on the species' variant list; ONE trait-slot table (`FusionRoller.SlotsFor` is the authority); tx-consistent `now` in material spends; named private tuples; `ProjectCost` split into explicit overloads with the essence-breadcrumb decision documented (leak is deliberate — it tells the player which essence to farm); hub sends replay-guarded; preview refuses duplicate sacrifices; SIM mint guards traitless species.
- [x] FE: selecting a base strips it from the sacrifice tray (was a raw server-error bounce); `useSpeciesIndex()` extracted (third copy was the escalation point) and adopted by all three pages; star pips added to expedition squad picks (spec parity); preview effect rebuilt with a complete dependency list (picked trait deliberately not sent — server ignores it; documented).
- [x] Spec synced: structure line (FusionCostTable lives in StarPolicy.cs), essence breadcrumb + replayed-discovery + cross-path species-bonus paragraphs.
- Post-fix sweep: Core 1264 / Data 140 / Guard 40 / E2E 127 / Vitest 211, guards 4/4, FE build clean.

## WAVE P — patron-demon (spec: docs/architecture/demons/spec-patron-demon.md; injector scope, ends at a LIVE owner gate)

- [x] PT1: `PatronPolicy` — aura magnitudes + patron kill-earn shape as a running-total difference (cap exact at the boundary, no bonus overshoot). 10 tests. S
- [x] PT2: Data — `rpg_patron`, `SetPatron` (first free; switch spends 100, ledger dedupe = replay anchor; same-target = natural free replay), fusion guard `sacrifice.is-patron` (patron may LEAD merges), earn hook gated on a PK point lookup (unpatroned path byte-identical). 6 tests. M
- [x] PT3: Server — `/api/patron` get/set (409 insufficient), `PatronUpdated`, `patron.aura` command pushed on set AND on injector Hello (grant-snapshot rehydrate discipline), runtime state refreshed at boot/set/reset. M
- [x] PT4: Injector — investigation resolved the risk: the aura is BOTH a session grant (server upserts `patron:aura` into `EffectGrantSession` at each pvzrh board.start → SIM-visible, reconnect-rehydrated, lifecycle-cleared) and a compose-time overlay (`PatronAuraOverlay` in `InjectorCombatBridge.ResolveActor`, plant-side only, riding the side the element resolve already looks up — zero extra board scans; ‰→points at /10). `PatronSecondaryPlugin` (grant-only) freezes the match aura so mid-match switches stay inert; pinned prove-pack scenarios stay aura-free. Secondary-no-unity guard green. L
- [x] PT5: FE — `bus/patron.ts`, roster "Make patron" + patron badge with aura preview label, fusion trays disable + badge the patron. 2 Vitest. M
- [x] PT6: SIM sweep — 4 E2E (designation pricing, grant appears at board.start and leaves at board.end, 10-kill match pays 111, unset baseline pays exactly 110); injector builds clean against the game interop; suites Core 1303 / Data 146 / E2E 131 / Guard 40 / Vitest 213; guards 4/4. Registry tests updated for the third plugin; `/api/test/reset` clears the patron cache (process-state lesson).
- [ ] PT7: **LIVE gate (owner)** — see the handoff below.

### Checkpoint P
- [x] SIM half PASSED 2026-08-21 (criteria 1/2/3/5 + guards) — full-auto stops here.
- [ ] LIVE half — owner sign-off pending (criterion 4).

**LIVE handoff (PT7, owner):**
```powershell
$env:FUSIONRPG_GAME_DIR = "H:\Games\PVZ FUSION 3.8.1 FULL MOD TOOL"
.\scripts\deploy-play.ps1 -NoServer     # injector already built into the plugins dir
```
Then, with the server up and a patron designated in `#/demons`:
1. `/api/debug/effects/session-grants` shows `patron:aura` after board.start.
2. Plant damage vs a fixed target shifts by the aura (150‰ cap → up to +15 typed points).
3. board.end withdraws the grant (session view empties).
4. Perf probe window shows no new hot-path cost.
5. Switching patrons mid-match changes nothing until the next match.

Commit draft for the owner (Wave P):
```
Patron demon: element aura into live PvZ, soul-priced switching, kill bonus
  Core: PatronPolicy (rarityBase+10·star+level clamp 150; primary full /
  secondary half; kill-earn +1 per 10th earning kill as a running-total
  difference so the audited 50-soul cap is exact), PatronRuntimeState,
  grant-only PatronSecondaryPlugin + fx.patron_aura marker (passive, no
  actions). Data: rpg_patron + SetPatron one transaction (first free, switch
  spends 100 with ledger-dedupe replay), fusion guard sacrifice.is-patron,
  earn hook gated on a PK lookup — unpatroned earns byte-identical. Server:
  /api/patron get/set, patron.aura command on set + injector Hello, session
  grant upserted at each pvzrh board.start (reconnect rehydrate + SIM proof).
  Injector: PatronAuraOverlay applies plant-side typed channel points at
  compose time (no Unity writes, no extra board scans); patron.aura command
  cached at the client edge. FE: Make-patron roster action + aura badge;
  fusion trays protect the patron. LIVE checklist pending owner sign-off.
```

## Post-patron order: demon-contracts → demon-capture → world-events

## demon-fusion — owner decisions (locked 2026-08-21, all eight; spec builds from these)

1. **Identity — both, by mode:** same-species-band merges evolve the BASE demon (instanceId, nickname, XP, lineage survive; sacrifices consumed); cross-species RECIPE fusions consume all inputs and mint the recipe's output.
2. **Recipes — both layers:** rarity-band merges are the always-available floor; code-authored discoverable cross-species recipes (generated from the species catalog) are the ceiling — hidden until first success, codex-recorded with discovery Soul bonuses.
3. **Materials — cost + element gate:** every fusion costs rarity-matched shards; the result's element demands matching essences; Souls charge a base fee.
4. **Randomness — sure species, rolled extras:** output species guaranteed (recipes always work — crafting, the anti-gacha); traits/variant roll from a server seed, correlation-idempotent like summon pulls.
5. **Traits — pick one, roll rest:** player picks ONE guaranteed trait from any input; remaining slots (count by result rarity 1/2/2/3) seeded-roll from the combined input pool.
6. **Ceiling — recipes reach legendary:** deep recipes (epic bases + rare materials) mint legendaries as the deterministic path beside pity; capture-only species stay excluded everywhere.
7. **Merge floor — stars + capped promotion:** sacrifices raise the base's star rank (cap by rarity; per-mille combat channel bonuses per star, deploy-power later in PvZ); a max-star base may promote ONE rarity once, re-rolling trait slots upward.
8. **Patron demon — after fusion:** un-parked; slots between fusion and contracts (small injector scope; patron effect scales from stars/fused demons).

## WAVE G — demon-contracts (spec: docs/architecture/demons/spec-demon-contracts.md; detail table in demon-standalone-plan.md §Wave G)

Server + web only — no injector slice, no LIVE gate. Full-auto closes this wave.

- [x] G1: `ContractPolicy` + `LoyaltyRank` + `DemonPersonality` (pure Core) — rank bands/‰, personality percentages, upkeep by rarity, ritual, slot ladder, whole-day arithmetic. S
- [x] G2: Data schema (`rpg_demon_contracts`, `rpg_contract_state`, `Reset()` coverage) + one-shot deterministic migration auto-bind + mint-time auto-bind into a free slot + read model. M
- [x] G3: `SettleContracts` — 30-day clamp, per-day dedupe spend, insolvent-day decay floored at `DeployFloor`. M
- [x] G4: bind / release / ritual / buy-slot transactions (pact fee, correlation-idempotent, patron + on-expedition release guards, retirement frees the slot). M
- [x] G5: **risk slice** — the four fielding gates (`BuildSquad`, expedition dispatch, `TryBeginDeploy` for demon-profile specimens only, `SetPatron`) + `EnsureContractsReady`; full Data + E2E blast-radius sweep. M
- [x] G6: loyalty movement from battle/expedition results (+15 win / −10 loss, daily gain cap 60, personality gain %). M
- [x] G7: `BuildSquad` loyalty rank channel mods — Bound = +0‰ so battle + expedition goldens stay byte-identical (proof is the verify step). S
- [x] G8: `ContractEndpoints` (GET settles first; bind/release/ritual/slots POSTs; hub pushes) + SIM clock hook `/api/test/contracts/settle`. M
- [x] G9: FE — `bus/contracts.ts`, capacity header, contract badges + bind/release, ritual CTA, picker disable reasons. L
- [x] G10: Checkpoint G sweep + docs sync + commit draft.

### Checkpoint G — contracts success criteria
- [x] PASSED 2026-08-21. Capacity server-authoritative on all four fielding paths; a plain unique actor deploys untouched.
- [x] Settlement idempotent (same day twice = one charge), 30-day clamped with the remainder forgiven; an insolvent day decays and writes no ledger row.
- [x] Decay never crosses `DeployFloor`; only defeats do (11 losses proven end-to-end through real battles).
- [x] Migration auto-binds best-first, deterministically, exactly once — plus mint-time binding into a free slot (plan decision 1).
- [x] Battle + expedition goldens byte-identical (Bound = +0‰, re-run proof); `LoyaltyChannelMods` proven non-trivial at Sworn/Devoted.
- [x] Suites Core 1456 / Data 183 / E2E 137 / Guard 40 / Launcher 128 / Vitest 220; guards 4/4; FE build clean.
- Foreign reds excluded (other streams, mid-flight): CheatCore `lab-shield-bar` unknown step `debug.shield.demo-all` (shield/VFX stream); Core/World test file compiled mid-edit twice during the wave (world stream) — green on re-run.

**Open with the owner (built as planned, reversible):** the spec locks auto-bind for *migration*; the build also binds at *mint time when a slot is free* (plan §Wave G decision 1) — free, no pact fee, and it raises the daily tribute silently. One-line revert in `MintDemonUnlocked` if you want the stricter rule.

Commit draft for the owner (Wave G):
```
Demon contracts: binding slots, loyalty, daily tribute

  Core: ContractPolicy (rank bands 200/400/600/800 with +0/15/35/60 per-mille own-channel
  bonuses, five personalities scaling gain/decay/upkeep, rarity-scaled daily upkeep, ritual
  and slot-price ladders, whole-UTC-day arithmetic clamped to 30). Data: rpg_demon_contracts
  + rpg_contract_state; lazy day-quantised SettleContracts (one dedupe-keyed ledger row per
  UTC day, or decay when the balance cannot cover it, floored so time never costs a demon its
  deployability); one-shot best-first migration plus mint-time binding into a free slot;
  bind/release/ritual/buy-slot each one transaction, refusals write nothing; consumption frees
  the slot. Gates: web squads, expedition dispatch, PvZ deploy (demon-profile specimens only)
  and patron designation refuse unbound/insubordinate demons by name. Results move loyalty —
  +15 a win under a 60/day window, -10 a loss, the only path under the floor. Server:
  /api/contracts get/bind/release/ritual/slots-buy + a SIM clock hook. FE: capacity header,
  contract badges, ritual CTA, gated pickers. Battle and expedition goldens byte-identical —
  a fresh contract sits in the zero-bonus band by design.
```

## Post-Wave-G test pass (2026-08-21) — regression locks for what held only by construction

- [x] Settlement: solvency is decided **per day**, not once for the span (day 1 pays, days 2–3 decay in the same call).
- [x] Settlement: a day one Soul short is **not** partially paid — all-or-nothing, remainder untouched.
- [x] Settlement: consecutive settles never bill a day twice (settle 1 day, then ask for 3 → 2 more).
- [x] Churn guard: the pact fee is forgiven only within the same UTC day — a next-day re-sign pays again (without this the guard silently expires).
- [x] Isolation: contracts never reach across players — results, bind, and release all refuse another summoner's demon.
- [x] A consumed (Retired) demon cannot be re-contracted (`specimen.missing`); its slot stays reclaimable.
- [x] Slot ladder climbs all 36 purchases to the 48 ceiling, then refuses `capacity.max` writing nothing (199,800 Souls of ladder proven exactly).
- [x] Migration tie-break: level outranks seniority when rarity and stars tie (test asserts its own premise — that XP moved the level).
- [x] E2E: a squadless match **skips** unbound demons instead of refusing, and credits nothing to the demon that sat it out.
- [x] E2E: an expedition moves the loyalty of everyone who went, and both members share the trip's single verdict.

New: 8 Data + 2 E2E. Suites after: Core 1474 / Data 191 / E2E 139 / Guard 40; guards 4/4.

## Five-axis review of Wave G (2026-08-21) — findings + fixes

- [x] **Important (correctness):** settlement conflated "the ledger refused this charge" with "the player could not pay" — a day already on the books fell through to the decay branch, eroding loyalty on a day that was *paid for*. Currently only reachable if a dedupe row outlives its stamp, but silent and player-punishing. Split the balance check from the append result; an already-present key now counts as settled. Prove-It test verified RED against the old code first (`A_day_already_on_the_ledger_is_paid_not_unpaid`).
- [x] **Suggestion (robustness):** `DateTimeOffset.Parse` of round-trip stamps used the ambient culture — a non-Gregorian calendar parses those into a different date, and one of the two sites is on the mint path where a throw would break summoning. Both now pass `InvariantCulture` + `RoundtripKind`. Note: `ExpeditionEndpoints.cs:80-81` has the same pattern from Wave D — pre-existing, left alone, worth a sweep someday.
- [x] **Suggestion (readability):** hoisted the daily `due` out of the settle loop (it cannot change within a settle) and folded the `bound.Count == 0` check into the loop condition; dropped a `due <= 0` guard that could never fire.
- [x] **Documented, not changed (architecture):** the loyalty credit sits OUTSIDE the exactly-once envelope on both result paths (web match and expedition collect). A crash between ingest/rewards and the credit loses ±15 loyalty and no sweep replaces it; a retry can never double-credit. Accepted trade, now stated in both files rather than left as an accident.
- Reviewed and found sound: refusal-rollback semantics (a refused bind discards its own settlement, which the next call redoes — no double charge, no lost money); no nested store connections inside a held gate (every gate uses `*Unlocked` helpers); cross-player isolation enforced at the store, not the endpoint; `playerId`-in-body matches the existing PatronEndpoints pattern and the loopback threat model.
- Post-fix suites: Core 1483 / Data 202 / E2E 139 mine-green / Guard 40 / Vitest 220; guards 4/4. Foreign reds excluded: 6 × `WorldE2ETests` (world stream, landed mid-session), CheatCore `lab-shield-bar` (shield/VFX stream).
