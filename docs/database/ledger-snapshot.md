# Ledger + snapshot + cold archive

**Status: LIVE (A–E + W12)** — hot/media + DAL, watermarks, cold archive / post-run compact, `guard-dal` in deploy-play, and **user-driven Storage clear** are live. Auto archive GC and deep multi-archive query remain out of scope (`IColdPathQuery` / `IGarbageCollector` stubs).  
See [persistence-refactor-blast-radius.md](persistence-refactor-blast-radius.md) for historical abort record / cutover note.

**Live today:** `{ServerExeDir}/data/rpg-hot.sqlite` + `rpg-media.sqlite` + `archive/*`, SQL in `FusionRpg.Data` (`RpgStore*`, `ColdArchiveWriter`, `HotCompactor`, `RpgStore.Storage`). Activity ingest uses `ApplyDelta` + `through_fact_id`; XP awards maintain `through_ledger_id` + `xp_by_reason_json`. Post-run `CompactionWorker` promotes closed capture beyond KeepLastN and trims Activity/XP tails after snapshot-verified archive. Players clear selected archives / closed-run capture from Web `/storage` (no background GC). Legacy `rpg.sqlite` migrates once (bak; never auto-deleted).

**This document** is the sealed design for live persistence; remaining non-goals: scheduled archive GC and Log fan-in of archived events.

## Hard rule (live)

Any database read/write must go through `FusionRpg.Data`. No exceptions. Enforced by `scripts/guard-dal.ps1` in Guard.Tests and `deploy-play.ps1` (Slice E).

## File layout (live)

| File | Role |
|---|---|
| `rpg-hot.sqlite` | Projections, snapshots, ledger tails, last 50 full-capture runs |
| `rpg-media.sqlite` | Icon / almanac BLOBs (separate connection; never in ingest txn) |
| `archive/*` | Cold segments written **before** hot delete |

Legacy `rpg.sqlite` migrates once into hot+media on open when hot is missing (bak; never auto-deleted). See [persistence-implement-checklist.md](persistence-implement-checklist.md).

## Sealed limits

| Stream | Hot retain |
|---|---|
| Activity facts | 10 000 / player |
| XP ledger | 5 000 / actor |
| Full capture runs | 50 closed runs on hot |

## Lifecycle

1. Append ledger (mid-run → hot only)
2. Update durable snapshot / projection
3. On **run end**, if over limit: write cold archive → verify → trim hot
4. **Never** compact/archive mid-run

## Streams

### Activity

- Snapshot: `pvz_activity_rollups` + `through_fact_id` + `schema_version`
- Incremental `PvzActivityRollupBuilder.ApplyDelta` on the ingest path
- Over retainTail after run end → archive then trim

### XP

- Snapshot: `rpg_actor_progression` + `through_ledger_id` (+ `xp_by_reason_json` buckets)
- Over retainTail after run end → archive then trim

### Capture

- Cold **move** (not noisy-delete-first)
- Oldest closed runs beyond KeepLastN promoted to `archive/`
- `runs.archive_uri` points at archive file; run row stays on hot
- User purge: Web `/storage` + `/api/storage/*` (delete archives / purge or delete closed runs / trim tails)
- Deep cold-path query + auto GC: out of scope (`IColdPathQuery` / `IGarbageCollector` stubs)

## Invariants

1. Append-only until archive+trim
2. Snapshot sequence monotonic
3. Delete from hot only after successful archive write
4. Rebuild = snapshot + hot tail
5. No mid-run compact/archive

## Compaction trigger

```text
ON run closed:
  if activity count > 10000 → archive+trim
  if xp tail > 5000 → archive+trim
  if closed full-capture runs > 50 → promote oldest
NEVER while run open
```
