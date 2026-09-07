using System.Globalization;
using FusionRpg.Contracts;

namespace FusionRpg.Core.ActorSurface;

/// <summary>
/// Cooks <see cref="DerivedSurfaceDto"/> from configured surface hubs.
/// Join rules: expand none → <c>{family}</c>; otherwise <c>{family}.{variantId}</c> on <c>/sheet</c>.
/// </summary>
public static class DerivedSurfaceCook
{
    public static DerivedSurfaceDto Build(string? lang = null, string? side = null)
    {
        var resolvedLang = string.IsNullOrWhiteSpace(lang) ? "en" : lang.Trim();
        var resolvedSide = NormalizeSide(side);

        var derived = DerivedStatSurfaceCatalogHub.Catalog;
        var elements = ElementSurfaceCatalogHub.Catalog;
        var resources = ResourceSurfaceCatalogHub.Catalog;
        // Status hub is configured for ConfigureAll / version stamp; variants come from derived catalog.
        _ = StatusSurfaceCatalogHub.Catalog;

        var entriesByGroup = derived.Entries
            .GroupBy(e => e.SheetGroup, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);

        var tabs = derived.Tabs
            .OrderBy(t => t.Order)
            .Select(tab => CookTab(
                tab,
                derived,
                elements,
                resources,
                entriesByGroup,
                resolvedLang,
                resolvedSide))
            .ToList();

        return new DerivedSurfaceDto
        {
            Lang = resolvedLang,
            Side = resolvedSide,
            SchemaVersion = derived.SchemaVersion,
            VersionStamp = string.Create(CultureInfo.InvariantCulture,
                $"derived-stat-catalog.v{derived.SchemaVersion}+element:{elements.Version}+status:{StatusSurfaceCatalogHub.Catalog.Version}+resource:{resources.Version}"),
            Tabs = tabs
        };
    }

    static DerivedSurfaceTabDto CookTab(
        DerivedSurfaceTabDef tab,
        DerivedStatSurfaceCatalog derived,
        ElementSurfaceCatalog elements,
        ResourceSurfaceCatalog resources,
        IReadOnlyDictionary<string, List<DerivedStatSurfaceEntry>> entriesByGroup,
        string lang,
        string side)
    {
        var variants = tab.Id switch
        {
            "elements" => elements.Entries
                .OrderBy(e => e.Ordinal)
                .Select(e => new DerivedSurfaceVariantDto
                {
                    Id = e.Id,
                    DisplayName = e.DisplayName,
                    Ordinal = e.Ordinal,
                    PresentationOnly = e.PresentationOnly
                })
                .ToList(),
            "status" => derived.StatusCategoryVariants
                .OrderBy(v => v.Ordinal)
                .Select(v => new DerivedSurfaceVariantDto
                {
                    Id = v.Id,
                    DisplayName = v.DisplayName.Resolve(lang),
                    Ordinal = v.Ordinal,
                    PresentationOnly = false
                })
                .ToList(),
            "resources" => resources.Entries
                .Select((r, i) => new DerivedSurfaceVariantDto
                {
                    Id = r.Id,
                    DisplayName = side == "zombie" ? r.Labels.Zombie : r.Labels.Plant,
                    Ordinal = i,
                    PresentationOnly = false
                })
                .ToList(),
            _ => new List<DerivedSurfaceVariantDto>()
        };

        IReadOnlyList<DerivedSurfaceVariantDto>? actionCategoryVariants = null;
        if (tab.Id == "other")
        {
            actionCategoryVariants = derived.ActionCategoryVariants
                .OrderBy(v => v.Ordinal)
                .Select(v => new DerivedSurfaceVariantDto
                {
                    Id = v.Id,
                    DisplayName = v.DisplayName.Resolve(lang),
                    Ordinal = v.Ordinal,
                    PresentationOnly = false
                })
                .ToList();
        }

        var categories = derived.SheetGroups
            .Where(g => string.Equals(g.Tab, tab.Id, StringComparison.Ordinal))
            .OrderBy(g => g.Order)
            .Select(g =>
            {
                entriesByGroup.TryGetValue(g.Id, out var families);
                families ??= new List<DerivedStatSurfaceEntry>();
                return new DerivedSurfaceCategoryDto
                {
                    Id = g.Id,
                    DisplayName = g.DisplayName.Resolve(lang),
                    Order = g.Order,
                    Families = families
                        .OrderBy(f => f.Family, StringComparer.Ordinal)
                        .Select(f => new DerivedSurfaceFamilyDto
                        {
                            Family = f.Family,
                            DisplayName = f.DisplayName.Resolve(lang),
                            Reading = f.Reading.Resolve(lang),
                            Compose = f.Compose.ToString(),
                            UnitClass = f.UnitClass.ToString(),
                            Icon = f.Icon,
                            Gauge = f.Gauge,
                            CapRef = f.CapRef,
                            Expand = DerivedStatSurfaceCatalogLoader.ExpandWire(f.Expand),
                            ChannelPattern = DerivedStatSurfaceCatalogLoader.ChannelPattern(f.Expand)
                        })
                        .ToList()
                };
            })
            .ToList();

        return new DerivedSurfaceTabDto
        {
            Id = tab.Id,
            DisplayName = tab.DisplayName.Resolve(lang),
            Order = tab.Order,
            Expand = DerivedStatSurfaceCatalogLoader.ExpandWire(tab.Expand),
            Variants = variants,
            ActionCategoryVariants = actionCategoryVariants,
            Categories = categories
        };
    }

    static string NormalizeSide(string? side)
    {
        if (string.IsNullOrWhiteSpace(side))
            return "plant";
        var s = side.Trim().ToLowerInvariant();
        return s is "plant" or "zombie" ? s : "plant";
    }
}
