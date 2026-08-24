using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

public class SoulStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public SoulStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-souls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    EventEnvelope Ev(string kind, string matchKey, object payload) => new()
    {
        T = DateTime.UtcNow.ToString("o"),
        Game = RpgConstants.GameId,
        Kind = kind,
        MatchKey = matchKey,
        Payload = payload
    };

    void PlayMatch(string matchKey, int kills, string result)
    {
        var events = new List<EventEnvelope> { Ev("board.start", matchKey, new { levelName = "souls" }) };
        for (var i = 0; i < kills; i++)
            events.Add(Ev("zombie.die", matchKey, new { ptr = $"0xz{matchKey[..4]}{i}", type = 0 }));
        events.Add(Ev("match.result", matchKey, new { result }));
        events.Add(Ev("board.end", matchKey, new { levelName = "souls" }));
        _store.InsertEvents(events);
    }

    [Fact]
    public void Kills_and_victory_earn_policy_exact_totals()
    {
        PlayMatch(Guid.NewGuid().ToString("N"), 30, "victory");
        var b = _store.GetSoulBalance(1);
        Assert.Equal(30 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.VictoryDelta, b.Balance);
        Assert.Equal(b.Balance, b.EarnedTotal);
    }

    [Fact]
    public void Kill_earns_are_uncapped_past_the_old_fifty_per_match_boundary()
    {
        // T3.6 (spec-caps-reconcile.md §2.3, SSOT §11.7, 2026-08-24): KillCapPerMatch is deleted --
        // all 80 kills earn, not just the first 50 (the exact old boundary this scenario used to sit
        // on). Every earn here reads at the pin (RpgStore.Souls.cs's VanillaPvzKillAndRunTheta), so
        // it is still KillDelta flat per kill, just without the plateau.
        PlayMatch(Guid.NewGuid().ToString("N"), 80, "defeat");
        var b = _store.GetSoulBalance(1);
        Assert.Equal(80 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.DefeatDelta, b.Balance);
    }

    [Fact]
    public void Victory_bonus_no_longer_decays_after_the_old_third_win_of_day_boundary()
    {
        // T3.6: VictoryFullPerDay is deleted (audit F11 -- "a wall-clock throttle... refuses nothing").
        // The 4th and 5th win of the day now pay the same full VictoryDelta as the first three.
        for (var i = 0; i < 5; i++)
            PlayMatch(Guid.NewGuid().ToString("N"), 0, "victory");
        var b = _store.GetSoulBalance(1);
        Assert.Equal(5 * SoulEarnPolicy.VictoryDelta, b.Balance);
    }

    [Fact]
    public void Double_ingest_earns_once()
    {
        var matchKey = Guid.NewGuid().ToString("N");
        var kill = Ev("zombie.die", matchKey, new { ptr = "0xdup", type = 0 });
        _store.InsertEvents(new[] { Ev("board.start", matchKey, new { levelName = "dup" }), kill });
        var before = _store.GetSoulBalance(1).Balance;
        _store.InsertEvents(new[] { kill }); // same ptr → same fact dedupe → no new fact → no new earn
        Assert.Equal(before, _store.GetSoulBalance(1).Balance);
    }

    [Fact]
    public void Spend_is_atomic_refuses_overdraft_and_replays_idempotently()
    {
        _store.AwardSouls(1, 500, SoulEarnPolicy.Reasons.Discovery, "seed");

        var (ok, _, bal) = _store.TrySpendSouls(1, 300, SoulEarnPolicy.Reasons.Summon, "corr-1");
        Assert.True(ok);
        Assert.Equal(200, bal.Balance);

        var (again, reason2, bal2) = _store.TrySpendSouls(1, 300, SoulEarnPolicy.Reasons.Summon, "corr-1");
        Assert.True(again);
        Assert.Equal("replay", reason2);
        Assert.Equal(200, bal2.Balance); // replay spends nothing

        var (over, reason3, bal3) = _store.TrySpendSouls(1, 999, SoulEarnPolicy.Reasons.Summon, "corr-2");
        Assert.False(over);
        Assert.Equal("souls.insufficient", reason3);
        Assert.Equal(200, bal3.Balance); // refusal writes nothing

        Assert.Equal(500, _store.GetSoulBalance(1).EarnedTotal);
        Assert.Equal(300, _store.GetSoulBalance(1).SpentTotal);
    }

    [Fact]
    public void Awards_are_idempotent_on_dedupe_key()
    {
        var (first, _) = _store.AwardSouls(1, 75, SoulEarnPolicy.Reasons.Discovery, "species:hell-hound");
        var (second, bal) = _store.AwardSouls(1, 75, SoulEarnPolicy.Reasons.Discovery, "species:hell-hound");
        Assert.True(first);
        Assert.False(second);
        Assert.Equal(75, bal.Balance);
    }

    [Fact]
    public void Ledger_lists_newest_first_and_balance_watermark_advances()
    {
        _store.AwardSouls(1, 10, SoulEarnPolicy.Reasons.Discovery, "a");
        _store.AwardSouls(1, 20, SoulEarnPolicy.Reasons.Discovery, "b");
        var ledger = _store.ListSoulLedger(1);
        Assert.Equal(2, ledger.Items.Count);
        Assert.True(ledger.Items[0].Id > ledger.Items[1].Id);
        Assert.True(_store.GetSoulBalance(1).Revision >= 2);
    }

    [Fact]
    public void Ledger_pagination_walks_older_history()
    {
        _store.AwardSouls(1, 10, SoulEarnPolicy.Reasons.Discovery, "p1");
        _store.AwardSouls(1, 20, SoulEarnPolicy.Reasons.Discovery, "p2");
        _store.AwardSouls(1, 30, SoulEarnPolicy.Reasons.Discovery, "p3");
        var page1 = _store.ListSoulLedger(1, limit: 2);
        Assert.Equal(2, page1.Items.Count);
        var page2 = _store.ListSoulLedger(1, limit: 2, afterId: page1.Items[^1].Id);
        // Regression (review): with the old `id > after` + DESC, this returned page 1 again.
        Assert.Single(page2.Items);
        Assert.True(page2.Items[0].Id < page1.Items[^1].Id);
    }

    [Fact]
    public void Spend_replay_with_a_different_amount_is_refused()
    {
        _store.AwardSouls(1, 500, SoulEarnPolicy.Reasons.Seed, "bank");
        var (ok1, _, _) = _store.TrySpendSouls(1, 100, "ritual", "corr-r");
        Assert.True(ok1);
        var (ok2, reason, bal) = _store.TrySpendSouls(1, 200, "ritual", "corr-r");
        Assert.False(ok2);
        Assert.Equal("correlation.mismatch", reason);
        Assert.Equal(400, bal.Balance); // nothing double-spent
        var (ok3, replay, _) = _store.TrySpendSouls(1, 100, "ritual", "corr-r");
        Assert.True(ok3);
        Assert.Equal("replay", replay);
    }

    [Fact]
    public void Awards_beyond_the_balance_cap_are_rejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _store.AwardSouls(1, RpgStore.MaxSoulAwardFrom(0) + 1, SoulEarnPolicy.Reasons.Seed, "huge"));
        Assert.Equal(0, _store.GetSoulBalance(1).Balance);
    }

    [Fact]
    public void MaxSoulAwardFrom_is_headroom_to_long_MaxValue_not_a_constant()
    {
        // T3.5 (spec-caps-reconcile.md §2.1, F12): the bound is int64Max - balance, checked fresh,
        // not a fixed ceiling. Independently recomputed here, not read off the implementation.
        Assert.Equal(long.MaxValue, RpgStore.MaxSoulAwardFrom(0));
        Assert.Equal(long.MaxValue - 500, RpgStore.MaxSoulAwardFrom(500));
        Assert.Equal(0, RpgStore.MaxSoulAwardFrom(long.MaxValue));
    }

    [Fact]
    public void An_award_legal_at_balance_zero_is_refused_once_balance_nears_int64Max()
    {
        // The dynamic bound in action, not just the pure function: the SAME delta (1000) succeeds at
        // balance 0 and is refused once the live balance leaves it no headroom (spec's own testing
        // table row: "an award legal at balance 0 is refused near int64Max").
        var ok = _store.AwardSouls(1, 1000, SoulEarnPolicy.Reasons.Seed, "small-1");
        Assert.True(ok.Inserted);
        _store.AwardSouls(2, long.MaxValue - 100, SoulEarnPolicy.Reasons.Seed, "near-ceiling");

        Assert.Throws<ArgumentOutOfRangeException>(() =>
            _store.AwardSouls(2, 1000, SoulEarnPolicy.Reasons.Seed, "small-2"));
        Assert.Equal(long.MaxValue - 100, _store.GetSoulBalance(2).Balance); // refused, not partially applied
    }

    [Fact]
    public void Kill_earn_memo_stays_exact_across_runs()
    {
        // Two separate matches, 60 kills each -- past the OLD per-match cap boundary this scenario
        // used to sit on (T3.6 deleted KillCapPerMatch). The in-memory memo (review C1) still tracks
        // a per-(player,run) kill count for the patron-bonus path, and still must isolate runs rather
        // than accumulate across them; the unpatroned total here is the observable proof (all 120
        // kills across both runs earn, uncapped, and neither run's count leaks into the other's).
        PlayMatch(Guid.NewGuid().ToString("N"), 60, "defeat");
        PlayMatch(Guid.NewGuid().ToString("N"), 60, "defeat");
        var expected = 2 * (60 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.DefeatDelta);
        Assert.Equal(expected, _store.GetSoulBalance(1).Balance);
    }
}
