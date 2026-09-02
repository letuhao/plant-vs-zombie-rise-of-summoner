using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// T4.6 (`species-import`, `RpgStore.Species.cs`) — the DAL half: one bad row writes nothing; a
/// no-op reimport touches nothing; a species absent from the incoming roster is deleted.
/// `DemonSpeciesImportTests`/manual runs cover the CLI's own "stale generated tree refuses" pre-flight
/// (a `Program.cs`-level check this store's own `ImportSpecies` has no reason to duplicate).
/// </summary>
public class SpeciesImportStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public SpeciesImportStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-species-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static ConcreteSpecies Species(string id, long power = 362, DemonRarity rarity = DemonRarity.Cultivated) => new()
    {
        SpeciesId = id, Rarity = rarity, Theta = 13, PTheta = 452,
        AttackIntervalMs = 1500, AttackIntervalSource = "classified", RangeCells = 5, VariantCount = 2,
        Magnitudes = new Dictionary<string, long> { ["combat.power.omni"] = power, ["resource.max.hp"] = 2712 },
        Side = "plant", GameTypeId = 0, ElementPrimary = ElementTypeId.Earth,
        ElementSecondary = ElementTypeId.Fire, DeployMode = DemonDeployMode.PlantAvatar,
        Acquisition = DemonAcquisition.Summonable | DemonAcquisition.EventOnly,
        Variants = new[] { "normal", "mutated" }, TraitPool = new[] { "Projectile-launching", "Defensive" },
    };

    [Fact]
    public void Every_catalog_runtime_pass_through_field_round_trips_through_sqlite()
    {
        var outcome = _store.ImportSpecies(new[] { Species("Peashooter") });
        Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

        var back = _store.GetSpecies("Peashooter")!;

        Assert.Equal("plant", back.Side);
        Assert.Equal(0, back.GameTypeId);
        Assert.Equal(ElementTypeId.Earth, back.ElementPrimary);
        Assert.Equal(ElementTypeId.Fire, back.ElementSecondary);
        Assert.Equal(DemonDeployMode.PlantAvatar, back.DeployMode);
        Assert.Equal(DemonAcquisition.Summonable | DemonAcquisition.EventOnly, back.Acquisition);
        Assert.Equal(new[] { "normal", "mutated" }, back.Variants);
        Assert.Equal(new[] { "Projectile-launching", "Defensive" }, back.TraitPool);
    }

    [Fact]
    public void A_species_with_no_almanac_row_gets_the_generic_name_fallback_not_a_null()
    {
        // No almanac_seed data exists in a fresh test database — proves the LAST link of the
        // DisplayName ?? TypeName ?? "Demon {gameTypeId}" fallback chain, mirroring the pre-atom-
        // layer generator's own precedent (DemonSpeciesGenerator.cs:69).
        _store.ImportSpecies(new[] { Species("Peashooter") with { GameTypeId = 42 } });

        var back = _store.GetSpecies("Peashooter")!;

        Assert.Equal("Demon 42", back.Name);
    }

    [Fact]
    public void A_null_elementSecondary_round_trips_as_null_not_a_sentinel_string()
    {
        _store.ImportSpecies(new[] { Species("Peashooter") with { ElementSecondary = null } });

        Assert.Null(_store.GetSpecies("Peashooter")!.ElementSecondary);
    }

    [Fact]
    public void Reimporting_with_a_changed_pass_through_field_is_not_treated_as_unchanged()
    {
        _store.ImportSpecies(new[] { Species("Peashooter") });
        var outcome = _store.ImportSpecies(new[]
        {
            Species("Peashooter") with { DeployMode = DemonDeployMode.HypnoAlly },
        });

        Assert.Equal(1, outcome.Written);
        Assert.Equal(0, outcome.Unchanged);
        Assert.Equal(DemonDeployMode.HypnoAlly, _store.GetSpecies("Peashooter")!.DeployMode);
    }

    [Fact]
    public void A_clean_roster_writes_every_species_and_its_magnitudes()
    {
        var outcome = _store.ImportSpecies(new[] { Species("Peashooter"), Species("SunFlower") });

        Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));
        Assert.Equal(2, outcome.Written);
        Assert.Equal(0, outcome.Unchanged);

        var back = _store.GetSpecies("Peashooter");
        Assert.NotNull(back);
        Assert.Equal(DemonRarity.Cultivated, back!.Rarity);
        Assert.Equal(452, back.PTheta);
        Assert.Equal(362, back.Magnitudes["combat.power.omni"]);
        Assert.Equal(2712, back.Magnitudes["resource.max.hp"]);
    }

    [Fact]
    public void A_duplicate_speciesId_writes_nothing()
    {
        var outcome = _store.ImportSpecies(new[] { Species("Peashooter"), Species("Peashooter", power: 999) });

        Assert.False(outcome.IsOk);
        Assert.Null(_store.GetSpecies("Peashooter"));
    }

    [Fact]
    public void The_refusal_names_the_first_failure_and_the_total_count()
    {
        var outcome = _store.ImportSpecies(new[]
        {
            Species("Peashooter"), Species("Peashooter"), Species("SunFlower"), Species("SunFlower"),
        });

        Assert.False(outcome.IsOk);
        Assert.Equal(2, outcome.Errors.Count); // one duplicate flagged per repeat, not per pair
        Assert.Equal("Peashooter", outcome.Errors[0].SpeciesId); // the FIRST failure, in encounter order
    }

    [Fact]
    public void An_empty_speciesId_is_refused()
    {
        var outcome = _store.ImportSpecies(new[] { Species("") });

        Assert.False(outcome.IsOk);
        Assert.Contains(outcome.Errors, e => e.Detail.Contains("empty"));
    }

    [Fact]
    public void Reimporting_the_same_roster_is_row_identical()
    {
        _store.ImportSpecies(new[] { Species("Peashooter"), Species("SunFlower") });

        var second = _store.ImportSpecies(new[] { Species("Peashooter"), Species("SunFlower") });

        Assert.True(second.IsOk);
        Assert.Equal(0, second.Written);
        Assert.Equal(2, second.Unchanged);
    }

    [Fact]
    public void A_real_value_change_is_written_not_skipped()
    {
        _store.ImportSpecies(new[] { Species("Peashooter") });

        var outcome = _store.ImportSpecies(new[] { Species("Peashooter", power: 500) });

        Assert.Equal(1, outcome.Written);
        Assert.Equal(500, _store.GetSpecies("Peashooter")!.Magnitudes["combat.power.omni"]);
    }

    [Fact]
    public void A_species_absent_from_the_incoming_roster_is_deleted()
    {
        _store.ImportSpecies(new[] { Species("Peashooter"), Species("SunFlower") });

        var outcome = _store.ImportSpecies(new[] { Species("Peashooter") }); // SunFlower dropped upstream

        Assert.Equal(1, outcome.Deleted);
        Assert.Null(_store.GetSpecies("SunFlower"));
        Assert.NotNull(_store.GetSpecies("Peashooter"));
    }

    [Fact]
    public void Deleting_a_species_also_deletes_its_magnitude_rows()
    {
        _store.ImportSpecies(new[] { Species("Peashooter") });
        _store.ImportSpecies(Array.Empty<ConcreteSpecies>());

        // Re-adding the same id afterward must not resurrect the old magnitude rows under a stale key.
        var outcome = _store.ImportSpecies(new[] { Species("Peashooter", power: 1) });

        Assert.Equal(1, _store.GetSpecies("Peashooter")!.Magnitudes["combat.power.omni"]);
    }

    [Fact]
    public void Every_species_id_lists_in_order()
    {
        _store.ImportSpecies(new[] { Species("SunFlower"), Species("Peashooter") });

        Assert.Equal(new[] { "Peashooter", "SunFlower" }, _store.ListSpeciesIds());
    }

    [Fact]
    public void An_unknown_species_reads_as_null()
    {
        Assert.Null(_store.GetSpecies("nope"));
    }
}
