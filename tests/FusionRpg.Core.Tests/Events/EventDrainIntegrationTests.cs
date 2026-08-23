using FusionRpg.Contracts;
using FusionRpg.Core.Combat;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Events;
using Xunit;

namespace FusionRpg.Core.Tests.Events;

/// <summary>
/// End-to-end: ring → coalescer → drain → real EffectBag. Proves the drain's DTO conventions
/// (side flip, ptr hex, HitCount) actually drive grant matching and merged proc math — a
/// mismatch here would be invisible to the fake-processor drain tests until live.
/// </summary>
public class EventDrainIntegrationTests
{
    static FoundationHarness CounterHarness()
    {
        var h = new FoundationHarness();
        h.SetBoard(new[]
        {
            new BoardEntitySnap { Ptr = "B", Side = "zombie", TypeId = 0, Col = 7, Row = 2 }
        });
        h.Grant(new EffectGrantDto
        {
            GrantId = "streak",
            EffectId = "fx.overlay_damage",
            OwnerKey = EffectOwnerKeys.Match,
            Overlay = new Dictionary<string, object?>
            {
                ["icd_ms"] = 0,
                ["delivery"] = new Dictionary<string, object?>
                {
                    ["mode"] = DeliveryModes.Counter,
                    ["everyHits"] = 5,
                    ["resetOnBurst"] = true,
                    ["counterScope"] = CounterScopes.Target
                },
                ["burst"] = new Dictionary<string, object?>
                {
                    ["amount"] = -500L,
                    ["target"] = new Dictionary<string, object?> { ["mode"] = TargetModes.EventTarget },
                    ["delivery"] = new Dictionary<string, object?> { ["mode"] = DeliveryModes.Instant }
                }
            }
        });
        return h;
    }

    static GameEventRec Hit(EventDrain drain, long amount = -10) => new(
        GameEventKind.CombatHit, frame: 1, seq: drain.NextSeq(),
        actorPtr: new IntPtr(0xA), targetPtr: new IntPtr(0xB),
        typeId: 1, targetTypeId: 0, side: GameEventSide.Zombie,
        amount: amount, hitCount: 1, chainDepth: 0,
        sourceGrantIdx: -1, matchKeyIdx: -1, pairId: 0);

    [Fact]
    public void Merged_record_advances_counter_by_hit_count_and_bursts()
    {
        var h = CounterHarness();
        IntentPlanDto? last = null;
        var drain = new EventDrain(dto => last = h.OnEvent(dto));

        // Five same-key hits recorded in one frame → coalesce to one record, HitCount=5 →
        // the every-5 counter must burst on that single drained event.
        for (var i = 0; i < 5; i++)
            drain.Record(Hit(drain));

        var stats = drain.Drain(budgetTicks: long.MaxValue);
        Assert.Equal(1, stats.Processed);
        Assert.NotNull(last);
        var fa = Assert.Single(last!.Actions.Where(a => a.Action == EffectActions.ApplyResourceDelta));
        Assert.Equal(-500L, Convert.ToInt64(fa.Params["amount"]));
        Assert.Equal("b", fa.Params["targetPtr"]?.ToString(), ignoreCase: true);
    }

    [Fact]
    public void Four_merged_hits_do_not_burst()
    {
        var h = CounterHarness();
        IntentPlanDto? last = null;
        var drain = new EventDrain(dto => last = h.OnEvent(dto));

        for (var i = 0; i < 4; i++)
            drain.Record(Hit(drain));
        drain.Drain(long.MaxValue);

        Assert.NotNull(last);
        Assert.DoesNotContain(last!.Actions, a => a.Action == EffectActions.ApplyResourceDelta);
    }

    [Fact]
    public void Session_mode_five_individual_events_burst_on_fifth()
    {
        var h = CounterHarness();
        var plans = new List<IntentPlanDto>();
        var drain = new EventDrain(dto => plans.Add(h.OnEvent(dto))) { SessionMode = true };

        for (var i = 0; i < 5; i++)
            drain.Record(Hit(drain));
        drain.Drain(0);

        Assert.Equal(5, plans.Count); // no coalescing in sessions — v1 fidelity
        Assert.DoesNotContain(plans[3].Actions, a => a.Action == EffectActions.ApplyResourceDelta);
        Assert.Contains(plans[4].Actions, a => a.Action == EffectActions.ApplyResourceDelta);
    }

    [Fact]
    public void Extreme_merged_amounts_preserve_the_exact_value()
    {
        // P0.4 (power overflow audit): EffectEventDto.Damage is long, so a merged amount past
        // int32 range is no longer clamped at this boundary — it is exact, the same way a single
        // RPG-scaled hit already was. This test used to assert the clamp itself
        // (`Assert.Equal(int.MinValue, seen.Damage)`); that assertion encoded the defect the
        // widening fixes, not a property worth keeping.
        var h = CounterHarness();
        EffectEventDto? seen = null;
        var drain = new EventDrain(dto => { seen = dto; h.OnEvent(dto); });

        drain.Record(Hit(drain, amount: -(long)int.MaxValue));
        drain.Record(Hit(drain, amount: -(long)int.MaxValue));
        drain.Drain(long.MaxValue);

        Assert.NotNull(seen);
        Assert.Equal(-2L * int.MaxValue, seen!.Damage); // exact merged sum, well inside long
        Assert.Equal(2, seen.HitCount);
    }

    [Fact]
    public void Drained_dealt_side_is_attacker_side()
    {
        var h = CounterHarness();
        EffectEventDto? seen = null;
        var drain = new EventDrain(dto => { seen = dto; h.OnEvent(dto); });

        drain.Record(Hit(drain)); // record side = target (zombie)
        drain.Drain(long.MaxValue);

        Assert.NotNull(seen);
        Assert.Equal(EffectTriggers.OnDamageDealt, seen!.Trigger);
        Assert.Equal("plant", seen.Side); // DTO side = attacker, mirroring EffectEventAdapterCore
    }
}
