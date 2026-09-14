# Spec: Debug MCP server (`tools/debug-mcp/`)

**Status:** spec, 2026-09-13. Implements `docs/architecture/debug-mcp-ideal.md`
(idea, enriched). No code authorized until this spec is approved.

**Reading gate (DESIGN-GATE §1, satisfied in-session before writing):**
`the-game.md`, `the-loops.md`, CLAUDE.md RPG-layer rule, `validation-ssot.md`,
`live-probe-standard.md` (§§1–4 head), `data-architecture.md`, `decisions.md`,
`software-architecture.md`. Not read: `live-probe-standard.md` tail,
`protocol/rest.md` contents (routes referenced by index only). No box ticked on
unread material.

## Objective

Build a per-session MCP server that gives repo agents (primary users) and the
owner via MCP Inspector (secondary user) eight unified, scope-labeled,
budget-enforcing diagnostic tools over the real running system — replacing
ad-hoc inspection scripts and closing the fabricated-evidence failure class of
2026-09-13. Success looks like:

- `tools/list` returns exactly the 7 approved tools, each with a one-sentence
  disambiguating description and a scope label in its metadata.
- Every list-shaped response honors limit (default 20) / cap (100) / cursor /
  `truncated`, verified by tests that fail on silent truncation.
- A seeded pipeline break (e.g. an unresolvable web-match row) is reported by
  `debug_verify` with the first broken rung named plus file:line evidence —
  never a bare PASS/FAIL.
- The owner can walk all 7 tools in Inspector (stdio + local HTTP) with zero
  Python knowledge, following the README checklist.
- `guard-dal.ps1`, `guard-debug-scope.ps1`, and the full default test profile
  stay green; no new guard exemptions.

## Tech Stack

- **Language:** Python ≥3.10 (assumption 1; build verifies against the
  `fastmcp` floor).
- **MCP:** `fastmcp==4.0.3`, pinned exact (their release guidance forbids `>=`).
- **HTTP client:** `httpx`, pinned exact in `requirements.lock` at build time
  (connection pooling + timeouts; stdlib `urllib` rejected — no timeout
  discipline, and the `-32001` history is what unbounded calls cost).
- **Server under diagnosis:** `FusionRpg.Server` at `http://127.0.0.1:5088`
  (locked default; launcher port-hops — the server URL is a startup argument,
  never a const).
- **Transports:** stdio (default, per-session child, matches the commit-tool
  precedent) + local-HTTP mode behind a flag (`--transport http`, port **8899**,
  localhost bind only — verified no collision in-repo; binding any non-loopback
  address is refused, see Never). No auth per the locked Auth row — revisit if
  it ever leaves localhost.
- **Toolchain precedent:** layout and `requirements.lock` mirror
  `tools/seedsmith/`; pytest run mirrors
  `$env:PYTHONPATH="tools/debug-mcp"; python -m pytest tools/debug-mcp/tests -q`.

## Commands

```powershell
# Run (stdio, default — agent sessions)
python tools/debug-mcp/server.py

# Run (local HTTP — owner dashboards / Inspector remote mode)
python tools/debug-mcp/server.py --transport http --port 8899

# Test (mirrors the seedsmith invocation shape)
$env:PYTHONPATH = "tools/debug-mcp"; python -m pytest tools/debug-mcp/tests -q

# Owner smoke via Inspector (stdio)
npx @modelcontextprotocol/inspector python tools/debug-mcp/server.py
# Caveat: Inspector CLI flags were quoted from discussion, not verified
# against upstream docs — confirm exact invocation at build time.

# Dependency install (mirrors seedsmith)
cd tools/debug-mcp
python -m pip install -r requirements.lock
```

## Project Structure

