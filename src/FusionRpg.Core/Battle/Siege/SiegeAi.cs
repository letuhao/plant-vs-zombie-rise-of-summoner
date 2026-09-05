using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;

namespace FusionRpg.Core.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md), R1/R2/R5/R6: the pure decision mechanics — stance,
/// signed aggression, additive scoring, ordinal-tie-broken selection. No RNG, no `float`, no
/// `IBattleView` read anywhere in this file (structurally, not just by discipline — nothing here takes
/// one), so R5's determinism is provable from the type signatures alone.
///
/// <para><b>Deliberately does not implement a full board-reading AI.</b> What is built here proves
/// R1 (stance/aggression/score are three distinct things), R2 (additive scoring with XCOM's shipped
/// weights and a subtracting risk term), R5 (integer-only, ordinal tie-break) and R6 (a pure top-three
/// trace function) as pure, directly-testable mechanisms. What is named as a real, un-started gap
/// rather than rushed: R3's objective fallback (`BoardPathfinder`'s `TerrainOnlyOccupancy` — this file
/// never references `BoardPathfinder`), a real `IIntentSource` that reads `IBattleView` to compute
/// live `hitChanceMilli`/`incomingThreatMilli`/`objectiveClassMilli` from actual battle state (the
/// `AiSide` slot on <see cref="SiegeIntentSource"/> below is caller-supplied, not implemented here),
/// wiring <see cref="AiScoring.TopThree"/> into the real `DecisionTrace.cs`, §5.20 rule 5's emplacement
/// replacement vocabulary, and enforcing `RetargetLatencyTicks` from a live retarget loop. Every one of
/// those needs a working read of `IBattleView`/`BoardPathfinder` this session has not exercised in full,
/// and the spec's own §5.20 addendum on Relic's five-patch cover-seeking regression is a direct warning
/// against shipping an unverified live decision-maker under time pressure.</para>
/// </summary>
public enum Stance { Hold, Guard, Engage }

/// <summary>base-defense `siege-ai` §8 (§5.20 rule 2): a named, player-visible validity filter. Named,
/// because the whole thesis is that STATABILITY is the requirement — a filter the player cannot name
/// produces a miss they read as a bug.</summary>
public sealed record TargetFilter
{
    public string DisplayKey { get; init; } = "";
}

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md). Every weight is XCOM's own shipped value except
/// <see cref="WeightRisk"/> (a balance value, and decision 31's one-row rollback — 0 = cover-blind).
/// <see cref="AggressionRange"/>/<see cref="MaxCandidatesScored"/> are STRUCTURAL: the former IS the
/// taunt/stealth/decoy vocabulary (§5.20 rule 4), the latter a per-decision work bound — neither is a
/// progression ceiling.
/// </summary>
public sealed record AiTuning(
    int WeightHitChance, int WeightObjective, int WeightKill, int WeightLowHp, int WeightCannotCounter,
    int WeightRound, int WeightRisk, Stance StanceDefault, int AutoResolveHandicapMilli,
    long RetargetLatencyTicks, int AggressionRange, int MaxCandidatesScored);

/// <summary>
/// One candidate target, already resolved to the plain facts the scorer needs (§3's additive formula)
/// — deliberately decoupled from `IBattleView`, the same scoping `siege-objective`'s own
/// `SiegeCombatant` and `siege-economy`'s own `BoardOccupant` already established for this program.
/// </summary>
public readonly record struct AiCandidate(
    string ActorKey, int BaseTier, int Aggression,
    int HitChanceMilli, int ObjectiveClassMilli, bool IsKillingBlow,
    int TargetMissingHpMilli, bool TargetCanCounter, long IncomingThreatMilli);

public static class AiScoring
{
    /// <summary>
    /// §10's signed aggression applied INSIDE the tier computation (Isla's rule: a retarget hook goes
    /// inside the priority order, never on top of it) — never as a score bonus, which is what makes a
    /// taunt absolute within its tier and irrelevant outside it. Higher aggression pulls a candidate
    /// into a numerically LOWER (better) effective tier; a negative aggression (stealth) pushes it
    /// into a higher (worse) one. Bounded to `AggressionRange`: the range IS the vocabulary (§5.20
    /// rule 4's own comment), not a magnitude a balance pass widens.
    /// </summary>
    public static int EffectiveTier(int baseTier, int aggression, int aggressionRange)
    {
        if (aggressionRange <= 0) throw new ArgumentOutOfRangeException(nameof(aggressionRange));
        if (aggression < -aggressionRange || aggression > aggressionRange)
            throw new ArgumentOutOfRangeException(nameof(aggression),
                $"aggression {aggression} outside the authored ±{aggressionRange} range — the range IS the vocabulary.");
        return checked(baseTier - aggression);
    }

