using System.Text.Json;
using System.Text.Json.Nodes;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Effects.Atoms.Generation;
using FusionRpg.Core.Power;

// E43 family-expand generator (spec-family-expand.md §3.1, decided 2026-09-03 — the DemonSpeciesGen
// --check pattern). Reads the 98 authored affix-family definitions
// (data/seed/items/affix-families/*.json) and the tier-bands balance surface
// (data/seed/items/_tuning/tier-bands.v1.json), and writes one atom seed file PER SOURCE FAMILY FILE
// under data/seed/atoms/generated/ — a directory already inside AtomImporter's SeedScanner.OwnedFolders
// "atoms" root, so the importer sweeps the GENERATED rows and never parses a family file itself. The
// 98 definitions stay exactly where the item program put them and never move.
//
// Usage: dotnet run --project tools/FamilyExpandGen -- [--seed <dir>] [--out <dir>] [--check]
//        --seed   default: data/seed/items, found by walking up from the working directory
//        --out    default: data/seed/atoms/generated
//        --check  regenerate in memory, diff byte-for-byte against what's on disk, write nothing;
//                 exit 1 on any drift OR on a generated file whose source family no longer exists
//
// Exit codes: 0 clean/written, 1 stale (--check), 2 could not start. A refused family (no authored
// share, no reference-base curve, no matching pool) is expected, reported content — never a failure
// on its own; only DRIFT in --check mode, or a genuine crash, fails the run (spec §3.2 step 3).

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

var itemsRoot = seedOverride ?? FindUp("data", "seed", "items");
if (itemsRoot is null || !Directory.Exists(itemsRoot))
{
    Console.Error.WriteLine("could not locate data/seed/items; pass --seed <dir>");
    return 2;
}

var familiesDir = Path.Combine(itemsRoot, "affix-families");
var tierBandsPath = Path.Combine(itemsRoot, "_tuning", "tier-bands.v1.json");
if (!Directory.Exists(familiesDir))
{
    Console.Error.WriteLine($"missing {familiesDir}");
    return 2;
}
if (!File.Exists(tierBandsPath))
{
    Console.Error.WriteLine($"missing {tierBandsPath}");
    return 2;
}

var tuningRoot = FindUp("data", "tuning");
if (tuningRoot is null)
{
    Console.Error.WriteLine("could not locate data/tuning; needed to load the shipped power-scale curve");
    return 2;
}

var powerScalePath = Path.Combine(tuningRoot, "power-scale.v2.json");
if (!File.Exists(powerScalePath))
{
    Console.Error.WriteLine($"missing {powerScalePath}");
    return 2;
}

try
{
    PowerTuningHub.Configure(PowerTuningLoader.Parse(File.ReadAllText(powerScalePath)));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"could not load {powerScalePath}: {ex.Message}");
    return 2;
}

// data/seed/items/.. -> data/seed, then atoms/generated — the shipped "atoms" root SeedScanner
// already sweeps (SeedScanner.cs OwnedFolders), never a second, unswept location.
var outRoot = outOverride ?? Path.GetFullPath(Path.Combine(itemsRoot, "..", "atoms", "generated"));

TierBandsInput tierBands;
try
{
    tierBands = TierBandsFile.Read(File.ReadAllText(tierBandsPath));
}
catch (Exception ex)
{
    Console.Error.WriteLine($"{tierBandsPath}: {ex.Message}");
    return 2;
}

var families = new List<FamilyEntryInput>();
var sourceFiles = Directory.GetFiles(familiesDir, "*.json", SearchOption.TopDirectoryOnly)
    .Where(f => !Path.GetFileName(f).StartsWith('_'))
    .OrderBy(f => f, StringComparer.Ordinal)
    .ToArray();

foreach (var file in sourceFiles)
{
    var name = Path.GetFileName(file);
    try { families.AddRange(AffixFamilyFile.Read(name, File.ReadAllText(file))); }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"{file}: {ex.Message}");
        return 2;
    }
}

// Flat op only — Increased/More read the identity ratio and never touch a game curve
// (FamilyExpansion.TryReferenceBaseM1's own doc). "hp" reuses BaseHp's curve deliberately: it is the
// same hit-point unit space as "maxHp" and there is no separate current-hp curve anywhere in the
// tuning surface — reusing the one curve that exists is not a guessed magnitude, it is the least-
// surprising reading of a channel with no dedicated one. arm1Max/arm2Max/attackInterval/
// produceInterval/zombieSpeed have NO BattleRuleset curve at all (power-scale.v2.json's own
// "channels" block ships only atk/defense) — those return null and their families are refused,
// honestly, rather than inventing a base.
long? FlatReferenceBase(string channel) => channel switch
{
    "maxHp" or "hp" => BattleRuleset.BaseHp(FamilyExpansion.ReferenceLevel),
    "atk" => BattleRuleset.BaseAtk(FamilyExpansion.ReferenceLevel),
    "defense" => BattleRuleset.BaseDefense(FamilyExpansion.ReferenceLevel),
    _ => null,
};

