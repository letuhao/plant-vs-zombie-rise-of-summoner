using System.Net.Http.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.ActorSurface;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace FusionRpg.Server.Tests;

public sealed class DerivedSurfaceEndpointsTests : IAsyncLifetime
{
    WebApplication _app = null!;
    HttpClient _http = null!;

    public async Task InitializeAsync()
    {
        ActorSurfaceCatalogHub.ConfigureAll(
            AptitudeSurfaceCatalogLoader.Parse(ReadTuning("aptitude-catalog.v1.json")),
            DerivedStatSurfaceCatalogLoader.Parse(ReadTuning("derived-stat-catalog.v2.json")),
            StatusSurfaceCatalogLoader.Parse(ReadTuning("status-catalog.v1.json")),
            ResourceSurfaceCatalogLoader.Parse(ReadTuning("resource-catalog.v1.json")),
            ElementSurfaceCatalogLoader.Parse(ReadTuning("element-catalog.v1.json")),
            ActorSheetSurfaceCatalogLoader.Parse(ReadTuning("actor-sheet.v1.json")));

        var port = GetFreeTcpPort();
        var baseUrl = $"http://127.0.0.1:{port}";
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls(baseUrl);
        _app = builder.Build();
        _app.MapDerivedSurface();
        await _app.StartAsync();
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
    }

    public async Task DisposeAsync()
    {
        _http.Dispose();
        await _app.StopAsync();
    }

    [Fact]
    public async Task Get_derived_surface_returns_four_tabs()
    {
        var resp = await _http.GetAsync("/api/catalogs/derived-surface?lang=en&side=plant");
        if (!resp.IsSuccessStatusCode) throw new Exception(await resp.Content.ReadAsStringAsync());
        var body = await resp.Content.ReadFromJsonAsync<DerivedSurfaceDto>();
        Assert.NotNull(body);
        Assert.Equal("en", body!.Lang);
        Assert.Equal("plant", body.Side);
        Assert.Equal(2, body.SchemaVersion);
        Assert.Equal(new[] { "elements", "status", "resources", "other" }, body.Tabs.Select(t => t.Id));
        Assert.Contains(body.Tabs.Single(t => t.Id == "elements").Variants, v => v.Id == "omni" && v.PresentationOnly);
        var statusTab = body.Tabs.Single(t => t.Id == "status");
        Assert.Equal(25, statusTab.Variants.Count);
        Assert.Equal("omni", statusTab.Variants[0].Id);
        Assert.Contains(statusTab.Variants, v => v.Id == "butter");
        Assert.Contains(statusTab.Variants, v => v.Id == "nerve.afflicted");
        Assert.Empty(body.Tabs.Single(t => t.Id == "other").Variants);
        Assert.Equal(5, body.Tabs.Single(t => t.Id == "other").ActionCategoryVariants!.Count);
    }

    [Fact]
    public async Task Get_derived_surface_zombie_flips_hunger_label()
    {
        var resp = await _http.GetAsync("/api/catalogs/derived-surface?lang=en&side=zombie");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedSurfaceDto>();
        Assert.NotNull(body);
        Assert.Equal("Hunger", body!.Tabs.Single(t => t.Id == "resources").Variants.Single(v => v.Id == "hunger").DisplayName);
    }

    [Fact]
    public async Task Get_derived_surface_unknown_lang_falls_back_to_en()
    {
        var resp = await _http.GetAsync("/api/catalogs/derived-surface?lang=xx");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedSurfaceDto>();
        Assert.NotNull(body);
        Assert.Equal("xx", body!.Lang);
        Assert.Equal("Elements", body.Tabs.Single(t => t.Id == "elements").DisplayName);
    }

    [Fact]
    public async Task Get_derived_surface_defaults_and_family_floors()
    {
        var resp = await _http.GetAsync("/api/catalogs/derived-surface");
        resp.EnsureSuccessStatusCode();
        var body = await resp.Content.ReadFromJsonAsync<DerivedSurfaceDto>();
        Assert.NotNull(body);
        Assert.Equal("en", body!.Lang);
        Assert.Equal("plant", body.Side);
        Assert.Contains("derived-stat-catalog.v2", body.VersionStamp, StringComparison.Ordinal);
        Assert.Equal(28, body.Tabs.Single(t => t.Id == "elements").Categories.SelectMany(c => c.Families).Count());
        Assert.Equal(6, body.Tabs.Single(t => t.Id == "status").Categories.SelectMany(c => c.Families).Count());
        Assert.Equal(
            new[] { "hp", "stamina", "hunger", "spirit", "qi", "poise" },
            body.Tabs.Single(t => t.Id == "resources").Variants.Select(v => v.Id));
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

    static int GetFreeTcpPort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
