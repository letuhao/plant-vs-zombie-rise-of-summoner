# Persistence implement checklist

**Status:** A–E + W12 complete — user-driven Storage clear (no auto GC).  
**Sealed defaults (do not reopen without a new decision):** activity retainTail **10 000**, XP retainTail **5 000**, KeepLastN **50**, cold archive **on**, DAL = all SQL in `FusionRpg.Data`, `scripts/guard-dal.ps1`.

## One-line map

| Slice | Waves | Outcome |
|---|---|---|
| A–E | W0–W11 | Cutover live (DAL, hot/media, watermarks, cold archive, deploy guard) |
| W12 | Storage | User Storage page + `/api/storage/*` purge; no auto GC; `IColdPathQuery` / `IGarbageCollector` stay stubs |

## Wave order

```text
W0 → W11 (slices A–E)
W12 — user Storage clear (no auto GC)
```

Deep multi-archive cold-path query fan-in into Log remains out of scope.  
**Authority:** this file + [ledger-snapshot.md](ledger-snapshot.md).  
**Incident / restore:** [persistence-refactor-blast-radius.md](persistence-refactor-blast-radius.md).

## Preconditions (live today)

| Item | State |
|---|---|
| DB | `{data}/rpg-hot.sqlite` + `{data}/rpg-media.sqlite`; legacy `rpg.sqlite` migrates once (bak, never auto-deleted) |
| SQL | Only in `src/FusionRpg.Data` (`RpgStore*`, factory, `LegacyMonoMigrator`, cold archive/compactor); Server has zero Sqlite |
| Activity/XP | Watermarks + post-run archive/trim (retain 10k / 5k); capture KeepLastN=50 via cold move under `{data}/archive/` |
| Core | `PvzActivityRollupBuilder.ApplyDelta` ≡ `Build`; `SimEngine` clears baselines on BoardStart/End |
| `scripts/guard-dal.ps1` | Present; enforced via Guard.Tests **and** `deploy-play.ps1` (Slice E) |
| `FusionRpg.Data` | Exists; Server ProjectReferences it |

## Locked product defaults

| Knob | Value |
|---|---|
| Activity retainTail | 10 000 / player |
| XP retainTail | 5 000 / actor |
| KeepLastN full capture runs | 50 |
| Capture strategy | Cold **move** (archive before hot delete); not noisy-delete-first |
| Cold archive | ON v1 |
| Compact timing | Post-run + over limit only; **never mid-run** |
| DAL gate | All SQL only in `FusionRpg.Data`; empty allowlist end state |
| Storage clear (W12) | User-driven `/storage` + `/api/storage/*`; **no auto GC** |
| Deferred | Deep cold-path query fan-in into Log (`IColdPathQuery` stub) |

## Cutover strategy

1. Introduce `FusionRpg.Data`, **move** `RpgStore*` into it (type name `RpgStore`, namespace `FusionRpg.Data`). Server drops Sqlite package.
2. Ctor takes `dataDir` → `rpg-hot.sqlite` + `rpg-media.sqlite`; migrator from legacy `rpg.sqlite`.
3. Incremental rollups / watermarks / cold archive / post-run worker.
4. Hook `guard-dal.ps1` into deploy-play only when Server has zero SQL.

```text
W0 → W1 → W2 → W3
         ↘ W5 / W6 / W7
W4 → W5 → W8
W6 → W8
W7 → W8 → W10
W7 → W9 → W10
W2…W10 → W11
W12 — user Storage clear (no auto GC)
```

## Execution rule

Slices A–E and W12 are done. Further persistence work needs an explicit new decision (no auto GC / cold-path Log fan-in without approval).

---

## Suggested PR slices

| Slice | Work items | Ship gate |
|---|---|---|
| **A** | W0–W2 | DAL move; behavior unchanged; `guard-dal.ps1` passes |
| **B** | W3 | Media split + migrator |
| **C** | W4–W6 | Snapshots + incremental rollups |
| **D** | W7–W10 | Cold archive + post-run worker |
| **E** | W11 | Guard in deploy + protocol note + e2e; flip blast-radius stop line |
| **W12** | Storage | User Storage clear APIs + `/storage` page; no auto GC |

W12 shipped as its own session after A–E (user purge UI; not background GC).

---

## Work items

### W0 — Docs polish (pre-code)

| | |
|---|---|
| **Goal** | Docs consistent (live vs sealed target); this checklist linked |
| **Touch** | `docs/database/*`, `docs/README.md`, `docs/architecture/decisions.md` |
| **Steps** | Confirm ledger-snapshot / schema / decisions label live vs target; ensure checklist + README links |
| **Acceptance** | Docs consistent; no feature code |
| **Blast** | none |
| **Slice** | A |

### W1 — `FusionRpg.Data` skeleton

