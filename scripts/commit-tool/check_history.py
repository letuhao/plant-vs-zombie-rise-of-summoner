#!/usr/bin/env python3
"""CI/local check: recent commits must use allowlisted author and clean messages."""
from __future__ import annotations

import argparse
import subprocess
import sys
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR))

from validate import (  # noqa: E402
    DEFAULT_POLICY,
    load_policy,
    validate_identity,
    validate_message,
)


def git_lines(args: list[str]) -> list[str]:
    out = subprocess.check_output(
        ["git", *args], text=True, stderr=subprocess.DEVNULL, stdin=subprocess.DEVNULL)
    return [ln for ln in out.splitlines() if ln.strip()]


def commits_in_range(rev_range: str) -> list[str]:
    try:
        return git_lines(["rev-list", "--reverse", rev_range])
    except subprocess.CalledProcessError:
        return []


def check_commit(sha: str, policy: dict) -> list[str]:
    errors: list[str] = []
    meta = subprocess.check_output(
        ["git", "show", "-s", "--format=%an%n%ae%n%cn%n%ce%n%B", sha],
        text=True,
        stdin=subprocess.DEVNULL,
    )
    parts = meta.split("\n", 4)
    if len(parts) < 5:
        return [f"{sha}: cannot parse commit metadata"]
    an, ae, cn, ce, body = parts[0], parts[1], parts[2], parts[3], parts[4]
    errors.extend(validate_identity(an, ae, policy, role=f"{sha[:12]} author"))
    errors.extend(validate_identity(cn, ce, policy, role=f"{sha[:12]} committer"))
    for e in validate_message(body, policy):
        errors.append(f"{sha[:12]}: {e}")
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Check commit history against commit-tool policy")
    parser.add_argument(
        "--range",
        default="",
        help="git rev-list range (default: origin/main..HEAD if exists else HEAD~20..HEAD)",
    )
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--max", type=int, default=50, help="Max commits to scan when using fallback")
    args = parser.parse_args(argv)

    policy = load_policy(args.policy)
    rev_range = args.range
    if not rev_range:
        # Prefer PR range
        for candidate in ("origin/main..HEAD", "origin/master..HEAD", f"HEAD~{args.max}..HEAD"):
            try:
                subprocess.check_output(
                    ["git", "rev-list", "--count", candidate],
                    text=True,
                    stderr=subprocess.DEVNULL,
                    stdin=subprocess.DEVNULL,
                )
                rev_range = candidate
                break
            except subprocess.CalledProcessError:
                continue
        if not rev_range:
            rev_range = "HEAD"

    if rev_range == "HEAD":
        shas = git_lines(["rev-list", "-n", str(args.max), "HEAD"])
    else:
        shas = commits_in_range(rev_range)
        if not shas:
            # Shallow clones often lack HEAD~N / origin/main — fall back to tip history.
            shas = git_lines(["rev-list", "-n", str(args.max), "HEAD"])
            rev_range = f"HEAD~{args.max}..HEAD (fallback)"

    if not shas:
        print("check_history: no commits to scan", file=sys.stderr)
        return 2

    all_errors: list[str] = []
    for sha in shas:
        all_errors.extend(check_commit(sha, policy))

    if all_errors:
        print("check_history: policy violations:", file=sys.stderr)
        for e in all_errors:
            print(f"  - {e}", file=sys.stderr)
        return 1
    print(f"check_history: ok ({len(shas)} commit(s) in {rev_range})")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
