using FusionRpg.Core.Progression;
using Xunit;

namespace FusionRpg.Core.Tests.Progression;

public sealed class ProgressionTuningTests
{
    [Fact]
    public void Loader_reads_dedicated_specimen_lawn_awards()
    {
        var tuning = ProgressionTuningLoader.Parse("""
            { "schemaVersion": 1, "version": 1,
              "xpCurve": {
                "plant": { "first": 80, "step": 32 },
                "zombie": { "first": 70, "step": 28 },
                "player": { "first": 100, "step": 45 },
                "specimen": { "first": 100, "step": 45 } },
              "awards": { "kill": 12, "defeat": -100, "mower": -30,
                "plantPlace": 8, "zombieSpawn": 9,
                "specimenLawnKill": 18, "specimenBoundIntervalMs": 1000, "specimenBoundIntervalXp": 2 } }
            """);

        Assert.Equal(18, tuning.Awards.SpecimenLawnKill);
        Assert.Equal(1000, tuning.Awards.SpecimenBoundIntervalMs);
        Assert.Equal(2, tuning.Awards.SpecimenBoundIntervalXp);
    }

    [Fact]
    public void Older_documents_default_dedicated_awards_to_zero()
    {
        var tuning = ProgressionTuningLoader.Parse("""
            { "schemaVersion": 1, "version": 1,
              "xpCurve": {
                "plant": { "first": 80, "step": 32 },
                "zombie": { "first": 70, "step": 28 },
                "player": { "first": 100, "step": 45 },
                "specimen": { "first": 100, "step": 45 } },
              "awards": { "kill": 12, "defeat": -100, "mower": -30,
                "plantPlace": 8, "zombieSpawn": 9 } }
            """);

        Assert.Equal(0, tuning.Awards.SpecimenLawnKill);
        Assert.Equal(0, tuning.Awards.SpecimenBoundIntervalMs);
        Assert.Equal(0, tuning.Awards.SpecimenBoundIntervalXp);
    }

    [Theory]
    [InlineData("specimenLawnKill")]
    [InlineData("specimenBoundIntervalMs")]
    [InlineData("specimenBoundIntervalXp")]
    public void Dedicated_awards_reject_non_positive_values(string key)
    {
        var json = $$"""
        { "xpCurve": { "player": { "first": 60, "step": 30 }, "plant": { "first": 60, "step": 30 }, "zombie": { "first": 60, "step": 30 }, "specimen": { "first": 60, "step": 30 } },
          "awards": { "kill": 20, "defeat": -100, "mower": -30, "plantPlace": 8, "zombieSpawn": 9, "{{key}}": 0 } }
        """;

        Assert.Throws<ProgressionTuningRejection>(() => ProgressionTuningLoader.Parse(json));
    }
}
