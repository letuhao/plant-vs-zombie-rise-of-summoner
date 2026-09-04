using System.IO;
using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// world-map W48: `seasons.*Milli` arrays are read by index (`TurnCalendar.SeasonOf(turn) % Count`)
/// — found while wiring the first real reader (`LoamUpkeep.BreakdownFor`) that a mismatched array
/// length was not yet a loader-time rejection, only a future `IndexOutOfRangeException` waiting for
/// whichever turn landed on the missing entry. This is that gap closed at the boot-time gate instead.
/// </summary>
public class WorldTuningLoaderTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "FusionRpg.slnx")))
            dir = dir.Parent;
        Assert.NotNull(dir);
        return dir!.FullName;
    }

    [Fact]
    public void The_real_shipped_world_tuning_file_parses_with_matching_season_array_lengths()
    {
        var path = Path.Combine(RepoRoot(), "data", "tuning", "world.v4.json");
        var tuning = WorldTuningLoader.Parse(File.ReadAllText(path));

        Assert.Equal(tuning.Seasons.Count, tuning.Seasons.YieldMilli.Count);
        Assert.Equal(tuning.Seasons.Count, tuning.Seasons.UpkeepMilli.Count);
        Assert.Equal(tuning.Seasons.Count, tuning.Seasons.MovementMilli.Count);
    }

    [Fact]
    public void A_seasons_upkeepMilli_array_shorter_than_count_is_rejected_at_load_not_at_first_read()
    {
        var json = """
            {
              "schemaVersion": 1, "version": 1,
              "laneCostMultiplierMilli": {}, "worldSizeNodes": {}, "strengthBands": [],
              "placeholderBattle": { "defenderBonusMilli": 0, "wipeoutRatioMilli": 0, "routWoundMilli": 0, "guardWoundMilli": 0 },
              "calendar": { "daysPerWeek": 7, "weeksPerMonth": 4, "specialWeekChanceMilli": 0, "specialMonthChanceMilli": 0, "plagueChanceMilli": 0 },
              "movement": { "dowseBudgetMilli": 0 },
              "growth": { "seatPulsePerWeek": 0, "lairMultiplierMilli": 1000, "specialWeekMultiplierMilli": 1000, "raiseCostPoints": 0, "raiseMemberHp": 110, "legionTarget": { "min": 6, "max": 10, "byTurn": 40 } },
              "seasons": { "count": 4, "monthsPerSeason": 3, "yieldMilli": [1000,1000,1000,1000], "upkeepMilli": [1000,1000,1000], "movementMilli": [1000,1000,1000,1000] }
            }
            """;

        var ex = Assert.Throws<WorldTuningRejection>(() => WorldTuningLoader.Parse(json));
        Assert.Contains("seasons.upkeepMilli", ex.Message);
    }
}
