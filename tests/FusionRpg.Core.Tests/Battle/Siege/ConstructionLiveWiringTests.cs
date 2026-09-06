using System.Collections.Generic;
using System.Linq;
using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Rungs;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.Battle.Siege;
using FusionRpg.Core.World;
using Xunit;

namespace FusionRpg.Core.Tests.Battle.Siege;

/// <summary>
/// base-defense `siege-construction`/`siege-ai` (2026-09-07, session 5, owner-authorized default
/// policy): the FIRST live wiring of `ConstructionAi.ChooseBuiltSite` into a real
/// `BattleEngine.Resolve` round — proves `BasicAttack.TryDeclareBuilt`'s own new branch end to end,
/// through the REAL atomic action-phase loop (`Siege`/`Delve` both use it; `ClassicRound`/`GalaxySync`/
/// `HybridAtb` do not, confirmed directly against `BattleModeProfile.cs`), not a bare
/// `BattleEffectHost` shortcut the way `ConstructionActionsTests.cs` proves the firing mechanism
/// itself.
///
/// <para><b>Why the enemy can exist without ever becoming a false combat target</b>: `Built`'s own
/// `ActionRow` never overrides `MinRange`/`MaxRange` (both default to 0 — confirmed directly,
/// `ActionRow.cs`), and "with no board, every range check passes" is the ONLY case where that would
/// matter — every test here supplies a REAL board with real, distinct positions, so `Built` is
/// structurally never "in range" of another actor's cell. `StubIntentSource` therefore always reaches
/// its own step 5 (pass) for an actor holding only `Built`, returning `ActionIntent.None` — the exact
/// case `TryDeclareBuilt` exists to intercept before `Break`'s own hazard-3 round-ending.</para>
/// </summary>
public class ConstructionLiveWiringTests
{
    /// <summary>
    /// A real `ActionCatalog` carrying only `Built` — resolved through `ActionCompiler.Compile`
    /// (the real pipeline, not a hand-built `CompiledAction`), matching `ConstructionActions.Actions`'
    /// own real content. `RungTable(cap: 0, ...)` matches `Built`'s own `ActionRow.Rung` default (0,
    /// never overridden by `ConstructionActions.cs`), and `Built`'s own atom-carrying container is
    /// resolved via the SAME `ConstructionActions.ContainerResolver` these tests already pass to
    /// `BattleEngine.Resolve` for the runtime grant.
    /// </summary>
    static ActionCatalog BuiltOnlyCatalog()
    {
        var builtRow = ConstructionActions.Actions.Single(a => a.ActionId == ConstructionActions.BuiltActionId) with { Rung = 1 };
        var containerAtomIds = ConstructionActions.ContainerResolver.EffectIdsFor(ConstructionActions.BuiltContainerId);
        var rungTable = new RungTable(cap: 1, new[] { new RungRow(1, 1, 1, 1, 1000, 1000, 1000, Array.Empty<string>()) });
        var (rejection, compiled) = ActionCompiler.Compile(
            builtRow, Array.Empty<ActionCostRow>(), Array.Empty<ActionScopeRow>(),
            containerAtomIds, boardAvailable: true, rungTable);
        Assert.True(rejection.IsOk, rejection.ToString());
        return ActionCatalog.Build(new[] { compiled! });
    }

    static BattleActorSetup Builder(string key) => new()
    {
        Key = key, Side = "squad", MaxHp = 1000,
        EquippedActionIds = new[] { ConstructionActions.BuiltActionId },
    };

    static BattleActorSetup Enemy(string key) => new() { Key = key, Side = "wave", MaxHp = 1000 };

    static (BoardState Board, ConstructionBoardContext Ctx) Board(long sectorRubble, long sectorIronwork)
    {
        var spec = new GridSpec(20, 20);
        var board = new BoardState(spec);
        board.Place("squad:0", new GridPos(10, 15));
        board.Place("wave:0", new GridPos(10, 16)); // adjacent to the builder, but NOT to any build cell below
        var moat = StructureCatalog.Get("moat");
        var slotByCell = new Dictionary<GridPos, (int SlotIndex, SlotKind Kind)>
        {
            [new GridPos(9, 15)] = (0, moat.RequiredSlotKind), // the FIRST compass offset from (10,15)
        };
        var ctx = new ConstructionBoardContext(
            board, boardSide: 20, coreSideMilli: 400, rampartThickness: 1, slotByCell,
            sectorRubble, sectorIronwork);
        return (board, ctx);
    }

    [Fact]
    public void An_actor_holding_Built_places_a_structure_when_combat_finds_no_target()
    {
        var moat = StructureCatalog.Get("moat");
        var (board, ctx) = Board(moat.ConstructRubbleCost, moat.ConstructIronworkCost);

        var setup = new BattleSetup { Squad = new[] { Builder("squad:0") }, Wave = new[] { Enemy("wave:0") } };
        var report = FusionRpg.Core.Battle.BattleEngine.Resolve(
            setup, seed: 1,
            onEffectHostReady: host =>
            {
                var catalog = (FusionRpg.Core.Effects.InMemoryEffectCatalog)host.Bag.Catalog;
                foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
                host.ConstructionBoard = ctx;
            },
            containerResolver: ConstructionActions.ContainerResolver,
            actionCatalog: BuiltOnlyCatalog(),
            board: board);

        Assert.NotNull(report);
        Assert.True(ctx.Placed.Count > 0,
            $"expected 1 placement, got {ctx.Placed.Count}. Report={System.Text.Json.JsonSerializer.Serialize(report)}");
        var placed = Assert.Single(ctx.Placed);
        Assert.Equal("moat", placed.StructureId);
        Assert.Equal(0, ctx.RemainingRubble); // debited exactly the moat's own cost
        Assert.Equal(0, ctx.RemainingIronwork);
    }

