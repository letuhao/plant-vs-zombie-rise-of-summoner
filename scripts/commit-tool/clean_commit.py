#!/usr/bin/env python3
"""Shared perform_commit library + owner/debug CLI.

Agents must commit via MCP repo-git.commit (not this CLI).
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile
from dataclasses import dataclass
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
if str(TOOL_DIR) not in sys.path:
    sys.path.insert(0, str(TOOL_DIR))

from validate import (  # noqa: E402
    DEFAULT_POLICY,
    allowed_pairs,
    load_policy,
    validate_commit_context,
    validate_message,
)

REQUIRED_HOOKS_PATH = ".githooks"
MCP_ENV_FLAG = "REPO_GIT_MCP"


@dataclass
class CommitResult:
    ok: bool
    errors: list[str]
    hash: str | None = None
    subject: str | None = None
    author: str | None = None
    stdout: str = ""
    stderr: str = ""


def repo_root() -> Path:
    try:
        out = subprocess.check_output(
            ["git", "rev-parse", "--show-toplevel"],
            text=True,
            stderr=subprocess.DEVNULL,
            # The MCP server's stdin IS the JSON-RPC pipe. A child that inherits it (the
            # default) can block forever on Windows when git touches stdin, hanging the tool
            # call until the client times out with -32001. Git never needs our stdin.
            stdin=subprocess.DEVNULL,
            cwd=str(TOOL_DIR),
        )
        return Path(out.strip())
    except (subprocess.CalledProcessError, FileNotFoundError):
        # MCP servers often start with cwd outside the repo — fall back from tool path.
        candidate = TOOL_DIR.parent.parent
        if (candidate / ".git").exists() or (candidate / ".githooks").exists():
            return candidate
        raise


def run_git(args: list[str], *, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(
        ["git", *args], text=True, capture_output=True, env=env, check=False,
        # Never inherit the MCP server's stdin (see repo_root): a git process that reads it
        # blocks the whole tool call. Hooks git spawns (prepare-commit-msg, commit-msg) inherit
        # this DEVNULL in turn, so they cannot block on stdin either.
        stdin=subprocess.DEVNULL,
    )


def staged_paths() -> list[str]:
    cp = run_git(["diff", "--cached", "--name-only", "--"])
    if cp.returncode != 0:
        raise RuntimeError(cp.stderr.strip() or "git diff --cached failed")
    return [ln for ln in cp.stdout.splitlines() if ln.strip()]


def resolve_author(policy: dict) -> tuple[str, str]:
    pairs = allowed_pairs(policy)
    name, email = pairs[0]
    if len(pairs) > 1:
        cp = run_git(["config", "--get", "user.email"])
        current_email = (cp.stdout or "").strip().lower()
        for n, e in pairs:
            if e == current_email:
                return n, e
    return name, email


def assert_hooks_path() -> list[str]:
    """Refuse commits unless this repo uses .githooks."""
    cp = run_git(["config", "--get", "core.hooksPath"])
    raw = (cp.stdout or "").strip().replace("\\", "/")
    if not raw:
        return [
            "core.hooksPath is unset; run: powershell -File scripts/commit-tool/install_hooks.ps1"
        ]
    # Accept ".githooks", "githooks" relative, or absolute path ending in .githooks
    normalized = raw.rstrip("/")
    if normalized == REQUIRED_HOOKS_PATH or normalized.endswith("/" + REQUIRED_HOOKS_PATH):
        return []
    return [f"core.hooksPath must be '{REQUIRED_HOOKS_PATH}'; got {raw!r}"]


def build_commit_env(name: str, email: str) -> dict[str, str]:
    """Pin author/committer; set REPO_GIT_MCP so an optional PATH shim allows the forward."""
    env = os.environ.copy()
    env["GIT_AUTHOR_NAME"] = name
    env["GIT_AUTHOR_EMAIL"] = email
    env["GIT_COMMITTER_NAME"] = name
    env["GIT_COMMITTER_EMAIL"] = email
    env[MCP_ENV_FLAG] = "1"
    env.pop("GIT_EDITOR", None)
    env.pop("SEQUENCE_EDITOR", None)
    for key in list(env):
        upper = key.upper()
        if upper.startswith("GIT_TRAILER") or upper.startswith("TRAILER_"):
            env.pop(key, None)
    return env


def perform_commit(
    message: str,
    *,
    paths: list[str] | None = None,
    all_tracked: bool = False,
    amend: bool = False,
    allow_empty: bool = False,
    policy_path: Path | None = None,
) -> CommitResult:
    """Validate + commit with allowlisted identity. Never passes --no-verify."""
    try:
        root = repo_root()
    except (subprocess.CalledProcessError, FileNotFoundError) as exc:
        return CommitResult(ok=False, errors=[f"not a git repo / git missing: {exc}"])

    os.chdir(root)
    policy = load_policy(policy_path or DEFAULT_POLICY)

    hook_errors = assert_hooks_path()
    if hook_errors:
        return CommitResult(ok=False, errors=hook_errors)

    name, email = resolve_author(policy)

    msg_errors = validate_message(message, policy)
    if msg_errors:
        return CommitResult(ok=False, errors=msg_errors)

    ident_errors = validate_commit_context(
        message,
        policy,
        author_name=name,
        author_email=email,
        committer_name=name,
        committer_email=email,
        check_git_vars=False,
    )
    # validate_commit_context includes message errors again — de-dupe
    if ident_errors:
        # Filter to identity-only if message already checked
        only_ident = [e for e in ident_errors if e not in msg_errors]
        if only_ident:
            return CommitResult(ok=False, errors=only_ident)

    if paths:
        add = run_git(["add", "--", *paths])
        if add.returncode != 0:
            return CommitResult(
                ok=False,
                errors=[(add.stderr or add.stdout or "git add failed").strip()],
            )

    if not amend and not allow_empty and not all_tracked:
        try:
            if not staged_paths():
                return CommitResult(
                    ok=False,
                    errors=["nothing staged; pass paths, use all=true, or git add first"],
                )
        except RuntimeError as exc:
            return CommitResult(ok=False, errors=[str(exc)])

    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".msg") as fh:
        fh.write(message.strip() + "\n")
        msg_path = fh.name

    try:
        commit_args = [
            "-c",
            "trailer.ifexists=",
            "commit",
            "-F",
            msg_path,
            "--cleanup=strip",
        ]
        if all_tracked:
            commit_args.append("-a")
        if amend:
            commit_args.append("--amend")
        if allow_empty:
            commit_args.append("--allow-empty")

        env = build_commit_env(name, email)
        cp = run_git(commit_args, env=env)
        if cp.returncode != 0:
            return CommitResult(
                ok=False,
                errors=[(cp.stderr or cp.stdout or "git commit failed").strip()],
                stderr=cp.stderr or "",
                stdout=cp.stdout or "",
            )

        show = run_git(["log", "-1", "--format=%H%n%s%n%an <%ae>"])
        commit_hash = subject = author = None
        if show.returncode == 0:
            lines = show.stdout.splitlines()
            if len(lines) >= 3:
                commit_hash, subject, author = lines[0], lines[1], lines[2]
            elif lines:
                commit_hash = lines[0]

        return CommitResult(
            ok=True,
            errors=[],
            hash=commit_hash,
            subject=subject,
            author=author,
            stdout=cp.stdout or "",
            stderr=cp.stderr or "",
        )
    finally:
        try:
            os.unlink(msg_path)
        except OSError:
            pass


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description=(
            "Owner/debug clean commit. Agents must use MCP repo-git.commit instead."
        )
    )
    parser.add_argument("-m", "--message", help="Commit message")
    parser.add_argument("-F", "--file", type=Path, help="Read commit message from file")
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--all", "-a", action="store_true", help="git commit -a")
    parser.add_argument("--amend", action="store_true", help="Amend HEAD")
    parser.add_argument("--allow-empty", action="store_true")
    parser.add_argument("paths", nargs="*", help="Optional paths to git add before commit")
    args = parser.parse_args(argv)

    if bool(args.message) == bool(args.file):
        parser.error("provide exactly one of -m/--message or -F/--file")

    if args.file:
        message = args.file.read_text(encoding="utf-8")
    else:
        message = args.message or ""

    result = perform_commit(
        message,
        paths=list(args.paths) if args.paths else None,
        all_tracked=args.all,
        amend=args.amend,
        allow_empty=args.allow_empty,
        policy_path=args.policy,
    )
    if not result.ok:
        print("clean-commit: rejected:", file=sys.stderr)
        for e in result.errors:
            print(f"  - {e}", file=sys.stderr)
        if result.stderr:
            sys.stderr.write(result.stderr)
        return 1
    if result.stdout:
        sys.stdout.write(result.stdout)
    if result.hash:
        print(f"{result.hash[:12]} {result.author}")
        print(result.subject or "")
    return 0


if __name__ == "__main__":
    sys.exit(main())
