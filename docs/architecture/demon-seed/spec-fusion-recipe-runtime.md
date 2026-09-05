# Spec: `fusion-recipe-runtime`

**Module id:** `fusion-recipe-runtime` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 18 of 18 (new)
**Model calls:** none. **Depends on:** `fusion-recipe-generator` (17).

## Objective

Give `DemonRecipeCatalog` the same store/file-backed seam `catalog-runtime` (module 13) already built
for `DemonSpeciesCatalog`, so the committed `_fusion-recipes.json` seed — not an in-process
recomputation — is what the running game reads. Retires `DemonRecipeCatalog.Build()`'s live algorithm
as a *player-facing* source of truth; it keeps exactly one job, as the deterministic engine
`fusion-recipe-generator` §3 calls during content generation.

## Design

### 1. Which seam, and why not `catalog-runtime`'s own

`DemonSpeciesCatalog` reads a **database table** (`species-import` writes ~829 rows with real per-field
stat data, imported once per deploy). A recipe is four strings and a bool — `SpeciesBuildPlanCatalog`'s
own shape (a single small JSON file, `Configure`d directly from disk, no SQLite table) is the closer
precedent, not `DemonSpeciesCatalog`'s heavier DB-backed one. Reuse that pattern:

```csharp
public static class DemonRecipeCatalog
{
    static IReadOnlyDictionary<string, DemonRecipeDef>? _configured;
    public static void Configure(IReadOnlyDictionary<string, DemonRecipeDef> recipes) => ...
    public static bool IsConfigured => _configured != null;
    // All/Get/IsKnown/TryMatch keep their existing signatures — every one of spec-catalog-runtime.md's
    // own nine-consumer lessons applies here too: change the source, not the nine call sites.
}
```

`Build()` stays, renamed to make its new, narrower role explicit (`BuildDeterministicOnly()` or
similar) — it is no longer `All`'s implementation, only `fusion-recipe-generator` §3's own dependency.

### 2. What happens when the seed is missing or stale

Same rule `spec-catalog-runtime.md` §4 already established for the species catalog: **failing loudly at
load beats failing later.** `Configure` throws, naming `fusion-recipe-generator`'s own reconcile command,
when the file is absent or fails to parse — never a silent fall-through to
`BuildDeterministicOnly()`'s old algorithm, which is exactly the code path this module exists to stop
being load-bearing for players.

### 3. Order of operations (mirrors `spec-catalog-runtime.md` §7 exactly)

1. Add `Configure`/`IsConfigured` behind the existing `All`/`Get`/`IsKnown`/`TryMatch` API — no call site
   changes (`Server/DemonEndpoints.cs`, `RpgStore.Fusion.cs`, and every other real consumer of
   `DemonRecipeCatalog` keep their existing code, per `spec-catalog-runtime.md`'s own "nine call sites"
   lesson, applied here to this catalog's own smaller consumer set).
2. Both live hosts (`Server/Program.cs`, and the injector **only if a real consumer is found there** —
   verify by grep first, the same check that found the injector has no local species store either)
   load the committed file and call `Configure`.
3. A diff test: the store-backed recipe set, for every output NOT flagged `crossRungGapFill`, matches
   `DemonRecipeCatalog.BuildDeterministicOnly()`'s own live computation exactly. This is
   `spec-catalog-runtime.md` §6's own "prove the migration is correct" step, scoped to this catalog.

   **⛔ This proof is only valid if both sides read the identical species content — pin that
   explicitly, do not assume it.** `fusion-recipe-generator` (module 17) computed
   `BuildDeterministicOnly()`'s comparison basis from the COMMITTED `data/generated/demons/*.json`
   tree (its own §1). `DemonSpeciesCatalog.All` at diff-test time, per `catalog-runtime` (module 13),
   is normally DB-backed via `species-import` — a real database that can legitimately drift from the
   committed tree between an import and the next one. If the diff test calls `BuildDeterministicOnly()`
   against whatever `DemonSpeciesCatalog.All` happens to hold at THAT moment, a non-gap-fill recipe can
   show a spurious difference for a reason that has nothing to do with reconciliation correctness — a
   false failure that erodes exactly the trust this proof exists to build. The diff test therefore
   configures `DemonSpeciesCatalog` (via `UseScoped`, the same test-isolation seam `catalog-runtime`
   already built) from the SAME committed `data/generated/demons/*.json` tree the seed being tested was
   generated against, never from the live database, for the duration of the comparison.
4. Flip `Program.cs` from computing `DemonRecipeCatalog.All` live to `Configure`-ing it from the
   committed file, alongside the species catalog's own already-flipped `Configure` call.
5. Run the full suite; the diff test from step 3 is the gate.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter DemonRecipeCatalog
dotnet test tests/FusionRpg.Server.Tests --filter Fusion
.\scripts\guard-dal.ps1
.\scripts\deploy-play.ps1 -NoServer     # then a real lawn run: at least one real fusion execute
```

**A live check is required**, matching `spec-catalog-runtime.md`'s own binding rule for the sibling
catalog — a recipe silently missing for a real species is a defect a unit suite alone will not surface
if the fixture never included that species.

## Project structure

```text
src/FusionRpg.Core/Demons/Fusion/DemonRecipeCatalog.cs   API unchanged; source swapped; Build renamed
src/FusionRpg.Server/Program.cs                          gains the Configure call, beside the species one
tests/FusionRpg.Core.Tests/Demons/Fusion/DemonRecipeCatalogTests.cs   retargeted + the diff test
```

## Code style

```csharp
// Same discipline DemonSpeciesCatalog.cs already states for itself: the API is unchanged on
// purpose, so swapping the private source is one change, not N.
```

## Testing strategy

| Test | Asserts |
|---|---|
| `store_backed_recipes_match_deterministic_build_for_every_non_gap_fill_output` | the migration proof |
| `the_diff_test_scopes_DemonSpeciesCatalog_to_the_committed_tree_not_the_live_database` | pins §3 step 3's own precondition — a real, distinct check from the diff itself passing |
| `a_missing_seed_file_refuses_at_load_naming_the_reconcile_command` | the loud-failure rule |
| `crossRungGapFill_recipes_are_reachable_through_the_same_All_Get_TryMatch_api` | no second API surface for gap-fills |
| `every_real_consumer_of_DemonRecipeCatalog_works_unchanged` | one test per real call site, matching `spec-catalog-runtime.md`'s own per-consumer discipline |

## Boundaries

**Always:** keep the public API; fail loudly on a missing/stale seed; scope the diff test's
`DemonSpeciesCatalog` to the exact committed tree the seed was generated against, never the live
database; prove the diff before flipping; run the live check before calling this done.

**Ask first:** changing any real consumer of `DemonRecipeCatalog` (the design says none should need to).

**Never:** let `BuildDeterministicOnly()` become player-facing again after the flip; ship a fusion
recipe that was never validated by `fusion-recipe-generator` §3.

## Success criteria

- [ ] `Program.cs` loads recipes from the committed seed, not live computation.
- [ ] The diff test passes for every non-gap-fill output.
- [ ] A real fusion executes successfully against the store-backed catalog on a live lawn run.
- [ ] `DemonRecipeCatalog.Build()`'s public surface is gone; only the generator's own CLI calls the
      renamed deterministic-engine method.
