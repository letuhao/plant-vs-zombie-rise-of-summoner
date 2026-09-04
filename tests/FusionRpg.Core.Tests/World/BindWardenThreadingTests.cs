using FusionRpg.Core.World;
using FusionRpg.Core.World.Loam;
using FusionRpg.Core.World.Movement;
using FusionRpg.Core.World.Turn;
using Xunit;

namespace FusionRpg.Core.Tests.World;

/// <summary>
/// world-stage W28: `bind-warden` actually writes `WorldSector.WardenBindingId` in production for
/// the first time — the field itself, and every reader of it (`LoamForecast.Weakest`,
/// `LoamPhases.Pressure`, `ClaimResolver`), were already covered by `LoamTextureTests.cs` seeding the
/// field directly. What was missing, and what this file proves, is the *writer*.
/// </summary>
public class BindWardenThreadingTests
{
    static WorldState World() => WorldTemplateCatalog.Build(WorldTemplateCatalog.FirstLightId, seed: 1);

    static WorldCommand BindWarden(string commander, string sectorId, string wardenId) => new()
    {
        CommanderId = commander, CommandId = "c-bind", Kind = WorldCommandKinds.BindWarden,
        SectorId = sectorId, WardenId = wardenId
    };

    [Fact]
    public void A_committed_bind_warden_order_sets_the_sectors_warden_binding_id()
    {
        var result = TurnEngine.Step(World(), new[] { BindWarden("dave", "homeworld", "demon-1") }, seed: 1);

        Assert.Equal("demon-1", result.World.Sectors.Single(s => s.SectorId == "homeworld").WardenBindingId);
        Assert.Contains(result.Report.Entries,
            e => e.Kind == TurnReportKinds.CommandAccepted && e.Subject == "c-bind");
        Assert.Empty(result.Report.Dropped);
    }

    [Fact]
    public void A_bound_sector_is_thereafter_excluded_from_the_weakest_pick()
    {
        // Not re-proving LoamForecast.Weakest's own exclusion (LoamTextureTests.cs already does,
        // seeding the field directly) — proving the *command* actually reaches that same state, so a
        // player's bind-warden order and a hand-seeded fixture are provably the same world shape.
        var bound = TurnEngine.Step(World(), new[] { BindWarden("dave", "homeworld", "demon-1") }, seed: 1).World;
        var component = TerritoryComponents.For(bound, "dave").Single(c => c.Contains("homeworld"));

        Assert.Null(LoamForecast.Weakest(bound, component, available: 0, upkeep: 999_999));
    }

    [Fact]
    public void A_binder_who_no_longer_owns_the_sector_at_resolution_is_refused()
    {
        // Re-validated at resolution, not trusted from admission — the same discipline
        // ClaimResolver/BuildResolver already apply, and for the same reason: the ground may have
        // been lost to fade or conquest later the same turn the order was filed.
        var world = new WorldState
        {
            Factions = new[] { new WorldFaction { FactionId = "dave", Kind = WorldFactionKind.Player, Name = "Dave" } },
            Sectors = new[]
            {
                new WorldSector { SectorId = "s", OwnerFactionId = null, Phase = SectorPhase.Lost }
            }
        };
        var report = new TurnReport();

        var result = WardenResolver.Run(world, new[] { BindWarden("dave", "s", "demon-1") }, report, "snapshot");

        Assert.Null(result.Sectors.Single(s => s.SectorId == "s").WardenBindingId);
        Assert.Contains(report.Entries, e => e.Kind == TurnReportKinds.CommandDropped && e.Detail == "warden.not-yours");
    }
}
