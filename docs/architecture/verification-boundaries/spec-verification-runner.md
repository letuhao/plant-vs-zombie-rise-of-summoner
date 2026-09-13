# Spec: verification-boundaries / verification-runner

**Status:** draft. Module `verification-runner` from
[the capability map](../verification-boundaries-map.md).

## Objective

Execute only a successful plan's selected checks, in a predictable order, and leave an agent with
compact evidence that can be reported or committed. The runner is the canonical local workflow;
agents do not assemble an ad-hoc broad `dotnet test` command.

## Interface

```powershell
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs -Session <active-session>
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs -Format json
```

The command first produces the planner's data. It then runs, in order: source-only guards,
focused tests, required module tests, required seam tests, and explicitly required-on-change
disk/heavy tests. It stops on the first non-zero command and returns its plan plus completed evidence.

The JSON registry names logical project/guard/tool IDs only. The runner maps them to fixed,
repository-relative commands. It may not expand or invoke arbitrary command text from the registry.
Before planning or execution, it invokes the static integrity guard in an isolated PowerShell process;
a stale selector cannot become a successful zero-test filtered invocation.

A selected failure is terminal for that run. The runner reports the failed check and retains the
plan/evidence already collected, but never retries by widening from focused to module or full. That
widening is a registry/owner decision, not failure recovery.

For a selected test project the runner invokes `dotnet test` without `--no-build` after a source
change. Subsequent checks in the same invocation may use `--no-build` only after the runner records a
successful compatible build. This preserves the existing hang guard from `Directory.Build.props` and
avoids the documented stale-assembly trap.

## Profile behavior

Broad module checks use the existing local default exclusion filter. A selected focused or seam test
marked `requiredOnChange` runs even if it carries `DiskSemantics` or `Heavy`; this is a narrow direct
proof, not permission to run all excluded tests. Full evidence is never selected by this command
unless an explicit owner-only full mode is added later.

## Evidence output

Each result contains: check ID, level, reason, command-safe display, start/end UTC, elapsed duration,
exit code, and result (`passed`, `failed`, `not-run`). The default output is human-readable; `-Format
json` writes the same data to stdout. No tracked report is written by default.

## Project structure

```text
scripts/verify-change.ps1                          planner + runner façade
scripts/verification-boundaries.v1.json             declarative input
tests/FusionRpg.Guard.Tests/VerificationRunnerTests.cs
docs/contributing/testing-standard.md                profile policy updated by workflow-adoption
```

## Testing strategy

- Fixture-runner tests replace allowed commands with harmless test fixtures and assert order,
  arguments, stop-on-failure, and JSON evidence shape.
- Real-tree tests exercise `-PlanOnly`; a focused real test invocation proves normal command wiring.
- Tests prove a required-on-change disk/heavy group bypasses only its own broad filter.
- Tests prove the runner never emits unfiltered full suite arguments through normal mode.

## Boundaries

- Always: use `-LiteralPath`, fixed command argument lists, non-zero propagation, and elapsed-time
  reporting.
- Ask first: adding an executable check type, CI invocation behavior, or an owner-only full mode.
- Never: run a no-argument `test-fast.ps1`, call `deploy-play.ps1`, use `--no-build` on the first
  selected project after source input, treat a successful build as test evidence, or use a broad
  suite as a fallback for a failed/missing focused selection.

## Success criteria

1. A mapped narrow change runs no unrelated project suite.
2. A failed guard/test stops later execution and identifies the exact failed check.
3. The runner's normal mode cannot construct an unfiltered full-suite command.
4. Evidence proves which checks ran and which full evidence remains CI-owned.
