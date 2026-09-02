using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Power;
using FusionRpg.Core.Stats.Aptitudes;

// species-generator's own CLI (T4.5, spec-species-generator.md §7-8). Reads every real classified
// anchor under the seed root, expands each into a ConcreteSpecies, and writes the committed,
// canonically-serialised generated tree.
//
// Usage: dotnet run --project tools/DemonSpeciesGen -- [--seed <dir>] [--out <dir>] [--check]
//                                                       [--explain <speciesId>]
//        --seed     default: data/seed/demons/species, found by walking up from the working directory
//        --out      default: data/generated/demons
//        --check    compare against what is on disk; write nothing; exit 1 if anything differs
//        --explain  print the full derivation chain for one species; write nothing
//
// Exit codes: 0 clean/written, 1 stale (--check) or species not found (--explain), 2 could not start.

var args2 = args.ToList();
string? seedOverride = TakeOption("--seed");
string? outOverride = TakeOption("--out");
var check = args2.Remove("--check");
string? explainSpeciesId = TakeOption("--explain");

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

var repoRoot = FindUp("data", "tuning");
if (repoRoot is null)
{
    Console.Error.WriteLine("could not locate data/tuning; needed to load the shipped balance surface");
    return 2;
}

// repoRoot is ".../data/tuning" — its parent is ".../data", so "generated/demons" (not "data/generated/demons").
var outRoot = outOverride ?? Path.Combine(Directory.GetParent(repoRoot)!.FullName, "generated", "demons");

AptitudeTuning aptitudeTuning;
PowerTuning powerTuning;
DemonShapeTuning shapeTuning;
DemonThreatTuning threatTuning;
try
{
    aptitudeTuning = AptitudeTuningLoader.Parse(File.ReadAllText(Path.Combine(repoRoot, "aptitudes.v2.json")));
    powerTuning = PowerTuningLoader.Parse(File.ReadAllText(Path.Combine(repoRoot, "power-scale.v2.json")));
    shapeTuning = DemonShapeTuningLoader.Parse(File.ReadAllText(Path.Combine(repoRoot, "demon-shape.v1.json")));
    threatTuning = DemonThreatTuningLoader.Parse(File.ReadAllText(Path.Combine(repoRoot, "demon-threat.v1.json")));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"could not load the balance surface: {ex.Message}");
    return 2;
}

var anchors = new List<AnchorRow>();
foreach (var file in Directory.GetFiles(seedRoot, "*.json", SearchOption.AllDirectories).OrderBy(f => f, StringComparer.Ordinal))
{
    if (Path.GetFileName(file).StartsWith('_')) continue; // notes/exemplars, matching AtomImporter's own convention
    try { anchors.AddRange(AnchorRowReader.ReadAll(File.ReadAllText(file))); }
    catch (AnchorRowRejection ex) { Console.Error.WriteLine($"{file}: {ex.Message}"); return 1; }
}

if (explainSpeciesId is not null)
{
    var anchor = anchors.FirstOrDefault(a => string.Equals(a.SpeciesId, explainSpeciesId, StringComparison.Ordinal));
    if (anchor is null)
    {
        Console.Error.WriteLine($"'{explainSpeciesId}' not found under {seedRoot}");
        return 1;
    }
    Explain(anchor, aptitudeTuning, powerTuning, shapeTuning, threatTuning);
    return 0;
}

var stale = new List<string>();
var written = 0;
foreach (var anchor in anchors)
{
    ConcreteSpecies species;
    try { species = SpeciesExpander.Expand(anchor, aptitudeTuning, powerTuning, shapeTuning, threatTuning); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"'{anchor.SpeciesId}': {ex.Message}");
        return 1;
    }

    var json = ConcreteSpeciesSerializer.Canonical(species);
    var outPath = Path.Combine(outRoot, anchor.SpeciesId + ".json");

    if (check)
    {
        var existing = File.Exists(outPath) ? File.ReadAllText(outPath) : null;
        if (existing != json) stale.Add(anchor.SpeciesId);
        continue;
    }

    Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
    if (!File.Exists(outPath) || File.ReadAllText(outPath) != json)
    {
        File.WriteAllText(outPath, json);
        written++;
    }
}

if (check)
{
    if (stale.Count > 0)
    {
        Console.Error.WriteLine($"{stale.Count} species stale against {outRoot}: {string.Join(", ", stale)}");
        return 1;
    }
    Console.WriteLine($"--check: clean, {anchors.Count} species match {outRoot}");
    return 0;
}

Console.WriteLine($"{anchors.Count} species expanded, {written} file(s) written to {outRoot}");
return 0;

static void Explain(
    AnchorRow anchor, AptitudeTuning aptitudeTuning, PowerTuning powerTuning,
    DemonShapeTuning shapeTuning, DemonThreatTuning threatTuning)
{
    var species = SpeciesExpander.Expand(anchor, aptitudeTuning, powerTuning, shapeTuning, threatTuning);

    Console.WriteLine($"speciesId       = {anchor.SpeciesId}");
    Console.WriteLine($"rarity          = {anchor.Rarity}  -> ConcreteSpecies.Rarity = {species.Rarity}");
    Console.WriteLine($"threatBand      = {anchor.ThreatBand ?? "(absent, using demon-threat.v1.json's own inferredDefaultRung " + threatTuning.InferredDefaultRung + ")"}");
    Console.WriteLine($"  thetaOffset   = {threatTuning.OffsetFor(anchor.ThreatBand)} (demon-threat.v1.json)");
    Console.WriteLine($"  speciesBaseTheta = {shapeTuning.SpeciesBaseTheta} (demon-shape.v1.json)");
    Console.WriteLine($"theta           = {species.Theta}  (base + offset)");
    Console.WriteLine($"pTheta          = {species.PTheta}  (PowerLadder(power-scale.v2.json).Value(theta))");
    Console.WriteLine($"aptitudePrimary = {anchor.AptitudePrimary}, pure = {anchor.Pure}");
    Console.WriteLine($"aptitudeSecondary = {anchor.AptitudeSecondary ?? "(none)"}");
    Console.WriteLine($"attackTempo     = {anchor.AttackTempo} -> attackIntervalMs = {species.AttackIntervalMs} ({species.AttackIntervalSource})");
    Console.WriteLine($"reach           = {anchor.Reach} -> rangeCells = {species.RangeCells}");
    Console.WriteLine($"variants        = [{string.Join(", ", anchor.Variants)}] -> variantCount = {species.VariantCount}");
    Console.WriteLine($"magnitudes ({species.Magnitudes.Count} channel(s)):");
    foreach (var (channel, value) in species.Magnitudes.OrderBy(kv => kv.Key, StringComparer.Ordinal))
    {
        var edge = aptitudeTuning.Edges.First(e => e.Channel == channel &&
            (e.Source == anchor.AptitudePrimary || e.Source == anchor.AptitudeSecondary));
        Console.WriteLine($"  {channel,-40} = {value,12}  (source={edge.Source} kMilli={edge.KMilli})");
    }
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
