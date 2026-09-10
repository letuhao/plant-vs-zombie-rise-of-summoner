using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.ClassSystem;

public class AptitudeAutoAssignTests
{
    [Fact]
    public void FillEven_splits_budget_with_leftover()
    {
        var result = AptitudeAutoAssign.FillEven(100);
        Assert.True(result.Ok);
        Assert.Equal(12, result.Shares.Count);
        Assert.All(result.Shares.Values, v => Assert.Equal(8, v));
        Assert.Equal(4, result.Leftover); // 100 - 12*8
    }

    [Fact]
    public void FillPosture_force_puts_all_spend_in_force()
    {
        var result = AptitudeAutoAssign.FillPosture(40, Posture.Force);
        Assert.True(result.Ok);
        var forceIds = AptitudeCatalog.All.Where(a => a.Posture == Posture.Force).Select(a => a.Id).ToHashSet();
        foreach (var (id, share) in result.Shares)
        {
            if (forceIds.Contains(id)) Assert.Equal(10, share);
            else Assert.Equal(0, share);
        }
        Assert.Equal(0, result.Leftover);
    }

    [Fact]
    public void FillFromPermille_empty_favour_refuses_S7()
    {
        var result = AptitudeAutoAssign.FillFromPermille(100, new Dictionary<string, long>());
        Assert.False(result.Ok);
        Assert.Equal("autoAssign.favour.empty", result.Reason);
    }

    [Fact]
    public void Fill_species_favour_uses_materialize_math()
    {
        var favour = AptitudeCatalog.All.ToDictionary(a => a.Id, _ => 0L, StringComparer.Ordinal);
        // 1000 across Might only is invalid for ValidateTargetPermilleSum needing twelve rows sum 1000 —
        // give each ~83 with remainder on Might.
        long assigned = 0;
        foreach (var apt in AptitudeCatalog.All)
        {
            favour[apt.Id] = 83;
            assigned += 83;
        }
        favour["Might"] = 83 + (1000 - assigned);

        var result = AptitudeAutoAssign.Fill(
            AptitudeAutoAssignRules.SpeciesFavour, 1000, favourPermille: favour);
        Assert.True(result.Ok, result.Reason);
        Assert.True(result.Leftover >= 0);
        Assert.True(result.Shares.Values.Sum() + result.Leftover == 1000);
    }

    [Fact]
    public void Fill_active_preset_missing_rows_refuses()
    {
        var result = AptitudeAutoAssign.Fill(AptitudeAutoAssignRules.ActivePreset, 100);
        Assert.False(result.Ok);
        Assert.Equal("autoAssign.activePreset.missing", result.Reason);
    }

    [Fact]
    public void Fill_unknown_rule_refuses()
    {
        var result = AptitudeAutoAssign.Fill("nope", 10);
        Assert.False(result.Ok);
        Assert.Equal("autoAssign.rule.unknown", result.Reason);
    }

    [Fact]
    public void Fill_species_favour_refused_when_Mode_C()
    {
        var favour = AptitudeCatalog.All.ToDictionary(a => a.Id, _ => 83L, StringComparer.Ordinal);
        favour["Might"] = 83 + (1000 - 83 * 12);
        var result = AptitudeAutoAssign.Fill(
            AptitudeAutoAssignRules.SpeciesFavour, 1000, favourPermille: favour, favourAllowed: false);
        Assert.False(result.Ok);
        Assert.Equal("autoAssign.favour.modeC", result.Reason);
        Assert.Empty(result.Shares);
    }

    [Fact]
    public void Fill_active_preset_uses_D13_materialize_leftover_legal()
    {
        var rows = AptitudeCatalog.All.Select(a => new AptitudePresetRowSpec(a.Id, 83)).ToList();
        var might = rows.FindIndex(r => r.AptitudeId == "Might");
        rows[might] = rows[might] with { TargetPermille = 83 + (1000 - 83 * 12) };

        var result = AptitudeAutoAssign.Fill(
            AptitudeAutoAssignRules.ActivePreset, 100, activePresetRows: rows);
        Assert.True(result.Ok, result.Reason);
        Assert.True(result.Leftover >= 0);
        Assert.Equal(100, result.Shares.Values.Sum() + result.Leftover);
    }

    [Fact]
    public void FillEven_uses_long_budget_math_S6()
    {
        // Large budget — would overflow int if multiplied as int before cast.
        const long budget = 2_000_000_000L;
        var result = AptitudeAutoAssign.FillEven(budget);
        Assert.True(result.Ok);
        Assert.Equal(budget, result.Shares.Values.Sum() + result.Leftover);
    }
}
