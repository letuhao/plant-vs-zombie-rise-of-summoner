# Tasks: Creature RPG + standalone-first

Plan: [creature-standalone-plan.md](creature-standalone-plan.md). Test: `dotnet test tests\FusionRpg.Core.Tests` (+ Data/Guard/E2E per task). Full-auto; check off as they land.

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

## P3 creature-core — DONE (835 Core / 74 Data / 3 E2E at completion)
- [x] T7: Core catalogs — `CreatureRarity`+`CreatureAcquisition`+`CreatureDeployMode`, `CreatureTraitCatalog` (14 traits incl. void-touched/chaos-marked), `CreatureSpeciesCatalog` + validation
- [x] T8: generator — `Core/Creatures/Generation/CreatureSpeciesGenerator` (pure, FNV-seeded) + `tools/CreatureCatalogGen` (reads via DAL); ran against captured DB (339 type rows) → committed 24-species roster (2 legendary hypno bosses, 3 light, 3 dark, 2 capture-only rares); determinism + validation tests
- [x] T9: Data — `rpg_creature_profiles` + `rpg_creature_codex` (MAX-state lattice) + atomic `MintCreature` (one transaction) + nickname/lock (revision bumps); 6 Data tests
- [x] T10: Server — `/api/creatures/catalog|{playerId}|codex`, nickname/lock endpoints, `CreaturesUpdated`; 3 E2E tests

## P4 soul-economy — DONE (83 Data / 2 E2E; tail-trim deferred, see note)
- [x] T11: `SoulEarnPolicy` v2 + golden tables (incl. stall-defeat-never-beats-win assert)
- [x] T12: Data — ledger + watermarked balances; earns inside the fact transaction (result normalized like runs projection); `TrySpendSouls` (per-player correlation replay, refusals write nothing); `AwardSouls` idempotent
  - Deferred: soul-ledger tail trim/archive (≤55 rows/match — volume tiny; wire into compaction when expedition volume makes it real)
- [x] T13: Server — `/api/souls/{playerId}` + `/ledger` + SIM seed route; E2E: sim match earns policy-exact 103, seed reads back

## P5 match-source-core
- [x] T14: owned PRNG — `Core/Battle/SeededRng.cs` (xoshiro256** + splitmix64 streams, rejection-sampled bounds, per-mille integer rolls); golden sequence locked, stream-independence + distribution tests
- [x] T19 (pulled first): fixed latent XP-ledger dedupe bug — run-scoped ledger dedupe (`{runId}:{factDedupe}`); regression tests prove second defeat + reused-ptr kills both award
Remaining match-source tasks moved into **Wave B/C** below (replanned 2026-08-21 — summoning has zero pipeline dependencies, so the V1 gate builds first; see [creature-standalone-plan.md](creature-standalone-plan.md)).

## WAVE A — creature-summoning — DONE (Checkpoint A passed 2026-08-21: 888 Core / 91 Data / 107 E2E / 40 Guard / 204 Vitest, guards green)

Commit draft for the owner:
```
Creature summoning V1: banners + pity v2 + atomic pulls + #/creatures FE
  Two banners (standard 100/900, rotating element-focus 120/1080), epic hard
  pity 25 / legendary soft 41 hard 55 with visible counters, one-transaction
  pulls (spend+mint+codex+discovery+pity+log, per-player correlation replay),
  Active-24/Reserve roster with lock+nickname, codex with discovery rewards.
```

- [x] A1: `SummonBannerCatalog` (standard-rift 100/900 + rotating element-focus 120/1,080, 3× element weight) + `SummonRoller` (pure; takes pity-counter state in, returns results + new counters; rarity 74/20/5/1, epic hard pity 25, legendary soft 41 +6%/pull hard 55; variant `shiny` 1/64; traits 1–3 by rarity; `gacha` SeededRng stream)
  - Acceptance: roller is pure/deterministic; pity counters cross-banner; guarantee slots roll last.
  - Verify: fixed-seed distribution goldens; pity table tests (24 pulls no epic → 25th is; 54 no legendary → 55th is; counters reset on hit). Files: `Core/Creatures/SummonBannerCatalog.cs`, `SummonRoller.cs`, tests. M
- [x] A2: Data — `rpg_summon_log` (per-player UNIQUE correlation, results_json, rng_seed) + `rpg_summon_pity` row + `ExecuteSummon` ONE gate-serialized transaction: replay-check → spend → mints → codex (+ discovery `AwardSouls`) → pity update → log
  - Acceptance: replay returns stored results and validates stored (banner, count) vs request; refusal writes nothing; discovery rewards land in the same transaction.
  - Verify: forced mid-sequence failure ⇒ zero rows in ledger/actors/profiles/codex/pity/log; replay-identical test; overdraft test. Files: `RpgStore.Summons.cs`, schema block, tests. M
- [x] A3: Server `POST /api/creatures/summon` (+ pity in `GET /api/creatures/{playerId}`), pushes `SoulsUpdated` + `CreaturesUpdated`; SIM e2e: seed → ×1 and ×10 pulls → roster/codex/balance/counters exact → replayed request changes nothing
  - Files: `CreatureEndpoints.cs`, E2E test. S
- [x] A4: FE `#/creatures` — bus queries/mutations (`lib/bus`), Souls header, Summon panel (×1/×10, disabled below cost, visible pity counters), reveal flow (rarity-ordered, nickname/lock inline), Active-24/Reserve species-stacked roster, Codex grid (silhouette `???`, discovery rewards shown); route + nav registration; Vitest for pity display + reserve stacking logic
  - Files: `web/fusion-rpg-web/src/features/creatures/*`, routes, nav. L

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

