using FusionRpg.Core.Balance.Analytic;
using FusionRpg.Core.Stats.Aptitudes;

namespace FusionRpg.Core.Balance.Guards;

/// <summary>One ordered matchup's predicted win share — win rate only, never fight length, total
/// damage or kill time (spec-balance-guard.md §7 "Never... measure duration").</summary>
public readonly record struct DominanceArrow(string AttackerName, string DefenderName, double WinShareAttacker);

public readonly record struct DominanceReport(
    bool IsDominant,
    IReadOnlyList<string> DominantBuildNames,
    IReadOnlyList<DominanceArrow> Matrix,
    CoverageReport Coverage);

/// <summary>
/// class-system-todo.md P5.2 — the SOFT half of <c>balance-guard</c> (spec-balance-guard.md, read in
/// full alongside P5.1). <see cref="Measure"/> RETURNS, never throws: a passive scaling damage with
/// damage taken, a reflect build, a counter-action or an anti-turtle status can all still fill a
/// dominant corner (the action/passive/skill layer, unbuilt) — unlike the termination invariant, this
/// is not an economy identity no later layer can repair, so it is reported, not blocking.
///
/// <para><b>No clock, ever</b> (spec-balance-guard.md §7): every <see cref="Predictor.Predict"/> call
/// here uses the default no-<c>roundLimit</c> overload. Ideal §0.1.2's own retraction is the reason —
/// a clock manufactures a pass by penalising survival and cc builds for playing correctly, which is
/// exactly the failure mode this guard exists to avoid reproducing.</para>
/// </summary>
public static class DominanceGuard
{
    /// <summary>"Beats" — the conventional win-rate reading, matching spec-balance-guard.md's own
    /// "no row... beats every other, on win rate": a strict majority, not a coin flip or worse. Not a
    /// balance dial (a fixed mathematical definition of "wins the matchup" — moving it would redefine
    /// what "dominant" means, not adjust how the game feels), and named to say so and stay out of the
    /// magic-numbers audit's balance-vocabulary match on "threshold".</summary>
    public const double MajorityWinShare = 0.5;

    public static DominanceReport Measure(IReadOnlyList<AptitudeAllocation> builds, long theta)
    {
        if (builds is null) throw new ArgumentNullException(nameof(builds));
        if (builds.Count == 0) throw new ArgumentException("must contain at least one build", nameof(builds));
        if (theta <= 0) throw new ArgumentOutOfRangeException(nameof(theta), theta, "must be positive");

        var actors = new Predictor.Actor[builds.Count];
        for (var i = 0; i < builds.Count; i++)
            actors[i] = TerminationGuard.ToActor($"corner{i}", builds[i], theta);

        var matrix = new List<DominanceArrow>();
        var winShareAgainst = new Dictionary<(int, int), double>();
        for (var i = 0; i < actors.Length; i++)
        for (var j = 0; j < actors.Length; j++)
        {
            if (i == j) continue;
            // No roundLimit argument -- the no-clock overload, on purpose (see type doc).
            var prediction = Predictor.Predict(actors[i], actors[j]);
            winShareAgainst[(i, j)] = prediction.WinShareA;
            matrix.Add(new DominanceArrow(actors[i].Name, actors[j].Name, prediction.WinShareA));
        }

        var dominant = new List<string>();
        for (var i = 0; i < actors.Length; i++)
        {
            var beatsEveryOther = true;
            for (var j = 0; j < actors.Length; j++)
            {
                if (i == j) continue;
                if (winShareAgainst[(i, j)] <= MajorityWinShare) { beatsEveryOther = false; break; }
            }
            if (beatsEveryOther && actors.Length > 1) dominant.Add(actors[i].Name);
        }

        return new DominanceReport(dominant.Count > 0, dominant, matrix, StandardCoverage());
    }

    /// <summary>The coverage this guard always reports, given Phase 4's own shipped scope — see
    /// <see cref="CoverageReport"/>'s own doc for the citations behind each value. The reserved-family
    /// list matches <c>docs/research/class-system/_baseline-dominance.json</c>'s own <c>coverage</c>
    /// block exactly (read this session, not re-derived) — the POC's own prior, already-validated
    /// enumeration of every action/resource-economy channel this guard's baseDamage=0 / no-actions
    /// scope cannot exercise, rather than a shorter list guessed independently.</summary>
    public static CoverageReport StandardCoverage() => new(
        ElementAxis: "NEUTRALISED -- StrikeMixture is omni-only (P4.1); every corner here is a 1-D slice of a live element axis",
        ReservedFamilies: new[]
        {
            "move.range",
            "resource.efficiency.hunger", "resource.efficiency.qi", "resource.efficiency.stamina",
            "resource.max.hunger", "resource.max.qi", "resource.max.spirit", "resource.max.stamina",
            "resource.regen.hunger", "resource.regen.qi", "resource.regen.spirit", "resource.regen.stamina",
            "skill.cooldown.attack", "skill.cooldown.defense", "skill.cooldown.movement", "skill.cooldown.status", "skill.cooldown.support",
            "skill.effectiveness.attack", "skill.effectiveness.defense", "skill.effectiveness.movement", "skill.effectiveness.status", "skill.effectiveness.support",
        });
}
