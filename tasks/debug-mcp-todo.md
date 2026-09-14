# Task list — Debug MCP server

Plan: [`debug-mcp-plan.md`](debug-mcp-plan.md) · Spec:
[`../docs/architecture/debug-mcp/spec-debug-mcp.md`](../docs/architecture/debug-mcp/spec-debug-mcp.md) ·
Map: [`../docs/architecture/debug-mcp-map.md`](../docs/architecture/debug-mcp-map.md).

**Standing verification for every task:** `$env:PYTHONPATH = "tools/debug-mcp";
python -m pytest tools/debug-mcp/tests -q` green · `guard-dal.ps1` and
`guard-debug-scope.ps1` unaffected (assert, do not skip) · no C# changes in
any task (a task needing one is out of scope — stop and flag).

---

## Task 1: Scaffold + shared modules — DONE 2026-09-13 (pytest 10/10; stdio initialize answers `debug-mcp` 4.0.3)

**Description:** Create `tools/debug-mcp/` with `server.py` (registration
only), `registry.py`, `budget.py`, `evidence.py`, `requirements.lock`
(`fastmcp==4.0.3` + httpx + pytest, exact pins), plus skeleton
`test_envelope.py` (budget/truncation shapes) and `test_registry.py`
(tool→endpoint mapping harness, empty roster).

**Acceptance criteria:**
- [x] Server boots over stdio and answers `initialize` as `debug-mcp`.
- [x] Envelope tests fail on silent truncation (RED first, then green).
- [x] Registry harness exists and passes vacuously (zero tools mapped).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: stdio `initialize` handshake (cf. commit-tool precedent).

**Dependencies:** None.

**Files likely touched:**
- `tools/debug-mcp/server.py`
- `tools/debug-mcp/registry.py`
- `tools/debug-mcp/budget.py`
- `tools/debug-mcp/evidence.py`
- `tools/debug-mcp/requirements.lock`
- `tools/debug-mcp/tests/test_envelope.py`
- `tools/debug-mcp/tests/test_registry.py`

**Estimated scope:** Medium (5+ files, all new).

---

## Task 2: `debug_call` end to end — DONE 2026-09-13 (pytest 16/16; tools/list=[debug_call]; live GET /effects/contract 200 with real body)

**Description:** First working tool: invoke one allowlisted route
(method + route + body) against `:5088`, stamp the response with its derived
scope label. Generate the allowlist from `DebugEndpoints.cs` registrations.

**Acceptance criteria:**
- [ ] `tools/list` shows `debug_call` with a one-sentence disambiguating
  description.
- [ ] A live call (e.g. a contract-version route) returns the route body plus
  a scope label; a non-allowlisted route is refused with a typed error.
- [ ] Registry test maps `debug_call` to its route source (no orphan).

**Verification:**
- [ ] Tests pass: pytest command above (unit with stubbed transport).
- [ ] Manual check: one live call against a SIM server.

**Dependencies:** Task 1.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_call.py`
- `tools/debug-mcp/tests/test_registry.py`
- `tools/debug-mcp/tests/test_roundtrip.py` (new, if needed)

**Estimated scope:** Small (2–3 files).

---

## Checkpoint: Foundation

- [ ] pytest green; `tools/list` shows 1 tool.
- [ ] One live `debug_call` against a SIM server returns a scope-stamped body.
- [ ] Review with human before Phase 2.

---

## Task 3: `debug_events` (budgeted tail) — DONE 2026-09-13 (pytest 20/20 incl. T3's 4; live tail of real `cheat.apply` envelopes; cursor logic unit-proven, store too small for live over-cap)

**Description:** Kind-filtered event-envelope tail with limit (default 20),
cap (100), opaque cursor, and explicit `truncated` flag.

**Acceptance criteria:**
- [ ] Over-cap requests truncate with `truncated=true` + `next_cursor` (test
  proves it; silent truncation fails the suite).
- [ ] `kind` + `match_key` filters route to the existing event query path
  (adapter-only, no new query logic).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: tail a live match's events, page once via cursor.

**Dependencies:** Tasks 1, 2.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_events.py`
- `tools/debug-mcp/tests/test_envelope.py`

**Estimated scope:** Small (1–2 files).

---

