# Capability map: Standalone-first RPG architecture

Program goal: **invert the architecture's center of gravity** — the RPG (demons, souls, progression, battles) becomes a complete game playable in the web FE with the PvZ game closed; PvZ play becomes an optional *extension* mode. Status: **draft — module boundaries approved via owner decisions 2026-08-21** (gameplay form, authority, PvZ roles below). Module specs live in [demons/](demons/) (existing program) and [standalone/](standalone/) (this program).

## Why this is an inversion, not a rewrite

The stack was built game-agnostic from day one: server and web already speak only `game` + `kind` + JSON; `FusionRpg.Core` (StatSystem, ActorHub, StatusRuntime, ElementHub, OverlayCombatMath, EffectBag, MatchRuntime) is Unity-free; `SimEngine` + `FUSIONRPG_SIM=1` already drive the full pipeline without the game; the demon V1 slice is entirely Cold-plane. What changes: a **playable gameplay source** joins the injector as a peer producer of matches and facts, and the docs/decisions flip which mode is "core".

## Resolved decisions (2026-08-21)

1. **Gameplay form:** expeditions first (timed, auto-resolved, server-simulated squad missions), interactive turn-based battles second. Both feed the same event → Activity → XP/Souls pipelines.
2. **Combat authority:** server-authoritative. Battles resolve server-side with Core subsystems and seeded RNG (summon-roller precedent); the FE renders and commands, never rolls.
3. **PvZ extension roles — all four:** exclusive capture source (some species only catchable in real runs) · economy booster (multiplied earn rates in real runs) · shared battle content (one roster deploys to both battlefields via the existing UniqueBindings path) · trophy/prestige (real-run feats feed titles/codex flair).

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
