namespace FusionRpg.Core.Stats.Derived;

/// <summary>Pure derived compose — separate from primary StatComposer.</summary>
public sealed class DerivedComposer
{
    readonly DerivedStatRegistry _registry;

    public DerivedComposer(DerivedStatRegistry? registry = null) =>
        _registry = registry ?? DerivedStatRegistry.CreateDefault();

    public DerivedStatRegistry Registry => _registry;

    public ActorDerivedSnapshot Compose(IEnumerable<DerivedModifier>? modifiers = null)
    {
        var mods = (modifiers ?? Array.Empty<DerivedModifier>()).ToList();
        var snapshot = new ActorDerivedSnapshot();
        var channels = new HashSet<string>(StringComparer.Ordinal);

        foreach (var def in _registry.AllRegistered)
            channels.Add(def.ChannelId);
        foreach (var m in mods)
        {
            _registry.ValidateChannel(m.ChannelId);
            channels.Add(m.ChannelId);
        }

        foreach (var channelId in channels)
        {
            if (!_registry.TryResolveChannel(channelId, out var def))
                continue;
            var channelMods = mods.Where(m => string.Equals(m.ChannelId, channelId, StringComparison.Ordinal)).ToList();
            snapshot.Set(channelId, ComposeChannel(def, channelMods));
        }

        return snapshot;
    }

    static double ComposeChannel(DerivedStatDef def, IReadOnlyList<DerivedModifier> mods)
    {
        return def.Compose switch
        {
            DerivedComposeKind.FlatSum => def.DefaultValue + mods.Where(m => m.Op == DerivedModifierOp.Flat).Sum(m => m.Value),
            DerivedComposeKind.FlatReplace => ComposeFlatReplace(def.DefaultValue, mods),
            DerivedComposeKind.SumIncreased => Cap(def, def.DefaultValue + mods.Where(m => m.Op == DerivedModifierOp.Increased).Sum(m => m.Value)),
            DerivedComposeKind.MaxPriorityFlag => ComposeMaxFlag(def.DefaultValue, mods),
            _ => def.DefaultValue
        };
    }

    static double ComposeFlatReplace(double baseline, IReadOnlyList<DerivedModifier> mods)
    {
        var replaces = mods.Where(m => m.Op == DerivedModifierOp.Replace)
            .OrderByDescending(m => m.Priority)
            .ThenBy(m => m.SourceId, StringComparer.Ordinal)
            .ToList();
        if (replaces.Count > 0)
            return replaces[0].Value;
        var flats = mods.Where(m => m.Op == DerivedModifierOp.Flat).Sum(m => m.Value);
        return baseline + flats;
    }

    static double ComposeMaxFlag(double baseline, IReadOnlyList<DerivedModifier> mods)
    {
        var flags = mods.Where(m => m.Op is DerivedModifierOp.Flag or DerivedModifierOp.Replace or DerivedModifierOp.Increased)
            .Select(m => m.Value)
            .DefaultIfEmpty(baseline);
        var max = flags.Max();
        return max;
    }

    static double Cap(DerivedStatDef def, double value) =>
        def.Cap.HasValue ? Math.Min(value, def.Cap.Value) : value;
}
