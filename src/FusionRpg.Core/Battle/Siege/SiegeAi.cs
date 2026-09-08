using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle.Timeline;

namespace FusionRpg.Core.Battle.Siege;

/// <summary>
/// base-defense `siege-ai` (spec-siege-ai.md), R1/R2/R5/R6: the pure decision mechanics — stance,
/// signed aggression, additive scoring, ordinal-tie-broken selection. No random-number generator, no
/// non-integer numeric type anywhere in this file's arithmetic, and no `IBattleView` read anywhere in
/// this file (structurally, not just by discipline — nothing here takes one), so R5's determinism is
/// provable from the type signatures alone.
///
/// <para><b>Deliberately does not implement a full board-reading AI.</b> What is built here proves
/// R1 (stance/aggression/score are three distinct things), R2 (additive scoring with XCOM's shipped
/// weights and a subtracting risk term), R5 (integer-only arithmetic, ordinal tie-break) and R6 (a pure
/// top-three trace function) as pure, directly-testable mechanisms. What is named as a real, un-started
/// gap rather than rushed: R3's objective fallback (`BoardPathfinder`'s `TerrainOnlyOccupancy` — this
/// file never references `BoardPathfinder`), a real `IIntentSource` that reads `IBattleView` to compute
/// live `hitChanceMilli`/`incomingThreatMilli`/`objectiveClassMilli` from actual battle state (the
/// AI-side slot on <see cref="SiegeIntentSource"/> below is caller-supplied, not implemented here),
/// §5.20 rule 5's emplacement replacement vocabulary, and enforcing `RetargetLatencyTicks` from a live
/// retarget loop. Every one of those needed a working read of `IBattleView`/`BoardPathfinder`, and the
/// spec's own §5.20 addendum on Relic's five-patch cover-seeking regression was a direct warning
/// against shipping an unverified live decision-maker under time pressure — all four are now built
/// (`SiegeAiIntentSource`, 2026-09-05/07). <see cref="AiScoring.TopThree"/> is wired into a live trace
/// too (R6, 2026-09-07) — correcting the spec's own citation: `Battle/Timeline/DecisionTrace.cs`
/// replays HUMAN input decisions for `(setup, seed, trace)` determinism, a fixed `Player`/`Timeout`
/// shape with no room for a scored candidate list, so the real target is `BattleTrace.AiDecision`,
/// the SAME opt-in/non-null-gated/golden-neutral observability object the spec's own R6 text names as
/// the pattern to follow ("exactly as `BattleTrace` is").</para>
/// </summary>
public enum Stance { Hold, Guard, Engage }

/// <summary>
/// base-defense `siege-ai` 17.9, §5.20 rule 5: the garrisoned emplacement's OWN two-entry vocabulary,
/// replacing R3's objective-fallback movement (meaningless for something that cannot move) rather than
/// reusing <see cref="Stance"/>'s `Hold` value as a "close enough" stand-in — the spec's own §11 title
/// is literally "a replacement vocabulary, not a degraded one." `SiegeAiIntentSource` resolves every
/// garrisoned occupant to <see cref="FireAtWill"/> today (no content authors a `HoldFire` trigger yet);
/// the vocabulary exists and is named ahead of that consumer, the same order 17.7's own `TargetFilter`
/// already shipped in.
/// </summary>
public enum EmplacementFireMode { HoldFire, FireAtWill }

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
    long RetargetLatencyTicks, int AggressionRange, int MaxCandidatesScored,
    int ObjectiveReferenceDistanceCells, int ThreatRadiusCells);

/// <summary>
/// One candidate target, already resolved to the plain facts the scorer needs (§3's additive formula)
/// — deliberately decoupled from `IBattleView`, the same scoping `siege-objective`'s own
/// `SiegeCombatant` and `siege-economy`'s own `BoardOccupant` already established for this program.
/// </summary>
public readonly record struct AiCandidate(
    string ActorKey, int BaseTier, int Aggression,
    int HitChanceMilli, int ObjectiveClassMilli, bool IsKillingBlow,
    int TargetMissingHpMilli, bool TargetCanCounter, long IncomingThreatMilli);

