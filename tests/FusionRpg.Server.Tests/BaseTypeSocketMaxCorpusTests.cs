using FusionRpg.Server;
using Xunit;

namespace FusionRpg.Server.Tests;

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
            try { Directory.Delete(root, recursive: true); } catch { /* temp directory */ }
        }
    }
}
