using System.Text.Json;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.ActorSurface;

public sealed record DerivedStatSurfaceEntry(
    string Family,
    string Axis,
    DerivedComposeKind Compose,
    UnitClass UnitClass,
    string DisplayName,
    string Reading,
    string Icon,
    string Gauge,
    string SheetGroup,
    string? CapRef);

public sealed record DerivedStatSurfaceCatalog(
    int SchemaVersion,
    int Version,
    IReadOnlyList<DerivedStatSurfaceEntry> Entries);

/// <summary>Pure parser, no file I/O (tunables-ssot.md T7.2).</summary>
public static class DerivedStatSurfaceCatalogLoader
{
    const string Catalog = "derived-stat-catalog";

    public static DerivedStatSurfaceCatalog Parse(string json)
    {
        using var doc = ActorSurfaceJson.ParseDocument(json, Catalog);
        var root = doc.RootElement;
        var schemaVersion = ActorSurfaceJson.Int(root, "schemaVersion", "$", Catalog);
        var version = ActorSurfaceJson.Int(root, "version", "$", Catalog);
        var arr = ActorSurfaceJson.Arr(root, "entries", "$", Catalog);

        var entries = new List<DerivedStatSurfaceEntry>(arr.GetArrayLength());
        var i = 0;
        foreach (var el in arr.EnumerateArray())
        {
            if (el.ValueKind != JsonValueKind.Object)
                throw new ActorSurfaceCatalogRejection($"{Catalog}: non-object '$.entries[{i}]'");
            var path = $"entries[{i}]";
            entries.Add(new DerivedStatSurfaceEntry(
                Family: ActorSurfaceJson.Str(el, "family", path, Catalog),
                Axis: ActorSurfaceJson.Str(el, "axis", path, Catalog),
                Compose: ActorSurfaceJson.EnumValue<DerivedComposeKind>(el, "compose", path, Catalog),
                UnitClass: ActorSurfaceJson.EnumValue<UnitClass>(el, "unitClass", path, Catalog),
                DisplayName: ActorSurfaceJson.Str(el, "displayName", path, Catalog),
                Reading: ActorSurfaceJson.Str(el, "reading", path, Catalog),
                Icon: ActorSurfaceJson.Str(el, "icon", path, Catalog),
                Gauge: ActorSurfaceJson.Str(el, "gauge", path, Catalog),
                SheetGroup: ActorSurfaceJson.Str(el, "sheetGroup", path, Catalog),
                CapRef: ActorSurfaceJson.OptionalStr(el, "capRef", path, Catalog)));
            i++;
        }

        return new DerivedStatSurfaceCatalog(schemaVersion, version, entries);
    }
}

public static class DerivedStatSurfaceCatalogHub
{
    static DerivedStatSurfaceCatalog? _catalog;

    public static void Configure(DerivedStatSurfaceCatalog catalog) =>
        _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));

    public static DerivedStatSurfaceCatalog Catalog => _catalog ?? throw new InvalidOperationException(
        "DerivedStatSurfaceCatalogHub.Configure(...) has not run. Hosts read data/tuning/derived-stat-catalog.v{n}.json " +
        "(tunables-ssot.md T5) — there is no built-in default to fall back to.");
}
