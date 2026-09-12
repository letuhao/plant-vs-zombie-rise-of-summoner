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

## When and what to commit

**Commit what you changed, when the change is done — often, in small coherent pieces.**

1. **One logical change per commit.** A subsystem change plus its test plus its doc/tuning update is
   one commit. Two unrelated fixes are two.
2. **Commit at the end of each verified increment**, not at the end of the session. If a task has
   three landable pieces, commit three times. A long uncommitted stretch is how work is lost to a
   context reset or a crashed run.
3. **Pass `paths` explicitly.** `repo-git.commit(paths=[...])` stages exactly the files this change
   touched; leave `all=false`. Use `all=true` only when the entire tree is one stream's work — this
   repo runs parallel programs, so a broad `-a` can sweep another stream's half-finished files.
4. **Leave other streams' files alone.** `git status` will show unrelated dirty files; do not include
   them and do not revert them. A concurrent stream's uncommitted work is not yours to touch.
5. **Verify before you commit.** Run the subsystem's test/lint/guard. If it cannot run, say so in the
   message body and do not present the commit as a finished, green change.
6. **Message:** imperative subject (~72 chars) focused on *why*; add a body when the subject is not
   enough. No trailers, no vendor names, no file-list-only subjects.
7. **Never commit** secrets, game binaries, `data/`, `dist/`, machine-local paths (`H:\Games\...`), or
   gitignored local config (`.kilo/`, `.claude/`, `.cursor/`, `.agents/`, `AGENTS.md`, `CLAUDE.md`).
8. **Commit at task boundaries** — when each task in a plan/runbook completes, before a long
   unattended run, and before switching programs. A correction after the fact is a **new commit**, not
   an amend: do not rewrite history.

## One-time setup

```powershell
powershell -File scripts/commit-tool/install_hooks.ps1
# optional PATH shim for non-Cursor/Claude agents:
powershell -File scripts/commit-tool/install_hooks.ps1 -InstallShim
```

### If `repo-git.commit` reports `Request timed out` (-32001)

The MCP server is a **per-session stdio child**, not a daemon — the client starts it on demand
(cold start <1s) and it needs no "always run" setup. A `-32001` on `commit` while
`validate_message` answers in milliseconds is a **hang inside the tool call**, and the usual cause
is a child process inheriting the server's stdin (the JSON-RPC pipe) and blocking on it. Every git
subprocess in the tool must pass `stdin=subprocess.DEVNULL` (fixed in `clean_commit.repo_root` /
`run_git`, `validate.git_var`, `check_history`). The commit may still land even when the reply
times out — check `git log` before retrying.

**After editing anything under `scripts/commit-tool/`, reload the MCP server** (restart the IDE /
reconnect `repo-git`). An already-running session keeps the old code until then.

That copies **local gitignored** configs from `scripts/commit-tool/templates/` into `.cursor/`, `.claude/`, `.kilo/`, `.agents/`, and `.mcp.json`. Those trees stay out of git — do not carve them into the repo.

Then:

- Cursor: restart / reload MCP; confirm `repo-git` is connected.
- Claude Code: trust workspace, `/mcp` approve `repo-git`.
- Hygiene: turn off IDE Attribution (does not replace hooks).

## Owner/debug CLI

`python scripts/commit-tool/clean_commit.py` remains for humans. Agents must not use it.

## CI

`python scripts/commit-tool/check_history.py` runs in CI and fails on allowlist/trailer violations in the PR range.
