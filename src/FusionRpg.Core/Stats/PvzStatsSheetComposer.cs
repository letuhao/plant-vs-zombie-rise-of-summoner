namespace FusionRpg.Core.Stats;

/// <summary>
/// Monitor-only sheet: Compose(Y0=0, bag). Not combat entity finals.
/// </summary>
public static class PvzStatsSheetComposer
{
    static readonly string[] Channels =
    {
        StatChannels.Hp, StatChannels.MaxHp, StatChannels.Atk, StatChannels.Defense,
        StatChannels.Arm1, StatChannels.Arm1Max, StatChannels.Arm2, StatChannels.Arm2Max
    };

    /// <summary>Canonical StatChannels id, or null if unknown.</summary>
    public static string? TryCanonicalizeChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;
        var trimmed = channel.Trim();
        foreach (var c in Channels)
        {
            if (string.Equals(c, trimmed, StringComparison.OrdinalIgnoreCase))
                return c;
        }
        return null;
    }

    public static bool IsKnownChannel(string? channel) => TryCanonicalizeChannel(channel) != null;

    public static PvzStatsSheetResult Build(IEnumerable<StatModifier> mods)
    {
        var list = mods.Where(m => m != null).ToList();
        var strategy = new PhasedComposeStrategy();
        var channels = new List<PvzStatsChannelResult>();
        foreach (var ch in Channels)
        {
            var channelMods = list.Where(m => m.Channel == ch).ToList();
            if (channelMods.Count == 0) continue;
            var final = strategy.ComposeChannel(0, channelMods);
            channels.Add(new PvzStatsChannelResult
            {
                Channel = ch,
                Final = final,
                SourceCount = channelMods
                    .Select(m => $"{m.SourceKind}|{m.SourceId}")
                    .Distinct(StringComparer.Ordinal)
                    .Count(),
                Contributions = channelMods
            });
        }
        return new PvzStatsSheetResult { Channels = channels, Contributions = list };
    }

    public static ModifierOp ParseOp(string? op)
    {
        if (string.IsNullOrWhiteSpace(op)) return ModifierOp.Flat;
        return Enum.TryParse<ModifierOp>(op, ignoreCase: true, out var parsed) ? parsed : ModifierOp.Flat;
    }

    public static StatModifier ToStatModifier(
        string pluginId, string sourceKind, string sourceId, string channel, string op, double value, int priority) =>
        new()
        {
            PluginId = pluginId,
            SourceKind = sourceKind,
            SourceId = sourceId,
            Channel = channel,
            Op = ParseOp(op),
            Value = value,
            Priority = priority
        };
}

public sealed class PvzStatsSheetResult
{
    public IReadOnlyList<PvzStatsChannelResult> Channels { get; init; } = Array.Empty<PvzStatsChannelResult>();
    public IReadOnlyList<StatModifier> Contributions { get; init; } = Array.Empty<StatModifier>();
}

public sealed class PvzStatsChannelResult
{
    public string Channel { get; init; } = "";
    public double Final { get; init; }
    public int SourceCount { get; init; }
    public IReadOnlyList<StatModifier> Contributions { get; init; } = Array.Empty<StatModifier>();
}