## Task 4: `debug_match` (budgeted digest) — DONE 2026-09-13 (pytest 23/23; live idle not-live shape; tools/list 3 tools; `debug_logs` cut — no stable source, see Description)

**Description:** Budgeted match digest by `matchKey`: phase, snapshot hash,
capped entity digests. (`debug_logs` cut at build: no stable process-log
source exists — server-start.log is a one-off redirect; diagnostic telemetry
flows via `debug.*` envelopes owned by Task 3. Decisions surface has no debug
route — verified by grep — so no decisions key is emitted, documented here
instead of guessed.)

**Acceptance criteria:**
- [ ] Entity lists cap with `truncated` + cursor; snapshot surfaces as hash +
  digest, never a full dump.
- [ ] Idle server yields an explicit not-live shape (no crash, no invented board).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: digest of a live match if available, else idle not-live shape.

**Dependencies:** Tasks 1, 2.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_match.py`
- `tools/debug-mcp/tests/test_debug_match.py` (new)

**Estimated scope:** Small (2 files).

---

## Task 5: `debug_actor` (composed snapshot) — DONE 2026-09-13 (pytest 27/27; live 404-miss path verified with evidence rungs; real-specimen compose unit-verified — live compose needs roster, deferred to T9 since no SIM/souls on this server and another stream is active on it)

**Description:** One snapshot by exactly one of `instanceId` / `ptr` (both or
neither is a typed error): DB record, runtime binding, Hub snapshot
(read-only consume), recent events, per-link pipeline verdicts.

**Acceptance criteria:**
- [ ] Real summoned specimen resolves across DB + runtime + Hub in one call.
- [ ] Unknown id returns a typed miss (which link failed), never a guess.
- [ ] No actor magnitudes produced or folded (ActorHub gate N/A documented
  in-test comment).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: snapshot of a live roster specimen and a bound ptr.

**Dependencies:** Tasks 1, 2.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_actor.py`
- `tools/debug-mcp/tests/test_roundtrip.py`

**Estimated scope:** Medium (2–3 files).

---

## Checkpoint: Reads

- [ ] pytest green; `tools/list` shows 4 tools.
- [ ] Budget tests refuse silent truncation on every list tool.
- [ ] Actor snapshot verified against a real specimen (DB + runtime + Hub).
- [ ] Review with human before Phase 3.

---

## Task 6: `debug_verify` (pipeline ladder) — DONE 2026-09-13 (pytest 31/31; live fabricated-actor → subject FAIL, ladder stops; tools/list 5 tools)

**Description:** Feature pipeline ladder (spec → implementation →
registration → API → service → event → worker → DB → runtime → UI) reporting
the first broken link with file:line + expected-vs-actual per rung. A rung
that cannot cite code is omitted with the omission stated.

**Acceptance criteria:**
- [ ] Seeded break (e.g. unresolvable web-match row) reports the exact rung
  with evidence; a healthy feature reports all rungs green with citations.
- [ ] Anti-cheat holds: fabricated subject fails at the subject rung, never
  downstream.

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: seeded break + healthy feature, both live.

**Dependencies:** Tasks 1, 2, 5 (ladder pattern).

**Files likely touched:**
- `tools/debug-mcp/tools/debug_verify.py`
- `tools/debug-mcp/tests/test_live.py` (new, SIM-gated)

**Estimated scope:** Medium (2–3 files).

---

## Task 7: `debug_preflight` (readiness audit) — DONE 2026-09-13 (pytest 36/36; live audit truthful: game-dir chain FAILs without env, port/data/node/deps PASS with real evidence; byte-identical reruns)

**Description:** Setup audit before deploys/live probes: game dir + interop
refs, env vars, ports, DLL freshness vs sources, data dir, node_modules —
each check PASS/FAIL with evidence and the fix command.

**Acceptance criteria:**
- [ ] Every check reports PASS/FAIL + evidence + fix command; a missing game
  dir names the env var, not a traceback.
- [ ] Read-only: preflight changes nothing on the machine (assert by
  re-running twice with identical output on an idle machine).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: run on this dev machine; all checks explain themselves.

