using FusionRpg.Core.Actions;
using FusionRpg.Core.Battle;
using FusionRpg.Core.Battle.Board;
using FusionRpg.Core.World;
using FusionRpg.Core.World.District;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World.Turn;

/// <summary>
/// base-defense `siege-resolver` (module 15, spec-siege-resolver.md): the `IBattleResolver`
/// implementation for `BattleKinds.District` — the world/battle join. See
/// `DistrictAssaultResolver.cs`'s own top comment for the two named, deliberate simplifications this
/// pass makes (a structure's fixed battle side; `InCore` approximated as "alive" since `BattleReport`
/// carries no final position data for any battle kind).
/// </summary>
public class DistrictAssaultResolverTests
{
    static WorldEntity Legion(string id, string owner, string sectorId, params (string Species, int Level, long Hp)[] members) => new()
    {
        EntityId = id,
        Kind = WorldEntityKind.Warband,
        OwnerFactionId = owner,
        AtSectorId = sectorId,
        Members = members.Select(m => new WorldEntityMember { SpeciesId = m.Species, Level = m.Level, Hp = m.Hp }).ToList(),
    };

    static BattleRequest DistrictRequest(string battleId, string attackerId, string? defenderId, BoardProjection? board, int slotCount = 0) => new()
    {
        BattleId = battleId,
        Kind = BattleKinds.District,
        LocationId = "s1",
        AttackerEntityId = attackerId,
        DefenderEntityId = defenderId,
        DefenderStationary = defenderId is not null,
        Board = board,
    };

    static BoardProjection Board(ulong worldSeed = 42, IReadOnlyList<SlotProjection>? slots = null) => new()
    {
        SectorId = "s1",
        WorldSeed = worldSeed,
        SectorTypeId = "home",
        DevelopmentLevel = 0,
        AttackerEdge = FusionRpg.Core.World.District.BoardEdge.North,
        Slots = slots ?? Array.Empty<SlotProjection>(),
    };

    [Fact]
    public void Non_district_kinds_delegate_to_the_placeholder_unchanged()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var defender = Legion("e-d", "zomboss", "s1", ("normalzombie", 1, 100));
        var request = new BattleRequest
        {
            BattleId = "b1", Kind = BattleKinds.Sector, LocationId = "s1",
            AttackerEntityId = attacker.EntityId, DefenderEntityId = defender.EntityId, DefenderStationary = true,
        };
        var combatants = new[] { attacker, defender };

        var fromResolver = DistrictAssaultResolver.Instance.Resolve(request, combatants, 1);
        var fromPlaceholder = PlaceholderBattleResolver.Instance.Resolve(request, combatants, 1);

