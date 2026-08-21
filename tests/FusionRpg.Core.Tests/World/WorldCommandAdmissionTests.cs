using FusionRpg.Core.World;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// W5 (spec-turn-engine.md §Commands): admission is the cheap gate at submit time — well-formed,
/// references exist, the commander owns the subject. Legality *at reveal* is the engine's job and
/// drops a command into the report instead of throwing.
/// </summary>
public class WorldCommandAdmissionTests
{
    static readonly WorldState World =
        WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);

    static WorldCommand StandFast(string commander = "dave", string id = "c1", string? entityId = null) =>
        new() { CommanderId = commander, CommandId = id, Kind = WorldCommandKinds.StandFast, EntityId = entityId };

    [Fact]
    public void A_faction_wide_stand_fast_is_admitted()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast());
        Assert.True(ok, reason);
    }

    [Fact]
    public void Standing_one_of_your_own_legions_fast_is_admitted()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(entityId: "e-dave-legion-1"));
        Assert.True(ok, reason);
    }

    [Fact]
    public void An_unknown_kind_is_refused()
    {
        var cmd = StandFast() with { Kind = "teleport-everything" };
        var (ok, reason) = WorldCommandAdmission.Admit(World, cmd);
        Assert.False(ok);
        Assert.Equal("kind.unknown", reason);
    }

    [Fact]
    public void An_unknown_commander_is_refused()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(commander: "nobody"));
        Assert.False(ok);
        Assert.Equal("commander.unknown", reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void A_missing_command_id_is_refused(string id)
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(id: id));
        Assert.False(ok);
        Assert.Equal("command.id-missing", reason);
    }

    [Fact]
    public void An_overlong_command_id_is_refused()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(id: new string('x', 200)));
        Assert.False(ok);
        Assert.Equal("command.id-too-long", reason);
    }

    [Fact]
    public void A_command_naming_a_missing_entity_is_refused()
    {
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(entityId: "e-ghost"));
        Assert.False(ok);
        Assert.Equal("entity.unknown", reason);
    }

    [Fact]
    public void You_cannot_command_someone_elses_army()
    {
        // e-wild-pack-1 belongs to the wild faction — Dave does not get to order it about.
        var (ok, reason) = WorldCommandAdmission.Admit(World, StandFast(entityId: "e-wild-pack-1"));
        Assert.False(ok);
        Assert.Equal("entity.not-yours", reason);
    }

    [Fact]
    public void The_owning_faction_may_command_its_own_entity()
    {
        var cmd = StandFast(commander: "wild", entityId: "e-wild-pack-1");
        var (ok, reason) = WorldCommandAdmission.Admit(World, cmd);
        Assert.True(ok, reason);
    }
}
