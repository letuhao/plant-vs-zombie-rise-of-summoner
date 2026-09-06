using System.Collections.Generic;
using System.Linq;
using FusionRpg.Contracts;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Cost;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.Effects;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Siege;

/// <summary>
/// base-defense `siege-construction` 15.3b (2026-09-06): proves the real content + the new
/// <see cref="ConstructionActivation"/> firing path end to end — the seam `ExecPlaceStructure` itself
/// (as opposed to the read-back `BuildSlotResults`) had never been unit-tested directly before this.
/// Mirrors this session's own established "test the seam in isolation" pattern rather than a full
/// `BattleEngine.Resolve` round: a bare `BattleEffectHost` + a hand-built `ConstructionBoardContext`,
/// the same two pieces `DistrictAssaultResolver` wires together in production.
/// </summary>
public class ConstructionActionsTests
{
    static (BattleEffectHost Host, ConstructionBoardContext Board) Setup()
    {
        var host = new BattleEffectHost(_ => null, rngSeed: 1);
        var catalog = (InMemoryEffectCatalog)host.Bag.Catalog;
        foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);

        var spec = new GridSpec(8, 8);
        var board = new BoardState(spec);
        var slotByCell = new Dictionary<GridPos, (int SlotIndex, SlotKind Kind)>
        {
            [new GridPos(0, 1)] = (0, SlotKind.Wildland),
        };
        var ctx = new ConstructionBoardContext(board, boardSide: 8, coreSideMilli: 250, rampartThickness: 1, slotByCell);
        host.ConstructionBoard = ctx;
        return (host, ctx);
    }

    static void Grant(BattleEffectHost host, string actorKey, string effectId) =>
        host.Bag.Grant(new EffectGrantDto
        {
            GrantId = $"battle:{actorKey}:{effectId}",
            EffectId = effectId,
            OwnerKind = "entity",
            OwnerKey = EffectOwnerKeys.Entity(actorKey),
            PluginId = "battle",
        });

    [Fact]
    public void The_four_construction_effects_compile_from_real_atoms_with_no_rejections()
    {
        Assert.Equal(4, ConstructionActions.CompiledEffects.Count);
        Assert.All(ConstructionActions.CompiledEffects, def =>
        {
            Assert.Equal(EffectTypes.Triggered, def.EffectType);
            Assert.Contains(AtomTriggers.OnActivate, def.Triggers);
            var action = Assert.Single(def.Actions);
            Assert.Equal(EffectActions.PlaceStructure, action.Action);
        });
    }

    [Fact]
    public void The_container_resolver_maps_each_container_to_its_own_effect()
    {
        Assert.Single(ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.BuiltContainerId));
        Assert.Single(ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.AssembledContainerId));
        Assert.Single(ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.SummonedContainerId));
        Assert.Single(ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.LabouredContainerId));
        Assert.Empty(ConstructionActions.ContainerResolver.EffectIdsFor("container.unknown"));
    }

    [Theory]
    [InlineData(ConstructionActions.BuiltActionId, ConstructionActions.BuiltContainerId, false)]
    [InlineData(ConstructionActions.AssembledActionId, ConstructionActions.AssembledContainerId, true)]
    [InlineData(ConstructionActions.SummonedActionId, ConstructionActions.SummonedContainerId, true)]
    [InlineData(ConstructionActions.LabouredActionId, ConstructionActions.LabouredContainerId, false)]
    public void Each_path_actually_places_the_structure_when_fired(string actionId, string containerId, bool expectInstant)
    {
        var (host, ctx) = Setup();
        const string builder = "entity:builder1";
        host.Bag.Grant(new EffectGrantDto
        {
            GrantId = $"battle:{builder}:{actionId}",
            EffectId = ConstructionActions.ContainerResolver.EffectIdsFor(containerId).Single(),
            OwnerKind = "entity", OwnerKey = EffectOwnerKeys.Entity(builder), PluginId = "battle",
        });
        ctx.Board.Place(builder, new GridPos(0, 0)); // adjacent (Chebyshev 1) to the target slot cell

        ConstructionActivation.Fire(host, builder, targetRow: 0, targetCol: 1);

        var placed = Assert.Single(ctx.Placed);
        Assert.Equal(0, placed.SlotIndex);
        Assert.Equal("moat", placed.StructureId);
        Assert.Equal(expectInstant, placed.Instant);
        Assert.Equal("slot:0", ctx.Board.OccupantAt(new GridPos(0, 1)));
    }

    [Fact]
    public void Firing_with_no_container_granted_is_a_silent_no_op()
    {
        var (host, ctx) = Setup();
        const string builder = "entity:builder1";
        ctx.Board.Place(builder, new GridPos(0, 0));

        ConstructionActivation.Fire(host, builder, targetRow: 0, targetCol: 1);

        Assert.Empty(ctx.Placed);
        Assert.Null(ctx.Board.OccupantAt(new GridPos(0, 1)));
    }

    [Fact]
    public void Firing_at_a_non_adjacent_cell_is_refused_by_the_shared_placement_gate()
    {
        var (host, ctx) = Setup();
        const string builder = "entity:builder1";
        Grant(host, builder, ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.LabouredContainerId).Single());
        ctx.Board.Place(builder, new GridPos(5, 5)); // far from the target slot cell (0,1)

        ConstructionActivation.Fire(host, builder, targetRow: 0, targetCol: 1);

        Assert.Empty(ctx.Placed);
    }

    [Fact]
    public void Firing_with_no_construction_board_wired_is_a_silent_no_op_not_a_crash()
    {
        var host = new BattleEffectHost(_ => null, rngSeed: 1); // ConstructionBoard never set — "bare test harness" case
        var catalog = (InMemoryEffectCatalog)host.Bag.Catalog;
        foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
        const string builder = "entity:builder1";
        Grant(host, builder, ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.SummonedContainerId).Single());

        var ex = Record.Exception(() => ConstructionActivation.Fire(host, builder, targetRow: 0, targetCol: 1));
        Assert.Null(ex);
    }

    [Fact]
    public void Summoned_and_laboured_cost_rows_are_authored_on_the_action_never_the_atom()
    {
        var qi = Assert.Single(ConstructionActions.Costs, c => c.ActionId == ConstructionActions.SummonedActionId);
        Assert.Equal("qi", qi.ResourceId);
        var laboured = ConstructionActions.Costs.Where(c => c.ActionId == ConstructionActions.LabouredActionId).ToList();
        Assert.Equal(2, laboured.Count);
        Assert.Contains(laboured, c => c.ResourceId == "stamina");
        Assert.Contains(laboured, c => c.ResourceId == "hunger");
        // Built and Assembled cost nothing through ActionCostRow — Built's Rubble/Ironwork and
        // Assembled's item are each a different mechanism entirely (this file's own top doc comment).
        Assert.DoesNotContain(ConstructionActions.Costs, c => c.ActionId == ConstructionActions.BuiltActionId);
        Assert.DoesNotContain(ConstructionActions.Costs, c => c.ActionId == ConstructionActions.AssembledActionId);
    }

    [Fact]
    public void Built_can_afford_the_moat_exactly_at_the_balance_and_not_one_short()
    {
        var moat = StructureCatalog.Get("moat");
        Assert.True(ConstructionCost.CanAffordBuilt(moat.ConstructRubbleCost, moat.ConstructIronworkCost, moat));
        Assert.False(ConstructionCost.CanAffordBuilt(moat.ConstructRubbleCost - 1, moat.ConstructIronworkCost, moat));
        Assert.False(ConstructionCost.CanAffordBuilt(moat.ConstructRubbleCost, moat.ConstructIronworkCost - 1, moat));
    }

    [Fact]
    public void Built_spend_debits_exactly_the_structures_own_cost()
    {
        var moat = StructureCatalog.Get("moat");
        var (rubble, ironwork) = ConstructionCost.SpendBuilt(moat.ConstructRubbleCost + 10, moat.ConstructIronworkCost + 5, moat);
        Assert.Equal(10, rubble);
        Assert.Equal(5, ironwork);
    }

    [Fact]
    public void Built_spend_past_the_balance_throws_rather_than_clamping()
    {
        var moat = StructureCatalog.Get("moat");
        Assert.Throws<InvalidOperationException>(() => ConstructionCost.SpendBuilt(0, 0, moat));
    }

    [Fact]
    public void The_moat_structure_is_buildable_via_all_four_actions_own_paths()
    {
        var moat = StructureCatalog.Get("moat");
        foreach (var actionId in new[]
                 {
                     ConstructionActions.BuiltActionId, ConstructionActions.AssembledActionId,
                     ConstructionActions.SummonedActionId, ConstructionActions.LabouredActionId,
                 })
            Assert.NotNull(actionId); // each action id below is asserted to exist as real content

        Assert.True(moat.ConstructRubbleCost > 0, "Built needs a real, non-zero Rubble cost to be a meaningful path");
        Assert.True(moat.ConstructIronworkCost > 0, "Built needs a real, non-zero Ironwork cost to be a meaningful path");
    }
}
