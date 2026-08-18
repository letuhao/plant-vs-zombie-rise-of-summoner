using FusionRpg.Contracts;

namespace FusionRpg.Injector.Bridges;

/// <summary>Game-profile combat field access for zombie HP (Int64 on 3.9).</summary>
public static class ZombieCombatFields
{
    public static string ProfileId => RpgConstants.GameId39;

    public static long GetHp(Zombie z) => z.theHealth;
    public static long GetMaxHp(Zombie z) => z.theMaxHealth;

    public static void SetHp(Zombie z, long hp) => z.theHealth = hp;
    public static void SetMaxHp(Zombie z, long max) => z.theMaxHealth = max;

    public static long GetCurrentAllHealth(Zombie z) => z.CurrentAllHealth;
    public static long GetTotalAllHealth(Zombie z) => z.TotalAllHealth;
    public static long GetCurrentFirstHealth(Zombie z) => z.CurrentFirstHealth;

    /// <summary>3.9 exposes Int64; dumps use double for continuity with 3.8.1 Single.</summary>
    public static double GetTotalFirstHealthNumber(Zombie z) => z.TotalFirstHealth;

    public static int ClampToInt32(long value)
    {
        if (value > int.MaxValue) return int.MaxValue;
        if (value < int.MinValue) return int.MinValue;
        return (int)value;
    }
}
