#!/usr/bin/env python3
"""MCP server: repo-git — validated commits only (no push).

Tools:
  commit(message, paths?, all?, amend?)
  validate_message(message)

Run: python scripts/commit-tool/mcp_server.py

The server is a long-lived stdio child, so a plain module-level import would pin the
tool code to whatever was on disk at session start: editing scripts/commit-tool/ would
silently have no effect until the IDE was restarted, and the stale build was the proven
cause of `commit` answering `-32001` while `validate_message` stayed fast (2026-09-12).
`_reload_tool_modules` re-imports the tool's own modules before every call and on the
mtime of this file, so a saved edit takes effect on the next tool call.
"""
from __future__ import annotations

import importlib
import json
import sys
from pathlib import Path
from typing import Any

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

import clean_commit  # noqa: E402
import validate  # noqa: E402

# Modules that make up the tool's own code; reloading these is what un-stales a session.
_TOOL_MODULES = ("validate", "clean_commit")


def _reload_tool_modules() -> None:
    """Re-import the tool's own modules so an on-disk edit applies to this call.

    Called at the start of every tool invocation. importlib.reload always re-executes the
    source (these modules are small, so this is a few ms), which removes the "restart the
    IDE after editing the commit tool" footgun that produced a false -32001 hang.
    Ordering matters: validate is reloaded before clean_commit so clean_commit's own
    `from validate import ...` picks up the fresh module rather than the stale one.
    """
    for name in _TOOL_MODULES:
        module = sys.modules.get(name)
        if module is None:
            continue
        try:
            importlib.reload(module)
        except Exception as exc:  # noqa: BLE001 — a broken edit must not kill the server
            print(f"repo-git: reload of {name} failed: {exc}", file=sys.stderr)


def _tool_result(payload: dict[str, Any]) -> str:
    return json.dumps(payload, ensure_ascii=False, indent=2)


def main() -> None:
    try:
        from mcp.server.fastmcp import FastMCP
    except ImportError as exc:
        print(
            "mcp package missing; run: pip install -r scripts/commit-tool/requirements.txt",
            file=sys.stderr,
        )
        raise SystemExit(2) from exc

    mcp = FastMCP("repo-git")

    @mcp.tool(name="validate_message")
    def validate_message_tool(message: str) -> str:
        """Dry-run commit message policy (trailers, watermarks). Does not write git."""
        _reload_tool_modules()
        policy = validate.load_policy(validate.DEFAULT_POLICY)
        errors = validate.validate_message(message or "", policy)
        if errors:
            return _tool_result({"ok": False, "errors": errors})
        return _tool_result({"ok": True, "errors": []})

    @mcp.tool(name="commit")
    def commit_tool(
        message: str,
        paths: list[str] | None = None,
        all: bool = False,
        amend: bool = False,
        worktree: str | None = None,
    ) -> str:
        """Create a git commit with allowlisted author after policy validation.

        Push is not available — owner pushes manually. Only call when the user asked to commit.

        `worktree` targets a linked worktree (e.g. `.kilo/worktrees/<id>`, absolute or
        relative to the main checkout) so the commit lands on that worktree's branch.
        Omit it to commit the main checkout.
        """
        _reload_tool_modules()
        if not (message or "").strip():
            return _tool_result({"ok": False, "errors": ["message is required"]})
        result = clean_commit.perform_commit(
            message,
            paths=list(paths) if paths else None,
            all_tracked=bool(all),
            amend=bool(amend),
            allow_empty=False,
            worktree=worktree,
        )
        if not result.ok:
            return _tool_result({"ok": False, "errors": result.errors})
        return _tool_result(
            {
                "ok": True,
                "hash": result.hash,
                "subject": result.subject,
                "author": result.author,
            }
        )

    mcp.run()


if __name__ == "__main__":
    main()
