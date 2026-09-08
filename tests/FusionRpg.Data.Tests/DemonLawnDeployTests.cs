using System.Text.Json;
using FusionRpg.Contracts;
using FusionRpg.Core.Demons;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Stats.Derived;
using FusionRpg.Data;
using Microsoft.Data.Sqlite;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// demon-lawn-deploy T1.2: a demon specimen's own rolled `TraitIds` reconcile into real
/// `effect_binding` rows (through the same `OwnerKind.UniqueActor` owner equipment already uses) on
/// EVERY deploy — proven end to end against the real shipped `trait.critical-hunter` seed pair
/// (`data/seed/atoms/trait-critical-hunter.json`, `data/seed/containers/trait-critical-hunter.json`),
/// the one real trait container this session already proved live-grant-backed via a real fusion
/// `/api/fusion/execute`. Mirrors `UniqueEquipmentAtomBindingTests.cs`'s own real-seed-tree pattern
/// exactly, for the demon-trait side of the same `OwnerKind.UniqueActor` mechanism.
/// </summary>
public class DemonLawnDeployTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public DemonLawnDeployTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-lawndeploy-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
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
        Assert.NotNull(_store.GetContainer("trait.critical-hunter"));
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

    static OwnerScope DemonOwner(string instanceId) => new(OwnerKind.UniqueActor, instanceId);

    // DeployMode != HypnoAlly: this file tests trait-binding reconciliation (T1.2), a concern
    // unrelated to DeployMode (T1.4) — excluding HypnoAlly keeps it robust to which species happens
    // to sort first, rather than incidentally also exercising T1.4's own
    // deploy.hypno-ally-not-implemented refusal.
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

    /// <summary>Drives ActiveBound back to Roster the same way a real match end does — via the real
    /// event-observation path, not a direct phase write. Mirrors
    /// `UniqueActorStoreTests.Observe_die_and_board_end_recover_to_Roster`'s own exact shape.</summary>
    void RecoverToRosterViaBoardEnd(string instanceId, string ptr, string matchKey)
    {
        _store.ObserveUniqueActorEvents(new (string Kind, string? MatchKey, string PayloadJson)[]
        {
            ("board.end", matchKey, "{}")
        });
        Assert.Equal(UniqueActorPhases.Roster, _store.GetUniqueActor(instanceId)!.Phase);
    }

    /// <summary>Simulates what `RpgStore.Fusion.cs`'s real `PromotionUnlocked` does to `traits_json` —
    /// an in-place update on the SAME instanceId — via a direct SQL write against the same db file,
    /// deliberately bypassing the full fusion economy (souls/materials/star-cap climb) that a real
    /// promotion needs, since THIS test's own subject is the reconciler's reaction to a changed
    /// `traits_json`, not promotion's own mechanics (already covered by `FusionStoreTests.cs`'s
    /// `Promotion_gates_on_max_stars_and_runs_once`).</summary>
    void SimulatePromotionTraitChange(string instanceId, IReadOnlyList<string> newTraitIds)
    {
        using var db = new SqliteConnection($"Data Source={Path.Combine(_dir, "rpg-hot.sqlite")}");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = "UPDATE rpg_demon_profiles SET traits_json = $t WHERE instance_id = $id;";
        cmd.Parameters.AddWithValue("$t", JsonSerializer.Serialize(newTraitIds));
        cmd.Parameters.AddWithValue("$id", instanceId);
        var rows = cmd.ExecuteNonQuery();
        Assert.Equal(1, rows);
    }

    [Fact]
    public void A_fresh_specimens_own_trait_binds_on_first_deploy()
    {
        var id = MintWithTraits(new[] { "critical-hunter" });

        var deploy = _store.TryBeginUniqueDeploy(id, "deploy-1");
        Assert.True(deploy.Ok, deploy.Reason);

        var bindings = _store.ListBindings(DemonOwner(id));
        Assert.Contains(bindings, b => b.Slot == "critical-hunter" && b.Source == "demon-trait");
    }

    [Fact]
    public void Redeploying_with_unchanged_traits_writes_no_new_binding()
    {
        var id = MintWithTraits(new[] { "critical-hunter" });
        _store.TryBeginUniqueDeploy(id, "deploy-1");
        var afterFirst = _store.ListBindings(DemonOwner(id))
            .Where(b => b.Source == "demon-trait").Select(b => b.BindingId).OrderBy(x => x).ToArray();

        _store.TryAckUniqueSpawn("deploy-1", "ptr-1", "m-1");
        RecoverToRosterViaBoardEnd(id, "ptr-1", "m-1");
        _store.TryBeginUniqueDeploy(id, "deploy-2");

        var afterSecond = _store.ListBindings(DemonOwner(id))
            .Where(b => b.Source == "demon-trait").Select(b => b.BindingId).OrderBy(x => x).ToArray();
        Assert.Equal(afterFirst, afterSecond); // same binding ids — nothing withdrawn or re-produced
    }

    [Fact]
    public void A_specimen_promoted_between_two_deploys_gets_the_new_traits_binding_on_redeploy()
    {
        var id = MintWithTraits(Array.Empty<string>());
        _store.TryBeginUniqueDeploy(id, "deploy-1");
        Assert.Empty(_store.ListBindings(DemonOwner(id)).Where(b => b.Source == "demon-trait"));

        _store.TryAckUniqueSpawn("deploy-1", "ptr-1", "m-1");
        RecoverToRosterViaBoardEnd(id, "ptr-1", "m-1");
        SimulatePromotionTraitChange(id, new[] { "critical-hunter" });

        _store.TryBeginUniqueDeploy(id, "deploy-2");

        var bindings = _store.ListBindings(DemonOwner(id));
        Assert.Contains(bindings, b => b.Slot == "critical-hunter" && b.Source == "demon-trait");
    }

    [Fact]
    public void A_trait_removed_from_the_specimen_has_its_binding_withdrawn_on_redeploy()
    {
        var id = MintWithTraits(new[] { "critical-hunter" });
        _store.TryBeginUniqueDeploy(id, "deploy-1");
        Assert.Contains(_store.ListBindings(DemonOwner(id)), b => b.Slot == "critical-hunter");

        _store.TryAckUniqueSpawn("deploy-1", "ptr-1", "m-1");
        RecoverToRosterViaBoardEnd(id, "ptr-1", "m-1");
        SimulatePromotionTraitChange(id, Array.Empty<string>());

        _store.TryBeginUniqueDeploy(id, "deploy-2");

        Assert.DoesNotContain(_store.ListBindings(DemonOwner(id)), b => b.Source == "demon-trait");
    }
}