    /// <summary>
    /// §3's additive score. Every term is `long`-widened before summing, `checked` throughout — an
    /// overflow throws rather than silently inverting a comparison, which is the hardest possible bug
    /// to attribute in an AI (it would reliably pick the WORST option and look correct while doing it).
    /// </summary>
    public static long Score(AiCandidate c, int currentRound, AiTuning w)
    {
        if (currentRound < 0) throw new ArgumentOutOfRangeException(nameof(currentRound));

        long total = 0;
        total = checked(total + (long)w.WeightHitChance * c.HitChanceMilli);
        total = checked(total + (long)w.WeightObjective * c.ObjectiveClassMilli);
        total = checked(total + (long)w.WeightKill * (c.IsKillingBlow ? 1000 : 0));
        total = checked(total + (long)w.WeightLowHp * c.TargetMissingHpMilli);
        total = checked(total + (long)w.WeightCannotCounter * (c.TargetCanCounter ? 0 : 1000));
        total = checked(total + (long)w.WeightRound * currentRound);
        total = checked(total - (long)w.WeightRisk * c.IncomingThreatMilli);
        return total;
    }

    /// <summary>
    /// R1's three-step pipeline (tier, then score within it) plus R5's ordinal tie-break. Never a
    /// random move: an empty candidate list returns null (the caller falls back to objective-pathing
    /// or holds — R3), and this function itself has no notion of "no preference" once candidates exist
    /// (§5.20 rule 1). Truncates to <see cref="AiTuning.MaxCandidatesScored"/> BEFORE scoring — the
    /// structural work bound, applied in caller-supplied (i.e. already-meaningful) order.
    /// </summary>
    public static AiCandidate? ChooseTarget(IReadOnlyList<AiCandidate> candidates, int currentRound, AiTuning w)
    {
        if (candidates.Count == 0) return null;
        var pool = candidates.Count > w.MaxCandidatesScored ? candidates.Take(w.MaxCandidatesScored).ToList() : candidates;

        var bestTier = pool.Min(c => EffectiveTier(c.BaseTier, c.Aggression, w.AggressionRange));
        var inTier = pool.Where(c => EffectiveTier(c.BaseTier, c.Aggression, w.AggressionRange) == bestTier);

        return inTier
            .Select(c => (Candidate: c, Score: Score(c, currentRound, w)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Candidate.ActorKey, StringComparer.Ordinal)
            .Select(x => (AiCandidate?)x.Candidate)
            .First();
    }

    /// <summary>R6: the top three scored candidates with their raw score, ordinal-tie-broken —
    /// `DecisionTrace.cs`'s eventual input. Pure; costs nothing until a caller wires it in.</summary>
    public static IReadOnlyList<(string ActorKey, long Score)> TopThree(
        IReadOnlyList<AiCandidate> candidates, int currentRound, AiTuning w) =>
        candidates
            .Select(c => (c.ActorKey, Score: Score(c, currentRound, w)))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.ActorKey, StringComparer.Ordinal)
            .Take(3)
            .ToList();
}

/// <summary>
/// base-defense `siege-ai` §1: one `IIntentSource` for a siege, dispatching on `IBattleView.SideOf` —
/// `BattleEngine.Resolve` takes exactly one intent source and gains no parameter, so a played side and
/// an AI side are the same battle rather than two. A side whose delegate is null falls through to
/// <see cref="AiSide"/>, so "the human is playing the defender" and "nobody is playing" differ by one
/// nullable field.
///
/// <para><b>Deviates from the spec's own two-property literal shape by one required property,
/// <see cref="AiSide"/>.</b> The spec's snippet shows only `PlayedSide`/`PlayedSideId`, implicitly
/// assuming the class owns a working AI internally — but this module deliberately does not build that
/// AI (see this file's own top comment for why). Requiring the caller to supply `AiSide` keeps this
/// wrapper itself small, correct and fully testable today (the dispatch/fallthrough symmetry this task
/// exists to prove holds for ANY `IIntentSource` standing in for "AI"), while stating plainly that
/// wiring a REAL `AiScoring`-driven `IIntentSource` into that slot is the remaining, un-started work.
/// </para>
/// </summary>
public sealed class SiegeIntentSource : IIntentSource
{
    readonly IBattleView _view;

    public required IIntentSource AiSide { get; init; }
    public IIntentSource? PlayedSide { get; init; }
    public int PlayedSideId { get; init; } = -1;

    public SiegeIntentSource(IBattleView view)
    {
        _view = view ?? throw new ArgumentNullException(nameof(view));
    }

    public ActionIntent TryDeclare(string actorKey, long nowTick)
    {
        if (PlayedSide is not null && _view.SideOf(actorKey) == PlayedSideId)
            return PlayedSide.TryDeclare(actorKey, nowTick);
        return AiSide.TryDeclare(actorKey, nowTick);
    }
}
