# Audit — RPG mechanisms as built (2026-08-21)

**Purpose:** ground the next design round (a map / world layer) in what the RPG *actually is in code today*, not what the specs say it will be. Every number below was read out of `src/`; where a doc and the code disagree, the code wins and the drift is listed in §4.

**Method:** read the architecture set (combat stack, demon program, standalone program), then verified each claim against `FusionRpg.Core`, `FusionRpg.Data`, and the task lists. Grep and build results are quoted where they carry a finding.

**Moving-target caveat (important):** `src/FusionRpg.Core/Battle/` was being edited during this audit — `BattleEngine.cs` changed at 18:19 while the audit was in progress. At 18:1x the tree did not compile (`BattleEngine` still referenced the retired `BattleRuleset.Hit*Milli` constants); at 18:2x `dotnet build src/FusionRpg.Core` is clean, with the SSOT resolver, `DamageApplyPipeline`, and `ShieldRuntime` wired into the engine. Battle internals in §2.1 are a snapshot of a stream that is still landing — re-read before building on them.

---

## 1. Verdict

The RPG has a **deep, well-guarded machine and very little content or agency on top of it**.

- The machine — stats → derived channels → elements → statuses → shields → effects/funnel → damage resolution, plus a deterministic seeded battle engine, ledgers, and idempotent transactions — is strong, tested, and mode-agnostic (web and PvZ share it).
- What sits on the machine is thin: **4 waves, 4 expedition tiers, 24 species, 13 traits, 0 skills, 0 player decisions inside a battle, 0 places**.
- The economy is a closed roster-power loop: everything earned buys roster power, which earns more. Nothing consumes value in a way a player would call *territory*, *upkeep*, or *risk*.

That is exactly the shape where a world/map layer adds the most — and also the shape where it can quietly double content-authoring cost if the current string-switch content pattern carries forward (F-B3).

---

## 2. What exists (verified)

### 2.1 Combat stack

| Layer | Reality in code | State |
|---|---|---|
| StatSystem | Y0 + tagged bag → Flat→Increased→More→Override; `EntityStatWriter` sole Unity writer | shipped |
| ActorHub derived | catalog channels incl. `combat.*`, `combat.shield.*` (56 → 84 with shields) | shipped |
| ElementHub | 6 elements: ring `fire→ice→earth→air→fire` ±250‰, `light↔dark` mutual counter, all other cross pairs neutral | shipped |
| StatusRuntime | 21 statuses (8 Unity-wrapped, 13 overlay), families/mutex, two ICD clocks, sigmoid apply-vs-resist, contagion | shipped |
| Shields | element-typed pools, cap 3/actor, drain 30→20→10, absorb above the HP write | offline; live proof pending |
| Effects | FT1–FT4 triggers, FA1–FA10 actions, Secondary → Funnel → FA10 (hp add-only) | sealed v2 |
| Damage resolution | one SSOT resolver (sigmoid hit/crit + typed power/defense + matchup) → shield gate → funnel; battle now delegates to it | landing now (U9–U14) |
| Event pipeline v2 | hot hooks → ring → frame-budgeted coalesced drain; hitcount-aware proc math | shipped |
| VFX | cue → recipe → primitive; sustained per-status visuals specified, not built | v2 live, v3 in review |

**BattleEngine** (`src/FusionRpg.Core/Battle/BattleEngine.cs`): round-based auto-resolve, `RoundDurationMs = 1000`, `MaxRounds = 50`; per-round order = status ticks → initiative-ordered attacks → death sweep → round end. Stats from level curves (`BaseHp = 80 + 30L`, `BaseAtk = 12 + 4L`, `BaseDefense = 2 + L`); accuracy/crit baselines in resolver points (`BaseAccuracy = 220 + 26L`) tuned to parity hit ≈ 0.90, parity crit ≈ 0.076. Determinism: owned PRNG, per-system streams (`initiative`, `damage`, `crit`, `essence`, `status`), report stamped `(engineVersion, rngAlgoVersion, rulesetVersion, seed)`.

### 2.2 Content catalogs — the actual numbers

