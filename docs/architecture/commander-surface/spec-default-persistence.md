# Spec: `default-persistence`

**Module id:** `default-persistence` · **Program:** [../commander-surface-map.md](../commander-surface-map.md) ·
**Ideal:** [../commander-surface-ideal.md](../commander-surface-ideal.md)
**Depends on:** nothing · **Blocks:** `commander-list-api`, `match-snapshot`, `commanders-layer`,
`sanctum-readout`
**Status:** specced 2026-08-30 — strengthen pass 2026-08-30 — pending owner review. No build authorized.

---

## Assumptions

1. **`players` stays save-slots only** — no new columns on `players` (`RpgStore.cs:85-89`,
   `decisions.md` players row).
2. **Global `settings` is install-scoped**, not per-player prefs (`RpgStore.cs:90-94`) — wrong place
   for `defaultLawnCommanderId`.
3. **New table `rpg_player_commander`** — one row per player, PK `player_id`, mirrors `rpg_patron`
   (`RpgStore.cs:454-459`).
4. **No row = implicit `commander:dave`** — same empty-default pattern as patron read (no row → caller
   supplies default Dave at API layer; do not seed a row on first save read). **Not** the aptitude
   empty-allocation pattern (`RpgStore.Aptitudes.cs:105-108`) — aptitudes use a different store shape.
5. **Stable id strings** from `CommanderIds.ToStableId` (`CommanderId.cs:32-37`); validate on write via
   new `CommanderIds.TryParseStableId`.
6. **Zomboss is not a valid player default** — empire filter at API layer; store may reject
   `commander:zomboss` for player default writes.
7. **Invalid stored row** (unparseable id) → read path falls back to implicit Dave + log; do not crash
   GET.

---

## Objective

Persist which commander leads the **next** lawn run for each player save. The web FE and injector read
this field asynchronously; nothing gates play on opening the Commanders layer first.

**Success:** `SetDefaultLawnCommanderId(player, "commander:dave")` round-trips through DAL; absent row
reads as `"commander:dave"`; invalid ids reject without corrupting prior value; `Reset()` clears the
table.

---

## ⛔ Program acceptance share

Automated test: snapshot poll (or GET default) on a fresh save returns `"commander:dave"` without a seeded
row; after POST set, poll returns the saved id. This module is not done until that slice is green.

---

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests --filter FullyQualifiedName~PlayerCommander
dotnet test tests\FusionRpg.Server.Tests --filter FullyQualifiedName~CommanderDefault
.\scripts\guard-dal.ps1
```

---

## Project structure

| Path | Change |
|---|---|
| `src/FusionRpg.Core/Commanders/CommanderId.cs` | edit — add `TryParseStableId(string, out CommanderId?)` |
| `src/FusionRpg.Data/Sqlite/RpgStore.PlayerCommander.cs` | **new** — schema ensure, get/set, reset |
| `src/FusionRpg.Data/Sqlite/RpgStore.cs` | edit — `EnsureHotSchema` dispatch; `Reset()` **`DELETE FROM rpg_player_commander`** beside `rpg_patron` |
| `src/FusionRpg.Server/CommanderEndpoints.cs` | **new** — GET/POST default + list group (list-api owns file; this module owns default routes) |
| `src/FusionRpg.Server/Program.cs` | edit — `app.MapCommanders()` |
| `tests/FusionRpg.Data.Tests/Sqlite/PlayerCommanderStoreTests.cs` | **new** |
| `tests/FusionRpg.Server.Tests/CommanderDefaultEndpointsTests.cs` | **new** |

**Single endpoint file:** `CommanderEndpoints.cs` holds both default routes and the list route
(`commander-list-api` adds list handler to the same group). No separate `CommanderDefaultEndpoints.cs`.

---

## Design

### Schema

```sql
CREATE TABLE IF NOT EXISTS rpg_player_commander (
  player_id                   INTEGER NOT NULL PRIMARY KEY,
  default_lawn_commander_id   TEXT    NOT NULL,
  updated_utc                 TEXT    NOT NULL,
  revision                    INTEGER NOT NULL DEFAULT 0
);
```

Registered in `EnsureHotSchema` after loadout dispatch (`RpgStore.cs:612-613` pattern).

### Store API

```csharp
string GetDefaultLawnCommanderId(long playerId);
  // no row → CommanderIds.ToStableId(CommanderId.Dave)
  // corrupt row → Dave + log (do not throw on read)

