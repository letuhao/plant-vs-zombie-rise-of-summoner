namespace FusionRpg.Core.Stats.Plugins;

public sealed class ClassStatPlugin : IStatModifierPlugin
{
    public const string Id = "rpg.class";
    public string PluginId => Id;
    public int Order => 100;
    public void Contribute(StatContext ctx, IModifierBagEditor bag) { }
}

public sealed class AchievementStatPlugin : IStatModifierPlugin
{
    public const string Id = "rpg.achievement";
    public string PluginId => Id;
    public int Order => 200;
    public void Contribute(StatContext ctx, IModifierBagEditor bag) { }
}

public sealed class ItemStatPlugin : IStatModifierPlugin
{
    public const string Id = "rpg.item";
    public string PluginId => Id;
    public int Order => 300;
    public void Contribute(StatContext ctx, IModifierBagEditor bag) { }
}

public sealed class BuffStatPlugin : IStatModifierPlugin
{
    public const string Id = "rpg.buff";
    public string PluginId => Id;
    public int Order => 400;
    public void Contribute(StatContext ctx, IModifierBagEditor bag) { }
}
