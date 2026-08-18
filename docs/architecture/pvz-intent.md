# PvzIntent architecture

Solid **game write** path for features that must affect Unity without patching `GameHooks` ad hoc.

See [pvz-middle-layer.md](pvz-middle-layer.md).

## Pattern

1. Feature reads PvzStats (e.g. luck) at current revision.
2. Feature enqueues `CommandDto` on the injector inbox / SignalR `Command`.
3. Injector executes (spawn, etc.), emits source-tagged capture.
4. Capture projects to PvzActivity facts where applicable.

## v1 command

| Name | Payload | Effect |
|---|---|---|
| `pvz.spawn.extra` | `typeId`, `col?`, `row?`, `reason`, `correlationId`, `side` (`zombie` default) | On **inserted** accept: Activity `ExtraSpawnFired` + enqueue `pvz.spawn.extra` (`Command.Id = correlationId`). Injector tags spawn `source=extra`. When no live injector and sim is on, sim also spawns with `Source=extra`. |

Provenance: `plugin_id=pvz.spawn`, `source_kind=intent`, `source_id=extra` (or reason slug).

Idempotency: same `correlationId` → fact `OR IGNORE` → `inserted=false` → **no** second command enqueue.

## vs cheats

Cheat `spawn-zombie` remains operator tooling. Intent commands are namespaced `pvz.*` and must leave auditable Activity facts when they change the lawn.

## See also

- [lawn-projector.md](lawn-projector.md) — FE enqueue spawn/interact via Intent/debug; never Hot Admit in browser  
- [overlay-control-loops.md](overlay-control-loops.md) — Intent loop vs Hot procs  
