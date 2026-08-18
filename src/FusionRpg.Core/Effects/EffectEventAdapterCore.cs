using FusionRpg.Contracts;

namespace FusionRpg.Core.Effects;

/// <summary>Capture kind → FT* mapping without Unity. Injector and SimEffectHost share this.</summary>
public static class EffectEventAdapterCore
{
    public static EffectEventDto? TryMap(
        string kind,
        Dictionary<string, object> p,
        long tick,
        string? matchKey = null)
    {
        if (string.Equals(kind, "combat.hit", StringComparison.OrdinalIgnoreCase))
            return MapDealtFromCombatHit(p, tick, matchKey);

        if (string.Equals(kind, "zombie.damage", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "plant.damage", StringComparison.OrdinalIgnoreCase))
            return MapTaken(kind, p, tick, matchKey);

        if (string.Equals(kind, "zombie.die", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "plant.die", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "debug.kill.signal", StringComparison.OrdinalIgnoreCase))
            return MapDeath(kind, p, tick, matchKey);

        if (string.Equals(kind, "plant.place", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "zombie.place", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "bullet.init", StringComparison.OrdinalIgnoreCase))
            return MapSpawn(kind, p, tick, matchKey);

        if (string.Equals(kind, "effect.timer", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(kind, "combat.timer", StringComparison.OrdinalIgnoreCase))
        {
            return new EffectEventDto
            {
                Trigger = EffectTriggers.OnTimer,
                MatchKey = matchKey,
                Tick = tick,
                ActorPtr = p.TryGetValue("actorPtr", out var a) ? a?.ToString() : null,
                TargetPtr = p.TryGetValue("targetPtr", out var t) ? t?.ToString() : null
            };
        }

        return null;
    }

    /// <summary>
    /// Maps injector <c>combat.hit</c> → <see cref="EffectTriggers.OnDamageDealt"/>.
    /// W0-D SSOT payloads: <c>source=takeDamage</c> (Bullet via TakeDamage) and
    /// <c>source=attackPlant</c> (melee). Actor = <c>attackerPtr</c> else <c>bulletPtr</c>.
    /// Base Bullet.Hit* Harmony is not the primary surface.
    /// </summary>
    static EffectEventDto MapDealtFromCombatHit(Dictionary<string, object> p, long tick, string? matchKey)
    {
        var side = Str(p, "side") ?? "zombie";
        var actorSide = string.Equals(side, "zombie", StringComparison.OrdinalIgnoreCase) ? "plant" : "zombie";
        // Prefer melee attackerPtr; pea TakeDamage stamps bulletPtr only.
        return new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageDealt,
            MatchKey = matchKey,
            Side = actorSide,
            ActorPtr = Str(p, "attackerPtr") ?? Str(p, "bulletPtr"),
            TargetPtr = Str(p, "targetPtr"),
            TypeId = IntOrNull(p, "fromType"),
            TargetTypeId = IntOrNull(p, "targetType"),
            Damage = IntOrNull(p, "damage"),
            Tick = tick,
            ScenarioId = Str(p, "scenarioId")
        };
    }

    static EffectEventDto MapTaken(string kind, Dictionary<string, object> p, long tick, string? matchKey)
    {
        var side = kind.StartsWith("plant", StringComparison.OrdinalIgnoreCase) ? "plant" : "zombie";
        return new EffectEventDto
        {
            Trigger = EffectTriggers.OnDamageTaken,
            MatchKey = matchKey,
            Side = side,
            ActorPtr = Str(p, "damageFrom"),
            TargetPtr = Str(p, "ptr"),
            TypeId = IntOrNull(p, "type") ?? IntOrNull(p, "typeId"),
            TargetTypeId = IntOrNull(p, "type") ?? IntOrNull(p, "typeId"),
            Damage = IntOrNull(p, "damage"),
            Tick = tick,
            ScenarioId = Str(p, "scenarioId")
        };
    }

    static EffectEventDto MapDeath(string kind, Dictionary<string, object> p, long tick, string? matchKey)
    {
        var side = kind.Contains("plant", StringComparison.OrdinalIgnoreCase) ? "plant" : "zombie";
        return new EffectEventDto
        {
            Trigger = EffectTriggers.OnDeath,
            MatchKey = matchKey,
            Side = side,
            ActorPtr = Str(p, "ptr") ?? Str(p, "deadPtr"),
            TargetPtr = Str(p, "ptr") ?? Str(p, "deadPtr"),
            TypeId = IntOrNull(p, "type") ?? IntOrNull(p, "typeId"),
            KillerPtr = Str(p, "killerPtr") ?? Str(p, "damageFrom"),
            Tick = tick,
            ScenarioId = Str(p, "scenarioId")
        };
    }

    static EffectEventDto MapSpawn(string kind, Dictionary<string, object> p, long tick, string? matchKey)
    {
        string side;
        if (kind.Contains("bullet", StringComparison.OrdinalIgnoreCase)) side = "bullet";
        else if (kind.Contains("plant", StringComparison.OrdinalIgnoreCase)) side = "plant";
        else side = "zombie";

        return new EffectEventDto
        {
            Trigger = EffectTriggers.OnSpawn,
            MatchKey = matchKey,
            Side = side,
            ActorPtr = Str(p, "ptr"),
            TypeId = IntOrNull(p, "type") ?? IntOrNull(p, "typeId")
                     ?? IntOrNull(p, "plantType") ?? IntOrNull(p, "bulletType"),
            Tick = tick,
            ScenarioId = Str(p, "scenarioId")
        };
    }

    static string? Str(Dictionary<string, object> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v == null) return null;
        return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture);
    }

    static int? IntOrNull(Dictionary<string, object> p, string key)
    {
        if (!p.TryGetValue(key, out var v) || v == null) return null;
        try { return Convert.ToInt32(v, System.Globalization.CultureInfo.InvariantCulture); }
        catch { return null; }
    }
}
