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
              "version": 2,
              "statusStripMax": 3,
              "hpSliverEnabled": false,
              "anchorKind": "body",
              "worldYOffset": -0.35,
              "barWorldWidth": 0.95,
              "barWorldHeight": 0.12,
              "rowOffsetIdentity": 0.30,
              "rowOffsetResources": 0.0,
              "rowOffsetStatuses": 0.16,
              "maxStackPips": 3,
              "magnitudeMidThreshold": 10.0,
              "magnitudeHighThreshold": 30.0
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
        Assert.Equal(2, tuning.Version);
        Assert.Equal(3, tuning.StatusStripMax);
        Assert.False(tuning.HpSliverEnabled);
        Assert.Equal(99, tuning.BadgeMax);
        Assert.Equal("body", tuning.AnchorKind);
        Assert.Equal(0.08, tuning.WorldYOffset);
        Assert.Equal(0.95, tuning.BarWorldWidth);
        Assert.Equal(0.12, tuning.BarWorldHeight);
        Assert.Equal(0.30, tuning.RowOffsetIdentity);
        Assert.Equal(0.0, tuning.RowOffsetResources);
        Assert.Equal(0.16, tuning.RowOffsetStatuses);
        Assert.Equal(3, tuning.MaxStackPips);
        Assert.Null(tuning.EliteTierThreshold);
        Assert.Equal(10.0, tuning.MagnitudeMidThreshold);
        Assert.Equal(30.0, tuning.MagnitudeHighThreshold);
    }

    [Fact]
    public void Parse_rejects_non_body_anchorKind()
    {
        var json = """
            {
              "schemaVersion": 1,
              "version": 2,
              "statusStripMax": 3,
              "hpSliverEnabled": false,
              "badgeMax": 99,
              "anchorKind": "crown",
              "worldYOffset": -0.35,
              "barWorldWidth": 0.95,
              "barWorldHeight": 0.12,
              "rowOffsetIdentity": 0.30,
              "rowOffsetResources": 0.0,
              "rowOffsetStatuses": 0.16,
              "maxStackPips": 3,
              "magnitudeMidThreshold": 10.0,
              "magnitudeHighThreshold": 30.0
            }
            """;

        var ex = Assert.Throws<ActorHudTuningRejection>(() => ActorHudTuningLoader.Parse(json));
        Assert.Contains("anchorKind", ex.Message, StringComparison.Ordinal);
        Assert.Contains("body", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_missing_magnitudeMidThreshold_rejects()
    {
        var json = """
            {
              "schemaVersion": 1,
              "version": 2,
              "statusStripMax": 3,
              "hpSliverEnabled": false,
              "badgeMax": 99,
              "anchorKind": "body",
              "worldYOffset": -0.35,
              "barWorldWidth": 0.95,
              "barWorldHeight": 0.12,
              "rowOffsetIdentity": 0.30,
              "rowOffsetResources": 0.0,
              "rowOffsetStatuses": 0.16,
              "maxStackPips": 3,
              "magnitudeHighThreshold": 30.0
            }
            """;

        var ex = Assert.Throws<ActorHudTuningRejection>(() => ActorHudTuningLoader.Parse(json));
        Assert.Contains("magnitudeMidThreshold", ex.Message, StringComparison.Ordinal);
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
