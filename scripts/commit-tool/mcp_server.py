#!/usr/bin/env python3
"""MCP server: repo-git — validated commits only (no push).

Tools:
  commit(message, paths?, all?, amend?)
  validate_message(message)

Run: python scripts/commit-tool/mcp_server.py
"""
from __future__ import annotations

import json
import sys
from pathlib import Path
from typing import Any

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from clean_commit import perform_commit  # noqa: E402
from validate import DEFAULT_POLICY, load_policy, validate_message  # noqa: E402


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
        policy = load_policy(DEFAULT_POLICY)
        errors = validate_message(message or "", policy)
        if errors:
            return _tool_result({"ok": False, "errors": errors})
        return _tool_result({"ok": True, "errors": []})

    @mcp.tool(name="commit")
    def commit_tool(
        message: str,
        paths: list[str] | None = None,
        all: bool = False,
        amend: bool = False,
    ) -> str:
        """Create a git commit with allowlisted author after policy validation.

        Push is not available — owner pushes manually. Only call when the user asked to commit.
        """
        if not (message or "").strip():
            return _tool_result({"ok": False, "errors": ["message is required"]})
        result = perform_commit(
            message,
            paths=list(paths) if paths else None,
            all_tracked=bool(all),
            amend=bool(amend),
            allow_empty=False,
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
