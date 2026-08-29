namespace FusionRpg.Core.Stats.Aptitudes;

/// <summary>
/// class-system-todo.md P6.3 — spec-point-economy.md §3: "the only friction left, and it must not be
/// a ban." Free build withdrew the class price, whose job was to make a build a commitment; nothing
/// replaced it except this. Deliberately NOT three things, each for the reason §3 states: not a
/// cooldown (punishes being away from the game); not a cap on respec count (PS-8 — a hard progression
/// ceiling); not free (a free respec makes a build a menu selection, not a commitment, and every
/// fight is fought with the optimal counter).
///
/// <para><b>Placeholder resource choice, not a code default masquerading as a decision</b> — §8 marks
/// "which resource respec costs" an "Ask first," a mechanism choice this module cannot make alone.
/// <see cref="Resource"/> is a documented placeholder (Hunger — the closest existing "a resource
/// fighting also costs": resource-hub's own framing has hunger spent by regenerating the other pools,
/// which fighting drains), not a tuning value, because WHICH pool is a structural/mechanism decision
/// (like <see cref="AllocationScope"/> itself), not a magnitude a balance pass would dial — only the
/// AMOUNT is (§6: "carries no bare literal — every number is a named tunable"), and that one lives in
/// <see cref="AptitudePointEconomy.RespecPrice"/>.</para>
/// </summary>
public enum RespecResource { Hunger }

public readonly record struct RespecPrice(RespecResource Resource, long Amount);

public static class RespecPolicy
{
    /// <summary>Always available, always priced, never refused, never a cooldown, never a cap
    /// (spec-point-economy.md §3/§7 test 6/§8). There is no "cannot respec" return here on purpose —
    /// a caller that wants to know the price before paying it calls this; whether the payer CAN
    /// afford <see cref="RespecPrice.Amount"/> right now is that resource's own concern (a stamina/
    /// hunger/qi pool check), never this policy's — this type only ever answers "what does it cost,"
    /// never "are you allowed."</summary>
    public static RespecPrice PriceOf(AptitudeTuning tuning)
    {
        if (tuning is null) throw new ArgumentNullException(nameof(tuning));
        return new RespecPrice(RespecResource.Hunger, tuning.PointEconomy.RespecPrice);
    }
}
