using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>demon-lawn-deploy T1.1: the active Patron must never also get free lawn combat value on
/// top of its aura (RpgStore.Fusion.cs:354 already refuses it as a fusion sacrifice for the identical
/// reason). Commander is NOT tested here — CommanderId (Core/Commanders/CommanderId.cs) is a fixed
/// Dave/Zomboss enum with no demon-instance binding at all today, so there is no state yet that means
/// "this specimen is the Commander" to refuse against; see the comment at the real call site
/// (RpgStore.UniqueActors.cs, TryBeginUniqueDeploy) for the full reasoning.</summary>
public class DemonLawnDeployCommanderRefusalTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public DemonLawnDeployCommanderRefusalTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-lawndeploy-refusal-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    // DeployMode != HypnoAlly: this file tests Patron refusal (T1.1), a concern unrelated to
    // DeployMode (T1.4) — excluding HypnoAlly keeps it robust to which species happens to sort first,
    // rather than incidentally also exercising T1.4's own deploy.hypno-ally-not-implemented refusal.
    static readonly DemonSpeciesDef Species = DemonSpeciesCatalog.All
        .First(s => s.Side == "zombie" && s.Acquisition != DemonAcquisition.CaptureOnly
            && s.DeployMode != DemonDeployMode.HypnoAlly);

    string Mint()
    {
        var (specimen, _) = _store.MintDemon(1, new DemonMintSpec
        {
            SpeciesId = Species.SpeciesId,
            Side = Species.Side,
            GameTypeId = Species.GameTypeId,
            Rarity = Species.BaseRarity.ToId(),
            Variant = "normal",
            ElementPrimary = Species.ElementPrimary.ToElementId(),
            ElementSecondary = Species.ElementSecondary?.ToElementId(),
            TraitIds = new List<string> { Species.TraitPool[0] },
            Origin = "summon"
        });
        return specimen.Actor.InstanceId;
    }

    [Fact]
    public void The_active_patron_refuses_deploy_with_a_named_reason()
    {
        var patron = Mint();
        var set = _store.SetPatron(1, patron, "patron-corr-1");
        Assert.True(set.Ok, set.Reason);

        var deploy = _store.TryBeginUniqueDeploy(patron, "deploy-corr-1");

        Assert.False(deploy.Ok);
        Assert.Equal("patron.cannot-deploy", deploy.Reason);
        Assert.False(deploy.Queued);
    }

    [Fact]
    public void A_demon_that_is_not_the_active_patron_deploys_normally()
    {
        var patron = Mint();
        _store.SetPatron(1, patron, "patron-corr-2");
        var other = Mint();

        var deploy = _store.TryBeginUniqueDeploy(other, "deploy-corr-2");

        Assert.True(deploy.Ok, deploy.Reason);
        Assert.True(deploy.Queued);
    }

    [Fact]
    public void Switching_the_patron_away_lets_the_old_patron_deploy_again()
    {
        var oldPatron = Mint();
        _store.AwardSouls(1, 500, "seed", "patron-switch-bank");
        _store.SetPatron(1, oldPatron, "patron-corr-3");
        var newPatron = Mint();
        var switched = _store.SetPatron(1, newPatron, "patron-corr-4");
        Assert.True(switched.Ok, switched.Reason);

        var deploy = _store.TryBeginUniqueDeploy(oldPatron, "deploy-corr-3");

        Assert.True(deploy.Ok, deploy.Reason);
        Assert.True(deploy.Queued);
    }
}
