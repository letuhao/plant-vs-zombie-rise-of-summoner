using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>zomboss-deploy-ai T3.4 — the store-side half of the deploy wiring: a dedicated, idempotent
/// Zomboss player row, and minting a fresh specimen under it via the real `MintCreature` primitive.</summary>
public class ZombossDeployStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ZombossDeployStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-zomboss-deploy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    static readonly FusionRpg.Core.Creatures.CreatureSpeciesDef CatalogSpecies =
        FusionRpg.Core.Creatures.CreatureSpeciesCatalog.All.First(s =>
            s.DeployMode != FusionRpg.Core.Creatures.CreatureDeployMode.HypnoAlly);

    [Fact]
    public void EnsureZombossPlayer_creates_a_player_row_named_Zomboss()
    {
        var zomboss = _store.EnsureZombossPlayer();

        Assert.True(zomboss.Id > 0);
        Assert.Equal(RpgStore.ZombossPlayerName, zomboss.Name);
    }

    [Fact]
    public void EnsureZombossPlayer_is_idempotent_second_call_reuses_the_same_row()
    {
        var first = _store.EnsureZombossPlayer();
        var second = _store.EnsureZombossPlayer();

        Assert.Equal(first.Id, second.Id);
    }

    [Fact]
    public void EnsureZombossPlayer_never_collides_with_a_real_human_player()
    {
        var human = _store.CreatePlayer("real-player");
        var zomboss = _store.EnsureZombossPlayer();

        Assert.NotEqual(human.Id, zomboss.Id);
    }

    [Fact]
    public void MintForZomboss_mints_a_specimen_owned_by_the_Zomboss_player_not_the_human()
    {
        var human = _store.CreatePlayer("real-player");

        var specimen = _store.MintForZomboss(CatalogSpecies.SpeciesId, seed: 12345);

        var zomboss = _store.EnsureZombossPlayer();
        Assert.Equal(zomboss.Id, specimen.Actor.PlayerId);
        Assert.NotEqual(human.Id, specimen.Actor.PlayerId);
        Assert.Equal(CatalogSpecies.SpeciesId, specimen.Profile.SpeciesId);
    }

    [Fact]
    public void MintForZomboss_is_deterministic_same_seed_same_traits()
    {
        var traitSpecies = FusionRpg.Core.Creatures.CreatureSpeciesCatalog.All.First(s =>
            s.DeployMode != FusionRpg.Core.Creatures.CreatureDeployMode.HypnoAlly && s.TraitPool.Count > 0);

        var first = _store.MintForZomboss(traitSpecies.SpeciesId, seed: 999);
        var second = _store.MintForZomboss(traitSpecies.SpeciesId, seed: 999);

        Assert.Equal(first.Profile.TraitIds.OrderBy(t => t), second.Profile.TraitIds.OrderBy(t => t));
    }

    [Fact]
    public void MintForZomboss_rejects_an_unknown_species_id()
    {
        Assert.Throws<ArgumentException>(() => _store.MintForZomboss("not-a-real-species-id", seed: 1));
    }

    [Fact]
    public void A_Zomboss_minted_specimen_can_deploy_through_the_real_TryBeginUniqueDeploy_path()
    {
        var specimen = _store.MintForZomboss(CatalogSpecies.SpeciesId, seed: 1);

        var (ok, reason, _, _) = _store.TryBeginUniqueDeploy(
            specimen.Actor.InstanceId, Guid.NewGuid().ToString("N"), matchKey: "test-match");

        Assert.True(ok, reason);
    }
}
