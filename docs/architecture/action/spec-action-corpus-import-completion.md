# Spec: action-corpus-import-completion (A29)

Module **A29** in the [action map](../action-map.md) §17. Reads
[action-playability-ideal.md](../action-playability-ideal.md) gap #5 and Open Question Q1. Depends on
nothing — parallel-safe with A26/A27/A31.

## Objective

`Program.cs:402` imports only `committed-round-1.json`+`committed-round-2.json` (24 rows). The
action-corpus program's own audit (`docs/architecture/action-corpus/audit-2026-09-13-distribution.md`)
accepted **180** rows across four files — `committed-round-909.json` (102) and
`committed-round-2000.json` (53) are never read by any production code. This module closes that gap —
**after** confirming the importer actually handles their schema.

## Design

**Step 1 — schema-compat check, before any code change.** The two newer files were produced by a later
generator round (post the distribution-gaps audit: `affixClass` split on pool rows, `ALGORITHM_VERSION`
bump). Run `ActionCorpusImporter.Import` (`ActionCorpusImporter.cs:26-78`) against
`committed-round-909.json` and `committed-round-2000.json` in isolation (a scratch script or a new,
throwaway test) and read the result:

- If it imports cleanly → **Step 2a**: extend `Program.cs:402`'s literal array to all four filenames.
  One-line change.
- If it throws or silently drops fields → **Step 2b**: extend `ActionCorpusBriefJson.Parse`/
  `ActionCorpusImporter.Import` to handle the newer shape, **then** extend the file list. This is
  production import-logic work, not a config edit — flag it (see Boundaries) rather than silently
  broadening scope.

**Step 2a/2b, either way:** the corpus rows themselves (`data/seed/actions/committed-round-*.json`) are
**never hand-edited** — generated data, this repo's hard rule. Only the importer/`Program.cs` change.

**⚠️ Cross-file id collision — a proven historical bug class in this exact program, add a real guard
before enabling all four files.** The action-corpus program's own audit (`audit-2026-09-13-distribution.md`
§7, finding J) already found the SAME id present in two different round files with **different
payloads** (11 ids, `_rounds/round-1/survivors.json` vs `committed-round-2000.json`), silently
double-counted until a fresh-context gate caught it. `ActionCorpusImporter.Import`'s upsert is idempotent
**by id**, which is safe for the *same* id/payload appearing twice, but **silently last-write-wins** for
the same id with *different* payloads across files — exactly the shape of the proven defect. This module
must add an explicit check: if the same `action_id` appears in two of the four imported files with a
different payload, **reject the import and name the id + both source files**, rather than silently
picking whichever file the literal array lists last. This is a defensive check at the consumer
(importer), matching this repo's own precedent (§7's fix landed in the *consumer*, `_build_ctx`, "which
is where a violation does damage") — it does not fix the corpus generator itself, which is a separate,
already-tracked open item (`audit-2026-09-13-distribution.md` Open 4).

## Tunables

None. This module imports already-authored, already-priced content; it introduces no new balance
number.

## Commands

```powershell
# Step 1, schema-compat check (adjust path to the newer files):
dotnet run --project tools/ProveHubCombat -- --import-check data/seed/actions/committed-round-909.json
dotnet test tests\FusionRpg.Data.Tests --filter "FullyQualifiedName~ActionCorpusImporter"
```

## Project Structure

```
src/FusionRpg.Server/Program.cs                              (file-list literal, extended)
src/FusionRpg.Data/Sqlite/ActionCorpusImporter.cs             (only touched if Step 2b applies)
tests/FusionRpg.Data.Tests/Actions/ActionCorpusImporterTests.cs   (new cases for the two newer files)
```

## Code Style

If Step 2a: a pure data change (the file-list array). If Step 2b: match `ActionCorpusImporter.cs`'s
existing parse/validate/upsert shape — no parallel importer path for the newer schema.

## Testing Strategy

| Case | Expect |
|---|---|
| Import all four committed-round files via the real `Program.cs` startup path | real `rpg_action` row count matches the corpus audit's own reconciled total (179 committed + any genuinely-new, per the audit's own de-dup rule — **read the count, never assert a literal**, per this repo's guardrail-vs-population rule: this is a population, not a closed vocabulary) |
| Re-import (idempotency) | zero duplicate rows, matching A21's existing idempotent-upsert guarantee |
| A row already present in `committed-round-1/2.json` also appears (by id) in `committed-round-909/2000.json`, **identical payload** | no duplicate — one row, idempotent upsert |
| The same id appears in two files with **different** payloads (planted fixture, mirroring the real historical defect) | import **rejects loudly**, naming the id and both source files — never a silent last-write-wins |
| `committed-round-909/2000.json`'s newer fields (`affixClass`, etc.) | either already handled (Step 1 passes clean) or explicitly extended (Step 2b), never silently dropped |

## Boundaries

**Always:** run the schema-compat check (Step 1) before writing any code — do not assume the importer
handles the newer files just because they're "the same shape family."

**Ask first:** if Step 1 finds the importer needs real changes (Step 2b) rather than a one-line file-list
edit — that's a larger, different-shaped task than this spec sized for, and touches production import
logic other consumers may depend on.

**Never:** hand-edit `data/seed/actions/committed-round-*.json` to make an import succeed — fix the
importer/generator, never the generated row (this repo's hard rule, and these rows carry
`_provenance`).

## Success Criteria

1. All four committed-round files are read by the real host, or Step 2b's scope is named explicitly and
   the schema gap fixed first.
2. Real `rpg_action` count matches the corpus's own reconciled total, read not asserted.
3. Import stays idempotent.
