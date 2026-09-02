using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Match;
using Xunit;

namespace FusionRpg.Core.Tests.Match;

/// <summary>
/// T6.1 (`mods-absorption`) — real, tested groundwork, honestly separated from the part that stays
/// blocked. This session found the earlier "no atom exists for these effect ids" conclusion was
/// wrong (`data/seed/atoms/fx-core.json`/`fx-status.json` already carry real atoms for
/// `fx.passive_atk_flat`/`fx.butter_on_hit`/`fx.shield_grant`/`fx.cold_on_hit` — `EffectAtomCatalog.
/// Generated.cs`'s own header already proves they round-trip through `AtomCompiler`), and built
/// `data/seed/containers/unique-equip.json` plus <see cref="UniqueEquipmentCatalog.TryGetAtomBackedContainerId"/>
/// to make that real. What stopped here, found only by actually attempting the write-path wiring:
/// no <see cref="OwnerKind"/> value fits a PERSISTENT `rpg_unique_actor` (`Entity` is explicitly
/// session-scoped and cleared on session end — using it would silently wipe equipment bindings every
/// session; `Player`/`Plant`/`Zombie`/`Sector`/`Slot`/`Match` don't fit either) — `OwnerScope.cs`'s
/// own doc comment calls these "the seven owner scopes a binding may attach to," a closed, reviewed
/// set, the same class of boundary as `CurveInput`'s own "Ask first: adding a curve input." Adding
/// an eighth is a real design decision this task does not make. This file proves what IS real today.
/// </summary>
public class UniqueEquipmentAtomMappingTests
{
    static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    [Theory]
    [InlineData("stub.atk_ring", "item.fx-passive-atk-flat")]
    [InlineData("stub.butter_bead", "item.fx-butter-on-hit")]
    [InlineData("relic.ashen_reliquary", "item.fx-passive-atk-flat")] // shares atk_ring's own EffectId
    [InlineData("relic.sunworn_charm", "item.fx-shield-grant")]
    [InlineData("relic.tidewrack_band", "item.fx-cold-on-hit")]
    public void Items_with_a_real_atom_resolve_to_the_real_container_id(string itemId, string expectedContainerId)
    {
        Assert.True(UniqueEquipmentCatalog.TryGetAtomBackedContainerId(itemId, out var containerId));
        Assert.Equal(expectedContainerId, containerId);
    }

    [Theory]
    [InlineData("stub.hp_charm")] // fx.entity_atk — its own doc comment already calls it a placeholder
    [InlineData("relic.cracked_seal")] // same placeholder EffectId
    public void Items_whose_effect_has_no_real_atom_stay_on_the_legacy_path(string itemId)
    {
        Assert.False(UniqueEquipmentCatalog.TryGetAtomBackedContainerId(itemId, out _));
    }

    [Fact]
    public void An_unknown_item_id_is_never_atom_backed()
    {
        Assert.False(UniqueEquipmentCatalog.TryGetAtomBackedContainerId("not-a-real-item", out _));
    }

    [Fact]
    public void The_new_container_seed_file_parses_and_every_atom_it_references_is_present_in_the_real_atom_seed()
    {
        // Real end to end: read data/seed/containers/unique-equip.json through the SAME
        // AtomSeedFile.Collect entry point AtomImporter itself uses, alongside the real
        // fx-core.json/fx-status.json atom seed files — proves the four containers this task
        // added actually resolve against real, already-shipped atom content, not an invented id.
        var root = RepoRoot();
        var files = new (string Path, string Json)[]
        {
            (Path.Combine(root, "data", "seed", "atoms", "fx-core.json"),
                File.ReadAllText(Path.Combine(root, "data", "seed", "atoms", "fx-core.json"))),
            (Path.Combine(root, "data", "seed", "atoms", "fx-status.json"),
                File.ReadAllText(Path.Combine(root, "data", "seed", "atoms", "fx-status.json"))),
            (Path.Combine(root, "data", "seed", "containers", "unique-equip.json"),
                File.ReadAllText(Path.Combine(root, "data", "seed", "containers", "unique-equip.json"))),
        };

        var result = AtomSeedFile.Collect(files);

        Assert.True(result.IsOk, string.Join("; ", result.Errors));
        var containerIds = result.Content.Containers.Select(c => c.ContainerId).ToHashSet(StringComparer.Ordinal);
        Assert.Contains("item.fx-passive-atk-flat", containerIds);
        Assert.Contains("item.fx-butter-on-hit", containerIds);
        Assert.Contains("item.fx-shield-grant", containerIds);
        Assert.Contains("item.fx-cold-on-hit", containerIds);

        var atomIds = result.Content.Atoms.Select(a => a.AtomId).ToHashSet(StringComparer.Ordinal);
        var shieldGrant = result.Content.Containers.Single(c => c.ContainerId == "item.fx-shield-grant");
        Assert.Equal(3, shieldGrant.Atoms.Count); // the a/b/c coordinated bundle, all three present
        foreach (var containerId in containerIds)
        {
            var container = result.Content.Containers.Single(c => c.ContainerId == containerId);
            Assert.All(container.Atoms, a => Assert.Contains(a.AtomId, atomIds));
        }
    }
}
