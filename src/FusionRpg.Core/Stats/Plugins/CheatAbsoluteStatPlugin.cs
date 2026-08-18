namespace FusionRpg.Core.Stats.Plugins;

/// <summary>Tab B/C absolute writers — Override phase per channel.</summary>
public sealed class CheatAbsoluteStatPlugin : IStatModifierPlugin
{
    public const string Id = "cheat.absolute";
    public string PluginId => Id;
    public int Order => 950;

    readonly StatModifierFactory _mods = new();

    public void Contribute(StatContext ctx, IModifierBagEditor bag)
    {
        bag.WithdrawPlugin(Id);
        if (ctx.CheatAbsolute == null || ctx.CheatAbsolute.Count == 0) return;

        foreach (var kv in ctx.CheatAbsolute)
        {
            if (kv.Value < 0) continue; // convention: negative = skip
            bag.Upsert(_mods.Override(Id, Id, "tabBC", kv.Key, kv.Value, priority: 100));
        }
    }
}
