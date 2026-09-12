using FusionRpg.Core.Creatures;
using FusionRpg.Core.Creatures.Generation;

// `redistribution-plan`'s own CLI (T1.7, spec-redistribution-plan.md §"Commands"/"Project structure").
// Reads every real classified anchor under the seed root, plans the whole corpus in one pass
// (SpeciesBuildPlanner), and writes the committed, canonically-serialised plan.
//
// Usage: dotnet run --project tools/CreatureBuildPlanGen -- [--seed <dir>] [--out <file>] [--check]
//        --seed     default: data/seed/creatures/species, found by walking up from the working directory
//        --out      default: data/generated/creatures/_species-build-plan.json
//        --check    compare against what is on disk; write nothing; exit 1 if it differs or refuses
//
// Exit codes: 0 clean/written, 1 stale (--check) or the plan refuses (Phase 3, out-of-band), 2 could
// not start (missing seed/tuning root, or a malformed anchor).

var args2 = args.ToList();
string? seedOverride = TakeOption("--seed");
string? outOverride = TakeOption("--out");
var check = args2.Remove("--check");

string? TakeOption(string flag)
{
    var i = args2.IndexOf(flag);
    if (i < 0 || i + 1 >= args2.Count) return null;
    var value = args2[i + 1];
    args2.RemoveRange(i, 2);
    return value;
}

var seedRoot = seedOverride ?? FindUp("data", "seed", "creatures", "species");
if (seedRoot is null || !Directory.Exists(seedRoot))
{
    Console.Error.WriteLine("could not locate data/seed/creatures/species; pass --seed <dir>");
    return 2;
}

var tuningRoot = FindUp("data", "tuning");
if (tuningRoot is null)
{
    Console.Error.WriteLine("could not locate data/tuning; needed to load the shipped balance surface");
    return 2;
}

SpeciesBuildTuning tuning;
try
{
    tuning = SpeciesBuildTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningRoot, "species-build.v1.json")));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"could not load species-build.v1.json: {ex.Message}");
    return 2;
}

// repoRoot is ".../data/tuning" — its parent is ".../data", so "generated/creatures" (not "data/generated/creatures").
var outPath = outOverride ?? Path.Combine(
    Directory.GetParent(tuningRoot)!.FullName, "generated", "creatures", "_species-build-plan.json");

var anchors = new List<AnchorRow>();
foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    if (Path.GetFileName(file).StartsWith('_')) continue; // notes/exemplars, matching CreatureSpeciesGen's own convention
    try { anchors.AddRange(AnchorRowReader.ReadAll(File.ReadAllText(file))); }
    catch (AnchorRowRejection ex) { Console.Error.WriteLine($"{file}: {ex.Message}"); return 1; }
}

var skipped = new List<string>();
var resolved = new List<AnchorRow>();
foreach (var anchor in anchors)
{
    var unresolvedFields = SpeciesExpander.UnresolvedFields(anchor);
    if (unresolvedFields.Count > 0)
    {
        skipped.Add($"{anchor.SpeciesId} ({string.Join(", ", unresolvedFields)})");
        continue;
    }
    resolved.Add(anchor);
}

if (skipped.Count > 0)
{
    Console.WriteLine(
        $"{skipped.Count} species skipped — still unresolved on at least one voted field, not " +
        $"planned: {string.Join("; ", skipped)}");
}

if (resolved.Count == 0)
{
    Console.Error.WriteLine("no resolved species to plan — nothing written");
    return 2;
}

// G1 fix (species-build casing bug): the committed plan must be keyed by the RUNTIME speciesId
// that `CreatureSpeciesCatalog`/`SpeciesBuildPlanCatalog.SharesFor` actually look up, not the
// seedsmith-anchor's own `SpeciesId` text. Two independent, unrelated pipelines mint that text:
// this reader takes it straight from the raw anchor's `speciesId` field (seedsmith-anchor
// PascalCase, e.g. "FumeShroom"), while the live roster's id comes from a totally separate
// generator (`CreatureSpeciesGenerator`'s `KebabId`, over the game's own captured type name, e.g.
// "fumeshroom") — no shared casing convention, no guaranteed textual relationship at all. The one
// identity both sides carry straight from the game itself is (Side, GameTypeId); joining on that,
// instead of guessing a text transform (case-insensitive compare, kebab-casing, etc.), is the only
// correct way to find "which live species does this anchor describe, if any."
//
// ⛔ Real bug fixed 2026-09-07 (owner caught it: "why did we still stuck at 84 creature"): this used
// to call `CreatureSpeciesCatalog.ConfigureFromCompiledDefault()` — the compiled, 84-species snapshot
// from BEFORE `catalog-runtime`'s real flip (2026-09-05). That flip already moved the real, live
// game (`Server/Program.cs:350`, `Injector/Host/RpgHost.cs`) onto the full store-backed roster
// (904 species today) — this tool alone never followed, so 820 real, live, playable species have
// been silently getting EMPTY aptitude shares (`SpeciesBuildPlanCatalog.SharesFor` returning
// `EmptyShares`, indistinguishable from "not yet classified") this whole time. Fixed by reading the
// SAME committed `data/generated/creatures/*.json` tree the live roster is actually built from, via
// the SQL-free `ConcreteSpeciesSeedReader`/`ConcreteSpeciesMapper` pair the Injector already uses
// for exactly this reason (no SQL access, no `--db` needed) — never re-deriving the mapping by hand.
var generatedCreaturesRoot = FindUp("data", "generated", "creatures");
if (generatedCreaturesRoot is null)
{
    Console.Error.WriteLine("could not locate data/generated/creatures; run tools/CreatureSpeciesGen first");
    return 2;
}

