using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;

// species-import (T4.6, spec-species-generator.md's downstream consumer, demon-seed module 13).
// Re-derives every real classified anchor (the SAME way DemonSpeciesGen does), refuses the WHOLE
// import if the committed data/generated/demons/** tree is stale against that re-derivation, then
// writes the roster in one transaction via RpgStore.ImportSpecies — never raw SQL.
//
// Usage: dotnet run --project tools/DemonSpeciesImport -- [--seed <dir>] [--db <dir>]
//        --seed   default: data/seed/demons/species, found by walking up from the working directory
//        --out    default: data/generated/demons — read for the staleness check, not written
//        --db     default: $FUSIONRPG_DATA, else dist/FusionRpg.Server/data beside the repo root
//
// Exit codes: 0 imported, 1 refused (stale tree or a bad row), 2 could not start.

var args2 = args.ToList();
string? seedOverride = TakeOption("--seed");
string? outOverride = TakeOption("--out");
string? dbOverride = TakeOption("--db");

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

var tuningDir = FindUp("data", "tuning");
if (tuningDir is null)
{
    Console.Error.WriteLine("could not locate data/tuning; needed to load the shipped balance surface");
    return 2;
}

var outRoot = outOverride ?? Path.Combine(Directory.GetParent(tuningDir)!.FullName, "generated", "demons");

var dataDir = dbOverride
              ?? Environment.GetEnvironmentVariable("FUSIONRPG_DATA")
              ?? FindUp("dist", "FusionRpg.Server", "data");
if (string.IsNullOrWhiteSpace(dataDir))
{
    Console.Error.WriteLine("no database directory: pass --db <dir> or set FUSIONRPG_DATA");
    return 2;
}

AptitudeTuning aptitudeTuning;
PowerTuning powerTuning;
DemonShapeTuning shapeTuning;
DemonThreatTuning threatTuning;
try
{
    aptitudeTuning = AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "aptitudes.v2.json")));
    powerTuning = PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "power-scale.v2.json")));
    shapeTuning = DemonShapeTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "demon-shape.v1.json")));
    threatTuning = DemonThreatTuningLoader.Parse(File.ReadAllText(Path.Combine(tuningDir, "demon-threat.v1.json")));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"could not load the balance surface: {ex.Message}");
    return 2;
}

var anchors = new List<AnchorRow>();
foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    if (Path.GetFileName(file).StartsWith('_')) continue;
    try { anchors.AddRange(AnchorRowReader.ReadAll(File.ReadAllText(file))); }
    catch (AnchorRowRejection ex) { Console.Error.WriteLine($"{file}: {ex.Message}"); return 1; }
}

// ---- re-derive every species, and refuse the WHOLE import if the committed tree disagrees ---------
var species = new List<ConcreteSpecies>();
var stale = new List<string>();
foreach (var anchor in anchors)
{
    ConcreteSpecies expanded;
    try { expanded = SpeciesExpander.Expand(anchor, aptitudeTuning, powerTuning, shapeTuning, threatTuning); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"'{anchor.SpeciesId}': {ex.Message}");
        return 1;
    }

    var committedPath = Path.Combine(outRoot, anchor.SpeciesId + ".json");
    var committed = File.Exists(committedPath) ? File.ReadAllText(committedPath) : null;
    if (committed != ConcreteSpeciesSerializer.Canonical(expanded)) stale.Add(anchor.SpeciesId);

    species.Add(expanded);
}

if (stale.Count > 0)
{
    Console.Error.WriteLine(
        $"{stale.Count} species stale against {outRoot} — the import refuses the WHOLE roster, not " +
        $"just the stale rows (a half-imported roster is a state nobody authored): {string.Join(", ", stale)}");
    Console.Error.WriteLine("run 'dotnet run --project tools/DemonSpeciesGen' and commit the result first");
    return 1;
}

// RpgStore's static ctor (RpgStore.Atoms.cs's ComposeKindRegistry) builds a DerivedStatRegistry,
// which reads DerivedStatPolicy.Tuning — a standalone tool must configure that hub itself before
// touching RpgStore, the same fix AtomImporter/Program.cs already carries (found running it for
// real 2026-08-30; every test project bootstraps this globally, so no in-process test catches it).
FusionRpg.Core.Stats.Derived.DerivedStatPolicy.Configure(
    FusionRpg.Core.Stats.Derived.DerivedStatTuningLoader.Parse(
        File.ReadAllText(Path.Combine(tuningDir, "derived-stats.v2.json"))));

// ---- import, one transaction, via RpgStore — never raw SQL in this tool ---------------------------
var store = new RpgStore(dataDir);
store.Init();

var outcome = store.ImportSpecies(species);
if (!outcome.IsOk)
{
    Console.Error.WriteLine($"{outcome.Errors.Count} error(s) — the roster was refused; nothing was written");
    Console.Error.WriteLine("  first: " + outcome.Errors[0]);
    return 1;
}

Console.WriteLine(
    $"{species.Count} species: {outcome.Written} written, {outcome.Unchanged} unchanged, " +
    $"{outcome.Deleted} deleted (absent upstream)");
return 0;

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
