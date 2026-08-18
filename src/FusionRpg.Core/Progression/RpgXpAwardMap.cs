using FusionRpg.Core.Activity;

namespace FusionRpg.Core.Progression;

/// <summary>Maps Activity fact kinds to XP awards (pure; used by server apply).</summary>
public static class RpgXpAwardMap
{
    public readonly record struct Award(string Kind, int TypeId, double Delta, string Reason, double PowerScale = 1.0);

    public static IReadOnlyList<Award> FromActivity(string factKind, string? resultRaw, int? typeId, string? payloadJson = null)
    {
        switch (factKind)
        {
            case PvzActivityKinds.ZombieKilled:
            {
                var scale = RpgXpPowerScale.ForKill(typeId, payloadJson);
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
