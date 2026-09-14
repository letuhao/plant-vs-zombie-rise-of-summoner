#!/usr/bin/env python3
"""Cursor beforeShellExecution gate — delegates to block_git_write."""
from __future__ import annotations

import json
import sys
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parents[1]
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from block_git_write import cursor_decision, extract_command  # noqa: E402


def main() -> int:
    try:
        data = json.load(sys.stdin)
        cmd = extract_command(data) if isinstance(data, dict) else ""
        result = cursor_decision(cmd)
    except Exception as exc:  # noqa: BLE001
        result = {"permission": "ask", "agent_message": f"commit gate error: {exc}"}
    sys.stdout.write(json.dumps(result))
    sys.stdout.write("\n")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
