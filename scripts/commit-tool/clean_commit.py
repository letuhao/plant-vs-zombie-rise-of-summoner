#!/usr/bin/env python3
"""Clean commit entry point — the only commit path agents may use.

Forces an allowlisted author/committer, rejects vendor trailers / watermark
phrasing, and never passes agent-injected GIT_* identity env through.

Usage (repo root):
  python scripts/commit-tool/clean_commit.py -m "Fix overflow in SoulEarnPolicy"
  python scripts/commit-tool/clean_commit.py -m "Add delve loot table" -- path/a path/b
  python scripts/commit-tool/clean_commit.py -F message.txt
  python scripts/commit-tool/clean_commit.py -m "..." --all
  python scripts/commit-tool/clean_commit.py -m "..." --amend   # only when policy allows amend

Exit codes: 0 ok, 1 policy/git failure, 2 usage error.
"""
from __future__ import annotations

import argparse
import os
import subprocess
import sys
import tempfile
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


def repo_root() -> Path:
    out = subprocess.check_output(["git", "rev-parse", "--show-toplevel"], text=True)
    return Path(out.strip())


def run_git(args: list[str], *, env: dict[str, str] | None = None) -> subprocess.CompletedProcess[str]:
    return subprocess.run(["git", *args], text=True, capture_output=True, env=env, check=False)


def staged_paths() -> list[str]:
    cp = run_git(["diff", "--cached", "--name-only", "--"])
    if cp.returncode != 0:
        raise RuntimeError(cp.stderr.strip() or "git diff --cached failed")
    return [ln for ln in cp.stdout.splitlines() if ln.strip()]


def build_commit_env(name: str, email: str) -> dict[str, str]:
    """Pin author/committer and strip common agent trailer injectors."""
    env = os.environ.copy()
    # Force identity — do not trust ambient GIT_AUTHOR_* from the agent shell.
    env["GIT_AUTHOR_NAME"] = name
    env["GIT_AUTHOR_EMAIL"] = email
    env["GIT_COMMITTER_NAME"] = name
    env["GIT_COMMITTER_EMAIL"] = email
    # Prevent template / trailer helpers from appending Co-authored-by.
    env.pop("GIT_EDITOR", None)
    env.pop("SEQUENCE_EDITOR", None)
    # Some harnesses inject via these:
    for key in list(env):
        if key.upper().startswith("GIT_TRAILER") or key.upper().startswith("TRAILER_"):
            env.pop(key, None)
    return env


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(
        description="Commit with allowlisted identity and anti-watermark message checks"
    )
    parser.add_argument("-m", "--message", help="Commit message")
    parser.add_argument("-F", "--file", type=Path, help="Read commit message from file")
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--all", "-a", action="store_true", help="git commit -a (tracked modifications)")
    parser.add_argument("--amend", action="store_true", help="Amend HEAD (still re-validates message)")
    parser.add_argument(
        "--allow-empty",
        action="store_true",
        help="Pass --allow-empty through (still validates message/identity)",
    )
    parser.add_argument(
        "paths",
        nargs="*",
        help="Optional paths to `git add` before commit",
    )
    args = parser.parse_args(argv)

    if bool(args.message) == bool(args.file):
        parser.error("provide exactly one of -m/--message or -F/--file")

    try:
        root = repo_root()
    except (subprocess.CalledProcessError, FileNotFoundError) as exc:
        print(f"clean-commit: not a git repo / git missing: {exc}", file=sys.stderr)
        return 2

    os.chdir(root)
    policy = load_policy(args.policy)
    pairs = allowed_pairs(policy)
    name, email = pairs[0]
    if len(pairs) > 1:
        # Prefer exact match to current git user.email if it is allowlisted.
        cp = run_git(["config", "--get", "user.email"])
        current_email = (cp.stdout or "").strip().lower()
        for n, e in pairs:
            if e == current_email:
                name, email = n, e
                break

    if args.file:
        message = args.file.read_text(encoding="utf-8")
    else:
        message = args.message or ""

    # Message-only check first for fast feedback
    msg_errors = validate_message(message, policy)
    if msg_errors:
        print("clean-commit: message rejected:", file=sys.stderr)
        for e in msg_errors:
            print(f"  - {e}", file=sys.stderr)
        return 1

    ident_errors = validate_commit_context(
        message,
        policy,
        author_name=name,
        author_email=email,
        committer_name=name,
        committer_email=email,
        check_git_vars=False,
    )
    if ident_errors:
        print("clean-commit: identity rejected:", file=sys.stderr)
        for e in ident_errors:
            print(f"  - {e}", file=sys.stderr)
        return 1

    if args.paths:
        add = run_git(["add", "--", *args.paths])
        if add.returncode != 0:
            print(add.stderr or add.stdout, file=sys.stderr)
            return 1

    if not args.amend and not args.allow_empty and not args.all:
        if not staged_paths():
            print(
                "clean-commit: nothing staged. Pass paths, use --all, or git add first.",
                file=sys.stderr,
            )
            return 1

    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".msg") as fh:
        fh.write(message.strip() + "\n")
        msg_path = fh.name

    try:
        # -c trailer.* clears common auto-trailer injectors without touching signing.
        commit_args = [
            "-c",
            "trailer.ifexists=",
            "commit",
            "-F",
            msg_path,
            "--cleanup=strip",
        ]
        if args.all:
            commit_args.append("-a")
        if args.amend:
            commit_args.append("--amend")
        if args.allow_empty:
            commit_args.append("--allow-empty")

        env = build_commit_env(name, email)
        cp = run_git(commit_args, env=env)
        if cp.returncode != 0:
            sys.stderr.write(cp.stderr or cp.stdout or "git commit failed\n")
            return 1
        if cp.stdout:
            sys.stdout.write(cp.stdout)
        # Show resulting identity once
        show = run_git(["log", "-1", "--format=%h %an <%ae>%n%s"])
        if show.returncode == 0:
            sys.stdout.write(show.stdout)
        return 0
    finally:
        try:
            os.unlink(msg_path)
        except OSError:
            pass


if __name__ == "__main__":
    sys.exit(main())
