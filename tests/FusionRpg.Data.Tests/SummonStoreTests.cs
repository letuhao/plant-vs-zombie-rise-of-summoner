using FusionRpg.Core.Demons;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

public class SummonStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public SummonStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-summon-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _store.AwardSouls(1, 10_000, SoulEarnPolicy.Reasons.Seed, "test-bankroll");
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    [Fact]
    public void Ten_pull_mints_spends_and_logs_atomically()
    {
        var (ok, _, outcome) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-ten", 42, null);
        Assert.True(ok);
        Assert.Equal(10, outcome!.Specimens.Count);
        Assert.Equal(10, _store.ListDemonRoster(1).Items.Count);
        Assert.True(_store.ListDemonCodex(1).Entries.Count >= 1);
        // Spent 900; discovery rewards may add back — assert via totals.
        Assert.Equal(900, _store.GetSoulBalance(1).SpentTotal);
        Assert.True(outcome.Pity.PullsSinceLegendary <= 10);
        Assert.Equal(outcome.Pity, _store.GetSummonPity(1));
    }

    [Fact]
    public void Replay_returns_stored_results_without_spending()
    {
        var (_, _, first) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-replay", 7, null);
        var balanceAfter = _store.GetSoulBalance(1).Balance;
        var (ok, reason, replay) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-replay", 999, null);
        Assert.True(ok);
        Assert.Equal("replay", reason);
        Assert.True(replay!.Replayed);
        Assert.Equal(first!.Specimens.Select(s => s.Profile.InstanceId), replay.Specimens.Select(s => s.Profile.InstanceId));
        Assert.Equal(balanceAfter, _store.GetSoulBalance(1).Balance);
        Assert.Equal(10, _store.ListDemonRoster(1).Items.Count); // no extra mints
    }

    [Fact]
    public void Replay_with_mismatched_request_is_refused()
    {
        _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-mismatch", 7, null);
        var (ok, reason, _) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 1, "c-mismatch", 7, null);
        Assert.False(ok);
        Assert.Equal("correlation.mismatch", reason);
    }

    [Fact]
    public void Overdraft_refusal_writes_nothing()
    {
        _store.TrySpendSouls(1, 9_950, SoulEarnPolicy.Reasons.Summon, "drain"); // leave 50
        var (ok, reason, _) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 1, "c-broke", 7, null);
        Assert.False(ok);
        Assert.Equal("souls.insufficient", reason);
        Assert.Empty(_store.ListDemonRoster(1).Items);
        Assert.Equal(PityState.Fresh, _store.GetSummonPity(1));
        Assert.Equal(50, _store.GetSoulBalance(1).Balance);
    }

    [Fact]
    public void Mid_pull_failure_rolls_back_everything()
    {
        _store.SummonMidPullTestHook = () => throw new InvalidOperationException("boom");
        try
        {
            Assert.Throws<InvalidOperationException>(() =>
                _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-crash", 42, null));
        }
        finally
        {
            _store.SummonMidPullTestHook = null;
        }

        // Atomic-or-nothing: no specimens, no codex, no pity, no log, spend rolled back.
        Assert.Empty(_store.ListDemonRoster(1).Items);
        Assert.Empty(_store.ListDemonCodex(1).Entries);
        Assert.Equal(PityState.Fresh, _store.GetSummonPity(1));
        Assert.Equal(10_000, _store.GetSoulBalance(1).Balance);
        var (ok, _, outcome) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-crash", 42, null);
        Assert.True(ok); // correlation was never logged, so the retry runs fresh
        Assert.False(outcome!.Replayed);
    }

    [Fact]
    public void Discovery_rewards_pay_once_per_species()
    {
        var (_, _, a) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-d1", 42, null);
        var (_, _, b) = _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-d2", 42, null);
        // Identical seed + pity differences aside: any species discovered in pull 1 pays nothing in pull 2.
        var speciesInBoth = a!.Specimens.Select(s => s.Profile.SpeciesId)
            .Intersect(b!.Specimens.Select(s => s.Profile.SpeciesId)).ToList();
        Assert.NotEmpty(speciesInBoth);
        var ledger = _store.ListSoulLedger(1, 200);
        var discoveryRows = ledger.Items.Where(i => i.Reason == SoulEarnPolicy.Reasons.Discovery && i.RefKind == "species").ToList();
        Assert.Equal(discoveryRows.Select(d => d.RefId).Distinct().Count(), discoveryRows.Count);
    }

    [Fact]
    public void Pity_counters_accumulate_across_pulls_and_banners()
    {
        _store.ExecuteSummon(1, SummonBannerCatalog.StandardRift, 10, "c-p1", 1, null);
        var afterTen = _store.GetSummonPity(1);
        _store.ExecuteSummon(1, SummonBannerCatalog.ElementFocus, 1, "c-p2", 2, "fire");
        var afterEleven = _store.GetSummonPity(1);
        // Counter either advanced by one or reset on a hit — never unchanged-and-nonzero drifting.
        Assert.True(afterEleven.PullsSinceLegendary == afterTen.PullsSinceLegendary + 1
                    || afterEleven.PullsSinceLegendary == 0);
    }

    [Fact]
    public void Focus_banner_requires_a_valid_focus_element()
    {
        var (ok, reason, _) = _store.ExecuteSummon(1, SummonBannerCatalog.ElementFocus, 1, "c-f", 7, "42");
        Assert.False(ok);
        Assert.Equal("focus.unknown", reason);
    }
}
