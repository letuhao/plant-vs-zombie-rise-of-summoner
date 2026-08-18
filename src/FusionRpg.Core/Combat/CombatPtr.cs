namespace FusionRpg.Core.Combat;

/// <summary>
/// Canonical entity ptr for snapshots, Funnel keys, and FA10 lookup.
/// Strips <c>entity:</c> and <c>0x</c>; comparison is case-insensitive.
/// </summary>
public static class CombatPtr
{
    public static string Normalize(string? ptr)
    {
        if (string.IsNullOrWhiteSpace(ptr)) return "";
        var s = ptr.Trim();
        if (s.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
            s = s[7..];
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            s = s[2..];
        return s.ToLowerInvariant();
    }

    public static bool EqualsPtr(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
}
