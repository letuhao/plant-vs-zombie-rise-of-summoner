#!/usr/bin/env python3
"""Smoke checks for commit-tool validators (no git writes)."""
from __future__ import annotations

import json
import sys
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR))

from validate import load_policy, validate_identity, validate_message  # noqa: E402


def expect(cond: bool, msg: str) -> None:
    if not cond:
        raise AssertionError(msg)


def main() -> int:
    policy = load_policy(TOOL_DIR / "policy.json")
    name, email = policy["allowedAuthors"][0]["name"], policy["allowedAuthors"][0]["email"]

    expect(validate_message("Fix overflow in SoulEarnPolicy\n", policy) == [], "clean message should pass")
    bad = validate_message(
        "Fix thing\n\nCo-authored-by: Cursor <cursoragent@cursor.com>\n",
        policy,
    )
    expect(any("trailer" in e.lower() or "co-authored" in e.lower() for e in bad), f"expected trailer reject, got {bad}")

    bad2 = validate_message("AI-generated fix for the lawn\n", policy)
    expect(len(bad2) > 0, "watermark phrase should fail")

    expect(validate_identity(name, email, policy, role="author") == [], "allowlisted author")
    bad_id = validate_identity("Cursor Agent", "cursoragent@cursor.com", policy, role="author")
    expect(len(bad_id) > 0, "vendor author must fail")

    # Cursor gate unit check
    import importlib.util

    gate_path = TOOL_DIR / "cursor" / "gate_git_commit.py"
    spec = importlib.util.spec_from_file_location("gate_git_commit", gate_path)
    assert spec and spec.loader
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)

    expect(gate.RAW_COMMIT.search('git commit -m "x"') is not None, "raw commit detect")
    expect(gate.RAW_COMMIT.search("git check-attr eol -- .githooks/commit-msg") is None, "path false positive")
    expect(gate.CLEAN.search('python scripts/commit-tool/clean_commit.py -m "x"') is not None, "clean detect")
    expect(gate.CLEAN.search('git commit -m "x"') is None, "clean must not match raw")
    expect(gate.decide("git commit -m x")["permission"] == "deny", "decide deny")
    expect(gate.decide("git check-attr eol -- .githooks/commit-msg")["permission"] == "allow", "decide allow path")
    expect(gate.decide('python scripts/commit-tool/clean_commit.py -m x')["permission"] == "allow", "decide allow clean")

    # policy file is valid JSON with schemaVersion
    data = json.loads((TOOL_DIR / "policy.json").read_text(encoding="utf-8"))
    expect(data.get("schemaVersion") == 1, "schemaVersion")

    print("commit-tool smoke: ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
