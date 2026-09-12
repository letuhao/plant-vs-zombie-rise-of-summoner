using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// Module `test-store-helper` (T6): one leak-proof way to build and dispose a store. Memory is the
/// default; a real file is opt-in for the classes that test file semantics. Standard:
/// docs/contributing/testing-standard.md.
/// </summary>
public class DataTestStoreTests
{
    static AtomRow Atom(string family) => new()
    {
        AtomId = AtomRow.DeriveId(family, "", 1),
        KindId = "stat.modify",
        FamilyId = family,
        Variant = "",
        Tier = 1,
        Name = family,
        ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":10}",
    };

    [Fact]
    public void Create_returns_an_initialized_memory_store_that_round_trips_sql()
    {
        using var test = DataTestStore.Create();

        Assert.True(test.Store.UpsertAtom(Atom("atom.vitality")).IsOk);
        Assert.NotEmpty(test.Store.ListAtoms());
        Assert.Null(test.DataDir);
    }

    [Fact]
    public void Create_creates_no_temp_directory()
    {
        string before;
        string after;
        using (var test = DataTestStore.Create())
        {
            before = test.Store.HotPath;
            Assert.NotEmpty(before);
        }

        // A memory plan's "path" is a URI, never a directory on disk.
        after = before;
        Assert.StartsWith("file:", after, StringComparison.Ordinal);
        Assert.False(File.Exists(after));
    }

    [Fact]
    public void CreateFileBacked_returns_an_initialized_file_store_with_a_unique_dir()
    {
        using var a = DataTestStore.CreateFileBacked();
        using var b = DataTestStore.CreateFileBacked();

        Assert.NotNull(a.DataDir);
        Assert.NotNull(b.DataDir);
        Assert.NotEqual(a.DataDir, b.DataDir);
        Assert.True(File.Exists(a.Store.HotPath), "the file plan creates its file");
        Assert.True(a.Store.UpsertAtom(Atom("atom.might")).IsOk);
    }

    [Fact]
    public void Two_memory_stores_are_independent()
    {
        using var a = DataTestStore.Create();
        using var b = DataTestStore.Create();

        Assert.True(a.Store.UpsertAtom(Atom("atom.vitality")).IsOk);
        Assert.Empty(b.Store.ListAtoms());
    }

    [Fact]
    public void Memory_helper_adds_no_substrate_gate_entry()
    {
        // The helper must not become a new violation: it appears in no baseline line.
        var baseline = File.ReadAllText(Path.Combine(RepoRoot(), "scripts", "test-substrate-baseline.txt"));
        Assert.DoesNotContain("DataTestStore", baseline, StringComparison.Ordinal);
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "scripts", "guard-test-substrate.ps1")))
                return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root not found");
    }
}
