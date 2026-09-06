using FusionRpg.Data;
using FusionRpg.Server;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FusionRpg.E2E.Tests;

/// <summary>
/// party-dungeon D2.16 — the boot sweep must never finish a steered party on autopilot across a
/// restart. `WebMatchService.IsInteractive` reads a row's own `profile_id` (D2.15) directly via
/// `BattleModeProfileCatalog.Resolve`, because a delve row's `wave_id` is an encounter-anchor id
/// `WaveCatalog` has never heard of and the ORIGINAL wave-lookup path could never see it as
/// interactive on its own — regression coverage for the exact gap found while wiring D2.16.
/// </summary>
[Collection("e2e")]
public class WebMatchInteractiveSweepTests
{
    readonly RpgStore _store;
    readonly WebMatchService _matches;

    public WebMatchInteractiveSweepTests(RpgApiFactory factory)
    {
        _store = factory.Services.GetRequiredService<RpgStore>();
        _matches = factory.Services.GetRequiredService<WebMatchService>();
    }

    (long Id, string Corr) Unresolved(string tag, string? profileId, string? decisionsJson = null)
    {
        var corr = "interactive-" + tag + "-" + Guid.NewGuid().ToString("N")[..8];
        var (created, entry) = _store.AppendWebMatchLog(
            playerId: 1, correlationId: corr, matchKey: "match-" + corr,
            setupJson: "{}", seed: 7,
            engineVersion: FusionRpg.Core.Battle.BattleRuleset.EngineVersion,
            rulesetVersion: FusionRpg.Core.Battle.BattleRuleset.RulesetVersion,
            rngAlgoVersion: FusionRpg.Core.Battle.SeededRng.RngAlgoVersion,
            environmentStamp: FusionRpg.Core.Battle.BattleEnvironment.Stamp,
            contentHash: _store.ComputeContentHash().ToCompact(),
            profileId: profileId);
        Assert.True(created);
        if (decisionsJson != null) _store.WriteWebMatchDecisions(entry.Id, decisionsJson);
        return (entry.Id, corr);
    }

    string? RefusalFor(string corr) => _store.TryGetWebMatchLog(1, corr)?.SweepRefused;

    [Fact]
    public void A_delve_profiled_row_with_no_decision_trace_is_refused()
    {
        var row = Unresolved("no-trace", profileId: "delve");

        _matches.SweepUnresolved();

        Assert.NotNull(RefusalFor(row.Corr));
        Assert.DoesNotContain(_store.ListUnresolvedWebMatches(500), e => e.Id == row.Id); // terminal
    }

    /// <summary>⛔ The exact hole found while wiring D2.16: BEFORE the fix, `IsInteractive` looked only
    /// at the wave (which a delve row's synthetic id can never resolve), so this row would have been
    /// silently healed by `ResolveAndIngest` on the default AI policy — finishing a steered fight on
    /// autopilot across a restart. A trace being present does not make that safe either:
    /// `ResolveAndIngest` has no path to replay it or to pick this row's own profile.</summary>
    [Fact]
    public void A_delve_profiled_row_with_a_partial_decision_trace_is_still_refused_not_silently_healed()
    {
        var row = Unresolved("with-trace", profileId: "delve", decisionsJson: "[]");

        _matches.SweepUnresolved();

        Assert.NotNull(RefusalFor(row.Corr));
        Assert.DoesNotContain(_store.ListUnresolvedWebMatches(500), e => e.Id == row.Id);
    }

    [Fact]
    public void A_row_with_no_profile_id_and_an_unresolvable_wave_is_not_treated_as_interactive()
    {
        // Baseline: every match logged today (no production caller stamps profile_id yet) must keep
        // its exact prior behaviour — an empty setup's null WaveId can never resolve to a live-input
        // profile, so this is healed normally, not refused.
        var row = Unresolved("legacy", profileId: null);

        _matches.SweepUnresolved();

        Assert.Null(RefusalFor(row.Corr));
    }

    [Fact]
    public void A_row_with_an_unrecognised_profile_id_is_refused_rather_than_crashing_the_sweep()
    {
        var bad = Unresolved("garbage-profile", profileId: "not-a-real-profile");
        var good = Unresolved("after-garbage", profileId: null);

        _matches.SweepUnresolved(); // must not throw and abort before reaching `good`

        Assert.NotNull(RefusalFor(bad.Corr));
        Assert.Null(RefusalFor(good.Corr));
        // Not merely "unrefused" (which a sweep that aborted early would also show) — actually HEALED,
        // proving the loop really did continue past the bad row rather than throwing out of it.
        Assert.DoesNotContain(_store.ListUnresolvedWebMatches(500), e => e.Id == good.Id);
    }
}
