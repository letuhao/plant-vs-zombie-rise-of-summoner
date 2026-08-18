# Simple Spawner vs Rise of Summoner

Notes from [Tproplay/Simple-Spawner](https://github.com/Tproplay/Simple-Spawner) (MIT, targets 3.8.1).

| | Simple Spawner | Rise of Summoner (us) |
|---|---|---|
| License | MIT | MIT |
| Goal | Place plants/zombies from almanac | Capture + scale + SQLite/web |
| Almanac | `SelectCard` | Same hooks for telemetry |
| Factories | `CreatePlant.SetPlant`, `CreateZombie.SetZombie` | Same + dumps / place events |
| Stats log | No | Yes (`spawn_stats`, events) |
| Persistence | None | Server SQLite |

Useful takeaway: factory signatures on 3.8.1 are the place/spawn entry points. We do not copy their UI; we hook the same APIs for capture.