| | |
|---|---|
| **Goal** | Empty DAL project builds; Sqlite package only here |
| **Touch** | `src/FusionRpg.Data/FusionRpg.Data.csproj`, `Sqlite/SqliteConnectionFactory.cs`, `Policies/SealedCompactionPolicy.cs`, `Abstractions/*` |
| **Steps** | Create project; refs Contracts/Core/CheatCore; add factory (WAL every open), policy constants (10k/5k/50), `IRpgDb`, `IColdArchiveWriter`, `IHotCompactor`, `IColdArchiveCatalog`, stubs `IColdPathQuery` / `IGarbageCollector` (`IsImplemented=false`) |
| **Acceptance** | `dotnet build` Data succeeds; Server still unchanged |
| **Blast** | low |
| **Slice** | A |

### W2 — Move all SQL out of Server

| | |
|---|---|
| **Goal** | Zero SQL / Sqlite types in Server; behavior identical |
| **Touch** | Move `RpgStore.cs`, `RpgStore.Progression.cs`, `RpgStore.Icons.cs`, `RpgStore.Almanac.cs` → Data; `FusionRpg.Server.csproj`; `Program.cs`, Hub, EventIngest, TypeIconStore, Debug/Sim endpoints (`using FusionRpg.Data`) |
| **Steps** | Move files; namespace `FusionRpg.Data`; remove Server Sqlite PackageReference; add Data ProjectReference; keep type name `RpgStore`; still single `rpg.sqlite` path until W3 |
| **Acceptance** | `scripts/guard-dal.ps1` passes (empty allowlist); Server + E2E smoke green; play/ingest unchanged |
| **Blast** | high (compile surface) — dedicated PR |
| **Slice** | A |

### W3 — Media split + mono migrator

| | |
|---|---|
| **Goal** | BLOBs out of hot DB |
| **Touch** | RpgStore Init/Open, Icons/Almanac repos, `Program.cs` (`dataDir`), migrator class |
| **Steps** | Open `rpg-hot.sqlite` + `rpg-media.sqlite`; if legacy `rpg.sqlite` exists and hot missing → backup `.pre-dal.bak`, copy/split (ask before delete original); media APIs use media connection; never share ingest txn |
| **Acceptance** | Fresh install creates two files; migrator moves icon/almanac BLOBs; ingest works; icons/almanac serve |
| **Blast** | medium (disk layout); no destructive delete without ask |
| **Slice** | B |

### W4 — Core policy + ApplyDelta tests

| | |
|---|---|
| **Goal** | Delta rollup proven equivalent to full Build |
| **Touch** | `FusionRpg.Core` Activity; `FusionRpg.Core.Tests` |
| **Steps** | Keep/align sealed constants; unit tests: random/sequence facts → `ApplyDelta` loop ≡ `Build` |
| **Acceptance** | Core.Tests green for delta equivalence |
| **Blast** | low |
| **Slice** | C |

### W5 — Activity watermark + incremental rollup

| | |
|---|---|
| **Goal** | No full-fact rescan on ingest hot path |
| **Touch** | Activity rollup SQL in RpgStore; schema EnsureColumn |
| **Steps** | Add `through_fact_id`, `schema_version` on `pvz_activity_rollups`; load counters + `ApplyDelta` per new fact; stamp watermark; repair path may full rebuild |
| **Acceptance** | Ingest does not `SELECT` all facts; watermark monotonic; repair rebuild works |
| **Blast** | medium |
| **Slice** | C |

### W6 — XP watermark + chart buckets

| | |
|---|---|
| **Goal** | XP ledger trimmable without breaking charts |
| **Touch** | `RpgStore.Progression.cs`; schema |
| **Steps** | Add `through_ledger_id`, `xp_by_reason_json` on `rpg_actor_progression`; update award path; stats can use buckets after trim |
| **Acceptance** | Awards unchanged; watermark advances; stats/charts survive trim |
| **Blast** | medium |
| **Slice** | C |

### W7 — Cold archive writer + catalog

| | |
|---|---|
| **Goal** | Archive-before-delete for closed-run capture |
| **Touch** | New Sqlite cold archive writer; hot `archive_catalog` and/or `runs.archive_uri`; `archive/` under data dir |
| **Steps** | `PromoteClosedRunCapture(runId)`: copy events + spawn_stats (+ entities/mowers as designed) → verify → set `archive_uri` → delete hot capture rows; **refuse** if `ended_utc` IS NULL |
| **Acceptance** | Promote leaves run row; archive file readable; open run refused |
| **Blast** | high (data move) — backup first |
| **Slice** | D |

### W8 — Post-run Activity/XP archive+trim

