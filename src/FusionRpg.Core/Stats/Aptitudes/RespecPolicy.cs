using FusionRpg.Core.Demons.Generation;

namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>
/// species-build-todo.md T4.1 — spec-species-respec.md, read in full this session. Prices CHURN, not
/// investment (decision 15, replacing decision 9's level-scaled price after audit finding A2 showed
/// species level and soul income don't relate the way that formula assumed). Deliberately NOT three
/// things, same reasoning class-system-todo.md P6.3 already established for the original placeholder:
/// not a cooldown (a cooldown forbids; this only prices, and the decay means being away makes it
/// CHEAPER — the opposite of the "punishes being away" failure a cooldown would cause); not a cap on
/// respec count (PS-8); not free (a free respec makes a build a menu selection, not a commitment).
///
/// <para><b>Soul, not Hunger</b> — spec-point-economy.md §8's "Ask first: which resource respec costs"
/// is answered by spec-species-respec.md's own decision 1. The prior <see cref="RespecResource.Hunger"/>
/// value was an explicitly documented placeholder pending that answer, not a shipped default.</para>
///
/// <para><b>Count, never level</b> — <see cref="PriceOf"/> takes the caller's own persisted respec
/// counter (<c>RpgStore.SpeciesRespec.cs</c>, T4.2) as an argument; this policy holds no state and does
/// not know or care which species is being repriced, matching <see cref="RespecPrice"/>'s own
/// unscoped-by-<see cref="AllocationScope"/> shape from the original design.</para>
/// </summary>
public enum RespecResource { Soul }

public readonly record struct RespecPrice(RespecResource Resource, long Amount);

public static class RespecPolicy
{
    /// <summary>`price(count) = basePrice + basePrice × count × escalationPermille / 1000` — linear,
    /// not geometric (spec's own reasoning: geometric escalation against a flat soul faucet is how a
    /// price becomes a ceiling, and soul income is flat today per <c>RpgStore.Souls.cs</c>'s Θ pin).
    /// Widened to `long` throughout and divided by 1000 last, exactly once (CLAUDE.md's overflow
    /// rules); <c>checked</c> so a runaway count throws rather than wraps. Always available, always
    /// priced, never refused — there is no "cannot respec" return here on purpose, exactly like the
    /// policy this replaces.</summary>
    public static RespecPrice PriceOf(SpeciesBuildTuning tuning, long count)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count), count, "respec count cannot be negative");

        checked
        {
            var amount = tuning.RespecBasePrice
                + tuning.RespecBasePrice * count * tuning.RespecEscalationPermille / 1000;
            return new RespecPrice(RespecResource.Soul, amount);
        }
    }
}
