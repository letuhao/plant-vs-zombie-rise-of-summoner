# Git commits — MCP only

Agents must **not** run `git commit`, `git push`, `git rebase`/`cherry-pick`, or
`python scripts/commit-tool/clean_commit.py`.

`git merge` is allowed (owner decision, 2026-09-13) when run with no `-C`/`--git-dir` override —
i.e. against the agent's own current worktree only. It never rewrites history and can only ever
advance the current branch by folding in a sibling branch's already-committed work; the branch
merged FROM is never touched. A clean merge auto-commits with git's own boilerplate message (no
free-form agent prose to watermark-check). A conflicted merge stops with no commit at all —
finishing it still needs a real `git commit`/`git merge --continue`, which stays blocked, so an
agent that hits a conflict hands it back to the owner rather than resolving and committing it.

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
