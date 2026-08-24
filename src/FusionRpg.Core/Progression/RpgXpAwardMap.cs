using FusionRpg.Core.Activity;

namespace FusionRpg.Core.Progression;

/// <summary>Maps Activity fact kinds to XP awards (pure; used by server apply).</summary>
public static class RpgXpAwardMap
{
    public readonly record struct Award(string Kind, int TypeId, double Delta, string Reason, double PowerScale = 1.0);

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
                    new Award(RpgActorKinds.Player, 0, RpgXpAwards.Kill * scale, RpgXpReasons.Kill, scale)
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
