using Microsoft.Data.Sqlite;

namespace FusionRpg.Data.Tests;

/// <summary>
/// One way to build and dispose a store in a test (module <c>test-store-helper</c>). The default is
/// in-memory — no file, no temp dir, nothing to clean up — and the store is already <c>Init</c>ed.
/// <see cref="CreateFileBacked"/> is the opt-in for the classes that test file semantics (legacy
/// migration, WAL journal mode, <c>File.Exists</c> on hot/media, archive slices on disk, storage
/// purge), and its dispose is leak-proof: it clears SQLite's connection pool before deleting, and a
/// failed delete **throws** rather than being swallowed (docs/contributing/testing-standard.md R3).
///
/// <para>The caller never calls <c>Init()</c> and never calls <c>ClearAllPools()</c>; this helper owns
/// both.</para>
/// </summary>
public sealed class DataTestStore : IDisposable, IAsyncDisposable
{
    /// <summary>The initialized store.</summary>
    public RpgStore Store { get; }

    /// <summary>The file-backed data directory, or <c>null</c> for the memory plan.</summary>
    public string? DataDir { get; }

    DataTestStore(RpgStore store, string? dataDir)
    {
        Store = store;
        DataDir = dataDir;
    }

    /// <summary>An in-memory store with unique database names, initialized and ready to use.</summary>
    public static DataTestStore Create()
    {
        var store = RpgStore.InMemory();
        store.Init();
        return new DataTestStore(store, dataDir: null);
    }

    /// <summary>
    /// A real-file store in a unique directory under the test output root, initialized and ready to
    /// use. Dispose deletes the directory; a failed delete throws.
    /// </summary>
    public static DataTestStore CreateFileBacked()
    {
        var dir = Path.Combine(AppContext.BaseDirectory, "teststore-" + Guid.NewGuid().ToString("N"));
        var store = new RpgStore(dir);
        store.Init();
        return new DataTestStore(store, dir);
    }

    public void Dispose()
    {
        Store.Dispose(); // releases the memory keepers / closes the file store

        if (DataDir is null)
            return;

        // Pooling holds the file handle past the connection close, so clear pools before deleting.
        // No catch: a cleanup failure must fail the test, never be swallowed.
        SqliteConnection.ClearAllPools();
        Directory.Delete(DataDir, recursive: true);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }
}
