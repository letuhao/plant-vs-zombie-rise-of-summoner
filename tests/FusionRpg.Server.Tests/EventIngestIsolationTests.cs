using FusionRpg.Contracts;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// lawn-combat-wire L-N29: one event that fails to insert must cost only itself, never the neighbours that shared its
/// writer batch. The poison here is a payload System.Text.Json refuses (NaN), which throws inside
/// <see cref="RpgStore.InsertEvents"/> exactly as a unique-key violation did live.
/// </summary>
public class EventIngestIsolationTests : IDisposable
{
    readonly RpgStore _store;

    public EventIngestIsolationTests()
    {
        _store = RpgStore.InMemory();
        _store.Init();
    }

    public void Dispose() => (_store as IDisposable)?.Dispose();

    static EventEnvelope Ev(string kind, object payload) => new()
    {
        T = DateTime.UtcNow.ToString("o"),
        Game = RpgConstants.GameId39,
        Kind = kind,
        Payload = payload,
    };

    [Fact]
    public void A_poison_event_is_dropped_alone_and_its_neighbours_are_stored_in_order()
    {
        var before = _store.GetMaxEventId();
        var batch = new List<EventEnvelope>
        {
            Ev("isolation.first", new { n = 1 }),
            Ev("isolation.poison", new { n = double.NaN }),
            Ev("isolation.last", new { n = 3 }),
        };
        var dropped = new List<string>();

        var (_, stored) = EventIngest.InsertIsolatingFailures(_store, batch, (env, _) => dropped.Add(env.Kind));

        Assert.Equal(new[] { "isolation.poison" }, dropped);
        Assert.Equal(new[] { "isolation.first", "isolation.last" }, stored.Select(e => e.Kind));
        var persisted = _store.ListEvents(500, before).Select(e => e.Kind).ToList();
        Assert.Equal(new[] { "isolation.first", "isolation.last" }, persisted);
    }

    [Fact]
    public void A_clean_batch_is_one_insert_with_nothing_dropped()
    {
        var before = _store.GetMaxEventId();
        var batch = new List<EventEnvelope> { Ev("isolation.a", new { }), Ev("isolation.b", new { }) };

        var (_, stored) = EventIngest.InsertIsolatingFailures(_store, batch, (_, ex) => throw ex);

        Assert.Same(batch, stored);
        Assert.Equal(2, _store.ListEvents(500, before).Count);
    }
}
