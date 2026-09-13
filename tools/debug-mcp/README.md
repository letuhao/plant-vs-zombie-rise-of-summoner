# Debug MCP — owner walkthrough

Thin adapter MCP over the repo's real debug surface (spec:
`docs/architecture/debug-mcp/spec-debug-mcp.md`). Nine tools, stdio default,
local HTTP behind a flag. No domain logic lives here.

## Install

```powershell
cd tools/debug-mcp
python -m pip install -r requirements.lock
```

## Run (stdio — agent sessions)

```powershell
python tools/debug-mcp/server.py
```

## Run (local HTTP — dashboards, Inspector remote)

```powershell
python tools/debug-mcp/server.py --transport http --port 8899
```

Localhost bind only; any other `--host` is refused before serving.

## Inspector walkthrough (do all 9, in order)

```powershell
npx @modelcontextprotocol/inspector python tools/debug-mcp/server.py
```

1. `debug_call` — `{"method":"GET","route":"/effects/contract"}` → 200 plus
   scope label. Then `{"method":"GET","route":"/api/nope"}` → refused.
2. `debug_events` — `{"kind":"board.start","limit":5}` → budgeted envelope
   (`truncated`, `next_cursor`, never a silent dump).
3. `debug_match` — `{}` on an idle server → explicit not-live shape.
4. `debug_actor` — `{"instanceId":"00000000-0000-0000-0000-000000000000"}`
   → typed miss with evidence rung (fabricated id fails at the subject rung).
5. `debug_verify` — `{"feature":"actor","subject":"<same bogus id>"}` →
   `ok:false`, `broken_rung:"subject"`.
6. `debug_preflight` — `{}` → deploy checks plus `live`: server reachability,
   game process fact, injector heartbeat, and a fresh board snapshot. Treat
   `live.ready:true` as the only green light for a lawn probe. A connected
   injector with `board.state:"idle"` is deliberately **not** ready.
7. `debug_lawn_setup` — `{}` **only with a live game you own the board of**:
   enters level 1, freezes waves, runs lab-overlay, returns ptrs. It touches
   the live lawn — never run it against someone else's prove session.
8. `debug_ui_nav` — `{"action":"back-to-menu"}` then `{"action":"enter-main-menu"}`
   to leave a stuck/defeated board and reach the real main menu (one call
   alone can land on the previous menu layer instead — this game's menu stack
   is not flat). Requires `/ui-nav` to exist in the checkout this server runs
   against (`src/FusionRpg.Server/DebugEndpoints.cs`) — added 2026-09-14,
   check it landed before relying on this tool.
9. `debug_restart_game` — **DISRUPTIVE**: `{}` closes and relaunches the game
   process, polling for a fresh injector connection. Ends whatever the
   operator was looking at; prefer it over chasing `debug_ui_nav` in place
   when a provably clean board matters more than speed. Adapter over
   `scripts/restart-game.ps1` — refuses cleanly if that script is missing
   from the checkout this server runs against.

## Scope labels (every response carries one)

- `game-injector-debug` — engine state only; proves Unity reflects it, nothing
  about server correctness.
- `rpg-server-debug` — real domain/persistence path for a real record.
- `local-machine` — filesystem/process preflight facts. `debug_preflight.live`
  also carries server and injector scopes because it reads existing routes and
  requests one bounded snapshot; it never launches, deploys, or restarts.
- `null` — unclassifiable static read (served, flagged, never trusted blindly).

## Troubleshooting

| Symptom | Fix |
|---|---|
| Tool returns NOT-READY naming a start command | Run the named command from your own terminal (agent-spawned servers die with the tool call) |
| `debug_lawn_setup` 409 injector-not-connected | Launch the game with the injector loaded, then retry |
| `debug_preflight.live.ready` is false but injector is connected | Follow its `fix`: wait for loading, or open a lawn/use `debug_lawn_setup` only on a board you own |
| Live tests skip | Start the SIM server first; unit tests never need it |
| `debug_call`/`debug_ui_nav` says "route not in debug allowlist" for a route you know exists | The allowlist is generated from `DebugEndpoints.cs` in whatever checkout this server's cwd resolves to — a route added in a worktree is invisible here until it's merged/present in this checkout. Confirmed real 2026-09-14: `/ui-nav`/`/game-state` existed only in a worktree branch while this server ran against main. Check `git branch --show-current` in the server's own working directory. |
| A route you just added to `DebugEndpoints.cs` still isn't found | The allowlist cache is keyed by every `*Endpoints.cs` file's mtime and reloads automatically on the next call within the same checkout — no server restart needed for that case (fixed 2026-09-14; previously required a restart). |
