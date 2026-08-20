namespace FusionRpg.Injector.Effects;

/// <summary>Feature gate for overlay combat calculator — env or cheat toggle.</summary>
public static class OverlayCombatFeature
{
    public const string CheatToggleId = "OVERLAY-COMBAT";
    public const string EnvVar = "FUSIONRPG_OVERLAY_COMBAT";

    public static bool Enabled =>
        string.Equals(Environment.GetEnvironmentVariable(EnvVar), "1", StringComparison.Ordinal)
        || CheatState.On(CheatToggleId);
}
