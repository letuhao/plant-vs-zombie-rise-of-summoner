using FusionRpg.Core.Demons.Generation;
using FusionRpg.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace FusionRpg.E2E.Tests;

public sealed class RpgApiFactory : WebApplicationFactory<Program>
{
    public string DataDir { get; } = Path.Combine(Path.GetTempPath(), "fusionrpg-e2e-" + Guid.NewGuid().ToString("N"));

    public RpgApiFactory()
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("FUSIONRPG_SIM", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_NO_BROWSER", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_DATA", DataDir);
        SeedSpeciesRoster();
    }

    /// <summary>
    /// demon-lawn-deploy T1.6 found this whole suite's own server could never start: `Program.cs:322`
    /// (`catalog-runtime`'s 2026-09-05 flip) now calls `DemonSpeciesCatalog.Configure(store.
    /// BuildDemonSpeciesSnapshot())`, which throws on an empty roster — and a fresh `DataDir` has NEVER
    /// had `species-import` run against it. Confirmed pre-existing and suite-wide, not specific to any
    /// one test file: `StorageE2ETests.cs` (untouched by this program) failed identically, 0/7, before
    /// this fix. Seeded here, once per collection fixture (a NEW `RpgStore` instance against the SAME
    /// on-disk SQLite file `Program.cs`'s own DI-registered store will open next), from the real
    /// committed corpus — the same source and reader `ConcreteSpeciesSeedReaderTests.cs`'s own
    /// `RealCommittedSpecies()` already uses, so E2E tests exercise a realistic roster, not a synthetic
    /// one-off fixture.
    /// </summary>
    void SeedSpeciesRoster()
    {
        var dir = Path.Combine(RepoRoot(), "data", "generated", "demons");
        var files = Directory.EnumerateFiles(dir, "*.json")
            .Where(p => !Path.GetFileName(p).StartsWith('_'));
        var species = files.Select(ConcreteSpeciesSeedReader.ParseFile).ToList();
        if (species.Count == 0)
            throw new InvalidOperationException($"no real committed species found under {dir}");

        var store = new RpgStore(DataDir);
        store.Init();
        var outcome = store.ImportSpecies(species);
        if (!outcome.IsOk)
            throw new InvalidOperationException(
                "RpgApiFactory.SeedSpeciesRoster failed: " + string.Join("; ", outcome.Errors));
    }

    // Marker is src/FusionRpg.Injector, NOT data/generated/demons — the same choice
    // ConcreteSpeciesSeedReaderTests.cs's own RepoRoot() already made, and for the same reason found
    // here the hard way: an MSBuild content-copy rule creates an EMPTY data/generated/demons under the
    // test's own bin output, so that marker alone stops the upward search one level too early.
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not find repo root (no src/FusionRpg.Injector above test bin)");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Directory.CreateDirectory(DataDir);
        Environment.SetEnvironmentVariable("FUSIONRPG_SIM", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_NO_BROWSER", "1");
        Environment.SetEnvironmentVariable("FUSIONRPG_DATA", DataDir);
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try { Directory.Delete(DataDir, true); } catch { /* temp */ }
    }
}
