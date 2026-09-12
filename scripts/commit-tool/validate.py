#!/usr/bin/env python3
"""Shared commit policy validation for clean-commit and git hooks.

Exit codes: 0 ok, 1 policy violation, 2 usage/config error.
"""
from __future__ import annotations

import argparse
import json
import os
import re
import subprocess
import sys
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
DEFAULT_POLICY = TOOL_DIR / "policy.json"

# Git trailer line: Key: value  (also accepts Key=value)
TRAILER_LINE = re.compile(r"^([A-Za-z0-9][A-Za-z0-9-]{0,49})[:=]\s+\S")
IDENT_RE = re.compile(r"^(?P<name>.+?)\s+<(?P<email>[^>]+)>(?:\s+\d+\s+[+-]\d+)?\s*$")


def load_policy(path: Path | None = None) -> dict:
    policy_path = path or DEFAULT_POLICY
    try:
        raw = policy_path.read_text(encoding="utf-8")
    except OSError as exc:
        raise SystemExit(f"commit-tool: cannot read policy {policy_path}: {exc}") from exc
    try:
        data = json.loads(raw)
    except json.JSONDecodeError as exc:
        raise SystemExit(f"commit-tool: invalid JSON in {policy_path}: {exc}") from exc
    if not isinstance(data.get("allowedAuthors"), list) or not data["allowedAuthors"]:
        raise SystemExit("commit-tool: policy.allowedAuthors must be a non-empty list")
    return data


def _norm(s: str) -> str:
    return " ".join((s or "").strip().split())


def _norm_email(s: str) -> str:
    return _norm(s).lower()


def allowed_pairs(policy: dict) -> list[tuple[str, str]]:
    out: list[tuple[str, str]] = []
    for row in policy["allowedAuthors"]:
        name = _norm(row.get("name", ""))
        email = _norm_email(row.get("email", ""))
        if not name or not email or "@" not in email:
            raise SystemExit(f"commit-tool: invalid allowedAuthor entry: {row!r}")
        out.append((name, email))
    return out


def parse_ident(ident: str) -> tuple[str, str]:
    m = IDENT_RE.match(_norm(ident))
    if not m:
        raise ValueError(f"cannot parse git identity: {ident!r}")
    return _norm(m.group("name")), _norm_email(m.group("email"))


def git_var(name: str) -> str:
    try:
        out = subprocess.check_output(["git", "var", name], text=True, stderr=subprocess.DEVNULL)
    except (subprocess.CalledProcessError, FileNotFoundError) as exc:
        raise RuntimeError(f"git var {name} failed: {exc}") from exc
    return out.strip()


def validate_identity(name: str, email: str, policy: dict, *, role: str) -> list[str]:
    errors: list[str] = []
    name_n = _norm(name)
    email_n = _norm_email(email)
    pairs = allowed_pairs(policy)
    if (name_n, email_n) in pairs:
        return errors

    allowed = ", ".join(f"{n} <{e}>" for n, e in pairs)
    errors.append(
        f"{role} must be an allowed identity; got {name_n!r} <{email_n}>. Allowed: {allowed}"
    )
    for needle in policy.get("forbiddenAuthorNameSubstrings") or []:
        if needle and needle.lower() in name_n.lower():
            errors.append(f"{role} name contains forbidden token {needle!r}")
            break
    for needle in policy.get("forbiddenAuthorEmailSubstrings") or []:
        if needle and needle.lower() in email_n:
            errors.append(f"{role} email contains forbidden token {needle!r}")
            break
    return errors


def _split_subject_body(message: str) -> tuple[str, str]:
    text = message.replace("\r\n", "\n").replace("\r", "\n")
    if "\n" not in text:
        return text.strip(), ""
    subject, body = text.split("\n", 1)
    return subject.strip(), body


def _extract_trailing_trailers(body: str) -> list[str]:
    """Return trailer lines at the end of the message body (git trailer block)."""
    lines = body.replace("\r\n", "\n").replace("\r", "\n").split("\n")
    # Drop final empty lines
    while lines and lines[-1].strip() == "":
        lines.pop()
    if not lines:
        return []

    trailers: list[str] = []
    i = len(lines) - 1
    while i >= 0 and TRAILER_LINE.match(lines[i].strip()):
        trailers.append(lines[i].strip())
        i -= 1
    # A trailer block is only a trailer block if separated by a blank line
    # (or it is the entire body). Git is looser; we treat contiguous trailing
    # Key: value lines as trailers when preceded by blank or start.
    if not trailers:
        return []
    if i >= 0 and lines[i].strip() != "":
        # contiguous with body text — still ban if key is a known trailer
        return list(reversed(trailers))
    return list(reversed(trailers))


