using System.Text.RegularExpressions;
using FusionRpg.Core.Effects.Atoms;
using FusionRpg.Core.Power;
using FusionRpg.Data;
using Xunit;

namespace FusionRpg.Data.Tests.Items;

/// <summary>
/// `armoury`'s DAL half: the stock counter, the event log, the loadout library and the abuse-guard
/// row ceiling. The query surface, comparison algorithm and salvage guards are Core, DB-free, and
/// tested in <c>FusionRpg.Core.Tests.Items</c> instead.
/// </summary>
public class ArmouryTests : IDisposable
{
    readonly string _dir;
    readonly RpgStore _store;

    public ArmouryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "fusionrpg-armoury-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _store = new RpgStore(_dir);
        _store.Init();
    }

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { /* temp dir */ }
    }

    static readonly PowerTuning Tuning = PowerTuning.Build(
        1, 1, 80_000, 0, 20, 680,
        1000, 25000, 250, 1000, 5000, 5000, 25000);

    /// <summary>
    /// `rpg_item`'s FK to `effect_instance` is a real, enforced constraint (`Microsoft.Data.Sqlite`
    /// defaults `PRAGMA foreign_keys` on) — every fixture needing an owned item needs a real instance
    /// first, matching what an actual acquisition always does.
    /// </summary>
    string SeedInstance()
    {
        Assert.True(_store.UpsertAtom(new AtomRow
        {
            AtomId = AtomRow.DeriveId("atom.armoury-test", "", 1),
            KindId = "stat.modify", FamilyId = "atom.armoury-test", Variant = "", Tier = 1,
            Name = "Armoury Test", ParamsJson = "{\"channel\":\"maxHp\",\"op\":\"flat\",\"amount\":10}",
        }).IsOk);
        Assert.True(_store.UpsertContainer(new ContainerRow
        {
            ContainerId = "item.armoury-test", Kind = ContainerKind.Item,
            Atoms = new[] { new ContainerAtomRow(1, "atom.armoury-test.t1") },
        }).IsOk);

        var container = _store.GetContainer("item.armoury-test")!;
        var atoms = _store.ListAtoms().ToDictionary(a => a.AtomId, StringComparer.Ordinal);
        var r = Instantiator.TryInstantiate(container,
            id => atoms.TryGetValue(id, out var a) ? a : null, _store.GetAffix, 1, 20, Tuning, out var inst);
        Assert.True(r.IsOk, r.ToString());

        return _store.SaveInstance(inst!);
    }

    // ---- stock: the counter that makes D1 affordable -------------------------------------------

    [Fact]
    public void Stock_items_are_a_counter_not_a_row_per_copy()
    {
        _store.AdjustStock("p1", "item.iron-band", delta: 5);
        _store.AdjustStock("p1", "item.iron-band", delta: 3);

        var stock = Assert.Single(_store.ListStock("p1"));
        Assert.Equal(8, stock.Qty);
    }

    [Fact]
    public void Stock_quantity_never_goes_negative()
    {
        _store.AdjustStock("p1", "item.iron-band", delta: 2);
        _store.AdjustStock("p1", "item.iron-band", delta: -10);

        var stock = Assert.Single(_store.ListStock("p1"));
        Assert.Equal(0, stock.Qty);
    }

    // ---- the abuse-guard row ceiling --------------------------------------------------------------

    [Fact]
    public void The_structural_row_ceiling_is_an_abuse_guard_and_says_so()
    {
        var text = File.ReadAllText(FindSourceFile("RpgStore.Items.cs"));
        var ceilingDecl = Regex.Match(text, @"const int InventoryCeiling = ([\d_]+);");
        Assert.True(ceilingDecl.Success, "InventoryCeiling constant not found");

        // The comment immediately above it must say WHY it is exempt from AGENTS.md's no-hard-
        // ceilings rule — a structural bug guard, not a progression cap. Required, not optional.
        var declIndex = ceilingDecl.Index;
        var precedingComment = text[Math.Max(0, declIndex - 900)..declIndex];
        Assert.Contains("abuse guard", precedingComment, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("never a progression", precedingComment, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void No_capacity_cap_exists_outside_the_named_abuse_guard()
    {
        // Grep-shaped guard (spec-drop-volume.md's own precedent): every file under the armoury's
        // DAL and Core surface may declare exactly one capacity-shaped constant, InventoryCeiling.
        var capNamePattern = new Regex(@"const\s+int\s+(\w*(?:Cap|Ceiling|MaxRows|RowLimit)\w*)\s*=",
            RegexOptions.IgnoreCase);

        var files = new List<string> { FindSourceFile("RpgStore.Items.cs") };
        var itemsDir = FindSourceDir("src/FusionRpg.Core/Items");
        files.AddRange(Directory.GetFiles(itemsDir, "*.cs", SearchOption.TopDirectoryOnly));

        var found = new List<string>();
        foreach (var f in files)
            foreach (Match m in capNamePattern.Matches(File.ReadAllText(f)))
                found.Add(m.Groups[1].Value);

        Assert.Equal(new[] { "InventoryCeiling" }, found);
    }

    static string FindSourceFile(string fileName)
    {
        var dir = FindRepoRoot();
        var matches = Directory.GetFiles(dir, fileName, SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}") &&
                        !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .ToList();
        Assert.True(matches.Count == 1, $"expected exactly one {fileName}, found {matches.Count}");
        return matches[0];
    }

    static string FindSourceDir(string relative)
    {
        var dir = Path.Combine(FindRepoRoot(), relative.Replace('/', Path.DirectorySeparatorChar));
        Assert.True(Directory.Exists(dir), $"expected directory {dir} to exist");
        return dir;
    }

    static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "src", "FusionRpg.Injector"))) return dir.FullName;
            dir = dir.Parent;
        }
        throw new DirectoryNotFoundException("repo root");
    }

    // ---- events: what makes "where did my item go" answerable -----------------------------------

    [Fact]
    public void Acquiring_an_item_writes_the_owner_row_and_an_acquired_event_in_one_call()
    {
        var instanceId = SeedInstance();

        var r = _store.AcquireItem(new RpgItemRow
        {
            InstanceId = instanceId, PlayerId = "p1", AcquiredUtc = "2026-01-01T00:00:00Z",
        });

        Assert.True(r.IsOk, r.ToString());
        Assert.NotNull(_store.GetItem(instanceId));

        var events = _store.ListItemEvents(instanceId);
        Assert.Single(events);
        Assert.Equal("acquired", events[0].Kind);
    }

    [Fact]
    public void A_fabricated_instance_id_is_refused_by_the_enforced_foreign_key()
    {
        // rpg_item's FK to effect_instance is real and enforced (Microsoft.Data.Sqlite defaults
        // PRAGMA foreign_keys on) -- an ownership row can never outlive or precede its instance.
        Assert.Throws<Microsoft.Data.Sqlite.SqliteException>(() => _store.SaveItem(new RpgItemRow
        {
            InstanceId = "does-not-exist", PlayerId = "p1", AcquiredUtc = "2026-01-01T00:00:00Z",
        }));
    }

    // ---- loadouts: the library ----------------------------------------------------------------

    [Fact]
    public void A_loadout_round_trips_with_its_entries()
    {
        _store.SaveLoadout(
            new RpgItemLoadoutRow("lo-1", "p1", "Offense", Frame: null, "2026-01-01T00:00:00Z", Revision: 0),
            new[]
            {
                new RpgItemLoadoutEntryRow("lo-1", "armament-primary", "item", "inst-1"),
                new RpgItemLoadoutEntryRow("lo-1", "core-guard", "stock", "item.iron-band"),
            });

        var loadout = Assert.Single(_store.ListLoadouts("p1"));
        Assert.Equal("Offense", loadout.Name);

        var entries = _store.GetLoadoutEntries("lo-1");
        Assert.Equal(2, entries.Count);
        Assert.Contains(entries, e => e.Role == "armament-primary" && e.RefKind == "item" && e.RefId == "inst-1");
        Assert.Contains(entries, e => e.Role == "core-guard" && e.RefKind == "stock" && e.RefId == "item.iron-band");
    }

    [Fact]
    public void Resaving_a_loadout_replaces_its_entries_rather_than_accumulating_them()
    {
        var loadout = new RpgItemLoadoutRow("lo-1", "p1", "Offense", null, "2026-01-01T00:00:00Z", 0);
        _store.SaveLoadout(loadout, new[] { new RpgItemLoadoutEntryRow("lo-1", "armament-primary", "item", "a") });
        _store.SaveLoadout(loadout, new[] { new RpgItemLoadoutEntryRow("lo-1", "armament-primary", "item", "b") });

        var entries = _store.GetLoadoutEntries("lo-1");
        var entry = Assert.Single(entries);
        Assert.Equal("b", entry.RefId);
    }
}
