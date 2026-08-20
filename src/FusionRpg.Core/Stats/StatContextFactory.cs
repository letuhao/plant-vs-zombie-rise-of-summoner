using FusionRpg.Contracts;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Core.Stats;

public sealed class StatContextFactory
{
    public StatContext ForPlant(
        string entityKey,
        EntityBaseline baseline,
        int typeId = 0,
        string? matchKey = null,
        long? playerId = null,
        StatsConfig? cheatScale = null,
        IReadOnlyDictionary<string, int>? cheatAbsolute = null,
        bool applyStats = true,
        IReadOnlyList<StatModifier>? pvzStatsMods = null,
        ActorElementTypes? elementTypes = null) =>
        new()
        {
            Side = StatSide.Plant,
            EntityKey = entityKey,
            Baseline = baseline,
            TypeId = typeId,
            MatchKey = matchKey,
            PlayerId = playerId,
            CheatScale = cheatScale,
            CheatAbsolute = cheatAbsolute,
            ApplyStats = applyStats,
            PvzStatsMods = pvzStatsMods,
            ElementTypes = elementTypes ?? ActorElementTypes.Neutral
        };

    public StatContext ForZombie(
        string entityKey,
        EntityBaseline baseline,
        int typeId = 0,
        string? matchKey = null,
        long? playerId = null,
        StatsConfig? cheatScale = null,
        IReadOnlyDictionary<string, int>? cheatAbsolute = null,
        bool applyStats = true,
        IReadOnlyList<StatModifier>? pvzStatsMods = null,
        ActorElementTypes? elementTypes = null) =>
        new()
        {
            Side = StatSide.Zombie,
            EntityKey = entityKey,
            Baseline = baseline,
            TypeId = typeId,
            MatchKey = matchKey,
            PlayerId = playerId,
            CheatScale = cheatScale,
            CheatAbsolute = cheatAbsolute,
            ApplyStats = applyStats,
            PvzStatsMods = pvzStatsMods,
            ElementTypes = elementTypes ?? ActorElementTypes.Neutral
        };
}
