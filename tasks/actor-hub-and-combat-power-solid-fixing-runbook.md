# Runbook: actor-hub-and-combat-power-solid-fixing — execution + fan-out

**Program:** `actor-hub-and-combat-power-solid-fixing`
**Plan / todo:** [plan](actor-hub-and-combat-power-solid-fixing-plan.md) · [todo](actor-hub-and-combat-power-solid-fixing-todo.md)
**Evidence ledger:** [evidence map](actor-hub-and-combat-power-solid-fixing-evidence-map.md)
**Map / ideal:** [map](../docs/architecture/actor-hub-and-combat-power-solid-fixing-map.md) · [ideal](../docs/architecture/actor-hub-and-combat-power-solid-fixing-ideal.md)

This runbook is the operator manual for running the program with the `/solid-run` command. The
plan owns *what* to build; this file owns *how the work is sequenced and proven*.

---

## 1. Ground rules (unchanged from AGENTS.md)

- **Git is hands-off.** No agent runs `git commit`/`add`/`merge`/`push`. Worktrees are integrated by
  the owner (Apply or merge). Each wave checkpoint hands the owner a one-line message + paths.
- **No pre-work hard gate.** Waves start when the previous wave's evidence rows are executed.
- **The `RulesetVersion` bump is already approved.** The single bump (`= 4` → `= 5`) and golden
  re-bless are locked by the plan (T6); the run executes them without a fresh approval.
- **AUTO is the default path.** `/solid-run` runs all five waves on the current branch with no
  per-task stops; wave checkpoints verify and continue. `/solid-run fanout` opts into worktrees,
  which need the owner to merge between waves — that is the only mode that is not unattended.
- **Do not fork a second implementation** while a task is in fan-out. `unique-lawn-wire` stays in the
  aptitude-sheet stream; T12 only proves its Done criteria.

---

## 2. Pre-flight (once, on the integration branch)

```powershell
git branch --show-current          # expect: features/derived-stat-extension
git status --porcelain             # expect: clean (only planning artifacts untracked)
dotnet build                       # repo builds before any task
.\scripts\guard-actor-hub.ps1      # baseline guards
.\scripts\guard-single-writer.ps1
.\scripts\guard-funnel-delta.ps1
```

- Injector tasks (T13, T14) need `$env:FUSIONRPG_GAME_DIR` set and `.\scripts\prepare-injector-refs.ps1` run.
- FE tasks (T10, T11, T17) need `npm install` and `npm run test:e2e:install` under `web/fusion-rpg-web`.
- Confirm `git worktree list` shows no stale worktrees before creating new ones.

---

## 3. Wave execution map

`main` = the integration branch (`features/derived-stat-extension`). `WT-x` = an Agent Manager
worktree branched from `main`.

| Wave | Task | Where | Depends on | Why there |
|---|---|---|---|---|
| 1a | T1, T2 (`channelmods-hub`) | **WT-A** | — | one file seam (`WebMatchService`), serial inside |
| 1a | T3, T4 (`cold-equip-one`) | **WT-B** | — | independent of T1/T2, parallel |
| 1b | T5, T6, T7 (`battle-hub-fuse`, ops) | **main, serial** | WT-A + WT-B merged | fuse touches shared compose + goldens; one bump |
| 2 | T8, T9 | **main** | T6 | membership unlocks Standing; keep on main |
| 2 | T10 + T17 (chip + theta) | **WT-C** | T9 | FE fold + wire, one reviewer surface |
| 2 | T11 (copy) | **WT-D** | T9 | independent FE fold |
| 2 | T16 (coeffs) | **WT-E** | T9 | tuning JSON + CostFunction, independent |
| 3 | T12 (`lawn-aptitude-parity`) | **main** | T6 + unique-lawn-wire | coord/parity gate, no code fork |
| 3 | T13 (`lawn-tree-hydrate`) | **WT-F** | T6, T12 green | Injector |
| 3 | T14 (`bound-loadout-hub`) | **WT-G** | T12 | Injector loadout |
| 4 | T15 (`sim-hub-parity`) | **WT-H** | T6 | sim profiles |
| 4 | T18 (`stale-compose-docs`) | **main** | T6 | docs + code comments |
| 4 | T19 (`prove-hub-combat`) | **main** | T7, T9, T12, T14 | cross-cutting prove script |
| 5 | T20 → T21 → T22 → T23 | **main, serial** | T19 (T20 min T6) | deletions; each removes the prior stub |

**Start order after T6 lands:** open `WT-C ∥ WT-D ∥ WT-E` and `WT-F ∥ WT-G` and `WT-H` in the
waves above. Do not open a worktree for a task whose dependency has not landed on `main` yet —
worktree mode branches from the current `main`, not from a sibling worktree.

---

## 4. Launching fan-out sessions (optional — `/solid-run fanout`)

AUTO runs on the current branch; fan-out is the only mode that needs the owner. Use Agent Manager,
one task-group per worktree, each with the standard prompt:

```
/solid-run Tn
```

- Branch seed per group, e.g. `actorhub/w1-channelmods`, `actorhub/w2-chip-theta`,
  `actorhub/w3-tree`, `actorhub/w3-loadout`, `actorhub/w4-sim`.
- One session per worktree. Serial tasks in a group run in that one session; do not split a group
  across worktrees (they share files).
- Keep a wave's fan-out worktrees based on the same `main` tip. If a wave's own task lands on
  `main` mid-flight (e.g. T12), rebase/merge `main` into the dependent worktree before continuing
  (`WT-F`/`WT-G` after T12).

**Conflict seams to watch when integrating:** `WebMatchService.cs` (T1/T2), `BattleEngine.cs` +
`BattleModels` + goldens (T5/T6), `UniqueActorHubCompose` (T9/T16), docs (T18/T23), shared test
`.csproj` (adding files is safe; changing references is not).

---

## 5. Evidence across fan-out (no ledger merge conflicts)

The single ledger at `tasks/actor-hub-and-combat-power-solid-fixing-evidence-map.md` is owned by
`main`. Worktrees must **not** edit it concurrently.

- In a worktree, write rows to `tasks/evidence-fragments/<task-id>.md` (one fragment per task).
- At integration, the owner merges the worktree, then the next `main` session appends the fragment
  rows into the ledger and marks the fragment consumed (`<!-- folded -->`).
- An acceptance row counts only once its row is in the ledger with an **executed** result.

The fragment file format is the ledger row format:

```
| Criterion | Command | Executed result | Artifact |
|---|---|---|---|
| T1.1 Star/Loyalty via Hub | .\scripts\guard-actor-hub.ps1 | exit=0 :: ... | — |
```

---

## 6. Wave checkpoints

At each `## Checkpoint` in the todo, on the current branch:

1. Fold all fragments for the wave into the ledger; confirm every row is `PASS` or justified `N/A`.
2. Run the wave's guards + focused suites once on the integrated tree (proves the merge, not just
   each worktree).
3. In AUTO, continue straight into the next wave. In `/solid-run fanout`, hand the owner the
   one-line commit message (imperative, ~72 chars), the changed paths, and the quick test command so
   they can merge the wave's worktrees before the next wave opens.

---

## 7. Definition of program done

The todo's **Program Done when** list plus the map's **Done when** list, each backed by an executed
ledger row. `prove-hub-combat` green is the program's own end-to-end proof; the T6 golden re-bless
is executed under the plan's already-approved `RulesetVersion` bump. `world-actor-combat` is tracked
only — no Done claim here.
