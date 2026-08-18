using FusionRpg.Contracts;

namespace FusionRpg.Core.Stats;

public sealed class StatScope
{
    public string? MatchKey { get; init; }
    public long? PlayerId { get; init; }
    public string? EntityKey { get; init; }
}

public sealed class StatContext
{
    public StatSide Side { get; init; }
    public int TypeId { get; init; }
    public string EntityKey { get; init; } = "";
    public string? MatchKey { get; init; }
    public long? PlayerId { get; init; }
    public EntityBaseline Baseline { get; init; } = new();
    public bool ApplyStats { get; init; } = true;

    /// <summary>Legacy cheat / server stats input for CheatScaleStatPlugin.</summary>
    public StatsConfig? CheatScale { get; init; }

    /// <summary>Optional absolute overrides (Tab B/C) keyed by channel → value. Missing = no override.</summary>
    public IReadOnlyDictionary<string, int>? CheatAbsolute { get; init; }

    /// <summary>Enabled PvzStats modifiers for this resolve (hydrated by injector; Core stays DB-free).</summary>
    public IReadOnlyList<StatModifier>? PvzStatsMods { get; init; }

    public IReadOnlyDictionary<string, object> Tags { get; init; } =
        new Dictionary<string, object>(StringComparer.Ordinal);
}