| Catalog | Size | Notes |
|---|---|---|
| Species (`DemonSpeciesCatalog.Generated.cs`) | **24** — 12 common / 6 rare / 4 epic / 2 legendary | 18 zombie-side, 6 plant-side; 2 capture-only (8.3%, inside the ≤15% guardrail; neither legendary ✓); 2 hypno-ally deploy mode, unused |
| Battle traits (`TraitBattleCatalog`) | **13** | 7 funnel-routed, 6 engine behaviors; all passive — no activation, no cooldown |
| Waves (`WaveCatalog`) | **4** | skirmish / warband / onslaught / tyrant, built from rarity bands |
| Expedition tiers | **4** | 30 m · 6 ticks · 1 battle · 2 slots → 20 h · 10 ticks · 4 + boss · 5 slots |
| Tick events | **4** | quiet 40% / found-souls 35% / wild-demon-met 15% / injury 10% |
| Statuses | 21 | almost none reachable in web battles (F-A5) |
| Skills | **0** | wave E2 of battle-enrichment, unstarted |

### 2.3 Economy and progression

**Faucets:** kill +1 (cap 50/match) · victory +100 (first 3 per UTC day, then 50%) · defeat +25 · expedition found-souls 5–15 / 20–50 / 40–90 / 80–180 per tick by tier · discovery 25/75/200/500 by rarity · codex milestones 500 / 1500 · patron +1 per 10 kills.

**Sinks:** summon 100 (standard) / 120 (element focus) · star merge 50 + shard + essence · promotion 200 · recipes 150 / 400 / 1000 · patron switch 100.

**Power:** specimen level · stars (+30‰ power and defense each; caps 3/4/5/5) · promotion (+1 rarity, once) · traits (1/2/2/3 slots by rarity) · element typing · equipment rows.

### 2.4 The loops that exist

```text
play (PvZ run | web battle)
  → events → Activity facts → XP ledger + Soul ledger
  → Souls → summon (pity: epic @25, legendary ramp 41→55) → specimens
  → duplicates → fusion (stars / promotion / recipes) → stronger specimens
  → specimens → expeditions (dispatch → wait → collect) → Souls, XP, wild joins, materials
  → materials → fusion
  → one specimen → patron → small typed aura in live PvZ + earn bonus
```

Everything Cold is server-authoritative, seeded, correlation-idempotent, one transaction per mutation, resolved lazily — timers are just `due_utc`, never a scheduler.

---

## 3. Findings

Severity: **S1** = will distort or block the next layer if built on as-is · **S2** = material gap or imbalance · **S3** = note.

### A — mechanism depth

- **F-A1 (S2) — a battle contains no decisions.** Target selection is "first active opponent", or lowest-HP if the attacker is `bloodthirsty` (`BattleEngine.SelectTarget`). The only player input reaching combat is squad *composition and order*, and order matters solely for `guardian`/`loyal` adjacency. A world design that wants "that fight was hard and I won it" has nothing to hang the feeling on.
- **F-A2 (S2) — no skills, no cooldowns, no in-battle resource.** Every trait is a passive modifier or an engine behavior. Enrichment waves E1 (on-hit status riders), E2 (species skills), E3 (hybrid payloads) are unstarted (`tasks/combat-unification-todo.md:111–113`).
- **F-A3 (S2) — enemy power ignores rarity.** `WaveCatalog.Enemies` builds every enemy from the level curves alone; a legendary wave member has identical HP/ATK/DEF to a common of the same level. Rarity changes only element typing and trait pool — "elite" and "boss" are flavor, not statistics.
- **F-A4 (S1) — enemies get *all* their species traits; players roll a subset.** `WaveCatalog` passes `species.TraitPool` straight into `TraitIds` (`WaveCatalog.cs:60`), while player specimens roll `FusionRoller.SlotsFor(rarity)` = 1/2/2/3 (`SummonRoller.RollTraits`). A legendary enemy can field 5 traits including `immortal` + `void-touched` against a player legendary capped at 3. Enemy strength therefore shifts whenever a species trait pool is edited — invisibly to every tuning table and golden.
- **F-A5 (S2) — the status system barely exists in web battles.** Statuses enter only through scripted `InitialStatuses` at setup; no attack, trait, or element applies one (riders are E1). The 13 authored overlay statuses, the resistance evaluator, contagion, and the whole potency model are exercised in PvZ mode only. Element matchup is the sole element expression in a battle.
- **F-A6 (S3) — injuries are resolver-local.** An `injury` tick appends a −Atk/4 power mod for that expedition's remaining battles (`ExpeditionResolver.ApplyInjuries`) and vanishes at collect. No wound, recovery, or condition state exists for a world layer to read.

