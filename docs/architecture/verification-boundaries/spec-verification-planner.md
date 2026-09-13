# Spec: verification-boundaries / verification-planner

**Status:** draft. Module `verification-planner` from
[the capability map](../verification-boundaries-map.md).

## Objective

Resolve explicit changed paths into a deterministic, inspectable verification plan without running a
guard or a test. The planner is pure selection logic; keeping it separate from execution lets agents
see and challenge the exact required evidence before spending time.

## Interface

The canonical entry point is part of `scripts/verify-change.ps1`:

```powershell
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs -PlanOnly
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs -Session verification-boundaries-20260913-6f31 -PlanOnly -Format json
```

`-Paths` accepts one or more repository-relative existing files, not directories/globs. A deletion
uses `-DeletedPaths` with its former repository-relative file; it is resolved through the registry
without requiring it to exist. The planner canonicalizes separators, rejects absolute/traversal
paths, and deduplicates paths. It never calls `git diff` or `git status`.

`-Session` is required by default. It reads the named record under `tasks/sessions/`, requires
`active` status, and proves every supplied path matches that record's declared path patterns. Session
scope is an authorization fence; the record's broad patterns are not substituted for omitted paths.
`-AllowUnscoped` is an explicit maintainer-only override for read-only planning outside a session.

The result is structured data containing canonical input paths, selected owner IDs, selected seam
IDs, deduplicated check IDs, per-check reason/evidence level, profile treatment, CI-owned full
evidence, and unmapped paths. Text output is rendered from the same data.

## Resolution algorithm

1. Classify each input as production, test, test-support, documentation, generated data, or
   unsupported.
2. Resolve exactly one most-specific owner for production/generated paths; collect every matching
   additive seam.
3. Resolve test-only files from their declared `VerificationId` or explicit test-support owner. A
   test edit is not ignored: it selects the behavior it claims to prove.
4. Union checks across all paths by `(kind, project/guard/tool ID, selector, profile override)`.
5. Sort canonical paths, boundary IDs, and checks by ordinal repository-relative text.
6. If any production/test path is unsupported or unmapped, return a non-zero plan result and no
   executable fallback.

## Project structure

```text
scripts/verify-change.ps1                         public command and planner functions
scripts/verification-boundaries.v1.json            declarative input
tests/FusionRpg.Guard.Tests/VerificationPlannerTests.cs
tasks/sessions/*.json                              optional scope authorization input
```

## Testing strategy

- Fixture registries cover specific-vs-fallback owner resolution, seam union, deduplication,
  argument-order independence, and profile overrides.
- Session fixtures cover accepted scope, out-of-scope rejection, missing records, closed records, and
  path-traversal attempts.
- A real-tree test plans representative Data, Core, Server, and generated-data paths without running
  them; it asserts named contract fields, never number of entries.
- Snapshot tests compare canonical JSON, not human prose.

## Boundaries

- Always: return enough reason data for an agent to explain each selected check.
- Ask first: adding a new path classification or evidence level.
- Never: inspect a concurrent dirty-worktree state, broaden to a test project just because a focused
  selector is absent, or replace a failed plan with a full suite.

## Success criteria

1. The same explicit paths in any order yield byte-identical JSON.
2. A specific owner suppresses a broad fallback, while matching seams remain additive.
3. An out-of-session path and an unmapped source path fail before any test runs.
4. A direct disk/heavy proof is explicitly marked `requiredOnChange`; ordinary broad-profile
   exclusions remain visible in the plan.
