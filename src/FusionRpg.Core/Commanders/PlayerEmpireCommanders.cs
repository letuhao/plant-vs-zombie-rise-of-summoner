namespace FusionRpg.Core.Commanders;

/// <summary>
/// Player-empire commanders exposed through <c>/api/commanders</c> — today Dave only; Zomboss is
/// world/AI, never listed here (commander-surface ideal §4).
/// </summary>
public static class PlayerEmpireCommanders
{
    public static IReadOnlyList<CommanderId> ForPlayer(long playerId) =>
        playerId > 0 ? new[] { CommanderId.Dave } : Array.Empty<CommanderId>();

    public static string DisplayName(CommanderId id) => id switch
    {
        CommanderId.Dave => "Crazy Dave",
        CommanderId.Zomboss => "Dr. Zomboss",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, "unknown commander id"),
    };

    public static bool IsPlayerDefaultAllowed(CommanderId id) => id == CommanderId.Dave;
}
