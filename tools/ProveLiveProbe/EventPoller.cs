using System.Text.Json;
using FusionRpg.Contracts;

namespace FusionRpg.Tools.ProveLiveProbe;

/// <summary>
/// This tool's own bounded ack/poll wait, mirroring the ALGORITHM <c>DebugEndpoints.cs</c>'s own
/// <c>PollForKind</c> uses server-side (deadline + fixed delay between checks, never a single fixed
/// sleep) — but over the plain, non-debug-scoped <c>GET /api/events</c>
/// (<c>Program.cs</c>'s <c>app.MapGet("/api/events", ...)</c>), never
/// <c>/api/debug/events</c>. That is a deliberate substitution, not an oversight: this tool's own
/// boundary rule (spec-live-probe-tool.md, tasks/live-probe-todo.md Task 8) caps it at exactly two
/// debug-shaped routes — the identity-only <c>spawn-unique-actor</c> shortcut (which turns out to live
/// under <c>/api/creatures/debug/*</c>, not <c>/api/debug/*</c> — see <c>LiveProbeClient</c>'s own
/// note) and <c>POST /api/debug/board-stats</c> itself — polling the read side through the generic,
/// non-debug events feed keeps that count at two while still reading the same underlying event log
/// (<c>RpgStore.ListEvents</c> backs both routes identically).
/// </summary>
public static class EventPoller
{
    static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    const int PageSize = 500;
    const int MaxPagesWalked = 200; // 200 * 500 = 100k events walked at most before giving up on an exact tail

    /// <summary>
    /// <c>GET /api/events</c> only ever answers "events with id &gt; afterId, oldest-first, capped at
    /// 500" (<c>RpgStore.ListEvents</c>) — there is no HTTP-exposed "give me the current max id"
    /// (that's an in-process-only call, <c>RpgStore.GetMaxEventId</c>, used by
    /// <c>DebugEndpoints.cs</c>'s own handlers, which run inside the server and never need an HTTP
    /// round trip for it). Walking forward one full page at a time until a short page comes back is the
    /// only way this tool — an external HTTP client — can find "now" without a new endpoint, which the
    /// spec's own boundary forbids adding for this recipe. Run ONCE, right before sending the
    /// <c>debug.board-stats</c> command, never inside the poll loop itself.
    /// </summary>
    public static async Task<long> FindCurrentMaxEventIdAsync(LiveProbeClient client)
    {
        long afterId = 0;
        for (var page = 0; page < MaxPagesWalked; page++)
        {
            var (ok, _, body, _) = await client.GetAsync<EventPage>($"/api/events?limit={PageSize}&afterId={afterId}");
            if (!ok || body?.Items is not { Count: > 0 } items) return afterId;
            afterId = items[^1].Id ?? afterId;
            if (items.Count < PageSize) return afterId; // short page: reached the tail
        }
        return afterId; // gave up after MaxPagesWalked; best effort, not exact
    }

    /// <summary>
    /// Polls <c>GET /api/events</c> for the first <paramref name="kind"/> event, appearing strictly
    /// after <paramref name="afterId"/>, whose payload carries <paramref name="tag"/> (this tool's own
    /// correlation stamp — see <c>LiveProbeClient.SendBoardStatsAsync</c>) — never the first event of
    /// that kind at all, which could be a stale one still sitting in the log from an earlier run.
    /// Bounded by <paramref name="timeout"/>, checked on a fixed short delay, exactly
    /// <c>DebugEndpoints.PollForKind</c>'s own shape. Returns <c>null</c> on timeout — the caller reports
    /// that as its own distinct "timed out" outcome, never as a mismatch.
    /// </summary>
    public static async Task<EventEnvelope?> PollForTaggedKindAsync(
        LiveProbeClient client, long afterId, string kind, string tag, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            var (ok, _, body, _) = await client.GetAsync<EventPage>($"/api/events?limit={PageSize}&afterId={afterId}");
            if (ok && body?.Items is { Count: > 0 } items)
            {
                foreach (var e in items)
                {
                    if (!string.Equals(e.Kind, kind, StringComparison.OrdinalIgnoreCase)) continue;
                    if (PayloadHasTag(e.Payload, tag)) return e;
                }
            }
            await Task.Delay(300);
        }
        return null;
    }

    static bool PayloadHasTag(object? payload, string tag)
    {
        if (payload is not JsonElement el || el.ValueKind != JsonValueKind.Object) return false;
        return el.TryGetProperty("tag", out var t) && t.ValueKind == JsonValueKind.String &&
               string.Equals(t.GetString(), tag, StringComparison.Ordinal);
    }

    public static BoardStatsPayload? ParseBoardStats(EventEnvelope evt)
    {
        if (evt.Payload is not JsonElement el) return null;
        return JsonSerializer.Deserialize<BoardStatsPayload>(el.GetRawText(), JsonOpts);
    }

    sealed class EventPage
    {
        public List<EventEnvelope> Items { get; set; } = new();
    }
}
