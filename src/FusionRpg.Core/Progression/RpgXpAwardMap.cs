using FusionRpg.Core.Activity;

namespace FusionRpg.Core.Progression;

/// <summary>Maps Activity fact kinds to XP awards (pure; used by server apply).</summary>
public static class RpgXpAwardMap
{
    /// <summary>
    /// One XP award. <c>Delta</c> is whole XP (`long`) — the ONE place a scaled award is rounded, so
    /// no fraction ever reaches the persisted total. <c>PowerScale</c> stays `double`: it is carried
    /// into the kill ledger's audit payload and is never itself a magnitude.
    /// </summary>
    public readonly record struct Award(string Kind, int TypeId, long Delta, string Reason, double PowerScale = 1.0);

    /// <summary>
    /// The single rounding point for a scaled award — half away from zero, matching
    /// <c>PowerLadder</c>'s own end-rounding convention so both ladders round the same direction.
    /// Today <see cref="NoKillPowerScaleYet"/> is exactly 1.0 and this is the identity; it exists so
    /// that when content-scale supplies a real multiplier the fraction dies here rather than in the
    /// XP total (progression-shape-audit-2026-09-04.md §4.1).
    /// </summary>
    static long ScaledAward(long baseAward, double scale)
    {
        var scaled = baseAward * scale;
        if (double.IsNaN(scaled) || double.IsInfinity(scaled))
            throw new InvalidOperationException($"XP award scaled to a non-finite value: {baseAward} x {scale}");
        return checked((long)Math.Round(scaled, MidpointRounding.AwayFromZero));
    }

    // T3.3 (power-plan.md, done 2026-08-24): RpgXpPowerScale deleted -- its documented future job
    // ("scale kill XP by zombie power") is exactly what Theta_content does (content-scale, T3.4+).
    // Structural, not a balance tunable: it is the multiplicative identity, required so this
    // deletion changes zero observable behaviour until content-scale gives it a real value.
    // Award.PowerScale itself stays -- RpgStore.Progression.cs still reads it into the kill ledger's
    // audit payload (rpg-progression.md: "powerScale for future audit"), a separate consumer from
    // the XP multiply this constant used to feed.
    static readonly double NoKillPowerScaleYet = 1.0;

    public static IReadOnlyList<Award> FromActivity(string factKind, string? resultRaw, int? typeId, string? payloadJson = null)
    {
        switch (factKind)
        {
            case PvzActivityKinds.ZombieKilled:
            {
                var scale = NoKillPowerScaleYet;
                return new[]
                {
                    new Award(RpgActorKinds.Player, 0, ScaledAward(RpgXpAwards.Kill, scale), RpgXpReasons.Kill, scale)
                };
            }
            case PvzActivityKinds.MatchEnded:
                var result = PvzActivityKinds.NormalizeMatchResult(resultRaw);
                if (result == "defeat")
                    return new[] { new Award(RpgActorKinds.Player, 0, RpgXpAwards.Defeat, RpgXpReasons.Defeat) };
                return Array.Empty<Award>();
            case PvzActivityKinds.MowerUsed:
                return new[] { new Award(RpgActorKinds.Player, 0, RpgXpAwards.Mower, RpgXpReasons.Mower) };
            case PvzActivityKinds.PlantPlaced:
                return new[] { new Award(RpgActorKinds.Plant, typeId ?? 0, RpgXpAwards.PlantPlace, RpgXpReasons.PlantPlace) };
            case PvzActivityKinds.ZombieSpawned:
                return new[] { new Award(RpgActorKinds.Zombie, typeId ?? 0, RpgXpAwards.ZombieSpawn, RpgXpReasons.ZombieSpawn) };
            default:
                return Array.Empty<Award>();
        }
    }
}