def validate_message(message: str, policy: dict) -> list[str]:
    errors: list[str] = []
    text = message.replace("\r\n", "\n").replace("\r", "\n")
    if not text.strip():
        errors.append("commit message is empty")
        return errors

    subject, body = _split_subject_body(text)
    if not subject:
        errors.append("commit subject is empty")
    if subject.startswith("fix:"):
        # keep style soft; not an error
        pass
    max_len = int(policy.get("subjectMaxLen") or 72)
    if subject and len(subject) > max_len:
        # Soft style hint only — do not hard-fail long subjects.
        print(
            f"commit-tool: warning: subject is {len(subject)} chars (preferred max {max_len})",
            file=sys.stderr,
        )

    # Ban known trailer keys anywhere they look like trailer lines
    forbid_any = bool(policy.get("forbidAnyTrailers", True))
    forbidden_keys = {k.lower() for k in (policy.get("forbiddenTrailerKeys") or [])}
    trailing = _extract_trailing_trailers(body)
    for line in trailing:
        m = TRAILER_LINE.match(line)
        if not m:
            continue
        key = m.group(1)
        if forbid_any or key.lower() in forbidden_keys:
            errors.append(f"forbidden commit trailer: {line}")

    # Also scan full message for Co-authored-by-style lines not only at end
    for line in text.split("\n"):
        s = line.strip()
        m = TRAILER_LINE.match(s)
        if not m:
            continue
        key = m.group(1)
        if key.lower() in forbidden_keys:
            errors.append(f"forbidden commit trailer line: {s}")

    for pattern in policy.get("forbiddenMessagePatterns") or []:
        try:
            if re.search(pattern, text):
                errors.append(f"commit message matches forbidden pattern: {pattern}")
        except re.error as exc:
            raise SystemExit(f"commit-tool: bad forbiddenMessagePatterns entry {pattern!r}: {exc}") from exc

    # De-dupe while preserving order
    seen: set[str] = set()
    uniq: list[str] = []
    for e in errors:
        if e not in seen:
            seen.add(e)
            uniq.append(e)
    return uniq


def validate_commit_context(
    message: str,
    policy: dict,
    *,
    author_name: str | None = None,
    author_email: str | None = None,
    committer_name: str | None = None,
    committer_email: str | None = None,
    check_git_vars: bool = False,
) -> list[str]:
    errors = validate_message(message, policy)

    def resolve(name: str | None, email: str | None, var: str) -> tuple[str, str]:
        if name is not None and email is not None:
            return _norm(name), _norm_email(email)
        if check_git_vars:
            return parse_ident(git_var(var))
        raise RuntimeError(f"missing {var} identity")

    try:
        an, ae = resolve(author_name, author_email, "GIT_AUTHOR_IDENT")
        cn, ce = resolve(committer_name, committer_email, "GIT_COMMITTER_IDENT")
    except (RuntimeError, ValueError) as exc:
        errors.append(str(exc))
        return errors

    errors.extend(validate_identity(an, ae, policy, role="author"))
    errors.extend(validate_identity(cn, ce, policy, role="committer"))
    return errors


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description="Validate a commit message / identity against policy")
    parser.add_argument("--policy", type=Path, default=DEFAULT_POLICY)
    parser.add_argument("--commit-msg-file", type=Path, help="Path from git commit-msg hook")
    parser.add_argument("--message", "-m", help="Message text to validate")
    parser.add_argument("--author-name")
    parser.add_argument("--author-email")
    parser.add_argument("--committer-name")
    parser.add_argument("--committer-email")
    parser.add_argument(
        "--git-ident",
        action="store_true",
        help="Resolve author/committer via `git var` (for hooks)",
    )
    args = parser.parse_args(argv)

    policy = load_policy(args.policy)

    if args.commit_msg_file:
        message = args.commit_msg_file.read_text(encoding="utf-8")
        check_git = True
    elif args.message is not None:
        message = args.message
        check_git = bool(args.git_ident)
    else:
        parser.error("provide --commit-msg-file or --message")

    try:
        errors = validate_commit_context(
            message,
            policy,
            author_name=args.author_name,
            author_email=args.author_email,
            committer_name=args.committer_name,
            committer_email=args.committer_email,
            check_git_vars=check_git or args.git_ident,
        )
    except SystemExit:
        raise
    except Exception as exc:  # noqa: BLE001 — surface as policy/tool failure
        print(f"commit-tool: {exc}", file=sys.stderr)
        return 2

    if errors:
        print("commit-tool: policy rejected this commit:", file=sys.stderr)
        for e in errors:
            print(f"  - {e}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    # Hooks should not inherit a poisoned PYTHONPATH from the agent shell.
    os.environ.pop("PYTHONPATH", None)
    sys.exit(main())