(bool Ok, string Reason) SetDefaultLawnCommanderId(long playerId, string commanderStableId);
  // CommanderIds.TryParseStableId + empire-allowed; UPSERT with revision++
```

```csharp
public static bool TryParseStableId(string stableId, out CommanderId? id)
{
    // "commander:dave" / "commander:zomboss" only; unknown → false
}
```

Concurrency: `lock (_gate)` + transaction on write (match `RpgStore.Patron.cs` set pattern).

### REST (minimal — list module adds GET roster)

| Method | Path | Response / body |
|---|---|---|
| `GET` | `/api/commanders/{playerId:long}/default` | `{ defaultLawnCommanderId: "commander:dave" }` |
| `POST` | `/api/commanders/default` | `{ playerId?, commanderId }` → `{ defaultLawnCommanderId }` |

Conventions from `AptitudeEndpoints.cs` / `LoadoutEndpoints.cs`:

- `PlayerExists` → `404`
- Optional `playerId` in POST body, fallback `store.GetCurrentPlayerId()`
- Bad commander id → `400` `{ reason }`
- **No mid-run gate on this endpoint** — changing default affects next `board.start` only (ideal §2.1).
  Do not cite `LoadoutEndpoints.cs:26-35` mid-run oracle here; that gate applies to loadout writes, not
  default commander preference.

### Tunables

None. `"commander:dave"` as implicit default is a **structural** constant (first-save behavior), not a
balance tunable.

---

## Code style

Match existing server + DAL idioms:

- **DAL:** partial `RpgStore.*.cs` file; `EnsureHotSchema` registration; `Reset()` delete in the same
  `RpgStore.cs` batch as `DELETE FROM rpg_patron`.
- **REST:** `MapGroup("/api/commanders")`; thin handlers delegating to store; DTOs in
  `FusionRpg.Contracts` when shared with list-api.
- **Validation:** `TryParseStableId` in Core; empire allowlist in Server (Dave only v1).
- **Tests:** arrange store in SQLite test file; assert implicit Dave, round-trip, invalid write, reset.

---

## Testing strategy

| Layer | Tests |
|---|---|
| Data | Round-trip; no-row-default; invalid id rejected; corrupt row read → Dave; revision increments; reset clears |
| Server | 404 unknown player; GET implicit Dave; POST set + GET verify; POST invalid id 400 |
| Guard | `guard-dal.ps1` — SQL only in `FusionRpg.Data` |
| Share | Snapshot/GET poll test reads default without seeded row |

---

## Boundaries

- **Always:** SQL in `FusionRpg.Data` only; stable ids; implicit Dave when no row; single
  `CommanderEndpoints.cs` group
- **Ask first:** adding Penny to empire allowlist (content decision)
- **Never:** store loadout rows here; seed row on every new player; accept Zomboss as player default;
  block play when default unset; split default routes into a second endpoint file

---

## Success criteria

- [ ] Migration runs on existing hot DB via `CREATE TABLE IF NOT EXISTS`
- [ ] `CommanderIds.TryParseStableId` covers both stable ids
- [ ] `Reset()` includes `DELETE FROM rpg_player_commander`
- [ ] Data tests green for get/set/default/reset/corrupt-read
- [ ] Server tests green for GET/POST default
- [ ] ⛔ share: snapshot poll reads implicit or saved default
- [ ] `guard-dal.ps1` passes

---

## Open questions

None for this module — exact table name confirmed here; list API owns roster shape (`commander-list-api`).
