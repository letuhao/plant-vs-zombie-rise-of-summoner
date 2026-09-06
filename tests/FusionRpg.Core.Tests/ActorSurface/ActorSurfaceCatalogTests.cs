using FusionRpg.Core.ActorSurface;
using FusionRpg.Core.Status;
using Xunit;

namespace FusionRpg.Core.Tests.ActorSurface;

public sealed class ActorSurfaceCatalogTests
{
    [Fact]
    public void Parse_shipped_aptitude_catalog()
    {
        var catalog = AptitudeSurfaceCatalogLoader.Parse(ReadTuning("aptitude-catalog.v1.json"));
        Assert.Equal(1, catalog.SchemaVersion);
        Assert.Equal(1, catalog.Version);
        Assert.Equal(12, catalog.Entries.Count);
        Assert.Equal("Might", catalog.Entries[0].Id);
        Assert.Equal("Hit harder.", catalog.Entries[0].Reading);
    }

    [Fact]
    public void Parse_shipped_derived_stat_catalog()
    {
        var catalog = DerivedStatSurfaceCatalogLoader.Parse(ReadTuning("derived-stat-catalog.v1.json"));
        Assert.Equal(1, catalog.Version);
        Assert.Contains(catalog.Entries, e => e.Family == "combat.power");
        Assert.Contains(catalog.Entries, e => e.CapRef == "categoryResistCap");
    }

    [Fact]
    public void Parse_shipped_status_catalog()
    {
        var catalog = StatusSurfaceCatalogLoader.Parse(ReadTuning("status-catalog.v1.json"));
        Assert.Equal(24, catalog.Entries.Count);
        Assert.Contains(catalog.Entries, e => e.Id == "butter" && e.Kind == StatusKind.UnityCc);
        Assert.Contains(catalog.Entries, e => e.Id == "wither" && e.Kind == StatusKind.OverTime);
    }

    [Fact]
    public void Parse_shipped_resource_catalog()
    {
        var catalog = ResourceSurfaceCatalogLoader.Parse(ReadTuning("resource-catalog.v1.json"));
        Assert.Equal(6, catalog.Entries.Count);
        var hunger = Assert.Single(catalog.Entries, e => e.Id == "hunger");
        Assert.Equal("Sun", hunger.Labels.Plant);
        Assert.Equal("Hunger", hunger.Labels.Zombie);
    }

    [Fact]
    public void Parse_shipped_element_catalog()
    {
        var catalog = ElementSurfaceCatalogLoader.Parse(ReadTuning("element-catalog.v1.json"));
        Assert.Contains(catalog.Entries, e => e.Id == "omni" && e.PresentationOnly);
        Assert.Contains(catalog.Entries, e => e.Id == "fire" && !e.PresentationOnly);
    }

    [Fact]
    public void Parse_shipped_actor_sheet()
    {
        var catalog = ActorSheetSurfaceCatalogLoader.Parse(ReadTuning("actor-sheet.v1.json"));
        Assert.Equal(ActorSheetTabKind.Condition, catalog.DefaultOpen);
        Assert.Equal(8, catalog.Tabs.Count);
        Assert.Contains(catalog.KitRoles, r => r.RoleId == "armament-primary"
            && r.Labels.Humanoid == "Weapon"
            && r.Labels.Plant == "Stem");
    }

    [Fact]
    public void Parse_rejects_unknown_tab_kind()
    {
        var json = """
            {
              "schemaVersion": 1,
              "kind": "actor-sheet",
              "version": 1,
              "defaultOpen": "condition",
              "tabs": [
                { "kind": "inventory", "label": "Inventory", "order": 0, "hidden": false }
              ],
              "kitRoles": []
            }
            """;

        var ex = Assert.Throws<ActorSurfaceCatalogRejection>(() => ActorSheetSurfaceCatalogLoader.Parse(json));
        Assert.Contains("unknown tab kind", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("inventory", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_rejects_unknown_StatusKind()
    {
        var json = """
            {
              "schemaVersion": 1,
              "kind": "status-catalog",
              "version": 1,
              "entries": [
                {
                  "id": "bogus",
                  "kind": "NotARealKind",
                  "categories": ["dot"],
                  "stacking": "Refresh",
                  "payloadKinds": [],
                  "displayName": "Bogus",
                  "reading": "No.",
                  "hudToken": "X",
                  "color": "#ffffff"
                }
              ]
            }
            """;

        var ex = Assert.Throws<ActorSurfaceCatalogRejection>(() => StatusSurfaceCatalogLoader.Parse(json));
        Assert.Contains("kind", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NotARealKind", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildDto_happy_path_counts()
    {
        ActorSurfaceCatalogHub.ConfigureAll(
            AptitudeSurfaceCatalogLoader.Parse(ReadTuning("aptitude-catalog.v1.json")),
            DerivedStatSurfaceCatalogLoader.Parse(ReadTuning("derived-stat-catalog.v1.json")),
            StatusSurfaceCatalogLoader.Parse(ReadTuning("status-catalog.v1.json")),
            ResourceSurfaceCatalogLoader.Parse(ReadTuning("resource-catalog.v1.json")),
            ElementSurfaceCatalogLoader.Parse(ReadTuning("element-catalog.v1.json")),
            ActorSheetSurfaceCatalogLoader.Parse(ReadTuning("actor-sheet.v1.json")));

        var dto = ActorSurfaceCatalogHub.BuildDto();

        Assert.Equal(12, dto.Aptitudes.Count);
        Assert.Equal(6, dto.Resources.Count);
        Assert.Equal(24, dto.Statuses.Count);
        Assert.Equal(8, dto.Tabs.Count);
        Assert.Contains("aptitude:1", dto.VersionStamp, StringComparison.Ordinal);
        Assert.Contains("sheet:1", dto.VersionStamp, StringComparison.Ordinal);
        Assert.Equal("condition", dto.Tabs[0].Kind);
    }

    static string ReadTuning(string fileName)
    {
        var path = Path.Combine(FindRepoRoot(), "data", "tuning", fileName);
        Assert.True(File.Exists(path), "missing " + path);
        return File.ReadAllText(path);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "data", "tuning", "actor-sheet.v1.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root with data/tuning/actor-sheet.v1.json");
    }
}
