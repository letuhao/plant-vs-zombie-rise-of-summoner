using System.Linq;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Progression;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>`species-build` T1.4 (module 3, `species-xp`) — the expedition source
/// (spec-species-xp.md §2 "Expedition"), and this program's standalone-first proof: a species can
/// level with the game closed, purely off an expedition battle win, no lawn run anywhere in the test.
/// The lawn source (T1.2/T1.3) lives in `SpeciesProgressionTests.cs`.</summary>
public class SpeciesExpeditionTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public SpeciesExpeditionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-species-exp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    // Same "pick one from the real roster" convention as ExpeditionRewardApplyTests, decoupled from
    // any specific compiled id so a roster edit doesn't rot this test.
    static readonly DemonSpeciesDef CatalogSpecies = DemonSpeciesCatalog.All.First(s => s.Side == "zombie");

    DemonMintSpec Spec() => new()
    {
        SpeciesId = CatalogSpecies.SpeciesId,
        Side = "zombie",
        GameTypeId = CatalogSpecies.GameTypeId,
        Rarity = "chaff",
        Variant = "normal",
        ElementPrimary = "fire",
        TraitIds = new List<string> { "swift" },
        Origin = "expedition"
    };

    [Fact]
    public void Expedition_win_levels_the_species_with_no_lawn_run_at_all()
    {
        // The game-closed proof itself: nothing in this test ever calls AppendPvzActivityFact or
        // InsertEvent — the ONLY progression source touched is the expedition reward path.
        var (specimen, _) = _store.MintDemon(1, Spec());
        var instanceId = specimen.Actor.InstanceId;
        var (_, _, row) = _store.DispatchExpedition(1, "species-exp-1", "scout-30m", new[] { instanceId }, 1);

        var rewards = new RpgStore.ExpeditionRewardApply(
            EventSouls: 0,
            Materials: Array.Empty<(string, long)>(),
            SpecimenXp: new[] { (instanceId, 30L) },
            WildMints: Array.Empty<DemonMintSpec>());

        var applied = _store.ApplyExpeditionRewards(row!.Id, 1, ExpeditionStates.Collected, rewards);
        Assert.True(applied.Applied);

        var species = _store.GetRpgActor(1, RpgActorKinds.Species, CatalogSpecies.DemonTypeId);
        Assert.NotNull(species);
        Assert.Equal(30, species!.Xp); // the same xp the specimen earned, mirrored onto its species
    }

    [Fact]
    public void Species_award_shares_the_specimen_awards_transaction()
    {
        // Mirrors ExpeditionRewardApplyTests' own "Bad_material_in_rewards_applies_nothing": when the
        // WHOLE reward apply throws (bad material id), NEITHER the specimen NOR its species may have
        // gained anything -- proving the species award rides the same transaction, not a separate one.
        var (specimen, _) = _store.MintDemon(1, Spec());
        var instanceId = specimen.Actor.InstanceId;
        var (_, _, row) = _store.DispatchExpedition(1, "species-exp-2", "scout-30m", new[] { instanceId }, 1);

        Assert.ThrowsAny<Exception>(() => _store.ApplyExpeditionRewards(
            row!.Id, 1, ExpeditionStates.Collected,
            new RpgStore.ExpeditionRewardApply(
                EventSouls: 0,
                Materials: new[] { ("not.a.real.material", 1L) },
                SpecimenXp: new[] { (instanceId, 30L) },
                WildMints: Array.Empty<DemonMintSpec>())));

        Assert.Null(_store.GetRpgActor(1, RpgActorKinds.Species, CatalogSpecies.DemonTypeId));
        var specimenXp = _store.ListDemonRoster(1).Items.Single(s => s.Profile.InstanceId == instanceId).Actor.Xp;
        Assert.Equal(0, specimenXp);
    }

    [Fact]
    public void Replayed_collect_never_double_pays_the_species_either()
    {
        // Same exactly-once gate ExpeditionRewardApplyTests proves for souls/specimen xp/materials --
        // this test proves the species award inherits it too, since it rides the same transaction.
        var (specimen, _) = _store.MintDemon(1, Spec());
        var instanceId = specimen.Actor.InstanceId;
        var (_, _, row) = _store.DispatchExpedition(1, "species-exp-3", "scout-30m", new[] { instanceId }, 1);

        var rewards = new RpgStore.ExpeditionRewardApply(
            EventSouls: 0, Materials: Array.Empty<(string, long)>(),
            SpecimenXp: new[] { (instanceId, 30L) }, WildMints: Array.Empty<DemonMintSpec>());

        _store.ApplyExpeditionRewards(row!.Id, 1, ExpeditionStates.Collected, rewards);
        var retry = _store.ApplyExpeditionRewards(row.Id, 1, ExpeditionStates.Collected, rewards);
        Assert.False(retry.Applied);

        var species = _store.GetRpgActor(1, RpgActorKinds.Species, CatalogSpecies.DemonTypeId);
        Assert.Equal(30, species!.Xp); // once, not twice
    }
}
