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
    /// <summary>Not a balance dial in its own right — <b>every shipped profile overwrites this from
    /// `timeline.profiles.&lt;id&gt;.w`</b> (T14/B29). The literal is the record's inert default, kept
    /// so a hand-constructed profile in a test rig is strictly serial unless it asks not to be:
    /// `W = 1` is the most conservative possible scheduling choice, not a tuned one.</summary>
    public int W { get; init; } = 1;
    public WScope WScope { get; init; } = WScope.Global;
    public Commitment DefaultCommitment { get; init; } = Commitment.LateBound;
    /// <summary>Same status as <see cref="W"/>: overwritten from config for every shipped profile.
    /// The literal is one tick — the smallest advance that still advances, which is the only
    /// structurally safe default (0 would reschedule a passing actor at `now` forever).</summary>
    public long PassQuantum { get; init; } = 1;
    public int WReact { get; init; }
    public bool RendezvousEnabled { get; init; }
    /// <summary>
    /// Makes a FRESH economy for one battle. A factory, not an instance — and that distinction is a
    /// real defect this record used to have, found by B37 and reproduced before it was fixed.
    ///
    /// <para><b>Why an instance was unsafe.</b> Profiles are cached singletons
    /// (<see cref="BattleModeProfileCatalog"/>), and every economy holds mutable per-key budget state
    /// (<c>OneActionPerTurnEconomy._spent</c>, <c>ActionPointsEconomy._points</c>). Battle actor keys
    /// repeat across battles — <c>"squad:0"</c> is <c>"squad:0"</c> in every one — so two battles
    /// running at once shared a single budget and silently starved each other's actors of turns.
    /// Reproduced exactly that way: the trace goldens passed when run alone and failed inside the
    /// parallel suite, with actors 2..n never acting.</para>
    ///
    /// <para>A factory makes the hazard unrepresentable: an engine cannot accidentally share a budget,
    /// because it is handed a way to make its own.</para>
    /// </summary>
    public Func<ITurnEconomy> NewEconomy { get; init; } = static () => new OneActionPerTurnEconomy();

    /// <summary>
    /// T8 — how far a turn-order forecast can be trusted under this profile
    /// (spec-turn-order-forecast.md §2).
    ///
    /// <para><b>A declared field, not a computed branch, and that was a correction.</b> The obvious
    /// implementation is <c>AdvancePolicy == NextEvent ? Exact : SoftBounded</c> — and
    /// <c>ModeProfileArchitectureTests</c> rejects it in EVERY file, including this one: its
    /// profile-id exemption covers id literals only, never a branch on
    /// <see cref="AdvancePolicyKind"/>. The rule is absolute because the map's acceptance is
    /// structural — "adding a mode adds a row, never a branch in the kernel" — and a computed
    /// property is a branch wearing a row's clothes. So each row states its own exactness, and a
    /// fourth mode states its own too.</para>
    /// </summary>
    public ForecastExactness ForecastExactness { get; init; } = ForecastExactness.Exact;

    /// <summary>
    /// **B39 — whether turn order within a round is decided by readiness or by the initiative roll.**
    ///
    /// <para><b>Declared per row, never computed.</b> The obvious implementation is
    /// "`AdvancePolicy == FixedIncrement` means speed-ordered", and it is the wrong one: it is exactly
    /// the branch <c>ModeProfileArchitectureTests</c> bans, and it silently decides the question for
    /// every future mode that happens to share an advance policy. Adding a mode adds a row, never a
    /// branch — the same correction <see cref="ForecastExactness"/> already carries.</para>
    ///
    /// <para><b>False for <c>classic-round</c> and load-bearing.</b> That profile pins readiness to a
    /// constant by design (`battle-turn-ideal.md` §10) so every actor arrives together at the round
    /// tick; its initiative ordering is what every existing battle and expedition golden was blessed
    /// against. <c>galaxy-sync</c> is false for a different reason — no shipped surface selects it, and
    /// turning on a behaviour nobody can observe is a claim rather than a feature.</para>
    /// </summary>
    public bool OrdersBySpeed { get; init; }

    /// <summary>
    /// T6/B21 — whether this profile's `Ready` dwell expects a live human.
    ///
    /// <para>Declared per row, like <see cref="ForecastExactness"/>, and for the same architectural
    /// reason: `ModeProfileArchitectureTests` forbids branching on <see cref="AdvancePolicyKind"/> in
    /// every file, so "which modes are interactive" is data rather than a computed rule.</para>
    ///
    /// <para><b>False for all three shipped profiles.</b> It exists so an expedition can be barred from
    /// selecting an interactive one BY ASSERTION rather than by convention — an expedition resolves
    /// server-side with nobody watching, so an interactive profile there could only ever time out every
    /// turn, which is a slow way to produce a worse auto-resolve.</para>
    /// </summary>
    public bool RequiresLiveInput { get; init; }

    /// <summary>
    /// base-defense F2 — the battle's absolute round horizon, moved here from the engine-global
    /// <see cref="BattleRuleset.MaxRounds"/>. A siege needs a longer horizon than a squad fight, and
    /// with the value global, giving one gives all.
    ///
    /// <para><b>A structural bound, not a progression ceiling.</b> AGENTS.md's no-hard-ceilings rule
    /// exempts "per-frame/runtime caps" and this is one: it bounds how long a single battle may run,
    /// not how strong anything may become.</para>
    ///
    /// <para><b>No literal default here.</b> Every shipped row's concrete value comes from
    /// `BattleModeProfileCatalog.Build`, resolved from tuning with a fallback to
    /// <see cref="BattleRuleset.MaxRounds"/> — the same "overwritten from config for every shipped
    /// profile" status <see cref="W"/> and <see cref="PassQuantum"/> already carry. `Build` throws if
    /// this would resolve to zero, so a hand-constructed profile that forgets it fails loudly rather
    /// than silently producing a battle with no rounds.</para>
    /// </summary>
    public int MaxRounds { get; init; }

    /// <summary>Paired with <see cref="MaxRounds"/> — they are only ever read together
    /// (`maxBattleTick` multiplies them), so splitting their resolution would let a profile carry
    /// half a horizon. Same no-literal-default status.</summary>
    public int RoundDurationMs { get; init; }
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
    public const string SiegeId = "siege";

    // T14/B29 — the STRUCTURE of each row is here; its MAGNITUDES (W, WReact, PassQuantum, and
    // hybrid-atb's maxPoints) come from data/tuning/battle.v{n}.json's timeline.profiles.
    //
    // Lazy + cached, not `static readonly ... = new(){...}`, for the reason WaveCatalog already
    // records for itself (catalog-runtime §3a): a static field initializer runs at class-load, which
    // is before any host or test bootstrap calls Configure, so it could only ever have baked in a
    // hardcoded value. Caching also keeps each profile a SINGLE instance, which existing tests rely
    // on directly (`Assert.Same(BattleModeProfileCatalog.ClassicRound, ...)`).

    static BattleTuning? _tuning;
    static BattleModeProfile? _classicRound, _galaxySync, _hybridAtb, _siege;

    /// <summary>Called by <see cref="BattleTuningHub.Configure"/>, never directly by game code.
    /// Resets the cached rows so a reconfigure (CombatSim's `compare`, a scoped test) is honoured
    /// rather than serving a stale profile.</summary>
    public static void Configure(BattleTuning tuning)
    {
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        _classicRound = _galaxySync = _hybridAtb = _siege = null;
    }

    static BattleTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "BattleModeProfileCatalog.Configure(...) has not run. Every profile magnitude reads " +
        "data/tuning/battle.v{n}.json's timeline.profiles (tunables-ssot.md T5) — there is no " +
        "built-in default to fall back to.");

    /// <summary>Today's engine, described as data: one actor mid-action at a time, one action per
    /// turn — the profile every existing battle and expedition golden implicitly runs under.</summary>
    public static BattleModeProfile ClassicRound => _classicRound ??= Build(
        ClassicRoundId, AdvancePolicyKind.NextEvent, WScope.Global, Commitment.LateBound, points: false,
        forecast: ForecastExactness.Exact);

    /// <summary>Turn-based but concurrent — two actors per side may be mid-action at once. This IS
    /// the contrast case B12's own acceptance line names: `W=2` here provably overlaps where
    /// `classic-round`'s `W=1` provably cannot, in the same test file.</summary>
    public static BattleModeProfile GalaxySync => _galaxySync ??= Build(
        GalaxySyncId, AdvancePolicyKind.NextEvent, WScope.PerSide, Commitment.LateBound, points: false,
        forecast: ForecastExactness.Exact);

    /// <summary>Real-time-flavored: fixed-increment advance, a wider concurrency width, and an
    /// Action-Points economy rather than one-action-per-turn — genuinely exercises the OTHER half
    /// of <see cref="ITurnEconomy"/> this map names, not just a third copy of the first two.</summary>
    public static BattleModeProfile HybridAtb => _hybridAtb ??= Build(
        HybridAtbId, AdvancePolicyKind.FixedIncrement, WScope.Global, Commitment.EarlyBoundWithFallback, points: true,
        // Fixed-increment advance with W > 1: an action resolving inside the window can schedule an
        // event ahead of a forecast entry, so the projection is the queue's current truth but not a promise.
        forecast: ForecastExactness.SoftBounded,
        // B39: the profile whose turn order `turn.speed`/`turn.haste` actually decide. An
        // Active-Time-Battle mode that ignored speed would be one in name only.
        ordersBySpeed: true);

    /// <summary>The district board (base-defense-ideal.md §5.11/§5.16). Turn-based like
    /// <c>classic-round</c>, but speed-ordered and interactive — movement precedes contact on a
    /// board, so who steps first is a decision rather than a formality, and a siege is played, not
    /// auto-resolved by default (though `siege-ai` may supply its own <see cref="IIntentSource"/> and
    /// never dwell).
    ///
    /// <para><c>WScope.PerSide</c>, not <c>Global</c>: decision — both sides move. Under
    /// <c>WScope.Global</c> with <c>W=1</c> the two sides interleave one actor at a time
    /// (<c>classic-round</c>'s shape), which is not what "both sides move" means. <c>PerSide</c> is
    /// the scope <c>galaxy-sync</c> already proves concurrent under.</para>
    ///
    /// <para><c>points: false</c> — <b>one action per activation, never <c>ActionPointsEconomy</c>.</b>
    /// `action-map.md:430`: "No compound move-and-attack action is required, and no Action Points.
    /// The time cost is the economy... it is simply not what this mode needs." Readiness is work over
    /// rate: a fast actor's cheap step and a slow actor's expensive strike already cost differently
    /// through each action's own `TimeCostTicks`, so a fast actor can fit both a move and a strike
    /// into the window a slow one needs for one swing. Decision 14's "build is a third peer of move
    /// and attack" is satisfied by a heavy `TimeCostTicks` on the build action, not by a second
    /// economy.</para>
    /// </summary>
    public static BattleModeProfile Siege => _siege ??= Build(
        SiegeId, AdvancePolicyKind.NextEvent, WScope.PerSide, Commitment.LateBound, points: false,
        forecast: ForecastExactness.Exact,
        ordersBySpeed: true,
        requiresLiveInput: true);

    static BattleModeProfile Build(
        string id, AdvancePolicyKind advance, WScope wScope, Commitment commitment, bool points,
        ForecastExactness forecast, bool ordersBySpeed = false, bool requiresLiveInput = false)
    {
        var t = Tuning.ProfileOf(id);
        if (points && t.MaxPoints is null)
            throw new BattleTuningRejection(
                $"battle tuning: timeline.profiles.{id} runs an ActionPoints economy but carries no 'maxPoints'.");
        if (!points && t.MaxPoints is not null)
            throw new BattleTuningRejection(
                $"battle tuning: timeline.profiles.{id} carries 'maxPoints' but its economy has no budget — " +
                "a value that can never be read is a balance row lying about what it controls.");

        // base-defense F2: null means "content did not choose", inherited from the ruleset. Resolved
        // HERE, once, so every reader (BattleEngine) sees a concrete int and never has to know the
        // profile could have left it unset.
        var maxRounds = t.MaxRounds ?? BattleRuleset.MaxRounds;
        var roundDurationMs = t.RoundDurationMs ?? BattleRuleset.RoundDurationMs;
        if (maxRounds <= 0)
            throw new BattleTuningRejection(
                $"battle tuning: timeline.profiles.{id} resolves to maxRounds={maxRounds} (0 or " +
                "negative) — a hand-constructed profile or a misconfigured ruleset fallback would " +
                "silently produce a battle with no rounds.");
        if (roundDurationMs <= 0)
            throw new BattleTuningRejection(
                $"battle tuning: timeline.profiles.{id} resolves to roundDurationMs={roundDurationMs} " +
                "(0 or negative).");

        return new BattleModeProfile
        {
            ProfileId = id,
            AdvancePolicy = advance,
            W = t.W,
            WScope = wScope,
            DefaultCommitment = commitment,
            PassQuantum = t.PassQuantum,
            WReact = t.WReact,
            NewEconomy = points
                ? () => new ActionPointsEconomy(t.MaxPoints!.Value)
                : static () => new OneActionPerTurnEconomy(),
            ForecastExactness = forecast,
            OrdersBySpeed = ordersBySpeed,
            RequiresLiveInput = requiresLiveInput,
            MaxRounds = maxRounds,
            RoundDurationMs = roundDurationMs
        };
    }

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
        SiegeId => Siege,
        _ => throw new ArgumentException($"Unknown battle mode profile id '{profileId}'.", nameof(profileId))
    };
}
