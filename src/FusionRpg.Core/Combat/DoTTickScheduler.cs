using FusionRpg.Contracts;
using FusionRpg.Core.Effects;

namespace FusionRpg.Core.Combat;

public sealed class DoTEntry
{
    public string GrantId { get; init; } = "";
    public DamagePacket Template { get; init; } = new();
    public EffectEventDto Event { get; init; } = new();
    public string TargetPtr { get; init; } = "";
    public int PeriodMs { get; init; }
    public int DurationMs { get; init; }
    public int TickBudget { get; init; } = 1;
    public DateTimeOffset Start { get; init; }
    public DateTimeOffset Next { get; set; }
    public DateTimeOffset End { get; init; }
    public int TicksFired { get; set; }
}

/// <summary>Match-clock DoT. Caller coalesces (e.g. 100ms). Not per-frame required.</summary>
public sealed class DoTTickScheduler
{
    readonly List<DoTEntry> _entries = new();

    public IReadOnlyList<DoTEntry> Entries => _entries;

    public void Clear() => _entries.Clear();

    public void ClearGrant(string grantId) =>
        _entries.RemoveAll(e => string.Equals(e.GrantId, grantId, StringComparison.OrdinalIgnoreCase));

    public void Register(DoTEntry entry)
    {
        if (entry.PeriodMs <= 0) return;
        _entries.Add(entry);
    }

    public int Tick(
        DateTimeOffset now,
        BoardSnapshot snapshot,
        EffectFunnel? funnel,
        CombatPolicy policy,
        ICombatRng rng,
        ICombatMath math,
        List<string>? skipped)
    {
        var n = 0;
        for (var i = _entries.Count - 1; i >= 0; i--)
        {
            var e = _entries[i];
            // Inclusive end so period=1000 / duration=5000 yields ticks at 1000..5000.
            if (e.Next > e.End)
            {
                _entries.RemoveAt(i);
                continue;
            }

            if (now < e.Next) continue;

            var budget = e.TickBudget > 0 ? e.TickBudget : 1;
            var firedThisPulse = 0;
            while (now >= e.Next && e.Next <= e.End && firedThisPulse < budget)
            {
                var packet = CloneInstant(e.Template, e.GrantId);
                var ev = CloneEvent(e.Event);
                n += CombatDamageDispatcher.DispatchInstant(
                    packet, snapshot, ev, funnel, policy, rng, math, skipped);
                e.TicksFired++;
                firedThisPulse++;
                e.Next = e.Next.AddMilliseconds(e.PeriodMs);
            }

            if (e.Next > e.End)
                _entries.RemoveAt(i);
        }

        return n;
    }

    static EffectEventDto CloneEvent(EffectEventDto src) => new()
    {
        Trigger = EffectTriggers.OnTimer,
        MatchKey = src.MatchKey,
        Side = src.Side,
        ActorPtr = src.ActorPtr,
        TargetPtr = src.TargetPtr,
        TypeId = src.TypeId,
        TargetTypeId = src.TargetTypeId,
        Tick = src.Tick,
        ScenarioId = src.ScenarioId,
        ChainDepth = src.ChainDepth
    };

    static DamagePacket CloneInstant(DamagePacket src, string grantId) => new()
    {
        PacketId = Guid.NewGuid().ToString("N"),
        SourceGrantId = grantId,
        EffectId = src.EffectId,
        PluginId = src.PluginId,
        ActorPtr = src.ActorPtr,
        SignedAmount = src.SignedAmount,
        Channel = src.Channel,
        ChainDepth = src.ChainDepth + 1,
        ProcDepthLimit = src.ProcDepthLimit,
        Target = src.Target ?? new TargetSpec { Mode = TargetModes.EventTarget },
        Delivery = new DeliverySpec { Mode = DeliveryModes.Instant }
    };
}
