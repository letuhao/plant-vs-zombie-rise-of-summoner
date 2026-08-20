using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Data;

// Dev-time generator: captured types (via the DAL — no SQL here) → committed species roster.
// Usage: dotnet run --project tools/DemonCatalogGen -- <server data dir> [output .cs path]
if (args.Length < 1)
{
    Console.Error.WriteLine("usage: DemonCatalogGen <server data dir> [output .cs]");
    return 1;
}

var dataDir = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args.Length > 1
    ? args[1]
    : Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..",
        "src", "FusionRpg.Core", "Demons", "DemonSpeciesCatalog.Generated.cs"));

var store = new RpgStore(dataDir);
store.Init();
var seeds = store.ListTypes()
    .Select(t => new CapturedTypeSeed(t.Side, t.Type, t.TypeName, t.DisplayName, (int)(t.HpBase ?? 0)))
    .ToList();
Console.WriteLine($"captured type rows: {seeds.Count} (zombie {seeds.Count(s => s.Side == "zombie")}, plant {seeds.Count(s => s.Side == "plant")})");

var species = DemonSpeciesGenerator.Generate(seeds);
DemonSpeciesCatalog.Validate(species);
File.WriteAllText(output, DemonSpeciesGenerator.EmitCSharp(species));

Console.WriteLine($"species: {species.Count} " +
    $"(legendary {species.Count(s => s.BaseRarity == DemonRarity.Legendary)}, " +
    $"epic {species.Count(s => s.BaseRarity == DemonRarity.Epic)}, " +
    $"rare {species.Count(s => s.BaseRarity == DemonRarity.Rare)}, " +
    $"common {species.Count(s => s.BaseRarity == DemonRarity.Common)}; " +
    $"light {species.Count(s => s.ElementPrimary == FusionRpg.Core.Stats.Derived.ElementTypeId.Light)}, " +
    $"dark {species.Count(s => s.ElementPrimary == FusionRpg.Core.Stats.Derived.ElementTypeId.Dark)}, " +
    $"hypno {species.Count(s => s.DeployMode == DemonDeployMode.HypnoAlly)}, " +
    $"captureOnly {species.Count(s => s.Acquisition == DemonAcquisition.CaptureOnly)})");
Console.WriteLine($"wrote {output}");
return 0;
