# Spec: `catalog-runtime`

**Module id:** `catalog-runtime` · **Program:** [demon-seed](../demon-seed-map.md) · **Build order:** 14 of 16
**Model calls:** none. **The riskiest module in the program** — it changes how nine shipped call sites
get their data.

## Objective

Move `DemonSpeciesCatalog` from a compiled static array to store-backed reads, so the roster the game
uses is the one `species-import` wrote, and delete the C# generation path.

Owner, Q23: *"we will build runtime to read json in our rpg server."*

## Design

### 1. The nine consumers, named

Verified by grep, 2026-09-01. Every one reads `DemonSpeciesCatalog`:

| Site | Reads | Shape of the read |
|---|---|---|
| `Battle/WaveCatalog.cs:56` | `All.Where(BaseRarity == r)` | full scan, filtered, ordered |
| `Demons/Fusion/DemonRecipeCatalog.cs:49,59` | `All` twice | full scan |
| `Demons/SummonRoller.cs:104` | `All` | full scan, pool build |
| `Expeditions/ExpeditionResolver.cs:232` | `All` | full scan |
| `World/Movement/LaneCost.cs:57-58` | `IsKnown` + `Get(id).ElementPrimary` | **per-member point lookup, in a movement loop** |
| `Data/Sqlite/RpgStore.Demons.cs:34,39` | `IsKnown`, `KnownVariants` | validation |
| `Data/Sqlite/RpgStore.Fusion.cs:160,208` | `Get` | point lookup |
| `Data/Sqlite/RpgStore.Summons.cs:103,136` | `Get`, `All.Count` | point + count |
| `Server/DemonEndpoints.cs:19` | `All.Select(...)` | full projection |

**Two access patterns, and only two:** whole-roster scans, and point lookups by `speciesId`. Nothing
does a range query or a join. That is what makes this migration tractable.

### 2. The shape of the change: keep the API, replace the source

`DemonSpeciesCatalog`'s surface — `All`, `Get`, `IsKnown`, `KnownVariants`, `DemonTypeIdFloor` — stays
exactly as it is. What changes is where `_all` comes from (`DemonSpeciesCatalog.cs:36`):
`GeneratedSpecies` becomes a snapshot loaded from the store.

**None of the nine call sites changes.** That is the whole design goal — a migration that rewrites nine
consumers is nine chances to introduce a behavioural difference, and a migration that changes one
private field is one.

### 3. The seam: `Configure`, because a static class has no data directory

`DemonSpeciesCatalog` is a **static class** whose roster is a compiled field:

```csharp
public static IReadOnlyList<DemonSpeciesDef> All => _all ??= Validate(GeneratedSpecies);
```

There is nowhere to pass a data directory. **The repo has already solved this exact problem twice**,
and the pattern is not up for invention: `DerivedStatPolicy.Configure` (host-only, called once at
Injector/Server startup) plus `UseScoped` (an `AsyncLocal` override so a test does not race every other
test) — the same shape `Combat.Element.ElementTable` and `Stats.ChannelPolicyTable` already use.

`DemonSpeciesCatalog` gains the same pair. `DemonCorpusEmit/Program.cs` already documents what happens
when a host forgets: *"RpgStore's static ctor builds a DerivedStatRegistry, which reads
DerivedStatPolicy — and that throws unless Configure has run first."* Throwing is correct. Every host
(Server, Injector, every tool, every test bootstrap) gains one `Configure` call, and the failure when
one is missed is immediate and named.

### 3a. ⛔ Correction: three downstream catalogs cannot be reloaded as written

An earlier draft of this spec said the snapshot is *"replaced only at an explicit reload point (server
start, and after an import)."* **That is not achievable without further change, and the reason is worth
stating rather than quietly dropping.**

Three catalogs build themselves from `DemonSpeciesCatalog.All` in **inline static field initialisers**:

| Site | Field |
|---|---|
| `Battle/WaveCatalog.cs:29` | `public static readonly IReadOnlyList<WaveDef> All = Build();` |
| `Demons/Fusion/DemonRecipeCatalog.cs:19,21,24` | `All`, `ById`, `ByPair` |
| `Demons/DemonMaterialCatalog.cs:11,23` | `All`, `Known` |

`static readonly` initialised inline runs **once per process, at first touch of the type**. So:

1. **A reload after import cannot propagate.** `DemonSpeciesCatalog` would hold the new roster while
   `WaveCatalog` and `DemonRecipeCatalog` still hold recipes derived from the old one — a split-brain
   catalog that is worse than no reload at all.
2. **There is a static initialisation order hazard.** Whichever of the three is touched first triggers
   `DemonSpeciesCatalog.All`. If `Configure` has not run, that is a throw at an arbitrary call site
   rather than at startup.

**So the design is narrowed, deliberately: the roster is loaded once at host startup and is immutable
for the process lifetime.** No live reload. An import requires a server restart — which is already how
this repo deploys (`deploy-play.ps1 -RestartServer`), and already how a fresh `wwwroot` reaches a
running server.

The three downstream catalogs convert from inline `static readonly` to the same `_x ??= Build()` lazy
form the species catalog uses, **not to gain reload**, but so that first touch happens after
`Configure` rather than at an unpredictable point. A guard test asserts no `static readonly ... =
Build()` remains on a path that reads the species catalog.

**Point lookups stay O(1).** `Get` goes through `ByIdMap()`, a cached dictionary, and `LaneCost:57-58`
does a per-party-member lookup inside movement. A per-call database read there is precisely the defect
class the 2026-08 perf audit named — uncached resolves on a hot path, not transport. The snapshot is
what keeps that read free.

