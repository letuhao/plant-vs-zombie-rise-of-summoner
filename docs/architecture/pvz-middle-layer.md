# Pvz middle layer (Stats + Activity + Intent)

Player-bound **game data foundation** between future RPG content and the PVZ capture/injector path.

See also: [pvz-stats.md](pvz-stats.md), [pvz-activity.md](pvz-activity.md), [pvz-intent.md](pvz-intent.md), [decisions.md](decisions.md), [effect-system.md](effect-system.md) (Foundation Effects sit on Intent/Writer; Secondary never applies to Unity).

## Naming

| Prefix | Owns | Does not own |
|---|---|---|
| **`Pvz*` / `pvz.*`** | Game foundation (attributes, facts, intents) | Quests, trade UI, inventory screens |
| **`rpg.*`** | Content/progression that **uses** foundation | Direct Unity writes / bypass capture |
| **Cheats** | Operator overlay | Progression SSOT |

## Three pillars

| Pillar | Shape | Example |
|---|---|---|
| **PvzStats** | Mutable Xi bag + revision + sheet cache | luck, HP Flat |
| **PvzActivity** | Append-only typed facts + rollup cache | MatchEnded, ZombieKilled, ExtraSpawnFired |
| **PvzIntent** | Command bus → injector → game | `pvz.spawn.extra` |

**Capture** (`events`, `runs`, `spawn_stats`) stays raw telemetry. Progression must read **Activity** (and Stats), not invent XP from dump JSON.

```text
RPG features
  → upsert PvzStats | append PvzActivity | enqueue PvzIntent
Pvz*
  → API / SignalR / revision
Injector
  → EntityApply / spawn commands → Unity
Capture
  → project selected kinds → PvzActivity facts
```

## Constitution (R/W laws)

1. RPG never touches Unity — only Stats / Activity / Intent.
2. Injector is the only game writer (`EntityApply` / `EntityStatWriter` / explicit spawn commands).
3. Capture is observation; Activity is progression substrate.
4. Source-tag everything (`plugin_id`, `source_kind`, `source_id`).
5. Mutable state uses revision clocks; facts are append-only; rollups are cache.
6. Server stamps `player_id`; injector never sends it.
7. Run-scoped facts carry `run_id`; lifetime rollups are per player.
8. Cheats stay outside Pvz*.

## Luck → extra spawn (stress test)

```text
PvzStats luck (composed)
  → SpawnDirector feature
    → PvzIntent pvz.spawn.extra
      → ExtraSpawnFired (on Intent accept / enqueue)
      → Injector (or sim) spawn independent of Board waves
        → capture with source=extra
          → later ZombieKilled when that zombie dies
```

Do **not** project a second `ExtraSpawnFired` from `source=extra` capture (would double-count with enqueue-time fact). Vanilla wave spawn stays distinguishable by source.

RPG content (e.g. [RpgProgression](rpg-progression.md)) reads Activity and writes its own tables — never Unity or dump JSON for XP.