```text
tools/debug-mcp/
  server.py            → FastMCP app, transport flag, tool registration only (no logic)
  registry.py          → scope labels + route allowlist. The allowlist is
                         **generated at build** from `DebugEndpoints.cs` route
                         registrations filtered by guard-debug-scope semantics
                         (relay ⇒ injector-shaped) — a reviewable diff, not a
                         hand list. `test_registry.py` re-checks it live:
                         every tool mapped, no orphans.
  budget.py            → limit/cursor/truncate envelope shared by every list tool
  evidence.py          → PASS/FAIL rung envelope (verdict, file:line, expected, actual)
  tools/
    debug_call.py      → 1: allowlisted route invoker, scope-stamped responses
    debug_events.py    → 2: kind-filtered event tail
    debug_actor.py     → 3: instanceId XOR ptr snapshot + per-link ladder
    debug_verify.py    → 4: feature pipeline ladder, first broken link
    debug_match.py     → 5: budgeted match digest
    debug_preflight.py → 6: readiness audit (game dir, interop, env, ports,
                         DLL freshness, data dir, node_modules)
    debug_lawn_setup.py → 7: live-board setup (adapter over
                         `POST /api/debug/lawn/quick-start`; contract in
                         ideal §"Scope and honesty contract for
                         `debug_lawn_setup`")
  tests/
    test_envelope.py   → budget + evidence shape (incl. silent-truncation refusal)
    test_registry.py   → every tool maps to an endpoint/service/CLI (orphan ⇒ fail);
                         scope label present on every tool
    test_roundtrip.py  → JSON round-trip of every composed shape (the
                         AptitudeAllocation precedent: serialize-then-deserialize
                         in-test, never assume)
    test_live.py       → SIM-gated live checks (skipped without a running server;
                         never fabricate: live tests assert against real rows)
  requirements.lock    → exact pins (fastmcp==4.0.3 + httpx + pytest)
  README.md            → owner Inspector walkthrough checklist
```

## Code Style

One tool, whole discipline on display — scope label, budget params, evidence
envelope, adapter-only body:

```python
@mcp.tool(description="Tail event envelopes by kind. Budgeted; never dumps.")
def debug_events(kind: str, match_key: str | None = None,
                 limit: int = 20, cursor: str | None = None) -> dict:
    """Scope: read-only, both scopes' telemetry. Reads :5088 event query path."""
    page = store_api.events(kind=kind, match_key=match_key,
                            limit=min(limit, 100), cursor=cursor)
    return budget.envelope(
        items=page.items, scope="telemetry",
        evidence="src/FusionRpg.Server/EventQuery.cs:41",
        truncated=page.truncated, next_cursor=page.next_cursor,
    )
```

Conventions: tool names `debug_<noun>` (never verbs per endpoint — one job per
tool); descriptions one sentence answering "should the model pick this?";
`limit`/`cursor` parameter names identical on every list tool; evidence dicts
carry `file:line` + expected/actual on failures; no SQL string anywhere in the
tree (`guard-dal.ps1` covers `src/` only, so `test_registry.py` asserts the
same invariant for `tools/debug-mcp/`).

## Testing Strategy

- **Framework:** pytest under `tools/debug-mcp/tests/` (repo precedent:
  seedsmith). **No lint gate** — the repo runs none for Python, and a lint
  nobody runs is not a guardrail (cf. `tools/seedsmith/tests/` precedent);
  pytest plus the registry test plus guards are the gates.
- **Unit (no server):** envelope shape, budget caps, truncation refusal,
  registry mapping (tool→endpoint/service/CLI), scope-label presence,
  JSON round-trips of composed shapes.
- **Live (SIM server required):** one happy-path call per
  read tool against real rows; `debug_verify` against a seeded break;
  `debug_preflight` against the dev machine. Reachability-gated: no server →
  skip, never fail. Live tests assert on real
  persisted state read back through normal query paths (live-probe §3) —
  never on response bodies alone, never on fabricated rows (§4 anti-cheat).
- **Owner acceptance:** README Inspector checklist (all 7 tools invoked once
  each over stdio; `debug_verify` shown failing-then-passing on a seeded
  break). Script-assertable gates stay in pytest; Inspector is the human
  walkthrough, not the gate.
- **Guards:** `guard-dal.ps1` + `guard-debug-scope.ps1` keep passing unchanged;
  `test_registry.py` is the MCP-side mirror (orphan tool ⇒ red).

