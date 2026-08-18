using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

public sealed class RpgHub : Hub
{
    private readonly RpgStore _store;
    private readonly EventIngest _ingest;
    private readonly EffectGrantSession _grants;
    private readonly InjectorCommandInbox _inbox;

    public RpgHub(RpgStore store, EventIngest ingest, EffectGrantSession grants, InjectorCommandInbox inbox)
    {
        _store = store;
        _ingest = ingest;
        _grants = grants;
        _inbox = inbox;
    }

    public async Task Join(string role)
    {
        var group = role == RpgConstants.InjectorGroup ? RpgConstants.InjectorGroup : RpgConstants.WebGroup;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        if (group == RpgConstants.InjectorGroup)
            _store.Heartbeat(RpgConstants.SourceInjector);
    }

    public async Task Hello(HelloDto hello)
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        _ingest.Enqueue(new EventEnvelope
        {
            T = DateTime.UtcNow.ToString("o"),
            Game = hello.Game,
            Kind = "injector.hello",
            Payload = hello
        });
        await PushGrantSnapshotAsync();
    }

    /// <summary>W0-E: push session Effect grants so injector bag survives reconnect / re-inject.</summary>
    async Task PushGrantSnapshotAsync()
    {
        var cmd = EffectGrantRehydrate.TryBuildApplyCommand(_grants.Snapshot());
        if (cmd == null) return;
        _inbox.Enqueue(cmd);
        try
        {
            await Clients.Group(RpgConstants.InjectorGroup).SendAsync("Command", cmd);
        }
        catch
        {
            /* inbox poll */
        }
    }

    public Task Event(EventEnvelope envelope)
    {
        _ingest.Enqueue(envelope);
        return Task.CompletedTask;
    }

    public Task Events(List<EventEnvelope> batch)
    {
        _ingest.EnqueueRange(batch);
        return Task.CompletedTask;
    }

    public Task Metrics(List<MetricItem> items)
    {
        foreach (var m in items)
            _store.UpsertMetric(m.Name, m.Value);
        return Task.CompletedTask;
    }

    public async Task Heartbeat(HelloDto hello)
    {
        _store.Heartbeat(RpgConstants.SourceInjector);
        await Clients.Group(RpgConstants.WebGroup).SendAsync("Health", _ingest.Decorate(_store.ToHealth(SimFlags.Enabled)));
    }
}
