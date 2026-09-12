using FusionRpg.Tools.ItemSeedValidator.Registries;
using FusionRpg.Tools.ItemSeedValidator;

// The deterministic gate on the 125-agent item seed build. Reads files, resolves them against the
// six wave-0 registries, and reports. It opens no database and issues no SQL — this tool validates
// content, and `scripts/guard-dal.ps1` does not scan tools/.
//
// Usage: dotnet run --project tools/ItemSeedValidator -- [seed root] [--warnings-as-errors]
//        default seed root: data/seed/items, found by walking up from the working directory.

var warningsAsErrors = args.Contains("--warnings-as-errors", StringComparer.Ordinal);
// Briefs are generated from the allocation, and every partition-id defect this build has hit came
// from a brief transcribing it by hand instead. --list-partitions makes the authority readable.
var listPartitions = args.Contains("--list-partitions", StringComparer.Ordinal);
// The name-collision repair (seedsmith `items repair-names`) needs the SAME grouping the validator
// enforces. Reimplementing the collision normalizer in Python would fork it, so this mode prints the
// authoritative groups as JSON for that tool to consume.
var collisionGroups = args.Contains("--collision-groups", StringComparer.Ordinal);
var positional = args.Where(a => !a.StartsWith("--", StringComparison.Ordinal)).ToList();

var seedRoot = positional.Count > 0 ? Path.GetFullPath(positional[0]) : FindDefaultSeedRoot();
if (seedRoot is null)
{
    Console.Error.WriteLine("could not locate data/seed/items; pass the seed root explicitly");
    return 2;
}

if (!Directory.Exists(Path.Combine(seedRoot, Validator.RegistryDirName)))
{
    Console.Error.WriteLine($"no {Validator.RegistryDirName}/ under {seedRoot}; "
                            + "the validator cannot run without the wave-0 registries");
    return 2;
}

if (listPartitions)
{
    var registries = RegistrySet.Load(Path.Combine(seedRoot, Validator.RegistryDirName));
    var allocation = NamespaceAllocation.Build(registries);
    Console.WriteLine($"{"partition",-42} {"stage",-6} {"kind",-22} idPrefix");
    foreach (var a in allocation.All.OrderBy(a => a.Stage).ThenBy(a => a.PartitionId, StringComparer.Ordinal))
        Console.WriteLine($"{a.PartitionId,-42} {a.Stage,-6} {a.Kind,-22} {a.Prefix}{ShapeHint(a.Shape)}");
    foreach (var problem in allocation.Problems) Console.Error.WriteLine($"! {problem}");
    return 0;

    static string ShapeHint(SequenceShape shape) => shape switch
    {
        SequenceShape.ThreeDigit => "{seq:03}",
        SequenceShape.Fixed => "",
        SequenceShape.Derived => "{gridCellTokens}",
        _ => "{seq}",
    };
}

if (collisionGroups)
{
    var registries = RegistrySet.Load(Path.Combine(seedRoot, Validator.RegistryDirName));
    var normalizer = new FusionRpg.Tools.ItemSeedValidator.Naming.NameNormalizer(registries);
    var files = FusionRpg.Tools.ItemSeedValidator.Validator.Discover(seedRoot);

    // Group every player-facing name by the validator's own normalized key. A group of 2+ is one
    // collision; the winner is the lexically first id, and every other row must be renamed.
    var groups = new Dictionary<string, List<object>>(StringComparer.Ordinal);
    foreach (var file in files)
    {
        if (file.Root is null) continue;
        // An exemplar is a PATTERN, not corpus content, and the validator exempts it from the
        // global-uniqueness rules (NamingCheck.ExemptFromGlobalUniqueness). Including it here would
        // make the repair rename a real row to avoid colliding with a demonstration that never ships.
        if (file.IsExemplar) continue;
        if (file.Kind is "display-template" or "curve" or "recipe") continue; // no player-facing name
        foreach (var entry in file.Entries)
        {
            var name = entry.AsString("name");
            if (string.IsNullOrWhiteSpace(name)) continue;
            var key = normalizer.Normalize(name).Key;
            if (key.Length == 0) continue;
            if (!groups.TryGetValue(key, out var list))
                groups[key] = list = new List<object>();
            list.Add(new
            {
                id = entry.Id, name, kind = file.Kind, file = file.RelativePath,
                nameKey = entry.NameKey, partition = file.Directory,
            });
        }
    }

    var payload = groups
        .Where(g => g.Value.Count > 1)
        .OrderBy(g => g.Key, StringComparer.Ordinal)
        .Select(g => new
        {
            key = g.Key,
            members = g.Value.OrderBy(m => (string)m.GetType().GetProperty("id")!.GetValue(m)!,
                                             StringComparer.Ordinal).ToList(),
        })
        .ToList();

    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(
        new { collisionGroups = payload.Count, groups = payload },
        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    return 0;
}

ValidationResult result;
try
{
    result = Validator.Run(seedRoot);
}
catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine($"registry load failed: {ex.Message}");
    return 2;
}

Console.Write(Report.Render(result, seedRoot));

if (result.ScannedNothing) return 1;
if (result.ErrorCount > 0) return 1;
if (warningsAsErrors && result.WarningCount > 0) return 1;
return 0;

static string? FindDefaultSeedRoot()
{
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "data", "seed", "items");
        if (Directory.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    return null;
}
