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

        if (ctx.CheatAbsolute != null)
            foreach (var kv in ctx.CheatAbsolute)
            {
                if (kv.Value < 0) continue; // convention: negative = skip
                bag.Upsert(_mods.Override(Id, Id, "tabBC", kv.Key, kv.Value, priority: 100));
            }

        // E16: the three real-valued channels — attackInterval, produceInterval, zombieSpeed. They
        // used to be written straight to the Unity field from their cheat keys, behind the
        // composer's back; now they arrive here as Overrides like P-HP and P-ATK always have, so
        // there is one path to the field and the single-writer law holds.
        if (ctx.CheatAbsoluteReal != null)
            foreach (var kv in ctx.CheatAbsoluteReal)
            {
                if (kv.Value <= 0) continue; // same convention, and a zero interval is never a value
                bag.Upsert(_mods.Override(Id, Id, "tabBC", kv.Key, kv.Value, priority: 100));
            }
    }
}
