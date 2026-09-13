# Session boundary standard

**Status: binding for every automated session.** A session must establish and record its boundary
before its first edit. The boundary is what keeps concurrent agents from crossing paths, and it is
the contract `scripts/session-boundary-check.ps1` enforces.

This file is the *policy*. The *record* is one JSON file per session under
[`tasks/sessions/`](../../tasks/sessions/README.md). The *procedure* is the `/session-start` command.

---

## 1. What a session boundary is

One session = **one problem**, not one program. The owner assigns an individual problem to each
session precisely so two agents never work the same files. That is the unit this standard protects.

`features/derived-stat-extension` and `main` are long-lived integration branches; several programs
converge on them. A session boundary says, for this session only:

| Field | Meaning | Example |
|---|---|---|
| `session` | stable id | `actor-hub-20260912-a3f2` |
| `program` | which program from `AGENTS.md` | `actor-hub-and-combat-power-solid-fixing` |
| `problem` | the one problem this session solves | `T6: delete BattleStatComposer` |
| `mode` | `direct` or `worktree` | `direct` |
| `branch` | branch this session commits to | `features/derived-stat-extension` |
| `worktree` | absolute worktree path, or `null` | `null` |
| `paths` | paths this session OWNS and may edit/commit | `["src/FusionRpg.Core/Battle/**"]` |
| `started` | ISO-8601 UTC | `2026-09-12T08:50:00Z` |
| `status` | `active` · `merged` · `abandoned` | `active` |

**`paths` is the load-bearing field.** It is both an edit fence and the `repo-git.commit(paths=[…])`
list. A path outside your boundary belongs to another session; do not edit it, do not revert it, do
not stash around it.

---

## 2. Defaults (owner chooses per session)

These are the repo's defaults; the owner may override any of them at session start.

| Question | Default | When to change |
|---|---|---|
| Mode | **`direct`** — edit the current branch in the main worktree | Choose `worktree` when the session is long, risky, or overlaps another active session's paths |
| Branch | **one shared integration branch** (`git branch --show-current`) | Choose a per-session branch when the session should not land until reviewed, or when the owner wants a separate merge point |
| Concurrency | **assume parallel sessions exist** | Never assume you are alone: read the other records first |
| Scope | **one problem**, with `paths` limited to what that problem touches | Split the problem or hand it to a worktree if it spans two programs |

Mode `direct` is cheap and needs no setup. Mode `worktree` is isolated but must be set up (see §5).

### Direct (default)

- Commit to the current branch with `repo-git.commit`, `paths` limited to your boundary.
- Before committing, `git status` will show other sessions' dirty files. **Include only your
  `paths`.** A broad `-a`/`all=true` is how another session's half-finished file gets swept in — the
  exact failure that produced this standard.

### Worktree (opt-in)

- Each worktree is a separate checkout on `worktree-<session>` off the chosen base.
- The session commits inside the worktree; the **owner merges the branch** when green. Worktrees are
  not shared, so there is no file-level interference at all.
- Setup is automated by [`.kilo/setup-script.ps1`](../../.kilo/setup-script.ps1).

---

## 3. The record — one file per session

Records live at `tasks/sessions/<session>.json` (tracked) and are committed with the session's first
change. **One file per session** — never a shared registry file — so two sessions starting at once
cannot conflict on the record itself. The "index" is the directory listing; it is read, never written
by two agents.

Template: [`tasks/sessions/_template.json`](../../tasks/sessions/_template.json).

Local runtime detail that churns (current task, next task, evidence pointer, pending command) lives
under `.kilo/sessions/<session>.json` (gitignored, machine-local). Keep the tracked record stable;
keep the local file honest. Neither is a substitute for the evidence ledger.

---

## 4. Starting a session (procedure)

Run `/session-start`, or do it by hand:

1. **Read** every `tasks/sessions/*.json`, `git worktree list`, `git branch --show-current`, and
   `git status --porcelain`.
2. **Check for crossing**: does any *active* record's `paths` overlap the paths you are about to
   touch, or claim the same branch in `direct`/`worktree` mode? If yes, stop and resolve with the
   owner — either take a worktree, or narrow your scope.
3. **Ask the owner** the §2 questions (mode, branch, one problem, paths). Record the answers.
4. **Write** `tasks/sessions/<session>.json`.
5. **Then** begin work. Commit inside your boundary as you go.

Re-run `/session-start` only to change the boundary or close it. On close, set `status` and commit
the record (a new commit, never an amend).

---

## 5. Worktree setup

`.kilo/setup-script.ps1` runs once when Agent Manager creates a worktree. It copies `.env`, restores
Python deps for `tools/seedsmith`, runs `npm ci` for the web, and prints what still needs the owner
(`FUSIONRPG_GAME_DIR` is machine-local and never committed).

`scripts/session-boundary-check.ps1` validates the records and reports drift:

- a record whose `worktree` no longer exists, or whose `branch` is gone;
- two active records claiming overlapping `paths`;
- an orphan `worktree-*` branch that no record claims.

Run it at session start; a clean run is the precondition for editing.

---

## 6. Hard rules (restated)

- **Do not edit outside your `paths`.** Another session's dirty file is not yours; leave it.
- **Never** `git stash`/`checkout`/`reset` around another session's work, and never a bare
  `git checkout -- <path>` on a file your boundary does not own. Both destroy uncommitted work.
- **Never** `all=true` in `repo-git.commit` unless the owner says the whole tree is one session's.
- **One problem per session.** If a second problem appears, start a second session or hand back.
- **Commit via MCP only** ([agent-git.md](agent-git.md)); push is owner-only.
- A closed boundary is `status: merged` (or `abandoned`) in a new commit — not a deletion, so the
  history of who owned what survives.
