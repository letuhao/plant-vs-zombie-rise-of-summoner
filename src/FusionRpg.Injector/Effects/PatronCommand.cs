using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons.Patron;

namespace FusionRpg.Injector.Effects;

/// <summary>Parses the server's `patron.aura` command into the runtime cache. Malformed payloads
/// clear nothing — the last good designation stands until a good one replaces it.</summary>
public static class PatronCommand
{
    public static void Apply(CommandDto cmd)
    {
        if (cmd.Payload == null) return;
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(cmd.Payload));
        var root = doc.RootElement;
        var elementPrimary = root.TryGetProperty("elementPrimary", out var ep) ? ep.GetString() : null;
        if (string.IsNullOrWhiteSpace(elementPrimary)) return;

        var aura = new PatronAura(
            elementPrimary!,
            root.TryGetProperty("elementSecondary", out var es) ? es.GetString() : null,
            GetInt(root, "powerMilli"),
            GetInt(root, "defenseMilli"),
            GetInt(root, "secondaryPowerMilli"),
            GetInt(root, "secondaryDefenseMilli"));
        var playerId = root.TryGetProperty("playerId", out var pid) && pid.TryGetInt64(out var p) ? p : 0;
        PatronRuntimeState.Set(playerId, aura);
    }

    static int GetInt(JsonElement root, string name) =>
        root.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
}
