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
        _ => "{seq}",
    };
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
