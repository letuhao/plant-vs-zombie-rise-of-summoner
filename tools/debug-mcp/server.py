"""Debug MCP server: registration only, no logic (spec Project Structure).

Transports: stdio default (per-session child, commit-tool precedent);
local-HTTP mode behind --transport http (port 8899, localhost bind only —
non-loopback is refused: Auth=None row, localhost only).
"""
import argparse
import sys
from typing import Any, Optional

from fastmcp import FastMCP

from tools.debug_call import call as debug_call_impl
from tools.debug_events import tail as debug_events_impl
from tools.debug_match import digest as debug_match_impl
from tools.debug_actor import snapshot as debug_actor_impl
from tools.debug_verify import verify as debug_verify_impl
from tools.debug_preflight import audit as debug_preflight_impl
from tools.debug_lawn_setup import setup as debug_lawn_setup_impl

mcp = FastMCP("debug-mcp")

_LOOPBACK = ("127.0.0.1", "localhost", "::1")
_DEFAULT_PORT = 8899


@mcp.tool(description="Invoke one allowlisted debug/sim/test route and return its body scope-stamped.")
def debug_call(method: str, route: str, body: Optional[dict[str, Any]] = None) -> dict:
    """Scope: stamped per route from guard-debug-scope derivation."""
    return debug_call_impl(method, route, body)


@mcp.tool(description="Tail event envelopes by kind, budgeted with cursor paging.")
def debug_events(kind: Optional[str] = None, match_key: Optional[str] = None, limit: int = 20,
                 cursor: Optional[str] = None) -> dict:
    """Scope: rpg-server-debug (server event query path)."""
    return debug_events_impl(kind, match_key, limit, cursor)


@mcp.tool(description="Digest the live match snapshot: phase, hash, capped entity digests.")
def debug_match(match_key: Optional[str] = None, entity_limit: int = 20,
                cursor: Optional[str] = None) -> dict:
    """Scope: stamped from the snapshot route derivation."""
    return debug_match_impl(match_key, entity_limit, cursor)


@mcp.tool(description="Snapshot one actor by instanceId XOR ptr: record, Hub audit, events, verdicts.")
def debug_actor(instance_id: Optional[str] = None, ptr: Optional[str] = None) -> dict:
    """Scope: stamped from the Hub-audit route derivation. Read-only Hub consume."""
    return debug_actor_impl(instance_id, ptr)


@mcp.tool(description="Run one feature pipeline ladder to the first broken link, with evidence.")
def debug_verify(feature: str, subject: str) -> dict:
    """Closed feature set (actor, summon). Fabricated subjects fail at the subject rung."""
    return debug_verify_impl(feature, subject)


@mcp.tool(description="Audit setup readiness: game dir, interop, ports, DLLs, data, FE, deps.")
def debug_preflight() -> dict:
    """Scope: local-machine. Read-only; every check carries evidence + fix."""
    return debug_preflight_impl()


@mcp.tool(description="Set up a live lab board: enter level, freeze waves, poll target ptrs.")
def debug_lawn_setup(scenario: str = "lab-overlay", level: int = 1,
                     timeout: int = 60) -> dict:
    """Scope: game-injector-debug orchestration. Setup only, never a proof."""
    return debug_lawn_setup_impl(scenario, level, timeout)


def resolve_transport(argv=None):
    """Parse transport flags. Non-loopback bind is refused (never served)."""
    parser = argparse.ArgumentParser(prog="debug-mcp")
    parser.add_argument("--transport", choices=("stdio", "http"), default="stdio")
    parser.add_argument("--host", default="127.0.0.1")
    parser.add_argument("--port", type=int, default=_DEFAULT_PORT)
    args = parser.parse_args(argv)
    if args.host not in _LOOPBACK:
        raise ValueError(f"refusing non-loopback bind: {args.host} (localhost only)")
    return {"transport": args.transport, "host": args.host, "port": args.port}


def main(argv=None):
    cfg = resolve_transport(argv)
    if cfg["transport"] == "http":
        mcp.run(transport="http", host=cfg["host"], port=cfg["port"])
    else:
        mcp.run()


if __name__ == "__main__":
    main(sys.argv[1:])