| | |
|---|---|
| **Goal** | Bound hot ledger tails after run end |
| **Touch** | HotCompactor; Activity/XP repos |
| **Steps** | If count > retainTail: archive overflow segment → verify → trim hot to retainTail; refuse without snapshot cover / without archive OK |
| **Acceptance** | After forced overflow + post-run: hot count ≤ retainTail; snapshot counters/levels unchanged |
| **Blast** | high |
| **Slice** | D |

### W9 — Capture KeepLastN = 50

| | |
|---|---|
| **Goal** | At most 50 closed full-capture runs on hot |
| **Touch** | Capture promote loop |
| **Steps** | On run close: while closed full-capture runs on hot > 50, promote oldest closed (not open) |
| **Acceptance** | Never strips open run; hot closed full-capture count ≤ 50 |
| **Blast** | high |
| **Slice** | D |

### W10 — CompactionWorker (run-end / limit only)

| | |
|---|---|
| **Goal** | No mid-run compact/archive |
| **Touch** | New `CompactionWorker` BackgroundService; EventIngest / run-close notify |
| **Steps** | Queue work on `board.end` only (sets `ended_utc`); drain post-run; never promote open runId |
| **Acceptance** | Integration/unit proves mid-run path never promotes open run |
| **Blast** | medium |
| **Slice** | D |

### W11 — Gate + e2e + protocol

| | |
|---|---|
| **Goal** | Hard DAL gate in deploy; docs match live cutover |
| **Touch** | `scripts/deploy-play.ps1`, `docs/protocol/rest.md`, blast-radius stop line, E2E |
| **Steps** | Hook `guard-dal.ps1`; empty allowlist; note ledger/activity APIs = hot tail (+ compacted note); mark blast-radius cutover complete when green |
| **Acceptance** | Deploy-play fails if SQL reappears in Server; E2E + Core green |
| **Blast** | low (process) |
| **Slice** | E |

### W12 — User-driven storage clear (no auto GC)

| | |
|---|---|
| **Goal** | Player lists and deletes selected cold archives / closed-run capture from Web Storage; no background archive GC |
| **Touch** | `RpgStore.Storage.cs`, `StorageEndpoints.cs`, web `StoragePage`, docs, Data + E2E tests |
| **Steps** | Purge helpers (path-safe archive delete; refuse open runs); REST `/api/storage/*`; FE multi-select + confirms; `TrimHotTailsNow` = user-triggered compact; leave `IGarbageCollector` / `IColdPathQuery` stubs |
| **Acceptance** | `/storage` lists + deletes selected targets; open runs never deleted; no auto-clean; tests green |
| **Blast** | medium (destructive clears are explicit + confirmed) |
| **Slice** | W12 |

---

## Test matrix

| Test | Assert |
|---|---|
| ApplyDelta ≡ Build | Same counters for identical fact sequences |
| Archive refuse | No snapshot cover / no successful archive write → no hot delete |
| Open run | Promote refused when `ended_utc` null |
| Capture bound | Closed full-capture on hot ≤ 50 after post-run |
| Activity/XP tails | Hot counts ≤ 10 000 / 5 000 after post-run trim |
| Guard | `guard-dal.ps1` fails if Server contains Sqlite/SQL |
| Storage purge | Open run purge/delete refused; archive delete removes catalog + file; path escape refused |
| E2E | WebApplicationFactory + temp `FUSIONRPG_DATA`; storage summary/archives/purge |

## Non-goals

- Postgres
- Mid-run compaction
- Noisy-delete-first as primary capture strategy
- Multi-file WAL atomic ingest
- Automatic / scheduled archive GC
- Deep cold-path query fan-in of archived events into Log UI

## Checklist progress (fill when coding)

| ID | Slice | Done |
|---|---|---|
| W0 | A | yes |
| W1 | A | yes |
| W2 | A | yes |
| W3 | B | yes |
| W4 | C | yes |
| W5 | C | yes |
| W6 | C | yes |
| W7 | D | yes |
| W8 | D | yes |
| W9 | D | yes |
| W10 | D | yes |
| W11 | E | yes |
| W12 | W12 | yes |

**Slice A status:** done (2026-08-16). DAL move; SQL only in `FusionRpg.Data`.  
**Slice B status:** done (2026-08-16). Hot + media files + `LegacyMonoMigrator`.  
**Slice C status:** done (2026-08-16). Activity/XP watermarks + incremental ApplyDelta; buckets for charts.  
**Slice D status:** done (2026-08-16). Cold archive promote + Activity/XP trim + KeepLastN=50 + `CompactionWorker`.  
**Slice E status:** done (2026-08-16). `guard-dal.ps1` hooked in `deploy-play.ps1`; protocol hot-tail notes; blast-radius cutover complete.  
**W12 status:** done (2026-08-16). User Storage clear (`/api/storage/*` + `/storage`); no auto GC.
