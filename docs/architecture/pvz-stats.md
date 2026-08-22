# PvzStats architecture

Player-bound **game data foundation** between future RPG features and the injector write path.

See also: [stat-system.md](stat-system.md), [actor-hub-ssot.md](actor-hub-ssot.md), [decisions.md](decisions.md).

## Naming

| Name | Role |
|---|---|
| **PvzStats** | Persist Xi, revision, monitor sheet, FE drill-down |
| **StatSystem** | Pure compose engine (`Y0 + Xi → Y`) |
| **Cheats** | Global operator overlay (separate forever) |
| **RPG stats** (later) | Progression/content that **upserts into** PvzStats or **Actor Hub derived catalog** |

> **This row is about the RPG modifying the *game's* stats — not about the RPG storing its own state here.** (Clarified 2026-08-22 after the line was misread twice.) Upserting `pvz_stat_modifiers` is a **command**: "make this plant tougher." It is legitimate and shipped.
>
> What is never correct is putting an RPG gameplay concept into the PvZ channel because it looks like a stat. RPG resources, actions, skills, and their pools live entirely in `rpg.*` — the eight `StatChannels` are the game's stats, not ours. The two systems share **no state in either direction**, only messages ([software-architecture.md](software-architecture.md) §3).
>
> Rule of thumb: if PvZ would be meaningless without it, it belongs here. If PvZ neither knows nor cares about it, it does not.

```text
RPG features (later)
  → upsert pvz_stat_modifiers and/or derived catalog channels
PvzStats
  → revision + derived snapshot/contributions (cache)
  → API / SignalR
pvz.stats plugin → StatSystem.Resolve → EntityStatWriter

Actor Hub (design)
  → DerivedComposer → status.power.* / status.resist.* at Apply
  → progression.power stub from RpgProgressionSubsystem
```

**PvzStats** remains SSOT for persisted **primary-channel** modifier rows. **Actor Hub derived channels** (`progression.power`, `status.power.*`, …) are composed at resolve time — see [actor-hub-ssot.md](actor-hub-ssot.md). When PvzStats rows target catalog channels, validate against **DerivedStatCatalog** (future code plan).

## SSOT vs cache

- **SSOT:** `pvz_stat_modifiers` (source-tagged rows).
- **Cache only:** `pvz_stat_snapshots.finals_json` + `pvz_stat_contributions` — rebuilt on every mutate. Never re-apply from finals.

Sheet compose uses **Y0 = 0** for monitoring (Flat +10/−5 → sheet hp **5**). Living combat Resolve uses real entity Y0 + PvzStats + cheats. FE must label **PvzStats sheet**, not match HP.

## Plugin

Single injector plugin `pvz.stats` (Order 250) emits all enabled mods from `StatContext.PvzStatsMods`. Row `plugin_id` is **provenance** from higher features (`rpg.item`, …), not a filter on the write path.

## APIs

| Route | Role |
|---|---|
| `GET /api/pvz-stats/{playerId}` | Sheet summary |
| `GET /api/pvz-stats/{playerId}/channels/{channel}` | Contributions + detail_json |
| `GET /api/pvz-stats/{playerId}/modifiers` | Raw SSOT |
| `POST .../modifiers/upsert\|withdraw\|reset` | Mutate → bump → rebuild → `PvzStatsUpdated` + `pvz.stats.reload` |
| `POST /api/test/seed-pvz-stats-demo` | Demo +10/−5 on **hp and maxHp**; broadcasts `PvzStatsUpdated` + enqueues `pvz.stats.reload` |

Web UI: `#/pvz-stats`.

## Living reapply

Injector dirty path calls `CheatActions.ReapplyLivingFromStats` (not Tab-A-only `PushScalesNow`). Tracks `AppliedPvzStatsRevision` so clear/reset still rewrites living entities to baseline. Damage hooks pass `PvzStatsMods` and compose when cheats ApplyStats **or** PvzStats bag is non-empty.

## API hardening

- GET sheet/channel/modifiers → **404** if player missing (no orphan Ensure).
- Withdraw requires ≥1 filter else **400** (use reset for full clear).
- Channels canonicalize to `StatChannels` (case-insensitive); unknown → **400**.
