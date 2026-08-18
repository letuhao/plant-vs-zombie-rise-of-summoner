# Persistence refactor — blast radius (incident)

**Date:** 2026-08-16  
**Status:** CUTOVER COMPLETE (slices A–E + W12). §§1–4 / §6 remain a historical abort record. W12 is **user Storage clear** (no auto GC); deep cold-path query stays deferred.

## 1. Incident summary

Intent for that session was **document the sealed design only** (ledger + snapshot + DAL gate + cold archive), not implement.

Implementation started early anyway (create `FusionRpg.Data`, move `RpgStore*`). Work aborted mid-flight and left the tree **unbuildable**: Server still expected `RpgStore` in `FusionRpg.Server`, but sources lived under an unfinished `FusionRpg.Data` project with no Server `ProjectReference`.

This document records the blast radius. Restore steps put the code back to a working pre-cutover state while keeping sealed **design** docs as **target**, not live.

## 2. File inventory (as of abort)

### Moved (Server → Data/Sqlite)

| File | From | To (at abort) |
|---|---|---|
| `RpgStore.cs` | `src/FusionRpg.Server/` | `src/FusionRpg.Data/Sqlite/` |
| `RpgStore.Progression.cs` | same | same |
| `RpgStore.Icons.cs` | same | same |
| `RpgStore.Almanac.cs` | same | same |

Namespace had been changed to `FusionRpg.Data` while Server still compiled against `FusionRpg.Server.RpgStore`.

### Added (incomplete scaffold)

| Path | Notes |
|---|---|
| `src/FusionRpg.Data/FusionRpg.Data.csproj` | Not referenced by Server |
| `src/FusionRpg.Data/Abstractions/PersistenceContracts.cs` | `IRpgDb`, cold archive stubs |
| `src/FusionRpg.Data/Abstractions/DeferredColdPath.cs` | Deferred GC/query stubs |
| `src/FusionRpg.Data/Policies/SealedCompactionPolicy.cs` | Tail constants 10k / 5k / 50 |
| `src/FusionRpg.Data/Sqlite/SqliteConnectionFactory.cs` | WAL pragmas helper |
| `docs/database/ledger-snapshot.md` | Sealed design (keep as target) |
| `scripts/guard-dal.ps1` | Future DAL gate (not wired until cutover) |

### Modified (persistence-related)

| Path | Change |
|---|---|
| `docs/architecture/decisions.md` | DAL / hot+media / cold archive rows (must stay labeled **target**) |
| `docs/database/schema.md` | Header mentioned `rpg-hot` / media before code existed |
| `docs/README.md` | Link to ledger-snapshot |
| `scripts/deploy-play.ps1` | Called `guard-dal.ps1` (too early — breaks deploy while SQL is still in Server) |
| `src/FusionRpg.Core/Activity/PvzActivityKinds.cs` | `ApplyDelta` added; `Build` uses it (pure; kept) |

### Not touched by this abort (important)

- Live DB files under `data/rpg.sqlite` / `dist/.../rpg.sqlite` — **no migrator ran**
- No cold `archive/` writes
- No CompactionWorker
- No watermark columns applied to production schema via a shipping binary
- No E2E / Injector / Web protocol code for persistence cutover
- Sealed design plan file not edited by the restore pass

### Unrelated dirty tree (out of scope)

Other local edits (effect docs, `match-runtime.md`, web `main.tsx`, etc.) are **not** part of this incident. Do not mix them into restore unless asked.

## 3. Build / runtime impact

| Area | Impact at abort |
|---|---|
| `FusionRpg.Server` | **Broken** — missing `RpgStore` types |
| E2E (`WebApplicationFactory`) | **Broken** (depends on Server) |
| Injector / Contracts / CheatCore | Unaffected |
| Core | Builds; `ApplyDelta` existed but unused by Server path **at abort time** (now live on ingest — see §5) |
| Deploy-play | Would fail DAL guard once Server SQL restored, if guard stayed hooked |
| Player play session DB | **Safe** — no schema migrator executed against disk |

## 4. Data risk

**None to existing SQLite files** from the aborted implement: no `rpg-hot` / `rpg-media` split, no archive promote, no DELETE compact against live data.

## 5. Sealed design vs live code

| Topic | Live (current) | Remaining |
|---|---|---|
| DB files | `rpg-hot.sqlite` + `rpg-media.sqlite` + `archive/*` | Deep cold-path query / auto GC (non-goals) |
| SQL location | `FusionRpg.Data` only; `guard-dal.ps1` in Guard.Tests **and** `deploy-play.ps1` | — |
| Activity rollup | Incremental `ApplyDelta` + watermark; post-run archive/trim ≤10k | — |
| XP charts | Buckets + watermark; post-run archive/trim ≤5k/actor | — |
| Capture growth | Cold move; KeepLastN=50 closed full-capture on hot | — |
| Compact timing | `CompactionWorker` post-run only; open promote refused | — |

Canonical write-up: [ledger-snapshot.md](ledger-snapshot.md).

## 6. Restore performed (this pass) — historical abort restore

> **Historical:** describes the abort/restore that rolled back an incomplete DAL move. **Not** the live post–Slice E layout (see §5).

1. Moved `RpgStore*.cs` back to `src/FusionRpg.Server/`, namespace `FusionRpg.Server`
2. Deleted incomplete `src/FusionRpg.Data/`
3. Unhooked `guard-dal.ps1` from `deploy-play.ps1` (script kept for future cutover; header notes when to enable)
4. Relabeled docs: **live** vs **sealed target**
5. Verified: `dotnet build` FusionRpg.Server + FusionRpg.Core — **succeeded** (0 errors)

## 7. Stop line

A–E cutover is complete. W12 user Storage clear is live (`/api/storage/*`, Web `/storage`). Do **not** implement automatic archive GC or multi-archive Log fan-in without a new decision.

Checklist: [persistence-implement-checklist.md](persistence-implement-checklist.md).

`guard-dal.ps1` is hooked in `deploy-play.ps1` (Slice E / W11).

## 8. Remaining (non-goals unless reopened)

- Production `IColdPathQuery` fan-in of archived events into Log
- Scheduled / automatic `IGarbageCollector` archive cleanup

User purge goes through Storage APIs only. Stubs remain `IsImplemented=false`.
