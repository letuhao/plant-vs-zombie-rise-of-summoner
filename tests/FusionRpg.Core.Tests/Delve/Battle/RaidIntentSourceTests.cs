using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Timeline;
using FusionRpg.Core.Delve.Battle;
using Xunit;

namespace FusionRpg.Core.Tests.Delve.Battle;

/// <summary>
/// D2.13 — <see cref="RaidIntentSource"/>: the steered party's actors dispatch to an interactive
/// source, everyone else (the raid's other parties AND every wave enemy) dispatches to one shared
/// automated source. Tested the same way `SiegeAiTests` proves `SiegeIntentSource`'s own
/// dispatch/fallthrough symmetry — a fixed, hand-rolled `IIntentSource` double, no live battle
/// engine needed to prove pure routing logic.
/// </summary>
public class RaidIntentSourceTests
{
    sealed class FixedIntentSource : IIntentSource
    {
        readonly string _actionId;
        public FixedIntentSource(string actionId) => _actionId = actionId;
        public ActionIntent TryDeclare(string actorKey, long nowTick) => new(_actionId, null, null!);
    }

    static BattleActorSetup PartyActor(string key, int? partyIndex) => new() { Key = key, Side = "squad", PartyIndex = partyIndex };

    [Fact]
    public void The_steered_partys_own_actors_read_the_interactive_source()
    {
        var steered = new FixedIntentSource("player-move");
        var automated = new FixedIntentSource("ai-move");
        var source = new RaidIntentSource(steered, automated, new HashSet<string> { "squad:0", "squad:1" });

        Assert.Equal("player-move", source.TryDeclare("squad:0", 0).ActionId);
        Assert.Equal("player-move", source.TryDeclare("squad:1", 0).ActionId);
    }

    [Fact]
    public void A_four_party_raid_routes_one_steered_party_and_three_automated_parties_plus_the_wave_correctly()
    {
        var setup = new BattleSetup
        {
            Squad = new[]
            {
                PartyActor("squad:p0", partyIndex: 0),
                PartyActor("squad:p1", partyIndex: 1),
                PartyActor("squad:p2", partyIndex: 2),
                PartyActor("squad:p3", partyIndex: 3),
            },
            Wave = new[] { new BattleActorSetup { Key = "wave:0", Side = "wave" } }
        };

        var steeredKeys = RaidIntentSource.KeysForParty(setup, partyIndex: 0);
        Assert.Equal(new[] { "squad:p0" }, steeredKeys);

        var source = new RaidIntentSource(new FixedIntentSource("player-move"), new FixedIntentSource("ai-move"), steeredKeys);

        Assert.Equal("player-move", source.TryDeclare("squad:p0", 0).ActionId); // the one steered party
        Assert.Equal("ai-move", source.TryDeclare("squad:p1", 0).ActionId);     // three automated parties
        Assert.Equal("ai-move", source.TryDeclare("squad:p2", 0).ActionId);
        Assert.Equal("ai-move", source.TryDeclare("squad:p3", 0).ActionId);
        Assert.Equal("ai-move", source.TryDeclare("wave:0", 0).ActionId);       // every wave enemy too
    }

    [Fact]
    public void An_actor_with_no_party_index_falls_through_to_automated()
    {
        // Every existing non-raid caller's shape (PartyIndex null) -- must fall to the shared
        // automated source exactly like an un-steered party would, never accidentally match steered.
        var source = new RaidIntentSource(new FixedIntentSource("player-move"), new FixedIntentSource("ai-move"), new HashSet<string>());
        Assert.Equal("ai-move", source.TryDeclare("squad:0", 0).ActionId);
    }

    [Fact]
    public void Replay_with_the_same_dispatch_is_byte_identical()
    {
        var source = new RaidIntentSource(new FixedIntentSource("player-move"), new FixedIntentSource("ai-move"), new HashSet<string> { "squad:p0" });
        var first = (source.TryDeclare("squad:p0", 0).ActionId, source.TryDeclare("squad:p1", 0).ActionId);
        var replay = (source.TryDeclare("squad:p0", 0).ActionId, source.TryDeclare("squad:p1", 0).ActionId);
        Assert.Equal(first, replay); // the dispatch is a pure function of (actorKey, key set) -- no hidden state to drift
    }

    sealed class NeverActs : IIntentSource
    {
        public ActionIntent TryDeclare(string actorKey, long nowTick) => ActionIntent.None;
    }

    sealed class SpyIntentSource : IIntentSource
    {
        public List<string> Seen { get; } = new();
        public ActionIntent TryDeclare(string actorKey, long nowTick)
        {
            Seen.Add(actorKey);
            return ActionIntent.None;
        }
    }

    /// <summary>D2.16's own verify line: "a test asserts a steered fight is never finished by the
    /// automated policy." Dispatch is an unconditional key-set branch (no code path falls a steered
    /// key through to `_automated`), so this holds even when the steered source itself declares
    /// nothing (a timed-out player with no legal fallback) — the automated policy must never be
    /// consulted for that actor merely because the steered source came back empty.</summary>
    [Fact]
    public void A_steered_actor_that_declares_nothing_is_never_handed_to_the_automated_policy()
    {
        var automated = new SpyIntentSource();
        var source = new RaidIntentSource(new NeverActs(), automated, new HashSet<string> { "squad:p0" });

        var intent = source.TryDeclare("squad:p0", 0);

        Assert.True(intent.IsNone);
        Assert.DoesNotContain("squad:p0", automated.Seen);   // the automated policy was never asked
    }

    [Fact]
    public void Null_constructor_arguments_throw()
    {
        Assert.Throws<ArgumentNullException>(() => new RaidIntentSource(null!, new FixedIntentSource("x"), new HashSet<string>()));
        Assert.Throws<ArgumentNullException>(() => new RaidIntentSource(new FixedIntentSource("x"), null!, new HashSet<string>()));
        Assert.Throws<ArgumentNullException>(() => new RaidIntentSource(new FixedIntentSource("x"), new FixedIntentSource("y"), null!));
    }
}
