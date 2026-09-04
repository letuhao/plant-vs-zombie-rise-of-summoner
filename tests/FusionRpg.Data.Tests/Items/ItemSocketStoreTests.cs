using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Items;
using FusionRpg.Core.Items.Sockets;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests.Items;

/// <summary>
/// spec-sockets.md §5.2 / D2 §6 — <c>item_socket</c> is the SSOT, and the recipe tables are a
/// multiset (D41). Against a real SQLite store, not a mock.
/// </summary>
public class ItemSocketStoreTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ItemSocketStoreTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-sockets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    /// <summary>
    /// A REAL <c>effect_instance</c> row. <c>item_socket.instance_id</c> carries a live foreign key
    /// with <c>ON DELETE CASCADE</c>, so a socket cannot exist without a host — writing against a
    /// made-up id throws, which is the constraint doing its job.
    /// </summary>
    string NewHost() => _store.SaveInstance(new InstanceRow
    {
        ContainerId = "item.bark-plating",
        RollSeed = 8812349,
        CatalogRevision = _store.GetCatalogRevision(),
        Origin = InstanceOrigin.Drop,
        Atoms = new[] { new InstanceAtomRow(1, AtomRow.DeriveId("atom.vitality", "", 1), """{"amount":45}""") },
    });

    static SocketTuning Tuning()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md"))) dir = Path.GetDirectoryName(dir);
        return SocketTuning.Parse(File.ReadAllText(Path.Combine(dir!, "data", "tuning", "sockets.v1.json")));
    }

    [Fact]
    public void Sockets_round_trip_with_their_affinity_crafted_flag_and_contents()
    {
        var rows = new List<SocketSlot>
        {
            new(0, "earth", Crafted: false, "gem.stone-heart.t3", "inst-a"),
            new(1, "", Crafted: true, null, null),
        };

        var host = NewHost();
        _store.SetSockets(host, rows);
        var read = _store.GetSockets(host);

        Assert.Equal(2, read.Count);
        Assert.Equal("earth", read[0].Affinity);
        Assert.False(read[0].Crafted);
        Assert.Equal("gem.stone-heart.t3", read[0].InsertContainerId);
        Assert.Equal("inst-a", read[0].InsertInstanceId);
        Assert.True(read[1].Crafted);
        Assert.True(read[1].IsEmpty);
    }

    [Fact]
    public void Item_socket_is_the_ssot_and_no_read_path_replays_the_op_log()
    {
        // C2 / D2 §6, asserted directly against ssot-sockets.md §5.2's superseded claim: GetSockets
        // takes only an instance id and reaches no operation log. If socket state were derived from
        // effect_instance_op, an item with rows and no ops would read back empty.
        var host = NewHost();
        _store.SetSockets(host, new List<SocketSlot> { new(0, "fire", true, "gem.ember-shard.t3", null) });

        Assert.Empty(_store.ReadMutationOps(host));
        Assert.Single(_store.GetSockets(host));

        var parameters = typeof(RpgStore).GetMethod(nameof(RpgStore.GetSockets))!.GetParameters();
        Assert.Single(parameters);
        Assert.Equal(typeof(string), parameters[0].ParameterType);
    }

    [Fact]
    public void A_sparse_socket_list_is_refused_rather_than_stored()
    {
        var gap = new List<SocketSlot> { new(0, "", false), new(2, "", false) };
        Assert.Throws<ArgumentException>(() => _store.SetSockets(NewHost(), gap));
    }

    [Fact]
    public void Setting_sockets_replaces_the_whole_row_set()
    {
        var host = NewHost();
        _store.SetSockets(host, new List<SocketSlot> { new(0, "", false, "gem.a.t1", null), new(1, "", false) });
        _store.SetSockets(host, new List<SocketSlot> { new(0, "ice", true) });

        var read = _store.GetSockets(host);
        Assert.Single(read);
        Assert.Equal("ice", read[0].Affinity);
        Assert.True(read[0].IsEmpty);
    }

    [Fact]
    public void The_generated_twenty_five_seed_and_read_back_in_evaluation_order()
    {
        var tuning = Tuning();
        _store.SeedComboRecipes(ResonanceGenerator.Generate(tuning));

        var read = _store.GetComboRecipes();
        Assert.Equal(25, read.Count);
        Assert.Equal(18, read.Count(r => r.Shape == ComboShape.Pure));
        Assert.Equal(4, read.Count(r => r.Shape == ComboShape.Ring));
        Assert.Single(read, r => r.Shape == ComboShape.Eclipse);
        Assert.Equal(2, read.Count(r => r.Shape == ComboShape.Diversity));

        var shapes = read.Select(r => (int)r.Shape).ToList();
        Assert.Equal(shapes.OrderBy(s => s), shapes);

        // Idempotent: a second boot neither duplicates nor drops.
        _store.SeedComboRecipes(ResonanceGenerator.Generate(tuning));
        Assert.Equal(25, _store.GetComboRecipes().Count);
    }

    [Fact]
    public void A_strain_recipe_stores_its_ingredients_as_an_unordered_multiset()
    {
        // D41: the key is (combo_id, family_id, min_tier) with a quantity — there is no position
        // column to read, so a matcher cannot become order-sensitive by accident.
        var strain = new ComboRecipe(
            "combo.strain-test", ComboShape.Strain, "", 0, "armament-primary", "", 4, 2,
            new[]
            {
                new ComboIngredient("atom.elemental-power", 3, 3),
                new ComboIngredient("atom.vitality", 2, 1),
            });

        _store.SeedComboRecipes(new[] { strain });
        var read = Assert.Single(_store.GetComboRecipes());

        Assert.Equal("combo.strain-test", read.ComboId);
        Assert.Equal(ComboShape.Strain, read.Shape);
        Assert.Equal(2, read.Ingredients.Count);
        Assert.Equal(4, read.Ingredients.Sum(i => i.Quantity));
        Assert.DoesNotContain(
            typeof(ComboIngredient).GetProperties(), p => p.Name.Contains("Position", StringComparison.Ordinal));
    }

    [Fact]
    public void Socket_min_and_socket_max_seed_onto_every_rung_as_rarity_budget_rows()
    {
        var tuning = Tuning();
        _store.SeedRarityLadder(SampleRarityTuning());
        _store.SeedSocketGrants(tuning);

        foreach (var rung in RarityLadder.RungIds)
        {
            var window = tuning.RarityGrant[rung];
            Assert.Equal(window.Min, _store.GetRarityBudget(rung, "socket_min"));
            Assert.Equal(window.Max, _store.GetRarityBudget(rung, "socket_max"));
        }

        // Idempotent on every boot.
        _store.SeedSocketGrants(tuning);
        Assert.Equal(tuning.RarityGrant["almanac"].Max, _store.GetRarityBudget("almanac", "socket_max"));
    }

    [Fact]
    public void Socketing_writes_no_row_the_host_instance_owns()
    {
        // spec-sockets.md §1's table, as a test over the real schema: the only tables SetSockets
        // touches are item_socket's. effect_instance / effect_instance_atom are untouched, which is
        // what leaves InstanceRow.ContentFingerprint() byte-identical.
        var host = NewHost();
        var before = _store.GetInstanceMutationHead(host);

        _store.SetSockets(host, new List<SocketSlot>
        {
            new(0, "fire", false, "gem.ember-shard.t3", null),
            new(1, "fire", true, "gem.ember-shard.t3", null),
        });

        var after = _store.GetInstanceMutationHead(host);
        Assert.Equal(before!.MutationSeq, after!.MutationSeq);
        Assert.Equal(before.StateHash, after.StateHash);
        Assert.Equal(before.EnhanceLevel, after.EnhanceLevel);
        Assert.Empty(_store.ReadMutationOps(host));
        Assert.Equal(2, _store.GetSockets(host).Count);
    }

    static IReadOnlyDictionary<string, ItemRarityRungTuning> SampleRarityTuning()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null && !File.Exists(Path.Combine(dir, "AGENTS.md"))) dir = Path.GetDirectoryName(dir);
        return ItemRarityTuning.Parse(File.ReadAllText(Path.Combine(dir!, "data", "tuning", "item-rarity.v1.json")));
    }
}
