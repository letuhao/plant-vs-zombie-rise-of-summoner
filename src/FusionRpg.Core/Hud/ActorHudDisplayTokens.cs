namespace FusionRpg.Core.Hud;

/// <summary>
/// Shared display tokens for Unity TextMesh and Phaser/Inspector initials
/// (actor-hud-unity visual correction; mirrors web actorHudDisplayTokens).
/// </summary>
public static class ActorHudDisplayTokens
{
    public static string StatusInitials(string? id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "?";
        var parts = id.Split('_', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
            return (char.ToUpperInvariant(parts[0][0]) + "" + char.ToUpperInvariant(parts[1][0]));
        var s = id.Trim();
        if (s.Length >= 2) return s[..2].ToUpperInvariant();
        return s.ToUpperInvariant();
    }

    public static string TierLetter(ActorHudTier tier) => tier switch
    {
        ActorHudTier.Unique => "U",
        ActorHudTier.Elite => "E",
        ActorHudTier.Boss => "B",
        _ => ""
    };

    public static string TierLetter(string? tier)
    {
        if (string.IsNullOrWhiteSpace(tier)) return "";
        return tier.Trim().ToLowerInvariant() switch
        {
            "unique" => "U",
            "elite" => "E",
            "boss" => "B",
            _ => ""
        };
    }
}
