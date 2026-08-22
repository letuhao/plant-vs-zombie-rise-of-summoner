namespace FusionRpg.Injector.Host;

/// <summary>Host-agnostic config — BepInEx Config.Bind or MelonPreferences / fusionrpg.cfg.</summary>
public interface IRpgConfig
{
    string ServerUrl { get; }

    /// <summary>"launcher" (default) or "injector". FUSIONRPG_OVERLAY_HOST wins over this.</summary>
    string OverlayHost { get; }
    bool PersistCheats { get; }
    bool EnableUnsafeHitPatches { get; }
}
