using FusionRpg.Core.Effects;
using Xunit;

namespace FusionRpg.Core.Tests.Status;

public class StatusScenarioTests
{
    [Theory]
    [InlineData("status-wither-apply.json")]
    [InlineData("status-wither-area.json")]
    [InlineData("status-blight-spread.json")]
    [InlineData("status-blight-row.json")]
    [InlineData("status-blight-resist.json")]
    [InlineData("status-butter-apply.json")]
    [InlineData("status-butter-resist.json")]
    [InlineData("status-poison-immune.json")]
    public void Status_scenario_passes(string file)
    {
        var root = FindFixtures();
        var path = Path.Combine(root, "effects", "scenarios", file);
        var result = EffectScenarioRunner.RunFile(path, root);
        Assert.True(result.Ok, result.Error);
    }

    static string FindFixtures()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            var candidate = Path.Combine(dir, "fixtures");
            if (Directory.Exists(candidate)) return candidate;
            var up = Path.GetFullPath(Path.Combine(dir, "..", "..", "..", "..", "fixtures"));
            if (Directory.Exists(up)) return up;
            dir = Path.GetFullPath(Path.Combine(dir, ".."));
        }

        throw new DirectoryNotFoundException("fixtures");
    }
}