### 4. What happens when the table is empty

Today the catalog cannot be empty: it is compiled in. After this change it can be — a fresh database,
a failed import, a wrong data directory.

**Failing loudly at load beats failing later.** `Validate()` already runs on first touch
(`DemonSpeciesCatalog.cs:39`); it gains an empty-roster refusal that names the data directory and the
importer command. A server that starts with zero species and reports healthy is a server that fails in
`SummonRoller` an hour later with an error nobody can trace back.

### 5. What is deleted

| Deleted | Why it is safe |
|---|---|
| `Demons/Generation/DemonSpeciesGenerator.cs` | **no production callers** — referenced only from `DemonCatalogTests` |
| `Demons/DemonSpeciesCatalog.Generated.cs` | superseded by the imported roster |
| `tools/DemonCatalogGen` | its output is the file above |
| `Demons/Generation/DemonCorpusBuilder.cs` | consumed only by `DemonCorpusEmit`, deleted in `anchor-emit` |
| `tests/.../DemonCatalogTests.cs` generator tests | they test a deleted generator |

**The catalog tests are not deleted wholesale.** The ones asserting *properties of the roster* —
element distribution, id uniqueness, `DemonTypeId` floor — are retargeted at the loaded catalog. They
are the regression suite for this migration and are more valuable after it than before.

### 6. The proof this migration is correct

Before deleting `DemonSpeciesCatalog.Generated.cs`, both sources exist. So:

**A test loads the store-backed catalog and the compiled one and diffs them field by field.**

For the 84 species present in both, the only intended differences are the ones `anchor-emit --diff-legacy`
already reported and a human accepted. Any *unintended* difference is a defect in the chain, and this is
the last point at which it is cheap to find. Deleting the generated file first throws away the only
reference.

### 7. Order of operations

1. Convert the three downstream catalogs (§3a) to lazy form, and add the guard test. **First**, because
   it is behaviour-preserving today and removes the ordering hazard before anything else moves.
2. Add `Configure` / `UseScoped` to `DemonSpeciesCatalog`; every host gains its call.
3. Add store-backed loading behind the existing API; keep `GeneratedSpecies` as the source.
4. Add the diff test — both sources live.
5. Flip the source to the store.
6. Run the suites; the retargeted property tests are the gate.
7. Delete §5's list.

Steps 4 and 5 are separate on purpose. A flip with no diff test is a migration whose correctness was
asserted rather than checked. Step 1 is first for the same reason: fix the ordering hazard while it is
still invisible, not while a second change is also in flight.

## Commands

```powershell
dotnet test tests/FusionRpg.Core.Tests --filter DemonCatalog
dotnet test tests/FusionRpg.Data.Tests
dotnet test tests/FusionRpg.Guard.Tests
.\scripts\guard-dal.ps1
.\scripts\deploy-play.ps1 -NoServer     # then a real lawn run: summon, fuse, expedition
```

**A live check is required for this module**, not optional. Nine call sites across battle, fusion,
summoning, expeditions and movement is beyond what the unit suites cover, and the failure mode is a
roster that loads but behaves differently.

## Project structure

```text
src/FusionRpg.Core/Demons/DemonSpeciesCatalog.cs        API unchanged; source swapped
src/FusionRpg.Core/Demons/SpeciesSnapshot.cs            immutable snapshot + reload point
src/FusionRpg.Data/Sqlite/RpgStore.Species.cs           the read side (shared with species-import)
tests/FusionRpg.Core.Tests/Demons/DemonCatalogTests.cs  retargeted property tests + the diff test
```

## Code style

```csharp
// The API is unchanged on purpose. Nine call sites read this catalog; rewriting them is
// nine chances to change behaviour, swapping the private source is one.
static IReadOnlyList<DemonSpeciesDef>? _all;
```

## Testing strategy

| Test | Asserts |
|---|---|
| `store_backed_catalog_matches_generated_for_the_84` | the migration proof, while both exist |
| `empty_roster_refuses_at_load_naming_the_importer` | the loud-failure rule |
| `Get_is_constant_time_after_load` | no per-call database read on a point lookup |
| `All_returns_the_same_instance_within_a_snapshot` | no per-call rescan |
| `no_static_readonly_build_reads_the_species_catalog` | the §3a guard; the split-brain hazard cannot return |
| `touching_any_downstream_catalog_before_Configure_throws` | named at startup, not at an arbitrary site |
| `species_ids_are_unique_and_typeIds_are_above_the_floor` | retargeted property test |
| `every_one_of_the_nine_consumers_works_against_a_loaded_catalog` | one integration test per site |

## Boundaries

**Always:** keep the public API; load once at host startup into an immutable snapshot; fail loudly on
an empty roster; run the diff test before deleting anything; give every host a `Configure` call.

**Ask first:** changing any of the nine call sites (the design says none of them should need to).

**Never:** read the database per call in `LaneCost` or `WaveCatalog`; delete the generated file before
the diff test passes; let a server start healthy with zero species; claim a live reload that the three
downstream catalogs cannot honour.

## Success criteria

- [ ] All nine consumers work unchanged against the store-backed catalog.
- [ ] The diff against the compiled roster is reviewed and accepted before deletion.
- [ ] An empty roster refuses at load and names the fix.
- [ ] No per-call database read exists on any point-lookup path.
- [ ] No `static readonly ... = Build()` remains on a path that reads the species catalog.
- [ ] A real lawn run exercises summon, fusion and expedition after the flip.
- [ ] `DemonSpeciesGenerator`, `DemonSpeciesCatalog.Generated.cs` and `tools/DemonCatalogGen` are gone.
