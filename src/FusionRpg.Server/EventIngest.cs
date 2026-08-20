using System.Diagnostics;
using System.Threading.Channels;
using FusionRpg.Contracts;
using FusionRpg.Core.Effects;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;

namespace FusionRpg.Server;

public sealed class EventIngest : BackgroundService
{
    public const int WriterBatch = 800;

    private readonly Channel<EventEnvelope> _channel = Channel.CreateUnbounded<EventEnvelope>(new UnboundedChannelOptions
    {
        SingleReader = true,
        SingleWriter = false
    });
    private readonly RpgStore _store;
    private readonly IHubContext<RpgHub> _hub;
    private readonly CompactionWorker _compaction;
    private readonly EffectGrantSession _grants;
    private readonly UniqueActorService _uniqueActors;
    private int _queued;
    private int _writing;
    private double _lastFlushMs;

    public EventIngest(
        RpgStore store,
        IHubContext<RpgHub> hub,
        CompactionWorker compaction,
        EffectGrantSession grants,
        UniqueActorService uniqueActors)
    {
        _store = store;
        _hub = hub;
        _compaction = compaction;
        _grants = grants;
        _uniqueActors = uniqueActors;
    }

    long _droppedBatches;
    long _lastDropLogTick;

    public int Queued => Volatile.Read(ref _queued);
    public double LastFlushMs => Volatile.Read(ref _lastFlushMs);
    public long DroppedBatches => Interlocked.Read(ref _droppedBatches);

    public HealthDto Decorate(HealthDto health)
    {
        health.IngestQueued = Queued;
        health.LastFlushMs = LastFlushMs;
        return health;
    }

    /// <summary>PvZ-game events only — web-mode (webrpg) events must not touch injector-facing state.</summary>
    static bool IsPvzGameEvent(EventEnvelope env) => RpgConstants.IsPvzGame(env.Game);

    public int Enqueue(EventEnvelope? env)
    {
        if (env is null || string.IsNullOrWhiteSpace(env.Kind)) return 0;
        // Guard (audit 2026-08-21): a web battle's board.start/end must never clear the LIVE
        // PvZ match's effect-grant session.
        if (IsPvzGameEvent(env))
            EffectGrantSessionRecorder.NoteMatchLifecycle(_grants, env.Kind);
        Interlocked.Increment(ref _queued);
        if (!_channel.Writer.TryWrite(env))
        {
            Interlocked.Decrement(ref _queued);
            return 0;
        }
        return 1;
    }

    public int EnqueueRange(IEnumerable<EventEnvelope> events)
    {
        var n = 0;
        foreach (var e in events)
            n += Enqueue(e);
        return n;
    }

    public async Task FlushPendingAsync(CancellationToken ct = default)
    {
        var start = Stopwatch.GetTimestamp();
        while (!ct.IsCancellationRequested)
        {
            if (Volatile.Read(ref _queued) == 0 && Volatile.Read(ref _writing) == 0)
                return;
            if (Stopwatch.GetElapsedTime(start) > TimeSpan.FromSeconds(30))
                return;
            await Task.Delay(5, ct).ConfigureAwait(false);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;
        while (!stoppingToken.IsCancellationRequested)
        {
            EventEnvelope first;
            try { first = await reader.ReadAsync(stoppingToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { break; }

            Volatile.Write(ref _writing, 1);
            var batch = new List<EventEnvelope>(WriterBatch) { first };
            while (batch.Count < WriterBatch && reader.TryRead(out var next))
                batch.Add(next);

            var sw = Stopwatch.StartNew();
            try
            {
                var notify = _store.InsertEvents(batch);
                // UniqueActor recovery watches real PvZ matches only — web-battle die/end events
                // must not recover an ActiveBound specimen mid-PvZ-match (audit 2026-08-21).
                var pvzBatch = batch.Where(IsPvzGameEvent).ToList();
                if (pvzBatch.Count > 0)
                    try { _uniqueActors.ObserveEvents(pvzBatch); } catch { /* fail-closed */ }
                Volatile.Write(ref _lastFlushMs, sw.Elapsed.TotalMilliseconds);
                await BroadcastAsync(batch).ConfigureAwait(false);
                await BroadcastActivityAsync(notify.ActivityPlayers).ConfigureAwait(false);
                await BroadcastProgressionAsync(notify.Progression).ConfigureAwait(false);
                foreach (var runId in notify.ClosedRunIds)
                    _compaction.EnqueueClosedRun(runId);
            }
            catch (Exception ex)
            {
                // A failed flush drops the whole batch — make that visible instead of silent
                // (rate-limited: disk-full/locked-DB storms would otherwise flood the console).
                Interlocked.Increment(ref _droppedBatches);
                var now = Environment.TickCount64;
                if (now - Interlocked.Read(ref _lastDropLogTick) > 30_000)
                {
                    Interlocked.Exchange(ref _lastDropLogTick, now);
                    Console.Error.WriteLine(
                        $"[ingest] dropped batch of {batch.Count} events ({_droppedBatches} total): {ex.GetType().Name}: {ex.Message}");
                }

                Volatile.Write(ref _lastFlushMs, sw.Elapsed.TotalMilliseconds);
            }
            finally
            {
                Interlocked.Add(ref _queued, -batch.Count);
                Volatile.Write(ref _writing, 0);
            }
        }
    }

    async Task BroadcastAsync(List<EventEnvelope> batch)
    {
        var live = batch.Where(e => !RpgConstants.IsNoisyKind(e.Kind)).ToList();
        if (live.Count == 0) return;
        if (live.Count == 1)
            await _hub.Clients.Group(RpgConstants.WebGroup).SendAsync("Event", live[0]).ConfigureAwait(false);
        else
            await _hub.Clients.Group(RpgConstants.WebGroup).SendAsync("EventBatch", new EventBatch { Events = live }).ConfigureAwait(false);
    }

    async Task BroadcastActivityAsync(IReadOnlyList<long> playerIds)
    {
        if (playerIds.Count == 0) return;
        foreach (var playerId in playerIds.Distinct())
        {
            var rollup = _store.GetPvzActivityRollup(playerId);
            if (rollup is null) continue;
            await _hub.Clients.Group(RpgConstants.WebGroup).SendAsync("PvzActivityUpdated", rollup).ConfigureAwait(false);
            await _hub.Clients.Group(RpgConstants.InjectorGroup)
                .SendAsync("PvzActivityUpdated", new { playerId, revision = rollup.Revision }).ConfigureAwait(false);
        }
    }

    async Task BroadcastProgressionAsync(IReadOnlyList<RpgProgressionDirty> dirty)
    {
        if (dirty.Count == 0) return;
        foreach (var d in dirty)
        {
            await _hub.Clients.Group(RpgConstants.WebGroup).SendAsync("RpgProgressionUpdated", new
            {
                playerId = d.PlayerId,
                kind = d.Kind,
                typeId = d.TypeId,
                revision = d.Revision
            }).ConfigureAwait(false);
        }
    }
}
