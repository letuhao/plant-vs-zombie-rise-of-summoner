#!/usr/bin/env python3
"""Cursor beforeShellExecution gate: block raw `git commit`.

Only allow commits via:
  python scripts/commit-tool/clean_commit.py ...

Reads Cursor hook JSON on stdin; writes permission JSON on stdout.
Always emits JSON (fail-closed safe).
"""
from __future__ import annotations

import json
import re
import sys

CLEAN = re.compile(
    r"(?:python|python3|py)(?:\s+\-3)?\s+[^\n]*commit-tool[/\\]clean_commit\.py\b",
    re.IGNORECASE,
)
# Subcommand `commit` only — do not match paths like `.githooks/commit-msg`.
RAW_COMMIT = re.compile(
    r"(?:^|[;&|]|&&|\|\|)\s*\bgit(?:\.exe)?\b"
    r"(?:\s+(?:-c\s+\S+|-[^\s]+))*"
    r"\s+commit\b",
    re.IGNORECASE | re.MULTILINE,
)


def decide(cmd: str) -> dict:
    if CLEAN.search(cmd or ""):
        return {"permission": "allow"}
    if RAW_COMMIT.search(cmd or ""):
        return {
            "permission": "deny",
            "user_message": (
                'Raw git commit blocked. Use: python scripts/commit-tool/clean_commit.py -m "..."'
            ),
            "agent_message": (
                "Do not run `git commit` directly. Use "
                '`python scripts/commit-tool/clean_commit.py -m "subject"` '
                "(optional paths after --). It forces the allowlisted author and "
                "rejects Co-authored-by / vendor watermark trailers."
            ),
        }
    return {"permission": "allow"}


def main() -> int:
    try:
        data = json.load(sys.stdin)
        cmd = data.get("command") or ""
        result = decide(cmd)
    except Exception as exc:  # noqa: BLE001
        result = {
            "permission": "ask",
            "agent_message": f"commit gate error: {exc}",
        }
    sys.stdout.write(json.dumps(result))
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
