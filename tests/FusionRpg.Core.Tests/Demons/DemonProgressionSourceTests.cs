using FusionRpg.Core.Demons;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

public sealed class DemonProgressionSourceTests
{
    [Fact]
    public void EmpireGeneral_roundTripsWithStableScopeKey()
    {
        var source = DemonProgressionSource.EmpireGeneral("  imp  ");

        Assert.Equal("demon.progression.v1", source.Kind);
        Assert.Equal("general:imp", source.Id);
        Assert.Equal("imp", source.ScopeKey);
        Assert.Equal(source, DemonProgressionSource.Parse(source.Kind, source.Id));
    }

    [Fact]
    public void UniqueSpecimen_requiresInstanceAndOccurrenceIdentity()
    {
        var source = DemonProgressionSource.UniqueSpecimen("specimen-7", "occ-42");

        Assert.Equal("demon.progression.v1", source.Kind);
        Assert.Equal("unique:specimen-7:occ-42", source.Id);
        Assert.Equal("specimen-7", source.ScopeKey);
        Assert.Equal(source, DemonProgressionSource.Parse(source.Kind, source.Id));
    }

    [Fact]
    public void Commander_roundTrips()
    {
        var source = DemonProgressionSource.Commander("crazy-dave");

        Assert.Equal("demon.progression.v1", source.Kind);
        Assert.Equal("commander:crazy-dave", source.Id);
        Assert.Equal("crazy-dave", source.ScopeKey);
        Assert.Equal(source, DemonProgressionSource.Parse(source.Kind, source.Id));
    }

    [Theory]
    [InlineData("demon.progression.v1", "unique:specimen:occ:extra")]
    [InlineData("demon.progression.v1", "unique:specimen")]
    [InlineData("demon.progression.v1", "general:imp:extra")]
    [InlineData("demon.progression.v1", "general:")]
    [InlineData("unknown", "anything")]
    public void Parse_rejectsWrongGrammar(string kind, string id)
    {
        Assert.Throws<FormatException>(() => DemonProgressionSource.Parse(kind, id));
    }

    [Fact]
    public void Parse_rejectsSeparatorsInsideIdentity()
    {
        Assert.Throws<ArgumentException>(() => DemonProgressionSource.UniqueSpecimen("spec:imen", "occ"));
        Assert.Throws<ArgumentException>(() => DemonProgressionSource.Commander(""));
    }
}
