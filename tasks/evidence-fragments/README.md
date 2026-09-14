# Evidence fragments

Fan-out worktrees write one file per task here (`<task-id>.md`, e.g. `T10.md`) instead of editing
the shared ledger, so concurrent branches cannot conflict. At integration the rows are folded into
[tasks/actor-hub-and-combat-power-solid-fixing-evidence-map.md](../actor-hub-and-combat-power-solid-fixing-evidence-map.md)
and the fragment is marked consumed.

Only `/solid-run fanout` uses this directory. AUTO mode edits the ledger directly on the current
branch, so it writes no fragments.

Format (one row per acceptance criterion):

```
| Criterion | Command | Executed result | Artifact |
|---|---|---|---|
| T10.1 chip copy | npm test -- --run foldAptitudesSurfaceVm | exit=0 :: ... | — |
```

Rules: only real, executed results; `N/A` needs a reason; a documented-but-unfixed defect is `FAIL`.
