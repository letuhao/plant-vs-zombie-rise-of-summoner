namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>
/// class-system-todo.md P6.1 — the RATE table for the four allocation scopes
/// (spec-point-economy.md §1: "give the four allocation scopes their point budgets"). Pure function
/// over an explicit <see cref="AptitudeTuning"/> — never reaches into <see cref="AptitudeTuningHub"/>
/// itself, matching Phase 4's own established style (<c>FirstPassage</c>/<c>Race</c>/<c>PhaseModel</c>),
/// and sidestepping the exact global-mutable-hub race this session already found and fixed once
/// (<c>TerminationGuardTests</c>/<c>DominanceGuardTests</c>' own <c>[Collection("AptitudeTuningHub")]</c>).
///
/// <para>This module ships the TABLE and the LOADER — never the four scopes' own source values
/// (spec-point-economy.md §2's table: `Θ_player` for Commander, species level for DemonType,
/// `element_mastery` for Aspect, specimen level for UniqueDemon). The caller supplies
/// <paramref name="sourceValue"/>; <see cref="PointsFor"/> only knows the RATE. Aspect's own source
/// (`element_mastery`) is owned by the demon program's `aspect-scope` module and does not exist yet —
/// this type is agnostic to that, so it ships complete today and "lights up" for Aspect the moment a
/// caller has a real value to pass (spec-point-economy.md's own header: "ships three-of-four, lights
/// up the fourth when the tier lands").</para>
///
/// <para><b>`species-build` T0.4, audit finding A1.</b> The DemonType source used to be documented as
/// "type almanac XP" — an ACCUMULATION, while the other three tiers all read an INDEX (`Θ_player`,
/// `element_mastery`, specimen level). `PointsFor` multiplies `sourceValue × rate` with no unit
/// conversion, so an accumulation there inverted the locked commander-smallest-to-unique-largest
/// ordering by 176× at ordinary play levels (species L12: 2,640 cumulative XP × 4 = 10,560, against
/// the commander's 20 × 3 = 60). The source is species LEVEL now, and <see cref="DemonTypeSourceFromLevel"/>
/// is the one place that derives it — never almanac XP passed directly.</para>
/// </summary>
public static class PointBudget
{
    /// <summary>The DemonType scope's own source-value transform (spec-point-economy.md §2, corrected
    /// 2026-09-05). Species level is an index, so it is <c>max(0, level − 1)</c> rather than the raw
    /// level — a never-levelled species (the default for every actor with no progression row) must
    /// carry EXACTLY ZERO points, or `demon-type-allocation`'s compose-at-read baseline would give
    /// every species in every fixture — including every battle and expedition golden — a non-empty
    /// allocation nobody authored. Subtraction happens before the cast/multiply so nothing negative
    /// ever reaches a `checked` context; `Math.Max` floors it at zero rather than throwing, since a
    /// level of 0 or 1 is ordinary (a fresh actor), not a validation failure. Callers pass the RESULT
    /// of this into <see cref="PointsFor"/> as `sourceValue` — `PointsFor` itself stays scope-agnostic
    /// and does not know what a "level" is, matching this type's own stated architecture.</summary>
    public static long DemonTypeSourceFromLevel(long speciesLevel) => Math.Max(0, speciesLevel - 1);

    /// <summary>
    /// The budget for one scope, given that scope's own source value. `long`: a budget is
    /// <c>rateMilli × sourceValue</c> — see the field-level remark on the naming.
    /// <see cref="AptitudePointEconomy.AptitudePointsPerThetaMilliByScope"/> for why this does NOT
    /// divide by 1000 despite the "Milli" name. Every input PS-8 leaves uncapped can reach this
    /// multiply; overflow throws via <c>checked</c>, never wraps (CLAUDE.md's numeric-overflow rule).
    /// <b>No cap anywhere</b> (PS-8, spec-point-economy.md §8 "Never cap an aptitude, or cap respecs"):
    /// a budget an actor earns more of is not a ceiling.
    /// </summary>
    public static long PointsFor(AllocationScope scope, long sourceValue, AptitudeTuning tuning)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (sourceValue < 0)
            throw new ArgumentOutOfRangeException(nameof(sourceValue), sourceValue, "a scope's source value cannot be negative");

        var rate = tuning.PointEconomy.AptitudePointsPerThetaMilliByScope[scope];
        checked { return sourceValue * rate; }
    }

    /// <summary>"Each scope draws from its own budget" (spec-point-economy.md §7 test 2) — what a
    /// build spent in ONE scope, checked against THAT scope's own budget alone. <see cref="Spent"/>
    /// and <see cref="Budget"/> are never combined with any other scope's before this comparison, so
    /// overspending one scope can never be covered by surplus in another.</summary>
    public readonly record struct ScopeCheck(long Spent, long Budget)
    {
        public bool WithinBudget => Spent <= Budget;
    }

    public static ScopeCheck CheckScope(AllocationScope scope, AptitudeAllocation allocation, long sourceValue, AptitudeTuning tuning)
    {
        if (allocation is null) throw new ArgumentNullException(nameof(allocation));
        return new ScopeCheck(allocation.TotalForScope(scope), PointsFor(scope, sourceValue, tuning));
    }
}
