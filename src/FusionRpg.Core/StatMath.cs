using FusionRpg.Contracts;

namespace FusionRpg.Core;

public static class StatMath
{
    public static int ScaleHpOrAtk(int baseline, float percent, int flat) =>
        Math.Max(1, (int)Math.Round(baseline * percent) + flat);

    public static long ScaleHp(long baseline, float percent, int flat) =>
        Math.Max(1L, (long)Math.Round(baseline * percent) + flat);

    public static int ScaleIncoming(int damage, float defensePercent, int defenseFlat)
    {
        var p = defensePercent <= 0 ? 1f : defensePercent;
        return Math.Max(0, (int)Math.Round(damage / (double)p) - defenseFlat);
    }

    public static bool IsIdentity(StatMod m) =>
        Math.Abs(m.HpPercent - 1f) < 0.0001f && m.HpFlat == 0
        && Math.Abs(m.AttackPercent - 1f) < 0.0001f && m.AttackFlat == 0
        && Math.Abs(m.DefensePercent - 1f) < 0.0001f && m.DefenseFlat == 0;
}
