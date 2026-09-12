# Fan-out protocol: `data-test-substrate`

**Status:** binding for every fan-out wave in this program. Owner-authorized 2026-09-12.

This exists because the plan's remaining work has **three files that multiple agents would collide
on**, and fan-out is only safe once each has exactly one writer.

---

## The three collision points, and who owns each

| Shared thing | Why it collides | **Single writer** |
|---|---|---|
| `scripts/test-substrate-baseline.txt` | every migration batch deletes its own lines from **one** file; Items' 21 lines are contiguous (index 33–53), so two Items sub-batches conflict at the boundary | **the gatekeeper session only** |
| `AGENTS.md` / `CLAUDE.md` | gitignored; a worktree lacks them and 36 tests use `AGENTS.md` as their repo-root marker | `.kilo/setup-script.ps1` copies them (patched 2026-09-12); never hand-edited per wave |
| `.github/workflows/ci.yml` | T19b and T28 both add a step | **one wave-1 agent** (the leak alarm), then T28 appends afterwards |

`test.runsettings` / `Directory.Build.props` are **not** touched by this program (they belong to the
merged `test-hang-guard` session); the profile filter lives on the `dotnet test` command line.

---

## The rule: subagents propose, the gatekeeper commits

A fan-out worker (a `task` subagent, or an Agent Manager session) **must not**:

1. **edit `scripts/test-substrate-baseline.txt`** — it reports the list of files it migrated and the
   gatekeeper deletes those lines, once, in order;
2. **commit anything** — the gatekeeper gates and commits via `repo-git.commit`;
3. **touch another wave's files** — each task's `Files:` list is the fence; the shared-file rule above
   is the only exception and it is the gatekeeper's.

A fan-out worker **must**:

1. follow the migration recipe exactly (construction/teardown only, the read-only conversion, no
   assertion change);
2. run its **focused** test filter and `guard-test-substrate.ps1` (read-only — the gate is *detection*,
   not an edit; it fails loudly if the worker left a violation);
3. report: the files changed, the focused pass count, the per-file `Assert.` delta vs its start
   revision, and any test that needed more than the recipe (a mis-classification finding).

## Why the gate stays with the gatekeeper

The gate is what makes fan-out *safe*, and it is also the thing a worker cannot do for itself: a
worker grading its own output is not independent. So the gate runs **once per task in the gatekeeper**,
on the merged working tree, after the worker reports — the same fresh-context `general` subagent the
single-agent loop already uses, with the same PASS/FAIL contract and the same scope check.

## Worktrees: required before any Worktree-mode wave

Fan-out agents that run in a **worktree** need the setup-script fix (it now copies `AGENTS.md` +
`CLAUDE.md`). Until that patch is on the branch a worktree branches from, run fan-out agents as
**local** sessions in the main tree — they inherit both files — but then they share the working tree,
so the single-writer rules above become load-bearing rather than optional.

## Wave shape (owner: "clean all parallel blocks first, then fan out everything")

1. **Wave 0 (this commit):** clear the blockers — setup-script copies the instruction files; the
   single-writer rule recorded here; the 124.6s test already fixed.
2. **Wave 1 (parallel):** the disjoint, cheap-to-gate tasks — T26/T27 (tags), T19b (leak alarm, owns
   the `ci.yml` edit), T28 (profiles; appends after T19b).
3. **Wave 2 (bounded parallelism):** migration batches — **at most one migration agent at a time**
   (the baseline is serial), which may run beside `src/`-only work (T20a archive abstraction) since
   those files are disjoint.
4. **Wave 3 (serial):** T29 checkpoint, T23/T24 final gate — on a quiet machine, because each runs
   the full suite and this box already carries other agent streams.

**Honest limit, stated up front:** the migration is bottlenecked by the shared baseline file and a
~6-minute full-suite gate per task. Fan-out realistically buys ~2x, and most of that is Wave 1. It is
not a 5x.
