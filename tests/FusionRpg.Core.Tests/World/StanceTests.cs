using FusionRpg.Core.World;
using FusionRpg.Core.World.Intel;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Movement;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// W23 (spec-world-movement.md §What `hold` is for): stances finally do something.
///
/// `stance` was in the movement spec's command table from wave 1 and was never a command kind, so a
/// legion's posture was whatever the template authored and could never change — which made both
/// `scout` and `hold` dead letters. Closing that turned up something worse: **nothing in the game
/// could heal**. Wounds only ever accumulated, so every legion was on a one-way trip to death.
/// </summary>
public class StanceTests
{
    static WorldState World() => WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);

    static WorldCommand Stance(string entityId, string stance) => new()
    {
        CommanderId = "dave",
        CommandId = "s-" + entityId,
        Kind = WorldCommandKinds.Stance,
        EntityId = entityId,
        Stance = stance
    };

    static WorldCommand Move(string entityId, params string[] lanePath) => new()
    {
        CommanderId = "dave",
        CommandId = "m-" + entityId,
        Kind = WorldCommandKinds.Move,
        EntityId = entityId,
        LanePath = lanePath
    };

    static WorldEntity Legion(WorldState w) => w.Entities.Single(e => e.EntityId == "e-dave-legion-1");

    static WorldState Wounded(WorldState w, int wounds) => w with
    {
        Entities = w.Entities
            .Select(e => e.EntityId == "e-dave-legion-1"
                ? e with { Members = e.Members.Select(m => m with { Wounds = wounds }).ToList() }
                : e)
            .ToList()
    };

    static WorldState WithCarriedLoam(WorldState w, long amount) => w with
    {
        Entities = w.Entities
            .Select(e => e.EntityId == "e-dave-legion-1" ? e with { CarriedLoam = amount } : e)
            .ToList()
    };

    // ---- the command ---------------------------------------------------------------------

    [Fact]
    public void A_legion_can_be_told_to_change_posture()
    {
        var result = TurnEngine.Step(World(), new[] { Stance("e-dave-legion-1", "scout") }, seed: 1);

        Assert.Equal("scout", Legion(result.World).Stance);
        Assert.Empty(result.Report.Dropped);
    }

    [Fact]
    public void A_posture_nobody_has_heard_of_is_refused_at_admission()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World(), Stance("e-dave-legion-1", "skulk"));

        Assert.False(ok);
        Assert.Equal("stance.unknown", reason);
    }

    [Fact]
    public void Committing_to_a_posture_costs_the_turn_you_commit()
    {
        // Digging in and *then* marching your full distance would make the defensive bonus free.
        var world = World();
        var first = TurnEngine.Step(world, new[] { Stance("e-dave-legion-1", "hold") }, seed: 1);

        // The legion still had its full budget during the turn it gave the order…
        Assert.Equal("hold", Legion(first.World).Stance);
        // …and only pays for it at the refill that closes that turn.
        Assert.Equal(0, Legion(first.World).MovementRemaining);
    }

    // ---- scout ---------------------------------------------------------------------------

    [Fact]
    public void Scouting_costs_half_a_turns_march()
    {
        var scouting = TurnEngine.Step(World(), new[] { Stance("e-dave-legion-1", "scout") }, seed: 1);

        Assert.Equal(MovementPolicy.ScoutPointsPerTurn, Legion(scouting.World).MovementRemaining);
        Assert.Equal(MovementPolicy.PointsPerTurn / 2, Legion(scouting.World).MovementRemaining);
    }

    [Fact]
    public void Scouting_buys_twice_the_sight()
    {
        var scouting = TurnEngine.Step(World(), new[] { Stance("e-dave-legion-1", "scout") }, seed: 1);
        var view = new BelievedWorldView(scouting.World, "dave");

        // Two lanes out is normally invisible; a scout sees it.
        Assert.NotEqual(IntelState.Unknown, view.StateOf("ash-waste"));
        Assert.Equal(IntelState.Watched, view.StateOf("ash-waste"));
    }

    // ---- hold ----------------------------------------------------------------------------

    [Fact]
    public void A_held_legion_cannot_march_and_is_told_why()
    {
        var dug_in = TurnEngine.Step(World(), new[] { Stance("e-dave-legion-1", "hold") }, seed: 1);
        var result = TurnEngine.Step(dug_in.World, new[] { Move("e-dave-legion-1", "l-home-ember") }, seed: 1);

        Assert.Contains(result.Report.Dropped, e => e.Detail == "entity.held");
        Assert.Equal("homeworld", Legion(result.World).AtSectorId);
    }

    [Fact]
    public void Holding_in_supply_recovers_wounds()
    {
        var world = Wounded(World(), wounds: 90);
        var held = world with
        {
            Entities = world.Entities
                .Select(e => e.EntityId == "e-dave-legion-1" ? e with { Stance = "hold" } : e)
                .ToList()
        };

        var result = TurnEngine.Step(held, Array.Empty<WorldCommand>(), seed: 1);

        var after = Legion(result.World).Members.First();
        Assert.True(after.Wounds < 90, "a garrison in supply should be recovering");
        Assert.Equal(90 - 110 * MovementPolicy.RecoveryMilli / 1000, after.Wounds);
    }

    [Fact]
    public void Recovery_never_takes_a_member_past_whole()
    {
        var world = Wounded(World(), wounds: 3);
        var held = world with
        {
            Entities = world.Entities
                .Select(e => e.EntityId == "e-dave-legion-1" ? e with { Stance = "hold" } : e)
                .ToList()
        };

        var result = TurnEngine.Step(held, Array.Empty<WorldCommand>(), seed: 1);
        Assert.All(Legion(result.World).Members, m => Assert.Equal(0, m.Wounds));
    }

    /// <summary>
    /// Rewritten for spec-loam-legions.md: the currency is carried loam now, not wounds, but the
    /// property is the same one — holding is not a substitute for a supply line, it feeds nobody.
    /// </summary>
    [Fact]
    public void Standing_still_out_of_supply_still_burns()
    {
        var world = WithCarriedLoam(World(), amount: 100);
        var stranded = world with
        {
            Entities = world.Entities
                .Select(e => e.EntityId == "e-dave-legion-1"
                    ? e with { Stance = "hold", AtSectorId = "verdant-shelf" }
                    : e)
                .ToList()
        };

        var burn = LegionSupply.Burn(Legion(stranded));
        var result = TurnEngine.Step(stranded, Array.Empty<WorldCommand>(), seed: 1);

        Assert.Equal(100 - burn, Legion(result.World).CarriedLoam);
    }

    [Fact]
    public void A_marching_legion_recovers_nothing_even_at_home()
    {
        var world = Wounded(World(), wounds: 40);
        var result = TurnEngine.Step(world, Array.Empty<WorldCommand>(), seed: 1);

        Assert.Equal(40, Legion(result.World).Members.First().Wounds);
    }

    [Fact]
    public void A_dug_in_defender_counts_as_stationary_even_when_nobody_moved()
    {
        // `DefenderStationary` false on purpose: with it true this test would pass whether or not
        // the stance is read at all, which is exactly what it did before.
        var request = new BattleRequest
        {
            BattleId = "b", Kind = BattleKinds.Sector, LocationId = "x",
            AttackerEntityId = "a", DefenderEntityId = "d", DefenderStationary = false
        };

        var attacker = Legion(World()) with { EntityId = "a" };

        // Evenly matched, so entrenchment is the only thing that can decide it.
        var entrenched = Legion(World()) with { EntityId = "d", Stance = "hold" };
        Assert.Equal("d", PlaceholderBattleResolver.Instance.Resolve(request, new[] { attacker, entrenched }, seed: 1).WinnerEntityId);

        // The control: the same two forces with nobody dug in destroy each other, which is what
        // proves the assertion above is not passing by accident.
        var afoot = Legion(World()) with { EntityId = "d", Stance = "march" };
        Assert.Null(PlaceholderBattleResolver.Instance.Resolve(request, new[] { attacker, afoot }, seed: 1).WinnerEntityId);
    }

    [Fact]
    public void Stances_are_deterministic_and_independent_of_command_order()
    {
        var a = Stance("e-dave-legion-1", "scout");
        var b = new WorldCommand { CommanderId = "wild", CommandId = "s1", Kind = WorldCommandKinds.StandFast };

        Assert.Equal(
            TurnEngine.Step(World(), new[] { a, b }, seed: 1).StateHash,
            TurnEngine.Step(World(), new[] { b, a }, seed: 1).StateHash);
    }
}
