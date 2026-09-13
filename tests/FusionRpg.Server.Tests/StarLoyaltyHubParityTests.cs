using FusionRpg.Core.Battle;
using FusionRpg.Core.Creatures.Contracts;
using FusionRpg.Core.Creatures.Fusion;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Core.Stats.Derived.Subsystems;
using FusionRpg.Server;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// channelmods-hub T1 parity — Star/Loyalty moved out of private <c>BattleChannelMod</c> arithmetic
/// and into one shared Core formula (<see cref="StarLoyaltyBonus"/>) whose Hub twin
/// (<see cref="StarLoyaltySubsystem"/>) the sheet consumes. These tests pin both halves:
/// the adapter still produces the pre-migration numbers, and the Hub subsystem reaches the same
/// totals on <c>combat.power.omni</c> / <c>combat.defense.omni</c> with attributed GG-49 ids.
/// </summary>
public class StarLoyaltyHubParityTests
{
    static void ConfigureFusionAndContracts()
    {
        var dir = Path.Combine(FindRepoRoot(), "data", "tuning");
        StarPolicy.Configure(FusionTuningLoader.Parse(File.ReadAllText(Path.Combine(dir, "fusion.v2.json"))));
        ContractPolicy.Configure(ContractTuningLoader.Parse(File.ReadAllText(Path.Combine(dir, "contracts.v1.json"))));
    }

    [Fact]
    public void SharedFormula_matches_pre_migration_adapter_for_star_and_loyalty()
    {
        ConfigureFusionAndContracts();

        // The adapter IS the shared formula now -- proving it against the historical expression
        // independently (not calling the adapter twice) is the point: the literal arithmetic here is
        // copied from the deleted WebMatchService bodies.
        for (var star = 1; star <= StarPolicy.MaxStar; star++)
        {
            var level = 33;
            var expectedPower = Math.Max(star, BattleRuleset.BaseAtk(level) * StarPolicy.StarPowerMilli(star) / 1000);
            var expectedDefense = Math.Max(star, BattleRuleset.BaseDefense(level) * StarPolicy.StarDefenseMilli(star) / 1000);

            var mods = WebMatchService.StarChannelMods(star, level);
            Assert.Equal(2, mods.Count);
            Assert.Equal(expectedPower, mods[0].Amount);
            Assert.Equal(expectedDefense, mods[1].Amount);
        }

        // Loyalty, across every rank band including the +0 Bound band.
        foreach (var loyalty in new[] { 0, 200, 300, 400, 600, 800, 1000 })
        {
            var level = 33;
            var rank = ContractPolicy.RankFor(loyalty);
            var milli = ContractPolicy.RankBonusMilli(rank);
            var expected = milli <= 0
                ? Array.Empty<(string, long)>()
                : new[]
                {
                    (DerivedStatChannels.CombatPowerOmni,
                        Math.Max((long)rank - 1, BattleRuleset.BaseAtk(level) * milli / 1000)),
                    (DerivedStatChannels.CombatDefenseOmni,
                        Math.Max((long)rank - 1, BattleRuleset.BaseDefense(level) * milli / 1000))
                };

            var mods = WebMatchService.LoyaltyChannelMods(loyalty, level);
            Assert.Equal(expected.Length, mods.Count);
            for (var i = 0; i < expected.Length; i++)
            {
                Assert.Equal(expected[i].Item1, mods[i].ChannelId);
                Assert.Equal(expected[i].Item2, mods[i].Amount);
            }
        }
    }

    [Fact]
    public void HubSubsystem_reaches_same_channel_totals_with_attributed_source_ids()
    {
        ConfigureFusionAndContracts();
        const int level = 33;
        const int star = 3;
        const int loyalty = 800; // Devoted -- pays.

        var hub = ActorHubBootstrap.CreateDefault(
            starLoyalty: _ => new StarLoyaltyContribution(star, loyalty, level));
        var (snapshot, bag) = hub.ResolveDerivedWithContributions(
            new StatContextFactory().ForPlant("t1-parity", new EntityBaseline { Atk = BattleRuleset.BaseAtk(level) }));

        var starBonus = StarLoyaltyBonus.Star(star, level)!.Value;
        var loyaltyBonus = StarLoyaltyBonus.Loyalty(loyalty, level)!.Value;

        Assert.Equal(
            starBonus.Power + loyaltyBonus.Power,
            snapshot.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        Assert.Equal(
            starBonus.Defense + loyaltyBonus.Defense,
            snapshot.Get(DerivedStatChannels.CombatDefenseOmni, 0), 6);

        var powerSources = bag.ContributionsFor(DerivedStatChannels.CombatPowerOmni)
            .Select(c => c.SourceId).ToList();
        Assert.Contains($"grant:star:{star}", powerSources);
        Assert.Contains($"grant:loyalty:{loyalty}", powerSources);
        foreach (var s in powerSources)
            Assert.False(string.IsNullOrWhiteSpace(s), "GG-49: no unattributed contribution");

        // Channel totals equal the sum of the adapter's two mod groups -- the parity claim.
        var adapterTotal = WebMatchService.StarChannelMods(star, level)
            .Concat(WebMatchService.LoyaltyChannelMods(loyalty, level))
            .Where(m => m.ChannelId == DerivedStatChannels.CombatPowerOmni)
            .Sum(m => m.Amount);
        Assert.Equal(adapterTotal, (long)snapshot.Get(DerivedStatChannels.CombatPowerOmni, 0));
    }

    [Fact]
    public void HubSubsystem_is_inert_for_zero_star_and_bound_loyalty_without_configuration()
    {
        // No StarPolicy/ContractPolicy.Configure call: 0 stars and the Bound band must contribute
        // nothing and never read the tuning hubs -- so bare sheet resolves stay green.
        var hub = ActorHubBootstrap.CreateDefault(
            starLoyalty: _ => new StarLoyaltyContribution(0, 0, 10));
        var (snapshot, bag) = hub.ResolveDerivedWithContributions(
            new StatContextFactory().ForPlant("t1-inert", new EntityBaseline()));

        Assert.Equal(0.0, snapshot.Get(DerivedStatChannels.CombatPowerOmni, 0), 6);
        Assert.Equal(0.0, snapshot.Get(DerivedStatChannels.CombatDefenseOmni, 0), 6);
        Assert.Empty(bag.ContributionsFor(DerivedStatChannels.CombatPowerOmni));
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
