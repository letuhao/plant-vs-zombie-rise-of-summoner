namespace FusionRpg.Core.Expeditions;

/// <summary>
/// One expedition tier (spec-expeditions.md §Timeline): a fixed tick timeline with evenly
/// spaced battles; the 20h tier ends on a boss wave. Slots gate squad size at dispatch.
/// </summary>
public sealed record ExpeditionTierDef(
    string TierId, string Name, int DurationMinutes, int TickCount, int BattleCount, int SquadSlots, bool HasBossWave)
{
    public int TickMinutes => DurationMinutes / TickCount;
}

public static class ExpeditionTierCatalog
{
    public static readonly IReadOnlyList<ExpeditionTierDef> All = new[]
    {
        new ExpeditionTierDef("scout-30m", "Scouting Sortie", 30, 6, 1, 2, false),
        new ExpeditionTierDef("forage-4h", "Soul Foraging", 240, 8, 2, 3, false),
        new ExpeditionTierDef("hunt-8h", "Rift Hunt", 480, 8, 3, 4, false),
        new ExpeditionTierDef("warpath-20h", "Warpath", 1200, 10, 4, 5, true)
    };

    static readonly Dictionary<string, ExpeditionTierDef> ById =
        All.ToDictionary(t => t.TierId, StringComparer.Ordinal);

    public static bool IsKnown(string? tierId) => tierId != null && ById.ContainsKey(tierId);

    public static ExpeditionTierDef Get(string tierId) =>
        ById.TryGetValue(tierId, out var def)
            ? def
            : throw new ArgumentException($"Unknown expedition tier id '{tierId}'.");
}
