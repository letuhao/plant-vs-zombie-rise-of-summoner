using FusionRpg.Core.Combat;
using FusionRpg.Core.Stats.Derived;
using Xunit;

namespace FusionRpg.Core.Tests.Combat;

/// <summary>
/// E28 fix #1 (spec-param-parity.md §3 row 1): the resource-pool registry for LAWN actors, keyed by
/// combat ptr rather than <c>CommanderId</c> — the same keying <c>InjectorEntityRegistry</c> already
/// uses for every other live-match lookup. Mirrors <c>CommanderResourcePoolsTests</c>' own shape one
/// keying scheme over: same wrapped <c>ActorResourcePools</c>, same "same instance every later call"
/// contract, different key type.
/// </summary>
public class LawnActorResourcePoolsTests
{
    static ActorDerivedSnapshot BaselineDerived() => ActorDerivedSnapshot.FromValues(new[]
    {
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("hp"), 100),
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("stamina"), 50),
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("hunger"), 100),
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("spirit"), 20),
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("qi"), 30),
        new KeyValuePair<string, double>(DerivedStatChannels.ResourceMax("poise"), 10),
    });

    [Fact]
    public void Each_ptr_gets_its_own_pool_starting_at_max()
    {
        var pools = new LawnActorResourcePools();
        var actor = pools.GetOrCreate("0xABC123", BaselineDerived(), atTick: 0);

        Assert.Equal(100, actor.Resolve("hp", 0, BaselineDerived()));
        Assert.Equal(50, actor.Resolve("stamina", 0, BaselineDerived()));
        Assert.Equal(10, actor.Resolve("poise", 0, BaselineDerived()));
    }

    [Fact]
    public void GetOrCreate_returns_the_SAME_instance_on_a_later_call_for_the_same_ptr()
    {
        var pools = new LawnActorResourcePools();
        var derived = BaselineDerived();
        var first = pools.GetOrCreate("0xDEAD", derived, atTick: 0);

        first.Add("stamina", -30, nowTick: 100, derived);

        var second = pools.GetOrCreate("0xDEAD", derived, atTick: 200);

        Assert.Same(first, second); // not a fresh instance
        Assert.Equal(20, second.Resolve("stamina", 200, derived)); // 50 - 30 spent, still gone
    }

    /// <summary>Test 11 of the spec's own testing strategy shape, one layer down: two different
    /// targetPtrs never bleed into each other's pool — a drain on one actor must not touch another
    /// actor's pool of the same resource.</summary>
    [Fact]
    public void Two_different_ptrs_are_fully_independent_no_cross_actor_bleed()
    {
        var pools = new LawnActorResourcePools();
        var derived = BaselineDerived();
        var zombieA = pools.GetOrCreate("0x1111", derived, atTick: 0);
        var plantB = pools.GetOrCreate("0x2222", derived, atTick: 0);

        zombieA.Add("hp", -50, nowTick: 0, derived);

        Assert.Equal(50, zombieA.Resolve("hp", 0, derived));
        Assert.Equal(100, plantB.Resolve("hp", 0, derived)); // untouched by A's drain
    }

    /// <summary>Ptr normalization (<see cref="CombatPtr.Normalize"/>) means "0xABC", "entity:abc"
    /// and "ABC" must all resolve to the SAME pool — matching how every other Injector-side lookup
    /// keys a live actor.</summary>
    [Fact]
    public void Ptr_normalization_makes_0xABC_entity_abc_and_ABC_the_same_pool()
    {
        var pools = new LawnActorResourcePools();
        var derived = BaselineDerived();
        var viaHexPrefix = pools.GetOrCreate("0xABC", derived, atTick: 0);

        viaHexPrefix.Add("qi", -5, nowTick: 0, derived);

        var viaEntityPrefix = pools.GetOrCreate("entity:abc", derived, atTick: 0);
        var viaBare = pools.GetOrCreate("ABC", derived, atTick: 0);

        Assert.Same(viaHexPrefix, viaEntityPrefix);
        Assert.Same(viaHexPrefix, viaBare);
        Assert.Equal(25, viaBare.Resolve("qi", 0, derived));
    }

    [Fact]
    public void TryGet_reports_false_for_a_ptr_never_created()
    {
        var pools = new LawnActorResourcePools();
        Assert.False(pools.TryGet("0xNEVER", out _));
    }

    [Fact]
    public void Remove_drops_one_actors_pool_without_touching_another()
    {
        var pools = new LawnActorResourcePools();
        var derived = BaselineDerived();
        pools.GetOrCreate("0xAAA", derived, atTick: 0);
        pools.GetOrCreate("0xBBB", derived, atTick: 0);

        var removed = pools.Remove("0xAAA");

        Assert.True(removed);
        Assert.False(pools.TryGet("0xAAA", out _));
        Assert.True(pools.TryGet("0xBBB", out _));
        Assert.Equal(1, pools.Count);
    }

    [Fact]
    public void Clear_drops_every_pool()
    {
        var pools = new LawnActorResourcePools();
        var derived = BaselineDerived();
        pools.GetOrCreate("0xAAA", derived, atTick: 0);
        pools.GetOrCreate("0xBBB", derived, atTick: 0);

        pools.Clear();

        Assert.Equal(0, pools.Count);
        Assert.False(pools.TryGet("0xAAA", out _));
    }
}
