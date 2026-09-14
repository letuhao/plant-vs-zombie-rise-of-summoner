#!/usr/bin/env python3
"""Smoke checks for commit-tool (no permanent git writes)."""
from __future__ import annotations

import json
import subprocess
import sys
import tempfile
from pathlib import Path

TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR))

from block_git_write import deny_reason_for  # noqa: E402
from strip_watermarks import strip_file  # noqa: E402
from validate import load_policy, validate_identity, validate_message  # noqa: E402


def expect(cond: bool, msg: str) -> None:
    if not cond:
        raise AssertionError(msg)


def main() -> int:
    policy = load_policy(TOOL_DIR / "policy.json")
    name = policy["allowedAuthors"][0]["name"]
    email = policy["allowedAuthors"][0]["email"]

    expect(validate_message("Fix overflow in SoulEarnPolicy\n", policy) == [], "clean message")
    bad = validate_message(
        "Fix thing\n\nCo-authored-by: Cursor <cursoragent@cursor.com>\n",
        policy,
    )
    expect(len(bad) > 0, f"trailer reject: {bad}")
    expect(len(validate_message("AI-generated fix\n", policy)) > 0, "watermark phrase")
    expect(validate_identity(name, email, policy, role="author") == [], "allowlisted")
    expect(len(validate_identity("Cursor Agent", "cursoragent@cursor.com", policy, role="author")) > 0, "vendor author")

    # block_git_write
    expect(deny_reason_for("git commit -m x") is not None, "deny commit")
    expect(deny_reason_for("npm test && git commit -m x --no-verify") is not None, "deny chained")
    expect(deny_reason_for("git -c core.hooksPath=/dev/null commit -m x") is not None, "deny hooksPath")
    expect(deny_reason_for("git push origin main") is not None, "deny push")
    expect(deny_reason_for("gh pr create --title t") is not None, "deny gh pr")
    expect(deny_reason_for("python scripts/commit-tool/clean_commit.py -m x") is not None, "deny clean_commit")
    expect(deny_reason_for("git status") is None, "allow status")
    expect(deny_reason_for("git check-attr eol -- .githooks/commit-msg") is None, "allow check-attr path")
    expect(deny_reason_for("git diff --cached") is None, "allow diff")

    # strip watermarks
    with tempfile.NamedTemporaryFile("w", encoding="utf-8", delete=False, suffix=".msg") as fh:
        fh.write("Subject\n\nCo-authored-by: Cursor <cursoragent@cursor.com>\n")
        path = Path(fh.name)
    try:
        strip_file(path)
        text = path.read_text(encoding="utf-8")
        expect("Co-authored-by" not in text, f"strip failed: {text!r}")
        expect("Subject" in text, "kept subject")
    finally:
        path.unlink(missing_ok=True)

    # Cursor gate JSON
    import importlib.util

    gate_path = TOOL_DIR / "cursor" / "gate_git_commit.py"
    spec = importlib.util.spec_from_file_location("gate_git_commit", gate_path)
    assert spec and spec.loader
    gate = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(gate)
    out = subprocess.check_output(
        [sys.executable, str(gate_path)],
        input=json.dumps({"command": "git commit -m x"}),
        text=True,
    )
    expect(json.loads(out)["permission"] == "deny", "cursor deny")
    out2 = subprocess.check_output(
        [sys.executable, str(gate_path)],
        input=json.dumps({"command": "git status"}),
        text=True,
    )
    expect(json.loads(out2)["permission"] == "allow", "cursor allow")

    # Claude-style exit codes
    r = subprocess.run(
        [sys.executable, str(TOOL_DIR / "block_git_write.py"), "--command", "git push", "--format", "claude"],
        capture_output=True,
        text=True,
    )
    expect(r.returncode == 2, f"claude deny exit={r.returncode}")

    # MCP validate_message path without needing server running
    from clean_commit import assert_hooks_path  # noqa: E402

    # hooksPath should be set after install; if unset, assert returns errors (ok either way)
    _ = assert_hooks_path()

    # Hang guards (2026-09-12): every git child is bounded, and a stall becomes an error
    # result rather than hanging the MCP call until the client's -32001.
    import clean_commit as cc  # noqa: E402
    from validate import GIT_TIMEOUT_SECONDS as _validate_timeout  # noqa: E402

    expect(isinstance(_validate_timeout, (int, float)) and _validate_timeout > 0,
           f"validate timeout must be a positive number, got {_validate_timeout!r}")
    expect(cc.GIT_TIMEOUT_SECONDS == _validate_timeout,
           "clean_commit and validate must share one git timeout (no drift)")

    # Force a sub-millisecond timeout to prove the path, not wait 120s for it.
    saved = cc.GIT_TIMEOUT_SECONDS
    try:
        cc.GIT_TIMEOUT_SECONDS = 0.001
        cp = cc.run_git(["status", "--porcelain"])
        expect(cp.returncode == 124, f"timed-out git must return 124, got {cp.returncode}")
        expect("timed out" in (cp.stderr or ""), f"timed-out git must explain itself: {cp.stderr!r}")
    finally:
        cc.GIT_TIMEOUT_SECONDS = saved

    # The MCP server reloads its own modules per call, so an edit applies without an IDE
    # restart (the proven stale-server -32001 cause).
    server_src = (TOOL_DIR / "mcp_server.py").read_text(encoding="utf-8")
    expect("_reload_tool_modules" in server_src, "mcp_server must reload tool modules")
    expect(server_src.count("_reload_tool_modules()") >= 3,
           "both tools must call _reload_tool_modules (definition + 2 calls)")

    # Worktree commit target: a relative path anchors to the main checkout, not the server cwd,
    # and repo_root(cwd=...) resolves that checkout's own toplevel (linked worktree support).
    import clean_commit as cc2  # noqa: E402

    main_root = cc2.main_repo_root()
    rel = cc2.resolve_worktree_path(".kilo/worktrees/example-id")
    expect(str(rel).replace("\\", "/").endswith("/.kilo/worktrees/example-id"),
           f"relative worktree must anchor to the main checkout: {rel}")
    expect(cc2.repo_root() == main_root.resolve(), "default repo_root is the main checkout")
    wt = TOOL_DIR.parent.parent / ".kilo" / "worktrees" / "solid-run-20260912-eb53"
    if wt.is_dir():
        resolved = cc2.repo_root(wt)
        expect(resolved == wt.resolve(),
               f"worktree target must resolve its own toplevel, got {resolved}")
        expect(str(cc2.resolve_worktree_path(str(wt))) == str(wt), "absolute worktree passthrough")
    expect("worktree" in server_src, "MCP commit tool must expose the worktree parameter")

    data = json.loads((TOOL_DIR / "policy.json").read_text(encoding="utf-8"))
    expect(data.get("schemaVersion") == 1, "schemaVersion")

    # Templates present (tracked); live .mcp.json is gitignored and installer-copied
    root = TOOL_DIR.parent.parent
    expect((TOOL_DIR / "templates" / "mcp.json").is_file(), "templates/mcp.json")
    expect("repo-git" in (TOOL_DIR / "templates" / "mcp.json").read_text(encoding="utf-8"), "mcp server name")
    expect((TOOL_DIR / "mcp_server.py").is_file(), "mcp_server.py")
    expect((TOOL_DIR / "block_git_write.py").is_file(), "block_git_write.py")

    print("commit-tool smoke: ok")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
