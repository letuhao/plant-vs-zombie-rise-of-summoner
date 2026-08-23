using System.Text.Json;

namespace FusionRpg.Core.Overlay;

public sealed record OverlayPauseTuning(float PausedTimeScale, float MaxResumeScale);

public sealed record OverlaySwitchLayoutTuning(
    float BaseButtonW, float BaseButtonH, float BaseMargin, float ReferenceHeight,
    float MinScale, float MaxScale);

public sealed record OverlaySwitchStateTuning(int DebounceMs, int ProbeIntervalMs, int SendTimeoutMs);

/// <summary>Overlay balance/UI surface (tunables-ssot.md T1) — loaded, not hard-coded. See
/// <see cref="OverlayTuningHub.Configure"/> and <see cref="OverlayTuningLoader"/>.</summary>
public sealed record OverlayTuning(
    int SchemaVersion, int Version,
    OverlayPauseTuning Pause, OverlaySwitchLayoutTuning SwitchLayout, OverlaySwitchStateTuning SwitchState);

public sealed class OverlayTuningRejection : Exception
{
    public OverlayTuningRejection(string message) : base(message) { }
}

/// <summary>Pure parser, no file I/O (tunables-ssot.md §7.2).</summary>
public static class OverlayTuningLoader
{
    public static OverlayTuning Parse(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw new OverlayTuningRejection("overlay tuning: empty document");

        JsonDocument doc;
        try { doc = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new OverlayTuningRejection($"overlay tuning: not valid JSON — {ex.Message}"); }

        using (doc)
        {
            var root = doc.RootElement;
            var p = Obj(root, "pause");
            var pause = new OverlayPauseTuning(
                PausedTimeScale: Flt(p, "pausedTimeScale", "pause"),
                MaxResumeScale: Flt(p, "maxResumeScale", "pause"));

            var l = Obj(root, "switchLayout");
            var layout = new OverlaySwitchLayoutTuning(
                BaseButtonW: Flt(l, "baseButtonW", "switchLayout"),
                BaseButtonH: Flt(l, "baseButtonH", "switchLayout"),
                BaseMargin: Flt(l, "baseMargin", "switchLayout"),
                ReferenceHeight: Flt(l, "referenceHeight", "switchLayout"),
                MinScale: Flt(l, "minScale", "switchLayout"),
                MaxScale: Flt(l, "maxScale", "switchLayout"));

            var s = Obj(root, "switchState");
            var state = new OverlaySwitchStateTuning(
                DebounceMs: Int(s, "debounceMs", "switchState"),
                ProbeIntervalMs: Int(s, "probeIntervalMs", "switchState"),
                SendTimeoutMs: Int(s, "sendTimeoutMs", "switchState"));

            return new OverlayTuning(Int(root, "schemaVersion", "$"), Int(root, "version", "$"),
                pause, layout, state);
        }
    }

    static JsonElement Obj(JsonElement parent, string key)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Object)
            throw new OverlayTuningRejection($"overlay tuning: missing or non-object '$.{key}'");
        return el;
    }

    static int Int(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number || !el.TryGetInt32(out var v))
            throw new OverlayTuningRejection($"overlay tuning: missing or non-integer '{path}.{key}'");
        return v;
    }

    static float Flt(JsonElement parent, string key, string path)
    {
        if (!parent.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Number)
            throw new OverlayTuningRejection($"overlay tuning: missing or non-numeric '{path}.{key}'");
        return el.GetSingle();
    }
}

/// <summary>Single configuration point covering the three overlay files that read one
/// <c>overlay.v{n}.json</c> — mirrors the shared-hub shape <see cref="World.WorldTuningHub"/> uses.</summary>
public static class OverlayTuningHub
{
    static OverlayTuning? _tuning;

    public static void Configure(OverlayTuning tuning) =>
        _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));

    public static OverlayTuning Tuning => _tuning ?? throw new InvalidOperationException(
        "OverlayTuningHub.Configure(...) has not run. Every overlay rule reads data/tuning/" +
        "overlay.v{n}.json (tunables-ssot.md T5) — there is no built-in default to fall back to.");
}
