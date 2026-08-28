namespace FusionRpg.Core.Battle.Timeline;

/// <summary>Which of the kernel's two time-advance mechanisms a profile drives on
/// (spec-virtual-time-core.md) — next-event jump for turn-based modes, fixed-increment step for
/// real-time ones. The two are the same discrete-event-simulation architecture differing only in
/// this one choice, which is the whole point of the kernel existing.</summary>
public enum AdvancePolicyKind
{
    NextEvent,
    FixedIncrement
}

/// <summary>
/// B11 / T4a — a battle mode as data (spec: "advance policy + W + commitment + readiness function +
/// economy... acceptance is structural: adding a mode adds a row, never a branch in the kernel").
///
/// <para><b>No "readiness function" field.</b> The map's own draft named one, written before T3
/// closed on a single universal pure function (<see cref="TurnReadiness"/>) rather than a
/// per-mode formula — every profile reads the SAME readiness math over its own <c>W</c>/economy/
/// advance policy, so a per-profile delegate here would be a second readiness mechanism with
/// nothing to select between. Recorded here rather than silently dropped.</para>
///
/// <para><see cref="WReact"/> and <see cref="RendezvousEnabled"/> are B6's and B7's own gates,
/// named explicitly in spec-turn-fsm.md: "Both features are gated by profile knobs... that default
/// off, so <c>classic-round</c> is untouched and the T5 gate is unaffected." Both default off here
/// for exactly that reason — a profile that never sets them behaves as if the reaction lane and the
/// rendezvous lane did not exist, matching each module's own byte-identical-at-default claim.</para>
///
/// <para><see cref="PassQuantum"/> is the field <see cref="IIntentSource"/>'s own doc comment
/// already named ("reschedules at <c>now + PassQuantum</c> — a profile field") before this type
/// existed to hold it.</para>
///
/// <para><b>This module builds ZERO profile rows.</b> Defining <c>classic-round</c>,
/// <c>galaxy-sync</c>, and <c>hybrid-atb</c> is B12's job — the architecture test below must be
/// green with no row anywhere in the kernel, which is the acceptance line this file exists to let
/// pass truthfully rather than vacuously.</para>
/// </summary>
public sealed record BattleModeProfile
{
    public string ProfileId { get; init; } = "";
    public AdvancePolicyKind AdvancePolicy { get; init; } = AdvancePolicyKind.NextEvent;
    public int W { get; init; } = 1;
    public WScope WScope { get; init; } = WScope.Global;
    public Commitment DefaultCommitment { get; init; } = Commitment.LateBound;
    public long PassQuantum { get; init; } = 1;
    public int WReact { get; init; }
    public bool RendezvousEnabled { get; init; }
    public ITurnEconomy Economy { get; init; } = new OneActionPerTurnEconomy();
}

/// <summary>
/// B12 / T4b — the three rows battle-timeline-map.md names, and the resolver
/// <c>WaveCatalog.Get(waveId).Profile ?? classic-round</c> reads. The only file allowed to hold a
/// profile-id string literal or branch on <see cref="AdvancePolicyKind"/> —
/// <c>ModeProfileArchitectureTests</c> exempts exactly this file, nothing else. **Acceptance is
/// structural**: adding a fourth mode adds a row here (plus one line in <c>Resolve</c> and one in
/// <c>ModeProfileArchitectureTests.KnownProfileIds</c>), never a branch anywhere else in the kernel.
/// </summary>
public static class BattleModeProfileCatalog
{
    public const string ClassicRoundId = "classic-round";
    public const string GalaxySyncId = "galaxy-sync";
    public const string HybridAtbId = "hybrid-atb";

    /// <summary>Today's engine, described as data: one actor mid-action at a time, one action per
    /// turn — the profile every existing battle and expedition golden implicitly runs under.</summary>
    public static readonly BattleModeProfile ClassicRound = new()
    {
        ProfileId = ClassicRoundId,
        AdvancePolicy = AdvancePolicyKind.NextEvent,
        W = 1,
        WScope = WScope.Global,
        DefaultCommitment = Commitment.LateBound,
        Economy = new OneActionPerTurnEconomy()
    };

    /// <summary>Turn-based but concurrent — two actors per side may be mid-action at once. This IS
    /// the contrast case B12's own acceptance line names: `W=2` here provably overlaps where
    /// `classic-round`'s `W=1` provably cannot, in the same test file.</summary>
    public static readonly BattleModeProfile GalaxySync = new()
    {
        ProfileId = GalaxySyncId,
        AdvancePolicy = AdvancePolicyKind.NextEvent,
        W = 2,
        WScope = WScope.PerSide,
        DefaultCommitment = Commitment.LateBound,
        Economy = new OneActionPerTurnEconomy()
    };

    /// <summary>Real-time-flavored: fixed-increment advance, a wider concurrency width, and an
    /// Action-Points economy rather than one-action-per-turn — genuinely exercises the OTHER half
    /// of <see cref="ITurnEconomy"/> this map names, not just a third copy of the first two.</summary>
    public static readonly BattleModeProfile HybridAtb = new()
    {
        ProfileId = HybridAtbId,
        AdvancePolicy = AdvancePolicyKind.FixedIncrement,
        W = 4,
        WScope = WScope.Global,
        DefaultCommitment = Commitment.EarlyBoundWithFallback,
        Economy = new ActionPointsEconomy(maxPoints: 2)
    };

    /// <summary>
    /// <c>WaveCatalog.Get(waveId).Profile ?? classic-round</c>, made concrete: <c>null</c> resolves
    /// to <see cref="ClassicRound"/> (content did not choose); a known id resolves to its row; an
    /// UNKNOWN id throws rather than silently falling back — "content did not choose" and "content
    /// chose wrong" are different failure modes, and only the first one is the documented default.
    /// Loud-over-silent, matching this module's own stance everywhere else (illegal transitions,
    /// a `Category` cooldown missing its key).
    /// </summary>
    public static BattleModeProfile Resolve(string? profileId) => profileId switch
    {
        null => ClassicRound,
        ClassicRoundId => ClassicRound,
        GalaxySyncId => GalaxySync,
        HybridAtbId => HybridAtb,
        _ => throw new ArgumentException($"Unknown battle mode profile id '{profileId}'.", nameof(profileId))
    };
}
