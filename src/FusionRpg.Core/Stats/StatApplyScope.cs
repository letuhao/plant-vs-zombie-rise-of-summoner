namespace FusionRpg.Core.Stats;

/// <summary>
/// Whether a session <see cref="StatModifier.ApplyOwnerKey"/> applies to a resolve context.
/// Grammar matches <c>EffectOwnerKeys</c>: <c>match</c>, <c>plant:N</c>, <c>zombie:N</c>, <c>entity:HEX</c>, empty → match.
/// Durable <c>instance:{guid}</c> is Hot-forbidden until a deploy binder rewrites to <c>entity:{ptr}</c>.
/// </summary>
public static class StatApplyScope
{
    /// <summary>
    /// Canonical form for bag Keys and Matches: trim, lower-case; empty → <c>match</c>;
    /// <c>entity:0xHEX</c> → <c>entity:hex</c> (no 0x prefix).
    /// </summary>
    public static string Normalize(string? ownerKey)
    {
        if (string.IsNullOrWhiteSpace(ownerKey)) return "match";
        var key = ownerKey.Trim().ToLowerInvariant();
        if (key.StartsWith("entity:", StringComparison.Ordinal))
        {
            var hex = key[7..];
            if (hex.StartsWith("0x", StringComparison.Ordinal))
                hex = hex[2..];
            return "entity:" + hex;
        }

        return key;
    }

    /// <summary>
    /// Durable specimen key reserved for Server/Data. Must not match in Hot Resolve
    /// until a binder translates to <c>entity:{ptr}</c>.
    /// </summary>
    public static bool IsInstanceOwnerKey(string? applyOwnerKey)
    {
        var key = Normalize(applyOwnerKey);
        return key.StartsWith("instance:", StringComparison.Ordinal);
    }

    public static bool Matches(string? applyOwnerKey, StatContext ctx)
    {
        if (ctx == null) throw new ArgumentNullException(nameof(ctx));
        return Matches(applyOwnerKey, ctx.Side, ctx.TypeId, ctx.EntityKey);
    }

    public static bool Matches(string? applyOwnerKey, StatSide side, int typeId, string? entityKey)
    {
        // Hot guard: instance: never composes until binder exists (S-INSTANCE-KEY-HOT).
        if (IsInstanceOwnerKey(applyOwnerKey))
            return false;

        var key = Normalize(applyOwnerKey);
        if (string.Equals(key, "match", StringComparison.Ordinal))
            return true;

        if (key.StartsWith("plant:", StringComparison.Ordinal))
        {
            if (side != StatSide.Plant) return false;
            return int.TryParse(key.AsSpan(6), System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture, out var tid) &&
                   tid == typeId;
        }

        if (key.StartsWith("zombie:", StringComparison.Ordinal))
        {
            if (side != StatSide.Zombie) return false;
            return int.TryParse(key.AsSpan(7), System.Globalization.NumberStyles.Integer,
                       System.Globalization.CultureInfo.InvariantCulture, out var tid) &&
                   tid == typeId;
        }

        if (key.StartsWith("entity:", StringComparison.Ordinal))
        {
            var ptr = key[7..];
            if (string.IsNullOrEmpty(entityKey) || string.IsNullOrEmpty(ptr))
                return false;
            var live = entityKey.Trim();
            if (live.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                live = live[2..];
            return string.Equals(live, ptr, StringComparison.OrdinalIgnoreCase);
        }

        if (key.StartsWith("player:", StringComparison.Ordinal))
            return true; // stub → match-wide apply

        return false;
    }

    public static bool IsMatchWide(string? applyOwnerKey)
    {
        var key = Normalize(applyOwnerKey);
        return string.Equals(key, "match", StringComparison.Ordinal) ||
               key.StartsWith("player:", StringComparison.Ordinal);
    }

    /// <summary>True for match/player/plant/zombie/entity grammar (after Normalize).</summary>
    public static bool IsKnownOwnerKey(string? applyOwnerKey)
    {
        var key = Normalize(applyOwnerKey);
        if (string.Equals(key, "match", StringComparison.Ordinal)) return true;
        if (key.StartsWith("player:", StringComparison.Ordinal)) return true;
        if (key.StartsWith("entity:", StringComparison.Ordinal)) return true;
        if (key.StartsWith("plant:", StringComparison.Ordinal) &&
            int.TryParse(key.AsSpan(6), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return true;
        if (key.StartsWith("zombie:", StringComparison.Ordinal) &&
            int.TryParse(key.AsSpan(7), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out _))
            return true;
        return false;
    }
}
