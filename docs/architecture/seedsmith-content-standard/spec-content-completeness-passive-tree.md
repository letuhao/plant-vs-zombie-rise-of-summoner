# Seedsmith content-completeness — `passive-tree`

**Status:** Proposed 2026-09-08. Module 6 of 7 in
[seedsmith-content-standard-map.md](../seedsmith-content-standard-map.md). Depends on
`content-completeness-core` (built, Checkpoint 0 closed 2026-09-08).

Passive-tree already has the MOST mature ledger (`run_language_stage`) and REAL, committed
name/flavor content per node (`data/seed/passive-tree/nodes/ferocity.json`, confirmed: every one of
its 39 nodes carries a real `name` and `flavor` string). This module's own work is not generation —
it is closing the WIRING GAP that stops that already-generated content from ever reaching a player.

## 1. Objective

Carry a node's `name`/`flavor` — real content that already exists in the committed seed corpus —
through every layer between the seed file and the browser, with zero new generation. Confirmed,
file:line, the three places the chain currently drops it:

1. **`NodeRecord`** (`src/FusionRpg.Core/PassiveTree/Catalog/NodeRecord.cs:37-52`) has no
   `Name`/`Flavor` field at all.
2. **`PassiveTreeCatalogLoader.LoadNode`**
   (`src/FusionRpg.Core/PassiveTree/Catalog/PassiveTreeCatalogLoader.cs:134-238`) never reads a
   `name`/`flavor` property from the JSON it parses — it would silently ignore both even if
   `NodeRecord` had the fields.
3. **`tools/TreeBinder/PlanReader.cs`'s `ReadSeedOverrides`** (lines 164-198) — the function that
   already reads `nodes/<treeId>.json` (the REAL seed file with real `name`/`flavor`) — only
   extracts `affixIds`/`exclusionForm`/`excludeProps` from it. `name`/`flavor` are sitting right
   there in the same parsed document and are never read.

Consequence, confirmed live: `data/generated/passive-tree/ferocity.json` (the real, committed
binder output) has no `name`/`flavor` on any node — not because the content doesn't exist, but
because three separate reads in the chain each independently drop it. The web FE
(`TreeNodeSummary`, `web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) has no field for it either,
and `PathLattice.tsx:319`/`TraitDetail.tsx:133` render the raw node id as their own fallback.

## 2. The fix — additive, no new generation, no schema break

Every step below ADDS an optional field; none removes or renames anything. Confirmed against
`spec-tree-catalog.md`'s own R1-R6 migration rules: they govern id lifecycle/magnitude changes,
never field additions, and `PassiveTreeCatalogLoader.cs` parses with `TryGetProperty` throughout —
a tree missing `name`/`flavor` (any tree `tree-language` hasn't reached yet) loads exactly as
before, with both fields `null`. No `catalogVersion` bump needed.

1. `NodeRecord` gains `string? Name, string? Flavor` (nullable — a node with no generated content
   yet is a REAL, valid state, never fabricated).
2. `PassiveTreeCatalogLoader.LoadNode` reads `name`/`flavor` the same way it already reads
   `tagsJson` (`TryGetProperty` + null-safe), passes them into the `NodeRecord` constructor.
3. `BindInputNode` gains `string? Name = null, string? Flavor = null` — carried through UNUSED by
   the pricing path, the exact same pattern already established for `Branch`/`Tier`/`NodeKey`/
   `NodeClass`/`ExcludeProps` (see that record's own doc comment for why this shape is preferred
   over a second near-duplicate input type).
4. `PlanReader.ReadSeedOverrides` additionally reads `name`/`flavor` per node id from the same
   parsed `nodes/<treeId>.json` document it already opens; `ReadPlanNodesWithSeed`'s override merge
   adds `Name = seed.Name, Flavor = seed.Flavor` alongside the fields it already overrides.
5. `ReportWriter.Serialize`'s node-shape anonymous object adds `name = input.Name, flavor =
   input.Flavor` alongside the fields it already emits.
6. Server: `PassiveTreeDtos.cs`'s node-facing DTO and `PassiveTreeEndpoints.cs`'s projection add
   `Name`/`Flavor` passthrough from `NodeRecord`.
7. FE: `TreeNodeSummary` (`web/fusion-rpg-web/src/lib/bus/types.ts:516-521`) gains optional
   `name`/`flavor`; `PathLattice.tsx`/`TraitDetail.tsx` render them when present, falling back to
   the existing raw-id rendering when absent — never a blank cell, matching this program's own
   "never fabricate content" rule for the (currently 41 of 42 trees') still-ungenerated case.

## 3. Commands

```powershell
dotnet build src/FusionRpg.Core
dotnet test tests/FusionRpg.TreeBinder.Tests
dotnet test tests/FusionRpg.Core.Tests --filter FullyQualifiedName~PassiveTree
dotnet run --project tools/TreeBinder -- --tree ferocity   # real re-bind, real seed content
cd web/fusion-rpg-web && npx vitest run
```

## 4. Project structure

No new files — every change above is additive to an existing file already on this program's own
established chain (§2's numbered list is the complete file list).

## 5. Code style

Match each touched file's own existing convention exactly (already-established in this same
session's earlier H9 work): nullable reference fields for "may not exist yet" content, doc-comment
citations of the specific finding a change fixes (see `ReportWriter.cs`'s own existing 2026-09-07
doc comment for the pattern to extend, not replace).

## 6. Testing strategy

Real data, not synthetic: `data/seed/passive-tree/nodes/ferocity.json` already has real name/flavor
on 39 real nodes — round-trip ONE of them through the full chain (seed → `PlanReader` → `BindInputNode`
→ `ReportWriter` → generated catalog file → `PassiveTreeCatalogLoader.LoadNode` → `NodeRecord`) and
assert the exact string survives unchanged at each hop. A second node with NO seed-generated content
yet must round-trip with `Name`/`Flavor` both `null`, never a placeholder string.

## 7. Boundaries

- **Always:** keep every change additive (nullable fields, `TryGetProperty` reads); prove the real
  round-trip against `ferocity.json`, not a hand-typed fixture; keep the existing `--check`
  byte-identity guard and H9's own live-boot proof green after the change.
- **Ask first:** any change to the node id grammar or `catalogVersion` (this module needs neither).
- **Never:** fabricate a `name`/`flavor` for a node that has none in its seed file — render the
  existing id fallback instead, exactly as `PathLattice.tsx`/`TraitDetail.tsx` already do today.

## Open questions

None — the wiring gap and its fix are both fully traced above; this is confirming plumbing, not
new design (matches the todo's own Task 13 acceptance: "the ideal doc already did the hard
discovery work").
