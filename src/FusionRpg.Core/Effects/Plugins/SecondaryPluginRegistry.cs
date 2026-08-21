namespace FusionRpg.Core.Effects.Plugins;

/// <summary>Default Secondary grant plugins for offline sim and LIVE Injector.</summary>
public static class SecondaryPluginRegistry
{
    public static IEnumerable<IEffectGrantPlugin> CreateDefault()
    {
        yield return new MatchButterSecondaryPlugin();
        yield return new MatchPassiveAtkSecondaryPlugin();
        yield return new PatronSecondaryPlugin();
    }
}
