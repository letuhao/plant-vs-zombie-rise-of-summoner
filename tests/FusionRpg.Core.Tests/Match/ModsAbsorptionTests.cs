using System.Text.Json;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Match;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Core.Tests.Match;

/// <summary>
/// `mods-absorption` (module 5 of 10, `effect-pipeline`, `spec-mods-absorption.md`) acceptance —
/// the migration itself, not the groundwork <see cref="UniqueEquipmentAtomMappingTests"/> and
/// `UniqueEquipmentAtomBindingTests` (Data.Tests) already proved. Those files proved path 1
/// (<c>Instantiator</c> → <c>effect_binding</c>) is real; this file proves path 2
/// (<c>rpg_unique_stat_mods.mods_json</c>'s grant half) has actually stopped for the items path 1
/// now owns, with no window where both are live for the same slot (⛔ DECIDED 2026-09-03, the spec's
/// own atomic-per-actor cutover, not a read-through).
///
/// <para><b>Real seed tree, not invented fixtures</b> — the same
/// <c>data/seed/atoms/fx-*.json</c> + <c>data/seed/containers/unique-equip.json</c>
/// <see cref="UniqueEquipmentAtomBindingTests"/> imports, so a container miss here would be a real
/// regression, not a test-only illusion.</para>
/// </summary>
public class ModsAbsorptionTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;
    readonly long _playerId;

    public ModsAbsorptionTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-mods-absorption-" + Guid.NewGuid().ToString("N"));
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

    static JsonElement GrantsOf(string modsJson)
    {
        using var doc = JsonDocument.Parse(modsJson);
        return doc.RootElement.TryGetProperty("grants", out var g) ? g.Clone() : default;
    }

    [Fact]
    public void Equipping_an_item_produces_a_real_binding()
    {
        // "path 1 now live for equipment" — a real effect_binding row exists after equip, not merely
        // that some producer method was invoked.
        var a = _store.CreateUniqueActor(_playerId, "plant", 1);
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        var bindings = _store.ListBindings(UniqueOwner(a.InstanceId));
        var binding = Assert.Single(bindings);
        Assert.Equal("weapon", binding.Slot);

        var instance = _store.GetInstance(binding.InstanceId);
        Assert.NotNull(instance);
        Assert.Equal("item.fx-passive-atk-flat", instance!.ContainerId);
        Assert.Contains(instance.Atoms, at => at.AtomId == "atom.fx-passive-atk-flat.t1");

        Assert.Contains(eq.Items, x => x.Slot == "weapon" && x.ItemId == "stub.atk_ring");
    }

    [Fact]
    public void An_actor_never_carries_the_same_grant_through_both_paths()
    {
        // The double-grant invariant, proven mechanically: for an atom-backed slot, mods_json's grant
        // half is empty for that slot while effect_binding carries the real row — never both.
        var a = _store.CreateUniqueActor(_playerId, "zombie", 2);
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        // Nothing under fx.passive_atk_flat (or its stamped grantId) reached mods_json's grants.
        var grants = GrantsOf(eq.ModsJson);
        Assert.Equal(JsonValueKind.Array, grants.ValueKind);
        Assert.Equal(0, grants.GetArrayLength());
        Assert.DoesNotContain("fx.passive_atk_flat", eq.ModsJson, StringComparison.Ordinal);

        // effect_binding has the one real row instead.
        var bindings = _store.ListBindings(UniqueOwner(a.InstanceId));
        Assert.Single(bindings);
    }

    [Fact]
    public void Placeholder_items_with_no_real_atom_still_use_the_legacy_grant_and_carry_no_binding()
    {
        // The inverse of the invariant above, for the one item that has NOT moved: no atom exists for
        // fx.entity_atk (UniqueEquipmentCatalog's own doc comment calls it a placeholder), so the
        // legacy grant is still the only path — and it must remain exactly that, not silently drop.
        var a = _store.CreateUniqueActor(_playerId, "plant", 3);
        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.hp_charm");

        Assert.Empty(_store.ListBindings(UniqueOwner(a.InstanceId)));
        var grants = GrantsOf(eq.ModsJson);
        Assert.Equal(1, grants.GetArrayLength());
        Assert.Equal("fx.entity_atk", grants[0].GetProperty("effectId").GetString());
    }

    [Fact]
    public void Unequipping_removes_the_binding_not_just_the_mods_json_entry()
    {
        // Withdraw is symmetric: unequip must remove the real effect_binding row, not merely clear a
        // mods_json field that (post-cutover) was never carrying the grant to begin with.
        var a = _store.CreateUniqueActor(_playerId, "plant", 4);
        _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.atk_ring");
        var bound = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Equal(0, _store.CountOrphanInstances());

        var cleared = _store.ClearUniqueEquipmentSlot(a.InstanceId, "trinket");

        Assert.Empty(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Null(_store.GetInstance(bound.InstanceId)); // orphan-collected, not merely unbound
        Assert.DoesNotContain(cleared.Items, x => x.Slot == "trinket");
    }

    [Fact]
    public void Absolutes_and_flat_keys_are_unaffected()
    {
        // Scope discipline: only the grant half moves. Nested absolutes AND the flat root keys
        // BuildModsJson also carries survive an atom-backed equip byte-for-byte.
        var a = _store.CreateUniqueActor(_playerId, "zombie", 5);
        _store.UpsertUniqueStatModsJson(a.InstanceId, """{"absolutes":{"hp":500},"atk":40}""");

        var eq = _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");

        using var doc = JsonDocument.Parse(eq.ModsJson);
        var root = doc.RootElement;
        Assert.Equal(500, root.GetProperty("absolutes").GetProperty("hp").GetInt32());
        // BuildModsJson folds a pre-existing flat root key into the nested absolutes map (its own
        // documented contract, unchanged by this module) rather than re-emitting it at the root.
        Assert.Equal(40, root.GetProperty("absolutes").GetProperty("atk").GetInt32());

        // And the grant half genuinely did move — proving this isn't "nothing changed at all".
        Assert.Equal(0, GrantsOf(eq.ModsJson).GetArrayLength());
        Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
    }

    [Fact]
    public void Existing_save_data_migrates_without_a_stat_change()
    {
        // The highest-value test: a REAL save fixture — an actor whose mods_json still carries the
        // pre-cutover shape (the redundant grant BuildModsJson used to write for an atom-backed item,
        // exactly as a real player's row looked before this module shipped) and whose effect_binding
        // already exists (T6.1's reconcile groundwork ran on this actor's last equip, unaffected by
        // this module). CutoverUniqueEquipmentModsAbsorption is the per-actor atomic cutover
        // (spec-mods-absorption.md, ⛔ DECIDED 2026-09-03 — no read-through window).
        var a = _store.CreateUniqueActor(_playerId, "plant", 6);

        // Build the loadout the normal way first — this is what a real player's row looks like today,
        // atom binding included (T6.1 already reconciles on every equip, unaffected by this module).
        _store.UpsertUniqueEquipment(a.InstanceId, "weapon", "stub.atk_ring");
        _store.UpsertUniqueEquipment(a.InstanceId, "trinket", "stub.hp_charm"); // stays legacy — proves the mix survives too

        // Now overwrite mods_json with the PRE-CUTOVER shape a real save had before this fix: the
        // atom-backed grant re-added alongside the still-legitimate legacy grant and the absolutes —
        // literally what the old (unfiltered) BuildModsJson used to emit for this exact loadout.
        var preCutoverJson = JsonSerializer.Serialize(new Dictionary<string, object?>
        {
            ["absolutes"] = new Dictionary<string, int> { ["hp"] = 500, ["atk"] = 12 },
            ["grants"] = new object[]
            {
                new Dictionary<string, object?>
                {
                    ["grantId"] = "equip-stub-atk:weapon", ["effectId"] = "fx.passive_atk_flat",
                    ["ownerKind"] = "instance", ["ownerKey"] = "instance:pending",
                    ["pluginId"] = "unique.equip", ["priority"] = 0,
                },
                new Dictionary<string, object?>
                {
                    ["grantId"] = "equip-stub-hp:trinket", ["effectId"] = "fx.entity_atk",
                    ["ownerKind"] = "instance", ["ownerKey"] = "instance:pending",
                    ["pluginId"] = "unique.equip", ["priority"] = 0,
                },
            },
        });
        _store.UpsertUniqueStatModsJson(a.InstanceId, preCutoverJson);

        // ── BEFORE: capture what's observable pre-cutover ──────────────────────────────────────────
        var beforeMods = _store.GetUniqueStatModsJson(a.InstanceId);
        var beforeAbsolutes = UniqueLoadoutSpec.Parse(beforeMods).Absolutes
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var beforeGrantEffectIds = UniqueLoadoutSpec.Parse(beforeMods).Grants
            .Select(g => g.EffectId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var beforeWeaponBinding = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        var beforeWeaponInstance = _store.GetInstance(beforeWeaponBinding.InstanceId)!;
        var beforeWeaponAtomValues = beforeWeaponInstance.Atoms.Single().ValuesJson;

        // Confirms the fixture really is pre-cutover: BOTH the legacy grant AND the atom binding are
        // live for the weapon slot right now — the exact bug this module exists to close.
        Assert.Contains("fx.passive_atk_flat", beforeGrantEffectIds);
        Assert.Contains("fx.entity_atk", beforeGrantEffectIds);
        Assert.Equal(2, beforeGrantEffectIds.Length);

        // ── RUN THE CUTOVER ─────────────────────────────────────────────────────────────────────────
        var touched = _store.CutoverUniqueEquipmentModsAbsorption();
        Assert.True(touched >= 1);

        // ── AFTER: capture the same observables post-cutover ───────────────────────────────────────
        var afterMods = _store.GetUniqueStatModsJson(a.InstanceId);
        var afterAbsolutes = UniqueLoadoutSpec.Parse(afterMods).Absolutes
            .ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        var afterGrantEffectIds = UniqueLoadoutSpec.Parse(afterMods).Grants
            .Select(g => g.EffectId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var afterWeaponBinding = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        var afterWeaponInstance = _store.GetInstance(afterWeaponBinding.InstanceId)!;
        var afterWeaponAtomValues = afterWeaponInstance.Atoms.Single().ValuesJson;

        // Effective stats, defined as every numeric magnitude Core/Data can observe for this actor:
        // (1) the absolutes map — byte-identical, this module never touches it. Compared both as a
        // whole-map equality and key by key, so a subtle key-casing/count drift can't hide behind a
        // dictionary comparer's own semantics.
        Assert.Equal(beforeAbsolutes, afterAbsolutes);
        Assert.Equal(beforeAbsolutes.Count, afterAbsolutes.Count);
        Assert.Equal(2, afterAbsolutes.Count);
        Assert.Equal(500, beforeAbsolutes["hp"]);
        Assert.Equal(12, beforeAbsolutes["atk"]);
        Assert.Equal(500, afterAbsolutes["hp"]);
        Assert.Equal(12, afterAbsolutes["atk"]);

        // (2) the weapon slot's real magnitude — the SAME instance, the SAME frozen atom values,
        // completely untouched by the cutover (ReconcileUniqueEquipmentAtomBindingsUnlocked's own
        // idempotence: a binding that already points at the wanted container is left alone).
        Assert.Equal(beforeWeaponBinding.BindingId, afterWeaponBinding.BindingId);
        Assert.Equal(beforeWeaponBinding.InstanceId, afterWeaponBinding.InstanceId);
        Assert.Equal(beforeWeaponAtomValues, afterWeaponAtomValues);
        Assert.Contains("\"amount\":10", afterWeaponAtomValues, StringComparison.Ordinal);

        // (3) the trinket slot's legacy grant (fx.entity_atk, no real atom) is still exactly one
        // grant — this module does not touch the still-legitimate legacy path.
        Assert.Single(afterGrantEffectIds);
        Assert.Equal("fx.entity_atk", afterGrantEffectIds[0]);

        // The ONLY observed change: the redundant weapon-slot grant is gone. Total magnitude reaching
        // the actor through equipment is unchanged (still exactly the atom's +10 atk, once); only the
        // second, redundant application of that same fact has been removed.
        Assert.DoesNotContain("fx.passive_atk_flat", afterMods, StringComparison.Ordinal);

        // Idempotent: running the cutover again changes nothing further.
        var again = _store.CutoverUniqueEquipmentModsAbsorption();
        Assert.True(again >= 1);
        Assert.Equal(afterMods, _store.GetUniqueStatModsJson(a.InstanceId));
        var stillOneBinding = Assert.Single(_store.ListBindings(UniqueOwner(a.InstanceId)));
        Assert.Equal(afterWeaponBinding.BindingId, stillOneBinding.BindingId);
    }
}
