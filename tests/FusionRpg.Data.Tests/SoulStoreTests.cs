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
    public void Kill_earns_cap_at_50_per_match()
    {
        PlayMatch(Guid.NewGuid().ToString("N"), 80, "defeat");
        var b = _store.GetSoulBalance(1);
        Assert.Equal(50 * SoulEarnPolicy.KillDelta + SoulEarnPolicy.DefeatDelta, b.Balance);
    }

    [Fact]
    public void Victory_bonus_decays_after_third_win_of_day()
    {
        for (var i = 0; i < 5; i++)
            PlayMatch(Guid.NewGuid().ToString("N"), 0, "victory");
        var b = _store.GetSoulBalance(1);
        Assert.Equal(3 * 100 + 2 * 50, b.Balance);
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
}
