# Implementation plan: verification boundaries

## Overview

Build a path-owned verification system for automated contributors. A declarative registry selects
focused/module/seam tests and architecture guards from explicit changed paths; a PowerShell command
plans and runs those checks; CI remains the independent unfiltered full-suite owner.

Specs: [capability map](../docs/architecture/verification-boundaries-map.md) ·
[module specs](../docs/architecture/verification-boundaries/).

## Architecture decisions

- `scripts/verification-boundaries.v1.json` is declarative and versioned; no registry value is
  executable PowerShell.
- `scripts/verify-change.ps1` is the sole public planner/runner command. Shared pure helpers live in
  `scripts/lib/VerificationBoundaries.ps1`; this is reuse within one tool, not a second authority.
- Owners resolve exclusively by specificity; additive seams union afterward. Broad migration owners
  are fallback only.
- A trait's `VerificationId` selects a behavior family. `Heavy`/`DiskSemantics` remain cost/profile
  labels. A selected direct proof may opt into its own disk/heavy test.
- Guard.Tests is the validation host for PowerShell tooling. It follows existing external-process
  patterns and uses fixture roots, not mutations of real source/test trees.
- The normal local runner cannot invoke an unfiltered suite. CI/test profiles retain the existing
  unfiltered calls and add only the static integrity guard.

## Dependency graph

```text
T1 inventory ──────────────┐
T2 VerificationId contract ├─ T3 registry/parser ─ T4 planner ─ T5 runner ─ T6 integrity guard ─ T7 workflow
                           │                          │              │              │
                           └──────────────────────────┴──────────────┴──────────────┘
```

## Task list

### Phase 1 — discoverable contracts

- [ ] T1: Add the read-only inventory command and its fixture/real-tree contract tests.
- [ ] T2: Prove the installed xUnit adapter's `VerificationId` filter; define ID grammar and seed the
  first focused groups needed for the registry fixture.
- [ ] T3: Add registry v1, shared parser/path helpers, and a minimal complete top-level owner map
  with narrow seed overrides and explicit migration fallbacks.

### Checkpoint — registry foundation

- [ ] The inventory is deterministic and executes no discovered command.
- [ ] A known `VerificationId` is selectable, and malformed/zero-match groups are rejected.
- [ ] Every current in-scope root has exactly one owner fallback or a narrower owner.

### Phase 2 — plan and execute one vertical path

- [ ] T4: Implement `verify-change -PlanOnly` with explicit paths, owner/seam resolution,
  session-scope authorization, deterministic JSON, and fast unmapped rejection.
- [ ] T5: Implement normal `verify-change` execution for one selected focused group plus applicable
  static guards, with evidence reporting and stale-build protection.

### Checkpoint — agent value path

- [ ] A known Data path produces a plan and runs only its focused group/guards.
- [ ] Reordering the same paths does not alter JSON or execution order.
- [ ] An unmapped/out-of-session path runs no test command.

### Phase 3 — enforce and adopt

- [ ] T6: Add the static registry integrity guard and CI wiring without changing CI's unfiltered
  test profile.
- [ ] T7: Make broad local tests explicit opt-in, integrate supplied paths into deploy workflow, and
  update contributor instructions/standard.

### Checkpoint — complete

- [ ] All program acceptance criteria in capability map §9 are demonstrated.
- [ ] CI remains unfiltered; normal local agent workflow is path-scoped.
- [ ] No unrelated session file is staged or changed.

## Risks and mitigations

| Risk | Impact | Mitigation |
|---|---|---|
| VSTest trait filter has adapter-specific syntax | Planner cannot select groups | T2 is an early executable spike; registry is not implemented against an assumed filter. |
| Legacy source is not yet granularly mapped | A narrow edit falls to a module-level test | Record explicit fallback rationale; add focused boundary on first touch; never hide it with full-suite fallback. |
| Registry becomes an executable config surface | Arbitrary command execution | Fixed ID allowlists and runner-owned command construction; reject unknown fields. |
| In-memory Data test contention makes a mapped module fallback slow | Agents repeat expensive verification | T5 selects focused groups first; broad fallback remains exceptional. CI sharding remains a separate performance improvement. |
| Shared worktree diff includes another session | Wrong test plan / wasted work | `-Paths` is mandatory; optional `-Session` only validates supplied paths. |
| Deployment change accidentally weakens live safeguards | Bad live binary reaches game | T7 preserves existing build/guard behavior and changes only how test paths are selected. |

## Not doing

- No source-dependency graph inference as an authority.
- No full historical test taxonomy before the command delivers value.
- No removal of full CI/nightly, `Heavy`, `DiskSemantics`, leak alarm, or architecture guards.
- No generated population-count threshold for source/test coverage.

