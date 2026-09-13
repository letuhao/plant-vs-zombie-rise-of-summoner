# Spec: verification-boundaries / verification-integrity-guard

**Status:** draft. Module `verification-integrity-guard` from
[the capability map](../verification-boundaries-map.md).

## Objective

Keep the verification registry trustworthy as source roots and tests evolve. A stale selector,
unowned source root, unknown guard, or ambiguous owner must fail at review/CI rather than silently
return a plausible but incomplete local plan.

## Interface

```powershell
.\scripts\guard-verification-boundaries.ps1
dotnet test tests\FusionRpg.Guard.Tests\FusionRpg.Guard.Tests.csproj --filter "FullyQualifiedName~VerificationBoundary"
```

The guard is static/read-only. It reads the registry, source/test tree, and known script/project
allowlists. It does not run the selected behavioral tests and does not report a population-count
expectation.

## Required checks

1. Schema version, required fields, known-field rejection, normalized relative paths, and ID grammar.
2. Every in-scope production path resolves to exactly one owner after precedence; test-support and
   documentation roots are explicitly classified.
3. Owner ties at the same specificity fail; seams are additive only and cannot replace an owner.
4. Every referenced project, guard, generator validator, and verification ID is allowlisted and
   exists.
5. Every referenced verification selector matches at least one current test under its declared
   project; every changed annotated test is either reachable through the registry or intentionally
   marked test-support.
6. A migration fallback has an owner/rationale and can only shrink as a specific boundary lands.
7. Normal runner mode retains no path to an unfiltered full suite.

## Project structure

```text
scripts/guard-verification-boundaries.ps1
scripts/verification-boundaries.v1.json
tests/FusionRpg.Guard.Tests/VerificationBoundaryGuardTests.cs
.github/workflows/ci.yml                            invokes static guard
```

## Testing strategy

- Plant fixture registries for every failed contract shape and assert error codes, not prose.
- Run the guard on the real tree and assert its success only after the registry covers current roots.
- Guard tests create fixture trees outside the live `src/`/`tests/` roots; they never mutate the
  shared real registry.
- The guard validates relationship/closure, not counts of source files, test groups, or migrations.

## Boundaries

- Always: fail closed with a path and an actionable rule ID.
- Ask first: widening the in-scope root set or granting a permanent migration exception.
- Never: maintain a silent allowlist, accept unknown JSON fields, or weaken the guard to make a
  growing test population pass.

## Success criteria

1. A newly added production source path without an owner makes the guard fail.
2. A deleted/renamed selected test group makes the guard fail.
3. A registry pattern collision cannot choose a nondeterministic owner.
4. The guard itself completes without executing an application test suite.

