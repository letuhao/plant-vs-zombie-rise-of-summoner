# SignalR

Hub URL: `http://127.0.0.1:5088/hub/rpg`

JSON protocol. Two logical groups:

- `injector` — the game plugin (usually one connection)
- `web` — any number of browsers

On connect, client calls `Join(string role)` with `injector` or `web`.

## Client → server

| Method | Args | Who | Effect |
|---|---|---|---|
| `Join` | `role` | both | Add to group |
| `Hello` | `{ game, version }` | injector | Mark connected, enqueue `injector.hello`; **W0-E:** push `effects.grants.apply` (session grant snapshot) |
| `Event` | `EventEnvelope` | injector | **Enqueue only** (compat). No SQLite on this call |
| `Events` | `EventEnvelope[]` | injector | **Enqueue a batch** (preferred). One invoke per flush |
| `Metrics` | `{ name, value }[]` | injector | Upsert metrics |
| `Heartbeat` | `{ game }` | injector | Update last heartbeat |

Injector flush (120fps): send only if nothing is in flight **and** (`queue >= 256` or **16ms** since last send). Drain up to 256. HTTP fallback: `POST /api/events` with `{ "events": [ ... ] }`.

## Server → client

| Method | Args | Who | When |
|---|---|---|---|
| `Event` | envelope + `id` | web | After persist, single non-noisy event (compat) |
| `EventBatch` | `{ events: [ ... ] }` | web | After a writer commit, **non-noisy** kinds only |
| `StatsUpdated` | stats JSON (same as GET `/api/stats`) | injector | After PUT stats or reload command |
| `PvzStatsUpdated` | sheet / revision | web | After PvzStats mutate / seed |
| `PvzActivityUpdated` | rollup / revision (`playerId`) | web | After Activity fact insert (append, Intent, capture projection flush) |
| `RpgProgressionUpdated` | `{ playerId, kind?, typeId?, revision? }` | web | After XP apply / demotion clear |
| `Command` | `{ name, payload }` | injector | e.g. `reload-stats`, `pvz.spawn.extra`, `effects.grants.apply`, `ping` |
| `Health` | health DTO including `ingestQueued`, `lastFlushMs` | web | On heartbeat (optional; web may also poll `/health`) |

Noisy kinds **not** pushed live: `plant.damage`, `zombie.damage`, `bullet.init`, `bullet.place`, `item.drop`, `pet.xp`. They remain in SQLite. Metrics/runs still update.

## Reconnect

Clients auto-reconnect. Injector: on SignalR `Reconnected`, re-`Join` **and** re-`Hello` (W0-E grant rehydrate). If hub never connects within 5s, use HTTP fallback for events and poll `GET /api/stats` every 5s. Pending commands: `GET /api/cheats/commands/pending`.

## Payload types

Same DTOs as REST. See [events.md](events.md) and [rest.md](rest.md).
