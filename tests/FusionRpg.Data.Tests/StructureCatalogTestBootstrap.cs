using System.Runtime.CompilerServices;
using FusionRpg.Core.World;
using FusionRpg.Core.World.StructureSeed;

namespace FusionRpg.Data.Tests;

/// <summary>
/// base-defense-todo.md 25.4: this assembly never references `StructureCatalog` by name, but its own
/// world-turn-replay tests (`WorldWaveOneAcceptanceTests`, `WorldTwentyTurnCheckpointTests`, etc.) run
/// real `TurnEngine`/`DistrictAssaultResolver` code that reaches it transitively. Once `Seed` is
/// deleted, those tests need a real, loaded corpus first — the SAME reasoning and the SAME split from
/// `ContractTuningTestBootstrap.cs` as the `FusionRpg.Core.Tests` copy of this file; see that copy's
/// own doc comment for the full "why a separate initializer" account.
/// </summary>
internal static class StructureCatalogTestBootstrap
{
    [ModuleInitializer]
    public static void Init() => StructureCatalog.Configure(StructureCorpus.Load(CorpusRoot()));

    static string CorpusRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "data", "seed", "structures");
            if (Directory.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate data/seed/structures above " + AppContext.BaseDirectory);
    }
}
