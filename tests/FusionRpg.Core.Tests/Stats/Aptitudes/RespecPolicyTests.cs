using FusionRpg.Core.Stats.Aptitudes;
using Xunit;

namespace FusionRpg.Core.Tests.Stats.Aptitudes;

/// <summary>class-system-todo.md P6.3 — <see cref="RespecPolicy"/> (spec-point-economy.md §3, read in
/// full this session). Table in §7: test 6 covered here — never refused, never free, no cooldown.</summary>
public class RespecPolicyTests
{
    static string FindShippedAptitudesTuningPath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "tuning", "aptitudes.v2.json");
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not locate data/tuning/aptitudes.v2.json above " + AppContext.BaseDirectory);
    }

    static AptitudeTuning ShippedTuning() =>
        AptitudeTuningLoader.Parse(File.ReadAllText(FindShippedAptitudesTuningPath()));

    [Fact]
    public void PriceOf_isNeverFree()
    {
        // spec-point-economy.md §3: "Then a build is a menu selection, not a commitment" -- Amount
        // must be strictly positive, enforced at the loader boundary (PositiveMilli), reconfirmed here
        // from RespecPolicy's own testing table rather than trusted from the parser alone.
        var price = RespecPolicy.PriceOf(ShippedTuning());
        Assert.True(price.Amount > 0, $"expected a strictly positive respec price, got {price.Amount}");
    }

    [Fact]
    public void PriceOf_isNeverRefused_returnsUnconditionally()
    {
        // "Always available" -- PriceOf has no "cannot respec right now" return path at all: calling
        // it always succeeds and always returns a price, for any valid tuning. There is no separate
        // "CanRespec" gate to accidentally wire as a refusal.
        var tuning = ShippedTuning();
        var first = RespecPolicy.PriceOf(tuning);
        var second = RespecPolicy.PriceOf(tuning);
        var third = RespecPolicy.PriceOf(tuning);

        Assert.Equal(first, second);
        Assert.Equal(second, third); // calling it repeatedly never starts failing -- no hidden cooldown state.
    }

    [Fact]
    public void PriceOf_pricesTheSameResourceEveryTime_noHiddenCooldownOrEscalation()
    {
        // "Never a cooldown" (§3/§8) -- nothing about the price should escalate or change between
        // calls; RespecPolicy carries no mutable state that a cooldown or an increasing tax would need.
        var tuning = ShippedTuning();
        var price = RespecPolicy.PriceOf(tuning);

        Assert.Equal(RespecResource.Hunger, price.Resource);
        Assert.Equal(tuning.PointEconomy.RespecPrice, price.Amount);
    }

    [Fact]
    public void PriceOf_nullTuning_throws()
    {
        Assert.Throws<ArgumentNullException>(() => RespecPolicy.PriceOf(null!));
    }
}
