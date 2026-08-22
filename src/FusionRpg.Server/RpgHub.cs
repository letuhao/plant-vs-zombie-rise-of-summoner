using System.Globalization;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
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
        await PushPatronAsync();
    }

    /// <summary>A fresh inject/reconnect always receives the current patron designation
    /// (spec-patron-demon.md) — same rehydrate discipline as the grant snapshot above.</summary>
    async Task PushPatronAsync()
    {
        var cmd = PatronEndpoints.TryBuildPatronCommand(_store);
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

    /// <summary>
    /// W0-E: push session Effect grants so the injector bag survives reconnect / re-inject, plus
    /// E19's compiled atom output on the same command.
    ///
    /// <para><b>The atom half resolves per player</b>, via <c>GetCurrentPlayerId</c> — the same
    /// current-player the patron push already uses for this exact shape, and the constitution's rule
    /// that the server stamps <c>player_id</c> while the injector never sends it
    /// ([pvz-middle-layer.md](../../docs/architecture/pvz-middle-layer.md) §Constitution 6). The
    /// session grant snapshot beside it stays session-scoped, because it always was.</para>
    /// </summary>
    async Task PushGrantSnapshotAsync()
    {
        var cmd = BuildApplyCommand();
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

    /// <summary>
    /// The session grants and the compiled atom push travel on one command, because they are the same
    /// rehydrate: a reconnect must not leave the injector holding half of its effects.
    /// </summary>
    CommandDto? BuildApplyCommand()
    {
        var grants = _grants.Snapshot();

        AtomPushDto? atoms = null;
        try
        {
            var playerId = _store.GetCurrentPlayerId();
            // No seed at Hello: the lawn match key is born in the injector's board.start capture,
            // so the server has none here. The receiver derives the seed itself from that key with
            // MatchSeed.For — the same pure function the server uses when replaying, which is what
            // D5 actually needs. The wire field stays, so a stored seed can override it later.
            atoms = new AtomPushService(_store).Build(
                new OwnerScope(OwnerKind.Player, playerId.ToString(CultureInfo.InvariantCulture)),
                new BindContext(RuntimeId.Lawn),
                matchSeed: 0);
        }
        catch (Exception ex)
        {
            // A failed atom push must never cost the injector its Foundation grants — the two halves
            // share a command, not a fate.
            Console.Error.WriteLine("[atom-push] build failed: " + ex.Message);
        }

        var nothingToSend = grants.Count == 0
            && (atoms is null || (atoms.Grants.Count == 0 && atoms.RunnerBindings.Count == 0 && atoms.Defs.Count == 0));
        if (nothingToSend) return null;

        var payload = new Dictionary<string, object?> { ["grants"] = grants };
        if (atoms != null)
        {
            payload["defs"] = atoms.Defs;
            payload["runnerBindings"] = atoms.RunnerBindings;
            payload["catalogRevision"] = atoms.CatalogRevision;
            payload["contentHash"] = atoms.ContentHash;
            payload["matchSeed"] = atoms.MatchSeed;
            payload["matchKey"] = atoms.MatchKey;
            payload["upToDate"] = atoms.UpToDate;
        }

        return new CommandDto
        {
            Name = EffectGrantRehydrate.ApplyCommandName,
            Payload = payload,
            Id = Guid.NewGuid().ToString("N"),
        };
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