        Assert.Equal(fromPlaceholder.WinnerEntityId, fromResolver.WinnerEntityId);
        Assert.Equal(fromPlaceholder.Sides.Count, fromResolver.Sides.Count);
    }

    [Fact]
    public void District_kind_with_no_board_delegates_to_the_placeholder_unchanged()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var request = DistrictRequest("b1", attacker.EntityId, null, board: null);
        var combatants = new[] { attacker };

        var fromResolver = DistrictAssaultResolver.Instance.Resolve(request, combatants, 1);
        var fromPlaceholder = PlaceholderBattleResolver.Instance.Resolve(request, combatants, 1);

        Assert.Equal(fromPlaceholder.WinnerEntityId, fromResolver.WinnerEntityId);
    }

    [Fact]
    public void Resolver_is_constructible_from_statics_only()
    {
        // The whole point of `Instance` -- no constructor parameter, no live service.
        var resolver = new DistrictAssaultResolver();
        Assert.NotNull(resolver);
        Assert.NotNull(DistrictAssaultResolver.Instance);
    }

    [Fact]
    public void An_unopposed_assault_resolves_as_core_taken_with_no_battle_engine_call()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var request = DistrictRequest("b1", attacker.EntityId, null, Board());

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker }, seed: 7);

        Assert.Equal(attacker.EntityId, outcome.WinnerEntityId);
        var side = Assert.Single(outcome.Sides);
        Assert.Equal(attacker.EntityId, side.EntityId);
        Assert.False(side.Destroyed);
        Assert.NotEmpty(side.Survivors);
    }

    [Fact]
    public void A_real_fight_produces_two_sides_and_a_version_stamp()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 3, 300), ("conezombie", 3, 300));
        var defender = Legion("e-d", "zomboss", "s1", ("normalzombie", 3, 300));
        var request = DistrictRequest("b1", attacker.EntityId, defender.EntityId, Board());

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker, defender }, seed: 123);

        Assert.Equal(2, outcome.Sides.Count);
        Assert.Contains(outcome.Sides, s => s.EntityId == attacker.EntityId);
        Assert.Contains(outcome.Sides, s => s.EntityId == defender.EntityId);
        Assert.True(outcome.EngineVersion > 0);
        Assert.True(outcome.RulesetVersion > 0);
        Assert.NotNull(outcome.Seed);
        // Nobody is duplicated or invented -- survivor counts never exceed the roster they came from.
        foreach (var side in outcome.Sides)
        {
            var original = side.EntityId == attacker.EntityId ? attacker : defender;
            Assert.True(side.Survivors.Count <= original.Members.Count);
        }
    }

    [Fact]
    public void Same_seed_same_siege_10000_times()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 3, 300));
        var defender = Legion("e-d", "zomboss", "s1", ("normalzombie", 3, 300));
        var request = DistrictRequest("b1", attacker.EntityId, defender.EntityId, Board());
        var combatants = new[] { attacker, defender };

        var first = DistrictAssaultResolver.Instance.Resolve(request, combatants, seed: 999);
        for (var i = 0; i < 10_000; i++)
        {
            var repeat = DistrictAssaultResolver.Instance.Resolve(request, combatants, seed: 999);
            Assert.Equal(first.WinnerEntityId, repeat.WinnerEntityId);
            Assert.Equal(first.Sides.Select(s => s.Survivors.Count), repeat.Sides.Select(s => s.Survivors.Count));
        }
    }

    [Fact]
    public void Two_assaults_in_one_turn_get_different_seeds()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 3, 300));
        var defender = Legion("e-d", "zomboss", "s1", ("normalzombie", 3, 300));
        var combatants = new[] { attacker, defender };

        var requestOne = DistrictRequest("t1:district:s1:e-a|e-d", attacker.EntityId, defender.EntityId, Board());
        var requestTwo = DistrictRequest("t1:district:s2:e-a|e-d", attacker.EntityId, defender.EntityId, Board());

        var outcomeOne = DistrictAssaultResolver.Instance.Resolve(requestOne, combatants, seed: 5000);
        var outcomeTwo = DistrictAssaultResolver.Instance.Resolve(requestTwo, combatants, seed: 5000);

        Assert.NotEqual(outcomeOne.Seed, outcomeTwo.Seed);
    }

    [Fact]
    public void Structure_hp_survives_the_round_trip_as_long()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var slots = new[]
        {
            new SlotProjection { SlotIndex = 0, SlotTypeId = "rootbed", StructureId = "well", StructureHp = 4_000_000_000L },
        };
        var request = DistrictRequest("b1", attacker.EntityId, null, Board(slots: slots));

        // Unopposed (no defender entity) but a structure stands on the board -- still fights (the
        // structure is on the defender side), so this exercises the BattleEngine.Resolve path with a
        // real, large long HP value end to end.
        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker }, seed: 1);

        Assert.NotNull(outcome);
        // The structure itself is not a WorldEntity, so it never appears in outcome.Sides -- but it
        // MUST appear in SlotResults (base-defense siege-construction: found and fixed a real,
        // previously-unwired gap where this seam existed and was tested in isolation but nothing ever
        // populated it from a real fight). A single level-1 peashooterzombie cannot dent 4 billion HP.
        var slotResult = Assert.Single(outcome.SlotResults);
        Assert.Equal(0, slotResult.SlotIndex);
        Assert.False(slotResult.StructureDestroyed);
        Assert.True(slotResult.StructureHp > int.MaxValue, "structure HP must survive the round trip as a real long, not narrow to int");
    }

    /// <summary>
    /// `BuildSlotResults` tested directly against a synthetic `resultByKey`, the same "test the seam in
    /// isolation" discipline `StructureStateTests`/`BattleSeamWideningTests` already use for
    /// `ApplySlotResults` itself — deliberately NOT routed through a full `BattleEngine.Resolve` call.
    /// Found while writing this fix: an approach-zone attacker with no equipped movement action cannot
    /// reliably reach a core-zone structure within the siege profile's own round/tick budget in this
    /// synthetic test harness, so a real fight landing zero damage on a structure proves nothing about
    /// THIS translation step either way — a separate, pre-existing question (whether the shipped default
    /// action loadout and approach-to-core distance ever let a real attacker close to melee range) that
    /// belongs to `siege-pathing`/`siege-resolver`, not to this seam fix.
    /// </summary>
    [Fact]
    public void Build_slot_results_reports_an_existing_structures_damage_and_destruction()
    {
        var board = Board(slots: new[]
        {
            new SlotProjection { SlotIndex = 2, SlotTypeId = "wildland", StructureId = "granary", OwnerFactionId = "zomboss" },
        });
        var resultByKey = new Dictionary<string, BattleActorResult>(StringComparer.Ordinal)
        {
            ["slot:2"] = new("slot:2", "wave", "granary", 0, HpRemaining: 0, DamageDealt: 0, Kills: 0, Survived: false, Retreated: false, XpMilli: 0),
        };

        var results = DistrictAssaultResolver.BuildSlotResults(board, resultByKey);

        var slotResult = Assert.Single(results);
        Assert.Equal(2, slotResult.SlotIndex);
        Assert.True(slotResult.StructureDestroyed);
        Assert.Equal(0, slotResult.StructureHp);
        Assert.Equal("zomboss", slotResult.HeldByFactionId); // preserved, not recomputed from capture
    }

    [Fact]
    public void Build_slot_results_skips_a_slot_with_no_structure_or_no_battle_engine_result()
    {
        var board = Board(slots: new[]
        {
            new SlotProjection { SlotIndex = 0, SlotTypeId = "wildland", StructureId = null },
            new SlotProjection { SlotIndex = 1, SlotTypeId = "rootbed", StructureId = "well" }, // never fielded -- no "slot:1" entry
        });

        var results = DistrictAssaultResolver.BuildSlotResults(board, new Dictionary<string, BattleActorResult>(StringComparer.Ordinal));

        Assert.Empty(results);
    }

    [Fact]
    public void Build_slot_results_reports_a_structure_placed_this_battle_finished_immediately()
    {
        var board = Board(); // empty -- the slot did not exist before this battle
        var placed = new[] { new StructurePlacementRecord(SlotIndex: 3, StructureId: "granary", Instant: true) };

        var results = DistrictAssaultResolver.BuildSlotResults(
            board, new Dictionary<string, BattleActorResult>(StringComparer.Ordinal), placed);

        var slotResult = Assert.Single(results);
        Assert.Equal(3, slotResult.SlotIndex);
        Assert.Equal("granary", slotResult.StructurePlaced);
        Assert.Null(slotResult.PlacedConstructionTurnsRemaining); // instant: finished immediately
    }

    [Fact]
    public void Build_slot_results_reports_a_structure_placed_this_battle_still_under_construction()
    {
        // "well" ships with a real, positive BuildTurns (Loam.LoamPolicy.WellBuildTurns) -- instant:
        // false must read it from the catalog, not invent a countdown of its own.
        var board = Board();
        var placed = new[] { new StructurePlacementRecord(SlotIndex: 1, StructureId: "well", Instant: false) };

        var results = DistrictAssaultResolver.BuildSlotResults(
            board, new Dictionary<string, BattleActorResult>(StringComparer.Ordinal), placed);

        var slotResult = Assert.Single(results);
        Assert.Equal("well", slotResult.StructurePlaced);
        Assert.Equal(StructureCatalog.Get("well").BuildTurns, slotResult.PlacedConstructionTurnsRemaining);
        Assert.True(slotResult.PlacedConstructionTurnsRemaining > 0);
    }

    [Fact]
    public void An_existing_structures_ownership_is_preserved_not_recomputed_from_capture()
    {
        // This fix's own named, deliberate scope boundary: HP/destruction persist, but capture-based
        // ownership transfer for an EXISTING structure's slot is a separate, unscoped question --
        // HeldByFactionId passes the projection's own OwnerFactionId straight through, unchanged.
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var slots = new[]
        {
            new SlotProjection
            {
                SlotIndex = 0, SlotTypeId = "rootbed", StructureId = "well",
                StructureHp = 4_000_000_000L, OwnerFactionId = "zomboss",
            },
        };
        var request = DistrictRequest("b1", attacker.EntityId, null, Board(slots: slots));

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker }, seed: 1);

        Assert.Equal("zomboss", Assert.Single(outcome.SlotResults).HeldByFactionId);
    }

    /// <summary>
    /// base-defense `siege-construction`/`siege-ai` (2026-09-07, MAJOR finding this session): before
    /// `BattleActorSetup.AdditionalHeldActions` existed, no legion member built by this resolver could
    /// ever hold a construction action at all. Proving the FULL "a real siege actually builds a
    /// structure" chain also needs the attacker positioned adjacent to an affordable, legal cell —
    /// `ConstructionAi.ChooseBuiltSite`'s own 8-cell search — which this resolver's own
    /// zone-based `Placement.PlaceActors` does not guarantee relative to an arbitrary slot's cell
    /// (confirmed empirically: an earlier version of this test asserted a built `moat` and failed with
    /// neither combat damage nor a placement, meaning the attacker's real placement here simply was not
    /// adjacent to the target cell — a real, pre-existing, geometry-only constraint, unrelated to
    /// whether the action is held). `ConstructionLiveWiringTests.cs`'s own
    /// `AdditionalHeldActionsAloneMakeBuiltReachable` proves the actual mechanism this fix adds, under
    /// the same hand-controlled positioning that module's other tests already use. This test instead
    /// proves the narrower, still-real claim: wiring `AdditionalHeldActions` into a genuinely opposed,
    /// unpositioned real battle changes nothing observable when no legal build site is adjacent —
    /// no exception, no stray structure, no regression to the pre-existing "well" outcome.
    /// </summary>
    [Fact]
    public void Granting_construction_actions_does_not_change_an_unrelated_battles_outcome()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        var slots = new[]
        {
            new SlotProjection { SlotIndex = 0, SlotTypeId = "rootbed", StructureId = "well", StructureHp = 4_000_000_000L },
        };
        var board = Board(slots: slots) with { RubbleStock = 10, IronworkStock = 10 };
        var request = DistrictRequest("b1", attacker.EntityId, null, board);

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker }, seed: 1);

        // Same outcome shape `An_existing_structures_ownership_is_preserved...` already established
        // for this exact slot setup -- proving AdditionalHeldActions being wired for the FIRST time
        // introduced no regression to an unrelated battle it has no legal site to act on.
        var slotResult = Assert.Single(outcome.SlotResults);
        Assert.Equal(0, slotResult.SlotIndex);
        Assert.False(slotResult.StructureDestroyed);
    }

    [Fact]
    public void A_slot_with_no_structure_never_appears_in_slot_results()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 3, 300));
        var defender = Legion("e-d", "zomboss", "s1", ("normalzombie", 3, 300));
        var slots = new[] { new SlotProjection { SlotIndex = 0, SlotTypeId = "wildland", StructureId = null } };
        var request = DistrictRequest("b1", attacker.EntityId, defender.EntityId, Board(slots: slots));

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker, defender }, seed: 1);

        Assert.Empty(outcome.SlotResults);
    }

    [Fact]
    public void No_battle_engine_call_when_the_defender_has_no_living_members()
    {
        var attacker = Legion("e-a", "player", "s1", ("peashooterzombie", 1, 100));
        // Defender exists but every member is already past death -- Hp <= Wounds.
        var defender = new WorldEntity
        {
            EntityId = "e-d", Kind = WorldEntityKind.Warband, OwnerFactionId = "zomboss", AtSectorId = "s1",
            Members = new[] { new WorldEntityMember { SpeciesId = "normalzombie", Level = 1, Hp = 100, Wounds = 100 } },
        };
        var request = DistrictRequest("b1", attacker.EntityId, defender.EntityId, Board());

        var outcome = DistrictAssaultResolver.Instance.Resolve(request, new[] { attacker, defender }, seed: 1);

        Assert.Equal(attacker.EntityId, outcome.WinnerEntityId);
    }
}
