# Git commits — MCP only

Agents must **not** run `git commit`, `git push`, `git merge`/`rebase`/`cherry-pick`, or `python scripts/commit-tool/clean_commit.py`.

When the user asks to commit:

1. Call MCP server **`repo-git`** tool **`commit`** with a clean imperative message.
2. Optional: `validate_message` first.
3. Never add `Co-authored-by` or vendor watermarks.

**Push is owner-only.** Do not run `git push` or `gh pr create` / `gh release create`.

Install once: `powershell -File scripts/commit-tool/install_hooks.ps1`  
Policy: `docs/contributing/agent-git.md`
