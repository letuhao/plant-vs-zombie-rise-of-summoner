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
}
