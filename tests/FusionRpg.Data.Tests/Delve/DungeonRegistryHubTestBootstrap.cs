using System.Runtime.CompilerServices;
using FusionRpg.Core.Dungeon.Registry;

namespace FusionRpg.Data.Tests.Delve;

/// <summary>
/// Configures <see cref="DungeonRegistryHub"/> once for the whole assembly from the real, shipped
/// registry files — <see cref="FusionRpg.Core.Delve.Roll.DelveGraphRoll.Roll"/> reads
/// <see cref="RoomKindCatalog"/>/<see cref="RaidModeCatalog"/> through this static hub even though
/// its tuning comes in as an explicit parameter, so any Data.Tests class rolling a real graph needs
/// this configured regardless of which test class xunit happens to run first (mirrors
/// `FusionRpg.Core.Tests.Dungeon.DungeonHubTestBootstrap`'s shape).
/// </summary>
internal static class DungeonRegistryHubTestBootstrap
{
    [ModuleInitializer]
    public static void Init()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "dungeon")))
            dir = dir.Parent;
        if (dir is null) throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);

        var registries = DungeonRegistryLoader.LoadAll(Path.Combine(dir.FullName, "data", "seed", "dungeon", "_registry"));
        DungeonRegistryHub.Configure(registries);
    }
}
