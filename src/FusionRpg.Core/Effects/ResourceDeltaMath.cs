namespace FusionRpg.Core.Effects;

/// <summary>Pure clamp for FA10 current-HP add. Unity Writer calls this; tests do not need IL2CPP.</summary>
public static class ResourceDeltaMath
{
    public const int MailboxCap = 4096;
    public const long AmountCap = 1_000_000_000L;

    /// <summary>True when |amount| exceeds AmountCap. long.MinValue cannot Abs safely.</summary>
    public static bool ExceedsAmountCap(long amount) =>
        amount == long.MinValue || Math.Abs(amount) > AmountCap;

    /// <summary>live + delta, clamped to [0, max]. max &lt; 0 treated as 0.</summary>
    public static long Apply(long live, long delta, long max)
    {
        if (max < 0) max = 0;
        var next = live + delta;
        if (next > max) next = max;
        if (next < 0) next = 0;
        return next;
    }
}
