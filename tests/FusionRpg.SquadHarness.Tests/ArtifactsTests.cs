using System.Text.Json;
using FusionRpg.SquadHarness.Tests.TestSupport;
using FusionRpg.Tools.SquadHarness;
using Xunit;

namespace FusionRpg.SquadHarness.Tests;

/// <summary>
/// spec-squad-harness.md §5/Project structure -- the two on-disk artifacts. Every test here writes to a
/// TEMP path, never the real <c>docs/research/passive-tree/</c> files: a test run proposing numbers into
/// the checked-in research artifact would be indistinguishable from a real measurement, which is exactly
/// the "golden-artifact policy" (Testing strategy) this module refuses to blur.
/// </summary>
public class ArtifactsTests : IDisposable
{
    readonly List<string> _tempPaths = new();

    string TempJsonPath()
    {
        var path = Path.Combine(Path.GetTempPath(), $"squad-harness-test-{Guid.NewGuid():N}.json");
        _tempPaths.Add(path);
        return path;
    }

    public void Dispose()
    {
        foreach (var path in _tempPaths)
            if (File.Exists(path)) File.Delete(path);
    }

    [Fact]
    public void WriteTransfer_writes_to_the_given_path_and_never_the_named_defaults_when_out_is_given()
    {
        TuningBootstrap.Configure();
        var result = TransferReport.Build(new RunSpec(60, 3, 20260906), TinyClassifiedRoster.Duels(),
            TinyClassifiedRoster.Squads(), AllocationShape.PerActor, refineTrials: null, parallel: false);

        var path = TempJsonPath();
        var json = Artifacts.WriteTransfer(result, path);

        Assert.True(File.Exists(path));
        Assert.Equal(json, File.ReadAllText(path));
    }

    [Fact]
    public void WriteTransfer_output_carries_a_number_and_a_half_width_for_every_row_and_column()
    {
        TuningBootstrap.Configure();
        var result = TransferReport.Build(new RunSpec(60, 3, 20260906), TinyClassifiedRoster.Duels(),
            TinyClassifiedRoster.Squads(), AllocationShape.PerActor, refineTrials: null, parallel: false);

        var json = Artifacts.WriteTransfer(result, TempJsonPath());
        using var doc = JsonDocument.Parse(json);
        var rows = doc.RootElement.GetProperty("rows");
        Assert.Equal(4, rows.GetArrayLength());
        foreach (var row in rows.EnumerateArray())
        foreach (var column in new[] { "duelClosedForm", "duelTrials", "squadTrials" })
        {
            var cell = row.GetProperty(column);
            Assert.True(cell.TryGetProperty("winShareMilli", out _));
            Assert.True(cell.TryGetProperty("halfWidthMilli", out _));
        }
    }

    [Fact]
    public void WriteTransfer_never_writes_to_data_tuning()
    {
        TuningBootstrap.Configure();
        var result = TransferReport.Build(new RunSpec(60, 2, 20260906), TinyClassifiedRoster.Duels(),
            TinyClassifiedRoster.Squads(), AllocationShape.PerActor, refineTrials: null, parallel: false);

        var tuningDir = Path.Combine(TuningBootstrap.FindRepoRoot(), "data", "tuning");
        var before = Directory.GetFiles(tuningDir).Select(File.GetLastWriteTimeUtc).ToList();
        Artifacts.WriteTransfer(result, TempJsonPath());
        var after = Directory.GetFiles(tuningDir).Select(File.GetLastWriteTimeUtc).ToList();

        Assert.Equal(before.Count, after.Count);
        Assert.Equal(before, after);
    }

    [Fact]
    public void WriteSquadScope_marks_every_pair_with_a_half_width_and_a_lowConfidence_flag()
    {
        TuningBootstrap.Configure();
        var roster = TinyClassifiedRoster.Squads().Select(RosterEntry.From).ToList();
        var spec = new RunSpec(60, 3, 20260906);
        var screening = Screening.RunWithRefine("squad", roster, spec, refineTrials: null, parallel: false);

        var json = Artifacts.WriteSquadScope("squad", screening, spec, AllocationShape.PerActor, TempJsonPath());
        using var doc = JsonDocument.Parse(json);
        var pairs = doc.RootElement.GetProperty("pairs");
        Assert.True(pairs.GetArrayLength() > 0);
        foreach (var pair in pairs.EnumerateArray())
        {
            Assert.True(pair.TryGetProperty("winShareMilli", out _));
            Assert.True(pair.TryGetProperty("halfWidthMilli", out _));
            Assert.True(pair.TryGetProperty("lowConfidence", out _));
            Assert.True(pair.TryGetProperty("refined", out _));
        }
    }

    [Fact]
    public void WriteSquadScope_marks_refined_cells_when_a_refine_pass_ran()
    {
        TuningBootstrap.Configure();
        var roster = TinyClassifiedRoster.Squads().Select(RosterEntry.From).ToList();
        var spec = new RunSpec(60, 1, 20260906); // 1 trial: every cell fails to call a winner
        var screening = Screening.RunWithRefine("squad", roster, spec, refineTrials: 5, parallel: false);

        Assert.Equal(screening.Run.Pairs.Count, screening.RefinedPairKeys.Count);

        var json = Artifacts.WriteSquadScope("squad", screening, spec, AllocationShape.PerActor, TempJsonPath());
        using var doc = JsonDocument.Parse(json);
        foreach (var pair in doc.RootElement.GetProperty("pairs").EnumerateArray())
            Assert.True(pair.GetProperty("refined").GetBoolean());
    }
}
