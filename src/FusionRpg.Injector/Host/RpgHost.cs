using HarmonyLib;

namespace FusionRpg.Injector.Host;

/// <summary>
/// Global facade for host services. Hooks/Writer must use this — never BepInEx or MelonLoader types.
/// </summary>
public static class RpgHost
{
    public const string HarmonyId = "com.fusionrpg.injector";
    public const string DefaultServerUrl = "http://127.0.0.1:5088";

    static IRpgLog _log = NullRpgLog.Instance;
    static IRpgConfig _config = new DefaultRpgConfig();
    static string _pluginDir = "";
    static string _serverUrl = DefaultServerUrl;
    static string _gameProfileId = FusionRpg.Contracts.RpgConstants.GameId381;
    static FusionRpg.Core.Overlay.OverlayHostMode _overlayHost = FusionRpg.Core.Overlay.OverlayHostMode.Launcher;

    public static IRpgLog Log => _log;
    public static IRpgConfig Config => _config;
    public static string PluginDir => _pluginDir;
    public static string ServerUrl => _serverUrl;
    /// <summary>Which process owns the web overlay window. Default Launcher.</summary>
    public static FusionRpg.Core.Overlay.OverlayHostMode OverlayHost => _overlayHost;
    /// <summary>Active game profile id (e.g. pvzrh-3.8.1) from compile-time bridge.</summary>
    public static string GameProfileId => _gameProfileId;
    public static RpgClient? Client { get; set; }
    /// <summary>Harmony instance (fully qualified — MelonLoader also exposes a Harmony namespace).</summary>
    public static HarmonyLib.Harmony? Harmony { get; set; }
    public static bool EnableUnsafeHitPatches { get; private set; }
    public static bool IsInitialized { get; private set; }

    /// <summary>Wire host adapters before <see cref="InjectorBootstrap.Start"/>.</summary>
    public static void Initialize(IRpgLog log, IRpgConfig config, string pluginDir)
    {
        _log = log ?? NullRpgLog.Instance;
        _config = config ?? new DefaultRpgConfig();
        _pluginDir = pluginDir ?? "";

        var envUrl = Environment.GetEnvironmentVariable("FUSIONRPG_SERVER_URL");
        var cfgUrl = _config.ServerUrl;
        _serverUrl = !string.IsNullOrWhiteSpace(envUrl)
            ? envUrl.Trim().TrimEnd('/')
            : (string.IsNullOrWhiteSpace(cfgUrl) ? DefaultServerUrl : cfgUrl.Trim().TrimEnd('/'));

        _overlayHost = FusionRpg.Core.Overlay.OverlayHostSelection.Resolve(
            Environment.GetEnvironmentVariable(FusionRpg.Core.Overlay.OverlayHostSelection.EnvVar),
            _config.OverlayHost);

        EnableUnsafeHitPatches = _config.EnableUnsafeHitPatches;
        try { _gameProfileId = Bridges.ZombieCombatFields.ProfileId; }
        catch { _gameProfileId = FusionRpg.Contracts.RpgConstants.GameId381; }
        IsInitialized = true;
    }

    /// <summary>No-op — no in-game overlay. Telemetry goes to Log / web events.</summary>
    public static void Note(string line) { }
}

/// <summary>Defaults when host has not bound config yet.</summary>
public sealed class DefaultRpgConfig : IRpgConfig
{
    public string ServerUrl => RpgHost.DefaultServerUrl;
    public string OverlayHost => "launcher";
    public bool PersistCheats => false;
    public bool EnableUnsafeHitPatches => false;
}
