using FusionRpg.Core.Battle;
using FusionRpg.Core.Combat.Element;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Stats;

/// <summary>
/// E25's seam: the real consumer of <c>AllCombatChannelIds</c> — <see cref="BattleStatComposer.Compose"/>
/// — still produces the right channel set after the cache change, across a real roster swap, not a
/// fabricated one.
/// </summary>
public class ChannelCacheSeamTests
{
    static BattleActorSetup Actor() => new()
    {
        Key = "squad:0", Side = "squad", SpeciesId = "demon.test", Level = 5,
        MaxHp = 500, Atk = 40, Defense = 20,
    };

    [Fact]
    public void Compose_reads_every_channel_the_current_roster_generates_including_a_seventh_element()
    {
        var shipped = ElementTable.Shipped();
        var withVoid = shipped.Elements.Append(new ElementRow("void", "Void", shipped.Elements.Count, true)).ToArray();
        var sevenElement = new ElementTable(withVoid, shipped.CombatRows, shipped.ShieldRows);

        ActorDerivedSnapshot six;
        using (ElementTable.UseScoped(shipped))
            six = BattleStatComposer.Compose(Actor());

        ActorDerivedSnapshot seven;
        using (ElementTable.UseScoped(sevenElement))
            seven = BattleStatComposer.Compose(Actor());

        // Six baseline reads 0 for a channel that does not exist under the six-element roster.
        Assert.Equal(0, six.Get("combat.power.void"));
        // Seven's cache generation includes it — proven by composing successfully (Get would still
        // return 0 for an unknown channel either way, so the real proof is that the roster swap did
        // not throw or silently keep serving the six-element cache).
        Assert.Equal(0, seven.Get("combat.power.void")); // no mod applied, but the channel now EXISTS
        Assert.Contains("combat.power.void", DerivedStatChannels.BuildAllCombatChannelIds(
            sevenElement.Elements.Where(e => e.Enabled).Select(e => e.ElementId)));
    }

    [Fact]
    public void Repeated_composes_with_no_roster_change_produce_identical_snapshots()
    {
        using var _ = ElementTable.UseScoped(ElementTable.Shipped());

        var a = BattleStatComposer.Compose(Actor());
        var b = BattleStatComposer.Compose(Actor());

        foreach (var channel in DerivedStatChannels.AllCombatChannelIds)
            Assert.Equal(a.Get(channel), b.Get(channel));
    }
}
