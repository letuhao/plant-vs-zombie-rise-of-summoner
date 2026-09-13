using FusionRpg.Server;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// File-bound by subject: the thing under test is the corpus LOADER's nested-partition walk, so the
/// fixture is a real directory tree on disk, not a store. It uses no SQLite, so the substrate gate's
/// `temp-store` rule does not apply; the one thing it was doing wrong was swallowing a failed delete
/// (testing-standard R3) — a failed cleanup is a failure, never `catch { }`.
/// </summary>
[Trait("Category", "DiskSemantics")]
public sealed class BaseTypeSocketMaxCorpusTests
{
    [Fact]
    public void Load_readsNestedBaseTypePartitions()
    {
        var root = Path.Combine(Path.GetTempPath(), "fusionrpg-base-sockets-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(root, "footing", "plant");
        Directory.CreateDirectory(nested);
        try
        {
            File.WriteAllText(Path.Combine(nested, "a.json"), """
                { "kind": "base-type", "entries": [
                  { "id": "item.plant-runner-a-001", "socketMax": 3 }
                ] }
                """);

            var lookup = BaseTypeSocketMaxCorpus.Load(root);

            Assert.Equal(3, lookup("item.plant-runner-a-001"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
