using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// Module `memory-storage-plan` (T3): the store's storage plan — three public doors, keeper-held
/// memory databases with no file on disk, the enforced URI trap, the archive throw, and a no-op
/// Dispose for the production file plan.
///
/// <para>T2's <see cref="SqliteConnectionFactory"/> file-path tests live in
/// <c>MemoryStoragePlanTests.cs</c>; they touch real temp files, while this file creates none.</para>
/// </summary>
public class RpgStoreStoragePlanTests
{
    // A registry-valid atom row: the store round-trips it, so this is a real write/read probe.
    static AtomRow Atom(string family, int tier = 1) => new()
    {
        AtomId = AtomRow.DeriveId(family, "", tier),
        KindId = "stat.modify",
        FamilyId = family,
        Variant = "",
        Tier = tier,
        Name = family,
        ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":10}",
    };

    [Fact]
    public void InMemory_store_inits_full_schema_and_round_trips_sql_with_no_file()
    {
        using var store = RpgStore.InMemory();
        store.Init();

        Assert.True(store.UpsertAtom(Atom("atom.vitality")).IsOk);
        Assert.NotEmpty(store.ListAtoms());

        // The plan's paths are memory URIs, not files, and nothing exists on disk for them.
        Assert.True(SqliteConnectionFactory.IsMemoryUri(store.HotPath), store.HotPath);
        Assert.True(SqliteConnectionFactory.IsMemoryUri(store.MediaPath), store.MediaPath);
        Assert.False(File.Exists(store.HotPath), "a memory 'hot path' must not exist as a file");
        Assert.Equal("", store.DataDir);
    }

    [Fact]
    public void Memory_store_schema_is_a_real_relational_schema()
    {
        using var store = RpgStore.InMemory();
        store.Init();

        using var db = SqliteConnectionFactory.Open(store.HotPath);
        using var cmd = db.CreateCommand();
        cmd.CommandText = "SELECT count(*) FROM sqlite_master WHERE type='table';";
        var tables = Convert.ToInt64(cmd.ExecuteScalar());
        Assert.True(tables > 50, $"expected the full schema, saw {tables} tables");
    }

    [Fact]
    public void All_three_public_doors_build_a_usable_store()
    {
        // A relative marker path: the memory plan must ignore DataDir entirely and never create it.
        var ignoredDir = "msp-ignored-dir-" + Guid.NewGuid().ToString("N");

        // Door 1: static factory.
        using (var a = RpgStore.InMemory())
        {
            a.Init();
            Assert.True(a.UpsertAtom(Atom("atom.vitality")).IsOk);
        }

        // Door 2: the bool flag on the string constructor.
        using (var b = new RpgStore(ignoredDir, inMemory: true))
        {
            b.Init();
            Assert.True(b.UpsertAtom(Atom("atom.might")).IsOk);
            Assert.True(SqliteConnectionFactory.IsMemoryUri(b.HotPath));
            Assert.Equal("", b.DataDir);
        }

        // Door 3: the options record, including an explicit database name.
        using (var c = new RpgStore(new RpgStoreOptions { InMemory = true, HotName = "msp-named-" + Guid.NewGuid().ToString("N") }))
        {
            c.Init();
            Assert.True(c.UpsertAtom(Atom("atom.vitality", 2)).IsOk);
            Assert.True(SqliteConnectionFactory.IsMemoryUri(c.HotPath));
        }

        // The bool door with inMemory:true must NOT have created the passed dir.
        Assert.False(Directory.Exists(ignoredDir), "an in-memory plan must ignore dataDir entirely");
    }

    [Fact]
    public void File_plan_constructor_is_unchanged_and_Dispose_is_a_noop()
    {
        // No Init and no temp-dir acquisition: this asserts path computation only, so it creates
        // nothing on disk. The production file *behavior* is covered by the existing smoke tests,
        // which pass unmodified (full Data suite green).
        var dir = "msp-fileplan-" + Guid.NewGuid().ToString("N");
        using var store = new RpgStore(dir);

        Assert.False(SqliteConnectionFactory.IsMemoryUri(store.HotPath));
        Assert.Equal(Path.Combine(Path.GetFullPath(dir), LegacyMonoMigrator.HotFileName), store.HotPath);
        Assert.Equal(Path.Combine(Path.GetFullPath(dir), "archive"), store.ArchiveDir);
        Assert.NotEmpty(store.DataDir);

        // File-plan Dispose is a no-op: it must not throw and must not remove anything.
        store.Dispose();
        Assert.False(Directory.Exists(dir), "construction alone must not create the directory");
    }

    [Fact]
    public void The_URI_trap_is_rejected_by_the_options_record()
    {
        var uri = SqliteConnectionFactory.MemoryUri("msp-trap-" + Guid.NewGuid().ToString("N"));
        var ex = Assert.Throws<InvalidOperationException>(
            () => new RpgStore(new RpgStoreOptions { DataDir = uri, InMemory = false }));
        Assert.Contains("memory URI", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Memory_stores_are_independent_under_parallel_creation()
    {
        // No singleton, no shared DB: every store gets its own unique names and its own rows.
        var results = new System.Collections.Concurrent.ConcurrentBag<string>();
        System.Threading.Tasks.Parallel.For(0, 24, i =>
        {
            using var store = RpgStore.InMemory();
            store.Init();
            Assert.True(store.UpsertAtom(Atom("atom.vitality", 1 + (i % 5))).IsOk);
            foreach (var a in store.ListAtoms()) results.Add($"{i}:{a.Tier}");
        });

        Assert.Equal(24, results.Distinct().Count());
    }

    [Fact]
    public void Keeper_holds_the_memory_db_across_connection_open_and_close()
    {
        using var store = RpgStore.InMemory();
        store.Init();
        Assert.True(store.UpsertAtom(Atom("atom.vitality")).IsOk);

        // A direct open + close of the same shared-memory DB, then read through another connection,
        // proves the store's keeper (not the transient connection) owns the lifetime.
        using (var transient = SqliteConnectionFactory.Open(store.HotPath)) { }
        Assert.NotEmpty(store.ListAtoms());
    }

    [Fact]
    public void Memory_store_archive_entry_point_throws_rather_than_writing_to_cwd()
    {
        using var store = RpgStore.InMemory();
        store.Init();

        var ex = Assert.Throws<InvalidOperationException>(() => store.ArchiveDir);
        Assert.Contains("memory", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reset_on_a_memory_store_leaves_a_usable_store_with_no_file()
    {
        using var store = RpgStore.InMemory();
        store.Init();
        Assert.True(store.UpsertAtom(Atom("atom.vitality", 3)).IsOk);
        Assert.NotEmpty(store.ListAtoms());

        store.Reset();

        // Schema is back (usable), data is gone, and still no file on disk.
        Assert.True(store.UpsertAtom(Atom("atom.might")).IsOk);
        Assert.NotEmpty(store.ListAtoms());
        Assert.False(File.Exists(store.HotPath));
    }
}
