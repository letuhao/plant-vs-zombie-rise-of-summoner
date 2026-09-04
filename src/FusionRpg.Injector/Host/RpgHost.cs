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

        // tunables-ssot.md §7.2: the injector loads data/tuning/ and injects it; Core never reads a
        // file. Copied next to the built plugin DLL by the host .csproj (BepInEx / MelonLoader).
        var tuningDir = System.IO.Path.Combine(_pluginDir, "data", "tuning");
        FusionRpg.Core.Demons.Contracts.ContractPolicy.Configure(
            FusionRpg.Core.Demons.Contracts.ContractTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "contracts.v1.json"))));
        FusionRpg.Core.World.Loam.LoamPolicy.Configure(
            FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "loam.v1.json"))));
        var worldTuning = FusionRpg.Core.World.WorldTuningLoader.Parse(
            System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "world.v4.json")));
        FusionRpg.Core.World.WorldTuningHub.Configure(worldTuning);
        FusionRpg.Core.World.Growth.RecruitPolicy.Configure(worldTuning.Growth);
        FusionRpg.Core.Demons.SoulEarnPolicy.Configure(
            FusionRpg.Core.Demons.SoulEarnTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "souls.v1.json"))));
        FusionRpg.Core.Demons.Patron.PatronPolicy.Configure(
            FusionRpg.Core.Demons.Patron.PatronTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "patron.v1.json"))));
        FusionRpg.Core.Combat.Shield.ShieldPolicy.Configure(
            FusionRpg.Core.Combat.Shield.ShieldTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "shield.v1.json"))));
        FusionRpg.Core.Combat.CombatPolicy.Configure(
            FusionRpg.Core.Combat.CombatTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "combat.v1.json"))));
        FusionRpg.Core.Demons.Fusion.StarPolicy.Configure(
            FusionRpg.Core.Demons.Fusion.FusionTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "fusion.v1.json"))));
        FusionRpg.Core.Status.StatusPolicy.Configure(
            FusionRpg.Core.Status.StatusTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "status.v1.json"))));
        FusionRpg.Core.Stats.Derived.DerivedStatPolicy.Configure(
            FusionRpg.Core.Stats.Derived.DerivedStatTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "derived-stats.v2.json"))));
        // T4.7 step 2 / T4.8 (catalog-runtime): behaviour-preserving today — the SAME compiled
        // roster `DemonSpeciesCatalog.All` always read, now routed through Configure. NOT the
        // store-backed flip (SpeciesSnapshot.cs's own doc comment says why).
        FusionRpg.Core.Demons.DemonSpeciesCatalog.ConfigureFromCompiledDefault();
        FusionRpg.Core.Overlay.OverlayTuningHub.Configure(
            FusionRpg.Core.Overlay.OverlayTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "overlay.v1.json"))));
        FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(
            FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "stats.v1.json"))));
        FusionRpg.Core.Expeditions.ExpeditionTuningHub.Configure(
            FusionRpg.Core.Expeditions.ExpeditionTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "expeditions.v1.json"))));
        FusionRpg.Core.Match.MatchTuningPolicy.Configure(
            FusionRpg.Core.Match.MatchTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "match.v1.json"))));
        FusionRpg.Core.Effects.EffectsTuningHub.Configure(
            FusionRpg.Core.Effects.EffectsTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "effects.v1.json"))));
        FusionRpg.Core.Net.NetPolicy.Configure(
            FusionRpg.Core.Net.NetTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "net.v1.json"))));
        FusionRpg.Core.Vfx.VfxTuningHub.Configure(
            FusionRpg.Core.Vfx.VfxTuningLoader.Parse(
                // v2 -> v3 (2026-08-30, UnitFrame): sustained.spanScale + render.sortOffsetAboveUnit.
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "vfx.v3.json"))));
        FusionRpg.Core.Power.PowerTuningHub.Configure(
            FusionRpg.Core.Power.PowerTuningLoader.Parse(
                // T4.2 (power-dial, 2026-08-24): v1 (bMilli=0) -> v2 (bMilli=400). v1 stays on disk --
                // reverting is pointing this back at power-scale.v1.json and un-bumping RulesetVersion.
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "power-scale.v2.json"))));
        FusionRpg.Core.Stats.Aptitudes.AptitudeTuningHub.Configure(
            FusionRpg.Core.Stats.Aptitudes.AptitudeTuningLoader.Parse(
                // class-system-todo.md P8.2/P8.3 (2026-08-27): v1 -> v2. Phase 0 six-resource coverage (2026-09-02): v2 -> v3, then v3 -> v4 (0.8: combat.heal.power generalised to resource.restore.{resource}) -- 32 edges added so every (family x resource) cell is fed, closing P7.2's poise gap. v2 stays on disk -- reverting is pointing this back at aptitudes.v2.json.
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "aptitudes.v5.json"))));
        FusionRpg.Core.Hud.ActorHudTuningHub.Configure(
            FusionRpg.Core.Hud.ActorHudTuningLoader.Parse(
                System.IO.File.ReadAllText(System.IO.Path.Combine(tuningDir, "actor-hud.v1.json"))));

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
