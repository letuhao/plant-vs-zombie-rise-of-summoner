# Agent git policy (MCP-only commit)

**Status:** binding for automated assistants. Soft (docs/rules) + hard (hooks/MCP/CI).

## Soft vs hard

| Soft (advisory) | Hard (mechanical) |
|---|---|
| `AGENTS.md`, `CLAUDE.md`, Cursor/`.agents` rules | Cursor `beforeShellExecution`, Claude PreToolUse, Kilo plugin |
| “prefer MCP” wording | MCP `repo-git` validators, `.githooks`, optional PATH shim, `check_history.py` |

## Rules

1. **Agents must not** run `git commit`, `git push`, `git merge` / `rebase` / `cherry-pick` (commit-creating), or `python scripts/commit-tool/clean_commit.py`.
2. **Agents may commit** via MCP server **`repo-git`** tool **`commit`**, as part of finishing a task
   without waiting to be asked. No other commit path.
3. **Push is owner-only.** No MCP push tool; shell gates deny `git push` and `gh pr create` / `gh release create`.
4. No `Co-authored-by` / vendor watermark trailers. Author must match `scripts/commit-tool/policy.json`.

## One-time setup

```powershell
powershell -File scripts/commit-tool/install_hooks.ps1
# optional PATH shim for non-Cursor/Claude agents:
powershell -File scripts/commit-tool/install_hooks.ps1 -InstallShim
```

That copies **local gitignored** configs from `scripts/commit-tool/templates/` into `.cursor/`, `.claude/`, `.kilo/`, `.agents/`, and `.mcp.json`. Those trees stay out of git — do not carve them into the repo.

Then:

- Cursor: restart / reload MCP; confirm `repo-git` is connected.
- Claude Code: trust workspace, `/mcp` approve `repo-git`.
- Hygiene: turn off IDE Attribution (does not replace hooks).

## Owner/debug CLI

`python scripts/commit-tool/clean_commit.py` remains for humans. Agents must not use it.

## CI

`python scripts/commit-tool/check_history.py` runs in CI and fails on allowlist/trailer violations in the PR range.
