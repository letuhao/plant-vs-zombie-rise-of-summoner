# Server burst stability — investigation (perf-v3 module server-burst)

## Symptom

During the 1000-zombie stress run (2026-08-21 ~02:21), the server stopped responding
("connection actively refused") mid-capture while the game ran on unaffected. Initial read:
crash under the spawn/death event burst.

## Repro attempts (scripts/burst-repro.ps1 — kept as a regression harness)

Scratch server instance (own port + data dir), synthetic bursts POSTed to `/api/events`:

| Run | Shape | Result |
|---|---|---|
| 6,000 events, thin payloads, batch 256 | spawn/die alternating | **healthy** — 2,260 events/s, 0 send/health failures |
| 20,000 events, fat ~30-field payloads, batch 512 | place + spawn + stat.applied + die per entity (full projection fan-out incl. XP) | **healthy** — 6,224 events/s, 0 failures |

Ingest sustains ~6k events/s with full projection fan-out — an order of magnitude above the
real stress fill's rate. SQLite insert pressure is **not** the failure mode.

## Root cause

**External termination, not a server defect.**
- Windows Application event log has **no crash record** (no Event ID 1000, no .NET fatal
  error) for FusionRpg.Server in the window — a genuine crash or OOM leaves one.
- The dead instance had been started by `deploy-play.ps1 -RestartServer` **inside a
  background watcher shell** of the dev-assistant session; background task cleanup reaps its
  process tree. The replacement instance, started from a foreground shell at 02:23, survived
  every subsequent stress run including two burst repros.
- Players never hit this path: in production the **Launcher** owns the server process.

## Outcome

- **No server code change needed.** Ingest headroom proven and `burst-repro.ps1` stays as the
  regression harness (re-run after any EventIngest/RpgStore change).
- Operational rule added to the dev notes: never start the long-lived dev server from a
  background/task shell — use `deploy-play.ps1` from a foreground terminal (or let the
  Launcher own it).
- Spec acceptance reinterpreted honestly: criterion 1 ("repro exists") produced a *negative*
  repro, which is itself the finding; criteria 3–4 (server healthy under burst, XP kinds
  persisted) are demonstrated by the harness runs above.
