using FusionRpg.Core.Commanders;
using Xunit;

namespace FusionRpg.Core.Tests.Commanders;

/// <summary>aura-skill T9a: an addressable commander identity, distinct from
/// `WorldFaction.FactionId` (bare `"dave"`/`"zomboss"`, a world-map concept) and from
/// `BattleActorSetup.Key` (`"squad:N"`/`"wave:N"`, a per-match concept) — neither of which survives
/// across matches or the world map, which is exactly what T9b/T9c need to attach to.</summary>
public class CommanderIdTests
{
    [Theory]
    [InlineData(CommanderId.Dave, "commander:dave")]
    [InlineData(CommanderId.Zomboss, "commander:zomboss")]
    public void Stable_id_has_the_commander_prefix(CommanderId id, string expected)
    {
        Assert.Equal(expected, id.ToStableId());
    }

    [Fact]
    public void Exactly_two_commanders_exist_today()
    {
        // Owner decision (2026-08-30): "for now only have 2 of them for lawn run." A third entry
        // here is a reviewed program decision, not an incidental addition.
        Assert.Equal(2, CommanderIds.All.Count);
        Assert.Contains(CommanderId.Dave, CommanderIds.All);
        Assert.Contains(CommanderId.Zomboss, CommanderIds.All);
    }

    [Theory]
    [InlineData(CommanderId.Dave)]
    [InlineData(CommanderId.Zomboss)]
    public void Stable_id_never_collides_with_a_bare_WorldFaction_FactionId(CommanderId id)
    {
        // WorldTemplateCatalog's Dave/Zomboss consts are bare "dave"/"zomboss" -- no prefix. The
        // commander: prefix is what keeps these two id spaces from ever aliasing to the same string.
        Assert.NotEqual("dave", id.ToStableId());
        Assert.NotEqual("zomboss", id.ToStableId());
        Assert.StartsWith("commander:", id.ToStableId(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CommanderId.Dave)]
    [InlineData(CommanderId.Zomboss)]
    public void Stable_id_never_collides_with_a_BattleActorSetup_key_shape(CommanderId id)
    {
        // BattleActorSetup.Key values are "squad:N" / "wave:N" -- a different namespace prefix
        // entirely, so no commander id can ever alias a real battle actor's key.
        Assert.DoesNotContain("squad:", id.ToStableId(), StringComparison.Ordinal);
        Assert.DoesNotContain("wave:", id.ToStableId(), StringComparison.Ordinal);
    }

    [Fact]
    public void The_two_stable_ids_never_collide_with_each_other()
    {
        Assert.NotEqual(CommanderId.Dave.ToStableId(), CommanderId.Zomboss.ToStableId());
    }

    [Fact]
    public void Daves_allocation_scope_key_matches_AptitudeEndpoints_ScopeKey_shape_exactly()
    {
        // Core cannot reference FusionRpg.Server (wrong dependency direction), so this pins the
        // literal string shape AptitudeEndpoints.ScopeKey(playerId) => $"player:{playerId}" produces,
        // by convention rather than by a shared reference. A regression here is the signal that the
        // two have drifted and need reconciling by hand.
        Assert.Equal("player:42", CommanderId.Dave.AllocationScopeKey(42));
    }

    [Fact]
    public void Zombosss_allocation_scope_key_is_a_sibling_not_a_collision_with_Daves()
    {
        var daveKey = CommanderId.Dave.AllocationScopeKey(42);
        var zombossKey = CommanderId.Zomboss.AllocationScopeKey(42);

        Assert.NotEqual(daveKey, zombossKey); // same playerId, must not resolve to the same store row
        Assert.Equal("zomboss:42", zombossKey);
    }

    [Theory]
    [InlineData("commander:dave", CommanderId.Dave)]
    [InlineData("commander:zomboss", CommanderId.Zomboss)]
    public void TryParseStableId_round_trips_known_ids(string stableId, CommanderId expected)
    {
        Assert.True(CommanderIds.TryParseStableId(stableId, out var parsed));
        Assert.Equal(expected, parsed);
        Assert.Equal(stableId, parsed.ToStableId());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("dave")]
    [InlineData("commander:penny")]
    [InlineData("not-a-commander")]
    public void TryParseStableId_rejects_unknown_strings(string? stableId)
    {
        Assert.False(CommanderIds.TryParseStableId(stableId, out _));
    }

    [Fact]
    public void Different_players_get_different_scope_keys_for_the_same_commander()
    {
        Assert.NotEqual(
            CommanderId.Dave.AllocationScopeKey(1),
            CommanderId.Dave.AllocationScopeKey(2));
        Assert.NotEqual(
            CommanderId.Zomboss.AllocationScopeKey(1),
            CommanderId.Zomboss.AllocationScopeKey(2));
    }
}