/// <summary>
/// R6's own per-term breakdown of one candidate's <see cref="AiScoring.Score"/> — for trace/debug
/// readability only, never an independent computation: <see cref="Total"/> is always
/// <see cref="AiScoring.Score"/>'s own return value for the SAME candidate, called directly rather
/// than re-derived from the individual terms, so the breakdown can never silently disagree with the
/// total a real decision actually used.
/// </summary>
public readonly record struct AiScoreBreakdown(
    long HitChance, long Objective, long Kill, long LowHp, long CannotCounter, long Round, long Risk, long Total);

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

    /// <summary>
    /// Every individual weighted term <see cref="Score"/> sums, broken out for R6's own trace.
    /// <see cref="AiScoreBreakdown.Total"/> calls <see cref="Score"/> directly rather than re-summing
    /// the terms here — a second accumulation could overflow/round differently than the tested,
    /// already-shipped one, exactly the "silently-diverging second copy" this program avoids elsewhere
    /// (`SiegeExpectedDamage`/`SiegeHitChance` reusing `OverlayCombatCalculator`'s formula verbatim).
    /// </summary>
    public static AiScoreBreakdown ScoreBreakdownOf(AiCandidate c, int currentRound, AiTuning w) =>
        new(
            HitChance: checked((long)w.WeightHitChance * c.HitChanceMilli),
            Objective: checked((long)w.WeightObjective * c.ObjectiveClassMilli),
            Kill: checked((long)w.WeightKill * (c.IsKillingBlow ? 1000 : 0)),
            LowHp: checked((long)w.WeightLowHp * c.TargetMissingHpMilli),
            CannotCounter: checked((long)w.WeightCannotCounter * (c.TargetCanCounter ? 0 : 1000)),
            Round: checked((long)w.WeightRound * currentRound),
            Risk: checked((long)w.WeightRisk * c.IncomingThreatMilli),
            Total: Score(c, currentRound, w));

    /// <summary>R6: the top three scored candidates with their full per-term breakdown, ordinal-tie-broken
    /// — `BattleTrace.AiDecision`'s eventual input via `FormatTopThree`. Pure; costs nothing until a
    /// caller wires it in.</summary>
    public static IReadOnlyList<(string ActorKey, AiScoreBreakdown Breakdown)> TopThree(
        IReadOnlyList<AiCandidate> candidates, int currentRound, AiTuning w) =>
        candidates
            .Select(c => (c.ActorKey, Breakdown: ScoreBreakdownOf(c, currentRound, w)))
            .OrderByDescending(x => x.Breakdown.Total)
            .ThenBy(x => x.ActorKey, StringComparer.Ordinal)
            .Take(3)
            .ToList();

    /// <summary>
    /// Formats R6's own top-three-with-breakdown as one line for `BattleTrace.AiDecision`.
    /// `BattleTrace` stays domain-agnostic (every other method there takes primitives, never a
    /// subsystem's own type) — this module owns its own trace text instead of handing `BattleTrace`
    /// an `AiScoreBreakdown` and creating a `Timeline` → `Siege` dependency opposite the existing one
    /// (`SiegeAiIntentSource` already depends on `Timeline`, not the reverse).
    /// </summary>
    public static string FormatTopThree(IReadOnlyList<(string ActorKey, AiScoreBreakdown Breakdown)> topThree) =>
        string.Join(" ", topThree.Select((t, i) =>
            $"#{i + 1}={t.ActorKey}(hit={t.Breakdown.HitChance},obj={t.Breakdown.Objective}," +
            $"kill={t.Breakdown.Kill},lowhp={t.Breakdown.LowHp},cc={t.Breakdown.CannotCounter}," +
            $"rnd={t.Breakdown.Round},risk={t.Breakdown.Risk},total={t.Breakdown.Total})"));
}

/// <summary>
/// base-defense `siege-ai` §1: one `IIntentSource` for a siege — `BattleEngine.Resolve` takes exactly
/// one intent source and gains no parameter, so a played side and an AI side are the same battle rather
/// than two. A side whose delegate is null falls through to the AI side, so "the human is playing the
/// defender" and "nobody is playing" differ by one nullable field.
///
/// <para><b>Corrected during `siege-resolver` integration (module 15), 2026-09-05: the original
/// design dispatched on `IBattleView.SideOf`, which cannot work for any real caller.</b>
/// `BattleEngine.Resolve` builds its `IBattleView` internally, AFTER it is called — no external caller
/// of `Resolve` (this class's whole reason to exist) ever holds one to construct this class with. The
/// fix needs no live view at all: which actor keys belong to the played side is known BEFORE the battle
/// starts, from whichever side of the `BattleSetup` the caller is letting a human play (its `Squad` or
/// `Wave` key list) — so dispatch reduces to a plain, precomputed key-set lookup, which is simpler AND
/// more deterministic than a live interface call would have been. Filed under `siege-ai`'s own task
/// 17.1 (the module that shipped the defect) and this module's own record, since the fix was found and
/// made while wiring the first real caller.</para>
///
/// <para><b>Deviates from the spec's own two-property literal shape by one mandatory constructor
/// parameter, the AI side.</b> The spec's snippet shows only `PlayedSide`/`PlayedSideId`, implicitly
/// assuming the class owns a working AI internally — but this module deliberately does not build that
/// AI (see this file's own top comment for why). Requiring the caller to supply the AI side keeps this
/// wrapper itself small, correct and fully testable today (the dispatch/fallthrough symmetry this task
/// exists to prove holds for ANY `IIntentSource` standing in for "AI"), while stating plainly that
/// wiring a REAL `AiScoring`-driven `IIntentSource` into that slot is the remaining, un-started work.
/// A constructor parameter, not an `init` property with C# 11's `required` — this project targets
/// `net6.0` (C# 10), matching every other constructor-validated seam in this file's own neighbourhood
/// (`StubIntentSource`'s own precedent).</para>
/// </summary>
public sealed class SiegeIntentSource : IIntentSource
{
    readonly IIntentSource _aiSide;
    readonly IReadOnlySet<string> _playedSideKeys;

    public IIntentSource? PlayedSide { get; init; }

    public SiegeIntentSource(IIntentSource aiSide, IReadOnlySet<string> playedSideKeys)
    {
        _aiSide = aiSide ?? throw new ArgumentNullException(nameof(aiSide));
        _playedSideKeys = playedSideKeys ?? throw new ArgumentNullException(nameof(playedSideKeys));
    }

    public ActionIntent TryDeclare(string actorKey, long nowTick)
    {
        if (PlayedSide is not null && _playedSideKeys.Contains(actorKey))
            return PlayedSide.TryDeclare(actorKey, nowTick);
        return _aiSide.TryDeclare(actorKey, nowTick);
    }
}
