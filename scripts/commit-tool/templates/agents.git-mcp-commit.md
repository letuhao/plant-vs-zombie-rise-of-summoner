# Git commits — MCP only

Agents must **not** run `git commit`, `git push`, `git merge`/`rebase`/`cherry-pick`, or `python scripts/commit-tool/clean_commit.py`.

When you finish real work:

1. Run the subsystem's test/lint/guard first, or say in the body that it could not run.
2. Call MCP server **`repo-git`** tool **`commit`** with a clean imperative message.
3. Pass explicit `paths` for what this change touched; leave `all` off. Leave other streams' dirty files alone.
4. Optional: `validate_message` first.
5. Never add `Co-authored-by` or vendor watermarks.

Commit **often** — one logical change per commit, at the end of each verified increment and each task
boundary, not once at the end of the session. A correction is a new commit, never an amend.

**Push is owner-only.** Do not run `git push` or `gh pr create` / `gh release create`.

Install once: `powershell -File scripts/commit-tool/install_hooks.ps1`  
Policy: `docs/contributing/agent-git.md`
