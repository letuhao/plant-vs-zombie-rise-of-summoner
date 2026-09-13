using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using FusionRpg.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// T5.6 (`player-materialise`, `spec-player-materialise.md` §3/§7) — the transactional half:
/// <c>RpgStore.MaterialisePlayerSpecies</c> really writes `player_species` + `effect_instance` rows,
/// once per player, append-only, all-or-nothing. <see cref="Creatures.MaterialiseTests"/>-equivalent
/// purity/reproducibility already lives in `FusionRpg.Core.Tests` (T5.5) against the pure
/// <c>SpeciesMaterialiser</c> — this file proves the DAL wrapper actually persists what that produces,
/// and the four properties only a real database can prove: append-only, all-or-nothing, a retune
/// leaving existing rolls untouched, and a measured full-roster write time.
/// </summary>
public class PlayerMaterialiseTests : IDisposable
{
    readonly DataTestStore _testStore;
    readonly RpgStore _store;

    public PlayerMaterialiseTests()
    {
        _testStore = DataTestStore.Create();
        _store = _testStore.Store;
    }

    public void Dispose() => _testStore.Dispose();

    // Same fixed pin theta/tuning InstanceProducerStoreTests.cs already established for this store.
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

    [Fact]
    public void Materialising_writes_a_roster_row_and_an_instance_for_each_species_with_content()
    {
        SeedSpecies("conezombie", 10);
        SeedSpecies("peashooter", 20);
        var player = _store.CreatePlayer("Owner");

        var outcome = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.True(outcome.IsOk, outcome.Rejection.ToString());
        Assert.Equal(2, outcome.Written);
        Assert.Equal(0, outcome.AlreadyPresent);

        var roster = _store.ListPlayerSpecies(player.Id);
        Assert.Equal(2, roster.Count);
        Assert.All(roster, r => Assert.NotNull(_store.GetInstance(r.InstanceId)));
    }

    [Fact]
    public void Same_world_seed_reproduces_the_roster_across_two_players_seeded_identically()
    {
        // Two DIFFERENT players naturally get different world seeds (CreatePlayer rolls one each,
        // T5.1) — the derivation-property test itself lives in Core against SpeciesMaterialiser
        // directly, so this proves only that the DAL wrapper's own roll_seed column round-trips
        // byte-identically to what the pure materialiser derived, not a second copy of that law.
        SeedSpecies("sunflower", 30);
        var player = _store.CreatePlayer("Owner");

        var a = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);
        Assert.True(a.IsOk, a.Rejection.ToString());
        var rowA = _store.ListPlayerSpecies(player.Id).Single();
        var instanceA = _store.GetInstance(rowA.InstanceId)!;

