using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Contracts;
using FusionRpg.Core.Demons.Fusion;
using FusionRpg.Core.Demons.Patron;
using Xunit;

namespace FusionRpg.Core.Tests.Demons;

/// <summary>
/// seed-to-concrete T4.1 (spec-rarity-migration.md §5, §6, §7 step 4, Testing strategy) — reads the
/// REAL shipped <c>data/tuning/*.json</c> files (not a mock bootstrap) so these checks fail the moment
/// a future edit to the actual balance surface breaks ten-rung coverage, the 1000‰ summon-rate
/// invariant, or the "no rung strictly worse than the one below" shape.
/// </summary>
public class RarityTuningCoverageTests
{
    static string TuningDir => Path.Combine(FindRepoRoot(), "data", "tuning");
    static string Read(string name) => File.ReadAllText(Path.Combine(TuningDir, name));

    static readonly FusionTuning Fusion = FusionTuningLoader.Parse(Read("fusion.v1.json"));
    static readonly ContractTuning Contracts = ContractTuningLoader.Parse(Read("contracts.v1.json"));
    static readonly SoulEarnTuning Souls = SoulEarnTuningLoader.Parse(Read("souls.v1.json"));
    static readonly PatronTuning Patron = PatronTuningLoader.Parse(Read("patron.v1.json"));
    static readonly SummoningTuning Summoning = SummoningTuningLoader.Parse(Read("summoning.v1.json"));

    [Fact]
    public void Every_rarity_keyed_tuning_table_has_ten_entries()
    {
        Assert.Equal(10, Fusion.StarCap.Count);
        Assert.Equal(10, Fusion.SlotsByRarity.Count);
        Assert.Equal(10, Contracts.BaseUpkeepPerDay.Count);
        Assert.Equal(10, Contracts.RitualPriceSouls.Count);
        Assert.Equal(10, Souls.DiscoveryDelta.Count);
        Assert.Equal(10, Patron.RarityBaseMilli.Count);

        foreach (var rung in DemonRarityLadder.All)
        {
            Assert.True(Fusion.StarCap.ContainsKey(rung), $"StarCap missing {rung}");
            Assert.True(Fusion.SlotsByRarity.ContainsKey(rung), $"SlotsByRarity missing {rung}");
            Assert.True(Contracts.BaseUpkeepPerDay.ContainsKey(rung), $"BaseUpkeepPerDay missing {rung}");
            Assert.True(Contracts.RitualPriceSouls.ContainsKey(rung), $"RitualPriceSouls missing {rung}");
            Assert.True(Souls.DiscoveryDelta.ContainsKey(rung), $"DiscoveryDelta missing {rung}");
            Assert.True(Patron.RarityBaseMilli.ContainsKey(rung), $"RarityBaseMilli missing {rung}");
        }

        // RecipeCost is the one NAMED exception (spec §7 step 4 / DemonRecipeCatalog.OutputEligibilityFloor):
        // it only covers rungs a fusion OUTPUT can occupy — Cultivated..Almanac, seven rungs, not ten.
        Assert.Equal(
            DemonRarityLadder.RungCount - (int)DemonRecipeCatalog.OutputEligibilityFloor,
            Fusion.RecipeCost.Count);
        foreach (var rung in DemonRarityLadder.All.Where(r => DemonRarityLadder.AtLeast(r, DemonRecipeCatalog.OutputEligibilityFloor)))
            Assert.True(Fusion.RecipeCost.ContainsKey(rung), $"RecipeCost missing {rung}");
    }

