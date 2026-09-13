# Debug MCP — the ideal

**Status:** idea phase, 2026-09-13 (enriched: unified ship list). Not a spec. No build authorized.

## Which loop this extends

None — and that is stated, not missing. This is developer capability, not a
player feature: it serves every loop equally (lawn proofs, expedition
goldens, world turns, delves) and invents no player-facing pitch. The game
remains an RPG plus empire building as Crazy Dave (spine: level/power,
summon/fusion, items; places: lawn first-core, idle expeditions, world/empire
turns, delves), and gameless-first is unaffected: diagnostics attach to the
server and web with Fusion closed; injector telemetry is enrichment, never a
gate. No change to `the-loops.md` is proposed or needed.

## What this is

A small Python MCP server (`tools/debug-mcp/`, 7 tools, stdio) that lets an
agent inspect the **real running system** — server state, persisted rows,
event envelopes, engine telemetry — and prove a feature end to end with cited
evidence. It implements no domain logic: every tool is a thin adapter over a
route, service, CLI, or file the repo already owns. The problem it solves is
measured, not hypothetical: on 2026-09-13 a live probe bound a fabricated
loadout straight into the injector and read it back from the same injector's
telemetry — `ok:true`-shaped evidence for a broken feature — because the agent
had diagnosis tools but no unified, scope-honest verification path.

## What already exists

### Built (works end to end today)

- **Server debug surface: 82 routes**, counted from
  `src/FusionRpg.Server/DebugEndpoints.cs` (`MapPost`/`MapGet`), always on as
  `/api/debug/*` (`docs/architecture/software-architecture.md:146`). Proven by
  daily use in live probes and `smoke-*.ps1` scripts.
- **Injector debug vocabulary: 163 `debug.*` commands** in
  `src/FusionRpg.Injector/CheatCommandRunner.cs` (counted), relayed by the
  `MapPost(g, path, "debug.xyz")` wrappers — e.g. `debug.board-stats`,
  `debug.spawn-plant`, `debug.shield.grant`, `debug.effect.enqueue-delta`.
- **Scope classifier**: `scripts/guard-debug-scope.ps1` (worktree branch, 255
  lines) computes Game-Injector-Debug vs RPG-Server-Debug per route from handler
  bodies (any injector relay ⇒ injector-shaped; persisted/domain write with no
  relay ⇒ server-shaped). Rule text at its lines 6–14.
- **Probe runners**: `tools/ProveLiveProbe/` (worktree), `tools/ProveAptitude/`,
  `scripts/prove-vfx.ps1`, `scripts/smoke-effect-scoped-atk.ps1` — the
  adapter surface already exists as CLIs.
- **SIM/test doors**: `/api/sim/*` + `/api/test/*` only under `FUSIONRPG_SIM=1`
  (`software-architecture.md:146`); E2E `RpgApiFactory` proves reset/seed flows
  work (`/api/test/reset`, world create, content hash).
- **Store read paths**: `RpgStore*` partials in `FusionRpg.Data` own every table
  (`docs/architecture/data-architecture.md` §2 inventory; §6: all SQL lives in
  `FusionRpg.Data`, enforced by `guard-dal.ps1` with an empty allowlist).
- **Protocol docs**: `docs/protocol/rest.md`, `events.md`, `signalr.md`
  (indexed at `software-architecture.md:174`) — the route/event contracts tools
  would adapt, not duplicate.
- **MCP precedent**: `scripts/commit-tool/mcp_server.py` is a working per-session
  stdio MCP server (`validate_message`/`commit`/`merge`), proven from this very
  harness. No new transport to invent.

### Wiring gap (inert line, not a wall)

- **Scope banners not yet in `DebugEndpoints.cs`.** The guard's banner-agreement
  check is deliberately a no-op until `// Game Injector Debug` /
  `// RPG Server Debug` comments land above each route
  (`guard-debug-scope.ps1:18-22`, worktree). The classification machinery is
  built; the one-line-per-route annotations are the missing wire — and they are
  exactly the machine-readable registry a Debug MCP needs to derive scope
  labels and its route allowlist from, instead of maintaining a second list.

