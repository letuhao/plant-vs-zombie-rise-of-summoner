using FusionRpg.Core.Stats.Aptitudes;
using FusionRpg.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>class-system-todo.md P2.5/P9.1 — the battle-path seam, `WebMatchService.AptitudeChannelMods`.
/// Now reads the real commander-scope allocation via `RpgStore.LoadAllocation`
/// (spec-aptitude-allocation-surface.md, 2026-08-27) instead of hardcoding `AptitudeAllocation.Empty` —
/// these tests prove BOTH directions: an unset player still resolves inert (the wiring didn't regress
/// the "zero goldens move" property), and a saved allocation actually reaches the resolved mods (the
/// wire is load-bearing, not dead code that happens to compile).</summary>
public class AptitudeChannelModsTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public AptitudeChannelModsTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-aptchanmods-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    [Fact]
    public void UnsetPlayer_stillProducesNoChannelMods()
    {
        // A player who has never allocated must resolve exactly as inert as the old hardcoded-Empty
        // behavior did -- LoadAllocation's own "load never saved returns empty" contract
        // (AllocationStoreTests.cs), not a special case this seam has to invent.
        var mods = WebMatchService.AptitudeChannelMods(level: 50, playerId: 999, _store);
        Assert.Empty(mods);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(50)]
    [InlineData(1000)]
    public void UnsetPlayer_stillProducesNoChannelMods_atAnyLevel(int level)
    {
        Assert.Empty(WebMatchService.AptitudeChannelMods(level, playerId: 999, _store));
    }

    [Fact]
    public void SavedAllocation_actuallyReachesTheResolvedMods_theWireIsLoadBearing()
    {
        // The real point of the P9.1 wiring change: a saved commander-scope allocation must produce a
        // DIFFERENT, non-empty result than the unset case above -- not just "the code compiles and the
        // old inert case still passes." Might funds combat.power.omni directly (P2.4's own vertical).
        const long playerId = 42;
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 100_000);
        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId), allocation);

        var mods = WebMatchService.AptitudeChannelMods(level: 50, playerId, _store);

        Assert.NotEmpty(mods);
        Assert.Contains(mods, m => m.ChannelId == FusionRpg.Core.Stats.Derived.DerivedStatChannels.CombatPowerOmni);

        // A DIFFERENT player who never allocated, read through the same store, must still be inert --
        // proves the allocation is scoped per-player (ScopeKey), not accidentally global state.
        Assert.Empty(WebMatchService.AptitudeChannelMods(level: 50, playerId: 43, _store));
    }

    /// <summary>class-system-todo.md P9.2's own prerequisite, found while building it: rpg_aptitude_
    /// allocation is an upsert with no history, so a real player who ever re-allocates would make every
    /// earlier battle's aptitude signal unrecoverable unless the battle record itself carries a snapshot.
    /// Proves the fix end to end through the REAL public entry point (RunWebMatchAsync, the same
    /// log-before-ingest -> BattleEngine.Resolve -> BattleReportEmitter.Emit -> InsertWebMatchEvents
    /// pipeline a real expedition collect drives) — not a re-implementation of the emission logic.</summary>
    [Fact]
    public async Task RealBattle_recordsAnAptitudeSnapshotEvent_carryingTheAllocationThatProducedIt()
    {
        const long playerId = 77;
        var allocation = AptitudeAllocation.Single(AllocationScope.Commander, "Might", 300)
                        + AptitudeAllocation.Single(AllocationScope.Commander, "Vigor", 100);
        _store.SaveAllocation(AllocationScope.Commander, AptitudeEndpoints.ScopeKey(playerId), allocation);

        // BattleEngine.Resolve needs far more configured than AptitudeChannelMods alone (the other
        // tests in this file exercise only that) -- PowerAndAptitudeTuningTestBootstrap's
        // [ModuleInitializer] covers Power/Aptitude/DerivedStatPolicy only, nothing battle-shaped, and
        // nothing in this assembly ran a full battle before this test existed. Mirrors Program.cs's own
        // real startup sequence exactly (its every *.Configure(...) call except Power/Aptitude, which
        // the module initializer already set and this test does not need to override).
        var tuningDir = Path.Combine(FindRepoRoot(), "data", "tuning");
        string Read(string name) => File.ReadAllText(Path.Combine(tuningDir, name));
        FusionRpg.Core.Demons.Contracts.ContractPolicy.Configure(
            FusionRpg.Core.Demons.Contracts.ContractTuningLoader.Parse(Read("contracts.v1.json")));
        FusionRpg.Core.World.Loam.LoamPolicy.Configure(
            FusionRpg.Core.World.Loam.LoamTuningLoader.Parse(Read("loam.v4.json")));
        FusionRpg.Core.World.WorldTuningHub.Configure(
            FusionRpg.Core.World.WorldTuningLoader.Parse(Read("world.v5.json")));
        FusionRpg.Core.Demons.SoulEarnPolicy.Configure(
            FusionRpg.Core.Demons.SoulEarnTuningLoader.Parse(Read("souls.v1.json")));
        FusionRpg.Core.Demons.Patron.PatronPolicy.Configure(
            FusionRpg.Core.Demons.Patron.PatronTuningLoader.Parse(Read("patron.v1.json")));
        FusionRpg.Core.Demons.Fusion.StarPolicy.Configure(
            FusionRpg.Core.Demons.Fusion.FusionTuningLoader.Parse(Read("fusion.v1.json")));
        FusionRpg.Core.SimDefaults.Configure(
            FusionRpg.Core.SimTuningLoader.Parse(Read("sim.v1.json")));
        FusionRpg.Core.Demons.SummoningTuningHub.Configure(
            FusionRpg.Core.Demons.SummoningTuningLoader.Parse(Read("summoning.v1.json")));
        FusionRpg.Core.World.Ai.WorldAiPolicy.Configure(
            FusionRpg.Core.World.Ai.WorldAiTuningLoader.Parse(Read("ai.v2.json")));
        FusionRpg.Data.Policies.SealedCompactionPolicy.Configure(
            FusionRpg.Data.Policies.DataTuningLoader.Parse(Read("data.v1.json")));
        FusionRpg.Core.Combat.Shield.ShieldPolicy.Configure(
            FusionRpg.Core.Combat.Shield.ShieldTuningLoader.Parse(Read("shield.v1.json")));
        FusionRpg.Core.Combat.CombatPolicy.Configure(
            FusionRpg.Core.Combat.CombatTuningLoader.Parse(Read("combat.v1.json")));
        FusionRpg.Core.Status.StatusPolicy.Configure(
            FusionRpg.Core.Status.StatusTuningLoader.Parse(Read("status.v1.json")));
        FusionRpg.Core.Overlay.OverlayTuningHub.Configure(
            FusionRpg.Core.Overlay.OverlayTuningLoader.Parse(Read("overlay.v1.json")));
        FusionRpg.Core.Stats.Derived.StatsTuningHub.Configure(
            FusionRpg.Core.Stats.Derived.StatsTuningLoader.Parse(Read("stats.v1.json")));
        FusionRpg.Core.Expeditions.ExpeditionTuningHub.Configure(
            FusionRpg.Core.Expeditions.ExpeditionTuningLoader.Parse(Read("expeditions.v1.json")));
        FusionRpg.Core.Progression.ProgressionTuningHub.Configure(
            FusionRpg.Core.Progression.ProgressionTuningLoader.Parse(Read("progression.v1.json")));
        FusionRpg.Core.Battle.BattleTuningHub.Configure(
            FusionRpg.Core.Battle.BattleTuningLoader.Parse(Read("battle.v2.json")));

        // A real IHubContext<RpgHub>, not a hand-rolled fake -- SignalR's own DI wiring, the same
        // production type RunWebMatchAsync's own hub.Clients.Group(...).SendAsync(...) call needs.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSignalR();
        await using var provider = services.BuildServiceProvider();
        var hub = provider.GetRequiredService<IHubContext<RpgHub>>();
        var service = new WebMatchService(_store, hub);

        // No roster seeded -- BuildSquad's own documented SIM fallback ("an empty roster still gets a
        // deterministic synthetic squad") fields this without needing a real summoned demon at all.
        var (ok, reason, outcome) = await service.RunWebMatchAsync(
            playerId, correlationId: "aptsnap-test-1", waveId: "rift-skirmish", squadInstanceIds: null);
        Assert.True(ok, reason);

        var events = _store.ListEvents(limit: 1000, afterId: 0, playerId: playerId);
        var snapshot = Assert.Single(events, e => e.Kind == "aptitude.snapshot" && e.MatchKey == outcome!.MatchKey);
        var payload = Assert.IsType<System.Text.Json.JsonElement>(
            System.Text.Json.JsonSerializer.SerializeToElement(snapshot.Payload));
        var shares = payload.GetProperty("shares");
        Assert.Equal("commander", payload.GetProperty("scope").GetString());
        Assert.Equal(0.75, shares.GetProperty("Might").GetDouble(), precision: 6);
        Assert.Equal(0.25, shares.GetProperty("Vigor").GetDouble(), precision: 6);
        Assert.Equal(0.0, shares.GetProperty("Ferocity").GetDouble(), precision: 6);
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("could not locate repo root above " + AppContext.BaseDirectory);
    }
}
