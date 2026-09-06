using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Server.Tests;

/// <summary>
/// demon-lawn-deploy T1.2's own named minor gap, closed: the reconcile tests
/// (`FusionRpg.Data.Tests/DemonLawnDeployTests.cs`) only ever asserted the `effect_binding` ROWS a
/// deploy produces — never the actual <see cref="AtomPushService.Build(OwnerScope, BindContext, ulong,
/// string?, long?, int?, int?)"/> output a live match would receive for that owner. This is the wire-
/// level half, mirroring <see cref="MultiOwnerPushTests"/>'s own "prove the compiled push is correct
/// BEFORE reaching the lawn" pattern for the demon-trait case specifically.
/// </summary>
public class DemonLawnDeployAtomPushTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly AtomPushService _push;

    public DemonLawnDeployAtomPushTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-lawndeploy-push-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _push = new AtomPushService(_store);
        ImportRealTraitSeed();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    void ImportRealTraitSeed()
    {
        var root = RepoRoot();
        var files = new[]
        {
            Path.Combine(root, "data", "seed", "atoms", "trait-critical-hunter.json"),
            Path.Combine(root, "data", "seed", "containers", "trait-critical-hunter.json"),
        }.Select(f => (f, File.ReadAllText(f))).ToArray();

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

        var outcome = _store.ImportContent(collected.Content);
        Assert.True(outcome.Committed, string.Join("; ", outcome.Errors));
    }

    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "data", "seed", "atoms"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new InvalidOperationException("could not find repo root (no data/seed/atoms above test bin)");
    }

    // DeployMode != HypnoAlly: this file's subject is the atom-push wire payload for a demon-trait
    // binding, unrelated to T1.4's DeployMode — a HypnoAlly pick would refuse the deploy this test needs
    // to succeed before it can even reach the reconcile step under test.
    static readonly DemonSpeciesDef Species = DemonSpeciesCatalog.All
        .First(s => s.Side == "zombie" && s.Acquisition != DemonAcquisition.CaptureOnly
            && s.DeployMode != DemonDeployMode.HypnoAlly);

    string MintWithTraits(IReadOnlyList<string> traitIds)
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
            TraitIds = traitIds.ToList(),
            Origin = "summon"
        });
        return specimen.Actor.InstanceId;
    }

    static BindContext Lawn() => new(RuntimeId.Lawn);

    // A compiled grant's EffectId is the atom's own EffectiveIcdKey (AtomCompiler.cs:77,174,205), NOT
    // AtomRow.DeriveId(family, variant, tier) — trait-critical-hunter.json's real seed content
    // explicitly authors "icdKey": "trait.critical-hunter.crit", so that string is what the compiled
    // grant carries. MultiOwnerPushTests.cs's own hand-authored atoms never set an icdKey, which is why
    // THEIR grants fall back to DeriveId — a different atom shape, not a contradiction.
    const string CriticalHunterEffectId = "trait.critical-hunter.crit";

    [Fact]
    public void A_demon_specimens_reconciled_trait_binding_reaches_the_real_atom_push_payload()
    {
        var id = MintWithTraits(new[] { "critical-hunter" });
        var deploy = _store.TryBeginUniqueDeploy(id, "deploy-push-1");
        Assert.True(deploy.Ok, deploy.Reason);

        var payload = _push.Build(new OwnerScope(OwnerKind.UniqueActor, id), Lawn(), matchSeed: 7);

        var grant = Assert.Single(payload.Grants, g => g.EffectId == CriticalHunterEffectId);
        Assert.Equal("instance:" + id, grant.OwnerKey);
        Assert.Equal(EffectOwnerKeys.InstanceKind, grant.OwnerKind);
    }

    [Fact]
    public void A_specimen_with_no_traits_produces_no_grant_for_that_owner()
    {
        var id = MintWithTraits(Array.Empty<string>());
        var deploy = _store.TryBeginUniqueDeploy(id, "deploy-push-2");
        Assert.True(deploy.Ok, deploy.Reason);

        var payload = _push.Build(new OwnerScope(OwnerKind.UniqueActor, id), Lawn(), matchSeed: 7);

        Assert.DoesNotContain(payload.Grants, g => g.EffectId == CriticalHunterEffectId);
    }
}
