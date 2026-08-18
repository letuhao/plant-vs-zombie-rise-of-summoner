using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Combat;

/// <summary>Parse grant overlay / debug body into a planning <see cref="DamagePacket"/>.</summary>
public static class DamagePacketBuilder
{
    public static DamagePacket FromOverlay(
        Dictionary<string, object?>? overlay,
        EffectEventDto? ev = null,
        string? grantId = null,
        string? effectId = null,
        string? pluginId = null,
        long? amountOverride = null)
    {
        overlay ??= new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        var amount = amountOverride ?? (long)JsonOverlay.GetDouble(overlay, "amount");
        var packet = new DamagePacket
        {
            PacketId = Guid.NewGuid().ToString("N"),
            SourceGrantId = grantId ?? "",
            EffectId = effectId,
            PluginId = pluginId,
            ActorPtr = ev?.ActorPtr,
            SignedAmount = amount,
            Channel = JsonOverlay.GetString(overlay, "channel") ?? "hp",
            Tick = ev?.Tick ?? 0,
            ChainDepth = overlay.ContainsKey("chainDepth")
                ? JsonOverlay.GetInt(overlay, "chainDepth")
                : (ev?.ChainDepth ?? 0),
            ProcDepthLimit = overlay.ContainsKey("procDepthLimit")
                ? JsonOverlay.GetInt(overlay, "procDepthLimit")
                : null,
            Target = ParseTargetFromOverlay(overlay),
            Delivery = ParseDelivery(overlay)
        };

        if (overlay.TryGetValue("burst", out var burstObj) && burstObj != null)
        {
            var burstMap = JsonOverlay.FromObject(burstObj);
            packet.Burst = FromOverlay(burstMap, ev, grantId, effectId, pluginId);
            packet.Burst.ChainDepth = packet.ChainDepth + 1;
        }

        return packet;
    }

    public static TargetSpec ParseTargetFromOverlay(Dictionary<string, object?> overlay)
    {
        if (!overlay.TryGetValue("target", out var t) || t == null)
        {
            var ptr = JsonOverlay.GetString(overlay, "targetPtr");
            return new TargetSpec
            {
                Mode = string.IsNullOrWhiteSpace(ptr) ? TargetModes.EventTarget : TargetModes.Single,
                Ptr = ptr
            };
        }

        var map = JsonOverlay.FromObject(t);
        return new TargetSpec
        {
            Mode = JsonOverlay.GetString(map, "mode") ?? TargetModes.EventTarget,
            Ptr = JsonOverlay.GetString(map, "ptr"),
            Count = map.ContainsKey("count") ? JsonOverlay.GetInt(map, "count") : null,
            Shape = JsonOverlay.GetString(map, "shape"),
            Size = map.ContainsKey("size") ? JsonOverlay.GetInt(map, "size") : null,
            Width = map.ContainsKey("width") ? JsonOverlay.GetInt(map, "width") : null,
            Height = map.ContainsKey("height") ? JsonOverlay.GetInt(map, "height") : null,
            Anchor = map.TryGetValue("anchor", out var a) ? a : null,
            AnchorOrigin = JsonOverlay.GetString(map, "anchorOrigin"),
            Filters = map.TryGetValue("filters", out var f) && f != null
                ? JsonOverlay.FromObject(f)
                : null,
            MaxTargets = map.ContainsKey("maxTargets") ? JsonOverlay.GetInt(map, "maxTargets") : null
        };
    }

    static DeliverySpec ParseDelivery(Dictionary<string, object?> overlay)
    {
        if (!overlay.TryGetValue("delivery", out var d) || d == null)
            return new DeliverySpec { Mode = DeliveryModes.Instant };

        var map = JsonOverlay.FromObject(d);
        return new DeliverySpec
        {
            Mode = JsonOverlay.GetString(map, "mode") ?? DeliveryModes.Instant,
            PeriodMs = map.ContainsKey("periodMs") ? JsonOverlay.GetInt(map, "periodMs") : null,
            DurationMs = map.ContainsKey("durationMs") ? JsonOverlay.GetInt(map, "durationMs") : null,
            TickBudget = map.ContainsKey("tickBudget") ? JsonOverlay.GetInt(map, "tickBudget") : null,
            EveryHits = map.ContainsKey("everyHits") ? JsonOverlay.GetInt(map, "everyHits") : null,
            ResetOnBurst = !map.ContainsKey("resetOnBurst") || JsonOverlay.GetBool(map, "resetOnBurst", true),
            CounterScope = JsonOverlay.GetString(map, "counterScope")
        };
    }
}
