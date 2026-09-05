using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>`species-build` T3.2 (module 6, `allocation-transport`) — `SpeciesAllocationSource`'s own
/// `ctx → allocation` resolution, fully provable with fake resolvers (mirrors
/// `SpecimenOwnershipOracle`'s established shape) — no `LawnElementIndex`, no running game.</summary>
public class SpeciesAllocationSourceTests
{
    static StatContext Ctx(StatSide side, int typeId, long? playerId = 1) => new()
    {
        Side = side, TypeId = typeId, EntityKey = $"{side}:{typeId}", PlayerId = playerId
    };

    [Fact]
    public void Commander_and_species_merge_into_one_allocation()
    {
        var source = new SpeciesAllocationSource(
            resolveSpeciesId: (side, typeId) => SpeciesLookupResult.Hit("fumeshroom"),
            resolveSpeciesAllocation: id => AptitudeAllocation.Single(AllocationScope.DemonType, "Vigor", 40),
            resolveCommanderAllocation: pid => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 30),
            reportUnconfigured: _ => Assert.Fail("should not report when the index resolves a hit"));

        var result = source.Resolve(Ctx(StatSide.Plant, 7));

        // Merged into ONE AptitudeAllocation, not two independently resolved ones.
        Assert.Equal(30, result.PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(40, result.PointsAt(AllocationScope.DemonType, "Vigor"));
        Assert.Equal(70, result.Total("Might") + result.Total("Vigor")); // scopes summed, one total
    }

    [Fact]
    public void PolevaulterZombie_and_WallNut_share_a_GameTypeId_but_resolve_differently()
    {
        // The named test the spec calls for: side stays part of the key, always.
        var source = new SpeciesAllocationSource(
            resolveSpeciesId: (side, typeId) => side == StatSide.Zombie
                ? SpeciesLookupResult.Hit("polevaulterzombie")
                : SpeciesLookupResult.Hit("wallnut"),
            resolveSpeciesAllocation: id => id == "polevaulterzombie"
                ? AptitudeAllocation.Single(AllocationScope.DemonType, "Agility", 50)
                : AptitudeAllocation.Single(AllocationScope.DemonType, "Fortitude", 50),
            resolveCommanderAllocation: _ => AptitudeAllocation.Empty,
            reportUnconfigured: _ => Assert.Fail("index is always configured in this test"));

        var zombie = source.Resolve(Ctx(StatSide.Zombie, 3));
        var plant = source.Resolve(Ctx(StatSide.Plant, 3));

        Assert.Equal(50, zombie.PointsAt(AllocationScope.DemonType, "Agility"));
        Assert.Equal(0, zombie.PointsAt(AllocationScope.DemonType, "Fortitude"));
        Assert.Equal(50, plant.PointsAt(AllocationScope.DemonType, "Fortitude"));
        Assert.Equal(0, plant.PointsAt(AllocationScope.DemonType, "Agility"));
    }

    [Fact]
    public void Unconfigured_index_reports_and_falls_back_to_commander_only_never_a_silent_zero()
    {
        var reports = new List<string>();
        var source = new SpeciesAllocationSource(
            resolveSpeciesId: (side, typeId) => SpeciesLookupResult.NotConfigured,
            resolveSpeciesAllocation: _ => throw new InvalidOperationException("must not be called when unconfigured"),
            resolveCommanderAllocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 15),
            reportUnconfigured: msg => reports.Add(msg));

        var result = source.Resolve(Ctx(StatSide.Plant, 7));

        Assert.Single(reports);
        Assert.Contains("not configured", reports[0], StringComparison.OrdinalIgnoreCase);
        // A real, if incomplete, answer (commander alone) -- not AptitudeAllocation.Empty and not a
        // fabricated species contribution.
        Assert.Equal(15, result.PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(0, result.TotalForScope(AllocationScope.DemonType));
    }

    [Fact]
    public void Configured_index_with_genuinely_no_species_is_commander_only_and_does_not_report()
    {
        var reported = false;
        var source = new SpeciesAllocationSource(
            resolveSpeciesId: (side, typeId) => SpeciesLookupResult.NoSpecies,
            resolveSpeciesAllocation: _ => throw new InvalidOperationException("must not be called when there's no species"),
            resolveCommanderAllocation: _ => AptitudeAllocation.Single(AllocationScope.Commander, "Might", 10),
            reportUnconfigured: _ => reported = true);

        var result = source.Resolve(Ctx(StatSide.Plant, 999));

        Assert.False(reported, "a genuinely-configured 'no species here' answer must not report as unconfigured");
        Assert.Equal(10, result.PointsAt(AllocationScope.Commander, "Might"));
    }

    [Fact]
    public void Constructor_rejects_null_collaborators()
    {
        AptitudeAllocation Commander(long? _) => AptitudeAllocation.Empty;
        AptitudeAllocation Species(string _) => AptitudeAllocation.Empty;
        SpeciesLookupResult Lookup(StatSide _, int __) => SpeciesLookupResult.NoSpecies;

        Assert.Throws<ArgumentNullException>(() => new SpeciesAllocationSource(null!, Species, Commander, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new SpeciesAllocationSource(Lookup, null!, Commander, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new SpeciesAllocationSource(Lookup, Species, null!, _ => { }));
        Assert.Throws<ArgumentNullException>(() => new SpeciesAllocationSource(Lookup, Species, Commander, null!));
    }

    [Fact]
    public void SourceFile_performsNoIO_everyCollaboratorIsAnInjectedDelegate()
    {
        // "No I/O on the Hot path" (T3.2's own verify step) -- a text-scan guard, matching this
        // repo's established rigor for this class of assertion (DalGuardTests etc.): the type itself
        // must never reference File/Http/Sqlite/async I/O; every real read happens behind a delegate
        // the CALLER supplies, which is what makes this fully fake-able in a test with no game or DB.
        var path = FindSourceFile();
        var text = File.ReadAllText(path);
        foreach (var forbidden in new[] { "File.", "System.IO", "HttpClient", "Sqlite", "async ", "await " })
            Assert.DoesNotContain(forbidden, text, StringComparison.Ordinal);
    }

    static string FindSourceFile()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "src", "FusionRpg.Core", "Stats", "Aptitudes", "SpeciesAllocationSource.cs");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException("could not locate SpeciesAllocationSource.cs above " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Resolve_rejects_a_null_context()
    {
        AptitudeAllocation Commander(long? _) => AptitudeAllocation.Empty;
        AptitudeAllocation Species(string _) => AptitudeAllocation.Empty;
        SpeciesLookupResult Lookup(StatSide _, int __) => SpeciesLookupResult.NoSpecies;
        var source = new SpeciesAllocationSource(Lookup, Species, Commander, _ => { });

        Assert.Throws<ArgumentNullException>(() => source.Resolve(null!));
    }
}