## Boundaries

- **Always:** adapter-only (call routes/services/store APIs/CLIs/files);
  scope-label every tool and response (guard-debug-scope semantics); budgets
  on every list; CRLF-immune file reads; pytest + Inspector smoke before
  handoff; explicit `paths` on commit.
- **Ask first:** any new write-capable tool; any new dependency; exposing HTTP
  mode beyond localhost; any tool the registry cannot map to an existing
  endpoint/service/CLI (likely a second implementation — refused by default).
- **Never:** SQL text; fabricated subjects/results; per-endpoint or
  per-resource CRUD tools; a second scope vocabulary; golden/population
  assertions in tools or tests (validation-ssot: readings, not constants);
  binding any non-loopback address; secrets; committing `tools/debug-mcp/__pycache__/` or local logs; deploy /
  restart / launch as tools (readiness as a tool, actuation as a script).

## Tool contracts (acceptance-level detail)

- **`debug_verify` rungs, in order:** spec → implementation → registration →
  API → service → event → worker → DB → runtime → UI. First broken link wins;
  every rung carries file:line + expected-vs-actual. A rung that cannot cite
  code is not implemented — it is omitted and the omission is stated.
- **`debug_actor` identification:** exactly one of `instanceId` / `ptr`.
  Both or neither is a typed input error, never a guess.
- **`debug_lawn_setup` contract:** preconditions (server up, game + injector
  connected) are NOT-READY errors naming the exact owner-side commands, never
  silent waits and never process starts. Refuses Explore/Travel boards by
  surfacing the endpoint's refusal, not by reinterpreting it. Returns
  `entered`/`levelType`/`targetPtr`/`plantPtr` plus scope label
  (Game-Injector-Debug orchestration with server read-back). Never spawns test
  subjects; never claims a feature proof — setup only.
- **Roster stability:** `tools/list` returns exactly these 7 tools. An 8th
  tool amends this spec first — silently extending the surface repeats the
  count creep the prior art documents. (`debug_logs` was cut at build when its
  log source failed verification; the cut is recorded, not silent.)

## Out of scope (not this build)

Guard-script edits (`guard-debug-scope` extension ships separately under its
own program); per-endpoint tools; deploy/restart/launch tools; TypeScript or
remote variants; auth; dashboards (HTTP mode serves Inspector, not a UI).

## ActorHub gate

Not applicable by consumption: `debug_actor` reads Hub snapshots, it neither
produces nor folds actor combat/derived/AppliedCombat numbers. No private
fold exists anywhere in this tree; if a future tool needs one, this spec
must be amended first (never silently).

## Tunables

None. Pagination defaults (20/100) and the 7-tool ceiling are structural
constants with stated reasons (prior art in the ideal doc), kept as `const`
with the reason attached per the magic-numbers standard — never in
`data/tuning/`. Server URL, ports, and contract versions are read from the
running system, not configured here.

## Success Criteria

- [ ] `tools/list` shows exactly 7 tools; each description disambiguates its
  one job in one sentence.
- [ ] `debug_preflight` reports every check as PASS/FAIL with evidence and the
  fix command; a missing game dir names the env var, not a traceback.
- [ ] Budget tests fail on any silent truncation; round-trip tests cover every
  composed shape.
- [ ] Registry test maps all 7 tools to existing endpoints/services/CLIs.
- [ ] `debug_lawn_setup` against a live lab board returns a living-zombie
  `targetPtr`; against a stopped server / disconnected injector it returns
  NOT-READY naming the fix command (no hangs: bounded by the endpoint's own
  timeout).
- [ ] Live suite green against a SIM server; skipped-clean without one.
- [ ] Owner completes the README Inspector checklist.
- [ ] Guards + default test profile green, zero new exemptions.

## Open Questions

Resolved since drafting: HTTP port locked to 8899 (no in-repo collision);
allowlist generated at build (reviewable diff) and re-checked live by the
registry test. No open questions blocking build.
