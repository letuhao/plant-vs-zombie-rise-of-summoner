# Commit MCP gate — plan

**Program:** `commit-mcp`  
**Artifacts:** `tasks/commit-mcp-plan.md`, `tasks/commit-mcp-todo.md`  
**SSOT tool:** `scripts/commit-tool/`

## Goal

Agents may create commits **only** via MCP `repo-git.commit`. Push is owner-only. Docs/rules are soft; validators, host shell gates, `.githooks`, optional PATH shim, and CI are hard.

## Soft vs hard

| Soft | Hard |
|---|---|
| AGENTS.md / CLAUDE.md / Cursor & agents rules | Cursor beforeShellExecution, Claude PreToolUse, Kilo plugin |
| “prefer MCP” instructions | MCP validators + `.githooks` + CI `check_history.py` |

## Deliverables

1. `perform_commit` library + hooksPath refusal + `REPO_GIT_MCP=1`
2. MCP server (`commit`, `validate_message`) + `.mcp.json` / `.cursor/mcp.json` / kilo mcp
3. `block_git_write.py` wired to Cursor / Claude / Kilo
4. `prepare-commit-msg` + strengthened `commit-msg`
5. Optional git shim; CI history check
6. Docs + gitignore carve-outs + installer

## Verification

See todo checklist. Smoke: trailer reject, chained `git commit` deny, hooksPath refuse, allowlisted commit via MCP path.
