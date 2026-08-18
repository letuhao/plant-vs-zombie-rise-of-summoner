namespace FusionRpg.Core.Progression;

/// <summary>
/// Future: scale kill XP by zombie power from spawn dump / type SSOT.
/// Stub always returns 1.0 until that power source exists.
/// </summary>
public static class RpgXpPowerScale
{
    public static double ForKill(int? zombieTypeId, string? payloadJson)
    {
        _ = zombieTypeId;
        _ = payloadJson;
        return 1.0;
    }
}
