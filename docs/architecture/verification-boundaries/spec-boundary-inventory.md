# Spec: verification-boundaries / boundary-inventory

**Status:** draft. Module `boundary-inventory` from
[the capability map](../verification-boundaries-map.md).

## Objective

Produce a reproducible, read-only inventory that gives a maintainer evidence for authoring a
verification boundary. It answers where production and test code live, which test projects compile
which projects/tools, which positive test groups exist, and which existing docs name focused tests.
It does **not** infer that any candidate is required verification.

## Contract

`scripts/verification-boundary-inventory.ps1` scans repository-relative paths and writes a
deterministically ordered JSON document to stdout or an explicitly supplied output path. Each record
uses a stable key, not a count assertion:

```json
{
  "sourceRoots": [{ "path": "src/FusionRpg.Data", "project": "src/FusionRpg.Data/FusionRpg.Data.csproj" }],
  "testProjects": [{ "path": "tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj", "references": ["src/FusionRpg.Data/FusionRpg.Data.csproj"] }],
  "testGroups": [{ "id": "data.item-socket", "project": "tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj", "source": "tests/..." }],
  "guards": [{ "id": "dal", "script": "scripts/guard-dal.ps1" }],
  "focusedCommandEvidence": [{ "document": "docs/...", "selector": "FullyQualifiedName~ItemSocket" }]
}
```

The last field is migration evidence only. Registry entries require an explicit owner decision and
cannot be generated from it.

## Commands

```powershell
.\scripts\verification-boundary-inventory.ps1
.\scripts\verification-boundary-inventory.ps1 -Out .\artifacts\verification-boundary-inventory.json
dotnet test tests\FusionRpg.Guard.Tests\FusionRpg.Guard.Tests.csproj --filter "FullyQualifiedName~VerificationBoundaryInventory"
```

## Project structure

```text
scripts/verification-boundary-inventory.ps1           read-only inventory command
tests/FusionRpg.Guard.Tests/VerificationBoundaryInventoryTests.cs  contract tests
docs/architecture/verification-boundaries/            module specs
```

The output path is caller-owned. The command never writes a repository-tracked snapshot by default;
the inventory is a reading that changes as source/test populations grow.

## Code style

PowerShell must use repository-relative slash-normalized paths, `-LiteralPath` for filesystem
access, `ConvertFrom-Json` compatible with Windows PowerShell 5.1, and explicit sort keys:

```powershell
$records = @($records | Sort-Object path, project)
$records | ConvertTo-Json -Depth 8
```

## Testing strategy

- A fixture tree proves source roots, test projects, project references, traits, and guard discovery.
- A real-tree test asserts deterministic ordering and that every reported path is repository-relative.
- A test proves prose command extraction is labelled advisory and cannot become registry output.
- No test asserts a count of projects, files, groups, or commands.

## Boundaries

- Always: exclude `bin/`, `obj/`, `.git/`, worktrees, and generated build output.
- Ask first: adding a new inventory input class that changes the registry migration workflow.
- Never: write or update the registry, run tests, or execute a command discovered in source/docs.

## Success criteria

1. Identical trees yield byte-identical JSON regardless of filesystem enumeration order.
2. Every emitted path resolves under the supplied repository root.
3. The output distinguishes authoritative declarations from advisory historic command evidence.
4. The tool completes without creating a tracked file or invoking a test/guard.

