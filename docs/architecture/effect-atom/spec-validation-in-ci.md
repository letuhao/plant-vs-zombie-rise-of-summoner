# Spec: validation-in-ci (E24)

**Status: BUILT 2026-08-23, retrospective spec written 2026-09-03.** Module **E24** in the
[effect-atom map](../effect-atom-map.md) §3, Wave 6, Checkpoint F. This document records what shipped;
it is not a plan. Acceptance evidence: [tasks/effect-atom-todo.md](../../../tasks/effect-atom-todo.md)
(search `E24: validation-in-ci`). Scoped from [completeness-audit.md](completeness-audit.md)
findings B4 and B5.

> Reads [definitions.md](definitions.md), which wins where it and this document disagree.

## What it owns

Two things that make already-written checks run outside their own test files: the importer's
`--validate` flag, which drives E14b's `ContentValidation` over the batch it just imported and fails the
process on a finding, and the CI wiring for test projects that existed and ran nowhere — plus a standing
guard so the next unwired suite cannot ship quietly.

## What it closed

**B4 — `ContentValidation` ran only inside its own tests.** Lint, drift and budget were built, tested
and correct, and no tool or pipeline ever called them against the real shipped corpus. **B5 —
`FusionRpg.Server.Tests` and `FusionRpg.E2E.Tests` existed, passed locally, and were absent from
`ci.yml`.** That is the same mistake `tests/FusionRpg.AtomImporter.Tests` had already made once, which
is why the fix shipped with a general guard rather than two more literal lines.

## The contract as shipped

**The flag** — `tools/AtomImporter/Program.cs:24` parses `--validate`; `:126-137` is the block:

```csharp
var lint  = ContentValidation.Lint(collected.Content.Atoms, collected.Content.Containers);
var drift = ContentValidation.Drift(collected.Content.Atoms, store.GetPowerTables());
var decision = ValidationGate.Decide(lint, drift);
foreach (var line in decision.Lines) Console.WriteLine(line);
if (!decision.Ok) return 1;
```

These are the real production calls, not a re-derived check, and they run **after** the catalog has
accepted the import (`:120-124`) and **before** the summary line. Exit codes are the importer's own:
`0` clean, `1` refused, `2` could not start (`Program.cs:17`).

**The decision half** — `tools/AtomImporter/ValidationGate.cs:14-27`. `ValidationOutcome(bool Ok,
IReadOnlyList<string> Lines)`; `Decide` renders both reports, appends the budget line, and sets
`ok = lint.Ok && drift.Ok`, adding a `"--validate found a blocking finding"` line when it fails. It is a
class rather than top-level statements for exactly the reason `SeedScanner` is — so it has a test
independent of stdin, stdout and exit codes.

**Budget is deliberately not run**, and says so in its own output line (`ValidationGate.cs:20`):
`"budget: skipped — no ceiling data source exists yet (rarity table has no budget column)"`.
`ContentValidation.Budget` needs `ceilingFor(rarityId)`, and no ceiling column exists anywhere in the
schema, so every real call would evaluate nothing while looking clean. Printing the skip is the
audit's own "no silent green" principle applied to the gate itself.

**The CI wiring** — `.github/workflows/ci.yml:101-104` added `FusionRpg.Server.Tests` and
`FusionRpg.E2E.Tests` beside the existing seven. Neither needs game interop, so nothing else in the
workflow changed. Each `dotnet test` call now carries its own `$LASTEXITCODE` check
(`ci.yml:85-104`) — that per-call checking was **not** E24's work; it landed 2026-09-01 under the
`seed-to-concrete` program, and `ci.yml:78-84` records why.

**The standing guard** — `tests/FusionRpg.Guard.Tests/CiWiringGuardTests.cs`:
`Server_and_E2E_tests_are_wired_into_ci` (`:15-22`) pins the two literal project paths, and
`Every_test_project_under_tests_appears_somewhere_in_ci_yml` (`:24-50`) walks every `*.Tests.csproj`
under `tests/` (skipping `bin`/`obj` copies) and asserts its repo-relative path appears in the workflow
text.

## What it does NOT do

- **It does not run `--validate` in CI.** Despite the module id, no step in `.github/workflows/ci.yml`
  invokes `tools/AtomImporter` at all. See "Known residuals" — this is the load-bearing limitation.
- **It does not price anything.** Lint and drift are E14b's; E24 calls them and decides.
- **It does not check budgets** (above), and does not invent a ceiling source to make the check appear.
- **It does not gate the import.** A finding fails the process *after* the transaction has been decided
  by the catalog; `--validate` is a report gate, not a write gate. Pair it with `--check` to get both.
- **It adds no CI step for the other tools.** `ElementEnumGen` remains unrun as a CLI in CI
  ([spec-content-codegen.md](spec-content-codegen.md)).

## How it is verified today

- **Unit** — `tests/FusionRpg.AtomImporter.Tests/ValidationGateTests.cs`, 6 tests: two clean reports
  pass; a lint warning alone does not fail; a drift failure fails; a lint failure fails too; every pass
  prints its evaluated count so an empty pass cannot look thorough; a failing gate names the offender.
- **Seam** — `tests/FusionRpg.AtomImporter.Tests/ValidationGateSeamTests.cs`, 3 tests driving real
  `AtomRow`s through the real `ContentValidation.Lint`/`Drift`/`ValidationGate.Decide` chain — the exact
  calls `Program.cs` makes: real atoms with wildly wrong stored power fail the real gate; the same drift
  with a `PowerNote` passes, because a note is permission and not a fix; correctly priced atoms pass.
- **Guard** — `tests/FusionRpg.Guard.Tests/CiWiringGuardTests.cs`, 2 tests (above). This suite runs in
  CI, so the general form is genuinely standing.
- **Manual acceptance at build time**, recorded in the todo and not re-run for this document:
  `dotnet run --project tools/AtomImporter -- --check --validate` against the real `data/seed/**` exited
  `0`, printing `lint: 23 evaluated, 0 failure(s), 20 warning(s)` and `power drift: 0 evaluated`.

## Known residuals

- **`--validate` runs in CI nowhere.** Verified by reading `.github/workflows/ci.yml`: the only tool
  invocations are `DemonSpeciesGen --check` (`:50`), `DemonCorpusDump --verify` (`:130`) and
  `ItemSeedValidator` (`:136`). What E24 wired into CI is **B5** — the two missing test projects, plus
  the guard — while **B4**'s gate is reachable only from a hand-run command line and from its own unit
  and seam tests. The module's name overstates what shipped; one `ci.yml` step would close it.
- **Drift evaluates zero atoms against the real corpus.** Freshly parsed atoms carry no stored
  `power_json` until something backfills it, so `power drift: 0 evaluated` is the honest current state,
  not a bug — but a gate that evaluates nothing catches nothing, and the printed count is the only thing
  that makes that visible.
- **Lint reports 20 warnings on the shipped corpus** (orphan atoms: the migrated `fx-*` defs are not
  container-referenced). Expected and non-blocking by design, so the standing state of a clean run is
  "20 warnings", which erodes the signal value of a warning here.
- **Budget stays unimplementable** until the rarity schema grows a ceiling column.
