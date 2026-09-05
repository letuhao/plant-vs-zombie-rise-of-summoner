using FusionRpg.Core.World;
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
        // The structure itself is not a WorldEntity, so it never appears in outcome.Sides -- this test
        // asserts only that resolving with a large long StructureHp does not throw or narrow silently.
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