### Real gaps (nothing exists anywhere)

- **No debug MCP server.** Only the commit MCP exists.
- **No unified verify ladder.** `verify_pipeline(feature, subject)` — spec →
  impl → registration → API → service → event → worker → DB → runtime → UI,
  first broken link with evidence — has no implementation.
- **No response-budget discipline on debug reads.** Board dumps, event tails,
  and log reads assume a consumer that can skim; an agent pays per byte with
  no cursor, cap, or truncation signal.
- **No queryable scope registry.** The guard prints pass/fail; nothing answers
  "which scope is route X?" at runtime.

## Prior art (numbers, with sources)

- **Tool count degrades selection past ~40–50 tools**; Cursor warns past 40.
  Below the range agents weigh candidates; above it similar names blur and
  accuracy drops with nothing else changed
  (Merlonix 2026-07-08; Speakeasy "Cursor: more than 40 tools may degrade").
- **A standard trio (Playwright + GitHub + IDE) burns >20% of context before
  work starts** (EclipseSource 2026-01-22); agents must re-read every tool
  definition at every turn, and tool schemas — not names — are the bulk.
- **60–80 tools tolerable on Claude; past ~100 split or dynamically load**
  (modelcontextprotocol discussion #2036, multi-operator reports). Mitigations
  that work: per-domain servers of 20–40, concise descriptions, `listChanged`
  dynamic loading, namespaced names.
- **Budget every list response** (Runpod 2026-09-02, from a real 15×-context
  outage): default limit 20, hard cap 100, opaque cursor, explicit `truncated`
  flag (silent truncation is worse than an error), URIs-not-payloads for large
  results, and log per-tool response sizes to find the session killer.
  Descriptions answer one question: "should the model pick this tool?"
- **FastMCP 4.0.3 verified current** (GA 2026-08-31, ~27.5k stars, sessionless
  `2026-07-28` era; pin exact, never `>=`). Disambiguation: the standalone
  `fastmcp` package, not `mcp.server.fastmcp` (what the commit tool uses).
- **Naive mapping count, measured**: 82 server routes + 163 injector commands =
  245 candidate "tools" — five times the degradation threshold. This is the
  quantitative case for unification: the ship list below is 6.

## The shape — the unified ship list (7 tools, roles distinct)

A tool survives **only if it does something a generic route-caller can't**:
compose multiple sources into one budgeted response, compute a verdict, or
enforce a budget the raw route lacks. Everything that is a thin alias for one
GET dies — it rides `debug_call` plus the protocol docs instead. That single
rule cut the list from 10 to 6; readiness-vs-liveness earned the 7th, and the
highest-frequency multi-step setup earned the 8th — until `debug_logs` failed
verification (below) and the roster settled at 7.

Design rules (load-bearing, restated inline because downstream sessions read
this doc, not its links):

- **One tool per job, never per endpoint.** A second tool with the same or
  near-same role is refused at spec review — near-duplicates are the exact
  failure the count research predicts (`updateOrderStatus` vs
  `updateOrderShipment` confusion).
- **Every response carries its debug scope** (Game-Injector-Debug or
  RPG-Server-Debug, derived from the guard's classification, never hand-
  labeled): injector scope proves only that the engine reflects state; server
  scope proves domain logic and persistence for a real record. A response body
  alone is never proof — verification means persistence plus read-back through
  the normal path.
- **No SQL anywhere.** Tools call routes, services, store APIs, CLIs, files.
  `guard-dal.ps1` keeps passing unchanged.
- **No PvZ representation claims.** Tools observe engine telemetry; an RPG
  concept never needs a home in a PvZ channel (RPG-layer rule). RPG mechanics
  resolve in our own stack; diagnostics read that stack.
- **Every list response paginates** (default 20, cap 100, cursor, truncated
  flag); large payloads return fetchable references.
- **Write-capable tools are flagged and few** (blast radius + mis-selection
  risk); each routes through the real endpoint/service — trigger real,
  never fabricate.

| # | Tool | Role (one sentence) | Why it survives (not via `debug_call`) |
|---|---|---|---|
| 1 | `debug_call` | Invoke one allowlisted debug/sim/test route (method, route, body); response stamped with scope label | The universal adapter — this tool IS the unification; every plain route read rides it |
| 2 | `debug_events` | Kind-filtered event-envelope tail (limit default 20, cap 100, cursor, truncated flag) | Budget logic the raw event routes lack; unbounded tails are the documented session-killer |
| 3 | `debug_actor` | One actor snapshot by `instanceId` XOR `ptr`: DB record, runtime binding, Hub snapshot, recent events, per-link pipeline verdicts | Multi-source composition no single route returns |
| 4 | `debug_verify` | Feature pipeline ladder to the first broken link, evidence per rung (file:line, ids, expected-vs-actual) | Verdict logic, not a read — the anti-fabrication tool |
| 5 | `debug_match` | Budgeted match digest by `matchKey`: phase, snapshot hash, paginated entities, decisions | Raw snapshots are unbounded dumps; digest + entity cap is the budgeted form |
| 6 | ~~`debug_logs` — CUT at build~~ | No stable source exists: `dist/FusionRpg.Server/server-start.log` is a one-off deploy redirect (it currently holds a failed-start trace), and server stdout lives in the owner's terminal. Diagnostic telemetry already flows as `debug.*` event envelopes, owned by row 2. A file-tail tool here would read whatever file happens to exist — fabrication-adjacent. Cut with evidence during T4; re-add only if a stable log path ships. |
| 7 | `debug_preflight` | Setup/readiness audit before any deploy or live probe: game dir + interop refs, env vars, ports, DLL freshness vs sources, data dir, node_modules — each check PASS/FAIL with evidence and the fix command | Readiness (can we deploy?) vs `debug_status`-style liveness (is it up?) — the SRE distinction; mechanizes what agents today re-derive by hand from `deploy-play.ps1` failures |
| 8 | `debug_lawn_setup` | One-call live-board setup: enter level, lab-overlay scenario, wave-freeze, snapshot poll → `entered`/`levelType`/`targetPtr`/`plantPtr` (adapter over `POST /api/debug/lawn/quick-start`, `DebugEndpoints.cs:173`) | Highest-frequency multi-step setup (every live prove needs it); unifies the skill doc, `Ensure-LiveLabBoard`, `ensure_lab_board()`, and hand-rolled curl into one scope-labeled call |

Folded, not shipped (each reachable today without a new tool):

| Cut | Rides on |
|---|---|
| `debug_status` (health/versions) | `debug_call(/health)` + protocol docs |
| `debug_catalog` (vocabularies/versions) | `debug_call(/api/catalogs/*)` |
| `debug_content` (corpus contract checks) | Guard/audit scripts via shell — CI's job, agents already have bash |
| `debug_sim` (resets/seeds) | SIM routes via shell curl (`protocol/rest.md`); a wrapper adds no validation the routes lack |

Still refused outright: per-endpoint tools, per-resource CRUD, `database.query(sql)`
(DAL-guard violation), a second scope vocabulary, golden assertions inside tools
(validation-ssot applies to tool outputs too), TypeScript/remote variants.

### Scope and honesty contract for `debug_lawn_setup` (read before speccing)

Verified against code (`DebugEndpoints.cs:173-289`, skill
`live-lawn-quick-start`): the endpoint enters the level via injector relay,
refuses Explore/Travel levelTypes (`:243-244`), freezes waves (`:246`), runs
the lab-overlay scenario steps, and polls a board snapshot for the first
living zombie/plant ptrs. No `clear-plants`/`clear-zombies` inside — a "clear
board" is wave-freeze on a fresh level, not a wipe. No pick-up-menu command
exists anywhere (`CheatCommandRunner.cs`, `DebugEndpoints.cs` both searched):
menu states are handled inside the enter flow (skill triage), not by a
dedicated step — the tool must report NOT-READY with the manual step if a
menu ever blocks, never invent a dismiss.

Scope label is Game-Injector-Debug-shaped (any relay ⇒ injector-shaped, guard
rule) with server-side read-back — legitimate orchestration (entering a level
requires telling the injector), and live-probe §2's "skip tedium" allowance
covers setup. But setup is not evidence: the tool proves a usable board, never
a feature. It must never spawn the subjects under test — actors under test
come from the live board or real flows, or the next `debug_verify` runs on
fabricated ground (the 2026-09-13 incident class). Preconditions (server up,
game + injector connected) are NOT-READY errors naming the exact owner-side
commands (`Start-Process dist\FusionRpg.Server\…`, `deploy-play.ps1 -NoServer`,
launch game) — the tool never starts processes itself (process-lifetime +
destructiveness policy, §4.5).

### Deploy / restart / launch are NOT tools (deliberate)

Candidates like `deploy`, `restart_server`, `launch_game` were considered and
refused — not for count reasons but because the repo has measured why each
fails as an agent-invoked operation:

- **Process lifetime.** A server started from an agent tool call dies when that
  call's process tree is cleaned up, looking exactly like a mid-run crash
  (AGENTS.md live-deploy gotcha). A `deploy` tool would manufacture the outage
  it claims to resolve; server restarts stay owner-terminal operations.
- **Timeouts.** Builds + publish + game launch run multi-minute. The commit
  tool's `-32001` history (`agent-git.md`) is what unbounded agent-invoked
  operations cost: hangs mistaken for failures, retried into duplication.
- **Destructiveness policy.** Killing listeners on `:5088`, overwriting DLLs a
  running game holds locked (the freshness guard exists because this bit us),
  wiping data dirs — all in the ask-first class (destructive/irreversible),
  never behind an agent's single tool call.
- **The ps1 scripts stay scripts.** `deploy-play.ps1`'s roughness ("not very
  well") is a script-cleanup task, not a reason to re-platform it into MCP.
  `debug_preflight` (row 7) is the correct split: the MCP answers "is this
  machine ready, and if not, which exact command fixes it"; the owner (or an
  explicitly-authorized session) runs the deploy. Readiness as a tool,
  actuation as a script — never both in one surface.