### B — content shape

- **F-B1 (S1) — four waves is the entire enemy content.** The 20 h boss is literally `BossWaveId = "rift-tyrant"` — the same tier-4 wave, not the hypno-ally boss the standalone map describes. Expedition variety comes from RNG bands, not authored encounters. A map with N nodes has nothing distinct to put in them.
- **F-B2 (S2) — the roster is thin at the top and unused at the edges.** 2 legendaries; 2 capture-only species nothing can capture; 2 hypno-ally species nothing deploys. Deploy modes, acquisition flags, and lore fields are stored and unread.
- **F-B3 (S1 for the next layer) — content is authored as parallel string switches.** Tier facts live in `WaveChain`, `SoulsRange`, and `XpPerBattleWon` — three `switch (tier.TierId)` blocks in `ExpeditionResolver` — plus `ExpeditionTierCatalog` and `BossWaveId`. Nothing binds them; a fifth tier means finding all five sites. Multiplied by a map of locations, this becomes the dominant cost of every content addition. Folding chain + reward data into the tier def (catalog-validated, like species and statuses) is cheap now and expensive after a map ships.
- **F-B4 (S3) — the wild-join pool is correctly guarded:** never capture-only, never legendary; join chance 25% of wild ticks; shiny 1/64; materials are two shard tiers plus per-element essences.

### C — economy

- **F-C1 (S2) — every sink buys roster power.** Souls → summons → duplicates → fusion → stronger roster → more Souls. There is no upkeep, consumable, per-attempt cost, or territory to hold. A world layer that adds no sink of a *different shape* is another faucet.
- **F-C2 (S2) — expedition rewards do not read outcomes.** Shards drop at plan time, win or lose, deliberately (manifest determinism). Found-souls, wild joins, and materials are seed-rolled, never performance-rolled; only specimen XP is per-battle-won. An expedition pays for *time occupied*, not difficulty faced — the exact axis a map ("harder region pays more") will want to price.
- **F-C3 (S3) — no per-location or per-difficulty modifier exists** anywhere in the earn policy; the only content-scaled multipliers are the tier souls range and `greedy`'s +250‰.
- **F-C4 (S3) — the sole throttle on parallel play is expedition slots (2–5 by tier)**, by decision (no stamina). A map that adds many simultaneous destinations inherits that question directly.

### D — cross-mode

- **F-D1 (S2) — PvZ ↔ web coupling is one narrow bridge:** the patron aura (`PatronPolicy`: `rarityBase + 10·star + level`, clamped 150‰; primary element full power, half defense) plus a soul-earn sweetener. The other guardrailed roles from the standalone map — Blessing booster, exclusive capture, shared deploys, trophies — are policy text, not code.

---

## 4. Drift between docs and code (state, not blame — two streams are landing today)

| # | Doc says | Code says |
|---|---|---|
| D1 | [combat-unification-map.md](combat-unification-map.md): "Build is held until the owner confirms the battle stream is finished" | U1–U8 and U15–U16 are checked off, and `BattleEngine` already runs the SSOT resolver, `DamageApplyPipeline`, and battle shields — U11–U13 material is in the tree ahead of the todo's own gate note |
| D2 | [demon-system-map.md](demon-system-map.md) lists `patron-demon` as "next"; [demons/spec-patron-demon.md](demons/spec-patron-demon.md) says "implementation not started" | `Core/Demons/Patron/PatronPolicy.cs`, `Data/Sqlite/RpgStore.Patron.cs`, the `rpg_patron` table, and the `patron` earn reason all exist |
| D3 | [standalone/spec-expeditions.md](standalone/spec-expeditions.md): the 20 h tier ends on "a boss wave using a hypno-ally species as enemy" | boss = `rift-tyrant`, the ordinary tier-4 wave |
| D4 | `RulesetVersion = 2` is stamped in `BattleRuleset` | the golden re-baseline + expedition sweep that the bump requires is task U14, unchecked — version stamp and goldens are out of step until it lands |
| D5 | [shield-system-spec.md](shield-system-spec.md): standalone absorption "lands with Battle-C2"; the `BattleActorSetup` innate seam "is deferred" | `BattleInnateShield` and the shield event vocabulary are in `BattleModels.cs`, and the engine mounts a battle-local `ShieldRuntime` + gate |

