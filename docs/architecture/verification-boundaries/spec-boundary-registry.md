# Spec: verification-boundaries / boundary-registry

**Status:** draft. Module `boundary-registry` from
[the capability map](../verification-boundaries-map.md).

## Objective

Create the single machine-readable authority mapping a changed repository path to its smallest
correct verification requirements. It replaces scattered prose filters as the selection authority;
module specs remain the rationale for each entry.

## Registry location and shape

The tracked file is `scripts/verification-boundaries.v1.json`. It contains declarative values only:

```json
{
  "schemaVersion": 1,
  "projects": { "data": "tests/FusionRpg.Data.Tests/FusionRpg.Data.Tests.csproj" },
  "guards": { "dal": "scripts/guard-dal.ps1" },
  "boundaries": [
    {
      "id": "data-item-socket",
      "kind": "owner",
      "paths": ["src/FusionRpg.Data/Sqlite/RpgStore.Items.cs"],
      "tests": [{ "project": "data", "verificationIds": ["data.item-socket"], "level": "focused" }],
      "guards": ["dal", "test-substrate"],
      "rationale": "docs/architecture/species-gear-chain/spec-socket-combat-wiring.md"
    }
  ]
}
```

`owner` entries resolve by most-specific matching path; exactly one resolves for each production
path. `seam` entries are additive and may match alongside an owner. A broad migration fallback is
an owner of lower specificity, never an additive seam.

`testPaths` may map changed test code to a known group. If a changed test file carries a
`VerificationId`, the planner reads that declaration; a test-only file with no group is rejected
until the author declares one or explicitly identifies a test-support-only boundary.

## Commands

```powershell
.\scripts\guard-verification-boundaries.ps1
dotnet test tests\FusionRpg.Guard.Tests\FusionRpg.Guard.Tests.csproj --filter "FullyQualifiedName~VerificationBoundaryRegistry"
```

## Testing strategy

- Parse fixture registries for valid owner precedence, seam composition, normalized paths, known
  project/guard IDs, legal trait IDs, and source-root coverage.
- Prove a narrow owner overrides a broad migration fallback.
- Prove a changed path matching two seams unions their checks without duplicating any check.
- Prove malformed JSON, absolute paths, traversal, shell-like fields, unknown IDs, and zero-match
  selectors fail.
- Validate source-root coverage as a relation, never by asserting a source-file count.

## Boundaries

- Always: version the schema, normalize patterns to repository-relative `/`, and cite a rationale.
- Ask first: a schema version change, registry root expansion, or a broad fallback that changes a
  previously mapped path's verification level.
- Never: embed a command line, an absolute path, an environment-specific game path, a test-count
  target, or a silently ignored unknown field.

## Success criteria

1. Every in-scope production path matches one owner and zero or more seams.
2. Registry resolution is deterministic and does not depend on filesystem order.
3. The registry selects only declared project/guard/test-group IDs.
4. A specific mapping can narrow a legacy fallback without losing required seam guards.

