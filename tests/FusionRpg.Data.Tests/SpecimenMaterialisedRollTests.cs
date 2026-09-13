using FusionRpg.Contracts;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// WAVE F2.2 (creature-standalone, 2026-09-07, `creature-mechanism-gaps-ideal.md` §3.4): what a sacrificed
/// specimen actually rolled, for fusion inheritance. `spec-creature-fusion.md`'s own gap, closed here:
/// <c>RpgStore.GetSpecimenMaterialisedRoll</c> resolves a specimen's own player + species to its real
/// `player_species`-backed <c>effect_instance</c> row — the SAME roll <see cref="PlayerMaterialiseTests"/>
/// already proves is durable, read back through the specimen that carries that species, never a second,
/// invented read path.
/// </summary>
public class SpecimenMaterialisedRollTests : IDisposable
{
    readonly DataTestStore _testStore;
    readonly RpgStore _store;

    public SpecimenMaterialisedRollTests()
    {
        _testStore = DataTestStore.Create();
        _store = _testStore.Store;
    }

    public void Dispose()
    {
        _testStore.Dispose();
    }

    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680,
        1000, 25000, 250, 1000, 5000, 5000, 25000);
    const int PinTheta = 20;

    void SeedSpecies(string speciesId, int amount)
    {
        var atomId = $"atom.{speciesId}-vitality.t1";
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = atomId, KindId = "stat.modify", FamilyId = $"atom.{speciesId}-vitality", Tier = 1,
            Name = $"{speciesId} Vitality", ParamsJson = $$"""{"channel":"maxHp","op":"flat","amount":{{amount}}}""",
        }).IsOk);

        Assert.True(_store.UpsertContainer(new ContainerRow
        {
            ContainerId = $"species-passive.{speciesId}", Kind = ContainerKind.SpeciesPassive,
            Atoms = new[] { new ContainerAtomRow(1, atomId) },
        }).IsOk);
    }

    (long PlayerId, string SpecimenId) MintSpecimen(string speciesId, int typeId)
    {
        var player = _store.CreatePlayer("Owner");
        var (specimen, _) = _store.MintCreature(player.Id, new CreatureMintSpec
        {
            SpeciesId = speciesId, Side = "plant", GameTypeId = typeId, Rarity = "sprout",
            Variant = "normal", ElementPrimary = "earth", TraitIds = new List<string>(), Origin = "test",
        });
        return (player.Id, specimen.Actor.InstanceId);
    }

    [Fact]
    public void A_real_specimens_own_instanceId_resolves_to_its_own_real_rolled_atoms()
    {
        // A real, catalog-known species — MintCreature validates against CreatureSpeciesCatalog, unlike
        // SpeciesMaterialiser's own pure path (PlayerMaterialiseTests's synthetic ids don't apply here).
        SeedSpecies("peashooter", 42);
        var (playerId, specimenId) = MintSpecimen("peashooter", 0);
        var outcome = _store.MaterialisePlayerSpecies(playerId, PinTheta, Tuning);
        Assert.True(outcome.IsOk, outcome.Rejection.ToString());

        var roll = _store.GetSpecimenMaterialisedRoll(specimenId);

        Assert.NotNull(roll);
        Assert.Equal("peashooter", roll!.SpeciesId);
        var expected = _store.GetInstance(_store.ListPlayerSpecies(playerId).Single().InstanceId)!;
        Assert.Equal(expected.ContentFingerprint(), roll.Instance.ContentFingerprint());
        // Not the species' generic pool — a real, already-materialised roll's own atoms.
        Assert.Contains(roll.Instance.Atoms, a => a.AtomId == "atom.peashooter-vitality.t1");
    }

    [Fact]
    public void A_specimen_whose_species_has_never_been_materialised_returns_null_not_a_crash()
    {
        SeedSpecies("peashooter", 10);
        var (_, specimenId) = MintSpecimen("peashooter", 0);
        // Deliberately never call MaterialisePlayerSpecies for this player.

        var roll = _store.GetSpecimenMaterialisedRoll(specimenId);

        Assert.Null(roll);
    }

    [Fact]
    public void An_unknown_instanceId_returns_null_not_a_crash()
    {
        var roll = _store.GetSpecimenMaterialisedRoll("not-a-real-instance-id");

        Assert.Null(roll);
    }

    [Fact]
    public void A_bare_unique_actor_with_no_creature_profile_returns_null_not_a_crash()
    {
        var player = _store.CreatePlayer("Owner");
        var actor = _store.CreateUniqueActor(player.Id, "plant", typeId: 1);

        var roll = _store.GetSpecimenMaterialisedRoll(actor.InstanceId);

        Assert.Null(roll);
    }
}
