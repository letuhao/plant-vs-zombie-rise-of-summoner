using FusionRpg.Core.Demons;
using FusionRpg.Core.Demons.Generation;
using FusionRpg.Core.Progression;
using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>class-system-todo.md P6.2 — <c>rpg_aptitude_allocation</c> / <c>RpgStore.Aptitudes.cs</c>
/// (spec-point-economy.md, read in full this session; table in §7: tests 7 and 8 covered here — the
/// ones that are this store's own concern, not `PointBudget`'s (P6.1, already covered in
/// `PointBudgetTests.cs`) or `RespecPolicy`'s (P6.3, unbuilt).</summary>
public class AllocationStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public AllocationStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-aptalloc-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public void Allocation_roundTrips_perScope()
    {
        // spec-point-economy.md §7 test 7: round-trips PER SCOPE -- two different scopes, saved and
        // loaded independently, must not bleed into each other.
        var commander = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 12)
                       + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 30);
        var uniqueDemon = AptitudeAllocation.Single(AllocationScope.UniqueDemon, "Bulwark", 55);

        _store.SaveAllocation(AllocationScope.Commander, "player:1", commander);
        _store.SaveAllocation(AllocationScope.UniqueDemon, "instance:abc", uniqueDemon);

        var loadedCommander = _store.LoadAllocation(AllocationScope.Commander, "player:1");
        var loadedUnique = _store.LoadAllocation(AllocationScope.UniqueDemon, "instance:abc");

        Assert.Equal(12, loadedCommander.PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(30, loadedCommander.PointsAt(AllocationScope.Commander, "Vigor"));
        Assert.Equal(0, loadedCommander.PointsAt(AllocationScope.UniqueDemon, "Bulwark")); // no bleed

        Assert.Equal(55, loadedUnique.PointsAt(AllocationScope.UniqueDemon, "Bulwark"));
        Assert.Equal(0, loadedUnique.PointsAt(AllocationScope.Commander, "Might")); // no bleed
    }

    [Fact]
    public void Allocation_roundTrips_forDifferentKeysInTheSameScope()
    {
        // Two DIFFERENT commanders (different scopeKey, same scope) must not collide.
        var a = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 10);
        var b = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 90);

        _store.SaveAllocation(AllocationScope.Commander, "player:a", a);
        _store.SaveAllocation(AllocationScope.Commander, "player:b", b);

        Assert.Equal(10, _store.LoadAllocation(AllocationScope.Commander, "player:a").PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(90, _store.LoadAllocation(AllocationScope.Commander, "player:b").PointsAt(AllocationScope.Commander, "Might"));
    }

    [Fact]
    public void LoadAllocation_neverSaved_returnsEmpty_notNullNotThrown()
    {
        // AptitudeAllocation's own contract: "empty means all-zero shares, never invent a default."
        var loaded = _store.LoadAllocation(AllocationScope.Aspect, "never-saved-key");

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.GrandTotal());
    }

    [Fact]
    public void SaveAllocation_resavingWithFewerPoints_removesTheStaleRow()
    {
        // "The store holds the current allocation, not a change log" -- a respec that zeroes an
        // aptitude must actually delete its row, not leave a stale nonzero value behind.
        var first = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 40)
                  + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 20);
        _store.SaveAllocation(AllocationScope.Commander, "player:1", first);

        var respecced = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 60); // Vigor dropped entirely
        _store.SaveAllocation(AllocationScope.Commander, "player:1", respecced);

        var loaded = _store.LoadAllocation(AllocationScope.Commander, "player:1");
        Assert.Equal(60, loaded.PointsAt(AllocationScope.Commander, "Might"));
        Assert.Equal(0, loaded.PointsAt(AllocationScope.Commander, "Vigor")); // gone, not stale
        Assert.Equal(60, loaded.GrandTotal());
    }

    // ---- "unknown scope rejects" (§7 test 7) ---------------------------------------------------------

    [Fact]
    public void ScopeFromText_unknownScope_rejectsNamingIt()
    {
        var ex = Assert.Throws<ArgumentException>(() => RpgStore.ScopeFromText("guildmaster"));
        Assert.Contains("guildmaster", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScopeToText_andBack_roundTripsForAllFourScopes()
    {
        foreach (var scope in new[] { AllocationScope.Commander, AllocationScope.DemonType, AllocationScope.Aspect, AllocationScope.UniqueDemon })
            Assert.Equal(scope, RpgStore.ScopeFromText(RpgStore.ScopeToText(scope)));
    }

    [Fact]
    public void SaveAllocation_emptyScopeKey_rejects()
    {
        Assert.Throws<ArgumentException>(() => _store.SaveAllocation(AllocationScope.Commander, "", AptitudeAllocation.Empty));
        Assert.Throws<ArgumentException>(() => _store.SaveAllocation(AllocationScope.Commander, "   ", AptitudeAllocation.Empty));
    }

    // ---- "no channel-value column exists in the schema" (§7 test 8) ---------------------------------

    [Fact]
    public void Schema_storesInputsOnly_noResolvedChannelValueColumn()
    {
        // spec-point-economy.md §6: "Persistence stores the allocation, never the resolved channels."
        // Asserted directly on the live schema, not on the store's own C# API surface -- a resolved
        // value could otherwise sneak in as an extra column nothing in this test file's own method
        // calls would ever exercise.
        using var db = SqliteConnectionFactory.Open(_store.HotPath, readOnly: true);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "PRAGMA table_info(rpg_aptitude_allocation);";
        using var r = cmd.ExecuteReader();

        var columns = new List<string>();
        while (r.Read())
            columns.Add(r.GetString(1)); // column 1 of table_info is the column name

        Assert.Equal(
            new[] { "scope", "scope_key", "aptitude_id", "points" }.OrderBy(x => x, StringComparer.Ordinal),
            columns.OrderBy(x => x, StringComparer.Ordinal));

        // Explicitly not just "the four expected columns exist" -- also that nothing ELSE is there,
        // which is the actual "no resolved channel value" claim (a channel id would look like
        // "combat.power.omni", nothing in the four columns above resembles one).
        Assert.DoesNotContain(columns, c => c.Contains('.', StringComparison.Ordinal));
    }

    [Fact]
    public void Reset_clearsPersistedAllocations()
    {
        _store.SaveAllocation(AllocationScope.Commander, "player:1",
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 50));

        _store.Reset();

        var loaded = _store.LoadAllocation(AllocationScope.Commander, "player:1");
        Assert.Equal(0, loaded.GrandTotal());
    }

    // ---- species-build T2.1/T2.2 (demon-type-allocation) --------------------------------------

    const int FumeshroomDemonTypeId = 60007;

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    static readonly AptitudeTuning RealTuning = AptitudeTuningLoader.Parse(
        File.ReadAllText(Path.Combine(RepoRoot(), "data", "tuning", "aptitudes.v5.json")));

    static AllocationStoreTests()
    {
        // Global, unscoped (SpeciesBuildPlanCatalog has no test-scoping mechanism, unlike
        // DemonSpeciesCatalog's UseScoped) -- safe because this is the only file in the assembly that
        // touches it. Configured once via this static ctor, matching SpeciesProgressionTuningHub's own
        // "construct one inline" convention for a hub with no fixture file behind it.
        SpeciesBuildPlanCatalog.Configure(new Dictionary<string, IReadOnlyDictionary<string, long>>(StringComparer.Ordinal)
        {
            ["fumeshroom"] = new Dictionary<string, long>(StringComparer.Ordinal)
            {
                ["Might"] = 500, ["Vigor"] = 300, ["Fortitude"] = 200
            }
        });
    }

    /// <summary>Writes a species-progression row directly (bypassing the real XP curve entirely) —
    /// this file is testing `EffectiveSpeciesAllocation`'s own composition logic, not re-proving
    /// `species-xp`'s leveling pipeline (already covered in `SpeciesProgressionTests.cs`).</summary>
    void SeedSpeciesLevel(long playerId, int demonTypeId, long level, string speciesId)
    {
        using var db = SqliteConnectionFactory.Open(_store.HotPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            INSERT INTO rpg_actor_progression(
              player_id, kind, type_id, level, xp, highest_level, demotion_count, revision, updated_utc, scope_key)
            VALUES ($p, 'species', $tid, $lvl, 0, $lvl, 0, 0, $now, $sk);
            """;
        cmd.Parameters.AddWithValue("$p", playerId);
        cmd.Parameters.AddWithValue("$tid", demonTypeId);
        cmd.Parameters.AddWithValue("$lvl", level);
        cmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("o"));
        cmd.Parameters.AddWithValue("$sk", speciesId);
        cmd.ExecuteNonQuery();
    }

    [Fact]
    public void EffectiveSpeciesAllocation_withNoOverride_resolvesToThePlansBaseline_notZero()
    {
        var player = _store.CreatePlayer("SpeciesAllocBaseline");
        SeedSpeciesLevel(player.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom"); // source = 20

        var effective = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);

        // The silent-zero risk the spec calls out by name: a never-overridden species must NOT read
        // AptitudeAllocation.Empty once it has a real level.
        Assert.True(effective.TotalForScope(AllocationScope.DemonType) > 0);
        Assert.True(effective.PointsAt(AllocationScope.DemonType, "Might")
            > effective.PointsAt(AllocationScope.DemonType, "Vigor"));
    }

    [Fact]
    public void EffectiveSpeciesAllocation_atLevelOne_isEmpty()
    {
        var player = _store.CreatePlayer("SpeciesAllocLevelOne");
        // No SeedSpeciesLevel call at all -- GetRpgActor returns null, EffectiveSpeciesAllocation
        // must default to level 1, matching RpgActorState's own default (RpgStore.Progression.cs).
        var effective = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);
        Assert.Equal(0, effective.TotalForScope(AllocationScope.DemonType));
    }

    [Fact]
    public void EffectiveSpeciesAllocation_override_replaces_the_baseline_wholesale()
    {
        var player = _store.CreatePlayer("SpeciesAllocOverride");
        SeedSpeciesLevel(player.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom");
        var baseline = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);
        Assert.True(baseline.PointsAt(AllocationScope.DemonType, "Fortitude") > 0); // present in the baseline

        // A DIFFERENT vector spending the same budget on ONE aptitude only -- if override merely
        // layered onto the baseline, Fortitude would still show up; a true replace zeroes it.
        var budget = baseline.TotalForScope(AllocationScope.DemonType);
        var wholeVectorOverride = AptitudeAllocation.Single(AllocationScope.DemonType, "Ferocity", budget);
        _store.SaveAllocation(AllocationScope.DemonType, SpeciesAllocation.ScopeKey(player.Id, "fumeshroom"), wholeVectorOverride);

        var effective = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);
        Assert.Equal(budget, effective.PointsAt(AllocationScope.DemonType, "Ferocity"));
        Assert.Equal(0, effective.PointsAt(AllocationScope.DemonType, "Might"));
        Assert.Equal(0, effective.PointsAt(AllocationScope.DemonType, "Fortitude"));
    }

    [Fact]
    public void EffectiveSpeciesAllocation_deletingTheOverride_returnsExactlyTheBaseline_forFree()
    {
        var player = _store.CreatePlayer("SpeciesAllocRevert");
        SeedSpeciesLevel(player.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom");
        var baseline = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);

        var budget = baseline.TotalForScope(AllocationScope.DemonType);
        _store.SaveAllocation(AllocationScope.DemonType, SpeciesAllocation.ScopeKey(player.Id, "fumeshroom"),
            AptitudeAllocation.Single(AllocationScope.DemonType, "Ferocity", budget));
        Assert.NotEqual(baseline.PointsAt(AllocationScope.DemonType, "Might"),
            _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning).PointsAt(AllocationScope.DemonType, "Might"));

        // "Deleting the row" == saving Empty (SaveAllocation's own delete-then-insert-nonzero shape
        // leaves no rows for an all-zero save) -- reverting is free, no soul cost, no separate API.
        _store.SaveAllocation(AllocationScope.DemonType, SpeciesAllocation.ScopeKey(player.Id, "fumeshroom"), AptitudeAllocation.Empty);

        var reverted = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);
        foreach (var apt in AptitudeCatalog.All)
            Assert.Equal(baseline.PointsAt(AllocationScope.DemonType, apt.Id), reverted.PointsAt(AllocationScope.DemonType, apt.Id));
    }

    [Fact]
    public void EffectiveSpeciesAllocation_isPerPlayer_twoPlayersSameSpeciesSameLevel_oneOverridden()
    {
        var alice = _store.CreatePlayer("SpeciesAllocAlice");
        var bob = _store.CreatePlayer("SpeciesAllocBob");
        SeedSpeciesLevel(alice.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom");
        SeedSpeciesLevel(bob.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom");

        var budget = _store.EffectiveSpeciesAllocation(alice.Id, "fumeshroom", RealTuning).TotalForScope(AllocationScope.DemonType);
        _store.SaveAllocation(AllocationScope.DemonType, SpeciesAllocation.ScopeKey(alice.Id, "fumeshroom"),
            AptitudeAllocation.Single(AllocationScope.DemonType, "Ferocity", budget));

        var aliceEffective = _store.EffectiveSpeciesAllocation(alice.Id, "fumeshroom", RealTuning);
        var bobEffective = _store.EffectiveSpeciesAllocation(bob.Id, "fumeshroom", RealTuning);

        Assert.Equal(budget, aliceEffective.PointsAt(AllocationScope.DemonType, "Ferocity"));
        Assert.Equal(0, bobEffective.PointsAt(AllocationScope.DemonType, "Ferocity")); // Bob still reads his own baseline
        Assert.True(bobEffective.PointsAt(AllocationScope.DemonType, "Might") > 0);
    }

    [Fact]
    public void ScopesSum_anActorWithBothCommanderAndDemonType_readsTheSum_shareTakenOnTheSum()
    {
        var player = _store.CreatePlayer("SpeciesAllocScopeSum");
        SeedSpeciesLevel(player.Id, FumeshroomDemonTypeId, level: 21, "fumeshroom");
        _store.SaveAllocation(AllocationScope.Commander, "player:" + player.Id,
            AptitudeAllocation.Single(AllocationScope.Commander, "Might", 40));

        var commander = _store.LoadAllocation(AllocationScope.Commander, "player:" + player.Id);
        var species = _store.EffectiveSpeciesAllocation(player.Id, "fumeshroom", RealTuning);
        var combined = commander + species;

        var mightTotal = combined.Total("Might");
        Assert.Equal(40 + species.PointsAt(AllocationScope.DemonType, "Might"), mightTotal);
        // Share is taken on the SUM's grand total, not either scope's alone (AptitudeAllocation's own
        // "scopes sum before share" contract) -- a regression here would silently reintroduce
        // per-scope shares.
        Assert.Equal((double)mightTotal / combined.GrandTotal(), combined.Share("Might"));
    }
}
