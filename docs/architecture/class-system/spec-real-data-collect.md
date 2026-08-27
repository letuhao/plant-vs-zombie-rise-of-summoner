# Spec: `real-data-collect` — the durable store Phase 9's loop reads from

**Module id:** `real-data-collect` · **Program:** [class-system-map.md](../class-system-map.md) ·
**Status: AUTHORIZED 2026-08-27 — owner selected "pick the Phase 9 storage option now" via `/goal`'s
own `AskUserQuestion` gate, explicitly delegating the choice among the three options below to this
session's own engineering judgment.** Option **B** (file-based, append-only log) chosen — reasoning in
§4. Unlike this program's other 12 module specs, this one did not exist when the plan's 12-module scope
table (`class-system-plan.md` §2) was authorized; added same day P9.0-P9.4's own investigation found
its shape was knowable even though Phase 9's own "cannot start until the complete build" gate had not
opened.

**Depends on:** `residual-fit` (reuses `tools/ResidualFitLoop`'s fit/publish machinery unchanged) ·
**Blocks:** P9.1-P9.4, Checkpoint 9.

---

## 1. Why this needed a decision before code

AGENTS.md's own hard boundary: **"Architecture changes that lock behavior need `decisions.md` first."**
`spec-residual-fit.md` §5 scopes `residual-fit` itself to **"ships no `src/` code"** — Phase 9's own
storage mechanism was never in that module's remit, and the plan's 12-module table had no 13th entry
for it. This is why `P9.1 — Collect real-run metrics` was found genuinely blocked on 2026-08-27, and why
this doc exists rather than a straight-to-code decision.

## 2. What already exists — corrected 2026-08-27, the scope is narrower than first found

The telemetry TRANSPORT already ships and needs no new architecture:

```
PerfProbe.SnapshotAndReset()          src/FusionRpg.Core/Diagnostics/PerfProbe.cs:159
  -> PerfReporter.Flush               src/FusionRpg.Injector/Host/PerfReporter.cs
  -> POST /api/perf                   src/FusionRpg.Server/PerfEndpoints.cs
  -> PerfWindowBuffer (in-memory)     src/FusionRpg.Server/PerfEndpoints.cs
  -> GET /api/perf/recent             src/FusionRpg.Server/PerfEndpoints.cs
```

**First pass over-claimed a gap.** `PerfProbe.cs:230` (past where an earlier grep of this file stopped
reading) sets `["t"] = DateTime.UtcNow.ToString("o")` inside `SnapshotAndReset()` — every window already
carries a wall-clock timestamp, and `scripts/probe-perf.ps1` (the perf program's own baseline-capture
script) already dedups incoming windows on it. Found by reading `probe-perf.ps1` for a design reference,
not by re-grepping the same two files a third time.

What is **still** genuinely missing, narrower than first written:

1. **No run/session id** — `.t` identifies a WINDOW, nothing groups windows into "this play session."
2. **The ring buffer's own eviction is invisible from outside it** — `POST /api/perf`'s `{ok, count}`
   response reports current occupancy (capped at 240, ~20 minutes of 5s windows), never a lifetime-
   emitted total, so a poller that falls behind loses windows with no signal anywhere that it happened.

**Both are addressable entirely from the CONSUMER side, reading only the already-public `GET /api/perf/
recent` — no change to `PerfProbe.cs`/`PerfReporter.cs`/`PerfWindowBuffer.cs` needed.** This removes
what was originally this doc's second reason to need a decision (editing another program's own files
without their review, `perf-probe-plan.md` §1.4) — the collector this spec authorizes is a new,
class-system-owned reader of an existing public endpoint, the same relationship `probe-perf.ps1` itself
already has to it, just continuous and multi-run instead of one fixed-duration capture.

## 3. Options considered

**A. A new `FusionRpg.Data` table**, matching `RpgStore.Aptitudes.cs`'s own established partial-class
convention (P6.2). Durable, queryable, but real new `src/` architecture with its own schema (run
identity, raw-vs-aggregate storage, an unbounded-growth retention policy) — a bigger, more permanent
commitment than the gap actually requires.

**B. A file-based, append-only log (chosen)** — one JSONL file per collection run, under
`docs/research/class-system/real-runs/`, matching the ALREADY-ESTABLISHED `_baseline-*.json`/
`docs/research/` convention this program already uses throughout Phase 8, and `spec-residual-fit.md`
§5's own "ships no `src/` code" pattern (`tools/ResidualFitLoop`/`tools/DominanceBaseline` are the
precedent: measurement tools, not persistence layers).

**C. Extend `PerfWindowBuffer` itself.** Ruled out once §2's correction landed — it would touch the perf
program's own file for no remaining benefit this design doesn't already get by reading `.t` externally.

## 4. Why B, not A

Every acceptance-line requirement is met by B without opening a SQL schema question that would outlive
this specific need: **"durable"** — a file on disk survives a server restart, unlike the in-memory ring
buffer. **"source-tagged by run"** — the collector assigns a run id (a GUID, at invocation) and stamps
every line with it; the file's own name doubles as the tag. **"sampling is bounded and the drop rate is
itself a reported metric"** — the collector dedups on the window's own `.t`, and computes an estimated
drop count from gaps between consecutive captured `.t` values against the known ~5s cadence
(`NetPolicy.Tuning.PerfReporter.IntervalSeconds`, already public config) — reported in the same file's
own summary, not silently swallowed. A SQLite table (A) would need to solve the identical identity/
drop-rate questions on top of a schema-migration commitment neither this task nor Checkpoint 9 asks for;
a JSONL log needs none of that, and is trivially the input `P9.2`'s aggregation step reads from — one
file in, one aggregate out, the same shape every measurement tool in this program already takes.

## 5. What does NOT need this decision, and stays unbuilt here

`P9.3`'s own fit/publish half needs no new building: `tools/ResidualFitLoop` (P8.6/P8.7, already
shipped) already implements run→metrics→aggregate→fit→publish against simulated input, reserved-
coefficient-aware, with a post-publish termination re-check. Once P9.1/P9.2 produce a real aggregate,
P9.3 is "point the existing tool at the new input and re-verify," not new machinery.
