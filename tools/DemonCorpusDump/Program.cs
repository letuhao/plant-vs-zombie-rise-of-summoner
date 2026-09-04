using FusionRpg.Data;
using FusionRpg.Tools.DemonCorpusDump;

// demon-seed module 1 (spec-corpus-dump.md): the whole almanac_seed/spawn_stats/recipes table,
// not the C# generator's opinion of it — the defect DemonCorpusEmit had (Program.cs:45-52) is
// what this tool exists to fix. Every database read goes through RpgStore; no SQL here.
// Usage:
//   dotnet run --project tools/DemonCorpusDump -- <server data dir> [output root]
//   dotnet run --project tools/DemonCorpusDump -- <server data dir> --check      (owner, local, real DB)
//   dotnet run --project tools/DemonCorpusDump -- --verify <dump root>           (CI — no DB needed)
if (args.Length >= 2 && args[0] == "--verify")
{
    var (ok, reason) = DumpWriter.VerifyCommittedTree(Path.GetFullPath(args[1]));
    if (ok)
    {
        Console.WriteLine($"corpus-dump --verify: {args[1]} is self-consistent ({reason}).");
        return 0;
    }
    Console.Error.WriteLine($"corpus-dump --verify: {args[1]} FAILED self-consistency — {reason}");
    return 1;
}

if (args.Length < 1)
{
    Console.Error.WriteLine("usage: DemonCorpusDump <server data dir> [output root, default data/seed/demons/_dump] [--check]");
    Console.Error.WriteLine("       DemonCorpusDump --verify <dump root>   (CI, no database needed)");
    return 1;
}

var checkOnly = args.Contains("--check");
var positional = args.Where(a => a != "--check").ToArray();

var dataDir = Path.GetFullPath(positional[0]);
var outputRoot = Path.GetFullPath(positional.Length > 1
    ? positional[1]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "seed", "demons", "_dump"));

// RpgStore's static ctor builds a DerivedStatRegistry, which reads DerivedStatPolicy — throws
// unless Configure has run first (tunables-ssot.md T5). Same fix DemonCorpusEmit needed.
var tuningDir = new[]
    {
        Path.Combine(dataDir, "tuning"),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "data", "tuning")
    }
    .Select(Path.GetFullPath)
    .FirstOrDefault(d => File.Exists(Path.Combine(d, "derived-stats.v2.json")));
if (tuningDir is null)
{
    Console.Error.WriteLine($"no tuning dir found (looked in {Path.Combine(dataDir, "tuning")} and data/tuning)");
    return 1;
}
FusionRpg.Core.Stats.Derived.DerivedStatPolicy.Configure(
    FusionRpg.Core.Stats.Derived.DerivedStatTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "derived-stats.v2.json"))));

var store = new RpgStore(dataDir);
store.Init();

var payload = CorpusReader.BuildPayload(store);

// capturedUtc: the store's own max(RebuiltUtc) over every exported almanac row — never
// DateTime.UtcNow (spec §2). A dump with zero rows has no rebuilt stamp to read; that is a
// preflight failure elsewhere, not something this tool papers over with wall-clock time.
var capturedUtc = CorpusReader.CapturedUtc(payload);

var tree = DumpWriter.BuildTree(payload, capturedUtc);

if (checkOnly)
{
    var matches = DumpWriter.MatchesDisk(outputRoot, tree);
    if (matches)
    {
        Console.WriteLine($"corpus-dump --check: tree at {outputRoot} is current (hash {tree.Manifest.ContentHash}).");
        return 0;
    }
    Console.Error.WriteLine($"corpus-dump --check: tree at {outputRoot} is STALE — run without --check and commit the result.");
    Console.Error.WriteLine($"  expected hash {tree.Manifest.ContentHash}, plant={tree.Manifest.PlantCount} zombie={tree.Manifest.ZombieCount} baselines={tree.Manifest.BaselineCount} recipes={tree.Manifest.RecipeCount}");
    return 1;
}

DumpWriter.WriteToDisk(outputRoot, tree);
Console.WriteLine(
    $"corpus-dump: wrote {outputRoot} — plant={tree.Manifest.PlantCount} zombie={tree.Manifest.ZombieCount} " +
    $"baselines={tree.Manifest.BaselineCount} recipes={tree.Manifest.RecipeCount} capturedUtc={capturedUtc} hash={tree.Manifest.ContentHash}");
return 0;