var catalogIdByKey = new Dictionary<(string Side, int GameTypeId), string>();
foreach (var file in Directory.GetFiles(generatedCreaturesRoot, "*.json", SearchOption.TopDirectoryOnly))
{
    if (Path.GetFileName(file).StartsWith('_')) continue; // _fusion-recipes.json, _species-build-plan.json
    var def = FusionRpg.Core.Creatures.Generation.ConcreteSpeciesMapper.ToCreatureSpeciesDef(
        FusionRpg.Core.Creatures.Generation.ConcreteSpeciesSeedReader.ParseFile(file));
    catalogIdByKey[(def.Side, def.GameTypeId)] = def.SpeciesId;
}

// A real anchor-authoring duplicate — two anchors claiming the same (Side, GameTypeId) — would
// silently collide on the same runtime speciesId below and corrupt the plan (last one written
// wins, with no signal). Surface it instead, matching this CLI's existing exit-code convention
// (2 = "could not start / malformed", same as the missing-seed-root and bad-tuning checks above).
var duplicateAnchorKeys = resolved
    .GroupBy(a => (a.Side, a.GameTypeId))
    .Where(g => g.Count() > 1)
    .ToList();
if (duplicateAnchorKeys.Count > 0)
{
    foreach (var dup in duplicateAnchorKeys)
    {
        var names = string.Join(", ", dup.Select(a => a.SpeciesId));
        Console.Error.WriteLine(
            $"anchor authoring duplicate: side='{dup.Key.Side}' gameTypeId={dup.Key.GameTypeId} is " +
            $"claimed by {dup.Count()} resolved anchors ({names}) — (side, gameTypeId) must be unique");
    }
    return 2;
}

// A resolved anchor with no matching (Side, GameTypeId) in `data/generated/creatures` means
// `CreatureSpeciesGen` itself skipped it (some OTHER field is still unresolved, e.g. attackTempo) —
// excluded from the plan entirely rather than written under its own unjoinable anchor text.
// Historically this excluded most of the corpus (829 resolved anchors vs. 84 compiled-default
// species, pre-flip); now that this tool reads the same live-roster tree the real game does,
// `unmatchedCount` should be ~0 (only species blocked on a non-attackTempo field, if any exist).
var unmatchedCount = 0;
var joined = new List<AnchorRow>(resolved.Count);
foreach (var anchor in resolved)
{
    if (catalogIdByKey.TryGetValue((anchor.Side, anchor.GameTypeId), out var realSpeciesId))
        joined.Add(anchor with { SpeciesId = realSpeciesId });
    else
        unmatchedCount++;
}

Console.WriteLine(
    $"{joined.Count} resolved anchor(s) matched a live species by (side, gameTypeId); " +
    $"{unmatchedCount} resolved anchor(s) describe a species not in the live roster and were " +
    "excluded from the plan");

resolved = joined;

if (resolved.Count == 0)
{
    Console.Error.WriteLine("no resolved anchor matched a shipped species — nothing written");
    return 2;
}

SpeciesBuildResult result;
try
{
    result = SpeciesBuildPlanner.Plan(resolved, tuning);
}
catch (SpeciesBuildRefusal ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

var json = SpeciesBuildPlanSerializer.Canonical(result.Vectors);

if (check)
{
    var existing = File.Exists(outPath) ? File.ReadAllText(outPath) : null;
    if (existing != json)
    {
        Console.Error.WriteLine($"{outPath} is stale against the real corpus — run " +
            "'dotnet run --project tools/CreatureBuildPlanGen' and commit the result");
        return 1;
    }
    Console.WriteLine($"--check: clean, {result.Vectors.Count} species match {outPath}");
    PrintCorpusShare(result);
    return 0;
}

Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);
File.WriteAllText(outPath, json);
Console.WriteLine($"{result.Vectors.Count} species planned, written to {outPath}");
PrintCorpusShare(result);
return 0;

static void PrintCorpusShare(SpeciesBuildResult result)
{
    Console.WriteLine("corpus-wide share per aptitude (permille):");
    foreach (var (id, share) in result.CorpusSharePermille.OrderBy(kv => kv.Key, StringComparer.Ordinal))
        Console.WriteLine($"  {id,-12} {share,4}‰");
}

static string? FindUp(params string[] segments)
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(new[] { dir.FullName }.Concat(segments).ToArray());
        if (Directory.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}
