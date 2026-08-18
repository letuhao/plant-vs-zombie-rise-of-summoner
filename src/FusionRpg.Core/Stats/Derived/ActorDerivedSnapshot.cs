namespace FusionRpg.Core.Stats.Derived;

/// <summary>Resolved derived channel bag for one actor at Apply time.</summary>
public sealed class ActorDerivedSnapshot
{
    readonly Dictionary<string, double> _channels = new(StringComparer.Ordinal);

    public static ActorDerivedSnapshot Empty { get; } = new();

    public static ActorDerivedSnapshot FromValues(IEnumerable<KeyValuePair<string, double>> values)
    {
        var s = new ActorDerivedSnapshot();
        foreach (var (k, v) in values)
            s._channels[k] = v;
        return s;
    }

    public static ActorDerivedSnapshot StubNeutral() =>
        FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 1.0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 1.0)
        });

    public static ActorDerivedSnapshot AttackerLess() =>
        FromValues(new[]
        {
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionPower, 0),
            new KeyValuePair<string, double>(DerivedStatChannels.ProgressionRealm, 0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerOmni, 0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerDot, 0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerCc, 0),
            new KeyValuePair<string, double>(DerivedStatChannels.StatusPowerContagion, 0)
        });

    public IReadOnlyDictionary<string, double> Channels => _channels;

    public double Get(string channelId, double defaultValue = 0) =>
        _channels.TryGetValue(channelId, out var v) ? v : defaultValue;

    public bool TryGet(string channelId, out double value) =>
        _channels.TryGetValue(channelId, out value);

    public double TierPower =>
        Get(DerivedStatChannels.ProgressionPower, 1.0) * Get(DerivedStatChannels.ProgressionRealm, 1.0);

    public ActorDerivedSnapshot Overlay(IEnumerable<KeyValuePair<string, double>> extra)
    {
        var next = FromValues(_channels);
        foreach (var (k, v) in extra)
            next._channels[k] = v;
        return next;
    }

    internal void Set(string channelId, double value) => _channels[channelId] = value;
}
