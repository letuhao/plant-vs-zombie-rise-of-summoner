# Commit MCP gate — todo

- [x] Soft-vs-hard matrix locked in docs (`docs/contributing/agent-git.md`)
- [x] Refactor `perform_commit`; CLI human-only; hooksPath refuse; `REPO_GIT_MCP=1`
- [x] `mcp_server.py` + `requirements.txt` + smoke tests
- [x] Templates for MCP/host configs (`.cursor`/`.claude`/`.kilo`/`.agents`/`.mcp.json` stay gitignored; installer copies)
- [x] `block_git_write.py` + Cursor/Claude/Kilo/.agents via installer
- [x] `prepare-commit-msg` + commit-msg; optional PATH git shim
- [x] `check_history.py` + CI/guard wire
- [x] AGENTS.md / CLAUDE.md / CONTRIBUTING / docs
- [x] Expand `install_hooks.ps1`; no gitignore carve-outs
