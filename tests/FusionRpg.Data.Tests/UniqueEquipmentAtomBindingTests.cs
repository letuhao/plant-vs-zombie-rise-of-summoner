using FusionRpg.Core.Effects.Atoms;
using Xunit;

namespace FusionRpg.Data.Tests;

/// <summary>
/// `mods-absorption` (T6.1, `tasks/seed-to-concrete-todo.md`, decision 1 —
/// `tasks/seed-to-concrete-open-decisions.md`) acceptance: equipping an atom-backed item now produces
/// a real <c>effect_binding</c> row through <see cref="OwnerKind.UniqueActor"/>, not just the legacy
/// <c>mods_json</c> blob — proven end to end against the REAL shipped seed tree
/// (<c>data/seed/atoms/fx-*.json</c>, <c>data/seed/containers/unique-equip.json</c>), the same files
/// <c>EffectCatalogExecutionParityTests</c> (Core) already proves round-trip through
/// <see cref="AtomCompiler"/>.
/// </summary>
public class UniqueEquipmentAtomBindingTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly long _playerId;

    public UniqueEquipmentAtomBindingTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-unique-atoms-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
        _playerId = _store.GetCurrentPlayerId();
        ImportRealSeedTree();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, true); } catch { /* temp */ }
    }

    void ImportRealSeedTree()
    {
        var atomsDir = Path.Combine(RepoRoot(), "data", "seed", "atoms");
        var containersDir = Path.Combine(RepoRoot(), "data", "seed", "containers");
        var files = Directory.GetFiles(atomsDir, "fx-*.json", SearchOption.AllDirectories)
            .Concat(new[] { Path.Combine(containersDir, "unique-equip.json") })
            .OrderBy(f => f, StringComparer.Ordinal)
            .Select(f => (f, File.ReadAllText(f)))
            .ToArray();

        var collected = AtomSeedFile.Collect(files);
        Assert.True(collected.IsOk, string.Join("; ", collected.Errors));

        var outcome = _store.ImportContent(collected.Content);
        Assert.True(outcome.Committed, string.Join("; ", outcome.Errors));
        Assert.NotNull(_store.GetContainer("item.fx-passive-atk-flat"));
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

    static OwnerScope UniqueOwner(string instanceId) => new(OwnerKind.UniqueActor, instanceId);

    [Fact]
    public void Equipping_an_atom_backed_item_binds_through_OwnerKind_UniqueActor()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        var bindings = _store.ListBindings(UniqueOwner(a.InstanceId));
        Assert.Single(bindings);
        Assert.Equal("weapon", bindings[0].Slot);
        Assert.Equal(OwnerKind.UniqueActor, bindings[0].OwnerKind);
        Assert.Equal(a.InstanceId, bindings[0].OwnerKey);
    }

    [Fact]
    public void The_bound_instance_carries_the_atoms_own_real_stat_unscaled_at_the_content_scale_pin()
    {
        // fx-core.json's real row: atom.fx-passive-atk-flat.t1, {"channel":"atk","op":"flat","amount":10}.
        // The pin (Θc=20) makes contentScale exactly ×1.000, so the frozen value must still read 10 —
        // proving round-trip fidelity, not just "a binding exists".
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        var binding = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        var instance = _store.GetInstance(binding.InstanceId);
        Assert.NotNull(instance);
        var atom = Assert.Single(instance!.Atoms);
        Assert.Equal("atom.fx-passive-atk-flat.t1", atom.AtomId);
        Assert.Contains("\"amount\":10", atom.ValuesJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Re_equipping_the_same_item_in_the_same_slot_writes_no_second_binding()
    {
        // The double-grant invariant: reconciliation is idempotent when the loadout hasn't changed.
        var a = _store.CreateUniqueActor(_playerId, "zombie", 2);
        _store.UpsertUniqueEquipment(a.InstanceId, "armor", "stub.atk_ring");
        var first = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));

        _store.UpsertUniqueEquipment(a.InstanceId, "armor", "stub.atk_ring");
        var second = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));

        Assert.Equal(first.BindingId, second.BindingId);
        Assert.Equal(first.InstanceId, second.InstanceId);
    }

    [Fact]
    public void Unequipping_withdraws_the_binding_and_orphans_its_instance()
    {
        var a = _store.CreateUniqueActor(_playerId, "plant", 3);
        _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.atk_ring");
        var bound = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Equal(0, _store.CountOrphanInstances());

        _store.ClearUniqueEquipmentSlot(a.InstanceId, "trinket");

        Assert.Empty(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Null(_store.GetInstance(bound.InstanceId));
    }

    [Fact]
    public void Swapping_to_a_different_atom_backed_item_in_the_same_slot_replaces_the_binding()
    {
        var a = _store.CreateUniqueActor(_playerId, "zombie", 4);
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");
        var before = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));

        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.butter_bead");
        var after = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));

        Assert.NotEqual(before.BindingId, after.BindingId);
        Assert.NotEqual(before.InstanceId, after.InstanceId);
        Assert.Null(_store.GetInstance(before.InstanceId)); // the stale one is gone, not merely unbound
        var newInstance = _store.GetInstance(after.InstanceId)!;
        Assert.Equal("item.fx-butter-on-hit", newInstance.ContainerId);
    }

    [Fact]
    public void A_placeholder_item_with_no_real_atom_stays_on_the_legacy_mods_json_path()
    {
        // stub.hp_charm grants fx.entity_atk, which no seed atom defines — TryGetAtomBackedContainerId
        // returns false for it, so reconciliation must not attempt (and fail on) a real atom bind.
        var a = _store.CreateUniqueActor(_playerId, "plant", 5);
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.hp_charm");

        Assert.Empty(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Contains("fx.entity_atk", eq.ModsJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ResolveBindings_for_the_UniqueActor_owner_surfaces_the_equipped_atom()
    {
        // Real save-fixture proof: the same owner scope a combat host would resolve at bind time
        // returns the equipped item's atom, unrefused.
        var a = _store.CreateUniqueActor(_playerId, "plant", 6);
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        var resolution = _store.ResolveBindings(UniqueOwner(a.InstanceId), new BindContext(RuntimeId.Lawn));

        Assert.Single(resolution.Bindings);
        Assert.Empty(resolution.Refused);
        var atoms = resolution.AtomsByBinding![resolution.Bindings[0].BindingId];
        Assert.Contains(atoms, r => r.AtomId == "atom.fx-passive-atk-flat.t1");
    }
}
