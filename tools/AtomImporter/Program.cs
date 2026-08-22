using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data;
using FusionRpg.Tools.AtomImporter;

// The seed importer (E14a). Reads data/seed/**.json, validates everything, and writes it in one
// transaction — or writes nothing and says why.
//
// Usage: dotnet run --project tools/AtomImporter -- [seed root] [--db <dir>] [--check]
//        seed root  default: data/seed, found by walking up from the working directory
//        --db       default: $FUSIONRPG_DATA, else dist/FusionRpg.Server/data beside the repo root
//        --check    validate against the real catalog and roll back — writes nothing
//
// Exit codes: 0 imported (or checked clean), 1 refused, 2 could not start.
//
// This file holds arguments and a report. Every decision about content is in Core (the format) or
// the data project (the transaction), where scripts/guard-dal.ps1 applies — it does not scan
// tools/. Which files get swept is SeedScanner, next door, so a test can hold it to that.

var check = args.Contains("--check", StringComparer.Ordinal);
var positional = new List<string>();
string? dbOverride = null;

for (var i = 0; i < args.Length; i++)
{
    if (args[i] == "--db" && i + 1 < args.Length) { dbOverride = args[++i]; continue; }
    if (args[i].StartsWith("--", StringComparison.Ordinal)) continue;
    positional.Add(args[i]);
}

var seedRoot = positional.Count > 0 ? Path.GetFullPath(positional[0]) : FindUp("data", "seed");
if (seedRoot is null || !Directory.Exists(seedRoot))
{
    Console.Error.WriteLine("could not locate data/seed; pass the seed root explicitly");
    return 2;
}

var dataDir = dbOverride
              ?? Environment.GetEnvironmentVariable("FUSIONRPG_DATA")
              ?? FindUp("dist", "FusionRpg.Server", "data");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.Error.WriteLine("no database directory: pass --db <dir> or set FUSIONRPG_DATA");
    return 2;
}

var roots = SeedScanner.Roots(seedRoot, explicitRoot: positional.Count > 0, Directory.Exists);
var files = SeedScanner.Files(roots);

if (files.Count == 0)
{
    // Never a silent green: an empty run and a clean run look identical from the exit code alone.
    Console.Error.WriteLine(
        roots.Count == 0
            ? $"no {string.Join("/ ", SeedScanner.OwnedFolders)}/ under {seedRoot} — nothing to import"
            : $"no *.json under {string.Join(", ", roots)} — nothing to import");
    return 1;
}

var read = new List<(string Path, string Json)>(files.Count);
foreach (var file in files)
{
    try
    {
        read.Add((Relative(seedRoot, file), File.ReadAllText(file)));
    }
    catch (IOException ex)
    {
        Console.Error.WriteLine($"{Relative(seedRoot, file)}: {ex.Message}");
        return 2;
    }
}

var collected = AtomSeedFile.Collect(read);
if (!collected.IsOk)
{
    Report(collected.Errors, "the files were refused; nothing was imported");
    return 1;
}

ImportOutcome outcome;
try
{
    var store = new RpgStore(dataDir);
    store.Init();
    outcome = store.ImportContent(collected.Content, dryRun: check);
}
catch (Exception ex)
{
    // The import is one transaction, so a throw anywhere inside it has already rolled back. What
    // this catch buys is a message instead of a stack trace — the author still needs to be told
    // the catalog is untouched, which a crash does not say.
    Console.Error.WriteLine($"the import failed and was rolled back: {ex.Message}");
    return 1;
}

if (!outcome.IsOk)
{
    Report(outcome.Errors, "the catalog refused the import; nothing was written");
    return 1;
}

Console.WriteLine(
    $"{files.Count} file(s): {outcome.Atoms} atom(s), {outcome.Containers} container(s), " +
    $"{outcome.Curves} curve(s), {outcome.Rarities} rarity band(s), " +
    $"{outcome.Elements} element(s)");
if (check)
{
    // The revision and the hash below are the CURRENT ones, read after the rollback — saying
    // "catalog revision now N" here would report a number this run did not produce.
    Console.WriteLine(outcome.RowsChanged == 0
        ? "--check: clean, and nothing would change"
        : $"--check: clean; {outcome.RowsChanged} row(s) would change. Nothing was written.");
    Console.WriteLine($"catalog still at revision {outcome.CatalogRevision}, content {Short(outcome)}");
    return 0;
}

Console.WriteLine(
    outcome.RowsChanged == 0
        ? "nothing changed — catalog revision and content hash both held"
        : $"{outcome.RowsChanged} row(s) changed; catalog revision now {outcome.CatalogRevision}");
Console.WriteLine($"content {Short(outcome)}");
return 0;

static string Short(ImportOutcome o) => o.ContentHash?.Short ?? "(unknown)";

static void Report(IReadOnlyList<SeedError> errors, string headline)
{
    Console.Error.WriteLine($"{errors.Count} error(s) — {headline}");
    foreach (var e in errors) Console.Error.WriteLine("  " + e);
}

static string Relative(string root, string path)
{
    var parent = Directory.GetParent(root)?.Parent?.FullName ?? root;
    return Path.GetRelativePath(parent, path).Replace('\\', '/');
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