    /// <summary>The roller's NATURAL (no hard pity, no soft-ramp — pull 0) distribution must still sum
    /// to exactly 1000‰: eight named per-mille rates plus Sunwoven's un-ramped base rate, with Chaff
    /// as the implicit remainder (spec §5: "ten rates must still sum to 1000‰"). The top rung must be
    /// naturally reachable, not only via hard pity (Q15's own point: "a naive spread makes the top
    /// rung a rounding error").</summary>
    [Fact]
    public void Summon_rates_sum_to_1000_permille()
    {
        var r = Summoning.Roller;
        var namedSum = r.AlmanacPerMille + r.SunwovenBasePerMille + r.HeirloomPerMille + r.FirstseedPerMille +
                       r.ChimericPerMille + r.FusedPerMille + r.CultivatedPerMille + r.GraftedPerMille +
                       r.SproutPerMille;
        Assert.True(namedSum > 0 && namedSum < 1000,
            $"named rates must leave room for Chaff's implicit remainder, got sum={namedSum}");
        var chaffShare = 1000 - namedSum;
        Assert.True(chaffShare > 0, "Chaff's implicit remainder must be positive");
        Assert.Equal(1000, namedSum + chaffShare);

        Assert.True(r.AlmanacPerMille > 0, "the true top rung must be naturally reachable, not pity-only");
    }

    /// <summary>ssot-rarity.md §8.6's named failure mode: interpolating StarCap/SlotsByRarity/RecipeCost
    /// naively across ten rungs can make a mid rung strictly worse than the rung directly below it —
    /// a lower star cap, fewer trait slots, or (for a fusion output) a lower soul cost for a supposedly
    /// better result. Every one of the three tables must be non-decreasing up the ladder.</summary>
    [Fact]
    public void No_rung_is_strictly_worse_than_the_one_below()
    {
        var rungs = DemonRarityLadder.All;
        for (var i = 1; i < rungs.Count; i++)
        {
            var below = rungs[i - 1];
            var here = rungs[i];
            Assert.True(Fusion.StarCap[here] >= Fusion.StarCap[below],
                $"StarCap regressed: {here}={Fusion.StarCap[here]} < {below}={Fusion.StarCap[below]}");
            Assert.True(Fusion.SlotsByRarity[here] >= Fusion.SlotsByRarity[below],
                $"SlotsByRarity regressed: {here}={Fusion.SlotsByRarity[here]} < {below}={Fusion.SlotsByRarity[below]}");
        }

        // RecipeCost only exists for Cultivated..Almanac — souls must climb monotonically up that
        // sub-range (a higher-rung fusion output must never cost fewer souls than a lower one).
        var recipeRungs = rungs.Where(r => Fusion.RecipeCost.ContainsKey(r)).ToList();
        for (var i = 1; i < recipeRungs.Count; i++)
        {
            var below = recipeRungs[i - 1];
            var here = recipeRungs[i];
            Assert.True(Fusion.RecipeCost[here].Souls >= Fusion.RecipeCost[below].Souls,
                $"RecipeCost souls regressed: {here}={Fusion.RecipeCost[here].Souls} < {below}={Fusion.RecipeCost[below].Souls}");
        }
    }

    /// <summary>spec §6 / Q15: the pity guard fields must name the RUNG they guard, not a leftover
    /// four-value name ("epic"/"legendary") that would leave a reader guessing which rarity it means
    /// after the migration.</summary>
    [Fact]
    public void Pity_guards_name_their_rungs()
    {
        var names = typeof(RollerTuning).GetProperties().Select(p => p.Name).ToList();
        Assert.Contains("HeirloomHardPity", names);
        Assert.Contains("SunwovenSoftStart", names);
        Assert.Contains("SunwovenHardPity", names);
        Assert.DoesNotContain(names, n => n.Contains("Epic", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, n => n.Contains("Legendary", StringComparison.OrdinalIgnoreCase));

        // spec §6: the two guards sit at ssot-rarity.md §3.3's own ordinal scale — rung index * 10,
        // one-based — where Heirloom = 70 and Sunwoven = 90. The C# enum's own (zero-based)
        // declaration-order value is a DIFFERENT scale (DemonRarityLadder's internal one); this
        // checks the mapping between the two holds, not that either scale's raw number is 70/90.
        Assert.Equal(70, ((int)DemonRarity.Heirloom + 1) * 10);
        Assert.Equal(90, ((int)DemonRarity.Sunwoven + 1) * 10);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }
}
