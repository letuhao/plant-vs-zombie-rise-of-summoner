using FusionRpg.Core.Hud;
using Xunit;

namespace FusionRpg.Core.Tests.Hud;

public sealed class ActorHudTuningLoaderTests
{
    [Fact]
    public void Parse_empty_json_rejects()
    {
        var ex = Assert.Throws<ActorHudTuningRejection>(() => ActorHudTuningLoader.Parse(""));
        Assert.Contains("empty document", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_invalid_json_rejects()
    {
        var ex = Assert.Throws<ActorHudTuningRejection>(() => ActorHudTuningLoader.Parse("{ not json"));
        Assert.Contains("not valid JSON", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_missing_badgeMax_rejects()
    {
        var json = """
            {
              "schemaVersion": 1,
              "version": 1,
              "statusStripMax": 3,
              "hpSliverEnabled": false,
              "rowOffsetIdentity": 0.42,
              "rowOffsetResources": 0.28,
              "rowOffsetStatuses": 0.14
            }
            """;

        var ex = Assert.Throws<ActorHudTuningRejection>(() => ActorHudTuningLoader.Parse(json));
        Assert.Contains("badgeMax", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_shipped_actor_hud_v1_json()
    {
        var path = Path.Combine(FindRepoRoot(), "data", "tuning", "actor-hud.v1.json");
        Assert.True(File.Exists(path), "missing " + path);

        var tuning = ActorHudTuningLoader.Parse(File.ReadAllText(path));

        Assert.Equal(1, tuning.SchemaVersion);
        Assert.Equal(1, tuning.Version);
        Assert.Equal(3, tuning.StatusStripMax);
        Assert.False(tuning.HpSliverEnabled);
        Assert.Equal(99, tuning.BadgeMax);
        Assert.Null(tuning.EliteTierThreshold);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "data", "tuning", "actor-hud.v1.json")))
                return dir.FullName;
            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root with actor-hud.v1.json");
    }
}