- [x] C1: engine skeleton — `BattleSetup`/`BattleReport`/`BattleActorState` models, `WaveCatalog` (code-authored waves over creature species + pvz types), round loop with `RoundDurationMs = 1000` and locked order (status ticks → initiative attacks → death triggers → round end), integer-only state, `(engineVersion, rngAlgoVersion, rulesetVersion, seed)` stamps
  - Verify: same setup+seed ⇒ byte-identical serialized report (before any subsystem lands). Files: `Core/Battle/*`, tests. M
Refined 2026-08-21 into sliced tasks (detail table in [creature-standalone-plan.md](creature-standalone-plan.md) §Refinement; trait split: Funnel-routed stats/HP vs engine-native behaviors — behaviors are outside the FA vocabulary by design):

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

- [x] D1 (written 2026-08-21, presented with the Wave C report): `docs/architecture/standalone/spec-expeditions.md` from the LOCKED anchors (2026-08-21): tiers 30m/4h/8h/20h, slots 2→5, no stamina, recall pro-rated at tick boundaries, seed-sealed at dispatch; **content = chain + events** (1–4 battles by tier + boss wave at 20h, interleaved seed-rolled event ticks: found-souls / wild-creature-met / injury); **rewards = all channels** (Souls + player XP via pipeline, specimen XP per battle won, wild-join chance origin `expedition`, fusion material stubs in a per-player inventory); specimen soft-lock ⇄ PvZ deploy; soul-ledger tail-trim lands in this wave (volume becomes real)
- [x] D2: Data — `rpg_expeditions` (Dispatched/Collected/Recalled, tier, squad_json, seed, due_utc) + `rpg_creature_materials` + soft-lock membership checks in expedition dispatch AND UniqueActor deploy. Verify: locked specimens refuse cross-mode deploy both ways. M
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
  battles + boss at 20h; found-souls/wild-creature-met/injury events; per-tier
  goldens locked), CreatureMaterialCatalog stubs. Data: rpg_expeditions +
  soft-lock membership rows consulted by BOTH expedition dispatch and PvZ deploy,
  rpg_creature_materials inventory, exactly-once reward apply (one state-gated
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

## WAVE F — creature-fusion (spec: docs/architecture/creatures/spec-creature-fusion.md; detail table in creature-standalone-plan.md §Wave F)

- [x] F1: `StarPolicy` + `FusionCostTable` (pure Core). 9 tests. S
- [x] F2: `CreatureRecipeCatalog` — deterministic, 10 recipes (4 rare/4 epic/2 legendary), unique orderless input pairs, eager-warmed. 5 tests. M
- [x] F3: `FusionRoller` — pick-one + seeded rest, promotion keeps existing traits; `fusion:*` streams. 5 tests. S
- [x] F4: Data schema (`star`/`promoted`, lineage, fusion log, discovery; Reset covers all three new tables) + Retired filtering. 2 tests. M
- [x] F5: `ExecuteFusion` star-merge mode, ONE transaction (atomic-append replay gate — review C1 lesson applied from day one). 5 tests. M
- [x] F6: recipe + promotion modes; discovery pays `DiscoveryDelta` once (dedupe `recipe:{id}`); promotion keeps traits, resets stars, once only. 3 tests. M
- [x] F7: `FusionEndpoints` preview/execute/recipes (silhouette projection — undiscovered recipe ids/outputs never on the wire) + SIM fixtures (`/api/test/seed-materials`, `/api/test/mint-creature`) + guarded hub pushes. 3 E2E tests. M
- [x] F8: `BuildSquad` star channel mods (flat per-mille of level stats, floored at `star`); battle goldens re-run byte-identical. 2 tests. S
- [x] F9: FE `#/fusion` lab (mode tabs, base/sacrifice trays, pick-one trait selector, have/need cost, recipe book silhouettes) + star pips on `#/creatures` cards. 5 Vitest. L
- [x] F10: Checkpoint F sweep + E2E legendary chain (commons → rares → epics → legendary purely via the recipe graph) + docs sync.

### Checkpoint F — fusion success criteria
- [x] PASSED 2026-08-21: all six spec success criteria green; battle goldens untouched (re-run proof); suites Core 1222 / Data 131 / Guard 40 / E2E 126 / CheatCore 40 / Launcher 128 / Vitest 211; guards 4/4; FE build clean.

Commit draft for the owner (Wave F):
```
Creature fusion: star merges, capped promotion, discoverable recipes
  Core: StarPolicy (caps 3/4/5/5, n+1 sacrifices, +30‰/star) + FusionCostTable,
  CreatureRecipeCatalog (deterministic over the species catalog: one recipe per
  summonable rare+, band-below inputs, unique orderless pairs, eager-warmed),
  FusionRoller (pick-one guaranteed + seeded rest on fusion:* streams;
  promotion keeps existing traits). Data: star/promoted columns, append-only
  rpg_creature_lineage, rpg_fusion_log (atomic-append replay gate) +
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

## WAVE F2 — fusion trait/action inheritance (owner decision 2026-09-07, `creature-mechanism-gaps-ideal.md` §3.4)

⏸ **Deferred — future work, fully investigated, not spec-ready.** `docs/architecture/creature-scope-ideal.md`
§5-9 explores the broader "creature scope" idea this wave grew out of — general-creature (siege/world-map
legion) progression, troop-stack identity + per-source upgrade containers, stack experience, a
near-death survival→promotion mechanic, and a Survivor Title achievement system spanning world
events/Delve events/quests. Owner decision 2026-09-07: *"troop management, achievement is big
feature, we will turn back to fusion and creature scope... and complete creature species leftover to make
gameplayable first."* Resume there when picking this back up — every section names real built/wiring-
gap/real-gap findings with file:line, so it starts from evidence, not memory.

Wave F's own shipped locked decision 5 ("pick ONE guaranteed trait... roll rest") already exists —
but it picks from the old, flavour-only `TraitPool` string tags, never from the mechanically-real
`species-passive.{speciesId}` atom containers `SpeciesMaterialiser` rolls per player
(`src/FusionRpg.Core/Creatures/Materialise/SpeciesMaterialiser.cs:35-70`). This wave moves inheritance
onto real atoms. Owner decisions locked going in (not re-litigated here): **full `slotsByRarity`
(1/2/3) as the pick ceiling**, and **cost keyed by the picked trait's own source rarity**, reusing the
existing `recipeCost`-shaped table indexed differently, not a new curve.

Dependency order: F2.1 (producer capability) and F2.2 (read a specimen's own roll) are independent and
parallel-safe. F2.3 (cost table) is independent of both. F2.4 needs all three. F2.5 needs F2.4.

- [x] **F2.1: `InstanceProducer.Compose` accepts forced pool picks — DONE 2026-09-07** · **M**
  - **Design corrected 2026-09-07, before build resumed on it.** The original framing ("reduce
    `pool_rolls` by the forced count") doesn't match the real shape: `ContainerRow` tracks two
    separate roll budgets, `PrefixRolls` and `SuffixRolls` (`ContainerRow.cs:149,152`), not one
    unified count, and `Resolver.Resolve` draws each pass against its own eligible `AffixClass`
    (`Resolver.cs:154-213`). Forced picks are already-resolved atoms lifted from a PARENT specimen's
    own roll (F2.2) — they never went through an affix draw *for this instance*, so they don't
    inherently belong to "prefix" or "suffix" the way a fresh draw does.
  - **Resolution: reduce the roll budget, not the affix bookkeeping — the same operation
    `VariantShift.ShiftPrefixRolls`/`ShiftSuffixRolls` (`VariantShift.cs:49,52`) already performs for
    a completely different reason (shiny/corrupted variants), reused here rather than invented.**
    `Compose` clones the container via `container with { PrefixRolls = ..., SuffixRolls = ... }` (a
    plain `record`, confirmed `init`-only and clonable) before handing it to `Resolver.Resolve`,
    shrinking **`SuffixRolls` first, then `PrefixRolls`**, by exactly the forced-pick count, both
    clamped at 0. This is a named, documented default (no existing rule dictates which side a forced
    pick should "cost," since it never drew from either) rather than an invented mechanism — the
    total roll count still shrinks by exactly the forced count either way, and this repo's own
    established practice is a stated, deterministic default over blocking on a question play-testing
    would answer better than a plan can. If the forced-pick count exceeds `PrefixRolls + SuffixRolls`
    combined, `Compose` refuses (`AtomRejection`, named) rather than silently allowing more total
    atoms than the rarity's own tier permits.
  - Acceptance:
    - [x] A legal forced pick (a real pool member for this container) appears verbatim in the
          resulting `InstanceRow`, and the cloned container's `SuffixRolls` (then `PrefixRolls`) is
          reduced by exactly one per forced pick, clamped at 0 — asserted directly, not inferred from
          the output count alone.
    - [x] An illegal forced pick (not a real pool member for this container) is rejected by name;
          nothing is written, matching every other producer refusal's own contract.
    - [x] A forced-pick count exceeding `PrefixRolls + SuffixRolls` combined is refused by name,
          before any roll happens.
    - [x] Remaining slots (the SHRUNKEN `PrefixRolls`/`SuffixRolls`) still roll normally through
          `Resolver.Resolve`, on a container whose OTHER fields (pool, tiers, groups) are byte-identical
          to the original — only the two roll counts differ for this one `Compose` call.
    - [x] Zero forced picks reproduces today's exact output byte-for-byte — no regression to the
          existing species-materialise path, which calls `Compose` with none.
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter InstanceProducer`
  - Files: `src/FusionRpg.Core/Effects/Atoms/InstanceProducer.cs`, its tests.
  - ### ✅ Evidence — **DONE 2026-09-07**
    - New `ForcedPoolPick(string AffixId, IReadOnlyList<InstanceAtomRow> Atoms)` (`InstanceProducer.cs`)
      carries an already-resolved pick verbatim; `Compose` gained `IReadOnlyList<ForcedPoolPick>?
      forcedPicks = null`. Validates pool membership per pick and the total-budget cap BEFORE any
      roll, both refused via `AtomRejection.ContentRule` under a newly-registered `"fusion-inherit"`
      namespace (`fusion-inherit.not-a-pool-member`, `fusion-inherit.exceeds-roll-budget`) — one code
      with a namespaced payload (item-ideal.md §2b.1), never a 34th entry in the closed
      `AtomRejectionReason` list.
    - Shrinks `SuffixRolls` first then `PrefixRolls` via `container with { ... }` on a CLONE passed
      only to `Resolver.Resolve` — the container `ContainerValidator.Validate` checks stays the
      original, unshrunk authored row. Forced atoms are spliced in via `a with { Seq = nextSeq }`,
      preserving every other field (including `IdentityDigestHex`) verbatim.
    - **6 new tests, all passing** (13/13 in the file total, 7 pre-existing + 6 new): a legal pick
      shrinks Suffix and appears verbatim; two picks shrink Suffix then Prefix to zero; an illegal
      pick and an over-budget pick are each refused by name before any roll; the remaining (shrunk)
      budget still rolls normally on an otherwise-untouched pool; zero picks (`null` vs
      `Array.Empty<ForcedPoolPick>()`) reproduce identical `ContentFingerprint()`s.
    - Test design note: `ContainerValidator.Validate` refuses an all-zero-weight pool
      (`UnsatisfiablePool`) and requires `rolls ≤ drawable groups` — both surfaced as real, useful
      test-authoring corrections (weight-0 rows can't be used to force determinism; the "remaining
      slot" test instead proves the shrink by exact atom COUNT, which parent affix wins the last
      real draw is genuinely non-deterministic and untested by design).
    - Verified no regression: `--filter Atoms` 1335/1335 (a single perf-benchmark flake on the first
      run, confirmed by re-run, unrelated — CPU contention from the concurrent 64-species
      classification run, not this change).
    - ### ⛔ Real correction, caught before F2.4 shipped on top of it — DONE 2026-09-07
      - **The pool-membership check above was wrong and has been removed.** Verified against real
        content while building F2.4 (`data/seed/creatures/species-effects/plant/pilot-batch.json`): every
        species' pool uses opaque, per-species-authored affix ids (`affix.authored.affix-draw-008`)
        with zero overlap between species. Inheritance is inherently cross-species (a sacrifice's own
        species pool feeding a DIFFERENT output species' roll), so requiring a forced pick's `AffixId`
        to be a member of the TARGET container's own pool would refuse nearly every real inheritance
        pick — defeating the mechanic's entire point (a fused child is supposed to carry something
        its OWN species could never roll on its own, matching the genre's own "fusion inherits a skill
        neither parent's base kit includes" precedent already cited in `creature-mechanism-gaps-ideal.md`
        §3.3).
      - **Fix**: `Compose` no longer validates `AffixId` against `container.Pool` — `fusion-inherit.
        not-a-pool-member` no longer exists as a refusal. Legitimacy is now the CALLER's job (F2.4:
        source picks only from a real specimen's own real materialised roll via F2.2) — `AffixId` is
        kept on `ForcedPoolPick` for provenance/logging only. The budget check
        (`fusion-inherit.exceeds-roll-budget`) is unaffected and still real.
      - The former "illegal pick" test was replaced with
        `A_forced_pick_naming_an_affix_that_is_not_in_this_containers_own_pool_still_succeeds` — proves
        the corrected behavior directly rather than just deleting coverage. 13/13 in the file still
        green after the fix; `--filter Atoms` 1335/1335 (one more instance of the same unrelated perf
        flake, confirmed by re-run).

- [x] **F2.2: read a sacrificed specimen's own materialised roll at fusion time — DONE 2026-09-07** · **S**
  - Fusion today only knows input *species* ids (`spec-creature-fusion.md`: *"Recipe inputs are SPECIMENS
    of those species... All inputs consumed"* — the specimen's own roll is consumed, never inspected).
    Add a lookup: given a specimen's `instanceId`, return the real atoms it actually rolled.
  - Acceptance:
    - [x] A real specimen's own `instanceId` resolves to its own real rolled atoms, not the species'
          generic pool.
    - [x] A specimen with no materialised roll (no container ever existed for its species) returns an
          explicit "nothing to inherit from" result — never a crash, never a fabricated empty roll
          presented as real.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests --filter Fusion`
  - Files: `src/FusionRpg.Data/Sqlite/RpgStore.Fusion.cs` (extend), its tests. SQL stays inside
    `FusionRpg.Data` — `guard-dal.ps1` must stay green.
  - ### ✅ Evidence — **DONE 2026-09-07**
    - New `RpgStore.GetSpecimenMaterialisedRoll(string specimenInstanceId)` (placed in
      `RpgStore.PlayerSpecies.cs`, alongside `ListPlayerSpecies` — its own natural home, not a new
      file) — reuses `GetUniqueActor` → `GetCreatureProfile` → `ListPlayerSpeciesInstanceMapUnlocked` →
      `GetInstance`, the exact same tables/lookups every other read path already uses. Returns the
      existing `FusionRpg.Core.Creatures.Materialise.MaterialisedRoll(SpeciesId, Instance)` record —
      no new type invented.
    - `null` is the one, honest "nothing to inherit from" outcome, covering all four real causes:
      unknown instanceId, a bare `UniqueActor` with no creature profile, a real specimen whose species
      has no `species-passive.{id}` content, and one whose player has simply never run
      `player-materialise` for it.
    - **4 new tests, all passing**: a real specimen resolves to its own real rolled atoms (verified
      by `ContentFingerprint()` equality against the independently-read `player_species` row, plus a
      direct atom-id check); an unmaterialised species returns null; an unknown instanceId returns
      null; a bare `UniqueActor` with no creature profile returns null. `MintCreature` validates against
      the real `CreatureSpeciesCatalog` (unlike `SpeciesMaterialiser`'s own pure path), so the fixture
      uses a real catalog species (`peashooter`) rather than `PlayerMaterialiseTests`'s synthetic ids.
    - Verified: `guard-dal.ps1` clean; targeted filter (`PlayerSpecies|PlayerMaterialise|
      SpecimenMaterialised|RpgStore.Fusion|Data.Tests.FusionStoreTests`) 35/35, zero regressions.

- [x] **F2.3: `inheritCostByRarity` — reuse the existing table shape, index by the pick's own rarity — DONE 2026-09-07** · **S**
  - `data/tuning/fusion.v1.json` → `.v2.json`: add `inheritCostByRarity`, same 150→1000-souls shape
    `recipeCost` already uses across the same rarity rungs — but looked up by the *inherited pick's
    own source species' rarity*, not the fusion output's.
  - Acceptance:
    - [x] Table covers every rung `recipeCost` covers.
    - [ ] A real test fuses two differently-rarity'd sacrifices and confirms each pick's own cost is
          read from its own source rarity, not the output's. — **honestly deferred to F2.4**:
          `ExecuteFusion` (the only thing that could fuse two real, differently-rarity'd sacrifices)
          does not exist yet. Covered instead by a loader-level proof that the two tables are
          genuinely independent (see evidence).
  - Verify: `dotnet test tests/FusionRpg.Core.Tests --filter Fusion`
  - Files: `data/tuning/fusion.v1.json` → `.v2.json`, its loader.
  - ### ✅ Evidence — **DONE 2026-09-07**
    - `fusion.v2.json` published via a new, narrow `tools/tuning/publish.py --add-inherit-cost-table`
      flag (matching `--add-edge`/`--add-rung-power-budget`'s own established "add exactly one thing
      a coverage gap needs, refuse rather than guess" precedent) — `set`'s dotted-path mechanism
      explicitly refuses to invent a brand-new top-level key, so a real new-table publish needed its
      own narrow flag, not a hand-edit. Derives `inheritCostByRarity` from `recipeCost`'s own `souls`
      field verbatim (150/220/320/450/620/820/1000), refusing if the key already exists or if
      `recipeCost` is missing/malformed.
    - `FusionTuning.InheritCostByRarity` (`IReadOnlyDictionary<CreatureRarity, long>`) +
      `FusionTuningLoader` parses it over the SAME `CreatureRecipeCatalog.OutputEligibilityFloor`-filtered
      rung set `recipeCost` already uses (Cultivated..Almanac, 7 rungs) — a flat souls-only map, not
      the full compound `RecipeCostTuning` shape, since only souls are ever summed for a pick set.
    - **A real, previously-latent breaking-change discovered and fixed, not assumed away:** `FusionTuning`
      is a positional record — adding the new required constructor parameter broke all three
      hand-mirrored `ContractTuningTestBootstrap.cs` fixtures (Core/Data/E2E.Tests) and 5 real
      `Read("fusion.v1.json")` call sites (`RarityTuningCoverageTests.cs` + 4 Server.Tests files) that
      the real loader now refuses to parse (missing the new required key). All 3 fixtures gained a
      matching `InheritCostByRarity` block (mirroring `RecipeCost`'s own values); all 5 call sites
      bumped to `fusion.v2.json`; `Program.cs`/`RpgHost.cs`'s own hardcoded live-load paths bumped too.
    - **3 new/extended tests in `RarityTuningCoverageTests.cs`**: `InheritCostByRarity` covers exactly
      `RecipeCost`'s own rung set; its souls climb monotonically up the ladder, same rule as
      `RecipeCost`; and it is proven a genuinely separate table (`Assert.NotSame`) whose values happen
      to match `RecipeCost`'s own today, not an alias.
    - Verified: `RarityTuningCoverageTests` 5/5; `guard-dal.ps1` N/A (no SQL here). The Injector's own
      build could not be independently re-verified (`dotnet build` on `FusionRpg.Injector.csproj`
      throws `Ambiguous project name` — confirmed via `git stash`/rebuild/`pop` on `RpgHost.cs` alone
      to be a pre-existing environment issue, reproducing identically with the edit removed).

- [x] **F2.4: `ExecuteFusion` accepts, validates, and prices player-selected picks — DONE 2026-09-07** · **M**
  - Extend the fusion request to carry a list of picks (which atom, from which of the two sacrifices).
    Validate: pick count ≤ `slotsByRarity[resultRarity]` (the owner's own "full ceiling" decision);
    every picked atom is one the naming specimen actually rolled (F2.2); total souls ≥ the sum of each
    pick's own source-rarity cost (F2.3); remaining slots roll normally via F2.1.
  - **Real design constraint found and resolved while building this task (not pre-planned):**
    `player_species` is shared per (player, species) and materialised ONCE, never re-rolled
    (`spec-player-materialise.md` §3/§7 — "a species already present... is neither an error nor
    rerolled"). Since a fusion's forced picks can only land by composing the OUTPUT species' own
    `player_species` roll, and that roll is shared across every specimen of that species a player
    ever owns, picks are only honorable the FIRST time this player ever obtains the output species —
    if a shared roll already exists, honoring new picks would mean silently re-rolling it out from
    under every OTHER specimen of that species. Resolved as a named refusal
    (`picks.already-materialised`), not a gate the player can bypass — a picks-free fusion is
    completely unaffected either way (no `player_species` write happens at all when `Picks` is empty,
    matching every pre-F2.4 caller byte-for-byte).
  - **A second real gap found and fixed the same way:** `FusionCostTable.InheritPick` throws
    `ArgumentOutOfRangeException` for a source rarity below `CreatureRecipeCatalog.OutputEligibilityFloor`
    (Cultivated) — by design, per F2.3. Since a recipe's own inputs sit ONE RUNG BELOW its output
    (`InputPoolBelow`), a Cultivated-output recipe's inputs are always below that floor, meaning an
    unguarded call would crash the whole request with an unhandled exception instead of a clean
    refusal. Added an explicit `CreatureRarityLadder.AtLeast` guard before the `InheritPick` call,
    returning a named refusal (`picks.source-below-inherit-floor`) instead.
  - Acceptance:
    - [x] A valid, affordable pick-set fusion succeeds; the output's instance contains exactly the
          forced picks plus a correctly-rolled remainder.
    - [x] A pick-set over `slotsByRarity`'s cap is refused before spending anything.
    - [x] An unaffordable pick-set is refused before spending anything — matches `ExecuteFusion`'s own
          existing "refusals write nothing; mid-sequence failure leaves zero rows" contract.
    - [x] A pick naming an atom its specimen never actually rolled is rejected by name.
  - Verify: `dotnet test tests/FusionRpg.Data.Tests --filter ExecuteFusion`
  - Files: `RpgStore.Fusion.cs`, `FusionEndpoints.cs`, the fusion request/response DTOs.
  - ### ✅ Evidence — **DONE 2026-09-07**
    - New `FusionPick(string SourceInstanceId, string AtomId)` record; `FusionRequest` gained an
      additive optional `IReadOnlyList<FusionPick>? Picks = null` (positional, defaulted last — every
      pre-existing 4-arg call site across `FusionStoreTests.cs`/`PatronStoreTests.cs` compiles
      unchanged). `SameRequest`'s replay-equality check extended to compare `Picks` too (structural
      record equality via `SequenceEqual`) — an unmatched pick-set on a replayed correlation id now
      correctly reports `correlation.mismatch` instead of silently replaying the old outcome.
    - `RecipeUnlocked` gained a validate-then-price-then-materialise block, in this order (matching
      the method's own established refusal-before-spend discipline): already-materialised gate →
      slot-cap gate → per-pick source/atom/rarity-floor validation (accumulating `picksSouls` via
      `FusionCostTable.InheritPick`) → combined `cost with { Souls = cost.Souls + picksSouls }` spent
      through the SAME `SpendFusionCostsUnlocked` call the base recipe already uses (so "insufficient"
      is refused before any spend, and a mid-sequence materials failure rolls back the whole
      transaction, exactly like every other refusal in this method) → mint → (picks-only) compose the
      output species' own `species-passive.{id}` container with the resolved `ForcedPoolPick`s via
      `InstanceProducer.Compose`, then write `effect_instance`/`effect_instance_atom`/`player_species`
      inline using the SAME `db`/transaction `RecipeUnlocked` already holds (plain `db.CreateCommand()`
      calls, matching this method's own convention — not `RpgStore.PlayerSpecies.cs`'s `ExecIn`-based
      helpers, which open a SEPARATE lock/connection and would re-decide "already owned" against a
      different snapshot than the one already proven inside this transaction).
    - `FusionEndpoints.cs`: `FusionHttpRequest.Picks` (new `FusionPickHttp { SourceInstanceId, AtomId }`
      list, nullable/optional) maps into `FusionRequest.Picks` in `ToRequest`.
    - **7 new tests** in `tests/FusionRpg.Data.Tests/FusionInheritancePicksTests.cs`, all passing,
      covering every acceptance line above plus the two real gaps found while building it: a valid
      pick-set succeeds and the output's `player_species` instance carries exactly the forced pick
      (`PrefixRolls=1` total, 1 forced → 0 rolled remainder); over-cap and unaffordable pick-sets both
      refuse before any souls/materials move and leave no `player_species` row for the output species;
      a never-rolled atom id is rejected by name; a pick naming an instance that isn't one of the two
      sacrifices is rejected by name (`picks.source-not-a-sacrifice`); a pick-set against an
      already-owned output species refuses (`picks.already-materialised`); zero picks reproduces
      today's exact recipe-fusion behavior byte-for-byte (no `player_species` write attempted at all).
      Test fixture deliberately selects a recipe whose OWN INPUTS already sit at/above
      `OutputEligibilityFloor` (not the sibling `FusionStoreTests.Recipe`, a Cultivated-output recipe
      whose inputs sit one rung BELOW that floor and are therefore never pick-eligible).
    - Verified: `FusionInheritancePicksTests` 7/7; `FusionRpg.Data` + `FusionRpg.Server` both build
      clean (0 warnings introduced, 0 errors).

- [x] **F2.5: web FE — real picks in the fusion lab — DONE 2026-09-07** · **M**
  - Show each sacrifice's own real rolled atoms (named, readable — not the species' generic pool);
    let the player pick up to `slotsByRarity[resultRarity]` before confirming; show the running soul
    cost live as picks are added or removed, priced per F2.3.
  - Acceptance:
    - [x] A player can see and select from both sacrifices' own real, distinct rolled atoms.
    - [x] The displayed cost updates live and matches F2.3's own table exactly.
    - [x] The UI cannot submit a pick-set past the rarity's own cap.
  - Verify: Vitest + a real click-through against a live server (`local-web-review` skill).
  - Files: `src/FusionRpg.Server/FusionEndpoints.cs` (preview extended), `web/fusion-rpg-web/src/
    lib/bus/fusion.ts`, `web/fusion-rpg-web/src/features/fusion/{fusionView.ts,fusionView.test.ts,
    FusionPage.tsx}`.
  - ### ✅ Evidence — **DONE 2026-09-07**
    - `BuildPreview`'s Recipe branch now returns `pickableAtoms` (each sacrifice's own real rolled
      atoms via `GetSpecimenMaterialisedRoll`, filtered to specimens whose own rarity clears
      `OutputEligibilityFloor` AND only when this player doesn't already own the output species —
      mirroring `RecipeUnlocked`'s own two gates so the FE never offers a pick the server would
      refuse) and `pickSlotCap` (`FusionRoller.SlotsFor(output.BaseRarity)`, the same table F2.4
      enforces server-side).
    - New pure helpers in `fusionView.ts`: `togglePick` (add/remove capped at `slotCap` — the UI
      structurally cannot assemble an over-cap selection), `picksSoulsCost`/`costWithPicks` (sum
      selected picks' own priced souls onto the base recipe cost, reusing `haveNeed` unchanged rather
      than duplicating its affordability logic).
    - `FusionPage.tsx`: new "Inherit atoms" panel (shown only when `pickableAtoms.length > 0`) lets
      the player toggle up to `pickSlotCap` atoms, each labeled by its source species and its own
      priced souls cost; the Cost panel shows a live "+N Souls for M inherited atom(s)" line computed
      from the SAME selection; `picks` ride along on `execute`; a sacrifice-set change clears stale
      picks (the old selection could name a specimen no longer in the request).
    - **13 new Vitest cases** in `fusionView.test.ts` (`togglePick` add/remove/cap/cross-source
      distinction; `picksSoulsCost`/`costWithPicks` sum/zero/no-match-is-zero-never-NaN) — all
      passing; full FE suite unaffected (12 pre-existing unrelated failures — Phaser HUD mocks +
      whole-tree guard scans — confirmed identical with F2.5 fully `git stash`-ed out).
    - **Live click-through** (`local-web-review` skill): a stale published server was occupying
      5088 (killed per the skill's own documented incident); rebuilt `wwwroot`, and while getting a
      fresh server up **found and fixed a real, pre-existing, unrelated schema-drift bug** — the
      `dist/FusionRpg.Server/data` database predates `ContainerRow.Frame`/`BaseTypeId` (added earlier
      today per `loot-content-view-unwired.md`) and has no migration path for existing databases
      (`EnsureColumn` was never added for these two columns) — `RpgStore.ItemPower.cs`'s
      `ValidateRarityPowerBudget` crashed the whole server at boot. Patched non-destructively (two
      additive `ALTER TABLE ... ADD COLUMN` statements on the existing sqlite file, no data loss) to
      unblock the live check; **the missing `EnsureColumn` migration itself is a separate, named,
      unfixed gap** — any other pre-existing database hitting this boot path has the same crash.
    - Verified live against the real 904-species roster + a real connected player: `/api/fusion/preview`
      for a real matched recipe pair (`biggloom`+`bamboodragon` → `chimeric`) returned
      `pickSlotCap: 2` and a correctly-shaped (here: honestly empty, since neither specimen had a
      materialised roll for this player) `pickableAtoms` — no crash, no new console errors (13
      pre-existing, unrelated). Confirmed via the real UI too: selecting both specimens rendered the
      Cost panel with live have/need numbers and the silhouetted result, no "Inherit atoms" panel
      (correctly absent — nothing to pick). **Did not execute a real fusion** — a real game client was
      connected to this server (`injectorConnected: true`); an actual `execute` would have
      irreversibly consumed a real player's real specimens, which is out of scope for a verification
      pass. See Checkpoint F2 below for what this leaves open.

### ✅ Checkpoint F2 — a real fusion carries real, player-chosen inheritance end to end
- [ ] A live fusion: two real sacrificed specimens, a player picking real atoms from each (up to the
      full rarity cap, per the owner's own decision), souls spent matching each pick's own source
      rarity, remaining slots rolled normally — proven together, not just per-task. **Not run**: F2.5's
      live check verified preview/selection end-to-end (real server, real roster, real recipe match)
      but deliberately stopped short of `execute` — the only connected player was a real, live game
      session, and a real pick-set fusion irreversibly consumes two real specimens. Needs either the
      owner's own run, or a disposable test player/specimen pair.
- [x] `slotsByRarity`/`recipeCost`'s own existing shapes are reused, not duplicated by a second curve
      (`FusionRoller.SlotsFor`/`FusionCostTable.Recipe` on the server, `costWithPicks` layering onto
      `haveNeed` unchanged on the FE — confirmed by reading both, not assumed).
- [x] Full Data + Core suites green; `guard-dal.ps1` clean (targeted Fusion-substring filter run:
      1193 tests, 1188 passed — the 5 failures are pre-existing item/charm corpus-size drift,
      confirmed unrelated via `git stash` differential re-run).

## WAVE P — patron-creature (spec: docs/architecture/creatures/spec-patron-creature.md; injector scope, ends at a LIVE owner gate)

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
Then, with the server up and a patron designated in `#/creatures`:
1. `/api/debug/effects/session-grants` shows `patron:aura` after board.start.
2. Plant damage vs a fixed target shifts by the aura (150‰ cap → up to +15 typed points).
3. board.end withdraws the grant (session view empties).
4. Perf probe window shows no new hot-path cost.
5. Switching patrons mid-match changes nothing until the next match.

Commit draft for the owner (Wave P):
```
Patron creature: element aura into live PvZ, soul-priced switching, kill bonus
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

## Post-patron order: creature-contracts → creature-capture → world-events

## creature-fusion — owner decisions (locked 2026-08-21, all eight; spec builds from these)

1. **Identity — both, by mode:** same-species-band merges evolve the BASE creature (instanceId, nickname, XP, lineage survive; sacrifices consumed); cross-species RECIPE fusions consume all inputs and mint the recipe's output.
2. **Recipes — both layers:** rarity-band merges are the always-available floor; code-authored discoverable cross-species recipes (generated from the species catalog) are the ceiling — hidden until first success, codex-recorded with discovery Soul bonuses.
3. **Materials — cost + element gate:** every fusion costs rarity-matched shards; the result's element demands matching essences; Souls charge a base fee.
4. **Randomness — sure species, rolled extras:** output species guaranteed (recipes always work — crafting, the anti-gacha); traits/variant roll from a server seed, correlation-idempotent like summon pulls.
5. **Traits — pick one, roll rest:** player picks ONE guaranteed trait from any input; remaining slots (count by result rarity 1/2/2/3) seeded-roll from the combined input pool.
6. **Ceiling — recipes reach legendary:** deep recipes (epic bases + rare materials) mint legendaries as the deterministic path beside pity; capture-only species stay excluded everywhere.
7. **Merge floor — stars + capped promotion:** sacrifices raise the base's star rank (cap by rarity; per-mille combat channel bonuses per star, deploy-power later in PvZ); a max-star base may promote ONE rarity once, re-rolling trait slots upward.
8. **Patron creature — after fusion:** un-parked; slots between fusion and contracts (small injector scope; patron effect scales from stars/fused creatures).

## WAVE G — creature-contracts (spec: docs/architecture/creatures/spec-creature-contracts.md; detail table in creature-standalone-plan.md §Wave G)

Server + web only — no injector slice, no LIVE gate. Full-auto closes this wave.

- [x] G1: `ContractPolicy` + `LoyaltyRank` + `CreaturePersonality` (pure Core) — rank bands/‰, personality percentages, upkeep by rarity, ritual, slot ladder, whole-day arithmetic. S
- [x] G2: Data schema (`rpg_creature_contracts`, `rpg_contract_state`, `Reset()` coverage) + one-shot deterministic migration auto-bind + mint-time auto-bind into a free slot + read model. M
- [x] G3: `SettleContracts` — 30-day clamp, per-day dedupe spend, insolvent-day decay floored at `DeployFloor`. M
- [x] G4: bind / release / ritual / buy-slot transactions (pact fee, correlation-idempotent, patron + on-expedition release guards, retirement frees the slot). M
- [x] G5: **risk slice** — the four fielding gates (`BuildSquad`, expedition dispatch, `TryBeginDeploy` for creature-profile specimens only, `SetPatron`) + `EnsureContractsReady`; full Data + E2E blast-radius sweep. M
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

**Open with the owner (built as planned, reversible):** the spec locks auto-bind for *migration*; the build also binds at *mint time when a slot is free* (plan §Wave G decision 1) — free, no pact fee, and it raises the daily tribute silently. One-line revert in `MintCreatureUnlocked` if you want the stricter rule.

Commit draft for the owner (Wave G):
```
Creature contracts: binding slots, loyalty, daily tribute

  Core: ContractPolicy (rank bands 200/400/600/800 with +0/15/35/60 per-mille own-channel
  bonuses, five personalities scaling gain/decay/upkeep, rarity-scaled daily upkeep, ritual
  and slot-price ladders, whole-UTC-day arithmetic clamped to 30). Data: rpg_creature_contracts
  + rpg_contract_state; lazy day-quantised SettleContracts (one dedupe-keyed ledger row per
  UTC day, or decay when the balance cannot cover it, floored so time never costs a creature its
  deployability); one-shot best-first migration plus mint-time binding into a free slot;
  bind/release/ritual/buy-slot each one transaction, refusals write nothing; consumption frees
  the slot. Gates: web squads, expedition dispatch, PvZ deploy (creature-profile specimens only)
  and patron designation refuse unbound/insubordinate creatures by name. Results move loyalty —
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
- [x] Isolation: contracts never reach across players — results, bind, and release all refuse another summoner's creature.
- [x] A consumed (Retired) creature cannot be re-contracted (`specimen.missing`); its slot stays reclaimable.
- [x] Slot ladder climbs all 36 purchases to the 48 ceiling, then refuses `capacity.max` writing nothing (199,800 Souls of ladder proven exactly).
- [x] Migration tie-break: level outranks seniority when rarity and stars tie (test asserts its own premise — that XP moved the level).
- [x] E2E: a squadless match **skips** unbound creatures instead of refusing, and credits nothing to the creature that sat it out.
- [x] E2E: an expedition moves the loyalty of everyone who went, and both members share the trip's single verdict.

New: 8 Data + 2 E2E. Suites after: Core 1474 / Data 191 / E2E 139 / Guard 40; guards 4/4.

## Five-axis review of Wave G (2026-08-21) — findings + fixes

- [x] **Important (correctness):** settlement conflated "the ledger refused this charge" with "the player could not pay" — a day already on the books fell through to the decay branch, eroding loyalty on a day that was *paid for*. Currently only reachable if a dedupe row outlives its stamp, but silent and player-punishing. Split the balance check from the append result; an already-present key now counts as settled. Prove-It test verified RED against the old code first (`A_day_already_on_the_ledger_is_paid_not_unpaid`).
- [x] **Suggestion (robustness):** `DateTimeOffset.Parse` of round-trip stamps used the ambient culture — a non-Gregorian calendar parses those into a different date, and one of the two sites is on the mint path where a throw would break summoning. Both now pass `InvariantCulture` + `RoundtripKind`. Note: `ExpeditionEndpoints.cs:80-81` has the same pattern from Wave D — pre-existing, left alone, worth a sweep someday.
- [x] **Suggestion (readability):** hoisted the daily `due` out of the settle loop (it cannot change within a settle) and folded the `bound.Count == 0` check into the loop condition; dropped a `due <= 0` guard that could never fire.
- [x] **Documented, not changed (architecture):** the loyalty credit sits OUTSIDE the exactly-once envelope on both result paths (web match and expedition collect). A crash between ingest/rewards and the credit loses ±15 loyalty and no sweep replaces it; a retry can never double-credit. Accepted trade, now stated in both files rather than left as an accident.
- Reviewed and found sound: refusal-rollback semantics (a refused bind discards its own settlement, which the next call redoes — no double charge, no lost money); no nested store connections inside a held gate (every gate uses `*Unlocked` helpers); cross-player isolation enforced at the store, not the endpoint; `playerId`-in-body matches the existing PatronEndpoints pattern and the loopback threat model.
- Post-fix suites: Core 1483 / Data 202 / E2E 139 mine-green / Guard 40 / Vitest 220; guards 4/4. Foreign reds excluded: 6 × `WorldE2ETests` (world stream, landed mid-session), CheatCore `lab-shield-bar` (shield/VFX stream).