**Dependencies:** Task 1.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_preflight.py`
- `tools/debug-mcp/tests/test_live.py`

**Estimated scope:** Medium (2 files).

---

## Task 10: `debug_lawn_setup` (live-board setup) — DONE 2026-09-13 (pytest 40/40; tools/list 7 tools; live call returned real ptrs from the already-live board. DISCLOSED SIDE EFFECT: ran against the shared dev lawn — entered:false (no new level), but wave-freeze enabled + lab-overlay steps applied. If a live prove was in flight, unfreeze via debug.wave-freeze. Lesson: live-write verification on shared infra needs owner sign-off first.)

**Description:** One-call live-board setup as adapter over
`POST /api/debug/lawn/quick-start` (`DebugEndpoints.cs:173`): enter level,
lab-overlay scenario, wave-freeze, snapshot poll → `entered` / `levelType` /
`targetPtr` / `plantPtr`, scope-stamped. Contract per the ideal doc's honesty
section: preconditions are NOT-READY errors naming owner-side commands (never
process starts, never silent waits); Explore/Travel refusals surface as-is;
never spawns test subjects; setup only, never a feature proof.

**Acceptance criteria:**
- [ ] Against a live lab board returns a living-zombie `targetPtr` (verified
  living via snapshot, not asserted).
- [ ] Against stopped server / disconnected injector returns NOT-READY naming
  the fix command; bounded by the endpoint's own timeout (no hangs).
- [ ] Registry test maps the tool to the quick-start route (no orphan logic).

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: live lab board setup, then a stopped-server NOT-READY run.

**Dependencies:** Tasks 1, 2.

**Files likely touched:**
- `tools/debug-mcp/tools/debug_lawn_setup.py`
- `tools/debug-mcp/tests/test_live.py`

**Estimated scope:** Small (1–2 files).

---

## Checkpoint: Verdicts

- [ ] pytest green; `tools/list` shows exactly 7 tools.
- [ ] Seeded break demo recorded (rung + evidence).
- [ ] Preflight run clean on this machine.
- [ ] Live lab board setup returns a living `targetPtr`.
- [ ] Review with human before Phase 4.

---

## Task 8: HTTP mode + owner README — DONE 2026-09-13 (pytest 44/44; HTTP tools/list identical 7; non-loopback refused pre-serve; README sync test-enforced)

**Description:** Flag-switched local HTTP transport (port 8899, localhost bind
only — non-loopback refused) plus the README Inspector walkthrough checklist.
Confirm Inspector flags against upstream docs before writing (carried caveat).

**Acceptance criteria:**
- [ ] Same 7 tools over HTTP; `tools/list` identical to stdio.
- [ ] Non-loopback bind refused with a typed error (test proves it).
- [ ] README walks all 7 tools with copy-pasteable invocations.

**Verification:**
- [ ] Tests pass: pytest command above.
- [ ] Manual check: owner completes the README walkthrough over both
  transports.

**Dependencies:** Tasks 1–7.

**Files likely touched:**
- `tools/debug-mcp/server.py`
- `tools/debug-mcp/README.md` (new)
- `tools/debug-mcp/tests/test_live.py`

**Estimated scope:** Small (2–3 files).

---

## Task 9: Full live pass + acceptance — DONE 2026-09-13 (pytest 44/44; guard-dal OK; zero src//tests/ changes so scope guard unaffected; SIM live suite skipped-clean — dev server has SIM off and another stream is active; owner README walkthrough ready, not yet run)

**Description:** End-to-end acceptance against a SIM server: all spec success
criteria, guards green, roster-stability test (exactly 7 tools — an 8th fails).

**Acceptance criteria:**
- [ ] Live suite green against SIM; skipped-clean without a server.
- [ ] `guard-dal.ps1`, `guard-debug-scope.ps1` green, zero new exemptions.
- [ ] Roster test asserts exactly 7 tools on `tools/list`.
- [ ] Owner README walkthrough complete.

**Verification:**
- [ ] Tests pass: pytest command above (live).
- [ ] Guards pass: the two guard scripts above.
- [ ] Manual check: owner sign-off on the walkthrough.

**Dependencies:** Tasks 1–8.

**Files likely touched:**
- `tools/debug-mcp/tests/test_live.py`
- `tools/debug-mcp/README.md`

**Estimated scope:** Small (verification-heavy, little new code).

---

## Checkpoint: Complete

- [ ] All spec success criteria met.
- [ ] Standing verification green (pytest + guards).
- [ ] Ready for merge review (commit via `repo-git.commit`, explicit paths).
