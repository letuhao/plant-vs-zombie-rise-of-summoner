#!/usr/bin/env python3
"""Deny agent shell git/gh write commands. Shared by Cursor, Claude, and CLI checks.

Exit codes (CLI / Claude PreToolUse):
  0 allow
  2 deny (Claude convention: exit 2 blocks + stderr to model)

Cursor beforeShellExecution: prints permission JSON on stdout.
"""
from __future__ import annotations

import argparse
import json
import re
import sys

GIT_WRITE_SUBCMDS = (
    "commit",
    "push",
    "merge",
    "rebase",
    "cherry-pick",
    "am",
    "revert",
    "tag",
    "update-ref",
    "filter-branch",
    "filter-repo",
)

DENY_REASON = (
    "Blocked git/gh write from agent shell. "
    "Commit only via MCP tool repo-git.commit. "
    "Push is owner-only (no agent push)."
)

# Subshell / substitution that might hide git
SUBSHELL = re.compile(r"(?:\$\(|`)", re.IGNORECASE)
GIT_BIN = re.compile(r"(?:^|[\s;|&])(?:git(?:\.exe)?)\b", re.IGNORECASE)
GH_BIN = re.compile(r"(?:^|[\s;|&])(?:gh(?:\.exe)?)\b", re.IGNORECASE)
CLEAN_COMMIT = re.compile(
    r"commit-tool[/\\]clean_commit\.py\b",
    re.IGNORECASE,
)
HOOKS_OVERRIDE = re.compile(
    r"(?:-c\s+core\.hooksPath=|/c\s+core\.hooksPath=|core\.hooksPath\s*=)",
    re.IGNORECASE,
)
NO_VERIFY = re.compile(r"(?:--no-verify|\s-n)(?:\s|$)", re.IGNORECASE)

WRITE_SUB = re.compile(
    r"\b(?:" + "|".join(re.escape(s) for s in GIT_WRITE_SUBCMDS) + r")\b",
    re.IGNORECASE,
)

GH_WRITE = re.compile(
    r"\bgh(?:\.exe)?\b(?:\s+\S+)*\s+(?:pr\s+create|release\s+create|repo\s+sync)\b",
    re.IGNORECASE,
)


def split_segments(command: str) -> list[str]:
    """Split on shell separators; keep simple (good enough for agent cmds)."""
    parts = re.split(r"&&|\|\||;|\|", command or "")
    return [p.strip() for p in parts if p and p.strip()]


def segment_is_git_write(seg: str) -> bool:
    if not GIT_BIN.search(" " + seg):
        return False
    # Avoid false positive on paths like .githooks/commit-msg without git binary
    # Require git binary then a write subcommand as a token
    if not WRITE_SUB.search(seg):
        return False
    # `git check-attr ... commit-msg` has "commit" inside a path — require subcommand position
    # Heuristic: after optional -c/-config flags, first non-flag token is subcommand
    tokens = seg.split()
    # find 'git' token
    idx = None
    for i, t in enumerate(tokens):
        if re.fullmatch(r"git(?:\.exe)?", t, re.IGNORECASE):
            idx = i
            break
    if idx is None:
        return False
    i = idx + 1
    while i < len(tokens):
        t = tokens[i]
        if t == "-c" and i + 1 < len(tokens):
            i += 2
            continue
        if t.startswith("-"):
            i += 1
            continue
        return t.lower() in GIT_WRITE_SUBCMDS
    return False


def deny_reason_for(command: str) -> str | None:
    cmd = command or ""
    if CLEAN_COMMIT.search(cmd):
        return DENY_REASON + " (clean_commit.py is owner/debug only; use MCP repo-git.commit)"

    if GH_WRITE.search(cmd):
        return DENY_REASON

    if HOOKS_OVERRIDE.search(cmd) and GIT_BIN.search(cmd):
        return DENY_REASON + " (core.hooksPath override blocked)"

    # Subshell containing git write
    if SUBSHELL.search(cmd) and GIT_BIN.search(cmd) and WRITE_SUB.search(cmd):
        # Conservative: if subshell present with git+write tokens, deny
        if segment_is_git_write(cmd) or any(segment_is_git_write(s) for s in split_segments(cmd)):
            return DENY_REASON

    for seg in split_segments(cmd):
        if segment_is_git_write(seg):
            if NO_VERIFY.search(seg):
                return DENY_REASON + " (--no-verify blocked)"
            return DENY_REASON
        if GH_WRITE.search(seg):
            return DENY_REASON

    return None


def extract_command(payload: dict) -> str:
    """Cursor uses `command`; Claude PreToolUse uses tool_input.command."""
    if not isinstance(payload, dict):
        return ""
    if payload.get("command"):
        return str(payload["command"])
    tool_input = payload.get("tool_input") or {}
    if isinstance(tool_input, dict) and tool_input.get("command"):
        return str(tool_input["command"])
    # Kilo / others
    args = payload.get("args") or {}
    if isinstance(args, dict) and args.get("command"):
        return str(args["command"])
    return ""


def cursor_decision(command: str) -> dict:
    reason = deny_reason_for(command)
    if reason:
        return {
            "permission": "deny",
            "user_message": reason,
            "agent_message": reason,
        }
    return {"permission": "allow"}


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Block agent git/gh write commands")
    parser.add_argument("--command", "-c", help="Command string to evaluate")
    parser.add_argument(
        "--format",
        choices=("cursor", "claude", "exit"),
        default="exit",
        help="cursor=JSON permission; claude/exit=exit 0/2",
    )
    parser.add_argument(
        "--stdin-json",
        action="store_true",
        help="Read Cursor/Claude hook JSON from stdin",
    )
    args = parser.parse_args(argv)

    command = args.command or ""
    if args.stdin_json or (not command and not sys.stdin.isatty()):
        try:
            raw = sys.stdin.read()
            if raw.strip():
                payload = json.loads(raw)
                command = extract_command(payload) or command
                # Auto-detect cursor if permission-shaped expectation
                if args.format == "exit" and "command" in (payload or {}) and "tool_input" not in payload:
                    args.format = "cursor"
                if args.format == "exit" and payload.get("hook_event_name") == "PreToolUse":
                    args.format = "claude"
                if args.format == "exit" and payload.get("tool_name") == "Bash":
                    args.format = "claude"
        except json.JSONDecodeError:
            if args.format == "cursor":
                sys.stdout.write(
                    json.dumps(
                        {
                            "permission": "ask",
                            "agent_message": "block_git_write: invalid stdin JSON",
                        }
                    )
                    + "\n"
                )
                return 0

    reason = deny_reason_for(command)

    if args.format == "cursor":
        sys.stdout.write(json.dumps(cursor_decision(command)) + "\n")
        return 0

    if reason:
        sys.stderr.write(reason + "\n")
        return 2
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
