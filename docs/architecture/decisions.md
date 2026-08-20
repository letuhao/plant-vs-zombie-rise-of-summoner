# Architecture decisions

Locked for v1. Change here before changing code.

| Topic | Decision |
|---|---|
| Live channel | SignalR for **both** injector and web |
| HTTP fallback | If injector SignalR fails to load/connect, POST `/api/events` and GET `/api/stats` still work |
| Frontend | Vite + React + TypeScript. **Players get a static build hosted by FusionRpg.Server** (same origin). Vite/Node is developer-only |
| Web UI kit / bus | Lawn Almanac tokens + shared `ui/` kit + AppShell. Data bus = TanStack Query (REST snapshots) + one SignalR hub (live) + event ring. Features call `useX()` only. HashRouter (`#/…`). See [web/spec.md](../web/spec.md) |
| Player entry | **FusionRpg.Launcher** (WPF). Browse game folder, optional official BepInEx/MelonLoader install, plugin install, port pick, start server+game, FusionRpg self-update. See [launcher/spec.md](../launcher/spec.md) |
| Player runtime | Self-contained `win-x64` publish of **launcher** + **server** (separate folders — Desktop vs ASP.NET runtimes). No SDK, no Desktop Runtime, no Node |
| Server URL | Default `http://127.0.0.1:5088`. Launcher sets `FUSIONRPG_URLS` on server and `FUSIONRPG_SERVER_URL` on game (wins over BepInEx `ServerUrl` cfg) |
| Game folder | User Browse + `%AppData%\FusionRpg\launcher.json`. Suggest only relative to launcher/parent with `PlantsVsZombiesRH.exe`. Never hardcode machine-local paths |
| FusionRpg update | Download `FusionRpg-win-x64.zip` from our GitHub Releases only; preserve `Server\data\`; bootstrap replace. Never download/patch the PVZ game binary |
| Loader installs | Official GitHub only (pins in `loader-manifest.json`). BepInEx `v6.0.0-pre.2` IL2CPP win-x64; MelonLoader latest x64. Refuse dual-load |
| Injector host | **BepInEx 6 plugin and MelonLoader MelonMod** (same Harmony id `com.fusionrpg.injector`, shared `RpgHost` facade). Play installs the matching DropIntoGame payload. Never dual-load. Port plan: [injector/dual-host-roadmap.md](../injector/dual-host-roadmap.md) |
| Stats | **StatSystem** in Core: Y0 baseline + source-tagged modifier bag + plugin registry. Forward-only `Y = Compose(Y0, bag)`. **Actor Hub** (design) adds derived snapshot for status power/resist — see [actor-hub-ssot.md](actor-hub-ssot.md). See [stat-system.md](stat-system.md) |
| Stat compose | Phased: Flat → Increased(sum) → More(product) → Override → clamp. Legacy `StatMod.percent` → More `(p-1)`; flat → Flat |
| Stat extension | Features register `IStatModifierPlugin`; never edit composer / GameHooks apply path |
| Stats transport | `StatsConfig` remains REST/UI shape; feeds `cheat.scale` plugin only — not apply SSOT |
| Cheats SSOT | Web/server document is SoT. Absence = unset. Strip identity floats on migrate/merge. No in-game menu/GUI. Injector present-only apply; HTTP command inbox backup for SignalR |
| PvzStats | Player-bound Xi SSOT (`pvz_stat_modifiers`) + derived sheet cache. Not RPG progression. Single plugin `pvz.stats`. Cheats stay separate. Sheet Y0=0 is monitor-only |
| Pvz middle layer | Three pillars: **PvzStats** (mutable Xi), **PvzActivity** (append facts + rollup cache), **PvzIntent** (`pvz.*` commands). RPG never touches Unity. Capture stays telemetry; progression reads Activity. See [pvz-middle-layer.md](pvz-middle-layer.md) |
| PvzActivity | Append-only `pvz_activity_facts`; rollups/revisions are cache. Project Match*/Kill*/Place*/ExtraSpawn from capture/intent. Not RPG quests |
| PvzIntent | Injector commands under `pvz.*` (v1: `pvz.spawn.extra`). Source-tagged capture; Activity fact on fire. Luck directors read PvzStats then enqueue Intent |
| Game id | Active profile from injector (`pvzrh-3.8.1`, `pvzrh-3.9`, …). Catalog: [game-profiles.json](../../game-profiles.json). Architecture: [game-versioning.md](game-versioning.md). Default / legacy constant `RpgConstants.GameId` = `pvzrh-3.8.1` |
| Game × loader matrix | Compile-time profiles + thin `Bridges/{profile}/` (zombie HP width, SetZombie arity). Ship one DLL per cell. Launcher fingerprints pack → installs matching DropIntoGame subtree. No reflection adapters; no dual-load |
| TakeDamage log | **On** by default this ingest dump (still togglable) |
| Defense | **Vanilla** hits: compose DEF then Prefix `newDamage = max(0, round(damage / defensePercent) - defenseFlat)`. **Overlay/RPG** hits: CombatMath later **above** Funnel; FA10 does not re-run this Prefix |
| HP / ATK write | Compose path: `EntityFinal` after StatSystem.Resolve via **EntityStatWriter** (max / ATK; ratio-remap current when max changes). After spawn, **current HP is Unity-owned**. Instant overlay current-HP (v2 FA10) is Writer **Add** + `Die` if HP≤0 — never `SetHp` from an RPG snapshot, never Unity `TakeDamage` (re-entry / double-dip). Vanilla peas/bites stay `TakeDamage` + Prefix DEF. Spec: [effect-funnel.md](effect-funnel.md) |
| Single Unity writer | `EntityApply.Run*` → Resolve → `EntityStatWriter`; Tab A and Tab B share this path; guard script `scripts/guard-single-writer.ps1` |
| Foundation Effects | Minimal Passive\|Triggered opcodes; sole Effect apply path (`EffectBag` → Writer/Intent/Status). Secondary = Funnel enqueue only — never apply to game, never `Bag.Grant`. See [effect-system.md](effect-system.md), [effect-funnel.md](effect-funnel.md), [effect-data.md](effect-data.md), [effect-runtime.md](effect-runtime.md) |
| VFX | Cue → recipe → primitive presentation layer: producers emit semantic `VfxCueDto` into one `IVfxSink`; injector `VfxDirector` + pooled primitives render from a C#-seeded `VfxCatalog`. Presentation-only, thread-safe enqueue, no `renderer.material`, no per-VFX sinks ever again. See [vfx-ssot.md](vfx-ssot.md) |
| Effect Funnel + Guard | **SSOT:** Secondary always `Funnel.Enqueue` — no `Bag.Grant` exception. Funnel is **Hot** (Core mailbox, injector game thread) — not Server, not ingest 256/16ms. Modifiers = **pass-through** (one Grant per `grantId`; do not fold sources). Mutations = **sum** then **FA10** Writer Add (`hp` only; `mode` add-only). **Never** emit absolute HP from an RPG snapshot; **never** FA10 `TakeDamage` (Prefix DEF + `combat.hit` re-entry). CombatMath (DEF / element / shield) sits **above** Funnel later. `FoundationContractVersion` **2** when plans contain FA10. Guard: `scripts/guard-funnel-delta.ps1` shipped. LIVE HP+FX: `POST /api/debug/effect/enqueue-delta`. Spec: [effect-funnel.md](effect-funnel.md). |
| Combat damage SSOT | Overlay HP changes use **`DamagePacket`** (signed delta: negative = loss, positive = heal — **one pipeline**, no separate heal feature). **TargetSpec** (who) is resolved by **`TargetResolver`**. **DeliverySpec** is **Instant-only** going forward — timed/counter **state** moves to [Status SSOT](status-ssot.md). Apply: plan packet → resolve ptrs → **`OverlayCombatMath`** when `elementPayload` present and flag on (typed power/defense, hit/crit, element matchup) → Funnel → FA10 Writer Add. **Heal v1:** Funnel transport only, no matchup/hit/crit. **No** vanilla `BoardAction` cherry/chili for overlay HP AOE. **`ProcDepthLimit`** from match/policy (default **6**). Spec: [combat-damage-ssot.md](combat-damage-ssot.md), implement plan: [combat-element-implement-plan.md](combat-element-implement-plan.md). **Shipped (flag-gated)** — pass-through when no payload or flag off. |
| Element Hub SSOT | **Element typing** (`fire`/`ice`/`air`/`earth`/`light`/`dark`, max 2 slots; `omni` additive-only), **56 combat derived channels** (8 families × roster, generated from `ElementRoster`), and the **matchup matrix**: ring cycle unchanged + `light ⇄ dark` mutual counter, both neutral vs the ring (`MatchupShareK=0.25`, per-component hybrid, dual-type product rule). Strict name-only element parse (numeric strings reject). Element Hub owns matrix semantics; Actor Hub registers channels. Spec: [element-hub-ssot.md](element-hub-ssot.md), extension: [demons/spec-element-extension.md](demons/spec-element-extension.md). **Shipped** — C1 + light/dark 2026-08-21. |
| Status SSOT | Hot **StatusRuntime** owns actor-scoped status **instances** on `entity:{ptr}` (plants + zombies), lifecycle, ICD, contagion, and Apply-time **resistance/immunity**. **StatusCatalog** = in-memory Core registry (code-first; **no** runtime YAML loader v1). Magnitudes in grant **overlay_json**; power/resist on **actor derived catalog** ([actor-hub-ssot.md](actor-hub-ssot.md)). Status **pulses** emit Instant `DamagePacket` → Funnel → FA10; Unity CC via L4 **StatusExecutor**. **21** named catalog ids locked in spec. Spec: [status-ssot.md](status-ssot.md). **Shipped in Core + Injector** (S0–S7); area DoT + counter meters on StatusRuntime (S6 tail). |
| Actor Hub SSOT | **ActorDerivedSnapshot** = derived-stat SSOT for status Apply (power/resist), dynamic **ApplyScale** from `progression.power`, and **`progression.bonus.*`** combat flats at AppliedCombat merge. **StatSystem** keeps primary compose; **ActorHub** wraps Resolve. Two-phase status resolve: sigmoid apply chance + linear potency netFactor. **DerivedStatCatalog** — unknown channel → reject. Overlay **combat.*** channels reserved per [element-hub-ssot.md](element-hub-ssot.md); registration in Actor Hub, semantics in Element Hub. Spec: [actor-hub-ssot.md](actor-hub-ssot.md). **Shipped** (S0–S1, S2–S7 status path) — see P1/P2 rows below. |
| P1 UpdatePower | Level→`progression.power` via **`IProgressionPowerProvider`** + POC curve `ProgressionPowerCurve` (`2^min(level,12)`). **`StatusPolicy.IncludeTierPowerInDelta = true`**. Injector cache: `InjectorProgressionPowerProvider` (SQLite hydrate later). Spec: [rpg-progression.md](rpg-progression.md), [actor-hub-ssot.md](actor-hub-ssot.md) §4. |
| P2 progression.bonus.* | **`progression.bonus.{maxHp,atk,defense,arm1,arm2}`** flat-sum in derived compose; **`ActorHub.MergeAppliedCombat`** adds flats to Writer input. Level-scaled stub in `RpgProgressionSubsystem` until dedicated bonus ADR. Spec: [actor-hub-ssot.md](actor-hub-ssot.md) §3A. |
| MatchRuntime | Live overlay SSOT = RAM `MatchRuntime` FSM + `MatchState` (BoardProjection, EffectSession, Debug, CapPolicy, **UniqueBindings**). Unity = physics; durable/telemetry only via **`FusionRpg.Data`** (never from MatchRuntime/Injector). Caps gate **our** Intent/FA4/debug extras. Spec: [match-runtime.md](match-runtime.md). **Implementation deferred** to a separate plan. |
| UniqueActor (dual FSM) | Durable unique plant/zombie **specimens** (`instanceId`, level, gear) live in **`FusionRpg.Data`** under UniqueActor FSM (Roster → Deploying → ActiveBound → Recovering). Live lawn uses MatchRuntime UniqueBindings (`instanceId ↔ ptr`) then FA1 `entity:{ptr}`. Three IDs orthogonal: `typeId` / `ptr` / `instanceId`. Type RpgProgression stays separate. Spec: [unique-actor-runtime.md](unique-actor-runtime.md). **Implementation deferred**. |
| Overlay control loops | **Hot** = Injector `EffectBag` + Funnel mailbox + **StatusRuntime** (design) for timed status instances (combat procs; no Server RTT). **Cold** = UniqueActor / Data (equip, loadout push). **Intent** = `pvz.*` extras after Admit. Ban: Server FSM must not sit between `combat.hit` and FA* apply. Spec: [overlay-control-loops.md](overlay-control-loops.md), [effect-funnel.md](effect-funnel.md), [status-ssot.md](status-ssot.md). |
| Overlay P0 hardening | Before unique gear LIVE: (1) Withdraw entity grants on die before ptr reuse, (2) Admit/CapPolicy before FA4/our Create, (3) reject `instance:` in Hot Resolve, (4) FT* on-hit SSOT = TakeDamage + melee arm (not base Hit*), (5) rehydrate grants on injector hello. Reject Server on-hit RNG. Workshop: [../research/architecture-stress/05-p0-workshop-verdict.md](../research/architecture-stress/05-p0-workshop-verdict.md). Plan: [p0-hot-path-hardening.md](p0-hot-path-hardening.md). |
| Lawn projector (FE) | Phaser **4** `#/lawn` observes run grid/entities (MatchSnapshot or events fold); interact via Intent/debug bus only. Never Hot Admit, proc RNG, or Activity rollups as living SSOT. Spec: [lawn-projector.md](lawn-projector.md). **Implementation deferred**. |
| Overlay implement roadmap | Ordered W0–W12 checklist for P0 Hot, MatchRuntime, UniqueActor, lawn FE, guards — [implementation-roadmap.md](implementation-roadmap.md). Docs checklist; waves pending until code plans ship. |
| LimHealth stickiness | Observe via `stat.limhealth` when `SYS-EMIT-PROOF`; active gate `SYS-LIMHEALTH-GATE` default off until proof |
| Apply once | Entity key + Applied gate so Start + InitHealth cannot double-buff; reapply clears Applied but keeps Y0 |
| Match identity | Injector mints `matchKey` (guid) on `board.start`. Every later event carries it. Server maps `matchKey` → `runs.id`. Unique entity/mower key is `(run_id, ptr)` |
| Match end | `BoardStatistics.GameOver(GameResult)` is win/lose source of truth (`match.result`). `HandleGameLose` / `BoardVictory.Win` are breadcrumbs. `Board.Die` only closes the run |
| Mower used | `Mower.StartMove` → `mower.start`. `runs.mowers_used` counts those. `Mower.Die` is cleanup |
| Events vs projections | `events` is the capture log. Combat SSOT is `spawn_stats.stats_json` (same JSON as spawn/recapture payload). `types` is catalog only |
| Type catalog | `type` + `typeName` + `displayName` (`Lawnf.GetName`). First-seen `sample_json` only; never overwrite. Unsafe for damage/XP |
| Spawn dump | Full live-field JSON (`DumpPlant`/`DumpZombie`). `hpBase` = before our slider. Recapture appends `entity.stats` — never overwrite the first dump |
| Plants planted | Count `plant.place` (`SetPlant`), not `plant.spawn` |
| Players | Table `players` (`id` + `name` only). Server stamps `player_id` from `settings.current_player_id` on `board.start`. Injector never sends player id |
| Mid-match switch | Open run keeps the player it started with. Next `board.start` uses whoever is current then |
| RPG reads | `events`, `spawn_stats`, `runs.modifiers_json` / `snapshot_json`. Never `types.hp_base`. Empty dump = missed hook, do not invent stats |
| Match XP later | Use `match.result` (`GameOver`), not `board.end` (ghost boards) |
| RpgProgression | Per-save **type** actors `(player_id, kind, type_id)`; Activity-driven XP; arithmetic `XpToNext`; demotion_count debt; CoR level hooks; power deferred. Specimens ≠ type actors — [unique-actor-runtime.md](unique-actor-runtime.md). See [rpg-progression.md](rpg-progression.md) |
| Metrics | Global rollups this pass (not per-player) |
| Contracts | C# DTOs in `FusionRpg.Contracts` (net6). Web hand-writes TS from protocol docs |
| TFMs | Injector net6.0. Server net8.0 (or net6 if SDK missing). Contracts net6 |
| SQLite | **Live:** `{ServerExeDir}/data/rpg-hot.sqlite` + `rpg-media.sqlite` + `archive/*` (legacy `rpg.sqlite` migrates once). User Storage clear via `/api/storage/*` — [ledger-snapshot.md](../database/ledger-snapshot.md) |
| DAL single gate | **Live:** all SQL in `FusionRpg.Data`; `scripts/guard-dal.ps1` in Guard.Tests **and** `deploy-play.ps1` |
| Ledger snapshots | **Live:** Activity/XP watermarks + post-run tails (10k / 5k); capture KeepLastN=50 cold-move |
| Compact / archive timing | **Live:** `CompactionWorker` on `board.end` only; never mid-run / open promote refused |
| Cold archive | **Live:** write `archive/` before hot delete. User purge on `/storage`; no auto GC. Deep cold-path query deferred |
| Auth | None |
| Docs language | English |
| Third-party clients | Study only if needed; never copy foreign plugin source into this AGPL tree |
| Leftover `RpgPlugin/` | Do not delete unless the user asks |
| Simulator | In-process `SimEngine` behind `FUSIONRPG_SIM=1`. Same event kinds and StatMath as the injector. Not in the player zip |
| Test probes | `/api/test/reset`, `/api/test/snapshot`, `/api/test/probe`. Same flag. SQLite `events` is the log; no extra log files |
| Sim vs injector | Live injector heartbeat → sim POST 409. Health `source` is `none` / `sim` / `injector` |
| CI | xUnit Core + WebApplicationFactory. Web SPA: Vitest coverage + Playwright e2e (`web/fusion-rpg-web`). No real game / Harmony in CI |
| Capture fps | **120fps** design target. Harmony only enqueues. Injector flush when `!inFlight` and (`queue >= 256` or `16ms` elapsed). Not once per Update |
| Event ingest | In-process Channel (not Memcached/Redis). Hub/`POST /api/events` ack immediately. One writer commits SQLite batches (500–1000) in a single transaction |
| Live web | After commit, SignalR `EventBatch` of non-noisy kinds. Damage, `bullet.init`/`bullet.place`, `item.drop`, `pet.xp` stay in SQLite only |
| Test snapshot | `/api/test/snapshot` flushes the writer first so e2e sees SQLite, not the RAM queue |
| Standalone-first (2026-08-21) | **The web RPG is the core game; PvZ is extension gameplay.** Gameless-first rule: every RPG feature must be fully playable and CI-provable with the game closed — the injector may *enrich* a feature (four guardrailed roles: exclusive capture ≤15% no-legendaries, Blessing booster, ≤2 shared deploys, cosmetic trophies), never *gate* one. One economy: web and PvZ modes write the same ledgers through the same ingest, source-tagged. Server-authoritative web outcomes (seeded, correlation-idempotent). Charter: [standalone/spec-standalone-charter.md](standalone/spec-standalone-charter.md); map: [standalone-rpg-map.md](standalone-rpg-map.md); review: [standalone/audit-2026-08-21.md](standalone/audit-2026-08-21.md) |
| Web game profile | `webrpg-1` (`RpgConstants.GameIdWebRpg`, source `web`) identifies server-resolved matches. Event vocabulary + `runs` only — **never** a `game-profiles.json` entry (that catalog drives launcher fingerprint matching). **Qualifies the "Game id is build-level only" row:** `runs.game` becomes a DB column (match-source-core precondition — per-source attribution, retention exemption, runs-list display); facts derive source via run join, no facts column |
| Demon program | Demons = UniqueActor specimens + `rpg_demon_profiles`; species catalog **generated deterministically from captured game data** (types/almanac/icons/spawn_stats), output checked in; elements extended `+light/+dark` (mutual counter, ring untouched, 40→56 channels); Souls = append ledger + watermark (earn v2: +1/kill cap 50, victory 100 w/ repeat decay); summoning = one-transaction pulls, pity v2 visible counters. V1 (core+gacha+souls) is an **internal gate**; expeditions is the announced ship. Maps: [demon-system-map.md](demon-system-map.md), specs in [demons/](demons/spec-demon-core.md) |

