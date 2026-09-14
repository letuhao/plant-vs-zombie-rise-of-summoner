# Spec: verification-boundaries / test-group-contract

**Status:** draft. Module `test-group-contract` from
[the capability map](../verification-boundaries-map.md).

## Objective

Give a coherent focused test family a stable identifier that a source-path boundary can select even
when test classes move or rename. Existing `Category=Heavy` and `Category=DiskSemantics` continue to
describe execution cost; they do not describe behavioral ownership.

## Contract

A focused test class or method is annotated with xUnit's existing trait API:

```csharp
[Trait("VerificationId", "data.item-socket")]
public sealed class ItemSocketStoreTests { }
```

`VerificationId` follows `^[a-z][a-z0-9-]*(\.[a-z][a-z0-9-]*)+$`: at least a module segment and a
behavior segment, lowercase ASCII, dot-separated. A class-level trait is preferred when all methods
prove the same boundary; method-level traits are used only for a genuinely separate behavior.

The same group may contain many tests. A group may be attached to more than one class. A test may
carry multiple IDs only when it intentionally proves multiple named boundaries; it is not a shortcut
for unrelated broad coverage.

The runner selects the group through the VSTest/xUnit trait filter. This module first proves the
exact filter syntax against the installed adapter before registry/runner implementation relies on it.

## Commands

```powershell
dotnet test tests\FusionRpg.Data.Tests\FusionRpg.Data.Tests.csproj --filter "VerificationId=data.item-socket"
dotnet test tests\FusionRpg.Guard.Tests\FusionRpg.Guard.Tests.csproj --filter "FullyQualifiedName~VerificationId"
```

## Project structure

```text
tests/FusionRpg.*.Tests/**                         existing xUnit test families receive traits
tests/FusionRpg.Guard.Tests/VerificationIdTests.cs convention and adapter-proof tests
docs/architecture/verification-boundaries/         contract documentation
```

No custom xUnit attribute package is introduced. The installed `xunit` and `xunit.runner.visualstudio`
packages already support the trait convention used by `Category` today.

## Testing strategy

- Prove an annotated fixture is selected by one `VerificationId` filter and an unannotated fixture is
  excluded.
- Prove a group can coexist with `Heavy`/`DiskSemantics` traits.
- Reject malformed IDs, missing IDs for registry-referenced tests, and group selectors that match
  zero tests through the integrity guard.
- Record selected-test names in the adapter spike; do not pin their count.

## Boundaries

- Always: put the trait beside the test it describes and use the behavior-facing ID.
- Ask first: changing the ID grammar or reassigning an existing ID to a different behavior.
- Never: use `Category` as an ownership alias, derive identity from file names, or tag an entire test
  project merely to make a registry entry easy.

## Success criteria

1. The installed adapter selects a known trait group with one stable filter expression.
2. Renaming its test class does not change the selected group.
3. An invalid ID or zero-match registry selector fails the integrity guard.
4. Existing profile trait behavior remains unchanged.