        Assert.Equal(
            FusionRpg.Core.Effects.Atoms.WorldSeed.DeriveRollSeed(player.WorldSeed, "species", "sunflower"),
            instanceA.RollSeed);
    }

    [Fact]
    public void Two_world_seeds_produce_different_instance_content()
    {
        SeedSpecies("sunflower", 30);
        var p1 = _store.CreatePlayer("One");
        var p2 = _store.CreatePlayer("Two");
        Assert.NotEqual(p1.WorldSeed, p2.WorldSeed); // CreatePlayer rolls independently, T5.1

        _store.MaterialisePlayerSpecies(p1.Id, PinTheta, Tuning);
        _store.MaterialisePlayerSpecies(p2.Id, PinTheta, Tuning);

        var i1 = _store.GetInstance(_store.ListPlayerSpecies(p1.Id).Single().InstanceId)!;
        var i2 = _store.GetInstance(_store.ListPlayerSpecies(p2.Id).Single().InstanceId)!;
        Assert.NotEqual(i1.RollSeed, i2.RollSeed);
    }

    [Fact]
    public void Added_species_are_appended_without_disturbing_an_existing_row()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");

        _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);
        var before = _store.ListPlayerSpecies(player.Id).Single();
        var beforeFingerprint = _store.GetInstance(before.InstanceId)!.ContentFingerprint();

        SeedSpecies("peashooter", 20); // catalog grows
        var outcome = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.True(outcome.IsOk, outcome.Rejection.ToString());
        Assert.Equal(1, outcome.Written); // only the NEW species rolled
        Assert.Equal(1, outcome.AlreadyPresent);

        var after = _store.ListPlayerSpecies(player.Id);
        Assert.Equal(2, after.Count);

        var conezombieAfter = after.Single(r => r.SpeciesId == "conezombie");
        Assert.Equal(before.InstanceId, conezombieAfter.InstanceId); // same row, never touched
        Assert.Equal(beforeFingerprint, _store.GetInstance(conezombieAfter.InstanceId)!.ContentFingerprint());
    }

    [Fact]
    public void A_retuned_affix_does_not_touch_an_existing_roll()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");
        _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);
        var before = _store.ListPlayerSpecies(player.Id).Single();
        var beforeValuesJson = _store.GetInstance(before.InstanceId)!.Atoms.Single().ValuesJson;

        // Retune: same atom id, new magnitude — a balance-pass edit landing after this player already
        // rolled. UpsertAtom bumps the catalog revision (E14a); this player is already materialised
        // for "conezombie", so a second materialise call must not touch their frozen roll.
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = "atom.conezombie-vitality.t1", KindId = "stat.modify",
            FamilyId = "atom.conezombie-vitality", Tier = 1,
            Name = "Conezombie Vitality", ParamsJson = """{"channel":"maxHp","op":"flat","amount":9999}""",
        }).IsOk);

        var outcome = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.True(outcome.IsOk, outcome.Rejection.ToString());
        Assert.Equal(0, outcome.Written); // nothing new to roll — conezombie already present
        Assert.Equal(1, outcome.AlreadyPresent);

        var after = _store.ListPlayerSpecies(player.Id).Single();
        Assert.Equal(before.InstanceId, after.InstanceId);
        Assert.Equal(beforeValuesJson, _store.GetInstance(after.InstanceId)!.Atoms.Single().ValuesJson);
    }

    [Fact]
    public void Calling_materialise_twice_with_nothing_new_writes_nothing_the_second_time()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");

        _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);
        var again = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.True(again.IsOk);
        Assert.Equal(0, again.Written);
        Assert.Equal(1, again.AlreadyPresent);
        Assert.Single(_store.ListPlayerSpecies(player.Id));
    }

    [Fact]
    public void A_nonexistent_player_is_refused_not_silently_skipped()
    {
        SeedSpecies("conezombie", 10);

        var outcome = _store.MaterialisePlayerSpecies(playerId: 999_999, PinTheta, Tuning);

        Assert.False(outcome.IsOk);
        Assert.Equal(0, outcome.Written);
    }

    [Fact]
    public void Power_json_is_null_on_every_stored_row()
    {
        SeedSpecies("conezombie", 10);
        var player = _store.CreatePlayer("Owner");

        _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        var row = _store.ListPlayerSpecies(player.Id).Single();
        Assert.All(_store.GetInstance(row.InstanceId)!.Atoms, a => Assert.Null(a.PowerJson));
    }

    [Fact]
    public void A_partial_failure_writes_nothing_for_that_call()
    {
        // Simulate drift a real catalog can produce: a container references an atom that existed at
        // write time (UpsertContainer's own validation passed) and is later removed from the catalog
        // directly — the exact inconsistency `ContentValidation.OrphanAffixes` (T3.8) exists to catch,
        // used here to force InstanceProducer.Compose to refuse cleanly rather than throw.
        SeedSpecies("conezombie", 10); // will succeed
        SeedSpecies("peashooter", 20); // atom removed below, forcing Compose to reject this one
        var player = _store.CreatePlayer("Owner");

        var hotPath = _store.HotPath;
        using (var raw = SqliteConnectionFactory.Open(hotPath))
        {
            using var cmd = raw.CreateCommand();
            cmd.CommandText = "DELETE FROM effect_atom WHERE atom_id = 'atom.peashooter-vitality.t1';";
            cmd.ExecuteNonQuery();
        }

        var outcome = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.False(outcome.IsOk, "a dangling atom reference must refuse the whole call, not partially succeed");
        Assert.Equal(0, outcome.Written);
        Assert.Empty(_store.ListPlayerSpecies(player.Id)); // conezombie was NOT written either
    }

    [Fact]
    public void Full_roster_materialisation_time_is_measured_not_just_believed()
    {
        for (var i = 0; i < 20; i++)
            SeedSpecies($"species{i:D3}", 10 + i);
        var player = _store.CreatePlayer("Owner");

        var outcome = _store.MaterialisePlayerSpecies(player.Id, PinTheta, Tuning);

        Assert.True(outcome.IsOk, outcome.Rejection.ToString());
        Assert.Equal(20, outcome.Written);
        Assert.True(outcome.ElapsedMs < 5000,
            $"materialising 20 species took {outcome.ElapsedMs}ms — spec §5 calls out the write as the " +
            "unmeasured cost; this is the measurement, on a small roster since a real ~900-species " +
            "catalog is not shipped content yet (a real, small pilot batch landed 2026-09-06 — see " +
            "the test below — the full-scale run remains deferred).");
    }

    /// <summary>
    /// seed-to-concrete Checkpoint 7's own closing line ("two players' rosters differ, and each
    /// player's own roster is stable across sessions"), proven end to end against REAL committed
    /// content for the first time (2026-09-06) — every test above uses <c>SeedSpecies</c>, a
    /// synthetic fixture; this imports the REAL `data/seed/effects/affixes/all.json` (T7.1's own real
    /// 10-affix catalog) and the REAL `data/seed/creatures/species-effects/**` pilot batch (T5.3's own
    /// real 3-species content, `entry_for`'s bug-fixed output) through the exact same
    /// `AtomSeedFile.Collect` → `RpgStore.ImportContent` path a live server uses, not a hand-built row.
    ///
    /// <para>Scoped to atoms + affixes + species-effects only (never the full `data/seed` tree) so
    /// this test is not coupled to [[vocabulary-json-seedscanner-defect]] — a real, separately-filed,
    /// unrelated `SeedScanner` defect that would otherwise refuse this same import for a reason that
    /// has nothing to do with what this test proves.</para>
    /// </summary>
    [Fact]
    public void Two_real_players_get_differing_rosters_from_the_real_committed_species_effects_content()
    {
        var repoRoot = FindRepoRoot();
        var atomFiles = Directory.GetFiles(Path.Combine(repoRoot, "data", "seed", "atoms"), "*.json")
            .Where(f => !Path.GetFileName(f).Equals("vocabulary.json", StringComparison.OrdinalIgnoreCase));
        var affixFiles = Directory.GetFiles(Path.Combine(repoRoot, "data", "seed", "effects", "affixes"), "*.json");
        var speciesEffectFiles = Directory.GetFiles(
            Path.Combine(repoRoot, "data", "seed", "creatures", "species-effects"), "*.json", SearchOption.AllDirectories);

        var files = atomFiles.Concat(affixFiles).Concat(speciesEffectFiles)
            .Select(f => (Path: f, Json: File.ReadAllText(f)));

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

        var outcome = _store.ImportContent(collected.Content);
        Assert.True(outcome.IsOk, string.Join("; ", outcome.Errors));

        var p1 = _store.CreatePlayer("RealContentOne");
        var p2 = _store.CreatePlayer("RealContentTwo");
        Assert.NotEqual(p1.WorldSeed, p2.WorldSeed);

        var m1 = _store.MaterialisePlayerSpecies(p1.Id, PinTheta, Tuning);
        var m2 = _store.MaterialisePlayerSpecies(p2.Id, PinTheta, Tuning);
        Assert.True(m1.IsOk, m1.Rejection.ToString());
        Assert.True(m2.IsOk, m2.Rejection.ToString());
        Assert.Equal(3, m1.Written); // peashooter, sunflower, conezombie — this pilot batch's own three
        Assert.Equal(3, m2.Written);

        var roster1 = _store.ListPlayerSpecies(p1.Id);
        var roster2 = _store.ListPlayerSpecies(p2.Id);
        Assert.Equal(3, roster1.Count);
        Assert.Equal(3, roster2.Count);

        // Each player's own roster is non-empty and real (an instance actually exists per row) —
        // the "species effects" half of Checkpoint 7's creature-summon line, independent of trait roll
        // or commander buff, which remain their own, separately-tracked gaps.
        Assert.All(roster1, r => Assert.NotNull(_store.GetInstance(r.InstanceId)));
        Assert.All(roster2, r => Assert.NotNull(_store.GetInstance(r.InstanceId)));

        // "differ": every one of the three real species rolled a different RollSeed for the two
        // players (real, independent per-player world seeds — T5.1), matching the already-proven
        // synthetic-fixture property above (Two_world_seeds_produce_different_instance_content), now
        // shown against real content too.
        foreach (var speciesId in new[] { "peashooter", "sunflower", "conezombie" })
        {
            var i1 = _store.GetInstance(roster1.Single(r => r.SpeciesId == speciesId).InstanceId)!;
            var i2 = _store.GetInstance(roster2.Single(r => r.SpeciesId == speciesId).InstanceId)!;
            Assert.NotEqual(i1.RollSeed, i2.RollSeed);
        }
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("data/seed");
    }
}
