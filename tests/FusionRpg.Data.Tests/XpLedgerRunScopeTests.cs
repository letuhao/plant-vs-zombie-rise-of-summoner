using FusionRpg.Contracts;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// Regression for the run-unscoped XP dedupe bug (2026-08-21 review): defeat XP deduped on the
/// literal "run" — the second defeat ever was silently eaten; reused ptrs did the same for kills.
/// </summary>
public class XpLedgerRunScopeTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public XpLedgerRunScopeTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-xpscope-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    void PlayMatch(string matchKey, string result, string killPtr)
    {
        var t = DateTime.UtcNow.ToString("o");
        _store.InsertEvents(new[]
        {
            new EventEnvelope { T = t, Game = RpgConstants.GameId, Kind = "board.start", MatchKey = matchKey, Payload = new { levelName = "xp" } },
            new EventEnvelope { T = t, Game = RpgConstants.GameId, Kind = "zombie.die", MatchKey = matchKey, Payload = new { ptr = killPtr, type = 0 } },
            new EventEnvelope { T = t, Game = RpgConstants.GameId, Kind = "match.result", MatchKey = matchKey, Payload = new { result } },
            new EventEnvelope { T = t, Game = RpgConstants.GameId, Kind = "board.end", MatchKey = matchKey, Payload = new { levelName = "xp" } }
        });
    }

    [Fact]
    public void Second_defeat_still_awards_match_xp()
    {
        PlayMatch(Guid.NewGuid().ToString("N"), "defeat", "0xk1");
        PlayMatch(Guid.NewGuid().ToString("N"), "defeat", "0xk2");
        var page = _store.ListRpgXpLedger(1, kind: null, typeId: null, reason: null, limit: 100);
        Assert.NotNull(page);
        var matchRows = page!.Items.Where(i => i.Reason.Contains("match", StringComparison.OrdinalIgnoreCase)
                                            || i.Reason.Contains("defeat", StringComparison.OrdinalIgnoreCase)
                                            || i.Reason.Contains("result", StringComparison.OrdinalIgnoreCase)).ToList();
        Assert.True(matchRows.Count >= 2, $"expected match-end XP from both defeats, got {matchRows.Count}");
    }

    [Fact]
    public void Same_kill_ptr_across_matches_awards_both()
    {
        PlayMatch(Guid.NewGuid().ToString("N"), "victory", "0xSAME");
        PlayMatch(Guid.NewGuid().ToString("N"), "victory", "0xSAME");
        var page = _store.ListRpgXpLedger(1, kind: "player", typeId: null, reason: "kill", limit: 100);
        Assert.NotNull(page);
        Assert.True(page!.Items.Count >= 2, $"expected kill XP in both matches despite reused ptr, got {page.Items.Count}");
    }
}
