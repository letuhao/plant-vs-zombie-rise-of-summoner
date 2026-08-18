# Test probes

Present only when `FUSIONRPG_SIM=1`. Otherwise these routes are **404**.

## `POST /api/test/reset`

Flushes the event writer, then deletes rows in `settings`, `events`, `runs`, `entities`, `spawn_stats`, `mowers`, `types`, `recipes`, `metrics`, `players`, re-seeds `Player 1` + defaults, resets `SimEngine`.

Returns `{ "ok": true }`.

## `GET /api/test/snapshot`

**Flushes** the in-process writer so SQLite matches what was accepted. Assertion payload:

```json
{
  "health": { "ok": true, "injectorConnected": false, "lastHeartbeatUtc": null, "simEnabled": true, "source": "none", "ingestQueued": 0, "lastFlushMs": 0 },
  "stats": {},
  "events": [],
  "eventCount": 0,
  "eventCounts": { "plant.spawn": 1 },
  "runs": [],
  "metrics": [],
  "players": [],
  "currentPlayerId": 1,
  "entities": [],
  "mowers": [],
  "types": [],
  "recipes": [],
  "spawnStats": [],
  "sim": {}
}
```

`events` is the last 100 rows (same default as `GET /api/events`). Use `eventCount` / `eventCounts` for volume asserts.

## `POST /api/test/probe`

Body: `{ "name": "after-spawn", "scenario": "match-lifecycle", "data": {} }`.

Enqueues event kind `test.probe`. Broadcasts to SignalR group `web` after persist (non-noisy).