var result = FamilyExpansion.Expand(families, tierBands, FlatReferenceBase);

var familySource = families.ToDictionary(f => f.Id, f => f.SourceFile, StringComparer.Ordinal);
var bySource = result.Rows
    .GroupBy(r => familySource[r.FamilyId])
    .OrderBy(g => g.Key, StringComparer.Ordinal)
    .ToDictionary(g => g.Key, g => g.OrderBy(r => r.AtomId, StringComparer.Ordinal).ToList());

var wantedFiles = new Dictionary<string, string>(StringComparer.Ordinal); // absolute out path -> json text
foreach (var (sourceFile, rows) in bySource)
{
    var stem = Path.GetFileNameWithoutExtension(sourceFile);
    var outName = $"family-expand.{stem}.json";
    if (outName.StartsWith("fx-", StringComparison.OrdinalIgnoreCase))
    {
        // Mechanical, not just documented — spec-family-expand.md §3.3/§4: this generator's own
        // output must never collide with ElementEnumGen's fx-*.json sweep or with
        // EffectAtomCatalogGeneratedTests' frozen-catalog id set.
        Console.Error.WriteLine($"refusing to name generated output '{outName}' — fx-* is reserved");
        return 2;
    }

    wantedFiles[Path.GetFullPath(Path.Combine(outRoot, outName))] = ToSeedFileJson(rows);
}

var totalRows = bySource.Values.Sum(r => r.Count);
Console.WriteLine(
    $"{families.Count} families read, {totalRows} row(s) emitted across {bySource.Count} family file(s), " +
    $"{result.Refusals.Count} family(ies) refused:");
foreach (var r in result.Refusals.OrderBy(r => r.FamilyId, StringComparer.Ordinal))
    Console.WriteLine($"  {r.FamilyId} — {r.Reason}");

var existingGenerated = Directory.Exists(outRoot)
    ? Directory.GetFiles(outRoot, "family-expand.*.json").Select(Path.GetFullPath).ToArray()
    : Array.Empty<string>();

if (check)
{
    var stale = new List<string>();

    foreach (var (outPath, json) in wantedFiles)
    {
        var existing = File.Exists(outPath) ? File.ReadAllText(outPath) : null;
        if (existing != json) stale.Add(outPath);
    }

    // A committed generated file with no corresponding source family any more is stale output —
    // §3.1's own "a stale generation fails CI" acceptance criterion, not just a diff on files that
    // still exist.
    foreach (var existingFile in existingGenerated)
        if (!wantedFiles.ContainsKey(existingFile))
            stale.Add(existingFile);

    if (stale.Count > 0)
    {
        Console.Error.WriteLine($"{stale.Count} generated file(s) stale against {outRoot}:");
        foreach (var s in stale.OrderBy(x => x, StringComparer.Ordinal)) Console.Error.WriteLine("  " + s);
        return 1;
    }

    Console.WriteLine($"--check: clean, {wantedFiles.Count} generated file(s) match {outRoot}");
    return 0;
}

Directory.CreateDirectory(outRoot);
var written = 0;
foreach (var (outPath, json) in wantedFiles)
{
    if (!File.Exists(outPath) || File.ReadAllText(outPath) != json)
    {
        File.WriteAllText(outPath, json);
        written++;
    }
}

var removed = 0;
foreach (var existingFile in existingGenerated)
{
    if (wantedFiles.ContainsKey(existingFile)) continue;
    File.Delete(existingFile);
    removed++;
}

Console.WriteLine($"{written} file(s) written, {removed} stale file(s) removed, under {outRoot}");
return 0;

static string ToSeedFileJson(IReadOnlyList<AtomRow> rows)
{
    var entries = new JsonArray();
    foreach (var row in rows)
    {
        entries.Add(new JsonObject
        {
            ["family"] = row.FamilyId,
            ["tier"] = row.Tier,
            ["kind"] = row.KindId,
            ["name"] = row.Name,
            ["params"] = JsonNode.Parse(row.ParamsJson),
            ["tags"] = JsonNode.Parse(row.TagsJson),
        });
    }

    var file = new JsonObject
    {
        ["schemaVersion"] = 1,
        ["kind"] = "atom",
        ["entries"] = entries,
    };

    return file.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n";
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
