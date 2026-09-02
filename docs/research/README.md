# Research index

Two kinds of thing live here, and they have different rules.

- **Game-design research** (the folders below) — prior art from *other* games, gathered so this project
  reasons from evidence rather than memory. **Every file ends with a "What I could not find" section.**
- **This project's own audits and dumps** (the loose files) — measurements of *this* codebase and the
  host game.

---

## ⛔ Before commissioning any new research, read the gap sections

**Three rounds and roughly twenty research passes have run here.** Between them they have spent well
past a thousand web queries and recorded **17 files' worth of named absences and access blocks.**

| Read first | Why |
|---|---|
| [game-design/06-unsourced.md](game-design/06-unsourced.md) | **The canonical "what does not exist" file.** Its **§2a is the current access map** (re-measured 2026-09-02) and supersedes the older table above it — most importantly, **Fandom's HTTP 402 is bypassable via `r.jina.ai`** |
| Each folder's `README.md` | Per-round method, sourcing strength, and the findings that mattered |
| Each file's `## What I could not find` | The specific searches already run and already failed |

**The standing negative findings**, confirmed across every round — do not go looking for these again:

- **No studio has ever published a quantified counter-strength target.**
- **No studio publishes a power-vs-cost ratio or a cooldown-to-power formula.**
- **No published break-even mathematics for buffs in turn-based combat.** (The general form was
  *derived* in `action-taxonomy/05` §4.1.)
- **Almost no designer commentary on roster or grid design exists anywhere.** The one first-party
  design-rationale document found across every pass was Blizzard's page on Warcraft III's upkeep.
  The exception is the **healer-mandatory problem**, which designers genuinely do discuss on record.

---

## The research folders

| Folder | Round | What it answers |
|---|---|---|
| [game-design/](game-design/README.md) | 2026-09-01, 8 passes | **Unit design and roster scale.** Typing matrices with real values, the unit-attribute checklist across 16 systems, the units-per-grid-cell ratio, designer quotes, documented failure modes, and the canonical unsourced file |
| [genre-mechanics/](genre-mechanics/README.md) | 2026-09-02, 8 passes | **PvZ and the neighbouring genres.** PvZ2 international and Chinese, the mod scene, franchise siblings, tower defense, summoner/fusion RPGs, RTS and auto-battlers, endless scaling — plus a feature comparison against this project's own build state |
| [action-taxonomy/](action-taxonomy/README.md) | 2026-09-02, 7 passes | **Actions and skills.** How many categories shipped RPGs use, a 25-row targeting gap table, composable/procedural skill systems, status and control design, support and healing, cost models, and cost-mistuning failures |
| [arpg-effects/](arpg-effects/00-index.md) | earlier | Primary attributes, modifier stacking, procs and triggers, ailments, hit/crit/conversion — and the mapping onto this project |
| [ai-native-generation/](ai-native-generation/README.md) | earlier | What a model may decide and what it may never touch; contract design, enum bias, vote resolution, cost budgeting |

**The strongest single file in the set** is
[genre-mechanics/02-pvz2-chinese-and-fusion.md](genre-mechanics/02-pvz2-chinese-and-fusion.md) — it
documents the host game's fusion API, rarity enums, content counts and difficulty ladder **from its own
shipped assemblies**, read with Mono.Cecil. Do not re-derive any of that from wikis.

---

## This project's own audits and dumps

Measurements of this codebase and the host game, not prior art.

| Area | Files |
|---|---|
| **Host game internals** | [game-types-381.md](game-types-381.md) · [melonloader-assembly-csharp-39.md](melonloader-assembly-csharp-39.md) · [melonloader-assembly-csharp-p0.md](melonloader-assembly-csharp-p0.md) · [harmony-hook-map.md](harmony-hook-map.md) · [stat-fields.md](stat-fields.md) · [modifiable-gameplay.md](modifiable-gameplay.md) · [level-entry.md](level-entry.md) · [simple-spawner.md](simple-spawner.md) · [sources.md](sources.md) |
| **Loaders and hosting** | [mod-loaders.md](mod-loaders.md) · [2026-08-22-overlay-injector-host.md](2026-08-22-overlay-injector-host.md) |
| **Subsystem audits** | [actor-hud-audit-2026-08-30.md](actor-hud-audit-2026-08-30.md) · [actor-hud-data-pipeline-audit-2026-08-30.md](actor-hud-data-pipeline-audit-2026-08-30.md) · [commander-fe-audit-2026-08-30.md](commander-fe-audit-2026-08-30.md) · [chaos-derived-stats-audit.md](chaos-derived-stats-audit.md) · [actor-core-chaos-mapping.md](actor-core-chaos-mapping.md) · [status-core-chaos-mapping.md](status-core-chaos-mapping.md) · [cheat-menu-coverage.md](cheat-menu-coverage.md) · [events-lifecycle.md](events-lifecycle.md) |
| **Effect / atom layer** | [atom-effect-pool-audit-2026-09-02.md](atom-effect-pool-audit-2026-09-02.md) — **⛔ the atom vocabulary is built (12 kinds, 11 primary + 267 derived channels) and the pool file `data/seed/atoms/*.json` IS wired into seedsmith — but it holds 21 demo atoms; 1 primary and 1 derived channel are addressed by shipped content, 63 channels have no designed family, and `stat.derived` never checks its channel is registered** |
| **Balance baselines** | [resource-symmetry-audit-2026-09-02.md](resource-symmetry-audit-2026-09-02.md) — **⛔ `poise` (the 6th resource) has zero aptitude edges, blocking `guard-economy`; `DominanceGuard` hand-lists resources and misses three** · [class-analytic-balance-2026-08-25.md](class-analytic-balance-2026-08-25.md) · [class-rps-balance-2026-08-25.md](class-rps-balance-2026-08-25.md) · [class-residual-2026-08-27.md](class-residual-2026-08-27.md) · [class-system/](class-system/) |
| **Perf, VFX, world, effects** | [perf/](perf/) · [vfx/](vfx/) · [world/](world/) · [effect-runtime/](effect-runtime/) · [architecture-stress/](architecture-stress/) |
| **Open threads** | [open-questions.md](open-questions.md) |

---

## Rules these folders follow

Carried forward from `game-design/README.md`, and applied to every round since.

1. **Shipped data over wiki prose.** Datamines, official APIs, engine source and patch notes first;
   a wiki is second-tier and says so inline.
2. **Every non-obvious number carries a source URL.** Self-tallied numbers are marked **(computed)**.
3. **FACT and INFERENCE are marked separately.**
4. **A mandatory, non-empty "What I could not find".** This is the anti-re-research mechanism; a file
   without one is not finished.
5. **These are not specs and not proposals.** They hold evidence. Design work goes through
   [../DESIGN-GATE.md](../DESIGN-GATE.md), whose §1 topic index routes to the relevant folder.
