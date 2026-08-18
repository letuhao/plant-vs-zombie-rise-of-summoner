using FusionRpg.Contracts;
using FusionRpg.Core.Stats;

namespace FusionRpg.Core.Effects;

public static class EffectOwnerKey
{
    public static bool MatchesEvent(EffectGrant grant, EffectEventDto ev)
    {
        var key = grant.OwnerKey ?? "";
        if (string.Equals(key, EffectOwnerKeys.Match, StringComparison.OrdinalIgnoreCase))
            return true;

        if (key.StartsWith("plant:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(key.AsSpan(6), out var tid)) return false;
            if (string.Equals(ev.Trigger, EffectTriggers.OnSpawn, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ev.Trigger, EffectTriggers.OnDeath, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(ev.Trigger, EffectTriggers.OnDamageTaken, StringComparison.OrdinalIgnoreCase))
            {
                if (!string.Equals(ev.Side, "plant", StringComparison.OrdinalIgnoreCase)) return false;
                return (ev.TypeId ?? ev.TargetTypeId) == tid;
            }

            if (string.Equals(ev.Trigger, EffectTriggers.OnDamageDealt, StringComparison.OrdinalIgnoreCase))
                return string.Equals(ev.Side, "plant", StringComparison.OrdinalIgnoreCase) && ev.TypeId == tid;
            return false;
        }

        if (key.StartsWith("zombie:", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(key.AsSpan(7), out var tid)) return false;
            if (!string.Equals(ev.Side, "zombie", StringComparison.OrdinalIgnoreCase) &&
                !(string.Equals(ev.Trigger, EffectTriggers.OnDamageDealt, StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(ev.Side, "zombie", StringComparison.OrdinalIgnoreCase)))
            {
                // death/spawn/taken with zombie side
                if (ev.Side != null && !string.Equals(ev.Side, "zombie", StringComparison.OrdinalIgnoreCase))
                    return false;
            }

            return (ev.TypeId ?? ev.TargetTypeId) == tid ||
                   (string.Equals(ev.Trigger, EffectTriggers.OnDamageTaken, StringComparison.OrdinalIgnoreCase) &&
                    ev.TargetTypeId == tid);
        }

        if (key.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
        {
            var want = StatApplyScope.Normalize(key);
            if (!string.IsNullOrWhiteSpace(ev.ActorPtr) &&
                string.Equals(want, StatApplyScope.Normalize(EffectOwnerKeys.Entity(ev.ActorPtr)), StringComparison.Ordinal))
                return true;
            if (!string.IsNullOrWhiteSpace(ev.TargetPtr) &&
                string.Equals(want, StatApplyScope.Normalize(EffectOwnerKeys.Entity(ev.TargetPtr)), StringComparison.Ordinal))
                return true;
            return false;
        }

        if (key.StartsWith("player:", StringComparison.OrdinalIgnoreCase))
            return true; // match-scoped for now; player filter is grant-time

        // instance: (Hot-forbidden) and unknown grammar: never auto-match (was self-equals).
        return false;
    }

    public static bool PassesOverlayFilters(Dictionary<string, object?> overlay, EffectEventDto ev, EffectGrant? grant = null)
    {
        if (!overlay.TryGetValue("filters", out var f) || f == null) return true;
        var filters = JsonOverlay.FromObject(f);

        var filterSide = JsonOverlay.GetString(filters, "side");
        var filterTypeId = filters.ContainsKey("typeId")
            ? JsonOverlay.GetInt(filters, "typeId", int.MinValue)
            : (int?)null;

        if (!string.IsNullOrEmpty(filterSide) || filterTypeId.HasValue)
        {
            ResolveFilterTarget(ev, out var damagedSide, out var damagedTypeId);
            if (!string.IsNullOrEmpty(filterSide) &&
                !string.Equals(filterSide, damagedSide, StringComparison.OrdinalIgnoreCase))
                return false;
            if (filterTypeId.HasValue && damagedTypeId != filterTypeId.Value)
                return false;
        }

        if (JsonOverlay.GetBool(filters, "actorIsKiller", false))
        {
            if (string.IsNullOrEmpty(ev.KillerPtr))
                return false;
            var ownerKey = grant?.OwnerKey ?? "";
            if (ownerKey.StartsWith("entity:", StringComparison.OrdinalIgnoreCase))
            {
                var ptr = ownerKey[7..];
                if (!string.Equals(ev.KillerPtr, ptr, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            // match / type grants: KillerPtr present = credit exists
        }

        return true;
    }

    /// <summary>
    /// Overlay filters.side / typeId refer to the <b>damaged</b> entity on OnDamageDealt
    /// (event.Side is attacker). Other triggers use Side / TypeId as-is.
    /// </summary>
    public static void ResolveFilterTarget(EffectEventDto ev, out string? damagedSide, out int? damagedTypeId)
    {
        if (string.Equals(ev.Trigger, EffectTriggers.OnDamageDealt, StringComparison.OrdinalIgnoreCase))
        {
            damagedSide = InvertSide(ev.Side);
            damagedTypeId = ev.TargetTypeId ?? ev.TypeId;
            return;
        }

        damagedSide = ev.Side;
        damagedTypeId = ev.TypeId ?? ev.TargetTypeId;
    }

    static string? InvertSide(string? side)
    {
        if (string.Equals(side, "plant", StringComparison.OrdinalIgnoreCase)) return "zombie";
        if (string.Equals(side, "zombie", StringComparison.OrdinalIgnoreCase)) return "plant";
        return side;
    }
}

public static class EffectOverlayMerge
{
    static readonly Dictionary<string, HashSet<string>> AllowedByAction = new(StringComparer.OrdinalIgnoreCase)
    {
        [EffectActions.ModifyStat] = new(StringComparer.OrdinalIgnoreCase)
            { "channel", "flat", "increased", "more", "byChannel", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.ApplyStatus] = new(StringComparer.OrdinalIgnoreCase)
            { "status", "duration", "level", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.ClearStatus] = new(StringComparer.OrdinalIgnoreCase)
            { "status", "target", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.SpawnEntity] = new(StringComparer.OrdinalIgnoreCase)
        {
            "kind", "typeId", "hp", "maxHp", "atk", "mindControlled", "row", "col", "x",
            "chance", "icd_ms", "max_stacks", "filters"
        },
        [EffectActions.BoardAction] = new(StringComparer.OrdinalIgnoreCase)
            { "op", "row", "col", "x", "y", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.SpawnGridItem] = new(StringComparer.OrdinalIgnoreCase)
            { "gridItemType", "row", "col", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.ClearGridItem] = new(StringComparer.OrdinalIgnoreCase)
            { "selector", "gridItemType", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.SetBoxType] = new(StringComparer.OrdinalIgnoreCase)
            { "boxType", "cells", "row", "col", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.Economy] = new(StringComparer.OrdinalIgnoreCase)
            { "currency", "op", "amount", "capPerMatch", "chance", "icd_ms", "max_stacks", "filters" },
        [EffectActions.ApplyResourceDelta] = new(StringComparer.OrdinalIgnoreCase)
        {
            "channel", "amount", "mode", "mergedCount", "targetPtr",
            "target", "delivery", "burst", "procDepthLimit", "chainDepth",
            "chance", "icd_ms", "max_stacks", "filters",
            "statusId", "periodMs", "durationMs", "status_icd_ms", "statusIcdMs",
            "everyHits", "resetOnBurst", "counterScope", "tickBudget",
            "spread", "immunityTags"
        }
    };

    public static bool TryValidateOverlayForDef(
        IReadOnlyList<EffectActionRow> actions,
        Dictionary<string, object?> overlay,
        out string? error)
    {
        var union = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "filters", "chance", "icd_ms", "max_stacks"
        };
        foreach (var action in actions)
        {
            if (!AllowedByAction.TryGetValue(action.Action, out var allowed))
            {
                error = "unknown action " + action.Action;
                return false;
            }
            foreach (var k in allowed) union.Add(k);
        }

        foreach (var kv in overlay)
        {
            if (!union.Contains(kv.Key))
            {
                error = "unknown overlay key '" + kv.Key + "' for effect actions";
                return false;
            }
        }

        error = null;
        return true;
    }

    public static bool TryMerge(
        string action,
        Dictionary<string, object?> actionParams,
        Dictionary<string, object?> overlay,
        out Dictionary<string, object?> merged,
        out string? error)
    {
        merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in actionParams) merged[kv.Key] = kv.Value;

        if (!AllowedByAction.TryGetValue(action, out var allowed))
        {
            error = "unknown action " + action;
            return false;
        }

        foreach (var kv in overlay)
        {
            if (kv.Key.Equals("filters", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("chance", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("icd_ms", StringComparison.OrdinalIgnoreCase) ||
                kv.Key.Equals("max_stacks", StringComparison.OrdinalIgnoreCase))
                continue;

            // Grant-time validated against action union; per-action merge applies only keys for this action.
            if (!allowed.Contains(kv.Key))
                continue;

            merged[kv.Key] = kv.Value;
        }

        // channel-specific magnitudes
        if (string.Equals(action, EffectActions.ModifyStat, StringComparison.OrdinalIgnoreCase) &&
            overlay.TryGetValue("byChannel", out var byCh) && byCh != null)
        {
            var channel = JsonOverlay.GetString(merged, "channel");
            var map = JsonOverlay.FromObject(byCh);
            if (!string.IsNullOrEmpty(channel) && map.TryGetValue(channel, out var chObj) && chObj != null)
            {
                foreach (var kv in JsonOverlay.FromObject(chObj))
                    merged[kv.Key] = kv.Value;
            }
        }
        else if (string.Equals(action, EffectActions.ModifyStat, StringComparison.OrdinalIgnoreCase))
        {
            foreach (var mag in new[] { "flat", "increased", "more" })
            {
                if (overlay.TryGetValue(mag, out var v))
                    merged[mag] = v;
            }
        }

        error = null;
        return true;
    }
}

public sealed class EffectProcPolicy
{
    readonly IEffectClock _clock;
    readonly IEffectRandom _rng;
    readonly Dictionary<string, DateTimeOffset> _lastFire = new(StringComparer.Ordinal);
    readonly Dictionary<string, int> _stacks = new(StringComparer.Ordinal);

    public EffectProcPolicy(IEffectClock clock, IEffectRandom rng)
    {
        _clock = clock;
        _rng = rng;
    }

    public void Clear()
    {
        _lastFire.Clear();
        _stacks.Clear();
    }

    public void ClearGrant(string grantId)
    {
        if (string.IsNullOrEmpty(grantId)) return;
        var prefix = grantId + "|";
        foreach (var k in _lastFire.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _lastFire.Remove(k);
        foreach (var k in _stacks.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)).ToList())
            _stacks.Remove(k);
    }

    public bool TryPass(EffectGrant grant, string trigger, out string? skipReason)
    {
        var overlay = grant.Overlay;
        var chance = overlay.ContainsKey("chance")
            ? JsonOverlay.GetDouble(overlay, "chance", 1)
            : 1.0;
        if (chance < 1.0 && _rng.NextDouble() > chance)
        {
            skipReason = "chance";
            return false;
        }

        var icdMs = ResolveIcdMs(overlay, trigger);
        var key = grant.GrantId + "|" + grant.EffectId;
        if (icdMs > 0 && _lastFire.TryGetValue(key, out var last))
        {
            var elapsed = (_clock.UtcNow - last).TotalMilliseconds;
            if (elapsed < icdMs)
            {
                skipReason = "icd";
                return false;
            }
        }

        var maxStacks = JsonOverlay.GetInt(overlay, "max_stacks", 0);
        if (maxStacks > 0)
        {
            _stacks.TryGetValue(key, out var s);
            if (s >= maxStacks)
            {
                skipReason = "max_stacks";
                return false;
            }

            _stacks[key] = s + 1;
        }

        if (icdMs > 0)
            _lastFire[key] = _clock.UtcNow;

        skipReason = null;
        return true;
    }

    public static int ResolveIcdMs(Dictionary<string, object?> overlay, string trigger)
    {
        if (overlay.ContainsKey("icd_ms"))
            return JsonOverlay.GetInt(overlay, "icd_ms", 0);

        if (string.Equals(trigger, EffectTriggers.OnDamageDealt, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trigger, EffectTriggers.OnDamageTaken, StringComparison.OrdinalIgnoreCase))
            return 250;

        return 0;
    }
}

public sealed class EffectEventDedupe
{
    readonly Dictionary<string, long> _seen = new(StringComparer.Ordinal);
    readonly long _windowTicks;

    public EffectEventDedupe(long windowTicks = 1)
    {
        _windowTicks = Math.Max(1, windowTicks);
    }

    public bool ShouldEmit(EffectEventDto ev)
    {
        var key = string.Join("|",
            ev.MatchKey ?? "",
            ev.Trigger,
            ev.ActorPtr ?? "",
            ev.TargetPtr ?? "",
            (ev.Tick / _windowTicks).ToString());
        if (_seen.TryGetValue(key, out var t) && t == ev.Tick / _windowTicks)
            return false;
        _seen[key] = ev.Tick / _windowTicks;
        if (_seen.Count > 4096)
            _seen.Clear();
        return true;
    }

    public void Clear() => _seen.Clear();
}