## Tunables

None. A debug MCP introduces no balance numbers. Pagination defaults (20/100)
and the 7-tool ceiling are structural constants with stated reasons (prior
art above), not tunable policy — per the magic-numbers standard they stay
`const` with the reason attached, never in `data/tuning/`.

## What this deliberately does not decide

- stdio-per-session vs a local long-lived HTTP server for game-attached use
  (stdio fits agents; HTTP fits dashboards).
- Whether `debug_call`'s allowlist is generated from `DebugEndpoints.cs` at
  spec time or checked live by the registry guard (both satisfy adapter-only;
  generation is cheaper per endpoint, guard is harder to drift).
- `fastmcp` minor-pin bump policy.
- AuthN (none: localhost, owner machine — revisit if it ever leaves).

## Open questions (owner decisions only)

1. Approve the 7-tool roster and the generic-`debug_call` trade (schema
   validation per endpoint vs count stability)? Recommendation: approve as
   listed; per-endpoint tools stay refused.
2. stdio only, or also local HTTP for dashboard use?
3. SIM-gated ops in the same server (flagged) or a separate surface?

## Gate gaps (stated, not hidden)

- Read in-session: `the-game.md`, `the-loops.md`, CLAUDE.md RPG-layer rule
  (full §), `validation-ssot.md` (full), `live-probe-standard.md` (§§1–4 head),
  `data-architecture.md` (full), `decisions.md` + `software-architecture.md`
  (full). Not read: `live-probe-standard.md` remainder (§4 tail–end),
  `protocol/rest.md` contents (only indexed), subsystem rows beyond those above.
- Counts (82 routes, 163 commands) measured on this checkout, not quoted.
- Web sources dated 2026-01 to 2026-09; Inspector CLI flags from prior
  discussion still unverified against upstream docs.

## Handoff

Path written: `docs/architecture/debug-mcp-ideal.md`. Next step is `/spec`
(capability map + module specs) once the three open questions are answered.
No specs, plans, or code were written; the ideal doc is where this phase stops.
