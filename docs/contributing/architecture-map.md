# Architecture map (contributors)

Short map. Specs live under `docs/architecture/` and module folders.

## Four player-facing modules

| Module | Path | Responsibility |
|---|---|---|
| Launcher | `src/FusionRpg.Launcher` | Browse game folder, install loaders from official GitHub, install plugin, start/stop server+game, FusionRpg self-update |
| Injector | `src/FusionRpg.Injector` + `.BepInEx` / `.MelonLoader` | Shared Harmony hooks behind RpgHost; thin loader hosts |
| Server | `src/FusionRpg.Server` | ASP.NET REST + SignalR; no SQL in controllers |
| Web | `web/fusion-rpg-web` | Vite/React control UI (built into `Server/wwwroot`) |

Supporting libraries: `FusionRpg.Core`, `FusionRpg.Data` (sole SQL), `FusionRpg.Contracts`, `FusionRpg.CheatCore`.

## Where not to put things

| Concern | Put it here | Not here |
|---|---|---|
| SQL / SQLite | `FusionRpg.Data` only | Server controllers, Injector, Web, Core MatchRuntime / CapPolicy |
| Live match / AdmitSpawn | MatchRuntime RAM ([match-runtime.md](../architecture/match-runtime.md)) | `FusionRpg.Data`, Activity rollups, `entities` table |
| Durable unique specimens | UniqueActor / Data ([unique-actor-runtime.md](../architecture/unique-actor-runtime.md)) | BoardProjection, type `rpg_actor_progression` PK, hot StatSystem `instance:` keys |
| Combat procs (on-hit freeze/heal) | Injector EffectBag + Funnel Hot loop ([overlay-control-loops.md](../architecture/overlay-control-loops.md), [effect-funnel.md](../architecture/effect-funnel.md)) | Server FSM mid-hit RTT; RPG `SetHp` from snapshot; FA10 `TakeDamage` |
| Timed status state (DoT, counters, contagion) | StatusRuntime RAM ([status-ssot.md](../architecture/status-ssot.md)) — design locked; **blocked on Actor Hub** | `DeliverySpec.OverTime/Counter` long-term; Server status FSM; direct Writer from status tick |
| Derived status power/resist at Apply | Actor Hub DerivedComposer ([actor-hub-ssot.md](../architecture/actor-hub-ssot.md)) | Primary StatSystem only; ad-hoc resist tags outside catalog |
| **Implementing Actor Hub / Status** | Start at [actor-hub-status-implement-plan.md](../architecture/actor-hub-status-implement-plan.md) (S0–S7 slices) | Ad-hoc slice order; StatusRuntime before S1 |
| Unity lawn XY (cherry, floaters, pet/bucket) | Injector `LawnCoords` / Mouse box ([lawn-coords.md](../injector/lawn-coords.md)) | Entity `transform.position` feet; Phaser `gridMath.ts`; `(col,row)` as world units |
| FE lawn grid / RPG interact | Phaser 4 projector ([lawn-projector.md](../architecture/lawn-projector.md)) | Hot Admit, proc RNG, Activity-as-living |
| Unity / IL2CPP writes | Injector (+ Core effect plans) | Server, Data, Web |
| Player process orchestration | Launcher | Server |
| Architecture locks | `docs/architecture/decisions.md` first | Silent PR-only changes |

## Loader rules (v1)

- Official BepInEx / MelonLoader downloads only (pinned in `loader-manifest.json`).
- Never dual-load. Play copies `DropIntoGame/{profile}/{loader}/` → `BepInEx\plugins\FusionRpg\` **or** `Mods\` (never both).
- Dual-host artifacts: BepInEx + MelonLoader (+ Melon 3.9) — [dual-host-roadmap.md](../injector/dual-host-roadmap.md).
- “Update” = FusionRpg zip only — never the game binary.

## Further reading

- [architecture/actor-hub-ssot.md](../architecture/actor-hub-ssot.md)
- [architecture/actor-hub-status-implement-plan.md](../architecture/actor-hub-status-implement-plan.md) — S0–S7 implement checklist
- [docs/README.md](../README.md)
- [architecture/overview.md](../architecture/overview.md)
- [injector/lawn-coords.md](../injector/lawn-coords.md)
- [launcher/spec.md](../launcher/spec.md)
