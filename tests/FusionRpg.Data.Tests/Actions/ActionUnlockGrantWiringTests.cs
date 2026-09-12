using FusionRpg.Core.Actions;
using FusionRpg.Core.Actions.Eligibility;
using FusionRpg.Core.Actions.Unlock;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests.Actions;

/// <summary>
/// T59.7 (spec-action-instance-and-grant.md §4): `AwardUniqueActorXpUnlocked`'s new `LevelsGained`
/// field wired to a real `ActionUnlockGrantService` roll, through both real production call sites'
/// shared shape (`AwardUniqueActorXp` here; the expedition reward apply mirrors it identically).
///
/// <para>`UnlockTuningPolicy`/`ActionFamilyMapPolicy` are process-wide statics (matching
/// `RungPolicy`/`DemonSpeciesCatalog`'s own established shape) — configured once here, never reset,
/// the same convention every other `*Policy`/`*Hub` in this codebase already follows. Every EXISTING
/// XP-award test in this project is unaffected regardless, because `TryRollActionUnlocks` no-ops
/// whenever `UnlockTuningPolicy.Tuning` is unset — this file is what turns it on.</para>
/// </summary>
public class ActionUnlockGrantWiringTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    static ActionUnlockGrantWiringTests()
    {
        // DeltaMilli: 1000 -- NO decay, so every roll (not just the first) stays at a 100% chance.
        // A real DeltaMilli < 1000 decays the chance per earn (chance(n) = p1 * delta^n) -- correct
        // ratchet behavior, but wrong for THIS test, which wants every one of several rolls to
        // deterministically succeed so it can assert on how many grants landed, not on which ones did.
        UnlockTuningPolicy.Configure(new UnlockTuning(
            P1Milli: 1000, DeltaMilli: 1000, FloorMilli: 1000, HeldCap: 10, RungCap: 10, DiscardTaxCoeffMilli: 100));
        ActionFamilyMapPolicy.Configure(new Dictionary<string, IReadOnlyList<string>>());
    }

    public ActionUnlockGrantWiringTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-action-unlock-wiring-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    void SeedAction(string actionId)
    {
        var containerId = "skill." + actionId.Replace('.', '-');
        var containerCheck = _store.UpsertContainer(new ContainerRow { ContainerId = containerId, Kind = ContainerKind.Skill });
        Assert.True(containerCheck.IsOk, containerCheck.Detail);

        var actionCheck = _store.UpsertAction(new ActionRow
        {
            ActionId = actionId, Name = actionId, Kind = ActionKind.Skill, Rung = 1,
            Enabled = true, Grantable = true, ContainerId = containerId,
        });
        Assert.True(actionCheck.IsOk, actionCheck.Detail);
    }

    [Fact]
    public void ALevelGainWithNoImportedActionsIsALegalNoOpAndAwardStillSucceeds()
    {
        var actor = _store.CreateUniqueActor(playerId: 1, side: "plant", typeId: 1);

        var (ok, reason, updated) = _store.AwardUniqueActorXp(actor.InstanceId, delta: 1_000_000);

        Assert.True(ok, reason);
        Assert.NotNull(updated);
        Assert.True(updated!.Level > 1); // liveness -- a real level gain actually happened
    }

    [Fact]
    public void ARealLevelGainGrantsAnImportedActionToTheSpecimen()
    {
        SeedAction("action.wiring-test.only");
        var actor = _store.CreateUniqueActor(playerId: 1, side: "plant", typeId: 1);

        var (ok, reason, updated) = _store.AwardUniqueActorXp(actor.InstanceId, delta: 1_000_000);
        Assert.True(ok, reason);
        Assert.True(updated!.Level > 1);

        var grants = _store.ListGrants(new OwnerScope(OwnerKind.UniqueActor, actor.InstanceId)); // matches WebMatchService.EquippedActionIdsFor's own real read scope (action-grant-owner-kind-durability, fixed 2026-09-07)
        Assert.Contains(grants, g => g.ActionId == "action.wiring-test.only");
    }

    /// <summary>Acceptance: "a level gain that crosses N thresholds in one award attempts N rolls,
    /// each pricing independently." Three distinct actions, `AlwaysAccepts`-shaped tuning (every roll
    /// succeeds) — a level gain crossing >= 3 thresholds must grant all three, not one.</summary>
    [Fact]
    public void ALevelGainCrossingMultipleThresholdsAttemptsOneRollPerLevelGained()
    {
        SeedAction("action.wiring-test.a");
        SeedAction("action.wiring-test.b");
        SeedAction("action.wiring-test.c");
        var actor = _store.CreateUniqueActor(playerId: 1, side: "plant", typeId: 1);

        var (ok, reason, updated) = _store.AwardUniqueActorXp(actor.InstanceId, delta: 100_000_000);
        Assert.True(ok, reason);
        var levelsGained = updated!.Level - 1;
        Assert.True(levelsGained >= 3, $"expected a huge XP delta to cross at least 3 levels; got {levelsGained}");

        var grants = _store.ListGrants(new OwnerScope(OwnerKind.UniqueActor, actor.InstanceId)); // matches WebMatchService.EquippedActionIdsFor's own real read scope (action-grant-owner-kind-durability, fixed 2026-09-07)
        Assert.Equal(3, grants.Count); // exactly the 3 available candidates, no more, no fewer
    }
}