None of these are errors — they are a fast-moving tree with docs written before the stream finished. They matter here only because a world design that reads the docs alone would design against a stale machine.

---

## 5. Readiness for a map / world layer

**There is no spatial vocabulary in the system.** Verified: zero occurrences of `region`, `zone`, `biome`, `location`, `territory`, or `worldnode` in `FusionRpg.Core` or `FusionRpg.Server`; none of the 38 tables holds a place. The word "world" appears only in the deferred `world-events` module id.

What already has the *shape* of a place:

| Existing thing | Place-like property it already has |
|---|---|
| Expedition tier | duration, squad slots, a battle chain, a reward range — a destination without a name |
| `WaveDef` | an encounter table with a recommended level |
| Sealed expedition seed | content determinism per visit |
| Materials (shards, per-element essences) | a regional-currency shape waiting for regions |
| Codex | a discovery ledger — but keyed by species, not by place |
| Patron aura / Blessing policy | a cross-mode modifier channel a map could vary by location |

What would have to be invented:

1. **A location entity and catalog** — id, danger tier, element bias, encounter table, reward policy — catalog-validated like species and statuses.
2. **Encounter tables keyed by location.** `WaveCatalog` is flat and small (F-B1); a map immediately needs enemy content that varies by node, and enemies that read rarity (F-A3, F-A4).
3. **A travel/time model.** Today "time" is only `due_utc`; there is no distance, route, or cost to being far away. Whether the map has distance at all is a real fork.
4. **Persistent world state — or an explicit refusal of it.** The expensive fork: stateful places (ownership, depletion, rotation, events) need their own ledger and trim policy, following the archive discipline every other table follows; a stateless map (nodes as content indexes, all state on the expedition) costs almost nothing and keeps lazy resolution intact.
5. **A per-location reward policy** replacing the tier string switches (F-B3), so a node's payout is authored once.
6. **Discovery/fog for places.** The codex covers species only; "I found a place" has no ledger.
7. **A seed hierarchy** — world seed vs per-visit seed, and who owns replay once a place persists between visits.

Hard constraints any map design inherits (from [decisions.md](decisions.md)): gameless-first (playable and CI-provable with PvZ closed) · one economy, source-tagged, through the existing ingest · server-authoritative and correlation-idempotent · **lazy resolution only — no background schedulers or clock-driven world ticks** · pure integer Core resolvers · SQL only inside `FusionRpg.Data` · no new event kinds without asking.

That schedulers constraint is the sharpest one: a world that "moves while you are away" must be *computed from elapsed time on read*, exactly as expeditions do — never simulated by a background job.

---

## 6. Questions the map design has to answer

1. **Index or place?** Is a node a curated content bundle (encounter table + reward policy + flavor), or a stateful location that changes between visits? Cost, trim, and determinism all follow from this one.
2. **Does the map replace expedition tiers or multiply them?** (node × tier = 4× the content surface; node-with-its-own-duration is a clean replacement.)
3. **Is travel a cost** — time, Souls, distance — or free selection from a list?
4. **What does the map ask of combat?** If nodes must feel different, they need enemy variety (F-A3, F-A4) and probably the enrichment waves (riders, skills) before a node can express anything.
5. **What does the map consume?** Without a new sink shape (F-C1), it is another faucet.
6. **Where does PvZ sit on the map** — a node, an off-map blessing source, or the frontier that reveals nodes?
7. **Does interactive battle arrive with the map or after it?** A map of places you cannot play in is a menu with a background image.

Answering 1, 2, and 5 is enough to start a capability map; the rest can resolve inside module specs.
