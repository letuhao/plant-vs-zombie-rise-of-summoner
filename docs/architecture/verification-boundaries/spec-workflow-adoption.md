# Spec: verification-boundaries / workflow-adoption

**Status:** draft. Module `workflow-adoption` from
[the capability map](../verification-boundaries-map.md).

## Objective

Make path-owned verification the normal agent behavior rather than a documented option. The workflow
must stop accidental broad local testing while retaining the existing unfiltered CI/nightly/release
bar and the live deployment safety checks that are relevant to a change.

## Local workflow contract

For any implementation task, an agent supplies its changed paths to `verify-change` and reports the
resulting evidence. It does not run raw unfiltered `dotnet test`, no-argument `test-fast.ps1`, or
`deploy-play.ps1` merely to decide whether a narrow change is correct.

The agent does not broaden after a failure. A missing path mapping, selector with zero tests, or
out-of-session path is a fast boundary defect. A failed selected test is diagnosed against its
selected behavior. Either case may lead to a registry/spec correction, but neither authorizes a
full-suite fallback.

`test-fast.ps1` changes to require an explicit `-Project` for routine use. An intentional broad local
run requires `-AllDefault`; both the invocation and output say it is broad validation. This preserves
the existing category filter in one place but removes the silent four-project default.

`deploy-play.ps1` receives explicit `-VerificationPaths` and delegates to `verify-change` before a
live deployment. A caller that deliberately wants the historical broad local profile passes an
explicit `-AllDefaultTests`; no deployment mode silently chooses it.

## CI and nightly contract

CI keeps direct unfiltered `dotnet test` calls. It remains the owner of ordinary `Heavy`,
`DiskSemantics`, full project, E2E, and release evidence. CI additionally runs
`guard-verification-boundaries.ps1`; the new guard does not filter, replace, or weaken the existing
CI test calls.

The local runner may run one selected disk/heavy proof marked `requiredOnChange`. This is distinct
from changing CI ownership of the whole excluded category.

## Commands

```powershell
.\scripts\verify-change.ps1 -Paths src/FusionRpg.Data/Sqlite/RpgStore.Items.cs
.\scripts\test-fast.ps1 -Project tests/FusionRpg.Data.Tests
.\scripts\test-fast.ps1 -AllDefault
.\scripts\deploy-play.ps1 -VerificationPaths src/FusionRpg.Injector/Stats/EntityStatWriter.cs
```

## Project structure

```text
AGENTS.md                                           contributor rule and reporting expectation
docs/contributing/testing-standard.md               profiles + evidence-level policy
docs/contributing/dev-setup.md                      human-facing command examples
scripts/test-fast.ps1                               explicit broad opt-in
scripts/deploy-play.ps1                             supplied-path verification integration
.github/workflows/ci.yml                            static guard; full suite unchanged
tests/FusionRpg.Guard.Tests/VerificationWorkflowTests.cs
```

## Testing strategy

- Script-level tests prove no-argument `test-fast.ps1` refuses with an explicit message and
  `-AllDefault` is required to select all historical default projects.
- Tests prove deploy forwards supplied verification paths and never implicitly selects broad tests.
- CI-configuration tests prove existing full `dotnet test` calls remain unfiltered and the new static
  guard is independently checked.
- Documentation/agent-rule tests assert the canonical command is named; behavioral tests remain the
  runner/guard modules' responsibility.

## Boundaries

- Always: keep full CI/nightly test calls unfiltered and preserve the static substrate guard.
- Ask first: changing release workflow, removing a CI test project, changing deployment's build/game
  behavior, or granting a local full-suite exception beyond an explicit owner request.
- Never: encode a full-suite fallback for unmapped paths, silently reroute `deploy-play` to all tests,
  or claim local focused evidence is a CI replacement.

## Success criteria

1. An agent following repository instructions has one canonical local verification command.
2. Broad local verification is visible and opt-in at every entry point.
3. CI's current unfiltered test contract is preserved and guarded against accidental filtering.
4. A live deploy can consume exact verification paths without importing another session's dirty diff.