## Why REST and SignalR together

REST survives page refresh and is easy to inspect (`GET /api/stats`). SignalR pushes spawn/die/metrics and `reload-stats` without polling. Raw WebSocket would need reconnect and rooms built by hand.

## Why HTTP fallback on the injector

`Microsoft.AspNetCore.SignalR.Client` may fail to load inside this BepInEx IL2CPP host. The game must still apply last-known stats and ship events over HTTP.

## Why no SQLite in the injector

The plugin must stay a thin hook. Persistence belongs to the server so the web UI works with the game closed.

## Why in-process queues, not Memcached

Localhost, one game, one server. Extra processes add failure modes. Injector `ConcurrentQueue` + server `Channel` + one SQLite writer is the cache.

## Why batch SignalR `Events`, not per-event `Event`

At 120fps a fight is thousands of events/s. One `InvokeAsync("Event")` per row waits on the previous persist. HTTP already posts `{ events: [] }`. The hub must match.

## Why the server stamps `player_id`

The game does not know saves. Later RPG is per-save, not per-PC. Current player lives in SQLite; child rows copy `player_id` from the run opened at `board.start`.

## Risks (do not block v1)

See [research/open-questions.md](../research/open-questions.md):

- Whether `Plant.Start` HP is final
- Whether `attackDamage` or `Bullet.Damage` is the real ATK
- Whether SignalR.Client loads in BepInEx (HTTP fallback covers this)
