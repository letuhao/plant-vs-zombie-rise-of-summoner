using FusionRpg.Core.Status;
using FusionRpg.Core.Stats;
using FusionRpg.Core.Stats.Derived;

namespace FusionRpg.Injector.Stats;

/// <summary>Injector progression level cache — hydrated from server/RpgProgressionUpdated later.</summary>
public sealed class InjectorProgressionPowerProvider : IProgressionPowerProvider
{
    readonly Dictionary<string, int> _levels = new(StringComparer.OrdinalIgnoreCase);

    public void SetLevel(long? playerId, StatSide side, int typeId, int level)
    {
        var key = Key(playerId, side, typeId);
        if (level <= 0)
            _levels.Remove(key);
        else
            _levels[key] = level;
    }

    public void Clear() => _levels.Clear();

    public int GetLevel(StatContext ctx)
    {
        var key = Key(ctx.PlayerId, ctx.Side, ctx.TypeId);
        return _levels.TryGetValue(key, out var level) ? level : 0;
    }

    public double GetPower(StatContext ctx) =>
        ProgressionPowerCurve.PowerFromLevel(GetLevel(ctx));

    public double GetRealm(StatContext ctx) => StatusPolicy.ProgressionPowerStubDefault;

    static string Key(long? playerId, StatSide side, int typeId) =>
        (playerId ?? 0) + ":" + side + ":" + typeId;
}
