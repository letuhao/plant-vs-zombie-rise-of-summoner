using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// The map view renders from a checked-in fixture so the FE has no server dependency (W13). That
/// only stays honest if something notices when the DTO moves, which is this: the fixture must be
/// exactly what `GET /api/world/{id}/state` returns for `first-light`.
///
/// Set `FUSIONRPG_BLESS_WORLD_FIXTURE=1` to rewrite it after a deliberate DTO change.
/// </summary>
[Collection("e2e")]
public class WorldFixtureTests : IAsyncLifetime
{
    const string FixturePath = "web/fusion-rpg-web/src/features/world/fixtures/first-light.json";

    readonly HttpClient _http;

    public WorldFixtureTests(RpgApiFactory factory) => _http = factory.CreateClient();

    public async Task InitializeAsync() =>
        (await _http.PostAsJsonAsync("/api/test/reset", new { })).EnsureSuccessStatusCode();

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task The_checked_in_world_fixture_still_matches_the_live_dto()
    {
        (await _http.PostAsJsonAsync("/api/test/world/create", new
        {
            worldId = "first-light",
            templateId = "first-light",
            seed = "1"
        })).EnsureSuccessStatusCode();

        var state = await _http.GetFromJsonAsync<JsonElement>("/api/world/first-light/state");
        var json = JsonSerializer.Serialize(state, new JsonSerializerOptions { WriteIndented = true }) + "\n";

        var path = Path.Combine(RepoRoot(), FixturePath);
        if (Environment.GetEnvironmentVariable("FUSIONRPG_BLESS_WORLD_FIXTURE") == "1")
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, json);
        }

        Assert.True(File.Exists(path), $"missing fixture {FixturePath} — run with FUSIONRPG_BLESS_WORLD_FIXTURE=1");
        Assert.Equal(json.Replace("\r\n", "\n"), File.ReadAllText(path).Replace("\r\n", "\n"));
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "AGENTS.md"))) dir = dir.Parent;
        return dir?.FullName ?? throw new InvalidOperationException("repo root not found");
    }
}
