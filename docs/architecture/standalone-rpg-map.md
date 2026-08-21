# Capability map: Standalone-first RPG architecture

Program goal: **invert the architecture's center of gravity** — the RPG (demons, souls, progression, battles) becomes a complete game playable in the web FE with the PvZ game closed; PvZ play becomes an optional *extension* mode. Status: **wave 1 + expeditions SHIPPED 2026-08-21** — charter, pipeline adaptations, match-source-core (BattleEngine + WebMatchService, goldens locked), and expeditions (the announced ship gate: dispatch→collect playable in the web FE) are implemented with all suites green; module boundaries were approved via owner decisions 2026-08-21. Module specs live in [demons/](demons/) (existing program) and [standalone/](standalone/) (this program).

## Why this is an inversion, not a rewrite

The stack was built game-agnostic from day one: server and web already speak only `game` + `kind` + JSON; `FusionRpg.Core` (StatSystem, ActorHub, StatusRuntime, ElementHub, OverlayCombatMath, EffectBag, MatchRuntime) is Unity-free; `SimEngine` + `FUSIONRPG_SIM=1` already drive the full pipeline without the game; the demon V1 slice is entirely Cold-plane. What changes: a **playable gameplay source** joins the injector as a peer producer of matches and facts, and the docs/decisions flip which mode is "core".

## Resolved decisions (2026-08-21)

1. **Gameplay form:** expeditions first (timed, auto-resolved, server-simulated squad missions), interactive turn-based battles second. Both feed the same event → Activity → XP/Souls pipelines.
2. **Combat authority:** server-authoritative. Battles resolve server-side with Core subsystems and seeded RNG (summon-roller precedent); the FE renders and commands, never rolls.
3. **PvZ extension roles — all four, with guardrails** (adjudicated in the [2026-08-21 review](standalone/audit-2026-08-21.md); companion-game precedent says gating content behind a companion is the classic failure, so each role is bounded):
   - **One-axis rule:** each role owns exactly one axis — capture = collection *breadth*, booster = earn *tempo*, shared battles = roster *expression*, trophies = *prestige*. PvZ must never be the best source of something web mode also provides.
   - **Exclusive capture:** ≤15% of the species catalog, zero legendaries; codex completion milestones claimable at 90% so web-only players get every milestone; world-events later rotates a slow web path to each exclusive (exclusive *method*, not permanent lock).
   - **Booster = Blessing, not multiplier:** a real PvZ run charges +50% Soul earn for the next 3 web expeditions (max 1 charge banked) — PvZ play makes web play better instead of competing with it; PvZ-sourced income intended ≤40% of total.
   - **Shared battles:** ≤2 demon deploys per PvZ run, small additive grants via the existing bounded effect path; specimens on a web expedition are soft-locked from PvZ deploy (Cold-plane flag, not the UniqueActor FSM) and vice versa — no double-dipping.
   - **Trophies:** cosmetic, one-time grants only — never stats, never repeatable faucets.

**Expedition design anchors** (prior-art research + owner decisions 2026-08-21; the expeditions spec elaborates): duration tiers 30 min / 4 h / 8 h / 20 h (20 h so daily schedule drift never punishes); parallelism gated by expedition slots (2 → 5 via progression), **no stamina system** — with no monetization a stamina gate has no honest job; recall allowed anytime with rewards pro-rated to completed ticks; outcome sealed at dispatch by recorded seed, revealed at collection; nothing expires if uncollected.

- **Content shape (locked): chain + events** — each tier resolves a battle chain (30 min = 1 battle … 20 h = 4 + a boss wave using a hypno-ally species as enemy) interleaved with seed-rolled non-combat event ticks (found souls, met a wild demon, took an injury). Ticks = the recall pro-rating boundary.
- **Rewards (locked): all channels** — Souls + player XP (via the normal pipeline), **specimen XP** per battle won (existing unique-actor XP path), a small seed-rolled **wild-join chance** (a defeated wave demon joins the roster, origin `expedition` — the non-gacha acquisition path that honors vision rule #2 before PvZ capture ships), and **fusion material stubs** (per-player material inventory, unusable until demon-fusion lands — deliberately pre-seeding that economy).
- **Post-expeditions order (locked): demon-fusion next** (duplicate pressure is already real; fusion is the promised Reserve sink), then contracts, then PvZ capture.

## Modules

| Module id | Responsibility | Depends on | Wave |
|---|---|---|---|
| `standalone-charter` | The inversion SSOT: mode taxonomy, gameless-first rule, `game` profile for web mode, decisions.md amendment | — | **1** |
| `match-source-core` | Promote server-side match production to a first-class source: `BattleEngine` in Core (pure, seeded) resolving squad-vs-wave combat via ActorHub/Status/Element/CombatMath/EffectBag; canonical events through the normal ingest (runs, facts, XP, Souls) | standalone-charter | **1** |
| `expeditions` | Playable loop #1: squad select → timed expedition → server auto-resolve → rewards + encounter discoveries; FE screens | match-source-core, demon-core, soul-economy | **2** |
| `web-battles` | Playable loop #2: interactive turn-based battles (server-resolved turns, same BattleEngine); FE battle UI | expeditions | **3** |
| `game-bridge` | PvZ-as-extension policy: earn multipliers by source, exclusive-capture species flags, shared-deploy continuity, trophies | standalone-charter (+ demon-capture later) | **3** |

**Combined roadmap with the [demon program](demon-system-map.md):** `standalone-charter` + `element-extension` (parallel) → `demon-core` → `soul-economy` + `match-source-core` (parallel) → `demon-summoning` → `expeditions` (**the moment the RPG is a standalone playable game**) → `web-battles` / `demon-contracts` / `demon-capture` (PvZ) → `demon-fusion` → `game-bridge` polish → `world-events`.

## Invariants this program adds

1. **Gameless-first:** every RPG feature must be fully playable and CI-provable with the PvZ game closed. The injector may *enrich* a feature, never *gate* it.
2. **One economy:** web mode and PvZ mode write the same ledgers through the same ingest; source-tagged (`source=web|injector`), never forked.
3. **Server-authoritative play:** all web-mode outcomes (rolls, battles, expeditions) resolve server-side with recorded seeds; correlation-idempotent commands.
4. **Existing locks unbroken:** injector Hot-path invariants, Funnel/Writer, DAL boundary, and guard scripts are untouched by this program.