    [Fact]
    public void An_actor_holding_Built_falls_through_to_the_existing_break_when_nothing_is_affordable()
    {
        var (board, ctx) = Board(sectorRubble: 0, sectorIronwork: 0);

        var setup = new BattleSetup { Squad = new[] { Builder("squad:0") }, Wave = new[] { Enemy("wave:0") } };
        var report = FusionRpg.Core.Battle.BattleEngine.Resolve(
            setup, seed: 1,
            onEffectHostReady: host =>
            {
                var catalog = (FusionRpg.Core.Effects.InMemoryEffectCatalog)host.Bag.Catalog;
                foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
                host.ConstructionBoard = ctx;
            },
            containerResolver: ConstructionActions.ContainerResolver,
            actionCatalog: BuiltOnlyCatalog(),
            board: board);

        Assert.NotNull(report); // the round-break path still resolves cleanly, exactly as before this task
        Assert.Empty(ctx.Placed);
    }

    [Fact]
    public void A_battle_with_no_construction_board_wired_is_unaffected()
    {
        // The SAME builder setup, but onEffectHostReady never sets ConstructionBoard -- byte-identical
        // to every battle before this task existed. Proves TryDeclareBuilt's own null-check, not just
        // asserts it by reading the code.
        var spec = new GridSpec(20, 20);
        var board = new BoardState(spec);
        board.Place("squad:0", new GridPos(10, 15));
        board.Place("wave:0", new GridPos(10, 16));

        var setup = new BattleSetup { Squad = new[] { Builder("squad:0") }, Wave = new[] { Enemy("wave:0") } };
        var report = FusionRpg.Core.Battle.BattleEngine.Resolve(
            setup, seed: 1,
            onEffectHostReady: host =>
            {
                var catalog = (FusionRpg.Core.Effects.InMemoryEffectCatalog)host.Bag.Catalog;
                foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
                // ConstructionBoard deliberately never set.
            },
            containerResolver: ConstructionActions.ContainerResolver,
            actionCatalog: BuiltOnlyCatalog(),
            board: board);

        Assert.NotNull(report); // no exception, no crash -- the pre-existing Break path runs unchanged
    }

    [Fact]
    public void A_besieging_legion_can_afford_more_than_one_structure()
    {
        // 15.3b's own headline acceptance test, finally drivable now that a live intent source
        // exists: TWO builders, each with their own legal adjacent cell, and enough Rubble/Ironwork
        // in the SHARED battle budget for both moats -- proves the budget is a real, shared, battle-
        // scoped pool (ConstructionBoardContext), not per-actor.
        var moat = StructureCatalog.Get("moat");
        var spec = new GridSpec(20, 20);
        var board = new BoardState(spec);
        board.Place("squad:0", new GridPos(10, 15));
        board.Place("squad:1", new GridPos(10, 3)); // far from squad:0 -- independent build sites
        board.Place("wave:0", new GridPos(10, 16));
        var slotByCell = new Dictionary<GridPos, (int SlotIndex, SlotKind Kind)>
        {
            [new GridPos(9, 15)] = (0, moat.RequiredSlotKind),
            [new GridPos(9, 3)] = (1, moat.RequiredSlotKind),
        };
        var ctx = new ConstructionBoardContext(
            board, boardSide: 20, coreSideMilli: 400, rampartThickness: 1, slotByCell,
            sectorRubble: checked(moat.ConstructRubbleCost * 2), sectorIronwork: checked(moat.ConstructIronworkCost * 2));

        var setup = new BattleSetup
        {
            Squad = new[] { Builder("squad:0"), Builder("squad:1") },
            Wave = new[] { Enemy("wave:0") },
        };
        var report = FusionRpg.Core.Battle.BattleEngine.Resolve(
            setup, seed: 1,
            onEffectHostReady: host =>
            {
                var catalog = (FusionRpg.Core.Effects.InMemoryEffectCatalog)host.Bag.Catalog;
                foreach (var def in ConstructionActions.CompiledEffects) catalog.Upsert(def);
                host.ConstructionBoard = ctx;
            },
            containerResolver: ConstructionActions.ContainerResolver,
            actionCatalog: BuiltOnlyCatalog(),
            board: board);

        Assert.NotNull(report);
        Assert.Equal(2, ctx.Placed.Count);
        Assert.All(ctx.Placed, p => Assert.Equal("moat", p.StructureId));
        Assert.Equal(0, ctx.RemainingRubble);
        Assert.Equal(0, ctx.RemainingIronwork);
    }
}
