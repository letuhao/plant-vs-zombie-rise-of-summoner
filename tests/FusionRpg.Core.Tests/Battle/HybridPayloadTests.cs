using FusionRpg.Core.Battle;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Battle;

/// <summary>
/// combat-unification **Wave E3** — the secondary element as a weighted payload component.
///
/// <para>The mechanism ships <b>inert</b>: `hybrid.secondaryWeightMilli` is 0, so the payload is the
/// single full-weight primary component it was before E3, and every golden is unmoved. The weight is
/// a <b>tunable</b> rather than the hardcoded policy constant the map sketched (0.7/0.3), for two
/// reasons stated here so the choice is not mistaken for an oversight: the todo marks that constant
/// <b>ask-first</b>, and picking it is not free — it <b>moves the expedition goldens</b>, because wave
/// demons carry a real `ElementSecondary` (`WaveCatalog.cs:115`) while the hand-built battle goldens
/// do not.</para>
/// </summary>
public class HybridPayloadTests
{
    const ElementTypeId Fire = ElementTypeId.Fire;
    const ElementTypeId Ice = ElementTypeId.Ice;

    // ---- inert at the shipped default ----

    [Fact]
    public void AtWeightZeroThePayloadIsTheSingleFullWeightPrimary_evenWithASecondary()
    {
        var withSecondary = HybridPayload.Build(Fire, Ice, secondaryWeightMilli: 0);
        var without = HybridPayload.Build(Fire, null, secondaryWeightMilli: 0);

        var one = Assert.Single(withSecondary);
        Assert.Equal(Fire, one.Element);
        Assert.Equal(1.0, one.Weight);

        // Identical shape either way: a zero-weight second component must never reach the resolver.
        Assert.Equal(without.Length, withSecondary.Length);
    }

    [Fact]
    public void TheShippedTuningLeavesItInert()
    {
        Assert.Equal(0, BattleRuleset.HybridSecondaryWeightMilli);
    }

    [Fact]
    public void NoPrimaryMeansAnEmptyPayload()
    {
        Assert.Empty(HybridPayload.Build(null, Ice, 300));
        Assert.Empty(HybridPayload.Build(null, null, 0));
    }

    // ---- the mechanism, when the dial is on ----

    [Fact]
    public void ANonZeroWeightSplitsThePayloadAndTheWeightsSumToOne()
    {
        var payload = HybridPayload.Build(Fire, Ice, secondaryWeightMilli: 300);

        Assert.Equal(2, payload.Length);
        Assert.Equal(Fire, payload[0].Element);
        Assert.Equal(Ice, payload[1].Element);
        Assert.Equal(0.7, payload[0].Weight, precision: 10);
        Assert.Equal(0.3, payload[1].Weight, precision: 10);
        Assert.Equal(1.0, payload[0].Weight + payload[1].Weight, precision: 10);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(250)]
    [InlineData(500)]
    [InlineData(999)]
    [InlineData(1000)]
    public void TheWeightsAlwaysSumToExactlyOne(int weightMilli)
    {
        var payload = HybridPayload.Build(Fire, Ice, weightMilli);
        Assert.Equal(1.0, payload.Sum(c => c.Weight), precision: 10);
    }

    /// <summary>A secondary equal to the primary is not a hybrid. The engine normalises that one line
    /// before it calls this, so the payload builder is handed a null — asserted here so the two cannot
    /// drift into disagreeing about the rule.</summary>
    [Fact]
    public void ASecondaryIdenticalToThePrimaryIsNotAHybrid()
    {
        var payload = HybridPayload.Build(Fire, null, secondaryWeightMilli: 300);
        Assert.Single(payload);
        Assert.Equal(1.0, payload[0].Weight);
    }

    // ---- the bound is structural, and refuses ----

    [Theory]
    [InlineData(-1)]
    [InlineData(1001)]
    [InlineData(int.MaxValue)]
    public void AShareOutsideZeroToOneThousandIsRefused(int bad)
    {
        // Not a progression cap (PS-8): above 1000 the PRIMARY takes a negative weight, which is a
        // nonsense payload rather than an aggressive balance choice.
        Assert.Throws<ArgumentOutOfRangeException>(() => HybridPayload.Build(Fire, Ice, bad));
    }

    [Fact]
    public void TheLoaderRefusesAnOutOfRangeShareToo()
    {
        static string Doc(int w) =>
            "{\"schemaVersion\":2,\"version\":2," +
            "\"ruleset\":{\"roundDurationMs\":1000,\"maxRounds\":50}," +
            "\"statComposer\":{\"primaryAffinityDivisor\":4,\"secondaryAffinityDivisor\":8}," +
            "\"timeline\":{\"profiles\":{\"classic-round\":{\"w\":1,\"wReact\":0,\"passQuantum\":1}}}," +
            "\"hybrid\":{\"secondaryWeightMilli\":" + w + "}," +
            "\"traits\":{}}";

        Assert.Throws<BattleTuningRejection>(() => BattleTuningLoader.Parse(Doc(1001)));
        Assert.Throws<BattleTuningRejection>(() => BattleTuningLoader.Parse(Doc(-1)));

        var ok = BattleTuningLoader.Parse(Doc(300));
        Assert.Equal(300, ok.HybridSecondaryWeightMilli);
    }
}
