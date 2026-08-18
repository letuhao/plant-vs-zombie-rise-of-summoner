namespace FusionRpg.Core.Stats.Derived;

public enum DerivedComposeKind
{
    FlatSum,
    FlatReplace,
    SumIncreased,
    MaxPriorityFlag
}

public sealed record DerivedStatDef(
    string ChannelId,
    DerivedComposeKind Compose,
    double DefaultValue,
    double? Cap = null);

/// <summary>Catalog SSOT for derived channels — unknown id → reject.</summary>
public sealed class DerivedStatRegistry
{
    readonly Dictionary<string, DerivedStatDef> _defs = new(StringComparer.Ordinal);

    public static DerivedStatRegistry CreateDefault()
    {
        var r = new DerivedStatRegistry();
        r.RegisterDefaults();
        return r;
    }

    void RegisterDefaults()
    {
        Register(new(DerivedStatChannels.ProgressionBonusMaxHp, DerivedComposeKind.FlatSum, 0));
        Register(new(DerivedStatChannels.ProgressionBonusAtk, DerivedComposeKind.FlatSum, 0));
        Register(new(DerivedStatChannels.ProgressionBonusDefense, DerivedComposeKind.FlatSum, 0));
        Register(new(DerivedStatChannels.ProgressionBonusArm1, DerivedComposeKind.FlatSum, 0));
        Register(new(DerivedStatChannels.ProgressionBonusArm2, DerivedComposeKind.FlatSum, 0));

        Register(new(DerivedStatChannels.ProgressionPower, DerivedComposeKind.FlatReplace, 1.0));
        Register(new(DerivedStatChannels.ProgressionRealm, DerivedComposeKind.FlatReplace, 1.0));

        Register(new(DerivedStatChannels.StatusPowerOmni, DerivedComposeKind.SumIncreased, 0));
        Register(new(DerivedStatChannels.StatusPowerDot, DerivedComposeKind.SumIncreased, 0));
        Register(new(DerivedStatChannels.StatusPowerCc, DerivedComposeKind.SumIncreased, 0));
        Register(new(DerivedStatChannels.StatusPowerContagion, DerivedComposeKind.SumIncreased, 0));

        Register(new(DerivedStatChannels.StatusResistOmni, DerivedComposeKind.SumIncreased, 0));
        Register(new(DerivedStatChannels.StatusResistDot, DerivedComposeKind.SumIncreased, 0, 0.95));
        Register(new(DerivedStatChannels.StatusResistCc, DerivedComposeKind.SumIncreased, 0, 0.95));
        Register(new(DerivedStatChannels.StatusResistContagion, DerivedComposeKind.SumIncreased, 0, 0.95));

        Register(new(DerivedStatChannels.CombatCritChance, DerivedComposeKind.SumIncreased, 0));
    }

    public void Register(DerivedStatDef def)
    {
        if (string.IsNullOrWhiteSpace(def.ChannelId))
            throw new ArgumentException("ChannelId required");
        _defs[def.ChannelId] = def;
    }

    public bool IsKnown(string channelId) => TryGet(channelId, out _);

    public bool TryGet(string channelId, out DerivedStatDef def) =>
        _defs.TryGetValue(channelId, out def!);

    public DerivedStatDef GetRequired(string channelId)
    {
        if (!TryGet(channelId, out var def))
            throw new UnknownDerivedChannelException(channelId);
        return def;
    }

    public bool TryResolveChannel(string channelId, out DerivedStatDef def)
    {
        if (TryGet(channelId, out def!))
            return true;

        if (channelId.StartsWith("status.power.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0);
            return true;
        }

        if (channelId.StartsWith("status.resist.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0, 0.95);
            return true;
        }

        if (channelId.StartsWith("status.immune.", StringComparison.Ordinal)
            && !channelId.StartsWith("status.immuneReduction.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.MaxPriorityFlag, 0, 1);
            return true;
        }

        if (channelId.StartsWith("status.immuneReduction.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.MaxPriorityFlag, 0, 1);
            return true;
        }

        if (channelId.StartsWith("status.expose.", StringComparison.Ordinal))
        {
            def = new DerivedStatDef(channelId, DerivedComposeKind.SumIncreased, 0);
            return true;
        }

        def = null!;
        return false;
    }

    public void ValidateChannel(string channelId)
    {
        if (!TryResolveChannel(channelId, out _))
            throw new UnknownDerivedChannelException(channelId);
    }

    public IReadOnlyCollection<DerivedStatDef> AllRegistered => _defs.Values.ToList();
}

public sealed class UnknownDerivedChannelException : Exception
{
    public string ChannelId { get; }

    public UnknownDerivedChannelException(string channelId)
        : base($"Unknown derived channel: {channelId}")
    {
        ChannelId = channelId;
    }
}
