namespace FusionRpg.Injector.Host;

/// <summary>Host-agnostic config — BepInEx Config.Bind or MelonPreferences / fusionrpg.cfg.</summary>
public interface IRpgConfig
{
    string ServerUrl { get; }
    bool PersistCheats { get; }
    bool EnableUnsafeHitPatches { get; }
}
