using FusionRpg.Core.Demons.Generation;

// `redistribution-plan`'s own CLI (T1.7, spec-redistribution-plan.md §"Commands"/"Project structure").
// Reads every real classified anchor under the seed root, plans the whole corpus in one pass
// (SpeciesBuildPlanner), and writes the committed, canonically-serialised plan.
//
// Usage: dotnet run --project tools/DemonBuildPlanGen -- [--seed <dir>] [--out <file>] [--check]
//        --seed     default: data/seed/demons/species, found by walking up from the working directory
//        --out      default: data/generated/demons/_species-build-plan.json
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

var seedRoot = seedOverride ?? FindUp("data", "seed", "demons", "species");
if (seedRoot is null || !Directory.Exists(seedRoot))
{
    Console.Error.WriteLine("could not locate data/seed/demons/species; pass --seed <dir>");
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

// repoRoot is ".../data/tuning" — its parent is ".../data", so "generated/demons" (not "data/generated/demons").
var outPath = outOverride ?? Path.Combine(
    Directory.GetParent(tuningRoot)!.FullName, "generated", "demons", "_species-build-plan.json");

var anchors = new List<AnchorRow>();
foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    if (Path.GetFileName(file).StartsWith('_')) continue; // notes/exemplars, matching DemonSpeciesGen's own convention
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
            "'dotnet run --project tools/DemonBuildPlanGen' and commit the result");
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
